namespace OpenTPW;

/// <summary>
/// Where Theme Park World is installed, and what a file inside it is really called.
///
/// <para>
/// The original addressed its own files relative to itself - ".\data\2dmap\gsprite.tga" - because it ran
/// from the folder it was installed into. OpenTPW is meant to be used the same way: copy the disc somewhere
/// that can be written to, put this build in with the game's files, and start it. <see cref="Find"/> takes
/// the first of these that holds the game: --game on the command line, OPENTPW_GAME_PATH in the environment,
/// the GamePath setting if it has been set away from the value it ships with, the folder this build sits in
/// and the folders above it, the working directory and the folders above it, and last the place the original
/// installs itself on Windows.
/// </para>
/// <para>
/// A folder holds the game when it has a data folder with the game's levels inside it. Names are matched
/// without regard to case throughout: the disc spells its top folder "Data" while an installed copy spells it
/// "data", and on anything but Windows only one of those opens.
/// </para>
/// <para>
/// The original also kept an installer key, software\Bullfrog Productions Ltd (FUN_005f8ae0, 0x005f8ae0), but
/// there is no step here that reads it: both of that function's callers name their subkey and value through
/// pointers filled in at run time, and the one that reads clearly reads a language rather than a path, so
/// there is nothing to show the key holds a folder at all.
/// </para>
/// </summary>
public static class GameDir
{
	/// <summary>Names the game's folder for this run: "--game &lt;folder&gt;" or "--game=&lt;folder&gt;".</summary>
	private const string PathArgument = "--game";

	/// <summary>Names the game's folder for every run on this machine, for a build started from elsewhere.</summary>
	private const string PathVariable = "OPENTPW_GAME_PATH";

	private const string SettingSource = "the GamePath setting";
	private const string BuildSource = "the folder this build is in";
	private const string WorkingSource = "the working directory";
	private const string DefaultSource = "where the original installs itself";

	private const string DataName = "data";
	private const string SaveName = "save";
	private const string LevelsName = "levels";
	private const string LongName = "Challenges.sam";

	/// <summary>
	/// How far above the build and the working directory the game is looked for. Five is the depth of a build
	/// made inside the game's own folder - &lt;game&gt;\source\OpenTPW\bin\Debug\net10.0 - and there is nothing
	/// worth walking into above that: past the folder holding the build, the folders above it are the
	/// player's own.
	/// </summary>
	private const int ParentLevels = 5;

	/// <summary>
	/// Names from the disc's plain ISO 9660 tree, which is in capitals and cut to 8.3. A copy taken from that
	/// tree rather than from the disc's Joliet one has these where the game asks for Challenges.sam,
	/// onlineoverride.sam and _Resolution.sam, and nothing in the game opens. Read off the disc itself, from
	/// both of its directory trees.
	/// </summary>
	private static readonly string[] ShortNames = ["CHALLE~0.SAM", "ONLINE~0.SAM", "_RESOL~0.SAM"];

	/// <summary>The installed game's folder - where TP.exe lives. Empty until <see cref="Find"/> has found it.</summary>
	public static string Root { get; private set; } = string.Empty;

	/// <summary>The game's data folder, spelled as it is on disk: "data" installed, "Data" on the disc.</summary>
	public static string Data { get; private set; } = string.Empty;

	/// <summary>
	/// Finds the installed game and sets <see cref="Root"/> and <see cref="Data"/>, answering whether there is
	/// anything to start. Nothing is thrown when there is not: it says where it looked and what would put it
	/// right, because a player who cannot start the game needs the list rather than a stack trace.
	/// </summary>
	public static bool Find( string[] args )
	{
		var tried = new List<(string Where, string Path)>();
		var shortNamed = new List<string>();

		foreach ( var (where, path) in Candidates( args ) )
		{
			// A folder named for this run is never fallen through, whether it is missing or simply wrong.
			// Carrying on down the list would start the game somewhere the player did not ask for, or bury
			// the mistake among every other place that was looked.
			var named = where is PathArgument or PathVariable;
			var folder = FullPath( path );

			if ( folder == null )
			{
				if ( named )
				{
					Log.Error( $"Game files: there is no folder at {path}, given by {where}." );
					return false;
				}

				tried.Add( (where, path) );
				continue;
			}

			if ( tried.Any( entry => entry.Path == folder ) )
				continue;

			tried.Add( (where, folder) );

			if ( FindEntry( folder, DataName ) is not { } data || FindEntry( data, LevelsName ) == null )
			{
				if ( named )
				{
					Log.Error( $"Game files: {folder}, given by {where}, does not hold Theme Park World." );
					Log.Error( $"Game files: a folder counts when it holds a {DataName} folder with the game's {LevelsName} in it - the folder TP.exe was installed in." );
					return false;
				}

				continue;
			}

			if ( HasShortNames( data ) )
			{
				// Named for this run, and holding a copy nothing can be opened out of. Going on down the list
				// would quietly start whatever other copy is lying about instead of the one that was asked for.
				if ( named )
				{
					ExplainShortNames( [folder] );
					return false;
				}

				shortNamed.Add( folder );
				continue;
			}

			Root = folder;
			Data = data;

			Log.Info( $"Game files: Theme Park World in {Root} ({where})" );
			return true;
		}

		Explain( tried, shortNamed );
		return false;
	}

