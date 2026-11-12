namespace OpenTPW;

/// <summary>
/// A park's sounds - its music, the crowd's voice, its weather, its rides' screams, the staff's voices, a guest put
/// off something or yawning, and the sounds placed in the land.
///
/// <para>
/// The original loads four categories as a park comes up and plays one thing. Its state machine
/// registers cat_ambient, cat_rides, cat_speech and cat_music (FUN_0051ec50), re-applies the group
/// volumes, and then plays <b>effect 2 of cat_music</b> - the only effect that category declares -
/// and keeps the voice (<c>docs/exe/scenes.md</c>, "Sound scopes"). That order is the order this is built in.
/// </para>
///
/// <para>
/// <b>The crowd picks the music's variation, not its loudness.</b> The voice's parameter 4 is set to nought as it
/// starts and every 32nd tick afterwards from a count, <c>clamp( guests / 2, 0, 100 )</c> held to 89 - see
/// <see cref="CrowdLevel"/>. It is the voice's zone parameter and nothing else: each next sample is drawn from the
/// variation the current one's zones give for it (from the voice's own slot at 0x006bde00). Every variation's volume
/// is a fixed hundred with no controller on it, in all four themes, so the level never touches how loud the music is.
/// Lost Kingdom's zones are 0-14, 15-29, 30-44, 45-59, 60-74 and 75-90 for its six variations: thirteen guests, level
/// six, play the first, and so does an empty park (<c>docs/exe/park-engine.md</c>, "The music's level").
/// </para>
///
/// <para>
/// Engine and content: which category and which effect a park plays, and how loud, is this park's
/// content, played through the engine's <see cref="Audio"/> and <see cref="SoundCategory"/> - the same
/// split <see cref="LobbyAudio"/> sits on.
/// </para>
/// </summary>
public sealed partial class ParkAudio : Entity
{
	/// <summary>The one park audio system. There is only ever one, and none in the lobby.</summary>
	internal static ParkAudio? Current { get; private set; }

	/// <summary>
	/// The effect the original plays, and the only one cat_music declares - in all four themes.
	/// It has six variations in jungle, five in hallow and space, seven in fantasy, each a set of samples
	/// seventeen and a half seconds long, played one after another with no wait (every variation's gap is nought).
	/// </summary>
	private const int MusicEffect = 2;

	/// <summary>
	/// Thunder, and the rain bed it rolls over - effects 32 and 33 of the <b>global</b> cat_ambient
	/// category, which is not one of the four a park registers as it comes up.
	///
	/// <para>
	/// The executable names the category and the effect ids but cannot say which sounds they are:
	/// FUN_00512880 plays effect 0x20 from the category handle at DAT_00803a20, which
	/// Sound_RegisterGlobalCategories (0x0051eae0) fills in by name as "cat_ambient", and the rain
	/// loop is 0x21 through FUN_0051e830. <b>The shipped data says which samples those are</b>, and
	/// the two agree exactly: in data/global/sound/cat_ambientSFX.map effect 32 is one list of
	/// Thunder2, Thunder3 and Thunder4 at cumulative weights 21845/43690/65535 - even thirds - and
	/// effect 33 is RAIN.mp2.
	/// </para>
	/// </summary>
	private const int ThunderEffect = 32;

	private const int RainEffect = 33;

	/// <summary>The parameter the rain's level is set under (<c>FUN_0051e830</c>'s 8); each of 33's four variations names it on its volume.</summary>
	public const int RainParameter = 8;

	/// <summary>
	/// How loud, and it is a correction rather than a preference.
	///
	/// <para>
	/// <see cref="LobbyAudio"/> sets every layer by measuring the samples themselves and puts the
	/// music at -26 dBFS. A park's music is not the lobby's: measured across twenty-five of jungle's
	/// hundred and twenty-nine tracks it sits at a median of <b>-16.4 dBFS</b> where the lobby's park
	/// themes sit at -19.8, so the gain that lands it on the same target is 0.33 and not the lobby's
	/// 0.50. Its peaks measure a little <i>over</i> full scale, which is why this is not louder.
	/// </para>
	/// <para>
	/// Put another way, it is the lobby's own setting carried across: park music is 3.4 dB hotter than
	/// the lobby's themes, and <c>0.50 * 10^(-3.4/20)</c> is 0.338, so this sits where the lobby's
	/// music sits rather than at a number chosen to taste.
	/// </para>
	/// <para>
	/// Confirmed by capturing the mixer's own output through SDL's disk driver: music at this full gain came out at
	/// <b>-36.3 dBFS</b> over eighteen sounding seconds. That is the -26 above less the 6 dB of
	/// <see cref="Audio.MasterVolume"/>, which defaults to 0.5 and sits under every one of these
	/// targets, and the rest is which of the arrangements happened to play - they span seven decibels.
	/// <b>The -26 is a level before master volume, not a level at the speakers</b>, which is easy to
	/// compare against the wrong thing.
	/// </para>
	/// </summary>
	internal const float MusicVolume = 0.33f;

	/// <summary>How long the music takes to fade as the park ends - the same as the lobby's stop.</summary>
	private const float StopSeconds = 0.15f;

	/// <summary>The top of the level the crowd drives, and the original's own clamp.</summary>
	public const int LoudestCrowd = 100;

	/// <summary>
	/// The level the crowd gives the music, nought through a hundred - the original's
	/// <c>FUN_004c81e0</c>, which is <c>clamp( counted / 2, 0, 100 )</c> and nothing more. It is the music voice's
	/// zone parameter (<see cref="NextMusic"/>), not a loudness.
	///
	/// <para>
	/// <b>It counts GUESTS, not people.</b> <c>FUN_004c7fa0</c> walks the thing list and counts a thing only where
	/// <c>*(thing + 2) == 1</c> - the model byte, and model 1 is a guest - so the five staff are not in it.
	/// The caller clamps the result again to 89, below this hundred, which binds from 180 guests
	/// (<see cref="MusicLevel"/>).
	/// </para>
	/// <para>
	/// <b>It counts the guests <c>FUN_004fa990</c> passes.</b> The caller hands it
	/// <see cref="ParkPeople.GateGuestCensus"/>: a guest counts only where the cell under them is a cleared cell, a
	/// path, a queue or a ride's end (<see cref="Peep.CountsOn"/>), so a guest still on the bus stop's type 30 approach (several
	/// cells), or on a ride's footprint, is left out. The original asks with an age limit of 0 (<c>FUN_004c81e0</c>
	/// pushes it); <c>FUN_004c7fa0</c> still reuses its cached count within a quarter tick of the last
	/// (<c>0x004c7fb3</c>..<c>0x004c7fc4</c>), which is not built. A guest riding stands on a type 9 cell and is counted;
	/// only the ride's footprint cells of type 4 leave one out.
	/// </para>
	/// </summary>
	public static int CrowdLevel( int guests ) => Math.Clamp( guests / 2, 0, LoudestCrowd );

	/// <summary>The most the park loop hands the music: 89 (<c>CMP EAX,0x59</c>, <c>0x0054f84e</c>).</summary>
	public const int LoudestMusic = 89;

	/// <summary>The world state in which the music is sent nought (<c>+0x1da738</c> against 4, <c>0x0054f860</c>).</summary>
	public const int SilentWorldState = 4;

	/// <summary>How often the level is set, in 31 ms ticks: every 32nd (<c>TEST [0x00877d34],0x1f</c>, <c>0x0054f82d</c>).</summary>
	public const int MusicLevelEvery = 32;

	/// <summary>
	/// What the park loop hands the music's level setter <c>FUN_0051e790</c>: the crowd's level held to
	/// <see cref="LoudestMusic"/>, and nought while the world's state is <see cref="SilentWorldState"/>.
	/// </summary>
	public static int MusicLevel( int guests, int worldState )
		=> worldState == SilentWorldState ? 0 : Math.Min( CrowdLevel( guests ), LoudestMusic );

	/// <summary>
	/// The level the park loop hands the music for <paramref name="people"/>: <see cref="MusicLevel"/> of the guests
	/// <see cref="ParkPeople.GateGuestCensus"/> counts, nought with no park.
	/// </summary>
	internal static int MusicLevelFor( ParkPeople? people, int worldState )
		=> MusicLevel( people?.GateGuestCensus ?? 0, worldState );

	/// <summary>
	/// Whether a frame that ran <paramref name="ticksDue"/> ticks ending on <paramref name="lastTick"/> ran one the
	/// level is set on, a tick whose low five bits are nought. The loop tests every tick, so a long frame's 32nd is
	/// not missed.
	/// </summary>
	public static bool SetsMusicLevel( int lastTick, int ticksDue )
	{
		for ( var tick = lastTick - ticksDue + 1; tick <= lastTick; ++tick )
		{
			if ( (tick & (MusicLevelEvery - 1)) == 0 )
				return true;
		}

		return false;
	}

	/// <summary>The level the music was last set to, nought until the first set, as the original sets it as it starts the voice (<c>FUN_0051e730</c>).</summary>
	internal int LevelNow { get; private set; }

	/// <summary>The variation the music's last sample was drawn from, zero-based; -1 before the first.</summary>
	internal int VariationNow { get; private set; } = -1;

	/// <summary>The sample the music's last draw from a list of two or more took, -1 for none: the original's voice byte <c>+0x46</c>, which a change of variation leaves alone (<c>0x006bc747</c>, <c>docs/exe/audio.md</c>, "The sample draw").</summary>
	internal int MusicLastSample => _musicLast;

	private int _musicLast = -1;

