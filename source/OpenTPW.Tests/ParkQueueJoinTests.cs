using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The queue a running park actually moves - <see cref="ParkState"/>'s join and leave, which are the
/// original's <c>FUN_004ddb90</c> and <c>FUN_004ddd20</c>.
///
/// <para>
/// <b>These need no game files at all, and that is the point of them.</b> Every queue in the shipped park
/// is empty, so the save can neither exercise a join nor tell a correct unlink from a broken one. A queue
/// built here by hand can do both - and the middle-removal case below is the one a doubly-linked list
/// gets wrong quietly, because dropping a link only shows up from the side you did not check.
/// </para>
/// </summary>
[TestClass]
public class ParkQueueJoinTests
{
	/// <summary>A park with no save behind it, which is all these need - see the second constructor.</summary>
	private static ParkState Park() => new( parkIsClosed: false, visitorsToDate: 0 );

	private const int Ride = 13;

	/// <summary>
	/// Joining puts a guest at the BACK and hands back the place they took, counting from nought.
	/// </summary>
	[TestMethod]
	public void JoiningAppendsAtTheBackAndReportsThePlaceTaken()
	{
		var park = Park();

		Assert.AreEqual( 0, park.JoinQueue( Ride, 7 ), "the first to arrive is at the front" );
		Assert.AreEqual( 1, park.JoinQueue( Ride, 8 ), "the second is behind them" );
		Assert.AreEqual( 2, park.JoinQueue( Ride, 9 ), "and the third behind that" );

		Assert.AreEqual( 3, park.QueueLength( Ride ), "three are queueing" );
		Assert.AreEqual( 7, park.FirstInQueue( Ride ), "and the first is still at the head" );

		// Forwards and backwards, because a join that set only one direction would still pass a length test.
		Assert.AreEqual( 8, park.NextInQueue( 7 ) );
		Assert.AreEqual( 9, park.NextInQueue( 8 ) );
		Assert.AreEqual( 0, park.NextInQueue( 9 ), "nobody behind the last" );

		Assert.AreEqual( 0, park.PreviousInQueue( 7 ), "nobody in front of the first" );
		Assert.AreEqual( 7, park.PreviousInQueue( 8 ) );
		Assert.AreEqual( 8, park.PreviousInQueue( 9 ) );
	}

