using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The arithmetic a person's navigator does around the parts that need a map: measuring, totalling,
/// reporting progress, advancing the waypoint cursor and deciding when someone is stuck.
///
/// <para>
/// These need no game files and no device. Every constant is in the executable rather than the balance
/// file, and every operation is integer, so all of it can be pinned exactly.
/// </para>
/// </summary>
[TestClass]
public class PeepNavigatorTests
{
	/// <summary>
	/// The three legs that actually exist in Lost Kingdom, with the distances the original worked out and
	/// wrote into the save.
	///
	/// <para>
	/// <b>These are real and they are also not enough, which is the point of the test after this one.</b>
	/// Guests 39 and 35 are the only two people in the park on a route with a corner, and all three of
	/// their legs run straight along an axis - so every one of them has <c>min(ax, ay) = 0</c> and none of
	/// them exercises the halving that makes this metric octagonal rather than a plain sum. They confirm
	/// the shape of the calculation and say nothing at all about its most distinctive term.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheLegsInTheShippedParkComeBackAtTheDistancesItSaved()
	{
		// Waypoints as the save holds them: a cell number times 65536, plus half a cell.
		var legs = new (string Who, int FromX, int FromY, int ToX, int ToY, int Distance)[]
		{
			("guest 39", 3112960,  688128, 3112960,  884736, 196608),
			("guest 35", 3047424,  360448, 3112960,  360448,  65536),
			("guest 35", 3112960,  360448, 3112960,  884736, 524288)
		};

		foreach ( var (who, fromX, fromY, toX, toY, distance) in legs )
		{
			Assert.AreEqual( distance, PeepNavigator.Distance( toX - fromX, toY - fromY ),
				$"the leg {who} was saved walking" );
		}
	}

	/// <summary>
	/// The diagonal term, which the shipped park never exercises and which therefore has to come from the
	/// executable rather than from the save.
	///
	/// <para>
	/// The metric is <c>ax + ay - min(ax, ay) / 2</c>, halved with a shift so it truncates towards zero.
	/// The rows below are chosen so that a plain Manhattan sum, a real Euclidean distance and this all
	/// give different answers: for a whole cell diagonally the three are 131072, about 92682, and 98304.
	/// </para>
	/// </summary>
	[TestMethod]
	public void DistanceIsOctagonalRatherThanManhattanOrEuclidean()
	{
		Assert.AreEqual( 98304, PeepNavigator.Distance( 65536, 65536 ), "one cell diagonally" );
		Assert.AreEqual( 65536, PeepNavigator.Distance( 65536, 0 ), "one cell along a row" );
		Assert.AreEqual( 65536, PeepNavigator.Distance( 0, -65536 ), "one cell up a column" );
		Assert.AreEqual( 98304, PeepNavigator.Distance( -65536, -65536 ), "and diagonally backwards" );

		// The halving truncates, so an odd smaller axis loses its remainder rather than rounding.
		Assert.AreEqual( 9, PeepNavigator.Distance( 6, 5 ),
			"6 + 5 - 5/2, with 5/2 truncated to 2 - and Manhattan would say 11, Euclidean about 7.8" );
		Assert.AreEqual( 9, PeepNavigator.Distance( 6, 6 ), "6 + 6 - 3" );

		Assert.AreEqual( 0, PeepNavigator.Distance( 0, 0 ), "no distance at all" );
	}

	/// <summary>
	/// A waypoint names a whole cell and the walker heads for the middle of it - which is what makes every
	/// waypoint in the shipped park end in exactly half a cell.
	/// </summary>
	[TestMethod]
	public void AWaypointIsTheCentreOfItsCell()
	{
		Assert.AreEqual( 32768, PeepNavigator.WaypointCentre( 0 ), "the middle of the first cell" );
		Assert.AreEqual( 3112960, PeepNavigator.WaypointCentre( 47 ), "the cell guest 39 walks to" );
		Assert.AreEqual( 688128, PeepNavigator.WaypointCentre( 10 ), "and the one it walks from" );

		// Every waypoint the shipped park holds satisfies this, which is how the convention was found.
		foreach ( var cell in new[] { 5, 10, 13, 46, 47 } )
		{
			Assert.AreEqual( PeepNavigator.One / 2, PeepNavigator.WaypointCentre( cell ) % PeepNavigator.One,
				$"cell {cell} should be walked to at its centre" );
		}
	}

