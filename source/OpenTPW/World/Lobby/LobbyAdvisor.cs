namespace OpenTPW;

/// <summary>
/// The advisor talking in the lobby, and the rest of the mix getting out of his way.
///
/// <para>
/// <b>How the original does it.</b> Every line the advisor says is a "ResponseID". FUN_00599050
/// - the one that logs "Advisor says ResponseID %d" - looks that id up in a table of 32-byte
/// records at 0x00768fb8, terminated by 9999, whose fields are, in order: the response id, the
/// speech sample id, the lip-sync file number, an animation, a packed word holding a
/// global/local flag in its high half and the advisor's park in its low half, and two more the
/// lobby does not reach. Sample in hand, it plays it from one of two categories - DAT_00803a34
/// for global speech, DAT_00803a40 for a park's own - and those are the cat_speech*.map pairs in
/// data\global\Speech and data\levels\&lt;park&gt;\Speech.
/// </para>
/// <para>
/// The two behaviours worth copying from that function are less obvious than the lookup. It
/// starts the sample, asks how long it is, then <i>stops it again</i> and sets a timer for a
/// second later (DAT_00f79694 = now + 1000) - so the advisor never speaks the instant he is
/// asked to, he speaks a second afterwards. And it calls FUN_0051e6f0(1) before playing, which
/// sets the flag FUN_0051bd70 reads to decide whether the music and effects group volumes get
/// multiplied by a percentage; the matching FUN_0051e6f0(0) is in the advisor's update, on the
/// frame his sample runs out. That is the ducking, and <see cref="Audio.Duck"/> is our version.
/// </para>
/// <para>
/// <b>What is not settled.</b> Which ResponseID the lobby asks for on a first run. The call is
/// at 0x005d5fcf, which Ghidra has disassembled but never made into a function, so the bridge
/// cannot decompile it and read the constant; the record table itself is undefined bytes for the
/// same reason. <see cref="FirstLaunchSample"/> is therefore a guess, and the <c>speech</c>
/// console command is there to settle it by ear.
/// </para>
/// </summary>
public sealed class LobbyAdvisor : Entity
{
	internal static LobbyAdvisor? Current { get; private set; }

	/// <summary>
	/// The sample the advisor greets a new player with - <b>not yet confirmed against the
	/// original</b>, see the class remarks.
	///
	/// 617 is a guess with reasons rather than a shot in the dark: the bank holds 641 samples,
	/// data\Language\&lt;lang&gt;\TAG_SYSTEM.str holds 567 subtitle strings for the in-park
	/// advisor messages, and this sits in the handful past the end of that range - so it is one
	/// of the lines that has no in-park subtitle. It is also 23.9 seconds against a median of
	/// 5.7, which is intro length rather than advice length.
	///
	/// Run <c>speech &lt;n&gt;</c> in the debug console to audition another, then change this.
	/// </summary>
	private const int FirstLaunchSample = 617;

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
	/// FUN_0051bd70 ducks by <c>(volume * DAT_00785914) / 100</c> - a percentage. That value sits
	/// in zeroed memory in the image with no writer, so it is filled in from config at runtime,
	/// and DUCKINGLEVEL is the only candidate the config has. <b>Inferred, not proven</b>: the
	/// file's own header comment says its numbers are detail levels at which a feature switches
	/// on, which would make 38 a threshold instead. 38% is a sensible duck either way.
	/// </summary>
	private const float DuckLevel = 0.38f;

	/// <summary>
	/// The original steps the group volumes; we ramp them. A step down onto a sustained park
	/// theme is audible as a lurch, and the ramp is short enough that it still reads as the mix
	/// getting out of the way rather than as a fade.
	/// </summary>
	private const float DuckSeconds = 0.35f;

	/// <summary>Longer coming back, so the theme swells rather than snapping back in.</summary>
	private const float UnduckSeconds = 1.2f;

	/// <summary>
	/// The original's pause between being asked for a line and speaking it: DAT_00f79694 is set
	/// to the current time plus 1000ms. The duck starts at the beginning of this, so the mix has
	/// already made room by the time he opens his mouth.
	/// </summary>
	private const float StartDelaySeconds = 1f;

	private SoundCategory? _speech;
	private Voice? _voice;

	/// <summary>The sample waiting on <see cref="_speakAt"/>, or 0 when nothing is pending.</summary>
	private int _pending;

	private float _speakAt;

	private bool _ducked;

	/// <summary>Whether the first-run greeting has been dealt with this session.</summary>
	private bool _greeted;

	public LobbyAdvisor()
	{
		Current = this;
	}

	/// <summary>
	/// Whether any of the four player slots holds a saved game.
	///
	/// Always false, because there is no save system yet - which is what the original tests
	/// before deciding a player is new, so until saves exist the greeting is correct every time
	/// rather than being a stub. This is the one line to change when they do.
	/// </summary>
	private static bool HasSavedGame => false;

	protected override void OnUpdate()
	{
		if ( !Audio.Ready )
			return;

		_speech ??= new SoundCategory( "global", "global/Speech", "speech" );

		if ( !_greeted )
		{
			_greeted = true;

			if ( !HasSavedGame )
				Say( FirstLaunchSample );
		}

		if ( _pending != 0 && Time.Now >= _speakAt )
		{
			_voice = _speech.Play( _pending, SpeechVolume, respectDelay: false, bus: AudioBus.Speech );
			_pending = 0;

			if ( _voice == null )
				Release();
		}

		// The original un-ducks on the frame the sample runs out, in the same update that drives
		// the lip-sync. Nothing else here holds the duck, so a line that failed to play releases
		// it above rather than leaving the lobby quiet for good.
		if ( _ducked && _pending == 0 && _voice is not { Playing: true } )
			Release();
	}

	/// <summary>
	/// Has the advisor say <paramref name="sample"/>, a second from now, with the rest of the mix
	/// ducked from this moment until he finishes.
	/// </summary>
	internal void Say( int sample )
	{
		if ( !Audio.Ready || _speech is not { IsValid: true } )
			return;

		// Ids run from 1, so anything below that is "stop talking" rather than a line. 80 of the
		// 641 in the global bank are a 315-byte stub that is not valid MPEG - response ids with
		// nothing recorded against them - and those simply come back silent from the decoder.
		if ( sample <= 0 )
		{
			Hush();
			return;
		}

		_voice?.FadeOut( 0.2f );
		_voice = null;

		_pending = sample;
		_speakAt = Time.Now + StartDelaySeconds;

		_ducked = true;
		Audio.Duck( DuckLevel, DuckSeconds );

		Log.Info( $"Advisor: sample {sample} in {StartDelaySeconds:0.0}s, ducking to {DuckLevel:0.00}" );
	}

	/// <summary>Stops him mid-sentence and lets the mix back up.</summary>
	internal void Hush()
	{
		_pending = 0;
		_voice?.FadeOut( 0.25f );
		_voice = null;
		Release();
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
			: _voice is { Playing: true } ? $"saying {_voice.Name}" : "quiet";

		return $"{what} duck={Audio.DuckLevel:0.00} samples={_speech?.EffectIds.Count() ?? 0}";
	}
}
