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
/// <c>FUN_00483740</c> - previous and next of the same class - and wear <c>b_arup</c> and
/// <c>b_ardown</c>.
/// </para>
///
/// <para>
/// <b>What is filled in and what is not.</b> The stats table takes its seven labels and the figures
/// this game can answer (<see cref="FillStats"/>), and the preview shows a fresh instance of the item's
/// <c>P</c> model turning (<see cref="ParkObjectPreview"/>); excitement and reliability are counted instead. The
/// original BUFFERS the three sliders and commits them only when the window closes or either arrow is
/// pressed - capacity and duration byte-wide where speed is a dword - and so does this
/// (<see cref="Commit"/>), all but what the speed word does to the script's waits.
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

		// 0xaaee5929 is the node "b_ride it!" inside b_rideit.MD2 - a name with a space and an
		// exclamation mark, which a token-splitting scan drops, in a model that is refpack-compressed
		// like every ui.wad model.
		//
		// Its handler is FUN_004e15b0( 0 ) followed by a close: the ride's view, entered from this window
		// (docs/exe/hud.md, "Four ways out of camcorder mode"). There is none here, so the button draws and
		// reports itself like the others.
		(0x3e37, 16, "b_rideit",   "ride it"),

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

	/// <summary>
	/// The readout under each slider, in the same order. <c>FUN_004aec30</c> re-letters exactly these
	/// three from the window's own buffered values - which is why the words change as a slider moves,
	/// before anything has been committed to the ride.
	/// </summary>
	private static readonly int[] ReadoutIds = [0x3e33, 0x3e31, 0x3e32];

	private static readonly UiRect[] ReadoutRects =
	[
		new( 346, 711, 725, 753 ), new( 782, 711, 1161, 753 ), new( 1216, 711, 1595, 753 ),
	];

	/// <summary>
	/// How often the figures are filled again while the window is open, in milliseconds of real time - the original's timer
	/// <c>0x80080</c>, armed with 4000 ms as the window is made (<c>0x004af67b</c>) and answered by
	/// <c>FUN_004ade40</c> (<c>0x004af63b</c>).
	/// </summary>
	/// <remarks>
	/// Real time, as a control's timer is (<see cref="UiTimer"/>): held under the game menu, the options screen or
	/// a message box. The door's press refills them too (<c>0x004af871</c>), and the door is not a button here (Q92).
	/// </remarks>
	private const long RefreshEvery = 4000;

	/// <summary>The figures' timer: its first tick is a period after the window is made.</summary>
	private readonly UiTimer _refresh = new( RefreshEvery );

	/// <summary>How many finished days Users last month adds up - <c>MOV EDI,0x1e</c> at <c>0x004ade83</c>.</summary>
	private const int UsersLastMonthDays = 30;

	/// <summary>Speed, capacity and duration - the order <see cref="Sliders"/> is in.</summary>
	private const int Speed = 0;
	private const int Capacity = 1;
	private const int Duration = 2;

	/// <summary>The stats table's cells, by the control id the stream gives each.</summary>
	private readonly Dictionary<int, UiControl> _stats = [];

	/// <summary>The four condition rows, which are gauges rather than text - see <see cref="UiStatBar"/>.</summary>
	private readonly Dictionary<int, UiStatBar> _bars = [];

	/// <summary>
	/// Where the red line's bar runs inside each slider - control <c>0x3e2e</c>, from the stream.
	/// <b>Duration has none</b>: it wears the <c>slider_n</c> mesh with a plus and a minus on its ends,
	/// where speed and capacity wear <c>slider_w</c> and carry this instead.
	/// </summary>
	private static readonly UiRect?[] RedLineRects =
	[
		new UiRect( 378, 619, 692, 635 ),
		new UiRect( 813, 619, 1127, 635 ),
		null,
	];

	private readonly UiRedLine?[] _redLines = new UiRedLine?[3];

	private readonly UiSlider[] _sliders = new UiSlider[3];
	private readonly UiControl[] _readouts = new UiControl[3];

	private readonly UiControl _title;

	/// <summary>The panel the ride is shown spinning in - control <c>0x3e24</c>.</summary>
	private readonly UiControl _preview;

	/// <summary>
	/// The message box's border over the preview - control <c>0x3e25</c>, the yellow-and-black chevron.
	/// Shows the closed status, or the existing broken-ride warning.
	/// </summary>
	private readonly UiControl _broken;

	/// <summary>The ride's door, <c>b_door</c> (<c>0x3e38</c>): down while the ride is closed.</summary>
	private readonly UiButton? _door;

	/// <summary>The model turning in the preview, built when the window first draws a thing and let go when it shows another.</summary>
	private ParkObjectPreview? _model;

	/// <summary>The thing <see cref="_model"/> was last built for, or null before the first.</summary>
	private int? _previewOf;

	/// <summary>Which thing the window is showing. The original keeps the same in <c>DAT_007c2658</c>.</summary>
	internal int ThingId { get; private set; }

	public ParkObjectWindow( WindowStack stack, int thingId ) : base( stack )
	{
		// Neither FUN_0048cea0 nor FUN_004af980 asks for a pause, and the cycle arrows only make sense
		// with the park running behind - see the class remarks.
		Modal = false;
		Pauses = false;

		// Built onto the park's own layer (0x0048ceca) - see UiWindow.ParkScreen.
		ParkScreen = true;

		Root = new UiControl
		{
			Id = 0x3e14,
			Rect = new UiRect( 248, 30, 1800, 1007 ),

			// The hash resolves to the NODE name "window2", which lives in w_med.MD2 - the loader opens
			// files, so this is the file. Node and file often diverge; see docs/exe/hud.md.
			Mesh = UiMesh.Get( "w_med" )
		};

		_title = Root.Add( new UiControl
		{
			Id = 0x3e28,
			Rect = new UiRect( 746, 74, 1219, 153 ),
			HelpText = 3,
			Font = 5,
			TextColour = UiColour.White,

			// A left click on the title opens the thing's rename box (FUN_0048d2e0, the helper every object
			// window's title is given by FUN_0048d290). Renaming is not built (docs/QUEUE.md Q28): counted.
			LeftClicked = static () => Unimplemented.Report( "RENAME_OBJECT" )
		} );

		_preview = Root.Add( new UiControl
		{
			Id = 0x3e24,
			Rect = new UiRect( 348, 162, 762, 576 ),
			Mesh = UiMesh.Get( "!frame" ),

			// A click on the preview, with either button, moves the camera to the window's thing and closes the
			// window: the base's handler for this panel, 0x0048d1a0, answers the click 0x10006 whatever its button.
			LeftClicked = GoToThing,
			RightClicked = GoToThing
		} );

		// FUN_004ad720 starts the status box hidden; ShowTheDoor supplies the current warning.
		_broken = _preview.Add( new UiControl
		{
			Id = 0x3e25,
			Visible = false,
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
			// The four condition rows are GAUGES and the rest are text - see UiStatBar. Text set on a
			// gauge draws nothing, so those four rows would sit blank beside their labels.
			if ( id is 0x3e16 or 0x3e17 or 0x3e18 or 0x3e19 )
			{
				_bars[id] = stats.Add( new UiStatBar { Id = id, Rect = rect } );
				continue;
			}

			_stats[id] = stats.Add( new UiControl
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

			var verb = Root.Add( new UiButton
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

			if ( id == 0x3e38 )
				_door = verb;
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

		for ( var i = 0; i < Sliders.Length; ++i )
		{
			var (id, help, rect, track, thumb, mesh) = Sliders[i];
			var which = i;

			var slider = Root.Add( new UiSlider
			{
				Id = id,
				Rect = rect,
				HitRect = rect,
				Track = track,
				HelpText = help,
				Mesh = UiMesh.Get( mesh ),
				Moved = () => Moved( which )
			} );

			// The red line goes on BEFORE the thumb, so the thumb rides over it rather than under it.
			if ( RedLineRects[i] is { } line )
				_redLines[i] = slider.Add( new UiRedLine { Id = 0x3e2e, Rect = line } );

			slider.AddThumb( new UiSliderThumb { Id = 3, Rect = thumb, Mesh = UiMesh.Get( "b_scrollera" ) } );

			_sliders[i] = slider;

			// BLACK, because these sit on the slider's own bright green bar - white on green is not
			// readable at this size, which is plain the moment the window is looked at rather than
			// measured.
			_readouts[i] = Root.Add( new UiControl
			{
				Id = ReadoutIds[i],
				Rect = ReadoutRects[i],
				Font = 6,
				TextColour = UiColour.Black
			} );
		}

		Show( thingId );
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

		// The sliders and the stats belong to the ride being shown, not to the window, so both are
		// rebuilt every time it changes - which is what makes the cycle arrows work.
		ShowSettings();
		FillStats();
		ShowTheDoor();

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
		// The buffered sliders are written BEFORE moving on - FUN_0048cbe0 and FUN_0048caf0 both call
		// vtable +0x3c first - so the ride you were adjusting keeps what you set it to.
		Commit();

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
	/// Puts the three sliders where the ride has them, with the bounds its item allows.
	/// </summary>
	/// <remarks>
	/// <c>FUN_004af030</c>: each slider takes its range from the ITEM and its value from the THING -
	/// speed from <c>+0x58</c> as a dword, capacity from <c>+0x5d</c> and duration from <c>+0x5c</c> as
	/// bytes. <b>A slider whose item gives it no room hides itself</b>, and the duration one hides
	/// whenever <c>DurationUnit</c> is nought, which is how a coaster says its ride length comes from
	/// its track rather than from a slider.
	/// </remarks>
	private void ShowSettings()
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| !state.TryObject( ThingId, out var placed )
			|| level.Catalogue is not { } catalogue || !catalogue.TryGet( placed.CatalogueId, out var item ) )
			return;

		// EACH SLIDER HIDES ON ITS OWN TERMS, and only where the original hides it. FUN_004af030 hides
		// the capacity slider AND its readout when the item leaves it no room (min == max), and hides
		// the duration one when DurationUnit is nought. It does NEITHER for speed: it sets the range
		// and the value and leaves the control standing, dead, for an item that declares no speed keys
		// - and Lost Kingdom's Belly Bounce is exactly that item. Hiding it would be this project's
		// idea rather than the game's.
		Set( Speed, item.MinSpeed, item.MaxSpeed, placed.OperatingSpeed, hideWhenEmpty: false );
		Set( Capacity, item.MinCapacity, item.MaxCapacity, placed.OperatingCapacity, hideWhenEmpty: true );
		Set( Duration, item.MinDuration, item.MaxDuration, placed.OperatingDuration, hideWhenEmpty: true );

		// Nought is not "no minimum": it means this ride has no duration at all.
		if ( item.DurationUnit == 0 )
			Hide( Duration );

		// And where the red line falls on the two tracks that have one.
		Mark( Speed, item.RedLineSpeed, item.MinSpeed, item.MaxSpeed );
		Mark( Capacity, item.RedLineCapacity, item.MinCapacity, item.MaxCapacity );

		for ( var i = 0; i < _sliders.Length; ++i )
			Letter( i );

		// Said out loud with the RANGES as well as the values, so a test can check both what the ride
		// carries and which bounds its item allowed - and can read a value back after the window has
		// been closed and opened again, which is the only way "it saved" is observable at all. A thumb
		// sitting somewhere new proves a thumb moved.
		Log.Info( $"Ride window: thing {ThingId} settings" +
			$" speed {placed.OperatingSpeed} of {item.MinSpeed}..{item.MaxSpeed}," +
			$" capacity {placed.OperatingCapacity} of {item.MinCapacity}..{item.MaxCapacity}," +
			$" duration {placed.OperatingDuration} of {item.MinDuration}..{item.MaxDuration}," +
			$" unit {item.DurationUnit}" );
	}

	/// <summary>
	/// Fills the stats table - the shared base's vtable <c>+0xc</c> (<c>0x004ad890</c>) for the labels,
	/// and <c>FUN_004ade40</c> for the figures beside them.
	/// </summary>
	/// <remarks>
	/// <b>The pairing is corroborated twice over.</b> The builder gives the seven LEFT cells UITEXT
	/// rows 0x11 to 0x17 in the order 0x3e20, 0x3e1a, 0x3e1c, 0x3e1e, 0x3e1f, 0x3e1d, 0x3e22; the
	/// refresh writes the RIGHT cells of the same rows. Both orders agree, and they agree with the
	/// rectangles the stream lays out, so the table below is read off three sources rather than one.
	/// <para>
	/// <b>Two of the seven figures are not answerable here and are counted rather than invented.</b>
	/// Excitement and reliability are type-9 bars skinned <c>ridestatbar.wct</c> that the engine computes
	/// from the three sliders - <c>FUN_004e0560</c> divides two slider values by per-upgrade maxima this
	/// decode has not read. Users last month sums the object's ring of customers over the last thirty finished
	/// game days (<see cref="ParkObjectRings.Customers"/>).
	/// State of repair and remaining life are bars too, read straight off the thing.
	/// </para>
	/// <para>
	/// The figures are filled when a thing is shown (<c>FUN_004ae430</c>) and again every four seconds while the
	/// window is open (<see cref="RefreshEvery"/>), as the original's are.
	/// </para>
	/// </remarks>
	private void FillStats()
	{
		// label cell, value cell, what the label says.
		(int Label, int Value, UIStrings Text)[] rows =
		[
			(0x3e20, 0x3e21, UIStrings.UsersLastMonth),
			(0x3e1a, 0x3e1b, UIStrings.Age),
			(0x3e1c, 0x3e16, UIStrings.Excitement),
			(0x3e1e, 0x3e18, UIStrings.Reliability),
			(0x3e1f, 0x3e19, UIStrings.StateOfRepair),
			(0x3e1d, 0x3e17, UIStrings.RemainingLife),
			(0x3e22, 0x3e23, UIStrings.ScrapValue),
		];

		foreach ( var (label, _, text) in rows )
		{
			if ( _stats.TryGetValue( label, out var cell ) )
				cell.Text = Localization.Get( text );
		}

		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| !state.TryObject( ThingId, out var placed )
			|| level.Catalogue is not { } catalogue || !catalogue.TryGet( placed.CatalogueId, out var item ) )
			return;

		// How old it is, in whole days on the park's calendar - FUN_004dd670, which FUN_004ade40 prints signed
		// (0x004adf40). The Belly Bounce is 32 days old as Lost Kingdom loads.
		if ( _stats.TryGetValue( 0x3e1b, out var age ) )
			age.Text = placed.Built.IsSet ? $"{state.AgeInDays( placed )}" : null;

		// FUN_004e2400: the age-bucket scrap percentage times the item's build price. That percentage
		// is SCRAP_VALUE_DEPRECIATION, already counted where Sell refunds - it answers 100 only for a thing under
		// thirty days old with no customer yet, so today this is the build price and is wrong once a ride is used or ages.
		if ( _stats.TryGetValue( 0x3e23, out var scrap ) )
			scrap.Text = $"{item.BuildPrice}";

		// The two condition gauges that are fully derived. Both are 0..100 in the save and the
		// original maps them onto a 0..1024 bar with ((v & 0xff) << 10) / 100, so as a proportion
		// they are simply v/100 - see UiStatBar for why these rows are gauges and not text.
		// Named Gauge, not Mark: Mark is the RED LINE helper and takes a slider index, so a stat
		// control id handed to it would have addressed slider 0x3e17 and written somewhere real.
		void Gauge( int id, float percent )
		{
			if ( !_bars.TryGetValue( id, out var bar ) )
				return;

			bar.At = Math.Clamp( percent / 100f, 0f, 1f );
			bar.Known = true;
		}

		Gauge( 0x3e17, placed.RemainingLife );    // +0x48, what a breakdown eats
		Gauge( 0x3e19, placed.StateOfRepair );    // +0x44, what a repair restores

		// Excitement (0x3e16) and Reliability (0x3e18) are NOT left out for want of a control - the
		// gauge above draws them the moment there is a number. FUN_004ade40 fills them from FUN_004e0560 and
		// FUN_004df640 over the window's own slider values (docs/exe/park-engine.md, "The object window's stats
		// panel"). Their inputs are named, and ParkRideScore.ExcitementOf works out the first from the thing's own
		// settings; the bars themselves, over the sliders, and Reliability's FUN_004df450 are not built.
		Unimplemented.Report( "RIDE_EXCITEMENT_BAR" );
		Unimplemented.Report( "RIDE_RELIABILITY_BAR" );

		// Users last month: the object's customers over the last thirty finished days, today's not among them
		// (FUN_004ade40, 0x004ade7d), printed "%d" (0x0048ff8f).
		if ( _stats.TryGetValue( 0x3e21, out var users ) )
			users.Text = $"{state.RingsFor( ThingId ).Customers.LastDays( UsersLastMonthDays )}";

		// The age is written as a bare number, and the original's wording for it is NOT known.
		//
		// FUN_004ade40 formats it through FUN_006acd60( ..., 0x1f, 0x1b1, &record ) with a VARM
		// placeholder, and 0x1b1 = 433 read as a UITEXT row is EMPTY. That is not an off-by-one:
		// sweeping 428..438 in the running game gives "Duration : ", "Duration : ", "Laps : ",
		// "Cycles : ", "Repetitions : " and then nothing, which is the slider-duration family this
		// window already uses - the age wording is not in that neighbourhood at all. So 0x1b1 is
		// something other than a row of UITEXT.str, and until that is decoded a plain number is the
		// honest output. 0x1f is plainly the 31-byte buffer.

		// The two floats are printed RAW, and at full precision, because a gauge cannot report its own
		// input: v/100 clamped to 0..1 draws a full bar for 100, for 1e30 and for anything else past
		// the top, so a wrong offset would look exactly like a healthy ride. The number is the check,
		// not the picture.
		Log.Info( $"Ride window: thing {ThingId} stats age {(placed.Built.IsSet ? state.AgeInDays( placed ) : -1)} days," +
			$" users {users?.Text ?? "-"}, scrap {item.BuildPrice}," +
			$" built {placed.Built.Year}-{placed.Built.Month:D2}-{placed.Built.Day:D2}," +
			$" repair {placed.StateOfRepair:R}, life {placed.RemainingLife:R}" );
	}

	/// <summary>The preview's click (<c>0x0048d1c0</c>): the camera to the thing shown, then the window closed.</summary>
	private void GoToThing()
	{
		ParkOrbitCameraMode.GoToThing( ThingId );
		Stack.Close( this );
	}

	/// <summary>
	/// Draws the thing's preview model, turning, inside the preview panel (<see cref="ParkObjectPreview"/>).
	/// </summary>
	/// <remarks>
	/// <b>Drawn in the overlay pass, not with the interface.</b> <c>Level.Render</c> clears depth and
	/// runs that pass after the HUD, which is how the advisor sits in front of everything; a model
	/// drawn in the HUD's own pass would be flat UI geometry competing with the window frame.
	/// </remarks>
	internal void DrawPreview()
	{
		// A fresh instance for each thing shown, the one before it let go (FUN_00486410). One that
		// would not build is not asked for again every frame.
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

	/// <summary>Holds the preview's turn at an angle, or lets it go: the debug console's.</summary>
	internal void HoldPreview( float? angle ) => _model?.Hold( angle );

	/// <summary>The status box must cover the model, which the overlay pass draws after the HUD.</summary>
	internal void DrawStatus() => _broken.Draw();

	/// <summary>
	/// Puts the red line where the item says it falls, as a fraction of the track.
	/// </summary>
	/// <remarks>
	/// <b>The engine writes <c>(redline &lt;&lt; 10) / (max - min)</c> out of 1024</b>, which reduces
	/// to plain <c>redline / (max - min)</c> - the shift and the divide cancel. The simple form is
	/// written here rather than carrying fixed-point arithmetic that looks meaningful and is not.
	/// <b>Note it does NOT subtract the minimum</b>, which the obvious reading would: for speed's 1..100
	/// that is 0.606 against 0.596, too small to see and a real deviation if it were quietly corrected.
	/// </remarks>
	private void Mark( int which, int redLine, int lowest, int highest )
	{
		if ( _redLines[which] is not { } bar )
			return;

		bar.At = highest > lowest ? Math.Clamp( redLine / (float)(highest - lowest), 0f, 1f ) : 0f;
		bar.Visible = _sliders[which].Visible && bar.At > 0f;
	}

	/// <summary>
	/// The red line along a slider's track - control <c>0x3e2e</c>, which sits inside the speed and
	/// capacity sliders and marks the point past which the ride is being run harder than it should be.
	/// Green below it, red beyond.
	/// </summary>
	/// <remarks>
	/// Drawn rather than skinned because two colours meeting at a movable point is not something a
	/// <see cref="UiMesh"/> frame can express. The solid colours are 1x1 textures, which is how
	/// <c>LoadingScreen</c> already makes one, and the quad goes through <see cref="Material.UI"/>
	/// with the y-flip every other <c>Graphics.Quad</c> caller in this interface uses.
	/// </remarks>
	private sealed class UiRedLine : UiControl
	{
		private static Texture? _green;
		private static Texture? _red;

		/// <summary>How far along the track the line falls, 0 to 1.</summary>
		internal float At { get; set; }

		protected override void OnDraw()
		{
			base.OnDraw();

			if ( At <= 0f )
				return;

			_green ??= new Texture( [0x20, 0xB0, 0x20, 0xFF], 1, 1 );
			_red ??= new Texture( [0xC0, 0x20, 0x20, 0xFF], 1, 1 );

			var box = Pixels;
			var split = box.Width * Math.Clamp( At, 0f, 1f );

			Bar( _green, box.X, box.Y, split, box.Height );
			Bar( _red, box.X + split, box.Y, box.Width - split, box.Height );
		}

		private static void Bar( Texture texture, float x, float y, float width, float height )
		{
			if ( width <= 0f || height <= 0f )
				return;

			Material.UI.Set( "Color", texture );

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( new Rectangle( x, Screen.Height - y - height, width, height ), Material.UI );
		}
	}

	/// <summary>
	/// One of the four condition rows - Excitement <c>0x3e16</c>, Remaining life <c>0x3e17</c>,
	/// Reliability <c>0x3e18</c> and State of repair <c>0x3e19</c>. A proportion of the row filled,
	/// not a number.
	/// </summary>
	/// <remarks>
	/// <b>These are gauges, and treated as text they show nothing.</b> The labels
	/// beside them are text cells and render fine; the value cells are not. <c>FUN_004ade40</c> hands
	/// each of these four <c>((value &amp; 0xff) &lt;&lt; 10) / 100</c> - a 0..100 percentage mapped onto
	/// 0..1024 - through the control's <c>+0x1c</c> entry. Users last month and Scrap value go through the same
	/// entry as a plain number, each to its own painter (<c>0x004adf28</c>, <c>0x004ae0da</c>); only Age is given a
	/// string.
	/// <para>
	/// <b>The colour is chosen, not measured.</b> The original's gauge skin is not something this
	/// project has read; what IS read is the proportion. So the fill is one flat colour rather than a
	/// green-to-red scheme that would state a judgement about the value the game never makes here.
	/// </para>
	/// </remarks>
	private sealed class UiStatBar : UiControl
	{
		private static Texture? _track;
		private static Texture? _fill;

		/// <summary>How much of the row is filled, 0 to 1.</summary>
		internal float At { get; set; }

		/// <summary>Whether a value was set at all - an unfilled bar and a zero one are different things.</summary>
		internal bool Known { get; set; }

		protected override void OnDraw()
		{
			base.OnDraw();

			if ( !Known )
				return;

			_track ??= new Texture( [0x18, 0x20, 0x18, 0xFF], 1, 1 );
			_fill ??= new Texture( [0x30, 0xC0, 0x40, 0xFF], 1, 1 );

			var box = Pixels;

			// Inset so the gauge reads as a bar sitting in the row rather than as a filled cell.
			var inset = box.Height * 0.25f;
			var y = box.Y + inset;
			var height = box.Height - (inset * 2f);
			var filled = box.Width * Math.Clamp( At, 0f, 1f );

			Paint( _track, box.X, y, box.Width, height );
			Paint( _fill, box.X, y, filled, height );
		}

		private static void Paint( Texture texture, float x, float y, float width, float height )
		{
			if ( width <= 0f || height <= 0f )
				return;

			Material.UI.Set( "Color", texture );

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( new Rectangle( x, Screen.Height - y - height, width, height ), Material.UI );
		}
	}

	private void Set( int which, int lowest, int highest, int value, bool hideWhenEmpty )
	{
		var slider = _sliders[which];

		if ( hideWhenEmpty && highest <= lowest )
		{
			Hide( which );
			return;
		}

		slider.Visible = true;
		_readouts[which].Visible = true;

		slider.SetRange( lowest, highest );
		slider.SetValue( value );
	}

	private void Hide( int which )
	{
		_sliders[which].Visible = false;
		_readouts[which].Visible = false;
	}

	/// <summary>
	/// A slider moved. <b>Nothing is committed here</b> - the original buffers all three and writes
	/// them only when the window closes or either arrow is pressed. What does happen is the readout
	/// under it changes, which is <c>FUN_004aec30</c> with a mask of 1, 2 or 4.
	/// </summary>
	private void Moved( int which ) => Letter( which );

	/// <summary>
	/// Re-letters one readout - <c>FUN_004aec30</c>. The wording of the duration one comes from the
	/// item's <c>DurationUnit</c>, and the engine picks a singular row for a value of one and a plural
	/// for anything else.
	/// </summary>
	private void Letter( int which )
	{
		if ( !_readouts[which].Visible )
			return;

		var value = _sliders[which].Value;

		_readouts[which].Text = which switch
		{
			Speed => $"{Localization.Get( UIStrings.Speed )} {value}",
			Capacity => $"{Localization.Get( UIStrings.Capacity )} {value}",
			_ => $"{DurationWord( value )} {value}"
		};
	}

	/// <summary>
	/// What a duration is counted in. <c>FUN_004aec30</c> switches on the item's <c>DurationUnit</c>:
	/// 1 picks UITEXT 428 for a value of one and 429 otherwise, 2 picks Laps, 3 Cycles, 4 Repetitions.
	/// </summary>
	/// <remarks>
	/// <b>429 has no name in <see cref="UIStrings"/></b> - it is the plural of 428 - so it is read by
	/// row rather than given an invented one. See <see cref="Localization.Text"/>.
	/// </remarks>
	private string DurationWord( int value )
	{
		var unit = Level.Current is { } level && level.ParkState is { } state
			&& state.TryObject( ThingId, out var placed )
			&& level.Catalogue is { } catalogue && catalogue.TryGet( placed.CatalogueId, out var item )
				? item.DurationUnit
				: 1;

		return unit switch
		{
			1 => value == 1 ? Localization.Get( UIStrings.Duration ) : Localization.Text( 429 ),
			2 => Localization.Get( UIStrings.Laps ),
			3 => Localization.Get( UIStrings.Cycles ),
			4 => Localization.Get( UIStrings.Repetitions ),
			_ => Localization.Get( UIStrings.Duration )
		};
	}

	/// <summary>
	/// Writes the three buffered values onto the ride - the shared base's vtable <c>+0x3c</c>
	/// (<c>0x004af440</c>), which both cycle buttons call before they move on and the close path calls
	/// on the way out.
	/// </summary>
	/// <remarks>
	/// <b>The original writes each through a setter that clamps again</b> - <c>FUN_004dd6e0</c> for
	/// speed, <c>FUN_004dd7f0</c> for capacity, <c>FUN_004dd720</c> for duration - and the duration one
	/// is skipped entirely when the item has no duration. Each setter writes the thing's own field AND
	/// pushes the value into the running script, which is why a change takes effect on the ride rather
	/// than only on the screen.
	/// </remarks>
	private void Commit()
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| !state.TryObject( ThingId, out var placed )
			|| level.Catalogue is not { } catalogue || !catalogue.TryGet( placed.CatalogueId, out var item ) )
			return;

		var speed = _sliders[Speed].Visible ? _sliders[Speed].Value : placed.OperatingSpeed;
		var capacity = _sliders[Capacity].Visible ? _sliders[Capacity].Value : placed.OperatingCapacity;
		var duration = item.DurationUnit != 0 && _sliders[Duration].Visible
			? _sliders[Duration].Value
			: placed.OperatingDuration;

		if ( speed == placed.OperatingSpeed && capacity == placed.OperatingCapacity
			&& duration == placed.OperatingDuration )
			return;

		state.ReplaceObject( placed with
		{
			OperatingSpeed = speed,
			OperatingCapacity = capacity,
			OperatingDuration = duration
		} );

		// Into the running script as well, which is what makes the ride carry the new number rather
		// than the window remembering it. Capacity and duration are named variables; speed is not.
		if ( ParkRides.Current is { } rides && rides.ScriptFor( ThingId ) is var id and not 0
			&& rides.Scheduler.Find( id ) is { } script )
		{
			script.Set( ParkRideOperation.CapacityVariable, capacity );
			script.Set( ParkRideOperation.DurationVariable, duration );
		}

		// THE SPEED WORD IS A SEPARATE THING and deliberately not written. RideScript.Wait records that
		// the engine divides every wait by 0.5 + 0.01 * speed, worked out afresh per instruction from the
		// word at +0xc0, and leaves that division out. No opcode writes the word; the object constructor
		// does, from outside the script system, and so does this panel: FUN_004dd6e0 writes it from this
		// very slider. So moving speed here would re-time every WAIT in the script,
		// which is a behaviour change too wide to make as a side effect of a slider.
		Unimplemented.Report( "RIDE_SPEED_SCALES_WAITS" );

		Log.Info( $"Ride window: thing {ThingId} set to speed {speed}, capacity {capacity}, duration {duration}" );
	}

	/// <summary>The window closing commits, the same as either arrow does - see <see cref="Commit"/>.</summary>
	protected internal override void Closed()
	{
		Commit();

		_model?.Delete();
		_model = null;
		_previewOf = null;
	}

	/// <summary>
	/// Refreshes the status warning and door from the live ride, including changes to its queue.
	/// </summary>
	/// <remarks>
	/// Read every frame because a ride can break while its window is open - the window does not pause
	/// the game, which is the whole reason its cycle arrows are worth having.
	/// </remarks>
	protected internal override void Update()
	{
		ShowTheDoor();

		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 )
			return;

		FillStats();
	}

	/// <summary>
	/// The door switch follows the ride's <c>mCanLoad</c>: <c>FUN_004ad4e0</c> sets it down for a closed ride
	/// (<c>Button_SetDown</c>, <c>0x004ad606</c>..<c>0x004ad622</c>) whenever the ride's status changes.
	/// </summary>
	private void ShowTheDoor()
	{
		if ( _door == null || Level.Current is not { ParkState: { } state } level
			|| !state.TryObject( ThingId, out var placed ) )
			return;

		var closed = placed.CanLoad == 0;
		var trackType = level.Catalogue is { } catalogue && catalogue.TryGet( placed.CatalogueId, out var item )
			? item.TrackType : 0;

		_door.IsDown = closed;
		_door.HelpText = closed ? 12 : 13;
		_door.Enabled = !closed || ParkRideOperation.MayOpen( level.Park, placed, trackType );

		var status = ParkClosedStatus.For( level.Park, placed, trackType );
		// Breakdown already precedes the closed status in FUN_00485f60. Other maintenance/track
		// statuses remain outside this closed-door implementation (docs/exe/ride-window-door.md).
		var broken = IsBroken();
		_broken.Visible = status != 0 || broken;
		_broken.Text = broken ? Localization.Text( 366 ) : status == 0 ? null
			: Localization.Text( status switch { 22 => 388, 23 => 389, _ => 365 } );
		_broken.TextColour = broken ? new UiColour( 255, 50, 30 ) : ParkClosedStatus.Colour( status );
	}

	/// <summary>Whether the ride being shown has broken down - <c>VAR_BROKEN</c> on its own script.</summary>
	private bool IsBroken()
	{
		if ( ParkRides.Current is not { } rides )
			return false;

		var id = rides.ScriptFor( ThingId );

		if ( id == 0 || rides.Scheduler.Find( id ) is not { } script )
			return false;

		return script[ParkRideOperation.BrokenVariable] != 0;
	}

	/// <summary>
	/// One of the bottom row's verbs. Delete, move, queue and the door are built; the rest are counted by name.
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

			// FUN_0048cfa0: demolish, then take the same item into the hand facing the way it stood, and
			// close - the window gets out of the way so the park can be clicked, and Level.WorldClick puts
			// whatever is in the hand down on the cell under the pointer.
			case 0x3e2c:
				Log.Info( $"Ride window: {ParkBuilding.PickUp( ThingId )}" );
				Stack.Close( this );
				return;

			// FUN_004af200( 0 ): select this thing, install mode 0x14 - "edit this ride's queue", the same
			// mode clicking one of its queue cells installs - and run it at once, which anchors the queue
			// tool on the queue's far end. Then close.
			case 0x3e34:
				Log.Info( $"Ride window: {ParkPathBuilding.EditQueue( ThingId )}" );
				Stack.Close( this );
				return;

			// FUN_004af600 -> FUN_0048ccf0 uses the switch's new down state, with no handler guard.
			case 0x3e38:
				if ( _door != null )
					ParkPeople.Current?.SetRideClosed( ThingId, _door.IsDown );
				ShowTheDoor();
				return;

			default:
				Unimplemented.Report( $"RIDE_WINDOW_{what.ToUpperInvariant().Replace( ' ', '_' )}" );
				Log.Info( $"Ride window: '{what}' is not built yet" );
				return;
		}
	}
}
