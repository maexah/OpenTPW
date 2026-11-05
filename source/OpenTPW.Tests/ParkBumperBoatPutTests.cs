using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// Where the Hot Pot's boat is stood for the frame - <c>FUN_00546280</c>, <see cref="ParkBumperBoats"/>'s <c>Put</c> in its parts: the
/// position and heading of the car eased back by the part of a tick not yet come, the four-corner bob, and the yaw the model is
/// turned by. <c>Put</c> itself needs a model, so these read the arithmetic it calls, which is the same numbers.
/// </summary>
[TestClass]
public class ParkBumperBoatPutTests
{
	private static ParkBumperCars.Car Car( int x = 0, int z = 0, int vx = 0, int vz = 0, int heading = 0, int turn = 0,
		int phase = 0, ParkBumperCars.CarFlags flags = default )
		=> new() { X = x, Z = z, VelocityX = vx, VelocityZ = vz, Heading = heading, Turn = turn, Phase = phase, Flags = flags };

	/// <summary>
	/// The position is the car's, less the part of a tick not yet come of its velocity, at 1/3072 of a cell to a unit and the cell's
	/// size on each axis: half a tick back along (768, -1536) from (2, 1) cells is (1.875, 1.25) cells, at a cell of 10 by 20.
	/// </summary>
	/// <remarks><b>Mutations:</b> the ease added where it is taken off; the velocity left out; the two axes' cell sizes swapped.</remarks>
	[TestMethod]
	public void TheBoatStandsWhereTheCarWasHalfATickBack()
	{
		var car = Car( x: 2 * ParkBumperCars.CellUnits, z: ParkBumperCars.CellUnits, vx: 768, vz: -1536 );
		var (x, y, _, ease) = ParkBumperBoats.Eased( car, 0.5f, cellX: 10f, cellY: 20f );

		Assert.AreEqual( -0.5f, ease, 1e-6f, "the ease is the part not yet come, less one" );
		Assert.AreEqual( 18.75f, x, 1e-4f );
		Assert.AreEqual( 25f, y, 1e-4f );
	}

	/// <summary>
	/// The part of a tick is held to 0..1 first: a frame past the tick (1.7) stands the car where it is, one before it (-0.3) a whole
	/// tick back.
	/// </summary>
	/// <remarks><b>Mutation:</b> the clamp missing, which puts the car 0.7 of a tick ahead.</remarks>
	[TestMethod]
	public void ThePartOfATickIsHeldToZeroToOne()
	{
		var car = Car( x: 3072, z: 0, vx: 1536, heading: 0 );

		var past = ParkBumperBoats.Eased( car, 1.7f, 10f, 10f );
		var before = ParkBumperBoats.Eased( car, -0.3f, 10f, 10f );

		Assert.AreEqual( (0f, 10f), (past.Ease, past.X), "a tick past: its own place" );
		Assert.AreEqual( (-1f, 5f), (before.Ease, before.X), "a tick before: 1536 back, half a cell" );
	}

	/// <summary>
	/// The heading is eased back by the turn it is taking, truncated to a whole 512th toward nought (<c>0x00546409</c>..<c>0x00546439</c>):
	/// three quarters of 41 is 30.75 and the boat takes 30, three quarters of -41 is -30.75 and it takes -30, where a floor would take
	/// -31.
	/// </summary>
	/// <remarks><b>Mutations:</b> a floor or a round for the truncation; the ease's sign turned (the heading eased forward); the turn left out.</remarks>
	[TestMethod]
	[DataRow( 41, 270 )]
	[DataRow( -41, 330 )]
	[DataRow( 0, 300 )]
	public void TheHeadingIsEasedBackByTheTurnAndTruncated( int turn, int expected )
	{
		var car = Car( heading: 300, turn: turn );

		Assert.AreEqual( expected, ParkBumperBoats.Eased( car, 0.25f, 10f, 10f ).Heading );
	}

