namespace OpenTPW;

/// <summary>
/// Program entry point
/// </summary>
public class Program
{
	public static void Main( string[] args )
	{
		// What is printed from here on is kept in console.log once the game's folder is found - see ConsoleLog.
		ConsoleLog.Start();

		// Before anything can open a native library - see NativeLibraries.
		NativeLibraries.Register();

		try
		{
			Game.Run( args );
		}
		catch ( Exception e ) when ( !StartFailure.Started )
		{
			// Before the main loop, nobody is watching a terminal: log it and say it in a box (Q978). The
			// stack goes to the log; the box has the message. An exception once the loop runs is not caught
			// here and is reported as it always was.
			Console.Error.WriteLine( $"Unhandled exception: {e}" );

			// An exception ahead of the game folder's being found leaves no log yet: keep it where a failed look does.
			if ( ConsoleLog.Path.Length == 0 )
				ConsoleLog.OpenWithoutGame();

			StartFailure.Show( StartFailure.Compose( e, ConsoleLog.Path ) );

			Environment.ExitCode = 1;
		}
	}
}
