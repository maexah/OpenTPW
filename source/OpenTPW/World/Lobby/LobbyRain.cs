namespace OpenTPW;

/// <summary>
/// The lobby's rain, drawn with the game's own Raindrop.tga.
///
/// RAINY(n) in a park's script sets a rain level, and the original copies the currently selected
/// island's value into one global every frame - so this is a single system whose intensity
/// follows the camera rather than rain belonging to a place. Only hallow asks for it, and only
/// at level 1.
///
/// Drops live in a box that travels with the camera, wrapping round rather than respawning at
/// the top: a drop that falls out of the bottom comes back in at the top, and one the camera
/// leaves behind comes back in on the other side. That keeps the density even wherever you look
/// while still letting the drops have real world positions, so they slide past the islands with
/// proper parallax instead of hanging off the camera.
/// </summary>
public sealed class LobbyRain : WeatherSprites
{
	/// <summary>Drops at RAINY(1). The pool is sized for this and never grows.</summary>
	private const int DropsPerLevel = 900;

	/// <summary>
	/// Headroom over RAINY(1), which is the only level any shipped park asks for. The pool is
	/// allocated for this and never grows.
	/// </summary>
	private const int MaxLevel = 2;

	/// <summary>Half-extents of the box drops live in, centred a little ahead of the camera.</summary>
	private static readonly Vector3 HalfVolume = new( 44f, 44f, 34f );

	/// <summary>How far ahead of the camera to centre that box, so most of it is on screen.</summary>
	private const float LookAhead = 26f;

	private const float FallSpeed = 62f;
	private const float DropHalfWidth = 0.09f;
	private const float DropHalfHeight = 0.5f;

	/// <summary>
	/// Drops nearer the camera than this are not drawn.
	///
	/// A drop is a quad of fixed size in the world, so one that happens to wrap in a few units
	/// from the eye covers a huge part of the screen and reads as a falling icicle rather than
	/// as rain. Real rain has the same drops at the same size much closer to the eye, but they
	/// are too fast and too out of focus to register; dropping them is nearer the truth than
	/// drawing them.
	/// </summary>
	private const float NearCutoff = 9f;

	/// <summary>A slow drift, so it doesn't read as a static column of falling dots.</summary>
	private static readonly Vector3 Wind = new( 5.5f, 2.5f, 0f );

	/// <summary>How quickly rain fades in and out when the camera changes island.</summary>
	private const float FadeRate = 2.2f;

	/// <summary>0 for dry, 1 for a park asking for RAINY(1). Set by <see cref="LobbyWeather"/>.</summary>
	public float Level { get; set; }

	private readonly Vector3[] _drops;
	private readonly Random _rng = new( 0x2A1D );
	private float _visible;
	private bool _seeded;

	public LobbyRain() : base( DropsPerLevel * MaxLevel, "generic/weather/Raindrop.tga" )
	{
		_drops = new Vector3[DropsPerLevel * MaxLevel];
	}

	protected override void OnUpdate()
	{
		// Fade rather than snap, because the level changes the moment the camera starts moving
		// to another island and a storm blinking out mid-flight looks like a bug.
		_visible = _visible.LerpTo( Level, Time.SmoothingFactor( FadeRate ) );

		if ( _visible < 0.01f && Level <= 0f )
		{
			// Fully dry: leave the pool collapsed and stop doing any work at all.
			if ( _seeded )
			{
				Upload( 0 );
				_seeded = false;
			}

			return;
		}

		var centre = Camera.Position + (Camera.Rotation.Forward * LookAhead);

		if ( !_seeded )
		{
			Seed( centre );
			_seeded = true;
		}

		var count = Math.Clamp( (int)(DropsPerLevel * _visible), 0, _drops.Length );
		var dt = Time.Delta;

		for ( int i = 0; i < count; ++i )
		{
			var drop = _drops[i] + (new Vector3( Wind.X, Wind.Y, -FallSpeed ) * dt);

			_drops[i] = Wrap( drop, centre );

			if ( (_drops[i] - Camera.Position).LengthSquared < NearCutoff * NearCutoff )
				Collapse( i );
			else
				WriteQuad( i, _drops[i], Vector3.Up, DropHalfWidth, DropHalfHeight );
		}

		// Brightening the drops with the level rather than only adding more of them keeps the
		// fade smooth - otherwise it steps one drop at a time.
		Tint = new System.Numerics.Vector4( 0.62f, 0.68f, 0.78f, _visible.Clamp( 0f, 1f ) );

		Upload( count );
	}

	/// <summary>Brings a drop back into the box on whichever axes it has left, keeping the rest.</summary>
	private static Vector3 Wrap( Vector3 drop, Vector3 centre )
	{
		return new Vector3(
			WrapAxis( drop.X, centre.X, HalfVolume.X ),
			WrapAxis( drop.Y, centre.Y, HalfVolume.Y ),
			WrapAxis( drop.Z, centre.Z, HalfVolume.Z ) );
	}

	private static float WrapAxis( float value, float centre, float half )
	{
		var span = half * 2f;
		var offset = value - (centre - half);

		// A single modulo rather than a loop, so a big camera jump between islands costs the
		// same as an ordinary frame.
		offset -= MathF.Floor( offset / span ) * span;

		return centre - half + offset;
	}

	private void Seed( Vector3 centre )
	{
		for ( int i = 0; i < _drops.Length; ++i )
		{
			_drops[i] = centre + new Vector3(
				((_rng.NextSingle() * 2f) - 1f) * HalfVolume.X,
				((_rng.NextSingle() * 2f) - 1f) * HalfVolume.Y,
				((_rng.NextSingle() * 2f) - 1f) * HalfVolume.Z );
		}
	}
}
