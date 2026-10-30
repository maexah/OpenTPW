using System;
using System.IO;
using N = System.Numerics;

namespace OpenTPW;

/// <summary>
/// A park's landscape: the heights the ground is built from, and one record per cell saying how that
/// cell is drawn. It lives in a block inside the park's own <c>base.MD2</c> - the same file
/// <see cref="ModelFile"/> reads for the scenery standing on it - rather than in a file of its own.
///
/// <para>
/// <b>base.lnd is not this.</b> It is the obvious candidate by name, it is the biggest thing in
/// terrain.wad, and it is the wrong file: it holds the procedural ground's texture source data, which the
/// original reads unless a render bit forbids it (it logs "Procedural textures not allowed" only when a debug bit is
/// also set). The ground at medium and high is composited from it (<c>ProceduralGround</c>); a park without it draws
/// every cell's own texture.
/// </para>
///
/// <para>
/// The original finds the block through the MD2 header's pointer at 0x6C (FileFormats <c>models.md</c>; the
/// engine reads <c>[[0x007a0854] + 0x6c]</c>, <c>docs/exe/park-engine.md</c>). This reader does not read
/// that pointer: it searches for the block by its own invariants, because the block sits somewhere
/// different in every theme - 0x144e30 in the jungle, 0x115840 in fantasy, 0x1129c8 in hallow and
/// 0x11a3b0 in space - and the header is stored <i>after</i> the two arrays it points at. On all five
/// files that set 0x6C (the four parks' and the lobby's) the pointer names the offset the search
/// finds, and no other 4-aligned position satisfies the invariants: the two counts have to agree with
/// the two dimensions both ways round, and the gap between the two array offsets has to be exactly four
/// bytes per vertex.
/// </para>
///
/// <para>
/// All four shipped parks are the same size - 96x85 cells, 97x86 vertices, 8,342 heights and 8,160
/// cell records, at ten world units a cell - which is what makes the parks re-skins of one another
/// rather than different shapes.
/// </para>
/// </summary>
public sealed class HeightfieldFile
{
	/// <summary>The block header's size in bytes - its last field is the uint at +0x2c - which the scan needs left in the file.</summary>
	private const int HeaderSize = 0x30;

	/// <summary>Cells across, and down. 96 x 85 in every park the game ships.</summary>
	public int CellsX { get; private init; }

	public int CellsY { get; private init; }

	/// <summary>Where cell (0, 0)'s near corner lies, at +0x08 and +0x0C: nought in every park the game ships.</summary>
	public float OriginX { get; private init; }

	public float OriginY { get; private init; }

	/// <summary>How wide and deep one cell is in world units. 10.0 in every park the game ships.</summary>
	public float CellSizeX { get; private init; }

	public float CellSizeY { get; private init; }

	/// <summary>
	/// The heights' authored range, low and high, at +0x20 and +0x24: whole numbers within 1.0 of the
	/// lowest and highest height (jungle -10 and 60; the lobby 0 and 0). The original lights the ground
	/// with its span - see <c>ParkGround.NormalAt</c>.
	/// </summary>
	public float HeightLow { get; private init; }

	public float HeightHigh { get; private init; }

	/// <summary>
	/// One height per <i>vertex</i>, so there is one more of them in each direction than there are
	/// cells: <c>(CellsX + 1) * (CellsY + 1)</c>, indexed <c>y * (CellsX + 1) + x</c>. Raw world units -
	/// the vertical scale is 1, not a fixed-point fraction of anything.
	/// </summary>
	public float[] Heights { get; private init; }

	/// <summary>
	/// One record per cell, indexed <c>y * CellsX + x</c>. The low sixteen bits are flags and the high
	/// sixteen are a texture index - it is <b>not</b> four independent bytes, and reading it that way
	/// turns the mirror and rotation bits into a texture id.
	/// </summary>
	public uint[] Cells { get; private init; }

	/// <summary>Where in the file the block header was found, for diagnostics.</summary>
	public int BlockOffset { get; private init; }

