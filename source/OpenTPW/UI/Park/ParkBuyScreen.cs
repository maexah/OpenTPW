namespace OpenTPW.UI;

/// <summary>
/// The purchase menu - the screen behind the gadget's <c>b_buy</c> button, and the first of the four
/// dead ones to have anything behind it.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_004acc70</c> builds it from the compiled layout stream
/// at <c>0x00754cf8</c>, with <c>FUN_004ac270</c> as its handler. Every rectangle and every id below
/// is that stream's, walked opcode by opcode to a balanced close - 1080 bytes, 32 controls - and the
/// walk is corroborated three ways: the tab help rows, the resolved mesh names and the screen's own
/// tab-index switch all give the same ordering, matching UITEXT 119-122. See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>The tab ids are deliberately not in order</b>, and that is the stream's doing rather than a
/// mistake here: tab 0 (rides) is <c>0x1fb</c>, tab 1 (shops) <c>0x1fd</c>, tab 2 (sideshows)
/// <c>0x1fa</c> and tab 3 (features) <c>0x1fc</c>. The tab index is the item's own
/// <c>Info.WhichUIType</c>, which is what makes the four tabs one walk with one test.
/// </para>
///
/// <para>
/// <b>The tab group is a CHILD of the list.</b> In the original's tree the tabs, the column headers
/// and the scrollbar all hang off the list control, which is why that screen installs its tab handler
/// on the list. Nothing here depends on the bubbling - each tab carries its own action - but the
/// nesting is kept because it is the layout.
/// </para>
///
/// <para>
/// <b>Two things are deliberately NOT built, each for a reason rather than for want of a rect.</b>
/// <b>The root's frame mesh is <c>w_big</c>, and this paragraph said for months that it resolved to
/// nothing.</b> It claimed <c>0xf76e4200</c> matched "no name in the executable or any shipped file,
/// after a search of all 2,488 of them" - but that search was over FILE names, and the stream hashes
/// a model's first NODE name. The node is <c>window4</c>, inside <c>w_big.MD2</c>. Five screens share
/// it and all five drew without a backdrop until it was found. The stats panel <c>0x1ed</c> and its
/// seven readouts are excitement, reliability
/// and capacity - simulation values this game does not have, the same reason the map screen's overlays
/// are refused. And the row's third column is a tick-box in the original, skinned with
/// <c>i_boxtick</c> and framed from the row state; until that is drawn it shows the state in the
/// game's own words instead of a bare number.
/// </para>
///
/// <para>
/// <b>Whether the original pauses here is NOT established</b> - its own builder was read and nothing
/// in it asks for a pause, unlike the map screen, which plainly does. So this does not pause, and the
/// park carries on behind it.
/// </para>
/// </summary>
internal sealed class ParkBuyScreen : UiWindow
{
	/// <summary>The four tabs, in tab-index order, with the control id and help row the stream gives each.</summary>
	private static readonly (int Index, int Id, int Help, string Mesh, UIStrings Title, UiRect Rect)[] Tabs =
	[
		// b_srides, plural - the stream hashes the model's first NODE name and the file is named
		// otherwise. See ParkFrontEnd.Meshes, where all seven of these were corrected from the archive.
		(0, 0x1fb, 140, "b_srides",    UIStrings.BuyRide,      new UiRect( 1283, 195, 1386, 297 )),
		(1, 0x1fd, 141, "b_sshop",     UIStrings.BuyShop,      new UiRect( 1521, 195, 1623, 297 )),
		(2, 0x1fa, 142, "b_sshow",     UIStrings.BuySideshow,  new UiRect( 1402, 195, 1504, 297 )),
		(3, 0x1fc, 143, "b_sfeature",  UIStrings.BuyMiscItems, new UiRect( 1640, 195, 1742, 297 )),
	];

