namespace OpenTPW;

/// <summary>
/// A person's journey along a route, and the steering force that keeps them on it - the back half of the
/// original's <c>follow_path</c> behaviour at <c>FUN_0050ed10</c>, which is the strongest thing pulling on
/// a walking guest after the walls.
///
/// <para>
/// <b>Every one of the three forces below had to be read off the disassembly</b>, because each is built by
/// a <c>thiscall</c> helper whose <c>ECX</c> the decompiler drops - and <c>ECX</c> is exactly what decides
/// whether a difference is <i>target minus position</i> or the other way about, and whether the last one
/// negates the velocity or something else. Decompiled they all read as bare two-argument calls.
/// </para>
/// <para>
/// <b>What is here and what is not.</b> This is the part that answers "which way should I be pushed": the
/// waypoint to aim at, when to move on to the next one, when to call it arrived, and how to slow down at
/// the end. The front of the same function - the ground-staleness check, the speed factor, and the
/// stuck-bit history that decides to renavigate - is deliberately not built yet, because it needs the
/// terrain stamp grid and the navigator, and naming it here is better than half-building it.
/// <see cref="Refill"/> stands in for the one call the walking half really does make.
/// </para>
/// <para>
/// <b>Two thresholds, and they are not the same.</b> A middle waypoint is passed at <b>two</b> radii and
/// the final destination is reached at <b>one and three fifths</b>, both measured with the eight-sided
/// distance rather than the true one. So a guest cuts a corner sooner than it settles on the end.
/// </para>
/// </summary>
public sealed class PeepJourney
{
	/// <summary>How near the end counts as arriving, in radii - <c>0x19999</c>, which is 1.6.</summary>
	public const int ArriveWithin = 0x19999;

	/// <summary>How near a middle waypoint counts as passing it, in radii - <c>0x20000</c>, which is 2.</summary>
	public const int PassWithin = 0x20000;

	/// <summary>
	/// What the remaining distance is divided by on the last leg to decide how fast to close on the end.
	/// The original builds this constant through the floating-point unit - <c>2.0 * 65536.0</c> - purely to
	/// get a fixed-point two, which is the only floating point anywhere in the peep simulation.
	/// </summary>
	public const int SlowingOver = 0x20000;

	/// <summary>Where the person is.</summary>
	public FixedVector Position { get; set; }

	/// <summary>How fast they are going, which every force here is measured against.</summary>
	public FixedVector Velocity { get; set; }

	/// <summary>How big the person is, which both thresholds are multiples of.</summary>
	public int Radius { get; set; }

	/// <summary>The fastest they may close on the end.</summary>
	public int MaxSpeed { get; set; }

	/// <summary>Where the journey is going, which is not the same as the last carried waypoint.</summary>
	public FixedVector Target { get; set; }

	/// <summary>
	/// The waypoints being carried. <b>The original carries at most five at a time</b> however long the
	/// route is, and asks the navigator for more when they run out.
	/// </summary>
	public List<FixedVector> Waypoints { get; } = [];

	/// <summary>How many waypoints the whole route has, which is not how many are carried.</summary>
	public int Count { get; set; }

	/// <summary>
	/// How far along the carried waypoints the person has got.
	///
	/// <para>
	/// <b>Settable on purpose</b>: when the carried waypoints run out the original navigates again, and
	/// navigating again restocks the list and puts this back to zero. Anything standing in for
	/// <see cref="Refill"/> has to be able to do the same.
	/// </para>
	/// </summary>
	public int Index { get; set; }

	/// <summary>Whether the end has been reached, after which the only force is a brake.</summary>
	public bool Arrived { get; set; }

	/// <summary>How much of the carried route is still to walk, which each passed leg comes off.</summary>
	public int CarriedLength { get; set; }

	/// <summary>How long each carried leg is, so passing one can be taken off the total.</summary>
	public List<int> LegLengths { get; } = [];

	/// <summary>
	/// Asked for more waypoints when the carried ones run out. The original navigates again from where the
	/// person is standing; left out, the journey simply stops advancing.
	/// </summary>
	public Action? Refill { get; set; }

	/// <summary>A distance in radii, in the fixed-point the rest of the simulation uses.</summary>
	private int Radii( int howMany ) => (int)(((long)howMany * Radius) >> 16);

	/// <summary>
	/// Which way the person should be pushed this tick, and move the journey on if a waypoint has been
	/// reached.
	///
	/// <para>
	/// <b>Arriving does not stop the force being worked out.</b> The original sets the flag and then falls
	/// straight through into the same arithmetic, so the tick a person arrives on still gets a real force;
	/// it is only the <i>next</i> tick that brakes. That is kept.
	/// </para>
	/// </summary>
	public FixedVector Steer()
	{
		// Nothing to follow, or nothing left to follow: stop.
		if ( Count == 0 || Arrived )
			return -Velocity;

		var target = Index == Count - 1 ? EndOfTheRoute() : NextWaypoint();

		if ( Index < Count - 1 )
		{
			// The original multiplies by one here. It is an identity and it is kept, because taking it out
			// would make this a different function for no gain.
			return (target - Position).ScaledBy( FixedVector.One ) - Velocity;
		}

		return ClosingOn( target );
	}

	/// <summary>The end itself, which is reached rather than passed - and at the closer of the two marks.</summary>
	private FixedVector EndOfTheRoute()
	{
		if ( (Target - Position).OctagonalLength < Radii( ArriveWithin ) )
			Arrived = true;

		return Target;
	}

	/// <summary>
	/// The waypoint being aimed at, stepping on to the next one if this one has been passed. <b>The
	/// waypoint is read again afterwards</b>, so a tick that steps on aims at the new one rather than
	/// spending a tick on the one it has just left.
	/// </summary>
	private FixedVector NextWaypoint()
	{
		if ( (Waypoints[Index] - Position).OctagonalLength < Radii( PassWithin ) )
		{
			if ( Index < LegLengths.Count )
				CarriedLength -= LegLengths[Index];

			++Index;

			if ( Index == Waypoints.Count )
				Refill?.Invoke();
		}

		// A departure, named rather than hidden. The original cannot read past the end here, because the
		// only way to arrive with the index level with the list is to have just navigated again, and that
		// restocks the list and sets the index back to zero. A stand-in that does neither would walk off
		// the end, so the last carried waypoint is used and the journey simply stops advancing.
		if ( Index >= Waypoints.Count )
			Index = Waypoints.Count - 1;

		return Waypoints[Index];
	}

	/// <summary>
	/// Closing on the end: aim at it no faster than half the distance still to go, and never faster than
	/// the person can walk.
	///
	/// <para>
	/// <b>Standing exactly on it answers with no force at all</b> - not with a brake. The original writes
	/// two zeroes and returns without ever reaching the subtraction, which is a different answer from
	/// <c>-velocity</c> and is worth not tidying into one.
	/// </para>
	/// </summary>
	private FixedVector ClosingOn( FixedVector target )
	{
		var toGo = target - Position;
		var far = toGo.Length;

		if ( far == 0 )
			return FixedVector.Zero;

		var slowly = (int)(((long)far << 16) / SlowingOver);
		var speed = MaxSpeed < slowly ? MaxSpeed : slowly;

		return toGo.ScaledBy( (int)(((long)speed << 16) / far) ) - Velocity;
	}
}
