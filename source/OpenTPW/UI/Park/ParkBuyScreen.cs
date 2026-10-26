namespace OpenTPW.UI;

/// <summary>
/// The purchase menu - the screen behind the gadget's <c>b_buy</c> button.
///
/// <para>
/// <b>Where the layout comes from.</b> The original's compiled layout stream at <c>0x00754cf8</c>,
/// 32 controls: every rectangle and every id below is that stream's. See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>The tab ids are not in order</b>, because the stream's are not: tab 0 (rides) is <c>0x1fb</c>,
/// tab 1 (shops) <c>0x1fd</c>, tab 2 (sideshows) <c>0x1fa</c> and tab 3 (features) <c>0x1fc</c>. The
/// tab index is the item's own <c>Info.WhichUIType</c>, which is what makes the four tabs one walk with
/// one test.
/// </para>
///
/// <para>
/// <b>The tab group is a child of the list.</b> In the original's tree the tabs, the column headers
/// and the scrollbar all hang off the list control. Nothing here depends on the bubbling - each tab
/// carries its own action - but the nesting is kept because it is the layout.
/// </para>
///
/// <para>
/// <b>One thing is not built.</b> The stats panel <c>0x1ed</c> and its seven readouts are excitement,
/// reliability and capacity: none is filled in, and the panel is kept as bare backing art, as the map
/// screen keeps its two button panels. The root's frame is <c>w_big</c>, the frame five screens share, whose first node is
/// <c>window4</c>. The row's third column is the original's tick-box, <c>i_boxtick</c> framed from the
/// row state - see <c>UiList.StateMesh</c>.
/// </para>
///
/// <para>
/// <b>It does not pause.</b> Nothing in the original's builder asks for a pause, where the map
/// screen's does, so the park carries on behind it.
/// </para>
/// </summary>
internal sealed class ParkBuyScreen : UiWindow
{
	/// <summary>The four tabs, in tab-index order, with the control id and help row the stream gives each.</summary>
	private static readonly (int Index, int Id, int Help, string Mesh, UIStrings Title, UiRect Rect)[] Tabs =
	[
		// b_srides, plural - the stream hashes the model's first NODE name and the file is named
		// otherwise. See ParkFrontEnd.Meshes, which lists the names ui.wad really holds.
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

	private readonly ParkFootprintPicture _footprint;

	/// <summary>The footprint picture as the panel has it.</summary>
	internal ParkFootprintPicture Footprint => _footprint;

	/// <summary>
	/// The row waiting to be shown in the panel, by its id, and when it began to wait - the original's
	/// <c>[0x007cc1e4]</c> and <c>[0x007cc1f0]</c>. Nought is no row: no item has that id.
	/// </summary>
	private int _pending;

	private long _pendingSince;

	/// <summary>How long a row waits before the panel shows it: more than 500 ms (<c>0x004ac443</c>).</summary>
	internal const long PreviewDelay = 500;

	/// <summary>The row the panel shows, by its id, or nought before any has been shown.</summary>
	internal int Previewed { get; private set; }

	private int _tab;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastTab;

	/// <summary>
	/// The list's sort, kept for the session: the column + 1, negated when descending - the original's
	/// <c>[0x00755140]</c>, which starts at 1, the name ascending.
	/// </summary>
	private static int _sort = 1;

	/// <summary>The headings' font slot and colour, as the opener hands them to <c>FUN_00486660</c> (<c>[0x00755138]</c>).</summary>
	private const int HeadingFont = 8;

	/// <summary>The rows' font slot, the row factory's and the styler's (<c>[0x00755134]</c>).</summary>
	private const int RowFont = 7;

	private static readonly UiColour HeadingColour = new( 255, 239, 0 );

	private ParkState? _parkGiven;

	/// <summary>The running park whose advisor hears of this screen: the level's, or one a test gives.</summary>
	internal ParkState? Park { get => _parkGiven ?? Level.Current?.ParkState; set => _parkGiven = value; }

	// The park whose advisor this screen has asked for its line, none before it is first shown.
	private ParkState? _asked;

	/// <summary>
	/// The opener's last act on a screen it has just built (FUN_004acc70, 0x004ad1e2): the advisor is posted
	/// message 0xbc (FUN_00486b00), the screen's own line. A screen already open is asked for nothing more. Its
	/// first act (0x004acc8f) leaves the gadget's category on buy.
	/// </summary>
	protected internal override void Shown()
	{
		ParkCategoryScreens.Remember( ParkCategoryScreens.Building, ParkCategoryScreens.Buy );

		if ( _asked != null || Park is not { AdvisorMessages: { } lines } park )
			return;

		_asked = park;
		lines.Post( ParkAdvisorMessages.BuyScreen, park.GameTick );
	}

	/// <summary>
	/// The screen's destroy (FUN_004ac270, event 0x14): the advisor's message 0xbc is withdrawn (FUN_00486b40),
	/// which cuts him off where he is saying it.
	/// </summary>
	protected internal override void Closed()
		=> _asked?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.BuyScreen, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

	/// <param name="openOn">The tab to open on (FUN_004acc70's first argument), null for the one last left on.</param>
	public ParkBuyScreen( WindowStack stack, int? openOn = null ) : base( stack )
	{
		// Not modal and not pausing - see the class remarks. Built onto the park's own layer (0x004acd62): the gadget
		// answers beside it, and what a press beside it does is UiWindow.ParkScreen's to say.
		ParkScreen = true;

		Root = new UiControl
		{
			Id = 0x1e9,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" ),
			// Lettered larger than the original's where each box holds it: a deviation, UiFonts.ParkScreenLettering.
			TextSize = UiFonts.ParkScreenLettering
		};

		// Lettered by UI_SetTitle (0x00485d20): font 5 on the purple skin in (234, 239, 102), centred in the stream's
		// rect (805 to 1277) widened by half its width either side.
		_title = Root.Add( new UiControl
		{
			Id = 0x1fe,
			Rect = new UiRect( 569, 74, 1513, 153 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true
		} );

		// The purple skin, font 5 in white, from the right (PurpleSkin_Construct, 0x004ace70).
		_money = Root.Add( new UiControl
		{
			Id = 0x200,
			Rect = new UiRect( 1297, 75, 1769, 154 ),
			Font = 5,
			TextColour = UiColour.White,
			TextShadow = true,
			TextAcross = TextAlign.End
		} );

		// The description panel. Its frame is real art the theme ships; of what goes IN it, the footprint picture is
		// built and the item's turning model and its name row 0x1ec are not.
		var panel = Root.Add( new UiControl
		{
			Id = 0x1ea,
			Rect = new UiRect( 408, 179, 822, 593 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		_footprint = panel.Add( new ParkFootprintPicture
		{
			Id = 0x1eb,
			Rect = new UiRect( 440, 407, 594, 561 )
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

			// The name is text; the price and the state are numbers (0x004ad033).
			TextColumns = [true, false, false],
			StateMesh = "i_boxtick",

			// The row factory FUN_004ad240 measures its rows from font 7 ([0x00755134], kept at [0x007cc200]), and the
			// styler FUN_004ad340 letters each cell in it. It leaves the name's label at its default, from the left, sets the
			// price from the right (FUN_0065c428( 2, 1 ), 0x004ad428) and hands the tick-box a mesh alone, no lettering.
			RowFont = RowFont,
			ColumnAligns = [TextAlign.Start, TextAlign.End],
			RowHeight = UiList.RowHeightOf( RowFont ),
			SelectsUnderPointer = true,
			Activated = Chose,
			SelectionChanged = RowSelected,

			// The handler's 0x403 arm, BuyList_CellColourCallback (0x004acb70), asked as the rows are drawn again.
			RowColour = row => ColourOf( row.Id, Level.Current?.Catalogue, Level.Current?.ParkState?.Balance ?? 0,
				GoldenTickets.Rides, GoldenTickets.TicketsLeft ),

			// The screen's handler keeps the list's 0x406.
			SortChanged = word => _sort = word
		} );

		// Handed over while the list is still empty, so it sorts nothing (0x004ad057).
		_list.SetSort( _sort );
		Log.Info( $"Buy list: {_list.RowsCensus()}, aligned {string.Join( ' ', _list.ColumnAligns )}" );

		// UITEXT 123, 124 and 138; the rects are the stream's.
		(int Text, int Help, UiRect Rect)[] headings =
		[
			(123, 0x90, new UiRect( 1032, 317, 1454, 422 )),
			(124, 0x91, new UiRect( 1458, 317, 1664, 422 )),
			(138, 0x92, new UiRect( 1669, 317, 1729, 422 )),
		];

		for ( var column = 0; column < headings.Length; ++column )
		{
			var heading = headings[column];

			_list.AddHeading( column, heading.Rect, Localization.Text( heading.Text ), "!cbut", HeadingFont, HeadingColour, heading.Help )
				.Clicked += LogOrder;
		}

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

		// The cross-link to the other half of this button. The original's b_buy opens whichever of the
		// two was last left on, and the pair reach each other directly as well - help row 153, "Left-
		// click to hire staff (H)". A deviation: here b_buy always opens this screen.
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
		Show( openOn ?? _lastTab );

		// The opener letters the corner itself, as the tick does (FUN_004acc70, 0x004acdae).
		_money.Text = MoneyText();
	}

	/// <summary>What FUN_004acc70 does with the screen already open: the list filled with <paramref name="tab"/>'s items and its button put down.</summary>
	internal void PickTab( int tab ) => Show( tab );

	/// <summary>The tab shown.</summary>
	internal int Tab => _tab;

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
		_tabs.Put( chosen.Id );

		_list.Clear();

		// The help bar's item goes with the rows: the original's fill picks the new first row (0x004aca3e), which this
		// list does not (LIST_FIRST_ROW_SELECTED), so the new tab has no item until the pointer selects one.
		_helpItem = 0;

		// Each row goes in by the list's sort, in the catalogue's order as the original walks it (FUN_00412e60), so rows
		// equal on the sorted column stand in that order: the two mystery rides, Jurassic Tours' before Eruption's.
		// A deviation: the price is the item file's, where the original shows its control record's +0x04 (0x004ab086),
		// which the record takes from the same key; the two agree in all 50 of the shipped park's records.
		if ( Level.Current is { Catalogue: { } catalogue, Research: { } research } )
		{
			foreach ( var item in Listed( catalogue, research, _tab ) )
			{
				_list.Add( GoldenTickets.IsMystery( item )
					? PricedRow( item.Id, Localization.Get( UIStrings.MysteryRide ), -item.GoldenTicketCost, StateOf( item ) )
					: PricedRow( item.Id, ParkObjectNames.NameOf( item ), item.BuildPrice, StateOf( item ) ) );
			}
		}

		// The land tools, which only the features tab carries - see the constants above.
		if ( _tab == ItemDescriptionFile.Feature )
		{
			_list.Add( PricedRow( BuyLandRow, Localization.Get( UIStrings.BuyLand ), 0 ) );
			_list.Add( PricedRow( ClearLandRow, Localization.Get( UIStrings.ClearLand ), 0 ) );
		}

		LogOrder();
	}

	private void LogOrder() => Log.Info( $"Buy list: tab {_tab}, {_list.Census()}" );

	/// <summary>
	/// The items one tab lists, in the catalogue's order: its kind, and <b>only a researched item</b>
	/// (<c>0x004ab023</c>), the park's flag - see <see cref="ParkResearch"/>.
	/// </summary>
	internal static IEnumerable<ParkItemCatalogue.Item> Listed( ParkItemCatalogue catalogue, ParkResearch research, int tab )
		=> catalogue.All.Where( item => item.UiType == tab && research.IsResearched( item.Id ) );

	/// <summary>
	/// A row with its price lettered by the price cell's skin (<see cref="PriceText"/>) and sorted on the number. A mystery
	/// row (<c>0x004ab023</c>..<c>0x004ab063</c>, <see cref="GoldenTickets.IsMystery(ParkItemCatalogue.Item)"/>) is
	/// named UITEXT 137 and priced at its ticket cost negated.
	/// </summary>
	private static UiList.Row PricedRow( int id, string name, int price, int state = 0 )
		=> new( id, name, price, state, Values: [PriceText( price )] );

	/// <summary>
	/// The price cell's setter, <c>PriceSkin_SetValue</c> (<c>0x00490650</c>): a number at or above nought by UITEXT 449
	/// "$ {P13}", one below nought by 455 "{P23} gt" with its negation. A number past ±<c>0xeeeeeee</c> takes 449 or 450
	/// "-$ {P19}" less that bias, as the skin letters a biased price.
	/// </summary>
	internal static string PriceText( int value )
	{
		const int Bias = 0xeeeeeee;

		if ( Math.Abs( (long)value ) <= Bias )
			return value < 0 ? Localization.Format( 455, (23, $"{-value}") ) : Localization.Format( 449, (13, $"{value}") );

		return value < 0
			? Localization.Format( 450, (19, $"{Math.Abs( (long)value + Bias )}") )
			: Localization.Format( 449, (13, $"{value - Bias}") );
	}

	/// <summary>A land row's lettering, a row id below nought.</summary>
	internal static readonly UiColour LandColour = new( 231, 239, 75 );

	/// <summary>A mystery row the tickets buy.</summary>
	internal static readonly UiColour TicketsColour = new( 255, 224, 0 );

	/// <summary>A row the bank or the tickets do not buy.</summary>
	internal static readonly UiColour ShortColour = new( 255, 0, 0 );

	/// <summary>
	/// <c>BuyList_CellColourCallback</c> (<c>0x004acb70</c>), the same for a row's every cell: a land row
	/// <see cref="LandColour"/>; a mystery row gold where <see cref="GoldenTickets.CanBuyRide"/> passes and red where it
	/// fails; any other white, or red where its price is above the bank.
	/// </summary>
	/// <remarks>A deviation, the list's own: the price is the item file's, where the original reads its control record's <c>+0x04</c>.</remarks>
	internal static UiColour? ColourOf( int rowId, ParkItemCatalogue? catalogue, long balance, IReadOnlyCollection<ushort>? rides, int tickets )
	{
		if ( rowId < 0 )
			return LandColour;

		if ( catalogue is null || !catalogue.TryGet( rowId, out var item ) )
			return null;

		if ( GoldenTickets.IsMystery( item, rides ) )
			return GoldenTickets.CanBuyRide( item, tickets ) ? TicketsColour : ShortColour;

		return balance < item.BuildPrice ? ShortColour : UiColour.White;
	}

	/// <summary>
	/// The row's state column (<c>0x004ab097</c>..<c>0x004ab0f7</c>): <b>1 if the park already owns at least one</b>,
	/// else 2 for one of the three items its tab's ring of the recently researched holds
	/// (<see cref="ParkResearchedRings.IsRecent"/>), else 0. The ring is fed only by a project finishing, never by
	/// building. The game's own help row 146 names both halves: "sort the list by items already owned or recently
	/// researched".
	/// </summary>
	internal static int StateOf( ParkItemCatalogue.Item item, ParkState? state )
	{
		if ( state is null )
			return 0;

		foreach ( var placed in state.Objects )
		{
			if ( placed.CatalogueId == item.Id && placed.IsPlaced )
				return 1;
		}

		return state.RecentlyResearched.IsRecent( item.UiType, item.Id ) ? 2 : 0;
	}

	private static int StateOf( ParkItemCatalogue.Item item ) => StateOf( item, Level.Current?.ParkState );

	/// <summary>
	/// A row was chosen (<c>BuyScreen_OnMessage</c> <c>0x004ac270</c>, message <c>0x400</c>). The original checks the
	/// price against the balance, or a mystery ride's ticket cost against the tickets left with the bank not asked, and,
	/// if it passes, puts the item in the player's hand and closes the screen - neither is taken until the thing is
	/// actually placed.
	/// </summary>
	/// <remarks>
	/// <b>So does this.</b> <c>ParkBuilding.Carry</c> makes the same two tests and puts the item in the
	/// hand, and <c>Level.ClickWorldAt</c> puts it down through <c>ParkBuilding.PlaceCarried</c> where
	/// the park is next clicked. While it is carried its footprint is drawn as the original's
	/// (<c>ParkBuilding.CarrySquares</c>).
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

		// Into the hand, and the screen closes behind it - which is what the original does, and why the money is not
		// taken here: it is taken when the thing is actually put down. A mystery ride asks the tickets, not the bank.
		// Refused either way, the screen stays and the refusal sounds (0x00486ae0).
		Log.Info( "Buy screen: " + ParkBuilding.Carry( item.Id, out var held ) );

		if ( !held )
		{
			Log.Info( $"Buy screen: cat_ui 0x{RefusedSound:x} for the refusal, {(UiSounds.Interface( RefusedSound ) ? "a voice" : "no voice")}" );
			return;
		}

		Stack.Close( this );

		// And clicking the park puts it down - Level.WorldClick, which takes the click only when the
		// interface did not. `put <x> <y>` still does it from the console, for a test that cannot move
		// the pointer.
	}

	/// <summary>The click's refusal, <c>cat_ui</c> <c>0x1D</c> played flat (<c>0x00486ae0</c>).</summary>
	internal const int RefusedSound = 0x1d;

	/// <summary>How often the screen's own timer ticks, in real milliseconds (<c>0x80080</c>, armed at <c>0x004ac3ee</c>).</summary>
	internal const long TickEvery = 1000;

	private readonly UiTimer _tick = new( TickEvery );

	/// <summary>
	/// The timer's tick, the screen's message <c>0x10</c> (<c>0x004ac2db</c>): the list's rows are drawn again
	/// (<c>FUN_00663324</c> on <c>0x1f8</c>) and the balance is written. No other arm of the screen's handler writes
	/// it, so a change in the park's money shows there on the next tick, the first a second after the screen opens.
	/// </summary>
	private void Tick()
	{
		_list.Refresh();
		_money.Text = MoneyText();

		Log.Info( $"Buy list: cells {_list.CellsCensus()}" );
	}

	/// <summary>
	/// What the park has, as control <c>0x200</c> letters it: UITEXT <c>0x1ca</c> and the balance, or <c>0x1cb</c>
	/// and the balance without its sign when the park owes (<c>0x004ac31e</c>).
	/// </summary>
	internal static string? MoneyText()
		=> Level.Current?.ParkState is { } state ? MoneyText( state.Balance ) : null;

	/// <inheritdoc cref="MoneyText()"/>
	internal static string MoneyText( long balance )
		=> Localization.Get( balance < 0 ? UIStrings.CashNegativeDollar : UIStrings.CashDollar ) + Math.Abs( balance );

	/// <summary>What the corner shows now, for the console.</summary>
	internal string MoneyCensus() => $"money \"{_money.Text}\"";

	/// <summary>
	/// The list's selection moved to a row - the handler's <c>0x401</c> arm (<c>0x004aca16</c>): a row other than the
	/// one waiting takes its place and starts the wait again.
	/// </summary>
	/// <remarks>
	/// The wait is milliseconds of real time (<c>FUN_0065968e</c>, <see cref="Time.WallMilliseconds"/>), as the
	/// interface's click limits are. The original's list also selects its first row as
	/// it is filled, so there the panel shows the top row half a second after the screen or a tab opens; here a row is
	/// shown only once the pointer has been over one (<c>UiList.FirstRow</c>).
	/// </remarks>
	internal void RowSelected( int rowId )
	{
		// The help bar's item, whether or not the row waits anew (0x004aca3e).
		_helpItem = rowId;

		if ( rowId == _pending )
			return;

		_pending = rowId;
		_pendingSince = Time.WallMilliseconds;
	}

	/// <summary>
	/// Fills the panel for a row - the footprint picture's half of <c>FUN_004ab1b0</c>. A land row and a mystery ride
	/// clear the picture (<c>0x004ab4c8</c>); any other item's shape is painted.
	/// </summary>
	private void Preview( int rowId )
	{
		Previewed = rowId;

		_footprint.Shape = rowId > 0 && Level.Current?.Catalogue is { } catalogue && catalogue.TryGet( rowId, out var item )
			&& !GoldenTickets.IsMystery( item )
				? item.Shape
				: null;
	}

	/// <summary>What the panel shows and what waits, for the console.</summary>
	internal string PreviewCensus()
		=> $"row {Previewed} shown, {(_pending != 0 ? $"row {_pending} waiting {Time.WallMilliseconds - _pendingSince} ms" : "none waiting")}, "
			+ $"list selection {_list.Selected}: {_footprint.Census()}";

	/// <summary>
	/// The item the help bar speaks of: the row the list last selected, by id, nought before any - the original's
	/// <c>[0x007cc1e8]</c>, which the opener noughts (<c>0x004acd7f</c>) and the <c>0x401</c> arm sets.
	/// </summary>
	private int _helpItem;

	/// <summary>The item the bar last spoke of, nought before any - <c>[0x007cc1e0]</c>, noughted by the opener.</summary>
	private int _helpPosted;

	/// <summary>
	/// The state the bar last spoke of, or <see cref="HelpOffList"/> - <c>[0x0075513c]</c>, a global that starts at -1 and
	/// that no opener resets.
	/// </summary>
	private static int _helpState = HelpOffList;

	/// <summary>The pointer is off the list's row area: nothing is posted, and the next item on it is.</summary>
	internal const int HelpOffList = -1;

	/// <summary>A mystery ride the player's golden tickets buy: UIHELPTEXT <c>0x97</c>, "Left-click to buy this item".</summary>
	internal const int HelpMysteryBought = 0;

	/// <summary>A mystery ride they do not: <c>0x98</c>, "You don't have enough golden tickets to buy this item!".</summary>
	internal const int HelpMysteryShort = 1;

	/// <summary>An item dearer than the bank: <c>0x96</c>, its kind's name and "You can't afford to buy this item!".</summary>
	internal const int HelpDear = 2;

	/// <summary>An item the bank buys: <c>0x94</c>, its kind's name and "Left-click to buy".</summary>
	internal const int HelpToBuy = 3;

	/// <summary>A land row: <c>0x21a</c> for Buy Land, <c>0x21b</c> for Clear Land.</summary>
	internal const int HelpLand = 4;

	/// <summary>
	/// The state an item's row is spoken of in (<c>FUN_004abc80</c>, <c>0x004abd6e</c> to <c>0x004abdf2</c>): a mystery
	/// ride by the tickets the player has left (<c>FUN_004d4b90</c>: the cost at most what <c>FUN_005af5d0</c> counts),
	/// any other item by its price against the bank, a price equal to the bank buying it.
	/// </summary>
	/// <remarks>A deviation, the list's own: the price is the item file's, where the original reads its control record's <c>+0x04</c>.</remarks>
	internal static int HelpStateOf( ParkItemCatalogue.Item item, long balance, IReadOnlyCollection<ushort>? unlocked, int tickets )
		=> GoldenTickets.IsMystery( item, unlocked )
			? GoldenTickets.CanBuyRide( item, tickets ) ? HelpMysteryBought : HelpMysteryShort
			: item.BuildPrice <= balance ? HelpToBuy : HelpDear;

	/// <summary>
	/// The frame message's last arm (<c>0x004ac465</c>): with an item selected, <c>FUN_004abc80</c> posts its row while the
	/// pointer is on the list's region (<c>0x1f8</c>'s <c>+0x2c</c>, <c>FUN_00659e83</c>), which is its row area: the
	/// original's state went to -1 over its tabs and headings and over the strips beside the rows (<c>q495/orig/</c>).
	/// </summary>
	/// <remarks>
	/// The original's list picks its first row as it fills, so there the bar speaks of that row as soon as the pointer
	/// comes onto the rows; this list picks none (<c>LIST_FIRST_ROW_SELECTED</c>, counted as it fills), so nothing is
	/// posted before the pointer has selected a row, on opening and after each tab change.
	/// </remarks>
	protected internal override (int Row, (int Parameter, string Value)[] Values)? HelpPost()
	{
		if ( _helpItem == 0 )
			return null;

		var mouse = Input.Mouse.Position;

		var rows = VirtualScreen.ToPixels( _list.RowArea, _list.Anchor, _list.VerticalAnchor );

		return HelpPost( rows.Contains( mouse.X, mouse.Y ), Level.Current?.ParkState?.Balance ?? 0,
			GoldenTickets.Rides, GoldenTickets.TicketsLeft );
	}

	/// <summary>
	/// <c>FUN_004abc80</c>: posted only on a change of the item or of its state, so a row stands under whatever posts after
	/// it until one of the two moves. Off the list nothing is posted and the state is forgotten, so the item is spoken of
	/// again as the pointer comes back; a land row is posted once for as long as it is the item, wherever the pointer goes.
	/// </summary>
	internal (int Row, (int Parameter, string Value)[] Values)? HelpPost( bool onList, long balance, IReadOnlyCollection<ushort>? unlocked,
		int tickets )
	{
		if ( !onList )
		{
			_helpState = HelpOffList;
			return null;
		}

		if ( _helpItem < 0 )
		{
			if ( _helpPosted == _helpItem )
				return null;

			_helpState = HelpLand;
			_helpPosted = _helpItem;

			Log.Info( $"Buy screen: help for land row {_helpItem}, state {HelpLand}" );

			return (_helpItem == BuyLandRow ? 0x21a : 0x21b, []);
		}

		if ( Level.Current?.Catalogue is not { } catalogue || !catalogue.TryGet( _helpItem, out var item ) )
			return null;

		var state = HelpStateOf( item, balance, unlocked, tickets );

		if ( state == _helpState && _helpPosted == _helpItem )
			return null;

		_helpPosted = _helpItem;
		_helpState = state;

		Log.Info( $"Buy screen: help for {item.Id} {item.Name}, state {state}" );

		return state switch
		{
			HelpMysteryBought => (0x97, []),
			HelpMysteryShort => (0x98, []),
			HelpDear => (0x96, [(0, TypeNameOf( item ))]),
			_ => (0x94, [(0, TypeNameOf( item ))])
		};
	}

	/// <summary>The kinds' names, the table the boot loads as <c>mObjectTypeNames</c> (table <c>0x3f9</c>, <c>[0x0078bad0]</c>).</summary>
	internal const string TypeTable = "Language/English/ITEMTYPES.str";

	private static StringFile? _typeNames;

	/// <summary>
	/// What the help row names an item by (<c>0x004abe7f</c>): row <see cref="ParkItemCatalogue.Item.RideTypeStringIndex"/>
	/// of <c>ITEMTYPES.str</c>, the kind and not the item's own name, so the Belly Bounce is "Bouncy Castle" and a shop,
	/// whose row is nought, is nothing. The row's text follows it with no space ("Bouncy CastleLeft-click to buy"), as the
	/// original's does.
	/// </summary>
	internal static string TypeNameOf( ParkItemCatalogue.Item item )
	{
		_typeNames ??= new StringFile( TypeTable );

		var row = item.RideTypeStringIndex;

		return row >= 0 && row < _typeNames.Entries.Length ? _typeNames[row] : string.Empty;
	}

	/// <summary>What the help bar's poster holds, for the console.</summary>
	internal string HelpCensus() => $"help item {_helpItem} posted {_helpPosted} state {_helpState}";

	protected internal override void Update()
	{
		if ( _tick.Owed( Stack.HoldsTimers ) > 0 )
			Tick();

		// The frame message's arm (0x004ac42e): a row that has waited long enough is shown, and waits no longer.
		if ( _pending != 0 && Time.WallMilliseconds - _pendingSince > PreviewDelay )
		{
			Preview( _pending );
			_pending = 0;
		}
	}
}
