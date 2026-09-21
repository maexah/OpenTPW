using System.Text;

namespace OpenTPW;

/// <summary>
/// One buildable item's description - the <c>.sam</c> that sits inside the item's own <c>.wad</c>, which
/// the file itself calls a "Theme Park 2 Ride Description File" whether it describes a ride, a shop, a
/// sideshow or a litter bin.
///
/// <para>
/// This exists rather than a plain <see cref="SettingsFile"/> because the part that matters here is the
/// part that file cannot reach. <c>Info.Shape</c> is not a value but a block - a row of dashes, an ASCII
/// picture of the item's footprint, another row of dashes - and <see cref="SAMParser"/> reads a value as
/// the single word after the key, so it comes back as "---" with the picture left behind as unparsed
/// junk. The scalars are read the same way the parser would; only the block needs its own handling.
/// </para>
///
/// <para>
/// <b>The footprint is the grid's bounding box, not the cells drawn in it.</b> That is worth stating
/// because the picture invites the opposite reading: <c>4x4rock</c> draws fourteen stars inside a four by
/// four box, <c>5x5rck</c> twenty-three inside five by five, and <c>ground</c> draws none at all - yet
/// every one of the jungle's seventy items has a <c>.hmp</c> whose length says width times height, all
/// seventy, including the three that would otherwise disagree.
/// </para>
/// </summary>
public sealed class ItemDescriptionFile
{
	/// <summary>
	/// The catalogue number, which is what a saved park stores to say which item is standing somewhere.
	/// They are banded: 11xx rides, 12xx shops, 13xx sideshows, 14xx features, 15xx upgrades and 16xx the
	/// fixed items that every park has.
	/// </summary>
	public int Id { get; private set; }

	/// <summary>What the item is called, as the game shows it - "Small Toilet", "Belly Bounce".</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>How many cells across the item stands, and how many deep.</summary>
	public int FootprintWidth { get; private set; } = 1;

	public int FootprintDepth { get; private set; } = 1;

	/// <summary>
	/// The category's own description, whose values this one falls back to - see the constructor.
	/// </summary>
	private readonly ItemDescriptionFile? _category;

	/// <param name="category">
	/// The folder-level description this item inherits from, or null for a description that stands alone.
	///
	/// <para>
	/// <b>An item's file is an OVERRIDE, not a whole description.</b> Each folder carries one
	/// - <c>rides/Rides.sam</c>, <c>shops/Shops.sam</c>, <c>sideshow/SideShow.sam</c>,
	/// <c>features/Features.sam</c> - holding the defaults for everything of that kind, and an item's own
	/// file says only what differs. Read on its own, <c>Bouncy.sam</c> does not say it is a ride, does not
	/// say people may use it, and does not say it has a queue; all three come from the folder.
	/// </para>
	/// </param>
	public ItemDescriptionFile( Stream stream, ItemDescriptionFile? category = null )
	{
		_category = category;

		using var reader = new StreamReader( stream, Encoding.ASCII );
		Read( reader.ReadToEnd() );
	}

	public ItemDescriptionFile( string text, ItemDescriptionFile? category = null )
	{
		_category = category;
		Read( text );
	}

	/// <summary>
	/// Which of the four kinds this is - <b>0 rides, 1 shops, 2 sideshows, 3 features</b>, numbered by the
	/// game's own comment beside the key. Nothing in the jungle overrides it, so in practice it is the
	/// folder the item lives in.
	/// </summary>
	public int WhichUIType => _whichUIType ?? _category?.WhichUIType ?? Feature;

	/// <summary>The value <see cref="WhichUIType"/> takes for a feature, which is what an unknown item reads as.</summary>
	public const int Feature = 3;

	/// <summary>
	/// Whether a guest may choose to come here - <c>Info.IsChoosable</c>, whose own comment reads "People
	/// CAN use this" where it is set and "People CANNOT choose to use most features in their decision
	/// making" in the features default.
	/// </summary>
	/// <remarks>
	/// <b>This is where the save's own "a guest may be offered this" flag comes from</b>, and the two agree
	/// exactly in the shipped park: rides, shops and sideshows inherit 1 from their folder, features
	/// inherit 0, and the Small Toilet overrides itself back to 1 - which is precisely the six objects
	/// whose record carries the bit.
	/// </remarks>
	public bool IsChoosable => (_isChoosable ?? _category?._isChoosable ?? 0) != 0;

