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
/// <c>mState</c>, answering states 0 to 7 through the same three handlers - whose own diagnostics call
/// them <c>CStaff::</c>. So this is one machine with five extensions, not five machines, and the shared
/// part moves every kind.
/// </para>
/// <para>
/// <b>What is built.</b> All eight shared states, and the decide arm of the two kinds whose decide arm is
/// itself shared: a guard and a researcher roll three times in four for somewhere to walk and take it.
/// That is what makes Lost Kingdom's guard and researcher patrol.
/// </para>
/// <para>
/// <b>What is deliberately not built, each for a named reason.</b> A handyman, a mechanic and an
/// entertainer finish a walk by jumping into a work-finding function of their own
/// (<c>FUN_004d7100</c>, <c>FUN_004da5b0</c>, <c>FUN_004d46d0</c>), and those want litter on map cells, a
/// broken ride and guests close enough to entertain - none of which this project has. Those three
/// therefore finish the walk the save left them on and then stand, which is honest rather than invented.
/// The strike arms are absent for a different reason: reaching them means asking the staff union's thing
/// what its script says, the same question <c>ParkRides.GateStatus</c> answers for the gate, and nothing
/// binds a script to the union yet.
/// </para>
/// <para>
/// <b>Rest areas ARE built now, and this said they were absent because the flag naming one was unread.</b>
/// That flag is read - bit 1 of a catalogue object's <c>mFlags</c> - so a tired staff member does what
/// <c>FUN_00506a40</c> does: looks for the nearest object flagged as a rest area and walks to it. The
/// "couldn't find a rest area" path is still here, because the original still takes it when there is none
/// in reach.
/// </para>
/// </summary>
public sealed class StaffBehaviour
{
	private readonly Random _random;

	/// <summary>
	/// The park as it is being played, for the one question this needs of it: which objects standing now
	/// are rest areas, and where each one wants to be approached from. Null leaves a staff member unable to
	/// find one, which is the original's own "couldn't find a rest area" path rather than a failure.
	/// </summary>
	private readonly ParkState? _state;

	private readonly int[] _idleDuration = new int[ParkWorld.StaffState.PayGrades];
	private readonly float[] _recuperation = new float[ParkWorld.StaffState.PayGrades];
	private readonly float[] _happinessRecuperation = new float[ParkWorld.StaffState.PayGrades];

	/// <param name="balance">
	/// The park's balance stack, which is where every staff constant lives. Null leaves the fallbacks in
	/// place - the global file's own values - so that a test can drive this without mounting a game.
	/// </param>
	/// <param name="random">The rolls this makes. Taken so a test can seed them; the game does not.</param>
	/// <param name="state">
	/// The park these staff are in, for finding a rest area. Null leaves them unable to find one - see the
	/// field's own remarks.
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

	/// <summary>How many times the patrol roll tries for a cell before giving up - <c>FUN_00506f30</c>.</summary>
	public const int PatrolTries = 30;

	/// <summary>
	/// One turn in four is the chance a guard or researcher stays put rather than walking somewhere:
	/// the original takes the walk when <c>roll &amp; 3</c> is <b>not</b> nought.
	/// </summary>
	public const int StayPutShare = 4;

	/// <summary>How many idle durations the waiting state waits - three.</summary>
	public const int WaitingIsIdleTimes = 3;

	/// <summary>
	/// The four sides in the order the original tests them, which is the same order and the same reason as
	/// <see cref="PeepBehaviour"/>'s: the connection bits run 0x10, 0x04, 0x01, 0x40.
	/// </summary>
	private static readonly StepDirection[] SlotOrder =
		[StepDirection.North, StepDirection.West, StepDirection.South, StepDirection.East];

