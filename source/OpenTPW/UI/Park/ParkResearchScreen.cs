namespace OpenTPW.UI;

/// <summary>
/// The research lab's screen, "Research Lab", which the research opener builds in Full Simulation with a researcher on
/// the staff (<see cref="ParkResearchOpener"/>; <c>ResearchScreen_Open</c>, <c>0x004aa480</c>, handler
/// <c>ResearchScreen_OnMessage</c>, <c>0x004a96b0</c>).
///
/// <para>
/// <b>Where the layout comes from.</b> The compiled stream at <c>0x00754490</c>; every rect, id, help row and mesh below
/// is that stream's (<c>docs/exe/hud.md</c>, "The research screen's panels"). A panel a kind of research, top to bottom
/// rides, upgrades, shops, sideshows and miscellaneous: its icon, its two arrows, its bar and its pump, with the
/// kind's effort slider to its left. The work-load slider stands at the left with the gauge and the dial in it.
/// </para>
///
/// <para>
/// <b>The bar</b> is filled to <c>(100 - left) × 1024 / 100</c>, <c>left</c> the points left in the kind's current
/// project as a whole per cent of the dearest project's cost (<see cref="ParkResearch.PercentLeftOfDearest"/>), and
/// lettered with UITEXT 446, the project's item and its price, or 185 with nothing left. The bars are set as the screen
/// opens, on each tick of its 1000 ms timer, after a work-load drag, after an arrow, and for a kind alone when that kind
/// finishes (<see cref="ParkResearch.KindFinished"/>).
/// </para>
///
/// <para>
/// <b>The work-load slider</b> runs nought to 1023, held to a limit set by the researchers' ability added up:
/// <c>((100 - c) / 3 + c) × 1024 / most</c>, which the gauge shows. Its value <c>v</c> is the lab's work load
/// <c>30 × v / limit + 70</c>, and the dial's frame <c>2 × (30 × v / limit) / 7</c>. <b>Opening it is a change of the
/// work load</b>: the knob is set to <c>(work load - 70) × limit / 30</c>, a slider tells its handler of every change of
/// its value (<c>Slider_SetValue</c>, <c>0x0066b508</c>), and the round trip loses one where the division does not come
/// back (one researcher of ability 3: 85, then 84 at the next opening). Measured in the original
/// (<c>docs/exe/hud.md</c>). The efforts are written back as they stand, and every kind's output worked again.
/// </para>
///
/// <para>
/// <b>The pumps</b> (<see cref="ResearchPump"/>) are handed each kind's rate, <c>Lab_KindRate</c> where the kind has a
/// current project and nought where not, at the opening, after a drag and for a kind left with nothing to research
/// (<c>ResearchScreen_RefreshPumps</c>, <c>0x004a9c70</c>). The original moves them once a UI pass, on no clock;
/// <b>here they move on <see cref="Time.Delta"/> at <see cref="PassesPerSecond"/></b>, the passes measured in the
/// original under its 30 frames a second, so a pump runs as fast at any frame rate (a deviation, <c>docs/exe/hud.md</c>,
/// "The pump"). A running pump is drawn as one smooth scroll, moved between passes too, where the original steps it
/// picture by picture (a deviation, on by default, stepped under <c>graphics faithful</c>; <see cref="ResearchPump"/>).
/// </para>
///
/// <para>
/// <b>The sounds</b>, out of <c>cat_ui</c> and flat: two loops started as it opens, <c>0x9a</c> (the music's class; its
/// zone parameter <c>0x12</c> the five rates' mean, its volume parameter <c>0xc</c>) and <c>0x9b</c> (the music's class,
/// parameter <c>0xd</c> both its zone and its volume and pitch), both <c>0xc</c> and <c>0xd</c> set from the work-load
/// slider's value, <c>v × 100 / 1024</c>; and <c>0x9c</c> (the plain class) while an effort thumb is held, its volume
/// parameter <c>0xa</c> and pitch parameter <c>0xb</c> the effort, faded at the release. All three stop as it goes.
/// </para>
/// </summary>
internal sealed class ParkResearchScreen : UiWindow
{
	/// <summary>The work-load slider's ends (<c>Slider_SetRange( 0, 0x3ff )</c>) and the efforts' (0 to 100).</summary>
	internal const int WorkLoadTop = 0x3ff;

