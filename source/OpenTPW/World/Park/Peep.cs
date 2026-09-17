namespace OpenTPW;

/// <summary>
/// One guest in the park as the simulation sees them: what the save said they were doing, kept mutable
/// so that a tick can change it.
///
/// <para>
/// <see cref="ParkWorld.GuestState"/> is the record the save reader produces and is deliberately
/// immutable - it describes a file. This is the running copy, seeded from it once when the park opens.
/// </para>
/// <para>
/// <b>What this is not, yet.</b> It carries the needs and the behaviour a guest was saved in, and ticks
/// the needs. It does not walk, decide, queue or ride - those are the rest of the original's
/// <c>FUN_005019f0</c> and are deliberately not attempted here, the way the ride VM was brought up an
/// instruction at a time rather than all at once.
/// </para>
/// </summary>
public sealed class Peep
{
	/// <summary>The thing this guest is in the park's own numbering, which their tick slot depends on.</summary>
	public int ThingId { get; }

	/// <summary>Which of the eight kinds of guest they are - an index into <c>PeepTypes[0..7]</c>.</summary>
	public int PersonType { get; }

	/// <summary>What this guest is doing - see <see cref="SetState"/> for what entering one does.</summary>
	public PeepState State { get; private set; }

	/// <summary>The state to go back to once a one-off animation has finished.</summary>
	public PeepState SavedState { get; set; }

	public int Cash { get; set; }

	/// <summary>
	/// The countdown to going home, in the guest's own ticks rather than in seconds. The balance file
	/// calls its starting value <c>PeepInfo.ExitLevel</c> and says it is "in SECONDS", but the code only
	/// ever decrements it once per needs tick, so what it actually measures is turns of this loop.
	/// </summary>
	public int ExitLevel { get; set; }

	public float Happiness { get; set; }

	public float Thirst { get; set; }

	public float Hunger { get; set; }

	public float Toilet { get; set; }

	public float Illness { get; set; }

	public float Litter { get; set; }

	public int MajorDest { get; set; }

	public int QueuePos { get; set; }

	/// <summary>
	/// The speed term the walk reads to decide whether this guest is hurrying, which also picks a
	/// different walk animation. Set by the tick and by entering a state, and by nothing else.
	/// </summary>
	public int PurposeSpeed { get; private set; }

	/// <summary>
	/// How long this guest will wait for the park to open, set when they begin waiting. In their own
	/// ticks, like <see cref="ExitLevel"/>.
	/// </summary>
	public int ParkOpeningWait { get; private set; }

	/// <summary>When the guest last began a one-off animation, so that its end can be noticed.</summary>
	public int TimeOfLastSpotAnim { get; private set; }

	/// <summary>When the guest last began standing about, which the decision state reads.</summary>
	public int TimeStartedIdling { get; private set; }

	/// <summary>The animation the state they are in asked for as they entered it.</summary>
	public PeepAnimation Animation { get; private set; } = PeepAnimation.None;

	/// <summary>
	/// How far along a route this guest had got - the running copy of the navigator block the save keeps
	/// for them.
	///
	/// <para>
	/// <b>A guest restored from a save has the bookkeeping and not the route</b>, and keeping those two
	/// apart is what this paragraph is for. The file carries the cursor, how many waypoints the route had
	/// and how many were buffered, the three distances, whether it finished or gave up, and the stuck
	/// record. The waypoints ARE the route, and the save reader deliberately does not parse them -
	/// <c>subpath_buffer[]</c> is filled by <c>SetDest</c> for only <c>path_buffer_count - 1</c> entries,
	/// so the rest hold <c>0xCDCDCDCD</c> or a stale distance from whatever route was there before, and
	/// reading them without that rule would hand out numbers that look entirely plausible.
	/// </para>
	/// <para>
	/// <b>So a route is planned and never resumed.</b> <see cref="PeepNavigator.NavigateTo"/> is what puts
	/// waypoints here, by searching the live map from where this guest is standing to where they were
	/// going - the destination being the one part of a route that does survive a save. Until it has been
	/// called, <see cref="PeepNavigator.Waypoints"/> is empty.
	/// </para>
	/// <para>
	/// <b>Never null, and required rather than optional, because the save always has one.</b> The reader
	/// calls its navigator parser unconditionally for every person and says so - "every person has one,
	/// staff included" - so a guest without one would be a state the file cannot produce. Making it
	/// optional would have left two test helpers untouched at the cost of modelling something that does
	/// not exist.
	/// </para>
	/// </summary>
	public PeepNavigator Navigator { get; }

