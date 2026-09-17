using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Planning a route across the park that actually ships - <see cref="PeepNavigator.NavigateTo"/>, which is
/// the original's <c>FUN_0050f8e0</c> and the join between the pathfinder and a person.
///
/// <para>
/// Everything below runs against Lost Kingdom's own map through <see cref="CellEdge.For"/>, so the geography
/// is the game's rather than a fixture's. <see cref="CellSearchTests"/> and the rest of that family are
/// deliberately synthetic, which is the right shape for pinning the algorithm; what they cannot say is
/// whether it answers sensibly about a real park, and that is what this file is for.
/// </para>
/// <para>
/// <b>The numbers here were measured and not predicted, and where one is small it is said so rather than
/// dressed up.</b> Only three routes in the whole path network have a non-zero tail - see
/// <see cref="TheThreeDistancesSplitTheRouteRatherThanRepeatingIt"/>, which pins that count precisely
/// because an identity satisfied by zeros on both sides would prove nothing.
/// </para>
/// </summary>
[TestClass]
public class PeepRoutePlanningTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>
	/// Through a stream of this test's own bytes rather than the global file system, which belongs to a
	/// running game - the same rule the other park tests follow.
	/// </summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The mode the edge test is asked in. <b>Not a default and not a guess</b>: the original keeps it in a
	/// global that the whole search reads, set from the <c>this + 0xb4</c> field of the object doing the
	/// asking.
	///
	/// <para>
	/// <b>This comment used to end "so no mode can honestly be called the mode", and that has since been
	/// measured.</b> The field is on the navigator - <c>FUN_0050f3b0</c> reads it at <c>0050f501</c> beside
	/// position, velocity and the force limits, and <c>avoid_walls</c> reads it twenty times over. Scanning
	/// every one of the executable's 881,521 instructions for a write to <c>+0xb4</c> finds exactly one that
	/// lands on a navigator: <c>0051009f</c>, in the constructor <c>FUN_0050ffe0</c>, writing zero. So zero
	/// is the mode every person in the game walks and searches in, and
	/// <see cref="ParkPeople.WalkingMode"/> reproduces it rather than defaulting to it. The other hundred
	/// writes to that offset in the image belong to unrelated structures.
	/// </para>
	/// <para>
	/// <see cref="WhichModeIsAskedChangesWhatCanBeReached"/> still earns its place: it measures how much the
	/// argument matters, which is what makes hardcoding it a choice worth defending rather than a detail.
	/// </para>
	/// </summary>
	private const int Ordinary = 0;

	private const int Strict = 2;

	/// <summary>Every cell the shipped park draws as path, which is where guests can actually walk.</summary>
	private static List<(int X, int Y)> PathCells( ParkWorld world )
	{
		var cells = new List<(int X, int Y)>();

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( world.CellAt( x, y ).Type == CellEdge.Path )
					cells.Add( (x, y) );
			}
		}

		return cells;
	}

	/// <summary>
	/// A navigator standing in the middle of a named cell, carrying the constructor's own radius and limits.
	/// Only the position matters to planning; everything else is what the original builds a person with.
	/// </summary>
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

	/// <summary>The walk from where a person stands to the first waypoint, which no stored distance holds.</summary>
	private static int FirstLeg( PeepNavigator nav )
		=> PeepNavigator.Distance( nav.Waypoints[0].X - nav.Position.X, nav.Waypoints[0].Y - nav.Position.Y );

	/// <summary>
	/// Every guest the save names can be given a route to the destination it was already carrying. The
	/// destination is the one part of a route that survives a save, so routing needs no
	/// destination-choosing behaviour - which is just as well, since that is undecoded.
	///
	/// <para>
	/// <b>What this does not show, said plainly.</b> All thirteen come back with exactly one waypoint,
	/// because every guest in the shipped park is queued in the entrance avenue on open approach cells and a
	/// clear run records no corners at all - the search writes down only the goal. So this test proves the
	/// wiring reaches real data and proves nothing whatever about cornering. The route that does exercise
	/// that is in <see cref="ALongRouteIsCarriedFiveAtATimeWithTheRestLeftAsTail"/>, and the count is
	/// asserted here so the limitation is pinned rather than left to be discovered.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestCanBeGivenARouteToWhereTheSaveWasSendingThem()
	{
		var world = World();
		var blocked = CellEdge.For( world, Ordinary ).Blocked;
		var guests = ParkPeople.PeepsIn( world );

		Assert.AreEqual( 13, guests.Count, "guests in Lost Kingdom" );

		foreach ( var guest in guests )
		{
			var nav = guest.Navigator;

			Assert.IsTrue( nav.NavigateTo( nav.Target, blocked, addCurrent: false ),
				$"guest {guest.ThingId} should be able to reach where the save was sending them" );

			Assert.AreEqual( 1, nav.TotalWaypoints,
				$"guest {guest.ThingId} is on a clear run down the approach, so only the goal is written" );

			Assert.AreEqual( nav.Target.Cell, (nav.Waypoints[0].X >> 16, nav.Waypoints[0].Y >> 16),
				$"guest {guest.ThingId} should end on the cell they were sent to" );

			Assert.IsFalse( nav.Finished, $"guest {guest.ThingId} has not walked it yet" );
			Assert.IsFalse( nav.CannotReach, $"guest {guest.ThingId} has not given up" );
		}
	}

	/// <summary>
	/// <b>A route is planned and never resumed, and this is the test that pins the difference.</b> The save
	/// carries the bookkeeping about a route - how many waypoints it had, how far along it the person was -
	/// and not one coordinate of it, because <c>subpath_buffer[]</c> is deliberately unparsed. So a guest
	/// straight out of the file has counts without places.
	///
	/// <para>
	/// <b>Guest 33 is chosen because their saved count cannot carry the assertion.</b> The save says their
	/// route had one waypoint and the planned one also has one, so <see cref="PeepNavigator.TotalWaypoints"/>
	/// reads the same either side and would pass whether or not anything was planned. What changes is
	/// <see cref="PeepNavigator.Waypoints"/>, from empty to filled, and that is what is asserted.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ASavedGuestHasNoWaypointsUntilOneIsPlanned()
	{
		var world = World();
		var guest = ParkPeople.PeepsIn( world ).Single( peep => peep.ThingId == 33 );
		var nav = guest.Navigator;

		Assert.AreEqual( 1, nav.TotalWaypoints, "the save says their route had one waypoint" );
		Assert.AreEqual( 0, nav.Waypoints.Count, "and does not say where it was" );

		Assert.IsTrue( nav.NavigateTo( nav.Target, CellEdge.For( world, Ordinary ).Blocked,
			addCurrent: false ) );

		Assert.AreEqual( 1, nav.Waypoints.Count, "planning is what puts a place there" );
		Assert.AreEqual( 1, nav.TotalWaypoints, "the count reads the same either side, which is the point" );

		// They are already standing in the cell they were sent to, so the route is the degenerate one.
		Assert.AreEqual( (47, 13), nav.Position.Cell );
		Assert.AreEqual( (47, 13), (nav.Waypoints[0].X >> 16, nav.Waypoints[0].Y >> 16) );
		Assert.AreEqual( 39615, nav.TotalDistance, "the walk to the middle of the cell they stand in" );
	}

	/// <summary>
	/// A route longer than the five slots is carried five at a time with the rest left as tail. The longest
	/// route anywhere in the park's path network is this one, and it is the only case in the whole file that
	/// exercises the buffering split with numbers on both sides of it.
	/// </summary>
	[TestMethod]
	public void ALongRouteIsCarriedFiveAtATimeWithTheRestLeftAsTail()
	{
		var world = World();
		var nav = StandingIn( (57, 15) );

		Assert.IsTrue( nav.NavigateTo( Centre( (43, 29) ), CellEdge.For( world, Ordinary ).Blocked,
			addCurrent: false ) );

		Assert.AreEqual( 7, nav.TotalWaypoints, "the whole route" );
		Assert.AreEqual( 5, nav.BufferedWaypoints, "held five at a time" );
		Assert.AreEqual( 5, nav.Waypoints.Count, "and five is what is actually here" );

		CollectionAssert.AreEqual(
			new[] { (56, 15), (56, 21), (48, 21), (47, 28), (44, 28) },
			nav.Waypoints.Select( point => (point.X >> 16, point.Y >> 16) ).ToList(),
			"the cells it carries, in order" );

		Assert.AreEqual( 1769472, nav.TotalDistance, "the whole route" );
		Assert.AreEqual( 1605632, nav.BufferedDistance, "the legs between carried waypoints" );
		Assert.AreEqual( 98304, nav.TailDistance, "and the legs beyond them" );

		CollectionAssert.AreEqual( new[] { 393216, 524288, 491520, 196608 }, nav.LegLengths.ToList(),
			"one leg per gap between carried waypoints, which is one fewer than there are waypoints" );

		// The first leg belongs to the total alone - it is the walk from where the person stands to the
		// first waypoint, and neither of the other two distances includes it.
		Assert.AreEqual( 65536, FirstLeg( nav ), "one cell west, to the middle of (56,15)" );
		Assert.AreEqual( nav.TotalDistance, FirstLeg( nav ) + nav.BufferedDistance + nav.TailDistance );

		Assert.AreEqual( 0, nav.Cursor, "a fresh route starts at its beginning" );
		Assert.IsFalse( nav.Finished );
		Assert.IsFalse( nav.CannotReach );
	}

	/// <summary>
	/// <b>The three distances split a route rather than each measuring it.</b> The total is the walk to the
	/// first waypoint plus every leg after it; the buffered distance is only the legs between carried
	/// waypoints; the tail only the legs beyond them. So the identity is
	/// <c>Total = firstLeg + Buffered + Tail</c>, and it is checked over every pair of path cells in the
	/// park.
	///
	/// <para>
	/// <b>The guard matters more than the identity here.</b> An identity of three numbers holds trivially
	/// when two of them are zero, and most routes in this park are short enough that they are. So the counts
	/// below are pinned exactly: 4,554 routes have a real buffered distance, and <b>only three in the whole
	/// network have a non-zero tail</b>. Three is a small number and it is the true one - nine routes exceed
	/// five waypoints, but six of those end on a repeated waypoint whose leg is zero, so their tail sums to
	/// nothing. Asserting "some route has a tail" would have passed on a far weaker fact.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheThreeDistancesSplitTheRouteRatherThanRepeatingIt()
	{
		var world = World();
		var blocked = CellEdge.For( world, Ordinary ).Blocked;
		var cells = PathCells( world );

		Assert.AreEqual( 78, cells.Count, "path cells in Lost Kingdom" );

		var reached = 0;
		var withTail = 0;
		var withBuffered = 0;
		var overflowed = 0;

		foreach ( var from in cells )
		{
			foreach ( var to in cells )
			{
				if ( from == to )
					continue;

				var nav = StandingIn( from );

				if ( !nav.NavigateTo( Centre( to ), blocked, addCurrent: false ) )
					continue;

				++reached;

				Assert.AreEqual( nav.TotalDistance,
					FirstLeg( nav ) + nav.BufferedDistance + nav.TailDistance,
					$"({from.X},{from.Y}) to ({to.X},{to.Y}) should split rather than double-count" );

				Assert.AreEqual( nav.BufferedWaypoints, nav.Waypoints.Count, "carried what it says it does" );
				Assert.AreEqual( nav.BufferedDistance, nav.LegLengths.Sum(), "the legs should add to it" );

				if ( nav.TailDistance != 0 )
					++withTail;

				if ( nav.BufferedDistance != 0 )
					++withBuffered;

				if ( nav.BufferedWaypoints < nav.TotalWaypoints )
					++overflowed;
			}
		}

		Assert.AreEqual( 5951, reached, "routes found between path cells" );
		Assert.AreEqual( 4554, withBuffered, "routes with a real buffered distance" );
		Assert.AreEqual( 9, overflowed, "routes with more waypoints than the five slots hold" );
		Assert.AreEqual( 3, withTail,
			"routes with a real tail - small, and pinned exactly so the identity above is not met by zeros" );
	}

	/// <summary>
	/// <b>Which mode the edge test is asked in changes what can be reached</b>, which is why no mode is
	/// hardcoded anywhere in the planner. The difference is large and it runs in the direction the edge test
	/// describes: on the strict mode a path-to-open-ground step is allowed outright, so people cut across
	/// grass and need fewer corners, while the unflagged approach cells shut and take some destinations out
	/// of reach altogether.
	/// </summary>
	[TestMethod]
	public void WhichModeIsAskedChangesWhatCanBeReached()
	{
		var world = World();
		var cells = PathCells( world );

		var counted = new Dictionary<int, (int Reached, int Failed, int Overflowed)>();

		foreach ( var mode in new[] { Ordinary, Strict } )
		{
			var blocked = CellEdge.For( world, mode ).Blocked;
			var reached = 0;
			var failed = 0;
			var overflowed = 0;

			foreach ( var from in cells )
			{
				foreach ( var to in cells )
				{
					if ( from == to )
						continue;

					var nav = StandingIn( from );

					if ( !nav.NavigateTo( Centre( to ), blocked, addCurrent: false ) )
					{
						++failed;
						continue;
					}

					++reached;

					if ( nav.BufferedWaypoints < nav.TotalWaypoints )
						++overflowed;
				}
			}

			counted[mode] = (reached, failed, overflowed);
		}

		Assert.AreEqual( (5951, 55, 9), counted[Ordinary], "the ordinary mode" );
		Assert.AreEqual( (5905, 101, 0), counted[Strict], "the strict one" );

		Assert.AreNotEqual( counted[Ordinary], counted[Strict],
			"if these agreed, the mode would be unobservable and taking it as a parameter would be theatre" );
	}

	/// <summary>
	/// Failing to find a way does two things that read oddly together and are both the original's: it marks
	/// the person as having <b>arrived</b> and as <b>unable to reach</b> anywhere, and it throws away
	/// everything about the route while keeping the destination.
	///
	/// <para>
	/// <b>The person is given a good route first, and that is what makes this test mean anything.</b> A
	/// navigator that has never planned is already empty, so asserting emptiness after a failure would pass
	/// against code that did nothing at all. The route primed in below is the longest anything can reach
	/// from this cell - four waypoints and three leg lengths - and the assertions are that all of it is
	/// gone afterwards.
	/// </para>
	/// <para>
	/// The pair below are <b>both path cells</b>, which is worth knowing: this is not a route into a wall
	/// but two parts of the park's own path network that the walk cannot get between on this mode.
	/// </para>
	/// </summary>
	[TestMethod]
	public void FailingToFindAWayThrowsTheOldRouteAwayAndGivesUp()
	{
		var world = World();
		var blocked = CellEdge.For( world, Ordinary ).Blocked;

		Assert.AreEqual( CellEdge.Path, world.CellAt( 47, 22 ).Type, "standing on path" );
		Assert.AreEqual( CellEdge.Path, world.CellAt( 54, 21 ).Type, "and sent to path" );

		var nav = StandingIn( (47, 22) );

		Assert.IsTrue( nav.NavigateTo( Centre( (43, 29) ), blocked, addCurrent: false ),
			"a route it can find, so that there is something to lose" );

		Assert.AreEqual( 4, nav.TotalWaypoints, "the route it is about to lose" );
		Assert.AreEqual( 4, nav.Waypoints.Count, "short enough that all of it is carried" );
		Assert.AreEqual( 3, nav.LegLengths.Count, "with three legs between those waypoints" );
		Assert.AreEqual( 688128, nav.TotalDistance );

		Assert.IsFalse( nav.NavigateTo( Centre( (54, 21) ), blocked, addCurrent: false ),
			"and one it cannot" );

		Assert.AreEqual( 0, nav.Waypoints.Count, "the old waypoints should be gone" );
		Assert.AreEqual( 0, nav.LegLengths.Count );
		Assert.AreEqual( 0, nav.TotalWaypoints );
		Assert.AreEqual( 0, nav.BufferedWaypoints );
		Assert.AreEqual( 0, nav.TotalDistance );
		Assert.AreEqual( 0, nav.BufferedDistance );
		Assert.AreEqual( 0, nav.TailDistance );
		Assert.AreEqual( 0, nav.Cursor );

		Assert.IsTrue( nav.Finished, "the original sets the arrived flag on a failure" );
		Assert.IsTrue( nav.CannotReach, "and the cannot-reach one as well" );

		// The destination is the one thing kept, because the caller still wants to know where it was sent.
		Assert.AreEqual( Centre( (54, 21) ), nav.Target );
	}

	/// <summary>
	/// Putting the cell the person is standing in on the front of the route - the original's second
	/// argument, which <c>follow_path</c> passes as one when the ground has changed underneath somebody and
	/// when they have got stuck, and as zero when they have merely run out of carried waypoints.
	///
	/// <para>
	/// It shifts the whole route up by one rather than replacing anything, so the count grows and the
	/// waypoint that was last carried is pushed out of the buffer rather than lost from the route.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AddingTheCurrentCellPutsItOnTheFrontOfTheRoute()
	{
		var world = World();
		var blocked = CellEdge.For( world, Ordinary ).Blocked;

		var without = StandingIn( (57, 15) );
		var with = StandingIn( (57, 15) );

		Assert.IsTrue( without.NavigateTo( Centre( (43, 29) ), blocked, addCurrent: false ) );
		Assert.IsTrue( with.NavigateTo( Centre( (43, 29) ), blocked, addCurrent: true ) );

		Assert.AreEqual( 7, without.TotalWaypoints );
		Assert.AreEqual( 8, with.TotalWaypoints, "one longer, because the standing cell joins the route" );

		Assert.AreEqual( (57, 15), (with.Waypoints[0].X >> 16, with.Waypoints[0].Y >> 16),
			"and it goes on the front" );

		// Everything that was carried before is still carried, one slot further along - except the last,
		// which the five slots no longer reach.
		CollectionAssert.AreEqual(
			without.Waypoints.Take( PeepNavigator.Slots - 1 ).Select( p => (p.X >> 16, p.Y >> 16) ).ToList(),
			with.Waypoints.Skip( 1 ).Select( p => (p.X >> 16, p.Y >> 16) ).ToList(),
			"the rest should be shifted along rather than rebuilt" );

		Assert.AreEqual( PeepNavigator.Slots, with.Waypoints.Count, "still five at a time" );
	}
}
