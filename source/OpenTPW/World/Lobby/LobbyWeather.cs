namespace OpenTPW;

/// <summary>
/// The lobby's weather, which belongs to whichever park the camera is looking at rather than to
/// any place in the world.
///
/// This is how the original does it. Its lobby tick works out which island is nearest, then
/// every frame copies that island's SKYCOLOUR into the renderer's sky colour, its RAINY value
/// into a single global rain level, and rolls its LIGHTNING mask for a strike. There is one sky,
/// one rain system and one bolt for the whole lobby; selecting a park is what changes them. So
/// this reads <see cref="LobbyCameraMode.CurrentIsland"/> and does the same three things.
///
/// Of the four parks only hallow asks for any of it - RAINY(1) and LIGHTNING(63) - which is why
/// Halloween World is the only one that storms. All four set a SKYCOLOUR.
/// </summary>
public sealed class LobbyWeather : Entity
{
	/// <summary>
	/// How fast the sky crossfades between parks. The original snaps, because it spends the
	/// change spinning a globe from one island to the next and the snap happens out of sight;
	/// this slides straight between islands in about half a second, where a snap reads as a
	/// glitch. Matched to the camera's own body rate so the two arrive together.
	/// </summary>
	private const float SkyRate = 1.32f;

	/// <summary>The one weather system, so DebugConsole can reach it. There is only ever one.</summary>
	internal static LobbyWeather? Current { get; private set; }

	/// <summary>Overrides the script's rain level when set, for DebugConsole.</summary>
	internal float? DebugRain { get; set; }

	/// <summary>Fires a strike now rather than waiting on the roll, for DebugConsole.</summary>
	internal void DebugStrike() => _lightning.Strike( LobbyCameraMode.CurrentIsland?.Position ?? Vector3.Zero );

	private readonly LobbyRain _rain = new();
	private readonly LobbyLightning _lightning = new();

	public LobbyWeather() => Current = this;

	private Sky? _sky;
	private Vector3 _skyColour = LobbyScript.DefaultSkyColour;
	private bool _placed;

	protected override void OnUpdate()
	{
		var script = LobbyCameraMode.CurrentIsland?.Script;

		if ( script == null )
			return;

		UpdateSky( script );

		_rain.Level = DebugRain ?? script.Rainy;

		UpdateLightning( script );
	}

	/// <summary>
	/// Eases the sky toward the current park's colour, and lifts it toward white for as long as a
	/// strike is on screen. The sun goes with it: every lit model multiplies by the sun's colour,
	/// so pushing that up is what makes a strike light the whole scene rather than just draw a
	/// bright rectangle in the sky.
	/// </summary>
	private void UpdateSky( LobbyScript script )
	{
		_sky ??= Entity.All.OfType<Sky>().FirstOrDefault();

		if ( _sky == null )
			return;

		var wanted = script.HasSkyColour ? script.SkyColour : LobbyScript.DefaultSkyColour;

		if ( _placed )
		{
			_skyColour = _skyColour.LerpTo( wanted, Time.SmoothingFactor( SkyRate ) );
		}
		else
		{
			// Nothing to ease from on the first frame.
			_skyColour = wanted;
			_placed = true;
		}

		var flash = _lightning.Flash;

		var lit = _skyColour.LerpTo( Vector3.One, flash * 0.8f );

		_sky.Colour = lit;

		// The horizon is where the fogged distance meets the sky, so they have to be the same
		// colour or there is a hard line across the middle of the screen.
		Level.FogColour = lit;

		if ( Level.SunLight != null )
			Level.SunLight.Color = Vector3.One * (1f + (flash * LobbyLightning.MaxFlash));
	}

	/// <summary>
	/// Rolls for a strike. <see cref="LobbyScript.StrikesPerSecond"/> is the original's per-frame
	/// chance restated as a rate, and this turns that into a per-frame chance again for however
	/// long this frame actually was - so the storm keeps the same pace at any frame rate, which
	/// the original's straight per-frame test does not.
	/// </summary>
	private void UpdateLightning( LobbyScript script )
	{
		var rate = script.StrikesPerSecond;

		if ( rate <= 0f )
			return;

		var chance = 1f - MathF.Exp( -rate * Time.Delta );

		if ( Random.Shared.NextSingle() < chance )
			_lightning.Strike( LobbyCameraMode.CurrentIsland!.Position );
	}
}
