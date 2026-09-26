using NeoVeldrid;

namespace OpenTPW.UI;

/// <summary>
/// A park's interface: the management gadget in the corner, the camcorder's viewfinder, the game menu
/// Escape brings up and what its choices do, and what the advisor is given to say.
///
/// <para>
/// The original builds the same widgets twice. GameMenu_Open (0x0048c830) branches to GameMenu_BuildLobby
/// (0x0048c600) or GameMenu_BuildPark (0x0048c150) on one menu list, and Escape reaches it by a different
/// road in each scene: the lobby's key handler opens it unless its island camera cancels a flight into a
/// park instead (0x005e41c0), while a park hands the key
/// to the binding tables built in FUN_0040cb80 - game action 0 first closes the staff locator or installs
/// the idle mode over whatever is held, and only failing that does the shortcut action 0 "menu"
/// (0x0040c4d0) open it. Both roads run on the key's release, and here both are
/// <see cref="WindowStack.KeysWithoutFocus"/>: the park's is <see cref="ParkKeys"/>.
/// </para>
/// <para>
/// <b>Leaving first person is part of that.</b> In first person the key goes to the viewfinder's own layer,
/// whose handler (0x00488a00) answers a key-up on VK_ESCAPE and one whose bound action is camcorder (16)
/// identically, by leaving first person - so Escape and C are one road out. The viewfinder's eject button and a right
/// click on its layer are the others (<see cref="ParkViewfinder"/>). <see cref="MenuKey"/> takes the Escape road first.
/// </para>
/// <para>
/// <b>The choices are the park's own, and they are not the lobby's.</b> GameMenu_BuildPark adds them in
/// this order, each with its id in the park menu's handler (0x0048b6a0): Load 1, Save 2, Restart Park 3,
/// Publish Park 4, then Go Offline 7 while the online side is connected, then Options 5 unless
/// FUN_005b6230 finds an online object busy, then Resume Game 0, Exit To Lobby 6 and Quit Game 8.
/// <b>Neither of those two conditions can hold here</b> - nothing ever connects - so Go Offline is left
/// out and Options is always shown. That is what the original's own tests come to in an offline game,
/// not a simplification of them.
/// </para>
/// <para>
/// <b>Exit To Lobby asks nothing first.</b> The handler's case 6 writes the state machine's exit reason
/// and closes the menu - FUN_005508b0 is nothing but <c>DAT_00879088 = param</c> - and state 0xb then
/// tears the park down and state 1 builds the lobby. Restart Park and Quit Game do ask, in a message box,
/// with UITEXT 11 and 9.
/// </para>
/// <para>
/// <b>Engine and content.</b> The menu, the message box, the options screen and all the window handling
/// are engine, and are the same code in both scenes. This file is the park's content: which choices it
/// has, in what order, what each one does, the lines it has the advisor say (<see cref="ParkLines"/>),
/// and holding him while a window pauses the game.
/// </para>
/// </summary>
internal sealed class ParkFrontEnd : Panel
{
	/// <summary>
	/// What this scene's windows draw, loaded behind the loading screen rather than as each first opens -
	/// the arrangement <see cref="FrontEnd"/> already has, and what keeps a menu opened mid-play from
	/// hitching. It is the lobby's list without the five only the lobby wears: its login and delete
	/// buttons, the joystick panel, the logo, and the waved dialog the new player box uses. What is left
	/// is the dimming backdrop, the message box and the options screen.
	/// </summary>
	private static readonly string[] Meshes =
	[
		"LOLIGHT", "w_dialog", "b_okay", "b_exit", "!frame", "!f_plain", "b_ltoggle", "f_helpbg",
		"f_screen", "f_optpanel", "f_optpanel2", "f_optpanel3", "b_on", "b_on2", "b_scroller",

		// The management gadget's own, which are up for as long as the park is - see <see cref="ParkGadget"/>.
		// They are named by the model files ui.wad ships; the layout stream asks for several of them under
		// the name of their first mesh node instead, which is why "base" is mainpanel and "guage" is gauge.
		"mainpanel", "gauge", "date", "b_retract", "b_buy", "b_camera", "b_info", "b_map", "b_money",
		"b_resrch",

		// The currency icon beside the bank balance in the top-left corner - the stream's 0x32, and the
		// one piece of artwork that cluster has of its own. The balance beside it is lettering.
		"i_dollar",

		// The arm the gadget carries a panel out on, and the camcorder panel that rides on it - see
		// <see cref="ParkGadget"/>. The two long pieces are another case of the naming above: the stream
		// asks for them as pan_money and pane_money, which are the first nodes of panel.md2 and
		// panelend.md2, and each of those models carries one part per category.
		"panel", "panelend", "handle", "b_1person", "b_postcard",

		// The park map screen - see <see cref="ParkMapScreen"/>. Its scroll arrows are asked for by
		// their node names nuup/nudown/nuleft/nuright, which belong to b_mapup/dn/left/right.md2, and
		// the three panel backings are buttpan1/buttpan2/textpan in f_mapbut1/f_mapbut2/f_maptext.md2.
		// b_okay is already loaded above, for the message box.
		"b_mapup", "b_mapdn", "b_mapleft", "b_mapright", "b_plus", "b_minus",
		"f_mapbut1", "f_mapbut2", "f_maptext",

		// The golden key and ticket in the top right corner, which the gadget shows live - see
		// <see cref="ParkGadget"/>. The lobby loads gkey too, but a park entered without going through
		// the lobby first would otherwise fetch them mid-play.
		"gkey", "gtick",

		// The camcorder's frame and its eject button - see <see cref="ParkViewfinder"/>. Loaded here
		// rather than as first person is first entered, so the toggle does not hitch.
		"f_viewfinder", "b_eject",

		// The buy and hire screens, from their own layout streams at 0x00754cf8 and 0x00751fa8 - see
		// docs/exe/hud.md, where both are walked. The frame and the list backings first, then the four
		// buy tabs, the five hire tabs, the scrollbar's three pieces, and the two cross-link buttons
		// each screen carries to the other.
		//
		// THE NAME THE STREAM HASHES IS NOT ALWAYS THE NAME OF THE FILE. The layout stream asks for a
		// mesh by a hash of the model's first NODE name; UiMesh.Get loads ui/<name>.md2 by FILE name.
		// Where an artist named the node differently from the file, the two diverge - the node "b_sride"
		// is in a model shipped as b_srides.MD2.
		//
		// These are the names ui.wad really holds, listed from the archive rather than inferred:
		//     buyitem -> f_buyitem      hirestaff -> list_hirestaff    balance -> f_balance
		//     staffinfo -> f_staffinfo  staffpic  -> f_staffpic        b_sride -> b_srides
		//     b_sresrcher -> b_sresrhcer   <- the FILE is misspelled; its own texture is not
		//
		// A name that does not resolve loads nothing and warns, which is the visible failure rather
		// than the silent one.
		"!frame", "!slider", "f_buyitem", "list_hirestaff", "f_staffinfo", "f_staffpic", "f_balance",
		"b_srides", "b_sshop", "b_sshow", "b_sfeature",
		"b_shandy", "b_smech", "b_senter", "b_sguard", "b_sresrhcer",
		"b_scroller", "b_up", "b_down", "b_allstaff", "b_allthings", "i_boxtick",

		// The Information and Money categories' own screens - allstaff (0x750e10), allitems (0x7508e0
		// and its three list trees), allpeeps (0x7506c8) and entryprice (0x751798). Every one of these
		// names was RESOLVED from the hash its layout stream carries, against ui.wad's own table, rather
		// than guessed: the stream asks for a mesh as h = (c ^ h) * 47 over the model's first node name,
		// and the arithmetic was checked against two hashes this file already knew before any of it was
		// trusted. The entry-price screen's three right-hand buttons resolve to b_staffcost / b_loans /
		// b_finance, not a spinner, which the handler confirms.
		"list_allstaff", "list_kids", "list_all",
		"b_finance", "b_loans", "b_staffcost", "b_door", "b_plus", "b_minus",
		"b_parkinfo", "b_kids",

		// THE SCREEN FRAMES, found by hashing the models' NODE names rather than their file names.
		// w_big is the node "window4" and w_small the node "window1"; f_varibox is the boxed number the
		// entry-price spinner sits in.
		"w_big", "w_small", "f_varibox",

		// The per-object management window, stream 0x00755150 - see <see cref="ParkObjectWindow"/>.
		// Listed so the window does not fetch all ten the first time a player clicks a ride, which is
		// the hitch this list exists to prevent. w_med is the node "window2", the
		// third of the same frame family as w_big and w_small; f_chev is the node "chev"; and
		// b_rideit is the node "b_ride it!", whose space and exclamation mark are why no token scan
		// ever matched its hash.
		"w_med", "f_chev", "b_rideit", "b_erase", "b_move", "b_track", "b_queue",
		"b_callmech", "b_upgrade", "b_scrollera",

		// The staff list's tabs are the hire screen's five models: ui.wad ships one scientist tab model,
		// b_sresrhcer.MD2, misspelled where its textures (b_sresrcher.wct) are not. This b_sguard is
		// already in the list above.
		"b_sguard"
	];

