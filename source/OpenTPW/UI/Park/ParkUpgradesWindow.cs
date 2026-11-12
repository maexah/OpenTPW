namespace OpenTPW.UI;

/// <summary>
/// The ride window's Buy Upgrades list (<c>FUN_004ae560</c>, stream <c>0x00755750</c>, handler <c>0x004ae430</c>): a title,
/// UITEXT 31, over a list of a name cell and a price cell in font 7, which <see cref="ParkUpgradeList"/> fills and whose
/// choice it judges (<c>docs/exe/ride-operation.md</c>, "The ride window's Buy Upgrades list").
/// </summary>
/// <remarks>
/// <b>A deviation: the layout is not the stream's.</b> Stream <c>0x00755750</c>'s rectangles and meshes are not read
/// (Q1516); this window takes the message box's frame (<c>w_dialog</c>) and lays its title, list and close button inside it
/// in the park screens' style. It opens over the ride window, which stays open behind it, and neither pauses nor shuts the
/// park out: whether the original's does is not read either.
/// </remarks>
internal sealed class ParkUpgradesWindow : UiWindow
{
	private const int RowFont = 7;

	private readonly ParkObjectWindow? _rideWindow;
	private readonly UiList _list;

	/// <summary>The ride the list is for.</summary>
	internal int ThingId { get; }

	public ParkUpgradesWindow( WindowStack stack, int thingId, ParkObjectWindow? rideWindow ) : base( stack )
	{
		ThingId = thingId;
		_rideWindow = rideWindow;
		Modal = false;
		Pauses = false;

		Root = new UiControl
		{
			Id = 0x1f4c,
			Rect = new UiRect( 500, 233, 1516, 830 ),
			Mesh = UiMesh.Get( "w_dialog" ),
			TextSize = UiFonts.ParkScreenLettering
		};

		// UITEXT 31, "Buy Upgrades", on the title control 0x1f4e.
		Root.Add( new UiControl
		{
			Id = 0x1f4e,
			Rect = new UiRect( 600, 270, 1416, 340 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			Text = Localization.Get( UIStrings.BuyUpgrades )
		} );

		_list = Root.Add( new UiList
		{
			Id = 0x1f4d,
			Rect = new UiRect( 600, 350, 1370, 790 ),
			RowArea = new UiRect( 600, 350, 1370, 790 ),
			Columns = [(610, 1100), (1110, 1360)],
			TextColumns = [true, false],
			RowFont = RowFont,
			ColumnAligns = [TextAlign.Start, TextAlign.End],
			RowHeight = UiList.RowHeightOf( RowFont ),
			SelectsUnderPointer = true,
			Activated = Chose
		} );

		// The stream's -2: a 0x100 from it closes the list.
		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1387, 706, 1470, 790 ),
			HelpText = 2,
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Fill();
	}

	/// <summary>The list filled from the ride as it stands (<c>FUN_004ae140</c>), and its rows logged.</summary>
	private void Fill()
	{
		_list.Clear();

		if ( Level.Current is not { ParkState: { } state, Catalogue: { } catalogue, Research: { } research }
			|| !state.TryObject( ThingId, out var ride ) )
		{
			Log.Info( $"Upgrades list: thing {ThingId} is not a placed object of a loaded park" );
			return;
		}

		var rows = ParkUpgradeList.For( ride, catalogue, research, Level.InstantAction );

		foreach ( var row in rows )
		{
			// The message is split into a row a line (0x0067c430 on 0x007558f0, 0x004ae3fa). What it splits on is not read;
			// here it is the text's own line breaks, which UITEXT 27 has one of.
			if ( row.Kind == ParkUpgradeList.Kind.Message )
			{
				foreach ( var line in Lettered( row, catalogue ).Split( '\n' ) )
					_list.Add( new UiList.Row( row.Value, line, 0, Values: [""] ) );

				continue;
			}

			_list.Add( new UiList.Row( row.Value, Lettered( row, catalogue ), row.Price, Values: [ParkBuyScreen.PriceText( row.Price )] ) );
		}

		Log.Info( $"Upgrades list: {Census( ride, rows, catalogue, Level.InstantAction )}; {_list.Rows.Count} list rows, {_list.RowsCensus()}" );
	}

