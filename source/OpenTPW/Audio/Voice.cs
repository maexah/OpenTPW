namespace OpenTPW;

/// <summary>
/// One sound currently playing, and the handle the game holds it by.
///
/// Fields are read by the mixer on SDL's audio thread and written by the game thread, both under
/// <see cref="Audio"/>'s voice lock, so the setters here are the only safe way to touch one.
/// A voice removes itself once it has run out - or once a fade has taken it to silence - so a
/// one-shot needs nothing done to it after <see cref="Audio.Play"/>.
/// </summary>
public sealed class Voice
{
	private readonly AudioClip _clip;

	/// <summary>Where playback has got to, in source frames. Fractional so a fade can't drift it.</summary>
	private double _frame;

	private float _volume;
	private float _targetVolume;

	/// <summary>How much <see cref="_volume"/> moves per second, or 0 to snap.</summary>
	private float _volumeRate;

	/// <summary>Whether reaching <see cref="_targetVolume"/> should end the voice.</summary>
	private bool _stopWhenFaded;

	private volatile bool _stopped;

	/// <summary>Where in the world this is sounding, or null for a sound with no place in it.</summary>
	private readonly Vector3? _position;

	/// <summary>
	/// How much of this reaches each ear, 0 to 1, and where those are heading as of the last time the
	/// listener moved.
	///
	/// Both are 1 for a sound with no position, which is every sound the game plays today, and 1 and 1
	/// is exactly what the mixer did before there was a pan at all. They never go above 1: the levels
	/// every layer was set to already sum close to full scale - see <see cref="LobbyAudio"/> - so
	/// placing a sound takes the far ear away rather than adding to the near one.
	/// </summary>
	private float _gainLeft = 1f;
	private float _gainRight = 1f;
	private float _targetGainLeft = 1f;
	private float _targetGainRight = 1f;

	/// <summary>How long a hold takes to fade the sound out, and letting it go to fade it back in - just long enough not to click.</summary>
	private const float PauseFadeSeconds = 0.01f;

	/// <summary>Whether it is being held where it has got to - see <see cref="Pause"/>.</summary>
	private bool _paused;

	/// <summary>Where the hold's fade stands, from 1 playing to 0 held. Only the mixer moves it.</summary>
	private float _pauseGain = 1f;

	/// <summary>Frames of the device's output that went by while it was held, which <see cref="Position"/> leaves out.</summary>
	private long _heldFrames;

	/// <summary>Whether this restarts from the top rather than ending. Read by the mixer's cull.</summary>
	internal bool Loop { get; }

	/// <summary>
	/// Which group this belongs to, which is what decides whether <see cref="Audio.Duck"/>
	/// applies to it. Fixed for the life of the voice.
	/// </summary>
	internal AudioBus Bus { get; }

	/// <summary>Where the fade has got to. Read by the mixer's cull.</summary>
	internal float Volume => _volume;

	/// <summary>
	/// False once the sound has finished, been stopped, or faded away.
	///
	/// Read without the lock, because this is what the lobby tests every frame and taking the
	/// mixer's lock sixty times a second to read one bool would be worse than the answer being a
	/// buffer out of date - which is 46ms, and only ever in the direction of noticing late.
	/// </summary>
	public bool Playing => !_stopped;

	/// <summary>What is playing, so the lobby can say what it picked.</summary>
	public string Name => _clip.Name;

	/// <summary>The frame of the device's output its first sample went out on, or -1 until then.</summary>
	private long _startedAtFrame = -1;

	/// <summary>
	/// How long this has been sounding, by the audio device's clock rather than the game's - see
	/// <see cref="Audio.PlayedFrames"/>.
	///
	/// The game's clock is the wrong one to time anything against a sound by. A voice only starts
	/// at the mixer's next buffer, up to 46ms after <see cref="Audio.Play"/>; and the game's clock
	/// stops for a pause and gives up on a stall longer than a tenth of a second, while the sound
	/// carries straight on. Zero until the first buffer; read without a lock, like
	/// <see cref="Playing"/>, and only meaningful while that is true. Time spent held by
	/// <see cref="Pause"/> is left out, a buffer at a time.
	/// </summary>
	public TimeSpan Position
	{
		get
		{
			var started = Interlocked.Read( ref _startedAtFrame );
			var held = Interlocked.Read( ref _heldFrames );

			return started < 0
				? TimeSpan.Zero
				: TimeSpan.FromSeconds( Math.Max( Audio.PlayedFrames - started - held, 0 ) / Audio.SampleRate );
		}
	}

