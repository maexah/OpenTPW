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
/// <b>Of the kind-specific half the handyman's toilet job is here</b> (<see cref="ToiletToClean"/>,
/// <see cref="TimeStartedCleaning"/>)<b>, the entertainer's performance</b>
/// (<see cref="TimeStartedEntertaining"/>) <b>and the researcher's research</b>
/// (<see cref="TimeStartedResearching"/>). A handyman's target litter cell, a mechanic's object to repair and a
/// guard's perp are all saved and all decoded - see <c>ParkWorld.ReadStaff</c> - and none of them is read. See
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

	/// <summary>
	/// Their name, as text - the saved <c>mName</c>, which a hire copies from the candidate. No new candidate is
	/// given a name a member of staff in the park has (<c>FUN_005083f0</c>).
	/// </summary>
	public string Name { get; }

	/// <summary>What they are doing - the saved <c>mState</c>. Written only by <see cref="SetActivity"/>.</summary>
	public StaffActivity Activity { get; private set; }

	/// <summary>
	/// Their pay grade, 0 to 4, which indexes every per-grade constant in the balance file: how long they
	/// idle, how fast they recover, how fast they tire and what they are paid. Only the month's training
	/// raises it (<see cref="ParkPeople.TrainTheStaff"/>).
	/// </summary>
	public int PayGrade { get; internal set; }

	/// <summary>How they feel about the job, 0 to 100.</summary>
	public float Happiness { get; internal set; }

	/// <summary>
	/// How rested they are, 0 to 100 - and the name runs the opposite way to what it measures, which is the
	/// original's own. It falls as they work and is recovered by resting. Tired is its truncated byte at or
	/// under <c>AllStaffConstants.RestLevel</c>; too tired to work is that byte under it.
	/// </summary>
	public float Tiredness { get; internal set; }

	/// <summary>What they last thought, and the bubble over them.</summary>
	public Thoughts Thoughts { get; } = new();

	/// <summary>How many jobs they have finished - <c>mJobsDone</c>.</summary>
	public int JobsDone { get; internal set; }

	/// <summary>
	/// How many draws they have taken for a <c>cat_staff</c> sound, and how many sounds they have started, the
	/// undrawn ones among them - the <c>staff</c> census's two counts, kept by no save.
	/// </summary>
	public (int Draws, int Played) Sounds { get; internal set; }

	/// <summary>
	/// How far through their grade their training is, as a percentage - the one-byte <c>mPercentageThroughGrade</c>
	/// (<c>+0x1e8</c>). The month's training adds to it and takes 100 off at a promotion.
	/// </summary>
	public int PercentageThroughGrade { get; internal set; }

	/// <summary>
	/// The thing id of the rest area they are walking to or sitting in, or nought for none. A handle rather
	/// than an index, compared with <c>==</c> as every other thing handle in the save is.
	/// </summary>
	public int RestArea { get; internal set; }

	/// <summary>
	/// The toilet a handyman is walking to or cleaning, a thing id, or nought - <c>mToiletToClean</c>
	/// (<c>+0x21a</c>). His decide writes it every time it looks, nought when it finds none.
	/// </summary>
	public int ToiletToClean { get; internal set; }

	/// <summary>
	/// The park clock when a handyman began cleaning - <c>mTimeStartedCleaning</c> (<c>+0x214</c>), stamped on
	/// entering <see cref="StaffActivity.Cleaning"/>.
	/// </summary>
	public int TimeStartedCleaning { get; internal set; }

	/// <summary>
	/// The object a mechanic's search last found, a thing id, or nought - <c>mObjectToRepair</c>
	/// (<c>+0x218</c>). His decide writes it every time it looks, and it is kept where no route reaches the ride.
	/// </summary>
	public int ObjectToRepair { get; internal set; }

	/// <summary>
	/// How many more sweeps a mechanic's repair takes - <c>mDurationOfRepair</c> (<c>+0x214</c>), a count down
	/// set by <see cref="StartRepairing"/>.
	/// </summary>
	public int DurationOfRepair { get; internal set; }

	/// <summary>
	/// The park clock when an entertainer began performing - <c>mTimeStartedEntertaining</c>, the same
	/// <c>+0x214</c>, stamped by <see cref="StartPerforming"/>.
	/// </summary>
	public int TimeStartedEntertaining { get; internal set; }

	/// <summary>
	/// The park clock when a researcher began researching - <c>mTimeStartedResearching</c>, the same
	/// <c>+0x214</c>, stamped by <see cref="StartResearching"/> and again at the end of a spell that finds
	/// nowhere to walk.
	/// </summary>
	public int TimeStartedResearching { get; internal set; }

	/// <summary>
	/// The park's calendar as they were made - <c>mTimeHired</c>, <c>+0x1f0</c>, a <c>FILETIME</c>: the staff
	/// constructor stamps it (<c>FUN_00504b90</c>, <c>0x00504bb8</c>) and a load reads the file's over it. The staff
	/// window's days employed are measured from it (<c>FUN_00505b70</c>), which nothing here shows yet.
	/// </summary>
	public long TimeHired { get; }

	/// <summary>
	/// The dword at <c>+0x188</c>: set as a member sets off for the picket (<c>0x00506a89</c>) and cleared as that
	/// walk ends (<c>FUN_005056e0</c>). The <c>staff</c> census prints it; what reads it in the original is not traced.
	/// </summary>
	public bool SettingOffForTheStrike { get; internal set; }

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
	/// The hurry, <c>mPurposeSpeed</c> at <c>+0xc2</c>: nought on going idle and on strike (<c>FUN_005054d0</c>
	/// cases 0 and 5), the hurried 25 on setting off for the strike (case 4) and for a toilet
	/// (<c>FUN_004d7330</c>), and left as it was by every other state. One of the three words
	/// <see cref="Pace"/> sums.
	/// </summary>
	public int PurposeSpeed { get; internal set; }

	/// <summary>
	/// Their walking speed in hundredths by how rested they are - <c>mBaseSpeed</c>, the word at <c>+0xc0</c>:
	/// 60 at hire below grade 3 and for every entertainer, 100 otherwise (<c>FUN_00504b90</c>,
	/// <c>0x004d43fa</c>), and one of <see cref="Peep.BaseSpeeds"/> by fifths of the rest byte on every decide
	/// that finds them not tired (<see cref="StaffBehaviour"/>, <c>FUN_00506a40</c>).
	/// </summary>
	public int BaseSpeed { get; internal set; }

	/// <summary><c>mAdjustorSpeed</c>, the word at <c>+0xc4</c>: read from the save, and nothing gives staff any.</summary>
	public int AdjustorSpeed { get; internal set; }

	/// <summary>The speed the walk was last eased to - <c>mPreviousSpeed</c>, the float at <c>+0xc8</c>.</summary>
	public float PreviousSpeed { get; private set; }

	/// <summary>
	/// Whether <see cref="Pace"/> eases this member's walking speed: everyone read from a save or hired. One a
	/// test builds from a bare record keeps the navigator's speed as it was given.
	/// </summary>
	public bool Paced { get; }

	/// <summary>
	/// Eases their walking speed and hands it to the walk - <c>FUN_004fa870</c>, the first call of the shared
	/// staff tick <c>FUN_00505490</c> every sweep, whatever they are doing (<see cref="Peep.Ease"/>).
	/// </summary>
	public void Pace()
	{
		if ( !Paced )
			return;

		var sum = (PurposeSpeed & 0xffff) + (BaseSpeed & 0xffff) + (AdjustorSpeed & 0xffff);

		PreviousSpeed = Peep.Ease( sum, PreviousSpeed, Navigator );
		AdjustorSpeed = Peep.Fade( AdjustorSpeed );
	}

	/// <summary>The base speed a member is hired with (<c>FUN_00504b90</c>; an entertainer's at <c>0x004d43fa</c>).</summary>
	public static int HiredBaseSpeed( int model, int grade )
		=> model == EntertainerModel || grade < 3 ? Peep.BaseSpeeds[0] : Peep.BaseSpeeds[2];

	/// <summary>The entertainer's thing model.</summary>
	private const int EntertainerModel = 6;

	/// <summary>The animation this staff member's state has asked for, or nought for none.</summary>
	/// <remarks>
	/// Queued rather than applied, exactly as a guest's is - <c>FUN_004217f0</c> is a bare write of the
	/// person's own <c>mNextAnim</c>, and something else hands it to the sprite later.
	/// </remarks>
	public int NextAnimation { get; set; }

	/// <inheritdoc cref="Peep.NextInterval"/>
	public int NextInterval { get; set; }

	/// <summary>
	/// The packed id of the cell they were made on and, for an entertainer and a guard, of the cell their region
	/// effect stands round: the person base's <c>mLastRecordedMapId</c>, <c>+0xcc</c>, which only those two kinds'
	/// pre-steps move (<c>FUN_004f9460</c>; <see cref="ParkPeople.MoveEffect"/>). Nought is no cell.
	/// </summary>
	public int RecordedCell { get; set; }

	public Staff( int thingId, int model, ParkWorld.StaffState saved, ParkWorld.NavigatorState navigator,
		ParkWorld.PaceState? pace = null, int recordedCell = 0 )
	{
		Navigator = new PeepNavigator( navigator );
		RecordedCell = recordedCell;

		if ( pace is { } speeds )
		{
			Paced = true;
			AdjustorSpeed = speeds.AdjustorSpeed;
			BaseSpeed = speeds.BaseSpeed;
			PreviousSpeed = speeds.PreviousSpeed;
			PurposeSpeed = speeds.PurposeSpeed;
		}

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
		ToiletToClean = saved.ToiletToClean;
		TimeStartedCleaning = saved.TimeStartedCleaning;
		TimeStartedEntertaining = saved.TimeStartedEntertaining;
		TimeStartedResearching = saved.TimeStartedResearching;
		ObjectToRepair = saved.ObjectToRepair;
		DurationOfRepair = saved.DurationOfRepair;
		TimeHired = saved.TimeHired;

		// The bubble showing when the park was saved is a slot of the sprite table (mThoughtScript), which ParkPeople joins.
		Thoughts.Restore( saved.LastThought, saved.TimeBubbleShown );
		PatrolBottomLeft = saved.PatrolBottomLeft;
		PatrolTopRight = saved.PatrolTopRight;
		Name = saved.Name;
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
	/// no area at all is at home anywhere here; the original's test has no case for it and puts them
	/// outside every cell, a deviation. Every member Lost Kingdom's save holds carries one; a member hired here
	/// carries none (<c>ParkPeople.Hire</c>), and whether the original's hire sets one is not decoded.
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
	/// strike; and a handyman's walk to a toilet. The rest stand, sit, work or are being carried.
	/// </para>
	/// </summary>
	public static bool IsAWalkingState( StaffActivity activity ) => activity is
		StaffActivity.Walking or StaffActivity.GoingToRest or StaffActivity.GoingOnStrike
		or StaffActivity.GoingToLoo or StaffActivity.GoingToRide;

	/// <summary>
	/// Moves this member of staff into a new state, doing what the original's <c>FUN_005054d0</c> does on
	/// the way in - which is more than assigning the field.
	/// </summary>
	/// <param name="tick">The park's own clock, which is what the idle stamp is a reading of.</param>
	public void SetActivity( StaffActivity next, int tick )
	{
		// <b>The stamp is taken only when they arrive at idling from a walk.</b> Coming from anywhere else
		// it is cleared to nought, so the idle wait is over on the next sweep (0x00505542) - and that
		// asymmetry is why three of the shipped park's staff carry no stamp at all.
		if ( next == StaffActivity.Idle )
			TimeStartedIdling = Activity == StaffActivity.Walking ? tick : 0;

		// The hurry: off on going idle and on strike, on for the walk to the strike (0x00505555, 0x00505590,
		// 0x0050556c).
		if ( next is StaffActivity.Idle or StaffActivity.OnStrike )
			PurposeSpeed = 0;

		if ( next == StaffActivity.GoingOnStrike )
			PurposeSpeed = HurryingSpeed;

		if ( next == StaffActivity.Waiting )
			TimeStartedIdling = tick;

		// The handyman's own setter, FUN_004d7330: the walk to a toilet takes the hurry speed (0x0075c7f2),
		// and cleaning stamps the clock.
		if ( next == StaffActivity.GoingToLoo )
			PurposeSpeed = HurryingSpeed;

		if ( next == StaffActivity.Cleaning )
			TimeStartedCleaning = tick;

		var wanted = AnimationFor( next );

		if ( wanted != 0 )
			NextAnimation = wanted;

		Activity = next;
	}

	/// <summary>
	/// Starts a mechanic's repair - <c>FUN_004da370</c>'s <c>0xd</c> arm: animation <c>0x12</c> queued, the count
	/// down and the state. The shared setter does not run, so the idle stamp and the hurry are left as they were.
	/// </summary>
	/// <param name="duration">How many sweeps the repair takes after this one.</param>
	internal void StartRepairing( int duration )
	{
		NextAnimation = RepairingAnimation;
		DurationOfRepair = duration;
		Activity = StaffActivity.Repairing;
	}

	/// <summary>The animation a repairing mechanic is put on, <c>0x12</c> (<c>0x004da399</c>).</summary>
	public const int RepairingAnimation = 0x12;

	/// <summary>
	/// Starts an entertainer's performance - the three inline writes at the end of <c>FUN_004d46d0</c>'s look:
	/// the animation queued, the state <c>0xe</c> and the stamp. No setter runs, so the idle stamp is left as
	/// it was.
	/// </summary>
	/// <param name="animation">The animation to queue, or nought for none.</param>
	internal void StartPerforming( int animation, int tick )
	{
		if ( animation != 0 )
			NextAnimation = animation;

		Activity = StaffActivity.Performing;
		TimeStartedEntertaining = tick;
	}

	/// <summary>
	/// Starts a researcher's research - the three writes of <c>FUN_00502c20</c>'s <c>0xf</c> arm, which the
	/// decide after a walk and after a rest makes inline (<c>0x00502b17</c>, <c>0x00502cc7</c>): the stamp,
	/// animation 10 queued and the state. The shared setter does not run, so the idle stamp is left as it was.
	/// </summary>
	internal void StartResearching( int tick )
	{
		TimeStartedResearching = tick;
		NextAnimation = ResearchingAnimation;
		Activity = StaffActivity.Researching;
	}

	/// <summary>The animation a researcher researching is put on - the 10 of <c>0x00502c43</c>.</summary>
	public const int ResearchingAnimation = 10;

	/// <summary>
	/// Which animation a state queues as it is entered, by the original's own numbers.
	///
	/// <para>
	/// <b>These are not a guest's numbers and are deliberately left as numbers.</b> A guest's walk is 1 and
	/// their stand is 3; a staff member walking asks for 9, going to a rest area asks for 1, striking asks
	/// for 0x13 and waiting for 0x14; a handyman walking to a toilet asks for 9 and cleaning for 0x11
	/// (<c>FUN_004d7330</c>). Which script each one runs is <see cref="SpriteScript"/>'s table, the
	/// one a guest's animations use too; what the pictures show is up to each kind's own bank, so naming
	/// them would be inventing meanings rather than recording the calls.
	/// </para>
	/// </summary>
	public static int AnimationFor( StaffActivity activity ) => activity switch
	{
		StaffActivity.Walking or StaffActivity.GoingOnStrike or StaffActivity.GoingToLoo
			or StaffActivity.GoingToRide => 9,
		StaffActivity.Cleaning => 0x11,
		StaffActivity.GoingToRest => 1,
		StaffActivity.OnStrike => 0x13,
		StaffActivity.Waiting => 0x14,
		_ => 0
	};

	/// <summary>The hurry speed, 25 - the word at <c>0x0075c7f2</c>.</summary>
	public const int HurryingSpeed = 25;

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
