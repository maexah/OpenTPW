using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Guests actually walking, across the park that actually ships - <see cref="PeepWalk"/>, which is the
/// original's <c>FUN_004fa2a0</c> and the thing that finally moves anybody.
///
/// <para>
/// <b>Every number below was measured before it was asserted.</b> The pieces underneath - the steering step,
/// the route follower, the wall push and the route planner - each have their own tests against fixtures; what
/// those cannot say is whether the assembly of them walks a person across Lost Kingdom's real geography, and
/// that is what this file is for.
/// </para>
/// </summary>
[TestClass]
public class PeepWalkTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>Through a stream of this test's own bytes, as the other park tests do.</summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>Long enough for any walk in this park - the slowest measured here takes 108.</summary>
	private const int LongEnough = 400;

	private static PeepNavigator StandingIn( (int X, int Y) cell )
		=> new( new ParkWorld.NavigatorState(
			X: PeepNavigator.WaypointCentre( cell.X ), Y: PeepNavigator.WaypointCentre( cell.Y ),
			VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
			Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: 31457, MaxSpeed: 15728, NavMode: 0, CantReachDest: 0, PathFinished: false,
			PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
			BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

	private static FixedVector Centre( (int X, int Y) cell )
		=> new( PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) );

	/// <summary>Walks until the verdict stops being <see cref="WalkVerdict.Walking"/>, or gives up.</summary>
	private static (WalkVerdict Verdict, int Ticks) WalkOut( PeepWalk walk, int limit = LongEnough )
	{
		for ( var tick = 1; tick <= limit; ++tick )
		{
			var verdict = walk.Step();

			if ( verdict != WalkVerdict.Walking )
				return (verdict, tick);
		}

		return (WalkVerdict.Walking, limit);
	}

	/// <summary>
	/// The eleven states that walk, and the eleven that do not.
	///
	/// <para>
	/// <b>This is pinned state by state rather than counted, because a count would have passed on the wrong
	/// set.</b> The list was found by searching every one of <c>FUN_005019f0</c>'s twenty-two handlers for a
	/// path to the walk tick, not by reading the calls that function makes in its own body - four of the
	/// eleven are called there and the other seven from a handler one level down.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheStatesThatWalkAreTheElevenThatCanReachTheWalkTick()
	{
		var walks = new[]
		{
			PeepState.Walking, PeepState.HeadingForGate, PeepState.Entering, PeepState.Wandering,
			PeepState.GoingToMinorDestination, PeepState.GoingToRide, PeepState.SteppingUpQueue,
			PeepState.BeingAdmitted, PeepState.OnRide, PeepState.HeadingForExit, PeepState.WalkingOutside
		};

		var stays = new[]
		{
			PeepState.AtGate, PeepState.WaitingForOpening, PeepState.JudgingTheFee, PeepState.Deciding,
			PeepState.PlayingSpotAnimation, PeepState.InQueue, PeepState.EnteringRide, PeepState.Riding,
			PeepState.Leaving, PeepState.PickingACellOutside, PeepState.AtTheBusStop
		};

		Assert.AreEqual( ParkWorld.GuestState.States, walks.Length + stays.Length,
			"between them the two lists have to account for every state there is" );

		foreach ( var state in walks )
			Assert.IsTrue( Peep.IsAWalkingState( state ), $"{state} reaches the walk tick" );

		foreach ( var state in stays )
			Assert.IsFalse( Peep.IsAWalkingState( state ), $"{state} does not" );
	}

	/// <summary>
	/// <b>Twelve of the thirteen guests in the shipped park are in a state that walks, and this is the test
	/// that makes the list above worth having.</b>
	///
	/// <para>
	/// An earlier reading of the state switch gave four walking states - <c>Walking</c>, <c>Wandering</c>,
	/// <c>SteppingUpQueue</c> and <c>WalkingOutside</c>. Every assertion about the eleven would have looked
	/// fine beside it, because it is a subset. What that reading could not survive is the park's own data:
	/// <b>no guest in Lost Kingdom is in any of those four</b>, so the count here would have been zero and
	/// nobody would have moved. The one guest who does not walk is thing 33, waiting for the park to open.
	/// </para>
	/// </summary>
	[TestMethod]
	public void MostOfTheParkIsInAStateThatWalksAndTheOneWhoIsNotIsWaiting()
	{
		var guests = ParkPeople.PeepsIn( World() );

		Assert.AreEqual( 13, guests.Count, "guests in Lost Kingdom" );

		var walking = guests.Where( guest => Peep.IsAWalkingState( guest.State ) ).ToList();

		Assert.AreEqual( 12, walking.Count, "twelve of the thirteen are going somewhere" );

		var still = guests.Single( guest => !Peep.IsAWalkingState( guest.State ) );

		Assert.AreEqual( 33, still.ThingId );
		Assert.AreEqual( PeepState.WaitingForOpening, still.State );

		// And the twelve are only two states between them, which is why a wrong list is so easy to miss.
		CollectionAssert.AreEquivalent(
			new[] { PeepState.HeadingForGate, PeepState.Entering },
			walking.Select( guest => guest.State ).Distinct().ToList() );
	}

	/// <summary>
	/// Every guest the save names walks to where it was sending them and stops there. None gives up, and
	/// none is still going when the tick budget runs out.
	/// </summary>
	[TestMethod]
	public void EveryGuestWalksToWhereTheSaveWasSendingThemAndStops()
	{
		var world = World();
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		foreach ( var guest in ParkPeople.PeepsIn( world ) )
		{
			var walk = new PeepWalk( guest.Navigator, blocked );

			Assert.IsTrue( walk.PlanRoute(), $"guest {guest.ThingId} should be able to plan a route" );

			var (verdict, ticks) = WalkOut( walk );

			Assert.AreEqual( WalkVerdict.Arrived, verdict, $"guest {guest.ThingId} should arrive" );
			Assert.IsTrue( ticks < LongEnough, $"guest {guest.ThingId} arrived at tick {ticks}" );
			Assert.IsTrue( guest.Navigator.Finished, $"guest {guest.ThingId} is done walking" );
			Assert.IsFalse( guest.Navigator.CannotReach, $"guest {guest.ThingId} never gave up" );
		}
	}

	/// <summary>
	/// One guest's walk in full, so the numbers are pinned somewhere rather than only being bounded.
	/// </summary>
	[TestMethod]
	public void ThingFortyTwoWalksFourCellsAndTakesThirtyTwoTicksOverIt()
	{
		var world = World();
		var guest = ParkPeople.PeepsIn( world ).Single( peep => peep.ThingId == 42 );
		var nav = guest.Navigator;

		Assert.AreEqual( (47, 9), nav.Position.Cell, "where the save has them standing" );
		Assert.AreEqual( (48, 13), nav.Target.Cell, "and where it was sending them" );

		var started = nav.Position;
		var walk = new PeepWalk( nav, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.IsTrue( walk.PlanRoute() );

		Assert.AreEqual( 1, nav.TotalWaypoints, "a clear run down the approach records only the goal" );
		Assert.AreEqual( 257364, nav.TotalDistance );

		var (verdict, ticks) = WalkOut( walk );

		Assert.AreEqual( WalkVerdict.Arrived, verdict );
		Assert.AreEqual( 32, ticks, "how long the walk takes, at 31ms a tick" );
		Assert.AreEqual( (48, 13), nav.Position.Cell, "they end on the cell they were sent to" );

		Assert.AreEqual( 266323,
			PeepNavigator.Distance( nav.Position.X - started.X, nav.Position.Y - started.Y ),
			"and this is how far they actually travelled, which is not the route's length" );
	}

	/// <summary>
	/// <b>Arriving is measured to the destination point, not to the cell it sits in</b>, so a guest can stop
	/// one cell short and still be there. Thing 39 is sent to a point in <c>(47,13)</c> and settles in
	/// <c>(47,12)</c>, because the point lies within one and three fifths of their radius of where they stop.
	///
	/// <para>
	/// Written down because it looks like an off-by-one and is not: <c>PeepJourney.EndOfTheRoute</c> compares
	/// against <see cref="PeepNavigator.Target"/> with the eight-sided distance, and the original does the
	/// same at <c>0050ef11</c>. Tidying this into "ends on the target cell" would be a real regression.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ArrivingIsMeasuredToThePointAndNotToItsCell()
	{
		var world = World();
		var guest = ParkPeople.PeepsIn( world ).Single( peep => peep.ThingId == 39 );
		var nav = guest.Navigator;

		Assert.AreEqual( (47, 13), nav.Target.Cell );

		var walk = new PeepWalk( nav, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.IsTrue( walk.PlanRoute() );

		var (verdict, ticks) = WalkOut( walk );

		Assert.AreEqual( WalkVerdict.Arrived, verdict );
		Assert.AreEqual( 14, ticks );
		Assert.AreEqual( (47, 12), nav.Position.Cell, "one cell short of the target's cell, and arrived" );

		Assert.IsTrue( (nav.Target - nav.Position).OctagonalLength
			< (int)(((long)nav.Radius * PeepJourney.ArriveWithin) >> 16),
			"which is to say they are inside the arrival tolerance of the point itself" );
	}

	/// <summary>
	/// The longest route in the park's path network, walked end to end - <b>the only case anywhere in Lost
	/// Kingdom that runs out of carried waypoints and has to refill</b>, which is the newest and least
	/// exercised path through the walk.
	/// </summary>
	[TestMethod]
	public void TheLongestRouteIsWalkedRightThroughItsRefill()
	{
		var world = World();
		var nav = StandingIn( (57, 15) );

		nav.Target = Centre( (43, 29) );

		var walk = new PeepWalk( nav, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.IsTrue( walk.PlanRoute() );

		Assert.AreEqual( 7, nav.TotalWaypoints, "more waypoints than there are slots to carry them" );
		Assert.AreEqual( 5, nav.BufferedWaypoints, "so the rest has to stream in" );

		var cells = new List<(int X, int Y)> { nav.Position.Cell };
		var verdict = WalkVerdict.Walking;
		var ticks = 0;

		for ( var tick = 1; tick <= LongEnough; ++tick )
		{
			verdict = walk.Step();

			if ( nav.Position.Cell != cells[^1] )
				cells.Add( nav.Position.Cell );

			if ( verdict != WalkVerdict.Walking ) { ticks = tick; break; }
		}

		Assert.AreEqual( WalkVerdict.Arrived, verdict );
		Assert.AreEqual( 108, ticks );
		Assert.AreEqual( 27, cells.Count, "cells entered on the way" );
		Assert.AreEqual( (57, 15), cells[0] );
		Assert.AreEqual( (43, 29), cells[^1] );

		Assert.IsTrue( nav.Finished );
		Assert.IsFalse( nav.CannotReach );
	}

	/// <summary>
	/// <b>Progress is measured to the waypoint being walked to, from where the step landed.</b> Two separate
	/// claims, and this test exists because a control proved that neither was pinned by anything.
	///
	/// <para>
	/// <c>FUN_0050fd40</c> loads <c>ECX</c> with <c>[ESI + cursor*8 + 0x64]</c> - the current waypoint, not
	/// the destination - and reads the position from <c>[ESI + 8]</c>, which <c>FUN_0050f3b0</c> has already
	/// written the step into. Rewriting either of those was measured to break <b>no test at all</b>, which
	/// is why this one is written against the value the steering step actually consumed rather than against
	/// a re-implementation of it.
	/// </para>
	/// <para>
	/// <b>The guard is the first assertion and it is the point.</b> On the last leg, and anywhere the cursor
	/// happens to sit on the goal, measuring to the waypoint and to the destination give the same answer -
	/// so an assertion made there would hold whichever the code used and would prove nothing. The walk is
	/// stepped well into a seven-waypoint route first, and the two numbers are asserted to differ before
	/// either is used.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ProgressIsMeasuredToTheWaypointAndFromWhereTheStepLanded()
	{
		var world = World();
		var nav = StandingIn( (57, 15) );

		nav.Target = Centre( (43, 29) );

		var walk = new PeepWalk( nav, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.IsTrue( walk.PlanRoute() );

		// Far enough in that the cursor has left the first waypoint behind, so that "the waypoint" and
		// "the destination" are two plainly different places.
		for ( var tick = 0; tick < 40; ++tick )
			walk.Step();

		Assert.IsTrue( nav.Cursor > 0, "the walk has passed at least one waypoint by now" );
		Assert.IsFalse( nav.Finished, "and is not done - once finished, progress is One either way" );

		var waypoint = nav.Waypoints[nav.Cursor];
		var toWaypoint = nav.Progress( waypoint.X - nav.Position.X, waypoint.Y - nav.Position.Y );
		var toTarget = nav.Progress( nav.Target.X - nav.Position.X, nav.Target.Y - nav.Position.Y );

		Assert.AreNotEqual( toWaypoint, toTarget,
			"the guard: were these equal, the assertion below would hold whichever one the code used" );

		Assert.AreEqual( toWaypoint, walk.Steering.LastProgress,
			"the steering step is handed the distance to the waypoint, measured after the step" );
	}

	/// <summary>
	/// Walking turns a guest the way they are actually going.
	///
	/// <para>
	/// <b>The bound is derived from the cardinals rather than from the function under test.</b> Asserting
	/// that the heading equals <c>PeepHeading.Of(...)</c> would only check that function against itself.
	/// Thing 42 walks from <c>(47,9)</c> to <c>(48,13)</c> - both components positive, with far more y than
	/// x - and by the decoded branch structure a step like that lands strictly between the heading for
	/// straight-along-y (<c>0x400</c>) and the one for straight-along-x (<c>0x600</c>), in the half nearer
	/// y. Both ends are asserted, so a mirrored or rotated table fails here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WalkingTurnsAGuestTheWayTheyAreActuallyGoing()
	{
		var world = World();
		var guest = ParkPeople.PeepsIn( world ).Single( peep => peep.ThingId == 42 );
		var walk = new PeepWalk( guest.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.IsTrue( walk.PlanRoute() );

		var before = walk.Position;

		Assert.AreEqual( WalkVerdict.Walking, walk.Step() );

		var moved = walk.Position - before;

		Assert.IsTrue( moved.X > 0 && moved.Y > 0, "they step towards higher x and higher y" );
		Assert.IsTrue( moved.Y > moved.X, "and much further in y, which is what puts the answer below 0x500" );

		Assert.IsTrue( walk.Heading > 0x400 && walk.Heading < 0x500,
			$"a step mostly along y and a little along x, but the heading came back {walk.Heading:x}" );

		Assert.AreNotEqual( 0x400, walk.Heading,
			"and not exactly along y - the guard that the x part of the step is doing something" );
	}

	/// <summary>
	/// <b>Arriving does not turn them, and neither does giving up.</b> <c>FUN_004fa2a0</c> returns at both
	/// of those before it works out any heading, so a person who has finished keeps the way they were last
	/// facing rather than being spun by the last twitch of a walk that is over. The position is written
	/// every tick; the heading is not.
	/// </summary>
	[TestMethod]
	public void ArrivingDoesNotTurnThem()
	{
		var world = World();
		var guest = ParkPeople.PeepsIn( world ).Single( peep => peep.ThingId == 42 );
		var walk = new PeepWalk( guest.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		Assert.IsTrue( walk.PlanRoute() );

		var facing = walk.Heading;

		for ( var tick = 1; tick <= LongEnough; ++tick )
		{
			if ( walk.Step() != WalkVerdict.Walking )
				break;

			facing = walk.Heading;
		}

		Assert.IsTrue( guest.Navigator.Finished, "they got there" );
		Assert.AreNotEqual( 0, facing, "and turned at some point on the way, or this proves nothing" );

		// Every further tick is an arrival, and none of them may move the heading.
		for ( var tick = 0; tick < 5; ++tick )
		{
			Assert.AreEqual( WalkVerdict.Arrived, walk.Step() );
			Assert.AreEqual( facing, walk.Heading, $"still facing where they were, {tick + 1} ticks on" );
		}
	}

	/// <summary>
	/// <b>A guest is already moving when the park loads.</b> The save records velocity, not just position, so
	/// the first tick of a walk continues a step rather than starting one from rest - which is worth pinning
	/// because a reader would reasonably assume otherwise, and a test written on that assumption would pass
	/// against code that quietly zeroed it.
	/// </summary>
	[TestMethod]
	public void AGuestIsAlreadyMovingBeforeTheFirstTick()
	{
		var guests = ParkPeople.PeepsIn( World() );

		Assert.IsTrue( guests.All( guest => guest.Navigator.Velocity != FixedVector.Zero ),
			"every one of the thirteen is in motion in the file" );

		var walker = guests.Single( guest => guest.ThingId == 42 );

		Assert.AreEqual( new FixedVector( 1048, 7796 ), walker.Navigator.Velocity,
			"and this is the speed the file gives thing 42" );
	}
}