	/// <summary>One turn of one staff member's behaviour.</summary>
	/// <param name="tick">The park's own clock, which the idle stamps are readings of.</param>
	public void Step( Staff staff, PeepWalk walk, SpriteScript? playing, int tick )
	{
		ArgumentNullException.ThrowIfNull( staff );
		ArgumentNullException.ThrowIfNull( walk );

		// <b>A stamp that reads ahead of the clock is stale, and the original says so itself.</b>
		// FUN_004f9490 opens by zeroing mStrandedTime whenever it is greater than the current time, which
		// is the same situation every staff stamp is in the moment a park is loaded: they were taken
		// against the original's own mGameTick, which Lost Kingdom's save left at 755, and our clock starts
		// again at nought.
		//
		// <b>Without this the whole feature is inert and looks fine.</b> The guard is saved idle with a
		// stamp of 752, so "have I idled long enough" stayed false for the first 752 ticks of every
		// session - he stood exactly where the file left him while every state in this class was
		// reachable, correct and never reached. Found by the test that asserts position rather than state.
		if ( staff.TimeStartedIdling > tick )
			staff.TimeStartedIdling = 0;

		switch ( staff.Activity )
		{
			// Standing about. They wait out their grade's idle duration and then look for something to do.
			case StaffActivity.Idle:
				if ( tick <= staff.TimeStartedIdling + IdleDurationAt( staff.PayGrade ) )
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

			// Sitting down recovering. Both stats climb by the grade's own rates until one of them is full.
			case StaffActivity.Resting:
				Rest( staff, tick );

				break;

			// Walking to the picket. Arriving and giving up are treated alike, as they are for a guest
			// shuffling up a queue: somebody who cannot reach the picket is still on strike.
			case StaffActivity.GoingOnStrike:
				if ( Walked( staff, walk, playing ) != WalkVerdict.Walking )
					staff.SetActivity( StaffActivity.OnStrike, tick );

				break;

			// Waiting out a spell three times as long as an ordinary idle.
			case StaffActivity.Waiting:
				if ( tick - staff.TimeStartedIdling > IdleDurationAt( staff.PayGrade ) * WaitingIsIdleTimes )
					staff.SetActivity( StaffActivity.Idle, tick );

				break;

			// On strike and being carried both do nothing here. The strike needs the union's script state,
			// and the original's own case 7 has an empty body.
			default:
				break;
		}
	}

	/// <summary>
	/// What a staff member does when they have finished a walk or run out of idling.
	///
	/// <para>
	/// <b>Only a guard and a researcher get past the first line, and that is the original's shape rather
	/// than a limit of this build.</b> The other three kinds jump into a work-finding function of their
	/// own here; see the class remarks for what each of those wants.
	/// </para>
	/// </summary>
	private void Decide( Staff staff, PeepWalk walk, int tick )
	{
		if ( staff.Model is not (GuardModel or ResearcherModel) )
		{
			// They have arrived somewhere and have no work to look for, so they stand. Going to Idle is
			// what stamps the clock, which is what stops this being asked again every turn.
			staff.SetActivity( StaffActivity.Idle, tick );

			return;
		}

		// Too tired to carry on: find the nearest rest area and set off for it - the tired branch of
		// FUN_00506a40, which asks FUN_00506910 for the nearest object flagged as one.
		//
		// FAILING TO FIND ONE AND FAILING TO REACH IT ARE THE SAME PATH IN THE ORIGINAL, and that is worth
		// not tidying into two: both fall through to the same "Staff member couldn't find a rest area"
		// line and the same one-in-sixteen loss of heart. GoAndRest returning false covers both.
		//
		// The original also queues animation 0x14 on the way into this branch, before it knows whether it
		// will find anything. Here the animation follows from the activity - see Staff.AnimationFor - so
		// the queue is left to SetActivity rather than written twice.
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

		// Three turns in four they walk somewhere; the fourth they stand and are asked again.
		var walks = (_random.Next() & (StayPutShare - 1)) != 0;

		staff.SetActivity( walks && SetRandomDest( staff, walk ) ? StaffActivity.Walking : StaffActivity.Idle,
			tick );
	}

	/// <summary>The thing models whose decide arm is answered inline by the shared switch.</summary>
	private const int GuardModel = 7;

