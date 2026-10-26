namespace OpenTPW;

/// <summary>
/// A park as it is being <i>played</i>, which is a different thing from the file it was loaded out of.
///
/// <para>
/// <see cref="ParkWorld"/> describes a <b>file</b> and is deliberately immutable - it says what the save
/// held, and nothing that happens afterwards may change it. This is the other half: the numbers and the
/// cells that a running park moves. It is seeded from the save once, when the park opens, and owned by
/// the <see cref="Level"/>.
/// </para>
/// <para>
/// <b>It holds what a running park moves and its file cannot</b> - among them the balance, the gate's
/// takings and the visitor count, each cell's litter and occupants, the object chain, the queues, and
/// each object's takings, costs and nominee.
/// </para>
/// <para>
/// <b>What is not here: a dirt field of its own.</b> A toilet's dirt is its State of repair, which use lowers in
/// the park's own record (<see cref="ParkRideOperation.WearByUse"/>) and <see cref="IsDirty"/> reads. What a
/// built path writes is kept, seeded from the save, even where the original's reader is not built: each object's
/// takings and costs are read by the park file's writer and the censuses, and the bank's red tick by
/// <see cref="MonthsInTheRed"/> and the console. A ride's operation reads the per-object
/// nominee and the gate reads the cells' occupant lists; the cells' litter is read by the advisor's
/// <see cref="LitterShare"/> and by <see cref="LitteredCells"/>, which only tests call, and nothing in play drops or sweeps
/// it, because the guests' drop and the handyman's litter search are not built (<see cref="StaffBehaviour"/>).
/// </para>
/// </summary>
public sealed class ParkState
{
	/// <summary>
	/// One cell as the running park holds it. Only the three fields anything can change are here: the rest
	/// of a cell - its type, its tile, its neighbours - is scenery the save describes and play does not
	/// move, so it stays on <see cref="ParkWorld.MapCell"/> and is not copied.
	/// </summary>
	public struct RuntimeCell
	{
		/// <summary>How much has been dropped here - <c>mLitter</c>.</summary>
		public int Litter;

		/// <summary>The handyman who has claimed this cell's litter, or nought - <c>mLitterCollector</c>.</summary>
		public ushort LitterCollector;

		/// <summary>The thing standing on this cell - see <see cref="ParkWorld.MapCell.Occupant"/>.</summary>
		public ushort Occupant;
	}

	private readonly RuntimeCell[] _cells;

	private readonly short[] _effects = new short[ParkWorld.MapSize * ParkWorld.MapSize * ParkWorld.EffectWords];

	/// <summary>The eight region effects' numbers: the park's balance's, or <c>Standard.sam</c>'s until one is given.</summary>
	public ParkRegionEffects RegionEffects { get; init; } = ParkRegionEffects.Standard;

	/// <summary>
	/// Every cell's region effects as the park runs, <see cref="ParkWorld.EffectWords"/> words a cell in the map's
	/// order: the file's, with what a thing bought, sold, dirtied or cleaned since has stamped and taken off
	/// (the world's <c>+0x2d8 + 0x1b1104</c>). The litter's are the file's and do not move; an entertainer's and a guard's move with them (<see cref="ParkPeople.MoveEffect"/>).
	/// </summary>
	public IReadOnlyList<short> Effects => _effects;

	/// <summary>A copy of <see cref="Effects"/>, for a caller that will stamp on it.</summary>
	public short[] EffectsCopy() => [.. _effects];

	/// <summary>One cell's five words: happiness, illness, hunger, security, attraction.</summary>
	public ReadOnlySpan<short> EffectsAt( int x, int y )
		=> _effects.AsSpan( ((y * ParkWorld.MapSize) + x) * ParkWorld.EffectWords, ParkWorld.EffectWords );

	/// <summary>Stamps a region effect round a cell - <c>FUN_004d8440</c>.</summary>
	public void StampEffect( int effect, int x, int y ) => RegionEffects.Stamp( _effects, effect, x, y );

	/// <summary>Takes a region effect off round a cell - <c>FUN_004d8460</c>.</summary>
	public void UnstampEffect( int effect, int x, int y ) => RegionEffects.Stamp( _effects, effect, x, y, off: true );

	/// <summary>
	/// The park being played, or null outside one - the arrangement <see cref="ParkObjects.Current"/>
	/// and <see cref="ParkPeople.Current"/> use. The ground
	/// needs it to draw cells a player has changed, which it cannot ask the save for. Cleared as the park
	/// ends - see <see cref="ForgetCurrent"/>.
	/// </summary>
	public static ParkState? Current { get; private set; }

	/// <summary>
	/// Lets go of <see cref="Current"/>, which holds the park's save and would otherwise keep a left park in memory
	/// through the lobby. The last of <see cref="Level.Unload"/>, through <see cref="Level.ForgetRunningPark"/>.
	/// </summary>
	internal static void ForgetCurrent() => Current = null;

	/// <summary>The initial world this was seeded from, for the cells nothing has changed - see <see cref="Record"/>.</summary>
	private readonly IParkInitialState? _park;

	/// <summary>
	/// The cells a player has changed since the park was loaded, by packed index. Sparse on purpose:
	/// a park is 16,384 cells and a session changes a handful, so this holds only the difference.
	/// </summary>
	private readonly Dictionary<int, ParkWorld.MapCell> _records = [];

	/// <summary>
	/// What a cell IS now - built on, path, queue, bare ground - which is a different question from
	/// <see cref="CellAt"/>, the three fields a running park moves.
	///
	/// <para>
	/// <b><see cref="ParkWorld"/> describes a file and may never be written to</b>, so a cell a player
	/// builds on cannot be recorded there. This answers the save until something changes a cell and
	/// that change afterwards, which is the whole of what makes building possible.
	/// </para>
	/// </summary>
	public ParkWorld.MapCell Record( int x, int y )
	{
		if ( !OnMap( x, y ) )
			return default;

		return _records.TryGetValue( (y * ParkWorld.MapSize) + x, out var changed )
			? changed
			: _park?.CellAt( x, y ) ?? default;
	}

	/// <summary>The initial world this was seeded from, so that a caller can tell whether this overlay is the one
	/// describing the park it has in hand - see <see cref="CellFor"/>.</summary>
	public IParkInitialState? Park => _park;

	/// <summary>
	/// A cell as the RUNNING park holds it: the player's changes first, and the file for everything
	/// nobody has touched. <b>The one statement of that rule</b>, for the reason
	/// <see cref="ParkPaths.IsPath"/> is shared rather than repeated - everything that reads a cell
	/// must get the same answer or the drawing, the pathing and the queue walk disagree about the same
	/// square.
	/// </summary>
	/// <remarks>
	/// <b>It consults <see cref="Current"/> only when that overlay describes THIS park, and the guard is
	/// load-bearing rather than defensive.</b> <see cref="Current"/> is whichever overlay was built last
	/// until a park ends, so an overlay built for another park - or the two-fact overlay a test constructs,
	/// whose <see cref="Park"/> is null - would otherwise answer for this one. Its <see cref="Record"/> falls
	/// back to <c>default</c> when it has no save, and a default cell is <b>type 0</b>: every cell in the
	/// park would read as bare ground, which silently rewrites every route, every queue walk and every
	/// edge test rather than failing. Tying the overlay to the park it was seeded from makes a mismatch
	/// answer from the file.
	/// </remarks>
	public static ParkWorld.MapCell CellFor( IParkInitialState? park, int x, int y )
	{
		if ( park == null )
			return default;

		return Current is { } state && ReferenceEquals( state.Park, park )
			? state.Record( x, y )
			: park.CellAt( x, y );
	}

	/// <summary>Records what a cell has become. The park's surfaces have to be rebuilt to show it -
	/// see <see cref="ParkSurfaces.Rebuild"/>, which lays all three together.</summary>
	public void SetRecord( int x, int y, ParkWorld.MapCell cell )
	{
		if ( !OnMap( x, y ) )
			return;

		StampTypeWrite( x, y, Record( x, y ).Type, cell.Type );

		_records[(y * ParkWorld.MapSize) + x] = cell;
	}

	/// <summary>
	/// The shared counter, <c>[0x007cdb98]</c>: <c>FUN_004d8c50</c> adds one and answers it. A logical clock, never
	/// reset or saved, whose values order the block stamps, a route's stamp and a guest's stranded stamp against
	/// one another (<c>docs/exe/ride-operation.md</c>, "The stranded bookkeeping").
	/// </summary>
	public static uint NextCounter() => ++_counter;

	/// <summary>The counter's last value, for a census.</summary>
	public static uint Counter => _counter;

	private static uint _counter;

	/// <summary>How many blocks a side the stamps have, one a 16 x 16 cells and one over - world <c>+0x1b02d8</c>.</summary>
	public const int BlocksASide = 33;

	private readonly uint[] _blockStamps = new uint[BlocksASide * BlocksASide];

	/// <summary>
	/// Writes a fresh counter value into a cell's 16 x 16 block - <c>FUN_004d8c50</c> then <c>FUN_004d8c60</c>.
	/// A walker re-plans when its block's stamp is newer than its route, and a stranded guest is freed by one.
	/// </summary>
	public void StampBlock( int x, int y )
	{
		if ( OnMap( x, y ) )
			_blockStamps[((y >> 4) * BlocksASide) + (x >> 4)] = NextCounter();
	}

	/// <summary>A cell's block stamp by cell - <c>FUN_004d8cd0</c> of the two block indices.</summary>
	public uint BlockStamp( int x, int y )
		=> OnMap( x, y ) ? _blockStamps[((y >> 4) * BlocksASide) + (x >> 4)] : 0;

	/// <summary>
	/// A cell's block stamp by packed id - <c>FUN_004d8ca0</c>, which takes one off the id's sixteen bits and reads
	/// <c>((id &gt;&gt; 4) &amp; 7) + (id &gt;&gt; 11) × 33</c>, so an id off the map's edge reads a neighbouring block.
	/// </summary>
	public uint BlockStampOf( int cellId )
	{
		var id = (uint)(cellId - 1) & 0xffff;

		return _blockStamps[((id >> 4) & 7) + ((id >> 11) * BlocksASide)];
	}

	/// <summary>
	/// The block stamp a cell's change of type leaves: after writing bare ground, path or queue
	/// (<c>FUN_005346d0</c>, <c>0x005347af</c>; a queue cell made path, <c>0x00534913</c>) the original stamps the
	/// cell's block (<see cref="StampBlock"/>).
	/// </summary>
	private void StampTypeWrite( int x, int y, int was, int now )
	{
		if ( was != now && now is CellEdge.Nothing or CellEdge.Path or ParkRideChoice.QueueCellType )
			StampBlock( x, y );
	}

	/// <summary>
	/// Drops a cell's override, so that the save answers for it again - what selling a thing gives back to
	/// a cell the save records as terrain the original never builds on (<see cref="ParkBuilding.Unstamp"/>).
	/// Every other cell a sold thing stood on is written the cleared record instead, because the save's
	/// record of it still names the thing, or names whatever the player has since changed.
	/// </summary>
	public void ClearRecord( int x, int y )
	{
		if ( !OnMap( x, y ) )
			return;

		StampTypeWrite( x, y, Record( x, y ).Type, _park?.CellAt( x, y ).Type ?? 0 );

		_records.Remove( (y * ParkWorld.MapSize) + x );
	}

	/// <summary>
	/// The cells changed since the park was loaded, by their place in <see cref="ParkWorld.Cells"/>, each as
	/// <see cref="Record"/> answers it: what a park file written from this park says of its ground.
	/// </summary>
	public IReadOnlyDictionary<int, ParkWorld.MapCell> ChangedRecords => _records;

	/// <summary>How many cells have been changed since the park was loaded.</summary>
	public int ChangedCells => _records.Count;

	private readonly List<ParkWorld.CatalogueObject> _objects = [];
	private readonly Dictionary<int, RideHoardingState> _hoardings = [];
	internal RideHoardingState? HoardingFor( int thingId ) => _hoardings.GetValueOrDefault( thingId );
	internal RideHoardingState BindHoarding( int thingId ) => _hoardings.TryGetValue( thingId, out var known )
		? known : _hoardings[thingId] = new RideHoardingState();

	/// <summary>
	/// Everything standing in the park <i>now</i>, which is the save's list plus whatever has been
	/// built since and minus whatever has been sold. <see cref="ParkWorld.Objects"/> is the file's
	/// list and never moves.
	/// </summary>
	public IReadOnlyList<ParkWorld.CatalogueObject> Objects => _objects;

	/// <summary>How many of <see cref="Objects"/>' first entries are the file's: everything built since is appended after them.</summary>
	private int _fromFile;

	/// <summary>
	/// The standing objects in the original's used-thing list order (<c>[0x007cf56c]</c>, walked by <c>0x00516130</c> and
	/// <c>0x00516160</c>): a new thing heads it (<c>FUN_00516270</c>), so what was built here comes newest first, then the
	/// file's in the order it lists them (<c>docs/exe/ride-operation.md</c>, <c>FUN_00516270</c>'s row). The object
	/// windows' arrows walk it.
	/// </summary>
	public IEnumerable<ParkWorld.CatalogueObject> ObjectsInUsedOrder()
	{
		for ( var i = _objects.Count - 1; i >= _fromFile; --i )
			yield return _objects[i];

		for ( var i = 0; i < _fromFile; ++i )
			yield return _objects[i];
	}

	/// <summary>The head of the object chain - the save's <c>mFirstObject</c>, the engine's <c>+0x1da746</c>.</summary>
	private int _firstObject;

	/// <summary>Each item's standing count and first-build stamp, by item id - see <see cref="BuiltOf"/>.</summary>
	private readonly Dictionary<int, (int Standing, uint FirstBuilt)> _built = [];

	/// <summary>
	/// How many of an item stand in the park and <c>mGameTick</c> as the first of them was built - its control
	/// record's <c>+0x18</c> and <c>+0x1c</c>, seeded from the save (<see cref="ParkWorld.ObjectControl"/>) and kept
	/// as things are bought and sold. An item never built reads nought and nought. The park's worth divides by the
	/// one and ages a ride by the other (<see cref="ParkWorth"/>).
	/// </summary>
	public (int Standing, uint FirstBuilt) BuiltOf( int itemId ) => _built.GetValueOrDefault( itemId );

