namespace OpenTPW;

/// <summary>
/// How far along a route a person has got, and the arithmetic that moves them along it.
///
/// <para>
/// <see cref="ParkWorld.NavigatorState"/> is what the save reader produces and describes a file. This is
/// the running copy, seeded from it once when the park opens - the same arrangement <see cref="Peep"/>
/// has with <see cref="ParkWorld.GuestState"/>.
/// </para>
/// <para>
/// <b>A navigator seeded from a save has no waypoints, and one that has planned a route has them all.</b>
/// The distinction is not a detail and the two must not be confused. <c>subpath_buffer[]</c> is
/// deliberately not parsed by the reader: <c>SetDest</c> writes only <c>path_buffer_count - 1</c> of the
/// distances, so the remaining slots hold the uninitialised fill or a stale value from an earlier route,
/// and reading them would produce entirely plausible wrong answers. So a person restored from a file
/// knows how far along they were and <i>not</i> where they were going - <see cref="Cursor"/>,
/// <see cref="TotalWaypoints"/> and <see cref="BufferedWaypoints"/> are then bookkeeping about a route
/// whose coordinates are gone, and <see cref="Waypoints"/> is empty.
/// </para>
/// <para>
/// <b><see cref="NavigateTo"/> is what fills them, and it computes them rather than recovering them.</b>
/// A route cannot be resumed from a save; it has to be planned afresh against the map. Once it has been,
/// every count above describes waypoints that really are here.
/// </para>
/// <para>
/// <b>Everything here is 16.16 fixed point and integer arithmetic, deliberately.</b> The original is, and
/// the truncations are load-bearing: the distance metric halves with a shift, the tolerances multiply by
/// a fixed-point constant and shift back, and the stuck test divides an integer count. Doing any of it in
/// floating point would give answers that are close and not the same.
/// </para>
/// <para>
/// <b>What this is not.</b> It does not steer - the behaviour list that produces a force is
/// <see cref="PeepSteering"/>, and following the route it plans is <see cref="PeepJourney"/>.
/// <b>Both are ticked from the park</b>, through <c>ParkPeople.OnUpdate</c> into
/// <see cref="PeepBehaviour"/> and <see cref="PeepWalk"/>, so a route planned here does make somebody
/// walk.
/// </para>
/// </summary>
public sealed class PeepNavigator
{
	/// <summary>What 1.0 is, and so also what "arrived" is on the scale <see cref="Progress"/> returns.</summary>
	public const int One = ParkWorld.NavigatorState.One;

	/// <summary>How many waypoints the navigator holds at once - the rest of a long route streams in.</summary>
	public const int Slots = ParkWorld.NavigatorState.SubpathSlots;

	/// <summary>
	/// How many of the most recent steps the "was I blocked" record covers. The original keeps them as
	/// bits in one word and only ever reads the low fifteen.
	/// </summary>
	public const int StuckWindow = 15;

	/// <summary>
	/// How many of those fifteen have to be blocked before a person gives up and asks for a new route.
	///
	/// <para>
	/// The original does not compare a count. It works out <c>blocked * 65536 / 15</c> - the fraction, in
	/// fixed point - and tests it against <c>0x6665</c>, which is one short of four tenths. Six blocked
	/// steps give <c>26214</c> and pass; five give <c>21845</c> and do not. So the real threshold is six,
	/// and it is written here as six rather than as a percentage that would have to be rediscovered.
	/// </para>
	/// </summary>
	public const int StuckStepsNeeded = 6;

	/// <summary>The fraction of a person's radius they must get within to count as having reached a corner.</summary>
	public const int MidPathTolerance = 2 * One;

	/// <summary>
	/// The same, at the end of the route, where it is <b>tighter</b> rather than looser - a person should
	/// stop on their destination more precisely than they round a corner.
	/// </summary>
	public const int LastLegTolerance = 0x19999;

	public int Radius { get; }

