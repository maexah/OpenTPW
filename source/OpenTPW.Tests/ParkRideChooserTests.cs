using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest actually sets off for - <see cref="ParkRideChooser"/>, the original's <c>FUN_004fcb10</c>.
///
/// <para>
/// <b>The load-bearing test here is the one about what is NOT chosen.</b> Six of the shipped park's
/// objects carry the "a guest may choose this" bit and all six pass <see cref="ParkRideChoice"/>, but
/// toilets 21 and 22 are never the best candidate from any of the six entry cells: from each toilet's cell
/// all three tie on distance, and 23, walked first, keeps the tie at game tick 0. Pinning the answer at
/// four is what tells the scorer from the filter.
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

	private const int DrinksShop = 16;

	/// <summary>
	/// What a guest is actually offered, standing at each visitable thing in turn.
	///
	/// <para>
	/// The filter walks a queue off the map - see
	/// <see cref="ParkRideChoice.QueueCellsFor"/> - so the Drinks Shop is chosen when a guest is standing by
	/// it. <b>Measured rather than predicted:</b> the answer is four of the six, not all six. Toilets 21 and
	/// 22 are never the best candidate from any of these six cells: the three toilets stand in a row, (55,15)
	/// to (55,17), so from each toilet's cell all three score a full hundred on distance, and 23, walked
	/// first, keeps the tie at game tick 0. That is the scorer working, not the filter.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheShopIsAmongWhatAGuestIsOfferedAndTwoOfTheToiletsAreNot()
	{
		var park = Park();
		var chooser = Chooser( park );
		var offered = new System.Collections.Generic.HashSet<int>();

		// From the entry cell of every visitable object in the park, including the two toilets that are
		// never chosen - so the test stands a guest right on top of the tempting ones.
		foreach ( var from in park.Objects.Where( o => o.IsVisitable ) )
		{
			if ( chooser.ChooseFor( Guest(), from.EntryCellX, from.EntryCellY, gameTick: 0 ) is { } chosen )
				offered.Add( chosen.ThingId );
		}

		CollectionAssert.AreEquivalent( new[] { BellyBounce, JungleSpray, DrinksShop, 23 }, offered.ToArray(),
			"the ride, the sideshow, the drinks shop and the first of the three toilets" );

		// The half that matters for spending, said on its own so a future change cannot quietly drop it
		// back into the set it came from: a guest really is sent to the shop.
		Assert.IsTrue( offered.Contains( DrinksShop ),
			"the Drinks Shop is chosen - this is the whole of PLAYER-GAPS item 8's shop half" );

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
