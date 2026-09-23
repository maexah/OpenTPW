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
/// into its ride, its sound and <b>its child</b> - that last one found through this same registry, by the
/// child id at <c>+0x0c</c> - and moves any particle objects hung off it. The speed word is 50 for every
/// script that ever runs and no opcode writes it, so copying it into a child could not change anything;
/// the rest does not exist here yet, and inventing it would put motion in the world that nothing asked
/// for - see <see cref="RideScript"/> for the same reasoning about world-touching opcodes.
/// </para>
/// </summary>
public sealed class RideScriptScheduler
{
	/// <summary>One turn in eight, which is what the engine's <c>&amp; 7</c> comes to.</summary>
	public const int TicksBetweenTurns = RideScript.TicksBetweenTurns;

	private readonly List<Entry> _entries = [];

	/// <summary>
	/// The highest id handed out or taken in, so a script from <see cref="Spawn"/> gets one nothing else
	/// is using. The engine keeps the same counter at <c>DAT_008791a8</c> and only ever increments it - it
	/// is written in three places, the loader and the two that reset the whole system - so <b>an id is
	/// never reused while the game runs</b>, and nothing can be left holding an id that has quietly come
	/// to mean a different script.
	/// </summary>
	private int _lastId;

	/// <summary>
	/// How the name in a <c>SPAWNCHILD</c> or a <c>SPAWNSOUND</c> becomes a script, or null where nothing
	/// can load one - in which case both instructions are counted rather than guessed.
	///
	/// <para>
	/// The engine's loader takes a whole path, which the instruction builds from the script's own
	/// directory: <c>FUN_005587f0</c> keeps that at <c>+0x38</c>, having stripped the last component off
	/// the path it was itself loaded from. Nothing here knows where a script came from, so the caller is
	/// handed the name as the script wrote it. Two things about those names are worth knowing before
	/// resolving one: <b>the name already carries its extension</b> - every shipped <c>SPAWNCHILD</c> asks
	/// for something ending <c>.rse</c> - and <b>its case will not match the file</b>, since scripts ask
	/// for <c>Effects.rse</c>, <c>clock.rse</c>, <c>worn.rse</c> and <c>anims.rse</c> where the archives
	/// hold <c>effects.RSE</c>, <c>Clock.RSE</c>, <c>Worn.RSE</c> and <c>Anims.RSE</c>.
	/// </para>
	/// </summary>
	public Func<string, RideScript?>? Loader { get; set; }

	/// <summary>
	/// Whether the music is muted, and by what value - the engine's one global, which <c>DIPMUSIC</c>
	/// writes and the mixer tests against nought when it next re-applies its group volumes.
	///
	/// <para>
	/// <b>It is one setting for the whole game rather than one per script</b>, so the last script to set
	/// it wins and the death of any script that was holding it releases it for everybody. That is the
	/// engine's shape, not a simplification: the per-script part is only the marker byte recording who
	/// is holding it - see <see cref="RideScript.DippedMusic"/>.
	/// </para>
	/// </summary>
	public int MusicDip { get; set; }

	/// <summary>
	/// How many ticks have been taken. The engine counts these in <c>DAT_008791a4</c> and increments it
	/// at the top of the tick, before it looks at a single script.
	/// </summary>
	public int Tick { get; private set; }

	/// <summary>How many scripts are still being given turns.</summary>
	public int Count => _entries.Count;

	/// <summary>
	/// How many scripts have stopped and been dropped since this scheduler was made. A script taken down
	/// because the script that spawned it died is removed without being counted here: it did not finish,
	/// it was killed - see <see cref="Destroy"/>.
	/// </summary>
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

		// A script carries its own id in the original - field 2 of its frame - and every instruction that
		// reaches another script goes through this one registry to find it. So registering a script is
		// also what tells it who it is and where to look.
		script.Id = id;
		script.Host = this;

		if ( id > _lastId )
			_lastId = id;

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
	/// The script with this id, or null where nothing has it - the engine's <c>FUN_0055a070</c>, which
	/// every instruction that reaches another script goes through.
	///
	/// <para>
	/// <b>Newest first</b>, because the engine's registry is a linked list its loader pushes each new
	/// script onto the head of, and this is that walk. The order is not cosmetic: <c>FINDSCRIPTRAND</c>
	/// picks the n-th script matching a name, and walking the other way would pick a different one.
	/// </para>
	///
	/// <para>
	/// <b>Nought is not special here.</b> The instructions test their child and parent ids against nought
	/// before they ever call this, exactly as the engine does, so the sentinel stays where the engine puts
	/// it - and a caller that genuinely registered a script under 0 still finds it.
	/// </para>
	/// </summary>
	public RideScript? Find( int id )
	{
		for ( int i = _entries.Count - 1; i >= 0; --i )
		{
			if ( _entries[i].Id == id )
				return _entries[i].Script;
		}

		return null;
	}

	/// <summary>Every script, newest first - the order all of the engine's registry walks take.</summary>
	public IEnumerable<RideScript> NewestFirst()
	{
		for ( int i = _entries.Count - 1; i >= 0; --i )
			yield return _entries[i].Script;
	}

