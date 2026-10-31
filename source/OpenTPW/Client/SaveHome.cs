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
/// copied out of it. The game says so once each start (<see cref="SaveTrouble.Tell"/>; nothing is kept between starts).
/// </para>
/// </summary>
internal static class SaveHome
{
	internal const string Name = "save";

	private const string UsersName = "users";

	/// <param name="Folder">The folder to map.</param>
	/// <param name="Fallback">Whether it is the player's folder, not the game's.</param>
	/// <param name="Notice">What to tell the player once, or null where nothing is lost to them by the move.</param>
	internal readonly record struct Pick( string Folder, bool Fallback, string? Notice );

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

		return new Pick( playersSave, true, holds ? Notice( playersSave ) : null );
	}

	/// <summary>The one line the player is told when the move leaves players behind, in the message box's capitals-then-sentence form.</summary>
	internal static string Notice( string folder )
		=> $"SAVES GO IN\n\n{folder}\nPlayers in the game's own folder are not copied.";

	/// <summary>
	/// Why a file cannot be made in the folder (made first if it is not there), or null where one can: one is made and let
	/// go at once, leaving the folder as it was.
	/// </summary>
	internal static string? Unwritable( string folder )
	{
		try
		{
			Directory.CreateDirectory( folder );

			using ( new FileStream( Path.Join( folder, ".write-test" ), FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose ) )
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
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException )
		{
			return false;
		}
	}
}
