using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The bumper family's record and cars - <see cref="ParkBumperCars"/> - and the Hot Pot's own script driving them through
/// <c>BUMP</c>. See <c>docs/exe/park.md</c>, "How a bumper ride ends a go, and lets its riders off" and "Where a bumper
/// ride's cars float".
/// </summary>
[TestClass]
public class ParkBumperCarsTests
{
	[TestInitialize]
	public void StartTheLog() => Log ??= new();

	/// <summary>The Hot Pot's BumperType.</summary>
	private const int HotPot = -1;

	private const int Centre = 30 * ParkBumperCars.CellUnits + ParkBumperCars.CellUnits / 2;

	private static (ParkTrackRideTable Table, int Handle) Ride()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( HotPot );

		table.Cars.Place( handle, Centre, Centre );

		return (table, handle);
	}

	[TestMethod]
	public void AHotPotOpensClosedWithItsTemplate()
	{
		var (table, handle) = Ride();
		var ride = table.Cars.RideOf( handle )!;

		Assert.AreEqual( ParkBumperCars.RideState.Closed, ride.State );
		Assert.AreEqual( 4350, ride.Duration, "the template's duration" );
		Assert.AreEqual( 8, ride.MostCars );
		Assert.AreEqual( 0x1200, ride.ArenaRadius );

		// Closed, nothing launches and nobody boards.
		Assert.IsNull( table.Cars.Launch( handle ) );
		Assert.IsFalse( table.Cars.Board( handle, 101 ) );
	}

	[TestMethod]
	public void CarsLaunchInsideTheArenaAndClearOfEachOther()
	{
		var (table, handle) = Ride();
		table.Cars.OpenForLoading( handle );

		var cars = Enumerable.Range( 0, 4 ).Select( _ => table.Cars.Launch( handle ) ).ToArray();

		Assert.IsTrue( cars.All( car => car is not null ), "four cars" );
		Assert.AreEqual( 4, table.Cars.CarCount( handle ) );

		foreach ( var car in cars )
		{
			var dx = car!.X - Centre;
			var dz = car.Z - Centre;

			Assert.IsTrue( System.Math.Sqrt( (double)dx * dx + (double)dz * dz ) <= 0x1200, $"inside the arena: {dx},{dz}" );
			Assert.AreEqual( 1, car.Mesh, "the template's mesh 1, b_car" );
			Assert.AreEqual( RideAnimations.NoRole, car.Animation, "at rest" );
			Assert.AreEqual( ParkBumperCars.CarFlags.None, car.Flags & ParkBumperCars.CarFlags.Active, "a new car's retarget clears its active flag" );
			Assert.AreNotEqual( ParkBumperCars.CarFlags.None, car.Flags & ParkBumperCars.CarFlags.Bobs, "a Hot Pot car bobs" );

			foreach ( var other in cars.Where( other => other != car ) )
			{
				var ox = (double)(other!.X - car.X);
				var oz = (double)(other.Z - car.Z);

				Assert.IsTrue( System.Math.Sqrt( ox * ox + oz * oz ) >= 768 * 2, "clear of every other car" );
			}
		}

		// The template allows eight, and no more.
		for ( var i = 4; i < 8; ++i )
			Assert.IsNotNull( table.Cars.Launch( handle ) );

		Assert.IsNull( table.Cars.Launch( handle ), "a ninth" );
	}

	/// <summary>
	/// The whole go, as the Hot Pot's script drives it with a duration of 25: a rider boards, fills the first empty car,
	/// the go starts, and 750 track ticks later the car unloads onto the leaving list and the ride reads loading again.
	/// </summary>
	[TestMethod]
	public void AGoEndsAfterItsDurationInTicksAndLetsTheRiderOff()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );

		for ( var i = 0; i < 4; ++i )
			cars.Launch( handle );

		Assert.IsTrue( cars.Board( handle, 101 ) );
		Assert.IsTrue( cars.Fill( handle ) );

		var ride = cars.RideOf( handle )!;
		var seated = cars.CarsOf( handle ).Single( car => car.Riders.Count > 0 );

		Assert.AreEqual( 1, ride.Seated );
		Assert.AreEqual( 1, seated.Riders.Single().Seat, "seat 1, the b_car's one" );

		cars.SetDuration( handle, 25 * ParkBumperCars.TicksPerDurationUnit );
		cars.Start( handle );

		Assert.AreEqual( ParkBumperCars.RideState.Running, ride.State );
		Assert.AreEqual( 5, seated.Animation, "a car with a rider rocks in the go" );
		Assert.IsTrue( cars.CarsOf( handle ).Where( car => car != seated ).All( car => car.Animation == RideAnimations.NoRole ),
			"an empty car stays at rest" );

		for ( var tick = 1; tick < 750; ++tick )
			cars.Tick();

		Assert.AreEqual( 0, cars.TakeLeaving( handle ), "nobody off before the 750th tick" );
		Assert.AreEqual( ParkBumperCars.RideState.Running, ride.State );

		cars.Tick();

		Assert.AreEqual( ParkBumperCars.RideState.Loading, ride.State, "loading again once nobody is seated" );
		Assert.AreEqual( 0, ride.Seated );
		Assert.AreEqual( RideAnimations.NoRole, seated.Animation );
		Assert.AreEqual( 101, cars.TakeLeaving( handle ), "the rider let off" );
		Assert.AreEqual( 0, cars.TakeLeaving( handle ) );
	}

	[TestMethod]
	public void AGoOfNoTicksNeverUnloads()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );
		cars.Launch( handle );
		cars.Board( handle, 101 );
		cars.Fill( handle );
		cars.SetDuration( handle, 0 );
		cars.Start( handle );

		for ( var tick = 0; tick < 2000; ++tick )
			cars.Tick();

		Assert.AreEqual( 0, cars.TakeLeaving( handle ), "a timer started at nought goes to -1 and never counts again" );
		Assert.AreEqual( ParkBumperCars.RideState.Running, cars.RideOf( handle )!.State );

		// Only the close unloads it.
		cars.Shut( handle );
		cars.Tick();

		Assert.AreEqual( 101, cars.TakeLeaving( handle ) );
	}

	[TestMethod]
	public void ABrokenRideFreezesItsTimers()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );
		cars.Launch( handle );
		cars.Board( handle, 101 );
		cars.Fill( handle );
		cars.SetDuration( handle, 10 );
		cars.Start( handle );
		cars.Break( handle );

		for ( var tick = 0; tick < 50; ++tick )
			cars.Tick();

		Assert.AreEqual( 0, cars.TakeLeaving( handle ) );

		cars.Fix( handle );

		for ( var tick = 0; tick < 10; ++tick )
			cars.Tick();

		Assert.AreEqual( 101, cars.TakeLeaving( handle ) );
	}

	/// <summary>
	/// A go ending with two cars seated: the first unloads while the second still holds a rider, so it is still flagged
	/// to unload when <c>BUMP 12</c> refills it in the same step. Its retarget clears the flag, so the new rider stays.
	/// </summary>
	[TestMethod]
	public void ACarRefilledAsTheGoEndsKeepsItsNewRider()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );
		cars.Launch( handle );
		cars.Launch( handle );

		cars.Board( handle, 101 );
		cars.Fill( handle );
		cars.Board( handle, 102 );
		cars.Fill( handle );
		cars.SetDuration( handle, 10 );
		cars.Start( handle );

		for ( var tick = 0; tick < 10; ++tick )
			cars.Tick();

		var first = cars.CarsOf( handle ).First();

		Assert.AreNotEqual( ParkBumperCars.CarFlags.None, first.Flags & ParkBumperCars.CarFlags.Unloading,
			"the first car still flagged: the second held a rider when it unloaded" );

		cars.Board( handle, 103 );
		Assert.IsTrue( cars.Fill( handle ) );
		Assert.AreEqual( 103, first.Riders.Single().Peep, "the first empty car is the one still flagged" );

		cars.Tick();

		Assert.AreEqual( 103, first.Riders.SingleOrDefault()?.Peep, "the retarget cleared the unload" );
	}

	/// <summary>The quirk copied: <c>BUMP 16</c> answers nought after removing an occupied car.</summary>
	[TestMethod]
	public void RemovingACarAnswersNoughtWhenEveryCarCarriesSomebody()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );
		cars.Launch( handle );
		cars.Launch( handle );

		Assert.AreEqual( 1, cars.RemoveACar( handle ), "an empty car" );

		cars.Board( handle, 101 );
		cars.Fill( handle );

		Assert.AreEqual( 0, cars.RemoveACar( handle ), "the occupied one, answering nought" );
		Assert.AreEqual( 0, cars.CarCount( handle ) );
		Assert.AreEqual( 101, cars.TakeLeaving( handle ), "its rider to the leaving list" );
		Assert.AreEqual( ParkBumperCars.RideState.Loading, cars.RideOf( handle )!.State, "the last car gone: loading" );
	}

	/// <summary>The quirk copied: a closed ride with cars reads loading again on the next tick.</summary>
	[TestMethod]
	public void AClosedRideWithCarsReadsLoadingOnTheNextTick()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );
		cars.Launch( handle );
		cars.Board( handle, 102 );
		cars.Shut( handle );

		Assert.AreEqual( ParkBumperCars.RideState.Closed, cars.RideOf( handle )!.State );
		Assert.AreEqual( 102, cars.TakeLeaving( handle ), "the boarding list onto the leaving list" );

		cars.Tick();

		Assert.AreEqual( ParkBumperCars.RideState.Loading, cars.RideOf( handle )!.State );
	}

	[TestMethod]
	public void FreeingTheSlotTakesItsCars()
	{
		var (table, handle) = Ride();
		table.Cars.OpenForLoading( handle );
		table.Cars.Launch( handle );
		table.Cars.Launch( handle );

		table.Free( handle );

		Assert.IsNull( table.Cars.RideOf( handle ) );
		Assert.IsFalse( table.Cars.All.Any( car => car.IsLive ) );
	}

	[TestMethod]
	public void OnlyTheBumperFamilyHasARecord()
	{
		var table = new ParkTrackRideTable();
		var karts = table.Take( -4 );

		Assert.IsTrue( table.Holds( karts ) );
		Assert.IsNull( table.Cars.RideOf( karts ), "the go-karts' cars steer to buoys, not built" );
	}

	/// <summary>The sine table <c>FUN_00544360</c> fills: 512 steps, its rounded constant truncating the peaks to 255.</summary>
	[TestMethod]
	public void TheSineTableIsTheTrackSystems()
	{
		Assert.AreEqual( 0, ParkBumperCars.Sine( 0 ) );
		Assert.AreEqual( 255, ParkBumperCars.Sine( 128 ), "255.99999999977, truncated" );
		Assert.AreEqual( 0, ParkBumperCars.Sine( 256 ) );
		Assert.AreEqual( -255, ParkBumperCars.Sine( 384 ) );
		Assert.AreEqual( 181, ParkBumperCars.Sine( 64 ), "sin 45 degrees times 256, 181.02, truncated" );
		Assert.AreEqual( ParkBumperCars.Sine( 3 ), ParkBumperCars.Sine( 515 ), "wraps at 512" );
	}
}

