namespace OpenTPW.UI;

/// <summary>
/// Everyone the park employs, by kind - the original's <c>allstaff</c>, and the screen the gadget's
/// Information button is seeded to open on after park status.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_00496620</c> builds it from the compiled stream at
/// <c>0x00750e10</c>, handler <c>FUN_00495da0</c>, walked here opcode by opcode. Every rect, id and
/// mesh is that stream's; the five column headings are UITEXT <b>101-105</b>, which the builder hands
/// to the header children by id <c>0x10 + index</c>. See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>Its five tabs are the same five kinds the hire screen hires</b>, in the same order and with the
/// same artwork - cleaners, mechanics, entertainers, guards, scientists. The builder's own switch maps
/// kind to tab id (<c>0x323</c>, <c>0x326</c>, <c>0x325</c>, <c>0x327</c>, <c>0x324</c>), which is why
/// the ids below look shuffled and are not.
/// </para>
///
/// <para>
/// <b>The two meters are stat bars in <c>happygrad.wct</c>, not the gauge's skin.</b> <c>FUN_00496620</c> hands
/// both the stat bars' skin (<c>FUN_00477ff0</c>) with that texture, each inset 8 from its top and bottom
/// (<c>FUN_00485e30( meter, 0, 8 )</c>) - <c>0x32c</c> the mean over all staff, <c>0x32d</c> the mean over the
/// chosen kind - and scales each by <c>(v &lt;&lt; 10) / 100</c>, so the bar's full scale is
/// <b>1024</b> against a percentage.
/// </para>
///
/// <para>
/// <b>The name is the member's own</b> (<see cref="Staff.Name"/>): the save's <c>mName</c>, or the candidate's
/// at a hire. One with none, which only a test makes, shows the kind and the thing id in the game's own words
/// for the kind (<c>STAFF_TYPES.str</c>). <b>The row's cells are the styler <c>FUN_00496d20</c>'s</b>: the name and
/// the status labels, the wage the park is paying in a money cell (<c>FUN_00490250</c>, "$ 81"), the skill a bar in
/// <c>ridestatbar.wct</c> and the happiness a bar in <c>happygrad.wct</c>, both inset 8 (<see cref="NumbersOf"/>).
/// </para>
/// </summary>
internal sealed class ParkStaffScreen : UiWindow
{
	/// <summary>The five tabs, by kind, with the id, help row and mesh the stream gives each.</summary>
	private static readonly (int Kind, int Id, int Help, string Mesh, UiRect Rect)[] Tabs =
	[
		(0, 0x323, 114, "b_shandy",    new UiRect( 1160, 191, 1262, 294 )),
		(1, 0x326, 115, "b_smech",     new UiRect( 1279, 191, 1381, 294 )),
		(2, 0x325, 116, "b_senter",    new UiRect( 1398, 191, 1500, 294 )),
		(3, 0x327, 117, "b_sguard",    new UiRect( 1516, 191, 1619, 294 )),

		// b_sresrhcer, as on the hire screen - the MODEL FILE is misspelled in ui.wad, where its
		// textures (b_sresrcher.wct) are not, and it is the only scientist tab model the archive ships.
		(4, 0x324, 118, "b_sresrhcer", new UiRect( 1635, 191, 1738, 294 )),
	];

	/// <summary>The header rects, in column order, from the stream's <c>op 0xc</c> children.</summary>
	private static readonly UiRect[] Headings =
	[
		new( 294, 306, 716, 412 ), new( 722, 306, 1007, 412 ), new( 1013, 306, 1221, 412 ),
		new( 1226, 306, 1476, 411 ), new( 1483, 306, 1733, 411 )
	];

