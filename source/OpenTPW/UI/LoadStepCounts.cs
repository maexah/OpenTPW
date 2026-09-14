namespace OpenTPW;

/// <summary>
/// How many steps a load takes, learned by watching rather than written down.
///
/// <para>
/// The loading bar is a count, not a clock: it fills as assets register themselves
/// (<see cref="Asset.Register"/>), so it needs to know how many to expect before the load starts. That
/// number used to be a constant per scene, which was wrong in two ways at once. It had to be re-measured
/// by hand after any change to what a scene loads, and - worse - <b>there are more situations than there
/// were constants</b>. A scene built a second time in the same run costs far less than the first, because
/// the caches already hold most of what it asks for: the lobby costs 829 cold and 404 again, a park 918
/// and 758. Two constants cannot be right about four numbers, so two of them were always wrong.
/// </para>
/// <para>
/// So each situation keeps its own count here, and each is <b>whatever it measured last time</b>. A key
/// is the scene and whether it has been built before in this run - see <see cref="KeyFor"/> - which makes
/// a new situation cost nothing: a theme nobody has entered, or a kind of load that does not exist yet,
/// learns itself the first time it happens instead of needing a number chosen for it.
/// </para>
/// <para>
/// <b>The seeds are not maintained.</b> A count that has never been measured falls back to the seed its
/// caller passes, which is only what a first-ever run on a fresh install uses; from the second run on,
/// every bar is driven by what actually happened. Those seeds are allowed to drift and are not worth
/// correcting - that is the whole point of this. See <see cref="Expect"/>.
/// </para>
/// <para>
/// What is learned is kept in <c>save\opentpw.cfg</c> beside the display settings, because it is exactly
/// what that file is for - OpenTPW's own settings, which the original's files have nowhere to put. Its
/// format passes over any line it does not recognise, so a file written by a version that knows more
/// situations than this one loses only the lines this one cannot use.
/// </para>
/// </summary>
internal static class LoadStepCounts
{
	/// <summary>What each situation cost when it was last measured, by <see cref="KeyFor"/>'s key.</summary>
	private static readonly Dictionary<string, int> Known = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Which scenes have been built already in this run - the difference between a cold load and a rebuild.</summary>
	private static readonly HashSet<string> Built = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Whether anything has been learned that is not yet written down - see <see cref="TakeChanged"/>.</summary>
	private static bool _changed;

	/// <summary>The line name these are written under in <c>opentpw.cfg</c>.</summary>
	public const string LineName = "steps";

	/// <summary>
	/// The key for loading this scene now: the scene's own name, and whether it is being built for the
	/// first time in this run or again.
	///
	/// <para>
	/// The two differ by more than half - a rebuilt lobby is 404 steps against a cold 829 - because
	/// nothing releases what a scene loaded, so the second build of a scene finds most of its textures,
	/// models and materials already in the caches and registers only what is genuinely new. Both routes
	/// out of a park reach the second case: Exit To Lobby rebuilds the lobby, Restart Park rebuilds the
	/// park.
	/// </para>
	/// </summary>
	public static string KeyFor( string what ) => $"{Name( what )}.{(Built.Contains( Name( what ) ) ? "again" : "first")}";

	/// <summary>
	/// A scene's name with its spaces taken out. A key is written as one field of a line in
	/// <c>opentpw.cfg</c>, whose fields are separated by spaces, so "the lobby" would read back as two
	/// fields and put the count where the key should be. Done here rather than where the file is written,
	/// so that the key a test sees and the key the file holds are the same string.
	/// </summary>
	private static string Name( string what ) => what.Replace( ' ', '-' );

	/// <summary>
	/// How many steps to expect for a key - what it measured last time, or <paramref name="seed"/> where
	/// it has never been measured. A seed is a starting point for a first-ever run and nothing more; it
	/// is not kept up to date, because the measurement replaces it the moment one exists.
	/// </summary>
	public static int Expect( string key, int seed ) => Known.TryGetValue( key, out var steps ) && steps > 0 ? steps : seed;

	/// <summary>
	/// What a load actually cost, once it is over. The scene is marked as built, so the next load of it
	/// is a rebuild and asks a different question.
	/// </summary>
	public static void Record( string what, string key, int steps )
	{
		if ( steps > 0 && (!Known.TryGetValue( key, out var before ) || before != steps) )
		{
			Known[key] = steps;
			_changed = true;
		}

		Built.Add( Name( what ) );
	}

	/// <summary>One learned count as it was read back from <c>opentpw.cfg</c>. Does not count as a change to write out.</summary>
	public static void Restore( string key, int steps )
	{
		if ( steps > 0 )
			Known[key] = steps;
	}

	/// <summary>Every count worth writing, in a settled order so the file does not churn between runs.</summary>
	public static IEnumerable<KeyValuePair<string, int>> All
		=> Known.OrderBy( pair => pair.Key, StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// Whether anything has been learned since this was last asked, and forgets that it had. The file is
	/// only rewritten when a count actually moved, so a run that loads nothing new leaves the save folder
	/// alone - which matters, because these live beside the player's own settings.
	/// </summary>
	public static bool TakeChanged()
	{
		var changed = _changed;
		_changed = false;

		return changed;
	}

	/// <summary>Forgets everything, for tests - nothing in the game has cause to call it.</summary>
	internal static void Forget()
	{
		Known.Clear();
		Built.Clear();
		_changed = false;
	}
}
