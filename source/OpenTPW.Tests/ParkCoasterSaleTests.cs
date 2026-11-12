using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A coaster's pylons follow its sale, its purchase and its move (Q951; <c>docs/exe/park-engine.md</c>, "A sold coaster's pylons"): the
/// sale's destructor destroys the coaster and every node's pylon (<c>Coaster_Destroy</c>, <c>0x00436fa0</c>), and a coaster put down
/// is made with its station's two pylons and no track (<c>Coaster_Create</c>, <c>0x004368f0</c>). The pylons want a graphics device, so
/// <see cref="ParkBuilding.StandPylons"/> and <see cref="ParkBuilding.TakeDownPylons"/> stand in for them and record what was asked.
/// </summary>
[TestClass]
public class ParkCoasterSaleTests
{
	private const int TempleOfGloom = 1180;

	private BaseFileSystem data = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;
	private readonly List<Entity> made = [];
	private readonly List<string> asked = [];

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = GameData.Required();
		FileSystem = data;
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		shipped = new ParkWorld( new SaveReader( stream ).ReadFile() );
		catalogue = new ParkItemCatalogue( "jungle", data );
		Unimplemented.Forget();

		ParkBuilding.StandThing = _ => true;
		ParkBuilding.StandPylons = ( placed, item ) =>
		{
			asked.Add( $"up {placed.ThingId} {item.Id} ({placed.CellX},{placed.CellY})" );
			return 2;
		};
		ParkBuilding.TakeDownPylons = thing =>
		{
			asked.Add( $"down {thing}" );
			return 2;
		};
	}

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		ParkBuilding.StandThing = null;
		ParkBuilding.StandPylons = null;
		ParkBuilding.TakeDownPylons = null;
		ParkBuilding.Drop();
		ParkBuildMode.Forget();

		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
		Unimplemented.Forget();
	}

	/// <summary>The shipped park, its rides bound, with the first item of the kind bought at the first cell that takes it.</summary>
	private (ParkState State, ParkRides Rides, int Thing, (int X, int Y) Cell) BuyFirst( Func<ParkItemCatalogue.Item, bool> kind )
	{
		var state = new ParkState( shipped );
		var rides = new ParkRides( "jungle", shipped, catalogue, data );

		made.Add( rides );

		foreach ( var item in catalogue.All.Where( kind ) )
		for ( var y = 20; y < 108; y += 3 )
		for ( var x = 20; x < 108; x += 3 )
		{
			if ( ParkBuilding.PlacementRefusal( state, item, x, y, 0 ) != null )
				continue;

			var built = ParkBuilding.Build( state, shipped, catalogue, null, rides, item.Id, x, y, 0 );

			if ( built.ThingId != 0 )
				return (state, rides, built.ThingId, (x, y));
		}

		Assert.Fail( "no cell took any item of the kind" );
		throw new InvalidOperationException();
	}

	/// <summary>A coaster bought stands its station's pylons, once, for the thing it made where it made it; its sale takes that thing's down.</summary>
	/// <remarks><b>Mutations:</b> the stand or the take-down call removed; the take-down given another thing's id.</remarks>
	[TestMethod]
	public void ACoasterBoughtPutsItsPylonsUpAndItsSaleTakesThemDown()
	{
		var (state, rides, thing, cell) = BuyFirst( item => item.Id == TempleOfGloom );

		CollectionAssert.AreEqual( new[] { $"up {thing} {TempleOfGloom} ({cell.X},{cell.Y})" }, asked );

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, thing ), "sold" );

		CollectionAssert.AreEqual( new[] { $"up {thing} {TempleOfGloom} ({cell.X},{cell.Y})", $"down {thing}" }, asked );
	}

	/// <summary>
	/// A moved coaster's pylons go down with the old thing and its station's come up on the new one at the cell it was put: the move is
	/// the sale and then a purchase (<c>docs/exe/park-engine.md</c>, "Moving a thing").
	/// </summary>
	/// <remarks><b>Mutations:</b> the take-down or the stand dropped; the stand made before the take-down; the stand at the old cell.</remarks>
	[TestMethod]
	public void AMovedCoasterTakesTheOldPylonsDownAndPutsItsStationsUpWhereItWasPut()
	{
		var (state, rides, thing, cell) = BuyFirst( item => item.Id == TempleOfGloom );
		var to = (X: -1, Y: -1);

		Assert.IsTrue( catalogue.TryGet( TempleOfGloom, out var gloom ) );

		// A free cell for it once its own footprint is cleared: tried on a copy of the state with it sold.
		var probe = new ParkState( shipped );

		for ( var y = 20; y < 108 && to.X < 0; y += 3 )
		for ( var x = 20; x < 108 && to.X < 0; x += 3 )
		{
			if ( (x, y) != cell && Math.Abs( x - cell.X ) + Math.Abs( y - cell.Y ) > 12 && ParkBuilding.PlacementRefusal( probe, gloom, x, y, 0 ) == null )
				to = (x, y);
		}

		asked.Clear();

		var reply = ParkBuilding.Move( state, shipped, catalogue, null, rides, thing, to.X, to.Y, 0 );
		var moved = state.Objects.Single( placed => placed.CatalogueId == TempleOfGloom );

		CollectionAssert.AreEqual( new[] { $"down {thing}", $"up {moved.ThingId} {TempleOfGloom} ({to.X},{to.Y})" }, asked, reply );
	}

	/// <summary>Neither a shop's purchase nor its sale asks for a pylon.</summary>
	/// <remarks><b>Mutations:</b> the coaster test dropped from either call.</remarks>
	[TestMethod]
	public void AShopsPurchaseAndSaleAskForNoPylon()
	{
		var (state, rides, thing, _) = BuyFirst( item => item.UiType == 1 /* a shop */ );

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, thing ), "sold" );

		Assert.AreEqual( 0, asked.Count, string.Join( "; ", asked ) );
	}
}
