using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// <b>An item with a map offset (the Huge Hollow Rock, the Dino Karts Tunnel) is marked, refused, carried and sounded on the cells its
/// model covers</b>, the anchor staying the cells' owner (Q744). The original's purchase of the rock typed 23 cells, the model's 5 x 5 at
/// the row above the anchor less the picture's two empty corners (Q888, <c>q888/orig/run1/</c>), and the saved parks hold exactly the
/// picture's non-empty cells as type 4 for every rock, tunnel and item 1426 in 861 distinct files (Q744's census, <c>q744/census2.txt</c>).
/// </summary>
[TestClass]
public class ParkMapOffsetCellsTests
{
	private const string Theme = "jungle";
	private const int Rock = 1427, Tunnel = 1501, Bones = 1426;

	private BaseFileSystem data = null!;

	private (ParkWorld World, ParkItemCatalogue Catalogue) Park()
	{
		data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return (new ParkWorld( new SaveReader( stream ).ReadFile() ), new ParkItemCatalogue( Theme, data ));
	}

	/// <summary>The first anchor in the park's open ground the placement test accepts the item at, turned 0.</summary>
	private static (int X, int Y) FreeAnchor( ParkState state, ParkItemCatalogue.Item item )
		=> Enumerable.Range( 0, 128 * 128 ).Select( i => (X: i % 128, Y: i / 128) )
			.Where( cell => cell.X is >= 12 and < 90 && cell.Y is >= 12 and < 80 )
			.First( cell => ParkBuilding.PlacementRefusal( state, item, cell.X, cell.Y, 0 ) == null );

	/// <summary>
	/// <b>The cells an item marks are its picture's non-empty cells, swept from the cell its model stands on</b>: inside the model's
	/// rectangle, every cell of it but the picture's empty ones, at every angle, for every item of the theme. The rock anchored at
	/// (69,24) is the 23 cells the original's own purchase typed (Q888).
	/// </summary>
	/// <remarks><b>Mutations:</b> the sweep from the anchor instead of the model's cell moves every rock's cells a row; the empty
	/// kind swept as a cell types 25.</remarks>
	[TestMethod]
	public void AnItemMarksItsPicturesCellsFromTheCellItsModelStandsOn()
	{
		var (_, catalogue) = Park();

		var checkedItems = 0;
		var withEmpty = new List<int>();

		foreach ( var item in catalogue.All.Where( i => i.Shape != null ) )
		{
			var shape = item.Shape!;

			// A picture that is not the item's own width and depth (the fixed items') is marked as the rectangle, as before.
			if ( shape.Width != item.Width || shape.Depth != item.Depth )
			{
				Assert.IsTrue( ParkObjects.CellsOf( ParkObjects.FloorFootprint( item, 40, 40, 0 ) ).SequenceEqual( ParkObjects.CoveredCells( item, 40, 40, 0 ) ), item.Name );
				continue;
			}

			var empty = Enumerable.Range( 0, shape.Depth ).SelectMany( row => Enumerable.Range( 0, shape.Width ).Select( column => (column, row) ) )
				.Count( cell => shape.KindAt( cell.column, cell.row ) == 0 );

			foreach ( var angle in new[] { 0, 90, 180, 270 } )
			{
				var rectangle = ParkObjects.CellsOf( ParkObjects.FloorFootprint( item, 40, 40, angle ) ).ToHashSet();
				var marked = ParkObjects.CoveredCells( item, 40, 40, angle ).ToList();

				Assert.AreEqual( marked.Count, marked.Distinct().Count(), $"{item.Name} turned {angle}: no cell twice" );
				Assert.IsTrue( marked.All( rectangle.Contains ), $"{item.Name} turned {angle}: inside the model's rectangle" );
				Assert.AreEqual( shape.Width * shape.Depth - empty, marked.Count, $"{item.Name} turned {angle}: the non-empty cells" );
			}

			++checkedItems;

			if ( empty > 0 )
				withEmpty.Add( item.Id );
		}

		Assert.IsTrue( checkedItems >= 55, $"{checkedItems} items with a picture" );
		// Item 1426, the rock and the tunnel are the ones the 861 saved parks place (Q744's census); the Large Rock Pillar (1422) is placed in
		// none of them and the three tools (Mystery, Buy Land and Clear Land: UI type 4, no buy row, pictures of empty cells only) are never
		// built, so the placer's rule on an empty cell is measured on the first three alone.
		CollectionAssert.AreEquivalent( new[] { 100, 101, 102, 1422, Bones, Rock, Tunnel }, withEmpty, "the items whose picture has empty cells" );

		Assert.IsTrue( catalogue.TryGet( Rock, out var rock ) );

		var expected = Enumerable.Range( 70, 4 ).Select( x => (x, 23) )
			.Concat( Enumerable.Range( 24, 3 ).SelectMany( y => Enumerable.Range( 69, 5 ).Select( x => (x, y) ) ) )
			.Concat( Enumerable.Range( 69, 4 ).Select( x => (x, 27) ) ).ToHashSet();

		Assert.IsTrue( expected.SetEquals( ParkObjects.CoveredCells( rock, 69, 24, 0 ) ), "the 23 cells the original's own purchase typed" );
	}

