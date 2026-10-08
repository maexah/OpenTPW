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
/// <b>What is deliberately NOT here: per-object dirt.</b> Nothing built writes or reads it: a toilet's dirtying
/// and its dirt gate are both counted. What a built path writes is kept, seeded from the save, even where the
/// original's reader is not built: each object's takings and costs and the bank's red tick are read here only by
/// the console, and each one's remarks name the reader that is missing. A ride's operation reads the per-object
/// nominee and the gate reads the cells' occupant lists; the cells' litter is read only by
/// <see cref="LitteredCells"/>, because the handyman's litter search is not built (<see cref="StaffBehaviour"/>).
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

	/// <summary>Whether any cell has been changed at all - what a rebuild can skip on.</summary>
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

	/// <summary>The head of the object chain - the save's <c>mFirstObject</c>, the engine's <c>+0x1da746</c>.</summary>
	private int _firstObject;

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
	/// The next free thing id. <b>Objects and people share one numbering, and this is the only thing
	/// that hands ids out of it.</b>
	///
	/// <para>
	/// <b>It counts rather than rescanning</b>, so that there is one allocator over the one space: a rescan
	/// never sees a hired staff member, and a private counter never sees a bought object.
	/// </para>
	/// <para>
	/// Counting also stops an id being handed out twice after the thing holding it is sold, which a
	/// rescan would do as soon as the highest-numbered object was demolished.
	/// </para>
	/// <para>
	/// <b>A deviation:</b> the original takes the first slot of its free list (<c>FUN_00516270</c>), so an id a
	/// thing gave up is handed out again: measured, the first guest of a load took 38, which a leaver had freed, and
	/// the rest 43 on (<c>docs/exe/ride-operation.md</c>, "Where a ride's turn comes from"). Here no id is used twice.
	/// </para>
	/// </summary>
	public int NextThingId()
	{
		// Seeded on first use rather than at load, so a state that was never bound to a park still
		// answers something usable - nought is the sentinel for "nothing", never an id.
		if ( _nextThingId == 0 )
			_nextThingId = HighestThingId() + 1;

		return _nextThingId++;
	}

	private int _nextThingId;

	/// <summary>The highest id the file and everything built since have used.</summary>
	private int HighestThingId()
	{
		var highest = 0;

		foreach ( var placed in _objects )
			highest = Math.Max( highest, placed.ThingId );

		if ( _park != null )
		{
			foreach ( var thing in _park.Things )
				highest = Math.Max( highest, thing.ThingId );
		}

		return highest;
	}

	/// <summary>
	/// Adds something built, and links it into the object chain at the <b>head</b> - the original's
	/// <c>FUN_00519d80</c>, whose only caller is the object constructor.
	/// </summary>
	/// <remarks>
	/// Without the link the thing stands, draws and runs its script, and is still never offered to
	/// anybody: the walk a guest's choice makes starts at <c>mFirstObject</c>, so an object in no chain
	/// cannot be reached however the list beside it is built.
	/// </remarks>
	public void AddObject( ParkWorld.CatalogueObject placed )
	{
		_objects.Add( placed );
		_rings[placed.ThingId] = new ParkObjectRings();

		_nextObject[placed.ThingId] = _firstObject;
		_firstObject = placed.ThingId;
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

		_objects.RemoveAt( at );
		_hoardings.Remove( thingId );
		_rings.Remove( thingId );
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

		// Everything the file placed, copied so that what is built and sold afterwards moves here rather
		// than in ParkWorld, which describes a file.
		_objects.AddRange( park.Objects );

		// And the chain that threads them, which the original keeps live and this one now does too - see
		// ObjectsInChainOrder. Seeded from the save's own head and links, so a park straight out of the
		// file is considered in exactly the order the file's chain gives.
		_firstObject = park.FirstObject;

		foreach ( var thing in park.Objects )
			_nextObject[thing.ThingId] = thing.NextObject;

		// And the queues as the save left them. Every one is empty in the park that ships - nobody has ever
		// been admitted to it - so this seeds nothing today and is still what makes a saved queue survive
		// being loaded, rather than the park quietly starting everybody at the front.
		foreach ( var thing in park.Objects )
		{
			if ( thing.FirstInQueue != 0 )
				_queueHead[thing.ThingId] = thing.FirstInQueue;

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
	/// Nothing reads it here. The original's readers, golden ticket 4 and an advisor row, are not built.
	/// </remarks>
	public int ProfitThisYear { get; private set; }

	/// <summary>
	/// How many guests this park has ever admitted, counting on from what the save recorded - the
	/// original's <c>world + 0x1da714</c>, moved in exactly one place, by a guest finishing at the gate.
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
	/// A thing sweep begins: the clock goes one up before anything in the sweep runs, as <c>FUN_00516380</c> does at
	/// <c>0x00516394</c>. <see cref="ParkPeople"/> runs the sweep and is the one caller.
	/// </summary>
	public int AdvanceGameTick() => ++GameTick;

	/// <summary>
	/// Writes the park's clock, for the console's <c>clock</c>: an instrument, as a write to <c>mGameTick</c> in
	/// the original's memory is. Every stamp already taken stays as it was.
	/// </summary>
	internal void SetGameTick( int tick ) => GameTick = tick;

	/// <summary>
	/// Now on the park's own calendar - <c>FUN_004f8690</c>: <c>mFunnyTimeStart</c> plus <c>mGameTick ×
	/// mFunnySecsPerRealSec / 4</c> whole seconds, the tick taken unsigned. A thing built is stamped with it
	/// (<c>FUN_004db090</c>, <c>0x004db66a</c>) and its age is measured against it (<see cref="AgeInDays"/>).
	/// </summary>
	/// <remarks>
	/// The start and the rate are <see cref="GameCalendar.Epoch"/> and <see cref="GameCalendar.Rate"/>, the clock
	/// constructor's own: the save's clock block is not read, and Lost Kingdom's holds the same two (the rate
	/// <c>docs/exe/weather.md</c>, the start <c>docs/exe/ride-operation.md</c>, "What it gives the three rides"). This
	/// is not <see cref="GameCalendar.Now"/>, which counts from nought (Q149).
	/// </remarks>
	public DateTime CalendarNow
		=> GameCalendar.Epoch.AddTicks( (long)(uint)GameTick * GameCalendar.Rate / GameCalendar.AdvancesPerSecond
			* TimeSpan.TicksPerSecond );

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
	/// the original's <c>FUN_00519ef0( open, 0 )</c>.
	/// </summary>
	/// <remarks>
	/// <b>Each arm acts only on a change</b> (<c>0x00519f76</c>, <c>0x0051a09e</c>). It sets
	/// <see cref="ParkIsClosed"/>, which <see cref="PeepBehaviour"/> reads at the gate, and hands the rides to
	/// <see cref="DoorMoved"/>: closing closes every object a guest may be offered (<c>0x0051a1ae</c>), opening
	/// opens each that <see cref="ParkRideOperation.MayOpen"/> allows (<c>0x0051a01e</c>).
	/// <para>
	/// The people command the gate script - opening writes 1, closing writes 0
	/// only when nobody is in the park and the gate reads open (<c>0x0051a0e8</c>..<c>0x0051a161</c>) - and
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
	}

	/// <summary>
	/// Who closes and opens the rides when the park's door moves - the park's people, which set it when they
	/// are made (<c>ParkPeople.DoorMoved</c>). With nobody set, the flag and advisor still receive the request.
	/// </summary>
	internal Action<bool>? DoorMoved { get; set; }

	/// <summary>The park's posted advisor messages, installed during level setup.</summary>
	internal ParkAdvisorMessages? AdvisorMessages { get; set; }

	/// <summary>
	/// Takes an admission fee - the bank's <c>FUN_004d0600</c>: onto the balance and <see cref="ProfitThisYear"/>,
	/// the deposit's adds, and onto the gate's running total, which only this moves (<c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's money").
	/// </summary>
	/// <remarks>
	/// Counted, not built: the park analyser's month cash in and gate takings (<c>+0x1fc90</c>, <c>+0x1fee0</c>),
	/// its lifetime visitors and dearest ticket, and the message that counts a new visitor toward a challenge of
	/// type 11, which takes it only while one is on.
	/// </remarks>
	public void Take( int fee )
	{
		unchecked
		{
			Balance += fee;
			ProfitThisYear += fee;
		}

		Takings += fee;

		Log.Info( $"Bank: admission {fee}, balance {Balance}, profit {ProfitThisYear}" );

		Unimplemented.Report( "BANK_ANALYSER_MONEY_IN" );
		Unimplemented.Report( "GATE_FEE_ANALYSER_TOTALS" );
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
	/// nought still writes the last balance. Counted, not built: the park analyser's month money out
	/// (<c>+0x1f5a0</c>).
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

			Unimplemented.Report( "BANK_ANALYSER_MONEY_OUT" );

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
	/// bare <c>RET</c> and refuses nothing. Counted, not built: the park analyser's month cash in (<c>+0x1fc90</c>).
	/// </para>
	/// </summary>
	public void Deposit( int amount )
	{
		unchecked
		{
			Balance += amount;

			Unimplemented.Report( "BANK_ANALYSER_MONEY_IN" );

			ProfitThisYear += amount;
		}

		Log.Info( $"Bank: deposit {amount}, balance {Balance}, profit {ProfitThisYear}" );
	}

	/// <summary>Admits one guest and hands back which visitor they are, counting from one.</summary>
	public int Admit() => ++VisitorsToDate;

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
	/// (<c>docs/exe/ride-operation.md</c>, "The month's change"). The analyser's close of the month is counted. The
	/// bank's turn, <c>FUN_004d0370</c>: <see cref="BatchBalance"/> deposited and zeroed; each bought loan's
	/// instalment withdrawn, its months repaid counted, <see cref="ProfitThisYear"/> given the principal's share back
	/// by the original's unsigned division, and the loan closed at its period; then, with the balance and
	/// <see cref="LastBalance"/> both below nought, the whole thirty-day months since <see cref="TurnEnteredRed"/> -
	/// at six the park ends, counted.
	/// </summary>
	public void TurnTheMonth()
	{
		Unimplemented.Report( "ANALYSER_MONTH_CLOSE" );

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
	/// <remarks>
	/// The edge is <see cref="GameCalendar"/>'s, which counts from nought rather than from the save's clock, so after
	/// a load the year turns at another moment than the original's would (<c>docs/QUEUE.md</c> Q149).
	/// </remarks>
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
	// the whole structure lives in one place and is seeded once.
	private readonly Dictionary<int, int> _onCellNext = [];
	private readonly Dictionary<int, int> _onCellPrev = [];

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
	/// moves, and asking "where are you now" answers both cases with one call: a guest the save leaves
	/// standing still is entered into their cell's list on their first turn.
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
	/// would evict whoever is standing where its previous owner used to be. Nothing reuses an id today,
	/// which is exactly why this would have gone unnoticed.
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