	/// <summary>
	/// A route longer than the navigator's five slots is held five at a time. Every route in Lost Kingdom
	/// is three or shorter, so the streaming case is pinned here rather than measured.
	/// </summary>
	[TestMethod]
	public void ALongRouteIsHeldFiveWaypointsAtATime()
	{
		Assert.AreEqual( 1, PeepNavigator.BufferedFor( 1 ) );
		Assert.AreEqual( 3, PeepNavigator.BufferedFor( 3 ), "the longest route in the shipped park" );
		Assert.AreEqual( 5, PeepNavigator.BufferedFor( 5 ) );
		Assert.AreEqual( 5, PeepNavigator.BufferedFor( 6 ), "a route past the slots is cut to them" );
		Assert.AreEqual( 5, PeepNavigator.BufferedFor( 200 ) );
	}

	/// <summary>
	/// A route is the walk to its first waypoint plus every leg between waypoints after that.
	/// </summary>
	[TestMethod]
	public void ARouteIsItsFirstLegPlusEveryLegBetweenWaypoints()
	{
		// Guest 35's route, from where the save has them standing.
		var waypoints = new[] { (3047424, 360448), (3112960, 360448), (3112960, 884736) };

		var fromWalker = PeepNavigator.Distance( 3047424 - 3099805, 360448 - 637724 );

		Assert.AreEqual( fromWalker + 65536 + 524288,
			PeepNavigator.RouteDistance( 3099805, 637724, waypoints ),
			"the walk to the first corner, then both legs after it" );

		Assert.AreEqual( 0, PeepNavigator.RouteDistance( 0, 0, [] ), "a route to nowhere has no length" );

		Assert.AreEqual( 65536, PeepNavigator.RouteDistance( 0, 0, [(65536, 0)] ),
			"a route of one waypoint is just the walk to it" );
	}

	/// <summary>
	/// How close counts as having got there: wider at a corner than at the destination.
	///
	/// <para>
	/// <b>The two are the opposite way round from what a reader might expect</b>, and from what this
	/// project's own notes said until they were checked against the code. Rounding a corner is forgiven by
	/// two radii; stopping on the destination is held to 1.6 of one.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ReachingACornerIsForgivenMoreThanReachingTheDestination()
	{
		var radius = ParkWorld.NavigatorState.DefaultRadius;

		Assert.AreEqual( 26214, PeepNavigator.ToleranceFor( radius, lastLeg: false ), "at a corner" );
		Assert.AreEqual( 20971, PeepNavigator.ToleranceFor( radius, lastLeg: true ), "at the destination" );

		Assert.IsTrue( PeepNavigator.ToleranceFor( radius, lastLeg: true )
			< PeepNavigator.ToleranceFor( radius, lastLeg: false ),
			"the destination should be the tighter of the two, not the looser" );
	}

	/// <summary>
	/// Being blocked six times in the last fifteen steps is what makes a person ask for a new route.
	///
	/// <para>
	/// The original never counts to six. It works the blocked steps out as a fixed-point fraction of
	/// fifteen and compares that with <c>0x6665</c>, one short of four tenths - so six steps give 26214 and
	/// trip it while five give 21845 and do not. Six is therefore the real rule, and both sides of it are
	/// pinned here because a threshold tested only from one side would pass at any value below it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void SixBlockedStepsInFifteenIsGivingUp()
	{
		Assert.IsFalse( PeepNavigator.BlockedTooOften( 0b000000000011111 ), "five blocked steps" );
		Assert.IsTrue( PeepNavigator.BlockedTooOften( 0b000000000111111 ), "six blocked steps" );

		Assert.IsFalse( PeepNavigator.BlockedTooOften( 0 ), "a clean run" );
		Assert.IsTrue( PeepNavigator.BlockedTooOften( 0b111111111111111 ), "blocked every step" );

		// Spread out rather than consecutive - it is a count, not a run.
		Assert.IsTrue( PeepNavigator.BlockedTooOften( 0b101010101010101 ), "eight, every other step" );
		Assert.IsFalse( PeepNavigator.BlockedTooOften( 0b001000100010001 ), "four, every fourth step" );

		// Only the low fifteen are looked at, so older steps cannot keep a person stuck for ever.
		Assert.IsFalse( PeepNavigator.BlockedTooOften( unchecked((int)0xffff8000) ),
			"steps older than the window should not count" );
	}