	/// <summary>
	/// What the music plays when its sample has run out: the variation its next sample is drawn from, and how
	/// loud - <c>FUN_006be450</c>, the rule a held scream's chain follows too
	/// (<see cref="ParkScreams.NextVariation"/>): the first sample takes the effect's first variation whatever the
	/// level; each later one draws, by the targets' weights, among the current variation's zone records whose range
	/// holds the level. The loudness is the music's one, whatever the level. Null where no zone holds the level, and
	/// the voice then waits for a level one holds.
	/// </summary>
	internal static (int Variation, float Volume)? NextMusic( IReadOnlyList<SoundCategoryFile.Variation> variations,
		int current, int level, Random random )
	{
		var next = ParkScreams.NextVariation( variations, current, level, random );

		return next < 0 ? null : (next, MusicVolume);
	}

	/// <summary>The sound library draws on a generator of its own (<c>0x00fb1f20</c>); this is the music's.</summary>
	private readonly Random _musicRandom = new();

	/// <summary>How many times the level has been set since the park's sound was made, for the console.</summary>
	internal int LevelSets { get; private set; }

	private readonly SoundCategory? _music;
	private Voice? _voice;

	/// <summary>The kids' effect that is the crowd's voice, <c>0x5b</c> (<c>FUN_0051e7b0</c>, <c>0x0051e7ea</c>).</summary>
	public const int CrowdVoiceEffect = 0x5b;

	/// <summary>The parameter the park loop sets on it (<c>PUSH 0x7</c>, <c>0x0051e7fd</c>), which is the effect's own id.</summary>
	public const int CrowdVoiceParameter = 7;

	/// <summary>How many cells each way from the pointer's the crowd is counted over (<c>FUN_004c8d30</c>'s third argument).</summary>
	public const int CrowdVoiceReach = 4;

	/// <summary>
	/// What the park loop hands the crowd's voice (<c>0x0054f875</c>..<c>0x0054f8b9</c>): the guests counted round the
	/// pointer's cell, held to a hundred, and nought while the world's state is <see cref="SilentWorldState"/>.
	/// </summary>
	public static int CrowdVoiceLevel( int guestsNear, int worldState )
		=> worldState == SilentWorldState ? 0 : Math.Clamp( guestsNear, 0, LoudestCrowd );

	/// <summary>
	/// A sample's volume (<paramref name="bit"/> 1, <c>FUN_006bc090</c>) or pitch (2, <c>FUN_006bc170</c>) under one
	/// parameter set to <paramref name="level"/>: the first of the variation's two controllers whose mask has the bit
	/// gives <c>value × range / 100 + low</c>, its value the level where its key is the parameter's and nought
	/// otherwise; with neither, a draw from <c>low</c> up to, not including, <c>high</c>.
	/// </summary>
	internal static int Controlled( SoundCategoryFile.Variation header, int bit, int parameter, int level, Random random )
		=> ControlledBy( header, bit, header.FirstKey == parameter ? level : 0, header.SecondKey == parameter ? level : 0, random );

	/// <summary>
	/// The same with the two controllers' values given: <paramref name="first"/> the first's (slot 1), <paramref name="second"/>
	/// the second's (slot 2).
	/// </summary>
	internal static int ControlledBy( SoundCategoryFile.Variation header, int bit, int first, int second, Random random )
	{
		var range = bit == 1 ? header.Volume : header.Pitch;

		if ( (header.FirstMask & bit) != 0 )
			return ParkCarSounds.Controlled( first, range );

		if ( (header.SecondMask & bit) != 0 )
			return ParkCarSounds.Controlled( second, range );

		var (low, high) = range.High < range.Low ? (range.High, range.Low) : range;

		return high == low ? low : low + random.Next( high - low );
	}

	/// <summary>Whether one of a variation's two controllers is on its volume and keyed by <paramref name="parameter"/>.</summary>
	internal static bool NamesOnVolume( SoundCategoryFile.Variation header, int parameter )
		=> ((header.FirstMask & 1) != 0 && header.FirstKey == parameter)
			|| ((header.SecondMask & 1) != 0 && header.SecondKey == parameter);

	/// <summary>The level the crowd's voice was last set to (<c>[0x00803ab0]</c>).</summary>
	internal int CrowdVoiceLevelNow { get; private set; }

	/// <summary>How many guests the last count found round the pointer's cell, before the hold.</summary>
	internal int CrowdVoiceGuests { get; private set; }

	/// <summary>The pointer's cell at the last count, packed, nought for none.</summary>
	internal int CrowdVoiceCell { get; private set; }

	/// <summary>Whether the voice is held, the original's handle at <c>[0x00803aa4]</c>.</summary>
	internal bool CrowdVoiceHeld { get; private set; }

	/// <summary>The variation the voice's last sample was drawn from, zero-based; -1 with none since it was started.</summary>
	internal int CrowdVoiceVariation { get; private set; } = -1;

	/// <summary>The voice's volume, of a hundred, and its pitch in 96ths of an octave: what its sample plays at.</summary>
	internal (int Volume, int Pitch) CrowdVoiceSample { get; private set; }

	/// <summary>How many samples the voice has played, and how many times it has been started, for the console.</summary>
	internal (int Samples, int Starts) CrowdVoicePlays { get; private set; }

	/// <summary>What the crowd's sample is playing at, before <see cref="Audio.MasterVolume"/>; nought with none.</summary>
	internal float CrowdVoiceVolume => _crowd is { Playing: true } ? _crowd.Volume : 0f;

	/// <summary>The sample the crowd's voice's last draw from a list of two or more took, -1 for none: the original's voice byte <c>+0x46</c>, which a change of variation leaves alone (<c>0x006bc747</c>, <c>docs/exe/audio.md</c>, "The sample draw").</summary>
	internal int CrowdVoiceLastSample => _crowdLast;

	private int _crowdLast = -1;
	private readonly Random _crowdRandom = new();
	private Voice? _crowd;

	/// <summary>
	/// <c>FUN_0051e7b0</c>, on the music's beat: above nought the voice is started if it is not held and its
	/// parameter set, which a sample whose variation names it as a controller takes at once (<c>0x006bbb80</c>); at
	/// nought a held voice is faded out and let go.
	/// </summary>
	private void SetCrowdVoice( int level )
	{
		CrowdVoiceLevelNow = level;

		if ( level == 0 )
		{
			if ( !CrowdVoiceHeld )
				return;

			_crowd?.StopFading();
			_crowd = null;
			CrowdVoiceHeld = false;

			Log.Info( $"Crowd voice: stopped at level 0 on tick {GameClock.Ticks}" );
			return;
		}

		if ( !CrowdVoiceHeld )
		{
			// A new voice has no variation yet, so its first sample is the first variation's (FUN_006be450).
			CrowdVoiceHeld = true;
			CrowdVoiceVariation = -1;
			_crowdLast = -1;
			CrowdVoicePlays = (CrowdVoicePlays.Samples, CrowdVoicePlays.Starts + 1);

			Log.Info( $"Crowd voice: started at level {level} on tick {GameClock.Ticks}" );
			return;
		}

		var headers = _kids?.VariationsOf( CrowdVoiceEffect ) ?? [];

		if ( _crowd is not { Playing: true } || CrowdVoiceVariation < 0 || CrowdVoiceVariation >= headers.Count )
			return;

		// The set applies volume and pitch again only where the parameter is one of the variation's own controllers.
		var header = headers[CrowdVoiceVariation];

		if ( header.FirstKey == CrowdVoiceParameter || header.SecondKey == CrowdVoiceParameter )
			ApplyCrowdVoice( _crowd, header );
	}

	/// <summary>
	/// The voice's volume and pitch set again, from the level or drawn (<c>0x006bbbe0</c>), and put on
	/// <paramref name="voice"/> when there is one. Answers the volume as a gain, the channel's own number
	/// (<see cref="ChannelGain"/>): a volume of 14 is 10 of 127 beside the music's 60.
	/// </summary>
	private float ApplyCrowdVoice( Voice? voice, SoundCategoryFile.Variation header )
	{
		CrowdVoiceSample = HeldSample( header, first: true, CrowdVoiceSample, CrowdVoiceParameter, CrowdVoiceLevelNow, _crowdRandom );
		var gain = ChannelGain( CrowdVoiceSample.Volume );

		voice?.SetVolume( gain );
		voice?.SetRate( ParkCarSounds.Rate( CrowdVoiceSample.Pitch ) );

		return gain;
	}

	/// <summary>
	/// What a held voice's sample plays at. A voice's first is given its variation's volume and pitch as its
	/// channel is set up (<c>FUN_006bc410</c>, <see cref="Controlled"/>); each later one is queued with its sample
	/// alone (<c>0x006bde00</c>, <c>FUN_006bc240</c>), so it plays at what the voice <paramref name="kept"/>, a
	/// variation with no controller too (<c>docs/exe/audio.md</c>, "A held voice's next sample").
	/// </summary>
	internal static (int Volume, int Pitch) HeldSample( SoundCategoryFile.Variation header, bool first,
		(int Volume, int Pitch) kept, int parameter, int level, Random random )
		=> first
			? (Controlled( header, 1, parameter, level, random ), Controlled( header, 2, parameter, level, random ))
			: kept;

