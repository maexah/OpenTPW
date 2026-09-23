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

		// RESOLVED 2026-09-21: 0xaaee5929 is the node "b_ride it!" inside b_rideit.MD2, so this button
		// had artwork all along and drew nothing. This comment said the hash matched "no member stem
		// and no node name inside any of ui.wad's 1202 members" - the scan behind that read the
		// members' RAW BYTES, and every one of ui.wad's 278 models is refpack-compressed, so it was
		// searching compressed noise. The name also carries a space and an exclamation mark, which a
		// token-splitting scan drops even once decompressed.
		//
		// The ARTWORK is named; the VERB still is not. Its handler is FUN_004e15b0( 0 ) followed by a
		// close, which is not decoded, so the button draws and reports itself like the others.
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
	/// <b>Shown only while the ride is broken down</b>; see where it is built for why.
	/// </summary>
	private readonly UiControl _broken;

	/// <summary>
	/// How far the preview looks down at the ride, in degrees. <b>A choice, and marked as one.</b> The
	/// park's own camera runs 45 pulled in to 65 pushed out (<see cref="ParkOrbitCameraMode.Pitch"/>),
	/// and this takes the pulled-in end: reading the live pitch would swing the preview whenever the
	/// player zoomed, which the original's does not do.
	/// </summary>
	private const float PreviewPitch = 45f;

	/// <summary>Whether the preview has already reported what it found. Once, not once a frame.</summary>
	private bool _previewSaidWhy;

	/// <summary>The same, for the fit it worked out - reported after the scale exists to report.</summary>
	private bool _previewSaidFit;

	/// <summary>
	/// How fast the preview turns, in radians a second. <b>A choice, and marked as one.</b> The
	/// original advances its angle by <c>elapsed * _DAT_006fe804</c> and indexes a sine table masked
	/// to wrap (<c>FUN_00468e50</c>); the table and the mask are decoded, that constant is not.
	/// </summary>
	private const float SpinRate = 0.9f;

	/// <summary>
	/// How much of the panel the model fills. <b>Also a choice.</b> The original fits by the model's
	/// bounding box against the panel's width and height and takes whichever is tighter
	/// (<c>FUN_004689f0</c>); this fits by <see cref="LobbyModel.Radius"/>, which is that box's loose
	/// radius, so the margin below stands in for the difference.
	/// </summary>
	private const float Fill = 0.8f;

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

		_preview = Root.Add( new UiControl
		{
			Id = 0x3e24,
			Rect = new UiRect( 348, 162, 762, 576 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		// HIDDEN UNTIL THE RIDE BREAKS DOWN. This is not decoration over the preview: it is the border
		// of a message box that appears on top of it, and the builder says so - FUN_004ad720 sets its
		// frame and then calls UI_SetVisible(0), so it starts hidden. This decode recorded that and
		// then drew it anyway, which left a yellow-and-black bar across a working preview.
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
			// The four condition rows are GAUGES and the rest are text - see UiStatBar. Setting Text
			// on a gauge is exactly why those four rows sat blank while their labels rendered.
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

		// Reached every time the window opens, and none of them answerable from what is decoded: the
		// stats table and the preview are filled by the shared base's own vtable slots against UITEXT
		// rows this has not read, and the sliders commit into the ride's script variables.
		// RIDE_STATS_PANEL is gone: the table's seven labels and two of its figures are filled now, and
		// what is left unanswerable is counted by name from FillStats instead.
		// RIDE_PREVIEW_ACTOR is gone: the panel shows the ride's own model now, turning, drawn from
		// Level.Render's overlay pass - see DrawPreview. A counter left on a path that works is a lie
		// in the gap census in the same way a missing one is.
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
	/// <b>Four of the seven figures are not answerable here and are counted rather than invented.</b>
	/// Excitement, reliability, state of repair and remaining life are type-9 bars skinned
	/// <c>ridestatbar.wct</c>, and the engine computes each from the ride's own condition and from the
	/// three sliders - <c>FUN_004e0560</c> divides two slider values by per-upgrade maxima this decode
	/// has not read. Users last month reads a thirty-month ring buffer, and this game keeps no monthly
	/// history at all, which is the same gap the hire screen's mini-balance already records.
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

		// How old it is, in DAYS. FUN_004dd670 divides the built stamp by 864,000,000,000 - one day in
		// hundred-nanosecond units - so this is real elapsed time and not the park's own calendar.
		//
		// A shipped save therefore answers in the THOUSANDS, and that is right: its rides were built
		// when the save was made, which is now 26 years ago, so Belly Bounce reads 9759 days. A figure
		// that size is the save's real age showing through, not a zero epoch to go hunting for.
		if ( _stats.TryGetValue( 0x3e1b, out var age ) )
			age.Text = placed.Built.IsSet ? $"{DaysSince( placed.Built )}" : null;

		// FUN_004e2400: the age-bucket scrap percentage times the item's build price. That percentage
		// is SCRAP_VALUE_DEPRECIATION, already counted where Sell refunds - it answers 100 for anything
		// newly built, so today this is the build price and is increasingly wrong as a ride ages.
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
		// gauge above draws them the moment there is a number. They are left out because there is no
		// number yet: FUN_004ade40 fills them from FUN_004e0560 and FUN_004df640, whose closing
		// multiply the decompiler dropped into a bare __ftol. The shape is known - two ratios of the
		// speed and capacity sliders against the item's per-upgrade figures, each clamped to
		// 0.75..1.25 - and the shape alone would only produce a plausible bar, which is worse than
		// an empty one because it cannot be told apart from a measured one later.
		Unimplemented.Report( "RIDE_EXCITEMENT_BAR" );
		Unimplemented.Report( "RIDE_RELIABILITY_BAR" );

		// Users last month sums thirty entries of one of the object record's six ring buffers
		// (FUN_004ade40, 0x1e entries). ParkWorld deliberately does not read the rings: 720 bytes
		// have to be split over six of them and nothing measured says the split is even, so every
		// field BETWEEN rings would move if the guess were wrong.
		Unimplemented.Report( "RIDE_USERS_LAST_MONTH" );

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
		Log.Info( $"Ride window: thing {ThingId} stats age {(placed.Built.IsSet ? DaysSince( placed.Built ) : -1)} days," +
			$" scrap {item.BuildPrice}, built {placed.Built.Year}-{placed.Built.Month:D2}-{placed.Built.Day:D2}," +
			$" repair {placed.StateOfRepair:R}, life {placed.RemainingLife:R}" );
	}

	/// <summary>
	/// Draws the ride itself, turning, inside the preview panel.
	/// </summary>
	/// <remarks>
	/// <b>It is the model standing in the park, not a copy of it.</b> So the preview shows a ride that
	/// is running - its animation is whatever the park has it doing this frame - and the entities must
	/// not be moved to centre them: <see cref="ModelEntity.DrawOverlay"/> takes a transform of the
	/// caller's own for exactly this.
	/// <para>
	/// <b>Drawn in the overlay pass, not with the interface.</b> <c>Level.Render</c> clears depth and
	/// runs that pass after the HUD, which is how the advisor sits in front of everything; a model
	/// drawn in the HUD's own pass would be flat UI geometry competing with the window frame. The
	/// scissor keeps it inside the panel, which is what the original's view region does - it builds one
	/// over the panel's rect and replaces it whenever the shown thing changes (<c>FUN_00486410</c>).
	/// </para>
	/// </remarks>
	internal void DrawPreview()
	{
		if ( ParkObjects.Current?.ModelFor( ThingId ) is not { } model || model.Radius <= 0f )
		{
			// Said out loud: an empty panel and a panel drawn off its own edge look identical from
			// outside, and guessing between them is how a matrix gets "adjusted" until something
			// appears. Once a frame is too noisy, so this reports the first time it cannot draw.
			if ( !_previewSaidWhy )
			{
				_previewSaidWhy = true;

				Log.Info( $"Ride window: no preview for thing {ThingId} - " +
					$"model {(ParkObjects.Current?.ModelFor( ThingId ) is null ? "not found" : "found")}, " +
					$"objects {(ParkObjects.Current is null ? "null" : "live")}" );
			}

			return;
		}

		var panel = _preview.Pixels;

		if ( panel.Width <= 0f || panel.Height <= 0f )
			return;

		if ( !_previewSaidWhy )
		{
			_previewSaidWhy = true;

			var box = model.HasBounds
				? $"box ({model.BoundsMin.X:F1},{model.BoundsMin.Y:F1},{model.BoundsMin.Z:F1})" +
					$"..({model.BoundsMax.X:F1},{model.BoundsMax.Y:F1},{model.BoundsMax.Z:F1})"
				: "box none";

			Log.Info( $"Ride window: preview thing {ThingId} radius {model.Radius:F1}," +
				$" meshes {model.Entities.Length}, {box}, panel ({panel.X:F0},{panel.Y:F0})" +
				$" {panel.Width:F0}x{panel.Height:F0}, screen {Screen.Width:F0}x{Screen.Height:F0}" );
		}

		// FIT BY THE BOX, NOT BY THE RADIUS. Radius is a distance from the model's ORIGIN, and Belly
		// Bounce reports 100.2 across eight meshes while its bulk is a fraction of that - so sizing by
		// it drew the ride at about eight pixels, off the panel entirely. The engine fits its own
		// preview from the model's box, taking (max + min) / 2 as the centre and max - min as the
		// size (FUN_004689f0), which is what this does: the centre is subtracted in PreviewTransform
		// and the half-extent is what the panel is divided by.
		if ( !model.HasBounds )
			return;

		// THE PIVOT COMES FROM WHAT IS ACTUALLY DRAWN, not from the static box, and that is what stops
		// the spin looking odd. The box is measured over every mesh the model ships - including the
		// ones PoseAsBuilt hides once the ride is up, and including each mesh's own padding - so its
		// centre is not the centre of what you can see. Turning about a point that is not the visual
		// centre swings the model round instead of rotating it in place.
		//
		// IT TURNS ABOUT THE RIDE'S OWN CENTRE, which is all a preview wants - and two wrong turns got
		// here. The model's whole box covers every mesh it ships, INCLUDING the building meshes
		// PoseAsBuilt hides once the ride is up, so its centre sits below what can be seen and the ride
		// rode high. Taking the drawn meshes' ORIGINS instead cured the swing and not the height,
		// because a ride's meshes all have their origins on its base plane. What is wanted is the box
		// of the geometry actually DRAWN, and one centre serves both the pivot and the framing.
		//
		// The REST boxes are used rather than live positions deliberately: a pivot that followed the
		// animation would bob about as the ride moved, and spinning about a moving point is the very
		// wobble this is meant to remove.
		var low = new Vector3( float.MaxValue, float.MaxValue, float.MaxValue );
		var high = new Vector3( float.MinValue, float.MinValue, float.MinValue );
		var drawn = 0;

		for ( var i = 0; i < model.Entities.Length && i < model.MeshBoxes.Count; ++i )
		{
			if ( model.Entities[i].Model is null || model.Entities[i].Opacity <= 0f )
				continue;

			var (meshLow, meshHigh) = model.MeshBoxes[i];

			low = new Vector3( MathF.Min( low.X, meshLow.X ), MathF.Min( low.Y, meshLow.Y ),
				MathF.Min( low.Z, meshLow.Z ) );

			high = new Vector3( MathF.Max( high.X, meshHigh.X ), MathF.Max( high.Y, meshHigh.Y ),
				MathF.Max( high.Z, meshHigh.Z ) );

			++drawn;
		}

		// A model with nothing drawable in it falls back to its whole box.
		if ( drawn == 0 )
		{
			low = model.BoundsMin;
			high = model.BoundsMax;
		}

		// One centre, for the pivot and the framing alike, and the size off the same box - so what is
		// turned about is what is looked at, and what is fitted is what is drawn.
		var centre = (low + high) * 0.5f;
		var size = high - low;

		// FITTED FOR THE ANGLE IT IS SEEN AT, which the first version was not. Sizing by
		// max( size.X, size.Z ) assumes the model is looked at square on; tilted down by PreviewPitch
		// the model's DEPTH climbs into the picture as well, so it reaches
		// depth * sin(pitch) + height * cos(pitch) up the screen. Ignoring that is what pushed the
		// ride off the bottom of its panel and into the scissor, which cut it clean across.
		//
		// Across, the spin turns X and Y through each other, so the widest it can ever be is the
		// diagonal of its own footprint - not either side of it.
		var pitch = PreviewPitch.DegreesToRadians();

		var across = MathF.Sqrt( (size.X * size.X) + (size.Y * size.Y) );
		var tall = (size.Y * MathF.Sin( pitch )) + (size.Z * MathF.Cos( pitch ));

		var half = MathF.Max( MathF.Max( across, tall ) * 0.5f, 0.001f );

		var perUnit = MathF.Min( panel.Width, panel.Height ) * 0.5f * Fill / half;

		if ( !_previewSaidFit )
		{
			_previewSaidFit = true;

			var first = model.Entities.Length > 0
				? entityLocal( model.Entities[0] )
				: Vector3.Zero;

			// THE TERMS THEMSELVES, not another guess about them. The preview translates each mesh by
			// (Position - PlacedOrigin - centre) while `centre` comes from MeshBoxes, which are built
			// from REST offsets at load. If those two spaces disagree by any CONSTANT, the leftover is
			// rigid and `spin` swings it round - which is exactly the clean ring of fixed radius a
			// burst of frames showed. One line per mesh settles which, instead of editing and looking.
			// The box of what is ACTUALLY DRAWN, built alongside, to say whether the ride sitting low in
			// its panel is a framing fault or just where its pixels are. MeshBoxes[i] is offset +
			// geometry, so taking Offsets[i] off it leaves the mesh's own extent, and putting that back
			// on the LIVE position gives the pose on screen rather than the pose at rest. The gap
			// between this centre and the one the fit uses IS the error, in model units - the camera is
			// orthographic and aimed at the origin, so a centred box cannot land off-centre.
			//
			// >>> IT ANSWERED NOUGHT, AND THE FIT IS THEREFORE NOT WHAT SITS THE RIDE LOW. <<< Belly
			// Bounce reports off by (0.0, 0.0, 0.0), with every mesh's live position equal to its rest
			// offset. A burst of frames still puts the lit-pixel centroid at 0.591 down the panel
			// against a 0.500 middle, and that gap is WHERE THE PIXELS ARE: the ride's wide wooden base
			// carries far more of them than the thin figure standing on it, so the centroid sits below
			// the geometry's centre while the geometry's centre is exactly where it should be.
			//
			// So this is left alone deliberately. Centring the SILHOUETTE instead would be a deviation
			// from the engine, which fits a preview from the model's box - (max + min) / 2 and
			// max - min, FUN_004689f0 - and not from its pixels.
			var liveLow = new Vector3( float.MaxValue, float.MaxValue, float.MaxValue );
			var liveHigh = new Vector3( float.MinValue, float.MinValue, float.MinValue );

			for ( var i = 0; i < model.Entities.Length && i < model.MeshBoxes.Count; ++i )
			{
				if ( model.Entities[i].Model is null || model.Entities[i].Opacity <= 0f )
					continue;

				var live = model.Entities[i].Position - model.PlacedOrigin;
				var (meshLow, meshHigh) = model.MeshBoxes[i];
				var rest = (meshLow + meshHigh) * 0.5f;

				var lowAt = live + (meshLow - model.Offsets[i]);
				var highAt = live + (meshHigh - model.Offsets[i]);

				liveLow = new Vector3( MathF.Min( liveLow.X, lowAt.X ), MathF.Min( liveLow.Y, lowAt.Y ),
					MathF.Min( liveLow.Z, lowAt.Z ) );

				liveHigh = new Vector3( MathF.Max( liveHigh.X, highAt.X ), MathF.Max( liveHigh.Y, highAt.Y ),
					MathF.Max( liveHigh.Z, highAt.Z ) );

				Log.Info( $"Ride window: preview mesh {i}" +
					$" live ({live.X:F1},{live.Y:F1},{live.Z:F1})" +
					$" restoff ({model.Offsets[i].X:F1},{model.Offsets[i].Y:F1},{model.Offsets[i].Z:F1})" +
					$" restbox ({rest.X:F1},{rest.Y:F1},{rest.Z:F1})" +
					$" minus centre ({(live.X - centre.X):F1},{(live.Y - centre.Y):F1},{(live.Z - centre.Z):F1})" );
			}

			if ( drawn > 0 )
			{
				var liveCentre = (liveLow + liveHigh) * 0.5f;
				var off = liveCentre - centre;

				Log.Info( $"Ride window: preview drawn box centre" +
					$" ({liveCentre.X:F1},{liveCentre.Y:F1},{liveCentre.Z:F1})" +
					$" vs fit centre ({centre.X:F1},{centre.Y:F1},{centre.Z:F1})" +
					$" off by ({off.X:F1},{off.Y:F1},{off.Z:F1})" );
			}

			// `drawn` is reported because its being NOUGHT is invisible otherwise: the fallback quietly
			// reframes the ride by its whole box, and the only tell was a centre that looked familiar.
			Log.Info( $"Ride window: preview fit drawn {drawn} of {model.Entities.Length}," +
				$" half {half:F1}, perUnit {perUnit:F3}," +
				$" span {(size.X * perUnit):F0}px of {panel.Width:F0}," +
				$" centre ({centre.X:F1},{centre.Y:F1},{centre.Z:F1})," +
				$" mesh0 local ({first.X:F1},{first.Y:F1},{first.Z:F1})" );

			Vector3 entityLocal( ModelEntity one ) => one.Position - model.PlacedOrigin - centre;
		}

		// LOOKED AT FROM ABOVE AND IN FRONT, the way the park's camera sees a ride, rather than square
		// on. The first version copied AdvisorModel.ScreenProjection, which is a flat elevation - model
		// X across, Z up, Y squashed almost out of depth - and gave a ride with no perspective on it at
		// all. Here the eye sits back and up by the pitch and looks at the model's own centre, so the
		// centring stops being arithmetic to get right and becomes a consequence of what is aimed at.
		var eye = new System.Numerics.Vector3( 0f, -MathF.Cos( pitch ), MathF.Sin( pitch ) ) * (half * 4f);

		var view = System.Numerics.Matrix4x4.CreateLookAt( eye,
			System.Numerics.Vector3.Zero, new System.Numerics.Vector3( 0f, 0f, 1f ) );

		// A box big enough to hold the model at any angle of spin, then squeezed from the whole screen
		// down into the panel: scale by the panel's share of the screen, then move it to the panel's
		// own centre in normalised coordinates.
		var ortho = System.Numerics.Matrix4x4.CreateOrthographic(
			2f * half / Fill, 2f * half / Fill, -8f * half, 8f * half );

		var x = ((panel.X + (panel.Width * 0.5f)) * 2f / Screen.Width) - 1f;
		var y = 1f - ((panel.Y + (panel.Height * 0.5f)) * 2f / Screen.Height);

		var projection = ortho
			* System.Numerics.Matrix4x4.CreateScale( panel.Width / Screen.Width, panel.Height / Screen.Height, 1f )
			* System.Numerics.Matrix4x4.CreateTranslation( x, y, 0f );

		// Turning about the model's own up axis, off the frame clock.
		//
		// <b>A DECLARED DEVIATION: this stops while the clock is held, and the original's does not.</b>
		// The engine advances its angle by differencing a real-time clock every frame and wrapping
		// through a masked sine table (FUN_00468e50), so its preview keeps turning through a pause.
		// Here Time.Now only advances by Time.Delta, and a held clock reports zero - Now, Delta and
		// RawDelta all freeze together, and nothing in this project exposes wall-clock time while
		// paused. Adding such a clock for a spinning model would be a wider change than the model is
		// worth, so the deviation is said here instead. In normal play the window does not pause the
		// game, so it turns.
		var spin = System.Numerics.Matrix4x4.CreateRotationZ( Time.Now * SpinRate );

		var command = global::Global.Render.CommandList;

		command.SetScissorRect( 0, (uint)MathF.Max( 0f, panel.X ), (uint)MathF.Max( 0f, panel.Y ),
			(uint)MathF.Max( 0f, panel.Width ), (uint)MathF.Max( 0f, panel.Height ) );

		// THE DRAWN SET AND THE BOXED SET MUST BE THE SAME SET, and they were not: the box covered the
		// four meshes that pass the visibility test while all eight were drawn. Centring on half a ride
		// and turning all of it is an ORBIT of the offset between the two centres - which is exactly
		// what a burst of frames showed, the centroid tracing a clean ring about the panel's middle.
		//
		// A hidden mesh is one PoseAsBuilt put away when the ride finished going up, and it has no more
		// business in the preview than in the park. DrawOverlay does not consult Opacity the way
		// ModelEntity.OnRender does, so the skip has to be made here.
		foreach ( var entity in model.Entities )
		{
			if ( entity.Model is null || entity.Opacity <= 0f )
				continue;

			entity.DrawOverlay( view, projection, PreviewLight, PreviewLightColour,
				PreviewAmbient, worldNormals: true, transform: PreviewTransform( entity, model, centre, spin ) );
		}

		// Every solid half before any see-through one, the order the scene uses - see AdvisorModel.Draw
		// for what drawing them a mesh at a time costs.
		foreach ( var entity in model.Entities )
		{
			if ( entity.TranslucentModel is null || entity.Opacity <= 0f )
				continue;

			entity.DrawOverlay( view, projection, PreviewLight, PreviewLightColour,
				PreviewAmbient, worldNormals: true, translucent: true,
				transform: PreviewTransform( entity, model, centre, spin ) );
		}

		command.SetFullScissorRects();
	}

	/// <summary>
	/// Where one of the model's meshes goes in the preview: its own placement within the model, spun,
	/// with the park position taken out.
	/// </summary>
	/// <remarks>
	/// This is <see cref="Entity.ModelMatrix"/> with one substitution. That composes
	/// <c>LinearTransform * Rotation * Translate( Position )</c>, and <see cref="LobbyModel"/> sets
	/// each mesh's <c>Position</c> to its own offset PLUS the model's origin - so subtracting
	/// <see cref="LobbyModel.PlacedOrigin"/> leaves the mesh where it belongs inside the model and
	/// drops where the ride happens to stand in the park. Reading the live position rather than the
	/// rest-pose <c>Offsets</c> is what makes the preview show the ride animating.
	/// </remarks>
	private static System.Numerics.Matrix4x4 PreviewTransform( ModelEntity entity, LobbyModel model,
		Vector3 centre, System.Numerics.Matrix4x4 spin )
	{
		var matrix = entity.LinearTransform
			?? System.Numerics.Matrix4x4.CreateScale( entity.Scale.GetSystemVector3() );

		matrix *= System.Numerics.Matrix4x4.CreateFromQuaternion( entity.Rotation );

		// TWO subtractions, and both are needed. PlacedOrigin takes out where the ride stands in the
		// park; the box's centre takes out where the model sits relative to its OWN origin. Without
		// the second the model is scaled right and still hangs off the panel by exactly that offset,
		// which is the fault this was written to fix.
		matrix *= System.Numerics.Matrix4x4.CreateTranslation(
			(entity.Position - model.PlacedOrigin - centre).GetSystemVector3() );

		return matrix * spin;
	}

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
	/// <b>These are gauges, and treating them as text is why they showed nothing.</b> The labels
	/// beside them are text cells and render fine; the value cells are not. <c>FUN_004ade40</c> hands
	/// each of these four <c>((value &amp; 0xff) &lt;&lt; 10) / 100</c> - a 0..100 percentage mapped onto
	/// 0..1024 - through the control's <c>+0x1c</c> entry, while the rows either side of them
	/// (Users last month, Age, Scrap value) are given a string instead.
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

	/// <summary>How the preview is lit. <b>Chosen, not measured</b> - the original lights it from its own scene.</summary>
	private static readonly Vector3 PreviewLight = new( -400f, -600f, 400f );

	private static readonly Vector3 PreviewLightColour = Vector3.One;

	private const float PreviewAmbient = 0.55f;

	/// <summary>Whole days between a built stamp and now, never negative.</summary>
	private static int DaysSince( ParkWorld.BuiltWhen built )
	{
		try
		{
			var when = new DateTime( built.Year, built.Month, built.Day,
				built.Hour, built.Minute, built.Second, DateTimeKind.Utc );

			return Math.Max( 0, (int)(DateTime.UtcNow - when).TotalDays );
		}
		catch ( ArgumentOutOfRangeException )
		{
			// A stamp the save never filled in, or one this calendar cannot hold.
			return 0;
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

		// THE SPEED WORD IS A SEPARATE THING and deliberately not written. RideScript records that the
		// engine divides every wait by 0.5 + 0.01 * speed, worked out afresh per instruction from the
		// word at +0xc0, and argues the divisor can never differ from one because no opcode writes it.
		// That argument is about the SCRIPT system and this panel is outside it: FUN_004dd6e0 writes
		// that word from this very slider. So moving speed here would re-time every WAIT in the script,
		// which is a behaviour change too wide to make as a side effect of a slider.
		Unimplemented.Report( "RIDE_SPEED_SCALES_WAITS" );

		Log.Info( $"Ride window: thing {ThingId} set to speed {speed}, capacity {capacity}, duration {duration}" );
	}

	/// <summary>The window closing commits, the same as either arrow does - see <see cref="Commit"/>.</summary>
	protected internal override void Closed() => Commit();

	/// <summary>
	/// The chevron border belongs to a message box that appears over the preview when the ride breaks
	/// down, so it follows the ride's own state rather than being drawn as decoration.
	/// </summary>
	/// <remarks>
	/// Read every frame because a ride can break while its window is open - the window does not pause
	/// the game, which is the whole reason its cycle arrows are worth having.
	/// </remarks>
	protected internal override void Update() => _broken.Visible = IsBroken();

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

			default:
				Unimplemented.Report( $"RIDE_WINDOW_{what.ToUpperInvariant().Replace( ' ', '_' )}" );
				Log.Info( $"Ride window: '{what}' is not built yet" );
				return;
		}
	}
}
