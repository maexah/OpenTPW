using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }

	/// <summary>A park's lighting, or null where the scene is not a park - see <see cref="OpenTPW.ParkLight"/>.</summary>
	public ParkLight? ParkLight { get; set; }

	/// <summary>
	/// What distance fades to. Static so the shaders' per-draw uniform fill can reach it the same
	/// way it reaches the sun.
	///
	/// <see cref="Sky"/> keeps it at roughly what the sky comes to where it meets the horizon, so
	/// a distant island on a storming Halloween hazes into the storm rather than into a bright
	/// blue day. It is also what the frame is cleared to, so the gaps between the sky's cloud
	/// layers show sky rather than black. The value below is only what the first frame uses -
	/// it is the constant the original's lobby fogs with, 0xFF44DDFF.
	/// </summary>
	public static Vector3 FogColour { get; set; } = new( 0x44 / 255f, 0xDD / 255f, 0xFF / 255f );

	/// <summary>
	/// How thick the haze is - the multiplier on the shaders' <c>exp( depth * 0.01 )</c>, so it is
	/// what the fade is at zero distance and it reaches full strength around three hundred and
	/// seventy units out. Clear up close and a wall at the horizon, which is the shape this has
	/// always had.
	/// </summary>
	public static float FogDensity { get; set; } = 0.025f;

	public SettingsFile Global { get; private init; }

	/// <summary>
	/// A park's balance numbers - the theme's own Standard.sam over the global one - or null in the
	/// lobby. Nothing reads <see cref="Global"/> (Q74). Settable rather than init-only because it is
	/// built during scene setup rather than in the constructor's own body.
	/// </summary>
	public ParkBalance? Balance { get; private set; }

	/// <summary>The read-only starting world, from a file or fresh initialization; null in the lobby.
	/// Running numbers and changed cells belong to <see cref="ParkState"/>.</summary>
	public IParkInitialState? Park { get; private set; }

	/// <summary>
	/// The park as it is being <i>played</i>, seeded once from <see cref="Park"/> - the balance that
	/// moves, the visitor count, and the cells anything can change. Null in the lobby.
	///
	/// <para>
	/// <b>The level owns it because nothing smaller can.</b> The guests move the money, the interface
	/// shows it, and a handyman will read the cells; a number kept by any one of those is a number the
	/// others cannot see, which is exactly what it replaces - see <see cref="ParkState"/>.
	/// </para>
	/// </summary>
	public ParkState? ParkState { get; private set; }

	/// <summary>Everything this theme sells, for as long as the park is up - see <see cref="ParkBuilding"/>.</summary>
	public ParkItemCatalogue? Catalogue { get; private set; }

	/// <summary>Which of those items the park has researched, the save's word first - see <see cref="ParkResearch"/>.</summary>
	public ParkResearch? Research { get; private set; }

	/// <summary>
	/// Whether the park is Instant Action's, the original's game type 2: the balance lays <c>Easy_Standard.sam</c> and
	/// the catalogue each item's <c>Easy_</c> file, both on this one condition.
	/// </summary>
	/// <remarks>A console park with nobody selected retains Instant Action for existing diagnostics.</remarks>
	internal static bool InstantAction => Players.Roster.Current?.InstantAction ?? true;

	/// <summary>Who the park may hire - see <see cref="ParkStaffPool"/>.</summary>
	public ParkStaffPool? StaffPool { get; private set; }

	/// <summary>
	/// Which of the game's two worlds this is. The original runs them as separate states of one
	/// machine - the lobby is states 1/2/3 and a park is 9/10/0xb - and they share almost nothing but
	/// the renderer, the sound device and the player's own files.
	///
	/// <para>
	/// This is the smallest form of that split: enough for a park to be built instead of the lobby,
	/// and no more. Teardown ordering and the state machine proper are still the lobby's.
	/// </para>
	/// </summary>
	public enum Scene
	{
		Lobby,
		Park
	}

	public Scene Kind { get; private init; }

	/// <summary>
	/// This scene's windows, kept so <see cref="PausedByWindow"/> can ask whether any of them holds the
	/// world. Null until the HUD is built, and in any scene that has no window stack at all.
	/// </summary>
	private WindowStack? _windows;

	/// <summary>The park file this park was asked to be built from, or null for the one it is entered with.</summary>
	private readonly string? _parkFile;

	/// <summary>The theme folder this level was built from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; private init; }

	/// <param name="parkFile">A park file in the save folder to build the park from, the Load Park screen's; null for the one the park is entered with.</param>
	public Level( string levelName, Scene kind = Scene.Lobby, string? parkFile = null )
	{
		_parkFile = parkFile;

		// Lower-cased once here rather than at each place that builds a path out of it. The two ways
		// into a park disagree about case: the debug console passes "jungle" and the island panel
		// passes the island's own name, "Jungle". Everything downstream lower-cases anyway, so the
		// only thing that actually differed was the log - but a name that is sometimes capitalised is
		// a trap set for the first thing that ever keys on it.
		ThemeName = levelName.ToLowerInvariant();
		Kind = kind;

		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		// The seconds this scene is about to spend loading are not ticks anybody owes - see GameClock.Rebase,
		// and the original doing the same as it enters a park (0x0054ed7c).
		GameClock.Rebase();

		// And the calendar starts over with it, from nought, where the original zeroes its world-tick
		// counter at park init (0x00515865) and then loads the save's - see GameCalendar.Rebase. Unconditional rather than park-only so a lobby cannot be left showing the
		// date of the park before it; only a park ever advances it - see the Update below.
		GameCalendar.Rebase();

		if ( kind == Scene.Park )
		{
			SetupParticles();
			SetupParkEntities();
			SetupParkHud();

			return;
		}

		SetupEntities();
		SetupParticles();
		SetupHud();
	}

	private void SetupEntities()
	{
		// The sun stands in front of the park gates, which is where every gate in the lobby faces.
		//
		// Measured rather than guessed: a gate is authored in its island's own model space and is
		// never rotated (LobbyGate only sets a position), so the offset from an island's centre to
		// its gate's centre is the direction that gate faces. Through the engine's own parser that
		// comes to bearings of 187, 152, 187 and 198 degrees for jungle, fantasy, hallow and space -
		// all of them the island's -Y side. Fantasy reads furthest off only because its gate is a
		// 421-vertex worm whose bulk pulls the centroid sideways; its parts sit at -8.5 and -13.3 in
		// Y with the rest. So one light can face all four, and this is it.
		//
		// The shader takes this as a point, not a direction, so how far away it stands decides how
		// alike the four islands are lit. They span 283 units corner to corner, so the spread in
		// direction across them is atan(283/distance): at four thousand units it is four degrees,
		// which reads as a sun rather than as a lamp standing in the sea. The height is that
		// distance at an elevation of thirty-five degrees.
		//
		// <b>This is a choice, not a recovery.</b> The original's world geometry arrives at the card
		// already transformed, carrying a colour per vertex that its own engine worked out
		// (FVF 0x1c4, with DIFFUSE and SPECULAR), so Direct3D does no lighting for it and there may
		// be no sun position in there to find. Alexah's call, from how the game looks: lit from the
		// front, facing the park gates.
		//
		// Water reads this too, so the sea takes its shading from the same place.
		SunLight = new Sun() { Position = new( 500, -3500, 2800 ) };

		// The sky first, because it never writes depth: it has to be laid down before anything
		// that should cover it, and the ocean runs out further than the sky's own horizon does.
		_ = new Sky();

		_ = new Water() { Scale = new Vector3( 10000f ) };

		// Each island reads its own script and brings its gate and its FLYINGMESH swarm with it.
		_ = new LobbyIsland( new Vector3( 400, 400, 0 ), "Jungle" );

		// Positions come from lobby.wad's own lobby.txt, which lists one ISLANDCAMERAPOSITION per
		// island index: 0 (400,400), 1 (600,400), 2 (600,600), 3 (400,600). Each park's script
		// gives its index - jungle 0, fantasy 1, hallow 2, space 3.
		_ = new LobbyIsland( new Vector3( 600, 400, 0 ), "Fantasy" );
		_ = new LobbyIsland( new Vector3( 600, 600, 0 ), "Hallow" );
		_ = new LobbyIsland( new Vector3( 400, 600, 0 ), "Space" );

		// One weather system for the whole lobby, taking its cue from whichever island the
		// camera is on - see LobbyWeather.
		_ = new LobbyWeather();

		// ...and one sound system, which takes its cue from the same place. Built after the
		// islands so the first frame already has one to play, and with each group of sound at the
		// volume the options give it.
		GameOptions.Current.ApplySound();
		_ = new LobbyAudio();

		// The advisor rides on top of that, ducking everything above while he talks. What he says is
		// handed to him by the front end, which is built with the HUD below.
		_ = new Advisor();

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	/// <summary>
	/// A park: its own sky, the ground's scenery, the things placed on it, its rides, people and staff, its
	/// sound, weather and advisor, and something to look at it with.
	///
	/// <para>
	/// It is the theme's own throughout - its balance numbers, its sun and its sky - over <c>base.MD2</c> out
	/// of the park's own terrain.wad, through the same model and texture path the lobby already uses.
	/// </para>
	/// <para>
	/// The advisor says a single line - see <see cref="UI.ParkLines"/> - rather than the scored queue of them
	/// the original runs.
	/// </para>
	/// </summary>
	private void SetupParkEntities()
	{
		// The theme's own numbers, global defaults underneath - see ParkBalance for why that stack
		// matters more than it looks - and Instant Action's over them (see InstantAction).
		// What each phase below costs, in milliseconds - see LoadTimer for why this is left on where
		// the frame profiler is not.
		var load = new LoadTimer( ThemeName );

		Balance = new ParkBalance( ThemeName, easyMode: InstantAction );
		Log.Info( $"{ThemeName}: balance stack came to {Balance.Count} keys" );
		load.Mark( "balance" );

		// What distance fades to. The lobby's Sky rewrites this every frame from its own horizon,
		// because that horizon moves with whichever park the camera is on; a park's sky is untinted and
		// deliberately leaves this alone, so setting it once here holds. Jungle and fantasy are a pale
		// blue within a few points of the lobby's own constant, hallow is nearly black and space is
		// orange.
		FogColour = Balance.Colour( "ThemeEngine.FogColour", FogColour );

		// The park's light, as the original lights it (FUN_005741b0; docs/exe/park-engine.md, "The
		// lighting model"): every vertex gets the ambient colour plus the sun's colour times its facing
		// to the sun, each channel clamped. LightNormal is the direction the light travels - the original
		// normalises it and never negates it - written in the original's Y-up axes, so it swaps into this
		// engine's Z-up space the way a model's vertices do. All three are read once, as the original
		// reads them once in state 9 (FUN_004080e0, applied by FUN_00458590).
		var lightNormal = Balance.Vector( "LightNormal", new Vector3( 0.4f, -0.8f, 0.4f ) );
		var travels = new Vector3( lightNormal.X, lightNormal.Z, lightNormal.Y ).Normal;

		ParkLight = new ParkLight( Balance.Colour( "ThemeEngine.AmbientLightLevel", new Vector3( 0.333f ) ), travels );
		Log.Info( $"{ThemeName}: lit per vertex by ambient {ParkLight.Ambient} and a sun travelling {travels}" );

		SunLight = new Sun()
		{
			// Nothing in a park reads the position: the park's lighting takes only the direction. It is
			// kept because ModelEntity hands it to the shader with every draw, and stands far along the
			// sun's direction so that anything reading it would see the same sun.
			Position = new Vector3( 480f, 425f, 0f ) - (travels * 4000f),

			// ThemeEngine.DirectionalLightLevel, 0xFFFFFFD8 for every park the game ships - a white
			// barely warmed at the blue end.
			Color = Balance.Colour( "ThemeEngine.DirectionalLightLevel", Vector3.One )
		};

		// The park's own sky, and it is a real one: every theme ships a sky/ folder holding the same
		// three textures the lobby's does, and the original loads them through the same loader -
		// FUN_005852b0, called by the lobby at 0x005d8bac and by a park's level load at 0x00407f95,
		// which state 9 reaches through FUN_00407e00 (0x0054ed3f).
		//
		// Centred on the world origin at 300, because that is where the sky object puts itself
		// (FUN_00584ef0 writes both, FUN_005856c0 puts them back) and NOTHING re-centres it for a park:
		// the setter's only caller is the lobby, and the rebuild's only other caller is engine init.
		// Untinted, because a park has no island script and so no SKYCOLOUR - see Sky's constructor for
		// everything that decides.
		//
		// First, as in the lobby, because the sky never writes depth and has to go down before anything
		// that should cover it.
		_ = new Sky( $"levels/{ThemeName}/sky", centre: Vector3.Zero, height: Sky.ParkHeight, tinted: false );
		load.Mark( "sky" );

		Catalogue = new ParkItemCatalogue( ThemeName, instantAction: InstantAction );
		Catalogue.RegisterParticleEffects( ParticleSystem.Current?.Library );
		load.Mark( "catalogue" );
		var catalogue = Catalogue;

		var park = CreatePark( ThemeName, Balance, catalogue, _parkFile );
		Park = park;
		ParkState = new ParkState( park )
		{
			RegionEffects = ParkRegionEffects.From( Balance ),
			AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) )
		};
		Log.Info( $"{ThemeName}: the park's clock starts at mGameTick {ParkState.GameTick}" );
		// Saved advisor histories are not restored yet; fresh histories follow FUN_00599e70.
		if ( park?.Save != null ) Unimplemented.Report( "PARK_ADVISOR_SAVED_HISTORY" );
		load.Mark( "park state" );

		Research = park == null || catalogue == null ? null : new ParkResearch( park.ObjectControlRecords, catalogue );

		_ = new ParkGround( ThemeName, park );
		load.Mark( "ground" );

		_ = new ParkPaths( ThemeName, park );
		load.Mark( "paths" );

		_ = new ParkQueues( ThemeName, park );
		load.Mark( "queues" );

		// The queue tool's squares, drawn over whichever of those three is under them.
		_ = new ParkBuildMarkers();

		_ = new ParkTerrain( ThemeName );
		load.Mark( "terrain" );

		// The objects before the fixed items, which is the one ordering here that matters: the gate and the
		// traffic lights are swept out of the very registry the placed things are swept from, so it has to
		// exist before they can put themselves into it.
		var objects = new ParkObjects( ThemeName, park, catalogue );
		load.Mark( "objects" );

		_ = new ParkFixedItems( ThemeName, park, objects );
		load.Mark( "fixed items" );

		// And the scripts those objects run. After the objects, because a script belongs to something
		// standing in the park rather than the other way round - and now literally so: a script is handed
		// the very animation player its own thing's model is posed from, and its tick loop is what drives
		// the sweep that poses it.
		var rides = new ParkRides( ThemeName, park, catalogue, objects: objects );
		load.Mark( "rides" );

		// The bumper rides' cars, drawn from what the rides' track tick leaves them - so after the rides.
		_ = new ParkBumperBoats( ThemeName, catalogue );

		// And the park's people. After the ground, because a guest stands on the land and has to ask how
		// high it is under them; they are sprites rather than models, so they are nothing to do with the
		// objects above and only need the save that named them.
		// How many banks of each kind the guests draw over, the kid banks capped by the detail file.
		var banks = ParkSpriteBanks.Read( FileSystem, ThemeName, NumKids() );

		_ = new ParkGuestSprites( ThemeName, park, banks );
		load.Mark( "guest sprites" );

		// And what those people want, which is deliberately not the same object as what they look like:
		// the sprites above never run a tick, and this never touches a vertex. It goes after them only
		// for readability - it asks nothing of them.
		// And it is handed the two things the admission states need that the save alone cannot answer: the
		// balance stack, for what a guest will put up with paying, and the gate's own script state, which
		// is what a guest waiting outside is actually waiting on.
		// And the scripts a ride's own turn drives, by the same delegate argument the gate already uses: the
		// people need one script per thing and nothing else from the rides, and the rides are built above
		// this line so they cannot be handed the people instead.
		// Who the park may hire, which is a population of its own and nothing to do with the people
		// already in it - see ParkStaffPool. Built before the park's own people only for readability;
		// neither asks anything of the other.
		StaffPool = StaffPoolFor( park, Balance, ParkState?.GameTick ?? 0 );
		load.Mark( "staff pool" );

		_ = new ParkPeople( park, Balance, () => rides.GateStatus( park ), ParkState, catalogue,
			thingId => rides.Scheduler.Find( rides.ScriptFor( thingId ) ), banks: banks );
		load.Mark( "people" );

		// Each group of sound at the volume the options give it, and then the park's own music - which
		// is the order the original uses too: it registers the park's categories, re-applies the group
		// volumes (0x0054ec9a), and only then plays the music (0x0054ec9f). See ParkAudio for what it
		// does with it afterwards: the crowd drives its level, as the original's does.
		GameOptions.Current.ApplySound();
		_ = new ParkAudio( ThemeName );
		load.Mark( "audio" );

		// After the audio, because a park opens with its weather already rolled and that first roll
		// may want to start the rain straight away.
		_ = new ParkWeather();
		load.Mark( "weather" );

		// The advisor last, as in the lobby: he rides on top of the rest and ducks everything above him
		// while he talks, and the teardown runs in the order things were made, so a park's sound and its
		// weather end before he does. Advisor_Update runs from a park's loop (0x0054f9f9) exactly as it
		// runs from the lobby's (0x0054e6df) - he is the same speaker in both scenes, and what differs is
		// who hands him lines: UI.ParkLines for its screens, ParkAdvisorMessages for its door.
		_ = new Advisor();
		load.Mark( "advisor" );

		load.Done();

		Camera.SetCameraMode<ParkOrbitCameraMode>();
	}

	/// <summary>
	/// Who the park may hire: the save's own pool for a park loaded from one, a rolled one for a park made fresh
	/// (<see cref="ParkStaffPool"/>).
	/// </summary>
	internal static ParkStaffPool StaffPoolFor( IParkInitialState? park, ParkBalance? balance, int gameTick )
		=> new( balance, gameTick: gameTick, saved: park as ParkWorld );

	/// <summary>
	/// The park a theme is entered with: the newest park file in the player's own folder for it, or a world made
	/// fresh where that folder holds none, as the original's (0x0054f12b; <c>docs/exe/saves.md</c>, "Entering a park").
	/// </summary>
	/// <remarks>
	/// <b>A deviation with nobody playing</b>, which only the console's <c>park</c> reaches: the copy of the park that
	/// ships beside the level is read, the file a new Instant Action player's folder is given. The original has a
	/// player on every entry.
	/// </remarks>
	/// <param name="parkFile">
	/// A park file in the save folder to read instead, the row clicked on the Load Park screen (0x005ac5d0). One that
	/// will not read leaves the park with nothing placed in it; the original's failed load leaves its state undefined.
	/// </param>
	internal static IParkInitialState? CreatePark( string theme, ParkBalance balance, ParkItemCatalogue catalogue,
		string? parkFile = null )
	{
		if ( parkFile != null )
			return ReadPark( theme, SaveFileSystem, parkFile );

		if ( Players.Roster.Current is not { } player )
			return ReadPark( theme, FileSystem, $"levels/{theme.ToLowerInvariant()}/Easymode.TPWI" );

		if ( SaveFolder.NewestPark( player.Slot, player.Name, theme.ToLowerInvariant() ) is { } own )
			return ReadPark( theme, SaveFileSystem, own );

		Log.Info( $"{theme}: {player.Name} has no park file for this theme, so the park is made fresh" );
		var park = new FreshPark( theme, balance, catalogue );
		Log.Info( $"{theme}: {park.Census()}" );
		return park;
	}

	/// <summary>
	/// A park file, walked, or null where there is nothing to read.
	///
	/// <para>
	/// Only the jungle ships one beside its level, which is also why Lost Kingdom is the only park a new Instant
	/// Action player's folder is given a park for.
	/// </para>
	/// </summary>
	private static ParkWorld? ReadPark( string themeName, BaseFileSystem files, string path )
	{
		if ( !files.FileExists( path ) )
		{
			Log.Info( $"{themeName}: no park file ships with this theme, so nothing is placed in it" );
			return null;
		}

		Log.Info( $"{themeName}: reading the park from {path}" );

		try
		{
			using var stream = files.OpenRead( path );
			var reader = new SaveReader( stream );
			var world = new ParkWorld( reader.ReadFile(), reader.Preamble );

			if ( world.Problem != null )
				Log.Warning( $"{themeName}: the park file stopped being readable partway - {world.Problem}" );
			else if ( !world.ClosedOnTrailer )
				Log.Warning( $"{themeName}: the park file's world block did not end where it should have" );

			Log.Info( $"{themeName}: park file holds {world.ThingCount} things, {world.Objects.Count} of them objects" );

			return world;
		}
		catch ( Exception e )
		{
			// A park worth looking at without its shops beats no park at all.
			Log.Warning( $"{themeName}: the park file would not read, so the park is empty - {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Writes the running park into the player's folder for the theme as <c>&lt;name&gt;.TPWS</c>, the park file
	/// <c>FUN_005ac610</c> writes (<c>docs/exe/saves.md</c>, "The save" and "Module by module"), and answers where,
	/// or null where nothing was written.
	///
	/// <para>
	/// <b>Three of the writer's five stages are built, and the fourth for the things the file holds</b>
	/// (<see cref="ParkFileWriter"/>): the file the park was loaded
	/// from goes out again with the park's clock, its door, its visitor count, its cash, the camera, the ground
	/// (<see cref="WrittenCells"/>), the people (<see cref="ParkPeople.Written"/>), the pool of candidates
	/// (<see cref="ParkStaffPool.Written"/>), the arrival timer (<see cref="ParkPeople.WrittenArrival"/>) and the
	/// file's objects, scripts and models as they run (<see cref="ParkState.WrittenObjects"/>,
	/// <see cref="ParkRides.Written"/>) written over it; an object bought since the load is written whole and one
	/// sold taken out, a track ride with its record in the track-rides module and, where it has cars out, each car
	/// with its riders, its model and its wake's (<see cref="ParkCarWriter"/>); and a queue cell laid, cleared or
	/// tiled again goes out with its model made, or let go.
	/// </para>
	/// <para>
	/// <b>Deviations.</b> The original writes the player's <c>gms.dat</c> first and puts the pointer back to its
	/// default mode; neither is done here. A park made fresh, with no file behind it, is counted and not written.
	/// With nobody playing there is no folder, and nothing is written; the original has a player on every entry.
	/// </para>
	/// </summary>
	internal string? WritePark( string name )
	{
		if ( Kind == Scene.Park && ParkState is { } state )
			return WritePark( Park, state, ThemeName, name, ParkPeople.Current, ParkStaffPool.Current, ParkRides.Current, Catalogue );

		Log.Warning( $"Save: '{name}' is not written - no park is running" );
		return null;
	}

	/// <summary><see cref="WritePark(string)"/> for a park's own parts, the camera's being the orbit camera's.</summary>
	internal static string? WritePark( IParkInitialState? park, ParkState state, string theme, string name,
		ParkPeople? people = null, ParkStaffPool? pool = null, ParkRides? rides = null, ParkItemCatalogue? catalogue = null )
	{
		if ( Players.Roster.Current is not { } player )
		{
			Log.Warning( $"Save: '{name}' is not written - nobody is playing, so there is no folder to write it in" );
			return null;
		}

		if ( park?.Save is not { } loaded )
		{
			Unimplemented.Report( "SAVE_PARK_WITH_NO_FILE" );
			Log.Warning( $"Save: '{name}' is not written - this park was made fresh, and only a loaded park's file is written back" );
			return null;
		}

		var point = ParkOrbitCameraMode.PointOfInterest;

		// The file's rotation turns the other way from the orbit camera's yaw: nought is the same view in both, and a
		// quarter turn written as it stands puts the original's camera on the far side of the point.
		// The things: each of the file's objects, its script and its model as it runs, each object bought since the
		// load written whole and each sold taken out. Without the park's scripts, or where the file's clock, scripts
		// or models did not read, they go out as the file's, and are counted.
		var names = ObjectNames();

		// A thing bought or sold is written with the people, the scripts and the catalogue to hand. A thing whose
		// folder holds emitter files is written as any other: those are the particle library's templates, which the
		// file holds whatever stands in the park (docs/exe/saves.md, "OpenTPW's writer, a thing with emitter files").
		// The particles module's live emitters follow the scripts' records (docs/exe/saves.md, "OpenTPW's writer,
		// an emitter started"). A deviation: a puff a script's EVENT had alive at the save is not in it, where the
		// original's file holds each one, and the file's own puffs stay as the file has them.
		//
		// A track ride is written where its record in the track-rides module can be: a ride of the bumper family
		// with a record here, made, with its cars; and one of the file's, taken out, with no car under its handle
		// or of that family (docs/exe/saves.md, "OpenTPW's writer, a track ride's record" and "a track ride's
		// cars"). A ride with no record here (the karts, the water ride) with a car, and a tracked ride, stay as
		// the file has them, counted.
		bool FileRideWritable( int handle )
			=> loaded.TrackRides.ClosedOnTag && (loaded.TrackRides.CarsOf( handle ) == 0 || ParkBumperCars.HasRecord( handle >> 8 ));

		bool TrackWritable( ParkWorld.CatalogueObject thing )
		{
			if ( thing.TrackRide == 0 )
				return true;

			if ( loaded.Objects.Any( file => file.ThingId == thing.ThingId ) )
				return FileRideWritable( thing.TrackRide );

			// A ride of the file's sold and left in the file keeps its slot there, and no ride made takes it.
			return state.TrackRides.Cars.RideOf( thing.TrackRide ) != null
				&& loaded.TrackRides.Rides.All( ride => (ride.Handle & 0xff) != (thing.TrackRide & 0xff) || FileRideWritable( ride.Handle ) );
		}

		Func<ParkWorld.CatalogueObject, bool>? writable = rides != null && people != null && catalogue != null
			? thing => catalogue.TryGet( thing.CatalogueId, out var item ) && item.TrackType == 0
				&& (item.BumperType != 0) == (thing.TrackRide != 0) && TrackWritable( thing )
				&& ParkObjectNames.Lines( thing.CatalogueId, names ) != null
			: null;

		var objects = state.WrittenObjects( loaded, writable, out var made, out var gone );

		var bought = made.Select( thing =>
		{
			catalogue!.TryGet( thing.CatalogueId, out var item );

			var (lineA, lineB) = ParkObjectNames.Lines( thing.CatalogueId, names )!.Value;

			return new ParkRides.BoughtThing( new ParkWorld.MadeObject( thing, lineA, lineB ), item.Width, item.Depth,
				"data\\" + item.Directory.Replace( '/', '\\' ).TrimEnd( '\\' ) + "\\" );
		} ).ToList();

		var things = rides?.Written( loaded, objects,
			id => catalogue != null && catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1, state.HoardingFor,
			bought, gone, state.BuiltItems );

		// Each track ride made or gone, for the track-rides module, and each of the file's still standing. One with
		// no car in the file and none out here is written over where it lies, or put in; one with a car, here or in
		// the file, is taken out and written again whole with its cars; one with no record here is left the
		// file's, counted.
		if ( things != null )
		{
			var kept = new List<SavedTrackRide>();
			var bare = new List<SavedTrackRide>();
			var whole = new List<ParkFileWriter.WrittenTrack>();
			var bumpers = state.TrackRides.Cars;

			void Take( ParkWorld.CatalogueObject thing, List<SavedTrackRide> carless )
			{
				if ( bumpers.Written( thing.TrackRide, thing.Angle, thing.CatalogueId ) is not { } record )
				{
					Unimplemented.Report( "SAVE_PARK_TRACK_RIDE_AS_THE_FILE" );
					return;
				}

				if ( loaded.TrackRides.CarsOf( thing.TrackRide ) == 0
					&& bumpers.RideOf( thing.TrackRide ) is { Cars: 0, Boarding.Count: 0, Leaving.Count: 0 } )
				{
					carless.Add( record );
				}
				else if ( catalogue!.TryGet( thing.CatalogueId, out var item )
					&& ParkCarWriter.Track( bumpers, record, item, rides!.Readings, ParkBumperBoats.Current, FileSystem ) is { } track )
				{
					whole.Add( track );
				}
				else
				{
					Unimplemented.Report( "SAVE_PARK_TRACK_RIDE_AS_THE_FILE" );
				}
			}

			foreach ( var thing in objects.Where( thing => thing.TrackRide != 0 ) )
				Take( thing, kept );

			foreach ( var thing in made.Where( thing => thing.TrackRide != 0 ) )
				Take( thing, bare );

			things = things with
			{
				KeptTracks = kept,
				MadeTracks = bare,
				Tracks = whole,
				GoneTracks = [.. loaded.Objects.Where( thing => gone.Contains( thing.ThingId ) && thing.TrackRide != 0 )
					.Select( thing => thing.TrackRide )]
			};
		}

		if ( rides != null && things == null )
		{
			Unimplemented.Report( "SAVE_PARK_THINGS_AS_THE_FILE" );
			Log.Warning( $"Save: '{name}' holds its things as the file had them - the file's clock, scripts or models did not read" );
			made.Clear();
			gone.Clear();
		}

		// A queue cell's piece is a model, written with the things; without them it is left the file's, counted.
		var pieces = things != null ? new Dictionary<int, ParkFileWriter.QueuePiece?>() : null;
		var cells = WrittenCells( loaded, state, made, gone, pieces );
		var written = WrittenThings( loaded, made, gone );

		if ( things != null && pieces!.Count > 0 )
			things = things with { QueueCells = pieces };

		var running = new ParkFileWriter.Running( state.GameTick, state.ParkIsClosed, state.VisitorsToDate, state.Balance,
			new ParkCameraModule.View( ParkOrbitCameraMode.Zoom, -ParkOrbitCameraMode.Yaw, point.X, point.Y ), cells,
			people?.Written( written.Contains ), pool?.Written(), people?.WrittenArrival( things != null ? written.Contains : null ), things, people?.WrittenLetGo(),
			WrittenEffects( loaded, state, written.Contains, asTheFile: things == null ) );

		byte[] file;
		ParkWorld.PeopleWritten? peopleWritten;
		ParkFileWriter.ThingsWritten? thingsWritten;

		try
		{
			file = ParkFileWriter.Write( loaded, running, out peopleWritten, out thingsWritten );
		}
		catch ( InvalidOperationException e )
		{
			Log.Warning( $"Save: '{name}' is not written - {e.Message}" );
			return null;
		}

		if ( SaveFolder.WritePark( player.Slot, player.Name, theme, name, file ) is not { } path )
			return null;

		Log.Info( $"Save: wrote {path}, {file.Length} bytes: mGameTick {running.GameTick}, " +
			$"{(running.ParkClosed ? "closed" : "open")}, {running.VisitorsToDate} visitors to date, balance {running.Balance}, " +
			$"camera {ParkOrbitCameraMode.State()}, {cells.Count} cells of ground" );

		if ( things != null && thingsWritten is { } wrote )
		{
			if ( wrote.Made > 0 || wrote.Gone > 0 )
			{
				Log.Info( $"Save: {wrote.Made} things bought written whole and {wrote.Gone} sold taken out" );

				foreach ( var thing in things.Made ?? [] )
				{
					var placed = thing.Object.Object;

					Log.Info( $"Save: thing {placed.ThingId} made, item {placed.CatalogueId} '{thing.Object.NameA}' '{thing.Object.NameB}' "
						+ $"at ({placed.RawX >> 8},{placed.RawY >> 8}) turned {placed.Angle}, door {placed.CanLoad}, "
						+ (thing.Script is { } script
							? $"script {script.Script.Handle} from {script.Directory} at word {script.Script.Position}, wait "
								+ (script.Script.WaitDeadline != 0 ? unchecked((int)(script.Script.WaitDeadline - things.Clock)).ToString() : "none")
							: "no script")
						+ $", channels {string.Join( ",", thing.Channels.Select( channel => $"{channel.Role}/{channel.Entry}" ) )}, "
						+ $"hoarding 0x{thing.HoardingFlags:x} at {thing.HoardingProgress}" );
				}
			}

			foreach ( var (cell, handle, freed) in wrote.QueueCells ?? [] )
			{
				var piece = things.QueueCells![cell];

				Log.Info( $"Save: queue cell ({cell % ParkWorld.MapSize},{cell / ParkWorld.MapSize}) "
					+ (freed != 0 ? $"gives up model {freed} and " : "")
					+ (piece is { } laid
						? $"names model {handle}, item {ParkThingStates.FirstQueuePieceItem + laid.TileIndex} turned {(360 - laid.TileAngle) % 360}"
						: "names none") );
			}

			if ( wrote.Emitters is { } emitters )
			{
				// An effect that links an effector is not started, and neither is any where the module did not read.
				for ( var i = 0; i < emitters.NotStarted; ++i )
					Unimplemented.Report( "SAVE_PARK_EMITTER_NOT_STARTED" );

				foreach ( var (script, emitter) in emitters.Started )
				{
					Log.Info( $"Save: emitter started for script {script}: slot {emitter.Slot} count {emitter.Count} handle 0x{emitter.Handle:x} "
						+ $"effect {emitter.Template} at ({emitter.X / 64},{emitter.Height / 64},{emitter.Z / 64}) life {emitter.Life}" );
				}

				foreach ( var emitter in emitters.Killed )
					Log.Info( $"Save: emitter killed: slot {emitter.Slot} count {emitter.Count} effect {emitter.Template}" );

				foreach ( var (ride, emitter, kept) in emitters.Smoke ?? [] )
				{
					Log.Info( $"Save: smoke {(kept ? "kept" : "started")} for a car of ride 0x{ride:x}: slot {emitter.Slot} count {emitter.Count} handle 0x{emitter.Handle:x} "
						+ $"effect {emitter.Template} at ({emitter.X / 64f:0.00},{emitter.Height / 64f:0.00},{emitter.Z / 64f:0.00}) life {emitter.Life}" );
				}

				Log.Info( $"Save: {emitters.Started.Count} emitters started, {emitters.Killed.Count} killed, {emitters.NotStarted} not started, "
					+ $"{emitters.Smoke?.Count ?? 0} cars' smoke" );
			}

			Log.Info( $"Save: {wrote.Objects} objects, {wrote.Scripts} scripts and {wrote.Models} models written as they run, "
				+ $"the script scheduler on tick {things.SchedulerTick} with handle {things.NextHandle} next, the clock reading {things.Clock}"
				+ (wrote.ScriptTablesLeft > 0 ? $"; {wrote.ScriptTablesLeft} script tables of another length left the file's" : "") );

			foreach ( var script in things.Scripts )
			{
				var riders = (script.Limbo?.Count( slot => slot.Handle != 0 ) ?? 0)
					+ (script.Bounce?.Count( slot => slot.Handle != 0 ) ?? 0)
					+ (script.Walk?.Count( slot => slot.State != 0 ) ?? 0);

				Log.Info( $"Save: script {script.Handle} at word {script.Position}, result {script.Result}, "
					+ $"wait {(script.WaitDeadline != 0 ? unchecked((int)(script.WaitDeadline - things.Clock)).ToString() : "none")}, "
					+ $"{riders} in its slots (bouncing {script.Bouncing}: "
					+ string.Join( ",", (script.Bounce ?? []).Where( slot => slot.Handle != 0 ).Select( slot => $"{slot.Handle}@{slot.Node}" ) ) + ")" );
			}
		}

		if ( running.StaffPool is { } candidates )
		{
			Log.Info( $"Save: {candidates.Slots.Count( slot => slot != null )} candidates in the pool, topped up on mGameTick "
				+ $"{candidates.TimeSig}" );
		}

		if ( running.Arrival is { } arrival )
		{
			Log.Info( $"Save: the arrival timer's mark {arrival.TimeSig}, "
				+ (arrival.Offloading ? $"a load held with {arrival.PeopleOnBus} still to drop" : "no load held")
				+ (arrival.CurrentVehicle is { } vehicle ? $", mCurrentArrivalVehicle {vehicle}" : ", the vehicle left the file's") );
		}

		if ( peopleWritten is { } report )
		{
			// A made sprite's +0xbc is a byte of its bank; one whose bank was not to hand, written without a like
			// sprite in the file to copy it from, is counted.
			for ( var i = 0; i < report.UnmatchedSpriteSets; ++i )
				Unimplemented.Report( "SAVE_PARK_SPRITE_SET_BYTE" );

			Log.Info( $"Save: {report.Kept} people kept, {report.Made} made, {report.Gone} gone; " +
				$"{report.LiveSprites} sprites in {report.SpriteSlots} slots, {report.Balloons} of them balloons and " +
				$"{report.Bubbles} bubbles, {report.LetGo} balloons let go, {report.Heads} riders' heads; {report.CellsHeaded} cells headed anew" );
		}

		return path;
	}

	/// <summary>
	/// The things a park file written from <paramref name="loaded"/> holds that are no person: the file's own, since
	/// nothing bought or sold is written yet. A person's handle to any other thing is written as nought.
	/// </summary>
	internal static HashSet<int> WrittenThings( ParkWorld loaded ) =>
		[.. loaded.Things.Where( thing => thing.Model is not (ParkWorld.GuestModel or (>= 4 and <= 8)) ).Select( thing => thing.ThingId )];

	/// <summary><see cref="WrittenThings(ParkWorld)"/> with the objects bought written whole and without the ones sold taken out.</summary>
	internal static HashSet<int> WrittenThings( ParkWorld loaded, IEnumerable<ParkWorld.CatalogueObject> made, IReadOnlySet<int> gone )
	{
		var things = WrittenThings( loaded );

		things.ExceptWith( gone );
		things.UnionWith( made.Select( thing => thing.ThingId ) );

		return things;
	}

	/// <summary>The running language's table of object names, or null where it will not read.</summary>
	private static StringFile? ObjectNames()
	{
		try
		{
			return new StringFile( ParkObjectNames.TablePath );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or ArgumentException or NullReferenceException )
		{
			Log.Warning( $"Save: {ParkObjectNames.TablePath} did not read - {e.Message}" );
			return null;
		}
	}

	/// <summary>The ground tile the original gives a cell under a footprint (set 0, turned 0): every such cell of seven park files.</summary>
	internal const int FootprintTile = 8;

	/// <summary>
	/// The cells a park file written from <paramref name="state"/> holds in place of the file's own: every cell the
	/// running park has changed (<see cref="ParkState.ChangedRecords"/>) that differs from the file's in a field the
	/// writer writes, but a footprint's (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the cells").
	///
	/// <para>
	/// <b>A cell that has joined or left a footprint, or changed its footprint's owner, is left as the file's and
	/// counted</b>: the thing that stands there is not written yet, and neither is the cell's <c>mWho</c>, so the file
	/// would hold a footprint with nothing on it, or a thing on bare ground. An entrance that has only gained or lost
	/// a link is written.
	/// </para>
	/// <para>
	/// <b>A queue cell laid, cleared or tiled again is written and counted</b> here: its <c>mMeshInstance</c> is the
	/// handle of the model the original's retile makes for it (<c>FUN_005365d0</c>), which no load makes again, and
	/// the file's is left there. The overload that is handed somewhere to put the pieces writes them instead.
	/// </para>
	/// </summary>
	internal static Dictionary<int, ParkWorld.MapCell> WrittenCells( ParkWorld loaded, ParkState state )
		=> WrittenCells( loaded, state, [], new HashSet<int>() );

	/// <summary>
	/// Every cell's region effects as the file holds its things (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the
	/// region effects"): the running park's, with each object's effect as its written record has it. A thing
	/// bought and not written holds none; a thing sold and still written holds the file's; and where the objects
	/// go out as the file's, a toilet dirtied or cleaned since holds the file's too.
	/// </summary>
	/// <param name="written">Whether an object goes into the file: a kept one, or one bought and written whole.</param>
	/// <param name="asTheFile">Whether the kept objects' records are left as the file has them.</param>
	internal static short[] WrittenEffects( ParkWorld loaded, ParkState state, Func<int, bool> written, bool asTheFile = false )
	{
		var effects = state.EffectsCopy();

		void Move( ParkWorld.CatalogueObject thing, bool off )
		{
			foreach ( var effect in ParkRegionEffects.Of( thing ) )
				state.RegionEffects.Stamp( effects, effect, thing.CellX, thing.CellY, off );
		}

		foreach ( var thing in state.Objects.Where( thing => !written( thing.ThingId ) ) )
			Move( thing, off: true );

		foreach ( var thing in loaded.Objects.Where( thing => written( thing.ThingId ) ) )
		{
			var runs = state.TryObject( thing.ThingId, out var running );

			if ( runs && !asTheFile )
				continue;

			if ( runs )
				Move( running, off: true );

			Move( thing, off: false );
		}

		return effects;
	}

	/// <summary>
	/// <see cref="WrittenCells(ParkWorld, ParkState)"/> where objects bought are written whole and objects sold are
	/// taken out: a cell under the footprint of one of <paramref name="made"/>, and a cell one of
	/// <paramref name="gone"/> has left, is written too. A cell that has joined a footprint goes out on the tile the
	/// original stamps one with (<see cref="FootprintTile"/>), which the park here does not keep; one left bare
	/// is on bare ground's already (<see cref="ParkPathBuilding"/>).
	///
	/// <para>
	/// <b>A queue cell laid, cleared or tiled again goes into <paramref name="pieces"/></b> with the piece it holds
	/// now, or null where it has left the queue (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a queue cell's model"),
	/// for the writer to make its model and free the one the file's cell names. A tile index outside the eight
	/// pieces draws nothing here and has no model to make: that cell is counted and left naming the file's. With
	/// nowhere to put them every such cell is counted and left.
	/// </para>
	/// </summary>
	internal static Dictionary<int, ParkWorld.MapCell> WrittenCells( ParkWorld loaded, ParkState state,
		IReadOnlyList<ParkWorld.CatalogueObject> made, IReadOnlySet<int> gone,
		Dictionary<int, ParkFileWriter.QueuePiece?>? pieces = null )
	{
		var written = new Dictionary<int, ParkWorld.MapCell>();
		var madeAnchors = new HashSet<int>( made.Select( thing => MapStep.CellId( thing.RawX >> 8, thing.RawY >> 8 ) ) );

		var goneAnchors = new HashSet<int>( loaded.Objects.Where( thing => gone.Contains( thing.ThingId ) )
			.Select( thing => MapStep.CellId( thing.RawX >> 8, thing.RawY >> 8 ) ) );

		// A file whose map was never reached is refused by the writer, which says why.
		if ( loaded.Cells.Count != ParkWorld.MapSize * ParkWorld.MapSize )
			return written;

		foreach ( var (index, now) in state.ChangedRecords )
		{
			var was = loaded.Cells[index];

			if ( SameGround( was, now ) )
				continue;

			var footprintWas = IsFootprint( was.Type );
			var footprintNow = IsFootprint( now.Type );

			if ( footprintWas != footprintNow || (footprintNow && (was.Type != now.Type || was.ParentId != now.ParentId)) )
			{
				// Written where the thing that stands there now is, and the thing that stood there is not.
				if ( (footprintNow && !madeAnchors.Contains( now.ParentId )) || (footprintWas && !goneAnchors.Contains( was.ParentId )) )
				{
					Unimplemented.Report( "SAVE_PARK_FOOTPRINT_CELL" );
					continue;
				}

				NotePiece( index, was, now );
				written[index] = footprintNow ? now with { TileSet = 0, TileIndex = FootprintTile, TileAngle = 0 } : now;

				continue;
			}

			NotePiece( index, was, now );
			written[index] = now;
		}

		return written;

		// A cell whose queue piece is not the file's: handed on, or counted where it cannot be.
		void NotePiece( int index, ParkWorld.MapCell was, ParkWorld.MapCell now )
		{
			var queueWas = was.Type == ParkRideChoice.QueueCellType;
			var queueNow = now.Type == ParkRideChoice.QueueCellType;

			if ( queueWas == queueNow && (!queueNow || (was.TileIndex == now.TileIndex && was.TileAngle == now.TileAngle)) )
				return;

			if ( pieces == null || (queueNow && (now.TileIndex < 0 || now.TileIndex >= ParkThingStates.QueuePieces)) )
				Unimplemented.Report( "SAVE_PARK_QUEUE_CELL_MODEL" );
			else
				pieces[index] = queueNow ? new ParkFileWriter.QueuePiece( now.TileIndex, now.TileAngle ) : null;
		}
	}

	private static bool IsFootprint( int type ) => type is CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd;

	/// <summary>Whether two records agree in every field <see cref="ParkWorld.PutCells"/> writes.</summary>
	private static bool SameGround( ParkWorld.MapCell a, ParkWorld.MapCell b ) =>
		(a.Type, a.Flags, a.Neighbours, a.Direction, a.TileSet, a.TileIndex, a.TileAngle, a.OverlapCounter, a.ParentId)
			== (b.Type, b.Flags, b.Neighbours, b.Direction, b.TileSet, b.TileIndex, b.TileAngle, b.OverlapCounter, b.ParentId)
		&& (a.TrackType, a.TrackFlags, a.TrackParentId, a.TrackNeighbours)
			== (b.TrackType, b.TrackFlags, b.TrackParentId, b.TrackNeighbours);

	/// <summary>
	/// A park's interface: the windows its menu opens in, the park's own build of that menu, and the
	/// pointer. The lobby's front end belongs to the lobby, and this is the separate build of the same
	/// widgets the original makes - which is why <see cref="ParkFrontEnd"/> stands beside
	/// <see cref="FrontEnd"/> rather than a flag being added to it.
	/// </summary>
	private void SetupParkHud()
	{
		Hud = new();

		// The same window engine the lobby uses, built again for this scene: it deals out the pointer and
		// the keys and holds the modal stop for whatever opens in it.
		var windows = _windows = Hud.AddChild( new WindowStack() );

		// What Escape does here, and what the menu's choices do, which is this scene's to say. After the
		// stack, as the lobby's front end is, so it hears about a frame once the stack has dealt it out.
		Hud.AddChild( new ParkFrontEnd( windows, ThemeName ) );

		// The same on-screen effects the lobby has, and for a reason rather than for symmetry: a park
		// makes button glints. SetupParticles runs for parks as well as for the lobby, the windows here
		// start and stop glints exactly as the lobby's do - see OptionsScreen.Closed, which calls
		// StopGlint - and WindowStack draws them through ScreenParticles.Current, which this sets.
		//
		// Over the interface it decorates and under the pointer, which is the order SetupHud uses.
		Hud.AddChild( new ScreenParticles() );

		_cursor = Hud.AddChild( new Cursor() );
	}

	private void SetupHud()
	{
		Hud = new();

		// The interface's windows, and what the pointer and the keys do to them.
		var windows = _windows = Hud.AddChild( new WindowStack() );

		// The lobby's interface - the player slots, its dialogs and the island panel - opened in those windows.
		// Built here, behind the loading screen, along with everything its windows draw. After the stack, so it
		// hears about a frame's clicks and keys once the stack has dealt them out.
		Hud.AddChild( new FrontEnd( windows ) );

		// On-screen particle effects go over the interface they decorate, and under the pointer.
		Hud.AddChild( new ScreenParticles() );

		Hud.AddChild( new Cursor() );
	}

	/// <summary>
	/// The particle system, which the state machine loads before the lobby itself (Data\Particle\Tp2.plb).
	///
	/// Its density comes from the detail file for the options' graphics quality - low.sam, med.sam or
	/// high.sam, going by their names - which starts at medium (0x00423690). The quality is only read as
	/// a level loads, as the original's state machine reads it for the particles (0x0051fd80).
	/// </summary>
	private static void SetupParticles()
	{
		var density = 1024;
		var detail = DetailFile;

		try
		{
			if ( int.TryParse( new SettingsFile( $"/{detail}" )["GameOptions.PARTICLEDENSITY"], out var value ) )
				density = value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Particles: {detail} would not load, so effects run at their own rates - {e.Message}" );
		}

		_ = new ParticleSystem( "Particle/Tp2.plb", density );
	}

	/// <summary>
	/// The detail file for the options' graphics quality - low.sam, med.sam or high.sam, going by their names - which
	/// starts at medium (0x00423690).
	/// </summary>
	private static string DetailFile => GameOptions.Current.GraphicsQuality switch
	{
		0 => "low.sam",
		2 => "high.sam",
		_ => "med.sam"
	};

	/// <summary>
	/// The detail file's <c>GameOptions.NUMKIDS</c>, which caps the kid banks (<see cref="ParkSpriteBanks.KidCap"/>):
	/// 0 in low.sam and 2 in med.sam and high.sam. A file that will not read gives medium's 2, the quality it starts at.
	/// </summary>
	private static int NumKids()
	{
		try
		{
			if ( int.TryParse( new SettingsFile( $"/{DetailFile}" )["GameOptions.NUMKIDS"], out var value ) )
				return value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Guests: {DetailFile} would not load, so the kids take medium's six banks - {e.Message}" );
		}

		return 2;
	}

	public void Update()
	{
		DebugConsole.Poll();

		// The world's clock first, so everything below sees one frame's worth of game time and the
		// same count of ticks. Only a park stops it, and only a park gets the longer catch-up - both
		// are the original's, and GameClock says where each is read from.
		//
		// Whether a window is pausing is read here, at the top of the frame, so it reflects what was
		// open when the frame began. The interface deals out its clicks in Hud.Update below, so a menu
		// opened by this frame's click holds the world from the next one. That one frame of lag is the
		// arrangement the advisor already has, and for the same reason - see FrontEnd.OnUpdate: a
		// choice that closes one screen and opens another never lets the world go in between.
		GameClock.Update( PausedByWindow(), Kind == Scene.Park ? GameClock.ParkCatchUp : GameClock.LobbyCatchUp );

		// Then the date, which only a park keeps: the original's counter is advanced from the park
		// loop's every-eighth-tick gate (0x0054f668) and from nowhere else, so the lobby has no calendar
		// at all. It reads the ticks the line above just counted, so a held park stops the date for
		// free rather than by testing anything.
		if ( Kind == Scene.Park )
			GameCalendar.Update();

		Entity.All.ForEach( entity => entity.Update() );

		// The bumper rides' boats stood where this frame's track ticks left their cars, outside the walk above, which
		// cannot take the entities a new boat adds.
		ParkBumperBoats.Current?.Sync();

		// The day's change after every thing has had its turn, as the original's calendar sends it at the end of the
		// world tick (0x00516695), so a settle-up in the same frame counts into the day that is closing. The edge is
		// GameCalendar's, which counts from nought rather than from the save's clock, so after a load the days turn
		// at other moments than the original's (docs/QUEUE.md Q149).
		if ( Kind == Scene.Park && GameCalendar.DayRolled )
			ParkState?.RollTheDay();

		// Then the month's change and the year's, which the calendar sends after the day's in the same tick. The
		// original's month goes to thing 1's training, the analyser, the bank and then each wage, in ascending thing id
		// (docs/exe/ride-operation.md, "The month's change").
		if ( Kind == Scene.Park && GameCalendar.MonthRolled )
		{
			ParkPeople.Current?.TrainTheStaff();
			ParkState?.TurnTheMonth();
			ParkPeople.Current?.PayTheWages();
		}

		if ( Kind == Scene.Park && GameCalendar.YearRolled )
			ParkState?.TurnTheYear();

		// Whatever was deleted during that walk leaves the list now the walk is over - see Entity.Delete.
		Entity.ApplyDeletions();

		ParticleSystem.Current?.Update();

		// What the pointer is over, before the interface deals out this frame's clicks - so a click
		// and the cell it landed on are the same frame's answer. Only a park has ground to point at.
		if ( Kind == Scene.Park )
			ParkPicking.Update();

		// The help row the world shows for the cell under the pointer - see WorldHelpRow.
		UI.WindowStack.WorldHelpText = Kind == Scene.Park ? WorldHelpRow() : -1;

		// The HUD is not an entity - see RootPanel - so it is driven from here, after the world.
		Hud.Update();

		// And a click on the WORLD, after the interface has had this frame's - so a press that landed
		// on a button never also opens whatever is drawn behind it. The original reaches the same place
		// from the park's own window proc (FUN_004879d0), which acts only while the current interaction
		// mode is idle and hands the click to the mode otherwise.
		if ( Kind == Scene.Park )
		{
			WorldClick();
			BuildKeys();
			ShowToolCursor();
		}
	}

	private bool _worldMouseWasDown;
	private bool _worldRightWasDown;

	/// <summary>The park's own pointer, so an armed tool can show its cursor.</summary>
	private Cursor? _cursor;

	/// <summary>The cursor the park's pointer wears, for the console's <c>pointer</c>.</summary>
	internal Input.CursorTypes? ParkCursor => _cursor?.CursorType;

	/// <summary>
	/// The cursor an armed build tool shows - <c>c_path</c> for path and <c>c_queue</c> for queue, the ids
	/// <c>FUN_00489720</c> registers as 3 and 4 - and the ordinary pointer otherwise. Set only when it
	/// changes, because setting it reloads the picture.
	/// </summary>
	private void ShowToolCursor()
	{
		if ( _cursor is not { } cursor )
			return;

		// Idle, the pointer already says what a click would do over ground a path can go on
		// (FUN_0052f950 with the hover category, cursor 3). Under the full-screen view the park's mouse proc hears
		// nothing and the pointer is the plain arrow, as the original's frame shows it over grass; with a tool armed
		// there it is not measured.
		var wanted = FullScreenView ? Input.CursorTypes.Normal : ParkBuildMode.Current switch
		{
			ParkBuildMode.Queue => QueueCursor(),
			ParkBuildMode.Path => PathCursor(),
			_ => IdleOverPath() ? Input.CursorTypes.Path : Input.CursorTypes.Normal
		};

		if ( cursor.CursorType != wanted )
			cursor.CursorType = wanted;
	}

	/// <summary>
	/// The queue tool's cursor follows its preview (<c>FUN_0052f950</c>): <c>c_link</c> when the run
	/// would join a path, <c>c_end</c> when it would close on the ride's own queue, <c>c_noplace</c> when
	/// any square is red, <c>c_cash</c> when that is because the run cannot be paid for, and
	/// <c>c_queue</c> otherwise. Of the loose cursor pictures <c>Clin.tga</c> is
	/// the link cursor, which <see cref="Input.CursorTypes"/> names <c>Line</c>, and <c>Cnog.tga</c> the
	/// refusal.
	/// </summary>
	private static Input.CursorTypes QueueCursor()
	{
		if ( !ParkPicking.TryCell( out var x, out var y ) )
			return Input.CursorTypes.Queue;

		var strip = ParkPathBuilding.QueueStrip( x, y );

		if ( strip.Any( square => square.Unaffordable ) )
			return Input.CursorTypes.Cash;

		if ( strip.Any( square => square.Marker == ParkPathBuilding.MarkerRed ) )
			return Input.CursorTypes.NoGo;

		return strip.Count > 0 ? strip[^1].Marker switch
		{
			ParkPathBuilding.MarkerLink => Input.CursorTypes.Line,
			ParkPathBuilding.MarkerEnd => Input.CursorTypes.End,
			_ => Input.CursorTypes.Queue
		} : Input.CursorTypes.Queue;
	}

	/// <summary>
	/// What a quick right click does: with the Options switch "RMB cancel" on it installs the idle mode over
	/// whatever is current (<c>0x0048842b</c>), letting go of the hand and putting any build tool away - see
	/// <see cref="ParkHand.LetGo"/>. With the option off it does nothing. Null when there was nothing to do.
	/// </summary>
	/// <remarks>
	/// <b>One body, shared with the console's <c>rightclick</c></b>, so the console reaches this rather than a
	/// copy of it.
	/// </remarks>
	internal static string? QuickRightClick()
	{
		if ( !GameOptions.Current.RmbCancel || ParkHand.LetGo() is not { } letGo )
			return null;

		return $"world click: right click - {letGo}";
	}

	/// <summary>
	/// The path tool's cursor follows its preview as the queue tool's does (<c>FUN_0052f950</c>):
	/// <c>c_cash</c>, then <c>c_noplace</c> for any red square, then <c>c_link</c> when the run would pave
	/// a loose queue end, <c>c_end</c> when it would end on a path, and <c>c_path</c> otherwise.
	/// </summary>
	private static Input.CursorTypes PathCursor()
	{
		if ( !ParkPicking.TryCell( out var x, out var y ) )
			return Input.CursorTypes.Path;

		var strip = ParkPathBuilding.PathStrip( x, y );

		if ( strip.Any( square => square.Unaffordable ) )
			return Input.CursorTypes.Cash;

		if ( strip.Any( square => square.Marker == ParkPathBuilding.MarkerRed ) )
			return Input.CursorTypes.NoGo;

		if ( strip.Any( square => square.Marker == ParkPathBuilding.MarkerLink ) )
			return Input.CursorTypes.Line;

		return strip.Any( square => square.Marker == ParkPathBuilding.MarkerEnd )
			? Input.CursorTypes.End
			: Input.CursorTypes.Path;
	}

	/// <summary>
	/// Whether a click here with nothing armed and an empty hand would pick up the path tool - the
	/// original's hover categories 2 (bare ground) and 1 (path), which <c>FUN_004879d0</c> answers by
	/// installing the path tool. See <see cref="ArmsThePathTool"/>. Never while a park screen is open: the hover is
	/// not taken then (<c>0x00488569</c>, <c>0x0048884e</c>), and opening one puts the plain arrow on the layer
	/// (<c>FUN_004a2aa0( 0 )</c>, <c>0x00485cc8</c>).
	/// </summary>
	private bool IdleOverPath()
		=> ParkBuildMode.Current == ParkBuildMode.None && ParkHand.Empty && !UI.WindowStack.ParkScreenOpen
			&& ParkPicking.TryCell( out var x, out var y ) && ArmsThePathTool( x, y, ParkPicking.ThingUnderCursor );

	/// <summary>
	/// The help row for the cell under the pointer, idle: UIHELPTEXT 441 "Left-click to build path" over
	/// bare ground and 442 "Left-click to extend this path" over path. Nothing is found in the original to
	/// show 443, "BACKSPACE to undo", so nothing here does.
	/// </summary>
	private int WorldHelpRow()
	{
		if ( !IdleOverPath() || Park is not { } park || !ParkPicking.TryCell( out var x, out var y ) )
			return -1;

		return ParkState.CellFor( park, x, y ).Type == CellEdge.Path ? 442 : 441;
	}

	/// <summary>
	/// Whether a click on this cell, idle and empty-handed, picks up the path tool: bare ground or path
	/// (<c>FUN_00486d90</c>'s categories 2 and 1), with no Shift held - Shift is the guest pick - and no
	/// member of staff under the pointer, whose window comes first. A guest on the cell, or the ride a
	/// path serves, does not stop it. Track cells of type 11 and 16 have a category of their own, and a
	/// type-12 track cell whose parent is type 25 has none at all (<c>0x004873b3</c>).
	/// </summary>
	private bool ArmsThePathTool( int x, int y, int thingUnderCursor )
	{
		if ( Park is not { } park || !ParkState.OnMap( x, y ) )
			return false;

		var cell = ParkState.CellFor( park, x, y );

		if ( cell.Type is not (CellEdge.Nothing or CellEdge.Path) )
			return false;

		if ( cell.TrackType is 11 or 16 )
		{
			Unimplemented.Report( "HOVER_TRACK_CELL_CATEGORY" );
			return false;
		}

		if ( cell.TrackType == 12 && cell.TrackParentId != 0 )
		{
			var (parentX, parentY) = MapStep.CellAt( cell.TrackParentId );

			if ( ParkState.OnMap( parentX, parentY ) && ParkState.CellFor( park, parentX, parentY ).TrackType == 25 )
				return false;
		}

		if ( Input.ShiftHeld )
			return false;

		return thingUnderCursor == 0 || ParkPeople.Current?.IsStaff( thingUnderCursor ) != true;
	}

	/// <summary>
	/// The build keys, on release as the original's game table fires them: Backspace and Delete. Not
	/// while a box has the keyboard, and not while a window holds the park - which is inferred for the pausing
	/// windows. Not while a park screen is open: opening one switches the game table off (<c>FUN_00485b70</c>,
	/// <c>0x00485ce6</c>) and its handler runs the shortcuts' alone (<c>FUN_00488ba0</c>). And not under the
	/// full-screen view, whose control runs the camera's table alone (<c>0x004a2918</c>).
	/// </summary>
	/// <remarks>
	/// The release edge here also fires when a modifier changes while the key is held, which the
	/// original's key-up does not.
	/// </remarks>
	private void BuildKeys()
	{
		if ( Input.TextCaptured || PausedByWindow() || FullScreenView || UI.WindowStack.ParkScreenOpen )
			return;

		if ( Input.Released( InputButton.Delete ) )
			Log.Info( BackspaceKey() );

		if ( Input.Released( InputButton.Clear ) )
			Log.Info( ClearKey() );
	}

	/// <summary>
	/// Backspace - the game table's row 5, handler <c>0x0040bda0</c>. With the path tool armed it takes up
	/// the last run laid; with nothing armed and an empty hand it takes one press of the clear off the path
	/// under the pointer. One body, shared with the console's <c>backspace</c>.
	/// </summary>
	internal string BackspaceKey()
	{
		switch ( ParkBuildMode.Current )
		{
			case ParkBuildMode.Path:
				return ParkPathBuilding.UndoPathRun();

			case ParkBuildMode.Queue:
				Unimplemented.Report( "BACKSPACE_UNDO_QUEUE_RUN" );
				return "backspace: undoing a queue run is not built";

			case ParkBuildMode.None when ParkHand.Empty:
				if ( !ParkPicking.TryCell( out var x, out var y ) )
					return "backspace: the pointer is not on the park";

				return ParkPathBuilding.DeletePathUnderPointer( x, y );

			default:
				return "backspace: nothing to do";
		}
	}

	/// <summary>
	/// Delete - the game table's row 3, <c>FUN_0040c5e0</c>, which installs Clear Land (mode <c>0x3a</c>) through
	/// the setter over whatever was current, so the hand is let go of and any tool put away. Clear Land is not
	/// built, so the letting go is all this does.
	/// </summary>
	internal static string ClearKey()
	{
		var letGo = ParkHand.LetGo();
		Unimplemented.Report( "CLEAR_LAND_TOOL" );

		return $"delete: the clear-land tool is not built - {letGo ?? "nothing was held"}";
	}

	/// <summary>
	/// Whether the right button's press is still a quick click (the original's <c>DAT_007c2500</c>), and when, in
	/// milliseconds of real time (<c>DAT_007c2504</c>, <see cref="Time.WallMilliseconds"/>), and where it went down.
	/// </summary>
	private (bool Armed, long At, Vector2 Where) _rightClick;

	/// <summary>
	/// How long a right press stays a quick click: a frame's tick more than 200 ms after it disarms it
	/// (<c>0x0048823f</c>). The original's release reads no clock, only the flag (<c>0x0048836f</c>); after a stopped
	/// frame its tick came before the release, three times of three, which is the order here.
	/// </summary>
	internal const long QuickClickLimit = 200;

	/// <summary>
	/// A press on the park itself. Clicking a placed thing opens its management window -
	/// <c>FUN_004879d0</c> reads the hover category and sends category 4, a placed object, to
	/// <c>FUN_00486920</c>.
	/// </summary>
	private void WorldClick()
	{
		var down = Input.Mouse.Left;
		var pressed = down && !_worldMouseWasDown;

		_worldMouseWasDown = down;

		ParkCamcorderCameraMode.ReadRightButton();

		if ( RightButton( Input.Mouse.Right, Input.Mouse.Position / UI.VirtualScreen.Scale, RightPressTaken( UI.WindowStack.RightPointerTaken ) ) is { } putAway )
		{
			Log.Info( putAway );
			return;
		}

		if ( !pressed || LeftPressTaken( UI.WindowStack.PointerTaken ) || KeptFromThePark( UI.WindowStack.ParkScreenOpen ) )
			return;

		if ( ParkPicking.TryCell( out var cellX, out var cellY ) )
			Log.Info( ClickWorldAt( cellX, cellY, ParkPicking.ThingUnderCursor ) );
	}

	/// <summary>
	/// Whether a left press is the interface's rather than the park's, given whether a window took it
	/// (<see cref="UI.WindowStack.PointerTaken"/>): in first person always, since entering it hides the park's own layer
	/// (<c>0x004a2ac0</c>) and the viewfinder's layer hands a press to the camera table alone (<c>FUN_00488a00</c>;
	/// <c>docs/exe/park-engine.md</c>, "Whose a right press is").
	/// </summary>
	internal static bool LeftPressTaken( bool byAWindow )
		=> ParkCamcorderCameraMode.Active || byAWindow;

	/// <summary>
	/// Whether a left press that landed on the park is kept from it because one of the park's screens is open
	/// (<see cref="UI.WindowStack.ParkScreenOpen"/>): with nothing held and no tool armed, always. The park's mouse proc
	/// skips the press while a screen is open (<c>CMP [0x007c24c8]</c>, <c>0x00488741</c>): no hover is taken, the idle
	/// click <c>FUN_004879d0</c> is not run, so no window opens and no path tool is picked up, and the mode's
	/// button-down slot is not called. The release has no such test (<c>0x004885a5</c>) and reaches the mode's
	/// button-up slot, which is where a carry shell commits (<c>FUN_00524960</c>; its one read of the down's flag,
	/// <c>0x00524a77</c>, gates a sound), so a click beside a screen still puts down what the hand holds and still
	/// lays an armed tool's run. Here every click acts on its press, so that is this press.
	/// </summary>
	internal static bool KeptFromThePark( bool parkScreenOpen )
		=> parkScreenOpen && ParkHand.Empty && ParkBuildMode.Current == ParkBuildMode.None;

	/// <summary>
	/// Whether a right press is the interface's rather than the park's, given whether a window took it
	/// (<see cref="UI.WindowStack.TakesRightPress"/>): in first person always, since entering it hides the park's own
	/// layer (<c>0x004a2ac0</c>), and with the HUD hidden by F2 never, since the stack is not updated then.
	/// </summary>
	internal static bool RightPressTaken( bool byAWindow )
		=> ParkCamcorderCameraMode.Active || (!UI.RootPanel.Hidden && byAWindow);

	/// <summary>
	/// The right button over the park, this frame: down or up, where the pointer is in the interface's 2048x1536
	/// units (<see cref="UI.VirtualScreen"/>), and whether the interface took the press (<see cref="RightPressTaken"/>).
	/// Answers what a quick click let go of, or null.
	/// </summary>
	/// <remarks>
	/// <b>Only a quick click does anything</b>, and only with the Options switch "RMB cancel" on - see
	/// <see cref="QuickRightClick"/>. With it on the park's mouse proc arms a click on the press, and lets it
	/// go once the button has been held more than 200 ms or the pointer has moved more than 8 units across or
	/// down from where it went down; a release while it is still armed is the click (<c>0x0048842b</c>;
	/// <c>docs/exe/park-engine.md</c>, "The hand's ways out"). A press, a held or dragged click, or any click
	/// with the option off leaves the hand as it is, because the right-button slots of every mode are bare
	/// <c>RET 8</c>.
	/// <para>
	/// <b>Only a press the interface did not take arms it</b> - see <see cref="RightPressTaken"/>. The release is the
	/// park's wherever the pointer has gone.
	/// </para>
	/// </remarks>
	internal string? RightButton( bool down, Vector2 at, bool taken = false )
	{
		var pressed = down && !_worldRightWasDown;
		var released = !down && _worldRightWasDown;

		_worldRightWasDown = down;

		// With the option off none of this runs, and the press is the camera's alone.
		if ( !GameOptions.Current.RmbCancel )
			return null;

		if ( pressed )
			_rightClick = (!taken, Time.WallMilliseconds, at);

		if ( _rightClick.Armed && (Time.WallMilliseconds - _rightClick.At > QuickClickLimit
			|| MathF.Abs( at.X - _rightClick.Where.X ) > 8f || MathF.Abs( at.Y - _rightClick.Where.Y ) > 8f) )
			_rightClick.Armed = false;

		if ( !released || !_rightClick.Armed )
			return null;

		_rightClick.Armed = false;

		return QuickRightClick();
	}

	/// <summary>
	/// What a click on the park does at one cell.
	/// </summary>
	/// <remarks>
	/// <b>Split out from <see cref="WorldClick"/> so the debug console can drive the same path.</b> The
	/// console cannot press the mouse button, so without this the whole placing-by-pointing path would be
	/// unreachable from it, which is the same reason <c>ParkPicking.PickAt</c> takes coordinates.
	/// </remarks>
	internal string ClickWorldAt( int cellX, int cellY, int thingUnderCursor )
	{
		// ANYTHING IN THE HAND GOES DOWN FIRST, and only an empty hand opens a window. That is the
		// original's own order: a place mode consumes the click - FUN_004879d0 acts only while the
		// current interaction mode is idle, type 0 or 1 - and the modes that carry something are types
		// 3, 5 and 6. Nothing is charged until it actually goes up, which is why cancelling needs no
		// refund; see ParkBuilding.Carrying.
		if ( ParkBuilding.Carrying != 0 )
			return $"world click: {ParkBuilding.PlaceCarried( cellX, cellY )}";

		if ( ParkStaffPool.Carrying != 0 )
			return $"world click: {ParkStaffPool.PlaceCarried( cellX, cellY )}";

		// Somebody already employed, picked up off the park rather than taken out of the pool.
		if ( ParkPeople.Current is { CarriedStaff: not 0 } people )
			return $"world click: put staff down at ({cellX},{cellY}) - {people.DropStaff( cellX, cellY )}";

		// A BUILD MODE CONSUMES THE CLICK, and it is tested here for the same reason the hand is: in
		// the original only an IDLE mode opens a window, so a player mid-run cannot accidentally open
		// a ride's panel by clicking past the end of their path.
		//
		// There is no drag - the original's drag slots are bare RET stubs - so the first click anchors
		// and the second lays the run between. The anchor then advances to the SNAPPED TARGET rather
		// than to wherever the run reached, which is what makes an L-shaped run work click by click.
		if ( ParkBuildMode.Current != ParkBuildMode.None )
			return $"world click: {RunBuildMode( cellX, cellY )}";

		// CLICKING A QUEUE CELL HANDS BACK THE QUEUE TOOL for the thing that queue serves, anchored on the
		// queue's far end - the original's mode 0x14, "edit this ride's queue". It has to be tested BEFORE
		// the thing under the cursor, because a queue cell names its ride through the owner a queue
		// carries, so the click would otherwise resolve to that ride and open its window instead.
		if ( ParkState is { } state && Park is { } park
			&& ParkState.CellFor( park, cellX, cellY ).Type == ParkRideChoice.QueueCellType
			&& ParkPathBuilding.OwnerOf( state, ParkState.CellFor( park, cellX, cellY ) ) is var owner
			&& owner != 0 )
			return $"world click: {ParkPathBuilding.EditQueue( owner )}";

		// BARE GROUND OR PATH PICKS UP THE PATH TOOL - there is no button for it - and the same click
		// anchors it, the original's press installing the tool and its release applying it.
		if ( ArmsThePathTool( cellX, cellY, thingUnderCursor ) )
		{
			ParkBuildMode.Arm( ParkBuildMode.Path );

			return $"world click: {ParkPathBuilding.RunPath( cellX, cellY )}";
		}

		if ( thingUnderCursor != 0 && ParkPeople.Current?.IsStaff( thingUnderCursor ) == true )
			Unimplemented.Report( "STAFF_WINDOW" );

		if ( thingUnderCursor != 0 )
		{
			OpenObjectWindow( thingUnderCursor );

			return $"world click: opened the window for thing {thingUnderCursor}";
		}

		return $"world click: nothing to do at ({cellX},{cellY})";
	}

	/// <summary>
	/// One click of an armed build mode - see <see cref="ParkPathBuilding.RunPath"/> and
	/// <see cref="ParkPathBuilding.RunQueue"/>.
	/// </summary>
	/// <remarks>
	/// <b>It acts on the press</b>; the original applies on the release, at the preview's target.
	/// </remarks>
	private static string RunBuildMode( int cellX, int cellY )
	{
		if ( ParkBuildMode.Current == ParkBuildMode.Path )
			return ParkPathBuilding.RunPath( cellX, cellY );

		if ( ParkBuildMode.Anchored )
			return ParkPathBuilding.RunQueue( cellX, cellY );

		ParkBuildMode.AnchorAt( cellX, cellY );

		return $"anchored a queue run at ({cellX},{cellY}) - click again to lay it";
	}

	/// <summary>
	/// Closes the park screen that is open, for what closes one without opening a window: the way into first person
	/// (<see cref="ParkCamcorderCameraMode.Enter"/>). See <see cref="UI.WindowStack.CloseParkScreen"/>.
	/// </summary>
	/// <returns>Whether one was open.</returns>
	internal bool CloseParkScreen() => _windows?.CloseParkScreen() == true;

	/// <summary>The object windows open in this park, oldest first - the debug console's way to the preview.</summary>
	internal IEnumerable<UI.ParkObjectWindow> OpenObjectWindows()
		=> _windows?.Windows.OfType<UI.ParkObjectWindow>() ?? [];

	/// <summary>
	/// Opens the management window for one thing - the original's <c>FUN_00486920</c>, which switches
	/// on the thing's kind byte at <c>+2</c> and then, for a placed object, on the item's
	/// <c>WhichUIType</c>. <b>Nine windows share one base</b>; only the ride's is built.
	/// </summary>
	internal void OpenObjectWindow( int thingId )
	{
		if ( Kind != Scene.Park || _windows is not { } windows )
			return;

		if ( ParkState is not { } state || !state.TryObject( thingId, out var placed ) )
			return;

		if ( Catalogue is not { } catalogue || !catalogue.TryGet( placed.CatalogueId, out var item ) )
			return;

		// 0 rides, 1 shops, 2 sideshows, 3 features - the files' own numbering, and the same field the
		// buy screen's four tabs are sorted by.
		if ( item.UiType != 0 )
		{
			Unimplemented.Report( item.UiType switch
			{
				1 => "SHOP_WINDOW",
				2 => "SIDESHOW_WINDOW",
				_ => "FEATURE_WINDOW"
			} );

			Log.Info( $"Object window: '{item.Name}' is UI type {item.UiType}, whose window is not built" );

			return;
		}

		// One window, retargeted - the original keeps the shown thing in a global rather than opening a
		// second copy, which is what makes the two arrows work the way they do.
		foreach ( var open in windows.Windows )
		{
			if ( open is ParkObjectWindow already )
			{
				already.Show( thingId );
				return;
			}
		}

		windows.Open( new ParkObjectWindow( windows, thingId ) );
	}

	/// <summary>
	/// Whether a window open over this scene holds the world - the game menu, a message box, the park map or the options
	/// screen, each of which says so through <see cref="UiWindow.Pauses"/>.
	///
	/// <para>
	/// <b>Only in a park, which is the original's own rule and not a simplification of it.</b> The helper
	/// those screens call (0x004092a0) does nothing unless 0x00786ba4 is exactly 1 - Game+0x3c, the
	/// pause-permission gate, which the state machine sets to 0 as the lobby comes up (0x0054e682) and to
	/// 1 as a park loads (0x0054ea4c). See <see cref="GameClock"/> for why that field means both "a park
	/// is running" and "nobody already holds the pause".
	/// </para>
	/// <para>
	/// <b>Not all three screens test that global before calling.</b> The
	/// lobby's game menu never reaches the test: GameMenu_Open (0x0048c830) branches on its scene argument
	/// at 0x0048c83a, and non-zero - which is what the lobby passes, at 0x005e4207 - builds the lobby's
	/// menu and returns without ever touching the pause. Only the park path reaches the gate at 0x0048c868.
	/// The other two do test it: the message box at 0x0047f251, also requiring the lobby's front-end object
	/// to be gone (0x00f82884, a pointer rather than a flag), and the options screen at 0x004a3a4f - which
	/// the lobby genuinely does reach, so there the gate really is what prevents the pause, by declining to
	/// pause rather than by refusing to open. So in the lobby the world carries on behind an open menu, and
	/// what the lobby does instead is hold its advisor: see <see cref="FrontEnd.OnUpdate"/> and
	/// <see cref="Advisor.Paused"/>.
	/// </para>
	/// </summary>
	internal bool PausedByWindow() => Kind == Scene.Park && _windows is { AnyPausing: true };

	/// <summary>
	/// Whether the park's full-screen view is on (F3, <c>FUN_004a29d0</c>): the interface's layer hidden under a control
	/// that hears the camera's keys and mouse and nothing else - see <see cref="UI.ParkFrontEnd.ToggleFullScreen"/>.
	/// </summary>
	internal bool FullScreenView => Kind == Scene.Park && _windows is { Covered: true };

	/// <summary>
	/// Opens the purchase menu on this scene's own window stack.
	///
	/// <para>
	/// It exists for the debug console. A screen is verifiable by eye and capture rather than by test,
	/// and the console cannot move the pointer - a warp with no real motion
	/// behind it reaches the window system and never reaches the game - so the console needs a way in
	/// that does not go through the gadget's button. The button opens the very same screen.
	/// </para>
	/// </summary>
	internal void OpenBuyScreen()
	{
		if ( Kind == Scene.Park && _windows is { } windows )
			windows.Open( new UI.ParkBuyScreen( windows ) );
	}

	/// <summary>
	/// Opens the hire screen - the sibling of the purchase menu rather than a button of its own. See
	/// <see cref="OpenBuyScreen"/> for why the console needs a way in that does not go through the
	/// gadget.
	/// </summary>
	internal void OpenHireScreen()
	{
		if ( Kind == Scene.Park && _windows is { } windows )
			windows.Open( new UI.ParkHireScreen( windows ) );
	}

	/// <summary>
	/// Opens one of the gadget's CATEGORY screens - see <see cref="OpenBuyScreen"/> for why the console
	/// needs a way in that does not go through the gadget, and <see cref="UI.ParkCategoryScreens"/> for
	/// what a category is and why a button opens whichever screen it was last left on.
	/// </summary>
	/// <param name="screen">
	/// The original's own screen number, or nought for whichever one the category was last left on -
	/// which is what the gadget's button itself asks for.
	/// </param>
	internal void OpenCategoryScreen( int category, int screen = 0 )
	{
		if ( Kind != Scene.Park || _windows is not { } windows )
			return;

		if ( screen > 0 )
			UI.ParkCategoryScreens.Show( windows, category, screen );
		else
			UI.ParkCategoryScreens.Open( windows, category );
	}

	/// <summary>
	/// How long every voice still sounding takes to fade as a level ends. The original's state machine hands its
	/// stop-all (0x0051bcb0) 90, in the same untraced unit as the 60 that Sound_StopFading is handed when the advisor
	/// is quietened, which he already takes as milliseconds - so 0.09 seconds. <b>Inferred, not proven.</b>
	/// </summary>
	private const float StopAllSeconds = 0.09f;

	/// <summary>
	/// Ends the level. <see cref="ForgetPark"/> runs first, while the park still stands; then, in the order the original
	/// leaves its lobby (state 3), the interface goes - the windows
	/// close, and the front end empties the advisor's queue through his crying stop. Then every entity, in the
	/// order they were made, which ends the lobby's sound and weather before the advisor himself. Then the camera
	/// lets go of its island, every voice still sounding fades, the particle system shuts down, and a park's
	/// running state goes last - see <see cref="ForgetRunningPark"/>.
	///
	/// Only between frames: nothing may update or draw a level while it ends, or be half way through a walk over
	/// its entities.
	/// </summary>
	public void Unload()
	{
		// First, while the park still stands: letting go of a worker puts them down in the park's own people.
		ForgetPark();

		foreach ( var panel in Hud.Children.ToArray() )
			panel.Delete();

		foreach ( var entity in Entity.All.ToArray() )
			entity.Delete();

		Entity.ApplyDeletions();

		LobbyCameraMode.ForgetIsland();

		Audio.StopAll( StopAllSeconds );
		ParticleSystem.Current?.Shutdown();

		ForgetRunningPark();
	}

	/// <summary>
	/// Lets go of the running park itself - its state and its hiring pool - which would otherwise hold a left park
	/// in memory through the lobby. The last of <see cref="Unload"/>, after everything standing in the park has been
	/// deleted, as the original destroys every thing before it frees the world the pool is part of and zeroes its
	/// pointer (<c>0x004091b8</c>). See docs/exe/park-engine.md, "Leaving a park with something in the hand".
	/// Apart from <see cref="Unload"/> only so a test can reach it.
	/// </summary>
	internal static void ForgetRunningPark()
	{
		ParkState.ForgetCurrent();
		ParkStaffPool.ForgetCurrent();
	}

	/// <summary>
	/// Lets go of the park's static state that would otherwise carry into the next park: where both park cameras
	/// were, the armed build tool, the pinned pick, and whatever is in the hand. The first part of
	/// <see cref="Unload"/>, before anything in the park is deleted, and apart from it only so a test can reach it.
	/// </summary>
	internal static void ForgetPark()
	{
		// Both park cameras let go of where they were. Their state is static so that it survives
		// Camera.SetCameraMode building a fresh instance, which means it survives the scene as well
		// unless something says otherwise - so a second park would otherwise open looking at whatever
		// the first one was left looking at, and standing wherever it was last walked to.
		ParkCamcorderCameraMode.Forget();
		ParkOrbitCameraMode.Forget();

		// And whatever build mode was armed, for exactly the reason the two cameras above are forgotten:
		// it is static so that it survives a frame, which means it survives the SCENE as well unless
		// something says otherwise - and a park entered after one left mid-run would otherwise open with
		// a path tool armed and an anchor pointing at a cell in a different park.
		ParkBuildMode.Forget();
		ParkPicking.Pinned = null;

		// And whatever is in the hand, which would otherwise go down at the next park's first click. The
		// original's park end takes its interaction mode down while the park still stands - the save it makes on
		// leaving installs the idle mode over it (0x00516d13) - which runs the same uninstall as every other way
		// out: a candidate goes back to the pool, a worker is put down in their cell, and an item is let go of with
		// nothing built and nothing refunded, so a moved thing stays sold. See docs/exe/park-engine.md, "Leaving a
		// park with something in the hand".
		if ( ParkHand.LetGo() is { } letGo )
			Log.Info( $"Leaving the park: {letGo}" );
	}

	public void Render()
	{
		Camera.Update();

		// The ears go where the camera just went. Here rather than in Update because the camera itself
		// moves here, so a listener set during the update pass would be a frame behind the picture.
		Audio.SetListener( Camera.Position, Camera.Rotation.Forward );

		Entity.All.ForEach( entity => entity.Render() );

		// Everything see-through comes after everything solid, so a graded surface blends over a
		// finished picture rather than into a half-drawn one.
		//
		// This pass is not sorted within itself. These surfaces write depth, so two of them resolve by
		// distance rather than by the order their entities were created in.
		//
		// What a sort would still buy is blend order between two genuinely graded surfaces that
		// overlap. The original does sort for exactly that, per triangle and back to front, and
		// only for its graded and additive batches (FUN_00565590). Nothing in the lobby needs it:
		// the graded surfaces here are the shoreline ripples and the Space dish's cone, each a
		// single layer that does not overlap another.
		Entity.All.ForEach( entity => entity.RenderTranslucent() );

		// And the HUD on top of the finished world. Nothing in either pass above can reach it,
		// because it is not an entity at all - see RootPanel.
		Hud.Render();

		// Then whatever sits on the screen in front of everything, the HUD included - the advisor,
		// who covers whatever he pops up over. Depth is cleared first so nothing drawn before can
		// cut into him: the original draws him in screen space, flattened almost to nothing in
		// depth.
		global::Global.Render.CommandList.ClearDepthStencil( 1 );

		// A ride's window shows the ride itself, turning, in a panel. It is drawn HERE rather than with
		// the interface because it is a model: the HUD pass draws flat UI meshes, and this needs the
		// depth-cleared overlay stage the advisor already uses. A scissor keeps it inside the panel.
		if ( _windows is { } showing )
		{
			foreach ( var window in showing.Windows )
			{
				if ( window is UI.ParkObjectWindow ride )
					ride.DrawPreview();
			}

			// Draw the active warning after every preview, but beneath the advisor. Covered windows
			// keep their HUD drawing order; the preview pass has its own existing overlay behaviour.
			if ( showing.Windows.LastOrDefault( window => !window.Hidden && !window.PutAway ) is UI.ParkObjectWindow activeRide )
				activeRide.DrawStatus();
		}

		Entity.All.ForEach( entity => entity.RenderOverlay() );
	}
}
