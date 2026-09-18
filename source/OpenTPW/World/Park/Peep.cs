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
/// <b>What this is and is not.</b> It carries the needs and the behaviour a guest was saved in, ticks the
/// needs, and holds the state that <see cref="PeepBehaviour"/> moves them through - thirteen of the
/// original's twenty-two cases, which is enough to get a guest to the gate, through it, to a ride, onto
/// it and off again. <b>This said five, and named choosing a ride and queueing for one as deliberately
/// not attempted</b>; both are built, and what is left of <c>FUN_005019f0</c> is going home and the
/// states that want a world this does not simulate yet.
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

	/// <summary>
	/// How close this guest is to being sick - <c>PeepInfo.VomitCapacity</c> is the level it is measured
	/// against, and a ride raises it by the ride's excitement over <c>PeepInfo.RideVomitDivisor</c>.
	/// </summary>
	/// <remarks>
	/// <b>This was called <c>Illness</c> until 2026-09-17 and the name was wrong</b> - see the note beside
	/// <c>ParkWorld.ReadGuest</c>. The balance file calls the same meter "illness" in
	/// <c>RegionFX[i].Illness</c> and <c>DecisionVarIllnessWeight</c>, so both words describe it; the
	/// field's own name in the save is the one carried here.
	/// </remarks>
	public float Vomit { get; set; }

	public float Litter { get; set; }

	public int MajorDest { get; set; }

	public int QueuePos { get; set; }

	/// <summary>
	/// How much longer this guest will put up with standing out of place before they re-take their
	/// position in a queue - <c>mQueueMoveDelay</c>, the four bytes at guest-block offset 490.
	///
	/// <para>
	/// <b>It paces the shuffle rather than gating it.</b> <c>FUN_004ffff0</c> compares
	/// <see cref="QueuePos"/> against the place the queue links actually give and, when the two differ,
	/// re-takes it at once if this is nought <b>or</b> if they are more than
	/// <see cref="PeepBehaviour.QueueDriftAllowed"/> out; otherwise it spends one of these and waits.
	/// </para>
	/// </summary>
	public int QueueMoveDelay { get; set; }

	/// <summary>
	/// The speed term the walk reads to decide whether this guest is hurrying, which also picks a
	/// different walk animation - the person's own <c>+0xc2</c>.
	///
	/// <para>
	/// Written by the needs tick, by entering a state, and by <see cref="PeepBehaviour"/>, which is why the
	/// setter is <c>internal</c> rather than private: <c>FUN_004ff730</c> decides afresh every turn whether
	/// a guest hurries to the gate, before it walks them. Nothing outside the assembly can set it.
	/// </para>
	/// </summary>
	public int PurposeSpeed { get; internal set; }

	/// <summary>
	/// Which visitor this guest was, counting every admission the park has ever made, or zero for somebody
	/// who has not been admitted.
	///
	/// <para>
	/// The original keeps it at <c>person + 0x1d8</c> and writes it once, as a guest finishes coming
	/// through the gate: <c>FUN_004ffb20</c> stores what <c>FUN_0051aaf0</c> hands back, which is the
	/// world's running total after the increment. Not parsed from the save - a guest who was already inside
	/// when the park was written carries a number this cannot know.
	/// </para>
	/// </summary>
	public int VisitorNumber { get; internal set; }

	/// <summary>
	/// How long this guest will go on waiting, in their own ticks, like <see cref="ExitLevel"/> - the
	/// original's <c>mParkOpeningWaitingTime</c>.
	///
	/// <para>
	/// <b>Two states share it, so the name is narrower than the field.</b> Beginning to wait for a shut
	/// park rolls <c>rand % 150 + 200</c> here (<see cref="SetState"/>); a guest who thinks the fee
	/// expensive but not outrageous rolls <c>rand % 50 + 50</c> into the same field and re-judges when it
	/// reaches nought. The name is the save's own and is kept for that reason.
	/// </para>
	/// <para>
	/// The setter is <c>internal</c> for the same reason <see cref="PurposeSpeed"/>'s is: the behaviour
	/// writes it, and nothing outside the assembly should.
	/// </para>
	/// </summary>
	public int ParkOpeningWait { get; internal set; }

	/// <summary>
	/// Whether this guest has already accepted what the park charges - the original's
	/// <c>mPaidAdmission</c>.
	///
	/// <para>
	/// <b>This is what makes waiting outside a two-way state</b>, and it is carried from the save rather
	/// than assumed: <c>FUN_004ff7f0</c> tests it and nothing else to choose between sending a guest back
	/// to the ticket booths and letting them through the gate, so a guest saved mid-wait must be restored
	/// with whichever answer the file gave.
	/// </para>
	/// </summary>
	public bool PaidAdmission { get; internal set; }

	/// <summary>
	/// Whether a ride has said this guest may come aboard - the original's <c>mBeenAdmitted</c>.
	///
	/// <para>
	/// <b>It is a one-shot flag the guest clears themselves.</b> A guest standing at the front of a queue
	/// (<see cref="QueuePos"/> nought) who carries it, and whom the ride has actually nominated, clears it
	/// and sets off to board - <c>FUN_004ffff0</c>. Clearing it is what stops them boarding twice off one
	/// invitation.
	/// </para>
	/// </summary>
	public bool BeenAdmitted { get; internal set; }

	/// <summary>When the guest last began a one-off animation, so that its end can be noticed.</summary>
	public int TimeOfLastSpotAnim { get; private set; }

	/// <summary>
	/// When the guest last began standing about, which the decision state reads - <c>mTimeStartedIdling</c>.
	///
	/// <para>
	/// <b>Written by two states, not one</b>, which is why the setter is <c>internal</c> rather than
	/// private: entering a queue stamps it (<see cref="SetState"/>), and <c>FUN_004fec90</c> stamps it again
	/// each time a guest decides something, because the thirty-turn gate in front of choosing a ride is
	/// measured from it.
	/// </para>
	/// </summary>
	public int TimeStartedIdling { get; internal set; }

	/// <summary>The animation the state they are in asked for as they entered it.</summary>
	public PeepAnimation Animation { get; private set; } = PeepAnimation.None;

	/// <summary>
	/// The animation asked for but not yet started, and how fast to run it - the person's own
	/// <c>mNextAnim</c> and <c>mNextServiceInterval</c>, which sit beside the sprite handle in their block.
	///
	/// <para>
	/// <b>A change of animation is queued, not applied.</b> <c>FUN_004217f0</c> does nothing but write the
	/// number down; <c>FUN_004d4190</c> is what hands it to the sprite, and its only caller is the
	/// per-guest needs call - so an animation asked for by the walk waits for that guest's own turn in four
	/// rather than starting the instant it is wanted.
	/// </para>
	/// <para>
	/// <b>Zero means nothing is waiting</b>, for both. That is why an interval of zero leaves the sprite's
	/// own alone instead of setting it to nothing, which matters because the walk's arithmetic truncates to
	/// zero for a person in a hurry.
	/// </para>
	/// </summary>
	public int NextAnimation { get; set; }

	/// <inheritdoc cref="NextAnimation"/>
	public int NextInterval { get; set; }

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
		Vomit = saved.Vomit;
		Litter = saved.Litter;
		MajorDest = saved.MajorDest;
		QueuePos = saved.QueuePos;
		QueueMoveDelay = saved.QueueMoveDelay;

		// Both of these decide what a guest partway through being admitted does next, so they are seeded
		// rather than started fresh - the shipped park has a guest saved waiting for the gate, and whether
		// they have paid is the whole of what happens to them.
		PaidAdmission = saved.PaidAdmission != 0;
		ParkOpeningWait = saved.ParkOpeningWait;

		// Carried from the file for the same reason: a guest saved at the front of a queue with a ride
		// already expecting them must not lose their place by being restored without it.
		BeenAdmitted = saved.BeenAdmitted != 0;
	}

	/// <summary>The range every need is held in - <c>FUN_004fb4f0</c> and the clamps inlined beside it.</summary>
	public const float Most = 100f;

	public const float Least = 0f;

	/// <summary>
	/// How many <b>thing</b> ticks apart a guest's needs are updated: <c>(id &amp; 3) == (tick &amp; 3)</c>,
	/// so each guest takes one turn in four and the park's guests are spread evenly across them.
	///
	/// <para>
	/// <b>The tick counted here is the thing engine's, not the game's 31ms beat</b> - see
	/// <see cref="ParkPeople.ThingTickEvery"/>, which is eight of those to one of these. The original
	/// reads a counter of its own for this test rather than the loop tick the engine is gated on, and the
	/// distinction is load-bearing: eight divides four, so a share taken over game ticks would be true
	/// only for guests whose id divides four, and the other three quarters would never age at all.
	/// </para>
	///
	/// <para>
	/// <b>This gates the needs and nothing else, and an earlier draft of this comment said it gated "the
	/// whole tick".</b> It does not, and the difference is visible: a guest is ticked by
	/// <c>FUN_0050b360</c>, which switches on the thing's model byte and, for a guest, calls
	/// <c>FUN_00501650</c> (the needs) and then <c>FUN_005019f0</c> (the twenty-two behaviours) back to
	/// back. The test above lives <i>inside</i> the first of those - and not even around all of it, since
	/// the call at its head and the queue check at its tail both sit outside. The second has no such test
	/// anywhere in it. So <b>walking runs every tick</b> and only the needs take one turn in four; believing
	/// otherwise would have walked every guest in the park at a quarter speed.
	/// </para>
	/// </summary>
	public const int TickShare = 4;

	/// <summary>
	/// Whether a guest in this state is walking somewhere, and so should be given a turn of
	/// <see cref="PeepWalk"/>.
	///
	/// <para>
	/// <b>Eleven of the twenty-two, found by asking which handlers can reach the walk tick at all.</b>
	/// <c>FUN_005019f0</c> switches on the state and calls <c>FUN_004fa2a0</c> - the walk - from four of its
	/// cases in its own body; but most cases jump straight to a handler that makes the call itself, one
	/// level down: <c>FUN_004ff730</c> for <see cref="PeepState.HeadingForGate"/>, <c>FUN_004ffb20</c> for
	/// <see cref="PeepState.Entering"/>, <c>FUN_004fff20</c>, <c>FUN_004ffbc0</c>, <c>FUN_005006b0</c>,
	/// <c>FUN_00500900</c> and <c>FUN_00500a50</c> for the rest. Searching every one of the twenty-two
	/// handlers for a path to the walk gives states 0, 2, 5, 7, 9, 10, 12, 13, 15, 18 and 20.
	/// </para>
	/// <para>
	/// <b>An earlier version of this said four, and it was wrong in a way that mattered.</b> It was read off
	/// the body of the switch alone - stopping at the calls that function makes itself rather than following
	/// the ones its handlers make. The cost was not academic: every guest in the shipped park is in
	/// <see cref="PeepState.HeadingForGate"/> or <see cref="PeepState.Entering"/> or
	/// <see cref="PeepState.WaitingForOpening"/>, and the first two are among the seven that reading missed,
	/// so nobody in Lost Kingdom would have taken a single step.
	/// </para>
	/// <para>
	/// <b>Ten of the eleven agree with a list written from the other direction.</b> <see cref="AnimationFor"/>
	/// queues the walking animation for exactly ten states, and they are these without
	/// <see cref="PeepState.LeavingRide"/>. That is not a contradiction: the animation list says which picture is
	/// shown, and a guest on a ride takes its picture from the ride while still being moved.
	/// </para>
	/// </summary>
	public static bool IsAWalkingState( PeepState state ) => state is
		PeepState.Walking or PeepState.HeadingForGate or PeepState.Entering or PeepState.Wandering
		or PeepState.GoingToMinorDestination or PeepState.GoingToRide or PeepState.SteppingUpQueue
		or PeepState.BeingAdmitted or PeepState.LeavingRide or PeepState.HeadingForExit
		or PeepState.WalkingOutside;

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
	/// What each place further back in a queue costs in <see cref="QueueMoveDelay"/> - the float at
	/// <c>0x007007a4</c>, which is <b>1.2</b>, and which <c>FUN_00501db0</c>'s case <c>0xb</c> multiplies
	/// <see cref="QueuePos"/> by on the way into <see cref="PeepState.InQueue"/>.
	/// </summary>
	/// <remarks>
	/// Read out of the executable rather than guessed: the instructions are <c>FILD</c> of the queue place,
	/// <c>FMUL float ptr [0x007007a4]</c>, then the truncation into <c>+0x1f4</c>. A guest at the front
	/// therefore waits not at all, which is what keeps the head of a queue responsive.
	/// </remarks>
	public const float QueueDelayPerPlace = 1.2f;

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
		// in turn: vomit, hunger, thirst, toilet.
		foreach ( var need in new[] { Vomit, Hunger, Thirst, Toilet } )
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

		// <b>And QUEUE it, which is what the original does and what this used to leave out.</b>
		// FUN_00501db0 does not merely record the animation a state wants - it calls FUN_004217f0, which
		// decompiles to a bare `*(person + 4) = value`, the person's own mNextAnim. Recording it in
		// Animation and nothing else left ParkPeople.Apply - which reads NextAnimation - with nothing to
		// hand the sprite, so a guest who arrived somewhere kept playing the walk they arrived on, for
		// ever, on screen. No test saw it: the one that should have asserted AnimationFor(State), which is
		// a table lookup that never touches a sprite.
		//
		// The four states that queue nothing do not call FUN_004217f0 at all, and they are exactly the
		// four AnimationFor answers None for - so the guard here is the original's own shape rather than a
		// defensive check.
		if ( Animation != PeepAnimation.None )
			NextAnimation = (int)Animation;

		switch ( next )
		{
			// How long they will put up with waiting outside, rolled once as they begin to wait.
			case PeepState.WaitingForOpening:
				ParkOpeningWait = (random.Next() % 150) + 200;
				break;

			case PeepState.PlayingSpotAnimation:
				TimeOfLastSpotAnim = tick;
				break;

			// <b>And the move delay is seeded from how far back they are.</b> FUN_00501db0's case 0xb
			// reads mQueuePos, converts it to a float, multiplies by the constant at 0x007007a4 - which
			// is 1.2 - and truncates it back into mQueueMoveDelay. So somebody at the back of a long queue
			// waits proportionally longer before shuffling up, and the guest at the front waits not at all.
			// This was missing when the step-up was built, so the delay was read from the save once and
			// never renewed.
			case PeepState.InQueue:
				TimeStartedIdling = tick;
				QueueMoveDelay = (int)(QueuePos * QueueDelayPerPlace);
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
	/// <see cref="PeepState.LeavingRide"/> and <see cref="PeepState.Riding"/> choose theirs from the state of
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
