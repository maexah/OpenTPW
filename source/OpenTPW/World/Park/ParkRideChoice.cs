namespace OpenTPW;

/// <summary>
/// Which of a park's objects a guest may be offered right now - the original's <c>FUN_004dd920</c>, the
/// gate every candidate passes before it is worth scoring at all.
///
/// <para>
/// <b>This is the filter, not the choice.</b> <c>FUN_004fcb10</c> walks the object list, asks this of each
/// candidate, scores the survivors with <c>FUN_004fcc30</c> and takes the best. Only the asking is here;
/// the walk is <see cref="ParkRideChooser"/> and the scoring, a seven-term weighted mean, is
/// <see cref="ParkRideScore"/>.
/// </para>
/// <para>
/// <b>The track-type arms ARE reproduced.</b> The
/// original refuses a candidate of type 1 or 2 - a car track or a water track - unless
/// <c>mIsTrackRideValid</c> is set. The field is <c>Bumper.WhichTrackType</c> on the item's own
/// description, identified across the whole catalogue rather than from one item: the jungle's
/// <c>Dino Karts</c> is 1, <c>Splish Splash</c> is 2, its three coasters are 3, and every other ride is
/// nought.
/// </para>
/// <para>
/// <b>ONE arm is still not reproduced, and it is a different function rather than a missing field.</b> A
/// type 3 candidate - a coaster - is additionally refused unless <c>FUN_00441970</c> passes, and that is
/// a check on the thing's MODEL and its flags rather than on the item. Nothing here models that, so a
/// coaster is admitted where the original might refuse it. <b>The shipped park contains no coaster</b>,
/// so the arm cannot fire in it either way.
/// </para>
/// </summary>
public static class ParkRideChoice
{
	/// <summary>
	/// How many people may queue per cell of queue before the object stops being offered -
	/// <c>FUN_004dd920</c> compares the queue's length against <c>mQueueSizeInCells</c> shifted left twice.
	/// </summary>
	public const int QueueRoomPerCell = 4;

	/// <summary>
	/// The two states the offer gate refuses by number: 1 is broken down and 4 is condemned, the values
	/// <c>FUN_004e14e0</c> hands to SetState (docs/exe/ride-operation.md, "Where an object's state comes
	/// from"). State 2, an upgrade waiting to be done, is refused through <c>CanLoad</c>, which SetState
	/// clears; state 3 through <c>IsVisitable</c>, since only an object that is not visitable is given it.
	/// The shipped park holds only 0 and 3, so neither value here occurs in it.
	/// </summary>
	public const int StateRefusedOne = 1;

	/// <inheritdoc cref="StateRefusedOne"/>
	public const int StateRefusedFour = 4;

	/// <summary>
	/// Whether a guest may be offered this object, given how many people are already queueing for it.
	/// </summary>
	/// <param name="queueLength">
	/// How many are in its queue now. The save leaves every <c>mFirstInQ</c> at nought - nobody has ever
	/// queued in this park - so a park read from the file starts every queue empty and fills it as guests
	/// join.
	/// </param>
	/// <param name="trackType">
	/// The item's <c>Bumper.WhichTrackType</c>, or nought where the catalogue cannot say. A car track or a
	/// water track needs its track ride to be valid before anyone may be sent to it.
	/// </param>
	/// <param name="park">
	/// The park this object stands in, so that its queue can be <b>walked on the map</b> rather than read
	/// from the save - see <see cref="QueueCellsFor"/>, which is what lets the Drinks Shop and the
	/// three toilets be offered. <b>Null falls back to the save's own cached pair</b>, which is
	/// what the original does whenever <c>mBackOfQueue</c> is already set, and is the honest answer for a
	/// test holding an object with no world around it.
	/// </param>
	public static bool CanBeOffered( ParkWorld.CatalogueObject item, int queueLength, int trackType = 0,
		ParkWorld? park = null )
	{
		// A tracked ride whose track is not valid is not open, whatever else is true of it.
		if ( trackType is ItemDescriptionFile.CarTrack or ItemDescriptionFile.WaterTrack
			&& item.IsTrackRideValid == 0 )
			return false;

		// A guest may only be sent somewhere the item's own description says they may - see
		// CatalogueObject.IsVisitable, which is the save's side of Info.IsChoosable.
		if ( !item.IsVisitable )
			return false;

		if ( item.State is StateRefusedOne or StateRefusedFour )
			return false;

		// mCanLoad. The original tests it for non-zero rather than for a particular value, and so does this.
		if ( item.CanLoad == 0 )
			return false;

		// It must have somewhere to be approached from. An unplaced object carries the sentinel entry, and
		// walking to it would send a guest to the corner of the map.
		if ( !item.IsPlaced || item.EntryPos == 0 )
			return false;

		var (backOfQueue, cells) = QueueCellsFor( park, item );

		// GetBackOfQueue answering nought is the original's FIRST refusal, and it is a different one from
		// having no room: FUN_004dd920 tests the returned cell before it ever reaches the multiply, so an
		// object with nowhere for a queue to begin never gets as far as counting anybody.
		if ( backOfQueue == 0 )
			return false;

		return HasQueueRoom( queueLength, cells );
	}