	/// <summary>
	/// A row's name cell: an add-on's item name (<c>0x00412fb0</c>), else its UITEXT row with the level shown as parameter
	/// nought.
	/// </summary>
	internal static string Lettered( ParkUpgradeList.Row row, ParkItemCatalogue catalogue )
		=> row.Kind switch
		{
			ParkUpgradeList.Kind.AddOn => catalogue.TryGet( row.Value, out var item ) ? ParkObjectNames.NameOf( item ) : $"item {row.Value}",
			ParkUpgradeList.Kind.Message => Localization.Text( row.Text ),
			_ => Localization.Format( row.Text, (0, row.Shown.ToString()) )
		};

	/// <summary>The ride's level and state and each row's kind, value, lettering and price, for the log and the console's <c>upgrades</c>.</summary>
	internal static string Census( ParkWorld.CatalogueObject ride, IReadOnlyList<ParkUpgradeList.Row> rows, ParkItemCatalogue catalogue,
		bool instantAction )
		=> $"thing {ride.ThingId} item {ride.CatalogueId} level {(byte)ride.UpgradeLevel} state {ride.State}, "
			+ $"{(instantAction ? "Instant Action" : "Full Simulation")}, {rows.Count} rows: "
			+ string.Join( ", ", rows.Select( row => $"{row.Kind} {row.Value} '{Lettered( row, catalogue ).Replace( "\n", " / " )}' {row.Price}" ) );

	/// <summary>
	/// A row chosen (<c>0x004ae430</c>, message <c>0x400</c>, then <c>FUN_004af480</c>): see <see cref="ParkUpgradeList.Choose"/>.
	/// An add-on goes into the hand for this ride (<c>0x0046c5a0( 4, item )</c>, <c>0x005276d0( 1 )</c>) and the ride window
	/// closes (<c>0x0048cf70</c>), the list with it; a dearer one sounds <c>cat_ui</c> <c>0x1D</c> (<c>0x00486ae0</c>) and the
	/// list stays. A level's order and the cancel close the list; what they do to the ride (<c>FUN_004e0050( 1 )</c> and
	/// <c>( 0 )</c>) is not read through, so both are counted.
	/// </summary>
	private void Chose( int value )
	{
		if ( Level.Current is not { ParkState: { } state, Catalogue: { } catalogue, Research: { } research }
			|| !state.TryObject( ThingId, out var ride ) )
			return;

		var outcome = ParkUpgradeList.Choose( value, ride, catalogue, research, state.Balance );
		Log.Info( $"Upgrades list: thing {ThingId} row {value} with {state.Balance} in the bank: {outcome}" );

		switch ( outcome )
		{
			case ParkUpgradeList.Outcome.Nothing:
				return;

			case ParkUpgradeList.Outcome.Cancel:
				Unimplemented.Report( "RIDE_UPGRADE_CANCEL" );
				Stack.Close( this );
				return;

			case ParkUpgradeList.Outcome.Order:
				Unimplemented.Report( "RIDE_UPGRADE_ORDER" );
				Stack.Close( this );
				return;

			case ParkUpgradeList.Outcome.OrderRefused:
				Stack.Close( this );
				return;

			case ParkUpgradeList.Outcome.TooDear:
				Log.Info( $"Upgrades list: cat_ui 0x{ParkBuyScreen.RefusedSound:x} for the refusal, "
					+ $"{(UiSounds.Interface( ParkBuyScreen.RefusedSound ) ? "a voice" : "no voice")}" );
				return;

			case ParkUpgradeList.Outcome.Carry:
				// ParkBuilding.CarryFor also asks the item's own build price of the bank, as every carry does.
				Log.Info( $"Upgrades list: {ParkBuilding.CarryFor( value, ThingId )}" );
				Stack.Close( this );

				if ( _rideWindow != null )
					Stack.Close( _rideWindow );

				return;
		}
	}
}
