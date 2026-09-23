using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The camcorder's step, swept against the cell grid - <see cref="ParkCamcorderCameraMode.Slide"/> - and
/// the walk that sweeps it, <see cref="ParkCamcorderCameraMode.Step"/>.
///
/// <para>
/// <b>What these can and cannot see.</b> <see cref="ParkCamcorderCameraMode.Slide"/> is pure: it takes a
/// position, a step and an edge test, and nothing in it needs a park, a graphics device or a clock, so the
/// arithmetic is pinned against it directly. <see cref="ParkCamcorderCameraMode.Step"/> - the body the keys
/// and the console both run - reads the park on show from <c>Level.Current</c>, which is given a stand-in
/// level holding the real park. What is left unpinned is <c>Walk</c> reading <c>Input</c> and calling
/// <see cref="ParkCamcorderCameraMode.Step"/>, since a test cannot press a key.
/// </para>
/// </summary>
[TestClass]
public class ParkCamcorderWalkTests
{
	[TestCleanup]
	public void ForgetTheWalk() => ParkCamcorderCameraMode.Forget();

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
	/// And a step just over the band is taken whole: the band is the original's 1e-4, not a coarser one that
	/// would swallow a slow walk on a fast frame.
	/// </summary>
	[TestMethod]
	public void AStepJustOverTheDeadBandIsTaken()
	{
		var from = new Vector3( 45f, 45f, 0f );

		var to = ParkCamcorderCameraMode.Slide( from, 2e-4f, -2e-4f, NothingIsShut );

		Assert.AreEqual( 45.0002f, to.X, 0.00001f );
		Assert.AreEqual( 44.9998f, to.Y, 0.00001f );
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

	/// <summary>
	/// <b>The walk stops at a ride, not only the sweep.</b> The same ways into a footprint as
	/// <see cref="TheViewerCannotWalkIntoARidesFootprint"/>, walked: the viewer stands at the neighbour's
	/// centre and takes one <see cref="ParkCamcorderCameraMode.Step"/> of a whole cell into the footprint with
	/// Lost Kingdom on show - once facing it, and once side-on to it, stepping sideways.
	/// </summary>
	/// <remarks>
	/// The control is the same step with no level on show, which <see cref="ParkCamcorderCameraMode.Step"/>
	/// takes whole: it must land on the footprint every time, or the refusal could be the step falling short.
	/// <b>Mutations:</b> <see cref="ParkCamcorderCameraMode.Step"/> taking the step without
	/// <see cref="ParkCamcorderCameraMode.Slide"/>, handing it no edge test, not reading the park on show,
	/// sweeping only the forward part of the step, or turning a sideways step the wrong way, each walk the
	/// viewer into a ride.
	/// </remarks>
	[TestMethod]
	public void TheWalkStopsAtARidesFootprint()
	{
		var world = Jungle();

		var tried = 0;
		var control = 0;

		OnShow( world, onShow =>
		{
			for ( var y = 1; y < 127; ++y )
			{
				for ( var x = 1; x < 127; ++x )
				{
					if ( world.CellAt( x, y ).Type != CellEdge.Footprint )
						continue;

					foreach ( var direction in new[] { StepDirection.North, StepDirection.East,
						StepDirection.South, StepDirection.West } )
					{
						var (fromX, fromY) = MapStep.Beyond( x, y, direction );

						if ( world.CellAt( fromX, fromY ).Type == CellEdge.Footprint )
							continue;

						var from = new Vector3( (fromX * 10f) + 5f, (fromY * 10f) + 5f, 0f );

						// Facing back into the footprint, then turned a quarter so the same step is taken sideways.
						var yaw = Facing( Opposite( direction ) );

						foreach ( var (forward, right, turned) in new[] { (1f, 0f, 0f), (0f, 1f, MathF.PI / 2f) } )
						{
							++tried;

							Level.Current = onShow;

							var (landedX, landedY) = Walked( from, yaw + turned, forward, right );

							Assert.AreNotEqual( CellEdge.Footprint, world.CellAt( landedX, landedY ).Type,
								$"walking {(forward > 0f ? "forward" : "sideways")} from ({fromX},{fromY}) toward ({x},{y}) " +
								$"landed on the footprint cell ({landedX},{landedY})" );

							Level.Current = null!;

							var (freeX, freeY) = Walked( from, yaw + turned, forward, right );

							if ( world.CellAt( freeX, freeY ).Type == CellEdge.Footprint )
								++control;
						}
					}
				}
			}
		} );

		Assert.IsTrue( tried > 40, $"only {tried} ways into a footprint were tried" );
		Assert.AreEqual( tried, control, "every one of those steps reaches the footprint with no park on show" );
	}

	/// <summary>
	/// <b>The walk asks the camcorder's own mode, 2</b> - <c>FUN_004d8750( x, y, direction, 2 )</c> - not the
	/// guests' 0 or anything else. Every side of Lost Kingdom where mode 2 answers differently from mode 0 or
	/// mode 1 is walked across from its cell's centre, one cell, and the walk has to cross exactly where mode 2
	/// says it may.
	/// </summary>
	/// <remarks>
	/// <see cref="TheWalkStopsAtARidesFootprint"/> cannot see the mode: a footprint is shut from outside in every
	/// mode. <b>Mutations:</b> the walk's mode set to 0 or to 1 fails here.
	/// </remarks>
	[TestMethod]
	public void TheWalkAsksTheCamcordersOwnMode()
	{
		var world = Jungle();

		var strict = CellEdge.For( world, 2 ).Blocked;
		var others = new[] { CellEdge.For( world, 0 ).Blocked, CellEdge.For( world, 1 ).Blocked };
		var differing = new int[others.Length];

		OnShow( world, onShow =>
		{
			Level.Current = onShow;

			for ( var y = 1; y < 127; ++y )
			{
				for ( var x = 1; x < 127; ++x )
				{
					foreach ( var direction in new[] { StepDirection.North, StepDirection.East,
						StepDirection.South, StepDirection.West } )
					{
						var shut = strict( x, y, direction );
						var differs = false;

						for ( var i = 0; i < others.Length; ++i )
						{
							if ( others[i]( x, y, direction ) == shut )
								continue;

							++differing[i];
							differs = true;
						}

						if ( !differs )
							continue;

						var beyond = MapStep.Beyond( x, y, direction );
						var from = new Vector3( (x * 10f) + 5f, (y * 10f) + 5f, 0f );

						var landed = Walked( from, Facing( direction ), forward: 1f, right: 0f );

						Assert.AreEqual( shut ? (x, y) : beyond, landed,
							$"walking {direction} from ({x},{y}): mode 2 says that side is {(shut ? "shut" : "open")}" );
					}
				}
			}
		} );

		Assert.IsTrue( differing[0] > 0, "no side of the park where mode 2 and mode 0 disagree, so this saw nothing" );
		Assert.IsTrue( differing[1] > 0, "no side of the park where mode 2 and mode 1 disagree, so this saw nothing" );
	}

	private static ParkWorld Jungle()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The yaw that faces <paramref name="direction"/>. Forward is +Y at yaw 0 and turns toward -X as yaw grows,
	/// and North is -Y - see <see cref="ParkCamcorderCameraMode.Step"/> and <see cref="MapStep.Beyond"/>.
	/// </summary>
	private static float Facing( StepDirection direction ) => direction switch
	{
		StepDirection.South => 0f,
		StepDirection.West => MathF.PI / 2f,
		StepDirection.North => MathF.PI,
		_ => -MathF.PI / 2f
	};

	private static StepDirection Opposite( StepDirection direction ) => direction switch
	{
		StepDirection.North => StepDirection.South,
		StepDirection.South => StepDirection.North,
		StepDirection.East => StepDirection.West,
		_ => StepDirection.East
	};

	/// <summary>One step of a whole cell from <paramref name="from"/> facing <paramref name="yaw"/>; the cell it ends in.</summary>
	private static (int X, int Y) Walked( Vector3 from, float yaw, float forward, float right )
	{
		ParkCamcorderCameraMode.Stand = from;
		ParkCamcorderCameraMode.Yaw = yaw;

		ParkCamcorderCameraMode.Step( forward, right, distance: 10f );

		var stand = ParkCamcorderCameraMode.Stand;

		return ((int)MathF.Floor( stand.X / 10f ), (int)MathF.Floor( stand.Y / 10f ));
	}

	/// <summary>
	/// A level with <paramref name="world"/> on show and nothing else: no constructor runs, since building a
	/// real level loads a scene, and <see cref="Level.ParkState"/> is set through its private setter to the
	/// park's own running state.
	/// </summary>
	/// <remarks>
	/// Building the park's state makes it <see cref="ParkState.Current"/>, so that and <c>Level.Current</c> are
	/// both put back as they were when <paramref name="walk"/> is done.
	/// </remarks>
	private static void OnShow( ParkWorld world, Action<Level> walk )
	{
		var current = typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!;

		var levelBefore = Level.Current;
		var stateBefore = ParkState.Current;

		try
		{
			var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

			typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, new ParkState( world ) );

			walk( level );
		}
		finally
		{
			Level.Current = levelBefore;
			current.SetValue( null, stateBefore );
		}
	}
}
