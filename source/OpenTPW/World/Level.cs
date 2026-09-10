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

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();

		Hud.AddChild( new Cursor() );
	}

	public void Update()
	{
		DebugConsole.Poll();

		Entity.All.ForEach( entity => entity.Update() );
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
