namespace OpenTPW;

/// <summary>
/// A park's music - which is, for now, the whole of what a park makes a noise with.
///
/// <para>
/// The original loads four categories as a park comes up and plays one thing. Its state machine
/// registers cat_ambient, cat_rides, cat_speech and cat_music (FUN_0051ec50 at 0x0054ec92), re-applies
/// the group volumes (Sound_ApplyGroupVolumes 0x0051bd70, at 0x0054ec9a), and then calls FUN_0051e730
/// at 0x0054ec9f, which plays <b>effect 2 of cat_music</b> - the only effect that category declares -
/// and keeps the voice. That order is the order this is built in.
/// </para>
///
/// <para>
/// <b>And then it turns it down to nothing.</b> The very next line is FUN_0051bc40( voice, 4, 0 ),
/// which sets that voice's level to zero, and the park loop drives the level every pass afterwards
/// (FUN_0051e790 at 0x0054f870). What it drives it from is a count: FUN_004c81e0 is
/// <c>clamp( things / 2, 0, 100 )</c>, clamped again to 89 by the caller, where the count walks the
/// park's thing list and takes everything passing one of five type tests (FUN_004c7fa0 into
/// FUN_004fa990) - people, in other words. <b>So in an empty park the original's music is silent</b>,
/// and it swells as the park fills up.
/// </para>
///
/// <para>
/// <b>It is played that way here now, and the deviation this paragraph used to describe is retired.</b>
/// It said the fixed level was deliberate "because there are no guests yet, so the faithful reading is
/// silence", and named the exact condition for ending it: when guests exist. They do - see
/// <see cref="ParkPeople"/> - so the crowd drives the level, and an empty park really is silent.
/// <b>The shipped park is quiet, and that is the original's design rather than a fault</b>: thirteen
/// guests give a level of six out of a hundred, because the music is meant to swell as a park fills.
/// </para>
///
/// <para>
/// Engine and content: which category and which effect a park plays, and how loud, is this park's
/// content, played through the engine's <see cref="Audio"/> and <see cref="SoundCategory"/> - the same
/// split <see cref="LobbyAudio"/> sits on.
/// </para>
/// </summary>
public sealed class ParkAudio : Entity
{
	/// <summary>The one park audio system. There is only ever one, and none in the lobby.</summary>
	internal static ParkAudio? Current { get; private set; }

	/// <summary>
	/// The effect the original plays, and the only one cat_music declares - in all four themes.
	/// It picks between six arrangements in jungle, five in hallow and space, seven in fantasy; each
	/// runs about eight and a half seconds and the effect waits ten before it may go again.
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

	/// <summary>
	/// How loud thunder is, and this one is <b>carried across on evidence rather than chosen</b>: the
	/// lobby's thunder and a park's are the same recordings. SfxHD.sdt, which the lobby's
	/// globallobbysfx draws on, holds Thunder2, Thunder3 and Thunder4 as well, two of the three at
	/// byte-identical sizes to the ambient bank's. So LobbyAudio's measured 0.40 is this park's too.
	/// </summary>
	private const float ThunderVolume = 0.40f;

