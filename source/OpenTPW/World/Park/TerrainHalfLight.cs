namespace OpenTPW;

/// <summary>
/// The terrain model's meshes on land outside the park, lit by shortened normals. After the ground's cells are marked
/// (<see cref="ParkGround.LandOutsideThePark"/>) <c>Terrain_ScaleNormalsByHalfLight</c> (<c>0x00450b30</c>) walks
/// <c>base.MD2</c>'s meshes, poses each vertex and takes its field cell (<see cref="CellOf"/>): the normal is made
/// <see cref="HalfLength"/> long where that cell is half lit, <see cref="FullLength"/> long elsewhere on the field, and
/// left alone off it. The lighter does not normalise, so a half-lit face takes 0.225 of the sun over the whole ambient:
/// a level face reads 0.517 where the ground beside it is halved (docs/exe/park-engine.md, "The terrain model's meshes
/// on that land are half lit too").
///
/// <para>
/// <b>A deviation:</b> the walk has no zero guard, so the original makes each of <c>base.MD2</c>'s zero-length normals
/// on the field NaN (102 in Lost Kingdom's), and what its lighter draws for one is not read. The normals here are the
/// loader's (<see cref="ModelFile.Mesh.Normals"/>), and one of those is shortened like any other; a zero one stays zero.
/// </para>
/// </summary>
public sealed class TerrainHalfLight
{
	/// <summary>The length a half-lit vertex's normal is given (<c>0x0074cf30</c>).</summary>
	public const float HalfLength = 0.225f;

	/// <summary>The length every other vertex's normal on the field is given (<c>0x006fe380</c>).</summary>
	public const float FullLength = 1.0f;

	/// <summary>The cell a unit of the model's x or z is worth (<c>0x006fe3a0</c>, the float 0.1).</summary>
	private const float CellPerUnit = 0.1f;

	private readonly bool[] _halfLit;
	private readonly int _cellsX;
	private readonly int _cellsY;

	/// <summary>Each mesh's vertices made <see cref="HalfLength"/>, <see cref="FullLength"/> and left alone.</summary>
	private readonly Dictionary<ModelFile.Mesh, (int Half, int Full, int Off)> _census = new( ReferenceEqualityComparer.Instance );

	/// <param name="halfLit">The ground cells at half light, by <c>y * cellsX + x</c> (<see cref="ParkGround.HalfLitMask"/>).</param>
	public TerrainHalfLight( bool[] halfLit, int cellsX, int cellsY )
	{
		_halfLit = halfLit;
		_cellsX = cellsX;
		_cellsY = cellsY;
	}

	/// <summary>
	/// The field cell a posed point stands on, from its x and z in the model's own axes (Y up), or null off the field:
	/// each times the float 0.1, truncated toward nought by <c>__ftol</c>, so a point at x in
	/// (-10, 0) is column 0; compared unsigned (<c>0x00450bd5</c>, <c>0x00450bdf</c>), so a negative cell is off it.
	/// </summary>
	internal static (int X, int Y)? CellOf( float x, float z, int cellsX, int cellsY )
	{
		// The product of two floats is exact in a double. The cell is the same when it is rounded to a float, as a 24-bit
		// x87 control word would, for every float within 0.01 of a cell edge from -10 to 960 (122,344 values checked).
		var column = (long)((double)x * CellPerUnit);
		var row = (long)((double)z * CellPerUnit);

		if ( column < 0 || column >= cellsX || row < 0 || row >= cellsY )
			return null;

		return ((int)column, (int)row);
	}

	/// <summary>The length a normal at a posed point is given, or null where it is left alone (off the field).</summary>
	internal float? LengthAt( float x, float z )
	{
		if ( CellOf( x, z, _cellsX, _cellsY ) is not { } cell )
			return null;

		return _halfLit[(cell.Y * _cellsX) + cell.X] ? HalfLength : FullLength;
	}

	/// <summary>
	/// The normal a mesh's vertex is lit by: the loader's, at the length its posed place gives (<see cref="LengthAt"/>),
	/// posed by the mesh's place in the node tree as the walk poses it. Counted in the census.
	/// </summary>
	public Vector3 Normal( ModelFile.Mesh mesh, int vertex )
	{
		var posed = System.Numerics.Vector3.Transform( mesh.Vertices[vertex].Position.GetSystemVector3(), mesh.WorldTransform );
		var length = LengthAt( posed.X, posed.Z );

		_census.TryGetValue( mesh, out var counts );
		_census[mesh] = length switch
		{
			HalfLength => counts with { Half = counts.Half + 1 },
			FullLength => counts with { Full = counts.Full + 1 },
			_ => counts with { Off = counts.Off + 1 }
		};

		var normal = mesh.Normals[vertex];

		if ( length is not { } scale )
			return normal;

		// Vector3.Normal keeps a zero normal zero.
		return normal.Normal * scale;
	}

	/// <summary>The vertices of a mesh made half, full and left alone, or nought for a mesh not seen.</summary>
	public (int Half, int Full, int Off) CountsFor( ModelFile.Mesh mesh ) => _census.GetValueOrDefault( mesh );

	private static string NameOf( ModelFile.Mesh mesh ) => mesh.Name.TrimEnd( '\0' ).Trim();

	/// <summary>
	/// The census as one line: the totals, then every mesh with a half-lit vertex as <c>name half/all</c>, in name order.
	/// </summary>
	public string Summary()
	{
		var half = _census.Values.Sum( c => c.Half );
		var full = _census.Values.Sum( c => c.Full );
		var off = _census.Values.Sum( c => c.Off );
		var meshes = _census
			.Where( pair => pair.Value.Half > 0 )
			.OrderBy( pair => NameOf( pair.Key ), StringComparer.Ordinal )
			.Select( pair => $"{NameOf( pair.Key )} {pair.Value.Half}/{pair.Value.Half + pair.Value.Full + pair.Value.Off}" );

		return $"{half} vertices of {_census.Count} terrain meshes made {HalfLength} long (half lit), {full} made {FullLength}, " +
			$"{off} off the field left alone; {_census.Count( pair => pair.Value.Half > 0 )} meshes half lit: {string.Join( ", ", meshes )}";
	}
}
