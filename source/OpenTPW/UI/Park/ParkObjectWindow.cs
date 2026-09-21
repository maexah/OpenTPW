namespace OpenTPW.UI;

/// <summary>
/// The window that opens when a placed ride is clicked - the park's per-object management menu.
///
/// <para>
/// <b>There is no single management screen: there are NINE.</b> Nine window classes share one base
/// whose opener is <c>FUN_0048cea0</c>, and clicking a thing runs <c>FUN_00486920( thing )</c>, which
/// switches on the thing's kind byte at <c>+2</c> - 1 a visitor, 4 to 8 the five staff, 3 a placed
/// object - and then, for a placed object, on the item's <c>WhichUIType</c>: 0 a ride, 1 a shop, 2 a
/// sideshow, 3 a feature, which splits again by the flags at <c>+0x32</c> into toilet, staff room and
/// misc. This is the RIDE one, from the layout stream at <c>0x00755150</c> (1536 bytes, walked to a
/// balanced op 5, handler <c>FUN_004af600</c>, builder <c>FUN_004af980</c>). See
/// <c>docs/exe/park-engine.md</c> for the table of all nine.
/// </para>
///
/// <para>
/// <b>It is not modal and it does not pause.</b> Neither the shared opener nor the ride builder asks
/// for one, and the two arrows are there precisely so a player can walk every ride in the park with it
/// open and the park running behind.
/// </para>
///
/// <para>
/// <b>The verbs are the shared base's, and each was identified twice.</b> <c>FUN_0048cd10</c> sets the
/// map tool to <b>0x33</b>, demolish, and applies it at the thing's own cell; <c>FUN_0048cfa0</c>
/// demolishes and then takes the item into the hand with <b>0x3b</b>, move. The meshes agree
/// independently: those two controls wear <c>b_erase</c> and <c>b_move</c>. The cycle pair
/// <c>FUN_0048cbe0</c> / <c>FUN_0048caf0</c> differ only in calling <c>FUN_00483770</c> against
/// <c>FUN_00483740</c> - next and previous of the same class - and wear <c>b_arup</c> and
/// <c>b_ardown</c>.
/// </para>
///
/// <para>
/// <b>What is deliberately not filled in.</b> The stats table and the preview are built and left
/// blank, and counted. Their contents come from the base's own vtable slots (<c>+0xc</c> fills the
/// stats panel, <c>+0x10</c> the preview) against UITEXT rows this decode has not read, and a label
/// invented in English would be content this project does not own. The three sliders are built because
/// they are part of the window's shape, but the original BUFFERS them and commits only when the window
/// closes or either arrow is pressed - capacity and duration byte-wide where speed is a dword - so
/// moving one here changes nothing yet and says so.
/// </para>
/// </summary>
internal sealed class ParkObjectWindow : UiWindow
{
	/// <summary>The bottom row of verbs, left to right, exactly as the stream lays them out.</summary>
	private static readonly (int Id, int Help, string? Mesh, string What)[] Verbs =
	[
		(0x3e2b, 15, "b_erase",    "delete"),
		(0x3e2c, 14, "b_move",     "move"),
		(0x3e2a, 10, "b_track",    "show the track"),
		(0x3e34,  9, "b_queue",    "show the queue"),

		// 0xaaee5929 matches no member stem and no node name inside any of ui.wad's 1202 members - the
		// same shape as the buy and hire screens' root frame. A named gap, not a guess.
		(0x3e37, 16, null,         "the unnamed one"),

		(0x3e36,  8, "b_callmech", "call a mechanic"),
		(0x3e35, 19, "b_upgrade",  "upgrades"),
		(0x3e38, 13, "b_door",     "open or close the ride"),
	];

	/// <summary>Where each verb sits. Kept beside <see cref="Verbs"/> so the two read as one table.</summary>
	private static readonly UiRect[] VerbRects =
	[
		new( 300, 800, 454, 953 ), new( 459, 800, 612, 953 ),
		new( 625, 800, 779, 953 ), new( 784, 800, 938, 953 ),
		new( 956, 800, 1109, 953 ), new( 1114, 800, 1268, 953 ),
		new( 1271, 800, 1425, 953 ), new( 1430, 784, 1635, 953 ),
	];

	/// <summary>
	/// The stats table: seven rows of two columns. The ids interleave because the RIGHT column is a
	/// type-9 bar on four rows and text on three, so the stream opens them in a different order from
	/// the order they appear in.
	/// </summary>
	private static readonly (int Id, UiRect Rect)[] StatCells =
	[
		(0x3e20, new UiRect( 876, 177, 1273, 222 )), (0x3e21, new UiRect( 1315, 177, 1550, 222 )),
		(0x3e1a, new UiRect( 876, 228, 1273, 273 )), (0x3e1b, new UiRect( 1315, 228, 1550, 273 )),
		(0x3e1c, new UiRect( 876, 279, 1273, 324 )), (0x3e16, new UiRect( 1315, 279, 1550, 324 )),
		(0x3e1e, new UiRect( 876, 331, 1273, 375 )), (0x3e18, new UiRect( 1315, 331, 1550, 375 )),
		(0x3e1f, new UiRect( 876, 382, 1273, 427 )), (0x3e19, new UiRect( 1315, 382, 1550, 427 )),
		(0x3e1d, new UiRect( 876, 433, 1273, 478 )), (0x3e17, new UiRect( 1315, 433, 1550, 478 )),
		(0x3e22, new UiRect( 876, 484, 1273, 529 )), (0x3e23, new UiRect( 1315, 484, 1550, 529 )),
	];