/// <summary>The Hot Pot read from the game: its item's arena and its own script driving the cars through <c>BUMP</c>.</summary>
[TestClass]
public class HotPotScriptTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int HotPotId = 1140;

	private ParkItemCatalogue.Item HotPot()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );

		Assert.IsTrue( catalogue.TryGet( HotPotId, out var item ), "the Hot Pot" );

		return item;
	}

	/// <summary>
	/// The arena's centre, worked from the item's adjusts and the placer's offsets, is the middle cell of the 5 by 5
	/// footprint at every turn - where the pot stands in the model, (25, 25).
	/// </summary>
	[TestMethod]
	public void TheArenaIsTheMiddleOfTheFootprintAtEveryTurn()
	{
		var item = HotPot();

		Assert.AreEqual( 5, item.Width );
		Assert.AreEqual( 5, item.Depth );
		Assert.AreEqual( "b_car.md2", item.SupplementalMeshes![1] );
		Assert.AreEqual( "b_wake.md2", item.SupplementalMeshes[0] );

		foreach ( var angle in new[] { 0, 90, 180, 270 } )
		{
			var (left, top, right, bottom) = ParkObjects.FootprintAt( item, 57, 23, angle );
			var (x, z) = ParkBumperCars.ArenaCentre( item, 57, 23, angle );

			Assert.AreEqual( (left + right) / 2 * ParkBumperCars.CellUnits + ParkBumperCars.CellUnits / 2, x, $"x at {angle}" );
			Assert.AreEqual( (top + bottom) / 2 * ParkBumperCars.CellUnits + ParkBumperCars.CellUnits / 2, z, $"z at {angle}" );
		}
	}

	private RideScript Script( ParkTrackRideTable table, int handle )
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bumper/bumper.RSE" );

		var script = new RideScript( new RideScriptFile( stream ) )
		{
			Bumpers = table.Cars,
			TrackRide = handle
		};

		script.Set( ParkRideOperation.CapacityVariable, 4 );
		script.Set( ParkRideOperation.DurationVariable, 25 );

		return script;
	}

	/// <summary>
	/// The Hot Pot's own script, turn by turn: it opens the ride and launches a boat per unit of capacity, takes four
	/// riders, starts the go, and after 750 track ticks lets all four off through <c>VAR_LETMEOFF</c>, never more than
	/// four aboard.
	/// </summary>
	[TestMethod]
	public void TheScriptLaunchesFourBoatsAndLetsFourRidersOff()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );
		var script = Script( table, handle );
		var cars = table.Cars;

		var now = 0f;
		var tick = 0;

		void Step()
		{
			cars.Tick();

			if ( (tick++ & 7) == 0 )
				script.Turn( now );

			now += 31f;
		}

		for ( var i = 0; i < 64; ++i )
			Step();

		Assert.AreEqual( 4, cars.CarCount( handle ), "a boat per unit of capacity" );
		Assert.AreEqual( 4, script["VAR_CARS"] );
		Assert.AreEqual( 0, cars.CarsOf( handle ).Sum( car => car.Riders.Count ), "the boats float empty" );

		// Four guests admitted, one at a time, as the engine writes VAR_LETMEON.
		var waiting = new Queue<int>( [201, 202, 203, 204] );
		var off = new List<int>();
		var mostAboard = 0;
		var started = -1;

		for ( var i = 0; i < 4000 && off.Count < 4; ++i )
		{
			if ( script["VAR_LETMEON"] == 0 && waiting.Count > 0 )
				script.Set( "VAR_LETMEON", waiting.Dequeue() );

			Step();

			if ( started < 0 && cars.RideOf( handle )!.State == ParkBumperCars.RideState.Running )
				started = tick;

			mostAboard = System.Math.Max( mostAboard, cars.RideOf( handle )!.Seated );

			if ( script["VAR_LETMEOFF"] is var leaving and not 0 )
			{
				off.Add( leaving );
				script.Set( "VAR_LETMEOFF", 0 );
			}
		}

		CollectionAssert.AreEquivalent( new[] { 201, 202, 203, 204 }, off, "all four let off" );
		Assert.AreEqual( 4, mostAboard, "never more than four aboard" );
		Assert.IsTrue( started > 0, "the go started" );
		Assert.AreEqual( 750, cars.RideOf( handle )!.Duration, "VAR_DURATION 25 x 30 track ticks, set by BUMP 13" );
	}
}