	/// <summary>
	/// Where this person is standing, in the same 16.16 fixed point as everything else here - so
	/// <see cref="FixedVector.One"/> is one map cell and <see cref="FixedVector.Cell"/> says which cell
	/// they are in. Settable because walking is what changes it.
	/// </summary>
	public FixedVector Position { get; set; }

	/// <summary>
	/// Where this person stood when the thing tick now running began - the original's <c>mPreviousX</c>
	/// and <c>mPreviousY</c> at person <c>+0x190</c>/<c>+0x194</c>, named by its own person-base
	/// serialiser <c>FUN_004f8b10</c>.
	///
	/// <para>
	/// <b>It exists so the drawing can interpolate.</b> The simulation moves somebody once every eight
	/// ticks - 248ms - and the original slides the picture between these two positions across the frames
	/// in between: <c>FUN_004f9f00</c> is <c>prev + (cur - prev) * t</c> per axis. See
	/// <c>docs/exe/ride-operation.md</c>, "Where a WALKING peep is drawn".
	/// </para>
	/// <para>
	/// <b>This is NOT the save's <c>mLastPosX</c>/<c>mLastPosY</c> at 430/434.</b> Those are a trailing
	/// sprite's own last placement, read only by <c>FUN_004fe900</c> behind a gate, and nothing here
	/// reads them at all - taking them for a previous position is the trap that field invites.
	/// </para>
	/// </summary>
	public FixedVector Previous { get; private set; }

	/// <summary>
	/// Marks where this person is standing now as where they started this tick.
	///
	/// <para>
	/// The original does this in <c>FUN_004fa870</c>, which is the <b>first call of every person kind's
	/// tick handler</b> - <c>FUN_00501650</c> at <c>0x00501658</c> for a guest, ahead of that handler's
	/// own <c>(id &amp; 3)</c> needs stagger - so it happens for everybody on every sweep, whatever state
	/// they are in. That last part is load-bearing: stamping only where somebody walks would leave a
	/// person who stopped with two different positions for ever, and the drawing would slide them back
	/// and forth between them.
	/// </para>
	/// <para>
	/// A teleport re-stamps it as well (<c>0x004fa95d</c>), or the picture slides all the way from
	/// wherever the person used to be.
	/// </para>
	/// </summary>
	public void StampPrevious() => Previous = Position;

	/// <summary>How fast and which way they are going, held to <see cref="MaxSpeed"/> by the steering step.</summary>
	public FixedVector Velocity { get; set; }

	/// <summary>
	/// The point they were heading for - the original's <c>path_target_pos</c>. This is a destination and
	/// not a waypoint of a route: it survives in the save where the route itself does not.
	/// </summary>
	public FixedVector Target { get; set; }

	/// <summary>The speed the steering step clamps velocity to.</summary>
	public int MaxSpeed { get; }

	/// <summary>The force the steering step clamps the summed behaviours to before applying them.</summary>
	public int MaxForce { get; }

	/// <summary>
	/// Which waypoint of the route the person is walking towards - an index into <see cref="Waypoints"/>.
	/// After a save it indexes waypoints that are not here; see the class remarks.
	///
	/// <para>
	/// <b>Written from outside only by <see cref="PeepWalk"/>, and that is why this and the three below are
	/// <c>internal</c> rather than private.</b> The original keeps all of this on one struct that the
	/// steering step and the route follower are both methods of; this project split them so each could be
	/// tested alone, so the join has to hand the follower's answer back. Nothing outside the assembly can
	/// move a person's cursor.
	/// </para>
	/// </summary>
	public int Cursor { get; internal set; }

	/// <summary>How many waypoints the whole route had, buffered or not.</summary>
	public int TotalWaypoints { get; private set; }

	/// <summary>
	/// How many of them are held at once. The route streams when it is longer than <see cref="Slots"/>.
	/// </summary>
	public int BufferedWaypoints { get; private set; }

