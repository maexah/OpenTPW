using NeoVeldrid;

namespace OpenTPW.UI;

/// <summary>
/// The interface's windows, one over another, and what the pointer and the keys do to them.
///
/// <para>
/// A click is a press and a release on the same control. Only the front window takes the pointer
/// when it is modal; otherwise the pointer goes to whatever is front-most under it, in any window. A park's screens
/// are not modal, and one is open at a time - see <see cref="UiWindow.ParkScreen"/>.
/// </para>
/// <para>
/// The original sets this up once, in UI_Init (0x00489ca0, reached only from Boot_Init through 0x004813c0):
/// the root every window hangs off (0x006584df) and the hook that clicks for every button (DAT_00faa5fc).
/// The game menu (0x0048c830), the message box (0x0047f020) and the options screen (0x004a3a30) are the
/// same functions in the lobby and in a park, and keys reach a window the same way in both - through the
/// box it is typing into, or the control last clicked (0x006698e6). With nothing to type into, the keys are
/// the scene's, and both act on a key's release: the lobby's go to its own root control, which hands them to the
/// island camera (0x005e41c0; see FrontEnd.LobbyKeys), and a park's to its binding tables (0x00488921; see
/// ParkFrontEnd.ParkKeys) unless a park screen in front has taken the focus - both through
/// <see cref="KeysWithoutFocus"/>. Here a stack is made with each scene's
/// HUD, and what should outlive a scene - the meshes, fonts and sounds its windows use - is cached apart
/// from it.
/// </para>
/// <para>
/// It is a panel on the HUD, so F2 hides it with everything else there. The park's full-screen view, F3, is
/// <see cref="Covered"/>.
/// </para>
/// <para>
/// <b>Engine and content.</b> All of it is engine: opening and closing windows, the hit test and the modal
/// stop, hover, press and release and the click, the wheel, focus and typing, the help bar, the button
/// glints, and each window's particle effects drawn with it. The windows themselves, and what Escape does
/// with nothing to type into, are the scene's.
/// </para>
/// </summary>
internal sealed class WindowStack : Panel
{
	private readonly List<UiWindow> _windows = new();
	private readonly HelpBar _helpBar;
	private readonly ButtonGlint _glint = new();

	private UiControl? _hovered;
	private UiControl? _pressed;
	private bool _mouseWasDown;
	private bool _rightWasDown;

	/// <summary>
	/// How long a press may be held and still make a click: 500 ms (<c>[0x0077c480]</c>) of real time
	/// (<see cref="Time.WallMilliseconds"/>), from the press to the release.
	/// </summary>
	internal const long ClickLimit = 500;

	/// <summary>
	/// How far the pointer may stray from where the press went down, across or down, in the interface's 2048x1536
	/// units, and the press still make a click: 6, and 7 is a drag (<c>0x0065fab7</c>).
	/// </summary>
	internal const float ClickStray = 6f;

	/// <summary>
	/// The right button's press, as the base control proc keeps it for button 1: whether it is unspoiled (state 1,
	/// neither a double click's second nor strayed), where it went down in interface units, the control it landed on,
	/// and what answers its click, which the scene says at the press (<see cref="ViewRightClick"/>).
	/// </summary>
	private (bool Unspoiled, Vector2 Where, UiControl? On, Action? Answer) _rightPress;

	/// <summary>
	/// The right button's one time stamp, which the base proc keeps for the button and not for a control
	/// (<c>0x00faa5ac</c>): the press's time while it is held, the release's once an unspoiled press is let go of, and
	/// none after any other release. A press within <see cref="ClickLimit"/> of it is a double click's second and spoiled.
	/// </summary>
	private long? _rightStamp;

	/// <summary>
	/// The left button's press and stamp, kept as the right's are (<c>0x00faa598</c>, <c>0x00faa5a0</c>). A press on a
	/// <see cref="UiButton"/> is never unspoiled: the button's own proc answers it and the base proc never sees it.
	/// A slider's thumb, alone, leaves the stamp as it found it.
	/// </summary>
	private (bool Unspoiled, bool KeepsStamp, Vector2 Where, UiControl? On) _leftPress;

	private long? _leftStamp;

	/// <summary>
	/// Whether the interface used this frame's wheel, so that the world does not use it as well.
	///
	/// <para>
	/// <b>Without it, a wheel over a scrolling list would scroll the list AND zoom the park behind it</b>:
	/// the park camera reads <c>Input.Mouse.Wheel</c> itself, and zooms only when this is false.
	/// </para>
	/// <para>
	/// It is read by <see cref="ParkOrbitCameraMode"/> in the same frame it is written: the HUD updates
	/// in <see cref="Level.Update"/> and the camera in <see cref="Level.Render"/>, in that order, so
	/// there is no lag and no ordering hazard.
	/// </para>
	/// </summary>
	internal static bool WheelTaken { get; private set; }

