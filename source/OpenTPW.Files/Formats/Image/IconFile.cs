namespace OpenTPW;

/// <summary>One image out of an icon file, as straight RGBA with the top row first.</summary>
public record struct IconImage( int Width, int Height, int BitsPerPixel, byte[] Pixels );

/// <summary>
/// A Windows .ico, for the game's own TP.ico - the head that Windows showed for Theme Park World.
///
/// <para>
/// The file is a small directory followed by one image each. Every image is a bottom-up DIB whose header
/// claims twice the height it has, because the colour rows are followed by a one-bit AND mask saying which
/// pixels the desktop lets the background through: a set bit is transparent. That mask is the only
/// transparency these images have, so it is what becomes the alpha channel here.
/// </para>
/// <para>
/// The directory's own width, height and colour depth are not believed - TP.ico leaves planes and bit count
/// at zero in all four entries - so each image is measured from its own BITMAPINFOHEADER instead.
/// </para>
/// <para>
/// Palette depths and 24-bit colour are read, which is everything the game ships: TP.ico holds a 32x32 at
/// 4 bits, a 32x32 and a 64x64 at 8, and a 32x32 at 24. An image in any other shape - the 32-bit and
/// PNG-compressed entries a modern icon editor writes - is reported and passed over rather than guessed at,
/// so a file this cannot read says so instead of drawing rubbish.
/// </para>
/// </summary>
public sealed class IconFile : BaseFormat
{
	/// <summary>ICONDIR: two bytes reserved, two of type, two of count.</summary>
	private const int DirectoryHeaderSize = 6;

	/// <summary>ICONDIRENTRY.</summary>
	private const int DirectoryEntrySize = 16;

	/// <summary>BITMAPINFOHEADER, the only image header shaped like an icon of this age.</summary>
	private const int InfoHeaderSize = 40;

	/// <summary>The type word an icon carries; 2 would be a cursor, which has hotspot words in place of the planes.</summary>
	private const int TypeIcon = 1;

	/// <summary>BI_RGB - uncompressed. Nothing in an icon of this age is anything else.</summary>
	private const uint CompressionNone = 0;

	private readonly List<IconImage> _images = [];

	/// <summary>Every image that could be read, in the order the file lists them.</summary>
	public IReadOnlyList<IconImage> Images => _images;

	/// <summary>
	/// The one to show: the largest, and the deepest of those where a file holds one size more than once.
	/// Null when nothing in the file could be read.
	/// </summary>
	public IconImage? Best => _images.Count == 0
		? null
		: _images.OrderByDescending( image => image.Width * image.Height )
			.ThenByDescending( image => image.BitsPerPixel )
			.First();

	public IconFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		// Icons are a few kilobytes and every image is reached by an offset from the start, so the whole
		// file is taken at once rather than seeking about in whatever the caller opened.
		using var memory = new MemoryStream();

		stream.CopyTo( memory );
		memory.Position = 0;

		using var reader = new BinaryReader( memory );

		if ( memory.Length < DirectoryHeaderSize )
		{
			Log.Warning( $"Icon: {memory.Length} bytes is too short to hold an icon directory" );
			return;
		}

		reader.ReadUInt16();

		var type = reader.ReadUInt16();
		var count = reader.ReadUInt16();

		if ( type != TypeIcon )
		{
			Log.Warning( $"Icon: type {type} is not an icon" );
			return;
		}

		if ( DirectoryHeaderSize + (long)count * DirectoryEntrySize > memory.Length )
		{
			Log.Warning( $"Icon: the directory claims {count} images, which do not fit in {memory.Length} bytes" );
			return;
		}

