namespace OpenTPW;

/// <summary>
/// Everything a theme can have standing in it, indexed by the number a saved park stores to name it.
///
/// <para>
/// A park save says "item 1402 is at cell (55,15)" and nothing more - no path, no name, no size. The
/// number is the item's own <c>Info.Id</c>, declared in the <c>.sam</c> inside its <c>.wad</c>, so the
/// only way to turn one back into a model is to read the theme's items and see which one claims it.
/// That is what this does, once, when a park is built.
/// </para>
///
/// <para>
/// The four folders are the four the save's own feature list names - <c>Features</c>, <c>Shops</c>,
/// <c>Rides</c> and <c>Sideshow</c>. Each <c>.wad</c> in them stands in for a directory of its own name,
/// the same way terrain.wad does, so an item's description is at
/// <c>levels/&lt;theme&gt;/&lt;folder&gt;/&lt;name&gt;/&lt;name&gt;.sam</c>.
/// </para>
///
/// <para>
/// Ids are banded, which is a useful sanity check when reading one: 11xx rides, 12xx shops, 13xx
/// sideshows, 14xx features, 15xx upgrades, and 16xx the fixed items every park has.
/// </para>
/// </summary>
public sealed class ParkItemCatalogue
{
	/// <summary>
	/// One buildable item: what it is called, where its model lives, how much ground it covers, and the
	/// artwork for its name board if it has one - see <see cref="ParkObjects"/>, which paints it.
	/// </summary>
	/// <param name="UiType">Which kind it is - 0 rides, 1 shops, 2 sideshows, 3 features.</param>
	/// <param name="IsChoosable">
	/// Whether a guest may be sent here at all. <b>This is the item file's side of the save's own
	/// "offerable" flag</b>, and the two agree object for object in the shipped park - see
	/// <see cref="ItemDescriptionFile.IsChoosable"/>.
	/// </param>
	/// <param name="AnimationChannels">
	/// How many animations it can run at once - <see cref="ItemDescriptionFile.NumSimultAnims"/>, which is
	/// the size of the channel array the <c>_CH</c> instructions index. One unless the item says otherwise.
	/// </param>
	/// <param name="ChanceOfWinning">
	/// How often a guest gets what they came for - the object's <c>+0x190</c>, which the original derives as
	/// <c>100 - UsageInfo.InitChanceOfLoosing</c> as the thing is built. <b>The default of a hundred is the
	/// mechanism rather than a convenience</b>: a shop declares no chance of losing, so its roll never fails
	/// and it always serves. See <see cref="ItemDescriptionFile.ChanceOfWinning"/>.
	/// </param>
	/// <param name="CostOfGoods">
	/// What the item costs the park to provide - and, for a sideshow, <b>the prize it pays a winner</b>.
	/// </param>
	/// <param name="BuildPrice">
	/// What buying and building one costs - <see cref="ItemDescriptionFile.BuildPrice"/>. Nought on the
	/// items no buy list can reach, which are exactly the <see cref="UiType"/> 4 ones.
	/// </param>
	public readonly record struct Item( int Id, string Name, string Directory, string Stem, int Width, int Depth,
		string? SignPath, int UiType = ItemDescriptionFile.Feature, bool IsChoosable = false,
		bool ProvidesRelief = false, bool HasQueue = false, bool IsIndoors = false,
		int ExcitementLevel = 0, int AttractionValue = 0, int NewAttractionDecayTime = 0,
		int ThirstEffect = 0, int HungerEffect = 0, int VomitEffect = 0, int HappinessEffect = 0,
		int LitterEffect = 0, int TrackType = 0, int AnimationChannels = 1,
		int ChanceOfWinning = 100, int CostOfGoods = 0, int BuildPrice = 0,

		// The ride window's operating envelope - what its three sliders may be set to, and where a
		// newly built one starts. DurationUnit nought means the ride has no duration slider at all.
		int MinSpeed = 0, int MaxSpeed = 0, int InitSpeed = 0,
		int MinCapacity = 0, int MaxCapacity = 0, int InitCapacity = 0,
		int MinDuration = 0, int MaxDuration = 0, int InitDuration = 0, int DurationUnit = 0,

		// Where the red line sits on the speed and capacity tracks - the window marks it with a
		// coloured bar, green below and red beyond.
		int RedLineSpeed = 0, int RedLineCapacity = 0,

		// Where a guest walks up to this and where one is put down leaving it, as a column and a row of
		// the item's own unrotated footprint picture - see ItemDescriptionFile.EntryDeltaX. Without
		// these a thing the player builds has no entry cell, and nothing can queue for it.
		int EntryDeltaX = 0, int EntryDeltaY = 0,
		int ExitDeltaX = 0, int ExitDeltaY = 0, bool HasEntrance = false,

		// The compass bit each end's character carries, unrotated - ItemDescriptionFile.EntryDirection.
		int EntryDirection = 0x01, int ExitDirection = 0x10,

		// Every cell kind the picture uses - ItemDescriptionFile.CellKinds.
		IReadOnlyList<int>? CellKinds = null,

		// The particle effect selling one gives off - ItemDescriptionFile.DestroyParticleEffect.
		int DestroyParticleEffect = 0,

		// What the price opinion at the door reads beside the effects - ItemDescriptionFile.RipOffOK and the
		// two keys that decide which price samples it pushes. See PeepPriceOpinion.
		int RipOffOK = 0, int SpecialIngredient = 0, int AppearanceEffect = 0,

		// Whether a viewer walking in first person may not ride it from its entrance - ItemDescriptionFile.CannotRide.
		bool CannotRide = false,

		// What the ride score reads beside the excitement - ItemDescriptionFile.GoldenTicketCost - and what the
		// placer takes a track ride's slot for - BumperType (ParkBuilding.TakeTrackRide).
		int GoldenTicketCost = 0, int BumperType = 0,

		// What a go costs on one just built - ItemDescriptionFile.InitPricePerUse. See ParkBuilding.
		int InitPricePerUse = 0,

		// The two later tiers' starting speed and duration - ItemDescriptionFile.InitSpeedAt. See StartingAt.
		int InitSpeed1 = 0, int InitSpeed2 = 0, int InitDuration1 = 0, int InitDuration2 = 0,

		// Each tier's queue constant - ItemDescriptionFile.QueueWaitTimeConstantAt. See QueueWaitTimeConstantAt.
		float QueueWaitTimeConstant = 0f, float QueueWaitTimeConstant1 = 0f, float QueueWaitTimeConstant2 = 0f,

		// Whether every node of its model keeps a posed position - ItemDescriptionFile.DoHeadProcessing. See RideNodes.
		bool DoHeadProcessing = false,

		// What researching it costs - ItemDescriptionFile.ResearchCost. Nought is researched from the start: ParkResearch.
		int ResearchCost = 0,

		// A bumper ride's arena: the placer's adjust for each turn, north, east, south and west, and the meshes its
		// cars are made of - ItemDescriptionFile.BumperAdjust and SupplementalMeshes. See ParkBumperCars.
		IReadOnlyList<(int X, int Y)>? BumperAdjusts = null, IReadOnlyList<string?>? SupplementalMeshes = null,
		ItemHoarding? Hoarding = null,

		// The footprint picture, a kind a cell - ItemDescriptionFile.Shape. The buy screen paints it: ParkFootprintPicture.
		ItemShape? Shape = null,

		// What being new adds to AttractionValue, a step of the age each - ItemDescriptionFile.NewBonusAt. ParkWorth reads it.
		int NewBonus0 = 0, int NewBonus1 = 0, int NewBonus2 = 0 )
	{
		/// <summary>The bonus of one step of the age, nought past the third - <see cref="ItemDescriptionFile.NewBonusAt"/>.</summary>
		public int NewBonusAt( int step ) => step switch { 0 => NewBonus0, 1 => NewBonus1, 2 => NewBonus2, _ => 0 };

		/// <summary>The placer's arena adjust for a turn - <see cref="ItemDescriptionFile.BumperAdjust"/>.</summary>
		public (int X, int Y) BumperAdjustAt( int angle ) => BumperAdjusts is { Count: 4 } adjusts
			? angle switch { 0 => adjusts[0], 90 => adjusts[1], 180 => adjusts[2], 270 => adjusts[3], _ => (0, 0) }
			: (0, 0);

		/// <summary>
		/// A tier's starting speed and duration, <c>Upgrades[tier]</c> - what an upgraded ride's excitement divides its
		/// own by (<c>FUN_004e0560</c>). Tier nought is the purchase's, <see cref="InitSpeed"/> and <see cref="InitDuration"/>.
		/// </summary>
		public (int Speed, int Duration) StartingAt( int tier ) => tier switch
		{
			0 => (InitSpeed, InitDuration),
			1 => (InitSpeed1, InitDuration1),
			2 => (InitSpeed2, InitDuration2),
			_ => throw new ArgumentOutOfRangeException( nameof( tier ), tier, "Upgrades has three tiers" )
		};

		/// <summary>
		/// A tier's queue constant, <c>Upgrades[tier].QueueWaitTimeConstant</c> - what the longest queue a guest joins
		/// or stays in is scaled by (<c>FUN_004dda40</c>, <see cref="PeepBehaviour.LongestQueue"/>).
		/// </summary>
		public float QueueWaitTimeConstantAt( int tier ) => tier switch
		{
			0 => QueueWaitTimeConstant,
			1 => QueueWaitTimeConstant1,
			2 => QueueWaitTimeConstant2,
			_ => throw new ArgumentOutOfRangeException( nameof( tier ), tier, "Upgrades has three tiers" )
		};
	}

