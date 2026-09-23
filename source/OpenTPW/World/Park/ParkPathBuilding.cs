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

		// NOMODIFY marks a cell the player may not change - the level's avenue, and the cells the placer
		// lays before a thing's ends. See NoModify.
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
	/// The flag that marks a cell the player may not change - bit <c>0x20</c>, named NOMODIFY by the
	/// game's own debug string.
	///
	/// <para>
	/// <b>Two things set it, and the shipped park's nineteen flagged cells are exactly theirs.</b> The
	/// loader sets it on each path cell it rebuilds from the level's design map (<c>FUN_00536490</c>) -
	/// the ten-cell avenue at x 47..48, y 17..21 in Lost Kingdom - and the placer assigns it to the one
	/// cell it lays before a thing's entrance or exit (<c>FUN_0053a510( 0x20 )</c>): eight path cells and
	/// the Belly Bounce's queue cell (52,22). The park's other sixty path cells were laid as ordinary path.
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
	/// <param name="rebuild">
	/// Whether to lay the park's surfaces again afterwards. A run lays many cells and rebuilds once at
	/// the end - see <see cref="ParkSurfaces.Rebuild"/>, which re-reads the ground model each time.
	/// </param>
	/// <param name="firstOfRun">
	/// Whether this is the first cell of a run, which is the only cell that may bond to the ride's
	/// entrance and the one cell that is not joined back to <paramref name="fromX"/>,
	/// <paramref name="fromY"/> - a run's first cell has nothing before it, so that pair only says which
	/// way the run is heading.
	/// </param>
	public static string LayQueue( int cellX, int cellY, int servesThingId, int fromX, int fromY,
		bool lastOfRun = true, bool rebuild = true, bool firstOfRun = false )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "queue: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"queue: ({cellX},{cellY}) is off the map";

		if ( !state.TryObject( servesThingId, out var serves ) )
			return $"queue: nothing in the park is thing {servesThingId}";

		var cell = ParkState.CellFor( park, cellX, cellY );

		// Already queue: the stamp only counts it again and charges nothing, but the rest of the run's
		// ops still reach it - the link pass, and the owner - which is how a run's first cell renews its
		// bond to the entrance.
		if ( cell.Type == ParkRideChoice.QueueCellType )
		{
			if ( OwnerOf( state, cell ) is var holder && holder != 0 && holder != servesThingId )
				return $"queue: ({cellX},{cellY}) is thing {holder}'s queue";

			var ownerCell = MapStep.CellId( serves.CellX, serves.CellY );

			LinkQueueCell( state, park, cellX, cellY, fromX, fromY, ownerCell, serves, firstOfRun );
			state.SetRecord( cellX, cellY, ParkState.CellFor( park, cellX, cellY ) with { ParentId = (ushort)ownerCell } );
			RetileAround( state, park, cellX, cellY );

			return $"queue: ({cellX},{cellY}) is already queue - nothing charged";
		}

		if ( !MayBecome( cell.Type, ParkRideChoice.QueueCellType, lastOfRun ) )
			return $"queue: ({cellX},{cellY}) is type {cell.Type}, which queue may not be laid over"
				+ (cell.Type == PathType ? " on the last cell of a run" : "");

		if ( (cell.Flags & NoModify) != 0 )
			return $"queue: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		var price = QueueCost( level );

		if ( state.Balance < price )
			return $"queue: a cell costs {price} and the park has {state.Balance}";

		var flow = StampQueueCell( state, park, cellX, cellY, fromX, fromY, serves, firstOfRun );

		state.Spend( price );

		// The END of the transaction, which is where every one of the original's eight invalidation
		// sites fires - after the cells are written, never once per cell as they are written.
		state.InvalidateQueue( servesThingId );

		RetileAround( state, park, cellX, cellY );

		if ( rebuild )
			ParkSurfaces.Rebuild();

		return $"queue: laid at ({cellX},{cellY}) for thing {serves.ThingId}, flow 0x{flow:x2}, "
			+ $"cost {price}, balance {state.Balance}";
	}

	/// <summary>
	/// One click of the armed queue tool: a straight run from the anchor to the clicked cell, snapped to
	/// its longer axis - the original's mode-3 arm of the apply dispatcher (<c>0x00527222</c>..
	/// <c>0x005275f2</c>).
	/// </summary>
	/// <remarks>
	/// <b>The tool puts itself away</b> in the original's four cases (each through
	/// <c>FUN_0052f200( 0, 0 )</c>):
	/// <list type="bullet">
	/// <item>the run <b>ends on a path</b>, which is left a path and joined to the queue - the finished
	/// queue, with sound <c>0x8b</c>; or ends on this ride's own queue, with advisor message <c>0x151</c>;</item>
	/// <item>the click is <b>the anchor itself</b>;</item>
	/// <item><b>any cell of the run would be refused</b> - the preview flags it red, the click lays
	/// nothing at all and the tool ends with sound <c>0xaf</c>;</item>
	/// <item>a quick right click with the Options switch "RMB cancel" on - <see cref="Level"/>'s.</item>
	/// </list>
	/// Otherwise the run is laid and the anchor moves to its far end, so an L is laid a click at a time.
	/// <para>
	/// <b>The refusal is the preview's</b>: the click lays nothing when <see cref="QueueStrip"/> - the
	/// squares the player is looking at - has a red one in it, which is the original's own gate
	/// (<c>DAT_00816d48</c>, <c>0x00524a63</c>..<c>0x00524acd</c>).
	/// </para>
	/// </remarks>
	public static string RunQueue( int clickX, int clickY )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "queue: a park has to be loaded";

		var serves = ParkBuildMode.Serves;
		var (fromX, fromY) = ParkBuildMode.Anchor;
		var (toX, toY) = ParkBuildMode.SnapToAxis( clickX, clickY );

		// Clicking the anchor lays the one cell there - already queue, so only its links are renewed - and
		// puts the tool away, with the advisor's message for a queue left unjoined.
		if ( toX == fromX && toY == fromY )
		{
			LayQueue( fromX, fromY, serves, fromX, fromY, lastOfRun: false, firstOfRun: true );
			ParkBuildMode.Disarm();
			Unimplemented.Report( "QUEUE_MODE_ADVISOR_MESSAGE_0x151" );

			return $"queue: clicked the anchor ({fromX},{fromY}) - the queue tool is put away";
		}

		// What the preview showed is what the click does: any red square and nothing is laid.
		var strip = QueueStrip( clickX, clickY );

		if ( strip.FirstOrDefault( square => square.Marker == MarkerRed ) is { Why: { } why } refused )
		{
			ParkBuildMode.Disarm();
			Unimplemented.Report( "QUEUE_TOOL_REFUSED_SOUND_0xAF" );

			return $"queue: nothing laid - ({refused.X},{refused.Y}) {why}, so the queue tool is put away";
		}

		var (acrossBy, downBy) = (Math.Sign( toX - fromX ), Math.Sign( toY - fromY ));
		var laid = 0;
		var (atX, atY) = (fromX, fromY);

		for ( var step = 0; step < strip.Count; ++step )
		{
			var (x, y, marker, _, _) = strip[step];

			if ( marker == MarkerLink && state.TryObject( serves, out var ride ) )
			{
				JoinQueueToPath( state, park, atX, atY, x, y, ride );
				state.InvalidateQueue( serves );
				ParkSurfaces.Rebuild();
				ParkBuildMode.Disarm();
				Unimplemented.Report( "QUEUE_TOOL_LINK_SOUND_0x8B" );

				return $"queue: laid {laid} from ({fromX},{fromY}) and joined the path at ({x},{y}) - done";
			}

			if ( marker == MarkerEnd && step == strip.Count - 1 && state.TryObject( serves, out var own ) )
			{
				LinkQueueCell( state, park, x, y, atX, atY, MapStep.CellId( own.CellX, own.CellY ), own, firstOfRun: false );
				RetileAround( state, park, x, y );
				state.InvalidateQueue( serves );
				ParkSurfaces.Rebuild();
				ParkBuildMode.Disarm();
				Unimplemented.Report( "QUEUE_MODE_ADVISOR_MESSAGE_0x151" );

				return $"queue: laid {laid} from ({fromX},{fromY}) and ended on its own queue at ({x},{y})";
			}

			// The first cell is the anchor, and has nothing before it: the pair handed over only says which
			// way the run heads, so that a cell laid fresh there takes its flow from the line.
			var (fromCellX, fromCellY) = step == 0 ? (x - acrossBy, y - downBy) : (atX, atY);

			if ( LayQueue( x, y, serves, fromCellX, fromCellY, lastOfRun: false, rebuild: false, firstOfRun: step == 0 )
				.Contains( "laid at" ) )
				++laid;

			(atX, atY) = (x, y);
		}

		ParkSurfaces.Rebuild();
		ParkBuildMode.AnchorAt( toX, toY );

		return $"queue: laid {laid} from ({fromX},{fromY}) to ({toX},{toY}) - click again to carry on";
	}

	/// <summary>
	/// One square of the queue tool's preview: where it is, the marker it wears, why it is red, and
	/// whether it is red because the run cannot be paid for - which the original shows with its own
	/// cursor, <c>c_cash</c> (<c>DAT_00816d5c</c>, <c>FUN_0052f950</c>).
	/// </summary>
	public readonly record struct QueueSquare( int X, int Y, int Marker, string? Why = null, bool Unaffordable = false );

	/// <summary>A square of the queue tool's preview - an index into the original's twenty-entry marker table at <c>0x00763b38</c>.</summary>
	public const int MarkerBlue = 0;

	/// <inheritdoc cref="MarkerBlue"/>
	public const int MarkerRed = 1;

	/// <summary>The run ends on a path and will join it - <c>m_link</c>.</summary>
	public const int MarkerLink = 8;

	/// <summary>The run ends on this ride's own queue - <c>m_end</c>.</summary>
	public const int MarkerEnd = 11;

	/// <summary>
	/// The squares the armed queue tool shows from its anchor to a cell, snapped to the longer axis -
	/// the original's hover pass, <c>FUN_00536100( 0x103, anchor, target )</c> with the per-cell verdict
	/// <c>FUN_00535670( 3 )</c> - and so also what a click there would do. Empty when the queue tool is
	/// not armed and anchored.
	/// </summary>
	/// <remarks>
	/// <b>After the first red square every later square is red</b>, as the original latches it.
	/// </remarks>
	public static List<QueueSquare> QueueStrip( int toX, int toY )
	{
		var strip = new List<QueueSquare>();

		if ( ParkBuildMode.Current != ParkBuildMode.Queue || !ParkBuildMode.Anchored
			|| Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return strip;

		var serves = ParkBuildMode.Serves;
		var (fromX, fromY) = ParkBuildMode.Anchor;
		var (endX, endY) = ParkBuildMode.SnapToAxis( toX, toY );

		var steps = Math.Max( Math.Abs( endX - fromX ), Math.Abs( endY - fromY ) );
		var (acrossBy, downBy) = (Math.Sign( endX - fromX ), Math.Sign( endY - fromY ));
		var price = QueueCost( level );
		var owed = 0;
		string? refused = null;

		for ( var step = 0; step <= steps; ++step )
		{
			var (x, y) = (fromX + (acrossBy * step), fromY + (downBy * step));

			if ( refused != null )
			{
				strip.Add( new( x, y, MarkerRed, refused ) );
				continue;
			}

			var (marker, why) = Verdict( state, park, x, y, serves, step == steps, price, ref owed );

			if ( marker == MarkerRed )
				refused = why;

			strip.Add( new( x, y, marker, why, Unaffordable: marker == MarkerRed && owed > state.Balance ) );
		}

		return strip;
	}

	/// <summary>
	/// One square of the queue tool's preview - the queue arm of <c>FUN_00535670</c>, as far as it is
	/// decoded. It answers only blue, red, <c>m_link</c> and <c>m_end</c>.
	/// </summary>
	private static (int Marker, string? Why) Verdict( ParkState state, ParkWorld park, int x, int y, int serves,
		bool last, int price, ref int owed )
	{
		if ( !ParkState.OnMap( x, y ) )
			return (MarkerRed, "is off the map");

		var cell = ParkState.CellFor( park, x, y );

		// Any path may end a run, NOMODIFY or not: that is the join, and the path stays a path.
		if ( last && cell.Type == PathType )
			return (MarkerLink, null);

		// This ride's own queue: a cell NOT joined exactly two ways - its end, or its node - may close a
		// run or be carried through; one joined exactly two ways is the middle of the file and may not
		// (FUN_00522790's count against 2, as the original tests it).
		if ( cell.Type == ParkRideChoice.QueueCellType )
		{
			if ( OwnerOf( state, cell ) != serves )
				return (MarkerRed, "is another ride's queue");

			var twoLinked = System.Numerics.BitOperations.PopCount( cell.Neighbours ) == 2;

			if ( last )
				return twoLinked ? (MarkerRed, "is the middle of this ride's queue") : (MarkerEnd, null);

			return twoLinked ? (MarkerRed, "is the middle of this ride's queue") : (MarkerBlue, null);
		}

		// A thing's own cells are red unless its item's overwrite priority is below the queue tool's 3,
		// in which case the original bulldozes it (op 0x87 into FUN_00527ee0). Every jungle ride's is 5;
		// nothing here demolishes, so it is red either way.
		if ( cell.Type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd )
		{
			Unimplemented.Report( "QUEUE_RUN_OVER_A_LOW_PRIORITY_THING" );

			return (MarkerRed, "has something built on it");
		}

		if ( !MayBecome( cell.Type, ParkRideChoice.QueueCellType, lastOfRun: false ) )
			return (MarkerRed, $"is type {cell.Type}, which queue may not be laid over");

		if ( (cell.Flags & NoModify) != 0 )
			return (MarkerRed, "is marked NOMODIFY");

		// The original also refuses bare ground and path where two corner tests fire (FUN_0053ae00 and
		// FUN_0053ae90), which are not decoded.
		Unimplemented.Report( "QUEUE_VERDICT_CORNER_RULE" );

		// The preview's cash test leaves a path out of the total (0x005358e9..0x005358f1), though
		// laying queue over it is charged like any other cell.
		if ( cell.Type != PathType )
			owed += price;

		if ( state.Balance < owed )
			return (MarkerRed, $"would bring the run to {owed} against a balance of {state.Balance}");

		return (MarkerBlue, null);
	}

	/// <summary>
	/// Makes one cell queue for a ride and joins it up - the stamp, then ops <c>0x80</c>, <c>0x82</c> and
	/// <c>0x83</c> as a queue run applies them. Answers the flow byte it gave the cell. No checks and no
	/// money: <see cref="LayQueue"/> makes both first.
	/// </summary>
	/// <remarks>
	/// <b>The flow byte is the original's <c>+0x0d</c></b>, written as the OPPOSITE of the step the run took
	/// into this cell and only while it is still nought - first writer wins, which is why a cell the placer
	/// has already pointed at its ride keeps that heading rather than being turned round by the run.
	/// Internal so that a test can lay a run on real ground without a loaded level.
	/// </remarks>
	internal static int StampQueueCell( ParkState state, ParkWorld park, int x, int y, int fromX, int fromY,
		ParkWorld.CatalogueObject serves, bool firstOfRun )
	{
		var flow = FlowFrom( fromX, fromY, x, y );
		var owner = MapStep.CellId( serves.CellX, serves.CellY );

		// Queue over path force-clears the path first (the stamp's FUN_005367a0( 0, 0 ) with
		// DAT_0081d7a8 set, 0x0053473c..0x0053475d), so the queue does not inherit its links.
		ForceClearPath( state, park, x, y );

		var cell = ParkState.CellFor( park, x, y );

		state.SetRecord( x, y, cell with
		{
			Type = ParkRideChoice.QueueCellType,
			TileSet = ParkQueues.QueueTileSet,
			Direction = cell.Direction != 0 ? cell.Direction : (byte)flow,
			ParentId = (ushort)owner
		} );

		LinkQueueCell( state, park, x, y, fromX, fromY, owner, serves, firstOfRun );

		return flow;
	}

	/// <summary>
	/// Clears a path cell out of the way of a queue stamped over it - the path arm of
	/// <c>FUN_005367a0</c> under force: the neighbours lose their bits toward it, then its mask, flow
	/// byte, flags and owner go, whatever NOMODIFY said. No refund. Does nothing to any other cell.
	/// </summary>
	internal static void ForceClearPath( ParkState state, ParkWorld park, int x, int y )
	{
		if ( !ParkState.OnMap( x, y ) || ParkState.CellFor( park, x, y ).Type != PathType )
			return;

		Unlink( state, park, x, y );

		state.SetRecord( x, y, ParkState.CellFor( park, x, y ) with
		{
			Neighbours = 0,
			Direction = 0,
			Flags = 0,
			ParentId = 0
		} );
	}

	/// <summary>
	/// Joins a cell a queue run has just reached to what is around it - the queue arm of
	/// <c>FUN_005348d0</c>, which op <c>0x80</c> runs on every cell of a queue run with the laid type 3
	/// (<c>0x0053522d</c>..<c>0x00535597</c>). It makes <b>two links at most, and none by type</b>.
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	/// <item><b>Back to the cell the run came from</b>, both bits, only when that cell is queue, has
	/// fewer than two of its eight bits set, and is owned by this ride or by nobody - which is what keeps a
	/// queue a single file. Where the cell being joined is a PATH, its two diagonals either side of the
	/// link are cleared, whether or not the link was made.</item>
	/// <item><b>To the ride's entrance</b>, on the first cell of a run only, when the entrance is next to
	/// it and the entrance's direction byte EQUALS the bit pointing back here - an equality, not a mask.</item>
	/// </list>
	/// Nothing else: a queue cell never links to a path, an exit or another ride's cells beside it, so a
	/// queue laid along a path does not leak into it.
	/// </remarks>
	private static void LinkQueueCell( ParkState state, ParkWorld park, int x, int y, int fromX, int fromY,
		int owner, ParkWorld.CatalogueObject serves, bool firstOfRun )
	{
		if ( firstOfRun )
		{
			BondToEntrance( state, park, x, y, serves );
			return;
		}

		var back = FlowFrom( fromX, fromY, x, y );

		if ( back == 0 || !ParkState.OnMap( fromX, fromY ) )
			return;

		var previous = ParkState.CellFor( park, fromX, fromY );

		if ( previous.Type != ParkRideChoice.QueueCellType )
			return;

		if ( ParkState.CellFor( park, x, y ).Type == PathType )
		{
			var path = ParkState.CellFor( park, x, y );

			state.SetRecord( x, y, path with { Neighbours = (byte)(path.Neighbours & ~FlankingDiagonals( back )) } );
		}

		if ( System.Numerics.BitOperations.PopCount( previous.Neighbours ) >= 2 || (previous.ParentId != owner && previous.ParentId != 0) )
			return;

		var cell = ParkState.CellFor( park, x, y );

		state.SetRecord( x, y, cell with { Neighbours = (byte)(cell.Neighbours | back) } );
		state.SetRecord( fromX, fromY, previous with { Neighbours = (byte)(previous.Neighbours | CellEdge.Opposite( back )) } );
	}

	/// <summary>The two diagonals either side of a cardinal link, which a path joined by a queue loses.</summary>
	private static int FlankingDiagonals( int cardinal ) => cardinal switch
	{
		0x01 => 0x02 | 0x80,
		0x04 => 0x02 | 0x08,
		0x10 => 0x08 | 0x20,
		0x40 => 0x80 | 0x20,
		_ => 0
	};

	/// <summary>
	/// The first cell of a queue run bonds to the ride's own entrance when it is beside it and the
	/// entrance's direction byte points straight at it (<c>0x0053525a</c>..<c>0x00535323</c>).
	/// </summary>
	private static void BondToEntrance( ParkState state, ParkWorld park, int x, int y, ParkWorld.CatalogueObject serves )
	{
		if ( serves.EntryPos == 0 )
			return;

		var (entryX, entryY) = (serves.EntryCellX, serves.EntryCellY);

		foreach ( var (bit, acrossBy, downBy) in Sides )
		{
			if ( bit is not (0x01 or 0x04 or 0x10 or 0x40) || (x + acrossBy, y + downBy) != (entryX, entryY) )
				continue;

			var entrance = ParkState.CellFor( park, entryX, entryY );

			if ( entrance.Direction != CellEdge.Opposite( bit ) )
				return;

			var cell = ParkState.CellFor( park, x, y );

			state.SetRecord( x, y, cell with { Neighbours = (byte)(cell.Neighbours | bit) } );
			state.SetRecord( entryX, entryY, entrance with { Neighbours = (byte)(entrance.Neighbours | CellEdge.Opposite( bit )) } );
			return;
		}
	}

	/// <summary>
	/// The path a queue run ends on: <b>left a path</b> - the stamp refuses queue over path on a run's
	/// last cell - but joined to the queue, given the ride as its owner and a flow byte if it had none,
	/// and redrawn. The original runs op <c>0x80</c>, <c>0x82</c> and <c>0x83</c> on it like any other
	/// cell of the line, and its <c>0x81</c> then reports the run finished.
	/// </summary>
	/// <remarks>
	/// <b>The shipped park carries the owner as a fingerprint</b>: of Lost Kingdom's 78 path cells, only
	/// (48,22) - where the Belly Bounce's queue meets the path - names an owner, the ride's own 2996.
	/// </remarks>
	internal static void JoinQueueToPath( ParkState state, ParkWorld park, int queueX, int queueY, int pathX, int pathY,
		ParkWorld.CatalogueObject serves )
	{
		var owner = MapStep.CellId( serves.CellX, serves.CellY );

		LinkQueueCell( state, park, pathX, pathY, queueX, queueY, owner, serves, firstOfRun: false );

		var path = ParkState.CellFor( park, pathX, pathY );

		state.SetRecord( pathX, pathY, path with
		{
			Direction = path.Direction != 0 ? path.Direction : (byte)FlowFrom( queueX, queueY, pathX, pathY ),
			ParentId = (ushort)owner
		} );

		RetileAround( state, park, pathX, pathY );
		RetileAround( state, park, queueX, queueY );
	}

	/// <summary>
	/// Hands the player the queue tool for a ride's existing queue, anchored on its far end - the
	/// original's mode <c>0x14</c>, "edit this ride's queue", which both clicking a queue cell and the ride
	/// window's queue button install (<c>FUN_004af200( 0 )</c> is that button's handler).
	/// </summary>
	/// <remarks>
	/// <c>FUN_00530120</c> walks the queue out from the entrance to its last cell, <b>lets that cell go of
	/// the path it joined</b> - both bits cleared, both cells retiled - and anchors there; then the queue
	/// is rewalked and mode 3 is entered with the anchor kept (<c>0x005260f5</c>..<c>0x00526120</c>). So the
	/// next click carries the queue on from its end, and the run that finishes it joins it up again.
	/// </remarks>
	public static string EditQueue( int thingId )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "queue: a park has to be loaded";

		if ( !state.TryObject( thingId, out var placed ) )
			return $"queue: nothing in the park is thing {thingId}";

		if ( placed.EntryPos == 0 || !ParkState.OnMap( placed.EntryCellX, placed.EntryCellY ) )
		{
			Unimplemented.Report( "EDIT_QUEUE_WITH_NO_ENTRANCE" );

			return ParkBuildMode.Arm( ParkBuildMode.Queue, thingId );
		}

		// The walk starts where the entrance's link points; with no link at all, at the cell its direction
		// byte faces - which for a queued thing is the cell the placer laid its node on.
		var start = ParkRideChoice.StartOfQueue( park, placed );

		if ( start == 0 )
		{
			var facing = ParkState.CellFor( park, placed.EntryCellX, placed.EntryCellY ).Direction;
			var (acrossBy, downBy) = Sides.FirstOrDefault( side => side.Bit == facing ) is var side && side.Bit != 0
				? (side.AcrossBy, side.DownBy)
				: (0, 0);

			start = MapStep.CellId( placed.EntryCellX + acrossBy, placed.EntryCellY + downBy );
		}

		var (startX, startY) = MapStep.CellAt( start );

		// No queue: the tool is anchored on that faced cell, and the next click lays from it.
		if ( !ParkState.OnMap( startX, startY ) || ParkState.CellFor( park, startX, startY ).Type != ParkRideChoice.QueueCellType )
		{
			state.InvalidateQueue( thingId );

			return ParkBuildMode.ArmAt( ParkBuildMode.Queue, thingId, startX, startY );
		}

		// Bounded as the original bounds every queue walk - see ParkState.LongestQueue.
		var back = start;

		for ( int cell = start, cells = 0; cell != 0 && cells < ParkState.LongestQueue;
			cell = ParkRideChoice.StepToNextQueueCell( park, cell ), ++cells )
			back = cell;

		var (backX, backY) = MapStep.CellAt( back );

		DetachFromPath( state, park, backX, backY );
		state.InvalidateQueue( thingId );
		ParkSurfaces.Rebuild();

		return ParkBuildMode.ArmAt( ParkBuildMode.Queue, thingId, backX, backY );
	}

	/// <summary>Clears the mutual link between a queue's last cell and any path beside it, and redraws both.</summary>
	internal static void DetachFromPath( ParkState state, ParkWorld park, int x, int y )
	{
		foreach ( var (bit, acrossBy, downBy) in Sides )
		{
			if ( bit is not (0x01 or 0x04 or 0x10 or 0x40) || !ParkState.OnMap( x + acrossBy, y + downBy ) )
				continue;

			var cell = ParkState.CellFor( park, x, y );
			var nb = ParkState.CellFor( park, x + acrossBy, y + downBy );

			if ( (cell.Neighbours & bit) == 0 || nb.Type != PathType || (nb.Neighbours & CellEdge.Opposite( bit )) == 0 )
				continue;

			state.SetRecord( x, y, cell with { Neighbours = (byte)(cell.Neighbours & ~bit) } );
			state.SetRecord( x + acrossBy, y + downBy,
				nb with { Neighbours = (byte)(nb.Neighbours & ~CellEdge.Opposite( bit )) } );

			Retile( state, park, x, y );
			Retile( state, park, x + acrossBy, y + downBy );
		}
	}

	/// <summary>
	/// Clears a demolished thing's whole queue, the placer's NOMODIFY node included, and answers what it
	/// gave back - the queue half of <c>FUN_00527ee0</c>.
	/// </summary>
	/// <remarks>
	/// <b>The queue's end is let go of its path first</b> (<c>FUN_00530120</c>, the walk mode <c>0x14</c>
	/// uses), then every cell is cleared under force - NOMODIFY or not, and with no unlink of the cells
	/// beside it (<c>FUN_0052fe50</c> driving op <c>0x32</c> into <c>FUN_005367a0</c>). <b>Each cell refunds
	/// its price, and then one cell's worth is taken back</b> (<c>FUN_004d01f0</c> after the drain), which
	/// is exactly the node the placer laid for nothing. A queue of four returns three cells' worth. With no
	/// queue cells there is neither drain nor debit.
	/// </remarks>
	internal static int DrainQueue( ParkState state, ParkWorld park, ParkWorld.CatalogueObject placed )
	{
		var start = ParkRideChoice.StartOfQueue( park, placed );
		var cells = new List<int>();

		for ( var cell = start; cell != 0 && cells.Count < ParkState.LongestQueue;
			cell = ParkRideChoice.StepToNextQueueCell( park, cell ) )
		{
			var (x, y) = MapStep.CellAt( cell );

			if ( ParkState.CellFor( park, x, y ).Type != ParkRideChoice.QueueCellType )
				break;

			cells.Add( cell );
		}

		if ( cells.Count == 0 )
			return 0;

		var (backX, backY) = MapStep.CellAt( cells[^1] );

		DetachFromPath( state, park, backX, backY );

		// The percentage the original scales a refund by is per-age, which nothing here keeps; see
		// LiftQueue, which gives a cell back in full for the same reason.
		var price = QueueCost( Level.Current );

		foreach ( var cell in cells )
		{
			var (x, y) = MapStep.CellAt( cell );

			state.SetRecord( x, y, ParkState.CellFor( park, x, y ) with
			{
				Type = NothingType,
				Neighbours = 0,
				Direction = 0,
				Flags = 0,
				ParentId = 0,
				TileSet = 0,
				TileIndex = 0,
				TileAngle = 0
			} );

			state.Refund( price );
		}

		state.Spend( price );
		state.InvalidateQueue( placed.ThingId );

		return (cells.Count - 1) * price;
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
	internal static void Unlink( ParkState state, ParkWorld park, int x, int y )
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
