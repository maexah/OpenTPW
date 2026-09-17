using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which objects a guest may be offered - <see cref="ParkRideChoice"/>, the gate the original puts in
/// front of scoring a candidate.
///
/// <para>
/// <b>The interesting result is that the answer is two, not six.</b> Six objects carry the "a guest may
/// choose this" bit, but four of them declare no queue cells at all - and the original's own queue test is
/// <c>length &lt; cells * 4</c>, which nought cells can never pass. So the shipped park can offer a guest
/// exactly the sideshow and the ride. That falls out of the arithmetic rather than being put in, and it is
/// the sort of thing worth pinning before anything is built on top of it.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideChoiceTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private const int JungleSpray = 14;

	private const int BellyBounce = 13;

	/// <summary>
	/// What the shipped park can actually offer, walked from the header's own list head.
	/// </summary>
	[TestMethod]
	public void OnlyTheSideshowAndTheRideCanBeOfferedBecauseTheRestHaveNoQueue()
	{
		var world = Park();
		var offerable = ParkRideChoice.Offerable( world );

		CollectionAssert.AreEquivalent( new[] { JungleSpray, BellyBounce },
			offerable.Select( o => o.ThingId ).ToArray(),
			"the two objects that declare queue cells" );

		// Anti-vacuity: six objects pass the "may be chosen" bit, so the filter is doing more than
		// repeating it - it is the queue term that removes the other four.
		Assert.AreEqual( 6, world.Objects.Count( o => o.IsVisitable ), "six may be chosen at all" );
		Assert.AreEqual( 4, world.Objects.Count( o => o.IsVisitable && o.QueueSizeInCells == 0 ),
			"and four of those declare no queue" );
	}

	/// <summary>
	/// The queue term itself: a queue fills at four per cell, and the object stops being offered exactly
	/// when it is full rather than one either side of it.
	/// </summary>
	[TestMethod]
	public void AQueueHoldsFourPerCellAndTheObjectIsRefusedWhenItIsFull()
	{
		var ride = Park().Objects.Single( o => o.ThingId == BellyBounce );

		Assert.AreEqual( 4, ride.QueueSizeInCells, "the ride's queue is four cells" );

		var room = ride.QueueSizeInCells * ParkRideChoice.QueueRoomPerCell;

		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, room - 1 ), "one short of full is still offered" );
		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride, room ), "exactly full is refused" );
		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride, room + 1 ), "and so is over-full" );
		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, 0 ), "an empty queue is offered" );
	}

	/// <summary>
	/// Each of the other refusals, one at a time, against an object that is otherwise offerable - so that
	/// every arm is shown to matter rather than only the one that happens to fire first.
	/// </summary>
	[TestMethod]
	public void EachRefusalMattersOnItsOwn()
	{
		var ride = Park().Objects.Single( o => o.ThingId == BellyBounce );

		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, 0 ), "the ride is offerable to begin with" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { Flags = 0 }, 0 ),
			"an object a guest may not choose is refused" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { State = ParkRideChoice.StateRefusedOne }, 0 ),
			"and one in the first out-of-service state" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { State = ParkRideChoice.StateRefusedFour }, 0 ),
			"and the second" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { CanLoad = 0 }, 0 ),
			"and one that cannot load anybody" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { EntryPos = 0 }, 0 ),
			"and one with nowhere to be approached from" );

		// State 3 is what most of this park's objects carry and it is NOT a refusal, which is what keeps
		// the state test from being "anything but nought".
		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride with { State = 3 }, 0 ),
			"state three is not one of the two that take an object out of service" );
	}

	/// <summary>A park with nothing loaded offers nothing, rather than throwing.</summary>
	[TestMethod]
	public void NoParkOffersNothing()
		=> Assert.AreEqual( 0, ParkRideChoice.Offerable( null ).Count );
}
