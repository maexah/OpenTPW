using Veldrid;

namespace OpenTPW.UI;

/// <summary>
/// The lobby's interface: the player slots it opens on, the dialogs they lead to, and the panel that
/// takes over once someone is playing.
///
/// <para>
/// It follows the original's front end. FrontEnd_Init (0x005d5970) finds nobody playing and opens the
/// player slots (FrontEnd_ShowPlayerSlots, 0x004a6580). An empty slot opens the new player dialog
/// (0x004a6e40); ticking it makes the player and closes the slots (FrontEnd_ClosePlayerSlots,
/// 0x004a6a50), which gives a new player their first golden key and sets the advisor off on his tour
/// of the lobby, and the island panel (0x004b8ca0) takes over. Quit Game asks first (0x004a61d0), and
/// the tick leaves the game (0x004a6290).
/// </para>
/// <para>
/// Escape brings up the <see cref="GameMenu"/> over whichever of those is showing, and takes it away
/// again. Its Options opens the <see cref="OptionsScreen"/>, which puts the front end's window away
/// until it closes, and its Select New Player brings the player slots back.
/// </para>
/// <para>
/// A click is a press and a release on the same control. Only the front window takes the pointer
/// when it is modal; otherwise the pointer goes to whatever is front-most under it, in any window.
/// </para>
/// <para>
/// It is a panel on the HUD, so F2 hides it with everything else there. The advisor is not part of
/// it - he is drawn over the top of all of it, as the original draws him.
/// </para>
/// </summary>
internal sealed class FrontEnd : Panel
{
	/// <summary>What the windows draw, loaded with the lobby - behind the loading screen - rather than as each first opens.</summary>
	private static readonly string[] Meshes =
	[
		"b_login", "b_dellog", "e_joy", "tpwlogo", "LOLIGHT", "w_dialog", "w_dialog_wave", "b_okay", "b_exit",
		"!frame", "!f_plain", "b_ltoggle", "f_helpbg", "f_screen", "f_optpanel", "f_optpanel2", "f_optpanel3",
		"b_on", "b_on2", "b_scroller"
	];

	private readonly List<UiWindow> _windows = new();
	private readonly HelpBar _helpBar;
	private readonly IslandPanel _islandPanel;
	private readonly ButtonGlint _glint = new();
	private PlayerSlots? _playerSlots;
	private OptionsScreen? _options;

	private UiControl? _hovered;
	private UiControl? _pressed;
	private bool _mouseWasDown;
	private bool _quitting;

	public Players Players { get; } = new();

	/// <summary>The lobby's front end, while there is one - for saving whoever is playing as the game closes.</summary>
	internal static FrontEnd? Current { get; private set; }

	public FrontEnd()
	{
		Current = this;

		foreach ( var mesh in Meshes )
			UiMesh.Get( mesh );

		UiFonts.Preload();
		UiSounds.Preload();

		Players.Load();

		_helpBar = new HelpBar();
		_islandPanel = new IslandPanel( this );

		ShowPlayerSlots();
	}

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

	/// <summary>A player slot was clicked (0x004a6000): an empty one asks who is playing, a used one plays as whoever is in it.</summary>
	internal void SlotClicked( int slot )
	{
		if ( Players[slot] == null )
		{
			Open( new NewPlayerDialog( this, slot ) );
			LobbyAdvisor.Current?.ExplainNewPlayer();
			return;
		}

		Players.Select( slot );
		ClosePlayerSlots( newPlayer: false );
	}

	internal void PlayerCreated( NewPlayerDialog dialog )
	{
		Close( dialog );
		ClosePlayerSlots( newPlayer: true );
	}

	/// <summary>Quit Game (0x004a61d0): asks, and quits on the tick.</summary>
	internal void AskToQuit() => Open( new MessageBox( this, Localization.Get( UIStrings.ConfirmQuit ), Quit ) );

	/// <summary>
	/// The game menu's Options (0x0048bd40, item 11). OptionsScreen_Open (0x004a3a30) quietens the
	/// advisor, fading his voice rather than cutting it (Advisor_StopQuietly with 1); puts the front end's
	/// window away (message 6); and opens the options screen over everything.
	/// </summary>
	internal void OpenOptions()
	{
		if ( _options != null )
			return;

		LobbyAdvisor.Current?.StopQuietly();

		foreach ( var window in _windows )
			window.Hidden = true;

		_options = new OptionsScreen( this );
		Open( _options );
	}

	/// <summary>The options screen has closed (0x004a2bf0, message 0x14): the front end's window comes back, and any glints go.</summary>
	internal void OptionsClosed()
	{
		_options = null;

		foreach ( var window in _windows.Where( window => window.Hidden ).ToArray() )
		{
			window.Hidden = false;
			window.Shown();
		}

		_glint.Stop();
	}

	/// <summary>
	/// Select New Player, once its box is ticked (0x0048bc50). The player is saved and stops playing
	/// (0x005c8650), and the player slots open again (FrontEnd_ShowPlayerSlots), which greets whoever is at
	/// them - with a player in a slot, the advisor welcomes them back.
	/// </summary>
	internal void SelectNewPlayer()
	{
		Players.SaveAndDeselect();
		Close( _islandPanel );

		ShowPlayerSlots();
		LobbyAdvisor.Current?.Greet( Players.UsedSlots );
	}

