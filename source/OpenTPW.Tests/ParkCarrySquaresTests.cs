using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What is drawn while a thing is carried, before it is put down: the original's placer draw pass,
/// <c>FUN_00528a70( x, y, 4, angle, 0, 0 )</c>, one square a cell of the item's shape picture with <c>m_front</c> along
/// row nought, then <c>m_enter</c> (or <c>m_inout</c>) before the entrance and <c>m_exit</c> before the exit, each turned
/// by a compass bit (<c>FUN_0053df30</c>). docs/exe/park-engine.md, "While a thing is carried".
/// </summary>
[TestClass]
public class ParkCarrySquaresTests
{
	private BaseFileSystem data = null!;

	private ParkWorld World()
	{
		data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>An item drawn by a shape picture, as the description reader reads it, with a queue or not.</summary>
	private static ParkItemCatalogue.Item Item( string shape, bool hasQueue, int width, int depth )
	{
		var described = new ItemDescriptionFile( "Info.Id 1\nInfo.Shape\n---\n" + shape + "---\n" );

		return new ParkItemCatalogue.Item( 1, "carried", "", "", width, depth, null, HasQueue: hasQueue,
			HasEntrance: described.HasEntrance, EntryDeltaX: described.EntryDeltaX, EntryDeltaY: described.EntryDeltaY,
			ExitDeltaX: described.ExitDeltaX, ExitDeltaY: described.ExitDeltaY,
			EntryDirection: described.EntryDirection, ExitDirection: described.ExitDirection, Shape: described.Shape );
	}

	/// <summary>The Belly Bounce's picture: <c>2</c> on its last row, <c>S</c> on its first.</summary>
	private static ParkItemCatalogue.Item BellyBounce() => Item( "*S*\n***\n***\n*2*\n", hasQueue: true, 3, 4 );

	private static string Census( IEnumerable<ParkBuilding.CarrySquare> squares )
		=> string.Join( ";", squares.Select( square => $"{square.X},{square.Y},{square.Marker},{square.Turn:x2}" ) );

	/// <summary>
	/// <b>Row nought wears <c>m_front</c>, the rest of the body blue, and the cells before the ends <c>m_enter</c> and
	/// <c>m_exit</c></b>, in the original's order: rows from nought, columns within each, then the entrance's cell and
	/// the exit's. The Belly Bounce on bare ground east of the stock park's own, where a click builds it (Q633's runs).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>m_front</c> on the picture's first row (row three) instead; the entrance's cell one step
	/// along its own bit (into the ride) rather than against it; <c>m_enter</c> for a thing with a queue swapped for
	/// <c>m_inout</c>; no turn on the front row.
	/// </remarks>
	[TestMethod]
	public void ACarriedRideShowsItsFrontRowAndItsEnds()
	{
		var state = new ParkState( World() );

		Assert.IsNull( ParkBuilding.PlacementRefusal( state, BellyBounce(), 57, 23, 0 ), "a click there builds it" );

		Assert.AreEqual(
			"57,23,6,01;58,23,6,01;59,23,6,01;"
			+ "57,24,0,00;58,24,0,00;59,24,0,00;57,25,0,00;58,25,0,00;59,25,0,00;"
			+ "57,26,0,00;58,26,0,00;59,26,0,00;"
			+ "58,22,3,01;58,27,4,01",
			Census( ParkBuilding.CarrySquares( state, BellyBounce(), 57, 23, 0 ) ) );
	}

	/// <summary>
	/// <b>A quarter turn turns every bit with it</b>: the front row's to west's <c>0x40</c>, the entrance's <c>0x10</c> to
	/// east's <c>0x04</c>, so its cell is a step west of the entrance and <c>m_enter</c> wears west's turn. The rows run
	/// along x now: row nought is the column x = 57.
	/// </summary>
	/// <remarks><b>Mutation:</b> the front's turn left at north's <c>0x01</c> whatever the angle.</remarks>
	[TestMethod]
	public void AQuarterTurnTurnsTheSquaresWithIt()
	{
		var state = new ParkState( World() );
		var squares = ParkBuilding.CarrySquares( state, BellyBounce(), 57, 23, 90 );

		Assert.IsNull( ParkBuilding.PlacementRefusal( state, BellyBounce(), 57, 23, 90 ), "a click there builds it" );
		Assert.AreEqual( "57,23,6,40;57,22,6,40;57,21,6,40", Census( squares.Take( 3 ) ), "row nought, turned west" );
		Assert.AreEqual( "56,22,3,40;61,22,4,40", Census( squares.TakeLast( 2 ) ), "the ends' cells, turned the same way" );
	}

	/// <summary>
	/// <b>One refusal reds every square</b>, the ends' cells too, as the original's failed test pass does; and a thing
	/// with no queue shows <c>m_inout</c> before its one way in and nothing before an exit.
	/// </summary>
	/// <remarks><b>Mutation:</b> a refused footprint left wearing its markers.</remarks>
	[TestMethod]
	public void ARefusedPlacementIsAllRedAndAShopShowsItsWayInAndOut()
	{
		var state = new ParkState( World() );

		// (51,25) is on the stock park's own Belly Bounce: another will not go down there.
		var refused = ParkBuilding.CarrySquares( state, BellyBounce(), 51, 25, 0 );

		Assert.IsNotNull( ParkBuilding.PlacementRefusal( state, BellyBounce(), 51, 25, 0 ), "a click there is refused" );
		Assert.AreEqual( 14, refused.Count );
		Assert.IsTrue( refused.All( square => square is { Marker: ParkPathBuilding.MarkerRed, Turn: 0 } ), Census( refused ) );

		var shop = Item( "*\n2\n", hasQueue: false, 1, 2 );

		Assert.AreEqual( "60,23,6,01;60,24,0,00;60,22,7,01", Census( ParkBuilding.CarrySquares( state, shop, 60, 23, 0 ) ) );
	}

	/// <summary>
	/// <b>A station cell points <c>m_direct</c> at the cell its facing names</b>, both turned by south's bit, and a
	/// station pointing off the map refuses the whole thing.
	/// </summary>
	[TestMethod]
	public void AStationCellPointsAtItsNeighbour()
	{
		var state = new ParkState( World() );
		var station = Item( ">\n", hasQueue: false, 1, 1 );

		Assert.AreEqual( "60,23,0,10;61,23,5,10", Census( ParkBuilding.CarrySquares( state, station, 60, 23, 0 ) ) );
		Assert.IsTrue( ParkBuilding.CarrySquares( state, station, 127, 23, 0 )
			.All( square => square.Marker == ParkPathBuilding.MarkerRed ), "off the map's edge" );
	}

	/// <summary>
	/// <b>Every cell of every Lost Kingdom item's picture, turned as <see cref="ParkBuilding.CarrySquares"/> turns it, falls
	/// inside the footprint the click stamps</b>, at all four angles: the picture's integer turn
	/// (<see cref="ParkBuilding.RotateDelta"/>, the original's <c>MapDelta::Rotate</c>) and the model's own agree, so the
	/// squares stand where the thing will.
	/// </summary>
	[TestMethod]
	public void EveryItemsPictureTurnsOntoItsOwnFootprint()
	{
		World();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var cells = 0;

		foreach ( var item in catalogue.All.Where( item => item.Shape != null ) )
		{
			var shape = item.Shape!;

			foreach ( var angle in new[] { 0, 90, 180, 270 } )
			{
				var (left, top, right, bottom) = ParkObjects.FootprintAt( item, 60, 60, angle );

				for ( var row = 0; row < shape.Depth; ++row )
				{
					for ( var column = 0; column < shape.Width; ++column )
					{
						if ( shape.KindAt( column, row ) == 0 )
							continue;

						var (dx, dy) = ParkBuilding.RotateDelta( column, row, angle );
						var (x, y) = (60 + dx, 60 + dy);

						Assert.IsTrue( x >= left && x <= right && y >= top && y <= bottom,
							$"{item.Name} at {angle}: ({x},{y}) outside ({left},{top})-({right},{bottom})" );
						++cells;
					}
				}
			}
		}

		Assert.IsTrue( cells > 100, $"{cells} cells" );
	}

	/// <summary>
	/// <b>The pointer's cell centres the footprint</b>, as the original's placer does: measured in the original under
	/// Proton (<c>q633/orig/c-3-over.squares</c>), a Belly Bounce carried with the pointer on (48,31) laid these fourteen
	/// squares, in this order, with these textures and turns.
	/// </summary>
	/// <remarks><b>Mutation:</b> the anchor left on the pointer's cell, as before.</remarks>
	[TestMethod]
	public void ThePointersCellCentresTheFootprintAsTheOriginalsDoes()
	{
		var state = new ParkState( World() );
		var (x, y) = ParkBuilding.AnchorUnder( BellyBounce(), 48, 31, 0 );

		Assert.AreEqual( (47, 29), (x, y ) );
		Assert.AreEqual(
			"47,29,6,01;48,29,6,01;49,29,6,01;47,30,0,00;48,30,0,00;49,30,0,00;47,31,0,00;48,31,0,00;49,31,0,00;"
			+ "47,32,0,00;48,32,0,00;49,32,0,00;48,28,3,01;48,33,4,01",
			Census( ParkBuilding.CarrySquares( state, BellyBounce(), x, y, 0 ) ) );
	}

	/// <summary>
	/// <b>At every angle the anchor's picture covers the cells the original's sweep visits</b> for the same pointer: the
	/// sweep's own per-angle cells (<c>FUN_00528a70</c>, <c>0x00528f62</c>..; its box offsets from <c>(5 − w) / 2</c> and
	/// <c>(5 − d) / 2</c>), written out here as the decompiled loop has them, against <see cref="ParkBuilding.AnchorUnder"/>
	/// turned by <see cref="ParkBuilding.RotateDelta"/>, for every Lost Kingdom item.
	/// </summary>
	[TestMethod]
	public void TheAnchorCoversTheCellsTheOriginalsSweepVisitsAtEveryAngle()
	{
		World();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var cells = 0;

		foreach ( var item in catalogue.All.Where( item => item.Shape != null ) )
		{
			var shape = item.Shape!;
			var (w, d) = (shape.Width, shape.Depth);

			// The placer's locals: param_1 and param_2 after the centring, iVar16, iVar15 and the four offsets.
			var (p1, p2) = (60 - (w / 2), 60 - (d / 2));
			var (i16, i15) = ((5 - w) / 2, (5 - d) / 2);
			var (s3c, s38, s14) = ((i16 * -2) - 1, i15 - i16, (i15 * -2) - 1);
			var (v10, v20) = (-1 - i16 - i15, i16 - i15);
			var s34 = v10;

			foreach ( var angle in new[] { 0, 90, 180, 270 } )
			{
				var (ax, ay) = ParkBuilding.AnchorUnder( item, 60, 60, angle );

				for ( var row = 0; row < d; ++row )
				{
					for ( var column = 0; column < w; ++column )
					{
						var original = angle switch
						{
							90 => (s38 + row + p1, s34 - column + 5 + p2),
							180 => (s3c - column + 5 + p1, s14 - row + 5 + p2),
							270 => (v10 - row + 5 + p1, v20 + column + p2),
							_ => (column + p1, row + p2)
						};
						var (dx, dy) = ParkBuilding.RotateDelta( column, row, angle );

						Assert.AreEqual( original, (ax + dx, ay + dy ), $"{item.Name} at {angle}, column {column}, row {row}" );
						++cells;
					}
				}
			}
		}

		Assert.IsTrue( cells > 100, $"{cells} cells" );
	}

	/// <summary>
	/// <b>The texture turns with the square's bit</b> as <c>FUN_0053df30</c> picks its UV pairs: east's bit puts (0, 1) on
	/// the cell's first corner, south's (1, 1), west's (1, 0), and north's or none leave (0, 0) there.
	/// </summary>
	[TestMethod]
	public void TheTextureTurnsWithTheSquaresBit()
	{
		string Uvs( int turn ) => string.Join( " ", ParkBuildMarkers.TurnedUvs( turn ).Select( uv => $"{uv.X:0}{uv.Y:0}" ) );

		Assert.AreEqual( "00 10 11 01", Uvs( 0 ) );
		Assert.AreEqual( "00 10 11 01", Uvs( 0x01 ) );
		Assert.AreEqual( "01 00 10 11", Uvs( 0x04 ) );
		Assert.AreEqual( "11 01 00 10", Uvs( 0x10 ) );
		Assert.AreEqual( "10 11 01 00", Uvs( 0x40 ) );
	}

	/// <summary>
	/// <b>The footprint glides</b>: each step closes the given share of the way, as the original's halving does, and the
	/// squares are drawn as far from their cells as the glide's point still is from the pointer's cell, behind it.
	/// </summary>
	[TestMethod]
	public void TheGlideClosesItsShareOfTheWay()
	{
		Assert.AreEqual( (15f, 30f), ParkBuildMarkers.Glide( (10f, 40f), (20f, 20f), 0.5f ) );
		Assert.AreEqual( (20f, 20f), ParkBuildMarkers.Glide( (10f, 40f), (20f, 20f), 1f ) );
		Assert.AreEqual( (-5f, 10f), ParkBuildMarkers.GlideOffset( (15f, 30f), (20f, 20f) ), "drawn back toward where it came from" );
	}

	/// <summary>
	/// <b>A carried thing that will not go down reds a followed member's patrol edges</b>, which read the placer's failure
	/// first; one that will leaves them blue.
	/// </summary>
	[TestMethod]
	public void ARefusedCarryRedsThePatrolEdges()
	{
		var state = new ParkState( World() );

		Assert.IsTrue( ParkBuildMarkers.CarriedRefused( ParkBuilding.CarrySquares( state, BellyBounce(), 51, 25, 0 ) ) );
		Assert.IsFalse( ParkBuildMarkers.CarriedRefused( ParkBuilding.CarrySquares( state, BellyBounce(), 57, 23, 0 ) ) );
		Assert.IsFalse( ParkBuildMarkers.CarriedRefused( [] ), "nothing carried" );

		Assert.IsTrue( ParkBuildMarkers.PatrolLatch( false, ParkBuilding.CarrySquares( state, BellyBounce(), 51, 25, 0 ) ), "the edges red" );
		Assert.IsFalse( ParkBuildMarkers.PatrolLatch( false, ParkBuilding.CarrySquares( state, BellyBounce(), 57, 23, 0 ) ), "blue" );
		Assert.IsTrue( ParkBuildMarkers.PatrolLatch( true, [] ), "the latch alone" );
	}
}
