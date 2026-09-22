namespace OpenTPW;

/// <summary>
/// Laying and lifting path, which is the verb the player actually performs - the original's
/// <c>FUN_005346d0</c> (the stamp) driven along a line by <c>FUN_00536100</c>.
///
/// <para>
/// <b>There is no drag.</b> Both drag slots of the build-tool interaction mode are bare <c>RET</c>
/// stubs, so a run of path is <b>click to anchor, click to commit</b>: the second click snaps its
/// target to the dominant axis and the line is walked one cell at a time. The action recorder
/// corroborates it from the other side - one record per click carrying a single cell, where a real
/// drag would have to record every intermediate cell or a start/end pair.
/// </para>
///
/// <para>
/// <b>The order inside one cell is the original's and it is not arbitrary:</b> the type gate first,
/// then the same-type shortcut, then the price, then affordability, then the write, and only then the
/// debit. Money moves <i>after</i> the cell changes, and a cell that is already path costs nothing at
/// all - so dragging back over your own path is free, which is a thing a player will do constantly.
/// </para>
/// </summary>
public static class ParkPathBuilding
{
	/// <summary>The cell type a path is.</summary>
	private const int PathType = CellEdge.Path;

	/// <summary>Bare ground, and the type a lifted cell goes back to.</summary>
	private const int NothingType = CellEdge.Nothing;

	/// <summary>
	/// What one path cell costs - <c>Costs.PathCell</c>, <b>20</b> in Lost Kingdom.
	/// </summary>
	/// <remarks>
	/// <b>Measured from the game's own data, which is the only place it exists.</b> The executable
	/// holds the key names and an uninitialised dword; the value is read wholesale from
	/// <c>data/levels/Standard.sam</c>, where <c>Costs.PathCell</c> is 20 and <c>Costs.QueueCell</c>
	/// is 75. Jungle's <c>Easy_Standard.sam</c> overrides <c>MapCell</c>, <c>KartTrackCell</c> and
	/// <c>WaterTrackCell</c> but <b>not</b> those two, so 20 stands for this park.
	/// </remarks>
	public const string PathCellCostKey = "Costs.PathCell";

	/// <summary>The fallback if the balance file is missing the key, which no shipped theme is.</summary>
	private const int PathCellCostFallback = 20;

	/// <summary>What one path cell costs in the park now loaded.</summary>
	public static int CellCost( Level? level )
		=> level?.Balance?.Int( PathCellCostKey, PathCellCostFallback ) ?? PathCellCostFallback;

	/// <summary>
	/// Whether a cell may become <paramref name="wanted"/> - the original's <c>FUN_00535600</c>, the
	/// gate the stamp consults before anything else happens.
	/// </summary>
	/// <remarks>
	/// It permits only: clearing to nothing; the type it already is; building on bare ground; path
	/// over queue; a footprint over path; and a coaster footprint over itself. <b>Notably 4-over-4 is
	/// refused</b>, which is a carve-out from the "same type is allowed" rule it otherwise follows.
	/// </remarks>
	/// <param name="lastOfRun">
	/// Whether this is the final cell of a run, which decides one arm on its own: <b>queue may be laid
	/// over path on the cells in the middle of a run and not on the last one</b>. The original gates
	/// that on a flag its line walker sets only for the closing cell, and the gameplay intent is
	/// inferred rather than measured - what is measured is the flag and the branch.
	/// </param>
	public static bool MayBecome( int existing, int wanted, bool lastOfRun = true )
	{
		if ( wanted == NothingType )
			return true;

		if ( wanted == CellEdge.Footprint && existing == CellEdge.Footprint )
			return false;

		if ( wanted == existing || existing == NothingType )
			return true;

		if ( wanted == PathType && existing == ParkRideChoice.QueueCellType )
			return true;

		if ( wanted == ParkRideChoice.QueueCellType && existing == PathType )
			return !lastOfRun;

		if ( wanted == CellEdge.Footprint && existing == PathType )
			return true;

		return wanted == 0x15 && existing == 0x15;
	}

	/// <summary>
	/// Lays one cell of path, and answers a line saying what happened.
	///
	/// <para>
	/// <b>A cell that is already path costs nothing and changes nothing</b>, which is the original's
	/// own shortcut: it bumps a re-stamp counter and returns success before any price is fetched. This
	/// keeps that meaning without the counter, which nothing here reads yet.
	/// </para>
	/// </summary>
	public static string Lay( int cellX, int cellY )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "path: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"path: ({cellX},{cellY}) is off the map";

		var cell = ParkState.CellFor( park, cellX, cellY );

