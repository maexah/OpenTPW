using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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

	/// <summary>An edge test that shuts every side, so any change of cell is one nobody allowed.</summary>
	private static bool EverythingIsShut( int x, int y, StepDirection direction ) => true;

	/// <summary><paramref name="edge"/>, noting every side it is asked about in <paramref name="asked"/>.</summary>
	private static Func<int, int, StepDirection, bool> Recording( List<(int, int, StepDirection)> asked,
		Func<int, int, StepDirection, bool> edge ) => ( x, y, direction ) =>
		{
			asked.Add( (x, y, direction) );

			return edge( x, y, direction );
		};

	/// <summary>The cell a position is in.</summary>
	private static (int X, int Y) Cell( Vector3 at ) => ((int)MathF.Floor( at.X / 10f ), (int)MathF.Floor( at.Y / 10f ));

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
	/// <b>An exact tie is broken toward Y, and X is cut short for the rest of the step</b> (<c>0x0042bff8</c>). From a
	/// cell's centre a step of a cell each way meets both boundaries at the same fraction: Y's side is asked first,
	/// X's step is divided by 1.01, and X ends a hundredth of its step short. The tie is broken before the whole
	/// step is weighed, so a tie inside the cell cuts X short too.
	/// </summary>
	/// <remarks>
	/// Y is asked first without the tie-break too, since X is asked only when its reach is strictly the smaller.
	/// What the rule changes is where X lands: without it X lands on its own boundary, is put back at 49.999 and
	/// ends at 54.999.
	/// </remarks>
	[TestMethod]
	public void AnExactTieIsBrokenTowardY()
	{
		var asked = new List<(int, int, StepDirection)>();

		var to = ParkCamcorderCameraMode.Slide( new Vector3( 45f, 45f, 0f ), 10f, 10f, Recording( asked, NothingIsShut ) );

		CollectionAssert.AreEqual( new[] { (4, 4, StepDirection.South), (4, 5, StepDirection.East) }, asked,
			$"asked {string.Join( ", ", asked )}" );
		Assert.AreEqual( 45f + (10f / 1.01f), to.X, 0.0001f, "X's step was divided by 1.01" );
		Assert.AreEqual( 55f, to.Y, 0.0001f );

		// A tie that meets no boundary is broken all the same, before the whole step is taken.
		var whole = ParkCamcorderCameraMode.Slide( new Vector3( 45f, 45f, 0f ), 1f, 1f, NothingIsShut );

		Assert.AreEqual( 45f + (1f / 1.01f), whole.X, 0.00001f, "X's whole step was divided by 1.01" );
		Assert.AreEqual( 46f, whole.Y, 0.00001f );
	}

	/// <summary>
	/// <b>The axis not asked may not change cell</b> (<c>0x0042c197</c>). With every side shut, a step aimed at a
	/// cell's corner asks one side, is refused there, and carries the other axis the same fraction of its own step -
	/// which rounding takes onto its boundary. It is put back, and the viewer stays in the cell they started in.
	/// Once with X carried and once with Y.
	/// </summary>
	[TestMethod]
	public void TheAxisNotAskedIsPutBackInItsCell()
	{
		// Y is asked and refused; X, carried, rounds onto 350.
		var carriedX = ParkCamcorderCameraMode.Slide( new Vector3( 349.93414306640625f, 799.9341430664062f, 0f ),
			0.7071091532707214f, 0.707104504108429f, EverythingIsShut );

		Assert.AreEqual( (34, 79), Cell( carriedX ), $"X was carried out of its cell to {carriedX.X}" );
		Assert.AreEqual( 349.999f, carriedX.X, 0.0001f );

		// X is asked and refused; Y, carried, rounds onto 140.
		var carriedY = ParkCamcorderCameraMode.Slide( new Vector3( 389.7848815917969f, 139.78488159179688f, 0f ),
			0.23570072650909424f, 0.23570381104946136f, EverythingIsShut );

		Assert.AreEqual( (38, 13), Cell( carriedY ), $"Y was carried out of its cell to {carriedY.Y}" );
		Assert.AreEqual( 139.999f, carriedY.Y, 0.0001f );
	}

	/// <summary>
	/// <b>A step that meets no boundary is still put back if its cell changed</b> (<c>0x0042c460</c>), whether that
	/// side is open or not, and nothing is asked. 229.33333 + 0.6666667 is 230 in float, though the reach to 230 is
	/// not under 1: the viewer is put back at 229.999, still in cell 22, and a later frame asks.
	/// </summary>
	[TestMethod]
	public void AStepThatMeetsNoBoundaryIsPutBackIfItsCellChanged()
	{
		foreach ( var (edge, what) in new (Func<int, int, StepDirection, bool>, string)[]
			{ (NothingIsShut, "nothing"), (EverythingIsShut, "everything") } )
		{
			var asked = new List<(int, int, StepDirection)>();

			var to = ParkCamcorderCameraMode.Slide( new Vector3( 515f, 229.33333f, 0f ), 0f, 0.6666667f,
				Recording( asked, edge ) );

			Assert.AreEqual( 0, asked.Count, $"with {what} shut, asked {string.Join( ", ", asked )}" );
			Assert.AreEqual( 229.999f, to.Y, 0.0001f, $"with {what} shut" );
		}

		// After a refusal: X is parked at 770 going west, and Y, carried and then taken whole, rounds onto 470.
		var afterRefusal = ParkCamcorderCameraMode.Slide( new Vector3( 770.4713745117188f, 469.5285949707031f, 0f ),
			-0.47140663862228394f, 0.4714024066925049f, EverythingIsShut );

		Assert.AreEqual( (77, 46), Cell( afterRefusal ), $"at ({afterRefusal.X}, {afterRefusal.Y})" );
		Assert.AreEqual( 469.999f, afterRefusal.Y, 0.0001f );
	}

	/// <summary>
	/// <b>A refusal going negative parks the viewer on the boundary itself</b>, <c>cell * 10</c>, which is still the
	/// cell being left (<c>0x0042c0b0</c>). Going positive it is <c>cell * 10 + 9.999</c>.
	/// </summary>
	[TestMethod]
	public void ARefusalGoingNegativeParksOnTheBoundary()
	{
		var west = ParkCamcorderCameraMode.Slide( new Vector3( 45f, 45f, 0f ), -6f, 0f,
			( x, y, direction ) => direction == StepDirection.West );

		Assert.AreEqual( 40f, west.X, "parked on the west boundary of cell 4" );

		var north = ParkCamcorderCameraMode.Slide( new Vector3( 45f, 45f, 0f ), 0f, -6f,
			( x, y, direction ) => direction == StepDirection.North );

		Assert.AreEqual( 40f, north.Y, "parked on the north boundary of cell 4" );
	}

	/// <summary>
	/// <b>The reach is the original's, from the fractional part of <c>position * 0.1f</c></b> (<c>0x0042bef5</c>). A
	/// step of exactly 5 from 245 ends on the boundary at 250. 0.1f is a little over a tenth, so the reach comes out
	/// at 0.99999928: the side is asked, and the viewer crosses onto 250 itself in this pass. Measured as
	/// <c>(250 - 245) / 5</c> the reach is 1, and the step would be taken whole and put back at 249.999.
	/// </summary>
	/// <remarks>This is the 53-bit reading. At 24 bits the reach would round to 1 as well.</remarks>
	[TestMethod]
	public void AStepEndingOnABoundaryAsksItsSide()
	{
		var asked = new List<(int, int, StepDirection)>();

		var to = ParkCamcorderCameraMode.Slide( new Vector3( 245f, 45f, 0f ), 5f, 0f, Recording( asked, NothingIsShut ) );

		CollectionAssert.AreEqual( new[] { (24, 4, StepDirection.East) }, asked, $"asked {string.Join( ", ", asked )}" );
		Assert.AreEqual( 250f, to.X );
	}

	/// <summary>
	/// A step that is not a number ends. The x87's compares read a NaN as nought, which ends the original's sweep;
	/// C#'s read it as nothing at all, and the cap on the passes, ours, is what ends it here.
	/// </summary>
	[TestMethod]
	[Timeout( 10000 )]
	public void AStepThatIsNotANumberEnds()
	{
		var to = ParkCamcorderCameraMode.Slide( new Vector3( 45f, 45f, 0f ), float.NaN, float.NaN, NothingIsShut );

		Assert.IsTrue( float.IsNaN( to.X ) || float.IsNaN( to.Y ), $"({to.X}, {to.Y})" );
	}

	/// <summary>
	/// <b>The walks that went into a ride in Lost Kingdom stay out of it.</b> The four the game is confirmed by, each
	/// walked a frame at a time at the ordinary speed: never in a footprint, and ending where the original's sweep
	/// puts them - (52,23) and (58,15) are ride entrances, reached through open sides.
	/// </summary>
	/// <remarks>
	/// Before the sweep put anything back, the first went in at its 11th frame, the second (through the queue's
	/// shut south side) at its 1st and the fourth at its 1st. The third stays out on the reach alone and goes in at
	/// its 2nd frame only without the unasked axis put back; the fourth goes in without the whole step put back.
	/// </remarks>
	[TestMethod]
	public void TheWalksThatWentIntoARideStayOut()
	{
		var world = Jungle();

		OnShow( world, onShow =>
		{
			Level.Current = onShow;

			foreach ( var (x, y, yaw, frames, end) in new[]
			{
				(505f, 225f, 5.497787f, 60, (52, 23)),
				(515f, 229.33333f, 0f, 31, (51, 22)),
				(509.05718994140625f, 229.05718994140625f, 5.497790336608887f, 40, (52, 23)),
				(579.82861328125f, 159.52859497070312f, 5.497786045074463f, 40, (58, 15))
			} )
			{
				ParkCamcorderCameraMode.Stand = new Vector3( x, y, 0f );
				ParkCamcorderCameraMode.Yaw = yaw;

				for ( var frame = 1; frame <= frames; ++frame )
				{
					ParkCamcorderCameraMode.DebugWalk( 1f, 0f, 1 );

					var (cellX, cellY) = Cell( ParkCamcorderCameraMode.Stand );

					Assert.AreNotEqual( CellEdge.Footprint, world.CellAt( cellX, cellY ).Type,
						$"from ({x},{y}) at yaw {yaw}, frame {frame} stood in the footprint cell ({cellX},{cellY})" );
				}

				Assert.AreEqual( end, Cell( ParkCamcorderCameraMode.Stand ), $"from ({x},{y}) at yaw {yaw}" );
			}
		} );
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
