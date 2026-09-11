namespace OpenTPW;

/// <summary>
/// The advisor: what he says, how he moves while he says it, and the rest of the mix getting out
/// of his way.
///
/// <para>
/// <b>What he says</b> is not his to choose: he says the lines he is handed, in the order they are
/// handed over. Advisor_Update (0x00599880) runs from the lobby's loop (0x0054e6df) and a park's
/// (0x0054f9f9) alike, so he is the same speaker in both, and what differs is who hands him lines. In
/// the lobby that is the front end, which queues them where the original's front end does - see
/// <see cref="UI.FrontEndLines"/>.
/// </para>
/// <para>
/// <b>Engine and content.</b> He is engine: the queue and its flush, the lead-in, the clips he talks
/// through and a line's cue, his cry when cut off, the duck and the pause. Which lines he is given, when,
/// and what a cue does are the scene's content. His cries and his clip numbers are the same for every
/// line, so they stay with him.
/// </para>
/// <para>
/// <b>How a line is spoken.</b> Lines go through a queue, AdvisorQueue_Add (0x005d6110): a line
/// added with flush cuts off whatever he is saying and empties the queue first, one added without
/// waits its turn - see <see cref="StopSpeaking"/> for what cutting him off does. When he is free,
/// Advisor_SayResponse (0x00599050) maps the response to a sample through the table at 0x00768fb8
/// - the sample ids he is handed are copied from it, because it lives in the executable rather than
/// the data - ducks the rest of the mix, and schedules the sample for 800ms later: the clock minus
/// 200, plus 1000.
/// </para>
/// <para>
/// <b>How he moves.</b> The same call picks the animations he talks through, 0x00598b20: clip 14 to
/// start, then random picks from clips 1 to 10 until they cover the line plus half a second, then
/// clip 15 to finish. Clip 14 is counted as taking no time. The first clip starts at once, before
/// his voice does; each following clip starts when the one before it ends. He is busy until the
/// sample has finished and the clips' total has passed, and only then does the duck lift and the
/// queue move on. See <see cref="BuildGestures"/>. Clips 14 and 15 are the ones that move him
/// bodily: 14 brings him up from below the screen and 15 ducks him back down out of view, after
/// which he is hidden until his next line.
/// </para>
/// <para>
/// <b>His mouth.</b> Each sample has a .lip file of timings - see <see cref="LipFile"/>. While a
/// file says he is talking, Advisor_Update (0x00599880) gives him one of his five mouths at random
/// every 100ms; otherwise mouth 1, his mouth at rest.
/// </para>
/// <para>
/// Two deliberate differences. The duck is ramped rather than stepped - see
/// <see cref="Audio.Duck"/> - and held across queued lines, where the original lifts and restores it
/// between them in a frame nobody hears but a ramp would make a swell. And between queued lines he
/// rests out of sight for <see cref="CooldownSeconds"/>, where the original brings him straight
/// back up.
/// </para>
/// <para>
/// <b>Timing.</b> Nothing he does is counted in frames, so he moves the same at any frame rate.
/// His clips sit on a timeline from the moment a line starts - see <see cref="ClipSequence"/> -
/// where the original starts each one on the frame after it notices the last has finished, and so
/// falls a little further behind at every change the slower it runs. His mouth keeps a steady
/// 100ms beat for the same reason - see <see cref="MouthChangeSeconds"/>. And whether his mouth
/// moves at all is read against where his voice has actually got to, by the audio device's clock
/// - see <see cref="Voice.Position"/> - where the original reads its lip file against its own
/// clock from the moment it asked for the sound, which runs ahead of a sound that starts at the
/// mixer's next buffer and keeps going through a stall.
/// </para>
/// <para>
/// <b>Paused.</b> In a park, the game menu (GameMenu_Open, 0x0048c830), a message box (0x0047f020) and
/// the options screen (0x004a3a30) each pause the game through 0x004092a0 as they open. That stops the
/// game's clock (0x00402d90) and holds his sample where it has got to (Advisor_PauseVoice, 0x00598960);
/// his clips, his lead-in and his cue are timed on that clock, so once the game is let go the line
/// carries on from where it stopped. In the lobby none of that happens. All three pause only while
/// 0x00786ba4 says a park is running, and the lobby sets it to 0 as it starts (0x0054e682). So there he
/// talks on over the menu, and a line queued behind the one the options screen quietens starts
/// straight away, over that screen. This departs from the lobby on purpose: he is paused there as a
/// park pauses him (see <see cref="Paused"/>), and is not drawn while paused, so none of those windows
/// shows him.
/// </para>
/// </summary>
public sealed class Advisor : Entity
{
	internal static Advisor? Current { get; private set; }

