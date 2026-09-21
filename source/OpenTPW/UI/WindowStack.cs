using NeoVeldrid;

namespace OpenTPW.UI;

/// <summary>
/// The interface's windows, one over another, and what the pointer and the keys do to them.
///
/// <para>
/// A click is a press and a release on the same control. Only the front window takes the pointer
/// when it is modal; otherwise the pointer goes to whatever is front-most under it, in any window.
/// </para>
/// <para>
/// The original sets this up once, in UI_Init (0x00489ca0, reached only from Boot_Init through 0x004813c0):
/// the root every window hangs off (0x006584df) and the hook that clicks for every button (DAT_00faa5fc).
/// The game menu (0x0048c830), the message box (0x0047f020) and the options screen (0x004a3a30) are the
/// same functions in the lobby and in a park, and keys reach a window the same way in both - through the
/// box it is typing into, or the control last clicked (0x006698e6). Only Escape with nothing to type into
/// is the scene's: the lobby opens its game menu on it (0x005e41c0), and a park hands it to its key
/// bindings (0x0040c4d0) - see <see cref="EscapeWithoutFocus"/>. Here a stack is made with each scene's
/// HUD, and what should outlive a scene - the meshes, fonts and sounds its windows use - is cached apart
/// from it.
/// </para>
/// <para>
/// It is a panel on the HUD, so F2 hides it with everything else there.
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

	/// <summary>
	/// Whether the interface used this frame's wheel, so that the world does not use it as well.
	///
	/// <para>
	/// <b>Nothing consumed the mouse for the world before this.</b> The park camera reads
	/// <c>Input.Mouse.Wheel</c> with no guard at all, so a wheel over a scrolling list would scroll the
	/// list AND zoom the park behind it. There was nothing to guard while no list existed, which is why
	/// it was left until one did.
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
	/// straight through a dimmed screen into the park behind.
	/// </para>
	/// </summary>
	internal static bool PointerTaken { get; private set; }

	public WindowStack()
	{
		UiFonts.Preload();
		UiSounds.Preload();

		_helpBar = new HelpBar();
	}

	/// <summary>
	/// What Escape does when the front window has no box to type into, or no window is open - handed the
	/// front window, if there is one. Only on the key going down.
	/// </summary>
	public Action<UiWindow?>? EscapeWithoutFocus { get; set; }

	/// <summary>The open windows, back to front.</summary>
	public IReadOnlyList<UiWindow> Windows => _windows;

	/// <summary>Whether any open window pauses the game - see <see cref="UiWindow.Pauses"/>.</summary>
	public bool AnyPausing => _windows.Exists( window => window.Pauses );

	internal bool IsFront( UiWindow window ) => _windows.Count > 0 && _windows[^1] == window;

	internal void Open( UiWindow window )
	{
		if ( _windows.Contains( window ) )
			return;

		_windows.Add( window );
		window.Shown();
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
		_helpBar.ReleaseText();
		Input.TextCaptured = false;
	}

	protected override void OnUpdate()
	{
		// Cleared at the top of the frame the interface deals with, not at the end: the camera reads it
		// later in the same frame, so clearing it after would put the answer a frame behind.
		WheelTaken = false;
		PointerTaken = false;

		foreach ( var window in _windows.ToArray() )
			window.Update();

		var mouse = Input.Mouse.Position;
		var hit = HitTest( mouse.X, mouse.Y );

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

		var mouseDown = Input.Mouse.Left;

		if ( mouseDown && !_mouseWasDown )
		{
			PointerTaken = hit != null
				|| _windows.Exists( window => window.Modal && !window.Hidden && !window.PutAway );

			Press( hit, mouse.X, mouse.Y );
		}
		else if ( mouseDown && _pressed != null )
			_pressed.PointerDragged( mouse.X, mouse.Y );
		else if ( !mouseDown && _mouseWasDown )
			Release( hit );

		_mouseWasDown = mouseDown;

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

		_helpBar.Update( _hovered?.HelpText ?? -1 );
	}

	protected override void OnRender()
	{
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
	/// <b>This exists because a harness cannot move the pointer.</b> A warp with no real motion behind
	/// it reaches the window system and never reaches SDL - measured twice against this game - so a
	/// test that could only click where the cursor already sits would be measuring X rather than this
	/// interface. <c>ParkPicking.PickAt</c> was given coordinates for the same reason and says so.
	/// Only SDL is skipped: the hit test, the row arithmetic and both handlers are the real ones.
	/// </remarks>
	/// <returns>
	/// Whether the interface took it, on the same reading as <see cref="PointerTaken"/> - so a caller
	/// can hand the click on to the world exactly when a real frame would.
	/// </returns>
	internal bool ClickAt( float x, float y )
	{
		var hit = HitTest( x, y );

		Press( hit, x, y );
		Release( hit );

		return hit != null
			|| _windows.Exists( window => window.Modal && !window.Hidden && !window.PutAway );
	}

	private UiControl? HitTest( float x, float y )
	{
		for ( int i = _windows.Count - 1; i >= 0; --i )
		{
			if ( _windows[i].Hidden || _windows[i].PutAway )
				continue;

			if ( _windows[i].Root.HitTest( x, y ) is { } hit )
				return hit;

			if ( _windows[i].Modal )
				return null;
		}

		return null;
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
	/// window (0x802 and 0x804), and no window hears them otherwise. The new player dialog takes Enter
	/// as its tick by sending itself the same message the tick sends (0x004a6d00), so it clicks as
	/// the tick does. With no box to type into, Escape is the scene's - see <see cref="EscapeWithoutFocus"/>.
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
			// Only on the key going down. A held Escape - one that has just closed a dialog, or is
			// repeating - is still pressed without having gone down this frame.
			if ( Input.Pressed( InputButton.Menu ) && Input.KeysPressed.Contains( Key.Escape ) )
				EscapeWithoutFocus?.Invoke( front );

			return;
		}

		focus.Type( Input.TypedText );

		if ( Input.KeysPressed.Contains( Key.BackSpace ) )
			focus.Backspace();

		if ( Input.KeysPressed.Contains( Key.Enter ) || Input.KeysPressed.Contains( Key.KeypadEnter ) )
		{
			UiSounds.Click( toggle: false );
			front.Accept();
		}
		else if ( Input.KeysPressed.Contains( Key.Escape ) )
		{
			front.Cancel();
		}
	}
}
