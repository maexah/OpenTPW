namespace OpenTPW;

/// <summary>
/// A write to the save folder that failed because the folder cannot be written, kept until the scene tells the player; it
/// also carries the start's notice about players in the folder not in use (<see cref="SaveHome"/>).
///
/// <para>
/// <b>A deviation</b> (Q982, <c>AGENTS.md</c> rule 11). The original says nothing here: its save answers a failure
/// number and its screen closes all the same (<c>docs/exe/saves.md</c>, "The save"). On Windows a player's install under
/// Program Files is not writable unless something opened it, so a save lost quietly is a real case. The scene's window
/// stack opens the game's own message box with its cross alone, as the research opener's boxes are
/// (<see cref="UI.ParkResearchOpener"/>), once a frame and never over another modal window.
/// </para>
/// <para>
/// What the player asked for (<see cref="Asked"/>: Alt+S, Save Park, the options screen's tick) is told every time.
/// What the game does on its own (the autosave on leaving, restart.INTS on entering, a player written as a new
/// one is made) is told once to a visit, and a visit is one scene built (<see cref="NewVisit"/>), so a folder
/// that stays shut does not nag. The autosave at leaving is told in the lobby the game goes on to, so it never holds
/// the exit. Quit Game cannot tell afterwards, as the window is gone: its question says so beforehand
/// (<see cref="QuitQuestion"/>). The window's own close tells nobody; the log says.
/// </para>
/// </summary>
internal static class SaveTrouble
{
	private static string? _what;
	private static bool _asked;
	private static bool _told;
	private static string? _notice;
	private static string? _noticeName;

	/// <summary>Whether a failure or a notice waits to be told.</summary>
	internal static bool Pending => _what != null || _notice != null;

	/// <summary>Whether a failed write waits to be told (not the start's notice): what was just written was lost.</summary>
	internal static bool LostAWrite => _what != null;

	/// <summary>
	/// Something the player is told once, not a failure: that the game's folder cannot be written and saves went to the player's,
	/// or that the player's folder holds players the kept game folder does not read (<see cref="SaveHome"/>, Q984, Q908). Shown by the
	/// same poll, ahead of a failure.
	/// </summary>
	/// <param name="name">The name opentpw.cfg remembers the notice under; <see cref="Take(out string?)"/> hands it back with the text, for the caller to count it told once its box is open (<see cref="SaveHome.Told"/>).</param>
	internal static void Tell( string text, string? name = null )
	{
		_notice = text;
		_noticeName = name;
	}

	/// <summary>
	/// A write failed. Only a folder that cannot be written counts: a fault in a file's format
	/// (<see cref="InvalidDataException"/>) is not this. One failure is told: the first waiting, except that the park
	/// outranks the player's file written before it and the options.
	/// </summary>
	/// <param name="what">What was lost, in capitals: PARK, PLAYER or OPTIONS.</param>
	internal static void Failed( string what, Exception e )
	{
		if ( e is not (IOException or UnauthorizedAccessException) )
			return;

		if ( _what == null || what == "PARK" )
			_what = what;
	}

	/// <summary>The failure waiting was of something the player asked for, so it is told whenever it happens.</summary>
	internal static void Asked()
	{
		if ( _what != null )
			_asked = true;
	}

	/// <summary>A scene was built: what the game does on its own is told to it once more.</summary>
	internal static void NewVisit() => _told = false;

	/// <summary>
	/// What to say, or null where nothing waits or the game's own failure was told already this visit. Either
	/// way the failure waiting is let go.
	/// </summary>
	internal static string? Take() => Take( out _ );

	/// <summary>As <see cref="Take()"/>; <paramref name="noticeName"/> is the name the start's notice was told under, null for a failure.</summary>
	internal static string? Take( out string? noticeName )
	{
		noticeName = null;

		if ( _notice is { } notice )
		{
			noticeName = _noticeName;
			_notice = null;
			_noticeName = null;
			return notice;
		}

		if ( _what is not { } what )
			return null;

		var asked = _asked;
		_what = null;
		_asked = false;

		if ( !asked && _told )
		{
			Log.Info( $"Save trouble: {what} NOT SAVED is not told again this visit" );
			return null;
		}

		_told = true;

		return $"{what} NOT SAVED\n\nThe save folder cannot be written.";
	}

	/// <summary>Quit Game's question, with a line more where nothing can be saved.</summary>
	internal static string QuitQuestion( string question )
		=> SaveFolder.CanWrite() ? question : $"{question}\nThe save folder cannot be written, so nothing is saved.";
}
