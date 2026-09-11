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

	public Level( string levelName )
	{
		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		SetupEntities();
		SetupParticles();
		SetupHud();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

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
		// islands so the first frame already has one to play.
		_ = new LobbyAudio();

		// The advisor rides on top of that: he greets a new player on the first frame he gets,
		// and ducks everything above while he does it.
		_ = new LobbyAdvisor();

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		// The lobby's interface - the player slots, its dialogs and the island panel. Built here, behind
		// the loading screen, along with everything its windows draw.
		Hud.AddChild( new FrontEnd() );

		// On-screen particle effects go over the interface they decorate, and under the pointer.
		Hud.AddChild( new ScreenParticles() );

		Hud.AddChild( new Cursor() );
	}

	/// <summary>
	/// The particle system, which the state machine loads before the lobby itself (Data\Particle\Tp2.plb).
	///
	/// Its density comes from the detail files, which OpenTPW has no setting to choose between yet, so
	/// it takes the middle one's. Which of them the original starts on was not found.
	/// </summary>
	private static void SetupParticles()
	{
		var density = 1024;

		try
		{
			if ( int.TryParse( new SettingsFile( "/med.sam" )["GameOptions.PARTICLEDENSITY"], out var value ) )
				density = value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Particles: med.sam would not load, so effects run at their own rates - {e.Message}" );
		}

		_ = new ParticleSystem( "Particle/Tp2.plb", density );
	}

	public void Update()
	{
		DebugConsole.Poll();

		Entity.All.ForEach( entity => entity.Update() );

		ParticleSystem.Current?.Update();

		// The HUD is not an entity - see RootPanel - so it is driven from here. After the world,
		// which is where it sat when it was the last entity in the list.
		Hud.Update();
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );

		// Everything see-through comes after everything solid. A translucent surface doesn't write
		// depth, so drawn in creation order alongside the rest it would be hidden by any solid
		// geometry that happened to be drawn after it - which for the Space island's antenna is
		// the island itself.
		//
		// Two passes rather than a sort, and this pass is not sorted within itself. That is not
		// because nothing overlaps - most of what the flag marks is cut-out foliage, and a palm
		// crown overlaps itself heavily - but because the alpha test in the shader carries that
		// case: a frond's texels are either kept or discarded, so the order two fronds arrive in
		// does not change the result. It is only the handful of genuinely graded surfaces, the
		// ripple rings and the antenna's cone, that a sort would help, and those are small,
		// scattered, and do not overlap each other.
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