	/// <summary>
	/// The samples of his own, which no line he is handed leads to. What each line's sample is belongs to
	/// whoever hands it over - see <see cref="UI.FrontEndLines"/> for the lobby's.
	/// </summary>
	private static class Samples
	{
		/// <summary>
		/// What he cries out when he is cut off, one picked at random by Advisor_StopSpeaking
		/// (0x005994e0) - no response leads to them. Three short takes, each under two-fifths of a second
		/// with no words a transcriber could find, which the bank names z_z_ouch1, z_z_ouch2 and
		/// z_z_Ouch3.
		/// </summary>
		public static readonly int[] CutOff = { 639, 640, 641 };
	}

	/// <summary>The clip he starts every line with. Counted as taking no time - see the class remarks.</summary>
	private const int StartClip = 14;

	/// <summary>The clip he finishes every line with.</summary>
	private const int EndClip = 15;

	/// <summary>Clips 1 to this are the ones he talks through.</summary>
	private const int TalkingClips = 10;

	/// <summary>The engine's limit on how many clips one line may string together, clip 15 aside.</summary>
	private const int MostClipsBeforeEnd = 19;

	/// <summary>How much past the end of the sample his clips must reach.</summary>
	private const int GestureMarginMilliseconds = 500;

	/// <summary>
	/// How often he changes mouth while talking. Advisor_Update (0x00599880) sets the next change
	/// for 100ms after the update that makes one, which stretches every beat to the next frame and
	/// so slows him the lower the frame rate; this keeps to 100ms - see <see cref="Time.NextBeat"/>.
	/// </summary>
	private const float MouthChangeSeconds = 0.1f;

	/// <summary>
	/// How loud the advisor is, before <see cref="Audio.MasterVolume"/>.
	///
	/// Set the way the lobby's layers were - see <see cref="LobbyAudio"/> - from the measured
	/// loudness of the bank rather than by ear. The 641 global speech samples have a median RMS
	/// of -17.4 dBFS and a tight spread (-18.5 to -16.3 at the deciles), so one gain suits all of
	/// them. 0.52 puts him at about -23 dBFS in the mix, a little over the park themes' -25.8,
	/// which with the ducking below leaves about 11dB between his voice and the music under it.
	/// </summary>
	private const float SpeechVolume = 0.52f;

	/// <summary>
	/// What everything else drops to while he talks.
	///
	/// data\sound.sam has exactly one ducking setting, <c>SoundInfo.DUCKINGLEVEL 38</c>, and
	/// Sound_ApplyGroupVolumes (0x0051bd70) ducks by <c>(volume * 0x00785914) / 100</c> - a
	/// percentage. That value sits in zeroed memory in the image with no writer, so it is filled in
	/// from config at runtime, and DUCKINGLEVEL is the only candidate the config has.
	/// <b>Inferred, not proven</b>: the file's own header comment says its numbers are detail
	/// levels at which a feature switches on, which would make 38 a threshold instead.
	/// </summary>
	private const float DuckLevel = 0.38f;

	/// <summary>How long the duck takes to come in - see the class remarks on ramping.</summary>
	private const float DuckSeconds = 0.35f;

	/// <summary>Longer coming back, so the theme swells rather than snapping back in.</summary>
	private const float UnduckSeconds = 1.2f;

	/// <summary>From being handed a line to speaking it: the clock minus 200ms, plus 1000ms.</summary>
	private const float LeadInSeconds = 0.8f;

