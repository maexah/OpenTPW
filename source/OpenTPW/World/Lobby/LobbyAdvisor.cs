namespace OpenTPW;

/// <summary>
/// The advisor in the lobby: what he says, how he moves while he says it, and the rest of the mix
/// getting out of his way.
///
/// <para>
/// <b>What he says.</b> On entering the front end with no player chosen, FrontEnd_Init
/// (0x005d5970) shows the four-slot player screen, FrontEnd_ShowPlayerSlots (0x004a6580). That
/// counts the slots in use with Players_CountUsedSlots (0x005c7bb0) and queues advisor lines on
/// it: with no slot in use, response 390 and then 391 behind it; otherwise response 398. There is
/// no save system yet, so no slot is ever in use, and the first of those plays on every launch -
/// the original's own test with nothing to find.
/// </para>
/// <para>
/// <b>How a line is spoken.</b> Lines go through a queue, AdvisorQueue_Add (0x005d6110): a line
/// added with flush cuts off whatever he is saying and empties the queue first, one added without
/// waits its turn. When he is free, Advisor_SayResponse (0x00599050) maps the response to a sample
/// through the table at 0x00768fb8 - the ids here are copied from it, because it lives in the
/// executable rather than the data - ducks the rest of the mix, and schedules the sample for 800ms
/// later: the clock minus 200, plus 1000.
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
/// </summary>
public sealed class LobbyAdvisor : Entity
{
	internal static LobbyAdvisor? Current { get; private set; }

	/// <summary>
	/// The samples behind the lobby's response ids, from the original's response table
	/// (0x00768fb8). Which line is which was confirmed by listening, not inferred from the ids.
	/// </summary>
	private static class Samples
	{
		/// <summary>Response 390: "Welcome to Sim Theme Park! I'm the advisor around here..."</summary>
		public const int Welcome = 465;

		/// <summary>Response 391: "...I don't even know your name! ...click on the New Player button."</summary>
		public const int AskForName = 466;

		/// <summary>Response 398: "Welcome to Sim Theme Park! Don't I know you?..."</summary>
		public const int WelcomeBack = 471;

		/// <summary>
		/// Response 570: "First type your name into the text box, then click a button to choose an
		/// Instant Action or a Full Simulation game. When you're finished, click the checked button..."
		/// </summary>
		public const int NewPlayerDialog = 588;

		/// <summary>
		/// Response 393: "...this area is called the lobby... You need golden keys to enter the parks.
		/// Here's one now to get you started. This key will get you into the Halloween World and Lost
		/// Kingdom parks right away..."
		/// </summary>
		public const int LobbyTour = 468;
	}

	/// <summary>What a player with no saved game hears, in order - responses 390 and 391.</summary>
	private static readonly int[] NewPlayerLines = { Samples.Welcome, Samples.AskForName };

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

	private readonly Random _random = new();

	private SoundCategory? _speech;
	private AdvisorModel? _figure;
	private bool _figureFailed;
	private bool _shown;

	/// <summary>Until when he rests out of sight before another queued line - see <see cref="CooldownSeconds"/>.</summary>
	private float _restUntil = float.NegativeInfinity;

	private Voice? _voice;
	private LipFile? _lips;

	/// <summary>
	/// When the key in his tour of the lobby is handed over, counted from the moment he is given the
	/// line.
	///
	/// Response 393's row of the gesture table at 0x0076dc18 (row 12) holds no clips - he talks through
	/// random ones as usual - but one timed event, flag 0x400000 with 17000ms, counted from the clock
	/// minus 200 that the line was given at (0x00598bf0). When it comes due, Advisor_Update
	/// (0x00599880) plays goldkey (effect 198 of the interface's sounds), starts a burst of particles
	/// (effect 87 of data\Particle\Tp2.plb) and refreshes the lobby panel (0x004b9340), which is the
	/// moment the new key shows up on it - some way into "Here's one now to get you started". What
	/// the cue does is the caller's; the front end plays goldkey and refreshes the panel, and no
	/// particles are drawn yet.
	/// </summary>
	private const float TourKeySeconds = 16.8f;

