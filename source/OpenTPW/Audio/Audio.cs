using NeoVeldrid.Sdl2;

using SdlAudioSpec = Silk.NET.SDL.AudioSpec;
using SdlAudioCallback = Silk.NET.SDL.AudioCallback;

namespace OpenTPW;

/// <summary>
/// The game's sound output: one SDL2 audio device, and a software mixer feeding it.
///
/// SDL2 rather than anything else because it is already here - the window and the input both come
/// from it - so opening its audio device adds no native dependency on any platform. Opened on
/// <see cref="Sdl2Window.SdlInstance"/>, which is the SDL the window itself came out of. That
/// matters: SDL keeps its subsystem state inside whichever copy of the library is asked, so naming
/// one by name instead - as this did, with [DllImport( "SDL2" )] - loaded a second copy out of
/// whatever the machine happened to have, and left the game holding two SDLs that knew nothing of
/// one another. The build ships its own in runtimes\&lt;rid&gt;\native; this uses that one.
///
/// The mixing is ours rather than SDL_mixer's. It is a few voices of straight addition, which is
/// less code than binding another library would be, and it keeps the fades and the looping in C#
/// where the lobby can reach them.
///
/// Nothing here throws. A machine with no sound card, a container with no audio server, a test
/// run - all of them end up with <see cref="Ready"/> false and every call below doing nothing,
/// because sound going missing should never be the reason the game will not start.
///
/// Engine and content: all of this is engine - the device, the mixer, the groups, the duck, and stopping
/// everything between scenes. What plays, and how loud, belongs to whoever plays it.
/// </summary>
public static class Audio
{
	/// <summary>
	/// What the device runs at, and what every clip is resampled to.
	///
	/// 22,050Hz because that is what all but five of the game's 3,739 samples are recorded at -
	/// see <see cref="AudioClip"/> - so at this rate the mixer is a copy rather than an
	/// interpolation, and the five odd ones out are converted once when they load.
	/// </summary>
	public const int SampleRate = 22050;

	/// <summary>Frames per callback: 1024 is about 46ms here, which is a comfortable buffer.</summary>
	private const int BufferFrames = 1024;

	/// <summary>How many sounds can be going at once before the quietest is dropped.</summary>
	private const int MaxVoices = 32;

	/// <summary>Whether there is a device to play through. False leaves every call a no-op.</summary>
	public static bool Ready { get; private set; }

	/// <summary>
	/// Scales everything the mixer puts out. 0 is silence.
	///
	/// The default leaves headroom rather than filling the range. Every layer's level is already
	/// set against a target - see <see cref="LobbyAudio"/> - and this sits under all of them so
	/// that the worst case, everything peaking at once, still lands around 0.6 and the clamp in
	/// <see cref="Mix"/> stays a backstop. Turning it up to 1 is safe; it will clip on a loud
	/// moment rather than hurt anything.
	/// </summary>
	public static float MasterVolume
	{
		get => _masterVolume;
		set => _masterVolume = value.Clamp( 0f, 1f );
	}

	private static float _masterVolume = 0.5f;

	/// <summary>
	/// How far everything that is not <see cref="AudioBus.Speech"/> is turned down, and where
	/// that is heading. 1 is unducked.
	///
	/// The original does this as a straight multiply on the music and effects group volumes the
	/// moment the advisor is given a line, and puts it back the moment his sample ends - see
	/// <see cref="Advisor"/> for the two calls. It steps rather than fades, which on a
	/// sustained park theme is audible as a lurch, so <see cref="Duck"/> ramps instead. The ramp
	/// is the only place this deliberately departs from the original.
	/// </summary>
	private static float _duck = 1f;

	private static float _duckTarget = 1f;

	/// <summary>Movement per second, or 0 to snap.</summary>
	private static float _duckRate;

	/// <summary>Where the ducking ramp stands, for the debug console to report.</summary>
	public static float DuckLevel => _duck;

	/// <summary>How loud each group is, as a multiple of its layers' levels - see <see cref="SetBusVolume"/>. By <see cref="AudioBus"/>.</summary>
	private static readonly float[] BusVolumes = [1f, 1f, 1f];

	/// <summary>
	/// Turns a whole group up or down - the options screen's volumes, through
	/// <see cref="GameOptions.ApplySound"/>. 1 leaves it at the levels its layers were set to, 0
	/// silences it.
	///
	/// Silencing speech also stops the advisor ducking everything else. Sound_ApplyGroupVolumes
	/// (0x0051bd70) only ducks while the speech group's volume is at least 1.
	/// </summary>
	public static void SetBusVolume( AudioBus bus, float volume )
	{
		lock ( Lock )
			BusVolumes[(int)bus] = MathF.Max( volume, 0f );
	}