	/// <summary>
	/// The label under the per-kind meter, by kind. <b>NOT 107 to 111 in order.</b>
	/// </summary>
	/// <remarks>
	/// <c>FUN_00496620</c>'s switch is deliberately out of order - case 0 takes <c>0x6b</c>, case 1
	/// <c>0x6c</c>, case <b>2</b> takes <c>0x6e</c> and case <b>3</b> takes <c>0x6d</c>, case 4
	/// <c>0x6f</c> - because the string file lists guards before entertainers while the kinds run
	/// cleaners, mechanics, entertainers, guards, scientists. Written as a plain 107..111 run, the
	/// guards tab would show a list of guards under the heading "Entertainers' Happiness".
	/// </remarks>
	private static readonly int[] KindHappiness = [107, 108, 110, 109, 111];

	/// <summary>The original's own meter scale - see the class remarks.</summary>
	private const int MeterFull = 1024;

	/// <summary>How often the rows are rewritten, in milliseconds of real time: <c>FUN_0065ef90( 0x80083, 2000 )</c>.</summary>
	private const long RefreshEvery = 2000;

	/// <summary>The font the rows are lettered in, the screen's font slot 7.</summary>
	private const int RowFont = 7;

	/// <summary>The rewrite's timer, running from the screen's opening; a tab does not restart it.</summary>
	private readonly UiTimer _refresh = new( RefreshEvery );

	private readonly UiList _list;
	private readonly UiRadioGroup _tabs;
	private readonly UiControl _kindLabel;
	private readonly ParkObjectWindow.UiStatBar _kindHappiness;
	private readonly ParkObjectWindow.UiStatBar _allHappiness;

	private int _kind;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastKind;

	/// <summary>
	/// The list's sort, kept for the session and shared by the five tabs: the column + 1, negated when descending - the
	/// original's <c>[0x0075118c]</c>, which starts at 1, the name ascending, and which the handler's <c>0x406</c> writes
	/// (<c>0x00495fe0</c>).
	/// </summary>
	private static int _sort = 1;

