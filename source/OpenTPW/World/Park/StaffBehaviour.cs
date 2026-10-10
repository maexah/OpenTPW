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
/// <b>What is built.</b> Every shared state, and every kind's decide with no work
/// found: the mechanic and the handyman set off on a random walk every time, the guard and the entertainer on
/// three sweeps in four by the park's clock, <c>mGameTick &amp; 3</c>, and the researcher on three decides in
/// four by a draw.
/// </para>
/// <para>
/// <b>The researcher's research is built</b>: state <c>0xf</c> on the fourth decide or with nowhere to walk,
/// its stamp and its end - <see cref="Research"/>. The lab it works for is not: the points are counted.
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
/// </para>
/// <para>
/// <b>The strike is built</b>: the arm every decide opens with, the walk to the picket (state 4) and the
/// picket's turn with its two ends (state 5) - <see cref="GoOnStrike"/>, <see cref="Picket"/>. The records
/// are <see cref="ParkStrikes"/>'s.
/// </para>
/// <para>
/// <b>Rest areas are built.</b> The flag naming one is read - bit 1
/// of a catalogue object's <c>mFlags</c> - so a tired staff member does what
/// <c>FUN_00506a40</c> does: looks for the nearest object flagged as a rest area and walks to it. With none
/// found or reached the kind's own choice follows, so they walk on (<see cref="TiredOrCarryingOn"/>).
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

	/// <summary>An object's item's <c>TrackType</c>, which the repair's open asks; nought where nothing answers.</summary>
	public Func<ParkWorld.CatalogueObject, int>? TrackTypeOf { get; init; }

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

	/// <summary>
	/// Plays a <c>cat_staff</c> effect where a member stands - <c>FUN_004faa00</c> on <c>[0x00803a30]</c>
	/// (<see cref="ParkAudio.StaffSound"/>). Null plays nothing; the draw before it is taken all the same.
	/// </summary>
	public Action<Staff, int>? Sound { get; init; }

	/// <summary>The staff HQ's strike records, which the decide's first arm and a striker's turn ask. Null: nobody strikes.</summary>
	public ParkStrikes? Strikes { get; init; }

	/// <summary>
	/// What the gate's script says it is doing - <c>FUN_0051a290</c>, <c>ParkRides.GateStatus</c>. A member
	/// sets off for the picket only while it reads <see cref="ParkRides.GateIsOpen"/>.
	/// </summary>
	public Func<int>? GateStatus { get; init; }

	/// <summary>
	/// How many guests are inside the park, counted afresh - <c>FUN_004c9130</c>
	/// (<see cref="ParkPeople.GateGuestCensus"/>). A strike ends in a park shut with none.
	/// </summary>
	public Func<int>? GuestsInside { get; init; }

	/// <summary>
	/// The strike area: its first cell and its size in cells - <c>FixedItemInfo.StrikeAreaStartX</c>,
	/// <c>StartY</c>, <c>SizeX</c> and <c>SizeY</c> (<c>0x007855ec</c> on): (40,9), six by one.
	/// </summary>
	public (int X, int Y, int Across, int Down) StrikeArea { get; }

	/// <summary>Where a striker goes back to - <c>FixedItemInfo.EntranceAPosX</c> and <c>Y</c> (<c>0x007855bc</c>): (47,17).</summary>
	public (int X, int Y) EntranceA { get; }

	private readonly int[] _researcherWork = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _researchAbility = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _entertainerWork = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _entertainerReach = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _idleDuration = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _handymanWork = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _handymanRange = new int[ParkWorld.StaffState.PayGrades];
	private readonly int[] _mechanicWork = new int[ParkWorld.StaffState.PayGrades];
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

		StrikeArea = (balance?.Int( "FixedItemInfo.StrikeAreaStartX", 40 ) ?? 40,
			balance?.Int( "FixedItemInfo.StrikeAreaStartY", 9 ) ?? 9,
			balance?.Int( "FixedItemInfo.StrikeAreaSizeX", 6 ) ?? 6,
			balance?.Int( "FixedItemInfo.StrikeAreaSizeY", 1 ) ?? 1);
		EntranceA = (balance?.Int( "FixedItemInfo.EntranceAPosX", 47 ) ?? 47,
			balance?.Int( "FixedItemInfo.EntranceAPosY", 17 ) ?? 17);

		int[] idleFallback = [40, 30, 20, 10, 5];
		float[] restFallback = [0.2f, 0.3f, 0.4f, 0.5f, 0.75f];
		float[] moodFallback = [1f, 2f, 2f, 3f, 3f];
		int[] workFallback = [40, 30, 20, 10, 5];
		int[] rangeFallback = [2, 3, 3, 4, 5];
		int[] performFallback = [10, 20, 30, 50, 75];
		int[] reachFallback = [3, 3, 4, 4, 5];
		int[] researchFallback = [10, 20, 30, 40, 50];
		int[] abilityFallback = [2, 3, 4, 5, 6];
		int[] repairFallback = [80, 60, 40, 30, 20];

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

			_mechanicWork[grade] = balance?.Int( $"MechanicConstsPerGrade[{grade}].WorkDuration", repairFallback[grade] )
				?? repairFallback[grade];

			var entertainer = $"EntertainerConstsPerGrade[{grade}]";

			_entertainerWork[grade] = balance?.Int( $"{entertainer}.WorkDuration", performFallback[grade] )
				?? performFallback[grade];
			_entertainerReach[grade] = balance?.Int( $"{entertainer}.ActivationDistance", reachFallback[grade] )
				?? reachFallback[grade];

			var researcher = $"ResearcherConstsPerGrade[{grade}]";

			_researcherWork[grade] = balance?.Int( $"{researcher}.WorkDuration", researchFallback[grade] )
				?? researchFallback[grade];
			_researchAbility[grade] = balance?.Int( $"{researcher}.ResearchAbility", abilityFallback[grade] )
				?? abilityFallback[grade];
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
	/// How long a mechanic of this grade takes over a ride with no repair left -
	/// <c>MechanicConstsPerGrade.WorkDuration</c>, the table at <c>0x0078542c</c>: 80, 60, 40, 30, 20.
	/// </summary>
	public int MechanicWorkDurationAt( int grade )
		=> _mechanicWork[Math.Clamp( grade, 0, _mechanicWork.Length - 1 )];

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

	/// <summary>
	/// How long a researcher of this grade researches - <c>ResearcherConstsPerGrade.WorkDuration</c>, the table at
	/// <c>0x00785458</c>: 10, 20, 30, 40, 50.
	/// </summary>
	public int ResearcherWorkDurationAt( int grade )
		=> _researcherWork[Math.Clamp( grade, 0, _researcherWork.Length - 1 )];

	/// <summary>
	/// How many points a researcher of this grade hands the lab at a time -
	/// <c>ResearcherConstsPerGrade.ResearchAbility</c>, the table at <c>0x0078545c</c>: 2, 3, 4, 5, 6.
	/// </summary>
	public int ResearchAbilityAt( int grade )
		=> _researchAbility[Math.Clamp( grade, 0, _researchAbility.Length - 1 )];

	/// <summary>A researcher hands the lab points on every sweep the clock divides by this - <c>0x0050297f</c>.</summary>
	public const int ResearchPointsEvery = 20;

	/// <summary>
	/// The points researchers have been counted as handing the lab since the park loaded, which nothing
	/// spends: there is no lab.
	/// </summary>
	public int ResearchPointsCounted { get; private set; }

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
	/// by the clock; the researcher's are a draw's (<c>0x00502ba9</c>), and where it would stay it researches.
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

		// A researcher's pre-step hands the lab the grade's ResearchAbility on every sweep the clock divides by
		// twenty, in any state but going to rest, resting, the two strike states and carried (FUN_00502960,
		// FUN_005064f0 answering 3, 4 or 5). The lab's spending of them, FUN_00503430, is not built: counted.
		if ( staff.Model == ResearcherModel && (uint)tick % ResearchPointsEvery == 0
			&& staff.Activity is not (StaffActivity.GoingToRest or StaffActivity.Resting
				or StaffActivity.GoingOnStrike or StaffActivity.OnStrike or StaffActivity.Held) )
		{
			Unimplemented.Report( "RESEARCH_POINTS_TO_THE_LAB" );
			ResearchPointsCounted += ResearchAbilityAt( staff.PayGrade );

			Log.Info( $"Staff: {staff.ThingId} hands the lab {ResearchAbilityAt( staff.PayGrade )} research points "
				+ $"on mGameTick {tick}, counted and not spent: {ResearchPointsCounted} so far" );
		}

		switch ( staff.Activity )
		{
			// Standing about. They wait out their grade's idle duration and then look for something to do: the
			// first sweep past stamp + IdleDuration, so IdleDuration + 1 sweeps after a walk's stamp (0x004d6545).
			case StaffActivity.Idle:
				DrawForSound( staff, IdleSoundOf( staff.Model ), tick );

				if ( (uint)tick <= (uint)(staff.TimeStartedIdling + IdleDurationAt( staff.PayGrade )) )
					break;

				Decide( staff, walk, tick );

				break;

			// Walking somewhere. Still going costs them rest and mood; arriving or giving up both end in
			// the same decision, which is the original's own shape - it falls out of the switch either way.
			case StaffActivity.Walking:
				DrawForSound( staff, IdleSoundOf( staff.Model ) - 1, tick );

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
				Rest( staff, walk, tick );

				break;

			// Walking to the picket - FUN_005056e0's case 4: a step of the walk, thought 0x15 every turn, and at
			// the walk's end, arrived or failed alike, the mark at +0x188 is cleared and the member is on strike.
			case StaffActivity.GoingOnStrike:
				var picketWalk = Walked( staff, walk, playing );

				Think( staff, 0x15 );

				if ( picketWalk != WalkVerdict.Walking )
				{
					var (picketX, picketY) = walk.Position.Cell;

					staff.SettingOffForTheStrike = false;
					staff.SetActivity( StaffActivity.OnStrike, tick );

					Log.Info( $"Staff: {staff.ThingId} is on strike at ({picketX},{picketY}) on mGameTick {tick}, "
						+ $"the walk {picketWalk}" );
				}

				break;

			case StaffActivity.OnStrike:
				Picket( staff, walk, tick );

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

			// A mechanic on the way to a ride - FUN_004da740. The ride's own word on who is assigned to it comes
			// first; then a step of the walk, which costs no rest or mood here: the arm has no FUN_005066a0.
			case StaffActivity.GoingToRide:
				if ( _state == null || !_state.TryObject( staff.ObjectToRepair, out var aimedAt )
					|| AssignedTo( aimedAt, tick ) != staff.ThingId )
				{
					Log.Info( $"Staff: mechanic {staff.ThingId} is not the member assigned to ride "
						+ $"{staff.ObjectToRepair} on mGameTick {tick}" );
					staff.ObjectToRepair = 0;
					Decide( staff, walk, tick );

					break;
				}

				switch ( Walked( staff, walk, playing ) )
				{
					case WalkVerdict.Arrived:
						ArriveAtTheRide( staff, walk, aimedAt, tick );
						break;

					case WalkVerdict.CannotReach:
						staff.ObjectToRepair = 0;
						Decide( staff, walk, tick );
						break;

					default:
						break;
				}

				break;

			// A mechanic repairing - FUN_004da830: a turn of work, the count down a sweep, and at nought the end.
			case StaffActivity.Repairing:
				Work( staff );

				if ( staff.DurationOfRepair != 0 )
				{
					--staff.DurationOfRepair;

					break;
				}

				RepairOn( staff, walk, tick );

				break;

			// An entertainer performing - FUN_004d4810's case 0xe: a turn of work, then, on the first sweep past
			// stamp + WorkDuration, the end's sound and the decide again in the same turn, which may start another
			// performance on a fresh stamp.
			case StaffActivity.Performing:
				Work( staff );

				if ( (uint)tick <= (uint)(staff.TimeStartedEntertaining + EntertainerWorkDurationAt( staff.PayGrade )) )
					break;

				// cat_staff effect 0x87 at the sprite's position, with no draw (0x004d48b0).
				PlaySound( staff, PerformanceEndSound, tick );
				Log.Info( $"Staff: {staff.ThingId} ends the performance of mGameTick {staff.TimeStartedEntertaining} "
					+ $"on mGameTick {tick}" );

				Decide( staff, walk, tick );

				break;

			// A researcher researching - FUN_005029f0's case 0xf: a turn of work, then, on the first sweep past
			// stamp + WorkDuration, a walk if somewhere is found (0x00502a40, with no FUN_00506a40 and no draw)
			// and otherwise a fresh stamp and the same again. A turn that does not end draws for cat_staff
			// effect 0x8a (0x00502a61).
			case StaffActivity.Researching:
				Work( staff );

				if ( (uint)tick <= (uint)(staff.TimeStartedResearching + ResearcherWorkDurationAt( staff.PayGrade )) )
				{
					DrawForSound( staff, ResearchingSound, tick );

					break;
				}

				if ( SetRandomDest( staff, walk ) )
				{
					Log.Info( $"Staff: {staff.ThingId} ends the research of mGameTick {staff.TimeStartedResearching} "
						+ $"on mGameTick {tick} and walks" );
					staff.SetActivity( StaffActivity.Walking, tick );

					break;
				}

				Log.Info( $"Staff: {staff.ThingId} ends the research of mGameTick {staff.TimeStartedResearching} "
					+ $"on mGameTick {tick} and researches again, finding nowhere to walk" );
				staff.TimeStartedResearching = tick;

				break;

			// Being carried does nothing here: the original's case 7 has an empty body.
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
	/// none, set off on a random walk every time; the entertainer and the guard stay on one choice in four, where
	/// the researcher researches. The handyman's toilet search and the entertainer's look find work; the others
	/// are counted where the original makes them.
	/// </para>
	/// </summary>
	private void Decide( Staff staff, PeepWalk walk, int tick )
	{
		if ( TiredOrCarryingOn( staff, walk, tick ) )
			return;

		// The mechanic's, the handyman's and the entertainer's decides skip their search for work when the
		// member is too tired for it and go straight to the walk (0x004da5c7, 0x004d7113, 0x004d46e0); the
		// researcher's asks only before it researches, and the guard's not at all.
		var tooTired = TooTiredToWork( staff );

		if ( tooTired )
			Log.Info( $"Staff: {staff.ThingId} is too tired to work on mGameTick {tick}, rest {staff.Tiredness:0.000}" );

		switch ( staff.Model )
		{
			// FUN_004da5b0: a ride to fix (FUN_004daa90; docs/exe/ride-operation.md, "The mechanic's search"),
			// else a random walk. The ride found, or nought, is written every time the search is made, and is
			// kept where no route reaches it.
			case MechanicModel:
				if ( tooTired )
				{
					WalkAbout( staff, walk, tick );

					break;
				}

				staff.ObjectToRepair = FindRide( staff, walk, tick );

				if ( staff.ObjectToRepair != 0 && _state!.TryObject( staff.ObjectToRepair, out var broken ) )
				{
					if ( AimAtEntryOf( staff, walk, broken ) )
					{
						_state.ReplaceObject( broken with
						{
							AssignedStaff = (ushort)staff.ThingId, TimeMarkedForMaintenance = tick
						} );

						// A ride nobody called him to gets advisor message 0x46 (0x004da6bf), which nothing posts.
						if ( broken.RequestedService == 0 )
							Unimplemented.Report( "MECHANIC_ON_HIS_WAY_MESSAGE" );

						Log.Info( $"Staff: mechanic {staff.ThingId} found ride {broken.ThingId} that needs fixing, "
							+ $"walking to it on mGameTick {tick}" );
						staff.SetActivity( StaffActivity.GoingToRide, tick );

						break;
					}

					Log.Info( $"Staff: mechanic {staff.ThingId} could not reach broken ride {broken.ThingId} "
						+ $"on mGameTick {tick}" );
				}

				WalkAbout( staff, walk, tick );

				break;

			// FUN_004d7100: litter in range (FUN_004c8ed0), else a toilet to clean (FUN_004d7880 from
			// 0x004d72f7; docs/exe/ride-operation.md, "A toilet's dirt"), else a random walk. The litter search
			// is not built, and no cell here holds litter. The toilet found, or nought, is written every time
			// the search is made.
			case HandymanModel:
				if ( tooTired )
				{
					WalkAbout( staff, walk, tick );

					break;
				}

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
				Entertain( staff, walk, tick, tooTired );

				break;

			case ResearcherModel:
				Research( staff, walk, tick, tooTired );

				break;

			default:
				PatrolOrStay( staff, walk, tick );

				break;
		}
	}

	/// <summary>
	/// What every kind's decide opens with - <c>FUN_00506a40</c>, which answers whether it set a state
	/// (<c>docs/exe/ride-operation.md</c>, "Drawn on the way").
	///
	/// <para>
	/// <b>The strike comes first</b> (<see cref="GoOnStrike"/>): a member whose kind is on strike sets off for the
	/// picket and this answers true; with no route there the rest of this follows.
	/// </para>
	/// <para>
	/// <b>Tired</b> is the rest byte, the float truncated, at or under <see cref="RestLevel"/>
	/// (<c>0x00506b41</c>): thought <c>0x14</c>, then the nearest rest area (<see cref="GoAndRest"/>). Found and
	/// routed to, the member sets off for it and this answers true. Not found and not reached are one path in the
	/// original, down to the same log line: a draw whose low four bits are nought takes
	/// <see cref="HappinessHitForNoRestArea"/> off their happiness, and the answer is false, so the kind's own
	/// choice follows and the member walks on.
	/// </para>
	/// <para>
	/// <b>Not tired</b>, the base speed is set from the rest byte, one of <see cref="Peep.BaseSpeeds"/> by
	/// fifths and the last at a hundred (<c>0x00506c84</c>), and then the mood's thought: unhappy, <c>0x13</c>,
	/// at a happiness byte of 10 or less (<c>0x00506ccd</c>); else very happy, <c>0x12</c>, above 97 on one draw
	/// in sixteen (<c>0x00506cf4</c>), a draw taken only above 97. The answer is false.
	/// </para>
	/// </summary>
	private bool TiredOrCarryingOn( Staff staff, PeepWalk walk, int tick )
	{
		if ( GoOnStrike( staff, walk, tick ) )
			return true;

		var rest = RestByte( staff );

		if ( rest > RestLevel )
		{
			staff.BaseSpeed = Peep.BaseSpeeds[Math.Min( rest / RestPerBaseSpeed, Peep.BaseSpeeds.Length - 1 )];

			var happiness = (byte)(int)staff.Happiness;

			if ( happiness <= UnhappyThoughtAtMost )
				Think( staff, 0x13 );
			else if ( happiness > VeryHappyThoughtAbove && (_random.Next() & 0xf) == 0 )
				Think( staff, 0x12 );

			return false;
		}

		Think( staff, 0x14 );

		if ( GoAndRest( staff, walk ) )
		{
			Log.Info( $"Staff: {staff.ThingId} is tired on mGameTick {tick}, rest {staff.Tiredness:0.000}, "
				+ $"and sets off for rest area {staff.RestArea}" );
			staff.SetActivity( StaffActivity.GoingToRest, tick );

			return true;
		}

		if ( (_random.Next() & 0xf) == 0 )
			staff.Happiness = Staff.Change( staff.Happiness, -HappinessHitForNoRestArea );

		Log.Info( $"Staff: {staff.ThingId} is tired on mGameTick {tick}, rest {staff.Tiredness:0.000}, "
			+ "and found or reached no rest area: carries on" );

		return false;
	}

	/// <summary>
	/// The decide's strike arm - <c>0x00506a4d</c>..<c>0x00506b25</c> (<c>docs/exe/ride-operation.md</c>, "The
	/// strike"). The member's kind on strike and the gate's status reading open: the mark at <c>+0x188</c> is
	/// set and the world random is drawn four times, for a cell across the strike area and a byte inside it, then
	/// a cell down it and a byte inside that. A route there takes state 4 and answers true; none answers false.
	/// </summary>
	private bool GoOnStrike( Staff staff, PeepWalk walk, int tick )
	{
		if ( Strikes == null || !Strikes.IsOnStrike( ParkStaffPool.KindFor( staff.Model ) )
			|| GateStatus?.Invoke() != ParkRides.GateIsOpen )
			return false;

		staff.SettingOffForTheStrike = true;

		var x = StrikeArea.X + (int)((uint)_random.Next() % (uint)StrikeArea.Across);
		var withinX = _random.Next() & 0xff;
		var y = StrikeArea.Y + (int)((uint)_random.Next() % (uint)StrikeArea.Down);
		var withinY = _random.Next() & 0xff;

		staff.Navigator.Target = new FixedVector(
			(x * PeepNavigator.One) + (withinX * (PeepNavigator.One / 256)),
			(y * PeepNavigator.One) + (withinY * (PeepNavigator.One / 256)) );

		if ( !walk.PlanRoute() )
		{
			Log.Info( $"Staff: {staff.ThingId} is fed up on mGameTick {tick} and finds no route to the strike area "
				+ $"at ({x},{y})" );

			return false;
		}

		Log.Info( $"Staff: {staff.ThingId} is fed up and goes on strike on mGameTick {tick}, to ({x},{y})" );
		staff.SetActivity( StaffActivity.GoingOnStrike, tick );

		return true;
	}

	/// <summary>
	/// A striker's turn - state 5, <c>FUN_00506300</c>. One draw: on its low three bits nought the facing moves
	/// by the draw's low byte less 128, and a sum past <c>0x7ff</c> unsigned, a turn below nought among them, is
	/// held at <c>0x7ff</c>. Then, with the park shut and no guest inside it, the kind's strike is ended
	/// (<see cref="ParkStrikes.EndStrike"/>). Then, the kind not on strike, a route to
	/// <see cref="EntranceA"/>'s cell takes the walk and none takes idle.
	/// </summary>
	private void Picket( Staff staff, PeepWalk walk, int tick )
	{
		var draw = _random.Next();

		if ( (draw & PicketTurnMask) == 0 )
			walk.Heading = (int)Math.Min( (uint)((draw & 0xff) - 0x80 + walk.Heading), PeepHeading.FullTurn - 1 );

		var kind = ParkStaffPool.KindFor( staff.Model );

		if ( _state is { ParkIsClosed: true } && (GuestsInside?.Invoke() ?? 0) == 0 )
			Strikes?.EndStrike( kind );

		if ( Strikes?.IsOnStrike( kind ) == true )
			return;

		staff.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( EntranceA.X ), PeepNavigator.WaypointCentre( EntranceA.Y ) );

		var routed = walk.PlanRoute();

		staff.SetActivity( routed ? StaffActivity.Walking : StaffActivity.Idle, tick );

		Log.Info( $"Staff: {staff.ThingId} leaves the picket on mGameTick {tick}, "
			+ (routed ? "walking to the park entrance" : "finding no route to the park entrance") );
	}

	/// <summary>A striker turns on a draw whose low three bits are nought (<c>TEST AL,0x7</c>, <c>FUN_00506300</c>).</summary>
	public const int PicketTurnMask = 7;

	/// <summary>How rested a member is as the original tests it: the float truncated to a byte (<c>__ftol</c>, <c>AND 0xff</c>).</summary>
	private static int RestByte( Staff staff ) => (byte)(int)staff.Tiredness;

	/// <summary>
	/// Too tired to work - <c>FUN_00506680</c>: the rest byte under <see cref="RestLevel"/>, strictly
	/// (<c>0x00506698</c>), where tired is at or under it. With the shipped level of 1, a rest byte of nought.
	/// </summary>
	private bool TooTiredToWork( Staff staff ) => RestByte( staff ) < RestLevel;

	/// <summary>How much of the rest byte each of <see cref="Peep.BaseSpeeds"/> covers - the 20 of <c>FUN_00506a40</c>'s divide.</summary>
	public const int RestPerBaseSpeed = 20;

	/// <summary>
	/// The mechanic's search for a ride to fix - <c>FUN_004daa90</c>, over the park's objects from the
	/// mechanics' cursor (<see cref="ParkState.ObjectsFromTheMechanicsCursor"/>;
	/// <c>docs/exe/ride-operation.md</c>, "The mechanic's search"). An object is a candidate when it is broken
	/// down (state 1), or a mechanic has been called to it and it is no toilet, and nobody else is assigned to
	/// it (<see cref="AssignedTo"/>). The nearest wins, cell to cell from the mechanic's own, squared and
	/// strictly, at any distance; no route is asked for here.
	/// </summary>
	/// <remarks>
	/// <b>Not built, counted.</b> An object waiting for an upgrade (state 2) is a candidate in the original;
	/// here it is passed over, <c>MECHANIC_UPGRADE_JOB</c>. A thing in the hand is passed over, as
	/// <see cref="FindToilet"/> passes it over.
	/// </remarks>
	/// <returns>The ride's thing id, or nought.</returns>
	private int FindRide( Staff staff, PeepWalk walk, int tick )
	{
		if ( _state == null )
			return 0;

		var (x, y) = walk.Position.Cell;
		var winner = 0;
		var best = uint.MaxValue;

		foreach ( var candidate in _state.ObjectsFromTheMechanicsCursor() )
		{
			if ( !candidate.IsPlaced )
				continue;

			if ( candidate.State == 2 )
			{
				Unimplemented.Report( "MECHANIC_UPGRADE_JOB" );

				continue;
			}

			if ( candidate.State != ParkRideChoice.StateRefusedOne
				&& !(candidate.RequestedService != 0 && !candidate.IsToilet) )
				continue;

			var assigned = AssignedTo( candidate, tick );

			if ( assigned != 0 && assigned != staff.ThingId )
				continue;

			var acrossBy = candidate.CellX - x;
			var downBy = candidate.CellY - y;
			var distance = (uint)((acrossBy * acrossBy) + (downBy * downBy));

			if ( distance >= best )
				continue;

			best = distance;
			winner = candidate.ThingId;
		}

		return winner;
	}

	/// <summary>
	/// The end of a mechanic's walk to a ride - <c>FUN_004da740</c>'s arrival: a ride still broken down or
	/// still calling for him is repaired, for <see cref="MechanicWorkDurationAt"/> sweeps scaled by the State of
	/// repair it has lost, cut to a byte (<c>FUN_004da370</c>, <c>0x004da42a</c>); any other sends him back to
	/// his decide, the ride still named.
	/// </summary>
	private void ArriveAtTheRide( Staff staff, PeepWalk walk, ParkWorld.CatalogueObject ride, int tick )
	{
		if ( ride.State != ParkRideChoice.StateRefusedOne && ride.RequestedService == 0 )
		{
			Log.Info( $"Staff: mechanic {staff.ThingId} arrived at ride {ride.ThingId} on mGameTick {tick} "
				+ "but it does not need fixing" );
			Decide( staff, walk, tick );

			return;
		}

		var lost = 100 - (byte)(int)ride.StateOfRepair;

		staff.StartRepairing( lost * MechanicWorkDurationAt( staff.PayGrade ) / 100 );

		Log.Info( $"Staff: mechanic {staff.ThingId} starts repairing ride {ride.ThingId} on mGameTick {tick}, "
			+ $"{staff.DurationOfRepair} sweeps to go" );
	}

	/// <summary>
	/// The end of a repair's count down - <c>0x004da86d</c>..<c>0x004da8d6</c>. A ride broken down has its
	/// script told the break is over (<see cref="ParkRideOperation.BreakStatVariable"/> nought), and the
	/// mechanic works on, a sweep at a time, while the script still reads broken
	/// (<see cref="ParkRideOperation.BrokenVariable"/> above nought). Then the ride is repaired and opened
	/// (<see cref="ParkRideOperation.Repair"/>), the job forgotten and the decide made in the same turn.
	/// </summary>
	private void RepairOn( Staff staff, PeepWalk walk, int tick )
	{
		if ( _state != null && _state.TryObject( staff.ObjectToRepair, out var ride ) )
		{
			var script = ScriptFor?.Invoke( ride.ThingId );

			if ( ride.State == ParkRideChoice.StateRefusedOne )
			{
				script?.Set( ParkRideOperation.BreakStatVariable, 0 );

				if ( script != null && script[ParkRideOperation.BrokenVariable] > 0 )
					return;
			}

			ParkRideOperation.Repair( _state, script, ride.ThingId, TrackTypeOf?.Invoke( ride ) ?? 0 );
		}

		Log.Info( $"Staff: mechanic {staff.ThingId} finished repairing ride {staff.ObjectToRepair} on mGameTick {tick}" );

		staff.ObjectToRepair = 0;
		Decide( staff, walk, tick );
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
	/// <see cref="Staff.ToiletToClean"/>, a mechanic's <see cref="Staff.ObjectToRepair"/>) is forgotten there
	/// and then, member and stamp both.
	/// </summary>
	private int AssignedTo( ParkWorld.CatalogueObject thing, int tick )
	{
		if ( thing.AssignedStaff == 0 )
			return 0;

		if ( (uint)(thing.TimeMarkedForMaintenance + AssignmentKept) >= (uint)tick )
			return thing.AssignedStaff;

		if ( StaffById?.Invoke( thing.AssignedStaff ) is { } member
			&& ((member.Model == HandymanModel && member.ToiletToClean == thing.ThingId)
				|| (member.Model == MechanicModel && member.ObjectToRepair == thing.ThingId)) )
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
	/// One too tired to work takes neither the draw nor the look (<c>0x004d46e0</c>).
	/// </summary>
	/// <remarks>
	/// Staying and a walk found each take one more draw the original throws away (<c>0x004d4734</c>,
	/// <c>0x004d4718</c>). <b>Finding nowhere leaves the state as it was</b>, with its stamp: no setter is called,
	/// so the entertainer is asked again on the next sweep, and one who was performing takes another turn of
	/// work and its end again.
	/// </remarks>
	private void Entertain( Staff staff, PeepWalk walk, int tick, bool tooTired )
	{
		if ( !tooTired && _random.Next() % PerformShare == 0 && Perform( staff, walk, tick ) )
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
	/// The guard's choice: a walk unless <c>mGameTick</c>'s low two bits are nought (<c>0x004d655d</c>). Staying,
	/// or finding nowhere, sets idle, which stamps only from a walk.
	/// </summary>
	private void PatrolOrStay( Staff staff, PeepWalk walk, int tick )
	{
		var walks = (tick & (StayPutShare - 1)) != 0;
		var walking = walks && SetRandomDest( staff, walk );

		staff.SetActivity( walking ? StaffActivity.Walking : StaffActivity.Idle, tick );

		if ( !walking )
		{
			Log.Info( $"Staff: {staff.ThingId} stands on mGameTick {tick}, "
				+ (walks ? "finding nowhere to walk" : "its choice's low two bits nought") );
		}
	}

	/// <summary>
	/// The researcher's choice - <c>FUN_005029f0</c>'s idle and walking arms and <c>FUN_00502c70</c> after a
	/// rest, the same three tests each: a draw whose low two bits are not nought looks for somewhere to walk
	/// (<c>0x00502ba9</c>), and a nought, or nowhere found, researches where they stand
	/// (<see cref="Staff.StartResearching"/>). So a researcher never stands idle by this choice.
	/// </summary>
	/// <remarks>
	/// <b>One too tired to work does not research</b> (<c>FUN_00506680</c>, <c>0x00502bca</c>): the state is left
	/// as it was, with its stamp, so they are asked again on the next sweep.
	/// </remarks>
	private void Research( Staff staff, PeepWalk walk, int tick, bool tooTired )
	{
		var walks = (_random.Next() & (StayPutShare - 1)) != 0;

		if ( walks && SetRandomDest( staff, walk ) )
		{
			staff.SetActivity( StaffActivity.Walking, tick );

			return;
		}

		if ( tooTired )
		{
			Log.Info( $"Staff: {staff.ThingId} is left {staff.Activity} on mGameTick {tick}, too tired to research" );

			return;
		}

		staff.StartResearching( tick );

		Log.Info( $"Staff: {staff.ThingId} researches on mGameTick {tick}, "
			+ (walks ? "finding nowhere to walk" : "its choice's low two bits nought") );
	}

	/// <summary>
	/// A kind's idle <c>cat_staff</c> effect; its walking one is the id before it: handyman <c>0xa1</c>
	/// (<c>0x004d74fc</c>) and <c>0xa0</c>, mechanic <c>0xa3</c> and <c>0xa2</c>, entertainer <c>0xa5</c> and
	/// <c>0xa4</c>, guard <c>0xa7</c> and <c>0xa6</c>, researcher <c>0xa9</c> and <c>0xa8</c>.
	/// </summary>
	public static int IdleSoundOf( int model ) => model switch
	{
		HandymanModel => 0xa1,
		MechanicModel => 0xa3,
		EntertainerModel => 0xa5,
		GuardModel => 0xa7,
		ResearcherModel => 0xa9,
		_ => 0
	};

	/// <summary>The effect at a performance's end, <c>TADA.mp2</c> (<c>0x004d48a8</c>).</summary>
	public const int PerformanceEndSound = 0x87;

	/// <summary>The effect a researching turn draws for (<c>0x00502a73</c>); the shipped one holds only a blank sample.</summary>
	public const int ResearchingSound = 0x8a;

	/// <summary>A drawn sound plays when the draw's low four bits are nought (<c>TEST AL,0xf</c>, <c>0x004d74f2</c>).</summary>
	public const int SoundDrawMask = 0xf;

	/// <summary>
	/// The draw an idle, walking or researching turn opens with, and the effect on one in sixteen
	/// (<c>docs/exe/ride-operation.md</c>, "Drawn on the way"). The draw is taken whatever the effect.
	/// </summary>
	private void DrawForSound( Staff staff, int effect, int tick )
	{
		var draw = _random.Next();

		staff.Sounds = (staff.Sounds.Draws + 1, staff.Sounds.Played);

		if ( (draw & SoundDrawMask) == 0 && effect > 0 )
			PlaySound( staff, effect, tick );
	}

	/// <summary>A <c>cat_staff</c> effect at the member, counted and logged.</summary>
	private void PlaySound( Staff staff, int effect, int tick )
	{
		staff.Sounds = (staff.Sounds.Draws, staff.Sounds.Played + 1);

		Log.Info( $"Staff: {staff.ThingId} sounds cat_staff effect 0x{effect:x} on mGameTick {tick}" );
		Sound?.Invoke( staff, effect );
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
	/// the decompiler and is a number. The member is then idle at stamp nought and takes their kind's decide
	/// in the same sweep (<c>0x00506298</c>).
	/// </remarks>
	private void Rest( Staff staff, PeepWalk walk, int tick )
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

		Log.Info( $"Staff: {staff.ThingId} ends the rest on mGameTick {tick} and decides" );

		// The kind's decide in the same sweep (0x00506298), from idle at stamp nought.
		Decide( staff, walk, tick );
	}

	/// <summary>
	/// A member just hired: every kind's constructor ends in its decide, on the clock as it stands - the
	/// mechanic's <c>0x004d9fc4</c>, the handyman's <c>0x004d6c73</c>, the entertainer's <c>0x004d4430</c>, the
	/// guard's inline at <c>0x004d5e76</c> and the researcher's at <c>0x005026cb</c>.
	/// </summary>
	internal void Hired( Staff staff, PeepWalk walk, int tick )
	{
		ArgumentNullException.ThrowIfNull( staff );
		ArgumentNullException.ThrowIfNull( walk );

		Decide( staff, walk, tick );

		Log.Info( $"Staff: {staff.ThingId} decides at hire on mGameTick {tick}: {staff.Activity}, base speed {staff.BaseSpeed}" );
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

		if ( thingId != 0 && staff.Model == MechanicModel && staff.ObjectToRepair == thingId )
		{
			staff.ObjectToRepair = 0;
			staff.SetActivity( StaffActivity.Idle, tick );

			Log.Info( $"Staff: {staff.ThingId} dropped the job on ride {thingId}, now {staff.Activity}" );
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