	/// <inheritdoc cref="GuardModel"/>
	private const int ResearcherModel = 8;

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
	private static WalkVerdict Walked( Staff staff, PeepWalk walk, SpriteScript? playing )
	{
		if ( !walk.HasRoute && (staff.Navigator.CannotReach || !walk.PlanRoute()) )
			return WalkVerdict.CannotReach;

		var verdict = walk.Step();

		if ( verdict != WalkVerdict.Walking )
			return verdict;

		var wanted = Staff.AnimationFor( StaffActivity.Walking );

		if ( playing != null && !playing.IsOn( wanted ) )
			staff.NextAnimation = wanted;

		staff.NextInterval = SpriteScript.IntervalFor( walk.LastStep.X, walk.LastStep.Y, hurrying: false );

		return verdict;
	}

	/// <summary>
	/// Sends a staff member somewhere - the staff half of <c>FUN_004f9490</c>, which is a different
	/// function from the guest half and sits in front of it.
	///
	/// <para>
	/// <b>Standing outside your patrol area is answered before anything else</b>: a staff member who has
	/// wandered out of their patch heads straight back into it rather than picking a neighbour. Inside it,
	/// the ordinary neighbour pick runs with any candidate outside the area struck out, and if that leaves
	/// nothing the patrol roll answers again.
	/// </para>
	/// <para>
	/// <b>A guest in the same position gets a "stranded" stamp and a thought bubble instead</b> - the
	/// <c>mStrandedTime</c> the person base names at <c>+0x198</c>. Staff never take that path; they take
	/// the patrol roll, which is why this is not simply the guest's routine with an extra test.
	/// </para>
	/// </summary>
	private bool SetRandomDest( Staff staff, PeepWalk walk )
	{
		var (x, y) = walk.Position.Cell;

		if ( !staff.Patrols( x, y ) )
			return PatrolRoll( staff, walk );

		var candidates = new (int X, int Y)?[SlotOrder.Length];
		var found = 0;

		for ( var slot = 0; slot < SlotOrder.Length; ++slot )
		{
			if ( walk.Blocked( x, y, SlotOrder[slot] ) )
				continue;

			var cell = MapStep.Beyond( x, y, SlotOrder[slot] );

			// The strike-out the guest version has no idea about.
			if ( !staff.Patrols( cell.X, cell.Y ) )
				continue;

			candidates[slot] = cell;
			++found;
		}

		if ( found == 0 )
			return PatrolRoll( staff, walk );

		var first = _random.Next() & (SlotOrder.Length - 1);

		for ( var step = 0; step < SlotOrder.Length; ++step )
		{
			var slot = (first + step) & (SlotOrder.Length - 1);

			if ( candidates[slot] is not { } cell )
				continue;

			// A random point inside the cell rather than its centre, the same clamped roll a wandering
			// guest takes - this tail is shared between the two halves of the original's function.
			staff.Navigator.Target = new FixedVector( SomewhereIn( cell.X ), SomewhereIn( cell.Y ) );

			return walk.PlanRoute();
		}

		return PatrolRoll( staff, walk );
	}

	/// <summary>
	/// Thirty tries at a cell inside the patrol area - <c>FUN_00506f30</c>, whose own log line when it runs
	/// out is "Could not find or reach a destination".
	/// </summary>
	/// <remarks>
	/// <b>This one aims at the cell CENTRE</b>, not at a random point inside it: it goes through the
	/// ordinary destination setter rather than through the tail that jitters. The two are a few lines apart
	/// in the original and do different things, which is worth not tidying.
	/// <para>
	/// One predicate of the original's is left out: between the bounds check and the route it asks
	/// something of the map cell that this project has not established. A cell that fails it would almost
	/// certainly fail to produce a route either, which is the test that follows here.
	/// </para>
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

			staff.Navigator.Target = new FixedVector(
				PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );

			if ( walk.PlanRoute() )
				return true;
		}

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
	/// <b>Not built, and each has nothing here to act on.</b> A mechanic's ride job (<c>+0x218</c>) and a
	/// handyman's toilet job (<c>+0x21a</c>) are dropped by their own arms first; no staff member holds a
	/// job on a thing in this build. <c>FUN_00506d10</c> also takes one off the rest area's
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