	internal const int EffortTop = 100;

	/// <summary>The work load at the work-load slider's foot, and how much its travel up to the limit adds.</summary>
	internal const int LowestWorkLoad = 70;

	internal const int WorkLoadSpan = 30;

	/// <summary>How often the screen's timer sets the bars again, in real milliseconds (<c>0x80080</c>, armed with 1000 on message <c>0x15</c>).</summary>
	internal const long TickEvery = 1000;

	/// <summary>
	/// The original's UI passes a second, each of which moves a pump once (message <c>0x1e</c>, <c>UI_TickTree</c>), measured
	/// with the screen open under its 30 frames a second (<c>docs/exe/hud.md</c>, "The pump").
	/// </summary>
	internal const float PassesPerSecond = 30f;

	/// <summary>The screen's three sounds in <c>cat_ui</c>, and the parameters it sets on them.</summary>
	internal const int LoopSound = 0x9a, WorkSound = 0x9b, EffortSound = 0x9c;

	internal const int RateParameter = 0x12, LoopVolumeParameter = 0xc, WorkParameter = 0xd, EffortVolumeParameter = 0xa,
		EffortPitchParameter = 0xb;

	/// <summary>The bars' label: font slot 6 (<c>FUN_00485a70</c>), red (<c>FUN_0065c5d5( 0xe0, 0x56, 0x56, 0xff )</c>), in the middle both ways.</summary>
	private const int LabelFont = 6;

	private static readonly UiColour LabelColour = new( 224, 86, 86 );

	/// <summary>The bars' texture (<c>FUN_00477ff0</c> with <c>bluebar.wct</c>) and the gauge's (<c>Meter_Construct</c> with <c>effortguage.wct</c>).</summary>
	private const string BarSkin = "ui/textures/bluebar.wct";

	private const string GaugeSkin = "ui/textures/effortguage.wct";

	/// <summary>
	/// One kind's row: its panel's id and top, the kind (a project's <c>mCat</c>), its icon, the left arrow's help row (the
	/// right's is the next), and its effort slider's id, rect top, help row, thumb (op 8) and track (its text rect). In the
	/// stream's order of panels.
	/// </summary>
	internal static readonly Row[] Rows =
	[
		new( 0xd2d01, 158, ItemDescriptionFile.Ride, "h_ride", 249, 0xd2d0e, 178, 244, new( 579, 199, 646, 267 ), new( 532, 199, 714, 268 ) ),
		new( 0xd2d07, 318, ParkResearch.Upgrade, "h_upg", 251, 0xd2d0f, 338, 245, new( 579, 360, 646, 427 ), new( 532, 355, 714, 423 ) ),
		new( 0xd2d08, 479, ShopKind, "h_shop", 253, 0xd2d0d, 499, 246, new( 579, 521, 646, 588 ), new( 532, 517, 714, 585 ) ),
		new( 0xd2d09, 640, ItemDescriptionFile.SideShow, "h_show", 255, 0xd2d0c, 660, 247, new( 584, 683, 652, 750 ), new( 538, 678, 720, 746 ) ),
		new( 0xd2d0a, 801, ItemDescriptionFile.Feature, "h_feature", 257, 0xd2d0b, 820, 248, new( 579, 842, 646, 909 ), new( 532, 838, 714, 907 ) )
	];

	internal readonly record struct Row( int Panel, int Top, int Kind, string Icon, int BackHelp, int Slider, int SliderTop,
		int SliderHelp, UiRect Thumb, UiRect Track );

	/// <summary>A shop's kind of research, its <c>WhichUIType</c>.</summary>
	private const int ShopKind = 1;

	/// <summary>The order the opener and the handler set the five bars and labels in: rides, upgrades, shops, sideshows, miscellaneous.</summary>
	private static readonly int[] KindOrder = [0, 4, 1, 2, 3];

	private readonly ParkObjectWindow.UiStatBar[] _bars = new ParkObjectWindow.UiStatBar[ParkWorld.ResearchKindCount];
	private readonly UiControl[] _pumps = new UiControl[ParkWorld.ResearchKindCount];
	private readonly ResearchPump[] _pumpStates = Enumerable.Range( 0, ParkWorld.ResearchKindCount ).Select( _ => new ResearchPump() ).ToArray();