	/// <summary>
	/// <b>The refusal asks the model's cells, not the anchor's rectangle</b>: a path on the model's top row, a row above the anchor,
	/// refuses the rock; a path on the anchor's rectangle's last row, which the model does not cover, does not.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>PlacementRefusal</c> over <see cref="ParkObjects.FootprintAt"/> fails both halves.</remarks>
	[TestMethod]
	public void ARockIsRefusedOnTheModelsCellsAndNotTheAnchorsRectangle()
	{
		var (world, catalogue) = Park();
		var state = new ParkState( world );

		Assert.IsTrue( catalogue.TryGet( Rock, out var rock ) );

		var (x, y) = FreeAnchor( state, rock );

		var was = new Dictionary<(int, int), ParkWorld.MapCell>();

		void PathAt( int cellX, int cellY )
		{
			was[(cellX, cellY)] = state.Record( cellX, cellY );
			state.SetRecord( cellX, cellY, was[(cellX, cellY)] with { TileSet = ParkPaths.PathTileSet } );
		}

		void GroundAt( int cellX, int cellY ) => state.SetRecord( cellX, cellY, was[(cellX, cellY)] );

		PathAt( x + 2, y - 1 );
		StringAssert.Contains( ParkBuilding.PlacementRefusal( state, rock, x, y, 0 ), $"({x + 2},{y - 1}) is a path", "the model's top row" );
		GroundAt( x + 2, y - 1 );

		PathAt( x + 2, y + 4 );
		Assert.IsNull( ParkBuilding.PlacementRefusal( state, rock, x, y, 0 ), $"the anchor's last row is not the model's ({ParkBuilding.PlacementRefusal( state, rock, x, y, 0 )})" );
		GroundAt( x + 2, y + 4 );

		// The picture's empty corners are not asked either.
		PathAt( x, y - 1 );
		Assert.IsNull( ParkBuilding.PlacementRefusal( state, rock, x, y, 0 ), "an empty corner of the picture" );
	}

	/// <summary>
	/// <b>The carried rock's squares stand on the model's cells</b>: 23 squares, the picture's row nought (<c>m_front</c>) on the row above
	/// the anchor, none on the anchor rectangle's last row.
	/// </summary>
	/// <remarks><b>Mutation:</b> the squares swept from the anchor put four on the anchor's last row and none above it.</remarks>
	[TestMethod]
	public void ACarriedRockShowsItsSquaresOnTheModelsCells()
	{
		var (world, catalogue) = Park();
		var state = new ParkState( world );

		Assert.IsTrue( catalogue.TryGet( Rock, out var rock ) );

		var squares = ParkBuilding.CarrySquares( state, rock, 69, 24, 0 );

		Assert.IsFalse( rock.HasEntrance, "the rock has no ends, so the squares are the body's alone" );
		Assert.IsTrue( ParkObjects.CoveredCells( rock, 69, 24, 0 ).ToHashSet().SetEquals( squares.Select( square => (square.X, square.Y) ) ),
			"on the cells the purchase will type" );
		Assert.AreEqual( 23, squares.Count );
		Assert.AreEqual( 4, squares.Count( square => square.Y == 23 ), "the row above the anchor" );
		Assert.AreEqual( 0, squares.Count( square => square.Y == 28 ), "and not the anchor rectangle's last" );
	}

