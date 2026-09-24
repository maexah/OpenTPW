using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A park's fixed items: the gate standing over the entrance, the traffic lights at the two
/// pedestrian crossings, and the three arrival vehicles (bus, ferry, seaplane) at their spawns off the map. They are "fixed" in the original's own
/// sense - every park has them, in the same place, and the player can neither build nor remove them.
///
/// <para>
/// <b>They carry no position in the save.</b> Their records in the saved thing list hold the sentinel
/// 128/128 that every unplaced thing holds, which is what makes them look absent. The positions are
/// baked into the models instead: each mesh's node transform is already in world coordinates, exactly
/// as <see cref="ParkTerrain"/>'s <c>base.MD2</c> is, so these load at the origin rather than being
/// placed. Their <c>.sam</c> says as much - <c>Info.DontApplyOffset 1</c>, "this is a fixed item whose
/// animation should be played relative to world (0,0), not the object pos".
/// </para>
///
/// <para>
/// That the models really are world-positioned is checked against a file that knows nothing about
/// them, <c>Standard.sam</c>: the gate's two door meshes sit at world x 470 and 490, bracketing the
/// two entrance cells <c>EntranceA</c> (47,17) and <c>EntranceB</c> (48,17), and the four traffic
/// lights land on <c>CrossingParkSideA/B</c> (47,9),(48,9) and <c>CrossingBSSideA/B</c> (47,5),(48,5)
/// - four out of four. A mesh bounding box would <i>not</i> have shown this: bounds are node-local and
/// say only how big a mesh is, never where it stands. See ParkFixedItemsTests.
/// </para>
///
/// <para>
/// <b>Five of the six fixed items are here; end is not.</b> None of the vehicles is scenery: the bus spawns at
/// cell (29.7,-11.5) and the ferry at (90.0,-17.6), both off the map on negative depth, and the seaplane off it on
/// negative x; end.wad is three aircraft 59 units in the air. Each vehicle runs its own <c>.RSE</c> in
/// <see cref="RideScript"/> and travels the Bezier route authored in its model file (<see cref="ModelFile.Paths"/>,
/// which <see cref="LobbyModel"/> takes up): not one of the nine vehicle clips carries a position track
/// (<c>Endm1/2/3</c> do, which is the control). <c>ParkPeople.StepVehicle</c> releases them round their circuits.
/// </para>
///
/// <para>
/// <b>Ferry and seaplane travel on <c>TRIGWAITANIM</c>, which this class is what made buildable.</b>
/// Their scripts start every animation with it where <c>bus.RSE</c> uses plain <c>TRIGANIM</c>, so while
/// that opcode had no case the two stood at the end of their routes with nothing ever triggered - the
/// one visible difference between a vehicle that drives and two that do not. It compares against a raw
/// operand and parks the script for ever when no model is bound, which is why it had to wait for the
/// binding this class does; see <see cref="RideScript"/> for the handler and its one deviation.
/// </para>
///
/// <para>
/// <b>These hold still until something asks them to move, and that is the point.</b> Both are scripted
/// things in the original - the archives ship <c>Gates.RSE</c> and <c>lights.RSE</c> beside the models -
/// so what they do belongs to an animation player a script triggers, exactly as a placed thing's does;
/// see <see cref="ParkObjects.Sweep"/>. What happened instead was that <see cref="LobbyModel"/> picked up
/// the companions with an M1, M2... suffix - the jungle archive holds <c>gatesm1</c>, <c>gatesm2</c> and
/// <c>gatesm3</c>, matched case-insensitively - and looped them on a clock of its own, so the gate swung
/// open and shut for ever with nothing having asked for it. That is the opposite of the terrain's own
/// <c>basem.MD2</c>, whose bare <c>m</c> nothing picks up.
/// </para>
///
/// <para>
/// <b>Both now run the script they ship, and the two turn out to want opposite things.</b> The save's own
/// header names which thing is the gate and which is the lights, so each is registered into
/// <see cref="ParkObjects"/> under that id and <see cref="ParkRides"/> binds its <c>.RSE</c> against it -
/// which is what the engine does, by a route of its own: it never reads these two out of the save's
/// placements at all, but searches the item descriptions by name and builds each one through the ordinary
/// object constructor.
/// </para>
///
/// <para>
/// <b>The gate moves when it is commanded, and the lights never do.</b> <c>Gates.RSE</c> idles on
/// <c>VAR_COMMAND</c>, which <see cref="ParkRides"/> writes from the save's <c>mParkClosed</c> as the park
/// loads (1 opens, 2 shuts), as the original's <c>FUN_00519ef0</c> does when a park opens or closes. <c>lights.RSE</c> is the opposite, starting an
/// unconditional <c>LOOPANIM</c> as its second instruction - but <b>both of the clips it loops declare ten
/// frames and carry not one track</b>, so a correctly wired crossing spins a channel for ever while nothing
/// on screen can move. Whatever changes the lamps is not in those clips. That is measured, and it is also
/// why the old suffix-matching loop was only ever visible on the gate.
/// </para>
///
/// <para>
/// <b>The gate half of that is three themes out of four rather than a rule.</b> Every theme ships its own
/// <c>Gates.RSE</c> and all four differ; jungle, fantasy and hallow open on <c>TEST VAR_COMMAND</c> and idle,
/// but <b>space opens with an unconditional <c>LOOPANIM_CH</c></b> before it reaches any test at all. Its gate
/// holds still here regardless, and for a second reason worth knowing rather than relying on:
/// <c>LOOPANIM_CH</c> is one of the opcodes this interpreter does not implement, so it is counted rather than
/// obeyed, and implementing it would set that gate moving. The lights are the safe generalisation instead -
/// <c>lights.RSE</c> is byte-identical across all four themes. See ParkFixedItemsTests, which pins both.
/// </para>
/// </summary>
public sealed class ParkFixedItems : Entity
{
	/// <summary>The theme folder these were loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	/// <summary>
	/// The park's fixed items, for the debug console to ask questions of - the same idiom as
	/// <see cref="ParkPeople.Current"/>. Null outside a park.
	/// </summary>
	public static ParkFixedItems? Current { get; private set; }

