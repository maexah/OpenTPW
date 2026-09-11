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
	/// closes; this wants bringing into line with it when the lobby comes to load more.
	/// </summary>
	private const int LobbyLoadSteps = 3166;

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
		Level level;

		using ( new LoadingScreen( "the lobby", LobbyLoadSteps ) )
		{
			//
			// Init audio. Opening the device can fail - no sound card, no audio server - and that
			// is not a reason to stop, so Audio.Init reports it and leaves everything a no-op.
			//
			Audio.Init();

			//
			// Create level
			//
			level = new Level( "jungle" );
		}

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
		Render.OnRender += level.Render;
		Render.Run();

		// Whoever is still playing is saved as the game closes, however it was closed (WinMain_Main, 0x0045acfc).
		Players.Roster.SaveAndDeselect();

		Audio.Shutdown();
	}
}
