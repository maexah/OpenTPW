using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The World block inside <c>Easymode.TPWI</c> - what actually stands in Lost Kingdom. These read real
/// game files and are skipped where there is no installation: see <see cref="GameData"/>.
///
/// <para>
/// The payload is a serialised memory image, so nothing in it can be found by searching and every offset
/// has to be walked to. That is what makes these tests worth having: a walk that is wrong by one byte
/// anywhere still produces plausible-looking numbers, and the only thing that catches it is the far end.
/// </para>
/// </summary>
[TestClass]
public class ParkWorldTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>
	/// Through a stream of this test's own bytes rather than the global file system, which belongs to a
	/// running game - the same rule the other park tests follow.
	/// </summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The one check that exercises every byte of the walk at once.
	///
	/// <para>
	/// The block is a megzabyte and a half of variable-length records - a header, a hundred and fifty
	/// object controls, a pool of timers, sixteen thousand map cells each measured by the status byte it
	/// opens with, then forty-two things of nine different models. The original writes a four-character
	/// tag after each of its seventeen modules and checks it on the way back in, so ending exactly on that
	/// tag means every single record length in between was right. Miss by one byte and this fails.
	/// </para>
	/// <para>
	/// The tag is stored as a little-endian dword, so it reads backwards in the file: a search for "WRLD"
	/// finds nothing and one for "DLRW" finds it at once.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheWalkEndsExactlyOnTheNextModulesTag()
	{
		var world = World();

		Assert.IsNull( world.Problem, "nothing should have stopped the walk" );
		Assert.IsTrue( world.ClosedOnTrailer,
			$"the walk should end on the {ParkWorld.Trailer} trailer" );

		Assert.AreEqual( 42, world.ThingCount, "things in the park" );
	}

	/// <summary>
	/// The people and their sprites, which are two separate tables that have to agree.
	///
	/// <para>
	/// This is the reconciliation the whole reading rests on: the thing list says how many people the
	/// park holds, the sprite table says how many pictures are live, and they are counted by entirely
	/// different means - one by walking variable-length records, the other by counting handles that are
	/// not zero. Thirteen guests and one of each of the five staff is eighteen, and the table has
	/// eighteen live. A record size that drifted would desynchronise the walk and break this long before
	/// it produced anything that looked wrong on screen.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryPersonInTheParkHasASpriteOfTheirOwn()
	{
		var world = World();

		Assert.AreEqual( 18, world.People.Count, "people in the park" );
		Assert.AreEqual( 13, world.People.Count( person => person.Model == 1 ), "guests among them" );

		var live = world.Sprites.Select( sprite => sprite.Slot ).ToArray();

		Assert.AreEqual( 18, live.Length, "live sprites in the table" );
		CollectionAssert.AllItemsAreUnique( world.People.Select( person => person.SpriteSlot ).ToArray(),
			"two people should never share one sprite" );

		foreach ( var person in world.People )
			CollectionAssert.Contains( live, person.SpriteSlot,
				$"thing {person.ThingId} names sprite slot {person.SpriteSlot}, which should be live" );
	}

	/// <summary>
	/// The sprite table ends exactly on the tag after it, which is worth as much here as the World
	/// block's own trailer: the table is a slot count and a run of fixed records, so landing on
	/// <c>CSPS</c> to the byte says both numbers were right. Get the record size wrong and this misses by
	/// a whole multiple of it.
	/// </summary>
	[TestMethod]
	public void TheSpriteTableEndsExactlyOnTheNextModulesTag()
	{
		var world = World();

		Assert.IsTrue( world.ClosedOnSpriteTrailer,
			$"the sprite table should end on the {ParkWorld.SpriteTrailer} tag" );

		Assert.IsFalse( world.Sprites.Any( sprite => sprite.Slot == 0 ),
			"slot zero is never used" );
	}

	/// <summary>
	/// A person's heading and their sprite's own stored direction, which are two different fields written
	/// by two different parts of the game, reduced to the same answer.
	///
	/// <para>
	/// A person keeps an eleven-bit angle; the sprite keeps the octant the game last drew them at. The
	/// rule that turns one into the other biases by <c>0x380</c> - half an octant of rounding and three
	/// of turn - and if it were wrong this would fail for the five people who are not facing octant zero.
	/// That is what stops the check being vacuous: thirteen of the eighteen do face zero, so the
	/// assertion below also pins that the column is not simply all zeros.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EachPersonFacesTheWayTheirSpriteWasDrawnFacing()
	{
		var world = World();
		var sprites = world.Sprites.ToDictionary( sprite => sprite.Slot );

		foreach ( var person in world.People )
			Assert.AreEqual( sprites[person.SpriteSlot].Facing, person.Facing,
				$"thing {person.ThingId} faces one way and its sprite another" );

		Assert.IsTrue( world.People.Select( person => person.Facing ).Distinct().Count() >= 4,
			"the park's people should not all be facing the same way" );
	}

	/// <summary>
	/// The art each person wears was rolled once, when they were made, and written down. A reader that
	/// rolled again on load would keep every count in these tests and still put every guest in different
	/// clothes each time the park opened, so what is pinned here is that the banks <i>vary</i> and that
	/// the two people saved mid-stand are still saved mid-stand.
	/// </summary>
	[TestMethod]
	public void ThePeopleWearTheArtTheSaveGaveThem()
	{
		var world = World();
		var guests = world.People.Where( person => person.Model == 1 )
			.Select( person => world.Sprites.First( sprite => sprite.Slot == person.SpriteSlot ) )
			.ToArray();

		Assert.IsTrue( guests.All( sprite => sprite.Type == 0 ), "every guest should wear a kids bank" );
		Assert.IsTrue( guests.Select( sprite => sprite.Bank ).Distinct().Count() >= 2,
			"the guests should not all have been given the same bank" );

		Assert.AreEqual( 2, world.Sprites.Count( sprite => sprite.Set == 0 ),
			"the guard and one kid are the two saved standing; the rest are mid-walk" );
	}

	/// <summary>
	/// The header's handles. These are not positions and not indices into anything: the original compares
	/// them against a thing's own id with <c>==</c>, so eleven means "the thing whose id is 11".
	///
	/// <para>
	/// They are pinned because they are the check that the thing list is being read the right way round.
	/// The record prefix holds the id of the <i>next</i> thing rather than its own, and reading it as its
	/// own is off by one everywhere - which made the gate come out as the traffic lights and the traffic
	/// lights as a fairground ride, both entirely plausible-looking.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheHeaderNamesTheGateTheLightsAndTheFirstObject()
	{
		var world = World();

		Assert.AreEqual( 11, world.ParkGates, "the thing that is the park gate" );
		Assert.AreEqual( 12, world.TrafficLights, "the thing that is the traffic lights" );
		Assert.AreEqual( 15, world.FirstObject, "the head of the object list" );

		// The gate and the lights have to be objects, and unplaced ones - see the fixed-item test below.
		var gate = world.Objects.Single( item => item.ThingId == world.ParkGates );
		Assert.AreEqual( 1601, gate.CatalogueId, "the gate's catalogue number" );
	}

	/// <summary>
	/// Everything standing in Lost Kingdom, by catalogue number and cell. The whole table is pinned rather
	/// than a sample of it, because a walk that drifts produces a table that is still the right shape.
	///
	/// <para>
	/// The catalogue numbers are each item's own <c>Info.Id</c>, read from the <c>.sam</c> inside its
	/// <c>.wad</c> - 1402 is the Small Toilet, 1100 the Belly Bounce. Positions are in 256ths of a cell,
	/// which was settled by sweeping the shift: at eight, every value lands inside the map's populated
	/// extent, and at seven several fall off the map entirely.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ThePlacedObjectsAreTheOnesTheParkWasBuiltWith()
	{
		var expected = new (int ThingId, int CatalogueId, int CellX, int CellY, int Angle)[]
		{
			(24, 1403, 57, 19, 90),   // Round Fountain
			(23, 1402, 55, 15, 270),  // Small Toilet
			(22, 1402, 55, 16, 270),  // Small Toilet
			(21, 1402, 55, 17, 270),  // Small Toilet
			(20, 1411, 58, 16, 90),   // Staff Room
			(19, 1413, 40, 29, 0),    // Security Camera
			(18, 1413, 55, 29, 0),    // Security Camera
			(17, 1406, 44, 29, 0),    // Litter Bin
			(16, 1203, 43, 30, 0),    // Drinks Shop
			(14, 1303, 51, 30, 0),    // Jungle Spray
			(13, 1100, 51, 23, 0)     // Belly Bounce
		};

		var placed = World().Objects.Where( item => item.IsPlaced ).ToArray();

		Assert.AreEqual( expected.Length, placed.Length, "objects standing somewhere" );

		for ( var i = 0; i < expected.Length; ++i )
		{
			var (thingId, catalogueId, cellX, cellY, angle) = expected[i];

			Assert.AreEqual( thingId, placed[i].ThingId, $"object {i}'s thing id" );
			Assert.AreEqual( catalogueId, placed[i].CatalogueId, $"object {i}'s catalogue number" );
			Assert.AreEqual( cellX, placed[i].CellX, $"object {i} ({catalogueId}) cell x" );
			Assert.AreEqual( cellY, placed[i].CellY, $"object {i} ({catalogueId}) cell y" );
			Assert.AreEqual( angle, placed[i].Angle, $"object {i} ({catalogueId}) angle" );
		}
	}

	/// <summary>
	/// The three fixed items are saved as objects like everything else and carry no position at all.
	///
	/// <para>
	/// This is the thing that made them look absent for so long, and it is worth a test because the
	/// sentinel does not read as one: both coordinates hold 128, which is half a cell, so anything
	/// treating it as a position puts all three at the origin rather than noticing they have none. Their
	/// real places are baked into their models - see <see cref="ParkFixedItems"/>.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGateTheLightsAndTheBusAreSavedWithoutAPosition()
	{
		var world = World();

		Assert.AreEqual( 14, world.Objects.Count, "catalogue objects in the park, placed or not" );

		var unplaced = world.Objects.Where( item => !item.IsPlaced )
			.Select( item => item.CatalogueId )
			.OrderBy( id => id )
			.ToArray();

		CollectionAssert.AreEqual( new[] { 1600, 1601, 1603 }, unplaced,
			"the bus, the gate and the traffic lights should be the ones without a position" );

		foreach ( var item in world.Objects.Where( item => !item.IsPlaced ) )
		{
			Assert.AreEqual( ParkWorld.CatalogueObject.Unplaced, item.RawX, $"{item.CatalogueId} raw x" );
			Assert.AreEqual( ParkWorld.CatalogueObject.Unplaced, item.RawY, $"{item.CatalogueId} raw y" );
		}
	}

	/// <summary>
	/// An item's footprint is the box its <c>Info.Shape</c> picture is drawn in - not the cells marked
	/// inside it, which is the reading the picture invites and which is wrong.
	///
	/// <para>
	/// The falsification is the <c>.hmp</c> beside it, a file that carries no such picture and whose
	/// length alone gives a cell count (a 48-byte header and 27 bytes a cell). The items below are chosen
	/// because they are where the two readings differ: <c>4x4rock</c> draws fourteen stars in a four by
	/// four box and its .hmp says sixteen, and <c>gates</c> draws a single cell but declares a six by
	/// three override and its .hmp says eighteen. Counting stars would fail on both.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnItemsFootprintIsTheBoxItsShapeIsDrawnIn()
	{
		var expected = new (string Directory, string Description, string Footprint, int Width, int Depth)[]
		{
			("features/toilet", "Toilet.sam", "toilet.hmp", 1, 1),
			("features/fountain", "Fountain.sam", "fountain.hmp", 3, 3),
			("features/staff", "Staff.sam", "staff.hmp", 2, 2),
			("features/4x4rock", "4x4rock.sam", "4x4rock.hmp", 4, 4),
			("features/gates", "Gates.sam", "gates.hmp", 6, 3),
			("shops/coconut", "Coconut.sam", "coconut.hmp", 2, 2),
			("sideshow/junspray", "Junspray.sam", "junspray.hmp", 3, 3),
			("rides/bouncy", "Bouncy.sam", "bouncy.hmp", 3, 4)
		};

		foreach ( var (directory, description, footprint, width, depth) in expected )
		{
			using var stream = new MemoryStream( data.ReadAllBytes( $"levels/jungle/{directory}/{description}" ) );
			var item = new ItemDescriptionFile( stream );

			Assert.AreEqual( width, item.FootprintWidth, $"{description} width" );
			Assert.AreEqual( depth, item.FootprintDepth, $"{description} depth" );

			// And the same answer from a file that knows nothing about the picture.
			var size = data.ReadAllBytes( $"levels/jungle/{directory}/{footprint}" ).Length;

			Assert.AreEqual( 0, (size - 48) % 27,
				$"{footprint} is {size} bytes, which is not a 48-byte header and whole cells of 27" );

			Assert.AreEqual( width * depth, (size - 48) / 27,
				$"{footprint} should hold one record per cell of the {width}x{depth} box" );
		}
	}

	/// <summary>
	/// The catalogue numbers the park stores really do name items the theme ships, which is what makes
	/// them resolvable to a model at all. Read from each item's own description rather than a table
	/// written out here.
	/// </summary>
	[TestMethod]
	public void EveryPlacedObjectNamesAnItemTheThemeShips()
	{
		var catalogue = new[]
		{
			("features/toilet", "Toilet.sam"),
			("features/fountain", "Fountain.sam"),
			("features/staff", "Staff.sam"),
			("features/camera", "Camera.sam"),
			("features/pelbin", "PelBin.sam"),
			("shops/coconut", "Coconut.sam"),
			("sideshow/junspray", "Junspray.sam"),
			("rides/bouncy", "Bouncy.sam")
		}
		.Select( item =>
		{
			using var stream = new MemoryStream( data.ReadAllBytes( $"levels/jungle/{item.Item1}/{item.Item2}" ) );
			return new ItemDescriptionFile( stream ).Id;
		} )
		.ToHashSet();

		foreach ( var placed in World().Objects.Where( item => item.IsPlaced ) )
		{
			Assert.IsTrue( catalogue.Contains( placed.CatalogueId ),
				$"catalogue number {placed.CatalogueId} at ({placed.CellX},{placed.CellY}) should be one of the jungle's items" );
		}
	}

	/// <summary>
	/// The whole map is read, and what it is made of.
	///
	/// <para>
	/// The counts are pinned rather than sampled because a cell reader that drifts still produces a full
	/// grid of plausible numbers - the failure looks like data, not like a fault. Every cell of the shipped
	/// park carries a map record, so a cell that reads as unmapped means the walk lost its place.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheMapIsSixteenThousandCellsAndEveryOneIsRead()
	{
		var world = World();

		Assert.IsNull( world.Problem, "nothing should have stopped the walk" );
		Assert.IsTrue( world.ClosedOnTrailer, "reading the map should not have cost the walk its place" );

		Assert.AreEqual( 128 * 128, world.Cells.Count, "cells in the map" );
		Assert.AreEqual( world.Cells.Count, world.Cells.Count( cell => cell.IsMapped ),
			"every cell of this park carries a map record" );

		var types = world.Cells.GroupBy( cell => cell.Type ).ToDictionary( group => group.Key, group => group.Count() );

		Assert.AreEqual( 78, types[1], "cells that are path" );
		Assert.AreEqual( 35, types[4], "cells something is built on" );
		Assert.AreEqual( 4, types[3], "cells that are queue" );
		Assert.AreEqual( 6875, types[0], "cells of type 0" );
		Assert.AreEqual( 9077, types[7], "cells of type 7" );
	}

	/// <summary>
	/// The paths, which are the whole reason for reading the map: a park's walkways are in its save and
	/// nowhere in its ground model, so without this the rides stand in an empty field.
	///
	/// <para>
	/// The avenue is what makes this more than a count. Two neighbouring columns run twelve cells each,
	/// unbroken, from the ride loop down to the row the park entrance sits on - which is the double-wide
	/// path every screenshot of Lost Kingdom shows leading in from the gate. Getting the indexing backwards
	/// (<c>x * 128 + y</c>, which is how the attribute map in <c>base.map</c> is ordered) scatters these
	/// same 78 cells into incoherent blobs, so this is also the test that pins the cell order.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ThePathsRunDownToTheParkEntrance()
	{
		var world = World();

		var paths = Enumerable.Range( 0, 128 )
			.SelectMany( y => Enumerable.Range( 0, 128 ).Select( x => (X: x, Y: y) ) )
			.Where( at => world.CellAt( at.X, at.Y ).Type == 1 )
			.ToArray();

		Assert.AreEqual( 78, paths.Length, "path cells" );

		foreach ( var column in new[] { 47, 48 } )
		{
			var down = paths.Where( at => at.X == column ).Select( at => at.Y ).OrderBy( y => y ).ToArray();

			CollectionAssert.AreEqual( Enumerable.Range( 17, 12 ).ToArray(), down,
				$"the entrance avenue's column at x={column} should be unbroken from y17 to y28" );
		}
	}

	/// <summary>
	/// The cells under the Belly Bounce, which is the check that the map and the object list agree with
	/// each other: the ride's position comes from the thing stream and its footprint from its own
	/// <c>.hmp</c>, and neither of those knows anything about the map.
	///
	/// <para>
	/// Ten of its twelve cells are marked as built on. The two that are not are both in the middle column
	/// and at opposite ends of it, and the four queue cells sit in the row directly beyond one of them -
	/// which is what a ride's way in and way out look like. The types are pinned; naming them entrance and
	/// exit would be a guess, and this asserts the arrangement instead of the meaning.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheBellyBounceMarksItsFootprintAndItsQueue()
	{
		var world = World();

		// The ride stands at (51,23) and its .hmp declares a 3x4 box - see the footprint test above.
		var box = Enumerable.Range( 23, 4 )
			.SelectMany( y => Enumerable.Range( 51, 3 ).Select( x => (X: x, Y: y, world.CellAt( x, y ).Type) ) )
			.ToArray();

		Assert.AreEqual( 10, box.Count( cell => cell.Type == 4 ), "cells of the ride's box marked as built on" );
		Assert.AreEqual( 9, world.CellAt( 52, 23 ).Type, "the near end of the ride's middle column" );
		Assert.AreEqual( 10, world.CellAt( 52, 26 ).Type, "the far end of the ride's middle column" );

		var queue = Enumerable.Range( 49, 4 ).Select( x => world.CellAt( x, 22 ).Type ).ToArray();

		CollectionAssert.AreEqual( new[] { 3, 3, 3, 3 }, queue,
			"the four queue cells should lie in the row beyond the ride's near end" );
	}

	/// <summary>
	/// A path cell says which tile it draws and which way that tile is turned, so neither has to be worked
	/// out from the cells around it.
	///
	/// <para>
	/// This is what says the twelve bytes the original calls <c>mTileData</c> are three dwords rather than
	/// one opaque run: the first splits the map exactly along the boundary the theme's <c>.tct</c> draws
	/// between its <c>PathTex</c> and <c>QueueTex</c> sections, and the third is a quarter turn on every
	/// one of the 16,384 cells and never anything else. A wrong split would have to produce both of those
	/// by accident.
	/// </para>
	/// </summary>
	[TestMethod]
	public void APathCellCarriesTheTileItDrawsAndTheTurnItTakes()
	{
		var world = World();

		foreach ( var cell in world.Cells )
		{
			var expected = cell.Type switch { 1 => 1, 3 => 2, _ => 0 };

			Assert.AreEqual( expected, cell.TileSet,
				$"a cell of type {cell.Type} should draw from tile set {expected}" );

			Assert.IsTrue( cell.TileAngle is 0 or 90 or 180 or 270,
				$"a tile is turned by a quarter, not by {cell.TileAngle} degrees" );
		}

		// PathTex runs 0..21 in the theme's .tct, and every path cell names one of those slots.
		foreach ( var cell in world.Cells.Where( cell => cell.Type == 1 ) )
			Assert.IsTrue( cell.TileIndex is >= 0 and <= 21,
				$"path tile {cell.TileIndex} is outside the PathTex table" );
	}
}
