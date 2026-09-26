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
/// <b>What this can actually answer, and what stays blank.</b> Rides fill <i>Name</i>, <i>State Of
/// Repair</i> and <i>Remaining Life</i> - the two floats the ride window already reads - and leave
/// <i>Users Last Month</i> and <i>Excitement</i> empty, the first wanting the record's monthly ring
/// buffers and the second the descriptor field <c>park.md</c> records as unproven. Shops and sideshows
/// fill only their name, for the same two reasons. <b>Miscellaneous items fill both their columns</b>,
/// because <i>Number Owned</i> is a count of what is standing. A blank column is an honest gap; a
/// plausible wrong one is not - putting an object's gross takings under "Total Profit" would be a
/// different quantity wearing that label, which is the mistake the hire screen's own remarks warn of.
/// </para>
/// </summary>
internal sealed class ParkItemsScreen : UiWindow
{
	/// <summary>How the four tabs are laid out, in the tab order <c>FUN_00495aa0</c>'s switch uses.</summary>
	private readonly record struct Tab( int Index, int Id, int Help, string Mesh, UIStrings Title,
		int FirstHeading, int Columns, UiRect Rect );

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
		new( 0, 0x12c4ba, 101, "b_srides",   UIStrings.AllRides,     82, 5, new UiRect( 1283, 195, 1386, 297 ) ),
		new( 1, 0x12c4bc, 102, "b_sshop",    UIStrings.AllShops,     87, 5, new UiRect( 1521, 195, 1623, 297 ) ),
		new( 2, 0x12c4bb, 103, "b_sshow",    UIStrings.AllSideshows, 92, 6, new UiRect( 1402, 195, 1504, 297 ) ),
		new( 3, 0x12c4bd, 104, "b_sfeature", UIStrings.AllMiscItems, 98, 2, new UiRect( 1640, 195, 1742, 297 ) ),
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

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastTab;

	public ParkItemsScreen( WindowStack stack ) : base( stack )
	{
		Modal = true;

		// Built onto the park's own layer (0x00495abe), so a right press beside it is the park's.
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

		_title = Root.Add( new UiControl
		{
			Id = 0x12c4be,
			Rect = new UiRect( 809, 74, 1281, 153 ),
			Font = 5,
			TextColour = UiColour.White
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

		Show( _lastTab );
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

			// A right click on a row moves the camera to that thing and closes the screen
			// (0x00495584; docs/QUEUE.md Q117). Not built: counted on the press.
			RightPressed = () => Unimplemented.Report( "LIST_ROW_RIGHT_CLICK" )
		} );

		for ( var column = 0; column < headings.Length; ++column )
			list.AddHeading( column, headings[column], string.Empty );

		list.Build();

		_lists[columns] = list;
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
		_tabs.Select( tab.Id );

		foreach ( var (shape, list) in _lists )
		{
			list.Visible = shape == tab.Columns;
			list.Clear();
		}

		var chosen = _lists[tab.Columns];

		// The headings, which change with the tab even where the shape does not - rides and shops share
		// a tree and name its five columns differently.
		for ( var column = 0; column < tab.Columns; ++column )
		{
			foreach ( var child in chosen.Children )
			{
				if ( child.Id == 0x10 + column )
					child.Text = Localization.Text( tab.FirstHeading + column );
			}
		}

		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue )
		{
			return;
		}

		if ( tab.Index == ItemDescriptionFile.Feature )
		{
			// Miscellaneous items are counted rather than listed one by one, because their two columns
			// are Name and Number Owned - a row per bin would make that second column read 1 for ever.
			var owned = new Dictionary<int, int>();

			foreach ( var placed in state.Objects )
			{
				if ( placed.IsPlaced && catalogue.TryGet( placed.CatalogueId, out var each )
					&& each.UiType == tab.Index )
				{
					owned[placed.CatalogueId] = owned.GetValueOrDefault( placed.CatalogueId ) + 1;
				}
			}

			foreach ( var (id, count) in owned.OrderBy( entry => entry.Key ) )
			{
				if ( catalogue.TryGet( id, out var item ) )
					chosen.Add( new UiList.Row( id, item.Name, count ) );
			}

			return;
		}

		foreach ( var placed in state.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item )
				|| item.UiType != tab.Index )
			{
				continue;
			}

			// Rides are the one tab with numbers this game can answer - the two floats the ride window
			// already reads back. Everything else stays empty; see the class remarks.
			var values = tab.Index == 0
				? new[] { "", "", $"{(int)placed.StateOfRepair}", $"{(int)placed.RemainingLife}" }
				: new string[tab.Columns - 1];

			chosen.Add( new UiList.Row( placed.ThingId, item.Name, 0, Values: values ) );

			// The original colours a row by the thing's status (FUN_00485f60, table 0x0074fb50): grey for
			// closed.
			if ( placed.CanLoad == 0 )
				Unimplemented.Report( "ALL_ITEMS_CLOSED_ROW_COLOUR" );
		}

		// Named rather than left quietly blank: the monthly ring buffers behind "last month" and
		// "profit", and the descriptor field behind "excitement" that park.md records as unproven.
		Unimplemented.Report( "ALL_ITEMS_MONTHLY_HISTORY" );
		Unimplemented.Report( "ALL_ITEMS_EXCITEMENT" );
	}
}