	/// <summary>How far down a park's first choice starts - see <see cref="GameMenu"/>.</summary>
	private const int FirstTop = 10;

	private readonly WindowStack _stack;
	private readonly string _themeName;

	/// <summary>The management gadget, up for as long as the park is - see <see cref="ParkGadget"/>.</summary>
	private readonly ParkGadget _gadget;

	/// <summary>The camcorder's frame, up only in first person - see <see cref="ParkViewfinder"/>.</summary>
	private readonly ParkViewfinder _viewfinder;

	private bool _quitting;

	/// <summary>Whether the line a park opens with has been said - see <see cref="ParkLines.ExplainGadget"/>.</summary>
	private bool _explained;

	/// <param name="themeName">
	/// Which park this is, so Restart Park can load the same one again rather than guessing at it.
	/// </param>
	public ParkFrontEnd( WindowStack stack, string themeName )
	{
		_stack = stack;
		_themeName = themeName;
		_stack.KeysWithoutFocus = ParkKeys;

		foreach ( var mesh in Meshes )
			UiMesh.Get( mesh );

		// The gadget is open for the whole of a park, as the lobby's island panel is open for the whole
		// of the lobby. It is not modal and does not pause, so the park carries on behind it.
		_gadget = new ParkGadget( stack );
		_stack.Open( _gadget );

		// The camcorder's frame, opened after the gadget so that it draws over it - the stack renders
		// back to front. The two are never up together in any case: each puts itself away when the
		// other's moment comes.
		_viewfinder = new ParkViewfinder( stack );
		_stack.Open( _viewfinder );
		_stack.ViewRightClick = ParkViewfinder.RightClickAnswer;
	}

