namespace OpenTPW;

/// <summary>
/// Everything the lobby makes a noise with: each park's theme, its ambience, and the thunder over
/// Halloween World.
///
/// The original's lobby plays sound in four places and nowhere else, all of them found in its
/// decompile:
///
///   - <b>lobby start</b> (FUN_005dcfe0), right after it loads data\lobby\globe, starts two
///     continuous voices from the global lobby sfx category - effects 2 and 3. Only effect 3 is
///     played here; see <see cref="KeepGlobalPlaying"/> for why effect 2 is not.
///   - <b>selecting an island</b> (FUN_005e14a0, and the same three lines again in the next- and
///     previous-island handlers) stops whatever the last island had going and starts two more:
///     effect 1 of that park's local lobby <i>music</i> category, and effect 2 of its local lobby
///     <i>sfx</i> category.
///   - <b>the lobby tick</b> (FUN_005e0470), having rolled the LIGHTNING mask and drawn a bolt,
///     plays effect 1 of the global lobby sfx category. That is the thunder.
///   - <b>the lobby tick again</b>, every frame, unconditionally:
///
///         if ( (random() &gt;&gt; 13 &amp; 0xf) == 1 )
///             PlaySound( localLobbySfx[island], 3 + random() % 3 );
///
///     so a one-in-sixteen chance each frame of one of effects 3, 4 and 5, picked evenly.
///
/// Every one of those calls passes (0,0,0) as the position, so none of the lobby's sound is
/// placed in the world - it is all played flat, and only the selected island's is played at all.
/// That is why there is no panning here.
///
/// <b>The gates have no sound.</b> Those four calls are the whole of it: nothing in the lobby's
/// tick, its island handlers or its start plays anything when a gate animates, and the lobby
/// categories hold no door sample to play. The game does have gate and door sounds - dooropen1
/// and doorcls1b in the global kids bank, spacegate1 in space's ambient bank, gateslm1b in
/// hallow's ride bank - but those belong to rides and queue huts inside a park, not to the
/// lobby's front gates.
/// </summary>
public sealed class LobbyAudio : Entity
{
	/// <summary>The one lobby audio system, so the lightning and the debug console can reach it.</summary>
	internal static LobbyAudio? Current { get; private set; }

	/// <summary>
	/// The effect ids, which are what the original passes to its play call. They are ids rather
	/// than indices - see <see cref="SoundCategoryFile.Effect"/> - so they are named rather than
	/// counted.
	/// </summary>
	private const int GlobalThunder = 1;
	private const int GlobalMusicBed = 2;
	private const int GlobalAmbientBed = 3;
	private const int LocalMusic = 1;
	private const int LocalBed = 2;
	private const int LocalOneShotFirst = 3;
	private const int LocalOneShotCount = 3;

	/// <summary>
	/// How likely a one-shot is, per second.
	///
	/// The original rolls one in sixteen per frame, which taken literally means a busier jungle on
	/// a faster machine. At the 25fps the rest of its data assumes - see
	/// <see cref="Time.TicksPerSecond"/> - that comes out as about one and a half a
	/// second, and that is what this uses, so the ambience keeps its pace at any frame rate. The
	/// effects' own repeat delays do most of the thinning from there.
	/// </summary>
	private static readonly float OneShotsPerSecond = Time.TicksPerSecond / 16f;

	/// <summary>
	/// How loud each layer sits, before <see cref="Audio.MasterVolume"/>.
	///
	/// These are not arbitrary. The original keeps a volume per effect - the 100s and 50s are
	/// visible in the bytes either side of each effect's sample list - but that part of the .map
	/// header has not been decoded, so the balance is set here instead, from the measured loudness
	/// of the samples themselves. Median RMS of what actually plays, in dBFS:
	///
	///     park themes      -19.8   (-21.2 jungle, -18.5 fantasy, -22.4 hallow, -17.1 space)
	///     park beds        -16.5   (-16.1 jungle, -23.6 fantasy, -16.9 hallow, -13.1 space)
	///     one-shots        -17.0
	///     thunder          -16.2
	///     global ambience  -12.4
	///
	/// So the music is the <b>quietest</b> thing in the lobby as recorded, by five to seven
	/// decibels, and playing every layer at roughly equal gain buries it - which is exactly what
	/// the first cut of this did. Each constant below is therefore a correction rather than a
	/// level: it is what puts that layer's median at the target it should sit at, which is
	/// -26 dBFS for the theme, -34 for the bed and the one-shots, -36 for the global ambience,
	/// and -24 for thunder. The theme therefore leads the ambience by about eight decibels and
	/// thunder is the only thing above it, which is the shape the first cut had backwards: it
	/// ran the one-shots nine decibels <i>over</i> the music.
	///
	/// Nothing here evens out the samples <i>within</i> a layer, and one case is worth knowing
	/// about: of Lost Kingdom's three arrangements, jungle-2 is around ten decibels quieter than
	/// jungle-1 and jungle-3 - not a quiet opening but the whole track, at every percentile of its
	/// level. That is the game's own mastering, and it cannot be corrected by gain without
	/// clipping, because its peaks are only six decibels below the other two while its body is
	/// twenty below. So one visit in three to Lost Kingdom has a noticeably quieter theme. It is
	/// the data, not a fault here.
	///
	/// The peaks matter as much as the averages. This is 1999 game audio and it is mastered hot -
	/// nearly every sample in the lobby peaks within a decibel of full scale - so gains that look
	/// modest still sum past 1.0 and clip, and clipping is most of what "too loud" turns out to
	/// mean. Worst case here, with a theme, a bed, a one-shot, a thunder and the global ambience
	/// all peaking on the same sample, comes to 1.18 before the master volume and 0.59 after it,
	/// so <see cref="Audio"/>'s clamp is a backstop that never actually fires. Forty seconds of
	/// Lost Kingdom measures -30.7 dBFS RMS peaking at -11.4, against -17.2 peaking at -0.4 - that
	/// is, hard against the rails - before this.
	/// </summary>
	private const float MusicVolume = 0.50f;
	private const float BedVolume = 0.13f;
	private const float OneShotVolume = 0.14f;
	private const float ThunderVolume = 0.40f;
	private const float GlobalBedVolume = 0.07f;

