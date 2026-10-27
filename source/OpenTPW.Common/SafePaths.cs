namespace OpenTPW;

/// <summary>
/// Cuts what would say whose computer this is out of a line of the console's log, so a tester can share
/// <c>console.log</c> as it is: the game's folder becomes <c>&lt;game folder&gt;</c>, the player's home folder
/// <c>~</c>, and the account's name, wherever it stands as a whole folder name in some other path,
/// <c>&lt;user&gt;</c>. Only the file is cut; the terminal shows the paths in full. Nothing in the game prints
/// the machine's name or the account's name on its own.
///
/// <para>
/// A folder is matched only as a whole folder - followed by a separator, the end of the text or a character no
/// file name continues with - so <c>/home/al</c> does not eat the start of <c>/home/alex</c>. Each is matched
/// with either separator, and without regard to case where the file system has none (Windows, macOS).
/// </para>
/// </summary>
public sealed class SafePaths
{
	public const string GameFolderName = "<game folder>";
	public const string HomeName = "~";
	public const string UserName = "<user>";

	private readonly (string Path, string Name)[] _folders;
	private readonly string _user;
	private readonly StringComparison _comparison;

	/// <param name="folders">Each folder and what it is written as; the longest is tried first, so a game folder inside the home folder is named as the game's.</param>
	/// <param name="user">The account's name, cut wherever it is a whole folder name; empty for none.</param>
	public SafePaths( IEnumerable<(string Path, string Name)> folders, string user, bool ignoreCase )
	{
		_comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		_user = user;
		_folders = folders
			.Where( f => !string.IsNullOrWhiteSpace( f.Path ) )
			.Select( f => (f.Path.TrimEnd( '/', '\\' ), f.Name) )
			.Where( f => f.Item1.Length > 1 )
			.SelectMany( f => new[] { f, (f.Item1.Replace( '\\', '/' ), f.Name), (f.Item1.Replace( '/', '\\' ), f.Name) } )
			.Distinct()
			.OrderByDescending( f => f.Item1.Length )
			.ToArray();
	}

	/// <summary>This machine's: the game's folder, the home folder and the account's name.</summary>
	public static SafePaths ForThisMachine( string gameFolder )
	{
		var home = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
		var ignoreCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

		return new SafePaths( [(Full( gameFolder ), GameFolderName), (gameFolder, GameFolderName), (home, HomeName)], Environment.UserName, ignoreCase );
	}

	private static string Full( string path )
	{
		try
		{
			return string.IsNullOrEmpty( path ) ? path : Path.GetFullPath( path );
		}
		catch ( Exception e ) when ( e is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException )
		{
			return path;
		}
	}

	/// <summary>The text with every folder named and the account's name cut.</summary>
	public string Scrub( string text )
	{
		if ( string.IsNullOrEmpty( text ) )
			return text;

		foreach ( var (path, name) in _folders )
			text = Replace( text, path, name, whole: false );

		return _user.Length == 0 ? text : Replace( text, _user, UserName, whole: true );
	}

	/// <summary>
	/// Every place <paramref name="find"/> stands as a folder: never followed by a character a name goes on
	/// with, and, when <paramref name="whole"/>, only straight after a separator.
	/// </summary>
	private string Replace( string text, string find, string with, bool whole )
	{
		var at = text.IndexOf( find, _comparison );

		if ( at < 0 )
			return text;

		var built = new System.Text.StringBuilder( text.Length );
		var from = 0;

		for ( ; at >= 0; at = text.IndexOf( find, from, _comparison ) )
		{
			var end = at + find.Length;
			var starts = !whole || (at > 0 && IsSeparator( text[at - 1] ));

			built.Append( text, from, at - from ).Append( starts && !GoesOn( text, end ) ? with : text.Substring( at, find.Length ) );
			from = end;
		}

		return built.Append( text, from, text.Length - from ).ToString();
	}

	private static bool IsSeparator( char c ) => c is '/' or '\\';

	/// <summary>
	/// Whether a name goes on at <paramref name="at"/>, so a match ending there is part of a longer name. A full
	/// stop goes on only before a letter or digit (<c>alex.old</c>); before a space or the end it ends a sentence.
	/// </summary>
	private static bool GoesOn( string text, int at )
	{
		if ( at >= text.Length )
			return false;

		var c = text[at];

		if ( c == '.' )
			return at + 1 < text.Length && char.IsLetterOrDigit( text[at + 1] );

		return char.IsLetterOrDigit( c ) || c is '_' or '-' or '~' or '$';
	}
}
