using System;

namespace OpenTPW;

/// <summary>
/// One turn of what a member of staff is doing - the shared half of the original's five per-kind
/// behaviours, which turns out to be nearly all of them.
///
/// <para>
/// <b>The finding this class rests on.</b> Each kind of staff has a per-turn function of its own
/// (<c>FUN_004da490</c> mechanic, <c>FUN_004d73c0</c> handyman, <c>FUN_004d4810</c> entertainer,
/// <c>FUN_004d6410</c> guard, <c>FUN_005029f0</c> researcher) and all five open with the same switch on
/// <c>mState</c>, answering states 2 to 7 through the same three handlers - whose own diagnostics call
/// them <c>CStaff::</c> - and 0 and 1 in arms of their own. So this is one machine with five extensions,
/// not five machines, and the shared part moves every kind.
/// </para>
/// <para>
/// <b>What is built.</b> Every shared state but on strike (5, Q138), and every kind's decide with no work
/// found: the mechanic and the handyman set off on a random walk every time, the guard and the entertainer on
/// three sweeps in four by the park's clock, <c>mGameTick &amp; 3</c>, and the researcher on three decides in
/// four by a draw.
/// </para>
/// <para>
/// <b>The handyman's toilet job is built</b>: the search (<c>FUN_004d7880</c>), the walk to the toilet
/// (state <c>0xa</c>) and the clean (state <c>0xb</c>, <c>FUN_004dfd80</c>) - <see cref="FindToilet"/>,
/// <see cref="ArriveAtTheLoo"/>, <see cref="CleanOn"/>.
/// </para>
/// <para>
/// <b>The entertainer's performance is built</b>: the look for a guest in reach (<c>FUN_004c8eb0</c>), the
/// bank's own animation and state <c>0xe</c> - <see cref="Perform"/>.
/// </para>
/// <para>
/// <b>What is deliberately not built, each for a named reason.</b> The rest of the work: a mechanic's broken
/// ride (<c>FUN_004daa90</c>) and a handyman's litter (<c>FUN_004c8ed0</c>). Each search is counted where the
/// original makes it and answers as finding nothing.
/// The strike arms are absent too (Q138): reaching them means asking <c>mStaffHQ</c>'s own flag for the
/// kind, which nothing here keeps, and the gate's status, which <c>ParkRides.GateStatus</c> answers.
/// </para>
/// <para>
/// <b>Rest areas are built.</b> The flag naming one is read - bit 1
/// of a catalogue object's <c>mFlags</c> - so a tired staff member does what
/// <c>FUN_00506a40</c> does: looks for the nearest object flagged as a rest area and walks to it. The
/// "couldn't find a rest area" path is still here, because the original still takes it when there is none
/// in reach.
/// </para>
/// </summary>
public sealed class StaffBehaviour
{
	private readonly Random _random;

	/// <summary>
	/// The live cells used to choose wandering destinations, and the objects used to find rest areas.
	/// Null leaves cell filtering and rest-area lookup unavailable to isolated behaviour fixtures.
	/// </summary>
	private readonly ParkState? _state;

	/// <summary>A thing's own ride script, for the clean's <c>VAR_WORN</c> and the open that follows it.</summary>
	public Func<int, RideScript?>? ScriptFor { get; init; }

	/// <summary>
	/// The member of staff with a thing id, which a toilet's forgetting asks of whoever it is assigned to
	/// (<c>FUN_004e0220</c>).
	/// </summary>
	public Func<int, Staff?>? StaffById { get; init; }

	/// <summary>
	/// How many guests stand on the cells from a reach before to a reach after a packed cell each way -
	/// <c>FUN_004c8d30</c> for kind 1 (<see cref="ParkPeople.GuestsNear"/>). Null finds nobody.
	/// </summary>
	public Func<int, int, int>? GuestsNear { get; init; }

	/// <summary>
	/// How many of a member's sprite bank's four state groups are in use - <c>FUN_00541fa0</c> on the sprite's
	/// kind and bank - or nought when the bank is not known.
	/// </summary>
	public Func<Staff, int>? StateGroupsOf { get; init; }

	private readonly int[] _entertainerWork = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _entertainerReach = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _idleDuration = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _handymanWork = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _handymanRange = new int[ParkWorld.StaffState.PayGrades];
	private readonly float[] _recuperation = new float[ParkWorld.StaffState.PayGrades];
	private readonly float[] _happinessRecuperation = new float[ParkWorld.StaffState.PayGrades];

	/// <param name="balance">
	/// The park's balance stack, which is where every staff constant lives. Null leaves the fallbacks in
	/// place - the global file's own values - so that a test can drive this without mounting a game.
	/// </param>
	/// <param name="random">The rolls this makes. Taken so a test can seed them; the game does not.</param>
	/// <param name="state">
	/// The park these staff are in, for wandering cell filters and finding a rest area.
	/// </param>
	public StaffBehaviour( ParkBalance? balance = null, Random? random = null, ParkState? state = null )
	{
		_random = random ?? new Random();
		_state = state;

		// The fallbacks are the shipped global file's own numbers rather than zeros: a missing key should
		// leave the simulation running, and a zero idle duration would have every staff member decide
		// again on every single turn.
		RestLevel = balance?.Int( "AllStaffConstants.RestLevel", 1 ) ?? 1;
		HappinessHitForNoRestArea = balance?.Int( "AllStaffConstants.HappyHitCosNoRestArea", 2 ) ?? 2;

		int[] idleFallback = [40, 30, 20, 10, 5];
		float[] restFallback = [0.2f, 0.3f, 0.4f, 0.5f, 0.75f];
		float[] moodFallback = [1f, 2f, 2f, 3f, 3f];
		int[] workFallback = [40, 30, 20, 10, 5];
		int[] rangeFallback = [2, 3, 3, 4, 5];
		int[] performFallback = [10, 20, 30, 50, 75];
		int[] reachFallback = [3, 3, 4, 4, 5];

		for ( var grade = 0; grade < ParkWorld.StaffState.PayGrades; ++grade )
		{
			var key = $"PerGradeStaffConsts[{grade}]";

			_idleDuration[grade] = balance?.Int( $"{key}.IdleDuration", idleFallback[grade] )
				?? idleFallback[grade];
			_recuperation[grade] = balance?.Float( $"{key}.RecuperationRate", restFallback[grade] )
				?? restFallback[grade];
			_happinessRecuperation[grade] =
				balance?.Float( $"{key}.HappinessRecuperationRate", moodFallback[grade] )
				?? moodFallback[grade];

			var handyman = $"HandymanConstsPerGrade[{grade}]";

			_handymanWork[grade] = balance?.Int( $"{handyman}.WorkDuration", workFallback[grade] )
				?? workFallback[grade];
			_handymanRange[grade] = balance?.Int( $"{handyman}.DetectionRange", rangeFallback[grade] )
				?? rangeFallback[grade];

			var entertainer = $"EntertainerConstsPerGrade[{grade}]";

			_entertainerWork[grade] = balance?.Int( $"{entertainer}.WorkDuration", performFallback[grade] )
				?? performFallback[grade];
			_entertainerReach[grade] = balance?.Int( $"{entertainer}.ActivationDistance", reachFallback[grade] )
				?? reachFallback[grade];
		}
	}