	/// <summary>
	/// The park's keys, with no box to type into, as the stack hands them over: Escape, on its release, once for each
	/// time it is let go. The park's world control runs its binding tables on a key's release (message 0x1000b,
	/// 0x00488921) and only latches a row on its press, so a held Escape, its repeats included, does nothing until it
	/// comes up (<c>docs/exe/scenes.md</c>, "The park Escape route"). The menu and the viewfinder take the key on its
	/// release too. The build keys are read in <see cref="Level"/>, and the camcorder key by the camera modes.
	/// </summary>
	private void ParkKeys()
	{
		foreach ( var key in Input.KeysReleased )
		{
			if ( key == Key.Escape )
				MenuKey( _stack.Windows.Count > 0 ? _stack.Windows[^1] : null );
		}
	}

	/// <summary>
	/// Escape, on its release, as <see cref="ParkKeys"/> hands it over. The park's own handler closes the menu
	/// on the same key: its message 0x1000b case answers VK_ESCAPE (0x1b) by hiding and destroying the menu
	/// and letting the park run again. A modal window in front keeps Escape from it, as in the lobby.
	///
	/// <para>
	/// <b>Not the original's over a park screen.</b> The six management screens, an object window and the map take the
	/// focus as they open, and their key handler closes the screen on a plain Escape let go (0x00488bc6).
	/// Here those screens are modal and keep the key, and an object window, which is not, lets it through to the hand and
	/// the menu (<c>docs/QUEUE.md</c> Q119).
	/// </para>
	/// </summary>
	private void MenuKey( UiWindow? front )
	{
		// The menu's handler compares the key alone (0x0048bb36), so an Escape let go with a modifier held closes it.
		if ( front is GameMenu menu )
		{
			_stack.Close( menu );
			return;
		}

		if ( front is { Modal: true } )
			return;

		// In first person the key is the viewfinder layer's, which leaves first person on a key-up whose key is
		// VK_ESCAPE, whatever modifier is held, or whose action is camcorder (16), found by key and modifier alike
		// (0x00488a00). The layer's other ways out are its eject button and a right click - see ParkViewfinder.
		if ( ParkCamcorderCameraMode.Active )
		{
			ParkCamcorderCameraMode.Leave();
			return;
		}

		// The rest is the park's binding tables, whose Escape rows - the game table's row 0 and the shortcuts' row 0 -
		// name no modifier, which a row must match exactly (FUN_0040c990).
		if ( !Input.NoModifierHeld )
			return;

		// Escape over anything but the idle mode installs the idle mode and is spent doing it (0x0040c368; see
		// docs/exe/park-engine.md, "The hand's ways out"): whatever is in the hand is let go of and a build tool put
		// away. The next opens the menu.
		if ( ParkHand.LetGo() is { } letGo )
		{
			Log.Info( $"Escape: {letGo}" );
			return;
		}

		_stack.Open( new GameMenu( _stack, MenuChoices(), FirstTop ) );
	}

