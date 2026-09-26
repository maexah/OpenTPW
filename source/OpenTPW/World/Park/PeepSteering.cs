namespace OpenTPW;

/// <summary>
/// One behaviour pulling on a walking person, and how much say it gets.
/// </summary>
/// <param name="Weight">A fraction: nine tenths for the walls, a half for the route, a tenth for crowding.</param>
/// <param name="Force">Which way this behaviour wants the person pushed, asked afresh every tick.</param>
public readonly record struct Steer( int Weight, Func<FixedVector> Force );

/// <summary>
/// One tick of a walking person - the original's <c>FUN_0050f3b0</c>, which is the thing that actually
/// moves anybody.
///
/// <para>
/// Each behaviour is asked which way it would push, its answer is <b>clamped before it is weighted</b> so
/// that no single behaviour can shout, and the weighted answers are added up. The total is clamped again,
/// divided, added to the speed the person already has, and clamped once more - this time to how fast they
/// can walk. Only then is the step tried, and <b>only taken if it is a step a person may make</b>.
/// </para>
/// <para>
/// <b>The dividing step divides by one.</b> The original pushes the literal <c>0x10000</c> as the divisor,
/// so what reads like "divide by the mass" divides by nothing, and there is no mass on a person anywhere.
/// It is kept because the original keeps it.
/// </para>
/// <para>
/// <b>The record of being stuck does not advance at the same rate every tick.</b> A refused step shifts
/// the history and writes a one; a taken step does not shift there at all. Then, at the end of every
/// tick either way, it shifts again and writes a one if the person got
/// no closer than last time. So <b>a walking person collects one bit a tick and a blocked one collects
/// two</b> - which is why a genuinely stuck person reaches the two-fifths mark that triggers a fresh
/// route so much faster than a merely slow one.
/// </para>
/// </summary>
public sealed class PeepSteering
{
	/// <summary>Keeping off the walls, at <c>0xe666</c> - nine tenths, and the loudest of the three.</summary>
	public const int AvoidWallsWeight = 0xe666;

	/// <summary>Following the route, at <c>0x8000</c> - a half.</summary>
	public const int FollowPathWeight = 0x8000;

	/// <summary>
	/// Keeping out of other people's way, at <c>0x1999</c> - a tenth. <b>Not built</b>: it needs the
	/// per-cell lists of who is standing where that the engine keeps and this project does not. Named
	/// here so that its absence is a known gap rather than a silent zero.
	/// </summary>
	public const int SeparationWeight = 0x1999;

	/// <summary>Where the person is. Only moved when the step is allowed.</summary>
	public FixedVector Position { get; set; }

	/// <summary>How fast they are going, which is updated whether or not the step is allowed.</summary>
	public FixedVector Velocity { get; set; }

	/// <summary>The most any one behaviour, or all of them together, may ask for.</summary>
	public int MaxForce { get; set; }

	/// <summary>The fastest this person walks.</summary>
	public int MaxSpeed { get; set; }

	/// <summary>
	/// The last fifteen ticks of not getting anywhere, newest in the low bits. Two things write into it:
	/// a refused step and a tick that got no closer.
	///
	/// <para>
	/// <b>Settable because the original keeps only one of these.</b> It lives on the navigator at
	/// <c>+0xb0</c>, where the steering step writes it and <c>follow_path</c> reads it to decide whether to
	/// ask for a new route; splitting those across two classes here means <see cref="PeepWalk"/> has to
	/// carry the one value between them, and a fresh route zeroes it.
	/// </para>
	/// </summary>
	public int StuckBits { get; set; }

	/// <summary>How close the person was last tick, which is what "got no closer" is measured against.</summary>
	public int LastProgress { get; set; }

	/// <summary>
	/// Move this person on by one tick.
	/// </summary>
	/// <param name="behaviours">
	/// The things pulling on them, asked in order. The original keeps three in a list it rebuilds at
	/// startup; passing them in keeps this from having to know which three.
	/// </param>
	/// <param name="mayStep">
	/// Whether a person may go from one place to the other - <see cref="MapStep.CanStep"/> over the two
	/// cells, which is what the original asks through a function pointer.
	/// </param>
	/// <param name="progress">
	/// How far along the person is, larger being closer. Asked once, at the end. The original works it out
	/// from what is left of the route; it is taken here so that this stays one tick of steering and
	/// nothing else.
	/// </param>
	public void Step( IReadOnlyList<Steer> behaviours,
		Func<FixedVector, FixedVector, bool> mayStep, Func<int> progress )
	{
		ArgumentNullException.ThrowIfNull( behaviours );
		ArgumentNullException.ThrowIfNull( mayStep );
		ArgumentNullException.ThrowIfNull( progress );

		var wanted = FixedVector.Zero;

		foreach ( var behaviour in behaviours )
		{
			// Clamped first, then weighted - so a behaviour cannot buy itself extra say by shouting.
			wanted += behaviour.Force().ClampedTo( MaxForce ).ScaledBy( behaviour.Weight );
		}

		// And the total is held to the same limit as any one of them.
		var pull = wanted.ClampedTo( MaxForce ).DividedBy( FixedVector.One );

		Velocity = (Velocity + pull).ClampedTo( MaxSpeed );

		var goingTo = Position + Velocity;

		if ( mayStep( Position, goingTo ) )
			Position = goingTo;
		else
			StuckBits = (StuckBits << 1) | 1;

		// This one happens every tick, whether the step was taken or not.
		var closeness = progress();

		StuckBits <<= 1;

		if ( closeness <= LastProgress )
			StuckBits |= 1;

		LastProgress = closeness;
	}
}
