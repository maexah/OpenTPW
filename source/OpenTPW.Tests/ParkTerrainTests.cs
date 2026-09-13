using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A park's land: the heightfield block inside base.MD2 and the attribute map beside it. These read
/// real game files and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// Two things here are worth a test rather than a comment, because both are invisible in a build and
/// both produce a map that looks plausible and is wrong: the attribute map's header is 72 bytes and not
/// the tempting 80 (16464 is 16384 + 80 as well as 72 + 16384 + 8), and its cells are indexed
/// <c>x * 128 + y</c> where the heightfield in the very same park is <c>y * width + x</c>.
/// </para>
/// </summary>
[TestClass]
public class ParkTerrainTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>The four themes the game ships, by the folder each lives in.</summary>
	private static readonly string[] Themes = ["jungle", "fantasy", "hallow", "space"];

	/// <summary>
	/// Read through the file system this test mounted rather than the global one, which belongs to a
	/// running game. <see cref="SettingsFile"/>'s path constructor goes through that global, so it is
	/// deliberately not used here.
	/// </summary>
	private T Read<T>( string path, System.Func<Stream, T> make )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( path ) );
		return make( stream );
	}

	private HeightfieldFile Landscape( string theme )
		=> Read( $"levels/{theme}/terrain/base.MD2", stream => new HeightfieldFile( stream ) );

	private AttributeMapFile Attributes( string theme )
		=> Read( $"levels/{theme}/terrain/base.map", stream => new AttributeMapFile( stream ) );

	private SettingsFile Balance( string theme )
		=> Read( $"levels/{theme}/Standard.sam", stream => new SettingsFile( stream ) );

	/// <summary>
	/// Every park is the same shape, which is what makes them re-skins of one another rather than
	/// different places - and what lets one park engine serve all four. The block is found by its own
	/// invariants rather than at an offset, because it sits somewhere different in each file.
	/// </summary>
	[TestMethod]
	public void EveryParkHasTheSameLandscapeShape()
	{
		foreach ( var theme in Themes )
		{
			var field = Landscape( theme );

			Assert.AreEqual( 96, field.CellsX, $"{theme} cells across" );
			Assert.AreEqual( 85, field.CellsY, $"{theme} cells down" );
			Assert.AreEqual( 97 * 86, field.VertexCount, $"{theme} heights - one more than cells each way" );
			Assert.AreEqual( 10f, field.CellSizeX, $"{theme} cell width" );
			Assert.AreEqual( 10f, field.CellSizeY, $"{theme} cell depth" );
			Assert.AreEqual( field.VertexCount, field.Heights.Length, $"{theme} height array length" );
			Assert.AreEqual( field.CellCount, field.Cells.Length, $"{theme} cell record array length" );
		}
	}

	/// <summary>
	/// The attribute map is 128 square in every park, and says so itself rather than being assumed.
	/// </summary>
	[TestMethod]
	public void EveryParkHasA128SquareAttributeMap()
	{
		foreach ( var theme in Themes )
		{
			var map = Attributes( theme );

			Assert.AreEqual( 128, map.Width, $"{theme} attribute map width" );
			Assert.AreEqual( 128, map.Height, $"{theme} attribute map height" );
			Assert.AreEqual( 128 * 128, map.Cells.Length, $"{theme} attribute cell count" );
		}
	}

	/// <summary>
	/// The one that pins both the header offset and the indexing at once.
	///
	/// <para>
	/// Standard.sam places the ticket booths, bus stops, crossings and entrance at grid cells. Read the
	/// map correctly and every one of them lands on a meaningful attribute; read it eight bytes out, or
	/// with the two axes the other way round, and they land on empty ground instead. The transposed
	/// lookup is asserted to be zero as well, so that a map which happened to be symmetrical could not
	/// pass by accident.
	/// </para>
	/// </summary>
	[TestMethod]
	public void FixedItemsLandOnTheirOwnAttributes()
	{
		var items = new[]
		{
			"TicketBoothA", "TicketBoothB",
			"BusStopA", "BusStopB",
			"CrossingBSSideA", "CrossingBSSideB",
			"CrossingParkSideA", "CrossingParkSideB",
			"EntranceA", "EntranceB"
		};

		foreach ( var theme in Themes )
		{
			var balance = Balance( theme );
			var map = Attributes( theme );

			foreach ( var item in items )
			{
				var x = Cell( balance, $"FixedItemInfo.{item}PosX" );
				var y = Cell( balance, $"FixedItemInfo.{item}PosY" );

				Assert.AreNotEqual( 0, map.At( x, y ),
					$"{theme}: {item} is at cell ({x},{y}) and that cell should not be empty ground" );

				Assert.AreEqual( 0, map.At( y, x ),
					$"{theme}: {item} read with its axes swapped should land on nothing - the map is x*128+y" );
			}
		}
	}

	/// <summary>
	/// And the attributes themselves, in the one park the work is being done against. The park entrance
	/// is 8, the bus road and its crossings 144, the ticket booths 148 - the same values in the same
	/// places in all four parks, because every park inherits the same fixed approach.
	/// </summary>
	[TestMethod]
	public void TheEntranceAndTheBusRoadReadBackTheirKnownValues()
	{
		var balance = Balance( "jungle" );
		var map = Attributes( "jungle" );

		byte Attribute( string item )
			=> map.At( Cell( balance, $"FixedItemInfo.{item}PosX" ), Cell( balance, $"FixedItemInfo.{item}PosY" ) );

		Assert.AreEqual( 8, Attribute( "EntranceA" ), "the park entrance" );
		Assert.AreEqual( 8, Attribute( "EntranceB" ), "the park entrance" );
		Assert.AreEqual( 144, Attribute( "BusStopA" ), "the bus road" );
		Assert.AreEqual( 144, Attribute( "CrossingParkSideA" ), "the crossing on the park side" );
		Assert.AreEqual( 148, Attribute( "TicketBoothA" ), "the ticket booths" );
	}

	/// <summary>
	/// The playable region Standard.sam declares is the region the attribute map actually fills. The
	/// map is 128 square and the park is 95x84 of it, so everything beyond that should be empty - which
	/// is the check that the two files agree about where the park is.
	/// </summary>
	[TestMethod]
	public void NothingIsMarkedOutsideTheDeclaredHeightfield()
	{
		var balance = Balance( "jungle" );
		var map = Attributes( "jungle" );

		var width = Cell( balance, "MapInfo.HeightfieldWidth" );
		var height = Cell( balance, "MapInfo.HeightfieldHeight" );

		Assert.AreEqual( 95, width, "declared heightfield width" );
		Assert.AreEqual( 84, height, "declared heightfield height" );

		var outside = 0;

		for ( var x = 0; x < map.Width; ++x )
			for ( var y = 0; y < map.Height; ++y )
				if ( (x > width || y > height) && map.At( x, y ) != 0 )
					outside++;

		Assert.AreEqual( 0, outside,
			$"{outside} marked cells sit outside the {width}x{height} the park declares" );
	}

	private static int Cell( SettingsFile balance, string key )
	{
		var value = balance[key];

		Assert.IsNotNull( value, $"{key} is missing from the balance file" );
		Assert.IsTrue( int.TryParse( value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cell ),
			$"{key} is '{value}', which is not a cell" );

		return cell;
	}
}
