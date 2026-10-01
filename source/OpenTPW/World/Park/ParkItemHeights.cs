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
	{
		if ( _read.TryGetValue( item.Directory, out var known ) )
			return known;

		var path = $"{item.Directory}/{item.Stem}.hmp";
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

		if ( file != null && (file.Columns != item.Width || file.Rows != item.Depth) )
		{
			Log.Warning( $"{path} is {file.Columns}x{file.Rows} where the item is {item.Width}x{item.Depth}" );
			file = null;
		}

		if ( file == null )
			Unimplemented.Report( "HMP_REBUILD" );

		_read[item.Directory] = file;

		return file;
	}

	/// <summary>
	/// <c>FUN_00452ae0( x, y, null )</c>: the height of the thing built on the cell over it - its <c>.hmp</c>'s
	/// byte there / 2.55 plus the height its origin stands at - or, where no placed thing owns the cell, the
	/// ground at the cell's corner (<c>FUN_004527f0</c>).
	/// </summary>
	public static float Over( ParkState state, ParkItemCatalogue? catalogue, HeightfieldFile field, int x, int y )
	{
		if ( Owner( state, x, y ) is { } placed && catalogue != null && catalogue.TryGet( placed.CatalogueId, out var item )
			&& For( item ) is { } file )
		{
			// The cell's middle carried back into the item's own unturned space, the original's
			// 0x00452bb0..0x00452c1d, which does the same with the cell's corner for each quarter turn.
			// The thing's base is the ground under its anchor, where OriginFor stands it.
			var origin = ParkObjects.OriginFor( placed.CellX, placed.CellY, placed.Angle ).GetSystemVector3() with { Z = 0f };
			var middle = new System.Numerics.Vector3( (x + 0.5f) * field.CellSizeX, (y + 0.5f) * field.CellSizeY, 0f );
			var local = System.Numerics.Vector3.Transform( middle - origin,
				System.Numerics.Quaternion.Inverse( ParkObjects.Turn( placed.Angle ) ) );

			if ( file.HeightOver( (int)MathF.Floor( local.X / field.CellSizeX ), (int)MathF.Floor( local.Y / field.CellSizeY ) ) is { } above )
				return above + field.HeightAt( placed.CellX, placed.CellY );
		}

		return field.HeightAt( x, y );
	}

	/// <summary>
	/// The placed thing a cell belongs to: the one anchored there, else the one its <c>mParentID</c> names -
	/// the original's cell object (<c>FUN_0053bf30</c>), then its thing (<c>FUN_00527e80</c>).
	/// </summary>
	private static ParkWorld.CatalogueObject? Owner( ParkState state, int x, int y )
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
