namespace OpenTPW;

/// <summary>
/// What a candidate is worth to a guest choosing where to go - the original's <c>FUN_004fcc30</c>, which
/// <c>FUN_004fcb10</c> asks of every object that passes <see cref="ParkRideChoice"/> and then takes the
/// best of. <c>docs/exe/ride-operation.md</c>, "What a thing is worth to a guest", is the decode, step by step.
///
/// <para>
/// <b>The same kind as the thing the guest left last is worth nought</b>, before anything else is worked out.
/// Otherwise it is a weighted mean of seven terms - distance, queue, excitement, thirst, hunger, toilet and
/// illness - each weighted by a <c>PeepInfo.DecisionVar…Weight</c> key, then multiplied for newness, for
/// shelter in the rain and for a golden-ticket ride or a dear one, and finally divided down by the guest's two
/// histories: the first match among the last four things they left, and every match among the last four they
/// turned away from.
/// </para>
/// <para>
/// <b>Two of the weights are not constants.</b> The queue term and its weight are both nought unless the
/// guest is within three cells of the object (the original tests the squared distance against 8), and the
/// excitement weight is nought unless the item declares an excitement level at all. So the divisor is a
/// sum of whichever weights actually apply, not a fixed total - which is why they are summed here rather
/// than precomputed.
/// </para>
/// <para>
/// <b>What a thing's excitement is</b> is <see cref="ExcitementOf"/>, the original's <c>FUN_004e0860</c>: a
/// sideshow's comes from its cost of goods, its price and its chance of winning; a ride's is its item's level
/// times its speed and its duration against its tier's starting ones. Its coaster, track-crowd and upgrade-tier
/// branches are counted, not built.
/// </para>
/// </summary>
public sealed class ParkRideScore
{
	/// <param name="balance">
	/// The park's balance stack, which holds every weight, every multiplier and each guest type's preferred
	/// excitement. Null takes <c>data/levels/Standard.sam</c>'s weights and multipliers, so a test can drive
	/// this without mounting a game, but prefers 50 for every type where the file gives 80, 65, 50, 35, 65, 80,
	/// 45 and 80: a park being played hands its balance in (<see cref="PeepBehaviour"/>).
	/// </param>
	public ParkRideScore( ParkBalance? balance = null )
	{
		DistanceWeight = balance?.Int( "PeepInfo.DecisionVarDistWeight", 1 ) ?? 1;
		QueueWeight = balance?.Int( "PeepInfo.DecisionVarQueueWeight", 1 ) ?? 1;
		ExcitementWeight = balance?.Int( "PeepInfo.DecisionVarExcitementWeight", 1 ) ?? 1;
		ThirstWeight = balance?.Int( "PeepInfo.DecisionVarThirstWeight", 2 ) ?? 2;
		HungerWeight = balance?.Int( "PeepInfo.DecisionVarHungerWeight", 2 ) ?? 2;
		ToiletWeight = balance?.Int( "PeepInfo.DecisionVarToiletWeight", 2 ) ?? 2;
		IllnessWeight = balance?.Int( "PeepInfo.DecisionVarIllnessWeight", 2 ) ?? 2;

		NewForDays = balance?.Int( "PeepInfo.DecisionVariable1", 7 ) ?? 7;
		NewRideMultiplier = balance?.Int( "PeepInfo.DecisionVariable2", 5 ) ?? 5;
		IndoorInRainMultiplier = balance?.Int( "PeepInfo.DecisionVariable3", 5 ) ?? 5;

		_preferredExcitement = new int[ParkWorld.GuestState.PersonTypes];

		for ( var kind = 0; kind < _preferredExcitement.Length; ++kind )
			_preferredExcitement[kind] = balance?.Int( $"PeepTypes[{kind}].PreferredExcitement", 50 ) ?? 50;
	}

	public int DistanceWeight { get; }

	public int QueueWeight { get; }

	public int ExcitementWeight { get; }

	public int ThirstWeight { get; }

	public int HungerWeight { get; }

	public int ToiletWeight { get; }

	public int IllnessWeight { get; }

	/// <summary>How many days an attraction counts as new for - <c>DecisionVariable1</c>, 7.</summary>
	public int NewForDays { get; }

