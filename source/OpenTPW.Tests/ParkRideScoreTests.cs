using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a candidate is worth to a guest - <see cref="ParkRideScore"/>, the original's
/// <c>FUN_004fcc30</c>.
///
/// <para>
/// <b>The tests that carry this file are the ones about the two weights that vanish.</b> A weighted mean
/// is easy to write in a way that looks right and is wrong at the edges: the queue term and its weight
/// both fall away beyond three cells, and the excitement weight falls away for an item with no excitement
/// declared - so the <em>divisor</em> changes, not just the numerator. A build that kept the weights fixed
/// would agree with this one everywhere except exactly there.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideScoreTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue.Item Item( int catalogueId )
	{
		Assert.IsTrue( new ParkItemCatalogue( "jungle", data ).TryGet( catalogueId, out var item ) );

		return item;
	}

	/// <summary>
	/// The catalogue entry a placed object actually points at. Tests go through this rather than naming a
	/// catalogue number beside a thing id, so nothing here can quietly describe a different item than the
	/// one it scores.
	/// </summary>
	private ParkItemCatalogue.Item ItemFor( ParkWorld.CatalogueObject placed ) => Item( placed.CatalogueId );

	private ParkWorld.CatalogueObject Ride() => Park().Objects.Single( o => o.ThingId == 13 );

	/// <summary>Every weight and multiplier comes from the balance file, which documents them itself.</summary>
	[TestMethod]
	public void TheWeightsAreTheBalanceFilesOwn()
	{
		var score = new ParkRideScore( Balance() );

		Assert.AreEqual( 1, score.DistanceWeight, "DecisionVarDistWeight" );
		Assert.AreEqual( 1, score.QueueWeight, "DecisionVarQueueWeight" );
		Assert.AreEqual( 1, score.ExcitementWeight, "DecisionVarExcitementWeight" );
		Assert.AreEqual( 2, score.ThirstWeight, "DecisionVarThirstWeight" );
		Assert.AreEqual( 2, score.HungerWeight, "DecisionVarHungerWeight" );
		Assert.AreEqual( 2, score.ToiletWeight, "DecisionVarToiletWeight" );
		Assert.AreEqual( 2, score.IllnessWeight, "DecisionVarIllnessWeight" );

		Assert.AreEqual( 7, score.NewForDays, "DecisionVariable1 - days before a ride is not new" );
		Assert.AreEqual( 5, score.NewRideMultiplier, "DecisionVariable2" );
		Assert.AreEqual( 5, score.IndoorInRainMultiplier, "DecisionVariable3" );

		// The eight kinds of guest want different things, which is what makes the excitement term mean
		// anything at all.
		Assert.AreEqual( 80, score.PreferredExcitementFor( 0 ), "PeepTypes[0].PreferredExcitement" );
		Assert.AreEqual( 35, score.PreferredExcitementFor( 3 ), "PeepTypes[3]" );
		Assert.AreEqual( 45, score.PreferredExcitementFor( 6 ), "PeepTypes[6]" );
	}

	/// <summary>Both tables are what the image holds, at their ends and in the middle.</summary>
	[TestMethod]
	public void BothLookupTablesAreTheOnesInTheExecutable()
	{
		// The need table is nought wherever either side is small and a hundred at its far corner.
		Assert.AreEqual( 0, ParkRideScore.NeedMatch( 0f, 0 ), "no need, no effect" );
		Assert.AreEqual( 0, ParkRideScore.NeedMatch( 100f, 0 ), "a great need and no effect is still nothing" );
		Assert.AreEqual( 100, ParkRideScore.NeedMatch( 100f, 100 ), "a great need fully met" );
		Assert.AreEqual( 40, ParkRideScore.NeedMatch( 80f, 40 ), "a middling case, from the table itself" );

		// The relief table is flat until the need passes forty, then climbs steeply.
		Assert.AreEqual( 0, ParkRideScore.ReliefMatch( 0f ) );
		Assert.AreEqual( 0, ParkRideScore.ReliefMatch( 40f ), "still nothing at forty" );
		Assert.AreEqual( 1, ParkRideScore.ReliefMatch( 45f ), "and then it starts" );
		Assert.AreEqual( 100, ParkRideScore.ReliefMatch( 100f ), "desperate" );
	}

	/// <summary>
	/// Distance: a candidate underfoot is worth a hundred, one 450 squared-cells away is worth nothing,
	/// and the effects count divides whatever is left.
	/// </summary>
	[TestMethod]
	public void DistanceFallsAwayOverFourHundredAndFiftySquaredCells()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );

		int At( int distanceSquared, int effects = 0 ) => score.Of(
			new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ),
			new ParkRideScore.Candidate( ride, item, distanceSquared, 0, effects, 999, false, 40 ) );

		var underfoot = At( 0 );
		var farOff = At( ParkRideScore.DistanceDivisor );

		Assert.IsTrue( underfoot > farOff, "standing on it beats being far from it" );
		Assert.AreEqual( 0, At( 100000 ) - At( ParkRideScore.DistanceDivisor ),
			"and past the divisor it cannot get any worse" );

		// The effects count divides the distance term, so a cell full of them is worth less.
		Assert.IsTrue( At( 0, effects: 4 ) < underfoot, "a cell with effects scores lower" );
	}

	/// <summary>
	/// <b>The queue term and its weight both vanish beyond three cells.</b> A build that kept the weight
	/// fixed would still pass every other test here.
	/// </summary>
	[TestMethod]
	public void TheQueueStopsCountingAtAllOnceTheGuestIsNotClose()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );

		int WithQueue( int distanceSquared, int queueLength ) => score.Of(
			new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ),
			new ParkRideScore.Candidate( ride, item, distanceSquared, queueLength, 0, 999, false, 40 ) );

		// Close by, the queue's length changes the answer.
		Assert.AreNotEqual( WithQueue( 0, 0 ), WithQueue( 0, 12 ),
			"within three cells a long queue is worth less than an empty one" );

		// Beyond it, the queue is not consulted at all - so its length cannot matter.
		Assert.AreEqual( WithQueue( ParkRideScore.QueueMattersWithin, 0 ),
			WithQueue( ParkRideScore.QueueMattersWithin, 12 ),
			"at exactly nine squared cells the queue has stopped counting" );
	}

	/// <summary>
	/// Excitement is a match rather than a maximum: a guest who likes gentle things scores a gentle ride
	/// higher than a wild one, and the guest who likes wild ones does the opposite.
	/// </summary>
	[TestMethod]
	public void ExcitementIsAMatchAndNotAMaximum()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );

		int For( int personType, int excitement ) => score.Of(
			new ParkRideScore.Wants( personType, 0f, 0f, 0f, 0f ),
			new ParkRideScore.Candidate( ride, item, 0, 0, 0, 999, false, excitement ) );

		// Kind 0 prefers 80, kind 3 prefers 35.
		Assert.IsTrue( For( 0, 80 ) > For( 0, 35 ), "the thrill-seeker prefers the wilder ride" );
		Assert.IsTrue( For( 3, 35 ) > For( 3, 80 ), "and the timid guest the gentler one" );
	}

	/// <summary>
	/// A new attraction is worth five times an old one, and the boundary is the balance file's own seven
	/// days - so the day either side of it is where a wrong comparison shows.
	/// </summary>
	[TestMethod]
	public void ANewAttractionIsWorthFiveTimesAnOldOneAndTheBoundaryIsSeven()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );

		int AtAge( int days ) => score.Of(
			new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ),
			new ParkRideScore.Candidate( ride, item, 0, 0, 0, days, false, 80 ) );

		Assert.AreEqual( AtAge( 8 ) * score.NewRideMultiplier, AtAge( 7 ),
			"seven days old is still new, and new is worth five times" );
		Assert.AreEqual( AtAge( 8 ), AtAge( 100 ), "past the boundary the age stops mattering" );
	}

	/// <summary>
	/// Shelter counts only while it rains, and only for something indoors - the Drinks Shop is indoors and
	/// the ride is not.
	/// </summary>
	[TestMethod]
	public void ShelterIsWorthSomethingOnlyWhileItRains()
	{
		var score = new ParkRideScore( Balance() );
		var shop = Park().Objects.Single( o => o.ThingId == 16 );
		var shopItem = ItemFor( shop );

		Assert.AreEqual( 1203, shop.CatalogueId, "thing 16 is the Drinks Shop" );

		int Weather( bool raining ) => score.Of(
			new ParkRideScore.Wants( 0, 60f, 0f, 0f, 0f ),
			new ParkRideScore.Candidate( shop, shopItem, 0, 0, 0, 999, raining, 0 ) );

		Assert.IsTrue( shopItem.IsIndoors, "the Drinks Shop is shelter" );
		Assert.AreEqual( Weather( false ) * score.IndoorInRainMultiplier, Weather( true ),
			"and in the rain it is worth the multiplier" );

		var ride = Ride();
		var rideItem = ItemFor( ride );

		Assert.IsFalse( rideItem.IsIndoors, "the ride is not shelter" );
		Assert.AreEqual(
			score.Of( new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ),
				new ParkRideScore.Candidate( ride, rideItem, 0, 0, 0, 999, false, 80 ) ),
			score.Of( new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ),
				new ParkRideScore.Candidate( ride, rideItem, 0, 0, 0, 999, true, 80 ) ),
			"so rain changes nothing for it" );
	}

	/// <summary>
	/// Anything ridden lately is worth progressively less - a fifth, a quarter, a third, a half - and
	/// anything not in the history is untouched.
	/// </summary>
	[TestMethod]
	public void SomethingRiddenLatelyIsWorthLess()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );

		var wants = new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f );
		var candidate = new ParkRideScore.Candidate( ride, item, 0, 0, 0, 999, false, 80 );

		var fresh = score.Of( wants, candidate );

		Assert.AreEqual( fresh / 5, score.Of( wants, candidate, [ride.ThingId] ), "just ridden" );
		Assert.AreEqual( fresh / 4, score.Of( wants, candidate, [0, ride.ThingId] ), "one before that" );
		Assert.AreEqual( fresh / 3, score.Of( wants, candidate, [0, 0, ride.ThingId] ), "and the one before" );
		Assert.AreEqual( fresh / 2, score.Of( wants, candidate, [0, 0, 0, ride.ThingId] ), "and the fourth" );

		Assert.AreEqual( fresh, score.Of( wants, candidate, [0, 0, 0, 0, ride.ThingId] ),
			"past the fourth it is forgotten" );
		Assert.AreEqual( fresh, score.Of( wants, candidate, [1, 2, 3, 4] ), "and other rides do not count" );
	}

	/// <summary>
	/// A toilet is worth something only to a guest who needs one, and the same flag drives the illness
	/// term - which is what the original does, tested here so the reproduction is visible rather than
	/// assumed.
	/// </summary>
	[TestMethod]
	public void AToiletIsWorthSomethingOnlyToAGuestWhoNeedsOne()
	{
		var score = new ParkRideScore( Balance() );
		var toilet = Park().Objects.First( o => o.IsToilet );
		var item = ItemFor( toilet );

		Assert.AreEqual( 1402, toilet.CatalogueId, "the park's toilets are the Small Toilet" );

		// Its category file says ProvidesRelief 0 with the comment "set to 1 for toilets", and the item's
		// own file is what sets it - so this is the two-level read working, not a default.
		Assert.IsTrue( item.ProvidesRelief, "and the item file overrides its category's nought" );

		int Needing( float toiletNeed ) => score.Of(
			new ParkRideScore.Wants( 0, 0f, 0f, toiletNeed, 0f ),
			new ParkRideScore.Candidate( toilet, item, 0, 0, 0, 999, false, 0 ) );

		Assert.IsTrue( Needing( 90f ) > Needing( 10f ), "a desperate guest values it more" );
		Assert.AreEqual( Needing( 0f ), Needing( 40f ), "and below forty the table is flat" );
	}
}
