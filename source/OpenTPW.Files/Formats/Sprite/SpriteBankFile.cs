using System.Text;

namespace OpenTPW;

/// <summary>
/// A sprite bank, <c>*.ESP</c>: which of the sprites in the bank's picture pack
/// (<see cref="SpritePackFile"/>, the <c>.TPC</c> of the same name) make up each of its sixteen
/// sets. The game's sprites live in <c>esprites.wad</c>, a bank to a folder's worth - the guests,
/// the staff, balloons, litter, thought bubbles and particles.
///
/// <para>
/// The layout is from the game's loader (0x00540d90), 350 bytes:
/// </para>
/// <list type="table">
/// <item><term>0x000, 12 bytes</term><description><c>ESP_FILE2.00</c></description></item>
/// <item><term>0x00C, 256 bytes</term><description>A name - <c>SPR_PA.TPS</c> - NUL-padded</description></item>
/// <item><term>0x10C, 2 bytes</term><description>Two flags; the first steers which picture pack is loaded</description></item>
/// <item><term>0x10E, 16 x 4 bytes</term><description>The sets - see <see cref="SpriteSet"/></description></item>
/// <item><term>0x14E, 16 bytes</term><description>Four groups of four flags the loader counts but nothing found uses</description></item>
/// </list>
/// <para>
/// The game asks for a sprite by a number whose top part is the bank - banks are numbered in the
/// order a folder's files are found - and whose low four bits are the set (0x005423a0).
/// </para>
/// </summary>
public sealed class SpriteBankFile : BaseFormat
{
	private const string Magic = "ESP_FILE2.00";

	public string Name { get; private set; } = "";

	public SpriteSet[] Sets { get; private set; } = [];

	public SpriteBankFile( string path )
	{
		ReadFromFile( path );
	}

	public SpriteBankFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var reader = new BinaryReader( stream );

		var magic = Encoding.ASCII.GetString( reader.ReadBytes( Magic.Length ) );
		if ( magic != Magic )
			throw new InvalidDataException( $"Not a sprite bank - it starts '{magic}'" );

		var name = reader.ReadBytes( 256 );
		var end = Array.IndexOf( name, (byte)0 );
		Name = Encoding.ASCII.GetString( name, 0, end < 0 ? name.Length : end );

		_ = reader.ReadBytes( 2 );

		Sets = new SpriteSet[16];
		for ( int i = 0; i < Sets.Length; ++i )
			Sets[i] = new SpriteSet( reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte() != 0 );
	}
}

/// <summary>
/// One set of a <see cref="SpriteBankFile"/>: a run of sprites in its picture pack. Frame N of the
/// set is sprite <see cref="First"/> + N; a directional set follows that with the same run again for
/// each way something can face, <see cref="FramesPerDirection"/> apart.
/// </summary>
public readonly record struct SpriteSet( int First, int FramesPerDirection, bool Directional );