	/// <summary>What a new attraction's score is multiplied by - <c>DecisionVariable2</c>, 5.</summary>
	public int NewRideMultiplier { get; }

	/// <summary>And what shelter is worth while it rains - <c>DecisionVariable3</c>, 5.</summary>
	public int IndoorInRainMultiplier { get; }

	private readonly int[] _preferredExcitement;

	/// <summary>What a guest of this kind likes - <c>PeepTypes[n].PreferredExcitement</c>.</summary>
	public int PreferredExcitementFor( int personType )
		=> _preferredExcitement[Math.Clamp( personType, 0, _preferredExcitement.Length - 1 )];

	/// <summary>What this decision reads of the guest: their kind, their needs and their two histories.</summary>
	/// <param name="Visits"><see cref="Peep.PreviousRides"/>, newest first; null is none.</param>
	/// <param name="Refusals"><see cref="Peep.PreviousTemporaryRides"/>, newest first; null is none.</param>
	/// <param name="LastVisitKind">
	/// The catalogue id of the thing <paramref name="Visits"/> names first, or nought when it names none or a thing
	/// no longer there - the original looks the handle up in the thing table (<c>0x7cfb90</c>). The chooser fills it.
	/// </param>
	public readonly record struct Wants( int PersonType, float Thirst, float Hunger, float Toilet, float Vomit,
		IReadOnlyList<int>? Visits = null, IReadOnlyList<int>? Refusals = null, int LastVisitKind = 0 );

	/// <summary>
	/// One thing being considered, and the facts about it that live outside its own record.
	/// </summary>
	/// <param name="DistanceSquared">Squared distance in cells between the guest and the object's entry cell.</param>
	/// <param name="QueueLength">
	/// How many are queueing, counted from the head up to and including the first guest no longer queueing -
	/// <c>FUN_004ddf50( 0 )</c>, <see cref="ParkState.QueueCount"/>.
	/// </param>
	/// <param name="EffectsNearby">
	/// The cell's own effects count, which <b>divides</b> the distance term when it is not nought - see
	/// <see cref="ParkWorld.MapCell.NearbyEffects"/>, which is read but unconfirmed.
	/// </param>
	/// <param name="DaysOld">
	/// How many whole days old the thing is on the park's calendar - <see cref="ParkState.AgeInDays"/>. Compared
	/// unsigned, so a stamp a day or more in the future is not new.
	/// </param>
	/// <param name="Raining">Whether drops are falling - the weather's <c>mCurrentDrops</c> above nought.</param>
	/// <param name="Excitement"><see cref="ExcitementOf"/> the thing.</param>
	/// <param name="QueueCells">
	/// The queue's walked cell count, <c>+0x40</c> - <see cref="ParkRideChoice.QueueCellsFor"/>. Nought is read as one.
	/// </param>
	public readonly record struct Candidate( ParkWorld.CatalogueObject Placed, ParkItemCatalogue.Item Item,
		int DistanceSquared, int QueueLength, int EffectsNearby, int DaysOld, bool Raining, int Excitement,
		int QueueCells = 0 );

	/// <summary>Within this squared distance the queue matters (<c>JG</c> past 8 at <c>0x004fce3e</c>); beyond it the term and its weight are nought.</summary>
	public const int QueueMattersWithin = 9;

	/// <summary>What the squared distance is scaled against before being taken from a hundred.</summary>
	public const int DistanceDivisor = 450;

	/// <summary>The most the excitement mismatch is allowed to count for.</summary>
	public const int ExcitementSpread = 50;