	/// <summary>
	/// <b>The load-bearing one.</b> Taking somebody out of the middle joins up the two either side of
	/// them, in both directions.
	/// </summary>
	[TestMethod]
	public void LeavingFromTheMiddleJoinsUpBothSides()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );
		park.JoinQueue( Ride, 9 );

		Assert.IsTrue( park.LeaveQueue( Ride, 8 ), "they were in it" );

		Assert.AreEqual( 2, park.QueueLength( Ride ) );
		Assert.AreEqual( 9, park.NextInQueue( 7 ), "the one in front now points past them" );
		Assert.AreEqual( 7, park.PreviousInQueue( 9 ), "and the one behind points back past them" );

		// And the leaver keeps no links of their own - the original asserts exactly this, twice.
		Assert.AreEqual( 0, park.NextInQueue( 8 ) );
		Assert.AreEqual( 0, park.PreviousInQueue( 8 ) );
	}

	/// <summary>The head leaving makes the next one the head, with nobody in front of them.</summary>
	[TestMethod]
	public void LeavingFromTheFrontMakesTheNextOneTheHead()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );

		Assert.IsTrue( park.LeaveQueue( Ride, 7 ) );

		Assert.AreEqual( 8, park.FirstInQueue( Ride ) );
		Assert.AreEqual( 0, park.PreviousInQueue( 8 ), "the new head has nobody in front of them" );
		Assert.AreEqual( 1, park.QueueLength( Ride ) );
	}

	/// <summary>The last one leaving leaves nobody behind the one in front.</summary>
	[TestMethod]
	public void LeavingFromTheBackLeavesNobodyBehind()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );

		Assert.IsTrue( park.LeaveQueue( Ride, 8 ) );

		Assert.AreEqual( 0, park.NextInQueue( 7 ), "nobody behind the one who stayed" );
		Assert.AreEqual( 7, park.FirstInQueue( Ride ), "who is still the head" );
		Assert.AreEqual( 1, park.QueueLength( Ride ) );
	}

	/// <summary>The only one leaving empties the queue rather than leaving a head pointing at nobody.</summary>
	[TestMethod]
	public void TheLastOneOutEmptiesTheQueue()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );

		Assert.IsTrue( park.LeaveQueue( Ride, 7 ) );

		Assert.AreEqual( 0, park.FirstInQueue( Ride ), "no head left" );
		Assert.AreEqual( 0, park.QueueLength( Ride ) );

		// And it can be joined again afterwards, which a stale head would break.
		Assert.AreEqual( 0, park.JoinQueue( Ride, 9 ), "the next to arrive is at the front again" );
	}

	/// <summary>
	/// Somebody who was never in it is reported as such, <b>and the head is still written</b>: the original
	/// splices by the leaver's own links (<c>FUN_004ddd20</c>, <c>0x004ddde9</c>), so a leaver with nobody in
	/// front and nobody behind leaves the head nought, and the guest who was at the front is linked to nobody's
	/// queue.
	/// </summary>
	[TestMethod]
	public void SomebodyWhoWasNeverInItEmptiesTheHead()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );

		Assert.IsFalse( park.LeaveQueue( Ride, 99 ), "they were never queueing" );
		Assert.AreEqual( 0, park.FirstInQueue( Ride ), "the head is their own next, which is nobody" );
		Assert.AreEqual( 0, park.QueueLength( Ride ), "so the queue walks as nought long" );
		Assert.AreEqual( 8, park.NextInQueue( 7 ), "while the two who were in it keep their links" );
		Assert.AreEqual( 7, park.PreviousInQueue( 8 ) );
	}

	/// <summary>
	/// A leaver cut off from the head - nobody in front, somebody behind - makes whoever is behind them the head,
	/// in place of the guest who was there (<c>0x004ddde9</c>).
	/// </summary>
	[TestMethod]
	public void ALeaverWithNobodyInFrontMakesTheNextTheHead()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );
		park.JoinQueue( Ride, 9 );

		// 7 stops queueing and the ride drops them (FUN_004e0b90), leaving 8 with a link back to 7.
		park.ClearQueueHead( Ride );
		park.JoinQueue( Ride, 5 );

		Assert.AreEqual( 5, park.FirstInQueue( Ride ), "a guest joining an empty head becomes it" );

		// 7 leaves by their own links: nobody in front, 8 behind.
		Assert.IsTrue( park.LeaveQueue( Ride, 7 ) );
		Assert.AreEqual( 8, park.FirstInQueue( Ride ), "8 is the head now, and 5 is cut off" );
		Assert.AreEqual( 2, park.QueueLength( Ride ), "8 and 9" );
	}

	/// <summary>
	/// Two objects keep two queues - which is what says the head is per-object rather than one global
	/// list that happens to work while only one thing is busy.
	/// </summary>
	[TestMethod]
	public void EachObjectKeepsItsOwnQueue()
	{
		var park = Park();
		const int Sideshow = 14;

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Sideshow, 8 );
		park.JoinQueue( Ride, 9 );

		Assert.AreEqual( 2, park.QueueLength( Ride ) );
		Assert.AreEqual( 1, park.QueueLength( Sideshow ) );

		Assert.AreEqual( 7, park.FirstInQueue( Ride ) );
		Assert.AreEqual( 8, park.FirstInQueue( Sideshow ) );
		Assert.AreEqual( 9, park.NextInQueue( 7 ), "the sideshow's guest is not in the ride's queue" );
	}

	/// <summary>
	/// Where each guest stands, counting from nought - the original's <c>FUN_004ddf50</c>, whose own
	/// assertion reads "GetPositionInQueue: Could not fi[nd]".
	/// </summary>
	[TestMethod]
	public void EveryGuestKnowsWhereTheyStandInTheQueue()
	{
		var park = Park();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );
		park.JoinQueue( Ride, 9 );

		Assert.AreEqual( 0, park.PositionInQueue( Ride, 7 ), "the head" );
		Assert.AreEqual( 1, park.PositionInQueue( Ride, 8 ) );
		Assert.AreEqual( 2, park.PositionInQueue( Ride, 9 ), "and the last" );

		Assert.AreEqual( -1, park.PositionInQueue( Ride, 99 ), "somebody who is not in it at all" );
		Assert.AreEqual( -1, park.PositionInQueue( 14, 7 ), "nor standing in another object's queue" );
	}

	/// <summary>
	/// <b>The fact the whole step-up exists for: leaving a queue renumbers nobody.</b>
	///
	/// <para>
	/// Neither this nor the original's <c>FUN_004ddd20</c> touches anybody's <c>mQueuePos</c> - both only
	/// unlink and fix the head. So the place a guest was handed when they joined goes stale the moment
	/// somebody in front of them boards, and <see cref="ParkState.PositionInQueue"/> is the only thing
	/// that knows the truth. <b>Leaving that uncompared would stop every queue in the park dead after
	/// one rider</b>, because <see cref="ParkRideOperation.Invite"/> only calls forward a head whose place is
	/// nought.
	/// </para>
	/// </summary>
	[TestMethod]
	public void LeavingRenumbersNobodyWhichIsWhyThePlaceHasToBeRecomputed()
	{
		var park = Park();

		var handed = new[]
		{
			park.JoinQueue( Ride, 7 ), park.JoinQueue( Ride, 8 ), park.JoinQueue( Ride, 9 )
		};

		CollectionAssert.AreEqual( new[] { 0, 1, 2 }, handed, "the places they joined with" );

		Assert.IsTrue( park.LeaveQueue( Ride, 7 ), "the head is called forward and comes out" );

		// What the links now say, which is what the guest's own turn has to read.
		Assert.AreEqual( 0, park.PositionInQueue( Ride, 8 ), "8 is the head now" );
		Assert.AreEqual( 1, park.PositionInQueue( Ride, 9 ) );

		// And the anti-vacuity half: the number 8 was HANDED on joining is still 1, so a guest who
		// trusted it would never be invited again.
		Assert.AreEqual( 1, handed[1], "the place 8 was handed on joining has not moved" );
	}

	/// <summary>A queue nobody has joined is empty rather than throwing.</summary>
	[TestMethod]
	public void AQueueNobodyHasJoinedIsEmpty()
	{
		var park = Park();

		Assert.AreEqual( 0, park.QueueLength( Ride ) );
		Assert.AreEqual( 0, park.FirstInQueue( Ride ) );
		Assert.AreEqual( 0, park.NextInQueue( 7 ) );
	}
}