	/// <summary>How rested a staff member has to be to keep working - <c>AllStaffConstants.RestLevel</c>, 1.</summary>
	public int RestLevel { get; }

	/// <summary>
	/// What failing to reach a rest area costs them -
	/// <c>AllStaffConstants.HappyHitCosNoRestArea</c>, 2.
	/// </summary>
	public int HappinessHitForNoRestArea { get; }

	/// <summary>How long a staff member of this grade stands about before looking for something to do.</summary>
	public int IdleDurationAt( int grade ) => _idleDuration[Math.Clamp( grade, 0, _idleDuration.Length - 1 )];

	/// <summary>How much rest a staff member of this grade recovers per turn sitting in a rest area.</summary>
	public float RecuperationAt( int grade ) => _recuperation[Math.Clamp( grade, 0, _recuperation.Length - 1 )];

	/// <inheritdoc cref="RecuperationAt"/>
	public float HappinessRecuperationAt( int grade )
		=> _happinessRecuperation[Math.Clamp( grade, 0, _happinessRecuperation.Length - 1 )];

	/// <summary>
	/// How long a handyman of this grade cleans - <c>HandymanConstsPerGrade.WorkDuration</c>, the table at
	/// <c>0x007853ec</c>: 40, 30, 20, 10, 5.
	/// </summary>
	public int HandymanWorkDurationAt( int grade )
		=> _handymanWork[Math.Clamp( grade, 0, _handymanWork.Length - 1 )];

	/// <summary>
	/// How many cells off a handyman of this grade notices a dirty toilet -
	/// <c>HandymanConstsPerGrade.DetectionRange</c>, the table at <c>0x007853f0</c>: 2, 3, 3, 4, 5.
	/// </summary>
	public int HandymanDetectionRangeAt( int grade )
		=> _handymanRange[Math.Clamp( grade, 0, _handymanRange.Length - 1 )];

	/// <summary>
	/// How long an entertainer of this grade performs - <c>EntertainerConstsPerGrade.WorkDuration</c>, the table
	/// at <c>0x00785398</c>: 10, 20, 30, 50, 75.
	/// </summary>
	public int EntertainerWorkDurationAt( int grade )
		=> _entertainerWork[Math.Clamp( grade, 0, _entertainerWork.Length - 1 )];

	/// <summary>
	/// How many cells each way an entertainer of this grade looks for a guest -
	/// <c>EntertainerConstsPerGrade.ActivationDistance</c>, the table at <c>0x007853a0</c>: 3, 3, 4, 4, 5.
	/// </summary>
	public int EntertainerActivationDistanceAt( int grade )
		=> _entertainerReach[Math.Clamp( grade, 0, _entertainerReach.Length - 1 )];

	/// <summary>How much rest one turn of work costs, before the grade multiplier - the float at <c>0x00700858</c>.</summary>
	public const float TirednessPerWorkingTurn = 0.025f;

	/// <summary>The same for mood - the double at <c>0x00700860</c>.</summary>
	public const float HappinessPerWorkingTurn = 0.01f;

	/// <summary>
	/// How many sweeps a thing keeps the member assigned to it once that member aims elsewhere - the 100 of
	/// <c>FUN_004e0220</c>.
	/// </summary>
	public const int AssignmentKept = 100;

	/// <summary>
	/// How much rest one turn of walking costs, before the grade multiplier - the double at
	/// <c>0x700848</c>, read out of the executable because it is a code constant rather than a balance key.
	/// </summary>
	public const float TirednessPerWalkingTurn = 0.012f;

	/// <summary>The same for mood - the double at <c>0x700850</c>.</summary>
	public const float HappinessPerWalkingTurn = 0.005f;

	/// <summary>
	/// What the pay grade is subtracted from to scale both drains: <c>(6 - grade)</c>, so a grade 4 staff
	/// member tires at a third of the rate of a grade 0 one. Six rather than five, so that even the best
	/// grade still tires.
	/// </summary>
	public const int TiringBase = 6;

	/// <summary>The happiness byte at or below which a deciding member thinks <c>0x13</c> - <c>0x00506cc2</c>.</summary>
	private const int UnhappyThoughtAtMost = 10;

	/// <summary>The happiness byte above which a deciding member may think <c>0x12</c> - <c>0x00506cda</c>.</summary>
	private const int VeryHappyThoughtAbove = 0x61;

	/// <summary>How many times the patrol roll tries for a cell before giving up - <c>FUN_00506f30</c>.</summary>
	public const int PatrolTries = 30;

	/// <summary>
	/// A guard or researcher stays put when the low two bits of its choice are nought, and otherwise looks for
	/// somewhere to walk. The guard's are <c>mGameTick</c>'s (<c>0x004d655d</c>), so it stays on one sweep in four
	/// by the clock; the researcher's are a draw's (<c>0x00502ba9</c>), and where this one stays the original's
	/// researches (Q134).
	/// </summary>
	public const int StayPutShare = 4;

	/// <summary>How many idle durations the waiting state waits - three.</summary>
	public const int WaitingIsIdleTimes = 3;

