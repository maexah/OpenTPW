namespace OpenTPW;

/// <summary>
/// Which of a model's materials the original's model loader makes opaque (<c>FUN_004629d0</c>). Asked with flag
/// <c>0x2000</c> it sets <c>0x800</c>, the bit <c>FUN_00581790</c> draws opaque whatever the texture's alpha, on every
/// material of the floor mesh <c>FUN_00469a80</c> picks, or of the root mesh where it picks none
/// (<c>Mesh_SetMaterialsOpaque</c>, <c>0x00462c71</c>). docs/exe/textures.md, "Which materials the loader makes opaque".
/// </summary>
/// <remarks>
/// A material's flag word is its frame-table entry's, so the bit reaches every material of the model naming the same
/// texture: the set is of <see cref="ModelFile.MaterialData.FrameOffset"/>s. <c>ProceduralBase_SkirtFlags</c> runs
/// before it on the same floor and only sets or clears <c>0x800</c> on the floor's own materials, so with
/// <c>0x2000</c> after it nothing it clears survives; every load here that runs it carries <c>0x2000</c>.
/// </remarks>
public static class ProceduralBase
{
	/// <summary>The ground grid a model's base is measured against: <c>[0x007a0854] + 0x6c</c>, the park's heightfield.</summary>
	public readonly record struct Grid( float OriginX, float OriginZ, float CellX, float CellZ );

	/// <summary>The running park's grid, or the one every shipped park has (origin nought, cells of 10) without one.</summary>
	public static Grid ParkGrid => ParkGround.Current?.Heightfield is { } ground
		? new Grid( ground.OriginX, ground.OriginY, ground.CellSizeX, ground.CellSizeY )
		: new Grid( 0f, 0f, 10f, 10f );

	/// <summary>How near a cell line and the ground a vertex must be to be a base vertex.</summary>
	private const float Near = 0.01f;

	/// <summary>Header flag 0x100: every base vertex on a cell corner counts, wherever the outline runs.</summary>
	private const uint CountsEveryCorner = 0x100;

	/// <summary>A node record flagged 0x200 is skipped by <c>FUN_00469a80</c>, its children not.</summary>
	private const uint SkippedNode = 0x200;

	/// <summary>A mesh flagged 0x20000 is walked by <c>FUN_00469a80</c> and counts nothing.</summary>
	private const uint CountsNothing = 0x20000;

	/// <summary>The edge bits <c>ProceduralBase_Build</c> gives a footprint cell where its neighbour differs.</summary>
	private const int LowZ = 1, LowX = 2, HighZ = 4, HighX = 8;

	/// <summary>
	/// The frames an ordinary item's model draws opaque: its floor's, by its <c>Info.Shape</c> picture. A fixed item
	/// (<c>Info.DontApplyOffset</c>) is loaded with neither flag (<c>0x00414192</c>) and is not asked.
	/// </summary>
	public static IReadOnlySet<uint> ForItem( ModelFile model, ItemShape? shape )
		=> FramesOf( model, FloorMesh( model, shape, ParkGrid, out _ ) ?? model.RootNode );

	/// <summary>
	/// The frames a queue piece draws opaque (<c>FUN_00522900</c>, flags <c>0x12423</c>, a one-cell footprint of kind
	/// nought): its root mesh's, as the floor search finds no outline to count along.
	/// </summary>
	public static IReadOnlySet<uint> ForQueuePiece( ModelFile model )
		=> FramesOf( model, FloorMesh( model, null, ParkGrid, out _ ) ?? model.RootNode );

	/// <summary>The frames an item's <c>P</c> model draws opaque: its root mesh's, searched for no floor (<c>0x00462ca7</c>).</summary>
	public static IReadOnlySet<uint> ForPModel( ModelFile model ) => FramesOf( model, model.RootNode );

	/// <summary>The frames every material of a mesh names, none for a node that is no mesh.</summary>
	public static IReadOnlySet<uint> FramesOf( ModelFile model, int mesh )
		=> mesh >= 0 && mesh < model.Meshes.Count
			? model.Meshes[mesh].Materials.Select( m => m.FrameOffset ).ToHashSet()
			: new HashSet<uint>();

