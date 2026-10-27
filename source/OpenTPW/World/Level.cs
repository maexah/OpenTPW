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
	/// seventy units out. The scene's own - see <see cref="FogDensityFor"/>.
	/// </summary>
	public static float FogDensity => FogDensityFor( Current?.Kind );

	/// <summary>
	/// The lobby's haze: clear up close and a wall at the horizon.
	///
	/// <para>
	/// Not the original's. Its lobby fogs linearly, none before depth 50 and the fog colour alone at 250 (the three
	/// lobby states that call <c>FUN_005d9680</c> then <c>FUN_005d96a0</c>, <c>0x005e1407</c>, <c>0x005e1980</c>,
	/// <c>0x005e24a2</c>; the lobby's own initialisation sets 50 to 300 with the fog off, <c>0x005d8bef</c>;
	/// docs/exe/park-engine.md, "The park view's colours"), where the shaders' <c>exp</c> here has no start and no end.
	/// It is the value the global fog had before parks stopped using it, and the lobby with it has not been
	/// photographed against the original's.
	/// </para>
	/// </summary>
	internal const float LobbyFogDensity = 0.025f;

	/// <summary>
	/// The haze a scene is drawn with: none in a park, the lobby's anywhere else.
	///
	/// <para>
	/// The original draws a park's own view with no fog: the terrain and the objects ask for it only under
	/// render-word <c>0x2000</c> (FUN_0056f670 calls FUN_00565ee0 behind it), which <c>Game_StateMachine</c> turns on
	/// while <c>gui_CameraFlags &amp; 0x16</c> is (first person, a ride's view; <c>0x0054fae2</c>). That fog is
	/// <see cref="LinearFog"/>, not this.
	/// </para>
	/// </summary>
	internal static float FogDensityFor( Scene? kind ) => kind == Scene.Park ? 0f : LobbyFogDensity;

	/// <summary>
	/// The park's first-person fog band, read from the detail file as the park loads (the original reads it once a
	/// start, <c>DetailFile_Apply</c>), or null outside a park. The console's <c>fog</c> holds another band.
	/// </summary>
	public FirstPersonFog? FirstPersonFog { get; set; }

	/// <summary>
	/// The shaders' <c>g_vLinearFog</c> for this frame's world draws: first person's fog in a park while first person is
	/// up, nought otherwise (<see cref="LinearFogFor"/>).
	/// </summary>
	internal static System.Numerics.Vector4 LinearFog =>
		LinearFogFor( Current?.Kind, InsideView, Current?.FirstPersonFog );

	/// <summary>
	/// Whether the camera is in a view from inside the park, <c>gui_CameraFlags &amp; 0x16</c>: first person (<c>0x2</c>)
	/// or a ride's view (<c>0x4</c>; the End feature's is the only one built). The original sets its render word's
	/// <c>0x2000</c> with <c>0x80</c> while it is (<c>Game_StateMachine</c>'s <c>XOR 0x2080</c>, <c>0x0054fae2</c>), which
	/// fogs the world and stops the pull on the people's bodies, and skips the picker under the same test
	/// (<c>0x0054e2c0</c>). The view of <c>0x10</c> is not decoded.
	/// </summary>
	internal static bool InsideView => ParkCamcorderCameraMode.Active || ParkRideViewCameraMode.Active;

	/// <summary>
	/// First person's fog where it reaches: in a park, in first person or a ride's view (<see cref="InsideView"/>).
	/// </summary>
	internal static System.Numerics.Vector4 LinearFogFor( Scene? kind, bool firstPerson, FirstPersonFog? fog ) =>
		kind == Scene.Park && firstPerson && fog != null ? fog.Uniform : default;

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

	/// <summary>The park gadget's staff and visitor locators, which a click on the park asks (<see cref="PickPerson"/>).</summary>
	internal UI.ParkLocator? Locator { get; set; }

	/// <summary>Which of those items the park has researched, the save's word first - see <see cref="ParkResearch"/>.</summary>
	public ParkResearch? Research { get; private set; }

	/// <summary>
	/// The calendar's compare and its turns, then the golden tickets the sweeps owed (<see cref="ParkGoldenTickets.Owe"/>),
	/// in the order the original's sweep tail has them: the calendar (<c>0x004f8260</c>), then the tickets' test on every
	/// hundredth tick (<c>0x004d7b54</c>, <c>docs/exe/ride-operation.md</c>, "Golden tickets"), so a tick that turns a year
	/// is tested on the new year's profit. A deviation: the original compares at the tail of every sweep, so in a frame of
	/// two or three sweeps whose day turns before the last, its rings roll and its month turns between two sweeps, and ours
	/// after the last, the later sweeps counting into the closing day; the tickets owed by an earlier sweep of the frame are
	/// tested here on the park as that frame leaves it. The sweep is split across the entities, so a compare per sweep waits on
	/// one sweep loop for all of them (<c>docs/exe/park-engine.md</c>, "Still parted").
	/// </summary>
	/// <param name="tickets">The ticket test, given the tick that owed it.</param>
	internal static void TurnTheCalendar( ParkState state, ParkPeople? people, ParkItemCatalogue? catalogue, Action<int> tickets )
	{
		GameCalendar.Update( state.GameTick );

		if ( GameCalendar.DayRolled || GameCalendar.MonthRolled || GameCalendar.YearRolled )
			Log.Info( $"Calendar: {GameCalendar.Now:yyyy-MM-dd} on mGameTick {GameCalendar.Counter}, the day's change"
				+ (GameCalendar.DayRolled ? "" : " NOT") + (GameCalendar.MonthRolled ? ", the month's" : "")
				+ (GameCalendar.YearRolled ? ", the year's" : "") );

		// The day's change, then the month's and the year's, which the calendar sends after the day's in the same tick. The
		// gadget's date and trend arrow are read between the day's and the month's (FUN_00481560, 0x004f83ac).
		if ( GameCalendar.DayRolled )
		{
			state.RollTheDay();
			state.ReadTheTrend();
		}

		if ( GameCalendar.MonthRolled )
			ParkPeople.TurnTheMonth( people, state, catalogue );

		if ( GameCalendar.YearRolled )
			state.TurnTheYear();

		if ( ParkGoldenTickets.TakeOwed() is { } owed )
			tickets( owed );
	}

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
		// a trap set for the first thing that ever keys on it. Global's path below is the one still built
		// from the name as given; the file system matches a name whatever its case.
		ThemeName = levelName.ToLowerInvariant();
		Kind = kind;

		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		// The seconds this scene is about to spend loading are not ticks anybody owes - see GameClock.Rebase,
		// and the original doing the same as it enters a park (0x0054ed7c).
		GameClock.Rebase();

		// The lobby has no calendar, so it starts over from nought and stays there, rather than showing the date of the
		// park before it. A park's is put on the park's own clock once the park is read (SetupParkEntities).
		if ( kind != Scene.Park )
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
		// Deviation: the original turns each island and its gate by the island's ISLAND heading
		// (docs/exe/lobby.md, 0x005dfa60), which this never does; once Q721 turns them the gates face four
		// different ways and this one sun no longer faces them all.
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

		LobbyCameraMode.BeginLobby();
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
	/// The advisor is handed a park's lines by <see cref="ParkAdvisorMessages"/>, which scores its built messages
	/// through eight slots and polls one. The original's other park messages are not built.
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

		// The theme's fog colour. A park's own view fades nothing to it (FogDensityFor) and first person fades to it
		// (LinearFog); the frame is cleared to it and the sky hazes into it. The lobby's Sky rewrites this every frame from its own horizon,
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

		ParkLight = ParkLightFor( Balance.Colour( "ThemeEngine.AmbientLightLevel", new Vector3( 0.333f ) ), travels, DetailFile );
		Log.Info( ParkLight.Lit
			? $"{ThemeName}: lit per vertex by ambient {ParkLight.Ambient} and a sun travelling {travels}"
			: $"{ThemeName}: {DetailFile}'s LIGHTING 0, so every vertex takes the full colour, no sun, no ambient, no half light" );

		FirstPersonFog = FirstPersonFog.Read( DetailFile );
		Log.Info( $"{ThemeName}: {DetailFile}'s FIRSTPERSONVIEWDISTANCE fogs first person from {FirstPersonFog} in {FogColour}" );

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
		// three textures the lobby's does, and the original loads them through the same loader
		// (FUN_005852b0; docs/exe/park-engine.md, "The sky").
		//
		// Centred on the world origin at 300, because that is where the sky object puts itself and
		// NOTHING re-centres it for a park. Untinted, because a park has no island script and so no
		// SKYCOLOUR - see Sky's constructor for everything that decides.
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

		StartTheCalendar( ThemeName, park );

		ParkState = new ParkState( park )
		{
			RegionEffects = ParkRegionEffects.From( Balance ),
			AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), cursor: ParkAdvisorMessages.ProgramCursor )
		};
		Log.Info( $"{ThemeName}: the park's clock starts at mGameTick {ParkState.GameTick}" );
		DressTags( ParkState.Tags, Balance );

		if ( park?.Save?.Tags.Problem is { } unread )
			Log.Warning( $"{ThemeName}: the file's aerial tags did not read - {unread}" );
		// The advisor's records of the built messages are the file's; a park made fresh keeps FUN_00599e70's.
		if ( park?.Save is { } advised )
		{
			Log.Info( $"{ThemeName}: {ParkState.AdvisorMessages.Restore( advised.AdvisorHistoryOf )} of the advisor's built messages have a record in the file" );
			Log.Info( $"{ThemeName}: {ParkState.AdvisorMessages.RestoreSlots( advised.AdvisorSlotOf )} of the file's waiting advisor messages are taken up" );
			// His last action is timed on the file's second stopwatch; a file whose clock did not read keeps none.
			if ( advised.AdvisorActionOf() is { } action && advised.Clock.Second is { } stopwatch )
				ParkState.AdvisorMessages.RestoreAction( action, stopwatch, (long)(GameClock.Now * 1000) );
			// The poll goes on from the file's row; a park made fresh keeps the program's.
			if ( advised.AdvisorPollCursorOf() is { } row )
				ParkState.AdvisorMessages.RestoreCursor( row );
		}
		load.Mark( "park state" );

		Research = park == null || catalogue == null ? null : new ParkResearch( park.ObjectControlRecords, catalogue, park.ResearchProjects,
			park.ResearchKinds, park.ResearchWorkLoad, ParkResearch.GroupOpensAt( Balance ),
			park.ResearchMostExpensiveProject )
		{
			Unlocked = id => Players.Roster.Current?.File.RideIds.Contains( (ushort)id ) == true
		};
		if ( Research is { } researched )
		{
			Log.Info( researched.Summary );
			var state = ParkState;
			HearResearch( researched, state, catalogue!, ThemeName, () => OweTheEndFeature( state ) );
		}

		_ = MakeGround( ThemeName, park, Balance, ParkLight.Lit );
		load.Mark( "ground" );

		_ = new ParkPaths( ThemeName, park );
		load.Mark( "paths" );

		_ = new ParkQueues( ThemeName, park );
		load.Mark( "queues" );

		// The queue tool's squares, drawn over whichever of those three is under them.
		_ = new ParkBuildMarkers();

		_ = new ParkTerrain( ThemeName );
		load.Mark( "terrain" );

		// The moving sea over the harbour, laid over the terrain's still lake.
		_ = ParkSea.For( Balance, TextureQuality() );
		load.Mark( "sea" );

		// The gates' flags, which wave at the sea's phase.
		_ = ParkFlags.For( TextureQuality() );
		load.Mark( "flags" );

		// The objects before the fixed items, which is the one ordering here that matters: the gate and the
		// traffic lights are swept out of the very registry the placed things are swept from, so it has to
		// exist before they can put themselves into it.
		var objects = new ParkObjects( ThemeName, park, catalogue );
		load.Mark( "objects" );

		_ = new ParkFixedItems( ThemeName, park, objects );
		load.Mark( "fixed items" );

		// The wall round the land the park owns, made again from the file's model records, or for a park made
		// fresh from the pieces its walk chose.
		_ = new ParkBoundaryHoarding( ThemeName, park, catalogue );
		load.Mark( "boundary" );

		// The karts' and water rides' track pieces, one at each anchor.
		_ = new ParkTrackPieces( ThemeName, park );
		load.Mark( "track" );

		// And the scripts those objects run. After the objects, because a script belongs to something
		// standing in the park rather than the other way round - and now literally so: a script is handed
		// the very animation player its own thing's model is posed from, and its tick loop is what drives
		// the sweep that poses it.
		var rides = new ParkRides( ThemeName, park, catalogue, objects: objects );
		load.Mark( "rides" );

		// The bumper rides' cars, drawn from what the rides' track tick leaves them - so after the rides.
		_ = new ParkBumperBoats( ThemeName, catalogue );

		// And the particles the park's scripts start, drawn where they stand.
		_ = new WorldParticles();

		// And the effects the land itself carries, started as the level loads: the jungle's waterfall's spray.
		ParkPlacedObjects.Start( ThemeName, ParticleSystem.Current );

		// And the park's people. After the ground, because a guest stands on the land and has to ask how
		// high it is under them; they are sprites rather than models, so they are nothing to do with the
		// objects above and only need the save that named them.
		// How many banks of each kind the guests draw over, the kid banks capped by the detail file.
		var banks = ParkSpriteBanks.Read( FileSystem, ThemeName, NumKids() );

		_ = new ParkGuestSprites( ThemeName, park, banks );
		load.Mark( "guest sprites" );

		// Who the park may hire, which is a population of its own and nothing to do with the people
		// already in it - see ParkStaffPool. Built before the park's own people only for readability;
		// neither asks anything of the other.
		// ParkPeople, built after it, is what those people want, which is deliberately not the same object
		// as what they look like: the sprites above never run a tick, and ParkPeople never touches a vertex.
		// It goes after them only for readability - it asks nothing of them.
		// It is handed the two things the admission states need that the save alone cannot answer: the
		// balance stack, for what a guest will put up with paying, and the gate's own script state, which
		// is what a guest waiting outside is actually waiting on.
		// And the scripts a ride's own turn drives, by the same delegate argument the gate uses: the
		// people need one script per thing and nothing else from the rides, and the rides are built above
		// this line so they cannot be handed the people instead.
		StaffPool = StaffPoolFor( park, Balance, ParkState?.GameTick ?? 0 );
		load.Mark( "staff pool" );

		_ = new ParkPeople( park, Balance, () => rides.GateStatus( park ), ParkState, catalogue,
			thingId => rides.Scheduler.Find( rides.ScriptFor( thingId ) ), banks: banks, research: Research );
		load.Mark( "people" );

		// Each group of sound at the volume the options give it, and then the park's own music - which
		// is the order the original uses too: it registers the park's categories, re-applies the group
		// volumes (0x0054ec9a), and only then plays the music (0x0054ec9f). See ParkAudio for what it
		// does with it afterwards: the crowd drives its level, as the original's does.
		GameOptions.Current.ApplySound();
		var audio = new ParkAudio( ThemeName );

		// And the sounds the land itself carries, started as the level loads: the jungle's waterfall and its river,
		// and its gulls and its sea, which follow the listener.
		ParkPlacedObjects.StartSounds( ThemeName, audio.StartPlaced, audio.StartFollowing );

		// And the park file's live voices, which the original's load puts back (FUN_0051c400, 0x0051c497): the held
		// chains here, each handed to the resumed script record that names its handle (ParkAudio.PutBackFileVoices).
		if ( park is ParkWorld world )
		{
			var voices = audio.PutBackFileVoices( world.Voices );
			var taken = rides.Scheduler.Scripts.Sum( script => script.TakeRestoredSounds( voices ) );

			Log.Info( $"{ThemeName}: {voices.Count} of the park file's voices put back, {taken} of them taken by a script's record" );
		}

		load.Mark( "audio" );

		// After the audio, because a park opens with its weather already rolled and that first roll
		// may want to start the rain straight away.
		_ = new ParkWeather( park?.Save?.WindDirection );
		load.Mark( "weather" );

		// The advisor last, as in the lobby: he rides on top of the rest and ducks everything above him
		// while he talks, and the teardown runs in the order things were made, so a park's sound and its
		// weather end before he does. Advisor_Update runs from a park's loop (0x0054f9f9) exactly as it
		// runs from the lobby's (0x0054e6df) - he is the same speaker in both scenes, and what differs is
		// who hands him lines: a park's are ParkAdvisorMessages'.
		_ = new Advisor( ThemeName, Balance, ParkLight );
		load.Mark( "advisor" );

		load.Done();

		Camera.SetCameraMode<ParkOrbitCameraMode>();
	}

	/// <summary>
	/// A park read from a file runs its particles at the file's density, as the original's loader leaves it
	/// (<c>FUN_0051f7a0</c> copies the file's live header, its density at <c>+0x20</c>); a park made fresh, or a file
	/// whose particles module will not read, keeps the options' (<c>docs/exe/park.md</c>, "A park's particles").
	/// Answers the density taken, or null where the options' stands.
	/// </summary>
	internal static int? TakeParticleDensity( string themeName, IParkInitialState? park, ParticleSystem? particles )
	{
		if ( particles == null || park is not ParkWorld world )
			return null;

		if ( world.Particles.Problem is { } problem )
		{
			Log.Info( $"Particles: {themeName}'s park file has no particles module to read ({problem}), so effects run at the options' density {particles.Density}" );
			return null;
		}

		Log.Info( $"Particles: {themeName}'s park file holds density {world.Particles.Density}, which replaces the options' {particles.Density}" );
		particles.TakeFileDensity( world.Particles.Density );

		return world.Particles.Density;
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
	/// Puts the calendar on the park's own clock, <c>mGameTick</c>, with the file's month, day and rate - see
	/// <see cref="GameCalendar.Rebase"/>.
	/// </summary>
	internal static void StartTheCalendar( string theme, IParkInitialState? park )
	{
		GameCalendar.Rebase( park?.GameTick ?? 0, park?.Save?.Calendar );
		Log.Info( $"{theme}: the calendar starts on {GameCalendar.Now:yyyy-MM-dd HH:mm:ss}, the date of mGameTick {GameCalendar.Counter}" );
	}

	/// <summary>
	/// A park file, walked, or null where there is nothing to read. The running particle system takes the file's density
	/// (<see cref="TakeParticleDensity"/>).
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

			// Taken as the file is read, before anything of the park starts an effect, as the original's file read
			// calls its loader (0x00415437).
			TakeParticleDensity( themeName, world, ParticleSystem.Current );

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
	/// <c>FUN_005ac610</c> writes (<c>docs/exe/saves.md</c>, "The save" and "Module by module"), or under the extension
	/// given, as <c>FUN_005ac630</c> writes <c>restart.INTS</c>, and answers where, or null where nothing was written.
	///
	/// <para>
	/// <b>The stages built are the ones <c>docs/exe/saves.md</c>'s "OpenTPW's writer" sections describe</b>
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
	/// <b>In the original's order</b>: the player's <c>gms.dat</c> first (<c>FUN_005c8a10</c>, the save's step 1),
	/// then the idle mode installed over the hand (<see cref="ParkHand.LetGo"/>; the world writer does it before it
	/// writes anything, <c>0x00516d13</c>), so a worker held is put down and written where they stand, then the park.
	/// </para>
	/// <para>
	/// <b>Deviations.</b> A park made fresh, with no file behind it, is counted and not written. With nobody playing
	/// there is no folder, and nothing is written; the original has a player on every entry. A save that fails
	/// leaves the file already at its path as it was, where the original deletes it on most failures after the
	/// open (<c>docs/exe/saves.md</c>, "The save", step 3): a fill in the player's favour, so a failed save never
	/// costs them the old one. The bytes go to a file beside it that then takes its place
	/// (<see cref="BaseFileSystem.WriteAllBytes"/>), so a failure never leaves a part-written park either. A failure
	/// of any kind is logged and answered with null, and the game goes on, as the original's save answers a failure
	/// number and its screen closes all the same.
	/// </para>
	/// </summary>
	internal string? WritePark( string name, string extension = SaveFolder.SavedParkExtension )
	{
		if ( Kind != Scene.Park || ParkState is not { } state )
		{
			Log.Warning( $"Save: '{name}' is not written - no park is running" );
			return null;
		}

		try
		{
			Players.Roster.Current?.Save();

			if ( ParkHand.LetGo() is { } letGo )
				Log.Info( $"Save: the idle mode is installed before the park is written: {letGo}" );

			return WritePark( Park, state, ThemeName, name, ParkPeople.Current, ParkStaffPool.Current, ParkRides.Current, Catalogue,
				extension, Research );
		}
		catch ( Exception e )
		{
			Log.Error( $"Save: '{name}' is not written - the save failed: {e}" );
			return null;
		}
	}

	/// <summary>The name the park is written under as it is left (<c>0x00747a58</c>).</summary>
	internal const string AutosaveName = "autosave";

	/// <summary>
	/// Writes the running park as <c>autosave.TPWS</c> when the scene is a park, as the original's state 0xb does
	/// before it takes the park down (<c>0x0054ff10</c>; <c>docs/exe/saves.md</c>, "Leaving a park: the autosave"): the park
	/// menu's Exit To Lobby and Quit Game, and the game closed with a park up. Entering the theme again finds it
	/// as the newest park file (<see cref="SaveFolder.NewestPark"/>). The original skips it only online, which is
	/// not built. Answers where it was written, or null.
	/// </summary>
	internal string? WriteParkOnLeaving()
	{
		if ( Kind != Scene.Park )
			return null;

		var written = WritePark( AutosaveName );
		Log.Info( $"Leaving the park: {AutosaveName} {(written is null ? "is not written" : $"is written to {written}")}" );

		return written;
	}

	/// <summary>
	/// Hooks the lab's three tellings to the running park: a finish to the advisor and to the analyser's ring of the
	/// item's buy list tab, its <c>Info.WhichUIType</c> whatever the project's kind (<c>FUN_00412e90</c>'s <c>+0x4ac</c>,
	/// read at <c>0x004c73c3</c>), a group raised to the advisor, and the end to <see cref="EndOfResearch"/>, with
	/// <paramref name="endFeature"/> run where the End feature follows.
	/// </summary>
	internal static void HearResearch( ParkResearch research, ParkState state, ParkItemCatalogue catalogue, string theme,
		Action endFeature )
	{
		research.Researched = ( item, kind, tick )
			=> state.HearResearched( item, kind, catalogue.TryGet( item, out var made ) ? made.UiType : -1, tick );
		research.GroupOpened = state.HearGroupOpened;
		research.AllDone = tick =>
		{
			if ( EndOfResearch( state, theme, tick ) )
				endFeature();
		};
	}

	/// <summary>
	/// The end of research (<see cref="ParkResearch.AllDone"/>; <c>Lab_OnMessage</c>, <c>0x00503e15</c>..<c>0x00503fb7</c>):
	/// the park marked all researched in the player's progress and, where <see cref="ParkResearch.MarkAllResearched"/>
	/// says they follow, advisor message <c>0xa7</c> (row 167) posted about no thing; true where the End feature follows.
	/// </summary>
	private static bool EndOfResearch( ParkState state, string theme, int tick )
	{
		var player = Players.Roster.Current;
		var follows = ParkResearch.MarkAllResearched( player?.File, theme, InstantAction );
		Log.Info( $"Research: every project done on mGameTick {tick}; "
			+ (player is null ? "nobody is playing, so no park is marked" : $"{player.Name}'s {theme} record marked all researched")
			+ (follows ? ", so row 167 and the End feature follow" : ", and a park of theirs is not, so nothing follows") );

		if ( follows )
			state.AdvisorMessages?.Post( ParkAdvisorMessages.AllResearched, tick );

		return follows;
	}

	/// <summary>
	/// The End feature made at the end of research (<c>World_MakeEndFeature</c>, <c>0x00516b00</c>;
	/// <see cref="ParkEndFeature"/>), owed until this frame's entity walk is over.
	/// </summary>
	private void OweTheEndFeature( ParkState state )
	{
		if ( Catalogue is not { } catalogue || ParkRides.Current is not { } rides || ParkObjects.Current is not { } objects
			|| _windows is not { } windows )
		{
			Log.Warning( "Research: the park has no catalogue, rides, objects or windows, so no End feature is made" );
			return;
		}

		// Made after this frame's entity walk, in which the lab's sweep runs: the original makes it inside the lab's
		// message (0x00503fb7), so ours stands up later in the same frame, before anything is drawn.
		_endFeatureOwed = () => ParkEndFeature.Make( catalogue, state, rides, objects, windows, InstantAction );
	}

	/// <summary>The End feature owed by <see cref="OweTheEndFeature"/>, made after the frame's entity walk.</summary>
	private Action? _endFeatureOwed;

	/// <summary>
	/// Writes the park just entered as <c>restart.INTS</c> where the player's folder for the theme holds none, as the
	/// original's state 0xf does after the park's load and before its first tick (<c>FUN_00550b30</c>;
	/// <c>docs/exe/saves.md</c>, "Restart Park: restart.INTS"): the save whole, the player's <c>gms.dat</c> first. A file
	/// there already is left as it is, so Restart Park goes back to the park as the theme was first entered. The original
	/// skips it only online, which is not built. Answers where it was written, or null.
	/// </summary>
	internal string? WriteRestartOnEntering()
	{
		if ( Kind != Scene.Park || Players.Roster.Current is not { } player )
			return null;

		if ( SaveFolder.RestartParkPath( player.Slot, player.Name, ThemeName ) is { } there )
		{
			Log.Info( $"Entering the park: {there} is there already, so it is not written" );
			return null;
		}

		var written = WritePark( SaveFolder.RestartName, SaveFolder.RestartExtension );
		Log.Info( $"Entering the park: {SaveFolder.RestartName}{SaveFolder.RestartExtension} "
			+ (written is null ? "is not written" : $"is written to {written}") );

		return written;
	}

	/// <summary><see cref="WritePark(string, string)"/> for a park's own parts, the camera's being the orbit camera's.</summary>
	internal static string? WritePark( IParkInitialState? park, ParkState state, string theme, string name,
		ParkPeople? people = null, ParkStaffPool? pool = null, ParkRides? rides = null, ParkItemCatalogue? catalogue = null,
		string extension = SaveFolder.SavedParkExtension, ParkResearch? research = null )
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

			// The file's own record, or a thing on its id that is still it: an id deleted since the load names a new thing.
			if ( loaded.Objects.Any( file => file.ThingId == thing.ThingId
				&& (file == thing || !state.DeletedSinceLoad.Contains( thing.ThingId )) ) )
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
				"data\\" + item.Directory.Replace( '/', '\\' ).TrimEnd( '\\' ) + "\\", ParkFixedItems.IsVehicle( thing.CatalogueId ) );
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

		// The file's ids deleted since the load and taken out of it: a thing on one now is a new one, written whole as
		// made (docs/exe/saves.md, "OpenTPW's writer, an id given out again"). An object sold that the file keeps,
		// counted above, keeps its id there, and a person made on it is left out, counted. A deviation: the original writes
		// the person and drops the object. Where the things go out as the file's (above: a file whose clock, scripts or models
		// did not read, as Alexah's jungle Full Simulation saves' RSYS module, Q167) every object of the file is kept, sold or
		// not, so a person on any sold object's id is left out. What is written then still names the left-out thing: the other
		// guests' queue links, and in the things-written case the ride's head and nominee, a script's slots, a handle, the
		// arrival slots, the mechanic's cursor and the region effects name the id, which the file holds as the kept object
		// (docs/exe/saves.md, "OpenTPW's writer, an id given out again"; Q607).
		var keptObjects = new HashSet<int>( loaded.Objects.Select( thing => thing.ThingId ).Where( id => !gone.Contains( id ) ) );
		var fileIds = new HashSet<int>( loaded.Things.Select( thing => thing.ThingId ) );
		var ids = new ParkWorld.ThingIds( state.DeletedSinceLoad.Where( id => fileIds.Contains( id ) && !keptObjects.Contains( id ) ).ToHashSet(),
			state.MadeAt );

		var writtenPeople = people?.Written( written.Contains ).Where( person =>
		{
			if ( !keptObjects.Contains( person.Person.ThingId ) )
				return true;

			Unimplemented.Report( "SAVE_PARK_ID_KEPT_FOR_A_SOLD_THING" );
			return false;
		} ).ToList();

		if ( things != null && pieces!.Count > 0 )
			things = things with { QueueCells = pieces };

		var running = new ParkFileWriter.Running( state.GameTick, state.ParkIsClosed, state.VisitorsToDate, state.Balance,
			new ParkCameraModule.View( ParkOrbitCameraMode.Zoom, -ParkOrbitCameraMode.Yaw, point.X, point.Y ), cells,
			writtenPeople, pool?.Written(), people?.WrittenArrival( things != null ? written.Contains : null ), things, people?.WrittenLetGo(),
			WrittenEffects( loaded, state, written.Contains, asTheFile: things == null ),
			written.Contains( state.MechanicCursor ) ? state.MechanicCursor : 0,
			// The aerial's tags end the file (FUN_0048ee50). A file whose own block did not read keeps its bytes.
			loaded.Tags.Problem == null ? state.Tags.Written() : null,
			state.AdvisorMessages?.Written(),
			// A waiting message's second word is its thing's item (FUN_0059b590's callers hand it [ride + 0xe]).
			state.AdvisorMessages?.WrittenSlots( subject => state.TryObject( subject, out var about ) ? about.CatalogueId : 0 ),
			state.AdvisorMessages is { } messages ? stopwatch =>
			{
				var action = messages.WrittenAction( stopwatch, (long)(GameClock.Now * 1000) );
				Log.Info( $"Save: advisor action written: started {action.Started} duration {action.Duration} playing {action.Playing} stopwatch {stopwatch}" );
				return action;
			} : null,
			state.AdvisorMessages?.PollRow, state.LastStaffHired, state.LastObjectBuilt,
			people != null ? (people.TrainingBudgets.ToArray(), people.HaveEverTrained) : null,
			state.MonthRings, (state.LifetimeVisitors, state.MostPaidForTicket), state.MisbehavingKids, state.ParkLastOpened,
			state.RandomSeed,
			people != null ? (people.Strikes.ForceWord, people.Strikes.Records) : null,
			state.Economy, state.CalendarAtLastUpdate, ParkWeather.Current?.Written( loaded.WeatherRecord ), research?.Written,
			state.RecentlyResearched.Written, ids, ParkAudio.Current?.FileVoicesLetGo );

		// The sound module goes out as the loaded file's, less the voices put back from it and let go since: the original
		// writes the manager's live voices (Sound_SaveModule, 0x0051c350), so what was started here, and each put-back
		// voice's children and draws since, are not written. Carried, as the coasters' and the advisor's modules are, and
		// counted where the park holds a voice started here (PARK_FILE_SOUND_WRITER_LIVE_VOICES). With no sound device
		// nothing was put back, so nothing is let go and the file's voices are all carried, where the original's save
		// writes no voice list at all (FileFormats saves.md): a sold Loudspeaker's chain survives such a save.
		if ( ParkAudio.Current is { HoldsVoicesStartedHere: true } )
			Unimplemented.Report( "PARK_FILE_SOUND_WRITER_LIVE_VOICES" );

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

		if ( SaveFolder.WritePark( player.Slot, player.Name, theme, name, file, extension ) is not { } path )
			return null;

		if ( people != null )
			Log.Info( $"Save: strikes written: {people.Strikes.Describe()}" );

		Log.Info( $"Save: economy written: {state.DescribeEconomy()}" );
		Log.Info( $"Save: calendar written: month {running.Calendar!.Value.Month} day {running.Calendar.Value.Day}, "
			+ $"the date of tick {state.GameTick}, {state.CalendarNow:yyyy-MM-dd}" );

		if ( running.Weather is { } weather )
			Log.Info( $"Save: weather written: {ParkWeather.Describe( weather )}" );

		foreach ( var (message, history) in running.AdvisorHistories ?? new Dictionary<int, ParkWorld.AdvisorHistory>() )
		{
			if ( history != ParkWorld.AdvisorHistory.Fresh )
				Log.Info( $"Save: advisor message {message} written: line {history.Line} lastTick {history.LastTick} said {(history.Said ? 1 : 0)} slaps {history.Slaps}" );
		}

		for ( var index = 0; index < (running.AdvisorSlots?.Count ?? 0); ++index )
		{
			if ( running.AdvisorSlots![index] is { Valid: true } slot )
				Log.Info( $"Save: advisor slot {index} written: message {slot.Message} thing {slot.Thing} secondary {slot.Secondary} score {slot.Score}" );
		}

		foreach ( var tag in running.Tags ?? [] )
			Log.Info( $"Save: aerial tag {tag.Id} written: thing {tag.Thing} item {tag.Item} life {tag.Life} picture {tag.Picture} "
				+ $"buttons {tag.Button1},{tag.Button2} \"{tag.Text}\"" );

		Log.Info( $"Save: wrote {path}, {file.Length} bytes: mRandomSeed 0x{running.RandomSeed:x8}, mGameTick {running.GameTick}, " +
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
				+ (arrival.CurrentVehicle is { } vehicle ? $", mCurrentArrivalVehicle {vehicle}" : ", the vehicle left the file's")
				+ (arrival.Vehicles is var (size1, size2, size3) ? $", mArrivalVehicle_Size1..3 {size1} {size2} {size3}" : "") );
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
	/// nothing bought or sold is handed to this overload. A person's handle to any other thing is written as nought.
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

	/// <summary>The aerial's tags given the running language's strings and the park's golden-ticket thresholds, which their texts read.</summary>
	internal static ParkTags DressTags( ParkTags tags, ParkBalance? balance )
	{
		tags.Strings = TagStrings()?.Parts;
		if ( balance != null )
			tags.Thresholds = ParkGoldenTickets.Thresholds.From( balance );
		return tags;
	}

	/// <summary>The running language's table of the aerial tags' strings, or null where it will not read.</summary>
	private static StringFile? TagStrings()
	{
		try
		{
			return new StringFile( ParkTags.StringsPath );
		}
		catch ( Exception e ) when ( e is IOException or InvalidDataException or ArgumentException or NullReferenceException )
		{
			Log.Warning( $"Park tags: {ParkTags.StringsPath} did not read - {e.Message}" );
			return null;
		}
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
	/// counted</b>: this overload is handed no thing bought or sold, so the thing that stands there is not written, nor the cell's <c>mWho</c>, and the file
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

			// A kart or water piece laid here is written as its track record; its model record is not made, so the original
			// loading the file draws no piece there.
			if ( ParkTrackPieces.IsPieceAnchor( now.TrackType ) && (was.TrackType, was.TrackTileIndex) != (now.TrackType, now.TrackTileIndex) )
				Unimplemented.Report( "SAVE_PARK_TRACK_PIECE_MODEL" );

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
		&& (a.TrackType, a.TrackFlags, a.TrackParentId, a.TrackNeighbours, a.TrackDirection, a.TrackTileSet, a.TrackTileIndex, a.TrackTileAngle)
			== (b.TrackType, b.TrackFlags, b.TrackParentId, b.TrackNeighbours, b.TrackDirection, b.TrackTileSet, b.TrackTileIndex, b.TrackTileAngle);

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
		Locator = Hud.AddChild( new ParkFrontEnd( windows, ThemeName ) ).Gadget.Locator;

		// The same on-screen effects the lobby has, and for a reason rather than for symmetry: a park
		// makes button glints. SetupParticles runs for parks as well as for the lobby, the windows here
		// start and stop glints exactly as the lobby's do - see OptionsScreen.Closed, which calls
		// StopGlint - and WindowStack draws them through ScreenParticles.Current, which this sets.
		//
		// Over the interface it decorates and under the pointer, which is the order SetupHud uses.
		Hud.AddChild( new ScreenParticles() );

		_cursor = AttachPointer( Hud, new Cursor() );
	}

	/// <summary>
	/// Hands the pointer to the interface as its <see cref="RootPanel.Pointer"/>. Not a child: the pointer is
	/// drawn after the overlay pass (<see cref="RenderOverTheWorld"/>), and as a child it would be drawn a second time,
	/// under the previews.
	/// </summary>
	internal static T AttachPointer<T>( RootPanel hud, T pointer ) where T : Panel
	{
		hud.Pointer = pointer;

		return pointer;
	}

	private void SetupHud()
	{
		Hud = new();

		// The build's name, bottom right, under every window (the original's "v 2.0"; a deviation said there).
		Hud.AddChild( new VersionLabel( LaunchReport.RunningBuildName ) );

		// The interface's windows, and what the pointer and the keys do to them.
		var windows = _windows = Hud.AddChild( new WindowStack() );

		// The lobby's interface - the player slots, its dialogs and the island panel - opened in those windows.
		// Built here, behind the loading screen, along with everything its windows draw. After the stack, so it
		// hears about a frame's clicks and keys once the stack has dealt them out.
		Hud.AddChild( new FrontEnd( windows ) );

		// On-screen particle effects go over the interface they decorate, and under the pointer.
		Hud.AddChild( new ScreenParticles() );

		AttachPointer( Hud, new Cursor() );
	}

	/// <summary>
	/// The particle system, which the state machine loads before the lobby itself (Data\Particle\Tp2.plb).
	///
	/// Its density comes from the detail file for the options' graphics quality - low.sam, med.sam or
	/// high.sam, going by their names - which starts at medium. A park read from a file then runs at the file's
	/// (<see cref="TakeParticleDensity"/>).
	///
	/// <b>A deviation:</b> the detail file is read as each level loads, where the original reads it once, at boot, and
	/// copies that density into the system at each state's entry (<c>0x007858c8</c>; <c>docs/exe/lobby.md</c>'s row for
	/// it). It shows only after a graphics quality changed mid-run, whose RESTART GAME is left out
	/// (<see cref="OptionsScreen"/>): here the next level takes the new quality's density.
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
	internal static string DetailFile => GameOptions.Current.GraphicsQuality switch
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

	/// <summary>
	/// A detail file's <c>GraphicalOptions.TEXTURE32</c>: 0 in all three shipped, the original's texture format 2,
	/// A4R4G4B4; 1 is its format 3, A8R8G8B8 (<c>0x0044e1f4</c>, <c>2 + TEXTURE32</c>; <c>textures.md</c>, "The texture
	/// cache's surface format"). <c>DetailFile_Apply</c> copies only 0 and 1 into the flag word, which starts clear, so
	/// any other value, or a file that will not read, is 0.
	/// </summary>
	internal static bool Texture32( string detailFile )
	{
		try
		{
			if ( int.TryParse( new SettingsFile( $"/{detailFile}" )["GraphicalOptions.TEXTURE32"], out var value ) )
				return value == 1;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Textures: {detailFile} would not load, so every texture is cut to four bits a channel - {e.Message}" );
		}

		return false;
	}

	/// <summary>The park's light: the theme's ambient and the sun's direction, lit unless <paramref name="detailFile"/> says <c>LIGHTING</c> 0.</summary>
	internal static ParkLight ParkLightFor( Vector3 ambient, Vector3 travels, string detailFile ) => new( ambient, travels, Lighting( detailFile ) );

	/// <summary>
	/// A detail file's <c>GraphicalOptions.LIGHTING</c>: 1 in med.sam and high.sam, 0 in low.sam, where the original sets
	/// render-state <c>0x1800</c> (<c>DetailFile_Apply</c> <c>0x0044d8f0</c>), whose <c>0x0800</c> keeps land from half
	/// light and gives every vertex the full colour (<see cref="ParkLight.Lit"/>). Any other value leaves the word, so
	/// lit. A file that will not read gives medium's 1.
	/// </summary>
	internal static bool Lighting( string detailFile )
	{
		try
		{
			if ( int.TryParse( new SettingsFile( $"/{detailFile}" )["GraphicalOptions.LIGHTING"], out var value ) )
				return value != 0;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Ground: {detailFile} would not load, so the park is lit as at medium - {e.Message}" );
		}

		return true;
	}

	/// <summary>
	/// The detail file's <c>GraphicalOptions.TEXTUREQUALITY</c>: 1 in low.sam, 2 in med.sam, 3 in high.sam. A file that will not read
	/// gives medium's 2.
	/// </summary>
	internal static int TextureQuality()
	{
		try
		{
			if ( int.TryParse( new SettingsFile( $"/{DetailFile}" )["GraphicalOptions.TEXTUREQUALITY"], out var value ) )
				return value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Sea: {DetailFile} would not load, so its textures are medium's - {e.Message}" );
		}

		return 2;
	}

	/// <summary>
	/// The procedural ground's tile size for the detail file's <c>GraphicalOptions.PROCEDURALTEXTURING</c> (see
	/// <see cref="TileSizeFor"/>). A file that will not read is taken as medium's 2.
	/// </summary>
	internal static int ProceduralTileSize()
	{
		var ask = 2;

		try
		{
			if ( int.TryParse( new SettingsFile( $"/{DetailFile}" )["GraphicalOptions.PROCEDURALTEXTURING"], out var value ) )
				ask = value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Ground: {DetailFile} would not load, so the ground is composited as at medium - {e.Message}" );
		}

		var original = GameOptions.Current.Faithful;
		var size = TileSizeFor( ask, original );
		Log.Info( size == 0
			? $"Ground: {DetailFile}'s PROCEDURALTEXTURING 0, so every ground cell draws its own texture"
			: original
				? $"Ground: opentpw.cfg's \"graphics faithful\", so {size}-pixel tiles, as the original draws on a machine with over 4 GB"
				: $"Ground: {DetailFile}'s PROCEDURALTEXTURING {ask}, so {size}-pixel tiles, as asked, OpenTPW's default; opentpw.cfg's \"graphics faithful\" takes the original's 16" );
		return size;
	}

	/// <summary>The park's ground, its tiles the size <see cref="ProceduralTileSize"/> gives; the one place a park's ground is made.</summary>
	internal static ParkGround MakeGround( string themeName, IParkInitialState? park, ParkBalance? balance, bool lighting )
		=> new( themeName, park, balance, lighting, ProceduralTileSize() );

	/// <summary>
	/// The tile size for a <c>PROCEDURALTEXTURING</c> of <paramref name="ask"/>: 0 for 0 (the compositor off, low.sam),
	/// 16 for 1, 64 for 3 and 32 for any other, as <c>FUN_00457a90</c> asks (<c>0x00457ac5</c>; the shipped med.sam and
	/// high.sam say 2). The original then cuts the ask to 16 where a signed test reads <c>dwTotalPhys</c> under 48 MB, as it
	/// reads any machine over 4 GB (<c>FUN_00572110</c>; docs/exe/park-engine.md, "The tile size"), so on today's machines
	/// it draws 16 whatever is asked. OpenTPW draws the ask, what the original drew on the machines of its day: a
	/// deviation from the original as it runs now. <paramref name="original"/> (<see cref="GameOptions.Faithful"/>, opentpw.cfg's <c>graphics faithful</c>)
	/// takes the cut. The original's other cuts (its texture memory; 64 to 32 under 64 MB, <c>0x00572170</c>) are not modelled.
	/// </summary>
	internal static int TileSizeFor( int ask, bool original ) => ask switch
	{
		0 => 0,
		_ when original => 16,
		1 => 16,
		3 => 64,
		_ => 32
	};

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

		Entity.All.ForEach( entity => entity.Update() );

		// The End feature asked for in the walk above, made outside it: its model adds entities, which the walk cannot take.
		if ( _endFeatureOwed is { } owed )
		{
			_endFeatureOwed = null;
			owed();
		}

		// The bumper rides' boats stood where this frame's track ticks left their cars, outside the walk above, which
		// cannot take the entities a new boat adds.
		ParkBumperBoats.Current?.Sync();

		// And the model of an arrival vehicle made at a summons in the walk, for the same reason.
		ParkFixedItems.Current?.Sync();

		// The date of the park's clock once this frame's sweeps have counted it on, compared once a frame after the last of
		// them, and the day's, month's and year's work run on its edges, then the golden tickets owed by this frame's sweeps.
		// Only a park keeps a date.
		if ( Kind == Scene.Park && ParkState is { } clocked )
			TurnTheCalendar( clocked, ParkPeople.Current, Catalogue, tick =>
			{
				if ( ParkPeople.Current is { } people )
					ParkGoldenTickets.Sweep( tick, clocked, people );
			} );

		// Whatever was deleted during that walk leaves the list now the walk is over - see Entity.Delete.
		Entity.ApplyDeletions();

		ParticleSystem.Current?.Update();

		// What the pointer is over, before the interface deals out this frame's clicks - so a click
		// and the cell it landed on are the same frame's answer. Only a park has ground to point at.
		// A gap: the original skips the picker in first person and a ride's view ([0x790ab0] & 0x16, 0x0054e2c0), so its
		// cell stands where it was and the crowd voice counts round it; ours keeps following the pointer, counted in both
		// (docs/QUEUE.md Q505).
		if ( Kind == Scene.Park )
		{
			if ( InsideView )
				Unimplemented.Report( "PICK_IN_FIRST_PERSON" );

			ParkPicking.Update();
		}

		// The help row the hover posts for the cell under the pointer, if it posts one - see WorldHelpRow.
		UI.WindowStack.WorldHelpPost = Kind == Scene.Park ? HoverPost( WorldHelpRow() ) : null;

		// The HUD is not an entity - see RootPanel - so it is driven from here, after the world.
		Hud.Update();

		// And a click on the WORLD, after the interface has had this frame's - so a press that landed
		// on a button never also opens whatever is drawn behind it. The original reaches the same place
		// from the park's own window proc (FUN_004879d0), which acts only while the current interaction
		// mode is idle and hands the click to the mode otherwise.
		if ( Kind == Scene.Park )
		{
			WorldClick();
			GameTableKeys();
			ShowToolCursor();
		}

		DebugConsole.StallFrame();
	}

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
		var wanted = WantedCursor();

		if ( cursor.CursorType != wanted )
			cursor.CursorType = wanted;
	}

	/// <summary>
	/// The cursor the park's pointer wears this frame: a layer's own over the hidden view, first person's crosshair
	/// (<see cref="UI.WindowStack.LayerCursor"/>), and the view's otherwise. Leaving first person by a key, the original's
	/// layer wears <c>c_busy.ani</c> until the swap 29 ms on (<c>docs/exe/hud.md</c>, "The help bar in first person");
	/// ours swaps in the same frame, so it is never seen.
	/// </summary>
	internal Input.CursorTypes WantedCursor()
		=> _windows?.LayerCursor is { } layer ? layer
			: FullScreenView ? Input.CursorTypes.Normal : ParkBuildMode.Current switch
			{
				ParkBuildMode.Queue => QueueCursor(),
				ParkBuildMode.Path => PathCursor(),
				ParkBuildMode.Patrol => ParkPicking.TryCell( out var x, out var y ) ? PatrolCursor( x, y ) : PatrolCursor(),
				ParkBuildMode.KartTrack or ParkBuildMode.WaterTrack => ParkPicking.TryCell( out var tx, out var ty )
					? ParkTrackTool.CursorOver( tx, ty ) : Input.CursorTypes.Track,
				_ when ParkBuilding.Carrying != 0 => CarryCursor(),
				_ => IdleCursor()
			};

	/// <summary>
	/// The pointer while a thing is carried (mode 4, <c>FUN_0052f950</c>): <c>c_place</c> (cursor 7), <c>c_noplace</c> where
	/// its squares are red (cursor 8, the pass's refusal <c>DAT_00816d48</c>), and <c>c_cash</c> where that is because its
	/// price is above the balance (cursor <c>0x14</c>, <c>DAT_00816d5c</c>), which only a move reaches here: the buy list
	/// lets nothing dearer into the hand.
	/// </summary>
	private static Input.CursorTypes CarryCursor()
	{
		if ( !ParkPicking.TryCell( out var x, out var y ) )
			return Input.CursorTypes.Place;

		if ( !ParkBuildMarkers.CarriedRefused( ParkBuilding.CarrySquares( x, y ) ) )
			return Input.CursorTypes.Place;

		return ParkBuilding.CarriedUnaffordable() ? Input.CursorTypes.Cash : Input.CursorTypes.NoGo;
	}

	/// <summary>
	/// The pointer with nothing armed: off the park's view the control's own, the arrow over the gadget's buttons; on it
	/// the hover's own (<see cref="HoverPointer"/>): the pick hand over a person the hover would take, an object, the gate
	/// or a ticket booth, <c>c_queue</c> over a queue; <c>c_path</c> where a click would arm the path tool, and the arrow otherwise.
	/// </summary>
	internal Input.CursorTypes IdleCursor()
		=> !_onTheView ? Input.CursorTypes.Normal
			: HoverTaken && HoverPointer is { } pointer ? pointer
			: IdleOverPath() ? Input.CursorTypes.Path : Input.CursorTypes.Normal;

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
	/// The patrol tool sets no cursor of its own (<c>FUN_0052f950</c> has no case for mode <c>0x36</c>), only
	/// <c>c_noplace</c> on a frame its outline is red (cursor 8, the refusal <c>DAT_00816d48</c>), which then stays until
	/// the tool is put away (<see cref="ParkPatrolTool.PointerRefused"/>); before that the ordinary pointer.
	/// </summary>
	internal static Input.CursorTypes PatrolCursor( int x, int y )
	{
		if ( ParkPatrolTool.Strip( x, y ).Any( square => square.Marker == ParkPathBuilding.MarkerRed ) )
			ParkPatrolTool.PointerRefused = true;

		return PatrolCursor();
	}

	/// <summary>The patrol tool's pointer off the map's cells: whatever its last refusal left.</summary>
	internal static Input.CursorTypes PatrolCursor()
		=> ParkPatrolTool.PointerRefused ? Input.CursorTypes.NoGo : Input.CursorTypes.Normal;

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
		=> HoverTaken && ParkPicking.TryCell( out var x, out var y ) && ArmsThePathTool( x, y, ParkPicking.PersonUnderCursor );

	/// <summary>Whether the hover is taken at all: nothing armed, an empty hand and no park screen open.</summary>
	private static bool HoverTaken
		=> ParkBuildMode.Current == ParkBuildMode.None && ParkHand.Empty && !UI.WindowStack.ParkScreenOpen;

	private int WorldHelpRow()
	{
		var onTheView = _windows?.PointerOnTheView ?? true;

		return ParkPicking.TryCell( out var x, out var y )
			? WorldHelpRow( x, y, ParkPicking.PersonUnderCursor, onTheView )
			: WorldHelpRow( -1, -1, ParkPicking.PersonUnderCursor, onTheView );
	}

	/// <summary>
	/// Whether the pointer was on the park's own view at the last hover, <c>[0x007c24fc]</c>: set as it comes onto the
	/// view from a control (message <c>0x10001</c>) and cleared as it leaves for one (<c>0x10002</c>).
	/// </summary>
	private bool _onTheView;

	/// <summary>The values of <see cref="WorldHelpRow(int, int, int, bool)"/>'s row's parameters, as the last hover filled them.</summary>
	internal (int Parameter, string Value)[] WorldHelpValues { get; private set; } = [];

	/// <summary>Whether the last hover posted its row to the help bar (<c>FUN_004924b0</c>), rather than leaving it.</summary>
	internal bool HoverPosted { get; private set; }

	/// <summary>
	/// What the frame hands the help bar for the hover's <paramref name="row"/>: the row with its values where the hover
	/// posted, nothing where it left the bar alone. <see cref="Update"/> and the tests both take it from here.
	/// </summary>
	internal (int Row, (int Parameter, string Value)[] Values)? HoverPost( int row )
		=> HoverPosted ? (row, WorldHelpValues) : null;

	/// <summary>The cell and the person the last hover on the view was asked about, which the hover after a return reads.</summary>
	private (int X, int Y, int Person)? _lastHover;

	/// <summary>
	/// The pointer the last hover put up where it is not the path's or the arrow (<c>FUN_004a2aa0</c>): pointer 2,
	/// <c>c_pick</c>, over a person it would take, an object, the gate or a ticket booth; pointer 4, <c>c_queue</c>, over a queue.
	/// </summary>
	internal Input.CursorTypes? HoverPointer { get; private set; }

	/// <summary>Whether the last hover put up the pick pointer, <c>c_pick</c> (<see cref="HoverPointer"/>).</summary>
	internal bool PickPointer => HoverPointer == Input.CursorTypes.Pick;

	/// <summary>
	/// The object the hover's object arm took last, the thing <c>[0x007c24f8]</c> as that arm compares it (<c>0x0048769d</c>):
	/// its row is posted only for an object other than this one, whatever the category. Every arm that forgets the thing
	/// or takes a person forgets it; those that post nothing and leave the thing (no cell, a hoarding, an object's cell
	/// with no object, an add-on, <c>0x004878d3</c>), nobody taken where the guests decide (<c>0x004870f3</c>) and a locator open
	/// with nobody taken (<c>0x0048721c</c>) keep it.
	/// </summary>
	private int _objectTaken;

	/// <summary>The person whose row the hover posted last, so their row and its log line are posted once.</summary>
	private int _rowPerson;

	/// <summary>
	/// The row the hover posted last, which stands while a locator is open and nobody is taken; -1 after the pointer has
	/// been on a control, whose leaving empties the bar.
	/// </summary>
	private int _postedRow = -1;

	/// <summary>
	/// The last hover's category <c>[0x007c24f4]</c>: each hover zeroes it and its arm sets its own (<c>0x00486de5</c>). A
	/// row is posted only when the category or the person changes, and where the guests decide and nobody is taken the
	/// empty row is posted only after a category. Type <c>0x15</c>'s 6 and a track's 5 post the empty row here
	/// (<see cref="GroundArm.UnbuiltRow"/>).
	/// </summary>
	private int _category;

	/// <summary>
	/// The help row for a cell under the pointer, idle, and the person's pointer (<c>FUN_00486d90</c>). A guest it would
	/// pick shows UIHELPTEXT 553 "Visitor #{P3} : Left-click to view this person's details", their number <c>+0x1d8</c>,
	/// or 554 "New Visitor" at nought; a member 555 "{P2} : Left-click to view this staff member's details", their name
	/// (<c>FUN_00506860</c>); either puts up pointer 2, <c>c_pick</c> (<c>FUN_004a2aa0( 2 )</c>). Where the guests decide
	/// and nobody is taken the row is emptied only if the last hover's category was not nought, and otherwise the old row
	/// stands (<c>0x004870f3</c> to <c>0x004870ff</c>); with a locator open and nobody taken nothing is posted, so the row last
	/// posted stands (<c>0x0048721c</c> to <c>0x004878d3</c>). Otherwise UIHELPTEXT 441 "Left-click to build path" over
	/// bare ground and 442 "Left-click to extend this path" over path, where the click would arm the path tool
	/// (<see cref="ArmsThePathTool"/>); 459 "Park gates - left-click to open/close the park or change the ticket price (G)"
	/// over the gate and 460, its "Ticket booth" twin, over a booth, with the pick hand (<see cref="GateArmAt"/>); 444
	/// "Left-click to edit this queue" over a queue with <c>c_queue</c>, and 550 "{P2}: Left-click to view/edit this item's
	/// details. Hold CTRL and click to buy another" over an object, its name, with the pick hand; the
	/// ground's other arms post nothing, or the empty row only after a category
	/// (<see cref="GroundArmAt"/>), and so does a tool armed or a hand full (<c>0x00486fd9</c>). Nothing is found in the
	/// original to show 443, "BACKSPACE to undo", so nothing here does. The hover runs only while the pointer is on the
	/// park's view (<c>0x00488575</c>).
	/// </summary>
	/// <param name="onTheView">Whether the pointer is on the park's own view rather than a control
	/// (<see cref="UI.WindowStack.PointerOnTheView"/>).</param>
	internal int WorldHelpRow( int x, int y, int person, bool onTheView = true )
	{
		HoverPosted = false;

		// Off the view the park runs no hover (0x00488575): nothing is posted, so the row and the pointer stand.
		if ( !onTheView )
		{
			_onTheView = false;
			return _postedRow;
		}

		// Coming onto the view from a control, whose leaving emptied the bar, the hover runs with the thing forgotten and
		// the category is nought after it (0x0048884e to 0x00488870); the timer's hover after that reads the cell picked
		// before the pointer moved. So both run at the last place on the view, the second posting its row if its arm posts
		// one, a member's again, and the next hover is at the pointer's own place (docs/exe/park-engine.md, "The hover
		// after a return to the view").
		if ( !_onTheView )
		{
			_onTheView = true;
			_rowPerson = 0;
			_objectTaken = 0;
			_postedRow = -1;
			WorldHelpValues = [];

			var (lastX, lastY, lastPerson) = _lastHover ?? (x, y, person);
			WorldHelpRow( lastX, lastY, lastPerson, onTheView );
			var posted = HoverPosted;

			_rowPerson = 0;
			_category = 0;
			var row = WorldHelpRow( lastX, lastY, lastPerson, onTheView );
			HoverPosted |= posted;

			return row;
		}

		_lastHover = (x, y, person);

		var standingValues = WorldHelpValues;
		WorldHelpValues = [];
		HoverPointer = null;

		// Under a park screen the original runs no hover and posts nothing (0x00488569), so the row last posted stands.
		if ( UI.WindowStack.ParkScreenOpen || Park is not { } park || ParkPeople.Current is not { } people )
		{
			WorldHelpValues = standingValues;
			return _postedRow;
		}

		// With a tool armed or a hand full the hover runs, forgets the thing and leaves the category nought, and posts the
		// empty row only if the last category was not nought (0x00486fd9): otherwise the old row stands.
		var last = _category;
		var objectTaken = _objectTaken;
		_objectTaken = 0;

		if ( !HoverTaken )
			return Silent( standingValues, last, emptyAfterACategory: true );

		switch ( PersonUnder( person ) )
		{
			case PersonPick.Guest:
				var guest = people.Guests[person];
				HoverPointer = Input.CursorTypes.Pick;

				if ( guest.VisitorNumber == 0 )
					return Posted( 554, person, 7, last );

				WorldHelpValues = [(3, $"{guest.VisitorNumber}")];
				return Posted( 553, person, 7, last );

			case PersonPick.Member:
				var member = people.Staff.First( staff => staff.ThingId == person );
				HoverPointer = Input.CursorTypes.Pick;
				WorldHelpValues = [(2, member.Name)];

				// As the row is posted the member is set idle wherever PICK UP would take them (FUN_00505f50: FUN_00505bd0,
				// then SetState( 0 )): taken by their picture, a member the camera follows stays taken and is set once.
				if ( _rowPerson != person && UI.ParkStaffWindow.CanBePickedUp( member, ParkState ) )
				{
					var was = member.Activity;
					member.SetActivity( StaffActivity.Idle, ParkState!.GameTick );
					Log.Info( $"Park: hover set thing {person} idle from {was} on mGameTick {ParkState.GameTick}" );
				}

				return Posted( 555, person, 9, last );

			// Off a person the category falls to nought, so the same person's row is posted again when the pointer returns.
			// Where the guests decide, the empty row is posted only after a category (0x004870f3); with a locator open
			// otherwise, never (0x0048721c).
			case PersonPick.Nothing:
				_objectTaken = objectTaken;
				return Silent( standingValues, last, emptyAfterACategory: GuestsDecide( LocatorOpen, Input.Held ) );
		}

		// Only the hover counts a row it has not built; the click and the pointer ask the same arm without counting.
		switch ( GroundArmAt( x, y, out var unbuilt, out var placed ) )
		{
			case GroundArm.Ground:
				return Posted( 441, 0, 2, last );

			case GroundArm.Path:
				return Posted( 442, 0, 1, last );

			// The gate's category 14 and a ticket booth's 13 put up the pick hand, pointer 2 (0x00487868, 0x004877d3).
			case GroundArm.Gate:
				HoverPointer = Input.CursorTypes.Pick;
				return Posted( 459, 0, 14, last );

			case GroundArm.Booth:
				HoverPointer = Input.CursorTypes.Pick;
				return Posted( 460, 0, 13, last );

			// A queue's category 3 posts 444 on a change of category and puts up pointer 4, c_queue (0x00487603).
			case GroundArm.Queue:
				HoverPointer = Input.CursorTypes.Queue;
				var queueRow = Posted( 444, 0, 3, last );

				if ( HoverPosted )
					Log.Info( $"Park: hover row 444 \"{Localization.FormatHelp( 444, WorldHelpValues )}\" over the queue at ({x},{y}), pointer c_queue" );

				return queueRow;

			// An object's category 4 posts 550 with its name only for an object other than the thing the hover holds, and
			// puts up pointer 2, c_pick (0x00487693 to 0x00487718).
			case GroundArm.Object:
				HoverPointer = Input.CursorTypes.Pick;
				_objectTaken = placed!.Value.ThingId;
				return ObjectPosted( placed.Value, objectTaken, standingValues );

			case GroundArm.Silent:
				_objectTaken = objectTaken;
				return Silent( standingValues, last, emptyAfterACategory: false );

			case GroundArm.NoCategory:
				return Silent( standingValues, last, emptyAfterACategory: true );

			// Type 0x15's category 6 and a track ride's 5: their rows are not built, and counted.
			case GroundArm.UnbuiltRow:
			default:
				if ( unbuilt is not null )
					Unimplemented.Report( unbuilt );

				return Posted( -1, 0, unbuilt == "HOVER_TYPE_15_ROW" ? 6 : 5, last );
		}
	}

	/// <summary>
	/// The object arm's row (<c>0x00487693</c>): category 4, and UIHELPTEXT 550 posted with the object's name as
	/// <c>FUN_004e2780</c> joins it (<see cref="UI.ParkVisitorWindow.NameOfThing"/>) only where the object is not the thing the
	/// hover held, <paramref name="objectTaken"/>; otherwise nothing is posted and the row and its name stand.
	/// </summary>
	private int ObjectPosted( ParkWorld.CatalogueObject placed, int objectTaken, (int Parameter, string Value)[] standingValues )
	{
		_category = 4;
		_rowPerson = 0;

		if ( placed.ThingId == objectTaken )
		{
			WorldHelpValues = standingValues;
			return _postedRow;
		}

		WorldHelpValues = [(2, UI.ParkVisitorWindow.NameOfThing( placed.ThingId ))];
		Log.Info( $"Park: hover row 550 \"{Localization.FormatHelp( 550, WorldHelpValues )}\" for object thing {placed.ThingId}, pointer c_pick" );
		HoverPosted = true;
		return _postedRow = 550;
	}

	/// <summary>
	/// An arm of the hover that leaves the category nought: where <paramref name="emptyAfterACategory"/> the empty row is
	/// posted only if the last category was not nought, and otherwise nothing is posted, so the old row and its values
	/// stand. The person is forgotten on every such arm here; the original forgets the thing only at some (<c>0x00487441</c>,
	/// <c>0x00486ff7</c>, <c>0x004877b9</c>), but each leaves the category nought, so the next person is posted anew either way.
	/// The object arm, which compares the thing alone, keeps its own (<see cref="_objectTaken"/>).
	/// </summary>
	private int Silent( (int Parameter, string Value)[] standingValues, int last, bool emptyAfterACategory )
	{
		_category = 0;

		if ( emptyAfterACategory && last != 0 )
			return Posted( -1, 0, 0, last );

		WorldHelpValues = standingValues;
		_rowPerson = 0;
		return _postedRow;
	}

	/// <summary>
	/// The row of an arm with category <paramref name="category"/>, posted only when the category differs from the last
	/// hover's, <paramref name="last"/>, or the person from the last taken (<c>FUN_00486d90</c>, the ground's arm at
	/// <c>0x00487522</c>), and logged as it changes to or from a person's. Otherwise nothing is posted and the row last
	/// posted stands, even where a control has emptied the bar since.
	/// </summary>
	private int Posted( int row, int person, int category, int last )
	{
		_category = category;

		if ( category == last && person == _rowPerson )
			return _postedRow;

		if ( person != _rowPerson || (person != 0 && row != _postedRow) )
		{
			if ( person != 0 )
				Log.Info( $"Park: hover row {row} \"{Localization.FormatHelp( row, WorldHelpValues )}\" for thing {person}, pointer c_pick" );
			else if ( _rowPerson != 0 )
				Log.Info( $"Park: hover row {row} after thing {_rowPerson}" );

			_rowPerson = person;
		}

		HoverPosted = true;
		return _postedRow = row;
	}

	/// <summary>
	/// Whether a click on this cell, idle and empty-handed, picks up the path tool: bare ground or path
	/// (<c>FUN_00486d90</c>'s categories 2 and 1, <see cref="GroundArmAt"/>), where the person under the pointer leaves the
	/// click to the ground (<see cref="PickPerson"/>): no locator open, no Shift alone, no member of staff. A guest on the
	/// cell, or the ride a path serves, does not stop it.
	/// </summary>
	private bool ArmsThePathTool( int x, int y, int person )
		=> GroundArmAt( x, y ) is GroundArm.Ground or GroundArm.Path && PersonUnder( person ) == PersonPick.Further;

	/// <summary>Which arm of the hover a cell falls to where no person is taken - see <see cref="GroundArmAt"/>.</summary>
	internal enum GroundArm
	{
		/// <summary>Category 2, bare ground: UIHELPTEXT 441.</summary>
		Ground,

		/// <summary>Category 1, path: UIHELPTEXT 442.</summary>
		Path,

		/// <summary>No category and nothing posted, whatever the last category: the old row stands.</summary>
		Silent,

		/// <summary>No category, and the empty row posted only if the last category was not nought.</summary>
		NoCategory,

		/// <summary>Category 14, the park gate: UIHELPTEXT 459, and a click opens the entry price.</summary>
		Gate,

		/// <summary>Category 13, a ticket booth: UIHELPTEXT 460, and a click opens the entry price.</summary>
		Booth,

		/// <summary>Category 3, a queue: UIHELPTEXT 444, and a click hands back the queue tool.</summary>
		Queue,

		/// <summary>Category 4, an object: UIHELPTEXT 550 with its name, and a click opens its window.</summary>
		Object,

		/// <summary>A category whose row is not built: type <c>0x15</c>, a track ride.</summary>
		UnbuiltRow
	}

	/// <summary>
	/// Which arm of the hover's ground a cell falls to (<c>FUN_00486d90</c> from <c>0x00487231</c>). No cell under the
	/// pointer posts nothing (<c>0x00487240</c>; the picker answers none only before its first meeting with the ground,
	/// <see cref="ParkPicking.Cell"/>), and neither does a hoarding, a type-12 track cell whose parent is type 25
	/// (<c>0x004873bd</c>), an object's cell with no object (<c>0x00487667</c>), nor an add-on's other than another
	/// ride's, <c>AddOn.UpgradeType</c> 2 (<c>0x0048768d</c>). Then the map type: 0 is bare ground,
	/// 1 path, 3 a queue, 4, 9 and 10 an object's, <c>0x15</c> its own, and every other the switch's default
	/// (<c>0x004877a3</c>), which empties the row only after a category: in Lost Kingdom map types 7, 2 and 30 (the
	/// approach). Before the cell's own categories, after the test for no cell, come the gate and the ticket booths
	/// (<see cref="GateArmAt"/>).
	/// </summary>
	internal GroundArm GroundArmAt( int x, int y )
		=> GroundArmAt( x, y, out _, out _ );

	/// <summary>
	/// <see cref="GroundArmAt(int, int)"/>, and for <see cref="GroundArm.UnbuiltRow"/> the name of the gap the hover counts
	/// (<paramref name="unbuilt"/>): the click and the pointer's tool test ask the arm too, and must not count a row they do
	/// not post. For <see cref="GroundArm.Object"/>, <paramref name="placed"/> is the object.
	/// </summary>
	private GroundArm GroundArmAt( int x, int y, out string? unbuilt, out ParkWorld.CatalogueObject? placed )
	{
		unbuilt = null;
		placed = null;

		if ( Park is not { } park || ParkState is not { } state )
			return GroundArm.Silent;

		if ( !ParkState.OnMap( x, y ) )
			return GroundArm.Silent;

		if ( GateArmAt( x, y ) is { } gate )
			return gate;

		var cell = ParkState.CellFor( park, x, y );

		if ( cell.TrackType == 12 && cell.TrackParentId != 0 )
		{
			var (parentX, parentY) = MapStep.CellAt( cell.TrackParentId );

			if ( ParkState.OnMap( parentX, parentY ) && ParkState.CellFor( park, parentX, parentY ).TrackType == 25 )
				return GroundArm.Silent;
		}

		// Types 11 and 16, and 12 under any other parent, ask the cell's thing and the track's (0x004873d5).
		if ( cell.TrackType is 11 or 12 or 16 )
		{
			unbuilt = "HOVER_TRACK_CELL_CATEGORY";
			return GroundArm.UnbuiltRow;
		}

		switch ( cell.Type )
		{
			case CellEdge.Nothing:
				return GroundArm.Ground;

			case CellEdge.Path:
				return GroundArm.Path;

			// A queue's row UIHELPTEXT 0x1bc (0x00487621).
			case 3:
				return GroundArm.Queue;

			// Type 0x15's row 0x1c0 (0x00487742).
			case 0x15:
				unbuilt = "HOVER_TYPE_15_ROW";
				return GroundArm.UnbuiltRow;

			// The object is the parent cell's (FUN_00527e80); its row is 0x226 with the object's name (0x004876d1). An add-on
			// fitting a ride (AddOn.UpgradesId, FUN_004e2710) posts nothing unless it is another ride's, AddOn.UpgradeType 2
			// (0x0048768d): every add-on Lost Kingdom ships is a track ride's, type 1.
			case CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd:
				if ( ParkItemHeights.Owner( state, x, y ) is not { } owner )
					return GroundArm.Silent;

				if ( Catalogue is { } catalogue && catalogue.TryGet( owner.CatalogueId, out var item )
					&& item.AddOnUpgradesId != 0 && item.AddOnUpgradeType != 2 )
					return GroundArm.Silent;

				placed = owner;
				return GroundArm.Object;

			default:
				return GroundArm.NoCategory;
		}
	}

	/// <summary>The theme's gate cells and ticket booths, read once per balance - see <see cref="GateArmAt"/>.</summary>
	private (ParkBalance Balance, (int X, int Y)[] Gate, (int X, int Y)[] Booths)? _gateCells;

	/// <summary>
	/// Whether a cell is the park gate's or a ticket booth's (<c>FUN_00486d90</c>, <c>0x004872a4</c> to <c>0x0048730d</c>):
	/// the gate is <c>EntranceA</c>, <c>EntranceB</c> and the cells either side of them on their row (<c>FUN_004d8690</c>'s
	/// cell id less one and its other's plus one), category 14; then the booths <c>TicketBoothA</c> and <c>B</c>
	/// (<c>FUN_004d8610</c>), category 13. Lost Kingdom's gate is (46,17) to (49,17), its booths (47,13) and (48,13).
	/// </summary>
	private GroundArm? GateArmAt( int x, int y )
	{
		if ( Balance is not { } balance )
			return null;

		if ( _gateCells is not { } cells || cells.Balance != balance )
		{
			var (a, b) = (ParkAdmission.Cell( balance, "EntranceA" ), ParkAdmission.Cell( balance, "EntranceB" ));
			_gateCells = cells = (balance, [a, b, (a.X - 1, a.Y), (b.X + 1, b.Y)],
				[ParkAdmission.Cell( balance, "TicketBoothA" ), ParkAdmission.Cell( balance, "TicketBoothB" )]);
		}

		return cells.Gate.Contains( (x, y) ) ? GroundArm.Gate
			: cells.Booths.Contains( (x, y) ) ? GroundArm.Booth
			: null;
	}

	/// <summary>What the person under the pointer makes of a click - see <see cref="PickPerson"/>.</summary>
	internal enum PersonPick
	{
		/// <summary>Nobody's: the ground's own categories decide.</summary>
		Further,

		/// <summary>Category 0: the click does nothing.</summary>
		Nothing,

		/// <summary>Category 7: the visitor's window, <c>FUN_004b79b0</c>.</summary>
		Guest,

		/// <summary>Categories 8 to 12: the staff member's window, <c>FUN_004b6080</c>.</summary>
		Member
	}

	/// <summary>
	/// The first of <c>FUN_00486d90</c>'s tests, which decide before the ground's: with the visitors' locator open and no
	/// modifier held, or Shift alone and no locator, a guest under the pointer is category 7 and anything else is
	/// nothing (<c>0x00487009</c> to <c>0x0048702d</c>); otherwise a member of staff is their kind's category, and with either locator open
	/// whatever else is under the pointer is nothing (<c>0x0048721c</c>), so a click there arms no tool and opens no window.
	/// </summary>
	/// <param name="locator">Which locator is open, <c>[0x007cc2f0]</c>.</param>
	/// <param name="held">The modifiers held, as <c>GetAsyncKeyState</c> reads Shift, Control and Alt.</param>
	/// <param name="guest">Whether a guest is under the pointer.</param>
	/// <param name="member">Whether a member of staff is.</param>
	internal static PersonPick PickPerson( UI.ParkLocator.Kind locator, Input.Modifiers held, bool guest, bool member )
	{
		if ( GuestsDecide( locator, held ) )
			return guest ? PersonPick.Guest : PersonPick.Nothing;

		if ( member )
			return PersonPick.Member;

		return locator == UI.ParkLocator.Kind.None ? PersonPick.Further : PersonPick.Nothing;
	}

	/// <summary>Whether the guests decide alone: the visitors' locator open and no modifier, or Shift alone and no locator.</summary>
	private static bool GuestsDecide( UI.ParkLocator.Kind locator, Input.Modifiers held )
		=> (locator == UI.ParkLocator.Kind.Visitors && held == Input.Modifiers.None)
			|| (held == Input.Modifiers.Shift && locator == UI.ParkLocator.Kind.None);

	/// <summary><see cref="PickPerson"/> for this thing under the pointer, the locator open now and the keys held now.</summary>
	private static PersonPick PersonUnder( int person )
	{
		var people = ParkPeople.Current;

		return PickPerson( LocatorOpen, Input.Held,
			guest: person != 0 && people?.Guests.ContainsKey( person ) == true,
			member: person != 0 && people?.IsStaff( person ) == true );
	}

	/// <summary>Which locator the park's gadget has open, or none.</summary>
	private static UI.ParkLocator.Kind LocatorOpen => Current?.Locator?.Open ?? UI.ParkLocator.Kind.None;

	/// <summary>
	/// The game table's keys built here, on release as the original fires them: the build keys, Backspace and
	/// Delete, and the quicksave and quickload, Alt+S and Alt+L. Not while a box has the keyboard, and not while a
	/// window holds the park - which is inferred for the pausing windows. Not while a park screen is open: opening
	/// one switches the game table off (<c>FUN_00485b70</c>, <c>0x00485ce6</c>) and its handler runs the shortcuts'
	/// alone (<c>FUN_00488ba0</c>). Not under the full-screen view, whose control runs the camera's table alone
	/// (<c>0x004a2918</c>). And not in first person, where the viewfinder's layer has the focus and its key-up runs the
	/// camera's table alone (<c>FUN_00488a00</c>, <c>0x00488ae3</c>; <c>docs/exe/saves.md</c>, "The quicksave and quickload keys").
	/// </summary>
	/// <remarks>
	/// The build keys' release edge also fires when a modifier changes while the key is held, which the
	/// original's key-up does not; the quick keys are the key-up itself (<see cref="Input.KeyUp"/>).
	/// </remarks>
	private void GameTableKeys()
	{
		if ( Input.TextCaptured || PausedByWindow() || FullScreenView || UI.WindowStack.ParkScreenOpen || ParkCamcorderCameraMode.Active )
			return;

		if ( Input.Released( InputButton.Delete ) )
			Log.Info( BackspaceKey() );

		if ( Input.Released( InputButton.Clear ) )
			Log.Info( ClearKey() );

		if ( Input.KeyUp( InputButton.QuickSave ) )
			Log.Info( QuickSaveKey() );

		if ( Input.KeyUp( InputButton.QuickLoad ) )
			Log.Info( QuickLoadKey() );

		foreach ( var release in Input.Releases )
		{
			if ( release.Held == Input.Modifiers.Alt && CheatRows.FirstOrDefault( row => row.Key == release.Key ).Name is { } cheat )
				Unimplemented.Report( cheat );
		}
	}

	/// <summary>
	/// The cheat table's twelve rows (<c>DAT_0078733c</c>; <c>docs/exe/park-engine.md</c>, "The cheat table"), each Alt
	/// alone on the key's release: research, ten thousand, the Local golden tickets 0 to 5 (<c>G</c>, the keypad's 1 to 5)
	/// and the Global 0 to 3 (the keypad's 6 to 9). None is built; each is counted. Whether the cheat flag lets them
	/// fire in the original, and whether the keypad's digits reach their rows, is not settled (Q720).
	/// </summary>
	private static readonly (NeoVeldrid.Key Key, string Name)[] CheatRows =
	[
		(NeoVeldrid.Key.R, "CHEAT_RESEARCH"), (NeoVeldrid.Key.Number1, "CHEAT_MONEY"), (NeoVeldrid.Key.G, "CHEAT_LOCAL_TICKET"),
		(NeoVeldrid.Key.Keypad1, "CHEAT_LOCAL_TICKET"), (NeoVeldrid.Key.Keypad2, "CHEAT_LOCAL_TICKET"),
		(NeoVeldrid.Key.Keypad3, "CHEAT_LOCAL_TICKET"), (NeoVeldrid.Key.Keypad4, "CHEAT_LOCAL_TICKET"),
		(NeoVeldrid.Key.Keypad5, "CHEAT_LOCAL_TICKET"), (NeoVeldrid.Key.Keypad6, "CHEAT_GLOBAL_TICKET"),
		(NeoVeldrid.Key.Keypad7, "CHEAT_GLOBAL_TICKET"), (NeoVeldrid.Key.Keypad8, "CHEAT_GLOBAL_TICKET"),
		(NeoVeldrid.Key.Keypad9, "CHEAT_GLOBAL_TICKET")
	];

	/// <summary>
	/// Alt+S, the game table's row 12, <c>GameKey_QuickSave</c> (<c>0x0040bf90</c>): the running park written as <c>&lt;theme&gt;.TPWS</c>, the
	/// theme's name being the one its folder is named by (the game object's string, <c>0x00786b68</c>), through the
	/// same save as the menu's Save Game (<see cref="WritePark(string, string)"/>, <c>FUN_005ac610</c>), over the last one and
	/// with no question. The original hands "QuickSave OK" or "Quicksave failed" to its empty logger, so nothing is
	/// shown either way (<c>docs/exe/saves.md</c>, "The quicksave and quickload keys").
	/// </summary>
	internal string QuickSaveKey()
		=> WritePark( ThemeName ) is { } written
			? $"Quicksave: QuickSave OK - written to {written}"
			: "Quicksave: Quicksave failed - the log says why";

	/// <summary>
	/// Alt+L, the game table's row 11, <c>GameKey_QuickLoad</c> (<c>0x0040c040</c>): <c>&lt;theme&gt;.TPWS</c> loaded over the running park, as a
	/// Load Park row's click loads a file (<c>FUN_005ac5d0</c>). With no such file nothing happens, the original's load
	/// failing at the open before it takes anything down.
	/// </summary>
	/// <remarks>
	/// <b>A deviation</b>, Load Park's: the park's scene is ended and built again from the file behind the loading
	/// screen, where the original reads the file over the running park, and keeps the hand (<c>docs/exe/park-engine.md</c>,
	/// "In-park loads keep the hand").
	/// </remarks>
	internal string QuickLoadKey()
	{
		if ( Players.Roster.Current is not { } player )
			return "Quickload: QuickSave Load Failed - nobody is playing, so there is no folder to load from";

		if ( SaveFolder.SavedParkPath( player.Slot, player.Name, ThemeName, ThemeName ) is not { } path )
			return $"Quickload: QuickSave Load Failed - {player.Name}'s {ThemeName} folder holds no {ThemeName}.TPWS";

		Game.RequestParkLoad( ThemeName, path );
		return $"Quickload: QuickSave Loaded - {path} is loaded";
	}

	/// <summary>
	/// Backspace - the game table's row 5, handler <c>0x0040bda0</c>. With the path tool armed it takes up
	/// the last run laid, with the track tool the last run of track, refunded; with nothing armed and an empty
	/// hand it takes one press of the clear off the path under the pointer. One body, shared with the console's <c>backspace</c>.
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

			// BuildTool_Backtrack in mode 0x35 clears the last run (Track_ClearCell, 0x0053b280) and refunds its pieces.
			case ParkBuildMode.KartTrack or ParkBuildMode.WaterTrack:
				return ParkTrackTool.Backtrack();

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
	/// (<c>0x0048823f</c>, <see cref="RightTick"/>). The release reads no clock, only the flag (<c>0x0048836f</c>).
	/// </summary>
	internal const long QuickClickLimit = 200;

	/// <summary>
	/// The park's frame tick for the right button (message <c>0x1e</c>, <c>0x0048821f</c>): a click still armed more
	/// than <see cref="QuickClickLimit"/> after its press is let go, as a held press. Run once a frame, after the
	/// frame's steps, as the original's UI pass hands out the queued buttons (<c>UI_DispatchQueue</c>) before its tick
	/// (<c>UI_TickTree</c>; <c>docs/exe/park-engine.md</c>, "The hand's ways out"), so a release that waited out a
	/// stopped frame is still a click.
	/// </summary>
	internal void RightTick()
	{
		if ( GameOptions.Current.RmbCancel && _rightClick.Armed && Time.WallMilliseconds - _rightClick.At > QuickClickLimit )
			_rightClick.Armed = false;
	}

	/// <summary>
	/// A press on the park itself. Clicking a placed thing opens its management window -
	/// <c>FUN_004879d0</c> reads the hover category and sends category 4, a placed object, to
	/// <c>FUN_00486920</c>.
	/// </summary>
	/// <remarks>
	/// The press is one the window system sent this frame (<see cref="Input.MouseInfo.LeftWentDown"/>), so one let go
	/// inside the frame still lands, as the original's press from the cursor library's callback (<c>0x00486c70</c>) does, and a button already held when the
	/// park began is none. A frame's second press is counted, not acted on (<c>PARK_PRESSES_IN_ONE_FRAME</c>): the
	/// first may open a window that the second would land on, and the interface has dealt out the frame's presses by now.
	/// </remarks>
	private void WorldClick()
	{
		var pressed = Input.Mouse.LeftWentDown;

		ParkCamcorderCameraMode.ReadRightButton();

		// Each step the right button took this frame, so a quick click let go inside one frame is still a press and a
		// release, as the original's park is posted both (0x00486c70).
		//
		// Latent: the stack runs before this (Hud.Update). A first-person right click let go inside one frame is answered by
		// the stack's release, which leaves first person, so its press step is read here with first person already left and
		// arms, and its release runs the quick right click; the original's park is posted the press first, in first person,
		// and takes none of it. Harmless while entering first person lets go of the hand and LetGo answers null.
		var putAway = false;

		foreach ( var rightDown in UI.WindowStack.RightSteps( _worldRightWasDown, Input.Mouse ) )
		{
			if ( RightButton( rightDown, Input.Mouse.Position / UI.VirtualScreen.Scale, RightPressTaken( UI.WindowStack.RightPointerTaken ) ) is { } letGo )
			{
				Log.Info( letGo );
				putAway = true;
			}
		}

		RightTick();

		// Ours: the original's one queue keeps the two buttons' order (0x00486c70); here a frame whose quick right click
		// put something away hands the park none of its left presses (docs/exe/hud.md, "A click and a double click").
		if ( putAway )
			return;

		if ( !pressed || LeftPressTaken( UI.WindowStack.PointerTaken ) || KeptFromThePark( UI.WindowStack.ParkScreenOpen ) )
			return;

		if ( Input.Mouse.LeftPresses > 1 )
			Unimplemented.Report( "PARK_PRESSES_IN_ONE_FRAME" );

		if ( ParkPicking.TryCell( out var cellX, out var cellY ) )
			Log.Info( ClickWorldAt( cellX, cellY, ParkPicking.PersonUnderCursor, ParkPicking.ObjectUnderCursor ) );
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
	/// skips the press while a screen is open (<c>0x00488741</c>; <c>docs/exe/park-engine.md</c>, "A park screen is
	/// open"): no hover is taken, the idle click is not run, so no window opens and no path tool is picked up, and
	/// the mode's button-down slot is not called. The release has no such test and reaches the mode's button-up
	/// slot, which is where a carry shell commits, so a click beside a screen still puts down what the hand holds
	/// and still lays an armed tool's run. Here every click acts on its press, so that is this press.
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
	/// go once a frame's tick finds the button held more than 200 ms (<see cref="RightTick"/>) or the pointer has moved
	/// more than 8 units across or down from where it went down; a release while it is still armed is the click (<c>0x0048842b</c>;
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

		// The move test (0x00488265): the frame's pointer stands for the moves posted before this step.
		if ( _rightClick.Armed && (MathF.Abs( at.X - _rightClick.Where.X ) > 8f || MathF.Abs( at.Y - _rightClick.Where.Y ) > 8f) )
			_rightClick.Armed = false;

		if ( released )
			Log.Info( $"right release: armed={(_rightClick.Armed ? 1 : 0)} held={Time.WallMilliseconds - _rightClick.At} ms" );

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
	/// <param name="person">The person under the pointer (<see cref="ParkPicking.PersonUnderCursor"/>), or nought.</param>
	/// <param name="placed">The cell's thing that is not a person (<see cref="ParkPicking.ObjectUnderCursor"/>), or nought:
	/// the original's click takes a person's window from <c>[0x007c2650]</c> and an object's from <c>[0x007c2654]</c>, which
	/// the frame's picker fills from the cell (<c>FUN_0046c110</c>) and the click snapshots, so a guest passing a ride leaves the ride's window to open (<c>FUN_004879d0</c>).</param>
	internal string ClickWorldAt( int cellX, int cellY, int person, int placed = 0 )
	{
		// ANYTHING IN THE HAND GOES DOWN FIRST, and only an empty hand opens a window. That is the
		// original's own order: a place mode consumes the click - FUN_004879d0 acts only while the
		// current interaction mode is idle, type 0 or 1 - and the modes that carry something are types
		// 3, 5 and 6. Nothing is charged until it actually goes up, which is why cancelling needs no
		// refund; see ParkBuilding.Carrying.
		if ( ParkBuilding.Carrying != 0 )
			return $"world click: {ParkBuilding.PlaceCarriedUnder( cellX, cellY, pressReachedTheTool: !UI.WindowStack.ParkScreenOpen )}";

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

		// THE PERSON UNDER THE POINTER IS ASKED FIRST, and an open locator or Shift alone keeps the click from everything after.
		switch ( PersonUnder( person ) )
		{
			case PersonPick.Guest:
				if ( _windows is { } guestStack )
					UI.ParkVisitorWindow.Open( guestStack, person );

				return $"world click: opened the visitor window for thing {person}";

			case PersonPick.Member:
				if ( _windows is { } memberStack )
					UI.ParkStaffWindow.Open( memberStack, person );

				return $"world click: opened the staff window for thing {person}";

			case PersonPick.Nothing:
				return $"world click: nothing to do at ({cellX},{cellY}), the person under the pointer decides it ({LocatorOpen} locator open)";
		}

		// THE GATE AND THE TICKET BOOTHS OPEN THE ENTRY PRICE, their categories 14 and 13 (FUN_00498d80, 0x00487c93), as G does.
		if ( GroundArmAt( cellX, cellY ) is (GroundArm.Gate or GroundArm.Booth) and var gate )
		{
			if ( _windows is { } priceStack )
				UI.ParkCategoryScreens.Show( priceStack, UI.ParkCategoryScreens.Finance, 10 );

			return $"world click: the {(gate == GroundArm.Gate ? "gate" : "ticket booth")} at ({cellX},{cellY}) opens the entry price";
		}

		// A KART OR WATER RIDE'S PIECE OF TRACK ARMS THE TRACK TOOL at its track's open end - the hover's category 5
		// (FUN_004879d0, 0x00487f6c), mode 0xe or 0x13 on the press and 0xb or 0x10 on the release.
		if ( ParkState is { } trackState && ParkTrackTool.AnchorUnder( trackState, cellX, cellY ) is not null )
			return $"world click: {ParkTrackTool.Arm( cellX, cellY )}";

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
		if ( ArmsThePathTool( cellX, cellY, person ) )
		{
			ParkBuildMode.Arm( ParkBuildMode.Path );

			return $"world click: {ParkPathBuilding.RunPath( cellX, cellY, !UI.WindowStack.ParkScreenOpen )}";
		}

		if ( placed != 0 )
		{
			// CTRL, WHATEVER SHIFT AND ALT ARE, PUTS ANOTHER OF IT IN THE HAND and opens no window: the original asks
			// VK_CONTROL alone (0x00487ac0). Too dear, it plays the buy screen's refusal, cat_ui 0x1D (0x00487b56), and the
			// hand stays empty (docs/exe/park-engine.md, "Ctrl+click buys another of a placed thing"). The lookups that fail
			// before the price test (no park, no thing, no item) are silent: the original returns at 0x00487ab8 there.
			if ( (Input.Held & Input.Modifiers.Control) != 0 )
			{
				var another = ParkBuilding.CarryAnother( placed, out _, out var tooDear );

				if ( !tooDear )
					return $"world click: {another}";

				return $"world click: {another} - cat_ui 0x{UI.ParkBuyScreen.RefusedSound:x} for the refusal, " +
					$"{(UiSounds.Interface( UI.ParkBuyScreen.RefusedSound ) ? "a voice" : "no voice")}";
			}

			OpenObjectWindow( placed );

			return $"world click: opened the window for thing {placed}";
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
			return ParkPathBuilding.RunPath( cellX, cellY, !UI.WindowStack.ParkScreenOpen );

		if ( ParkBuildMode.Current == ParkBuildMode.Patrol )
			return ParkPatrolTool.Run( cellX, cellY, !UI.WindowStack.ParkScreenOpen );

		if ( ParkBuildMode.IsTrack( ParkBuildMode.Current ) )
			return ParkTrackTool.Run( cellX, cellY, !UI.WindowStack.ParkScreenOpen );

		if ( ParkBuildMode.Anchored )
			return ParkPathBuilding.RunQueue( cellX, cellY, !UI.WindowStack.ParkScreenOpen );

		// An unanchored queue click plays the laying sound too, as mode 3 does before its anchor test (0x005271ea, then
		// 0x00527222). Dead by CONTENT in Lost Kingdom: only EDIT_QUEUE_WITH_NO_ENTRANCE and the console's `tool queue` arm
		// the queue tool with no anchor.
		ParkPathBuilding.LayingSound( "queue", cellX, cellY );
		ParkBuildMode.AnchorAt( cellX, cellY );

		return $"anchored a queue run at ({cellX},{cellY}) - click again to lay it";
	}

	/// <summary>
	/// Closes the park screen that is open, for what closes one without opening a window: the way into first person
	/// (<see cref="ParkCamcorderCameraMode.Enter"/>). See <see cref="UI.WindowStack.CloseParkScreen"/>.
	/// </summary>
	/// <returns>Whether one was open.</returns>
	internal bool CloseParkScreen() => _windows?.CloseParkScreen() == true;

	/// <summary>The windows open in this park that turn a model in a preview, the ride's and the shop's, oldest first - the
	/// debug console's way to the preview.</summary>
	internal IEnumerable<UI.UiWindow> OpenPreviewWindows()
		=> _windows?.Windows.Where( window => window is UI.ParkObjectWindow or UI.ParkShopWindow ) ?? [];

	/// <summary>
	/// Opens the management window for one thing - the original's <c>FUN_00486920</c>, which switches
	/// on the thing's kind byte at <c>+2</c> and then, for a placed object, on the item's
	/// <c>WhichUIType</c>. <b>Nine windows share one base</b>; of the placed objects' the ride's and the shop's
	/// (<see cref="UI.ParkShopWindow"/>) are built (the visitor's is
	/// <see cref="UI.ParkVisitorWindow"/>, opened from the locator and the visitors list, and the staff member's
	/// <see cref="UI.ParkStaffWindow"/>).
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
		if ( item.UiType == 1 )
		{
			UI.ParkShopWindow.Open( windows, thingId );
			return;
		}

		if ( item.UiType != 0 )
		{
			Unimplemented.Report( item.UiType switch
			{
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
	/// those screens call (0x004092a0) does nothing unless the pause-permission gate is exactly 1, which
	/// the state machine sets to 0 as the lobby comes up and to 1 as a park loads (docs/exe/weather.md,
	/// "Why the lobby does not pause"). See <see cref="GameClock"/> for why that field means both "a park
	/// is running" and "nobody already holds the pause".
	/// </para>
	/// <para>
	/// <b>Not all three screens test that gate before calling.</b> The
	/// lobby's game menu never reaches the test: GameMenu_Open branches on its scene argument, and non-zero -
	/// which is what the lobby passes - builds the lobby's menu and returns without ever touching the pause.
	/// The other two do test it: the message box at 0x0047f251, also requiring the lobby's front-end object
	/// to be gone (a pointer rather than a flag), and the options screen at 0x004a3a4f - which
	/// the lobby does reach, so there the gate is what prevents the pause, by declining to
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
	/// stop-all (0x0051bcb0) 90; Sound_StopFading's 60 is a percentage kept per 100 ms worker step (docs/exe/audio.md, "The
	/// fade, the SoundInfo settings and the reverb"), and whether the 90 is the same is not traced. A deviation: 0.09
	/// seconds stands in for it (docs/QUEUE.md Q509).
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
		ParkRideViewCameraMode.Leave();
		ParkEndFeature.Forget();

		// And whatever build mode was armed, for exactly the reason the two cameras above are forgotten:
		// it is static so that it survives a frame, which means it survives the SCENE as well unless
		// something says otherwise - and a park entered after one left mid-run would otherwise open with
		// a path tool armed and an anchor pointing at a cell in a different park.
		ParkBuildMode.Forget();
		ParkPicking.Forget();

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
		Audio.SetListener( Camera.Ears, Camera.Rotation.Forward );

		// The solid geometry, then everything see-through, then the sprites.
		Entity.RenderWorld();

		RenderOverTheWorld( Hud, () => global::Global.Render.CommandList.ClearDepthStencil( 1 ), DrawPreviews, DrawActiveWarning,
			() => Entity.All.ForEach( entity => entity.RenderOverlay() ) );
	}

	/// <summary>
	/// A ride's window shows the ride itself, turning, in a panel. It is drawn HERE rather than with
	/// the interface because it is a model: the HUD pass draws flat UI meshes, and this needs the
	/// depth-cleared overlay stage the advisor already uses. A scissor keeps it inside the panel.
	/// </summary>
	private void DrawPreviews()
	{
		if ( _windows is not { } showing )
			return;

		foreach ( var window in showing.Windows )
		{
			if ( window is UI.ParkObjectWindow ride )
				ride.DrawPreview();
			else if ( window is UI.ParkShopWindow shop )
				shop.DrawPreview();
		}
	}

	/// <summary>
	/// The active warning, drawn after every preview but beneath the advisor. Covered windows keep their HUD drawing
	/// order; the preview pass has its own existing overlay behaviour.
	/// </summary>
	private void DrawActiveWarning()
	{
		if ( _windows is not { } showing )
			return;

		var active = showing.Windows.LastOrDefault( window => !window.Hidden && !window.PutAway );

		if ( active is UI.ParkObjectWindow activeRide )
			activeRide.DrawStatus();
		else if ( active is UI.ParkShopWindow activeShop )
			activeShop.DrawStatus();
	}

	/// <summary>
	/// The frame's end, after the world: the HUD, the depth cleared, the windows' model previews, the active warning, the
	/// entities' overlays (the advisor), and the pointer last, as the original's cursor library draws it onto the finished
	/// frame just before the flip (<c>0x00564210</c>; <c>docs/exe/hud.md</c>, "The pointer is drawn last"). The steps are
	/// handed in so a test can see the order.
	/// </summary>
	internal static void RenderOverTheWorld( RootPanel hud, Action clearDepth, Action previews, Action warning, Action overlays )
	{
		// The HUD on the finished world. Nothing in the passes before can reach it, because it is not an entity at all -
		// see RootPanel.
		hud.Render();

		// Then whatever sits on the screen in front of everything but the pointer, the HUD included - the advisor,
		// who covers whatever he pops up over. Depth is cleared first so nothing drawn before can
		// cut into him: the original draws him in screen space, flattened almost to nothing in
		// depth.
		clearDepth();
		previews();
		warning();
		overlays();

		hud.RenderPointer();
	}
}
