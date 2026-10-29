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
/// The <b>interpolating pass</b> is the second of <c>FUN_00469940</c>'s two: the meshes the lookup records mark <c>0x8</c>
/// (the kart station's track and the water ride's, and their add-ons' bridges and tunnels) have the vertices the type load listed along
/// their cell lines snapped to the line and raised by the ground interpolated along it.
/// Not reproduced: the original's first pass also walks a mesh the second count flagged <c>0x1000</c> against the type list's
/// next vertex index (<c>0x00469b7c</c>); <c>gk_TRACK01</c> changes 17 vertices in the original against the 16 it lists (park-engine.md).
/// The arithmetic is <c>float</c> (the corner's fraction <c>double</c>), where the original's runs on the x87 under the control
/// word in force, <c>0x27f</c> (53-bit) under Proton; what Windows' DirectDraw leaves under <c>DDSCL_FPUSETUP</c> is not read
/// (park-engine.md, "The control word"). It moves the last bits only: the stored vertices agree to 0.0001. The original's
/// recomputed face normals (<c>FUN_00473c30</c>) reach no lighting, and are not made here.
/// </remarks>
public static class ProceduralConform
{
	/// <summary>A node's lookup-record flag that becomes the run-time <c>0x20000</c>: the drape arm takes the mesh (<c>0x0044a942</c>).</summary>
	internal const uint LookupDrapes = 0x10000;

	/// <summary>How near a cell line a vertex must be to be snapped to it (the immediate at <c>0x00469a96</c>).</summary>
	private const float SnapNear = 0.1f;

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
	/// <remarks>
	/// <b>A deviation:</b> ours always snaps x and y and clamps a corner past the field to its edge; the original snaps an axis
	/// only within 0.01 of a line (<c>0x00469f71</c>..<c>0x0046a028</c>) and ends the node and its children at a corner past
	/// the field. No placed thing reaches the difference (every origin is on a corner, a listed vertex on a line in model
	/// space). docs/exe/park-engine.md, "Where a placed object's model stands".
	/// </remarks>
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

				// The nearest corner: a fraction of a half or more takes the next. Always snapped and clamped: see the remarks.
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

	/// <summary>
	/// The interpolating pass (<c>FUN_00469a80</c>'s second call; the four arms are <c>0x0046a0b4</c>..<c>0x0046a0ea</c> and <c>0x0046a2a7</c>..<c>0x0046a32d</c>): of each mesh the lookup records
	/// mark <c>0x8</c>, the vertices <see cref="ProceduralBase.InterpolatedVertices"/> lists are snapped to the cell line they are
	/// within 0.1 of and raised by the ground interpolated along that line less <paramref name="origin"/>'s height, the height the
	/// model was made at (<see cref="Interpolated"/>). Answers how many vertices it moved.
	/// </summary>
	public static int Interpolate( ModelFile file, ItemShape? shape, Vec origin, Quaternion turn, HeightfieldFile field )
	{
		if ( (file.HeaderFlags & 0x8) == 0 )
			return 0;

		var grid = new ProceduralBase.Grid( field.OriginX, field.OriginY, field.CellSizeX, field.CellSizeY );
		var moved = 0;

		foreach ( var (index, vertices) in ProceduralBase.InterpolatedVertices( file, shape, grid ) )
		{
			var mesh = file.Meshes[index];
			var made = new Vec?[mesh.SourceVertices.Length];

			foreach ( var vertex in vertices )
			{
				var world = WorldOf( mesh, mesh.SourceVertices[vertex].GetSystemVector3(), origin, turn );

				// A vertex past the heightfield's last corner ends the mesh's pass (0x00469dd7..0x0046a2a0).
				if ( Interpolated( field, world, origin.Z ) is not { } stands )
					break;

				made[vertex] = ModelOf( mesh, stands, origin, turn );
			}

			moved += Put( mesh, made );
		}

		return moved;
	}

	/// <summary>
	/// Where the interpolating pass stands one vertex given in the world: x and y are snapped to the cell line within 0.1 of them
	/// (the line the nearest corner names, a fraction of a half or more taking the next), and the height rises by the ground
	/// interpolated along that line less <paramref name="baseHeight"/>. Four arms by where the vertex is: off the x line, the
	/// ground is blended along x from the corner on its side of the cell towards the neighbour on its other side; on it, along y.
	/// Null where the nearest corner is past the heightfield's last.
	/// </summary>
	public static Vec? Interpolated( HeightfieldFile field, Vec world, float baseHeight )
	{
		var (cornerX, fractionX, onX) = AlongAxis( world.X, field.OriginX, field.CellSizeX );
		var (cornerY, fractionY, onY) = AlongAxis( world.Y, field.OriginY, field.CellSizeY );

		if ( cornerX > field.CellsX || cornerY > field.CellsY )
			return null;

		var highX = fractionX >= 0.5f;
		var highY = fractionY >= 0.5f;

		float Height( int x, int y ) => field.HeightAt( Math.Clamp( x, 0, field.CellsX ), Math.Clamp( y, 0, field.CellsY ) );

		float ground;

		if ( !onX )
		{
			ground = highX
				? (fractionX * Height( cornerX, cornerY )) + ((1f - fractionX) * Height( cornerX - 1, cornerY ))
				: ((1f - fractionX) * Height( cornerX, cornerY )) + (fractionX * Height( cornerX + 1, cornerY ));
		}
		else
		{
			ground = highY
				? (fractionY * Height( cornerX, cornerY )) + ((1f - fractionY) * Height( cornerX, cornerY - 1 ))
				: ((1f - fractionY) * Height( cornerX, cornerY )) + (fractionY * Height( cornerX, cornerY + 1 ));
		}

		return new Vec( onX ? field.OriginX + (cornerX * field.CellSizeX) : world.X,
			onY ? field.OriginY + (cornerY * field.CellSizeY) : world.Y,
			world.Z + (ground - baseHeight) );
	}