	/// <summary>
	/// A used slot's delete button (0x004a6000, message 0x100): asks first, in a message box with UITEXT 399 -
	/// a line the shipped text leaves empty, so the box asks nothing - and deletes on the tick.
	/// </summary>
	internal void AskToDeletePlayer( int slot )
		=> Open( new MessageBox( this, Localization.Get( UIStrings.ConfirmDeletePlayer ), () => DeletePlayer( slot ) ) );

	/// <summary>The delete box's tick (0x004a61b0): the player goes, folder and all, and the slots are filled again (0x004a62b0).</summary>
	private void DeletePlayer( int slot )
	{
		Players.Delete( slot );

		if ( _playerSlots != null )
			Close( _playerSlots );

		ShowPlayerSlots();
	}

	private void ShowPlayerSlots()
	{
		_playerSlots = new PlayerSlots( this );
		Open( _playerSlots );
	}

	/// <summary>
	/// FrontEnd_ClosePlayerSlots (0x004a6a50). It puts up the island panel, which looks at the player's
	/// keys as it comes into view, and empties the advisor's queue whoever was picked. A Full Simulation
	/// player just made is then given a golden key (0x005afc30), written out at once (0x005c8a10), and the
	/// advisor's tour of the lobby (response 393), which shows the key on the panel when he hands it over.
	/// An Instant Action player gets no key - every park is open to them - and a line of their own instead
	/// (response 394).
	/// </summary>
	private void ClosePlayerSlots( bool newPlayer )
	{
		if ( _playerSlots != null )
		{
			Close( _playerSlots );
			_playerSlots = null;
		}

		Open( _islandPanel );

		if ( newPlayer && Players.Current is { InstantAction: true } )
		{
			LobbyAdvisor.Current?.ExplainInstantAction();
		}
		else if ( newPlayer && Players.Current != null )
		{
			Players.Current.AddKey();

			// With nobody to hand it over, the key would never show.
			if ( LobbyAdvisor.Current is { CanSpeak: true } advisor )
				advisor.GiveLobbyTour( KeyHandedOver );
			else
				_islandPanel.ShowKeys();
		}
		else
		{
			LobbyAdvisor.Current?.Hush();
		}
	}

	/// <summary>
	/// The advisor's cue in his tour of the lobby: goldkey, the key effect (87) at the player's key
	/// count in the top right, and the panel looks at the player's keys again and shows the one just
	/// given (Advisor_Update, 0x00599880). The effect is a ring of little keys turning round the count,
	/// which an effector draws back in when it ends before they burst into sparkles.
	/// </summary>
	private void KeyHandedOver()
	{
		UiSounds.GoldKeyHandedOver();
		ParticleSystem.Current?.Spawn( (int)ParLib.P_EFFECT_Key, 90000, 0, 7000 );
		_islandPanel.ShowKeys();
	}

	/// <summary>
	/// Leaves the game. The original's tick (0x004a6290) tells the lobby it is finished, with quitting
	/// as the choice (0x005d5db0), and its state machine tears everything down and stops. Here the
	/// window is closed once the frame in progress has been shown, so nothing is drawn into a window
	/// that has gone, and the game loop ends with it.
	/// </summary>
	private void Quit()
	{
		if ( _quitting )
			return;

		_quitting = true;
		Log.Info( "Front end: quitting" );

		Render.PostUpdate += () => Render.Window.SdlWindow.Close();
	}

	protected override void OnUpdate()
	{
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
					_glint.Start( hit );
			}
		}

		_glint.Update();

		var mouseDown = Input.Mouse.Left;

		if ( mouseDown && !_mouseWasDown )
			Press( hit, mouse.X, mouse.Y );
		else if ( mouseDown && _pressed != null )
			_pressed.PointerDragged( mouse.X, mouse.Y );
		else if ( !mouseDown && _mouseWasDown )
			Release( hit );

		_mouseWasDown = mouseDown;

		// The wheel goes to the slider under the pointer, or the slider whose thumb it is.
		if ( Input.Mouse.Wheel != 0f && (_hovered as UiSlider ?? (_hovered as UiSliderThumb)?.Slider) is { } slider )
			slider.Scroll( Input.Mouse.Wheel );

		Keyboard();

		_helpBar.Update( _hovered?.HelpText ?? -1 );
	}

	protected override void OnRender()
	{
		foreach ( var window in _windows )
		{
			if ( !window.Hidden )
				window.Root.Draw();
		}

		_helpBar.Draw();
	}

	private UiControl? HitTest( float x, float y )
	{
		for ( int i = _windows.Count - 1; i >= 0; --i )
		{
			if ( _windows[i].Hidden )
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
	/// the tick does. With no box to type into, Escape is the game menu's - see <see cref="MenuKey"/>.
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
				MenuKey( front );

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

	/// <summary>
	/// Escape, with no box to type into. The lobby's key handler (0x005e41c0) opens the game menu on it
	/// (GameMenu_Open with 1) unless it is already open, and the menu's handler (0x0048bd40) closes it on
	/// the same key. Whether the original opens it over a message box or the options screen was not
	/// established; here a modal window in front keeps Escape from it.
	/// </summary>
	private void MenuKey( UiWindow? front )
	{
		if ( front is GameMenu menu )
		{
			Close( menu );
			return;
		}

		if ( front is { Modal: true } )
			return;

		Open( new GameMenu( this ) );
	}
}
