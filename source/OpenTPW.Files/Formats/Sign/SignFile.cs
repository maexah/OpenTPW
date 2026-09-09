using System.Text;

namespace OpenTPW;

/// <summary>
/// A .sgn park sign - the artwork and lettering for one park's name board.
///
/// Each park has exactly one, sitting in lobby.wad's terrain directory next to whichever model
/// carries the sign meshes, and named after that model rather than after the park: the jungle
/// and hallow signs are on the island (Jun_isle.sgn, Hal_isle.sgn) while fantasy and space are
/// on the gate (Fan_gate.sgn, Spa_gate.sgn). The same format is used by the placeable in-park
/// sign feature (levels/jungle/features/sign1.wad), so this is not lobby-specific.
///
/// The header is a fixed 0x43DD bytes in all five files that ship, laid out as:
///
///   0x0011  font record 0, 436 bytes
///   0x01C5  font record 1, 436 bytes
///   0x03C5  a 64x64 BGRA thumbnail, uncompressed (16384 bytes)
///   0x43C5  four floats - the Y, Cb, Cr and alpha dequantisation scales
///   0x43D5  int32 colour chunk size, int32 alpha chunk size
///   0x43DD  the colour chunk, then the alpha chunk
///
/// Those last three rows are a .wct image body with its header fields rearranged: the chunks are
/// the same BILZ-wrapped zlib streams, the colour one holding Y at full size plus Cb and Cr at
/// half, and they are handed to <see cref="TextureFile"/> unchanged. The image is always 256x256
/// - the colour chunk inflates to 98304 bytes in every park, which is exactly 256*256 + 2*128*128,
/// and the alpha chunk to 256*256.
///
/// A font record holds a display name and the TrueType file it came from, both in fixed 64-byte
/// fields, the name again at +0x168, and eight floats at +0x18C. Three of those eight always land
/// in 0..1 and read as a colour (the jungle's first is 0.40, 0.87, 0.31 - a green), but nothing
/// here depends on that reading, so they are exposed raw rather than interpreted.
/// </summary>
public sealed class SignFile : BaseFormat
{
	public const int Size = 256;

	private const int FontRecordOffset = 0x0011;
	private const int FontRecordStride = 436;
	private const int FontRecordCount = 2;
	private const int TtfNameOffset = 0x40;
	private const int ParametersOffset = 0x18C;
	private const int ParameterCount = 8;
	private const int NameFieldLength = 64;
	private const int ImageHeaderOffset = 0x43C5;
	private const int ChunkSizesOffset = 0x43D5;
	private const int ColorChunkOffset = 0x43DD;

	/// <param name="Parameters">
	/// Eight floats whose meaning is only partly established. Three of them are the text colour -
	/// see <see cref="Colour"/> - and the rest are exposed raw rather than guessed at.
	/// </param>
	public sealed record Font( string Name, string FileName, float[] Parameters )
	{
		/// <summary>
		/// The colour this line is lettered in. Parameters 2, 3 and 4 are the only ones that
		/// always land in 0..1, and they differ per park in ways that suit each board - the
		/// jungle's first line is a green (0.40, 0.87, 0.31) over brown bark.
		/// </summary>
		public (float R, float G, float B) Colour => (Parameters[2], Parameters[3], Parameters[4]);
	}

	/// <summary>False when the file was missing or did not match the layout - never throws.</summary>
	public bool IsValid { get; private set; }

	public IReadOnlyList<Font> Fonts { get; private set; } = Array.Empty<Font>();

	/// <summary>The sign artwork, 256x256 RGBA, or null when <see cref="IsValid"/> is false.</summary>
	public TextureData? Image { get; private set; }

	public SignFile( string path )
	{
		ReadFromFile( path );
	}

	public SignFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromFile( string path )
	{
		// FileSystem.FileExists is archive-blind and OpenRead returns null for a missing entry,
		// so the null check here is the only thing standing between a park without a sign and a
		// crash during scene setup. A miss is left unlogged because callers try more than one
		// candidate name - see LobbyIsland - and only they know when it was the last one.
		using var stream = FileSystem.OpenRead( path );

		if ( stream == null )
			return;

		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var memoryStream = new MemoryStream();
		stream.CopyTo( memoryStream );
		var data = memoryStream.ToArray();

		if ( data.Length <= ColorChunkOffset )
		{
			Log.Warning( $"Sign file is {data.Length} bytes, too short to hold a {ColorChunkOffset}-byte header" );
			return;
		}

		var colorChunkSize = BitConverter.ToInt32( data, ChunkSizesOffset );
		var alphaChunkSize = BitConverter.ToInt32( data, ChunkSizesOffset + 4 );

		if ( colorChunkSize <= 0 || alphaChunkSize < 0
			|| ColorChunkOffset + (long)colorChunkSize + alphaChunkSize > data.Length )
		{
			Log.Warning( $"Sign file chunk sizes ({colorChunkSize}, {alphaChunkSize}) do not fit in {data.Length} bytes" );
			return;
		}

		Fonts = ReadFonts( data );

		var fileData = new TextureFile.TextureFileData()
		{
			// Every .wct beside these signs sets FullScale, and the sign decodes to a full-size
			// 256x256 image rather than a half-scale one, so it is set here too.
			Flags = TextureFile.TextureFlags.None,
			HasAlphaChannel = true,
			BitsPerPixel = 32,
			Version = 4,

			Width = Size,
			Height = Size,

			YChannelQuantizationScale = (short)BitConverter.ToSingle( data, ImageHeaderOffset ),
			CbChannelQuantizationScale = (short)BitConverter.ToSingle( data, ImageHeaderOffset + 4 ),
			CrChannelQuantizationScale = (short)BitConverter.ToSingle( data, ImageHeaderOffset + 8 ),
			AChannelQuantizationScale = (short)BitConverter.ToSingle( data, ImageHeaderOffset + 12 ),

			ColorChunkSize = colorChunkSize,
			AlphaChunkSize = alphaChunkSize,

			ColorChunk = data[ColorChunkOffset..(ColorChunkOffset + colorChunkSize)],
			AlphaChunk = data[(ColorChunkOffset + colorChunkSize)..(ColorChunkOffset + colorChunkSize + alphaChunkSize)]
		};

		Image = new TextureFile( fileData ).Data;
		IsValid = true;
	}

	private static Font[] ReadFonts( byte[] data )
	{
		var fonts = new List<Font>();

		for ( int i = 0; i < FontRecordCount; ++i )
		{
			var record = FontRecordOffset + (i * FontRecordStride);

			if ( record + ParametersOffset + (ParameterCount * 4) > data.Length )
				break;

			var name = ReadFixedString( data, record );

			if ( string.IsNullOrEmpty( name ) )
				continue;

			var parameters = new float[ParameterCount];
			for ( int p = 0; p < ParameterCount; ++p )
				parameters[p] = BitConverter.ToSingle( data, record + ParametersOffset + (p * 4) );

			fonts.Add( new Font( name, ReadFixedString( data, record + TtfNameOffset ), parameters ) );
		}

		return [.. fonts];
	}

	private static string ReadFixedString( byte[] data, int offset )
	{
		var length = 0;
		while ( length < NameFieldLength && data[offset + length] != 0 )
			++length;

		return Encoding.ASCII.GetString( data, offset, length );
	}
}