	private readonly List<LobbyModel> _models = [];

	// Item names, kept beside _models rather than derived from Items, because an item that would not
	// load adds neither and the two lists would drift apart.
	private readonly List<string> _modelNames = [];

	// Which thing each fixed item was stood as, by the item's own name.
	private readonly Dictionary<string, int> _thingOf = [];

	/// <summary>
	/// The thing this park stood <paramref name="item"/> as, or nought where it stood none. The three
	/// vehicles are the ones worth asking for: two of them carry ids the save never gave them, so
	/// there is nowhere else to look them up.
	/// </summary>
	internal int ThingFor( string item ) => _thingOf.GetValueOrDefault( item );

	/// <summary>
	/// What the arrival manager's choice of vehicle means here - 1, 2 and 3 are the bus, the seaplane
	/// and the ferry, in the order the save's own <c>mArrivalVehicle_Size1..3</c> name them.
	/// </summary>
	internal static string VehicleName( int vehicle ) => vehicle switch
	{
		2 => "seaplane",
		3 => "ferry",
		_ => "bus"
	};

	/// <summary>
	/// The gate's two sign panels, kept only so that they can be let go of again. They are painted
	/// for this park rather than loaded by path, so nothing else holds them - see <see cref="OnDelete"/>.
	/// </summary>
	private IReadOnlyDictionary<string, Texture>? _sign;