	/// <summary>
	/// The model's own texture names, in the order a cell's texture index counts through them - so
	/// <c>TextureNames[Cells[i] &gt;&gt; 16]</c> is the texture that cell is drawn with. They are named
	/// <c>.tga</c> here and the files on disk are the same stem with <c>.wct</c>.
	///
	/// <para>
	/// Read here, in a class named for the heightfield, because the cell records that index them are
	/// here and the bytes are already in hand: the alternative is parsing a 272-mesh, 1.3MB model a
	/// third time during a park load to recover seven strings.
	/// </para>
	///
	/// <para>
	/// Every park the game ships uses six of these - its ground base set, <c>jgr_bas1..6</c> for the
	/// jungle and fantasy, <c>hrk_bas1..6</c> for hallow, <c>sfl_bas1..6</c> for space - plus index 0,
	/// which is not a ground texture at all. Index 0 lands on whatever frame happens to sit first and
	/// covers exactly the cells whose flag word is 0x1, which are the river and the paths. <b>The
	/// indices differ per theme</b>, so they are positions in this model's table and nothing more.
	/// </para>
	/// </summary>
	public string[] TextureNames { get; private init; } = [];

	public int VertexCount => (CellsX + 1) * (CellsY + 1);

	public int CellCount => CellsX * CellsY;

	/// <summary>The height at a vertex of the grid, clamped to the edge so that a cell on the boundary can still ask for its far corner.</summary>
	public float HeightAt( int x, int y )
	{
		var stride = CellsX + 1;

		x = Math.Clamp( x, 0, CellsX );
		y = Math.Clamp( y, 0, CellsY );

		return Heights[(y * stride) + x];
	}

	/// <summary>
	/// The height of the ground under a world position, interpolated across whichever cell it falls
	/// in rather than stepped per cell - so something following the ground rides up a slope instead
	/// of climbing it ten units at a time.
	///
	/// <para>
	/// Outside the map it returns the nearest edge height, because <see cref="HeightAt"/> clamps.
	/// That is deliberate: a camera scrolled past the edge should sit level with the ground it just
	/// left, not fall to zero.
	/// </para>
	/// </summary>
	public float HeightAtWorld( float worldX, float worldY )
	{
		var gridX = worldX / CellSizeX;
		var gridY = worldY / CellSizeY;

		var x = (int)MathF.Floor( gridX );
		var y = (int)MathF.Floor( gridY );

		var alongX = gridX - x;
		var alongY = gridY - y;

		var near = HeightAt( x, y ) + ((HeightAt( x + 1, y ) - HeightAt( x, y )) * alongX);
		var far = HeightAt( x, y + 1 ) + ((HeightAt( x + 1, y + 1 ) - HeightAt( x, y + 1 )) * alongX);

		return near + ((far - near) * alongY);
	}

	/// <summary>A cell flag: the cell is two triangles, each with its own slope (<c>FUN_004527f0</c>).</summary>
	private const ushort SplitFlag = 0x800;

	/// <summary>A cell flag, read with <see cref="SplitFlag"/>: the split runs from the far-x corner to the far-y one.</summary>
	private const ushort OtherDiagonalFlag = 0x4;

	/// <summary>
	/// The landscape's height under a world position as the original answers it - <c>FUN_004527f0</c>: nought off the
	/// map; on a cell not flagged <c>0x800</c>, the plane through its near corner and that corner's two neighbours,
	/// across the whole cell; on a flagged one, the plane of whichever of its two triangles the point is in, split
	/// along the diagonal from the near corner, or along the other where the cell's flags carry <c>0x4</c>.
	/// </summary>
	public float ScapeHeight( float worldX, float worldY )
	{
		if ( worldX < 0f || worldY < 0f || worldX >= CellsX * CellSizeX || worldY >= CellsY * CellSizeY )
			return 0f;

		var gridX = worldX / CellSizeX;
		var gridY = worldY / CellSizeY;

		var x = (int)gridX;
		var y = (int)gridY;

		var u = gridX - x;
		var v = gridY - y;

		var near = HeightAt( x, y );
		var alongX = HeightAt( x + 1, y );
		var alongY = HeightAt( x, y + 1 );
		var far = HeightAt( x + 1, y + 1 );

		var flags = FlagsAt( x, y );

		if ( (flags & SplitFlag) == 0 )
			return ((alongY - near) * v) + ((1f - u) * near) + (alongX * u);

		return TriangleHeight( near, alongX, alongY, far, u, v, (flags & OtherDiagonalFlag) != 0 );
	}

