namespace OpenTPW;

/// <summary>
/// Handles the creation and management of various systems, including the game
/// window.
/// </summary>
internal static class Game
{
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

		//
		// Custom OpenTPW cache directory (mainly for editor-related stuff)
		//
		CacheFileSystem = new BaseFileSystem( $"./.opentpw" );

		//
		// Init renderer
		//
		Render = new();

		//
		// Init audio. Opening the device can fail - no sound card, no audio server - and that is
		// not a reason to stop, so Audio.Init reports it and leaves everything a no-op.
		//
		Audio.Init();

		//
		// Create level
		//
		var level = new Level( "jungle" );

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
		Render.OnRender += level.Render;
		Render.Run();

		Audio.Shutdown();
	}
}
