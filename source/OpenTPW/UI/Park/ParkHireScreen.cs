namespace OpenTPW.UI;

/// <summary>
/// The hire screen - the other half of the gadget's <c>b_buy</c> button, and the sibling of
/// <see cref="ParkBuyScreen"/> rather than a second button of its own.
///
/// <para>
/// <b>Where the layout comes from.</b> The original's compiled layout stream at <c>0x00751fa8</c>,
/// 31 controls. See <c>docs/exe/hud.md</c>, where both screens' trees are written out.
/// </para>
///
/// <para>
/// <b>The two screens reach each other directly.</b> The category's opener shows whichever of them it
/// was last left on; each also carries a button to the other - this one's <c>0x2495</c>, and the buy
/// screen's <c>0x1ff</c>.
/// </para>
///
/// <para>
/// <b>Its list has two columns, not three.</b> A row's payload is sized by the column count, so where
/// the buy screen pushes name, price and a tick-box, this pushes a name and a wage. That is why
/// <see cref="UiList.Row"/> carries a value per column rather than a fixed record.
/// </para>
///
/// <para>
/// <b>The mini-balance is last month's account, not the park's cash.</b> The block (<c>0x2485</c>, help row 163,
/// "indicates the financial effects of hiring the selected person") reads the park analyser's newest closed month of
/// cash in, staff costs and all costs as the screen opens (<c>FUN_0049bdd0</c>): cash in, staff costs, all costs less
/// staff costs, and cash in less all costs. A pick (<c>0x401</c>) adds the candidate's wage to the staff costs and
/// takes it off the balance; nothing else moves them while the screen is up.
/// </para>
/// </summary>
internal sealed class ParkHireScreen : UiWindow
{
	/// <summary>
	/// The five tabs, in tab-index order, with the control id, help row and mesh the stream gives each.
	/// <b>The ids are not sequential</b> - that is the stream's own doing, as on the buy screen.
	/// </summary>
	private static readonly (int Kind, int Id, int Help, string Mesh, UIStrings Title, UiRect Rect)[] Tabs =
	[
		(0, 0x2490, 154, "b_shandy",    UIStrings.HireCleaners,     new UiRect( 1164, 195, 1267, 297 )),
		(1, 0x2493, 155, "b_smech",     UIStrings.HireMechanics,    new UiRect( 1283, 195, 1386, 297 )),
		(2, 0x2492, 156, "b_senter",    UIStrings.HireEntertainers, new UiRect( 1402, 195, 1504, 297 )),
		(3, 0x2494, 157, "b_sguard",    UIStrings.HireGuards,       new UiRect( 1521, 195, 1623, 297 )),

		// b_sresrhcer - the MODEL FILE is misspelled in ui.wad where its own textures are not.
		(4, 0x2491, 158, "b_sresrhcer", UIStrings.HireScientists,   new UiRect( 1640, 195, 1742, 297 )),
	];

	private readonly UiList _list;
	private readonly UiControl _title;
	private readonly UiControl _money;
	private readonly UiControl _staffCosts;

	/// <summary>The account's three months as the screen opened: <c>DAT_007ca2f0</c>, <c>DAT_007ca2fc</c>, <c>DAT_007ca2f8</c>.</summary>
	private readonly int _cashIn, _monthStaffCosts, _monthCosts;

	private readonly UiControl _cashInFigure, _otherCosts, _balanceFigure;

	/// <summary>The picked candidate's skill, <c>0x2482</c>, set only as a row is picked (<c>0x401</c>).</summary>
	private readonly ParkObjectWindow.UiStatBar _skill;

	/// <summary>The skill bar's inset from its rectangle's top and bottom (<c>FUN_00485e30( bar, 0, 10 )</c>).</summary>
	private const int BarInset = 10;

	/// <summary>A grade as the skill bar is handed it, <c>((grade &amp; 0xff) &lt;&lt; 10) / 5</c> of 1024 (<c>0x0049b8dc</c>).</summary>
	internal static int SkillUnits( int grade ) => ((grade & 0xff) << 10) / 5;

	private readonly UiRadioGroup _tabs;

	private int _kind;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastKind;

	/// <summary>
	/// The list's sort, kept for the session: the column + 1, negated when descending - the original's
	/// <c>[0x007523e4]</c>, which starts at 1, the name ascending.
	/// </summary>
	private static int _sort = 1;

