using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Whether a person may step from one cell to the next: the geometry of the original's step check, with
/// the question about what is built on a cell's edge answered by the test rather than by the map.
///
/// <para>
/// These need no game files and no device. Everything here is integer geometry over cell numbers.
/// </para>
/// </summary>
[TestClass]
public class MapStepTests
{
	/// <summary>An edge test that closes nothing, for the cases that are about geometry alone.</summary>
	private static readonly Func<int, int, StepDirection, bool> NothingClosed = ( _, _, _ ) => false;

	/// <summary>An edge test that closes a named set of sides, so a test can say exactly what is shut.</summary>
	private static Func<int, int, StepDirection, bool> Closed(
		params (int X, int Y, StepDirection Side)[] sides )
	{
		var shut = new HashSet<(int, int, StepDirection)>( sides.Select( s => (s.X, s.Y, s.Side) ) );

		return ( x, y, side ) => shut.Contains( (x, y, side) );
	}

	/// <summary>
	/// A cell's number is its position counted from one. This is pinned in both directions because the
	/// off-by-one is the whole difficulty: an id read as an index lands on the neighbour every time and
	/// still looks entirely reasonable.
	/// </summary>
	[TestMethod]
	public void ACellNumberIsItsPositionCountedFromOne()
	{
		Assert.AreEqual( 1, MapStep.CellId( 0, 0 ), "the first cell is one, not nought" );
		Assert.AreEqual( 129, MapStep.CellId( 0, 1 ), "the start of the second row" );
		Assert.AreEqual( 16384, MapStep.CellId( 127, 127 ), "the last cell of the map" );

		foreach ( var (x, y) in new[] { (0, 0), (1, 0), (0, 1), (47, 10), (127, 127), (13, 46) } )
		{
			Assert.AreEqual( (x, y), MapStep.CellAt( MapStep.CellId( x, y ) ),
				$"({x}, {y}) should survive being turned into a cell number and back" );
		}
	}

	/// <summary>
	/// Each direction goes where its name says. North is towards row zero, which is the convention the
	/// original's boundary checks give away.
	/// </summary>
	[TestMethod]
	public void EachDirectionGoesWhereItsNameSays()
	{
		Assert.AreEqual( (5, 4), MapStep.Beyond( 5, 5, StepDirection.North ) );
		Assert.AreEqual( (6, 5), MapStep.Beyond( 5, 5, StepDirection.East ) );
		Assert.AreEqual( (5, 6), MapStep.Beyond( 5, 5, StepDirection.South ) );
		Assert.AreEqual( (4, 5), MapStep.Beyond( 5, 5, StepDirection.West ) );

		// The numbers are the original's and are relied on by the "lower first" rule for diagonals.
		Assert.AreEqual( 0, (int)StepDirection.North );
		Assert.AreEqual( 1, (int)StepDirection.East );
		Assert.AreEqual( 2, (int)StepDirection.South );
		Assert.AreEqual( 3, (int)StepDirection.West );
	}

	/// <summary>
	/// The edge of the map is closed on every side that faces off it, whatever is built there.
	/// </summary>
	[TestMethod]
	public void TheEdgeOfTheMapIsClosed()
	{
		Assert.IsTrue( MapStep.LeavesTheMap( 10, 0, StepDirection.North ) );
		Assert.IsTrue( MapStep.LeavesTheMap( 127, 10, StepDirection.East ) );
		Assert.IsTrue( MapStep.LeavesTheMap( 10, 127, StepDirection.South ) );
		Assert.IsTrue( MapStep.LeavesTheMap( 0, 10, StepDirection.West ) );

		// And the other three sides of each of those cells are not.
		Assert.IsFalse( MapStep.LeavesTheMap( 10, 0, StepDirection.South ) );
		Assert.IsFalse( MapStep.LeavesTheMap( 127, 10, StepDirection.West ) );
		Assert.IsFalse( MapStep.LeavesTheMap( 10, 127, StepDirection.North ) );
		Assert.IsFalse( MapStep.LeavesTheMap( 0, 10, StepDirection.East ) );

		// A cell in the middle faces off the map on no side at all.
		foreach ( var side in Enum.GetValues<StepDirection>() )
			Assert.IsFalse( MapStep.LeavesTheMap( 64, 64, side ), $"the middle of the map, {side}" );
	}

	/// <summary>
	/// A step of more than one cell is refused - the check that owns the original's complaint about a peep
	/// walking so fast it tried to cross more than one cell at once.
	/// </summary>
	[TestMethod]
	public void MoreThanOneCellAtATimeIsRefused()
	{
		var here = MapStep.CellId( 40, 40 );

		Assert.IsFalse( MapStep.TooFarInOneStep( here, MapStep.CellId( 41, 41 ) ), "one cell diagonally" );
		Assert.IsFalse( MapStep.TooFarInOneStep( here, MapStep.CellId( 40, 40 ) ), "staying put" );

		Assert.IsTrue( MapStep.TooFarInOneStep( here, MapStep.CellId( 42, 40 ) ), "two cells across" );
		Assert.IsTrue( MapStep.TooFarInOneStep( here, MapStep.CellId( 40, 38 ) ), "two cells up" );
		Assert.IsTrue( MapStep.TooFarInOneStep( here, MapStep.CellId( 41, 42 ) ), "a knight's move" );

		// And through the step itself, which refuses it before asking the map anything.
		var asked = 0;

		Assert.IsFalse( MapStep.CanStep( here, MapStep.CellId( 42, 40 ),
			( _, _, _ ) => { ++asked; return false; } ) );
		Assert.AreEqual( 0, asked, "a step that far should be refused without consulting the map" );
	}