		// Already path: free, and the original returns success rather than refusing.
		if ( cell.Type == PathType )
			return $"path: ({cellX},{cellY}) is already path - nothing charged";

		if ( !MayBecome( cell.Type, PathType ) )
			return $"path: ({cellX},{cellY}) is type {cell.Type}, which path may not be laid over";

		// NOMODIFY marks a cell the level owns. It covers 18 of Lost Kingdom's 78 path cells, NOT all
		// of them - see the remarks on NoModify, which record why the two differ.
		if ( (cell.Flags & NoModify) != 0 )
			return $"path: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		var price = CellCost( level );

		if ( state.Balance < price )
			return $"path: a cell costs {price} and the park has {state.Balance}";

		// The write, then the money - the original's order.
		state.SetRecord( cellX, cellY, cell with { Type = PathType, TileSet = ParkPaths.PathTileSet } );

		ParkPathNeighbours.LinkPath( state, park, cellX, cellY );

		state.Spend( price );

		RetileAround( state, park, cellX, cellY );

		ParkSurfaces.Rebuild();

		return $"path: laid at ({cellX},{cellY}) for {price}, balance {state.Balance}";
	}

	/// <summary>
	/// Lifts one cell of path back to bare ground. <b>It refunds nothing</b>, and that asymmetry is the
	/// original's: only a queue cell's removal credits anything back.
	/// </summary>
	public static string Lift( int cellX, int cellY )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "delpath: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"delpath: ({cellX},{cellY}) is off the map";

		var cell = ParkState.CellFor( park, cellX, cellY );

		if ( cell.Type != PathType )
			return $"delpath: ({cellX},{cellY}) is type {cell.Type}, not path";

		if ( (cell.Flags & NoModify) != 0 )
			return $"delpath: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		// The neighbours are unlinked BEFORE the cell is reset, while its own mask is still intact -
		// the original's order, and the reason it can find who to unlink at all.
		Unlink( state, park, cellX, cellY );

		state.SetRecord( cellX, cellY, cell with
		{
			Type = NothingType,
			Neighbours = 0,
			Direction = 0,
			TileSet = 0,
			TileIndex = 0,
			TileAngle = 0
		} );

		ParkSurfaces.Rebuild();

		return $"delpath: lifted ({cellX},{cellY}) - nothing refunded, which is what the original does";
	}

	/// <summary>
	/// The flag that marks a cell the level itself owns - bit <c>0x20</c>, named NOMODIFY by the
	/// game's own debug string.
	///
	/// <para>
	/// <b>It covers 18 of Lost Kingdom's 78 path cells, not all of them, and that was measured after
	/// two guesses went the other way.</b> The executable's loader rebuilds cells from the level's
	/// design map and sets this flag on the path cells it makes there; OpenTPW reads the <b>save's
	/// stored</b> flags, and the two do not agree. So the refusal below is real but partial here,
	/// where in the original's runtime it covers every cell the level laid.
	/// </para>
	/// <para>
	/// <b>A hypothesis, and left as one:</b> the 18 are plausibly the author's own fixed paths and the
	/// other 60 were laid while the scenario was authored - which is what a shipped scenario save
	/// would look like. Checking the 18 against <c>base.map</c>'s design bits would settle it.
	/// </para>
	/// </summary>
	public const int NoModify = 0x20;

	/// <summary>What one queue cell costs - <c>Costs.QueueCell</c>, <b>75</b> in Lost Kingdom.</summary>
	public const string QueueCellCostKey = "Costs.QueueCell";

	private const int QueueCellCostFallback = 75;

	/// <summary>What one queue cell costs in the park now loaded.</summary>
	public static int QueueCost( Level? level )
		=> level?.Balance?.Int( QueueCellCostKey, QueueCellCostFallback ) ?? QueueCellCostFallback;

	/// <summary>
	/// Lays one cell of queue for a named object, entered from <paramref name="fromX"/>,
	/// <paramref name="fromY"/>.
	///
	/// <para>
	/// <b>Where it was entered from is not bookkeeping - it is what makes the queue measurable.</b> The
	/// cell records a flow direction that is the <b>opposite of the step taken into it</b>, so the
	/// direction points back down the queue toward the ride, and the walk that measures the queue
	/// accepts a neighbour only when its flow byte is the opposite of the direction probed. Written
	/// the other way round, every queue measures nought cells long.
	/// </para>
	/// <para>
	/// <b>First writer wins</b>, which is the original's own rule: the byte is written only while it is
	/// still nought, so laying over an existing queue cell does not turn it round.
	/// </para>
	/// </summary>
	public static string LayQueue( int cellX, int cellY, int servesThingId, int fromX, int fromY,
		bool lastOfRun = true )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "queue: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"queue: ({cellX},{cellY}) is off the map";

		if ( !state.TryObject( servesThingId, out var serves ) )
			return $"queue: nothing in the park is thing {servesThingId}";

		var cell = ParkState.CellFor( park, cellX, cellY );

		if ( cell.Type == ParkRideChoice.QueueCellType )
			return $"queue: ({cellX},{cellY}) is already queue - nothing charged";

		if ( !MayBecome( cell.Type, ParkRideChoice.QueueCellType, lastOfRun ) )
			return $"queue: ({cellX},{cellY}) is type {cell.Type}, which queue may not be laid over"
				+ (cell.Type == PathType ? " on the last cell of a run" : "");

		if ( (cell.Flags & NoModify) != 0 )
			return $"queue: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		var price = QueueCost( level );

		if ( state.Balance < price )
			return $"queue: a cell costs {price} and the park has {state.Balance}";

		var flow = FlowFrom( fromX, fromY, cellX, cellY );

		// The flow byte is the original's +0x0d, written as the OPPOSITE of the step the run took into
		// this cell and only while it is still nought - first writer wins, which is why a cell the placer
		// has already pointed at its ride keeps that heading rather than being turned round by the run.
		//
		// THE MASK BIT IS THE ONE PART STILL NOT DECODED, and it is one bit rather than the pair it was.
		// The way in is authored by the placer now (ParkBuilding.Mark), so a cell laid against a ride
		// already adjoins it; what has no decoded writer is this cell's own bit back toward the cell the
		// run came from - op 0x82 writes the flow byte and nothing else, and FUN_005348d0's cardinal rule
		// says a type-3 neighbour never forms a link. Without some bit here CellEdge.Blocked refuses the
		// step in from every direction, so it is set to match the shipped park's own queue cells and
		// counted. FlowFrom is already in the sense Blocked tests: it answers 0x01 for a step in +y, and
		// CellEdge.BitFor( South ) is 0x01 for that same step.
		Unimplemented.Report( "QUEUE_CELL_NEIGHBOUR_AUTHORING" );

		state.SetRecord( cellX, cellY, cell with
		{
			Type = ParkRideChoice.QueueCellType,
			TileSet = ParkQueues.QueueTileSet,
			Direction = cell.Direction != 0 ? cell.Direction : (byte)flow,
			Neighbours = (byte)(cell.Neighbours | flow),
			ParentId = (ushort)MapStep.CellId( serves.CellX, serves.CellY )
		} );

		state.Spend( price );

		// The END of the transaction, which is where every one of the original's eight invalidation
		// sites fires - after the cells are written, never once per cell as they are written.
		state.InvalidateQueue( servesThingId );

		RetileAround( state, park, cellX, cellY );

		ParkSurfaces.Rebuild();

		return $"queue: laid at ({cellX},{cellY}) for thing {serves.ThingId}, flow 0x{flow:x2}, "
			+ $"cost {price}, balance {state.Balance}";
	}

	/// <summary>
	/// Lifts one cell of queue. <b>It refunds</b>, where lifting a path does not - that asymmetry is
	/// the original's, and only the queue arm of its teardown calls the credit at all.
	/// </summary>
	/// <remarks>
	/// <b>Whatever lies beyond the break is ORPHANED, and that is correct rather than a gap.</b> The
	/// original has no trimming loop anywhere: the cells past the break keep their type, their flow
	/// and their owner and are simply never walked to again, so the measured queue just gets shorter.
	/// </remarks>
	public static string LiftQueue( int cellX, int cellY )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "delqueue: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"delqueue: ({cellX},{cellY}) is off the map";

		var cell = ParkState.CellFor( park, cellX, cellY );

		if ( cell.Type != ParkRideChoice.QueueCellType )
			return $"delqueue: ({cellX},{cellY}) is type {cell.Type}, not queue";

		if ( (cell.Flags & NoModify) != 0 )
			return $"delqueue: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		// The percentage the original scales a refund by is per-item, per-build-state and per-age, and
		// nothing here keeps a thing's age in those units - the same gap ParkBuilding.Sell records. So
		// this gives the cell back in full, which is right the moment it was laid.
		Unimplemented.Report( "QUEUE_REFUND_DEPRECIATION" );

		var refund = QueueCost( level );
		var owner = OwnerOf( state, cell );

		Unlink( state, park, cellX, cellY );

		state.SetRecord( cellX, cellY, cell with
		{
			Type = NothingType,
			Neighbours = 0,
			Direction = 0,
			ParentId = 0,
			TileSet = 0,
			TileIndex = 0,
			TileAngle = 0
		} );

		state.Refund( refund );

		if ( owner != 0 )
			state.InvalidateQueue( owner );

		ParkSurfaces.Rebuild();

		return $"delqueue: lifted ({cellX},{cellY}) for {refund}, "
			+ (owner != 0 ? $"thing {owner}'s queue re-walks" : "no owner recorded")
			+ $", balance {state.Balance}";
	}

	/// <summary>Which object a queue cell says it serves, from the packed cell its owner stands on.</summary>
	/// <remarks>
	/// Internal rather than private so that <see cref="Level"/> can answer "whose queue is this cell",
	/// which is what clicking a queue cell has to know before it can re-arm the tool against that ride.
	/// </remarks>
	internal static int OwnerOf( ParkState state, ParkWorld.MapCell cell )
	{
		if ( cell.ParentId == 0 )
			return 0;

		var (ownerX, ownerY) = MapStep.CellAt( cell.ParentId );

		foreach ( var placed in state.Objects )
		{
			if ( placed.CellX == ownerX && placed.CellY == ownerY )
				return placed.ThingId;
		}

		return 0;
	}

	/// <summary>The flow byte for a step, which is the <b>opposite</b> of the direction travelled.</summary>
	/// <remarks>
	/// <b>The four tests are sequential rather than exclusive in the original, and that is reproduced:</b>
	/// a step with both axes non-zero has its horizontal answer overwritten by its vertical one, so a
	/// diagonal leaves only the vertical opposite.
	/// </remarks>
	private static int FlowFrom( int fromX, int fromY, int toX, int toY )
	{
		var flow = 0;

		if ( toX - fromX == -1 )
			flow = 0x04;

		if ( toX - fromX == 1 )
			flow = 0x40;

		if ( toY - fromY == -1 )
			flow = 0x10;

		if ( toY - fromY == 1 )
			flow = 0x01;

		return flow;
	}

	/// <summary>
	/// Clears this cell's bit from every neighbour it was joined to, and retiles each of them.
	/// </summary>
	private static void Unlink( ParkState state, ParkWorld park, int x, int y )
	{
		var cell = ParkState.CellFor( park, x, y );

		foreach ( var (bit, acrossBy, downBy) in Sides )
		{
			if ( (cell.Neighbours & bit) == 0 || !ParkState.OnMap( x + acrossBy, y + downBy ) )
				continue;

			var nb = ParkState.CellFor( park, x + acrossBy, y + downBy );

			state.SetRecord( x + acrossBy, y + downBy,
				nb with { Neighbours = (byte)(nb.Neighbours & ~CellEdge.Opposite( bit )) } );

			Retile( state, park, x + acrossBy, y + downBy );
		}
	}

	/// <summary>
	/// Retiles a cell <b>and every cell around it</b>, because joining up changed their masks too.
	///
	/// <para>
	/// <b>This was found by playing, not by a test, and the test suite could not have found it.</b>
	/// Laying a run of three left the middle cell drawing <c>jpa_end1</c> - the tile its mask asked for
	/// at the moment it was laid, when it still had only one neighbour - because only the cell being
	/// laid was retiled while <see cref="ParkPathNeighbours.LinkPath"/> had also rewritten its
	/// neighbours' masks. The masks were right the whole time; the art was a step behind.
	/// </para>
	/// <para>
	/// The lifting path never had the bug, and that is what identified it: <see cref="Unlink"/> already
	/// retiles each neighbour it detaches, so a cell read correctly after a delete and wrongly after a
	/// build.
	/// </para>
	/// </summary>
	internal static void RetileAround( ParkState state, ParkWorld park, int x, int y )
	{
		Retile( state, park, x, y );

		foreach ( var (_, acrossBy, downBy) in Sides )
		{
			if ( ParkState.OnMap( x + acrossBy, y + downBy ) )
				Retile( state, park, x + acrossBy, y + downBy );
		}
	}

	/// <summary>Works out what a cell should draw now and records it - the original's <c>FUN_005365d0</c>.</summary>
	/// <remarks>
	/// <b>A QUEUE cell is retiled too, and this answered only for paths until it was.</b>
	/// <c>FUN_00535dd0</c> takes <c>abs(mType)</c> and sends 3 to its own eleven-row table at
	/// <c>DAT_007630b0</c>, exactly as it sends a path to the forty-nine at <c>DAT_00763138</c> - so a
	/// laid queue cell has a piece like any other cell. Left out, a queue cell kept whatever tile index
	/// the ground under it carried - <b>55 on bare ground</b>, which is outside
	/// <c>ParkQueues.Pieces</c> - so it drew <b>nothing at all</b> and was counted as a cell naming a
	/// piece the game has no table for.
	/// <para>
	/// Internal rather than private only so that it can be tested, the same reason
	/// <see cref="ParkBuilding.Stamp"/> is: <see cref="LayQueue"/> needs a loaded level, and a test that
	/// re-derived the lookup instead of calling this would pass just as happily with the queue arm put
	/// back.
	/// </para>
	/// </remarks>
	internal static void Retile( ParkState state, ParkWorld park, int x, int y )
	{
		var cell = ParkState.CellFor( park, x, y );
		var queue = cell.Type == ParkRideChoice.QueueCellType;

		if ( cell.Type != PathType && !queue )
			return;

		// The art variant is a coin the original carries between calls, so a straight is not a
		// function of its neighbours at all - see ParkPathTiles.Vary. Nothing here reproduces the
		// original's RNG sequence, and it is cosmetic either way.
		var links = queue ? PathLinks( park, x, y, cell ) : 0;
		var (set, index, angle) = ParkPathTiles.TileFor( cell.Type, cell.Neighbours, cell.Direction, links );

		// A queue cell whose index lands outside the game's own table of pieces draws NOTHING, and the
		// ground has already been told to leave a tile-set-2 cell alone - so the SKY SHOWS THROUGH a
		// hole where the piece belongs. Photographed once: a cell with two mutual path links takes
		// 2 + 3 + 3 = 8 against a table of eight, and the park had a flat sky-coloured square in it.
		//
		// The BUMP is the uncertain part here, not the table row. The original gates each link on a
		// flags test against the TRACK cell beside this one - a separate array this project has no
		// layer for - so it cannot say which of the two links the original would have refused. Dropping
		// links until the index is one the table holds keeps the game's own art and the end piece the
		// cell is asking for; the alternative is the hole. The shipped park cannot arbitrate: its one
		// end piece at (49,22) has a single path link, so no cell in it ever reaches this.
		while ( queue && index >= ParkQueues.PieceCount && links > 0 )
		{
			Unimplemented.Report( "QUEUE_TILE_INDEX_OUTSIDE_TABLE" );

			--links;

			(set, index, angle) = ParkPathTiles.TileFor( cell.Type, cell.Neighbours, cell.Direction, links );
		}

		state.SetRecord( x, y, cell with { TileSet = set, TileIndex = index, TileAngle = angle } );
	}

	/// <summary>
	/// How many of a queue cell's cardinal links reach a path - the <b>three</b> the original adds to a
	/// queue's tile index for each one, which is what makes the cell where a queue meets a path draw the
	/// end piece rather than a straight.
	/// </summary>
	/// <remarks>
	/// <b>The link has to be MUTUAL.</b> <c>FUN_00535dd0</c> walks the four cardinals and counts a
	/// neighbour only where this cell's mask carries the bit, the neighbour is type 1, and the
	/// neighbour's own mask carries the opposite bit - so a one-sided bit, which a park's mask is
	/// legitimately full of, does not count.
	/// <para>
	/// <b>Its fourth test is NOT reproduced and is counted instead.</b> The original also asks
	/// <c>FUN_0053ad20</c> of the TRACK cell beside this one - a separate <c>0x28</c>-stride array,
	/// re-targeted through its parent where that cell defers - and this project has no track-cell layer
	/// to ask. So a link this vouches for might be one the original refuses.
	/// </para>
	/// </remarks>
	private static int PathLinks( ParkWorld park, int x, int y, ParkWorld.MapCell cell )
	{
		var links = 0;

		foreach ( var (bit, acrossBy, downBy) in Sides )
		{
			if ( bit is not (0x01 or 0x04 or 0x10 or 0x40) )
				continue;

			if ( (cell.Neighbours & bit) == 0 || !ParkState.OnMap( x + acrossBy, y + downBy ) )
				continue;

			var nb = ParkState.CellFor( park, x + acrossBy, y + downBy );

			if ( nb.Type != PathType || (nb.Neighbours & CellEdge.Opposite( bit )) == 0 )
				continue;

			Unimplemented.Report( "QUEUE_TILE_TRACK_FLAGS_GATE" );

			++links;
		}

		return links;
	}

	/// <summary>The eight sides, in ring order, matching <see cref="ParkPathNeighbours"/>'s own table.</summary>
	private static readonly (int Bit, int AcrossBy, int DownBy)[] Sides =
	[
		(0x01, 0, -1), (0x02, 1, -1), (0x04, 1, 0), (0x08, 1, 1),
		(0x10, 0, 1), (0x20, -1, 1), (0x40, -1, 0), (0x80, -1, -1)
	];
}
