using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The two records the walk already sized correctly and then stepped over: the twenty-three bytes that
/// close a map cell, and the catalogue object's own fields past <c>mId</c>.
///
/// <para>
/// <b>Most of the litter block is nought in the shipped park, so the tests that matter are the ones that
/// could not pass at a wrong offset.</b> A park nobody has played holds no litter, and a suite that only
/// asserted nought would pass identically with the reading deleted - the shape of test this project has
/// been caught by before. Three assertions carry the weight instead: the saved status byte is checked
/// against <c>base.map</c>, a completely separate file, cell for cell; the closing short is checked
/// against the objects' own positions; and the object list is walked as a chain.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkDecodedRecordTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private AttributeMapFile Attributes()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.map" ) );
		return new AttributeMapFile( stream );
	}

	/// <summary>
	/// <b>The strongest check here, because it crosses files.</b> The byte at offset 45 of a cell's map
	/// record is the same value <c>base.map</c> carries for that cell - a file parsed by different code,
	/// with a different header, indexed the other way round (<c>x * 128 + y</c> against the save's
	/// <c>y * 128 + x</c>). Sixteen thousand three hundred and eighty-four agreements cannot come of a
	/// misaligned read, and the opposite indexing means even a transposed park would fail.
	/// </summary>
	[TestMethod]
	public void TheSavedStatusByteIsTheAttributeMapItself()
	{
		var world = Park();
		var attributes = Attributes();

		Assert.AreEqual( 128, attributes.Width, "the attribute map is 128 across" );

		var disagreements = new List<string>();

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var saved = world.CellAt( x, y ).StatusFlags;
				var attribute = attributes.Cells[(x * attributes.Width) + y];

				if ( saved != attribute && disagreements.Count < 10 )
					disagreements.Add( $"({x},{y}) save {saved} map {attribute}" );
			}
		}

		Assert.AreEqual( 0, disagreements.Count,
			$"every cell's saved status byte should be its attribute - {string.Join( "; ", disagreements )}" );
	}

	/// <summary>
	/// And the same reading is not vacuously right by being all nought: 1,495 of the 16,384 cells carry a
	/// non-zero status, over exactly the eight values the attribute map uses.
	/// </summary>
	[TestMethod]
	public void TheStatusByteCarriesRealValuesRatherThanNought()
	{
		var cells = Park().Cells;

		Assert.AreEqual( 16384, cells.Count, "the map is 128 by 128" );
		Assert.AreEqual( 1495, cells.Count( cell => cell.StatusFlags != 0 ),
			"cells with a non-zero status byte" );

		CollectionAssert.AreEquivalent(
			new[] { 0, 1, 3, 8, 17, 128, 144, 148 },
			cells.Select( cell => (int)cell.StatusFlags ).Distinct().OrderBy( value => value ).ToArray(),
			"the status byte takes the attribute map's own eight values and no others" );
	}

	/// <summary>
	/// <b>Nothing has been dropped in a park nobody has played</b>, which is a fact about the file and not
	/// a gap in the reading - the two tests above are what prove the block is read where it really sits.
	/// Worth pinning because it decides whether a handyman built against it would have anything to do.
	/// </summary>
	[TestMethod]
	public void NobodyHasDroppedAnythingInAParkNobodyHasPlayed()
	{
		var cells = Park().Cells;

		Assert.AreEqual( 0, cells.Count( cell => cell.HasLitter ), "cells holding litter" );
		Assert.AreEqual( 0, cells.Count( cell => cell.LitterCollector != 0 ), "cells claimed by a handyman" );
		Assert.AreEqual( 0, cells.Count( cell => cell.TimeMarkedForLitterCollection != 0 ), "cells marked for collection" );
		Assert.AreEqual( 0, cells.Count( cell => cell.PylonIndex != 0 ), "cells carrying a pylon" );
	}

	/// <summary>
	/// The short that closes the record is occupancy: <b>every one of the eleven placed catalogue objects
	/// names itself on its own cell</b>. That is the assertion that identified the field, and it is a
	/// falsifying one - a misread short would have to land on each object's own id, at each object's own
	/// cell, eleven times over.
	/// </summary>
	[TestMethod]
	public void EveryPlacedObjectNamesItselfOnTheCellItStandsOn()
	{
		var world = Park();
		var placed = world.Objects.Where( o => o.IsPlaced ).ToArray();

		Assert.AreEqual( 11, placed.Length, "the shipped park's placed objects" );

		foreach ( var o in placed )
		{
			Assert.AreEqual( o.ThingId, world.CellAt( o.CellX, o.CellY ).Occupant,
				$"object {o.ThingId} stands at ({o.CellX},{o.CellY}) and that cell should name it" );
		}

		// And occupancy is not simply "every cell", which would make the check above free.
		Assert.AreEqual( 24, world.Cells.Count( cell => cell.IsOccupied ),
			"twenty-four cells are occupied - the eleven objects, twelve people, and the unplaced sentinel at (0,0)" );
	}

	/// <summary>
	/// <c>mNext</c> at file offset 208 is the object list's own link. Walked from the header's
	/// <c>mFirstObject</c> it reaches every object exactly once and stops on nought - which a wrong offset
	/// could not do, because garbage does not terminate.
	/// </summary>
	[TestMethod]
	public void TheObjectListIsOneChainThatReachesEveryObjectAndStops()
	{
		var world = Park();
		var byId = world.Objects.ToDictionary( o => o.ThingId );

		Assert.AreEqual( 14, byId.Count, "the shipped park's objects, placed and not" );
		Assert.IsTrue( byId.ContainsKey( world.FirstObject ),
			$"the header's mFirstObject ({world.FirstObject}) should name one of them" );

		var seen = new List<int>();

		for ( var id = world.FirstObject; id != 0; )
		{
			Assert.IsFalse( seen.Contains( id ), $"the chain should not come back to object {id}" );
			Assert.IsTrue( byId.ContainsKey( id ), $"the chain should not leave the object list at {id}" );

			seen.Add( id );
			id = byId[id].NextObject;

			Assert.IsTrue( seen.Count <= byId.Count, "the chain should not run longer than the list" );
		}

		CollectionAssert.AreEquivalent( byId.Keys.ToArray(), seen.ToArray(),
			"the chain should reach every object exactly once" );
	}

	/// <summary>
	/// The flags byte at file offset 58. <b>Three toilets and one rest area</b>, and the three toilets are
	/// three copies of one catalogue item standing in a row - which is what says the bit is being read
	/// rather than that a bit happens to be set.
	/// </summary>
	[TestMethod]
	public void ThreeToiletsStandInARowAndOneRestAreaBesideThem()
	{
		var objects = Park().Objects;

		var toilets = objects.Where( o => o.IsToilet ).OrderBy( o => o.CellY ).ToArray();
		var restAreas = objects.Where( o => o.IsRestArea ).ToArray();

		Assert.AreEqual( 3, toilets.Length, "toilets in the shipped park" );
		Assert.IsTrue( toilets.All( o => o.CatalogueId == 1402 ),
			"all three are the same catalogue item, which is what three toilets in a row should be" );
		CollectionAssert.AreEqual( new[] { 15, 16, 17 }, toilets.Select( o => o.CellY ).ToArray(),
			"and they stand in one column, at rows 15, 16 and 17" );
		Assert.IsTrue( toilets.All( o => o.CellX == 55 ), "all in column 55" );

		Assert.AreEqual( 1, restAreas.Length, "rest areas in the shipped park" );
		Assert.AreEqual( 1411, restAreas[0].CatalogueId, "the rest area's catalogue item" );
		Assert.AreEqual( 58, restAreas[0].CellX, "rest area x" );
		Assert.AreEqual( 16, restAreas[0].CellY, "rest area y" );

		// Not every object is flagged, so the two above are readings rather than a constant.
		Assert.IsTrue( objects.Count( o => o.Flags != 0 ) < objects.Count,
			"some objects carry no flags at all" );
	}

	/// <summary>
	/// <c>mEntryPos</c> at file offset 206 is a cell index in the save's own <c>y * 128 + x</c>, and it
	/// lands beside the object it belongs to. Nine of the eleven are immediately adjacent and the other
	/// two are two cells out, which is what a wider footprint looks like - so the bound is two, and a
	/// field that was not a cell index would miss it by the width of the map.
	/// </summary>
	[TestMethod]
	public void EachObjectsEntryCellSitsBesideTheObject()
	{
		var world = Park();

		foreach ( var o in world.Objects.Where( o => o.IsPlaced ) )
		{
			var entryX = o.EntryPos % ParkWorld.MapSize;
			var entryY = o.EntryPos / ParkWorld.MapSize;
			var distance = System.Math.Abs( entryX - o.CellX ) + System.Math.Abs( entryY - o.CellY );

			Assert.IsTrue( distance is > 0 and <= 2,
				$"object {o.ThingId} at ({o.CellX},{o.CellY}) has its entry at ({entryX},{entryY}), {distance} away" );
		}
	}
}