	/// <summary>
	/// The loudest the rain bed gets, at every drop the balance file allows.
	///
	/// <para>
	/// <b>Measured, not chosen - and the first guess at 0.30 was more than twice this.</b> RAIN.mp2,
	/// pulled out of AmbientHD.sdt, measures <b>-16.9 dBFS RMS</b> and peaks at full scale. The three
	/// thunder samples beside it measure -19.9, -14.7 and -16.1, a median of -16.1 - so rain and
	/// thunder are recorded within a decibel of each other and the samples give no reason to treat one
	/// as louder than the other.
	/// </para>
	/// <para>
	/// What differs is the role, and <see cref="LobbyAudio"/> is an established scale for it: music
	/// 0.50, thunder 0.40, one-shots 0.14, a scene's own bed 0.13, the global bed 0.07. Rain is a
	/// continuous loop belonging to the scene, which is what a bed is, and beds sit about 9.8 dB below
	/// thunder - <c>20*log10(0.13/0.40)</c>. With the samples level, that puts rain at the bed's own
	/// 0.13. The headroom argument agrees: these are 1999 samples mastered hard against the rails, and
	/// LobbyAudio's remarks warn that modest-looking gains still sum past 1.0 and clip. Rain, thunder
	/// and music together come to 0.86 before <see cref="Audio.MasterVolume"/> and 0.43 after it.
	/// </para>
	/// <para>
	/// The original is no help here, which is why this rests on the game's own scale instead: it drives
	/// the voice 0 to 100 through a control slot (FUN_0051bc40 selector 8) whose default sits behind a
	/// runtime pointer that cannot be resolved from the binary.
	/// </para>
	/// <para>
	/// <b>Then confirmed out of the mixer, which is the half that settles it.</b> Capturing through
	/// SDL's disk driver and measuring a dry window against a wet one, the rain's own contribution by
	/// power subtraction is <b>-36.3 dBFS</b> - the same figure a park's music was confirmed at - and it
	/// adds 3.4 dB to the mix at the heaviest rain the game can produce. The level scales with the drop
	/// count, so ordinary weather sits well below that.
	/// </para>
	/// <para>
	/// <b>One caveat on the reasoning above, because it was half wrong.</b> Arguing from LobbyAudio's
	/// scale predicted rain would sit <i>below</i> the music, its gain being about a quarter of the
	/// music's; measured, the two are level. Gains do not compare across samples of different density -
	/// RAIN.mp2 is a continuous loop peaking at full scale where music has dynamics and gaps, so the
	/// same RMS falls out of a much lower gain. The number is right; the route to it only half was, and
	/// the capture is what decided it.
	/// </para>
	/// </summary>
	private const float RainVolume = 0.13f;

	/// <summary>How long the rain takes to come up and to go away, so a storm does not switch on.</summary>
	private const float RainFadeSeconds = 1.5f;

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
	/// Confirmed by capturing the mixer's own output through SDL's disk driver: a park came out at
	/// <b>-36.3 dBFS</b> over eighteen sounding seconds. That is the -26 above less the 6 dB of
	/// <see cref="Audio.MasterVolume"/>, which defaults to 0.5 and sits under every one of these
	/// targets, and the rest is which of the arrangements happened to play - they span seven decibels.
	/// <b>The -26 is a level before master volume, not a level at the speakers</b>, which is easy to
	/// compare against the wrong thing.
	/// </para>
	/// </summary>
	private const float MusicVolume = 0.33f;

	/// <summary>How long the music takes to fade as the park ends - the same as the lobby's stop.</summary>
	private const float StopSeconds = 0.15f;

	/// <summary>The top of the level the crowd drives, and the original's own clamp.</summary>
	public const int LoudestCrowd = 100;

	/// <summary>
	/// How loud the crowd makes the music, nought through a hundred - the original's
	/// <c>FUN_004c81e0</c>, which is <c>clamp( counted / 2, 0, 100 )</c> and nothing more.
	///
	/// <para>
	/// <b>It counts GUESTS, not people, and reading it as people would have made a park twice as loud as
	/// the original.</b> <c>FUN_004c7fa0</c> walks the thing list and counts a thing only where
	/// <c>*(thing + 2) == 1</c> - the model byte, and model 1 is a guest - so the five staff are not in it.
	/// The caller clamps the result again to 89, below this hundred, which never binds at any crowd a park
	/// this size can hold.
	/// </para>
	/// <para>
	/// <b>One term is deliberately missing and is named rather than quietly dropped.</b> The original
	/// applies a further test to each guest before counting them: <c>FUN_004fa990</c> passes a thing when
	/// any of five predicates hold, and all five read one dword at <c>thing + 8</c>, against 0, 1, 3, 9 and
	/// 10. What that field is has not been established - it is not the person type, which runs 0 to 7, and
	/// not the state, which lives at <c>+0x220</c> - and those predicates are asked of things right across
	/// the executable rather than of guests alone. So this counts every guest the park simulates, which is
	/// an <b>upper bound</b> on the original's count; where the filter excludes anybody, a park of ours is
	/// a little louder than a park of theirs.
	/// </para>
	/// </summary>
	public static int CrowdLevel( int guests ) => Math.Clamp( guests / 2, 0, LoudestCrowd );