	internal Voice( AudioClip clip, float volume, bool loop, float fadeInSeconds, AudioBus bus,
		Vector3? position )
	{
		_clip = clip;
		Loop = loop;
		Bus = bus;
		_position = position;
		_targetVolume = volume;

		if ( fadeInSeconds > 0f )
		{
			_volume = 0f;
			_volumeRate = volume / fadeInSeconds;
		}
		else
		{
			_volume = volume;
		}
	}

	/// <summary>Stops now. A hard stop on a loud voice clicks - prefer <see cref="FadeOut"/>.</summary>
	public void Stop()
	{
		lock ( Audio.Lock )
			_stopped = true;
	}

	/// <summary>Fades to silence over <paramref name="seconds"/> and then stops.</summary>
	public void FadeOut( float seconds )
	{
		if ( seconds <= 0f )
		{
			Stop();
			return;
		}

		lock ( Audio.Lock )
		{
			_targetVolume = 0f;
			_volumeRate = _volume / seconds;
			_stopWhenFaded = true;
		}
	}

	/// <summary>
	/// Holds it where it has got to, still playing as far as anything asking is concerned, until
	/// <see cref="Resume"/>. It fades out over <see cref="PauseFadeSeconds"/> before it holds, so the
	/// few milliseconds of that fade are the only part heard of what came after the pause. A fade or a
	/// stop still happens while it is held.
	/// </summary>
	public void Pause()
	{
		lock ( Audio.Lock )
			_paused = true;
	}

	/// <summary>Lets a held voice carry on from where it was held, fading back in over <see cref="PauseFadeSeconds"/>.</summary>
	public void Resume()
	{
		lock ( Audio.Lock )
			_paused = false;
	}

	/// <summary>Moves the volume to <paramref name="volume"/>, over <paramref name="seconds"/>.</summary>
	public void SetVolume( float volume, float seconds = 0f )
	{
		volume = volume.Clamp( 0f, 1f );

		lock ( Audio.Lock )
		{
			_targetVolume = volume;
			_volumeRate = seconds <= 0f ? 0f : MathF.Abs( volume - _volume ) / seconds;
			_stopWhenFaded = false;
		}
	}

	/// <summary>
	/// Works out how much of this voice each ear should get, now the listener is where it is.
	///
	/// Runs on the game thread under <see cref="Audio.Lock"/> - see <see cref="Audio.SetListener"/> -
	/// and leaves the mixer nothing to do but read a pair of numbers and step towards them. A voice
	/// with no position is left alone, so a flat sound stays flat however the camera moves.
	/// </summary>
	/// <param name="immediately">
	/// Whether to arrive at the new balance rather than glide to it. True when the voice is being
	/// started, so a placed sound begins at the balance it belongs at instead of sliding there over
	/// its first buffer.
	/// </param>
	internal void Locate( in AudioListener listener, bool immediately )
	{
		if ( _position is not { } position )
			return;

		var pan = listener.PanTo( position );

		// Which side it is on, and how far away it is, are one pair of numbers by the time the mixer
		// sees them - so distance costs the audio thread nothing that the pan was not costing already.
		var attenuation = listener.AttenuationTo( position );

		_targetGainLeft = (1f - MathF.Max( pan, 0f )) * attenuation;
		_targetGainRight = (1f + MathF.Min( pan, 0f )) * attenuation;

		if ( !immediately )
			return;

		_gainLeft = _targetGainLeft;
		_gainRight = _targetGainRight;
	}

