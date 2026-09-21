using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }

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
	/// lobby, which reads only <see cref="Global"/>. Settable rather than init-only because it is
	/// built during scene setup rather than in the constructor's own body.
	/// </summary>
	public ParkBalance? Balance { get; private set; }

	/// <summary>
	/// The park's own save as it was read, or null in the lobby and in a theme that ships no park of its
	/// own. Kept so that the interface can show what the file says - the balance it was left with and what
	/// it charges - without reading a megabyte and a half a second time.
	///
	/// <para>
	/// <b>It describes a FILE and it is deliberately immutable</b>, which is why everything that moves has
	/// to be held somewhere else: see <see cref="PeepBehaviour.Takings"/> and
	/// <see cref="PeepBehaviour.VisitorsToDate"/>, each of which exists only because a park has nowhere yet
	/// to write its own state back to. Anything that reads this for a running number must add that number
	/// on rather than expect to find it here.
	/// </para>
	/// </summary>
	public ParkWorld? Park { get; private set; }

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

	/// <summary>Who the park may hire - see <see cref="ParkStaffPool"/>.</summary>
	public ParkStaffPool? StaffPool { get; private set; }

	/// <summary>
	/// Which of the game's two worlds this is. The original runs them as separate states of one
	/// machine - the lobby is states 1/2/3 and a park is 9/10/0xb - and they share almost nothing but
	/// the renderer, the sound device and the player's own files.
	///
	/// <para>
	/// This is the smallest form of that split: enough for a park to be built instead of the lobby,
	/// and no more. Teardown ordering, the state machine proper and the park's own front end are still
	/// the lobby's - see the park plan's later milestones.
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

	/// <summary>The theme folder this level was built from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; private init; }

	public Level( string levelName, Scene kind = Scene.Lobby )
	{
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

		// And the calendar starts over with it, as the original zeroes its world-tick counter at park
		// init (0x00515865). Unconditional rather than park-only so a lobby cannot be left showing the
		// date of the park before it; only a park ever advances it - see the Update below.
		GameCalendar.Rebase();

		if ( kind == Scene.Park )
		{
			SetupParkEntities();
			SetupParticles();
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
		// It replaces (0, 100, 100), which was a leftover of the old test scene and meant nothing
		// while the light was being subtracted from a view-space position - that made it a headlight
		// that lit whatever faced the camera, wherever it was standing. Water reads this too, so the
		// sea takes its shading from the same place.
		SunLight = new Sun() { Position = new( 500, -3500, 2800 ) };

		// The sky first, because it never writes depth: it has to be laid down before anything
		// that should cover it, and the ocean runs out further than the sky's own horizon does.
		_ = new Sky();

		_ = new Water() { Scale = new Vector3( 10000f ) };

		// Each island reads its own script and brings its gate and its FLYINGMESH swarm with it.
		_ = new LobbyIsland( new Vector3( 400, 400, 0 ), "Jungle" );

		// Positions come from lobby.wad's own lobby.txt, which lists one ISLANDCAMERAPOSITION per
		// island index: 0 (400,400), 1 (600,400), 2 (600,600), 3 (400,600). Each park's script
		// gives its index - jungle 0, fantasy 1, hallow 2, space 3 - and fantasy and hallow were
		// the wrong way round here, which put them diagonally opposite where the game has them.
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
	/// A park, as far as it goes today: its own sky, the ground's scenery and something to look at it
	/// with.
	///
	/// <para>
	/// Sound, weather and the advisor are all here now - this paragraph used to say that none of them
	/// were, and each was a job with its own evidence to gather. What it does have is the theme's own
	/// throughout - its balance numbers, its sun and its sky - over <c>base.MD2</c> out of the park's
	/// own terrain.wad, through the same model and texture path the lobby already uses.
	/// </para>
	/// <para>
	/// What it still has no part of is the simulation: no guests, no staff, and no ride that operates.
	/// That is what keeps the advisor to a single line - see <see cref="UI.ParkLines"/> - rather than
	/// the scored queue of them the original runs, which has nothing here to score.
	/// </para>
	/// </summary>
	private void SetupParkEntities()
	{
		// The theme's own numbers, global defaults underneath - see ParkBalance for why that stack
		// matters more than it looks.
		//
		// <b>Easy mode, because the only park file this loads is Easymode.TPWI</b> - see ReadPark below,
		// which names it outright and unconditionally. That is not a guess about what the player chose:
		// the park it reads carries nought APR on all eight of its loans, which matches
		// Easy_Standard.sam and matches the global file nowhere. If a park that is NOT the easy one is
		// ever loaded, this has to move with it rather than stay true.
		Balance = new ParkBalance( ThemeName, easyMode: true );
		Log.Info( $"{ThemeName}: balance stack came to {Balance.Count} keys" );

		// What distance fades to. The lobby's Sky rewrites this every frame from its own horizon,
		// because that horizon moves with whichever park the camera is on; a park's sky is untinted and
		// deliberately leaves this alone, so setting it once here holds. Jungle and fantasy are a pale
		// blue within a few points of the lobby's own constant, hallow is nearly black and space is
		// orange.
		FogColour = Balance.Colour( "ThemeEngine.FogColour", FogColour );

		// LightNormal is written in the original's axes, where Y is up, so it swaps into this engine's
		// Z-up space the same way a model's vertices do. It is read here as the direction the light
		// travels, which puts the sun above the park rather than below it - the reading that makes
		// physical sense, and a CHOICE rather than a finding: nothing traced it as far as the shading
		// itself, so travels-versus-toward is still unconfirmed. Placed far enough away that its
		// direction barely changes across a thousand-unit park, which is what makes a point light
		// stand in for a sun.
		var lightNormal = Balance.Vector( "LightNormal", new Vector3( 0.4f, -0.8f, 0.4f ) );
		var travels = new Vector3( lightNormal.X, lightNormal.Z, lightNormal.Y ).Normal;

		SunLight = new Sun()
		{
			Position = new Vector3( 480f, 425f, 0f ) - (travels * 4000f),

			// ThemeEngine.DirectionalLightLevel, 0xFFFFFFD8 for every park the game ships - a white
			// barely warmed at the blue end. AmbientLightLevel (0xFF555568) is deliberately not applied:
			// the shader's ambient is one float, not a colour, and world draws leave it at zero and take
			// the shader's own 0.4. Giving a park a coloured ambient means changing what every draw is
			// handed, which is a job of its own.
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

		// The park's own save, read once here and handed to everything that needs it: the ground to know
		// which cells it must leave alone, the paths to draw those cells, and the objects to stand where
		// it says. It inflates to a megabyte and a half, so reading it three times would be careless.
		var park = ReadPark( ThemeName );

		// Kept on the level as well as handed round below, so that the interface can show what the file
		// says without opening a megabyte and a half a second time - see the Park property, and note that
		// anything running has to be added to what it holds rather than looked for inside it.
		Park = park;

		// And the running copy of everything in it that moves, made once here so that the guests, the
		// interface and the staff all read and write the same numbers rather than each keeping their own.
		ParkState = new ParkState( park );

		// The ground first, then what stands on it. The paths follow the ground because they lie on its
		// heightfield, and ParkObjects is last because it asks how high the land is under each thing it
		// places.
		// Everything this theme sells, read once and shared: the objects need it to know what to stand on
		// the ground, and the rides need it to know where each item's script lives. Built only where there
		// is a park to place anything in, since without one neither of them has anything to ask it.
		// Kept on the level as well as handed round below, because buying something needs it long after
		// the park has finished loading - see ParkBuilding.
		Catalogue = park == null ? null : new ParkItemCatalogue( ThemeName );

		var catalogue = Catalogue;

		_ = new ParkGround( ThemeName, park );
		_ = new ParkPaths( ThemeName, park );
		_ = new ParkQueues( ThemeName, park );
		_ = new ParkTerrain( ThemeName );

		// The objects before the fixed items, which is the one ordering here that matters: the gate and the
		// traffic lights are swept out of the very registry the placed things are swept from, so it has to
		// exist before they can put themselves into it.
		var objects = new ParkObjects( ThemeName, park, catalogue );

		_ = new ParkFixedItems( ThemeName, park, objects );

		// And the scripts those objects run. After the objects, because a script belongs to something
		// standing in the park rather than the other way round - and now literally so: a script is handed
		// the very animation player its own thing's model is posed from, and its tick loop is what drives
		// the sweep that poses it.
		var rides = new ParkRides( ThemeName, park, catalogue, objects: objects );

		// And the park's people. After the ground, because a guest stands on the land and has to ask how
		// high it is under them; they are sprites rather than models, so they are nothing to do with the
		// objects above and only need the save that named them.
		_ = new ParkGuestSprites( ThemeName, park );

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
		StaffPool = new ParkStaffPool( Balance );

		_ = new ParkPeople( park, Balance, () => rides.GateStatus( park ), ParkState, catalogue,
			thingId => rides.Scheduler.Find( rides.ScriptFor( thingId ) ) );

		// Each group of sound at the volume the options give it, and then the park's own music - which
		// is the order the original uses too: it registers the park's categories, re-applies the group
		// volumes (0x0054ec9a), and only then plays the music (0x0054ec9f). See ParkAudio for what it
		// does with it afterwards, and for why this plays at a fixed level where the original swells
		// it with the crowd.
		GameOptions.Current.ApplySound();
		_ = new ParkAudio( ThemeName );

		// After the audio, because a park opens with its weather already rolled and that first roll
		// may want to start the rain straight away.
		_ = new ParkWeather();

		// The advisor last, as in the lobby: he rides on top of the rest and ducks everything above him
		// while he talks, and the teardown runs in the order things were made, so a park's sound and its
		// weather end before he does. Advisor_Update runs from a park's loop (0x0054f9f9) exactly as it
		// runs from the lobby's (0x0054e6df) - he is the same speaker in both scenes, and what differs is
		// who hands him lines. A park's are in ParkFrontEnd, through UI.ParkLines.
		_ = new Advisor();

		Camera.SetCameraMode<ParkOrbitCameraMode>();
	}

	/// <summary>
	/// The park file for a theme, walked, or null where there is nothing to read.
	///
	/// <para>
	/// Only the jungle ships one of these, which is also why Lost Kingdom is the only park an Instant
	/// Action player can start in. The other three themes have no saved park at all, so they get their
	/// ground and their gate and nothing else - which is a fact about the game's data, not a failure, and
	/// is reported as such.
	/// </para>
	/// <para>
	/// This reads the copy that ships beside the level rather than the player's own. Nothing writes a park
	/// back yet, so the two are identical; when saving exists, this is the line that has to start asking
	/// which player is playing.
	/// </para>
	/// </summary>
	private static ParkWorld? ReadPark( string themeName )
	{
		var path = $"levels/{themeName.ToLowerInvariant()}/Easymode.TPWI";

		if ( !FileSystem.FileExists( path ) )
		{
			Log.Info( $"{themeName}: no park file ships with this theme, so nothing is placed in it" );
			return null;
		}

		try
		{
			using var stream = FileSystem.OpenRead( path );
			var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

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
		// already MAKES button glints and could not draw a single one. SetupParticles runs for parks as
		// well as for the lobby, the windows here start and stop glints exactly as the lobby's do - see
		// OptionsScreen.Closed, which calls StopGlint - and WindowStack draws them through
		// ScreenParticles.Current, which was null for the whole life of a park. So every glint a park
		// raised was allocated, ticked and killed without ever reaching the screen.
		//
		// Over the interface it decorates and under the pointer, which is the order SetupHud uses.
		Hud.AddChild( new ScreenParticles() );

		Hud.AddChild( new Cursor() );
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
		var detail = GameOptions.Current.GraphicsQuality switch
		{
			0 => "low.sam",
			2 => "high.sam",
			_ => "med.sam"
		};

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

		// Whatever was deleted during that walk leaves the list now the walk is over - see Entity.Delete.
		Entity.ApplyDeletions();

		ParticleSystem.Current?.Update();

		// What the pointer is over, before the interface deals out this frame's clicks - so a click
		// and the cell it landed on are the same frame's answer. Only a park has ground to point at.
		if ( Kind == Scene.Park )
			ParkPicking.Update();

		// The HUD is not an entity - see RootPanel - so it is driven from here. After the world,
		// which is where it sat when it was the last entity in the list.
		Hud.Update();

		// And a click on the WORLD, after the interface has had this frame's - so a press that landed
		// on a button never also opens whatever is drawn behind it. The original reaches the same place
		// from the park's own window proc (FUN_004879d0), which acts only while the current interaction
		// mode is idle and hands the click to the mode otherwise.
		if ( Kind == Scene.Park )
			WorldClick();
	}

	private bool _worldMouseWasDown;
	private bool _worldRightWasDown;

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

		// THE RIGHT BUTTON CANCELS whatever is being carried, which is the original's own way out of a
		// place mode. Nothing was charged for picking it up - the money is taken when the thing goes
		// up - so cancelling gives nothing back and takes nobody out of the pool.
		var rightDown = Input.Mouse.Right;
		var rightPressed = rightDown && !_worldRightWasDown;

		_worldRightWasDown = rightDown;

		if ( rightPressed && CancelCarried() is { } cancelled )
		{
			Log.Info( cancelled );
			return;
		}

		if ( !pressed || UI.WindowStack.PointerTaken )
			return;

		if ( ParkPicking.TryCell( out var cellX, out var cellY ) )
			Log.Info( ClickWorldAt( cellX, cellY, ParkPicking.ThingUnderCursor ) );
	}

	/// <summary>
	/// Puts back whatever is on the cursor - an item, or a candidate taken off the hire screen - or
	/// null when nothing is being carried.
	/// </summary>
	/// <remarks>
	/// <b>One body, shared with the console's <c>drop</c>.</b> The right button's edge cannot be driven
	/// by a harness any more than the left's can, so the console reaches the same method rather than a
	/// copy of it; two copies would be free to drift, and only one of them would ever be tested.
	/// </remarks>
	internal string? CancelCarried()
	{
		// Never both at once: each screen closes as it fills its own hand, and neither fills the other's.
		if ( ParkStaffPool.Carrying != 0 )
			return $"world click: {ParkStaffPool.Drop()}";

		if ( ParkBuilding.Carrying != 0 )
			return $"world click: {ParkBuilding.Drop()}";

		return null;
	}

	/// <summary>
	/// What a click on the park does at one cell.
	/// </summary>
	/// <remarks>
	/// <b>Split out from <see cref="WorldClick"/> so the debug console can drive the same path.</b> The
	/// mouse button is not something a harness can press - synthetic motion reaches the window system
	/// and never reaches SDL - so without this the whole placing-by-pointing path would be unreachable
	/// by any test, which is the same reason <c>ParkPicking.PickAt</c> takes coordinates.
	/// </remarks>
	internal string ClickWorldAt( int cellX, int cellY, int thingUnderCursor )
	{
		// ANYTHING IN THE HAND GOES DOWN FIRST, and only an empty hand opens a window. That is the
		// original's own order: a place mode consumes the click - FUN_004879d0 acts only while the
		// current interaction mode is idle, type 0 or 1 - and the modes that carry something are types
		// 4 and 5. Nothing is charged until it actually goes up, which is why cancelling needs no
		// refund; see ParkBuilding.Carrying.
		if ( ParkBuilding.Carrying != 0 )
			return $"world click: {ParkBuilding.PlaceCarried( cellX, cellY )}";

		if ( ParkStaffPool.Carrying != 0 )
			return $"world click: {ParkStaffPool.PlaceCarried( cellX, cellY )}";

		// Somebody already employed, picked up off the park rather than taken out of the pool.
		if ( ParkPeople.Current is { CarriedStaff: not 0 } people )
			return $"world click: put staff down at ({cellX},{cellY}) - {people.DropStaff( cellX, cellY )}";

		if ( thingUnderCursor != 0 )
		{
			OpenObjectWindow( thingUnderCursor );

			return $"world click: opened the window for thing {thingUnderCursor}";
		}

		return $"world click: nothing to do at ({cellX},{cellY})";
	}

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
	/// Whether a window open over this scene holds the world - the game menu, a message box or the options
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
	/// <b>This used to say all three screens test that global before calling, and that is wrong.</b> The
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
	/// Opens the purchase menu on this scene's own window stack.
	///
	/// <para>
	/// It exists for the debug console. A screen is verifiable by eye and capture rather than by test,
	/// and the pointer is not something a harness can move reliably - a warp with no real motion
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
	/// How long every voice still sounding takes to fade as a level ends. The original's state machine hands its
	/// stop-all (0x0051bcb0) 90, in the same untraced unit as the 60 that Sound_StopFading is handed when the advisor
	/// is quietened, which he already takes as milliseconds - so 0.09 seconds. <b>Inferred, not proven.</b>
	/// </summary>
	private const float StopAllSeconds = 0.09f;

	/// <summary>
	/// Ends the level, in the order the original leaves its lobby (state 3). The interface goes first - the windows
	/// close, and the front end empties the advisor's queue through his crying stop. Then every entity, in the
	/// order they were made, which ends the lobby's sound and weather before the advisor himself. Then the camera
	/// lets go of its island, every voice still sounding fades, and the particle system shuts down.
	///
	/// Only between frames: nothing may update or draw a level while it ends, or be half way through a walk over
	/// its entities.
	/// </summary>
	public void Unload()
	{
		foreach ( var panel in Hud.Children.ToArray() )
			panel.Delete();

		foreach ( var entity in Entity.All.ToArray() )
			entity.Delete();

		Entity.ApplyDeletions();

		LobbyCameraMode.ForgetIsland();

		// And both park cameras let go of where they were. Their state is static so that it survives
		// Camera.SetCameraMode building a fresh instance, which means it survives the scene as well
		// unless something says otherwise - so a second park would otherwise open looking at whatever
		// the first one was left looking at, and standing wherever it was last walked to.
		ParkCamcorderCameraMode.Forget();
		ParkOrbitCameraMode.Forget();

		Audio.StopAll( StopAllSeconds );
		ParticleSystem.Current?.Shutdown();
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
		// This pass is still not sorted within itself, and no longer needs to be for the case that
		// used to break: these surfaces write depth now, so two of them resolve by distance rather
		// than by the order their entities happened to be created in. That order is creation
		// order, and an island builds its own meshes before its gate, so the Hallow gate - whose
		// every material is see-through - could draw over the tree standing in front of it.
		//
		// That last part is reasoned from the draw order, not measured. Hallow carries fifty bats
		// seeded afresh every run, which put a 5% noise floor on any frame comparison there and
		// swamp a change of this size.
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
		}

		Entity.All.ForEach( entity => entity.RenderOverlay() );
	}
}
