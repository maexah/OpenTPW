namespace OpenTPW.UI;

/// <summary>
/// Every guest in the park, one row each - the original's <c>allpeeps</c>, and the fourth of the
/// screens behind the gadget's Information button.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_00493530</c> builds it from the compiled stream at
/// <c>0x007506c8</c>, walked here opcode by opcode. Every rect, id and mesh is that stream's; the six
/// column headings are UITEXT <b>113-118</b>, which the builder hands to the header children by id
/// <c>0x10 + index</c>. See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>It has no tabs</b>, where its three siblings do - a guest has no kind to sort by, so the list is
/// the whole screen and the only controls beside it are the title, the three cross-links and the way
/// out.
/// </para>
///
/// <para>
/// <b>All six columns are numbers</b>: <c>FUN_006636b2</c> types each column 1, numeric, where the staff and item
/// screens type their first 0, text. The first column is a visitor NUMBER rather than a name.
/// </para>
///
/// <para>
/// <b>The fifth column is the guest's last thought, as a picture.</b> <b>UITEXT row 117 is literally <c>"?"</c> in the
/// shipped string file</b>, so the original heads it so too; the cell shows the part of <c>thoughts.MD2</c> its value
/// numbers, the last thought less one (<see cref="ThoughtPart"/>). Rides Ridden is the guest's
/// <see cref="Peep.NumRides"/> (<c>0x00493850</c>); Time In Park is <see cref="HoursIn"/>, lettered by
/// <see cref="TimeText"/>.
/// </para>
///
/// <para>
/// <b>The styler <c>FUN_004939d0</c> makes the other cells</b>: the visitor number a cell lettering UITEXT 457 at nought
/// (<c>FUN_00490c30</c>, its setter <c>0x00490dc0</c>), the time in park hours or days (<c>FUN_00490e60</c>, its setter
/// <c>0x00490ff0</c>), the cash a money cell (<c>FUN_00490250</c>), and the happiness a
/// bar in <c>happygrad.wct</c> inset 8 (<c>FUN_00477ff0</c>, <c>FUN_00485e30( cell, 0, 8 )</c>), and the thought the mesh
/// the game files under its first node's name, <c>ti_hungry</c>, which is <c>thoughts.MD2</c>, inset 4 across
/// (<c>FUN_00485e30( cell, 4, 0 )</c>). See <see cref="RowOf"/>.
/// </para>
/// </summary>
internal sealed class ParkVisitorsScreen : UiWindow
{
	/// <summary>The header rects, in column order, from the stream's <c>op 0xc</c> children.</summary>
	private static readonly UiRect[] Headings =
	[
		new( 296, 203, 690, 309 ), new( 695, 203, 955, 309 ), new( 960, 203, 1220, 309 ),
		new( 1225, 203, 1366, 309 ), new( 1371, 203, 1475, 309 ), new( 1480, 203, 1717, 308 )
	];

	/// <summary>
	/// How often each row's values are rewritten, in milliseconds of real time (<see cref="Time.WallMilliseconds"/>, as
	/// a control's timer is, <c>FUN_00661fe5</c>) - <b>the original's own cadence</b>.
	/// <c>FUN_00493530</c> arms a 2000ms timer (id <c>0x80083</c>, the same one the gadget's gauge
	/// uses), and on it <c>0x00493270</c> rewrites every existing row in place: nothing is added, removed,
	/// re-sorted or scrolled, so the list stays where the player scrolled it. <c>docs/exe/hud.md</c>,
	/// "How allpeeps keeps itself current".
	/// </summary>
	private const long RefreshEvery = 2000;

	/// <summary>The font the rows are lettered in, the screen's font slot 7.</summary>
	private const int RowFont = 7;

	/// <summary>
	/// The thought column's mesh: <c>ui.wad</c>'s <c>thoughts.MD2</c>, which the original files under its first node's
	/// name, <c>ti_hungry</c> (<c>FUN_00489060</c>), the name the styler asks for. Its seventeen parts are thoughts 1 to
	/// 17's pictures, hungry first and confused last.
	/// </summary>
	internal const string ThoughtsMesh = "thoughts";

	private readonly UiList _list;

	/// <summary>
	/// The list's sort, kept for the session: the column + 1, negated when descending - the original's
	/// <c>[0x007508bc]</c>, which starts at 1, the Visitor Number ascending.
	/// </summary>
	private static int _sort = 1;

