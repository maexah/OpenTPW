namespace OpenTPW.UI;

/// <summary>
/// Game Options, which the game menu's Options opens.
///
/// <para>
/// OptionsScreen_Open (0x004a3a30) quietens the advisor, puts the front end's window away and loads
/// the layout stream at 0x00752f30 over the whole screen, on f_screen.md2, with the callback 0x004a2bf0.
/// "Game Options" (UITEXT 315) is its title, in font 5 on the purple skin in (234, 239, 102), in a
/// rectangle widened by half its width either side (0x00485d20). Down the left are the rendering and
/// video card panels and the sliders, down the right the switches, and in the bottom right the tick and
/// the cross in a plain frame.
/// </para>
/// <para>
/// <b>Rows.</b> Each is a panel - f_optpanel.md2 for a switch, f_optpanel2 for a volume, f_optpanel3
/// for any other slider - with one line on the plain label skin, in black in font 6, from the left and
/// centred up and down (0x004a43b0, 0x004a4490). A line is a label with its value after it: " On" or
/// " Off" (0x004a3960), " 75 %" (" %d %%"), or for a group of sound switched off " " and then " Off"
/// (" %s"). The switches, b_on.md2 flagged 0x10, are up for on and down for off (0x004a3480). The b_on2
/// arrows turn Rotation and Scroll between their two settings, and switch rendering and the video card.
/// </para>
/// <para>
/// <b>Sliders</b> run from 0 to 100. Screen resolution and graphics quality show the setting times 50,
/// and take back the slider times two over 100 in whole numbers: so 0 to 49 is the first setting, 50 to
/// 99 the second, and only 100 the third. In software rendering graphics quality takes back only the
/// slider over 100, so it goes no higher than medium, and switching rendering puts it through that again
/// (0x004a2e90). The volumes and audio quality show their number.
/// </para>
/// <para>
/// <b>Changes are made as they happen.</b> A switch or arrow changes its option as it is clicked, and a
/// slider as it moves. Sound effects, music and speech are heard at their new volume at once, or
/// silenced when switched off; the movie volume is only kept (0x004a2bf0, 0x004a2e90). Popup help turns
/// the help bar and the button glints on or off from the next pointer movement.
/// </para>
/// <para>
/// <b>The tick and the cross.</b> The options are copied as the screen opens (0x00423b00). The cross
/// (-2) puts the copy back and closes (0x00423ad0); the tick (-1) keeps the changes, applies the group
/// volumes, saves, and closes (0x004237f0). Escape does nothing here - the callback takes no keys. As it
/// closes, the front end's window comes back and any button glints go (message 0x14).
/// </para>
/// <para>
/// <b>Not the original's:</b>
/// <list type="bullet">
/// <item>The original's cross puts the copy back without applying the group volumes, so a volume moved
/// before cancelling is still heard until the options are next applied. Here the cross applies them.</item>
/// <item>The tick's checks are left out. In the original, a change of rendering, resolution or video card,
/// or a graphics quality whose detail file needs a restart (0x00423bc0), shows RESTART GAME (UITEXT 403)
/// instead of closing. A change of audio quality across one of sound.sam's thresholds sets the sound
/// library up again (0x0051b920).</item>
/// <item>Nothing is saved. The original writes these options to the player's save and to a dialog.tcf
/// beside the game, and neither exists here, so they last until the game closes.</item>
/// </list>
/// </para>
/// <para>
/// <b>Dead for now, by choice:</b> 3D card or software rendering and the video card do nothing, and
/// screen resolution only shows its setting. With one video card counted, the video card never leaves
/// Primary, as on a machine with one (0x004a3480). Audio quality, Tutorial, Confirmations, RMB cancel,
/// Rotation and Scroll are kept, but nothing reads them yet - they belong to the sound library's set-up
/// and to parks.
/// </para>
/// </summary>
internal sealed class OptionsScreen : UiWindow
{
	private const int TitleFont = 5;
	private const int LineFont = 6;

	/// <summary>Every row's line is a child with this id (0x004a43b0 asks each panel for it).</summary>
	private const int LineId = 0x1d4c9;

	/// <summary>How many video cards there are to choose from - OpenTPW draws on whichever the system gives it.</summary>
	private const int VideoCards = 1;

	private static readonly UiColour TitleColour = new( 234, 239, 102 );

