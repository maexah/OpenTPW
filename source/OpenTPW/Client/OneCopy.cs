using System.Security.Cryptography;
using System.Text;

namespace OpenTPW;

/// <summary>
/// One copy of the game at a time on one game folder, as the original lets one copy run (Q995).
///
/// <para>
/// The original's <c>WinMain_Main</c> calls 0x0045f640 first (0x0045a97f), which makes a named mutex with
/// <c>CreateMutexA</c> and answers 0 when <c>GetLastError</c> says it was there already, and the game then leaves
/// (<c>docs/exe/boot.md</c>, "WinMain"). Here the mutex is keyed by the game folder, never machine-wide: two copies of
/// the game in two folders each have their own save folder and log, and both run (a harness runs lanes side by side
/// that way). The original's name is not known (0x7a2508 is outside the file's bytes), so the name is OpenTPW's own.
/// </para>
/// <para>
/// It is taken before <see cref="ConsoleLog.Open"/>, so a refused copy never rotates the running copy's console.log;
/// it keeps no log file at all. The name has <c>Global\</c> ahead of it: on Linux and macOS a plain name is kept for one
/// terminal session, and two terminals would each start a copy. A mutex that cannot be made or opened (another user's,
/// a name the system refuses) is logged and the game runs: a lock that cannot be told is not a reason to refuse.
/// </para>
/// </summary>
internal static class OneCopy
{
	/// <summary>Ahead of the folder's hash in the mutex's name.</summary>
	public const string Prefix = @"Global\OpenTPW.OneCopy.";

	/// <summary>The running copy's mutex, held until the process ends; never closed, so never collected.</summary>
	private static Mutex? _held;

	/// <summary>
	/// The mutex's name for <paramref name="gameFolder"/>: one name for every spelling of one folder (relative or full, with
	/// or without a closing separator, and in any case where the system's names ignore it), another for another folder.
	/// </summary>
	public static string Name( string gameFolder )
		=> Name( gameFolder, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() );

	/// <summary><see cref="Name(string)"/> with whether case counts given, for a test of both systems' rules on one.</summary>
	internal static string Name( string gameFolder, bool ignoreCase )
	{
		var key = Path.TrimEndingDirectorySeparator( Path.GetFullPath( gameFolder.Trim() ) );

		if ( ignoreCase )
			key = key.ToUpperInvariant();

		// A path cannot be a mutex's name (a separator is refused off Windows, and it may be too long): its hash is.
		var hash = SHA256.HashData( Encoding.UTF8.GetBytes( key ) );

		return Prefix + Convert.ToHexString( hash, 0, 16 );
	}

	/// <summary>
	/// Takes the lock on <paramref name="gameFolder"/> and keeps it for the rest of the run, answering false when another copy
	/// holds it already. Called once a start, by <see cref="Game.Run"/>.
	/// </summary>
	public static bool Take( string gameFolder )
	{
		var mutex = Open( gameFolder, out var reason );

		if ( mutex is null )
		{
			if ( reason is not null )
				Log.Warning( reason );

			return reason is not null;
		}

		_held = mutex;
		Log.Info( $"One copy: this copy holds {Name( gameFolder )} for {gameFolder}" );

		return true;
	}

	/// <summary>
	/// Makes the mutex for <paramref name="gameFolder"/>. Answers it where this is the first copy; null with a null
	/// <paramref name="unknown"/> where another copy made it first (the handle is closed, as 0x0045f699 closes it); null with
	/// <paramref name="unknown"/> saying why where the system would not say either way.
	/// </summary>
	internal static Mutex? Open( string gameFolder, out string? unknown )
	{
		unknown = null;
		var name = Name( gameFolder );

		try
		{
			var mutex = new Mutex( false, name, out var createdNew );

			if ( createdNew )
				return mutex;

			mutex.Dispose();
			return null;
		}
		catch ( Exception e ) when ( e is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException or ArgumentException )
		{
			unknown = $"One copy: whether another copy runs on {gameFolder} cannot be told ({e.GetType().Name}: {e.Message}); this one runs";
			return null;
		}
	}

	/// <summary>Lets go of the lock this run holds: a test that stands in for a run's end.</summary>
	internal static void ReleaseForTest()
	{
		_held?.Dispose();
		_held = null;
	}

	/// <summary>
	/// What the box says when another copy holds the folder, and the log with "One copy: " ahead of each line. The original quits without a word; a copy that
	/// vanishes when started looks broken, so the box is a deviation (Q995, in the manner of Q978's).
	/// </summary>
	public static IEnumerable<string> Refusal( string gameFolder ) =>
	[
		$"Another copy of OpenTPW is already running on the game in {gameFolder}.",
		"Two copies on one game would share its save folder and its log, so this one stops; close the other first, or start this one on another copy of the game.",
	];
}