	/// <summary>The passes owed and not yet run, as a fraction of one.</summary>
	private float _passesOwed;
	private readonly UiSlider[] _efforts = new UiSlider[ParkWorld.ResearchKindCount];
	private readonly UiSlider _workLoad;
	private readonly UiControl _dial;
	private readonly UiMeter _gauge;
	private readonly UiTimer _tick = new( TickEvery );

	/// <summary>The lab the screen was opened over, and the researchers' ability added up as it stands; a test gives its own.</summary>
	private readonly ParkResearch? _lab;
	private readonly Func<int> _abilityNow;

	/// <summary>The most research the staff could do (<c>[0x007cc1b4]</c>, set at the opening), the ability added up the limit was last set by (<c>[0x007cc1c8]</c>) and the limit (<c>[0x007cc1c0]</c>).</summary>
	private readonly int _most;
	private int _ability, _limit;

	/// <summary>Whether the opening is still seeding the sliders (<c>[0x007cc1bc]</c>): no drag's pump refresh runs then.</summary>
	private bool _seeding;

	/// <summary>The park whose advisor this screen has asked for its line.</summary>
	private ParkState? _asked;

	/// <summary>The two loops (<c>[0x007cc1d0]</c>, <c>[0x007cc1cc]</c>) and the effort's sound while a thumb is held (<c>[0x007cc1d4]</c>).</summary>
	private readonly ParkAudio? _audio;
	private readonly ParkAudio.ScriptVoice? _loop, _work;
	private ParkAudio.ScriptVoice? _effort;

	public ParkResearchScreen( WindowStack stack )
		: this( stack, Level.Current?.Research, () => ParkPeople.Current?.ResearchAbility ?? 0, ParkPeople.Current?.MostResearchAbility ?? 0,
			ParkAudio.Current ) { }

