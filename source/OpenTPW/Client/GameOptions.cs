namespace OpenTPW;

/// <summary>
/// The game's options - what the options screen shows and changes.
///
/// <para>
/// The original keeps them in one object at 0x0078d8d8, fifteen words long, with room after it for a
/// copy: the options screen takes one as it opens (0x00423b00) and puts it back if it is cancelled
/// (0x00423ad0). Its constructor, 0x00423690, gives the defaults. 3D card rendering, 640 x 480, medium
/// graphics and the primary video card; every group of sound at its default volume, switched on if
/// sound is; the default audio quality; and Advisor, Tutorial, Popup help, Confirmations and RMB cancel
/// on, Rotation at 90 degrees and Scroll at Pushscroll.
/// </para>
/// <para>
/// The volumes, whether sound starts on, and the audio quality it copies from values that are zero in
/// the image and filled in at run time. data\sound.sam has exactly those rows, in the same order -
/// DefaultVolume.SFX, MUSIC, SPEECH and MOVIE, SoundInfo.SOUNDON and SoundInfo.DEFAULTQUALITY - so they
/// are read from there. <b>Inferred by name</b>; the reader that fills them in was not traced.
/// </para>
/// <para>
/// <b>What acts on them here.</b> The three volumes set the mixer's groups (<see cref="ApplySound"/>),
/// Popup help is the help bar and the button glints, Advisor is whether he speaks at all, and graphics
/// quality picks the detail file particles are set up from when a level loads. Nothing yet reads the
/// rest.
/// </para>
/// <para>
/// <b>Where they are kept.</b> The original splits them. Rendering, resolution, graphics quality, the video
/// card, audio quality and the movie volume belong to the machine, in save\Config.tcf
/// (<see cref="ConfigFile"/>, through <see cref="SaveFolder"/>). The other volumes and the seven switches
/// belong to the player, in their gms.dat (<see cref="PlayerOptions"/>), with the movie volume again - the
/// player's copy wins when they are picked. Every gms.dat written takes the options as they stand.
/// </para>
/// </summary>
internal sealed class GameOptions
{
	/// <summary>The options in force. The options screen puts its copy back here when it is cancelled.</summary>
	public static GameOptions Current { get; set; } = new();

	/// <summary>+0x00: rendering on the 3D card rather than in software. <b>Dead</b> for now, by choice.</summary>
	public bool CardRendering { get; set; } = true;

	/// <summary>+0x04: 512 x 384, 640 x 480 or 800 x 600, as 0, 1 or 2. <b>Decorative</b> for now, by choice.</summary>
	public int ScreenResolution { get; set; } = 1;

	/// <summary>+0x08: low, medium or high, as 0, 1 or 2.</summary>
	public int GraphicsQuality { get; set; } = 1;

	/// <summary>+0x0c: the secondary video card rather than the primary. <b>Dead</b> for now, by choice.</summary>
	public bool SecondaryVideoCard { get; set; }

	/// <summary>+0x10 and +0x14: whether sound effects are on, and how loud out of 100.</summary>
	public bool EffectsOn { get; set; } = SoundDefaults.On;

	public int EffectsVolume { get; set; } = SoundDefaults.Effects;

	/// <summary>+0x18 and +0x1c: the same for music.</summary>
	public bool MusicOn { get; set; } = SoundDefaults.On;

	public int MusicVolume { get; set; } = SoundDefaults.Music;

	/// <summary>+0x20 and +0x24: the same for speech.</summary>
	public bool SpeechOn { get; set; } = SoundDefaults.On;

	public int SpeechVolume { get; set; } = SoundDefaults.Speech;

	/// <summary>+0x28 and +0x2c: the same for the movies, which nothing plays yet.</summary>
	public bool MovieOn { get; set; } = SoundDefaults.On;

	public int MovieVolume { get; set; } = SoundDefaults.Movie;

	/// <summary>+0x30: out of 100, measured against sound.sam's SoundInfo thresholds. Nothing reads it yet.</summary>
	public int AudioQuality { get; set; } = SoundDefaults.Quality;

	/// <summary>+0x34: whether the advisor speaks (Advisor_SayResponse, 0x00599050, says nothing without it).</summary>
	public bool Advisor { get; set; } = true;

	/// <summary>+0x35. Nothing reads it yet.</summary>
	public bool Tutorial { get; set; } = true;

	/// <summary>+0x36: the interface's pop-up help - the help bar, and the glints round a button.</summary>
	public bool PopupHelp { get; set; } = true;

	/// <summary>+0x37: whether a park asks before deleting (0x0048cd10). Nothing reads it yet.</summary>
	public bool Confirmations { get; set; } = true;

	/// <summary>+0x38: Right button scroll rather than Pushscroll. Nothing reads it yet.</summary>
	public bool RightButtonScroll { get; set; }

	/// <summary>+0x39: whether the right mouse button cancels. Nothing reads it yet.</summary>
	public bool RmbCancel { get; set; } = true;