	/// <summary>
	/// The object constructor's two writes to the item's record (<c>FUN_004db090</c>): the stamp takes the clock
	/// only while it reads nought (<c>0x004db690</c>), so a sold item keeps its first build's and one built on
	/// tick nought takes the next build's; then the count goes one up (<c>0x004db6d0</c>).
	/// </summary>
	private void CountBuilt( int itemId )
	{
		var (standing, firstBuilt) = BuiltOf( itemId );

		_built[itemId] = (standing + 1, firstBuilt == 0 ? unchecked((uint)GameTick) : firstBuilt);
	}

	/// <summary>The object destructor's first act: the count goes one down, the stamp stays (<c>0x004dd0e1</c>).</summary>
	private void CountSold( int itemId )
	{
		var (standing, firstBuilt) = BuiltOf( itemId );

		_built[itemId] = (standing - 1, firstBuilt);
	}

	/// <summary>
	/// Each object's link to the next, by thing id - the save's <c>mNext</c>, which is the engine's
	/// thing <c>+0xc</c>.
	/// </summary>
	private readonly Dictionary<int, int> _nextObject = [];

	/// <summary>
	/// Every object in the order a guest considers them - from <c>mFirstObject</c> along each object's own
	/// <c>mNext</c>, newest first.
	/// </summary>
	/// <remarks>
	/// <b>The chain is LIVE in the original, and that is why it is kept here rather than read off the
	/// save.</b> <c>FUN_00519d80</c> links a newly built object at the <b>head</b> and has exactly one
	/// caller, the object constructor <c>FUN_004db090</c>; <c>FUN_00519dc0</c> unlinks and has exactly one,
	/// the demolish <c>FUN_004dd0a0</c>. So every object the player builds joins the chain and every one
	/// they sell leaves it, and a walk that followed <see cref="ParkWorld"/>'s copy would reach only what
	/// the file placed.
	/// <para>
	/// <b>Newest first is observable rather than cosmetic.</b> <c>FUN_004fcb10</c> keeps the later candidate
	/// on a tie only when <c>mGameTick &amp; 1</c>, so where an object sits in this walk decides ties between
	/// equally good ones - see <see cref="ParkRideChooser"/>, which reproduces that rule.
	/// </para>
	/// <para>
	/// The walk is bounded by the object count for the same reason <see cref="ParkRideChoice.Offerable"/>
	/// bounds its own: a chain that came back on itself would otherwise hang instead of ending.
	/// </para>
	/// </remarks>
	public IEnumerable<ParkWorld.CatalogueObject> ObjectsInChainOrder()
	{
		var byId = new Dictionary<int, ParkWorld.CatalogueObject>( _objects.Count );

		foreach ( var placed in _objects )
			byId[placed.ThingId] = placed;

		var seen = 0;

		for ( var id = _firstObject; id != 0 && seen <= byId.Count; ++seen )
		{
			if ( !byId.TryGetValue( id, out var placed ) )
				break;

			yield return placed;

			id = _nextObject.GetValueOrDefault( id );
		}
	}

	/// <summary>
	/// The object the mechanics' search last began on, a thing id, or nought - the mechanics' HQ's
	/// <c>mNextObject</c> (<see cref="ParkWorld.MechanicCursor"/>).
	/// </summary>
	public int MechanicCursor { get; private set; }

	/// <summary>
	/// The objects one search for a ride to fix looks at, in its order - the walk of <c>FUN_004daa90</c>
	/// (<c>docs/exe/ride-operation.md</c>, "The mechanic's repair"). <b>Each search begins one object further
	/// down the chain than the last</b>: <see cref="MechanicCursor"/> steps to the object after the one it
	/// names, or to the chain's head where it names none still in the chain, and the walk runs from there right
	/// round the chain. <b>A search whose step runs off the chain's end looks at nothing</b>: the cursor is left
	/// nought and the next search begins at the head.
	/// </summary>
	public IReadOnlyList<ParkWorld.CatalogueObject> ObjectsFromTheMechanicsCursor()
	{
		var chain = ObjectsInChainOrder().ToList();
		var named = MechanicCursor == 0 ? -1 : chain.FindIndex( placed => placed.ThingId == MechanicCursor );
		var start = named < 0 ? 0 : named + 1;

		if ( start >= chain.Count )
		{
			MechanicCursor = 0;

			return [];
		}

		MechanicCursor = chain[start].ThingId;

		return [.. chain.Skip( start ), .. chain.Take( start )];
	}

	/// <summary>
	/// The next free thing id. <b>Objects and people share one numbering, and this is the only thing
	/// that hands ids out of it.</b>
	///
	/// <para>
	/// It is the head of the original's free list (<c>FUN_00516270</c>): after a load, every id no thing of the file
	/// holds, in rising order (<c>FUN_005179c0</c>), so the file's holes first and then one past its highest; and a
	/// thing deleted goes back on at the head, once the sweep it was deleted in has ended
	/// (<see cref="DeleteThing"/>, <see cref="FreeDeletedThings"/>). <c>docs/exe/ride-operation.md</c>, "Where a
	/// ride's turn comes from".
	/// </para>
	/// <para>
	/// <b>With every id up to <see cref="LastThingId"/> held it answers nought, and the caller makes nothing.</b> The
	/// original's allocator answers nought too, but its constructor carries on and links thing nought in through the
	/// table's empty node 0, a write to address 8 (<c>FUN_004d91f0</c>): the game falls over. DEVIATION: here nothing
	/// is made and the park goes on, until a delete frees an id (<c>docs/exe/ride-operation.md</c>, the
	/// <c>FUN_00516270</c> row).
	/// </para>
	/// </summary>
	public int NextThingId()
	{
		SeedFreeIds();

		int thingId;

		if ( _freeIds.First is { } head )
		{
			_freeIds.RemoveFirst();
			thingId = head.Value;
		}
		else if ( _nextThingId > LastThingId )
		{
			// Said once a time it fills, as each thing tick of an unloading vehicle asks again.
			if ( !_saidFull )
				Log.Warning( $"Park: the thing table is full, every id to {LastThingId} held, so nothing is made" );

			_saidFull = true;

			return 0;
		}
		else
		{
			thingId = _nextThingId++;
		}

		_saidFull = false;

		// The node goes to the head of the used list as it is taken (FUN_00516270), so the newest made heads it.
		_madeAt[thingId] = ++_makes;

		return thingId;
	}

	/// <summary>
	/// When each thing made since the load was made, the newer the greater: the order the original's used list holds
	/// them in from its head, which its writer walks (<c>docs/exe/saves.md</c>, "OpenTPW's writer, an id given out again").
	/// </summary>
	public IReadOnlyDictionary<int, long> MadeAt => _madeAt;

	/// <summary>
	/// Every id deleted since the load, whatever holds it now. One the file held names a thing that is gone from it:
	/// a thing made on the id since is a new one, built from nought by its constructor (<c>FUN_0050afe0</c>).
	/// </summary>
	public IReadOnlySet<int> DeletedSinceLoad => _deletedSinceLoad;

	private bool _saidFull;
	private readonly Dictionary<int, long> _madeAt = [];
	private readonly HashSet<int> _deletedSinceLoad = [];
	private long _makes;

	/// <summary>
	/// Marks a thing deleted (<c>FUN_0050b780</c>): its id is pushed onto the head of the deleted list
	/// (<c>FUN_00516310</c>, <c>FUN_0051ad40</c>) and is not free until <see cref="FreeDeletedThings"/>.
	/// </summary>
	public void DeleteThing( int thingId )
	{
		if ( thingId <= 0 )
			return;

		SeedFreeIds();
		_deletedIds.Push( thingId );
		_deletedSinceLoad.Add( thingId );
	}

	/// <summary>
	/// The sweep's end (<c>FUN_00516380</c>, <c>0x00516654</c>..<c>0x00516685</c>): each deleted id is popped from the head of the deleted
	/// list (<c>FUN_0051ad90</c>) and freed onto the head of the free list (<c>FUN_00516200</c>). The two heads cancel,
	/// so of the things deleted in one sweep the first deleted is the first given out again, ahead of any older free id.
	/// </summary>
	public void FreeDeletedThings()
	{
		while ( _deletedIds.TryPop( out var thingId ) )
		{
			_freeIds.AddFirst( thingId );
			Log.Info( $"Park: thing {thingId} freed, the free list's head" );
		}
	}

	/// <summary>The last id the original's thing table holds: 0x2800 nodes, nought never a thing.</summary>
	public const int LastThingId = 0x27ff;

	/// <summary>
	/// Builds the free list as the reader leaves it, on first use rather than at load, so a state never bound to a
	/// park still answers: every id under the highest used that no thing holds, rising, and past them a count.
	/// </summary>
	private void SeedFreeIds()
	{
		if ( _nextThingId != 0 )
			return;

		var used = UsedThingIds();
		var highest = used.Count == 0 ? 0 : used.Max();

		for ( var id = 1; id < highest; ++id )
		{
			if ( !used.Contains( id ) )
				_freeIds.AddLast( id );
		}

		_nextThingId = highest + 1;
	}

	private int _nextThingId;
	private readonly LinkedList<int> _freeIds = new();
	private readonly Stack<int> _deletedIds = new();

	/// <summary>The free list's ids from its head, for the console: <paramref name="count"/> at most.</summary>
	public IEnumerable<int> FreeThingIds( int count )
	{
		SeedFreeIds();

		var past = Math.Max( 0, Math.Min( count - _freeIds.Count, LastThingId + 1 - _nextThingId ) );

		return _freeIds.Take( count ).Concat( Enumerable.Range( _nextThingId, past ) );
	}

	/// <summary>The ids deleted this sweep and not yet freed, newest first.</summary>
	public IReadOnlyCollection<int> DeletedThingIds => _deletedIds;

	/// <summary>Every id the file and the objects built before the first id was asked for hold.</summary>
	private HashSet<int> UsedThingIds()
	{
		var used = new HashSet<int>();

		foreach ( var placed in _objects )
			used.Add( placed.ThingId );

		if ( _park != null )
		{
			foreach ( var thing in _park.Things )
				used.Add( thing.ThingId );
		}

		used.Remove( 0 );

		return used;
	}

	/// <summary>
	/// Adds something built, and links it into the object chain at the <b>head</b> - the original's
	/// <c>FUN_00519d80</c>, whose only caller is the object constructor - and stamps the analyser as that
	/// constructor's tail does (<see cref="StampObjectBuilt"/>).
	/// </summary>
	/// <remarks>
	/// Without the link the thing stands, draws and runs its script, and is still never offered to
	/// anybody: the walk a guest's choice makes starts at <c>mFirstObject</c>, so an object in no chain
	/// cannot be reached however the list beside it is built.
	/// </remarks>
	public void AddObject( ParkWorld.CatalogueObject placed )
	{
		_objects.Add( placed );
		CountBuilt( placed.CatalogueId );
		_rings[placed.ThingId] = new ParkObjectRings();

		// The constructor zeroes its totals, its queue's head and its nominee (FUN_004db090: +0x180 and +0x184 at
		// 0x004db163, mFirstInQ at 0x004db0fe, mPersonBeingLoaded at 0x004db132): nothing passes to it from a thing
		// that held its id before.
		_takings.Remove( placed.ThingId );
		_costs.Remove( placed.ThingId );
		_queueHead.Remove( placed.ThingId );
		_beingLoaded.Remove( placed.ThingId );

		_nextObject[placed.ThingId] = _firstObject;
		_firstObject = placed.ThingId;

		StampObjectBuilt();
	}

	/// <summary>
	/// Takes something sold out of the park and unlinks it from the object chain - the original's
	/// <c>FUN_00519dc0</c>, whose only caller is the demolish. Answers whether it was there.
	/// </summary>
	public bool RemoveObject( int thingId )
	{
		var at = _objects.FindIndex( placed => placed.ThingId == thingId );

		if ( at < 0 )
			return false;

		CountSold( _objects[at].CatalogueId );
		_objects.RemoveAt( at );

		if ( at < _fromFile )
			--_fromFile;
		_hoardings.Remove( thingId );
		_rings.Remove( thingId );
		_takings.Remove( thingId );
		_costs.Remove( thingId );
		_queueHead.Remove( thingId );
		_beingLoaded.Remove( thingId );
		_queuesInvalidated.Remove( thingId );
		Unlink( thingId );

		return true;
	}

	/// <summary>
	/// What an object keeps of its days (<see cref="ParkObjectRings"/>). An object the state was never told of -
	/// a test's - starts empty, as a thing just built does.
	/// </summary>
	public ParkObjectRings RingsFor( int thingId )
	{
		if ( !_rings.TryGetValue( thingId, out var rings ) )
			_rings[thingId] = rings = new ParkObjectRings();

		return rings;
	}

	/// <summary>
	/// The day's change for every object - message <c>0xb</c>, which the calendar sends once a world tick at most,
	/// after every thing's turn, to each object in ascending thing id (<c>0x004f8321</c>, <c>FUN_004dd320</c>).
	/// </summary>
	/// <remarks>
	/// The original sends it to the objects its saved listener sets name; this rolls every object the park holds,
	/// which is the same wherever a save lists them all.
	/// </remarks>
	public void RollTheDay()
	{
		foreach ( var thingId in _rings.Keys.Order() )
			_rings[thingId].Roll();

		Log.Info( $"The day's change: {_rings.Count} objects' rings rolled on {GameCalendar.Now:yyyy-MM-dd}" );
	}

	/// <summary>
	/// Takes one thing id out of the object chain, moving the head where it was the head and patching
	/// its predecessor's link where it was not.
	/// </summary>
	private void Unlink( int thingId )
	{
		var next = _nextObject.GetValueOrDefault( thingId );

		_nextObject.Remove( thingId );

		if ( _firstObject == thingId )
		{
			_firstObject = next;
			return;
		}

		// Bounded by the link count for the same reason the walk is: a chain that came back on itself
		// would otherwise spin here rather than end.
		var steps = 0;

		for ( var id = _firstObject; id != 0 && steps <= _nextObject.Count; ++steps )
		{
			if ( !_nextObject.TryGetValue( id, out var following ) )
				return;

			if ( following == thingId )
			{
				_nextObject[id] = next;
				return;
			}

			id = following;
		}
	}