	private static readonly List<Voice> Voices = new( MaxVoices );

	/// <summary>
	/// Guards <see cref="Voices"/> against the callback, which SDL runs on a thread of its own.
	///
	/// A lock in an audio callback is normally a mistake - if the other side holds it too long
	/// the buffer runs dry and the output clicks. It is safe here because the game thread only
	/// ever holds it to add or remove one list entry, and never to decode, load or allocate.
	/// </summary>
	internal static readonly object Lock = new();

	/// <summary>
	/// Guards the three fields below, which say where output had got to when the latest buffer was
	/// handed over. Separate from <see cref="Lock"/>, which the callback holds for the whole mix, so
	/// the game can read the clock every frame without ever waiting on a mix to finish.
	/// </summary>
	private static readonly object ClockLock = new();

	/// <summary>How many frames had been handed to the device before the latest buffer.</summary>
	private static long _bufferStartFrame;

	/// <summary>How many frames the latest buffer holds.</summary>
	private static int _bufferFrames;

	/// <summary>When the latest buffer was handed over, on <see cref="System.Diagnostics.Stopwatch"/>'s clock.</summary>
	private static long _bufferStartedAt;

	/// <summary>
	/// How far the device has played, in frames since it opened: everything handed over before
	/// the latest buffer, plus as much of that buffer as there has been time to play since.
	///
	/// SDL asks for a buffer when the device is ready to play it, so the moment it asks is close to
	/// the moment that buffer starts to sound. Reading between asks by the real clock keeps this
	/// moving smoothly rather than in 46ms steps, and capping it at the buffer's end keeps a late
	/// ask from running it ahead of what has actually gone out.
	/// </summary>
	internal static double PlayedFrames
	{
		get
		{
			long start;
			long at;
			int frames;

			lock ( ClockLock )
			{
				start = _bufferStartFrame;
				at = _bufferStartedAt;
				frames = _bufferFrames;
			}

			if ( frames == 0 )
				return start;

			var since = System.Diagnostics.Stopwatch.GetElapsedTime( at ).TotalSeconds * SampleRate;
			return start + Math.Min( since, frames );
		}
	}

	private static uint _device;

	/// <summary>
	/// Held for as long as the device is open. The callback is called from native code, so
	/// letting the delegate be collected would leave SDL calling into freed memory.
	/// </summary>
	private static SdlAudioCallback? _callback;

	/// <summary>
	/// The pointer SDL is actually given, kept alongside the delegate it was made from. Silk hands
	/// the function pointer over in the spec; the delegate above is what keeps it pointing at
	/// something.
	/// </summary>
	private static Silk.NET.SDL.PfnAudioCallback _callbackPointer;

	/// <summary>
	/// Opens the device. Safe to call more than once; safe to call on a machine with no audio.
	/// </summary>
	public static unsafe void Init()
	{
		if ( Ready )
			return;

		try
		{
			var sdl = Sdl2Window.SdlInstance;

			if ( sdl.InitSubSystem( Silk.NET.SDL.Sdl.InitAudio ) != 0 )
			{
				Log.Warning( $"No audio: SDL_InitSubSystem said '{sdl.GetErrorS()}'" );
				return;
			}

			_callback = Mix;
			_callbackPointer = new Silk.NET.SDL.PfnAudioCallback( _callback );

			var wanted = new SdlAudioSpec
			{
				Freq = SampleRate,
				Format = (ushort)Silk.NET.SDL.Sdl.AudioF32Sys,
				Channels = 2,
				Samples = BufferFrames,
				Callback = _callbackPointer
			};

			// allowed_changes 0, so SDL converts for us if the hardware wants something else and
			// the format we mix in is the format we get. A null device name asks for the default one.
			var got = new SdlAudioSpec();
			_device = sdl.OpenAudioDevice( (string?)null, 0, ref wanted, ref got, 0 );

			if ( _device == 0 )
			{
				Log.Warning( $"No audio: SDL_OpenAudioDevice said '{sdl.GetErrorS()}'" );
				_callback = null;
				return;
			}

			sdl.PauseAudioDevice( _device, 0 );
			Ready = true;

			Log.Info( $"Audio: {sdl.GetCurrentAudioDriverS()}, "
				+ $"{got.Freq}Hz {got.Channels}ch, {got.Samples} frame buffer" );
		}
		catch ( Exception e )
		{
			// A machine with no sound card, a container with no audio server, an SDL built without
			// audio at all: they all land here, and none of them is a reason not to start the game.
			Log.Warning( $"No audio: {e.Message}" );
		}
	}

