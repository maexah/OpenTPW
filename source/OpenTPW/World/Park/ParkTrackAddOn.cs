namespace OpenTPW;

/// <summary>
/// A kart or water ride's track add-on put down (<c>AddOn.UpgradeType</c> 1: Lost Kingdom's Dino Karts Jump and Tunnel and
/// the Splish Splash Tunnel), as the place mode does it with the add-on flag <c>[0x008187f8]</c> set: the preview snaps the
/// pointer to the ride's own two-cell grid and turns it by the track's way on (<c>0x005236b0</c>), each of its <c>+</c> cells
/// must lie on a straight, an end or the station of that ride (<c>BuildTool_CellVerdict</c>'s add-on arm, <c>0x0053594e</c>),
/// and the commit stamps the picture by kind over the cells the model covers, its <c>+</c> cells flagged <c>0x10</c> in both
/// records (<c>0x0052950d</c>). docs/exe/park-engine.md, "The add-on's snap"; docs/exe/park.md, "The walk".
/// </summary>
public static class ParkTrackAddOn
{
	/// <summary>The shape kind of an add-on's track cells, <c>+</c> (the kart kind; <c>#</c>, 0x10, the water's, is no shipped add-on's).</summary>
	private const int KartKind = 0x0b;

	/// <summary>The shape kind <c>#</c>, the water track's, which the placer treats as <see cref="KartKind"/>.</summary>
	private const int WaterKind = 0x10;

	/// <summary>Whether a carried or placed item is a track add-on (<see cref="ParkObjects.IsTrackAddOn"/>).</summary>
	public static bool Is( ParkItemCatalogue.Item item ) => ParkObjects.IsTrackAddOn( item );

	/// <summary>
	/// The preview's cell for an add-on with the pointer on (x, y), and its angle: the snap of <c>BuildTool_PreviewPass</c>
	/// (<c>0x005236b0</c>..<c>0x005238d7</c>). The cell goes onto the ride's grid, its cell plus twice the floor of half the
	/// distance on each axis; where that cell, or the anchor a piece's other cell names, is track, the angle is its way on's
	/// (1 0, 4 270, 0x10 180, 0x40 90; any other kept); the item's <c>Bumper.*Adjust</c> of the angle is added (North at 0,
	/// West at 90, South at 180, East at 270, the snap's own table); and the ride's turn moves it (90 one west, 180 one west and
	/// one south, 270 one south). A pointer off the map, or a snapped cell off it, is taken as it is.
	/// </summary>
	internal static (int X, int Y) Snap( ParkState state, ParkItemCatalogue.Item item, ParkWorld.CatalogueObject ride,
		int x, int y, ref int angle )
	{
		if ( !ParkState.OnMap( x, y ) )
			return (x, y);

		var snapped = (X: ride.CellX + Grid( x - ride.CellX ), Y: ride.CellY + Grid( y - ride.CellY ));

		if ( !ParkState.OnMap( snapped.X, snapped.Y ) )
			return snapped;

		var head = Head( state, snapped.X, snapped.Y );

		if ( IsTrack( head.TrackType ) )
		{
			angle = head.TrackDirection switch
			{
				0x01 => 0,
				0x04 => 270,
				0x10 => 180,
				0x40 => 90,
				_ => angle
			};
		}

		var (adjustX, adjustY) = item.BumperAdjustAt( angle switch { 90 => 270, 270 => 90, _ => angle } );
		var (cellX, cellY) = (snapped.X + adjustX, snapped.Y + adjustY);

		return (((ride.Angle % 360) + 360) % 360) switch
		{
			90 => (cellX - 1, cellY),
			180 => (cellX - 1, cellY + 1),
			270 => (cellX, cellY + 1),
			_ => (cellX, cellY)
		};
	}

	/// <summary>
	/// The preview's cell and the anchor an add-on takes with the pointer on (x, y), turning <paramref name="angle"/> as the
	/// snap does: <see cref="Snap"/> for the ride named (none: the pointer's own cell), then the placer's centring of the
	/// picture on that cell (<see cref="ParkBuilding.AnchorUnder"/>) and the thing's own cell from the picture's
	/// (<see cref="AnchorFor"/>).
	/// </summary>
	internal static ((int X, int Y) Preview, (int X, int Y) Anchor) Under( ParkState state, ParkItemCatalogue.Item item,
		ParkWorld.CatalogueObject? ride, int x, int y, ref int angle )
	{
		(int X, int Y) preview = ride is { } named ? Snap( state, item, named, x, y, ref angle ) : (x, y);
		var picture = ParkBuilding.AnchorUnder( item, preview.X, preview.Y, angle );

		return (preview, AnchorFor( item, picture.X, picture.Y, angle ));
	}

	/// <summary>Twice the floor of half a distance, the snap's <c>dec</c> of a negative odd one then <c>sar 1</c> (<c>0x005236fb</c>).</summary>
	private static int Grid( int distance ) => 2 * (int)Math.Floor( distance / 2.0 );

