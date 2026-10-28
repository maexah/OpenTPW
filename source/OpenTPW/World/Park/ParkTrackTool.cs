namespace OpenTPW;

/// <summary>
/// The karts' and the water rides' track tool, modes <c>0xb</c> and <c>0x10</c>: armed by a click on a piece of a ride's
/// track, it lays a straight run of pieces a click (<c>docs/exe/park.md</c>, "The track tool").
///
/// <para>
/// <b>Armed.</b> The idle click on a kart or water anchor (or a piece's other cell, 0xc in both kits, which names its anchor) installs
/// mode <c>0xe</c> (<c>0x13</c>) for the piece's ride (<c>FUN_004879d0</c>, category 5); its release walks the ride's track
/// from the mouth to the open end (<c>FUN_00530830</c>), which becomes the anchor, the walk's bends the pending list, and
/// arms <c>0xb</c> (<c>0x10</c>). Ours does both on the press.
/// </para>
/// <para>
/// <b>A click</b> (<c>0x00526cb3</c>..<c>0x00526fb2</c>) plays sound <c>0xce</c>, moves the target onto the anchor's row or
/// column, whichever is farther, and lays the run in 2-cell steps by eight passes over it (<c>FUN_0053aac0</c>, each op
/// through <c>Track_CellOp</c>, <c>FUN_005370e0</c>): the clear, the stamp, the links, the flow, the owner, the hill flags,
/// the mouths' walks and the retile. A run that ends on track closes the tool; any other moves the anchor to its end.
/// </para>
/// </summary>
public static class ParkTrackTool
{
	/// <summary>The sound every click of the track tool plays before it lays anything (<c>cat_ui</c> <c>0xce</c>, <c>0x00526ce2</c>).</summary>
	public const int ClickSound = 0xce;

	/// <summary>The step two cells on along a heading bit: north <c>0x01</c> is −y, east <c>0x04</c> +x, south <c>0x10</c> +y, west <c>0x40</c> −x.</summary>
	public static (int Dx, int Dy) StepOf( byte heading ) => heading switch
	{
		0x01 => (0, -2),
		0x04 => (2, 0),
		0x10 => (0, 2),
		0x40 => (-2, 0),
		_ => (0, 0)
	};

	/// <summary>The heading bit of a step, or nought for none.</summary>
	public static byte HeadingOf( int dx, int dy ) => dx < 0 ? (byte)0x40 : dx > 0 ? (byte)0x04 : dy < 0 ? (byte)0x01 : dy > 0 ? (byte)0x10 : (byte)0;

	/// <summary>
	/// The step the last link pass took (<c>DAT_00763b00</c>/<c>b04</c>), which the next run's first cell links back along,
	/// or null where the tools were put away, a run closed or Backspace took one back since. Those write <c>0x80</c>:
	/// <c>FUN_0052f200</c> with mode 0 (<c>0x0052f35e</c>) or <c>0x35</c> (<c>0x0052f55e</c>), the close
	/// (<c>0x00526fa7</c>) and Backspace (<c>0x0052ff25</c>). Installing a tool, the arming's <c>FUN_0052f580</c>, writes none.
	/// </summary>
	private static (int Dx, int Dy)? _lastLinkStep;

	/// <summary>Forgets the last link step, as putting the tools away does.</summary>
	public static void Forget() => _lastLinkStep = null;

	/// <summary>
	/// The anchor a click on this cell arms the tool on, or null where it arms nothing: a kart or water anchor itself, or a
	/// piece's other cell (0xc in both kits), which names its anchor (<c>FUN_004879d0</c>, <c>0x00487f39</c>..<c>0x00487f4e</c>).
	/// The hover sends only track types 0xb, 0xc and 0x10 to the track's category (<c>0x00487392</c>), so the stations arm
	/// nothing.
	/// </summary>
	public static (int X, int Y)? AnchorUnder( ParkState state, int x, int y )
	{
		var cell = state.Record( x, y );

		if ( cell.TrackType == ParkTrackPieces.KartCell )
		{
			(x, y) = MapStep.CellAt( cell.TrackParentId );
			cell = state.Record( x, y );
		}

		return ParkTrackPieces.IsPieceAnchor( cell.TrackType ) && cell.TrackParentId != 0 ? (x, y) : null;
	}

	/// <summary>What the arming walk found: the open end, the cells it pushed, and whether it met the station instead.</summary>
	public readonly record struct OpenEnd( (int X, int Y) End, IReadOnlyList<(int X, int Y)> Pending, bool Closed );

	/// <summary>
	/// The arming walk, <c>FUN_00530830</c>: from the mouth along its heading in 2-cell steps over laid track (0xb, 0x10),
	/// turning at each bend the way its links leave it, until a cell with no link onward, or the station met from its far
	/// side (the step after stands on it). The mouth (when laid), each bend and the cell it stops on are pushed; each cell's
	/// overlap counter is set, 1 at a bend or crossing and 0 on a straight or an end (<c>FUN_005228a0</c>).
	/// </summary>
	public static OpenEnd WalkToOpenEnd( ParkState state, int rideX, int rideY, int angle )
	{
		var (x, y) = ParkTrackPieces.StationOf( rideX, rideY, angle ).Mouth;
		var heading = ParkTrackPieces.HeadingOf( angle );
		var owner = state.Record( x, y ).TrackParentId;
		var pending = new List<(int X, int Y)>();
		var closed = false;

		if ( ParkTrackPieces.IsPieceAnchor( state.Record( x, y ).TrackType ) )
			pending.Add( (x, y) );

		// The original walks on until it stops, however far that is, with no bound; the map's cells bound this one.
		for ( var steps = 0; steps < ParkWorld.MapSize * ParkWorld.MapSize && ParkState.OnMap( x, y ); ++steps )
		{
			var cell = state.Record( x, y );
			var stop = false;

			if ( ParkTrackPieces.IsPieceAnchor( cell.TrackType ) )
			{
				var bend = IsBend( cell.TrackNeighbours );

				if ( bend )
				{
					heading = (byte)(cell.TrackNeighbours ^ CellEdge.Opposite( heading ));
					pending.Add( (x, y) );
				}

				state.SetRecord( x, y, cell with { TrackOverlapCounter = (short)(bend || CellEdge.Links( cell.TrackNeighbours ) >= 3 ? 1 : 0) } );

				if ( !bend && (heading & cell.TrackNeighbours) == 0 )
					stop = true;

				var (dx, dy) = StepOf( heading );
				var next = state.Record( x + dx, y + dy );

				if ( IsStation( state, next ) && next.TrackParentId == cell.TrackParentId && next.TrackParentId == owner
					&& (next.TrackNeighbours & CellEdge.Opposite( heading )) != 0 )
				{
					stop = true;
					closed = true;
					(x, y) = (x + dx, y + dy);
				}
			}

			if ( stop )
				break;

			var (stepX, stepY) = StepOf( heading );
			(x, y) = (x + stepX, y + stepY);
		}

		pending.Add( (x, y) );

		return new OpenEnd( (x, y), pending, closed );
	}

	/// <summary>A station cell, or a station's other cell (<c>FUN_0053adb0</c>).</summary>
	private static bool IsStation( ParkState state, ParkWorld.MapCell cell )
		=> cell.TrackType is ParkTrackPieces.KartStation or ParkTrackPieces.WaterStation
			|| (cell.TrackType is ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell
				&& state.Record( MapStep.CellAt( cell.TrackParentId ).X, MapStep.CellAt( cell.TrackParentId ).Y ).TrackType
					is ParkTrackPieces.KartStation or ParkTrackPieces.WaterStation);

	/// <summary>Where a mouth's walk stopped: whether it met the station from its far side, and the open end it stopped on.</summary>
	public readonly record struct Shaped( bool MetStation, (int X, int Y)? OpenEnd );

	/// <summary>
	/// A mouth's walk, <c>TrackRide_ShapeFromMouth</c> (<c>0x0052c6f0</c>), or with <paramref name="fromOtherMouth"/>
	/// <c>TrackRide_ShapeFromOtherMouth</c> (<c>0x0052ccb0</c>): from the mouth on the turn's heading, or from the other
	/// mouth away from the station, in 2-cell steps over laid track (0xb, 0x10), out of a bend the way its links leave it
	/// (its links less the way in, <c>opposite( heading )</c> where it links that way, else the heading), until a cell that
	/// is no bend and links no further on, the open end, or a station of the ride two cells on linking back, which answers
	/// 1. A start that is not track answers 1 at once. The original reshapes and retiles a hill on the way by its
	/// neighbours; that is counted, <c>TRACK_WALK_HILL_SHAPE</c>.
	/// </summary>
	public static Shaped Shape( ParkState state, int rideX, int rideY, int angle, bool fromOtherMouth )
	{
		var station = ParkTrackPieces.StationOf( rideX, rideY, angle );
		var (x, y) = fromOtherMouth ? station.OtherMouth : station.Mouth;
		var heading = ParkTrackPieces.HeadingOf( angle );

		if ( fromOtherMouth )
			heading = (byte)CellEdge.Opposite( heading );

		if ( !ParkState.OnMap( x, y ) || !IsTrack( state.Record( x, y ).TrackType ) )
			return new Shaped( true, null );

		// The original steps on until it stops, however far that is, with no bound; the map's cells bound this one.
		for ( var steps = 0; steps < ParkWorld.MapSize * ParkWorld.MapSize && ParkState.OnMap( x, y ); ++steps )
		{
			var cell = state.Record( x, y );

			if ( ParkTrackPieces.IsPieceAnchor( cell.TrackType ) )
			{
				var bend = IsBend( cell.TrackNeighbours );

				if ( bend )
				{
					var wayIn = (byte)CellEdge.Opposite( heading );

					if ( (wayIn & cell.TrackNeighbours) == 0 )
						wayIn = heading;

					heading = (byte)(cell.TrackNeighbours ^ wayIn);
				}
				else if ( (cell.TrackFlags & 0xf) != 0 )
				{
					Unimplemented.Report( "TRACK_WALK_HILL_SHAPE" );
				}

				(int X, int Y)? openEnd = !bend && (heading & cell.TrackNeighbours) == 0 ? (x, y) : null;
				var (dx, dy) = StepOf( heading );
				var met = ParkState.OnMap( x + dx, y + dy ) && state.Record( x + dx, y + dy ) is var next && IsStation( state, next )
					&& next.TrackParentId == cell.TrackParentId && (next.TrackNeighbours & CellEdge.Opposite( heading )) != 0;

				if ( openEnd != null || met )
					return new Shaped( met, openEnd );
			}

			var (stepX, stepY) = StepOf( heading );
			(x, y) = (x + stepX, y + stepY);
		}

		return new Shaped( false, null );
	}

