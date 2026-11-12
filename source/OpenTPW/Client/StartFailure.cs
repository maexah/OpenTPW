namespace OpenTPW;

/// <summary>
/// Says why a start failed in a message box, as well as in the log.
///
/// <para>
/// A start that cannot find the game logs its reasons (<see cref="GameDir"/>) and exits, and on Windows a start from
/// Explorer has no terminal to show them on; the same holds for an exception before the game's main loop begins. The original never needed this, so it is a deviation, made on every system at the
/// maintainer's word (Q978): an SDL message box holding the reason lines, where the game looked, how to put it right
/// and where the log is. The log keeps the same lines whether or not the box could be shown.
/// </para>
/// <para>
/// A reader closes a long box unread, and SDL draws a line unwrapped, so on a 1024-pixel display the head and the tail of
/// a long one fall off the screen (Q912): the box is wrapped to <see cref="BoxWidth"/> columns when it is shown, and the
/// log keeps each line whole. An exception's box has its message and no more, as the log has the type and the stack.
/// </para>
/// <para>
/// SDL's message box may be called before SDL is initialised. With no display it answers an error and shows nothing
/// (nothing reaches the terminal either), and the log's line says so. <c>OPENTPW_NO_MESSAGE_BOX</c> set to
/// anything keeps the box off, which the tests and harnesses that start the program on purpose without a game set.
/// </para>
/// </summary>
internal static class StartFailure
{
	/// <summary>Set to anything to keep the box from being shown: a harness or a test that fails a start on purpose.</summary>
	public const string SuppressVariable = "OPENTPW_NO_MESSAGE_BOX";

	public const string Title = "OpenTPW could not start";

	/// <summary>The columns a line of the box is wrapped to: SDL's X11 box was 7 pixels a column with the font the Xvfb picked (Q912's shots; SDL asks for any 12-point font), so this is under 500 of a 640-pixel display's.</summary>
	public const int BoxWidth = 64;

	/// <summary>The line the box opens with.</summary>
	public const string Head = "OpenTPW could not start.";

	/// <summary>Whether the main loop has begun: an exception after this is not a failed start.</summary>
	public static bool Started { get; set; }

	/// <summary>The first exception <see cref="IsFailedStart"/> saw: the cause, which a later one thrown while unwinding must not replace.</summary>
	public static Exception? First { get; private set; }

	/// <summary>Forgets <see cref="First"/>: a test that stands in for a start.</summary>
	internal static void ResetForTest() => First = null;

	/// <summary>
	/// The filter of <c>Program.Main</c>'s catch, run before anything unwinds: answers whether <paramref name="e"/> ended the
	/// start (the loop has not begun), and if so logs it with its stack there and then, so the log has the cause ahead of
	/// anything unwinding says ("Loaded ..." lines, a later exception) as it did before the catch existed.
	/// </summary>
	public static bool IsFailedStart( Exception e )
	{
		if ( Started )
			return false;

		Console.Error.WriteLine( $"Unhandled exception: {e}" );
		First ??= e;

		return true;
	}

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

	/// <summary>
	/// The message for an exception (the cause of a type initialiser's): its message's first line, then where the log is. The
	/// log has the type, the whole message and the stack (<see cref="IsFailedStart"/>). A message that is empty gives the type's name.
	/// </summary>
	public static string Compose( Exception e, string logPath )
	{
		// A static initialiser that throws arrives wrapped (Material's shaders are built in one); the box names the cause.
		while ( e is TypeInitializationException { InnerException: { } inner } )
			e = inner;

		var reason = e.Message.ReplaceLineEndings( "\n" ).Split( '\n' )[0].Trim();

		return Compose( Head, [reason.Length > 0 ? reason : e.GetType().Name], logPath );
	}

	/// <summary>
	/// <paramref name="text"/> with each line broken at its spaces to at most <paramref name="width"/> columns. A word longer than
	/// that (a path) is broken after its last separator that fits, else at the width.
	/// </summary>
	internal static string Wrap( string text, int width = BoxWidth )
		=> string.Join( "\n", text.ReplaceLineEndings( "\n" ).Split( '\n' ).SelectMany( line => WrapLine( line, width ) ) );

	private static IEnumerable<string> WrapLine( string line, int width )
	{
		var current = string.Empty;

		foreach ( var word in line.Split( ' ' ) )
		{
			var rest = word;

			if ( rest.Length == 0 )
			{
				if ( current.Length > 0 && current.Length < width )
					current += " ";

				continue;
			}

			if ( current.Length > 0 && current.Length + 1 + rest.Length <= width )
			{
				current += " " + rest;
				continue;
			}

			if ( current.Length > 0 && rest.Length > 0 )
			{
				yield return current;
				current = string.Empty;
			}

			while ( rest.Length > width )
			{
				var cut = rest.LastIndexOfAny( ['/', '\\'], width - 1 );
				cut = cut > 0 ? cut + 1 : width;

				yield return rest[..cut];
				rest = rest[cut..];
			}

			current = current.Length > 0 ? current + " " + rest : rest;
		}

		yield return current;
	}

	/// <summary>
	/// Shows <paramref name="message"/> in an error box and answers whether SDL did. The log is told which, so a
	/// start that showed nothing says why.
	/// </summary>
	public static bool Show( string message ) => Show( message, Title, Label );

	/// <summary><see cref="Show(string)"/> under another title, its log lines opening with <paramref name="label"/>: a failure once the game runs (<see cref="RunFailure"/>).</summary>
	internal static bool Show( string message, string title, string label )
		=> Show( message, text => ShowWithSdl( title, text ), line => Log.Info( line ), line => Log.Warning( line ), label );

	/// <summary>What the log's lines of a failed start's box open with.</summary>
	public const string Label = "Start failure";

	/// <summary>The SDL box: null where it was shown, else what SDL said. Throws where SDL itself would not load.</summary>
	private static unsafe string? ShowWithSdl( string title, string message )
	{
		var sdl = NeoVeldrid.Sdl2.Sdl2Window.SdlInstance;

		return sdl.ShowSimpleMessageBox( (uint)Silk.NET.SDL.MessageBoxFlags.Error, title, message, (Silk.NET.SDL.Window*)null ) == 0
			? null
			: sdl.GetErrorS();
	}

	/// <summary><see cref="Show(string)"/> with the box and the log's two lines given, for a test. The box is handed the message wrapped; the log's line has it whole.</summary>
	internal static bool Show( string message, Func<string, string?> box, Action<string> info, Action<string> warn, string label = Label )
	{
		if ( Environment.GetEnvironmentVariable( SuppressVariable ) is { Length: > 0 } )
		{
			info( $"{label}: no message box, {SuppressVariable} is set: {message.ReplaceLineEndings( " | " )}" );
			return false;
		}

		string? error;

		try
		{
			error = box( Wrap( message ) );
		}
		catch ( Exception e )
		{
			// SDL itself would not load: the log and the terminal are all there is.
			warn( $"{label}: no message box, SDL could not be used ({e.GetType().Name}: {e.Message})" );
			return false;
		}

		if ( error is not null )
		{
			warn( $"{label}: no message box, SDL said '{error}'" );
			return false;
		}

		// Outside the try: the log's hook may draw (the loading screen's), and a throw from it says nothing about the box, which was shown.
		try
		{
			info( $"{label}: message box shown" );
		}
		catch ( Exception e )
		{
			Console.Error.WriteLine( $"{label}: the log's hook threw after the box line ({e.GetType().Name}: {e.Message})" );
		}

		return true;
	}
}
