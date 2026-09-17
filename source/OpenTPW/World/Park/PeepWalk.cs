namespace OpenTPW;

/// <summary>What one tick of walking came to.</summary>
public enum WalkVerdict
{
	/// <summary>Still going. The original returns 1.</summary>
	Walking,

	/// <summary>Got there. The original returns 0, and every state that walks reacts to it.</summary>
	Arrived,

	/// <summary>Gave up - no way through. The original returns 2 and the state machine complains.</summary>
	CannotReach
}

/// <summary>
/// One tick of a walking person - the original's <c>FUN_004fa2a0</c>, which is what the state machine calls
/// for a guest who is going somewhere, and the thing that finally makes anybody move.
///
/// <para>
/// <b>It is a join rather than an algorithm.</b> Every piece it uses was built and tested on its own:
/// <see cref="WallAvoidance"/> pushes off walls, <see cref="PeepJourney"/> follows the route,
/// <see cref="PeepSteering"/> adds those up and takes the step, and <see cref="PeepNavigator"/> plans the
/// route and measures the progress. What was missing until now was the thing that calls them in the right
/// order with the right state, which is all this is.
/// </para>
/// <para>
/// <b>The navigator is the record of truth, and the other two are views of it.</b> The original has one
/// struct - the navigator at <c>peep + 0xd4</c> - carrying position, velocity, the route, the cursor and the
/// stuck history together, and every function here is a method on it. This project split those across three
/// classes so each could be tested without the others, so the join has to seed the views from the navigator
/// and write them back. <b>It must also re-seed them the moment a route is replanned</b>, because
/// <see cref="PeepJourney.Refill"/> fires <i>inside</i> the force query and rewrites the navigator underneath
/// it - see <see cref="Renavigate"/>.
/// </para>
/// <para>
/// <b>Two of the three behaviours are here and the third is named.</b> The original registers exactly three
/// at <c>FUN_0050fdf0</c>, looking each up by a name stored on the behaviour itself: <c>avoid_walls</c> at
/// <c>0xe666</c>, <c>follow_path</c> at <c>0x8000</c> and <c>separation</c> at <c>0x1999</c>, in that order.
/// Separation needs the per-cell lists of who is standing where that the engine keeps and this project does
/// not, so it is left out - which makes crowds walk through one another and is a known gap rather than a
/// silent zero. The order does not matter: the weighted forces are summed, and addition does not care.
/// </para>
/// <para>
/// <b>Progress is measured to the waypoint being walked to, never to the destination.</b>
/// <c>FUN_0050fd40</c> loads <c>ECX</c> with <c>[ESI + cursor*8 + 0x64]</c> and subtracts the position from
/// it, on the last leg as much as any other - even though the journey is steering at
/// <see cref="PeepNavigator.Target"/> by then. Passing whatever the journey happens to be aiming at would
/// give a plausible and wrong number on every final leg, which is why it is spelled out here.
/// </para>
/// <para>
/// <b>What is deliberately not built.</b> The front of <c>follow_path</c> tests whether the ground has been
/// rebuilt under the person - <c>FUN_0050ed10</c> shifts the position right by <b>twenty</b>, so sixteen-cell
/// blocks, indexes a stamp table at <c>world + 0x2d8</c> and compares it against the stamp the navigator
/// recorded at <c>+0x48</c> when it planned. Neither the table nor that field exists here, so a person never
/// notices a path being demolished under them. The stuck half of the same front <i>is</i> built, because
/// everything it needs is present.
/// </para>
/// </summary>
public sealed class PeepWalk
{
	private readonly PeepNavigator _navigator;
	private readonly Func<int, int, StepDirection, bool> _blocked;
	private readonly PeepJourney _journey = new();
	private readonly PeepSteering _steering = new();
	private readonly List<Steer> _behaviours;

	/// <summary>
	/// A person and the map they are walking on.
	/// </summary>
	/// <param name="blocked">
	/// Whether a side of a cell is shut - <c>CellEdge.For( park, mode ).Blocked</c>. <b>The mode rides inside
	/// it</b>, and it is the same one the route was planned in: the original keeps it in a field of the
	/// navigator at <c>+0xb4</c> that both the pathfinder and <c>avoid_walls</c> read.
	/// </param>
	public PeepWalk( PeepNavigator navigator, Func<int, int, StepDirection, bool> blocked )
	{
		_navigator = navigator ?? throw new ArgumentNullException( nameof( navigator ) );
		_blocked = blocked ?? throw new ArgumentNullException( nameof( blocked ) );

		// Built once and kept, as the original keeps one list it rebuilds at startup rather than one per
		// person. The closures read the navigator as it stands when they are asked, which is what lets the
		// list outlive any one tick.
		_behaviours =
		[
			new Steer( PeepSteering.AvoidWallsWeight,
				() => WallAvoidance.Steer( _navigator.Position, _navigator.Velocity,
					_navigator.Radius, _blocked ) ),
			new Steer( PeepSteering.FollowPathWeight, () => _journey.Steer() )
		];

		_journey.Refill = () => Renavigate( addCurrent: false );

		Seed();
	}