	/// <summary>
	/// The items to load, by the name of both the archive and the model inside it, whether that model
	/// carries the park's name board, and which of the save header's two handles names the thing it is.
	/// Only the gate carries a board; the lights name no sign material, so offering them one would be noise.
	///
	/// <para>
	/// The names are the engine's own: it looks for exactly <c>"gates"</c> and <c>"lights"</c> among the item
	/// descriptions, which is why these are spelled out here rather than derived from the catalogue. The ids
	/// those handles hold are <i>not</i> catalogue numbers but thing ids - 11 and 12 in the shipped park.
	/// </para>
	/// </summary>
	private static readonly (string Name, bool CarriesSign, Func<ParkWorld, int> Thing)[] Items =
	[
		("gates", true, world => world.ParkGates),
		("lights", false, world => world.TrafficLights),

		// The three vehicles, which are not scenery - see the remarks on this class. Lost Kingdom's save
		// names only the bus; the ferry and the seaplane are given ids below and stood anyway, so all three
		// run their own scripts. The original makes a vehicle only when a crowd first needs it -
		// docs/QUEUE.md Q26.
		("bus", false, world => ThingByCatalogue( world, BusCatalogueId )),
		("ferry", false, world => ThingByCatalogue( world, FerryCatalogueId )),
		("seaplane", false, world => ThingByCatalogue( world, SeaplaneCatalogueId ))
	];

	/// <summary>
	/// The catalogue numbers the fixed items carry in their own <c>.sam</c>, read off the shipped files:
	/// 1600 Bus, 1601 Gates, 1602 Seaplane, 1603 Lights, 1604 Ferry, 1605 End.
	/// </summary>
	private const int BusCatalogueId = 1600;

	private const int SeaplaneCatalogueId = 1602;

	private const int FerryCatalogueId = 1604;

	/// <summary>
	/// The id of the thing this park holds for a catalogue number, or nought where it holds none.
	///
	/// <para>
	/// <b>A vehicle is found this way and not by a header handle, which is a trap worth naming.</b> The
	/// gate and the lights each have a field of their own in the save header - <c>mParkGates</c> and
	/// <c>mTrafficLights</c> - and the bus does not. What it has instead is <c>mFirstObject</c>, which in
	/// the shipped park happens to hold 15, which happens to be the bus. That is a <b>coincidence of list
	/// order, not an identity</b>: <c>mFirstObject</c> is the head of the object linked list, walked from
	/// by <c>FUN_004fcb10</c>, sitting beside <c>Used_Thing_Head</c>/<c>Used_Thing_Next</c> in the header
	/// and with siblings <c>mFirstEntertainer</c>, <c>mFirstGuard</c> and <c>mFirstResearcher</c> - and
	/// thing 10's own <c>mNextObject</c> is 15, which is what makes it the head. Reading it as "the bus"
	/// would have looked right in Lost Kingdom and been wrong in every other park.
	/// </para>
	///
	/// <para>
	/// The catalogue number is the item's own <c>Info.Id</c> (the save calls it <c>mId</c>), so this is
	/// the same question the engine asks when it builds these by name out of the item descriptions rather
	/// than from the save's placements.
	/// </para>
	/// </summary>
	/// <remarks>
	/// Internal rather than private only so that it can be tested, the same reason
	/// <see cref="LobbyModel.LoadAnimations"/> is. A test that re-derived this lookup over
	/// <c>world.Objects</c> would pass just as well with the <c>mFirstObject</c> mistake put back, which
	/// is the shape of test that proves nothing - so the test calls this.
	/// </remarks>
	internal static int ThingByCatalogue( ParkWorld world, int catalogueId )
	{
		foreach ( var placed in world.Objects )
		{
			if ( placed.CatalogueId == catalogueId )
				return placed.ThingId;
		}

		return 0;
	}

