namespace OpenTPW;

/// <summary>
/// The twenty-two things a guest can be doing. The numbers are the save's own and are written into
/// every park file, so they are fixed rather than ours to choose.
///
/// <para>
/// The names come from what each state's code does and from the lines the game prints while in it - the
/// bus arriving, the fee being judged, being stuck outside the park. <see cref="Deciding"/> is the one
/// that matters most: a guest is constructed in it, returns to it whenever they finish anything, and it
/// is where the original picks what to do next.
/// </para>
/// </summary>
public enum PeepState
{
	/// <summary>Walking to somewhere chosen; on arrival becomes <see cref="AtGate"/>.</summary>
	Walking = 0,

	AtGate = 1,

	/// <summary>Heading for the park gate - the state that prints "The bus is coming! RUUUN!".</summary>
	HeadingForGate = 2,

	WaitingForOpening = 3,

	/// <summary>Judging the admission fee, which is where the four opinions about the price are printed.</summary>
	JudgingTheFee = 4,

	Entering = 5,

	/// <summary>
	/// Choosing what to do next, and what a new guest is made in. Everything that finishes comes back
	/// here.
	/// </summary>
	Deciding = 6,

	Wandering = 7,

	/// <summary>Playing a one-off animation, after which the guest returns to <c>SavedState</c>.</summary>
	PlayingSpotAnimation = 8,

	GoingToMinorDestination = 9,

	GoingToRide = 10,

	InQueue = 11,

	SteppingUpQueue = 12,

	BeingAdmitted = 13,

	EnteringRide = 14,

	/// <summary>
	/// Walking away from a ride that has just let them off - <b>not</b> being on one.
	///
	/// <para>
	/// The original enters it
	/// at the END of a go: <c>FUN_005014e0</c> ("Person %d: ExitRide, leaving rid...") asks the thing for
	/// its exit point, sets a destination, and only then calls the state setter with <b>15</b>. Sixteen is
	/// what a guest is put into when they are admitted, and that one really is riding.
	/// </para>
	/// <para>
	/// <see cref="ParkRideOperation.Dismiss"/> sets this on letting somebody off, and
	/// <see cref="Peep.IsAWalkingState"/> counts it as walking, which is right precisely because they are
	/// walking to the exit.
	/// </para>
	/// </summary>
	LeavingRide = 15,

	/// <summary>Actually on the ride - entered on admission, and the state a guest is dismissed from.</summary>
	Riding = 16,

	/// <summary>Leaving the park for good - the guest is deleted unless a guard still wants them.</summary>
	Leaving = 17,

	/// <summary>Heading for the exit, which prints "I'm stuck in the park, even though it's closed!!".</summary>
	HeadingForExit = 18,

	PickingACellOutside = 19,

	/// <summary>Walking about outside the park; getting stuck here prints so.</summary>
	WalkingOutside = 20,

	AtTheBusStop = 21
}

/// <summary>
/// Which animation a state asks for as it is entered. The original queues these by number through one
/// call - 1 is the walk, 2 the hurried walk and 3 the stand - and several states reach the stand only by
/// falling through to the call at the end of the setter rather than by asking for it.
/// </summary>
public enum PeepAnimation
{
	/// <summary>The state queues nothing, and the guest keeps whatever they were already playing.</summary>
	None = 0,

	/// <summary>Set 1, eight pictures - <see cref="SpriteScript.Walking"/>.</summary>
	Walk = 1,

	/// <summary>
	/// Set 2, eight pictures. Queued instead of <see cref="Walk"/> for a guest who is hurrying, which the
	/// walk decides from their purpose speed.
	/// </summary>
	HurriedWalk = 2,

	/// <summary>Set 0, one picture - <see cref="SpriteScript.Standing"/>.</summary>
	Stand = 3
}
