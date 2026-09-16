using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Finding a way from one cell to another, which is the thing everything that walks is waiting on.
///
/// <para>
/// The interesting cases are the ones with an obstacle in them, and those are laid out so that every tick
/// of both traces is worked out in advance rather than asserted on loosely. The wall below is chosen so
/// that <b>both</b> traces rejoin the line on the very same tick, which is the only way to pin which of
/// them the original prefers.
/// </para>
/// </summary>
[TestClass]
public class CellSearchTests
{
	private const int Plenty = 60000;

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

	private static CellRoute RouteFrom( (int X, int Y) start, (int X, int Y) goal )
		=> new() { Start = start, Goal = goal };

	/// <summary>The caps the original compares against, byte for byte.</summary>
	[TestMethod]
	public void TheCapsAreTheOriginalsOwn()
	{
		Assert.AreEqual( 0xfa, CellSearch.Room );
		Assert.AreEqual( 0xfc, CellSearch.SpliceRoom );
	}

	/// <summary>
	/// Over open ground it walks straight at the goal and writes down nothing but the goal - no corner is
	/// ever turned, so there is nothing worth recording on the way.
	/// </summary>
	[TestMethod]
	public void AClearRunWritesDownOnlyTheGoal()
	{
		var route = RouteFrom( (0, 0), (5, 0) );
		var search = new CellSearch( route, Plenty, NothingShut );

		Assert.IsTrue( search.Run( 0, 0 ) );
		CollectionAssert.AreEqual( new[] { (5, 0) }, (ICollection)route.Waypoints );
		Assert.AreEqual( 5, search.Steps, "one turn per cell crossed" );
	}

	/// <summary>
	/// <b>Running out of turns writes nothing at all</b>, which is not how the other failures behave. That
	/// test sits at the top of the walk beside the arrival test and only writes when it has arrived.
	/// </summary>
	[TestMethod]
	public void RunningOutOfTurnsWritesNothingOnTheEnd()
	{
		var route = RouteFrom( (0, 0), (5, 0) );
		var search = new CellSearch( route, 2, NothingShut );

		Assert.IsFalse( search.Run( 0, 0 ) );
		Assert.AreEqual( 0, route.Waypoints.Count, "it got two cells along and wrote none of it down" );
		Assert.AreEqual( 2, search.Steps );
	}

	/// <summary>
	/// <b>A turn that can be started one way round but not finished is written down as a corner.</b> The
	/// same walk over open ground records nothing, so it is the shut side doing it and not the turn.
	/// </summary>
	[TestMethod]
	public void ATurnThatCannotBeRoundedIsWrittenDown()
	{
		// Open: the line staggers from (0,0) to (2,1) and keeps no corners at all.
		var open = RouteFrom( (0, 0), (2, 1) );

		Assert.IsTrue( new CellSearch( open, Plenty, NothingShut ).Run( 0, 0 ) );
		CollectionAssert.AreEqual( new[] { (2, 1) }, (ICollection)open.Waypoints,
			"nothing but the goal" );

		// Shut the far side of the turn and the corner is kept.
		var route = RouteFrom( (0, 0), (2, 1) );
		var search = new CellSearch( route, Plenty, Shut( (2, 1, StepDirection.South) ) );

		Assert.IsTrue( search.Run( 0, 0 ) );

		// (1,1) is found through the LATE branch - the way out of it eastwards is open, and the way on
		// from there southwards is shut. (2,1) is found through the EARLY branch, reached because the way
		// out of (2,1) southwards is shut. Then the arrival test writes the goal on the end, which is why
		// (2,1) appears twice. Pinning the whole route makes this a test of both branches at once.
		CollectionAssert.AreEqual( new[] { (1, 1), (2, 1), (2, 1) }, (ICollection)route.Waypoints,
			"both corners and the goal" );
	}

	/// <summary>
	/// <b>Both traces rejoin the line on the same tick here, and the one turning alongside wins.</b> The
	/// wall runs between x=2 and x=3 for three rows, so the pair set off from (2,0) and meet the line
	/// again at (3,0) after exactly five ticks each - one round the south end, one round the north. The
	/// route that comes back goes south, so the tie went to the trace the original asks about first.
	/// </summary>
	[TestMethod]
	public void WhenBothTracesRejoinAtOnceTheFirstOneWins()
	{
		var wall = Shut(
			(2, -1, StepDirection.East), (2, 0, StepDirection.East), (2, 1, StepDirection.East),
			(3, -1, StepDirection.West), (3, 0, StepDirection.West), (3, 1, StepDirection.West) );

		var route = RouteFrom( (0, 0), (4, 0) );
		var search = new CellSearch( route, Plenty, wall );

		Assert.IsTrue( search.Run( 0, 0 ), "it should get round the wall" );

		Assert.AreEqual( (4, 0), route.Waypoints[^1], "and finish on the goal" );

		Assert.IsTrue( route.Waypoints.Any( w => w.Y > 0 ),
			"it went round the south end, which is the trace that turns alongside" );

		Assert.IsFalse( route.Waypoints.Any( w => w.Y < 0 ),
			"and none of the north detour survived, so the tie did not go to the other one" );
	}

	/// <summary>
	/// Boxed in on all four sides, both traces spin on the spot until they run out of room, and it gives
	/// up - leaving the cell it had reached on the end, which is what running out of turns does not do.
	/// </summary>
	[TestMethod]
	public void BoxedInItGivesUpAndLeavesWhereItGotTo()
	{
		var boxed = Shut(
			(0, 0, StepDirection.North), (0, 0, StepDirection.East),
			(0, 0, StepDirection.South), (0, 0, StepDirection.West) );

		var route = RouteFrom( (0, 0), (5, 0) );
		var search = new CellSearch( route, Plenty, boxed );

		Assert.IsFalse( search.Run( 0, 0 ) );

		CollectionAssert.AreEqual( new[] { (0, 0), (0, 0) }, (ICollection)route.Waypoints,
			"once where the way was shut, once on giving up" );
	}

	/// <summary>The route it is given is the route it fills in, and it is not replaced.</summary>
	[TestMethod]
	public void ItWritesIntoTheRouteItWasGiven()
	{
		var route = RouteFrom( (0, 0), (3, 0) );

		route.Waypoints.Add( (9, 9) );

		Assert.IsTrue( new CellSearch( route, Plenty, NothingShut ).Run( 0, 0 ) );

		CollectionAssert.AreEqual( new[] { (9, 9), (3, 0) }, (ICollection)route.Waypoints,
			"what was already there is left alone and the goal goes on the end" );
	}
}
