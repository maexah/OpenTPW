using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest actually sets off for - <see cref="ParkRideChooser"/>, the original's <c>FUN_004fcb10</c>.
///
/// <para>
/// <b>The load-bearing test here is the one about what is NOT chosen.</b> Six of the shipped park's
/// objects carry the "a guest may choose this" bit, but four declare no queue cells and the original's own
/// queue test - <c>length &lt; cells * 4</c> - can never be passed by nought cells. So a chooser that
/// scored every visitable object would happily send guests to the Drinks Shop and the three toilets, and
/// would look entirely reasonable doing it. Pinning the answer at two is what tells the two builds apart.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideChooserTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue Catalogue() => new( "jungle", data );

	private ParkRideChooser Chooser( ParkWorld? park, bool described = true )
		=> new( park, described ? Catalogue() : null, new ParkRideScore( new ParkBalance( "jungle", true ) ) );

	private const int JungleSpray = 14;

	private const int BellyBounce = 13;

	/// <summary>A guest with nothing wrong with them, so that the distance terms are what move the answer.</summary>
	private static ParkRideScore.Wants Guest( int personType = 0 )
		=> new( personType, 0f, 0f, 0f, 0f );

	/// <summary>
	/// Whatever a guest is offered, it is one of the two objects with a queue - never the shop and never a
	/// toilet, however close they are standing to one.
	/// </summary>
	[TestMethod]
	public void OnlyTheTwoThingsWithAQueueAreEverOffered()
	{
		var park = Park();
		var chooser = Chooser( park );
		var offered = new System.Collections.Generic.HashSet<int>();

		// From the entry cell of every visitable object in the park, including the four that cannot be
		// offered - so the test stands a guest right on top of the tempting ones.
		foreach ( var from in park.Objects.Where( o => o.IsVisitable ) )
		{
			if ( chooser.ChooseFor( Guest(), from.EntryCellX, from.EntryCellY, gameTick: 0 ) is { } chosen )
				offered.Add( chosen.ThingId );
		}

		CollectionAssert.AreEquivalent( new[] { JungleSpray, BellyBounce }, offered.ToArray(),
			"the sideshow and the ride, and nothing else" );

		// Anti-vacuity: the guest really was stood next to things that were passed over.
		Assert.AreEqual( 6, park.Objects.Count( o => o.IsVisitable ),
			"six objects were candidates by the choosable bit alone" );
	}

	/// <summary>
	/// Distance is what settles it between the two: a guest standing at one object's own entry cell is
	/// offered that one.
	/// </summary>
	[TestMethod]
	public void AGuestIsOfferedTheThingTheyAreStandingAt()
	{
		var park = Park();
		var chooser = Chooser( park );

		var ride = park.Objects.Single( o => o.ThingId == BellyBounce );
		var sideshow = park.Objects.Single( o => o.ThingId == JungleSpray );

		Assert.AreEqual( BellyBounce,
			chooser.ChooseFor( Guest(), ride.EntryCellX, ride.EntryCellY, gameTick: 0 )?.ThingId,
			"standing at the ride's entry" );

		Assert.AreEqual( JungleSpray,
			chooser.ChooseFor( Guest(), sideshow.EntryCellX, sideshow.EntryCellY, gameTick: 0 )?.ThingId,
			"and standing at the sideshow's" );

		// Anti-vacuity: the two entry cells are not the same place, so the answer above actually moved.
		Assert.AreNotEqual( (ride.EntryCellX, ride.EntryCellY), (sideshow.EntryCellX, sideshow.EntryCellY),
			"the two are approached from different cells" );
	}

	/// <summary>
	/// <b>The filter really is consulted.</b> Fill every queue and nothing may be offered at all - which a
	/// chooser that scored candidates without asking <see cref="ParkRideChoice"/> would fail.
	/// </summary>
	[TestMethod]
	public void NothingIsOfferedOnceEveryQueueIsFull()
	{
		var park = Park();
		var chooser = Chooser( park );
		var ride = park.Objects.Single( o => o.ThingId == BellyBounce );

		var full = ride.QueueSizeInCells * ParkRideChoice.QueueRoomPerCell;

		Assert.IsNotNull( chooser.ChooseFor( Guest(), ride.EntryCellX, ride.EntryCellY, 0 ),
			"somewhere is offered while the queues are empty" );

		Assert.IsNull(
			chooser.ChooseFor( Guest(), ride.EntryCellX, ride.EntryCellY, 0, queueLength: _ => full ),
			"and nowhere once they are full" );
	}

	/// <summary>
	/// A park with nothing behind it offers nothing, rather than throwing - the same answer
	/// <see cref="ParkRideChoice.Offerable"/> gives.
	/// </summary>
	[TestMethod]
	public void NoParkOffersNothing()
		=> Assert.IsNull( Chooser( null ).ChooseFor( Guest(), 40, 40, 0 ) );

	/// <summary>
	/// With no catalogue a guest can still be offered somewhere - the scoring falls back to distance and
	/// queue, which is what keeps a park with unreadable items moving rather than freezing.
	/// </summary>
	[TestMethod]
	public void AGuestCanStillBeOfferedSomewhereWithNothingDescribingIt()
	{
		var park = Park();
		var ride = park.Objects.Single( o => o.ThingId == BellyBounce );

		var chosen = Chooser( park, described: false )
			.ChooseFor( Guest(), ride.EntryCellX, ride.EntryCellY, gameTick: 0 );

		Assert.IsNotNull( chosen, "somewhere is still offered" );
		Assert.IsTrue( chosen.Value.ThingId is JungleSpray or BellyBounce, "and it is one of the two" );
	}

	/// <summary>
	/// The newness multiplier is reachable and is worth five times - shown through the injected age,
	/// because the shipped park's own build dates cannot answer it (see
	/// <see cref="ParkRideChooser"/>'s remarks).
	/// </summary>
	[TestMethod]
	public void SomethingBuiltThisWeekOutscoresTheSameThingBuiltLongAgo()
	{
		var park = Park();
		var score = new ParkRideScore( new ParkBalance( "jungle", true ) );
		var ride = park.Objects.Single( o => o.ThingId == BellyBounce );

		Assert.IsTrue( Catalogue().TryGet( ride.CatalogueId, out var item ) );

		var candidate = new ParkRideScore.Candidate( ride, item, 0, 0, 0, ParkRideChooser.NotNew, false, 40 );

		var old = score.Of( Guest(), candidate );
		var built = score.Of( Guest(), candidate with { DaysOld = score.NewForDays } );

		Assert.AreEqual( old * score.NewRideMultiplier, built, "a new attraction is worth five times" );

		// And the default the chooser uses really is past the boundary, rather than merely large.
		Assert.IsTrue( ParkRideChooser.NotNew > score.NewForDays,
			"the age used when nothing can date a thing counts as old" );
	}
}
