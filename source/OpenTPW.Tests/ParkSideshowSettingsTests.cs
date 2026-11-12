using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The sideshow window's first piece (<see cref="ParkSideshowSettings"/>; <c>docs/exe/hud.md</c>, "The shop, sideshow and ride
/// windows"), on the shipped park's Jungle Spray, thing 14. Every test but the ranges' needs the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkSideshowSettingsTests
{
	private const int JungleSpray = 14;
	private const int JungleSprayItem = 1303;
	private const int StrengthBirdItem = 1304;
	private const int DrinksShopItem = 1203;

	private static (ParkState State, ParkItemCatalogue Catalogue) ThePark()
	{
		var files = GameData.Required();
		FileSystem = files;
		Log ??= new();

		using var stream = new MemoryStream( files.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		return (new ParkState( world ), new ParkItemCatalogue( "jungle", files ));
	}

	private static ParkWorld.CatalogueObject Spray( ParkState state )
		=> state.Objects.Single( placed => placed.ThingId == JungleSpray );

	private static float? Nobody( int thing ) => null;

	[TestInitialize]
	[TestCleanup]
	public void Forget() => Unimplemented.Forget();

	/// <summary>
	/// <b>The load reads the chance's low byte, the prize and the price</b> (<c>0x004b1ee0</c>: <c>+0x190</c>, <c>+0x188</c>,
	/// <c>+0x194</c>): the shipped Jungle Spray loads chance 25, prize 50, price 20 (the item's 100 less its chance of losing,
	/// 75; its cost of goods; its price); a chance word of <c>0x119</c> loads 25.
	/// </summary>
	/// <remarks><b>Mutations:</b> the low byte dropped; the prize read from the price; the chance read as the chance of losing.</remarks>
	[TestMethod]
	public void TheLoadReadsTheChancesByteThePrizeAndThePrice()
	{
		var (state, _) = ThePark();
		var spray = Spray( state );

		Assert.AreEqual( new ParkSideshowSettings.Settings( 25, 50, 20 ), ParkSideshowSettings.Loaded( spray ) );
		Assert.AreEqual( 25, ParkSideshowSettings.Loaded( spray with { ChanceOfWinning = 0x119 } ).Chance );
	}

	/// <summary>
	/// <b>The apply writes the price, the chance's low byte and the prize</b> (<c>0x004b20b0</c>: <c>0x004e2770</c>,
	/// <c>0x004e21c0</c>, <c>0x004e1a20</c>) and nothing else: a chance of <c>0x164</c> is written 100.
	/// </summary>
	/// <remarks><b>Mutations:</b> the chance's cut gone; the prize written to the price; the price not written.</remarks>
	[TestMethod]
	public void TheApplyWritesThePriceTheChancesByteAndThePrize()
	{
		var (state, _) = ThePark();
		var spray = Spray( state );

		var applied = ParkSideshowSettings.Applied( spray, new( 0x164, 3000, 45 ) );

		Assert.AreEqual( (100, 3000, 45), (applied.ChanceOfWinning, applied.CostOfGoods, applied.PricePerUse) );
		Assert.AreEqual( spray with { ChanceOfWinning = 100, CostOfGoods = 3000, PricePerUse = 45 }, applied, "nothing else moves" );

		Assert.IsTrue( ParkSideshowSettings.ApplyTo( state, JungleSpray, new( 60, 75, 30 ) ) );
		Assert.AreEqual( new ParkSideshowSettings.Settings( 60, 75, 30 ), ParkSideshowSettings.Loaded( Spray( state ) ), "the park holds it" );
		Assert.IsFalse( ParkSideshowSettings.ApplyTo( state, JungleSpray, new( 60, 75, 30 ) ), "the same again writes nothing" );
	}

	/// <summary>
	/// <b>The controls hold their ranges</b>: the chance's slider 0 to 100 (<c>0xa08e</c>), the prize's box 0 to 10000
	/// (<c>0xa090</c>) and the price's spinner 0 to 500 (<c>0xa08f</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the prize held to the price's 500; the chance held to 255.</remarks>
	[TestMethod]
	public void TheControlsHoldTheirRanges()
	{
		Assert.AreEqual( new ParkSideshowSettings.Settings( 100, 10000, 500 ), new ParkSideshowSettings.Settings( 150, 20000, 900 ).Held() );
		Assert.AreEqual( new ParkSideshowSettings.Settings( 0, 0, 0 ), new ParkSideshowSettings.Settings( -5, -1, -30 ).Held() );
		Assert.AreEqual( new ParkSideshowSettings.Settings( 99, 9000, 499 ), new ParkSideshowSettings.Settings( 99, 9000, 499 ).Held() );
	}

	/// <summary>
	/// <b>Apply to all reaches the placed sideshows of the shown one's item</b> (<c>0xa0a9</c>, mask <c>0x100</c>, then
	/// <c>0x004b20f0</c>'s item test): a second Jungle Spray, not a Strength Bird, not a Drinks Shop, not one in the hand; and
	/// <b>the arrows walk every sideshow</b>, newest first, so they show with two.
	/// </summary>
	/// <remarks><b>Mutations:</b> the item test gone (the Strength Bird reached); the UI type the shops'.</remarks>
	[TestMethod]
	public void ApplyToAllReachesTheSameItemsSideshowsAndTheArrowsEverySideshow()
	{
		var (state, catalogue) = ThePark();
		var unplaced = ParkWorld.CatalogueObject.Unplaced;

		Assert.AreEqual( 1, ParkSideshowSettings.Siblings( state, catalogue ).Count, "the shipped park's one sideshow: no arrows" );

		state.AddObject( new ParkWorld.CatalogueObject( 60, JungleSprayItem, 32 << 8, 30 << 8, 0 ) );
		state.AddObject( new ParkWorld.CatalogueObject( 61, StrengthBirdItem, 30 << 8, 30 << 8, 0 ) );
		state.AddObject( new ParkWorld.CatalogueObject( 62, DrinksShopItem, 34 << 8, 30 << 8, 0 ) );
		state.AddObject( new ParkWorld.CatalogueObject( 63, JungleSprayItem, unplaced, unplaced, 0 ) );

		CollectionAssert.AreEquivalent( new[] { JungleSpray, 60 }, ParkSideshowSettings.OfItem( state.Objects, catalogue, JungleSprayItem ) );
		CollectionAssert.AreEqual( new[] { 61, 60, JungleSpray }, ParkSideshowSettings.Siblings( state, catalogue ) );
	}

	/// <summary>
	/// <b>The figures are the finished days', the excitement the buffered settings'</b> (<c>FUN_004b16c0</c>): a day of four
	/// customers, three served, a walk-away, 80 taken and 50 spent reads "4 of 5", takings 80, profit 30, winners 75%; the
	/// excitement at the loaded chance 25 is 30 and at a buffered chance of 100 is 63, the placed sideshow still 25. With no
	/// finished day the winners are 0 (<c>0x004e1fca</c>), the satisfaction 50 and nothing is counted. A day's satisfaction of 300
	/// (over the thirty days of the saved ring) reads 60, not the bar's units; a guest of happiness 60 on the next cell reads 60 as the local happiness.
	/// </summary>
	/// <remarks><b>Mutations:</b> winners over the asked; the excitement read from the placed sideshow; the walk-aways left out of M;
	/// winners null or counted over no customer; the satisfaction in the bar's units or without its 50; the local happiness read at
	/// another cell or not at all.</remarks>
	[TestMethod]
	public void TheFiguresAreTheFinishedDaysAndTheExcitementTheBufferedSettings()
	{
		var (state, catalogue) = ThePark();
		Assert.IsTrue( catalogue.TryGet( JungleSprayItem, out var item ) );
		var spray = Spray( state );

		var fresh = ParkSideshowSettings.FiguresOf( state, spray, item, ParkSideshowSettings.Loaded( spray ), Nobody );
		Assert.AreEqual( (0, 0, 0, 30, 50), (fresh.Customers, fresh.Asked, fresh.WinnersPercent, fresh.Excitement, fresh.Satisfaction) );
		Assert.AreEqual( 0, Unimplemented.Summary.Count( gap => gap.What == "SIDESHOW_WINNERS_NO_CUSTOMERS" ), "nothing is counted" );

		var rings = state.RingsFor( JungleSpray );

		for ( var i = 0; i < 4; ++i )
			rings.CountCustomer();

		rings.CountWalkAway();
		rings.Served.Today = 3;
		rings.Takings.Today = 80;
		rings.Costs.Today = 50;
		rings.Satisfaction.Today = 300;
		rings.Roll();

		// A guest of happiness 60 on the cell east of the sideshow, and nobody else.
		const int guest = 9999;
		state.EnterCell( spray.CellX + 1, spray.CellY, guest );
		float? Guest( int thing ) => thing == guest ? 60f : null;

		var month = ParkSideshowSettings.FiguresOf( state, spray, item, new( 100, 50, 20 ), Guest );

		Assert.AreEqual( (4, 5, 80, 30, 75, 63), (month.Customers, month.Asked, month.Takings, month.Profit, month.WinnersPercent, month.Excitement) );
		Assert.AreEqual( 60, month.Satisfaction, "the finished day's 300 over the saved ring's thirty days, on the 50 (0x004e1ef8), in no bar's units" );
		Assert.AreEqual( 60, month.LocalHappiness, "the guest on the neighbouring cell" );
		Assert.AreEqual( ParkShopWindow.LocalHappiness( state, spray.CellX, spray.CellY, Guest ), month.LocalHappiness );
		Assert.AreEqual( 25, Spray( state ).ChanceOfWinning, "the buffered chance is not the sideshow's" );
	}
}