	/// <summary>
	/// The held voice's next sample once the last has ended, drawn as the music's is (<see cref="NextMusic"/>): the
	/// level's zone picks the variation, and the sample plays at the voice's own volume and pitch
	/// (<see cref="HeldSample"/>). Flat, as the original plays it at (0,0,0), and on a frame's edge, where the
	/// original's service pass starts it.
	/// </summary>
	private void NextCrowdSample()
	{
		if ( !CrowdVoiceHeld || _crowd is { Playing: true } || _kids is not { IsValid: true } )
			return;

		var headers = _kids.VariationsOf( CrowdVoiceEffect );
		var first = CrowdVoiceVariation < 0;
		var next = ParkScreams.NextVariation( headers, CrowdVoiceVariation, CrowdVoiceLevelNow, _crowdRandom );

		if ( next < 0 )
			return;

		CrowdVoiceVariation = next;
		CrowdVoiceSample = HeldSample( headers[next], first, CrowdVoiceSample, CrowdVoiceParameter, CrowdVoiceLevelNow, _crowdRandom );
		_crowd = Audio.Play( _kids.PickFrom( CrowdVoiceEffect, next, ref _crowdLast, avoidRepeat: !first ), ChannelGain( CrowdVoiceSample.Volume ), bus: AudioBus.Effects );

		if ( _crowd is null )
			return;

		_crowd.SetRate( ParkCarSounds.Rate( CrowdVoiceSample.Pitch ) );
		CrowdVoicePlays = (CrowdVoicePlays.Samples + 1, CrowdVoicePlays.Starts);
		Log.Trace( $"Crowd voice: sample '{_crowd.Name}' variation {next + 1} volume {CrowdVoiceSample.Volume} plays {_crowd.Volume:F4} pitch {CrowdVoiceSample.Pitch} on tick {GameClock.Ticks}" );
	}

	/// <summary>
	/// The global ambient category, which is where a park's weather sounds live. Global rather than
	/// the park's own: its bank path is "Sound\Ambient" under data/global, and the category is
	/// registered once at startup rather than per level.
	/// </summary>
	private readonly SoundCategory? _ambient;

	/// <summary>One of the rain's two held voices: its level, the variation its sample is of, and the volume it keeps.</summary>
	internal sealed class RainVoice
	{
		/// <summary>Whether the voice is held, playing one sample after another.</summary>
		public bool Held { get; set; }

		/// <summary>The level it was last set to, which picks its next sample's variation; nought on a voice nobody sets.</summary>
		public int Level { get; set; }

		/// <summary>The variation its sample is of, zero-based, or -1 before its first.</summary>
		public int Variation { get; set; } = -1;

		/// <summary>The volume, of a hundred, its first sample or the last set gave it.</summary>
		public int Volume { get; set; }

		/// <summary>How many samples it has started since the park began.</summary>
		public int Samples { get; set; }

		public Voice? Voice { get; set; }

		/// <summary>What its sample is playing at, nought with none.</summary>
		public float Plays => Voice is { Playing: true } ? Voice.Volume : 0f;
	}

	/// <summary>The rain's bed (<c>[0x00803aac]</c>), the voice the level is set on.</summary>
	internal RainVoice RainBed { get; } = new();

	/// <summary>The weather thing's own rain voice (<c>mRainSoundHandle</c>, <c>+0x54</c>), which nothing sets a level on.</summary>
	internal RainVoice RainOwn { get; } = new();

	private readonly Random _rainRandom = new();

	/// <summary>
	/// The guests' own voices, which is where the scream samples live - <c>cat_kids</c>, the category
	/// <c>Sound_RegisterGlobalCategories</c> parks at <c>DAT_00803a24</c>.
	/// </summary>
	/// <remarks>
	/// <b>The address order does not name it, and guessing from that gives the wrong category.</b> The
	/// registration assigns <c>0x803a20</c> ambient, <c>0x803a28</c> rides, <c>0x803a2c</c> ui,
	/// <c>0x803a24</c> <b>kids</b>, <c>0x803a30</c> staff - so the slot between ambient and rides is the
	/// kids one, and "it is a ride sound, so it must be cat_rides" is wrong. The map corroborates it:
	/// <c>cat_kidsSFX.map</c> declares exactly the four ids the scream player asks for.
	/// </remarks>
	private readonly SoundCategory? _kids;

	/// <summary>The staff's voices, the global <c>cat_staff</c> at <c>[0x00803a30]</c>.</summary>
	private readonly SoundCategory? _staff;

	/// <summary>The draws a staff sound's variation, volume and pitch take.</summary>
	private readonly Random _staffRandom = new();

	/// <summary>How many staff sounds have started, and how many of those were a sample other than the blank one.</summary>
	internal (int Started, int Voiced) StaffSounds { get; private set; }

	/// <summary>The park's <c>cat_rides</c>, which every <c>EventMap.rse</c> slot names an effect of.</summary>
	private readonly SoundCategory? _rides;

	/// <summary>The park's <c>cat_rides</c>, or null with no sound device or no such category.</summary>
	internal SoundCategory? Rides => _rides is { IsValid: true } ? _rides : null;

	/// <summary>The screams the rides are holding, each on its own clock - see <see cref="ParkScreams"/>.</summary>
	private readonly ParkScreams _screams;

	/// <summary>
	/// Every distinct sample any scream in this park has played, which is what makes "the screams vary"
	/// a number the game reports about itself rather than something inferred from a recording -
	/// <c>docs/VERIFYING.md</c> rules 99 and 100.
	/// </summary>
	private readonly HashSet<string> _screamSamples = [];

	/// <summary>
	/// The four held scream effects, chosen by <c>STARTSCREAM</c>'s first operand -
	/// <c>FUN_00551130</c>'s bands: nought screams not at all, 1, 2-3, 4-7, then 8 and over. A negative band
	/// falls through the signed <c>&lt; 4</c> test with 2 and 3 and plays <c>0x48</c>.
	/// </summary>
	private static readonly int[] ScreamEffects = [0x47, 0x48, 0x49, 0x4a];

	/// <summary>
	/// The speed word of a script nothing has written - the script creator's 50 (<c>0x00558c3c</c>), which the
	/// scream functions below take where a caller names none. A script reads its own (<see cref="RideScript.Speed"/>,
	/// Q155): the scream readings average the operand with it, where 50 is half the answer rather than an identity.
	/// </summary>
	public const int ScriptSpeed = 50;

	/// <summary>
	/// A held scream's parameter 6: the engine's <c>(a + b) / 2</c>, held to nought through 100
	/// (<c>0x00551241</c>..<c>0x00551265</c>). Its chain picks each later child's variation by it. It is no
	/// loudness: a child's is its variation's own - see <see cref="PlayScream"/>.
	/// </summary>
	public static int ScreamVolume( int level, int speed = ScriptSpeed ) => Math.Clamp( (level + speed) / 2, 0, 100 );

	/// <summary>Which sample a band asks for, or nought when the band is silent.</summary>
	public static int ScreamEffectFor( int band ) => band switch
	{
		0 => 0,
		1 => ScreamEffects[0],
		< 4 => ScreamEffects[1],
		< 8 => ScreamEffects[2],
		_ => ScreamEffects[3]
	};

	/// <summary>
	/// The second axis of <c>SINGLESCREAM</c>'s grid: <c>(level + speed) / 50</c>, held at three.
	///
	/// <b>Held at the top only</b>, which is the engine's own asymmetry - <c>FUN_00551320</c> does
	/// <c>if ( 3 &lt; i ) i = 3;</c> and nothing at the bottom, then falls out of its own switch
	/// returning nought for any index it does not recognise. A level below nought cannot reach here
	/// anyway, because the handler sends it to the band-only branch first.
	/// </summary>
	public static int ScreamGridIndex( int level, int speed = ScriptSpeed ) => Math.Min( (level + speed) / 50, 3 );

	/// <summary>
	/// Which one-shot <c>SINGLESCREAM</c> plays, or nought when nothing does.
	///
	/// <para>
	/// <b>Two branches, and the handler picks between them on the SECOND operand</b> (<c>0x00555f6b</c>,
	/// <c>CMP ESI,EDI</c> then <c>JGE</c>). A negative level takes <c>FUN_00551560</c>, which picks on
	/// the band alone - <b>and skips <c>0x6b</c></b>, which is the original's own gap and is matched by
	/// the shipped category, where 107 is absent. Anything else takes <c>FUN_00551320</c>'s 4x4 grid,
	/// the band crossed with <see cref="ScreamGridIndex"/>, giving <c>0x4b</c> through <c>0x5a</c>.
	/// </para>
	/// <para>
	/// <b>Both branches are reached by shipped content</b>, which is why neither is a guess: of the 46
	/// uses across all 308 scripts, 44 pass 65535 - which <c>Value</c> sign-extends to -1 - while
	/// <c>Monkey.rse</c> passes 90 and <c>Totem.RSE</c> passes 100, landing on grid steps 2 and 3.
	/// </para>
	/// </summary>
	public static int SingleScreamEffectFor( int band, int level, int speed = ScriptSpeed )
	{
		if ( band <= 0 )
			return 0;

		if ( level < 0 )
			return band switch
			{
				1 => 0x69,
				< 4 => 0x6a,
				< 8 => 0x6c,
				_ => 0x6d
			};

		var step = ScreamGridIndex( level, speed );

		if ( step is < 0 or > 3 )
			return 0;

		var first = band switch
		{
			1 => 0x4b,
			< 4 => 0x4f,
			< 8 => 0x53,
			_ => 0x57
		};

		return first + step;
	}