	/// <summary>
	/// How long a park's theme and bed take to come in and go out when the camera moves.
	///
	/// The original cuts both dead - it spends the change spinning a globe from one island to the
	/// next, so the cut lands out of sight and out of earshot of anything else. This slides
	/// straight between islands in about half a second, the same as <see cref="LobbyWeather"/>'s
	/// sky does, and a cut over half a second of visible travel reads as a fault.
	/// </summary>
	private const float CrossfadeSeconds = 0.9f;

	/// <summary>
	/// A park's two lobby categories. They live in the same folder and are always wanted
	/// together, but they are two separate pairs of .map files - cat_locallobbymusic* holds the
	/// theme and cat_locallobbysfx* the ambience - so they load as two.
	/// </summary>
	private sealed record ParkSounds( SoundCategory Music, SoundCategory Ambience );

	private SoundCategory? _global;

	/// <summary>One per park, built the first time that park is the one on show.</summary>
	private readonly Dictionary<string, ParkSounds> _parks = new( StringComparer.OrdinalIgnoreCase );

	private ParkSounds? _current;
	private Voice? _globalBed;
	private string? _playing;
	private Voice? _music;
	private Voice? _bed;
	private readonly Random _random = new();

	/// <summary>
	/// Silences the lobby without unloading it, for the debug console.
	///
	/// Setting it stops what is already playing as well as holding back what would start next -
	/// the beds run for up to ninety seconds, so only doing the latter would leave the lobby
	/// playing for a minute and a half after being told to stop.
	/// </summary>
	internal bool Muted
	{
		get => _muted;
		set
		{
			_muted = value;

			if ( value )
				StopEverything();
		}
	}

	private bool _muted;

	public LobbyAudio()
	{
		Current = this;
	}

	protected override void OnUpdate()
	{
		if ( !Audio.Ready )
			return;

		if ( _global == null )
			StartGlobal();

		if ( !Muted )
			KeepGlobalPlaying();

		var island = LobbyCameraMode.CurrentIsland;

		if ( island == null )
			return;

		if ( !string.Equals( island.ThemeName, _playing, StringComparison.OrdinalIgnoreCase ) )
			MoveTo( island );

		if ( Muted || _current == null )
			return;

		KeepPlaying();
		RollOneShot();
	}

	/// <summary>
	/// Thunder, from the global lobby sfx category - the same effect 1 the original plays inside
	/// the strike test, right after it draws the bolt.
	/// </summary>
	internal void Thunder()
	{
		if ( !Audio.Ready || Muted )
			return;

		_global?.Play( GlobalThunder, ThunderVolume );
	}

	/// <summary>What is playing right now, for the debug console.</summary>
	internal string State()
		=> !Audio.Ready
			? "no audio device"
			: $"park={_playing ?? "none"} theme={Describe( _music )} bed={Describe( _bed )} "
				+ $"muted={Muted} volume={Audio.MasterVolume:0.00}";

	private static string Describe( Voice? voice ) => voice is { Playing: true } ? voice.Name : "-";

	/// <summary>Loads the category the thunder and the global ambience both come from.</summary>
	private void StartGlobal()
	{
		_global = new SoundCategory( "global", "global/sound", "globallobbysfx" );

		if ( !_global.IsValid )
			Log.Warning( "Lobby audio: no global lobby sfx category - there will be no thunder" );
	}

