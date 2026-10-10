namespace OpenTPW;

/// <summary>
/// Where a bumper car's sound goes (<see cref="ParkBumperCars.ISounds"/>): the effect a ride's <c>EventMap.rse</c> names
/// by slot, played from the park's <c>cat_rides</c> at the car, and the parameters the step sets on it.
/// <c>docs/exe/audio.md</c>, "What an EventMap's slots feed" and "A voice's two controllers".
///
/// <para>
/// <b>A held voice.</b> The Hot Pot's engine, jungle effect 194, is flagged <c>0x6</c>: no timer ends it, and its own
/// tick plays its sample again each time the channel ends, so the car keeps one handle for the whole go (measured in
/// the original, Q202). Its one 447 ms sample is looped here, which the original does with up to a service pass's gap.
/// </para>
/// <para>
/// <b>Its two controllers.</b> A variation names the parameter ids its first and second controllers answer to and what
/// each drives - bit 1 the volume, bit 2 the pitch - over the variation's ranges: for 194, parameter 16, the
/// <c>EventMap</c>'s slot 10, drives the pitch over -24 to 36 96ths of an octave, so the speed / 3 the step hands it
/// raises the engine from 0.83 of its pitch at rest toward 1.2 at a boat's top speed.
/// </para>
/// </summary>
public sealed class ParkCarSounds : ParkBumperCars.ISounds
{
	/// <summary>The float the original scales a car's place by for the sound (<c>0x00700f20</c>): 302.7 to a world unit.</summary>
	private const double PlaceScale = 0.0033036007080227137;

	/// <summary>What <c>Sound_StopFading</c> is handed (<c>0x0051c300</c>), taken as milliseconds as the advisor's is.</summary>
	private const float FadeSeconds = 0.06f;

	/// <summary>A voice the cars hold: what plays, its variation's header, and the four controller slots' key and value.</summary>
	public sealed class Held
	{
		/// <summary>What plays; null only while it is being started.</summary>
		public Voice Voice { get; internal set; } = null!;

		public required int Effect { get; init; }

		public required SoundCategoryFile.Variation Header { get; init; }

		/// <summary>The keys, <c>[+0x4c]</c>'s first four bytes: the effect's parameter id, then the two controllers', then 0.</summary>
		public required byte[] Keys { get; init; }

		/// <summary>Each slot's value, the next four bytes.</summary>
		public byte[] Values { get; } = new byte[4];

		/// <summary>The pitch last applied, in 96ths of an octave, for the census.</summary>
		public int Pitch { get; internal set; }
	}

	private readonly RideScriptScheduler _scripts;
	private readonly Random _random = new();

	public ParkCarSounds( RideScriptScheduler scripts )
	{
		_scripts = scripts;
	}

	/// <summary>
	/// The script of the thing whose track ride <paramref name="ride"/> is - the original walks the things for one of
	/// type 3 holding the handle (<c>0x0054a2bd</c>..<c>0x0054a2ee</c>), and reads its script.
	/// </summary>
	private RideScript? ScriptOf( int ride )
		=> _scripts.NewestFirst().FirstOrDefault( script => script.TrackRide == ride && script.Bumpers is not null );

	public object? Play( int ride, int slot, int x, int z )
	{
		if ( ScriptOf( ride ) is not { } script )
			return null;

		var effect = _scripts.SoundVariable( script.Id, slot );

		// FUN_0051eeb0 plays nothing for a slot holding nought.
		if ( effect == 0 || ParkAudio.Current?.Rides is not { } rides )
			return null;

		var headers = rides.VariationsOf( effect );

		if ( headers.Count == 0 )
			return null;

		var header = headers[0];
		var at = Place( x, z );

		// The first child takes the first variation (0x006c3e26); 194 has one. The volume is the variation's byte
		// out of 100 - the original multiplies it by its group levels (FUN_006bb860) before QMixer's SetVolume.
		var held = new Held
		{
			Effect = effect,
			Header = header,
			Keys = [(byte)rides.ParameterOf( effect ), (byte)header.FirstKey, (byte)header.SecondKey, 0]
		};

		held.Voice = Audio.Play( rides.PickFrom( effect, 0 ), Level( held, 1, header.Volume ) / 100f, loop: true,
			bus: AudioBus.Effects, position: at, range: rides.RangeOf( effect, 0 ) )!;

		if ( held.Voice is null )
			return null;

		Apply( held );

		Log.Info( $"Car sounds: ride 0x{ride:x} slot {slot}: effect {effect} '{held.Voice.Name}' at ({at.X:0.0},{at.Y:0.0}), "
			+ $"pitch {held.Pitch}/96 octave, rate {held.Voice.Rate:0.000}" );