	/// <summary>The route follower, exposed so a test can read how far along it the person has got.</summary>
	internal PeepJourney Journey => _journey;

	/// <summary>The steering step, exposed for the same reason - its stuck record is the interesting part.</summary>
	internal PeepSteering Steering => _steering;

	/// <summary>
	/// Which way this person is facing, as an eleven-bit turn - the heading the original writes onto the
	/// thing at <c>+0x1c</c>, and what <see cref="ParkWorld.Person.Facing"/> folds into one of eight
	/// octants for the picture to be chosen from.
	///
	/// <para>
	/// <b>Settable because it starts from the save.</b> A person who has not taken a step yet faces
	/// whatever the file says they face, and only a step they actually take turns them.
	/// </para>
	/// </summary>
	public int Heading { get; set; }

	/// <summary>
	/// Gives this person a route to where they were already going, which is what the save says and the only
	/// part of a route that survives one.
	///
	/// <para>
	/// <b>This is a departure and it is named.</b> In the original nobody plans a route from inside the walk:
	/// the state machine does it on the way in, through <c>FUN_00510100</c>, which is reached from three
	/// places in <c>FUN_004f9490</c> and one each in <c>FUN_004fa530</c> and <c>FUN_004fa5f0</c>. Those are
	/// the twenty-two state behaviours, which are not built. Until they are, a guest restored from a file has
	/// a destination and no route, and something has to ask for one or nobody ever takes a step.
	/// </para>
	/// </summary>
	public bool PlanRoute() => Renavigate( addCurrent: false );

	/// <summary>Whether this person has a route to walk at all.</summary>
	public bool HasRoute => _navigator.TotalWaypoints > 0 && _navigator.Waypoints.Count > 0;

	/// <summary>
	/// Where this person is standing, in the 16.16 the simulation uses - so <see cref="FixedVector.One"/>
	/// is one map cell. Asked by whatever draws them, which should read the walk rather than reach past it
	/// into the navigator.
	/// </summary>
	public FixedVector Position => _navigator.Position;

	/// <summary>
	/// Move this person on by one tick.
	///
	/// <para>
	/// The order is the original's and it matters. The stuck history can ask for a fresh route <i>before</i>
	/// anything is added up; the forces are then asked of the position as it stands; the step is taken or
	/// refused; and only then is progress measured and the verdict read. <b>Arriving is decided after the
	/// step, not before it</b>, so the tick a person arrives on is a tick they still moved.
	/// </para>
	/// </summary>
	public WalkVerdict Step()
	{
		// Where they were before any of this, which the heading at the end is measured against. The
		// original reads it first thing, at 004fa2bb, before the steering step runs.
		var before = _navigator.Position;

		// The half of follow_path's front that can be built: six of the last fifteen steps blocked and the
		// person asks for a new way round, keeping the cell they are standing in on the front of it.
		if ( !_navigator.Finished && PeepNavigator.BlockedTooOften( _navigator.StuckBits ) )
			Renavigate( addCurrent: true );

		_steering.Step( _behaviours, MayStep, Progress );

		WriteBack();

		// The original reads the give-up flag from the thing at +0x18c, which is the navigator's own +0xb8
		// seen through the person - the navigator sits at +0xd4, and 0xd4 + 0xb8 is 0x18c. It is the same
		// flag, checked after the step rather than before it.
		if ( _navigator.CannotReach )
			return WalkVerdict.CannotReach;

		if ( Progress() == PeepNavigator.One )
			return WalkVerdict.Arrived;

		// <b>Only now, and the ordering is the original's.</b> FUN_004fa2a0 returns at both of the cases
		// above BEFORE it works out any heading, so somebody who has arrived or given up keeps the way they
		// were last facing rather than being turned by the last twitch of a walk that is over. The position
		// is written every tick; the heading is not.
		var moved = _navigator.Position - before;

		Heading = PeepHeading.Of( -moved.X, moved.Y, Heading );

		return WalkVerdict.Walking;
	}

