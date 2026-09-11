namespace OpenTPW.UI;

/// <summary>
/// A .bf4 bitmap font out of the language folder - the lettering on every button, label and dialog
/// in the interface.
///
/// <para>
/// <b>The file.</b> "F4FB", a version byte, the line height in pixels, a 16-bit glyph count, and then
/// one 32-bit offset per glyph. The loader checks nothing but the magic (0x006b0680). Each glyph is
/// </para>
/// <code>
///     u32 character   u32 bytes stored   u32 bytes unpacked   u32 packing
///     u16 width       u16 height         s8 left   s8 top     u16 advance
/// </code>
/// <para>
/// followed by its stored bytes, padded to four. Left and top place the glyph from the pen and from
/// the top of the line, and the advance is how far the pen moves. The characters are Unicode - the
/// fonts reach U+2019 for the apostrophe - and are not in any order. Checked against every glyph of
/// TITLEMED, TITLEBIG, MENUBIG, SESHBIG, GAME8, GAME9, GAME8AA and GAME10AA: the stored size always
/// accounts for the record exactly, and the unpacked size is always half the pixel count, rounded up.
/// </para>
/// <para>
/// <b>The pixels.</b> Four bits of coverage each, high nibble first, row after row with nothing
/// between the rows. How they are stored is the packing (0x006b4aa0): 0 is the nibbles as they are;
/// 1 is run-length coded, where a nibble other than zero stands for itself, a zero is followed by a
/// count and a value to repeat, and a zero count ends the glyph; 2 is one bit a pixel, high bit
/// first, a set bit being full coverage. The anti-aliased fonts use the first two, the plain ones
/// the last. The blitter (0x006b15f0) moves what is underneath towards the text's colour by the
/// coverage over fifteen.
/// </para>
/// </summary>
internal sealed class BitmapFont
{
	private const uint Magic = 0x42463446; // "F4FB"

	private const string Folder = "Language/English";

	private static readonly Dictionary<string, BitmapFont?> Loaded = new( StringComparer.OrdinalIgnoreCase );

	private readonly Dictionary<int, Glyph> _glyphs = new();

	public string Name { get; }

	/// <summary>How tall a line is, in the font's own pixels.</summary>
	public int LineHeight { get; }

	/// <param name="Coverage">One byte a pixel, 0 to 15, a row at a time.</param>
	private sealed record Glyph( int Width, int Height, int Left, int Top, int Advance, byte[] Coverage );