	/// <summary>
	/// Whether the interface took this frame's press, so the world does not act on it as well.
	///
	/// <para>
	/// Read after <see cref="Level.Update"/> has called <c>Hud.Update</c>, in the same frame it is
	/// written - the same arrangement as <see cref="WheelTaken"/>, and the reason a click on the gadget
	/// never also opens whatever is drawn behind it.
	/// </para>
	/// <para>
	/// <b>An open MODAL window counts as having taken it, even where nothing was hit.</b> The hit test
	/// stops at a modal and answers null for a press outside it, so "nothing was hit" and "a modal
	/// swallowed it" arrive here looking identical - and treating them the same would let a click pass
	/// straight through a dimmed screen into the park behind. A park screen's bare frame takes it the same way
	/// (<see cref="OnParkScreen"/>).
	/// </para>
	/// </summary>
	internal static bool PointerTaken { get; private set; }

	/// <summary>
	/// Whether one of the park's screens is open (the original's <c>DAT_007c24c8</c>), for the park's own frame, which
	/// runs after the stack's: written every frame the stack updates, as <see cref="PointerTaken"/> is. While one is
	/// open the original has its camera, game and cheat tables switched off (<c>FUN_00485b70</c>, <c>0x00485ccd</c>)
	/// and the screen's handler runs the shortcuts' alone (<c>FUN_00488ba0</c>, <c>0x00488c13</c>), and its park proc
	/// keeps a left press from the idle click (<c>0x00488741</c>).
	/// </summary>
	internal static bool ParkScreenOpen { get; private set; }

	/// <summary>
	/// Whether the park map is open: the original hands its root to <c>FUN_00485b70</c> as it does a park screen's
	/// (<c>0x005f0c01</c>), which switches the camera's table off, so the arrow keys are the map's. Written as
	/// <see cref="ParkScreenOpen"/> is.
	/// </summary>
	internal static bool MapOpen { get; private set; }

	/// <summary>
	/// Whether a window that keeps the camera keys from the park is open, so no camera key pans, zooms, turns or resets
	/// the park behind it. A message box and the Save and Load screens switch the camera's table off as they open
	/// (<c>FUN_00486b70</c>, <c>0x0047efa0</c>, <c>0x0049f406</c>). The game menu and the options screen leave it on, but
	/// each takes every key itself (the menu's handler <c>0x0048b6a0</c>), so the park's layer hears none
	/// (<c>docs/exe/park-engine.md</c>, "The game menu and the options screen hold the keys"). Not the park map, whose own
	/// <see cref="MapOpen"/> it is. Written as <see cref="ParkScreenOpen"/> is.
	/// </summary>
	internal static bool CameraKeysAway { get; private set; }

	/// <summary>Whether one of the park's screens is open in this stack - see <see cref="ParkScreenOpen"/>.</summary>
	internal bool ParkScreenUp => _windows.Exists( window => window.ParkScreen );

	/// <summary>
	/// Whether the interface took this frame's RIGHT press, so the park does not arm its quick click on it
	/// (<c>Level.RightButton</c>) - see <see cref="TakesRightPress"/>. Written and read in the same frame, as
	/// <see cref="PointerTaken"/> is.
	/// </summary>
	internal static bool RightPointerTaken { get; private set; }

	/// <summary>
	/// The row the scene's own view posted this frame (<c>FUN_004924b0</c>), with its parameters' values, or null where it
	/// posted nothing. The scene sets it each frame before the stack updates; only the park's hover posts. Taken only
	/// while the pointer is on the view, after the row a control left behind, and cleared as it is taken.
	/// </summary>
	internal static (int Row, (int Parameter, string Value)[] Values)? WorldHelpPost { get; set; }

	/// <summary>
	/// The help bar's one row, as last posted (<see cref="PostHelp"/>), or -1 for the empty bar: whatever posted last
	/// stands, a control's or the park's alike.
	/// </summary>
	internal int HelpRow { get; private set; } = -1;

	/// <summary>The values of <see cref="HelpRow"/>'s parameters: a person's number or name.</summary>
	internal (int Parameter, string Value)[] HelpValues { get; private set; } = [];

	/// <summary>What the help bar says after the last update.</summary>
	internal string HelpBarText => _helpBar.Text;

	/// <summary>The help bar itself, for the console's look at how it last set its text.</summary>
	internal HelpBar HelpBar => _helpBar;

	/// <summary>
	/// Where the help bar's top stands, or null for its place at the bottom - see <see cref="HelpBar.Top"/>. A stack made
	/// without its constructor, as a test makes one, has no bar, and this then does nothing.
	/// </summary>
	internal int? HelpBarTop
	{
		get => _helpBar?.Top;
		set
		{
			if ( _helpBar != null )
				_helpBar.Top = value;
		}
	}

	/// <summary>
	/// What the pointer was over at the last update, for the bar: a control, <see cref="Background"/> (a modal window, a
	/// park screen's body or the cover), a window's <see cref="Layer"/>, or null for the scene's own view.
	/// </summary>
	private object? _pointed;

	/// <summary>The row <see cref="_pointed"/> posted, so a control's row changed under the pointer is posted too.</summary>
	private int _pointedRow = -1;

	/// <summary>Whatever is under the pointer that is neither a control nor the view: its row is none.</summary>
	private static readonly object Background = new();

	public WindowStack()
	{
		UiFonts.Preload();
		UiSounds.Preload();

		_helpBar = new HelpBar();
	}