	/// <summary>
	/// How long he stays down out of sight after ducking away before the next queued line brings
	/// him back up.
	///
	/// <b>Not the original's.</b> Its queue, AdvisorQueue_Tick (0x005d5f80), says the next line on
	/// the first tick after he is free, with no wait of any kind - the only timer it has repeats a
	/// line while the queue is empty - so he ducks and pops straight back up. This rest was asked
	/// for, so that a line reads as finished before the next begins. A line that cuts him off does
	/// not wait for it - see <see cref="Add"/>.
	/// </summary>
	private const float CooldownSeconds = 1.5f;

	/// <summary>
	/// How long his voice takes to stop when he is cut off. The original stops it dead; this is only
	/// long enough that stopping a waveform partway through does not click.
	/// </summary>
	private const float CutOffSeconds = 0.01f;

	/// <summary>
	/// How long his voice takes to fade when he is quietened. Advisor_StopQuietly (0x005996d0) hands
	/// Sound_StopFading (0x0051c300) 60, which the sound library takes on to a fade (0x006b8930) in a
	/// unit that was not traced. It is taken as milliseconds.
	/// </summary>
	private const float QuietenSeconds = 0.06f;

	private readonly Random _random = new();

	private SoundCategory? _speech;
	private AdvisorModel? _figure;
	private bool _figureFailed;
	private bool _shown;

	/// <summary>
	/// His own clock, which everything he does is timed on. It follows the game's, but stands still
	/// while he is paused - see <see cref="Paused"/> - so a line picks up exactly where it was left.
	/// </summary>
	private float _now;

	private bool _paused;

	/// <summary>Until when he rests out of sight before another queued line - see <see cref="CooldownSeconds"/>.</summary>
	private float _restUntil = float.NegativeInfinity;

	private Voice? _voice;
	private LipFile? _lips;

	/// <summary>Something to do partway through a line, and how long after the line is given.</summary>
	private readonly record struct Cue( float Seconds, Action Action );

	/// <summary>Samples waiting their turn, oldest first, each with its cue if it has one.</summary>
	private readonly Queue<(int Sample, Cue? Cue)> _queue = new();

	/// <summary>The cue of the line being said, until it is due - see <see cref="Add(int, bool, float, Action)"/>.</summary>
	private Action? _cue;

	private float _cueAt;

	/// <summary>The sample handed over and waiting on <see cref="_speakAt"/>, or 0.</summary>
	private int _pending;

	private float _speakAt;

	/// <summary>When the clips for the current line have all played out, on <see cref="_now"/>'s clock.</summary>
	private float _gesturesEndAt = float.NegativeInfinity;

	private readonly List<int> _gestures = new();

	/// <summary>The clips for the current line, on a timeline that starts at <see cref="_gesturesStartedAt"/>.</summary>
	private ClipSequence _sequence = ClipSequence.Empty;

	private float _gesturesStartedAt;

	private int _mouth = 1;
	private float _nextMouthAt;

	private bool _ducked;

	public Advisor()
	{
		Current = this;

		// Loaded here, during level setup, and not on his first update: a model is entities, and an
		// entity joins Entity.All the moment it is made - which, from inside an update, is the list
		// Level.Update is walking at the time.
		LoadFigure();
	}

	protected override void OnUpdate()
	{
		if ( !Audio.Ready )
			return;

		_speech ??= new SoundCategory( "global", "global/Speech", "speech" );

		// Held where he is: his clips, his lead-in, his cue and his queue all wait along with his voice.
		if ( _paused )
			return;

		_now += Time.Delta;

		if ( _pending != 0 && _now >= _speakAt )
		{
			_voice = _speech.Play( _pending, SpeechVolume, respectDelay: false, bus: AudioBus.Speech );
			_pending = 0;
		}

		Animate();

		if ( _cue != null && _now >= _cueAt )
		{
			var cue = _cue;
			_cue = null;
			cue();
		}

		if ( Busy )
			return;

		if ( _queue.Count > 0 )
		{
			// Not while he is still ducking away, and not until he has rested once he has.
			if ( !_shown && _now >= _restUntil )
			{
				var (sample, cue) = _queue.Dequeue();
				Speak( sample, cue );
			}
		}
		else
		{
			Release();
		}
	}

