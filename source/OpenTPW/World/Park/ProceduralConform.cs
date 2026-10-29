using System.Numerics;
using Vec = System.Numerics.Vector3;

namespace OpenTPW;

/// <summary>
/// How the original stands a made model's vertices on the ground (<c>FUN_00469940</c> -> <c>FUN_00469a80</c> in its apply mode;
/// docs/exe/park-engine.md, "Where a placed object's model stands"): the <b>conform</b> of a thing's floor rim to the terrain's
/// corners, and the <b>drape</b> of a queue, hoarding or track piece over the ground vertex by vertex.
/// </summary>
/// <remarks>
/// Both work on the model's file before <see cref="LobbyModel"/> builds its meshes from it, moving only the drawn
/// <see cref="ModelFile.Mesh.Vertices"/>: <see cref="ModelFile.Mesh.SourceVertices"/> stay the model's rest, which the floor search,
/// the floor blend and the ride hoardings read. A vertex goes through the mesh's node chain into the world (the model's place
/// and turn), moves there, and is carried back through the inverse: the original's float LU inverse, here the matrix inverse.
/// Not built: the interpolating pass for the nine track meshes (<c>PROCEDURAL_BASE_INTERPOLATE</c>), the control word the
/// original's arithmetic runs under (moves the last bits only), and the recomputed face normals (<c>FUN_00473c30</c>).
/// </remarks>
public static class ProceduralConform
{
	/// <summary>A node's lookup-record flag that becomes the run-time <c>0x20000</c>: the drape arm takes the mesh (<c>0x0044a942</c>).</summary>
	private const uint LookupDrapes = 0x10000;

	/// <summary>A node's lookup-record flag that becomes the run-time <c>0x40</c>: the interpolating pass takes the mesh.</summary>
	private const uint LookupInterpolates = 0x8;

	/// <summary>The cell flag that sends a cell's split to its other diagonal (<c>0x0046a34c</c>; Q398's note).</summary>
	private const ushort OtherDiagonal = 0x4;

	/// <summary>
	/// The model-space vertex <paramref name="vertex"/> of <paramref name="mesh"/> in the world: through its node chain, the file's
	/// Y-up axes swapped for the world's Z-up, turned by <paramref name="turn"/> and moved to <paramref name="origin"/>.
	/// </summary>
	private static Vec WorldOf( ModelFile.Mesh mesh, Vec vertex, Vec origin, Quaternion turn )
	{
		var at = Vec.Transform( vertex, mesh.WorldTransform );

		return Vec.Transform( new Vec( at.X, at.Z, at.Y ), turn ) + origin;
	}

	/// <summary>The model-space vertex that stands at <paramref name="world"/>, the inverse of <see cref="WorldOf"/>; null where the mesh's chain is singular.</summary>
	private static Vec? ModelOf( ModelFile.Mesh mesh, Vec world, Vec origin, Quaternion turn )
	{
		if ( !Matrix4x4.Invert( mesh.WorldTransform, out var inverse ) )
			return null;

		var local = Vec.Transform( world - origin, Quaternion.Conjugate( turn ) );

		return Vec.Transform( new Vec( local.X, local.Z, local.Y ), inverse );
	}

	/// <summary>
	/// Stands every base vertex the type load listed on the nearest terrain corner (<c>0x00469f71</c>..<c>0x0046a33c</c>): x and
	/// y snap to the grid line, z is that corner's height outright, nothing added. A model made without the header's
	/// <c>0x8</c> (a procedural base) lists none. Answers how many vertices it moved.
	/// </summary>
	public static int Conform( ModelFile file, ItemShape? shape, Vec origin, Quaternion turn, HeightfieldFile field )
	{
		if ( (file.HeaderFlags & 0x8) == 0 )
			return 0;

		var grid = new ProceduralBase.Grid( field.OriginX, field.OriginY, field.CellSizeX, field.CellSizeY );
		var moved = 0;

		foreach ( var (index, vertices) in ProceduralBase.BaseVertices( file, shape, grid ) )
		{
			var mesh = file.Meshes[index];
			var made = new Vec?[mesh.SourceVertices.Length];

			foreach ( var vertex in vertices )
			{
				var world = WorldOf( mesh, mesh.SourceVertices[vertex].GetSystemVector3(), origin, turn );

				// The nearest corner: a fraction of a half or more takes the next.
				var cornerX = Math.Clamp( (int)MathF.Floor( ((world.X - field.OriginX) / field.CellSizeX) + 0.5f ), 0, field.CellsX );
				var cornerY = Math.Clamp( (int)MathF.Floor( ((world.Y - field.OriginY) / field.CellSizeY) + 0.5f ), 0, field.CellsY );

				var stands = new Vec( field.OriginX + (cornerX * field.CellSizeX), field.OriginY + (cornerY * field.CellSizeY),
					field.HeightAt( cornerX, cornerY ) );

				made[vertex] = ModelOf( mesh, stands, origin, turn );
			}

			moved += Put( mesh, made );
		}

		return moved;
	}

