namespace OpenTPW;

/// <summary>
/// The advisor talking in the lobby, and the rest of the mix getting out of his way.
///
/// <para>
/// <b>How the original decides what to say.</b> On entering the front end with no player chosen,
/// FrontEnd_Init (0x005d5970) shows the four-slot player screen, FrontEnd_ShowPlayerSlots
/// (0x004a6580). That counts the slots in use with Players_CountUsedSlots (0x005c7bb0) - a plain
/// count of the non-empty entries in a four-entry array - and queues advisor lines on it:
/// </para>
/// <list type="bullet">
/// <item>no slot in use: response 390, then 391 queued behind it - the greeting and the prompt
/// for a name, <see cref="NewPlayerLines"/></item>
/// <item>any slot in use: response 398, "don't I know you?"</item>
/// </list>
/// <para>
/// There is no save system yet, so no slot is ever in use and the first of those plays on every
/// launch. That is the original's own test with nothing to find, not a stand-in for it.
/// </para>
/// <para>
/// <b>How a line is spoken.</b> Lines go through a queue, AdvisorQueue_Add (0x005d6110): a line
/// added with flush set cuts off whatever he is saying and empties the queue first, and a line
/// added without it waits its turn. AdvisorQueue_Tick (0x005d5f80) hands the next line to
/// Advisor_SayResponse (0x00599050) whenever he is not busy. That maps the response id to a
/// sample through the table at 0x00768fb8 - the ids here are copied from it, because it lives in
/// the executable rather than in the game data - ducks the rest of the mix, and schedules the
/// sample 800ms later: it takes the clock minus 200 and adds 1000. He then stays busy until his
/// talking animations have covered the sample plus at least 500ms, after which Advisor_Update
/// (0x00599880) lifts the duck and the queue moves on.
/// </para>
/// <para>
/// Two deliberate differences. The duck is ramped rather than stepped - see
/// <see cref="Audio.Duck"/>. And it is held across queued lines: the original lifts it and puts
/// it straight back between one line and the next, which with a step is a frame nobody hears but
/// with a ramp would be an audible swell. The talking animations are not reproduced, because the
/// lobby does not draw the advisor yet, so a line's tail is the guaranteed 500ms and not whatever
/// the animations happen to round it up to.
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
	}

	/// <summary>What a player with no saved game hears, in order - responses 390 and 391.</summary>
	private static readonly int[] NewPlayerLines = { Samples.Welcome, Samples.AskForName };

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
	/// How long he stays busy after a sample ends before the queue moves on. The original pads
	/// the sample by at least 500ms when it picks his talking animations.
	/// </summary>
	private const float TailSeconds = 0.5f;

	private SoundCategory? _speech;
	private Voice? _voice;

	/// <summary>Samples waiting their turn, oldest first.</summary>
	private readonly Queue<int> _queue = new();

	/// <summary>The sample handed over and waiting on <see cref="_speakAt"/>, or 0.</summary>
	private int _pending;

	private float _speakAt;

	/// <summary>When the line that is playing, or just played, stops keeping him busy.</summary>
	private float _busyUntil = float.NegativeInfinity;

	private bool _ducked;

	/// <summary>Whether the front-end greeting has been dealt with this session.</summary>
	private bool _greeted;

	public LobbyAdvisor()
	{
		Current = this;
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
			_busyUntil = _voice == null ? Time.Now : float.PositiveInfinity;
			_pending = 0;
		}

		// A playing voice holds him busy until it ends, and the tail starts from there.
		if ( _pending == 0 && float.IsPositiveInfinity( _busyUntil ) && _voice is not { Playing: true } )
			_busyUntil = Time.Now + TailSeconds;

		if ( Busy )
			return;

		if ( _queue.Count > 0 )
			Speak( _queue.Dequeue() );
		else
			Release();
	}

	private bool Busy => _pending != 0 || Time.Now < _busyUntil;

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
	/// AdvisorQueue_Add: queues <paramref name="sample"/> behind whatever is waiting, or with
	/// <paramref name="flush"/> stops him and throws the queue away first.
	/// </summary>
	internal void Add( int sample, bool flush )
	{
		if ( flush )
		{
			_queue.Clear();
			StopCurrent();
		}

		if ( sample > 0 )
			_queue.Enqueue( sample );
	}

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

	/// <summary>Stops him mid-sentence, forgets what was queued, and lets the mix back up.</summary>
	internal void Hush()
	{
		_queue.Clear();
		StopCurrent();
		Release();
	}

	private void StopCurrent()
	{
		_pending = 0;
		_voice?.FadeOut( 0.2f );
		_voice = null;
		_busyUntil = float.NegativeInfinity;
	}

	/// <summary>
	/// Advisor_SayResponse: ducks the mix now and speaks the sample after the lead-in.
	///
	/// 80 of the 641 samples in the bank are a 315-byte stub that is not valid MPEG - response ids
	/// with nothing recorded - and those simply come back silent, which ends the line at once.
	/// </summary>
	private void Speak( int sample )
	{
		if ( _speech is not { IsValid: true } )
			return;

		_pending = sample;
		_speakAt = Time.Now + LeadInSeconds;

		if ( !_ducked )
		{
			_ducked = true;
			Audio.Duck( DuckLevel, DuckSeconds );
		}

		Log.Info( $"Advisor: sample {sample} in {LeadInSeconds:0.0}s, {_queue.Count} more queued" );
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
			: _voice is { Playing: true } ? $"saying {_voice.Name}" : Busy ? "finishing" : "quiet";

		return $"{what} queued={_queue.Count} duck={Audio.DuckLevel:0.00} samples={_speech?.EffectIds.Count() ?? 0}";
	}
}
