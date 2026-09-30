using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A shop's own terms of a visit, on its amount of special ingredient - <see cref="ParkRideOperation.TakeTheIngredient"/>,
/// the rest of <c>FUN_004fe1e0</c>'s step 3 and its step 3b (<c>docs/exe/ride-operation.md</c>, "The effects of a
/// visit"): the two docks, the happiness the amount adds, and fat, salt, ice and sugar.
/// <para>
/// These read Lost Kingdom's save and catalogue, whose shops carry their effects in the <c>.sam</c> inside their own
/// <c>.wad</c>, and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkIngredientTests
{
	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	private const int DrinksShop = 16;

	private const int BalloonShop = 1209, BurgerShop = 1207, FriesShop = 1212, GiftShop = 1211, IceCreamShop = 1206;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
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

	private static Peep Guest( float happiness = 50f, float thirst = 80f, float hunger = 80f, float toilet = 10f )
		=> new( 7, new ParkWorld.GuestState(
			State: (int)PeepState.Riding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: happiness, Thirst: thirst, Hunger: hunger, Toilet: toilet, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 1, PrankeryIndex: 0 ), StandingStill );

	/// <summary>The park's own mood constants - <c>PeepInfo.SmallHappinessChange</c> is 5 in this stack.</summary>
	private static ParkAdmission Mood() => new( new ParkBalance( "jungle", easyMode: true ), 25 );

	/// <summary>The Drinks Shop the park is saved with, standing in for a shop of <paramref name="item"/>.</summary>
	private ParkWorld.CatalogueObject Shop( int item = 1203, int amount = 50 )
		=> Park().Objects.Single( o => o.ThingId == DrinksShop ) with { CatalogueId = item, AmountOfSpecialIngredient = amount };

	/// <summary>Lets <paramref name="peep"/> off <paramref name="shop"/>, having won, through <see cref="ParkRideOperation.Dismiss"/>.</summary>
	private Peep LetOff( Peep peep, ParkWorld.CatalogueObject shop, Random random )
	{
		var script = Script();

		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		Assert.IsTrue( new ParkRideOperation( new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 ),
				new Dictionary<int, Peep> { [peep.ThingId] = peep }, Mood() )
			.Dismiss( script, shop, tick: 9, random, catalogue: Catalogue() ),
			"the guest should have been let off" );

		return peep;
	}

	/// <summary>Every draw it is asked for, answered from a list and counted.</summary>
	private sealed class Draws( params int[] answers ) : Random
	{
		public int Taken { get; private set; }

		public override int Next()
		{
			var answer = answers.Length == 0 ? 0 : answers[Taken % answers.Length];

			Taken++;

			return answer;
		}
	}

	/// <summary>
	/// <b>A drink gives back twenty of the forty it quenched and two more happiness</b>, the original's measured drinker:
	/// thirst 36 to 20, happiness 50 to 57. Ice adds the amount times the thirst effect over a hundred, after the forty is
	/// taken and held at nought; the amount adds its times the five over a hundred, truncated.
	/// </summary>
	[TestMethod]
	public void ADrinkGivesBackTwentyOfItsFortyAndTwoMoreHappiness()
	{
		var peep = LetOff( Guest( happiness: 50f, thirst: 36f ), Shop(), new Draws() );

		Assert.AreEqual( 20f, peep.Thirst, 0.001f, "36 less 40 is held at nought, then the ice's 50 x 40 / 100" );
		Assert.AreEqual( 57f, peep.Happiness, 0.001f, "50, the drink's 5, then 50 x 5 / 100" );
	}

	/// <summary>
	/// <b>The ice and the happiness follow the shop's amount</b>, and a sum past a hundred is held there: from thirst 80
	/// the drink leaves 40, then the ice gives back nought, 20, 40 or 102 at the amounts 0, 50, 100 and 255.
	/// </summary>
	[TestMethod]
	public void TheIceAndTheHappinessFollowTheAmount()
	{
		foreach ( var (amount, thirst, happiness) in new[] { (0, 40f, 55f), (50, 60f, 57f), (100, 80f, 60f), (255, 100f, 67f) } )
		{
			var peep = LetOff( Guest(), Shop( amount: amount ), new Draws() );

			Assert.AreEqual( (thirst, happiness), (peep.Thirst, peep.Happiness), $"at an amount of {amount}" );
		}
	}

	/// <summary>
	/// <b>Each effect that is not nought takes one draw, whether or not its dock can fire</b>: the Drinks Shop's thirst
	/// one, the Ice Cream Shop's two, the Burger Shop's hunger one; the Balloon and Gift Shops declare neither.
	/// </summary>
	[TestMethod]
	public void EachEffectDrawsOnceWhetherOrNotItsDockCanFire()
	{
		foreach ( var (item, draws) in new[] { (1203, 1), (IceCreamShop, 2), (BurgerShop, 1), (BalloonShop, 0), (GiftShop, 0) } )
		{
			var random = new Draws();

			LetOff( Guest(), Shop( item ), random );

			Assert.AreEqual( draws, random.Taken, $"item {item}" );
		}
	}

	/// <summary>
	/// <b>A dock fires when the draw's low three bits, the amount and the effect sum under thirty</b>: the Burger Shop's
	/// hunger 25 at an amount of nought docks the small change, five, on a draw ending 0 to 4 and not on 5 to 7.
	/// </summary>
	[TestMethod]
	public void ADockFiresUnderThirtyOnTheDrawsLowThreeBits()
	{
		foreach ( var (draw, happiness) in new[] { (4, 50f), (5, 55f), (8, 50f), (12, 50f), (13, 55f), (7, 55f) } )
		{
			var peep = LetOff( Guest(), Shop( BurgerShop, amount: 0 ), new Draws( draw ) );

			Assert.AreEqual( happiness, peep.Happiness, 0.001f, $"a draw of {draw}: 50, the burger's 5, and the dock or not" );
		}
	}

	/// <summary>
	/// <b>The two docks are independent</b>: the Ice Cream Shop at an amount of nought docks on its hunger 15 and again on
	/// its thirst 5, whatever it draws.
	/// </summary>
	[TestMethod]
	public void TheIceCreamShopAtNoAmountDocksTwice()
	{
		var peep = LetOff( Guest(), Shop( IceCreamShop, amount: 0 ), new Draws( 7 ) );

		Assert.AreEqual( 45f, peep.Happiness, 0.001f, "50, the ice cream's 5, and two docks of 5" );
	}

	/// <summary>
	/// <b>Hunger's dock draws first, then thirst's</b>: the Ice Cream Shop at an amount of 10 handed a 7 and then a 0 docks
	/// once, hunger's 15 + 10 + 7 being 32 and thirst's 5 + 10 + 0 being 15; the other way round it would dock twice.
	/// </summary>
	[TestMethod]
	public void HungersDockDrawsBeforeThirsts()
	{
		var peep = LetOff( Guest(), Shop( IceCreamShop, amount: 10 ), new Draws( 7, 0 ) );

		Assert.AreEqual( 50f, peep.Happiness, 0.001f, "50, the ice cream's 5, one dock of 5 and 10 x 5 / 100, nought" );
	}

	/// <summary>
	/// <b>Fat adds the amount to the toilet need, salt to thirst</b>, each held to a hundred; neither docks at the stock
	/// amount.
	/// </summary>
	[TestMethod]
	public void FatFillsTheToiletNeedAndSaltTheThirst()
	{
		var burger = LetOff( Guest( toilet: 10f ), Shop( BurgerShop ), new Draws() );

		Assert.AreEqual( 60f, burger.Toilet, 0.001f, "10 and the amount's 50" );
		Assert.AreEqual( 57f, burger.Happiness, 0.001f, "no dock at 50 + 25" );

		var fries = LetOff( Guest( thirst: 10f ), Shop( FriesShop ), new Draws() );

		Assert.AreEqual( 60f, fries.Thirst, 0.001f, "the fries quench nothing, and the salt adds 50" );

		var salty = LetOff( Guest( thirst: 70f ), Shop( FriesShop ), new Draws() );

		Assert.AreEqual( 100f, salty.Thirst, 0.001f, "held at a hundred" );
	}

	/// <summary>
	/// <b>Sugar adds the amount times six over a hundred to the guest's speed word</b>: three at the stock amount,
	/// fifteen at the most, and nothing to any need.
	/// </summary>
	[TestMethod]
	public void SugarAddsToTheSpeedWord()
	{
		foreach ( var (amount, adjustor) in new[] { (50, 3), (255, 15), (16, 0) } )
		{
			var peep = LetOff( Guest(), Shop( IceCreamShop, amount: amount ), new Draws() );

			Assert.AreEqual( adjustor, peep.AdjustorSpeed, $"at an amount of {amount}" );
		}
	}

	/// <summary>
	/// <b>The dock's sum is compared unsigned</b>, so an effect that takes it below nought never docks.
	/// </summary>
	[TestMethod]
	public void ANegativeSumNeverDocks()
	{
		var peep = Guest();

		Ingredient().TakeTheIngredient( peep, Shop( amount: 0 ), Drink() with { ThirstEffect = -40 }, new Draws( 0 ) );

		Assert.AreEqual( 50f, peep.Happiness, 0.001f, "-40 + 0 + 0 is past thirty unsigned" );
	}

	/// <summary>
	/// <b>An ingredient of five, the one other the file allows, does nothing</b>, nor does nought: at the stock amount
	/// the meters the four arms move are left alone, and only the amount's happiness is added.
	/// </summary>
	[TestMethod]
	public void AFifthIngredientOrNoneDoesNothing()
	{
		foreach ( var ingredient in new[] { 0, 5 } )
		{
			var peep = Guest();

			Ingredient().TakeTheIngredient( peep, Shop(), Drink() with { SpecialIngredient = ingredient }, new Draws( 0 ) );

			Assert.AreEqual( (52f, 80f, 10f, 0), (peep.Happiness, peep.Thirst, peep.Toilet, peep.AdjustorSpeed),
				$"ingredient {ingredient}: 50 and 50 x 5 / 100" );
		}
	}

	private ParkItemCatalogue.Item Drink()
	{
		Assert.IsTrue( Catalogue().TryGet( 1203, out var drink ) );

		return drink;
	}

	private static ParkRideOperation Ingredient()
		=> new( new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 0 ), new Dictionary<int, Peep>(), Mood() );
}