	/// <summary>Whether using this relieves a guest who needs the toilet - <c>UsageInfo.ProvidesRelief</c>,
	/// whose default comment says "set to 1 for toilets". The three Small Toilets set it and nothing else does.</summary>
	public bool ProvidesRelief => (_providesRelief ?? _category?._providesRelief ?? 0) != 0;

	/// <summary>Whether people queue for this at all - <c>Info.HasQueue</c>, set for rides and not for the rest.</summary>
	public bool HasQueue => (_hasQueue ?? _category?._hasQueue ?? 0) != 0;

	/// <summary>Whether this is shelter from the rain - <c>UsageInfo.ISIndoors</c>, the game's own spelling.</summary>
	public bool IsIndoors => (_isIndoors ?? _category?._isIndoors ?? 0) != 0;

	/// <summary>How exciting this is - <c>UsageInfo.ExcitementLevel</c>. Belly Bounce 40, Jungle Spray 35.</summary>
	public int ExcitementLevel => _excitementLevel ?? _category?.ExcitementLevel ?? 0;

	/// <summary>What this adds to the park's draw - <c>Info.AttractionValue</c>. Belly Bounce overrides it to 25.</summary>
	public int AttractionValue => _attractionValue ?? _category?.AttractionValue ?? 0;

	/// <summary>How long it stays "new" - <c>Info.NewAttractionDecayTime</c>, 60 for rides and 30 for features.</summary>
	public int NewAttractionDecayTime => _newAttractionDecayTime ?? _category?.NewAttractionDecayTime ?? 0;

	/// <summary>How much thirst using this takes away - <c>UsageInfo.ThirstEffect</c>. The Drinks Shop sets 40.</summary>
	public int ThirstEffect => _thirstEffect ?? _category?.ThirstEffect ?? 0;

	/// <summary>The same for hunger. <b>The Drinks Shop sets it to nought</b>, which is a drink rather than a meal.</summary>
	public int HungerEffect => _hungerEffect ?? _category?.HungerEffect ?? 0;

	/// <summary>
	/// What using this does to a guest's sickness - <c>UsageInfo.VomitEffect</c>.
	///
	/// <para>
	/// <b>These three complete the effect block, and which meter each moves is what names it.</b> The
	/// original applies five consecutive descriptor fields to five guest meters when somebody finishes
	/// using a thing: <c>+0x144</c> to thirst, <c>+0x148</c> to hunger, <c>+0x14c</c> to sickness,
	/// <c>+0x150</c> to happiness and <c>+0x154</c> to the litter they carry. The <c>.sam</c> files carry
	/// exactly five <c>UsageInfo.*Effect</c> keys, on the same eight items, so each key is matched to the
	/// meter its offset writes rather than by assuming the file and the struct share an order.
	/// </para>
	/// <para>
	/// <b>Sickness is spelled "Vomit" here because the file is</b> - the balance file calls the same meter
	/// illness, and <c>Peep.Vomit</c> carries a note about the two names being one thing.
	/// </para>
	/// </summary>
	public int VomitEffect => _vomitEffect ?? _category?.VomitEffect ?? 0;

	/// <inheritdoc cref="VomitEffect"/>
	public int HappinessEffect => _happinessEffect ?? _category?.HappinessEffect ?? 0;

	/// <inheritdoc cref="VomitEffect"/>
	public int LitterEffect => _litterEffect ?? _category?.LitterEffect ?? 0;

	/// <summary>
	/// What kind of track the item runs on - <c>Bumper.WhichTrackType</c>, and the field the original's
	/// "is this open for business" test keys on.
	///
	/// <para>
	/// <b>The file's own comment undercounts its own enum.</b> <c>Rides.sam</c> documents it as
	/// "0=no track, 1=car track, 2=water track", and the shipped data carries a <b>3</b> as well: the
	/// jungle's <c>Dino Karts</c> is 1, <c>Splish Splash</c> is 2, and all three of its coasters -
	/// <c>Chac Atak</c>, <c>Gorilla Thrilla</c> and <c>Temple Of Gloom</c> - are 3, while every other ride
	/// is nought.
	/// </para>
	/// <para>
	/// <b>That reading is not taken from the comment but from what the code does with it</b>, across the
	/// whole set rather than one item: a candidate of type 1 or 2 is refused unless its track ride is
	/// valid, and a type 3 has its excitement computed from the ride's own script handle instead of from
	/// its speed, duration and capacity. The three items carrying 3 are exactly the three that declare no
	/// duration at all, which is what makes the second path necessary for them.
	/// </para>
	/// </summary>
	public int TrackType => _trackType ?? _category?.TrackType ?? 0;