	/// <summary>
	/// Drapes the meshes <paramref name="which"/> picks: every vertex rises by the ground under it less
	/// <paramref name="origin"/>'s height, the height the model was made at (<c>0x0046a34c</c>; <see cref="DrapeHeight"/>).
	/// Answers how many vertices it moved.
	/// </summary>
	public static int Drape( ModelFile file, Func<int, bool> which, Vec origin, Quaternion turn, HeightfieldFile field )
	{
		var moved = 0;

		for ( var index = 0; index < file.Meshes.Count; ++index )
		{
			if ( !which( index ) )
				continue;

			var mesh = file.Meshes[index];
			var made = new Vec?[mesh.SourceVertices.Length];

			for ( var vertex = 0; vertex < made.Length; ++vertex )
			{
				var world = WorldOf( mesh, mesh.SourceVertices[vertex].GetSystemVector3(), origin, turn );
				var rise = DrapeHeight( field, world.X, world.Y ) - origin.Z;

				if ( rise != 0f )
					made[vertex] = ModelOf( mesh, world with { Z = world.Z + rise }, origin, turn );
			}

			moved += Put( mesh, made );
		}

		return moved;
	}

	/// <summary>What <see cref="Measure"/> reads: the listed base vertices, how many of them the drawn model stands on a terrain corner, and the largest height off.</summary>
	public readonly record struct Measurement( int Listed, int OnCorner, float LargestOff );

	/// <summary>
	/// How the drawn model stands against the ground: of the base vertices the type load would list, how many have their drawn
	/// position's height equal to the nearest terrain corner's (within 0.001), and the largest difference. The reading
	/// <c>original/basevertices.py</c> takes of the original's live memory, here of the model <see cref="Conform"/> moved.
	/// </summary>
	public static Measurement Measure( ModelFile file, ItemShape? shape, Vec origin, Quaternion turn, HeightfieldFile? field )
	{
		if ( field == null || (file.HeaderFlags & 0x8) == 0 )
			return default;

		var grid = new ProceduralBase.Grid( field.OriginX, field.OriginY, field.CellSizeX, field.CellSizeY );
		int listed = 0, on = 0;
		var largest = 0f;

		foreach ( var (index, vertices) in ProceduralBase.BaseVertices( file, shape, grid ) )
		{
			var mesh = file.Meshes[index];
			var drawn = new Dictionary<int, int>();

			for ( var i = 0; i < mesh.VertexOrder.Length && i < mesh.Vertices.Length; ++i )
				drawn.TryAdd( mesh.VertexOrder[i], i );

			foreach ( var vertex in vertices )
			{
				++listed;

				if ( !drawn.TryGetValue( vertex, out var at ) )
					continue;

				var world = WorldOf( mesh, mesh.Vertices[at].Position.GetSystemVector3(), origin, turn );
				var cornerX = Math.Clamp( (int)MathF.Floor( ((world.X - field.OriginX) / field.CellSizeX) + 0.5f ), 0, field.CellsX );
				var cornerY = Math.Clamp( (int)MathF.Floor( ((world.Y - field.OriginY) / field.CellSizeY) + 0.5f ), 0, field.CellsY );
				var off = MathF.Abs( world.Z - field.HeightAt( cornerX, cornerY ) );

				largest = MathF.Max( largest, off );
				on += off <= 0.001f ? 1 : 0;
			}
		}

		return new Measurement( listed, on, largest );
	}

	/// <summary>Every mesh: the queue, hoarding, kart and water pieces, loaded with load bit <c>0x2</c>, which marks all of their meshes <c>0x20000</c> (<c>FUN_00461f10</c>).</summary>
	public static bool EveryMesh( int mesh ) => true;

