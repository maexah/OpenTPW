using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// The camcorder's step, swept against the cell grid - <see cref="ParkCamcorderCameraMode.Slide"/>.
///
/// <para>
/// <b>What these can and cannot see.</b> <see cref="ParkCamcorderCameraMode.Slide"/> is pure: it takes a
/// position, a step and an edge test, and nothing in it needs a park, a graphics device or a clock. That
/// is deliberate, so the arithmetic can be pinned here. <b>The wiring cannot be</b> - <c>Walk</c> reads
/// <c>Level.Current</c> and <c>Input</c>, which a test run has neither of - so deleting the call in
/// <c>Walk</c> would leave every one of these green. That division is the same one items 3, 4, 5 and 6 of
/// the cleanup plan each recorded, per <c>docs/VERIFYING.md</c> rule 48, and it is why this change rests
/// on the capture as well as on the suite.
/// </para>
/// </summary>
[TestClass]
public class ParkCamcorderWalkTests
{
	/// <summary>An edge test that shuts nothing, which is the control every refusal is measured against.</summary>
	private static bool NothingIsShut( int x, int y, StepDirection direction ) => false;

	/// <summary>
	/// A step that crosses no boundary is taken whole. If this failed, every other test here would be
	/// measuring the sweep's own arithmetic error rather than a refusal.
	/// </summary>
	[TestMethod]
	public void AStepInsideOneCellIsTakenWhole()
	{
		var from = new Vector3( 45f, 45f, 0f );

		var to = ParkCamcorderCameraMode.Slide( from, 2f, 3f, NothingIsShut );

		Assert.AreEqual( 47f, to.X, 0.0001f );
		Assert.AreEqual( 48f, to.Y, 0.0001f );
	}

	/// <summary>
	/// <b>The two-sided control.</b> The same step, twice, differing only in whether the side is shut: it
	/// must cross when nothing is shut and stop when that one side is. A sweep that simply never moved
	/// would pass the second half and fail the first.
	/// </summary>
	[TestMethod]
	public void TheSameStepCrossesOrStopsAccordingToTheEdgeTest()
	{
		// Standing at x 48, stepping +6 reaches x 54 - over the boundary at 50, into cell 5.
		var from = new Vector3( 48f, 45f, 0f );

		var crossed = ParkCamcorderCameraMode.Slide( from, 6f, 0f, NothingIsShut );

		Assert.AreEqual( 54f, crossed.X, 0.0001f, "nothing shut, so the whole step is taken" );

		var stopped = ParkCamcorderCameraMode.Slide( from, 6f, 0f,
			( x, y, direction ) => x == 4 && direction == StepDirection.East );

		Assert.IsTrue( stopped.X < 50f, $"the east side of cell 4 is shut, so x should stay under 50 - it was {stopped.X}" );
		Assert.AreEqual( 50f - 0.001f, stopped.X, 0.0001f, "and it parks just inside the cell it was refused from" );
	}

	/// <summary>
	/// Refusing one axis leaves the other running - which is what makes a viewer slide along a wall rather
	/// than stick to it, and is the behaviour the original's loop produces by advancing both axes by the
	/// fraction that reached the nearer boundary.
	/// </summary>
	[TestMethod]
	public void ARefusedAxisStopsAndTheOtherCarriesOn()
	{
		var from = new Vector3( 48f, 45f, 0f );

		// Going east and north at once, with only the east side shut.
		var to = ParkCamcorderCameraMode.Slide( from, 6f, 4f,
			( x, y, direction ) => direction == StepDirection.East );

		Assert.AreEqual( 50f - 0.001f, to.X, 0.0001f, "x stopped at the shut side" );
		Assert.AreEqual( 49f, to.Y, 0.0001f, "y took its whole step regardless" );
	}

	/// <summary>
	/// A step the dead band swallows moves nobody. The original zeroes each axis against
	/// <c>_DAT_006fde00</c>/<c>_DAT_006fde04</c> before it sweeps.
	/// </summary>
	[TestMethod]
	public void AStepUnderTheDeadBandIsNotTakenAtAll()
	{
		var from = new Vector3( 45f, 45f, 0f );

		var to = ParkCamcorderCameraMode.Slide( from, 1e-5f, -1e-5f, NothingIsShut );

		Assert.AreEqual( 45f, to.X, 0.0000001f );
		Assert.AreEqual( 45f, to.Y, 0.0000001f );
	}