	protected override void OnRenderOverlay()
	{
		if ( _shown && !_paused && _figure != null )
			_figure.Draw( Screen.Aspect );
	}

	/// <summary>
	/// Busy until the sample is over and so are the clips he talks through - Advisor_Update only
	/// lets go once both have passed.
	/// </summary>
	private bool Busy => _pending != 0 || _voice is { Playing: true } || _now < _gesturesEndAt;

	/// <summary>
	/// Whether he can say anything at all: the options have him switched on, and there is an audio device
	/// and a speech bank to say it from.
	/// </summary>
	internal bool CanSpeak => GameOptions.Current.Advisor && Audio.Ready && _speech is not { IsValid: false };

	/// <summary>
	/// Whether he is held where he is: his voice stops where it has got to, and nothing he is doing or
	/// waiting to do moves on until he is let go, when all of it carries on from the same point. He is
	/// not drawn while held. The front end holds him while the game menu, a message box or the options
	/// screen is open - see the class remarks.
	/// </summary>
	internal bool Paused
	{
		get => _paused;
		set
		{
			if ( _paused == value )
				return;

			_paused = value;

			if ( value )
				_voice?.Pause();
			else
				_voice?.Resume();

			Log.Info( value ? "Advisor: paused" : "Advisor: carrying on" );
		}
	}

	/// <summary>
	/// AdvisorQueue_Add: queues <paramref name="sample"/> behind whatever is waiting, or with
	/// <paramref name="flush"/> cuts him off and throws the queue away first - see
	/// <see cref="StopSpeaking"/>.
	///
	/// A flushed line is meant to be heard now, so it skips the rest between lines and he comes
	/// straight back up with it.
	/// </summary>
	private void Add( int sample, bool flush, Cue? cue )
	{
		if ( flush )
		{
			_queue.Clear();
			StopSpeaking();
			_restUntil = float.NegativeInfinity;
		}

		if ( sample > 0 )
			_queue.Enqueue( (sample, cue) );
	}

	internal void Add( int sample, bool flush ) => Add( sample, flush, cue: null );

	/// <summary>
	/// <see cref="Add(int, bool)"/>, with <paramref name="cue"/> called <paramref name="cueSeconds"/> after he is
	/// given the line - the one timed event a row of the gesture table (0x0076dc18) can hold, flag 0x400000, which
	/// Advisor_Update (0x00599880) counts from the clock minus 200 that the line was given at (0x00598bf0). A line
	/// that cannot be said still has its cue called, straight away; a line cut off before the cue loses it.
	/// </summary>
	internal void Add( int sample, bool flush, float cueSeconds, Action cue ) => Add( sample, flush, new Cue( cueSeconds, cue ) );

	/// <summary>Plays one sample now, cutting off anything else - for auditioning from the console.</summary>
	internal void Say( int sample )
	{
		if ( sample <= 0 )
		{
			Hush();
			return;
		}

		Add( sample, flush: true );
	}

	/// <summary>
	/// AdvisorQueue_Clear (0x005d6070): cuts him off, forgets what was queued, and lets the mix back
	/// up - see <see cref="StopSpeaking"/>.
	/// </summary>
	internal void Hush()
	{
		_queue.Clear();
		StopSpeaking();
		Release();
	}

	/// <summary>
	/// Advisor_StopSpeaking (0x005994e0), which everything that cuts him off goes through.
	///
	/// His voice stops - with a plain stop in the original (0x0051c2c0), not the fading one that its
	/// quiet stop (0x005996d0), which opening the options screen uses, can choose - and he leaves the
	/// screen at once rather than ducking away, through its "Kill advisor" call (0x00429d60).
	/// Whatever was still to come in the line goes with it.
	///
	/// If his voice had started, he cries out: one of <see cref="Samples.CutOff"/> at random, through
	/// the same category and the same call as his lines. Not otherwise. The original holds no
	/// handle on the sound through the lead-in, since the sample is only started once that has
	/// passed, and Advisor_Update (0x00599880) lets go of the handle on the frame the sample
	/// finishes. So a line still waiting to be said goes quietly, and so does one already said
	/// while he finishes the clips he talks through.
	/// </summary>
	private void StopSpeaking()
	{
		var busy = Busy;
		var sounding = Dismiss( CutOffSeconds );

		if ( !sounding || _speech is not { IsValid: true } )
		{
			if ( busy )
				Log.Info( "Advisor: cut off with nothing sounding, quietly" );

			return;
		}

		var cry = Samples.CutOff[_random.Next( Samples.CutOff.Length )];
		_speech.Play( cry, SpeechVolume, respectDelay: false, bus: AudioBus.Speech );

		Log.Info( $"Advisor: cut off mid-line, crying out with sample {cry}" );
	}

