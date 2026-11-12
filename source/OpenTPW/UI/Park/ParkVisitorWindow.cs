namespace OpenTPW.UI;

/// <summary>
/// The visitor's window: one guest's figures, their status and their last thought, with arrows to the next and the
/// previous guest - one of the nine object windows (<c>docs/exe/hud.md</c>, "The person's windows").
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_004b79b0</c> builds it on the shared base <c>FUN_0048cea0</c> from the
/// stream at <c>0x007575e0</c>, with the handler <c>FUN_004b7720</c>. Every rect, id, mesh and help row is that
/// stream's. It is a park screen, so opening it closes the one open and folds the gadget's arm (<c>FUN_00485b70</c>).
/// </para>
///
/// <para>
/// <b>Who opens it</b>: the locator strip's left click on a visitor (<c>FUN_004b3c10</c>) and a row's <c>0x400</c> in the
/// visitors list (<c>0x0049351e</c>). A guest's world click opens it in the original too (<c>FUN_004879d0</c>); here the
/// pick finds no guest to click.
/// </para>
///
/// <para>
/// <b>As it is built</b> the kids' effect <c>0x81</c> plays and the advisor's message <c>0x12e</c> is posted; the
/// destroy withdraws it. The figures are filled as a guest is shown and every four seconds of real time while the
/// window is up (timer <c>0x80080</c>).
/// </para>
///
/// <para>
/// <b>Counted, not built:</b> the preview's copy of the guest's model turning in its panel (<c>FUN_00486530</c>):
/// <c>VISITOR_WINDOW_PREVIEW</c>; the panel stands empty and its click still goes to the guest.
/// </para>
/// </summary>
internal sealed class ParkVisitorWindow : UiWindow
{
	/// <summary>The status words, the table the boot loads as <c>mUIKidStates</c> (table <c>0x3ec</c>, <c>[0x0078baa0]</c>).</summary>
	internal const string StatusTable = "Language/English/KIDSTATES.str";

	/// <summary>The thoughts' words, <c>[0x0078ba9c]</c>.</summary>
	internal const string ThoughtTable = "Language/English/THOUGHTS.str";

	/// <summary>The status row that names what the guest is using, "Using: {P0}" (<c>0x004b73ea</c>).</summary>
	internal const int UsingRow = 16;

	/// <summary>The thought icon's part with no thought: past the last, so nothing is drawn.</summary>
	internal const int NoThought = 999;

	/// <summary>How often the figures are filled again, in milliseconds of real time: timer <c>0x80080</c>, armed with 4000.</summary>
	private const long RefreshEvery = 4000;

	/// <summary>The stats' eight rows: the label's id, the UITEXT row it letters and the figure's id, in the stream's rects.</summary>
	private static readonly (int Label, int Text, int Figure, int Top, int Bottom)[] Rows =
	[
		(0x372c, 70, 0x3724, 282, 327),
		(0x3725, 71, 0x372d, 333, 378),
		(0x3726, 72, 0x372e, 384, 429),
		(0x3727, 75, 0x372f, 435, 480),
		(0x3728, 73, 0x3730, 487, 531),
		(0x3729, 74, 0x3731, 538, 583),
		(0x372a, 76, 0x3732, 589, 634),
		(0x372b, 77, 0x3733, 640, 685),
	];

	/// <summary>The happiness bar's inset from its row's top and bottom (<c>FUN_00485e30( cell, 0, 10 )</c>).</summary>
	private const int BarInset = 10;

	private static StringFile? _statusWords;
	private static StringFile? _thoughtWords;

	private readonly UiTimer _refresh = new( RefreshEvery );
	private readonly UiControl _title;
	private readonly Dictionary<int, UiControl> _figures = [];
	private readonly ParkObjectWindow.UiStatBar _happiness;
	private readonly UiControl _thought;
	private readonly UiControl _thoughtIcon;

	/// <summary>The park whose advisor was told of the window, which is the one told it is gone.</summary>
	private ParkState? _told;

