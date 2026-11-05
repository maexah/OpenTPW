namespace OpenTPW;

/// <summary>
/// A lightning bolt, drawn with the game's own lightning.tga. Engine, not either scene's - the lobby
/// strikes near the island the camera is on, and a park anywhere across its map.
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
/// Engine and content: the bolt - its grey, its sixteen slots, its fade near the camera and its flash - is
/// engine, and draws a park's lightning the same way. When it strikes, and what that sounds like,
/// is the weather's.
///
/// Each live bolt is drawn in the grey of <see cref="BoltEnvelope"/>: a saw-tooth of 192 to 255 every 500 ms of the
/// life it has left, plus a 15-bit draw of Lightning's own generator each frame, standing for the CRT's <c>rand()</c> (0x0067b5c0), whose <c>&amp; 63</c> the grey takes,, packed as the original packs it, so a sum past
/// 255 wraps to near black. The bolt is cut at the frame its life left reaches nought, with no fade. Up to sixteen are
/// alive at once (<see cref="BoltSlots"/>). Its shape stays one quad, nine wide (Q1035).
/// </summary>
public sealed class Lightning : WeatherSprites
{
	/// <summary>The original's bolt height, straight out of the call above.</summary>
	private const float BoltHeight = 500f;

	private const float BoltHalfWidth = 9f;

	/// <summary>How far from the island a bolt can come down, and how far its top can lean.</summary>
	private const float GroundSpread = 50f;
	private const float LeanSpread = 10f;

	/// <summary>How much the strike lifts the scene's lighting at its brightest. A deviation (Q1050): the original lifts nothing.</summary>
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
	/// close to show still lights the lobby - which is what a strike just out of frame should do. In a
	/// park only the debug console reads it.
	/// </summary>
	private const float HiddenWithin = 25f;
	private const float SolidBeyond = 55f;

	/// <summary>Where the camera is relative to the bolt, and what that did to it. For DebugConsole.</summary>
	internal float DebugAxisDistance { get; private set; } = float.MaxValue;

	/// <summary>The brightest slot's grey, 0 to 1, after the fade near the camera. For DebugConsole.</summary>
	internal float DebugOpacity { get; private set; }

	/// <summary>How many bolts are alive. For DebugConsole.</summary>
	internal int DebugLive => _bolts.Count;

	/// <summary>The first live bolt's life left in ms and the grey bytes it was drawn in this frame. For DebugConsole.</summary>
	internal float DebugLifeLeft { get; private set; }
	internal (byte R, byte G, byte B) DebugColour { get; private set; }

	/// <summary>
	/// 0 when nothing is happening, rising to 1 at the peak of a strike. Read by
	/// <see cref="LobbyWeather"/> to lift the sun along with the bolt, a deviation the original does not make (Q1050). It keeps
	/// its own curve, a fade over the bolt's lifetime with a stutter, which is not the bolt's grey.
	/// </summary>
	public float Flash { get; private set; }

	private readonly Random _rng = new( 0x104E );
	private readonly BoltSlots _bolts = new();
	private float _duration;

	public Lightning() : base( BoltSlots.Capacity, "generic/weather/lightning.tga" )
	{
	}

	/// <summary>The lifetime, in seconds, the strike last started was given.</summary>
	internal float DebugLifetime => _duration;

	/// <summary>
	/// Starts a strike near <paramref name="islandOrigin"/>. Its
	/// <paramref name="lifetime"/> is the scene's: the original's bolt draw (FUN_00580320) takes it as
	/// argument 7, in milliseconds (<c>docs/exe/weather.md</c>, "The bolt").
	/// </summary>
	public void Strike( Vector3 islandOrigin, float lifetime )
	{
		float Spread( float extent ) => ((_rng.NextSingle() * 2f) - 1f) * extent;

		var ground = new Vector3(
			islandOrigin.X + Spread( GroundSpread ),
			islandOrigin.Y + Spread( GroundSpread ),
			0f );

		Strike( ground, ground + new Vector3( Spread( LeanSpread ), Spread( LeanSpread ), BoltHeight ), lifetime );
	}

