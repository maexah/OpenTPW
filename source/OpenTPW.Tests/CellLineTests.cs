using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// The straight walk the pathfinder makes before it has to think about anything - Bresenham over cells,
/// with the original's own seed and its own degenerate cases.
/// </summary>
[TestClass]
public class CellLineTests
{
	/// <summary>Walks a line the way the original does, returning the cells it passes through.</summary>
	private static List<(int X, int Y)> Walk( int fromX, int fromY, int toX, int toY, int most = 64 )
	{
		var line = new CellLine( fromX, fromY, toX, toY );
		var been = new List<(int X, int Y)>();
		var (x, y) = (fromX, fromY);

		for ( var step = 0; step < most && (x, y) != (toX, toY); ++step )
		{
			var (dx, dy) = CellLine.StepFor( line.Next() );

			x += dx;
			y += dy;
			been.Add( (x, y) );
		}

		return been;
	}

	/// <summary>
	/// The four steps are the executable's own table, which is a second source for the numbering beside
	/// the step check's boundary guards.
	/// </summary>
	[TestMethod]
	public void TheStepsAreTheEnginesOwnTable()
	{
		Assert.AreEqual( (0, -1), CellLine.StepFor( StepDirection.North ) );
		Assert.AreEqual( (1, 0), CellLine.StepFor( StepDirection.East ) );
		Assert.AreEqual( (0, 1), CellLine.StepFor( StepDirection.South ) );
		Assert.AreEqual( (-1, 0), CellLine.StepFor( StepDirection.West ) );

		// And they agree with the step geometry, which worked them out a different way entirely.
		foreach ( var direction in new[]
		{
			StepDirection.North, StepDirection.East, StepDirection.South, StepDirection.West
		} )
		{
			var (dx, dy) = CellLine.StepFor( direction );

			Assert.AreEqual( (40 + dx, 40 + dy), MapStep.Beyond( 40, 40, direction ), $"{direction}" );
		}
	}

	/// <summary>A line that only moves on one axis takes that axis every single time.</summary>
	[TestMethod]
	public void AStraightLineNeverLeavesItsAxis()
	{
		CollectionAssert.AreEqual(
			new[] { (11, 5), (12, 5), (13, 5) }, Walk( 10, 5, 13, 5 ), "three cells east" );

		CollectionAssert.AreEqual(
			new[] { (9, 5), (8, 5) }, Walk( 10, 5, 8, 5 ), "two cells west" );

		CollectionAssert.AreEqual(
			new[] { (10, 4), (10, 3) }, Walk( 10, 5, 10, 3 ), "two cells north" );

		CollectionAssert.AreEqual(
			new[] { (10, 6), (10, 7) }, Walk( 10, 5, 10, 7 ), "two cells south" );
	}

	/// <summary>
	/// A true diagonal alternates, and arrives - which is the check that the shared accumulator is seeded
	/// and moved the way the original does it.
	/// </summary>
	[TestMethod]
	public void ADiagonalArrivesByAlternating()
	{
		var been = Walk( 10, 10, 14, 14 );

		Assert.AreEqual( (14, 14), been[^1], "the walk should arrive" );
		Assert.AreEqual( 8, been.Count, "a four-by-four diagonal is eight single steps, never a diagonal one" );

		// Every step moves exactly one cell on exactly one axis.
		var (x, y) = (10, 10);

		foreach ( var (nx, ny) in been )
		{
			Assert.AreEqual( 1, System.Math.Abs( nx - x ) + System.Math.Abs( ny - y ),
				"each step moves one cell on one axis" );
			(x, y) = (nx, ny);
		}
	}

	/// <summary>
	/// A shallow line takes mostly the long axis, and the short one only where it must.
	/// </summary>
	[TestMethod]
	public void AShallowLineLeansOnTheLongAxis()
	{
		var been = Walk( 0, 0, 8, 2 );

		Assert.AreEqual( (8, 2), been[^1], "the walk should arrive" );
		Assert.AreEqual( 10, been.Count, "eight across and two down" );

		var across = 0;
		var (x, _) = (0, 0);

		foreach ( var (nx, _) in been )
		{
			if ( nx != x )
				++across;

			x = nx;
		}

		Assert.AreEqual( 8, across, "eight of the ten steps go across" );
	}

	/// <summary>
	/// <b>The error starts at half of each distance added together</b>, which is not the usual Bresenham
	/// seed and is what decides which cells a diagonal actually crosses.
	/// </summary>
	[TestMethod]
	public void TheErrorStartsAtHalfOfEachDistanceAdded()
	{
		Assert.AreEqual( 5, new CellLine( 0, 0, 8, 2 ).Error, "eight and two halve to four and one" );
		Assert.AreEqual( 4, new CellLine( 0, 0, 4, 4 ).Error );
		Assert.AreEqual( 1, new CellLine( 0, 0, 3, 0 ).Error, "three halves to one, and nothing down" );
		Assert.AreEqual( 0, new CellLine( 7, 7, 7, 7 ).Error, "a line going nowhere" );
	}

	/// <summary>
	/// The distances and directions a line is made of, including that they never come out negative.
	/// </summary>
	[TestMethod]
	public void ALineKnowsHowFarAndWhichWayItGoes()
	{
		var line = new CellLine( 10, 10, 4, 13 );

		Assert.AreEqual( 6, line.AcrossDistance );
		Assert.AreEqual( 3, line.DownDistance );
		Assert.AreEqual( StepDirection.West, line.Across );
		Assert.AreEqual( StepDirection.South, line.Down );
		Assert.IsTrue( line.AcrossIsLonger );

		var other = new CellLine( 10, 10, 12, 2 );

		Assert.AreEqual( StepDirection.East, other.Across );
		Assert.AreEqual( StepDirection.North, other.Down );
		Assert.IsFalse( other.AcrossIsLonger );
	}

	/// <summary>
	/// <b>The degenerate cases are the original's and they are not symmetric.</b> A line that does not move
	/// across borrows the down direction; a line that does not move down borrows the across one - and that
	/// happens second, so a line going nowhere at all ends with both holding the vertical direction for a
	/// zero drop. That is an artefact of the order the original assigns them in, and it is kept rather than
	/// tidied, because tidying it would change which way a stalled walker tries to go.
	/// </summary>
	[TestMethod]
	public void ALineGoingNowhereFallsOutOfTheOrderTheOriginalAssignsIn()
	{
		var upright = new CellLine( 5, 5, 5, 9 );

		Assert.AreEqual( StepDirection.South, upright.Down );
		Assert.AreEqual( StepDirection.South, upright.Across, "no across, so it borrows the down one" );

		var flat = new CellLine( 5, 5, 9, 5 );

		Assert.AreEqual( StepDirection.East, flat.Across );
		Assert.AreEqual( StepDirection.East, flat.Down, "no down, so it borrows the across one" );

		// Neither: dy is zero, so Down is North, Across becomes North, and then Down borrows it back.
		var nowhere = new CellLine( 5, 5, 5, 5 );

		Assert.AreEqual( StepDirection.North, nowhere.Across );
		Assert.AreEqual( StepDirection.North, nowhere.Down );
	}
}