	public static void Shutdown()
	{
		if ( !Ready )
			return;

		Ready = false;

		// Pause first: this stops SDL calling the mixer, so the voice list can be emptied without
		// racing it, and the delegate is safe to let go of afterwards.
		var sdl = Sdl2Window.SdlInstance;

		sdl.PauseAudioDevice( _device, 1 );
		sdl.CloseAudioDevice( _device );

		lock ( Lock )
			Voices.Clear();

		_callback = null;
		_device = 0;
	}

	/// <summary>
	/// Fades every voice to silence over <paramref name="fadeSeconds"/> as a scene ends, and leaves the device
	/// open for the next one - what the original's state machine does between scenes with 0x0051bcb0.
	/// </summary>
	public static void StopAll( float fadeSeconds )
	{
		if ( !Ready )
			return;

		lock ( Lock )
		{
			foreach ( var voice in Voices )
				voice.FadeOut( fadeSeconds );
		}
	}

	/// <summary>
	/// Starts <paramref name="clip"/> and hands back the voice playing it, or null if there is no
	/// device or nothing to play.
	/// </summary>
	/// <param name="volume">0 to 1, before <see cref="MasterVolume"/>.</param>
	/// <param name="loop">Whether it starts again from the top rather than ending.</param>
	/// <param name="fadeInSeconds">How long it takes to reach <paramref name="volume"/>.</param>
	/// <param name="bus">Which group it belongs to, and so whether <see cref="Duck"/> applies.</param>
	/// <param name="position">
	/// Where in the world it is sounding, or null to play it flat.
	///
	/// One entry point with an optional position, rather than a positional path alongside this one,
	/// because that is the shape the original had: every sound in the game goes through one call
	/// taking x, y and z, and a sound with no place in the world is played at (0,0,0) - its user
	/// interface sounds literally are. Null rather than a zero here, because (0,0,0) is a real corner
	/// of the lobby rather than a way of saying "nowhere".
	/// </param>
	public static Voice? Play( AudioClip? clip, float volume = 1f, bool loop = false,
		float fadeInSeconds = 0f, AudioBus bus = AudioBus.Effects, Vector3? position = null )
	{
		if ( !Ready || clip == null || clip.Frames == 0 )
			return null;

		var voice = new Voice( clip, volume.Clamp( 0f, 1f ), loop, fadeInSeconds, bus, position );

		lock ( Lock )
		{
			// From where the listener stands now, so a placed sound starts at the balance it belongs
			// at rather than sliding to it across its first buffer.
			voice.Locate( _listener, immediately: true );

			if ( Voices.Count >= MaxVoices )
			{
				// Full. Drop the quietest one that isn't a loop - the loops are the beds and the
				// music, and losing one of those is far more noticeable than losing a one-shot.
				var quietest = -1;

				for ( int i = 0; i < Voices.Count; ++i )
					if ( !Voices[i].Loop && (quietest < 0 || Voices[i].Volume < Voices[quietest].Volume) )
						quietest = i;

				if ( quietest < 0 )
					return null;

				Voices.RemoveAt( quietest );
			}

			Voices.Add( voice );
		}

		return voice;
	}

	/// <summary>
	/// Where the game is heard from. Written only by <see cref="SetListener"/>, on the game thread,
	/// under <see cref="Lock"/>.
	/// </summary>
	private static AudioListener _listener = new( Vector3.Zero, Vector3.Right );

	/// <summary>
	/// How far a placed sound may be before it starts to fade - see
	/// <see cref="AudioListener.AttenuationTo"/> for the law, which is one over the distance.
	///
	/// <para>
	/// <b>Zero, which is what it starts at, turns distance off entirely</b> and leaves a placed sound
	/// panned but never attenuated. So this is the scene's to switch on: the engine supplies the law
	/// and whoever builds the world says at what distance a sound is heard at the level it was given.
	/// The lobby takes it from the camera rig it was composed around - see
	/// <see cref="LobbyCameraMode.NominalDistance"/>.
	/// </para>
	/// <para>
	/// One distance for the whole mix rather than one per sound. A reference really belongs to the
	/// source - a cricket and a klaxon do not carry the same distance - but nothing needs that yet,
	/// and the shape to add later is a value on the voice that falls back to this one.
	/// </para>
	/// </summary>
	public static float ReferenceDistance
	{
		get => _referenceDistance;
		set => _referenceDistance = MathF.Max( value, 0f );
	}

