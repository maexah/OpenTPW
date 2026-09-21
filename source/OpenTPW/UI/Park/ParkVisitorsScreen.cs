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
/// <b>All six columns read from the RIGHT, the name column included</b>, and that is the one place the
/// list widget's own default is wrong: <c>FUN_006636b2</c> is called with 1 for all six here where the
/// staff and item screens pass 0 for their first. The first column is a visitor NUMBER rather than a
/// name, which is presumably why.
/// </para>
///
/// <para>
/// <b>Three of the six columns have no data behind them and are left blank.</b> A <see cref="Peep"/>
/// records neither how long they have been in the park nor how many rides they have been on - there is
/// no field for either, and inventing one from the tick clock would be a number nobody measured. The
/// fifth column is stranger: <b>UITEXT row 117 is literally <c>"?"</c> in the shipped string file</b>,
/// so the original ships that column unnamed too. It is built, headed as the game heads it, and left
/// empty.
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
	/// How often the list is rebuilt, in seconds - <b>the original's own cadence</b>.
	/// <c>FUN_00493530</c> arms a 2000ms timer (id <c>0x80083</c>, the same one the gadget's gauge
	/// uses) and refreshes on it, and the staff screen's builder arms the identical one.
	/// </summary>
	/// <remarks>
	/// <b>This is here because rebuilding every frame was measurably wrong, not because it was
	/// untidy.</b> A single visit to this screen reported its three counted columns <b>260 times</b> -
	/// eighteen guests re-added on every frame - which is what made the per-frame rebuild visible at
	/// all, in the gap census rather than on screen.
	/// </remarks>
	private const float RefreshEvery = 2f;

	private readonly UiList _list;

	private float _nextRefresh;

	public ParkVisitorsScreen( WindowStack stack ) : base( stack )
	{
		Modal = true;

		Root = new UiControl { Id = 0x1e496, Rect = new UiRect( 186, 30, 2018, 1007 ) };

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
			Columns = [(297, 689), (701, 948), (966, 1213), (1233, 1358), (1372, 1474), (1478, 1718)],

			// All six from the right - see the class remarks.
			ColumnAligns =
			[
				TextAlign.End, TextAlign.End, TextAlign.End,
				TextAlign.End, TextAlign.End, TextAlign.End
			]
		} );

		for ( var column = 0; column < Headings.Length; ++column )
			_list.AddHeading( column, Headings[column], Localization.Text( 113 + column ) );

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
		Show();

		// Counted once, as the screen opens - not once a frame. See RefreshEvery.
		Unimplemented.Report( "VISITOR_TIME_IN_PARK" );
		Unimplemented.Report( "VISITOR_RIDES_RIDDEN" );
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
	/// Fills the list with the park's guests.
	/// </summary>
	/// <remarks>
	/// <see cref="ParkPeople.Peeps"/> is guests only - staff are a list of their own - so this needs no
	/// filter, where the staff screen's walk does.
	/// </remarks>
	private void Show()
	{
		_list.Clear();

		if ( ParkPeople.Current is not { } people )
			return;

		foreach ( var guest in people.Peeps )
		{
			_list.Add( new UiList.Row( guest.ThingId, $"{guest.VisitorNumber}", 0, Values:
			[
				$"{guest.Cash}",
				"",
				"",
				"",
				$"{(int)guest.Happiness}"
			] ) );
		}
	}

	protected internal override void Update()
	{
		if ( Time.Now < _nextRefresh )
			return;

		_nextRefresh = Time.Now + RefreshEvery;

		Show();
	}
}
