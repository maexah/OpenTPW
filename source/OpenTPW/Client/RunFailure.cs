namespace OpenTPW;

/// <summary>
/// Ends the run in order when an exception escapes the game once its main loop has begun (Q79): <c>Program.Main</c>'s
/// last catch.
///
/// <para>
/// Before this, such an exception left the process as the runtime's crash: the log had it (ConsoleLog's handler) and
/// nothing else did, a player who started the game from Explorer saw it vanish, and audio was never shut down. OpenTPW's
/// own, a deviation in the manner of <see cref="StartFailure"/> (Q978): the exception and its stack are logged before
/// anything unwinds, the audio device is closed, a message box says the game stopped and where the log is, and the exit
/// code is 1. What the original does when its loop fails is not in <c>docs/exe/</c> and was not read.
/// </para>
/// <para>
/// <b>Nothing is saved.</b> A frame that threw may have left the park and the players half-changed, and the autosave or
/// a player's file written from that would put a broken file over a good one; so for an exception out of the loop neither
/// the park's leaving nor the players' save (<see cref="Game.LeaveOnClose"/>) runs, and the box says so. An exception out
/// of the close after the loop (<see cref="Closing"/>) comes while those are being written, so the box says either may
/// not have been.
/// </para>
/// <para>
/// Only the thread that runs the loop is covered. The other thread that runs game code is SDL's audio thread
/// (<c>Audio.Mix</c>, a native callback): an exception there is still the runtime's crash, with no box and no exit code 1.
/// The update check catches its own, and the console's reader only queues lines.
/// </para>
/// </summary>
internal static class RunFailure
{
	public const string Title = "OpenTPW stopped";

	/// <summary>The line the box opens with.</summary>
	public const string Head = "OpenTPW stopped because of an error.";

	/// <summary>The box's line that says what was not written.</summary>
	public const string NothingSaved = "Nothing was saved as it stopped, so that a park or player left half-changed is not written over a good one.";

	/// <summary>The box's line in place of <see cref="NothingSaved"/> for an exception out of the close, which saves the park and then the player.</summary>
	public const string ClosingSaved = "It stopped while closing, as the park and the player were being saved, so either may not have been.";

	/// <summary>What the log's lines of the failure open with.</summary>
	public const string Label = "Run failure";

	/// <summary>The first exception <see cref="IsRunFailure"/> saw: the cause, which a later one thrown while unwinding must not replace.</summary>
	public static Exception? First { get; private set; }

	/// <summary>Whether the loop has returned and the close after it runs (<see cref="Game.LeaveOnClose"/>): set by <see cref="Game.Run"/>.</summary>
	public static bool Closing { get; set; }

	/// <summary>Forgets <see cref="First"/> and <see cref="Closing"/>: a test that stands in for a run.</summary>
	internal static void ResetForTest()
	{
		First = null;
		Closing = false;
	}

	/// <summary>
	/// The filter of <c>Program.Main</c>'s last catch, run before anything unwinds: answers whether <paramref name="e"/>
	/// ended a run whose loop had begun (<see cref="StartFailure.Started"/>), and if so logs it with its stack there and then.
	/// </summary>
	public static bool IsRunFailure( Exception e )
	{
		if ( !StartFailure.Started )
			return false;

		Console.Error.WriteLine( $"{Label}: the game stops on an unhandled exception: {e}" );
		First ??= e;

		return true;
	}

	/// <summary>
	/// The message: the head, the exception's first line (its type where it has none), what was not saved (or, in the close,
	/// what may not have been), and the log's place.
	/// </summary>
	public static string Compose( Exception e, string logPath, bool closing = false )
	{
		while ( e is TypeInitializationException { InnerException: { } inner } )
			e = inner;

		var reason = e.Message.ReplaceLineEndings( "\n" ).Split( '\n' )[0].Trim();

		return StartFailure.Compose( Head, [reason.Length > 0 ? reason : e.GetType().Name, closing ? ClosingSaved : NothingSaved], logPath );
	}

	/// <summary>
	/// Closes the audio device, shows the box and sets the exit code: what the catch does once the stack has unwound. The
	/// sound stops before the box opens, so the music and looping voices do not play on behind a frozen window.
	/// </summary>
	public static void End( Exception e ) => End( e, Audio.Shutdown, message => StartFailure.Show( message, Title, Label ) );

	/// <summary><see cref="End(Exception)"/> with the shutdown and the box given, for a test.</summary>
	internal static void End( Exception e, Action shutdown, Action<string> show )
	{
		End( shutdown );
		show( Compose( First ?? e, ConsoleLog.Path, Closing ) );
	}

	/// <summary>The shutdown and the exit code, with the shutdown given, for a test: a shutdown that throws too is logged and the exit code is still 1.</summary>
	internal static void End( Action shutdown )
	{
		try
		{
			shutdown();
		}
		catch ( Exception e )
		{
			Console.Error.WriteLine( $"{Label}: the audio device could not be closed ({e.GetType().Name}: {e.Message})" );
		}

		Environment.ExitCode = 1;
	}
}
