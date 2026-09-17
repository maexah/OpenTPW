namespace OpenTPW;

/// <summary>
/// One member of the park's staff as a simulation: what they are doing, how they feel about it, and the
/// patch of map they keep to.
///
/// <para>
/// <b>This is the staff answer to <see cref="Peep"/> and is deliberately a separate type.</b> A guest and a
/// member of staff share the person base - both walk, both carry a navigator, both wear a sprite - and
/// share nothing above it: a guest has needs, cash and an opinion of the admission fee, and a staff member
/// has a pay grade, a patrol area and a job. The original keeps them as separate classes over a common
/// base for the same reason, and folding them together here would mean a type where half the fields are
/// meaningless for half the instances.
/// </para>
/// <para>
/// <b>What is NOT here is the kind-specific half.</b> A handyman's target litter cell, a mechanic's object
/// to repair and a guard's perp are all saved and all decoded - see <c>ParkWorld.ReadStaff</c> - and none
/// of them is read, because the behaviour built on this is the part all five kinds share. See
/// <see cref="StaffBehaviour"/> for which arms that leaves out and why.
/// </para>
/// </summary>
public sealed class Staff
{
	/// <summary>The thing id this member of staff is, which is what the save's handles compare against.</summary>
	public int ThingId { get; }

	/// <summary>
	/// Which kind of staff they are, as the <b>thing model</b>: 4 mechanic, 5 handyman, 6 entertainer,
	/// 7 guard, 8 researcher.
	/// </summary>
	/// <remarks>
	/// <b>Two other numberings of the same five exist and neither is this one</b> - the sprite folder and
	/// the balance file's <c>PerTypeStaffConsts</c>. See <c>ParkWorld.StaffState.PayTypeOf</c>, which is the
	/// only sanctioned way to get from this to the balance file's index.
	/// </remarks>
	public int Model { get; }

	/// <summary>What they are doing - the saved <c>mState</c>. Written only by <see cref="SetActivity"/>.</summary>
	public StaffActivity Activity { get; private set; }

	/// <summary>
	/// Their pay grade, 0 to 4, which indexes every per-grade constant in the balance file: how long they
	/// idle, how fast they recover, how fast they tire and what they are paid.
	/// </summary>
	public int PayGrade { get; }

	/// <summary>How they feel about the job, 0 to 100.</summary>
	public float Happiness { get; internal set; }

	/// <summary>
	/// How rested they are, 0 to 100 - and the name runs the opposite way to what it measures, which is the
	/// original's own. It falls as they work and is recovered by resting, and "too tired" is
	/// <c>value &lt; AllStaffConstants.RestLevel</c>.
	/// </summary>
	public float Tiredness { get; internal set; }

	/// <summary>How many jobs they have finished - <c>mJobsDone</c>.</summary>
	public int JobsDone { get; internal set; }

	/// <summary>How far through their grade their training is, as a percentage.</summary>
	public int PercentageThroughGrade { get; }

	/// <summary>
	/// The thing id of the rest area they are walking to or sitting in, or nought for none. A handle rather
	/// than an index, compared with <c>==</c> as every other thing handle in the save is.
	/// </summary>
	public int RestArea { get; internal set; }

	/// <summary>
	/// When they last started standing about, read against the park's own clock.
	///
	/// <para>
	/// <b>It is stamped on entering <see cref="StaffActivity.Idle"/> only when they arrive there from
	/// walking</b>, and zeroed coming from anywhere else - which is the original's own shape and is why
	/// three of Lost Kingdom's five staff carry nought while the entertainer and guard carry 712 and 752
	/// against a park clock of 755.
	/// </para>
	/// </summary>
	public int TimeStartedIdling { get; internal set; }

	/// <summary>
	/// The corners of the rectangle this member of staff keeps to, as <b>packed cell ids</b> -
	/// <c>y * 128 + 1 + x</c>. Both nought means no area at all.
	/// </summary>
	public int PatrolBottomLeft { get; }

	/// <inheritdoc cref="PatrolBottomLeft"/>
	public int PatrolTopRight { get; }

	/// <summary>Where they are and where they are going - the same navigator every person carries.</summary>
	public PeepNavigator Navigator { get; }

	/// <summary>
	/// How fast they mean to walk, written into the thing's <c>+0xc2</c> before the walk runs. Staff take
	/// the unhurried speed everywhere the shared spine reaches; only a guard chasing somebody hurries, and
	/// that arm is not built.
	/// </summary>
	public int PurposeSpeed { get; internal set; }

	/// <summary>The animation this staff member's state has asked for, or nought for none.</summary>
	/// <remarks>
	/// Queued rather than applied, exactly as a guest's is - <c>FUN_004217f0</c> is a bare write of the
	/// person's own <c>mNextAnim</c>, and something else hands it to the sprite later. Recording it
	/// anywhere else is the mistake that left every guest striding on the spot at the gate.
	/// </remarks>
	public int NextAnimation { get; set; }

