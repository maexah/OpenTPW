namespace OpenTPW.UI;

/// <summary>
/// The shop's window: one shop's figures, its three settings and its verbs - one of the nine object windows
/// (<c>docs/exe/hud.md</c>, "The shop's window").
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_004b0e30</c> builds it on the shared base <c>FUN_0048cea0</c> from the
/// stream at <c>0x00755908</c>, with the handler <c>FUN_004b0b30</c>; every rect, id, mesh and help row here is that
/// stream's. Its vtable is <c>0x006ffc68</c>: the open <c>0x004b0640</c> (ranges, readouts, the arrows hidden), the
/// labels <c>0x004afc10</c>, the load <c>0x004b0950</c>, the refresh <c>0x004b01d0</c> and the apply <c>0x004b0aa0</c>.
/// It is a park screen, not modal, and does not pause.
/// </para>
///
/// <para>
/// <b>The three settings are buffered</b> in the window - quality as 0 to 2, the amount of the special ingredient 0 to
/// 100, the price 0 to 500 - and written to the shop only as the window closes, either arrow steps to another shop or
/// apply to all writes every shop of the same item. The cost of goods row follows the buffered quality and amount at
/// once, so a moved slider shows a cost the sales do not book yet.
/// </para>
///
/// <para>
/// <b>Counted, not built:</b> the title's rename box (<c>RENAME_OBJECT</c>, Q28) and the age-bucket scrap percentage
/// (<c>SCRAP_VALUE_DEPRECIATION</c>: the row shows the build price). The cursor Up and Down keys step it as its arrows do
/// (<c>0x0048d200</c>, <see cref="ParkFrontEnd.ObjectWindowKeys"/>).
/// </para>
/// </summary>
internal sealed class ParkShopWindow : UiWindow, IStatusBox, IObjectWindowSteps
{
	/// <summary>The special ingredients' words, the boot's table <c>INGREDIENT</c> (<c>[0x0078ba8c]</c>).</summary>
	internal const string IngredientTable = "Language/English/INGREDIENT.str";

	/// <summary>How often the figures are filled again, in milliseconds of real time: timer <c>0x80080</c>, armed with 4000 on message <c>0x15</c>.</summary>
	private const long RefreshEvery = 4000;

	/// <summary>How many finished days the month's rows add up (<c>0x1e</c>, <c>0x004b0211</c>).</summary>
	private const int MonthDays = 30;

	/// <summary>Quality's slider runs 0 to 2 (<c>0x004b06b7</c>) and is the shop's <c>mQualityOfGoods</c> over 50 (<c>0x004b09d0</c>).</summary>
	internal const int QualityStep = 50;

	/// <summary>The amount's slider, 0 to 100 (<c>0x004b06d2</c>).</summary>
	private const int MostIngredient = 100;

	/// <summary>The price's spinner, 0 to 500 in steps of 1 (<c>0x004b0735</c>, <c>0x004b0749</c>).</summary>
	private const int HighestPrice = 500;

	/// <summary>The three quality icons over the slider: the type-13 control <c>0xc07f</c>'s op 0x10 children 0, 1 and 2.</summary>
	private static readonly (UiRect Rect, string Name)[] QualityIcons =
	[
		(new UiRect( 355, 619, 390, 654 ), "l_low"),
		(new UiRect( 517, 619, 552, 654 ), "l_med"),
		(new UiRect( 677, 619, 712, 654 ), "l_high"),
	];

	/// <summary>
	/// The stats table, a row a figure: the label's cell and its UITEXT row (<c>0x004afc10</c>), and the figure's cell.
	/// Customer satisfaction and local happiness are type-9 bars; the rest are text.
	/// </summary>
	private static readonly (int Label, UiRect LabelRect, UIStrings Text, int Value, UiRect ValueRect)[] Rows =
	[
		(0xc075, new UiRect( 876, 189, 1273, 234 ), UIStrings.CustomersLastMonth, 0xc070, new UiRect( 1315, 189, 1550, 234 )),
		(0xc071, new UiRect( 876, 240, 1273, 285 ), UIStrings.CostOfGoods, 0xc076, new UiRect( 1315, 240, 1550, 285 )),
		(0xc072, new UiRect( 876, 291, 1273, 336 ), UIStrings.TakingsLastMonth, 0xc077, new UiRect( 1315, 291, 1550, 336 )),
		(0xc073, new UiRect( 876, 342, 1273, 387 ), UIStrings.ProfitLastMonth, 0xc078, new UiRect( 1315, 342, 1550, 387 )),
		(0xc074, new UiRect( 876, 394, 1273, 438 ), UIStrings.CustomerSatisfaction, 0xc079, new UiRect( 1315, 394, 1550, 438 )),
		(0xc07a, new UiRect( 876, 445, 1273, 490 ), UIStrings.ShopScrapValue, 0xc07d, new UiRect( 1315, 445, 1550, 490 )),
		(0xc07b, new UiRect( 876, 496, 1273, 541 ), UIStrings.LocalHappiness, 0xc07c, new UiRect( 1315, 496, 1550, 541 )),
	];

