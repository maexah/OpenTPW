using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The one numbering things share - <see cref="ParkState.NextThingId"/>, the original's free list
/// (<c>FUN_00516270</c>): seeded rising by the reader (<c>FUN_005179c0</c>), a deleted id pushed on the head of the
/// deleted list (<c>FUN_0051ad40</c>) and popped onto the free list's head at the sweep's end (<c>FUN_00516380</c>).
/// <c>docs/exe/ride-operation.md</c>, "Where a ride's turn comes from".
/// </summary>
[TestClass]
public class ParkThingIdTests
{
	/// <summary>A state with no file whose objects hold <paramref name="ids"/>, added before any id is asked for.</summary>
	private static ParkState Holding( params int[] ids )
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		foreach ( var id in ids )
			state.AddObject( new ParkWorld.CatalogueObject( id, 1100, 10 << 8, 10 << 8, 0 ) );

		return state;
	}

	private static int[] Take( ParkState state, int count )
		=> Enumerable.Range( 0, count ).Select( _ => state.NextThingId() ).ToArray();

	/// <summary>
	/// <b>A load's holes come first, rising, then one past the highest</b>: late30's file holds 1 to 42 but 14, and the
	/// original made its seaplane thing 14; Q238's load took 38 for its first guest and 43 on.
	/// </summary>
	[TestMethod]
	public void TheFilesHolesComeFirstRisingThenOnePastItsHighest()
	{
		var state = Holding( [.. Enumerable.Range( 1, 42 ).Where( id => id is not 14 and not 38 )] );

		CollectionAssert.AreEqual( new[] { 14, 38, 43, 44 }, Take( state, 4 ) );
	}

	/// <summary><b>A deleted id is not free until the sweep it was deleted in has ended</b>.</summary>
	[TestMethod]
	public void ADeletedIdWaitsForTheSweepsEnd()
	{
		var state = Holding( 1, 2, 3, 4, 5 );

		state.DeleteThing( 3 );

		CollectionAssert.AreEqual( new[] { 3 }, state.DeletedThingIds.ToArray() );
		Assert.AreEqual( 6, state.NextThingId(), "still held through the sweep" );

		state.FreeDeletedThings();

		Assert.AreEqual( 0, state.DeletedThingIds.Count );
		CollectionAssert.AreEqual( new[] { 3, 7 }, Take( state, 2 ), "then the head of the free list" );
	}

	/// <summary>
	/// <b>Of the things deleted in one sweep, the first deleted is the first given out, ahead of any older free
	/// id</b>: the deleted list is pushed and popped at its head and the free list pushed at its head, so the two
	/// reversals cancel.
	/// </summary>
	[TestMethod]
	public void TheFirstDeletedInASweepIsTheFirstGivenOutAheadOfOlderFreeIds()
	{
		var state = Holding( 1, 3, 4, 5, 6 );

		state.DeleteThing( 5 );
		state.DeleteThing( 1 );
		state.DeleteThing( 4 );
		state.FreeDeletedThings();

		CollectionAssert.AreEqual( new[] { 5, 1, 4, 2, 7 }, Take( state, 5 ) );
	}

	/// <summary><b>A later sweep's frees go ahead of an earlier one's</b>: the free list is pushed at its head.</summary>
	[TestMethod]
	public void ALaterSweepsFreesGoAheadOfAnEarlierOnes()
	{
		var state = Holding( 1, 2, 3, 4 );

		state.DeleteThing( 2 );
		state.FreeDeletedThings();
		state.DeleteThing( 4 );
		state.FreeDeletedThings();

		CollectionAssert.AreEqual( new[] { 4, 2, 5 }, Take( state, 3 ) );
	}

	/// <summary>Nought is never a thing and is never deleted, and an empty park's first id is one.</summary>
	[TestMethod]
	public void NoughtIsNeverAThing()
	{
		var state = Holding();

		state.DeleteThing( 0 );
		state.FreeDeletedThings();

		CollectionAssert.AreEqual( new[] { 1, 2 }, Take( state, 2 ) );
	}

	/// <summary>The console's look at the free list does not take from it.</summary>
	[TestMethod]
	public void LookingAtTheFreeListTakesNothing()
	{
		var state = Holding( 1, 2, 4 );

		CollectionAssert.AreEqual( new[] { 3, 5, 6 }, state.FreeThingIds( 3 ).ToArray() );
		CollectionAssert.AreEqual( new[] { 3, 5 }, Take( state, 2 ) );
	}

	/// <summary>
	/// <b>0x27ff is the last id given out, and past it nought</b>: the original's table holds the ids 1 to 0x27ff
	/// (<c>FUN_00515f30</c>) and its allocator answers nought with none free (<c>FUN_00516270</c>).
	/// </summary>
	[TestMethod]
	public void PastTheLastIdNoughtIsGivenOut()
	{
		var state = Holding( 0x27fe );

		CollectionAssert.AreEqual( Enumerable.Range( 1, 0x27fd ).ToArray(), Take( state, 0x27fd ), "the holes first" );
		Assert.AreEqual( 0x27ff, state.NextThingId(), "the table's last node" );
		CollectionAssert.AreEqual( new[] { 0, 0 }, Take( state, 2 ), "and then none" );
		Assert.IsFalse( state.MadeAt.ContainsKey( 0 ), "nothing was made" );
	}

	/// <summary><b>An id freed once the table is full is given out again</b>, and then it is full once more.</summary>
	[TestMethod]
	public void AnIdFreedInAFullTableIsGivenOutAgain()
	{
		var state = Holding( 0x27ff );

		Take( state, 0x27fe );
		Assert.AreEqual( 0, state.NextThingId() );

		state.DeleteThing( 7 );
		Assert.AreEqual( 0, state.NextThingId(), "held through the sweep" );

		state.FreeDeletedThings();
		CollectionAssert.AreEqual( new[] { 7, 0 }, Take( state, 2 ) );
	}

	/// <summary>The console's look at a nearly full table lists no id past the last.</summary>
	[TestMethod]
	public void TheLookAtTheFreeListStopsAtTheLastId()
	{
		var state = Holding( 0x27fc );

		Take( state, 0x27fb );
		CollectionAssert.AreEqual( new[] { 0x27fd, 0x27fe, 0x27ff }, state.FreeThingIds( 5 ).ToArray() );

		Take( state, 3 );
		Assert.AreEqual( 0, state.FreeThingIds( 5 ).Count() );
	}
}