	/// <summary>The options as they were when the screen opened, for the cross to put back.</summary>
	private readonly GameOptions _before = GameOptions.Current.Copy();

	private readonly UiControl _rendering;
	private readonly UiControl _videoCard;
	private readonly UiControl _advisorLine;
	private readonly UiControl _tutorialLine;
	private readonly UiControl _popupHelpLine;
	private readonly UiControl _confirmationsLine;
	private readonly UiControl _rmbCancelLine;
	private readonly UiControl _rotationLine;
	private readonly UiControl _scrollLine;

	private readonly UiSlider _resolution;
	private readonly UiControl _resolutionLine;
	private readonly UiSlider _quality;
	private readonly UiControl _qualityLine;
	private readonly UiSlider _effects;
	private readonly UiControl _effectsLine;
	private readonly UiSlider _music;
	private readonly UiControl _musicLine;
	private readonly UiSlider _speech;
	private readonly UiControl _speechLine;
	private readonly UiSlider _movie;
	private readonly UiControl _movieLine;
	private readonly UiSlider _audioQuality;
	private readonly UiControl _audioQualityLine;

	private readonly UiButton _advisor;
	private readonly UiButton _tutorial;
	private readonly UiButton _popupHelp;
	private readonly UiButton _confirmations;
	private readonly UiButton _rmbCancel;
	private readonly UiButton _effectsSwitch;
	private readonly UiButton _musicSwitch;
	private readonly UiButton _speechSwitch;
	private readonly UiButton _movieSwitch;

	private static GameOptions Options => GameOptions.Current;

