using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where a guest is put down when a ride lets them off, and the load flag that decides whether it will
/// take them in the first place - <c>FUN_004e1410</c> and the <c>FUN_005014e0</c> it calls.
///
/// <para>
/// <b>One object in the whole park can tell a right answer from a wrong one here, and it is the ride.</b>
/// <c>mExitPos</c> holds the same value as <c>mEntryPos</c> on ten of the eleven placed objects, so a
/// build that walked a dismissed guest to the ENTRY would be indistinguishable from a correct one almost
/// everywhere. Thing 13, the Belly Bounce, is the exception: it is entered from (52,23) and left from
/// (52,26), three cells apart on opposite sides. Every assertion below that matters is about that gap.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideExitTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Ride = 13;

	/// <summary>Measured, not chosen: <c>mExitPos</c> 3381 unpacks here.</summary>
	private const int ExitX = 52;

	private const int ExitY = 26;

	/// <summary>And <c>mEntryPos</c> 2997 unpacks here, which is the cell they queued at.</summary>
	private const int EntryX = 52;

	private const int EntryY = 23;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private static Peep Guest( int thingId, PeepState state )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: Ride, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>
	/// <b>The reading itself, and the only object in the park that can vouch for it.</b> The ride's exit is
	/// a different cell from its entry, and the decode without the one lands where nobody could walk.
	/// </summary>
	[TestMethod]
	public void TheRideIsLeftByADifferentCellFromTheOneItIsEnteredBy()
	{
		var world = Park();
		var ride = world.Objects.Single( o => o.ThingId == Ride );

		Assert.AreEqual( 3381, ride.ExitPos, "mExitPos packed, as it sits in the record" );
		Assert.AreEqual( 2997, ride.EntryPos, "and mEntryPos beside it" );

		Assert.AreEqual( ExitX, ride.ExitCellX, "the exit unpacks to (52,26)" );
		Assert.AreEqual( ExitY, ride.ExitCellY, "the exit unpacks to (52,26)" );

		Assert.AreNotEqual( (ride.EntryCellX, ride.EntryCellY), (ride.ExitCellX, ride.ExitCellY),
			"entry and exit are different cells, which is the whole reason this field is read" );

		// The same discriminator that settled mEntryPos: a cell with no connected edges is one no route
		// can ever reach, so the reading without the one is not a matter of taste.
		Assert.AreNotEqual( 0, world.CellAt( ride.ExitCellX, ride.ExitCellY ).Neighbours,
			"the cell the packed reading names has connected edges" );
		Assert.AreEqual( 0, world.CellAt( ride.ExitPos % ParkWorld.MapSize, ride.ExitPos / ParkWorld.MapSize ).Neighbours,
			"where the unpacked-by-nothing reading lands has none, which is what rules it out" );
	}

	/// <summary>
	/// <b>And the rest of the park cannot vouch for it</b>, which is why the test above leans on one object.
	/// Stating it stops a later reader mistaking a park-wide sweep for evidence.
	/// </summary>
	[TestMethod]
	public void EveryOtherPlacedObjectLeavesByTheCellItIsEnteredBy()
	{
		var placed = Park().Objects.Where( o => o.IsPlaced ).ToArray();

		Assert.AreEqual( 11, placed.Length, "the shipped park's placed objects" );

		var differ = placed.Where( o => o.ExitPos != o.EntryPos ).ToArray();

		Assert.AreEqual( 1, differ.Length, "exactly one object's exit differs from its entry" );
		Assert.AreEqual( Ride, differ[0].ThingId, "and it is the ride" );
	}

	/// <summary>A guest let off is walked to the exit - not to the entry, which is the mistake to catch.</summary>
	[TestMethod]
	public void ADismissedGuestIsWalkedToTheExit()
	{
		var world = Park();
		var ride = world.Objects.Single( o => o.ThingId == Ride );
		var park = new ParkState( world );
		var script = Script();

		var peep = Guest( 7, PeepState.Riding );
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		script.Set( ParkRideOperation.DismissVariable, 7 );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [7] = peep } )
			.Dismiss( script, ride, tick: 9, new Random( 1 ), _ => walk ), "let off" );

		var (targetX, targetY) = peep.Navigator.Target.Cell;

		Assert.AreEqual( ExitX, targetX, "they are put down at the ride's exit" );
		Assert.AreEqual( ExitY, targetY, "and the same down the map" );

		Assert.AreNotEqual( (EntryX, EntryY), (targetX, targetY),
			"walking them back to the entry would look right on every other object in the park" );

		Assert.AreEqual( PeepState.OnRide, peep.State, "and they are leaving the ride" );
	}

	/// <summary>
	/// Without somewhere to walk them the state still changes, which is what every caller did before a
	/// ride's turn existed to supply a route.
	/// </summary>
	[TestMethod]
	public void WithNoWalkTheyStillComeOffTheRide()
	{
		var script = Script();
		var peep = Guest( 7, PeepState.Riding );

		script.Set( ParkRideOperation.DismissVariable, 7 );

		Assert.IsTrue( new ParkRideOperation(
			new ParkState( parkIsClosed: false, visitorsToDate: 0 ),
			new Dictionary<int, Peep> { [7] = peep } )
			.Dismiss( script, Park().Objects.Single( o => o.ThingId == Ride ), 9, new Random( 1 ) ),
			"let off with nowhere to go" );

		Assert.AreEqual( PeepState.OnRide, peep.State );
	}

	/// <summary>
	/// <c>mCanLoad</c> stops an admission, exactly as it already stops the choice that leads to one.
	/// </summary>
	[TestMethod]
	public void ARideThatCannotLoadAdmitsNobody()
	{
		var ride = Park().Objects.Single( o => o.ThingId == Ride );

		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		park.NominateForLoading( Ride, 7 );

		var script = Script();

		Assert.IsFalse( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.AdmitPerson( script, ride with { CanLoad = 0 }, 7 ), "mCanLoad nought, so nobody is handed over" );

		Assert.AreEqual( 0, script[ParkRideOperation.AdmitVariable], "nothing was written into the slot" );

		// Anti-vacuity: the shipped ride's own value admits, so the refusal is reading the field rather
		// than refusing everything.
		Assert.AreEqual( 1, ride.CanLoad, "the Belly Bounce can load" );
		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.AdmitPerson( Script(), ride, 7 ), "and with its real value it admits" );
	}

	/// <summary>And it stops the invite that would have called somebody forward.</summary>
	[TestMethod]
	public void ARideThatCannotLoadInvitesNobody()
	{
		var ride = Park().Objects.Single( o => o.ThingId == Ride );

		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		park.JoinQueue( Ride, 7 );

		var head = Guest( 7, PeepState.InQueue );
		var guests = new Dictionary<int, Peep> { [7] = head };

		var script = Script();
		script.Set( ParkRideOperation.CapacityVariable, 5 );

		Assert.AreEqual( 0, new ParkRideOperation( park, guests )
			.Invite( script, ride with { CanLoad = 0 } ), "mCanLoad nought, so nobody is called forward" );

		Assert.IsFalse( head.BeenAdmitted, "and they carry no invitation" );
		Assert.AreEqual( 0, park.PersonBeingLoaded( Ride ), "and the ride nominates nobody" );

		// Anti-vacuity, as above: the same call with the shipped value succeeds.
		var open = Script();
		open.Set( ParkRideOperation.CapacityVariable, 5 );

		Assert.AreEqual( 7, new ParkRideOperation( park, guests ).Invite( open, ride ),
			"with its real mCanLoad the same queue head is invited" );
	}

	/// <summary>
	/// <c>mCanLoad</c> is 1 on every object in this park, so the two refusals above cannot fire in it.
	/// Pinned deliberately: a reader should know the arm is faithful rather than exercised.
	/// </summary>
	[TestMethod]
	public void NothingInThisParkIsUnableToLoad()
	{
		var objects = Park().Objects;

		Assert.AreEqual( 14, objects.Count, "the shipped park's objects, placed and not" );
		Assert.AreEqual( 0, objects.Count( o => o.CanLoad == 0 ), "none of them refuses to load" );
	}

	/// <summary>Dismisses one guest from a ride priced as the caller asks, and hands back what happened.</summary>
	private (ParkState Park, Peep Guest) PayFor( int price, int cash = 300, int balance = 500 )
	{
		var ride = Park().Objects.Single( o => o.ThingId == Ride ) with { PricePerUse = price };
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance );
		var script = Script();

		var peep = Guest( 7, PeepState.Riding );
		peep.Cash = cash;

		script.Set( ParkRideOperation.DismissVariable, 7 );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [7] = peep } )
			.Dismiss( script, ride, tick: 9, new Random( 1 ) ), "the guest should have been let off" );

		return (park, peep);
	}

	/// <summary>
	/// <b>A guest pays on leaving, and the object keeps what they paid.</b> <c>FUN_004fe1a0</c>: read the
	/// price, credit the object, take it off the guest.
	/// </summary>
	[TestMethod]
	public void APricedRideTakesTheMoneyAndKeepsIt()
	{
		var (park, peep) = PayFor( price: 20 );

		Assert.AreEqual( 280, peep.Cash, "twenty off three hundred" );
		Assert.AreEqual( 20, park.TakingsFor( Ride ), "and the ride has taken it" );
	}

	/// <summary>
	/// <b>The park's own balance does NOT move, and that is the original's arrangement.</b> An admission fee
	/// goes through <c>FUN_004d0600</c> and lands on <c>mBalance</c>; a charge for a ride goes through
	/// <c>FUN_004e16b0</c>, which credits the object and a global pool and never touches the balance.
	/// <b>Do not "fix" this</b> - making the park's money move here would be inventing behaviour.
	/// </summary>
	[TestMethod]
	public void PayingForARideLeavesTheParksBalanceAlone()
	{
		var (park, _) = PayFor( price: 20, balance: 500 );

		Assert.AreEqual( 500, park.Balance, "the balance the park started with" );
		Assert.AreEqual( 0, park.Takings, "and the gate's running total is untouched too" );
	}

	/// <summary>
	/// A ride priced at nought charges nothing - and that is <b>this park's own ride</b>, so the refusal is
	/// the shipped case rather than a contrived one.
	/// </summary>
	[TestMethod]
	public void AFreeRideTakesNothing()
	{
		var ride = Park().Objects.Single( o => o.ThingId == Ride );

		Assert.AreEqual( 0, ride.PricePerUse, "the Belly Bounce is free, which is why this is the control" );

		var (park, peep) = PayFor( price: 0 );

		Assert.AreEqual( 300, peep.Cash, "nobody paid anything" );
		Assert.AreEqual( 0, park.TakingsFor( Ride ), "and the ride took nothing" );
	}

	/// <summary>
	/// <b>A guest short of the price is left short, and is not refused.</b> The original subtracts whatever
	/// the price is with no test and no clamp: what stops it in practice is <c>FUN_004fde50</c>, which
	/// decides whether a thing is worth its price <i>before</i> a guest is sent to it. A refusal here would
	/// be one the engine never makes.
	/// </summary>
	[TestMethod]
	public void AGuestShortOfThePriceIsLeftShortRatherThanRefused()
	{
		var (park, peep) = PayFor( price: 20, cash: 5 );

		Assert.AreEqual( -15, peep.Cash, "five less twenty, unclamped, as the original leaves it" );
		Assert.AreEqual( 20, park.TakingsFor( Ride ), "and the ride is credited the full price regardless" );
	}

	/// <summary>
	/// Nobody has paid for anything in the park that ships, so per-object takings start at nought - which is
	/// a fact about the file rather than a gap, and is what the seeding has to reproduce.
	/// </summary>
	[TestMethod]
	public void NothingInThisParkHasEverTakenAnything()
	{
		var world = Park();
		var state = new ParkState( world );

		Assert.AreEqual( 0, world.Objects.Count( o => o.TotalTakings != 0 ), "mTotalTakings across the park" );

		foreach ( var thing in world.Objects )
			Assert.AreEqual( 0, state.TakingsFor( thing.ThingId ), $"thing {thing.ThingId}" );
	}
}