	private readonly SoundCategory? _music;
	private Voice? _voice;

	/// <summary>
	/// The global ambient category, which is where a park's weather sounds live. Global rather than
	/// the park's own: its bank path is "Sound\Ambient" under data/global, and the category is
	/// registered once at startup rather than per level.
	/// </summary>
	private readonly SoundCategory? _ambient;

	private Voice? _rain;

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

	/// <summary>The one scream a ride may have going, by the script holding it.</summary>
	private readonly Dictionary<int, (Voice Voice, int Effect)> _screams = [];

	/// <summary>
	/// The four looping scream samples, chosen by <c>STARTSCREAM</c>'s first operand -
	/// <c>FUN_00551130</c>'s bands: nought screams not at all, 1, 2-3, 4-7, then 8 and over.
	/// </summary>
	private static readonly int[] ScreamEffects = [0x47, 0x48, 0x49, 0x4a];

	/// <summary>
	/// The script speed the volume is averaged against - the engine's <c>+0xc0</c>, which its loader
	/// sets to 50 and <b>no opcode ever writes</b>.
	/// </summary>
	/// <remarks>
	/// <b>A constant rather than a field, and deliberately.</b> <see cref="RideScript"/> leaves the speed
	/// word out because the dispatcher's <c>0.5 + 0.01 * speed</c> divisor is exactly 1 at 50, so a field
	/// could never differ from one. That argument holds for <c>WAIT</c> and does NOT hold here: the
	/// scream's volume is <c>(operand + speed) / 2</c>, where 50 is half the answer rather than an
	/// identity. So the number is needed even though the field is not.
	/// </remarks>
	public const int ScriptSpeed = 50;

	/// <summary>How loud a scream is: the engine's <c>(a + b) / 2</c>, held to nought through 100.</summary>
	public static int ScreamVolume( int level ) => Math.Clamp( (level + ScriptSpeed) / 2, 0, 100 );

	/// <summary>Which sample a band asks for, or nought when the band is silent.</summary>
	public static int ScreamEffectFor( int band ) => band switch
	{
		<= 0 => 0,
		1 => ScreamEffects[0],
		< 4 => ScreamEffects[1],
		< 8 => ScreamEffects[2],
		_ => ScreamEffects[3]
	};

	public ParkAudio( string themeName )
	{
		Current = this;

		// Nothing to load and nothing to play without a device, exactly as the lobby's does.
		if ( !Audio.Ready )
			return;

		// A category names its banks relative to the LEVEL folder rather than to the folder its own
		// .map files sit in - cat_music's bank path reads "Music\Music" - so the root is the level and
		// the maps are the Music folder beside it. The other three park categories are deliberately
		// not loaded: cat_ambient's effects are placed emitters that come out of the level's scape.omp
		// (the OBJ_ chunk FUN_00550e00 reads, type 1 records), cat_rides wants somewhere for a ride's
		// sounds to GO - the ride runtime itself exists and runs, and this said it did not; what is
		// missing is the sink, since RideEffects records that nothing it starts is drawn or heard - and
		// cat_speech wants an advisor in a park. Loading cat_rides alone would decode three hundred
		// and six samples for nothing that can yet be heard.
		var root = $"levels/{themeName.ToLowerInvariant()}";

		_music = new SoundCategory( root, $"{root}/Music", "music" );

		if ( !_music.IsValid )
			Log.Warning( $"Park audio: {themeName} has no music category in {root}/Music" );

		// And the weather's, which is not the park's own category at all - see ThunderEffect.
		_ambient = new SoundCategory( "global", "global/sound", "ambient" );

		if ( !_ambient.IsValid )
			Log.Warning( "Park audio: the global ambient category would not load, so the weather is silent" );

		// And the guests' voices, which is where the screams are. Loaded now that something asks for
		// them: the note above says a category with nowhere for its sounds to go is waste, and this one
		// has somewhere - see Scream.
		_kids = new SoundCategory( "global", "global/sound", "kids" );

		if ( !_kids.IsValid )
			Log.Warning( "Park audio: the global kids category would not load, so nobody can scream" );
		else
			Log.Info( $"Park audio: kids category ready, screams are {string.Join( ", ", ScreamEffects )}" );
	}

