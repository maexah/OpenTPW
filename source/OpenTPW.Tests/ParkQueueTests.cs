using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The queue itself - <c>mQNext</c> and <c>mQPrev</c>, and the walk that counts along them.
///
/// <para>
/// <b>The park cannot test this, which is why the walk takes its link lookup as a parameter.</b> Every
/// queue in Lost Kingdom is empty - its <c>mFirstInQ</c> is nought on every object, because nobody has
/// ever been admitted - so any assertion made against the save alone would pass just as happily against a
/// method that returned nought and walked nothing at all. The synthetic chains below are the ones that
/// actually exercise it; the park's own zeros are asserted separately, together with the reason they are
/// zeros.
/// </para>
/// <para>
/// <b>Finding the field at all turned on its name.</b> Four sweeps of the executable's serialised field
/// names - <c>InQ</c>, <c>mNext</c>, <c>Queue</c>, <c>mPrev</c> - found no per-guest queue link, and "it
/// is not in the save" was nearly recorded as the answer. It is called <c>mQNext</c>, which none of those
/// reaches; reading the guest serialiser's whole field list is what found it.
/// </para>
/// </summary>
[TestClass]
public class ParkQueueTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>A chain as a lookup, ending at whatever is not in it.</summary>
	private static System.Func<int, int?> Chain( Dictionary<int, int> links )
		=> id => links.TryGetValue( id, out var next ) ? next : null;

	/// <summary>
	/// <b>The load-bearing test.</b> Three guests one behind another are counted as three.
	/// </summary>
	[TestMethod]
	public void AQueueOfThreeCountsAsThree()
	{
		var queue = Chain( new Dictionary<int, int> { [7] = 8, [8] = 9, [9] = 0 } );

		Assert.AreEqual( 3, ParkRideChoice.QueueLengthFrom( 7, queue, guests: 13 ) );

		// And the same chain entered part way along is shorter, which is what says the walk is following
		// the links rather than counting the dictionary.
		Assert.AreEqual( 2, ParkRideChoice.QueueLengthFrom( 8, queue, guests: 13 ), "from the second" );
		Assert.AreEqual( 1, ParkRideChoice.QueueLengthFrom( 9, queue, guests: 13 ), "from the last" );
	}

	/// <summary>Nobody at the head of it means nobody in it.</summary>
	[TestMethod]
	public void AnEmptyQueueIsNought()
		=> Assert.AreEqual( 0,
			ParkRideChoice.QueueLengthFrom( 0, Chain( new Dictionary<int, int> { [7] = 0 } ), guests: 13 ) );

	/// <summary>
	/// A queue whose head names something that is not a guest ends there rather than throwing - the
	/// original's own "Could not find the thing in the queue" case.
	/// </summary>
	[TestMethod]
	public void AHandleThatNamesNobodyEndsTheWalk()
		=> Assert.AreEqual( 0,
			ParkRideChoice.QueueLengthFrom( 5, Chain( new Dictionary<int, int> { [7] = 8 } ), guests: 13 ) );

	/// <summary>
	/// <b>A circular chain stops instead of hanging.</b> Two guests pointing at each other is not a queue
	/// any park should contain, but a walk with no bound would spin on it for ever, and a test suite that
	/// hangs tells you far less than one that fails.
	/// </summary>
	[TestMethod]
	public void ACircularQueueIsBoundedRatherThanEndless()
	{
		var length = ParkRideChoice.QueueLengthFrom( 1,
			Chain( new Dictionary<int, int> { [1] = 2, [2] = 1 } ), guests: 2 );

		Assert.IsTrue( length <= 3, $"the walk stopped, at {length}" );
	}

	/// <summary>
	/// Every queue in the shipped park is empty - and the anti-vacuity half is that this is a fact about
	/// the park, not about the walk.
	/// </summary>
	[TestMethod]
	public void EveryQueueInTheShippedParkIsEmptyBecauseNobodyHasEverBeenAdmitted()
	{
		var park = Park();

		foreach ( var item in park.Objects )
			Assert.AreEqual( 0, ParkRideChoice.QueueLength( park, item ), $"thing {item.ThingId}" );

		Assert.IsTrue( park.Objects.All( o => o.FirstInQueue == 0 ),
			"nothing names a first guest, which is WHY the lengths are nought" );

		Assert.AreEqual( 0, park.NumberOfVisitorsToDate, "and nobody has ever been admitted" );
	}

	/// <summary>
	/// Both links are read, and they sit beside fields that still read what they always did - which is
	/// what says the two new offsets did not disturb their neighbours.
	/// </summary>
	[TestMethod]
	public void BothQueueLinksAreReadAndTheirNeighboursAreUndisturbed()
	{
		var guests = Park().People.Where( p => p.Guest != null ).ToArray();

		Assert.AreEqual( 13, guests.Length, "the park's guests" );

		foreach ( var person in guests )
		{
			var guest = person.Guest!.Value;

			Assert.AreEqual( 0, guest.QNext, $"guest {person.ThingId} mQNext" );
			Assert.AreEqual( 0, guest.QPrev, $"guest {person.ThingId} mQPrev" );

			// mQueuePos at 494 and mSavedState at 501 bracket the new reads at 486 and 488, and both
			// still carry what they carried before.
			Assert.AreEqual( 0, guest.QueuePos, $"guest {person.ThingId} mQueuePos" );
			Assert.AreEqual( ParkWorld.GuestState.Deciding, guest.SavedState,
				$"guest {person.ThingId} mSavedState" );
		}
	}

	/// <summary>A park with nothing behind it has no queues, rather than throwing.</summary>
	[TestMethod]
	public void NoParkHasNoQueue()
		=> Assert.AreEqual( 0, ParkRideChoice.QueueLength( null, default ) );
}