	/// <summary>
	/// How many animation players this fixed item's own description asks for -
	/// <see cref="ItemDescriptionFile.NumSimultAnims"/>, which is what the engine hands its model loader.
	///
	/// <para>
	/// <b>The park gate declares two, and nothing here would notice if it were read as one</b>: the only
	/// channel instruction <c>Gates.RSE</c> carries is a single <c>LOOPANIM_CH</c> naming channel nought.
	/// It is read because it is the item's own number, not because a fault forced it - and a fixed item
	/// that will not describe itself still gets the one player the engine floors a nought count to.
	/// </para>
	/// </summary>
	private int ChannelsFor( string directory, string stem )
	{
		try
		{
			using var stream = FileSystem.OpenRead( $"{directory}/{stem}.sam" );

			return new ItemDescriptionFile( stream ).NumSimultAnims;
		}
		catch ( Exception )
		{
			return 1;
		}
	}

	/// <param name="world">
	/// The park's own save, or null where the theme ships none - three of the four do not. It is asked for
	/// one thing only: the two thing ids its header names, which are what a script is bound against.
	/// </param>
	/// <param name="objects">
	/// What is already standing in this park, so these two are swept along with it rather than keeping a
	/// clock of their own - see <see cref="ParkObjects.Stand"/>. Null leaves them standing and inert, which
	/// is what a theme with no park file gets.
	/// </param>
	public ParkFixedItems( string themeName, ParkWorld? world = null, ParkObjects? objects = null )
	{
		ThemeName = themeName;
		Name = $"{themeName} fixed items";
		Current = this;

		var features = $"levels/{themeName.ToLowerInvariant()}/features";

		foreach ( var (item, carriesSign, thingOf) in Items )
		{
			// Each .wad stands in for a directory of its own name, the same way terrain.wad gives the
			// terrain its terrain/ paths.
			var directory = $"{features}/{item}";

			try
			{
				if ( carriesSign )
					_sign = BuildSign( directory, themeName );

				// The twelve roles this item's archive ships, read exactly as a placed thing's are. The model
				// is bound against all of them because an animation player names a role outright: the gate's
				// own script asks for role 5 entries 0, 1 and 2, and a probe for a numbered run would have
				// found them by luck rather than because they were asked for.
				var animations = RideAnimations.Load( directory, item, FileSystem, ChannelsFor( directory, item ) );

				var model = new LobbyModel(
					$"{directory}/{item}.MD2",
					$"{directory}/textures",
					Vector3.Zero,
					textureOverrides: carriesSign ? _sign : null,
					clips: animations.AllClips );

				_models.Add( model );
				_modelNames.Add( item );

				// And into the one registry this park sweeps, under the thing id the save's own header
				// gives it.
				var thing = world == null ? 0 : thingOf( world );

				// <b>A vehicle the save does not name is given an id here and stood anyway.</b> The save
				// records a vehicle only once a crowd of that size has arrived, because the engine makes
				// the thing the first time it needs one (FUN_0051a2f0) rather than shipping it - so Lost
				// Kingdom names a bus and neither a ferry nor a seaplane. Without an id nothing binds
				// their scripts and they cannot move, which is exactly what left those two parked.
				//
				// The id counts DOWN from the top of the ushort the save keeps thing ids in, because
				// ParkPeople.Admit hands arriving guests ids UP from one past the highest the file used.
				// Two allocators sharing one numbering would eventually collide; these cannot meet. It is
				// derived from the row rather than counted, so a vehicle keeps the same id every load.
				var catalogueId = item switch
				{
					"bus" => BusCatalogueId,
					"ferry" => FerryCatalogueId,
					"seaplane" => SeaplaneCatalogueId,
					_ => 0
				};

				if ( thing == 0 && catalogueId != 0 )
					thing = ushort.MaxValue - Array.FindIndex( Items, row => row.Name == item );

				// The catalogue number goes with it, so that a vehicle the save never named can still be
				// given a script - see ParkObjects.Stood. The gates and the lights carry theirs too and
				// lose nothing by it: they are in the save's object list already, so ParkRides binds them
				// on its first pass and the second one steps over them.
				if ( thing != 0 )
					objects?.Stand( thing, model, animations, catalogueId );

				// Kept by name, so that whatever drives a vehicle can find the thing it has to command.
				// A dictionary rather than a list beside _models, because this one is asked by name and
				// two lists that must stay in step are two lists that can fall out of it.
				if ( thing != 0 )
					_thingOf[item] = thing;

				// Said out loud because two of these carry ids this park's save never gave them, and
				// whether they were stood at all is otherwise only visible by their not moving - which
				// looks identical to being stood and never driven.
				Log.Info( $"{themeName}: fixed item '{item}' is thing {thing}"
					+ (catalogueId == 0 ? "" : $", catalogue {catalogueId}")
					+ (thing == 0 ? " - NOT STOOD" : "") );
			}
			catch ( Exception e )
			{
				// A theme missing one of these is a broken installation, but the park is still worth
				// looking at without it - and saying which one is missing beats an empty entrance.
				Log.Warning( $"{themeName}: fixed item '{item}' would not load, so it is missing from the park - {e.Message}" );
			}
		}
	}