	/// <summary>
	/// Escape, for the debug console - see <see cref="Level.OpenBuyScreen"/> for why the console needs a
	/// way in that does not go through a key.
	///
	/// <para>
	/// It goes through <see cref="MenuKey"/> rather than opening a <see cref="GameMenu"/> itself, so the
	/// console takes the real road including the toggle, the modal guard and the camcorder exit. A command
	/// that opened the menu directly would be testing a path no player reaches - and this one exists to
	/// hold the game clock, which is exactly what a second, private road would be free to get wrong.
	/// </para>
	/// </summary>
	internal void DebugOpenMenu() => MenuKey( _stack.Windows.Count > 0 ? _stack.Windows[^1] : null );

	/// <summary>
	/// The park's game menu, in the order GameMenu_BuildPark adds its choices, each with its id in the park
	/// menu's handler - see the class remarks for where the order and the ids come from.
	/// </summary>
	private List<GameMenu.Item> MenuChoices()
	{
		return
		[
			new( UIStrings.Load, 1, menu => NotYet( menu, "Load Game", "no park has been saved to read back" ) ),
			new( UIStrings.Save, 2, menu => NotYet( menu, "Save Game", "nothing writes a park back yet" ) ),

			new( UIStrings.RestartPark, 3, menu => _stack.Open( new MessageBox( _stack,
				Localization.Get( UIStrings.ConfirmRestartPark ), () => RestartPark( menu ) ) ) ),

			new( UIStrings.PublishPark, 4, menu => NotYet( menu, "Publish Park", "the online world is a dead end here" ) ),

			new( UIStrings.Options, 5, menu =>
			{
				_stack.Close( menu );
				OptionsScreen.Open( _stack );
			} ),

			new( UIStrings.ResumeGame, 0, menu => _stack.Close( menu ) ),
			new( UIStrings.ExitToLobby, 6, ExitToLobby ),
			new( UIStrings.QuitGame, 8, _ => AskToQuit() )
		];
	}

	/// <summary>
	/// Exit To Lobby (case 6), which asks nothing first. <see cref="Game.RequestLobbyReload"/> is the pair
	/// of states the original answers this with: the park ends and the lobby is built again, between two
	/// frames rather than in the middle of a tick.
	/// </summary>
	private void ExitToLobby( GameMenu menu )
	{
		_stack.Close( menu );
		Log.Info( "Park menu: Exit To Lobby" );

		Game.RequestLobbyReload();
	}

	/// <summary>
	/// Restart Park, once its box is ticked (UITEXT 11, handler case 3).
	///
	/// <para>
	/// <b>This is a departure, and a traced one.</b> The original's tick is FUN_0048b5b0, and it writes exit
	/// reason <b>1</b> - not the 2 that Exit To Lobby writes. Against the state machine, reason 1 sends the
	/// in-park loop to 0xd and then 0xe, which quietens the advisor and calls FUN_005ac5f0, and goes
	/// <i>back into state 10</i>; only 2 and 3 reach 0xb, where a park is torn down. So the original
	/// restarts a park <b>in place, with no teardown and no load</b>. What FUN_005ac5f0 resets is not
	/// established: it forwards to FUN_005ac650, whose decompilation is unreliable - an unrecovered frame
	/// with unaff_EBX and unaff_ESI standing in for arguments - so nothing is claimed about it here.
	/// </para>
	/// <para>
	/// What this does instead is load the park again from the copy that ships beside it, which is where
	/// every park still starts. While nothing is ever written back that file is still where the park
	/// began, so reading it again is the closest thing to a reset that exists here. <b>When a
	/// park can be saved and played on, this has to start meaning "back to the beginning" rather than "read
	/// the file again"</b>, and it should stop reloading the scene when there is a state to reset in place.
	/// </para>
	/// </summary>
	private void RestartPark( GameMenu menu )
	{
		_stack.Close( menu );
		Log.Info( $"Park menu: Restart Park - loading {_themeName} again" );

		Game.RequestParkLoad( _themeName );
	}

