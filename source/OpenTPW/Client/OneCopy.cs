using System.Security.Cryptography;
using System.Text;

namespace OpenTPW;

/// <summary>
/// One copy of the game at a time on one game folder, as the original lets one copy run (Q995).
///
/// <para>
/// The original's <c>WinMain_Main</c> calls 0x0045f640 first (0x0045a97f), which checks the installer's registry key and
/// then makes a named mutex with <c>CreateMutexA</c>, answering 0 when <c>GetLastError</c> says it was there already, and
/// the game then leaves (<c>docs/exe/boot.md</c>, "WinMain"). The original's name is <c>TPWMutex</c> (the static string at
/// 0x007a2500, built from 0x007477f0), one name per Windows session. Here the name is OpenTPW's own, keyed by the game
/// folder: two copies of the game in two folders each have their own save folder and log, and both run (a harness runs
/// lanes side by side that way).
/// </para>
/// <para>
/// It is taken before <see cref="ConsoleLog.Open"/>, so a refused copy never rotates the running copy's console.log;
/// it keeps no log file at all. The name has <c>Global\</c> ahead of it: on Linux and macOS a plain name is kept for one
/// terminal session, and two terminals would each start a copy. <b>A deviation on Windows</b>: there <c>Global\</c> is
/// one name for every session, so a copy on the same folder in another session (the same user through Remote Desktop)
/// is refused where the original's per-session name lets it run. A mutex that cannot be made or opened (another user's,
/// a name the system refuses) is logged and the game runs, as the original's runs on a null handle (0x0045f673): a lock
/// that cannot be told is not a reason to refuse.
/// </para>
/// <para>
/// <b>Not covered</b>: two game folders whose own <c>save\</c> cannot be written both save in the player's one data
/// folder (<see cref="SaveHome.PlayersFolder"/>) and log there (<see cref="ConsoleLog.Open"/>), so two copies on two
/// such folders take two locks and share one save folder and one log.
/// </para>
/// </summary>
internal static class OneCopy
{
	/// <summary>Ahead of the folder's hash in the mutex's name.</summary>
	public const string Prefix = @"Global\OpenTPW.OneCopy.";

	/// <summary>The running copy's mutexes, held until the process ends; never closed, so never collected.</summary>
	private static IDisposable? _held;

	/// <summary>One copy's mutexes, let go of together.</summary>
	private sealed class Held( Mutex[] mutexes ) : IDisposable
	{
		public void Dispose()
		{
			foreach ( var mutex in mutexes )
				mutex.Dispose();
		}
	}

	/// <summary>
	/// The mutex's name for <paramref name="gameFolder"/>: one name for every spelling of one folder (relative or full, with
	/// or without a closing separator, through a symbolic link or a junction anywhere along it, and in any case where the
	/// system's names ignore it), another for another folder.
	/// </summary>
	public static string Name( string gameFolder )
		=> Name( gameFolder, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() );

	/// <summary><see cref="Name(string)"/> with whether case counts given, for a test of both systems' rules on one.</summary>
	internal static string Name( string gameFolder, bool ignoreCase )
		=> Name( gameFolder, ignoreCase, followLinks: true );

	/// <summary>
	/// The name a build before Q1174 took for <paramref name="gameFolder"/>: as spelled, no link followed. <see cref="Open"/>
	/// takes it beside <see cref="Name(string)"/>, so a copy of such a build still running on a folder with a link along its
	/// path refuses this one and is refused by it; one spelling is one folder, so it refuses nothing more.
	/// </summary>
	internal static string SpelledName( string gameFolder )
		=> Name( gameFolder, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), followLinks: false );

	private static string Name( string gameFolder, bool ignoreCase, bool followLinks )
	{
		var full = Path.GetFullPath( gameFolder.Trim() );
		var key = Path.TrimEndingDirectorySeparator( followLinks ? WithoutLinks( full ) : full );

		if ( ignoreCase )
			key = key.ToUpperInvariant();

		// A path cannot be a mutex's name (a separator is refused off Windows, and it may be too long): its hash is.
		var hash = SHA256.HashData( Encoding.UTF8.GetBytes( key ) );

		return Prefix + Convert.ToHexString( hash, 0, 16 );
	}

	/// <summary>
	/// Links followed in one path before it is taken as it stands: a guard. A loop of links does not exist to
	/// <see cref="DirectoryInfo"/> (its stat fails), so the walk ends at it before the cap.
	/// </summary>
	private const int MaxLinks = 40;

	/// <summary>
	/// <paramref name="fullPath"/> with every link along it followed, from the root down: a symbolic link or a junction at
	/// any component, not only the last (which is all <see cref="Directory.ResolveLinkTarget"/> looks at), and a link whose
	/// target holds links of its own. A component that does not exist, or cannot be read, ends the walk and the rest is kept
	/// as spelled; a loop of links is such a component, so the walk ends without reaching <see cref="MaxLinks"/>. A subst drive is not a link and is not followed (Q1248).
	/// </summary>
	internal static string WithoutLinks( string fullPath )
	{
		var path = fullPath;

		for ( var hops = 0; hops < MaxLinks; hops++ )
		{
			var root = Path.GetPathRoot( path ) ?? string.Empty;
			var parts = path[root.Length..].Split( [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries );
			var walked = root;
			string? followed = null;

			for ( var i = 0; i < parts.Length; i++ )
			{
				walked = Path.Join( walked, parts[i] );

				string? target;

				try
				{
					var info = new DirectoryInfo( walked );

					if ( !info.Exists )
						break;

					target = info.LinkTarget;
				}
				catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException or System.Security.SecurityException )
				{
					break;
				}

				if ( target is null )
					continue;

				// A relative target is relative to the folder the link sits in; what is left of the path follows it.
				var into = Path.GetFullPath( target, Path.GetDirectoryName( walked ) ?? root );
				followed = Path.GetFullPath( Path.Join( [into, .. parts[( i + 1 )..]] ) );
				break;
			}

			if ( followed is null )
				return path;

			path = followed;
		}

		return path;
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
	/// Makes the mutexes for <paramref name="gameFolder"/>, its <see cref="Name(string)"/> and, where a link makes it
	/// another, its <see cref="SpelledName"/>. Answers them where this is the first copy; null with a null
	/// <paramref name="unknown"/> where another copy made either first (the handles are closed, as 0x0045f699 closes it);
	/// null with <paramref name="unknown"/> saying why where the system would not say either way.
	/// </summary>
	internal static IDisposable? Open( string gameFolder, out string? unknown )
	{
		unknown = null;
		var made = new List<Mutex>();

		try
		{
			foreach ( var name in new[] { Name( gameFolder ), SpelledName( gameFolder ) }.Distinct() )
			{
				var mutex = new Mutex( false, name, out var createdNew );

				made.Add( mutex );

				if ( !createdNew )
				{
					made.ForEach( held => held.Dispose() );
					return null;
				}
			}

			return new Held( [.. made] );
		}
		catch ( Exception e ) when ( e is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException or ArgumentException )
		{
			made.ForEach( held => held.Dispose() );
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
