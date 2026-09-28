using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a thing the player buys starts at: its price, and its speed, capacity and duration - the object
/// constructor <c>FUN_004db090</c>'s writes (the price at <c>0x004db3ad</c>, the three at <c>0x004db51c</c>..<c>0x004db64f</c>),
/// which <see cref="ParkBuilding.Constructed"/> and <see cref="ParkBuilding.StartingSettings"/> copy.
///
/// <para>
/// <b>The reading and rule tests need no game files</b>, because the description reader takes text and the
/// catalogue's item is a plain record. The rest read the shipped themes.
/// </para>
/// </summary>
[TestClass]
public class ParkStartingSettingsTests
{
	private const int DrinksShop = 1203;
	private const int JungleSpray = 1303;
	private const int BellyBounce = 1100;
	private const int Arcade = 1309;

	private static ParkItemCatalogue Jungle() => new( "jungle", GameData.Required() );

	private static ParkWorld.CatalogueObject Bought( ParkItemCatalogue.Item item )
		=> ParkBuilding.Constructed( item, 99, 40, 40, 0, 0, 0, default );

	/// <summary>
	/// <c>UsageInfo.InitPricePerUse</c> is read like every other key: the item's own over its category's, and
	/// nought where neither declares it. Every shipped shop and sideshow declares its own, so only this can
	/// show the category's coming through.
	/// </summary>
	[TestMethod]
	public void TheCategorysPriceShowsThroughWhereTheItemIsSilent()
	{
		var shops = new ItemDescriptionFile( "UsageInfo.InitPricePerUse\t\t10\t\tsale price\n" );

		Assert.AreEqual( 10, new ItemDescriptionFile( "Info.Id 1\n", shops ).InitPricePerUse, "the category's" );
		Assert.AreEqual( 30, new ItemDescriptionFile( "Info.Id 1\nUsageInfo.InitPricePerUse\t\t30\n", shops ).InitPricePerUse,
			"the item's own wins" );
		Assert.AreEqual( 0, new ItemDescriptionFile( "Info.Id 1\n" ).InitPricePerUse, "and with neither, nought" );
	}

	/// <summary>
	/// Lost Kingdom's eight shops and five sideshows each start at their own price, and nothing else has one:
	/// no ride or feature, nor their categories, declares the key.
	/// </summary>
	[TestMethod]
	public void EveryJungleShopAndSideshowStartsAtItsOwnPrice()
	{
		var catalogue = Jungle();

		var priced = catalogue.All.Where( item => item.InitPricePerUse != 0 )
			.ToDictionary( item => item.Id, item => item.InitPricePerUse );

		CollectionAssert.AreEquivalent( new Dictionary<int, int>
		{
			[1202] = 60, [1203] = 30, [1206] = 30, [1207] = 30, [1208] = 45, [1209] = 45, [1211] = 75, [1212] = 30,
			[1303] = 20, [1304] = 10, [1305] = 10, [1307] = 10, [1309] = 10,
		}, priced, "the costume, drinks, ice cream, burger, steak, balloon, gift and fries shops; the five sideshows" );

		Assert.IsTrue( catalogue.All.Where( item => item.UiType is 1 or 2 ).All( item => priced.ContainsKey( item.Id ) ),
			"every shop (UI type 1) and sideshow (2) has a price" );
	}

	/// <summary>
	/// A bought Drinks Shop, Jungle Spray and Belly Bounce, as the constructor makes them: each at its item's
	/// price, and at its purchase tier's settings - the shop's own capacity of 1 unheld, since the shops' bounds
	/// are both nought; the sideshow's the category's 3; the ride the category's speed 60 and its own 5 and 30.
	/// The Arcade declares its capacity twice, 6 and then 5, and the later is the one kept, as in the original.
	/// </summary>
	[TestMethod]
	public void ABoughtThingStartsAtItsItemsPriceAndSettings()
	{
		var catalogue = Jungle();

		(int, int, int, int) Start( int id )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );
			var bought = Bought( item );

