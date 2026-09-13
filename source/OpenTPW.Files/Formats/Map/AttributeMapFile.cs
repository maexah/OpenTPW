using System;
using System.IO;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A park's attribute map - one byte a cell over a 128x128 grid, saying what each cell <i>is</i> rather
/// than what it looks like. It ships inside the park's own <c>terrain.wad</c> as <c>base.map</c>, and
/// the jungle carries a second, <c>terrain.map</c>, which differs from it in 1,489 bytes.
///
/// <para>
/// The file says what it is on the tin: it opens <c>TP2M</c> followed by the ASCII
/// "Theme Park 2 Attribute Map File". <b>Attributes, not heights</b> - the heights live in
/// <see cref="HeightfieldFile"/>, in a block inside base.MD2.
/// </para>
///
/// <para>
/// <b>The header is 72 bytes, not 80.</b> 16464 is a tempting 16384 + 80, and that reading is wrong by
/// eight bytes in exactly the way that silently shifts every row of a grid against its own coordinates.
/// The real shape is a 72-byte header, 16,384 bytes of cells, then an 8-byte <c>END </c> trailer.
/// </para>
///
/// <para>
/// <b>Indexing is <c>x * 128 + y</c></b> - X is the major axis, which is the opposite of the
/// heightfield's <c>y * width + x</c> in the same park. Proven rather than assumed: all ten
/// <c>FixedItemInfo</c> positions in Standard.sam land on a meaningful attribute this way round and on
/// zero the other, in every theme.
/// </para>
/// </summary>
public sealed class AttributeMapFile
{
	/// <summary>Cells across and down. 128 in every park the game ships, and the file says so itself.</summary>
	public int Width { get; private init; }

	public int Height { get; private init; }

	/// <summary>One byte a cell, indexed <c>x * Width + y</c>.</summary>
	public byte[] Cells { get; private init; }

	public AttributeMapFile( Stream stream )
	{
		using var memory = new MemoryStream();
		stream.CopyTo( memory );

		var data = memory.GetBuffer();
		var length = (int)memory.Length;

		if ( length < 0x30 || Encoding.ASCII.GetString( data, 0, 4 ) != "TP2M" )
			throw new InvalidDataException( "Not an attribute map - no TP2M." );

		// 4 magic + a 32-byte description, then a chunk: 4-byte tag, 4-byte length, and a payload that
		// opens with its own two dimensions and five reserved words before the cells themselves.
		var tag = Encoding.ASCII.GetString( data, 0x24, 4 );

		if ( tag != "MAP " )
			throw new InvalidDataException( $"Expected a 'MAP ' chunk, found '{tag}'." );

		Width = (int)ReadUInt32( data, 0x2c );
		Height = (int)ReadUInt32( data, 0x30 );

		if ( Width <= 0 || Height <= 0 || Width > 4096 || Height > 4096 )
			throw new InvalidDataException( $"Attribute map says it is {Width}x{Height}." );

		const int CellsAt = 0x48;

		if ( CellsAt + (Width * Height) > length )
			throw new InvalidDataException(
				$"Attribute map is {length} bytes, too short for {Width}x{Height} cells from 0x{CellsAt:x}." );

		Cells = new byte[Width * Height];
		Array.Copy( data, CellsAt, Cells, 0, Cells.Length );
	}

	/// <summary>The attribute of one cell, or 0 outside the grid.</summary>
	public byte At( int x, int y )
		=> x < 0 || y < 0 || x >= Width || y >= Height
			? (byte)0
			: Cells[(x * Width) + y];

	private static uint ReadUInt32( byte[] data, int at )
		=> (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));
}
