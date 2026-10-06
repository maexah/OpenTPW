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
/// needs, and holds the state that <see cref="PeepBehaviour"/> moves them through - eighteen of the
/// original's twenty-two cases, which is enough to get a guest to the gate, through it, to a ride, onto
/// it and off again. What is left of <c>FUN_005019f0</c> is the end of going home and the states that
/// want a world this does not simulate yet.
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
	/// ever decrements it once per needs tick, so what it actually measures is turns of this loop. It has no
	/// floor, and only the deciding turn reads it, for exactly nought (<see cref="PeepBehaviour.WantsToLeave"/>).
	/// </summary>
	public int ExitLevel { get; set; }

	public float Happiness { get; set; }

	/// <summary>The saved or newly drawn prankery index; initialization is in <c>docs/exe/guest-arrivals.md</c>.</summary>
	public int PrankeryIndex { get; set; }

	/// <summary>
	/// Happiness as it was when this guest joined the queue of the thing they are visiting - <c>+0x20c</c>, copied
	/// at the join (<c>0x004ffd92</c>) and read only by the settle-up, which averages three times the change into
	/// the thing's satisfaction.
	/// </summary>
	/// <remarks>
	/// Not saved: the original zeroes it before reading a guest's record (<c>0x0051861c</c>), so a guest loaded
	/// already queued or riding compares against nought.
	/// </remarks>
	public float JoinHappiness { get; set; }

	/// <summary><c>mNumRides</c>, <c>+0x1c4</c>: rides ridden, which the settle-up counts.</summary>
	public int NumRides { get; set; }

	/// <summary><c>mNumShops</c>, <c>+0x1c8</c>: purchases made.</summary>
	public int NumShops { get; set; }

	/// <summary><c>mNumSideshows</c>, <c>+0x1cc</c>: sideshows played, won or lost.</summary>
	public int NumSideshows { get; set; }

	/// <summary><c>mNumSideshowsWon</c>, <c>+0x1d0</c>: sideshows won.</summary>
	public int NumSideshowsWon { get; set; }

	/// <summary>
	/// Which kind of sprite this guest wears - <c>mESPSprite</c>, <c>+0x24</c>: a child
	/// (<see cref="ParkSpriteBanks.ChildKind"/>) or a costume (<see cref="ParkSpriteBanks.CostumeKind"/>). A Costume Shop
	/// changes it (<see cref="ParkRideOperation"/>'s settle-up), and it is what the guest is drawn in.
	/// </summary>
	public int SpriteKind { get; set; }

	/// <summary>Which bank of <see cref="SpriteKind"/> - <c>mSpriteID</c>, <c>+0x20</c>: which child, or which costume.</summary>
	public int SpriteBank { get; set; }

	/// <summary>
	/// The balloon this guest holds, or null - <c>mBalloonScript</c>, <c>+0x210</c>, a slot in the sprite table there.
	/// A Balloon Shop gives it, boarding anything takes it away (<see cref="SetState"/>) and leaving brings it back
	/// (<c>docs/exe/ride-operation.md</c>, "A held balloon").
	/// </summary>
	public Balloon? Balloon { get; set; }

	/// <summary>
	/// How many more needs sweeps the balloon lasts - <c>mRemainingBalloonLife</c>, <c>+0x214</c>. It outlasts the
	/// sprite: a guest on a ride keeps it, and <see cref="Tick"/> counts it down whether or not they hold one.
	/// </summary>
	public int BalloonLife { get; set; }

	/// <summary>
	/// Where the held balloon goes next frame, across, in world units - <c>mLastPosX</c>, <c>+0x218</c>; see
	/// <see cref="Balloon.Place"/>. <see cref="BalloonLastY"/> (<c>mLastPosY</c>) is down.
	/// </summary>
	public float BalloonLastX { get; set; }

	/// <inheritdoc cref="BalloonLastX"/>
	public float BalloonLastY { get; set; }

	/// <summary>
	/// A balloon let go since <see cref="TakeLetGo"/> was last asked, bursting where it was: the guest no longer
	/// holds it, and whoever draws the park lets it finish.
	/// </summary>
	public Balloon? LetGo { get; private set; }

	/// <summary>Hands over the balloon let go, if any, and forgets it.</summary>
	public Balloon? TakeLetGo()
	{
		var letGo = LetGo;
		LetGo = null;

		return letGo;
	}

	/// <summary>
	/// Lets go of the balloon held, if any - <c>FUN_004fe950</c>: its sprite is put on the let-go script and the
	/// guest holds none; the life is left as it was.
	/// </summary>
	public void LetGoOfTheBalloon()
	{
		if ( Balloon is not { } held )
			return;

		held.LetGo();

		LetGo = held;
		Balloon = null;
	}

	public float Thirst { get; set; }

	public float Hunger { get; set; }

	public float Toilet { get; set; }

	/// <summary>
	/// How close this guest is to being sick - <c>PeepInfo.VomitCapacity</c> is the level it is measured
	/// against, and a visit raises it by the thing's excitement over <c>PeepInfo.RideVomitDivisor</c> times how
	/// little hungry they are, so a guest who has just eaten is the sickest (<see cref="ParkRideOperation"/>'s
	/// excitement match).
	/// </summary>
	/// <remarks>
	/// <b><c>mVomit</c> is this project's name for it</b>, as all seven need names are (see the note beside
	/// <c>ParkWorld.ReadGuest</c>); the save tags the need floats with the key <c>pv</c>, and the game's own
	/// log calls this one "illness", as the balance file does in <c>RegionFX[i].Illness</c> and
	/// <c>DecisionVarIllnessWeight</c>.
	/// </remarks>
	public float Vomit { get; set; }

	public float Litter { get; set; }

	public int MajorDest { get; set; }

	/// <summary>
	/// The thing this guest was bound for when the walk's minor decision turned them to a nearer one -
	/// <c>mSavedMajorDest</c>, <c>+0x1de</c>; nought for none. The switch writes it whatever it held
	/// (<c>0x004fd934</c>), the walk off the nearer thing sends them on to it and clears it (<c>0x0050092a</c>),
	/// and a removed thing clears it where it names that thing (<c>0x004fb4b3</c>); nothing else does, so a
	/// diversion given up leaves it for the guest's next walk off anything (<c>docs/exe/ride-operation.md</c>,
	/// "A second toilet").
	/// </summary>
	public int SavedMajorDest { get; set; }

	/// <summary>
	/// Turns of walking to a chosen thing, counted across walks and never reset between them - <c>mCount</c>,
	/// the person base's byte <c>+0x2c</c>. <see cref="PeepBehaviour"/> adds one on every such turn with the park
	/// open and runs the minor decision on the twelfth, zeroing it first (<c>0x004ffef2</c>..<c>0x004fff06</c>).
	/// A byte, so it wraps as the original's does.
	/// </summary>
	public byte WalkingTurns { get; set; }

	public int QueuePos { get; set; }

	private readonly int[] _previousRides = new int[ParkWorld.GuestState.Remembered];
	private readonly int[] _previousTemporaryRides = new int[ParkWorld.GuestState.Remembered];

	/// <summary>
	/// The last four things this guest left, newest first, nought where there is none - <c>mPreviousRides</c>,
	/// <c>+0x1e0</c>. <see cref="ParkRideScore"/> scores the same kind as the newest nought and divides the others
	/// down (<c>docs/exe/ride-operation.md</c>, "What a thing is worth to a guest").
	/// </summary>
	public IReadOnlyList<int> PreviousRides => _previousRides;

	/// <summary>
	/// The last four things this guest turned away from at the back of their queue, newest first -
	/// <c>mPreviousTemporaryRides</c>, <c>+0x1e8</c>, which <see cref="AgeRefusals"/> pushes noughts into.
	/// </summary>
	public IReadOnlyList<int> PreviousTemporaryRides => _previousTemporaryRides;

	/// <summary>
	/// How many sweeps apart a nought goes onto <see cref="PreviousTemporaryRides"/> - <c>FUN_004fdc90</c>'s
	/// <c>mGameTick % 20</c>.
	/// </summary>
	public const int RefusalsAgeEvery = 20;

	/// <summary>
	/// A thing this guest has left goes in front of <see cref="PreviousRides"/> and the three older move back -
	/// <c>FUN_004fd970</c>, <c>0x004fd98b</c>..<c>0x004fd9a5</c>, the settle-up's first act.
	/// </summary>
	public void RememberVisit( int thingId ) => Push( _previousRides, thingId );

	/// <summary>
	/// A thing this guest turned away from goes in front of <see cref="PreviousTemporaryRides"/> - <c>FUN_004fdc60</c>,
	/// at the arrival's two refusals (<c>0x004ffce6</c>, <c>0x004ffd74</c>).
	/// </summary>
	public void RememberRefusal( int thingId ) => Push( _previousTemporaryRides, thingId );

	/// <summary>
	/// A nought onto <see cref="PreviousTemporaryRides"/> on every sweep whose <c>mGameTick</c> divides by
	/// <see cref="RefusalsAgeEvery"/>, unsigned - <c>FUN_004fdc90</c>, the last call of the guest tick handler
	/// <c>FUN_00501650</c> (<c>0x005019da</c>), after its <c>(id &amp; 3)</c> needs block, so every sweep. A refusal is
	/// forgotten 61 to 80 sweeps after it is pushed.
	/// </summary>
	public void AgeRefusals( int gameTick )
	{
		if ( (uint)gameTick % RefusalsAgeEvery == 0 )
			Push( _previousTemporaryRides, 0 );
	}

	/// <summary>
	/// A removed thing leaves no visit behind - <c>FUN_004fb360</c>, <c>0x004fb4ba</c>..<c>0x004fb4d9</c>: each
	/// <see cref="PreviousRides"/> slot naming it is emptied, and the <see cref="PreviousTemporaryRides"/> slot at the
	/// same place with it, whatever that one holds. A refusal of the thing in any other slot stays.
	/// </summary>
	public void ForgetThing( int thingId )
	{
		for ( var i = 0; i < _previousRides.Length; ++i )
		{
			if ( _previousRides[i] != thingId )
				continue;

			_previousRides[i] = 0;
			_previousTemporaryRides[i] = 0;
		}
	}

	private static void Push( int[] history, int thingId )
	{
		for ( var i = history.Length - 1; i > 0; --i )
			history[i] = history[i - 1];

		history[0] = thingId;
	}

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
	/// The hurry, <c>mPurposeSpeed</c> at <c>+0xc2</c> (file 236): 0, 25 or 50, summed with <see cref="BaseSpeed"/> and
	/// <see cref="AdjustorSpeed"/> into the walking speed every sweep (<see cref="Pace"/>), and read by the walk to pick
	/// the hurried walk animation.
	///
	/// <para>
	/// Read from the save, 25 on arrival, and written by the needs tick, by entering a state, by a toilet and by
	/// <see cref="PeepBehaviour"/>, which is why the setter is <c>internal</c> rather than private:
	/// <c>FUN_004ff730</c> decides afresh every turn whether a guest hurries to the gate, before it walks them.
	/// Nothing outside the assembly can set it.
	/// </para>
	/// </summary>
	public int PurposeSpeed { get; internal set; }

	/// <summary>
	/// The guest's own walking speed in hundredths, one of <see cref="BaseSpeeds"/> - <c>mBaseSpeed</c>, the word at
	/// <c>+0xc0</c>, drawn as the person is made (<c>FUN_004f8940</c>) and never changed for a guest.
	/// </summary>
	public int BaseSpeed { get; internal set; }

	/// <summary>
	/// The sugar's hundredths on top of it - <c>mAdjustorSpeed</c>, the word at <c>+0xc4</c>: an Ice Cream Shop adds to
	/// it (<c>0x004fe60e</c>) and every sweep takes it to 99 hundredths of itself (<see cref="Pace"/>).
	/// </summary>
	public int AdjustorSpeed { get; internal set; }

	/// <summary>
	/// The speed the walk was last eased to, in cells over five a sweep - <c>mPreviousSpeed</c>, the float at <c>+0xc8</c>.
	/// </summary>
	public float PreviousSpeed { get; private set; }

	/// <summary>
	/// Whether <see cref="Pace"/> eases this guest's walking speed: every guest read from a save or made on arrival.
	/// A guest a test builds from a bare record keeps the navigator's speed as it was given.
	/// </summary>
	public bool Paced { get; }

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
	/// than assumed: once the gate will admit, <c>FUN_004ff7f0</c> tests it and nothing else to choose
	/// between sending a guest back to the ticket booths and letting them through the gate, so a guest saved
	/// mid-wait must be restored
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

	/// <summary>
	/// The shared counter's value when a wander last met a dead end, or nought - <c>mStrandedTime</c>,
	/// <c>+0x198</c>. While it is set and no block stamp around the guest is as new, every route and wander is
	/// refused (<see cref="PeepBehaviour.RefusedAsStranded"/>) and a red square blinks under them; a walk tick and
	/// any route asked for zero it (<c>docs/exe/ride-operation.md</c>, "The stranded bookkeeping").
	/// </summary>
	public uint StrandedTime { get; internal set; }

	/// <summary>What they last thought, and the bubble over them.</summary>
	public Thoughts Thoughts { get; } = new();

	/// <summary>
	/// Whether SetRandomDest has ever routed this guest - <c>mSetDestSuccessfully</c>, <c>+0xd0</c>, which only
	/// the constructor and a load clear. Entering <see cref="PeepState.Wandering"/> routes again to the stored
	/// destination while it is set (<c>docs/exe/ride-operation.md</c>, "The state-6 turn, in order").
	/// </summary>
	public bool SetDestSuccessfully { get; internal set; }

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

	public Peep( int thingId, ParkWorld.GuestState saved, ParkWorld.NavigatorState navigator, ParkWorld.PaceState? pace = null )
	{
		Navigator = new PeepNavigator( navigator );

		if ( pace is { } speeds )
		{
			Paced = true;
			AdjustorSpeed = speeds.AdjustorSpeed;
			BaseSpeed = speeds.BaseSpeed;
			PreviousSpeed = speeds.PreviousSpeed;
			PurposeSpeed = speeds.PurposeSpeed;
		}

		ThingId = thingId;
		PersonType = saved.PersonType;
		State = (PeepState)saved.State;
		SavedState = (PeepState)saved.SavedState;
		Cash = saved.Cash;
		ExitLevel = saved.ExitLevel;
		Happiness = saved.Happiness;
		PrankeryIndex = saved.PrankeryIndex;
		Thirst = saved.Thirst;
		Hunger = saved.Hunger;
		Toilet = saved.Toilet;
		Vomit = saved.Vomit;
		Litter = saved.Litter;
		MajorDest = saved.MajorDest;
		SavedMajorDest = saved.SavedMajorDest;
		WalkingTurns = (byte)saved.WalkingTurns;
		QueuePos = saved.QueuePos;
		QueueMoveDelay = saved.QueueMoveDelay;
		NumRides = saved.NumRides;
		NumShops = saved.NumShops;
		NumSideshows = saved.NumSideshows;
		NumSideshowsWon = saved.NumSideshowsWon;

		StrandedTime = saved.StrandedTime;
		Thoughts.Restore( saved.LastThought, saved.TimeBubbleShown );

		// A bubble showing when the park was saved is a slot of the sprite table (mThoughtScript); it is not made again.
		if ( saved.ThoughtScript != 0 )
			Unimplemented.Report( "SAVED_THOUGHT_BUBBLE" );

		// The balloon's life and its next place; its sprite is the table's, which ParkPeople joins by the slot.
		BalloonLife = saved.RemainingBalloonLife;
		BalloonLastX = saved.LastPosX;
		BalloonLastY = saved.LastPosY;

		saved.PreviousRides?.Take( _previousRides.Length ).ToArray().CopyTo( _previousRides, 0 );
		saved.PreviousTemporaryRides?.Take( _previousTemporaryRides.Length ).ToArray().CopyTo( _previousTemporaryRides, 0 );

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
	/// reads <c>mGameTick</c>, one a sweep, for this test rather than the loop tick the engine is gated on
	/// (this is handed <c>GameClock.Ticks</c> over eight instead, Q132), and the distinction is
	/// load-bearing: four divides eight, so a share taken over game ticks would be true only for guests
	/// whose id is a multiple of four, and the other three quarters would never age at all.
	/// </para>
	///
	/// <para>
	/// <b>This gates the needs and nothing else.</b> A guest is ticked by
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
	/// <b>Ten of the eleven agree with a list written from the other direction.</b> <see cref="AnimationFor"/>
	/// queues the walking animation for exactly ten states, and they are these without
	/// <see cref="PeepState.LeavingRide"/>, and there the difference is this build's: the original's setter
	/// asks for the walk on the way into it too - see <see cref="AnimationFor"/>.
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

	/// <summary>The hurry of a guest heading for the gate while the bus leaves (<see cref="PeepBehaviour.GateHurry"/>).</summary>
	public const int RunningForTheBusSpeed = 50;

	/// <summary>
	/// The five base speeds, in hundredths - the words at <c>0x0075c7f8</c>. A guest is made with one of them drawn
	/// modulo five (<c>docs/exe/ride-operation.md</c>, the person <c>+0xc0</c> row).
	/// </summary>
	public static readonly int[] BaseSpeeds = [60, 80, 100, 120, 140];

	/// <summary>What the three speed words are summed over - the word at <c>0x0075c7fc</c>, <see cref="BaseSpeeds"/>' middle one.</summary>
	public const int SpeedDivisor = 100;

	/// <summary>The most speed the mover takes (the float at <c>0x007009a0</c>).</summary>
	public const float MostSpeed = 2f;

	/// <summary>A speed of one in the mover's <c>max_speed</c>: a fifth of a cell a sweep (the double at <c>0x007009b0</c>).</summary>
	public const double MaxSpeedPerSpeed = 13107.2;

	/// <summary>A speed of one in the mover's <c>max_force</c> (the double at <c>0x007009a8</c>).</summary>
	public const double MaxForcePerSpeed = 26214.4;

	/// <summary>The least either may be, a hundredth of a cell (<c>0x28f</c> at <c>0x005101d0</c>).</summary>
	public const int LeastMaxSpeed = 655;

	/// <summary>
	/// Eases this guest's walking speed and hands it to the walk - the first half of <c>FUN_004fa870</c>, the first
	/// call of the guest's turn every sweep, whatever they are doing (<c>docs/exe/ride-operation.md</c>, "Where a
	/// WALKING peep is drawn").
	///
	/// <para>
	/// The three words are summed unsigned and over <see cref="SpeedDivisor"/>, and the speed moves a quarter of the way
	/// there: <c>(sum / 100 - previous × -3) × 0.25</c>, worked in single precision. Then the mover's own setter holds
	/// it to <see cref="MostSpeed"/> and writes the mover's force and speed, each truncated and at least
	/// <see cref="LeastMaxSpeed"/>; and the sugar's word falls to 99 hundredths of itself, taken as a word, which is one
	/// a sweep below a hundred.
	/// </para>
	/// <para>
	/// A member of staff is not eased here, and keeps the speed the save gave them: their base follows their rest,
	/// which is unbuilt (Q136).
	/// </para>
	/// </summary>
	public void Pace()
	{
		if ( !Paced )
			return;

		var sum = (PurposeSpeed & 0xffff) + (BaseSpeed & 0xffff) + (AdjustorSpeed & 0xffff);

		PreviousSpeed = ((float)sum / SpeedDivisor - PreviousSpeed * -3f) * 0.25f;

		var speed = PreviousSpeed > MostSpeed ? MostSpeed : PreviousSpeed;

		// __ftol: truncated through a 64-bit integer, its low dword kept.
		Navigator.MaxForce = Math.Max( LeastMaxSpeed, unchecked((int)(long)(speed * MaxForcePerSpeed)) );
		Navigator.MaxSpeed = Math.Max( LeastMaxSpeed, unchecked((int)(long)(speed * MaxSpeedPerSpeed)) );

		if ( AdjustorSpeed != 0 )
			Log.Info( $"Person {ThingId}: pace {sum} eased to {PreviousSpeed}, speed {Navigator.MaxSpeed}, adjustor {AdjustorSpeed}" );

		AdjustorSpeed = ((AdjustorSpeed * 99) & 0xffff) / 100;
	}

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
	/// chosen at each call site in the executable and is read only for a toilet's and the fireworks'
	/// (<c>ride-operation.md</c>, "A toilet's dirt"), so modelling it now would mean inventing the rest. Nothing here reads a cell, and nothing pretends to.
	/// </para>
	/// <para>
	/// <b>A quirk worth not tidying away.</b> The drift is gated on the same counter as the whole tick,
	/// and sixteen is a multiple of four - so <c>tick &amp; 15 == 0</c> can only happen on a tick where
	/// <c>tick &amp; 3 == 0</c>, and therefore only guests whose id is a multiple of four ever get
	/// hungrier, thirstier or more desperate on their own. The other three quarters of the park only
	/// change through what happens to them. That is what the original does, so it is what this does.
	/// </para>
	/// </summary>
	/// <param name="onACountingCell">
	/// Whether the cell they stand on passes <c>FUN_004fa990</c>, as <see cref="CountsOn"/> answers of its type. False
	/// by default, which leaves the balloon's countdown alone for a caller asking about the needs.
	/// </param>
	/// <param name="think">
	/// The block's draw of the park's generator and, one time in ten, the thought the needs pick
	/// (<see cref="ParkPeople"/> hands it in, with the generator and the park's clock).
	/// </param>
	public void Tick( int tick, bool onACountingCell = false, Action<Peep>? think = null )
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

		// The block's last test (0x005018f8..0x00501949): off a ride and on a counting cell, a draw of the park's
		// generator, one in ten of which picks a thought (FUN_004fc8a0, the caller's), and then the balloon's life goes down by one,
		// unsigned, letting it go at nought. It reads the life alone, so a guest whose balloon is not showing still
		// counts down.
		if ( State is PeepState.Riding or PeepState.Leaving || !onACountingCell )
			return;

		think?.Invoke( this );

		if ( BalloonLife != 0 && --BalloonLife == 0 )
			LetGoOfTheBalloon();
	}

	/// <summary>
	/// Whether a thing standing on a cell of this type is counted - <c>FUN_004fa990</c>, reading the runtime cell's
	/// <c>mType</c> under the thing's own cell bytes: a cleared cell (0), a path (1), a queue (3), a ride's end (9)
	/// or its far end (10) (<c>FUN_00536310</c> and its neighbours).
	/// </summary>
	public static bool CountsOn( int cellType ) => cellType is 0 or 1 or 3 or 9 or 10;

	/// <summary>
	/// Enters a state: the self-contained half of the original's <c>FUN_00501db0</c>.
	///
	/// <para>
	/// That function writes the new state, queues an animation for it, and then does whatever entering
	/// it calls for. The animation and the effects below are everything it does that depends on the
	/// guest alone. Case <c>0xe</c>'s roll is
	/// <c>PeepBehaviour.RollForTheVisit</c>'s, made straight after this; boarding's putting a balloon away is here,
	/// and leaving's building it again is <see cref="ParkRideOperation.Dismiss"/>'s, which knows the thing left. The
	/// rest - firing the events a ride raises, paying at the bus stop - reaches into a ride or the event ring, and is
	/// not built.
	/// </para>
	/// </summary>
	public void SetState( PeepState next, int tick, Random random )
	{
		State = next;
		Animation = AnimationFor( next );

		// <b>And QUEUE it, as the original does.</b> FUN_00501db0 does not merely record the animation a
		// state wants - it calls FUN_004217f0, which decompiles to a bare `*(person + 4) = value`, the
		// person's own mNextAnim, and that is what ParkPeople.Apply hands the sprite.
		//
		// Of the four AnimationFor answers None for, only PlayingSpotAnimation and Leaving never call
		// FUN_004217f0 in the original: its case 0xf asks for the walk, and its case 0x10 for the stand on
		// a thing flagged 0x20 - see AnimationFor.
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
			case PeepState.InQueue:
				TimeStartedIdling = tick;
				QueueMoveDelay = (int)(QueuePos * QueueDelayPerPlace);
				break;

			// Shuffling up a queue is never done in a hurry, whatever the guest's needs say.
			case PeepState.SteppingUpQueue:
				PurposeSpeed = UnhurriedSpeed;
				break;

			// Boarding anything deletes the balloon's sprite, with no burst, and keeps its life (0x00502156..
			// 0x00502169); leaving the thing builds it again (ParkRideOperation.Dismiss).
			case PeepState.Riding:
				Balloon = null;
				break;

			// Both of these give up on wherever they were going, and entering the first lets go of a balloon
			// (0x005022ef). A guard's catch, which enters it, has deleted the sprite first, and so has the bus
			// stop's leaver, whom the original deletes without entering it at all (PeepBehaviour, AtTheBusStop).
			case PeepState.Leaving:
				MajorDest = 0;
				LetGoOfTheBalloon();
				break;

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
	/// Two of the four that queue nothing are the original's: <see cref="PeepState.PlayingSpotAnimation"/> is
	/// already playing one, and <see cref="PeepState.Leaving"/> queues none at all. The other two are this
	/// build's. The original's setter asks for the walk for <see cref="PeepState.LeavingRide"/>, and for
	/// <see cref="PeepState.Riding"/> the stand on a thing flagged <c>0x20</c> and no sprite on any other
	/// (<c>FUN_00501db0</c> cases <c>0xf</c> and <c>0x10</c>; the hidden rider is Q52).
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
