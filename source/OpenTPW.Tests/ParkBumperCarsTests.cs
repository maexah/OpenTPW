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

	/// <summary>
	/// The performance lerps the template's four ranges: at 60, the speed word every bumper ride is bought at, the
	/// record the original's memory read (thrust 10, friction 990, turn 11, restitution 1060); at the template's 50 before.
	/// </summary>
	[TestMethod]
	public void ThePerformanceLerpsTheTemplatesRanges()
	{
		var (table, handle) = Ride();
		var ride = table.Cars.RideOf( handle )!;

		Assert.AreEqual( (50, 10, 985, 11, 1050), (ride.Performance, ride.Thrust, ride.Friction, ride.TurnRate, ride.Restitution) );

		table.Cars.BindSpeedWord( handle, 60 );
		table.Cars.PushSpeedWords();

		Assert.AreEqual( (60, 10, 990, 11, 1060), (ride.Performance, ride.Thrust, ride.Friction, ride.TurnRate, ride.Restitution) );

		table.Cars.SetPerformance( handle, 140 );
		Assert.AreEqual( (100, 12, 1010, 14, 1100), (ride.Performance, ride.Thrust, ride.Friction, ride.TurnRate, ride.Restitution), "clamped to 100" );
	}

	/// <summary>
	/// A heading from an offset: (sin, cos) in (x, z), so +z heads 0 and +x 127, the constant being just over 1/π and
	/// the result truncated; and no offset at all heads 256, as the binary's negated integer loads +0, which a bump
	/// between two boats at rest reads.
	/// </summary>
	[TestMethod]
	public void AHeadingFromAnOffset()
	{
		Assert.AreEqual( 0, ParkBumperCars.HeadingOf( 0, 1000 ) & 0x1ff );
		Assert.AreEqual( 127, ParkBumperCars.HeadingOf( 1000, 0 ) );
		Assert.AreEqual( 256, ParkBumperCars.HeadingOf( 0, 0 ) );
	}

	/// <summary>The Hot Pot's eight buoys, as <c>park.md</c> lists them from the laying's arithmetic.</summary>
	[TestMethod]
	public void TheBuoysRingTheArena()
	{
		var (table, handle) = Ride();

		CollectionAssert.AreEqual(
			new[] { (0, 3072), (2172, 2172), (3071, 0), (2171, -2171), (0, -3069), (-2169, -2169), (-3067, 0), (-2168, 2168) },
			table.Cars.RideOf( handle )!.Buoys.ToArray() );
	}

	/// <summary>
	/// A boat with a rider drives in a go; a boat with none, on a ride of its own, never moves, nor does a filled boat
	/// waiting for its go - the thrust needs <c>0x4000</c> and not <c>0x80000</c>.
	/// </summary>
	[TestMethod]
	public void OnlyABoatWithARiderInAGoDrives()
	{
		var table = new ParkTrackRideTable();
		var cars = table.Cars;
		var full = table.Take( HotPot );
		var empty = table.Take( HotPot );

		cars.Place( full, Centre, Centre );
		cars.Place( empty, Centre + 20 * ParkBumperCars.CellUnits, Centre );

		foreach ( var handle in new[] { full, empty } )
		{
			cars.OpenForLoading( handle );
			cars.Launch( handle );
		}

		Assert.IsTrue( cars.Board( full, 101 ) );
		Assert.IsTrue( cars.Fill( full ) );

		var driver = cars.CarsOf( full ).Single();
		var idle = cars.CarsOf( empty ).Single();
		var waitingAt = (driver.X, driver.Z);

		for ( var tick = 0; tick < 100; ++tick )
			cars.Tick();

		Assert.AreEqual( waitingAt, (driver.X, driver.Z), "a filled boat waiting for its go stands still" );

		cars.Start( full );
		cars.Start( empty );
		cars.Tick();

		// Pointed straight at the point it steers at, so only the flags stand between it and the thrust.
		idle.Steering = ParkBumperCars.HeadingOf( idle.SteerX - idle.X, idle.SteerZ - idle.Z );

		var restAt = (idle.X, idle.Z);
		var fastest = 0;

		for ( var tick = 0; tick < 700; ++tick )
		{
			cars.Tick();
			fastest = System.Math.Max( fastest, driver.Speed );

			Assert.AreEqual( 0, idle.Speed, $"the empty boat drifts only when struck, tick {tick}" );
		}

		Assert.AreEqual( restAt, (idle.X, idle.Z) );
		Assert.IsTrue( fastest > 150 && fastest <= 247, $"the boat with a rider drives, under the steady 247: {fastest}" );
	}

	/// <summary>
	/// Four boats with riders through a whole go: never past the rim, never over the top speed, bumping each other, and
	/// let off after 750 ticks.
	/// </summary>
	[TestMethod]
	public void AFullGoStaysInThePotUnderTopSpeedAndBumps()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		var ride = cars.RideOf( handle )!;

		cars.BindSpeedWord( handle, 60 );
		cars.PushSpeedWords();
		cars.OpenForLoading( handle );

		for ( var i = 0; i < 4; ++i )
			cars.Launch( handle );

		for ( var i = 0; i < 4; ++i )
		{
			Assert.IsTrue( cars.Board( handle, 101 + i ) );
			Assert.IsTrue( cars.Fill( handle ) );
		}

		cars.SetDuration( handle, 750 );
		cars.Start( handle );

		var fastest = 0;
		var bumps = 0;
		var chases = 0;

		for ( var tick = 0; tick < 750; ++tick )
		{
			cars.Tick();

			foreach ( var car in cars.CarsOf( handle ) )
			{
				fastest = System.Math.Max( fastest, car.Speed );

				if ( (car.Flags & ParkBumperCars.CarFlags.Bumped) != 0 )
					++bumps;

				if ( (car.Flags & ParkBumperCars.CarFlags.Chasing) != 0 )
					++chases;

				Assert.IsTrue( ParkBumperCars.Distance( car.X - Centre, car.Z - Centre ) <= ride.ArenaRadius, $"in the pot at tick {tick}" );
			}
		}

		Assert.IsTrue( fastest <= 247, $"top speed {fastest}" );
		Assert.IsTrue( bumps > 0, "the boats bump" );
		Assert.IsTrue( chases > 0, "the boats chase" );
		Assert.AreEqual( ParkBumperCars.RideState.Loading, ride.State, "the go over after 750 ticks" );
		Assert.AreEqual( 4, ride.Leaving.Count );
	}

	/// <summary>
	/// A closing pair meets twice from one snapshot: the struck boat is kicked along the line between them, and the
	/// second meeting, from its side, kicks the first back, so a boat driving into one at rest stops and sends it on.
	/// </summary>
	[TestMethod]
	public void ABumpKicksTheStruckBoatOnAndStopsTheOther()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );

		var a = cars.Launch( handle )!;
		var b = cars.Launch( handle )!;

		(a.X, a.Z, a.VelocityX, a.VelocityZ) = (Centre - 500, Centre, 200, 0);
		(b.X, b.Z, b.VelocityX, b.VelocityZ) = (Centre + 500, Centre, 0, 0);

		cars.Tick();

		Assert.IsTrue( b.VelocityX > 150, $"struck and sent on: {b.VelocityX}" );
		Assert.IsTrue( System.Math.Abs( a.VelocityX ) < 10, $"stopped: {a.VelocityX}" );
		Assert.AreEqual( ParkBumperCars.CarFlags.Bumped, b.Flags & ParkBumperCars.CarFlags.Bumped );
		Assert.AreEqual( ParkBumperCars.CarFlags.Bumped, a.Flags & ParkBumperCars.CarFlags.Bumped, "struck in turn at the second meeting" );
	}

	/// <summary>A boat past the rim is put back on it, its velocity turned inward.</summary>
	[TestMethod]
	public void TheRimReflectsABoat()
	{
		var (table, handle) = Ride();
		var cars = table.Cars;
		cars.OpenForLoading( handle );

		var car = cars.Launch( handle )!;
		(car.X, car.Z, car.VelocityX, car.VelocityZ) = (Centre + 0x1200 - 50, Centre, 200, 0);

		cars.Tick();

		Assert.AreEqual( Centre + 0x1200, car.X, "on the rim" );
		Assert.AreEqual( Centre, car.Z );
		Assert.IsTrue( car.VelocityX < -150, $"reflected inward: {car.VelocityX}" );
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

	/// <summary>
	/// A car's height before its bob is the average of the scene's under its four corners, the ride's car radius from
	/// its middle: ahead, behind and to each side (<c>docs/exe/park.md</c>, "Where a bumper ride's cars float"). The
	/// Hot Pot's 768 reaches 255 × 4 × 768 / 1024 of a 3072nd of a cell, 2.49 units.
	/// </summary>
	[TestMethod]
	public void ACarStandsAtTheAverageOfItsFourCorners()
	{
		var asked = new List<(float X, float Y)>();

		float Under( float x, float y )
		{
			asked.Add( (x, y) );
			return asked.Count switch { 1 => 10f, 2 => 20f, 3 => 40f, _ => 80f };
		}

		var reach = 255f * 4f * 768f / 1024f / 3072f * 10f;

		// Heading nought: the sine is nought and its quarter-turn 255, so "ahead" lies along y.
		Assert.AreEqual( 37.5f, ParkBumperBoats.Float( 400f, 200f, 0, 768, 10f, 10f, Under ), 0.0001f );

		(float, float)[] want = [(400f, 200f + reach), (400f, 200f - reach), (400f - reach, 200f), (400f + reach, 200f)];

		for ( var i = 0; i < 4; ++i )
		{
			Assert.AreEqual( want[i].Item1, asked[i].X, 0.001f, $"corner {i} x" );
			Assert.AreEqual( want[i].Item2, asked[i].Y, 0.001f, $"corner {i} y" );
		}

		// A quarter turn on: "ahead" lies along x, and a cell half as deep halves the reach along y.
		asked.Clear();
		ParkBumperBoats.Float( 400f, 200f, 0x80, 768, 10f, 5f, Under );

		Assert.AreEqual( 400f + reach, asked[0].X, 0.001f );
		Assert.AreEqual( 200f, asked[0].Y, 0.02f );
		Assert.AreEqual( 400f, asked[2].X, 0.02f );
		Assert.AreEqual( 200f + (reach / 2f), asked[2].Y, 0.001f );

		asked.Clear();
		ParkBumperBoats.Float( 400f, 200f, 0, 768, 10f, 5f, Under );

		Assert.AreEqual( 200f + (reach / 2f), asked[0].Y, 0.001f );
		Assert.AreEqual( 400f - reach, asked[2].X, 0.001f );
	}
}