	/// <summary>
	/// The scene's keys: run every frame the front window has no box to type into, or no window is open, to read this
	/// frame's keys itself. The original gives a key to one control only (0x006698e6), so a key a box took never reaches
	/// this as well.
	/// </summary>
	public Action? KeysWithoutFocus { get; set; }

	/// <summary>
	/// What a left press does when it lands on no window at all - on the scene's own view, which the original's lobby
	/// covers with a control of its own that takes it (0x005d58b0) - answering whether the view took it. Only a press the
	/// window system sent reaches it (<see cref="Input.MouseInfo.LeftWentDown"/>), never a button that was already down
	/// when the stack next looked. The park reads its clicks itself.
	/// </summary>
	public Func<bool>? ViewPressed { get; set; }

	/// <summary>
	/// What answers a right click whose press lands on the scene's own view - on no control, no park screen's body and
	/// no modal window (<see cref="TakesRightPress"/>) - asked at the press, since the click goes to whatever took the
	/// press however things stand at the release; null when nothing does. Only a park's first person answers one - see
	/// <see cref="ParkViewfinder"/>. The click is <see cref="RightClick"/>'s.
	/// </summary>
	public Func<Action?>? ViewRightClick { get; set; }

	/// <summary>Whether the view took the press of the last <see cref="ClickAt"/>, for the debug console's reply.</summary>
	internal bool ViewTook { get; private set; }

	/// <summary>The control the pointer is over, if any, for the debug console.</summary>
	internal UiControl? Hovered => _hovered;

	/// <summary>
	/// Whether the pointer is on the scene's own view now: over no control and no park screen's body, no modal window up
	/// and no cover over the stack. Hit-tested where it is asked, not taken from the stack's last update, which the park's
	/// hover runs before: the frame the pointer reaches a control takes nothing behind it. The park's view hears the
	/// pointer come onto it and leave it for a control (<c>0x10001</c>, <c>0x10002</c>), a park screen's body among them;
	/// a modal window counts as a control here, as it does for the help bar, which is not measured in the original.
	/// </summary>
	internal bool PointerOnTheView
		=> !Covered && !ModalUp && Layer == null && HitTest( Input.Mouse.Position.X, Input.Mouse.Position.Y ) == null
			&& !OnParkScreen( Input.Mouse.Position.X, Input.Mouse.Position.Y );

	/// <summary>
	/// The window whose full-screen layer lies over the scene's view (<see cref="UiWindow.LayerCursor"/>), or null: first
	/// person's. The view under it is never pointed at.
	/// </summary>
	private UiWindow? Layer => _windows.LastOrDefault( window => window.LayerCursor != null );

	/// <summary>
	/// The cursor of the layer under the pointer, or null where the pointer is on no layer: on a control, a modal, a park
	/// screen, under the cover or on the view itself.
	/// </summary>
	internal Input.CursorTypes? LayerCursor
		=> Covered || ModalUp || HitTest( Input.Mouse.Position.X, Input.Mouse.Position.Y ) != null
			|| OnParkScreen( Input.Mouse.Position.X, Input.Mouse.Position.Y ) ? null : Layer?.LayerCursor;

	/// <summary>
	/// Whether a full-screen control over the stack's layer has the keys and the pointer, the layer hidden under it, as
	/// the park's full-screen view makes one (<c>FUN_004a29d0</c>; <c>docs/exe/park-engine.md</c>, "The full-screen
	/// view: F3"). Nothing of the stack is drawn, the help bar included, and nothing in it is pointed at or pressed: every
	/// press is the cover's, so the scene gets none, and the wheel is left to the camera. The keys are the scene's
	/// (<see cref="KeysWithoutFocus"/>). The pointer is not the stack's and stays.
	/// </summary>
	internal bool Covered { get; private set; }

	/// <summary>
	/// Puts the cover over the stack or takes it off. A control held down as it goes on is let go of unclicked, and a
	/// press of either button that was waiting on its release makes no click.
	/// </summary>
	internal void Cover( bool covered )
	{
		Covered = covered;

		if ( !covered )
		{
			CoverKeys = null;
			CoverRightClick = null;
			return;
		}

		_leftPress = default;
		_rightPress = default;

		if ( _pressed == null )
			return;

		_pressed.Pressed = false;
		_pressed.PointerReleased();
		_pressed = null;
	}

	/// <summary>
	/// The keys of a cover with a handler of its own, in place of the full-screen view's, each as it is let go: the End
	/// feature's control (<c>EndFeature_OnMessage</c>, <c>0x0048a970</c>). Null for the full-screen view's; taken off with the cover.
	/// </summary>
	internal Action<Input.KeyRelease>? CoverKeys { get; set; }

	/// <summary>
	/// What a right click on a cover with a handler of its own answers, asked at the press as <see cref="ViewRightClick"/>
	/// is; null for the full-screen view's, which answers none. Taken off with the cover.
	/// </summary>
	internal Func<Action?>? CoverRightClick { get; set; }

	/// <summary>Whether a modal window is up, which takes every press that misses its controls - see <see cref="PointerTaken"/>.</summary>
	internal bool ModalUp => _windows.Exists( window => window.Modal && !window.Hidden && !window.PutAway );

