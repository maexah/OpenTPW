using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What the park is worth and how many a load brings - <see cref="ParkWorth"/>, the original's
/// <c>FUN_004c8240</c> and <c>FUN_004cf5b0</c> (<c>docs/exe/park.md</c>, "The headcount score").
///
/// <para>
/// The expected numbers are the listing's, and two of them the original's own: its stock park's first two loads
/// held 13 and 12 (<c>q26/orig/a.log</c>). The tests on the shipped park read real game files and are skipped
/// where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkWorthTests
{
	private const int BellyBounce = 13;

	private const int BellyBounceItem = 1100;

	private const int DinoKarts = 1150;

	private static ParkWorld Park( BaseFileSystem data )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static int Worth( ParkState state, ParkWorld world, ParkItemCatalogue catalogue )
		=> ParkWorth.Of( state, world, catalogue, thing => ParkRideChoice.QueueLength( world, thing ) );

	/// <summary>
	/// A day is 23.04 sweeps at the shipped rate, counted from the stamp and cut to whole days: the Belly Bounce,
	/// stamped 15, is 54 days old on the first load's tick and turns 60 on tick 1398.
	/// </summary>
	[DataTestMethod]
	[DataRow( 755, 15u, 32 )]
	[DataRow( 1264, 15u, 54 )]
	[DataRow( 1397, 15u, 59 )]
	[DataRow( 1398, 15u, 60 )]
	[DataRow( 1916, 15u, 82 )]
	[DataRow( 15, 15u, 0 )]
	public void AnAgeIsWholeDaysOnTheParksClock( int tick, uint stamp, int days )
		=> Assert.AreEqual( days, ParkWorth.DaysSince( tick, stamp, GameCalendar.DefaultRate ) );

	/// <summary>The ticks since are taken unsigned, so a stamp ahead of the clock is a very old thing, not a new one.</summary>
	[TestMethod]
	public void AStampAheadOfTheClockIsOldNotNew()
	{
		var days = ParkWorth.DaysSince( 10, 15, GameCalendar.DefaultRate );

		Assert.IsTrue( days is < 0 or > 180, $"{days} days must not land on a bonus step" );
		Assert.AreEqual( 0, ParkWorth.NewBonus( new ParkItemCatalogue.Item( 1, "", "", "", 1, 1, null,
			NewAttractionDecayTime: 60, NewBonus0: 10, NewBonus1: 7, NewBonus2: 4 ), days ) );
	}

	/// <summary>The bonus steps down each decay time and is nought from the fourth step on, and before the first.</summary>
	[DataTestMethod]
	[DataRow( 0, 10 )]
	[DataRow( 59, 10 )]
	[DataRow( 60, 7 )]
	[DataRow( 119, 7 )]
	[DataRow( 120, 4 )]
	[DataRow( 179, 4 )]
	[DataRow( 180, 0 )]
	[DataRow( 100000, 0 )]
	[DataRow( -60, 0 )]
	public void TheBonusStepsDownWithTheAge( int days, int bonus )
	{
		var item = new ParkItemCatalogue.Item( 1, "", "", "", 1, 1, null,
			NewAttractionDecayTime: 60, NewBonus0: 10, NewBonus1: 7, NewBonus2: 4 );

		Assert.AreEqual( bonus, ParkWorth.NewBonus( item, days ) );
	}

	/// <summary>
	/// The load's arithmetic, on Lost Kingdom's easy numbers (bonus 20, 5 points a visitor, at least 1): 13 and 12
	/// are the two loads the original called; the rain's and the worthless park's are the listing's. The product is
	/// cut, not rounded: 29 points in fair weather are 34.8, so 34 over 5, six people where 35 would bring seven.
	/// </summary>
	[DataTestMethod]
	[DataRow( 35, false, 13 )]
	[DataRow( 32, false, 12 )]
	[DataRow( 35, true, 8 )]
	[DataRow( 32, true, 8 )]
	[DataRow( 0, false, 4 )]
	[DataRow( 0, true, 3 )]
	[DataRow( 105, false, 30 )]
	[DataRow( 9, false, 6 )]
	public void ALoadIsSizedFromTheWorthAndTheWeather( int worth, bool raining, int people )
		=> Assert.AreEqual( people, ParkWorth.LoadSize( worth, raining, 20, 5, 1 ) );

	/// <summary>The floor and the divisor's own floor: never fewer than the least, and a divisor under 1 divides by 1.</summary>
	[TestMethod]
	public void ALoadIsNeverUnderTheLeastAndNeverDividedByNought()
	{
		Assert.AreEqual( 7, ParkWorth.LoadSize( 0, false, 20, 5, 7 ), "the least, where the sum gives four" );
		Assert.AreEqual( 24, ParkWorth.LoadSize( 0, false, 20, 0, 1 ), "a divisor of nought is one" );
		Assert.AreEqual( 24, ParkWorth.LoadSize( 0, false, 20, -3, 1 ), "and so is one below it" );
	}

	/// <summary>
	/// The save's control records carry the count and the stamp (FileFormats <c>saves.md</c>, "The object
	/// controls"), as the original's memory read them: the Belly Bounce 1 and 15, three Small Toilets, the Balloon
	/// Shop sold and still stamped, the bus stamped 604.
	/// </summary>
	[TestMethod]
	public void TheSavesRecordsCarryTheCountAndTheFirstBuild()
	{
		var records = Park( GameData.Required() ).ObjectControlRecords.ToDictionary( record => record.ItemId );

		Assert.AreEqual( (1, 15u), (records[1100].Standing, records[1100].FirstBuilt), "the Belly Bounce" );
		Assert.AreEqual( (3, 15u), (records[1402].Standing, records[1402].FirstBuilt), "the Small Toilet" );
		Assert.AreEqual( (0, 15u), (records[1209].Standing, records[1209].FirstBuilt), "the Balloon Shop, sold" );
		Assert.AreEqual( (1, 604u), (records[1600].Standing, records[1600].FirstBuilt), "the bus" );
		Assert.AreEqual( (1, 0u), (records[1601].Standing, records[1601].FirstBuilt), "the gates, made on tick nought" );
		Assert.AreEqual( 14, records.Values.Sum( record => record.Standing ), "the park's fourteen objects" );
	}

	/// <summary>Lost Kingdom's item files: a ride's value and bonuses, a track ride's larger ones, a sideshow's nought.</summary>
	[TestMethod]
	public void TheItemFilesGiveEachRideItsValueAndBonuses()
	{
		FileSystem = GameData.Required();

		var catalogue = new ParkItemCatalogue( "jungle" );

		Assert.IsTrue( catalogue.TryGet( BellyBounceItem, out var bouncy ) );
		Assert.AreEqual( (25, 60, 10, 7, 4),
			(bouncy.AttractionValue, bouncy.NewAttractionDecayTime, bouncy.NewBonus0, bouncy.NewBonus1, bouncy.NewBonus2) );

		Assert.IsTrue( catalogue.TryGet( DinoKarts, out var karts ) );
		Assert.AreEqual( (25, 60, 15, 10, 5),
			(karts.AttractionValue, karts.NewAttractionDecayTime, karts.NewBonus0, karts.NewBonus1, karts.NewBonus2) );

		// A feature states none of its own: these are its folder's Features.sam showing through.
		Assert.IsTrue( catalogue.TryGet( 1402, out var toilet ) );
		Assert.AreEqual( (10, 30, 10, 5, 2),
			(toilet.AttractionValue, toilet.NewAttractionDecayTime, toilet.NewBonus0, toilet.NewBonus1, toilet.NewBonus2) );

		Assert.IsTrue( catalogue.TryGet( 1303, out var spray ) );
		Assert.AreEqual( (0, 1, 0, 0, 0),
			(spray.AttractionValue, spray.NewAttractionDecayTime, spray.NewBonus0, spray.NewBonus1, spray.NewBonus2) );
	}

	/// <summary>
	/// The stock park is worth 35 while its Belly Bounce is in its first 60 days, 32 from tick 1398, and nought
	/// on a sweep the offer gate refuses it; the Jungle Spray, a sideshow on offer, adds nothing.
	/// </summary>
	[TestMethod]
	public void TheStockParkIsWorthItsOneRide()
	{
		var data = GameData.Required();
		FileSystem = data;

		var world = Park( data );
		var catalogue = new ParkItemCatalogue( "jungle", instantAction: true );
		var state = new ParkState( world );

		Assert.AreEqual( 35, Worth( state, world, catalogue ), "25 and the first bonus, 10, on the save's tick 755" );

		state.SetGameTick( 1397 );
		Assert.AreEqual( 35, Worth( state, world, catalogue ), "still new on its 59th day" );

		state.SetGameTick( 1398 );
		Assert.AreEqual( 32, Worth( state, world, catalogue ), "the second bonus, 7, from its 60th" );

		state.SetGameTick( 15 + 1383 * 3 );
		Assert.AreEqual( 25, Worth( state, world, catalogue ), "and none from its 180th" );

		state.SetGameTick( 755 );

		var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );

		Assert.IsTrue( state.ReplaceObject( ride with { CanLoad = 0 } ) );
		Assert.AreEqual( 0, Worth( state, world, catalogue ), "a ride not loading is worth nought on that sweep" );

		Assert.IsTrue( state.ReplaceObject( ride ) );
		Assert.AreEqual( 35, Worth( state, world, catalogue ) );
		Assert.AreEqual( 0, ParkWorth.Of( state, world, null, _ => 0 ), "and with no catalogue nothing is known to be a ride" );
	}

	/// <summary>
	/// Two of one flat ride are worth what one is, each adding its share by whole division, and both are aged from
	/// the first's build; the count goes down as one is sold and the stamp stays.
	/// </summary>
	[TestMethod]
	public void TwoOfOneRideShareItsWorthAndItsFirstBuild()
	{
		var data = GameData.Required();
		FileSystem = data;

		var world = Park( data );
		var catalogue = new ParkItemCatalogue( "jungle", instantAction: true );
		var state = new ParkState( world );
		var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );

		state.SetGameTick( 1000 );
		state.AddObject( ride with { ThingId = 900 } );

		Assert.AreEqual( (2, 15u), state.BuiltOf( BellyBounceItem ), "the second is counted, and the stamp is the first's" );
		Assert.AreEqual( 34, Worth( state, world, catalogue ), "35 over 2 is 17, twice" );

		// Past the first's 60th day a second bought this minute is no newer than the first: 32 over 2, twice, where
		// its own age would make it 16 and 17.
		state.SetGameTick( 1398 );
		Assert.IsTrue( state.ReplaceObject( ride with { ThingId = 900, Built = ParkWorld.BuiltWhen.At( state.CalendarNow ) } ) );
		Assert.AreEqual( 32, Worth( state, world, catalogue ), "both aged from the item's first build" );

		state.SetGameTick( 1000 );
		Assert.IsTrue( state.RemoveObject( 900 ) );
		Assert.AreEqual( (1, 15u), state.BuiltOf( BellyBounceItem ), "sold, the count goes down and the stamp stays" );
		Assert.AreEqual( 35, Worth( state, world, catalogue ) );
	}

	/// <summary>
	/// An item never built is stamped with the clock at its first build and keeps that stamp through a sale and a
	/// second build; one whose stamp reads nought takes the next build's.
	/// </summary>
	[TestMethod]
	public void TheFirstBuildStampsTheItemOnce()
	{
		var data = GameData.Required();
		var world = Park( data );
		var state = new ParkState( world );
		var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );

		Assert.AreEqual( (0, 0u), state.BuiltOf( 1101 ), "no Crazy Ape was ever built here" );

		state.SetGameTick( 800 );
		state.AddObject( ride with { ThingId = 900, CatalogueId = 1101 } );
		Assert.AreEqual( (1, 800u), state.BuiltOf( 1101 ) );

		state.SetGameTick( 900 );
		Assert.IsTrue( state.RemoveObject( 900 ) );
		Assert.AreEqual( (0, 800u), state.BuiltOf( 1101 ) );

		state.AddObject( ride with { ThingId = 901, CatalogueId = 1101 } );
		Assert.AreEqual( (1, 800u), state.BuiltOf( 1101 ), "built again, it is as old as its first" );

		Assert.AreEqual( (1, 0u), state.BuiltOf( 1601 ), "the gates' stamp reads nought" );
		state.AddObject( ride with { ThingId = 902, CatalogueId = 1601 } );
		Assert.AreEqual( (2, 900u), state.BuiltOf( 1601 ), "so the next build writes it" );
	}

	/// <summary>
	/// A track ride adds its whole value however many stand, and is aged by its own purchase, not its item's
	/// first: two Dino Karts, one new and one 60 days old, add 40 and 35 to the Belly Bounce's 35.
	/// </summary>
	[TestMethod]
	public void EachTrackRideAddsItsWholeValueByItsOwnAge()
	{
		var data = GameData.Required();
		FileSystem = data;

		var world = Park( data );
		var catalogue = new ParkItemCatalogue( "jungle" );
		var state = new ParkState( world );
		var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );
		var now = state.CalendarNow;

		state.AddObject( ride with { ThingId = 900, CatalogueId = DinoKarts, IsTrackRideValid = 1,
			Built = ParkWorld.BuiltWhen.At( now ) } );

		Assert.AreEqual( 35 + 40, Worth( state, world, catalogue ), "25 and its first bonus, 15" );

		state.AddObject( ride with { ThingId = 901, CatalogueId = DinoKarts, IsTrackRideValid = 1,
			Built = ParkWorld.BuiltWhen.At( now.AddDays( -60 ) ) } );

		Assert.AreEqual( 35 + 40 + 35, Worth( state, world, catalogue ), "the older adds 25 and 10, and halves nothing" );

		Assert.IsTrue( state.ReplaceObject( ride with { ThingId = 901, CatalogueId = DinoKarts, IsTrackRideValid = 0,
			Built = ParkWorld.BuiltWhen.At( now.AddDays( -60 ) ) } ) );

		Assert.AreEqual( 35 + 40, Worth( state, world, catalogue ), "one with no valid track is not on offer" );
	}

	/// <summary>
	/// A fresh Full Simulation park counts its two fixed objects, made on tick nought, and is worth nought: its
	/// first load is four at the regular 6 points a visitor.
	/// </summary>
	[TestMethod]
	public void AFreshParkIsWorthNoughtAndCountsItsFixedObjects()
	{
		var data = GameData.Required();
		FileSystem = data;

		var catalogue = new ParkItemCatalogue( "jungle" );
		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle" ), catalogue );
		var state = new ParkState( fresh );

		Assert.AreEqual( (1, 0u), state.BuiltOf( 1601 ), "the gates" );
		Assert.AreEqual( (1, 0u), state.BuiltOf( 1603 ), "the lights" );
		Assert.AreEqual( (0, 0u), state.BuiltOf( BellyBounceItem ) );
		Assert.AreEqual( 0, ParkWorth.Of( state, fresh, catalogue, _ => 0 ) );
		Assert.AreEqual( 4, ParkWorth.LoadSize( 0, false, 20, 6, 1 ) );
	}

	/// <summary>
	/// The guests' judgement reads the same sum, afresh: against the stock park a guest's fair fee is 20 and the
	/// worth's share, 25 to 28 by the divisor's draw, and 20 on a sweep the Belly Bounce is not loading.
	/// </summary>
	[TestMethod]
	public void TheGatesJudgementReadsTheParksWorthAfresh()
	{
		var data = GameData.Required();
		FileSystem = data;

		var world = Park( data );
		var catalogue = new ParkItemCatalogue( "jungle", instantAction: true );
		var state = new ParkState( world );
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), state );
		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, state: state, catalogue: catalogue );

		Assert.AreEqual( 35, behaviour.ParkExcitement );

		var ideals = Enumerable.Range( 0, 64 )
			.Select( seed => admission.IdealPrice( behaviour.ParkExcitement, new Random( seed ) ) ).Distinct().Order().ToArray();

		CollectionAssert.AreEqual( new[] { 25, 27, 28 }, ideals, "35 over 6, 5 and 4, on the least fee of 20" );

		var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );

		Assert.IsTrue( state.ReplaceObject( ride with { CanLoad = 0 } ) );
		Assert.AreEqual( 0, behaviour.ParkExcitement, "taken at the asking, not kept" );

		// And the gate is asked with the queue as the park counts it: one short of the room is on offer, full is not.
		Assert.IsTrue( state.ReplaceObject( ride ) );

		var room = ParkRideChoice.QueueCellsFor( world, ride ).Cells * ParkRideChoice.QueueRoomPerCell;

		for ( var guest = 0; guest < room - 1; ++guest )
			state.JoinQueue( BellyBounce, 1000 + guest );

		Assert.AreEqual( 35, behaviour.ParkExcitement, $"{room - 1} queueing of {room}" );

		state.JoinQueue( BellyBounce, 1000 + room );
		Assert.AreEqual( 0, behaviour.ParkExcitement, "a ride whose queue is full is worth nought on that sweep" );
	}
}
