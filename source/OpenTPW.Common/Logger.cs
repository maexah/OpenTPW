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
	/// An informational line. <paramref name="quiet"/> keeps it off the console; it still reaches
	/// <see cref="QuietLog"/> and <see cref="OnLog"/> - see <see cref="Log"/>.
	/// </summary>
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

		// A quiet line stays off the console. It still reaches QuietLog and OnLog, because "quiet" is about
		// the console and not about suppressing the record.
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
