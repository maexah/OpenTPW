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
