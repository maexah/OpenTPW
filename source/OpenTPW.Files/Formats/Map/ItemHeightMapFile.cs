using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;

namespace OpenTPW;

/// <summary>
/// An item's <c>.hmp</c>: how high the item stands over each cell of its footprint, in its own unturned
/// space. The layout is FileFormats <c>hmp.md</c>; the original loads it in <c>FUN_00451640</c> and reads it in
/// <c>FUN_00452ae0</c> (<c>docs/exe/park-engine.md</c>, "Placement feedback").
/// </summary>
/// <remarks>
/// <see cref="Read"/> answers null where the original would rebuild the file instead: missing, or a
/// signature it does not recognise. A file of the wrong size for its footprint is the caller's to judge,
/// because the size it expects comes from the item's <c>.sam</c>.
/// </remarks>
public sealed class ItemHeightMapFile
{
	/// <summary>The two signature dwords at <c>0x00</c> and <c>0x04</c> the loader tests.</summary>
	public const uint Signature = 0xAB1E0003, Version = 0x00640005;

	private const int HeaderSize = 0x30;

	/// <summary>A raster byte is a height times 2.55; the original reads it back times <c>0x006fe408</c>, 1 / 2.55.</summary>
	public const float UnitsPerByte = 0.39215687f;

	/// <summary>Raster samples a cell has along each side.</summary>
	public const int SamplesPerCell = 5;

	/// <summary>Cells across, the item's own x (<c>+0x08</c>).</summary>
	public int Columns { get; }

	/// <summary>Cells down, the item's own z (<c>+0x0a</c>).</summary>
	public int Rows { get; }

	/// <summary>The model's box, min then max, in the original's axes (<c>+0x18</c>, which the loader copies to the model's <c>+0x80</c>).</summary>
	public Vector3 BoxMin { get; }

	public Vector3 BoxMax { get; }

	/// <summary>The fine raster, (5 x columns) by (5 x rows) bytes, row by row.</summary>
	public byte[] Raster { get; }

	/// <summary>The highest raster byte over each cell, row by row - the grid <c>FUN_00452ae0</c> reads.</summary>
	public byte[] CellMaxima { get; }

	/// <summary>One byte a cell, 1 where the item's <c>Info.Shape</c> marks it, row by row.</summary>
	public byte[] Marks { get; }

	private ItemHeightMapFile( byte[] data )
	{
		Columns = BinaryPrimitives.ReadUInt16LittleEndian( data.AsSpan( 0x08 ) );
		Rows = BinaryPrimitives.ReadUInt16LittleEndian( data.AsSpan( 0x0a ) );

		var cells = (long)Columns * Rows;
		var rasterAt = (long)ReadInt32( data, 0x0c );
		var maximaAt = (long)ReadInt32( data, 0x10 );
		var marksAt = (long)ReadInt32( data, 0x14 );

		if ( cells == 0 || rasterAt < HeaderSize || maximaAt < HeaderSize || marksAt < HeaderSize
			|| rasterAt + (cells * SamplesPerCell * SamplesPerCell) > data.Length
			|| maximaAt + cells > data.Length || marksAt + cells > data.Length )
			throw new InvalidDataException( $".hmp says {Columns}x{Rows} with planes at 0x{rasterAt:x}, 0x{maximaAt:x}, 0x{marksAt:x} in {data.Length} bytes" );

		BoxMin = new Vector3( ReadSingle( data, 0x18 ), ReadSingle( data, 0x1c ), ReadSingle( data, 0x20 ) );
		BoxMax = new Vector3( ReadSingle( data, 0x24 ), ReadSingle( data, 0x28 ), ReadSingle( data, 0x2c ) );

		Raster = data.AsSpan( (int)rasterAt, (int)cells * SamplesPerCell * SamplesPerCell ).ToArray();
		CellMaxima = data.AsSpan( (int)maximaAt, (int)cells ).ToArray();
		Marks = data.AsSpan( (int)marksAt, (int)cells ).ToArray();
	}

	/// <summary>The file, or null where its signature is not the one the original's loader accepts.</summary>
	public static ItemHeightMapFile? Read( byte[] data )
	{
		if ( data.Length < HeaderSize || ReadUInt32( data, 0 ) != Signature || ReadUInt32( data, 4 ) != Version )
			return null;

		return new ItemHeightMapFile( data );
	}

	/// <summary>
	/// How high the item stands over one of its own cells, above its own base - <c>FUN_00452ae0</c>'s byte / 2.55.
	/// Null outside the footprint, where the original reads past the grid unchecked.
	/// </summary>
	public float? HeightOver( int column, int row )
		=> column < 0 || row < 0 || column >= Columns || row >= Rows
			? null
			: CellMaxima[(row * Columns) + column] * UnitsPerByte;

	private static int ReadInt32( byte[] data, int at ) => BinaryPrimitives.ReadInt32LittleEndian( data.AsSpan( at ) );

	private static uint ReadUInt32( byte[] data, int at ) => BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( at ) );

	private static float ReadSingle( byte[] data, int at ) => BinaryPrimitives.ReadSingleLittleEndian( data.AsSpan( at ) );
}