	/// <summary>
	/// Thunder for a bolt, sounding where the bolt is rather than flat.
	///
	/// The original plays it as a placed sound - Sound_PlayEffect's fourth, fifth and sixth arguments
	/// are a position, not the single volume a decompile makes them look like - and the place it
	/// passes is the bolt's <i>top</i>, three hundred units up.
	/// </summary>
	internal void Thunder( Vector3 at )
	{
		if ( !Audio.Ready )
			return;

		_ambient?.Play( ThunderEffect, ThunderVolume, bus: AudioBus.Effects, position: at );
	}

	/// <summary>
	/// Sets how hard it is raining, 0 for dry through 1 for as hard as the balance file allows,
	/// starting the bed if it is not already going.
	///
	/// <para>
	/// <b>One voice, where the original starts two, and that is a deliberate departure.</b> The
	/// original runs the bed twice over during rain: FUN_005131f0 starts effect 0x21 into the weather
	/// thing's own mRainSoundHandle, and FUN_0051e830 starts the same effect again into a global
	/// handle, with no dedupe anywhere - the driver's only guard is keyed on the handle passed in, and
	/// both sites pass zero. Only the global one is ever given a level. The second voice is what makes
	/// its rain sound unlike its snow, but its level lives behind a runtime pointer that cannot be read
	/// from the binary, so how loud it was is unknown, and doubling an identical loop at a gain nobody
	/// can establish would be a guess.
	/// </para>
	/// <para>
	/// <b>This rested on a better argument until it did not.</b> It was first written as "snow is
	/// unreachable, so the distinction never arises" - and that turned out to be false: hallow's snow
	/// band is 0..25 and it snows perfectly well. What keeps the departure defensible is narrower and
	/// temporary: <b>hallow has no saved park file</b>, so it cannot be played at all yet. The moment a
	/// park other than jungle can be entered, snow and rain will sound identical here where the
	/// original distinguishes them, and this wants settling by capturing the mix - see
	/// <c>ParkWeather.Snowing</c>.
	/// </para>
	/// </summary>
	internal void SetRainLevel( float level )
	{
		if ( !Audio.Ready || _ambient is not { IsValid: true } )
			return;

		level = level.Clamp( 0f, 1f );

		if ( level <= 0f )
		{
			StopRain();
			return;
		}

		if ( _rain is not { Playing: true } )
			_rain = _ambient.Play( RainEffect, RainVolume * level, loop: true,
				fadeInSeconds: RainFadeSeconds, bus: AudioBus.Effects );
		else
			_rain.SetVolume( RainVolume * level, RainFadeSeconds );
	}

	/// <summary>
	/// Starts a ride screaming - <c>STARTSCREAM</c>, through <c>FUN_00551130</c>.
	///
	/// <para>
	/// <b>The engine refuses to start a second one over the first</b>, and says so: its own line is
	/// "RSSE: Started screaming without s...". Reproduced rather than tidied, because a script that
	/// does it is doing something wrong and the silence would hide it.
	/// </para>
	/// </summary>
	/// <param name="scriptId">Whose scream this is, so <see cref="StopScream"/> can find it again.</param>
	/// <param name="band">The first operand: nought is silent, then 1, 2-3, 4-7, 8 and over.</param>
	/// <param name="level">The second operand, averaged with the script speed - see <see cref="ScreamVolume"/>.</param>
	/// <param name="at">Where the ride stands. The engine takes this from the script's own thing.</param>
	/// <returns>Whether anything started.</returns>
	internal bool Scream( int scriptId, int band, int level, Vector3 at )
	{
		if ( !Audio.Ready || _kids is not { IsValid: true } )
			return false;

		if ( _screams.ContainsKey( scriptId ) )
		{
			Log.Warning( $"Park audio: script {scriptId} started screaming without stopping first" );
			return false;
		}

		var effect = ScreamEffectFor( band );

		if ( effect == 0 )
		{
			Log.Trace( $"Park audio: script {scriptId} asked for band {band}, which screams not at all" );
			return false;
		}

		var volume = ScreamVolume( level );
		var voice = _kids.Play( effect, volume / 100f, loop: true, bus: AudioBus.Effects, position: at );

		if ( voice == null )
		{
			Log.Warning( $"Park audio: scream effect {effect} would not start for script {scriptId}" );
			return false;
		}

		_screams[scriptId] = (voice, effect);

		Log.Info( $"Park audio: script {scriptId} screaming, band {band} effect {effect} "
			+ $"volume {volume} at ({at.X:0.0},{at.Y:0.0},{at.Z:0.0})" );

		return true;
	}

