namespace OpenTPW.UI;

/// <summary>
/// Everything standing in the park, in four tabs - the original's <c>allitems</c>, one of the four
/// screens behind the gadget's Information button.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_00495aa0</c> builds the frame from the stream at
/// <c>0x007508e0</c> and then hands the list to one of <b>four sub-builders</b>, one per tab. Every
/// rect, id and mesh below is from those streams, walked opcode by opcode. See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>The four tabs do not share a list shape, which is the whole reason this screen is awkward.</b>
/// Rides and shops load the five-column tree at <c>0x750ab8</c>, sideshows the <b>six</b>-column tree
/// at <c>0x750ba0</c>, and miscellaneous items the <b>two</b>-column tree at <c>0x750ca0</c>. So the
/// three trees are built here and the tab shows one of them, which is what the original does by
/// loading a different tree each time.
/// </para>
///
/// <para>
/// <b>The headings are in CODE, not in the stream.</b> Each sub-builder fetches the header child by id
/// <c>0x10 + index</c> and gives it a UITEXT row: rides <b>82-86</b>, shops <b>87-91</b>, sideshows
/// <b>92-97</b>, miscellaneous <b>98-99</b>. Walking the stream alone leaves five unnamed boxes.
/// </para>
///
/// <para>
/// <b>What this can actually answer, and what stays blank.</b> Every tab's second column is the customers of the
/// last thirty finished days, a number; the rides' Excitement, State Of Repair and Remaining Life and the
/// sideshows' Excitement are bars (<see cref="NumbersOf(int, ParkWorld.CatalogueObject, ParkItemCatalogue.Item, ParkState)"/>);
/// the shops' and sideshows' Profit Last Month and Total Profit are money, and their Customer Satisfaction a bar.
/// <b>Miscellaneous items are a row an object</b>, each <i>Number Owned</i> its item's count of what is standing.
/// </para>
/// </summary>
internal sealed class ParkItemsScreen : UiWindow
{
	/// <summary>How the four tabs are laid out, in the tab order <c>FUN_00495aa0</c>'s switch uses.</summary>
	private readonly record struct Tab( int Index, int Id, int Help, string Mesh, UIStrings Title,
		int FirstHeading, int Columns, UiRect Rect, bool[] Bars, int[] Helps );

	/// <remarks>
	/// <b>Each tab's rect is the stream's own, NOT a stride from the first one.</b> The ids are not in
	/// screen order - the builder's switch takes case 1 to <c>0x12c4bc</c> and case 2 to
	/// <c>0x12c4bb</c> - so laying them out as <c>1283 + index * 119</c> would put shops where
	/// sideshows belong and sideshows where shops do, at the same four x positions the stream holds.
	/// </remarks>
	private static readonly Tab[] Tabs =
	[
		// b_srides is asked for by its first NODE name, b_sride, which is not the file's - the same
		// divergence ParkFrontEnd.Meshes lists. The other three resolve to their file names.
		// The bars are the columns the styler FUN_00494690 skins with ridestatbar.wct, by tab: the rides' excitement,
		// state of repair and remaining life, the shops' satisfaction, the sideshows' excitement and satisfaction.
		// The help rows are each tab builder's, handed to FUN_00486660 with its heading: FUN_00494ec0, FUN_00494c70,
		// FUN_00494250 and FUN_00495110.
		new( 0, 0x12c4ba, 101, "b_srides",   UIStrings.AllRides,     82, 5, new UiRect( 1283, 195, 1386, 297 ),
			[false, false, true, true, true], [0x69, 0x6a, 0x6b, 0x6c, 0x6d] ),
		new( 1, 0x12c4bc, 102, "b_sshop",    UIStrings.AllShops,     87, 5, new UiRect( 1521, 195, 1623, 297 ),
			[false, false, false, false, true], [0x69, 0x248, 0x249, 0x24a, 0x24b] ),
		new( 2, 0x12c4bb, 103, "b_sshow",    UIStrings.AllSideshows, 92, 6, new UiRect( 1402, 195, 1504, 297 ),
			[false, false, true, false, false, true], [0x69, 0x248, 0x6b, 0x249, 0x24a, 0x24b] ),
		new( 3, 0x12c4bd, 104, "b_sfeature", UIStrings.AllMiscItems, 98, 2, new UiRect( 1640, 195, 1742, 297 ), [],
			[0x69, 0x24c] ),
	];

	/// <summary>The five-column tree at <c>0x750ab8</c> - rides and shops.</summary>
	private static readonly (int Left, int Right)[] Five =
		[(275, 654), (671, 849), (868, 1140), (1167, 1439), (1466, 1739)];

