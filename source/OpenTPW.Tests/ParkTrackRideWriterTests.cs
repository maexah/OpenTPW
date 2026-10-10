using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A track ride's record written into a park file's track-rides module, and read back by a load -
/// <see cref="ParkTrackRides.Splice"/>, <see cref="ParkTrackRides.Put"/>, <see cref="ParkBumperCars.Written"/> and
/// <see cref="ParkBumperCars.Restore"/>. <c>docs/exe/saves.md</c>, "OpenTPW's writer, a track ride's record".
/// </summary>
[TestClass]
public class ParkTrackRideWriterTests
{
	private const int HotPotHandle = unchecked((int)0xffffff00);

	/// <summary>The original's own bought Hot Pot, shut: the ride chunk and the close of its file, word for word.</summary>
	private static readonly uint[] TheirHotPot =
	[
		3, 52, 52, 0xffffff00, 0x20400, 0x12c00, 0, 0x474, 0x3c, 1, 1, 0x10fe, 0,
		6, 16, 16, 0xffffff00
	];

	private static readonly SavedTrackRide HotPot = new( HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 0 );

	private static byte[] Chunk( int type, params int[] dwords )
	{
		var size = 12 + (dwords.Length * 4);

		return new[] { type, size, size }.Concat( dwords ).SelectMany( BitConverter.GetBytes ).ToArray();
	}

	/// <summary>A body holding a track-rides module of these chunks after its stamp, filler either side.</summary>
	private static byte[] Body( params byte[][] chunks )
	{
		var children = Chunk( 2, 0x7cf, 9, 3, 22, 0 ).Concat( chunks.SelectMany( chunk => chunk ) ).ToArray();
		var root = new[] { 1, 12, 12 + children.Length }.SelectMany( BitConverter.GetBytes );

		return Encoding.ASCII.GetBytes( "RSYS....SYSR" ).Concat( root ).Concat( children )
			.Concat( Encoding.ASCII.GetBytes( "KARTRYLF" ) ).ToArray();
	}

	/// <summary>A ride of the bumper family in a slot with everything a file holds under it: a section, two cars, a rider's record, both lists and the close.</summary>
	private static byte[][] Whole( int slot, int duration = 750, int state = 2 )
	{
		var handle = slot | (-1 << 8);

		return
		[
			Chunk( 3, handle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, duration, state ),
			Chunk( 4, handle, 9, 0x1800, 0x1800 ),
			Chunk( 5, [handle, .. new int[43 + 5]] ),
			Chunk( 9, handle, 40, 1 ),
			Chunk( 5, [handle, .. new int[43 + 5]] ),
			Chunk( 7, handle, 41 ),
			Chunk( 8, handle, 42 ),
			Chunk( 6, handle )
		];
	}

	private static byte[] ModuleOf( byte[] body )
	{
		var from = body.AsSpan().IndexOf( "SYSR"u8 ) + 4;

		return body[from..body.AsSpan().IndexOf( "KART"u8 )];
	}

	[TestMethod]
	public void ARidesRecordAndCloseAreTheOriginalsBytes()
	{
		CollectionAssert.AreEqual( TheirHotPot.SelectMany( BitConverter.GetBytes ).ToArray(), ParkTrackRides.RideChunks( HotPot ) );
	}

	[TestMethod]
	public void ARidesFiveWordsAreRead()
	{
		var read = new ParkTrackRides( Body( Whole( 0 ) ) );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( HotPot with { Duration = 750, State = 2 }, read.Rides.Single() );
		Assert.AreEqual( 2, read.CarsOf( HotPotHandle ) );
		Assert.AreEqual( 0, read.CarsOf( 1 | (-1 << 8) ), "another slot's" );
	}

	/// <summary>A ride chunk as short as the ones before the five words were known reads them nought, as the loader's reads do.</summary>
	[TestMethod]
	public void AShortRideChunkReadsItsWordsNought()
	{
		var read = new ParkTrackRides( Body( Chunk( 3, HotPotHandle, 1, 2, 5, 1140, 60, 7 ), Chunk( 6, HotPotHandle ) ) );

		Assert.AreEqual( new SavedTrackRide( HotPotHandle, 1, 2, 5, 1140, 60, 7 ), read.Rides.Single() );
	}