	/// <summary>
	/// Whether the controls' timers are held (<see cref="UiTimer"/>): a message box, the game menu, the options
	/// screen or the park's map is up, each of which sets the original's hold as it opens (<c>FUN_00662411</c>, from
	/// <c>MessageBox_Open</c>, <c>GameMenu_Open</c>, <c>OptionsScreen_Open</c> and the map's <c>FUN_005f0b40</c>,
	/// <c>0x005f0b8a</c>). The other two that set it put up the pause overlay, which is not built
	/// (<c>FUN_0048a6e0</c>, <c>FUN_0048a720</c>; <c>docs/exe/hud.md</c>).
	/// </summary>
	/// <remarks>
	/// The original's hold is one flag, so a message box closed over the map lets the timers go with the map still
	/// up; here they stay held until the map closes too. No timer shows under the map, which closes the park's screens.
	/// </remarks>
	internal bool HoldsTimers
		=> _windows.Exists( window => window is MessageBox or GameMenu or OptionsScreen or ParkMapScreen && !window.Hidden && !window.PutAway );

	/// <summary>The open windows, back to front.</summary>
	public IReadOnlyList<UiWindow> Windows => _windows;

	/// <summary>Whether any open window pauses the game - see <see cref="UiWindow.Pauses"/>.</summary>
	public bool AnyPausing => _windows.Exists( window => window.Pauses );

	// Dead by CODE: nothing calls it.
	internal bool IsFront( UiWindow window ) => _windows.Count > 0 && _windows[^1] == window;

	internal void Open( UiWindow window )
	{
		if ( _windows.Contains( window ) )
			return;

		// One park screen at a time: each opener first sends the open one its close, as the game menu's and the map's do.
		if ( window.ParkScreen || window.ClosesParkScreen )
			CloseParkScreen();

		_windows.Add( window );
		window.Shown();
	}

	/// <summary>
	/// Closes the park screen that is open, if one is - the original's <c>FUN_00485b40</c>, message 5 to
	/// <c>DAT_007c24c8</c>. Every opener calls it first (<see cref="Open"/>), and so does the way into first person,
	/// which opens no window (<c>FUN_00481a10</c>, <c>0x00481a2b</c>).
	/// </summary>
	/// <returns>Whether one was open.</returns>
	internal bool CloseParkScreen()
	{
		var open = _windows.Where( window => window.ParkScreen ).ToArray();

		foreach ( var screen in open )
			Close( screen );

		return open.Length > 0;
	}

	internal void Close( UiWindow window )
	{
		if ( !_windows.Remove( window ) )
			return;

		if ( _hovered?.IsWithin( window.Root ) == true )
		{
			// A button going away takes its glints with it at once.
			if ( _hovered is UiButton )
				_glint.Stop();

			_hovered.Hovered = false;
			_hovered = null;
		}

		if ( _pressed?.IsWithin( window.Root ) == true )
		{
			_pressed.Pressed = false;
			_pressed = null;
		}

		if ( _leftPress.On?.IsWithin( window.Root ) == true )
			_leftPress = default;

		if ( _rightPress.On?.IsWithin( window.Root ) == true )
			_rightPress = default;

		if ( window.Focus != null )
			window.Focus.HasFocus = false;

		window.Root.ReleaseText();
		window.Closed();
	}

	/// <summary>Ends the button glints at once - see <see cref="ButtonGlint.Stop"/>.</summary>
	internal void StopGlint() => _glint.Stop();

	/// <summary>
	/// The scene is ending. Every window closes, front to back, through <see cref="Close"/>, so each hears it is
	/// closing - the island panel ends its sparkles while there is still a particle system to end them in - and
	/// lets go of its text. Then the glints go, nothing is left hovered or pressed, the help bar lets go of its
	/// text, and typing is no longer captured.
	/// </summary>
	protected override void OnDelete()
	{
		for ( int i = _windows.Count - 1; i >= 0; --i )
			Close( _windows[i] );

		_glint.Stop();
		_hovered = null;
		_pressed = null;
		_mouseWasDown = false;
		_rightWasDown = false;
		Covered = false;
		CoverKeys = null;
		CoverRightClick = null;
		ParkScreenOpen = false;
		MapOpen = false;
		CameraKeysAway = false;
		HelpBarTop = null;
		_pointed = null;
		PostHelp( -1, [] );
		_helpBar.ReleaseText();
		Input.TextCaptured = false;
	}

