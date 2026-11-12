using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A track ride's cars, their riders and its two lists read out of a park file's track-rides module and put back by a
/// load - <see cref="ParkTrackRides.Cars"/>, <see cref="ParkTrackRides.Listed"/> and <see cref="ParkBumperCars"/>'s two
/// <c>Restore</c>s. <c>docs/exe/saves.md</c>, "OpenTPW's reader, a track ride's cars".
/// </summary>
[TestClass]
public class ParkTrackRideCarsTests
{
	private const int HotPotHandle = unchecked((int)0xffffff00);

	/// <summary>
	/// The track-rides module of the original's own file of a bought Hot Pot in a go, four boats and four riders
	/// (<c>hotpot-later.TPWS</c>), word for word.
	/// </summary>
	private static readonly uint[] TheirGo =
	[
		1, 0xc, 0x410, 2, 0x20, 0x20, 0x323a3531, 0x32303a35, 0x2072614d, 0x32203731, 0x303030, 3,
		0x34, 0x34, 0xffffff00, 0x20400, 0x12c00, 0, 0x474, 0x3c, 1, 1, 0x2ee, 2,
		5, 0xd0, 0xd0, 0xffffff00, 0x340c009, 1, 0x5e, 0x5f, 5, 0, 0, 0,
		0x1800057, 3, 2, 0xffffffff, 0x12617820, 0x20b9f, 0x12fad, 0xffffff7a, 0xffffffa2, 0xffffff7a, 0xffffffa2, 0xa3,
		0x159, 0x150, 0x159, 1, 0, 0x300, 0, 0x202cb, 0x12acc, 0x144, 0x145, 5,
		0xffffffff, 0, 0x13f, 3, 0xb47, 0, 0x125f67f0, 0x125e87d0, 0x41ede714, 0xffffffff, 0, 0x20a00,
		0x13200, 0xffffff00, 0x20187, 0x12987, 9, 0x18, 0x18, 0xffffff00, 0x1f, 1, 5, 0xd0,
		0xd0, 0xffffff00, 0x300c005, 1, 0x60, 0x61, 5, 0, 0, 0, 0, 3,
		2, 0xffffffff, 0x12617834, 0x2012e, 0x12723, 0xffffff8c, 0x1d, 0xffffff8c, 0x1d, 0x77, 0x1b1, 0x1b3,
		0x1b1, 0, 0, 0x300, 0, 0x1ff91, 0x12776, 0x82, 0x99, 5, 0, 0,
		0x13f, 0x56, 0xb47, 0x125ebbe0, 0x125f67f0, 0x125e87d0, 0x41ede714, 0xffffffff, 0, 0x20a00, 0x13200, 0xffffff00,
		0x20187, 0x12987, 9, 0x18, 0x18, 0xffffff00, 0x27, 1, 5, 0xd0, 0xd0, 0xffffff00,
		0x300c009, 1, 0xa9, 0xaa, 5, 0, 0, 0, 0, 3, 2, 0xffffffff,
		0x12617848, 0x21202, 0x12876, 0x68, 0xffffffb8, 0x68, 0xffffffb8, 0x7e, 0x4f, 0x7b, 0x4f, 0xfffffffa,
		0, 0x300, 0, 0x20a3f, 0x127d3, 0x3f, 0x1d0, 4, 1, 0, 0x13f, 2,
		0xb47, 0x125ebbe0, 0x125f67f0, 0x125e87d0, 0x41ede714, 0xffffffff, 0, 0x20a00, 0x13200, 0xffffff00, 0x20a00, 0x12603,
		9, 0x18, 0x18, 0xffffff00, 0x2a, 1, 5, 0xd0, 0xd0, 0xffffff00, 0x300c009, 1,
		0xab, 0xac, 5, 0, 0, 0, 0, 3, 2, 0xffffffff, 0x1261785c, 0x20f0d,
		0x13b01, 0x90, 0xffffffe3, 0x90, 0xffffffe3, 0x92, 0x93, 0xa0, 0x93, 0xfffffffe, 0, 0x300,
		0, 0x2145f, 0x13948, 0x1e3, 0xfffffecc, 1, 0xffffffff, 0, 0x13f, 3, 0xb47, 0x125ebd38,
		0x125f67f0, 0x125e87d0, 0x41ede714, 0xffffffff, 0, 0x20a00, 0x13200, 0xffffff00, 0x2127c, 0x13a7c, 9, 0x18,
		0x18, 0xffffff00, 0x30, 1, 6, 0x10, 0x10, 0xffffff00,
	];

