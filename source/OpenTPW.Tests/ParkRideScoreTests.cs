using System;
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
		Assert.AreEqual( AtAge( 8 ) * score.NewRideMultiplier, AtAge( 0 ), "and so is one built today" );
		Assert.AreEqual( AtAge( 8 ), AtAge( 100 ), "past the boundary the age stops mattering" );
		Assert.AreEqual( AtAge( 8 ), AtAge( -1 ),
			"a stamp a day or more ahead of the calendar is not new: the comparison is unsigned" );
	}

	/// <summary>
	/// The park's calendar starts at 2000-01-01 and runs 3,750 seconds a sweep from the save's <c>mGameTick</c>, so a
	/// thing stamped on one sweep is seven days old 184 sweeps later and eight at 185 - the whole of its window.
	/// Lost Kingdom's Belly Bounce, stamped 2000-01-01 15:37:30, is 32 days old as the park loads on sweep 755.
	/// </summary>
	[TestMethod]
	public void AThingIsNewForOneHundredAndEightyFourSweeps()
	{
		var park = Park();
		var state = new ParkState( park );

		Assert.AreEqual( 755, state.GameTick, "the save's mGameTick" );
		Assert.AreEqual( new DateTime( 2000, 2, 2, 18, 27, 30 ), state.CalendarNow );
		Assert.AreEqual( 32, state.AgeInDays( Ride() ), "the Belly Bounce as the park loads" );

		var stamp = ParkWorld.BuiltWhen.At( state.CalendarNow );

		for ( var sweep = 0; sweep < 184; ++sweep )
			state.AdvanceGameTick();

		Assert.AreEqual( 7, ParkState.AgeInDays( state.CalendarNow, stamp ), "184 sweeps on it is seven days old" );

		state.AdvanceGameTick();

		Assert.AreEqual( 8, ParkState.AgeInDays( state.CalendarNow, stamp ), "and at 185 it is eight" );
		Assert.AreEqual( -1, ParkState.AgeInDays( new DateTime( 2000, 1, 1 ), ParkWorld.BuiltWhen.At(
			new DateTime( 2000, 1, 2, 0, 0, 1 ) ) ), "a day and a second ahead is minus one" );
		Assert.AreEqual( 0, ParkState.AgeInDays( new DateTime( 2000, 1, 1 ), ParkWorld.BuiltWhen.At(
			new DateTime( 2000, 1, 1, 23, 0, 0 ) ) ), "and less than a day ahead is nought" );
		Assert.AreEqual( ParkRideChooser.NotNew, ParkRideChooser.AgeOf( state.CalendarNow,
			new ParkWorld.BuiltWhen( 2000, 13, 40, 0, 0, 0, 0, 0 ) ), "a stamp that makes no date is not new to the score" );
		Assert.AreEqual( ParkRideChooser.NotNew, ParkRideChooser.AgeOf( null, stamp ), "nor is anything with no calendar" );
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
	/// A thing left lately is worth less - the first of the last four visits naming it divides by five, four,
	/// three or two, and only the first - and a thing turned away from is worth less again, every refusal naming
	/// it dividing once more. The same kind as the thing left last is worth nothing at all.
	/// </summary>
	[TestMethod]
	public void ThingsLeftAndThingsRefusedAreWorthLess()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );
		var id = ride.ThingId;

		var wants = new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f );
		var candidate = new ParkRideScore.Candidate( ride, item, 0, 0, 0, 999, false, 80 );

		int Visited( params int[] visits ) => score.Of( wants with { Visits = visits }, candidate );
		int Refused( params int[] refusals ) => score.Of( wants with { Refusals = refusals }, candidate );

		var fresh = score.Of( wants, candidate );

		Assert.AreEqual( 27, fresh, "distance 100, queue 100 and excitement 100 over the eleven weights" );
		Assert.AreEqual( fresh / 5, Visited( id ), "left last" );
		Assert.AreEqual( fresh / 4, Visited( 0, id ), "one before that" );
		Assert.AreEqual( fresh / 3, Visited( 0, 0, id ), "and the one before" );
		Assert.AreEqual( fresh / 2, Visited( 0, 0, 0, id ), "and the fourth" );
		Assert.AreEqual( fresh / 4, Visited( 0, id, id, id ), "only the first visit naming it divides" );
		Assert.AreEqual( fresh, Visited( 1, 2, 3, 4 ), "and other things do not count" );

		Assert.AreEqual( fresh / 5 / 4, Refused( id, id ), "every refusal naming it divides" );
		Assert.AreEqual( fresh / 3 / 2, Refused( 0, 0, id, id ), "by three and two at the back" );
		Assert.AreEqual( fresh / 4 / 5, score.Of( wants with { Visits = [0, id], Refusals = [id] }, candidate ),
			"and the two histories each divide" );

		Assert.AreEqual( 0, score.Of( wants with { LastVisitKind = ride.CatalogueId }, candidate ),
			"the same kind as the thing left last is worth nought" );
		Assert.AreEqual( fresh, score.Of( wants with { LastVisitKind = 1203 }, candidate ), "and another kind is not" );
	}

	/// <summary>
	/// A golden-ticket ride is worth <c>1 + 0.1 (g + 1)</c> times as much, and a ride dearer than 3,000 is worth
	/// <c>1 + cost / 30,000</c> times; anything else is untouched. Jurassic Tours' ticket is 1, Eruption's 3; the
	/// Inca Totem costs 3,250, the Aztec Mayhem 2,500 and the Belly Bounce 500.
	/// </summary>
	[TestMethod]
	public void AGoldenTicketRideAndADearOneAreWorthMore()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		ParkItemCatalogue.Item Named( string stem ) => catalogue.All.Single( item => item.Stem == stem );

		Assert.AreEqual( 1, Named( "tourride" ).GoldenTicketCost );
		Assert.AreEqual( 3, Named( "volcano" ).GoldenTicketCost );
		Assert.AreEqual( 0, Named( "totem" ).GoldenTicketCost );

		Assert.AreEqual( 54, ParkRideScore.Priced( 45, Named( "tourride" ) ), "Jurassic Tours, times 1.2" );
		Assert.AreEqual( 62, ParkRideScore.Priced( 45, Named( "volcano" ) ),
			"Eruption, times 1.4 at double precision: 62.99999999999999" );
		Assert.AreEqual( 83, ParkRideScore.Priced( 75, Named( "totem" ) ), "the Inca Totem, times 1.108" );
		Assert.AreEqual( 75, ParkRideScore.Priced( 75, Named( "tvsim" ) ), "the Aztec Mayhem, at 2,500, is not dear" );
		Assert.AreEqual( 75, ParkRideScore.Priced( 75, Named( "bouncy" ) ), "nor the Belly Bounce" );

		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var wants = new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f );

		Assert.AreEqual( 29, score.Of( wants, new ParkRideScore.Candidate( ride, Named( "totem" ), 0, 0, 0, 999, false, 80 ) ),
			"the mean of 27, times 1.108" );
		Assert.AreEqual( 32, score.Of( wants, new ParkRideScore.Candidate( ride, Named( "tourride" ), 0, 0, 0, 999, false, 80 ) ),
			"and times 1.2" );
		Assert.AreEqual( 0x39aec33e, BitConverter.SingleToInt32Bits( ParkRideScore.ThreeThousandth ), "the float at 0x00700750" );
		Assert.AreEqual( unchecked((int)0xbdcccccd), BitConverter.SingleToInt32Bits( ParkRideScore.MinusATenth ), "0x00700754" );
		Assert.AreEqual( unchecked((int)0xbf4ccccd), BitConverter.SingleToInt32Bits( ParkRideScore.MinusFourFifths ), "0x007005b0" );

		Assert.AreEqual( -1, Named( "bumper" ).BumperType, "the Hot Pot has a track handle" );
		Assert.AreEqual( -4, Named( "gokarts" ).BumperType, "Dino Karts" );
		Assert.AreEqual( -5, Named( "wateride" ).BumperType, "Splish Splash" );
		Assert.AreEqual( 0, Named( "bouncy" ).BumperType, "the Belly Bounce has none" );
	}

	/// <summary>
	/// A sideshow's excitement is worked out from its cost of goods, its price and its chance of winning - the
	/// Jungle Spray's 50, 20 and 25 make 30, not the 35 its file declares - and a ride's is its level times its
	/// speed and duration against its starting ones, each held between 0.75 and 1.25.
	/// </summary>
	[TestMethod]
	public void ASideshowWorksOutItsExcitementAndARideScalesItsOwn()
	{
		var spray = Park().Objects.Single( o => o.ThingId == 14 );
		var sprayItem = ItemFor( spray );

		Assert.AreEqual( 35, sprayItem.ExcitementLevel, "what the file declares" );
		Assert.AreEqual( 20, spray.PricePerUse, "the saved price" );
		Assert.AreEqual( 30, ParkRideScore.ExcitementOf( spray, sprayItem ), "20 + trunc( 0.08 × 25 × √30 )" );
		Assert.AreEqual( 34, ParkRideScore.SideshowExcitement( 50, 0, 25 ), "free, it is 34" );
		Assert.AreEqual( 20, ParkRideScore.SideshowExcitement( 50, 60, 25 ), "and dearer than its prize, 20" );

		var ride = Ride();

		Assert.AreEqual( 40, ParkRideScore.ExcitementOf( ride, ItemFor( ride ) ), "the Belly Bounce at its settings" );
		Assert.AreEqual( 50, ParkRideScore.ExcitementOf( ride with { OperatingSpeed = 75 }, ItemFor( ride ) ),
			"its speed a quarter over its starting 60" );
		Assert.AreEqual( 30, ParkRideScore.ExcitementOf( ride with { OperatingDuration = 15 }, ItemFor( ride ) ),
			"its duration half its starting 30, held at 0.75" );

		int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

		var catalogue = new ParkItemCatalogue( "jungle", data );
		var coaster = catalogue.All.Single( item => item.Stem == "coaster1" );
		var track = Counted( "RIDE_EXCITEMENT_COASTER_TRACK" );

		Assert.AreEqual( 90, ParkRideScore.ExcitementOf( ride with { CatalogueId = coaster.Id }, coaster ),
			"a coaster scores its level" );
		Assert.AreEqual( track + 1, Counted( "RIDE_EXCITEMENT_COASTER_TRACK" ), "and is counted" );
		Assert.AreEqual( 50, ParkRideScore.RatioExcitement( 40, 75, 60, 30, 30 ), "a quarter faster" );
		Assert.AreEqual( 62, ParkRideScore.RatioExcitement( 40, 90, 60, 60, 30 ), "held at 1.25 twice" );
		Assert.AreEqual( 30, ParkRideScore.RatioExcitement( 40, 30, 60, 30, 30 ), "held at 0.75" );
		Assert.AreEqual( 30, ParkRideScore.RatioExcitement( 40, 0, 0, 30, 30 ), "and a ratio that is no number, 0.75" );
	}

	/// <summary>
	/// An upgraded ride divides its speed and its duration by its own tier's starting ones: the Belly Bounce's tiers
	/// start at 60, 75 and 90, each lasting 30, so tier 1 at speed 60 is 32 where tier nought's divisors would make it
	/// 40, and at 75 is 40 where they would make it 50. Past the third tier the original reads the fields after the
	/// array: counted, and scored without the ratios.
	/// </summary>
	[TestMethod]
	public void AnUpgradedRideIsMeasuredAgainstItsOwnTier()
	{
		var ride = Ride();
		var item = ItemFor( ride );

		int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

		Assert.AreEqual( (60, 30), item.StartingAt( 0 ) );
		Assert.AreEqual( (75, 30), item.StartingAt( 1 ), "Rides.sam's tier 1 speed, the item's own duration" );
		Assert.AreEqual( (90, 30), item.StartingAt( 2 ) );

		Assert.AreEqual( 32, ParkRideScore.ExcitementOf( ride with { UpgradeLevel = 1, OperatingSpeed = 60 }, item ) );
		Assert.AreEqual( 40, ParkRideScore.ExcitementOf( ride with { UpgradeLevel = 1, OperatingSpeed = 75 }, item ) );
		Assert.AreEqual( 50, ParkRideScore.ExcitementOf( ride with { UpgradeLevel = 0, OperatingSpeed = 75 }, item ),
			"on tier nought's divisors" );
		Assert.AreEqual( 40, ParkRideScore.ExcitementOf( ride with { UpgradeLevel = 2, OperatingSpeed = 90 }, item ) );

		var past = Counted( "RIDE_EXCITEMENT_UPGRADE_TIER" );

		Assert.AreEqual( 40, ParkRideScore.ExcitementOf( ride with { UpgradeLevel = 3, OperatingSpeed = 80 }, item ),
			"a fourth tier, its level: tier 2's divisors would make it 35, tier 1's 42" );
		Assert.AreEqual( past + 1, Counted( "RIDE_EXCITEMENT_UPGRADE_TIER" ), "counted" );
	}

	/// <summary>
	/// A thing with a track handle starts from 60% of its level plus its track's term: bought, with no track laid, the
	/// Hot Pot is 42, Dino Karts 48 and Splish Splash 45; the played Dino Karts' track adds 3 × 1 + 6 + 2 × 12, so 81.
	/// The arm follows the object's handle, not the item: a Dino Karts record with none is its level, 80, and a stale
	/// handle writes nothing, 48.
	/// </summary>
	[TestMethod]
	public void ATrackRideStartsFromSixtyPercentAndItsTrack()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var tracks = new ParkTrackRideTable();

		ParkItemCatalogue.Item Named( string stem ) => catalogue.All.Single( item => item.Stem == stem );

		int Bought( ParkItemCatalogue.Item item )
			=> ParkRideScore.ExcitementOf( ParkBuilding.Constructed( item, 9000, 20, 12, 0, 0, 0, default,
				tracks.Take( item.BumperType ) ), item, tracks );

		Assert.AreEqual( 42, Bought( Named( "bumper" ) ), "the Hot Pot, 70 × 60%" );
		Assert.AreEqual( 48, Bought( Named( "gokarts" ) ), "Dino Karts, 80 × 60%" );
		Assert.AreEqual( 45, Bought( Named( "wateride" ) ), "Splish Splash, 75 × 60%" );

		var karts = Named( "gokarts" );
		var standing = ParkBuilding.Constructed( karts, 9000, 20, 12, 0, 0, 0, default );

		Assert.AreEqual( 80, ParkRideScore.ExcitementOf( standing, karts, tracks ), "no handle: its level" );
		Assert.AreEqual( 48, ParkRideScore.ExcitementOf( standing with { TrackRide = unchecked((int)0xfffffd07) },
			karts, tracks ), "a stale handle: 60% of it" );

		var played = new ParkTrackRideTable( new ParkTrackRides(
			TrackBytes.Kart( TrackBytes.DinoKartsHandle, TrackBytes.DinoKarts, TrackBytes.PlayedKarts ) ) );

		Assert.AreEqual( 81, ParkRideScore.ExcitementOf( standing with { TrackRide = TrackBytes.DinoKartsHandle },
			karts, played ), "48 + 33" );
		Assert.AreEqual( 0, Unimplemented.Summary.FirstOrDefault( entry => entry.What == "RIDE_EXCITEMENT_TRACK_CROWD" ).Times,
			"the track is built, not counted" );

		Assert.AreEqual( 88, ParkRideScore.TrackBase( 80, new ParkTrackRideTable.Layout( 20, 10, 5, 50 ) ),
			"the track's 65 held at 40" );
		Assert.AreEqual( 100, ParkRideScore.TrackBase( 200, new ParkTrackRideTable.Layout( 20, 10, 5, 50 ) ),
			"and 160 in all held at 100" );
	}

	/// <summary>
	/// The queue term divides the queue by four to a cell of the WALKED queue, nought read as one - not by the
	/// saved <c>mQueueSizeInCells</c>. Four in a queue of two cells is 50, of none 0.
	/// </summary>
	[TestMethod]
	public void TheQueueTermIsOverTheWalkedCells()
	{
		var score = new ParkRideScore( Balance() );
		var ride = Ride();
		var item = ItemFor( ride );
		var wants = new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f );

		int Over( int cells ) => score.Of( wants, new ParkRideScore.Candidate( ride, item, 0, 4, 0, 999, false, 80, cells ) );

		Assert.AreNotEqual( 2, ride.QueueSizeInCells, "the saved count is not the one used" );
		Assert.AreEqual( 22, Over( 2 ), "( 100 + 50 + 100 ) / 11" );
		Assert.AreEqual( 18, Over( 0 ), "( 100 + 0 + 100 ) / 11" );
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
