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
/// <b>All six columns read from the RIGHT, the name column included</b>, and that is one place the
/// list widget's own default is wrong: <c>FUN_006636b2</c> is called with 1 for all six here where the
/// staff and item screens pass 0 for their first. The first column is a visitor NUMBER rather than a
/// name, which is presumably why.
/// </para>
///
/// <para>
/// <b>Two of the six columns are left blank.</b> Time In Park is decoded - a quarter of the park hours since the
/// guest's arrival stamp <c>+0x1d4</c> (<c>FUN_004fd950</c>, <c>0x0049383e</c>) - and a <see cref="Peep"/> keeps no
/// arrival stamp yet. The fifth column is
/// stranger: <b>UITEXT row 117 is literally <c>"?"</c> in the shipped string file</b>, so the original ships
/// that column unnamed too; its row adder fills it from the guest's last thought (<c>0x0049385a</c>), which
/// nothing here keeps. It is built, headed as the game heads it, and left empty. Rides Ridden is the guest's
/// <see cref="Peep.NumRides"/> (<c>0x00493850</c>).
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

	public ParkVisitorsScreen( WindowStack stack ) : base( stack )
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

		Root.Add( new UiControl
		{
			Id = 0x1e498,
			Rect = new UiRect( 803, 81, 1276, 161 ),
			Font = 5,
			TextColour = UiColour.White,
			Text = Localization.Get( UIStrings.AllVisitors )
		} );

		_list = Root.Add( new UiList
		{
			Id = 0x1e497,
			Rect = new UiRect( 272, 181, 1817, 930 ),
			HelpText = 135,
			Mesh = UiMesh.Get( "list_kids" ),
			RowArea = new UiRect( 294, 315, 1720, 914 ),

			// A right click on the list moves the camera to the selected row's guest and closes the screen (0x004934c5).
			RowRightClicked = GoTo,

			// The screen's handler keeps the list's 0x406 (0x00493479). Time In Park and the fifth column are not
			// built, so a sort on either leaves the rows as they stand: counted.
			SortChanged = word =>
			{
				_sort = word;

				if ( Math.Abs( word ) is 3 or 5 )
					Unimplemented.Report( "VISITOR_SORT_ON_UNBUILT_COLUMN" );
			},
			Columns = [(297, 689), (701, 948), (966, 1213), (1233, 1358), (1372, 1474), (1478, 1718)],

			// All six from the right - see the class remarks.
			ColumnAligns =
			[
				TextAlign.End, TextAlign.End, TextAlign.End,
				TextAlign.End, TextAlign.End, TextAlign.End
			]
		} );

		// Handed over while the list is still empty, so it sorts nothing.
		_list.SetSort( _sort );

		for ( var column = 0; column < Headings.Length; ++column )
			_list.AddHeading( column, Headings[column], Localization.Text( 113 + column ) ).Clicked += LogOrder;

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

		if ( _people != null )
		{
			_people.GuestArrived += Arrived;
			_people.GuestLeaving += Leaving;
		}

		// Counted once, as the screen opens - not once a frame. See RefreshEvery.
		Unimplemented.Report( "VISITOR_TIME_IN_PARK" );
		Unimplemented.Report( "VISITOR_COLUMN_117_UNNAMED" );
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
			_list.Add( RowOf( guest ) );

		LogOrder();
	}

	private void LogOrder() => Log.Info( $"Visitors list: {_list.Census()}" );

	/// <summary>A guest's row - the original's row adder <c>FUN_00493800</c>.</summary>
	/// <summary>
	/// The list's right click (<c>0x402</c>, <c>0x00493483</c>): the camera to the row's guest, then the screen closed
	/// (message 4, <c>0x004934da</c>), whether or not the guest is still there to go to.
	/// </summary>
	private void GoTo( int guest )
	{
		ParkOrbitCameraMode.GoToThing( guest );
		Stack.Close( this );
	}

	/// <remarks>
	/// Every column is a number, and the list sorts on it. The original's happiness is its whole part as a bar,
	/// <c>(byte) &lt;&lt; 10 / 100</c> (<c>0x00493870</c>), which stands in the same order as the whole part.
	/// </remarks>
	private static UiList.Row RowOf( Peep guest )
		=> new( guest.ThingId, $"{guest.VisitorNumber}", guest.VisitorNumber, Values:
		[
			$"{guest.Cash}",
			"",
			$"{guest.NumRides}",
			"",
			$"{(int)guest.Happiness}"
		],
		Numbers: [guest.VisitorNumber, guest.Cash, 0, guest.NumRides, 0, (int)guest.Happiness] );

	/// <summary>A guest was made while the list is open: their row goes in by sort order (<c>FUN_00493c50</c>).</summary>
	private void Arrived( Peep guest )
	{
		var at = _list.Add( RowOf( guest ) );

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

		for ( var index = _list.Rows.Count - 1; index >= 0; --index )
		{
			if ( _people.Guests.TryGetValue( _list.Rows[index].Id, out var guest ) )
				_list.Replace( index, RowOf( guest ) );
		}
	}

	protected internal override void Closed()
	{
		if ( _people == null )
			return;

		_people.GuestArrived -= Arrived;
		_people.GuestLeaving -= Leaving;
	}
}
