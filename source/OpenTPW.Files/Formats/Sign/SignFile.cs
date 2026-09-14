using System.Text;

namespace OpenTPW;

/// <summary>
/// A .sgn sign - the lettering, and sometimes the artwork, for one name board.
///
/// <para>
/// The lobby's four park boards and the name boards on the rides inside a park are the same thing.
/// The lobby's sit in lobby.wad's terrain directory, named after the model that carries the sign
/// meshes rather than after the park - the jungle and hallow signs are on the island (Jun_isle.sgn,
/// Hal_isle.sgn) while fantasy and space are on the gate (Fan_gate.sgn, Spa_gate.sgn) - and a ride's
/// sits beside its model under the ride's own name.
/// </para>
///
/// <para>
/// <b>Almost nothing in the file is at a fixed offset.</b> The engine reads it strictly in order
/// (FUN_005ec3a0) and three separate things change where the rest of it lands, so it has to be
/// walked rather than indexed:
/// </para>
///
/// <code>
///   0x0000  int32   version - the engine refuses anything below 100
///   0x0004  int32   which of the two lines is inked first
///   0x0008  byte    whether artwork follows, or the board is left bare
///   0x0009  int32   line 0's mode
///   0x000D  int32   line 1's mode
///   0x0011  font record 0, 436 bytes
///   0x01C5  font record 1, 436 bytes
///           line 0's ink, 20 bytes - present only when line 0's mode is non-zero
///           line 1's ink, 20 bytes - present only when line 1's mode is non-zero
///           line 0's fill texture - int32 width, height, bytes per pixel, then that many pixels
///           line 1's fill texture - the same again
///           the artwork - present only when the byte at 0x0008 says so
/// </code>
///
/// <para>
/// Walking it that way lands exactly on the end of every one of the 84 signs the game holds: for the
/// 23 that carry artwork the computed offset hits the <c>BILZ</c> magic 23 times out of 23, and for
/// the 61 that do not it lands precisely on end-of-file 61 times out of 61. The two ink blocks are
/// what made the header look like it had two fixed sizes - three signs (jelly, zob and C_SCAT) set
/// both modes to zero, so both blocks are absent and everything after them shifts down the 40 bytes
/// that 17337 and 17297 differ by.
/// </para>
///
/// <para>
/// <b>Most signs have no artwork at all.</b> 61 of the 84 leave the byte at 0x0008 clear, and for
/// those the engine clears the board to transparent black and skips the artwork entirely
/// (0x005ecd09, 0x005ecd18) - the ride's name is left floating on the model with the ride showing
/// through behind it. That is not a broken or truncated file, and reading one has to succeed with no
/// image rather than fail, which is what an earlier reading of this format got wrong: it demanded a
/// 17373-byte header before ever checking whether an image was declared, and so refused every ride
/// sign in the game that was not one of the few carrying a painted board.
/// </para>
///
/// <para>
/// The artwork itself is a .wct image body with its header fields rearranged - a width, a height, a
/// type, four dequantisation floats and the two chunk sizes, then the same BILZ-wrapped zlib streams
/// handed to <see cref="TextureFile"/> unchanged. Every shipped sign states 256x128 and type 4.
/// <b>The stated height is the artwork's, not the buffer's:</b> the chunks are encoded at the
/// wavelet's aligned 256x256, which is why the colour chunk inflates to 98304 bytes and the alpha
/// chunk to 65536. The colour figure cannot tell the two readings apart - 256*128*3 and
/// 256*256 + 2*128*128 are both 98304 - but the alpha figure can, and 65536 is 256*256 rather than
/// the 256*128 a full-resolution 256x128 image would need. So the image is decoded at 256x256 and
/// only its top 128 rows carry the board; see <c>SignTexture</c>, which takes exactly those.
/// </para>
///
/// <para>
/// A font record holds a display name in a fixed 64-byte field, then the TrueType file name in a
/// 260-byte one, two 4-byte fields, and a 60-byte Windows LOGFONTA - which is why the name appears
/// to occur a second time part-way through, at the LOGFONT's own lfFaceName. The original letters
/// its signs with GDI, so it stores a LOGFONT rather than a size and a weight of its own.
/// </para>
///
/// <para>
/// The two fill textures are skipped here but they are real data, not padding: each is one line's
/// 16x128 fill pattern, which the engine resamples to the text's height and tiles across the glyph
/// mask (FUN_005eb8a0), and many of them are pure vertical ramps. Lettering a line in the flat colour
/// from its ink block, as this does, is an approximation of that gradient.
/// </para>
/// </summary>
public sealed class SignFile : BaseFormat
{
	/// <summary>
	/// The size the artwork's chunks are encoded at, which is not the size the file states - see the
	/// class remarks. Only the top <see cref="ArtworkHeight"/> rows of the decoded image are board.
	/// </summary>
	public const int Size = 256;

