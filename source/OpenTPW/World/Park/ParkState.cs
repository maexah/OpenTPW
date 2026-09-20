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
/// <b>It exists because four separate features were each waiting on it, not as a tidying.</b> Two
/// workarounds in the tree said so outright: <c>PeepBehaviour.Takings</c> and
/// <c>PeepBehaviour.VisitorsToDate</c> each carried a number that belongs to the park, each documented at
/// its own site as living there only because the park had nowhere to keep it. Both are folded in here.
/// The same gap blocked litter on cells and the admission gate's per-cell occupancy.
/// </para>
/// <para>
/// <b>What is deliberately NOT here: per-object dirt.</b> It wants a consumer, and nothing reads it yet -
/// a layer built for a consumer that does not exist is the speculative kind this project does not add.
/// <b>Ride state was named here too, and no longer belongs in this list:</b> a ride does operate now, so
/// the takings and the per-object nominee below arrived with the consumer that needed them. The cells
/// are the same case: they are read by the handyman's litter search.
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
	/// Seeded from the save. A null park gives an empty, open one - the same answer
	/// <see cref="PeepBehaviour"/> already gives for a null park, and for the same reason: a park with
	/// nothing loaded is not a park whose gates are shut.
	/// </summary>
	public ParkState( ParkWorld? park )
	{
		Balance = park?.Economy?.Balance ?? 0;
		VisitorsToDate = park?.NumberOfVisitorsToDate ?? 0;
		ParkIsClosed = park is not null && park.ParkClosed != 0;

		_cells = new RuntimeCell[ParkWorld.MapSize * ParkWorld.MapSize];

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
	/// <see cref="PeepBehaviour"/> already has, and it exists for the same reason: the only park that can
	/// be loaded is saved open, so every branch that turns on a shut park would otherwise be unreachable.
	/// </summary>
	public ParkState( bool parkIsClosed, int visitorsToDate, int balance = 0 )
	{
		ParkIsClosed = parkIsClosed;
		VisitorsToDate = visitorsToDate;
		Balance = balance;
		_cells = new RuntimeCell[ParkWorld.MapSize * ParkWorld.MapSize];
	}

	/// <summary>
	/// What the park is worth now - the balance the save was left with, moved by everything since.
	///
	/// <para>
	/// <b>This is one number where it used to be two.</b> The interface added the save's balance to a
	/// running total held on the behaviours, because nothing could move the saved one. It can now, which
	/// is what <see cref="Take"/> does - and that matches the original, where taking a fee adds it
	/// straight onto <c>mBalance</c> (<c>FUN_004d0600</c>).
	/// </para>
	/// </summary>
	public int Balance { get; private set; }

	/// <summary>
	/// What has been taken at the gate since the park opened, kept beside <see cref="Balance"/> rather
	/// than folded into it because it answers a different question - the balance is a position and this is
	/// a flow. The original keeps both too, adding a fee to <c>mBalance</c> and to
	/// <c>mProfitThisYear</c> alike.
	/// </summary>
	public int Takings { get; private set; }

	/// <summary>
	/// How many guests this park has ever admitted, counting on from what the save recorded - the
	/// original's <c>world + 0x1da714</c>, moved in exactly one place, by a guest finishing at the gate.
	/// </summary>
	public int VisitorsToDate { get; private set; }

	/// <summary>Whether the park is shut to visitors, as the save left it - <b>zero is open</b>.</summary>
	public bool ParkIsClosed { get; }

	/// <summary>
	/// Takes an admission fee: onto the balance and onto the running total alike, which is the one place
	/// the two move together.
	/// </summary>
	public void Take( int fee )
	{
		Balance += fee;
		Takings += fee;
	}

	/// <summary>Admits one guest and hands back which visitor they are, counting from one.</summary>
	public int Admit() => ++VisitorsToDate;

	/// <summary>What this object has taken, counting on from what the save recorded.</summary>
	public int TakingsFor( int objectId ) => _takings.GetValueOrDefault( objectId );

	/// <summary>
	/// Credits an object with what a guest has just paid it - the object half of <c>FUN_004e16b0</c>,
	/// which adds the price to <c>mTotalTakings</c> at <c>+0x180</c>.
	///
	/// <para>
	/// <b>It deliberately does NOT move <see cref="Balance"/>, and that is the original's arrangement
	/// rather than an omission.</b> An admission fee goes through <c>FUN_004d0600</c>, which adds it
	/// straight onto <c>mBalance</c> - that is what <see cref="Take"/> reproduces. A charge for a ride or a
	/// shop goes through <c>FUN_004e16b0</c> instead, which credits the object and one of two GLOBAL income
	/// pools chosen by the item descriptor's <c>+0x4ac</c> (<c>+0x20130</c> for rides, <c>+0x20380</c> for
	/// shops) and never touches the balance at all. Moving the park's money here so that the interface
	/// reacted would be inventing behaviour the original does not have.
	/// </para>
	/// <para>
	/// <b>Those two global pools are NOT reproduced</b>, and nothing here keeps them: they are counters on
	/// the world that no screen this project draws has ever read.
	/// </para>
	/// </summary>
	public void TakeAt( int objectId, int amount )
	{
		if ( objectId == 0 || amount == 0 )
			return;

		_takings[objectId] = TakingsFor( objectId ) + amount;
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

	// The queues - once the only structure a running park changed that the save could not hold for it,
	// and now one of three, beside the takings and the per-object nominee below:
	// ParkWorld describes a file and is immutable, so a guest joining a queue has nowhere to write. The
	// shape is the original's own - a head on the object (mFirstInQ) and a doubly-linked list through the
	// guests themselves (mQNext, mQPrev) - kept here rather than on Peep so that the whole structure lives
	// in one place and is seeded once.
	/// <summary>
	/// What each object has taken, by thing id - the original's <c>mTotalTakings</c> at the object's
	/// <c>+0x180</c>, which a charge moves and <see cref="ParkWorld"/> cannot because it describes a file.
	/// </summary>
	private readonly Dictionary<int, int> _takings = [];

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
	/// moves. This method was written as a from/to move first, and the cost showed up in a running park
	/// immediately: <b>the one guest the save leaves standing still was never in any cell's list</b>, so
	/// the gate could not see them and they waited at the booth for ever while the five who walked there
	/// went through. Asking "where are you now" answers both cases with one call.
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
	/// Takes a thing off the map entirely - a guest who has gone home, which is the only thing that
	/// leaves a park so far.
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
	/// <b>Leaving that comparison unbuilt is what stopped every queue in the park dead after one rider,
	/// and it took playing the game to see it.</b>
	/// <see cref="ParkRideOperation.Invite"/> will only call forward a head whose <c>mQueuePos</c> is
	/// nought; <see cref="PeepBehaviour"/> wrote the place once when a guest joined and never again, so
	/// the guest who became head still carried the 1 they joined with and was refused for ever. Measured
	/// in a running park: one guest rode, and the three behind them stood on the same cell for the whole
	/// of the rest of the run.
	/// </para>
	/// </summary>
	public int PositionInQueue( int objectId, int guestId )
	{
		var place = 0;

		for ( var id = FirstInQueue( objectId ); id != 0 && place < LongestQueue; ++place )
		{
			if ( id == guestId )
				return place;

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
	/// <returns>Whether they were in that queue to begin with.</returns>
	public bool LeaveQueue( int objectId, int guestId )
	{
		var wasQueueing = FirstInQueue( objectId ) == guestId
			|| _queueNext.ContainsKey( guestId ) || _queuePrev.ContainsKey( guestId );

		if ( !wasQueueing )
			return false;

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

		return true;
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

	// Who a ride has picked out to load next - the object's own mPersonBeingLoaded at +0x6c. It is
	// per-object runtime state, which this class deliberately had none of; the remarks at the top said so
	// and said why ("a layer built for a consumer that does not exist"). Admitting IS that consumer now.
	private readonly Dictionary<int, int> _beingLoaded = [];

	/// <summary>
	/// The guest this ride has nominated to load next, or nought - <c>mPersonBeingLoaded</c>.
	/// </summary>
	/// <remarks>
	/// <b>One at a time, which is the original's own shape rather than a simplification.</b>
	/// <c>FUN_004e0aa0</c> is nothing but <c>person == object[+0x6c]</c>, so a ride holds exactly one
	/// nominee; and <c>FUN_004e0900</c> asserts the person it is asked to admit is that one, printing
	/// "admitting wrong person - check d..." when it is not.
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