	public Peep( int thingId, ParkWorld.GuestState saved, ParkWorld.NavigatorState navigator )
	{
		Navigator = new PeepNavigator( navigator );
		ThingId = thingId;
		PersonType = saved.PersonType;
		State = (PeepState)saved.State;
		SavedState = (PeepState)saved.SavedState;
		Cash = saved.Cash;
		ExitLevel = saved.ExitLevel;
		Happiness = saved.Happiness;
		Thirst = saved.Thirst;
		Hunger = saved.Hunger;
		Toilet = saved.Toilet;
		Illness = saved.Illness;
		Litter = saved.Litter;
		MajorDest = saved.MajorDest;
		QueuePos = saved.QueuePos;
	}

	/// <summary>The range every need is held in - <c>FUN_004fb4f0</c> and the clamps inlined beside it.</summary>
	public const float Most = 100f;

	public const float Least = 0f;

	/// <summary>
	/// How many ticks apart a guest's needs are updated: the original gates the whole tick on
	/// <c>(id &amp; 3) == (tick &amp; 3)</c>, so each guest takes one turn in four and the park's guests are
	/// spread evenly across them.
	/// </summary>
	public const int TickShare = 4;

	/// <summary>How often the three needs that grow on their own do so - <c>TEST byte ptr [..],0xf</c>.</summary>
	public const int DriftEvery = 16;

	// How much each grows when it does. The original writes these as a subtraction of a negative
	// constant - FSUB of -1.0 and -2.0 - so they are growth, not decay.
	public const float ToiletDrift = 1f;

	public const float HungerDrift = 2f;

	public const float ThirstDrift = 2f;

	/// <summary>What a need sitting at its maximum costs in happiness, once per tick, per need.</summary>
	public const float UnhappinessPerMaxedNeed = 1f;

	/// <summary>
	/// The toilet level above which a guest hurries. A hardcoded <c>CMP AL,0x50</c> rather than a balance
	/// key: <c>PeepInfo.ToiletDesparate</c> is 100 and is a different threshold used elsewhere.
	/// </summary>
	public const int HurryAboveToilet = 80;

	public const int HurryingSpeed = 25;

	public const int UnhurriedSpeed = 0;

	/// <summary>
	/// Whether this guest's needs are updated on this tick. Their thing id decides which of the four
	/// slots they take, so it is fixed for the life of the guest.
	/// </summary>
	public bool DueOn( int tick ) => (ThingId & (TickShare - 1)) == (tick & (TickShare - 1));

	/// <summary>
	/// One turn of the guest's needs - the body of <c>FUN_00501650</c>, in its own order.
	///
	/// <para>
	/// <b>One term is deliberately missing, and it is named rather than quietly left out.</b> Between the
	/// countdown and the drift the original adds the guest's <i>cell's</i> own influence to happiness,
	/// illness and hunger - three signed shorts of a ten-byte per-cell record that placed objects and
	/// walking staff stamp into the cells around them. Those values are the balance file's
	/// <c>RegionFX[0..7]</c> and are well understood, but which of the eight a given thing stamps is
	/// chosen at each call site in the executable and is only known for two of them, so modelling it now
	/// would mean inventing the rest. Nothing here reads a cell, and nothing pretends to.
	/// </para>
	/// <para>
	/// <b>A quirk worth not tidying away.</b> The drift is gated on the same counter as the whole tick,
	/// and sixteen is a multiple of four - so <c>tick &amp; 15 == 0</c> can only happen on a tick where
	/// <c>tick &amp; 3 == 0</c>, and therefore only guests whose id is a multiple of four ever get
	/// hungrier, thirstier or more desperate on their own. The other three quarters of the park only
	/// change through what happens to them. That is what the original does, so it is what this does.
	/// </para>
	/// </summary>
	public void Tick( int tick )
	{
		if ( !DueOn( tick ) )
			return;

		--ExitLevel;

		if ( (tick & (DriftEvery - 1)) == 0 )
		{
			Toilet = Change( Toilet, ToiletDrift );
			Hunger = Change( Hunger, HungerDrift );
			Thirst = Change( Thirst, ThirstDrift );
		}

		// In the original's order, which matters only in that each one takes its bite out of happiness
		// in turn: illness, hunger, thirst, toilet.
		foreach ( var need in new[] { Illness, Hunger, Thirst, Toilet } )
		{
			// The original truncates to an integer before comparing with 100, so a need has to have
			// actually reached the top rather than merely be near it.
			if ( (int)need == (int)Most )
				Happiness = Change( Happiness, -UnhappinessPerMaxedNeed );
		}

		PurposeSpeed = Toilet > HurryAboveToilet ? HurryingSpeed : UnhurriedSpeed;
	}

