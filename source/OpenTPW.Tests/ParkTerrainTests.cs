using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
	/// The block's +0x20 and +0x24 are the heights' authored range, which the original lights the ground
	/// with. In every park each is a whole number within 1.0 of the field's own
	/// lowest and highest height (fantasy says -9 where its lowest is -10).
	/// </summary>
	[TestMethod]
	public void EveryParkCarriesItsHeightRange()
	{
		var expected = new System.Collections.Generic.Dictionary<string, (float Low, float High)>
		{
			["jungle"] = (-10f, 60f),
			["fantasy"] = (-9f, 20f),
			["hallow"] = (0f, 21f),
			["space"] = (-10f, 28f),
		};

		foreach ( var theme in Themes )
		{
			var field = Landscape( theme );

			Assert.AreEqual( expected[theme].Low, field.HeightLow, theme );
			Assert.AreEqual( expected[theme].High, field.HeightHigh, theme );
			Assert.IsTrue( field.Heights.Min() >= field.HeightLow - 1f && field.Heights.Max() <= field.HeightHigh + 1f, theme );
		}
	}

	/// <summary>
	/// The ground is lit with the original's own normal (FUN_0056ef10): nearer height minus further, times
	/// one over the height range, 1.0 up, not normalised - and the last row reaches 128 floats back. A
	/// normal worked out from the true slope and normalised lights the jungle's hills three and a half
	/// times steeper than the original does.
	/// </summary>
	[TestMethod]
	public void TheGroundNormalIsTheOriginals()
	{
		var field = Landscape( "jungle" );
		var stride = field.CellsX + 1;
		var k = 1f / 70f;

		// The steepest interior vertex across X, so a normalised or true-slope normal cannot pass.
		var (x, y) = Enumerable.Range( 1, field.CellsY - 1 )
			.SelectMany( row => Enumerable.Range( 1, field.CellsX - 1 ).Select( col => (col, row) ) )
			.MaxBy( v => System.Math.Abs( field.HeightAt( v.col - 1, v.row ) - field.HeightAt( v.col + 1, v.row ) ) );

		var normal = ParkGround.NormalAt( field, x, y );

		Assert.AreEqual( (field.HeightAt( x - 1, y ) - field.HeightAt( x + 1, y )) * k, normal.X, 1e-5f );
		Assert.AreEqual( 1f, normal.Y );
		Assert.AreEqual( (field.HeightAt( x, y - 1 ) - field.HeightAt( x, y + 1 )) * k, normal.Z, 1e-5f );
		Assert.IsTrue( normal.Length > 1.01f, $"({x}, {y}) is steep, so its normal is longer than 1: {normal.Length}" );

		// The last row, where the original steps back by 128 rather than by the row: at the vertex where
		// those two heights differ most (40 of the jungle's 97 differ, by up to 2.0).
		var last = field.CellsY;
		var at = Enumerable.Range( 0, stride )
			.MaxBy( col => System.Math.Abs( field.Heights[(last * stride) + col - 128] - field.HeightAt( col, last - 1 ) ) );
		var edge = ParkGround.NormalAt( field, at, last );
		var here = (last * stride) + at;

		Assert.AreNotEqual( field.Heights[here - 128], field.HeightAt( at, last - 1 ), 0.5f, "the jungle's last row has no vertex to tell the two apart" );

		Assert.AreEqual( (field.Heights[here - 128] - field.Heights[here]) * k, edge.Z, 1e-5f );
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

	/// <summary>
	/// The landscape's height as the original answers it (<c>docs/exe/park.md</c>, "The scene's height under a
	/// point"): nought off the map, and on it the plane of three of the cell's corners - the near one and its two
	/// neighbours, or, on a cell flagged <c>0x800</c>, the triangle the point is in, split along one diagonal or,
	/// with <c>0x4</c>, the other. Held against each plane worked out from its three corners, at five points of every
	/// cell of all four parks.
	/// </summary>
	[TestMethod]
	public void TheLandscapesHeightIsThePlaneOfThreeOfItsCellsCorners()
	{
		static float Plane( (float U, float V, float H) a, (float U, float V, float H) b, (float U, float V, float H) c, float u, float v )
		{
			var nx = ((b.V - a.V) * (c.H - a.H)) - ((b.H - a.H) * (c.V - a.V));
			var ny = ((b.H - a.H) * (c.U - a.U)) - ((b.U - a.U) * (c.H - a.H));
			var nz = ((b.U - a.U) * (c.V - a.V)) - ((b.V - a.V) * (c.U - a.U));

			return a.H - (((nx * (u - a.U)) + (ny * (v - a.V))) / nz);
		}

		(float U, float V)[] points = [(0.25f, 0.25f), (0.75f, 0.25f), (0.25f, 0.75f), (0.75f, 0.75f), (0.5f, 0.125f)];
		int plain = 0, split = 0, other = 0, bent = 0;

		foreach ( var theme in Themes )
		{
			var field = Landscape( theme );

			Assert.AreEqual( 0f, field.ScapeHeight( -0.5f, 5f ), theme );
			Assert.AreEqual( 0f, field.ScapeHeight( 5f, -0.5f ), theme );
			Assert.AreEqual( 0f, field.ScapeHeight( field.CellsX * field.CellSizeX, 5f ), theme );
			Assert.AreEqual( 0f, field.ScapeHeight( 5f, field.CellsY * field.CellSizeY ), theme );

			for ( var y = 0; y < field.CellsY; ++y )
			{
				for ( var x = 0; x < field.CellsX; ++x )
				{
					var near = (0f, 0f, field.HeightAt( x, y ));
					var alongX = (1f, 0f, field.HeightAt( x + 1, y ));
					var alongY = (0f, 1f, field.HeightAt( x, y + 1 ));
					var far = (1f, 1f, field.HeightAt( x + 1, y + 1 ));
					var flags = field.FlagsAt( x, y );

					if ( MathF.Abs( near.Item3 + far.Item3 - alongX.Item3 - alongY.Item3 ) > 0.01f )
						++bent;

					if ( (flags & 0x800) == 0 )
						++plain;
					else if ( (flags & 0x4) == 0 )
						++split;
					else
						++other;

					foreach ( var (u, v) in points )
					{
						var want = (flags & 0x800) == 0 ? Plane( near, alongX, alongY, u, v )
							: (flags & 0x4) == 0 ? (v <= u ? Plane( near, alongX, far, u, v ) : Plane( near, alongY, far, u, v ))
							: (1f - v <= u ? Plane( alongX, far, alongY, u, v ) : Plane( near, alongX, alongY, u, v ));

						Assert.AreEqual( want, field.ScapeHeight( (x + u) * field.CellSizeX, (y + v) * field.CellSizeY ), 0.002f,
							$"{theme} cell ({x},{y}) flags 0x{flags:x} at ({u},{v})" );
					}
				}
			}
		}

		Console.WriteLine( $"cells not flagged 0x800: {plain}; flagged: {split}; flagged with 0x4: {other}; not flat: {bent}" );
		Assert.IsTrue( plain > 0 && bent > 0, "the parks have cells of the plain kind, and cells that are not flat" );
	}
}