	/// <summary>+0x3a: rotating the park 90 degrees at a time rather than smoothly. Nothing reads it yet.</summary>
	public bool NinetyDegreeRotation { get; set; } = true;

	/// <summary>A copy to put back later - 0x00423b00.</summary>
	public GameOptions Copy() => (GameOptions)MemberwiseClone();

	/// <summary>Takes the machine's options from save\Config.tcf.</summary>
	public void Apply( ConfigFile file )
	{
		CardRendering = file.CardRendering != 0;
		ScreenResolution = file.ScreenResolution;
		GraphicsQuality = file.GraphicsQuality;
		SecondaryVideoCard = file.VideoCard != 0;
		MovieOn = file.MovieOn;
		MovieVolume = file.MovieVolume;
		AudioQuality = file.AudioQuality;
	}

	/// <summary>The machine's options, as save\Config.tcf holds them.</summary>
	public ConfigFile ToConfigFile() => new()
	{
		CardRendering = CardRendering ? 1 : 0,
		ScreenResolution = ScreenResolution,
		GraphicsQuality = GraphicsQuality,
		VideoCard = SecondaryVideoCard ? 1 : 0,
		MovieOn = MovieOn,
		MovieVolume = MovieVolume,
		AudioQuality = AudioQuality
	};

	/// <summary>Takes a player's own options from their gms.dat, as picking them does (0x005c83b0) - the movie volume included.</summary>
	public void Apply( PlayerOptions options )
	{
		EffectsOn = options.EffectsOn;
		EffectsVolume = options.EffectsVolume;
		MusicOn = options.MusicOn;
		MusicVolume = options.MusicVolume;
		SpeechOn = options.SpeechOn;
		SpeechVolume = options.SpeechVolume;
		MovieOn = options.MovieOn;
		MovieVolume = options.MovieVolume;
		Advisor = options.Advisor;
		Tutorial = options.Tutorial;
		PopupHelp = options.PopupHelp;
		Confirmations = options.Confirmations;
		RightButtonScroll = options.RightButtonScroll;
		RmbCancel = options.RmbCancel;
		NinetyDegreeRotation = options.NinetyDegreeRotation;
	}

	/// <summary>A player's own options, as their gms.dat holds them.</summary>
	public PlayerOptions ToPlayerOptions() => new()
	{
		EffectsOn = EffectsOn,
		EffectsVolume = EffectsVolume,
		MusicOn = MusicOn,
		MusicVolume = MusicVolume,
		SpeechOn = SpeechOn,
		SpeechVolume = SpeechVolume,
		MovieOn = MovieOn,
		MovieVolume = MovieVolume,
		Advisor = Advisor,
		Tutorial = Tutorial,
		PopupHelp = PopupHelp,
		Confirmations = Confirmations,
		RightButtonScroll = RightButtonScroll,
		RmbCancel = RmbCancel,
		NinetyDegreeRotation = NinetyDegreeRotation
	};

	/// <summary>
	/// Sets each group of sound to its volume, or silences it when it is switched off - 0x00423dd0, and
	/// the options screen as a slider moves.
	///
	/// The original hands the sound library a group's volume out of 100. How that library turns it into
	/// loudness was not traced, so here it is a straight multiply, taken against the group's default: at
	/// the defaults every group plays at the level its layers were set to by measurement, a group turned
	/// to 0 is silent, and one turned all the way up plays at 100 over its default.
	/// </summary>
	public void ApplySound()
	{
		Audio.SetBusVolume( AudioBus.Effects, Gain( EffectsOn, EffectsVolume, SoundDefaults.Effects ) );
		Audio.SetBusVolume( AudioBus.Music, Gain( MusicOn, MusicVolume, SoundDefaults.Music ) );
		Audio.SetBusVolume( AudioBus.Speech, Gain( SpeechOn, SpeechVolume, SoundDefaults.Speech ) );
	}

	private static float Gain( bool on, int volume, int byDefault ) => on ? volume / (float)Math.Max( byDefault, 1 ) : 0f;

	/// <summary>What data\sound.sam starts the sound options at - see the class remarks.</summary>
	private static class SoundDefaults
	{
		private static readonly SettingsFile? File = Load();

		public static readonly int Effects = Read( "DefaultVolume.SFX", 75 );
		public static readonly int Music = Read( "DefaultVolume.MUSIC", 60 );
		public static readonly int Speech = Read( "DefaultVolume.SPEECH", 75 );
		public static readonly int Movie = Read( "DefaultVolume.MOVIE", 100 );
		public static readonly int Quality = Read( "SoundInfo.DEFAULTQUALITY", 50 );
		public static readonly bool On = Read( "SoundInfo.SOUNDON", 1 ) != 0;

		private static SettingsFile? Load()
		{
			try
			{
				return new SettingsFile( "/sound.sam" );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Options: sound.sam would not load, so the sound options start at the shipped file's values - {e.Message}" );
				return null;
			}
		}

		/// <summary>A row of the file, or what the shipped file has there.</summary>
		private static int Read( string key, int shipped )
			=> int.TryParse( File?[key], out var value ) ? value : shipped;
	}
}
