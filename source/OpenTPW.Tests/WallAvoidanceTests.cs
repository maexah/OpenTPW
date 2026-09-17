using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Being pushed away from walls, which is the heaviest of the three forces on a walking person.
///
/// <para>
/// The person here is a quarter of a cell across, so the wall it is kept off sits a quarter of a cell
/// inside each shut side and every expected push divides exactly.
/// </para>
/// </summary>
[TestClass]
public class WallAvoidanceTests
{
	private const int Cell = FixedVector.One;
	private const int Radius = Cell / 4;

	private static Func<int, int, StepDirection, bool> Shut(
		params (int X, int Y, StepDirection Side)[] sides )
	{
		var shut = new HashSet<(int, int, StepDirection)>();

		foreach ( var side in sides )
			shut.Add( (side.X, side.Y, side.Side) );

		return ( x, y, side ) => shut.Contains( (x, y, side) );
	}

	private static readonly Func<int, int, StepDirection, bool> NothingShut = ( _, _, _ ) => false;

	/// <summary>A wall pushes back twice as hard as you are into it.</summary>
	[TestMethod]
	public void AWallPushesTwiceAsHardAsYouAreIntoIt()
	{
		Assert.AreEqual( 2, WallAvoidance.PushOut );
	}

	/// <summary>Over open ground nothing pushes at all.</summary>
	[TestMethod]
	public void OverOpenGroundNothingPushes()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( Cell / 2, Cell / 2 );

