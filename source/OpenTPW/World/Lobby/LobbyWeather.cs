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
///
/// Engine and content: this is the lobby's own weather driver, reading each island's script and playing the
/// lobby's thunder. The sky, the rain and the bolt it drives are engine; a park has a weather controller of
/// its own (0x00512880) that draws with the same bolt renderer and plays its own thunder.
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

	/// <summary>The live bolt, so DebugConsole can report how near the camera it came.</summary>
	internal LobbyLightning DebugBolt => _lightning;

	/// <summary>Fires a strike now rather than waiting on the roll, for DebugConsole.</summary>
	internal void DebugStrike() => Strike( LobbyCameraMode.CurrentIsland?.Position ?? Vector3.Zero );

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
	/// Eases the park's SKYCOLOUR into the sky, and pushes the sun up for as long as a strike is on
	/// screen: every lit model multiplies by the sun's colour, so that is what makes a strike light
	/// the scene rather than just draw a bright rectangle in it.
	///
	/// The sky itself does not flash. It is what the distance hazes toward, so lifting it toward
	/// white lifts the haze with it - and the sky can only reach white by clipping each of its
	/// layers separately against the framebuffer, while the haze reaches it exactly, so the two
	/// arrive at different colours and the horizon shows for as long as the strike lasts. The
	/// original does not flash its sky either; its flash is a renderer flag on lit geometry.
	///
	/// SKYCOLOUR does not paint the sky - it tints one of its four cloud layers, and the horizon
	/// stays the blue its texture paints whatever the park asks for. See <see cref="Sky"/>.
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

		_sky.Tint = _skyColour;

		var flash = _lightning.Flash;

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
			Strike( LobbyCameraMode.CurrentIsland!.Position );
	}

	/// <summary>
	/// A strike near <paramref name="islandOrigin"/>: the bolt, and then its thunder - effect 1 of the global lobby
	/// sfx, which the original's lobby tick (0x005e0470) plays on the line after it draws the bolt (0x005e1100).
	/// The roll and the debug console both come here, so a forced strike thunders too.
	/// </summary>
	private void Strike( Vector3 islandOrigin )
	{
		_lightning.Strike( islandOrigin );
		LobbyAudio.Current?.Thunder();
	}
}