	/// <summary>
	/// Puts a changed object back in place of the one carrying its thing id, and answers whether there
	/// was one to replace.
	/// </summary>
	/// <remarks>
	/// <see cref="ParkWorld.CatalogueObject"/> is a record, so anything that changes one - the ride
	/// window's three sliders - builds a new one from the old and hands it here. Replacing in place
	/// keeps the list's order, which is what the censuses and the cycle arrows walk.
	/// </remarks>
	public bool ReplaceObject( ParkWorld.CatalogueObject placed )
	{
		var at = _objects.FindIndex( candidate => candidate.ThingId == placed.ThingId );

		if ( at < 0 )
			return false;

		_objects[at] = placed;

		return true;
	}

	/// <summary>
	/// The file's objects as they run, for a park file's writer (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the
	/// objects"): each object <paramref name="loaded"/> holds that still stands, as the park's own record has it,
	/// with its queue's head, the guest it is loading, its two totals and its rings; and its queue's back cell and
	/// size as measured since the queue was last edited, the file's where it was not
	/// (<see cref="ParkRideChoice.QueueCellsFor"/>). An object sold since the load is left out, and counted: its
	/// record is still the file's. So is one whose id was deleted since, whatever holds the id now.
	/// </summary>
	internal List<ParkWorld.CatalogueObject> WrittenObjects( ParkWorld loaded )
		=> WrittenObjects( loaded, null, out _, out _ );

	/// <summary>
	/// <see cref="WrittenObjects(ParkWorld)"/>, with the objects bought and sold since the load
	/// (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a thing bought and a thing sold"): in <paramref name="made"/>
	/// each object the park holds and the file does not, as it runs, the newest made first; in <paramref name="gone"/>
	/// the id of each object of the file's that no longer stands. <b>An id deleted since the load names a new thing
	/// wherever it is held again</b>: the file's object on it is gone, and an object on it now is made
	/// (<c>docs/exe/saves.md</c>, "OpenTPW's writer, an id given out again").
	///
	/// <para>
	/// <b>Only a thing <paramref name="writable"/> passes is written so.</b> Any other (a track ride of a
	/// BumperType with no record here, a tracked ride) is counted and left as it was: one sold stays in the file, one bought is not in it.
	/// With no test given nothing is made or taken out.
	/// </para>
	/// </summary>
	internal List<ParkWorld.CatalogueObject> WrittenObjects( ParkWorld loaded, Func<ParkWorld.CatalogueObject, bool>? writable,
		out List<ParkWorld.CatalogueObject> made, out HashSet<int> gone )
	{
		var written = new List<ParkWorld.CatalogueObject>( loaded.Objects.Count );
		var held = new HashSet<int>();
		var kept = new HashSet<int>();

		made = [];
		gone = [];

		// A thing bought has had its queue rewalked by the placer once the first cell was down (FUN_004de1f0 at
		// 0x00529890), so its pair is read off the map as an edited queue's is.
		ParkWorld.CatalogueObject AsItRuns( ParkWorld.CatalogueObject now )
		{
			var (back, cells) = QueueWasInvalidated( now.ThingId )
				? ParkRideChoice.QueueCellsFor( _park, now )
				: (now.BackOfQueue, now.QueueSizeInCells);

			return now with
			{
				FirstInQueue = (ushort)FirstInQueue( now.ThingId ),
				PersonBeingLoaded = (ushort)PersonBeingLoaded( now.ThingId ),
				BackOfQueue = (ushort)back,
				QueueSizeInCells = cells,
				TotalTakings = TakingsFor( now.ThingId ),
				TotalCosts = CostsFor( now.ThingId ),
				Rings = RingsFor( now.ThingId ).Written(),
			};
		}

		// A file's object whose id was deleted is gone whatever holds the id now: that is a new thing.
		foreach ( var file in loaded.Objects )
		{
			if ( _deletedSinceLoad.Contains( file.ThingId ) || !TryObject( file.ThingId, out var now ) )
			{
				if ( writable?.Invoke( file ) == true )
					gone.Add( file.ThingId );
				else
				{
					Unimplemented.Report( "SAVE_PARK_OBJECT_SOLD" );
					held.Add( file.ThingId );
				}

				continue;
			}

			held.Add( file.ThingId );
			kept.Add( file.ThingId );
			written.Add( AsItRuns( now ) );
		}

		if ( writable == null )
			return written;

		// The made, newest first, as the original's used list holds them from its head.
		foreach ( var now in _objects.Where( thing => !kept.Contains( thing.ThingId ) )
			.OrderByDescending( thing => _madeAt.GetValueOrDefault( thing.ThingId ) ).ThenByDescending( thing => thing.ThingId ) )
		{
			// The file keeps a sold thing it cannot write without, so a thing made on its id is not written. A deviation: the
			// original writes it and drops the sold object. What is written still names the id as the kept object (Level.WritePark).
			if ( held.Contains( now.ThingId ) )
				Unimplemented.Report( "SAVE_PARK_ID_KEPT_FOR_A_SOLD_THING" );
			else if ( writable( now ) )
				made.Add( AsItRuns( now ) );
			else
				Unimplemented.Report( "SAVE_PARK_OBJECT_BOUGHT" );
		}

		return written;
	}

	/// <summary>Every item's standing count and first-build stamp as the park runs them, for a park file's object controls.</summary>
	internal IReadOnlyDictionary<int, (int Standing, uint FirstBuilt)> BuiltItems => _built;

	/// <summary>
	/// The State of repair below which a toilet is dirty - 25.0 (<c>0x00700550</c>), which the original holds the
	/// float's truncated low byte against.
	/// </summary>
	public const int DirtyBelow = 25;

	/// <summary>
	/// Whether a thing is a dirty toilet - <c>FUN_004e0390</c>: the toilet bit, and the State of repair
	/// (<c>+0x44</c>) truncated to a byte below <see cref="DirtyBelow"/>. Hand it the park's own record
	/// (<see cref="TryObject"/>), which use lowers; the save's never moves.
	/// </summary>
	public static bool IsDirty( ParkWorld.CatalogueObject thing )
		=> thing.IsToilet && ((int)thing.StateOfRepair & 0xff) < DirtyBelow;

	/// <summary>The placed object with this thing id, if the park has one.</summary>
	public bool TryObject( int thingId, out ParkWorld.CatalogueObject placed )
	{
		foreach ( var candidate in _objects )
		{
			if ( candidate.ThingId == thingId )
			{
				placed = candidate;
				return true;
			}
		}

		placed = default;

		return false;
	}

	/// <summary>
	/// Seeded from the save. A null park gives an empty, open one - the same answer
	/// <see cref="PeepBehaviour"/> already gives for a null park, and for the same reason: a park with
	/// nothing loaded is not a park whose gates are shut.
	/// </summary>
	public ParkState( IParkInitialState? park )
	{
		_park = park;
		Current = this;

		// The file's tags go back on the aerial, each with its whole life before it (FUN_0048ef70). A deviation:
		// the original's lives run from the read, inside its load; these from here, behind the loading screen.
		if ( park?.Save?.Tags is { } tags )
			Tags.Restore( tags.Saved, Time.WallMilliseconds );

		Balance = park?.Economy?.Balance ?? 0;
		AdmissionFee = park?.Economy?.AdmissionFee ?? 0;

		// The bank's constructor starts withdrawals on and the rest at nought (FUN_004cf7c0).
		WithdrawalsEnabled = park?.Economy?.WithdrawalsEnabled ?? 1;
		LastBalance = park?.Economy?.LastBalance ?? 0;
		TurnEnteredRed = park?.Economy?.TurnEnteredRed ?? 0;
		ProfitThisYear = park?.Economy?.ProfitThisYear ?? 0;
		BatchBalance = park?.Economy?.BatchBalance ?? 0;

		if ( park?.Economy?.Loans is { } loans )
		{
			for ( var i = 0; i < _loans.Length && i < loans.Count; ++i )
				_loans[i] = loans[i];
		}

		VisitorsToDate = park?.NumberOfVisitorsToDate ?? 0;
		ParkIsClosed = park is not null && park.ParkClosed != 0;
		GameTick = park?.GameTick ?? 0;
		RandomSeed = unchecked((uint)(park?.RandomSeed ?? 0));

		_cells = new RuntimeCell[ParkWorld.MapSize * ParkWorld.MapSize];

		// Each track ride back in its saved slot with its sections, as the track-rides module's loader puts them.
		// Said out loud when a module will not read, because its track rides then score as stale handles and its
		// coasters go unfound, which looks like the game's own doing.
		TrackRides = new ParkTrackRideTable( park?.Save?.TrackRides );

		if ( park?.Save?.TrackRides.Problem is { } kart )
			Log.Warning( $"Park: the track-rides module would not read ({kart}); every saved track ride has no track" );

		if ( park?.Save?.Coasters.Problem is { } saoc )
			Log.Warning( $"Park: the coasters module would not read ({saoc}); every saved coaster is counted and offered" );

		if ( park == null )
			return;

		if ( park.Save?.CellEffects is { Count: > 0 } effects && effects.Count == _effects.Length )
		{
			for ( var i = 0; i < _effects.Length; ++i )
				_effects[i] = effects[i];
		}

		for ( var i = 0; i < _cells.Length && i < park.Cells.Count; ++i )
		{
			var cell = park.Cells[i];

			_cells[i] = new RuntimeCell
			{
				Litter = cell.Litter,
				LitterCollector = cell.LitterCollector,
				Occupant = cell.Occupant
			};
		}

		if ( park.Save is { } save )
			SeedCellLists( save );

		// Everything the file placed, copied so that what is built and sold afterwards moves here rather
		// than in ParkWorld, which describes a file.
		_objects.AddRange( park.Objects );
		_fromFile = _objects.Count;

		// The save's word on how many of each item stand and when the first was built: the file's objects are
		// counted in it already, so none of them is counted here.
		foreach ( var record in park.ObjectControlRecords )
			_built.TryAdd( record.ItemId, (record.Standing, record.FirstBuilt) );

		// And the chain that threads them, which the original keeps live and this one now does too - see
		// ObjectsInChainOrder. Seeded from the save's own head and links, so a park straight out of the
		// file is considered in exactly the order the file's chain gives.
		_firstObject = park.FirstObject;
		MechanicCursor = park.Save?.MechanicCursor ?? 0;
		LastStaffHired = park.Save?.LastStaffHired ?? ParkWorld.StaffState.FileTimeOf( GameCalendar.Epoch );
		LastObjectBuilt = park.Save?.LastObjectBuilt ?? ParkWorld.StaffState.FileTimeOf( GameCalendar.Epoch );
		ParkLastOpened = park.Save?.ParkLastOpened ?? ParkWorld.StaffState.FileTimeOf( GameCalendar.Epoch );
		RecentlyResearched = new( park.Save?.RecentlyResearched );
		MonthBalance = park.Save?.MonthBalance?.Copy() ?? new();
		MonthParkValue = park.Save?.MonthParkValue?.Copy() ?? new();
		MonthGuestsInside = park.Save?.MonthGuestsInside?.Copy() ?? new();
		MonthVisitors = park.Save?.MonthVisitors?.Copy() ?? new();
		MonthRides = park.Save?.MonthRides?.Copy() ?? new();
		MonthHappiness = park.Save?.MonthHappiness?.Copy() ?? new();
		MonthMeanStay = park.Save?.MonthMeanStay?.Copy() ?? new();
		MonthParkScore = park.Save?.MonthParkScore?.Copy() ?? new();
		LeaverStays = park.Save?.LeaverStays?.Copy() ?? new( ParkMonthRing.Stays );
		ArrivalLoads = park.Save?.ArrivalLoads?.Copy() ?? new( ParkMonthRing.Loads );
		MonthArrivals = park.Save?.MonthArrivals?.Copy() ?? new();
		LifetimeVisitors = park.Save?.LifetimeVisitors ?? 0;
		MostPaidForTicket = park.Save?.MostPaidForTicket ?? 0;
		// The original's load reads the month's two and never the third (its read path calls the writer at 0x004c719a),
		// leaving it as its load factory's unzeroed memory had it (FUN_005179c0); nought here, the constructor's.
		MisbehavingKids = park.Save?.MisbehavingKids is { } kids ? kids with { Ever = 0 } : default;
		MonthCosts = park.Save?.MonthCosts?.Copy() ?? new();
		MonthStaffCosts = park.Save?.MonthStaffCosts?.Copy() ?? new();
		MonthCashIn = park.Save?.MonthCashIn?.Copy() ?? new();
		MonthTraining = park.Save?.MonthTraining?.Copy() ?? new();
		MonthGateTakings = park.Save?.MonthGateTakings?.Copy() ?? new();
		MonthShopTakings = park.Save?.MonthShopTakings?.Copy() ?? new();
		MonthSideshowTakings = park.Save?.MonthSideshowTakings?.Copy() ?? new();

		// The trend arrow as the load's own call reads the rings (FUN_004815e0 from the load's rebuild, 0x0048166f).
		ReadTheTrend();

		foreach ( var thing in park.Objects )
			_nextObject[thing.ThingId] = thing.NextObject;

		// And the queues as the save left them. Every one is empty in the park that ships - nobody has ever
		// been admitted to it - so this seeds nothing today and is still what makes a saved queue survive
		// being loaded, rather than the park quietly starting everybody at the front.
		foreach ( var thing in park.Objects )
		{
			if ( thing.FirstInQueue != 0 )
				_queueHead[thing.ThingId] = thing.FirstInQueue;

			// And the guest it had called forward, the head of its queue wherever a file sets it.
			if ( thing.PersonBeingLoaded != 0 )
				_beingLoaded[thing.ThingId] = thing.PersonBeingLoaded;

			// And what each has taken so far. Nought on every object in the park that ships - nobody has
			// ever paid for anything in it - so this seeds nothing today, for the same reason the queues
			// above seed nothing, and is still what stops a played park's takings being lost on load.
			if ( thing.TotalTakings != 0 )
				_takings[thing.ThingId] = thing.TotalTakings;

			if ( thing.TotalCosts != 0 )
				_costs[thing.ThingId] = thing.TotalCosts;

			// Its days as the record left them. In the park that ships every finished day is nought; most entries are on
			// 1 and wrapped, things 11 and 12 on 2, and thing 15 on 5 and not wrapped, with the heap's fill past its days.
			if ( thing.Rings is { } rings )
				_rings[thing.ThingId] = new ParkObjectRings( rings );
		}

		foreach ( var person in park.People )
		{
			if ( person.Guest is not { } guest )
				continue;

			if ( guest.QNext != 0 )
				_queueNext[person.ThingId] = guest.QNext;

			if ( guest.QPrev != 0 )
				_queuePrev[person.ThingId] = guest.QPrev;
		}
	}

