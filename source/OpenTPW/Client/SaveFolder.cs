using System.Text;

namespace OpenTPW;

/// <summary>
/// The game's save folder - save\ beside its data, which <see cref="SaveFileSystem"/> maps - laid out as the
/// original lays it out.
///
/// <para>
/// The original keeps the options that belong to the machine in save\Config.tcf (<see cref="ConfigFile"/>),
/// each player in a folder of their own under save\users\ named for their slot and their name, with their
/// progress and their own options in gms.dat (<see cref="PlayerFile"/>), and each player's parks in a
/// folder per theme inside theirs. There is no list of players: the folders are the list.
/// </para>
/// <para>
/// File names are matched without regard to case when reading. The original ran where case never
/// mattered, and names its own files inconsistently - it writes "Config.tcf", and the safe-mode batch file
/// shipped beside it copies to "config.tcf" - so a file is written back under whatever name it was found by.
/// </para>
/// <para>
/// A file that will not read is reported and left alone, and whatever it held keeps its default. Every
/// file is written whole to one beside it and then moved into place, so a save cut short never leaves half
/// a file behind.
/// </para>
/// </summary>
internal static class SaveFolder
{
	private const string ConfigName = "Config.tcf";
	private const string DisplayName = "opentpw.cfg";
	private const string UsersFolder = "users";
	private const string OnlineFolder = "online";
	private const string PlayerFileName = "gms.dat";
	private const string EasyModeName = "easymode.TPWI";

	// What a park file's name holds: the original's search is "*" and then ".TPW*" (0x00f7b948, 0x007479f8).
	private const string ParkPattern = ".TPW";

	private static string[]? _themes;

	// A failed load leaves defaults in memory. A later readable destination does not make those
	// defaults safe to write over it. Scoped to the mapped filesystem so another installation is independent.
	private static BaseFileSystem? _unreadConfig;

	/// <summary>
	/// The themes every player has a folder and a park record for: the folders under data\levels that hold
	/// a global.sam - fantasy, hallow, jungle and space. The original walks a list of its own (0x00786b90)
	/// whose names were not read out of it; these are the names the data itself goes by.
	/// </summary>
	public static IReadOnlyList<string> Themes => _themes ??= FindThemes();

	/// <summary>Whether a park record is for a theme the game has - only those count their tickets (0x005af530).</summary>
	public static bool IsTheme( string name ) => Themes.Contains( name, StringComparer.OrdinalIgnoreCase );

	/// <summary>Reads save\Config.tcf into the options, if there is one - as the original does before anything else starts (0x00424930).</summary>
	public static void LoadConfig()
	{
		_unreadConfig = SaveFileSystem;
		if ( Find( ConfigName ) is not { } path )
		{
			_unreadConfig = null;
			return;
		}

		try
		{
			using var stream = SaveFileSystem.OpenRead( path );

			if ( ConfigFile.Read( stream ) is { } file )
			{
				GameOptions.Current.Apply( file );
				if ( file.CanWrite )
					_unreadConfig = null;
			}

			Log.Info( $"Saves: read the options from {path}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {path} would not read, so the options keep their defaults - {e.Message}" );
		}
	}

	/// <summary>Writes the machine's options to save\Config.tcf - the options screen's tick (0x004237f0), and a player being saved (0x00424820).</summary>
	public static void SaveConfig()
	{
		var path = ConfigName;

		try
		{
			if ( ReferenceEquals( _unreadConfig, SaveFileSystem ) )
				throw new InvalidDataException( "the options were not loaded successfully; leaving the original unchanged" );

			if ( Find( ConfigName ) is { } existing )
			{
				path = existing;
				using var original = SaveFileSystem.OpenRead( path );
				if ( ConfigFile.Read( original ) is not { CanWrite: true } )
					throw new InvalidDataException( "the existing options were not fully understood; leaving them unchanged" );
			}

			using var memory = new MemoryStream();
			GameOptions.Current.ToConfigFile().Write( memory );

			SaveFileSystem.WriteAllBytes( path, memory.ToArray() );
			Log.Info( $"Saves: wrote the options to {path}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the options could not be written to {path} - {e.Message}" );
		}
	}