	/// <summary>The park whose guests arriving and going this list is told of, let go of as it closes.</summary>
	private readonly ParkPeople? _people;

	/// <summary>The rewrite's timer, running from the screen's making.</summary>
	private readonly UiTimer _refresh = new( RefreshEvery );

	/// <param name="pick">
	/// A guest whose row is picked and brought into view as the list fills (<c>FUN_00493530</c>'s argument: the visitor's
	/// window's <c>b_kids</c>), or nought.
	/// </param>
	public ParkVisitorsScreen( WindowStack stack, int pick = 0 ) : base( stack )
	{
		// Built onto the park's own layer (0x0049353e) - see UiWindow.ParkScreen.
		ParkScreen = true;

		// w_big, the node "window4" inside w_big.MD2 - the frame five screens share. Without it this
		// screen is a list floating over the park. See docs/exe/hud.md.
		Root = new UiControl
		{
			Id = 0x1e496,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" )
		};

		// Lettered by UI_SetTitle on message 0x15 (0x004933b9): font 5 in (234, 239, 102), with its edge, centred in the
		// stream's rect (803 to 1276) widened by half its width either side.
		Root.Add( new UiControl
		{
			Id = 0x1e498,
			Rect = new UiRect( 567, 81, 1512, 161 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			Text = Localization.Get( UIStrings.AllVisitors )
		} );

		_list = Root.Add( new UiList
		{
			Id = 0x1e497,
			Rect = new UiRect( 272, 181, 1817, 930 ),
			HelpText = 135,
			Mesh = UiMesh.Get( "list_kids" ),
			RowArea = new UiRect( 294, 315, 1720, 914 ),

			// The row factory FUN_004938c0 measures font 7 and keeps none of it: every row is 0x38 tall (0x004938fa).
			// The styler FUN_004939d0 letters each cell in font 7 ([0x007508b4]).
			RowFont = RowFont,
			RowHeight = 0x38,

			// A right click on the list moves the camera to the selected row's guest and closes the screen (0x004934c5).
			RowRightClicked = GoTo,

			// A row's 0x400 opens the visitor's window on its guest (0x0049351e), which closes this screen.
			Activated = guest => ParkVisitorWindow.Open( Stack, guest ),

			// The tree's list record carries flag 0x80 (0x00750716): the row under the moving pointer is picked. The
			// screen's handler (0x00493230) has no 0x401 arm, so the pick is the highlight alone.
			SelectsUnderPointer = true,

			// The screen's handler keeps the list's 0x406 (0x00493479).
			SortChanged = word => _sort = word,
			Columns = [(297, 689), (701, 948), (966, 1213), (1233, 1358), (1372, 1474), (1478, 1718)],

			// The styler's FUN_0065c428( across, 1 ) on each cell: the number and the time centred, the cash to the right,
			// the rides ridden at a number cell's default, from the left (FUN_0048fde0) - as the original's frame letters them.
			ColumnAligns =
			[
				TextAlign.Centre, TextAlign.End, TextAlign.Centre,
				TextAlign.Start, TextAlign.Start, TextAlign.End
			],

			// The styler's happiness cell, inset 8 (FUN_004939d0).
			BarColumns = [false, false, false, false, false, true],
			BarSkins = [null, null, null, null, null, ParkObjectWindow.UiStatBar.HappyGrad],
			BarInset = 8,

			// The styler's thought cell (FUN_004939d0): the mesh filed as "ti_hungry", inset 4 across.
			MeshColumns = [null, null, null, null, ThoughtsMesh, null],
			MeshInset = 4
		} );

		// Handed over while the list is still empty, so it sorts nothing.
		_list.SetSort( _sort );

		// Framed by FUN_00486660 with help rows 0x81 to 0x86 (0x00493682).
		for ( var column = 0; column < Headings.Length; ++column )
		{
			_list.AddFramedHeading( column, Headings[column], Localization.Text( 113 + column ), 0x81 + column ).Clicked
				+= LogOrder;
		}

		Log.Info( $"Visitors list: headings {_list.HeadingsCensus()}" );
		Log.Info( $"Visitors list: {_list.RowsCensus()}" );

		Root.Add( CrossLink( 0x1e49b, 360, 136, "b_allthings", 5 ) );
		Root.Add( CrossLink( 0x1e499, 467, 137, "b_parkinfo", 3 ) );
		Root.Add( CrossLink( 0x1e49a, 575, 138, "b_allstaff", 4 ) );

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();

		_people = ParkPeople.Current;
		Show();

		// The opener's last act posts the advisor's message 0x11e (FUN_00493530), which is not built.
		Unimplemented.Report( "VISITORS_LIST_ADVISOR_LINE" );

		if ( pick != 0 )
		{
			_list.Pick( pick );
			Log.Info( $"Visitors list: guest {pick} picked, selected {_list.Selected}, top row {_list.ScrollTop}" );
		}

		if ( _people != null )
		{
			_people.GuestArrived += Arrived;
			_people.GuestLeaving += Leaving;
		}
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
	/// Fills the list with the park's guests, once, as it opens - each inserted in sort order.
	/// </summary>
	/// <remarks>
	/// <see cref="ParkPeople.Peeps"/> is guests only - staff are a list of their own - so this needs no
	/// filter, where the staff screen's walk does.
	/// <para>
	/// <b>The sort is the screen's kept one</b>, <see cref="_sort"/>: Visitor Number ascending until a heading is
	/// clicked.
	/// </para>
	/// </remarks>
	private void Show()
	{
		if ( _people == null )
			return;

		foreach ( var guest in _people.Peeps )
		{
			var row = RowOf( guest, _people.State.GameTick, rewrite: false );

			_list.Add( row );
			Log.Info( $"Visitors list: row {Census( row )}" );
		}

		LogOrder();
	}

	private void LogOrder() => Log.Info( $"Visitors list: {_list.Census()}" );

	/// <summary>
	/// The list's right click (<c>0x402</c>, <c>0x00493483</c>): the camera to the row's guest, then the screen closed
	/// (message 4, <c>0x004934da</c>), whether or not the guest is still there to go to.
	/// </summary>
	private void GoTo( int guest )
	{
		ParkOrbitCameraMode.GoToThing( guest );
		Stack.Close( this );
	}

	/// <summary>
	/// A guest's row - the original's row adder <c>FUN_00493800</c>, or with <paramref name="rewrite"/> the two-second
	/// rewrite's own copy of it (<c>0x00493270</c>).
	/// </summary>
	/// <remarks>
	/// Every column is a number, and the list sorts on it. The visitor number letters UITEXT 457 at nought
	/// (<c>0x00490dc0</c>) and <c>"%d"</c> otherwise; the cash is a money cell's text; the time in park is the hours at
	/// the park's clock <paramref name="gameTick"/>; the happiness is its whole part's low byte as a bar,
	/// <c>((x &amp; 0xff) &lt;&lt; 10) / 100</c> of 1024 (<c>0x00493870</c>).
	/// <para>
	/// <b>The rewrite shifts the stay back left by two</b> (<c>SHL EAX, 2</c> at <c>0x004932db</c>) where the adder does
	/// not, so a row letters four times its hours from the first rewrite on, as the original's does.
	/// </para>
	/// </remarks>
	private static UiList.Row RowOf( Peep guest, int gameTick, bool rewrite )
	{
		var stay = guest.StayAt( gameTick );
		var hours = HoursIn( rewrite ? stay << 2 : stay );

		return new( guest.ThingId, NumberText( guest.VisitorNumber ), guest.VisitorNumber, Values:
		[
			ParkItemsScreen.Money( guest.Cash ),
			TimeText( hours ),
			$"{guest.NumRides}",
			"",
			""
		],
		Numbers:
		[
			guest.VisitorNumber, guest.Cash, hours, guest.NumRides, ThoughtPart( guest.Thoughts.Last ),
			ParkItemsScreen.Bar( (int)guest.Happiness )
		] );
	}

	/// <summary>
	/// The thought cell's value (the adder's <c>0x0049385a</c>, the rewrite's <c>0x0049330c</c>): the last thought less
	/// one, the part of <see cref="ThoughtsMesh"/> it shows, or 999 with none. Thoughts 18 to 22 and 999 are past its
	/// last part, so the cell shows nothing; the list sorts the column on this number.
	/// </summary>
	internal static int ThoughtPart( int thought ) => thought == 0 ? 999 : thought - 1;

	/// <summary>
	/// A stay's whole park hours (<c>0x00493834</c>): <c>FUN_004f8730</c> takes it times <c>mFunnySecsPerRealSec</c>,
	/// over four, times 10,000,000 in 64 bits (<c>__allmul</c>, which wraps), and the adder divides that by
	/// 36,000,000,000 (<c>__alldiv</c>, signed) and keeps the low half. A guest stamped after the clock has a stay near
	/// 2<sup>30</sup>: four sweeps ahead the wrap gives 93,661,950 hours at the shipped rate, and the rewrite's four
	/// times that stay -137,761,755, as the original's.
	/// </summary>
	internal static int HoursIn( uint stay )
		=> unchecked( (int)((long)((ulong)GameCalendar.Rate * stay / GameCalendar.AdvancesPerSecond * 10_000_000)
			/ 36_000_000_000) );

	/// <summary>
	/// The time in park cell (<c>FUN_00490e60</c>'s setter <c>0x00490ff0</c>): past 48 hours UITEXT 443 ("days") and past
	/// 24 UITEXT 442 ("day"), each with the hours over 24 as parameter 8; else UITEXT 441 ("hours") with the hours as
	/// parameter 11. A negative count is handed as its size and a sign word that the writer (<c>0x006add40</c>, which
	/// prints the value <c>"%d"</c>) does not read, so it letters without its sign: the rewrite's wrap of a guest
	/// stamped after the clock does.
	/// </summary>
	internal static string TimeText( int hours )
		=> hours > 48 ? Localization.Format( 443, (8, $"{hours / 24}") )
			: hours > 24 ? Localization.Format( 442, (8, $"{hours / 24}") )
			: Localization.Format( 441, (11, $"{(hours < 0 ? unchecked( -hours ) : hours)}") );

	/// <summary>The visitor number's cell (<c>FUN_00490c30</c>'s setter <c>0x00490dc0</c>): "New Visitor" at nought.</summary>
	private static string NumberText( int number )
		=> number == 0 ? Localization.Get( UIStrings.NewVisitor ) : $"{number}";

	/// <summary>
	/// A row for the log: id, the number's text, the cash's, the time in park's with its hours, the rides ridden, the
	/// thought's part and the happiness bar.
	/// </summary>
	private static string Census( UiList.Row row )
		=> $"{row.Id} {row.Name}: {row.Values![0]} {row.Values[1]} ({row.Numbers![2]} h) {row.Values[2]} "
			+ $"part {row.Numbers[4]} {row.Numbers[5]}";

	/// <summary>A guest was made while the list is open: their row goes in by sort order (<c>FUN_00493c50</c>).</summary>
	private void Arrived( Peep guest )
	{
		var at = _list.Add( RowOf( guest, _people!.State.GameTick, rewrite: false ) );

		Log.Info( $"Visitors list: row added for guest {guest.ThingId} (visitor {guest.VisitorNumber}) at {at} - "
			+ $"{_list.Rows.Count} rows, top row {_list.ScrollTop}" );
	}

	/// <summary>A guest is going while the list is open: their row comes out (<c>FUN_00493c10</c>).</summary>
	private void Leaving( int thingId )
	{
		var at = _list.Remove( thingId );

		if ( at < 0 )
			return;

		Log.Info( $"Visitors list: row removed for guest {thingId} at {at} - "
			+ $"{_list.Rows.Count} rows, top row {_list.ScrollTop}" );
	}

	/// <summary>
	/// Every two seconds, each row's values rewritten where it stands, from the last row to the first
	/// (<c>0x00493270</c>).
	/// </summary>
	protected internal override void Update()
	{
		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 )
			return;

		if ( _people == null )
			return;

		var rewritten = 0;

		for ( var index = _list.Rows.Count - 1; index >= 0; --index )
		{
			if ( _people.Guests.TryGetValue( _list.Rows[index].Id, out var guest ) )
			{
				_list.Replace( index, RowOf( guest, _people.State.GameTick, rewrite: true ) );
				++rewritten;
			}
		}

		if ( _list.Rows.Count > 0 )
			Log.Info( $"Visitors list: {rewritten} rows rewritten, row 0 {Census( _list.Rows[0] )}" );
	}

	protected internal override void Closed()
	{
		if ( _people == null )
			return;

		_people.GuestArrived -= Arrived;
		_people.GuestLeaving -= Leaving;
	}
}