	/// <summary>
	/// Adds this voice into an output buffer that already holds whatever mixed before it, and
	/// says whether it is still going.
	///
	/// The volume moves a step per frame rather than a step per buffer. A buffer is 46ms, and a
	/// fade that jumped in 46ms steps would be a staircase of clicks rather than a fade.
	/// </summary>
	/// <param name="duck">
	/// Where the advisor's ducking ramp stands at the first frame of this buffer, and how much it
	/// moves per frame. <see cref="Audio"/> works both out once and hands every voice the same
	/// pair, so the whole mix ducks together rather than each voice reading a value that has
	/// moved on since the voice before it. Speech ignores it.
	/// </param>
	/// <param name="bufferStart">Which frame of the device's output this buffer starts at.</param>
	internal unsafe bool MixInto( float* output, int frames, long bufferStart, float master, float duck, float duckStep )
	{
		if ( _stopped )
			return false;

		if ( _startedAtFrame < 0 )
			Interlocked.Exchange( ref _startedAtFrame, bufferStart );

		var samples = _clip.Samples;
		var channels = _clip.Channels;
		var length = _clip.Frames;
		var step = _volumeRate / Audio.SampleRate;
		var pauseStep = 1f / (PauseFadeSeconds * Audio.SampleRate);
		var held = 0L;

		// The pan glides to wherever the listener last put it and arrives by the end of this buffer,
		// which is the same shape as the ducking ramp above and for the same reason: a buffer is 46ms,
		// and a pan that moved in 46ms steps would be a staircase of clicks rather than a sound going
		// past. The listener moves once a frame, so there is always a fresh target to head for.
		var panStepLeft = frames > 0 ? (_targetGainLeft - _gainLeft) / frames : 0f;
		var panStepRight = frames > 0 ? (_targetGainRight - _gainRight) / frames : 0f;

		if ( Bus == AudioBus.Speech )
		{
			duck = 1f;
			duckStep = 0f;
		}

		for ( int i = 0; i < frames; ++i )
		{
			if ( _volumeRate > 0f )
			{
				if ( _volume < _targetVolume )
					_volume = MathF.Min( _volume + step, _targetVolume );
				else if ( _volume > _targetVolume )
					_volume = MathF.Max( _volume - step, _targetVolume );

				if ( _volume == _targetVolume )
				{
					_volumeRate = 0f;

					if ( _stopWhenFaded )
					{
						_stopped = true;
						return false;
					}
				}
			}

			var frameDuck = duck;
			duck += duckStep;

			var frameLeft = _gainLeft;
			var frameRight = _gainRight;

			_gainLeft += panStepLeft;
			_gainRight += panStepRight;

			// After the volume, so a voice faded or stopped while it is held still goes.
			if ( _paused ? _pauseGain > 0f : _pauseGain < 1f )
				_pauseGain = _paused ? MathF.Max( _pauseGain - pauseStep, 0f ) : MathF.Min( _pauseGain + pauseStep, 1f );

			if ( _pauseGain <= 0f )
			{
				held++;
				continue;
			}

			var at = (int)_frame;

			if ( at >= length )
			{
				if ( !Loop )
				{
					_stopped = true;
					return false;
				}

				// By subtraction rather than by zeroing, so a buffer that ran past the end of the
				// clip carries its overrun into the next time round instead of dropping it.
				_frame -= length;
				at = (int)_frame;

				if ( at >= length )
					at = 0;
			}

			var gain = _volume * master * frameDuck * _pauseGain;

			if ( channels == 1 )
			{
				var value = samples[at] * gain;
				output[i * 2] += value * frameLeft;
				output[(i * 2) + 1] += value * frameRight;
			}
			else
			{
				var index = at * channels;
				output[i * 2] += samples[index] * gain * frameLeft;
				output[(i * 2) + 1] += samples[index + 1] * gain * frameRight;
			}

			_frame += 1.0;
		}

		// Arrive exactly, rather than wherever a buffer's worth of additions landed, so nothing
		// accumulates across buffers.
		_gainLeft = _targetGainLeft;
		_gainRight = _targetGainRight;

		if ( held > 0 )
			Interlocked.Add( ref _heldFrames, held );

		return true;
	}
}
