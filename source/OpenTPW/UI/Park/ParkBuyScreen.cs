namespace OpenTPW.UI;

/// <summary>
/// The purchase menu - the screen behind the gadget's <c>b_buy</c> button.
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
/// <b>One thing is deliberately NOT built, for a reason rather than for want of a rect.</b> The stats
/// panel <c>0x1ed</c> and its seven readouts are excitement, reliability and capacity - simulation
/// values this game does not have, the same reason the map screen's overlays are refused. The root's
/// frame is <c>w_big</c>: the stream hashes a model's first NODE name, <c>0xf76e4200</c> is
/// <c>window4</c>, and that node is inside <c>w_big.MD2</c>, the frame five screens share. The row's
/// third column is the original's tick-box, <c>i_boxtick</c> framed from the row state - see
/// <c>UiList.StateMesh</c>.
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

	private static readonly UiColour HeadingColour = new( 255, 239, 0 );

	public ParkBuyScreen( WindowStack stack ) : base( stack )
	{
		// Not modal and not pausing - see the class remarks. Built onto the park's own layer (0x004acd62): the gadget
		// answers beside it, and what a press beside it does is UiWindow.ParkScreen's to say.
		ParkScreen = true;

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
			SelectsUnderPointer = true,
			Activated = Chose,
			SelectionChanged = RowSelected,

			// The screen's handler keeps the list's 0x406.
			SortChanged = word => _sort = word
		} );

		// Handed over while the list is still empty, so it sorts nothing (0x004ad057).
		_list.SetSort( _sort );

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
		Show( _lastTab );

		// The opener letters the corner itself, as the tick does (FUN_004acc70, 0x004acdae).
		_money.Text = MoneyText();
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

		// Each row goes in by the list's sort. A deviation: they are handed over in name order, where the original
		// walks its catalogue (FUN_00412e60), which shows only among rows equal on the sorted column.
		// A deviation: the price is the item file's, where the original shows its control record's +0x04 (0x004ab086),
		// which the record takes from the same key; the two agree in all 50 of the shipped park's records.
		if ( Level.Current is { Catalogue: { } catalogue, Research: { } research } )
		{
			foreach ( var item in Listed( catalogue, research, _tab ) )
			{
				_list.Add( IsMystery( item )
					? new UiList.Row( item.Id, Localization.Get( UIStrings.MysteryRide ), -item.GoldenTicketCost, StateOf( item ) )
					: new UiList.Row( item.Id, item.Name, item.BuildPrice, StateOf( item ) ) );
			}
		}

		// The land tools, which only the features tab carries - see the constants above.
		if ( _tab == ItemDescriptionFile.Feature )
		{
			_list.Add( new UiList.Row( BuyLandRow, Localization.Get( UIStrings.BuyLand ), 0 ) );
			_list.Add( new UiList.Row( ClearLandRow, Localization.Get( UIStrings.ClearLand ), 0 ) );
		}

		LogOrder();
	}

	private void LogOrder() => Log.Info( $"Buy list: tab {_tab}, {_list.Census()}" );

	/// <summary>
	/// The items one tab lists: its kind, and <b>only a researched item</b> (<c>0x004ab023</c>), the park's flag - see
	/// <see cref="ParkResearch"/>.
	/// </summary>
	internal static IEnumerable<ParkItemCatalogue.Item> Listed( ParkItemCatalogue catalogue, ParkResearch research, int tab )
		=> catalogue.All.Where( item => item.UiType == tab && research.IsResearched( item.Id ) )
			.OrderBy( item => item.Name, StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// The mystery row (<c>0x004ab023</c>..<c>0x004ab063</c>): a ride with a golden-ticket cost that the player has not
	/// unlocked is named UITEXT 137 and priced at the ticket cost, negated. The unlocks are the player's, their
	/// gms.dat's ride ids (<c>FUN_004d4b70</c>; <c>docs/exe/hud.md</c>, "The mystery row"); with nobody playing there
	/// are none, so every such ride is a mystery.
	/// </summary>
	internal static bool IsMystery( ParkItemCatalogue.Item item, IReadOnlyCollection<ushort>? unlocked )
		=> item.GoldenTicketCost > 0 && unlocked?.Contains( (ushort)item.Id ) != true;

	/// <inheritdoc cref="IsMystery(ParkItemCatalogue.Item, IReadOnlyCollection{ushort})"/>
	private static bool IsMystery( ParkItemCatalogue.Item item ) => IsMystery( item, Players.Roster.Current?.File.RideIds );

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
	/// <b>So does this.</b> <c>ParkBuilding.Carry</c> makes the same two tests and puts the item in the
	/// hand, and <c>Level.ClickWorldAt</c> puts it down through <c>ParkBuilding.PlaceCarried</c> where
	/// the park is next clicked. The footprint the original draws while it is carried is counted as
	/// <c>CARRY_PREVIEW_MARKERS</c>.
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

		// Placing a mystery ride spends golden tickets and unlocks it rather than taking money (FUN_004db090 ->
		// FUN_004d4ad0); none of that is built, so the row is not taken.
		if ( IsMystery( item ) )
		{
			Unimplemented.Report( "MYSTERY_RIDE_PURCHASE" );
			Log.Info( $"Buy screen: {item.Name} is a mystery ride, and buying one with golden tickets is not built" );
			return;
		}

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
			&& !IsMystery( item )
				? item.Shape
				: null;
	}

	/// <summary>What the panel shows and what waits, for the console.</summary>
	internal string PreviewCensus()
		=> $"row {Previewed} shown, {(_pending != 0 ? $"row {_pending} waiting {Time.WallMilliseconds - _pendingSince} ms" : "none waiting")}, "
			+ $"list selection {_list.Selected}: {_footprint.Census()}";

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