	/// <summary>
	/// Keeps the lobby's global ambience going.
	///
	/// The original starts <i>two</i> voices here, as the lobby opens, and never touches them
	/// again - effects 2 and 3 of the global lobby sfx category. This plays only effect 3.
	///
	/// Effect 2 is left out deliberately. Its two samples are level4c and level4w from the global
	/// music bank, and level4c is the same recording as one of Space Zone's three lobby themes -
	/// the two files differ by eleven bytes out of 253,807, all of them in the final MPEG frame,
	/// and the decoded audio correlates at 1.0000. So playing it does what it sounds like: Space
	/// Zone's music comes up over whichever park you are actually looking at, every twenty-odd
	/// seconds, on top of that park's own theme. Whatever it was for in the original - most likely
	/// the globe view, which the lobby here never shows because it always has an island selected -
	/// it reads as a bug in this one.
	///
	/// Effect 3 is the one worth having: four short atmospheric samples on a five-second delay,
	/// which is texture rather than melody and does not fight anything.
	///
	/// Like the per-park beds it is replayed rather than looped, on its own effect's delay.
	/// </summary>
	private void KeepGlobalPlaying()
	{
		if ( _global is not { IsValid: true } )
			return;

		if ( _globalBed is not { Playing: true } )
			_globalBed = _global.Play( GlobalAmbientBed, GlobalBedVolume, fadeInSeconds: CrossfadeSeconds );
	}

	/// <summary>
	/// Fades out the park that was playing and brings in the one now on show.
	///
	/// A park's categories are kept once built. Moving back and forth between two islands is the
	/// ordinary way to use the lobby, and decoding jungle's ninety-second bed again on every
	/// arrival would stall the frame the camera got there on.
	/// </summary>
	private void MoveTo( LobbyIsland island )
	{
		_music?.FadeOut( CrossfadeSeconds );
		_bed?.FadeOut( CrossfadeSeconds );

		// Those voices were holding their effects - see SoundCategory.Play - so the park being
		// left has to be told it may start them again next time the camera comes round.
		_current?.Music.Release( LocalMusic );
		_current?.Ambience.Release( LocalBed );

		_music = null;
		_bed = null;
		_playing = island.ThemeName;
		_current = ParkFor( island.ThemeName );

		if ( _current == null || Muted )
			return;

		_music = _current.Music.Play( LocalMusic, MusicVolume, fadeInSeconds: CrossfadeSeconds,
			bus: AudioBus.Music );
		_bed = _current.Ambience.Play( LocalBed, BedVolume, fadeInSeconds: CrossfadeSeconds );

		Log.Info( $"Lobby audio: {island.ThemeName} - theme '{Describe( _music )}', bed '{Describe( _bed )}'" );
	}

	/// <summary>
	/// Starts the theme and the bed again when either runs out.
	///
	/// Neither loops. Both are effects with a repeat delay of their own - four seconds for the
	/// music, two for the bed - and letting a voice end and asking the category for another is
	/// what turns those delays into behaviour. It also means a park picks a different one of its
	/// three arrangements each time round, which looping a single clip could not do, and it is
	/// the only reading under which Wonder Land works at all: its bed is nine bird calls of under
	/// a second each and no loop, so it wants replaying every couple of seconds, not looping.
	/// </summary>
	private void KeepPlaying()
	{
		if ( _music is not { Playing: true } )
			_music = _current!.Music.Play( LocalMusic, MusicVolume, bus: AudioBus.Music );

		if ( _bed is not { Playing: true } )
			_bed = _current!.Ambience.Play( LocalBed, BedVolume );
	}

	/// <summary>
	/// The one-in-sixteen-a-frame roll, restated as a rate - the same conversion
	/// <see cref="LobbyWeather"/> does to the strike test, and for the same reason.
	/// </summary>
	private void RollOneShot()
	{
		var chance = 1f - MathF.Exp( -OneShotsPerSecond * Time.Delta );

		if ( _random.NextSingle() >= chance )
			return;

		// The effect's own delay applies, so a roll landing on something still playing comes to
		// nothing rather than doubling it up. That is the original's behaviour, and it is most of
		// what keeps the jungle from sounding like a pet shop.
		_current!.Ambience.Play( LocalOneShotFirst + _random.Next( LocalOneShotCount ), OneShotVolume );
	}

	/// <summary>
	/// Stops every voice the lobby holds and lets their effects go, so unmuting starts them again
	/// from the top rather than waiting out a delay that was counted against a voice that is no
	/// longer playing.
	/// </summary>
	private void StopEverything()
	{
		foreach ( var voice in new[] { _music, _bed, _globalBed } )
			voice?.FadeOut( 0.15f );

		_music = null;
		_bed = null;
		_globalBed = null;

		_current?.Music.Release( LocalMusic );
		_current?.Ambience.Release( LocalBed );
		_global?.Release( GlobalAmbientBed );
	}

	private ParkSounds? ParkFor( string theme )
	{
		if ( _parks.TryGetValue( theme, out var park ) )
			return park;

		// The bank paths inside a park's categories read "Sound/LobbySfx", relative to the park,
		// while the .map files themselves sit in that park's Sound folder.
		var root = $"levels/{theme.ToLowerInvariant()}";
		var directory = $"{root}/Sound";

		park = new ParkSounds(
			new SoundCategory( root, directory, "locallobbymusic" ),
			new SoundCategory( root, directory, "locallobbysfx" ) );

		_parks[theme] = park;

		if ( !park.Music.IsValid && !park.Ambience.IsValid )
			Log.Warning( $"Lobby audio: {theme} has no lobby categories in {directory}" );

		return park;
	}
}