	/// <summary>
	/// A choice the original has and this cannot do yet: the menu closes and the reason is said out loud.
	/// The lobby's Go Online already works this way. Leaving them out would misreport the menu's shape,
	/// and making them look as though they had worked would be worse than either.
	/// </summary>
	private void NotYet( GameMenu menu, string choice, string why )
	{
		_stack.Close( menu );
		Log.Info( $"Park menu: {choice} - {why}, so nothing more happens" );
	}

	/// <summary>Quit Game (case 8), which asks in a message box with UITEXT 9 and quits on the tick.</summary>
	private void AskToQuit() => _stack.Open( new MessageBox( _stack, Localization.Get( UIStrings.ConfirmQuit ), Quit ) );

	/// <summary>
	/// Leaves the game, the way <see cref="FrontEnd"/> does: the window closes once the frame in progress
	/// has been shown, so nothing is drawn into a window that has gone.
	/// </summary>
	private void Quit()
	{
		if ( _quitting )
			return;

		_quitting = true;
		Log.Info( "Park menu: quitting" );

		Render.PostUpdate += () => Render.Window.SdlWindow.Close();
	}

	/// <summary>
	/// Once a frame, after the stack has dealt out the frame's clicks and keys and anything they did has
	/// opened or closed what it does - the arrangement <see cref="FrontEnd.OnUpdate"/> already has, and
	/// for the same reason.
	/// </summary>
	protected override void OnUpdate()
	{
		if ( Advisor.Current is not { } advisor )
			return;

		// A park is the scene the original really does pause. The game menu, a message box and the options
		// screen all set Pauses as they open, so all three arrive here. For the first two that holds his
		// sample where it has got to and stops the clock his clips and his lead-in are timed on. The
		// options screen is the exception: OptionsScreen.Open calls
		// Advisor.StopQuietly first, fading his voice and throwing the line away, so by the time it pauses
		// there is nothing left to hold. The lobby pauses him too, but there it is a choice - see
		// Level.PausedByWindow and Advisor.Paused.
		//
		// Being paused does NOT take him off screen, and neither does the original's pause: FUN_004092a0
		// calls Advisor_PauseVoice (0x00598960) and the clock stop and nothing else that touches him, and
		// the one function that removes his model - Advisor_KillModel (0x00429d60) - has exactly three
		// callers, Advisor_StopSpeaking twice and Advisor_StopQuietly, none of which is the pause. So he
		// stands behind the menu here too - see Advisor's class remarks.
		advisor.Paused = _stack.AnyPausing;

		// The line a park opens with, said from the first running frame rather than from the constructor -
		// but NOT because the constructor would talk over the loading screen. Add only queues; nothing is
		// ever said outside Advisor.OnUpdate, and
		// no entity updates while a level is still being built, so queueing it there would have behaved
		// identically. It sits here because this is where the once-a-visit latch belongs, beside the pause
		// it has to respect.
		//
		// The latch is set BEFORE the line is queued, so a line he never voices - switched off in the
		// options, where Advisor.Speak drops it - still spends the "once". That is the original's own
		// behaviour, not an oversight: Advisor_SayResponse (0x00599050) returns 0 with the switch off and
		// the tick writes mLastActionStarted and the per-message counters regardless, spending the once on
		// the offer rather than on anything being heard.
		if ( !_explained && !advisor.Paused )
		{
			_explained = true;
			ParkLines.ExplainGadget();
		}
	}

	/// <summary>
	/// The park is ending: what is still queued goes with it, and he is cut off where he stands - the
	/// same call the lobby's front end makes as it stops, see <see cref="FrontEnd.OnDelete"/>.
	///
	/// <para>
	/// <b>This is the crying stop, not the quiet one.</b> <see cref="Advisor.Hush"/> goes through Advisor_StopSpeaking (0x005994e0), which
	/// cries out - one of samples 639 to 641 - whenever his voice was actually sounding. "Quietly" is a
	/// term of art here for <see cref="Advisor.StopQuietly"/>, a different path, which the options screen and
	/// the park map take. The cry is right rather than unfortunate: the original's own cry
	/// at the end of a scene comes from its front end emptying the queue, which is this.
	/// </para>
	/// </summary>
	protected override void OnDelete() => Advisor.Current?.Hush();
}