	/// <summary>
	/// What this candidate is worth. Nought means "not worth offering" - the chooser refuses anything
	/// scoring nine or less.
	/// </summary>
	public int Of( Wants wants, Candidate candidate )
	{
		var item = candidate.Item;

		// The same kind as the thing left last is worth nought, whichever one of that kind this is
		// (0x004fcd3f..0x004fcd97). So a guest who has just left a toilet scores every toilet nought, and reaches a
		// second one on the walk, through the minor decision and the saved major (PeepBehaviour.MinorDecision).
		if ( wants.LastVisitKind != 0 && wants.LastVisitKind == candidate.Placed.CatalogueId )
			return 0;

		// Distance. The squared distance scaled against 450, held at a hundred and taken from it - so a
		// candidate underfoot scores a hundred and one 450 squared-cells away scores nothing. The effects count
		// divides what is left, unsigned, which is the "nearby fireworks" branch.
		var distance = 100 - Math.Min( 100, (candidate.DistanceSquared * 100) / DistanceDivisor );

		if ( candidate.EffectsNearby != 0 )
			distance = (int)((uint)distance / (uint)candidate.EffectsNearby);

		// Queue. BOTH the term and its weight fall away when the guest is not close, which is what stops a
		// distant empty queue counting for anything. Unsigned, over the walked cells, nought read as one.
		var queueMatch = 0;
		var queueWeight = 0;

		if ( candidate.DistanceSquared < QueueMattersWithin )
		{
			var cells = candidate.QueueCells != 0 ? candidate.QueueCells : 1;

			queueMatch = 100 - (int)((uint)(candidate.QueueLength * 100)
				/ (uint)(cells * ParkRideChoice.QueueRoomPerCell));

			queueWeight = QueueWeight;
		}

		// Excitement, both sides a byte. The weight is nought where the item declares no excitement at all,
		// which is the original's own gate rather than a guard against missing data (0x004fcf06).
		var mismatch = Math.Min( Math.Abs( (PreferredExcitementFor( wants.PersonType ) & 0xff)
			- (candidate.Excitement & 0xff) ), ExcitementSpread );

		var excitementMatch = 100 - (2 * mismatch);
		var excitementWeight = (item.ExcitementLevel & 0xff) != 0 ? ExcitementWeight : 0;

		var thirstMatch = NeedMatch( wants.Thirst, item.ThirstEffect );
		var hungerMatch = NeedMatch( wants.Hunger, item.HungerEffect );

		// Relief. Both of these read the SAME flag in the original - the decompilation shows the toilet
		// bit tested for the illness term too - so it is reproduced that way rather than tidied into a
		// second flag the binary does not have.
		var toiletMatch = item.ProvidesRelief ? ReliefMatch( wants.Toilet ) : 0;
		var illnessMatch = item.ProvidesRelief ? ReliefMatch( wants.Vomit ) : 0;

		var total = (distance * DistanceWeight)
			+ (queueMatch * queueWeight)
			+ (excitementMatch * excitementWeight)
			+ (thirstMatch * ThirstWeight)
			+ (hungerMatch * HungerWeight)
			+ (toiletMatch * ToiletWeight)
			+ (illnessMatch * IllnessWeight);

		var weights = DistanceWeight + queueWeight + excitementWeight
			+ ThirstWeight + HungerWeight + ToiletWeight + IllnessWeight;

		if ( weights == 0 )
			return 0;

		// The mean is an unsigned divide (0x004fd22a).
		var score = (int)((uint)total / (uint)weights);

		// New for DecisionVariable1's days, compared unsigned (JA at 0x004fd24f); shelter while drops fall.
		if ( (uint)candidate.DaysOld <= (uint)NewForDays )
			score *= NewRideMultiplier;

		if ( candidate.Raining && item.IsIndoors )
			score *= IndoorInRainMultiplier;

		score = Priced( score, item );

		return Staled( score, candidate.Placed.ThingId, wants.Visits, wants.Refusals );
	}

	/// <summary>
	/// A golden-ticket ride, or a dear one, is worth more - <c>0x004fd2aa</c>..<c>0x004fd352</c>, whose log calls
	/// them "GT-only ride" and "expensive ride". A golden-ticket cost <c>g</c> above nought multiplies by
	/// <c>1 - (g + 1) × -0.1</c>; otherwise a purchase price (<c>Upgrades[0].CostOfUpgrade</c>, tier nought
	/// whatever the level) that is more than 3,000 multiplies by <c>1 - price / 3000 × -0.1</c>. Each is truncated.
	/// </summary>
	/// <remarks>
	/// The arithmetic is done at double precision, the precision the runtime starts the FPU at (<c>0x006804da</c>).
	/// Which precision is live in the original is not settled (<c>docs/exe/park-engine.md</c>, "Which rounding is
	/// live"), so this is an assumption. A golden-ticket cost of 3 on a score of 45 is where it shows: 62 here, 63 at
	/// single or extended precision.
	/// </remarks>
	public static int Priced( int score, ParkItemCatalogue.Item item )
	{
		if ( item.GoldenTicketCost > 0 )
			return (int)((1.0 - ((item.GoldenTicketCost + 1) * -0.1)) * score);

		if ( item.BuildPrice == 0 )
			return score;

		var dearness = item.BuildPrice * (double)ThreeThousandth;

		if ( !(dearness > 1.0) )
			return score;

		return (int)((1.0 - (dearness * (double)MinusATenth)) * score);
	}

