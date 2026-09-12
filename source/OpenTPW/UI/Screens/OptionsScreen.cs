namespace OpenTPW.UI;

/// <summary>
/// Game Options, which the game menu's Options opens.
///
/// <para>
/// OptionsScreen_Open (0x004a3a30) quietens the advisor, puts the open windows away and loads
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
/// <b>Sliders</b> run from 0 to 100 in the original. Graphics quality shows the setting times 50 and
/// takes back the slider times two over 100 in whole numbers, so 0 to 49 is the first setting, 50 to 99
/// the second and only 100 the third; in software rendering it takes back only the slider over 100, so
/// it goes no higher than medium (0x004a2e90). The volumes and audio quality show their number.
/// <b>Screen resolution does not run to 100 here.</b> It steps through the modes the display says it
/// has (<see cref="Display.Modes"/>), so its range is one less than however many there are and one
/// notch of the wheel is one mode, rather than a hundredth of a range of three.
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
/// volumes, writes the machine's options to save\Config.tcf (<see cref="ConfigFile"/>), and closes
/// (0x004237f0). The player's options reach their gms.dat only when the player is next saved - on Select
/// New Player or as the game closes - which is the original's way too. Escape does nothing here - the
/// callback takes no keys. As it closes, the windows it put away come back and any button glints go
/// (message 0x14).
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
/// <item>The display and the resolution are put into effect by the tick instead, and need no restart:
/// the window here is resized while the game runs. They are the only settings the tick acts on rather
/// than merely keeping, and it acts only when one of them actually changed, so a tick that moved a
/// volume leaves the screen alone.</item>
/// </list>
/// </para>
/// <para>
/// <b>The rendering row is now the display row.</b> The original has no row for windowed or full
/// screen and no text for one - it chose between its two ways as it started and never showed the
/// choice (see <see cref="Display"/>) - and on a modern machine "3D card rendering or software" is a
/// choice about a renderer that will never exist here. So that row carries the display mode instead,
/// which keeps the screen's layout exactly the original's compiled stream. Rendering itself is no
/// longer reachable from the interface; <see cref="GameOptions.CardRendering"/> is still read from
/// Config.tcf and written back to it, and still caps graphics quality, so nothing about the original's
/// file changes.
/// <para>
/// <b>Still dead, by choice:</b> the video card does nothing, and with one card counted it never
/// leaves Primary, as on a machine with one (0x004a3480). Audio quality, Tutorial, Confirmations, RMB
/// cancel, Rotation and Scroll are kept, but nothing reads them yet - they belong to the sound
/// library's set-up and to parks.
/// </para>
/// </para>
/// <para>
/// <b>Engine and content.</b> The screen is engine: one screen for the lobby and parks, opening and closing
/// itself as OptionsScreen_Open does. The options it shows are kept in <see cref="GameOptions"/>, with their
/// defaults read from sound.sam at the boundary, and written to the original's save files by
/// <see cref="SaveFolder"/>.
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

	/// <summary>The green screen everything sits on, over the dimmed window - see the constructor.</summary>
	private readonly UiControl _screen;

	private readonly UiControl _displayLine;
	private readonly UiControl _videoCard;

	/// <summary>The sizes the display can be put into, which the resolution slider steps through.</summary>
	private readonly IReadOnlyList<VideoMode> _modes;
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

	/// <summary>
	/// OptionsScreen_Open (0x004a3a30), which the lobby's game menu (0x0048bf28) and a park's (0x0048b977) both
	/// call. It quietens the advisor, fading his voice rather than cutting it (Advisor_StopQuietly with 1); puts
	/// away the windows that are open (message 6); and opens the screen over everything. It does nothing while
	/// the screen is already up (0x007cb2fc).
	/// </summary>
	public static void Open( WindowStack stack )
	{
		if ( stack.Windows.Any( window => window is OptionsScreen ) )
			return;

		Advisor.Current?.StopQuietly();

		foreach ( var window in stack.Windows )
			window.Hidden = true;

		stack.Open( new OptionsScreen( stack ) );
	}

	private OptionsScreen( WindowStack stack ) : base( stack )
	{
		Modal = true;
		Pauses = true;

		// Asked once, as the screen opens: the slider steps through these, so it has to know how many
		// there are before it is built.
		_modes = Window.Current is { } display ? Display.Modes( display ) : [];

		// f_screen is 4:3 artwork - a green field of waves with its border painted in - so it is
		// drawn at the interface's own shape rather than stretched over the window. That leaves the
		// lobby showing down both sides of it on a window of any other shape, which is what every
		// window that was not 4:3 used to do, so the screen sits on the same dimmed backdrop the
		// game menu and the message boxes already use and covers the window between them.
		Root = Backdrop();

		_screen = Root.Add( new UiControl
		{
			Id = 0x1d4c0,
			Rect = VirtualScreen.Whole,
			Mesh = UiMesh.Get( "f_screen" ),
			HoldsChildren = true
		} );

		// In the order the layout stream has them, but for the buttons, which 0x004a3a30 raises over
		// everything else after loading - they sit on the panels.
		_displayLine = Panel( 0x1d4c8, new UiRect( 57, 167, 668, 316 ), new UiRect( 124, 218, 424, 263 ) );
		_videoCard = Panel( 0x1d4ca, new UiRect( 675, 167, 1286, 316 ), new UiRect( 739, 218, 1039, 263 ) );
		_advisorLine = Panel( 0x1d4cb, new UiRect( 1331, 167, 1964, 316 ), new UiRect( 1401, 215, 1701, 260 ) );
		_tutorialLine = Panel( 0x1d4cc, new UiRect( 1331, 323, 1964, 472 ), new UiRect( 1401, 371, 1701, 416 ) );
		_popupHelpLine = Panel( 0x1d4cd, new UiRect( 1331, 479, 1964, 628 ), new UiRect( 1401, 528, 1701, 573 ) );
		_confirmationsLine = Panel( 0x1d4ce, new UiRect( 1331, 681, 1964, 830 ), new UiRect( 1401, 732, 1701, 777 ) );
		_rmbCancelLine = Panel( 0x1d4cf, new UiRect( 1331, 837, 1964, 986 ), new UiRect( 1401, 888, 1701, 932 ) );
		_rotationLine = Panel( 0x1d4d0, new UiRect( 1331, 994, 1964, 1143 ), new UiRect( 1401, 1044, 1701, 1089 ) );
		_scrollLine = Panel( 0x1d4d1, new UiRect( 1331, 1150, 1964, 1299 ), new UiRect( 1401, 1200, 1701, 1245 ) );

		_screen.Add( new UiControl
		{
			Id = 0x1d4d4,
			Rect = new UiRect( 788 - 236, 46, 1260 + 236, 126 ),
			Text = Localization.Get( UIStrings.GameOptions ),
			Font = TitleFont,
			TextColour = TitleColour,
			TextShadow = true
		} );

		// One notch of this one is one mode, rather than one hundredth of a range of three.
		(_resolution, _resolutionLine) = Slider( 0x1d4d5, new UiRect( 57, 323, 1289, 472 ), "f_optpanel3", 361, 364, 375, 331, ResolutionMoved, Math.Max( _modes.Count - 1, 0 ) );
		(_quality, _qualityLine) = Slider( 0x1d4d6, new UiRect( 57, 479, 1289, 628 ), "f_optpanel3", 518, 520, 532, 487, QualityMoved );
		(_effects, _effectsLine) = Slider( 0x1d4d8, new UiRect( 52, 838, 1289, 988 ), "f_optpanel2", 877, 880, 891, 845, EffectsMoved );
		(_music, _musicLine) = Slider( 0x1d4da, new UiRect( 52, 994, 1289, 1144 ), "f_optpanel2", 1033, 1035, 1047, 1004, MusicMoved );
		(_speech, _speechLine) = Slider( 0x1d4dc, new UiRect( 52, 1151, 1289, 1301 ), "f_optpanel2", 1190, 1192, 1204, 1157, SpeechMoved );
		(_movie, _movieLine) = Slider( 0x1d4de, new UiRect( 52, 1307, 1289, 1457 ), "f_optpanel2", 1346, 1348, 1360, 1313, MovieMoved );

		_screen.Add( new UiControl
		{
			Id = 0x1d4df,
			Rect = new UiRect( 1742, 1321, 1947, 1443 ),
			Mesh = UiMesh.Get( "!f_plain" )
		} );

		(_audioQuality, _audioQualityLine) = Slider( 0x1d4e0, new UiRect( 57, 638, 1289, 787 ), "f_optpanel3", 677, 679, 691, 646, AudioQualityMoved );

		Button( 0x1d4d2, new UiRect( 521, 190, 624, 292 ), "b_on2", SwitchDisplayMode );
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

	/// <summary>
	/// The screen has closed (0x004a2bf0, message 0x14): the windows it put away come back, each shown again, and
	/// any button glints go.
	/// </summary>
	protected internal override void Closed()
	{
		foreach ( var window in Stack.Windows.Where( window => window.Hidden ).ToArray() )
		{
			window.Hidden = false;
			window.Shown();
		}

		Stack.StopGlint();
	}

	/// <summary>Shows every option as it stands - the second half of 0x004a3a30.</summary>
	private void ShowOptions()
	{
		ShowDisplay();
		ShowVideoCard();

		_audioQuality.SetValue( Options.AudioQuality );
		AudioQualityMoved();

		ShowVolume( _effects, _effectsSwitch, Options.EffectsVolume, Options.EffectsOn, EffectsMoved );
		ShowVolume( _music, _musicSwitch, Options.MusicVolume, Options.MusicOn, MusicMoved );
		ShowVolume( _speech, _speechSwitch, Options.SpeechVolume, Options.SpeechOn, SpeechMoved );
		ShowVolume( _movie, _movieSwitch, Options.MovieVolume, Options.MovieOn, MovieMoved );

		_resolution.SetValue( NearestMode( Options.FullScreenSize ) );
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
		SaveFolder.SaveConfig();
		SaveFolder.SaveDisplay();

		// The display is changed here rather than as the row is clicked, so that stepping through the
		// modes does not take the screen with it, and a tick that changed nothing about the display
		// leaves the window alone entirely.
		if ( (Options.DisplayMode != _before.DisplayMode || Options.FullScreenSize != _before.FullScreenSize)
			&& Window.Current is { } window )
		{
			Display.Apply( window, Options.DisplayMode, Options.FullScreenSize );
		}

		Log.Info( "Options: kept" );
		Stack.Close( this );
	}

	/// <summary>The cross (-2): the options go back to how they were - 0x00423ad0, and the group volumes with them.</summary>
	private void PutBack()
	{
		GameOptions.Current = _before;
		GameOptions.Current.ApplySound();
		Log.Info( "Options: cancelled" );
		Stack.Close( this );
	}

	/// <summary>The display row's arrow (0x1d4d2): round the three ways the game can fill the screen.</summary>
	private void SwitchDisplayMode()
	{
		Options.DisplayMode = Options.DisplayMode switch
		{
			DisplayMode.Windowed => DisplayMode.FullScreen,
			DisplayMode.FullScreen => DisplayMode.BorderlessFullScreen,
			_ => DisplayMode.Windowed
		};

		ShowDisplay();
		ShowResolution();
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
		if ( Chosen() is { } mode )
		{
			Options.FullScreenSize = mode;

			// And the nearest of the original's own three into the field its Config.tcf owns, so that
			// file still says something true about the game - see GameOptions.ScreenResolution.
			Options.ScreenResolution = mode.Width <= 512 ? 0 : mode.Width <= 640 ? 1 : 2;
		}

		ShowResolution();
	}

	/// <summary>The mode the slider is sitting on, or nothing if the display offered none at all.</summary>
	private VideoMode? Chosen()
		=> _modes.Count == 0 ? null : _modes[Math.Clamp( _resolution.Value, 0, _modes.Count - 1 )];

	/// <summary>
	/// Where in the list a size sits: itself if the display has it, otherwise whichever mode is nearest
	/// it by area. A size of nothing - a game that has never been put into full screen - starts at the
	/// mode the desktop is already in, which is what someone switching to full screen expects.
	/// </summary>
	private int NearestMode( VideoMode wanted )
	{
		if ( _modes.Count == 0 )
			return 0;

		if ( wanted.Width <= 0 || wanted.Height <= 0 )
			wanted = (Window.Current is { } window ? Display.Desktop( window ) : null) ?? _modes[^1];

		var best = 0;
		var closest = long.MaxValue;

		for ( int i = 0; i < _modes.Count; ++i )
		{
			var difference = Math.Abs( ((long)_modes[i].Width * _modes[i].Height) - ((long)wanted.Width * wanted.Height) );

			if ( difference < closest )
			{
				closest = difference;
				best = i;
			}
		}

		return best;
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

	/// <summary>
	/// The display row. Its words are OpenTPW's own: the original's UITEXT has nothing for windowed or
	/// full screen, because it chose between its two ways as it started and never showed the choice
	/// (see <see cref="Display"/>), so these are written in the style of the lines around them - the
	/// same way the delete box's missing question is.
	/// </summary>
	private void ShowDisplay()
	{
		_displayLine.Text = $"Display: {Name( Options.DisplayMode )}";

		// The resolution is only a choice in full screen - see ShowResolution.
		_resolution.Enabled = Options.DisplayMode == DisplayMode.FullScreen;

		static string Name( DisplayMode mode ) => mode switch
		{
			DisplayMode.FullScreen => "Full screen",
			DisplayMode.BorderlessFullScreen => "Borderless full screen",
			_ => "Windowed"
		};
	}

	private void ShowVideoCard()
		=> _videoCard.Text = Line( UIStrings.Videocard, Localization.Get( Options.SecondaryVideoCard && VideoCards > 1 ? UIStrings.Secondary : UIStrings.Primary ) );

	/// <summary>
	/// The resolution line. It is only a choice in full screen, where the display is given a mode of
	/// its own. Borderless full screen takes whatever mode the desktop is in, and a window is whatever
	/// size it has been dragged to, so in both of those the line says what is being used rather than
	/// offering something that would do nothing.
	/// </summary>
	private void ShowResolution()
	{
		var text = Options.DisplayMode switch
		{
			DisplayMode.FullScreen => Chosen()?.ToString(),
			DisplayMode.BorderlessFullScreen => (Window.Current is { } window ? Display.Desktop( window ) : null)?.ToString(),
			_ => $"{Screen.Size.X} x {Screen.Size.Y}"
		};

		_resolutionLine.Text = Line( UIStrings.ScreenResolution, $" {text ?? "-"}" );
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
		var panel = _screen.Add( new UiControl { Id = id, Rect = rect, Mesh = UiMesh.Get( "f_optpanel" ) } );
		return panel.Add( LineControl( line ) );
	}

	/// <summary>
	/// A slider row. They all share their track and thumb across - 990 to 1248 and 1023 to 1090 - and
	/// what takes the pointer, 962 to 1281; so each is given only the tops that differ, of the track, the
	/// thumb, the line and the pointer's rectangle.
	/// </summary>
	private (UiSlider Slider, UiControl Line) Slider( int id, UiRect rect, string mesh, int trackTop, int thumbTop, int lineTop, int hitTop, Action moved, int maximum = 100 )
	{
		var slider = _screen.Add( new UiSlider
		{
			Id = id,
			Rect = rect,
			Mesh = UiMesh.Get( mesh ),
			Maximum = maximum,
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
		=> _screen.Add( new UiButton { Id = id, Rect = rect, Mesh = UiMesh.Get( mesh ), Clicked = clicked } );

	/// <summary>A b_on switch, which says whether it is on - up - once a click has turned it.</summary>
	private UiButton Switch( int id, UiRect rect, Action<bool> switched )
	{
		var button = _screen.Add( new UiButton { Id = id, Rect = rect, Mesh = UiMesh.Get( "b_on" ), Toggles = true } );
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