	/// <summary>The headings' font slot and colour, as the opener hands them to <c>FUN_00486660</c> (<c>[0x007523e0]</c>).</summary>
	private const int HeadingFont = 8;

	/// <summary>The rows' font slot, the row factory's and the styler's (<c>[0x007523dc]</c>).</summary>
	private const int RowFont = 7;

	private static readonly UiColour HeadingColour = new( 255, 239, 0 );

	/// <summary>The tab the screen shows, 0 the cleaners' to 4 the scientists' ([0x007ca30c]).</summary>
	internal int Tab => _kind;

	/// <param name="park">The running park whose advisor hears of this screen, null for the level's.</param>
	/// <param name="openOn">The tab to open on (FUN_0049bdd0's argument), null for the one last left on (its -1).</param>
	public ParkHireScreen( WindowStack stack, ParkState? park = null, int? openOn = null ) : base( stack )
	{
		_parkGiven = park;

		// Not modal and not pausing - the same reading as the buy screen: nothing in either builder asks
		// for a pause, unlike the map screen, which plainly does. Built onto the park's own layer (0x0049bf34).
		ParkScreen = true;

		// w_big, the node "window4" - the same frame the buy screen and the three Information lists
		// wear. See docs/exe/hud.md.
		Root = new UiControl
		{
			Id = 0x247f,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" ),
			// Lettered larger than the original's where each box holds it: a deviation, UiFonts.ParkScreenLettering.
			TextSize = UiFonts.ParkScreenLettering
		};

		// Lettered by UI_SetTitle (0x00485d20): font 5 on the purple skin in (234, 239, 102), centred in the stream's
		// rect (805 to 1277) widened by half its width either side.
		_title = Root.Add( new UiControl
		{
			Id = 0x2480,
			Rect = new UiRect( 569, 74, 1513, 153 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true
		} );

		// The purple skin, font 5 in white, from the right (PurpleSkin_Construct, FUN_0049bdd0).
		_money = Root.Add( new UiControl
		{
			Id = 0x2496,
			Rect = new UiRect( 1297, 74, 1769, 153 ),
			Font = 5,
			TextColour = UiColour.White,
			TextShadow = true,
			TextAcross = TextAlign.End
		} );

		// The portrait. The original renders the chosen candidate's own actor into it, rebuilt only
		// when the (kind, costume) pair changes - and it uses the SPRITE BANK numbering, not the thing
		// model's. Nothing is drawn into it here; the frame is the screen's own art.
		Root.Add( new UiControl
		{
			Id = 0x2484,
			Rect = new UiRect( 484, 179, 748, 443 ),
			Mesh = UiMesh.Get( "f_staffpic" )
		} );

		// Who is selected, and their skill. The bar 0x2482 is built at nought (0x0049c335) and set only as a
		// row is picked (message 0x401, 0x0049b8f8), to (grade << 10) / 5 - so even a top-grade candidate fills
		// only four fifths of it. That is the original's arithmetic, not a bug. Its skin stands in 10 from the
		// stream's (722, 536)-(957, 575) top and bottom (FUN_00485e30( bar, 0, 10 ), 0x0049c2ef).
		var info = Root.Add( new UiControl
		{
			Id = 0x2481,
			Rect = new UiRect( 259, 521, 971, 600 ),
			Mesh = UiMesh.Get( "f_staffinfo" )
		} );

		_skill = info.Add( new ParkObjectWindow.UiStatBar
		{
			Id = 0x2482,
			Rect = new UiRect( 722, 536 + BarInset, 957, 575 - BarInset ),
			Known = true
		} );

		// UITEXT 146, "Skill", put on the label once as it is built (FUN_00485b00( 0x92 ), 0x0049c2c8); nothing
		// writes it again.
		info.Add( new UiControl
		{
			Id = 0x2483,
			Rect = new UiRect( 283, 536, 679, 575 ),
			Font = 6,
			TextColour = UiColour.White,
			TextAcross = TextAlign.Start,
			Text = Localization.Text( 146 )
		} );

		var balancePanel = Root.Add( new UiControl
		{
			Id = 0x2485,
			Rect = new UiRect( 259, 702, 971, 930 ),
			HelpText = 163,
			Mesh = UiMesh.Get( "f_balance" )
		} );

		// UITEXT 357/358/359/361 - "Cash in", "- Staff costs", "- Other costs", "Balance". The two COST
		// rows are red and the other two white: FUN_0049bdd0 calls the colour setter with (0xff,0,0,0xff)
		// while each cost row is the current control, and never for the cash-in or balance rows.
		balancePanel.Add( Label( 0x2488, new UiRect( 283, 714, 679, 759 ), UIStrings.CashIn ) );
		balancePanel.Add( Label( 0x248d, new UiRect( 283, 765, 679, 810 ), UIStrings.StaffCosts, Cost ) );
		balancePanel.Add( Label( 0x248a, new UiRect( 283, 816, 679, 861 ), UIStrings.OtherCosts, Cost ) );
		balancePanel.Add( Label( 0x2486, new UiRect( 283, 868, 679, 912 ), UIStrings.Balance ) );

		// The account's figures, the analyser's newest month as the screen opens (FUN_0049bdd0) - see the class remarks.
		_cashInFigure = balancePanel.Add( Figure( 0x2489, new UiRect( 722, 714, 957, 759 ) ) );
		_staffCosts = balancePanel.Add( Figure( 0x248c, new UiRect( 722, 765, 957, 810 ), Cost ) );
		_otherCosts = balancePanel.Add( Figure( 0x248b, new UiRect( 722, 816, 957, 861 ), Cost ) );
		_balanceFigure = balancePanel.Add( Figure( 0x2487, new UiRect( 722, 868, 957, 912 ) ) );

		if ( Level.Current?.ParkState is { } state )
		{
			_cashIn = state.MonthCashIn.Newest;
			_monthStaffCosts = state.MonthStaffCosts.Newest;
			_monthCosts = state.MonthCosts.Newest;
		}

		ShowAccount( 0 );
		Log.Info( $"Hire screen: opened, {Account()}" );

		_list = Root.Add( new UiList
		{
			Id = 0x248e,
			Rect = new UiRect( 1017, 179, 1813, 930 ),
			Mesh = UiMesh.Get( "list_hirestaff" ),
			RowArea = new UiRect( 1043, 441, 1729, 911 ),

			// TWO columns - name and wage. See the class remarks. The name is text and the wage a number (0x0049c0cf).
			Columns = [(1043, 1456), (1477, 1724)],
			TextColumns = [true, false],

			// The row factory FUN_0049bbb0 measures its rows from font 7 ([0x007523dc], kept at [0x007ca314]), and the
			// styler FUN_0049c880 letters each cell in it. It leaves the name's label at its default, from the left, and sets
			// the wage from the right (FUN_0065c428( 2, 1 ), 0x0049c94d).
			RowFont = RowFont,
			ColumnAligns = [TextAlign.Start, TextAlign.End],
			RowHeight = UiList.RowHeightOf( RowFont ),
			Activated = Chose,
			SelectionChanged = Selected,

			// Flag 0x80 in the stream (0x291): the row under the pointer is the selection, which is what shows a
			// candidate's grade and skill bar before the click that chooses them.
			SelectsUnderPointer = true,

			// The add's pick of the first row, as the list fills on opening and on a tab; taken in Update.
			PicksFirstRow = true,

			// The screen's handler keeps the list's 0x406 (0x0049b85e).
			SortChanged = word => _sort = word
		} );

		// Handed over while the list is still empty, so it sorts nothing (0x0049c0f0).
		_list.SetSort( _sort );
		Log.Info( $"Hire list: {_list.RowsCensus()}, aligned {string.Join( ' ', _list.ColumnAligns )}" );

		// UITEXT 144 and 145, "Candidate Name" and "Monthly Wage"; the rects are the stream's.
		_list.AddHeading( 0, new UiRect( 1039, 331, 1460, 436 ), Localization.Text( 144 ), "!cbut", HeadingFont, HeadingColour, 159 )
			.Clicked += LogOrder;
		_list.AddHeading( 1, new UiRect( 1471, 331, 1731, 436 ), Localization.Text( 145 ), "!cbut", HeadingFont, HeadingColour, 160 )
			.Clicked += LogOrder;

		_tabs = _list.Add( new UiRadioGroup
		{
			Id = 0x248f,
			Rect = new UiRect( 1161, 192, 1770, 300 )
		} );

		foreach ( var tab in Tabs )
		{
			_tabs.AddOption( new UiButton
			{
				Id = tab.Id,
				Rect = tab.Rect,
				HelpText = tab.Help,
				Mesh = UiMesh.Get( tab.Mesh ),
				Toggles = true
			} );
		}

		// The group's own handler is what lifts the other members - never assign over it. See
		// ParkBuyScreen for what assigning over it does.
		_tabs.SelectionChanged = () =>
		{
			foreach ( var tab in Tabs )
			{
				if ( tab.Id == _tabs.Selected )
					Show( tab.Kind );
			}
		};

		Root.Add( new UiButton
		{
			Id = 0x2495,
			Rect = new UiRect( 1866, 583, 1968, 686 ),
			HelpText = 162,
			Mesh = UiMesh.Get( "b_allthings" ),
			Clicked = () =>
			{
				Stack.Close( this );
				Stack.Open( new ParkBuyScreen( Stack ) );
			}
		} );

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 2,
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Show( openOn ?? _lastKind );

		// Reached every time this screen opens, and not answerable yet: the portrait needs the candidate's own actor
		// rendered into its frame. Counted rather than left quietly blank.
		Unimplemented.Report( "STAFF_PORTRAIT_ACTOR" );
	}

	/// <summary>What the original letters the two outgoing rows in - <c>FUN_0065c5d5( 0xff, 0, 0, 0xff )</c>.</summary>
	private static readonly UiColour Cost = new( 255, 0, 0 );

	private static UiControl Label( int id, UiRect rect, UIStrings text, UiColour? colour = null )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = 6,
			TextColour = colour ?? UiColour.White,
			TextAcross = TextAlign.Start,
			Text = Localization.Get( text )
		};