	/// <summary>A cell's track record, or for a piece's other cell (0xc, 0x11) the anchor's it names (<c>TrackCell_IsPieceCell</c>).</summary>
	internal static ParkWorld.MapCell Head( ParkState state, int x, int y )
	{
		var cell = state.Record( x, y );

		if ( cell.TrackType is not (ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell) )
			return cell;

		var (headX, headY) = MapStep.CellAt( cell.TrackParentId );

		return ParkState.OnMap( headX, headY ) ? state.Record( headX, headY ) : cell;
	}

	/// <summary>Track to the snap and the verdict: a kart or water anchor or station, or a boundary piece (<c>TrackCell_IsTrack</c>).</summary>
	private static bool IsTrack( int type )
		=> type is ParkTrackPieces.KartAnchor or ParkTrackPieces.KartStation or ParkTrackPieces.WaterAnchor or ParkTrackPieces.WaterStation
			or ParkTrackPieces.Boundary;

	/// <summary>
	/// The ride a click with the preview on (x, y) puts the add-on on: the owner of the track there (the anchor a piece's other
	/// cell names), which the put-down makes the builder's object (<c>0x00524b9b</c>..<c>0x00524c3d</c>); none where the cell is
	/// no track, and then the put-down builds nothing and lets the add-on go (<c>0x00524bbe</c>).
	/// </summary>
	internal static ParkWorld.CatalogueObject? RideUnder( ParkState state, int x, int y )
	{
		if ( !ParkState.OnMap( x, y ) )
			return null;

		var cell = state.Record( x, y );

		if ( !IsTrack( cell.TrackType ) && cell.TrackType is not (ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell) )
			return null;

		var (ownerX, ownerY) = MapStep.CellAt( Head( state, x, y ).TrackParentId );

		return state.Objects.FirstOrDefault( o => o.IsPlaced && o.CellX == ownerX && o.CellY == ownerY ) is { ThingId: not 0 } ride
			? ride
			: null;
	}

	/// <summary>
	/// The anchor (the thing's own cell) of an add-on whose picture starts at a cell: that cell moved by the item's map offset
	/// turned by the angle, the inverse of <see cref="ParkBuilding.TopLeftFor"/>, as the placer constructs the object there
	/// (<c>FUN_004d9cc0</c> at <c>0x00529f9d</c>). Alexah's played Tunnel: picture (16,21) at 90, anchor (17,21).
	/// </summary>
	internal static (int X, int Y) AnchorFor( ParkItemCatalogue.Item item, int pictureX, int pictureY, int angle )
	{
		var (dx, dy) = ParkBuilding.RotateDelta( item.MapOffsetX, item.MapOffsetY, angle );

		return (pictureX + dx, pictureY + dy);
	}

	/// <summary>
	/// The picture's cells of an add-on anchored at a cell, each with its kind: the placer's sweep from the cell its model stands
	/// on (<see cref="ParkObjects.ModelCell"/>, the anchor less the turned offset), every column and row turned by
	/// <see cref="ParkBuilding.RotateDelta"/>, the empty kind <c>.</c> left out as the placer passes it by (<c>0x0052916c</c>'s case 0).
	/// </summary>
	internal static IEnumerable<(int X, int Y, int Kind)> Cells( ParkItemCatalogue.Item item, int anchorX, int anchorY, int angle )
	{
		if ( item.Shape is not { } shape )
			yield break;

		var (originX, originY) = ParkObjects.ModelCell( item, anchorX, anchorY, angle );

		for ( var row = 0; row < shape.Depth; ++row )
		{
			for ( var column = 0; column < shape.Width; ++column )
			{
				var kind = shape.KindAt( column, row );

				if ( kind == 0 )
					continue;

				var (dx, dy) = ParkBuilding.RotateDelta( column, row, angle );

				yield return (originX + dx, originY + dy, kind);
			}
		}
	}

	/// <summary>Whether a kind is one of the track kinds an add-on's put-down flags (<c>+</c> 0xb, <c>#</c> 0x10).</summary>
	internal static bool IsTrackKind( int kind ) => kind is KartKind or WaterKind;