	/// <summary>
	/// Whether the queue has room - <c>queueLength &lt; cells * 4</c>, the original's <c>FUN_004dda20</c>.
	/// </summary>
	/// <remarks>
	/// <b>The count is the one <see cref="QueueCellsFor"/> produces, NOT <c>mQueueSizeInCells</c> read
	/// straight out of the save</b>.
	/// The two agree wherever the save carries a cached pair and differ on exactly the objects that do not.
	/// </remarks>
	public static bool HasQueueRoom( int queueLength, int cells )
		=> queueLength < cells * QueueRoomPerCell;

	/// <summary>The cell type a queue is laid out on - <c>FUN_00536320</c> accepts 3 and 9, and
	/// <c>FUN_00536340</c> then rejects 9, so a queue cell is exactly <b>3</b>.</summary>
	public const int QueueCellType = 3;

	/// <summary>
	/// A cell as the RUNNING park holds it: the player's changes first, and the file for everything
	/// nobody has touched.
	///
	/// <para>
	/// <b>A queue cell a player has just laid exists only in the overlay</b> - <see cref="ParkWorld"/>
	/// describes the file and may never be written to - so a walk that asked the save would step
	/// straight past it and report the queue short by exactly the cells the player had built. It falls
	/// through to the save for every cell nobody has changed, so a park nobody has edited answers
	/// exactly as it did before.
	/// </para>
	/// </summary>
	private static ParkWorld.MapCell LiveCell( ParkWorld park, int x, int y )
		=> ParkState.CellFor( park, x, y );

	/// <summary>
	/// Where an object's queue begins - the original's <c>FUN_004de040</c>.
	///
	/// <para>
	/// <b>It reads the entry cell's <c>mNeighbours</c>, not its <c>mDirection</c>, and that is the single
	/// fact this whole feature turned on.</b> <c>FUN_004de040</c> calls <c>FUN_00522770</c> with the
	/// object's own entry cell - <c>LEA ECX,[EDX + ECX*0x4 + -0x44]</c> built from <c>mEntryPos</c> - and
	/// that function is a one-line read of the cell's <c>+0xc</c>, which the game's own cell serialiser
	/// (<c>FUN_004d0b30</c>) names <c>mNeighbours</c>. It then takes the <b>first bit that is set</b>, in a
	/// fixed order that is not compass order, and steps one cell that way.
	/// </para>
	/// <para>
	/// <b>The bit-to-vector table is the executable's own</b>, out of the jump table in
	/// <c>FUN_004d97e0</c> (<c>CMapCell::GetNeighbouringCell( Direction )</c>, named by its own assert):
	/// <c>0x01</c> is <c>(0,-1)</c>, <c>0x10</c> is <c>(0,+1)</c>, <c>0x40</c> is <c>(-1,0)</c> and
	/// <c>0x04</c> is <c>(+1,0)</c>.
	/// </para>
	/// <para>
	/// <b>These constants are the mirror of <see cref="CellEdge.BitFor"/> and neither is wrong.</b> That
	/// table answers a different question - the bit of the cell being <i>entered</i>, on the side facing the
	/// cell being left - so it is reverse-facing by design. This one is an outward step. Reading either as
	/// the other inverts every answer, which is why they are written out separately instead of shared.
	/// </para>
	/// <para>
	/// <b>No check of any kind is made on the cell it lands on</b> - not its type, not the map's edge.
	/// <c>FUN_004de040</c>'s whole body holds exactly one call and it is the neighbours read; the checking
	/// belongs to <see cref="StepToNextQueueCell"/>. Adding a guard here would quietly change which objects
	/// have a queue at all.
	/// </para>
	/// </summary>
	/// <returns>The packed cell the queue starts at, or nought where the entry cell connects to nothing.</returns>
	public static int StartOfQueue( ParkWorld? park, ParkWorld.CatalogueObject item )
	{
		if ( park == null || item.EntryPos == 0 )
			return 0;

		if ( !ParkState.OnMap( item.EntryCellX, item.EntryCellY ) )
			return 0;

		var connections = LiveCell( park, item.EntryCellX, item.EntryCellY ).Neighbours;

		foreach ( var (bit, acrossBy, downBy) in StartSides )
		{
			if ( (connections & bit) != 0 )
				return item.EntryPos + (downBy * ParkWorld.MapSize) + acrossBy;
		}

		return 0;
	}

