namespace OpenTPW;

/// <summary>
/// A lightning bolt over the lobby, drawn with the game's own lightning.tga.
///
/// The original's lobby tick, having picked the island nearest the camera, does this every frame:
///
///     if ( island-&gt;lightning &gt; 0 &amp;&amp; (island-&gt;lightning &amp; random()) == 1 )
///     {
///         DrawBolt( bottomX, 0, bottomZ, topX, 500.0f, topZ, 500, 1 );
///         SetLightningFlag();
///         PlaySound( thunder );
///     }
///
/// So a bolt runs from ground level up to 500 units, its top offset from its base by a small
/// random lean, somewhere near the island; the mask decides how often - see
/// <see cref="LobbyScript.Lightning"/>. The spread constants the original picks those offsets
/// with are float globals this hasn't run down, so the ones here are chosen to look right at the
/// distance the lobby camera actually sits at; the 500-unit height is the game's own.
///
/// There is no sound system yet, so the thunder is missing.
/// </summary>
public sealed class LobbyLightning : WeatherSprites
{
	/// <summary>The original's bolt height, straight out of the call above.</summary>
	private const float BoltHeight = 500f;

	private const float BoltHalfWidth = 9f;

	/// <summary>How far from the island a bolt can come down, and how far its top can lean.</summary>
	private const float GroundSpread = 90f;
	private const float LeanSpread = 45f;

	/// <summary>How long a strike is on screen. Short, and flickering while it lasts.</summary>
	private const float Duration = 0.42f;

	/// <summary>How much the strike lifts the scene's lighting at its brightest.</summary>
	public const float MaxFlash = 1.35f;

	/// <summary>
	/// 0 when nothing is happening, rising to 1 at the peak of a strike. Read by
	/// <see cref="LobbyWeather"/> to flash the sun and the sky along with the bolt.
	/// </summary>
	public float Flash { get; private set; }

	private readonly Random _rng = new( 0x104E );
	private Vector3 _base;
	private Vector3 _top;
	private float _remaining;
	private float _flicker;

	public LobbyLightning() : base( 1, "generic/weather/lightning.tga" )
	{
	}

	/// <summary>Starts a strike near <paramref name="islandOrigin"/>, replacing any still running.</summary>
	public void Strike( Vector3 islandOrigin )
	{
		float Spread( float extent ) => ((_rng.NextSingle() * 2f) - 1f) * extent;

		_base = new Vector3(
			islandOrigin.X + Spread( GroundSpread ),
			islandOrigin.Y + Spread( GroundSpread ),
			0f );

		_top = _base + new Vector3( Spread( LeanSpread ), Spread( LeanSpread ), BoltHeight );

		_remaining = Duration;
		_flicker = 0f;
	}

	protected override void OnUpdate()
	{
		if ( _remaining <= 0f )
		{
			if ( Flash > 0f )
			{
				Flash = 0f;
				Upload( 0 );
			}

			return;
		}

		_remaining -= Time.Delta;
		_flicker += Time.Delta;

		var life = (_remaining / Duration).Clamp( 0f, 1f );

		// Bright immediately and then falling away, with a stutter on top so it reads as a
		// strike rather than a fading rectangle. Frame-rate independent: the stutter is a
		// function of elapsed time, not of how many frames have gone by.
		var stutter = 0.55f + (0.45f * MathF.Abs( MathF.Sin( _flicker * 47f ) ));
		var brightness = life * life * stutter;

		Flash = brightness;

		if ( _remaining <= 0f )
		{
			Upload( 0 );
			return;
		}

		var centre = (_base + _top) * 0.5f;
		var along = _top - _base;

		// The quad spans the whole bolt, so its "up" is the lean direction and its half-height
		// is half the bolt's actual length rather than half its vertical extent.
		WriteQuad( 0, centre, along.Normal, BoltHalfWidth, along.Length * 0.5f );

		Tint = new System.Numerics.Vector4( 0.85f, 0.88f, 1f, brightness );

		Upload( 1 );
	}
}
