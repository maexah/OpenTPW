using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What entering a state does to a guest - the self-contained half of the original's state setter.
///
/// <para>
/// These need no game files and no device: a state's animation and its entry effects depend on the
/// guest alone, which is the whole reason that half could be built before the navigator exists.
/// </para>
/// </summary>
[TestClass]
public class PeepStateTests
{
	private static Peep Guest( int thingId = 4, float toilet = 10f )
		=> new( thingId, new ParkWorld.GuestState(
			State: 6, SavedState: 6, PersonType: 0, Cash: 300, ExitLevel: 100,
			Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: toilet, Illness: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ) );

	private static Random Rolls() => new( 1234 );

	/// <summary>
	/// Every one of the twenty-two states and the animation its setter queues. The whole table is pinned
	/// rather than a sample, because the two groups look arbitrary until they are read as "going
	/// somewhere" and "staying put", and a state moved between them would still look plausible.
	/// </summary>
	[TestMethod]
	public void EveryStateQueuesTheAnimationItsSetterDoes()
	{
		var expected = new (PeepState State, PeepAnimation Animation)[]
		{
			(PeepState.Walking, PeepAnimation.Walk),
			(PeepState.AtGate, PeepAnimation.Stand),
			(PeepState.HeadingForGate, PeepAnimation.Walk),
			(PeepState.WaitingForOpening, PeepAnimation.Stand),
			(PeepState.JudgingTheFee, PeepAnimation.Stand),
			(PeepState.Entering, PeepAnimation.Walk),
			(PeepState.Deciding, PeepAnimation.Stand),
			(PeepState.Wandering, PeepAnimation.Walk),
			(PeepState.PlayingSpotAnimation, PeepAnimation.None),
			(PeepState.GoingToMinorDestination, PeepAnimation.Walk),
			(PeepState.GoingToRide, PeepAnimation.Walk),
			(PeepState.InQueue, PeepAnimation.Stand),
			(PeepState.SteppingUpQueue, PeepAnimation.Walk),
			(PeepState.BeingAdmitted, PeepAnimation.Walk),
			(PeepState.EnteringRide, PeepAnimation.Stand),
			(PeepState.OnRide, PeepAnimation.None),
			(PeepState.Riding, PeepAnimation.None),
			(PeepState.Leaving, PeepAnimation.None),
			(PeepState.HeadingForExit, PeepAnimation.Walk),
			(PeepState.PickingACellOutside, PeepAnimation.Stand),
			(PeepState.WalkingOutside, PeepAnimation.Walk),
			(PeepState.AtTheBusStop, PeepAnimation.Stand)
		};

		Assert.AreEqual( 22, expected.Length, "there are twenty-two states" );

		foreach ( var (state, animation) in expected )
		{
			Assert.AreEqual( animation, Peep.AnimationFor( state ), $"{state} should queue {animation}" );

			// And through the setter, which is what the game actually calls.
			var peep = Guest();
			peep.SetState( state, tick: 8, Rolls() );

			Assert.AreEqual( state, peep.State, $"{state} should be the state entered" );
			Assert.AreEqual( animation, peep.Animation, $"{state} through the setter" );
		}
	}

	/// <summary>
	/// The three groups, counted. This is the anti-vacuity guard for the table above: the numbers have to
	/// add up to twenty-two, so a state quietly dropped from one group and not added to another would
	/// fail here even if its own row were removed from the table as well.
	/// </summary>
	[TestMethod]
	public void TheThreeGroupsAccountForEveryState()
	{
		var states = Enum.GetValues<PeepState>();

		Assert.AreEqual( 22, states.Length, "states in the enum" );

		CollectionAssert.AreEqual( Enumerable.Range( 0, 22 ).ToArray(),
			states.Select( state => (int)state ).ToArray(),
			"the numbers are the save's own, so they run 0 to 21 with no gaps" );

		var grouped = states.GroupBy( Peep.AnimationFor )
			.ToDictionary( group => group.Key, group => group.Count() );

		Assert.AreEqual( 10, grouped[PeepAnimation.Walk], "states that walk" );
		Assert.AreEqual( 8, grouped[PeepAnimation.Stand], "states that stand" );
		Assert.AreEqual( 4, grouped[PeepAnimation.None], "states that queue nothing" );
	}

	/// <summary>
	/// How long a guest will wait outside is rolled once, as they begin waiting, and lands in the band
	/// the original rolls it in.
	/// </summary>
	[TestMethod]
	public void WaitingForTheParkToOpenRollsHowLongTheyWillPutUpWithIt()
	{
		var seen = new System.Collections.Generic.HashSet<int>();
		var random = Rolls();

		for ( var i = 0; i < 50; ++i )
		{
			var peep = Guest();

			peep.SetState( PeepState.WaitingForOpening, tick: 0, random );

			Assert.IsTrue( peep.ParkOpeningWait is >= 200 and <= 349,
				$"a wait of {peep.ParkOpeningWait} is outside the two hundred to three hundred and forty-nine the game rolls" );

			seen.Add( peep.ParkOpeningWait );
		}

		Assert.IsTrue( seen.Count > 1, "the wait should be rolled rather than fixed" );
	}

	/// <summary>
	/// The two states that stamp the clock as they are entered, so that what comes later can tell how
	/// long the guest has been at it.
	/// </summary>
	[TestMethod]
	public void StartingAnAnimationOrAQueueStampsTheTick()
	{
		var spot = Guest();
		spot.SetState( PeepState.PlayingSpotAnimation, tick: 517, Rolls() );
		Assert.AreEqual( 517, spot.TimeOfLastSpotAnim );

		var queued = Guest();
		queued.SetState( PeepState.InQueue, tick: 904, Rolls() );
		Assert.AreEqual( 904, queued.TimeStartedIdling );
	}

	/// <summary>
	/// Shuffling up a queue is never done in a hurry, whatever the guest's needs say - the setter clears
	/// the hurry flag outright. Pinned with a guest who <i>is</i> hurrying, or it would pass on a guest
	/// who never was.
	/// </summary>
	[TestMethod]
	public void ShufflingUpAQueueIsNeverDoneInAHurry()
	{
		var peep = Guest( toilet: 90f );

		peep.Tick( 4 );

		Assert.AreEqual( Peep.HurryingSpeed, peep.PurposeSpeed, "this guest should be hurrying to begin with" );

		peep.SetState( PeepState.SteppingUpQueue, tick: 4, Rolls() );

		Assert.AreEqual( Peep.UnhurriedSpeed, peep.PurposeSpeed, "and should stop hurrying to join the queue" );
	}

	/// <summary>Both of the states that give up on the park forget wherever the guest was headed.</summary>
	[TestMethod]
	public void GivingUpForgetsWhereTheyWereGoing()
	{
		foreach ( var state in new[] { PeepState.Leaving, PeepState.HeadingForExit } )
		{
			var peep = Guest();
			peep.MajorDest = 24;

			peep.SetState( state, tick: 4, Rolls() );

			Assert.AreEqual( 0, peep.MajorDest, $"{state} should clear the chosen destination" );
		}

		// And a state that is not one of those leaves it alone, or the test above proves nothing.
		var walking = Guest();
		walking.MajorDest = 24;

		walking.SetState( PeepState.GoingToRide, tick: 4, Rolls() );

		Assert.AreEqual( 24, walking.MajorDest, "going to a ride should keep the ride they chose" );
	}
}
