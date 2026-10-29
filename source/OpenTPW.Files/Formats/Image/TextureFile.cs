using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenTPW;

public record struct TextureData( int Width, int Height, byte[] Data );

/// <summary>
/// The three alpha states the original's texture flags give a texture (<c>docs/exe/render-states.md</c>, "The four state
/// words"): no alpha test, the test at 240 of 255 (<c>0x100</c>) or at 16 (<c>0x200</c>), both blending.
/// </summary>
public enum TextureAlpha
{
	Opaque,
	CutOut,
	Graded
}

public partial class TextureFile : BaseFormat
{
	public TextureData Data { get; set; }

	/// <summary>
	/// False when there was no image to read - the file isn't in the archive, or the path is
	/// wrong. <see cref="Data"/> still holds something drawable in that case, but callers that
	/// have a better answer than a debug colour should check this first.
	/// </summary>
	public bool IsValid { get; private set; }

	/// <summary>
	/// How the original draws this .wct's alpha, by its texture flags (<c>FUN_00574bd0</c>): opaque without an alpha
	/// chunk, cut out (reference 240) with one under a header byte 1 of nought, graded (16) with one under a byte 1 set.
	/// </summary>
	public TextureAlpha Alpha { get; private set; }

	static float ComputeDequantizationScaleY( int n ) => 1.0f - ((float)n * -0.5f);
	static float ComputeDequantizationScaleCbCr( int n ) => 1.0f - ((float)n * -0.25f);
	static float ComputeDequantizationScaleA( int n ) => (float)n + 1.0f;

	/// <summary>Reads a texture, or with no stream (a file not found) the last resort, <see cref="LastResort"/>.</summary>
	public TextureFile( Stream? stream )
	{
		if ( stream == null )
			LastResort();
		else
			ReadFromStream( stream );
	}

	public TextureFile( string path )
	{
		ReadFromFile( path );
	}

	/// <summary>Decodes an image whose header was assembled by the caller - see <see cref="Decode"/>.</summary>
	internal TextureFile( TextureFileData fileData )
	{
		Decode( fileData );
	}

	/// <summary>A texture an archive does not hold reads as <see cref="LastResort"/>, not a throw.</summary>
	protected override void ReadFromFile( string path )
	{
		using var stream = FileSystem.OpenReadOrNull( path );

		if ( stream == null )
			LastResort();
		else
			ReadFromStream( stream );
	}

	/// <summary>
	/// Last resort only. Anything drawn into the world goes through Texture, which substitutes the game's own
	/// not-found texture before this can reach the screen.
	/// </summary>
	private void LastResort()
	{
		Data = new TextureData( 1, 1, new byte[] { 255, 0, 255, 255 } );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var binaryReader = new BinaryReader( stream );

		//
		// Read header
		//
		var flags = (TextureFlags)binaryReader.ReadByte();

		// Byte 1 does not decide the alpha. Set over an alpha chunk, the original's texture flags take the graded alpha
		// reference (16) over the cut-out one (240), FUN_00574bd0 (docs/exe/textures.md, "The loader, `FUN_00564c00`").
		var graded = binaryReader.ReadByte() != 0;

		var fileData = new TextureFileData()
		{
			Flags = flags,
			BitsPerPixel = binaryReader.ReadByte(),
			Version = binaryReader.ReadByte(),
			Width = binaryReader.ReadInt16(),
			Height = binaryReader.ReadInt16(),
			// The header's four scales made multipliers as the loader makes them (0x00564e20). Cr's scale comes before
			// Cb's: the original hands them on as Y, offset 12, offset 10, and both of its decoders take the second
			// channel (Cb, x1.772) with the first of those.
			YMultiplier = ComputeDequantizationScaleY( binaryReader.ReadInt16() ),
			CrMultiplier = ComputeDequantizationScaleCbCr( binaryReader.ReadInt16() ),
			CbMultiplier = ComputeDequantizationScaleCbCr( binaryReader.ReadInt16() ),
			AMultiplier = ComputeDequantizationScaleA( binaryReader.ReadInt16() ),

			ColorChunkSize = binaryReader.ReadInt32(),
			AlphaChunkSize = binaryReader.ReadInt32(),

			Checksum = binaryReader.ReadInt32()
		};

		// The alpha chunk is read, and the image given its alpha, when the bit count is 32 (0x00564c72); without one
		// the image is opaque.
		fileData.HasAlphaChannel = fileData.BitsPerPixel == 32;
		Alpha = !fileData.HasAlphaChannel ? TextureAlpha.Opaque : graded ? TextureAlpha.Graded : TextureAlpha.CutOut;

		//
		// Read blocks
		//

		// Color
		if ( fileData.ColorChunkSize == 0 )
			throw new InvalidDataException( "Texture does not have color data" );

		fileData.ColorChunk = binaryReader.ReadBytes( fileData.ColorChunkSize );

		// Alpha
		if ( fileData.AlphaChunkSize > 0 )
			fileData.AlphaChunk = binaryReader.ReadBytes( fileData.AlphaChunkSize );

		Decode( fileData );
	}

