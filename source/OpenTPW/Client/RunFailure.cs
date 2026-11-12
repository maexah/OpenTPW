namespace OpenTPW;

/// <summary>
/// Ends the run in order when an exception escapes the game once its main loop has begun (Q79): <c>Program.Main</c>'s
/// last catch.
///
/// <para>
/// Before this, such an exception left the process as the runtime's crash: the log had it (ConsoleLog's handler) and
/// nothing else did, a player who started the game from Explorer saw it vanish, and audio was never shut down. OpenTPW's
/// own, a deviation in the manner of <see cref="StartFailure"/> (Q978): the exception and its stack are logged before
/// anything unwinds, a message box says the game stopped and where the log is, the audio device is closed, and the exit
/// code is 1. What the original does when its loop fails is not in <c>docs/exe/</c> and was not read.
/// </para>
/// <para>
/// <b>Nothing is saved.</b> A frame that threw may have left the park and the players half-changed, and the autosave or
/// a player's file written from that would put a broken file over a good one; so neither the park's leaving nor the
/// players' save (<see cref="Game.LeaveOnClose"/>) runs, and the box says so.
/// </para>
/// <para>
/// Only the thread that runs the loop is covered: an exception on another thread (the update check, the console's
/// reader) is still the runtime's.
/// </para>
/// </summary>
internal static class RunFailure
{
	public const string Title = "OpenTPW stopped";

	/// <summary>The line the box opens with.</summary>
	public const string Head = "OpenTPW stopped because of an error.";

	/// <summary>The box's line that says what was not written.</summary>
	public const string NothingSaved = "Nothing was saved as it stopped, so that a park or player left half-changed is not written over a good one.";

	/// <summary>What the log's lines of the failure open with.</summary>
	public const string Label = "Run failure";

	/// <summary>The first exception <see cref="IsRunFailure"/> saw: the cause, which a later one thrown while unwinding must not replace.</summary>
	public static Exception? First { get; private set; }

	/// <summary>Forgets <see cref="First"/>: a test that stands in for a run.</summary>
	internal static void ResetForTest() => First = null;

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

	/// <summary>The message: the head, the exception's first line (its type where it has none), what was not saved, and the log's place.</summary>
	public static string Compose( Exception e, string logPath )
	{
		while ( e is TypeInitializationException { InnerException: { } inner } )
			e = inner;

		var reason = e.Message.ReplaceLineEndings( "\n" ).Split( '\n' )[0].Trim();

		return StartFailure.Compose( Head, [reason.Length > 0 ? reason : e.GetType().Name, NothingSaved], logPath );
	}

	/// <summary>Shows the box, closes the audio device and sets the exit code: what the catch does once the stack has unwound.</summary>
	public static void End( Exception e )
	{
		StartFailure.Show( Compose( First ?? e, ConsoleLog.Path ), Title, Label );

		End( Audio.Shutdown );
	}

	/// <summary>The end after the box, with the shutdown given, for a test: a shutdown that throws too is logged and the exit code is still 1.</summary>
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