		for ( int i = 0; i < count; ++i )
		{
			memory.Position = DirectoryHeaderSize + i * DirectoryEntrySize;

			// Width, height, colour count and reserved, then the planes and bit count that TP.ico leaves
			// at zero. All of it is read again from the image's own header below.
			reader.ReadBytes( 8 );

			var length = reader.ReadUInt32();
			var offset = reader.ReadUInt32();

			if ( ReadImage( reader, i, offset, length ) is { } image )
				_images.Add( image );
		}
	}

	/// <summary>
	/// One image, or null where it is in a shape this does not read. Everything is bounds-checked against the
	/// length the directory gave: an icon is a file like any other and may be truncated or wrong.
	/// </summary>
	private static IconImage? ReadImage( BinaryReader reader, int index, uint offset, uint length )
	{
		var file = reader.BaseStream;

		if ( offset + (long)length > file.Length || length < InfoHeaderSize )
		{
			Log.Warning( $"Icon: image {index} says {length} bytes at {offset}, which is not in the file" );
			return null;
		}

		file.Position = offset;

		var headerSize = reader.ReadUInt32();

		if ( headerSize != InfoHeaderSize )
		{
			// A PNG-compressed image begins with its own signature, so this is where one lands.
			Log.Warning( $"Icon: image {index} has a {headerSize}-byte header rather than a BITMAPINFOHEADER, so it is passed over" );
			return null;
		}

		var width = reader.ReadInt32();
		var storedHeight = reader.ReadInt32();

		reader.ReadUInt16();

		var bitsPerPixel = reader.ReadUInt16();
		var compression = reader.ReadUInt32();

		// Image size, then the two pixels-per-metre words, none of which an icon fills in usefully.
		reader.ReadUInt32();
		reader.ReadInt32();
		reader.ReadInt32();

		var paletteCount = reader.ReadUInt32();

		reader.ReadUInt32();

		// The stored height counts the colour rows and the mask rows together.
		var height = storedHeight / 2;

		if ( width <= 0 || height <= 0 || storedHeight % 2 != 0 )
		{
			Log.Warning( $"Icon: image {index} is {width} by {storedHeight}, which is not an icon's shape" );
			return null;
		}

		if ( compression != CompressionNone )
		{
			Log.Warning( $"Icon: image {index} is compressed ({compression}), so it is passed over" );
			return null;
		}

		if ( bitsPerPixel is not (1 or 2 or 4 or 8 or 24) )
		{
			Log.Warning( $"Icon: image {index} is {bitsPerPixel} bits a pixel, which is not read, so it is passed over" );
			return null;
		}

		if ( paletteCount == 0 && bitsPerPixel <= 8 )
			paletteCount = 1u << bitsPerPixel;

		// Both bitmaps are stored bottom-up with every row padded out to four bytes.
		var rowBytes = ((width * bitsPerPixel + 31) / 32) * 4;
		var maskBytes = ((width + 31) / 32) * 4;
		var needed = (long)paletteCount * 4 + (long)rowBytes * height + (long)maskBytes * height;

		if ( InfoHeaderSize + needed > length )
		{
			Log.Warning( $"Icon: image {index} needs {InfoHeaderSize + needed} bytes but the directory gave it {length}" );
			return null;
		}

		// The palette is BGRA, its fourth byte unused.
		var palette = new byte[paletteCount * 4];

		if ( palette.Length > 0 )
			reader.Read( palette, 0, palette.Length );

		var colours = reader.ReadBytes( rowBytes * height );
		var mask = reader.ReadBytes( maskBytes * height );
		var pixels = new byte[width * height * 4];

		for ( int y = 0; y < height; ++y )
		{
			var row = (height - 1 - y) * rowBytes;
			var maskRow = (height - 1 - y) * maskBytes;

			for ( int x = 0; x < width; ++x )
			{
				var to = (y * width + x) * 4;

				if ( bitsPerPixel == 24 )
				{
					pixels[to + 0] = colours[row + x * 3 + 2];
					pixels[to + 1] = colours[row + x * 3 + 1];
					pixels[to + 2] = colours[row + x * 3 + 0];
				}
				else
				{
					// Indices run from the top of each byte down: the first pixel of a 4-bit row is its
					// high nibble.
					var bit = x * bitsPerPixel;
					var entry = (colours[row + bit / 8] >> (8 - bitsPerPixel - bit % 8)) & ((1 << bitsPerPixel) - 1);

					if ( entry * 4 + 2 >= palette.Length )
						continue;

					pixels[to + 0] = palette[entry * 4 + 2];
					pixels[to + 1] = palette[entry * 4 + 1];
					pixels[to + 2] = palette[entry * 4 + 0];
				}

				// A set mask bit is a pixel the desktop shows through.
				var transparent = (mask[maskRow + x / 8] >> (7 - x % 8)) & 1;

				pixels[to + 3] = transparent == 1 ? (byte)0 : (byte)255;
			}
		}

		return new IconImage( width, height, bitsPerPixel, pixels );
	}
}