	/// <summary>
	/// The four sides <c>FUN_004de040</c> tries, <b>in its own order</b> - the bit it tests in
	/// <c>mNeighbours</c> and the step that bit stands for. The order is the tested order and not compass
	/// order, and it decides which way a queue runs when an entry cell connects two ways.
	/// </summary>
	private static readonly (int Bit, int AcrossBy, int DownBy)[] StartSides =
	[
		(0x01, 0, -1),
		(0x10, 0, 1),
		(0x40, -1, 0),
		(0x04, 1, 0)
	];

	/// <summary>
	/// The four probes <c>FUN_004de670</c> makes, in its own order: the step, and the <c>mDirection</c> the
	/// cell it finds must carry.
	/// </summary>
	/// <remarks>
	/// <b>Each expected value is the OPPOSITE of the step</b>, so a queue cell's direction points back down
	/// the queue toward the thing it serves. It is an equality test on the whole byte rather than a mask,
	/// which is the original's own comparison - a cell carrying two direction bits matches none of them.
	/// <b>Note the order differs from <see cref="StartSides"/></b>: this one probes east before west.
	/// </remarks>
	private static readonly (int AcrossBy, int DownBy, int FacingBack)[] StepSides =
	[
		(0, -1, 0x10),
		(0, 1, 0x01),
		(1, 0, 0x40),
		(-1, 0, 0x04)
	];

	/// <summary>
	/// One step outward along a queue - the original's <c>FUN_004de670</c>.
	///
	/// <para>
	/// <b>This is where the checking lives.</b> A neighbour counts only if it is on the map, its type is
	/// exactly <see cref="QueueCellType"/> - <c>FUN_00536320</c> admits 3 or 9 and <c>FUN_00536340</c> then
	/// rejects 9, so the ride-end cells a queue touches are excluded - and its own <c>mDirection</c> equals
	/// the bit facing back the way this step came.
	/// </para>
	/// </summary>
	/// <returns>The packed cell one further along, or nought at the end of the queue.</returns>
	public static int StepToNextQueueCell( ParkWorld? park, int cellId )
	{
		if ( park == null || cellId == 0 )
			return 0;

		var (x, y) = MapStep.CellAt( cellId );

		foreach ( var (acrossBy, downBy, facingBack) in StepSides )
		{
			var (nextX, nextY) = (x + acrossBy, y + downBy);

			if ( !ParkState.OnMap( nextX, nextY ) )
				continue;

			var cell = LiveCell( park, nextX, nextY );

			if ( cell.Type == QueueCellType && cell.Direction == facingBack )
				return MapStep.CellId( nextX, nextY );
		}

		return 0;
	}