/// <summary>A rider in a boat is drawn as a head: which bank, and that every head bank the park can need is packed.</summary>
[TestClass]
public class RiderHeadTests
{
	[TestMethod]
	public void AChildRidesAsTheirOwnHeadAndACostumeAsItsHead()
	{
		Assert.AreEqual( (ParkSpriteBanks.KidHeadKind, 5), ParkGuestSprites.HeadOf( ParkSpriteBanks.ChildKind, 5 ) );
		Assert.AreEqual( (ParkSpriteBanks.CostumeHeadKind, 0), ParkGuestSprites.HeadOf( ParkSpriteBanks.CostumeKind, 0 ) );
	}

	/// <summary>
	/// <c>FUN_0044b510</c>'s pick, against directions whose answer the picture grid shows: from straight above the crown
	/// (0), level and in front (24), each −45° turn about up the next column, from below the chin (48).
	/// </summary>
	[TestMethod]
	public void AHeadShowsTheCameraThePictureForItsDirection()
	{
		static System.Numerics.Vector3 At( float tiltDegrees, float turnDegrees )
		{
			var tilt = tiltDegrees * System.MathF.PI / 180f;
			var turn = turnDegrees * System.MathF.PI / 180f;
			return new( System.MathF.Sin( tilt ) * System.MathF.Sin( turn ), System.MathF.Cos( tilt ), System.MathF.Sin( tilt ) * System.MathF.Cos( turn ) );
		}

		Assert.AreEqual( 0, ParkBumperBoats.HeadFrame( new( 0, 1, 0 ) ), "above" );
		Assert.AreEqual( 0, ParkBumperBoats.HeadFrame( At( 5, -90 ) ), "nearly above keeps column 0" );
		Assert.AreEqual( 24, ParkBumperBoats.HeadFrame( new( 0, 0, 1 ) ), "level, in front" );
		Assert.AreEqual( 25, ParkBumperBoats.HeadFrame( At( 90, -45 ) ) );
		Assert.AreEqual( 26, ParkBumperBoats.HeadFrame( new( -1, 0, 0 ) ) );
		Assert.AreEqual( 30, ParkBumperBoats.HeadFrame( new( 1, 0, 0 ) ) );
		Assert.AreEqual( 20, ParkBumperBoats.HeadFrame( At( 60, -180 ) ), "row 2, column 4" );
		Assert.AreEqual( 48, ParkBumperBoats.HeadFrame( new( 0, -1, 0 ) ), "below" );
		Assert.AreEqual( 9, ParkBumperBoats.HeadFrame( At( 32, -50 ) ), "the nearest of the 56" );
	}

	[TestMethod]
	public void EveryChildsAndCostumesHeadIsPacked()
	{
		var counts = new ParkSpriteBanks( KidBanks: 6, CostumeBanks: 1, BalloonSets: 4 );
		var packed = ParkGuestSprites.BanksToPack( [], counts ).ToHashSet();

		for ( var bank = 0; bank < 6; ++bank )
			Assert.IsTrue( packed.Contains( (ParkSpriteBanks.KidHeadKind, bank) ), $"kid head {bank}" );

		Assert.IsTrue( packed.Contains( (ParkSpriteBanks.CostumeHeadKind, 0) ), "the costume's head" );
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