	/// <summary>
	/// The height of the ground under a world position as the park camera's sampler takes it - <c>FUN_00452560</c>, called with
	/// its third argument 1 (<c>0x0042c817</c>): the plane of whichever triangle of the cell the point is in, the cell cut on
	/// the diagonal its flag <c>0x4</c> names, on every cell and not only the ones flagged <c>0x800</c> (<c>0x00452719</c>;
	/// <c>park-engine.md</c>, "The triangle diagonal"). Off the map it answers the nearest edge's height where the original
	/// answers nought, so a camera scrolled past the edge stays level with the ground it left (a deviation, as <see cref="HeightAtWorld"/> has it).
	/// </summary>
	public float CameraHeight( float worldX, float worldY )
	{
		var gridX = (worldX - OriginX) / CellSizeX;
		var gridY = (worldY - OriginY) / CellSizeY;

		var x = Math.Clamp( (int)MathF.Floor( gridX ), 0, CellsX - 1 );
		var y = Math.Clamp( (int)MathF.Floor( gridY ), 0, CellsY - 1 );

		var u = Math.Clamp( gridX - x, 0f, 1f );
		var v = Math.Clamp( gridY - y, 0f, 1f );

		return TriangleHeight( HeightAt( x, y ), HeightAt( x + 1, y ), HeightAt( x, y + 1 ), HeightAt( x + 1, y + 1 ),
			u, v, (FlagsAt( x, y ) & OtherDiagonalFlag) != 0 );
	}

	/// <summary>
	/// The plane of the triangle of a cell a point (u, v) is in: split from the near corner to the far one, the point at or
	/// under the diagonal (<c>v &lt;= u</c>) takes the near, along-x and far corners and the other side the near, along-y and
	/// far corners; split the other way (<paramref name="otherDiagonal"/>), the point at or over it (<c>1 - v &lt;= u</c>) takes the
	/// along-y, far and along-x corners and the other side the near, along-x and along-y corners.
	/// </summary>
	private static float TriangleHeight( float near, float alongX, float alongY, float far, float u, float v, bool otherDiagonal )
	{
		if ( !otherDiagonal )
		{
			return v <= u
				? ((far - alongX) * v) + (u * alongX) + ((1f - u) * near)
				: ((1f - v) * -(alongY - near)) + ((1f - u) * alongY) + (far * u);
		}

		return 1f - v <= u
			? (-(far - alongX) * (1f - v)) + (u * far) + ((1f - u) * alongY)
			: ((alongY - near) * v) + ((1f - u) * near) + (alongX * u);
	}

	/// <summary>
	/// The plane through a cell's near corner and that corner's two neighbours, over every cell, split or not: what the
	/// original stands a person's shadow on (<c>SpriteShadow_BuildQuad</c>, <c>0x00589631</c> to <c>0x0058970b</c>). The cell is
	/// found by truncation and its far corners are clamped to the grid; off the map the near corner is clamped too, where
	/// the original reads past the heights (a deviation only there).
	/// </summary>
	public float CornerPlaneAt( float worldX, float worldY )
	{
		var gridX = worldX / CellSizeX;
		var gridY = worldY / CellSizeY;

		var x = (int)gridX;
		var y = (int)gridY;

		var near = HeightAt( x, y );

		return near + ((HeightAt( x + 1, y ) - near) * (gridX - x)) + ((HeightAt( x, y + 1 ) - near) * (gridY - y));
	}

