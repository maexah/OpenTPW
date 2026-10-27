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
/// <b>A click</b> (<c>0x00526cb3</c>..<c>0x00526f9a</c>) plays sound <c>0xce</c>, moves the target onto the anchor's row or
/// column, whichever is farther, and lays the run in 2-cell steps by eight passes over it (<c>FUN_0053aac0</c>, each op
/// through <c>Track_CellOp</c>, <c>FUN_005370e0</c>): the clear, the stamp, the links, the flow, the owner, the hill flags,
/// the mouths' walks and the retile. A run that ends on track closes the tool; any other moves the anchor to its end.
/// </para>
/// </summary>
public static class ParkTrackTool
{
	/// <summary>The sound every click of the track tool plays before it lays anything (<c>cat_ui</c> <c>0xce</c>, <c>0x00526cd0</c>).</summary>
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
	/// or null where the tool was installed or a run closed since (both write <c>0x80</c>).
	/// </summary>
	private static (int Dx, int Dy)? _lastLinkStep;

	/// <summary>Forgets the last link step, as installing a tool does.</summary>
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

		// The original walks on until it stops, however far that is; a map's width of steps bounds this one.
		for ( var steps = 0; steps < ParkWorld.MapSize && ParkState.OnMap( x, y ); ++steps )
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

	/// <summary>Two links at right angles (<c>TrackCell_BendLinks</c>, <c>FUN_0053ae00</c>).</summary>
	public static bool IsBend( byte mask ) => CellEdge.Links( mask ) == 2 && (mask & 0x11) != 0x11 && (mask & 0x44) != 0x44;

	/// <summary>
	/// Where a click's run really ends: the target moved onto the anchor's row or column, whichever is farther, ties to the
	/// row (<c>0x00526d04</c>..<c>0x00526d2c</c>), then onto the anchor's 2-cell grid, an odd distance taken to the lower
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

	/// <summary>A run's verdict: refused or not, why, what its new pieces cost, and whether that is the refusal (<c>DAT_00816d5c</c>).</summary>
	public readonly record struct Judged( bool Refused, string? Why, int Total, bool OverBalance = false );

	/// <summary>
	/// The preview's verdict on a run, cell by cell (<c>Track_CellVerdict</c>, <c>FUN_00539760</c>): refused where a piece's
	/// two by two leaves the map or the park's land (map flag <c>0x40</c>), stands on an object (map types 4, 9 and 10), on
	/// type 7, 0x15 or 30, or on another ride's track; where the run's last cell, unless it meets its own track, stands on
	/// queue, a ride's used-from cell or a cell flagged <c>0x20</c> (<c>0x00539fc5</c>..<c>0x0053a05f</c>); or where the new
	/// pieces together cost more than the balance. The river (map type 2) takes a plain piece. What the original allows
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