	/// <summary>
	/// Stops a ride screaming - <c>STOPSCREAM</c>, which fades rather than cuts
	/// (<c>Sound_StopFading</c>) and forgets the handle.
	/// </summary>
	/// <remarks>
	/// <b>The release is what makes a second scream possible.</b> A looping voice holds its effect for
	/// ever - <see cref="SoundCategory.Play"/> parks it at positive infinity - so without this the ride
	/// would scream once per park and then be silent, which looks exactly like the instruction never
	/// having been built. <see cref="StopRain"/> learned the same lesson.
	/// </remarks>
	internal bool StopScream( int scriptId )
	{
		if ( !_screams.Remove( scriptId, out var scream ) )
			return false;

		scream.Voice.FadeOut( StopSeconds );
		_kids?.Release( scream.Effect );

		Log.Info( $"Park audio: script {scriptId} stopped screaming (effect {scream.Effect} released)" );

		return true;
	}

	/// <summary>
	/// Stops the rain, and lets the effect go.
	///
	/// The release matters: a looping voice holds its effect for ever - <see cref="SoundCategory.Play"/>
	/// parks it at positive infinity - so without this the next shower in the same park would find the
	/// effect still taken.
	/// </summary>
	internal void StopRain()
	{
		if ( _rain == null )
			return;

		_rain.FadeOut( RainFadeSeconds );
		_rain = null;

		_ambient?.Release( RainEffect );
	}

	/// <summary>The park is ending: what it has playing fades, and its effect is let go.</summary>
	protected override void OnDelete()
	{
		_voice?.FadeOut( StopSeconds );
		_voice = null;

		_rain?.FadeOut( StopSeconds );
		_rain = null;
		_ambient?.Release( RainEffect );

		// And whoever is still screaming as the park ends, for the same reason the rain is let go: a
		// looping voice holds its effect, and the next park in this process would find it taken.
		foreach ( var (voice, effect) in _screams.Values )
		{
			voice.FadeOut( StopSeconds );
			_kids?.Release( effect );
		}

		_screams.Clear();

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
	/// It is replayed rather than looped, which is what the lobby does with its beds and for the same
	/// reason: the effect carries its own ten-second delay and picks between several arrangements, so
	/// letting a voice end and asking the category for another is what turns that delay into
	/// behaviour and what lets a park play a different arrangement each time round. Looping one clip
	/// could do neither.
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

		if ( !Audio.Ready || _music is not { IsValid: true } )
			return;

		// Asked every pass, as the original asks it: FUN_0051e790 runs from the park loop at 0x0054f870 and
		// re-counts rather than being told when the crowd changes. Nobody spawns or leaves yet, so today it
		// is the same number every time - but the shape is the original's, and it will move on its own the
		// moment guests arrive by bus.
		var volume = MusicVolume * CrowdLevel( ParkPeople.Current?.Peeps.Count ?? 0 ) / (float)LoudestCrowd;

		if ( _voice is not { Playing: true } )
			_voice = _music.Play( MusicEffect, volume, bus: AudioBus.Music );
		else
			_voice.SetVolume( volume );
	}
}