	protected override void OnUpdate()
	{
		// Cleared at the top of the frame the interface deals with, not at the end: the camera reads it
		// later in the same frame, so clearing it after would put the answer a frame behind.
		WheelTaken = false;
		PointerTaken = false;
		RightPointerTaken = false;

		foreach ( var window in _windows.ToArray() )
			window.Update();

		ParkScreenOpen = ParkScreenUp;
		MapOpen = _windows.Exists( window => window is ParkMapScreen );
		CameraKeysAway = _windows.Exists( window => window is MessageBox or ParkFileScreen or GameMenu or OptionsScreen
			&& !window.Hidden && !window.PutAway );

		// Ours: the hover is hit-tested once a frame, and a press made after the last frame goes to it. The original works
		// its hover out only on a move and when its interface changes (FUN_006589f9), so a press made after a window
		// opens, before the pointer moves, can go to the old hover (docs/exe/lobby.md, "The lobby's keys act on the
		// release", Unsettled). Kept as a fix (docs/DECISIONS.md).
		// Every step of one frame's presses (LeftSteps) goes to this one hit; only PointerTaken's modal and park-screen tests
		// are read live. Whether the original sends a frame's second press to the control the first one's click left is not
		// decided (its press poster, UI_PostButtonPress 0x00658af1, takes the hover current at posting).
		var mouse = Input.Mouse.Position;
		var hit = Covered ? null : HitTest( mouse.X, mouse.Y );

		if ( hit != _hovered )
		{
			if ( _hovered != null )
			{
				_hovered.Hovered = false;
				_hovered.Exited?.Invoke();

				if ( _hovered is UiButton )
					_glint.Leave();
			}

			_hovered = hit;

			if ( hit != null )
			{
				hit.Hovered = true;
				hit.Entered?.Invoke();

				if ( hit is UiButton && _helpBar.Enabled )
					_glint.Start( hit, _windows.FirstOrDefault( window => hit.IsWithin( window.Root ) ) );
			}
		}

		_glint.Update();

		// The original's move message 0x10001, which goes to the control under the pointer. Sent only on a frame the
		// pointer moved, so what a list rewrites under a pointer at rest is not chosen until it moves. The control is
		// told whether a button pressed on it is still down, the original's own record of that (control +0x11c),
		// known here for a press that landed on it; whether theirs outlives a release let go elsewhere is not decoded.
		if ( hit != null && Input.Mouse.Delta != Vector2.Zero )
		{
			hit.PointerMoved( mouse.X, mouse.Y,
				(Input.Mouse.Left && _leftPress.On == hit) || (Input.Mouse.Right && _rightPress.On == hit) );
		}

		// The left button's steps this frame, in order, so a press let go inside one frame is still a press and a
		// release, as the original's cursor library posts a press and a release as two messages (0x00486c70) - see LeftSteps.
		foreach ( var mouseDown in LeftSteps( _mouseWasDown, Input.Mouse ) )
		{
			if ( mouseDown && !_mouseWasDown )
			{
				PointerTaken = Covered || hit != null || ModalUp || OnParkScreen( mouse.X, mouse.Y );

				Press( hit, mouse.X, mouse.Y );

				if ( !PointerTaken && Input.Mouse.LeftWentDown )
					ViewPressed?.Invoke();
			}
			else if ( mouseDown && _pressed != null )
				_pressed.PointerDragged( mouse.X, mouse.Y );
			else if ( !mouseDown && _mouseWasDown )
				Release( hit );

			LeftClick( mouseDown, hit, mouse / VirtualScreen.Scale );

			_mouseWasDown = mouseDown;
		}

		// The right button's steps the same way (RightSteps): the cursor library posts its press and release as two
		// messages too (0x00486c70, button 1).
		// Ours: the left's steps are dealt before the right's; the original's one queue keeps their order (0x00486c70).
		foreach ( var rightDown in RightSteps( _rightWasDown, Input.Mouse ) )
		{
			if ( rightDown && !_rightWasDown )
			{
				RightPointerTaken = Covered || TakesRightPress( mouse.X, mouse.Y );
				hit?.RightPressed?.Invoke();
			}

			RightClick( rightDown, hit, mouse / VirtualScreen.Scale );

			_rightWasDown = rightDown;
		}

		// The wheel goes to the slider under the pointer, or the slider whose thumb it is.
		if ( Input.Mouse.Wheel != 0f && (_hovered as UiSlider ?? (_hovered as UiSliderThumb)?.Slider) is { } slider )
		{
			slider.Scroll( Input.Mouse.Wheel );
			WheelTaken = true;
		}

		// And to a list, which scrolls by whole rows. The original reaches the same place by a longer
		// road - its list owns the scrollbar as a child and the wheel arrives through that - but the
		// effect is this.
		if ( Input.Mouse.Wheel != 0f && _hovered is UiList list )
		{
			list.ScrollByWheel( Input.Mouse.Wheel );
			WheelTaken = true;
		}

		Keyboard();

		// The bar holds one row, the last posted (docs/exe/hud.md, "The help bar holds one posted row"). The pointer coming
		// onto a control posts its row and leaving it empties the bar (0x10001 and 0x10002 to FUN_0065f6d1, the help sink's
		// +0x24 and +0x28); the view's own row comes after that, so a row the park posted stands until something else
		// posts. Under the cover the bar is not drawn, and its own key is still heard: the window procedure matches the
		// system table on every key.
		// A layer laid over the view is the control there, whose row is none (docs/exe/hud.md, "The help bar in first person").
		var pointed = hit ?? (Covered || ModalUp || OnParkScreen( mouse.X, mouse.Y ) ? Background : Layer);

		// Ours: a control's row changed under a pointer at rest is posted at once. The original posts the hovered control's
		// row again when a button with flag 0x10 is let go (0x10004: 0x00669172 calls the sink's +0x1c, 0x00492320); whether
		// it posts at any other change is not decoded (docs/exe/hud.md, "The help bar holds one posted row").
		if ( pointed != _pointed || (hit != null && hit.HelpText != _pointedRow) )
		{
			_pointed = pointed;
			_pointedRow = hit?.HelpText ?? -1;
			PostHelp( _pointedRow, [] );
		}

		// A screen's own post, from its frame message: the buy screen's row for the item under the pointer.
		foreach ( var window in _windows.ToArray() )
		{
			if ( window.HelpPost() is { } posted )
				PostHelp( posted.Row, posted.Values );
		}

		if ( pointed == null && WorldHelpPost is { } post )
			PostHelp( post.Row, post.Values );

		WorldHelpPost = null;
		_helpBar.Update( HelpRow, HelpValues );
	}