		for ( var i = 0; i < cells.Count; ++i )
		{
			var (x, y) = cells[i];
			var first = i == 0;
			var last = i == cells.Count - 1;

			foreach ( var (cx, cy) in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) } )
			{
				if ( !ParkState.OnMap( cx, cy ) )
					return new Judged( true, $"({cx},{cy}) is off the map", total );

				var under = state.Record( cx, cy );

				if ( (under.Flags & 0x40) != 0 )
					return new Judged( true, $"({cx},{cy}) is outside the park's land", total );

				if ( under.Type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd or 7 or 0x15 or CellEdge.Approach )
					return new Judged( true, $"({cx},{cy}) is map type {under.Type}", total );

				if ( last && state.Record( x, y ).TrackType != type
					&& (under.Type is ParkRideChoice.QueueCellType or CellEdge.RideEnd || (under.Flags & 0x20) != 0) )
					return new Judged( true, $"({cx},{cy}) is map type {under.Type}, flags 0x{under.Flags:x}, where a run may not end", total );

				if ( under.Type is CellEdge.Path or ParkRideChoice.QueueCellType )
				{
					if ( count )
						Unimplemented.Report( "TRACK_RUN_OVER_PATH" );

					return new Judged( true, $"({cx},{cy}) is map type {under.Type}, which the original crosses with a hill (not built)", total );
				}

				// The three other cells: any ride's track but this one's refuses (0x00539b66..0x00539c8c).
				if ( (cx, cy) != (x, y) && under.TrackType != 0 )
				{
					var parent = under.TrackType is ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell
						? state.Record( MapStep.CellAt( under.TrackParentId ).X, MapStep.CellAt( under.TrackParentId ).Y )
						: under;

					if ( parent.TrackType is not 0 && parent.TrackParentId != owner )
						return new Judged( true, $"({cx},{cy}) is another ride's track", total );
				}
			}

			var cell = state.Record( x, y );

			if ( cell.TrackType == 0 )
			{
				total += price;
				continue;
			}

			if ( cell.TrackParentId != owner )
				return new Judged( true, $"({x},{y}) is another ride's track", total );

			if ( cell.TrackType is ParkTrackPieces.KartStation or ParkTrackPieces.WaterStation )
			{
				if ( count )
					Unimplemented.Report( "TRACK_RUN_INTO_STATION" );

				return new Judged( true, $"({x},{y}) is the station, which a run meets only along its flow (not built)", total );
			}

			if ( cell.TrackType != type )
				return new Judged( true, $"({x},{y}) is track type 0x{cell.TrackType:x}", total );

			// The anchor itself, and an end piece the run closes on, are free; anything else of its own is a crossing or a
			// junction.
			if ( !first && !(last && CellEdge.Links( cell.TrackNeighbours ) == 1) )
			{
				if ( count )
					Unimplemented.Report( "TRACK_RUN_CROSSING" );

				return new Judged( true, $"({x},{y}) is its own track, which the run would cross or join (not built)", total );
			}
		}

		return total > state.Balance
			? new Judged( true, $"the run's {total} is more than the balance {state.Balance}", total, OverBalance: true )
			: new Judged( false, null, total );
	}

	/// <summary>What a run did: whether its last cell was track already (the retile pass answers nought), and what it charged.</summary>
	public readonly record struct Laid( bool MetTrack, int Charged, int NewPieces );

	/// <summary>
	/// Lays one run from the anchor to its target, pass by pass over every cell, as the click does (<c>0x00526d3c</c>..
	/// <c>0x00526dd6</c>): the stamp, the links, the flow, the owner, the hill flags and the retile. The clear pass
	/// (<c>0x87</c>) clears only what <see cref="Verdict"/> refuses to lay over, so it has nothing to do; the mouths' walks
	/// (<c>0x86</c>) are counted, <c>TRACK_OPEN_END_PULSE</c>.
	/// </summary>
	public static Laid LayRun( ParkState state, int type, ushort owner, (int X, int Y) from, (int X, int Y) to, int price )
	{
		var (cells, dx, dy) = CellsOf( from, to );
		var charged = 0;
		var pieces = 0;
		var metTrack = false;

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

		// The hill flags (0x00538a8c): a piece over path or queue would be a hill, which the verdict refuses; over bare
		// ground an end or straight loses its hill flags.
		foreach ( var (x, y) in cells )
		{
			var cell = state.Record( x, y );

			if ( !IsBend( cell.TrackNeighbours ) && CellEdge.Links( cell.TrackNeighbours ) < 3 )
				state.SetRecord( x, y, cell with { TrackFlags = (ushort)(cell.TrackFlags & 0xfff0) } );
		}

		// The mouths' walks (op 0x86, 0x00538ca0): the open end's pulse and a hill's shape by its neighbours.
		Unimplemented.Report( "TRACK_OPEN_END_PULSE" );

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
	/// Unless checking, the ride's sections go first (<see cref="ParkTrackRideTable.ClearSections"/>) and each cell lays
	/// one: a bend 5 (links north and east), 6 (north, west), 7 (south, west) or 8 (east, south); a crossing 11; a straight
	/// 9 east-west or 10 north-south, flagged <c>0x10000</c> on the first two cells, the station's. Checking or not, a bend
	/// whose way on is not the way it leaves takes that way and is retiled (<c>0x0052b1a9</c>..<c>0x0052b1fc</c>).
	/// </summary>
	/// <returns>
	/// What the ride's <c>mIsTrackRideValid</c> (<c>+0x2c</c>) takes, 1 for a closed circuit, else 0; and how many cells
	/// it stepped from, the count each cell's <c>+0x24</c> takes in the original (<c>FUN_0053aef0</c>), not kept here.
	/// </returns>
	public static (int Valid, int Steps) WalkCircuit( ParkState state, ParkWorld.CatalogueObject ride, bool check )
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

				if ( IsBend( mask ) )
				{
					if ( !check )
						state.TrackRides.Lay( handle, mask switch { 0x05 => 5, 0x41 => 6, 0x50 => 7, _ => 8 }, x * 0xc00, y * 0xc00 );
				}
				else if ( links >= 3 )
				{
					if ( !check )
						state.TrackRides.Lay( handle, 11, x * 0xc00, y * 0xc00 );
				}
				else if ( links == 2 )
				{
					// An add-on's cell (flag 0x10) lays 12 or 13 and keeps the add-on's model, whose end caps the walk
					// shows or hides by the track on either side (0x0052ade3..0x0052b18c).
					var addOn = (cell.TrackFlags & ParkTrackPieces.AddOnFlag) != 0;

					if ( addOn )
						Unimplemented.Report( "TRACK_WALK_ADD_ON" );

					var kind = (step < 2 ? 0x10000 : 0) | (mask == 0x11 ? (addOn ? 13 : 10) : (addOn ? 12 : 9));

					if ( !check )
						state.TrackRides.Lay( handle, kind, x * 0xc00, y * 0xc00 );
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

		// A piece's other cell is cleared through its anchor (0x0053b2d2); a run's cells are all anchors on its grid.
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
	/// tidied alone. The pulse the walks that follow start is counted (<c>TRACK_OPEN_END_PULSE</c>).
	/// </summary>
	public static string Backtrack()
	{
		if ( Level.Current is not { ParkState: { } state } level || level.Catalogue is not { } catalogue )
			return "backspace: a park has to be loaded";

		if ( state.Objects.FirstOrDefault( o => o.ThingId == ParkBuildMode.Serves ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) )
			return $"backspace: thing {ParkBuildMode.Serves} is gone";

		var mouth = ParkTrackPieces.StationOf( ride.CellX, ride.CellY, ride.Angle ).Mouth;

		// The red latch is let go first (FUN_00527940( 0 )), and the next run's first cell links along nothing (0x80).
		ParkBuildMode.RefusalLatched = false;
		_lastLinkStep = null;

		if ( !ParkBuildMode.TryPopRun( out var end, out var start ) )
		{
			TidyMouth( state, mouth, backToFirst: false );
			Unimplemented.Report( "TRACK_OPEN_END_PULSE" );
			return "backspace: nothing laid since the track tool was armed";
		}

		var balance = state.Balance;
		var taken = TakeBack( state, ride, end, start, ParkTrackPieces.PieceCost( level, item.TrackType ),
			backToFirst: ParkBuildMode.Pending.Count == 1 );

		ParkBuildMode.AnchorAt( start.X, start.Y );
		ParkTrackPieces.Current?.Rebuild();

		// The pair of walks after it (0x00530025, 0x0053003e): the open end's pulse.
		Unimplemented.Report( "TRACK_OPEN_END_PULSE" );

		var report = $"took back the run from ({start.X},{start.Y}) to ({end.X},{end.Y}), {taken.Removed} piece(s) removed, "
			+ $"{taken.Refunded} refunded, balance {balance} -> {state.Balance} - the anchor is ({start.X},{start.Y})";

		Log.Info( $"track: {report}" );

		return $"backspace: {report}";
	}

	/// <summary>
	/// The armed tool's pointer over a cell (<c>FUN_0052f950</c>, <c>0x0052f9a9</c>): <c>c_track.ani</c> (cursor 6),
	/// <c>c_noplace</c> (8) where the run's preview is red, and <c>c_cash</c> (<c>0x14</c>) where that is its total above
	/// the balance. The verdict's <c>c_end</c> (<c>0x12</c>; its latch <c>DAT_00816d4c</c>, set at <c>0x0053a1c6</c> where an
	/// end of the run meets its own track by rules on its links and way on) is counted where the run's last cell is its own
	/// track, <c>TRACK_TOOL_CURSOR_END</c>; <c>c_link</c> (5) is the path verdict's alone.
	/// </summary>
	public static Input.CursorTypes CursorOver( int x, int y )
	{
		if ( Level.Current is not { ParkState: { } state } level || level.Catalogue is not { } catalogue
			|| state.Objects.FirstOrDefault( o => o.ThingId == ParkBuildMode.Serves ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) || !ParkBuildMode.Anchored )
			return Input.CursorTypes.Track;

		var type = ParkBuildMode.Current == ParkBuildMode.WaterTrack ? ParkTrackPieces.WaterAnchor : ParkTrackPieces.KartAnchor;
		var anchor = ParkBuildMode.Anchor;
		var target = TargetOf( anchor, x, y );

		if ( target != anchor && ParkState.OnMap( target.X, target.Y ) && state.Record( target.X, target.Y ).TrackType == type )
			Unimplemented.Report( "TRACK_TOOL_CURSOR_END" );

		var judged = Verdict( state, type, (ushort)MapStep.CellId( ride.CellX, ride.CellY ), anchor, target,
			ParkTrackPieces.PieceCost( level, item.TrackType ), count: false );

		return !judged.Refused ? Input.CursorTypes.Track : judged.OverBalance ? Input.CursorTypes.Cash : Input.CursorTypes.NoGo;
	}

	/// <summary>
	/// The idle click on a piece: arms the tool for its ride at its track's open end. A ride whose track is closed is
	/// edited by two clicks in the original (mode <c>0xe</c>'s other arm), not built: <c>TRACK_EDIT_CLOSED_LOOP</c>.
	/// </summary>
	public static string Arm( int x, int y )
	{
		if ( Level.Current is not { ParkState: { } state } level || level.Catalogue is not { } catalogue )
			return "track: a park has to be loaded";

		if ( AnchorUnder( state, x, y ) is not { } anchor )
			return $"track: ({x},{y}) is no piece of track";

		var owner = state.Record( anchor.X, anchor.Y ).TrackParentId;

		if ( state.Objects.FirstOrDefault( o => MapStep.CellId( o.CellX, o.CellY ) == owner ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) || ParkTrackPieces.AnchorOf( item.TrackType ) == 0 )
			return $"track: the piece at ({anchor.X},{anchor.Y}) belongs to no track ride";

		var walk = WalkToOpenEnd( state, ride.CellX, ride.CellY, ride.Angle );

		if ( walk.Closed )
		{
			Unimplemented.Report( "TRACK_EDIT_CLOSED_LOOP" );
			return $"track: thing {ride.ThingId}'s track is closed, and editing a closed loop is not built";
		}

		var mode = item.TrackType == 2 ? ParkBuildMode.WaterTrack : ParkBuildMode.KartTrack;
		var armed = ParkBuildMode.ArmAt( mode, ride.ThingId, walk.End.X, walk.End.Y );

		ParkBuildMode.ClearPending();

		foreach ( var (px, py) in walk.Pending )
			ParkBuildMode.Push( px, py );

		_lastLinkStep = null;

		// The arming's pair of walks (0x00526c89): the other mouth pulses while the loop is open.
		Unimplemented.Report( "TRACK_OPEN_END_PULSE" );

		return $"{armed} (the open end, {walk.Pending.Count} pending)";
	}

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

		var mode = ParkBuildMode.Current;
		var type = mode == ParkBuildMode.WaterTrack ? ParkTrackPieces.WaterAnchor : ParkTrackPieces.KartAnchor;

		if ( state.Objects.FirstOrDefault( o => o.ThingId == ParkBuildMode.Serves ) is not { ThingId: not 0 } ride
			|| !catalogue.TryGet( ride.CatalogueId, out var item ) )
		{
			ParkBuildMode.Disarm();
			return $"track: thing {ParkBuildMode.Serves} is gone - the tool is put away";
		}

		var owner = (ushort)MapStep.CellId( ride.CellX, ride.CellY );
		var price = ParkTrackPieces.PieceCost( level, item.TrackType );
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

		if ( anchor != target || mouth == target )
		{
			laid = LayRun( state, type, owner, anchor, target, price );
			ParkBuildMode.Push( target.X, target.Y );
			ParkTrackPieces.Current?.Rebuild();
		}

		var report = $"run ({anchor.X},{anchor.Y}) to ({target.X},{target.Y}), {laid.NewPieces} new piece(s), {laid.Charged} charged, balance {state.Balance}";

		if ( anchor != target && !laid.MetTrack )
		{
			ParkBuildMode.AnchorAt( target.X, target.Y );
			return $"track: {report} - the anchor moves to ({target.X},{target.Y})";
		}

		// The circuit's walk into the ride's +0x2c, and with 1 and the open guard the ride opened (0x00526f02..0x00526f2d);
		// then the pieces' clips; then the anchor goes and, the queue's pointer nought, the tool with it.
		var (valid, _) = WalkCircuit( state, ride, check: false );
		var script = ParkRides.Current?.Scheduler.Scripts.FirstOrDefault( s => s.ThingId == ride.ThingId );

		if ( state.TryObject( ride.ThingId, out var walked ) )
		{
			state.ReplaceObject( walked with { IsTrackRideValid = valid } );

			if ( script != null )
				script.TrackRideValid = valid;

			if ( valid != 0 && ParkRideOperation.MayOpen( level.Park, walked, item.TrackType ) )
				ParkRideOperation.Open( state, script, ride.ThingId );
		}

		// The pieces' clip 0xc from the mouth (TrackRide_PlayPiecesFromMouth, and with no circuit from the open end too).
		Unimplemented.Report( "TRACK_PIECES_CLIP" );

		_lastLinkStep = null;
		ParkBuildMode.Disarm();

		return $"track: {report} - {(laid.MetTrack ? "it met track" : "the anchor did not move")}, +0x2c {valid}, the tool put away";
	}
}