	/// <summary>Track types that need a valid track ride before the item may be offered.</summary>
	public const int CarTrack = 1;

	/// <inheritdoc cref="CarTrack"/>
	public const int WaterTrack = 2;

	/// <summary>The coasters - excitement comes from the ride's own script rather than its settings.</summary>
	public const int CoasterTrack = 3;

	/// <summary>
	/// How many animations this item can run at once - <c>UsageInfo.NumSimultAnims</c>, whose own comment
	/// reads "How many different anims can this ride run?  Usu. 1, sideshows more".
	///
	/// <para>
	/// <b>It is the engine's animation-channel count rather than a hint.</b> The thing loader hands this
	/// field straight to the model loader as its channel count - <c>FUN_00413c10</c> passes
	/// <c>thing+0x170</c> to <c>FUN_004629d0</c>, which substitutes 1 when it is nought, which is why the
	/// default here is 1 and not nought. Ride vehicles pass a literal 5 and the queue models pass nothing
	/// at all, so this is the only route by which an item's own file decides the number.
	/// </para>
	/// <para>
	/// <b>The shipped scripts agree with it exactly, which is what identifies it.</b> Every script that
	/// names a channel reaches precisely the last one its item allows: the Jungle Spray, Hyenas, Frushy,
	/// Squirtem and Marsmoon declare 3 and use channel 2, while the Totem declares 4 and uses channel 3 -
	/// so the declaration and the use move together rather than both happening to be three. It sizes
	/// <c>RideAnimations</c>' player array, which lives in the game rather than in this assembly.
	/// </para>
	/// </summary>
	public int NumSimultAnims => _numSimultAnims ?? _category?.NumSimultAnims ?? 1;

	/// <summary>
	/// How often a guest <b>loses</b> at this thing - <c>UsageInfo.InitChanceOfLoosing</c>, the game's own
	/// spelling. The chance of WINNING is <see cref="ChanceOfWinning"/>, which is a hundred less this.
	///
	/// <para>
	/// <b>The category default is 70 and it is declared only by <c>sideshow/SideShow.sam</c></b>; the Jungle
	/// Spray overrides it to <b>75</b> in its own <c>.sam</c>, inside <c>junspray.wad</c>. Nothing in the
	/// <c>shops</c> or <c>rides</c> folders declares it at all, which is not an omission but the whole
	/// mechanism - see <see cref="ChanceOfWinning"/>.
	/// </para>
	/// </summary>
	public int ChanceOfLosing => _chanceOfLosing ?? _category?.ChanceOfLosing ?? 0;

	/// <summary>
	/// How often a guest gets what they came for - the object's <c>+0x190</c>, which
	/// <c>FUN_004db090</c> builds as <c>100 - UsageInfo.InitChanceOfLoosing</c> at
	/// <c>004db38f</c>..<c>004db3a1</c>.
	///
	/// <para>
	/// <b>A thing that declares no chance of losing therefore always succeeds, and that is what makes a
	/// shop work.</b> <c>FUN_004e2670</c> rolls <c>rand() % 100 &lt;= chance</c> and writes the answer into
	/// the guest's <c>mQueuePos</c>, which the settle-up then uses to decide whether the visit did anything
	/// at all. A shop declares nothing, so its chance is <b>100</b>, the roll never fails, and the drink is
	/// always served; the Jungle Spray declares 75, so its chance is <b>25</b>.
	/// </para>
	/// <para>
	/// <b>The assertion in that function is what confirms the reading rather than a guess at it</b>: it
	/// insists the object is a sideshow <i>or</i> that this value is <c>'d'</c> - decimal <b>100</b> - which
	/// is exactly the case a shop falls into.
	/// </para>
	/// </summary>
	public int ChanceOfWinning => 100 - ChanceOfLosing;

	/// <summary>
	/// What this costs the park to provide - <c>UsageInfo.InitCostOfGoods</c>, descriptor <c>+0x140</c>,
	/// which the object keeps at <c>+0x188</c> and <c>FUN_004e1a10</c> answers.
	///
	/// <para>
	/// <b>It is the PRIZE a sideshow pays out</b>, added straight to the winner's cash by
	/// <c>FUN_004fe1e0</c>, and the numerator of the happiness the win is worth. The Jungle Spray's own
	/// file sets it to <b>50</b> against a price of 20 - so winning it is worth more than playing it cost;
	/// the Drinks Shop's sets <b>20</b> against a price of 30.
	/// </para>
	/// <para>
	/// <b>Both come from the item's own <c>.sam</c> INSIDE its <c>.wad</c>, not from the category file</b>,
	/// and the difference reverses the sign of the result - the category declares 30 against 10. Anything
	/// asking this question of the category alone gets a different park.
	/// </para>
	/// </summary>
	public int CostOfGoods => _costOfGoods ?? _category?.CostOfGoods ?? 0;