	/// <summary>How much of the decoded image is actually artwork, as the file itself states it.</summary>
	public const int ArtworkHeight = 128;

	private const int FontRecordOffset = 0x0011;
	private const int FontRecordStride = 436;
	private const int FontRecordCount = 2;
	private const int TtfNameOffset = 0x40;
	private const int NameFieldLength = 64;

	/// <summary>Four colour bytes plus four fields describing the line's outline and drop shadow.</summary>
	private const int LineBlockStride = 0x14;

	/// <summary>One fill texture per line, each stating its own dimensions before its pixels.</summary>
	private const int FillTextureCount = 2;
	private const int FillTextureHeaderSize = 12;

	/// <summary>The engine gives up on anything older than this - see 0x005ec3d2.</summary>
	private const int MinimumVersion = 100;

	private const int ArtworkFlagOffset = 0x08;
	private static readonly int[] LineModeOffsets = [0x09, 0x0D];

	/// <summary>Width, height and type, four dequantisation floats, then the two chunk sizes.</summary>
	private const int ArtworkHeaderSize = 36;

	/// <summary>The only artwork type any shipped sign uses: a colour chunk and an alpha chunk.</summary>
	private const int ChunkedArtworkType = 4;

	/// <param name="Colour">
	/// The colour this line is lettered in, and how strongly it is laid over the board. Both come
	/// from the line's own ink block, not from the font record.
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

	/// <summary>
	/// The sign artwork, <see cref="Size"/> square, or null when this sign declares none - which
	/// most do. A null image is not a failure: check <see cref="IsValid"/> for that.
	/// </summary>
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

		var afterFontRecords = FontRecordOffset + (FontRecordCount * FontRecordStride);

		if ( data.Length < afterFontRecords )
		{
			Log.Warning( $"Sign file is {data.Length} bytes, too short to hold its two font records" );
			return;
		}

		var version = BitConverter.ToInt32( data, 0 );

		if ( version < MinimumVersion )
		{
			Log.Warning( $"Sign file is version {version}, which the engine refuses below {MinimumVersion}" );
			return;
		}

		var offset = afterFontRecords;
		var ink = new int?[FontRecordCount];

		// A line whose mode is zero has no ink block written at all, so the next line's block - or
		// the first fill texture - takes its place rather than sitting where a full file would put it.
		for ( int line = 0; line < FontRecordCount; ++line )
		{
			if ( ReadLineMode( data, line ) == 0 )
				continue;

			if ( offset + LineBlockStride > data.Length )
			{
				Log.Warning( $"Sign file runs out at {data.Length} bytes, part way through line {line}'s ink" );
				return;
			}

			ink[line] = offset;
			offset += LineBlockStride;
		}

		for ( int fill = 0; fill < FillTextureCount; ++fill )
		{
			if ( !TrySkipFillTexture( data, ref offset ) )
			{
				Log.Warning( $"Sign file's fill texture {fill} does not fit in {data.Length} bytes" );
				return;
			}
		}

		Fonts = ReadFonts( data, ink );

		// Everything the sign needs to letter a name has now been read, so a sign with no artwork is
		// finished and valid here - the board is simply left transparent for the name to float on.
		if ( data[ArtworkFlagOffset] != 0 )
			Image = ReadArtwork( data, offset );

