using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// What a ride does to its own queue on its turn - <see cref="ParkRideOperation"/>, the tail of the
/// original's per-object tick (<c>FUN_004e0b90</c>).
///
/// <para>
/// <b>These need no game files.</b> Every queue in the shipped park is empty, so it cannot exercise a
/// stale head at all - the situation only arises once guests join queues and then stop queueing, which is
/// a running park rather than a saved one. A queue built here by hand is the only thing that can reach it.
/// </para>
/// <para>
/// <b>The discriminating case is state 8.</b> <c>FUN_00502430</c> reads a guest's state, and when it is
/// eight - <see cref="PeepState.PlayingSpotAnimation"/>, the one state that exists to be returned from -
/// it reads the SAVED state instead. A reading that ignored that would treat a guest mid-animation as
/// having left the queue and would throw away the head underneath them.
/// </para>
/// </summary>
[TestClass]
public class ParkRideOperationTests
{
	private const int Ride = 13;

	/// <summary>A guest built from nothing, with both states under the test's control.</summary>
	private static Peep Guest( int thingId, PeepState state, PeepState savedState = PeepState.Deciding )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)savedState, PersonType: 0, Cash: 300, ExitLevel: 100,
			Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	private static ParkState Park() => new( parkIsClosed: false, visitorsToDate: 0 );

	/// <summary>
	/// The four states that count as queueing, and the many that do not.
	/// </summary>
	[TestMethod]
	public void FourStatesCountAsQueueing()
	{
		foreach ( var state in new[]
		{
			PeepState.InQueue, PeepState.SteppingUpQueue,
			PeepState.BeingAdmitted, PeepState.EnteringRide
		} )
		{
			Assert.IsTrue( ParkRideOperation.IsQueueing( Guest( 7, state ) ), $"{state} is queueing" );
		}

		// The neighbours either side of the band matter most: one below and one above.
		foreach ( var state in new[]
		{
			PeepState.Walking, PeepState.Deciding, PeepState.Wandering,
			PeepState.GoingToMinorDestination, PeepState.GoingToRide,
			PeepState.LeavingRide, PeepState.Riding, PeepState.HeadingForExit
		} )
		{
			Assert.IsFalse( ParkRideOperation.IsQueueing( Guest( 7, state ) ), $"{state} is not" );
		}
	}

	/// <summary>
	/// <b>A guest playing a one-off animation is judged on the state they will go back to.</b> This is the
	/// case a simpler reading gets wrong, and it goes both ways.
	/// </summary>
	[TestMethod]
	public void AGuestMidAnimationIsJudgedOnTheStateTheyWillReturnTo()
	{
		Assert.IsTrue( ParkRideOperation.IsQueueing(
			Guest( 7, PeepState.PlayingSpotAnimation, savedState: PeepState.InQueue ) ),
			"mid-animation, but they were queueing and will be again" );

		Assert.IsFalse( ParkRideOperation.IsQueueing(
			Guest( 7, PeepState.PlayingSpotAnimation, savedState: PeepState.Wandering ) ),
			"mid-animation, and they were not queueing" );

		// And eight itself is not in the band, so the deferral is doing the work rather than the range.
		Assert.IsFalse( ParkRideOperation.IsQueueing(
			Guest( 7, PeepState.PlayingSpotAnimation, savedState: PeepState.PlayingSpotAnimation ) ),
			"the saved state is not a queueing one either" );
	}

	/// <summary>A head who is still queueing is left exactly where they are.</summary>
	[TestMethod]
	public void AQueueingHeadIsLeftAlone()
	{
		var park = Park();
		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );

		var guests = new Dictionary<int, Peep>
		{
			[7] = Guest( 7, PeepState.SteppingUpQueue ),
			[8] = Guest( 8, PeepState.InQueue )
		};

		Assert.IsFalse( new ParkRideOperation( park, guests ).DropStaleQueueHead( Ride ),
			"nothing to drop" );

		Assert.AreEqual( 7, park.FirstInQueue( Ride ) );
		Assert.AreEqual( 2, park.QueueLength( Ride ) );
	}

	/// <summary>
	/// <b>A head who has stopped queueing is dropped - and the rest of the chain is deliberately left
	/// standing</b>, which is the original's own reach rather than a half-finished removal.
	/// </summary>
	[TestMethod]
	public void AHeadWhoHasStoppedQueueingIsDroppedAndNobodyIsPromoted()
	{
		var park = Park();
		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );

		var guests = new Dictionary<int, Peep>
		{
			[7] = Guest( 7, PeepState.Deciding ),
			[8] = Guest( 8, PeepState.InQueue )
		};

		Assert.IsTrue( new ParkRideOperation( park, guests ).DropStaleQueueHead( Ride ), "dropped" );

		Assert.AreEqual( 0, park.FirstInQueue( Ride ), "the head is forgotten" );
		Assert.AreEqual( 0, park.QueueLength( Ride ), "so the queue measures nought" );

		// NOT tidied: the second guest is not promoted, and their links are exactly as they were. The
		// original repairs this on the next join or leave, not here.
		Assert.AreEqual( 8, park.NextInQueue( 7 ), "the chain behind the dropped head is untouched" );
		Assert.AreEqual( 7, park.PreviousInQueue( 8 ), "and still names the guest who left the front" );
	}

	/// <summary>A head naming somebody the park has never heard of is dropped too.</summary>
	[TestMethod]
	public void AHeadNamingNobodyIsDropped()
	{
		var park = Park();
		park.JoinQueue( Ride, 7 );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.DropStaleQueueHead( Ride ), "nobody by that id, so the head cannot be queueing" );

		Assert.AreEqual( 0, park.FirstInQueue( Ride ) );
	}

	/// <summary>An empty queue has no head to drop, and says so rather than throwing.</summary>
	[TestMethod]
	public void AnEmptyQueueHasNothingToDrop()
	{
		var park = Park();

		Assert.IsFalse( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.DropStaleQueueHead( Ride ) );

		Assert.AreEqual( 0, park.QueueLength( Ride ) );
	}

	/// <summary>A park with nothing loaded sweeps nothing, rather than throwing.</summary>
	[TestMethod]
	public void NoParkSweepsNothing()
		=> Assert.AreEqual( 0, new ParkRideOperation( Park(), new Dictionary<int, Peep>() )
			.DropStaleQueueHeads( null ) );
}