	/// <summary>Posts row <paramref name="row"/> to the help bar, or empties it with -1 (<c>FUN_004924b0</c>).</summary>
	private void PostHelp( int row, (int Parameter, string Value)[] values )
	{
		HelpRow = row;
		HelpValues = row < 0 ? [] : values;
	}

	protected override void OnRender()
	{
		if ( Covered )
			return;

		// Each window's effects straight after it, so the windows over it cover them.
		foreach ( var window in _windows )
		{
			if ( window.Hidden || window.PutAway )
				continue;

			window.Root.Draw();
			ScreenParticles.Current?.Draw( window );
		}

		_helpBar.Draw();
	}

	/// <summary>
	/// Presses and releases at a point of the window, as though the pointer had been there - the hit
	/// test and both halves of a real click.
	/// </summary>
	/// <remarks>
	/// <b>This exists because the debug console cannot move the pointer.</b> A warp with no real motion behind
	/// it reaches the window system and never reaches SDL - measured twice against this game - so a
	/// test that could only click where the cursor already sits would be measuring X rather than this
	/// interface. <c>ParkPicking.PickAt</c> was given coordinates for the same reason and says so.
	/// Only SDL is skipped: the hit test, the row arithmetic and both handlers are the real ones, and a press that lands
	/// on no window goes to <see cref="ViewPressed"/> as a real one does.
	/// </remarks>
	/// <returns>
	/// Whether the interface took it, on the same reading as <see cref="PointerTaken"/> - so a caller
	/// can hand the click on to the world exactly when a real frame would.
	/// </returns>
	internal bool ClickAt( float x, float y )
	{
		var hit = Covered ? null : HitTest( x, y );
		var taken = Covered || hit != null || ModalUp || OnParkScreen( x, y );

		Press( hit, x, y );
		ViewTook = !taken && ViewPressed?.Invoke() == true;
		Release( hit );

		// And the click the base proc would make of so short a press, for what is not a button.
		if ( hit is not (null or UiButton) )
			LeftClicked( hit, x, y );

		return taken;
	}

	/// <summary>
	/// Whether a point is on a park screen's body: its root is one plain rectangle that takes a press of either button
	/// anywhere on it, bare frame included (<c>docs/exe/park-engine.md</c>, "Whose a right press is").
	/// </summary>
	internal bool OnParkScreen( float x, float y )
		=> _windows.Exists( window => window.ParkScreen && !window.Hidden && !window.PutAway
			&& window.Root.Visible && window.Root.Holds( x, y ) );

	/// <summary>
	/// Whether a right press at a point is the interface's rather than the park's: over a control that takes the
	/// pointer, over a park screen's body, or anywhere while a modal window is up - the reading a left press gets
	/// (<see cref="PointerTaken"/>).
	/// </summary>
	/// <remarks>
	/// The original's press goes to the control under the pointer and on to no parent, so only one that lands on the
	/// park's own layer arms the quick click (<c>0x0048833a</c>). The game menu and the message box cover that layer and
	/// the options and map screens hide it; a <see cref="UiWindow.ParkScreen"/> does neither, so a right press beside
	/// one still arms. See <c>docs/exe/park-engine.md</c>, "Whose a right press is".
	/// </remarks>
	internal bool TakesRightPress( float x, float y )
		=> OnParkScreen( x, y ) || _windows.Exists( window => !window.Hidden && !window.PutAway
			&& (window.Root.HitTest( x, y ) != null || window.Modal) );

