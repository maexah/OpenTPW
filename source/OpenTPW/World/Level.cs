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
	/// The original's lobby uses one constant here, 0xFF44DDFF, and never varies it by park. It is
	/// also what the frame is cleared to, so the gaps between the sky's cloud layers show sky
	/// rather than black.
	/// </summary>
	public static Vector3 FogColour { get; set; } = new( 0x44 / 255f, 0xDD / 255f, 0xFF / 255f );

	/// <summary>
	/// How thick that fade is, and whether there is one at all. Off in the lobby, as it is in the
	/// original's island view - see <see cref="ObjectUniformBuffer.g_flFogDensity"/>.
	/// </summary>
	public static float FogDensity { get; set; }

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