	/// <summary>
	/// Plans again from where the person is standing, and re-seeds the views from what came back.
	///
	/// <para>
	/// <b>The re-seed is the whole point of this being a method.</b> Refilling happens inside
	/// <see cref="PeepJourney.Steer"/>, which is called inside <see cref="PeepSteering.Step"/> - so the
	/// navigator's waypoints, cursor and distances are all replaced halfway through a tick, and a journey
	/// still holding the old ones would walk the wrong route for the rest of it. The original has no such
	/// problem because it only ever had the one copy.
	/// </para>
	/// </summary>
	private bool Renavigate( bool addCurrent )
	{
		var found = _navigator.NavigateTo( _navigator.Target, _blocked, addCurrent );

		// A failed plan zeroes every count and distance on the navigator. It also zeroes the last-progress
		// mark, which lives on the steering step here rather than on the navigator - the original writes
		// [ESI + 0xac] at 0050f971 along with all the rest, and leaving ours behind would have the next tick
		// measure progress against a route that no longer exists.
		if ( !found )
			_steering.LastProgress = 0;

		Seed();

		return found;
	}

	/// <summary>Fills the two views from the navigator, which is where the state actually lives.</summary>
	private void Seed()
	{
		_journey.Position = _navigator.Position;
		_journey.Velocity = _navigator.Velocity;
		_journey.Radius = _navigator.Radius;
		_journey.MaxSpeed = _navigator.MaxSpeed;
		_journey.Target = _navigator.Target;
		_journey.Count = _navigator.TotalWaypoints;
		_journey.Index = _navigator.Cursor;
		_journey.Arrived = _navigator.Finished;
		_journey.CarriedLength = _navigator.BufferedDistance;

		_journey.Waypoints.Clear();
		_journey.Waypoints.AddRange( _navigator.Waypoints );

		_journey.LegLengths.Clear();
		_journey.LegLengths.AddRange( _navigator.LegLengths );

		_steering.Position = _navigator.Position;
		_steering.Velocity = _navigator.Velocity;
		_steering.MaxForce = _navigator.MaxForce;
		_steering.MaxSpeed = _navigator.MaxSpeed;
		_steering.StuckBits = _navigator.StuckBits;
	}

	/// <summary>
	/// Puts the tick's result back on the navigator, which is what the next tick will be seeded from.
	/// </summary>
	private void WriteBack()
	{
		_navigator.Position = _steering.Position;
		_navigator.Velocity = _steering.Velocity;
		_navigator.StuckBits = _steering.StuckBits;

		// The journey is what walks the cursor on and decides that the end has been reached, so those come
		// back from it rather than from the steering step.
		_navigator.Cursor = _journey.Index;
		_navigator.Finished = _journey.Arrived;
		_navigator.BufferedDistance = _journey.CarriedLength;

		// And the views follow the position, so a behaviour asked again this tick sees where the person
		// actually ended up.
		_journey.Position = _navigator.Position;
		_journey.Velocity = _navigator.Velocity;
	}

	/// <summary>
	/// Whether a person may go from one place to the other - <see cref="MapStep.CanStep"/> over the two cells,
	/// which is what the original asks through a function pointer at <c>PTR_FUN_00762190</c>.
	/// </summary>
	private bool MayStep( FixedVector from, FixedVector to )
	{
		var (fromX, fromY) = from.Cell;
		var (toX, toY) = to.Cell;

		return MapStep.CanStep( MapStep.CellId( fromX, fromY ), MapStep.CellId( toX, toY ), _blocked );
	}

	/// <summary>
	/// How far along the route the person is, measured to the waypoint they are walking to - see the class
	/// remarks for why that is not the destination.
	/// </summary>
	private int Progress()
	{
		if ( _navigator.Waypoints.Count == 0 )
			return PeepNavigator.One;

		// The cursor can sit one past the carried waypoints for the moment between a route running out and
		// being refilled; the original reads the slot anyway, and reading the last one is the nearest honest
		// thing to that which cannot fault.
		var at = _navigator.Cursor < _navigator.Waypoints.Count
			? _navigator.Cursor
			: _navigator.Waypoints.Count - 1;

		var waypoint = _navigator.Waypoints[at];

		// <b>From where the step LANDED, not from where it set off.</b> The original writes the navigator's
		// own position in place at 0050f3b0 the instant the map allows the step, and only then calls
		// FUN_0050fd40, which reads [ESI + 8] - so the distance is measured after the move. Here the
		// steering step holds the live position for the length of a tick and the navigator only catches up
		// in WriteBack, so reading the navigator would measure from the previous tick's position and hand
		// back a number that is wrong by exactly one step, every step.
		var from = _steering.Position;

		return _navigator.Progress( waypoint.X - from.X, waypoint.Y - from.Y );
	}
}