	/// <summary>
	/// Reads save\opentpw.cfg: the settings that are OpenTPW's own, because the original's files have
	/// nowhere to put them. How the game fills the screen, and the mode full screen asks the display
	/// for - see <see cref="Display"/>.
	///
	/// <para>
	/// It is a plain text file of its own rather than more fields in Config.tcf, so that the original's
	/// file stays a file the original can still read: its resolution field has room for three modes and
	/// its own options screen shows a blank line for anything else. A line is a name and its values,
	/// and anything unreadable is passed over, so a file from a later version loses only what this
	/// version does not know.
	/// </para>
	/// </summary>
	public static void LoadDisplay()
	{
		if ( Find( DisplayName ) is not { } path )
			return;

		try
		{
			foreach ( var line in SaveFileSystem.ReadAllText( path ).Split( '\n' ) )
			{
				var parts = line.Trim().Split( ' ', StringSplitOptions.RemoveEmptyEntries );

				if ( parts.Length < 2 || parts[0].StartsWith( '#' ) )
					continue;

				switch ( parts[0].ToLowerInvariant() )
				{
					case "display":
						GameOptions.Current.DisplayMode = parts[1].ToLowerInvariant() switch
						{
							"fullscreen" => DisplayMode.FullScreen,
							"borderless" => DisplayMode.BorderlessFullScreen,
							_ => DisplayMode.Windowed
						};
						break;

					case "fullscreen" when parts.Length >= 3
						&& int.TryParse( parts[1], out var width )
						&& int.TryParse( parts[2], out var height ):

						var refresh = parts.Length >= 4 && int.TryParse( parts[3], out var rate ) ? rate : 0;
						GameOptions.Current.FullScreenSize = new VideoMode( width, height, refresh );
						break;

					// What a kind of load cost when it was last measured - see LoadStepCounts. Read before
					// anything loads, which is why this is called from Game.Run rather than with a scene.
					case LoadStepCounts.LineName when parts.Length >= 3 && int.TryParse( parts[2], out var steps ):
						LoadStepCounts.Restore( parts[1], steps );
						break;
				}
			}

			Log.Info( $"Saves: read the display settings from {path}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {path} would not read, so the display settings keep their defaults - {e.Message}" );
		}
	}

	/// <summary>
	/// Writes save\opentpw.cfg - see <see cref="LoadDisplay"/>. Written with Config.tcf by the options screen's tick,
	/// and alone when a loading screen has learned a new step count.
	/// </summary>
	public static void SaveDisplay()
	{
		var path = Find( DisplayName ) ?? DisplayName;
		var options = GameOptions.Current;

		var mode = options.DisplayMode switch
		{
			DisplayMode.FullScreen => "fullscreen",
			DisplayMode.BorderlessFullScreen => "borderless",
			_ => "windowed"
		};

		try
		{
			var text = new StringBuilder()
				.AppendLine( "# OpenTPW's own settings. The original's Config.tcf beside this one holds the" )
				.AppendLine( "# options it knows about; these are the ones it has no room for." )
				.AppendLine( $"display {mode}" )
				.AppendLine( $"fullscreen {options.FullScreenSize.Width} {options.FullScreenSize.Height} {options.FullScreenSize.RefreshRate}" );

			// How many steps each kind of load took last time, which is how the loading bar knows how far
			// to fill without being told - see LoadStepCounts. These are measurements rather than choices,
			// and they settle: once a situation has been seen, it is only written again if it changes.
			foreach ( var (key, steps) in LoadStepCounts.All )
				text.AppendLine( $"{LoadStepCounts.LineName} {key} {steps}" );

			SaveFileSystem.WriteAllBytes( path, Encoding.UTF8.GetBytes( text.ToString() ) );
			Log.Info( $"Saves: wrote the display settings to {path}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the display settings could not be written to {path} - {e.Message}" );
		}
	}