	/// <summary>
	/// The mesh <c>FUN_00469a80</c> takes for the floor: of the meshes walked from the root, children after their
	/// parent, the first with the most base vertices, a vertex at the ground (<c>|y| &lt; 0.01</c>) on a cell's corner
	/// along the footprint's outline; null where none has one. <paramref name="count"/> is the model's base vertices
	/// in all (<c>[model + 8] + 0xc</c>).
	/// </summary>
	public static int? FloorMesh( ModelFile model, ItemShape? shape, Grid grid, out int count )
	{
		var edges = Edges( shape );
		var width = shape?.Width ?? 0;
		var depth = shape?.Depth ?? 0;
		var every = (model.HeaderFlags & CountsEveryCorner) != 0;

		int? floor = null;
		var most = 0;
		var total = 0;

		void Visit( int node, int guard )
		{
			for ( ; node >= 0 && node < model.Nodes.Count && guard > 0; node = model.Nodes[node].NextSibling, --guard )
			{
				var flags = model.Nodes[node].Flags;

				if ( node < model.Meshes.Count && (flags & SkippedNode) == 0 && (flags & CountsNothing) == 0 )
				{
					var mesh = model.Meshes[node];
					var bases = 0;

					foreach ( var source in mesh.SourceVertices )
					{
						var at = System.Numerics.Vector3.Transform( new System.Numerics.Vector3( source.X, source.Y, source.Z ), mesh.WorldTransform );

						if ( !IsBase( at, grid, edges, width, depth, every ) )
							continue;

						++total;

						if ( ++bases > most )
						{
							most = bases;
							floor = node;
						}
					}
				}

				Visit( model.Nodes[node].FirstChild, guard - 1 );
			}
		}

		Visit( model.RootNode, model.Nodes.Count + 1 );

		count = total;
		return floor;
	}

	/// <summary>
	/// <c>FUN_00469a80</c>'s test of one vertex in model space (the file's axes, y up): at the ground, on a cell line
	/// both ways, and on a side of its cell the outline runs along. A coordinate below the grid's origin is taken as
	/// on it; a cell past the footprint's last is its last, seen from the far side.
	/// </summary>
	/// <remarks>
	/// The original also stops the mesh's walk, and its children's, at a vertex past the ground's last cell (96 x 85, 960 x 850
	/// units); no Lost Kingdom mesh reaches past 502 units (its offset and its box), so that end is not kept.
	/// </remarks>
	private static bool IsBase( System.Numerics.Vector3 at, Grid grid, int[,]? edges, int width, int depth, bool every )
	{
		if ( MathF.Abs( at.Y ) >= Near )
			return false;

		var (cellX, onX, lowX) = Line( at.X, grid.OriginX, grid.CellX, width );
		var (cellZ, onZ, lowZ) = Line( at.Z, grid.OriginZ, grid.CellZ, depth );

		if ( !onX || !onZ )
			return false;

		if ( every )
			return true;

		if ( edges is null || cellX < 0 || cellZ < 0 || cellX >= edges.GetLength( 0 ) || cellZ >= edges.GetLength( 1 ) )
			return false;

		var bits = edges[cellX, cellZ];

		return (lowX && (bits & LowX) != 0) || (!lowX && (bits & HighX) != 0)
			|| (lowZ && (bits & LowZ) != 0) || (!lowZ && (bits & HighZ) != 0);
	}

	/// <summary>
	/// Where a coordinate lies on one axis: its cell (truncated, after a negative is taken as nought), whether it is
	/// within 0.01 of a line, and whether that is the cell's low side (a fraction under a half).
	/// </summary>
	private static (int Cell, bool On, bool Low) Line( float value, float origin, float size, int cells )
	{
		var position = (value - origin) / size;

		if ( position < 0f )
			position = 0f;

		var whole = Math.Floor( (double)position );
		var fraction = (float)((double)position - whole);
		var cell = (int)whole;
		var low = fraction < 0.5f;

		if ( cells > 0 && cell >= cells )
		{
			cell = cells - 1;
			low = false;
		}

		var on = MathF.Abs( fraction ) < Near || MathF.Abs( fraction - 1f ) < Near;

		return (cell, on, low);
	}

	/// <summary>
	/// <c>ProceduralBase_Build</c>'s edge bits for each cell of the picture: a side
	/// gets its bit where the cell is in the footprint (its kind's low four bits not nought) and the neighbour that way
	/// is not, or the other way round; past the picture's edge is out.
	/// </summary>
	private static int[,]? Edges( ItemShape? shape )
	{
		if ( shape is null || shape.Width <= 0 || shape.Depth <= 0 )
			return null;

		var w = shape.Width;
		var d = shape.Depth;

		bool In( int x, int z ) => x >= 0 && z >= 0 && x < w && z < d && (shape.KindAt( x, z ) & 0xf) != 0;

		var edges = new int[w, d];

		for ( var z = 0; z < d; ++z )
		{
			for ( var x = 0; x < w; ++x )
			{
				var here = In( x, z );
				var bits = 0;

				if ( here != In( x, z + 1 ) ) bits |= HighZ;
				if ( here != In( x - 1, z ) ) bits |= LowX;
				if ( here != In( x, z - 1 ) ) bits |= LowZ;
				if ( here != In( x + 1, z ) ) bits |= HighX;

				edges[x, z] = bits;
			}
		}

		return edges;
	}
}
