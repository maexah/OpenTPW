namespace OpenTPW;

/// <summary>
/// The karts' and the water rides' track tool, modes <c>0xb</c> and <c>0x10</c>: armed by a click on a piece of a ride's
/// track, it lays a straight run of pieces a click (<c>docs/exe/park.md</c>, "The track tool").
///
/// <para>
/// <b>Armed.</b> The idle click on a kart or water anchor (or a kart piece's other cell, which names its anchor) installs
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
	/// kart piece's other cell (0xc), which names its anchor (<c>FUN_004879d0</c>, <c>0x00487f8e</c>). The hover sends only
	/// track types 0xb, 0xc and 0x10 to the track's category (<c>0x00487392</c>), so a water piece's 0x11 cells and the
	/// stations arm nothing.
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

	/// <summary>A run's verdict: refused or not, why, and what its new pieces cost.</summary>
	public readonly record struct Judged( bool Refused, string? Why, int Total );

	/// <summary>
	/// The preview's verdict on a run, cell by cell (<c>Track_CellVerdict</c>, <c>FUN_00539760</c>): refused where a piece's
	/// two by two leaves the map or the park's land (map flag <c>0x40</c>), stands on an object (map types 4, 9 and 10), on
	/// type 7, 0x15 or 30, or on another ride's track; or where the new pieces together cost more than the balance. What the
	/// original allows that is not built here is refused too, and counted: a run over path, queue or water
	/// (<c>TRACK_RUN_OVER_PATH</c>: the original lays it as a hill), through or into its own track other than at its two ends
	/// (<c>TRACK_RUN_CROSSING</c>) and onto its own station (<c>TRACK_RUN_INTO_STATION</c>).
	/// </summary>
	public static Judged Verdict( ParkState state, int type, ushort owner, (int X, int Y) from, (int X, int Y) to, int price )
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

				if ( under.Type is CellEdge.Path or ParkRideChoice.QueueCellType or 2 )
				{
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
				Unimplemented.Report( "TRACK_RUN_INTO_STATION" );
				return new Judged( true, $"({x},{y}) is the station, which a run meets only along its flow (not built)", total );
			}

			if ( cell.TrackType != type )
				return new Judged( true, $"({x},{y}) is track type 0x{cell.TrackType:x}", total );

			// The anchor itself, and an end piece the run closes on, are free; anything else of its own is a crossing or a
			// junction.
			if ( !first && !(last && CellEdge.Links( cell.TrackNeighbours ) == 1) )
			{
				Unimplemented.Report( "TRACK_RUN_CROSSING" );
				return new Judged( true, $"({x},{y}) is its own track, which the run would cross or join (not built)", total );
			}
		}

		return total > state.Balance
			? new Judged( true, $"the run's {total} is more than the balance {state.Balance}", total )
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

			if ( ParkTrackPieces.IsPieceAnchor( cell.TrackType ) )
			{
				var (row, turn) = ParkTrackPieces.PieceFor( cell.TrackType, cell.TrackNeighbours, cell.TrackDirection, cell.TrackFlags );
				cell = cell with
				{
					TrackTileSet = cell.TrackType == ParkTrackPieces.KartAnchor ? ParkTrackPieces.KartFamily : ParkTrackPieces.WaterFamily,
					TrackTileIndex = row,
					TrackTileAngle = turn
				};
			}

			state.SetRecord( x, y, cell );
		}

		return new Laid( metTrack, charged, pieces );
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
	/// the tool: the circuit's walk follows (<c>TRACK_WALK_CIRCUIT</c>, not built) and the tool goes idle.
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

		// The circuit's walk into the ride's +0x2c, the ride opened where it closes, the pieces' clips (0x00526de9..
		// 0x00526e6a); then the anchor goes and, the queue's pointer nought, the tool with it.
		Unimplemented.Report( "TRACK_WALK_CIRCUIT" );
		_lastLinkStep = null;
		ParkBuildMode.Disarm();

		return $"track: {report} - {(laid.MetTrack ? "it met track" : "the anchor did not move")}, the tool put away";
	}
}