	/// <summary>
	/// What routes each fixed item loaded. A model reads its routes when it loads, so a reader
	/// looking at the wrong offset shows up here as "no route" rather than as a crash - which is why
	/// this prints the point count and the first point rather than merely saying yes.
	/// </summary>
	public IEnumerable<string> PathCensus()
	{
		for ( int i = 0; i < _models.Count; ++i )
		{
			var name = i < _modelNames.Count ? _modelNames[i] : "?";
			var routes = _models[i].Paths;

			if ( routes.Count == 0 )
			{
				yield return $"{name}: no route";
				continue;
			}

			for ( int r = 0; r < routes.Count; ++r )
			{
				var route = routes[r];
				var shape = route.IsBezier ? "bezier" : route.IsStraight ? "straight" : "unknown";
				var first = route.Points.Length > 0 ? route.Points[0] : Vector3.Zero;

				yield return $"{name}: route {r} type {route.Type} {shape} {route.Points.Length} points,"
					+ $" first ({first.X:F1}, {first.Y:F1}, {first.Z:F1})";
			}

			// What moves a thing along that route is not in the model but in its clips - channel 0x200,
			// a percentage of the route per frame. A route with no scalar is a thing that cannot move,
			// which is worth saying out loud rather than leaving to be inferred from a still bus.
			var scalars = 0;
			var low = float.MaxValue;
			var high = float.MinValue;

			foreach ( var clip in _models[i].Clips )
			{
				foreach ( var track in clip.PathTracks )
				{
					++scalars;

					foreach ( var value in track.Values )
					{
						if ( value < low )
							low = value;

						if ( value > high )
							high = value;
					}
				}
			}

			yield return scalars == 0
				? $"{name}: no progress scalar - it cannot move"
				: $"{name}: {scalars} progress scalar(s), {low:F1}..{high:F1}, {high - low:F1} of the route";

			// Where it actually is, right now. This is the line that says whether a thing MOVES rather
			// than merely having a route to move along: poll it twice and compare. A route read
			// correctly and never applied looks identical to one applied, in every other line here.
			// Every mesh, with the three things that decide whether it is DRAWN - see the gate in
			// ModelEntity.Render: no model, no opacity, or drawn by its owner. A right-looking
			// position over an empty road is what sent this line here: the census was reporting
			// where a thing was without ever saying whether there was anything to see.
			for ( int mesh = 0; mesh < _models[i].Entities.Length; ++mesh )
			{
				var entity = _models[i].Entities[mesh];
				var at = entity.Position;

				yield return $"{name}: mesh {mesh} at ({at.X:F1}, {at.Y:F1}, {at.Z:F1})"
					+ $" model={(entity.Model == null ? "NULL" : "yes")}"
					+ $" translucent={(entity.TranslucentModel == null ? "null" : "yes")}"
					+ $" opacity={entity.Opacity:F2} ownerDrawn={entity.DrawnByOwner}";
			}
		}
	}