	/// <summary>
	/// Why an add-on will not go down anchored at a cell, turned that far, for a ride (none named: every track cell refuses), or
	/// null where it will: the placer's test pass, cell by cell. A track cell asks <see cref="TrackCellRefusal"/> and is then
	/// refused where it is a thing's cell (map type 4, <c>FUN_00536380</c> after op <c>0x20b</c>, <c>0x005294e2</c>); any other
	/// kind is judged as any thing's body cell is (<paramref name="bodyRefusal"/>), but a body cell over track passes past the
	/// verdict's first answers (<see cref="Early"/>; <c>0x00535bc3</c>: op 4 over track answers blue only for an add-on).
	/// </summary>
	internal static string? Refusal( ParkState state, ParkItemCatalogue.Item item, ParkWorld.CatalogueObject? ride,
		int anchorX, int anchorY, int angle, Func<int, int, string?> bodyRefusal )
	{
		foreach ( var (x, y, kind) in Cells( item, anchorX, anchorY, angle ) )
		{
			if ( !ParkState.OnMap( x, y ) )
				return $"({x},{y}) is off the map";

			if ( IsTrackKind( kind ) )
			{
				if ( TrackCellRefusal( state, ride, item.OverwritePriority, x, y ) is { } why )
					return $"({x},{y}) {why}";

				if ( state.Record( x, y ).Type == CellEdge.Footprint )
					return $"({x},{y}) is a thing's cell";

				continue;
			}

			if ( IsTrack( Head( state, x, y ).TrackType ) )
			{
				if ( Early( state, item.OverwritePriority, x, y ).Why is { } early )
					return $"({x},{y}) {early}";

				continue;
			}

			if ( bodyRefusal( x, y ) is { } refused )
				return refused;
		}

		return null;
	}

	/// <summary>
	/// The verdict on one of an add-on's track cells, null where it passes (<c>BuildTool_CellVerdict</c> with op 0xb or 0x10 and
	/// the add-on flag, <c>0x00535670</c>), in its order: its first answers (<see cref="Early"/>); then the cell passes only where it carries no add-on already (map flag 0x10), its anchor is track of no bend and no crossing,
	/// that track is the ride's (its owner the ride's cell) and the cell is no queue (<c>0x0053594e</c>..<c>0x005359b3</c>).
	/// The money is the hand's to refuse (<see cref="ParkBuilding.PlacementRefusal"/>).
	/// </summary>
	internal static string? TrackCellRefusal( ParkState state, ParkWorld.CatalogueObject? ride, int priority, int x, int y )
	{
		if ( Early( state, priority, x, y ) is (var why, true) )
			return why;

		var cell = state.Record( x, y );
		var head = Head( state, x, y );

		if ( (cell.Flags & ParkTrackPieces.AddOnFlag) != 0 )
			return "has an add-on already";

		if ( !IsTrack( head.TrackType ) )
			return "is not track";

		if ( ParkPathBuilding.TrackCornerOrJunction( head ) )
			return "is a bend or a crossing of the track";

		if ( ride is not { } owner || head.TrackParentId != MapStep.CellId( owner.CellX, owner.CellY ) )
			return ride is null ? "has no ride named for the add-on" : "is another ride's track";

		if ( cell.Type == ParkRideChoice.QueueCellType )
			return "is a queue";

		return null;
	}

	/// <summary>
	/// The verdict's first answers, the same for an add-on's every cell (<c>0x005357c7</c>..<c>0x005358c9</c>): land outside the
	/// park refuses; a thing's cell (map type 4, 9 or 10) passes where the thing's overwrite priority is below the add-on's and
	/// refuses otherwise; a boundary piece refuses. <c>Decided</c> where one of them answered.
	/// </summary>
	private static (string? Why, bool Decided) Early( ParkState state, int priority, int x, int y )
	{
		var cell = state.Record( x, y );

		if ( (cell.Flags & ParkPathBuilding.OutsideThePark) != 0 )
			return ("is outside the park", true);

		if ( cell.Type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd )
			return (ParkTrackTool.OverwritePriorityOf( state, cell.ParentId ) is { } other && other < priority
				? null
				: "is another thing's, which the add-on may not build over", true);

		return Head( state, x, y ).TrackType == ParkTrackPieces.Boundary ? ("is the boundary's", true) : (null, false);
	}

	/// <summary>
	/// The commit's stamp of an add-on anchored at a cell (<c>BuildTool_Placer</c> with the commit flag, <c>0x005290bf</c>..
	/// <c>0x0052914c</c>, <c>0x0052950d</c>): each cell of its picture but <c>.</c> becomes a thing's cell owned by the anchor,
	/// the piece under it goes (the track record's model freed) and its track flags lose their low four bits; a track cell's
	/// map flags and track flags are then both written 0x10. The track's type, links, way on and owner stay, as Alexah's played
	/// file holds them under its Jump and Tunnel. Answers how many track cells were flagged.
	/// </summary>
	internal static int Stamp( ParkState state, ParkItemCatalogue.Item item, int anchorX, int anchorY, int angle )
	{
		var flagged = 0;
		var owner = (ushort)MapStep.CellId( anchorX, anchorY );

		foreach ( var (x, y, kind) in Cells( item, anchorX, anchorY, angle ) )
		{
			if ( !ParkState.OnMap( x, y ) )
				continue;

			var cell = state.Record( x, y );

			cell = cell with { Type = CellEdge.Footprint, ParentId = owner, TrackFlags = (ushort)(cell.TrackFlags & ~0xf) };

			if ( IsTrackKind( kind ) )
			{
				cell = cell with { Flags = ParkTrackPieces.AddOnFlag, TrackFlags = ParkTrackPieces.AddOnFlag };
				++flagged;
			}

			state.SetRecord( x, y, cell );
		}

		return flagged;
	}
}