	/// <summary>
	/// The distance of the legs still ahead within the buffer, which the cursor eats into. See
	/// <see cref="Cursor"/> for why this is settable within the assembly.
	/// </summary>
	public int BufferedDistance { get; internal set; }

	/// <summary>The distance of the part of the route that has not been loaded yet.</summary>
	public int TailDistance { get; private set; }

	/// <summary>How long the route was when it was planned, which is what progress is measured against.</summary>
	public int TotalDistance { get; private set; }

	private readonly List<FixedVector> _waypoints = [];

	/// <summary>
	/// The waypoints being carried, at most <see cref="Slots"/> of them, each the <b>centre</b> of the
	/// cell the pathfinder named. Empty until <see cref="NavigateTo"/> has planned a route.
	/// </summary>
	public IReadOnlyList<FixedVector> Waypoints => _waypoints;

	private readonly List<int> _legLengths = [];

	/// <summary>
	/// How long each carried leg is, so that passing one can be taken off <see cref="BufferedDistance"/>.
	/// One shorter than <see cref="Waypoints"/> - these are the gaps between them, and the walk from
	/// where the person is standing to the first is not one of them.
	/// </summary>
	public IReadOnlyList<int> LegLengths => _legLengths;

	/// <summary>
	/// Whether the person has reached the end of their route. See <see cref="Cursor"/> for why this is
	/// settable within the assembly.
	/// </summary>
	public bool Finished { get; internal set; }

	/// <summary>Whether the person has given up on getting there at all.</summary>
	public bool CannotReach { get; private set; }

	/// <summary>
	/// One bit per recent step, most recent lowest, set when that step was blocked or made no progress.
	/// The original keeps this single copy at <c>+0xb0</c>, written by the steering step and read by
	/// <c>follow_path</c>; see <see cref="Cursor"/> for why it is settable within the assembly.
	/// </summary>
	public int StuckBits { get; internal set; }

	public PeepNavigator( ParkWorld.NavigatorState saved )
	{
		Radius = saved.Radius;
		Position = new FixedVector( saved.X, saved.Y );

		// Standing still until something moves them, so where they started is where they are. Without
		// this a restored person would interpolate out of (0,0) on the park's very first frames.
		Previous = Position;
		Velocity = new FixedVector( saved.VelocityX, saved.VelocityY );
		Target = new FixedVector( saved.TargetX, saved.TargetY );
		MaxSpeed = saved.MaxSpeed;
		MaxForce = saved.MaxForce;

		// Mass and NavMode are parsed by the reader and deliberately not carried. The steering loop
		// divides the summed force by a literal 1.0 rather than by mass - which is why all eighteen
		// people still hold the value the constructor gave them - and what NavMode selects has not been
		// established. A field nothing reads cannot be wrong in an interesting way.
		Cursor = saved.PathCount;
		TotalWaypoints = saved.PathTotalCount;
		BufferedWaypoints = saved.PathBufferCount;
		TailDistance = saved.TailDistance;
		TotalDistance = saved.TotalDistance;
		BufferedDistance = saved.BufferedDistance;
		Finished = saved.PathFinished;
		CannotReach = saved.CantReachDest != 0;
		StuckBits = saved.StuckBits;
	}

	/// <summary>
	/// How far apart two points are, the way the navigator measures it - <b>which is not Euclidean</b>.
	///
	/// <para>
	/// It is the octagonal approximation: the larger of the two axes plus half the smaller, written as
	/// <c>ax + ay - min(ax, ay) / 2</c> and halved with a shift, so it truncates. Every distance in the
	/// navigator goes through this - the legs of a route, the total, and the test for having arrived - so
	/// using a real square root anywhere would put this out of step with the original by a few per cent in
	/// every diagonal direction and by nothing at all along the axes.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>One implementation, here and in <see cref="FixedVector.OctagonalLength"/>.</b>
	/// <c>PeepNavigatorTests</c> pins the metric against Manhattan <i>and</i> Euclidean, and against real
	/// legs out of the shipped save.
	/// </remarks>
	public static int Distance( int dx, int dy ) => new FixedVector( dx, dy ).OctagonalLength;