	/// <summary>The guest shown - the original's <c>[0x007c2658]</c>.</summary>
	internal int ThingId { get; private set; }

	/// <summary>The visitor number the title was lettered with, <c>this[9]</c>: nought while it says "New Visitor".</summary>
	internal int TitleNumber { get; private set; }

	private ParkVisitorWindow( WindowStack stack, int thingId ) : base( stack )
	{
		// Neither the base nor the builder asks for a pause.
		ParkScreen = true;
		ThingId = thingId;

		Root = new UiControl
		{
			Id = 0x371f,
			Rect = new UiRect( 328, 130, 1720, 901 ),

			// The node "window1", in w_small.MD2.
			Mesh = UiMesh.Get( "w_small" )
		};

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1591, 714, 1674, 797 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		// b_arup is FUN_0048cbe0 and b_ardown FUN_0048caf0, on the visitors' mask 0x40 (Cycle).
		Root.Add( new UiButton
		{
			Id = 0x3720,
			Rect = new UiRect( 1591, 234, 1674, 317 ),
			HelpText = 98,
			Mesh = UiMesh.Get( "b_arup" ),
			Clicked = () => Cycle( newer: true )
		} );

		Root.Add( new UiButton
		{
			Id = 0x3721,
			Rect = new UiRect( 1580, 322, 1663, 406 ),
			HelpText = 97,
			Mesh = UiMesh.Get( "b_ardown" ),
			Clicked = () => Cycle( newer: false )
		} );

		// The base's handler for the preview, 0x0048d1a0: a click of either button goes to the guest and closes.
		Root.Add( new UiControl
		{
			Id = 0x3722,
			Rect = new UiRect( 456, 267, 720, 531 ),
			Mesh = UiMesh.Get( "!frame" ),
			LeftClicked = GoToGuest,
			RightClicked = GoToGuest
		} );

		var stats = Root.Add( new UiControl
		{
			Id = 0x3723,
			Rect = new UiRect( 745, 265, 1521, 709 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		// The labels (FUN_004b6c20) and the figures, font 6, from the left as the cells' skins letter them.
		foreach ( var (label, text, figure, top, bottom) in Rows )
		{
			stats.Add( new UiControl
			{
				Id = label,
				Rect = new UiRect( 778, top, 1168, bottom ),
				Font = 6,
				TextColour = UiColour.White,
				TextAcross = TextAlign.Start,
				Text = Localization.Text( text )
			} );

			if ( figure == 0x3732 )
				continue;

			_figures[figure] = stats.Add( new UiControl
			{
				Id = figure,
				Rect = new UiRect( 1170, top, 1498, bottom ),
				Font = 6,
				TextColour = UiColour.White,
				TextAcross = TextAlign.Start
			} );
		}

		// The happiness, a type-9 bar in happygrad.wct inset 10 down.
		_happiness = stats.Add( new ParkObjectWindow.UiStatBar
		{
			Id = 0x3732,
			Rect = new UiRect( 1170, 589 + BarInset, 1498, 634 - BarInset ),
			Skin = ParkObjectWindow.UiStatBar.HappyGrad
		} );

		// The thought (FUN_004b75a0): Ethoughts3, its words font 6 in black, centred both ways, and inside it the icon
		// wearing thoughts.MD2 (the node ti_hungry).
		_thought = Root.Add( new UiControl
		{
			Id = 0x3734,
			Rect = new UiRect( 641, 720, 1352, 856 ),
			HelpText = 95,
			Mesh = UiMesh.Get( "ethoughts" ),
			TextRect = new UiRect( 817, 760, 1314, 805 ),
			Font = 6,
			TextColour = UiColour.Black,
			TextAcross = TextAlign.Centre
		} );

		_thoughtIcon = _thought.Add( new UiControl
		{
			Id = 0x3735,
			Rect = new UiRect( 676, 739, 780, 819 ),
			Mesh = UiMesh.Get( ParkVisitorsScreen.ThoughtsMesh )
		} );

		// UI_SetTitle: font 5 in (234, 239, 102), centred in the stream's rect (749 to 1221) widened by half its width
		// either side.
		_title = Root.Add( new UiControl
		{
			Id = 0x3736,
			Rect = new UiRect( 513, 174, 1457, 253 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true
		} );

		Root.Add( new UiButton
		{
			Id = 0x3737,
			Rect = new UiRect( 1568, 411, 1670, 513 ),
			HelpText = 99,
			Mesh = UiMesh.Get( "b_locate" ),
			Clicked = Locate
		} );

		Root.Add( new UiButton
		{
			Id = 0x3738,
			Rect = new UiRect( 1568, 518, 1670, 620 ),
			HelpText = 100,
			Mesh = UiMesh.Get( "b_kids" ),
			Clicked = () => Stack.Open( new ParkVisitorsScreen( Stack, pick: ThingId ) )
		} );

		Show();
	}

	/// <summary>
	/// <c>FUN_004b79b0( guest )</c>: the window built on them where none is up, then the kids' effect <c>0x81</c> and the
	/// advisor's message <c>0x12e</c>. With one already up the base builds nothing and the window stays on its guest.
	/// </summary>
	internal static void Open( WindowStack stack, int thingId )
	{
		if ( ParkPeople.Current?.Guests.ContainsKey( thingId ) != true )
			return;

		if ( !stack.Windows.Any( window => window is ParkVisitorWindow ) )
			stack.Open( new ParkVisitorWindow( stack, thingId ) );

		ParkAudio.Current?.VisitorWindowOpened();

		if ( Level.Current?.ParkState is { AdvisorMessages: { } lines } park )
			lines.Post( ParkAdvisorMessages.VisitorWindow, park.GameTick );

		if ( stack.Windows.LastOrDefault( window => window is ParkVisitorWindow ) is ParkVisitorWindow up )
			up._told ??= Level.Current?.ParkState;
	}

	private static ParkPeople? People => ParkPeople.Current;

	/// <summary>The guest shown, or null where the park has none of that id.</summary>
	private Peep? Guest => People is { } people && people.Guests.TryGetValue( ThingId, out var guest ) ? guest : null;

	/// <summary>
	/// The show, vtable <c>+4</c> (<c>0x004b7660</c>): the preview, the figures, and the title, "Visitor #" and the
	/// number (UITEXT 456) or "New Visitor" (457) at nought.
	/// </summary>
	private void Show()
	{
		if ( Guest is not { } guest )
		{
			Gone();
			return;
		}

		Unimplemented.Report( "VISITOR_WINDOW_PREVIEW" );

		Refresh();

		TitleNumber = guest.VisitorNumber;
		_title.Text = TitleOf( TitleNumber );

		Log.Info( $"Visitor window: showing thing {ThingId} \"{_title.Text}\"" );
	}

	/// <summary>The title of a visitor number (<c>0x004b7660</c>): UITEXT 456 with it as parameter 24, or 457 at nought.</summary>
	internal static string TitleOf( int number )
		=> number == 0 ? Localization.Text( 457 ) : Localization.Format( 456, (24, $"{number}") );

	/// <summary>
	/// The refresh, vtable <c>+0x14</c> (<c>FUN_004b72a0</c>): a title still "New Visitor" given the number once there is
	/// one, then the eight figures and the thought.
	/// </summary>
	private void Refresh()
	{
		if ( Guest is not { } guest )
		{
			Gone();
			return;
		}

		if ( TitleNumber == 0 && guest.VisitorNumber != 0 )
		{
			TitleNumber = guest.VisitorNumber;
			_title.Text = TitleOf( TitleNumber );
		}

		var tick = People!.State.GameTick;
		var figures = FiguresOf( guest, tick, NameOfThing );

		_figures[0x3724].Text = figures.Cash;
		_figures[0x372d].Text = figures.Time;
		_figures[0x372e].Text = $"{guest.NumRides}";
		_figures[0x372f].Text = $"{guest.NumShops}";
		_figures[0x3730].Text = $"{guest.NumSideshows}";
		_figures[0x3731].Text = $"{guest.NumSideshowsWon}";
		_figures[0x3733].Text = figures.Status;

		_happiness.Known = true;
		_happiness.Units = figures.Bar;

		_thoughtIcon.Frame = ThoughtPart( guest.Thoughts.Last );
		_thought.Text = ThoughtText( guest.Thoughts.Last );

		Log.Info( $"Visitor window: thing {ThingId} cash \"{figures.Cash}\" time \"{figures.Time}\" rides {guest.NumRides}"
			+ $" purchases {guest.NumShops} sideshows {guest.NumSideshows} won {guest.NumSideshowsWon} bar {figures.Bar}"
			+ $" status \"{figures.Status}\" thought {guest.Thoughts.Last} part {_thoughtIcon.Frame} \"{_thought.Text}\"" );
	}

	/// <summary>
	/// The figures that are words: the cash as a money cell letters it, the time in park of four times the stay
	/// (<c>FUN_004fd950</c> shifted left by two), the happiness's bar, and the status.
	/// </summary>
	internal static (string Cash, string Time, int Bar, string Status) FiguresOf( Peep guest, int gameTick, Func<int, string> nameOf )
		=> (ParkItemsScreen.Money( guest.Cash ),
			ParkVisitorsScreen.TimeText( ParkVisitorsScreen.HoursIn( guest.StayAt( gameTick ) << 2 ) ),
			ParkItemsScreen.Bar( (int)guest.Happiness ),
			StatusOf( (int)guest.State, guest.MajorDest, nameOf ));

	/// <summary>
	/// The status (<c>0x004b73a8</c>): row <paramref name="state"/> of <c>KIDSTATES.str</c>, and at row 16 its parameter 0
	/// the name of the thing the guest is using, <c>+0x1dc</c>. A row past the table's end letters nothing.
	/// </summary>
	internal static string StatusOf( int state, int using_, Func<int, string> nameOf )
	{
		_statusWords ??= new StringFile( StatusTable );

		if ( state < 0 || state >= _statusWords.Entries.Length )
			return string.Empty;

		return state == UsingRow
			? Localization.Format( _statusWords, state, (0, nameOf( using_ )) )
			: _statusWords[state];
	}

	/// <summary>The thought icon's part (<c>0x004b7477</c>): the thought less one, or <see cref="NoThought"/> with none.</summary>
	internal static int ThoughtPart( int thought ) => thought == 0 ? NoThought : thought - 1;

	/// <summary>The thought's words: row <paramref name="thought"/> of <c>THOUGHTS.str</c>, nothing for a row past its end.</summary>
	internal static string ThoughtText( int thought )
	{
		_thoughtWords ??= new StringFile( ThoughtTable );

		return thought >= 0 && thought < _thoughtWords.Entries.Length ? _thoughtWords[thought] : string.Empty;
	}

	/// <summary>
	/// A placed thing's name as <c>FUN_004e2780</c> joins its two lines: the second alone where the first is empty, the
	/// first without its last character and the second straight after where the first ends in a hyphen, the first
	/// alone where the second is empty, else the two with a space between. The lines are the item's rows of
	/// <c>OBJECT_NAMES.str</c>, which are the thing's own until a thing can be renamed (Q28).
	/// </summary>
	internal static string JoinName( string first, string second )
		=> first.Length == 0 ? second
			: first[^1] == '-' ? first[..^1] + second
			: second.Length == 0 ? first
			: $"{first} {second}";

	/// <summary>
	/// A placed thing's name (<c>FUN_004e2780</c>): its item's two lines joined (<see cref="JoinName"/>), the catalogue's
	/// name where the table has no row for it, and nothing for a thing not placed.
	/// </summary>
	internal static string NameOfThing( int thingId )
	{
		if ( Level.Current is not { ParkState: { } state, Catalogue: { } catalogue }
			|| !state.TryObject( thingId, out var placed ) )
			return string.Empty;

		if ( ParkObjectNames.Lines( placed.CatalogueId, ParkObjectNames.Table ) is { } lines )
			return JoinName( lines.A, lines.B );

		return catalogue.TryGet( placed.CatalogueId, out var item ) ? item.Name : string.Empty;
	}

	/// <summary>
	/// An arrow (<c>FUN_0048cbe0</c>, <c>FUN_0048caf0</c> on mask <c>0x40</c>): b_arup the next guest towards the newest
	/// (<c>FUN_00482da0</c>, round from the oldest), b_ardown the next towards the oldest (<c>FUN_004824d0</c>, round
	/// from the newest), in the used-thing list's order, which <see cref="ParkPeople.Peeps"/> keeps. The guest shown
	/// again shows nothing new.
	/// </summary>
	private void Cycle( bool newer )
	{
		if ( People is not { } people )
			return;

		var next = Next( [.. people.Peeps.Select( peep => peep.ThingId )], ThingId, newer );

		if ( next == 0 || next == ThingId )
			return;

		ThingId = next;
		Show();
	}

	/// <summary>
	/// The guest after <paramref name="current"/> in <paramref name="newestFirst"/>: towards its head with
	/// <paramref name="newer"/>, round to its tail, else towards its tail, round to its head; nought for none. A guest no
	/// longer in the list starts from the end the walk begins at.
	/// </summary>
	internal static int Next( IReadOnlyList<int> newestFirst, int current, bool newer )
	{
		if ( newestFirst.Count == 0 )
			return 0;

		var at = -1;

		for ( var i = 0; i < newestFirst.Count; ++i )
		{
			if ( newestFirst[i] == current )
				at = i;
		}

		if ( newer )
			return at > 0 ? newestFirst[at - 1] : newestFirst[^1];

		return at >= 0 && at + 1 < newestFirst.Count ? newestFirst[at + 1] : newestFirst[0];
	}

	/// <summary>The preview's click (<c>0x0048d1a0</c>): the camera to the guest, then the window closed.</summary>
	private void GoToGuest()
	{
		ParkOrbitCameraMode.GoToThing( ThingId );
		Stack.Close( this );
	}

	/// <summary>b_locate (<c>FUN_004b3f60( guest )</c>): the visitors' locator out on the arm, on this guest.</summary>
	private void Locate()
	{
		if ( Guest is not { } guest || Stack.Windows.OfType<ParkGadget>().FirstOrDefault() is not { } gadget )
			return;

		gadget.Locator.OpenOnVisitor( guest.ThingId, gadget.CarryLocatorPanel, () => Stack.CloseParkScreen() );
	}

	/// <summary>
	/// The guest is no longer in the park. The original closes the window when the guest it shows is deleted: the
	/// delete relay <c>FUN_004818c0</c> (guests only) calls <c>FUN_004b7a00</c>, then <c>FUN_0048ccd0</c>, vtable
	/// <c>+0x2c</c> (docs/exe/hud.md, "A guest arriving or going"). Closed here the same way; still counted until Q31b
	/// retires the count.
	/// </summary>
	private void Gone()
	{
		Unimplemented.Report( "VISITOR_WINDOW_GUEST_GONE" );
		Log.Info( $"Visitor window: thing {ThingId} is not in the park, so the window closes" );
		Stack.Close( this );
	}

	protected internal override void Update()
	{
		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 )
			return;

		Refresh();
	}

	/// <summary>The destroy (<c>0x14</c>): the advisor's message <c>0x12e</c> withdrawn, which cuts him off where he says it.</summary>
	protected internal override void Closed()
		=> _told?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.VisitorWindow, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

	/// <summary>The window, for the debug console's <c>visitorwindow</c>.</summary>
	internal string Census()
		=> $"visitor window: thing {ThingId} title \"{_title.Text}\" "
			+ string.Join( " ", Rows.Where( row => _figures.ContainsKey( row.Figure ) ).Select( row => $"{row.Figure:x}=\"{_figures[row.Figure].Text}\"" ) )
			+ $" bar {_happiness.Units} thought {_thoughtIcon.Frame} \"{_thought.Text}\"";
}