	/// <summary>
	/// Lets go of the gate's sign panels, for the reason LobbyIsland.OnDelete gives: a material
	/// leaves the textures bound into it alone because they are usually cached by path and shared,
	/// and these are neither.
	/// </summary>
	protected override void OnDelete()
	{
		// Before the early return below, not after it: a park whose sign never built still has to stop
		// being the one the console reaches, or a deleted park answers for the live one.
		if ( Current == this )
			Current = null;

		if ( _sign == null )
			return;

		foreach ( var panel in _sign.Values )
			panel.Delete();
	}

	/// <summary>
	/// The gate's name board, painted the same way the lobby paints the one on its islands: the
	/// artwork comes out of the gate's own <c>.sgn</c> and the lettering is rasterised over it, then cut
	/// into the two 128x128 panels the gateway mesh draws as 'sign1' and 'sign2'.
	///
	/// <para>
	/// Null leaves the model's own textures in place, which is what a theme wants when the paint fails:
	/// hallow ships real sign1.wct and sign2.wct beside its model, and the other three ship none, so
	/// those would fall back to the not-found texture instead. Overrides are taken ahead of the ones on
	/// disk, so this wins wherever it succeeds.
	/// </para>
	/// </summary>
	private static IReadOnlyDictionary<string, Texture>? BuildSign( string directory, string themeName )
	{
		var parkName = ParkDisplayName( themeName );

		if ( string.IsNullOrEmpty( parkName ) )
			return null;

		if ( !SignTexture.TryBuild( $"{directory}/gates.sgn", parkName, out var left, out var right ) )
			return null;

		Log.Info( $"{themeName}: painted '{parkName}' onto the park gate's sign" );

		return new Dictionary<string, Texture>( StringComparer.OrdinalIgnoreCase )
		{
			["sign1"] = left!,
			["sign2"] = right!
		};
	}

	/// <summary>
	/// What a park is called on its own gate, read from the shipped strings rather than written out
	/// here: <c>Language/English/THEMENAMES.str</c> holds exactly four entries, and they are the four
	/// park names.
	///
	/// <para>
	/// The order is the one below and was read out of the file's own header rather than assumed - the
	/// four records declare lengths 12, 15, 11 and 10 with their spaces at index 4, 9, 6 and 5, which
	/// only "Lost Kingdom", "Halloween World", "Wonder Land" and "Space Zone" fit. BFST stores indices
	/// into MBToUni.dat rather than text, so no plain search of the file shows this.
	/// </para>
	///
	/// <para>
	/// <see cref="LobbyIsland"/> keeps its own hardcoded copy of these four names. That is worth
	/// replacing with this read, but it is the lobby's to change and not this entity's errand.
	/// </para>
	/// </summary>
	private static string ParkDisplayName( string themeName )
	{
		var index = Array.FindIndex( ThemeOrder, name => string.Equals( name, themeName, StringComparison.OrdinalIgnoreCase ) );

		if ( index < 0 )
			return string.Empty;

		try
		{
			var names = new StringFile( "Language/English/THEMENAMES.str" );

			return index < names.Entries.Length ? names[index] : string.Empty;
		}
		catch ( Exception e )
		{
			Log.Warning( $"{themeName}: THEMENAMES.str would not read, so the gate keeps a blank sign - {e.Message}" );
			return string.Empty;
		}
	}

	/// <summary>The order THEMENAMES.str lists the parks in - see <see cref="ParkDisplayName"/>.</summary>
	private static readonly string[] ThemeOrder = ["jungle", "hallow", "fantasy", "space"];

	// There is deliberately no OnUpdate here. These models are kept in _models so that this entity owns
	// them for as long as the park stands, not so that anything drives them: see the remarks on this class
	// for why a gate that swings itself open is a clock this program invented rather than behaviour the
	// original has.
}