	public OptionsScreen( FrontEnd frontEnd ) : base( frontEnd )
	{
		Modal = true;

		Root = new UiControl
		{
			Id = 0x1d4c0,
			Rect = VirtualScreen.Whole,
			Mesh = UiMesh.Get( "f_screen" ),
			HoldsChildren = true
		};

		// In the order the layout stream has them, but for the buttons, which 0x004a3a30 raises over
		// everything else after loading - they sit on the panels.
		_rendering = Panel( 0x1d4c8, new UiRect( 57, 167, 668, 316 ), new UiRect( 124, 218, 424, 263 ) );
		_videoCard = Panel( 0x1d4ca, new UiRect( 675, 167, 1286, 316 ), new UiRect( 739, 218, 1039, 263 ) );
		_advisorLine = Panel( 0x1d4cb, new UiRect( 1331, 167, 1964, 316 ), new UiRect( 1401, 215, 1701, 260 ) );
		_tutorialLine = Panel( 0x1d4cc, new UiRect( 1331, 323, 1964, 472 ), new UiRect( 1401, 371, 1701, 416 ) );
		_popupHelpLine = Panel( 0x1d4cd, new UiRect( 1331, 479, 1964, 628 ), new UiRect( 1401, 528, 1701, 573 ) );
		_confirmationsLine = Panel( 0x1d4ce, new UiRect( 1331, 681, 1964, 830 ), new UiRect( 1401, 732, 1701, 777 ) );
		_rmbCancelLine = Panel( 0x1d4cf, new UiRect( 1331, 837, 1964, 986 ), new UiRect( 1401, 888, 1701, 932 ) );
		_rotationLine = Panel( 0x1d4d0, new UiRect( 1331, 994, 1964, 1143 ), new UiRect( 1401, 1044, 1701, 1089 ) );
		_scrollLine = Panel( 0x1d4d1, new UiRect( 1331, 1150, 1964, 1299 ), new UiRect( 1401, 1200, 1701, 1245 ) );

		Root.Add( new UiControl
		{
			Id = 0x1d4d4,
			Rect = new UiRect( 788 - 236, 46, 1260 + 236, 126 ),
			Text = Localization.Get( UIStrings.GameOptions ),
			Font = TitleFont,
			TextColour = TitleColour,
			TextShadow = true
		} );

		(_resolution, _resolutionLine) = Slider( 0x1d4d5, new UiRect( 57, 323, 1289, 472 ), "f_optpanel3", 361, 364, 375, 331, ResolutionMoved );
		(_quality, _qualityLine) = Slider( 0x1d4d6, new UiRect( 57, 479, 1289, 628 ), "f_optpanel3", 518, 520, 532, 487, QualityMoved );
		(_effects, _effectsLine) = Slider( 0x1d4d8, new UiRect( 52, 838, 1289, 988 ), "f_optpanel2", 877, 880, 891, 845, EffectsMoved );
		(_music, _musicLine) = Slider( 0x1d4da, new UiRect( 52, 994, 1289, 1144 ), "f_optpanel2", 1033, 1035, 1047, 1004, MusicMoved );
		(_speech, _speechLine) = Slider( 0x1d4dc, new UiRect( 52, 1151, 1289, 1301 ), "f_optpanel2", 1190, 1192, 1204, 1157, SpeechMoved );
		(_movie, _movieLine) = Slider( 0x1d4de, new UiRect( 52, 1307, 1289, 1457 ), "f_optpanel2", 1346, 1348, 1360, 1313, MovieMoved );

		Root.Add( new UiControl
		{
			Id = 0x1d4df,
			Rect = new UiRect( 1742, 1321, 1947, 1443 ),
			Mesh = UiMesh.Get( "!f_plain" )
		} );

		(_audioQuality, _audioQualityLine) = Slider( 0x1d4e0, new UiRect( 57, 638, 1289, 787 ), "f_optpanel3", 677, 679, 691, 646, AudioQualityMoved );

		Button( 0x1d4d2, new UiRect( 521, 190, 624, 292 ), "b_on2", SwitchRendering );
		Button( 0x1d4d3, new UiRect( 1140, 190, 1242, 292 ), "b_on2", SwitchVideoCard );

		_advisor = Switch( 0x1d4c1, new UiRect( 1786, 190, 1930, 292 ), on => ShowSwitch( _advisorLine, UIStrings.Advisor, Options.Advisor = on ) );
		_tutorial = Switch( 0x1d4c2, new UiRect( 1786, 346, 1930, 449 ), on => ShowSwitch( _tutorialLine, UIStrings.Tutorial, Options.Tutorial = on ) );
		_popupHelp = Switch( 0x1d4c3, new UiRect( 1786, 503, 1930, 605 ), on => ShowSwitch( _popupHelpLine, UIStrings.PopupHelp, Options.PopupHelp = on ) );
		_confirmations = Switch( 0x1d4c4, new UiRect( 1786, 705, 1930, 807 ), on => ShowSwitch( _confirmationsLine, UIStrings.Confirmations, Options.Confirmations = on ) );
		_rmbCancel = Switch( 0x1d4c5, new UiRect( 1786, 861, 1930, 963 ), on => ShowSwitch( _rmbCancelLine, UIStrings.RmbCancel, Options.RmbCancel = on ) );

		Button( 0x1d4c6, new UiRect( 1807, 1017, 1910, 1119 ), "b_on2", () =>
		{
			Options.NinetyDegreeRotation = !Options.NinetyDegreeRotation;
			ShowRotation();
		} );

		Button( 0x1d4c7, new UiRect( 1807, 1173, 1910, 1276 ), "b_on2", () =>
		{
			Options.RightButtonScroll = !Options.RightButtonScroll;
			ShowScroll();
		} );

		_effectsSwitch = Switch( 0x1d4d7, new UiRect( 801, 862, 945, 964 ), on => { Options.EffectsOn = on; EffectsMoved(); } );
		_musicSwitch = Switch( 0x1d4d9, new UiRect( 801, 1018, 945, 1120 ), on => { Options.MusicOn = on; MusicMoved(); } );
		_speechSwitch = Switch( 0x1d4db, new UiRect( 801, 1175, 945, 1277 ), on => { Options.SpeechOn = on; SpeechMoved(); } );
		_movieSwitch = Switch( 0x1d4dd, new UiRect( 801, 1331, 945, 1433 ), on => { Options.MovieOn = on; MovieMoved(); } );

		Button( -1, new UiRect( 1763, 1340, 1846, 1424 ), "b_okay", Keep );
		Button( -2, new UiRect( 1853, 1340, 1936, 1424 ), "b_exit", PutBack );

		ShowOptions();
	}

	protected internal override void Closed() => FrontEnd.OptionsClosed();

