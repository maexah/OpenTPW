using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }

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

		_ = new Water() { Scale = new Vector3( 10000f ) };
		_ = new Sky();

		var jungle = new Vector3( 400, 400, 0 );
		_ = new LobbyIsland( jungle, "Jungle" );
		_ = new LobbyGate( jungle, "Jungle" );

		SpawnButterflies( jungle, count: 16 );

		_ = new LobbyIsland( new Vector3( 600, 400, 0 ), "Hallow" );
		_ = new LobbyIsland( new Vector3( 600, 600, 0 ), "Fantasy" );
		_ = new LobbyIsland( new Vector3( 400, 600, 0 ), "Space" );

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	/// <summary>
	/// Spawns a swarm of butterflies with randomised colours, start points and flight
	/// directions - all derived from one seed, logged so a particular arrangement can be
	/// reproduced if it's ever worth debugging.
	/// </summary>
	private void SpawnButterflies( Vector3 islandOrigin, int count )
	{
		var seed = Environment.TickCount;
		var rng = new Random( seed );

		Log.Info( $"Jungle butterfly swarm seed: {seed}" );

		string[] colours = { "Bfly_YELL", "Bfly_PINK" };

		for ( int i = 0; i < count; ++i )
		{
			var colour = colours[rng.Next( colours.Length )];

			// Scatter spawn points across the flight volume rather than clustering them all
			// at one radius/height, so the swarm looks established from the first frame
			// instead of visibly starting from a single ring.
			var angle = rng.NextSingle() * MathF.PI * 2f;
			var radius = MathF.Sqrt( rng.NextSingle() ) * LobbyButterfly.MaxRadius * 0.8f;
			var height = LobbyButterfly.MinHeight
				+ (rng.NextSingle() * (LobbyButterfly.MaxHeight - LobbyButterfly.MinHeight));

			var startPosition = islandOrigin + new Vector3(
				MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, height );

			// Mostly horizontal to start, with a shallow pitch so nobody spawns already
			// diving or climbing steeply.
			var headingYaw = rng.NextSingle() * MathF.PI * 2f;
			var headingPitch = ((rng.NextSingle() * 2f) - 1f) * (MathF.PI / 12f);
			var startDirection = new Vector3(
				MathF.Cos( headingYaw ) * MathF.Cos( headingPitch ),
				MathF.Sin( headingYaw ) * MathF.Cos( headingPitch ),
				MathF.Sin( headingPitch ) );

			_ = new LobbyButterfly( colour, islandOrigin, startPosition, startDirection, rng.Next() );
		}
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
		Entity.All.ForEach( entity => entity.Update() );
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