	public ParkAudio( string themeName )
	{
		Current = this;

		_screams = new ParkScreams( effect => _kids?.VariationsOf( effect ) ?? [], PlayScream, () => Audio.Listener.Position );

		// Nothing to load and nothing to play without a device, exactly as the lobby's does.
		if ( !Audio.Ready )
			return;

		// A category names its banks relative to the LEVEL folder rather than to the folder its own
		// .map files sit in - cat_music's bank path reads "Music\Music" - so the root is the level and
		// the maps are the Music folder beside it. One park category is deliberately not loaded: cat_speech, where
		// the advisor's five bank-1 responses play from (docs/exe/advisor-park.md, the response table's +0x10), and a
		// park here says none of them. cat_rides is loaded below, for the bumper cars' engines (ParkCarSounds) and the
		// scripts' type 3; the level's own cat_ambient with the scripts' other categories (LoadScriptCategories).
		var root = $"levels/{themeName.ToLowerInvariant()}";

		_music = new SoundCategory( root, $"{root}/Music", "music", keep: true );

		if ( !_music.IsValid )
			Log.Warning( $"Park audio: {themeName} has no music category in {root}/Music" );

		// And the weather's, which is not the park's own category at all - see ThunderEffect.
		_ambient = new SoundCategory( "global", "global/sound", "ambient", keep: true );

		if ( !_ambient.IsValid )
			Log.Warning( "Park audio: the global ambient category would not load, so the weather is silent" );

		// The rides' own category, in the level's Sound folder, its banks again relative to the level
		// ("Sound\Ride"). Every caller of the EventMap's slots plays into it ([0x00803a3c]).
		_rides = new SoundCategory( root, $"{root}/Sound", "rides", keep: true );

		if ( !_rides.IsValid )
			Log.Warning( $"Park audio: {themeName} has no rides category in {root}/Sound, so the rides are silent" );

		// The staff's voices, played by StaffSound, and after them the guests', which is where the
		// screams are - see Scream.
		_staff = new SoundCategory( "global", "global/sound", "staff", keep: true );

		if ( !_staff.IsValid )
			Log.Warning( "Park audio: the global staff category would not load, so the staff are silent" );

		_kids = new SoundCategory( "global", "global/sound", "kids", keep: true );

		if ( !_kids.IsValid )
			Log.Warning( "Park audio: the global kids category would not load, so nobody can scream" );
		else
			Log.Info( $"Park audio: kids category ready, screams are {string.Join( ", ", ScreamEffects )}" );

		LoadScriptCategories( root );
	}

	/// <summary>
	/// Thunder for a bolt, sounding where the bolt is rather than flat.
	///
	/// The original plays it as a placed sound - Sound_PlayEffect's fourth, fifth and sixth arguments
	/// are a position, not the single volume a decompile makes them look like - and the place it
	/// passes is the bolt's <i>top</i>, three hundred units up. It is heard over its variation's own range, 1500
	/// (<see cref="SoundCategory.RangeOf"/>). The engine sets no level for it (<c>FUN_00512880</c>: the call
	/// alone), so it plays at its variation's own volume (<see cref="OneShotGain"/>: 85 is 64 of 127 beside the
	/// music's 60) and at a pitch drawn from it (<see cref="OneShotRate"/>: -4 to 3).
	/// </summary>
	/// <returns>Whether anything started.</returns>
	internal bool Thunder( Vector3 at )
	{
		if ( !Audio.Ready || _ambient is not { IsValid: true } )
			return false;

		var voice = _ambient.Play( ThunderEffect, bus: AudioBus.Effects, position: at, ownRange: true,
			gain: OneShotGain, rate: OneShotRate );

		if ( voice == null )
			return false;

		Log.Info( $"Park audio: thunder, effect {ThunderEffect} sample '{voice.Name}' volume {voice.Volume:0.0000} pitch {OneShotPitch} "
			+ $"at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0}) range {voice.Range ?? 0f:0} gain {voice.DistanceGain:0.0000}" );

		return true;
	}

	/// <summary>
	/// <c>FUN_0051e830</c>, called at each step of the drops' ramp with the drops as a hundredth of the most: above
	/// nought the bed is held if it is not and its parameter 8 set, which gives the variation then current its
	/// volume again, <c>level x (high - low) / 100 + low</c> (<see cref="Controlled"/>); at nought it is faded and
	/// let go. The four variations' volumes are 1 to 2, 3, 4 and 5 of a hundred, so the bed is 1 to 3 of 127 beside
	/// the music's 60 (<c>docs/exe/weather.md</c>, "Two rain voices, and how loud each is").
	/// </summary>
	internal void SetRainLevel( int level )
	{
		level = Math.Clamp( level, 0, 100 );

		if ( level == 0 )
		{
			LetGo( RainBed, null );
			return;
		}

		RainBed.Level = level;

		if ( !RainBed.Held )
		{
			// Its first sample starts on the next pass and takes the level as it does.
			RainBed.Held = true;

			Log.Info( $"Park audio: the rain's bed started at level {level} on tick {GameClock.Ticks}" );
			return;
		}

		var headers = _ambient?.VariationsOf( RainEffect ) ?? [];

		if ( RainBed.Variation < 0 || RainBed.Variation >= headers.Count )
			return;

		// The set applies the volume again only where the parameter is one of the variation's own controllers.
		var header = headers[RainBed.Variation];

		if ( header.FirstKey != RainParameter && header.SecondKey != RainParameter )
			return;

		RainBed.Volume = Controlled( header, 1, RainParameter, level, _rainRandom );
		RainBed.Voice?.SetVolume( ChannelGain( RainBed.Volume ) );

		Log.Info( $"Park audio: the rain's bed set to level {level}, variation {RainBed.Variation + 1} volume {RainBed.Volume} "
			+ $"plays {ChannelGain( RainBed.Volume ):F4} on tick {GameClock.Ticks}" );
	}

	/// <summary>
	/// The weather thing's own rain voice, started as a quality turns to rain (<c>FUN_005131f0</c>, mode 0): the same
	/// effect again, held beside the bed. Nothing sets its level, so its controllers stay at the nought a new voice
	/// has (<c>FUN_006b7150</c>): the first variation for ever, at its low volume of 1.
	/// </summary>
	internal void StartOwnRain()
	{
		if ( RainOwn.Held )
			return;

		RainOwn.Held = true;

		Log.Info( $"Park audio: the weather's own rain voice started on tick {GameClock.Ticks}" );
	}

	/// <summary>The weather thing's own rain voice faded and let go: a turn to snow, or the drops at nought (<c>FUN_005131f0</c>, modes 1 and 2).</summary>
	internal void StopOwnRain() => LetGo( RainOwn, null );

	/// <summary>A rain voice let go: faded over <paramref name="seconds"/>, or by <c>Sound_StopFading</c> (<see cref="Voice.StopFading"/>) when null.</summary>
	private static void LetGo( RainVoice rain, float? seconds )
	{
		if ( !rain.Held )
			return;

		if ( seconds is { } fade )
			rain.Voice?.FadeOut( fade );
		else
			rain.Voice?.StopFading();

		rain.Voice = null;
		rain.Held = false;
		rain.Variation = -1;
		rain.Level = 0;
		rain.Volume = 0;
	}

	/// <summary>
	/// A held rain voice's next sample once the last has ended, as the crowd's (<see cref="NextCrowdSample"/>): the
	/// first variation first, then the one the level's zone gives. A voice's first sample takes its volume from the
	/// level as its channel is set up; a later one is queued with its sample alone and plays at what the voice kept.
	/// </summary>
	private void NextRainSample( RainVoice rain, string name )
	{
		if ( !rain.Held || rain.Voice is { Playing: true } || _ambient is not { IsValid: true } )
			return;

		var headers = _ambient.VariationsOf( RainEffect );
		var first = rain.Variation < 0;
		var next = ParkScreams.NextVariation( headers, rain.Variation, rain.Level, _rainRandom );

		if ( next < 0 )
			return;

		rain.Variation = next;

		if ( first )
			rain.Volume = Controlled( headers[next], 1, RainParameter, rain.Level, _rainRandom );

		// The rain voices are the music class (docs/exe/audio.md); global ambient 33 holds lists of one sample, so no pick is kept.
		rain.Voice = Audio.Play( _ambient.PickFrom( RainEffect, next ), ChannelGain( rain.Volume ), bus: AudioBus.Effects );

		if ( rain.Voice is null )
			return;

		++rain.Samples;

		Log.Info( $"Park audio: {name}, sample '{rain.Voice.Name}' variation {next + 1} at level {rain.Level}, volume {rain.Volume} "
			+ $"plays {rain.Voice.Volume:F4} on tick {GameClock.Ticks}" );
	}

	/// <summary>The rain's two voices as they stand, for the console.</summary>
	internal string RainCensus()
	{
		static string One( RainVoice rain )
			=> rain.Held
				? $"held, level {rain.Level} variation {rain.Variation + 1} volume {rain.Volume} plays {rain.Plays:F4}, {rain.Samples} samples"
				: $"not held, {rain.Samples} samples";

		return $"bed {One( RainBed )}; own {One( RainOwn )}";
	}

