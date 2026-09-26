namespace OpenTPW;

/// <summary>
/// What a candidate is worth to a guest choosing where to go - the original's <c>FUN_004fcc30</c>, which
/// <c>FUN_004fcb10</c> asks of every object that passes <see cref="ParkRideChoice"/> and then takes the
/// best of.
///
/// <para>
/// <b>It is a weighted mean of seven terms</b> - distance, queue, excitement, thirst, hunger, toilet and
/// illness - each weighted by a <c>PeepInfo.DecisionVar…Weight</c> key, then multiplied for newness and
/// for shelter in the rain, and finally divided down for anything the guest has ridden lately.
/// </para>
/// <para>
/// <b>Two of the weights are not constants.</b> The queue term and its weight are both nought unless the
/// guest is within three cells of the object (the original tests the squared distance against 9), and the
/// excitement weight is nought unless the item declares an excitement level at all. So the divisor is a
/// sum of whichever weights actually apply, not a fixed total - which is why they are summed here rather
/// than precomputed.
/// </para>
/// <para>
/// <b>ONE INPUT IS INJECTED RATHER THAN COMPUTED, and it is the honest seam in this class.</b>
/// <see cref="Candidate.Excitement"/> is what <c>FUN_004e0860</c> works out. Its sideshow branch is
/// fully decoded - <c>20 - clamp(cost - price, 0, 100)</c> - and its ride branch scales the item's own
/// <c>UsageInfo.ExcitementLevel</c> by two ratios each held between <b>0.75</b> and <b>1.25</b> (the
/// doubles at <c>0x007005b8</c> and <c>0x007005c0</c>). What is <i>not</i> established is which per-upgrade
/// field sits in each ratio's denominator: they are the descriptor's <c>+0x1a0</c> and <c>+0x1a8</c>, eight
/// bytes apart in an array strided <c>0x40</c> by upgrade level, and the item file offers
/// <c>RedLineDuration</c>, <c>RedLineCapacity</c> and a speed among the candidates. Rather than guess the
/// pairing and bury it inside the mean, the value comes in from outside.
/// </para>
/// </summary>
public sealed class ParkRideScore
{
	/// <param name="balance">
	/// The park's balance stack, which holds every weight and multiplier. Null leaves the shipped global
	/// file's own numbers in place, so a test can drive this without mounting a game.
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

	/// <summary>What the guest wants, which is all of them this decision reads.</summary>
	public readonly record struct Wants( int PersonType, float Thirst, float Hunger, float Toilet, float Vomit );

	/// <summary>
	/// One thing being considered, and the facts about it that live outside its own record.
	/// </summary>
	/// <param name="DistanceSquared">Squared distance in cells between the guest and the object's entry cell.</param>
	/// <param name="EffectsNearby">
	/// The cell's own effects count, which <b>divides</b> the distance term when it is not nought - see
	/// <see cref="ParkWorld.MapCell.NearbyEffects"/>, which is read but unconfirmed.
	/// </param>
	/// <param name="DaysOld">How long ago the thing was built - see <see cref="ParkWorld.BuiltWhen"/>.</param>
	/// <param name="Excitement">See the class remarks: injected, not computed.</param>
	public readonly record struct Candidate( ParkWorld.CatalogueObject Placed, ParkItemCatalogue.Item Item,
		int DistanceSquared, int QueueLength, int EffectsNearby, int DaysOld, bool Raining, int Excitement );

	/// <summary>Within this squared distance the queue matters; beyond it the term and its weight are nought.</summary>
	public const int QueueMattersWithin = 9;

	/// <summary>What the squared distance is scaled against before being taken from a hundred.</summary>
	public const int DistanceDivisor = 450;

	/// <summary>The most the excitement mismatch is allowed to count for.</summary>
	public const int ExcitementSpread = 50;

	/// <summary>
	/// What this candidate is worth. Nought means "not worth offering" - the chooser refuses anything
	/// scoring nine or less.
	/// </summary>
	public int Of( Wants wants, Candidate candidate,
		IReadOnlyList<int>? recentRides = null, IReadOnlyList<int>? alsoRecent = null )
	{
		var item = candidate.Item;

		// Distance. The original takes the squared distance, scales it against 450, holds it at a hundred
		// and subtracts - so a candidate underfoot scores a hundred and one 450 squared-cells away scores
		// nothing. The effects count divides what is left, which is the "nearby fireworks" branch.
		var distance = 100 - Math.Min( 100, (candidate.DistanceSquared * 100) / DistanceDivisor );

		if ( candidate.EffectsNearby != 0 )
			distance /= candidate.EffectsNearby;

		// Queue. BOTH the term and its weight fall away when the guest is not close, which is what stops a
		// distant empty queue counting for anything.
		var queueMatch = 0;
		var queueWeight = 0;

		if ( candidate.DistanceSquared < QueueMattersWithin )
		{
			// The original divides by the cell count outright. This reads the save's mQueueSizeInCells, which
			// is nought on the shop and the three toilets that ParkRideChoice offers by their walked count
			// (QueueCellsFor), so for those within three cells this guard divides by one cell.
			var cells = candidate.Placed.QueueSizeInCells > 0 ? candidate.Placed.QueueSizeInCells : 1;

			queueMatch = 100 - ((candidate.QueueLength * 100) / (cells * ParkRideChoice.QueueRoomPerCell));
			queueWeight = QueueWeight;
		}

		// Excitement. The weight is nought where the item declares no excitement at all, which is the
		// original's own gate rather than a guard against missing data.
		var mismatch = Math.Clamp( Math.Abs( PreferredExcitementFor( wants.PersonType ) - candidate.Excitement ),
			0, ExcitementSpread );

		var excitementMatch = 100 - (2 * mismatch);
		var excitementWeight = item.ExcitementLevel != 0 ? ExcitementWeight : 0;

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

		var score = total / weights;

		// A new attraction is worth a multiple of an old one, and shelter is worth the same while it rains.
		if ( candidate.DaysOld <= NewForDays )
			score *= NewRideMultiplier;

		if ( candidate.Raining && item.IsIndoors )
			score *= IndoorInRainMultiplier;

		// And anything ridden lately is worth progressively less. The original walks two separate
		// histories, dividing by five, four, three and two for the most recent four of each.
		score = Staled( score, recentRides, candidate.Placed.ThingId );
		score = Staled( score, alsoRecent, candidate.Placed.ThingId );

		return score;
	}

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

	/// <summary>Divides a score down where the thing appears among the last four of a history.</summary>
	private static int Staled( int score, IReadOnlyList<int>? history, int thingId )
	{
		if ( history == null )
			return score;

		for ( var back = 0; back < 4 && back < history.Count; ++back )
		{
			if ( history[back] == thingId )
				return score / (5 - back);
		}

		return score;
	}

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
