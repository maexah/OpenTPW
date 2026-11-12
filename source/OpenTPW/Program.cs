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

		// A gap, said at the site (Q995): the original lets one copy run (a named mutex, 0x0045f640 from 0x0045a97f, which leaves
		// when it exists already) and this game starts as many as are asked for, which then share one save folder and one console.log.
		try
		{
			Game.Run( args );
		}
		catch ( Exception e ) when ( StartFailure.IsFailedStart( e ) )
		{
			// Before the main loop, nobody is watching a terminal: the filter has logged the exception and its stack
			// before anything unwound, and the box has the message of the first one. An exception once the loop runs is the
			// next catch's.

			// An exception ahead of the game folder's being found leaves no log yet: keep it where a failed look does.
			if ( ConsoleLog.Path.Length == 0 )
				ConsoleLog.OpenWithoutGame();

			StartFailure.Show( StartFailure.Compose( StartFailure.First ?? e, ConsoleLog.Path ) );

			Environment.ExitCode = 1;
		}
		catch ( Exception e ) when ( RunFailure.IsRunFailure( e ) )
		{
			// Once the loop runs (Q79, a deviation): the filter has logged it, and this says so in a box, saves nothing and
			// ends with exit code 1 rather than the runtime's crash - see RunFailure.
			RunFailure.End( e );
		}
	}
}
