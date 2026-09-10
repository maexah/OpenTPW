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
/// A font record holds a display name in a fixed 64-byte field, then the TrueType file name in a
/// 260-byte one, two 4-byte fields, and a 60-byte Windows LOGFONTA - which is why the name appears
/// to occur a second time part-way through, at the LOGFONT's own lfFaceName. The original letters
/// its signs with GDI, so it stores a LOGFONT rather than a size and a weight of its own.
///
/// The ink is not in the font record. Each line's colour is four bytes in a block that follows
/// both records - see ReadLineColour - and an earlier reading of this format took three floats
/// from past the end of the LOGFONT as a colour instead. Those floats are real data belonging to
/// whatever the engine reads next, and they are not a colour: taking them as one lettered the
/// Fantasy park's board in the same pale mint the board itself is painted, which left its name
/// invisible.
/// </summary>
public sealed class SignFile : BaseFormat
{
	public const int Size = 256;

	private const int FontRecordOffset = 0x0011;
	private const int FontRecordStride = 436;
	private const int FontRecordCount = 2;
	private const int TtfNameOffset = 0x40;
	private const int NameFieldLength = 64;

	/// <summary>
	/// Where the per-line ink blocks start - immediately after the second font record, which is
	/// exactly where the engine's reader gets to when it has finished with them.
	/// </summary>
	private const int LineBlockOffset = FontRecordOffset + (FontRecordCount * FontRecordStride);

	/// <summary>Four colour bytes plus four unidentified 4-byte fields.</summary>
	private const int LineBlockStride = 0x14;

	/// <summary>
	/// The two line modes, in the seventeen bytes of header the engine reads before the font
	/// records - see <see cref="ReadLineMode"/>. A zero there means the line is absent entirely
	/// and its ink block is not written at all, which no shipped sign does.
	/// </summary>
	private static readonly int[] LineModeOffsets = [0x09, 0x0D];
	private const int ImageHeaderOffset = 0x43C5;
	private const int ChunkSizesOffset = 0x43D5;
	private const int ColorChunkOffset = 0x43DD;

	/// <param name="Colour">
	/// The colour this line is lettered in, and how strongly it is laid over the board. Both come
	/// from the line's own four bytes - see <see cref="LineBlockOffset"/> - not from the font
	/// record.
	/// </param>
	public sealed record Font( string Name, string FileName, LineColour Colour );

	/// <summary>
	/// One line's ink: a colour and the strength it is blended at, 0..1 each.
	///
	/// The engine composites the glyph mask over the board with this, so the opacity is a real
	/// blend and not a threshold - see FUN_005e7f60, which walks the glyph coverage and moves each
	/// board pixel that fraction of the way toward this colour.
	/// </summary>
	public readonly record struct LineColour( float R, float G, float B, float Opacity );

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

		// Both lines drawn as one takes its ink from the first line only - see LineModeOffsets.
		var merged = ReadLineMode( data, 0 ) == 2 && ReadLineMode( data, 1 ) == 2;

		for ( int i = 0; i < FontRecordCount; ++i )
		{
			var record = FontRecordOffset + (i * FontRecordStride);

			if ( record + FontRecordStride > data.Length )
				break;

			var name = ReadFixedString( data, record );

			if ( string.IsNullOrEmpty( name ) )
				continue;

			fonts.Add( new Font(
				name,
				ReadFixedString( data, record + TtfNameOffset ),
				ReadLineColour( data, merged ? 0 : i ) ) );
		}

		return [.. fonts];
	}

	/// <summary>
	/// How this line is laid down, from the two 4-byte fields near the start of the file - 1 for
	/// each line inked on its own, 2 for the two masks merged and inked together.
	///
	/// It matters only because of what the merged case does with colour: the engine maxes the two
	/// glyph masks into one surface and then runs a single colour over the result, so the second
	/// line's own four bytes are never reached and both words come out in the first line's ink.
	/// Lost Kingdom and Space Zone are the two that do this; Wonder Land and Halloween ink each
	/// line separately. A file where the two disagree would take a third path that nothing in the
	/// shipped data exercises.
	/// </summary>
	private static int ReadLineMode( byte[] data, int line )
	{
		var offset = LineModeOffsets[line];

		return offset + 4 <= data.Length ? BitConverter.ToInt32( data, offset ) : 1;
	}

	/// <summary>
	/// The line's ink, from the block the engine reads straight after the two font records.
	///
	/// Each line contributes twenty bytes there: the four below, then four more fields that are
	/// not yet identified. The engine loads the four singly into consecutive bytes of its sign
	/// object (FUN_005ec3a0) and hands them to the compositor in the order alpha, then the three
	/// channels (FUN_005ecb40); the board those land in is packed ARGB4444 further down that same
	/// function, which is what fixes the order as red, green, blue, opacity rather than any other
	/// reading of the same four bytes.
	/// </summary>
	private static LineColour ReadLineColour( byte[] data, int line )
	{
		var offset = LineBlockOffset + (line * LineBlockStride);

		if ( offset + 4 > data.Length )
			return new LineColour( 1f, 1f, 1f, 1f );

		return new LineColour(
			data[offset + 0] / 255f,
			data[offset + 1] / 255f,
			data[offset + 2] / 255f,
			data[offset + 3] / 255f );
	}

	private static string ReadFixedString( byte[] data, int offset )
	{
		var length = 0;
		while ( length < NameFieldLength && data[offset + length] != 0 )
			++length;

		return Encoding.ASCII.GetString( data, offset, length );
	}
}