	private readonly Dictionary<int, Item> _items = [];

	/// <summary>How many items this theme turned out to offer.</summary>
	public int Count => _items.Count;

	/// <summary>
	/// The folders holding free-standing items. <c>upgrades</c> is deliberately not among them: its
	/// contents are tunnels and jumps that attach to a ride rather than stand on their own, and no park
	/// the game ships places one as an object.
	/// </summary>
	private static readonly string[] Folders = ["features", "shops", "rides", "sideshow"];

	/// <summary>
	/// Where the items are read from. A running game passes nothing and gets the one it mounted; a test
	/// passes its own, so that cataloguing a real theme needs no global to have been set - which is the
	/// rule every other park test already follows.
	/// </summary>
	private readonly BaseFileSystem _files;

	/// <summary>The file system the items were read from, where their models are too.</summary>
	public BaseFileSystem Files => _files;

	/// <summary>
	/// Whether this is Instant Action's catalogue: each item's <c>Easy_&lt;stem&gt;.sam</c> laid over its own file, and
	/// an item whose wad has none left out, as the original does in game type 2 (<c>FUN_00413930</c>,
	/// <c>0x00413ac4</c>; <c>park-engine.md</c>, "How a key finds its global"). In Lost Kingdom that leaves 50 of 67.
	/// </summary>
	public bool InstantAction { get; }

