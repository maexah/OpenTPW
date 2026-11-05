using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The Hot Pot's car step - <c>Bumper_StepCar</c>, <c>FUN_00547f50</c> (<see cref="ParkBumperCars"/>): the friction a
/// car loses its velocity by (seven tenths of the ride's while seating, 1000 for a broken ride) and the turn it takes
/// toward its steering heading while the ride runs. Each is found by putting one launched car in a state and
/// stepping the track once (<c>docs/exe/park.md</c>, "How a bumper ride ends a go, and lets its riders off").
/// </summary>
[TestClass]
public class ParkBumperCarStepTests
{
	[TestInitialize]
	public void StartTheLog() => Log ??= new();

	private const int HotPot = -1;

	private const int Centre = 30 * ParkBumperCars.CellUnits + ParkBumperCars.CellUnits / 2;

	/// <summary>The ride's friction at the speed word every bumper ride is bought at: the lerp of 960 and 1010 at 60.</summary>
	private const int FrictionAt60 = 990;

	/// <summary>
	/// One lone car on a Hot Pot at performance 60, its flags set to <paramref name="flags"/> alone, moving
	/// (<paramref name="vx"/>, <paramref name="vz"/>) with nothing to chase, bump or thrust it.
	/// </summary>
	private static (ParkBumperCars Cars, ParkBumperCars.Ride Ride, ParkBumperCars.Car Car) Lone( ParkBumperCars.CarFlags flags, int vx, int vz )
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( HotPot );

		table.Cars.Place( handle, Centre, Centre );
		table.Cars.OpenForLoading( handle );
		table.Cars.PushSpeedWord( handle, 60 );

		var car = table.Cars.Launch( handle )!;
		var ride = table.Cars.RideOf( handle )!;

		Assert.AreEqual( FrictionAt60, ride.Friction, "the lerp the tests below read their friction from" );

		car.Flags = ParkBumperCars.CarFlags.Live | flags;
		car.Timer = -1;

		// Launch drops the car at a random place and heading; the step must not carry it out of the arena.
		(car.X, car.Z, car.Heading, car.Steering, car.Turned, car.Turn) = (ride.CentreX, ride.CentreZ, 0, 0, 0, 0);
		car.VelocityX = vx;
		car.VelocityZ = vz;

		return (table.Cars, ride, car);
	}

	/// <summary>Seating, the velocity is kept at 693 of 1024 (990 × 7 / 10), and the car moves by what is left.</summary>
	[TestMethod]
	public void ASeatingCarKeepsSevenTenthsOfTheRidesFriction()
	{
		var (cars, _, car) = Lone( ParkBumperCars.CarFlags.Seating, 2048, 1024 );
		var (x, z) = (car.X, car.Z);

		cars.Tick();

		Assert.AreEqual( 693 * 2048 / 1024, car.VelocityX );
		Assert.AreEqual( 693 * 1024 / 1024, car.VelocityZ );
		Assert.AreEqual( (car.VelocityX, car.VelocityZ), (car.SteppedX, car.SteppedZ), "the bumps read the stepped velocity" );
		Assert.AreEqual( (int)System.Math.Sqrt( 1386.0 * 1386 + 693.0 * 693 ), car.Speed );
		Assert.AreEqual( (x + 1386, z + 693), (car.X, car.Z), "moved by the velocity after friction" );
	}

	/// <summary>A car on a running ride that is not seating keeps the ride's own friction: 990 of 1024.</summary>
	[TestMethod]
	public void ACarNotSeatingKeepsTheRidesOwnFriction()
	{
		var (cars, _, car) = Lone( ParkBumperCars.CarFlags.None, 2048, 1024 );

		cars.Tick();

		Assert.AreEqual( FrictionAt60 * 2048 / 1024, car.VelocityX );
		Assert.AreEqual( FrictionAt60 * 1024 / 1024, car.VelocityZ );
	}

	/// <summary>A broken ride (wear 2) slows its cars by 1000 where a working one's 990 would.</summary>
	[TestMethod]
	public void ABrokenRidesCarSlowsBy1000()
	{
		var (cars, ride, car) = Lone( ParkBumperCars.CarFlags.None, 2048, 1024 );

		ride.Wear = 2;
		cars.Tick();

		Assert.AreEqual( 1000 * 2048 / 1024, car.VelocityX );
		Assert.AreEqual( 1000 * 1024 / 1024, car.VelocityZ );
	}

	/// <summary>Seating wins over broken: the seven tenths are tested first, the 1000 is the arm after.</summary>
	[TestMethod]
	public void ASeatingCarOnABrokenRideStillTakesSevenTenths()
	{
		var (cars, ride, car) = Lone( ParkBumperCars.CarFlags.Seating, 2048, 0 );

		ride.Wear = 2;
		cars.Tick();

		Assert.AreEqual( 693 * 2048 / 1024, car.VelocityX );
	}

	/// <summary>
	/// Running, the heading eases toward the steering heading by the speed over 1000 (truncated), and the steering heading
	/// is remembered as the one turned toward. At speed 990 and a quarter-turn of 64 off, the turn is 63.
	/// </summary>
	[TestMethod]
	public void ARunningCarTurnsTowardItsSteeringBySpeedOverAThousand()
	{
		var (cars, ride, car) = Lone( ParkBumperCars.CarFlags.None, 1024, 0 );

		ride.State = ParkBumperCars.RideState.Running;
		car.Steering = 0x40;
		cars.Tick();

		Assert.AreEqual( FrictionAt60, car.Speed );
		Assert.AreEqual( 0x40 * FrictionAt60 / 1000, car.Turn );
		Assert.AreEqual( 63, car.Heading );
		Assert.AreEqual( 0x40, car.Turned );
	}

	/// <summary>A steering heading more than half a circle round is turned toward the short way: -64 here, and the heading wraps to 449.</summary>
	[TestMethod]
	public void ARunningCarTurnsTheShortWayRound()
	{
		var (cars, ride, car) = Lone( ParkBumperCars.CarFlags.None, 1024, 0 );

		ride.State = ParkBumperCars.RideState.Running;
		car.Steering = 0x1c0;
		cars.Tick();

		Assert.AreEqual( -64 * FrictionAt60 / 1000, car.Turn, "truncated toward nought: -63" );
		Assert.AreEqual( 0x200 - 63, car.Heading );
	}

	/// <summary>A ride that is not running (loading, or closed) turns no car, whatever the steering.</summary>
	[TestMethod]
	public void ACarOnARideNotRunningDoesNotTurn()
	{
		var (cars, ride, car) = Lone( ParkBumperCars.CarFlags.None, 1024, 0 );

		Assert.AreEqual( ParkBumperCars.RideState.Loading, ride.State );

		car.Steering = 0x40;
		car.Turn = 17;
		cars.Tick();

		Assert.AreEqual( 0, car.Turn );
		Assert.AreEqual( 0, car.Heading );
		Assert.AreEqual( 0, car.Turned );
	}
}