	/// <summary>
	/// The folder saves go in. The original keeps them in save\ beside its data and so does OpenTPW, so that a
	/// park saved by one is seen by the other. It is named here but not made: whoever maps it makes it, since
	/// a fresh copy of the game has no save folder until something writes one.
	/// </summary>
	public static string FindSaveFolder() => FindEntry( Root, SaveName ) ?? Path.Join( Root, SaveName );

	/// <summary>
	/// An absolute path to something inside the installed game, with every name resolved to the case it really
	/// has on disk. Paths in the code are written the way the original wrote them - "\data\ui\cursors" - while
	/// the disc spells its top folder "Data" and an installed copy spells it "data". A name that is not there
	/// is joined on as it was asked for, so whatever fails next reports the name the caller wanted.
	/// </summary>
	public static string GetPath( string path )
	{
		var parts = path.Split( '\\', '/' ).Where( part => part.Length > 0 ).ToArray();
		var resolved = Root;

		for ( int i = 0; i < parts.Length; ++i )
		{
			if ( FindEntry( resolved, parts[i] ) is not { } entry )
				return Path.Join( resolved, string.Join( Path.DirectorySeparatorChar, parts[i..] ) );

			resolved = entry;
		}

		return resolved;
	}

	/// <summary>Everywhere the game might be, in the order they are believed.</summary>
	private static List<(string Where, string Path)> Candidates( string[] args )
	{
		var candidates = new List<(string, string)>();

		if ( Argument( args ) is { } argument )
			candidates.Add( (PathArgument, argument) );

		if ( Environment.GetEnvironmentVariable( PathVariable ) is { Length: > 0 } variable )
			candidates.Add( (PathVariable, variable) );

		var (setting, byDefault) = Setting();

		// Only once it has been set. What it ships with is where the original installs itself on Windows,
		// which is a guess like any other rather than something the player asked for, so that value is tried
		// last instead - and on anything but Windows it is a path that cannot exist.
		if ( !string.IsNullOrWhiteSpace( setting ) && setting != byDefault )
			candidates.Add( (SettingSource, setting) );

		// Where this build is: put in with the game's files in place of TP.exe, or built into a folder
		// inside the game's own.
		foreach ( var folder in WithParents( AppContext.BaseDirectory ) )
			candidates.Add( (BuildSource, folder) );

		foreach ( var folder in WithParents( Directory.GetCurrentDirectory() ) )
			candidates.Add( (WorkingSource, folder) );

		if ( !string.IsNullOrWhiteSpace( byDefault ) )
			candidates.Add( (DefaultSource, byDefault) );

		return candidates;
	}

	/// <summary>
	/// The GamePath setting as it stands, and the value it ships with. A settings file that will not read is
	/// not a reason to stop - the game is very likely findable without it - so it is reported and passed over.
	/// </summary>
	private static (string? Setting, string? Default) Setting()
	{
		try
		{
			return (Settings.Default.GamePath, Settings.Default.Properties["GamePath"]?.DefaultValue as string);
		}
		catch ( Exception e )
		{
			Log.Warning( $"Game files: the GamePath setting could not be read, so it is passed over - {e.Message}" );
			return (null, null);
		}
	}