	private static float _referenceDistance;

	/// <summary>
	/// Moves the listener, and works out afresh what every placed voice sounds like from there.
	///
	/// Called once a frame, right after the camera has moved - see <see cref="Level.Render"/>. Doing
	/// every voice in one pass is deliberate: the lock is the one the mixer holds for a whole buffer,
	/// so this takes it once a frame rather than once per voice, and the work inside it is a dot
	/// product each.
	/// </summary>
	/// <param name="forward">
	/// Which way it is looking - <c>Camera.Rotation.Forward</c>. Its ears are worked out from that
	/// rather than passed in, because the obvious thing to pass, <c>Rotation.Right</c>, is rolled by
	/// an arbitrary amount and would be wrong at most camera angles - see
	/// <see cref="AudioListener.Facing"/>.
	/// </param>
	public static void SetListener( Vector3 position, Vector3 forward )
	{
		if ( !Ready )
			return;

		lock ( Lock )
		{
			_listener = AudioListener.Facing( position, forward, _referenceDistance );

			foreach ( var voice in Voices )
				voice.Locate( _listener, immediately: false );
		}
	}

	/// <summary>
	/// Turns everything that is not speech down to <paramref name="level"/>, taking
	/// <paramref name="seconds"/> to get there. <c>Duck( 1f, ... )</c> puts it back.
	/// </summary>
	public static void Duck( float level, float seconds )
	{
		level = level.Clamp( 0f, 1f );

		lock ( Lock )
		{
			_duckTarget = level;
			_duckRate = seconds <= 0f ? 0f : MathF.Abs( level - _duck ) / seconds;

			if ( _duckRate == 0f )
				_duck = level;
		}
	}

	/// <summary>
	/// Fills one buffer. Runs on SDL's audio thread - see <see cref="Lock"/> - so it does no
	/// allocation, no I/O and no logging.
	/// </summary>
	private static unsafe void Mix( void* userData, byte* stream, int lengthInBytes )
	{
		var output = (float*)stream;
		var frames = lengthInBytes / (sizeof( float ) * 2);

		// Stamp the clock before mixing, so a voice starting in this buffer knows which frame of
		// the device's output its first sample lands on - see PlayedFrames and Voice.Position.
		long bufferStart;

		lock ( ClockLock )
		{
			bufferStart = _bufferStartFrame + _bufferFrames;
			_bufferStartFrame = bufferStart;
			_bufferFrames = frames;
			_bufferStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
		}

		for ( int i = 0; i < frames * 2; ++i )
			output[i] = 0f;

		var master = _masterVolume;

		lock ( Lock )
		{
			// Work the ducking ramp out for the whole buffer before any voice is mixed, so every
			// voice is handed the same starting point and the same step. Advancing a shared field
			// inside the voice loop instead would duck each voice by a different amount.
			var duck = _duck;
			var duckStep = 0f;

			if ( _duckRate > 0f )
			{
				var perFrame = _duckRate / SampleRate;
				var reached = _duck + ((_duck < _duckTarget ? perFrame : -perFrame) * frames);

				if ( (_duck < _duckTarget && reached >= _duckTarget)
					|| (_duck > _duckTarget && reached <= _duckTarget) )
				{
					reached = _duckTarget;
					_duckRate = 0f;
				}

				duckStep = frames > 0 ? (reached - _duck) / frames : 0f;
				_duck = reached;
			}

			// The ramp carries on regardless, so the duck is where it should be if speech comes back up.
			if ( BusVolumes[(int)AudioBus.Speech] <= 0f )
			{
				duck = 1f;
				duckStep = 0f;
			}

			for ( int i = Voices.Count - 1; i >= 0; --i )
			{
				var voice = Voices[i];

				if ( !voice.MixInto( output, frames, bufferStart, master * BusVolumes[(int)voice.Bus], duck, duckStep ) )
					Voices.RemoveAt( i );
			}
		}

		// Everything above adds, so the sum can leave the range even when no one voice does.
		// Clamping is the cheap answer and it is what the range is for; the alternative is a
		// limiter, which the four or five voices the lobby runs do not need.
		for ( int i = 0; i < frames * 2; ++i )
			output[i] = output[i] < -1f ? -1f : (output[i] > 1f ? 1f : output[i]);
	}

}