	/// <summary>
	/// A null edge test takes the step whole, which is what this camera did before the sweep existed. It
	/// is the fallback for a scene with no park, and it is pinned so that "no park" cannot quietly become
	/// "cannot move".
	/// </summary>
	[TestMethod]
	public void WithNoEdgeTestTheStepIsTakenWhole()
	{
		var to = ParkCamcorderCameraMode.Slide( new Vector3( 48f, 48f, 0f ), 25f, 25f, null );

		Assert.AreEqual( 73f, to.X, 0.0001f );
		Assert.AreEqual( 73f, to.Y, 0.0001f );
	}

	/// <summary>
	/// Against the real park: <b>a footprint cell cannot be walked into from outside it</b>, which is the
	/// complaint this item is about - "on the ground, you can walk straight through a ride".
	///
	/// <para>
	/// Every footprint cell of Lost Kingdom that has a non-footprint neighbour is tried, from that
	/// neighbour's own centre, stepping a whole cell toward it. <see cref="CellEdge"/> already refuses
	/// <c>to.Type == Footprint</c> from outside; what is under test is whether the sweep asks it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheViewerCannotWalkIntoARidesFootprint()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var edge = CellEdge.For( world, 2 );

		var tried = 0;
		var control = 0;

		for ( var y = 1; y < 127; ++y )
		{
			for ( var x = 1; x < 127; ++x )
			{
				if ( world.CellAt( x, y ).Type != CellEdge.Footprint )
					continue;

				foreach ( var direction in new[] { StepDirection.North, StepDirection.East,
					StepDirection.South, StepDirection.West } )
				{
					// Step from the NEIGHBOUR back into the footprint, so the move is from outside in.
					var (fromX, fromY) = MapStep.Beyond( x, y, direction );

					if ( world.CellAt( fromX, fromY ).Type == CellEdge.Footprint )
						continue;

					var back = direction switch
					{
						StepDirection.North => StepDirection.South,
						StepDirection.South => StepDirection.North,
						StepDirection.East => StepDirection.West,
						_ => StepDirection.East
					};

					var (dx, dy) = back switch
					{
						StepDirection.North => (0f, -10f),
						StepDirection.South => (0f, 10f),
						StepDirection.East => (10f, 0f),
						_ => (-10f, 0f)
					};

					var from = new Vector3( (fromX * 10f) + 5f, (fromY * 10f) + 5f, 0f );

					var walked = ParkCamcorderCameraMode.Slide( from, dx, dy, edge.Blocked );

					var landedX = (int)MathF.Floor( walked.X / 10f );
					var landedY = (int)MathF.Floor( walked.Y / 10f );

					++tried;

					Assert.AreNotEqual( CellEdge.Footprint, world.CellAt( landedX, landedY ).Type,
						$"walking {back} from ({fromX},{fromY}) landed on the footprint cell ({landedX},{landedY})" );

					// The control: with nothing shut, the very same step DOES land on the footprint. Without
					// this the assertion above would be satisfied by a sweep that never moved anyone.
					var free = ParkCamcorderCameraMode.Slide( from, dx, dy, NothingIsShut );

					if ( world.CellAt( (int)MathF.Floor( free.X / 10f ), (int)MathF.Floor( free.Y / 10f ) ).Type
						== CellEdge.Footprint )
						++control;
				}
			}
		}

		// The anti-vacuity guards: the loop proves nothing if it found no such pair, and the control proves
		// the refusal is the edge test's doing rather than the step being too short to arrive.
		Assert.IsTrue( tried > 20, $"only {tried} ways into a footprint were tried" );
		Assert.AreEqual( tried, control, "every one of those steps reaches the footprint when nothing is shut" );
	}

	/// <summary>
	/// And the other half, so this is not simply "the camcorder can no longer move": the park's own path
	/// network is still walkable along itself at mode 2.
	/// </summary>
	[TestMethod]
	public void TheViewerCanStillWalkAlongThePark()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var edge = CellEdge.For( world, 2 );

		var moved = 0;

		for ( var y = 1; y < 127; ++y )
		{
			for ( var x = 1; x < 127; ++x )
			{
				if ( world.CellAt( x, y ).Type != CellEdge.Path )
					continue;

				var from = new Vector3( (x * 10f) + 5f, (y * 10f) + 5f, 0f );

				// A short step that crosses nothing must always be taken, wherever it is made.
				var to = ParkCamcorderCameraMode.Slide( from, 1f, 0f, edge.Blocked );

				Assert.AreEqual( from.X + 1f, to.X, 0.0001f,
					$"a step inside path cell ({x},{y}) was refused" );

				++moved;
			}
		}

		Assert.AreEqual( 78, moved, "path cells in Lost Kingdom" );
	}
}
