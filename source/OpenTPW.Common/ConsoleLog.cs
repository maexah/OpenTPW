using System.Reflection;
using System.Text;

namespace OpenTPW;

/// <summary>
/// Everything the process prints, kept in <c>console.log</c> as well as shown: stdout and stderr both, so
/// <see cref="Logger"/>'s lines, the debug console's [dbg] replies and an unhandled exception are in the file
/// the terminal showed. The launch before is kept as <c>console-prev.log</c>, so two runs of one build can be
/// compared and a bug report from another machine has the whole run. The new file is opened first, beside the old
/// one, and the old ones are moved only once it is open: a folder that cannot be written loses nothing.
///
/// <para>
/// The file goes in the game folder beside <c>save\</c>; where that cannot be written (a Program Files install)
/// it goes in the player's own data folder (<c>%AppData%\OpenTPW</c> on Windows, <c>~/.local/share/OpenTPW</c>
/// elsewhere), and the terminal says which. <c>OPENTPW_LOG_DIR</c> names a folder instead, which a harness
/// that starts the game sets to its own, so a run never rotates the player's own logs away. What is printed
/// before the folder is known is held and written once it is.
/// </para>
/// <para>
/// A write to the file that fails (a full disk, a removed drive) ends the file there, says so once on the
/// terminal, and the terminal carries on: the log never stops the game.
/// </para>
/// <para>
/// A launch that finds no game writes no file: nothing is rotated for it, and the terminal has all it said.
/// </para>
/// </summary>
public static class ConsoleLog
{
	public const string FileName = "console.log";
	public const string PreviousName = "console-prev.log";

	/// <summary>Appended to <see cref="FileName"/> for the new file while it is opened, before the old one is moved aside.</summary>
	public const string NewSuffix = ".new";
	public const string DirectoryVariable = "OPENTPW_LOG_DIR";

	private static Sink? _sink;

	/// <summary>Where the file is, once <see cref="Open"/> has made it; empty before.</summary>
	public static string Path => _sink?.Path ?? string.Empty;

	/// <summary>Starts keeping what stdout and stderr print. Called first thing in Main.</summary>
	public static void Start()
	{
		if ( _sink != null )
			return;

		_sink = new Sink();

		Console.SetOut( new Tee( Console.Out, _sink ) );
		Console.SetError( new Tee( Console.Error, _sink ) );

		// The runtime prints an unhandled exception to the native stderr, past Console.Error.
		AppDomain.CurrentDomain.UnhandledException += ( _, e ) => Console.Error.WriteLine( $"Unhandled exception: {e.ExceptionObject}" );
	}

	/// <summary>Opens the file now the game's folder is found: rotates the old one and writes what was held.</summary>
	public static void Open( string gameFolder )
	{
		if ( _sink is not { } sink )
			return;

		if ( !sink.Open( Candidates( gameFolder, Environment.GetEnvironmentVariable( DirectoryVariable ), UserFolder() ), Header() ) )
			Console.Error.WriteLine( $"[console log] no folder could be written, so this run is not kept: {sink.Failures}" );
		else if ( sink.Failures.Length > 0 )
			Console.Error.WriteLine( $"[console log] this run is kept in {sink.Path}: {sink.Failures}" );
	}

	/// <summary>The player's own data folder for the log: <c>%AppData%\OpenTPW</c> on Windows, <c>~/.local/share/OpenTPW</c> elsewhere.</summary>
	public static string UserFolder()
	{
		var root = OperatingSystem.IsWindows() ? Environment.SpecialFolder.ApplicationData : Environment.SpecialFolder.LocalApplicationData;

		return System.IO.Path.Join( Environment.GetFolderPath( root ), "OpenTPW" );
	}

	/// <summary>The folders to try, in order: the variable's alone when set, otherwise the game's and then the player's own.</summary>
	public static string[] Candidates( string gameFolder, string? variable, string userFolder )
		=> string.IsNullOrWhiteSpace( variable ) ? [gameFolder, userFolder] : [variable];

	private static string Header()
	{
		var build = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown build";

		return $"OpenTPW {build}, started {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}";
	}

	/// <summary>
	/// The file and what waits for it. Public so a test can drive it over folders of its own; the game goes
	/// through <see cref="Start"/> and <see cref="Open"/>.
	/// </summary>
	public sealed class Sink : IDisposable
	{
		private readonly object _lock = new();
		private readonly Func<string, Stream> _create;
		private StringBuilder? _held = new();
		private StreamWriter? _file;

		/// <param name="create">Opens the file at a path (the new file is opened beside console.log first); a test hands in a stream that fails.</param>
		public Sink( Func<string, Stream>? create = null )
		{
			// Shared for delete as well as read, so that the file can be renamed while it is open (Windows refuses otherwise).
			_create = create ?? ( path => new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete ) );
		}