	/// <param name="kind">
	/// The kind to open on (<c>FUN_00496620</c>'s first argument: a staff member's window's <c>b_allstaff</c>), or -1 for the
	/// one it was last left on.
	/// </param>
	/// <param name="pick">A member whose row is picked and brought into view as the list fills (its second), or nought.</param>
	public ParkStaffScreen( WindowStack stack, int kind = -1, int pick = 0 ) : base( stack )
	{
		if ( kind != -1 )
			_lastKind = kind;

		// Built onto the park's own layer (0x00496643) - see UiWindow.ParkScreen.
		ParkScreen = true;

		// w_big, the node "window4" inside w_big.MD2 - the frame five screens share. Without it this
		// screen is a list floating over the park. See docs/exe/hud.md.
		Root = new UiControl
		{
			Id = 0x320,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" )
		};

		// Lettered by UI_SetTitle on message 0x15 (0x00495f22): font 5 in (234, 239, 102), with its edge, centred in the
		// stream's rect (803 to 1276) widened by half its width either side.
		Root.Add( new UiControl
		{
			Id = 0x328,
			Rect = new UiRect( 567, 81, 1512, 161 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			Text = Localization.Get( UIStrings.AllStaff )
		} );

		_list = Root.Add( new UiList
		{
			Id = 0x321,
			Rect = new UiRect( 272, 181, 1817, 801 ),
			HelpText = 124,
			Mesh = UiMesh.Get( "list_allstaff" ),
			RowArea = new UiRect( 294, 418, 1734, 777 ),

			// The row factory at 0x00496c10 measures its rows from font 7 ([0x00751184], kept at [0x007ca170]), and the
			// styler FUN_00496d20 letters each cell in it.
			RowFont = RowFont,
			RowHeight = UiList.RowHeightOf( RowFont ),

			// A right click on the list moves the camera to the selected row's member of staff and closes the screen
			// (0x0049602f).
			RowRightClicked = GoTo,

			// A row's 0x400 opens the member's window (0x00496088, FUN_004b6080).
			Activated = member => ParkStaffWindow.Open( Stack, member ),

			// The screen's handler keeps the list's 0x406 (0x00495fe0).
			SortChanged = word => _sort = word,

			// FUN_006636b2 from the opener: the name and the status sorted as text, the wage, the skill and the happiness
			// as numbers.
			TextColumns = [true, true],

			// The add picks the top row as the first goes in (FUN_0066403b), so each fill highlights the row sorted first,
			// and a heading's re-sort keeps the index. The screen's handler FUN_00495da0 has no 0x401 arm (its default
			// FUN_00488ba0 hands it to FUN_0065f66e): nothing takes the pick, and it is the highlight alone.
			PicksFirstRow = true,

			// The tree's list record carries flag 0x80 (0x00750e5e): the row under the moving pointer is picked, the
			// highlight alone as the add's pick is.
			SelectsUnderPointer = true,

			// The stream's own op 0xb record. The styler FUN_00496d20 leaves the name's and the status's labels at their
			// default, from the left, and sets the wage from the right (FUN_0065c428( 2, 1 ), 0x00496e39); the bars take none.
			Columns = [(299, 711), (729, 999), (1017, 1217), (1231, 1471), (1488, 1728)],
			ColumnAligns = [TextAlign.Start, TextAlign.Start, TextAlign.End, TextAlign.End, TextAlign.End],

			// The styler's skill and happiness cells, each inset 8 (FUN_00496d20).
			BarColumns = [false, false, false, true, true],
			BarSkins = [null, null, null, null, ParkObjectWindow.UiStatBar.HappyGrad],
			BarInset = MeterInset
		} );

		// Handed over while the list is still empty, so it sorts nothing (0x004966fb).
		_list.SetSort( _sort );

		// Framed by FUN_00486660 with help rows 0x77 to 0x7b (0x00496713).
		for ( var column = 0; column < Headings.Length; ++column )
		{
			_list.AddFramedHeading( column, Headings[column], Localization.Text( 101 + column ), 0x77 + column ).Clicked
				+= LogOrder;
		}

		Log.Info( $"Staff list: headings {_list.HeadingsCensus()}" );
		Log.Info( $"Staff list: {_list.RowsCensus()}" );

		_tabs = _list.Add( new UiRadioGroup
		{
			Id = 0x322,
			Rect = new UiRect( 1157, 188, 1737, 297 )
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

		_tabs.SelectionChanged = () =>
		{
			foreach ( var tab in Tabs )
			{
				if ( tab.Id == _tabs.Selected )
					Show( tab.Kind );
			}
		};

		// The panel along the bottom: the chosen kind's mean happiness over one meter, and every
		// member of staff's over the other.
		var panel = Root.Add( new UiControl
		{
			Id = 0x329,
			Rect = new UiRect( 615, 815, 1589, 950 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		_kindLabel = panel.Add( Label( 0x32a, new UiRect( 654, 832, 1177, 877 ) ) );

		panel.Add( Label( 0x32b, new UiRect( 654, 883, 1177, 928 ) ) ).Text = Localization.Text( 106 );

		_kindHappiness = panel.Add( Meter( 0x32d, new UiRect( 1191, 832, 1567, 877 ) ) );
		_allHappiness = panel.Add( Meter( 0x32c, new UiRect( 1191, 883, 1567, 928 ) ) );

		Root.Add( CrossLink( 0x32f, 360, 125, "b_kids", 6 ) );
		Root.Add( CrossLink( 0x330, 467, 126, "b_allthings", 5 ) );
		Root.Add( CrossLink( 0x32e, 575, 127, "b_parkinfo", 3 ) );

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Show( _lastKind );

		// The opener's last act posts the advisor's message 0x11d (FUN_00496620), which is not built.
		Unimplemented.Report( "STAFF_LIST_ADVISOR_LINE" );

		if ( pick != 0 )
		{
			_list.Pick( pick );
			Log.Info( $"Staff list: member {pick} picked, selected {_list.Selected}, top row {_list.ScrollTop}" );
		}

		// Every member of staff's meter is set once, as the screen opens (FUN_00496620, FUN_004c7cc0): no tab or
		// timer moves it. A mean over nobody reads nought, as the gadget's gauge does for a park with no guests.
		if ( ParkPeople.Current is { } people )
			_allHappiness.Units = Scaled( people.Staff );
	}

	/// <summary>A meter: the stat bars' skin in <c>happygrad.wct</c>, inset 8 from the stream's rect (<c>FUN_00496620</c>).</summary>
	private static ParkObjectWindow.UiStatBar Meter( int id, UiRect rect )
		=> new()
		{
			Id = id,
			Rect = new UiRect( rect.Left, rect.Top + MeterInset, rect.Right, rect.Bottom - MeterInset ),
			Skin = ParkObjectWindow.UiStatBar.HappyGrad,
			Known = true
		};

	/// <summary>How far the meters and the rows' bars stand in from their top and bottom: <c>FUN_00485e30( cell, 0, 8 )</c>.</summary>
	private const int MeterInset = 8;

	private static UiControl Label( int id, UiRect rect )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = 6,
			TextColour = UiColour.White,
			TextAcross = TextAlign.Start
		};

	private UiButton CrossLink( int id, int top, int help, string mesh, int screen )
		=> new()
		{
			Id = id,
			Rect = new UiRect( 1866, top, 1968, top + 102 ),
			HelpText = help,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () =>
			{
				Stack.Close( this );
				ParkCategoryScreens.Show( Stack, ParkCategoryScreens.Information, screen );
			}
		};

	/// <summary>
	/// The list's right click (<c>0x402</c>, <c>0x00495feb</c>): the camera to the row's member of staff, then the screen
	/// closed (message 4, <c>0x00496044</c>).
	/// </summary>
	private void GoTo( int member )
	{
		ParkOrbitCameraMode.GoToThing( member );
		Stack.Close( this );
	}

	/// <summary>
	/// Fills the list with everyone of one kind (the opener's or a tab's <c>FUN_00481bc0</c> with the row adder
	/// <c>0x00496320</c>), and sets the kind's meter, as the opener and each tab do (<c>FUN_004964d0</c>). Each row goes
	/// through the list's sorted insert under the kept sort, <see cref="_sort"/>.
	/// </summary>
	private void Show( int kind )
	{
		_kind = Math.Clamp( kind, 0, Tabs.Length - 1 );
		_lastKind = _kind;

		_tabs.Put( Tabs[_kind].Id );
		_kindLabel.Text = Localization.Text( KindHappiness[_kind] );

		Log.Info( $"Staff list: kind {_kind}, filling" );

		_list.Clear();

		if ( ParkPeople.Current is not { } people )
			return;

		foreach ( var member in people.Staff )
		{
			if ( ParkStaffPool.KindFor( member.Model ) == _kind )
			{
				var row = RowOf( member );
				_list.Add( row );
				Log.Info( $"Staff list: kind {_kind}, row {Census( row )}" );
			}
		}

		_kindHappiness.Units = Scaled( people.Staff
			.Where( member => ParkStaffPool.KindFor( member.Model ) == _kind ) );

		LogOrder();
	}

	private void LogOrder() => Log.Info( $"Staff list: kind {_kind}, {_list.Census()}; picked {_list.Selected}" );

	/// <summary>The status words' table, the one the boot loads as <c>mUIStaffStates</c> (table <c>0x3ed</c>).</summary>
	internal const string StatusTable = "Language/English/STAFFSTATES.str";

	private static StringFile? _statusWords;

	/// <summary>
	/// A member's status as the row adder letters it: row <see cref="Staff.StatusRow"/> of <c>STAFFSTATES.str</c>, read
	/// from <c>[0x0078baa4]</c> by <c>FUN_006ace60</c>, which answers nothing for a row past the table's end.
	/// </summary>
	internal static string StatusOf( Staff member )
	{
		_statusWords ??= new StringFile( StatusTable );

		var row = member.StatusRow;

		return row < _statusWords.Entries.Length ? _statusWords[row] : string.Empty;
	}

	/// <summary>
	/// A member of staff's row: name, status, the wage the park pays as a money cell's text, and the skill's and the
	/// happiness's bars, with <see cref="NumbersOf"/> for the numbers.
	/// </summary>
	private UiList.Row RowOf( Staff member )
	{
		var name = member.Name.Length > 0 ? member.Name : $"{ParkStaffPool.NameOfKind( _kind )} {member.ThingId}";
		var numbers = NumbersOf( member, Level.Current?.StaffPool?.WageFor( _kind, member.PayGrade ) ?? 0 );

		return new UiList.Row( member.ThingId, name, 0, Values:
		[
			StatusOf( member ),
			ParkItemsScreen.Money( numbers[2] ),
			"",
			""
		], Numbers: numbers );
	}

	/// <summary>
	/// A member's row by column as the row adder (<c>0x00496320</c>) hands it: the name's and the status's nought, the
	/// wage (<c>FUN_00506870</c>), the skill's bar <c>(skill &lt;&lt; 10) / 100</c> (<see cref="SkillOf"/>) and the
	/// happiness's <c>((happiness &amp; 0xff) &lt;&lt; 10) / 100</c>, the float <c>+0x1f8</c> truncated.
	/// </summary>
	internal static int[] NumbersOf( Staff member, int wage )
		=> [0, 0, wage, (SkillOf( member ) << 10) / 100, ParkItemsScreen.Bar( (int)member.Happiness )];

	/// <summary>
	/// A member's skill, 0 to 100 through the grades and past it in a grade-4 member's training (<c>FUN_00506550</c>):
	/// <c>__ftol( (grade - percentage x -0.01) x 20 )</c>, which is this whole division for every grade and every
	/// byte of <c>mPercentageThroughGrade</c> at every precision the FPU may be set to.
	/// </summary>
	internal static int SkillOf( Staff member ) => (member.PayGrade * 100 + (member.PercentageThroughGrade & 0xff)) / 5;

	/// <summary>A row for the log: id, name, status, wage, skill bar, happiness bar.</summary>
	private static string Census( UiList.Row row )
		=> $"{row.Id} {row.Name}: \"{row.Values![0]}\" {row.Numbers![2]} {row.Numbers[3]} {row.Numbers[4]}";

	/// <summary>
	/// Every two seconds (timer <c>0x80083</c>, armed by the opener), each row's status, skill and happiness rewritten
	/// where it stands, from the last row to the first: <c>FUN_00495da0</c> hands <c>FUN_006644ea</c> the column mask
	/// <c>0x1a</c>, so the name and the wage stay as the fill wrote them. Nothing is added, removed or re-sorted, and the
	/// meters are not touched (<c>docs/exe/hud.md</c>, "How allstaff and allitems keep their rows current").
	/// </summary>
	protected internal override void Update()
	{
		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 || ParkPeople.Current is not { } people )
			return;

		var rewritten = 0;

		for ( var index = _list.Rows.Count - 1; index >= 0; --index )
		{
			var row = _list.Rows[index];

			if ( people.Staff.FirstOrDefault( member => member.ThingId == row.Id ) is not { } member
				|| row.Values is not { Count: 4 } old || row.Numbers is not { Count: 5 } numbers )
			{
				continue;
			}

			var now = RowOf( member );
			_list.Replace( index, row with
			{
				Values = [now.Values![0], old[1], now.Values[2], now.Values[3]],
				Numbers = [0, 0, numbers[2], now.Numbers![3], now.Numbers[4]]
			} );
			++rewritten;
		}

		Log.Info( $"Staff list: kind {_kind}, {rewritten} rows rewritten, meters {_kindHappiness.Units}/{_allHappiness.Units}"
			+ (_list.Rows.Count > 0 ? $", row 0 {Census( _list.Rows[0] )}" : "") );
	}

	/// <summary>A mean happiness on the original's own 0-1024 scale - <c>(v &lt;&lt; 10) / 100</c>.</summary>
	private static int Scaled( IEnumerable<Staff> staff )
	{
		var members = staff.ToList();

		return members.Count == 0
			? 0
			: (int)(members.Average( member => member.Happiness ) * MeterFull / 100);
	}
}
