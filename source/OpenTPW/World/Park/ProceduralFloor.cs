namespace OpenTPW;

/// <summary>
/// A built thing's floor blended into the ground's tiles: the footprint table <c>ProceduralBase_SkirtFlags</c>
/// (<c>0x0046aa10</c>) builds as the model loads, the turn the model maker <c>FUN_00463060</c> stamps it into the
/// heightfield with, and the cells <c>ProceduralBase_BlendFloor</c> (<c>0x0057ace0</c>) blends. docs/exe/park-engine.md, "A built thing's floor is blended
/// into the ground's tiles".
/// </summary>
/// <remarks>
/// Each used cell of the table names the frame of the floor face over it and how the face's texture lies on the cell
/// (the turn and mirror the compositor reads the ground tile by); the blend makes that frame's texture the cell's ground
/// tile with the frame's own texture laid over it by its alpha (<see cref="ProceduralGround.Tile"/>'s overlay), so the
/// floor draws at the ground's own tile size. A frame two cells share draws the last cell's tile, as the original's frame
/// table entry is written once a cell.
/// </remarks>
public static class ProceduralFloor
{
	/// <summary>One used cell of a model's footprint table: <c>0x80</c>, a turn (<c>0x08</c>, <c>0x10</c>, <c>0x20</c>) and the mirror <c>0x40</c>, and the floor face's frame.</summary>
	public readonly record struct Cell( ushort Flags, uint FrameOffset );

	/// <summary>The cell kinds <c>Footprint_EdgeMask</c> counts as floor: 4, 9, 10 and 0x17.</summary>
	private static bool IsFloorKind( int kind ) => kind is 4 or 9 or 10 or 0x17;

	/// <summary>An edge's share of one ground axis both a face's edges must pass to place it on a cell (<c>0x006fe870</c>).</summary>
	private const float Square = 0.9f;

	/// <summary>
	/// <c>Footprint_EdgeMask</c> (<c>0x0052c420</c>): the footprint's floor cells (<see cref="IsFloorKind"/>) not ringed on all
	/// four sides by floor cells, by column and row; a neighbour past the picture is none.
	/// </summary>
	internal static bool[,] EdgeMask( ItemShape shape )
	{
		var mask = new bool[shape.Width, shape.Depth];

		for ( var row = 0; row < shape.Depth; ++row )
		{
			for ( var column = 0; column < shape.Width; ++column )
			{
				if ( !IsFloorKind( shape.KindAt( column, row ) ) )
					continue;

				var ringed = (IsFloorKind( shape.KindAt( column, row - 1 ) ) ? 1 : 0) + (IsFloorKind( shape.KindAt( column - 1, row ) ) ? 1 : 0)
					+ (IsFloorKind( shape.KindAt( column + 1, row ) ) ? 1 : 0) + (IsFloorKind( shape.KindAt( column, row + 1 ) ) ? 1 : 0);

				mask[column, row] = ringed != 4;
			}
		}

		return mask;
	}

	/// <summary>The face <c>ProceduralBase_SkirtFlags</c> keeps for a cell: its first corner pair by each axis, and those edges' directions.</summary>
	private record struct Best( float AlongX, float AlongZ, int Face, int EdgeX, int EdgeZ, float EdgeXDirection, float EdgeZDirection );