	/// <summary>
	/// Starts a ride screaming - <c>STARTSCREAM</c>, through <c>FUN_00551130</c>.
	///
	/// <para>
	/// <b>The engine refuses to start a second one over the first</b>, and says so: its own line is
	/// "RSSE: Started screaming without stopping first.". The refusal's nought then goes over the script's
	/// handle (<c>0x00555ee6</c>), so the first scream plays on held by nothing, past the script's next
	/// <c>STOPSCREAM</c> and its teardown alike. This lets it go the same way (<see cref="ParkScreams.LetGo"/>),
	/// to scream until the park ends. No shipped script reaches it: each of the 40 <c>STARTSCREAM</c>s in
	/// the 308 is reached holding no scream.
	/// </para>
	/// </summary>
	/// <param name="scriptId">Whose scream this is, so <see cref="StopScream"/> can find it again.</param>
	/// <param name="band">The first operand: nought is silent, then 1, 2-3 (and any negative), 4-7, 8 and over.</param>
	/// <param name="level">The second operand, averaged with the script speed - see <see cref="ScreamVolume"/>.</param>
	/// <param name="at">Where the ride stands. The engine takes this from the script's own thing.</param>
	/// <returns>Whether anything started, which is what the script holds afterwards.</returns>
	internal bool Scream( int scriptId, int band, int level, Vector3 at, int speed = ScriptSpeed )
	{
		// The handle is tested before anything else (FUN_00551130), so a refusal needs no sound device.
		if ( _screams.LetGo( scriptId ) is { } refused )
		{
			Log.Warning( $"Park audio: script {scriptId} started screaming without stopping first; its scream "
				+ $"of effect {refused.Effect} plays on, held by nothing" );
			return false;
		}

		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		var effect = ScreamEffectFor( band );

		if ( effect == 0 )
		{
			Log.Trace( $"Park audio: script {scriptId} asked for band {band}, which screams not at all" );
			return false;
		}

		Log.Info( $"Park audio: script {scriptId} screaming, band {band} effect {effect} speed {speed} "
			+ $"parameter {ScreamVolume( level, speed )} volume {ScreamChildVolume( effect, 0, level, speed )} "
			+ $"at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0})" );

		// Its first child now if the listener is in range (else on the first pass that finds it so), the rest from
		// OnUpdate, each on this ride's own clock.
		_screams.Start( scriptId, effect, band, level, at, Time.Now, speed );

		return true;
	}

	/// <summary>
	/// Sounds one child of a held scream: a sample of that variation, placed at the ride, at the
	/// variation's own volume (<see cref="ChannelGain"/>) and pitch, heard over the variation's own range
	/// (<see cref="SoundCategory.RangeOf"/>). <see cref="ParkScreams"/> decides when and which; this is only
	/// the sound. One due while the world is held is drawn and given no voice (<see cref="Unheard"/>).
	/// </summary>
	/// <remarks>
	/// A child is a voice of its own, so each is given a volume and a pitch as its channel is set up
	/// (<c>0x006c3ae0</c>: <c>FUN_006bc090</c>, then <c>FUN_006bc170</c>). Parameter 6,
	/// <see cref="ScreamVolume"/>, is the chain's zone parameter and picks the variation (<c>0x006c3e1c</c>); the
	/// volume is the variation's bytes under it, 47 on every shipped scream whatever the parameter, and the pitch
	/// a draw between its two, 0 to 5 on every shipped one (<c>docs/exe/audio.md</c>, "A channel's volume",
	/// "A scream's child and its pitch").
	/// </remarks>
	private Voice? PlayScream( ParkScreams.Chain chain, int variation )
	{
		var volume = ScreamChildVolume( chain.Effect, variation, chain.Level, chain.Speed );

		chain.Pitch = ScreamChildPitch( chain.Effect, variation, chain.Level, chain.Speed );

		if ( Unheard( chain.At ) )
		{
			++chain.Unheard;
			return null;
		}

		var voice = Audio.Play( _kids?.PickFrom( chain.Effect, variation ), ChannelGain( volume ),
			bus: AudioBus.Effects, position: chain.At, range: _kids?.RangeOf( chain.Effect, variation ) );

		if ( voice != null )
		{
			voice.SetRate( ParkCarSounds.Rate( chain.Pitch ) );
			_screamSamples.Add( voice.Name );
		}

		return voice;
	}

	/// <summary>
	/// The volume, of a hundred, of a child of scream <paramref name="effect"/>'s variation
	/// <paramref name="variation"/> under a script's level: the variation's bytes, by a controller of its own
	/// that names the effect's parameter or else a draw between them (<see cref="Controlled"/>). Nought for a
	/// variation the category has not got.
	/// </summary>
	internal int ScreamChildVolume( int effect, int variation, int level, int speed = ScriptSpeed )
		=> _kids?.VariationsOf( effect ) is { } headers && variation >= 0 && variation < headers.Count
			? Controlled( headers[variation], 1, _kids.ParameterOf( effect ), ScreamVolume( level, speed ), _screamRandom )
			: 0;

	/// <summary>
	/// The pitch, in 96ths of an octave, of a child of scream <paramref name="effect"/>'s variation
	/// <paramref name="variation"/> under a script's level, drawn afresh for every child
	/// (<c>FUN_006bc170</c>): the variation's bytes, by a controller of its own that names the effect's
	/// parameter or else a draw between them (<see cref="Controlled"/>). Nought for a variation the category
	/// has not got.
	/// </summary>
	internal int ScreamChildPitch( int effect, int variation, int level, int speed = ScriptSpeed )
		=> _kids?.VariationsOf( effect ) is { } headers && variation >= 0 && variation < headers.Count
			? Controlled( headers[variation], 2, _kids.ParameterOf( effect ), ScreamVolume( level, speed ), _screamRandom )
			: 0;

	private readonly Random _screamRandom = new();

	/// <summary>
	/// A one-shot the engine sets no level for, the kids' or the ambient's thunder (<c>Sound_PlayEffect</c> with no
	/// <c>FUN_0051bc40</c> after it): the picked variation's own volume, its bytes or a draw between them.
	/// </summary>
	private float OneShotGain( SoundCategoryFile.Variation header )
		=> ChannelGain( Controlled( header, 1, 0, 0, _screamRandom ) );

	/// <summary>
	/// The rate a one-shot of the kids' category, or thunder, plays at: its pitch, drawn between the picked variation's two
	/// bytes as its channel is set up (<c>FUN_006bc410</c>: <c>FUN_006bc170</c>; <c>docs/exe/audio.md</c>,
	/// "A one-shot's pitch"), kept in <see cref="OneShotPitch"/> for the caller's line in the log.
	/// </summary>
	private float OneShotRate( SoundCategoryFile.Variation header )
	{
		OneShotPitch = Controlled( header, 2, 0, 0, _screamRandom );

		return ParkCarSounds.Rate( OneShotPitch );
	}

	/// <summary>The pitch, in 96ths of an octave, the last one-shot drew.</summary>
	internal int OneShotPitch { get; private set; }

	/// <summary>
	/// Stops a ride screaming - <c>STOPSCREAM</c>: its own chain is cut and forgotten, and no other
	/// ride's is touched, the same band's included (<c>0x006bd9b0</c>). Nothing is shared per effect
	/// in the original, so there is nothing to let go of.
	/// </summary>
	internal bool StopScream( int scriptId )
	{
		if ( _screams.Stop( scriptId ) is not { } chain )
			return false;

		Log.Info( $"Park audio: script {scriptId} stopped screaming after {chain.Plays} sample(s) "
			+ $"of effect {chain.Effect}" );

		return true;
	}

	/// <summary>
	/// <c>SINGLESCREAM</c> - one scream, once, belonging to nobody: the engine keeps no handle for it
	/// and sets no level (<c>FUN_00551320</c> and <c>FUN_00551560</c>, neither of which touches
	/// <c>FUN_0051bc40</c>), so it plays at its variation's own volume (<see cref="OneShotGain"/>) and at a pitch drawn from it
	/// (<see cref="OneShotRate"/>: -8 to 7 on effects 75 to 90). See
	/// <see cref="SingleScreamEffectFor"/> for which one.
	/// </summary>
	/// <returns>Whether anything started.</returns>
	internal bool SingleScream( int band, int level, Vector3 at, int speed = ScriptSpeed )
	{
		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		var effect = SingleScreamEffectFor( band, level, speed );

		if ( effect == 0 )
		{
			Log.Trace( $"Park audio: a single scream at band {band} level {level} plays nothing" );
			return false;
		}

		var voice = _kids.Play( effect, bus: AudioBus.Effects, position: at, ownRange: true, gain: OneShotGain, rate: OneShotRate );

		if ( voice == null )
			return false;

		_screamSamples.Add( voice.Name );

		Log.Info( $"Park audio: single scream, band {band} level {level} speed {speed} effect {effect} "
			+ $"sample '{voice.Name}' volume {voice.Volume:0.0000} pitch {OneShotPitch} at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0})" );

		return true;
	}

	/// <summary>
	/// The kids' effect <c>0x80</c>, which the original plays for a guest put off a ride by a sale
	/// (<c>FUN_004fb360</c>), for one put out of a queue by any of its seven routes when their thing id is a
	/// multiple of eight (<c>FUN_005012f0</c>, <c>0x0050133d</c>; all seven are built, the <c>InQueue</c> turn in
	/// part - <c>docs/exe/ride-operation.md</c>, "Every way out of a queue"), and for one it throws out of the park
	/// (<c>FUN_004feb50</c>). What the sample says has not been
	/// checked by listening.
	/// </summary>
	/// <remarks>
	/// The engine sets no level for it (<c>Sound_PlayEffect</c> with no handle, no <c>FUN_0051bc40</c>), as
	/// for <c>SINGLESCREAM</c>, so it plays at its variation's own volume (<see cref="OneShotGain"/>) and at a
	/// pitch drawn from it (<see cref="OneShotRate"/>: -5 to 4).
	/// It is not held back by the repeat delay: a sale puts several guests off at once, and the original
	/// throttles nothing.
	/// </remarks>
	/// <returns>Whether anything started.</returns>
	internal bool PutOff( Vector3 at )
	{
		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		var voice = _kids.Play( PutOffEffect, respectDelay: false, bus: AudioBus.Effects,
			position: at, ownRange: true, gain: OneShotGain, rate: OneShotRate );

		if ( voice == null )
			return false;

		Log.Info( $"Park audio: put off, effect {PutOffEffect} sample '{voice.Name}' volume {voice.Volume:0.0000} pitch {OneShotPitch} "
			+ $"at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0}) range {voice.Range ?? 0f:0} gain {voice.DistanceGain:0.0000}" );

		return true;
	}