	/// <summary>One turn of one staff member's behaviour.</summary>
	/// <param name="tick">
	/// The park's <c>mGameTick</c>, <see cref="ParkState.GameTick"/>, already one up for this sweep. Every stamp a
	/// member of staff writes is a reading of it, and a saved stamp is a reading of the saved one, compared raw:
	/// nothing zeroes or rebases it (<c>docs/exe/ride-operation.md</c>, "The staff turn").
	/// </param>
	public void Step( Staff staff, PeepWalk walk, SpriteScript? playing, int tick )
	{
		ArgumentNullException.ThrowIfNull( staff );
		ArgumentNullException.ThrowIfNull( walk );

		// An idle handyman's pre-step looks for litter in range on every sweep the clock divides by his grade's
		// idle duration, and decides at once when it finds some (FUN_004d7060, 0x004d7087). Not built.
		if ( staff.Model == HandymanModel && staff.Activity == StaffActivity.Idle
			&& (uint)tick % (uint)IdleDurationAt( staff.PayGrade ) == 0 )
			Unimplemented.Report( "HANDYMAN_LITTER_SEARCH" );

		switch ( staff.Activity )
		{
			// Standing about. They wait out their grade's idle duration and then look for something to do: the
			// first sweep past stamp + IdleDuration, so IdleDuration + 1 sweeps after a walk's stamp (0x004d6545).
			case StaffActivity.Idle:
				if ( (uint)tick <= (uint)(staff.TimeStartedIdling + IdleDurationAt( staff.PayGrade )) )
					break;

				Decide( staff, walk, tick );

				break;

			// Walking somewhere. Still going costs them rest and mood; arriving or giving up both end in
			// the same decision, which is the original's own shape - it falls out of the switch either way.
			case StaffActivity.Walking:
				if ( Walked( staff, walk, playing ) == WalkVerdict.Walking )
				{
					Tire( staff );

					break;
				}

				Decide( staff, walk, tick );

				break;

			// On the way to sit down. Arriving starts the rest; failing to get there costs them the mood
			// the balance file names for exactly this and puts them back to standing about.
			case StaffActivity.GoingToRest:
				switch ( Walked( staff, walk, playing ) )
				{
					// Arriving adds one to the rest area's VAR_STAFFIN and tells the resting-staff list
					// (FUN_00505fe0); neither is built.
					case WalkVerdict.Arrived:
						Unimplemented.Report( "REST_AREA_OCCUPANCY" );
						staff.SetActivity( StaffActivity.Resting, tick );
						break;

					case WalkVerdict.CannotReach:
						staff.Happiness = Staff.Change( staff.Happiness, -HappinessHitForNoRestArea );
						staff.SetActivity( StaffActivity.Idle, tick );
						break;

					default:
						break;
				}

				break;

			// Sitting down recovering. Both stats climb by the grade's own rates until Tiredness is full.
			case StaffActivity.Resting:
				Rest( staff, tick );

				break;

			// Walking to the picket. Arriving and giving up are treated alike, as they are for a guest
			// shuffling up a queue: somebody who cannot reach the picket is still on strike.
			case StaffActivity.GoingOnStrike:
				if ( Walked( staff, walk, playing ) != WalkVerdict.Walking )
					staff.SetActivity( StaffActivity.OnStrike, tick );

				break;

			// Waiting out a spell three times as long as an ordinary idle, unsigned as the original's (0x00505745).
			case StaffActivity.Waiting:
				var waited = (uint)(tick - staff.TimeStartedIdling);

				if ( waited > (uint)(IdleDurationAt( staff.PayGrade ) * WaitingIsIdleTimes) )
					staff.SetActivity( StaffActivity.Idle, tick );

				break;

			// A handyman on the way to a toilet - FUN_004d7790. The walk costs no rest or mood here: the arm
			// has no FUN_005066a0.
			case StaffActivity.GoingToLoo:
				switch ( Walked( staff, walk, playing ) )
				{
					case WalkVerdict.Arrived:
						ArriveAtTheLoo( staff, walk, tick );
						break;

					case WalkVerdict.CannotReach:
						staff.SetActivity( StaffActivity.Idle, tick );
						break;

					default:
						break;
				}

				break;

			// A handyman cleaning - FUN_004d73c0's case 0xb: a turn of work, then the end on the first sweep
			// past stamp + WorkDuration (0x004d7462).
			case StaffActivity.Cleaning:
				Work( staff );

				if ( (uint)tick <= (uint)(staff.TimeStartedCleaning + HandymanWorkDurationAt( staff.PayGrade )) )
					break;

				CleanOn( staff, tick );

				break;

			// An entertainer performing - FUN_004d4810's case 0xe: a turn of work, then, on the first sweep past
			// stamp + WorkDuration, the end's sound and the decide again in the same turn, which may start another
			// performance on a fresh stamp.
			case StaffActivity.Performing:
				Work( staff );

				if ( (uint)tick <= (uint)(staff.TimeStartedEntertaining + EntertainerWorkDurationAt( staff.PayGrade )) )
					break;

				// cat_staff effect 0x87 at the sprite's position (0x004d48f0), not built (Q135).
				Unimplemented.Report( "STAFF_SOUND_PERFORMANCE_END" );
				Log.Info( $"Staff: {staff.ThingId} ends the performance of mGameTick {staff.TimeStartedEntertaining} "
					+ $"on mGameTick {tick}" );

				Decide( staff, walk, tick );

				break;

			// On strike and being carried both do nothing here. The original ends a strike in state 5's
			// FUN_00506300, which is not built (Q138), and its own case 7 has an empty body.
			default:
				break;
		}
	}

	/// <summary>
	/// What a staff member does when they have finished a walk or run out of idling - each kind's decide, which
	/// opens with the shared <c>FUN_00506a40</c> (strike, tired, mood) and then makes the kind's own choice
	/// (<c>docs/exe/ride-operation.md</c>, "Leaving idle, or a walk: the choice by kind").
	///
	/// <para>
	/// <b>Every kind walks about when it has no work.</b> The mechanic and the handyman look for work and, with
	/// none, set off on a random walk every time; the entertainer, the guard and the researcher stay on one
	/// choice in four. The handyman's toilet search and the entertainer's look find work; the others are counted
	/// where the original makes them.
	/// </para>
	/// </summary>
	private void Decide( Staff staff, PeepWalk walk, int tick )
	{
		ThinkOfTheMood( staff );

		// Too tired to carry on: find the nearest rest area and set off for it - the tired branch of
		// FUN_00506a40, which asks FUN_00506910 for the nearest object flagged as one.
		// <b>A deviation (Q136 (a)):</b> the original tests the rest byte, truncated, <= RestLevel (0x00506b41);
		// this tests the float <, so a rest in [1, 2) is tired there and not here.
		//
		// FAILING TO FIND ONE AND FAILING TO REACH IT ARE THE SAME PATH IN THE ORIGINAL, and that is worth
		// not tidying into two: both fall through to the same "Staff member couldn't find a rest area"
		// line and the same one-in-sixteen loss of heart. GoAndRest returning false covers both.
		//
		// A deviation Q136 (d) holds: after that the original answers 0 and the kind's own choice follows,
		// so the member walks on; here they stand idle. There the three kinds with work to find skip their
		// searches when the rest byte is under RestLevel (FUN_00506680); that test is this one's, so no
		// member who gets past here is too tired to work.
		//
		// The original also shows thought 0x14, tired, on the way into this branch, before it knows whether it
		// will find anything (FUN_0050be80 at 0x00506b50): ThinkOfTheMood's.
		if ( staff.Tiredness < RestLevel )
		{
			if ( GoAndRest( staff, walk ) )
			{
				staff.SetActivity( StaffActivity.GoingToRest, tick );

				return;
			}

			if ( (_random.Next() & 0xf) == 0 )
				staff.Happiness = Staff.Change( staff.Happiness, -HappinessHitForNoRestArea );

			staff.SetActivity( StaffActivity.Idle, tick );

			return;
		}

		switch ( staff.Model )
		{
			// FUN_004da5b0: a ride to fix (FUN_004daa90), which nothing here looks for, else a random walk.
			case MechanicModel:
				Unimplemented.Report( "MECHANIC_RIDE_SEARCH" );
				WalkAbout( staff, walk, tick );

				break;

			// FUN_004d7100: litter in range (FUN_004c8ed0), else a toilet to clean (FUN_004d7880 from
			// 0x004d72f7; docs/exe/ride-operation.md, "A toilet's dirt"), else a random walk. The litter search
			// is not built, and no cell here holds litter. The toilet found, or nought, is written every time.
			case HandymanModel:
				Unimplemented.Report( "HANDYMAN_LITTER_SEARCH" );

				staff.ToiletToClean = FindToilet( staff, walk, tick );

				if ( staff.ToiletToClean != 0 )
				{
					Log.Info( $"Staff: {staff.ThingId} found dirty loo {staff.ToiletToClean}, walking to it "
						+ $"on mGameTick {tick}" );
					staff.SetActivity( StaffActivity.GoingToLoo, tick );

					break;
				}

				WalkAbout( staff, walk, tick );

				break;

			case EntertainerModel:
				Entertain( staff, walk, tick );

				break;

			default:
				PatrolOrStay( staff, walk, tick );

				break;
		}
	}