	/// <summary>
	/// Where a waypoint sits inside the cell it names. The pathfinder hands back whole cells as single
	/// bytes and the navigator walks to the <b>centre</b> of each, which is what the half adds.
	/// </summary>
	public static int WaypointCentre( int cell ) => cell * One + One / 2;

	/// <summary>How many waypoints of a route of this length are held at once.</summary>
	public static int BufferedFor( int waypoints ) => waypoints < Slots ? waypoints : Slots;

	/// <summary>
	/// How close a person has to get to a waypoint to have reached it: their radius, widened for a corner
	/// and tightened for the destination.
	/// </summary>
	public static int ToleranceFor( int radius, bool lastLeg )
		=> (int)(((long)radius * (lastLeg ? LastLegTolerance : MidPathTolerance)) >> 16);

	/// <summary>
	/// Whether a person has been blocked often enough to want a new route - six of the last fifteen steps.
	/// </summary>
	public static bool BlockedTooOften( int stuckBits )
	{
		var blocked = 0;

		for ( var bit = 0; bit < StuckWindow; ++bit )
			if ( (stuckBits & (1 << bit)) != 0 )
				++blocked;

		return blocked >= StuckStepsNeeded;
	}

	/// <summary>
	/// Adds up a route, given its waypoints in order and where the walker is starting from. The first leg
	/// is from the walker to the first waypoint, and every leg after that is between waypoints.
	/// </summary>
	public static int RouteDistance( int fromX, int fromY, IReadOnlyList<(int X, int Y)> waypoints )
	{
		if ( waypoints.Count == 0 )
			return 0;

		var total = Distance( waypoints[0].X - fromX, waypoints[0].Y - fromY );

		for ( var i = 0; i + 1 < waypoints.Count; ++i )
			total += Distance( waypoints[i + 1].X - waypoints[i].X, waypoints[i + 1].Y - waypoints[i].Y );

		return total;
	}

	/// <summary>
	/// How far along the route the walker is, as a fraction where <see cref="One"/> is arrived.
	///
	/// <para>
	/// It is one minus what is left over what there was: the distance still to run to the waypoint being
	/// walked to, plus the buffered legs beyond it, plus the part of the route not yet loaded. A person
	/// who has finished, or whose route had no length to begin with, is arrived - the second of those
	/// stops a route between two points in the same cell from dividing by nothing.
	/// </para>
	/// </summary>
	public int Progress( int toWaypointX, int toWaypointY )
	{
		if ( Finished || TotalDistance == 0 )
			return One;

		var remaining = TailDistance + BufferedDistance + Distance( toWaypointX, toWaypointY );

		return One - (int)(((long)remaining << 16) / TotalDistance);
	}

	/// <summary>
	/// Records whether the step just taken got anywhere, which is what <see cref="BlockedTooOften"/> reads.
	/// <b>Dead by CODE:</b> only <c>PeepNavigatorTests</c> calls it, and it shifts once either way. The live
	/// history is <see cref="PeepSteering.StuckBits"/>, which <c>PeepWalk.WriteBack</c> copies here.
	///
	/// <para>
	/// <b>How often the original shifts this word is not the same every step.</b> Read at
	/// <c>0050f54d</c>-<c>0050f5d6</c>: a step the map <i>refused</i> shifts and writes a one, and then the
	/// end of the tick shifts again - twice.
	/// A step that was <i>taken</i> does not shift at that first point at all, so it shifts once. Fifteen
	/// bits therefore cover fifteen steps for someone walking freely and rather fewer for someone blocked,
	/// which is what makes a truly stuck person trip the six-in-fifteen rule so much faster.
	/// </para>
	/// </summary>
	public void RecordStep( bool blocked )
		=> StuckBits = (StuckBits << 1) | (blocked ? 1 : 0);

