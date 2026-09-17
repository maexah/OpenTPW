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
/// <b>The waypoints themselves are not here, and the counts below must not be read as if they were.</b>
/// <see cref="Cursor"/>, <see cref="TotalWaypoints"/> and <see cref="BufferedWaypoints"/> are numbers the
/// save records about a route; no coordinate of that route is carried, because the reader deliberately
/// does not parse <c>subpath_buffer[]</c>. <c>SetDest</c> writes only <c>path_buffer_count - 1</c> of the
/// distances, so the remaining slots hold the uninitialised fill or a stale value from an earlier route,
/// and reading them without that rule would produce entirely plausible wrong answers. A person therefore
/// knows how far along they were and not where they were going.
/// </para>
/// <para>
/// <b>Everything here is 16.16 fixed point and integer arithmetic, deliberately.</b> The original is, and
/// the truncations are load-bearing: the distance metric halves with a shift, the tolerances multiply by
/// a fixed-point constant and shift back, and the stuck test divides an integer count. Doing any of it in
/// floating point would give answers that are close and not the same.
/// </para>
/// <para>
/// <b>What this is not, yet.</b> It does not steer, and it cannot plan a route - the behaviour list that
/// produces a force, and the pathfinder that fills the waypoints, are both still to come. What it does is
/// everything the original's arrival behaviour does <i>around</i> those: measure distance, total a route
/// up, say how far along it a person is, advance the waypoint cursor, and decide when someone has been
/// blocked often enough to give up. All of that is self-contained, which is why it can be built and
/// checked before the parts that need a map.
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
	/// Which waypoint of the route the person was walking towards - an index, not a place. Nothing here
	/// can say where that waypoint is; see the class remarks.
	/// </summary>
	public int Cursor { get; private set; }

	/// <summary>How many waypoints the whole route had, buffered or not.</summary>
	public int TotalWaypoints { get; }

	/// <summary>
	/// How many of them the original had loaded at the moment it saved. The route streams when it is
	/// longer than <see cref="Slots"/>. This is the count the save recorded; the waypoints it counts are
	/// not carried.
	/// </summary>
	public int BufferedWaypoints { get; }

	/// <summary>The distance of the legs still ahead within the buffer, which the cursor eats into.</summary>
	public int BufferedDistance { get; private set; }

	/// <summary>The distance of the part of the route that has not been loaded yet.</summary>
	public int TailDistance { get; }

	/// <summary>How long the route was when it was planned, which is what progress is measured against.</summary>
	public int TotalDistance { get; }

	/// <summary>Whether the person has reached the end of their route.</summary>
	public bool Finished { get; private set; }

	/// <summary>Whether the person has given up on getting there at all.</summary>
	public bool CannotReach { get; private set; }

	/// <summary>
	/// One bit per recent step, most recent lowest, set when that step was blocked or made no progress.
	/// </summary>
	public int StuckBits { get; private set; }

	public PeepNavigator( ParkWorld.NavigatorState saved )
	{
		Radius = saved.Radius;
		Position = new FixedVector( saved.X, saved.Y );
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
	/// <b>One implementation, here and in <see cref="FixedVector.OctagonalLength"/>.</b> This was written
	/// twice - once here and once again later, character for character, by someone who had not read this
	/// file - and the two are now the same code. The tests above are the better of the two sets and are
	/// what cover it: they pin the metric against Manhattan <i>and</i> Euclidean, and against real legs
	/// out of the shipped save.
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
	///
	/// <para>
	/// <b>How often the original shifts this word is not the same every step, and an earlier draft of this
	/// comment said it was.</b> It said "twice per step". Read at <c>0050f54d</c>-<c>0050f5d6</c>: a step
	/// the map <i>refused</i> shifts and writes a one, and then the end of the tick shifts again - twice.
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
	/// for the rest of a streamed route, which is reported here rather than done, because nothing can
	/// refill it yet.
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
}