	/// <summary>
	/// <b>A sale clears exactly the cells the purchase marked</b>, row above the anchor and all, and the rock's voice is sounded at the
	/// middle of the model's rectangle: one row north of where the anchor's rectangle put it.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Sell</c> clearing the anchor's rectangle leaves the row above marked; the sound box from
	/// <see cref="ParkObjects.FootprintAt"/> moves the voice a row south.</remarks>
	[TestMethod]
	public void ASaleClearsTheCellsThePurchaseMarkedAndTheVoiceIsAtTheModelsMiddle()
	{
		var (world, catalogue) = Park();
		var state = new ParkState( world );

		Assert.IsTrue( catalogue.TryGet( Rock, out var rock ) );

		var (x, y) = FreeAnchor( state, rock );
		var thingId = state.NextThingId();
		var placed = ParkBuilding.Constructed( rock, thingId, x, y, 0, 0, 0, ParkWorld.BuiltWhen.At( state.CalendarNow ) );

		state.AddObject( placed );
		ParkBuilding.Stamp( state, ParkObjects.CoveredCells( rock, x, y, 0 ), x, y );

		var marked = ParkObjects.CoveredCells( rock, x, y, 0 ).ToHashSet();
		var before = marked.Count( cell => state.Record( cell.X, cell.Y ).Type == CellEdge.Footprint );

		Assert.AreEqual( 23, before );
		StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, null, thingId ), "sell: 'Huge Hollow Rock'" );
		Assert.AreEqual( 0, ParkObjects.CellsOf( (x - 1, y - 2, x + 6, y + 6) ).Count( cell => state.Record( cell.X, cell.Y ).Type == CellEdge.Footprint ),
			"no cell round the rock is marked after the sale, the row above the anchor included" );

		// The voice: the model's rectangle (69..73, 23..27) at cells of ten units.
		var middle = ParkObjects.BoxMiddle( ParkObjects.SoundBoxOf( rock, placed with { RawX = 69 << 8, RawY = 24 << 8 } ), 0f );

		Assert.AreEqual( new Vector3( 715f, 255f, 0f ), middle, "the model's rectangle's middle" );
		Assert.AreNotEqual( middle, ParkObjects.BoxMiddle( ParkObjects.FootprintAt( rock, 69, 24, 0 ), 0f ), "which is not the anchor's rectangle's" );
	}

	/// <summary>
	/// <b>The tunnel turned a quarter marks the rotated model cells</b>: its picture's 12 less 2 empty, anchored at (17,21) turned 90, all
	/// inside the rectangle (16..19, 19..21) the saved park holds (Q744's census: item 1501, 34 things, every one owning exactly its
	/// picture's non-empty cells).
	/// </summary>
	[TestMethod]
	public void ATurnedTunnelMarksTheRotatedModelsCells()
	{
		var (_, catalogue) = Park();

		Assert.IsTrue( catalogue.TryGet( Tunnel, out var tunnel ) );

		var marked = ParkObjects.CoveredCells( tunnel, 17, 21, 90 ).ToList();

		Assert.AreEqual( (16, 19, 19, 21), ParkObjects.FloorFootprint( tunnel, 17, 21, 90 ) );
		Assert.AreEqual( 10, marked.Count );
		Assert.IsTrue( marked.All( cell => cell.X is >= 16 and <= 19 && cell.Y is >= 19 and <= 21 ) );
	}

	/// <summary>
	/// <b>A sold tunnel clears the cells its put-down marked</b>: the add-on's stamp (<see cref="ParkTrackAddOn.Stamp"/>) types the picture's
	/// non-empty cells from the model's cell, turned, owned by the anchor, and the sale takes exactly those off, none left on a thing
	/// that is gone (the cells the anchor's rectangle missed, a row or column off the model's).
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Sell</c> clearing the anchor's rectangle leaves the model's cells outside it marked.</remarks>
	[TestMethod]
	public void ASoldTunnelClearsTheCellsItsPutDownMarked()
	{
		var (world, catalogue) = Park();
		var state = new ParkState( world );

		Assert.IsTrue( catalogue.TryGet( Tunnel, out var tunnel ) );

		var thingId = state.NextThingId();
		var placed = ParkBuilding.Constructed( tunnel, thingId, 17, 21, 90, 0, 0, ParkWorld.BuiltWhen.At( state.CalendarNow ) );

		state.AddObject( placed );
		ParkTrackAddOn.Stamp( state, tunnel, 17, 21, 90 );

		var marked = ParkObjects.CellsOf( (14, 17, 21, 23) ).Where( cell => state.Record( cell.X, cell.Y ).Type == CellEdge.Footprint ).ToHashSet();

		Assert.IsTrue( ParkObjects.CoveredCells( tunnel, 17, 21, 90 ).ToHashSet().SetEquals( marked ), "the put-down marked the covered cells" );
		Assert.IsFalse( marked.SetEquals( ParkObjects.CellsOf( ParkObjects.FootprintAt( tunnel, 17, 21, 90 ) ) ), "which are not the anchor's rectangle" );

		StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, null, thingId ), "sell: 'Dino Karts Tunnel'" );
		Assert.AreEqual( 0, ParkObjects.CellsOf( (14, 17, 21, 23) ).Count( cell => state.Record( cell.X, cell.Y ).Type == CellEdge.Footprint ), "none left marked" );
	}
}
