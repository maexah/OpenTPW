namespace OpenTPW;

/// <summary>
/// Where this run keeps its saves: the game's own <c>save\</c> when that can be written, otherwise <c>save</c> in the
/// player's data folder (<see cref="UserFolders.Data"/>: <c>%AppData%\OpenTPW\save</c> on Windows).
///
/// <para>
/// <b>A deviation</b> (Q984, <c>AGENTS.md</c> rule 11). The original keeps saves in <c>save\</c> beside its data and
/// makes the folder itself on a first run. A stock Sim Theme Park install under Program Files is not writable, the
/// original's exe having no manifest, so Windows redirects its writes to the VirtualStore, and OpenTPW's is
/// <c>asInvoker</c> and is not redirected. Without a way out, the first start of a fresh install ended in Q978's box.
/// The test is a file really made and let go (<see cref="Unwritable"/>), never a guess from the path. A folder that
/// can be written is kept, as the original does, so a park saved by one game is seen by the other.
/// </para>
/// <para>
/// Q902 (Alexah's, open): a <c>save\</c> that holds players but cannot be written is read by nothing, and nothing is
/// copied out of it. The game says so in a short box (<see cref="Notice"/>).
/// </para>
/// <para>
/// The other way (Q908, Alexah's 2026-11-02): a game folder that has become writable is kept, and the players left in the
/// player's folder are read by nothing and nothing is copied; the game says so in a short box (<see cref="KeptNotice"/>).
/// Q922 (Alexah's words, 2026-11-02): neither box holds a path, which a player cannot use and a long one overwhelms; both
/// folders go to the log at every start. Each box is shown once and remembered (<see cref="Told"/>), in opentpw.cfg, or at every start where opentpw.cfg cannot be written.
/// </para>
/// </summary>
internal static class SaveHome
{
	internal const string Name = "save";

	/// <summary>The names the two notices are remembered under in opentpw.cfg.</summary>
	internal const string KeptName = "kept";
	internal const string MovedName = "moved";

	private const string UsersName = "users";

	/// <param name="Folder">The folder to map.</param>
	/// <param name="Fallback">Whether it is the player's folder, not the game's.</param>
	/// <param name="Notice">What to tell the player, or null where there is nothing to tell.</param>
	/// <param name="NoticeName">The name the notice is remembered under in opentpw.cfg once its box has opened (<see cref="Told"/>); null with no notice.</param>
	internal readonly record struct Pick( string Folder, bool Fallback, string? Notice, string? NoticeName = null );

	/// <summary>The fallback: <c>save</c> in the player's data folder.</summary>
	internal static string PlayersFolder() => Path.Join( UserFolders.Data(), Name );

	/// <summary>
	/// The game's folder if it can be written, else the player's. Where neither can be made the start cannot go on, and
	/// the exception says both reasons (Q978's box shows it). A player's folder that exists but cannot be written is
	/// mapped all the same, and Q982's boxes tell what is lost.
	/// </summary>
	internal static Pick Choose( string gameSave, string playersSave )
	{
		if ( Unwritable( gameSave ) is not { } why )
		{
			Log.Info( $"Saves: the game's own save folder, {gameSave}" );

			// Alexah's answer (Q908): say so, copy nothing and read no second folder.
			if ( !PathsMatch( gameSave, playersSave ) && HoldsPlayers( playersSave ) )
			{
				Log.Warning( $"Saves: {playersSave} holds players, and the game's own folder {gameSave} is kept, so they are read by nothing and nothing is copied (Q908)" );
				return new Pick( gameSave, false, KeptNotice(), KeptName );
			}

			return new Pick( gameSave, false, null );
		}

		var holds = HoldsPlayers( gameSave );
		var playersWhy = Unwritable( playersSave );

		if ( playersWhy != null && !Directory.Exists( playersSave ) )
			throw new IOException( $"Saves cannot go anywhere: {gameSave} cannot be written ({why}), and {playersSave} cannot be made ({playersWhy})" );

		Log.Warning( $"Saves: {gameSave} cannot be written ({why}), so saves go in {playersSave}"
			+ (holds ? "; the players already in the game's folder are read by nothing and nothing is copied (Q902)" : "") );

		if ( playersWhy != null )
			Log.Warning( $"Saves: {playersSave} cannot be written either ({playersWhy})" );

		return new Pick( playersSave, true, holds ? Notice() : null, holds ? MovedName : null );
	}

