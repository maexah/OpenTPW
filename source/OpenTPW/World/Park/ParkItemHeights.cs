namespace OpenTPW;

/// <summary>
/// How high whatever is built on a cell stands there: <c>FUN_00452ae0</c> asked with no thing handed in, the
/// height the build squares lift to over a built cell (<see cref="ParkBuildMarkers"/>). Each item's
/// <c>.hmp</c> (<see cref="ItemHeightMapFile"/>) is read once, the first time it is asked for.
/// </summary>
internal static class ParkItemHeights
{
	private static readonly Dictionary<string, ItemHeightMapFile?> _read = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// The item's <c>.hmp</c>, or null where the original would rebuild it from the model instead
	/// (<c>FUN_00451640</c>, <c>FUN_00451880</c>): no file, an unknown signature, or a size other than the
	/// footprint its <c>.sam</c> gives (<c>Info.Shape</c>'s box, or its <c>Engine*Override</c>). Every shipped
	/// file passes; a rebuild is counted, not done.
	/// </summary>
	public static ItemHeightMapFile? For( ParkItemCatalogue.Item item )
		=> Read( item.Directory, $"{item.Directory}/{item.Stem}.hmp", item.Width, item.Depth );

	/// <summary>
	/// A file read once under <paramref name="key"/>: the <c>.hmp</c> at <paramref name="path"/>, or null where it is missing,
	/// unknown, or of another size than <paramref name="columns"/> by <paramref name="rows"/> (counted <c>HMP_REBUILD</c>).
	/// </summary>
	private static ItemHeightMapFile? Read( string key, string path, int columns, int rows )
	{
		if ( _read.TryGetValue( key, out var known ) )
			return known;

		ItemHeightMapFile? file = null;

		// FileExists sees only loose files, never an archive's members, so opening it is the test: an archive
		// answers a missing member with no stream at all.
		try
		{
			using var stream = FileSystem.OpenRead( path );

			if ( stream != null )
			{
				using var bytes = new MemoryStream();
				stream.CopyTo( bytes );
				file = ItemHeightMapFile.Read( bytes.ToArray() );
			}
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException )
		{
			Log.Warning( $"{path}: {e.Message}" );
		}

		if ( file != null && (file.Columns != columns || file.Rows != rows) )
		{
			Log.Warning( $"{path} is {file.Columns}x{file.Rows} where the item is {columns}x{rows}" );
			file = null;
		}

		if ( file == null )
			Unimplemented.Report( "HMP_REBUILD" );

		_read[key] = file;

		return file;
	}

	/// <summary>
	/// <c>FUN_00452ae0( x, y, null )</c>: the height of the thing built on the cell over it - its <c>.hmp</c>'s
	/// byte there / 2.55 plus the height its origin stands at - or, where no placed thing owns the cell, the
	/// ground at the cell's corner (<c>FUN_004527f0</c>).
	/// </summary>
	public static float Over( ParkState state, ParkItemCatalogue? catalogue, HeightfieldFile field, int x, int y )
		=> Built( state, catalogue, field, x, y )?.Top ?? field.HeightAt( x, y );

	/// <summary>
	/// <c>FUN_00452ae0( x, y, null )</c> with the cell's track-layer object asked first (<c>FUN_0053bf30</c>): the record's own,
	/// or for a cell of 0xc or 0x11 its anchor's. A kart or water piece standing there answers its own <c>.hmp</c> over the
	/// cell plus the height its root stands at; the boundary's things (0x19) are not built, so such a cell is counted
	/// (<c>MARKER_LIFT_OVER_TRACK_THING</c>) and answers as <see cref="Over"/> does, which every cell without a track-layer
	/// object answers. The pieces are read from <paramref name="theme"/>'s kits, or the park now loaded's.
	/// </summary>
	public static float AtCell( ParkState state, ParkItemCatalogue? catalogue, HeightfieldFile field, int x, int y,
		string? theme = null )
	{
		var cell = state.Record( x, y );
		var (anchorX, anchorY) = cell.TrackType is ParkTrackPieces.KartCell or ParkTrackPieces.WaterCell && cell.TrackParentId != 0
			? MapStep.CellAt( cell.TrackParentId )
			: (x, y);
		var anchor = state.Record( anchorX, anchorY );

		if ( ParkTrackPieces.PieceAt( anchorX, anchorY, anchor ) is { } piece && (theme ?? ParkTrackPieces.Current?.Theme) is { } kits )
			return PieceTop( piece, kits, field, x, y ) ?? field.HeightAt( x, y );

		if ( anchor.TrackType == ParkTrackPieces.Boundary )
			Unimplemented.Report( "MARKER_LIFT_OVER_TRACK_THING" );

		return Over( state, catalogue, field, x, y );
	}

	/// <summary>
	/// How high a standing track piece's <c>.hmp</c> says it stands over a cell, plus the height its root stands at, or null
	/// where the file is not read (<c>FUN_00452ae0</c> then answers the ground). The pieces' loader asks for the file with
	/// bit <c>0x400</c> of its flags <c>0x10422</c> (<c>0x00462dc9</c>), so each kit piece is given its own.
	/// </summary>
	internal static float? PieceTop( ParkTrackPieces.Piece piece, string theme, HeightfieldFile field, int x, int y )
	{
		var directory = ParkTrackPieces.KitDirectory( theme, piece.Type );

		if ( Read( $"{directory}/{piece.Model}", $"{directory}/{piece.Model}.hmp", 2, 2 ) is not { } file )
			return null;

		var (origin, _) = ParkBoundaryHoarding.StandOf( new ParkBoundaryHoarding.Piece( piece.Model, piece.Row, piece.CellX,
			piece.CellY, piece.Angle ), field );
		var root = origin.GetSystemVector3();

		return AboveBase( file, root, piece.Angle, field, x, y ) is { } above ? above + root.Z : null;
	}