	/// <inheritdoc cref="Peep.NextInterval"/>
	public int NextInterval { get; set; }

	public Staff( int thingId, int model, ParkWorld.StaffState saved, ParkWorld.NavigatorState navigator )
	{
		Navigator = new PeepNavigator( navigator );
		ThingId = thingId;
		Model = model;
		Activity = (StaffActivity)saved.State;
		PayGrade = saved.PayGrade;
		Happiness = saved.Happiness;
		Tiredness = saved.Tiredness;
		JobsDone = saved.JobsDone;
		PercentageThroughGrade = saved.PercentageThroughGrade;
		RestArea = saved.RestArea;
		TimeStartedIdling = saved.TimeStartedIdling;
		PatrolBottomLeft = saved.PatrolBottomLeft;
		PatrolTopRight = saved.PatrolTopRight;
	}

	/// <summary>Whether this member of staff is penned into a patch of the map at all.</summary>
	public bool HasPatrolArea => PatrolBottomLeft != 0 || PatrolTopRight != 0;

	/// <summary>
	/// The patrol area as two cells rather than two packed ids - the original unpacks by subtracting one
	/// and splitting on the low seven bits, which is what makes these cell ids rather than indices.
	/// </summary>
	public (int X, int Y) PatrolFrom => ((PatrolBottomLeft - 1) & 0x7f, (PatrolBottomLeft - 1) >> 7);

	/// <inheritdoc cref="PatrolFrom"/>
	public (int X, int Y) PatrolTo => ((PatrolTopRight - 1) & 0x7f, (PatrolTopRight - 1) >> 7);

	/// <summary>
	/// Whether a cell is inside this staff member's patrol area - <c>FUN_00506ed0</c>. A staff member with
	/// no area at all is at home anywhere.
	/// </summary>
	public bool Patrols( int x, int y )
	{
		if ( !HasPatrolArea )
			return true;

		var (fromX, fromY) = PatrolFrom;
		var (toX, toY) = PatrolTo;

		return x >= fromX && x <= toX && y >= fromY && y <= toY;
	}

	/// <summary>
	/// Whether a staff member in this state should be given a turn of <see cref="PeepWalk"/>.
	///
	/// <para>
	/// Three of the eight shared states walk: going somewhere, going to a rest area, and going to the
	/// strike. The rest stand, sit or are being carried.
	/// </para>
	/// </summary>
	public static bool IsAWalkingState( StaffActivity activity ) => activity is
		StaffActivity.Walking or StaffActivity.GoingToRest or StaffActivity.GoingOnStrike;

	/// <summary>
	/// Moves this member of staff into a new state, doing what the original's <c>FUN_005054d0</c> does on
	/// the way in - which is more than assigning the field.
	/// </summary>
	/// <param name="tick">The park's own clock, which is what the idle stamp is a reading of.</param>
	public void SetActivity( StaffActivity next, int tick )
	{
		// <b>The stamp is taken only when they arrive at idling from a walk.</b> Coming from anywhere else
		// it is cleared, so that the idle countdown starts from nought rather than from a stale reading -
		// and that asymmetry is why three of the shipped park's staff carry no stamp at all.
		if ( next == StaffActivity.Idle )
			TimeStartedIdling = Activity == StaffActivity.Walking ? tick : 0;

		if ( next == StaffActivity.Waiting )
			TimeStartedIdling = tick;

		var wanted = AnimationFor( next );

		if ( wanted != 0 )
			NextAnimation = wanted;

		Activity = next;
	}

	/// <summary>
	/// Which animation a state queues as it is entered, by the original's own numbers.
	///
	/// <para>
	/// <b>These are not a guest's numbers and are deliberately left as numbers.</b> A guest's walk is 1 and
	/// their stand is 3; a staff member walking asks for 9, going to a rest area asks for 1, striking asks
	/// for 0x13 and waiting for 0x14. What those sets contain is a property of each kind's own sprite
	/// script, which nothing here reads yet, so naming them would be inventing meanings rather than
	/// recording the calls.
	/// </para>
	/// </summary>
	public static int AnimationFor( StaffActivity activity ) => activity switch
	{
		StaffActivity.Walking or StaffActivity.GoingOnStrike => 9,
		StaffActivity.GoingToRest => 1,
		StaffActivity.OnStrike => 0x13,
		StaffActivity.Waiting => 0x14,
		_ => 0
	};

	/// <summary>The range happiness and tiredness are held in, the same clamp a guest's needs take.</summary>
	public const float Most = 100f;

	/// <inheritdoc cref="Most"/>
	public const float Least = 0f;

	/// <summary>Moves one of the two stats and holds it in range - the clamp the original writes out inline.</summary>
	public static float Change( float stat, float by )
	{
		var moved = stat + by;

		return moved > Most ? Most : moved < Least ? Least : moved;
	}
}
