using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// How a guest reaches a second toilet, and what a toilet does for them - the walk's minor decision
/// (<c>FUN_004fd570</c>), the saved major and its restore (<c>FUN_00500900</c>), the walking-turn count, and the
/// settle-up's toilet arm. <c>docs/exe/ride-operation.md</c>, "A second toilet: the minor decision and the saved
/// major".
///
/// <para>
/// Lost Kingdom's three toilets stand at the dead end of the path up column 56: 21 on (55,17), 22 on (55,16) and
/// 23 on (55,15), each entered from its own cell and queued for on the path beside it. A guest walking up the
/// path to 23 passes the other two, which is where the minor decision can turn them aside.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkSecondToiletTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Toilet21 = 21;

	private const int Toilet22 = 22;

	private const int Toilet23 = 23;

	private const int BellyBounce = 13;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private ParkItemCatalogue Catalogue() => new( "jungle", data );

	private PeepBehaviour Behaviour( ParkWorld world, ParkState state )
		=> new( world, new Random( 1 ), new ParkAdmission( Balance(), world.Economy!.Value.AdmissionFee ),
			() => ParkRides.GateIsOpen, state, Catalogue(), balance: Balance() );

	/// <summary>A guest standing on one cell, aimed at another, at a guest's measured walking speed.</summary>
	private static Peep Guest( int thingId, PeepState state, (int X, int Y) at, (int X, int Y) aim, int majorDest,
		float toilet = 90f, float vomit = 0f, int savedMajorDest = 0, int walkingTurns = 0, int queuePos = 0,
		IReadOnlyList<int>? visits = null )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 2, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: toilet, Vomit: vomit,
			Litter: 0f, MajorDest: majorDest, QueuePos: queuePos, PrankeryIndex: 0,
			PreviousRides: visits, SavedMajorDest: savedMajorDest, WalkingTurns: walkingTurns ),
			new ParkWorld.NavigatorState(
				X: PeepNavigator.WaypointCentre( at.X ), Y: PeepNavigator.WaypointCentre( at.Y ),
				VelocityX: 0, VelocityY: 0,
				TargetX: PeepNavigator.WaypointCentre( aim.X ), TargetY: PeepNavigator.WaypointCentre( aim.Y ),
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 31457, MaxSpeed: 15728, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

	private static PeepWalk WalkOf( ParkWorld world, Peep peep )
		=> new( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

	private static ParkWorld.CatalogueObject Thing( ParkWorld world, int id ) => world.Objects.Single( o => o.ThingId == id );

	private static (int X, int Y) BackOf( ParkWorld world, int id )
		=> MapStep.CellAt( ParkRideChoice.QueueCellsFor( world, Thing( world, id ) ).BackOfQueue );

	/// <summary>
	/// <b>The corridor as the tests below read it</b>: each toilet entered from its own cell and queued for on the
	/// path beside it, so a walk to 23 passes 21 and then 22.
	/// </summary>
	[TestMethod]
	public void TheThreeToiletsStandUpTheCorridor()
	{
		var world = World();

		foreach ( var (id, y) in new[] { (Toilet21, 17), (Toilet22, 16), (Toilet23, 15) } )
		{
			var toilet = Thing( world, id );

			Assert.IsTrue( toilet.IsToilet, $"{id} is a toilet" );
			Assert.AreEqual( (55, y), (toilet.EntryCellX, toilet.EntryCellY), $"{id} is entered from its own cell" );
			Assert.AreEqual( (56, y), BackOf( world, id ), $"and queued for on the path beside it" );
		}
	}

	/// <summary>
	/// <b>The switch test's lengths</b> - <c>FUN_004d8b40</c>, the waypoints the bare search records, the leg from the
	/// start not counted. From 23's entry: to a guest at (56,19) 4 and to 21's entry 3, so a guest there switches;
	/// to a guest at (56,17) 2 and to 22's entry 2, a tie, which does not. From 22's entry: to a guest at (56,18) 2 and
	/// to 21's entry 1, so a second switch can follow a first. The corridor's first four measured by Q170's shadow in the
	/// running game.
	/// </summary>
	[TestMethod]
	public void TheLengthsUpTheCorridorAreTheSearchesWaypoints()
	{
		var world = World();
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		Assert.AreEqual( 4, CellSearch.RouteLength( (55, 15), (56, 19), blocked ), "23's entry to (56,19)" );
		Assert.AreEqual( 3, CellSearch.RouteLength( (55, 15), (55, 17), blocked ), "23's entry to 21's" );
		Assert.AreEqual( 2, CellSearch.RouteLength( (55, 15), (56, 17), blocked ), "23's entry to (56,17)" );
		Assert.AreEqual( 2, CellSearch.RouteLength( (55, 15), (55, 16), blocked ), "23's entry to 22's" );
		Assert.AreEqual( 2, CellSearch.RouteLength( (55, 16), (56, 18), blocked ), "22's entry to (56,18)" );
		Assert.AreEqual( 1, CellSearch.RouteLength( (55, 16), (55, 17), blocked ), "22's entry to 21's" );
	}

	/// <summary>
	/// <b>A search that does not arrive measures -1, and a nearer thing that does not route is not switched to.</b>
	/// With every way into 21's entry shut, a guest at (56,19) bound for 23 keeps 23.
	/// </summary>
	[TestMethod]
	public void ANearerThingThatDoesNotRouteIsNotSwitchedTo()
	{
		var world = World();
		var edge = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		bool Shut( int x, int y, StepDirection d ) => edge( x, y, d ) || MapStep.Beyond( x, y, d ) == (55, 17);

		Assert.AreEqual( -1, CellSearch.RouteLength( (55, 15), (55, 17), Shut ), "no route to 21's entry" );

		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 19), BackOf( world, Toilet23 ), Toilet23, walkingTurns: 11 );
		var walk = new PeepWalk( guest.Navigator, Shut );

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		Assert.AreEqual( 0, guest.WalkingTurns, "the decision was made" );
		Assert.AreEqual( Toilet23, guest.MajorDest, "and kept the major" );
		Assert.AreEqual( 0, guest.SavedMajorDest );
	}

	/// <summary>
	/// <b>A guest walking to 23 turns aside for 21 on the twelfth walking turn</b>, saving 23 and aimed at the centre
	/// of 21's entry (<c>0x004fd93b</c>). The count comes from the guest's record, so the first turn of this walk is the
	/// twelfth. A saved major left over from an earlier walk, the Belly Bounce, is overwritten.
	/// </summary>
	[TestMethod]
	public void OnTheTwelfthWalkingTurnAGuestTurnsAsideForANearerToilet()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 19), BackOf( world, Toilet23 ), Toilet23,
			savedMajorDest: BellyBounce, walkingTurns: 11 );
		var walk = WalkOf( world, guest );

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		Assert.AreEqual( (56, 19), walk.Position.Cell, "still on the cell the decision was made from" );
		Assert.AreEqual( Toilet21, guest.MajorDest, "turned aside for the nearer toilet" );
		Assert.AreEqual( Toilet23, guest.SavedMajorDest, "with the one they were bound for put by, over the stale one" );
		Assert.AreEqual( 0, guest.WalkingTurns, "the count zeroed" );
		Assert.AreEqual( PeepState.GoingToRide, guest.State, "still walking" );
		Assert.AreEqual( (55, 17), guest.Navigator.Target.Cell, "aimed at 21's entry, not its back of queue" );
		Assert.IsFalse( guest.Navigator.CannotReach, "which routes" );
	}

	/// <summary>
	/// <b>On the eleventh nothing is decided</b>, and the turn is counted: a guest one short of the twelfth walks on
	/// bound for 23.
	/// </summary>
	[TestMethod]
	public void EveryWalkingTurnIsCountedAndOnlyTheTwelfthDecides()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 19), BackOf( world, Toilet23 ), Toilet23, walkingTurns: 9 );
		var walk = WalkOf( world, guest );

		behaviour.Step( guest, walk, playing: null, tick: 2 );
		behaviour.Step( guest, walk, playing: null, tick: 3 );

		Assert.AreEqual( 11, guest.WalkingTurns, "two walking turns counted" );
		Assert.AreEqual( Toilet23, guest.MajorDest, "and nothing decided" );
		Assert.AreEqual( 0, guest.SavedMajorDest );

		// The byte wraps as the original's does (INC CL): 255 then nought, which is no twelfth.
		guest.WalkingTurns = 255;
		behaviour.Step( guest, walk, playing: null, tick: 4 );

		Assert.AreEqual( 0, guest.WalkingTurns, "255 wraps to nought" );
		Assert.AreEqual( Toilet23, guest.MajorDest, "without a decision" );
	}

	/// <summary>
	/// <b>The park shut under a walk</b> - <c>FUN_004ffbc0</c>'s shut arm, which comes before the count and returns:
	/// the turn is not counted, 25 comes off, the thing is let go of and the guest thinks again.
	/// </summary>
	[TestMethod]
	public void TheParkShuttingUnderAWalkCostsTwentyFiveAndTheThing()
	{
		var world = World();
		var state = new ParkState( world );
		var behaviour = Behaviour( world, state );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 19), BackOf( world, Toilet23 ), Toilet23,
			savedMajorDest: BellyBounce, walkingTurns: 11 );
		var walk = WalkOf( world, guest );

		state.SetParkClosed( true );

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		Assert.AreEqual( 11, guest.WalkingTurns, "not counted, so no minor decision" );
		Assert.AreEqual( 25f, guest.Happiness, "BigHappinessChange off 50" );
		Assert.AreEqual( 0, guest.MajorDest, "the thing let go of" );
		Assert.AreEqual( BellyBounce, guest.SavedMajorDest, "the saved major kept" );
		Assert.AreEqual( PeepState.Deciding, guest.State, "thinking again" );
	}

	/// <summary>
	/// <b>With the park open the same turn costs nothing</b>: the control for the test above.
	/// </summary>
	[TestMethod]
	public void AWalkingTurnInAnOpenParkCostsNothing()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 19), BackOf( world, Toilet23 ), Toilet23, walkingTurns: 3 );
		var walk = WalkOf( world, guest );

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		Assert.AreEqual( 50f, guest.Happiness );
		Assert.AreEqual( Toilet23, guest.MajorDest );
		Assert.AreEqual( PeepState.GoingToRide, guest.State );
	}

	/// <summary>
	/// <b>Stuck on the way</b> - <c>FUN_004ffbc0</c>'s arm for a walk that answers 2: event 3 (counted), 25 off, the
	/// thing let go of, the saved major kept, and the guest thinks again. The guest is aimed at a cell of bare ground
	/// no path reaches.
	/// </summary>
	[TestMethod]
	public void StuckOnTheWayCostsTwentyFiveAndTheThing()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 19), (5, 120), Toilet23, savedMajorDest: BellyBounce,
			walkingTurns: 4 );
		var walk = WalkOf( world, guest );
		var before = Times( "GOING_TO_RIDE_STUCK_EVENT" );

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		Assert.AreEqual( before + 1, Times( "GOING_TO_RIDE_STUCK_EVENT" ), "event 3 counted" );
		Assert.AreEqual( 25f, guest.Happiness, "BigHappinessChange off 50" );
		Assert.AreEqual( 0, guest.MajorDest, "the thing let go of" );
		Assert.AreEqual( BellyBounce, guest.SavedMajorDest, "the saved major kept" );
		Assert.AreEqual( 4, guest.WalkingTurns, "no walking turn counted" );
		Assert.AreEqual( PeepState.Deciding, guest.State, "thinking again" );
	}

	/// <summary>
	/// <b>A tie in length does not switch</b> - the <c>JLE</c> at <c>0x004fd888</c>. From (56,17), 22 is the only thing
	/// in the window nearer 23's back of queue, and both lengths are 2.
	/// </summary>
	[TestMethod]
	public void ATieInLengthKeepsTheMajor()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 17), BackOf( world, Toilet23 ), Toilet23, walkingTurns: 11 );
		var walk = WalkOf( world, guest );

		behaviour.Step( guest, walk, playing: null, tick: 3 );

		Assert.AreEqual( (56, 17), walk.Position.Cell );
		Assert.AreEqual( Toilet23, guest.MajorDest, "22 is no shorter from 23's entry than the guest is" );
		Assert.AreEqual( 0, guest.SavedMajorDest );
	}

	/// <summary>
	/// <b>A second switch puts by the first nearer thing, and the one before it is lost</b> (<c>0x004fd934</c>): a guest
	/// turned from 23 to 22, still on (56,18) at the next decision, turns again for 21, and 22 is what is put by.
	/// </summary>
	[TestMethod]
	public void ASecondSwitchPutsByTheFirstNearerThing()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (56, 18), BackOf( world, Toilet22 ), Toilet22,
			savedMajorDest: Toilet23, walkingTurns: 11 );
		var walk = WalkOf( world, guest );

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		Assert.AreEqual( (56, 18), walk.Position.Cell );
		Assert.AreEqual( Toilet21, guest.MajorDest, "turned again, for 21" );
		Assert.AreEqual( Toilet22, guest.SavedMajorDest, "22 put by, and 23 lost" );
	}

	/// <summary>
	/// <b>The pick has no floor, and a first candidate at nought wins on an odd tick</b> (<c>0x004fd750</c>). Just off a toilet every toilet scores nought for the same kind; from (56,19) bound for 23
	/// the window holds 21 alone (and the fountain, which is not offered), taken on an odd tick and not on an even one.
	/// </summary>
	[TestMethod]
	public void RightAfterAToiletOneIsStillTakenAtNoughtOnAnOddTick()
	{
		var world = World();
		var state = new ParkState( world );
		var chooser = new ParkRideChooser( world, Catalogue(), new ParkRideScore( Balance() ), state );
		var wants = new ParkRideScore.Wants( 2, 10f, 10f, 90f, 0f, Visits: [Toilet22, 0, 0, 0] );
		var back = BackOf( world, Toilet23 );

		var odd = chooser.MinorDecisionFor( wants, 56, 19, Toilet23, back, gameTick: 3, now: state.CalendarNow );
		var even = chooser.MinorDecisionFor( wants, 56, 19, Toilet23, back, gameTick: 2, now: state.CalendarNow );

		Assert.IsNotNull( odd, "taken on an odd tick" );
		Assert.AreEqual( Toilet21, odd.Value.Thing.ThingId );
		Assert.AreEqual( 0, odd.Value.Score, "at nought: the same kind as the last visit" );
		Assert.IsNull( even, "and not on an even one, which a nought never beats" );

		var needing = chooser.MinorDecisionFor( wants with { Visits = null }, 56, 19, Toilet23, back, gameTick: 2,
			now: state.CalendarNow );

		Assert.IsNotNull( needing, "a guest in need who has not just been scores it above nought" );
		Assert.IsTrue( needing.Value.Score > 0 );
	}

	/// <summary>
	/// <b>The window is two cells back and one on, and a cell must be strictly nearer the major's back of queue</b>
	/// (<c>JGE</c> at <c>0x004fd6ce</c>). Bound for 23 (back (56,15)): 21 on
	/// (55,17) is two cells west of (57,19) and in, three west of (58,19) and out; one cell east of (54,19) and in,
	/// two east of (53,19) and out. From (57,17) 21 is exactly as near 23's back as the guest (5 and 5), so only 22 is
	/// weighed. Bound for 21 (back (56,17)) from the corridor's end: 22 on (55,16) is one row on from (56,15) and in,
	/// and 23 on (55,15) two rows on from (56,13) and out.
	/// </summary>
	[TestMethod]
	public void TheWindowIsTwoBackAndOneOnAndStrictlyNearer()
	{
		var world = World();
		var state = new ParkState( world );
		var chooser = new ParkRideChooser( world, Catalogue(), new ParkRideScore( Balance() ), state );
		var wants = new ParkRideScore.Wants( 2, 10f, 10f, 90f, 0f );
		var back = BackOf( world, Toilet23 );

		int? Pick( int x, int y ) => chooser.MinorDecisionFor( wants, x, y, Toilet23, back, gameTick: 2,
			now: state.CalendarNow )?.Thing.ThingId;

		Assert.AreEqual( Toilet21, Pick( 57, 19 ), "x less two is in" );
		Assert.IsNull( Pick( 58, 19 ), "x less three is out" );
		Assert.AreEqual( Toilet21, Pick( 54, 19 ), "x plus one is in" );
		Assert.IsNull( Pick( 53, 19 ), "x plus two is out" );
		Assert.AreEqual( Toilet22, Pick( 57, 17 ), "a cell only as near as the guest is not weighed" );

		var back21 = BackOf( world, Toilet21 );
		int? PickFor21( int x, int y ) => chooser.MinorDecisionFor( wants, x, y, Toilet21, back21, gameTick: 2,
			now: state.CalendarNow )?.Thing.ThingId;

		Assert.AreEqual( Toilet22, PickFor21( 56, 15 ), "y plus one is in" );
		Assert.IsNull( PickFor21( 56, 13 ), "y plus two is out" );
	}

	/// <summary>
	/// <b>Walking off the nearer toilet, the guest is sent on to the one put by, unscored</b> - <c>FUN_00500900</c>'s
	/// arrival: the saved major becomes the chosen thing and is let go of, and the guest walks to its back of queue.
	/// </summary>
	[TestMethod]
	public void OffTheNearerToiletTheyAreSentOnToTheOnePutBy()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );

		// Standing where 21's walk off ends: the path beside it.
		var guest = Guest( 30, PeepState.LeavingRide, (56, 17), (56, 17), Toilet21, toilet: 0f, savedMajorDest: Toilet23,
			walkingTurns: 5, visits: [Toilet21, 0, 0, 0] );
		var walk = WalkOf( world, guest );

		for ( var tick = 2; tick <= 40 && guest.State == PeepState.LeavingRide; ++tick )
			behaviour.Step( guest, walk, playing: null, tick );

		Assert.AreEqual( PeepState.GoingToRide, guest.State, "walking again" );
		Assert.AreEqual( Toilet23, guest.MajorDest, "to the toilet they were bound for" );
		Assert.AreEqual( 0, guest.SavedMajorDest, "which is let go of" );
		Assert.AreEqual( BackOf( world, Toilet23 ), guest.Navigator.Target.Cell, "aimed at its back of queue" );
		Assert.AreEqual( 5, guest.WalkingTurns, "the count carried into the new walk" );
	}

	/// <summary>
	/// <b>Nor does choosing a thing reset the count</b>: only the walking turns, the constructor and the save write it.
	/// </summary>
	[TestMethod]
	public void ChoosingAThingKeepsTheCount()
	{
		var world = World();
		var state = new ParkState( world );
		var behaviour = new PeepBehaviour( world, new Random( 1 ), new ParkAdmission( Balance(), world.Economy!.Value.AdmissionFee ),
			() => ParkRides.GateIsOpen, state, Catalogue(), balance: Balance() );
		var guest = Guest( 30, PeepState.Deciding, (56, 20), (56, 20), 0, walkingTurns: 7 );
		var walk = WalkOf( world, guest );

		for ( var tick = PeepBehaviour.ThinkingGap + 1; tick <= 2000 && guest.State != PeepState.GoingToRide; ++tick )
		{
			if ( guest.State != PeepState.Deciding )
				guest.SetState( PeepState.Deciding, tick, new Random( 1 ) );

			behaviour.Step( guest, walk, playing: null, tick );
		}

		Assert.AreEqual( PeepState.GoingToRide, guest.State, "a thing chosen" );
		Assert.AreNotEqual( 0, guest.MajorDest );
		Assert.AreEqual( 7, guest.WalkingTurns, "the count as it was" );
	}

	/// <summary>
	/// <b>With nothing put by, arriving sends them back to deciding</b>, the thing just left still named.
	/// </summary>
	[TestMethod]
	public void OffAThingWithNothingPutByTheyThinkAgain()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.LeavingRide, (56, 17), (56, 17), Toilet21, toilet: 0f );
		var walk = WalkOf( world, guest );

		for ( var tick = 2; tick <= 40 && guest.State == PeepState.LeavingRide; ++tick )
			behaviour.Step( guest, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Deciding, guest.State );
		Assert.AreEqual( Toilet21, guest.MajorDest, "the arrival arm keeps it" );
	}

	/// <summary>
	/// <b>A saved major naming nothing in the park is let go of</b> - "Deleted major dest while I was doing minor
	/// dest!" - and the guest thinks again with nothing named.
	/// </summary>
	[TestMethod]
	public void ASavedMajorNoLongerInTheParkIsLetGoOf()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.LeavingRide, (56, 17), (56, 17), Toilet21, toilet: 0f, savedMajorDest: 999 );
		var walk = WalkOf( world, guest );

		for ( var tick = 2; tick <= 40 && guest.State == PeepState.LeavingRide; ++tick )
			behaviour.Step( guest, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Deciding, guest.State );
		Assert.AreEqual( 0, guest.MajorDest );
		Assert.AreEqual( 0, guest.SavedMajorDest );
	}

	/// <summary>
	/// <b>Giving up on a walk leaves the saved major</b>: both give-up arms clear <c>+0x1dc</c> alone, which is how a
	/// stale saved major is restored after the next walk off anything.
	/// </summary>
	[TestMethod]
	public void GivingUpLeavesTheSavedMajor()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );

		foreach ( var state in new[] { PeepState.GoingToRide, PeepState.LeavingRide } )
		{
			var guest = Guest( 30, state, (56, 17), (56, 17), Toilet21, savedMajorDest: Toilet23 );
			var walk = WalkOf( world, guest );

			// Aimed where no route goes: the bare ground west of the toilets.
			guest.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 54 ), PeepNavigator.WaypointCentre( 17 ) );
			Assert.IsFalse( walk.PlanRoute(), "no way there" );

			behaviour.Step( guest, walk, playing: null, tick: 2 );

			Assert.AreEqual( PeepState.Deciding, guest.State, $"{state} gave up" );
			Assert.AreEqual( 0, guest.MajorDest, $"{state} let go of the thing" );
			Assert.AreEqual( Toilet23, guest.SavedMajorDest, $"{state} kept the saved major" );
		}
	}

	/// <summary>
	/// <b>A removed thing is let go of as a saved major by every guest</b>, whatever they are bound for
	/// (<c>0x004fb4a6</c>, reached past the <c>MajorDest</c> test).
	/// </summary>
	[TestMethod]
	public void ARemovedThingIsLetGoOfAsASavedMajorByEveryGuest()
	{
		var world = World();
		var behaviour = Behaviour( world, new ParkState( world ) );
		var guest = Guest( 30, PeepState.GoingToRide, (52, 29), (52, 29), BellyBounce, savedMajorDest: Toilet23 );

		Assert.AreEqual( PeepBehaviour.PutOff.No, behaviour.ThingRemoved( guest, Toilet22, tick: 2 ) );
		Assert.AreEqual( Toilet23, guest.SavedMajorDest, "another thing's removal leaves it" );

		Assert.AreEqual( PeepBehaviour.PutOff.No, behaviour.ThingRemoved( guest, Toilet23, tick: 3 ) );
		Assert.AreEqual( 0, guest.SavedMajorDest, "its own removal clears it" );
		Assert.AreEqual( BellyBounce, guest.MajorDest, "and nothing else about the guest" );
		Assert.AreEqual( PeepState.GoingToRide, guest.State );
	}

	/// <summary>
	/// <b>A toilet empties the need, and illness above 90, and hurries the guest off</b> - the settle-up's toilet arm,
	/// reached through a script letting them off.
	/// </summary>
	[TestMethod]
	public void AToiletEmptiesTheNeedAndIllnessAboveNinety()
	{
		var world = World();
		var state = new ParkState( world );
		var toilet = Thing( world, Toilet23 );

		foreach ( var (vomit, left) in new[] { (95f, 0f), (91f, 0f), (90.9f, 90.9f), (40f, 40f) } )
		{
			var guest = Guest( 30, PeepState.Riding, (55, 15), (55, 15), Toilet23, toilet: 90f, vomit: vomit, queuePos: 1 );
			var walk = WalkOf( world, guest );
			var script = Script();

			script.Set( ParkRideOperation.DismissVariable, guest.ThingId );

			Assert.IsTrue( new ParkRideOperation( state, new Dictionary<int, Peep> { [guest.ThingId] = guest } )
				.Dismiss( script, toilet, tick: 2, new Random( 1 ), _ => walk, world, Catalogue() ), "let off" );

			Assert.AreEqual( 0f, guest.Toilet, $"the need emptied (illness {vomit})" );
			Assert.AreEqual( left, guest.Vomit, $"illness {vomit} leaves {left}" );
			Assert.AreEqual( Peep.HurryingSpeed, guest.PurposeSpeed, "hurrying off" );
		}
	}

	/// <summary>
	/// <b>Walking off a toilet goes to the path beside it</b>: the exit's facing turned round, stepped the way
	/// <c>FUN_004d97e0</c> steps (0x04 east), and that way routes.
	/// </summary>
	[TestMethod]
	public void AGuestLetOffAToiletIsAimedAtThePathBesideIt()
	{
		var world = World();
		var toilet = Thing( world, Toilet23 );
		var guest = Guest( 30, PeepState.Riding, (55, 15), (55, 15), Toilet23, queuePos: 1 );
		var walk = WalkOf( world, guest );
		var script = Script();

		script.Set( ParkRideOperation.DismissVariable, guest.ThingId );
		new ParkRideOperation( new ParkState( world ), new Dictionary<int, Peep> { [guest.ThingId] = guest } )
			.Dismiss( script, toilet, tick: 2, new Random( 1 ), _ => walk, world );

		Assert.AreEqual( (55, 15), walk.Position.Cell, "put down on the toilet's exit" );
		Assert.AreEqual( (56, 15), guest.Navigator.Target.Cell, "aimed at the path beside it" );
		Assert.IsFalse( guest.Navigator.CannotReach, "which routes" );
	}

	/// <summary>
	/// <b>The two fields come from the guest's record</b>: <c>mSavedMajorDest</c>, a word at 499, and <c>mCount</c>, the
	/// person base's byte at 36. Every guest in Lost Kingdom's save holds nought in both, which a reader at the wrong
	/// offset could read too, so guest 42's record is given values first.
	/// </summary>
	[TestMethod]
	public void AGuestStartsWithTheSavedMajorAndCountTheirRecordCarries()
	{
		var world = World();

		foreach ( var person in world.People.Where( person => person.Guest is { } ) )
		{
			Assert.AreEqual( 0, person.Guest!.Value.SavedMajorDest, $"{person.ThingId}'s saved major" );
			Assert.AreEqual( 0, person.Guest!.Value.WalkingTurns, $"{person.ThingId}'s count" );
		}

		// Guest 42's record, found by its own cash at 414, mExitLevel at 418 and kind at 468.
		var guest42 = world.People.Single( person => person.ThingId == 42 ).Guest!.Value;
		var bytes = new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile();
		var record = Enumerable.Range( 0, bytes.Length - 533 ).Single( at =>
			BitConverter.ToInt32( bytes, at + 414 ) == guest42.Cash && BitConverter.ToInt32( bytes, at + 418 ) == guest42.ExitLevel
			&& bytes[at + 468] == guest42.PersonType );

		BitConverter.GetBytes( (ushort)Toilet23 ).CopyTo( bytes, record + 499 );
		bytes[record + 36] = 7;

		var patched = new ParkWorld( bytes ).People.Single( person => person.ThingId == 42 );
		var peep = new Peep( 42, patched.Guest!.Value, patched.Navigator );

		Assert.AreEqual( Toilet23, patched.Guest!.Value.SavedMajorDest, "mSavedMajorDest at 499" );
		Assert.AreEqual( 7, patched.Guest!.Value.WalkingTurns, "mCount at 36" );
		Assert.AreEqual( Toilet23, peep.SavedMajorDest, "handed to the guest" );
		Assert.AreEqual( 7, peep.WalkingTurns );
		Assert.AreEqual( guest42.MajorDest, patched.Guest!.Value.MajorDest, "and nothing beside them moved" );
		Assert.AreEqual( guest42.SavedState, patched.Guest!.Value.SavedState );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}
}