	/// <summary>The bars' inset from their row's top and bottom (<c>FUN_00485e30( bar, 0, 10 )</c>).</summary>
	private const int BarInset = 10;

	private static StringFile? _ingredients;

	private readonly UiTimer _refresh = new( RefreshEvery );

	private readonly UiControl _title;
	private readonly UiControl _preview;
	private readonly UiControl _status;
	private readonly Dictionary<int, UiControl> _figures = [];
	private readonly ParkObjectWindow.UiStatBar _satisfaction;
	private readonly ParkObjectWindow.UiStatBar _localHappiness;
	private readonly UiSlider _quality;
	private readonly UiSlider _amount;
	private readonly UiControl _amountLabel;
	private readonly UiControl _price;
	private readonly UiQualityIcon[] _icons = new UiQualityIcon[3];
	private readonly UiButton _door;
	private readonly UiButton _next;
	private readonly UiButton _back;

	/// <summary>The buffered settings: the window's <c>+0x24</c>, <c>+0x28</c> and <c>+0x2c</c>.</summary>
	private int _pendingQuality, _pendingAmount, _pendingPrice;

	/// <summary>The status <c>FUN_00485f60( shop, 2 )</c> last answered, kept as the window's <c>+8</c>.</summary>
	private int _shownStatus = -1;

	/// <summary>The model turning in the preview, and the thing it was built for.</summary>
	private ParkObjectPreview? _model;
	private int? _previewOf;

	/// <summary>The park whose advisor was told of the window, which is the one told it is gone.</summary>
	private ParkState? _told;

	/// <summary>The shop shown - the original's <c>[0x007c2658]</c>.</summary>
	internal int ThingId { get; private set; }