	/// <summary>
	/// <c>ProceduralBase_SkirtFlags</c>' footprint table, <paramref name="width"/> by <paramref name="depth"/> by column and
	/// row, null where a cell has no entry (the original's <c>{ 1, 10000 }</c>). A cell is entered where
	/// <paramref name="everyCell"/> (the header's <c>0x80</c>), or where it is in the edge mask (<see cref="EdgeMask"/>), and the
	/// floor mesh has a face over it: of the faces whose middle (the three corners' mean in
	/// model space, truncated to cells of the grid) lies on the cell, the first with an edge more than 0.9 along x and one
	/// more than 0.9 along z, a later one only where it lies more squarely on either axis.
	/// </summary>
	/// <remarks>
	/// The entry's turn is the cell corner nearest the least u and the least v of the face's three corners and the fourth
	/// that completes them: corner 0 at the cell's low x and low z, 1 low x high z, 2 high x high z, 3 high x low z give
	/// nought, <c>0x08</c>, <c>0x10</c>, <c>0x20</c>; the mirror <c>0x40</c> is set where u changes more than v from that corner to
	/// the next. The original first moves each face's UVs into 0..1 (<c>FUN_004679d0</c>), which moves a face's corners alike
	/// and so leaves the answer as it is.
	/// </remarks>
	public static Cell?[,] Table( ModelFile model, int mesh, ItemShape? shape, int width, int depth, bool everyCell, ProceduralBase.Grid grid )
	{
		var table = new Cell?[width, depth];

		if ( mesh < 0 || mesh >= model.Meshes.Count || width <= 0 || depth <= 0 )
			return table;

		var floor = model.Meshes[mesh];
		var best = new Best?[width, depth];

		System.Numerics.Vector3 Corner( int face, int k )
		{
			var drawn = (int)floor.Indices[(face * 3) + k];
			return System.Numerics.Vector3.Transform( floor.SourceVertices[floor.VertexOrder[drawn]].GetSystemVector3(), floor.WorldTransform );
		}

		foreach ( var material in floor.Materials )
		{
			for ( var face = material.a; face < material.a + material.b && (face * 3) + 2 < floor.Indices.Length; ++face )
			{
				var p = new[] { Corner( face, 0 ), Corner( face, 1 ), Corner( face, 2 ) };

				// The corners' mean in the x87's precision, by the float third (0x006fe86c), cut to whole cells (__ftol).
				var x = (int)(((double)p[0].X + p[1].X + p[2].X) * 0.3333333432674408 / grid.CellX);
				var z = (int)(((double)p[0].Z + p[1].Z + p[2].Z) * 0.3333333432674408 / grid.CellZ);

				float alongX = 0f, alongZ = 0f, directionX = 0f, directionZ = 0f;
				int edgeX = -1, edgeZ = -1;

				for ( var k = 0; k < 3; ++k )
				{
					var d = p[(k + 1) % 3] - p[k];
					var length = MathF.Sqrt( (d.Z * d.Z) + (d.Y * d.Y) + (d.X * d.X) );
					var ax = MathF.Abs( d.X / length );
					var az = MathF.Abs( d.Z / length );

					if ( alongX < ax )
					{
						alongX = ax;
						edgeX = k;
						directionX = d.X / length;
					}

					if ( alongZ < az )
					{
						alongZ = az;
						edgeZ = k;
						directionZ = d.Z / length;
					}
				}

				if ( x < 0 || z < 0 || x >= width || z >= depth || !Keeps( alongX, alongZ, best[x, z]?.AlongX ?? 0f, best[x, z]?.AlongZ ?? 0f ) )
					continue;

				best[x, z] = new Best( alongX, alongZ, face, edgeX, edgeZ, directionX, directionZ );
			}
		}

		var mask = shape is null ? null : EdgeMask( shape );

		for ( var z = 0; z < depth; ++z )
		{
			for ( var x = 0; x < width; ++x )
			{
				// The original also asks the cell's kind be other than nought, which the edge mask already holds to.
				if ( !everyCell && !(mask != null && x < mask.GetLength( 0 ) && z < mask.GetLength( 1 ) && mask[x, z]) )
					continue;

				if ( best[x, z] is not { } face )
					continue;

				table[x, z] = new Cell( Flags( floor, face ), FrameOf( floor, face.Face ) );
			}
		}

		return table;
	}

	/// <summary>
	/// <c>ProceduralBase_BlendFloor</c> over a model's footprint: each table cell stamped at the thing's <paramref name="angle"/> on its
	/// cell of <paramref name="covered"/>, and its frame's texture (read from <paramref name="textureFor"/>'s path for the
	/// frame's name) blended into that cell's ground tile (<see cref="ParkGround.BlendFloor"/>). The textures by frame, the
	/// later cell's where two cells share one; every texture made, superseded ones too, goes in <paramref name="made"/>, the
	/// caller's to let go of with the model. <paramref name="cells"/> counts the cells blended.
	/// </summary>
	public static Dictionary<uint, Texture> Blend( ModelFile model, int mesh, ItemShape? shape, int width, int depth, bool everyCell,
		int angle, (int Left, int Top, int Right, int Bottom) covered, ParkGround ground, Func<string, string> textureFor,
		List<Texture> made, out int cells )
	{
		var textures = new Dictionary<uint, Texture>();
		var table = Table( model, mesh, shape, width, depth, everyCell, ProceduralBase.ParkGrid );
		var names = model.Meshes[mesh].Materials.Where( m => m.FrameOffset != 0 ).GroupBy( m => m.FrameOffset )
			.ToDictionary( g => g.Key, g => g.First().Name.TrimEnd( '\0' ) );

		cells = 0;

		for ( var row = 0; row < depth; ++row )
		{
			for ( var column = 0; column < width; ++column )
			{
				if ( table[column, row] is not { } cell || !names.TryGetValue( cell.FrameOffset, out var name ) )
					continue;

				var (x, y) = CellOf( column, row, angle, covered );

				if ( ground.BlendFloor( x, y, Stamped( cell.Flags, angle, 0 ), textureFor( name ) ) is not { } blended )
					continue;

				made.Add( blended );
				textures[cell.FrameOffset] = blended;
				++cells;
			}
		}

		return textures;
	}

	/// <summary>
	/// Whether a face replaces the one kept for its cell (<paramref name="heldX"/>, <paramref name="heldZ"/>; nought for none):
	/// both its edges more than 0.9 along their axes, and either more so than the kept face's.
	/// </summary>
	internal static bool Keeps( float alongX, float alongZ, float heldX, float heldZ )
		=> Square < alongX && Square < alongZ && (heldX < alongX || heldZ < alongZ);

	/// <summary>The frame of the material whose run holds a face, nought where none does.</summary>
	private static uint FrameOf( ModelFile.Mesh mesh, int face )
	{
		foreach ( var material in mesh.Materials )
		{
			if ( face >= material.a && face < material.a + material.b )
				return material.FrameOffset;
		}

		return 0;
	}

