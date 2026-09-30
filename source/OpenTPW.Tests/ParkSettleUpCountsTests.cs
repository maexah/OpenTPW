using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What the settle-up counts besides the effects, and the day rings it counts into - <c>docs/exe/ride-operation.md</c>,
/// "The settle-up's bookkeeping": the guest's rides, purchases, sideshows played and won; the object's customers,
/// served and satisfaction; the day's change; the join's snapshot; the door's walk-away.
/// <para>
/// The ring arithmetic runs without the game. The rest reads Lost Kingdom's save and catalogue, and is skipped where
/// there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkSettleUpCountsTests
{
	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	private const int BellyBounce = 13;
	private const int JungleSpray = 14;
	private const int DrinksShop = 16;
	private const int SmallToilet = 21;

	private const int Sweep = 40;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private RideScript Script()
	{
		using var stream = Data().OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	private static Peep Riding( float happiness, float joined )
	{
		var peep = new Peep( 7, new ParkWorld.GuestState(
			State: (int)PeepState.Riding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: happiness, Thirst: 80f, Hunger: 80f, Toilet: 80f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

		peep.JoinHappiness = joined;

		return peep;
	}

	/// <summary>The park's own mood constants - <c>PeepInfo.MediumHappinessChange</c> is 15 in this stack.</summary>
	private static ParkAdmission Mood() => new( new ParkBalance( "jungle", easyMode: true ), 25 );

	/// <summary>
	/// Lets one guest off <paramref name="thingId"/> through <see cref="ParkRideOperation.Dismiss"/>, won
	/// (<paramref name="won"/>, the settle-up's <c>+0x1f1</c>) or lost, into <paramref name="park"/>.
	/// </summary>
	private void LetOff( ParkState park, Peep peep, int thingId, bool won )
	{
		var thing = Park().Objects.Single( o => o.ThingId == thingId );
		var script = Script();

		peep.SetState( PeepState.Riding, tick: 1, new Random( 1 ) );
		peep.QueuePos = won ? 1 : 0;

		script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep> { [peep.ThingId] = peep }, Mood() )
			.Dismiss( script, thing, tick: 9, new Random( 1 ), catalogue: new ParkItemCatalogue( "jungle", Data() ) ),
			"the guest should have been let off" );
	}

	private static ParkState EmptyPark() => new( parkIsClosed: false, visitorsToDate: 0 );

	// ---- the rings, without the game ----

	/// <summary>
	/// <b>A day ends into the next slot</b> (<c>FUN_004dd320</c>): the entry steps from −1 to nought, today's figure is
	/// stored there and today starts again; the thirtieth wraps to nought and marks the ring wrapped.
	/// </summary>
	[TestMethod]
	public void ADaysChangeStoresTodayAndWrapsAtThirty()
	{
		var ring = new ParkObjectRings.DayRing();

		Assert.AreEqual( -1, ring.CurrentEntry, "a new ring has finished no day" );
		Assert.AreEqual( 0, ring.Filled );

		ring.Today = 7;
		ring.Roll();

		Assert.AreEqual( 0, ring.CurrentEntry );
		Assert.AreEqual( 0, ring.Today, "today starts again at nought" );
		Assert.AreEqual( 7, ring.DaysAgo( 0 ), "yesterday holds what today held" );
		Assert.IsFalse( ring.WrappedAround );

		for ( var day = 1; day < 30; ++day )
		{
			ring.Today = day + 100;
			ring.Roll();
		}

		Assert.AreEqual( 29, ring.CurrentEntry, "thirty days finished, the last slot filled" );
		Assert.IsFalse( ring.WrappedAround, "and not yet wrapped" );

		ring.Today = 5;
		ring.Roll();

		Assert.AreEqual( 0, ring.CurrentEntry, "the thirty-first wraps to the first slot" );
		Assert.IsTrue( ring.WrappedAround );
		Assert.AreEqual( 5, ring.DaysAgo( 0 ) );
		Assert.AreEqual( 129, ring.DaysAgo( 1 ), "the day before sits in the last slot" );
		Assert.AreEqual( 30, ring.Filled );
	}

	/// <summary>
	/// <b>Users last month adds the finished days only</b>: today's figure is not among them, a ring with three
	/// finished days adds three, and a wrapped one the last thirty (<c>FUN_004ade40</c>, <c>0x004adeb7</c>).
	/// </summary>
	[TestMethod]
	public void TheLastThirtyDaysAreTheFinishedOnes()
	{
		var ring = new ParkObjectRings.DayRing();

		ring.Today = 4;
		Assert.AreEqual( 0, ring.LastDays( 30 ), "today's four are not a finished day" );

		foreach ( var customers in new[] { 4, 2, 1 } )
		{
			ring.Today = customers;
			ring.Roll();
		}

		ring.Today = 50;
		Assert.AreEqual( 7, ring.LastDays( 30 ), "three finished days, and today's fifty left out" );
		Assert.AreEqual( 3, ring.LastDays( 2 ), "the last two only" );

		for ( var day = 0; day < 40; ++day )
		{
			ring.Today = day;
			ring.Roll();
		}

		// Forty days more, 0 to 39: the last thirty are 10 to 39.
		Assert.AreEqual( Enumerable.Range( 10, 30 ).Sum(), ring.LastDays( 30 ), "a wrapped ring adds its last thirty" );
	}

	/// <summary>
	/// <b>A day's satisfaction averages each visit in, as the original measured it</b> (<c>FUN_004e1e00</c>): 18 then
	/// 18 is 18; 45 then 15 is 30. A day at nought takes the next visit whole, and a halving truncates toward nought.
	/// </summary>
	[TestMethod]
	public void SatisfactionAveragesEachVisitIntoTheDay()
	{
		var rings = new ParkObjectRings();

		rings.Satisfy( 18 );
		rings.Satisfy( 18 );
		Assert.AreEqual( 18, rings.Satisfaction.Today, "18 and 18 to 18" );

		rings.Satisfaction.Roll();
		rings.Satisfy( 45 );
		rings.Satisfy( 15 );
		Assert.AreEqual( 30, rings.Satisfaction.Today, "45 and 15 to 30" );

		rings.Satisfaction.Roll();
		rings.Satisfy( -3 );
		rings.Satisfy( 0 );
		Assert.AreEqual( -1, rings.Satisfaction.Today, "-3 and 0 halve to -1, toward nought, where a floor gives -2" );

		rings.Satisfy( 1 );
		Assert.AreEqual( 0, rings.Satisfaction.Today, "-1 and 1 to nought" );

		rings.Satisfy( 12 );
		Assert.AreEqual( 12, rings.Satisfaction.Today, "and a day at nought takes the next visit whole" );
	}

	/// <summary>
	/// <b>The day's change rolls all six rings and neither count</b>: <c>mNumCustomers</c> and <c>mNumWalkAways</c>
	/// are lifetime totals.
	/// </summary>
	[TestMethod]
	public void TheDaysChangeRollsEveryRingAndNeitherCount()
	{
		var park = EmptyPark();
		var rings = park.RingsFor( BellyBounce );

		rings.CountCustomer();
		rings.CountWalkAway();
		rings.Served.Today = 3;
		rings.Takings.Today = 4;
		rings.Costs.Today = 5;
		rings.Satisfy( 6 );

		park.RollTheDay();

		Assert.AreEqual( 1, rings.NumCustomers, "the lifetime customers stay" );
		Assert.AreEqual( 1, rings.NumWalkAways, "and the lifetime walk-aways" );

		foreach ( var (name, ring, was) in new[] {
			("customers", rings.Customers, 1), ("walk-aways", rings.WalkAways, 1), ("served", rings.Served, 3),
			("takings", rings.Takings, 4), ("costs", rings.Costs, 5), ("satisfaction", rings.Satisfaction, 6) } )
		{
			Assert.AreEqual( 0, ring.Today, $"{name}: today starts at nought" );
			Assert.AreEqual( was, ring.DaysAgo( 0 ), $"{name}: yesterday holds the day just ended" );
		}
	}

	// ---- the save ----

	/// <summary>
	/// <b>Every object record in the park that ships holds six rings of thirty</b>, nothing counted in them: most
	/// objects have finished 32 days (entry 1, wrapped), the unplaced thing 15 six (entry 5, not wrapped). And every
	/// guest's four counts are nought.
	/// </summary>
	[TestMethod]
	public void TheShippedParksRingsAndCountsAreRead()
	{
		var world = Park();

		Assert.AreEqual( 14, world.Objects.Count );

		foreach ( var thing in world.Objects )
		{
			var saved = thing.Rings ?? throw new AssertFailedException( $"thing {thing.ThingId} read no rings" );

			Assert.AreEqual( 0, saved.NumCustomers, $"thing {thing.ThingId}: nobody has visited" );
			Assert.AreEqual( 0, saved.NumWalkAways, $"thing {thing.ThingId}: nor walked away" );

			foreach ( var ring in new[] { saved.Costs, saved.Takings, saved.Customers, saved.WalkAways, saved.Served,
				saved.Satisfaction } )
			{
				Assert.AreEqual( 30, ring.NumEntries, $"thing {thing.ThingId}: thirty days" );
				Assert.AreEqual( 0, ring.Today, $"thing {thing.ThingId}: nothing today" );
			}

			var (entry, wrapped) = thing.ThingId switch { 15 => (5, false), 11 or 12 => (2, true), _ => (1, true) };

			Assert.AreEqual( entry, saved.Customers.CurrentEntry, $"thing {thing.ThingId}'s entry" );
			Assert.AreEqual( wrapped, saved.Customers.WrappedAround, $"thing {thing.ThingId}'s wrap" );
		}

		foreach ( var person in world.People.Where( person => person.Guest is { } ) )
		{
			var guest = person.Guest!.Value;

			Assert.AreEqual( 0, guest.NumRides + guest.NumShops + guest.NumSideshows + guest.NumSideshowsWon,
				$"guest {person.ThingId} has counted nothing" );
		}

		// And the running park starts from the file's rings rather than from empty ones.
		var state = new ParkState( world );

		Assert.AreEqual( 1, state.RingsFor( BellyBounce ).Customers.CurrentEntry, "seeded from the record" );
		Assert.IsTrue( state.RingsFor( BellyBounce ).Customers.WrappedAround );
	}

	/// <summary>
	/// <b>A day not reached yet is never added</b>: thing 15 has finished six days, and its record holds
	/// <c>0xCDCDCDCD</c> in the slots past them, which a sum over all thirty would take in.
	/// </summary>
	[TestMethod]
	public void ADayNotReachedIsNeverAdded()
	{
		var world = Park();
		var customers = world.Objects.Single( o => o.ThingId == 15 ).Rings!.Customers;

		Assert.AreEqual( unchecked((int)0xCDCDCDCD), customers.Days[29], "the heap's fill, in the file" );
		Assert.AreEqual( 0, new ParkState( world ).RingsFor( 15 ).Customers.LastDays( 30 ), "six finished days of nought" );
	}

	/// <summary>
	/// <b>A thing built in the park starts empty and takes the day's change from its first day</b>
	/// (<c>FUN_004db090</c>: both counts nought, each entry −1, and it listens for message <c>0xb</c> at once).
	/// </summary>
	[TestMethod]
	public void ABuiltThingStartsEmptyAndRollsWithThePark()
	{
		var world = Park();
		var state = new ParkState( world );
		var bounce = world.Objects.Single( o => o.ThingId == BellyBounce );

		Assert.IsTrue( state.RemoveObject( BellyBounce ) );
		state.AddObject( bounce );
		state.RollTheDay();

		var rings = state.RingsFor( BellyBounce );

		Assert.AreEqual( 0, rings.Customers.CurrentEntry, "a new thing's first day ended into slot nought" );
		Assert.AreEqual( 0, rings.NumCustomers, "and it has had nobody" );
	}

	/// <summary><b>A thing sold takes its rings with it.</b></summary>
	[TestMethod]
	public void ASoldThingsRingsGoWithIt()
	{
		var state = new ParkState( Park() );

		state.RingsFor( BellyBounce ).CountCustomer();
		Assert.IsTrue( state.RemoveObject( BellyBounce ) );

		Assert.AreEqual( 0, state.RingsFor( BellyBounce ).NumCustomers, "the sold thing's customer went with it" );
	}

	/// <summary>
	/// <b>The running rings start from the record's</b>: each ring's entry, wrap, today and days, and both counts.
	/// The park that ships holds nought in all of them, so a record is made up here with a figure in each.
	/// </summary>
	[TestMethod]
	public void TheSavedRingsSeedTheRunningOnes()
	{
		static ParkWorld.DayRing Ring( int today, int entry = 3, bool wrapped = false, params int[] days )
		{
			var all = new int[ParkWorld.DayRing.Length];
			days.CopyTo( all, 0 );

			return new ParkWorld.DayRing( entry, 30, wrapped, today, all );
		}

		var saved = new ParkWorld.ObjectRings( Costs: Ring( 11 ), Takings: Ring( 12 ), NumCustomers: 23,
			Customers: Ring( 13, 3, false, 4, 2, 1, 7 ), NumWalkAways: 5, WalkAways: Ring( 14 ),
			Served: Ring( 15, 1, true, 9, 8 ), Satisfaction: Ring( 16 ) );

		var rings = new ParkObjectRings( saved );

		Assert.AreEqual( (11, 12, 13, 14, 15, 16), (rings.Costs.Today, rings.Takings.Today, rings.Customers.Today,
			rings.WalkAways.Today, rings.Served.Today, rings.Satisfaction.Today), "each ring's today, in its place" );
		Assert.AreEqual( (23, 5), (rings.NumCustomers, rings.NumWalkAways), "and both counts" );
		Assert.AreEqual( 7, rings.Customers.DaysAgo( 0 ), "yesterday is the entry's slot" );
		Assert.AreEqual( 14, rings.Customers.LastDays( 30 ), "four finished days, 4 + 2 + 1 + 7" );
		Assert.AreEqual( 8, rings.Served.DaysAgo( 0 ), "a wrapped ring's entry" );
		Assert.AreEqual( 9, rings.Served.DaysAgo( 1 ), "and the slot before it" );
		Assert.AreEqual( 30, rings.Served.Filled, "wrapped, so every slot counts" );
	}

	/// <summary>
	/// <b>Each ring and count is read from its own place in the record</b> (FileFormats <c>saves.md</c>: the costs ring
	/// at 228, takings 361, <c>mNumCustomers</c> 494, customers 498, <c>mNumWalkAways</c> 631, walk-aways 635, served
	/// 768, satisfaction 901; within a ring the entry, the count, the wrap byte, today, then the days). The shipped
	/// park holds the same figures in all six, so a copy of its payload is given a different one in each, at the Belly
	/// Bounce's record, found by fields read elsewhere.
	/// </summary>
	[TestMethod]
	public void EachRingAndCountIsReadFromItsOwnPlace()
	{
		var payload = Payload();
		var bounce = new ParkWorld( payload ).Objects.Single( o => o.ThingId == BellyBounce );
		var at = RecordAt( payload, p => U16( payload, p + 20 ) == bounce.CatalogueId
			&& U16( payload, p + 206 ) == bounce.EntryPos && U16( payload, p + 218 ) == bounce.ExitPos );

		foreach ( var (ring, today) in new[] { (228, 11), (361, 12), (498, 13), (635, 14), (768, 15), (901, 16) } )
			Put( payload, at + ring + 9, today );

		Put( payload, at + 494, 23 );
		Put( payload, at + 631, 5 );
		Put( payload, at + 498, 3 );                // the customers ring's entry
		payload[at + 498 + 8] = 0;                   // and its wrap byte
		Put( payload, at + 498 + 13 + (3 * 4), 7 );  // and slot 3's day

		var rings = new ParkWorld( payload ).Objects.Single( o => o.ThingId == BellyBounce ).Rings!;

		Assert.AreEqual( (11, 12, 13, 14, 15, 16), (rings.Costs.Today, rings.Takings.Today, rings.Customers.Today,
			rings.WalkAways.Today, rings.Served.Today, rings.Satisfaction.Today), "each ring from its own place" );
		Assert.AreEqual( (23, 5), (rings.NumCustomers, rings.NumWalkAways), "and each count" );
		Assert.AreEqual( (3, false, 7), (rings.Customers.CurrentEntry, rings.Customers.WrappedAround, rings.Customers.Days[3]),
			"the entry, the wrap byte and a day, each where the ring keeps it" );
	}

	/// <summary>
	/// <b>A guest's four counts are read from 444, 448, 452 and 456</b> (FileFormats <c>saves.md</c>). Every shipped
	/// guest holds nought there, so a copy of the payload is given 5, 4, 3 and 2 at one guest's record, found by the
	/// cash, state and kind read elsewhere.
	/// </summary>
	[TestMethod]
	public void AGuestsFourCountsAreReadFromTheirPlace()
	{
		var payload = Payload();
		var person = new ParkWorld( payload ).People.First( p => p.Guest is { } );
		var guest = person.Guest!.Value;
		var at = RecordAt( payload, p => I32( payload, p + 414 ) == guest.Cash && I32( payload, p + 505 ) == guest.State
			&& payload[p + 468] == guest.PersonType && I32( payload, p + 418 ) == guest.ExitLevel );

		foreach ( var (offset, count) in new[] { (444, 5), (448, 4), (452, 3), (456, 2) } )
			Put( payload, at + offset, count );

		var read = new ParkWorld( payload ).People.Single( p => p.ThingId == person.ThingId ).Guest!.Value;

		Assert.AreEqual( (5, 4, 3, 2), (read.NumRides, read.NumShops, read.NumSideshows, read.NumSideshowsWon) );
		Assert.AreEqual( guest.PaidAdmission, read.PaidAdmission, "and the field after them is untouched" );
	}

	private byte[] Payload()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new SaveReader( stream ).ReadFile();
	}

	/// <summary>The one offset in the payload where <paramref name="matches"/> holds.</summary>
	private static int RecordAt( byte[] payload, Func<int, bool> matches )
	{
		var found = Enumerable.Range( 0, payload.Length - 1100 ).Where( matches ).ToList();

		Assert.AreEqual( 1, found.Count, "exactly one record answers to those fields" );

		return found[0];
	}

	private static int U16( byte[] payload, int at ) => BitConverter.ToUInt16( payload, at );

	private static int I32( byte[] payload, int at ) => BitConverter.ToInt32( payload, at );

	private static void Put( byte[] payload, int at, int value ) => BitConverter.GetBytes( value ).CopyTo( payload, at );

	/// <summary>
	/// <b>A saved guest keeps their four counts</b>: the shipped park's are all nought, so a guest made from a record
	/// that carries some is what shows they reach the running guest.
	/// </summary>
	[TestMethod]
	public void ASavedGuestKeepsTheirCounts()
	{
		var peep = new Peep( 7, new ParkWorld.GuestState(
			State: (int)PeepState.Deciding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f, Litter: 0f,
			MajorDest: 0, QueuePos: 0, PrankeryIndex: 0,
			NumRides: 5, NumShops: 4, NumSideshows: 3, NumSideshowsWon: 2 ), StandingStill );

		Assert.AreEqual( (5, 4, 3, 2), (peep.NumRides, peep.NumShops, peep.NumSideshows, peep.NumSideshowsWon) );
		Assert.AreEqual( 0f, peep.JoinHappiness, "and the join's snapshot, which is not saved, starts at nought" );
	}

	// ---- the settle-up ----

	/// <summary>
	/// <b>The guest counts the visit by the item's kind, won or lost</b> (<c>0x004fd9ac</c>): the Belly Bounce a ride
	/// ridden, the Drinks Shop a purchase, the Jungle Spray a sideshow played; a toilet, a feature, none.
	/// </summary>
	[TestMethod]
	public void TheGuestCountsTheVisitByItsKind()
	{
		foreach ( var won in new[] { true, false } )
		{
			var park = EmptyPark();
			var peep = Riding( 50f, 50f );

			LetOff( park, peep, BellyBounce, won );
			LetOff( park, peep, DrinksShop, won );
			LetOff( park, peep, DrinksShop, won );
			LetOff( park, peep, JungleSpray, won );
			LetOff( park, peep, SmallToilet, won );

			var arm = won ? "won" : "lost";

			Assert.AreEqual( 1, peep.NumRides, $"{arm}: one ride" );
			Assert.AreEqual( 2, peep.NumShops, $"{arm}: two drinks" );
			Assert.AreEqual( 1, peep.NumSideshows, $"{arm}: one sideshow played" );
			Assert.AreEqual( won ? 1 : 0, peep.NumSideshowsWon, $"{arm}: and won only on the effects arm" );
		}
	}

	/// <summary>
	/// <b>The object counts every customer, won or lost, and serves only a win</b> (<c>FUN_004e1690</c> before the gate,
	/// <c>FUN_004e19f0</c> behind it). A lost play at the Jungle Spray is a customer and not served.
	/// </summary>
	[TestMethod]
	public void EveryVisitIsACustomerAndOnlyAWinIsServed()
	{
		var park = EmptyPark();
		var peep = Riding( 50f, 50f );

		LetOff( park, peep, JungleSpray, won: true );
		LetOff( park, peep, JungleSpray, won: false );
		LetOff( park, peep, JungleSpray, won: false );

		var spray = park.RingsFor( JungleSpray );

		Assert.AreEqual( 3, spray.NumCustomers, "three plays" );
		Assert.AreEqual( 3, spray.Customers.Today, "all three today" );
		Assert.AreEqual( 1, spray.Served.Today, "one won" );
		Assert.AreEqual( 60, spray.Takings.Today, "and twenty a play taken today" );

		LetOff( park, peep, SmallToilet, won: true );

		Assert.AreEqual( 1, park.RingsFor( SmallToilet ).Customers.Today, "a toilet counts its customer" );
		Assert.AreEqual( 1, park.RingsFor( SmallToilet ).Served.Today, "and serves them" );
		Assert.AreEqual( 0, park.RingsFor( BellyBounce ).NumCustomers, "and nothing else moved" );
	}

	/// <summary>
	/// <b>Three times the happiness gained since the join goes into the day's satisfaction</b>, each side cut to its
	/// low byte: the Drinks Shop's +5 from 50 is 15; the Jungle Spray's winner, joined at 40 and cheered from 50 to 69,
	/// is 3 × 29 = 87. A lost play puts nothing in.
	/// </summary>
	[TestMethod]
	public void TheChangeSinceTheJoinIsAveragedIntoTheDay()
	{
		var park = EmptyPark();

		LetOff( park, Riding( 50f, joined: 50.9f ), DrinksShop, won: true );
		Assert.AreEqual( 15, park.RingsFor( DrinksShop ).Satisfaction.Today, "(55 - 50) x 3, the join's .9 cut off" );

		LetOff( park, Riding( 50f, joined: 40f ), JungleSpray, won: true );
		Assert.AreEqual( 87, park.RingsFor( JungleSpray ).Satisfaction.Today, "(69 - 40) x 3" );

		LetOff( park, Riding( 50f, joined: 0f ), JungleSpray, won: false );
		Assert.AreEqual( 87, park.RingsFor( JungleSpray ).Satisfaction.Today, "a lost play averages nothing in" );

		// A guest loaded mid-visit compares against nought: the whole of their happiness, times three.
		LetOff( park, Riding( 50f, joined: 0f ), BellyBounce, won: true );
		Assert.AreEqual( 150, park.RingsFor( BellyBounce ).Satisfaction.Today, "50 x 3 against a snapshot of nought" );
	}

	/// <summary>
	/// <b>A guest arriving at the back of a queue copies their happiness as they join</b> (<c>0x004ffd92</c>); one
	/// turned away by a full queue copies nothing.
	/// </summary>
	[TestMethod]
	public void JoiningAQueueCopiesTheGuestsHappiness()
	{
		foreach ( var (queued, joins) in new[] { (7, true), (8, false) } )
		{
			var world = Park();
			var state = new ParkState( world );
			var bounce = world.Objects.Single( o => o.ThingId == BellyBounce );
			state.ReplaceObject( bounce with { OperatingCapacity = 2 } );

			for ( var guest = 70; state.QueueCount( BellyBounce, _ => true ) < queued; ++guest )
				state.JoinQueue( BellyBounce, guest );

			var balance = new ParkBalance( "jungle", easyMode: true );
			var behaviour = new PeepBehaviour( world, new Random( 1 ),
				new ParkAdmission( balance, world.Economy!.Value.AdmissionFee ), () => ParkRides.GateIsOpen, state,
				new ParkItemCatalogue( "jungle", Data() ), stillQueueing: _ => true, balance: balance );

			// A type 2, preferring 50, against the Belly Bounce's 40: the excitement gate lets them by.
			var (x, y) = MapStep.CellAt( ParkRideChoice.QueueCellsFor( world, bounce ).BackOfQueue );
			var centre = new FixedVector( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );
			var saved = world.People.First( person => person.Guest is { } );
			var arriving = new Peep( 30, saved.Guest!.Value with {
				State = (int)PeepState.GoingToRide, PersonType = 2, Happiness = 37f, MajorDest = BellyBounce },
				saved.Navigator with { X = centre.X, Y = centre.Y, TargetX = centre.X, TargetY = centre.Y } );

			behaviour.Step( arriving, new PeepWalk( arriving.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ),
				playing: null, Sweep );

			Assert.AreEqual( joins ? queued : -1, state.PositionInQueue( BellyBounce, arriving.ThingId ),
				$"{queued} queueing: {(joins ? "joins at the back" : "turned away")}" );
			Assert.AreEqual( joins ? 37f : 0f, arriving.JoinHappiness, 0.001f,
				joins ? "their happiness as they joined" : "nothing copied by a refusal" );
		}
	}
}