	/// <summary>
	/// A <c>cat_staff</c> effect where a member of staff stands (<c>FUN_004faa00</c>, <c>Sound_PlayEffect</c> with no
	/// handle): a variation evenly, a sample of it by weight, and the volume and pitch drawn from the variation's
	/// own bytes (<c>0x006bbbe0</c>; <c>docs/exe/audio.md</c>, "The staff's voices"), heard over the variation's
	/// own range (<see cref="SoundCategory.RangeOf"/>). Most draws land on the bank's 9 ms blank sample, which is
	/// played as any other.
	/// </summary>
	/// <remarks>
	/// The drawn volume plays at the channel's own number (<see cref="ChannelGain"/>): 24 is 18 of 127 beside the
	/// music's 60. It is not held back by the repeat delay, which the original does not have.
	/// </remarks>
	/// <returns>Whether anything started.</returns>
	internal bool StaffSound( int effect, Vector3 at )
	{
		if ( !Audio.Ready || _staff is not { IsValid: true } )
			return false;

		var headers = _staff.VariationsOf( effect );

		if ( headers.Count == 0 )
			return false;

		var variation = _staffRandom.Next( headers.Count );
		var volume = Controlled( headers[variation], 1, 0, 0, _staffRandom );
		var pitch = Controlled( headers[variation], 2, 0, 0, _staffRandom );
		var voice = Audio.Play( _staff.PickFrom( effect, variation ), ChannelGain( volume ),
			bus: AudioBus.Effects, position: at, range: _staff.RangeOf( effect, variation ) );

		if ( voice == null )
			return false;

		voice.SetRate( ParkCarSounds.Rate( pitch ) );
		StaffSounds = (StaffSounds.Started + 1, StaffSounds.Voiced + (voice.Name.StartsWith( BlankSample ) ? 0 : 1));

		Log.Info( $"Park audio: staff effect 0x{effect:x} sample '{voice.Name}' volume {volume} plays {voice.Volume:0.0000} pitch {pitch} "
			+ $"at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0}) range {voice.Range ?? 0f:0} gain {voice.DistanceGain:0.0000}" );

		return true;
	}

	/// <summary>The 9 ms silent sample most of a staff effect's weight lands on, by its name's start.</summary>
	private const string BlankSample = "blank44";

	/// <summary>The kids' category effect a guest put off something plays - see <see cref="PutOff"/>.</summary>
	private const int PutOffEffect = 0x80;

	/// <summary>The kids' effect the visitor's window plays as it is built, <c>0x81</c> (<c>FUN_004b79b0</c>).</summary>
	internal const int VisitorWindowEffect = 0x81;

	/// <summary>
	/// The kids' effect <c>0x81</c> as the visitor's window is built: <c>Sound_PlayEffect( 0, [0x00803a24], 0x81, 0, 0, 0 )</c>,
	/// no handle kept and no level set, so flat, at its variation's own volume (<see cref="OneShotGain"/>) and a pitch drawn
	/// from it (<see cref="OneShotRate"/>), as the crowd's voice at (0,0,0) is played. What the sample says has not been
	/// checked by listening. Not the original's in one way: the original plays it on every build and throttles nothing,
	/// while this call keeps <see cref="SoundCategory"/>'s per-effect throttle (this project's reading of a priority
	/// field, Q43), so a window opened again within the clip's length plus the effect's RepeatDelay is silent;
	/// <c>PutOff</c> and the yawn pass <c>respectDelay: false</c> for that reason.
	/// </summary>
	/// <returns>Whether anything started.</returns>
	internal bool VisitorWindowOpened()
	{
		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		var voice = _kids.Play( VisitorWindowEffect, bus: AudioBus.Effects, gain: OneShotGain, rate: OneShotRate );

		if ( voice == null )
			return false;

		Log.Info( $"Park audio: the visitor's window, effect {VisitorWindowEffect} sample '{voice.Name}' volume {voice.Volume:0.0000} pitch {OneShotPitch}" );

		return true;
	}

	/// <summary>
	/// The kids' effect <c>0x7e</c>, a yawn, which the original plays at a guest starting spot animation 4 when
	/// their id's low nibble is nought (<c>FUN_004fc800</c>, <c>0x004fc84e</c>). What the three samples say has
	/// not been checked by listening; they are named <c>yawn1</c>, <c>yawn2a</c> and <c>yawn3a</c>.
	/// </summary>
	/// <remarks>
	/// The engine sets no level for it, as for <see cref="PutOff"/>, so it plays at its variation's own volume,
	/// and it is not held back by the repeat delay.
	/// </remarks>
	/// <returns>Whether anything started.</returns>
	internal bool Yawn( Vector3 at )
	{
		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		var voice = _kids.Play( YawnEffect, respectDelay: false, bus: AudioBus.Effects,
			position: at, ownRange: true, gain: OneShotGain, rate: OneShotRate );

		if ( voice == null )
			return false;

		Log.Info( $"Park audio: yawn, effect {YawnEffect} sample '{voice.Name}' volume {voice.Volume:0.0000} pitch {OneShotPitch} "
			+ $"at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0})" );

		return true;
	}

	/// <summary>
	/// The kids' effect <c>0xd0</c>, a till (<c>cashD2b.mp2</c>), which <c>Person_PayCharge</c> plays at the guest who
	/// pays (<c>0x004fe1be</c>..<c>0x004fe1cb</c> through <c>Person_PlaySound</c>; <c>docs/exe/audio.md</c>, "The charge's
	/// sound"). No handle and no level, so a plain one-shot at its variation's own volume, over its own range, and not
	/// held back by the repeat delay.
	/// </summary>
	/// <returns>Whether anything started.</returns>
	internal bool Charge( Vector3 at )
	{
		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		var voice = _kids.Play( ChargeEffect, respectDelay: false, bus: AudioBus.Effects,
			position: at, ownRange: true, gain: OneShotGain, rate: OneShotRate );

		if ( voice == null )
			return false;

		Log.Info( $"Park audio: charge, effect {ChargeEffect} sample '{voice.Name}' volume {voice.Volume:0.0000} pitch {OneShotPitch} "
			+ $"at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0}) range {voice.Range ?? 0f:0}" );

		return true;
	}

	/// <summary>The kids' category effect a paying guest plays - see <see cref="Charge"/>.</summary>
	private const int ChargeEffect = 0xd0;

	/// <summary>The kids' category effect a bored guest's yawn plays - see <see cref="Yawn"/>.</summary>
	private const int YawnEffect = 0x7e;

	/// <summary>
	/// <c>SCREAMLEVEL</c> - moves the parameter of the scream a script is already holding, by the same
	/// <c>(operand + speed) / 2</c> the start used (<c>FUN_00551290</c>, parameter 6). The chain's later
	/// children pick their variation by it. The child playing takes a new volume only where its variation
	/// names the parameter on its volume (<c>0x006bbb80</c>, a match past slot 0), which no shipped scream
	/// does. It does nothing at all when no scream is held, which is the engine's own guard on a nought handle.
	/// </summary>
	/// <remarks>
	/// The engine writes the parameter call's answer back over the scream handle at <c>+0xd0</c>
	/// (<c>0x00556009</c>): the handle itself while the voice lives (<c>0x006b6620</c>), nought with no sound device
	/// (<c>0x0051bc6d</c>). So with a device keeping the held scream is the engine's own outcome; the level is carried
	/// on it, so a replay keeps it. With none, no scream is held here to move (<see cref="Scream"/>).
	/// </remarks>
	/// <returns>Whether a scream was there to change.</returns>
	internal bool ScreamLevel( int scriptId, int level, int speed = ScriptSpeed )
	{
		if ( _screams.Find( scriptId ) is not { } scream )
			return false;

		scream.Level = level;
		scream.Speed = speed;

		var parameter = ScreamVolume( level, speed );

		if ( _kids?.VariationsOf( scream.Effect ) is { } headers && scream.Variation >= 0
			&& scream.Variation < headers.Count && NamesOnVolume( headers[scream.Variation], _kids.ParameterOf( scream.Effect ) ) )
			scream.Voice?.SetVolume( ChannelGain( ScreamChildVolume( scream.Effect, scream.Variation, level, speed ) ) );

		Log.Info( $"Park audio: script {scriptId} scream level {level} speed {speed} -> parameter {parameter}" );

		return true;
	}

	/// <summary>What a script's scream is doing, for the <c>rides</c> census. A pure getter.</summary>
	internal string ScreamState( int scriptId )
		=> _screams.Find( scriptId ) is { } scream
			? $"effect {scream.Effect} band {scream.Band} level {scream.Level} speed {scream.Speed} variation {scream.Variation + 1} "
				+ $"sample '{scream.Sample}' pitch {scream.Pitch} plays {scream.Plays} next in {Math.Max( 0f, scream.NextAt - Time.Now ):0.00}s"
				+ (scream.Voice is { Playing: true } ? "" : " (between)")
				+ (scream.Away ? " away" : "")
			: "none";

	/// <summary>
	/// The scream a script is holding, or null - what <see cref="RideScript.Screaming"/> answers from. The
	/// tests read its clock from here.
	/// </summary>
	internal ParkScreams.Chain? HeldScream( int scriptId ) => _screams.Find( scriptId );

	/// <summary>How many distinct scream samples this park has played, across every ride.</summary>
	internal int ScreamSamplesHeard => _screamSamples.Count;