		return held;
	}

	public bool Move( object voice, int x, int z )
	{
		if ( voice is not Held { Voice.Playing: true } held )
			return false;

		held.Voice.MoveTo( Place( x, z ) );
		return true;
	}

	/// <summary><c>FUN_0051bc40</c> on a held voice: the parameter the ride's slot names, set (<see cref="Set"/>).</summary>
	public bool SetParameter( object voice, int ride, int slot, int level )
	{
		if ( voice is not Held { Voice.Playing: true } held )
			return false;

		// With no thing found, the original reads slot 10 of script 0, which FUN_0055a3e0 answers 0, and passes the 0 on.
		var id = ScriptOf( ride ) is { } script ? _scripts.SoundVariable( script.Id, slot ) : 0;

		Set( held, id, level );
		return true;
	}

	/// <summary>
	/// <c>0x006bbb80</c>: <paramref name="id"/>'s low byte matched against the four keys stores the level's low byte in
	/// every slot it matches; a match past the first slot applies the volume and pitch again.
	/// </summary>
	public void Set( Held held, int id, int level )
	{
		var apply = false;

		for ( var i = 0; i < held.Keys.Length; ++i )
		{
			if ( held.Keys[i] != (byte)id )
				continue;

			held.Values[i] = (byte)level;
			apply |= i > 0;
		}

		if ( apply )
			Apply( held );
	}

	public void Fade( object voice )
	{
		if ( voice is Held held && !held.Voice.Ending )
		{
			held.Voice.FadeOut( FadeSeconds );
			Log.Info( $"Car sounds: effect {held.Effect} '{held.Voice.Name}' faded" );
		}
	}

	/// <summary>The volume and pitch from the controllers or at random (<c>0x006bbbe0</c>), the pitch made a rate.</summary>
	private void Apply( Held held )
	{
		held.Pitch = Level( held, 2, held.Header.Pitch );
		held.Voice.SetRate( Rate( held.Pitch ) );

		// A fade runs on to its end whatever the step sets (the original's voice went 135 ms after its fade began,
		// Q202); a volume set here would stop it.
		if ( !held.Voice.Ending )
			held.Voice.SetVolume( Level( held, 1, held.Header.Volume ) / 100f );
	}

	/// <summary>
	/// <c>FUN_006bc090</c> (bit 1) and <c>FUN_006bc170</c> (bit 2): the first controller whose mask has the bit gives
	/// <c>value × range / 100 + low</c>; with neither, a draw from <c>low</c> up to, not including, <c>high</c>.
	/// </summary>
	private int Level( Held held, int bit, (int Low, int High) range )
	{
		var (low, high) = range.High < range.Low ? (range.High, range.Low) : range;
		var span = (uint)(high - low);

		if ( (held.Header.GapByParameter & bit) != 0 )
			return Controlled( held.Values[1], range );

		if ( (held.Header.SecondMask & bit) != 0 )
			return Controlled( held.Values[2], range );

		return span == 0 ? low : _random.Next( (int)span ) + low;
	}

	/// <summary>A controller's value over a range, <c>value × (high − low) / 100 + low</c>, the range put in order first.</summary>
	public static int Controlled( int value, (int Low, int High) range )
	{
		var (low, high) = range.High < range.Low ? (range.High, range.Low) : range;

		return (int)((uint)(value & 0xff) * (uint)(high - low) / 100) + low;
	}

	/// <summary>
	/// The frequency over the sample's own for a pitch in 96ths of an octave - <c>FUN_006d2820</c>'s tables, which a
	/// pitch of <c>p</c> reads one step past: 2^((p + 1) / 96) above nought, 2^((p - 1) / 96) below, 1 at nought.
	/// </summary>
	public static float Rate( int pitch )
		=> pitch == 0 ? 1f : MathF.Pow( 2f, (pitch + Math.Sign( pitch )) / 96f );

	/// <summary>
	/// A car's place for its sound: the original's scale (302.7 record units to a world unit, where its boats are drawn
	/// at 307.2), in this park's cells, at height nought as the original passes it.
	/// </summary>
	private static Vector3 Place( int x, int z )
	{
		var field = ParkGround.Current?.Heightfield;
		var cellX = field?.CellSizeX ?? 10f;
		var cellY = field?.CellSizeY ?? 10f;

		return new Vector3( (float)(x * PlaceScale * cellX / 10.0), (float)(z * PlaceScale * cellY / 10.0), 0f );
	}
}