	/// <summary>The folder named by --game, or null if it was not named.</summary>
	private static string? Argument( string[] args )
	{
		for ( int i = 0; i < args.Length; ++i )
		{
			if ( args[i].StartsWith( $"{PathArgument}=", StringComparison.OrdinalIgnoreCase ) )
				return args[i][(PathArgument.Length + 1)..];

			if ( string.Equals( args[i], PathArgument, StringComparison.OrdinalIgnoreCase ) && i + 1 < args.Length )
				return args[i + 1];
		}

		return null;
	}

	/// <summary>A folder and the folders above it, as far as <see cref="ParentLevels"/>.</summary>
	private static List<string> WithParents( string path )
	{
		var folders = new List<string>();
		var folder = new DirectoryInfo( path );

		for ( int level = 0; level <= ParentLevels && folder != null; ++level )
		{
			folders.Add( folder.FullName );
			folder = folder.Parent;
		}

		return folders;
	}

	/// <summary>
	/// The folder as the filesystem here knows it, or null if there is nothing there. Nothing is made absolute
	/// before it is known to exist: a Windows path on a machine that is not Windows is not rooted here, and
	/// making it absolute would hang the working directory on the front of it and report a folder nobody named.
	/// </summary>
	private static string? FullPath( string path )
	{
		try
		{
			var trimmed = path.Trim();

			if ( trimmed.Length == 0 || !Directory.Exists( trimmed ) )
				return null;

			return Path.TrimEndingDirectorySeparator( Path.GetFullPath( trimmed ) );
		}
		catch ( Exception )
		{
			return null;
		}
	}

	/// <summary>
	/// The full path of the entry called <paramref name="name"/> inside <paramref name="folder"/>, whatever
	/// case it is written in there, or null if there is none.
	/// </summary>
	private static string? FindEntry( string folder, string name )
	{
		var direct = Path.Join( folder, name );

		if ( Directory.Exists( direct ) || File.Exists( direct ) )
			return direct;

		try
		{
			return Directory.EnumerateFileSystemEntries( folder )
				.FirstOrDefault( entry => string.Equals( Path.GetFileName( entry ), name, StringComparison.OrdinalIgnoreCase ) );
		}
		catch ( Exception )
		{
			// Not a folder, or not one this player may read. Nothing here either way.
			return null;
		}
	}

	/// <summary>Whether a data folder came from the disc's short-name tree rather than its long-name one.</summary>
	private static bool HasShortNames( string data )
		=> FindEntry( data, LongName ) == null
			&& ShortNames.Any( name => FindEntry( data, name ) != null );

	/// <summary>Says what was looked at, and what would make the game findable, once nothing has been found.</summary>
	private static void Explain( List<(string Where, string Path)> tried, List<string> shortNamed )
	{
		// A copy of the disc under its short names is the answer in itself, and a list of everywhere else
		// would bury it.
		if ( shortNamed.Count > 0 )
		{
			ExplainShortNames( shortNamed );
			return;
		}

		Log.Error( "Game files: Theme Park World was not found." );
		Log.Error( $"Game files: a folder counts when it holds a {DataName} folder with the game's {LevelsName} in it - the folder TP.exe was installed in." );

		// One line for each place looked, rather than one for every folder walked through: the folders above
		// the working directory are the player's own, and naming them all would bury the answer.
		foreach ( var where in tried.Select( entry => entry.Where ).Distinct() )
		{
			var first = tried.First( entry => entry.Where == where ).Path;
			var above = where is BuildSource or WorkingSource ? ", and the folders above it" : "";

			Log.Error( $"Game files: looked in {first}{above} ({where})" );
		}

		Log.Error( $"Game files: put this build in with the game's files, or start it with {PathArgument} <folder>, or set {PathVariable} to that folder." );
	}

	/// <summary>
	/// Says that a folder holds the game copied the wrong way off the disc. Nothing in such a copy can be
	/// opened - every name the game asks for is cut to 8.3 and put into capitals - so it is worth saying on
	/// its own rather than among a list of everywhere that was looked.
	/// </summary>
	private static void ExplainShortNames( List<string> folders )
	{
		foreach ( var folder in folders )
			Log.Error( $"Game files: {folder} holds the game, but its names have been cut short - CHALLE~0.SAM where the game asks for {LongName}." );

		Log.Error( "Game files: that is the disc's plain ISO 9660 tree. Its Joliet tree holds the long names, and the long names are the ones every file in the game asks for." );
		Log.Error( "Game files: copy the disc again with its long names kept - mount it and copy from the mount, rather than unpacking the short-name tree." );
	}
}
