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
	public void MountTheGame() => data = GameData.Required();

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

	/// <summary>
	/// A state the machine does not answer leaves the guest untouched. This is the guard that stops the
	/// switch quietly acquiring a default.
	/// </summary>
	[TestMethod]
	public void AStateThatIsNotBuiltLeavesTheGuestExactlyAsTheyWere()
	{
		var behaviour = new PeepBehaviour( parkIsClosed: false, visitorsToDate: 4, new Random( 1 ) );

		foreach ( var state in new[] { PeepState.Deciding, PeepState.JudgingTheFee, PeepState.InQueue,
			PeepState.Riding, PeepState.AtTheBusStop } )
		{
			var peep = Guest( 6, (int)state );
			var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

			behaviour.Step( peep, walk, playing: null, tick: 1 );

			Assert.AreEqual( state, peep.State, $"{state} is not built, so nothing should have moved" );
		}

		Assert.AreEqual( 4, behaviour.VisitorsToDate, "and nobody should have been admitted" );
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

			var arrived = people.Peeps.Where( peep => !Peep.IsAWalkingState( peep.State ) ).ToArray();

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

			Assert.AreEqual( 7, people.Visitors,
				"the seven who came through the gate should have been counted as visitors" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
