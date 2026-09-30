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
				new ParkWorld.CatalogueObject( 9, 1, 0, 0, 0, QualityOfGoods: quality, AmountOfSpecialIngredient: amount ) );

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
				new ParkWorld.CatalogueObject( 9, id, 0, 0, 0, QualityOfGoods: quality, AmountOfSpecialIngredient: amount ) ),
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
	/// <b>The month's change counts a wage and a training share for each member of staff</b>, and the bank's turn
	/// once.
	/// </summary>
	[TestMethod]
	public void TheMonthsChangeCountsEachMemberOfStaff()
	{
		var world = Park();
		var staff = ParkPeople.StaffIn( world ).Count;
		var people = new ParkPeople( world );
		var (wages, training) = (Times( "STAFF_MONTHLY_WAGE" ), Times( "STAFF_MONTHLY_TRAINING" ));

		Assert.IsTrue( staff > 0, "the shipped park has staff" );

		people.TurnTheMonth();

		Assert.AreEqual( (wages + staff, training + staff), (Times( "STAFF_MONTHLY_WAGE" ), Times( "STAFF_MONTHLY_TRAINING" )) );
	}

	/// <summary>
	/// <b>A purchase is a withdrawal of the item's price</b>, and one with a golden-ticket cost is counted, as its
	/// tickets are not built: Lost Kingdom has two such rides, and the Belly Bounce is not one.
	/// </summary>
	[TestMethod]
	public void APurchaseWithdrawsThePriceAndCountsAGoldenTicketCost()
	{
		var catalogue = Catalogue();
		var ticketed = catalogue.All.First( item => item.GoldenTicketCost > 0 );
		Assert.IsTrue( catalogue.TryGet( Park().Objects.Single( o => o.ThingId == 13 ).CatalogueId, out var bounce ) );
		var bank = Bank( balance: 100000 );
		var before = Times( "PURCHASE_GOLDEN_TICKET_ARM" );

		ParkBuilding.PayFor( bank, bounce );

		Assert.AreEqual( (before, 100000 - bounce.BuildPrice), (Times( "PURCHASE_GOLDEN_TICKET_ARM" ), bank.LastBalance) );

		ParkBuilding.PayFor( bank, ticketed );

		Assert.AreEqual( before + 1, Times( "PURCHASE_GOLDEN_TICKET_ARM" ), $"'{ticketed.Name}' costs golden tickets" );
		Assert.AreEqual( 100000 - bounce.BuildPrice - ticketed.BuildPrice, bank.Balance, "and is paid in cash here" );
	}

	/// <summary><b>The month's change reaches the bank once</b>, counted: its month turn is not built.</summary>
	[TestMethod]
	public void TheMonthsChangeCountsTheBanksTurn()
	{
		var bank = Bank( balance: 100 );
		var before = Times( "BANK_MONTH_TURN" );

		bank.TurnTheMonth();

		Assert.AreEqual( (before + 1, 100), (Times( "BANK_MONTH_TURN" ), bank.Balance) );
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