	private static readonly UiRect[] FiveHeadings =
	[
		new( 266, 318, 659, 423 ), new( 667, 318, 853, 423 ), new( 858, 317, 1149, 422 ),
		new( 1158, 317, 1449, 422 ), new( 1457, 317, 1748, 422 )
	];

	/// <summary>The six-column tree at <c>0x750ba0</c> - sideshows.</summary>
	private static readonly (int Left, int Right)[] Six =
		[(273, 597), (608, 786), (806, 1024), (1047, 1264), (1286, 1504), (1525, 1741)];

	private static readonly UiRect[] SixHeadings =
	[
		new( 261, 318, 597, 423 ), new( 604, 318, 790, 423 ), new( 798, 318, 1031, 423 ),
		new( 1039, 318, 1272, 423 ), new( 1279, 318, 1512, 423 ), new( 1516, 318, 1749, 423 )
	];

	/// <summary>The two-column tree at <c>0x750ca0</c> - miscellaneous items.</summary>
	private static readonly (int Left, int Right)[] Two = [(274, 1093), (1116, 1742)];

	private static readonly UiRect[] TwoHeadings =
		[new( 266, 318, 1100, 423 ), new( 1111, 318, 1748, 423 )];

	private readonly UiControl _title;
	private readonly UiRadioGroup _tabs;

	/// <summary>One list per shape, of which exactly one is shown - see the class remarks.</summary>
	private readonly Dictionary<int, UiList> _lists = [];

	private int _tab;

	/// <summary>How often a tab's rows are rewritten, in milliseconds of real time: <c>FUN_0065ef90( 0x80083, 2000 )</c>.</summary>
	private const long RefreshEvery = 2000;

	/// <summary>The font the rows are lettered in, the screen's font slot 7.</summary>
	private const int RowFont = 7;

	/// <summary>
	/// The rewrite's timer. Each tab loads its own list tree in the original, whose handler arms the timer as the tree
	/// is shown (message <c>0x15</c>, <c>FUN_004955e0</c>), so a tab starts it again.
	/// </summary>
	private UiTimer _refresh = new( RefreshEvery );

	/// <summary>The tab shown, and the thing whose row is picked on it (-1 for none).</summary>
	internal int ShownTab => _tab;

	internal int Picked => _lists[Tabs[_tab].Columns].Selected;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastTab;

	/// <summary>
	/// Each tab's sort, kept for the session: the column + 1, negated when descending - the original's
	/// <c>[0x00750d5c]</c>, <c>[0x00750d60]</c>, <c>[0x00750d64]</c> and <c>[0x00750d68]</c>, each 1, the name ascending,
	/// and each written by the screen's handler on the list's <c>0x406</c> by the remembered tab (<c>0x004954eb</c>).
	/// </summary>
	private static readonly int[] _sorts = [1, 1, 1, 1];

