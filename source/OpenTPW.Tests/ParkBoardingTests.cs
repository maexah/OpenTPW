using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A guest being called forward from the front of a queue - the buildable arm of <c>FUN_004ffff0</c>,
/// and the <c>mBeenAdmitted</c> flag it turns on.
///
/// <para>
/// <b>Three things must all hold, and the tests here are mostly about what happens when one does not.</b>
/// The guest must be at the FRONT (<c>mQueuePos</c> nought), must carry the invitation
/// (<c>mBeenAdmitted</c>), and the ride must really have nominated THEM - the original's
/// <c>FUN_004e0aa0</c> is nothing but <c>person == mPersonBeingLoaded</c>. A build that dropped any one
/// of those would board the wrong guest, or board one twice, and would look perfectly fine doing it.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkBoardingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Ride = 13;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>A guest queueing for the ride, with the three conditions under the test's control.</summary>
	private static Peep Queueing( int thingId, int queuePos, bool admitted )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)PeepState.InQueue, SavedState: (int)PeepState.Deciding, PersonType: 0,
			Cash: 300, ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f,
			Vomit: 0f, Litter: 0f, MajorDest: Ride, QueuePos: queuePos, PrankeryIndex: 0,
			BeenAdmitted: admitted ? 1 : 0 ), StandingStill );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>Steps one guest once, against a real park, and hands back what they ended up doing.</summary>
	private PeepState StepOne( Peep peep, bool nominated )
	{
		var world = Park();
		var state = new ParkState( world );

		if ( nominated )
			state.NominateForLoading( Ride, peep.ThingId );

		var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen,
			state, new ParkItemCatalogue( "jungle", data ) );

		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		behaviour.Step( peep, walk, playing: null, tick: 40 );

		return peep.State;
	}

	/// <summary>
	/// <b>All three conditions met: the guest goes to board, and the invitation is used up.</b>
	/// </summary>
	[TestMethod]
	public void AGuestCalledForwardFromTheFrontGoesToBoard()
	{
		var peep = Queueing( 7, queuePos: 0, admitted: true );

		Assert.AreEqual( PeepState.BeingAdmitted, StepOne( peep, nominated: true ) );

		Assert.IsFalse( peep.BeenAdmitted,
			"the invitation is cleared, or one call forward would board them twice" );
	}

	/// <summary>
	/// <b>The three refusals, one at a time.</b> Each leaves the guest queueing, and each is a separate
	/// test of the original's - dropping any one of them would still pass the happy path above.
	/// </summary>
	[TestMethod]
	public void EachOfTheThreeConditionsIsNeededOnItsOwn()
	{
		// Invited and nominated, but not at the front.
		Assert.AreEqual( PeepState.InQueue,
			StepOne( Queueing( 7, queuePos: 1, admitted: true ), nominated: true ),
			"second in the queue is not called forward" );

		// At the front and nominated, but never invited.
		Assert.AreEqual( PeepState.InQueue,
			StepOne( Queueing( 7, queuePos: 0, admitted: false ), nominated: true ),
			"no invitation, no boarding" );

		// At the front and invited, but the ride has nominated somebody else.
		var world = Park();
		var state = new ParkState( world );
		state.NominateForLoading( Ride, 99 );

		var peep = Queueing( 7, queuePos: 0, admitted: true );
		var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen,
			state, new ParkItemCatalogue( "jungle", data ) );
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		behaviour.Step( peep, walk, playing: null, tick: 40 );

		Assert.AreEqual( PeepState.InQueue, peep.State, "the ride called somebody else forward" );
		Assert.IsTrue( peep.BeenAdmitted, "and this guest keeps their invitation" );
	}

	/// <summary>One park, one state, one behaviour - for the tests where a queue has to survive a turn.</summary>
	private (ParkWorld World, ParkState State, PeepBehaviour Behaviour) Standing()
	{
		var world = Park();
		var state = new ParkState( world );

		return (world, state, new PeepBehaviour( world, new Random( 1 ), null,
			() => ParkRides.GateIsOpen, state, new ParkItemCatalogue( "jungle", data ) ));
	}

	/// <summary>
	/// <b>The guest who becomes head has their place corrected, which is what lets the ride keep
	/// loading.</b>
	///
	/// <para>
	/// Nothing renumbers a queue when somebody leaves it, here or in the original, so the new head still
	/// carries the place they joined with - and <see cref="ParkRideOperation.Invite"/> only calls forward
	/// a head whose place is nought. Until the middle arm of <c>FUN_004ffff0</c> was built, that meant
	/// <b>exactly one guest could ever ride</b>: measured in a running park, one rode and the three
	/// behind them stood on the same cell for the remaining two minutes.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGuestWhoBecomesHeadHasTheirPlaceCorrected()
	{
		var (world, state, behaviour) = Standing();

		var head = Queueing( 7, queuePos: state.JoinQueue( Ride, 7 ), admitted: false );
		var second = Queueing( 8, queuePos: state.JoinQueue( Ride, 8 ), admitted: false );

		Assert.AreEqual( 0, head.QueuePos, "the first to arrive is at the front" );
		Assert.AreEqual( 1, second.QueuePos, "and the second joined behind them" );

		// The head is taken onto the ride, exactly as CompleteAdmission does it.
		Assert.IsTrue( state.LeaveQueue( Ride, 7 ), "the head comes out of the queue" );

		var walk = new PeepWalk( second.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		behaviour.Step( second, walk, playing: null, tick: 40 );

		Assert.AreEqual( 0, second.QueuePos,
			"the new head's place is recomputed from the links, or they are refused for ever" );
		Assert.AreEqual( PeepState.InQueue, second.State, "and they are still queueing" );
	}

	/// <summary>
	/// A guest only one place out waits out <see cref="Peep.QueueMoveDelay"/> before re-taking it -
	/// the <c>else</c> arm of the same comparison, which spends one of the delay instead.
	/// </summary>
	[TestMethod]
	public void AGuestOnlyOnePlaceOutWaitsOutTheirMoveDelayFirst()
	{
		var (world, state, behaviour) = Standing();

		state.JoinQueue( Ride, 7 );

		var second = Queueing( 8, queuePos: state.JoinQueue( Ride, 8 ), admitted: false );
		second.QueueMoveDelay = 3;

		Assert.IsTrue( state.LeaveQueue( Ride, 7 ) );

		var walk = new PeepWalk( second.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		behaviour.Step( second, walk, playing: null, tick: 40 );

		Assert.AreEqual( 1, second.QueuePos, "one place out, so they wait rather than shuffle at once" );
		Assert.AreEqual( 2, second.QueueMoveDelay, "and one turn of the delay is spent" );

		// Two more turns spend the rest of it, and the third corrects them.
		behaviour.Step( second, walk, playing: null, tick: 41 );
		behaviour.Step( second, walk, playing: null, tick: 42 );

		Assert.AreEqual( 0, second.QueueMoveDelay, "the delay is used up" );

		behaviour.Step( second, walk, playing: null, tick: 43 );

		Assert.AreEqual( 0, second.QueuePos, "and now they take their place" );
	}

	/// <summary>
	/// <b>A guest whose place has moved BACKWARD re-takes it at once, delay or no delay</b> - the
	/// original compares the drift in unsigned byte arithmetic, so a negative difference wraps past the
	/// two it tolerates. Reproducing it signed would have them sit out the delay instead.
	/// </summary>
	[TestMethod]
	public void AGuestWhosePlaceMovedBackwardDoesNotWait()
	{
		var (world, state, behaviour) = Standing();

		state.JoinQueue( Ride, 7 );
		state.JoinQueue( Ride, 8 );

		// Joined third, but carrying a recorded place of nought - so their true place is FURTHER BACK
		// than the one they hold, and QueuePos - place is negative.
		var third = Queueing( 9, queuePos: 0, admitted: false );
		state.JoinQueue( Ride, 9 );
		third.QueueMoveDelay = 50;

		var walk = new PeepWalk( third.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		behaviour.Step( third, walk, playing: null, tick: 40 );

		Assert.AreEqual( 2, third.QueuePos, "they take their real place immediately" );
		Assert.AreEqual( 50, third.QueueMoveDelay, "without spending any of the delay" );
	}

	/// <summary>
	/// <b>Joining a queue seeds the move delay from how far back you are</b> - <c>FUN_00501db0</c>'s case
	/// <c>0xb</c>, which converts <c>mQueuePos</c> to a float, multiplies by the constant at
	/// <c>0x007007a4</c> (1.2) and truncates it into <c>mQueueMoveDelay</c>.
	///
	/// <para>
	/// The guest at the front waits not at all, which is what keeps the head of a queue responsive to
	/// being called forward. This was missing when the step-up was first built, so the delay was read
	/// from the save once and never renewed.
	/// </para>
	/// </summary>
	[TestMethod]
	public void JoiningAQueueSeedsTheMoveDelayFromHowFarBackYouAre()
	{
		var back = Queueing( 7, queuePos: 5, admitted: false );

		back.SetState( PeepState.InQueue, tick: 40, new Random( 1 ) );

		Assert.AreEqual( 6, back.QueueMoveDelay, "five places back, at 1.2 a place, truncated" );
		Assert.AreEqual( (int)(5 * Peep.QueueDelayPerPlace), back.QueueMoveDelay,
			"and it is the executable's own constant rather than a literal repeated here" );

		var front = Queueing( 8, queuePos: 0, admitted: false );

		front.SetState( PeepState.InQueue, tick: 40, new Random( 1 ) );

		Assert.AreEqual( 0, front.QueueMoveDelay, "the guest at the front waits not at all" );
	}

	/// <summary>
	/// The flag is read from the save, and reading it has not disturbed its neighbours - <c>mCash</c> sits
	/// four bytes after it and is pinned independently.
	/// </summary>
	[TestMethod]
	public void TheAdmittedFlagIsReadAndItsNeighbourStillReadsTrue()
	{
		var guests = Park().People.Where( person => person.Guest != null ).ToArray();

		Assert.AreEqual( 13, guests.Length, "the park's guests" );

		foreach ( var person in guests )
		{
			Assert.AreEqual( 0, person.Guest!.Value.BeenAdmitted,
				$"guest {person.ThingId}: nobody in this park has ever been called onto a ride" );
		}

		// mBalloonScript 406, mBeenAdmitted 410, mCash 414 - so a misread here would show in the cash,
		// whose values are pinned in ParkGuestStateTests.
		var first = guests.First( person => person.ThingId == 42 ).Guest!.Value;

		Assert.AreEqual( 684, first.Cash, "guest 42's cash, four bytes after the flag" );
		Assert.AreEqual( 142, first.ExitLevel, "and their exit level after that" );
	}
}