	/// <summary>
	/// Where an object's queue ends and how many cells long it is - the original's <c>FUN_004de130</c>,
	/// <c>GetBackOfQueue</c>, named by its own <c>"*** GetBackOfQueue() crashed! ***"</c>.
	///
	/// <para>
	/// <b>This is what makes the Drinks Shop reachable.</b> The offer filter compares a queue's length
	/// against the object's <c>+0x40</c>, <c>mQueueSizeInCells</c>, which the save holds as nought for the
	/// shop and for all three toilets. The save
	/// loader does write that field (<c>FUN_004db7d0</c> names it at <c>004dcde6</c>), but
	/// <c>FUN_004de130</c> <b>overwrites it</b> by walking the map whenever <c>mBackOfQueue</c> is nought -
	/// and the filter calls this before it reads the count. So <c>+0x40</c> is a <b>cache</b>, and the save's
	/// copy is the cached answer to this very walk rather than a declaration.
	/// </para>
	/// <para>
	/// <b>The shipped park proves the walk twice over, which is why it can be trusted.</b> Run against the
	/// two objects whose save carries a cached pair, it reproduces both exactly: the Jungle Spray starts at
	/// (52,29), finds no queue cell beyond it and comes to <b>1 cell ending at 3765</b>, which is what its
	/// record holds; the Belly Bounce starts at (52,22) and walks (51,22), (50,22), (49,22) - each type 3
	/// with <c>mDirection</c> <c>0x04</c> - for <b>4 cells ending at 2866</b>, which is what its record
	/// holds. Neither number was put in.
	/// </para>
	/// <para>
	/// <b>A start cell that exists but leads nowhere still counts as one.</b> The loop body runs before its
	/// step can fail, so an object whose entry cell merely touches a path has a queue of one - which is
	/// four places by the <c>* 4</c> rule, and is exactly how a shop with no queue drawn on the ground comes
	/// to be offerable at all.
	/// </para>
	/// </summary>
	/// <returns>The packed back-of-queue cell and the number of cells, or <c>(0, 0)</c> for neither.</returns>
	public static (int BackOfQueue, int Cells) QueueCellsFor( ParkWorld? park, ParkWorld.CatalogueObject item )
	{
		// The cached pair. The original returns mBackOfQueue without touching the count whenever it is set,
		// which is what leaves the ride and the sideshow on the numbers their file was saved with.
		//
		// UNLESS THE PLAYER HAS EDITED THE CELLS IT WAS MEASURED FROM. FUN_004de1f0 zeroes mBackOfQueue
		// precisely so the next question re-walks the map, and without that the shipped park's two
		// cached objects - the Belly Bounce and the Jungle Spray - would answer their file's numbers
		// however much queue was laid or lifted. ParkState.InvalidateQueue is where that is recorded.
		if ( item.BackOfQueue != 0 && ParkState.Current?.QueueWasInvalidated( item.ThingId ) != true )
			return (item.BackOfQueue, item.QueueSizeInCells);

		var cell = StartOfQueue( park, item );

		if ( cell == 0 )
			return (0, 0);

		var back = cell;
		var cells = 0;

		// Bounded exactly as the original bounds it - a thousand, after which it complains rather than
		// spinning. See ParkState.LongestQueue, which carries the same constant for the person walk.
		while ( cell != 0 && cells < ParkState.LongestQueue )
		{
			back = cell;
			cell = StepToNextQueueCell( park, cell );

			++cells;
		}

		return (back, cells);
	}

	/// <summary>
	/// How many guests are actually queueing for this object, by walking the queue itself - the original's
	/// <c>GetPositionInQueue</c> (<c>FUN_004ddf50</c>) counting to the end instead of to a particular
	/// person.
	///
	/// <para>
	/// <b>The walk starts at the object's <c>mFirstInQ</c> and follows each guest's own <c>mQNext</c></b>,
	/// which is a thing handle and not a cell - the field two bytes before it,
	/// <see cref="ParkWorld.CatalogueObject.BackOfQueue"/>, IS a cell, and confusing the two is the trap
	/// that record's own remarks warn about.
	/// </para>
	/// <para>
	/// <b>Every queue in the shipped park measures nought, and that is the park rather than the walk.</b>
	/// Its <c>mFirstInQ</c> is nought on every object because nobody has ever been admitted to it -
	/// <c>mNumberOfVisitorsToDate</c> is nought too. This reads the save alone; the queues a running park
	/// fills through <c>PeepBehaviour.JoinTheQueue</c> are counted by <see cref="ParkState.QueueLength"/>.
	/// </para>
	/// </summary>
	public static int QueueLength( ParkWorld? park, ParkWorld.CatalogueObject item )
	{
		if ( park == null )
			return 0;

		var guests = new Dictionary<int, ParkWorld.GuestState>();

		foreach ( var person in park.People )
		{
			if ( person.Guest is { } guest )
				guests[person.ThingId] = guest;
		}

		return QueueLengthFrom( item.FirstInQueue,
			id => guests.TryGetValue( id, out var guest ) ? guest.QNext : null, guests.Count );
	}

