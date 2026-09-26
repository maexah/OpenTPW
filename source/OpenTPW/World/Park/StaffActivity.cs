namespace OpenTPW;

/// <summary>
/// What a member of staff is doing. The numbers are the save's own - they are what
/// <c>ParkWorld.StaffState.State</c> holds and what every staff behaviour switches on - so they are fixed
/// rather than ours to choose.
///
/// <para>
/// <b>Eight of these are shared by all five kinds of staff, and that is the finding that makes staff
/// tractable.</b> The original gives each kind its own per-turn function, and all five open with the same
/// switch answering 2 to 7 through the same three handlers (<c>FUN_00505fe0</c>, <c>FUN_005061d0</c> and
/// <c>FUN_005056e0</c>, whose own diagnostics name them <c>CStaff::</c>) and 0 and 1 in arms of their own
/// (<c>docs/exe/ride-operation.md</c>, "The staff turn"). Only the numbers above these
/// differ, one set per kind. So the five are one machine with five small extensions rather than five
/// state machines, which is what "five kinds of staff" reads like until the functions are measured.
/// </para>
/// <para>
/// <b>This is deliberately not called <c>StaffState</c></b>, which would be the name symmetric with
/// <see cref="PeepState"/>. <c>ParkWorld.StaffState</c> is the saved block, and a top-level type of the
/// same name would resolve to the nested one inside <c>ParkWorld</c> and to this one everywhere else -
/// compiling perfectly while meaning two different things in two files.
/// </para>
/// </summary>
public enum StaffActivity
{
	/// <summary>
	/// Standing about with nothing to do. They wait out their grade's <c>IdleDuration</c> from
	/// <c>mTimeStartedIdling</c> and then look for something. Every kind asks
	/// <c>FUN_00506a40</c> first (strike, tired, mood); here only the guard and the researcher ask, and only
	/// whether they are too tired (Q133).
	/// </summary>
	Idle = 0,

	/// <summary>
	/// Walking somewhere. Every kind drains happiness and rest while they walk, faster at a lower grade -
	/// the original's <c>(6 - grade)</c> multiplier.
	/// </summary>
	Walking = 1,

	/// <summary>
	/// On the way to a rest area, having decided they are too tired to work. Arriving puts them in
	/// <see cref="Resting"/>; failing to get there costs them
	/// <c>AllStaffConstants.HappyHitCosNoRestArea</c> and drops them back to <see cref="Idle"/>.
	/// </summary>
	GoingToRest = 2,

	/// <summary>
	/// Sitting in a rest area recovering, by their grade's <c>RecuperationRate</c> and
	/// <c>HappinessRecuperationRate</c>, until their rest - <see cref="Staff.Tiredness"/> - reaches a hundred.
	/// </summary>
	Resting = 3,

	/// <summary>
	/// Walking to the strike area - <c>FixedItemInfo.StrikeArea*</c>, a six-by-one strip at (40,9). Reached
	/// from the strike arm every decide opens with (<c>FUN_00506a40</c>), when <c>mStaffHQ</c>'s flag for
	/// the kind is set and the gate's <c>VAR_STATUS</c> reads 1: "Right! I'm fed up, I'm going on strike!".
	/// </summary>
	GoingOnStrike = 4,

	/// <summary>
	/// On strike, milling about the strike area and turning on the spot until the dispute ends, at which
	/// point they walk back to the park entrance.
	/// </summary>
	OnStrike = 5,

	/// <summary>
	/// Waiting out a spell three times as long as an ordinary idle before going back to
	/// <see cref="Idle"/>. Nothing in the original enters it; only a saved <c>mState</c> can.
	/// </summary>
	Waiting = 6,

	/// <summary>
	/// Doing nothing and not being asked again - the shared switch's case 7 has an empty body. A staff
	/// member being carried by the player is put here.
	/// </summary>
	Held = 7
}
