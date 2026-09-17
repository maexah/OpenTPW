namespace OpenTPW;

/// <summary>
/// Handles all debug logging functionality.
/// </summary>
public class Logger
{
	public enum Level
	{
		Trace,
		Info,
		Warning,
		Error
	};

	public void Trace( object obj ) => Log( obj?.ToString(), Level.Trace );

	/// <summary>
	/// An informational line. <paramref name="quiet"/> keeps it off the console and sends it only to
	/// <see cref="QuietLog"/> - see <see cref="Log"/> for why that used to do nothing at all.
	/// </summary>
	/// <remarks>
	/// <b>There were two overloads here and one of them was unreachable.</b> A bare <c>Info( x )</c> binds
	/// to the more specific one-argument method rather than to the defaulted one, so the two-argument
	/// version was reached only by the single call site that passes the flag positionally. One method with
	/// a default does what both were for, and leaves no version that silently wins overload resolution.
	/// </remarks>
	public void Info( object obj, bool quiet = false ) => Log( obj?.ToString(), Level.Info, quiet );

	public void Warning( object obj ) => Log( obj?.ToString(), Level.Warning );
	public void Error( object obj ) => Log( obj?.ToString(), Level.Error );

	public delegate void LogDelegate( Level severity, string logText );
	public static LogDelegate? OnLog;
	public static LogDelegate? QuietLog;

	private static void Log( string? str, Level severity = Level.Trace, bool quiet = false )
	{
		if ( str == null )
			return;

		// <b>Quiet now actually is quiet.</b> This used to raise QuietLog and then fall straight through to
		// the console anyway, with no return and no else - so the one caller that asked for a quiet line got
		// it printed regardless, and the flag did nothing but look like it worked. The line still reaches
		// QuietLog and OnLog, because "quiet" is about the console and not about suppressing the record.
		if ( quiet )
		{
			QuietLog?.Invoke( severity, str );
			OnLog?.Invoke( severity, str );

			return;
		}

		Console.ForegroundColor = SeverityToConsoleColor( severity );
		Console.WriteLine( $"[{DateTime.Now.ToLongTimeString()}] {str}" );

		OnLog?.Invoke( severity, str );
	}

	private static ConsoleColor SeverityToConsoleColor( Level severity ) => severity switch
	{
		Level.Error => ConsoleColor.DarkRed,
		Level.Warning => ConsoleColor.Red,
		Level.Trace => ConsoleColor.DarkGray,
		_ => ConsoleColor.White,
	};
}