	/// <param name="instantAction">
	/// Whether the park is Instant Action's - the same condition <see cref="ParkBalance"/>'s <c>easyMode</c> is given,
	/// as <see cref="Level"/> passes both. Defaulted to false, as the balance's is, so asking without an opinion gets
	/// every item the theme has.
	/// </param>
	public ParkItemCatalogue( string themeName, BaseFileSystem? files = null, bool instantAction = false )
	{
		_files = files ?? FileSystem;
		InstantAction = instantAction;

		var theme = themeName.ToLowerInvariant();
		var unreadable = 0;
		var leftOut = 0;

		foreach ( var folder in Folders )
		{
			var path = $"levels/{theme}/{folder}";

			string[] directories;

			try
			{
				directories = _files.GetDirectories( path );
			}
			catch ( Exception e )
			{
				// A theme without one of these folders is not broken - it simply sells nothing of that
				// kind - so this reports and carries on.
				Log.Info( $"{themeName}: no {folder} to catalogue - {e.Message}" );
				continue;
			}

			// The folder's own description, which every item in it inherits from. An item's file says only
			// what DIFFERS from this, so reading an item without it misses which kind the item is, whether
			// a guest may choose it, and whether it has a queue - none of which most items restate.
			var category = TryReadCategory( path, folder );

			// A category every item of the folder is read over: refused, it leaves out the folder.
			if ( category != null && Refuses( $"{path}/{folder}.sam", category ) )
				continue;

			// Every item is catalogued from its category and its own file and, in Instant Action, its Easy_ file
			// last; there an item whose wad has no Easy_ file is left out before it is read (0x00413ac4).
			foreach ( var directory in directories )
			{
				var stem = Path.GetFileName( directory );

				if ( string.IsNullOrEmpty( stem ) )
					continue;

				if ( InstantAction && !Exists( EasyPath( $"{path}/{stem}", stem ) ) )
				{
					++leftOut;
					continue;
				}

				if ( !TryRead( $"{path}/{stem}", stem, out var item, category ) )
				{
					++unreadable;
					continue;
				}

				// Last one wins, and says so. Nothing in the shipped themes collides, so a collision is
				// worth hearing about rather than resolving quietly.
				if ( _items.TryGetValue( item.Id, out var existing ) )
					Log.Warning( $"{themeName}: items '{existing.Stem}' and '{stem}' both claim catalogue number {item.Id}" );

				_items[item.Id] = item;
			}
		}

		Log.Info( $"{themeName}: catalogued {Count} items" + (unreadable > 0 ? $", and {unreadable} would not read" : "")
			+ (InstantAction ? $", {leftOut} left out of Instant Action" : "") );
	}