	/// <summary>
	/// The record of recent steps shifts along as steps are taken, so being blocked ages out.
	/// </summary>
	[TestMethod]
	public void BeingBlockedAgesOutOfTheRecord()
	{
		var nav = Navigator();

		for ( var i = 0; i < 6; ++i )
			nav.RecordStep( blocked: true );

		Assert.IsTrue( PeepNavigator.BlockedTooOften( nav.StuckBits ), "six in a row should be enough" );

		for ( var i = 0; i < 10; ++i )
			nav.RecordStep( blocked: false );

		Assert.IsFalse( PeepNavigator.BlockedTooOften( nav.StuckBits ),
			"and ten clear steps should push them out of the window again" );
	}

	private static ParkWorld.NavigatorState Saved( int cursor = 0, int total = 3, int buffered = 3,
		int bufferedDistance = 589824, int totalDistance = 655360, bool finished = false )
		=> new(
			X: 3099805, Y: 637724, VelocityX: 0, VelocityY: 0, TargetX: 3112960, TargetY: 884736,
			Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
			MaxForce: 31457, MaxSpeed: 15728, NavMode: 0, CantReachDest: 0, PathFinished: finished,
			PathCount: cursor, PathTotalCount: total, PathBufferCount: buffered,
			BufferedDistance: bufferedDistance, TailDistance: 0, TotalDistance: totalDistance,
			StuckBits: 0 );

	private static PeepNavigator Navigator( int cursor = 0, int total = 3, int buffered = 3 )
		=> new( Saved( cursor, total, buffered ) );

	/// <summary>
	/// The cursor moves on only once the walker is inside the tolerance, and takes the leg it just walked
	/// off what is left to walk.
	/// </summary>
	[TestMethod]
	public void ReachingACornerMovesToTheNextOne()
	{
		var nav = Navigator();
		var corner = PeepNavigator.ToleranceFor( nav.Radius, lastLeg: false );

		Assert.IsFalse( nav.StepTowards( corner, legDistance: 65536 ), "exactly the tolerance is not inside it" );
		Assert.AreEqual( 0, nav.Cursor, "so the cursor should not have moved" );
		Assert.AreEqual( 589824, nav.BufferedDistance );

		Assert.IsFalse( nav.StepTowards( corner - 1, legDistance: 65536 ), "a step inside it" );
		Assert.AreEqual( 1, nav.Cursor, "which should move the cursor on" );
		Assert.AreEqual( 589824 - 65536, nav.BufferedDistance, "and take that leg off what is left" );
		Assert.IsFalse( nav.Finished, "there is still a leg to go" );
	}

	/// <summary>
	/// Reaching the last waypoint ends the route rather than moving the cursor past the end of it.
	/// </summary>
	[TestMethod]
	public void ReachingTheDestinationEndsTheRoute()
	{
		var nav = Navigator( cursor: 2, total: 3, buffered: 3 );
		var destination = PeepNavigator.ToleranceFor( nav.Radius, lastLeg: true );

		Assert.IsFalse( nav.StepTowards( destination + 1, legDistance: 0 ), "still short of it" );
		Assert.IsFalse( nav.Finished );

		Assert.IsFalse( nav.StepTowards( destination - 1, legDistance: 0 ), "and now inside it" );
		Assert.IsTrue( nav.Finished, "which should finish the route" );
		Assert.AreEqual( 2, nav.Cursor, "without walking the cursor off the end" );

		// A finished route stays finished and stops moving.
		Assert.IsFalse( nav.StepTowards( 0, legDistance: 0 ) );
		Assert.AreEqual( 2, nav.Cursor );
	}

	/// <summary>
	/// Running off the end of the buffer is what asks for the rest of a streamed route. Nothing in the
	/// shipped park does this, because no route there is longer than the five slots.
	/// </summary>
	[TestMethod]
	public void RunningOffTheEndOfTheBufferAsksForMore()
	{
		var nav = Navigator( cursor: 4, total: 9, buffered: 5 );
		var corner = PeepNavigator.ToleranceFor( nav.Radius, lastLeg: false );

		Assert.IsTrue( nav.StepTowards( corner - 1, legDistance: 65536 ),
			"stepping onto the last buffered waypoint of a longer route should ask for a refill" );
		Assert.AreEqual( 5, nav.Cursor );
		Assert.IsFalse( nav.Finished, "a refill is not an arrival" );
	}