	/// <summary>
	/// Decodes an image from a header plus its already-read colour and alpha chunks.
	///
	/// Split out from <see cref="ReadFromStream"/> because a .wct file is not the only thing that
	/// carries one of these images - a .sgn park sign embeds the same chunks with its header
	/// fields laid out differently, so it builds the header itself and calls in here.
	/// </summary>
	internal void Decode( TextureFileData fileData )
	{
		//
		// Decompress blocks
		//
		var alphaData = Array.Empty<byte>();
		var colorData = Decompress( fileData.ColorChunk );

		if ( fileData.HasAlphaChannel )
			alphaData = Decompress( fileData.AlphaChunk );

		//
		// Decode image
		//
		var size = (int)Math.Max( GetAlignedSize( (uint)Math.Max( fileData.Height, fileData.Width ) ), 8 );

		// Type 0x12 (a sign's artwork too) is the original's zlib codec; type 0x13, LZSS, is decoded below.
		if ( !fileData.Flags.HasFlag( TextureFlags.FullScale ) )
		{
			Data = new TextureData( fileData.Width, fileData.Height, DecodeZlibCodec( fileData, colorData, alphaData, size ) );
			IsValid = true;
			return;
		}

		var outputY = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputCb = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputCr = Enumerable.Repeat( 0f, size * size ).ToList();
		var outputA = Enumerable.Repeat( 255f, size * size ).ToList();

		var state = new ImageDecodeState( size );
		DecodeChannel( ref state, size, ref colorData, ref outputY, fileData.YMultiplier );
		DecodeChannel( ref state, size / 2, ref colorData, ref outputCb, fileData.CbMultiplier );
		DecodeChannel( ref state, size / 2, ref colorData, ref outputCr, fileData.CrMultiplier );

		if ( fileData.HasAlphaChannel )
			DecodeChannel( ref state, size, ref alphaData, ref outputA, fileData.AMultiplier );

		uint alignedWidth = GetAlignedSize( (uint)fileData.Width );
		uint alignedHeight = GetAlignedSize( (uint)fileData.Height );

		var outputBytes = Enumerable.Repeat( 0f, fileData.Width * fileData.Height * 4 ).ToList();

		for ( int y = 0; y < fileData.Height; y++ )
		{
			for ( int x = 0; x < fileData.Width; x++ )
			{
				// Convert from ycbcr to rgb
				float cy = outputY[y * size + x];
				float cb = outputCb[((y / 2) * (size / 2) + (x / 2))];
				float cr = outputCr[((y / 2) * (size / 2) + (x / 2))];

				float r = cy + 1.402f * (cr);
				float g = cy - 0.344136f * (cb) - 0.714136f * (cr);
				float b = cy + 1.772f * (cb);

				// Alpha channel remains untouched..
				float a = outputA[y * size + x];

				outputBytes[((y * fileData.Width + x) * 4)] = r;
				outputBytes[((y * fileData.Width + x) * 4) + 1] = g;
				outputBytes[((y * fileData.Width + x) * 4) + 2] = b;
				outputBytes[((y * fileData.Width + x) * 4) + 3] = a;
			}
		}

		// Each channel is cut to a byte by truncation. The original (FUN_00595920) indexes its channel tables by
		// the channel's ROUNDED value (docs/exe/textures.md, "A channel is cut to its top bits"), so this is
		// up to one level low, on average 0.48 a channel; queued as Q724.
		List<byte> textureData = Enumerable.Repeat( (byte)0, fileData.Width * fileData.Height * 4 ).ToList();
		for ( int i = 0; i < outputBytes.Count; i++ )
		{
			textureData[i] = (byte)outputBytes[i].Clamp( 0, 255 );
		}

		Data = new TextureData( fileData.Width, fileData.Height, textureData.ToArray() );
		IsValid = true;
	}

	private static byte[] Decompress( byte[] data )
	{
		// Check if the file contains 'BILZ' as the first 4 bytes - if so then it is a zlib
		// compressed chunk
		if ( data[0] == 0x42 && data[1] == 0x49 && data[2] == 0x4C && data[3] == 0x5A )
			return DecompressZLIB( data );

		// Not zlib, so must be data compressed with LZSS
		return DecompressLZSS( data );
	}

	private static byte[] DecompressZLIB( byte[] data )
	{
		/*
		 * Theme Park World uses ZLIB 1.1.3
		 * We can therefore deduce that the custom ZLIB header is as follows:
		 * - 4 bytes: "BILZ" magic number
		 * - 4 bytes: decompressed size
		 * - 4 bytes: stream size
		 * - 4 bytes: Constant - MAX_WBITS (15) - from zlib
		 * - 4 bytes: Constant - DEF_MEM_LEVEL (9) - from zlib
		 * - 4 bytes: Unused
		 * - 4 bytes: Unused 
		 */

		// We don't need to use any of this data - so we'll just skip it entirely
		data = data[0x1C..]; // Unused data

		const int maxSize = 256 * 256 * 3 + 128 * 128 * 3 + 128 * 128 * 3 + 256 * 256 * 3;
		using var outputStream = new MemoryStream( maxSize );
		using var outputWriter = new BinaryWriter( outputStream );
		{
			using var inputStream = new MemoryStream( data );
			using var compressed = new InflaterInputStream( inputStream );
			compressed.CopyTo( outputStream );
		}

		return outputStream.ToArray();
	}

	private static byte[] DecompressLZSS( byte[] data )
	{
		/*					Y (full)      + Cb (half)     + Cr (half)     + Alpha (full) */
		const int maxSize = 256 * 256 * 3 + 128 * 128 * 3 + 128 * 128 * 3 + 256 * 256 * 3;
		using var outputStream = new MemoryStream( maxSize );
		using var outputWriter = new BinaryWriter( outputStream );

		var blockReader = new BitReader( data );
		if ( !LZSS.Decompress( blockReader, outputWriter ) )
		{
			throw new Exception( "LZSS failed to decompress" );
		}

		return outputStream.ToArray();
	}
}
