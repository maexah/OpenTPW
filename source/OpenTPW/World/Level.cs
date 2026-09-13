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

	/// <summary>The theme folder this level was built from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; private init; }

	public Level( string levelName, Scene kind = Scene.Lobby )
	{
		ThemeName = levelName;
		Kind = kind;

		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

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
	/// A park, as far as it goes today: the ground's scenery and something to look at it with.
	///
	/// <para>
	/// Deliberately short. There is no sky here because <see cref="Sky"/> takes its colours from
	/// whichever lobby island the camera is on, and no weather, no front end and no advisor, because
	/// each of those is a job with its own evidence still to gather. What this does have is the part
	/// that needed no new file format at all - <c>base.MD2</c> out of the park's own terrain.wad,
	/// through the same model and texture path the lobby already uses.
	/// </para>
	/// </summary>
	private void SetupParkEntities()
	{
		// The theme's own numbers, global defaults underneath - see ParkBalance for why that stack
		// matters more than it looks.
		Balance = new ParkBalance( ThemeName );
		Log.Info( $"{ThemeName}: balance stack came to {Balance.Count} keys" );

		// What distance fades to. The lobby's Sky rewrites this every frame from its own horizon; a
		// park builds no Sky, so setting it once here holds. Jungle and fantasy are a pale blue within
		// a few points of the lobby's own constant, hallow is nearly black and space is orange.
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

		// The ground first, then what stands on it.
		_ = new ParkGround( ThemeName );
		_ = new ParkTerrain( ThemeName );

		GameOptions.Current.ApplySound();

		Camera.SetCameraMode<ParkOrbitCameraMode>();
	}

	/// <summary>
	/// A park's interface, which for now is only the pointer. The lobby's front end belongs to the
	/// lobby, and the park's own is a separate build of the same widgets.
	/// </summary>
	private void SetupParkHud()
	{
		Hud = new();
		Hud.AddChild( new Cursor() );
	}

	private void SetupHud()
	{
		Hud = new();

		// The interface's windows, and what the pointer and the keys do to them.
		var windows = Hud.AddChild( new WindowStack() );

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

		Entity.All.ForEach( entity => entity.Update() );

		// Whatever was deleted during that walk leaves the list now the walk is over - see Entity.Delete.
		Entity.ApplyDeletions();

		ParticleSystem.Current?.Update();

		// The HUD is not an entity - see RootPanel - so it is driven from here. After the world,
		// which is where it sat when it was the last entity in the list.
		Hud.Update();
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
		Entity.All.ForEach( entity => entity.RenderOverlay() );
	}
}