	private static UiControl Figure( int id, UiRect rect, UiColour? colour = null )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = 6,
			TextColour = colour ?? UiColour.White,
			TextAcross = TextAlign.End
		};

	/// <summary>
	/// Fills the list with one kind's candidates - the original's <c>FUN_0049b5b0</c>, whose filter is
	/// the candidate record's own first field against the tab being shown. Each goes in by the list's sort, so the
	/// order they are handed over in shows only among equal rows.
	/// </summary>
	private void Show( int kind )
	{
		_kind = Math.Clamp( kind, 0, Tabs.Length - 1 );
		_lastKind = _kind;

		var tab = Tabs[_kind];

		_title.Text = Localization.Get( tab.Title );
		_tabs.Put( tab.Id );

		_list.Clear();

		var added = new List<int>();

		if ( Level.Current?.StaffPool is { } pool )
		{
			// The first goes into the empty list picked, its 0x401 posted for Update to take.
			foreach ( var person in pool.OfKind( _kind ) )
			{
				_list.Add( new UiList.Row( person.Id, person.Name, person.Wage ) );
				added.Add( person.Id );
			}
		}

		LogOrder();
		Log.Info( $"Hire list: added {string.Join( ",", added )}, row {_list.Selected} picked" );
	}

	private void LogOrder() => Log.Info( $"Hire list: kind {_kind}, {_list.Census()}" );

	private readonly ParkState? _parkGiven;

	/// <summary>The running park whose advisor hears of this screen: the one it was made with, or the level's.</summary>
	private ParkState? Park => _parkGiven ?? Level.Current?.ParkState;

	// The park whose advisor this screen has asked for its line, none before it is first shown.
	private ParkState? _asked;

	/// <summary>
	/// The opener's last act on a screen it has built (FUN_0049bdd0, 0x0049c800): the advisor is posted message
	/// 0xbd (FUN_00486b00), the screen's own line; a tab picked on the open screen asks for nothing more. And the
	/// list follows the pool while the screen is open: a candidate who joins it gets a row and one whose time runs
	/// out loses theirs, as the original's pool tells the hire list (<c>FUN_00481550</c>). Before any of it the
	/// opener leaves the gadget's category on hire (0x0049bdf5).
	/// </summary>
	protected internal override void Shown()
	{
		ParkCategoryScreens.Remember( ParkCategoryScreens.Building, ParkCategoryScreens.Hire );

		if ( _asked == null && Park is { AdvisorMessages: { } lines } park )
		{
			_asked = park;
			lines.Post( ParkAdvisorMessages.HireScreen, park.GameTick );
		}

		_following = Level.Current?.StaffPool;

		if ( _following == null )
			return;

		_following.Joined += Joined;
		_following.Left += Left;
	}

	/// <summary>
	/// The screen's destroy (FUN_0049b650, event 0x14): the advisor's message 0xbd is withdrawn (FUN_00486b40),
	/// which cuts him off where he is saying it.
	/// </summary>
	protected internal override void Closed()
	{
		_asked?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.HireScreen, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

		if ( _following == null )
			return;

		_following.Joined -= Joined;
		_following.Left -= Left;
		_following = null;
	}

	/// <summary>The pool the list is following, which is let go of as it was taken hold of, whatever the level is by then.</summary>
	private ParkStaffPool? _following;

	private void Joined( ParkStaffPool.Candidate person )
	{
		if ( person.Kind == _kind )
			_list.Add( new UiList.Row( person.Id, person.Name, person.Wage ) );
	}

	private void Left( int candidateId ) => _list.Remove( candidateId );

	/// <summary>
	/// A candidate was highlighted, by the pointer or by the fill's pick of the first row. The original previews the
	/// cost here - message <c>0x401</c> adds this wage to the staff-costs row and takes it off the balance, so the
	/// player sees what hiring would do before they do it (<see cref="ShowAccount"/>) - and sets the skill bar. Both
	/// stand until the next pick.
	/// </summary>
	private void Selected( int candidateId )
	{
		if ( Level.Current?.StaffPool is not { } pool )
			return;

		foreach ( var person in pool.Candidates )
		{
			if ( person.Id != candidateId )
				continue;

			_skill.Units = SkillUnits( person.Grade );
			ShowAccount( person.Wage );
			Log.Info( $"Hire screen: candidate {person.Id} grade {person.Grade}, skill bar {SkillUnits( person.Grade )} of 1024, {Account()}" );

			return;
		}
	}

	/// <summary>
	/// A candidate was chosen. The original marks them taken and carries them on the cursor in its
	/// place-staff mode; they stay in the pool, and on this list, until a click at a cell is accepted
	/// (docs/exe/park-engine.md, "Putting a candidate down: the type-5 mode").
	/// </summary>
	/// <remarks>
	/// <b>They are not hired here.</b> The candidate goes onto the cursor and the worker is built by
	/// the next click at a cell, in <c>Level.ClickWorldAt</c> - which is the original's order, and the
	/// reason a cancelled hire costs nothing and takes nobody out of the pool. <c>hire</c> from the
	/// console does the whole thing in one step.
	/// </remarks>
	private void Chose( int candidateId )
	{
		Log.Info( $"Hire screen: {ParkStaffPool.Carry( candidateId )}" );

		// Out of the way, so the park underneath can be clicked.
		Stack.Close( this );
	}

	/// <summary>
	/// The account with <paramref name="wage"/> hired: the opening's fill (<c>FUN_0049bdd0</c>) with nought, and a
	/// pick's (<c>0x401</c>, <c>FUN_0049b650</c>) with the candidate's wage, which goes onto the staff costs and comes
	/// off the balance. Each figure in the money skin (<c>FUN_00490250</c>): "$ 1500", "-$ 11675".
	/// </summary>
	private void ShowAccount( int wage )
	{
		var other = unchecked(_monthCosts - _monthStaffCosts);

		_cashInFigure.Text = ParkItemsScreen.Money( _cashIn );
		_staffCosts.Text = ParkItemsScreen.Money( unchecked(_monthStaffCosts + wage) );
		_otherCosts.Text = ParkItemsScreen.Money( other );
		_balanceFigure.Text = ParkItemsScreen.Money( unchecked(_cashIn - wage - _monthStaffCosts - other) );
	}

	/// <summary>The account as it reads, for the log: cash in, staff costs, other costs, balance.</summary>
	internal string Account()
		=> $"cash in {_cashInFigure.Text}, staff costs {_staffCosts.Text}, other costs {_otherCosts.Text}, balance {_balanceFigure.Text}";

	/// <summary>
	/// Takes the list's posted pick, as the original's loop hands the screen its queued <c>0x401</c>, then the park's cash,
	/// lettered as the buy screen's (UITEXT 0x1ca or 0x1cb, <c>FUN_0049bdd0</c>).
	/// </summary>
	protected internal override void Update()
	{
		_list.TakePosted();

		if ( Park is { } state )
			_money.Text = ParkBuyScreen.MoneyText( state.Balance );
	}
}
