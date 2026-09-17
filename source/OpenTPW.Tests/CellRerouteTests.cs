using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Going back over a route and trying to search each waypoint away, which is the last thing done to a
/// route before anyone walks it.
/// </summary>
[TestClass]
public class CellRerouteTests
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

	/// <summary>The numbers the original works to.</summary>
	[TestMethod]
	public void TheRoundsAndBudgetAreTheOriginals()
	{
		Assert.AreEqual( 9, CellReroute.Rounds );
		Assert.AreEqual( 60000, CellReroute.Budget );
		Assert.AreEqual( 4, CellReroute.BackUp );
	}

	/// <summary>
	/// Over open ground the straightening at the front does all the work and there is nothing left for a
	/// search to improve, because one waypoint cannot be searched away.
	/// </summary>
	[TestMethod]
	public void OverOpenGroundItCollapsesToWhereItWasGoing()
	{
		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) );

		CellReroute.Run( route, NothingShut );

		CollectionAssert.AreEqual( new[] { (4, 0) }, (ICollection)route.Waypoints );
	}

	/// <summary>
	/// <b>Whatever it does, the route still ends where it ended.</b> A splice only ever replaces a
	/// waypoint and its neighbour with a search that was aiming at that neighbour, so the last waypoint
	/// survives every pass - and so does straightening, which can never jump past the end.
	/// </summary>
	[TestMethod]
	public void ItStillEndsWhereItEnded()
	{
		var wall = Shut(
			(2, -1, StepDirection.East), (2, 0, StepDirection.East), (2, 1, StepDirection.East),
			(3, -1, StepDirection.West), (3, 0, StepDirection.West), (3, 1, StepDirection.West) );

		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (2, 2), (3, 2), (4, 2), (4, 0) );

		CellReroute.Run( route, wall );

		Assert.IsTrue( route.Waypoints.Count > 0, "it should not empty the route" );
		Assert.AreEqual( (4, 0), route.Waypoints[^1] );
	}

	/// <summary>
	/// <b>When nothing can be searched past, the route comes back exactly as it went in.</b> With every
	/// side of every cell shut, straightening can drop nothing - every line fails on its first step - and
	/// every search gives up, so no splice can land and the scan runs to the end having changed nothing.
	///
	/// <para>
	/// This replaced a test that asserted the route left behind is straight. It is not: after a splice
	/// the original straightens from four waypoints back, not from the beginning, so anything before that
	/// point is never looked at again and a further straightening can still find work to do.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WhenNothingCanBeSearchedPastTheRouteIsLeftExactlyAsItWas()
	{
		Func<int, int, StepDirection, bool> everythingShut = ( _, _, _ ) => true;

		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) );

		CellReroute.Run( route, everythingShut );

		CollectionAssert.AreEqual( new[] { (1, 0), (2, 0), (3, 0), (4, 0) },
			(ICollection)route.Waypoints );
	}

	/// <summary>
	/// Running it a second time changes nothing, which is what "a scan that splices nothing is the end of
	/// it" amounts to from outside.
	/// </summary>
	[TestMethod]
	public void RunningItAgainChangesNothing()
	{
		var wall = Shut(
			(2, -1, StepDirection.East), (2, 0, StepDirection.East), (2, 1, StepDirection.East),
			(3, -1, StepDirection.West), (3, 0, StepDirection.West), (3, 1, StepDirection.West) );

		var route = RouteFrom( (0, 0), (1, 0), (2, 0), (2, 2), (3, 2), (4, 2), (4, 0) );

		CellReroute.Run( route, wall );

		var afterOnce = new List<(int X, int Y)>( route.Waypoints );

		CellReroute.Run( route, wall );

		CollectionAssert.AreEqual( afterOnce, (ICollection)route.Waypoints );
	}

	/// <summary>
	/// A route of one waypoint has no neighbour to search between, so nothing is attempted and it is
	/// handed straight back.
	/// </summary>
	[TestMethod]
	public void ASingleWaypointIsLeftAlone()
	{
		var route = RouteFrom( (0, 0), (3, 0) );

		CellReroute.Run( route, NothingShut );

		CollectionAssert.AreEqual( new[] { (3, 0) }, (ICollection)route.Waypoints );
	}

	/// <summary>
	/// <b>The route's own goal is never consulted.</b> Every search it runs is between two waypoints, so
	/// leaving the goal at nothing at all changes none of the answers.
	/// </summary>
	[TestMethod]
	public void TheRoutesOwnGoalIsNeverConsulted()
	{
		var withGoal = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) );
		var without = RouteFrom( (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) );

		withGoal.Goal = (99, 99);

		CellReroute.Run( withGoal, NothingShut );
		CellReroute.Run( without, NothingShut );

		CollectionAssert.AreEqual( (ICollection)without.Waypoints, (ICollection)withGoal.Waypoints );
	}

	/// <summary>
	/// <b>A splice really does land, and this is the test that would notice if it stopped.</b> The wall
	/// makes a straight line from the start useless, so straightening can take nothing out; only a fresh
	/// search between the waypoint's neighbours finds a way round, and what it finds replaces the waypoint
	/// outright. The detour through (2,4) is gone afterwards.
	///
	/// <para>
	/// <b>Why this was added late:</b> the six tests above it all pass against a version that never splices
	/// anything at all, because straightening alone reaches the same answer in every one of them. Checked
	/// rather than assumed.
	/// </para>
	/// <para>
	/// It also drives the guard against splicing in an answer that runs back through the waypoint being cut
	/// out: on the second pass the search returns a route starting (3,2), which is by then the waypoint
	/// under consideration, so the splice is refused and the scan ends. <b>Removing that guard is still not
	/// caught by anything here</b> - also checked - because re-splicing the same answer yields the same
	/// route and costs only rounds.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ASearchThatGetsRoundTheWallReplacesTheWaypoint()
	{
		var wall = Shut(
			(2, -1, StepDirection.East), (2, 0, StepDirection.East), (2, 1, StepDirection.East),
			(3, -1, StepDirection.West), (3, 0, StepDirection.West), (3, 1, StepDirection.West) );

		var route = RouteFrom( (0, 0), (2, 4), (4, 0) );

		CellReroute.Run( route, wall );

		CollectionAssert.AreEqual( new[] { (3, 2), (4, 0) }, (ICollection)route.Waypoints,
			"the detour is replaced by what the search found round the wall" );
	}
}