	/// <summary>The float at <c>0x00700750</c>, one three-thousandth.</summary>
	public const float ThreeThousandth = 1f / 3000f;

	/// <summary>The float at <c>0x00700754</c>.</summary>
	public const float MinusATenth = -0.1f;

	/// <summary>
	/// Divides a score down for the guest's two histories (<c>0x004fd354</c>..<c>0x004fd496</c>): the FIRST of the
	/// last four visits naming the thing divides by 5, 4, 3 or 2, and EVERY one of the last four refusals naming it
	/// divides again, by 5, 4, 3 and 2 in turn. Through the chooser a visit in slot nought never reaches here: the
	/// same kind has already scored nought.
	/// </summary>
	public static int Staled( int score, int thingId, IReadOnlyList<int>? visits, IReadOnlyList<int>? refusals )
	{
		for ( var back = 0; visits != null && back < ParkWorld.GuestState.Remembered && back < visits.Count; ++back )
		{
			if ( visits[back] != thingId )
				continue;

			score /= 5 - back;
			break;
		}

		for ( var back = 0; refusals != null && back < ParkWorld.GuestState.Remembered && back < refusals.Count; ++back )
		{
			if ( refusals[back] == thingId )
				score /= 5 - back;
		}

		return score;
	}

	/// <summary>
	/// How exciting a thing is - <c>FUN_004e0860( object, 0 )</c>, its low byte, which the score, the arrival's
	/// refusal (<c>FUN_004fd4e0</c>) and the settle-up's excitement match (<c>FUN_004fdcc0</c>) read.
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	/// <item>No <c>UsageInfo.ExcitementLevel</c>: nought.</item>
	/// <item>A sideshow (<c>Info.WhichUIType</c> 2): <see cref="SideshowExcitement"/> of the thing's cost of goods,
	/// price and chance of winning - the object's <c>+0x188</c>, <c>+0x194</c> and <c>+0x190</c>. The price is the
	/// thing's own; the other two are its item's, as everywhere here (Q97).</item>
	/// <item>A coaster (track type 3): <c>trunc( 50 + f / 2 )</c> of what its track answers, or nought with no
	/// track (<c>0x004e05f8</c>..). Not built: counted, and scored as its level.</item>
	/// <item>Anything else: the level, times its speed and its duration each against its tier's starting ones,
	/// held between 0.75 and 1.25 (<see cref="RatioExcitement"/>). A thing with a track handle (a non-zero
	/// <c>Bumper.BumperType</c>) first takes 60% of the level plus its track's crowd, which is counted and left out.
	/// A thing upgraded past tier nought is counted and scored as its level: the catalogue reads tier nought only,
	/// and Lost Kingdom's save upgrades nothing.</item>
	/// </list>
	/// </remarks>
	public static int ExcitementOf( ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		var level = item.ExcitementLevel;

		if ( level == 0 )
			return 0;

		if ( item.UiType == ParkRideOperation.SideshowUiType )
			return SideshowExcitement( item.CostOfGoods, placed.PricePerUse, item.ChanceOfWinning );

		if ( item.TrackType == ItemDescriptionFile.CoasterTrack )
		{
			Unimplemented.Report( "RIDE_EXCITEMENT_COASTER_TRACK" );

			return level & 0xff;
		}

		if ( item.BumperType != 0 )
			Unimplemented.Report( "RIDE_EXCITEMENT_TRACK_CROWD" );

		if ( placed.UpgradeLevel != 0 )
		{
			Unimplemented.Report( "RIDE_EXCITEMENT_UPGRADE_TIER" );

			return level & 0xff;
		}

		return RatioExcitement( level, placed.OperatingSpeed, item.InitSpeed, placed.OperatingDuration,
			item.InitDuration ) & 0xff;
	}