	/// <summary>
	/// The right button's click, this frame, as the base control proc makes one for button 1 (<c>docs/exe/hud.md</c>, "A
	/// click and a double click"): the button down or up, the control under the pointer, and where the pointer is in the
	/// interface's units. A click whose press landed on the view goes to what <see cref="ViewRightClick"/> named at the
	/// press; one on a control goes to its <see cref="UiControl.RightClicked"/>, and stamps the time a second press is
	/// judged by either way. Only a press the window system sent can click, never a button already down when the
	/// stack next looked (F2 stops it).
	/// </summary>
	/// <remarks>
	/// <b>Not the original's in three ways.</b> No stamp is none here, where the original writes nought
	/// (<c>0x0065f9bd</c>) and compares signed, so its next press is a double click's second once its clock has passed
	/// <c>0x80000000</c>, about 25 days up. A control's flag <c>0x8</c>, which skips the double click's test
	/// (<c>0x0065f8b2</c>), is not read. And a stray is judged only while the
	/// pointer is over what the press landed on, which is the original's rule for a control without the mouse capture;
	/// the park's own layer takes the capture on a right press (<c>0x004882ba</c>), so the original judges a press on the
	/// park in orbit wherever the pointer goes. That can change only the stamp.
	/// </remarks>
	private void RightClick( bool down, UiControl? hit, Vector2 at )
	{
		if ( down && !_rightWasDown )
		{
			var answer = Covered ? CoverRightClick?.Invoke() : RightPointerTaken ? null : ViewRightClick?.Invoke();

			var now = Time.WallMilliseconds;

			_rightPress = (Input.Mouse.RightWentDown && !(now - _rightStamp < ClickLimit), at, hit, answer);
			_rightStamp = now;
			return;
		}

		if ( down )
		{
			if ( _rightPress.Unspoiled && hit == _rightPress.On && hit?.StraySpoilsAClick != false
				&& (MathF.Abs( at.X - _rightPress.Where.X ) > ClickStray || MathF.Abs( at.Y - _rightPress.Where.Y ) > ClickStray) )
				_rightPress.Unspoiled = false;

			return;
		}

		if ( !_rightWasDown )
			return;

		if ( !_rightPress.Unspoiled )
		{
			_rightStamp = null;
			return;
		}

		var let = Time.WallMilliseconds;
		var clicked = let - _rightStamp < ClickLimit;
		_rightStamp = let;

		if ( clicked )
		{
			_rightPress.Answer?.Invoke();
			_rightPress.On?.RightClicked?.Invoke();
			_rightPress.On?.RightClickedAt( _rightPress.Where.X * VirtualScreen.Scale, _rightPress.Where.Y * VirtualScreen.Scale );
		}
	}

	/// <summary>
	/// The left button's click, this frame, for whatever is not a button: the base control proc's, as
	/// <see cref="RightClick"/> is for button 1, with the same limit, stray and stamp. A click goes to the control the
	/// press landed on, wherever the pointer is let go (<see cref="UiControl.LeftClicked"/>, then
	/// <see cref="UiControl.LeftClickedAt"/> with the press's point); a press under <see cref="ClickLimit"/> from the
	/// last click's release is a double click's second, told to the control at once
	/// (<see cref="UiControl.LeftDoubleClicked"/>), and its release clicks nothing.
	/// <para>
	/// <b>A button keeps its own.</b> The button class's proc answers its press and release and reads no clock
	/// (<c>FUN_00668f9c</c>): its press stamps the button's record and its release clears the stamp, so a press on
	/// anything straight after a button's click is never a double click's second. <see cref="Release"/> is that proc.
	/// A slider's thumb keeps its press too and never touches the stamp (<c>0x0066bb9b</c>).
	/// </para>
	/// <para>
	/// A press on no control is the scene's own layer's, which hands it to the base proc
	/// (<c>docs/exe/park-engine.md</c>, the park's mouse proc), so it stamps as a control's does.
	/// </para>
	/// </summary>
	/// <remarks>
	/// The stray is judged as the right button's is, only while the pointer is over what the press landed on and
	/// never for a control that keeps the move from the base proc (<see cref="UiControl.StraySpoilsAClick"/>), and a
	/// control's flag <c>0x8</c> is not read - see <see cref="RightClick"/>.
	/// </remarks>
	private void LeftClick( bool down, UiControl? hit, Vector2 at )
	{
		if ( down && !_mouseWasDown )
		{
			var now = Time.WallMilliseconds;

			if ( hit is UiSliderThumb )
			{
				_leftPress = (false, true, at, hit);
				return;
			}

			if ( hit is UiButton )
			{
				_leftPress = (false, false, at, hit);
				_leftStamp = now;
				return;
			}

			var second = Input.Mouse.LeftWentDown && now - _leftStamp < ClickLimit;

			_leftPress = (Input.Mouse.LeftWentDown && !second, false, at, hit);
			_leftStamp = now;

			if ( second )
				hit?.LeftDoubleClicked();

			return;
		}

		if ( down )
		{
			if ( _leftPress.Unspoiled && hit == _leftPress.On && hit?.StraySpoilsAClick != false
				&& (MathF.Abs( at.X - _leftPress.Where.X ) > ClickStray || MathF.Abs( at.Y - _leftPress.Where.Y ) > ClickStray) )
				_leftPress.Unspoiled = false;

			return;
		}

		if ( !_mouseWasDown )
			return;

		var on = _leftPress.On;
		_leftPress.On = null;

		if ( !_leftPress.Unspoiled )
		{
			if ( !_leftPress.KeepsStamp )
				_leftStamp = null;

			return;
		}

		var let = Time.WallMilliseconds;
		var clicked = let - _leftStamp < ClickLimit;
		_leftStamp = let;

		if ( clicked && on != null )
			LeftClicked( on, _leftPress.Where.X * VirtualScreen.Scale, _leftPress.Where.Y * VirtualScreen.Scale );
	}

	/// <summary>Hands a control its left click, with the press's point in window pixels.</summary>
	private static void LeftClicked( UiControl on, float x, float y )
	{
		on.LeftClicked?.Invoke();
		on.LeftClickedAt( x, y );
	}