	/// <summary>
	/// Loads a script through <see cref="Loader"/> and puts it under this scheduler, answering its new id
	/// - or nought where nothing was loaded, which is what the engine's loader answers when it cannot open
	/// the file, and what both spawning instructions test.
	/// </summary>
	public int Spawn( string name )
	{
		var script = Loader?.Invoke( name );

		if ( script is null )
			return 0;

		var id = _lastId + 1;

		Add( id, script );

		return id;
	}

	/// <summary>
	/// Takes a script down for good - the engine's <c>FUN_00559060</c>, which is what <c>REMOVECHILD</c>
	/// calls, what the tick loop calls on a script that has stopped, and what selling a thing calls
	/// (<see cref="ParkRides.Unbind"/>).
	///
	/// <para>
	/// <b>A script does not die alone - but it dies exactly one level deep.</b> It takes its child and
	/// whatever a <c>SPAWNSOUND</c> spawned down with it, and it tells its parent it has gone. That last
	/// step is why nothing is ever left holding a child id that names nobody.
	/// </para>
	///
	/// <para>
	/// <b>One level, and no further.</b> The engine resolves the two it owns and then calls the flat
	/// destructor on them - <c>FUN_00558500</c>, which never reads their own child, parent or sound
	/// fields. So <b>a grandchild is not destroyed</b>: it survives with a parent id naming a script that
	/// no longer exists, and the dying child never performs its own parent fixup. That is reproduced
	/// rather than tidied into a recursion, and it is dormant in shipped content - of the 48 files the
	/// corpus spawns, not one contains a spawning instruction itself, so nothing shipped has a grandchild
	/// at all.
	/// </para>
	///
	/// <para>
	/// <b>One engine defect is reproduced and one is not.</b> The parent's child slot is cleared without
	/// checking that it still names the script that is dying, so a parent that has spawned a replacement
	/// loses hold of it - reproduced, because no shipped script can reach it: all four that spawn inside a
	/// loop run <c>REMOVECHILD</c> first. The other is a null dereference - where the parent id names
	/// nobody the engine writes through the null it just failed to find - and reproducing a crash would be
	/// a reading of the bytes rather than of the engine.
	/// </para>
	/// </summary>
	public bool Destroy( int id )
	{
		var at = _entries.FindIndex( entry => entry.Id == id );

		if ( at < 0 )
			return false;

		var entry = _entries[at];

		_entries.RemoveAt( at );

		Release( entry.Script );
		TakeDown( entry.Script );

		return true;
	}

	/// <summary>
	/// What the flat destructor does for the script itself, short of its relations: a script that was
	/// holding the music down lets it back up as it dies. <b>That is the only thing that ever un-mutes
	/// it</b>, since no opcode clears the setting and no shipped script passes nought to <c>DIPMUSIC</c>.
	/// </summary>
	private void Release( RideScript script )
	{
		if ( script.DippedMusic )
			MusicDip = 0;
	}

	/// <summary>
	/// What dying costs a script's relations, once it is already out of the list.
	///
	/// <para>
	/// <b>The sound script goes first</b>, then the child, then the parent is told - the engine's own
	/// order, three structurally identical arms at <c>0x55924b</c>, <c>0x55928c</c> and <c>0x5592cd</c>.
	/// Neither of the two it takes with it gets this treatment in turn: they are removed flat, which is
	/// what stops a grandchild being reached and is the whole of the difference from a recursion.
	/// </para>
	/// </summary>
	private void TakeDown( RideScript script )
	{
		// A script that dies mid-scream must not leave the voice behind: it loops, and a looping voice
		// holds its effect until it is released, so the next ride to scream would find it taken. The
		// engine's own teardown clears +0xd0 for the same reason.
		if ( script.Screaming )
			ParkAudio.Current?.StopScream( script.Id );

		if ( script.SoundChildId != 0 )
			RemoveFlat( script.SoundChildId );

		if ( script.ChildId != 0 )
			RemoveFlat( script.ChildId );

		script.ChildId = 0;
		script.SoundChildId = 0;

		if ( script.ParentId == 0 )
			return;

		var parent = Find( script.ParentId );

		if ( parent is not null )
			parent.ChildId = 0;
	}

	/// <summary>
	/// Takes one script out and does nothing else - the engine's <c>FUN_00558500</c>, which frees a
	/// script's own storage and unlinks it without ever looking at what it was related to.
	/// </summary>
	private void RemoveFlat( int id )
	{
		var at = _entries.FindIndex( entry => entry.Id == id );

		if ( at < 0 )
			return;

		var script = _entries[at].Script;

		_entries.RemoveAt( at );

		Release( script );
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

		// The engine drops a script whose program counter has gone negative, and what it calls to do it is
		// the same teardown REMOVECHILD runs - FUN_00559060, at the foot of FUN_005516b0. So a script that
		// stops takes its child down with it rather than leaving it running with nobody holding it.
		while ( true )
		{
			var at = _entries.FindIndex( entry => !entry.Script.Running );

			if ( at < 0 )
				break;

			var entry = _entries[at];

			_entries.RemoveAt( at );
			++Finished;

			Release( entry.Script );
			TakeDown( entry.Script );
		}
	}

	private readonly record struct Entry( int Id, RideScript Script );
}