	/// <summary>
	/// The two rows the features tab carries that are not items at all - the land tools. The original
	/// appends them after the real rows with the ids <b>-1</b> and <b>-2</b>, and they are synthetic
	/// for a measurable reason: both ARE shipped items (101 "Buy Land" and 102 "Clear Land") but both
	/// declare <c>Info.WhichUIType</c> 4, the files' own "Not to be shown in UI", so the walk that
	/// builds the list cannot reach them.
	/// </summary>
	private const int BuyLandRow = -1;

	private const int ClearLandRow = -2;

	private readonly UiList _list;
	private readonly UiControl _title;
	private readonly UiControl _money;
	private readonly UiRadioGroup _tabs;

	private int _tab;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastTab;

	public ParkBuyScreen( WindowStack stack ) : base( stack )
	{
		// Modal so the park behind cannot be clicked through, but NOT pausing - see the class remarks.
		Modal = true;

		Root = new UiControl
		{
			Id = 0x1e9,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" )
		};

		_title = Root.Add( new UiControl
		{
			Id = 0x1fe,
			Rect = new UiRect( 805, 74, 1277, 153 ),
			Font = 5,
			TextColour = UiColour.White
		} );

		_money = Root.Add( new UiControl
		{
			Id = 0x200,
			Rect = new UiRect( 1297, 75, 1769, 154 ),
			Font = 5,
			TextColour = UiColour.White,
			TextAcross = TextAlign.End
		} );

		// The description panel. Its frame is real art the theme ships; what goes IN it is the item's
		// own preview, which wants a model rendered into the screen and is not built.
		Root.Add( new UiControl
		{
			Id = 0x1ea,
			Rect = new UiRect( 408, 179, 822, 593 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		// The stats panel, kept as backing art with nothing in it - see the class remarks, and see
		// ParkMapScreen, which keeps its two bare button panels for the same reason.
		Root.Add( new UiControl
		{
			Id = 0x1ed,
			Rect = new UiRect( 259, 621, 971, 902 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		_list = Root.Add( new UiList
		{
			Id = 0x1f8,
			Rect = new UiRect( 1009, 179, 1813, 902 ),
			Mesh = UiMesh.Get( "f_buyitem" ),
			RowArea = new UiRect( 1037, 427, 1727, 871 ),

			// The stream's own column edges, op 0xb: name, price, and the owned/researched state. The
			// third is 51 units wide because it is a TICK-BOX, not a number - see UiList.StateMesh.
			Columns = [(1039, 1447), (1460, 1664), (1673, 1724)],
			StateMesh = "i_boxtick",
			Activated = Chose
		} );

		_tabs = _list.Add( new UiRadioGroup
		{
			Id = 0x1f9,
			Rect = new UiRect( 1280, 192, 1745, 300 )
		} );

		foreach ( var tab in Tabs )
		{
			var index = tab.Index;

			var option = _tabs.AddOption( new UiButton
			{
				Id = tab.Id,
				Rect = tab.Rect,
				HelpText = tab.Help,
				Mesh = UiMesh.Get( tab.Mesh ),
				Toggles = true
			} );

			// NOT assigned over the top of AddOption's own handler, which is what lifts the other
			// members: UiRadioGroup.AddOption sets Clicked to its own Select, so writing Clicked here
			// would leave the group with no way to turn the buttons over - a screen whose tabs all
			// stay down, compiling perfectly. The group's SelectionChanged is the hook that means
			// "the choice changed", and the button's own id is what it changed to.
			_ = option;
		}

		_tabs.SelectionChanged = () =>
		{
			foreach ( var tab in Tabs )
			{
				if ( tab.Id == _tabs.Selected )
					Show( tab.Index );
			}
		};

		// The cross-link to the other half of this button. The gadget's b_buy opens whichever of the
		// two was last left on, so the pair reach each other directly as well - help row 153, "Left-
		// click to hire staff (H)".
		Root.Add( new UiButton
		{
			Id = 0x1ff,
			Rect = new UiRect( 1866, 583, 1968, 686 ),
			HelpText = 153,
			Mesh = UiMesh.Get( "b_allstaff" ),
			Clicked = () =>
			{
				Stack.Close( this );
				Stack.Open( new ParkHireScreen( Stack ) );
			}
		} );

		// The stream gives the close button id -2 and UIHELPTEXT row 2, "Left-click to cancel".
		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 2,
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Show( _lastTab );
	}

	/// <summary>
	/// Fills the list with one tab's items - the original's <c>FUN_004aaf70</c>, whose filter is the
	/// item's <c>WhichUIType</c> against the tab being shown.
	/// </summary>
	private void Show( int tab )
	{
		_tab = Math.Clamp( tab, 0, Tabs.Length - 1 );
		_lastTab = _tab;

		var chosen = Tabs[_tab];

		_title.Text = Localization.Get( chosen.Title );
		_tabs.Select( chosen.Id );

		_list.Clear();

		if ( Level.Current?.Catalogue is { } catalogue )
		{
			foreach ( var item in catalogue.All.Where( item => item.UiType == _tab )
				.OrderBy( item => item.Name, StringComparer.OrdinalIgnoreCase ) )
			{
				_list.Add( new UiList.Row( item.Id, item.Name, item.BuildPrice, StateOf( item ) ) );
			}
		}

		// The land tools, which only the features tab carries - see the constants above.
		if ( _tab == ItemDescriptionFile.Feature )
		{
			_list.Add( new UiList.Row( BuyLandRow, Localization.Get( UIStrings.BuyLand ), 0 ) );
			_list.Add( new UiList.Row( ClearLandRow, Localization.Get( UIStrings.ClearLand ), 0 ) );
		}

		ShowMoney();
	}

	/// <summary>
	/// The row's state column: <b>1 if the park already owns at least one</b>, 2 for one of the three
	/// most recently researched, 0 otherwise.
	/// </summary>
	/// <remarks>
	/// <b>State 2 is never produced here, and that is a gap rather than a simplification.</b> The
	/// original's ring is fed only by research COMPLETING - not by building - and this game has no
	/// research, so nothing can be recently researched. The game's own help row 146 names both halves:
	/// "sort the list by items already owned or recently researched".
	/// </remarks>
	private static int StateOf( ParkItemCatalogue.Item item )
	{
		if ( Level.Current?.ParkState is not { } state )
			return 0;

		foreach ( var placed in state.Objects )
		{
			if ( placed.CatalogueId == item.Id && placed.IsPlaced )
				return 1;
		}

		return 0;
	}

	/// <summary>
	/// A row was chosen. The original checks the price against the balance and, if it passes, puts the
	/// item in the player's hand and closes the screen - the money is not taken until the thing is
	/// actually placed.
	/// </summary>
	/// <remarks>
	/// <b>Carrying is not built yet</b>, so this reports what it would carry and counts the gap rather
	/// than pretending. <c>ParkBuilding.Buy</c> is the verb underneath and is already proven in a
	/// running park from the console; what is missing between them is the mode that follows the
	/// cursor.
	/// </remarks>
	private void Chose( int rowId )
	{
		if ( rowId is BuyLandRow or ClearLandRow )
		{
			Unimplemented.Report( "BUY_LAND_TOOL" );
			Log.Info( "Buy screen: the land tools are not built" );
			return;
		}

		if ( Level.Current?.Catalogue is not { } catalogue || !catalogue.TryGet( rowId, out var item ) )
			return;

		// Into the hand, and the screen closes behind it - which is what the original does, and why
		// the money is not taken here: it is taken when the thing is actually put down.
		Log.Info( "Buy screen: " + ParkBuilding.Carry( item.Id ) );

		if ( ParkBuilding.Carrying != item.Id )
			return;

		Stack.Close( this );

		// And clicking the park puts it down - Level.WorldClick, which takes the click only when the
		// interface did not. `put <x> <y>` still does it from the console, for a test that cannot move
		// the pointer.
	}

	/// <summary>What the park has, in the corner the stream puts it - control 0x200.</summary>
	private void ShowMoney()
		=> _money.Text = Level.Current?.ParkState is { } state ? $"{state.Balance}" : null;

	protected internal override void Update() => ShowMoney();
}