	private static byte[] Chunk( int type, params int[] dwords )
	{
		var size = 12 + (dwords.Length * 4);

		return new[] { type, size, size }.Concat( dwords ).SelectMany( BitConverter.GetBytes ).ToArray();
	}

	private static byte[] Body( byte[] module )
		=> Encoding.ASCII.GetBytes( "RSYS....SYSR" ).Concat( module ).Concat( Encoding.ASCII.GetBytes( "KARTRYLF" ) ).ToArray();

	private static byte[] Body( params byte[][] chunks )
	{
		var children = Chunk( 2, 0x7cf, 9, 3, 22, 0 ).Concat( chunks.SelectMany( chunk => chunk ) ).ToArray();

		return Body( new[] { 1, 12, 12 + children.Length }.SelectMany( BitConverter.GetBytes ).Concat( children ).ToArray() );
	}

	private static int Counted( string gap ) => Unimplemented.Summary.Where( counted => counted.What == gap ).Sum( counted => counted.Times );

	private static ParkTrackRides Theirs() => new( Body( TheirGo.SelectMany( BitConverter.GetBytes ).ToArray() ) );

	/// <summary>A car's chunk: the handle, 43 words of which these are set, and the five after.</summary>
	private static byte[] Car( int handle, (int Offset, int Value)[] set, params int[] tail )
	{
		var words = new int[43];

		foreach ( var (offset, value) in set )
			words[offset / 4] = value;

		return Chunk( 5, [handle, .. words, .. tail] );
	}

	[TestMethod]
	public void TheOriginalsCarsAreReadWithTheirRiders()
	{
		var read = Theirs();

		Assert.IsNull( read.Problem );
		Assert.AreEqual( 4, read.Cars.Count );
		Assert.AreEqual( 0, read.StrayRiders );
		Assert.AreEqual( 0, read.Listed.Count );

		CollectionAssert.AreEqual( new[] { 31, 39, 42, 48 }, read.Cars.Select( car => car.Riders.Single().Peep ).ToArray() );
		Assert.IsTrue( read.Cars.All( car => car.Riders.Single().Seat == 1 && car.Handle == HotPotHandle && car.RidesBefore == 1 ) );

		var lead = read.Cars[0];

		Assert.AreEqual( 0x340c009, lead.Word( 0x00 ) );
		Assert.AreEqual( (134047, 77741), (lead.Word( 0x34 ), lead.Word( 0x38 )) );
		Assert.AreEqual( 319, lead.Word( 0x88 ) );
		Assert.AreEqual( (0x20a00, 0x13200, HotPotHandle, 0x20187, 0x12987),
			(lead.CentreX, lead.CentreZ, lead.BuoyRide, lead.BuoyX, lead.BuoyZ) );
	}

