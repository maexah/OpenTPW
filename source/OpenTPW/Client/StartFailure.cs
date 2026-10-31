namespace OpenTPW;

/// <summary>
/// Says why a start failed in a message box, as well as in the log.
///
/// <para>
/// A start that cannot find the game logs its reasons (<see cref="GameDir"/>) and exits, and on Windows a start from
/// Explorer then shows a terminal for about a second and nothing else; the same holds for an exception before the
/// game's main loop begins. The original never needed this, so it is a deviation, made on every system at the
/// maintainer's word (Q978): an SDL message box holding the reason lines, where the game looked, how to put it right
/// and where the log is. The log keeps the same lines whether or not the box could be shown.
/// </para>
/// <para>
/// SDL's message box may be called before SDL is initialised, and falls back to the terminal's own when there is no
/// display; a machine with neither shows nothing, and the log's line says so. <c>OPENTPW_NO_MESSAGE_BOX</c> set to
/// anything keeps the box off, which the tests and harnesses that start the program on purpose without a game set.
/// </para>
/// </summary>
internal static class StartFailure
{
	/// <summary>Set to anything to keep the box from being shown: a harness or a test that fails a start on purpose.</summary>
	public const string SuppressVariable = "OPENTPW_NO_MESSAGE_BOX";

	public const string Title = "OpenTPW could not start";

	/// <summary>The line the box opens with.</summary>
	public const string Head = "OpenTPW could not start.";

	/// <summary>Whether the main loop has begun: an exception after this is not a failed start.</summary>
	public static bool Started { get; set; }

	/// <summary>The message: the head, the reason lines as the log has them, and the log's place.</summary>
	public static string Compose( string head, IEnumerable<string> reasons, string logPath )
	{
		var lines = new List<string> { head, string.Empty };

		lines.AddRange( reasons.Select( reason => Sentence( reason.Replace( "Game files: ", string.Empty ) ) ) );

		lines.Add( string.Empty );
		lines.Add( logPath.Length > 0
			? $"Log: {logPath}"
			: "Log: none" );

		return string.Join( "\n", lines );
	}

	private static string Sentence( string line ) => line.Length > 0 ? char.ToUpperInvariant( line[0] ) + line[1..] : line;

	/// <summary>The message for an exception: its type and message, then where the log (which has the stack) is.</summary>
	public static string Compose( Exception e, string logPath )
		=> Compose( Head, [$"{e.GetType().Name}: {e.Message}"], logPath );

	/// <summary>
	/// Shows <paramref name="message"/> in an error box and answers whether SDL did. The log is told which, so a
	/// start that showed nothing says why.
	/// </summary>
	public static unsafe bool Show( string message )
	{
		if ( Environment.GetEnvironmentVariable( SuppressVariable ) is { Length: > 0 } )
		{
			Log.Info( $"Start failure: no message box, {SuppressVariable} is set" );
			return false;
		}

		try
		{
			var sdl = NeoVeldrid.Sdl2.Sdl2Window.SdlInstance;

			if ( sdl.ShowSimpleMessageBox( (uint)Silk.NET.SDL.MessageBoxFlags.Error, Title, message, (Silk.NET.SDL.Window*)null ) == 0 )
			{
				Log.Info( "Start failure: message box shown" );
				return true;
			}

			Log.Warning( $"Start failure: no message box, SDL said '{sdl.GetErrorS()}'" );
		}
		catch ( Exception e )
		{
			// SDL itself would not load: the log and the terminal are all there is.
			Log.Warning( $"Start failure: no message box, SDL could not be used ({e.GetType().Name}: {e.Message})" );
		}

		return false;
	}
}