	/// <summary>
	/// Advisor_StopQuietly (0x005996d0), which opening the options screen calls with 1: everything
	/// <see cref="StopSpeaking"/> does but the cry, with his voice faded (Sound_StopFading, 0x0051c300)
	/// rather than cut. What is still queued stays queued.
	/// </summary>
	internal void StopQuietly()
	{
		var busy = Busy;
		Dismiss( QuietenSeconds );

		if ( busy )
			Log.Info( "Advisor: quietened" );
	}

	/// <summary>
	/// What being cut off and being quietened share: his voice goes, over <paramref name="fadeSeconds"/>,
	/// and so does he, with whatever was still to come in the line. Says whether his voice was sounding.
	/// </summary>
	private bool Dismiss( float fadeSeconds )
	{
		var sounding = _voice is { Playing: true };

		_pending = 0;
		_voice?.FadeOut( fadeSeconds );
		_voice = null;
		_lips = null;
		_gesturesEndAt = float.NegativeInfinity;
		_cue = null;
		_shown = false;

		return sounding;
	}

	/// <summary>
	/// Advisor_SayResponse: ducks the mix now, starts him moving now, and speaks the sample after
	/// the lead-in.
	///
	/// 80 of the 641 samples in the bank are a 315-byte stub that is not valid MPEG - response ids
	/// with nothing recorded - and those simply come back silent, which ends the line at once.
	/// </summary>
	private void Speak( int sample, Cue? cue )
	{
		// Advisor_SayResponse (0x00599050) says nothing at all while the options have him switched off.
		if ( !GameOptions.Current.Advisor || _speech is not { IsValid: true } )
		{
			// A line that cannot be said still has its consequences.
			cue?.Action();
			return;
		}

		_pending = sample;
		_speakAt = _now + LeadInSeconds;

		_cue = cue?.Action;
		_cueAt = _now + (cue?.Seconds ?? 0f);

		var sampleMilliseconds = (int)_speech.Length( sample ).TotalMilliseconds;
		var gesturesMilliseconds = BuildGestures( sampleMilliseconds + GestureMarginMilliseconds );

		// The clips' total is counted from the moment the sample is due, not from now.
		_gesturesEndAt = _speakAt + (gesturesMilliseconds / 1000f);

		// What he is seen doing is the clips at their full lengths, clip 14 included, starting now.
		_sequence = new ClipSequence( _gestures, clip => _figure?.ClipMilliseconds( clip ) ?? 0 );
		_gesturesStartedAt = _now;

		_lips = LipFile.TryLoad( $"global/Speech/lips/sp_{sample:000}.lip", out var lips ) ? lips : null;

		if ( _figure != null )
			_shown = true;

		if ( !_ducked )
		{
			_ducked = true;
			Audio.Duck( DuckLevel, DuckSeconds );
		}

		Log.Info( $"Advisor: sample {sample} in {LeadInSeconds:0.0}s through clips [{string.Join( ", ", _gestures )}] "
			+ $"({gesturesMilliseconds}ms), {_queue.Count} more queued" );
	}

