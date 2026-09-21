using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The fourth bit of a catalogue object's <c>mFlags</c> - whether its queue is laid out on the ground.
///
/// <para>
/// <b>This file exists because it was written to prove something and disproved it instead.</b> The
/// prediction was that the bit would agree object for object with
/// <see cref="ParkWorld.CatalogueObject.QueueSizeInCells"/>, since that count is itself produced by
/// walking the queue path (<c>FUN_004de130</c>) - two readings by completely different routes, one a bit
/// in a byte at offset 58 and the other an integer at 1062 reached through the ring arithmetic. They do
/// NOT agree. They disagree on exactly one object, and that object is the finding.
/// </para>
/// <para>
/// <b>The Jungle Spray declares ONE queue cell and does not carry the bit.</b> Only the ride does. That
/// fits what the original does with it: <c>FUN_004de7e0</c> branches on the bit, walking the path when it
/// is set and otherwise standing people <em>inside</em> the back-of-queue cell - a "virtual queue" it
/// asserts is under four deep, with the line "Virtual queue problem!". Both objects have a non-zero
/// <c>mBackOfQueue</c>, so the bit is not "has a queue"; it marks a queue with a PATH, and a single cell
/// is evidently not one. <b>That last step is the interpretation and it is not proven</b> - one park with
/// one flagged object cannot separate "more than one cell" from any other rule that happens to pick the
/// ride. What IS measured is below.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkQueuePathTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	/// <summary>
	/// <b>The discriminating case, and the one that refuted the prediction.</b> The sideshow has a queue
	/// cell and no path bit; the ride has both.
	/// </summary>
	[TestMethod]
	public void OnlyTheRideHasAQueuePathAndTheSideshowIsWhatSeparatesTheTwoReadings()
	{
		var objects = Park().Objects;

		var withPath = objects.Where( o => o.HasQueuePath ).Select( o => o.ThingId ).ToArray();

		CollectionAssert.AreEquivalent( new[] { BellyBounce }, withPath,
			"exactly one object in this park has a queue laid out on the ground" );

		var ride = objects.Single( o => o.ThingId == BellyBounce );
		var sideshow = objects.Single( o => o.ThingId == JungleSpray );

		Assert.AreEqual( 4, ride.QueueSizeInCells, "the ride's queue is four cells" );
		Assert.IsTrue( ride.HasQueuePath, "and it has the bit" );

		// The whole point: one queue cell, no bit. A reading that treated the bit as "declares queue
		// cells" would have this object the wrong way round and nothing else would notice.
		Assert.AreEqual( 1, sideshow.QueueSizeInCells, "the sideshow declares one queue cell" );
		Assert.IsFalse( sideshow.HasQueuePath, "and does NOT have the bit" );

		// Both have somewhere for a queue to end, so the bit is not simply "has a queue".
		Assert.AreNotEqual( 0, ride.BackOfQueue, "the ride has a back of queue" );
		Assert.AreNotEqual( 0, sideshow.BackOfQueue, "and so does the sideshow" );
	}

	/// <summary>
	/// A path implies a guest can be sent there, but not the other way round - the sideshow is offerable
	/// with no path at all.
	/// </summary>
	[TestMethod]
	public void EveryObjectWithAPathIsOfferableButNotEveryOfferableObjectHasOne()
	{
		var world = Park();

		var offerable = ParkRideChoice.Offerable( world ).Select( o => o.ThingId ).ToHashSet();
		var withPath = world.Objects.Where( o => o.HasQueuePath ).Select( o => o.ThingId ).ToArray();

		foreach ( var id in withPath )
			Assert.IsTrue( offerable.Contains( id ), $"thing {id} has a path but cannot be offered" );

		// And the converse fails, which is what stops this collapsing into "the two are the same set".
		Assert.IsTrue( offerable.Contains( JungleSpray ), "the sideshow can be offered" );
		Assert.IsFalse( world.Objects.Single( o => o.ThingId == JungleSpray ).HasQueuePath,
			"and it still has no path" );

		// Six, not two - see ParkRideChoiceTests for why the four that declare no queue cells in their own
		// record are still offered. The point this test makes is unchanged: having a PATH is a stricter
		// thing than being offerable, and exactly one object has one.
		Assert.AreEqual( 6, offerable.Count, "all six choosable objects can be offered" );
		Assert.AreEqual( 1, withPath.Length, "and only one of them has a path" );
	}

	/// <summary>
	/// The bit is its own bit rather than a restatement of one already read, which a misread byte could
	/// easily make it look like.
	/// </summary>
	[TestMethod]
	public void TheQueuePathBitIsNotOneOfTheOthersUnderAnotherName()
	{
		var objects = Park().Objects;

		Assert.AreEqual( 1, objects.Count( o => o.HasQueuePath ), "one has a path" );
		Assert.AreEqual( 3, objects.Count( o => o.IsToilet ), "three are toilets" );
		Assert.AreEqual( 1, objects.Count( o => o.IsRestArea ), "one is a rest area" );
		Assert.AreEqual( 6, objects.Count( o => o.IsVisitable ), "six may be chosen" );

		// Same count as the rest area, so the counts alone cannot tell those two apart - the objects can.
		Assert.IsFalse( objects.Any( o => o.IsRestArea && o.HasQueuePath ),
			"the Staff Room has no queue path, though exactly one object of each exists" );

		Assert.IsFalse( objects.Any( o => o.IsToilet && o.HasQueuePath ),
			"a toilet queue is virtual in this park" );
	}
}
