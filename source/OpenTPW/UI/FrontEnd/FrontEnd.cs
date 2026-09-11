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
/// Its windows open in the interface's <see cref="WindowStack"/>, which deals with the pointer and the
/// keys for them. It is a panel on the HUD after the stack, so it hears about a frame's clicks and keys
/// once the stack has dealt them out. The advisor is not part of it - he is drawn over the top of all of
/// it, as the original draws him.
/// </para>
/// <para>
/// <b>Engine and content.</b> This is the lobby's content: which of its screens opens when, what their
/// choices do, what Escape does, the lines it has the advisor say (<see cref="FrontEndLines"/>), and
/// holding him while a window pauses the game. The window handling it relies on is the stack's, and is
/// engine.
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

	private readonly WindowStack _stack;
	private readonly IslandPanel _islandPanel;
	private PlayerSlots? _playerSlots;
	private bool _quitting;

	public FrontEnd( WindowStack stack )
	{
		_stack = stack;
		_stack.EscapeWithoutFocus = MenuKey;

		foreach ( var mesh in Meshes )
			UiMesh.Get( mesh );

		_islandPanel = new IslandPanel( stack );

		// FrontEnd_Init (0x005d5970) opens the player slots only while nobody is playing, which is always so as the
		// game starts. Coming back to the lobby with someone still playing, their island panel is up instead.
		if ( Players.Roster.Current == null )
			ShowPlayerSlots();
		else
			_stack.Open( _islandPanel );
	}

	/// <summary>A player slot was clicked (0x004a6000): an empty one asks who is playing, a used one plays as whoever is in it.</summary>
	internal void SlotClicked( int slot )
	{
		if ( Players.Roster[slot] == null )
		{
			_stack.Open( new NewPlayerDialog( _stack, this, slot ) );
			FrontEndLines.ExplainNewPlayer();
			return;
		}

		Players.Roster.Select( slot );
		ClosePlayerSlots( newPlayer: false );
	}

	internal void PlayerCreated( NewPlayerDialog dialog )
	{
		_stack.Close( dialog );
		ClosePlayerSlots( newPlayer: true );
	}

	/// <summary>Quit Game (0x004a61d0): asks, and quits on the tick.</summary>
	internal void AskToQuit() => _stack.Open( new MessageBox( _stack, Localization.Get( UIStrings.ConfirmQuit ), Quit ) );

	/// <summary>
	/// Select New Player, once its box is ticked (0x0048bc50). The player is saved and stops playing
	/// (0x005c8650), and the player slots open again (FrontEnd_ShowPlayerSlots), which greets whoever is at
	/// them - with a player in a slot, the advisor welcomes them back.
	/// </summary>
	internal void SelectNewPlayer()
	{
		Players.Roster.SaveAndDeselect();
		_stack.Close( _islandPanel );

		ShowPlayerSlots();
	}

	/// <summary>
	/// A used slot's delete button (0x004a6000, message 0x100): asks first, in a message box, and deletes on
	/// the tick.
	///
	/// <para>
	/// The original asks with UITEXT 399, handed the player's name, but both of its languages ship that line
	/// empty, so its box asks nothing at all. Deleting a player loses everything they have saved, so here the
	/// box asks in a line of OpenTPW's own, worded as the game words its other deletions (UITEXT 393 to 397),
	/// unless the text has a line 399 of its own.
	/// </para>
	/// </summary>
	internal void AskToDeletePlayer( int slot )
	{
		var text = Localization.Get( UIStrings.ConfirmDeletePlayer );

		if ( string.IsNullOrWhiteSpace( text ) )
			text = $"DELETE PLAYER\n\nAre you sure you want to delete {Players.Roster[slot]?.Name} ?\n\n(All of their saved games WILL be lost)";

		_stack.Open( new MessageBox( _stack, text, () => DeletePlayer( slot ) ) );
	}

	/// <summary>The delete box's tick (0x004a61b0): the player goes, folder and all, and the slots are filled again (0x004a62b0).</summary>
	private void DeletePlayer( int slot )
	{
		Players.Roster.Delete( slot );

		if ( _playerSlots != null )
			_stack.Close( _playerSlots );

		OpenPlayerSlots();
	}

	/// <summary>
	/// FrontEnd_ShowPlayerSlots (0x004a6580): the player slots open, and the advisor greets whoever is at them
	/// by how many slots hold a player - see <see cref="FrontEndLines.Greet"/>.
	/// </summary>
	private void ShowPlayerSlots()
	{
		OpenPlayerSlots();
		FrontEndLines.Greet( Players.Roster.UsedSlots );
	}

	/// <summary>The player slots, without a word - what the delete box's tick fills them again with (0x004a62b0).</summary>
	private void OpenPlayerSlots()
	{
		_playerSlots = new PlayerSlots( _stack, this );
		_stack.Open( _playerSlots );
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
			_stack.Close( _playerSlots );
			_playerSlots = null;
		}

		_stack.Open( _islandPanel );

		if ( newPlayer && Players.Roster.Current is { InstantAction: true } )
		{
			FrontEndLines.ExplainInstantAction();
		}
		else if ( newPlayer && Players.Roster.Current != null )
		{
			Players.Roster.Current.AddKey();

			// With nobody to hand it over, the key would never show.
			if ( Advisor.Current is { CanSpeak: true } )
				FrontEndLines.GiveLobbyTour( KeyHandedOver );
			else
				_islandPanel.ShowKeys();
		}
		else
		{
			Advisor.Current?.Hush();
		}
	}

	/// <summary>
	/// The advisor's cue in his tour of the lobby: goldkey, the key effect (87) at the player's key
	/// count in the top right, and the panel looks at the player's keys again and shows the one just
	/// given (Advisor_Update, 0x00599880). The effect is a ring of little keys turning round the count,
	/// which an effector draws back in when it ends before they burst into sparkles. It is drawn with
	/// the panel, so a menu opened over the panel covers it (see <see cref="ScreenParticles"/>).
	/// </summary>
	private void KeyHandedOver()
	{
		UiSounds.GoldKeyHandedOver();
		ParticleSystem.Current?.Spawn( (int)ParLib.P_EFFECT_Key, 90000, 0, 7000, owner: _islandPanel );
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
		// Once a frame, after the stack has dealt out the frame's clicks and keys and anything they did has
		// opened or closed what it does, so a choice that closes the menu and opens the options screen in one
		// go never lets him go in between. Each window says whether it pauses the game; what that means here
		// is the lobby's to say.
		if ( Advisor.Current is { } advisor )
			advisor.Paused = _stack.AnyPausing;
	}

	/// <summary>
	/// The lobby is ending. The original's front end empties the advisor's queue as it stops (0x005e4140), through
	/// AdvisorQueue_Clear (0x005d6060), which cuts him off with Advisor_StopSpeaking when he is busy - so he cries
	/// out if he was mid-line. Its windows have already been closed by the stack, which ends before it.
	/// </summary>
	protected override void OnDelete() => Advisor.Current?.Hush();

	/// <summary>
	/// Escape, with no box to type into, as the stack hands it over. The lobby's key handler (0x005e41c0)
	/// opens the game menu on it (GameMenu_Open with 1) unless it is already open, and the menu's handler
	/// (0x0048bd40) closes it on the same key. Whether the original opens it over a message box or the
	/// options screen was not established; here a modal window in front keeps Escape from it.
	/// </summary>
	private void MenuKey( UiWindow? front )
	{
		if ( front is GameMenu menu )
		{
			_stack.Close( menu );
			return;
		}

		if ( front is { Modal: true } )
			return;

		_stack.Open( new GameMenu( _stack, LobbyMenuChoices() ) );
	}

	/// <summary>
	/// The lobby's game menu, as GameMenu_BuildLobby (0x0048c600) adds its choices top to bottom, each with its
	/// id in the lobby menu's handler (0x0048bd40).
	///
	/// <para>
	/// Go Online (9), or Go Offline (10) while online, which is never here; Options (11), left out while the
	/// online side is busy, which it never is; Select New Player (12), only when the front end's screen says so,
	/// taken here to mean while someone is playing; then Resume Game (14) and Quit Game (15). Return To Park (13)
	/// takes the place of the first three while visiting someone else's park online.
	/// </para>
	/// <para>
	/// Go Online would start connecting (0x005b5cc0), but the online world is a dead end here, so it only closes
	/// the menu. Options closes it and opens the <see cref="OptionsScreen"/>. Select New Player and Quit Game ask
	/// first, in a message box over the menu (UITEXT 14 and 9), and the menu stays until the tick (0x0048bc50,
	/// 0x0048bc30). Resume Game closes it, and so does Escape.
	/// </para>
	/// </summary>
	private List<GameMenu.Item> LobbyMenuChoices()
	{
		var choices = new List<GameMenu.Item>
		{
			new( UIStrings.GoOnline, 9, menu =>
			{
				_stack.Close( menu );
				Log.Info( "Front end: Go Online - the online world is a dead end, so nothing more happens" );
			} ),

			new( UIStrings.Options, 11, menu =>
			{
				_stack.Close( menu );
				OptionsScreen.Open( _stack );
			} )
		};

		if ( Players.Roster.Current != null )
		{
			choices.Add( new( UIStrings.SelectNewPlayer, 12, menu => _stack.Open( new MessageBox( _stack, Localization.Get( UIStrings.ConfirmNewPlayer ), () =>
			{
				_stack.Close( menu );
				SelectNewPlayer();
			} ) ) ) );
		}

		choices.Add( new( UIStrings.ResumeGame, 14, menu => _stack.Close( menu ) ) );
		choices.Add( new( UIStrings.QuitGame, 15, _ => AskToQuit() ) );

		return choices;
	}
}
