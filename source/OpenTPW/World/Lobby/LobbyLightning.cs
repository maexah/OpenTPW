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
/// <see cref="LobbyScript.Lightning"/>. The spread it picks those offsets with is
/// <c>random01 * 100 - 50</c> for the base and a further <c>random01 * 20 - 10</c> for the top,
/// read out of the executable at 0x00702c94 and 0x00702c8c - so a strike lands within fifty
/// units of the island and leans by up to ten over its five hundred of height.
///
/// It makes no sound. Both of the original's weather drivers play their own thunder after calling the
/// bolt renderer (0x00580320): the lobby's tick (0x005e0470) plays effect 1 of the global lobby sfx on
/// the line after it draws the bolt (0x005e1100), and a park's weather controller (0x00512880) calls the
/// same renderer (0x00512b4c) and plays a thunder of its own. So the sound belongs with whoever strikes -
/// see <see cref="LobbyWeather"/>.
///
/// Engine and content: the bolt - its shape, its flicker, its fade near the camera and its flash - is
/// engine, and would draw a park's lightning the same way. When it strikes, and what that sounds like,
/// is the weather's.
/// </summary>
public sealed class LobbyLightning : WeatherSprites
{
	/// <summary>The original's bolt height, straight out of the call above.</summary>
	private const float BoltHeight = 500f;

	private const float BoltHalfWidth = 9f;

	/// <summary>How far from the island a bolt can come down, and how far its top can lean.</summary>
	private const float GroundSpread = 50f;
	private const float LeanSpread = 10f;

	/// <summary>How long a strike is on screen. Short, and flickering while it lasts.</summary>
	private const float Duration = 0.42f;

	/// <summary>How much the strike lifts the scene's lighting at its brightest.</summary>
	public const float MaxFlash = 1.35f;

	/// <summary>
	/// How close the camera may be to the bolt's axis before the bolt itself is not drawn, and how
	/// far away before it is drawn in full.
	///
	/// A strike lands anywhere within fifty units of the island and the camera orbits seventy out,
	/// so a bolt can come down more or less on top of it - and a bolt is five hundred units tall,
	/// eighteen wide and drawn additively, so at that range it is a full-screen white flash rather
	/// than lightning. Beyond the far end of the band it covers about a sixth of the screen's
	/// width, which is what a bolt near the island should look like.
	///
	/// Only the drawn column fades. <see cref="Flash"/> does not, so a strike that comes down too
	/// close to show still lights the scene - which is what a strike just out of frame should do.
	/// </summary>
	private const float HiddenWithin = 25f;
	private const float SolidBeyond = 55f;

	/// <summary>Where the camera is relative to the bolt, and what that did to it. For DebugConsole.</summary>
	internal float DebugAxisDistance { get; private set; } = float.MaxValue;
	internal float DebugOpacity { get; private set; }

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

	/// <summary>
	/// How much of the bolt to draw, given how near the camera is to it - see
	/// <see cref="HiddenWithin"/>.
	///
	/// Measured sideways from the bolt's axis at the camera's own height rather than from either
	/// end of it: the bolt is five hundred units tall and leans by at most ten, so the camera is
	/// always somewhere along its length and the only distance that means anything is the
	/// horizontal one.
	/// </summary>
	private float NearCameraFade()
	{
		var rise = _top.Z - _base.Z;
		var along = rise <= 0.001f ? 0f : ((Camera.Position.Z - _base.Z) / rise).Clamp( 0f, 1f );

		var axis = _base.LerpTo( _top, along );

		DebugAxisDistance = new Vector3( axis.X - Camera.Position.X, axis.Y - Camera.Position.Y, 0f ).Length;

		return DebugAxisDistance.FadeBetween( HiddenWithin, SolidBeyond );
	}

	protected override void OnUpdate()
	{
		if ( _remaining <= 0f )
		{
			if ( Flash > 0f )
			{
				Flash = 0f;
				DebugOpacity = 0f;
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

		brightness *= NearCameraFade();

		// The quad spans the whole bolt, so its "up" is the lean direction and its half-height
		// is half the bolt's actual length rather than half its vertical extent.
		WriteQuad( 0, centre, along.Normal, BoltHalfWidth, along.Length * 0.5f );

		Tint = new System.Numerics.Vector4( 0.85f, 0.88f, 1f, brightness );
		DebugOpacity = brightness;

		Upload( 1 );
	}
}