	/// <summary>How many screams a second start has left held by nothing, still making children.</summary>
	internal int ScreamsLetGo => _screams.LetGoCount;

	/// <summary>The sound effects' group level, of a hundred: the options' default (<c>sound.sam</c>, <c>DefaultVolume</c>).</summary>
	internal const int EffectsGroupLevel = 75;

	/// <summary>The music's group level, of a hundred, which <see cref="MusicVolume"/> stands for.</summary>
	internal const int MusicGroupLevel = 60;

	/// <summary>
	/// What a sound effect's volume, of a hundred, plays at here before <see cref="Audio.MasterVolume"/>: the
	/// original's whole number of 127, the volume times the group's level rounded to even
	/// (<c>FUN_006bb860</c>, handed to <c>FUN_006d2980</c>), beside the music's 60
	/// (<c>docs/exe/audio.md</c>, "A channel's volume").
	/// </summary>
	internal static float ChannelGain( int volume )
		=> MathF.Round( volume * EffectsGroupLevel / 100f, MidpointRounding.ToEven ) / MusicGroupLevel * MusicVolume;

	/// <summary>The land's own sounds that are sounding, with what each was started from.</summary>
	internal IReadOnlyList<(ParkPlacedObjects.Sound Sound, int Volume, Voice? Voice)> Placed => _placed;

	private readonly List<(ParkPlacedObjects.Sound Sound, int Volume, Voice? Voice)> _placed = [];

	/// <summary>
	/// Starts one of the land's own sounds where it stands (<c>FUN_0051c130</c>): a looped voice of the global
	/// <c>cat_ambient</c>, at its first variation's volume, turned down by the listener's distance inside its
	/// range. False, and nothing started, for an effect that is not a looped one or has nothing to play.
	/// </summary>
	internal bool StartPlaced( ParkPlacedObjects.Sound sound )
	{
		if ( !PlaysPlaced( _ambient, sound.Effect ) )
			return false;

		var header = _ambient!.VariationsOf( sound.Effect )[0];
		var volume = Controlled( header, 1, 0, 0, _musicRandom );

		var voice = _ambient.Play( sound.Effect, ChannelGain( volume ), loop: true, respectDelay: false,
			bus: AudioBus.Effects, position: sound.Place, range: sound.Range );

		_placed.Add( (sound, volume, voice) );

		Log.Info( $"Park audio: the land's own ambient {sound.Effect} started at ({sound.Place.X:F0},{sound.Place.Y:F0}), "
			+ $"range {sound.Range:F0}, volume {volume}" );

		return true;
	}

	/// <summary>Whether <paramref name="effect"/> of <paramref name="category"/> is one a placed record can loop: flagged looped, with a variation to play.</summary>
	internal static bool PlaysPlaced( SoundCategory? category, int effect )
		=> category is { IsValid: true }
			&& (category.FlagsOf( effect ) & SoundCategory.LoopedFlag) != 0
			&& category.VariationsOf( effect ).Count > 0;

	/// <summary>
	/// One of the land's own sounds that is kept beside the listener (a <c>scape.omp</c> type 3; the Lost Kingdom's
	/// gulls and its sea): a voice of the class that plays one sample after another (<c>FUN_006be090</c>).
	/// </summary>
	internal sealed class FollowingVoice( ParkPlacedObjects.Following sound )
	{
		/// <summary>The record it was started from.</summary>
		public ParkPlacedObjects.Following Sound { get; } = sound;

		/// <summary>The variation its sample is of, zero-based, or -1 before its first.</summary>
		public int Variation { get; set; } = -1;

		/// <summary>The volume, of a hundred, and the pitch its first sample drew, which every later one plays at.</summary>
		public (int Volume, int Pitch) Kept { get; set; }

		/// <summary>The sample the voice's last draw from a list of two or more took, -1 for none: the original's voice byte <c>+0x46</c>, which a change of variation leaves alone (<c>0x006bc747</c>, <c>docs/exe/audio.md</c>, "The sample draw").</summary>
		public int Last = -1;

		/// <summary>The silence after the sample that is playing, in milliseconds: the variation's wait, none after the first.</summary>
		public int Wait { get; set; }

		/// <summary>When the last sample was found ended, or null while one plays.</summary>
		public float? EndedAt { get; set; }

		/// <summary>How many samples it has started since the park began.</summary>
		public int Samples { get; set; }

		/// <summary>How many samples it drew and let pass with no voice while the world was held (<see cref="ParkAudio.Unheard"/>).</summary>
		public int Unheard { get; set; }

		public Voice? Voice { get; set; }
	}

	/// <summary>The land's own sounds that follow the listener, as they stand.</summary>
	internal IReadOnlyList<FollowingVoice> Following => _following;

	private readonly List<FollowingVoice> _following = [];
	private readonly Random _followingRandom = new();

	/// <summary>
	/// Starts one of the land's own sounds that is kept beside the listener (<c>0x00550fe5</c>): a voice of the
	/// global <c>cat_ambient</c> that plays one sample after another, moved each frame to the listener's place held
	/// inside its rectangle. False, and nothing started, for an effect of another class or with nothing to play.
	/// </summary>
	internal bool StartFollowing( ParkPlacedObjects.Following sound )
	{
		if ( !PlaysFollowing( _ambient, sound.Effect ) )
			return false;

		_following.Add( new FollowingVoice( sound ) );

		Log.Info( $"Park audio: the land's own ambient {sound.Effect} started, kept inside ({sound.X},{sound.Z}) to ({sound.FarX},{sound.FarZ})" );

		return true;
	}

	/// <summary>
	/// Whether <paramref name="effect"/> of <paramref name="category"/> makes the voice that plays one sample after
	/// another with no parameter (<c>0x006b677d</c>..<c>0x006b67c3</c>: flags <c>0x4</c> and <c>0x2</c>, and none of
	/// <c>0x10</c>, <c>0x400</c> and <c>0x100</c>), and has a variation to play.
	/// </summary>
	internal static bool PlaysFollowing( SoundCategory? category, int effect )
		=> category is { IsValid: true }
			&& (category.FlagsOf( effect ) & 0x516) == 0x6
			&& category.VariationsOf( effect ).Any( header => header.Samples > 0 );

	/// <summary>
	/// The variation such a voice's next sample is of (<c>FUN_006bc810</c>): a draw by weight among all of the
	/// effect's, the first whose running total of weights is at least the roll (<c>0x006bc86d</c>..<c>0x006bc872</c>), and
	/// for a sample after the first, of more than two, never the one before: the next round takes its
	/// place. -1 with none.
	/// </summary>
	internal static int AnyVariation( IReadOnlyList<SoundCategoryFile.Variation> variations, int last, Random random )
	{
		if ( variations.Count < 2 )
			return variations.Count - 1;

		var total = variations.Sum( variation => variation.Weight );
		var roll = total > 0 ? random.NextInt64( total ) : 0;
		var drawn = variations.Count - 1;

		for ( var index = 0; index < variations.Count; ++index )
		{
			roll -= variations[index].Weight;

			if ( roll <= 0 )
			{
				drawn = index;
				break;
			}
		}

		return variations.Count > 2 && drawn == last ? (drawn + 1) % variations.Count : drawn;
	}

	/// <summary>
	/// Whether a sample or a chain's child due now at <paramref name="place"/> is drawn and let pass with no voice:
	/// a placed one while the world is held (<see cref="Audio.PlacedHeld"/>). The original's service runs on wall time
	/// through a pause and tests a voice's range over x and z alone (<c>FUN_006bb9f0</c>), which its pause's lifted
	/// listener leaves as they were, so its voices go on drawing samples and making children that its mixer plays at
	/// nought (<c>docs/exe/audio.md</c>, "The pause substitution"). Ours holds a placed voice instead of lifting the
	/// listener, and one started under the hold would be born held and let go with all the others on resume; so the
	/// draw is made and its time passes, with no voice. One that would still be sounding when the hold ends is not
	/// heard from where it has got to, as the original's is.
	/// </summary>
	private static bool Unheard( Vector3? place ) => place is not null && Audio.PlacedHeld;

	/// <summary>When a sample of <paramref name="clip"/> drawn now at <paramref name="pitch"/>, and let pass unheard, would end.</summary>
	private static float UnheardEnd( AudioClip? clip, int pitch ) => Time.Now + (clip?.Duration ?? 0f) / ParkCarSounds.Rate( pitch );