		Assert.AreEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, new FixedVector( Cell, 0 ), Radius, NothingShut ) );
	}

	/// <summary>
	/// Walking east into a shut east side, already past the line the wall keeps you off: pushed back
	/// west by twice the overlap.
	/// </summary>
	[TestMethod]
	public void PastAShutEastSideItPushesBackWest()
	{
		// x is 3.875 cells; the wall line is 4 cells less a quarter, so 3.75. A quarter cell past it.
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( (Cell * 7) / 8, Cell / 2 );

		var push = WallAvoidance.Steer( at, new FixedVector( Cell, 0 ), Radius,
			Shut( (3, 3, StepDirection.East) ) );

		Assert.AreEqual( new FixedVector( -(Cell / 4), 0 ), push,
			"an eighth of a cell past the line, pushed back twice that" );
	}

	/// <summary>Not yet past the line, the same shut side does nothing.</summary>
	[TestMethod]
	public void ShortOfTheLineAShutSideDoesNothing()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( Cell / 2, Cell / 2 );

		Assert.AreEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, new FixedVector( Cell, 0 ), Radius,
				Shut( (3, 3, StepDirection.East) ) ) );
	}

	/// <summary>Walking west into a shut west side is the mirror of it.</summary>
	[TestMethod]
	public void PastAShutWestSideItPushesBackEast()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( Cell / 8, Cell / 2 );

		var push = WallAvoidance.Steer( at, new FixedVector( -Cell, 0 ), Radius,
			Shut( (3, 3, StepDirection.West) ) );

		Assert.AreEqual( new FixedVector( Cell / 4, 0 ), push );
	}

	/// <summary>And the two up-and-down sides push along the other axis.</summary>
	[TestMethod]
	public void TheUpAndDownSidesPushAlongTheOtherAxis()
	{
		var low = FixedVector.AtCell( 3, 3 ) + new FixedVector( Cell / 2, (Cell * 7) / 8 );

		Assert.AreEqual( new FixedVector( 0, -(Cell / 4) ),
			WallAvoidance.Steer( low, new FixedVector( 0, Cell ), Radius,
				Shut( (3, 3, StepDirection.South) ) ) );

		var high = FixedVector.AtCell( 3, 3 ) + new FixedVector( Cell / 2, Cell / 8 );

		Assert.AreEqual( new FixedVector( 0, Cell / 4 ),
			WallAvoidance.Steer( high, new FixedVector( 0, -Cell ), Radius,
				Shut( (3, 3, StepDirection.North) ) ) );
	}

	/// <summary>
	/// <b>The first push wins and the rest are never asked.</b> Wedged into a corner with both the east
	/// and south sides shut and past both lines, the answer is about the east one alone - because east is
	/// tried first.
	/// </summary>
	[TestMethod]
	public void TheFirstPushWinsAndTheRestAreNeverAsked()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( (Cell * 7) / 8, (Cell * 7) / 8 );

		var push = WallAvoidance.Steer( at, new FixedVector( Cell, Cell ), Radius,
			Shut( (3, 3, StepDirection.East), (3, 3, StepDirection.South) ) );

		Assert.AreEqual( new FixedVector( -(Cell / 4), 0 ), push,
			"east only, with nothing at all along the other axis" );
	}

	/// <summary>
	/// A corner with no shut side on the diagonal cell is not a corner, so nothing is pushed even though
	/// both ways round it are open.
	/// </summary>
	[TestMethod]
	public void ACornerWithNothingShutOnItIsNotACorner()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( (Cell * 7) / 8, (Cell * 7) / 8 );

		Assert.AreEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, new FixedVector( Cell, Cell ), Radius, NothingShut ) );
	}

	/// <summary>
	/// <b>A way round the corner being shut takes the corner out of consideration entirely.</b> The
	/// diagonal cell has a shut side, so there is a corner there - but one of the two ways past it is also
	/// shut, and that is the straight-sided case's business, not this one's.
	/// </summary>
	[TestMethod]
	public void AShutWayRoundTakesTheCornerOutOfConsideration()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( (Cell * 7) / 8, (Cell * 7) / 8 );

		var withTheWayOpen = Shut( (4, 4, StepDirection.North) );
		var withTheWayShut = Shut( (4, 4, StepDirection.North), (3, 4, StepDirection.North) );

		Assert.AreNotEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, new FixedVector( Cell, Cell ), Radius, withTheWayOpen ),
			"a real corner, so something is pushed" );

		Assert.AreEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, new FixedVector( Cell, Cell ), Radius, withTheWayShut ),
			"and with a way round it shut, nothing is" );
	}

	/// <summary>
	/// <b>The same, going the other way, which no other test here covers.</b> Walking up and to the left
	/// puts a different corner in play - the one below and left of the cell - and it is asked about
	/// different cells and different sides entirely. The first ten tests all went down and to the right,
	/// so a corner case reached only by going the other way was untested until this was added.
	/// </summary>
	[TestMethod]
	public void TheCornerBehindYouWorksTheSameWayRound()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( Cell / 8, Cell / 8 );
		var goingUpAndLeft = new FixedVector( -Cell, -Cell );

		// The corner is the cell below and left; it counts as a corner because one of its near sides is shut.
		var withTheWayOpen = Shut( (2, 2, StepDirection.South) );

		Assert.AreNotEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, goingUpAndLeft, Radius, withTheWayOpen ),
			"a real corner up and to the left, so something is pushed" );

		// Shutting one of the two ways past it takes it out of consideration, as in the other direction.
		var withTheWayShut = Shut( (2, 2, StepDirection.South), (2, 3, StepDirection.East) );

		Assert.AreEqual( FixedVector.Zero,
			WallAvoidance.Steer( at, goingUpAndLeft, Radius, withTheWayShut ),
			"and with a way round it shut, nothing is" );
	}

	/// <summary>
	/// Standing still still gets pushed off a wall. With no velocity there is no direction to normalise,
	/// and the original carries on regardless - the signs of a direction of nothing read as east and
	/// south, so those two sides are the ones asked about.
	/// </summary>
	[TestMethod]
	public void StandingStillItIsStillPushedOffAWall()
	{
		var at = FixedVector.AtCell( 3, 3 ) + new FixedVector( (Cell * 7) / 8, Cell / 2 );

		var push = WallAvoidance.Steer( at, FixedVector.Zero, Radius,
			Shut( (3, 3, StepDirection.East) ) );

		Assert.AreEqual( new FixedVector( -(Cell / 4), 0 ), push );
	}
}