	/// <summary>Something to do partway through a line, and how long after the line is given.</summary>
	private readonly record struct Cue( float Seconds, Action Action );

	/// <summary>Samples waiting their turn, oldest first, each with its cue if it has one.</summary>
	private readonly Queue<(int Sample, Cue? Cue)> _queue = new();

	/// <summary>The cue of the line being said, until it is due - see <see cref="TourKeySeconds"/>.</summary>
	private Action? _cue;

	private float _cueAt;

	/// <summary>The sample handed over and waiting on <see cref="_speakAt"/>, or 0.</summary>
	private int _pending;

	private float _speakAt;

	/// <summary>When the clips for the current line have all played out, on <see cref="Time.Now"/>'s clock.</summary>
	private float _gesturesEndAt = float.NegativeInfinity;

	private readonly List<int> _gestures = new();

	/// <summary>The clips for the current line, on a timeline that starts at <see cref="_gesturesStartedAt"/>.</summary>
	private ClipSequence _sequence = ClipSequence.Empty;

	private float _gesturesStartedAt;

	private int _mouth = 1;
	private float _nextMouthAt;

	private bool _ducked;

	/// <summary>Whether the front-end greeting has been dealt with this session.</summary>
	private bool _greeted;

	public LobbyAdvisor()
	{
		Current = this;

		// Loaded here, during level setup, and not on his first update: a model is entities, and an
		// entity joins Entity.All the moment it is made - which, from inside an update, is the list
		// Level.Update is walking at the time.
		LoadFigure();
	}

	/// <summary>
	/// How many of the four player slots hold a saved player - what Players_CountUsedSlots
	/// answers in the original.
	///
	/// Always 0, because there is no save system yet. This is the one line to change when saves
	/// arrive; everything that depends on it is already the original's behaviour.
	/// </summary>
	private static int UsedPlayerSlots => 0;

	protected override void OnUpdate()
	{
		if ( !Audio.Ready )
			return;

		_speech ??= new SoundCategory( "global", "global/Speech", "speech" );

		if ( !_greeted )
		{
			_greeted = true;
			Greet();
		}

		if ( _pending != 0 && Time.Now >= _speakAt )
		{
			_voice = _speech.Play( _pending, SpeechVolume, respectDelay: false, bus: AudioBus.Speech );
			_pending = 0;
		}

		Animate();

		if ( _cue != null && Time.Now >= _cueAt )
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
			if ( !_shown && Time.Now >= _restUntil )
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
		if ( _shown && _figure != null )
			_figure.Draw( Screen.Aspect );
	}

	/// <summary>
	/// Busy until the sample is over and so are the clips he talks through - Advisor_Update only
	/// lets go once both have passed.
	/// </summary>
	private bool Busy => _pending != 0 || _voice is { Playing: true } || Time.Now < _gesturesEndAt;

	/// <summary>
	/// What FrontEnd_ShowPlayerSlots says: the new-player greeting when no slot is in use, the
	/// welcome back otherwise.
	/// </summary>
	internal void Greet()
	{
		if ( UsedPlayerSlots == 0 )
		{
			Add( NewPlayerLines[0], flush: true );

			for ( int i = 1; i < NewPlayerLines.Length; ++i )
				Add( NewPlayerLines[i], flush: false );
		}
		else
		{
			Add( Samples.WelcomeBack, flush: true );
		}
	}

	/// <summary>
	/// What the new player dialog opens with (0x004a6e40): how to fill it in, cutting in on whatever he
	/// was saying.
	/// </summary>
	internal void ExplainNewPlayer() => Add( Samples.NewPlayerDialog, flush: true );

	/// <summary>
	/// What FrontEnd_ClosePlayerSlots (0x004a6a50) has him say once a new player has been made: his
	/// tour of the lobby, and the golden key he hands over, which <paramref name="keyHandedOver"/> is
	/// called for - see <see cref="TourKeySeconds"/>.
	/// </summary>
	internal void GiveLobbyTour( Action keyHandedOver )
		=> Add( Samples.LobbyTour, flush: true, new Cue( TourKeySeconds, keyHandedOver ) );

