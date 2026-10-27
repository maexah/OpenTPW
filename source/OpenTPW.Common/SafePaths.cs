namespace OpenTPW;

/// <summary>
/// Cuts what would say whose computer this is out of a line of the console's log, so a tester can share
/// <c>console.log</c> as it is: the game's folder becomes <c>&lt;game folder&gt;</c>, the player's home folder
/// <c>~</c>, and the account's name, wherever it stands as a whole folder name in some other path,
/// <c>&lt;user&gt;</c>; and each player's name, as it was typed, <c>&lt;player N&gt;</c> by their slot. Only the
/// file is cut; the terminal shows it all in full. Nothing in the game prints the machine's name or the account's
/// name on its own.
///
/// <para>
/// A folder is matched only as a whole folder - followed by a separator, the end of the text or a character no
/// file name continues with - so <c>/home/al</c> does not eat the start of <c>/home/alex</c>. Each is matched
/// with either separator, and without regard to case where the file system has none (Windows, macOS).
/// </para>
/// <para>
/// A player's name is matched with its case, wherever no letter stands either side of it and no digit after it:
/// a digit may stand before it, because a player's folder is their slot's number run into their name
/// (<c>save/users/1Kim</c>, see <c>SaveFolder.PlayerFolder</c>). A name of one character is left: it says nothing.
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
	private (string Name, string Label)[] _players = [];
	private readonly object _playersLock = new();

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

	/// <summary>The label a player's name is written as: <c>&lt;player 1&gt;</c> for the first slot.</summary>
	public static string PlayerLabel( int slot ) => $"<player {slot + 1}>";

	/// <summary>Cuts a player's name from here on, as <see cref="PlayerLabel"/> of their slot. Safe from any thread.</summary>
	public void HidePlayer( string name, int slot )
	{
		if ( string.IsNullOrEmpty( name ) || name.Length < 2 )
			return;

		lock ( _playersLock )
		{
			// The longest first, so a name holding another player's whole is cut as its own.
			_players = _players.Where( p => p.Name != name ).Append( (Name: name, Label: PlayerLabel( slot )) )
				.OrderByDescending( p => p.Name.Length ).ToArray();
		}
	}

	/// <summary>The text with every folder named and the account's and the players' names cut.</summary>
	public string Scrub( string text )
	{
		if ( string.IsNullOrEmpty( text ) )
			return text;

		foreach ( var (path, name) in _folders )
			text = Replace( text, path, name, whole: false );

		if ( _user.Length > 0 )
			text = Replace( text, _user, UserName, whole: true );

		foreach ( var (name, label) in _players )
			text = ReplaceName( text, name, label );

		return text;
	}

	/// <summary>Every place a player's name stands with no letter either side and no digit after it, case kept.</summary>
	private static string ReplaceName( string text, string name, string label )
	{
		var at = text.IndexOf( name, StringComparison.Ordinal );

		if ( at < 0 )
			return text;

		var built = new System.Text.StringBuilder( text.Length );
		var from = 0;

		for ( ; at >= 0; at = text.IndexOf( name, from, StringComparison.Ordinal ) )
		{
			var end = at + name.Length;
			var alone = (at == 0 || !char.IsLetter( text[at - 1] )) && (end == text.Length || !char.IsLetterOrDigit( text[end] ));

			built.Append( text, from, at - from ).Append( alone ? label : name );
			from = end;
		}

		return built.Append( text, from, text.Length - from ).ToString();
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