	/// <param name="openOn">The tab to open on, or -1 for the one last left (<c>FUN_00495aa0</c>'s first argument).</param>
	/// <param name="pick">A thing whose row is picked and brought into view, or nought (its second, <c>FUN_00664d2a</c>).</param>
	public ParkItemsScreen( WindowStack stack, int openOn = -1, int pick = 0 ) : base( stack )
	{
		// Built onto the park's own layer (0x00495abe) - see UiWindow.ParkScreen.
		ParkScreen = true;

		// w_big, the node "window4" inside w_big.MD2 - the frame five screens share. Without it this
		// screen is a list floating over the park. See docs/exe/hud.md.
		Root = new UiControl
		{
			Id = 0x12c4b7,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" )
		};

		Root.Add( new UiControl
		{
			Id = 0x12c4b8,
			Rect = new UiRect( 239, 179, 1835, 902 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		// Lettered by UI_SetTitle, each tab's builder with its own row (0x004953d7 to 0x0049547e): font 5 in
		// (234, 239, 102), with its edge, centred in the stream's rect (809 to 1281) widened by half its width either side.
		_title = Root.Add( new UiControl
		{
			Id = 0x12c4be,
			Rect = new UiRect( 573, 74, 1517, 153 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true
		} );

		BuildList( 5, new UiRect( 266, 318, 1825, 876 ), new UiRect( 275, 428, 1739, 875 ), Five, FiveHeadings );
		BuildList( 6, new UiRect( 261, 318, 1825, 876 ), new UiRect( 273, 428, 1741, 876 ), Six, SixHeadings );
		BuildList( 2, new UiRect( 266, 318, 1825, 876 ), new UiRect( 275, 428, 1739, 875 ), Two, TwoHeadings );

		// THE TAB STRIP HANGS OFF THE SCREEN, NOT OFF A LIST, and that is a deliberate departure.
		//
		// The original parents its tabs to the list, and can afford to: it loads ONE list tree into one
		// slot and replaces it per tab, so the tabs live on whichever list is current. This screen
		// builds all three shapes once and shows one of them, so a strip parented to any single list
		// goes away with it - Show() hides every list but the chosen one, and UiControl.Draw and
		// HitTest both stop at an invisible parent.
		//
		// Left on the five-column list, the strip would vanish with it the moment another shape was
		// shown, and no further tab could be clicked or even seen.
		//
		// On the root it is CONTAINED by the root, so it follows the screen's anchor with no pinning -
		// which is also what keeps the strip over the panel on a window that is not 4:3.
		_tabs = Root.Add( new UiRadioGroup
		{
			Id = 0x12c4b9,
			Rect = new UiRect( 1280, 192, 1745, 300 ),
			Mesh = UiMesh.Get( "list_all" )
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

		// Never assigned over AddOption's own handler, which is what lifts the other members - see
		// ParkBuyScreen for what assigning over it does.
		_tabs.SelectionChanged = () =>
		{
			foreach ( var tab in Tabs )
			{
				if ( tab.Id == _tabs.Selected )
					Show( tab.Index );
			}
		};

		// This category reaching its own siblings - the resolved meshes are what say which is which.
		Root.Add( CrossLink( 0x12c4bf, 360, 111, "b_parkinfo", 3 ) );
		Root.Add( CrossLink( 0x12c4c0, 467, 112, "b_allstaff", 4 ) );
		Root.Add( CrossLink( 0x12c4c1, 575, 113, "b_kids", 6 ) );

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		Show( openOn < 0 ? _lastTab : openOn );

		if ( pick != 0 )
		{
			_lists[Tabs[_tab].Columns].Pick( pick );
			LogOrder( _lists[Tabs[_tab].Columns] );
		}
	}

	/// <summary>One of the three list shapes, built once and shown when its tab is chosen.</summary>
	private void BuildList( int columns, UiRect rect, UiRect rows, (int Left, int Right)[] edges,
		UiRect[] headings )
	{
		var list = Root.Add( new UiList
		{
			Id = 400,
			Rect = rect,
			RowArea = rows,
			Columns = edges,

			// The row factory FUN_004944d0 measures its rows from font 7 ([0x00750d54], kept at [0x007ca158]), and the
			// styler FUN_00494690 letters each cell in it.
			RowFont = RowFont,
			RowHeight = UiList.RowHeightOf( RowFont ),

			// The add picks the top row as the first goes in (FUN_0066403b), so each fill highlights the row sorted first.
			// The screen's handler FUN_00495360 has no 0x401 arm: nothing takes the pick, and it is the highlight alone.
			PicksFirstRow = true,

			// Each tab's list record carries flag 0x80 (0x00750ab8, 0x00750ba0, 0x00750ca0): the row under the moving
			// pointer is picked, the highlight alone as the add's pick is.
			SelectsUnderPointer = true,

			// A right click on the list moves the camera to the selected row's thing and closes the screen (0x00495584).
			RowRightClicked = GoTo,
			SortChanged = word => _sorts[_tab] = word,

			// Every sub-builder's FUN_006636b2( 0, 0 ): the name is sorted as text.
			TextColumns = [true],

			// The styler FUN_00494690 leaves the name's label at its default, from the left, centres every tab's count
			// (FUN_0065c428( 1, 1 ), 0x00494780) and sets the money cells from the right ( 2, 1 ); the bars take none.
			ColumnAligns = [TextAlign.Start, TextAlign.Centre, .. Enumerable.Repeat( TextAlign.End, columns - 2 )]
		} );

		// Framed by FUN_00486660; the text and the help row are the tab's (Show).
		for ( var column = 0; column < headings.Length; ++column )
			list.AddFramedHeading( column, headings[column], string.Empty, -1 ).Clicked += () => LogOrder( list );

		list.Build();
		Log.Info( $"Items list: the {columns}-column shape, {list.RowsCensus()}, aligned {string.Join( ' ', list.ColumnAligns )}" );

		_lists[columns] = list;
	}

	/// <summary>
	/// The list's right click (<c>0x402</c>, <c>0x00495554</c>): the camera to the row's thing, then the screen closed
	/// (message 4, <c>0x00495599</c>).
	/// </summary>
	/// <remarks>The handler has no case by tab: a miscellaneous row goes to its object as a ride's does.</remarks>
	private void GoTo( int thing )
	{
		ParkOrbitCameraMode.GoToThing( thing );
		Stack.Close( this );
	}

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
	/// Fills the chosen tab's list with what the park has standing of that category.
	/// </summary>
	/// <remarks>
	/// <b>It walks <see cref="ParkState.Objects"/>, not the save's list</b>, which is the point of the
	/// screen: something bought this session appears and something sold does not. The purchase menu
	/// lists what the park MAY buy; this lists what it HAS.
	/// </remarks>
	private void Show( int index )
	{
		_tab = Math.Clamp( index, 0, Tabs.Length - 1 );
		_lastTab = _tab;

		var tab = Tabs[_tab];

		_title.Text = Localization.Get( tab.Title );
		_tabs.Put( tab.Id );
		_refresh = new UiTimer( RefreshEvery );

		Log.Info( $"Items list: tab {_tab}, filling" );

		foreach ( var (shape, list) in _lists )
		{
			list.Visible = shape == tab.Columns;
			list.Clear();
		}

		var chosen = _lists[tab.Columns];

		// Rides and shops share a tree here, and their bars stand in different columns: the cells are made again.
		chosen.BarColumns = tab.Bars;
		chosen.Build();

		// Handed over while the list is still empty, so it sorts nothing (FUN_006659af( word, 0 )).
		chosen.SetSort( _sorts[_tab] );

		// The headings, which change with the tab even where the shape does not - rides and shops share
		// a tree and name its five columns differently, and help with different rows.
		for ( var column = 0; column < tab.Columns; ++column )
		{
			foreach ( var child in chosen.Children )
			{
				if ( child.Id == 0x10 + column )
				{
					child.Text = Localization.Text( tab.FirstHeading + column );
					child.HelpText = tab.Helps[column];
				}
			}
		}

		Log.Info( $"Items list: headings of tab {_tab}: {chosen.HeadingsCensus()}" );

		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue )
		{
			return;
		}

		if ( tab.Index == ItemDescriptionFile.Feature )
		{
			// A row a placed object, as the other tabs, each with its item's standing count: the adder 0x004941e0
			// hands the object's name and its control record's +0x18 (FUN_004ca8c0), the row keyed by the thing.
			foreach ( var placed in state.Objects )
			{
				if ( placed.IsPlaced && catalogue.TryGet( placed.CatalogueId, out var each )
					&& each.UiType == tab.Index )
				{
					var owned = state.BuiltOf( placed.CatalogueId ).Standing;

					var name = ParkObjectNames.NameOf( each );

					chosen.Add( new UiList.Row( placed.ThingId, name, owned ) );
					Log.Info( $"Items list: tab {_tab}, row {placed.ThingId} {name}: owned {owned}" );
				}
			}

			LogOrder( chosen );
			return;
		}

		foreach ( var placed in state.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item )
				|| item.UiType != tab.Index )
			{
				continue;
			}

			var numbers = NumbersOf( tab.Index, placed, item, state );
			var name = ParkObjectNames.NameOf( item );

			chosen.Add( new UiList.Row( placed.ThingId, name, 0, Values: ValuesOf( tab, numbers ),
				Colour: ParkClosedStatus.Colour( ParkClosedStatus.For( level.Park, placed, item.TrackType ) ),
				Numbers: numbers ) );

			Log.Info( $"Items list: tab {_tab}, row {placed.ThingId} {name}: {Census( numbers )}" );
		}

		LogOrder( chosen );
	}

	/// <summary>The rows in the order they stand, after a fill and a heading's click, and the row picked.</summary>
	private void LogOrder( UiList list ) => Log.Info( $"Items order: tab {_tab}, {list.Census()}; picked {list.Selected}" );

	/// <summary>
	/// A placed object's row by column, as the fill <c>FUN_00493cd0</c> and the rewrite <c>FUN_004955e0</c> hand it: the
	/// name's nought, then the customers of the last thirty finished days (<c>FUN_00495c20</c> on the ring
	/// <c>+0x1a8</c>, <c>__ftol</c>'d), each bar's <c>((value &amp; 0xff) &lt;&lt; 10) / 100</c>: the excitement
	/// (<c>FUN_004e0860( 0 )</c>), the state of repair <c>+0x44</c>, the remaining life <c>+0x48</c>, each float
	/// truncated, and the satisfaction (<c>FUN_004e1e30</c>); and the money as it stands: the last thirty finished
	/// days' profit (<c>FUN_004e1c70</c>) and the total, <c>mTotalTakings</c> less <c>mTotalCosts</c>
	/// (<c>FUN_004e1c60</c>).
	/// </summary>
	internal static int[] NumbersOf( int tab, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item, ParkState state )
	{
		var rings = state.RingsFor( placed.ThingId );
		var customers = rings.Customers.LastDays( CustomersDays );
		var excitement = Bar( ParkRideScore.ExcitementOf( placed, item, state.TrackRides ) );

		if ( tab == 0 )
			return [0, customers, excitement, Bar( (int)placed.StateOfRepair ), Bar( (int)placed.RemainingLife )];

		var lastMonth = rings.ProfitOfLastDays( CustomersDays );
		var total = unchecked(state.TakingsFor( placed.ThingId ) - state.CostsFor( placed.ThingId ));
		var satisfaction = Bar( rings.SatisfactionOfLastDays( CustomersDays ) );

		return tab == 1
			? [0, customers, lastMonth, total, satisfaction]
			: [0, customers, excitement, lastMonth, total, satisfaction];
	}

	/// <summary>A value as a bar's 0 to 1024: its low byte as a percentage.</summary>
	internal static int Bar( int value ) => ((value & 0xff) << 10) / 100;

	/// <summary>How many finished days the customers column adds up (<c>PUSH 0x1e</c>, <c>0x004956ca</c>), and the profit
	/// and satisfaction (<c>MOV EDX,0x1e</c>, <c>0x004e1c9b</c>; <c>MOV EBP,0x1e</c>, <c>0x004e1e38</c>).</summary>
	private const int CustomersDays = 30;

	/// <summary>
	/// The columns after the name as text: the customers printed as a whole number, a bar's column empty, and a money
	/// column as its cell (<c>FUN_00490250</c>, <c>0x004903e0</c>) letters it: UITEXT <c>0x1c1</c> and the figure, or
	/// <c>0x1c2</c> and the figure without its sign when it is under nought, each <c>"%d"</c>.
	/// </summary>
	private static string[] ValuesOf( Tab tab, int[] numbers )
		=> [.. Enumerable.Range( 1, tab.Columns - 1 ).Select( column =>
			column == 1 ? $"{numbers[1]}"
			: tab.Bars[column] ? ""
			: Money( numbers[column] ) )];

	/// <summary>A money cell's text (<see cref="ValuesOf"/>).</summary>
	internal static string Money( int value )
		=> Localization.Get( value < 0 ? UIStrings.NegativeDollar : UIStrings.Dollar ) + unchecked(value < 0 ? -value : value);

	private static string Census( int[] numbers )
		=> string.Join( " ", numbers.Skip( 1 ).Select( number => number == UiList.NoBar ? "-" : $"{number}" ) );

	/// <summary>
	/// Every two seconds, on the rides', shops' and sideshows' tabs, each row's columns after the name rewritten where it
	/// stands, from the last row to the first (<c>FUN_004955e0</c>: masks <c>0x1e</c> and, for the sideshows,
	/// <c>0x3e</c>). The name and the row's colour stay as the fill wrote them, nothing is added, removed or re-sorted,
	/// and the miscellaneous tab is not rewritten (<c>docs/exe/hud.md</c>, "How allstaff and allitems keep their rows
	/// current").
	/// </summary>
	protected internal override void Update()
	{
		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 )
			return;

		var tab = Tabs[_tab];

		if ( tab.Index == ItemDescriptionFile.Feature || Level.Current?.ParkState is not { } state )
			return;

		var list = _lists[tab.Columns];
		var objects = state.Objects.ToList();
		var catalogue = Level.Current?.Catalogue;
		var rewritten = 0;

		for ( var index = list.Rows.Count - 1; index >= 0; --index )
		{
			var row = list.Rows[index];
			var at = objects.FindIndex( placed => placed.ThingId == row.Id );

			if ( at < 0 )
				continue;

			var placed = objects[at];

			if ( catalogue == null || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var numbers = NumbersOf( tab.Index, placed, item, state );

			list.Replace( index, row with { Values = ValuesOf( tab, numbers ), Numbers = numbers } );
			++rewritten;
		}

		Log.Info( $"Items list: tab {_tab}, {rewritten} rows rewritten"
			+ (list.Rows.Count > 0 ? $", row 0 {list.Rows[0].Id} {Census( [.. list.Rows[0].Numbers!] )}" : "") );
	}
}