	/// <param name="ability">The researchers' <c>ResearchAbility</c> added up, asked as the screen opens and on every pass (<c>FUN_004c9e60</c>).</param>
	/// <param name="most">The most research the staff could do (<c>FUN_004c9ed0</c>), asked as the screen opens.</param>
	/// <param name="audio">The park's sounds, which play the screen's; null for none.</param>
	internal ParkResearchScreen( WindowStack stack, ParkResearch? lab, Func<int> ability, int most, ParkAudio? audio = null ) : base( stack )
	{
		// Built onto the park's own layer, as the other park screens are: not modal, and the park goes on.
		ParkScreen = true;
		_lab = lab;
		_abilityNow = ability;
		_audio = audio;

		// w_res: the stream's hash 0x6c9ffd68 is the node "research_wind".
		Root = new UiControl
		{
			Id = 0xd2cff,
			Rect = new UiRect( 186, 30, 2018, 1032 ),
			Mesh = UiMesh.Get( "w_res" )
		};

		// UITEXT 184, "Research Lab", lettered by UI_SetTitle on 0xd2d00 on message 0x15: font 5 in (234, 239, 102), in
		// the stream's rect (866 to 1338) widened by half its width either side.
		Root.Add( new UiControl
		{
			Id = 0xd2d00,
			Rect = new UiRect( 630, 67, 1574, 146 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			Text = Localization.Get( UIStrings.ResearchLab )
		} );

		foreach ( var row in Rows )
			Root.Add( Panel( row ) );

		foreach ( var row in Rows )
			_efforts[row.Kind] = Root.Add( EffortSlider( row ) );

		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1889, 844, 1972, 927 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		// The work-load slider: type 3, flags 0x51, the one that runs up and down; its track is the stream's text rect.
		_workLoad = Root.Add( new UiSlider
		{
			Id = 0xd2d10,
			Rect = new UiRect( 234, 148, 464, 968 ),
			HitRect = new UiRect( 234, 148, 464, 968 ),
			Track = new UiRect( 273, 220, 330, 701 ),
			Vertical = true,
			HelpText = 238,
			Mesh = UiMesh.Get( "res_mainslider" ),
			Maximum = WorkLoadTop,
			Moved = WorkLoadMoved
		} );

		_workLoad.AddThumb( new UiSliderThumb { Id = 3, Rect = new UiRect( 267, 399, 334, 466 ), Mesh = UiMesh.Get( "b_scroller" ) } );

		_dial = _workLoad.Add( new UiControl { Id = 0xd2d11, Rect = new UiRect( 262, 760, 438, 936 ), Mesh = UiMesh.Get( "res_faces" ) } );

		// The gauge, type 9 under Meter_Construct's effortguage.wct, first given 500 of 1024 and then the limit.
		_gauge = _workLoad.Add( new UiMeter { Id = 0xd2d12, Rect = new UiRect( 354, 238, 422, 683 ), Skin = GaugeSkin, Max = 0x400 } );
		_gauge.Value = 500;

		_seeding = true;

		// The opening's two loops, started before the knob is set, so its round trip sets their parameters.
		_loop = _audio?.PlayInterfaceSound( LoopSound );
		_work = _audio?.PlayInterfaceSound( WorkSound );

		_most = most;
		SetLimit( ability() );

		foreach ( var kind in KindOrder )
			SetProjectText( kind );

		SetFills();

		// Each effort slider given its kind's effort (Lab_KindEffort), which tells the handler as a drag does where it
		// is not the slider's nought: Lab_SetEffort with the effort as it stands.
		foreach ( var kind in KindOrder )
			SeedEffort( kind );

		_seeding = false;
		RefreshPumps( -1 );

		Log.Info( $"Research screen: opened, {Census()}" );
	}

	/// <summary>
	/// The screen as it stands, for the log and the console: the work load, the slider's limit, knob and the dial's
	/// frame, the ability and most it was set by, then rides, upgrades, shops, sideshows and miscellaneous in turn, each
	/// bar's units, its label, its effort and the lab's output.
	/// </summary>
	internal string Census()
		=> $"workload {_lab?.WorkLoad ?? 0} limit {_limit} knob {_workLoad.Value} dial {_dial.Frame} gauge {_gauge.Value} ability {_ability} most {_most}; "
			+ string.Join( "; ", KindOrder.Select( kind => $"kind {kind} bar {_bars[kind].Units} effort {_efforts[kind].Value} "
				+ $"output {(_lab is { } lab && kind < lab.Kinds.Count ? lab.Kinds[kind].Output : 0)} label '{_bars[kind].Text?.Replace( "\n", " / " )}'"
				+ $" rate {PumpRate( kind )} pump {_pumpStates[kind]} passes {_pumpStates[kind].Passes}" ) )
			+ $"; sounds {Sound( "loop", _loop )}, {Sound( "work", _work )}, {Sound( "effort", _effort )}";

	private static string Sound( string name, ParkAudio.ScriptVoice? sound )
		=> sound is null ? $"{name} none"
			: $"{name} {sound.Effect} {(sound.Stopped ? "stopped" : sound.Voice is { Playing: true } ? "playing" : "silent")} variation {sound.Variation + 1} "
				+ $"samples {sound.Samples} values {string.Join( ",", sound.Values )} volume {sound.Kept.Volume} pitch {sound.Kept.Pitch}";

	/// <summary>One kind's panel: <c>res_panel</c> skinned <c>res_pipe</c> in its text rect, its icon, arrows, bar and pump.</summary>
	private UiControl Panel( Row row )
	{
		// The stream's rects of the first panel, 160 or 161 down a row; its own differ from that by a unit here and there.
		var down = row.Top - Rows[0].Top;

		var panel = new UiControl
		{
			Id = row.Panel,
			Rect = new UiRect( 785, row.Top, 1830, row.Top + 150 ),
			Mesh = UiMesh.Get( "res_panel" )
		};

		// The skin (op 2, the node "pipe") is drawn in the panel's text rect, under its children.
		panel.Add( new UiControl { Rect = new UiRect( 1497, 175 + down, 1804, 294 + down ), Mesh = UiMesh.Get( "res_pipe" ) } );

		panel.Add( new UiControl { Id = 0xd2d02, Rect = new UiRect( 816, 182 + down, 919, 284 + down ), Mesh = UiMesh.Get( row.Icon ) } );

		// The panel's proc on 0x100 (0x004aa0c0 and its four twins): the left arrow Lab_PickPreviousProject, the right
		// Lab_PickNextProject, then the label and the bar.
		panel.Add( Arrow( 0xd2d03, new UiRect( 924, 191 + down, 1007, 275 + down ), row.BackHelp, "b_sleft", row.Kind, back: true ) );
		panel.Add( Arrow( 0xd2d04, new UiRect( 1012, 191 + down, 1096, 275 + down ), row.BackHelp + 1, "b_sright", row.Kind, back: false ) );

		_bars[row.Kind] = panel.Add( new ParkObjectWindow.UiStatBar
		{
			Id = 0xd2d05,
			Rect = new UiRect( 1132, 179 + down, 1497, 284 + down ),
			HelpText = 259,
			Skin = BarSkin,
			Known = true,
			Font = LabelFont,
			TextColour = LabelColour
		} );

		_pumps[row.Kind] = panel.Add( new UiControl
		{
			Id = 0xd2d06,
			Rect = new UiRect( 1555, 196 + down, 1712, 264 + down ),
			HelpText = 260,
			Mesh = UiMesh.Get( "res_fluid" ),
			Frame = ResearchPump.RestFrame
		} );

		return panel;
	}

	private UiButton Arrow( int id, UiRect rect, int help, string mesh, int kind, bool back )
		=> new()
		{
			Id = id,
			Rect = rect,
			HelpText = help,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () =>
			{
				if ( back )
					_lab?.PickPrevious( kind );
				else
					_lab?.PickNext( kind );

				SetProjectText( kind );
				SetFill( kind );
				Log.Info( $"Research screen: the {(back ? "left" : "right")} arrow of kind {kind}, project {CurrentProject( kind )}, {Census()}" );
			}
		};

	/// <summary>One kind's effort slider: type 3, flags 0x61, a <c>b_scroller</c> on <c>res_slider</c> (the node <c>effortslidea</c>), 0 to 100.</summary>
	private UiSlider EffortSlider( Row row )
	{
		var rect = new UiRect( 472, row.SliderTop, 769, row.SliderTop + 111 );
		var slider = new UiSlider
		{
			Id = row.Slider,
			Rect = rect,
			HitRect = rect,
			Track = row.Track,
			HelpText = row.SliderHelp,
			Mesh = UiMesh.Get( "res_slider" ),
			Maximum = EffortTop
		};

		slider.Moved = () => EffortMoved( row.Kind, slider.Value );
		slider.ThumbPressed = () => EffortPressed( slider.Value );
		slider.ThumbReleased = EffortReleased;
		slider.AddThumb( new UiSliderThumb { Id = 3, Rect = row.Thumb, Mesh = UiMesh.Get( "b_scroller" ) } );

		return slider;
	}

	private void SeedEffort( int kind )
	{
		var effort = _lab is { } lab && kind < lab.Kinds.Count ? lab.Kinds[kind].Effort : 0;
		var was = _efforts[kind].Value;

		_efforts[kind].SetValue( effort );

		if ( _efforts[kind].Value != was )
			EffortMoved( kind, _efforts[kind].Value );
	}

	/// <summary>
	/// Message <c>0x800</c> from an effort slider: <c>Lab_SetEffort</c> with the value, then (past the opening) the pumps,
	/// and the effort's sound's parameters <c>0xb</c> and <c>0xa</c> where one is held.
	/// </summary>
	private void EffortMoved( int kind, int value )
	{
		_lab?.SetEffort( kind, value );

		if ( !_seeding )
			RefreshPumps( -1 );

		SetParameter( _effort, EffortPitchParameter, value );
		SetParameter( _effort, EffortVolumeParameter, value );

		if ( !_seeding )
			Log.Info( $"Research screen: effort of kind {kind} to {value}, {Census()}" );
	}

	/// <summary>Message <c>0x801</c>, an effort thumb taken hold of: sound <c>0x9c</c>, its parameters <c>0xb</c> and <c>0xa</c> the effort.</summary>
	private void EffortPressed( int value )
	{
		_effort = _audio?.PlayInterfaceSound( EffortSound );
		SetParameter( _effort, EffortPitchParameter, value );
		SetParameter( _effort, EffortVolumeParameter, value );
		Log.Info( $"Research screen: an effort thumb pressed at {value}, sound {EffortSound} {(_effort == null ? "not made" : "made")}" );
	}

	/// <summary>Message <c>0x802</c>, an effort thumb let go: its sound faded (<c>Sound_StopFading</c>) and forgotten.</summary>
	private void EffortReleased()
	{
		if ( _effort != null )
			_audio?.StopScriptSound( _effort, fade: true );

		_effort = null;
	}

	/// <summary><c>Sound_SetParameter</c> on one of the screen's voices; nothing for none, as a handle of nought finds none.</summary>
	private void SetParameter( ParkAudio.ScriptVoice? sound, int id, int value )
	{
		if ( sound != null )
			_audio?.SetScriptSoundParameter( sound, id, value );
	}

	/// <summary>
	/// The limit (<c>0x004aacce</c>): the gauge set to it and the knob to <c>(work load - 70) × limit / 30</c>, which
	/// tells the handler where the value changes (<see cref="WorkLoadMoved"/>).
	/// </summary>
	private void SetLimit( int ability )
	{
		_ability = ability;
		_limit = _most == 0 ? 0 : (((100 - ability) / 3) + ability) * 0x400 / _most;
		_gauge.Value = _limit;

		var was = _workLoad.Value;
		_workLoad.SetValue( ((_lab?.WorkLoad ?? LowestWorkLoad) - LowestWorkLoad) * _limit / WorkLoadSpan );

		if ( _workLoad.Value != was )
			WorkLoadMoved();
	}

	/// <summary>
	/// Message <c>0x800</c> from the work-load slider: the value held to the limit, <c>Lab_SetWorkLoad( 30 × v / limit
	/// + 70 )</c>, the pumps (past the opening), the five bars and the dial.
	/// </summary>
	private void WorkLoadMoved()
	{
		// A limit of nought divides by nought in the original (no park has no top grade); here the slider stands still.
		if ( _limit <= 0 )
			return;

		if ( _workLoad.Value > _limit )
			_workLoad.SetValue( _limit );

		var along = _workLoad.Value * WorkLoadSpan / _limit;
		_lab?.SetWorkLoad( along + LowestWorkLoad );

		if ( !_seeding )
			RefreshPumps( -1 );

		SetFills();
		_dial.Frame = 2 * along / 7;

		// Both loops' 0xc and 0xd, in the original's order: 0x9a takes 0xc, 0x9b 0xd.
		var level = _workLoad.Value * 100 / 1024;
		SetParameter( _loop, LoopVolumeParameter, level );
		SetParameter( _work, LoopVolumeParameter, level );
		SetParameter( _loop, WorkParameter, level );
		SetParameter( _work, WorkParameter, level );

		Log.Info( $"Research screen: work load slider at {_workLoad.Value}, {Census()}" );
	}

	/// <summary><c>ResearchScreen_SetFill</c> (<c>0x004a9bd0</c>): the kind's bar at <c>(100 - left) × 1024 / 100</c>.</summary>
	private void SetFill( int kind ) => _bars[kind].Units = (100 - (_lab?.PercentLeftOfDearest( kind ) ?? 0)) * 0x400 / 100;

	private void SetFills()
	{
		foreach ( var kind in KindOrder )
			SetFill( kind );
	}

	/// <summary>
	/// <c>ResearchScreen_SetProjectText</c> (<c>0x004aa120</c>): UITEXT 446 with the current project's item's name and
	/// <see cref="ParkResearch.EstimatedPrice"/>, or UITEXT 185, "All Items Researched", and that kind's pump.
	/// </summary>
	private void SetProjectText( int kind )
	{
		var item = _lab?.CurrentItem( kind ) ?? 0;

		if ( item == 0 || _lab?.Item( item ) is not { } known )
		{
			_bars[kind].Text = Localization.Get( UIStrings.AllItemsResearched );
			RefreshPumps( kind );
			return;
		}

		_bars[kind].Text = Localization.Format( ProjectText, (14, ParkObjectNames.NameOf( known )), (13, $"{_lab!.EstimatedPrice( kind )}") );
	}

	/// <summary>UITEXT 446, "{P14}" over "Estimated price : $ {P13}".</summary>
	internal const int ProjectText = 446;

	/// <summary>
	/// <c>ResearchScreen_RefreshPumps</c> (<c>0x004a9c70</c>): the pump of <paramref name="kind"/>, or of every kind for -1,
	/// handed its rate; then the loop <c>0x9a</c>'s parameter <c>0x12</c> set to the five rates' mean, in whole numbers.
	/// </summary>
	private void RefreshPumps( int kind )
	{
		foreach ( var each in KindOrder )
		{
			if ( kind < 0 || kind == each )
				_pumpStates[each].SetRate( PumpRate( each ) );
		}

		SetParameter( _loop, RateParameter, KindOrder.Sum( PumpRate ) / KindOrder.Length );
		ShowPumps();
	}

	/// <summary>A kind's pump's rate: <c>Lab_KindRate</c> (<see cref="ParkResearch.Rate"/>) with a current project, else nought.</summary>
	internal int PumpRate( int kind )
		=> _lab is { } lab && lab.CurrentItem( kind ) != 0 ? lab.Rate( kind, _abilityNow(), _most ) : 0;

	/// <summary>Each pump's part on its control, a run moved by <see cref="ResearchPump.Shift"/>.</summary>
	private void ShowPumps()
	{
		foreach ( var kind in KindOrder )
		{
			_pumps[kind].Frame = _pumpStates[kind].DrawnPart;
			_pumps[kind].TextureShift = _pumpStates[kind].Shift();
		}
	}

	/// <summary>A kind's pump, for the tests.</summary>
	internal ResearchPump Pump( int kind ) => _pumpStates[kind];

	private int CurrentProject( int kind ) => _lab is { } lab && kind < lab.Kinds.Count ? lab.Kinds[kind].CurrentProject : -1;

	/// <summary>
	/// <c>Research_OnKindFinished</c> (<c>0x004aaec0</c>) while the screen is up: the finished kind's label and bar again,
	/// which for a kind left with nothing stops its pump.
	/// </summary>
	private void KindFinished( int kind )
	{
		SetProjectText( kind );
		SetFill( kind );
		Log.Info( $"Research screen: kind {kind} finished a project, now project {CurrentProject( kind )}, {Census()}" );
	}

	/// <summary>The opener's last act (<c>FUN_00486b00( 200 )</c>): the advisor is posted the screen's line.</summary>
	protected internal override void Shown()
	{
		if ( _lab != null )
			_lab.KindFinished = KindFinished;

		if ( _asked != null || Level.Current?.ParkState is not { AdvisorMessages: { } lines } park )
			return;

		_asked = park;
		lines.Post( ParkAdvisorMessages.ResearchScreen, park.GameTick );
	}

	/// <summary>
	/// The screen's destroy (event <c>0x14</c>): the three sounds stopped (<c>Sound_Stop</c>, the effort's where one is held)
	/// and message 200 withdrawn, which cuts him off where he is saying it.
	/// </summary>
	protected internal override void Closed()
	{
		if ( _lab?.KindFinished == KindFinished )
			_lab.KindFinished = null;

		foreach ( var sound in new[] { _loop, _work, _effort } )
		{
			if ( sound != null )
				_audio?.StopScriptSound( sound, fade: false );
		}

		_asked?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.ResearchScreen, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );
	}