	/// <summary>
	/// The two facts on their own, for a test that has no park to load - the arrangement
	/// <see cref="PeepBehaviour"/> has, and for the same reason: the only park that can be loaded is saved
	/// open, and this starts one shut without loading anything.
	/// </summary>
	public ParkState( bool parkIsClosed, int visitorsToDate, int balance = 0 )
	{
		Current = this;
		ParkIsClosed = parkIsClosed;
		VisitorsToDate = visitorsToDate;
		Balance = balance;
		WithdrawalsEnabled = 1;
		_cells = new RuntimeCell[ParkWorld.MapSize * ParkWorld.MapSize];
		TrackRides = new ParkTrackRideTable();
	}

	/// <summary>
	/// The track rides' table: each ride's slot and the track laid for it - see <see cref="ParkTrackRideTable"/>.
	/// A purchase takes a slot and a sale lets it go (<see cref="ParkBuilding"/>).
	/// </summary>
	public ParkTrackRideTable TrackRides { get; }

	/// <summary>
	/// What the park is worth now - the bank's <c>mBalance</c> (<c>+0xc</c>) the save was left with, moved by
	/// everything since.
	///
	/// <para>
	/// <b>It is one number</b>, which the bank's three ways in and out move: <see cref="Take"/> (the gate fee,
	/// <c>FUN_004d0600</c>), <see cref="Deposit"/> (<c>FUN_004d0190</c>) and <see cref="Spend"/>
	/// (<c>FUN_004d01f0</c>) - <c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's money".
	/// </para>
	/// </summary>
	public int Balance { get; private set; }

	/// <summary>
	/// What has been taken at the gate since the park opened, kept beside <see cref="Balance"/> rather
	/// than folded into it because it answers a different question - the balance is a position and this is
	/// a flow. It is not a field of the original's bank: only <see cref="Take"/> moves it, where the original
	/// adds a fee to its analyser's month gate takings (<c>+0x1fee0</c>), which nothing here keeps.
	/// </summary>
	public int Takings { get; private set; }

	/// <summary>
	/// The bank's <c>mWithdrawalsEnabled</c> (<c>+0x114</c>, bank file 28): while it is nought <see cref="Spend"/>
	/// does nothing at all. The constructor sets it to 1, and only an online park's layout replay clears it, so it is
	/// 1 in every park that can be played here; a test sets it.
	/// </summary>
	public int WithdrawalsEnabled { get; internal set; }

	/// <summary>
	/// The bank's <c>mLastBalance</c> (<c>+0x11c</c>): the balance the last <see cref="Spend"/> left. A deposit never
	/// writes it, so <see cref="Balance"/> less this is what has come in since.
	/// </summary>
	public int LastBalance { get; private set; }

	/// <summary>
	/// The bank's <c>mTurnEnteredRed</c> (<c>+0x120</c>): the <see cref="GameTick"/> of the <see cref="Spend"/> that
	/// took the balance below nought from a <see cref="LastBalance"/> of nought or more.
	/// </summary>
	/// <remarks>
	/// Read by the bank's month turn (<see cref="MonthsInTheRed"/>). The original's other readers, advisor rows 103
	/// to 105, are not built.
	/// </remarks>
	public int TurnEnteredRed { get; private set; }

	/// <summary>
	/// The bank's <c>mProfitThisYear</c> (<c>+0x124</c>): every <see cref="Take"/> and <see cref="Deposit"/> adds
	/// to it, every <see cref="Spend"/> takes from it, and the year's change zeroes it (<see cref="TurnTheYear"/>).
	/// </summary>
	/// <remarks>
	/// Golden ticket Local 4 reads it (<see cref="ParkGoldenTickets.Measure"/>; <c>0x004d4d6b</c>). No advisor row reads it here (row 318,
	/// the profit hint, is never said by the original).
	/// </remarks>
	public int ProfitThisYear { get; private set; }

	/// <summary>
	/// How many guests this park has ever admitted, counting on from what the save recorded - the
	/// original's <c>world + 0x1da714</c>, moved once a guest by <c>FUN_0051aaf0</c>: as one finishes at the gate,
	/// or as one is made on a cell that counts as the park's (<c>docs/exe/guest-arrivals.md</c>).
	/// </summary>
	public int VisitorsToDate { get; private set; }

	/// <summary>
	/// The park's own clock, the original's <c>mGameTick</c> (<c>world + 0x1da70c</c>): one count a thing sweep, so
	/// every 248 ms, and seeded from the save, so it carries on from the sweep the park was saved on (755 in Lost
	/// Kingdom) rather than from nought. Entering a park zeroes it and then loads the save's over it
	/// (<c>docs/exe/park.md</c>, "Arrivals"). <see cref="AdvanceGameTick"/> moves it.
	/// </summary>
	public int GameTick { get; private set; }

	/// <summary>
	/// The park's one generator, the original's <c>mRandomSeed</c> (<c>world + 0x1da708</c>): seeded from the save and
	/// written back to it, stepped by <see cref="Draw"/> (<c>docs/exe/park.md</c>, "Arrivals: who comes, on what"). <b>Only the
	/// leavers' summons draws it here</b>; every other system keeps its own state and says so at its site, and the
	/// eight reseeds with a guest's id that the original makes on it (an arrival, a balloon, a costume's return among them:
	/// eight callers of <c>FUN_00516370</c>) are made on
	/// those sites' own states, so this one's sequence is the original's only where nothing else drew between.
	/// </summary>
	public uint RandomSeed { get; private set; }

	/// <summary>One draw of the park's generator - <c>FUN_00516330</c> on <see cref="RandomSeed"/>.</summary>
	internal uint Draw()
	{
		var state = RandomSeed;
		var drawn = ParkGenerator.Draw( ref state );

		RandomSeed = state;

		return drawn;
	}

	/// <summary>
	/// A thing sweep begins: the clock goes one up before anything in the sweep runs, as <c>FUN_00516380</c> does at
	/// <c>0x00516394</c>. <see cref="ParkPeople"/> runs the sweep and is the one caller.
	/// </summary>
	public int AdvanceGameTick() => ++GameTick;

	/// <summary>
	/// Writes the park's clock, for the console's <c>clock</c>: an instrument, as a write to <c>mGameTick</c> in
	/// the original's memory is. Every stamp already taken stays as it was.
	/// </summary>
	internal void SetGameTick( int tick ) => GameTick = tick;

	/// <summary>Writes the bank's balance and nothing else: the debug console's <c>money balance</c>, an instrument.</summary>
	internal void SetBalance( int balance ) => Balance = balance;

	/// <summary>
	/// Now on the park's own calendar - <c>FUN_004f8690</c>: <c>mFunnyTimeStart</c> plus <c>mGameTick ×
	/// mFunnySecsPerRealSec / 4</c> whole seconds, the tick taken unsigned. A thing built is stamped with it
	/// (<c>FUN_004db090</c>, <c>0x004db66a</c>) and its age is measured against it (<see cref="AgeInDays"/>).
	/// </summary>
	/// <remarks>
	/// The start is <see cref="GameCalendar.Epoch"/>, the clock constructor's own: the save's clock block is read
	/// (<see cref="ParkWorld.Calendar"/>) but its start is not used, and every park file holds the same one
	/// (<c>docs/exe/ride-operation.md</c>, "What it gives the three rides"). The rate is <see cref="GameCalendar.Rate"/>,
	/// the loaded file's. <see cref="GameCalendar.Now"/> is the same date as of the last frame's sweeps.
	/// </remarks>
	public DateTime CalendarNow
		=> GameCalendar.Epoch.AddTicks( (long)(uint)GameTick * GameCalendar.Rate / GameCalendar.AdvancesPerSecond
			* TimeSpan.TicksPerSecond );

	/// <summary>
	/// The park clock's <c>mMonthAtLastUpdate</c>, nought-based, and <c>mDayAtLastUpdate</c> as the original's hold them
	/// now: <c>Calendar_Update</c> (<c>0x004f8260</c>) stores the date of <c>mGameTick</c> at the end of every sweep, the
	/// same sweep that counts it on (<c>World_SweepTail</c>, <c>0x004d7b20</c>), so between sweeps they are
	/// <see cref="CalendarNow"/>'s month and day. A save writes them (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the
	/// calendar's fields").
	/// </summary>
	public (int Month, int Day) CalendarAtLastUpdate => (CalendarNow.Month - 1, CalendarNow.Day);

	/// <summary>
	/// The park's calendar as the newest member of staff was made, a <c>FILETIME</c>: the park analyser's
	/// <c>mLastStaffHired</c> (<c>+0x21c38</c>), the file's own at a load (<see cref="ParkWorld.LastStaffHired"/>)
	/// and the calendar's start in a park made with no file, whose analyser is made on tick nought
	/// (<c>FUN_004c58c0</c>).
	/// </summary>
	public long LastStaffHired { get; private set; }

	/// <summary>
	/// The park analyser's ring of the bank's <see cref="Balance"/> at each month's close (<c>+0x1f100</c>), the file's at
	/// a load and empty in a park made with no file; <see cref="TurnTheMonth"/> samples it first.
	/// </summary>
	public ParkMonthRing MonthBalance { get; private set; } = new();

	/// <summary>
	/// The park analyser's ring of the park value at each month's close (<c>+0x1f350</c>, <see cref="ParkValue"/>), as
	/// <see cref="MonthBalance"/>; <see cref="TurnTheMonth"/> samples it after the money rings.
	/// </summary>
	public ParkMonthRing MonthParkValue { get; private set; } = new();

	/// <summary>
	/// The park analyser's ring of <see cref="LifetimeVisitors"/> at each month's close (<c>+0x20820</c>), as
	/// <see cref="MonthBalance"/>; <see cref="TurnTheMonth"/> samples it after the money rings, before the park value.
	/// </summary>
	public ParkMonthRing MonthVisitors { get; private set; } = new();

	/// <summary>
	/// The park analyser's ring of the guests inside the park at each month's close (<c>+0x205d0</c>, <c>FUN_004c7fa0</c>),
	/// as <see cref="MonthBalance"/>; <see cref="TurnTheMonth"/> samples it after the money rings, before the lifetime visitors.
	/// </summary>
	public ParkMonthRing MonthGuestsInside { get; private set; } = new();

	/// <summary>
	/// The park analyser's ring of the rides standing at each month's close (<c>+0x20a70</c>, <see cref="RidesStanding"/>),
	/// as <see cref="MonthBalance"/>; <see cref="TurnTheMonth"/> samples it after the lifetime visitors, before the park value.
	/// </summary>
	public ParkMonthRing MonthRides { get; private set; } = new();

	/// <summary>
	/// The park analyser's 14th ring (<c>+0x20f10</c>), its one of floats: the mean happiness of the guests inside the park
	/// at each close (<c>FUN_004c7bb0</c>), pushed by <see cref="TurnTheMonth"/> after the rides; nothing adds to its month
	/// so far. Read with <see cref="ParkMonthRing.NewestSingle"/>.
	/// </summary>
	public ParkMonthRing MonthHappiness { get; private set; } = new();

	/// <summary>
	/// The park analyser's 15th ring (<c>+0x21160</c>): the mean of <see cref="LeaverStays"/>' newest fifty at each close, in
	/// whole days (<c>FUN_004cef30</c>, <see cref="ParkMonthRing.MeanOfNewest"/>), pushed by <see cref="TurnTheMonth"/>
	/// after the park value; nothing adds to its month so far.
	/// </summary>
	public ParkMonthRing MonthMeanStay { get; private set; } = new();

	/// <summary>
	/// The park analyser's 16th ring (<c>+0x213b0</c>): the <see cref="ParkScore"/> at each close (<c>FUN_004cceb0</c>),
	/// pushed by <see cref="TurnTheMonth"/> after the mean stay, the last of its rings; nothing adds to its month so far.
	/// </summary>
	public ParkMonthRing MonthParkScore { get; private set; } = new();

	/// <summary>
	/// The park analyser's ring of the last fifty stays (<c>+0x21600</c>): <see cref="AddLeaverStay"/> pushes a paying
	/// guest's whole days in the park as they reach the stop to go home.
	/// </summary>
	public ParkMonthRing LeaverStays { get; private set; } = new( ParkMonthRing.Stays );

	/// <summary>
	/// A paying guest at the stop to go home hands the analyser their stay (<c>FUN_004c7600</c>, from <c>0x00502333</c>):
	/// <paramref name="stay"/> (<c>FUN_004fd950</c>, <see cref="Peep.StayAt"/>) in <see cref="StayDays"/> into the next slot.
	/// </summary>
	public void AddLeaverStay( uint stay )
	{
		var days = StayDays( stay );

		LeaverStays.Push( days );
		Log.Info( $"Park: a leaver's stay, {stay} fours of sweeps, {days} days into slot {LeaverStays.Current}, the newest {ParkMonthRing.Stays}'s mean {LeaverStays.MeanOfNewest( ParkMonthRing.Stays )}" );
	}

	/// <summary>
	/// A stay's whole park days as <c>FUN_004c7600</c> takes them: the stay shifted back left by two (<c>SHL</c> at
	/// <c>0x0050234a</c>, so in sweeps, a multiple of four), <c>FUN_004f8730</c>'s park time in 100 ns units (times the rate,
	/// over four, times 10,000,000 in 64 bits, which wraps) and <c>__alldiv</c> by 864,000,000,000, signed, its low half.
	/// </summary>
	public static int StayDays( uint stay )
		=> unchecked( (int)((long)((ulong)(stay << 2) * (uint)GameCalendar.Rate / GameCalendar.AdvancesPerSecond * 10_000_000UL)
			/ 864_000_000_000L) );

	/// <summary>
	/// The arrival manager's ring of the last five loads, on the park analyser (<c>+0x216d8</c>): <see cref="AddArrivalLoad"/>
	/// pushes each load's size as the load is called. The advisor's golden-ticket explanation, row 0x119, scores while one
	/// has been called (<c>AdvisorScore_GoldTicketExplanation</c>, <c>0x005a0780</c>; <see cref="ParkPeople.AdvisorFacts"/>).
	/// </summary>
	public ParkMonthRing ArrivalLoads { get; private set; } = new( ParkMonthRing.Loads );