	/// <summary>
	/// The handyman's search for a toilet to clean - <c>FUN_004d7880</c>, over the park's live objects in chain
	/// order (<c>docs/exe/ride-operation.md</c>, "A toilet's dirt"). A toilet is a candidate when nobody else
	/// is assigned to it (<see cref="AssignedTo"/>) and either it has asked for service and is nearer than the
	/// best so far, at any distance, or it is dirty and nearer than the grade's
	/// <see cref="HandymanDetectionRangeAt"/> in cells, squared and strictly - a test that does not ask the best
	/// so far, so among dirty toilets in range the last in the chain wins (<c>0x004d79a9</c>..<c>0x004d79cb</c>).
	/// A candidate is taken only when a route to its entry cell exists. The winner is assigned to this
	/// handyman, stamped with the clock (<c>FUN_004e01f0</c>).
	/// </summary>
	/// <remarks>
	/// The distance is cell to cell, the toilet's own cell, not its entry.
	/// <para>
	/// <b>A deviation, counted.</b> The original's route test is the destination setter itself
	/// (<c>FUN_004fa530</c>), so the handyman is left aimed at the last candidate tested, which is the winner
	/// unless a later candidate's route failed. Here he is aimed at the winner again, and the case is counted,
	/// <c>HANDYMAN_TOILET_AIM_LEFT_ON_A_LATER_TOILET</c>.
	/// </para>
	/// <para>
	/// A thing in the hand (<see cref="ParkWorld.CatalogueObject.IsPlaced"/>) is passed over, as
	/// <see cref="GoAndRest"/> passes it over.
	/// </para>
	/// </remarks>
	/// <returns>The toilet's thing id, or nought.</returns>
	private int FindToilet( Staff staff, PeepWalk walk, int tick )
	{
		if ( _state == null )
			return 0;

		var (x, y) = walk.Position.Cell;
		var range = HandymanDetectionRangeAt( staff.PayGrade );
		var winner = 0;
		var tested = 0;
		var best = uint.MaxValue;

		foreach ( var candidate in _state.ObjectsInChainOrder().ToArray() )
		{
			if ( !candidate.IsToilet || !candidate.IsPlaced )
				continue;

			var assigned = AssignedTo( candidate, tick );

			if ( assigned != 0 && assigned != staff.ThingId )
				continue;

			var acrossBy = candidate.CellX - x;
			var downBy = candidate.CellY - y;
			var distance = (uint)((acrossBy * acrossBy) + (downBy * downBy));

			if ( !(candidate.RequestedService != 0 && distance < best)
				&& !(ParkState.IsDirty( candidate ) && distance < (uint)(range * range)) )
				continue;

			tested = candidate.ThingId;

			if ( !AimAtEntryOf( staff, walk, candidate ) )
				continue;

			winner = candidate.ThingId;
			best = distance;
		}

		if ( winner == 0 || !_state.TryObject( winner, out var toilet ) )
			return 0;

		if ( tested != winner )
		{
			Unimplemented.Report( "HANDYMAN_TOILET_AIM_LEFT_ON_A_LATER_TOILET" );
			AimAtEntryOf( staff, walk, toilet );
		}

		_state.ReplaceObject( toilet with
		{
			AssignedStaff = (ushort)staff.ThingId, TimeMarkedForMaintenance = tick
		} );

		return winner;
	}

	/// <summary>Aims a member at a thing's entry cell and says whether a route exists - <c>FUN_004fa530</c>.</summary>
	private static bool AimAtEntryOf( Staff staff, PeepWalk walk, ParkWorld.CatalogueObject thing )
	{
		staff.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( thing.EntryCellX ), PeepNavigator.WaypointCentre( thing.EntryCellY ) );