	/// <summary>
	/// The players in save\users - 0x005c7590, as the game starts. It makes save\users and save\online if they
	/// are missing, and takes every folder named for a slot, 1 to 4, and then a name at least one character long.
	/// The folders are taken in name order, as Windows lists them, and a later folder for a slot replaces an
	/// earlier one. A folder whose gms.dat is missing or will not read is still a player, with nothing read
	/// (<see cref="PlayerFile"/> null); each folder is given the theme folders it lacks.
	/// </summary>
	public static List<(int Slot, string Name, PlayerFile? File)> ScanPlayers()
	{
		var players = new List<(int, string, PlayerFile?)>();

		try
		{
			Directory.CreateDirectory( SaveFileSystem.GetAbsolutePath( UsersFolder ) );
			Directory.CreateDirectory( SaveFileSystem.GetAbsolutePath( OnlineFolder ) );

			var folders = SaveFileSystem.GetDirectories( UsersFolder )
				.Select( Path.GetFileName )
				.OfType<string>()
				.OrderBy( name => name, StringComparer.OrdinalIgnoreCase );

			foreach ( var folder in folders )
			{
				if ( folder.Length < 2 || folder[0] < '1' || folder[0] > '4' )
					continue;

				var slot = folder[0] - '1';
				var name = folder[1..];

				players.Add( (slot, name, LoadPlayer( slot, name )) );
				MakeThemeFolders( PlayerFolder( slot, name ) );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the players could not be looked for - {e.Message}" );
		}

		return players;
	}

	/// <summary>A player's gms.dat, or null if there is none or it will not open.</summary>
	public static PlayerFile? LoadPlayer( int slot, string name )
	{
		var folder = PlayerFolder( slot, name );

		if ( Find( PlayerFileName, folder ) is not { } path )
			return null;

		try
		{
			using var stream = SaveFileSystem.OpenRead( path );
			var file = PlayerFile.Read( stream );

			if ( !file.IsComplete )
				Log.Warning( $"Saves: {path} ended early or is not a player, so only what came before that was read" );

			return file;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {path} would not open - {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Makes a player's folder - 0x005c7f40 and 0x005c8190: the folder, a folder for each theme, a copy of each
	/// theme's easymode.TPWI for an Instant Action player (only jungle ships one), and their first gms.dat.
	/// </summary>
	public static void CreatePlayer( int slot, string name, PlayerFile file )
	{
		var folder = PlayerFolder( slot, name );

		try
		{
			Directory.CreateDirectory( SaveFileSystem.GetAbsolutePath( folder ) );
			MakeThemeFolders( folder );

			if ( file.InstantAction )
				CopyEasyModeParks( folder );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the folder for '{name}' could not be made - {e.Message}" );
		}

		SavePlayer( slot, name, file );
	}

	/// <summary>
	/// Writes a player's gms.dat. The options in it are always the options as they stand: the original writes
	/// its options object itself into the file (0x00423e90), whoever set them.
	/// </summary>
	/// <param name="slot">The player's slot.</param>
	/// <param name="name">The player's name.</param>
	/// <param name="file">What to write.</param>
	/// <param name="expectMissing">No gms.dat was there when this player was read; one found now is someone else's.</param>
	/// <returns>Whether the file was written.</returns>
	public static bool SavePlayer( int slot, string name, PlayerFile file, bool expectMissing = false )
	{
		var folder = PlayerFolder( slot, name );
		var path = Path.Join( folder, PlayerFileName );

		try
		{
			// A readable prefix is useful in memory, but never permission to replace the original.
			// Recheck the destination too: LoadPlayer may have failed and left a default PlayerFile.
			if ( !file.CanWrite )
				throw new InvalidDataException( "the player was not fully understood; leaving the original unchanged" );

			if ( Find( PlayerFileName, folder ) is { } existing )
			{
				if ( expectMissing )
					throw new InvalidDataException( "a player file has appeared that was never read; leaving it unchanged" );

				path = existing;
				using var original = SaveFileSystem.OpenRead( path );
				if ( !PlayerFile.Read( original ).CanWrite )
					throw new InvalidDataException( "the existing player was not fully understood; leaving it unchanged" );
			}

			file.Options = GameOptions.Current.ToPlayerOptions();
			using var memory = new MemoryStream();
			file.Write( memory );

			SaveFileSystem.WriteAllBytes( path, memory.ToArray() );
			Log.Info( $"Saves: wrote {path}" );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {path} could not be written - {e.Message}" );
			return false;
		}
	}

	/// <summary>
	/// The park file a player enters a theme with - 0x005accf0 (<c>docs/exe/saves.md</c>, "Entering a park"): of the
	/// files in their folder for the theme named <c>*.TPW*</c>, the one written last. That takes in the copied
	/// easymode.TPWI and every saved park, and leaves restart.INTS out. A file written at the same instant as the
	/// one held does not replace it. Null where the folder holds none: the original loads nothing then.
	/// </summary>
	/// <remarks>
	/// The original keeps the first of two equal times in the order Windows lists the folder, which is not known
	/// here: the files are taken in name order.
	/// </remarks>
	public static string? NewestPark( int slot, string name, string theme )
	{
		var folder = Path.Join( PlayerFolder( slot, name ), theme );

		try
		{
			if ( !SaveFileSystem.DirectoryExists( folder ) )
				return null;

			string? newest = null;
			var written = DateTime.MinValue;

			var parks = SaveFileSystem.GetFiles( folder )
				.Select( Path.GetFileName )
				.OfType<string>()
				.Where( file => file.Contains( ParkPattern, StringComparison.OrdinalIgnoreCase ) )
				.OrderBy( file => file, StringComparer.OrdinalIgnoreCase );

			foreach ( var file in parks )
			{
				var path = Path.Join( folder, file );
				var time = File.GetLastWriteTimeUtc( SaveFileSystem.GetAbsolutePath( path ) );

				if ( newest != null && time <= written )
					continue;

				newest = path;
				written = time;
			}

			return newest;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {folder} could not be looked through for a park - {e.Message}" );
			return null;
		}
	}

	// A saved park's extension: the Load and Save screens' search is "*" and then ".TPWS" (0x00f7b948, 0x00f7b548).
	private const string SavedParkExtension = ".TPWS";

	/// <summary>A saved park as the Load Park screen lists it: its name, where it is, and when it was last written, in local time.</summary>
	public readonly record struct SavedPark( string Name, string Path, DateTime Written );

	/// <summary>
	/// The saved parks in a player's folder for a theme, read afresh on every call - 0x005ac8f0
	/// (<c>docs/exe/saves.md</c>, "The list is the folder"): every <c>*.TPWS</c>, the quicksave and autosave.TPWS
	/// among them, each with its name less the extension and its last write time. easymode.TPWI and restart.INTS
	/// are not saved parks.
	/// </summary>
	/// <remarks>
	/// The original keeps the order Windows lists the folder in, which is not known here: the files are taken in
	/// name order, as <see cref="NewestPark"/> takes them.
	/// </remarks>
	public static IReadOnlyList<SavedPark> SavedParks( int slot, string name, string theme )
	{
		var folder = Path.Join( PlayerFolder( slot, name ), theme );

		try
		{
			if ( !SaveFileSystem.DirectoryExists( folder ) )
				return [];

			return SaveFileSystem.GetFiles( folder )
				.Select( Path.GetFileName )
				.OfType<string>()
				.Where( file => file.EndsWith( SavedParkExtension, StringComparison.OrdinalIgnoreCase ) )
				.OrderBy( file => file, StringComparer.OrdinalIgnoreCase )
				.Select( file => Path.Join( folder, file ) )
				.Select( path => new SavedPark( Path.GetFileName( path )[..^SavedParkExtension.Length], path,
					File.GetLastWriteTimeUtc( SaveFileSystem.GetAbsolutePath( path ) ).ToLocalTime() ) )
				.ToArray();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {folder} could not be looked through for saved parks - {e.Message}" );
			return [];
		}
	}

	/// <summary>
	/// Writes a saved park into a player's folder for a theme as <c>&lt;name&gt;.TPWS</c> - the path 0x005ac780
	/// builds (<c>docs/exe/saves.md</c>, "The save") - and answers its path, or null where it could not be written.
	/// A file of that name in another case is the one replaced, as on the original's file system.
	/// </summary>
	public static string? WritePark( int slot, string name, string theme, string saveName, byte[] file )
	{
		var folder = Path.Join( PlayerFolder( slot, name ), theme );
		var path = Find( saveName + SavedParkExtension, folder ) ?? Path.Join( folder, saveName + SavedParkExtension );

		try
		{
			SaveFileSystem.WriteAllBytes( path, file );
			return path;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {path} could not be written - {e.Message}" );
			return null;
		}
	}

	/// <summary>Whether a player's folder holds a gms.dat at all, readable or not.</summary>
	public static bool HasPlayerFile( int slot, string name ) => Find( PlayerFileName, PlayerFolder( slot, name ) ) != null;

	/// <summary>
	/// Deletes a player's folder and everything in it - 0x005c5250. Like the original, it passes over any name
	/// starting with a dot, so a folder holding one is left behind. A link is removed rather than followed.
	/// </summary>
	public static void DeletePlayer( int slot, string name )
	{
		var folder = SaveFileSystem.GetAbsolutePath( PlayerFolder( slot, name ) );

		try
		{
			if ( Directory.Exists( folder ) )
				DeleteFolder( folder );

			Log.Info( $"Saves: deleted the player '{name}' in slot {slot + 1}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the player '{name}' could not be deleted - {e.Message}" );
		}
	}

	/// <summary>save\users\ and the slot's number, 1 to 4, run straight into the name - 0x005c7de0.</summary>
	private static string PlayerFolder( int slot, string name ) => Path.Join( UsersFolder, $"{slot + 1}{name}" );

	private static void MakeThemeFolders( string folder )
	{
		foreach ( var theme in Themes )
			Directory.CreateDirectory( SaveFileSystem.GetAbsolutePath( Path.Join( folder, theme ) ) );
	}

	private static void CopyEasyModeParks( string folder )
	{
		foreach ( var theme in Themes )
		{
			var source = FileSystem.GetFiles( $"levels/{theme}" )
				.FirstOrDefault( file => string.Equals( Path.GetFileName( file ), EasyModeName, StringComparison.OrdinalIgnoreCase ) );

			if ( source != null )
				SaveFileSystem.WriteAllBytes( Path.Join( folder, theme, EasyModeName ), FileSystem.ReadAllBytes( source ) );
		}
	}

	private static void DeleteFolder( string folder )
	{
		foreach ( var file in Directory.GetFiles( folder ) )
		{
			if ( !Path.GetFileName( file ).StartsWith( '.' ) )
				File.Delete( file );
		}

		foreach ( var directory in Directory.GetDirectories( folder ) )
		{
			if ( Path.GetFileName( directory ).StartsWith( '.' ) )
				continue;

			if ( new DirectoryInfo( directory ).LinkTarget != null )
				Directory.Delete( directory );
			else
				DeleteFolder( directory );
		}

		if ( !Directory.EnumerateFileSystemEntries( folder ).Any() )
			Directory.Delete( folder );
	}

	private static string[] FindThemes()
	{
		try
		{
			return FileSystem.GetDirectories( "levels" )
				.Select( Path.GetFileName )
				.OfType<string>()
				.Where( name => FileSystem.FileExists( $"levels/{name}/global.sam" ) )
				.OrderBy( name => name, StringComparer.Ordinal )
				.ToArray();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the themes could not be listed - {e.Message}" );
			return [];
		}
	}

	/// <summary>The name a file in the save folder goes by, whatever its case, or null if there is none.</summary>
	private static string? Find( string name, string directory = "" )
	{
		if ( !SaveFileSystem.DirectoryExists( directory ) )
			return null;

		var match = SaveFileSystem.GetFiles( directory )
			.Select( Path.GetFileName )
			.FirstOrDefault( file => string.Equals( file, name, StringComparison.OrdinalIgnoreCase ) );

		return match == null ? null : Path.Join( directory, match );
	}
}