	/// <summary>
	/// <c>FUN_00452ae0</c> where a placed thing with a <c>.hmp</c> owns the cell: the thing, its file, the height its
	/// base stands at, and how high it stands over the cell; null where none does (where <c>FUN_00452ae0</c> answers
	/// nought to the original's object march, which ours asks through <c>ParkPicking.CandidateOn</c>, and the ground to a
	/// caller that is not handed its thing back). Only <see cref="Over"/> calls it, and reads <c>Top</c> alone.
	/// </summary>
	internal static (ParkWorld.CatalogueObject Placed, ItemHeightMapFile File, float Base, float Top)? Built( ParkState state,
		ParkItemCatalogue? catalogue, HeightfieldFile field, int x, int y )
	{
		if ( Owner( state, x, y ) is not { } placed || catalogue == null || !catalogue.TryGet( placed.CatalogueId, out var item )
			|| For( item ) is not { } file )
			return null;

		var (origin, under) = BaseOf( item, placed, field );

		if ( AboveBase( file, origin, placed.Angle, field, x, y ) is not { } above )
			return null;

		return (placed, file, under, above + under);
	}

	/// <summary>
	/// Where a thing's <c>.hmp</c> is laid from and how high its base stands: its model's place and height. The original
	/// reads the model's own matrix (<c>0x00452bc6</c>, its height at <c>0x00452c5f</c>), which stands on <c>mTopLeft</c>
	/// at its footprint's height (<see cref="ParkObjects.ModelOriginFor(ParkItemCatalogue.Item, ParkWorld.CatalogueObject)"/>).
	/// </summary>
	internal static (System.Numerics.Vector3 Origin, float Under) BaseOf( ParkItemCatalogue.Item item,
		ParkWorld.CatalogueObject placed, HeightfieldFile field )
	{
		var origin = ParkObjects.ModelOriginFor( item, placed, field ).GetSystemVector3();

		return (origin, origin.Z);
	}

	/// <summary>
	/// How high a <c>.hmp</c> says its thing stands over a cell, above the thing's base: the cell's middle carried back
	/// into the item's own unturned space from <paramref name="origin"/> (its root's place) by its quarter turn
	/// <paramref name="angle"/>, the original's <c>0x00452bb0</c>..<c>0x00452c1d</c>, which does the same with the cell's
	/// corner for each quarter turn. Null outside the file's cells.
	/// </summary>
	internal static float? AboveBase( ItemHeightMapFile file, System.Numerics.Vector3 origin, int angle, HeightfieldFile field,
		int x, int y )
	{
		var middle = new System.Numerics.Vector3( (x + 0.5f) * field.CellSizeX, (y + 0.5f) * field.CellSizeY, 0f );
		var local = System.Numerics.Vector3.Transform( middle - (origin with { Z = 0f }),
			System.Numerics.Quaternion.Inverse( ParkObjects.Turn( angle ) ) );

		return file.HeightOver( (int)MathF.Floor( local.X / field.CellSizeX ), (int)MathF.Floor( local.Y / field.CellSizeY ) );
	}

	/// <summary>
	/// The thing a cell answers to <c>FUN_00452ae0</c> where its track layer holds no piece (<c>FUN_00527e80</c>,
	/// <c>docs/exe/park.md</c>, "The scene's height under a point"): a cell on the entrance's row, from one before
	/// <paramref name="entranceA"/> to one past <paramref name="entranceB"/>, answers the gate, <paramref name="gate"/>,
	/// whatever it holds; a cell of type 4, 9 or 10 its parent cell's object (<see cref="Owner"/>); any other nothing.
	/// </summary>
	internal static int ThingAnswering( ParkState state, (int X, int Y) entranceA, (int X, int Y) entranceB, int gate,
		int x, int y )
	{
		if ( y == entranceA.Y && x >= entranceA.X - 1 && x <= entranceB.X + 1 )
			return gate;

		return state.Record( x, y ).Type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd
			&& Owner( state, x, y ) is { } placed
			? placed.ThingId
			: 0;
	}

	/// <summary>
	/// The placed thing a cell belongs to: the one anchored there, else the one its <c>mParentID</c> names -
	/// the original's cell object (<c>FUN_0053bf30</c>), then its thing (<c>FUN_00527e80</c>).
	/// </summary>
	internal static ParkWorld.CatalogueObject? Owner( ParkState state, int x, int y )
	{
		var parent = state.Record( x, y ).ParentId;
		var (ownerX, ownerY) = parent != 0 ? MapStep.CellAt( parent ) : (x, y);

		foreach ( var placed in state.Objects )
		{
			if ( placed.CellX == ownerX && placed.CellY == ownerY )
				return placed;
		}

		return null;
	}

	/// <summary>Forgets every file read, for a park left.</summary>
	public static void Clear() => _read.Clear();
}