	protected internal override void Update()
	{
		// The timer's tick, message 0x10: the five bars.
		if ( _tick.Owed( Stack.HoldsTimers ) > 0 )
			SetFills();

		// Message 0x1e, a UI pass: the limit and the knob again where the researchers' ability has changed.
		if ( _abilityNow() is var ability && ability != _ability )
		{
			Log.Info( $"Research screen: the researchers' ability is {ability}, was {_ability}" );
			SetLimit( ability );
		}

		RunPasses( Time.Delta );
	}

	/// <summary>
	/// The pumps' passes owed over <paramref name="seconds"/> at <see cref="PassesPerSecond"/>, each moving every pump once,
	/// a second's worth at most after a long frame. The drawn runs move on by the part of the passes each stretch of the
	/// frame spans (<see cref="ResearchPump.Advance"/>), so they stand where the frame's end puts them.
	/// </summary>
	internal void RunPasses( float seconds )
	{
		var at = _passesOwed;
		_passesOwed = MathF.Min( at + (seconds * PassesPerSecond), PassesPerSecond );
		var left = _passesOwed - at;

		for ( ; _passesOwed >= 1f; _passesOwed -= 1f )
		{
			foreach ( var pump in _pumpStates )
			{
				pump.Advance( 1f - at );
				pump.Step();
			}

			left -= 1f - at;
			at = 0f;
		}

		foreach ( var pump in _pumpStates )
			pump.Advance( left );

		ShowPumps();
	}
}
