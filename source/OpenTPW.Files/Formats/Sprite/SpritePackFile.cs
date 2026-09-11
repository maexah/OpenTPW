namespace OpenTPW;

/// <summary>
/// A sprite picture pack, <c>*.TPC</c>: the pictures a <see cref="SpriteBankFile"/> indexes, each a
/// small run-length-coded image on a shared palette.
///
/// <para>
/// From the game's loader (0x00587db0, and 0x00588660 for each picture). The header is a version
/// and a second number, both 3 in the packs used here, and a picture count. Version 3 then has a
/// palette of 256 colours, four bytes each in the order blue, green, red, alpha, before the
/// pictures. (Version 2 stores its pictures another way; nothing loaded here uses it.) Each picture
/// is:
/// </para>
/// <list type="table">
/// <item><term>4 bytes</term><description>How many bytes of picture data follow the header</description></item>
/// <item><term>2 + 2 bytes</term><description>Width and height</description></item>
/// <item><term>2 + 2 bytes</term><description>128 and 128 in every picture here; not identified</description></item>
/// <item><term>4 + 4 bytes</term><description>Where the picture's origin is, from its top left, negated - -8, -8 for a 15x15</description></item>
/// <item><term>the data</term><description>One run-length-coded row after another, top first</description></item>
/// </list>
/// <para>
/// A row is a byte giving how many bytes the row takes, then codes. A code of n below zero repeats
/// the next palette index -n times; one above zero is followed by n palette indices to copy. So
/// <c>07 F1 00 02 F6 8F F1 00</c> is fifteen of index 0, then 0xF6 and 0x8F, then fifteen more of 0 -
/// a 32-pixel row. Every picture in the particle packs comes out at exactly its width and height
/// and uses exactly its data this way.
/// </para>
/// </summary>
public sealed class SpritePackFile : BaseFormat
{
	public SpritePicture[] Pictures { get; private set; } = [];

	public SpritePackFile( string path )
	{
		ReadFromFile( path );
	}

	public SpritePackFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream );

		var version = reader.ReadUInt16();
		_ = reader.ReadUInt16();
		var count = reader.ReadInt32();

		if ( version != 3 )
			throw new NotSupportedException( $"Sprite pack version {version} - only version 3, with a palette, is read" );

		var palette = reader.ReadBytes( 256 * 4 );

		Pictures = new SpritePicture[count];

		for ( int i = 0; i < count; ++i )
		{
			var size = reader.ReadInt32();
			var width = reader.ReadUInt16();
			var height = reader.ReadUInt16();
			_ = reader.ReadUInt16();
			_ = reader.ReadUInt16();
			var originX = reader.ReadInt32();
			var originY = reader.ReadInt32();
			var data = reader.ReadBytes( size );

			Pictures[i] = new SpritePicture( width, height, -originX, -originY, Decode( data, width, height, palette ) );
		}
	}

	/// <summary>The rows of one picture, as red, green, blue and alpha bytes - see the class remarks.</summary>
	private static byte[] Decode( byte[] data, int width, int height, byte[] palette )
	{
		var rgba = new byte[width * height * 4];
		var position = 0;

		for ( int y = 0; y < height && position < data.Length; ++y )
		{
			var rowEnd = position + 1 + data[position];
			position++;

			var x = 0;

			while ( position < rowEnd && position < data.Length )
			{
				var code = (sbyte)data[position++];

				if ( code < 0 )
				{
					var index = position < data.Length ? data[position++] : 0;

					for ( int n = 0; n < -code; ++n )
						Put( rgba, width, x++, y, palette, index );
				}
				else
				{
					for ( int n = 0; n < code && position < data.Length; ++n )
						Put( rgba, width, x++, y, palette, data[position++] );
				}
			}

			position = rowEnd;
		}

		return rgba;
	}

	private static void Put( byte[] rgba, int width, int x, int y, byte[] palette, int index )
	{
		if ( x >= width )
			return;

		var to = ((y * width) + x) * 4;
		var from = index * 4;

		rgba[to] = palette[from + 2];
		rgba[to + 1] = palette[from + 1];
		rgba[to + 2] = palette[from];
		rgba[to + 3] = palette[from + 3];
	}
}

/// <summary>One picture of a <see cref="SpritePackFile"/>, decoded, with its origin measured from its top left.</summary>
public sealed record SpritePicture( int Width, int Height, int OriginX, int OriginY, byte[] Rgba );
