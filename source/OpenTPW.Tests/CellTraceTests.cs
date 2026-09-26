using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Feeling a way round an obstacle with a hand on it, which is what the pathfinder does instead of
/// searching.
///
/// <para>
/// These are written as single ticks wherever they can be. A trace is a loop, and a test that runs it
/// twenty times and checks where it ended would pass for any number of wrong reasons - so what each tick
/// does with what it was told is pinned directly, and only the short traces are run out.
/// </para>
/// </summary>
[TestClass]
public class CellTraceTests
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

	/// <summary>The numbers the original keeps in the field it shares between facing and ending.</summary>
	[TestMethod]
	public void TheSentinelsAreTheOriginalsOwn()
	{
		Assert.AreEqual( 0x32, CellTrace.RejoinedMarker );
		Assert.AreEqual( 0x33, CellTrace.FullMarker );
		Assert.AreEqual( 0xfb, CellTrace.Room );
	}

	/// <summary>Quarter turns go round in fours, both ways, and the reverse is two of them.</summary>
	[TestMethod]
	public void TurningGoesRoundInFours()
	{
		Assert.AreEqual( StepDirection.East, CellTrace.TurnedFrom( StepDirection.North, 1 ) );
		Assert.AreEqual( StepDirection.West, CellTrace.TurnedFrom( StepDirection.North, -1 ) );
		Assert.AreEqual( StepDirection.North, CellTrace.TurnedFrom( StepDirection.West, 1 ) );
		Assert.AreEqual( StepDirection.South, CellTrace.Opposite( StepDirection.North ) );
		Assert.AreEqual( StepDirection.West, CellTrace.Opposite( StepDirection.East ) );
	}

	/// <summary>
	/// Which side of us a point is on, per direction. This is the comparison both of the original's jump
	/// tables are built from - one used as it stands and one used reversed - so it is pinned on its own.
	/// </summary>
	[TestMethod]
	public void AheadMeansFurtherAlongThatDirection()
	{
		Assert.IsTrue( CellTrace.Ahead( StepDirection.North, 10, 10, 10, 4 ), "north is towards row zero" );
		Assert.IsFalse( CellTrace.Ahead( StepDirection.North, 10, 10, 10, 14 ) );

		Assert.IsTrue( CellTrace.Ahead( StepDirection.South, 10, 10, 10, 14 ) );
		Assert.IsTrue( CellTrace.Ahead( StepDirection.East, 10, 10, 14, 10 ) );
		Assert.IsTrue( CellTrace.Ahead( StepDirection.West, 10, 10, 4, 10 ) );

		// Level is not ahead, which is what lets a trace stop when it draws level.
		Assert.IsFalse( CellTrace.Ahead( StepDirection.North, 10, 10, 10, 10 ) );
		Assert.IsFalse( CellTrace.Ahead( StepDirection.East, 10, 10, 10, 10 ) );
	}

	/// <summary>Between takes its two ends in either order, and includes them.</summary>
	[TestMethod]
	public void BetweenTakesItsEndsEitherWayRound()
	{
		Assert.IsTrue( CellTrace.Between( 5, 1, 9 ) );
		Assert.IsTrue( CellTrace.Between( 5, 9, 1 ) );
		Assert.IsTrue( CellTrace.Between( 1, 1, 9 ), "the ends count" );
		Assert.IsTrue( CellTrace.Between( 9, 1, 9 ) );
		Assert.IsFalse( CellTrace.Between( 0, 1, 9 ) );
		Assert.IsFalse( CellTrace.Between( 10, 1, 9 ) );

		// Both ends the same admits only that one value.
		Assert.IsTrue( CellTrace.Between( 4, 4, 4 ) );
		Assert.IsFalse( CellTrace.Between( 5, 4, 4 ) );
	}

	/// <summary>
	/// <b>Shut ahead, and it turns against its own sense without moving</b>, writing down where it stands.
	/// </summary>
	[TestMethod]
	public void ShutAheadItTurnsTheOtherWayAndStaysPut()
	{
		var trace = new CellTrace( 10, 10, StepDirection.North, 1, 10, 40 );

		trace.Step( Shut( (10, 10, StepDirection.North) ) );

		Assert.AreEqual( (10, 10), (trace.X, trace.Y), "it should not have moved" );
		Assert.AreEqual( StepDirection.West, trace.Facing, "turning by minus its sense" );
		CollectionAssert.AreEqual( new[] { (10, 10) }, (System.Collections.ICollection)trace.Been );

		// The other sense turns the other way from the same start.
		var other = new CellTrace( 10, 10, StepDirection.North, -1, 10, 40 );

		other.Step( Shut( (10, 10, StepDirection.North) ) );

		Assert.AreEqual( StepDirection.East, other.Facing );
	}

	/// <summary>
	/// <b>Clear ahead, it moves - and takes the turn towards its hand only if that is open too.</b> A
	/// straight run along a wall records nothing, which is why only corners come back.
	/// </summary>
	[TestMethod]
	public void ClearAheadItMovesAndOnlyTurnsIfItCan()
	{
		// Open all round: it steps, then turns with its sense, and writes itself down.
		var turning = new CellTrace( 10, 10, StepDirection.North, 1, 40, 10 );

		turning.Step( NothingShut );

		Assert.AreEqual( (10, 9), (turning.X, turning.Y), "one step north" );
		Assert.AreEqual( StepDirection.East, turning.Facing, "turning by plus its sense" );
		Assert.AreEqual( 1, turning.Been.Count, "taking the turn is what writes a cell down" );

		// With the turn shut it keeps going and records nothing.
		var running = new CellTrace( 10, 10, StepDirection.North, 1, 40, 10 );

		running.Step( Shut( (10, 9, StepDirection.East) ) );

		Assert.AreEqual( (10, 9), (running.X, running.Y), "it still moves" );
		Assert.AreEqual( StepDirection.North, running.Facing, "but keeps its hand on the wall" );
		Assert.AreEqual( 0, running.Been.Count, "a straight run along a wall writes nothing down" );
	}

	/// <summary>
	/// A trace that has drawn level with where the walk is going, and is back between its start and that
	/// goal, has rejoined - which is the whole signal the pathfinder is waiting for.
	/// </summary>
	[TestMethod]
	public void DrawingLevelWithTheGoalRejoinsTheLine()
	{
		// Setting off east from (10,10) towards (12,10), with only the turn south from (11,10) shut.
		var trace = new CellTrace( 10, 10, StepDirection.East, 1, 12, 10 );

		trace.Step( Shut( (11, 10, StepDirection.South) ) );

		Assert.AreEqual( (11, 10), (trace.X, trace.Y) );
		Assert.AreEqual( TraceState.Rejoined, trace.State,
			"level with the goal on both axes and between start and goal" );
	}

	/// <summary>
	/// <b>Standing on the cell it set off from does not count as having come back to it.</b> Without that
	/// the very first tick of every trace would end it.
	/// </summary>
	[TestMethod]
	public void TheCellItSetOffFromDoesNotCount()
	{
		// Shut ahead, so the trace turns and stays exactly where it started.
		var trace = new CellTrace( 10, 10, StepDirection.North, 1, 10, 10 );

		trace.Step( Shut( (10, 10, StepDirection.North) ) );

		Assert.AreEqual( (10, 10), (trace.X, trace.Y) );
		Assert.AreEqual( TraceState.Running, trace.State, "it has not been anywhere yet" );
	}

	/// <summary>Running out of room ends it, and a finished trace does nothing more.</summary>
	[TestMethod]
	public void RunningOutOfRoomEndsIt()
	{
		// Boxed in facing a wall with the goal far away, so every tick turns on the spot and records.
		var trace = new CellTrace( 10, 10, StepDirection.North, 1, 10, 100 );
		var shut = Shut(
			(10, 10, StepDirection.North), (10, 10, StepDirection.East),
			(10, 10, StepDirection.South), (10, 10, StepDirection.West) );

		for ( var tick = 0; tick < CellTrace.Room + 5; ++tick )
			trace.Step( shut );

		Assert.AreEqual( TraceState.Full, trace.State );
		Assert.AreEqual( CellTrace.Room + 1, trace.Been.Count,
			"it stops one past the room it has, which is where the original's comparison falls" );

		// And a finished trace is asked nothing at all.
		var asked = 0;

		trace.Step( ( _, _, _ ) => { ++asked; return false; } );

		Assert.AreEqual( 0, asked );
		Assert.AreEqual( CellTrace.Room + 1, trace.Been.Count );
	}
}