	/// <summary>
	/// A straight step asks about exactly the one side it crosses, and about nothing else.
	/// </summary>
	[TestMethod]
	public void AStraightStepAsksAboutTheSideItCrosses()
	{
		var here = MapStep.CellId( 40, 40 );
		var asked = new List<(int, int, StepDirection)>();

		Assert.IsTrue( MapStep.CanStep( here, MapStep.CellId( 41, 40 ),
			( x, y, side ) => { asked.Add( (x, y, side) ); return false; } ) );

		CollectionAssert.AreEqual( new[] { (40, 40, StepDirection.East) }, asked,
			"stepping east should ask about the east side of the cell being left, once" );

		// And it is refused when that side is shut.
		Assert.IsFalse( MapStep.CanStep( here, MapStep.CellId( 41, 40 ),
			Closed( (40, 40, StepDirection.East) ) ) );

		// A different side being shut does not stop it, or the test above proves nothing.
		Assert.IsTrue( MapStep.CanStep( here, MapStep.CellId( 41, 40 ),
			Closed( (40, 40, StepDirection.West), (40, 40, StepDirection.North) ) ) );
	}

	/// <summary>
	/// Every one of the four straight steps, in both the open and the closed case.
	/// </summary>
	[TestMethod]
	public void AllFourStraightStepsWorkTheSameWay()
	{
		var here = MapStep.CellId( 40, 40 );

		foreach ( var (side, to) in new[]
		{
			(StepDirection.North, MapStep.CellId( 40, 39 )),
			(StepDirection.East, MapStep.CellId( 41, 40 )),
			(StepDirection.South, MapStep.CellId( 40, 41 )),
			(StepDirection.West, MapStep.CellId( 39, 40 ))
		} )
		{
			Assert.IsTrue( MapStep.CanStep( here, to, NothingClosed ), $"{side} with nothing shut" );
			Assert.IsFalse( MapStep.CanStep( here, to, Closed( (40, 40, side) ) ), $"{side} shut" );
		}
	}

	/// <summary>
	/// <b>A diagonal is two steps round a corner, and either way round will do.</b> There is no diagonal
	/// edge on the map, so the person has to go along and then up, or up and then along.
	/// </summary>
	[TestMethod]
	public void ADiagonalMayGoRoundTheCornerEitherWay()
	{
		var here = MapStep.CellId( 40, 40 );
		var northEast = MapStep.CellId( 41, 39 );

		Assert.IsTrue( MapStep.CanStep( here, northEast, NothingClosed ), "with nothing shut" );

		// Shutting the way north forces the other route: east first, then north from the cell east of here.
		Assert.IsTrue( MapStep.CanStep( here, northEast, Closed( (40, 40, StepDirection.North) ) ),
			"north shut should send them round by the east" );

		// Shutting the way east forces the first route.
		Assert.IsTrue( MapStep.CanStep( here, northEast, Closed( (40, 40, StepDirection.East) ) ),
			"east shut should send them round by the north" );

		// Both shut, and there is no way round at all.
		Assert.IsFalse( MapStep.CanStep( here, northEast,
			Closed( (40, 40, StepDirection.North), (40, 40, StepDirection.East) ) ),
			"both sides shut should refuse the step" );

		// The far halves matter too: north is open but the cell north of here is closed to the east, and
		// east is open but the cell east of here is closed to the north. Neither route completes.
		Assert.IsFalse( MapStep.CanStep( here, northEast,
			Closed( (40, 39, StepDirection.East), (41, 40, StepDirection.North) ) ),
			"both corners blocked at their far side should refuse the step" );
	}

	/// <summary>
	/// The order the two sides are tried in is the original's: the <b>lower-numbered</b> direction first.
	///
	/// <para>
	/// Three of the four diagonals try the vertical side first and would be reproduced by a "go up or down
	/// first" rule. The fourth, south-east, tries <b>east</b> first, because east is 1 and south is 2. This
	/// pins all four so that the tempting wrong generalisation fails here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheLowerNumberedSideIsTriedFirst()
	{
		foreach ( var (name, toX, toY, expected) in new[]
		{
			("north east", 41, 39, StepDirection.North),
			("north west", 39, 39, StepDirection.North),
			("south west", 39, 41, StepDirection.South),
			("south east", 41, 41, StepDirection.East)
		} )
		{
			var asked = new List<StepDirection>();

			MapStep.CanStep( MapStep.CellId( 40, 40 ), MapStep.CellId( toX, toY ),
				( _, _, side ) => { asked.Add( side ); return false; } );

			Assert.AreEqual( expected, asked[0], $"{name} should ask about {expected} first" );
		}
	}

	/// <summary>
	/// Standing still is allowed and asks the map nothing, which is the guard that stops a person with
	/// nowhere to go from being declared stuck.
	/// </summary>
	[TestMethod]
	public void StayingWhereYouAreIsAlwaysAllowed()
	{
		var here = MapStep.CellId( 40, 40 );
		var asked = 0;

		Assert.IsTrue( MapStep.CanStep( here, here, ( _, _, _ ) => { ++asked; return true; } ) );
		Assert.AreEqual( 0, asked, "no side is crossed, so none should be asked about" );
	}
}