	/// <summary>
	/// A load called: its <paramref name="size"/> into the next slot of <see cref="ArrivalLoads"/>, after the headcount is
	/// taken and before any vehicle is asked (<c>FUN_004cf3e0</c>, <c>0x004cf41f</c>..<c>0x004cf451</c>).
	/// </summary>
	internal void AddArrivalLoad( int size )
	{
		ArrivalLoads.Push( size );
		Log.Info( $"Park: a load of {size} into the arrival loads' slot {ArrivalLoads.Current}, {(ArrivalLoads.Wrapped ? ParkMonthRing.Loads : ArrivalLoads.Current + 1)} called" );
	}

	/// <summary>
	/// The park analyser's month of guests the arrival vehicles have dropped off (<c>+0x20cc0</c>) and its closed months,
	/// as <see cref="MonthCosts"/>; <see cref="AddArrival"/> adds to it, and <see cref="TurnTheMonth"/> closes it after
	/// the sideshow takings.
	/// </summary>
	public ParkMonthRing MonthArrivals { get; private set; } = new();

	/// <summary>
	/// One guest dropped off by the load's vehicle: onto the analyser's month of arrivals, after the guest is made and
	/// the count still to drop lowered (<c>FUN_004cf3e0</c>, <c>0x004cf586</c>).
	/// </summary>
	internal void AddArrival() => MonthArrivals.Add( 1 );

	/// <summary>
	/// The objects whose item is a ride's kind (<c>Info.WhichUIType</c> 0), whatever their state - <c>FUN_004c8070</c>,
	/// which walks the thing list for things of type 3 whose item's <c>+0x4ac</c> is nought. An item
	/// <paramref name="catalogue"/> does not name is not counted.
	/// </summary>
	/// <remarks>
	/// The original keeps its last count (<c>+0x1f0c8</c>) and gives it again to a caller asking within as many quarter
	/// ticks as it names; the close names nought. This counts afresh every time (a deviation of the same quarter tick at most).
	/// </remarks>
	public int RidesStanding( ParkItemCatalogue? catalogue )
		=> catalogue == null ? 0 : ObjectsInChainOrder().Count( thing => catalogue.TryGet( thing.CatalogueId, out var item )
			&& item.UiType == ItemDescriptionFile.Ride );

	/// <summary>
	/// The park analyser's <c>mLifetimeVisitors</c> (<c>+0x21c08</c>): one for every admission fee <see cref="Take"/>
	/// takes, nought or not; the file's at a load and nought in a park made with no file (<c>FUN_004c58c0</c>). Not the
	/// header's <see cref="VisitorsToDate"/>, which counts arrivals.
	/// </summary>
	public int LifetimeVisitors { get; private set; }

	/// <summary>
	/// The park analyser's <c>mMostPaidForTicket</c> (<c>+0x21c40</c>): the dearest admission fee <see cref="Take"/> has
	/// taken, as <see cref="LifetimeVisitors"/> is kept.
	/// </summary>
	public int MostPaidForTicket { get; private set; }

	/// <summary>
	/// The park analyser's three counts of misbehaving kids (<c>+0x21bfc</c> to <c>+0x21c04</c>): the month's two the
	/// file's at a load and the third nought, and all three nought in a park made with no file (<c>FUN_004c58c0</c>);
	/// <see cref="TurnTheMonth"/> zeroes the month's two. What
	/// counts into them, a guard's chase ending (<c>FUN_004c74f0</c>), waits on the pranks (<c>DECIDE_PRANK_*</c>).
	/// </summary>
	public ParkWorld.MisbehavingKidCounts MisbehavingKids { get; private set; }

	/// <summary>
	/// The park analyser's month of every withdrawal (<c>+0x1f5a0</c>) and its closed months, the file's at a load and
	/// empty in a park made with no file (<c>FUN_004c58c0</c>); <see cref="Spend"/> adds to it.
	/// </summary>
	public ParkMonthRing MonthCosts { get; private set; } = new();

	/// <summary>The analyser's month of wages and severances (<c>+0x1f7f0</c>), as <see cref="MonthCosts"/>; <see cref="AddStaffCost"/> adds to it.</summary>
	public ParkMonthRing MonthStaffCosts { get; private set; } = new();

	/// <summary>The analyser's month of every deposit (<c>+0x1fc90</c>), as <see cref="MonthCosts"/>; <see cref="Deposit"/> and <see cref="Take"/> add to it.</summary>
	public ParkMonthRing MonthCashIn { get; private set; } = new();

	/// <summary>
	/// Whether the newest closed month's cash in was below its costs when the rings were last read for the gadget's trend
	/// arrow, which is red while it is (<c>Gadget_SetDateAndTrend</c> <c>0x004a0e30</c>, a signed <c>JL</c> at
	/// <c>0x004a0f8a</c>): read at a load and by <see cref="ReadTheTrend"/>. A ring with no closed month reads the same stand-in
	/// for both, so equal and green (<c>docs/exe/hud.md</c>, "The money's trend arrow").
	/// </summary>
	public bool MonthInLoss { get; private set; }

	/// <summary>
	/// Reads the rings for the trend arrow (<see cref="MonthInLoss"/>): once a day, after the day's change and before the
	/// month's, so the first of a month still reads the month before last (<c>Calendar_Update</c> <c>0x004f83ac</c>).
	/// </summary>
	public void ReadTheTrend() => MonthInLoss = MonthCashIn.Newest < MonthCosts.Newest;

	/// <summary>The analyser's month of training paid (<c>+0x1fa40</c>), as <see cref="MonthCosts"/>; <see cref="AddTrainingCost"/> adds to it.</summary>
	public ParkMonthRing MonthTraining { get; private set; } = new();

	/// <summary>The analyser's month of admission fees (<c>+0x1fee0</c>), as <see cref="MonthCosts"/>; <see cref="Take"/> adds to it.</summary>
	public ParkMonthRing MonthGateTakings { get; private set; } = new();

	/// <summary>The analyser's month of what the shops have charged (<c>+0x20130</c>), as <see cref="MonthCosts"/>; <see cref="AddShopTakings"/> adds to it.</summary>
	public ParkMonthRing MonthShopTakings { get; private set; } = new();

	/// <summary>The analyser's month of what the sideshows have charged (<c>+0x20380</c>), as <see cref="MonthCosts"/>; <see cref="AddSideshowTakings"/> adds to it.</summary>
	public ParkMonthRing MonthSideshowTakings { get; private set; } = new();

	/// <summary>The analyser's eight money rings, its park value, its guests inside, its lifetime visitors, its rides, its arrivals, its guests' happiness, its mean stay, its park score, its leavers' stays and its arrival loads as the writer takes them (<see cref="ParkWorld.PutMonthRings"/>).</summary>
	public ParkWorld.MonthRingSet MonthRings
		=> new( MonthBalance, MonthParkValue, MonthCosts, MonthStaffCosts, MonthTraining, MonthCashIn, MonthGateTakings, MonthShopTakings, MonthSideshowTakings,
			MonthGuestsInside, MonthVisitors, MonthRides, MonthArrivals, MonthHappiness, MonthMeanStay, LeaverStays, MonthParkScore, ArrivalLoads );

	/// <summary>
	/// A wage or a severance paid: onto the analyser's month of staff costs, after the withdrawal and whether or not it
	/// was made (<c>FUN_00504c70</c>, <c>FUN_00505790</c>).
	/// </summary>
	internal void AddStaffCost( int wage ) => MonthStaffCosts.Add( wage );

	/// <summary>
	/// A member's training paid: onto the analyser's month of training, after the withdrawal and whether or not it was
	/// made (<c>FUN_00505a10</c>).
	/// </summary>
	internal void AddTrainingCost( int share ) => MonthTraining.Add( share );

	/// <summary>A shop's charge, after its deposit: onto the analyser's month of shop takings (<c>FUN_004e16b0</c>, <c>0x004e1711</c>).</summary>
	internal void AddShopTakings( int price ) => MonthShopTakings.Add( price );

	/// <summary>A sideshow's charge, after its deposit: onto the analyser's month of sideshow takings (<c>FUN_004e16b0</c>, <c>0x004e18a6</c>).</summary>
	internal void AddSideshowTakings( int price ) => MonthSideshowTakings.Add( price );

	/// <summary>
	/// A member of staff has been made: the staff constructor's tail stamps the analyser with now
	/// (<c>FUN_00504b90</c>, <c>FUN_004c76d0</c>), whatever their kind.
	/// </summary>
	internal void StampStaffHired()
	{
		LastStaffHired = ParkWorld.StaffState.FileTimeOf( CalendarNow );
		Log.Info( $"Park: mLastStaffHired stamped {CalendarNow:d/M/yyyy HH:mm:ss} on mGameTick {GameTick}" );
	}

	/// <summary>
	/// The park's calendar as the newest object was made, a <c>FILETIME</c>: the park analyser's
	/// <c>mLastObjectBuilt</c> (<c>+0x21c30</c>), the file's own at a load (<see cref="ParkWorld.LastObjectBuilt"/>)
	/// and the calendar's start in a park made with no file (<c>FUN_004c58c0</c>). A sale leaves it standing.
	/// </summary>
	public long LastObjectBuilt { get; private set; }

	/// <summary>
	/// An object has been made, bought or put down from a move: the object constructor's tail stamps the analyser
	/// with now (<c>FUN_004db090</c>, <c>0x004db7a3</c>, <c>FUN_004c7680</c>).
	/// </summary>
	private void StampObjectBuilt()
	{
		LastObjectBuilt = ParkWorld.StaffState.FileTimeOf( CalendarNow );
		Log.Info( $"Park: mLastObjectBuilt stamped {CalendarNow:d/M/yyyy HH:mm:ss} on mGameTick {GameTick}" );
	}

	/// <summary>
	/// The park's calendar at the newest request to open the park, a <c>FILETIME</c>: the park analyser's
	/// <c>mParkLastOpened</c> (<c>+0x21c28</c>), the file's own at a load (<see cref="ParkWorld.ParkLastOpened"/>)
	/// and the calendar's start in a park made with no file (<c>FUN_004c58c0</c>). Nothing in the original reads it.
	/// </summary>
	/// <remarks>
	/// The analyser's three stamps before it, <c>mLastStrike</c>, <c>mLastPuke</c> and <c>mLastPrank</c>, are not kept:
	/// the original never moves them from the constructor's (the strike's message <c>0x17</c> goes to the advisor
	/// alone), so a file's own go out as they came in (<c>docs/exe/hud.md</c>, "The analyser's other stamps").
	/// </remarks>
	public long ParkLastOpened { get; private set; }

	/// <summary>The analyser's four rings of the recently researched, the file's at a load (<see cref="ParkResearchedRings"/>).</summary>
	public ParkResearchedRings RecentlyResearched { get; private set; } = new();

	/// <summary>
	/// Message <c>0x11</c>, a project finished (<see cref="ParkResearch.Researched"/>), as the advisor and the park analyser
	/// hear it: the advisor is posted the kind's line (<see cref="ParkAdvisorMessages.ResearchedOf"/>; Advisor_OnMessage
	/// <c>0x0059b0c6</c>), about no thing and with <paramref name="item"/> its second word, which names the item on its
	/// tag; then the item goes into the ring of buy list tab <paramref name="tab"/>, its <c>Info.WhichUIType</c>.
	/// </summary>
	internal void HearResearched( int item, int kind, int tab, int tick )
	{
		if ( ParkAdvisorMessages.ResearchedOf( kind ) is { } message )
			AdvisorMessages?.Post( message, tick, secondary: item );
		else
			Log.Warning( $"Research: item {item}'s project is of kind {kind}, which the advisor has no line for" );

		RecentlyResearched.Push( tab, item );
	}

	/// <summary>
	/// A kind's highest group raised with work left (<see cref="ParkResearch.GroupOpened"/>): advisor message
	/// <c>0x85</c> + kind about no thing (<c>0x00503c8e</c>), which Instant Action slots at -1 and never says.
	/// </summary>
	internal void HearGroupOpened( int kind, int tick ) => AdvisorMessages?.Post( ParkAdvisorMessages.NewRideGroup + kind, tick );

	/// <summary>
	/// Whole days since the newest member of staff was made - <c>FUN_004c90d0</c>: now less
	/// <see cref="LastStaffHired"/> over 864,000,000,000, a signed 64-bit divide whose low 32 bits are the answer.
	/// </summary>
	public int DaysSinceStaffHired
		=> unchecked((int)((ParkWorld.StaffState.FileTimeOf( CalendarNow ) - LastStaffHired) / TimeSpan.TicksPerDay));

	/// <summary>
	/// Whole days since the newest object was made - <c>0x004c9070</c>: now less <see cref="LastObjectBuilt"/> over
	/// 864,000,000,000, a signed 64-bit divide whose low 32 bits are the answer.
	/// </summary>
	public int DaysSinceObjectBuilt
		=> unchecked((int)((ParkWorld.StaffState.FileTimeOf( CalendarNow ) - LastObjectBuilt) / TimeSpan.TicksPerDay));