		return walk.PlanRoute();
	}

	/// <summary>
	/// The member of staff assigned to a thing - <c>FUN_004e0220</c>, the getter every reader of
	/// <c>mAssignedStaffMember</c> goes through. An assignment more than <see cref="AssignmentKept"/> sweeps old
	/// whose member no longer aims at this thing (<c>FUN_00506580</c>: a handyman's
	/// <see cref="Staff.ToiletToClean"/>) is forgotten there and then, member and stamp both.
	/// </summary>
	/// <remarks>
	/// A mechanic's aim is his <c>+0x218</c>, which nothing here keeps: an assigned mechanic reads as aiming
	/// elsewhere. Nobody here assigns one; only a save can.
	/// </remarks>
	private int AssignedTo( ParkWorld.CatalogueObject thing, int tick )
	{
		if ( thing.AssignedStaff == 0 )
			return 0;

		if ( (uint)(thing.TimeMarkedForMaintenance + AssignmentKept) >= (uint)tick )
			return thing.AssignedStaff;

		if ( StaffById?.Invoke( thing.AssignedStaff ) is { Model: HandymanModel } member
			&& member.ToiletToClean == thing.ThingId )
			return thing.AssignedStaff;

		Log.Info( $"Object {thing.ThingId}: removing staff member {thing.AssignedStaff} from it, "
			+ $"assigned on mGameTick {thing.TimeMarkedForMaintenance}" );

		_state!.ReplaceObject( thing with { AssignedStaff = 0, TimeMarkedForMaintenance = 0 } );

		return 0;
	}

	/// <summary>
	/// The end of a handyman's walk to a toilet - <c>FUN_004d7790</c>: standing on the toilet's entry cell
	/// (<c>mEntryPos</c> against his own packed cell) and still its assigned member, he starts cleaning;
	/// otherwise he is idle.
	/// </summary>
	private void ArriveAtTheLoo( Staff staff, PeepWalk walk, int tick )
	{
		var (x, y) = walk.Position.Cell;

		if ( _state != null && _state.TryObject( staff.ToiletToClean, out var toilet )
			&& toilet.EntryPos == (y * ParkWorld.MapSize) + x + 1
			&& AssignedTo( toilet, tick ) == staff.ThingId )
		{
			staff.SetActivity( StaffActivity.Cleaning, tick );
			Log.Info( $"Staff: {staff.ThingId} starts cleaning loo {toilet.ThingId} on mGameTick {tick}" );

			return;
		}

		Log.Info( $"Staff: {staff.ThingId} got to loo {staff.ToiletToClean} on mGameTick {tick} and does not clean it" );
		staff.SetActivity( StaffActivity.Idle, tick );
	}

	/// <summary>
	/// The end of a clean - <c>0x004d7462</c>..<c>0x004d74c8</c>: the toilet is cleaned
	/// (<see cref="ParkRideOperation.Clean"/>) and opened whatever its state
	/// (<see cref="ParkRideOperation.Open(ParkState, RideScript?, int)"/>), the stand is queued, and the handyman
	/// is idle at stamp nought, so he decides again on the next sweep.
	/// </summary>
	private void CleanOn( Staff staff, int tick )
	{
		if ( _state != null && _state.TryObject( staff.ToiletToClean, out var toilet ) )
		{
			var script = ScriptFor?.Invoke( toilet.ThingId );

			ParkRideOperation.Clean( _state, script, toilet.ThingId );
			ParkRideOperation.Open( _state, script, toilet.ThingId );
		}

		Log.Info( $"Staff: {staff.ThingId} finished cleaning loo {staff.ToiletToClean} on mGameTick {tick}" );

		staff.NextAnimation = SpriteScript.Standing;
		staff.SetActivity( StaffActivity.Idle, tick );
	}

	/// <summary>
	/// What one turn of work costs - <c>FUN_00506760</c>, scaled by <c>(6 - grade)</c> as a walking turn is.
	/// </summary>
	private static void Work( Staff staff )
	{
		var scale = TiringBase - staff.PayGrade;

		staff.Tiredness = Staff.Change( staff.Tiredness, -( scale * TirednessPerWorkingTurn ) );
		staff.Happiness = Staff.Change( staff.Happiness, -( scale * HappinessPerWorkingTurn ) );
	}

	/// <summary>
	/// The mechanic's and the handyman's choice with no work: a random walk every time, whatever the clock reads
	/// (<c>0x004da6fa</c>, <c>0x004d712d</c>), and idle only when no destination is found. Idle stamps the clock
	/// from a walk and nought from an idle, so one who finds nowhere from standing is asked again every sweep.
	/// </summary>
	private void WalkAbout( Staff staff, PeepWalk walk, int tick )
	{
		var walking = SetRandomDest( staff, walk );

		staff.SetActivity( walking ? StaffActivity.Walking : StaffActivity.Idle, tick );

		if ( !walking )
			Log.Info( $"Staff: {staff.ThingId} stands on mGameTick {tick}, finding nowhere to walk" );
	}

	/// <summary>
	/// The entertainer's choice - <c>FUN_004d46d0</c>. A draw mod 3 of nought looks for a guest within the
	/// grade's <c>ActivationDistance</c> and, finding one, performs (<see cref="Perform"/>). Otherwise the
	/// guard's choice by <c>mGameTick &amp; 3</c>: nought stays, anything else looks for somewhere to walk.
	/// </summary>
	/// <remarks>
	/// Staying and a walk found each take one more draw the original throws away (<c>0x004d4734</c>,
	/// <c>0x004d4718</c>). <b>Finding nowhere leaves the state as it was</b>, with its stamp: no setter is called,
	/// so the entertainer is asked again on the next sweep, and one who was performing takes another turn of
	/// work and its end again.
	/// </remarks>
	private void Entertain( Staff staff, PeepWalk walk, int tick )
	{
		if ( _random.Next() % PerformShare == 0 && Perform( staff, walk, tick ) )
			return;

		if ( (tick & (StayPutShare - 1)) == 0 )
		{
			_random.Next();

			// The setter's stand, animation 3 (FUN_005054d0 case 0, FUN_004fa460), which Staff.SetActivity
			// queues for no idle; queued here from a performance, whose picture would otherwise play on.
			if ( staff.Activity == StaffActivity.Performing )
				staff.NextAnimation = SpriteScript.Standing;

			staff.SetActivity( StaffActivity.Idle, tick );

			Log.Info( $"Staff: {staff.ThingId} stands on mGameTick {tick}, its choice's low two bits nought" );

			return;
		}

		if ( !SetRandomDest( staff, walk ) )
		{
			Log.Info( $"Staff: {staff.ThingId} is left {staff.Activity} on mGameTick {tick}, finding nowhere to walk" );

			return;
		}

		_random.Next();
		staff.SetActivity( StaffActivity.Walking, tick );
	}

	/// <summary>
	/// The entertainer's look and the start of a performance - <c>0x004d4762</c>..<c>0x004d47e2</c>
	/// (<c>docs/exe/ride-operation.md</c>, "The entertainer's performance"). Any guest on the cells within the
	/// grade's <c>ActivationDistance</c> each way of the entertainer's own cell will do; nobody is kept, faced
	/// or walked to. Somebody there: a second draw, taken whatever follows; the animation is <c>0xd</c>, the
	/// bank's state 0, when the bank has one state group, and <c>0xd</c> + the draw mod (groups - 1) otherwise;
	/// then state <c>0xe</c> and its stamp, written inline.
	/// </summary>
	/// <remarks>
	/// <b>A bank with no state group</b> divides the draw by <c>0xffffffff</c> in the original and queues an
	/// animation past the table. No entertainer bank the game ships is one; here it is counted,
	/// <c>ENTERTAINER_BANK_WITHOUT_A_STATE_GROUP</c>, and no animation is queued.
	/// </remarks>
	/// <returns>Whether a performance began.</returns>
	private bool Perform( Staff staff, PeepWalk walk, int tick )
	{
		var (x, y) = walk.Position.Cell;
		var reach = EntertainerActivationDistanceAt( staff.PayGrade );
		var near = GuestsNear?.Invoke( (y * ParkWorld.MapSize) + x + 1, reach ) ?? 0;

		if ( near == 0 )
		{
			Log.Info( $"Staff: {staff.ThingId} looks from ({x},{y}) on mGameTick {tick} and finds no guest within {reach}" );

			return false;
		}

		var draw = _random.Next();
		var groups = StateGroupsOf?.Invoke( staff ) ?? 0;
		var animation = 0;

		if ( groups == 1 )
			animation = SpriteScript.FirstState;
		else if ( groups > 1 )
			animation = SpriteScript.FirstState + (draw % (groups - 1));
		else
			Unimplemented.Report( "ENTERTAINER_BANK_WITHOUT_A_STATE_GROUP" );

		staff.StartPerforming( animation, tick );

		Log.Info( $"Staff: {staff.ThingId} performs at ({x},{y}) on mGameTick {tick}, {near} guests within {reach}, "
			+ $"animation 0x{animation:x}" );

		return true;
	}

	/// <summary>
	/// The guard's and the researcher's choice: a guard walks on unless <c>mGameTick</c>'s low two bits are
	/// nought (<c>0x004d655d</c>); a researcher unless its draw's are (<c>0x00502ba9</c>). Staying, or finding
	/// nowhere, sets idle, which stamps only from a walk.
	/// </summary>
	/// <remarks>
	/// <b>A deviation (Q134):</b> where this researcher stays the original's researches, state <c>0xf</c>
	/// (<c>0x00502be4</c>), unless too tired; this one stands, and research never completes
	/// (<c>FUN_00504630</c>), counted here.
	/// </remarks>
	private void PatrolOrStay( Staff staff, PeepWalk walk, int tick )
	{
		if ( staff.Model == ResearcherModel )
			Unimplemented.Report( "RESEARCH_COMPLETING" );

		var choice = staff.Model == GuardModel ? tick : _random.Next();
		var walks = (choice & (StayPutShare - 1)) != 0;
		var walking = walks && SetRandomDest( staff, walk );

		staff.SetActivity( walking ? StaffActivity.Walking : StaffActivity.Idle, tick );

		if ( !walking )
		{
			Log.Info( $"Staff: {staff.ThingId} stands on mGameTick {tick}, "
				+ (walks ? "finding nowhere to walk" : "its choice's low two bits nought") );
		}
	}

	/// <summary>The mechanic's thing model.</summary>
	private const int MechanicModel = 4;

	/// <summary>The handyman's thing model.</summary>
	private const int HandymanModel = 5;

	/// <summary>The entertainer's thing model.</summary>
	private const int EntertainerModel = 6;

	/// <summary>The guard's thing model.</summary>
	private const int GuardModel = 7;

	/// <summary>The researcher's thing model.</summary>
	private const int ResearcherModel = 8;

	/// <summary>An entertainer looks for a guest to perform to on one decide's draw in three (<c>0x004d4756</c>).</summary>
	public const int PerformShare = 3;

	/// <summary>
	/// One turn of recovering - <c>FUN_005061d0</c>, which climbs both stats by this grade's own rates and
	/// holds each at a hundred.
	/// </summary>
	/// <remarks>
	/// <b>The rest ends when a stat reaches a hundred exactly</b>, which the original tests by truncating
	/// to a byte and comparing against 'd' - the character whose code is 100. It reads as a character in
	/// the decompiler and is a number.
	/// </remarks>
	private void Rest( Staff staff, int tick )
	{
		staff.Tiredness = Staff.Change( staff.Tiredness, RecuperationAt( staff.PayGrade ) );
		staff.Happiness = Staff.Change( staff.Happiness, HappinessRecuperationAt( staff.PayGrade ) );

		if ( (int)staff.Tiredness < (int)Staff.Most )
			return;

		// Up and back to work. The rest area is let go of on the way out, which is what the original does
		// before it hands back to the kind's own resume; it also takes the one back off VAR_STAFFIN
		// (FUN_00506d10, at 0x00506286), which is not built.
		Unimplemented.Report( "REST_AREA_OCCUPANCY" );
		staff.RestArea = 0;
		staff.SetActivity( StaffActivity.Idle, tick );
	}

	/// <summary>
	/// What one turn of walking costs - <c>FUN_005066a0</c>, scaled by <c>(6 - grade)</c> so that a better
	/// trained staff member wears down more slowly.
	/// </summary>
	private static void Tire( Staff staff )
	{
		var scale = TiringBase - staff.PayGrade;

		staff.Tiredness = Staff.Change( staff.Tiredness, -( scale * TirednessPerWalkingTurn ) );
		staff.Happiness = Staff.Change( staff.Happiness, -( scale * HappinessPerWalkingTurn ) );
	}

	/// <summary>
	/// One turn of walking and the animation that goes with it - the same join
	/// <see cref="PeepBehaviour"/> makes, and for the same reasons.
	/// </summary>
	private WalkVerdict Walked( Staff staff, PeepWalk walk, SpriteScript? playing )
	{
		var wandering = staff.Activity == StaffActivity.Walking;

		if ( !walk.HasRoute && (staff.Navigator.CannotReach
			|| !(wandering ? walk.PlanRoute( WanderBlocked ) : walk.PlanRoute())) )
			return WalkVerdict.CannotReach;

		// Containment deviation: the original filters destinations, but our steering can overshoot
		// a valid edge-near target onto the approach. Apply the same limits during a free wander.
		// Rest and strike walks keep the general route rules (docs/exe/staff-wandering.md).
		var verdict = wandering ? walk.Step( WanderBlocked ) : walk.Step();

		if ( verdict != WalkVerdict.Walking )
			return verdict;

		var wanted = Staff.AnimationFor( StaffActivity.Walking );

		if ( playing != null && !playing.IsOn( wanted ) )
			staff.NextAnimation = wanted;

		staff.NextInterval = SpriteScript.IntervalFor( walk.LastStep.X, walk.LastStep.Y, hurrying: false );

		return verdict;
	}

	/// <summary>
	/// Sends a staff member somewhere - the staff half of <c>FUN_004f9490</c>, one function with the guest
	/// half, whose patrol check sits in front of the link count (<c>docs/exe/ride-operation.md</c>, "SetRandomDest").
	///
	/// <para>
	/// <b>Standing outside your patrol area is answered before anything else</b>: a staff member who has
	/// wandered out of their patch heads straight back into it rather than picking a neighbour. Inside it,
	/// <see cref="LinkedWander"/> runs with any slot outside the area struck out, and at its dead end the
	/// patrol roll answers again. <b>On a cell with no links</b> - grass a member was put down on - the walk is
	/// <see cref="PeepBehaviour.WanderFromNowhere"/> instead, inside the area or outside it once the roll fails.
	/// </para>
	/// <para>
	/// <b>A guest in the same position gets a "stranded" stamp and a thought bubble instead</b>
	/// (<see cref="Peep.StrandedTime"/>). Staff never take that path; they take the patrol roll, which is why this
	/// is not simply the guest's routine with an extra test. The stranded refusal the original's call opens with
	/// is therefore not asked of staff here: nothing but a save can give a member a stamp.
	/// </para>
	/// </summary>
	private bool SetRandomDest( Staff staff, PeepWalk walk )
	{
		var (x, y) = walk.Position.Cell;

		// The call's pass count, drawn before the patrol check (0x004f9534).
		var passes = (_random.Next() % LinkedWander.MostPasses) + 1;

		// The original counts this cell's links once the patrol check is past - inside the area, or outside it
		// when the patrol roll fails (0x004f95af) - and at none takes the no-links arm whoever is asking
		// (0x004f95c0): the nearest path on seven rays, then five random cells, with no patrol roll after it.
		var noLinks = _state is { Park: not null } live && CellEdge.Links( live.Record( x, y ).Neighbours ) == 0;

		if ( !staff.Patrols( x, y ) )
		{
			if ( PatrolRoll( staff, walk ) )
				return true;

			// A deviation: on a linked cell the original goes on to the linked walk with no patrol filter;
			// this answers false (docs/exe/ride-operation.md, "Where OpenTPW differs").
			return noLinks && WanderFromNowhere( staff, walk, x, y );
		}

		if ( noLinks )
			return WanderFromNowhere( staff, walk, x, y );

		// The linked walk, a slot outside the patrol area struck out before the other filters (0x004f96fb). Its
		// dead end is the patrol roll for staff (0x004f9d8e), which a park never loaded, with no cells to walk,
		// takes as well.
		var stepped = _state is { } state
			? LinkedWander.Walk( state.Record, x, y, passes, _random, staff.Patrols )
			: null;

		if ( stepped == null )
			return PatrolRoll( staff, walk );

		// A random point inside the cell rather than its centre, the same clamped rolls a wandering guest
		// takes - this tail is shared between the two halves of the original's function.
		var cell = stepped[^1];

		staff.Navigator.Target = new FixedVector( SomewhereIn( cell.X ), SomewhereIn( cell.Y ) );

		var routed = walk.PlanRoute( WanderBlocked );

		Log.Info( $"Staff: {staff.ThingId} wander of {passes} from ({x},{y}) by "
			+ string.Join( " ", stepped.Select( step => $"({step.X},{step.Y})" ) ) + (routed ? "" : ", no route") );

		return routed;
	}

	/// <summary>
	/// The no-links arm for a member of staff - <see cref="PeepBehaviour.WanderFromNowhere"/>, the walk a guest
	/// takes, each aim routed as a staff wander's is.
	/// </summary>
	private bool WanderFromNowhere( Staff staff, PeepWalk walk, int x, int y )
		=> PeepBehaviour.WanderFromNowhere( _state!.Record, x, y, _random, cell =>
		{
			staff.Navigator.Target = new FixedVector(
				PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) );

			return walk.PlanRoute( WanderBlocked );
		}, $"Staff: {staff.ThingId}" );

	/// <summary>
	/// The linked-cell filters in FUN_004f9490. A cell with no links shuts nothing, so the walk off it to a
	/// path is free.
	/// </summary>
	private bool WanderBlocked( int x, int y, StepDirection direction )
	{
		if ( _state is not { } state )
			return false;

		var from = state.Record( x, y );
		if ( CellEdge.Links( from.Neighbours ) == 0 )
			return false;

		var (toX, toY) = MapStep.Beyond( x, y, direction );
		var to = state.Record( toX, toY );
		var bit = CellEdge.Opposite( CellEdge.BitFor( direction ) );

		return (from.Neighbours & bit) == 0
			|| (from.Type == CellEdge.Path && CellEdge.IsQueue( to.Type ))
			|| (from.Type == 3 && from.Direction == bit)
			|| to.Type == CellEdge.RideFarEnd;
	}

	/// <summary>
	/// The thought every kind's decide may show before it chooses - <c>FUN_00506a40</c>, which each kind's decide
	/// calls first: tired, <c>0x14</c> (<c>0x00506b50</c>); else unhappy, <c>0x13</c>, at a happiness byte of 10
	/// or less (<c>0x00506ccd</c>); else very happy, <c>0x12</c>, above 97 on one draw in sixteen
	/// (<c>0x00506cf4</c>), a draw taken only above 97. Their pictures are in <c>docs/exe/ride-operation.md</c>, "Thoughts and their pictures".
	/// </summary>
	/// <remarks>
	/// Tired is this file's own test (<see cref="RestLevel"/>, Q136 (a)), so the thought and the walk to a rest
	/// area agree with each other.
	/// </remarks>
	private void ThinkOfTheMood( Staff staff )
	{
		if ( staff.Tiredness < RestLevel )
		{
			Think( staff, 0x14 );

			return;
		}

		var happiness = (byte)(int)staff.Happiness;

		if ( happiness <= UnhappyThoughtAtMost )
			Think( staff, 0x13 );
		else if ( happiness > VeryHappyThoughtAbove && (_random.Next() & 0xf) == 0 )
			Think( staff, 0x12 );
	}

	/// <summary>Sets a member's thought - SetThought, <c>FUN_0050be80</c>, on the park's clock.</summary>
	private void Think( Staff staff, int thought )
	{
		var tick = _state?.GameTick ?? 0;
		var shown = staff.Thoughts.Set( thought, tick, ParkPeople.FirstPersonView );

		Log.Info( $"Staff {staff.ThingId}: thought 0x{thought:x}, bubble {(shown ? "made" : "not made")}, tick {tick}" );
	}

	/// <summary>
	/// Thirty tries at a cell inside the patrol area - <c>FUN_00506f30</c>, whose own log line when it runs
	/// out is "Could not find or reach a destination".
	/// </summary>
	/// <remarks>
	/// <b>This one aims at the cell CENTRE</b>, not at a random point inside it: it goes through the
	/// ordinary destination setter rather than through the tail that jitters. The two are a few lines apart
	/// in the original and do different things, which is worth not tidying.
	/// Only path cells are candidates (FUN_00506f30); route reachability is checked afterwards.
	/// </remarks>
	private bool PatrolRoll( Staff staff, PeepWalk walk )
	{
		if ( !staff.HasPatrolArea )
			return false;

		var (fromX, fromY) = staff.PatrolFrom;
		var (toX, toY) = staff.PatrolTo;

		// A rectangle the wrong way round would divide by nought below rather than merely pick badly. Every
		// patrol area the shipped park carries is well formed - ParkStaffStateTests pins that - so no test
		// here would ever have reached it, which is exactly why it is guarded rather than assumed.
		if ( toX < fromX || toY < fromY )
			return false;

		for ( var attempt = 0; attempt < PatrolTries; ++attempt )
		{
			var x = fromX + (_random.Next() % (toX - fromX + 1));
			var y = fromY + (_random.Next() % (toY - fromY + 1));

			if ( x < 0 || y < 0 || x >= ParkWorld.MapSize || y >= ParkWorld.MapSize )
				continue;

			if ( _state is { } state && state.Record( x, y ).Type != CellEdge.Path )
				continue;

			staff.Navigator.Target = new FixedVector(
				PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );

			if ( walk.PlanRoute( WanderBlocked ) )
				return true;
		}

		// Thought 0x16 beside the log line (0x0050701f).
		Think( staff, 0x16 );

		return false;
	}

	/// <summary>
	/// Finds the nearest rest area and sets off for it, or says it could not - <c>FUN_00506910</c>, which
	/// is what <c>FUN_00506a40</c> calls the moment a staff member is too tired to work.
	///
	/// <para>
	/// <b>It walks to the object's <c>mEntryPos</c>, not to the object.</b> The original takes what
	/// <c>FUN_00506910</c> hands back, reads that thing's <c>+0x36</c> - which the serialiser names
	/// <c>mEntryPos</c> - and gives <i>that</i> to the destination setter. Walking to the object's own cell
	/// would send them into the thing rather than to the spot it is approached from.
	/// </para>
	/// <para>
	/// <b>The rest area is taken only once a route exists</b>, which is the original's order too: it writes
	/// <c>mRestArea</c> inside the branch where the destination setter succeeded, so a staff member never
	/// claims somewhere they cannot get to.
	/// </para>
	/// <para>
	/// Nearest is by squared distance between cells, the first of two equally near in chain order, as the
	/// original measures it: it follows <c>mNext</c> from <c>mFirstObject</c>, the park's live objects. So a
	/// thing sold is never offered, since the destructor has taken it off the chain.
	/// </para>
	/// </summary>
	private bool GoAndRest( Staff staff, PeepWalk walk )
	{
		if ( _state == null )
			return false;

		var (x, y) = walk.Position.Cell;

		ParkWorld.CatalogueObject? nearest = null;
		var nearestDistance = int.MaxValue;

		foreach ( var candidate in _state.ObjectsInChainOrder() )
		{
			if ( !candidate.IsRestArea || !candidate.IsPlaced )
				continue;

			var acrossBy = candidate.CellX - x;
			var downBy = candidate.CellY - y;
			var distance = (acrossBy * acrossBy) + (downBy * downBy);

			if ( distance >= nearestDistance )
				continue;

			nearestDistance = distance;
			nearest = candidate;
		}

		if ( nearest is not { } restArea )
			return false;

		staff.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( restArea.EntryCellX ),
			PeepNavigator.WaypointCentre( restArea.EntryCellY ) );

		if ( !walk.PlanRoute() )
			return false;

		staff.RestArea = restArea.ThingId;

		return true;
	}

	/// <summary>
	/// A staff member's answer to the object destructor's type-10 message, sent when a thing is sold or
	/// picked up to be moved - the rest-area arm <c>FUN_00504c70</c>, which every kind of staff ends in.
	/// See <c>docs/exe/park-engine.md</c>, "Selling and the people on it".
	/// </summary>
	/// <remarks>
	/// Only a staff member whose <see cref="Staff.RestArea"/> is the thing answers, and only while resting
	/// or on the way to rest. <b>One resting there is put out</b> (<c>FUN_00506d10</c>) and goes
	/// <see cref="StaffActivity.Idle"/>, then is pointed at the nearest other rest area and claims it if a
	/// route exists. They stay Idle either way (<c>0x00504d8f</c>), so the claim goes unused until they are
	/// next sent to rest. <b>One on the way there</b> gives the claim up and goes Idle.
	/// <para>
	/// <b>A handyman whose toilet job (<c>+0x21a</c>) is the thing drops it first and goes idle</b>, in any
	/// state, by his own arm. A mechanic's ride job (<c>+0x218</c>) goes the same way and is not built: no
	/// mechanic holds one here.
	/// </para>
	/// <para>
	/// <b>Not built, and each has nothing here to act on.</b> <c>FUN_00506d10</c> also takes one off the rest area's
	/// <c>VAR_STAFFIN</c>, tells the resting-staff list (message 15) and rebuilds the sprite; the count and
	/// the list are unbuilt wherever the original touches them, and each site counts
	/// <c>REST_AREA_OCCUPANCY</c>.
	/// </para>
	/// <para>
	/// <b>A deviation both arms share:</b> the original's state-0 setter queues the stand, animation 3
	/// (<c>FUN_005054d0</c> case 0, through <c>FUN_004fa460</c>). <see cref="Staff.AnimationFor"/> queues
	/// nothing for <see cref="StaffActivity.Idle"/>, so a member put out keeps the cycle they had.
	/// </para>
	/// </remarks>
	internal void ThingRemoved( Staff staff, PeepWalk? walk, int thingId, int tick )
	{
		ArgumentNullException.ThrowIfNull( staff );

		if ( thingId != 0 && staff.Model == HandymanModel && staff.ToiletToClean == thingId )
		{
			staff.ToiletToClean = 0;
			staff.SetActivity( StaffActivity.Idle, tick );

			Log.Info( $"Staff: {staff.ThingId} dropped the job on loo {thingId}, now {staff.Activity}" );
		}

		if ( thingId == 0 || staff.RestArea != thingId )
			return;

		switch ( staff.Activity )
		{
			case StaffActivity.Resting:
				Unimplemented.Report( "REST_AREA_OCCUPANCY" );

				staff.RestArea = 0;
				staff.SetActivity( StaffActivity.Idle, tick );

				// The claim, and the route that makes it; the state is left alone.
				if ( walk != null )
					GoAndRest( staff, walk );

				Log.Info( $"Staff: {staff.ThingId} put out of rest area {thingId}, now {staff.Activity} "
					+ $"claiming {staff.RestArea}" );

				break;

			case StaffActivity.GoingToRest:
				staff.RestArea = 0;
				staff.SetActivity( StaffActivity.Idle, tick );

				Log.Info( $"Staff: {staff.ThingId} gave up going to rest area {thingId}, now {staff.Activity}" );

				break;

			default:
				break;
		}
	}

	/// <summary>The near edge of a cell plus a clamped roll - see <see cref="PeepBehaviour"/>.</summary>
	private int SomewhereIn( int cell )
	{
		var within = Math.Clamp( _random.Next() & 0x7f, 5, 0x7b );

		return (cell * PeepNavigator.One) + (within * (PeepNavigator.One / 256));
	}
}