	private UiControl? HitTest( float x, float y )
	{
		for ( int i = _windows.Count - 1; i >= 0; --i )
		{
			if ( _windows[i].Hidden || _windows[i].PutAway )
				continue;

			if ( _windows[i].Root.HitTest( x, y ) is { } hit )
				return hit;

			// A modal window shuts out everything behind it; a park screen only what its own rectangle covers.
			if ( _windows[i].Modal || (_windows[i].ParkScreen && _windows[i].Root.Visible && _windows[i].Root.Holds( x, y )) )
				return null;
		}

		return null;
	}

	/// <summary>
	/// Where the left button stood after each of this frame's presses and releases, given where it stood as the last
	/// frame ended: the button as it ends the frame when it was not pressed, and otherwise a release before each press
	/// made while it was down, the press, and the release that let it go inside the frame. The original is sent each as
	/// its own message (the cursor library's callback <c>0x00486c70</c> posts the press, <c>0x10005</c>, through <c>UI_PostButtonPress</c>, <c>0x00658af1</c>; <c>docs/exe/hud.md</c>), so none is lost.
	/// </summary>
	internal static IEnumerable<bool> LeftSteps( bool wasDown, Input.MouseInfo mouse )
		=> Steps( wasDown, mouse.Left, Math.Max( mouse.LeftPresses, mouse.LeftWentDown ? 1 : 0 ) );

	/// <summary>
	/// Where the right button stood after each of this frame's presses and releases, as <see cref="LeftSteps"/> is for
	/// the left. The original's cursor library posts the right button's too, as button 1 (<c>0x00486c70</c>).
	/// </summary>
	internal static IEnumerable<bool> RightSteps( bool wasDown, Input.MouseInfo mouse )
		=> Steps( wasDown, mouse.Right, Math.Max( mouse.RightPresses, mouse.RightWentDown ? 1 : 0 ) );

	private static IEnumerable<bool> Steps( bool wasDown, bool endsDown, int presses )
	{
		var down = wasDown;

		for ( int i = 0; i < presses; ++i )
		{
			if ( down )
				yield return false;

			yield return true;
			down = true;
		}

		if ( presses == 0 || down != endsDown )
			yield return endsDown;
	}

	private void Press( UiControl? hit, float x, float y )
	{
		_pressed = hit;

		if ( hit == null )
			return;

		hit.Pressed = true;
		hit.PointerPressed( x, y );

		if ( hit is UiEdit edit && _windows.FirstOrDefault( window => hit.IsWithin( window.Root ) ) is { } owner )
			owner.Focus = edit;
	}

	private void Release( UiControl? hit )
	{
		var pressed = _pressed;
		_pressed = null;

		if ( pressed == null )
			return;

		pressed.Pressed = false;
		pressed.PointerReleased();

		if ( pressed != hit || pressed is UiButton { Enabled: false } )
			return;

		// A button tells its parent it was clicked, and UI_Init's hook on every message (0x00485780)
		// clicks on that message whoever it was for. The purple buttons are not buttons to the
		// original - they take the pointer themselves and tell nobody - so they are silent.
		// A deviation (Q711; hud.md, "Which window the click sound is for"): the hook picks the sound by the RECEIVER's
		// style bit 0x10, and a list's heading buttons report to the list, which carries it, so the original plays Select3
		// (0xbd) for a heading; here a heading is a plain button and plays BUTTON01.
		if ( pressed is UiButton { Clicks: true } button )
		{
			UiSounds.Click( toggle: button.Toggles || pressed.Parent is UiRadioGroup );

			if ( button.Toggles )
				button.IsDown = !button.IsDown;
		}

		pressed.Clicked?.Invoke();
	}

	/// <summary>
	/// Typing goes to the front window's box, and so do Enter and Escape: a box sends them on to its
	/// window (0x802 and 0x804), and no window hears them otherwise. The box edits on a key's press and
	/// takes Enter and Escape on the release (its key-up handler, 0x00667fee), and only the main Enter:
	/// the keypad's reaches it as 0x0d00, which it does not answer. The new player dialog takes Enter
	/// as its tick by sending itself the same message the tick sends (0x004a6d00), so it clicks as
	/// the tick does. With no box to type into, the keys are the scene's - see <see cref="KeysWithoutFocus"/>.
	/// </summary>
	private void Keyboard()
	{
		var front = _windows.Count > 0 ? _windows[^1] : null;

		foreach ( var window in _windows )
		{
			if ( window.Focus != null )
				window.Focus.HasFocus = window == front;
		}

		var focus = front?.Focus;
		Input.TextCaptured = focus != null;

		if ( front == null || focus == null )
		{
			KeysWithoutFocus?.Invoke();
			return;
		}

		focus.Type( Input.TypedText );

		if ( Input.KeysPressed.Contains( Key.BackSpace ) )
			focus.Backspace();

		// In the order they came up, and only the first: either closes the box's window, and a key let go after it in the
		// same frame is not handed on to whatever has the keys next, where the original's queue would hand it on.
		foreach ( var key in Input.KeysReleased )
		{
			if ( key == Key.Enter )
			{
				UiSounds.Click( toggle: false );
				front.Accept();
				break;
			}

			if ( key == Key.Escape )
			{
				front.Cancel();
				break;
			}
		}
	}
}