	/// <summary>FUN_0051fa20: item emitters occupy the first unnamed library slots, in catalogue order.</summary>
	internal void RegisterParticleEffects( ParticleLibraryFile? library )
	{
		if ( library == null )
			return;

		foreach ( var item in All )
		{
			foreach ( var path in _files.GetFiles( item.Directory ).Where( path =>
				Path.GetExtension( path ).Equals( ".emt", StringComparison.OrdinalIgnoreCase ) ) )
			{
				using var stream = _files.OpenRead( path );
				var slot = library.RegisterEffect( stream );
				Log.Info( $"Particle catalogue: {path} -> slot {slot}" );
			}
		}
	}

	/// <summary>
	/// Reads one item's description, or answers false if that directory does not hold one - which is not
	/// an error worth a line of its own, because these folders can hold things that are not items.
	/// </summary>
	private bool TryRead( string directory, string stem, out Item item, ItemDescriptionFile? category = null )
	{
		item = default;

		try
		{
			var own = $"{directory}/{stem}.sam";
			using var stream = _files.OpenRead( own );
			var description = new ItemDescriptionFile( stream, category );

			if ( Refuses( own, description ) )
				return false;

			// Only where the wad holds one: whether an item without it is catalogued at all is the gate's to say.
			if ( InstantAction && Exists( EasyPath( directory, stem ) ) )
			{
				using var easy = _files.OpenRead( EasyPath( directory, stem ) );
				description.Overlay( easy );

				if ( Refuses( EasyPath( directory, stem ), description ) )
					return false;
			}

			if ( description.Id <= 0 )
				return false;

			// A ride keeps the artwork for its name board beside its model, always under the item's own
			// name - seventy-eight of the seventy-eight that have one, across all four themes.
			var sign = $"{directory}/{stem}.sgn";

			item = new Item( description.Id, description.Name, directory, stem,
				description.FootprintWidth, description.FootprintDepth,
				Exists( sign ) ? sign : null,
				description.WhichUIType, description.IsChoosable, description.ProvidesRelief,
				description.HasQueue, description.IsIndoors, description.ExcitementLevel,
				description.AttractionValue, description.NewAttractionDecayTime,
				description.ThirstEffect, description.HungerEffect, description.VomitEffect,
					description.HappinessEffect, description.LitterEffect, description.TrackType, description.NumSimultAnims,
					description.ChanceOfWinning, description.CostOfGoods, description.BuildPrice,
					description.MinSpeed, description.MaxSpeed, description.InitSpeed,
					description.MinCapacity, description.MaxCapacity, description.InitCapacity,
					description.MinDuration, description.MaxDuration, description.InitDuration,
					description.DurationUnit,
					description.RedLineSpeed, description.RedLineCapacity,
					description.EntryDeltaX, description.EntryDeltaY,
					description.ExitDeltaX, description.ExitDeltaY, description.HasEntrance,
					description.EntryDirection, description.ExitDirection, description.CellKinds,
					description.DestroyParticleEffect,
					description.RipOffOK, description.SpecialIngredient, description.AppearanceEffect,
					description.CannotRide, description.GoldenTicketCost, description.BumperType,
					description.InitPricePerUse,
					description.InitSpeedAt( 1 ), description.InitSpeedAt( 2 ),
					description.InitDurationAt( 1 ), description.InitDurationAt( 2 ),
					description.QueueWaitTimeConstantAt( 0 ), description.QueueWaitTimeConstantAt( 1 ),
					description.QueueWaitTimeConstantAt( 2 ),
					description.DoHeadProcessing, description.ResearchCost,
					[description.BumperAdjust( 0 ), description.BumperAdjust( 90 ), description.BumperAdjust( 180 ),
						description.BumperAdjust( 270 )],
					description.SupplementalMeshes, description.Hoarding, description.Shape,
					description.NewBonusAt( 0 ), description.NewBonusAt( 1 ), description.NewBonusAt( 2 ) );

			return true;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// Whether the file just read holds a line the original refuses. The original quits the game there, naming the file
	/// (<c>0x00413f18</c>); OpenTPW leaves out what the file describes and counts it (Alexah, 2026-09-30). No shipped
	/// file has one.
	/// </summary>
	private static bool Refuses( string file, ItemDescriptionFile description )
	{
		if ( description.Refused is not { } refused )
			return false;

		Log.Warning( $"{file}: '{refused}' is a line the original refuses, so what it describes is left out" );
		Unimplemented.Report( "ITEM_VALUE_REFUSED" );

		return true;
	}

	/// <summary>
	/// Where an item's Instant Action layer is, in its own wad: <c>"Easy_"</c> (<c>0x00747930</c>), the stem and
	/// <c>".sam"</c>. The file system matches it without regard to case, as the original's wad lookup does.
	/// </summary>
	private static string EasyPath( string directory, string stem ) => $"{directory}/Easy_{stem}.sam";

	/// <summary>
	/// The folder's own description - <c>rides/Rides.sam</c> and its three siblings - whose values every
	/// item in that folder inherits. Null where the folder has none, which leaves each item standing on
	/// its own file alone.
	/// </summary>
	/// <remarks>
	/// <b>The name is asked for in the folder's own case and found in the file's.</b> The folders are
	/// lower case and the files are not - <c>sideshow/SideShow.sam</c>, <c>rides/Rides.sam</c> - so this
	/// leans on the same Windows-style case-insensitive matching every other path in the game does. If
	/// that ever stopped working the symptom would be quiet: every item would read as unchoosable, because
	/// almost none of them restate it.
	/// </remarks>
	private ItemDescriptionFile? TryReadCategory( string path, string folder )
	{
		try
		{
			using var stream = _files.OpenRead( $"{path}/{folder}.sam" );

			return new ItemDescriptionFile( stream );
		}
		catch ( Exception e )
		{
			Log.Info( $"no category description for '{folder}', so its items stand on their own files - {e.Message}" );

			return null;
		}
	}

	/// <summary>
	/// Whether the file system can offer this path at all. It asks for a size rather than using
	/// <c>FileExists</c>, which does not look inside archives - and every one of these lives in a .wad.
	/// </summary>
	private bool Exists( string path )
	{
		try
		{
			return _files.GetSize( path ) > 0;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>The item that calls itself <paramref name="id"/>, if this theme has one.</summary>
	public bool TryGet( int id, out Item item ) => _items.TryGetValue( id, out item );

	/// <summary>
	/// Everything this theme catalogued, in no particular order - what a screen listing what a park may
	/// buy walks, where <see cref="TryGet"/> answers only for a number already in hand.
	/// </summary>
	/// <remarks>
	/// <b>It is unfiltered on purpose.</b> The original's buy list does the choosing itself, keeping a row
	/// only where the item's <c>Info.WhichUIType</c> equals the tab being shown - so the four tabs are one
	/// walk with one test, and the kinds above <see cref="ItemDescriptionFile.Feature"/>, which belong
	/// to no tab (the vehicles, the gates, the lights, the land tools) fall out of that same test rather
	/// than needing a rule of their own.
	/// </remarks>
	public IEnumerable<Item> All => _items.Values;
}
