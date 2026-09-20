using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest does when they get where they were going - the state machine that had been written and
/// never called.
///
/// <para>
/// The transitions are driven against the <b>real park</b> rather than a contrived walk, because the whole
/// point of this work is that Lost Kingdom's thirteen guests are in three states and every one of those
/// three delegates to a handler. A test built on the states that are answered inline would pass while the
/// park stood still, which is the mistake that once made the walk itself inert.
/// </para>
/// </summary>
[TestClass]
public class PeepBehaviourTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>A guest built from nothing, for the tests that are about a rule rather than about a park.</summary>
	private static Peep Guest( int thingId, int state )
		=> new( thingId, new ParkWorld.GuestState(
			State: state, SavedState: 6, PersonType: 0, Cash: 300, ExitLevel: 100,
			Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>
	/// Steps every guest of the shipped park until nobody changes state any more, and hands back what they
	/// each ended up doing. A hundred and fifty turns is comfortably past the longest route in the park,
	/// which arrives at turn 108.
	/// </summary>
	private (PeepBehaviour Behaviour, System.Collections.Generic.Dictionary<int, Peep> Guests) RunPark(
		bool parkIsClosed = false, int turns = 150 )
	{
		var world = World();
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var behaviour = new PeepBehaviour( parkIsClosed, world.NumberOfVisitorsToDate, new Random( 1234 ) );

		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var walks = guests.Values.ToDictionary( peep => peep.ThingId,
			peep => new PeepWalk( peep.Navigator, blocked ) );

		for ( var tick = 1; tick <= turns; ++tick )
		{
			foreach ( var peep in guests.Values )
				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );
		}

		return (behaviour, guests);
	}

	/// <summary>
	/// A quarter of the park hurries to the gate, chosen by thing id and fixed for the life of the guest.
	///
	/// <para>
	/// <b>And none of Lost Kingdom's five does</b>, which is why this rule needs guests built for it. Things
	/// 42, 39, 35, 31 and 29 leave remainders of 2, 3, 3, 3 and 1, so the shipped park exercises only one
	/// arm of the test - a test written against the park alone would have pinned "nobody ever hurries".
	/// </para>
	/// </summary>
	[TestMethod]
	public void AQuarterOfGuestsHurryToTheGateAndNoneOfLostKingdomsDo()
	{
		foreach ( var id in new[] { 0, 4, 8, 32, 36, 40 } )
			Assert.IsTrue( PeepBehaviour.HurriesToTheGate( Guest( id, 2 ) ), $"thing {id} should hurry" );

		foreach ( var id in new[] { 1, 2, 3, 29, 31, 35, 39, 42 } )
			Assert.IsFalse( PeepBehaviour.HurriesToTheGate( Guest( id, 2 ) ), $"thing {id} should not" );

		var heading = new[] { 42, 39, 35, 31, 29 };

		Assert.IsFalse( heading.Any( id => PeepBehaviour.HurriesToTheGate( Guest( id, 2 ) ) ),
			"none of the five guests heading for Lost Kingdom's gate hurries there" );
	}

	/// <summary>
	/// The five guests heading for the gate arrive and judge the admission fee, because the park is open.
	///
	/// <para>
	/// This is the transition the whole of the header work was for: <c>FUN_004ff730</c> asks
	/// <c>mParkClosed</c> and nothing else before choosing between judging the fee and waiting outside.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestHeadingForTheGateArrivesAndJudgesTheFee()
	{
		var (_, guests) = RunPark();

		foreach ( var id in new[] { 42, 39, 35, 31, 29 } )
		{
			Assert.AreEqual( PeepState.JudgingTheFee, guests[id].State,
				$"guest {id} should have reached the gate and be judging the fee" );
			Assert.AreEqual( PeepAnimation.Stand, guests[id].Animation,
				$"guest {id} should be standing still while they judge it" );
		}
	}

	/// <summary>
	/// The same five, in a park whose gates are shut, settle down to wait instead - and roll how long they
	/// will put up with it as they do.
	///
	/// <para>
	/// <b>This is the arm the shipped park cannot reach</b>, and it is the reason the behaviour can be built
	/// from the two facts rather than only from a saved park.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheSameGuestsWaitOutsideWhenTheGatesAreShut()
	{
		var (_, guests) = RunPark( parkIsClosed: true );

		foreach ( var id in new[] { 42, 39, 35, 31, 29 } )
		{
			Assert.AreEqual( PeepState.WaitingForOpening, guests[id].State,
				$"guest {id} should be waiting for a shut park to open" );
			Assert.IsTrue( guests[id].ParkOpeningWait is >= 200 and <= 349,
				$"guest {id} should have rolled how long they will wait, not {guests[id].ParkOpeningWait}" );
		}
	}

	/// <summary>
	/// The seven guests coming through the gate arrive, are counted as visitors, and go on to decide what to
	/// do next.
	///
	/// <para>
	/// The numbers are what makes this more than a state check. The save records that nobody has ever been
	/// admitted, so the seven take the first seven numbers between them - one each, none repeated, none
	/// skipped. A count that was incremented per tick rather than per arrival would still leave every guest
	/// in the right state and would fail here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestComingThroughTheGateIsCountedAsAVisitor()
	{
		var world = World();

		Assert.AreEqual( 0, world.NumberOfVisitorsToDate, "the save says nobody has been admitted yet" );

		var (behaviour, guests) = RunPark();
		var entering = new[] { 41, 40, 38, 37, 36, 34, 32 };

		foreach ( var id in entering )
		{
			Assert.AreEqual( PeepState.Deciding, guests[id].State,
				$"guest {id} should have come through the gate and be deciding what to do" );
		}

		Assert.AreEqual( entering.Length, behaviour.VisitorsToDate,
			"the park should have admitted exactly the seven who were coming through the gate" );

		var numbers = entering.Select( id => guests[id].VisitorNumber ).OrderBy( number => number ).ToArray();

		CollectionAssert.AreEqual( Enumerable.Range( 1, entering.Length ).ToArray(), numbers,
			"the seven should take the first seven visitor numbers, one each" );
	}

	/// <summary>
	/// The one guest waiting for the park to open is left exactly as they were, and that is deliberate
	/// rather than an oversight.
	///
	/// <para>
	/// <c>FUN_004ff7f0</c> needs the gate thing's script state and a destination chosen from two gate cells
	/// held in globals, neither of which exists here. So thing 33 stands still, and this pins that it stands
	/// still <i>without</i> being quietly moved on by a state machine guessing at the part it cannot read.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGuestWaitingForTheParkToOpenIsLeftAlone()
	{
		var (_, guests) = RunPark();

		Assert.AreEqual( PeepState.WaitingForOpening, guests[33].State,
			"thing 33 is waiting for the gate, and nothing here can tell them it is open" );
		Assert.AreEqual( 0, guests[33].VisitorNumber, "and they have not been admitted" );
	}

	/// <summary>
	/// Shuffling up a queue ends in the queue whether the guest got there or gave up - the original writes
	/// the same state from both arms, which reads like a mistake and is not: somebody who cannot shuffle
	/// forward is still queueing.
	/// </summary>
	[TestMethod]
	public void FailingToShuffleUpAQueueStillLeavesYouInIt()
	{
		var peep = Guest( 7, (int)PeepState.SteppingUpQueue );

		// A guest with no route and a navigator that has already given up: the walk can only report that it
		// cannot get through, which is the arm this test is about.
		peep.Navigator.NavigateTo( peep.Navigator.Target, ( _, _, _ ) => true, addCurrent: false );

		var behaviour = new PeepBehaviour( parkIsClosed: false, visitorsToDate: 0, new Random( 1 ) );
		var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

		behaviour.Step( peep, walk, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.InQueue, peep.State, "giving up on the shuffle still leaves them queueing" );
	}

	/// <summary>The park's fee and the five numbers that turn it into an opinion, with no park needed.</summary>
	private static ParkAdmission Admission( int fee = 25 )
		=> new( new ParkBalance( "jungle", easyMode: true ), fee );

	/// <summary>
	/// <b>Every one of the twenty-two states a guest can be in is answered by some case of the switch.</b>
	///
	/// <para>
	/// <b>This is the test whose absence Alexah found by playing the game.</b> <c>Step</c> declared
	/// twenty-two states and answered thirteen; the other nine fell out of the bottom of the switch in
	/// silence, so a guest put into one was never walked and never re-stated again. Two of the nine were
	/// <see cref="PeepState.Riding"/> and <see cref="PeepState.LeavingRide"/>, which had shipped that same
	/// day inside work that claimed the ride loop was closed - which is why guests entered a ride and
	/// never came off it.
	/// </para>
	/// <para>
	/// <b>It asserts the property the old test was believed to assert.</b> A guest standing still proves
	/// nothing either way, because several faithful states do exactly that; what distinguishes them is
	/// whether the switch <i>recognised</i> the state, which is what
	/// <see cref="PeepBehaviour.UnansweredState"/> records. Adding a twenty-third state without a case
	/// fails here rather than in somebody's park.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryStateAGuestCanBeInIsAnsweredByTheSwitch()
	{
		var behaviour = new PeepBehaviour( parkIsClosed: false, visitorsToDate: 0, new Random( 1 ),
			Admission() );

		var states = Enum.GetValues<PeepState>();

		Assert.AreEqual( 22, states.Length,
			"the numbering is the save's own and runs 0 to 21 - a new member needs a case, not a new count" );

		foreach ( var state in states )
		{
			var peep = Guest( 6, (int)state );
			var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

			behaviour.Step( peep, walk, playing: null, tick: 1 );

			Assert.IsNull( behaviour.UnansweredState,
				$"{state} fell out of the bottom of the switch, so a guest in it would stand still for ever" );
		}
	}

	/// <summary>
	/// A guest at the gate picks one of the two ticket booths and sets off to be charged -
	/// <c>FUN_004ff520</c>, and the head of the admission sequence.
	///
	/// <para>
	/// The assertion is about the <b>destination</b> as well as the state, because a guest who changed
	/// state without being aimed anywhere would satisfy a state check and then stand at the gate for ever -
	/// which is the failure this whole day's work is about.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestAtTheGateHeadsForOneOfTheTwoTicketBooths()
	{
		var admission = Admission();
		var behaviour = new PeepBehaviour( parkIsClosed: false, visitorsToDate: 0, new Random( 1 ),
			admission );

		var peep = Guest( 6, (int)PeepState.AtGate );
		var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => false );

		behaviour.Step( peep, walk, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.HeadingForGate, peep.State, "they should set off for a booth" );

		var booths = new[] { admission.TicketBoothA, admission.TicketBoothB }
			.Select( cell => new FixedVector(
				PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) ) )
			.ToArray();

		Assert.IsTrue( booths.Contains( peep.Navigator.Target ),
			$"they should be aimed at a ticket booth, not at {peep.Navigator.Target}" );
	}

	/// <summary>
	/// A guest on a ride stays on it, and that is the ride's business rather than theirs.
	///
	/// <para>
	/// <b>Doing nothing is the whole of the original's case 0x10</b> - one call that resolves the ride's
	/// thing pointer and returns. <see cref="ParkRideOperation.Dismiss"/> is what takes them off, from the
	/// ride's own turn. So this pins two things at once: that they are not moved by themselves, and that
	/// the state is <i>answered</i> rather than unrecognised - the difference the old test could not see.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestOnARideIsLeftForTheRideToTakeOff()
	{
		var behaviour = new PeepBehaviour( parkIsClosed: false, visitorsToDate: 0, new Random( 1 ),
			Admission() );

		var peep = Guest( 6, (int)PeepState.Riding );
		var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => false );

		for ( var tick = 1; tick <= 40; ++tick )
			behaviour.Step( peep, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Riding, peep.State, "only the ride may take them off" );
		Assert.IsNull( behaviour.UnansweredState, "and it is answered on purpose, not fallen through" );
	}

	/// <summary>
	/// A guest whose behaviour has nothing to consult is left exactly as they were - and the reason is a
	/// missing <b>input</b>, not a missing case.
	///
	/// <para>
	/// <b>This test used to say something false, and defended a bug with it.</b> It was called "a state
	/// that is not built leaves the guest exactly as they were", and it listed
	/// <see cref="PeepState.Riding"/> and <see cref="PeepState.AtTheBusStop"/> among the unbuilt - pinning
	/// as correct the very freeze Alexah reported, where a guest admitted to a ride never came off it. It
	/// was wrong about the other three it named as well: <see cref="PeepState.Deciding"/>,
	/// <see cref="PeepState.JudgingTheFee"/> and <see cref="PeepState.InQueue"/> all had cases, and stood
	/// still only because a behaviour built from the two facts has no <see cref="ParkAdmission"/> to ask.
	/// It conflated "no case" with "nothing to go on", which are the two things that most need telling
	/// apart here.
	/// </para>
	/// <para>
	/// What it is now is the honest half: with no admission, the states that need one do nothing. The
	/// property it was believed to guard is guarded by
	/// <see cref="EveryStateAGuestCanBeInIsAnsweredByTheSwitch"/>.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestWithNothingToConsultIsLeftExactlyAsTheyWere()
	{
		var behaviour = new PeepBehaviour( parkIsClosed: false, visitorsToDate: 4, new Random( 1 ) );

		Assert.IsNull( behaviour.Admission,
			"the two-fact constructor leaves the fee unjudged on purpose - that is what this test is about" );

		foreach ( var state in new[] { PeepState.Deciding, PeepState.JudgingTheFee, PeepState.AtGate } )
		{
			var peep = Guest( 6, (int)state );
			var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

			behaviour.Step( peep, walk, playing: null, tick: 1 );

			Assert.AreEqual( state, peep.State,
				$"{state} has a case, and with no admission to consult it must leave the guest alone" );
		}

		Assert.AreEqual( 4, behaviour.VisitorsToDate, "and nobody should have been admitted" );
		Assert.IsNull( behaviour.UnansweredState, "none of the three fell out of the switch" );
	}

	/// <summary>
	/// Nobody is left striding on the spot, which is the departure this work retired.
	///
	/// <para>
	/// Until the state machine existed, a guest who arrived was put on the standing animation by the walk
	/// itself, because there was nothing else to do it. Now the animation comes from the state they enter,
	/// through <see cref="Peep.AnimationFor"/>, exactly as the original queues it - so this asserts that
	/// every guest who has stopped is asking for a standing picture rather than a walking one.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestWhoHasStoppedIsStandingRatherThanStridingOnTheSpot()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			for ( var tick = 0; tick < 140 * ParkPeople.ThingTickEvery; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			// The save's own guests, not everybody in the park. A park ticked this long now gains an
			// arrival - 1120 ticks against a period of 600 - and this test is about the thirteen the
			// file named, so it says so rather than counting whoever happens to be standing about.
			var saved = world.People.Select( person => person.ThingId ).ToHashSet();

			var arrived = people.Peeps
				.Where( peep => saved.Contains( peep.ThingId ) )
				.Where( peep => !Peep.IsAWalkingState( peep.State ) )
				.ToArray();

			Assert.AreEqual( 13, arrived.Length, "every guest in this park ends up somewhere that stands" );

			foreach ( var peep in arrived )
			{
				var playing = people.SpriteFor( peep.ThingId );

				Assert.IsNotNull( playing, $"guest {peep.ThingId} should have a sprite" );

				// <b>The SPRITE, not the table.</b> This test asserted Peep.AnimationFor( peep.State )
				// until 2026-09-17, which is a pure lookup over an enum - it passed while every guest in
				// the park stood at the gate playing the eight-picture walk cycle on the spot, which is
				// what Alexah saw within seconds of opening a park. A test named for what is on screen has
				// to read what is on screen.
				Assert.IsTrue( playing!.IsOn( (int)PeepAnimation.Stand ),
					$"guest {peep.ThingId} is in {peep.State} but their sprite is on script {playing.Script}, "
					+ $"set {playing.Set}, frame {playing.Frame} - they are striding on the spot" );
			}

			// And the anti-vacuity guard, which is about MOVEMENT rather than about a particular picture.
			// An earlier draft of this line asserted frame 0 - a number nobody had measured, taken from
			// "set 0, one picture" in a doc comment. What "standing still" actually means is that the
			// picture stops changing, and that cannot pass for a guest still walking: the walk is eight
			// pictures and turns at least every other sprite step.
			var before = arrived.ToDictionary( peep => peep.ThingId,
				peep => people.SpriteFor( peep.ThingId )!.Frame );

			for ( var tick = 0; tick < 32 * ParkPeople.ThingTickEvery; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			foreach ( var peep in arrived )
			{
				Assert.AreEqual( before[peep.ThingId], people.SpriteFor( peep.ThingId )!.Frame,
					$"guest {peep.ThingId} has stopped walking, so their picture should have stopped too" );
			}
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// A guest who will not pay walks out of the park and goes on to pick a cell outside -
	/// <c>FUN_00500a50</c> arriving, then <c>FUN_00501db0(0x13)</c>.
	///
	/// <para>
	/// <b>This is the state four separate arms of this file send guests into, and it had no case.</b>
	/// Judging the fee as far too expensive, sulking down to no happiness, giving up on a shut gate and
	/// deciding in a shut park all set <see cref="PeepState.HeadingForExit"/>; with nothing answering it,
	/// every one of those guests stood exactly where they had decided, which is what Alexah saw at the
	/// ticket booths.
	/// </para>
	/// <para>
	/// <b>The route is asserted before the walk begins</b>, because a bus stop that turned out to be
	/// unreachable would make this pass for the wrong reason - the guest would never arrive, the loop would
	/// run out, and the final assertion would be the only thing that failed. It is also worth recording
	/// that the bus stops really are out on the road: (42,5) and (53,5) sit at its two ends, so a guest
	/// walking the length of the road to reach one is the original's behaviour and not a defect.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AGuestWhoWillNotPayWalksOutOfTheParkAndPicksACellOutside()
	{
		var world = World();
		var admission = Admission();
		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => ParkRides.GateIsOpen );

		var peep = ParkPeople.PeepsIn( world ).Single( person => person.ThingId == 41 );
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		peep.SetState( PeepState.HeadingForExit, tick: 0, new Random( 1 ) );

		peep.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( admission.BusStopA.X ),
			PeepNavigator.WaypointCentre( admission.BusStopA.Y ) );

		Assert.IsTrue( walk.PlanRoute(),
			"the bus stop has to be reachable from inside the park, or this test proves nothing" );

		for ( var tick = 1; tick <= 600 && peep.State == PeepState.HeadingForExit; ++tick )
			behaviour.Step( peep, walk, playing: null, tick );

		Assert.AreEqual( PeepState.PickingACellOutside, peep.State,
			"arriving at the bus stop should send them on to pick a cell outside the park" );

		Assert.AreEqual( admission.BusStopA, walk.Position.Cell,
			"and they should have got there on foot rather than been moved on where they stood" );
	}

	/// <summary>A guest who has already paid, which the save records at <c>mPaidAdmission</c>.</summary>
	private static Peep PaidGuest( int thingId )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)PeepState.WaitingForOpening, SavedState: 6, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0, PaidAdmission: 1 ), StandingStill );

	/// <summary>
	/// <b>A guest who has paid goes through when the cell they stand on names them - and waits while it
	/// names somebody else.</b>
	///
	/// <para>
	/// This is the gate letting one guest through at a time, and it went unbuilt until 2026-09-18.
	/// <c>FUN_004ff7f0</c> reads a short at the cell's <c>+0x24</c> and compares it with the guest's own
	/// thing id; that short is the head of the cell's thing list (<c>FUN_004d91f0</c> writes it), so the
	/// question is "am I the first thing standing here?". <b>Alexah watched six guests fail it for a whole
	/// run</b>, having judged the fee and paid.
	/// </para>
	/// <para>
	/// The negative arm turns on the list being LIFO: thing 99 arrives after our guest, so 99 heads the
	/// cell and our guest - who is standing still, and therefore is not relinked - stays behind them.
	/// </para>
	/// </summary>
	[TestMethod]
	public void APaidGuestGoesThroughOnlyWhenTheCellNamesThem()
	{
		var world = World();
		var admission = Admission();

		var waiting = new ParkState( world );
		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission,
			() => ParkRides.GateIsOpen, waiting );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		// Somebody else is standing here already, and arrives after our guest, so heads the list.
		var second = PaidGuest( 7 );
		var behind = new PeepWalk( second.Navigator, blocked );
		var (cellX, cellY) = behind.Position.Cell;

		waiting.StandOn( second.ThingId, cellX, cellY );
		waiting.StandOn( 99, cellX, cellY );

		behaviour.Step( second, behind, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.WaitingForOpening, second.State,
			"the cell names thing 99, so this guest waits their turn rather than pushing in" );

		// And a guest the cell does name goes through.
		var first = PaidGuest( 8 );
		var walk = new PeepWalk( first.Navigator, blocked );
		var (x, y) = walk.Position.Cell;

		waiting.StandOn( first.ThingId, x, y );

		behaviour.Step( first, walk, playing: null, tick: 2 );

		Assert.AreEqual( PeepState.Entering, first.State, "the cell names them, so they go through" );

		var entrances = new[] { admission.EntranceA, admission.EntranceB }
			.Select( cell => new FixedVector(
				PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) ) )
			.ToArray();

		Assert.IsTrue( entrances.Contains( first.Navigator.Target ),
			$"and they are aimed at a gateway cell, not at {first.Navigator.Target}" );
	}

	/// <summary>One frame, through both clocks, in the order <see cref="Level.Update"/> uses.</summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>Entering a park: the clock re-bases, and the frame spanning the load is spent.</summary>
	private static void EnterPark()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );
	}

	/// <summary>
	/// <b>The park's own tick carries its guests through the state machine - and none of the tests above
	/// says so.</b>
	///
	/// <para>
	/// Every other test in this file builds its own walks and its own behaviour and calls
	/// <see cref="PeepBehaviour.Step"/> directly. That proves the transitions and proves <i>nothing</i>
	/// about the wiring: all eight would pass just as contentedly if <see cref="ParkPeople"/> had never been
	/// changed to call the behaviour at all. This one goes through the real object, on the real clock, for
	/// long enough that guests actually arrive.
	/// </para>
	/// <para>
	/// <b>The length is the point.</b> The park's existing tick tests run about eight thing ticks, which is
	/// why none of them noticed this work: guests need up to a hundred and eight to finish the longest route
	/// in the park, so eight ticks is long enough to see movement and far too short to see an arrival.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TickingARealParkCarriesItsGuestsThroughTheStateMachine()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			for ( var tick = 0; tick < 140 * ParkPeople.ThingTickEvery; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			var guests = people.Peeps.ToDictionary( peep => peep.ThingId );

			foreach ( var id in new[] { 42, 39, 35, 31, 29 } )
			{
				Assert.AreEqual( PeepState.JudgingTheFee, guests[id].State,
					$"guest {id} should have reached the gate through the park's own tick" );
			}

			foreach ( var id in new[] { 41, 40, 38, 37, 36, 34, 32 } )
			{
				Assert.AreEqual( PeepState.Deciding, guests[id].State,
					$"guest {id} should have come through the gate through the park's own tick" );
			}

			Assert.AreEqual( PeepState.WaitingForOpening, guests[33].State,
				"thing 33 is waiting for a gate nothing here can open" );

			// <b>Seven through the gate, and one who arrived while this was running.</b> The eighth is
			// not a guest of the save's: the park now brings people in by itself, one load every
			// Arrival.TimeBetweenArrivals - 150 quarter-ticks, so 600 of the 31ms ticks - and this loop
			// runs 1120 of them, which fits exactly one. A second would need 1200.
			//
			// Counted rather than filtered because the count is the point: admitting somebody at the
			// gate and admitting somebody off a bus are the same event to the park, and ParkState.Admit
			// is the one place either is recorded.
			Assert.AreEqual( 8, people.Visitors,
				"seven came through the gate, and one arrived during the run" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
