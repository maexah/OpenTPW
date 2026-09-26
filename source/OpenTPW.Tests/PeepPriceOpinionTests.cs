using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest at a thing's door thinks its price is worth, and walking away when it is too much -
/// <c>FUN_004fde50</c> and the first arm of <c>FUN_005006b0</c>. See <c>docs/exe/ride-operation.md</c>, "At
/// the door".
///
/// <para>
/// The worths are the original's arithmetic worked by hand from the shipped items: the Drinks Shop costs 20
/// to provide, quenches 40 thirst, adds 10 vomit and 5 happiness, and shops take a <c>RipOffOK</c> of 100; the
/// Jungle Spray's prize is 50 at a chance of 25, with a <c>RipOffOK</c> of 250 and no effects.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class PeepPriceOpinionTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int DrinksShop = 16;

	private const int DrinksShopItem = 1203, JungleSprayItem = 1303, BellyBounceItem = 1100;

	private const float Before = 50f;

	/// <summary><c>MediumHappinessChange</c> twice: once at the door (<c>0x00500778</c>), once put out (<c>0x005007b4</c>).</summary>
	private const float WalkedAway = 20f;

	private ParkItemCatalogue.Item Item( int id )
	{
		Assert.IsTrue( new ParkItemCatalogue( "jungle", data ).TryGet( id, out var item ), $"item {id} is in the jungle" );

		return item;
	}

	private static Peep Guest( int cash, float happiness, float thirst = 10f, float hunger = 10f, float vomit = 0f,
		int thingId = 90, int majorDest = 0, PeepState state = PeepState.BeingAdmitted )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0,
			Cash: cash, ExitLevel: 100, Happiness: happiness, Thirst: thirst, Hunger: hunger, Toilet: 10f,
			Vomit: vomit, Litter: 0f, MajorDest: majorDest, QueuePos: 0, PrankeryIndex: 0 ), Stuck );

	/// <summary>A guest whose walk has given up, which the door takes as arriving ("Got stuck in middle of...").</summary>
	private static ParkWorld.NavigatorState Stuck => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 1, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>The three keys the opinion reads beside the effects, as the shipped files give them.</summary>
	[TestMethod]
	public void TheOpinionsKeysAreReadFromTheFilesThatSetThem()
	{
		var shop = Item( DrinksShopItem );
		var spray = Item( JungleSprayItem );
		var bounce = Item( BellyBounceItem );

		Assert.AreEqual( 100, shop.RipOffOK, "Shops.sam, the category" );
		Assert.AreEqual( 250, spray.RipOffOK, "SideShow.sam, the category" );
		Assert.AreEqual( 0, bounce.RipOffOK, "Rides.sam declares none" );
		Assert.AreEqual( 3, shop.SpecialIngredient, "Coconut.sam's own override: ice" );
		Assert.AreEqual( 0, shop.AppearanceEffect, "a drink changes nobody's looks" );
		Assert.AreEqual( 20, shop.CostOfGoods, "Coconut.sam's own cost of goods" );
	}

	/// <summary>
	/// <b>Happiness 50, thirst 10, hunger 10, vomit 0 at the Drinks Shop: 72.</b> Mood 100 + 5×50/100 + 40×10/100
	/// = 106; the goods 115×20/100 = 23; 106×23/100 = 24; ×(100+100)/100 = 48; ×(50+100)/100 = 72.
	/// </summary>
	[TestMethod]
	public void TheDrinksShopIsWorthWhatTheOriginalWorksOut()
	{
		var shop = Item( DrinksShopItem );

		Assert.AreEqual( 72u, PeepPriceOpinion.Worth( Guest( 300, Before ), shop ) );
		Assert.AreEqual( 42u, PeepPriceOpinion.Worth( Guest( 300, 0f, thirst: 0f, hunger: 0f, vomit: 100f ), shop ),
			"the least it is worth: 95×23/100 = 21, doubled, at happiness nought" );
		Assert.AreEqual( 128u, PeepPriceOpinion.Worth( Guest( 300, 100f, thirst: 100f, hunger: 0f ), shop ),
			"the most: 140×23/100 = 32, doubled, and doubled again at happiness a hundred" );
		Assert.AreEqual( 1, PeepPriceOpinion.SamplesPushed( shop ), "one price sample, for the ice" );
	}

	/// <summary>
	/// <b>A sideshow's prize enters its worth beside its cost of goods; a shop has no prize.</b> The Jungle Spray: 115×50/100
	/// = 57 for the goods at mood 100, 50×25/100 = 12 for the prize, ×350/100 = 241, then ×(happiness+100)/100.
	/// </summary>
	[TestMethod]
	public void TheJungleSprayIsWorthItsPrizeToo()
	{
		var spray = Item( JungleSprayItem );

		Assert.AreEqual( 241u, PeepPriceOpinion.Worth( Guest( 300, 0f ), spray ) );
		Assert.AreEqual( 482u, PeepPriceOpinion.Worth( Guest( 300, 100f ), spray ) );
		Assert.AreEqual( 1, PeepPriceOpinion.SamplesPushed( spray ), "a sideshow pushes one sample" );
		Assert.AreEqual( 0, PeepPriceOpinion.SamplesPushed( Item( BellyBounceItem ) ), "a ride pushes none" );
	}

	/// <summary>
	/// <b>Both tests, at their edges.</b> Too expensive is a price above the worth or above the cash, so a guest
	/// holding exactly the price, or facing a price exactly its worth, still goes in.
	/// </summary>
	[TestMethod]
	[DataRow( 30, 29, true, "a guest short of the price" )]
	[DataRow( 30, 30, false, "a guest holding exactly the price" )]
	[DataRow( 73, 300, true, "a price one over what the drink is worth to them" )]
	[DataRow( 72, 300, false, "a price exactly what it is worth" )]
	[DataRow( 30, -5, false, "cash below nought, compared unsigned, reads as a fortune" )]
	[DataRow( 0, 0, false, "no price, and nothing is asked" )]
	public void TooExpensiveIsThePriceAboveTheWorthOrTheCash( int price, int cash, bool expected, string why )
		=> Assert.AreEqual( expected, PeepPriceOpinion.TooExpensive( Guest( cash, Before ), price, Item( DrinksShopItem ) ), why );

	/// <summary>
	/// <b>A guest short of the price walks away from the door, and it costs them thirty.</b> Fifteen at the door and
	/// fifteen more put out of the queue; they let go of the shop and think again, the shop lets go of them, and
	/// whoever queued behind them is at the front.
	/// </summary>
	[TestMethod]
	public void AGuestShortOfThePriceWalksAwayFromTheDoor()
	{
		var (state, peep, behind, _) = AtTheDoor( cash: 10 );

		Assert.AreEqual( PeepState.Deciding, peep.State, "they think again" );
		Assert.AreEqual( 0, peep.MajorDest, "and name the shop no longer" );
		Assert.AreEqual( WalkedAway, peep.Happiness, 0.001f, "fifteen at the door and fifteen put out of the queue" );
		Assert.AreEqual( 10, peep.Cash, "nothing was charged" );
		Assert.AreEqual( 0, state.PersonBeingLoaded( DrinksShop ), "the shop forgets them" );
		Assert.AreEqual( -1, state.PositionInQueue( DrinksShop, peep.ThingId ), "they are out of its queue" );
		Assert.AreEqual( behind.ThingId, state.FirstInQueue( DrinksShop ), "and the guest behind is at the front" );
		Assert.AreEqual( 0, state.PreviousInQueue( behind.ThingId ), "with nobody before them" );
	}

	/// <summary><b>The control: a guest who can pay goes in</b>, is handed to the shop, and loses nothing.</summary>
	[TestMethod]
	public void AGuestWhoCanPayIsAdmitted()
	{
		var (state, peep, _, script) = AtTheDoor( cash: 30 );

		Assert.AreEqual( PeepState.EnteringRide, peep.State, "handed over" );
		Assert.AreEqual( Before, peep.Happiness, 0.001f, "at no cost to their mood" );
		Assert.AreEqual( peep.ThingId, script[ParkRideOperation.AdmitVariable], "the shop's script has them" );
		Assert.AreEqual( peep.ThingId, state.FirstInQueue( DrinksShop ), "still its head until the script takes them" );
	}

	/// <summary>
	/// <b>The same walk-away through the park's own people</b>, so the ride's side is the one
	/// <see cref="ParkPeople"/> wires to its guests rather than a copy: a saved guest at the Drinks Shop's door
	/// with 10, their walk given up (which the door takes as arriving), and the park ticked as a level ticks it
	/// until their turn comes. The shop must let go of them and its queue must be empty.
	/// <para>
	/// <b>The shop is put in state 3</b>, whose turn does nothing (<c>FUN_004e0e00</c> sends it straight to its
	/// return). An open shop's turn drops a nominee who is not boarding and a head who is not queueing a moment
	/// later, which would hide a walk-away that told the shop nothing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ThePeoplesOwnWalkAwayMakesTheShopForgetThem()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );

		using var rse = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( rse ) );

		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( "jungle", data ), id => id == DrinksShop ? script : null );

		try
		{
			var peep = people.Peeps.First( guest => guest.ExitLevel > 0 );

			peep.MajorDest = DrinksShop;
			peep.Cash = 10;
			peep.Happiness = Before;
			peep.Thirst = peep.Hunger = peep.Toilet = peep.Vomit = 10f;
			peep.SetState( PeepState.BeingAdmitted, tick: 1, new Random( 1 ) );
			state.JoinQueue( DrinksShop, peep.ThingId );
			state.NominateForLoading( DrinksShop, peep.ThingId );
			peep.Navigator.GiveUp();

			var shop = state.Objects.Single( thing => thing.ThingId == DrinksShop );
			Assert.IsTrue( state.ReplaceObject( shop with { State = 3 } ), "the shop's own turn is taken out" );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( 0f );

			for ( var frame = 0; frame < 120 && peep.State == PeepState.BeingAdmitted; ++frame )
			{
				Frame( 1f / 60f );
				people.Update();
			}

			Assert.AreEqual( PeepState.Deciding, peep.State, "they walked away from the door" );
			Assert.AreEqual( WalkedAway, peep.Happiness, 0.001f, "at thirty" );
			Assert.AreEqual( 0, state.PersonBeingLoaded( DrinksShop ), "the shop let go of them" );
			Assert.AreNotEqual( peep.ThingId, state.FirstInQueue( DrinksShop ), "and they are not its head" );
			Assert.AreEqual( -1, state.PositionInQueue( DrinksShop, peep.ThingId ), "nor anywhere in its queue" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// One frame, through both clocks, in the order a frame runs them: <c>Time.Update</c> in
	/// <c>Renderer.Update</c>, then <c>GameClock.Update</c> in <see cref="Level.Update"/>.
	/// </summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>
	/// The Drinks Shop's queue with this guest at its front, nominated and arrived at the door, and a second guest
	/// behind; one turn of the first. A Belly Bounce script stands in for the shop's: both declare
	/// <c>VAR_LETMEON</c> by name, and the script is not run.
	/// </summary>
	private (ParkState State, Peep Peep, Peep Behind, RideScript Script) AtTheDoor( int cash )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );

		using var rse = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( rse ) );

		var peep = Guest( cash, Before, thingId: 90, majorDest: DrinksShop );
		var behind = Guest( 300, Before, thingId: 91, majorDest: DrinksShop, state: PeepState.InQueue );

		state.JoinQueue( DrinksShop, peep.ThingId );
		state.JoinQueue( DrinksShop, behind.ThingId );
		state.NominateForLoading( DrinksShop, peep.ThingId );

		var guests = new Dictionary<int, Peep> { [90] = peep, [91] = behind };

		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ),
			admit: ( ride, id ) => new ParkRideOperation( state, guests ).AdmitPerson( script, ride, id ),
			walkAway: ( ride, id ) =>
			{
				new ParkRideOperation( state, guests ).Forget( script, ride.ThingId, id );
				ParkRideOperation.LeaveQueue( state, script, ride.ThingId, id );
			} );

		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.AreEqual( 30, state.Objects.Single( thing => thing.ThingId == DrinksShop ).PricePerUse,
			"the Drinks Shop's shipped price" );

		behaviour.Step( peep, walk, playing: null, tick: 40 );

		return (state, peep, behind, script);
	}
}
