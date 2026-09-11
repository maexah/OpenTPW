using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// The game's sound output: one SDL2 audio device, and a software mixer feeding it.
///
/// SDL2 rather than anything else because it is already here - the window and the input both
/// come from it through Veldrid - so opening its audio device adds no native dependency on any
/// platform. Veldrid's own binding covers only the window and input side of SDL, hence the
/// handful of imports below.
///
/// The mixing is ours rather than SDL_mixer's. It is a few voices of straight addition, which is
/// less code than binding another library would be, and it keeps the fades and the looping in C#
/// where the lobby can reach them.
///
/// Nothing here throws. A machine with no sound card, a container with no audio server, a test
/// run - all of them end up with <see cref="Ready"/> false and every call below doing nothing,
/// because sound going missing should never be the reason the game will not start.
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
	/// Opens the device. Safe to call more than once; safe to call on a machine with no audio.
	/// </summary>
	public static void Init()
	{
		if ( Ready )
			return;

		try
		{
			if ( SDL_InitSubSystem( SdlInitAudio ) != 0 )
			{
				Log.Warning( $"No audio: SDL_InitSubSystem said '{LastError()}'" );
				return;
			}

			_callback = Mix;

			var wanted = new SdlAudioSpec
			{
				Freq = SampleRate,
				Format = AudioF32Sys,
				Channels = 2,
				Samples = BufferFrames,
				Callback = Marshal.GetFunctionPointerForDelegate( _callback )
			};

			// allowed_changes 0, so SDL converts for us if the hardware wants something else and
			// the format we mix in is the format we get.
			_device = SDL_OpenAudioDevice( IntPtr.Zero, 0, ref wanted, out var got, 0 );

			if ( _device == 0 )
			{
				Log.Warning( $"No audio: SDL_OpenAudioDevice said '{LastError()}'" );
				_callback = null;
				return;
			}

			SDL_PauseAudioDevice( _device, 0 );
			Ready = true;

			Log.Info( $"Audio: {Marshal.PtrToStringAnsi( SDL_GetCurrentAudioDriver() )}, "
				+ $"{got.Freq}Hz {got.Channels}ch, {got.Samples} frame buffer" );
		}
		catch ( DllNotFoundException )
		{
			// SDL2 is here - the window came from it - so this only happens if audio was built
			// out of it. Worth saying, not worth stopping for.
			Log.Warning( "No audio: this SDL2 has no audio support" );
		}
		catch ( Exception e )
		{
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
		SDL_PauseAudioDevice( _device, 1 );
		SDL_CloseAudioDevice( _device );

		lock ( Lock )
			Voices.Clear();

		_callback = null;
		_device = 0;
	}

	/// <summary>
	/// Starts <paramref name="clip"/> and hands back the voice playing it, or null if there is no
	/// device or nothing to play.
	/// </summary>
	/// <param name="volume">0 to 1, before <see cref="MasterVolume"/>.</param>
	/// <param name="loop">Whether it starts again from the top rather than ending.</param>
	/// <param name="fadeInSeconds">How long it takes to reach <paramref name="volume"/>.</param>
	/// <param name="bus">Which group it belongs to, and so whether <see cref="Duck"/> applies.</param>
	public static Voice? Play( AudioClip? clip, float volume = 1f, bool loop = false,
		float fadeInSeconds = 0f, AudioBus bus = AudioBus.Effects )
	{
		if ( !Ready || clip == null || clip.Frames == 0 )
			return null;

		var voice = new Voice( clip, volume.Clamp( 0f, 1f ), loop, fadeInSeconds, bus );

		lock ( Lock )
		{
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
	private static unsafe void Mix( IntPtr userData, IntPtr stream, int lengthInBytes )
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

	private static string LastError() => Marshal.PtrToStringAnsi( SDL_GetError() ) ?? "unknown";

	#region SDL2

	private const uint SdlInitAudio = 0x00000010;

	/// <summary>AUDIO_F32SYS - 32-bit float, host byte order.</summary>
	private const ushort AudioF32Sys = 0x8120;

	[UnmanagedFunctionPointer( CallingConvention.Cdecl )]
	private delegate void SdlAudioCallback( IntPtr userData, IntPtr stream, int lengthInBytes );

	[StructLayout( LayoutKind.Sequential )]
	private struct SdlAudioSpec
	{
		public int Freq;
		public ushort Format;
		public byte Channels;
		public byte Silence;
		public ushort Samples;
		public ushort Padding;
		public uint Size;
		public IntPtr Callback;
		public IntPtr UserData;
	}

	// "SDL2" resolves to SDL2.dll on Windows - Veldrid.SDL2 puts one beside the executable - and
	// to libSDL2.so on Linux and macOS, which is the same library Veldrid already has open.
	private const string Sdl = "SDL2";

	[DllImport( Sdl, CallingConvention = CallingConvention.Cdecl )]
	private static extern int SDL_InitSubSystem( uint flags );

	[DllImport( Sdl, CallingConvention = CallingConvention.Cdecl )]
	private static extern IntPtr SDL_GetError();

	[DllImport( Sdl, CallingConvention = CallingConvention.Cdecl )]
	private static extern IntPtr SDL_GetCurrentAudioDriver();

	[DllImport( Sdl, CallingConvention = CallingConvention.Cdecl )]
	private static extern uint SDL_OpenAudioDevice( IntPtr device, int isCapture,
		ref SdlAudioSpec desired, out SdlAudioSpec obtained, int allowedChanges );

	[DllImport( Sdl, CallingConvention = CallingConvention.Cdecl )]
	private static extern void SDL_PauseAudioDevice( uint device, int pauseOn );

	[DllImport( Sdl, CallingConvention = CallingConvention.Cdecl )]
	private static extern void SDL_CloseAudioDevice( uint device );

	#endregion
}