	/// <summary>Whether he can say anything at all - there is an audio device and a speech bank to say it from.</summary>
	internal bool CanSpeak => Audio.Ready && _speech is not { IsValid: false };

	/// <summary>
	/// AdvisorQueue_Add: queues <paramref name="sample"/> behind whatever is waiting, or with
	/// <paramref name="flush"/> stops him and throws the queue away first.
	///
	/// A flushed line is meant to be heard now, so it skips the rest between lines: he is taken
	/// off the screen, as Advisor_StopSpeaking (0x005994e0) does through its "Kill advisor" call,
	/// and comes straight back up with it.
	/// </summary>
	private void Add( int sample, bool flush, Cue? cue )
	{
		if ( flush )
		{
			_queue.Clear();
			StopCurrent();
			_shown = false;
			_restUntil = float.NegativeInfinity;
		}

		if ( sample > 0 )
			_queue.Enqueue( (sample, cue) );
	}

	internal void Add( int sample, bool flush ) => Add( sample, flush, cue: null );

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
	/// Stops him mid-sentence, forgets what was queued, and lets the mix back up. He leaves the
	/// screen at once rather than ducking away, as the original's stop paths take him off it
	/// (0x005994e0 and 0x005996d0, through their "Kill advisor" call). The original's interruption
	/// noise - see Advisor_StopSpeaking - is not played yet.
	/// </summary>
	internal void Hush()
	{
		_queue.Clear();
		StopCurrent();
		Release();
		_shown = false;
	}

	private void StopCurrent()
	{
		_pending = 0;
		_voice?.FadeOut( 0.2f );
		_voice = null;
		_lips = null;
		_gesturesEndAt = float.NegativeInfinity;
		_cue = null;
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
		if ( _speech is not { IsValid: true } )
		{
			// A line that cannot be said still has its consequences.
			cue?.Action();
			return;
		}

		_pending = sample;
		_speakAt = Time.Now + LeadInSeconds;

		_cue = cue?.Action;
		_cueAt = Time.Now + (cue?.Seconds ?? 0f);

		var sampleMilliseconds = (int)_speech.Length( sample ).TotalMilliseconds;
		var gesturesMilliseconds = BuildGestures( sampleMilliseconds + GestureMarginMilliseconds );

		// The clips' total is counted from the moment the sample is due, not from now.
		_gesturesEndAt = _speakAt + (gesturesMilliseconds / 1000f);

		// What he is seen doing is the clips at their full lengths, clip 14 included, starting now.
		_sequence = new ClipSequence( _gestures, clip => _figure?.ClipMilliseconds( clip ) ?? 0 );
		_gesturesStartedAt = Time.Now;

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
		var sinceGesturesStarted = Time.Now - _gesturesStartedAt;

		if ( _sequence.TryLocate( sinceGesturesStarted, out var clip, out var intoClip ) )
			_figure.Pose( clip, intoClip );

		// Timed by where the sound itself has got to, not by the game's clock - see Voice.Position.
		var talking = _voice is { Playing: true } voice && _lips != null
			&& _lips.IsTalking( voice.Position );

		if ( !talking )
		{
			SetMouth( 1 );
		}
		else if ( Time.Now >= _nextMouthAt )
		{
			SetMouth( _random.Next( 5 ) + 1 );
			_nextMouthAt = Time.NextBeat( _nextMouthAt, MouthChangeSeconds, Time.Now );
		}

		// Clip 15 takes him down out of sight - its position keys drop his head and body well below
		// the bottom of the screen - and once it has played out he is gone, rather than holding his
		// last pose. He rests there before the next queued line brings him back up with clip 14.
		if ( !Busy && _sequence.Count > 0 && _sequence.IsFinished( sinceGesturesStarted ) )
		{
			_shown = false;
			_restUntil = Time.Now + CooldownSeconds;
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

		var clip = _sequence.TryLocate( Time.Now - _gesturesStartedAt, out var current, out _ ) ? current : 0;

		return $"{what} queued={_queue.Count} duck={Audio.DuckLevel:0.00} shown={_shown} clip={clip} mouth={_mouth} "
			+ $"samples={_speech?.EffectIds.Count() ?? 0}";
	}
}
