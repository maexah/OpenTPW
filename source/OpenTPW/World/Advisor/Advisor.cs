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
/// 0x00786ba4, the pause-permission gate, is 1, and the lobby sets it to 0 as it starts (0x0054e682). So there he
/// talks on over the menu, and a line queued behind the one the options screen quietens starts
/// straight away, over that screen. This departs from the lobby on purpose: he is paused there as a
/// park pauses him (see <see cref="Paused"/>).
/// </para>
/// <para>
/// <b>He is still drawn while he is held.</b> A pause does not take him off the screen:
/// the original's pause (0x004092a0) calls Advisor_PauseVoice (0x00598960) and the game clock's stop
/// and nothing else that touches him, and the one call that removes his model - Advisor_KillModel
/// (0x00429d60) - has exactly three callers, Advisor_StopSpeaking twice and Advisor_StopQuietly, none of
/// which is the pause. So the original holds him mid-gesture behind an open menu, and so does this, in
/// both scenes. What still takes him away is <see cref="Dismiss"/> clearing <c>_shown</c>, which is
/// reached by those same two stops - so the options screen, which quietens him, does remove him, exactly
/// as the original's third caller does.
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

	/// <summary>The model slot of the park's own advisor; slot 0 is the one every scene has.</summary>
	internal const int ThemeSlot = 1;

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
	/// How long his voice takes to fade when he is quietened. <b>A deviation:</b> Advisor_StopQuietly (0x005996d0) hands
	/// Sound_StopFading (0x0051c300), which at sound detail above SoundInfo.FADETOGGLE (30) takes 60 percent off the
	/// voice's volume on each service pass and otherwise cuts it (docs/exe/audio.md, "The fade"; Q509). This flat
	/// 0.06 s ramp stands in for both.
	/// </summary>
	private const float QuietenSeconds = 0.06f;

	private readonly Random _random = new();

	/// <summary>
	/// The speech category, made the first time there is a device to play it on and kept from then on, as the
	/// original registers cat_speech once, as the game starts (0x0051eae0) - so the advisor of the next scene
	/// speaks from the same one.
	/// </summary>
	private static SoundCategory? _speech;
	private AdvisorModel? _figure;
	private bool _figureFailed;

	/// <summary>
	/// The park's own advisor, model slot 1: <c>Advisor_</c> and the theme's first three letters, from the
	/// level's <c>advisor.wad</c> (<c>FUN_00457a90</c>). The lobby has none.
	/// </summary>
	private AdvisorModel? _themeFigure;

	/// <summary>The theme's costume, what the prop -1 puts on (<see cref="AdvisorProps.CostumeOf"/>).</summary>
	private readonly (int AddOn, int SubOff)[] _costume;

	/// <summary>The model the line being said plays on, by its response's slot (the response table's <c>+0x10</c>).</summary>
	private AdvisorModel? _speaker;

	private int _slot;
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

	/// <summary>The two props of the line he last began, the response table's <c>+0x14</c> and <c>+0x18</c>.</summary>
	private (int First, int Second) _wearing;

	/// <summary>The cue of the line being said, until it is due - see <see cref="Add(int, bool, float, Action)"/>.</summary>
	private Action? _cue;

	private float _cueAt;

	/// <summary>The sample handed over and waiting on <see cref="_speakAt"/>, or 0.</summary>
	private int _pending;

	private float _speakAt;

	/// <summary>When the clips for the current line have all played out, on <see cref="_now"/>'s clock.</summary>
	private float _gesturesEndAt = float.NegativeInfinity;

	private readonly List<int> _gestures = new();

	/// <summary>Each clip's flags, the clip word's <c>0x7f0000</c>: nought for the random chain's.</summary>
	private readonly List<int> _clipFlags = new();

	/// <summary>Which clip of the line has had its start, -1 before the first.</summary>
	private int _startedIndex = -1;

	/// <summary>Whether the clip playing carries 0x10000: no add-on, no mouth, no head.</summary>
	private bool _faceAway;

	/// <summary>UI particle channel 0, the golden ticket's glints, which the clip words' flags set - see <see cref="SetGlints"/>.</summary>
	private readonly UI.UiParticleChannel _glints = new();

	/// <summary>The clips for the current line, on a timeline that starts at <see cref="_gesturesStartedAt"/>.</summary>
	private ClipSequence _sequence = ClipSequence.Empty;

	private float _gesturesStartedAt;

	private int _mouth = 1;
	private float _nextMouthAt;

	private bool _ducked;

	/// <param name="theme">The park's theme, whose own advisor is model slot 1; null in the lobby, which has none.</param>
	/// <param name="balance">The park's balance, which names the theme's costume.</param>
	/// <param name="parkLight">The park's light, whose ambient and quality light him; null in the lobby (<see cref="AdvisorModel.LightFor"/>).</param>
	public Advisor( string? theme = null, ParkBalance? balance = null, ParkLight? parkLight = null )
	{
		Current = this;
		_costume = AdvisorProps.CostumeOf( key => balance?.Int( key ) ?? 0 );

		// Loaded here, during level setup, and not on his first update: a model is entities, and an
		// entity joins Entity.All the moment it is made - which, from inside an update, is the list
		// Level.Update is walking at the time.
		LoadFigure();

		if ( theme != null )
			LoadThemeFigure( theme.ToLowerInvariant() );

		var records = RecordsLoaded( _figure != null, _themeFigure != null );
		(_light, _lightStrength) = AdvisorModel.LightFor( parkLight, records, Level.Lighting( Level.DetailFile ) );
		Log.Info( $"Advisor: lit by ambient {_light.Ambient.X:0.000} {_light.Ambient.Y:0.000} {_light.Ambient.Z:0.000} and {records} record "
			+ $"light{(records == 1 ? "" : "s")}, strength {_lightStrength}{(_light.Lit ? "" : ", LIGHTING 0: every vertex full")}" );
	}

	/// <summary>
	/// How many advisor records the original has loaded, each filing a white light: the global fly's, and the theme's where a
	/// park loads one (<c>Advisor_LoadModel</c> <c>0x00429ba0</c>; <see cref="AdvisorModel.LightFor"/>).
	/// </summary>
	internal static int RecordsLoaded( bool global, bool theme ) => (global ? 1 : 0) + (theme ? 1 : 0);

	/// <summary>His light, as the original lights him (<see cref="AdvisorModel.LightFor"/>).</summary>
	private readonly ParkLight _light;

	/// <summary>How strong his white light is: one for each advisor record loaded, each filing its own light.</summary>
	private readonly float _lightStrength;

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
			Log.Info( $"Advisor: sample={_pending} voicePlaying={_voice is { Playing: true }}" );
			_pending = 0;
		}

		Animate();

		// Placed every frame after a clip's start, on his clock (0x00599800's tail).
		_glints.Place( _now );

		if ( _cue != null && _now >= _cueAt )
		{
			var cue = _cue;
			_cue = null;
			cue();
		}

		if ( Busy )
			return;

		// The line is over: Advisor_Update empties the glints' channel (0x0059993e).
		_glints.SetCount( 0 );

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
		// Drawn while he is held, not only while he is running - see the class remarks. What takes him off
		// the screen is Dismiss clearing _shown, which is the quiet stop and the crying one, and those are
		// the two the original removes his model for. A pause is not one of them.
		if ( _shown && _speaker != null )
			_speaker.Draw( _light, _lightStrength );
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
	/// still drawn while held. The front end holds him while the game menu, a message box or the options
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
	/// Advisor_SayResponse's direct park path; a busy or disabled speaker returns zero. He wears the response's
	/// two props for the line (<see cref="AdvisorProps"/>), on the model its slot names, moves his mouth by its lip file
	/// <paramref name="lip"/> (nought for the sample's own) and plays its row <paramref name="gestureRow"/> of the gesture
	/// table (<see cref="AdvisorGestures"/>), -1 for the random chain.
	/// </summary>
	internal int PlayParkResponse( int response, int sample, int firstProp = 0, int secondProp = 0, int slot = 0, int lip = 0,
		int gestureRow = -1 )
	{
		if ( Busy || _paused || !CanSpeak )
		{
			Log.Info( $"Advisor: response={response} sample={sample} playback=refused" );
			return 0;
		}
		_speech ??= new SoundCategory( "global", "global/Speech", "speech" );
		if ( !_speech.IsValid ) return 0;
		var gestures = Speak( sample, cue: null, firstProp, secondProp, slot, lip, gestureRow );
		// The response adds clip 15's length after the clips, a row's too (005994ac); docs/exe/advisor-park.md.
		Log.Info( $"Advisor: response={response} sample={sample} playback=started" );
		return ParkResponseDuration( gestures, ClipMilliseconds( EndClip ) );
	}

	internal static int ParkResponseDuration( int gesturesMilliseconds, int endClipMilliseconds )
		=> gesturesMilliseconds + endClipMilliseconds + 1000;

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
	/// AdvisorQueue_Clear (0x005d6060): cuts him off, forgets what was queued, and lets the mix back
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
	internal void StopSpeaking()
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

		// Advisor_StopSpeaking and Advisor_StopQuietly empty the glints' channel (0x00599543, 0x0059972a).
		_glints.SetCount( 0 );

		// Cutting a line abandons its clip wherever it had got to, which is what can leave his face
		// half-posed - see ResetFace.
		ResetFace();

		return sounding;
	}

	/// <summary>
	/// When a line given at <paramref name="now"/> has its voice start, its cue fall and its clips end. The voice starts
	/// after the lead-in, or at once without one. The clips and a row's cue count from the clock less 200, moved on by the
	/// lead-in's 1000 where there is one (0x00598c56, 0x0059944c); a cue the message gave counts from <paramref name="now"/>.
	/// A row's own cue, <paramref name="rowCueMilliseconds"/>, replaces it.
	/// </summary>
	internal static (float SpeakAt, float CueAt, float GesturesEndAt) LineTimes( float now, bool leadIn, float cueSeconds,
		int? rowCueMilliseconds, int gesturesMilliseconds )
	{
		var counted = now + (leadIn ? LeadInSeconds : -0.2f);

		return (now + (leadIn ? LeadInSeconds : 0f),
			rowCueMilliseconds is { } rowCue ? counted + (rowCue / 1000f) : now + cueSeconds,
			counted + (gesturesMilliseconds / 1000f));
	}

	/// <summary>
	/// Advisor_SayResponse: ducks the mix now, starts him moving now, and speaks the sample after
	/// the lead-in.
	///
	/// 80 of the 641 samples in the bank are a 315-byte stub that is not valid MPEG - response ids
	/// with nothing recorded - and those simply come back silent, which ends the line at once.
	///
	/// A line given a row of the gesture table plays that row's clips in place of the random chain where they add up
	/// to more than nought (Advisor_PlayGestureRow, 0x00598bf0); a row that does not open on clip 14 has no lead-in, so
	/// its voice starts at once and its clips' end and its cue count from the clock less 200 (0x00598c56, 0x0059944c).
	/// A row's cue hands over the key (<see cref="HandOverKey"/>). Says how long the clips take, in milliseconds.
	/// </summary>
	private int Speak( int sample, Cue? cue, int firstProp = 0, int secondProp = 0, int slot = 0, int lip = 0, int gestureRow = -1 )
	{
		// Advisor_SayResponse (0x00599050) says nothing at all while the options have him switched off.
		if ( !GameOptions.Current.Advisor || _speech is not { IsValid: true } )
		{
			// A line that cannot be said still has its consequences.
			cue?.Action();
			return 0;
		}

		var row = gestureRow >= 0 ? AdvisorGestures.Read( gestureRow, ClipMilliseconds ) : null;
		var leadIn = row?.LeadIn ?? true;

		// Advisor_SayResponse empties the glints' channel as a line begins (0x005993b3).
		_glints.SetCount( 0 );

		_pending = sample;

		_cue = cue?.Action;

		if ( row is { CueAt: not null } )
			_cue = HandOverKey;

		var sampleMilliseconds = (int)_speech.Length( sample ).TotalMilliseconds;
		int gesturesMilliseconds;

		if ( row is { Milliseconds: > 0 } played )
		{
			_gestures.Clear();
			_clipFlags.Clear();

			foreach ( var (clip, flags) in played.Clips )
			{
				_gestures.Add( clip );
				_clipFlags.Add( flags );
			}

			gesturesMilliseconds = played.Milliseconds;
		}
		else
		{
			gesturesMilliseconds = BuildGestures( sampleMilliseconds + GestureMarginMilliseconds );
			_clipFlags.Clear();
			_clipFlags.AddRange( _gestures.Select( _ => 0 ) );
		}

		(_speakAt, _cueAt, _gesturesEndAt) = LineTimes( _now, leadIn, cue?.Seconds ?? 0f, row?.CueAt, gesturesMilliseconds );

		// What he is seen doing is the clips at their full lengths, clip 14 included, starting now.
		// The line's clips are started on the model its slot names (0x005993d5), and that one alone is on the screen.
		_slot = slot;
		_speaker = slot == ThemeSlot ? _themeFigure : _figure;

		_sequence = new ClipSequence( _gestures, clip => _speaker?.ClipMilliseconds( clip ) ?? 0 );
		_gesturesStartedAt = _now;

		// The response's own lip file (its +0x08): the sample's, but for lines that share one.
		_lips = LipFile.TryLoad( $"global/Speech/lips/sp_{(lip > 0 ? lip : sample):000}.lip", out var lips ) ? lips : null;

		_wearing = (firstProp, secondProp);

		if ( _speaker != null )
		{
			// He is about to be seen again, so start him from a known face rather than from
			// whatever the last line's clips left behind - see ResetFace. His first clip starts now (0x005993d5).
			ResetFace();
			StartClipAt( 0 );
			_shown = true;
		}

		if ( !_ducked )
		{
			_ducked = true;
			Audio.Duck( DuckLevel, DuckSeconds );
		}

		Log.Info( $"Advisor: sample {sample} in {(leadIn ? LeadInSeconds : 0f):0.0}s through clips [{string.Join( ", ", _gestures )}] "
			+ $"({gesturesMilliseconds}ms), {_queue.Count} more queued, wearing {firstProp},{secondProp} on model slot {slot}"
			+ (row is { Milliseconds: > 0 } ? $", gesture row {gestureRow}, lip {(lip > 0 ? lip : sample)}" : "")
			+ (row is { CueAt: { } at } ? $", its cue at {at}ms" : "") );

		return gesturesMilliseconds;
	}

	/// <summary>
	/// A clip of the line starting (Advisor_Update 0x00599a46, then Advisor_ClipStarted 0x00598dc0 from 0x00599800): the
	/// model's hide lists turn over, he puts on the line's props again, and the clip word's flags act. 0x10000 shows no
	/// add-on, no mouth and no head; 0x20000 to 0x200000 set UI particle channel 0, the glints (<see cref="SetGlints"/>), and
	/// 0x40000 also plays cat_ui 197.
	/// </summary>
	private void StartClipAt( int index )
	{
		_startedIndex = index;

		if ( _speaker == null || index < 0 || index >= _gestures.Count )
			return;

		var flags = _clipFlags.Count > index ? _clipFlags[index] : 0;
		_faceAway = (flags & AdvisorGestures.FaceAway) != 0;

		Log.Info( $"Advisor: clip {_gestures[index]} starts, {index + 1} of {_gestures.Count}" );
		_speaker.StartClip( _gestures[index] );
		_speaker.DressForClip( _wearing.First, _wearing.Second, _costume, _faceAway );
		_mouth = 0;

		if ( SetGlints( _glints, flags, ClipMilliseconds( _gestures[index] ) / 1000f, _now ) )
			Log.Info( $"Advisor: glints {_glints.Count} of effects [{string.Join( ", ", Enumerable.Range( 0, _glints.Count ).Select( _glints.EffectOf ) )}] at <{_glints.Centre.X:0.00}, {_glints.Centre.Y:0.00}> radii "
				+ $"<{_glints.Radius.X:0.00}, {_glints.Radius.Y:0.00}> {_glints.Turns:0} turns in {_glints.Seconds:0.000}s" );
	}

	/// <summary>
	/// What a clip word's flags do to the glints, UI particle channel 0, as the clip starts (Advisor_ClipStarted,
	/// 0x00598e7d): only one flag of <c>0x3e0000</c> acts, and none where two are set. The centre and radii are
	/// fractions of the half screen; each move takes the starting clip's length, <paramref name="clipSeconds"/>.
	/// 0x20000 tops the channel up to one golden ticket glint (effect 44) and, where that started one, stands it at
	/// 0.34, 0.62, going round once a clip on an upright ellipse 0.01 by 0.2; 0x40000 plays <c>cat_ui</c> 197 and tops it
	/// to two of effect 49, moving to the middle on an ellipse grown to 0.6 by 0.4, twice round; 0x80000 tops it to three,
	/// three times round; 0x100000 empties it and starts two of 44, which close in on 0.8, -0.8, the top right, twice
	/// round; 0x200000 empties it. Says whether the channel was set.
	///
	/// <b>A deviation:</b> the original stretches its 4:3 screen to any mode; here the glints are started pinned to the
	/// right and the bottom, as he is (<see cref="AdvisorModel.ScreenProjection"/>), and the channel eases them to the pin
	/// of the third each centre is set in (<see cref="UI.UiParticleChannel.PinShift"/>): the middle's round the big ticket,
	/// the top right's at the count. They belong to no window, as the row's key does (<see cref="HandOverKey"/>).
	/// </summary>
	internal static bool SetGlints( UI.UiParticleChannel glints, int flags, float clipSeconds, float now )
	{
		switch ( flags & 0x3e0000 )
		{
			case 0x20000:
				if ( glints.SetCount( 1, (int)ParLib.P_EFFECT_GoldenTicket, UI.Anchor.Right, down: UI.VerticalAnchor.Bottom ) == 0 )
					return false;

				glints.SetCentre( true, 0.34f, 0.62f, clipSeconds, now );
				glints.SetRadius( true, 0.01f, 0.2f, 1f );
				return true;

			case 0x40000:
				Log.Info( $"Advisor: cat_ui 197 for the glints, {(UI.UiSounds.Interface( 197 ) ? "a voice" : "no voice")}" );
				glints.SetCount( 2, (int)ParLib.P_EFFECT_GoldenTicket2, UI.Anchor.Right, down: UI.VerticalAnchor.Bottom );
				glints.SetCentre( false, 0f, 0f, clipSeconds, now );
				glints.SetRadius( false, 0.6f, 0.4f, 2f );
				return true;

			case 0x80000:
				glints.SetCount( 3, (int)ParLib.P_EFFECT_GoldenTicket2, UI.Anchor.Right, down: UI.VerticalAnchor.Bottom );
				glints.SetCentre( false, 0f, 0f, clipSeconds, now );
				glints.SetRadius( false, 0.6f, 0.4f, 3f );
				return true;

			case 0x100000:
				glints.SetCount( 0 );
				glints.SetCount( 2, (int)ParLib.P_EFFECT_GoldenTicket, UI.Anchor.Right, down: UI.VerticalAnchor.Bottom );
				glints.SetCentre( false, 0.8f, -0.8f, clipSeconds, now );
				glints.SetRadius( false, 0f, 0f, 2f );
				return true;

			case 0x200000:
				glints.SetCount( 0 );
				return true;

			default:
				return false;
		}
	}

	/// <summary>
	/// A row's cue in a park (Advisor_Update, 0x005999a0): the key effect 87 at the counts in the top right (90000, 0,
	/// 7000) and goldkey (<c>cat_ui</c> 0xc6). Its third call, IslandPanel_Refresh (0x004b9340), does nothing without
	/// the lobby's island panel, which a park has not (0x007cc4b4 nought, q707/orig/O).
	/// </summary>
	private static void HandOverKey()
	{
		Log.Info( "Advisor: the row's cue, the key handed over" );
		ParticleSystem.Current?.Spawn( (int)ParLib.P_EFFECT_Key, 90000, 0, 7000 );
		UI.UiSounds.GoldKeyHandedOver();
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

	/// <summary>
	/// How long a clip is taken to last as a line's clips are picked: the park's own model's where there is one,
	/// whichever model the line plays on, and the lobby's otherwise (<c>Advisor_ResetState</c> fills the table once).
	/// </summary>
	private int ClipMilliseconds( int clip )
		=> clip == StartClip ? 0 : (_themeFigure ?? _figure)?.ClipMilliseconds( clip ) ?? 0;

	/// <summary>Plays his clips one after another and moves his mouth - Advisor_Update's other half.</summary>
	private void Animate()
	{
		if ( _speaker == null || !_shown )
			return;

		// Each clip starts when the one before it ends and the last holds its final frame - asked of
		// the timeline by how long ago the line began, so no frame rate can leave him behind.
		var sinceGesturesStarted = _now - _gesturesStartedAt;

		if ( _sequence.TryLocate( sinceGesturesStarted, out var clip, out var intoClip ) )
		{
			// Each clip's start turns his hide lists over and dresses him again, as the original does at every clip.
			var index = _sequence.IndexAt( sinceGesturesStarted );
			if ( index != _startedIndex )
				StartClipAt( index );

			_speaker.Pose( clip, intoClip );
		}

		// Timed by where the sound itself has got to, not by the game's clock - see Voice.Position.
		var talking = _voice is { Playing: true } voice && _lips != null
			&& _lips.IsTalking( voice.Position );

		if ( _faceAway )
		{
			// A clip of 0x10000 shows no mouth (0x00599ba3).
		}
		else if ( !talking )
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
		_speaker?.ShowMouth( shape );
	}

	/// <summary>
	/// Puts his face back to a known state, for the moments where a clip stops partway through and
	/// leaves it however it happened to be.
	///
	/// Mesh visibility persists between clips - see <see cref="AdvisorModel.OpenEyes"/> for why his
	/// eyes are the ones that get stranded by it. His mouths are exposed to the same thing from the
	/// other side: clip 13 hides all five of them partway through and never shows one again, and
	/// <see cref="SetMouth"/> only tells the model anything when the shape changes, so a mouth left
	/// hidden would stay hidden. Forgetting the shape here makes the next call assert it whatever
	/// it turns out to be.
	/// </summary>
	private void ResetFace()
	{
		_speaker?.OpenEyes();
		_mouth = 0;
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

	private void LoadThemeFigure( string theme )
	{
		var folder = $"levels/{theme}/advisor";
		var path = $"{folder}/Advisor_{theme[..Math.Min( theme.Length, 3 )].ToUpperInvariant()}.md2";

		try
		{
			_themeFigure = new AdvisorModel( path, $"{folder}/textures", "global/advisor/textures" );
			Log.Info( $"Advisor: the theme's own model is {path}, its costume "
				+ string.Join( " ", _costume.Select( pair => $"{pair.AddOn}/{pair.SubOff}" ) )
				+ $", clips 1 to 15 last [{string.Join( ", ", Enumerable.Range( 1, 15 ).Select( _themeFigure.ClipMilliseconds ) )}] ms"
				+ $" and the lobby model's [{string.Join( ", ", Enumerable.Range( 1, 15 ).Select( clip => _figure?.ClipMilliseconds( clip ) ?? 0 ) )}]" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Advisor: the theme's own model {path} would not load - {e.Message}" );
		}
	}

	private void Release()
	{
		if ( !_ducked )
			return;

		_ducked = false;
		Audio.Duck( 1f, UnduckSeconds );
	}

	/// <summary>
	/// His scene is ending, as FUN_00598ad0 ends him when the lobby or a park is left: what is queued goes, and so
	/// does he, quietly, and the mix is let back up. He goes without a cry here; whoever handed him lines empties
	/// its own queue first, which is where the original's cry comes from - see the front end's end.
	/// </summary>
	protected override void OnDelete()
	{
		_queue.Clear();
		Dismiss( QuietenSeconds );
		Release();

		if ( Current == this )
			Current = null;
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

		var lit = _speaker == null ? "" : $" lit: ambient {_light.Ambient.X:0.000} {_light.Ambient.Y:0.000} {_light.Ambient.Z:0.000} strength {_lightStrength}; "
			+ string.Join( "; ", _speaker.LitCensus( _light, _lightStrength ) );

		return $"{what} paused={_paused} queued={_queue.Count} duck={Audio.DuckLevel:0.00} shown={_shown} clip={clip} mouth={_mouth} wearing={_wearing.First},{_wearing.Second} slot={_slot} "
			+ $"samples={_speech?.EffectIds.Count() ?? 0}{lit}";
	}
}
