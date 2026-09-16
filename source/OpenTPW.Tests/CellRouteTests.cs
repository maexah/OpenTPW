using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Pulling the slack out of a route, which every stage of the original's pathfinder does before handing
/// one on.
///
/// <para>
/// The corner clause gets the most attention here, because it is the part that had to be read off the
/// disassembly: the offsets it is written with shift with whatever is already pushed, and two reads of the
/// same literal offset name two different variables. Tests that only checked "a shut side rejects the
/// line" would pass against a wrong reading of it, so what each of the two ways round a corner is asked
/// about is pinned one at a time.
/// </para>
/// </summary>
[TestClass]
public class CellRouteTests
{
	/// <summary>An edge test that shuts a named set of sides and nothing else.</summary>
	private static Func<int, int, StepDirection, bool> Shut(
		params (int X, int Y, StepDirection Side)[] sides )
	{
		var shut = new HashSet<(int, int, StepDirection)>();

		foreach ( var side in sides )
			shut.Add( (side.X, side.Y, side.Side) );

		return ( x, y, side ) => shut.Contains( (x, y, side) );
	}

	private static readonly Func<int, int, StepDirection, bool> NothingShut = ( _, _, _ ) => false;

	private static CellRoute RouteFrom( (int X, int Y) start, params (int X, int Y)[] waypoints )
	{
		var route = new CellRoute { Start = start };

		route.Waypoints.AddRange( waypoints );

		return route;
	}

	/// <summary>The ladder the original counts down, which decides how hard it tries before giving ground.</summary>
	[TestMethod]
	public void TheJumpLadderStartsAtFour()
	{
		Assert.AreEqual( 4, CellRoute.MostToJump );
	}

	/// <summary>
	/// <b>The first waypoint is measured from the route's own start</b>, because there is no waypoint
	/// before it. The original writes that as a different offset off the same pointer, which only makes
	/// sense once the record's four parts are known.
	/// </summary>
	[TestMethod]
	public void TheAnchorForTheFirstWaypointIsTheRoutesOwnStart()
	{
		var route = RouteFrom( (9, 9), (1, 1), (2, 2) );

		Assert.AreEqual( (9, 9), route.Before( 0 ), "nothing comes before the first one" );
		Assert.AreEqual( (1, 1), route.Before( 1 ) );
		Assert.AreEqual( (2, 2), route.Before( 2 ) );
	}

	/// <summary>A line over open ground gets where it is going.</summary>
	[TestMethod]
	public void AClearLineReachesStraightAcross()
	{
		Assert.IsTrue( CellRoute.Reaches( 0, 0, 5, 0, NothingShut ) );
		Assert.IsTrue( CellRoute.Reaches( 0, 0, 2, 1, NothingShut ), "and one that has to stagger" );
	}

	/// <summary>A shut side stops it, wherever along the line that side happens to be.</summary>
	[TestMethod]
	public void AShutSideStopsTheLine()
	{
		Assert.IsFalse( CellRoute.Reaches( 0, 0, 5, 0, Shut( (0, 0, StepDirection.East) ) ),
			"shut at the very first step" );

		Assert.IsFalse( CellRoute.Reaches( 0, 0, 5, 0, Shut( (3, 0, StepDirection.East) ) ),
			"and shut four cells along, which only a line that walks the whole way can find" );
	}

	/// <summary>A line to where it already is arrives without asking the map anything at all.</summary>
	[TestMethod]
	public void ALineThatGoesNowhereArrivesWithoutAskingAnything()
	{
		var asked = 0;

		Assert.IsTrue( CellRoute.Reaches( 3, 3, 3, 3, ( _, _, _ ) => { ++asked; return false; } ) );
		Assert.AreEqual( 0, asked, "arrival is tested before a step is taken, not after" );
	}

	/// <summary>
	/// <b>Turning one cell late must not be half open.</b> The line from (0,0) to (2,1) goes east and then
	/// turns south on (1,0); carrying on east to (2,0) and turning south from there is one of the two ways
	/// round that corner, and it is open at its first step - so its second step has to be open as well.
	/// </summary>
	[TestMethod]
	public void TurningLateMustNotBeHalfOpen()
	{
		// The corner itself is clear, and so is every side the line actually crosses.
		Assert.IsTrue( CellRoute.Reaches( 0, 0, 2, 1, NothingShut ) );

		Assert.IsFalse( CellRoute.Reaches( 0, 0, 2, 1, Shut( (2, 0, StepDirection.South) ) ),
			"the far side of the late turn, which the line never crosses" );
	}

