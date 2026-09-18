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
/// <item><term>0x14E, 16 bytes</term><description>Four groups of four bytes, read rather than merely
/// counted: <c>FUN_00540c70</c> takes a state of 0 to 3 and hands back that group's first two bytes as a
/// script and a set, and its last two to the sprite it makes. Only group 0 is ever in use, and only on the
/// twelve Entertainer banks - every one reads (1, 5, 0, 0) but <c>Hallow\Entertainers\SPR_DR</c>, which
/// reads (1, 5, 2, 40). Measured over all 46 banks; nothing here uses them yet.</description></item>
/// </list>
/// <para>
/// The game asks for a sprite by a number whose top part is the bank and whose low four bits are the
/// set (0x005423a0). <b>Banks are numbered in the order they are LOADED, which is not always the order
/// their folder lists them</b> - the four avatar banks come first for the guest kinds that have them.
/// This said "the order a folder's files are found", which <c>ParkGuestSprites.BanksIn</c> contradicts
/// and <c>ParkGuestArtTests</c> pins: a sweep of the kids folder gives one order and the load gives
/// another.
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
			Sets[i] = new SpriteSet( reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte() );
	}
}

/// <summary>
/// One set of a <see cref="SpriteBankFile"/>: a run of sprites in its picture pack. Frame N of the set
/// facing direction D is sprite <see cref="First"/> + N + <see cref="FramesPerDirection"/> * D, so the
/// set stores its whole run once per direction, one after another.
///
/// <para>
/// <b><see cref="Directions"/> is a count, not a flag.</b> This was read as a boolean - "is this set
/// directional" - which is true of what it means but loses what it says, and nothing in the tree
/// consumed it, so nothing went wrong visibly. Over every set in use across all 46 banks the byte reads
/// 5 on 201, 7 on ten, 4 on one, and 0 on the 71 that face nowhere at all. The ten sevens are the eight
/// <c>Kidsheads</c> banks and two <c>Costumeheads</c>; five is what a person's body stores. A count is
/// also what the engine treats it as: it folds a heading onto <c>8 - d</c> when <c>d</c> runs past
/// <c>Directions - 1</c> and draws the reflection, which needs the number and not a yes.
/// </para>
/// <para>
/// The count is what makes a bank's own arithmetic close: <c>Generic\Kids\SPR_BE</c> set 1 is first 135,
/// eight frames a direction, five directions, and 135 + 8 * 5 = 175 is exactly its pack's picture count.
/// One bank in 283 sets overruns - <c>Generic\Kidsheads\SPR_BE</c> asks for 56 pictures and its pack
/// holds 55 - which is a defect in that file rather than a rule, so a reader must not index blindly.
/// </para>
/// </summary>
/// <param name="Directions">How many ways this set stores, or 0 when it faces nowhere.</param>
public readonly record struct SpriteSet( int First, int FramesPerDirection, int Directions )
{
	/// <summary>Whether this set stores a run per direction, which is the question the old flag asked.</summary>
	public bool Directional => Directions > 0;
}