	/// <summary>
	/// Walks the cursor on if the person has reached what they were walking to.
	///
	/// <para>
	/// On the last leg reaching it ends the route. Anywhere else it takes the leg just walked off the
	/// buffered distance and moves to the next waypoint; running off the end of the buffer is what asks
	/// for the rest of a streamed route. <b>Nothing in the game calls this</b>, only
	/// <c>PeepNavigatorTests</c>: the walk steps its own copy in <c>PeepJourney.NextWaypoint</c>, which
	/// calls <c>PeepJourney.Refill</c> - a renavigate from <see cref="PeepWalk"/> - at this same point.
	/// </para>
	/// </summary>
	/// <returns>Whether the buffer has run out and needs refilling.</returns>
	public bool StepTowards( int distanceToWaypoint, int legDistance )
	{
		if ( Finished || TotalWaypoints == 0 )
			return false;

		var lastLeg = Cursor == TotalWaypoints - 1;

		if ( distanceToWaypoint >= ToleranceFor( Radius, lastLeg ) )
			return false;

		if ( lastLeg )
		{
			Finished = true;
			return false;
		}

		BufferedDistance -= legDistance;
		++Cursor;

		return Cursor == BufferedWaypoints;
	}

	/// <summary>Gives up on the route, as the original does when it cannot find a way through.</summary>
	public void GiveUp() => CannotReach = true;

	/// <summary>
	/// Plans a route to a destination and takes it - the original's <c>FUN_0050f8e0</c>, which is the join
	/// between the pathfinder and a person, and the thing that has to happen before anybody can walk.
	///
	/// <para>
	/// <b>It plans; it never resumes.</b> The search runs from the cell this person is standing in to the
	/// cell the destination falls in, so the only thing carried over from a save is the destination itself
	/// - which survives where the route does not. See the class remarks.
	/// </para>
	/// <para>
	/// <b>The verdict is the search's, not the straightening pass's</b>, and that is worth stating because
	/// the original makes it look otherwise. <c>FUN_00511420</c> calls the search, hands its answer to
	/// <c>FUN_005108a0</c> as an argument, and returns what that gives back - and <c>FUN_005108a0</c>
	/// returns that same argument untouched at <b>both</b> of its exits. So improving a route can never
	/// change whether one was found, and <c>0x70000000</c> - the failure the caller tests for - is written
	/// only inside the search itself.
	/// </para>
	/// <para>
	/// <b>Failing is not merely "no route".</b> The original marks the person as having arrived <i>and</i>
	/// as unable to reach anywhere, zeroes every count and distance, and keeps the destination. Both flags
	/// are set, which reads oddly and is what the executable does.
	/// </para>
	/// </summary>
	/// <param name="destination">
	/// Where to go, in the same 16.16 as <see cref="Position"/>. Only the cell it falls in is searched
	/// for, but the whole point is kept as <see cref="Target"/>, because the last leg closes on the exact
	/// point rather than on the middle of its cell.
	/// </param>
	/// <param name="blocked">
	/// Whether a side of a cell is shut - <see cref="CellEdge.Blocked"/>, and for a park that is loaded
	/// <c>CellEdge.For( park, mode ).Blocked</c>. <b>The mode rides inside this</b>: the original keeps it
	/// in a global that the whole search reads, set from the <c>this + 0xb4</c> field of the object asking.
	/// That field is <b>not</b> one of the twenty the save reader parses and must not be confused with
	/// <c>NavigatorState.NavMode</c>, so no mode is chosen here - the caller picks one and says why.
	/// </param>
	/// <param name="addCurrent">
	/// Whether to put the cell the person is standing in on the front of the route. <b>Live at two of the
	/// original's four call sites</b>: <c>follow_path</c> passes 1 when the ground has changed underneath
	/// the person and when they have been stuck, and 0 when they have simply run out of carried
	/// waypoints; <c>FUN_00510100</c>, which is what the state machine reaches, passes 0.
	/// </param>
	/// <returns>Whether a route was found. False leaves the person having given up.</returns>
	public bool NavigateTo( FixedVector destination,
		Func<int, int, StepDirection, bool> blocked, bool addCurrent )
	{
		ArgumentNullException.ThrowIfNull( blocked );

		var from = Position.Cell;
		var route = new CellRoute { Start = from, Goal = destination.Cell };

		var reached = new CellSearch( route, CellReroute.Budget, blocked ).Run( from.X, from.Y );

		CellReroute.Run( route, blocked );

		// Written whether or not a way was found: the original stores the destination on both paths.
		Target = destination;

		if ( !reached )
			return GaveUpOnEverything();

		if ( addCurrent )
		{
			route.Waypoints.Insert( 0, from );

			// The original's buffer is a fixed 0xfc slots and it shifts the whole thing up by one, so at
			// the cap the last waypoint falls off the end and the count does not grow.
			if ( route.Waypoints.Count > CellSearch.SpliceRoom )
				route.Waypoints.RemoveAt( route.Waypoints.Count - 1 );
		}

		Take( route.Waypoints );

		return true;
	}

