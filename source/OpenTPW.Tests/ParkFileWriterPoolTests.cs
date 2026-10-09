using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, the staff pool and the arrival timer (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the
/// staff pool and the arrival timer"): the running pool written slot for slot, the timer's mark, count and flag, and
/// both read again.
/// </summary>
[TestClass]
public class ParkFileWriterPoolTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld shipped = null!;
	private ParkBalance balance = null!;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );
		balance = new ParkBalance( "jungle", easyMode: true );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void Forget()
	{
		TestRun.DeleteEvery<ParkPeople>();
		ParkStaffPool.ForgetCurrent();
		Unimplemented.Forget();
	}

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	private ParkFileWriter.Running AsShipped => new( shipped.GameTick, shipped.ParkClosed != 0,
		shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value );

	private ParkStaffPool ShippedPool() => new( balance, gameTick: shipped.GameTick, saved: shipped );

	/// <summary>The pool run from the file's clock to <paramref name="until"/>, the park employing one of each kind as the file's does.</summary>
	private ParkStaffPool PoolRunTo( int until )
	{
		var pool = ShippedPool();

		for ( var tick = shipped.GameTick + 1; tick <= until; ++tick )
			pool.Sweep( tick, _ => 1 );

		return pool;
	}

	private ParkWorld Written( ParkFileWriter.Running running )
	{
		var written = new ParkWorld( ParkFileWriter.Body( shipped, running ) );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer, "the list closes" );

		return written;
	}

	[TestMethod]
	public void TheShippedPoolsCountsAreRead()
	{
		CollectionAssert.AreEqual( new[] { 1, 1, 1, 1, 1 }, shipped.StaffPoolPeopleInCat.ToArray(), "one of each kind employed" );
		CollectionAssert.AreEqual( new[] { false, false, false, false, false }, shipped.StaffPoolStopProducing.ToArray() );
	}

	/// <summary>A pool and a timer nothing has happened to write the file's own bytes: no byte of the body moves.</summary>
	[TestMethod]
	public void APoolAndATimerLeftAloneWriteTheFilesBytes()
	{
		var people = new ParkPeople( shipped );
		var body = ParkFileWriter.Body( shipped, AsShipped with { StaffPool = ShippedPool().Written(), Arrival = people.WrittenArrival() } );

		CollectionAssert.AreEqual( ParkFileWriter.Body( shipped, AsShipped ), body );
	}

	/// <summary>
	/// The shipped pool at 755 run to 1450: the six of 361 and the ten of 722 have gone or wait on, the pool was topped
	/// up on 1083 and 1444, and the file holds each candidate in their slot as the pool has them.
	/// </summary>
	[TestMethod]
	public void ARunPoolIsWrittenSlotForSlot()
	{
		var pool = PoolRunTo( 1450 );

		Assert.AreEqual( 1444, pool.Mark, "722, then 1083, then 1444: each 361 sweeps on" );

		var written = Written( AsShipped with { GameTick = 1450, StaffPool = pool.Written() } );

		Assert.AreEqual( 1444, written.StaffPoolTimeSig );
		Assert.AreEqual( pool.Candidates.Count, written.StaffPool.Count( record => record.Valid ) );
		Assert.IsTrue( pool.Candidates.Any( person => person.Mark == 1083 ) && pool.Candidates.Any( person => person.Mark == 1444 ),
			"candidates of both top-ups wait" );

		foreach ( var person in pool.Candidates )
		{
			var record = written.StaffPool[person.Slot];

			Assert.AreEqual( new ParkWorld.StaffCandidate( person.Kind, person.NameRow, person.Grade, person.Costume, true, false,
				person.Mark, person.Lifetime ), record, $"slot {person.Slot}, {person.Name}" );
		}

		Assert.AreEqual( pool.Candidates.Count, pool.Candidates.Select( person => person.Slot ).Distinct().Count(), "nobody shares a slot" );
		CollectionAssert.AreEqual( new[] { 1, 1, 1, 1, 1 }, written.StaffPoolPeopleInCat.ToArray() );

		// Nothing but the pool's records, counts and mark has moved.
		var before = ParkFileWriter.Body( shipped, AsShipped with { GameTick = 1450 } );
		var after = ParkFileWriter.Body( shipped, AsShipped with { GameTick = 1450, StaffPool = pool.Written() } );

		Assert.AreEqual( before.Length, after.Length );

		for ( var at = 0; at < before.Length; ++at )
		{
			if ( before[at] != after[at] )
				Assert.IsTrue( at >= shipped.StaffPoolAt && at < shipped.StaffPoolAt + 32 * 20 + 25 + 4, $"byte {at:x} moved" );
		}
	}

	/// <summary>The pool a written file gives back is the pool that was written: the same people in the same order of slots, on the same marks.</summary>
	[TestMethod]
	public void TheWrittenPoolLoadsAgain()
	{
		var pool = PoolRunTo( 1450 );
		var written = Written( AsShipped with { GameTick = 1450, StaffPool = pool.Written() } );
		var again = new ParkStaffPool( balance, gameTick: written.GameTick, saved: written );

		Assert.AreEqual( pool.Mark, again.Mark );

		CollectionAssert.AreEqual(
			pool.Candidates.OrderBy( person => person.Slot ).Select( person => (person.Slot, person.Kind, person.Name, person.Grade, person.Costume, person.Wage, person.Mark, person.Lifetime) ).ToArray(),
			again.Candidates.Select( person => (person.Slot, person.Kind, person.Name, person.Grade, person.Costume, person.Wage, person.Mark, person.Lifetime) ).ToArray() );

		// And it runs on as the first would have: no top-up before 1805, one on it.
		for ( var tick = 1451; tick <= 1804; ++tick )
			again.Sweep( tick, _ => 1 );

		Assert.AreEqual( 1444, again.Mark );
		again.Sweep( 1805, _ => 1 );
		Assert.AreEqual( 1805, again.Mark );
	}

	/// <summary>
	/// A slot whose candidate has gone keeps its bytes and loses its valid flag, as the original's does; a newcomer
	/// takes the lowest slot free.
	/// </summary>
	[TestMethod]
	public void AGoneCandidatesSlotIsEmptiedAndANewcomerTakesTheLowestFree()
	{
		var pool = ShippedPool();
		var gone = pool.Candidates.Single( person => person.Slot == 2 );
		var file = shipped.StaffPool[2];

		Assert.IsTrue( pool.Take( gone.Id, out _ ) );

		var written = Written( AsShipped with { StaffPool = pool.Written() } );

		Assert.AreEqual( file with { Valid = false }, written.StaffPool[2], "the record but its valid flag" );
		Assert.AreEqual( 15, written.StaffPool.Count( record => record.Valid ) );

		// Each newcomer takes the lowest slot nobody held as they came: slot 2 is among those the first top-up fills.
		var joined = 0;
		var tookTheGoneOnes = false;

		pool.Joined += who =>
		{
			var held = pool.Candidates.Where( person => person.Id != who.Id ).Select( person => person.Slot ).ToHashSet();

			Assert.AreEqual( Enumerable.Range( 0, 32 ).First( slot => !held.Contains( slot ) ), who.Slot, $"{who.Name}" );
			tookTheGoneOnes |= who.Slot == 2;
			++joined;
		};

		for ( var tick = shipped.GameTick + 1; tick <= 1083; ++tick )
			pool.Sweep( tick, _ => 1 );

		Assert.IsTrue( joined > 3 && tookTheGoneOnes, $"{joined} joined on the top-up, one of them into slot 2" );
		Assert.AreEqual( pool.Candidates.Count, pool.Candidates.Select( person => person.Slot ).Distinct().Count() );
	}

	/// <summary>The counts are the last top-up's: a kind at its limit then is written as stopped.</summary>
	[TestMethod]
	public void TheCountsAreTheLastTopUps()
	{
		var pool = ShippedPool();

		for ( var tick = shipped.GameTick + 1; tick <= 1083; ++tick )
			pool.Sweep( tick, kind => kind == 3 ? pool.MostInPark( 3 ) : kind );

		var written = Written( AsShipped with { StaffPool = pool.Written() } );

		CollectionAssert.AreEqual( new[] { 0, 1, 2, pool.MostInPark( 3 ), 4 }, written.StaffPoolPeopleInCat.ToArray() );
		CollectionAssert.AreEqual( new[] { false, false, false, true, false }, written.StaffPoolStopProducing.ToArray() );

		var again = new ParkStaffPool( balance, gameTick: written.GameTick, saved: written ).Written();

		CollectionAssert.AreEqual( written.StaffPoolPeopleInCat.ToArray(), again.PeopleInCat.ToArray() );
		CollectionAssert.AreEqual( written.StaffPoolStopProducing.ToArray(), again.StopProducing.ToArray() );
	}

	[TestMethod]
	public void ACandidateOnTheCursorIsWrittenInTheirSlotAndNotOnThePointer()
	{
		var pool = ShippedPool();
		var held = pool.Candidates[3];

		ParkStaffPool.Carry( held.Id );

		try
		{
			var written = Written( AsShipped with { StaffPool = pool.Written() } );

			Assert.AreEqual( shipped.StaffPool[held.Slot], written.StaffPool[held.Slot] );
			Assert.IsFalse( written.StaffPool[held.Slot].OnPointer );
		}
		finally
		{
			ParkStaffPool.Drop();
		}
	}

	/// <summary>A file whose candidate was in the hand is read with them in the pool, counted, and written with nobody on the pointer.</summary>
	[TestMethod]
	public void ACandidateTheFileHasOnThePointerIsWrittenOffIt()
	{
		var body = (byte[])ParkFileWriter.Body( shipped, AsShipped ).Clone();

		body[shipped.StaffPoolAt + 3 * 20 + 0x0b] = 1;

		var inHand = new ParkWorld( body );

		Assert.IsTrue( inHand.StaffPool[3].OnPointer );

		var pool = new ParkStaffPool( balance, gameTick: inHand.GameTick, saved: inHand );

		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVED_STAFF_CANDIDATE_ON_POINTER" ).Times );

		var written = new ParkWorld( ParkFileWriter.Body( inHand, AsShipped with { StaffPool = pool.Written() } ) );

		Assert.AreEqual( inHand.StaffPool[3] with { OnPointer = false }, written.StaffPool[3] );
	}

	/// <summary>The timer's mark, count and flag are written; the three fields nothing here runs are the file's.</summary>
	[TestMethod]
	public void TheArrivalTimerIsWritten()
	{
		var written = Written( AsShipped with { Arrival = new ParkFileWriter.ArrivalTimer( 1313, 7, true ) } );

		Assert.AreEqual( shipped.Arrival with { TimeSig = 1313, PeopleOnBus = 7, Offloading = true }, written.Arrival );
		Assert.AreEqual( 661, shipped.Arrival.TimeSig );
		Assert.AreEqual( 5, written.Arrival.TargetVehicleCapacity );
		Assert.IsTrue( written.Arrival.GatesOpen );

		var before = ParkFileWriter.Body( shipped, AsShipped );
		var after = ParkFileWriter.Body( shipped, AsShipped with { Arrival = new ParkFileWriter.ArrivalTimer( 1313, 7, true ) } );
		var moved = Enumerable.Range( 0, before.Length ).Where( at => before[at] != after[at] ).ToArray();

		CollectionAssert.AreEqual( new[] { 4, 5, 0x0c, 0x10 }, moved.Select( at => at - shipped.ArrivalAt ).ToArray(),
			"661 to 1313 is two bytes, the count one, the flag one" );
	}

	/// <summary>The running park's timer is what is handed over, and a load held is carried on by the park that reads it.</summary>
	[TestMethod]
	public void ALoadHeldIsWrittenAndCarriedOn()
	{
		var people = new ParkPeople( shipped );

		Assert.AreEqual( new ParkFileWriter.ArrivalTimer( 661, 0, false ), people.WrittenArrival() );

		people.HoldTheLoad( 5 );

		Assert.AreEqual( new ParkFileWriter.ArrivalTimer( 661, 5, true ), people.WrittenArrival() );

		var written = Written( AsShipped with { Arrival = people.WrittenArrival() } );
		var again = new ParkPeople( written );

		Assert.IsTrue( again.LoadHeld, "the load is still held" );
		Assert.AreEqual( 5, again.StillToDrop );
		Assert.AreEqual( 661, again.ArrivalMark );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What is "SAVED_ARRIVAL_LOAD" or "SAVE_PARK_ARRIVAL_VEHICLE" ),
			"the load is carried on, and no vehicle was current" );

		var none = new ParkPeople( shipped );

		Assert.IsFalse( none.LoadHeld );
		Assert.AreEqual( 0, none.StillToDrop );
	}

	/// <summary>
	/// The vehicle that is current is the header's <c>mCurrentArrivalVehicle</c>, the thing the file names for its
	/// size: written with the things, read back as the same vehicle, and written as none once none is. Without the
	/// things the handle is left the file's.
	/// </summary>
	[TestMethod]
	public void TheCurrentArrivalVehicleIsWrittenAndReadBack()
	{
		var bus = shipped.ArrivalVehicleForSmallCrowd;

		Assert.AreEqual( (15, 0, 0, 0), (bus, shipped.ArrivalVehicleForMediumCrowd, shipped.ArrivalVehicleForLargeCrowd, shipped.CurrentArrivalVehicle) );

		var none = new ParkPeople( shipped );

		Assert.AreEqual( 0, none.CurrentVehicle );
		Assert.AreEqual( new ParkFileWriter.ArrivalTimer( 661, 0, false, 0 ), none.WrittenArrival( _ => true ), "none current is written as nought" );
		Assert.IsNull( none.WrittenArrival().CurrentVehicle, "and left the file's without the things" );

		// Two bytes of the header move, and nothing else.
		var before = ParkFileWriter.Body( shipped, AsShipped );
		var after = ParkFileWriter.Body( shipped, AsShipped with { Arrival = none.WrittenArrival() with { CurrentVehicle = 0x1234 } } );
		var moved = Enumerable.Range( 0, before.Length ).Where( at => before[at] != after[at] ).ToArray();

		Assert.AreEqual( 2, moved.Length );
		Assert.AreEqual( (0x34, 0x12, 1), (after[moved[0]], after[moved[1]], moved[1] - moved[0]) );
		Assert.AreEqual( 0x1234, new ParkWorld( after ).CurrentArrivalVehicle );
		TestRun.DeleteEvery<ParkPeople>();

		var withBus = Written( AsShipped with { Arrival = none.WrittenArrival() with { CurrentVehicle = bus } } );

		Assert.AreEqual( bus, withBus.CurrentArrivalVehicle );

		var people = new ParkPeople( withBus );

		Assert.AreEqual( 1, people.CurrentVehicle, "the small crowd's" );
		Assert.IsFalse( people.LargerVehicleIsCurrent );
		Assert.AreEqual( bus, people.WrittenArrival( _ => true ).CurrentVehicle, "and written as its thing again" );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What is "SAVED_CURRENT_ARRIVAL_VEHICLE" or "SAVE_PARK_ARRIVAL_VEHICLE" ) );

		// A file whose own handle is the bus, written by a park with none current, holds none.
		TestRun.DeleteEvery<ParkPeople>();

		var cleared = new ParkWorld( ParkFileWriter.Body( withBus, AsShipped with { Arrival = new ParkPeople( shipped ).WrittenArrival( _ => true ) } ) );

		Assert.AreEqual( 0, cleared.CurrentArrivalVehicle );
	}

	/// <summary>
	/// A vehicle that is current and cannot be written is counted and written as none: one whose thing is not in
	/// the file being written, and one written without the things, whose handle is left. A file naming a current
	/// vehicle that is none of its three is counted at the load, and none is current.
	/// </summary>
	[TestMethod]
	public void ACurrentVehicleThatCannotBeWrittenOrReadIsCounted()
	{
		int Counted( string what ) => Unimplemented.Summary.Where( gap => gap.What == what ).Sum( gap => gap.Times );

		var withBus = Written( AsShipped with { Arrival = new ParkFileWriter.ArrivalTimer( 661, 0, false, shipped.ArrivalVehicleForSmallCrowd ) } );
		var people = new ParkPeople( withBus );

		Assert.AreEqual( 0, people.WrittenArrival( thing => thing != shipped.ArrivalVehicleForSmallCrowd ).CurrentVehicle, "its thing is not written" );
		Assert.AreEqual( 1, Counted( "SAVE_PARK_ARRIVAL_VEHICLE" ) );
		Assert.IsNull( people.WrittenArrival().CurrentVehicle, "the things are the file's" );
		Assert.AreEqual( 2, Counted( "SAVE_PARK_ARRIVAL_VEHICLE" ) );
		Assert.AreEqual( 0, Counted( "SAVED_CURRENT_ARRIVAL_VEHICLE" ) );
		TestRun.DeleteEvery<ParkPeople>();

		var stranger = new ParkPeople( Written( AsShipped with { Arrival = new ParkFileWriter.ArrivalTimer( 661, 0, false, 16 ) } ) );

		Assert.AreEqual( 0, stranger.CurrentVehicle );
		Assert.AreEqual( 1, Counted( "SAVED_CURRENT_ARRIVAL_VEHICLE" ) );
		Assert.AreEqual( 0, stranger.WrittenArrival( _ => true ).CurrentVehicle );
	}

	[TestMethod]
	public void APoolOrATimerThatCannotBeWrittenIsRefused()
	{
		var short31 = new ParkWorld.WrittenStaffPool( new ParkWorld.StaffCandidate?[31], new int[5], new bool[5], 0 );

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, AsShipped with { StaffPool = short31 } ) );

		var fourKinds = new ParkWorld.WrittenStaffPool( new ParkWorld.StaffCandidate?[32], new int[4], new bool[5], 0 );

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, AsShipped with { StaffPool = fourKinds } ) );
	}
}
