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
