using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park save container: <c>Easymode.TPWI</c>, the one file of this shape the game ships. These read
/// real game files and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// This is worth a test rather than a comment because a reader that insists on 500, the version a saved
/// park carries, and calls it a magic number rejects the only file it will ever be handed: the shipped
/// park carries 400. A wrong constant that throws is easy to reintroduce, so the shipped file's own
/// numbers are pinned here.
/// </para>
/// </summary>
[TestClass]
public class ParkSaveTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Lost Kingdom is the only park that ships one of these, which is also why it is the only park an
	/// Instant Action player can start in.
	/// </summary>
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private byte[] Raw() => data.ReadAllBytes( ShippedPark );

	private byte[] Inflated()
	{
		// Through a stream of this test's own bytes rather than the global file system, which belongs
		// to a running game - the same rule the terrain tests follow.
		using var stream = new MemoryStream( Raw() );
		return new SaveReader( stream ).ReadFile();
	}

	/// <summary>
	/// The first four bytes are not a magic number - they are a version, and the shipped park's is 400
	/// where a saved park's is 500.
	/// </summary>
	[TestMethod]
	public void TheShippedParkIsVersion400()
	{
		var raw = Raw();

		Assert.AreEqual( 400u, System.BitConverter.ToUInt32( raw, 0 ), "the version this file carries" );
		Assert.AreEqual( 0, raw[4], "the pad byte before the copyright" );
	}

	/// <summary>
	/// The container's own arithmetic has to close: the block says how long it is, and that has to reach
	/// exactly the end of the file from where the block starts. A reader that is one field out still
	/// produces a plausible-looking stream, so this is checked rather than assumed.
	/// </summary>
	[TestMethod]
	public void TheCompressedBlockReachesTheEndOfTheFile()
	{
		var raw = Raw();
		var tag = Encoding.ASCII.GetBytes( "BILZ" );

		var start = Enumerable.Range( 0, raw.Length - 4 )
			.First( i => raw[i] == tag[0] && raw[i + 1] == tag[1] && raw[i + 2] == tag[2] && raw[i + 3] == tag[3] );

		Assert.AreEqual( 0x60D, start, "where the compressed block starts, after a fixed preamble" );

		var inflatedSize = System.BitConverter.ToInt32( raw, start + 4 );
		var blockSize = System.BitConverter.ToInt32( raw, start + 8 );

		Assert.AreEqual( raw.Length, start + blockSize, "the block's length must reach the end of the file" );
		Assert.AreEqual( 1608309, inflatedSize, "the size the payload says it inflates to" );

		// The 28-byte header counts the tag, so the stream starts 28 from the tag and not 32. Getting
		// this wrong lands four bytes into the stream, where zlib's own header no longer is.
		Assert.AreEqual( 0x78, raw[start + 28], "zlib's first byte, at 0x629" );
	}

	/// <summary>
	/// And the payload actually inflates to the size its own header claims, which is the one check that
	/// exercises every offset in the container at once.
	/// </summary>
	[TestMethod]
	public void ThePayloadInflatesToTheSizeItsHeaderClaims()
	{
		var inflated = Inflated();

		Assert.AreEqual( 1608309, inflated.Length, "inflated length" );
		Assert.AreEqual( 0, System.BitConverter.ToInt32( inflated, 0 ), "first dword" );
		Assert.AreEqual( 1171, System.BitConverter.ToInt32( inflated, 4 ), "second dword" );
		Assert.AreEqual( 231, System.BitConverter.ToInt32( inflated, 8 ), "third dword" );
	}

	/// <summary>
	/// What the park is actually made of. The payload names the folder of every feature, shop, ride and
	/// sideshow standing in it, as ordinary text - including the gates, which is the piece that belongs
	/// in the two holes the ground leaves beside the entrance.
	/// </summary>
	[TestMethod]
	public void ThePayloadNamesTheParksOwnFeatures()
	{
		var text = Encoding.Latin1.GetString( Inflated() );

		foreach ( var expected in new[]
		{
			@"data\levels\jungle\Features\gates\",
			@"data\levels\jungle\Features\bus\",
			@"data\levels\jungle\Shops\coconut\",
			@"data\levels\jungle\Rides\bouncy\",
			@"data\levels\jungle\Sideshow\junspray\"
		} )
		{
			StringAssert.Contains( text, expected, $"the payload should name {expected}" );
		}
	}
}