		public string Path { get; private set; } = string.Empty;

		/// <summary>Why each folder tried could not be written, one clause each.</summary>
		public string Failures { get; private set; } = string.Empty;

		/// <summary>
		/// Adds the text to the file (or holds it until there is one). When the file cannot be written it is
		/// closed for good and the answer is the line to tell the terminal, once; otherwise null.
		/// </summary>
		public string? Write( string text )
		{
			lock ( _lock )
			{
				if ( _held != null )
					_held.Append( text );
				else if ( _file != null )
					return Guarded( () => _file.Write( text ) );

				return null;
			}
		}

		/// <summary>
		/// Moves the old console.log over console-prev.log and the open <paramref name="fresh"/> file to
		/// <paramref name="path"/>. A move that fails leaves the run's file where it is, named by <see cref="Path"/>, and
		/// says so in <see cref="Failures"/> and at the head of the file.
		/// </summary>
		private void Rotate( string folder, string path, string fresh )
		{
			// A test's create hook may hand in a stream that is no file; there is then nothing to name.
			if ( !File.Exists( fresh ) )
				return;

			try
			{
				if ( File.Exists( path ) )
					File.Move( path, System.IO.Path.Join( folder, PreviousName ), overwrite: true );

				File.Move( fresh, path );
				Path = path;
			}
			catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException )
			{
				var why = $"{folder}'s log could not be moved into place ({e.GetType().Name}: {e.Message}), so this run is kept as {Path}";

				Failures += Failures.Length == 0 ? why : "; " + why;
				_held?.Append( $"[console log] {why}\n" );
			}
		}

		private string? Guarded( Action write )
		{
			try
			{
				write();
				return null;
			}
			catch ( IOException e )
			{
				try { _file?.Dispose(); } catch ( IOException ) { }

				_file = null;

				return $"[console log] writing {Path} failed ({e.GetType().Name}: {e.Message}); the file stops here and the terminal carries on";
			}
		}

		/// <summary>
		/// Takes the first folder that can be written and starts the new file with <paramref name="header"/> and
		/// everything held. In each folder the new file is opened first, beside the old one as console.log.new; only
		/// once that is open is console.log moved over console-prev.log (replacing it) and the new file named
		/// console.log, so a folder that cannot be written is left as it was and keeps both older files. Should the
		/// move fail after the open, the run keeps writing the new file under the name it has, <see cref="Path"/> says
		/// which, and <see cref="Failures"/> says why. Answers whether a file was opened; when none was, the
		/// terminal's copy is all there is and the held text is dropped.
		/// </summary>
		public bool Open( IEnumerable<string> folders, string header )
		{
			lock ( _lock )
			{
				foreach ( var folder in folders )
				{
					try
					{
						Directory.CreateDirectory( folder );

						var path = System.IO.Path.Join( folder, FileName );
						var fresh = path + NewSuffix;

						_file = new StreamWriter( _create( fresh ), new UTF8Encoding( false ) ) { AutoFlush = true };
						Path = fresh;

						Rotate( folder, path, fresh );
						break;
					}
					catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException )
					{
						var why = $"{folder} cannot be written ({e.GetType().Name}: {e.Message})";

						Failures += Failures.Length == 0 ? why : "; " + why;
						_held?.Append( $"[console log] {why}\n" );
					}
				}

				if ( _file != null && Guarded( () =>
				{
					_file.WriteLine( header );
					_file.Write( _held );
				} ) is { } lost )
				{
					Failures += Failures.Length == 0 ? lost : "; " + lost;
				}

				_held = null;
				return _file != null;
			}
		}

		public void Dispose()
		{
			lock ( _lock )
			{
				try { _file?.Dispose(); } catch ( IOException ) { }
			}
		}
	}

	/// <summary>A writer that passes everything to the terminal's own and to the <see cref="Sink"/>.</summary>
	public sealed class Tee( TextWriter terminal, Sink sink ) : TextWriter
	{
		public override Encoding Encoding => terminal.Encoding;

		public override void Write( char value )
		{
			terminal.Write( value );
			Note( sink.Write( value.ToString() ) );
		}

		public override void Write( string? value )
		{
			terminal.Write( value );
			Note( sink.Write( value ?? string.Empty ) );
		}

		public override void Write( char[] buffer, int index, int count ) => Write( new string( buffer, index, count ) );

		public override void Write( ReadOnlySpan<char> buffer ) => Write( new string( buffer ) );

		public override void Flush() => terminal.Flush();

		// The terminal's own line, once: the sink answers it only the first time the file fails. It may land
		// mid-line, so it starts on a fresh one.
		private void Note( string? line )
		{
			if ( line != null )
				terminal.Write( "\n" + line + "\n" );
		}
	}
}