	/// <summary>
	/// The walk itself, over whatever can answer "who is behind this one" - which is what makes it
	/// testable at all.
	/// </summary>
	/// <remarks>
	/// <b>The shipped park cannot exercise this, so the seam is the test.</b> Every one of its queues is
	/// empty, so any assertion made against it would pass just as happily against a method that returned
	/// nought and walked nothing. Taking the link lookup as a parameter is what lets a real chain - and a
	/// circular one - be put through it.
	/// </remarks>
	/// <param name="nextOf">
	/// The <c>mQNext</c> of a guest, or null for a handle that names nobody - which ends the walk, as a
	/// queue naming a thing that is not a guest does in the original.
	/// </param>
	/// <param name="guests">
	/// How many guests there could be, bounding the walk so a circular chain stops instead of hanging -
	/// the same guard <see cref="Offerable"/> puts on the object list, and for the same reason.
	/// </param>
	public static int QueueLengthFrom( int firstInQueue, Func<int, int?> nextOf, int guests )
	{
		ArgumentNullException.ThrowIfNull( nextOf );

		var length = 0;

		for ( var id = firstInQueue; id != 0 && length <= guests; ++length )
		{
			if ( nextOf( id ) is not { } next )
				break;

			id = next;
		}

		return length;
	}

	/// <summary>
	/// Every object a guest could be offered, walked in the order the original walks them - from the
	/// header's <c>mFirstObject</c> along each object's own <c>mNext</c>, rather than in the order the
	/// reader happens to hold them.
	///
	/// <para>
	/// <b>This walks the FILE's chain, so it reaches only what the save placed.</b> The live offer path is
	/// <see cref="ParkRideChooser.ChooseFor"/>, which walks the running park's chain and therefore reaches
	/// something bought this session; this one answers the narrower question of what the save itself
	/// offers, which is what the tests that call it mean by it.
	/// </para>
	/// </summary>
	/// <param name="queueLength">
	/// How long each object's queue is, or null to treat every queue as empty - which is what a park
	/// straight out of the file has.
	/// </param>
	/// <param name="trackTypeOf">
	/// Each object's <c>Bumper.WhichTrackType</c>, from the item catalogue, or null where no catalogue is
	/// to hand - which treats every object as untracked.
	/// </param>
	public static List<ParkWorld.CatalogueObject> Offerable( ParkWorld? park,
		Func<ParkWorld.CatalogueObject, int>? queueLength = null,
		Func<ParkWorld.CatalogueObject, int>? trackTypeOf = null )
	{
		var offerable = new List<ParkWorld.CatalogueObject>();

		if ( park == null )
			return offerable;

		var byId = new Dictionary<int, ParkWorld.CatalogueObject>();

		foreach ( var item in park.Objects )
			byId[item.ThingId] = item;

		var seen = 0;

		for ( var id = park.FirstObject; id != 0 && seen <= byId.Count; ++seen )
		{
			if ( !byId.TryGetValue( id, out var item ) )
				break;

			// The park goes in, so that an object whose queue is not in its record gets it walked off the
			// map - which is the whole of why this answers six objects in the shipped park and not two.
			if ( CanBeOffered( item, queueLength?.Invoke( item ) ?? 0, trackTypeOf?.Invoke( item ) ?? 0, park ) )
				offerable.Add( item );

			id = item.NextObject;
		}

		return offerable;
	}
}
