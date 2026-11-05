using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The two fixed objects of a fresh Full Simulation park, the gates and the traffic lights (Q1029, from Q211): the object
/// constructor seeds the shop's cost of goods and the sideshow's chance of winning from the item's own description
/// (<c>FUN_004db090</c>; <c>docs/exe/ride-operation.md</c>, "The sideshow win roll"), whatever the item is.
/// </summary>
[TestClass]
public class FreshParkFixedObjectsTests
{
	[TestCleanup]
	public void PutTheRosterBack() => SetCurrentPlayer( null );

	[TestMethod]
	public void TheGatesAndLightsCarryTheirItemsGoodsAndChance()
	{
		FileSystem = GameData.Required();
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );
		var balance = new ParkBalance( "jungle", easyMode: Level.InstantAction );
		var catalogue = new ParkItemCatalogue( "jungle", instantAction: Level.InstantAction );
		var park = Level.CreatePark( "jungle", balance, catalogue )!;

		Assert.AreEqual( 2, park.Objects.Count );

		foreach ( var (thing, stem) in new[] { (park.Objects.Single( o => o.ThingId == park.ParkGates ), "gates" ),
			(park.Objects.Single( o => o.ThingId == park.TrafficLights ), "lights") } )
		{
			var item = catalogue.All.First( i => i.Stem.Equals( stem, System.StringComparison.OrdinalIgnoreCase ) );

			Assert.AreEqual( (item.CostOfGoods, item.ChanceOfWinning), (thing.CostOfGoods, thing.ChanceOfWinning), stem );
		}
	}

	/// <summary>
	/// The shipped gates and lights have the record's own 0 and 100, so the values are told apart with an item that has others:
	/// a shop's cost of goods and a sideshow's chance, which the constructor seeds whatever the item is.
	/// </summary>
	[TestMethod]
	public void AFixedObjectIsSeededWithItsItemsGoodsAndChance()
	{
		FileSystem = GameData.Required();
		var item = new ParkItemCatalogue( "jungle", instantAction: false ).All.First( i => i.Stem.Equals( "gates", System.StringComparison.OrdinalIgnoreCase ) )
			with { CostOfGoods = 37, ChanceOfWinning = 61 };

		var thing = FreshPark.FixedObject( item, 11, 0 );

		Assert.AreEqual( (37, 61), (thing.CostOfGoods, thing.ChanceOfWinning) );
		Assert.AreEqual( (item.InitCapacity, item.InitDuration, item.InitSpeed, item.InitPricePerUse),
			(thing.OperatingCapacity, thing.OperatingDuration, thing.OperatingSpeed, thing.PricePerUse) );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