	/// <summary>
	/// A pair of the mouths' walks: from the mouth, its open end pulsing where <paramref name="mouthPulses"/> and stopping
	/// where not, then from the other mouth the other way round, which <paramref name="onlyWhereOpen"/> skips where the
	/// first met the station (op 0x86's pair, <c>0x00538ca0</c>, and Backspace's, <c>0x00530025</c>). The pulse is on the
	/// open end's piece (<see cref="ParkTrackPieces.SetPulse"/>); a cell with no piece standing pulses nothing.
	/// </summary>
	public static (Shaped Mouth, Shaped? OtherMouth) ShapeMouths( ParkState state, int rideX, int rideY, int angle, bool mouthPulses,
		bool onlyWhereOpen )
	{
		var mouth = Shape( state, rideX, rideY, angle, fromOtherMouth: false );

		Pulse( mouth.OpenEnd, mouthPulses );

		if ( onlyWhereOpen && mouth.MetStation )
			return (mouth, null);

		var other = Shape( state, rideX, rideY, angle, fromOtherMouth: true );

		Pulse( other.OpenEnd, !mouthPulses );

		return (mouth, other);
	}

	/// <summary>
	/// A bought kart or water ride's put-down on the track grid: its station and mouths laid (<see cref="ParkTrackPieces.LayStation"/>),
	/// the pieces stood, and op 0x86's pair of walks, which leaves the mouth pulsing. Answers how many anchors were laid.
	/// </summary>
	public static int PutDown( ParkState state, int trackType, int x, int y, int angle )
	{
		var laid = ParkTrackPieces.LayStation( state, trackType, x, y, angle );

		ParkTrackPieces.Current?.Rebuild();
		ShapeMouths( state, x, y, angle, mouthPulses: true, onlyWhereOpen: true );

		return laid;
	}

	/// <summary>Where the walks' pulses go: the pieces standing, or in a test a recorder of its own.</summary>
	internal static Action<int, int, bool> PulseTo = ( x, y, on ) => ParkTrackPieces.Current?.SetPulse( x, y, on );

	private static void Pulse( (int X, int Y)? at, bool on )
	{
		if ( at is { } cell )
			PulseTo( cell.X, cell.Y, on );
	}

	/// <summary>Two links at right angles (<c>TrackCell_BendLinks</c>, <c>FUN_0053ae00</c>).</summary>
	public static bool IsBend( byte mask ) => CellEdge.Links( mask ) == 2 && (mask & 0x11) != 0x11 && (mask & 0x44) != 0x44;

	/// <summary>
	/// Where a click's run really ends: the target moved onto the anchor's row or column, whichever is farther, ties to the
	/// row (<c>0x00526d4f</c>..<c>0x00526d85</c>), then onto the anchor's 2-cell grid, an odd distance taken to the lower
	/// cell (<c>FUN_0053aac0</c>).
	/// </summary>
	public static (int X, int Y) TargetOf( (int X, int Y) anchor, int clickX, int clickY )
	{
		var across = Math.Abs( clickX - anchor.X );

		if ( Math.Abs( clickY - anchor.Y ) <= across )
			clickY = anchor.Y;

		if ( across <= Math.Abs( clickY - anchor.Y ) )
			clickX = anchor.X;

		return Math.Abs( clickY - anchor.Y ) < Math.Abs( clickX - anchor.X )
			? (OnGrid( anchor.X, clickX ), clickY)
			: (clickX, OnGrid( anchor.Y, clickY ));
	}

	private static int OnGrid( int start, int target )
	{
		var by = start - target;

		if ( by > 0 && (by & 1) != 0 )
			++by;

		return start - ((by / 2) * 2);
	}

	/// <summary>
	/// The anchors of a run from <paramref name="from"/> to its target (<see cref="TargetOf"/>: on the anchor's row or column
	/// and grid), in order, and their 2-cell step.
	/// </summary>
	public static (List<(int X, int Y)> Cells, int Dx, int Dy) CellsOf( (int X, int Y) from, (int X, int Y) to )
	{
		var (dx, dy) = (Math.Sign( to.X - from.X ) * 2, Math.Sign( to.Y - from.Y ) * 2);
		var steps = Math.Max( Math.Abs( to.X - from.X ), Math.Abs( to.Y - from.Y ) ) / 2;
		var cells = new List<(int X, int Y)>();

		for ( var i = 0; i < steps; ++i )
			cells.Add( (from.X + (i * dx), from.Y + (i * dy)) );

		cells.Add( to );

		return (cells, dx, dy);
	}

	/// <summary>A run's verdict: refused or not, the first reason, what its new pieces cost, and whether that total passes the balance (<c>DAT_00816d5c</c>), whether or not it is refused for another reason too.</summary>
	public readonly record struct Judged( bool Refused, string? Why, int Total, bool OverBalance = false );

	/// <summary>
	/// The preview's verdict on a run, cell by cell (<c>Track_CellVerdict</c>, <c>FUN_00539760</c>): refused where a piece's
	/// two by two leaves the map or the park's land (map flag <c>0x40</c>), stands on an object (map types 4, 9 and 10; blue over a
	/// feature of priority below 3, as the original answers, whose op <c>0x87</c> is <see cref="LayRun"/>'s first pass), on
	/// type 7, 0x15 or 30, or on another ride's track; where the run's last cell, unless it meets its own track, stands on
	/// queue, a ride's used-from cell or a cell flagged <c>0x20</c> (<c>0x00539fc5</c>..<c>0x0053a05f</c>); or where the new
	/// pieces together cost more than the balance. The total runs on past a refused cell, so a run refused for another reason
	/// can be over the balance too (<see cref="Judged.OverBalance"/>, <c>DAT_00816d5c</c>), which the pointer shows as <c>c_cash</c>. The river (map type 2) takes a plain piece. What the original allows
	/// that is not built here is refused too, and counted: a run over path or queue (<c>TRACK_RUN_OVER_PATH</c>: the
	/// original lays an end or straight there as a hill, op <c>0x85</c>, and a bend or junction plain), through or into its
	/// own track other than at its two ends (<c>TRACK_RUN_CROSSING</c>) and onto its own station
	/// (<c>TRACK_RUN_INTO_STATION</c>). <paramref name="count"/> false judges without counting, as the pointer's preview
	/// does every frame.
	/// </summary>
	public static Judged Verdict( ParkState state, int type, ushort owner, (int X, int Y) from, (int X, int Y) to, int price,
		bool count = true )
	{
		var (cells, _, _) = CellsOf( from, to );
		var total = 0;
		string? why = null;

		// The first refusal is the one said, and the pass goes on: the original's walk judges every cell, adds each empty
		// cell's price to the running total whether or not the cell is refused, and raises its over-the-balance latch the
		// first time the total passes the balance (0x00539f5c, 0x00539f87..0x00539fb6), so a refused run can be over it too.
		// Each counted gap is counted at the first refusal alone, as before.
		for ( var i = 0; i < cells.Count; ++i )
		{
			var judged = JudgeCell( state, type, owner, cells[i], i == 0, i == cells.Count - 1 );

			if ( judged.Why != null && why == null )
			{
				why = judged.Why;

				if ( count && judged.Gap != null )
					Unimplemented.Report( judged.Gap );
			}

			if ( judged.Empty )
				total += price;
		}

		var over = total > state.Balance;

		if ( over && why == null )
			why = $"the run's {total} is more than the balance {state.Balance}";

		return new Judged( why != null, why, total, over );
	}

	/// <summary>
	/// Which part of <c>Track_CellVerdict</c> refuses a cell, which decides what the refusal does to the cells after it in the
	/// preview: <see cref="Land"/> answers first and latches nothing (map flag <c>0x40</c>, <c>0x00539924</c>..
	/// <c>0x00539959</c>); <see cref="NearTrack"/> latches before the cell is charged (another ride's track among its three
	/// other cells, <c>0x00539c47</c>..<c>0x00539dbb</c>); <see cref="After"/> is judged after the charge and the money and
	/// leaves the refusal pending (<c>0x0053a307</c>), which the same anchor's second judgement turns into the latch
	/// (<c>0x00539dbc</c>..<c>0x00539dca</c>, before the third argument is read). <see cref="Scenery"/> answers blue at once,
	/// after the land and before the latch is read, uncharged and latching nothing (<c>0x00539961</c>..<c>0x00539c24</c>).
	/// </summary>
	public enum CellRefusal { None, Land, NearTrack, After, Scenery }

	/// <summary>One anchor of a run judged: its first reason (and the gap it counts, if any), which part refused it, and
	/// whether it is an empty cell on the map, the kind a run is charged for.</summary>
	public readonly record struct CellJudged( string? Why, string? Gap, CellRefusal Kind, bool Empty );

	/// <summary>The track modes' <c>[0x00818c10]</c> (<c>0x00523546</c>, <c>0x005244be</c>): a thing under a run whose item's priority is below it answers blue.</summary>
	private const int BuildsOverPriority = 3;

	/// <summary>
	/// The <c>Info.OverwritePriority</c> (item <c>+0x54</c>) of the thing a cell's owner names (its <c>ParentId</c>, a packed
	/// cell: <see cref="ParkBuilding.Stamp"/>), or null where no such thing or no catalogue is at hand. The running park's
	/// objects and catalogue unless a test stands in.
	/// </summary>
	internal static Func<ParkState, ushort, int?> OverwritePriorityOf { get; set; } = OverwritePriorityInLevel;

