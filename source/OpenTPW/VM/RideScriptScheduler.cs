namespace OpenTPW;

/// <summary>
/// Gives each ride script its turn, on the original's own schedule.
///
/// <para>
/// A ride script is not run every tick. The engine keeps one counter for the whole system and walks its
/// list of scripts each tick, running a script only when <c>(id ^ tick) &amp; 7</c> comes out zero - so
/// every script gets a turn once in eight ticks, and the eight are spread apart by the scripts' own ids
/// rather than all falling together. <c>TURBO</c> opts a script out of the spread and into every tick.
/// Read from <c>FUN_005516b0</c>, which <c>Game_StateMachine</c> calls at <c>0x0054f56b</c>.
/// </para>
///
/// <para>
/// <b>The counter moves before any script runs</b>, which is why the first tick is 1 and not 0 - it
/// decides who runs on it. A script that has stopped is dropped at the end of the same tick, as the
/// engine unregisters one whose program counter has gone negative.
/// </para>
///
/// <para>
/// <b>What this deliberately does not do.</b> Each turn, the original also pushes the script's speed word
/// into its ride, its sound and a linked script, and moves any particle objects hung off it. None of
/// those exist here yet, and inventing them would put motion in the world that nothing asked for - see
/// <see cref="RideScript"/> for the same reasoning about world-touching opcodes.
/// </para>
/// </summary>
public sealed class RideScriptScheduler
{
	/// <summary>One turn in eight, which is what the engine's <c>&amp; 7</c> comes to.</summary>
	public const int TicksBetweenTurns = RideScript.TicksBetweenTurns;

	private readonly List<Entry> _entries = [];

	/// <summary>
	/// How many ticks have been taken. The engine counts these in <c>DAT_008791a4</c> and increments it
	/// at the top of the tick, before it looks at a single script.
	/// </summary>
	public int Tick { get; private set; }

	/// <summary>How many scripts are still being given turns.</summary>
	public int Count => _entries.Count;

	/// <summary>How many scripts have stopped and been dropped since this scheduler was made.</summary>
	public int Finished { get; private set; }

	/// <summary>How many turns have been handed out in total - one per script per tick it was due.</summary>
	public int TurnsGiven { get; private set; }

	/// <summary>The scripts still being run, in the order they were added.</summary>
	public IEnumerable<RideScript> Scripts => _entries.Select( entry => entry.Script );

	/// <summary>
	/// Whether a script with this id is due a turn on this tick. The whole of the engine's rule, and
	/// pure, so it can be checked against the formula rather than inferred from a run.
	/// </summary>
	public static bool RunsOn( int id, int tick, bool everyTick ) => everyTick || ( ( id ^ tick ) & 7 ) == 0;

	/// <summary>
	/// Puts a script under the scheduler. The id is the caller's: in the original it is the script
	/// object's own field 2, the same id <c>COAST_INITIALISE</c> matches a ride on, and it is not
	/// anywhere in the script file - so nothing here can invent it.
	/// </summary>
	public void Add( int id, RideScript script )
	{
		ArgumentNullException.ThrowIfNull( script );

		_entries.Add( new Entry( id, script ) );
	}

	/// <summary>Takes a script out by id, whether or not it has stopped. True if one went.</summary>
	public bool Remove( int id )
	{
		var at = _entries.FindIndex( entry => entry.Id == id );

		if ( at < 0 )
			return false;

		_entries.RemoveAt( at );

		return true;
	}

	/// <summary>
	/// One tick: everybody due a turn takes one, and anything that stopped is dropped.
	/// <paramref name="now"/> is the caller's clock, handed to <see cref="RideScript.Turn"/> unchanged.
	/// </summary>
	public void Advance( float now )
	{
		++Tick;

		// Indexed rather than foreach because a script's turn must not be able to disturb the walk, and
		// because the engine's own walk is over a list it is prepared to have change under it.
		for ( int i = 0; i < _entries.Count; ++i )
		{
			var entry = _entries[i];

			if ( !RunsOn( entry.Id, Tick, entry.Script.EveryTick ) )
				continue;

			++TurnsGiven;
			entry.Script.Turn( now );
		}

		var dropped = _entries.RemoveAll( entry => !entry.Script.Running );

		Finished += dropped;
	}

	private readonly record struct Entry( int Id, RideScript Script );
}