	/// <summary>
	/// What the player is told when the move leaves players behind, in the message box's capitals-then-sentence form. No path:
	/// the log names both folders at every start (<see cref="Choose"/>).
	/// </summary>
	internal static string Notice()
		=> "YOUR GAME SAVES IN A NEW FOLDER\n\nPlayers saved before are safe, but won't show here.";

	/// <summary>What the player is told when the game's folder is kept and the player's folder still holds players, in the same form.</summary>
	internal static string KeptNotice()
		=> "CAN'T SEE YOUR PLAYERS?\n\nThey are safe. Nothing was deleted. If you changed how the game runs, changing it back will show them again.";

	/// <summary>
	/// The notice if it is not in <paramref name="told"/> (opentpw.cfg's <c>told</c> lines, <see cref="GameOptions.NoticesTold"/>);
	/// null where the pick has none or it was told. A check only: the notice is counted when its box is open (<see cref="Told"/>), so
	/// a start that fails or a window closed before the box does not use it up.
	/// </summary>
	internal static string? TellOnce( Pick pick, ISet<string> told )
		=> pick.Notice != null && pick.NoticeName is { } name && !told.Contains( name ) ? pick.Notice : null;

	/// <summary>
	/// The start's reading of opentpw.cfg (<see cref="SaveFolder.LoadDisplay"/>), then the pick's notice handed to
	/// <see cref="SaveTrouble"/> if it was not told before. The read comes first, as the told lines are what the check looks at.
	/// </summary>
	internal static void LoadDisplayAndTell( Pick pick )
	{
		SaveFolder.LoadDisplay();

		if ( TellOnce( pick, GameOptions.Current.NoticesTold ) is { } notice )
			SaveTrouble.Tell( notice, pick.NoticeName );
		else if ( pick.Notice != null )
			Log.Info( $"Saves: the notice {pick.NoticeName} was told before, so no box (delete its \"told\" line in opentpw.cfg to see it again)" );
	}

	/// <summary>
	/// The notice's box is open: counts it told and writes opentpw.cfg at once, so the next start has the line (where that
	/// file cannot be written, the box comes at every start).
	/// </summary>
	internal static void Told( string name )
	{
		if ( GameOptions.Current.NoticesTold.Add( name ) )
			SaveFolder.SaveDisplay( tell: false );
	}

	/// <summary>
	/// Whether two spellings name one folder: the same full path, trailing separators aside, in the system's case rule. A link to a
	/// folder is another path to this (<see cref="Path.GetFullPath(string)"/> follows no link), so a <c>save</c> linked to the
	/// player's folder is taken for a second folder and its players are called unread though the game reads them.
	/// </summary>
	private static bool PathsMatch( string a, string b )
		=> string.Equals( Path.TrimEndingDirectorySeparator( Path.GetFullPath( a ) ), Path.TrimEndingDirectorySeparator( Path.GetFullPath( b ) ),
			OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal );

	/// <summary>
	/// Why a file cannot be made in the folder (made first if it is not there), or null where one can: one is made and let
	/// go at once, leaving the folder as it was. Its name is its own and it must be new (<see cref="FileMode.CreateNew"/>), so a
	/// file of the player's, or another copy's probe at the same moment, is neither emptied nor taken for a reason to move the saves.
	/// </summary>
	internal static string? Unwritable( string folder )
	{
		try
		{
			Directory.CreateDirectory( folder );

			using ( new FileStream( Path.Join( folder, $".write-test-{Guid.NewGuid():N}" ), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose ) )
				return null;
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException )
		{
			return e.Message;
		}
	}

	/// <summary>Whether the folder has a player in it: a folder under <c>users</c>, in whatever case the disc spells it.</summary>
	internal static bool HoldsPlayers( string folder )
	{
		try
		{
			return Directory.EnumerateDirectories( folder )
				.Where( entry => string.Equals( Path.GetFileName( entry ), UsersName, StringComparison.OrdinalIgnoreCase ) )
				.Any( users => Directory.EnumerateDirectories( users ).Any() );
		}
		catch ( DirectoryNotFoundException )
		{
			return false;
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException )
		{
			Log.Warning( $"Saves: {folder} could not be read for players, so it is taken to hold none - {e.Message}" );
			return false;
		}
	}
}