	/// <summary>
	/// The clips he talks through, as 0x00598b20 picks them, and how long they take in all.
	///
	/// Clip 14 first. Then while the line is not yet covered, and fewer than nineteen clips are in:
	/// roll one of clips 1 to 10; if what is left of the line is shorter than the rolled clip, take
	/// instead the first of clips 1 to 10 that is at least as long as what is left (or clip 10 if
	/// none is). Clip 15 last. Clip 14's length is set to zero before any of this, so it covers
	/// nothing and adds nothing to the total.
	/// </summary>
	private int BuildGestures( int toCover )
	{
		_gestures.Clear();
		_gestures.Add( StartClip );

		var total = 0;

		while ( toCover >= 1 && _gestures.Count < MostClipsBeforeEnd )
		{
			var clip = _random.Next( TalkingClips ) + 1;

			if ( toCover < ClipMilliseconds( clip ) )
			{
				clip = TalkingClips;

				for ( int candidate = 1; candidate <= TalkingClips; ++candidate )
				{
					if ( toCover <= ClipMilliseconds( candidate ) )
					{
						clip = candidate;
						break;
					}
				}
			}

			var length = ClipMilliseconds( clip );

			// A clip that takes no time cannot cover anything, and the engine would stop at its
			// nineteen-clip limit; stop now rather than fill the list with it.
			if ( length <= 0 )
				break;

			_gestures.Add( clip );
			total += length;
			toCover -= length;
		}

		_gestures.Add( EndClip );
		total += ClipMilliseconds( EndClip );

		return total;
	}

	private int ClipMilliseconds( int clip )
		=> clip == StartClip || _figure == null ? 0 : _figure.ClipMilliseconds( clip );

	/// <summary>Plays his clips one after another and moves his mouth - Advisor_Update's other half.</summary>
	private void Animate()
	{
		if ( _figure == null || !_shown )
			return;

		// Each clip starts when the one before it ends and the last holds its final frame - asked of
		// the timeline by how long ago the line began, so no frame rate can leave him behind.
		var sinceGesturesStarted = _now - _gesturesStartedAt;

		if ( _sequence.TryLocate( sinceGesturesStarted, out var clip, out var intoClip ) )
			_figure.Pose( clip, intoClip );

		// Timed by where the sound itself has got to, not by the game's clock - see Voice.Position.
		var talking = _voice is { Playing: true } voice && _lips != null
			&& _lips.IsTalking( voice.Position );

		if ( !talking )
		{
			SetMouth( 1 );
		}
		else if ( _now >= _nextMouthAt )
		{
			SetMouth( _random.Next( 5 ) + 1 );
			_nextMouthAt = Time.NextBeat( _nextMouthAt, MouthChangeSeconds, _now );
		}

		// Clip 15 takes him down out of sight - its position keys drop his head and body well below
		// the bottom of the screen - and once it has played out he is gone, rather than holding his
		// last pose. He rests there before the next queued line brings him back up with clip 14.
		if ( !Busy && _sequence.Count > 0 && _sequence.IsFinished( sinceGesturesStarted ) )
		{
			_shown = false;
			_restUntil = _now + CooldownSeconds;
		}
	}

	private void SetMouth( int shape )
	{
		if ( shape == _mouth )
			return;

		_mouth = shape;
		_figure?.ShowMouth( shape );
	}

	private void LoadFigure()
	{
		if ( _figure != null || _figureFailed )
			return;

		try
		{
			_figure = new AdvisorModel( "global/advisor/Advisor.md2", "global/advisor/textures" );
		}
		catch ( Exception e )
		{
			_figureFailed = true;
			Log.Warning( $"Advisor: model would not load - {e.Message}" );
		}
	}

	private void Release()
	{
		if ( !_ducked )
			return;

		_ducked = false;
		Audio.Duck( 1f, UnduckSeconds );
	}

	/// <summary>What the advisor is doing, for the debug console.</summary>
	internal string State()
	{
		if ( !Audio.Ready )
			return "no audio device";

		var what = _pending != 0
			? $"about to say {_pending}"
			: _voice is { Playing: true } ? $"saying {_voice.Name}" : Busy ? "finishing"
			: _queue.Count > 0 ? (_shown ? "ducking" : "resting") : "quiet";

		var clip = _sequence.TryLocate( _now - _gesturesStartedAt, out var current, out _ ) ? current : 0;

		return $"{what} paused={_paused} queued={_queue.Count} duck={Audio.DuckLevel:0.00} shown={_shown} clip={clip} mouth={_mouth} "
			+ $"samples={_speech?.EffectIds.Count() ?? 0}";
	}
}