	/// <summary>
	/// Enters a state: the self-contained half of the original's <c>FUN_00501db0</c>.
	///
	/// <para>
	/// That function writes the new state, queues an animation for it, and then does whatever entering
	/// it calls for. The animation and the effects below are everything it does that depends on the
	/// guest alone. The rest - joining a queue, being given a balloon, firing the events a ride raises,
	/// paying at the bus stop - reaches into a ride, the sprite table or the event ring, and is left for
	/// when those exist rather than half-written here.
	/// </para>
	/// </summary>
	public void SetState( PeepState next, int tick, Random random )
	{
		State = next;
		Animation = AnimationFor( next );

		switch ( next )
		{
			// How long they will put up with waiting outside, rolled once as they begin to wait.
			case PeepState.WaitingForOpening:
				ParkOpeningWait = (random.Next() % 150) + 200;
				break;

			case PeepState.PlayingSpotAnimation:
				TimeOfLastSpotAnim = tick;
				break;

			case PeepState.InQueue:
				TimeStartedIdling = tick;
				break;

			// Shuffling up a queue is never done in a hurry, whatever the guest's needs say.
			case PeepState.SteppingUpQueue:
				PurposeSpeed = UnhurriedSpeed;
				break;

			// Both of these give up on wherever they were going.
			case PeepState.Leaving:
			case PeepState.HeadingForExit:
				MajorDest = 0;
				break;
		}
	}

	/// <summary>
	/// Which animation a state queues as it is entered.
	///
	/// <para>
	/// Several of the standing states reach it only by falling through to the call at the end of the
	/// original's setter rather than by asking for it, which is why the two groups look arbitrary until
	/// they are read as "going somewhere" and "staying put".
	/// </para>
	/// <para>
	/// The four that queue nothing are not an omission. <see cref="PeepState.PlayingSpotAnimation"/> is
	/// already playing one, <see cref="PeepState.Leaving"/> queues none at all, and
	/// <see cref="PeepState.OnRide"/> and <see cref="PeepState.Riding"/> choose theirs from the state of
	/// the ride they are on - which nothing here can ask yet.
	/// </para>
	/// </summary>
	public static PeepAnimation AnimationFor( PeepState state ) => state switch
	{
		PeepState.Walking or PeepState.HeadingForGate or PeepState.Entering or PeepState.Wandering
			or PeepState.GoingToMinorDestination or PeepState.GoingToRide or PeepState.SteppingUpQueue
			or PeepState.BeingAdmitted or PeepState.HeadingForExit or PeepState.WalkingOutside
			=> PeepAnimation.Walk,

		PeepState.AtGate or PeepState.WaitingForOpening or PeepState.JudgingTheFee or PeepState.Deciding
			or PeepState.InQueue or PeepState.EnteringRide or PeepState.PickingACellOutside
			or PeepState.AtTheBusStop
			=> PeepAnimation.Stand,

		_ => PeepAnimation.None
	};

	/// <summary>
	/// Moves a need and holds it in range - <c>FUN_004fb4f0</c>, the one helper every need change in the
	/// original funnels through, and the same clamp that is written out by hand beside each of the
	/// others.
	/// </summary>
	public static float Change( float need, float by )
	{
		var moved = need + by;

		return moved > Most ? Most : moved < Least ? Least : moved;
	}
}