	/// <summary>
	/// What it costs to buy and build one - <c>Upgrades[0].CostOfUpgrade</c>, whose own comment in every
	/// category file reads "cash cost when buying this item".
	///
	/// <para>
	/// <b>It is the item's own file that decides it, not the category's</b>, the same way
	/// <see cref="CostOfGoods"/> is: the categories declare a flat 1000 for rides and 100 for the other
	/// three, while the jungle's own items run from <b>100</b> (Small Toilet, Litter Bin, every small rock
	/// and bush) to <b>12,500</b> (Gorilla Thrilla). Belly Bounce 500, Drinks Shop 650, Jungle Spray 1750,
	/// Round Fountain 150, Staff Room 500. Reading the category alone would price every ride the same.
	/// </para>
	/// <para>
	/// <b><c>Upgrades[1]</c> and <c>[2]</c> are the later tiers and are NOT this</b> - the Belly Bounce's
	/// are 400 and 500, and the shops, sideshows and features declare both as nought with the comment
	/// "Always zero for shops (no upgrades possible)". Only slot nought is the purchase.
	/// </para>
	/// <para>
	/// The items that declare nought are exactly the ones no buy list can reach: the three arrival
	/// vehicles, the gates, the lights, the End sign, Mystery, and the two land tools - every one of them
	/// <c>Info.WhichUIType</c> <b>4</b>, which the files themselves annotate "Not to be shown in UI".
	/// </para>
	/// </summary>
	public int BuildPrice => _buildPrice ?? _category?.BuildPrice ?? 0;

	private int? _buildPrice;

	private int? _whichUIType;
	private int? _isChoosable;
	private int? _providesRelief;
	private int? _hasQueue;
	private int? _isIndoors;
	private int? _excitementLevel;
	private int? _attractionValue;
	private int? _newAttractionDecayTime;
	private int? _thirstEffect;
	private int? _hungerEffect;
	private int? _vomitEffect;
	private int? _happinessEffect;
	private int? _litterEffect;
	private int? _trackType;
	private int? _numSimultAnims;
	private int? _chanceOfLosing;
	private int? _costOfGoods;

	private void Read( string text )
	{
		var lines = text.Split( '\n' );

		var width = 0;
		var depth = 0;

		for ( var i = 0; i < lines.Length; ++i )
		{
			var line = lines[i].TrimEnd( '\r' );
			var key = KeyOf( line );

			switch ( key )
			{
				case "Info.Id":
					if ( int.TryParse( ValueOf( line ), out var id ) )
						Id = id;
					break;

				case "Info.Name":
					Name = Quoted( line );
					break;

				// The original's own escape hatch, and in the whole jungle only the park gate uses it: its
				// picture is a single cell but it really stands six by three over the entrance. Where these
				// are present they win, because the game's own loader takes them in preference.
				case "Info.EngineFootprintWidthOverride":
					if ( int.TryParse( ValueOf( line ), out var overrideWidth ) && overrideWidth > 0 )
						FootprintWidth = overrideWidth;
					break;

				case "Info.EngineFootprintHeightOverride":
					if ( int.TryParse( ValueOf( line ), out var overrideDepth ) && overrideDepth > 0 )
						FootprintDepth = overrideDepth;
					break;

				case "Info.Shape":
					ReadShape( lines, i, out width, out depth );
					break;

				// The decision keys. Each is left NULL when the item's own file does not mention it, so
				// that the category's value shows through - see the constructor's remarks.
				case "Info.WhichUIType":
					_whichUIType = Number( line );
					break;

				case "Info.IsChoosable":
					_isChoosable = Number( line );
					break;

				case "UsageInfo.ProvidesRelief":
					_providesRelief = Number( line );
					break;

				case "Info.HasQueue":
					_hasQueue = Number( line );
					break;

				case "UsageInfo.ISIndoors":
					_isIndoors = Number( line );
					break;

				case "UsageInfo.ExcitementLevel":
					_excitementLevel = Number( line );
					break;

				case "Info.AttractionValue":
					_attractionValue = Number( line );
					break;

				case "Info.NewAttractionDecayTime":
					_newAttractionDecayTime = Number( line );
					break;

				case "UsageInfo.ThirstEffect":
					_thirstEffect = Number( line );
					break;

				case "UsageInfo.HungerEffect":
					_hungerEffect = Number( line );
					break;

				case "UsageInfo.VomitEffect":
					_vomitEffect = Number( line );
					break;

				case "UsageInfo.HappinessEffect":
					_happinessEffect = Number( line );
					break;

				case "UsageInfo.LitterEffect":
					_litterEffect = Number( line );
					break;

				case "Bumper.WhichTrackType":
					_trackType = Number( line );
					break;

				case "UsageInfo.NumSimultAnims":
					_numSimultAnims = Number( line );
					break;

				// The game's own spelling of "losing", and it must be matched exactly - see ChanceOfLosing.
				case "UsageInfo.InitChanceOfLoosing":
					_chanceOfLosing = Number( line );
					break;

				case "UsageInfo.InitCostOfGoods":
					_costOfGoods = Number( line );
					break;

				// Slot nought only - the later two are upgrade tiers, not the purchase. See BuildPrice.
				case "Upgrades[0].CostOfUpgrade":
					_buildPrice = Number( line );
					break;
			}
		}

		// The picture only fills in what no override has already claimed, so an item carrying one keeps it.
		if ( width > 0 && !HasOverride( text, "Info.EngineFootprintWidthOverride" ) )
			FootprintWidth = width;

		if ( depth > 0 && !HasOverride( text, "Info.EngineFootprintHeightOverride" ) )
			FootprintDepth = depth;
	}