/// <summary>
/// What deletes a thing in the running park gives its id back: a guest going home, a member of staff fired and an
/// object sold, each on the deleted list until the sweep's end. Reads the shipped park; skipped without the game.
/// </summary>
[TestClass]
public class ParkThingIdDeletionTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private const int DrinksShop = 16;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private (ParkWorld World, ParkState State, ParkPeople People) Park()
	{
		var world = World();
		var state = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( "jungle", data ) );

		return (world, state, people);
	}

	/// <summary>Takes every free id, held by no thing, as the console's <c>thingfill</c> does.</summary>
	private static void Fill( ParkState state )
	{
		// Bounded, so a table with no end fails rather than spins.
		for ( var taken = 0; taken <= ParkState.LastThingId && state.NextThingId() != 0; ++taken )
		{
		}
	}

	/// <summary>
	/// <b>A full table admits no guest and hires nobody</b>, and a guest gone home makes room for one more. The original
	/// falls over here (<c>FUN_004d91f0</c>); here nothing is made.
	/// </summary>
	[TestMethod]
	public void AFullTableMakesNobodyUntilAnIdIsFreed()
	{
		var (_, state, people) = Park();
		var guests = people.Peeps.Count;
		var staff = people.Staff.Count;

		Fill( state );

		Assert.AreEqual( 0, people.Admit( 55, 30 ), "no guest" );
		Assert.AreEqual( 0, people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 1, Name: "Test", Grade: 0, Costume: 0, Wage: 69 ), 55, 30 ),
			"no hire" );
		Assert.AreEqual( (guests, staff), (people.Peeps.Count, people.Staff.Count), "nobody made" );

		var leaver = people.Peeps.First( peep => !PeepBehaviour.HeldByAThing( peep ) ).ThingId;

		Assert.IsTrue( people.Depart( leaver ) );
		state.FreeDeletedThings();

		Assert.AreEqual( leaver, people.Admit( 55, 30 ), "the freed id" );
		Assert.AreEqual( 0, people.Admit( 55, 30 ), "and full again" );
	}

	/// <summary>The shipped park holds 1 to 42 with no hole, so its first id made is 43.</summary>
	[TestMethod]
	public void TheShippedParkHasNoHole()
	{
		var (_, state, _) = Park();

		CollectionAssert.AreEqual( new[] { 43, 44 }, state.FreeThingIds( 2 ).ToArray() );
	}

	[TestMethod]
	public void AGuestGoingHomeIsDeleted()
	{
		var (_, state, people) = Park();
		var guest = people.Peeps.First( peep => !PeepBehaviour.HeldByAThing( peep ) ).ThingId;

		Assert.IsTrue( people.Depart( guest ) );
		CollectionAssert.AreEqual( new[] { guest }, state.DeletedThingIds.ToArray() );

		state.FreeDeletedThings();
		Assert.AreEqual( guest, state.NextThingId() );
	}

	[TestMethod]
	public void AMemberOfStaffFiredIsDeleted()
	{
		var (_, state, people) = Park();
		var member = people.Staff.First().ThingId;

		Assert.IsTrue( people.Fire( member ) );
		CollectionAssert.AreEqual( new[] { member }, state.DeletedThingIds.ToArray() );
	}

	[TestMethod]
	public void AnObjectSoldIsDeleted()
	{
		var (world, state, people) = Park();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, null, DrinksShop, people ), "sell: " );
		CollectionAssert.AreEqual( new[] { DrinksShop }, state.DeletedThingIds.ToArray() );
		Assert.AreEqual( 43, state.NextThingId(), "held until the sweep's end" );

		state.FreeDeletedThings();
		Assert.AreEqual( DrinksShop, state.NextThingId() );
	}

	/// <summary>
	/// <b>Nothing of a sold object passes to the next object on its id</b>: its totals, its queue's head and its
	/// nominee go with it, and the constructor zeroes them on the new one (<c>FUN_004db090</c>, 0x004db163, 0x004db169,
	/// 0x004db0fe, 0x004db132).
	/// </summary>
	[TestMethod]
	public void AnObjectOnASoldObjectsIdStartsFromNought()
	{
		var (world, state, people) = Park();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var guest = people.Peeps.First( peep => !PeepBehaviour.HeldByAThing( peep ) ).ThingId;

		state.TakeAt( DrinksShop, 50 );
		state.BookCostOfGoods( DrinksShop, 20 );
		state.JoinQueue( DrinksShop, guest );
		state.NominateForLoading( DrinksShop, guest );
		StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, null, DrinksShop, people ), "sell: " );

		Assert.AreEqual( (0, 0, 0, 0), (state.TakingsFor( DrinksShop ), state.CostsFor( DrinksShop ), state.FirstInQueue( DrinksShop ),
			state.PersonBeingLoaded( DrinksShop )), "gone with the sale" );

		// Left by hand on the freed id, as a queue head kept by nothing else would be: the constructor zeroes them all.
		state.FreeDeletedThings();
		state.TakeAt( DrinksShop, 7 );
		state.BookCostOfGoods( DrinksShop, 3 );
		state.JoinQueue( DrinksShop, guest );
		state.NominateForLoading( DrinksShop, guest );
		Assert.AreEqual( guest, state.FirstInQueue( DrinksShop ) );

		var id = state.NextThingId();

		state.AddObject( new ParkWorld.CatalogueObject( id, 1101, 41 << 8, 22 << 8, 0 ) );

		Assert.AreEqual( DrinksShop, id );
		Assert.AreEqual( (0, 0, 0, 0), (state.TakingsFor( id ), state.CostsFor( id ), state.FirstInQueue( id ), state.PersonBeingLoaded( id )) );
	}

	/// <summary>
	/// <b>The park's own sweep deletes its leavers in its order and frees them at its end</b>: of two leavers the
	/// one swept first is given out first, and nothing is left on the deleted list.
	/// </summary>
	[TestMethod]
	public void TheSweepFreesItsLeaversAtItsEndInItsOrder()
	{
		var (_, state, people) = Park();

		try
		{
			var leavers = people.Peeps.Where( peep => !PeepBehaviour.HeldByAThing( peep ) ).Take( 2 ).ToArray();

			foreach ( var leaver in leavers )
				leaver.SetState( PeepState.Leaving, 0, new Random( 1 ) );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( 0f );

			RunSweeps( people, 1 );

			Assert.IsFalse( leavers.Any( leaver => people.Guests.ContainsKey( leaver.ThingId ) ), "both went home" );
			Assert.AreEqual( 0, state.DeletedThingIds.Count, "and were freed at the sweep's end" );
			CollectionAssert.AreEqual( leavers.Select( leaver => leaver.ThingId ).ToArray(), state.FreeThingIds( 2 ).ToArray(),
				"the first swept is the first given out" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	private static void RunSweeps( ParkPeople people, int sweeps )
	{
		var until = GameClock.Ticks / ParkPeople.ThingTickEvery + sweeps;

		for ( var frame = 0; frame < 600 && GameClock.Ticks / ParkPeople.ThingTickEvery < until; ++frame )
		{
			Frame( 1f / 60f );
			ParkFrame.Run( people );
		}

		Assert.IsTrue( GameClock.Ticks / ParkPeople.ThingTickEvery >= until, "the sweeps ran" );
	}
}