	/// <summary>The three sliders: the control's own rect takes the pointer, op 3 is the track.</summary>
	private static readonly (int Id, int Help, UiRect Rect, UiRect Track, UiRect Thumb, string Mesh)[] Sliders =
	[
		(0x3e30, 5, new UiRect( 318, 607, 743, 769 ), new UiRect( 348, 643, 718, 701 ),
			new UiRect( 497, 641, 564, 709 ), "slider_w"),
		(0x3e2d, 6, new UiRect( 753, 607, 1178, 769 ), new UiRect( 784, 643, 1154, 701 ),
			new UiRect( 932, 641, 999, 709 ), "slider_w"),
		(0x3e2f, 7, new UiRect( 1188, 607, 1613, 769 ), new UiRect( 1218, 643, 1588, 701 ),
			new UiRect( 1367, 641, 1434, 709 ), "slider_n"),
	];

	private readonly UiControl _title;

	/// <summary>Which thing the window is showing. The original keeps the same in <c>DAT_007c2658</c>.</summary>
	internal int ThingId { get; private set; }

	public ParkObjectWindow( WindowStack stack, int thingId ) : base( stack )
	{
		// Neither FUN_0048cea0 nor FUN_004af980 asks for a pause, and the cycle arrows only make sense
		// with the park running behind - see the class remarks.
		Modal = false;
		Pauses = false;

		Root = new UiControl
		{
			Id = 0x3e14,
			Rect = new UiRect( 248, 30, 1800, 1007 ),

			// The hash resolves to the NODE name "window2", which lives in w_med.MD2 - the loader opens
			// files, so this is the file. Node and file diverge often enough that seven meshes failed to
			// load on the buy and hire screens before it was measured; see docs/exe/hud.md.
			Mesh = UiMesh.Get( "w_med" )
		};

		_title = Root.Add( new UiControl
		{
			Id = 0x3e28,
			Rect = new UiRect( 746, 74, 1219, 153 ),
			HelpText = 3,
			Font = 5,
			TextColour = UiColour.White
		} );

		var preview = Root.Add( new UiControl
		{
			Id = 0x3e24,
			Rect = new UiRect( 348, 162, 762, 576 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		preview.Add( new UiControl
		{
			Id = 0x3e25,
			Rect = new UiRect( 300, 275, 828, 462 ),
			Mesh = UiMesh.Get( "f_chev" ),
			TextRect = new UiRect( 328, 304, 799, 435 ),
			Font = 6,
			TextColour = UiColour.White,

			// ITS RECT IS WIDER THAN ITS PARENT'S - 300..828 against 348..762 - so UiControl.Anchor does
			// not treat it as sitting inside the panel and it would pin itself to the left third while
			// the window as a whole is centred, drawing 160px out of place on a 1280x720 window. Saying
			// which edge it keeps to is exactly what PinAcross is for.
			PinAcross = Anchor.Centre,
			PinDown = VerticalAnchor.Middle
		} );

		var stats = Root.Add( new UiControl
		{
			Id = 0x3e15,
			Rect = new UiRect( 853, 162, 1565, 549 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		foreach ( var (id, rect) in StatCells )
		{
			stats.Add( new UiControl
			{
				Id = id,
				Rect = rect,
				Font = 6,
				TextColour = UiColour.White,
				TextAcross = TextAlign.Start
			} );
		}

		Root.Add( Arrow( 0x3e26, 21, "b_arup", new UiRect( 1662, 126, 1745, 210 ), forward: true ) );
		Root.Add( Arrow( 0x3e27, 20, "b_ardown", new UiRect( 1662, 216, 1745, 299 ), forward: false ) );

		for ( var i = 0; i < Verbs.Length; ++i )
		{
			var (id, help, mesh, what) = Verbs[i];

			Root.Add( new UiButton
			{
				Id = id,
				Rect = VerbRects[i],
				HelpText = help,

				// Two of them are switches - flag 0x10 in the stream - and b_door carries a SECOND help
				// row in op 0x12, which is the caption for its other position.
				Toggles = id is 0x3e36 or 0x3e38,
				Mesh = mesh is null ? null : UiMesh.Get( mesh ),
				Clicked = () => Verb( id, what )
			} );
		}

		Root.Add( new UiButton
		{
			Id = 0x3e29,
			Rect = new UiRect( 1648, 583, 1750, 686 ),
			HelpText = 17,
			Mesh = UiMesh.Get( "b_allthings" ),
			Clicked = () => Level.Current?.OpenBuyScreen()
		} );

		// The stream gives the close button a NEGATIVE id, and the handler closes on anything in -2..-1.
		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1671, 818, 1754, 901 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		foreach ( var (id, help, rect, track, thumb, mesh) in Sliders )
		{
			var slider = Root.Add( new UiSlider
			{
				Id = id,
				Rect = rect,
				HitRect = rect,
				Track = track,
				HelpText = help,
				Mesh = UiMesh.Get( mesh ),
				Moved = Buffered
			} );

			slider.AddThumb( new UiSliderThumb { Id = 3, Rect = thumb, Mesh = UiMesh.Get( "b_scrollera" ) } );
		}

		Show( thingId );

		// Reached every time the window opens, and none of them answerable from what is decoded: the
		// stats table and the preview are filled by the shared base's own vtable slots against UITEXT
		// rows this has not read, and the sliders commit into the ride's script variables.
		Unimplemented.Report( "RIDE_STATS_PANEL" );
		Unimplemented.Report( "RIDE_PREVIEW_ACTOR" );
	}

	private UiButton Arrow( int id, int help, string mesh, UiRect rect, bool forward )
		=> new()
		{
			Id = id,
			Rect = rect,
			HelpText = help,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () => Cycle( forward )
		};

	/// <summary>
	/// Shows one thing. The original keeps the shown thing in a global and rebuilds the panels from it,
	/// which is why the arrows can move to another without opening a second window.
	/// </summary>
	internal void Show( int thingId )
	{
		ThingId = thingId;

		var name = Level.Current is { } level && level.ParkState is { } state
			&& state.TryObject( thingId, out var placed )
			&& level.Catalogue is { } catalogue && catalogue.TryGet( placed.CatalogueId, out var item )
				? item.Name
				: $"thing {thingId}";

		_title.Text = name;

		// Said out loud so a test can pair what is on screen with what the window thinks it is showing.
		// A region that changed when the arrows were pressed proves something moved, not that the
		// right ride arrived.
		Log.Info( $"Ride window: showing '{name}' (thing {thingId})" );
	}

	/// <summary>
	/// The next or previous thing of the same kind - <c>FUN_0048cbe0</c> and <c>FUN_0048caf0</c>, which
	/// differ only in which way they walk. The original asks a class MASK (a ride is 0x80), and the
	/// nearest thing here is the item's own UI type, which is what the dispatcher keys off as well.
	/// </summary>
	private void Cycle( bool forward )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue )
			return;

		if ( !state.TryObject( ThingId, out var current )
			|| !catalogue.TryGet( current.CatalogueId, out var mine ) )
			return;

		var siblings = new List<int>();

		foreach ( var placed in state.Objects )
		{
			if ( placed.IsPlaced && catalogue.TryGet( placed.CatalogueId, out var item )
				&& item.UiType == mine.UiType )
				siblings.Add( placed.ThingId );
		}

		siblings.Sort();

		var at = siblings.IndexOf( ThingId );

		if ( at < 0 || siblings.Count < 2 )
			return;

		var next = forward
			? (at + 1) % siblings.Count
			: (at - 1 + siblings.Count) % siblings.Count;

		Show( siblings[next] );
	}

	/// <summary>
	/// A slider moved. <b>Nothing is committed here</b>, and that is the original's arrangement rather
	/// than an omission: the three values are buffered and written only when the window closes or
	/// either arrow is pressed - <c>FUN_004aec30</c> with a mask of 1, 2 or 4 - and they land in the
	/// ride's own script variables, which this does not yet write.
	/// </summary>
	private static void Buffered() => Unimplemented.Report( "RIDE_SETTINGS_COMMIT" );

	/// <summary>
	/// One of the bottom row's verbs. Delete and move are built; the rest are counted by name.
	/// </summary>
	private void Verb( int id, string what )
	{
		switch ( id )
		{
			// FUN_0048cd10: demolish at the thing's own cell, then close.
			case 0x3e2b:
				Log.Info( $"Ride window: {ParkBuilding.Sell( ThingId )}" );
				Stack.Close( this );
				return;

			// FUN_0048cfa0: demolish, then take the same item into the hand - the original's move IS
			// those two, which is why nothing is charged in between and why putting it down charges
			// again. Putting it down needs the pointer, which is the already-counted gap.
			case 0x3e2c:
				Move();
				return;

			default:
				Unimplemented.Report( $"RIDE_WINDOW_{what.ToUpperInvariant().Replace( ' ', '_' )}" );
				Log.Info( $"Ride window: '{what}' is not built yet" );
				return;
		}
	}

	private void Move()
	{
		if ( Level.Current?.ParkState is not { } state || !state.TryObject( ThingId, out var placed ) )
			return;

		var item = placed.CatalogueId;

		Log.Info( $"Ride window: {ParkBuilding.Sell( ThingId )}" );
		Log.Info( $"Ride window: {ParkBuilding.Carry( item )}" );

		Unimplemented.Report( "PLACE_BY_POINTING" );

		Stack.Close( this );
	}
}
