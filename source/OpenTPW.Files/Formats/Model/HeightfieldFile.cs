using System;
using System.IO;

namespace OpenTPW;

/// <summary>
/// A park's landscape: the heights the ground is built from, and one record per cell saying how that
/// cell is drawn. It lives in a block inside the park's own <c>base.MD2</c> - the same file
/// <see cref="ModelFile"/> reads for the scenery standing on it - rather than in a file of its own.
///
/// <para>
/// <b>base.lnd is not this.</b> It is the obvious candidate by name, it is the biggest thing in
/// terrain.wad, and it is the wrong file: it holds procedural texture source data and the original
/// only reads it when a render flag is set, logging "Procedural textures not allowed" when it is not.
/// A park loads and draws perfectly without it.
/// </para>
///
/// <para>
/// The block is found by searching for its own invariants rather than at a fixed offset, because it
/// sits somewhere different in every theme - 0x144e30 in the jungle, 0x115840 in fantasy, 0x1129c8 in
/// hallow and 0x11a3b0 in space - and the header is stored <i>after</i> the two arrays it points at.
/// The invariants are strong enough that no other position in any of the four files satisfies them:
/// the two counts have to agree with the two dimensions both ways round, and the gap between the two
/// array offsets has to be exactly four bytes per vertex.
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
	/// <summary>The block header's own size, and the distance from the header back to nothing - it points forwards and backwards.</summary>
	private const int HeaderSize = 0x30;

	/// <summary>Cells across, and down. 96 x 85 in every park the game ships.</summary>
	public int CellsX { get; private init; }

	public int CellsY { get; private init; }

	/// <summary>How wide and deep one cell is in world units. 10.0 in every park the game ships.</summary>
	public float CellSizeX { get; private init; }

	public float CellSizeY { get; private init; }

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

	/// <summary>The flag word of a cell - the low half of its record.</summary>
	public ushort FlagsAt( int x, int y ) => (ushort)(Cells[(y * CellsX) + x] & 0xFFFF);

	/// <summary>The texture index of a cell - the high half of its record.</summary>
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
		CellSizeX = ReadSingle( data, offset + 0x10 );
		CellSizeY = ReadSingle( data, offset + 0x14 );

		var heightsOffset = (int)ReadUInt32( data, offset + 0x28 );
		var cellsOffset = (int)ReadUInt32( data, offset + 0x2c );

		Heights = new float[VertexCount];
		for ( var i = 0; i < Heights.Length; ++i )
			Heights[i] = ReadSingle( data, heightsOffset + (i * 4) );

		Cells = new uint[CellCount];
		for ( var i = 0; i < Cells.Length; ++i )
			Cells[i] = ReadUInt32( data, cellsOffset + (i * 4) );
	}

	/// <summary>
	/// Walks the file looking for a header whose fields agree with one another. Every one of these has
	/// to hold, and together they are specific enough that the four shipped parks each yield exactly
	/// one match:
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