	/// <summary>
	/// What the original does when the search fails: keep the destination, throw away everything else, and
	/// set <b>both</b> the finished and the cannot-reach flags.
	/// </summary>
	private bool GaveUpOnEverything()
	{
		_waypoints.Clear();
		_legLengths.Clear();

		TotalWaypoints = 0;
		BufferedWaypoints = 0;
		TotalDistance = 0;
		BufferedDistance = 0;
		TailDistance = 0;
		Cursor = 0;
		StuckBits = 0;
		Finished = true;
		CannotReach = true;

		return false;
	}

	/// <summary>
	/// Takes a route the pathfinder found: the waypoints as cell centres, the three distances, and the
	/// bookkeeping put back to the start of a fresh walk.
	///
	/// <para>
	/// <b>The three distances are not three measurements of the same thing.</b>
	/// <see cref="TotalDistance"/> is the walk to the first waypoint plus every leg after it, over the
	/// whole route; <see cref="BufferedDistance"/> is only the legs <i>between</i> carried waypoints, and
	/// <see cref="TailDistance"/> only the legs beyond them. So the first leg belongs to the total alone,
	/// and <c>Total = firstLeg + Buffered + Tail</c> rather than the total being the other two added up.
	/// </para>
	/// </summary>
	private void Take( IReadOnlyList<(int X, int Y)> cells )
	{
		var centres = new List<(int X, int Y)>( cells.Count );

		foreach ( var (cellX, cellY) in cells )
			centres.Add( (WaypointCentre( cellX ), WaypointCentre( cellY )) );

		TotalWaypoints = centres.Count;
		BufferedWaypoints = BufferedFor( TotalWaypoints );
		TotalDistance = RouteDistance( Position.X, Position.Y, centres );

		_waypoints.Clear();
		_legLengths.Clear();

		for ( var i = 0; i < BufferedWaypoints; ++i )
			_waypoints.Add( new FixedVector( centres[i].X, centres[i].Y ) );

		BufferedDistance = 0;

		for ( var i = 0; i + 1 < BufferedWaypoints; ++i )
		{
			var leg = Distance( centres[i + 1].X - centres[i].X, centres[i + 1].Y - centres[i].Y );

			_legLengths.Add( leg );
			BufferedDistance += leg;
		}

		// The rest of the route, which streams in as the carried waypoints are used up. It starts at the
		// last carried one, so the leg that crosses out of the buffer is counted here and not above.
		TailDistance = 0;

		for ( var i = BufferedWaypoints - 1; i + 1 < TotalWaypoints; ++i )
			TailDistance += Distance( centres[i + 1].X - centres[i].X, centres[i + 1].Y - centres[i].Y );

		Cursor = 0;
		StuckBits = 0;
		Finished = false;
		CannotReach = false;
	}
}