	/// <summary>A kept face's entry flags: <c>0x80</c>, its turn and its mirror (<see cref="Table"/>'s remarks).</summary>
	private static ushort Flags( ModelFile.Mesh mesh, Best face )
	{
		// Each edge's low and high end along its axis: the edge runs from its corner k to k + 1.
		var (lowX, highX) = face.EdgeXDirection <= 0f ? ((face.EdgeX + 1) % 3, face.EdgeX) : (face.EdgeX, (face.EdgeX + 1) % 3);
		var (lowZ, highZ) = face.EdgeZDirection <= 0f ? ((face.EdgeZ + 1) % 3, face.EdgeZ) : (face.EdgeZ, (face.EdgeZ + 1) % 3);

		// The face's corner on each of the cell's four corners, the one it lacks -1.
		var corners = new int[4];
		int missing;

		if ( lowX == lowZ )
			(missing, corners[0], corners[1], corners[2], corners[3]) = (2, lowX, highZ, -1, highX);
		else if ( highZ == lowX )
			(missing, corners[0], corners[1], corners[2], corners[3]) = (3, lowZ, highZ, highX, -1);
		else if ( highX != highZ )
			(missing, corners[0], corners[1], corners[2], corners[3]) = (1, lowX, -1, highZ, highX);
		else
			(missing, corners[0], corners[1], corners[2], corners[3]) = (0, -1, lowX, highX, lowZ);

		var uv = new System.Numerics.Vector2[4];

		for ( var c = 0; c < 4; ++c )
		{
			if ( c == missing )
				continue;

			var t = mesh.TexCoords[(int)mesh.Indices[(face.Face * 3) + corners[c]]];
			uv[c] = new System.Numerics.Vector2( t.X, t.Y );
		}

		// The fourth corner completes the other three, by the four cases of the function's switch.
		uv[missing] = missing switch
		{
			0 => uv[3] - uv[2] + uv[1],
			1 => uv[2] - uv[3] + uv[0],
			2 => uv[3] - uv[0] + uv[1],
			_ => uv[2] - uv[1] + uv[0],
		};

		var least = new System.Numerics.Vector2( MathF.Min( MathF.Min( uv[0].X, uv[1].X ), MathF.Min( uv[2].X, uv[3].X ) ),
			MathF.Min( MathF.Min( uv[0].Y, uv[1].Y ), MathF.Min( uv[2].Y, uv[3].Y ) ) );

		var nearest = 0;
		var distance = float.MaxValue;

		for ( var c = 0; c < 4; ++c )
		{
			var d = ((uv[c].Y - least.Y) * (uv[c].Y - least.Y)) + ((uv[c].X - least.X) * (uv[c].X - least.X));

			if ( d < distance )
			{
				nearest = c;
				distance = d;
			}
		}

		var flags = (ushort)(0x80 | nearest switch { 1 => 0x08, 2 => 0x10, 3 => 0x20, _ => 0 });
		var next = (nearest + 1) & 3;

		if ( MathF.Abs( uv[nearest].X - uv[next].X ) > MathF.Abs( uv[nearest].Y - uv[next].Y ) )
			flags |= 0x40;

		return flags;
	}

	/// <summary>
	/// The turn and mirror <c>FUN_00463060</c> stamps a table cell into the heightfield with at the thing's angle: the mirror
	/// kept, the turn carried round by the angle (90: nought to <c>0x08</c> to <c>0x10</c> to <c>0x20</c> to nought), the rest of
	/// the word <c>0x281</c> and the cell's own <c>0x6</c>.
	/// </summary>
	public static ushort Stamped( ushort flags, int angle, ushort word )
	{
		var mirror = flags & 0x40;
		var turn = flags & 0x38;

		var turned = angle switch
		{
			90 => turn switch { 0 => 0x08, 0x08 => 0x10, 0x10 => 0x20, _ => 0 },
			180 => turn switch { 0 => 0x10, 0x08 => 0x20, 0x20 => 0x08, _ => 0 },
			270 => turn switch { 0 => 0x20, 0x10 => 0x08, 0x20 => 0x10, _ => 0 },
			_ => flags & 0x38,
		};

		return (ushort)((word & 0x6) | mirror | turned | 0x281);
	}

	/// <summary>
	/// The heightfield cell <c>FUN_00463060</c> stamps table cell (<paramref name="column"/>, <paramref name="row"/>) on, in the
	/// thing's covered cells (<c>+0xc0</c>/<c>+0xc4</c>, inclusive): unturned from its first corner, at 90 down from its
	/// first column's last row, at 180 back from its last, at 270 across from its last column's first row.
	/// </summary>
	public static (int X, int Y) CellOf( int column, int row, int angle, (int Left, int Top, int Right, int Bottom) covered )
		=> angle switch
		{
			90 => (covered.Left + row, covered.Bottom - column),
			180 => (covered.Right - column, covered.Bottom - row),
			270 => (covered.Right - row, covered.Top + column),
			_ => (covered.Left + column, covered.Top + row),
		};
}