	/// <summary>
	/// The footprint picture: the lines between the two rows of dashes that follow the key. Width is the
	/// longest row and depth the number of rows, both of the box the picture is drawn in rather than of
	/// the marks inside it.
	/// </summary>
	private static void ReadShape( string[] lines, int keyLine, out int width, out int depth )
	{
		width = 0;
		depth = 0;

		var at = keyLine + 1;

		while ( at < lines.Length && lines[at].Trim() != Fence )
			++at;

		for ( ++at; at < lines.Length && lines[at].Trim() != Fence; ++at )
		{
			var row = lines[at].TrimEnd( '\r', '\n', ' ', '\t' );

			// Blank rows inside the fence are layout, not footprint - counting them would make an item
			// deeper than it is.
			if ( row.Length == 0 )
				continue;

			width = Math.Max( width, row.Length );
			++depth;
		}
	}

	/// <summary>
	/// The line's value as a whole number, or null where it has none. Null rather than nought matters:
	/// nought is a real answer for every one of these keys, and "the item did not say" has to stay
	/// distinguishable from "the item said no" or the category's value could never show through.
	/// </summary>
	private static int? Number( string line )
		=> int.TryParse( ValueOf( line ), out var value ) ? value : null;

	/// <summary>The row of dashes that opens and closes a block.</summary>
	private const string Fence = "---";

	private static bool HasOverride( string text, string key )
		=> text.Contains( key, StringComparison.OrdinalIgnoreCase );

	/// <summary>The key a line declares, or empty if it declares none - a comment, a blank, or part of a block.</summary>
	private static string KeyOf( string line )
	{
		var trimmed = line.TrimStart();

		if ( trimmed.Length == 0 || trimmed[0] == '#' )
			return string.Empty;

		var end = 0;

		while ( end < trimmed.Length && !char.IsWhiteSpace( trimmed[end] ) )
			++end;

		return trimmed[..end];
	}

	/// <summary>
	/// The first word after the key. Values carry an inline comment more often than not - the shipped
	/// files write things like <c>Info.IsChoosable 1 People CAN use this</c> - so everything past the
	/// first word is dropped rather than parsed.
	/// </summary>
	private static string ValueOf( string line )
	{
		var parts = line.Split( [' ', '\t'], StringSplitOptions.RemoveEmptyEntries );

		return parts.Length > 1 ? parts[1] : string.Empty;
	}

	/// <summary>
	/// What is between the first pair of quotes on the line. Names have spaces in them, which is exactly
	/// what the ordinary one-word read cannot carry: "Small Toilet" comes back as "Small.
	/// </summary>
	private static string Quoted( string line )
	{
		var open = line.IndexOf( '"' );

		if ( open < 0 )
			return string.Empty;

		var close = line.IndexOf( '"', open + 1 );

		return close > open ? line[(open + 1)..close] : string.Empty;
	}
}