	/// <summary>
	/// Drapes a queue, hoarding, kart or water piece over the ground it is made on: every mesh (<see cref="EveryMesh"/>), at the
	/// ground the park has. Counts <c>PROCEDURAL_BASE_INTERPOLATE</c> where a mesh the lookup records mark <c>0x8</c> then wants the
	/// interpolating pass, unbuilt. Answers how many vertices it moved; none where there is no ground.
	/// </summary>
	public static int DrapeOver( ModelFile file, Vector3 origin, Quaternion turn )
	{
		if ( ParkGround.Current?.Heightfield is not { } field )
			return 0;

		if ( InterpolatedMeshes( file ) > 0 )
			Unimplemented.Report( "PROCEDURAL_BASE_INTERPOLATE" );

		return Drape( file, EveryMesh, origin.GetSystemVector3(), turn, field );
	}

	/// <summary>The meshes of a placed object's model whose lookup record carries <c>0x10000</c> (Lost Kingdom's one: <c>mammtunn</c>'s <c>gk_mam1</c>).</summary>
	public static Func<int, bool> LookupDraped( ModelFile file )
		=> mesh => mesh < file.Nodes.Count && (file.Nodes[mesh].IdFlags & LookupDrapes) != 0;

	/// <summary>How many meshes of a model carry the lookup flag <c>0x8</c>, the ones the interpolating pass takes and this does not (<c>PROCEDURAL_BASE_INTERPOLATE</c>).</summary>
	public static int InterpolatedMeshes( ModelFile file )
		=> file.Meshes.Where( ( _, mesh ) => mesh < file.Nodes.Count && (file.Nodes[mesh].IdFlags & LookupInterpolates) != 0 ).Count();

	/// <summary>
	/// The ground's height under a world position as the draped vertex takes it (<c>0x0046a34c</c>, Q398's note): the cell by
	/// truncation, and the plane of the triangle its flag <c>0x4</c> picks - clear, the point below the diagonal (<c>z &lt;= x</c> in
	/// the cell) takes corners (0,0), (1,0), (1,1) and above it (0,0), (0,1), (1,1); set, the point (<c>1 - z &lt;= x</c>) takes
	/// (0,1), (1,1), (1,0) and else (0,0), (1,0), (0,1). The ground mesh here is always split the first way (Q398), so on a cell
	/// flagged <c>0x4</c> a draped vertex stands on the original's triangle and the mesh beside it on ours.
	/// </summary>
	public static float DrapeHeight( HeightfieldFile field, float worldX, float worldY )
	{
		var gridX = (worldX - field.OriginX) / field.CellSizeX;
		var gridY = (worldY - field.OriginY) / field.CellSizeY;

		var x = Math.Clamp( (int)MathF.Floor( gridX ), 0, field.CellsX - 1 );
		var y = Math.Clamp( (int)MathF.Floor( gridY ), 0, field.CellsY - 1 );

		var u = Math.Clamp( gridX - x, 0f, 1f );
		var v = Math.Clamp( gridY - y, 0f, 1f );

		float h00 = field.HeightAt( x, y ), h10 = field.HeightAt( x + 1, y ), h01 = field.HeightAt( x, y + 1 ), h11 = field.HeightAt( x + 1, y + 1 );

		if ( (field.FlagsAt( x, y ) & OtherDiagonal) == 0 )
		{
			return v <= u
				? h00 + ((h10 - h00) * u) + ((h11 - h10) * v)
				: h00 + ((h11 - h01) * u) + ((h01 - h00) * v);
		}

		return 1f - v <= u
			? h01 + ((h11 - h01) * u) + ((h10 - h11) * (1f - v))
			: h00 + ((h10 - h00) * u) + ((h01 - h00) * v);
	}

	/// <summary>Lays the new positions over the drawn vertices: a source vertex stands under every drawn vertex that names it. Answers how many source vertices moved.</summary>
	private static int Put( ModelFile.Mesh mesh, Vec?[] made )
	{
		var moved = 0;

		foreach ( var position in made )
			moved += position.HasValue ? 1 : 0;

		if ( moved == 0 )
			return 0;

		for ( var drawn = 0; drawn < mesh.Vertices.Length && drawn < mesh.VertexOrder.Length; ++drawn )
		{
			var source = mesh.VertexOrder[drawn];

			if ( source < made.Length && made[source] is { } position )
				mesh.Vertices[drawn].Position = (Vector3)position;
		}

		return moved;
	}
}
