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

		// <b>The POSITION, not the target</b>: being AIMED at the exit is what a guest stranded anywhere
		// also looks like. FUN_005014e0 PUTS a guest down on the exit (FUN_004fa930)
		// and then aims them one cell past it, so the exit is where they ARE.
		var (atX, atY) = peep.Navigator.Position.Cell;

		Assert.AreEqual( ExitX, atX, "they are put down on the ride's exit" );
		Assert.AreEqual( ExitY, atY, "and the same down the map" );

		Assert.AreNotEqual( (EntryX, EntryY), (atX, atY),
			"putting them back at the entry would look right on every other object in the park" );

		Assert.AreEqual( PeepState.LeavingRide, peep.State, "and they are leaving the ride" );
	}

	/// <summary>
	/// <b>A guest let off a ride ends up STANDING at its exit - their position moves, not just their
	/// destination.</b>
	///
	/// <para>
	/// Being <i>aimed</i> somewhere is exactly what a guest stranded in an unanswered state looks like, so
	/// this one starts them on the ride, has the ride put them off, and
	/// requires them to be standing on a different cell at the end of it.
	/// </para>
	/// <para>
	/// The Belly Bounce is the only object in the park whose exit differs from its entry - (52,23) in,
	/// (52,26) out, three cells apart on opposite sides - so it is also the only place where walking a
	/// guest to the wrong one of the two would show at all.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestLetOffARideEndsUpStandingAtItsExit()
	{
		var world = Park();
		var ride = world.Objects.Single( o => o.ThingId == Ride );
		var park = new ParkState( world );

		// On the ride, which is where a guest is when it lets them off: measured in a running park, a
		// riding guest stands at (52.520, 23.443).
		var peep = GuestAt( 7, PeepState.Riding, EntryX, EntryY );
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.AreEqual( (EntryX, EntryY), walk.Position.Cell, "they start on the ride" );

		var script = Script();
		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		// The world is handed over so the real arm runs: without it there is no cell facing to read and
		// the guest is put down and left, which is the fallback rather than the behaviour under test.
		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [7] = peep } )
			.Dismiss( script, ride, tick: 1, new Random( 1 ), _ => walk, world ), "the ride lets them off" );

		// <b>The moment that matters is the dismissal itself.</b> They are PUT on the exit there and then,
		// and only afterwards walk off it - so asserting after the walk would be asserting about the cell
		// beyond the exit instead.
		Assert.AreEqual( (ExitX, ExitY), walk.Position.Cell,
			$"the ride should have put them down on its exit, not left them at {walk.Position.Cell}" );

		Assert.AreNotEqual( (EntryX, EntryY), walk.Position.Cell,
			"and they should have MOVED there - being aimed somewhere is what a stranded guest looks like" );

		Assert.AreEqual( PeepState.LeavingRide, peep.State, "which puts them on the way out" );

		var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen,
			park, new ParkItemCatalogue( "jungle", data ) );

		for ( var tick = 2; tick <= 400 && peep.State == PeepState.LeavingRide; ++tick )
			behaviour.Step( peep, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Deciding, peep.State, "and then they think again" );

		Assert.AreNotEqual( (EntryX, EntryY), walk.Position.Cell,
			"and they are anywhere but back on the ride" );
	}

	/// <summary>A guest standing on a named cell, rather than at (0,0) where no route can begin.</summary>
	private static ParkWorld.NavigatorState StandingOn( int cellX, int cellY ) => new(
		X: PeepNavigator.WaypointCentre( cellX ), Y: PeepNavigator.WaypointCentre( cellY ),
		VelocityX: 0, VelocityY: 0,
		TargetX: PeepNavigator.WaypointCentre( cellX ), TargetY: PeepNavigator.WaypointCentre( cellY ),
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <inheritdoc cref="StandingOn"/>
	private static Peep GuestAt( int thingId, PeepState state, int cellX, int cellY )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: Ride, QueuePos: 0, PrankeryIndex: 0 ), StandingOn( cellX, cellY ) );

	/// <summary>
	/// <b>And then they walk it, arrive, and go back to deciding what to do - which is the step that closes
	/// the park's loop.</b>
	///
	/// <para>
	/// <see cref="ADismissedGuestIsWalkedToTheExit"/> ends at the state, and reaching
	/// <see cref="PeepState.LeavingRide"/> says nothing about what the state then does: a guest let off has
	/// to be walked again, which needs <c>PeepBehaviour.Step</c> to have a case for it.
	/// </para>
	/// <para>
	/// <b>The assertion that carries the test is WHICH arm ran, not that they thought again.</b>
	/// <c>FUN_00500900</c> drops a guest into <see cref="PeepState.Deciding"/> from both of its arms - on
	/// arriving, and on finding it cannot get through. Giving up zeroes <see cref="Peep.MajorDest"/>;
	/// arriving keeps it for a guest with no saved destination behind it (<c>+0x1de</c>, which nothing here
	/// writes), so a destination still naming the ride is what proves they arrived. The guest starts on the
	/// ride's exit cell, where dismissal puts them down.
	/// </para>
	/// <para>
	/// The behaviour is built with <b>no</b> <see cref="ParkAdmission"/> on purpose: deciding then returns
	/// at its first line, so a guest who has arrived stays arrived instead of immediately choosing
	/// somewhere new and walking out of the assertion.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestLetOffARideWalksToItsExitAndThinksAgain()
	{
		var world = Park();
		var park = new ParkState( world );

		// <b>Standing where a dismissed guest is actually put down</b>, because NO ROUTE EXISTS from the
		// park to a ride's EXIT cell - CellEdge only opens a ride end along the way it faces - because a
		// guest is put down at an exit and walks AWAY from it, never to it.
		var peep = GuestAt( 7, PeepState.LeavingRide, ExitX, ExitY );
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen,
			park, new ParkItemCatalogue( "jungle", data ) );

		for ( var tick = 1; tick <= 40 && peep.State == PeepState.LeavingRide; ++tick )
			behaviour.Step( peep, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Deciding, peep.State,
			"a guest let off a ride has to end up deciding what to do next, or the park has no loop" );

		// <b>And this is what says WHICH arm ran, which position cannot.</b> FUN_00500900 keeps the
		// destination on arriving and zeroes it on giving up, so a build that only ever reported "cannot
		// get through" would reach Deciding too - and would arrive here with MajorDest nought.
		Assert.AreEqual( Ride, peep.MajorDest,
			"arriving keeps the destination; only the give-up arm clears it, so this is the arrival" );
	}

	/// <summary>
	/// Without somewhere to walk them the state still changes.
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

		Assert.AreEqual( PeepState.LeavingRide, peep.State );
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
	/// The shipped save has <c>mCanLoad</c> 1 on all fourteen objects, so nothing refuses to load until
	/// something closes it. The park's door does (<see cref="ParkRideOperation.Close"/>); see
	/// <c>ParkClosedRideTests</c> for the refusals it causes.
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
	/// <b>The park's own balance does not move here, and that is a gap, not the original.</b> The original's
	/// charge deposits the price in the bank first (<c>FUN_004e16b0</c> calls <c>FUN_004d0190</c> at
	/// <c>0x004e16c6</c>), as the gate fee does through <c>FUN_004d0600</c>; <see cref="ParkState.TakeAt"/> counts
	/// the deposit rather than making it. This pins what the code does now; <c>docs/QUEUE.md</c> Q96 turns it
	/// round, and the gate's running total must still stay untouched.
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
	/// the price is with no test and no clamp: what stops it in practice is <c>FUN_004fde50</c>, asked at the
	/// door before boarding (<c>0x00500715</c>). A refusal here would be one the engine never makes.
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

	/// <summary>Thing 16, the <c>Drinks Shop</c> - the one placed object in this park that declares an
	/// effect block, which is what makes it the only one able to tell a built arm from an inert one.</summary>
	private const int DrinksShop = 16;

	/// <summary>
	/// Lets one guest off the named object with the real catalogue behind it, and hands back what became of
	/// them - <c>FUN_004fd970</c>'s settle-up runs on the way out.
	/// </summary>
	/// <remarks>
	/// <b>The meters start at eighty rather than where <see cref="Guest"/> leaves them, and that is not
	/// tidying.</b> <see cref="Peep.Change"/> clamps to <see cref="Peep.Least"/> and <see cref="Peep.Most"/>,
	/// so a guest whose thirst began at ten would read nought afterwards whether the deduction were forty or
	/// four hundred - and every assertion below would pass against arithmetic it had never looked at.
	/// </remarks>
	/// <param name="admission">
	/// The park's mood constants, or null to leave the two happiness arms of the settle-up alone.
	/// With no admission the losing arm and the sideshow's winnings both do nothing, so a test cannot tell
	/// a built one from an absent one; the tests that care pass one.
	/// </param>
	private (ParkState Park, Peep Guest) LetOffAt( int thingId, int queuePos,
		ParkAdmission? admission = null )
	{
		var thing = Park().Objects.Single( o => o.ThingId == thingId );
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();

		var peep = Guest( 7, PeepState.Riding );

		peep.QueuePos = queuePos;
		peep.Thirst = 80f;
		peep.Hunger = 80f;

		script.Set( ParkRideOperation.DismissVariable, 7 );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [7] = peep }, admission )
			.Dismiss( script, thing, tick: 9, new Random( 1 ),
				catalogue: new ParkItemCatalogue( "jungle", data ) ),
			"the guest should have been let off" );

		return (park, peep);
	}

	/// <summary>The park's own mood constants - <c>PeepInfo.MediumHappinessChange</c> is 15 in this stack.</summary>
	private static ParkAdmission Mood() => new( new ParkBalance( "jungle", easyMode: true ), 25 );

	/// <summary>Thing 14, the <c>Jungle Spray</c> - the park's only sideshow, and the only thing in it that
	/// pays a prize.</summary>
	private const int JungleSpray = 14;

	/// <summary>
	/// <b>What the two items' own files say, because every number below is derived from them</b> and the
	/// category defaults say something different. Read from inside each <c>.wad</c>, which is where an
	/// item's overrides live and where a grep of the installed folder cannot see them.
	/// </summary>
	[TestMethod]
	public void TheChanceOfWinningComesFromEachItemsOwnFileAndAShopAlwaysWins()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );

		Assert.IsTrue( catalogue.TryGet( 1203, out var coconut ), "the Drinks Shop's description" );
		Assert.IsTrue( catalogue.TryGet( 1303, out var junspray ), "the Jungle Spray's description" );

		// A shop declares no chance of LOSING at all, so its chance of winning is a hundred and its roll
		// cannot fail. That is why a drink is always served, and it is the mechanism rather than a default.
		Assert.AreEqual( 100, coconut.ChanceOfWinning, "a shop always serves" );
		Assert.AreEqual( 20, coconut.CostOfGoods, "and its own file sets the cost of goods to twenty" );

		// The sideshow declares 75, overriding its category's 70 - so it is won one time in four.
		Assert.AreEqual( 25, junspray.ChanceOfWinning, "a hundred less the seventy-five it declares" );

		// FIFTY: the item's own file overrides its
		// category's thirty, and the sign of the happiness arm turns on the prize beating the price.
		Assert.AreEqual( 50, junspray.CostOfGoods, "its prize is fifty, against the twenty it charges" );

		// Anti-vacuity: the two differ, so neither is a constant the parser fell back to.
		Assert.AreNotEqual( coconut.ChanceOfWinning, junspray.ChanceOfWinning );

		// And the roll itself, over enough draws that a hundred and a twenty-five cannot be confused.
		var random = new Random( 7 );
		var shopWins = 0;
		var sprayWins = 0;

		for ( var draw = 0; draw < 1000; ++draw )
		{
			if ( ParkRideOperation.Succeeds( coconut, random ) )
				++shopWins;

			if ( ParkRideOperation.Succeeds( junspray, random ) )
				++sprayWins;
		}

		Assert.AreEqual( 1000, shopWins, "every single one, because its chance is a hundred" );
		Assert.IsTrue( sprayWins is > 150 and < 400, $"about a quarter of a thousand, and it was {sprayWins}" );
	}

	/// <summary>
	/// <b>The losing arm.</b> A guest whose roll failed is charged, gets none of the
	/// item's effects, and loses <c>PeepInfo.MediumHappinessChange</c> - the original's "Person lost this
	/// sideshow..." path.
	/// </summary>
	[TestMethod]
	public void AGuestWhoGotNothingOutOfAVisitLosesTheMiddleMoodChange()
	{
		var (park, peep) = LetOffAt( DrinksShop, queuePos: 0, Mood() );

		Assert.AreEqual( 15, Mood().MediumHappinessChange, "the constant this arm spends, from the file" );
		Assert.AreEqual( 35f, peep.Happiness, 0.001f, "fifty less the fifteen the middle change costs" );

		// The effects still did not run, which is what separates this arm from the one below it.
		Assert.AreEqual( 80f, peep.Thirst, 0.001f, "the gate held, so the drink never reached them" );

		Assert.AreEqual( 270, peep.Cash, "and they paid regardless" );
		Assert.AreEqual( 30, park.TakingsFor( DrinksShop ), "which the shop kept" );
	}

	/// <summary>
	/// <b>A sideshow pays a prize and then moves the winner's happiness, and both halves are the original's.</b>
	/// <c>FUN_004fe1e0</c> adds the cost of goods to the guest's cash and then moves happiness by
	/// <c>log2( costOfGoods / pricePerUse ) * MediumHappinessChange</c>.
	///
	/// <para>
	/// <b>The prize is bigger than the price, so the winner gains.</b> Fifty over twenty is two and a half,
	/// its log is about 1.32, and fifteen of those is +19.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WinningAtTheSideshowPaysAPrizeWorthMoreThanThePriceAndCheersTheGuest()
	{
		var (park, peep) = LetOffAt( JungleSpray, queuePos: 1, Mood() );

		// Charged twenty, handed fifty back: three hundred less twenty plus fifty.
		Assert.AreEqual( 330, peep.Cash, "the twenty it charges, less the fifty it pays a winner" );
		Assert.AreEqual( 20, park.TakingsFor( JungleSpray ), "and the sideshow keeps the full price" );

		Assert.AreEqual( 69f, peep.Happiness, 0.001f, "fifty, and log2(50/20) * 15 truncates to +19" );

		// Anti-vacuity: the sideshow declares no effect block at all, so a build that ran the five effects
		// here would still read eighty - the nineteen must have come from the winnings arm and nowhere else.
		Assert.AreEqual( 80f, peep.Thirst, 0.001f, "a sideshow quenches nothing, and its file says so" );
	}

	/// <summary>
	/// <b>What a visit actually does to a guest</b> - the five effects <c>FUN_004fe1e0</c> applies from the
	/// item descriptor when somebody finishes with a thing.
	///
	/// <para>
	/// <b>Two deduct and three add, and the split is the data's own.</b> The balance file says so in its
	/// comment column - "How much thirst to deduct" against "How much vomit to add" - and the decompile
	/// agrees. Thirst falling while happiness and litter rise, in one guest and one call, is what separates a
	/// built arm from one that applied a single sign to all five.
	/// </para>
	/// <para>
	/// <b>Where the numbers come from, because the obvious place is wrong.</b> They are the Drinks Shop's
	/// own, resolved through the mounted filesystem. <c>levels/jungle/shops/Shops.sam</c> declares all five
	/// at <b>5</b> and is the <i>category default</i>; each shop's override lives in the <c>.sam</c> inside
	/// its own <c>.wad</c>, where a grep of the installed game folder cannot see it. Reading the folder alone
	/// yields five fives and a test that pins nothing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AVisitToAShopChangesTheFiveMetersItsOwnFileNames()
	{
		var (park, peep) = LetOffAt( DrinksShop, queuePos: 1 );

		Assert.AreEqual( 40f, peep.Thirst, 0.001f, "eighty less the forty a drink quenches" );

		Assert.AreEqual( 10f, peep.Vomit, 0.001f, "a drink adds ten to how sick they feel" );
		Assert.AreEqual( 55f, peep.Happiness, 0.001f, "fifty, and the five it cheers them" );
		Assert.AreEqual( 50f, peep.Litter, 0.001f, "and leaves them holding fifty of litter" );

		// A meter the item declares NOUGHT is left exactly alone, which is the half that says these are read
		// values rather than a bundle every visit hands out. The Drinks Shop's hunger effect is 0 - it is a
		// drink - so a build applying some fixed helping of everything would move this one too.
		Assert.AreEqual( 80f, peep.Hunger, 0.001f, "a drink does nothing for hunger, and the shop says so" );

		Assert.AreEqual( 270, peep.Cash, "and they paid the shop's thirty on the way out" );
		Assert.AreEqual( 30, park.TakingsFor( DrinksShop ), "which the shop keeps" );
	}

	/// <summary>
	/// The settle-up's gate: with <c>mQueuePos</c> at nought the five effects do not run.
	///
	/// <para>
	/// <b>This pins the branch and deliberately not a reading of it.</b> What the byte at <c>+0x1f1</c>
	/// MEANS is not settled - it is named <c>mQueuePos</c> by the save reader and it is read here. So the
	/// assertion is that the arm is gated on it, which the disassembly shows, and nothing about why.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestWhoseQueuePosIsNoughtIsChargedButOtherwiseUnchanged()
	{
		var (park, peep) = LetOffAt( DrinksShop, queuePos: 0 );

		Assert.AreEqual( 80f, peep.Thirst, 0.001f, "the gate held, so the drink never reached them" );
		Assert.AreEqual( 0f, peep.Vomit, 0.001f, "nor made them ill" );
		Assert.AreEqual( 50f, peep.Happiness, 0.001f, "nor cheered them" );
		Assert.AreEqual( 0f, peep.Litter, 0.001f, "nor left them anything to drop" );

		// And THIS is what says the gate sits inside the settle-up rather than in front of it: the money
		// moves anyway. A build that returned before charging would read three hundred here.
		Assert.AreEqual( 270, peep.Cash, "they still paid" );
		Assert.AreEqual( 30, park.TakingsFor( DrinksShop ), "and the shop still kept it" );
	}

	/// <summary>
	/// <b>The anti-vacuity half.</b> The same guest, the same queue byte and the same catalogue - only the
	/// object differs - and nothing moves at all, because the Belly Bounce declares no effect block.
	/// <c>Rides.sam</c> and <c>SideShow.sam</c> declare none of the five keys, so a ride reading nought is an
	/// inherited fallback rather than a coincidence, and that is what makes the arm safe to run on anything a
	/// guest leaves.
	/// </summary>
	[TestMethod]
	public void LeavingARideThatDeclaresNoEffectsLeavesEveryMeterAlone()
	{
		var (park, peep) = LetOffAt( Ride, queuePos: 1 );

		Assert.AreEqual( 80f, peep.Thirst, 0.001f, "a ride is not a drink" );
		Assert.AreEqual( 80f, peep.Hunger, 0.001f, "nor a meal" );
		Assert.AreEqual( 0f, peep.Vomit, 0.001f, "and this one declares no effect on how sick they feel" );
		Assert.AreEqual( 50f, peep.Happiness, 0.001f, "nor on their mood, here" );
		Assert.AreEqual( 0f, peep.Litter, 0.001f, "nor hands them anything to drop" );

		Assert.AreEqual( 300, peep.Cash, "the Belly Bounce is free, so nobody paid" );
		Assert.AreEqual( 0, park.TakingsFor( Ride ), "and it took nothing" );
	}
}