	/// <summary>
	/// The load's pass over the cells - <c>FUN_004504c0</c>, <c>0x0045075d</c>: the two triangles across the diagonal from the
	/// along-x corner to the along-y one each take the cross product of their two edges (each edge made a unit vector first,
	/// <c>0x43a9a0</c>, or inline for the near triangle; the cross products are <b>not</b> made unit, <c>0x004506c0</c>, <c>0x00450725</c>), and a cell whose
	/// two products have a dot (<c>0x43a970</c>) under 0.9999 gets <see cref="SplitFlag"/>, so <see cref="ScapeHeight"/> splits
	/// it as the original does (<c>docs/exe/park-engine.md</c>, "Which height each asker takes"). The file never stores the flag.
	/// A cell is flagged where its two faces differ, or where both edges of a triangle slope so that the product falls short of
	/// unit. A cell bent by under about 0.1 passes, and so do most holes (Lost Kingdom flags 431 of 1,159).
	/// </summary>
	private void FlagNonPlanarCells()
	{
		for ( var y = 0; y < CellsY; ++y )
		for ( var x = 0; x < CellsX; ++x )
		{
			var (near, alongX, alongY, far) = (HeightAt( x, y ), HeightAt( x + 1, y ), HeightAt( x, y + 1 ), HeightAt( x + 1, y + 1 ));

			var a = Face( new N.Vector3( CellSizeX, alongX - near, 0f ), new N.Vector3( 0f, alongY - near, CellSizeY ) );
			var b = Face( new N.Vector3( -CellSizeX, alongY - far, 0f ), new N.Vector3( 0f, alongX - far, -CellSizeY ) );

			if ( N.Vector3.Dot( a, b ) < PlanarDot )
				Cells[(y * CellsX) + x] |= SplitFlag;
		}
	}

	/// <summary>The dot of the two triangles' faces under which the load calls a cell not planar (<c>[0x006fe39c]</c>).</summary>
	private const float PlanarDot = 0.9999f;

	/// <summary>The cross product of two edges each made a unit vector first, as the load takes it.</summary>
	private static N.Vector3 Face( N.Vector3 p, N.Vector3 q )
		=> N.Vector3.Cross( N.Vector3.Normalize( q ), N.Vector3.Normalize( p ) );

	/// <summary>The flag word of a cell - the low half of its record.</summary>
	public ushort FlagsAt( int x, int y ) => (ushort)(Cells[(y * CellsX) + x] & 0xFFFF);

	/// <summary>The texture index of a cell - the high half of its record.</summary>
	/// Dead by CODE: only ParkPathTests and ParkFootprintTests call it.
	public ushort TextureAt( int x, int y ) => (ushort)(Cells[(y * CellsX) + x] >> 16);

	public HeightfieldFile( Stream stream )
	{
		using var memory = new MemoryStream();
		stream.CopyTo( memory );

		var data = memory.GetBuffer();
		var length = (int)memory.Length;

		if ( !TryFindBlock( data, length, out var offset ) )
			throw new InvalidDataException( "No landscape block in this model - it carries no heightfield." );

		BlockOffset = offset;

		CellsX = (int)ReadUInt32( data, offset + 0x18 );
		CellsY = (int)ReadUInt32( data, offset + 0x1c );
		OriginX = ReadSingle( data, offset + 0x08 );
		OriginY = ReadSingle( data, offset + 0x0c );
		CellSizeX = ReadSingle( data, offset + 0x10 );
		CellSizeY = ReadSingle( data, offset + 0x14 );
		HeightLow = ReadSingle( data, offset + 0x20 );
		HeightHigh = ReadSingle( data, offset + 0x24 );

		var heightsOffset = (int)ReadUInt32( data, offset + 0x28 );
		var cellsOffset = (int)ReadUInt32( data, offset + 0x2c );

		Heights = new float[VertexCount];
		for ( var i = 0; i < Heights.Length; ++i )
			Heights[i] = ReadSingle( data, heightsOffset + (i * 4) );

		Cells = new uint[CellCount];
		for ( var i = 0; i < Cells.Length; ++i )
			Cells[i] = ReadUInt32( data, cellsOffset + (i * 4) );

		FlagNonPlanarCells();

		TextureNames = ReadTextureNames( data, length );
	}