			Assert.AreEqual( id, bought.CatalogueId );

			return (bought.PricePerUse, bought.OperatingSpeed, bought.OperatingCapacity, bought.OperatingDuration);
		}

		Assert.AreEqual( (30, 0, 1, 0), Start( DrinksShop ), "the Drinks Shop: price, speed, capacity, duration" );
		Assert.AreEqual( (20, 0, 3, 0), Start( JungleSpray ), "the Jungle Spray" );
		Assert.AreEqual( (0, 60, 5, 30), Start( BellyBounce ), "the Belly Bounce, free" );
		Assert.AreEqual( (10, 0, 5, 0), Start( Arcade ), "the Arcade: its second capacity, 5, over its first, 6" );
	}

	/// <summary>
	/// The record is built at the constructor's settings, not the item's as written: a made-up item whose
	/// starting values all fall outside what the constructor keeps is built at what it keeps, at its own price.
	/// </summary>
	[TestMethod]
	public void AThingIsBuiltAtTheConstructorsSettingsNotTheItemsAsWritten()
	{
		var item = new ParkItemCatalogue.Item( 1234, "made up", "", "", 1, 1, null, InitPricePerUse: 7,
			InitSpeed: -1, InitCapacity: 300, InitDuration: 3, MinDuration: 10, MaxDuration: 60 );

		var bought = Bought( item );

		Assert.AreEqual( 1234, bought.CatalogueId );
		Assert.AreEqual( 7, bought.PricePerUse, "its own price" );
		Assert.AreEqual( (0, 44, 10), (bought.OperatingSpeed, bought.OperatingCapacity, bought.OperatingDuration),
			"speed none, capacity the low byte of 300, duration held up to 10" );
	}

	/// <summary>
	/// The price is what a sideshow's excitement reads: a bought Jungle Spray is 30, as the saved one is; at a
	/// price of nought it would be 34 - enough to move a kind that likes 35 from a good ride to a perfect one.
	/// </summary>
	[TestMethod]
	public void ABoughtJungleSprayIsAsExcitingAsTheSavedOne()
	{
		Assert.IsTrue( Jungle().TryGet( JungleSpray, out var spray ) );

		Assert.AreEqual( 30, ParkRideScore.ExcitementOf( Bought( spray ), spray ), "20 + trunc( 0.08 × 25 × √( 50 − 20 ) )" );
	}

	/// <summary>
	/// The constructor's own rules, on made-up items. Each setting is written only above nought. The capacity
	/// setter takes the low byte and holds it to its bounds only when they sum above nought; the duration is
	/// held whatever its bounds are, so one with none starts at nought. Both test the least bound first, and a
	/// bound that is used gives its low byte.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> copying any starting value straight, holding capacity always, never or on either bound
	/// alone, holding duration only with bounds, testing the most bound first, writing at nought, or keeping the
	/// value's high bits each fails a line here.
	/// </remarks>
	[TestMethod]
	public void TheStartingSettingsAreTheConstructorsRules()
	{
		static (int Speed, int Capacity, int Duration) Of( int speed = 0, int capacity = 0, int minCapacity = 0,
			int maxCapacity = 0, int duration = 0, int minDuration = 0, int maxDuration = 0 )
			=> ParkBuilding.StartingSettings( new ParkItemCatalogue.Item( 1, "made up", "", "", 1, 1, null,
				InitSpeed: speed, InitCapacity: capacity, MinCapacity: minCapacity, MaxCapacity: maxCapacity,
				InitDuration: duration, MinDuration: minDuration, MaxDuration: maxDuration ) );

		Assert.AreEqual( 60, Of( speed: 60 ).Speed, "a speed above nought, as it is" );
		Assert.AreEqual( 0, Of( speed: -5 ).Speed, "and none below" );
		Assert.AreEqual( 300, Of( speed: 300 ).Speed, "a dword, unheld" );

		Assert.AreEqual( 10, Of( capacity: 10 ).Capacity, "no bounds: a shop's own, unheld" );
		Assert.AreEqual( 3, Of( capacity: 5, minCapacity: 1, maxCapacity: 3 ).Capacity, "held down to the most" );
		Assert.AreEqual( 4, Of( capacity: 2, minCapacity: 4, maxCapacity: 8 ).Capacity, "held up to the least" );
		Assert.AreEqual( 0, Of( capacity: -1, minCapacity: 1, maxCapacity: 3 ).Capacity, "none below nought, bounds or not" );
		Assert.AreEqual( 0, Of( capacity: 0, minCapacity: 1, maxCapacity: 3 ).Capacity, "none at nought, however bounded" );
		Assert.AreEqual( 3, Of( capacity: 5, minCapacity: 0, maxCapacity: 3 ).Capacity, "held when only the most is set" );
		Assert.AreEqual( 0, Of( capacity: 5, minCapacity: 2, maxCapacity: 0 ).Capacity, "held when only the least is set" );
		Assert.AreEqual( 5, Of( capacity: 5, minCapacity: 3, maxCapacity: -3 ).Capacity, "not held when the two sum to nought" );
		Assert.AreEqual( 4, Of( capacity: 3, minCapacity: 4, maxCapacity: 2 ).Capacity, "the least bound tested first" );
		Assert.AreEqual( 44, Of( capacity: 300 ).Capacity, "the low byte of 300" );
		Assert.AreEqual( 8, Of( capacity: 264, minCapacity: 1, maxCapacity: 10 ).Capacity, "held after the low byte is taken" );

		Assert.AreEqual( 10, Of( duration: 3, minDuration: 10, maxDuration: 60 ).Duration, "the Jelly Bounce's 3, held up" );
		Assert.AreEqual( 10, Of( duration: 30, minDuration: 1, maxDuration: 10 ).Duration, "held down" );
		Assert.AreEqual( 0, Of( duration: 5 ).Duration, "no bounds hold it to nought" );
		Assert.AreEqual( 0, Of( duration: 0, minDuration: 10, maxDuration: 60 ).Duration, "none at nought, however bounded" );
		Assert.AreEqual( 0, Of( duration: -1, minDuration: 1, maxDuration: 10 ).Duration, "nor below it" );
		Assert.AreEqual( 4, Of( duration: 3, minDuration: 4, maxDuration: 2 ).Duration, "the least bound tested first" );
		Assert.AreEqual( 4, Of( duration: 260, minDuration: 1, maxDuration: 10 ).Duration, "the low byte of 260" );
		Assert.AreEqual( 44, Of( duration: 5, minDuration: 300, maxDuration: 400 ).Duration, "the least bound's low byte" );
	}

	/// <summary>
	/// The rules change no jungle item's starting settings, so there they change nothing a player sees; across all
	/// four themes they change exactly eight - four rides' durations held up and four sideshows' capacities held
	/// down - which is what <see cref="ParkBuilding.StartingSettings"/> says of them.
	/// </summary>
	[TestMethod]
	public void OnlyEightShippedItemsStartAwayFromTheirOwnSettings()
	{
		var data = GameData.Required();

		var outside = new[] { "jungle", "fantasy", "hallow", "space" }
			.SelectMany( theme => new ParkItemCatalogue( theme, data ).All.Select( item => (theme, item) ) )
			.Where( pair => ParkBuilding.StartingSettings( pair.item )
				!= (pair.item.InitSpeed, pair.item.InitCapacity, pair.item.InitDuration) )
			.Select( pair => $"{pair.theme} {pair.item.Id}" )
			.ToArray();

		CollectionAssert.AreEquivalent( new[]
		{
			"fantasy 4104", "fantasy 4140", "hallow 2110", "space 3107",      // the durations held up
			"fantasy 4300", "hallow 2306", "space 3301", "space 3302",        // the capacities held down
		}, outside, string.Join( ", ", outside ) );
	}
}