	[TestMethod]
	public void TheOriginalsGoComesBackAsItWasSaved()
	{
		var cars = new ParkTrackRideTable( Theirs() ).Cars;
		var ride = cars.RideOf( HotPotHandle )!;
		var boats = cars.CarsOf( HotPotHandle ).ToList();

		Assert.AreEqual( (ParkBumperCars.RideState.Running, 750, 4, 4, true), (ride.State, ride.Duration, ride.Cars, ride.Seated, ride.HasLead) );
		Assert.AreEqual( (0x20a00, 0x13200), (ride.CentreX, ride.CentreZ), "the arena is laid round the saved place" );

		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3 }, boats.Select( car => car.Index ).ToArray() );
		CollectionAssert.AreEqual( new[] { (134047, 77741), (131374, 75555), (135682, 75894), (134925, 80641) },
			boats.Select( car => (car.X, car.Z) ).ToArray() );
		CollectionAssert.AreEqual( new[] { (-134, -94), (-116, 29), (104, -72), (144, -29) },
			boats.Select( car => (car.VelocityX, car.VelocityZ) ).ToArray() );
		CollectionAssert.AreEqual( new[] { (-134, -94), (-116, 29), (104, -72), (144, -29) },
			boats.Select( car => (car.SteppedX, car.SteppedZ) ).ToArray() );
		CollectionAssert.AreEqual( new[] { 0x340c009, 0x300c005, 0x300c009, 0x300c009 }, boats.Select( car => (int)car.Flags ).ToArray() );
		CollectionAssert.AreEqual( new[] { (163, 345, 336, 1), (119, 433, 435, 0), (126, 79, 123, -6), (146, 147, 160, -2) },
			boats.Select( car => (car.Speed, car.Steering, car.Heading, car.Turn) ).ToArray() );
		CollectionAssert.AreEqual( new[] { (131787, 76492, 324, 325), (130961, 75638, 130, 153), (133695, 75731, 63, 464), (136287, 80200, 483, -308) },
			boats.Select( car => (car.SteerX, car.SteerZ, car.OffsetX, car.OffsetZ) ).ToArray() );
		CollectionAssert.AreEqual( new[] { 5, 5, 4, 1 }, boats.Select( car => car.Buoy ).ToArray(), "each found again by its place" );
		CollectionAssert.AreEqual( new[] { -1, 0, 1, -1 }, boats.Select( car => car.Chased ).ToArray() );
		CollectionAssert.AreEqual( new[] { 3, 0, 2, 3 }, boats.Select( car => car.Patience ).ToArray(), "a chaser's patience is nought" );
		CollectionAssert.AreEqual( new[] { (1, 5, 768, 319, 2887, false) },
			boats.Select( car => (car.Mesh, car.Animation, car.Radius, car.Timer, car.Phase, car.Smoking) ).Distinct().ToArray() );
		CollectionAssert.AreEqual( new[] { "31@1", "39@1", "42@1", "48@1" },
			boats.Select( car => string.Join( ",", car.Riders.Select( rider => $"{rider.Peep}@{rider.Seat}" ) ) ).ToArray() );

		Assert.IsTrue( boats.All( car => car.Arena == HotPotHandle && car.Voice is null ) );
	}

	/// <summary>
	/// The original, polled a track tick after its load of the same file (<c>q257p/orig/a-load.log</c>): the lead boat
	/// and the fourth, which draw nothing in their first three ticks, and the go's end 319 ticks on.
	/// </summary>
	[TestMethod]
	public void TheGoRunsOnAsTheOriginalsDid()
	{
		var cars = new ParkTrackRideTable( Theirs() ).Cars;
		var boats = cars.CarsOf( HotPotHandle ).ToList();

		(int, int, int, int, int, int, int) Of( ParkBumperCars.Car car )
			=> (car.X, car.Z, car.VelocityX, car.VelocityZ, car.Steering, car.Heading, car.Timer);

		cars.Tick();
		Assert.IsTrue( (boats[1].Flags & ParkBumperCars.CarFlags.Chasing) == 0 || boats[1].Patience == 90,
			"the chaser chose again on its first tick: a buoy, or a new chase with its patience full" );
		Assert.AreEqual( (133910, 77647, -137, -94, 345, 337, 318), Of( boats[0] ) );
		Assert.AreEqual( (135072, 80611, 147, -30, 158, 160, 318), Of( boats[3] ) );

		cars.Tick();
		Assert.AreEqual( (133770, 77553, -140, -94, 345, 338, 317), Of( boats[0] ) );
		Assert.AreEqual( (135222, 80580, 150, -31, 158, 160, 317), Of( boats[3] ) );

		cars.Tick();
		Assert.AreEqual( (133627, 77459, -143, -94, 345, 339, 316), Of( boats[0] ) );
		Assert.AreEqual( (135375, 80548, 153, -32, 158, 160, 316), Of( boats[3] ) );

		for ( var tick = 3; tick < 318; ++tick )
			cars.Tick();

		var ride = cars.RideOf( HotPotHandle )!;

		Assert.AreEqual( (ParkBumperCars.RideState.Running, 4, 0), (ride.State, ride.Seated, ride.Leaving.Count) );

		cars.Tick();

		Assert.AreEqual( (ParkBumperCars.RideState.Loading, 0), (ride.State, ride.Seated) );
		CollectionAssert.AreEqual( new[] { 48, 42, 39, 31 }, ride.Leaving, "head first, as the original's list read" );
	}

	[TestMethod]
	public void ACarsBuoyIsFoundByItsPlaceOrIsTheFirstOrNone()
	{
		const int Centre = 0x20a00, CentreZ = 0x13200;

		var ride = Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 );
		(int, int)[] afloat = [(0x00, 0x300c009), (0x34, Centre), (0x38, CentreZ), (0x7c, 6), (0x2c, -1)];

		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			ride,
			Car( HotPotHandle, afloat, Centre, CentreZ, HotPotHandle, Centre, CentreZ + 0xc00 ),
			Car( HotPotHandle, afloat, Centre, CentreZ, HotPotHandle, Centre + 5, CentreZ + 5 ),
			Car( HotPotHandle, afloat, Centre, CentreZ, 0, Centre, CentreZ + 0xc00 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		CollectionAssert.AreEqual( new[] { 0, 0, -1 }, cars.CarsOf( HotPotHandle ).Select( car => car.Buoy ).ToArray() );

		var second = new ParkTrackRideTable( new ParkTrackRides( Body(
			ride,
			Car( HotPotHandle, afloat, Centre, CentreZ, HotPotHandle, Centre + 2172, CentreZ + 2172 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		Assert.AreEqual( 1, second.CarsOf( HotPotHandle ).Single().Buoy );
	}

	[TestMethod]
	public void ACarOutOfEveryArenaTakesTheOneRoundItsSavedCentre()
	{
		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x40000), (0x38, 0x40000)], 0x20a00, 0x13200, 0, 0, 0 ),
			Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x40000), (0x38, 0x40000)], 0x40000, 0x40000, 0, 0, 0 ),
			Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x20a00), (0x38, 0x13200)], 0x40000, 0x40000, 0, 0, 0 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		CollectionAssert.AreEqual( new[] { HotPotHandle, 0, HotPotHandle }, cars.CarsOf( HotPotHandle ).Select( car => car.Arena ).ToArray(),
			"where it floats first, then the saved centre, then none" );
		Assert.IsFalse( cars.RideOf( HotPotHandle )!.HasLead, "no car of the three is flagged the lead" );
	}

	[TestMethod]
	public void ARidesListsComeBackTurnedRoundAndAShutRideBoardsNobody()
	{
		byte[][] lists =
		[
			Chunk( 7, HotPotHandle, 41 ), Chunk( 7, HotPotHandle, 43 ), Chunk( 8, HotPotHandle, 42 ), Chunk( 8, HotPotHandle, 44 ),
			Chunk( 6, HotPotHandle )
		];

		var read = new ParkTrackRides( Body( [Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ), .. lists] ) );

		CollectionAssert.AreEqual( new[] { (41, false), (43, false), (42, true), (44, true) },
			read.Listed.Select( peep => (peep.Peep, peep.Boarding) ).ToArray() );

		var open = new ParkTrackRideTable( read ).Cars.RideOf( HotPotHandle )!;

		CollectionAssert.AreEqual( new[] { 43, 41 }, open.Leaving );
		CollectionAssert.AreEqual( new[] { 44, 42 }, open.Boarding );

		var shut = new ParkTrackRideTable( new ParkTrackRides( Body(
			[Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 0 ), .. lists] ) ) ).Cars.RideOf( HotPotHandle )!;

		CollectionAssert.AreEqual( new[] { 43, 41 }, shut.Leaving );
		Assert.AreEqual( 0, shut.Boarding.Count, "BUMP 1's call refuses a closed ride" );
	}

	[TestMethod]
	public void ACarOrAListedPeepBeforeItsRidesRecordFindsNoRide()
	{
		var early = Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x20a00), (0x38, 0x13200)], 0x20a00, 0x13200, 0, 0, 0 );

		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			early, Chunk( 9, HotPotHandle, 40, 1 ), Chunk( 7, HotPotHandle, 41 ),
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 1 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		var ride = cars.RideOf( HotPotHandle )!;

		Assert.AreEqual( (0, 0, 0, false), (ride.Cars, ride.Seated, ride.Leaving.Count, ride.HasLead) );
		Assert.AreEqual( 0, cars.CarsOf( HotPotHandle ).Count() );
	}

	/// <summary>
	/// A ride whose BumperType has no record here (a Dino Karts, type -4) has its car, the car's riders and its two
	/// lists dropped at the load, each counted, where the original's car arm (<c>FUN_00543560</c>, chunk 5) restores a
	/// car for any ride its handle names; a Hot Pot's beside it, which has a record, loses and counts nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the car's drop counting nothing; its riders not counted; a listed peep not counted; a Hot Pot's car counted.</remarks>
	[TestMethod]
	public void ARideWithNoRecordHereHasItsCarsRidersAndListsCountedAsTheyAreDropped()
	{
		const int Karts = unchecked((int)0xfffffc00);
		const int HotPot = unchecked((int)0xffffff01);
		string[] gaps = ["SAVED_TRACK_CAR_WITH_NO_RECORD", "SAVED_TRACK_RIDER_WITH_NO_RECORD", "SAVED_TRACK_LISTED_PEEP_WITH_NO_RECORD"];
		var afloat = new[] { (0x00, 0x308c009), (0x34, 0x20a00), (0x38, 0x13200) };

		var read = new ParkTrackRides( Body(
			Chunk( 3, Karts, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ),
			Car( Karts, afloat, 0x20a00, 0x13200, 0, 0, 0 ), Chunk( 9, Karts, 40, 1 ), Chunk( 9, Karts, 41, 2 ),
			Chunk( 7, Karts, 42 ), Chunk( 8, Karts, 43 ), Chunk( 6, Karts ),
			Chunk( 3, HotPot, 0x30400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ),
			Car( HotPot, [(0x00, 0x308c009), (0x34, 0x30a00), (0x38, 0x13200)], 0x30a00, 0x13200, 0, 0, 0 ), Chunk( 9, HotPot, 50, 1 ),
			Chunk( 7, HotPot, 52 ), Chunk( 8, HotPot, 53 ), Chunk( 6, HotPot ) ) );

		Assert.AreEqual( (2, 3, 4), (read.Cars.Count, read.Cars.Sum( car => car.Riders.Count ), read.Listed.Count), "the file as written" );

		var before = gaps.Select( Counted ).ToArray();
		var cars = new ParkTrackRideTable( read ).Cars;

		CollectionAssert.AreEqual( new[] { 1, 2, 2 }, gaps.Select( ( gap, index ) => Counted( gap ) - before[index] ).ToArray(),
			"the kart's car, its two riders and its two listed peeps" );
		Assert.IsNull( cars.RideOf( Karts ) );
		Assert.AreEqual( 0, cars.CarsOf( Karts ).Count() );

		var pot = cars.RideOf( HotPot )!;

		Assert.AreEqual( (1, 1, 1, 1), (pot.Cars, pot.Seated, pot.Leaving.Count, pot.Boarding.Count), "the Hot Pot's kept whole" );
	}

	[TestMethod]
	public void ARiderGoesOnItsRidesLastCarHeadFirstAndOneWithNoCarIsCounted()
	{
		const int Other = 1 | (-1 << 8);

		var afloat = new[] { (0x00, 0x308c009), (0x34, 0x20a00), (0x38, 0x13200) };
		var read = new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ),
			Chunk( 9, HotPotHandle, 39, 1 ),
			Car( HotPotHandle, afloat, 0x20a00, 0x13200, 0, 0, 0 ),
			Car( HotPotHandle, [(0x00, 0x348c009), (0x34, 0x20a00), (0x38, 0x13200)], 0x20a00, 0x13200, 0, 0, 0 ),
			Chunk( 9, Other, 38, 1 ),
			Chunk( 9, HotPotHandle, 40, 1 ), Chunk( 9, HotPotHandle, 41, 2 ),
			Chunk( 6, HotPotHandle ) ) );

		Assert.AreEqual( 2, read.StrayRiders );

		var before = Counted( "SAVED_TRACK_RIDER_WITH_NO_CAR" );
		var cars = new ParkTrackRideTable( read ).Cars;
		var boats = cars.CarsOf( HotPotHandle ).ToList();

		Assert.AreEqual( 2, Counted( "SAVED_TRACK_RIDER_WITH_NO_CAR" ) - before );
		Assert.AreEqual( 0, boats[0].Riders.Count );
		CollectionAssert.AreEqual( new[] { (41, 2), (40, 1) }, boats[1].Riders.Select( rider => (rider.Peep, rider.Seat) ).ToArray() );
		Assert.AreEqual( (2, 2, true), (cars.RideOf( HotPotHandle )!.Cars, cars.RideOf( HotPotHandle )!.Seated, cars.RideOf( HotPotHandle )!.HasLead) );
	}

	/// <summary>A loaded car keeps the handle of its model's record, and a car launched in its pool slot names none.</summary>
	[TestMethod]
	public void ALoadedCarKeepsItsModelsHandleUntilItsSlotIsLaunchedAgain()
	{
		var read = new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ),
			Car( HotPotHandle, [(0x00, 0x308c009), (0x08, 94), (0x0c, 95), (0x34, 0x20a00), (0x38, 0x13200)], 0x20a00, 0x13200, 0, 0, 0 ),
			Car( HotPotHandle, [(0x00, 0x308c009), (0x08, 96), (0x0c, 97), (0x34, 0x20a00), (0x38, 0x13200)], 0x20a00, 0x13200, 0, 0, 0 ),
			Chunk( 6, HotPotHandle ) ) );

		var cars = new ParkTrackRideTable( read ).Cars;

		CollectionAssert.AreEqual( new[] { 94, 96 }, cars.CarsOf( HotPotHandle ).Select( car => car.SavedModel ).ToArray() );
		Assert.AreEqual( 0, Counted( "SAVED_TRACK_CAR_MODEL_RECORDS" ), "the record is read now, not counted" );

		while ( cars.RemoveACar( HotPotHandle ) > 0 )
		{
		}

		Assert.AreEqual( 0, cars.CarsOf( HotPotHandle ).Count() );

		var launched = cars.Launch( HotPotHandle );

		Assert.IsNotNull( launched );
		Assert.AreEqual( 0, launched!.SavedModel );
	}

	[TestMethod]
	public void AShortCarChunkReadsNoughtPastItsEnd()
	{
		var read = new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ),
			Chunk( 5, HotPotHandle, 0x300c009, 1, 7 ),
			Chunk( 6, HotPotHandle ) ) );

		var car = read.Cars.Single();

		Assert.AreEqual( (0x300c009, 1, 7, 0, 0), (car.Word( 0x00 ), car.Word( 0x04 ), car.Word( 0x08 ), car.Word( 0x0c ), car.Word( 0xa8 )) );
		Assert.AreEqual( (0, 0, 0, 0, 0), (car.CentreX, car.CentreZ, car.BuoyRide, car.BuoyX, car.BuoyZ) );
	}

	/// <summary>
	/// Pins the order of the loader's two arena lookups (<c>FUN_00543560</c>, chunk 5; <c>docs/exe/saves.md</c>,
	/// "OpenTPW's reader, a track ride's cars"): the arena is the first that holds where the car floats, and the one
	/// that holds the chunk's centre only failing that. Two rides are seated, slot 0 round (<c>0x20a00</c>,
	/// <c>0x13200</c>) and slot 1 round (<c>0x40a00</c>, <c>0x13200</c>), each the saved place plus half a cell and
	/// <c>0x20000</c> apart, far past the arena's <c>0x1200</c>. The car floats at the second's centre and its chunk
	/// gives the first's, so both lookups find an arena and the car's is the second.
	/// </summary>
	[TestMethod]
	public void ACarTakesTheArenaItFloatsInBeforeTheOneRoundItsSavedCentre()
	{
		const int Other = 1 | (-1 << 8);

		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Chunk( 3, Other, 0x40400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x40a00), (0x38, 0x13200)], 0x20a00, 0x13200, 0, 0, 0 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		Assert.AreEqual( (0x20a00, 0x13200), (cars.RideOf( HotPotHandle )!.CentreX, cars.RideOf( HotPotHandle )!.CentreZ) );
		Assert.AreEqual( (0x40a00, 0x13200), (cars.RideOf( Other )!.CentreX, cars.RideOf( Other )!.CentreZ) );
		Assert.AreEqual( Other, cars.CarsOf( HotPotHandle ).Single().Arena, "where it floats is looked up before the saved centre" );
	}

	/// <summary>
	/// Pins the buoy of a car whose chunk names a ride the table does not hold (<c>FUN_00543560</c>, chunk 5,
	/// <c>0x00543d9d</c>..<c>0x00543e1d</c>; <c>docs/exe/saves.md</c>, "OpenTPW's reader, a track ride's cars": -1
	/// where the chunk names no ride, else the id of the named ride's buoy on the chunk's place, nought where none
	/// does). The chunk's buoy ride is slot 1's handle and only slot 0 is seated: the loader walks the named slot's
	/// buoy list, which is empty, so no buoy lies on the place and the id is nought - not -1, since a ride is named,
	/// and the load goes through.
	/// </summary>
	[TestMethod]
	public void ACarWhoseBuoyRideIsAnEmptySlotTakesTheRingsFirstBuoy()
	{
		const int Other = 1 | (-1 << 8);

		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 ),
			Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x20a00), (0x38, 0x13200), (0x7c, 6)], 0x20a00, 0x13200, Other, 0x20a00, 0x13200 + 0xc00 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		Assert.IsNull( cars.RideOf( Other ), "slot 1 holds no ride" );
		Assert.AreEqual( 0, cars.CarsOf( HotPotHandle ).Single().Buoy );
	}

	/// <summary>
	/// Pins that a loaded rider takes one node of the pool every ride's lists share (<c>FUN_00543560</c>, chunk 9,
	/// <c>0x00543f24</c>: the head of the free list; <c>docs/exe/saves.md</c>, "OpenTPW's reader, a track ride's
	/// cars"). The pool is <c>0x5000</c> bytes of <c>0x14</c> a node, 1024 (<c>FUN_00544360</c>), and <c>BUMP 1</c>
	/// boards a peep only with a free node (<c>docs/exe/park.md</c>, "How a bumper ride ends a go", selector 1). Two
	/// riders are read onto a car of a ride that is loading, so 1024 - 2 = 1022 peeps board and the next is refused.
	/// </summary>
	[TestMethod]
	public void EachLoadedRiderTakesANodeOfThePoolEveryRideShares()
	{
		var cars = new ParkTrackRideTable( new ParkTrackRides( Body(
			Chunk( 3, HotPotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 4350, 1 ),
			Car( HotPotHandle, [(0x00, 0x300c009), (0x34, 0x20a00), (0x38, 0x13200)], 0x20a00, 0x13200, 0, 0, 0 ),
			Chunk( 9, HotPotHandle, 40, 1 ), Chunk( 9, HotPotHandle, 41, 2 ),
			Chunk( 6, HotPotHandle ) ) ) ).Cars;

		Assert.AreEqual( 2, cars.CarsOf( HotPotHandle ).Single().Riders.Count );

		for ( var boarder = 0; boarder < ParkBumperCars.ListNodes - 2; ++boarder )
			Assert.IsTrue( cars.Board( HotPotHandle, 1000 + boarder ), $"boarder {boarder} has a node" );

		Assert.IsFalse( cars.Board( HotPotHandle, 5000 ), "the two riders hold the pool's last two nodes" );
		Assert.AreEqual( 1022, cars.RideOf( HotPotHandle )!.Boarding.Count );
	}
}