	private ParkShopWindow( WindowStack stack, int thingId ) : base( stack )
	{
		ParkScreen = true;
		Modal = false;
		Pauses = false;
		ThingId = thingId;

		Root = new UiControl
		{
			Id = 0xc066,
			Rect = new UiRect( 248, 30, 1800, 1007 ),

			// The node "window2", in w_med.MD2.
			Mesh = UiMesh.Get( "w_med" )
		};

		// b_allthings: this window closed, the all-items list opened on its shops tab with this shop's row picked
		// (FUN_00495aa0( 1, shop )).
		Root.Add( new UiButton
		{
			Id = 0xc067,
			Rect = new UiRect( 1648, 575, 1750, 677 ),
			HelpText = 34,
			Mesh = UiMesh.Get( "b_allthings" ),
			Clicked = AllShops
		} );

		Root.Add( new UiButton
		{
			Id = 0xc068,
			Rect = new UiRect( 300, 800, 454, 953 ),
			HelpText = 33,
			Mesh = UiMesh.Get( "b_erase" ),
			Clicked = Delete
		} );

		// b_ardown is FUN_0048cbe0 and b_arup FUN_0048caf0 on the shops' mask 0x200, the other way round from the ride's.
		_next = Root.Add( new UiButton
		{
			Id = 0xc069,
			Rect = new UiRect( 1660, 222, 1743, 306 ),
			HelpText = 35,
			Mesh = UiMesh.Get( "b_ardown" ),
			Clicked = () => Cycle( newer: true )
		} );

		// The close button's negative id; the handler closes on anything in -2..-1.
		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1671, 820, 1754, 903 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		_back = Root.Add( new UiButton
		{
			Id = 0xc06a,
			Rect = new UiRect( 1671, 134, 1754, 217 ),
			HelpText = 36,
			Mesh = UiMesh.Get( "b_arup" ),
			Clicked = () => Cycle( newer: false )
		} );

		// No op 3 in the stream, so each slider's track is its own rect.
		_quality = Root.Add( new UiSlider
		{
			Id = 0xc06b,
			Rect = new UiRect( 318, 609, 743, 785 ),
			HitRect = new UiRect( 318, 609, 743, 785 ),
			Track = new UiRect( 318, 609, 743, 785 ),
			HelpText = 24,
			Mesh = UiMesh.Get( "slider_q" ),
			Moved = QualityMoved
		} );

		_quality.AddThumb( new UiSliderThumb { Id = 3, Rect = new UiRect( 497, 657, 564, 725 ), Mesh = UiMesh.Get( "b_scrollera" ) } );
		_quality.SetRange( 0, 2 );

		_preview = Root.Add( new UiControl
		{
			Id = 0xc06c,
			Rect = new UiRect( 348, 172, 762, 586 ),
			Mesh = UiMesh.Get( "!frame" ),

			// The base's handler for the preview, 0x0048d1a0: either button takes the camera to the shop and closes.
			LeftClicked = GoToShop,
			RightClicked = GoToShop
		} );

		// The status box (0x004afaa0): hidden at first, lettered in font 8, black, in the middle.
		_status = _preview.Add( new UiControl
		{
			Id = 0xc06d,
			Visible = false,
			Rect = new UiRect( 290, 286, 819, 472 ),
			Mesh = UiMesh.Get( "f_chev" ),
			TextRect = new UiRect( 319, 314, 789, 446 ),
			Font = 8,
			TextColour = UiColour.Black,
			PinAcross = Anchor.Centre,
			PinDown = VerticalAnchor.Middle
		} );

		// The load's FUN_0048d290 letters it through UI_SetTitle: font 5 in (234, 239, 102), with its shadow, centred in the
		// stream's rect (749 to 1221) widened by half its width either side.
		_title = Root.Add( new UiControl
		{
			Id = 0xc06e,
			Rect = new UiRect( 513, 74, 1457, 153 ),
			HelpText = 22,
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,

			// The title's rename box (FUN_0048d2e0, given by FUN_0048d290): Q28.
			LeftClicked = static () => Unimplemented.Report( "RENAME_OBJECT" )
		} );

		var stats = Root.Add( new UiControl
		{
			Id = 0xc06f,
			Rect = new UiRect( 853, 172, 1565, 562 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		foreach ( var (label, labelRect, text, value, valueRect) in Rows )
		{
			stats.Add( new UiControl
			{
				Id = label,
				Rect = labelRect,
				HelpText = label == 0xc07b ? 23 : -1,
				Text = Localization.Get( text ),
				Font = 6,
				TextColour = UiColour.White,
				TextAcross = TextAlign.Start
			} );

			if ( value is 0xc079 or 0xc07c )
				continue;

			_figures[value] = stats.Add( new UiControl
			{
				Id = value,
				Rect = valueRect,
				Font = 6,
				TextColour = UiColour.White,
				TextAcross = TextAlign.Start
			} );
		}

		// Satisfaction in ridestatbar.wct from 50 (0x004b00ec), local happiness in happygrad.wct from 100 (0x004b01aa).
		_satisfaction = stats.Add( new ParkObjectWindow.UiStatBar
		{
			Id = 0xc079,
			Rect = new UiRect( 1315, 394 + BarInset, 1550, 438 - BarInset ),
			Known = true,
			Units = 0x32
		} );

		_localHappiness = stats.Add( new ParkObjectWindow.UiStatBar
		{
			Id = 0xc07c,
			Rect = new UiRect( 1315, 496 + BarInset, 1550, 541 - BarInset ),
			HelpText = 23,
			Skin = ParkObjectWindow.UiStatBar.HappyGrad,
			Known = true,
			Units = 100
		} );

		// The readout under the quality slider: UITEXT 0x27, black, in the middle (0x004b0782).
		Root.Add( new UiControl
		{
			Id = 0xc07e,
			Rect = new UiRect( 345, 723, 724, 768 ),
			Text = Localization.Get( UIStrings.QualityOfGoods ),
			Font = 6,
			TextColour = UiColour.Black
		} );

		for ( var i = 0; i < QualityIcons.Length; ++i )
		{
			var (rect, name) = QualityIcons[i];
			_icons[i] = Root.Add( new UiQualityIcon( name ) { Id = 0x11100000 | i, Rect = rect } );
		}

		Root.Add( new UiButton
		{
			Id = 0xc080,
			Rect = new UiRect( 459, 800, 612, 953 ),
			HelpText = 32,
			Mesh = UiMesh.Get( "b_move" ),
			Clicked = Move
		} );

		Root.Add( new UiButton
		{
			Id = 0xc081,
			Rect = new UiRect( 1271, 800, 1425, 953 ),
			HelpText = 536,
			Mesh = UiMesh.Get( "b_applyall" ),
			Clicked = ApplyToAll
		} );

		// b_door, a switch (flag 0x10): help 31 while open, its op 0x12 row 30 while shut.
		_door = Root.Add( new UiButton
		{
			Id = 0xc082,
			Rect = new UiRect( 1430, 784, 1635, 953 ),
			HelpText = 31,
			Toggles = true,
			Mesh = UiMesh.Get( "b_door" ),
			Clicked = Door
		} );

		// The price's spinner (type 12): f_varibox, lettered as money, black, in the middle (0x004b06dc).
		_price = Root.Add( new UiControl
		{
			Id = 0xc085,
			Rect = new UiRect( 1188, 608, 1613, 770 ),
			HelpText = 29,
			Mesh = UiMesh.Get( "f_varibox" ),
			TextRect = new UiRect( 1279, 636, 1528, 681 ),
			Font = 6,
			TextColour = UiColour.Black
		} );

		_price.Add( Nudge( new UiRect( 1205, 631, 1265, 691 ), "b_minus", -1 ) );
		_price.Add( Nudge( new UiRect( 1543, 631, 1603, 691 ), "b_plus", 1 ) );

		_amount = Root.Add( new UiSlider
		{
			Id = 0xc086,
			Rect = new UiRect( 753, 608, 1178, 770 ),
			HitRect = new UiRect( 753, 608, 1178, 770 ),
			Track = new UiRect( 753, 608, 1178, 770 ),
			HelpText = 541,
			Mesh = UiMesh.Get( "slider_n" ),
			Moved = AmountMoved
		} );

		_amount.AddThumb( new UiSliderThumb { Id = 3, Rect = new UiRect( 932, 643, 999, 710 ), Mesh = UiMesh.Get( "b_scrollera" ) } );
		_amount.SetRange( 0, MostIngredient );

		// The readout under the price: UITEXT 0x28, black, in the middle (0x004b0892). It and the ingredient's label come
		// before the spinner and the slider in the stream, and the original letters them over their panels: added after.
		Root.Add( new UiControl
		{
			Id = 0xc083,
			Rect = new UiRect( 1210, 706, 1590, 751 ),
			Text = Localization.Get( UIStrings.SalePrice ),
			Font = 6,
			TextColour = UiColour.Black
		} );

		_amountLabel = Root.Add( new UiControl
		{
			Id = 0xc084,
			Rect = new UiRect( 778, 712, 1158, 757 ),
			Font = 6,
			TextColour = UiColour.Black
		} );

		// The open: with fewer than two shops both arrows are hidden (FUN_0048ca00's count, 0x004b066a).
		if ( Siblings().Count < 2 )
		{
			_next.Visible = false;
			_back.Visible = false;
		}

		Show();
	}

	/// <summary>
	/// <c>FUN_004b0e30( shop )</c>: the window built on it where none is up, then the advisor's message <c>0xd1</c>. With
	/// one already up the base (<c>FUN_0048cea0</c>) builds nothing.
	/// </summary>
	internal static void Open( WindowStack stack, int thingId )
	{
		if ( !stack.Windows.Any( window => window is ParkShopWindow ) )
			stack.Open( new ParkShopWindow( stack, thingId ) );

		if ( Level.Current?.ParkState is { AdvisorMessages: { } lines } park )
			lines.Post( ParkAdvisorMessages.ShopWindow, park.GameTick );

		if ( stack.Windows.LastOrDefault( window => window is ParkShopWindow ) is ParkShopWindow up )
			up._told ??= Level.Current?.ParkState;
	}

	private static ParkState? State => Level.Current?.ParkState;

	/// <summary>The shop shown and its item, or false where the park no longer has it.</summary>
	private bool TryShop( out ParkWorld.CatalogueObject shop, out ParkItemCatalogue.Item item )
	{
		item = default!;
		shop = default!;

		return Level.Current is { ParkState: { } state, Catalogue: { } catalogue }
			&& state.TryObject( ThingId, out shop ) && catalogue.TryGet( shop.CatalogueId, out item );
	}

	/// <summary>
	/// The load, vtable <c>+4</c> (<c>0x004b0950</c>): the title, the preview, the figures, the door down for a shut shop,
	/// the three settings from the shop (each also the buffered value, as the setter posts <c>0x800</c>), and the amount's
	/// slider with its label only where the item names a special ingredient.
	/// </summary>
	private void Show()
	{
		if ( !TryShop( out var shop, out var item ) )
		{
			Stack.Close( this );
			return;
		}

		_title.Text = ParkVisitorWindow.NameOfThing( ThingId );

		_door.IsDown = shop.CanLoad == 0;
		_door.HelpText = _door.IsDown ? 30 : 31;

		_quality.SetValue( (shop.QualityOfGoods & 0xff) / QualityStep );
		QualityMoved();

		_amount.SetValue( shop.AmountOfSpecialIngredient & 0xff );
		_pendingAmount = _amount.Value;

		var ingredient = IngredientName( item.SpecialIngredient );
		_amount.Visible = ingredient.Length > 0;
		_amountLabel.Visible = ingredient.Length > 0;
		_amountLabel.Text = ingredient;

		_pendingPrice = shop.PricePerUse;
		LetterPrice();

		_shownStatus = -1;
		Refresh();

		Log.Info( $"Shop window: showing '{_title.Text}' (thing {ThingId}), {Census()}" );
	}

	/// <summary>
	/// <c>FUN_004e2200</c>: the item's <c>SpecialIngredient</c> 1 to 4 as row 0 to 3 of <c>INGREDIENT.str</c>, nothing for
	/// any other value.
	/// </summary>
	internal static string IngredientName( int ingredient )
	{
		if ( ingredient is < 1 or > 4 )
			return string.Empty;

		_ingredients ??= new StringFile( IngredientTable );

		return ingredient - 1 < _ingredients.Entries.Length ? _ingredients[ingredient - 1] : string.Empty;
	}

	/// <summary>
	/// The refresh, vtable <c>+0x14</c> (<c>FUN_004b01d0</c>): the seven figures, then the status box where
	/// <c>FUN_00485f60( shop, 2 )</c> answers something new.
	/// </summary>
	private void Refresh()
	{
		if ( !TryShop( out var shop, out var item ) || State is not { } state )
			return;

		var figures = FiguresOf( state, shop, item, _pendingQuality, _pendingAmount );

		_figures[0xc070].Text = Localization.Format( 460, (25, $"{figures.Customers}"), (26, $"{figures.Asked}") );
		_figures[0xc076].Text = ParkItemsScreen.Money( figures.CostOfGoods );
		_figures[0xc077].Text = ParkItemsScreen.Money( figures.Takings );
		_figures[0xc078].Text = ParkItemsScreen.Money( figures.Profit );
		_figures[0xc07d].Text = ParkItemsScreen.Money( figures.Scrap );
		_satisfaction.Units = figures.SatisfactionUnits;
		_localHappiness.Units = figures.LocalHappinessUnits;

		// FUN_004e2400: the age-bucket scrap percentage times the build price, the percentage not computed here.
		Unimplemented.Report( "SCRAP_VALUE_DEPRECIATION" );

		var status = Level.Current is { } level ? ParkClosedStatus.For( level.Park, shop ) : 0;

		if ( status != _shownStatus )
		{
			_shownStatus = status;
			_status.Visible = status != 0;
			_status.Text = status == 0 ? null : Localization.Text( 365 );
			_status.TextColour = ParkClosedStatus.Colour( status );
		}
	}

	/// <summary>The seven figures as the refresh letters them, in the original's own units for the two bars.</summary>
	internal readonly record struct Figures( int Customers, int Asked, int CostOfGoods, int Takings, int Profit,
		int SatisfactionUnits, int Scrap, int LocalHappinessUnits );

	/// <summary>
	/// <c>FUN_004b01d0</c>'s figures: the customers of the last thirty finished days "of" those plus the walk-aways (UITEXT
	/// 460, C of M); the cost of goods the buffered quality (times 50) and amount would book (<c>FUN_004e1a30</c>); the
	/// thirty days' takings; their profit (<c>FUN_004e1c70</c>); the satisfaction (<c>FUN_004e1e30</c>) as a bar,
	/// <c>(s &lt;&lt; 10) / 100</c>; the scrap value (the build price); and the local happiness (<c>FUN_004cd880( cell, 1 )</c>)
	/// as a bar.
	/// </summary>
	internal static Figures FiguresOf( ParkState state, ParkWorld.CatalogueObject shop, ParkItemCatalogue.Item item,
		int quality, int amount )
	{
		var rings = state.RingsFor( shop.ThingId );
		var customers = rings.Customers.LastDays( MonthDays );
		var asked = unchecked(customers + rings.WalkAways.LastDays( MonthDays ));
		var cost = ParkRideOperation.ShopCostOfGoods( item, shop with
		{
			QualityOfGoods = quality * QualityStep,
			AmountOfSpecialIngredient = amount
		} );

		return new Figures( customers, asked, cost, rings.Takings.LastDays( MonthDays ), rings.ProfitOfLastDays( MonthDays ),
			(rings.SatisfactionOfLastDays( MonthDays ) << 10) / 100, item.BuildPrice,
			(LocalHappiness( state, shop.CellX, shop.CellY, GuestHappiness ) << 10) / 100 );
	}

	/// <summary>
	/// <c>FUN_004cd880( cell, 1 )</c>: the guests standing on the cells one either side of the shop's, across and down,
	/// held to 127 at the far edges and not at the near ones, each one's happiness truncated to its low byte, averaged;
	/// nought with nobody there.
	/// </summary>
	/// <param name="happinessOf">A guest's happiness by thing, or null for a thing that is not a guest (kind 1, <c>0x004cd94e</c>).</param>
	internal static int LocalHappiness( ParkState state, int cellX, int cellY, Func<int, float?> happinessOf )
	{
		var sum = 0;
		var count = 0;

		for ( var x = cellX - 1; x <= Math.Min( cellX + 1, ParkWorld.MapSize - 1 ); ++x )
		{
			for ( var y = cellY - 1; y <= Math.Min( cellY + 1, ParkWorld.MapSize - 1 ); ++y )
			{
				// The packed id y * 128 + x + 1 cut to a word (0x004cd901), which wraps a row at x = -1.
				var cell = ((y * ParkWorld.MapSize) + x + 1) & 0xffff;

				// Cell 0 (a shop at (0,0)) and row -1 (a shop on row 0) are off the map: 0x004cd906 reads their records
				// unchecked, before and past the array; skipped here.
				if ( cell == 0 || cell > ParkWorld.MapSize * ParkWorld.MapSize )
					continue;

				var (atX, atY) = ((cell - 1) % ParkWorld.MapSize, (cell - 1) / ParkWorld.MapSize);

				for ( int standing = state.CellAt( atX, atY ).Occupant; standing != 0; standing = state.NextOnCell( standing ) )
				{
					if ( happinessOf( standing ) is not { } happiness )
						continue;

					sum += (int)happiness & 0xff;
					++count;
				}
			}
		}

		return count == 0 ? 0 : sum / count;
	}

	/// <summary>A guest's happiness in the running park, or null for anyone else.</summary>
	private static float? GuestHappiness( int thingId )
		=> ParkPeople.Current?.Guests is { } guests && guests.TryGetValue( thingId, out var guest ) ? guest.Happiness : null;

	/// <summary>The handler's <c>0x800</c> from <c>0xc06b</c>: the buffered quality, its icon lit (<c>1 &lt;&lt; value</c> to <c>0xc07f</c>), the figures again.</summary>
	private void QualityMoved()
	{
		_pendingQuality = _quality.Value;

		for ( var i = 0; i < _icons.Length; ++i )
			_icons[i].Lit = i == _pendingQuality;

		Refresh();
	}

	/// <summary>The handler's <c>0x800</c> from <c>0xc086</c>: the buffered amount, the figures again.</summary>
	private void AmountMoved()
	{
		_pendingAmount = _amount.Value;
		Refresh();
	}

	/// <summary>The price lettered in its box as money (<c>FUN_00490250</c>).</summary>
	private void LetterPrice() => _price.Text = ParkItemsScreen.Money( _pendingPrice );

	/// <summary>One of the spinner's two buttons (its op 0xf and 0xe children): the price a step, held to 0..500, the figures again.</summary>
	private UiButton Nudge( UiRect rect, string mesh, int by )
		=> new()
		{
			Rect = rect,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () =>
			{
				_pendingPrice = Math.Clamp( _pendingPrice + by, 0, HighestPrice );
				LetterPrice();
				Refresh();
			}
		};

	/// <summary>
	/// The apply, vtable <c>+0x3c</c> (<c>FUN_004b0aa0</c>): the buffered amount to <c>+0x198</c>, the quality times 50 to
	/// <c>+0x18c</c> as a byte and the price to <c>+0x194</c>, on <paramref name="thingId"/>.
	/// </summary>
	private void ApplyTo( int thingId )
	{
		if ( State is not { } state || !state.TryObject( thingId, out var shop ) )
			return;

		var applied = Applied( shop, _pendingQuality, _pendingAmount, _pendingPrice );

		if ( applied == shop )
			return;

		state.ReplaceObject( applied );

		Log.Info( $"Shop window: thing {thingId} set to quality {applied.QualityOfGoods}, amount {applied.AmountOfSpecialIngredient}, price {applied.PricePerUse}" );
	}

	/// <summary>
	/// The three setters as the apply calls them: <c>0x004e21e0</c> the amount as a dword, <c>0x004e2190</c> the quality
	/// index times 50 cut to its low byte, <c>0x004e2770</c> the price as a dword.
	/// </summary>
	internal static ParkWorld.CatalogueObject Applied( ParkWorld.CatalogueObject shop, int quality, int amount, int price )
		=> shop with
		{
			AmountOfSpecialIngredient = amount,
			QualityOfGoods = (quality * QualityStep) & 0xff,
			PricePerUse = price
		};

	/// <summary>
	/// Apply to all's reach (<c>0x00482040</c> on mask <c>0x200</c>, then <c>0x004b0ae0</c>): the placed shops that fit
	/// no ride (<c>+0x284</c> nought) whose item is the shown one's, by thing.
	/// </summary>
	internal static int[] ShopsOfItem( IEnumerable<ParkWorld.CatalogueObject> objects, ParkItemCatalogue catalogue, int catalogueId )
		=> [.. objects
			.Where( placed => placed.IsPlaced && placed.CatalogueId == catalogueId
				&& catalogue.TryGet( placed.CatalogueId, out var item ) && item.UiType == 1 && item.AddOnUpgradesId == 0 )
			.Select( placed => placed.ThingId )];

	/// <summary>
	/// <c>0xc081</c>: <c>FUN_00481bc0( 0x200, 0x004b0ae0 )</c> - the settings to every shop of the shown one's item, itself
	/// among them - then the figures again.
	/// </summary>
	private void ApplyToAll()
	{
		if ( State is not { } state || !state.TryObject( ThingId, out var shown ) )
			return;

		if ( Level.Current?.Catalogue is not { } catalogue )
			return;

		var applied = ShopsOfItem( state.Objects, catalogue, shown.CatalogueId );

		foreach ( var id in applied )
			ApplyTo( id );

		Log.Info( $"Shop window: applied to all {applied.Length} of item {shown.CatalogueId}: things {string.Join( ", ", applied )}" );

		Refresh();
	}

	/// <summary>The shops an arrow walks, mask <c>0x200</c>: <see cref="ParkObjectWindow.Siblings"/> on UI type 1, newest first.</summary>
	private static List<int> Siblings()
		=> Level.Current is { ParkState: { } state, Catalogue: { } catalogue } ? ParkObjectWindow.Siblings( state, catalogue, uiType: 1 ) : [];

	/// <summary>
	/// An arrow (<c>FUN_0048cbe0</c>, <c>FUN_0048caf0</c>): the settings applied first, then the next shop either way
	/// (<see cref="ParkVisitorWindow.Next"/>), loaded where it is another.
	/// </summary>
	private void Cycle( bool newer )
	{
		ApplyTo( ThingId );

		var next = ParkVisitorWindow.Next( Siblings(), ThingId, newer );

		if ( next == 0 || next == ThingId )
			return;

		ThingId = next;
		Show();
	}

	/// <summary>The cursor Up and Down keys' step (<see cref="IObjectWindowSteps"/>): the arrows' own.</summary>
	public void Step( bool newer ) => Cycle( newer );

	/// <summary>
	/// <c>0xc068</c>, <c>FUN_0048cd10</c>: the box of UITEXT 396 first where Confirmations is on (<see
	/// cref="ParkObjectWindow.AskToDelete"/>), then the window closed with no shop shown, so the settings are not applied.
	/// </summary>
	private void Delete()
	{
		ParkObjectWindow.AskToDelete( Stack, ThingId, "Shop window" );
		ThingId = 0;
		Stack.Close( this );
	}

	/// <summary><c>0xc080</c>, <c>FUN_0048cfa0</c>: demolished and taken into the hand, then the window closed.</summary>
	private void Move()
	{
		Log.Info( $"Shop window: {ParkBuilding.PickUp( ThingId )}" );
		Stack.Close( this );
	}

	/// <summary><c>0xc082</c>, <c>FUN_0048ccf0( down )</c>: the shop shut or opened by the switch's new state, then the figures again.</summary>
	private void Door()
	{
		ParkPeople.Current?.SetRideClosed( ThingId, _door.IsDown );
		_door.HelpText = _door.IsDown ? 30 : 31;
		Log.Info( $"Shop window: thing {ThingId} {(_door.IsDown ? "shut" : "opened")}" );
		Refresh();
	}

	/// <summary><c>0xc067</c>: the all-items list on its shops tab with this shop's row picked.</summary>
	private void AllShops()
	{
		var thing = ThingId;
		Stack.Open( new ParkItemsScreen( Stack, openOn: 1, pick: thing ) );
	}

	/// <summary>The preview's click (<c>0x0048d1a0</c>): the camera to the shop, then the window closed.</summary>
	private void GoToShop()
	{
		ParkOrbitCameraMode.GoToThing( ThingId );
		Stack.Close( this );
	}

	/// <summary>Draws the shop's model turning in the preview (<c>0x004afb70</c>, <see cref="ParkObjectPreview"/>), in the overlay pass.</summary>
	internal void DrawPreview()
	{
		if ( _previewOf != ThingId )
		{
			_model?.Delete();
			_model = ParkObjectPreview.For( ThingId );
			_previewOf = ThingId;
		}

		_model?.Draw( _preview.Pixels );
	}

	/// <summary>The preview's line for the debug console, or null with none built.</summary>
	internal string? PreviewCensus => _model?.Census( _preview.Pixels );

	/// <summary>Holds the preview's turn at an angle, or lets it go.</summary>
	internal void HoldPreview( float? angle ) => _model?.Hold( angle );

	/// <summary>The status box over the model, which the overlay pass draws after the HUD.</summary>
	public void DrawStatus() => _status.Draw();

	protected internal override void Update()
	{
		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 )
			return;

		Refresh();
	}

	/// <summary>
	/// The destroy (<c>0x14</c>): the advisor's message <c>0xd1</c> withdrawn, then the base's close applies the buffered
	/// settings to the shop shown.
	/// </summary>
	protected internal override void Closed()
	{
		_told?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.ShopWindow, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

		ApplyTo( ThingId );

		_model?.Delete();
		_model = null;
		_previewOf = null;
	}

	/// <summary>The window, for the debug console's <c>shopwindow</c> and the log.</summary>
	internal string Census()
		=> $"thing {ThingId} title \"{_title.Text}\" customers=\"{_figures[0xc070].Text}\" cost=\"{_figures[0xc076].Text}\""
			+ $" takings=\"{_figures[0xc077].Text}\" profit=\"{_figures[0xc078].Text}\" satisfaction {_satisfaction.Units}"
			+ $" scrap=\"{_figures[0xc07d].Text}\" local {_localHappiness.Units} quality {_pendingQuality} (icon {Array.FindIndex( _icons, icon => icon.Lit )})"
			+ $" amount {_pendingAmount}{(_amount.Visible ? $" \"{_amountLabel.Text}\"" : " hidden")} price \"{_price.Text}\""
			+ $" door {(_door.IsDown ? "shut" : "open")} status {(_status.Visible ? $"\"{_status.Text}\"" : "none")}"
			+ $" arrows {(_next.Visible ? 1 : 0)}";

	/// <summary>
	/// One of the quality slider's three icons (the type-13 control's type-6 children): its <c>off</c> texture, or its
	/// <c>on</c> one while lit (<c>FUN_0066c85a</c> passes each child its bit through <c>FUN_0065d3a3</c>).
	/// </summary>
	/// <remarks>
	/// <b>Drawn as its texture over the icon's rect</b>, not through its mesh: the mesh's state message picks between its
	/// two textures, which the UI's meshes here do not carry. The picture is the same where the mesh is a quad over its rect.
	/// </remarks>
	private sealed class UiQualityIcon( string name ) : UiControl
	{
		private static readonly Dictionary<string, Texture> Loaded = [];

		internal bool Lit { get; set; }

		protected override void OnDraw()
		{
			base.OnDraw();

			var path = $"ui/textures/{name}{(Lit ? "on" : "off")}.wct";

			if ( !Loaded.TryGetValue( path, out var texture ) )
				Loaded[path] = texture = new Texture( path );

			var box = Pixels;
			Material.UI.Set( "Color", texture );

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( new Rectangle( box.X, Screen.Height - box.Y - box.Height, box.Width, box.Height ), new Rectangle( 0f, 0f, 1f, 1f ), Material.UI );
		}
	}
}
