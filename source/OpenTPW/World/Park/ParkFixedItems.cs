using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A park's fixed items: the gate standing over the entrance, the traffic lights at the two
/// pedestrian crossings, and the bus at its spawn off the map. They are "fixed" in the original's own
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
/// <b>Three of the six fixed items are here; ferry, seaplane and end are not, yet.</b> None of the
/// four is scenery: the bus sits at cell (29.7,-11.5) and the ferry at (90.0,-17.6), both off the map
/// on negative depth, the seaplane off it on negative x, and end.wad is three aircraft 59 units in the
/// air. They are vehicles parked at their spawns, driven by the <c>.RSE</c> scripts - whose runtime
/// <b>is</b> built, in <c>RideScript</c>. This paragraph once said that runtime was not built and
/// pointed at <c>World/Ride.cs</c>, which is itself dead code.
/// </para>
///
/// <para>
/// <b>The bus is here so that its script is BOUND, and it still does not move.</b> That is the whole of
/// what this step buys, and it is deliberately small: <c>bus.RSE</c> is 46 instructions over 16 distinct
/// opcodes and every one of those sixteen has a case in <see cref="RideScript"/>, so the interpreter
/// should run it without reaching the counted default - which had never actually been tested against a
/// vehicle. What it cannot do yet is travel. Not one of the nine vehicle clips carries a position
/// track (<c>Endm1/2/3</c> do, which is the control), because a vehicle's route is not in its clips at
/// all: it is a Bezier path authored in the model file, and neither <see cref="ModelFile"/> nor the
/// animation player reads it yet. So the bus stands at its spawn, off the map and out of sight, running
/// its script - and is confirmed by the <c>rides</c> census rather than by eye.
/// </para>
///
/// <para>
/// <b>Ferry and seaplane are held back on purpose, and the order matters.</b> Their scripts need
/// <c>TRIGWAITANIM</c>, which with no model bound compares against a raw operand and parks the script
/// for ever - so implementing it before a model is bound would hang the great majority of its shipped
/// uses. It is safe only after the binding this class does.
/// </para>
///
/// <para>
/// <b>These hold still until something asks them to move, and that is the point.</b> Both are scripted
/// things in the original - the archives ship <c>Gates.RSE</c> and <c>lights.RSE</c> beside the models -
/// so what they do belongs to an animation player a script triggers, exactly as a placed thing's does;
/// see <see cref="ParkObjects.Sweep"/>. What happened instead was that <see cref="LobbyModel"/> picked up
/// the companions with an M1, M2... suffix - the archive holds <c>gatesm1</c>, <c>gatesm2</c> and
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
/// <b>Neither of them moves yet, and each is still for its own reason.</b> <c>Gates.RSE</c> idles: with its
/// variables at nought it cycles five instructions round its dispatch loop, reaching no animation until
/// something writes <c>VAR_COMMAND</c> - and in the original the only thing that ever does is opening or
/// closing the park, which this program has no concept of. <c>lights.RSE</c> is the opposite, starting an
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

		// The three vehicles, which are not scenery - see the remarks on this class.
		//
		// ONLY THE BUS MOVES, and that is the shipped park's doing rather than a gap here: Lost Kingdom
		// names things for catalogue 1600, 1601 and 1603 and for nothing else, so ThingByCatalogue
		// answers nought for the ferry and the seaplane, no script is bound to them, and they stand at
		// their spawns. Measured, not assumed: across two polls of a live park the bus went
		// (647.4, -67.8) -> (510.5, 65.5) while the ferry held at (899.5, -176.0, -10.0) and the
		// seaplane at (-2.2, 174.5, 88.8), both exactly their rest pose.
		//
		// They are loaded anyway because their routes and their progress scalars read correctly, so
		// whatever ends up driving them needs no more of this class - see docs/PLAYER-GAPS.md.
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
				if ( thing == 0 && item is "bus" or "ferry" or "seaplane" )
					thing = ushort.MaxValue - Array.FindIndex( Items, row => row.Name == item );

				if ( thing != 0 )
					objects?.Stand( thing, model, animations );
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
