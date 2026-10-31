namespace OpenTPW;

/// <summary>
/// Program entry point
/// </summary>
public class Program
{
	public static void Main( string[] args )
	{
		// A Release build has no console of its own; a start from a terminal prints into that terminal (Q979).
		ParentConsole.Attach();

		// What is printed from here on is kept in console.log once the game's folder is found - see ConsoleLog.
		ConsoleLog.Start();

		// Before anything can open a native library - see NativeLibraries.
		NativeLibraries.Register();

		try
		{
			Game.Run( args );
		}
		catch ( Exception e ) when ( StartFailure.IsFailedStart( e ) )
		{
			// Before the main loop, nobody is watching a terminal: the filter has logged the exception and its stack
			// before anything unwound, and the box has the message of the first one. An exception once the loop runs is not
			// caught here and is reported as it always was.

			// An exception ahead of the game folder's being found leaves no log yet: keep it where a failed look does.
			if ( ConsoleLog.Path.Length == 0 )
				ConsoleLog.OpenWithoutGame();

			StartFailure.Show( StartFailure.Compose( StartFailure.First ?? e, ConsoleLog.Path ) );

			Environment.ExitCode = 1;
		}
	}
}
