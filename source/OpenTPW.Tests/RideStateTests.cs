using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The ride a script drives, on its own. These need no game data: the behaviour under test came out of
/// the engine's own functions, so it can be stated exactly.
///
/// <para>
/// Three of these guard against a reading that would look right and be wrong - room being the length of
/// the queue rather than what is left of it, closing being a flag rather than a transition, and breaking
/// being a boolean rather than the three values <c>FUN_0043b270</c> actually dispatches on.
/// </para>
/// </summary>
[TestClass]
public class RideStateTests
{
	/// <summary>
	/// <c>GETQUEUE</c> answers with the room remaining. Reading it as the queue's length would pass a
	/// test that only checked it changed when somebody joined, so this pins the direction.
	/// </summary>
	[TestMethod]
	public void RoomIsWhatIsLeftRatherThanWhoIsWaiting()
	{
		var ride = new RideState();

		ride.SetCapacity( 5 );

		Assert.AreEqual( 5, ride.RoomRemaining(), "an empty ride has all its room" );

		ride.AddRider( 1 );
		ride.AddRider( 2 );

		Assert.AreEqual( 2, ride.Waiting, "the riders did not join" );
		Assert.AreEqual( 3, ride.RoomRemaining(), "room went the wrong way" );
	}

	/// <summary>
	/// The original clamps the subtraction at zero rather than letting it go negative, which is what its
	/// <c>((int)x &lt; 0) - 1 &amp; x</c> tail does. Lowering the capacity under the riders already
	/// waiting is the way to reach that.
	/// </summary>
	[TestMethod]
	public void RoomStopsAtZeroRatherThanGoingNegative()
	{
		var ride = new RideState();

		ride.SetCapacity( 2 );
		ride.AddRider( 1 );
		ride.AddRider( 2 );
		ride.SetCapacity( 1 );

		Assert.AreEqual( 0, ride.RoomRemaining(), "the room went negative" );
	}

	/// <summary>A ride with no room turns a rider away, and says that it did.</summary>
	[TestMethod]
	public void ARiderIsTurnedAwayWhenThereIsNoRoom()
	{
		var ride = new RideState();

		ride.SetCapacity( 1 );
		ride.AddRider( 1 );
		ride.AddRider( 2 );

		Assert.AreEqual( 1, ride.Waiting, "the second rider got on a full ride" );
		Assert.AreEqual( 1, ride.Refused, "the refusal was not counted" );
	}

	/// <summary>
	/// <c>GETPEEP</c> answers 0 for "nobody", and the queue it drains is not the one riders join - in the
	/// original they are two separate rings, and nothing in <c>COAST</c> moves anyone between them.
	/// </summary>
	[TestMethod]
	public void TakingARiderDrainsTheFinishedQueueAndNotTheWaitingOne()
	{
		var ride = new RideState();

		ride.SetCapacity( 4 );
		ride.AddRider( 1 );

		Assert.AreEqual( 0, ride.TakeRider(), "somebody came off a ride nobody had finished" );
		Assert.AreEqual( 1, ride.Waiting, "the waiting rider was taken instead" );

		ride.FinishRider( 9 );

		Assert.AreEqual( 9, ride.TakeRider(), "the finished rider did not come back" );
		Assert.AreEqual( 0, ride.TakeRider(), "the same rider came back twice" );
	}

	/// <summary>
	/// Closing is a transition the engine refuses when it does not fit, not a flag that can be written
	/// twice. A ride starts open, so the close every shipped script opens with has something to act on.
	/// </summary>
	[TestMethod]
	public void ClosingIsATransitionRatherThanAFlag()
	{
		var ride = new RideState();

		Assert.IsFalse( ride.Closed, "a ride should start open" );

		ride.SetClosed( 1 );

		Assert.IsTrue( ride.Closed, "the ride did not shut" );

		ride.SetClosed( 1 );

		Assert.IsTrue( ride.Closed, "shutting it twice changed something" );
		Assert.AreEqual( 1, ride.Refused, "the second close was not refused" );

		ride.SetClosed( 0 );

		Assert.IsFalse( ride.Closed, "the ride did not open again" );
	}

	/// <summary>
	/// <c>SETBROKE</c> takes 0, 1 and 2 - the shipped handler dispatches on all three, even though no
	/// shipped script passes 2. Anything else is not a request the engine makes.
	/// </summary>
	[TestMethod]
	public void BreakingTakesThreeValuesAndNotABoolean()
	{
		var ride = new RideState();

		Assert.IsFalse( ride.Broken, "a ride should start working" );

		ride.SetBroken( 1 );

		Assert.IsTrue( ride.Broken, "the ride did not break" );

		ride.SetBroken( 0 );

		Assert.IsFalse( ride.Broken, "the ride did not mend" );

		// The third value is a state of its own, and it is not "broken".
		ride.SetBroken( 2 );

		Assert.IsFalse( ride.Broken, "the third value was treated as broken" );
		Assert.AreEqual( 0, ride.Refused, "a value the engine accepts was refused" );

		ride.SetBroken( 5 );

		Assert.AreEqual( 1, ride.Refused, "a value the engine never passes was accepted" );
	}

	/// <summary>A negative capacity clamps to zero, as the original's mask does.</summary>
	[TestMethod]
	public void CapacityClampsAtZero()
	{
		var ride = new RideState();

		ride.SetCapacity( -3 );

		Assert.AreEqual( 0, ride.Capacity, "a negative capacity survived" );
	}

	/// <summary>
	/// <c>SETWORN</c> does nothing, because the shipped handler does nothing: it resolves its operand and
	/// returns without calling anything. Twelve scripts use it, so this asserts the absence on purpose -
	/// implementing it from its name would invent a wear system the original has not got.
	/// </summary>
	[TestMethod]
	public void SettingWornChangesNothingAtAll()
	{
		var ride = new RideState();

		ride.SetCapacity( 3 );
		ride.AddRider( 1 );
		ride.SetWorn( 99 );

		Assert.AreEqual( 3, ride.Capacity, "wear changed the capacity" );
		Assert.AreEqual( 1, ride.Waiting, "wear changed the queue" );
		Assert.IsFalse( ride.Closed, "wear shut the ride" );
		Assert.IsFalse( ride.Broken, "wear broke the ride" );
		Assert.AreEqual( 0, ride.Refused, "wear was counted as a refusal" );
	}
}