	/// <summary>
	/// The four corners bob on the phase times 6, 4, 3 and 5, each eased from the last tick's toward this one's, and the height takes a
	/// quarter of the four at 0.00125 a unit: the sines at phase 10 are (171, 157), (120, 109), (92, 83) and (147, 134) now and a
	/// multiple earlier, so at an ease of -0.5 the corners are 164, 114.5, 87.5 and 140.5, which sum to 506.5.
	/// </summary>
	/// <remarks><b>Mutations:</b> any one corner's multiple changed; the ease term dropped; the scale or the quarter wrong; the flag not tested.</remarks>
	[TestMethod]
	public void ABobbingCarRidesTheAverageOfItsFourCornersBobs()
	{
		var bobbing = Car( phase: 10, flags: ParkBumperCars.CarFlags.Live | ParkBumperCars.CarFlags.Bobs );
		var still = Car( phase: 10, flags: ParkBumperCars.CarFlags.Live );

		Assert.AreEqual( 506.5f * 0.00125f * 0.25f, ParkBumperBoats.BobOf( bobbing, -0.5f ), 1e-6f );
		Assert.AreEqual( 0.15828125f, ParkBumperBoats.BobOf( bobbing, -0.5f ), 1e-6f, "the number written out" );
		Assert.AreEqual( 0f, ParkBumperBoats.BobOf( still, -0.5f ), "a car that does not bob adds nothing" );
		Assert.AreEqual( 0f, ParkBumperBoats.BobOf( Car( flags: ParkBumperCars.CarFlags.Bobs ), 0f ), "the sine of nought" );
	}

	/// <summary>
	/// The model is turned by <c>(heading - 0x100) &amp; 0x1ff</c> 512ths of a turn: the boat's model faces a quarter turn from
	/// the car's heading, and the mask wraps it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the 0x100 left out; the mask 0xff; the 512 a 256.</remarks>
	[TestMethod]
	public void TheModelIsTurnedByTheHeadingLessAQuarterTurnWrapped()
	{
		Assert.AreEqual( 0f, ParkBumperBoats.Yaw( 0x100 ), 1e-6f );
		Assert.AreEqual( MathF.PI, ParkBumperBoats.Yaw( 0 ), 1e-5f, "a heading of nought is half a turn" );
		Assert.AreEqual( MathF.PI / 2, ParkBumperBoats.Yaw( 0x180 ), 1e-5f );
		Assert.AreEqual( 0f, ParkBumperBoats.Yaw( 0x300 ), 1e-6f, "a whole turn on is the same place" );
		Assert.AreEqual( 255 * MathF.Tau / 512f, ParkBumperBoats.Yaw( -1 ), 1e-5f, "and a negative heading wraps up" );
	}

	/// <summary>
	/// <c>Put</c>'s whole placement for a bobbing car 2 cells over at 768 a tick, half a tick on, heading 0x194 turning 40 (eased back to 0x180), on ground that rises 0.1 a unit
	/// toward x: the boat stands at (18.75, 20), at the height of the ground under its four corners about that eased place (1.875) plus
	/// the bob (0.15828125) - not about the car's own place, which would give 2.0 - and is turned by the yaw of its eased heading (a quarter turn,
	/// 0x80 512ths) about the up axis the other way round.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bob left off the height; the corners taken about the car's own place; the yaw's sign turned; the yaw
	/// taken from the heading less the ease's; the height not handed back as the third of the position.</remarks>
	[TestMethod]
	public void ABoatIsStoodAtTheEasedPlaceOnTheGroundUnderItWithItsBobAndYaw()
	{
		var car = Car( x: 2 * ParkBumperCars.CellUnits, z: ParkBumperCars.CellUnits, vx: 768, heading: 0x194, turn: 40, phase: 10,
			flags: ParkBumperCars.CarFlags.Live | ParkBumperCars.CarFlags.Bobs );

		var (at, turn, height) = ParkBumperBoats.Placement( car, 0.5f, 10f, 20f, radius: 768, under: ( cornerX, _ ) => 0.1f * cornerX );

		Assert.AreEqual( 1.875f + 0.15828125f, height, 1e-3f );
		Assert.AreEqual( (18.75f, 20f, height), (at.X, at.Y, at.Z), "the position carries the same height" );
		Assert.IsTrue( System.Numerics.Quaternion.Dot( System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -MathF.PI / 2 ), turn ) > 0.99999f,
			"a quarter turn about up, the other way" );
	}
}