	/// <summary>
	/// The model's frame table, which is what a cell's texture index counts through. <c>frameCount</c>
	/// is a u16 at 0x36, the records start at the offset in 0x54 and are sixteen bytes each, and each
	/// one's name pointer is the dword at +12 - the same layout <see cref="ModelFile"/> reads to name
	/// its materials.
	///
	/// <para>
	/// Returns empty rather than throwing if any of that does not add up: a landscape with no names is
	/// still a landscape, and the caller can fall back to drawing it plainly.
	/// </para>
	/// </summary>
	private static string[] ReadTextureNames( byte[] data, int length )
	{
		if ( length < 0x58 )
			return [];

		int count = ReadUInt16( data, 0x36 );
		var list = (int)ReadUInt32( data, 0x54 );

		if ( count <= 0 || count > 4096 || list <= 0 || list + (count * 16) > length )
			return [];

		var names = new string[count];

		for ( var i = 0; i < count; ++i )
		{
			var at = (int)ReadUInt32( data, list + (i * 16) + 12 );

			if ( at <= 0 || at >= length )
			{
				names[i] = string.Empty;
				continue;
			}

			var end = at;

			while ( end < length && data[end] != 0 )
				end++;

			names[i] = System.Text.Encoding.ASCII.GetString( data, at, end - at );
		}

		return names;
	}

	/// <summary>
	/// Walks the file looking for a header whose fields agree with one another (the original follows the
	/// pointer at 0x6C instead; see the class remarks). Every one of these has to hold, and together they
	/// are specific enough that the four shipped parks each yield exactly one match:
	///
	/// <list type="bullet">
	/// <item>the vertex and cell counts at +0x04 and +0x06 are both plausible sizes;</item>
	/// <item>the dimensions at +0x18 and +0x1c multiply out to the cell count;</item>
	/// <item>those dimensions plus one multiply out to the vertex count;</item>
	/// <item>both array offsets are inside the file;</item>
	/// <item>the gap between them is exactly four bytes per vertex, so the heights run straight into the cells;</item>
	/// <item>the cell array ends inside the file;</item>
	/// <item>both cell sizes are positive.</item>
	/// </list>
	/// </summary>
	private static bool TryFindBlock( byte[] data, int length, out int offset )
	{
		for ( offset = 0; offset + HeaderSize <= length; offset += 4 )
		{
			var vertexCount = ReadUInt16( data, offset + 0x04 );
			var cellCount = ReadUInt16( data, offset + 0x06 );

			if ( vertexCount < 4 || cellCount < 1 )
				continue;

			var cellsX = ReadUInt32( data, offset + 0x18 );
			var cellsY = ReadUInt32( data, offset + 0x1c );

			if ( cellsX is 0 or > 4096 || cellsY is 0 or > 4096 )
				continue;

			if ( cellsX * cellsY != cellCount )
				continue;

			if ( (cellsX + 1) * (cellsY + 1) != vertexCount )
				continue;

			var heights = ReadUInt32( data, offset + 0x28 );
			var cells = ReadUInt32( data, offset + 0x2c );

			if ( heights == 0 || cells <= heights || heights >= (uint)length )
				continue;

			if ( cells - heights != (uint)(vertexCount * 4) )
				continue;

			if ( cells + (cellCount * 4u) > (uint)length )
				continue;

			if ( ReadSingle( data, offset + 0x10 ) <= 0f || ReadSingle( data, offset + 0x14 ) <= 0f )
				continue;

			return true;
		}

		offset = -1;
		return false;
	}

	private static ushort ReadUInt16( byte[] data, int at ) => (ushort)(data[at] | (data[at + 1] << 8));

	private static uint ReadUInt32( byte[] data, int at )
		=> (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));

	private static float ReadSingle( byte[] data, int at ) => BitConverter.ToSingle( data, at );
}