	/// <summary>
	/// A sideshow's excitement - <c>FUN_004e0560</c>'s first branch (<c>0x004e058a</c>..<c>0x004e05d4</c>): the byte
	/// <c>20 - trunc( chance × √clamp( cost − price, 0, 100 ) × 0.1 × -0.8f )</c>. The Jungle Spray, 50 against a price
	/// of 20 at a chance of 25, is 30.
	/// </summary>
	public static int SideshowExcitement( int costOfGoods, int price, int chanceOfWinning )
	{
		var margin = Math.Clamp( costOfGoods - price, 0, 100 );
		var lift = (int)((chanceOfWinning & 0xff) * Math.Sqrt( margin ) * 0.1 * (double)MinusFourFifths);

		return (byte)(20 - lift);
	}

	/// <summary>The float at <c>0x007005b0</c>.</summary>
	public const float MinusFourFifths = -0.8f;

	/// <summary>
	/// A ride's excitement at its settings - the tail of <c>FUN_004e0560</c> (<c>0x004e07be</c>..): the level times
	/// the speed's ratio to its starting speed and the duration's to its starting duration, each held between 0.75
	/// and 1.25 (the doubles at <c>0x007005b8</c> and <c>0x007005c0</c>), with the FPU's own float stores between.
	/// A ratio that is not a number is held at 0.75, as the compare's unordered flags take it.
	/// </summary>
	public static int RatioExcitement( int level, int speed, int startingSpeed, int duration, int startingDuration )
	{
		var bySpeed = (float)Held( (double)speed / startingSpeed );
		var byDuration = Held( (float)((double)duration / startingDuration) );
		var both = (float)(byDuration * bySpeed);

		return (int)(level * (double)both);
	}

	private static double Held( double ratio )
		=> !(ratio >= 0.75) ? 0.75 : ratio > 1.25 ? 1.25 : ratio;

	/// <summary>
	/// A need against what using this would do for it - the eleven by eleven table at <c>0x0075d0f8</c>,
	/// indexed by the need and the effect each divided by ten. It rises to a hundred where a large need
	/// meets a large effect, and is nought wherever either is small.
	/// </summary>
	public static int NeedMatch( float need, int effect )
		=> NeedTable[(Math.Clamp( (int)need, 0, 100 ) / 10) + ((Math.Clamp( effect, 0, 100 ) / 10) * 11)];

	/// <summary>
	/// How much a guest wants relief - the twenty-one entry table at <c>0x0075d178</c>, indexed by
	/// <c>(need + 4) / 5</c>. It is flat until the need passes forty and then climbs steeply.
	/// </summary>
	public static int ReliefMatch( float need )
		=> ReliefTable[Math.Clamp( ((int)need + 4) / 5, 0, ReliefTable.Length - 1 )];

	/// <summary>The eleven by eleven need table, read out of the image at <c>0x0075d0f8</c>.</summary>
	private static readonly int[] NeedTable =
	[
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 1, 2, 5, 7, 11, 15, 20, 25, 31,
		0, 0, 1, 4, 7, 11, 16, 21, 28, 36, 44,
		0, 0, 2, 4, 8, 13, 19, 26, 35, 44, 54,
		0, 0, 2, 5, 10, 15, 22, 30, 40, 51, 63,
		0, 0, 2, 6, 11, 17, 25, 34, 45, 57, 70,
		0, 0, 3, 6, 12, 19, 27, 37, 49, 62, 77,
		0, 0, 3, 7, 13, 20, 30, 40, 53, 67, 83,
		0, 0, 3, 8, 14, 22, 32, 43, 57, 72, 89,
		0, 0, 3, 8, 15, 23, 34, 46, 60, 76, 94,
		0, 1, 4, 9, 16, 25, 36, 49, 64, 81, 100
	];

	/// <summary>The twenty-one entry relief table, read out of the image at <c>0x0075d178</c>.</summary>
	private static readonly int[] ReliefTable =
	[
		0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 5, 10, 20, 30, 40, 50, 60, 75, 90, 100
	];
}