		IsValid = true;
	}

	/// <summary>
	/// Steps over one line's fill texture, which states its own width, height and bytes per pixel
	/// before its pixels. Every shipped one is 16x128x4, but the engine multiplies the three rather
	/// than assuming them (0x005e791b), so this does too - guessing would walk off the end of a file
	/// that did anything else.
	/// </summary>
	private static bool TrySkipFillTexture( byte[] data, ref int offset )
	{
		if ( offset + FillTextureHeaderSize > data.Length )
			return false;

		var width = BitConverter.ToInt32( data, offset );
		var height = BitConverter.ToInt32( data, offset + 4 );
		var bytesPerPixel = BitConverter.ToInt32( data, offset + 8 );

		if ( width <= 0 || height <= 0 || bytesPerPixel <= 0 )
			return false;

		var pixels = (long)width * height * bytesPerPixel;

		if ( offset + FillTextureHeaderSize + pixels > data.Length )
			return false;

		offset += FillTextureHeaderSize + (int)pixels;

		return true;
	}

	/// <summary>
	/// Reads the artwork that follows the text blocks, or answers null and says why. A sign that
	/// declares artwork it cannot deliver is still usable for its lettering, so this never
	/// invalidates the sign.
	/// </summary>
	private static TextureData? ReadArtwork( byte[] data, int offset )
	{
		if ( offset + ArtworkHeaderSize > data.Length )
		{
			Log.Warning( $"Sign file declares artwork but has only {data.Length - offset} bytes left for its header" );
			return null;
		}

		var width = BitConverter.ToInt32( data, offset );
		var height = BitConverter.ToInt32( data, offset + 4 );
		var type = BitConverter.ToInt32( data, offset + 8 );

		if ( type != ChunkedArtworkType )
		{
			Log.Warning( $"Sign artwork is type {type}, and only {ChunkedArtworkType} is understood" );
			return null;
		}

		// Decoded at the size the chunks are actually encoded at rather than the size stated here -
		// see the class remarks - so a sign that states anything else would decode wrongly.
		if ( width != Size || height != ArtworkHeight )
			Log.Warning( $"Sign artwork states {width}x{height} rather than {Size}x{ArtworkHeight}, so it may decode wrongly" );

		var quantisation = offset + 12;
		var colorChunkSize = BitConverter.ToInt32( data, offset + 28 );
		var alphaChunkSize = BitConverter.ToInt32( data, offset + 32 );
		var body = offset + ArtworkHeaderSize;

		if ( colorChunkSize <= 0 || alphaChunkSize < 0
			|| body + (long)colorChunkSize + alphaChunkSize > data.Length )
		{
			Log.Warning( $"Sign artwork chunk sizes ({colorChunkSize}, {alphaChunkSize}) do not fit in {data.Length} bytes" );
			return null;
		}

		var fileData = new TextureFile.TextureFileData()
		{
			// Left half-scale because that is how the chunks are encoded: the colour chunk's 98304
			// bytes are a full-size 256x256 luma plus two half-size chroma planes.
			Flags = TextureFile.TextureFlags.None,
			HasAlphaChannel = true,
			BitsPerPixel = 32,
			Version = 4,

			Width = Size,
			Height = Size,

			YChannelQuantizationScale = (short)BitConverter.ToSingle( data, quantisation ),
			CbChannelQuantizationScale = (short)BitConverter.ToSingle( data, quantisation + 4 ),
			CrChannelQuantizationScale = (short)BitConverter.ToSingle( data, quantisation + 8 ),
			AChannelQuantizationScale = (short)BitConverter.ToSingle( data, quantisation + 12 ),

			ColorChunkSize = colorChunkSize,
			AlphaChunkSize = alphaChunkSize,

			ColorChunk = data[body..(body + colorChunkSize)],
			AlphaChunk = data[(body + colorChunkSize)..(body + colorChunkSize + alphaChunkSize)]
		};

		return new TextureFile( fileData ).Data;
	}

	private static Font[] ReadFonts( byte[] data, int?[] ink )
	{
		var fonts = new List<Font>();

		// Both lines drawn as one takes its ink from the first line only - see ReadLineMode.
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
				ReadLineColour( data, ink[merged ? 0 : i] ) ) );
		}

		return [.. fonts];
	}

	/// <summary>
	/// How this line is laid down, from the two 4-byte fields near the start of the file - 1 for
	/// each line inked on its own, 2 for the two masks merged and inked together, and 0 for a line
	/// that is not lettered at all.
	///
	/// It matters twice over. For colour: the merged case maxes the two glyph masks into one surface
	/// and then runs a single colour over the result, so the second line's own bytes are never
	/// reached and both words come out in the first line's ink - Lost Kingdom and Space Zone do
	/// this, while Wonder Land and Halloween ink each line separately. For layout: a zero means the
	/// line's twenty-byte ink block was never written, so everything after it moves up, which is the
	/// whole reason this file has to be walked rather than indexed.
	/// </summary>
	private static int ReadLineMode( byte[] data, int line )
	{
		var offset = LineModeOffsets[line];

		return offset + 4 <= data.Length ? BitConverter.ToInt32( data, offset ) : 1;
	}

	/// <summary>
	/// The line's ink, from its own block between the font records and the fill textures.
	///
	/// <para>
	/// Each line contributes twenty bytes there: the four below, then four fields describing the
	/// outline and drop shadow the engine grows around the glyphs. The engine loads the four singly
	/// into consecutive bytes of its sign object (FUN_005ec3a0) and hands them to the compositor in
	/// the order alpha, then the three channels (FUN_005ecb40); the board those land in is packed
	/// ARGB4444 further down that same function, which is what fixes the order as red, green, blue,
	/// opacity rather than any other reading of the same four bytes.
	/// </para>
	///
	/// <para>
	/// A line with no block at all is given no ink, which is what the engine does with it too - it
	/// composites that line out of a field it never wrote, so nothing appears. Three shipped signs
	/// are in that state, and two of them carry artwork with the name already painted in.
	/// </para>
	/// </summary>
	private static LineColour ReadLineColour( byte[] data, int? block )
	{
		if ( block is not { } offset || offset + 4 > data.Length )
			return new LineColour( 1f, 1f, 1f, 0f );

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
