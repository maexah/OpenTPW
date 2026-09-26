namespace OpenTPW;

/// <summary>
/// Handles the creation and management of various systems, including the game
/// window.
/// </summary>
internal static class Game
{
	/// <summary>
	/// What the loading bar expects the lobby to take on a <b>first-ever run</b> - one step for every
	/// texture, shader, material and mesh registered while it loads.
	///
	/// <para>
	/// <b>This is a seed, and it is not maintained.</b> Every load records what it really cost,
	/// and the next load of that same situation expects the measurement instead - see
	/// <see cref="LoadStepCounts"/>. So this is only what a fresh install uses before anything has been
	/// measured: it is allowed to drift, and correcting it by hand buys nothing.
	/// </para>
	///
	/// <para>
	/// A rebuild re-registers only what the caches do not already hold, which is why a rebuild keeps a
	/// count of its own in <see cref="LoadStepCounts"/>.
	/// </para>
	/// </summary>
	private const int LobbyLoadSteps = 829;

	public static void Run( string[] args )
	{
		Log = new();

		//
		// Find the installed game. Nothing below this can run without it, and GameDir has already said
		// where it looked and what would put it right, so there is nothing to add by throwing.
		//
		if ( !GameDir.Find( args ) )
		{
			Environment.ExitCode = 1;
			return;
		}

		// Register game data directory
		FileSystem = new BaseFileSystem( GameDir.Data );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );

		//
		// Register save data directory - save\ beside the game's data, where the original keeps it, so that a
		// park saved by one is seen by the other. Made here if it is not there: a copy of the game that has
		// never been played has no save folder yet, and mapping one does not make it.
		//
		var saveFolder = GameDir.FindSaveFolder();

		Directory.CreateDirectory( saveFolder );
		SaveFileSystem = new BaseFileSystem( saveFolder );

		// The machine's options, which the original reads before it sets anything else up.
		SaveFolder.LoadConfig();

		// And the ones it has no room for, which are OpenTPW's own - how the game fills the screen.
		SaveFolder.LoadDisplay();

		// The players in save\users, kept for the whole run as the original keeps them (WinMain, 0x0045aa74)
		// rather than with the lobby.
		Players.Roster.Load();

		//
		// Custom OpenTPW cache directory (mainly for editor-related stuff). Kept with the player rather than
		// in whatever directory the game was started from: a copy of the game may sit somewhere nothing may
		// write to, and nothing in here is worth keeping if it is lost.
		//
		var cacheFolder = Path.Join(
			Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "OpenTPW", "cache" );

		Directory.CreateDirectory( cacheFolder );
		CacheFileSystem = new BaseFileSystem( cacheFolder );

		//
		// Init renderer
		//
		Render = new();

		// Now there is a window to put into it. Only when it is not simply a window, so the usual way
		// of starting does not ask the display for anything at all.
		if ( GameOptions.Current.DisplayMode != DisplayMode.Windowed )
			Display.Apply( Render.Window, GameOptions.Current.DisplayMode, GameOptions.Current.FullScreenSize );

		// The original shows these once a run, before its first lobby, and none is built: the Bullfrog splash and
		// the legal screen (LoadingScreen_Begin 0x00587380, whose files ship as Splash_American.tga and
		// Legal_American.tga), then the Bullfrog movie and one park movie (states 5 and 7, docs/exe/boot.md). The
		// lobby plan cut them (docs/QUEUE.md, section G); they are counted so the census names them.
		Unimplemented.Report( "BOOT_SPLASH" );
		Unimplemented.Report( "BOOT_LEGAL_SCREEN" );
		Unimplemented.Report( "INTRO_MOVIE_BULLFROG" );
		Unimplemented.Report( "INTRO_MOVIE_PARK" );

		//
		// Everything between here and the lobby's first frame happens behind the loading screen,
		// which puts itself away for good once the level exists.
		//
		LoadLobby( openAudio: true );

		//
		// Run game loop, over whichever level is current - a level built between two frames in place of
		// another is the one the next frame runs.
		//
		Render.OnUpdate += () => Level.Current.Update();
		Render.OnRender += () => Level.Current.Render();
		Render.PostUpdate += ReloadLobbyIfAsked;
		Render.Run();

		// Whoever is still playing is saved as the game closes, however it was closed (WinMain_Main, 0x0045acfc).
		Players.Roster.SaveAndDeselect();