	/// <summary>Shows every option as it stands - the second half of 0x004a3a30.</summary>
	private void ShowOptions()
	{
		ShowRendering();
		ShowVideoCard();

		_audioQuality.SetValue( Options.AudioQuality );
		AudioQualityMoved();

		ShowVolume( _effects, _effectsSwitch, Options.EffectsVolume, Options.EffectsOn, EffectsMoved );
		ShowVolume( _music, _musicSwitch, Options.MusicVolume, Options.MusicOn, MusicMoved );
		ShowVolume( _speech, _speechSwitch, Options.SpeechVolume, Options.SpeechOn, SpeechMoved );
		ShowVolume( _movie, _movieSwitch, Options.MovieVolume, Options.MovieOn, MovieMoved );

		_resolution.SetValue( Options.ScreenResolution * 100 / 2 );
		ShowResolution();

		_quality.SetValue( Options.GraphicsQuality * 100 / 2 );
		ShowQuality();

		SetSwitch( _advisor, _advisorLine, UIStrings.Advisor, Options.Advisor );
		SetSwitch( _tutorial, _tutorialLine, UIStrings.Tutorial, Options.Tutorial );
		SetSwitch( _popupHelp, _popupHelpLine, UIStrings.PopupHelp, Options.PopupHelp );
		SetSwitch( _confirmations, _confirmationsLine, UIStrings.Confirmations, Options.Confirmations );
		SetSwitch( _rmbCancel, _rmbCancelLine, UIStrings.RmbCancel, Options.RmbCancel );

		ShowRotation();
		ShowScroll();
	}

	/// <summary>The tick (-1): the changes stay - 0x004237f0, less its checks - see the class remarks.</summary>
	private void Keep()
	{
		Options.ApplySound();
		Log.Info( "Options: kept" );
		FrontEnd.Close( this );
	}

	/// <summary>The cross (-2): the options go back to how they were - 0x00423ad0, and the group volumes with them.</summary>
	private void PutBack()
	{
		GameOptions.Current = _before;
		GameOptions.Current.ApplySound();
		Log.Info( "Options: cancelled" );
		FrontEnd.Close( this );
	}

	/// <summary>3D card rendering or software (0x1d4d2): graphics quality goes through the slider again for its new limit.</summary>
	private void SwitchRendering()
	{
		Options.CardRendering = !Options.CardRendering;

		_quality.SetValue( Options.GraphicsQuality * 100 / 2 );
		QualityMoved();

		ShowRendering();
	}

	/// <summary>The video card (0x1d4d3): to the secondary if there is one, back to the primary if not.</summary>
	private void SwitchVideoCard()
	{
		if ( Options.SecondaryVideoCard )
			Options.SecondaryVideoCard = false;
		else if ( VideoCards > 1 )
			Options.SecondaryVideoCard = true;

		ShowVideoCard();
	}

	private void ResolutionMoved()
	{
		Options.ScreenResolution = _resolution.Value * 2 / 100;
		ShowResolution();
	}

	private void QualityMoved()
	{
		Options.GraphicsQuality = (Options.CardRendering ? 2 : 1) * _quality.Value / 100;
		ShowQuality();
	}

	private void EffectsMoved()
	{
		Options.EffectsVolume = _effects.Value;
		ShowVolumeLine( _effectsLine, UIStrings.SoundEffectsVolume, _effects.Value, Options.EffectsOn );
		Options.ApplySound();
	}

	private void MusicMoved()
	{
		Options.MusicVolume = _music.Value;
		ShowVolumeLine( _musicLine, UIStrings.MusicVolume, _music.Value, Options.MusicOn );
		Options.ApplySound();
	}

	private void SpeechMoved()
	{
		Options.SpeechVolume = _speech.Value;
		ShowVolumeLine( _speechLine, UIStrings.SpeechVolume, _speech.Value, Options.SpeechOn );
		Options.ApplySound();
	}

	private void MovieMoved()
	{
		Options.MovieVolume = _movie.Value;
		ShowVolumeLine( _movieLine, UIStrings.MovieVolume, _movie.Value, Options.MovieOn );
	}

	private void AudioQualityMoved()
	{
		Options.AudioQuality = _audioQuality.Value;
		_audioQualityLine.Text = Line( UIStrings.AudioQuality, $" {_audioQuality.Value} %" );
	}

	private void ShowRendering()
		=> _rendering.Text = Localization.Get( Options.CardRendering ? UIStrings.GPURendering : UIStrings.SoftwareRendering );

	private void ShowVideoCard()
		=> _videoCard.Text = Line( UIStrings.Videocard, Localization.Get( Options.SecondaryVideoCard && VideoCards > 1 ? UIStrings.Secondary : UIStrings.Primary ) );