	/// <summary>
	/// Each following voice moved beside the listener (<c>FUN_006bd5f0</c>, each service pass), and its next sample
	/// started once the last has ended and its wait is over. A voice's first sample draws the volume and pitch it
	/// keeps (<c>FUN_006bc410</c>); every sample, the first included, is followed by a silence drawn from its variation's
	/// wait (<c>0x006bdf00</c>, <c>FUN_006c6730</c>). While the world is held a sample due is drawn and let pass unheard
	/// (<see cref="Unheard"/>).
	/// </summary>
	private void ServeFollowing()
	{
		if ( _ambient is not { IsValid: true } )
			return;

		var ears = Camera.Ears;

		foreach ( var following in _following )
		{
			var place = following.Sound.Beside( ears );

			if ( following.Voice is { Playing: true } )
			{
				following.Voice.MoveTo( place );
				continue;
			}

			following.EndedAt ??= Time.Now;

			if ( Time.Now < following.EndedAt + (following.Wait / 1000f) )
				continue;

			var headers = _ambient.VariationsOf( following.Sound.Effect );
			var first = following.Variation < 0;
			var next = AnyVariation( headers, following.Variation, _followingRandom );

			if ( next < 0 )
				continue;

			following.Variation = next;
			following.Wait = ParkScreams.GapMilliseconds( headers[next], _followingRandom );
			following.Kept = HeldSample( headers[next], first, following.Kept, 0, 0, _followingRandom );
			following.EndedAt = null;

			var clip = _ambient.PickFrom( following.Sound.Effect, next, ref following.Last, avoidRepeat: !first );

			if ( Unheard( place ) )
			{
				following.Voice = null;
				following.EndedAt = UnheardEnd( clip, following.Kept.Pitch );
				++following.Unheard;
				Log.Info( $"Park audio: the land's own ambient {following.Sound.Effect}, variation {next + 1} unheard while the world is held, ending at {following.EndedAt:F3} s, {following.Wait} ms of silence after it" );
				continue;
			}

			following.Voice = Audio.Play( clip, ChannelGain( following.Kept.Volume ),
				bus: AudioBus.Effects, position: place, range: headers[next].Range );

			if ( following.Voice is null )
				continue;

			following.Voice.SetRate( ParkCarSounds.Rate( following.Kept.Pitch ) );
			++following.Samples;

			Log.Info( $"Park audio: the land's own ambient {following.Sound.Effect}, sample '{following.Voice.Name}' variation {next + 1} "
				+ $"volume {following.Kept.Volume} plays {following.Voice.Volume:F4} pitch {following.Kept.Pitch} "
				+ $"at ({place.X:F0},{place.Y:F0},{place.Z:F0}), {following.Wait} ms of silence after it" );
		}
	}

	/// <summary>The land's own sounds as they stand, for the console.</summary>
	internal string PlacedCensus()
	{
		var ears = Camera.Ears;
		var lines = new List<string> { $"listener ({ears.X:F0},{ears.Y:F0},{ears.Z:F0}), {_placed.Count} placed, {_following.Count} following" };

		foreach ( var (sound, volume, voice) in _placed )
		{
			var distance = (sound.Place - ears).Length;

			lines.Add( $"ambient {sound.Effect} at ({sound.Place.X:F0},{sound.Place.Y:F0},{sound.Place.Z:F0}) range {sound.Range:F0} "
				+ $"volume {volume} distance {distance:F2} law {AudioListener.RangeGain( distance, sound.Range ):F4} "
				+ $"gain {voice?.DistanceGain ?? -1f:F4} {(voice is null ? "no voice" : voice.IsHeld ? "held" : voice.Playing ? "sounding" : "ended")}" );
		}

		foreach ( var following in _following )
		{
			var sound = following.Sound;
			var place = sound.Beside( ears );
			var distance = (place - ears).Length;
			var range = following.Voice?.Range ?? 0f;
			var sounding = following.Voice is { Playing: true };

			lines.Add( $"ambient {sound.Effect} follows inside ({sound.X},{sound.Z}) to ({sound.FarX},{sound.FarZ}) "
				+ $"at ({place.X:F0},{place.Y:F0},{place.Z:F0}) range {range:F0} variation {following.Variation + 1} "
				+ $"volume {following.Kept.Volume} pitch {following.Kept.Pitch} distance {distance:F2} "
				+ $"law {AudioListener.RangeGain( distance, range ):F4} gain {(sounding ? $"{following.Voice!.DistanceGain:F4}" : "none")} "
				+ $"{following.Samples} samples, {(sounding ? $"sounding '{following.Voice!.Name}'" : $"silent for {following.Wait} ms")}" );
		}

		return string.Join( "; ", lines );
	}

	/// <summary>Both of the rain's voices faded and let go: the drops at nought, or the weather gone.</summary>
	internal void StopRain()
	{
		LetGo( RainBed, null );
		LetGo( RainOwn, null );
	}

	/// <summary>The park is ending: what it has playing fades, and its effect is let go.</summary>
	protected override void OnDelete()
	{
		_voice?.FadeOut( StopSeconds );
		_voice = null;

		LetGo( RainBed, StopSeconds );
		LetGo( RainOwn, StopSeconds );

		// And whoever is still screaming as the park ends: each chain's newest child is cut, as a stop
		// cuts it. The effects hold nothing to let go of.
		_screams.StopAll();

		_crowd?.FadeOut( StopSeconds );
		_crowd = null;
		CrowdVoiceHeld = false;

		// The land's own, each a looped voice holding its effect.
		foreach ( var (sound, _, voice) in _placed )
		{
			voice?.FadeOut( StopSeconds );
			_ambient?.Release( sound.Effect );
		}

		_placed.Clear();

		foreach ( var following in _following )
			following.Voice?.FadeOut( StopSeconds );

		_following.Clear();

		StopScriptSounds();

		// The voice was holding the effect - see SoundCategory.Play - so the category has to be told
		// it may start again, or the next park in this process waits out a delay counted against a
		// voice that is no longer playing.
		_music?.Release( MusicEffect );

		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// Starts the theme again when it runs out.
	///
	/// One sample follows another with no wait: once the voice has ended the next is drawn from the variation
	/// the crowd's level picks (<see cref="NextMusic"/>) and played straight from the bank, which the category's
	/// throttle does not hold. With no zone holding the level nothing is started until one does.
	/// </summary>
	protected override void OnUpdate()
	{
		// What the park's menu does to sound. The engine supplies the hold and the park says when, the
		// same split the rest of this file sits on - and <see cref="Audio.HoldPlaced"/> carries the
		// whole account of what is restoration here and what is ours.
		//
		// Before the guards below, not after: a theme whose music would not load must still go quiet
		// when the world is held.
		//
		// GameClock.Paused rather than the window stack, because this is about the WORLD being held
		// rather than about a window being open, and it is already correct for this frame -
		// Level.Update sets it from PausedByWindow() before it updates any entity. The debug console's
		// own `pause` is deliberately NOT this: that one stops Time so a screenshot repeats, and a
		// park whose screams fell silent for it would be reporting the instrument rather than the game.
		Audio.HoldPlaced( GameClock.Paused );

		// The screams are pumped BEFORE the music's own guard below, because a theme whose music would
		// not load must still let its rides scream; and through a park's menu, as the original's service ticks
		// its chains on wall time through a pause (0x0051bfab, 0x005f5fa0): a child due under the hold is made
		// with no voice (PlayScream, Unheard), and after the resume the next comes at the time the clock gives it.
		if ( Audio.Ready )
			_screams.Pump( Time.Now );

		if ( !Audio.Ready )
			return;

		// Re-counted rather than told when the crowd changes, as the original does, and on its beat: FUN_0051e790
		// runs from the park loop at 0x0054f870 on every 32nd tick, about once a second, and not while the game
		// is held, when no tick runs. Guests arrive by bus and leave, so the level moves with them. Asked once a frame after the
		// park's loop, where the original asks in the 32nd tick's own step: a later sweep in the same frame is counted (Q1120).
		if ( SetsMusicLevel( GameClock.Ticks, GameClock.TicksDue ) )
		{
			var worldState = Level.Current?.Park?.WorldState ?? 0;

			if ( _music is { IsValid: true } )
			{
				var guests = ParkPeople.Current?.GateGuestCensus ?? 0;
				var level = MusicLevelFor( ParkPeople.Current, worldState );

				if ( level != LevelNow )
					Log.Info( $"Park music: level {LevelNow} to {level} for {guests} counted guests on tick {GameClock.Ticks}" );

				LevelNow = level;
				++LevelSets;
			}

			// The same block's second call (0x0054f875 to 0x0054f8b9): the crowd's own voice, set from the guests
			// within four cells of the cell under the pointer (docs/exe/audio.md, "The crowd's voice").
			var cell = ParkPicking.Cell;
			var near = ParkPeople.Current?.GuestsNear( cell, CrowdVoiceReach ) ?? 0;
			var crowd = CrowdVoiceLevel( near, worldState );

			if ( crowd != CrowdVoiceLevelNow )
				Log.Info( $"Crowd voice: level {CrowdVoiceLevelNow} to {crowd} for {near} guests near cell {cell} on tick {GameClock.Ticks}" );

			CrowdVoiceGuests = near;
			CrowdVoiceCell = cell;
			SetCrowdVoice( crowd );

			// And its third (0x0054f8c8): FUN_0055ab50, the flying cars' rectangle, which nothing here flies in.
			Unimplemented.Report( "PARK_LOOP_FUN_0055AB50" );
		}

		NextCrowdSample();
		NextRainSample( RainBed, "the rain's bed" );
		NextRainSample( RainOwn, "the weather's own rain voice" );
		ServeFollowing();
		ServeScriptSounds();

		if ( _music is not { IsValid: true } )
			return;

		if ( _voice is { Playing: true } )
			return;

		// One sample after another: the next is drawn from the variation the level's zone gives, at the music's
		// one loudness. With no zone holding the level the voice waits, as the original's is parked (0x006be55e).
		if ( NextMusic( _music.VariationsOf( MusicEffect ), VariationNow, LevelNow, _musicRandom ) is not { } next )
			return;

		if ( next.Variation != VariationNow )
			Log.Info( $"Park music: variation {VariationNow + 1} to {next.Variation + 1} at level {LevelNow} on tick {GameClock.Ticks}" );

		var first = VariationNow < 0;

		VariationNow = next.Variation;
		_voice = Audio.Play( _music.PickFrom( MusicEffect, next.Variation, ref _musicLast, avoidRepeat: !first ), next.Volume, bus: AudioBus.Music );

		if ( _voice is not null )
			Log.Info( $"Park music: sample '{_voice.Name}' variation {next.Variation + 1} volume {next.Volume} on tick {GameClock.Ticks}" );
	}
}