	/// <summary>
	/// The map's developed cells - <c>FUN_004c81b0</c>: the analyser's 1,024 regions of four cells square, which tile the
	/// whole map (<c>FUN_004c7a90</c>), each counting its cells whose <c>mType</c> is path, queue, a footprint or a ride's
	/// two ends (<c>FUN_004c8c40</c>), inside the park or out. <c>docs/exe/advisor-park.md</c>, "A park too big for its
	/// handymen".
	/// </summary>
	/// <remarks>
	/// Each region keeps its count and gives it again to a caller asking within as many quarter ticks as it names
	/// (<c>FUN_0041a990</c>); row 10's score names nought, so only the same quarter tick reuses one. This counts afresh
	/// every time (a deviation of the same quarter tick at most).
	/// </remarks>
	public int DevelopedCells()
	{
		var cells = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( IsDeveloped( Record( x, y ).Type ) )
					++cells;
			}
		}

		return cells;
	}

	/// <summary>
	/// The share of the map's walked cells that hold litter, in whole per cent - <c>FUN_004ccdf0</c>: every cell of the
	/// map, counted where its <c>mType</c> is path, queue or a ride's entrance (<c>FUN_00536310</c>, <c>FUN_00536320</c>)
	/// or its litter is not nought, and littered where its litter is not nought; the littered times 100 over the counted,
	/// cut towards nought, and nought with none counted. <c>docs/exe/advisor-park.md</c>, "Litter and no handyman".
	/// </summary>
	public int LitterShare()
	{
		var walked = 0;
		var littered = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var dropped = CellAt( x, y ).Litter != 0;
				var type = Record( x, y ).Type;

				if ( dropped || type is CellEdge.Path or ParkRideChoice.QueueCellType or CellEdge.RideEnd )
					++walked;

				if ( dropped )
					++littered;
			}
		}

		return walked == 0 ? 0 : littered * 100 / walked;
	}

	/// <summary>The <c>mType</c>s a developed cell has (<c>FUN_00536310</c>, <c>FUN_00536320</c>, <c>FUN_00536340</c>, <c>FUN_00536350</c>, <c>FUN_00536380</c>).</summary>
	public static bool IsDeveloped( int type )
		=> type is CellEdge.Path or ParkRideChoice.QueueCellType or CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd;

	/// <summary>
	/// How many whole days old a thing is on the park's calendar - <c>FUN_004dd670</c>: <c>( now − built ) /
	/// 864,000,000,000</c>, a signed 64-bit divide whose low 32 bits are the answer. A stamp in the future is
	/// negative, and less than a day ahead is nought. The ride score compares it unsigned; the object window
	/// prints it. A stamp that makes no date reads as nought here; the score treats it as not new
	/// (<see cref="ParkRideChooser.AgeOf"/>).
	/// </summary>
	public static int AgeInDays( DateTime now, ParkWorld.BuiltWhen built )
		=> built.ToDateTime() is { } when ? unchecked((int)((now - when).Ticks / TimeSpan.TicksPerDay)) : 0;

	/// <inheritdoc cref="AgeInDays(DateTime, ParkWorld.BuiltWhen)"/>
	public int AgeInDays( ParkWorld.CatalogueObject thing ) => AgeInDays( CalendarNow, thing.Built );

	/// <summary>
	/// Whether the park is shut to visitors, seeded from the save - <b>zero is open</b> - and movable
	/// afterwards, because the entry-price screen carries the switch that moves it.
	/// </summary>
	public bool ParkIsClosed { get; private set; }

	/// <summary>
	/// What the park charges at the gate - the economy thing's <c>mAdmissionFee</c>, seeded from the
	/// save and <b>movable afterwards</b>, which is the whole point of it being here.
	///
	/// <para>
	/// <b>It lives on the running state for the reason <see cref="Balance"/> does.</b>
	/// <see cref="ParkWorld"/> describes a file and may never be written to. The gate reads it here each time a
	/// guest judges the price or pays it (<see cref="ParkAdmission.Fee"/>).
	/// </para>
	/// </summary>
	public int AdmissionFee { get; private set; }

	/// <summary>
	/// Sets what the gate charges. The original's own setter logs <i>"Admission fee set to %d"</i>
	/// (<c>FUN_004d05d0</c>) - though that logger is an empty stub in the shipped build, so the string
	/// is evidence of the field's name and not of anything the game prints.
	/// </summary>
	/// <remarks>
	/// <b>Negative fees are refused rather than clamped silently.</b> The gate's own judgement reads
	/// the fee against the balance file's bands, and a negative one would make every guest think the
	/// park was paying them in - which the original cannot express, since its screen only ever offers
	/// a spinner over non-negative values.
	/// </remarks>
	public bool SetAdmissionFee( int fee )
	{
		if ( fee < 0 )
			return false;

		AdmissionFee = fee;

		return true;
	}

	/// <summary>
	/// Opens the park to visitors or shuts it - the <c>b_door</c> switch on the entry-price screen, which is
	/// the original's <c>FUN_00519ef0( open, 0 )</c> (<c>docs/exe/ride-operation.md</c>, "The park's door").
	/// </summary>
	/// <remarks>
	/// <b>Each arm acts only on a change.</b> It sets
	/// <see cref="ParkIsClosed"/>, which <see cref="PeepBehaviour"/> reads at the gate, and hands the rides to
	/// <see cref="DoorMoved"/>: closing closes every object a guest may be offered, opening
	/// opens each that <see cref="ParkRideOperation.MayOpen"/> allows.
	/// <para>
	/// The people command the gate script - opening writes 1, closing writes 0
	/// only when nobody is in the park and the gate reads open - and
	/// each posts a type-<c>0x13</c> message, 3 open or 4 closed, which the advisor answers with its own
	/// message <c>0x80</c> or <c>0x81</c>; the message is posted whether or not anything changed.
	/// </para>
	/// </remarks>
	public void SetParkClosed( bool closed )
	{
		if ( closed != ParkIsClosed )
		{
			ParkIsClosed = closed;

			DoorMoved?.Invoke( closed );
		}

		AdvisorMessages?.PostDoor( closed, GameTick );

		// The open arm's message 0x13 (3) reaches the analyser too, which stamps now (FUN_004c7370, 0x004c7444).
		if ( !closed )
		{
			ParkLastOpened = ParkWorld.StaffState.FileTimeOf( CalendarNow );
			Log.Info( $"Park: mParkLastOpened stamped {CalendarNow:d/M/yyyy HH:mm:ss} on mGameTick {GameTick}" );
		}
	}

	/// <summary>
	/// Who closes and opens the rides when the park's door moves - the park's people, which set it when they
	/// are made (<c>ParkPeople.DoorMoved</c>). With nobody set, the flag and advisor still receive the request.
	/// </summary>
	internal Action<bool>? DoorMoved { get; set; }

	/// <summary>The park's posted advisor messages, installed during level setup.</summary>
	internal ParkAdvisorMessages? AdvisorMessages { get; set; }
	/// <summary>The tags said lines have left on the gadget's aerial.</summary>
	internal ParkTags Tags { get; } = new();

	/// <summary>
	/// Takes an admission fee - the bank's <c>FUN_004d0600</c>: onto the balance and <see cref="ProfitThisYear"/>,
	/// the deposit's adds, and onto the gate's running total, which only this moves (<c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's money").
	/// </summary>
	/// <remarks>
	/// Onto the park analyser's month cash in (<see cref="MonthCashIn"/>) and its month gate takings
	/// (<see cref="MonthGateTakings"/>), then its <c>FUN_004c7520</c>: one more of <see cref="LifetimeVisitors"/> and
	/// <see cref="MostPaidForTicket"/> raised to the fee. Counted, not built: that routine's message that counts a new
	/// visitor toward a challenge of type 11, which takes it only while one is on.
	/// </remarks>
	public void Take( int fee )
	{
		unchecked
		{
			Balance += fee;
			ProfitThisYear += fee;
		}

		Takings += fee;
		MonthCashIn.Add( fee );
		MonthGateTakings.Add( fee );

		Log.Info( $"Bank: admission {fee}, balance {Balance}, profit {ProfitThisYear}" );

		LifetimeVisitors = unchecked(LifetimeVisitors + 1);

		if ( MostPaidForTicket < fee )
			MostPaidForTicket = fee;

		Log.Info( $"Park: lifetime visitors {LifetimeVisitors}, most paid for a ticket {MostPaidForTicket}" );
		Unimplemented.Report( "GATE_FEE_CHALLENGE_POST" );
	}

	/// <summary>
	/// Pays for something - the bank's withdrawal, <c>FUN_004d01f0</c>, which every way money leaves the park goes
	/// through: a purchase, a path or queue cell, a sale's queue drain, a dismissal and a sale's cost of goods.
	///
	/// <para>
	/// <b>It does not refuse, and the refusal is deliberately the caller's.</b> The original tests
	/// affordability at the moment a buy row is clicked - <c>FUN_004ac270</c> compares the item's price
	/// against <c>FUN_006ad810()</c> and only then builds the placement mode - so by the time anything is
	/// paid for the decision has already been made somewhere with a screen to complain on. A refusal here
	/// would be a second, silent one. It has no floor either.
	/// </para>
	/// <para>
	/// In its order (<c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's money"): nothing at all while <see cref="WithdrawalsEnabled"/> is nought; the balance less
	/// the cost; <see cref="TurnEnteredRed"/> stamped when that is below nought and the old <see cref="LastBalance"/>
	/// is not; <see cref="LastBalance"/> the new balance; and <see cref="ProfitThisYear"/> less the cost. A cost of
	/// nought still writes the last balance; the cost goes onto the park analyser's month of costs (<see cref="MonthCosts"/>).
	/// </para>
	/// </summary>
	public void Spend( int cost )
	{
		if ( WithdrawalsEnabled == 0 )
		{
			Log.Info( $"Bank: withdrawal {cost} not made, withdrawals are off" );
			return;
		}

		unchecked
		{
			var balance = Balance - cost;

			if ( balance < 0 && LastBalance >= 0 )
				TurnEnteredRed = GameTick;

			Balance = balance;
			LastBalance = balance;

			MonthCosts.Add( cost );

			ProfitThisYear -= cost;
		}

		Log.Info( $"Bank: withdrawal {cost}, balance {Balance}, last {LastBalance}, red {TurnEnteredRed}, "
			+ $"profit {ProfitThisYear}" );
	}

	/// <summary>
	/// Puts money in the bank - the deposit, <c>FUN_004d0190</c>: what selling something gives back, what a
	/// cleared queue cell refunds, and what a guest pays for a ride, shop or sideshow (<see cref="TakeAt"/>).
	///
	/// <para>
	/// <b>It is not <see cref="Take"/>, and the difference is not cosmetic.</b> Taking a fee moves the
	/// balance <i>and</i> <see cref="Takings"/>, which is what the gates have taken; a deposit that went
	/// through there would report gate money the park never took. It moves the balance and
	/// <see cref="ProfitThisYear"/>, has no gate and writes no <see cref="LastBalance"/>; its size check goes to a
	/// bare <c>RET</c> and refuses nothing. It goes onto the park analyser's month cash in (<see cref="MonthCashIn"/>).
	/// </para>
	/// </summary>
	public void Deposit( int amount )
	{
		unchecked
		{
			Balance += amount;
			MonthCashIn.Add( amount );

			ProfitThisYear += amount;
		}

		Log.Info( $"Bank: deposit {amount}, balance {Balance}, profit {ProfitThisYear}" );
	}

	/// <summary>Admits one guest and hands back which visitor they are, counting from one.</summary>
	public int Admit()
	{
		++VisitorsToDate;

		Log.Info( $"Park: visitor {VisitorsToDate} received" );

		return VisitorsToDate;
	}

	/// <summary>What this object has taken, counting on from what the save recorded.</summary>
	public int TakingsFor( int objectId ) => _takings.GetValueOrDefault( objectId );

	/// <summary>
	/// What this object has booked as its cost of goods, counting on from what the save recorded - <c>mTotalCosts</c>.
	/// </summary>
	public int CostsFor( int objectId ) => _costs.GetValueOrDefault( objectId );

	/// <summary>
	/// What a guest has just paid an object - <c>FUN_004e16b0</c>: the price deposited in the park's bank first
	/// (<see cref="Deposit"/>), then added to the object's <c>mTotalTakings</c> at <c>+0x180</c> and today's takings
	/// at <c>+0x70</c>.
	/// </summary>
	/// <remarks>
	/// The park analyser's shop and sideshow month totals and the challenge posts are counted by the charge, which
	/// knows the kind.
	/// </remarks>
	public void TakeAt( int objectId, int amount )
	{
		if ( objectId == 0 || amount == 0 )
			return;

		Deposit( amount );

		_takings[objectId] = TakingsFor( objectId ) + amount;
		RingsFor( objectId ).Takings.Today += amount;
	}

	/// <summary>
	/// Books an object's cost of goods - <c>FUN_004e1920</c>: onto today's costs (<c>+0xf8</c>) and
	/// <c>mTotalCosts</c> (<c>+0x184</c>), then withdrawn from the park's bank (<see cref="Spend"/>).
	/// </summary>
	/// <remarks>
	/// No test of the amount, and the object's two are booked even while withdrawals are off, the gate being inside
	/// the withdrawal. Counted, not built: minus the amount posted as progress on a challenge of type 12 (a shop)
	/// or 13 (a sideshow), which takes it only while one is on (<c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's money").
	/// </remarks>
	public void BookCostOfGoods( int objectId, int amount )
	{
		unchecked
		{
			RingsFor( objectId ).Costs.Today += amount;
			_costs[objectId] = CostsFor( objectId ) + amount;
		}

		Log.Info( $"Object {objectId}: cost of goods {amount} booked, today {RingsFor( objectId ).Costs.Today}, "
			+ $"total {CostsFor( objectId )}" );

		Spend( amount );

		Unimplemented.Report( "COST_OF_GOODS_CHALLENGE_POST" );
	}

	/// <summary>
	/// The bank's <c>mBatchBalance</c> (<c>+0x10</c>): money waiting to be banked at the month's change. Only a file
	/// sets it, and it is nought in every park file.
	/// </summary>
	public int BatchBalance { get; internal set; }

	/// <summary>The bank's eight loans, as the save left them; the month's change pays the bought ones.</summary>
	public IReadOnlyList<ParkWorld.LoanState> Loans => _loans;

	private readonly ParkWorld.LoanState[] _loans = new ParkWorld.LoanState[ParkWorld.EconomyState.LoanSlots];

	/// <summary>The bank's seven fields and eight loans as they stand, the economy thing the writer writes.</summary>
	public ParkWorld.EconomyState Economy => new( AdmissionFee, Balance, BatchBalance, WithdrawalsEnabled, LastBalance,
		TurnEnteredRed, ProfitThisYear, _loans.ToArray() );

	/// <summary>The bank's fields and its bought loans, for the save's log and the console's <c>loans</c>.</summary>
	public string DescribeEconomy()
		=> $"fee {AdmissionFee} balance {Balance} batch {BatchBalance} withdrawals {WithdrawalsEnabled} last {LastBalance} "
			+ $"red {TurnEnteredRed} profit {ProfitThisYear} loans ["
			+ string.Join( ", ", _loans.Select( ( loan, i ) => (loan, i) ).Where( x => x.loan.Bought != 0 )
				.Select( x => $"{x.i}: {x.loan.MonthsRepaid}/{x.loan.RepaymentMonths} of {x.loan.MonthlyRepayment}" ) )
			+ "]";

	/// <summary>Puts a loan in a slot - a test's, as no loans screen is built and no park file buys one.</summary>
	internal void SetLoan( int slot, ParkWorld.LoanState loan ) => _loans[slot] = loan;

	/// <summary>
	/// Thirty funny days in the 100-nanosecond units <c>FUN_004f88b0</c> answers in - the divisor
	/// <c>0x1792f8648000</c> the bank's red count uses.
	/// </summary>
	private const long ThirtyDays = 25_920_000_000_000L;

	/// <summary>How many whole months in the red end a park - the bank's <c>5 &lt; months</c>.</summary>
	public const int MonthsInTheRedToEnd = 6;

	/// <summary>
	/// The month's change reaches the park analyser (thing 5) and then the bank (thing 8) - message <c>0xc</c>
	/// (<c>docs/exe/ride-operation.md</c>, "The month's change"). The analyser's close (<c>FUN_004c7720</c>) samples the
	/// bank's <see cref="Balance"/> into its first ring, then closes its seven money rings, all costs, staff costs,
	/// training, cash in, gate, shop and sideshow takings, in that order, and the month's arrivals, then samples <paramref name="guestsInside"/>,
	/// <see cref="LifetimeVisitors"/>, the <see cref="RidesStanding"/>, <paramref name="guestsHappiness"/>, the <see cref="ParkValue"/> of
	/// <paramref name="catalogue"/>'s items, zeroes the month's two <see cref="MisbehavingKids"/> counts, then samples the mean of the <see cref="LeaverStays"/> and the <see cref="ParkScore"/>. The bank's turn, <c>FUN_004d0370</c>: <see cref="BatchBalance"/> deposited and zeroed; each bought loan's
	/// instalment withdrawn, its months repaid counted, <see cref="ProfitThisYear"/> given the principal's share back
	/// by the original's unsigned division, and the loan closed at its period; then, with the balance and
	/// <see cref="LastBalance"/> both below nought, the whole thirty-day months since <see cref="TurnEnteredRed"/> -
	/// at six the park ends, counted.
	/// </summary>
	/// <param name="guestsInside">
	/// The guests on a counting cell (<c>FUN_004c7fa0</c> asked with nought, so counted afresh unless the same quarter tick
	/// already counted them).
	/// </param>
	/// <param name="guestsHappiness">The mean happiness of the guests inside, a float (<c>FUN_004c7bb0</c>, <see cref="ParkPeople.GuestsInsideHappiness"/>).</param>
	/// <param name="staff">The staff the <see cref="ParkScore"/> counts (<see cref="ParkPeople.ScoreStaff"/>).</param>
	public void TurnTheMonth( ParkItemCatalogue? catalogue, int guestsInside = 0, float guestsHappiness = 0f, ParkScore.StaffCounts staff = default )
	{
		MonthBalance.Push( Balance );
		MonthCosts.Close();
		MonthStaffCosts.Close();
		MonthTraining.Close();
		MonthCashIn.Close();
		MonthGateTakings.Close();
		MonthShopTakings.Close();
		MonthSideshowTakings.Close();
		MonthArrivals.Close();
		MonthGuestsInside.Push( guestsInside );
		MonthVisitors.Push( LifetimeVisitors );
		MonthRides.Push( RidesStanding( catalogue ) );
		MonthHappiness.Push( guestsHappiness );
		MonthParkValue.Push( ParkValue.Of( this, catalogue ) );
		MisbehavingKids = MisbehavingKids with { Month = 0, GotAwayThisMonth = 0 };
		MonthMeanStay.Push( LeaverStays.MeanOfNewest( ParkMonthRing.Stays ) );
		MonthParkScore.Push( ParkScore.Of( this, catalogue, guestsInside, staff ) );
		Log.Info( $"Park: analyser month closed, balance {MonthBalance.Newest}, park value {MonthParkValue.Newest}, guests inside {MonthGuestsInside.Newest}, "
			+ $"visitors {MonthVisitors.Newest}, rides {MonthRides.Newest}, happiness {MonthHappiness.NewestSingle}, mean stay {MonthMeanStay.Newest}, park score {MonthParkScore.Newest}, costs {MonthCosts.Newest}, staff costs {MonthStaffCosts.Newest}, "
			+ $"training {MonthTraining.Newest}, cash in {MonthCashIn.Newest}, gate {MonthGateTakings.Newest}, "
			+ $"shops {MonthShopTakings.Newest}, sideshows {MonthSideshowTakings.Newest}, arrivals {MonthArrivals.Newest}, "
			+ $"misbehaving kids {MisbehavingKids.Month} got away {MisbehavingKids.GotAwayThisMonth} ever {MisbehavingKids.Ever}" );

		var batch = BatchBalance;
		BatchBalance = 0;
		Deposit( batch );

		for ( var i = 0; i < _loans.Length; ++i )
		{
			var loan = _loans[i];

			if ( loan.Bought == 0 )
				continue;

			Spend( loan.MonthlyRepayment );

			unchecked
			{
				var repaid = loan.MonthsRepaid + 1;
				var period = (uint)loan.RepaymentMonths;

				if ( period != 0 )
				{
					var monthly = (uint)loan.MonthlyRepayment;
					ProfitThisYear += (int)(monthly - ((monthly * period) - (uint)loan.AmountAvailable) / period);
				}

				loan = repaid == loan.RepaymentMonths
					? loan with { Bought = 0, MonthsRepaid = 0 }
					: loan with { MonthsRepaid = repaid };
			}

			_loans[i] = loan;

			Log.Info( $"Bank: loan {i} instalment {loan.MonthlyRepayment}, months repaid {loan.MonthsRepaid}, "
				+ $"bought {loan.Bought}, profit {ProfitThisYear}" );
		}

		if ( Balance < 0 && MonthsInTheRed() >= MonthsInTheRedToEnd )
		{
			Log.Info( $"Bank: {MonthsInTheRed()} months in the red - the park ends" );
			Unimplemented.Report( "BANK_PARK_ENDS_IN_THE_RED" );
			Unimplemented.Report( "ADVISOR_PARK_ENDED_IN_THE_RED" );
		}
	}

	/// <summary>
	/// The bank's count of whole thirty-day months since <see cref="TurnEnteredRed"/>: the ticks since, times the
	/// calendar's rate, over four, as funny seconds (<c>FUN_004f88b0</c>), over thirty days; nought unless
	/// <see cref="LastBalance"/> is below nought.
	/// </summary>
	public int MonthsInTheRed()
	{
		if ( LastBalance >= 0 )
			return 0;

		unchecked
		{
			var ticks = (ulong)(uint)(GameTick - TurnEnteredRed);
			var hundredNanoseconds = ticks * (uint)GameCalendar.Rate / 4 * 10_000_000UL;

			return (int)((long)hundredNanoseconds / ThirtyDays);
		}
	}

	/// <summary>
	/// The year's change reaches the bank - message <c>0xd</c>, whose arm of the bank's handler zeroes
	/// <see cref="ProfitThisYear"/> and does nothing else (<c>0x004d034e</c>).
	/// </summary>
	public void TurnTheYear()
	{
		ProfitThisYear = 0;

		Log.Info( $"Bank: the year's change, profit this year {ProfitThisYear}" );
	}

	/// <summary>
	/// The runtime cell at a grid position, by reference so a caller can change it in place. Off the map
	/// throws rather than returning a default: a default would be silently writable and the write would
	/// go nowhere, which is the kind of no-op this project has been bitten by.
	/// </summary>
	public ref RuntimeCell CellAt( int x, int y )
	{
		if ( x < 0 || y < 0 || x >= ParkWorld.MapSize || y >= ParkWorld.MapSize )
			throw new ArgumentOutOfRangeException( nameof( x ), $"({x},{y}) is off a {ParkWorld.MapSize} square map" );

		return ref _cells[(y * ParkWorld.MapSize) + x];
	}

	/// <summary>Whether a grid position is on the map at all, for a caller that would rather ask than catch.</summary>
	public static bool OnMap( int x, int y )
		=> x >= 0 && y >= 0 && x < ParkWorld.MapSize && y < ParkWorld.MapSize;

	// The queues: ParkWorld describes a file and is immutable, so a guest joining a queue has nowhere
	// to write. The shape is the original's own - a head on the object (mFirstInQ) and a doubly-linked
	// list through the guests themselves (mQNext, mQPrev) - kept here rather than on Peep so that the
	// whole structure lives in one place and is seeded once.
	/// <summary>
	/// What each object has taken, by thing id - the original's <c>mTotalTakings</c> at the object's
	/// <c>+0x180</c>, which a charge moves and <see cref="ParkWorld"/> cannot because it describes a file.
	/// </summary>
	private readonly Dictionary<int, int> _takings = [];

	/// <summary>
	/// What each object has booked as its cost of goods, by thing id - the original's <c>mTotalCosts</c> at the
	/// object's <c>+0x184</c> (file 1086), which a sale moves and <see cref="ParkWorld"/> cannot.
	/// </summary>
	private readonly Dictionary<int, int> _costs = [];

	/// <summary>Each object's day rings and counts, by thing id - see <see cref="RingsFor"/>.</summary>
	private readonly Dictionary<int, ParkObjectRings> _rings = [];

	private readonly Dictionary<int, int> _queueHead = [];
	private readonly Dictionary<int, int> _queueNext = [];
	private readonly Dictionary<int, int> _queuePrev = [];

	// What is standing on each cell - a list per cell, headed by RuntimeCell.Occupant and linked
	// through the THINGS, exactly as the queues above are. The original keeps the links on the thing
	// itself (+8 previous, +10 next); they are kept here for the same reason mQNext/mQPrev are, so that
	// the whole structure lives in one place and is seeded once, from the file (SeedCellLists).
	private readonly Dictionary<int, int> _onCellNext = [];
	private readonly Dictionary<int, int> _onCellPrev = [];

	/// <summary>
	/// Every cell's list as the file chains it (<c>mWho</c>, then each thing's <c>mMapChild</c>; <see cref="ParkWorld.ChainAt"/>),
	/// each thing on it recorded as standing there - the original's loader takes the chains whole and enters nobody of
	/// its own accord. A person the save has heading a cell is then already on it, so their first turn's
	/// <see cref="StandOn"/> changes nothing, and walking off leaves whoever stood behind them the head.
	/// </summary>
	private void SeedCellLists( ParkWorld save )
	{
		for ( var cell = 0; cell < _cells.Length; ++cell )
		{
			if ( _cells[cell].Occupant == 0 )
				continue;

			var chain = save.ChainAt( cell % ParkWorld.MapSize, cell / ParkWorld.MapSize );

			for ( var i = 0; i < chain.Count; ++i )
			{
				if ( i > 0 )
					_onCellPrev[chain[i]] = chain[i - 1];

				if ( i + 1 < chain.Count )
					_onCellNext[chain[i]] = chain[i + 1];

				_cellOf[chain[i]] = cell;
			}
		}
	}

	/// <summary>
	/// Puts a thing on a cell, at the head of whatever is already there - the original's
	/// <c>FUN_004d91f0</c>, which ends <c>cell[0x24] = thing</c>.
	/// </summary>
	/// <remarks>
	/// <b>The list is LIFO</b>, and that is the original's own order rather than a convenience: the thing
	/// that arrives most recently becomes the head, and the one before it is linked behind. Everything
	/// that walks onto a cell goes through here, people and placed objects alike - which is why a placed
	/// object's own thing id is what <see cref="RuntimeCell.Occupant"/> holds in the save.
	/// </remarks>
	public void EnterCell( int x, int y, int thingId )
	{
		if ( thingId == 0 || !OnMap( x, y ) )
			return;

		ref var cell = ref CellAt( x, y );
		var head = cell.Occupant;

		if ( head != 0 )
			_onCellPrev[head] = thingId;

		_onCellPrev.Remove( thingId );

		if ( head == 0 )
			_onCellNext.Remove( thingId );
		else
			_onCellNext[thingId] = head;

		cell.Occupant = (ushort)thingId;
	}

	/// <summary>
	/// Takes a thing off a cell, joining up whatever stood either side of it - <c>FUN_004d9280</c>.
	/// </summary>
	/// <remarks>
	/// <b>Only the head's removal moves <see cref="RuntimeCell.Occupant"/></b>; anything else just relinks,
	/// which is what the original does and is the whole point of keeping a list rather than a single slot.
	/// </remarks>
	public void LeaveCell( int x, int y, int thingId )
	{
		if ( thingId == 0 || !OnMap( x, y ) )
			return;

		ref var cell = ref CellAt( x, y );

		var previous = _onCellPrev.GetValueOrDefault( thingId );
		var next = _onCellNext.GetValueOrDefault( thingId );

		if ( previous == 0 )
		{
			// It was the head - but only if the cell really names it, because a thing that was never
			// put here must not silently evict whoever is.
			if ( cell.Occupant == thingId )
				cell.Occupant = (ushort)next;
		}
		else if ( next == 0 )
		{
			_onCellNext.Remove( previous );
		}
		else
		{
			_onCellNext[previous] = next;
		}

		if ( next != 0 )
		{
			if ( previous == 0 )
				_onCellPrev.Remove( next );
			else
				_onCellPrev[next] = previous;
		}

		_onCellNext.Remove( thingId );
		_onCellPrev.Remove( thingId );
	}

	/// <summary>Which cell each thing is currently standing on, so that a move knows what to undo.</summary>
	private readonly Dictionary<int, int> _cellOf = [];

	/// <summary>
	/// Records that a thing is standing on a cell, taking it off whatever cell it was on - the original's
	/// <c>FUN_0050b6a0</c>, whose own assertion is "Attempt to move thing to invalid...".
	///
	/// <para>
	/// <b>It takes where the thing IS, not where it came from, and that is the original's shape rather
	/// than a convenience.</b> <c>FUN_0050b6a0</c>'s first test is whether the cell actually changed, and
	/// only then does it unlink and relink - so a walking guest, who crosses one cell over many steps,
	/// is relinked once rather than put back at the head of their own cell every tick.
	/// </para>
	/// <para>
	/// <b>It also covers a thing being placed for the first time, which a from/to move cannot.</b> The
	/// original links a thing into its cell when it is CREATED (<c>FUN_0050afe0</c>) as well as when it
	/// moves, and asking "where are you now" answers both cases with one call. A person the file holds is
	/// already on their cell's list (<see cref="SeedCellLists"/>), so their first call changes nothing.
	/// </para>
	/// </summary>
	public void StandOn( int thingId, int x, int y )
	{
		if ( thingId == 0 || !OnMap( x, y ) )
			return;

		var cell = (y * ParkWorld.MapSize) + x;

		if ( _cellOf.TryGetValue( thingId, out var was ) )
		{
			if ( was == cell )
				return;

			LeaveCell( was % ParkWorld.MapSize, was / ParkWorld.MapSize, thingId );
		}

		EnterCell( x, y, thingId );

		_cellOf[thingId] = cell;
	}

	/// <summary>
	/// Takes a thing off the map entirely - a guest who has gone home, or a member of staff dismissed.
	///
	/// <para>
	/// <b><see cref="LeaveCell"/> is not enough on its own.</b> It unlinks the cell's own chain but
	/// leaves <see cref="_cellOf"/> naming a cell the thing is no longer on, and that entry is what
	/// <see cref="StandOn"/> consults to decide what to undo - so a thing id handed out again later
	/// would evict whoever is standing where its previous owner used to be. Nothing reuses an id today.
	/// </para>
	/// </summary>
	public void Forget( int thingId )
	{
		if ( thingId == 0 || !_cellOf.TryGetValue( thingId, out var cell ) )
			return;

		LeaveCell( cell % ParkWorld.MapSize, cell / ParkWorld.MapSize, thingId );

		_cellOf.Remove( thingId );
	}

	/// <summary>The thing standing behind this one on the same cell, or nought - the original's thing <c>+10</c>.</summary>
	public int NextOnCell( int thingId ) => _onCellNext.GetValueOrDefault( thingId );

	/// <summary>
	/// The cell a thing is linked into (<see cref="StandOn"/>), or null for one on none - the original's thing bytes
	/// <c>+5</c> and <c>+7</c>, which <c>FUN_004fa990</c> reads.
	/// </summary>
	public (int X, int Y)? CellOf( int thingId )
		=> _cellOf.TryGetValue( thingId, out var cell ) ? (cell % ParkWorld.MapSize, cell / ParkWorld.MapSize) : null;

	/// <summary>
	/// How far a queue walk may go before it is treated as broken. The original uses a thousand in
	/// <c>GetBackOfQueue</c> and complains rather than spinning; this bounds the person walk the same way.
	/// </summary>
	public const int LongestQueue = 1000;

	/// <summary>The guest at the head of this object's queue, or nought - <c>mFirstInQ</c>.</summary>
	public int FirstInQueue( int objectId ) => _queueHead.GetValueOrDefault( objectId );

	/// <summary>The guest behind this one, or nought for the last - <c>mQNext</c>.</summary>
	public int NextInQueue( int guestId ) => _queueNext.GetValueOrDefault( guestId );

	/// <summary>The guest in front of this one, or nought for the first - <c>mQPrev</c>.</summary>
	public int PreviousInQueue( int guestId ) => _queuePrev.GetValueOrDefault( guestId );

	/// <summary>How many are queueing for this object, by walking the links.</summary>
	public int QueueLength( int objectId )
	{
		var length = 0;

		for ( var id = FirstInQueue( objectId ); id != 0 && length < LongestQueue; ++length )
			id = NextInQueue( id );

		return length;
	}

	/// <summary>
	/// How many are queueing for this object as the original counts them - <c>FUN_004ddf50( 0 )</c>: from the head
	/// up to and including the first guest who is no longer queueing (<c>FUN_00502430</c> at <c>0x004ddfa9</c>), and no further. The
	/// ride score's queue term, the queue-room test and the arrival's too-long gate all read it. This class keeps
	/// no guest's state, so the caller supplies the test; without one every link is counted.
	/// </summary>
	public int QueueCount( int objectId, Func<int, bool>? stillQueueing = null )
	{
		var count = 0;

		for ( var id = FirstInQueue( objectId ); id != 0 && count < LongestQueue; id = NextInQueue( id ) )
		{
			++count;

			if ( stillQueueing != null && !stillQueueing( id ) )
				break;
		}

		return count;
	}

	/// <summary>
	/// Where a guest stands in an object's queue, counting from nought, or <b>-1</b> if they are not in
	/// it at all - the original's <c>FUN_004ddf50</c>, whose own assertion reads "GetPositionInQueue:
	/// Could not fi[nd]".
	///
	/// <para>
	/// <b>It is walked rather than stored, and that is the whole point of it.</b> Nothing in the original
	/// ever decrements anybody's <c>mQueuePos</c> when a guest leaves a queue - <c>FUN_004ddd20</c> only
	/// unlinks <c>mQNext</c>/<c>mQPrev</c> and fixes the head - so a recorded place goes stale the moment
	/// the queue moves, and <c>FUN_004ffff0</c> compares it against this on every turn a guest spends
	/// queueing.
	/// </para>
	/// <para>
	/// <b>That comparison is what keeps a queue moving.</b> <see cref="ParkRideOperation.Invite"/> will
	/// only call forward a head whose <c>mQueuePos</c> is nought, and a place written only when a guest
	/// joined would leave the guest who became head carrying the 1 they joined with, refused for ever.
	/// </para>
	/// <para>
	/// <b>With <paramref name="stillQueueing"/> it gives up where the original does</b>: at the first guest
	/// it steps past who is no longer queueing (<c>FUN_00502430</c> at <c>0x004ddfa9</c>), answering -1 for
	/// everybody behind them. The guest sought is never asked. This class keeps no guest's state, so the
	/// caller supplies the test; without one every link is followed.
	/// </para>
	/// </summary>
	public int PositionInQueue( int objectId, int guestId, Func<int, bool>? stillQueueing = null )
	{
		var place = 0;

		for ( var id = FirstInQueue( objectId ); id != 0 && place < LongestQueue; ++place )
		{
			if ( id == guestId )
				return place;

			if ( stillQueueing != null && !stillQueueing( id ) )
				return -1;

			id = NextInQueue( id );
		}

		return -1;
	}

	/// <summary>
	/// Puts a guest at the back of a queue and hands back the place they took, counting from nought -
	/// <c>FUN_004ddb90</c>, whose own line is "Object %d adding person %d to queue".
	///
	/// <para>
	/// <b>It appends at the tail by walking to it</b>, which is what the original does rather than keeping
	/// a back pointer: with an empty queue the joiner becomes the head, and otherwise the last guest's
	/// <c>mQNext</c> is pointed at them. Their own <c>mQPrev</c> becomes whoever was last - nought when the
	/// queue was empty - and their <c>mQNext</c> is cleared.
	/// </para>
	/// </summary>
	public int JoinQueue( int objectId, int guestId )
	{
		var head = FirstInQueue( objectId );

		if ( head == 0 )
		{
			_queueHead[objectId] = guestId;
			_queuePrev.Remove( guestId );
		}
		else
		{
			var last = head;
			var steps = 0;

			while ( NextInQueue( last ) != 0 && ++steps < LongestQueue )
				last = NextInQueue( last );

			_queueNext[last] = guestId;
			_queuePrev[guestId] = last;
		}

		_queueNext.Remove( guestId );

		return QueueLength( objectId ) - 1;
	}

	/// <summary>
	/// Takes a guest out of a queue, joining up whoever stood either side of them - <c>FUN_004ddd20</c>,
	/// which the original follows with two assertions that both of the leaver's links are nought.
	/// </summary>
	/// <remarks>
	/// <b>It splices by the leaver's own links and asks nothing of the queue</b> (<c>0x004ddde9</c>): a leaver
	/// with nobody in front makes whoever is behind them the head, whoever the head was. So a leaver with no
	/// links at all empties the head, and the rest of that queue walks as nought long.
	/// </remarks>
	/// <returns>Whether they were at its head or linked to anybody when they left.</returns>
	public bool LeaveQueue( int objectId, int guestId )
	{
		var wasQueueing = FirstInQueue( objectId ) == guestId
			|| _queueNext.ContainsKey( guestId ) || _queuePrev.ContainsKey( guestId );

		var previous = PreviousInQueue( guestId );
		var next = NextInQueue( guestId );

		if ( previous == 0 )
		{
			if ( next == 0 )
				_queueHead.Remove( objectId );
			else
				_queueHead[objectId] = next;
		}
		else if ( next == 0 )
		{
			_queueNext.Remove( previous );
		}
		else
		{
			_queueNext[previous] = next;
		}

		if ( next != 0 )
		{
			if ( previous == 0 )
				_queuePrev.Remove( next );
			else
				_queuePrev[next] = previous;
		}

		_queueNext.Remove( guestId );
		_queuePrev.Remove( guestId );

		return wasQueueing;
	}

	/// <summary>
	/// Zeroes a guest's own two queue links <b>and nothing else</b> - the writes of <c>mQPrev</c> and
	/// <c>mQNext</c> in <c>FUN_005012f0</c>, when a guest is put out of the queue of a thing being sold.
	/// </summary>
	/// <remarks>
	/// <b>This is not <see cref="LeaveQueue"/></b>: on a sale the original never calls <c>FUN_004ddd20</c>,
	/// so the thing keeps its head and nobody's neighbours are joined up. Everybody in that queue is put
	/// out the same way, so no guest is left with a link. The head stays keyed by a thing id that is never
	/// reused.
	/// </remarks>
	public void ForgetQueueLinks( int guestId )
	{
		_queueNext.Remove( guestId );
		_queuePrev.Remove( guestId );
	}

	/// <summary>
	/// Forgets who is at the front of a queue, <b>and nothing else</b> - the original's own reach when a
	/// ride finds its head is no longer queueing (<c>FUN_004e0b90</c> writes <c>mFirstInQ = 0</c>).
	/// </summary>
	/// <remarks>
	/// <b>The rest of the chain is deliberately left standing, and that is not an oversight to tidy.</b>
	/// The original promotes nobody: whoever was second keeps a <c>mQPrev</c> naming a guest who is no
	/// longer at the front, and the links are repaired by the next join or leave rather than here. So a
	/// queue whose head has been dropped measures <b>nought</b> even while its links remain, because the
	/// walk starts at the head - which is exactly what the engine sees.
	/// </remarks>
	public void ClearQueueHead( int objectId ) => _queueHead.Remove( objectId );

	/// <summary>
	/// The objects whose saved queue measurements have been thrown away, because the player has edited
	/// the cells they were measured from.
	/// </summary>
	private readonly HashSet<int> _queuesInvalidated = [];

	/// <summary>
	/// Throws away what the save recorded about an object's queue, so the next question re-walks the
	/// map - the original's <c>FUN_004de1f0</c>, which zeroes <c>mBackOfQueue</c> for exactly this
	/// reason and then logs <i>"Object's queue is now %d cells long"</i>.
	/// </summary>
	/// <remarks>
	/// <b>Without this, editing a queue would change nothing at all for the two objects that matter.</b>
	/// <see cref="ParkRideChoice.QueueCellsFor"/> returns the save's cached pair whenever it is set,
	/// and the shipped park sets it on the Belly Bounce (4 cells ending 2866) and the Jungle Spray
	/// (1 cell ending 3765) - so those two would keep answering the numbers their file was written
	/// with however many cells the player laid or lifted.
	/// </remarks>
	public void InvalidateQueue( int objectId )
	{
		if ( objectId != 0 )
			_queuesInvalidated.Add( objectId );
	}

	/// <summary>Whether this object's saved queue pair has been thrown away - see <see cref="InvalidateQueue"/>.</summary>
	public bool QueueWasInvalidated( int objectId ) => _queuesInvalidated.Contains( objectId );

	/// <summary>
	/// <c>FUN_004de1f0</c>: the queue is measured again (<see cref="InvalidateQueue"/>) and then everybody in it
	/// is told (<i>"Telling people in queue to reevaluate"</i>), so whoever now stands past its end is put out,
	/// and a closed ride whose queue now joins something is opened again - see
	/// <see cref="ParkPeople.QueueRemeasured"/>, which does both. Every cell edit that can change a queue calls
	/// this at the end of its transaction, the sale's drain once for each run it clears, and a path clear
	/// after unlinking each cardinal entrance.
	/// </summary>
	public void RemeasureQueue( int objectId )
	{
		if ( objectId == 0 )
			return;

		// The counter is taken before the measure and written into the new back cell's block after it
		// (0x004de233, 0x004de266), so a queue edit frees its stranded guests.
		var stamp = NextCounter();

		InvalidateQueue( objectId );

		if ( TryObject( objectId, out var thing ) && ParkRideChoice.QueueCellsFor( _park, thing ) is (var back and not 0, _) )
		{
			var (x, y) = MapStep.CellAt( back );

			if ( OnMap( x, y ) )
				_blockStamps[((y >> 4) * BlocksASide) + (x >> 4)] = stamp;
		}

		QueueRemeasured?.Invoke( objectId );
	}

	/// <summary>
	/// Who is told a queue was measured again - the park's people, which set it when they are made. With
	/// nobody set, <see cref="RemeasureQueue"/> is <see cref="InvalidateQueue"/> alone.
	/// </summary>
	internal Action<int>? QueueRemeasured { get; set; }

	// Who a ride has picked out to load next - the object's own mPersonBeingLoaded at +0x6c, per-object
	// runtime state that admitting reads.
	private readonly Dictionary<int, int> _beingLoaded = [];

	/// <summary>
	/// The guest this ride has nominated to load next, or nought - <c>mPersonBeingLoaded</c>.
	/// </summary>
	/// <remarks>
	/// <b>One at a time, which is the original's own shape rather than a simplification.</b>
	/// <c>FUN_004e0aa0</c> is nothing but <c>person == object[+0x6c]</c>, so a ride holds exactly one
	/// nominee; and <c>FUN_004e0900</c> checks the person it is asked to admit is that one, printing
	/// "admitting wrong person - check d..." when it is not and admitting them all the same.
	/// </remarks>
	public int PersonBeingLoaded( int objectId ) => _beingLoaded.GetValueOrDefault( objectId );

	/// <summary>Nominates a guest to load next, or clears the nomination with nought.</summary>
	public void NominateForLoading( int objectId, int personId )
	{
		if ( personId == 0 )
			_beingLoaded.Remove( objectId );
		else
			_beingLoaded[objectId] = personId;
	}

	/// <summary>How many cells hold litter, which is what a park's cleanliness comes to.</summary>
	/// Dead by CODE: only ParkStateTests calls it.
	public int LitteredCells
	{
		get
		{
			var count = 0;

			foreach ( var cell in _cells )
			{
				if ( cell.Litter != 0 )
					++count;
			}

			return count;
		}
	}
}
