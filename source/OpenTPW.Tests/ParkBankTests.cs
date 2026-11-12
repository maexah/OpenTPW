using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park's bank and a sale's cost of goods - <c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's
/// money": the deposit (<c>FUN_004d0190</c>), the withdrawal (<c>FUN_004d01f0</c>) and the gate fee
/// (<c>FUN_004d0600</c>) on <see cref="ParkState"/>, the month's and the year's change, the purchase and the sale, the booking (<c>FUN_004e1920</c>) and a shop's amount
/// (<c>FUN_004e1b40</c>).
/// <para>
/// The bank's arithmetic runs without the game. The rest reads Lost Kingdom's save and catalogue, and is skipped where
/// there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkBankTests
{
	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private const int BellyBounce = 13;
	private const int JungleSpray = 14;
	private const int DrinksShop = 16;
	private const int SmallToilet = 21;

	private ParkWorld Park() => new( Payload() );

	private byte[] Payload()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new SaveReader( stream ).ReadFile();
	}

	private RideScript Script()
	{
		using var stream = Data().OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private ParkItemCatalogue Catalogue() => new( "jungle", Data() );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	private static Peep Guest() => new( 7, new ParkWorld.GuestState(
		State: (int)PeepState.Riding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
		ExitLevel: 100, Happiness: 50f, Thirst: 80f, Hunger: 80f, Toilet: 80f, Vomit: 0f,
		Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	/// <summary>The park's own mood constants - <c>PeepInfo.MediumHappinessChange</c> is 15 in this stack.</summary>
	private static ParkAdmission Mood() => new( new ParkBalance( "jungle", easyMode: true ), 25 );

	/// <summary>
	/// Lets one guest off <paramref name="thing"/> through <see cref="ParkRideOperation.Dismiss"/>, won
	/// (<paramref name="won"/>, the settle-up's <c>+0x1f1</c>) or lost, into <paramref name="park"/>.
	/// </summary>
	private Peep LetOff( ParkState park, ParkWorld.CatalogueObject thing, bool won )
	{
		var peep = Guest();
		var script = Script();

		peep.SetState( PeepState.Riding, tick: 1, new Random( 1 ) );
		peep.QueuePos = won ? 1 : 0;

		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [peep.ThingId] = peep }, Mood() )
			.Dismiss( script, thing, tick: 9, new Random( 1 ), catalogue: Catalogue() ),
			"the guest should have been let off" );

		return peep;
	}

	private ParkWorld.CatalogueObject Thing( int thingId ) => Park().Objects.Single( o => o.ThingId == thingId );

	private static ParkState Bank( int balance = 0 ) => new( parkIsClosed: false, visitorsToDate: 0, balance: balance );

	[TestMethod]
	public void Q97SavedGoodsAndChanceKeepTheirDwordsAndNeighbours()
	{
		var payload = Payload();
		var shop = new ParkWorld( payload ).Objects.Single( o => o.ThingId == DrinksShop );
		var at = RecordAt( payload, p => U16( payload, p + 20 ) == shop.CatalogueId
			&& U16( payload, p + 206 ) == shop.EntryPos && I32( payload, p + 1054 ) == shop.PricePerUse );
		Put( payload, at + 1042, 0x12345678 );
		Put( payload, at + 1050, 0x2345013a );
		var read = new ParkWorld( payload ).Objects.Single( o => o.ThingId == DrinksShop );
		Assert.AreEqual( (0x12345678, 0x2345013a), (read.CostOfGoods, read.ChanceOfWinning) );
		Assert.AreEqual( shop with { Rings = null }, read with { Rings = null, CostOfGoods = shop.CostOfGoods, ChanceOfWinning = shop.ChanceOfWinning } );
	}

	[TestMethod]
	public void Q97ShippedObjectSettingsAreLoaded()
	{
		var objects = Park().Objects;
		Assert.IsTrue( objects.All( o => o.CostOfGoods == (o.ThingId == JungleSpray ? 50 : o.ThingId == DrinksShop ? 20 : 0) ) );
		Assert.IsTrue( objects.All( o => o.ChanceOfWinning == (o.ThingId == JungleSpray ? 25 : 100) ) );
	}

	[TestMethod]
	public void Q97NewObjectsCopyEachItemsStartingSettings()
	{
		foreach ( var item in Catalogue().All )
		{
			var made = ParkBuilding.Constructed( item, 9000, 40, 40, 0, 0, 0, default );
			Assert.AreEqual( (item.CostOfGoods, item.ChanceOfWinning), (made.CostOfGoods, made.ChanceOfWinning), item.Stem );
		}
	}

	[TestMethod]
	public void Q97ShopBooksTheObjectsCostInsteadOfTheCatalogue()
	{
		var world = Park();
		var bank = new ParkState( world );
		var shop = Thing( DrinksShop ) with { CostOfGoods = 80 };
		Assert.IsTrue( bank.ReplaceObject( shop ) );
		var before = bank.Balance;
		var peep = LetOff( bank, shop, won: true );
		Assert.AreEqual( 300 - 30, peep.Cash );
		Assert.AreEqual( before + 30 - 80, bank.Balance );
		Assert.AreEqual( 80, bank.CostsFor( DrinksShop ) );
	}

	[TestMethod]
	public void Q97SideshowsBookPayAndValueTheObjectsPrize()
	{
		var bank = new ParkState( Park() );
		var ride = Thing( JungleSpray ) with { CostOfGoods = 80, ChanceOfWinning = 58 };
		Assert.IsTrue( bank.ReplaceObject( ride ) );
		var before = bank.Balance;
		var peep = LetOff( bank, ride, won: true );
		Assert.AreEqual( 300 - 20 + 80, peep.Cash );
		Assert.AreEqual( before + 20 - 80, bank.Balance );
		Assert.AreEqual( 80, bank.CostsFor( JungleSpray ) );
		Assert.AreEqual( 80f, peep.Happiness, "50 + log2(80/20) * 15, with no excitement matcher" );
	}

	[TestMethod]
	public void Q97ExcitementReadsBothObjectSettings()
	{
		var ride = Thing( JungleSpray ) with { CostOfGoods = 80, ChanceOfWinning = 256 + 58 };
		Assert.IsTrue( Catalogue().TryGet( ride.CatalogueId, out var item ) );
		Assert.AreEqual( 55, ParkRideScore.ExcitementOf( ride, item ), "20 + trunc(58 * sqrt(60) * 0.08)" );
	}

	private sealed class FixedRoll( int value ) : Random
	{
		public int Draws { get; private set; }
		public override int Next() { ++Draws; return value; }
	}

	[TestMethod]
	[DataRow( 58, 58, true )]
	[DataRow( 58, 59, false )]
	[DataRow( 314, 59, false )]
	[DataRow( 0, 0, true )]
	[DataRow( 100, 99, true )]
	public void Q97WinRollReadsTheObjectsLowByteAndDrawsOnce( int chance, int draw, bool expected )
	{
		var random = new FixedRoll( draw );
		var thing = new ParkWorld.CatalogueObject( 9, 1303, 0, 0, 0, ChanceOfWinning: chance );
		Assert.AreEqual( expected, ParkRideOperation.Succeeds( thing, random ) );
		Assert.AreEqual( 1, random.Draws );
	}

	// ---- the bank, without the game ----

	/// <summary>
	/// <b>A withdrawal takes the cost off the balance and the year's profit, and leaves the balance it made as the
	/// last</b> (<c>FUN_004d01f0</c>, <c>0x004d0205</c>..<c>0x004d0251</c>); a park in the black stamps no red.
	/// </summary>
	[TestMethod]
	public void AWithdrawalMovesTheBalanceTheProfitAndTheLastBalance()
	{
		var bank = Bank( balance: 100 );

		bank.Spend( 30 );

		Assert.AreEqual( (70, -30, 70, 0), (bank.Balance, bank.ProfitThisYear, bank.LastBalance, bank.TurnEnteredRed) );
	}

	/// <summary>
	/// <b>While withdrawals are off, a withdrawal does nothing at all</b> (<c>0x004d01f3</c>): not the balance, not
	/// the last balance, not the profit, and no red stamp however far below nought it would have gone.
	/// </summary>
	[TestMethod]
	public void AWithdrawalWhileWithdrawalsAreOffDoesNothing()
	{
		var bank = Bank( balance: 10 );
		bank.AdvanceGameTick();
		bank.WithdrawalsEnabled = 0;

		bank.Spend( 30 );

		Assert.AreEqual( (10, 0, 0, 0), (bank.Balance, bank.ProfitThisYear, bank.LastBalance, bank.TurnEnteredRed) );
	}

	/// <summary>
	/// <b>A park with nothing loaded starts with withdrawals on</b>, as the bank's constructor does
	/// (<c>0x004cf81a</c>): a test's park pays for things.
	/// </summary>
	[TestMethod]
	public void AParkWithNothingLoadedHasWithdrawalsOn()
	{
		Assert.AreEqual( 1, Bank().WithdrawalsEnabled );
		Assert.AreEqual( 1, new ParkState( null ).WithdrawalsEnabled );
	}

	/// <summary>
	/// <b>The red is stamped once, on the withdrawal that crosses from a last balance of nought or more</b>
	/// (<c>0x004d020a</c>..<c>0x004d0222</c>, a signed test of the OLD last balance). A second withdrawal in the red
	/// keeps the first stamp; deposits alone that climb back out write no last balance, so the next dip keeps it
	/// too; a withdrawal that leaves the balance at nought or more re-arms it.
	/// </summary>
	[TestMethod]
	public void TheRedIsStampedOnlyFromALastBalanceOfNoughtOrMore()
	{
		var bank = Bank( balance: 10 );

		for ( var tick = 0; tick < 5; ++tick )
			bank.AdvanceGameTick();

		bank.Spend( 20 );
		Assert.AreEqual( (-10, 5), (bank.Balance, bank.TurnEnteredRed), "into the red on tick 5" );

		bank.AdvanceGameTick();
		bank.Spend( 5 );
		Assert.AreEqual( 5, bank.TurnEnteredRed, "still the first stamp" );

		bank.Deposit( 100 );
		bank.AdvanceGameTick();
		bank.Spend( 200 );
		Assert.AreEqual( (-115, 5), (bank.Balance, bank.TurnEnteredRed),
			"climbing out on a deposit left the last balance in the red, so the dip is not stamped afresh" );

		bank.Deposit( 300 );
		bank.Spend( 10 );
		Assert.AreEqual( 175, bank.LastBalance, "a withdrawal left in the black writes a last balance of nought or more" );

		bank.AdvanceGameTick();
		bank.Spend( 500 );
		Assert.AreEqual( 8, bank.TurnEnteredRed, "and the next dip is stamped with its own tick" );
	}

	/// <summary>
	/// <b>A withdrawal of nought still writes the last balance</b>, and on a balance already below nought from a last
	/// balance of nought or more, as a fresh bank's is, still stamps the red: it has no test of the amount.
	/// </summary>
	[TestMethod]
	public void AWithdrawalOfNoughtStillWritesTheLastBalance()
	{
		var bank = Bank( balance: 40 );

		bank.Spend( 0 );

		Assert.AreEqual( 40, bank.LastBalance );

		var red = Bank( balance: -5 );
		red.AdvanceGameTick();
		red.Spend( 0 );

		Assert.AreEqual( (-5, 1), (red.LastBalance, red.TurnEnteredRed), "below nought from a last balance of nought" );
	}

	/// <summary>
	/// <b>A deposit moves the balance and the year's profit and nothing else</b> (<c>FUN_004d0190</c>): no last
	/// balance, no gate on withdrawals, and no gate's running total.
	/// </summary>
	[TestMethod]
	public void ADepositMovesTheBalanceAndTheProfitAlone()
	{
		var bank = Bank( balance: 100 );
		bank.WithdrawalsEnabled = 0;

		bank.Deposit( 30 );

		Assert.AreEqual( (130, 30, 0, 0), (bank.Balance, bank.ProfitThisYear, bank.LastBalance, bank.Takings) );
	}

	/// <summary>
	/// <b>An admission fee moves the balance, the year's profit and the gate's running total</b>
	/// (<c>FUN_004d0600</c>), and no last balance.
	/// </summary>
	[TestMethod]
	public void AnAdmissionFeeMovesTheBalanceTheProfitAndTheTakings()
	{
		var bank = Bank( balance: 100 );

		bank.Take( 25 );

		Assert.AreEqual( (125, 25, 25, 0), (bank.Balance, bank.ProfitThisYear, bank.Takings, bank.LastBalance) );
	}

	/// <summary>
	/// <b>A booking goes on the object's today's costs and its total, then comes out of the bank</b>
	/// (<c>FUN_004e1920</c>); with withdrawals off the object's two are still booked, the gate being inside the
	/// withdrawal.
	/// </summary>
	[TestMethod]
	public void ABookingIsTheObjectsAndThenTheBanks()
	{
		var bank = Bank( balance: 100 );

		bank.BookCostOfGoods( 16, 20 );
		bank.BookCostOfGoods( 16, 20 );

		Assert.AreEqual( (40, 40), (bank.RingsFor( 16 ).Costs.Today, bank.CostsFor( 16 )), "the object's two" );
		Assert.AreEqual( (60, -40, 60), (bank.Balance, bank.ProfitThisYear, bank.LastBalance), "and the bank's" );

		bank.WithdrawalsEnabled = 0;
		bank.BookCostOfGoods( 16, 20 );

		Assert.AreEqual( (60, 60), (bank.RingsFor( 16 ).Costs.Today, bank.CostsFor( 16 )), "booked with withdrawals off" );
		Assert.AreEqual( 60, bank.Balance, "and nothing withdrawn" );
	}

	/// <summary>
	/// <b>A shop's cost of goods</b> (<c>FUN_004e1b40</c>): each term the low byte less 50 times 0.005 held to ±0.5,
	/// the ingredient's taken off for fat and ice (1 and 3) and added otherwise, then truncated. An item made up for
	/// the arithmetic alone.
	/// </summary>
	[TestMethod]
	public void AShopsCostOfGoodsIsTheCostScaledByQualityAndIngredient()
	{
		static int Cost( int ingredient, int quality, int amount, int cost = 40 )
			=> ParkRideOperation.ShopCostOfGoods(
				new ParkItemCatalogue.Item( 1, "shop", "", "", 1, 1, null, CostOfGoods: cost, SpecialIngredient: ingredient ),
				new ParkWorld.CatalogueObject( 9, 1, 0, 0, 0, QualityOfGoods: quality, AmountOfSpecialIngredient: amount, CostOfGoods: cost ) );

		Assert.AreEqual( 40, Cost( 0, 50, 50 ), "at 50 and 50 the cost itself" );
		Assert.AreEqual( 50, Cost( 0, 100, 50 ), "quality 100 is a quarter more" );
		Assert.AreEqual( 30, Cost( 0, 0, 50 ), "quality nought a quarter less" );

		foreach ( var ingredient in new[] { 1, 3 } )
			Assert.AreEqual( 30, Cost( ingredient, 50, 100 ), $"ingredient {ingredient} takes the amount's term off" );

		foreach ( var ingredient in new[] { 0, 2, 4, 5 } )
			Assert.AreEqual( 50, Cost( ingredient, 50, 100 ), $"ingredient {ingredient} adds it" );

		Assert.AreEqual( 60, Cost( 0, 250, 50 ), "a term is held at a half" );
		Assert.AreEqual( 60, Cost( 0, 50, 250 ), "the ingredient's term is held at a half" );
		Assert.AreEqual( 20, Cost( 1, 50, 250 ), "and at a half taken off" );
		Assert.AreEqual( 40, Cost( 0, 256 + 50, 256 + 50 ), "each is read as its low byte" );
		Assert.AreEqual( 37, Cost( 0, 100, 50, cost: 30 ), "37.5 truncated" );
		Assert.AreEqual( 18, Cost( 3, 0, 10, cost: 20 ), "the sum in double: 18.99999976, where 24 bits make 19" );
		Assert.AreEqual( -10, Cost( 0, 50, 50, cost: -10 ), "the cost read unsigned, and __ftol's low dword" );
		Assert.AreEqual( 1073741811, Cost( 0, 100, 50, cost: -10 ), "1.25 x 4294967286, truncated, its low dword" );
	}

	// ---- the save and the running park ----

	/// <summary>
	/// <b>A shop books what the original booked</b>: the per-sale costs in Alexah's Lost Kingdom Full Simulation saves,
	/// written by the original, each item's cost, quality and amount of special ingredient as the save holds them
	/// (<c>~/.cache/tpw-harnesses/q177c/objmoney.py</c>), and the shipped Drinks Shop's.
	/// </summary>
	[TestMethod]
	public void AShopBooksWhatTheOriginalBooked()
	{
		var catalogue = Catalogue();

		foreach ( var (id, cost, quality, amount, booked) in new[]
		{
			(1203, 20, 0, 100, 10), (1206, 20, 0, 0, 10), (1209, 30, 100, 50, 37),
			(1211, 50, 0, 50, 37), (1208, 30, 0, 50, 22), (1203, 20, 50, 50, 20),
		} )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id} is Lost Kingdom's" );
			Assert.AreEqual( cost, item.CostOfGoods, $"item {id}'s cost of goods is the saved object's" );
			Assert.AreEqual( booked, ParkRideOperation.ShopCostOfGoods( item,
				new ParkWorld.CatalogueObject( 9, id, 0, 0, 0, QualityOfGoods: quality, AmountOfSpecialIngredient: amount, CostOfGoods: cost ) ),
				$"item {id} at quality {quality} and amount {amount}" );
		}
	}

	/// <summary>
	/// <b>A drink nets the park ten</b> - thirty in, twenty out, the original's measured +10: the balance and the
	/// year's profit up ten, the last balance the new balance, the Drinks Shop's takings thirty and costs twenty.
	/// </summary>
	[TestMethod]
	public void ADrinkNetsTheParkTen()
	{
		var bank = Bank( balance: 1000 );

		LetOff( bank, Thing( DrinksShop ), won: true );

		Assert.AreEqual( (1010, 10, 1010), (bank.Balance, bank.ProfitThisYear, bank.LastBalance) );
		Assert.AreEqual( (30, 20), (bank.TakingsFor( DrinksShop ), bank.CostsFor( DrinksShop )) );
		Assert.AreEqual( 20, bank.RingsFor( DrinksShop ).Costs.Today, "today's costs" );
	}

	/// <summary>
	/// <b>A Jungle Spray play won costs the park thirty, and one lost earns it twenty</b>: the price in, and for a
	/// win the cost of goods out, which is the prize the winner takes; a loss books nothing.
	/// </summary>
	[TestMethod]
	public void ASprayWonCostsThirtyAndOneLostEarnsTwenty()
	{
		var won = Bank( balance: 1000 );
		var winner = LetOff( won, Thing( JungleSpray ), won: true );

		Assert.AreEqual( (970, 50, 330), (won.Balance, won.CostsFor( JungleSpray ), winner.Cash) );

		var lost = Bank( balance: 1000 );
		var loser = LetOff( lost, Thing( JungleSpray ), won: false );

		Assert.AreEqual( (1020, 0, 280), (lost.Balance, lost.CostsFor( JungleSpray ), loser.Cash) );
		Assert.AreEqual( 0, lost.RingsFor( JungleSpray ).Costs.Today, "a loss books no cost" );
	}

	/// <summary>
	/// <b>A shop books its cost of goods scaled by its quality and special ingredient, not the bare cost</b>: the
	/// Drinks Shop at quality nought books 15 of its 20 (<c>FUN_004e1b40</c>), so the park keeps 15 of the 30.
	/// </summary>
	[TestMethod]
	public void AShopAtQualityNoughtBooksAQuarterLess()
	{
		var bank = Bank( balance: 1000 );

		LetOff( bank, Thing( DrinksShop ) with { QualityOfGoods = 0 }, won: true );

		Assert.AreEqual( (1015, 15), (bank.Balance, bank.CostsFor( DrinksShop )) );
	}

	/// <summary>
	/// <b>A ride or a feature books no cost of goods</b>: only a shop and a sideshow book, so letting a winner off the
	/// Belly Bounce or a Small Toilet writes no last balance and books nothing.
	/// </summary>
	[TestMethod]
	public void ARideOrAFeatureBooksNothing()
	{
		foreach ( var id in new[] { BellyBounce, SmallToilet } )
		{
			var bank = Bank( balance: 1000 );
			var thing = Thing( id );

			LetOff( bank, thing, won: true );

			Assert.AreEqual( (1000 + thing.PricePerUse, 0, 0, 0),
				(bank.Balance, bank.LastBalance, bank.CostsFor( id ), bank.RingsFor( id ).Costs.Today), $"thing {id}" );
		}
	}

	/// <summary>
	/// <b>The year's change zeroes the year's profit and nothing else</b> (message <c>0xd</c>, <c>0x004d034e</c>).
	/// </summary>
	[TestMethod]
	public void TheYearsChangeZeroesTheProfitAlone()
	{
		var bank = Bank( balance: 100 );
		bank.Spend( 30 );
		bank.Deposit( 5 );

		bank.TurnTheYear();

		Assert.AreEqual( (75, 0, 70), (bank.Balance, bank.ProfitThisYear, bank.LastBalance) );
	}

	/// <summary>
	/// <b>A drink with withdrawals off still books its cost against the shop</b>, and the park keeps all thirty.
	/// </summary>
	[TestMethod]
	public void ADrinkWithWithdrawalsOffKeepsThirty()
	{
		var bank = Bank( balance: 1000 );
		bank.WithdrawalsEnabled = 0;

		LetOff( bank, Thing( DrinksShop ), won: true );

		Assert.AreEqual( (1030, 30, 20), (bank.Balance, bank.ProfitThisYear, bank.CostsFor( DrinksShop )) );
	}

	/// <summary>
	/// <b>The park that ships holds its bank as the save left it</b>: withdrawals on, the last balance 87787, never in
	/// the red, and −12013 of profit this year (FileFormats <c>saves.md</c>, the bank's 28 to 40).
	/// </summary>
	[TestMethod]
	public void TheBankIsSeededFromTheSave()
	{
		var bank = new ParkState( Park() );

		Assert.AreEqual( (87987, 1, 87787, 0, -12013),
			(bank.Balance, bank.WithdrawalsEnabled, bank.LastBalance, bank.TurnEnteredRed, bank.ProfitThisYear) );
	}

	/// <summary>
	/// <b>The bank's flag and red stamp are read from the save</b>, not assumed (FileFormats <c>saves.md</c>, the bank's
	/// 28 and 36). The shipped park holds 1 and nought, so a copy of its payload is given nought and 42 at the bank's
	/// record, found by the fee, the balance and the last balance; with withdrawals off a withdrawal then does nothing.
	/// </summary>
	[TestMethod]
	public void TheBanksFlagAndRedAreReadFromTheSave()
	{
		var payload = Payload();
		var at = RecordAt( payload, p => I32( payload, p ) == 25 && I32( payload, p + 4 ) == 87987
			&& I32( payload, p + 16 ) == 87787 );

		Put( payload, at + 12, 0 );
		Put( payload, at + 20, 42 );

		var bank = new ParkState( new ParkWorld( payload ) );

		Assert.AreEqual( (0, 42), (bank.WithdrawalsEnabled, bank.TurnEnteredRed) );

		bank.Spend( 100 );

		Assert.AreEqual( 87987, bank.Balance, "a withdrawal with withdrawals off" );
	}

	/// <summary>
	/// <b>Every object in the park that ships holds quality 50, special ingredient 50 and no costs</b>, the Drinks Shop
	/// among them.
	/// </summary>
	[TestMethod]
	public void TheShippedObjectsHoldFiftyFiftyAndNoCosts()
	{
		var objects = Park().Objects;

		Assert.AreEqual( 14, objects.Count );
		Assert.IsTrue( objects.All( o => o.QualityOfGoods == 50 && o.AmountOfSpecialIngredient == 50 && o.TotalCosts == 0 ) );
	}

	/// <summary>
	/// <b>Quality, special ingredient and total costs are read from 1046, 1058 and 1086</b> (FileFormats
	/// <c>saves.md</c>). The shipped park holds 50, 50 and nought, so a copy of its payload is given 70, 90 and 555 at
	/// the Drinks Shop's record, found by fields read elsewhere; the running park carries the total on.
	/// </summary>
	[TestMethod]
	public void QualityIngredientAndTotalCostsAreReadFromTheirPlace()
	{
		var payload = Payload();
		var shop = new ParkWorld( payload ).Objects.Single( o => o.ThingId == DrinksShop );
		var at = RecordAt( payload, p => U16( payload, p + 20 ) == shop.CatalogueId
			&& U16( payload, p + 206 ) == shop.EntryPos && I32( payload, p + 1054 ) == shop.PricePerUse );

		Put( payload, at + 1046, 70 );
		Put( payload, at + 1058, 90 );
		Put( payload, at + 1086, 555 );

		var park = new ParkWorld( payload );
		var read = park.Objects.Single( o => o.ThingId == DrinksShop );

		Assert.AreEqual( (70, 90, 555), (read.QualityOfGoods, read.AmountOfSpecialIngredient, read.TotalCosts) );
		Assert.AreEqual( (shop.PricePerUse, shop.QueueSizeInCells, shop.TotalTakings),
			(read.PricePerUse, read.QueueSizeInCells, read.TotalTakings), "and the fields between them are untouched" );
		Assert.AreEqual( 555, new ParkState( park ).CostsFor( DrinksShop ), "the running park counts on from the save's" );
	}

	/// <summary>
	/// <b>A shop the player buys starts at quality 50 and special ingredient 50</b> (<c>0x004db389</c>,
	/// <c>0x004db3b3</c>), so it books its item's own cost of goods.
	/// </summary>
	[TestMethod]
	public void ABoughtShopBooksItsItemsCost()
	{
		Assert.IsTrue( Catalogue().TryGet( Thing( DrinksShop ).CatalogueId, out var item ) );

		var bought = ParkBuilding.Constructed( item, 9000, 40, 40, 0, 0, 0, default );

		Assert.AreEqual( (50, 50, 0), (bought.QualityOfGoods, bought.AmountOfSpecialIngredient, bought.TotalCosts) );
		Assert.AreEqual( 20, ParkRideOperation.ShopCostOfGoods( item, bought ) );
	}

	/// <summary>
	/// <b>Selling a coaster withdraws nought</b>, which still writes the last balance: the demolisher's track arm sends
	/// every track type but karts' and water's to a zero withdrawal (<c>0x00528179</c>), before the refund.
	/// </summary>
	[TestMethod]
	public void SellingACoasterWithdrawsNought()
	{
		var catalogue = Catalogue();
		var world = Park();
		var coaster = catalogue.All.First( item => item.TrackType == ItemDescriptionFile.CoasterTrack && item.HasQueue );
		var bank = new ParkState( world );

		bank.Deposit( 100 );
		bank.AddObject( ParkBuilding.Constructed( coaster, 9000, 20, 12, 0, 0, 0, default ) );

		StringAssert.StartsWith( ParkBuilding.Sell( bank, world, catalogue, null, null, 9000 ), "sell: '" );

		Assert.AreEqual( 87987 + 100, bank.LastBalance, "the balance before the refund, where the save's was 87787" );
	}

	/// <summary>
	/// <b>Selling a kart or water ride is counted</b>: its track's teardown and the withdrawal for it are not built,
	/// and nothing is withdrawn.
	/// </summary>
	[TestMethod]
	public void SellingAKartRideCountsItsTeardown()
	{
		var catalogue = Catalogue();
		var world = Park();
		var karts = catalogue.All.First( item => item.TrackType == ItemDescriptionFile.CarTrack && item.HasQueue );
		var bank = new ParkState( world );
		var before = Times( "SALE_TRACK_TEARDOWN" );

		bank.AddObject( ParkBuilding.Constructed( karts, 9000, 20, 12, 0, 0, 0, default ) );
		ParkBuilding.Sell( bank, world, catalogue, null, null, 9000 );

		Assert.AreEqual( before + 1, Times( "SALE_TRACK_TEARDOWN" ) );
		Assert.AreEqual( 87787, bank.LastBalance, "no withdrawal" );
	}

	/// <summary>
	/// <b>The shipped park's month costs 538</b>: its five staff's easy wages, 7 x 9, 7 x 23, 7 x 12, 7 x 15 and
	/// 5 x 25, as measured in the original at 3.1 and 4.1.2000; its training budgets are nought, so the training
	/// withdraws nought five times, and its bank's turn moves nothing.
	/// </summary>
	[TestMethod]
	public void TheShippedParksMonthCostsItsFiveWages()
	{
		var (bank, people) = Month();

		CollectionAssert.AreEqual( new[] { 0, 0, 0, 0, 0 }, people.TrainingBudgets, "the shipped staff HQ's budgets" );

		TurnTheMonth( bank, people );

		Assert.AreEqual( 87987 - 538, bank.Balance );
		Assert.AreEqual( 87987 - 538, bank.LastBalance );
	}

	/// <summary>
	/// <b>The analyser's money rings keep the month and close it at the month's change</b>: a deposit and a gate fee
	/// onto cash in, the gate fee onto the gate takings too, a withdrawal onto all costs (none while withdrawals are off),
	/// each wage onto staff costs and all costs (a severance's, which needs the level's staff pool, is held by the game
	/// run), a training share onto training whether or not its withdrawal is made; the bank's balance sampled first at the
	/// close; the close after the training and
	/// before the bank's batch deposit and the wages, which open the new month (<c>FUN_004c7720</c>; <c>FUN_00504c70</c>,
	/// <c>FUN_00505790</c>, <c>FUN_00505a10</c>, <c>FUN_004d0600</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the balance sampled after the bank's turn or before the training; the gate fee not onto cash in; the gate fee not onto the gate takings; a deposit onto the gate
	/// takings; a withdrawal counted while withdrawals are off; the wage not onto staff costs; the training not onto
	/// training; the close after the wages; a ring not closed; a ring handed to the writer as another.
	/// </remarks>
	[TestMethod]
	public void TheAnalysersMoneyRingsKeepTheMonthAndCloseItAtTheMonthsChange()
	{
		var (bank, people) = Month();
		int[] Totals() => [bank.MonthCashIn.Total, bank.MonthStaffCosts.Total, bank.MonthCosts.Total, bank.MonthGateTakings.Total, bank.MonthTraining.Total];
		int[] Newest() => [bank.MonthCashIn.Newest, bank.MonthStaffCosts.Newest, bank.MonthCosts.Newest, bank.MonthGateTakings.Newest, bank.MonthTraining.Newest];

		CollectionAssert.AreEqual( new[] { 200, 538, 538, 200, 0 }, Totals(), "the file's month so far" );
		CollectionAssert.AreEqual( new[] { 1500, 0, 13175, 0, 0 }, Newest(), "and its last month" );
		Assert.AreEqual( (0, 88325, 0), (bank.MonthBalance.Current, bank.MonthBalance.Newest, bank.MonthBalance.Total), "the file's balance at its first close" );

		bank.Deposit( 40 );
		bank.Take( 25 );
		bank.Spend( 100 );
		bank.WithdrawalsEnabled = 0;
		bank.Spend( 1000 );
		bank.WithdrawalsEnabled = 1;

		CollectionAssert.AreEqual( new[] { 265, 538, 638, 225, 0 }, Totals() );

		people.TrainingBudgets[1] = 25;
		bank.MonthShopTakings.Add( 7 );
		bank.MonthSideshowTakings.Add( 9 );

		bank.BatchBalance = 70;

		var batch = bank.BatchBalance;
		var balance = bank.Balance;

		TurnTheMonth( bank, people );

		Assert.AreEqual( (1, balance - 25, 0), (bank.MonthBalance.Current, bank.MonthBalance.Newest, bank.MonthBalance.Total),
			"the balance sampled after the training, before the bank's turn and the wages" );

		CollectionAssert.AreEqual( new[] { 265, 538, 663, 225, 25 }, Newest(), "the training in the month, closed before the bank's turn and the wages" );
		Assert.AreEqual( (7, 0, 9, 0), (bank.MonthShopTakings.Newest, bank.MonthShopTakings.Total, bank.MonthSideshowTakings.Newest, bank.MonthSideshowTakings.Total),
			"the shops' and sideshows' months closed with them" );

		var set = bank.MonthRings;

		CollectionAssert.AreEqual(
			new[] { bank.MonthBalance, bank.MonthCosts, bank.MonthStaffCosts, bank.MonthTraining, bank.MonthCashIn, bank.MonthGateTakings, bank.MonthShopTakings, bank.MonthSideshowTakings },
			new[] { set.Balance, set.Costs, set.StaffCosts, set.Training, set.CashIn, set.GateTakings, set.ShopTakings, set.SideshowTakings }, "the writer is handed each ring as itself" );
		CollectionAssert.AreEqual( new[] { batch, 538, 538, 0, 0 }, Totals(), "the batch and the five wages open the next" );

		bank.WithdrawalsEnabled = 0;
		people.TrainTheStaff();

		Assert.AreEqual( (25, 538), (bank.MonthTraining.Total, bank.MonthCosts.Total), "the training kept, its withdrawal not made" );
	}

	/// <summary>
	/// <b>The analyser counts every admission fee taken and keeps the dearest</b> (<c>FUN_004c7520</c>, from
	/// <c>FUN_004d0600</c>): the shipped file's 8 and 25 at a load, one more a fee whether it is nought or not, the dearest
	/// raised only by a fee above it; the month's close samples the count into the 11th ring after the money rings
	/// (<c>+0x20820</c>, <c>0x004c792a</c>) and leaves the count as it stands. A park made with no file starts at nought,
	/// nought and an empty ring (<c>FUN_004c58c0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the count not raised, or raised twice; the dearest raised by any fee, or never; the dearest compared at or above; the count zeroed by the close; the ring not pushed, or pushed the gate takings; the file's count, dearest or ring not read; the writer handed another ring as the visitors.</remarks>
	[TestMethod]
	public void TheAnalyserCountsEveryAdmissionAndClosesTheCountIntoItsEleventhRing()
	{
		var fresh = Bank();

		Assert.AreEqual( (0, 0, -1, 0), (fresh.LifetimeVisitors, fresh.MostPaidForTicket, fresh.MonthVisitors.Current, fresh.MonthVisitors.Newest), "no file" );

		var (bank, people) = Month();

		Assert.AreEqual( (8, 25, 0, 0, 0), (bank.LifetimeVisitors, bank.MostPaidForTicket, bank.MonthVisitors.Current, bank.MonthVisitors.Newest, bank.MonthVisitors.Total),
			"the shipped file's eight admissions, its 25, and one month closed on nought" );

		bank.Take( 25 );
		bank.Take( 0 );
		Assert.AreEqual( (10, 25), (bank.LifetimeVisitors, bank.MostPaidForTicket), "a fee of nought is an admission too" );

		bank.Take( 30 );
		bank.Take( 26 );
		bank.Take( 30 );
		Assert.AreEqual( (13, 30), (bank.LifetimeVisitors, bank.MostPaidForTicket), "the dearest kept" );

		TurnTheMonth( bank, people );

		Assert.AreEqual( (1, 13, 0), (bank.MonthVisitors.Current, bank.MonthVisitors.Newest, bank.MonthVisitors.Total), "the count sampled at the close" );
		Assert.AreEqual( (13, 30), (bank.LifetimeVisitors, bank.MostPaidForTicket), "and left as it stands" );
		Assert.AreSame( bank.MonthVisitors, bank.MonthRings.Visitors, "the writer is handed the ring as itself" );

		bank.Take( 25 );
		TurnTheMonth( bank, people );

		Assert.AreEqual( (2, 14), (bank.MonthVisitors.Current, bank.MonthVisitors.Newest) );
	}

	/// <summary>
	/// <b>A training budget is withdrawn and buys the member points</b>: the mechanics' 25, the one step up the
	/// original was measured with, took 563 at 5.1 and moved the grade-3 mechanic 0 to 1 (25 / 15).
	/// </summary>
	[TestMethod]
	public void AMechanicsBudgetOf25BuysOnePoint()
	{
		var (bank, people) = Month();
		var mechanic = people.Staff.Single( member => member.Model == 4 );

		people.TrainingBudgets[1] = 25;
		TurnTheMonth( bank, people );

		Assert.AreEqual( (87987 - 563, 3, 1), (bank.Balance, mechanic.PayGrade, mechanic.PercentageThroughGrade) );
	}

	/// <summary>
	/// <b>At 100 points a member goes up one grade, keeps what passed 100 and is made happy</b>, and is paid at the
	/// new grade in the same change: 150 on the grade-3 mechanic at 95 buys 10 points, 105, so grade 4 at 5.
	/// </summary>
	[TestMethod]
	public void AHundredPointsPromotesAndTheWageFollows()
	{
		var (bank, people) = Month();
		var mechanic = people.Staff.Single( member => member.Model == 4 );

		mechanic.PercentageThroughGrade = 95;
		mechanic.Happiness = 10;
		people.TrainingBudgets[1] = 150;
		TurnTheMonth( bank, people );

		Assert.AreEqual( (4, 5, 100f), (mechanic.PayGrade, mechanic.PercentageThroughGrade, mechanic.Happiness) );
		Assert.AreEqual( 87987 - 150 - (538 - (7 * 23) + (9 * 23)), bank.Balance, "the mechanic paid at grade 4" );
	}

	/// <summary><b>The points one month buys are held to 100</b>: 10000 at 15 a point buys 100, not 666.</summary>
	[TestMethod]
	public void AMonthsPointsAreHeldToAHundred()
	{
		var (bank, people) = Month();
		var mechanic = people.Staff.Single( member => member.Model == 4 );

		people.TrainingBudgets[1] = 10000;
		TurnTheMonth( bank, people );

		Assert.AreEqual( (4, 0), (mechanic.PayGrade, mechanic.PercentageThroughGrade) );
		Assert.AreEqual( 87987 - 10000 - (538 - (7 * 23) + (9 * 23)), bank.Balance, "the whole budget spent" );
	}

	/// <summary>
	/// <b>A member at grade 4 takes no training, though they count in the divisor</b>: with a grade-0 mechanic hired
	/// beside the shipped one raised to 4, a budget of 50 is 25 each, and the hire's 25 buys 5 points at 5 a point.
	/// </summary>
	[TestMethod]
	public void GradeFourTakesNoTrainingButCountsInTheDivisor()
	{
		var (bank, people) = Month();
		var mechanic = people.Staff.Single( member => member.Model == 4 );
		var hire = new ParkStaffPool.Candidate( Id: 999, Kind: 1, Name: "Test", Grade: 0, Costume: 0, Wage: 3 * 23 );

		mechanic.PayGrade = 4;
		Assert.AreNotEqual( 0, people.Hire( hire, 47, 21 ) );
		var hired = people.Staff[0];

		people.TrainingBudgets[1] = 50;
		TurnTheMonth( bank, people );

		Assert.AreEqual( (4, 0), (mechanic.PayGrade, mechanic.PercentageThroughGrade), "grade 4 untouched" );
		Assert.AreEqual( (0, 5), (hired.PayGrade, hired.PercentageThroughGrade), "25 of the 50" );
		Assert.AreEqual( 87987 - 25 - (538 - (7 * 23) + (9 * 23) + (3 * 23)), bank.Balance, "one share withdrawn" );
	}

	/// <summary>
	/// <b>The bank's turn deposits the batch and pays a bought loan</b>, giving the year's profit back by the
	/// original's unsigned division: 10000 over 36 at 277 takes 119,304,646 off it (<c>0x004d0499</c>).
	/// </summary>
	[TestMethod]
	public void TheBanksTurnBanksTheBatchAndPaysALoan()
	{
		var bank = Bank( balance: 1000 );

		bank.BatchBalance = 50;
		bank.SetLoan( 3, new ParkWorld.LoanState( 1, 10000, 0, 36, 277, Bought: 1, MonthsRepaid: 34, 0 ) );
		bank.TurnTheMonth( null );

		Assert.AreEqual( (1000 + 50 - 277, 0, 35, 1), (bank.Balance, bank.BatchBalance, bank.Loans[3].MonthsRepaid, bank.Loans[3].Bought) );
		Assert.AreEqual( 50 - 119304646, bank.ProfitThisYear );

		bank.TurnTheMonth( null );

		Assert.AreEqual( (0, 0), (bank.Loans[3].MonthsRepaid, bank.Loans[3].Bought), "repaid at its period" );

		bank.TurnTheMonth( null );

		Assert.AreEqual( 1000 + 50 - 277 - 277, bank.Balance, "and paid no more" );
	}

	/// <summary>
	/// <b>Six thirty-day months in the red end the park</b>: 4148 sweeps at the shipped rate, not 4147, and world state 4
	/// (<c>FUN_005168f0</c>); the end screen counted once. The advisor is posted record <c>0x6a</c> on every month that
	/// finds it so (<c>0x0059aee7</c>): about no thing, its line his own, unforced, at <c>Bankrupted.Score</c>, into a slot.
	/// Said (here by a tick the ended park would not give him), the next month posts it again.
	/// </summary>
	/// <remarks><b>Mutations:</b> five months ending it; the end not setting state 4; a second month ending it again; the
	/// advisor not posted; posted another record; posted only in the month that ends it.</remarks>
	[TestMethod]
	public void SixMonthsInTheRedEndThePark()
	{
		GameCalendar.Rebase();
		var bank = Bank( balance: 0 );
		bank.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( new MemoryStream( System.Text.Encoding.ASCII.GetBytes(
			"GeneralAdvisor.MinScoreForConsideration 25\nMessageGroups[0].MinTimeSameMessage 0\nMessageGroups[0].SayOnlyOnce 0\n"
			+ "MessageGroups[0].DiscardAfterSlaps 0\nBankrupted.Score 100000\n" ) ) ) );
		bank.Spend( 1 );

		for ( var i = 0; i < 4147; ++i )
			bank.AdvanceGameTick();

		var screen = Times( "PARK_END_SCREEN" );
		bank.TurnTheMonth( null );

		Assert.AreEqual( (5, 0, screen, 0), (bank.MonthsInTheRed(), bank.WorldState, Times( "PARK_END_SCREEN" ),
			bank.AdvisorMessages.PendingCount), "five months: the park goes on" );

		bank.AdvanceGameTick();
		bank.TurnTheMonth( null );

		Assert.AreEqual( (6, ParkState.EndedWorldState, screen + 1), (bank.MonthsInTheRed(), bank.WorldState,
			Times( "PARK_END_SCREEN" )), "six: the park ends" );
		Assert.AreEqual( new ParkWorld.AdvisorSlot( 0, 0, 0x6a, 100000, true, -1, false, false ),
			bank.AdvisorMessages.WrittenSlots( _ => 0 )[0], "and the advisor is posted record 0x6a" );

		var said = new List<(int, int)>();
		bank.AdvisorMessages.Tick( bank.GameTick, 0, ( response, sample ) => { said.Add( (response, sample) ); return 0; } );
		CollectionAssert.AreEqual( new[] { (274, 424) }, said, "its first line" );
		Assert.AreEqual( 0, bank.AdvisorMessages.PendingCount );

		bank.TurnTheMonth( null );

		Assert.AreEqual( (ParkState.EndedWorldState, screen + 1, 1), (bank.WorldState, Times( "PARK_END_SCREEN" ),
			bank.AdvisorMessages.PendingCount), "the next month: not ended again, the advisor posted again" );
		Assert.AreEqual( -1, bank.Balance, "and the bank goes on" );
	}

	/// <summary>
	/// <b>The End feature standing, six months in the red end nothing</b>: the handler skips while the End's handle is held
	/// (<c>FUN_00516c00</c>, <c>0x004d031b</c>), the advisor still posted record <c>0x6a</c> (his handler comes first).
	/// </summary>
	/// <remarks><b>Mutation:</b> the End feature's test dropped from the bank's guard.</remarks>
	[TestMethod]
	public void SixMonthsInTheRedEndNothingWhileTheEndStands()
	{
		GameCalendar.Rebase();
		var bank = Bank( balance: 0 );
		bank.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( new MemoryStream( System.Text.Encoding.ASCII.GetBytes(
			"GeneralAdvisor.MinScoreForConsideration 25\nMessageGroups[0].MinTimeSameMessage 0\nMessageGroups[0].SayOnlyOnce 0\n"
			+ "MessageGroups[0].DiscardAfterSlaps 0\nBankrupted.Score 100000\n" ) ) ) );
		bank.Spend( 1 );

		for ( var i = 0; i < 4148; ++i )
			bank.AdvanceGameTick();

		var current = typeof( ParkEndFeature ).GetProperty( nameof( ParkEndFeature.Current ),
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )!;
		var screen = Times( "PARK_END_SCREEN" );

		try
		{
			current.SetValue( null, System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( ParkEndFeature ) ) );
			bank.TurnTheMonth( null );
		}
		finally
		{
			current.SetValue( null, null );
		}

		Assert.AreEqual( (6, 0, screen, 1), (bank.MonthsInTheRed(), bank.WorldState, Times( "PARK_END_SCREEN" ),
			bank.AdvisorMessages.PendingCount) );

		bank.TurnTheMonth( null );
		Assert.AreEqual( ParkState.EndedWorldState, bank.WorldState, "the End gone, the next month ends it" );
	}

	/// <summary>
	/// <b>The end frees the ids deleted in its sweep</b>, as the sweep's end does: its loop empties the same list
	/// (<c>0x005169f7</c>..<c>0x00516acf</c>, <c>FUN_00516200</c>'s work inline), so the first deleted is the first given out.
	/// </summary>
	/// <remarks><b>Mutation:</b> the end leaving the list as it is.</remarks>
	[TestMethod]
	public void TheEndFreesThePendingDeletes()
	{
		var bank = Bank( balance: 0 );
		var (first, second) = (bank.NextThingId(), bank.NextThingId());

		bank.DeleteThing( first );
		bank.DeleteThing( second );
		bank.EndThePark();

		Assert.AreEqual( 0, bank.DeletedThingIds.Count );
		Assert.AreEqual( (first, second), (bank.NextThingId(), bank.NextThingId()), "the first deleted the first given out" );
	}

	/// <summary>
	/// <b>Six months after going red, a park climbed out on deposits alone does not end</b>: a deposit never writes
	/// <c>mLastBalance</c>, so the count still reads six, but the balance is not below nought (<c>0x004d054c</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the balance's test left off; the last balance read in its place.</remarks>
	[TestMethod]
	public void AParkClimbedOutOnDepositsDoesNotEnd()
	{
		GameCalendar.Rebase();
		var bank = Bank( balance: 0 );
		bank.Spend( 1 );
		bank.Deposit( 5 );

		for ( var i = 0; i < 4148; ++i )
			bank.AdvanceGameTick();

		var screen = Times( "PARK_END_SCREEN" );
		bank.TurnTheMonth( null );

		Assert.AreEqual( (6, -1, 4, 0, screen), (bank.MonthsInTheRed(), bank.LastBalance, bank.Balance, bank.WorldState,
			Times( "PARK_END_SCREEN" )) );
	}

	/// <summary>
	/// <b>A park loaded in world state 4 has ended</b>: the state is the file's (<c>mWorldState</c>, header field 19), so
	/// the bank does not end it again; and an ended park's save writes 4 back where the file's 0 was. Loaded, it draws
	/// nobody: the load clears the render flag for state 4.
	/// </summary>
	/// <remarks><b>Mutations:</b> the state not read from the file; the writer not writing it, or writing it elsewhere; the
	/// load's put-back not counted, or counted for any state; the load's render flag set for state 4
	/// (<c>0x00518d82</c>..<c>0x00518d93</c>), or cleared for any state.</remarks>
	[TestMethod]
	public void AnEndedParksStateIsWrittenAndReadBack()
	{
		var data = GameData.Required();
		using var stream = data.OpenRead( "levels/jungle/Easymode.TPWI" );
		var shipped = new ParkWorld( new SaveReader( stream ).ReadFile() );

		Assert.AreEqual( 0, new ParkState( shipped ).WorldState, "the shipped park is not ended" );

		var ended = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value,
			WorldState: ParkState.EndedWorldState ) );
		var plain = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value ) );
		var written = new ParkWorld( ended );

		Assert.AreEqual( (ParkState.EndedWorldState, 0), (written.WorldState, new ParkWorld( plain ).WorldState) );
		Assert.AreEqual( 1, Enumerable.Range( 0, ended.Length ).Count( at => ended[at] != plain[at] ), "one byte apart" );

		var putBack = Times( "PARK_END_SCREEN_PUT_BACK" );
		var loaded = new ParkState( written );
		Assert.IsTrue( loaded.Ended, "a park run from it has ended" );
		Assert.AreEqual( putBack + 1, Times( "PARK_END_SCREEN_PUT_BACK" ), "its end screen counted at the load" );
		Assert.IsFalse( loaded.SpritesDrawn, "and its render flag cleared, so nobody is drawn" );

		var running = new ParkState( new ParkWorld( plain ) );
		Assert.AreEqual( putBack + 1, Times( "PARK_END_SCREEN_PUT_BACK" ), "and not for a park running" );
		Assert.IsTrue( running.SpritesDrawn, "whose render flag is set" );
	}

	/// <summary>
	/// <b>The month's close zeroes this month's two counts of misbehaving kids</b> (<c>0x004c79eb</c>, <c>0x004c79f1</c>):
	/// a copy of the shipped park given 5, 2 and 9 at the analyser's record loads 5, 2 and nought, the original's load
	/// never reading the third (<c>0x004c719a</c>), and closes on nought, counting no gap. A park made with no file starts
	/// at nought (<c>FUN_004c58c0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the file's counts not read, or the third read; the close zeroing neither or one; the gap counted again.</remarks>
	[TestMethod]
	public void TheMonthsCloseZeroesThisMonthsMisbehavingKids()
	{
		Assert.AreEqual( new ParkWorld.MisbehavingKidCounts( 0, 0, 0 ), Bank().MisbehavingKids, "no file" );

		var payload = Payload();
		var at = new ParkWorld( payload ).AnalyserAt + 73468;
		Put( payload, at, 5 );
		Put( payload, at + 4, 2 );
		Put( payload, at + 8, 9 );
		var world = new ParkWorld( payload );
		var bank = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, bank );
		var before = Times( "ANALYSER_MONTH_CLOSE" );

		Assert.AreEqual( new ParkWorld.MisbehavingKidCounts( 5, 2, 9 ), world.MisbehavingKids, "the file's" );
		Assert.AreEqual( new ParkWorld.MisbehavingKidCounts( 5, 2, 0 ), bank.MisbehavingKids, "the park's" );
		Assert.AreEqual( 5, people.AdvisorFacts().MisbehavingKids, "row 12 reads this month's, caught or not" );
		Assert.AreEqual( 2, people.AdvisorFacts().KidsGotAway, "row 98 reads this month's who got away" );

		TurnTheMonth( bank, people );

		Assert.AreEqual( new ParkWorld.MisbehavingKidCounts( 0, 0, 0 ), bank.MisbehavingKids );
		Assert.AreEqual( 0, people.AdvisorFacts().MisbehavingKids );
		Assert.AreEqual( 0, people.AdvisorFacts().KidsGotAway );
		Assert.AreEqual( before, Times( "ANALYSER_MONTH_CLOSE" ), "the close is built whole" );
	}

	private (ParkState Bank, ParkPeople People) Month()
	{
		var world = Park();
		var bank = new ParkState( world );

		return (bank, new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, bank ));
	}

	/// <summary>The month's change as <see cref="Level"/> sends it.</summary>
	private void TurnTheMonth( ParkState bank, ParkPeople people ) => ParkPeople.TurnTheMonth( people, bank, Catalogue() );

	/// <summary>
	/// <b>A purchase is a withdrawal of the item's price</b>, and a mystery ride's spends golden tickets in its place
	/// (<c>docs/exe/hud.md</c>, "Spending golden tickets"), as the original's on <c>q709/gms-two.dat</c>: 4 tickets earned,
	/// 2 spent. Jurassic Tours (1) is bought, the bank untouched, its id put in order and the spent count 3; bought again
	/// it is paid in cash and spends nothing; Eruption (3) is more than the one left, so nothing is spent and, as the
	/// original's, no cash taken either.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the cash taken as well; the cost added on an id already there; the id appended, not put in
	/// order; the tickets compared strictly; a mystery ride short of tickets paid in cash.
	/// </remarks>
	[TestMethod]
	public void APurchaseWithdrawsThePriceOrSpendsAMysteryRidesTickets()
	{
		var catalogue = Catalogue();
		var tours = catalogue.All.Single( item => item.Id == 1170 );
		var eruption = catalogue.All.Single( item => item.Id == 1112 );
		Assert.AreEqual( (1, 3), (tours.GoldenTicketCost, eruption.GoldenTicketCost) );
		Assert.IsTrue( catalogue.TryGet( Park().Objects.Single( o => o.ThingId == 13 ).CatalogueId, out var bounce ) );

		var file = new PlayerFile { RecordTickets = [1, 1, 1, 1], TicketsSpent = 2, RideIds = [1200] };
		var player = new Player( 0, "Test", file );
		var bank = Bank( balance: 100000 );

		Assert.AreEqual( "500", ParkBuilding.PayFor( bank, bounce, player ) );
		Assert.AreEqual( 100000 - bounce.BuildPrice, bank.Balance );

		Assert.AreEqual( "1 golden tickets", ParkBuilding.PayFor( bank, tours, player ) );
		Assert.AreEqual( (100000 - bounce.BuildPrice, 3, "1170 1200"), (bank.Balance, file.TicketsSpent, string.Join( ' ', file.RideIds )) );

		Assert.AreEqual( $"{tours.BuildPrice}", ParkBuilding.PayFor( bank, tours, player ), "bought: its own price" );
		Assert.AreEqual( (100000 - bounce.BuildPrice - tours.BuildPrice, 3), (bank.Balance, file.TicketsSpent) );

		Assert.AreEqual( 1, player.TicketsUnspent );
		Assert.AreEqual( "no golden tickets", ParkBuilding.PayFor( bank, eruption, player ), "nothing was spent, and it says so" );
		Assert.AreEqual( (100000 - bounce.BuildPrice - tours.BuildPrice, 3, "1170 1200"),
			(bank.Balance, file.TicketsSpent, string.Join( ' ', file.RideIds )), "too few: built free, nothing spent" );

		file.TicketsSpent = 1;
		Assert.AreEqual( 3, player.TicketsUnspent );
		ParkBuilding.PayFor( bank, eruption, player );
		Assert.AreEqual( (4, "1112 1170 1200"), (file.TicketsSpent, string.Join( ' ', file.RideIds )), "three left buy a cost of three" );
	}

	/// <summary>
	/// <c>PlayerProgress_BuyRide</c> adds the cost only where the id is new, and the tickets have no floor: a player who
	/// has spent more than they earned is short of any cost.
	/// </summary>
	/// <remarks><b>Mutations:</b> the cost added on an id already there; the tickets floored at nought.</remarks>
	[TestMethod]
	public void TheSpendAddsTheCostOnlyForANewRide()
	{
		var ride = new ParkItemCatalogue.Item( 1170, "Ride", "", "ride", 0, 1, null, GoldenTicketCost: 2 );
		var file = new PlayerFile { TicketsSpent = 5, RideIds = [1170] };

		Assert.IsFalse( GoldenTickets.SpendForRide( file, ride, 9 ) );
		Assert.AreEqual( 5, file.TicketsSpent );

		Assert.IsFalse( GoldenTickets.CanBuyRide( ride with { GoldenTicketCost = 0 }, -1 ), "no floor: one ticket owed buys not even a cost of nought" );
		Assert.IsTrue( GoldenTickets.CanBuyRide( ride with { GoldenTicketCost = -2 }, -1 ) );
		Assert.IsFalse( GoldenTickets.SpendForRide( null, ride, 9 ), "nobody playing" );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	/// <summary>The one offset in the payload where <paramref name="matches"/> holds.</summary>
	private static int RecordAt( byte[] payload, Func<int, bool> matches )
	{
		var found = Enumerable.Range( 0, payload.Length - 1100 ).Where( matches ).ToList();

		Assert.AreEqual( 1, found.Count, "exactly one record answers to those fields" );

		return found[0];
	}

	private static int U16( byte[] payload, int at ) => BitConverter.ToUInt16( payload, at );

	private static int I32( byte[] payload, int at ) => BitConverter.ToInt32( payload, at );

	private static void Put( byte[] payload, int at, int value ) => BitConverter.GetBytes( value ).CopyTo( payload, at );
}