	/// <summary>
	/// One axis of a world position on the grid: the nearest corner's index (a fraction of a half or more takes the next), the
	/// fraction past the cell's low line (a position below the origin is taken as on it), and whether it is within
	/// <see cref="SnapNear"/> of a line.
	/// </summary>
	private static (int Corner, float Fraction, bool On) AlongAxis( float world, float origin, float size )
	{
		var position = (world - origin) / size;

		if ( position < 0f )
			position = 0f;

		var whole = Math.Floor( (double)position );
		var fraction = (float)((double)position - whole);
		var corner = (int)whole + (fraction >= 0.5f ? 1 : 0);

		return (corner, fraction, MathF.Abs( fraction ) < SnapNear || MathF.Abs( fraction - 1f ) < SnapNear);
	}

	/// <summary>
	/// What <see cref="Measure"/> reads: the listed base vertices, how many of them the drawn model stands on a terrain corner, and the
	/// largest height off; and the interpolating pass's listed vertices with the largest difference of their rise from the ground along
	/// their line less the model's height.
	/// </summary>
	public readonly record struct Measurement( int Listed, int OnCorner, float LargestOff, int Interpolated = 0, float LargestRiseOff = 0f );

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

		var (interpolated, riseOff) = MeasureInterpolated( file, shape, origin, turn, field );

		return new Measurement( listed, on, largest, interpolated, riseOff );
	}

	/// <summary>
	/// The interpolating pass read back (<c>original/basevertices.py</c>'s <c>interp</c>): of the vertices it lists, each one's drawn rise
	/// from its rest place against the ground under the drawn place less the model's height, the ground taken by the cell's own triangle
	/// (<see cref="DrapeHeight"/>) and not by the pass's blend along the line. Answers the vertices listed and the largest difference.
	/// </summary>
	private static (int Listed, float Largest) MeasureInterpolated( ModelFile file, ItemShape? shape, Vec origin, Quaternion turn, HeightfieldFile field )
	{
		var grid = new ProceduralBase.Grid( field.OriginX, field.OriginY, field.CellSizeX, field.CellSizeY );
		var listed = 0;
		var largest = 0f;

		foreach ( var (index, vertices) in ProceduralBase.InterpolatedVertices( file, shape, grid ) )
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

				var rest = WorldOf( mesh, mesh.SourceVertices[vertex].GetSystemVector3(), origin, turn );
				var now = WorldOf( mesh, mesh.Vertices[at].Position.GetSystemVector3(), origin, turn );
				var difference = (now.Z - rest.Z) - (DrapeHeight( field, now.X, now.Y ) - origin.Z);

				largest = MathF.Max( largest, MathF.Abs( difference ) );
			}
		}

		return (listed, largest);
	}

	/// <summary>Every mesh: the queue, hoarding, kart and water pieces, loaded with load bit <c>0x2</c>, which marks all of their meshes <c>0x20000</c> (<c>FUN_00461f10</c>).</summary>
	public static bool EveryMesh( int mesh ) => true;

	/// <summary>
	/// Drapes a queue, hoarding, kart or water piece over the ground it is made on: every mesh (<see cref="EveryMesh"/>), at the
	/// ground the park has. A piece's meshes are all drape nodes, which the interpolating pass's count
	/// skips (<c>0x0046a43d</c>), so nothing of <see cref="Interpolate"/> is owed here. Answers how many vertices it moved; none where there is no ground.
	/// </summary>
	public static int DrapeOver( ModelFile file, Vector3 origin, Quaternion turn )
	{
		if ( ParkGround.Current?.Heightfield is not { } field )
			return 0;

		return Drape( file, EveryMesh, origin.GetSystemVector3(), turn, field );
	}

	/// <summary>The meshes of a placed object's model whose lookup record carries <c>0x10000</c> (Lost Kingdom's one: <c>mammtunn</c>'s <c>gk_mam1</c>).</summary>
	public static Func<int, bool> LookupDraped( ModelFile file )
		=> mesh => mesh < file.Nodes.Count && (file.Nodes[mesh].IdFlags & LookupDrapes) != 0;

	/// <summary>
	/// The ground's height under a world position as the draped vertex takes it (<c>0x0046a34c</c>, Q398's note): the cell by
	/// truncation, and the plane of the triangle its flag <c>0x4</c> picks - clear, the point below the diagonal (<c>z &lt;= x</c> in
	/// the cell) takes corners (0,0), (1,0), (1,1) and above it (0,0), (0,1), (1,1); set, the point (<c>1 - z &lt;= x</c>) takes
	/// (0,1), (1,1), (1,0) and else (0,0), (1,0), (0,1). The ground mesh is cut on the same diagonal, by the cell's flag <c>0x2</c>
	/// (<see cref="ParkGround.PutCellElements"/>), so a draped vertex stands on the triangle the ground beside it draws.
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
