namespace OpenTPW;

/// <summary>
/// Handles the creation and management of various systems, including the game
/// window.
/// </summary>
internal static class Game
{
	/// <summary>
	/// How many steps the loading bar expects the lobby to take - one for every texture, shader,
	/// material and mesh registered while it loads. The loading screen logs the real count when it
	/// closes, and this is the count it logs for the lobby as it stands; it wants bringing into line
	/// again whenever the lobby comes to load more.
	/// </summary>
	private const int LobbyLoadSteps = 3214;

	public static void Run( string[] args )
	{
		Log = new();

		//
		// Check if the game data directory exists
		//
		if ( !Path.Exists( $"{Settings.Default.GamePath}/data/" ) )
			throw new DirectoryNotFoundException( "Theme Park World not found" );

		// Register game data directory
		FileSystem = new BaseFileSystem( $"{Settings.Default.GamePath}/data/" );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );

		// Archives are found by appending the extension and checking the file exists, which is
		// case sensitive on anything but Windows. One archive in the whole game is spelled in
		// capitals - data\global\Speech\lips.WAD, the advisor's lip sync - and without this it
		// is invisible.
		FileSystem.RegisterArchiveHandler<WadArchive>( ".WAD" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );

		//
		// Check if the save data directory exists (create if not)
		//
		if ( !Path.Exists( $"{Settings.Default.GamePath}/save/" ) )
			Directory.CreateDirectory( $"{Settings.Default.GamePath}/save/" );

		// Register save data directory
		SaveFileSystem = new BaseFileSystem( $"{Settings.Default.GamePath}/save/" );

		// The machine's options, which the original reads before it sets anything else up.
		SaveFolder.LoadConfig();

		// The players in save\users, kept for the whole run as the original keeps them (WinMain, 0x0045aa74)
		// rather than with the lobby.
		Players.Roster.Load();

		//
		// Custom OpenTPW cache directory (mainly for editor-related stuff)
		//
		CacheFileSystem = new BaseFileSystem( $"./.opentpw" );

		//
		// Init renderer
		//
		Render = new();

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
	/// Asks for the lobby to be ended and built again once the frame in progress has been shown. For the debug
	/// console, standing in for a park's exit, which in the original comes back to the lobby the same way: Exit
	/// To Lobby (FUN_005508b0 with 2), state 0xb ending the park, and state 1 loading the lobby.
	/// </summary>
	internal static void RequestLobbyReload() => _lobbyReloadAsked = true;

	/// <summary>
	/// Ends the lobby and loads it again, if that was asked for. Run on PostUpdate, so between two frames: the
	/// frame in progress has been presented, and the next has not yet begun the command list that the loading
	/// screen draws its own frames with.
	/// </summary>
	private static void ReloadLobbyIfAsked()
	{
		if ( !_lobbyReloadAsked )
			return;

		_lobbyReloadAsked = false;

		Level.Current.Unload();
		LoadLobby( openAudio: false );
	}
}