	/// <summary>A font by file name - "TITLEBIG.bf4" - loaded once. Null, and a warning, if it will not load.</summary>
	public static BitmapFont? Get( string fileName )
	{
		if ( Loaded.TryGetValue( fileName, out var font ) )
			return font;

		try
		{
			font = new BitmapFont( fileName, FileSystem.ReadAllBytes( $"{Folder}/{fileName}" ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"UI: font '{fileName}' would not load - {e.Message}" );
		}

		Loaded[fileName] = font;
		return font;
	}

	private BitmapFont( string name, byte[] data )
	{
		Name = name;

		if ( data.Length < 8 || BitConverter.ToUInt32( data, 0 ) != Magic )
			throw new InvalidDataException( "not a .bf4 font" );

		LineHeight = data[5];
		var count = BitConverter.ToUInt16( data, 6 );

		for ( int i = 0; i < count; ++i )
		{
			var at = (int)BitConverter.ToUInt32( data, 8 + (i * 4) );

			if ( at + 24 > data.Length )
				continue;

			var character = (int)BitConverter.ToUInt32( data, at );
			var stored = (int)BitConverter.ToUInt32( data, at + 4 );
			var packing = BitConverter.ToUInt32( data, at + 12 );
			var width = BitConverter.ToUInt16( data, at + 16 );
			var height = BitConverter.ToUInt16( data, at + 18 );

			var coverage = Unpack( data, at + 24, Math.Min( stored, data.Length - (at + 24) ), width * height, packing );

			_glyphs[character] = new Glyph( width, height, (sbyte)data[at + 20], (sbyte)data[at + 21],
				BitConverter.ToUInt16( data, at + 22 ), coverage );
		}
	}

	private static byte[] Unpack( byte[] data, int start, int stored, int pixels, uint packing )
	{
		var coverage = new byte[pixels];

		if ( packing == 2 )
		{
			for ( int pixel = 0; pixel < pixels && (pixel >> 3) < stored; ++pixel )
				coverage[pixel] = (byte)(((data[start + (pixel >> 3)] >> (7 - (pixel & 7))) & 1) * 15);

			return coverage;
		}

		var nibble = 0;
		var available = stored * 2;

		int Next()
		{
			if ( nibble >= available )
				return -1;

			var value = data[start + (nibble >> 1)];
			var result = (nibble & 1) == 0 ? value >> 4 : value & 0xF;
			++nibble;
			return result;
		}

		if ( packing != 1 )
		{
			for ( int pixel = 0; pixel < pixels; ++pixel )
				coverage[pixel] = (byte)Math.Max( Next(), 0 );

			return coverage;
		}

		var written = 0;

		while ( written < pixels )
		{
			var value = Next();

			if ( value < 0 )
				break;

			if ( value != 0 )
			{
				coverage[written++] = (byte)value;
				continue;
			}

			var run = Next();

			if ( run <= 0 )
				break;

			var repeated = Math.Max( Next(), 0 );

			for ( int i = 0; i < run && written < pixels; ++i )
				coverage[written++] = (byte)repeated;
		}

		return coverage;
	}

	/// <summary>How far the pen moves over <paramref name="text"/>, in the font's pixels.</summary>
	public int Measure( ReadOnlySpan<char> text )
	{
		var width = 0;

		foreach ( var character in text )
			width += Find( character )?.Advance ?? 0;

		return width;
	}

	/// <summary>
	/// Writes one line into an RGBA buffer, the top-left of the line at (<paramref name="x"/>,
	/// <paramref name="y"/>), laid over what is already there with <paramref name="opacity"/> of its
	/// coverage.
	/// </summary>
	public void Write( ReadOnlySpan<char> text, byte[] pixels, int bufferWidth, int bufferHeight, int x, int y,
		UiColour colour, float opacity )
	{
		var pen = x;

		foreach ( var character in text )
		{
			if ( Find( character ) is not { } glyph )
				continue;

			for ( int row = 0; row < glyph.Height; ++row )
			{
				var targetY = y + glyph.Top + row;

				if ( targetY < 0 || targetY >= bufferHeight )
					continue;

				for ( int column = 0; column < glyph.Width; ++column )
				{
					var targetX = pen + glyph.Left + column;
					var cover = glyph.Coverage[(row * glyph.Width) + column];

					if ( cover == 0 || targetX < 0 || targetX >= bufferWidth )
						continue;

					Over( pixels, ((targetY * bufferWidth) + targetX) * 4, colour, cover / 15f * opacity );
				}
			}

			pen += glyph.Advance;
		}
	}

	/// <summary>Straight-alpha "over": the text's colour at <paramref name="alpha"/> on top of the pixel.</summary>
	private static void Over( byte[] pixels, int index, UiColour colour, float alpha )
	{
		var below = pixels[index + 3] / 255f;
		var result = alpha + (below * (1f - alpha));

		if ( result <= 0f )
			return;

		byte Mix( byte source, byte under ) => (byte)(((source * alpha) + (under * below * (1f - alpha))) / result);

		pixels[index] = Mix( colour.R, pixels[index] );
		pixels[index + 1] = Mix( colour.G, pixels[index + 1] );
		pixels[index + 2] = Mix( colour.B, pixels[index + 2] );
		pixels[index + 3] = (byte)(result * 255f);
	}

	/// <summary>The glyph for a character, or for '?' when the font has none - null only if it has neither.</summary>
	private Glyph? Find( char character )
		=> _glyphs.TryGetValue( character, out var glyph ) ? glyph
			: _glyphs.TryGetValue( '?', out var fallback ) ? fallback
			: null;
}