		Audio.Shutdown();
	}

	/// <summary>Whether the lobby is to be built again once the frame in progress has been shown - see <see cref="RequestLobbyReload"/>.</summary>
	private static bool _lobbyReloadAsked;

	/// <summary>
	/// Loads the lobby behind the loading screen, as the original's state 1 does. Once it exists the advisor's
	/// duck is let go at once, as state 1 ends with Sound_SetSpeechDuck(0) (0x0054e6a6), so nothing the scene
	/// before left ducked stays ducked.
	/// </summary>
	/// <param name="openAudio">Whether to open the audio device behind this loading screen - only the first.</param>
	private static void LoadLobby( bool openAudio )
	{
		using ( new LoadingScreen( "the lobby", LobbyLoadSteps ) )
		{
			//
			// Init audio. Opening the device can fail - no sound card, no audio server - and that
			// is not a reason to stop, so Audio.Init reports it and leaves everything a no-op.
			//
			if ( openAudio )
				Audio.Init();

			//
			// Create level
			//
			_ = new Level( "jungle" );
		}

		Audio.Duck( 1f, 0f );
	}

	/// <summary>
	/// Asks for the lobby to be ended and built again once the frame in progress has been shown. This is a
	/// park's <b>Exit To Lobby</b>, and the debug console's reload with it - the original answers both the
	/// same way: Exit To Lobby writes the state machine's exit reason (FUN_005508b0 with 2), state 0xb ends
	/// the park, and state 1 loads the lobby. See <see cref="UI.ParkFrontEnd"/> for the menu it comes from.
	/// </summary>
	internal static void RequestLobbyReload() => _lobbyReloadAsked = true;

	/// <summary>
	/// Ends the lobby and loads it again, if that was asked for. Run on PostUpdate, so between two frames: the
	/// frame in progress has been presented, and the next has not yet begun the command list that the loading
	/// screen draws its own frames with.
	/// </summary>
	private static void ReloadLobbyIfAsked()
	{
		// A park first, because asking for one supersedes asking for the lobby again.
		if ( _parkAsked is { } themeName )
		{
			_parkAsked = null;

			Level.Current.Unload();
			LoadPark( themeName );

			return;
		}

		if ( !_lobbyReloadAsked )
			return;

		_lobbyReloadAsked = false;

		Level.Current.Unload();
		LoadLobby( openAudio: false );
	}

	/// <summary>Which park has been asked for, to be built once the frame in progress has been shown.</summary>
	private static string? _parkAsked;

	/// <summary>
	/// Asks for a park to be entered once the frame in progress has been shown. Between frames for the
	/// same reason the lobby's own reload is - see <see cref="ReloadLobbyIfAsked"/> - and in the same
	/// place the original changes scene, which is its state machine rather than the middle of a tick.
	/// </summary>
	/// <remarks>
	/// Lower-cased here, at the way in, rather than only in <see cref="Level"/> - the loading screen is
	/// built from this name before a Level exists, so normalising further down would leave the bar
	/// captioned "Jungle" while everything the level itself logs says "jungle".
	/// </remarks>
	internal static void RequestParkLoad( string themeName ) => _parkAsked = themeName.ToLowerInvariant();

	/// <summary>
	/// What the loading bar expects a park to take on a <b>first-ever run</b>.
	///
	/// <para>
	/// <b>A seed, not maintained</b>, for the same reason as <see cref="LobbyLoadSteps"/>: what each
	/// kind of load really costs is measured and remembered, so this is only where a fresh install
	/// starts from - see <see cref="LoadStepCounts"/>. The original budgets 500 for its own park
	/// load (LoadingScreen_Begin with 500, from state 9); the two count different things. A step is an
	/// <c>Asset.Register</c>, so anything that loads fewer assets lowers it.
	/// </para>
	/// <para>
	/// This is the count for a park built <i>cold</i> - entered from the lobby, with nothing of its own
	/// in the caches. Restart Park builds a park a second time in one run, and that rebuild costs roughly
	/// half as much, for the same reason a rebuilt lobby does; <see cref="LoadStepCounts"/> learns both.
	/// </para>
	/// <para>
	/// <b>The constant stays at the cold number</b>: it seeds only a first-ever run, and each situation's
	/// real count, cold or rebuilt, is learned by <see cref="LoadStepCounts"/>.
	/// </para>
	/// </summary>
	private const int ParkLoadSteps = 918;

	/// <summary>Builds a park behind the loading screen, the way <see cref="LoadLobby"/> builds the lobby.</summary>
	private static void LoadPark( string themeName )
	{
		using ( new LoadingScreen( themeName, ParkLoadSteps ) )
		{
			_ = new Level( themeName, Level.Scene.Park );
		}

		Audio.Duck( 1f, 0f );
	}
}