	/// <summary>
	/// How far along the route a walker is, as a fraction of it.
	/// </summary>
	[TestMethod]
	public void ProgressIsWhatIsLeftOverWhatThereWas()
	{
		var nav = new PeepNavigator( Saved( bufferedDistance: 0, totalDistance: 655360 ) );

		Assert.AreEqual( PeepNavigator.One, nav.Progress( 0, 0 ), "standing on the destination" );

		// Half the route still to run.
		Assert.AreEqual( PeepNavigator.One / 2, nav.Progress( 327680, 0 ), "halfway" );

		Assert.AreEqual( 0, nav.Progress( 655360, 0 ), "not started" );

		// A route with no length cannot be divided by, so the walker counts as arrived.
		var nowhere = new PeepNavigator( Saved( totalDistance: 0 ) );

		Assert.AreEqual( PeepNavigator.One, nowhere.Progress( 12345, 6789 ),
			"a route of no length should read as arrived rather than divide by nothing" );

		// And one already finished, whatever it is asked.
		var done = new PeepNavigator( Saved( finished: true ) );

		Assert.AreEqual( PeepNavigator.One, done.Progress( 999999, 999999 ) );
	}

	/// <summary>
	/// A navigator starts from the bookkeeping the save gave it - the cursor, the counts, the radius and
	/// the two flags.
	///
	/// <para>
	/// <b>This said "which is what lets a park resume mid-walk", and that was false.</b> A park cannot
	/// resume mid-walk: the waypoints are the route, and the reader deliberately does not parse
	/// <c>subpath_buffer[]</c>, because <c>SetDest</c> fills only <c>path_buffer_count - 1</c> of the
	/// distances and the rest hold the uninitialised fill or a stale value from an earlier route. Every
	/// assertion below is sound and not one of them is a waypoint. A route must be planned afresh, not
	/// continued.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ANavigatorStartsFromTheBookkeepingTheSaveGaveIt()
	{
		var nav = new PeepNavigator( Saved( cursor: 1, total: 3, buffered: 3 ) );

		Assert.AreEqual( 1, nav.Cursor );
		Assert.AreEqual( 3, nav.TotalWaypoints );
		Assert.AreEqual( 3, nav.BufferedWaypoints );
		Assert.AreEqual( ParkWorld.NavigatorState.DefaultRadius, nav.Radius );
		Assert.IsFalse( nav.Finished );
		Assert.IsFalse( nav.CannotReach );

		nav.GiveUp();

		Assert.IsTrue( nav.CannotReach, "giving up should stick" );
	}

	/// <summary>
	/// Where the person is standing, which way they are going, what they were heading for, and the two
	/// limits the steering step clamps to - all of it parsed by the reader and, until now, dropped on the
	/// way into the running copy. A person who cannot say where they are cannot walk.
	///
	/// <para>
	/// <b><see cref="ParkWorld.NavigatorState.Mass"/> and <c>NavMode</c> are deliberately not carried</b>,
	/// so this pins what is taken and by omission what is not. The steering loop divides the summed force
	/// by a literal 1.0 rather than by mass, and what NavMode selects has never been established.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ANavigatorKnowsWhereItIsAndHowFastItMayGo()
	{
		var nav = new PeepNavigator( Saved() );

		Assert.AreEqual( new FixedVector( 3099805, 637724 ), nav.Position, "where they are standing" );
		Assert.AreEqual( FixedVector.Zero, nav.Velocity, "this one is standing still" );
		Assert.AreEqual( new FixedVector( 3112960, 884736 ), nav.Target, "what they were heading for" );
		Assert.AreEqual( 15728, nav.MaxSpeed, "max speed" );
		Assert.AreEqual( 31457, nav.MaxForce, "max force" );

		// The position is in the same fixed point as everything else, so it names a cell.
		Assert.AreEqual( (47, 9), nav.Position.Cell, "the cell they are standing in" );

		// Walking is what changes it, so it has to be settable - and the velocity with it.
		nav.Position += new FixedVector( FixedVector.One, 0 );

		Assert.AreEqual( (48, 9), nav.Position.Cell, "a whole cell east" );
	}
}