	[TestMethod]
	public void AMadeRideGoesIntoAnEmptyModule()
	{
		var body = Body();
		var spliced = new ParkTrackRides( body ).Splice( body, [], [HotPot] );
		var read = new ParkTrackRides( spliced );

		Assert.IsNull( read.Problem );
		Assert.IsTrue( read.ClosedOnTag, "the root's whole size follows" );
		Assert.AreEqual( HotPot, read.Rides.Single() );
		Assert.AreEqual( 112, ModuleOf( spliced ).Length );
		CollectionAssert.AreEqual( ModuleOf( body )[12..], ModuleOf( spliced )[12..44], "the stamp is carried" );
		CollectionAssert.AreEqual( TheirHotPot.SelectMany( BitConverter.GetBytes ).ToArray(), ModuleOf( spliced )[44..] );
		CollectionAssert.AreEqual( body[..12], spliced[..12], "what lies before the module" );
		CollectionAssert.AreEqual( "KARTRYLF"u8.ToArray(), spliced[^8..], "and after it" );
	}

	[TestMethod]
	public void AGoneRideTakesEveryChunkUnderItsHandle()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 2 )] );
		var spliced = new ParkTrackRides( body ).Splice( body, [HotPotHandle], [] );
		var read = new ParkTrackRides( spliced );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( 2 | (-1 << 8), read.Rides.Single().Handle );
		Assert.AreEqual( 2, read.CarsOf( 2 | (-1 << 8) ), "the other ride's cars are carried" );
		CollectionAssert.AreEqual( ModuleOf( Body( Whole( 2 ) ) ), ModuleOf( spliced ) );
	}

	/// <summary>The original writes its table slot by slot, so a made ride lies between the rides either side of its slot.</summary>
	[TestMethod]
	public void AMadeRideLiesInItsSlotsPlace()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 2 )] );
		var between = HotPot with { Handle = 1 | (-1 << 8) };
		var last = HotPot with { Handle = 3 | (-1 << 8) };
		var read = new ParkTrackRides( new ParkTrackRides( body ).Splice( body, [], [last, between] ) );

		Assert.IsNull( read.Problem );
		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3 }, read.Rides.Select( ride => ride.Handle & 0xff ).ToArray() );
		Assert.AreEqual( 0, read.CarsOf( between.Handle ) );
		Assert.AreEqual( 2, read.CarsOf( 2 | (-1 << 8) ) );
	}

	[TestMethod]
	public void ASoldRidesSlotIsTakenByAMadeOne()
	{
		var body = Body( Whole( 0 ) );
		var read = new ParkTrackRides( new ParkTrackRides( body ).Splice( body, [HotPotHandle], [HotPot] ) );

		Assert.AreEqual( HotPot, read.Rides.Single() );
		Assert.AreEqual( 0, read.CarsOf( HotPotHandle ) );
	}

	[TestMethod]
	public void WithNothingMadeOrGoneTheBodyIsItself()
	{
		var body = Body( Whole( 0 ) );

		Assert.AreSame( body, new ParkTrackRides( body ).Splice( body, [], [] ) );
	}

	[TestMethod]
	public void AModuleThatDidNotReadIsRefused()
	{
		var body = Body( Whole( 0 ) );

		BitConverter.TryWriteBytes( body.AsSpan( 12 + 8, 4 ), 4000 );

		var read = new ParkTrackRides( body );

		Assert.IsNotNull( read.Problem );
		Assert.ThrowsException<InvalidOperationException>( () => read.Splice( body, [], [HotPot] ) );
	}

	[TestMethod]
	public void AKeptRidesFiveWordsAreWrittenOver()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 2 )] );
		var before = (byte[])body.Clone();
		var tracks = new ParkTrackRides( body );
		var running = HotPot with { Performance = 80, MeshBase = 2, MeshCount = 3, Duration = 900, State = 1, X = 7, ItemId = 9 };

		Assert.AreEqual( 1, tracks.Put( body, [running, HotPot with { Handle = 5 | (-1 << 8) }] ), "a ride the file does not hold is skipped" );

		var read = new ParkTrackRides( body );

		Assert.AreEqual( HotPot with { Performance = 80, MeshBase = 2, MeshCount = 3, Duration = 900, State = 1 }, read.Rides[0], "where it stands and its item are the file's" );
		Assert.AreEqual( 750, read.Rides[1].Duration, "the other ride is left" );
		Assert.IsTrue( body.Zip( before ).Select( ( pair, at ) => (pair, at) ).Where( entry => entry.pair.First != entry.pair.Second )
			.All( entry => entry.at is >= 12 + 12 + 32 + 32 and < 12 + 12 + 32 + 52 ), "only the five words change" );
	}

	/// <summary>A section the file holds before its ride's record is not the record: the five words go to the ride chunk alone.</summary>
	[TestMethod]
	public void AKeptRidesWordsGoToItsRideChunkAlone()
	{
		var section = Chunk( 4, HotPotHandle, 9, 0x1800, 0x1800, 0, 0, 0, 0, 0, 0 );
		var body = Body( section, Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 0 ), Chunk( 6, HotPotHandle ) );
		var at = body.AsSpan().IndexOf( section );

		Assert.AreEqual( 1, new ParkTrackRides( body ).Put( body, [HotPot with { Duration = 900, State = 1 }] ) );
		CollectionAssert.AreEqual( section, body[at..(at + section.Length)] );
		Assert.AreEqual( (900, 1), (new ParkTrackRides( body ).Rides.Single().Duration, new ParkTrackRides( body ).Rides.Single().State) );
	}

	/// <summary>
	/// The record the running table hands over: the arena's centre less the half cell the laying adds, the
	/// placer's code for the turn, and the record's own performance, mesh words, duration and state.
	/// </summary>
	[TestMethod]
	public void TheRunningRideHandsOverItsRecord()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );

		Assert.AreEqual( HotPotHandle, handle );

		table.Cars.Place( handle, 0x20a00, 0x13200 );
		table.Cars.SetPerformance( handle, 60 );

		Assert.AreEqual( HotPot, table.Cars.Written( handle, 0, 1140 ) );
		CollectionAssert.AreEqual( new[] { 0, 5, 6, 1 }, new[] { 0, 90, 180, 270 }.Select( angle => table.Cars.Written( handle, angle, 1140 )!.Value.Orientation ).ToArray() );

		table.Cars.OpenForLoading( handle );
		table.Cars.SetDuration( handle, 750 );

		Assert.AreEqual( (750, 1), (table.Cars.Written( handle, 0, 1140 )!.Value.Duration, table.Cars.Written( handle, 0, 1140 )!.Value.State) );
		Assert.IsNull( table.Cars.Written( 7 | (-1 << 8), 0, 1140 ), "a handle the table does not hold" );
	}

	/// <summary>A load lays the file's performance, mesh words, duration and state over the ride its template made (<c>FUN_00543560</c>).</summary>
	[TestMethod]
	public void ALoadTakesTheRecordsFiveWords()
	{
		Log ??= new();

		var saved = new ParkTrackRides( Body( Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 80, 2, 3, 750, 1 ), Chunk( 6, HotPotHandle ) ) );
		var ride = new ParkTrackRideTable( saved ).Cars.RideOf( HotPotHandle )!;

		Assert.AreEqual( (80, 2, 3, 750, ParkBumperCars.RideState.Loading), (ride.Performance, ride.MeshBase, ride.MeshCount, ride.Duration, ride.State) );
		Assert.AreEqual( 8 + ((12 - 8) * 80 / 100), ride.Thrust, "the performance is set, not stored" );
	}

	/// <summary>The lead boat of the original's own Hot Pot in a go (its file's first car), word for word, and the five words after it.</summary>
	private static readonly uint[] TheirCar =
	[
		0x340c009, 0x1, 0x5e, 0x5f, 0x5, 0x0, 0x0, 0x0, 0x1800057, 0x3, 0x2, 0xffffffff, 0x12617820, 0x20b9f, 0x12fad, 0xffffff7a,
		0xffffffa2, 0xffffff7a, 0xffffffa2, 0xa3, 0x159, 0x150, 0x159, 0x1, 0x0, 0x300, 0x0, 0x202cb, 0x12acc, 0x144, 0x145, 0x5,
		0xffffffff, 0x0, 0x13f, 0x3, 0xb47, 0x0, 0x125f67f0, 0x125e87d0, 0x41ede714, 0xffffffff, 0x0
	];

	private static readonly uint[] TheirCarsTail = [0x20a00, 0x13200, 0xffffff00, 0x20187, 0x12987];

	/// <summary>
	/// A ride written whole is the original's chunks in the original's order: its record, each car with its bytes and
	/// five words and then a chunk a rider, the leaving list, the boarding list and the close, no chunk inside another.
	/// </summary>
	[TestMethod]
	public void ARideWithCarsIsWrittenInTheOriginalsOrder()
	{
		var bytes = new byte[SavedTrackCar.Size];

		Buffer.BlockCopy( TheirCar, 0, bytes, 0, bytes.Length );

		var first = new SavedTrackCar( HotPotHandle, bytes, 0x20a00, 0x13200, HotPotHandle, 0x20187, 0x12987, 0 );

		first.Riders.Add( new SavedTrackRider( 31, 1 ) );
		first.Riders.Add( new SavedTrackRider( 32, 2 ) );

		var second = new SavedTrackCar( HotPotHandle, new byte[SavedTrackCar.Size], 1, 2, 0, 0, 0, 0 );
		var running = HotPot with { Duration = 750, State = 2 };

		var expected = new[]
		{
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Chunk( 5, [HotPotHandle, .. TheirCar.Select( word => (int)word ), .. TheirCarsTail.Select( word => (int)word )] ),
			Chunk( 9, HotPotHandle, 31, 1 ),
			Chunk( 9, HotPotHandle, 32, 2 ),
			Chunk( 5, [HotPotHandle, .. new int[43], 1, 2, 0, 0, 0] ),
			Chunk( 7, HotPotHandle, 41 ),
			Chunk( 7, HotPotHandle, 43 ),
			Chunk( 8, HotPotHandle, 42 ),
			Chunk( 6, HotPotHandle )
		}.SelectMany( chunk => chunk ).ToArray();

		var chunks = ParkTrackRides.RideChunks( new WrittenTrackRide( running, [first, second], [41, 43], [42] ) );

		CollectionAssert.AreEqual( expected, chunks );

		// And a reader takes it back: both cars, the riders on the first in the file's order, the two lists.
		var read = new ParkTrackRides( Body( chunks ) );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( running, read.Rides.Single() );
		Assert.AreEqual( 2, read.Cars.Count );
		CollectionAssert.AreEqual( bytes, read.Cars[0].Bytes );
		CollectionAssert.AreEqual( new[] { new SavedTrackRider( 31, 1 ), new SavedTrackRider( 32, 2 ) }, read.Cars[0].Riders );
		Assert.AreEqual( (0x20a00, 0x13200, HotPotHandle, 0x20187, 0x12987), (read.Cars[0].CentreX, read.Cars[0].CentreZ, read.Cars[0].BuoyRide, read.Cars[0].BuoyX, read.Cars[0].BuoyZ) );
		Assert.AreEqual( 0, read.Cars[1].Riders.Count );
		CollectionAssert.AreEqual( new[] { (41, false), (43, false), (42, true) }, read.Listed.Select( peep => (peep.Peep, peep.Boarding) ).ToArray() );
	}

	/// <summary>
	/// A ride taken out and put in again in one splice lies in its own slot's place with what it is given now: the
	/// file's section, cars, rider and lists under its handle are gone, and the ride after it is untouched.
	/// </summary>
	[TestMethod]
	public void ARideWrittenAgainTakesItsOwnPlace()
	{
		var body = Body( [.. Whole( 0 ), .. Whole( 1 )] );
		var file = new ParkTrackRides( body );
		var handle = 0 | (-1 << 8);
		var car = new SavedTrackCar( handle, new byte[SavedTrackCar.Size], 5, 6, 0, 0, 0, 0 );

		car.Riders.Add( new SavedTrackRider( 77, 1 ) );

		var spliced = file.SpliceWhole( body, [handle],
			[new WrittenTrackRide( new SavedTrackRide( handle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 900, 1 ), [car], [], [78] )] );

		var read = new ParkTrackRides( spliced );

		Assert.IsNull( read.Problem );
		CollectionAssert.AreEqual( new[] { handle, 1 | (-1 << 8) }, read.Rides.Select( ride => ride.Handle ).ToArray(), "in its slot's place, before the next ride" );
		Assert.AreEqual( (900, 1), (read.Rides[0].Duration, read.Rides[0].State) );
		Assert.AreEqual( (1, 2), (read.CarsOf( handle ), read.CarsOf( 1 | (-1 << 8) )) );
		Assert.AreEqual( 77, read.Cars[0].Riders.Single().Peep );
		Assert.AreEqual( 1, read.Sections.Count, "the first ride's section went with it" );
		CollectionAssert.AreEqual( new[] { (handle, 78, true), (1 | (-1 << 8), 41, false), (1 | (-1 << 8), 42, true) },
			read.Listed.Select( peep => (peep.Handle, peep.Peep, peep.Boarding) ).ToArray() );
		Assert.AreEqual( body.Length - (Whole( 0 ).Sum( chunk => chunk.Length ) - (52 + 208 + 24 + 20 + 16)), spliced.Length );
	}

	/// <summary>The model slots a ride's cars give up are the two handles each car's bytes hold, less one; a handle of nought names none.</summary>
	[TestMethod]
	public void ARidesCarsNameTheirModelSlots()
	{
		var handle = 0 | (-1 << 8);
		int[] words = new int[43 + 5];
		int[] bare = new int[43 + 5];

		words[2] = 94;
		words[3] = 95;
		bare[2] = 96;

		var file = new ParkTrackRides( Body( Chunk( 3, handle, 0, 0, 0, 1140, 60, 1, 1, 750, 2 ), Chunk( 5, [handle, .. words] ), Chunk( 5, [handle, .. bare] ), Chunk( 6, handle ),
			Chunk( 3, handle + 1, 0, 0, 0, 1140, 60, 1, 1, 750, 2 ), Chunk( 5, [handle + 1, .. words] ), Chunk( 6, handle + 1 ) ) );

		CollectionAssert.AreEqual( new[] { 93, 94, 95 }, file.CarModelSlots( [handle] ).ToArray() );
		Assert.AreEqual( 0, file.CarModelSlots( [] ).Count() );
	}

	/// <summary>
	/// The original's own boat, read by a load and written again, is the original's bytes: every word a car keeps, 3
	/// and 2 for its emitter nodes, the heading turned to, -1 for the stuck count, the height given, and the five
	/// words after it. The two model handles are left for the writer, and the held sound and the four pointers are
	/// nought.
	/// </summary>
	[TestMethod]
	public void ACarReadAndWrittenAgainIsTheOriginalsBytes()
	{
		Log ??= new();

		var saved = new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Chunk( 5, [HotPotHandle, .. TheirCar.Select( word => (int)word ), .. TheirCarsTail.Select( word => (int)word )] ),
			Chunk( 9, HotPotHandle, 31, 1 ),
			Chunk( 9, HotPotHandle, 32, 2 ),
			Chunk( 6, HotPotHandle ) ) );

		var cars = new ParkTrackRideTable( saved ).Cars;
		var car = cars.CarsOf( HotPotHandle ).Single();

		Assert.AreEqual( 0x159, car.Turned, "the load keeps +0x58" );

		var written = cars.Written( car, BitConverter.UInt32BitsToSingle( TheirCar[40] ), (3, 2) );
		var words = new uint[43];

		Buffer.BlockCopy( written.Bytes, 0, words, 0, written.Bytes.Length );

		// What is not the car's to write: the two model handles, the voice, and the pointers +0x30, +0x98 and +0x9c.
		var expected = (uint[])TheirCar.Clone();

		foreach ( var word in new[] { 2, 3, 8, 12, 38, 39 } )
			expected[word] = 0;

		CollectionAssert.AreEqual( expected, words );
		Assert.AreEqual( (0x20a00, 0x13200, HotPotHandle, 0x20187, 0x12987), (written.CentreX, written.CentreZ, written.BuoyRide, written.BuoyX, written.BuoyZ) );
		CollectionAssert.AreEqual( new[] { new SavedTrackRider( 32, 2 ), new SavedTrackRider( 31, 1 ) }, written.Riders, "head first, and the load turned the file's list round" );

		// A car with no buoy names no ride and no place; and the velocity and the stepped velocity are each its own.
		car.Buoy = -1;
		(car.VelocityX, car.VelocityZ, car.SteppedX, car.SteppedZ) = (7, 8, 9, 10);

		var bare = cars.Written( car, 0f, (-1, -1) );

		Assert.AreEqual( (0x20a00, 0x13200, 0, 0, 0), (bare.CentreX, bare.CentreZ, bare.BuoyRide, bare.BuoyX, bare.BuoyZ) );
		Assert.AreEqual( (-1, -1, -1, 0), (bare.Word( 0x24 ), bare.Word( 0x28 ), bare.Word( 0x7c ), bare.Word( 0xa0 )) );
		Assert.AreEqual( (7, 8, 9, 10), (bare.Word( 0x3c ), bare.Word( 0x40 ), bare.Word( 0x44 ), bare.Word( 0x48 )) );
	}

	/// <summary>
	/// The heading turned to (<c>+0x58</c>) is the steering heading as a tick of a go leaves it, and stands while the
	/// ride loads: nought on a boat before its ride's first go (<c>Bumper_StepCar</c>).
	/// </summary>
	[TestMethod]
	public void TheHeadingTurnedToIsKeptOnlyInAGo()
	{
		Log ??= new();

		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );
		var cars = table.Cars;

		cars.Place( handle, 0x20a00, 0x13200 );
		cars.OpenForLoading( handle );
		cars.Board( handle, 40 );

		var car = cars.Launch( handle )!;

		for ( var tick = 0; tick < 5; ++tick )
			cars.Tick();

		Assert.AreNotEqual( 0, car.Steering, "launched on a heading of its own" );
		Assert.AreEqual( 0, car.Turned, "loading, nothing writes it" );

		cars.Start( handle );

		for ( var tick = 0; tick < 40; ++tick )
			cars.Tick();

		Assert.AreEqual( car.Steering, car.Turned );
		Assert.AreEqual( car.Steering, cars.Written( car, 0f, (3, 2) ).Word( 0x58 ) );
		Assert.AreNotEqual( 0, car.Turned );

		// The ride gone and another in its slot: the pool's car is launched afresh, with no heading turned to.
		cars.Close( handle );
		cars.Open( handle, -1 );
		cars.Place( handle, 0x20a00, 0x13200 );
		cars.OpenForLoading( handle );

		Assert.AreSame( car, cars.Launch( handle ) );
		Assert.AreEqual( 0, car.Turned );
	}

	/// <summary>
	/// A boat's smoke handle (<c>+0x2c</c>) is its file's while it smokes: read by a load, written back, and -1 once
	/// its ride is fixed (<c>FUN_00544e50</c>). A boat that began to smoke here holds none of its own: -1 for the
	/// writer, which starts its emitter.
	/// </summary>
	[TestMethod]
	public void ASmokingBoatKeepsItsFilesHandleUntilItIsFixed()
	{
		Log ??= new();

		var smoking = TheirCar.Select( word => (int)word ).ToArray();

		smoking[11] = 0x1400001;

		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Chunk( 5, [HotPotHandle, .. smoking, .. TheirCarsTail.Select( word => (int)word )] ),
			Chunk( 5, [HotPotHandle, .. TheirCar.Select( word => (int)word ), .. TheirCarsTail.Select( word => (int)word )] ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		var boats = cars.CarsOf( HotPotHandle ).ToArray();

		Assert.AreEqual( (true, 0x1400001), (boats[0].Smoking, boats[0].Smoke) );
		Assert.AreEqual( (false, ParkBumperCars.NoSmoke), (boats[1].Smoking, boats[1].Smoke) );
		Assert.AreEqual( 0x1400001, cars.Written( boats[0], 0f, (3, 2) ).Word( 0x2c ) );
		Assert.AreEqual( -1, cars.Written( boats[1], 0f, (3, 2) ).Word( 0x2c ) );

		// Broken here: the second smokes with no handle; the first's is left.
		cars.Break( HotPotHandle );

		Assert.AreEqual( (true, ParkBumperCars.NoSmoke), (boats[1].Smoking, boats[1].Smoke) );
		Assert.AreEqual( -1, cars.Written( boats[1], 0f, (3, 2) ).Word( 0x2c ) );
		Assert.AreEqual( 0x1400001, cars.Written( boats[0], 0f, (3, 2) ).Word( 0x2c ) );

		cars.Fix( HotPotHandle );

		Assert.AreEqual( (false, ParkBumperCars.NoSmoke), (boats[0].Smoking, boats[0].Smoke) );
		Assert.AreEqual( -1, cars.Written( boats[0], 0f, (3, 2) ).Word( 0x2c ) );

		// A handle left on a boat that smokes no more is not written.
		boats[0].Smoke = 0x1400001;
		Assert.AreEqual( -1, cars.Written( boats[0], 0f, (3, 2) ).Word( 0x2c ) );

		// A boat taken off lets go of its handle, and one launched in its place starts with none.
		boats[1].Smoke = 0x1400001;
		cars.Empty( HotPotHandle );
		Assert.AreEqual( ParkBumperCars.NoSmoke, boats[1].Smoke );

		boats[0].Smoke = 0x1400001;
		cars.OpenForLoading( HotPotHandle );

		var again = cars.Launch( HotPotHandle )!;

		Assert.AreSame( boats[0], again );
		Assert.AreEqual( ParkBumperCars.NoSmoke, again.Smoke );
	}
}