	/// <summary>
	/// <b>Turning one cell early must not be half open either</b>, and it is asked about the cell the walk
	/// has just left rather than the one it is standing on - which is the half of the clause that reading
	/// the decompiled form gets wrong.
	/// </summary>
	[TestMethod]
	public void TurningEarlyMustNotBeHalfOpen()
	{
		Assert.IsFalse( CellRoute.Reaches( 0, 0, 2, 1, Shut( (0, 1, StepDirection.East) ) ),
			"the far side of the early turn, taken from the cell before the corner" );
	}

	/// <summary>
	/// <b>A way round that is shut at its first step is held against nothing</b> - and the consequence is
	/// worth stating plainly, because it looks wrong: shutting one more side can make a line legal that was
	/// refused before. The side that refused it is now behind a turn nobody can start.
	/// </summary>
	[TestMethod]
	public void ACornerShutAtItsFirstStepIsHeldAgainstNothing()
	{
		var lateTurnHalfOpen = Shut( (2, 0, StepDirection.South) );

		Assert.IsFalse( CellRoute.Reaches( 0, 0, 2, 1, lateTurnHalfOpen ) );

		// Shut the way INTO that turn as well, and the line is fine again.
		var lateTurnShutOutright = Shut(
			(2, 0, StepDirection.South), (1, 0, StepDirection.East) );

		Assert.IsTrue( CellRoute.Reaches( 0, 0, 2, 1, lateTurnShutOutright ),
			"one more shut side, and the line that was refused is allowed" );
	}

	/// <summary>Over open ground the whole route collapses to where it was going.</summary>
	[TestMethod]
	public void ItDropsEveryWaypointAClearLineReaches()
	{
		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0) );

		Assert.IsTrue( route.Straighten( 0, NothingShut ) );
		CollectionAssert.AreEqual( new[] { (6, 0) }, (ICollection)route.Waypoints );
	}

	/// <summary>
	/// <b>It gives ground one waypoint at a time.</b> With the line four ahead refused and the one three
	/// ahead clear, it takes the three - so the waypoint that survives is the fourth, not the fifth.
	/// </summary>
	[TestMethod]
	public void ItGivesGroundFromFourWaypointsToThree()
	{
		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0) );

		Assert.IsTrue( route.Straighten( 0, Shut( (4, 0, StepDirection.East) ) ) );

		CollectionAssert.AreEqual( new[] { (4, 0), (5, 0) }, (ICollection)route.Waypoints,
			"the jump of three landed on (4,0), and nothing reaches past the shut side" );
	}

	/// <summary>Everything before the anchor is left exactly as it stands.</summary>
	[TestMethod]
	public void ItLeavesEverythingBeforeTheAnchorAlone()
	{
		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0) );

		Assert.IsTrue( route.Straighten( 2, NothingShut ) );

		CollectionAssert.AreEqual( new[] { (1, 0), (2, 0), (6, 0) }, (ICollection)route.Waypoints,
			"the first two are untouched and the rest collapse" );
	}

	/// <summary>A wall keeps the waypoint that rounds it, which open ground would have thrown away.</summary>
	[TestMethod]
	public void AWallKeepsTheWaypointThatRoundsIt()
	{
		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (2, 1), (2, 2) );

		// Without the wall the whole thing collapses to its end.
		var open = RouteFrom( (0, 0), (1, 0), (2, 0), (2, 1), (2, 2) );

		Assert.IsTrue( open.Straighten( 0, NothingShut ) );
		CollectionAssert.AreEqual( new[] { (2, 2) }, (ICollection)open.Waypoints );

		Assert.IsTrue( route.Straighten( 0, Shut( (0, 0, StepDirection.South) ) ) );

		CollectionAssert.AreEqual( new[] { (2, 1), (2, 2) }, (ICollection)route.Waypoints,
			"the corner survives because no line from the start gets past the shut side" );
	}

	/// <summary>A route with nothing to give up says so, and is left alone.</summary>
	[TestMethod]
	public void AnAlreadyStraightRouteChangesNothing()
	{
		var route = RouteFrom( (0, 0), (1, 0) );

		Assert.IsFalse( route.Straighten( 0, NothingShut ) );
		CollectionAssert.AreEqual( new[] { (1, 0) }, (ICollection)route.Waypoints );
	}

	/// <summary>Anchored at or past its own end there is nothing to do, and nothing is touched.</summary>
	[TestMethod]
	public void AnchoredPastItsOwnEndItDoesNothing()
	{
		var route = RouteFrom( (0, 0), (1, 0) );

		Assert.IsFalse( route.Straighten( 5, NothingShut ) );
		CollectionAssert.AreEqual( new[] { (1, 0) }, (ICollection)route.Waypoints );

		Assert.IsFalse( route.Straighten( 1, NothingShut ), "and at exactly its end" );
		CollectionAssert.AreEqual( new[] { (1, 0) }, (ICollection)route.Waypoints );
	}
}