	private void ShowResolution()
	{
		var name = Options.ScreenResolution switch
		{
			0 => UIStrings.Resolution512x384,
			1 => UIStrings.Resolution640x480,
			2 => UIStrings.Resolution800x600,
			_ => UIStrings.Blank
		};

		_resolutionLine.Text = Line( UIStrings.ScreenResolution, Localization.Get( name ) );
	}

	private void ShowQuality()
	{
		var name = Options.GraphicsQuality switch
		{
			0 => UIStrings.Low,
			1 => UIStrings.Medium,
			2 => UIStrings.High,
			_ => UIStrings.Blank
		};

		_qualityLine.Text = Line( UIStrings.GraphicsQuality, Localization.Get( name ) );
	}

	private void ShowRotation()
		=> _rotationLine.Text = Line( UIStrings.Rotation, Localization.Get( Options.NinetyDegreeRotation ? UIStrings.Rotation90Degs : UIStrings.Smooth ) );

	private void ShowScroll()
		=> _scrollLine.Text = Line( UIStrings.Scroll, Localization.Get( Options.RightButtonScroll ? UIStrings.RightButton : UIStrings.Pushscroll ) );

	/// <summary>A group of sound's slider and switch as the screen opens, and its line through the handler for its slider.</summary>
	private static void ShowVolume( UiSlider slider, UiButton onOff, int volume, bool on, Action moved )
	{
		slider.SetValue( volume );
		onOff.IsDown = !on;
		moved();
	}

	private static void ShowVolumeLine( UiControl line, UIStrings label, int volume, bool on )
		=> line.Text = Line( label, on ? $" {volume} %" : " " + Localization.Get( UIStrings.Off ) );

	private static void SetSwitch( UiButton onOff, UiControl line, UIStrings label, bool on )
	{
		onOff.IsDown = !on;
		ShowSwitch( line, label, on );
	}

	private static void ShowSwitch( UiControl line, UIStrings label, bool on )
		=> line.Text = Line( label, Localization.Get( on ? UIStrings.On : UIStrings.Off ) );

	private static string Line( UIStrings label, string value ) => Localization.Get( label ) + value;

	/// <summary>A panel with its line, which is handed back.</summary>
	private UiControl Panel( int id, UiRect rect, UiRect line )
	{
		var panel = Root.Add( new UiControl { Id = id, Rect = rect, Mesh = UiMesh.Get( "f_optpanel" ) } );
		return panel.Add( LineControl( line ) );
	}

	/// <summary>
	/// A slider row. They all share their track and thumb across - 990 to 1248 and 1023 to 1090 - and
	/// what takes the pointer, 962 to 1281; so each is given only the tops that differ, of the track, the
	/// thumb, the line and the pointer's rectangle.
	/// </summary>
	private (UiSlider Slider, UiControl Line) Slider( int id, UiRect rect, string mesh, int trackTop, int thumbTop, int lineTop, int hitTop, Action moved )
	{
		var slider = Root.Add( new UiSlider
		{
			Id = id,
			Rect = rect,
			Mesh = UiMesh.Get( mesh ),
			Track = new UiRect( 990, trackTop, 1248, trackTop + 71 ),
			HitRect = new UiRect( 962, hitTop, 1281, hitTop + 135 )
		} );

		slider.AddThumb( new UiSliderThumb
		{
			Id = 3,
			Rect = new UiRect( 1023, thumbTop, 1090, thumbTop + 67 ),
			Mesh = UiMesh.Get( "b_scroller" )
		} );

		var line = slider.Add( LineControl( new UiRect( 117, lineTop, 706, lineTop + 45 ) ) );
		slider.Moved = moved;

		return (slider, line);
	}

	private void Button( int id, UiRect rect, string mesh, Action clicked )
		=> Root.Add( new UiButton { Id = id, Rect = rect, Mesh = UiMesh.Get( mesh ), Clicked = clicked } );

	/// <summary>A b_on switch, which says whether it is on - up - once a click has turned it.</summary>
	private UiButton Switch( int id, UiRect rect, Action<bool> switched )
	{
		var button = Root.Add( new UiButton { Id = id, Rect = rect, Mesh = UiMesh.Get( "b_on" ), Toggles = true } );
		button.Clicked = () => switched( !button.IsDown );
		return button;
	}

	private static UiControl LineControl( UiRect rect ) => new()
	{
		Id = LineId,
		Rect = rect,
		Font = LineFont,
		TextColour = UiColour.Black,
		TextAcross = TextAlign.Start
	};
}