	/// <summary>
	/// Starts a strike between two points the caller has chosen. It runs alongside any still alive, up to the
	/// original's sixteen; a seventeenth is dropped, as the original drops it (0x0058032c).
	///
	/// <para>
	/// A park picks its own rather than scattering them about an origin: FUN_00512c50 puts the ground
	/// point anywhere across the whole map, clamped only away from the very edge, and the top three
	/// hundred units up leaning by as much as fifty either way. The lobby's spreads - fifty around the
	/// island and a ten-unit lean over five hundred of height - cannot express that, so a park hands
	/// over the pair it has already worked out.
	/// </para>
	/// </summary>
	/// <param name="lifetime">How long the bolt is up, in seconds; nought or less draws nothing.</param>
	public void Strike( Vector3 ground, Vector3 top, float lifetime )
	{
		if ( _bolts.Add( ground, top, lifetime ) )
			_duration = lifetime;
		else if ( lifetime > 0f )
			Log.Info( $"Lightning: a seventeenth bolt dropped, as the original drops it ({BoltSlots.Capacity} slots)" );
	}

	/// <summary>
	/// How much of the bolt to draw, given how near the camera is to it - see
	/// <see cref="HiddenWithin"/>.
	///
	/// Measured sideways from the bolt's axis at the camera's own height rather than from either
	/// end of it: a bolt is three to five hundred units tall and leans by at most fifty, so the camera is
	/// always somewhere along its length and the only distance that means anything is the
	/// horizontal one.
	/// </summary>
	private float NearCameraFade( Vector3 ground, Vector3 top )
	{
		var rise = top.Z - ground.Z;
		var along = rise <= 0.001f ? 0f : ((Camera.Position.Z - ground.Z) / rise).Clamp( 0f, 1f );

		var axis = ground.LerpTo( top, along );

		DebugAxisDistance = new Vector3( axis.X - Camera.Position.X, axis.Y - Camera.Position.Y, 0f ).Length;

		return DebugAxisDistance.FadeBetween( HiddenWithin, SolidBeyond );
	}

	protected override void OnUpdate()
	{
		if ( _bolts.Count == 0 )
		{
			if ( Flash > 0f )
			{
				Flash = 0f;
				DebugOpacity = 0f;
				DebugLifeLeft = 0f;
				DebugColour = (0, 0, 0);
				Upload( 0 );
			}

			return;
		}

		// The bolt's clock is the game's own, so a park's menu holds it where the original's does (the life left
		// stays put, dt = 0, while the bolt is redrawn with a fresh grey each frame); nothing in the lobby pauses it.
		_bolts.Advance( GameClock.Delta );

		var flash = 0f;
		var brightest = 0f;

		for ( var slot = 0; slot < _bolts.Count; ++slot )
		{
			var bolt = _bolts[slot];

			// The flash is not the bolt's grey: it is bright immediately and then falling away, with a stutter on
			// top, frame-rate independent because the stutter is a function of elapsed time.
			var life = (bolt.LifeMs / bolt.DurationMs).Clamp( 0f, 1f );
			var stutter = 0.55f + (0.45f * MathF.Abs( MathF.Sin( bolt.AgeSeconds * 47f ) ));
			flash = MathF.Max( flash, life * life * stutter );

			// One draw a bolt a frame (Lightning's own generator, standing for the CRT's rand() at 0x0067b5c0), as the original takes it, a paused menu's frames too (the
			// original redraws a held bolt in a fresh grey each frame). Only the debug console's held frame clock keeps
			// the last grey, so a paused or stepped frame is repeatable.
			var fade = NearCameraFade( bolt.Ground, bolt.Top );
			var colour = _bolts.Draw( slot, _rng.Next( 0x8000 ), Time.Delta > 0f, fade );
			bolt = _bolts[slot];

			// The quad spans the whole bolt, so its "up" is the lean direction and its half-height
			// is half the bolt's actual length rather than half its vertical extent.
			var along = bolt.Top - bolt.Ground;
			WriteQuad( slot, (bolt.Ground + bolt.Top) * 0.5f, along.Normal, BoltHalfWidth, along.Length * 0.5f, colour );

			brightest = MathF.Max( brightest, MathF.Max( colour.X, MathF.Max( colour.Y, colour.Z ) ) );

			if ( slot == 0 )
			{
				DebugLifeLeft = bolt.LifeMs;
				DebugColour = (bolt.R, bolt.G, bolt.B);
			}
		}

		Flash = flash;
		DebugOpacity = brightest;

		Tint = System.Numerics.Vector4.One;

		Upload( _bolts.Count );
	}
}