	private static int? OverwritePriorityInLevel( ParkState state, ushort owner )
	{
		if ( owner == 0 || Level.Current?.Catalogue is not { } catalogue )
			return null;

		var (ownerX, ownerY) = MapStep.CellAt( owner );

		foreach ( var placed in state.Objects )
		{
			if ( placed.CellX == ownerX && placed.CellY == ownerY )
				return catalogue.TryGet( placed.CatalogueId, out var item ) ? item.OverwritePriority : null;
		}

		return null;
	}

	/// <summary>
	/// One anchor of a run (<paramref name="at"/>) by <see cref="Verdict"/>'s rules, the money left out: its two by two's
	/// cells first, in the map's order, then its own track. Off the map is taken as land outside the park (the original
	/// asks a cell off the map nothing it answers here). A cell refused by several rules says the first and takes the kind
	/// that answers first in the original: land, then another ride's track beside it, then the rest.
	/// </summary>
	internal static CellJudged JudgeCell( ParkState state, int type, ushort owner, (int X, int Y) at, bool first, bool last )
	{
		var (x, y) = at;
		string? why = null;
		string? gap = null;
		var (land, near, scenery) = (false, false, false);
		var onMap = true;
		string? southFeature = null;

		void Refuse( string reason, string? counted = null )
		{
			if ( why != null )
				return;

			why = reason;
			gap = counted;
		}

		foreach ( var (cx, cy) in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) } )
		{
			if ( !ParkState.OnMap( cx, cy ) )
			{
				Refuse( $"({cx},{cy}) is off the map" );
				land = true;
				onMap = false;
				continue;
			}

			var under = state.Record( cx, cy );

			// The verdict asks only the anchor, its east cell and its south-east cell about the owner's priority; the south
			// cell is never asked (0x00539961..0x00539b75).
			var thing = under.Type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd;
			var gives = thing && OverwritePriorityOf( state, under.ParentId ) is < BuildsOverPriority;
			var south = (cx, cy) == (x, y + 1);
			var feature = gives && !south;

			scenery |= feature;

			if ( (under.Flags & 0x40) != 0 )
			{
				Refuse( $"({cx},{cy}) is outside the park's land" );
				land = true;
			}
			else if ( feature )
			{
				// Answered below: blue, with the rest of the anchor unasked.
			}
			else if ( gives && south )
				southFeature = $"({cx},{cy}) is map type {under.Type}"; // a feature the verdict never asks: refused unless a cell it does ask is one
			else if ( under.Type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd or 7 or 0x15 or CellEdge.Approach )
				Refuse( $"({cx},{cy}) is map type {under.Type}" );
			else if ( last && ParkState.OnMap( x, y ) && state.Record( x, y ).TrackType != type
				&& (under.Type is ParkRideChoice.QueueCellType or CellEdge.RideEnd || (under.Flags & 0x20) != 0) )
				Refuse( $"({cx},{cy}) is map type {under.Type}, flags 0x{under.Flags:x}, where a run may not end" );
			else if ( under.Type is CellEdge.Path or ParkRideChoice.QueueCellType )
				Refuse( $"({cx},{cy}) is map type {under.Type}, which the original crosses with a hill (not built)", "TRACK_RUN_OVER_PATH" );
			else if ( (cx, cy) != (x, y) && under.TrackType != 0 )
			{
				// The three other cells: any ride's track but this one's refuses (0x00539c47..0x00539dbb).
				var parent = under.TrackType is ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell
					? state.Record( MapStep.CellAt( under.TrackParentId ).X, MapStep.CellAt( under.TrackParentId ).Y )
					: under;

				if ( parent.TrackType is not 0 && parent.TrackParentId != owner )
				{
					Refuse( $"({cx},{cy}) is another ride's track" );
					near = true;
				}
			}
		}

		if ( !onMap )
			return new CellJudged( why, gap, CellRefusal.Land, false );

		// A feature on the south cell is part of the answer where the anchor's, east or south-east cell is one too (a rock
		// of two by two or more); alone it is a cell of map type 4, 9 or 10 like any other.
		if ( southFeature != null && (!scenery || land) )
			Refuse( southFeature );

		// In the track modes the original answers 0 here, blue, uncharged and latching nothing, at once and whatever else
		// the anchor's cells hold, for a thing whose item's OverwritePriority (+0x54) is below 3 (a feature: 0 or 1; the
		// category files all say 5), and the click's op 0x87 tears it down (0x00538cf0; LayRun). A deviation: where one of the
		// anchor's other cells would be refused by a rule of ours, the refusal stands and the run is counted, for the
		// original lays its piece over that cell too.
		if ( scenery && !land )
		{
			if ( why == null )
				return new CellJudged( null, null, CellRefusal.Scenery, false );

			gap = "TRACK_RUN_OVER_SCENERY_MIXED";
		}

		var cell = state.Record( x, y );

		if ( cell.TrackType != 0 )
		{
			if ( cell.TrackParentId != owner )
				Refuse( $"({x},{y}) is another ride's track" );
			else if ( cell.TrackType is ParkTrackPieces.KartStation or ParkTrackPieces.WaterStation )
				Refuse( $"({x},{y}) is the station, which a run meets only along its flow (not built)", "TRACK_RUN_INTO_STATION" );
			else if ( cell.TrackType != type )
				Refuse( $"({x},{y}) is track type 0x{cell.TrackType:x}" );
			else if ( !first && !(last && CellEdge.Links( cell.TrackNeighbours ) == 1) )
			{
				// The anchor itself, and an end piece the run closes on, are free; anything else of its own is a crossing or a
				// junction.
				Refuse( $"({x},{y}) is its own track, which the run would cross or join (not built)", "TRACK_RUN_CROSSING" );
			}
		}

		var kind = why == null ? CellRefusal.None
			: land ? CellRefusal.Land
			: near ? CellRefusal.NearTrack
			: CellRefusal.After;

		return new CellJudged( why, gap, kind, cell.TrackType == 0 );
	}

	/// <summary>
	/// The armed tool's preview of a run, as its pass draws it (<c>BuildTool_PreviewPass</c> <c>0x005234d0</c>, modes
	/// <c>0xb</c> and <c>0x10</c>: the pointer taken onto the anchor's row or column and grid, then <c>FUN_0053aac0</c> with
	/// op <c>0x10b</c> or <c>0x110</c>, one <c>Track_CellOp</c> square arm, <c>0x005370e0</c>, an anchor): the squares, one
	/// an anchor on the map, from the anchor to the target; whether any is red (the pass's refusal <c>DAT_00816d48</c>,
	/// <c>c_noplace</c>), whether the last met its own track (<c>DAT_00816d4c</c>, <c>c_end</c>), whether the run's total
	/// passed the balance (<c>DAT_00816d5c</c>, <c>c_cash</c>), the refusal latch the pass leaves (<c>DAT_00816d50</c>), and
	/// the run's total (<c>DAT_0081af4c</c>).
	/// </summary>
	public readonly record struct Previewed( List<ParkPathBuilding.QueueSquare> Squares, bool Red, bool End, bool OverBalance,
		bool Latched, int Total );

	/// <summary>
	/// <see cref="Previewed"/> for a run from <paramref name="from"/> to <paramref name="to"/>. Each anchor is judged by
	/// <see cref="JudgeCell"/>, in the original's order (<c>Track_CellVerdict</c>, <c>0x00539760</c>): land outside the park
	/// answers red first and latches nothing; another ride's track beside it latches; a latch an earlier cell left answers
	/// red before the charge; then an empty cell's price is added to the run's total, and a total above the balance answers
	/// red and latches (<c>0x00539f87</c>..<c>0x00539fb6</c>); then any other refusal answers red and, its second judgement
	/// being asked at once, latches. A cell not refused is blue (texture 0), or <c>m_end</c> (11, the verdict's answer
	/// <c>0xb</c>, <c>0x0053a1c6</c>) where it is the run's last and its own track: the anchor itself, or an end the run
	/// closes on. Every red square is judged again (<c>0x00537a29</c>, third argument 1): past the land, an empty cell is
	/// charged again and the total asked again, raising <c>DAT_00816d5c</c> alone. Each square covers its anchor's two by two
	/// (<see cref="ParkPathBuilding.QueueSquare.Wide"/>: the queue's <c>+0x18</c> 1, <c>0x00537a5c</c>, which
	/// <c>FUN_0053df30</c> lays 20 wide, <c>0x0053df39</c>). The own-track rules are <see cref="Verdict"/>'s, so the squares
	/// are red where this game's click refuses; where the original's answers differ (a hill over path or queue, a crossing,
	/// the station along its flow) <see cref="Verdict"/> says so.
	/// <b>Not reproduced:</b> for the one pass after the pointer leaves an anchor it rested on, the original's first square
	/// is judged as a last (<c>DAT_00818688</c>, <c>0x0053a0cb</c>); here the anchor is a last only while the pointer is on it.
	/// </summary>
	public static Previewed Preview( ParkState state, int type, ushort owner, (int X, int Y) from, (int X, int Y) to, int price )
	{
		var (cells, dx, dy) = CellsOf( from, to );
		var step = HeadingOf( dx, dy );
		var squares = new List<ParkPathBuilding.QueueSquare>();
		var (latch, red, end, over, total) = (false, false, false, false, 0);

		for ( var i = 0; i < cells.Count; ++i )
		{
			var (x, y) = cells[i];
			var last = i == cells.Count - 1;

			// FUN_0053aac0 asks Track_CellOp only of an anchor on the map (FUN_004d8300).
			if ( !ParkState.OnMap( x, y ) )
				continue;

			var judged = JudgeCell( state, type, owner, (x, y), i == 0, last );

			// The anchor, its own end, with the run leading back against its way on: the verdict's own-track rules answer it
			// red alone, latching nothing (0x0053a216 to 0x0053a251, then 0x0053a4e9: links not three), where the click lays
			// nothing anyway, for the cell beyond is its own track or its station. Not applied: the rule's hill (the anchor's
			// +0xe & 0xf) and add-on (0x10) flags, which refuse more (0x0053a307, red and pending); this game builds
			// neither, so only a park built in the original reaches them.
			if ( i == 0 && !last && judged.Kind == CellRefusal.None && step != 0 && state.Record( x, y ) is var anchor
				&& anchor.TrackType == type && anchor.TrackDirection == CellEdge.Opposite( step ) )
				judged = judged with { Why = $"({x},{y}) is left against its way on", Kind = CellRefusal.Land };

			var refused = true;
			var unaffordable = false;

			if ( judged.Kind == CellRefusal.Scenery )
				refused = false;
			else if ( judged.Kind != CellRefusal.Land )
			{
				latch |= judged.Kind == CellRefusal.NearTrack;

				if ( !latch )
				{
					if ( judged.Empty )
						total += price;

					if ( total > state.Balance )
						(latch, over, unaffordable) = (true, true, true);
					else if ( judged.Kind == CellRefusal.After )
						latch = true; // pending (0x0053a30d), copied into the latch by the red square's second judgement
					else
						refused = false;
				}

				// The red square's second judgement: no latch answers first, so an empty cell is charged again.
				if ( refused && judged.Empty )
				{
					total += price;
					over |= total > state.Balance;
				}
			}

			var cell = state.Record( x, y );
			var own = last && !refused && cell.TrackType == type && cell.TrackParentId == owner;

			red |= refused;
			end |= own;

			squares.Add( new ParkPathBuilding.QueueSquare( x, y,
				refused ? ParkPathBuilding.MarkerRed : own ? ParkPathBuilding.MarkerEnd : ParkPathBuilding.MarkerBlue,
				refused ? judged.Why ?? "the refusal latched" : null, unaffordable, total, Wide: true ) );
		}

		return new Previewed( squares, red, end, over, latch, total );
	}

	/// <summary>
	/// The preview for the pointer's cell with the track tool armed, in the park now loaded: <see cref="Preview"/> from the
	/// anchor to <see cref="TargetOf"/>, or, unanchored, the pointer's cell alone (the pass's <c>FUN_0053aac0</c> from the
	/// cell to itself); none where no ride is served.
	/// </summary>
	public static Previewed PreviewOver( int x, int y )
	{
		if ( Level.Current is not { ParkState: { } state } level || level.Catalogue is not { } catalogue
			|| state.Objects.FirstOrDefault( o => o.ThingId == ParkBuildMode.Serves ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) )
			return new Previewed( [], false, false, false, false, 0 );

		return PreviewFor( state, ride, ParkTrackPieces.PieceCost( level, item.TrackType ), x, y );
	}

	/// <summary><see cref="PreviewOver"/> for a park and the ride the tool serves, the anchor being <see cref="ParkBuildMode.Anchor"/>.</summary>
	internal static Previewed PreviewFor( ParkState state, ParkWorld.CatalogueObject ride, int price, int x, int y )
	{
		var type = ParkBuildMode.Current == ParkBuildMode.WaterTrack ? ParkTrackPieces.WaterAnchor : ParkTrackPieces.KartAnchor;
		var owner = (ushort)MapStep.CellId( ride.CellX, ride.CellY );

		if ( !ParkBuildMode.Anchored )
			return Preview( state, type, owner, (x, y), (x, y), price );

		return Preview( state, type, owner, ParkBuildMode.Anchor, TargetOf( ParkBuildMode.Anchor, x, y ), price );
	}

	/// <summary>What a run did: whether its last cell was track already (the retile pass answers nought), and what it charged.</summary>
	public readonly record struct Laid( bool MetTrack, int Charged, int NewPieces );

	/// <summary>
	/// Lays one run from the anchor to its target, pass by pass over every cell, as the click does (<c>0x00526daa</c>..
	/// <c>0x00526ebe</c>): the stamp, the links, the flow, the owner, the hill flags and the retile. The clear pass
	/// (<c>0x87</c>, <see cref="TearDownUnder"/>) comes first and tears down what the verdict answered blue over, a feature of
	/// priority below 3; the mouths' walks (<c>0x86</c>) run once for the run (<see cref="ShapeMouths"/>).
	/// </summary>
	public static Laid LayRun( ParkState state, int type, ushort owner, (int X, int Y) from, (int X, int Y) to, int price )
	{
		var (cells, dx, dy) = CellsOf( from, to );
		var charged = 0;
		var pieces = 0;
		var metTrack = false;

		TearDownUnder( state, cells );

		// The stamp (Track_StampCell, 0x00538fc0): its own type again is free and counts once more; a new piece is charged,
		// and refused where the balance cannot pay, which stops this pass alone.
		for ( var i = 0; i < cells.Count; ++i )
		{
			var (x, y) = cells[i];
			var cell = state.Record( x, y );

			if ( i == cells.Count - 1 )
				metTrack = cell.TrackType is ParkTrackPieces.KartAnchor or ParkTrackPieces.KartStation
					or ParkTrackPieces.WaterAnchor or ParkTrackPieces.WaterStation;

			if ( cell.TrackType == type )
			{
				state.SetRecord( x, y, cell with { TrackOverlapCounter = (short)(cell.TrackOverlapCounter + 1) } );
				continue;
			}

			if ( cell.TrackType != 0 || state.Balance - price < 0 )
				break;

			state.SetRecord( x, y, cell with { TrackType = type } );
			state.Spend( price );
			charged += price;
			++pieces;
		}

		// The links (FUN_00539220): each cell to the one before it, the run's first along the last run's step.
		for ( var i = 0; i < cells.Count; ++i )
		{
			var step = i == 0 ? _lastLinkStep : (dx, dy);

			if ( step is { } back && back != (0, 0) )
				LinkBack( state, cells[i], back, owner );

			_lastLinkStep = (dx, dy);
		}

		// The flow (FUN_00539670): an end, straight or bend takes the step as its way on; a crossing adds it.
		var flow = HeadingOf( dx, dy );

		foreach ( var (x, y) in cells )
		{
			var cell = state.Record( x, y );

			if ( flow != 0 )
				state.SetRecord( x, y, cell with { TrackDirection = CellEdge.Links( cell.TrackNeighbours ) < 3 ? flow : (byte)(cell.TrackDirection | flow) } );
		}

		// The owner (0x00538867): the anchor names the ride, its three other cells stamped 0xc naming the anchor.
		foreach ( var (x, y) in cells )
		{
			var parent = (ushort)MapStep.CellId( x, y );
			state.SetRecord( x, y, state.Record( x, y ) with { TrackParentId = owner } );

			foreach ( var (cx, cy) in new[] { (x + 1, y), (x, y + 1), (x + 1, y + 1) } )
			{
				if ( !ParkState.OnMap( cx, cy ) )
					continue;

				var other = state.Record( cx, cy ) with { TrackParentId = parent };

				other = other.TrackType switch
				{
					ParkTrackPieces.KartCell => other with { TrackOverlapCounter = (short)(other.TrackOverlapCounter + 1) },
					0 => other with { TrackType = ParkTrackPieces.KartCell },
					_ => other
				};

				state.SetRecord( cx, cy, other );
			}
		}

		// The hill flags (op 0x85, 0x00538a92): a piece over path or queue would be a hill, which the verdict refuses; over bare
		// ground an end or straight loses its hill flags.
		foreach ( var (x, y) in cells )
		{
			var cell = state.Record( x, y );

			if ( !IsBend( cell.TrackNeighbours ) && CellEdge.Links( cell.TrackNeighbours ) < 3 )
				state.SetRecord( x, y, cell with { TrackFlags = (ushort)(cell.TrackFlags & 0xfff0) } );
		}

		// The mouths' walks (op 0x86, 0x00538ca0): from the mouth with 1, then from the other mouth with 0 where the first met
		// no station. Every cell of the run asks the same pair of its owner, so one pair stands for them all.
		if ( owner != 0 && state.Objects.FirstOrDefault( o => MapStep.CellId( o.CellX, o.CellY ) == owner ) is { ThingId: not 0 } ride )
			ShapeMouths( state, ride.CellX, ride.CellY, ride.Angle, mouthPulses: true, onlyWhereOpen: true );

		// The retile (0x00538757..0x005387da): a bend, or a crossing with links, loses its hill flags; every piece but a
		// station's takes its row and turn again.
		foreach ( var (x, y) in cells )
		{
			var cell = state.Record( x, y );
			var links = CellEdge.Links( cell.TrackNeighbours );

			if ( IsBend( cell.TrackNeighbours ) || (links >= 3 && cell.TrackNeighbours != 0) )
				cell = cell with { TrackFlags = (ushort)(cell.TrackFlags & 0xfff0) };

			state.SetRecord( x, y, Retiled( cell ) );
		}

		return new Laid( metTrack, charged, pieces );
	}

	/// <summary>Sells one thing the way the demolisher does (<c>FUN_00527ee0</c>), and answers the line to show. The running park unless a test stands in.</summary>
	internal static Func<int, string> TearDownThing { get; set; } = ParkBuilding.Sell;

	/// <summary>
	/// The click's op <c>0x87</c> (<c>0x00538cf0</c>..<c>0x00538d9e</c>), its first pass: for each anchor that is not track
	/// (<c>0x00538d45</c>..<c>0x00538d6c</c>) the demolisher is called on its two by two's cells in the order (x,y), (x+1,y),
	/// (x,y+1), (x+1,y+1) (<c>0x00538d76</c>..<c>0x00538d9e</c>), which takes down the thing a cell typed 4, 9 or 10 belongs to
	/// (<c>FUN_00527d60</c>) and finds nothing in a cell it already took down. The pass is gated on <c>[0x00818c10]</c> (3)
	/// being above <c>[0x00818c14]</c>, the priority last stored (the last asked cell's owner, or the preview's target cell's):
	/// a run onto a cell read at 3 or more and still let through (the station along its flow, the ride's 5) finds it shut and
	/// tears nothing down. Every run this game lets through reads below 3, since it refuses the run into the station
	/// (<c>TRACK_RUN_INTO_STATION</c>), so the gate stands open here; building that gap gates this on the priority. The last anchor's own clear
	/// (<c>FUN_005367a0</c> on its four cells, <c>0x00538db3</c>..<c>0x00538e37</c>) leaves bare ground, the river and track as
	/// they are (its jump table, <c>0x00536c10</c>), and a path or queue is refused before the click, so it has nothing more to
	/// do. Like the original, a south cell that holds a ride's footprint is taken down too, the verdict never having asked it.
	/// </summary>
	internal static void TearDownUnder( ParkState state, List<(int X, int Y)> anchors )
	{
		foreach ( var (x, y) in anchors )
		{
			if ( state.Record( x, y ).TrackType != 0 )
				continue;

			foreach ( var (cx, cy) in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) } )
			{
				if ( !ParkState.OnMap( cx, cy ) || state.Record( cx, cy ) is not { Type: CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd } under )
					continue;

				var (ownerX, ownerY) = MapStep.CellAt( under.ParentId );

				foreach ( var placed in state.Objects.ToArray() )
				{
					if ( placed.CellX != ownerX || placed.CellY != ownerY )
						continue;

					Log.Info( $"track: op 0x87 takes down thing {placed.ThingId} under ({cx},{cy}): {TearDownThing( placed.ThingId )}" );
					break;
				}
			}
		}
	}

	/// <summary>
	/// A cell retiled (<c>Track_RetileCell</c>, <c>FUN_0053afa0</c>): a kart or water anchor takes its row and turn again by
	/// its links, way on and flags; a cell of no track takes row <c>0x37</c> of set nought at turn nought, no piece; a
	/// station or any other type is left as it is.
	/// </summary>
	private static ParkWorld.MapCell Retiled( ParkWorld.MapCell cell )
	{
		if ( cell.TrackType < 1 )
			return cell with { TrackTileSet = 0, TrackTileIndex = ClearedRow, TrackTileAngle = 0 };

		if ( !ParkTrackPieces.IsPieceAnchor( cell.TrackType ) )
			return cell;

		var (row, turn) = ParkTrackPieces.PieceFor( cell.TrackType, cell.TrackNeighbours, cell.TrackDirection, cell.TrackFlags );

		return cell with
		{
			TrackTileSet = cell.TrackType == ParkTrackPieces.KartAnchor ? ParkTrackPieces.KartFamily : ParkTrackPieces.WaterFamily,
			TrackTileIndex = row,
			TrackTileAngle = turn
		};
	}

	/// <summary>The row a cell of no track is retiled to (<c>0x0053b006</c>), which makes no piece.</summary>
	public const int ClearedRow = 0x37;

	/// <summary>Track to the walks: a kart or water anchor or station, or a boundary piece (<c>TrackCell_IsTrack</c>, <c>0x0053ad30</c>).</summary>
	private static bool IsTrack( int type )
		=> type is ParkTrackPieces.KartAnchor or ParkTrackPieces.KartStation or ParkTrackPieces.WaterAnchor or ParkTrackPieces.WaterStation or 0x19;

	/// <summary>
	/// The circuit's walk, <c>TrackRide_WalkCircuit( check )</c> (<c>FUN_0052a970</c>): from the station's first section
	/// along the ride's heading in 2-cell steps, straight on through a straight or a crossing and out of a bend the way its
	/// links leave it, until it is back where it started (1) or meets a cell that is not track or has under two links (0).
	/// The heading is the turn's way, as it is in the original at every click but one: a click on the mouth while the anchor
	/// is the mouth walks back towards the other mouth there, laying 1 section where this lays 2 (<c>docs/exe/park.md</c>,
	/// "The walk"; <c>+0x2c</c> is 0 in both).
	/// Unless checking, the ride's sections go first (<see cref="ParkTrackRideTable.ClearSections"/>) and each cell lays
	/// one: a bend 5 (links north and east), 6 (north, west), 7 (south, west) or 8 (east, south); a crossing 11; a straight
	/// 9 east-west or 10 north-south, flagged <c>0x10000</c> on the first two cells, the station's. Checking or not, a bend
	/// whose way on is not the way it leaves takes that way and is retiled (<c>0x0052b1a9</c>..<c>0x0052b1fc</c>).
	/// </summary>
	/// <returns>
	/// What the ride's <c>mIsTrackRideValid</c> (<c>+0x2c</c>) takes, 1 for a closed circuit, else 0; and how many cells
	/// it stepped from, the count each cell's <c>+0x24</c> takes in the original (<c>FUN_0053aef0</c>), not kept here.
	/// </returns>
	/// <param name="addOnCaps">
	/// What shows and hides an add-on's end caps, given its thing and the bits (<see cref="ParkObjects.ShowAddOnCaps"/>
	/// where null): 1 where track with a model or a station lies two cells back along the add-on's way on, 0x10 where it
	/// lies two on, each hiding that side's cap. It is called at an add-on cell walked second in a row, the bits of every
	/// add-on cell walked so far ORed, as the original's are: its count goes back to nought at a straight without an add-on
	/// (and, unless checking, at a bend or a crossing), its bits never (<c>0x0052ade3</c>..<c>0x0052af54</c>). So an add-on
	/// of one piece is never reached, and a later one takes an earlier one's bits.
	/// </param>
	public static (int Valid, int Steps) WalkCircuit( ParkState state, ParkWorld.CatalogueObject ride, bool check,
		Action<int, int>? addOnCaps = null )
	{
		var start = ParkTrackPieces.StationOf( ride.CellX, ride.CellY, ride.Angle ).First;
		var heading = ParkTrackPieces.HeadingOf( ride.Angle );
		var (x, y) = start;

		if ( !ParkState.OnMap( x, y ) )
			return (0, 0);

		// A piece's other cell names its anchor (FUN_0053ad90, 0x0052aa1c), which decides alone whether there is track
		// and whose ride it is; the walk itself starts on the cell.
		var first = state.Record( x, y );

		if ( first.TrackType is ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell )
			first = state.Record( MapStep.CellAt( first.TrackParentId ).X, MapStep.CellAt( first.TrackParentId ).Y );

		if ( !IsTrack( first.TrackType ) )
			return (0, 0);

		var handle = state.Objects.FirstOrDefault( o => MapStep.CellId( o.CellX, o.CellY ) == first.TrackParentId ).TrackRide;

		if ( !check )
			state.TrackRides.ClearSections( handle );

		var valid = 1;
		var retiled = false;
		var step = 0;
		var addOns = 0;
		var addOnBits = 0;

		// The original walks until it is back or stops, however far that is; a map's cells bound this one.
		for ( ; step < ParkWorld.MapSize * ParkWorld.MapSize; )
		{
			var cell = state.Record( x, y );
			var mask = cell.TrackNeighbours;

			if ( !IsTrack( cell.TrackType ) )
				heading = 0;
			else
			{
				var links = CellEdge.Links( mask );

				var addOn = (cell.TrackFlags & ParkTrackPieces.AddOnFlag) != 0;

				if ( IsBend( mask ) )
				{
					if ( !check )
					{
						state.TrackRides.Lay( handle, mask switch { 0x05 => 5, 0x41 => 6, 0x50 => 7, _ => 8 }, x * 0xc00, y * 0xc00 );
						addOns = 0;
					}
				}
				else if ( links >= 3 )
				{
					if ( !check )
					{
						state.TrackRides.Lay( handle, 11, x * 0xc00, y * 0xc00 );
						addOns = 0;
					}
				}
				else if ( links == 2 )
				{
					// An add-on's cell (flag 0x10) lays 12 or 13 and keeps the add-on's model.
					var kind = (step < 2 ? 0x10000 : 0) | (mask == 0x11 ? (addOn ? 13 : 10) : (addOn ? 12 : 9));

					if ( !addOn )
						addOns = 0;

					if ( !check )
						state.TrackRides.Lay( handle, kind, x * 0xc00, y * 0xc00 );
				}

				// "Found upgrade... modify?": the add-on's thing is the occupant of its cell's parent (FUN_00527d60), and its
				// caps are shown or hidden by the track either side of the cell (0x0052ade3..0x0052b18c).
				if ( addOn )
				{
					++addOns;

					if ( AddOnThingAt( state, x, y ) is { } thing )
					{
						addOnBits |= (CarriesOn( state, x, y, cell.TrackDirection ) ? 0x10 : 0)
							| (CarriesOn( state, x, y, (byte)CellEdge.Opposite( cell.TrackDirection ) ) ? 0x01 : 0);

						if ( addOns == 2 )
						{
							if ( addOnCaps != null )
								addOnCaps( thing, addOnBits );
							else
								ParkObjects.Current?.ShowAddOnCaps( thing, addOnBits );
						}
					}
				}

				// Out of a bend the way its links leave it; a bend entered past its links keeps the heading as the link
				// it leaves by (0x0052b1cc).
				if ( IsBend( mask ) )
				{
					var back = (byte)CellEdge.Opposite( heading );

					if ( (back & mask) == 0 )
						back = heading;

					heading = (byte)(mask ^ back);

					if ( cell.TrackDirection != heading )
					{
						state.SetRecord( x, y, Retiled( cell with { TrackDirection = heading } ) );
						retiled = true;
					}
				}
			}

			if ( heading == 0 || CellEdge.Links( mask ) < 2 )
				valid = 0;

			// Three links: the walk goes on only where the cell beyond is of some type, and not, heading north, a piece of
			// track with no links (0x0052b22f..0x0052b280).
			if ( CellEdge.Links( mask ) == 3 )
			{
				var (bx, by) = StepOf( heading );

				if ( !ParkState.OnMap( x + bx, y + by ) )
					valid = 0;
				else
				{
					var beyond = state.Record( x + bx, y + by );

					if ( (IsTrack( beyond.TrackType ) && beyond.TrackNeighbours == 0 && (heading & 0x01) != 0) || beyond.TrackType == 0 )
						valid = 0;
				}
			}

			var (dx, dy) = StepOf( heading );
			(x, y) = (x + dx, y + dy);
			++step;

			if ( (x, y) == start || valid == 0 || !ParkState.OnMap( x, y ) )
				break;
		}

		if ( !ParkState.OnMap( x, y ) )
			valid = 0;

		if ( retiled )
			ParkTrackPieces.Current?.Rebuild();

		Log.Info( $"track: the circuit's walk for thing {ride.ThingId}{(check ? ", checking" : "")}: +0x2c {valid}, "
			+ $"{step} step(s), {state.TrackRides.SectionsOf( handle ).Count} section(s)" );

		return (valid, step);
	}

	/// <summary>
	/// The thing an add-on's cell is of, or null: the occupant of the parent its map record names, where the map type is 4
	/// (an object's), 9 or 10 (<c>FUN_00527d60</c>).
	/// </summary>
	private static int? AddOnThingAt( ParkState state, int x, int y )
	{
		var cell = state.Record( x, y );

		if ( cell.Type is not (4 or 9 or 10) )
			return null;

		var (px, py) = cell.ParentId != 0 ? MapStep.CellAt( cell.ParentId ) : (x, y);

		// The head of the parent's own list (FUN_004d99e0), which a put-down's EnterCell writes at once.
		return ParkState.OnMap( px, py ) && state.CellAt( px, py ).Occupant is var thing and not 0 ? thing : null;
	}

	/// <summary>
	/// Whether the anchor two cells from (<paramref name="x"/>, <paramref name="y"/>) the way <paramref name="way"/> carries
	/// the track on for an add-on's cap: a kart or water piece with a model of its own (<c>+4</c>), or a station
	/// (<c>0x0052af07</c>, <c>0x0052af4d</c>). Another add-on's cell has no model of the kit's, so does not.
	/// </summary>
	private static bool CarriesOn( ParkState state, int x, int y, byte way )
	{
		var (dx, dy) = StepOf( way );

		if ( (dx, dy) == (0, 0) || !ParkState.OnMap( x + dx, y + dy ) )
			return false;

		var next = state.Record( x + dx, y + dy );

		return next.TrackType is ParkTrackPieces.KartStation or ParkTrackPieces.WaterStation
			|| ParkTrackPieces.PieceAt( x + dx, y + dy, next ) != null;
	}

	/// <summary>
	/// One link of the link pass: the cell and the one two back along <paramref name="step"/>, where that one is track of this
	/// ride or of none and neither has four links already.
	/// </summary>
	private static void LinkBack( ParkState state, (int X, int Y) at, (int Dx, int Dy) step, ushort owner )
	{
		var (bx, by) = (at.X - step.Dx, at.Y - step.Dy);

		if ( !ParkState.OnMap( bx, by ) )
			return;

		var cell = state.Record( at.X, at.Y );
		var back = state.Record( bx, by );

		if ( back.TrackType is not (ParkTrackPieces.KartAnchor or ParkTrackPieces.KartStation or ParkTrackPieces.WaterAnchor
				or ParkTrackPieces.WaterStation or 0x19)
			|| CellEdge.Links( cell.TrackNeighbours ) >= 4 || CellEdge.Links( back.TrackNeighbours ) >= 4
			|| (back.TrackParentId != owner && back.TrackParentId != 0) )
			return;

		var toward = HeadingOf( -step.Dx, -step.Dy );

		state.SetRecord( at.X, at.Y, cell with { TrackNeighbours = (byte)(cell.TrackNeighbours | toward) } );
		state.SetRecord( bx, by, back with { TrackNeighbours = (byte)(back.TrackNeighbours | CellEdge.Opposite( toward )) } );
	}

	/// <summary>The four link bits in the order the original's loops take them (<c>FUN_004d8c20( bit, 4 )</c>).</summary>
	private static readonly byte[] Bits = [0x01, 0x04, 0x10, 0x40];

	/// <summary>What a run taken back did: the pieces it removed and the money they gave back.</summary>
	public readonly record struct TakenBack( int Removed, int Refunded );

	/// <summary>
	/// Takes back one run, as Backspace does with the track tool armed (<c>BuildTool_Backtrack</c>, <c>0x0052fe50</c>): in
	/// mode 0x35 its two clicks lay op <c>0x32</c> (<c>Track_ClearCell</c>, <c>0x0053b280</c>) over every anchor from the
	/// run's end back to its start, each given the step toward the start (<c>FUN_0053aac0</c> from <c>0x00524960</c>), then
	/// the mouth is tidied (<c>FUN_0052fc80</c>), its bend broken where the list is back at its first entry. A piece the run
	/// laid falls below nought and goes, refunding its price; the start, which the run bumped, keeps its piece.
	/// </summary>
	public static TakenBack TakeBack( ParkState state, ParkWorld.CatalogueObject ride, (int X, int Y) end, (int X, int Y) start,
		int price, bool backToFirst )
	{
		var station = ParkTrackPieces.StationOf( ride.CellX, ride.CellY, ride.Angle );
		var (cells, dx, dy) = CellsOf( end, start );
		var removed = 0;
		var refunded = 0;

		foreach ( var at in cells )
			refunded += ClearCell( state, at, (dx, dy), station.Mouth, station.OtherMouth, price, ref removed );

		TidyMouth( state, station.Mouth, backToFirst );

		return new TakenBack( removed, refunded );
	}

	/// <summary>
	/// Op <c>0x32</c> on one anchor, <c>Track_ClearCell( dx, dy, 1 )</c>: its overlap counter down by one (to −1 outright on
	/// a step of nought but at the mouth); on a cell with one way on, unless a mouth, the link back toward the cells already
	/// cleared goes, and a bend left with one link takes the way out of it; on a crossing that way's bit and both links on
	/// the step's axis go. Below nought, and no mouth, the piece goes: its price refunded (<c>0x0053b603</c>..
	/// <c>0x0053b627</c>), each link let go at both ends, a neighbour left with one link turned to lead out of it, the anchor
	/// and the cells of its two by two made bare and retiled to row <c>0x37</c>. The mouth then links back to its station.
	/// </summary>
	private static int ClearCell( ParkState state, (int X, int Y) at, (int Dx, int Dy) step, (int X, int Y) mouth,
		(int X, int Y) otherMouth, int price, ref int removed )
	{
		if ( !ParkState.OnMap( at.X, at.Y ) )
			return 0;

		var cell = state.Record( at.X, at.Y );
		var isMouth = at == mouth;
		var guarded = isMouth || at == otherMouth;
		var refund = 0;

		// A piece's other cell is cleared through its anchor (0x0053b2c3..0x0053b2ea); a run's cells are all anchors on its grid.
		if ( cell.TrackType is ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell )
		{
			Unimplemented.Report( "TRACK_CLEAR_PIECE_CELL" );
			return 0;
		}

		if ( IsTrack( cell.TrackType ) )
		{
			if ( step == (0, 0) && !isMouth )
				cell = cell with { TrackOverlapCounter = -1 };
			else
			{
				cell = cell with { TrackOverlapCounter = (short)(cell.TrackOverlapCounter - 1) };

				var behind = (byte)CellEdge.Opposite( HeadingOf( step.Dx, step.Dy ) );

				if ( CellEdge.Links( cell.TrackDirection ) < 2 )
				{
					if ( !guarded )
					{
						var bend = IsBend( cell.TrackNeighbours );

						cell = cell with { TrackNeighbours = (byte)(cell.TrackNeighbours & ~behind) };

						if ( bend && CellEdge.Links( cell.TrackNeighbours ) == 1 )
							cell = cell with { TrackDirection = (byte)CellEdge.Opposite( cell.TrackNeighbours ) };

						cell = Retiled( cell );
					}
				}
				else
				{
					var links = cell.TrackNeighbours;

					if ( Math.Abs( step.Dx ) == 2 )
						links &= unchecked((byte)~0x44);

					if ( Math.Abs( step.Dy ) == 2 )
						links &= unchecked((byte)~0x11);

					cell = Retiled( cell with { TrackDirection = (byte)(cell.TrackDirection & ~behind), TrackNeighbours = links } );
				}
			}

			state.SetRecord( at.X, at.Y, cell );

			if ( cell.TrackOverlapCounter < 0 && !guarded )
			{
				refund = Remove( state, at, price );
				++removed;
			}
		}

		if ( isMouth && IsTrack( state.Record( at.X, at.Y ).TrackType ) )
			RelinkClearedMouth( state, at );

		return refund;
	}

	/// <summary>The removal of <see cref="ClearCell"/>: answers the refund.</summary>
	private static int Remove( ParkState state, (int X, int Y) at, int price )
	{
		var cell = state.Record( at.X, at.Y ) with { TrackOverlapCounter = 0 };
		var refund = 0;

		// The refund is the price at the ride's scrap percentage (FUN_004e2290), 100 for a ride under thirty days old with
		// no customer; the percentage is not kept here, so the whole price comes back.
		if ( cell.TrackType != 0x19 && price > 0 )
		{
			Unimplemented.Report( "SCRAP_VALUE_DEPRECIATION" );
			state.Deposit( price );
			refund = price;
		}

		foreach ( var bit in Bits )
		{
			if ( (cell.TrackNeighbours & bit) == 0 )
				continue;

			cell = cell with { TrackNeighbours = (byte)(cell.TrackNeighbours & ~bit) };

			var (dx, dy) = StepOf( bit );

			if ( !ParkState.OnMap( at.X + dx, at.Y + dy ) )
				continue;

			var next = state.Record( at.X + dx, at.Y + dy );
			next = Retiled( next with { TrackNeighbours = (byte)(next.TrackNeighbours & ~CellEdge.Opposite( bit )) } );

			if ( CellEdge.Links( next.TrackNeighbours ) == 1 && next.TrackDirection != next.TrackNeighbours
				&& next.TrackDirection != CellEdge.Opposite( next.TrackNeighbours ) )
				next = next with { TrackDirection = (byte)CellEdge.Opposite( next.TrackNeighbours ) };

			state.SetRecord( at.X + dx, at.Y + dy, next );
		}

		// An add-on's thing is torn down with its cell (FUN_00527ee0).
		if ( (cell.TrackFlags & ParkTrackPieces.AddOnFlag) != 0 )
			Unimplemented.Report( "TRACK_CLEAR_ADD_ON" );

		var id = (ushort)MapStep.CellId( at.X, at.Y );

		state.SetRecord( at.X, at.Y, Retiled( cell with { TrackDirection = 0, TrackFlags = 0, TrackType = 0, TrackParentId = 0 } ) );

		foreach ( var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1) } )
		{
			if ( !ParkState.OnMap( at.X + dx, at.Y + dy ) || state.Record( at.X + dx, at.Y + dy ).TrackParentId != id )
				continue;

			state.SetRecord( at.X + dx, at.Y + dy, Retiled( state.Record( at.X + dx, at.Y + dy ) with
			{
				TrackNeighbours = 0, TrackDirection = 0, TrackFlags = 0, TrackType = 0, TrackOverlapCounter = 0, TrackParentId = 0
			} ) );
		}

		return refund;
	}

	/// <summary>
	/// The mouth after its own clear (<c>0x0053b8bb</c>..<c>0x0053ba7d</c>): linked to a station of its ride two cells off
	/// where it was not, with the way out of it; a link to no track let go; and with one link left, its way on away from
	/// the station.
	/// </summary>
	private static void RelinkClearedMouth( ParkState state, (int X, int Y) at )
	{
		foreach ( var bit in Bits )
		{
			var cell = state.Record( at.X, at.Y );

			if ( (cell.TrackNeighbours & bit) != 0 || Beyond( state, at, bit ) is not { } next )
				continue;

			if ( IsStation( state, next ) && next.TrackParentId == cell.TrackParentId )
				state.SetRecord( at.X, at.Y, Retiled( cell with
				{
					TrackDirection = (byte)CellEdge.Opposite( bit ), TrackNeighbours = (byte)(cell.TrackNeighbours | bit)
				} ) );

			if ( !IsTrack( next.TrackType ) )
				state.SetRecord( at.X, at.Y, Retiled( state.Record( at.X, at.Y ) with
				{
					TrackNeighbours = (byte)(state.Record( at.X, at.Y ).TrackNeighbours & ~bit)
				} ) );
		}

		var mouth = state.Record( at.X, at.Y );

		if ( CellEdge.Links( mouth.TrackNeighbours ) != 1 )
			return;

		mouth = mouth with { TrackDirection = 0 };

		foreach ( var bit in Bits )
		{
			if ( (mouth.TrackNeighbours & bit) != 0 && Beyond( state, at, bit ) is { } next && IsStation( state, next )
				&& next.TrackParentId == mouth.TrackParentId )
				mouth = mouth with { TrackDirection = (byte)CellEdge.Opposite( bit ) };
		}

		state.SetRecord( at.X, at.Y, mouth );
	}

	/// <summary>
	/// The mouth tidied after a take-back, <c>FUN_0052fc80( backToFirst )</c>: linked to a station of its ride two cells off
	/// where it was not, with the way out of it; a link to no track, or to track not linking back, let go; and where the
	/// list is back at its first entry and the mouth is a bend, the bend broken: the link to anything but the station let go
	/// at both ends, the way on away from the station.
	/// </summary>
	private static void TidyMouth( ParkState state, (int X, int Y) at, bool backToFirst )
	{
		if ( !ParkState.OnMap( at.X, at.Y ) || !IsTrack( state.Record( at.X, at.Y ).TrackType ) )
			return;

		foreach ( var bit in Bits )
		{
			var cell = state.Record( at.X, at.Y );

			if ( (cell.TrackNeighbours & bit) == 0 )
			{
				if ( Beyond( state, at, bit ) is { } station && IsStation( state, station ) && station.TrackParentId == cell.TrackParentId )
					state.SetRecord( at.X, at.Y, Retiled( cell with
					{
						TrackDirection = (byte)CellEdge.Opposite( bit ), TrackNeighbours = (byte)(cell.TrackNeighbours | bit)
					} ) );

				continue;
			}

			// The original reads the cell two on without a test here; a mouth's links stay on the map.
			if ( Beyond( state, at, bit ) is not { } next )
				continue;

			var letGo = IsTrack( next.TrackType )
				? (next.TrackNeighbours & CellEdge.Opposite( bit )) == 0
				: !IsStation( state, next );

			if ( letGo )
				state.SetRecord( at.X, at.Y, Retiled( cell with { TrackNeighbours = (byte)(cell.TrackNeighbours & ~bit) } ) );
		}

		if ( !backToFirst || !IsBend( state.Record( at.X, at.Y ).TrackNeighbours ) )
			return;

		foreach ( var bit in Bits )
		{
			var cell = state.Record( at.X, at.Y );

			if ( (cell.TrackNeighbours & bit) == 0 || Beyond( state, at, bit ) is not { } next )
				continue;

			if ( !IsStation( state, next ) )
			{
				state.SetRecord( at.X, at.Y, Retiled( cell with { TrackNeighbours = (byte)(cell.TrackNeighbours & ~bit) } ) );

				var (dx, dy) = StepOf( bit );
				state.SetRecord( at.X + dx, at.Y + dy, Retiled( next with
				{
					TrackNeighbours = (byte)(next.TrackNeighbours & ~CellEdge.Opposite( bit ))
				} ) );
			}
			else
				state.SetRecord( at.X, at.Y, cell with { TrackDirection = (byte)CellEdge.Opposite( bit ) } );
		}
	}

	/// <summary>The cell two on along a link bit (<c>FUN_0050c3e0( bit, 2 )</c>), or null off the map.</summary>
	private static ParkWorld.MapCell? Beyond( ParkState state, (int X, int Y) at, byte bit )
	{
		var (dx, dy) = StepOf( bit );

		return ParkState.OnMap( at.X + dx, at.Y + dy ) ? state.Record( at.X + dx, at.Y + dy ) : null;
	}

	/// <summary>
	/// Backspace with the track tool armed: the last run taken back (<see cref="TakeBack"/>) and the anchor put back at its
	/// start, the tool staying armed. With only the first entry left there is nothing to take back, and the mouth is
	/// tidied alone. Either way the walks follow, from the mouth with 0 and from the other mouth with 1 where the first met
	/// no station (<c>0x00530025</c>, <c>0x00530082</c>), so the open end left pulses.
	/// </summary>
	public static string Backtrack()
	{
		if ( Level.Current is not { ParkState: { } state } level || level.Catalogue is not { } catalogue )
			return "backspace: a park has to be loaded";

		if ( state.Objects.FirstOrDefault( o => o.ThingId == ParkBuildMode.Serves ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) )
			return $"backspace: thing {ParkBuildMode.Serves} is gone";

		return BacktrackOn( state, ride, ParkTrackPieces.PieceCost( level, item.TrackType ) );
	}

	/// <summary>
	/// <see cref="Backtrack"/> on a park and a ride, the list of run ends being <see cref="ParkBuildMode.Pending"/>: the last
	/// run is popped, taken back and the anchor put at its start; the mouth's bend is broken where the list is then back at
	/// its first entry (<c>FUN_0052fc80( count == 1 )</c>, <c>0x0052ff6e</c>..<c>0x00530005</c>).
	/// </summary>
	internal static string BacktrackOn( ParkState state, ParkWorld.CatalogueObject ride, int price )
	{
		var mouth = ParkTrackPieces.StationOf( ride.CellX, ride.CellY, ride.Angle ).Mouth;

		// The red latch is let go first (FUN_00527940( 0 )), and the next run's first cell links along nothing (0x80).
		ParkBuildMode.RefusalLatched = false;
		_lastLinkStep = null;

		if ( !ParkBuildMode.TryPopRun( out var end, out var start ) )
		{
			TidyMouth( state, mouth, backToFirst: false );
			ShapeMouths( state, ride.CellX, ride.CellY, ride.Angle, mouthPulses: false, onlyWhereOpen: true );
			return "backspace: nothing laid since the track tool was armed";
		}

		var balance = state.Balance;
		var taken = TakeBack( state, ride, end, start, price, backToFirst: ParkBuildMode.Pending.Count == 1 );

		ParkBuildMode.AnchorAt( start.X, start.Y );
		ParkTrackPieces.Current?.Rebuild();

		// The pair of walks after it (0x00530025, 0x0053003e).
		ShapeMouths( state, ride.CellX, ride.CellY, ride.Angle, mouthPulses: false, onlyWhereOpen: true );

		var report = $"took back the run from ({start.X},{start.Y}) to ({end.X},{end.Y}), {taken.Removed} piece(s) removed, "
			+ $"{taken.Refunded} refunded, balance {balance} -> {state.Balance} - the anchor is ({start.X},{start.Y})";

		Log.Info( $"track: {report}" );

		return $"backspace: {report}";
	}

	/// <summary>
	/// The armed tool's pointer over a cell (<c>FUN_0052f950</c>, <c>0x0052f9a9</c>), from its preview
	/// (<see cref="PreviewOver"/>): <c>c_end</c> (<c>0x12</c>) where the run's last square met its own track
	/// (<c>DAT_00816d4c</c>), else <c>c_track.ani</c> (cursor 6); over either <c>c_noplace</c> (8) where any square is red,
	/// and over that <c>c_cash</c> (<c>0x14</c>) where the run's total passed the balance, which wins over the red even where
	/// the run is refused for something else too. <c>c_link</c> (5) is the path verdict's alone.
	/// </summary>
	public static Input.CursorTypes CursorOver( int x, int y ) => CursorOf( PreviewOver( x, y ) );

	/// <summary><see cref="CursorOver"/> for a park and the ride the tool serves, the anchor being <see cref="ParkBuildMode.Anchor"/>.</summary>
	internal static Input.CursorTypes CursorFor( ParkState state, ParkWorld.CatalogueObject ride, int price, int x, int y )
		=> CursorOf( PreviewFor( state, ride, price, x, y ) );

	/// <summary>The pointer a preview gives: each of <c>FUN_0052f950</c>'s tests over the last (<c>0x0052f9a9</c>..).</summary>
	internal static Input.CursorTypes CursorOf( Previewed preview )
		=> preview.OverBalance ? Input.CursorTypes.Cash
			: preview.Red ? Input.CursorTypes.NoGo
			: preview.End ? Input.CursorTypes.End
			: Input.CursorTypes.Track;

	/// <summary>
	/// The idle click on a piece: arms the tool for its ride at its track's open end. A ride whose track is closed is
	/// edited by two clicks in the original (mode <c>0xe</c>'s other arm), not built: <c>TRACK_EDIT_CLOSED_LOOP</c>.
	/// </summary>
	public static string Arm( int x, int y )
	{
		if ( Level.Current is not { ParkState: { } state } || Level.Current.Catalogue is not { } catalogue )
			return "track: a park has to be loaded";

		return ArmOn( state, id => catalogue.TryGet( id, out var item ) ? item.TrackType : null, x, y );
	}

	/// <summary><see cref="Arm"/> on a park, the track type of a catalogue id being <paramref name="trackTypeOf"/> (null for none).</summary>
	internal static string ArmOn( ParkState state, Func<int, int?> trackTypeOf, int x, int y )
	{
		if ( AnchorUnder( state, x, y ) is not { } anchor )
			return $"track: ({x},{y}) is no piece of track";

		var owner = state.Record( anchor.X, anchor.Y ).TrackParentId;

		if ( state.Objects.FirstOrDefault( o => MapStep.CellId( o.CellX, o.CellY ) == owner ) is not { ThingId: not 0 } ride
			|| trackTypeOf( ride.CatalogueId ) is not { } trackType || ParkTrackPieces.AnchorOf( trackType ) == 0 )
			return $"track: the piece at ({anchor.X},{anchor.Y}) belongs to no track ride";

		var walk = WalkToOpenEnd( state, ride.CellX, ride.CellY, ride.Angle );

		if ( walk.Closed )
		{
			Unimplemented.Report( "TRACK_EDIT_CLOSED_LOOP" );
			return $"track: thing {ride.ThingId}'s track is closed, and editing a closed loop is not built";
		}

		var mode = trackType == 2 ? ParkBuildMode.WaterTrack : ParkBuildMode.KartTrack;
		var armed = ParkBuildMode.ArmAt( mode, ride.ThingId, walk.End.X, walk.End.Y );

		ParkBuildMode.ClearPending();

		foreach ( var (px, py) in walk.Pending )
			ParkBuildMode.Push( px, py );

		_lastLinkStep = null;

		// The arming's pair of walks (0x00526c89, 0x00526c9c): the other mouth's open end pulses while the loop is open.
		ShapeMouths( state, ride.CellX, ride.CellY, ride.Angle, mouthPulses: false, onlyWhereOpen: false );

		return $"{armed} (the open end, {walk.Pending.Count} pending)";
	}

	/// <summary>What a click of the armed tool that was not refused comes to.</summary>
	public enum RunOutcome
	{
		/// <summary>The run was laid and the anchor moves to its end; the tool stays.</summary>
		Moved,

		/// <summary>The run met track, or the click did not move the anchor: the circuit is walked and the tool put away.</summary>
		Closed
	}

	/// <summary>
	/// Whether a click lays its run: one that moves the anchor does, and so does one on the mouth while the anchor is the
	/// mouth, which falls through the click's passes where any other click that does not move the anchor goes straight to
	/// the walk (<c>0x00526d86</c>..<c>0x00526da4</c>).
	/// </summary>
	public static bool LaysRun( (int X, int Y) anchor, (int X, int Y) target, (int X, int Y) mouth ) => anchor != target || mouth == target;

	/// <summary>
	/// What a click that was not refused comes to: where the anchor moves and the run did not meet track, the anchor moves on
	/// (<c>0x0052718c</c>); else the click closes the tool (<c>0x00526eff</c>).
	/// </summary>
	public static RunOutcome OutcomeOf( (int X, int Y) anchor, (int X, int Y) target, bool metTrack )
		=> anchor != target && !metTrack ? RunOutcome.Moved : RunOutcome.Closed;

	/// <summary>
	/// One click of the armed tool. A refused run lays nothing, plays <c>0xaf</c> and puts the tool away, the preview's red
	/// (<c>0x00524a63</c>..<c>0x00524acd</c>). A run that ends on track, or a click that does not move the anchor, closes
	/// the tool: the circuit's walk follows (<see cref="WalkCircuit"/>) into the ride's <c>+0x2c</c>, the ride opens where the
	/// circuit is closed and the open guard lets it, and the tool goes idle.
	/// </summary>
	public static string Run( int clickX, int clickY, bool pressReachedTheTool = true )
	{
		if ( Level.Current is not { ParkState: { } state } level || level.Catalogue is not { } catalogue )
			return "track: a park has to be loaded";

		if ( state.Objects.FirstOrDefault( o => o.ThingId == ParkBuildMode.Serves ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) )
		{
			ParkBuildMode.Disarm();
			return $"track: thing {ParkBuildMode.Serves} is gone - the tool is put away";
		}

		var script = ParkRides.Current?.Scheduler.Scripts.FirstOrDefault( s => s.ThingId == ride.ThingId );

		return RunOn( state, level.Park, script, ride, item.TrackType, ParkTrackPieces.PieceCost( level, item.TrackType ), clickX, clickY,
			pressReachedTheTool );
	}

	/// <summary><see cref="Run"/> on a park and the ride the tool serves, its ride script (or null) and its piece price.</summary>
	internal static string RunOn( ParkState state, IParkInitialState? park, RideScript? script, ParkWorld.CatalogueObject ride,
		int trackType, int price, int clickX, int clickY, bool pressReachedTheTool )
	{
		var type = ParkBuildMode.Current == ParkBuildMode.WaterTrack ? ParkTrackPieces.WaterAnchor : ParkTrackPieces.KartAnchor;
		var owner = (ushort)MapStep.CellId( ride.CellX, ride.CellY );
		var anchor = ParkBuildMode.Anchor;
		var target = TargetOf( anchor, clickX, clickY );
		var mouth = ParkTrackPieces.StationOf( ride.CellX, ride.CellY, ride.Angle ).Mouth;
		var judged = Verdict( state, type, owner, anchor, target, price );

		if ( judged.Refused )
		{
			var sound = ParkBuildMode.RefusedSound( pressReachedTheTool );
			ParkBuildMode.Disarm();
			return $"track: refused, {judged.Why} - {sound}, the tool put away";
		}

		var voice = UI.UiSounds.Interface( ClickSound ) ? "a voice" : "no voice";
		Log.Info( $"track: cat_ui 0x{ClickSound:x} for the click on ({clickX},{clickY}), {voice}" );

		var laid = new Laid( false, 0, 0 );

		if ( LaysRun( anchor, target, mouth ) )
		{
			laid = LayRun( state, type, owner, anchor, target, price );
			ParkBuildMode.Push( target.X, target.Y );
			ParkTrackPieces.Current?.Rebuild();
		}

		var report = $"run ({anchor.X},{anchor.Y}) to ({target.X},{target.Y}), {laid.NewPieces} new piece(s), {laid.Charged} charged, balance {state.Balance}";

		if ( OutcomeOf( anchor, target, laid.MetTrack ) == RunOutcome.Moved )
		{
			ParkBuildMode.AnchorAt( target.X, target.Y );

			// The moving click's pair of walks (0x0052718c, 0x005271a0): the other mouth's open end pulses again.
			ShapeMouths( state, ride.CellX, ride.CellY, ride.Angle, mouthPulses: false, onlyWhereOpen: false );

			return $"track: {report} - the anchor moves to ({target.X},{target.Y})";
		}

		// The circuit's walk into the ride's +0x2c, and with 1 and the open guard the ride opened (0x00526f02..0x00526f2d);
		// then the pieces' clips; then the anchor goes and the tool with it. The original hands the queue tool over instead
		// where [0x008186c0], the queue node the put-down laid (FUN_0052a050), is still set (mode 3 at the entrance's front,
		// 0x00526f84..0x00527004); ours always puts the tool away and counts nothing here, as it is not known whether the
		// original's right click leaves that pointer set (FUN_0052f200 writes it nowhere, its eleven writers are in FUN_00524960).
		var (valid, _) = WalkCircuit( state, ride, check: false );

		Settle( state, park, script, ride.ThingId, trackType, valid );

		// The closing click's pair of walks (0x00526f3c, 0x00526f4e): from the mouth with 1, from the other mouth with 0, so a
		// closed loop pulses nothing and a run that met track short of closing leaves the mouth's open end pulsing.
		ShapeMouths( state, ride.CellX, ride.CellY, ride.Angle, mouthPulses: true, onlyWhereOpen: false );

		// The pieces' clip 0xc from the mouth (TrackRide_PlayPiecesFromMouth, and with no circuit from the open end too).
		Unimplemented.Report( "TRACK_PIECES_CLIP" );

		_lastLinkStep = null;
		ParkBuildMode.Disarm();

		return $"track: {report} - {(laid.MetTrack ? "it met track" : "the anchor did not move")}, +0x2c {valid}, the tool put away";
	}

	/// <summary>
	/// The closing click's tail after the walk (<c>0x00526f0d</c>..<c>0x00526f2d</c>): the walk's answer stored in the ride's
	/// <c>+0x2c</c> (<c>mIsTrackRideValid</c>) and its script, and with 1 and the open guard (<c>FUN_004df290</c>) the ride
	/// opened (<c>FUN_004df390</c>).
	/// </summary>
	internal static void Settle( ParkState state, IParkInitialState? park, RideScript? script, int thingId, int trackType, int valid )
	{
		if ( !state.TryObject( thingId, out var walked ) )
			return;

		state.ReplaceObject( walked with { IsTrackRideValid = valid } );

		if ( script != null )
			script.TrackRideValid = valid;

		if ( valid != 0 && ParkRideOperation.MayOpen( park, walked, trackType ) )
			ParkRideOperation.Open( state, script, thingId );
	}
}
