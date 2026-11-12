namespace OpenTPW.UI;

/// <summary>
/// A scrolling multi-column list - the original's control <b>type 7</b>, and the one widget both the
/// buy and the hire screen are built on.
///
/// <para>
/// <b>It is the interface's only scrolling list.</b> <see cref="UiSlider"/> scrolls a value between
/// two ends, and the game menu measures its items and stacks them without scrolling.
/// </para>
///
/// <para>
/// <b>Rows are not controls.</b> The original builds one reusable widget chain per VISIBLE SLOT
/// (<c>FUN_006639cb</c>) and pushes the current scroll window's values into those slots on every
/// refresh (<c>FUN_00663324</c>); the rows themselves are records in a pool. That is reproduced here
/// because it is also the right shape - a park's catalogue is seventy items and the list shows a
/// dozen, so this builds a dozen children once and rewrites their text.
/// </para>
///
/// <para>
/// <b>A row's payload is sized by the COLUMN COUNT, not by a fixed struct.</b> Buy pushes three -
/// name, price and a state - and hire pushes two, name and wage. A three-field record would be wrong
/// for hire, which is why <see cref="Row"/> carries a value per column.
/// </para>
///
/// <para>
/// <b>The messages the original sends are an index, not an id.</b> <c>0x400</c> (row activated) and
/// <c>0x401</c> (selection changed) both carry the selected row's INDEX, and the screens turn that
/// into the row's own id through <c>FUN_00664c71</c> - because sorting makes the two diverge. Here
/// the callbacks are handed the id directly and the lookup happens inside, which keeps that divergence
/// in one place.
/// </para>
/// </summary>
internal sealed class UiList : UiControl
{
	/// <summary>
	/// One row: what it is called, a value per column after the name, and the id the screen knows it
	/// by. The id is not the position - <see cref="Rows"/> can be re-ordered under it.
	/// </summary>
	/// <remarks>
	/// <b><see cref="Values"/> carries a value per column</b>, which is what lets a five-column staff
	/// list be expressed. <see cref="Value"/> fills the first of them for a row with no
	/// <see cref="Values"/> - the shape the buy and hire screens and the items screen's counted rows push.
	/// <para>
	/// <b><see cref="Numbers"/> is what a numeric column is sorted on</b>, a number per column, for a row whose
	/// columns hold more than one. Without it a numeric column's number is <see cref="Value"/>, and the tick-box
	/// column's <see cref="State"/>.
	/// </para>
	/// <para>
	/// <b><see cref="Depth"/> is how many rows it hangs under</b>: the original's list is a tree, a row added with an
	/// insert-under (<c>FUN_0066403b</c>'s parent argument, <see cref="AddUnder"/>) a child of that row, its name
	/// lettered <see cref="IndentStep"/> units further in for each level (<c>FUN_00663324</c>, <c>0x0066353f</c>). Nought for
	/// every row of every other list.
	/// </para>
	/// </remarks>
	internal readonly record struct Row( int Id, string Name, int Value, int State = 0,
		IReadOnlyList<string>? Values = null, UiColour? Colour = null, IReadOnlyList<int>? Numbers = null, int Depth = 0 );

	/// <summary>How far in a child row's name stands for each level it is under, in virtual units: <c>FUN_0065c672( depth * 0x14, 0 )</c> (<c>FUN_00663324</c>).</summary>
	internal const int IndentStep = 0x14;

	private readonly List<Row> _rows = [];

	/// <summary>Every row the list holds, in the order it stands in.</summary>
	internal IReadOnlyList<Row> Rows => _rows;

	/// <summary>
	/// Which columns hold text, by column: the original's column type 0, sorted as text, where every other column is
	/// type 1, a number (<c>FUN_006636b2</c>; the layout stream leaves every column a number). Empty is every column
	/// a number.
	/// </summary>
	internal bool[] TextColumns { get; init; } = [];

	/// <summary>
	/// The style bit <c>0x10</c> of the list's layout flags (<c>+0x48</c>): set on all seven sorted list records (buy <c>0x91</c>, the
	/// rest <c>0x291</c>), clear on the Load and Save Park list's <c>0x81</c>. The click hook (<c>0x00485780</c>) picks the click by the
	/// style of the control the click is told to, and a heading tells its list, so a heading of a list with it plays <c>Select3</c>
	/// (<c>hud.md</c>, "Which window the click sound is for").
	/// </summary>
	internal bool Sorted { get; init; }

	/// <summary>The column the list is sorted on (<c>list+0x168</c>), or -1 for a list no screen has given a sort.</summary>
	private int _sortColumn = -1;

	/// <summary>The sort's direction (<c>list+0x16a</c>).</summary>
	private bool _descending;

	/// <summary>
	/// The sort as the original tells it and each screen keeps it: the column + 1, negated when descending
	/// (<c>FUN_006658b9</c>). Nought is a list with no sort, which stands in the order it was given its rows.
	/// </summary>
	internal int SortWord => _sortColumn < 0 ? 0 : _descending ? -(_sortColumn + 1) : _sortColumn + 1;

	/// <summary>The sort changed - the original's message <c>0x406</c>. Handed <see cref="SortWord"/>.</summary>
	internal Action<int>? SortChanged { get; set; }

	/// <summary>
	/// Where the rows are drawn, which the original carries as a rect of its own - stream op <c>0xa</c>,
	/// distinct from the control's rect and from its text rect. On the buy and hire screens those happen to
	/// hold the same numbers, so conflating them looks right here and breaks elsewhere.
	/// </summary>
	internal UiRect RowArea { get; init; }

	/// <summary>
	/// The left and right edge of each column on the virtual screen, from the stream's op <c>0xb</c>.
	/// The name is column nought; the rest are numbers.
	/// </summary>
	internal (int Left, int Right)[] Columns { get; init; } = [];

	/// <summary>
	/// How tall a row is, in virtual units. The park-file, hire, buy, all-staff and all-items screens give the height
	/// their row factory measures from the font (<see cref="RowHeightOf"/>), the visitors screen its factory's fixed 56.
	/// <b>The default of 44 is a choice, and marked as one</b>: no screen takes it, and no row of the original's measures it.
	/// </summary>
	internal int RowHeight { get; init; } = 44;

	/// <summary>
	/// A row's height on the virtual screen, as a row factory works it out from the font it letters the row in
	/// (<c>FUN_0049f0b0</c>, <c>FUN_004944d0</c>, <c>0x00496c10</c>): the font's height times <c>0x600</c> over the
	/// screen's height, plus 6. The font here is one of a set drawn for a screen height of its own, so that height
	/// stands for the screen's. No font answers the default.
	/// </summary>
	internal static int RowHeightFor( int? fontHeight, int screenHeight )
		=> fontHeight is { } height && screenHeight > 0 ? (height * 0x600 / screenHeight) + 6 : 44;

	/// <summary>The factory's height for a row lettered in font <paramref name="font"/> of the current set.</summary>
	internal static int RowHeightOf( int font ) => RowHeightFor( UiFonts.Get( font )?.LineHeight, UiFonts.SetScreenHeight );

	/// <summary>Which font the rows are lettered in.</summary>
	internal int RowFont { get; init; } = 6;

	/// <summary>
	/// Which way each column's text is pushed, where the default is not what the screen wants. Empty
	/// leaves the default: the name reads from the left and every number from the right.
	/// </summary>
	/// <remarks>
	/// <b>The default is not a rule.</b> The original sets this per cell in each screen's styler, through
	/// <c>FUN_0065c428( across, down )</c> on the cell (2 to the right, 1 centred, nothing set the left): the visitors
	/// list centres its number and pushes its cash right (<c>FUN_004939d0</c>). <c>FUN_006636b2</c> is the column's
	/// type, text or number, not its alignment.
	/// </remarks>
	internal TextAlign[] ColumnAligns { get; init; } = [];

	/// <summary>
	/// A mesh the LAST column draws instead of text, framed by the row's state - the original's
	/// <c>i_boxtick</c> on the buy list.
	///
	/// <para>
	/// <b>That column is not a text column, and treating it as one collides with the one beside it.</b>
	/// The buy list's third column is 51 virtual units wide, which is a tick-box and nothing else;
	/// text written into it overruns the price column to its left.
	/// The original skins it with a sprite and picks the frame from the state, which is why it can be
	/// that narrow.
	/// </para>
	/// </summary>
	internal string? StateMesh { get; init; }

	/// <summary>
	/// Which columns are bars, by column: a <see cref="ParkObjectWindow.UiStatBar"/> in the column, inset
	/// <see cref="BarInset"/> from the row's top and bottom, filled from the row's <see cref="Row.Numbers"/> in the
	/// original's 0 to 1024 (<see cref="NoBar"/> leaves it unfilled). The all-items screen's styler (<c>FUN_00494690</c>)
	/// and the all-staff screen's (<c>FUN_00496d20</c>) skin their bar cells so. Set it, then <see cref="Build"/> again:
	/// the cells are made by it.
	/// </summary>
	internal bool[] BarColumns { get; set; } = [];

	/// <summary>A bar column's texture, by column, where it is not <see cref="ParkObjectWindow.UiStatBar.RideStatBar"/>:
	/// the all-staff styler's happiness wears <c>happygrad.wct</c>.</summary>
	internal string?[] BarSkins { get; init; } = [];

	/// <summary>How far a bar cell stands in from its row's top and bottom: <c>FUN_00485e30( cell, 0, 10 )</c> on the
	/// all-items rows, <c>( cell, 0, 8 )</c> on the all-staff rows.</summary>
	internal int BarInset { get; init; } = 10;

	/// <summary>A bar column's number for a value the row has none of: the bar is left unfilled.</summary>
	internal const int NoBar = int.MinValue;

	private bool IsBar( int column ) => column < BarColumns.Length && BarColumns[column];

	/// <summary>
	/// Which columns draw a mesh in place of text, by column: the mesh's part numbered by the row's <see cref="Row.Numbers"/>
	/// for that column, and nothing for a number past its last part. The visitors' styler (<c>FUN_004939d0</c>) skins its
	/// fifth cell, a type-6 control, with <c>thoughts.MD2</c>; that control's value is its skin's state (<c>0x0066da4d</c>),
	/// and the skin shows the part its state and frame count to (<c>0x00476e80</c>).
	/// </summary>
	internal string?[] MeshColumns { get; init; } = [];

	/// <summary>How far a mesh cell stands in from its column's left and right: <c>FUN_00485e30( cell, 4, 0 )</c> on the
	/// visitors' thought.</summary>
	internal int MeshInset { get; init; }

	private string? MeshOf( int column ) => column < MeshColumns.Length ? MeshColumns[column] : null;

	/// <summary>
	/// The list's flag <c>0x80</c>: the pointer moving over a row selects it, with no press (<c>FUN_006656a0</c>, from
	/// the proc's <c>0x10001</c>), and <see cref="SelectionChanged"/> hears it as it does any other selection. Over no
	/// row the selection stays. All seven of the original's list records carry the flag (<c>docs/exe/hud.md</c>, "Who acts on
	/// the click, and who on the release"), and every list here sets this.
	/// </summary>
	internal bool SelectsUnderPointer { get; init; }

	/// <summary>The row the pointer last chose, by id, or -1.</summary>
	/// <remarks>
	/// <b>The original keeps the selection as an INDEX</b> (<c>list+0x14c</c>), so a row inserted or removed above it
	/// moves the highlight onto a different row; <see cref="_selected"/> is that index, and this names whichever row
	/// stands there now.
	/// </remarks>
	internal int Selected => _selected >= 0 && _selected < _rows.Count ? _rows[_selected].Id : -1;

	private int _selected = -1;

	// The screen slot the highlight was last placed at, -1 for none. The original places its sprite when a row is
	// selected (FUN_0066525c, 0x00665471) and a scroll never moves it, so it stays on its slot over the rows that
	// slide under it (docs/exe/hud.md, "The list's scrollbar").
	private int _highlightSlot = -1;

	/// <summary>The screen slot the highlight stands on, -1 for none; a scroll leaves it where it is.</summary>
	internal int HighlightSlot => _selected < 0 ? -1 : _highlightSlot;

	/// <summary>A row was chosen - the original's message <c>0x400</c>. Handed the row's id.</summary>
	internal Action<int>? Activated { get; set; }

	/// <summary>The selection moved - message <c>0x401</c>. Handed the row's id.</summary>
	internal Action<int>? SelectionChanged { get; set; }

	/// <summary>
	/// A row's lettering colour, asked each time the rows are pushed into their cells - message <c>0x403</c>, which
	/// <c>FUN_00663324</c> sends the list's handler with the row's id for every cell it fills (<c>0x00663499</c>). Null,
	/// or a null answer, leaves the row's own <see cref="Row.Colour"/>.
	/// </summary>
	internal Func<Row, UiColour?>? RowColour { get; init; }

	/// <summary>The original's <c>0x402</c>: a right click on the list, with the selected row's id. See <see cref="RightClickedAt"/>.</summary>
	internal Action<int>? RowRightClicked { get; set; }

	/// <summary>How far down the list the visible window starts.</summary>
	private int _scrollTop;

	/// <summary>The reusable row widgets, outer list by slot and inner by column.</summary>
	private readonly List<UiControl[]> _slots = [];

	/// <summary>How many rows fit in the row area - what sizes the widget pool.</summary>
	internal int VisibleRows => Math.Max( 1, RowArea.Height / Math.Max( RowHeight, 1 ) );

	/// <summary>Whether the list is longer than the window onto it.</summary>
	internal bool Scrolls => _rows.Count > VisibleRows;

	internal override bool TakesMouse => true;

	/// <summary>
	/// Builds the reusable slots. Called once the rects are set, because the number of them comes from
	/// the row area's height - see <see cref="VisibleRows"/>.
	/// </summary>
	internal void Build()
	{
		// ONLY the cells this built before, never every child. The original's tree makes the tab
		// group, the column headers and the scrollbar children of the LIST, so a Build that cleared
		// everything would delete controls its owner had given it - the buy screen's four tabs among
		// them, with no error anywhere.
		foreach ( var cells in _slots )
		{
			foreach ( var cell in cells )
			{
				// The window's close walks Children to let go of every text, and these are no longer in it.
				cell.ReleaseText();
				Children.Remove( cell );
			}
		}

		_slots.Clear();

		for ( var slot = 0; slot < VisibleRows; ++slot )
		{
			var top = RowArea.Top + (slot * RowHeight);
			var cells = new UiControl[Math.Max( Columns.Length, 1 )];

			for ( var column = 0; column < cells.Length; ++column )
			{
				var (left, right) = column < Columns.Length
					? Columns[column]
					: (RowArea.Left, RowArea.Right);

				var isState = StateMesh != null && column == Columns.Length - 1 && Columns.Length > 2;

				if ( IsBar( column ) )
				{
					cells[column] = Add( new ParkObjectWindow.UiStatBar
					{
						Rect = new UiRect( left, top + BarInset, right, top + RowHeight - BarInset ),
						Skin = column < BarSkins.Length && BarSkins[column] is { } skin ? skin : ParkObjectWindow.UiStatBar.RideStatBar
					} );

					continue;
				}

				if ( MeshOf( column ) is { } picture )
				{
					cells[column] = Add( new UiControl
					{
						Rect = new UiRect( left + MeshInset, top, right - MeshInset, top + RowHeight ),
						Mesh = UiMesh.Get( picture )
					} );

					continue;
				}

				cells[column] = Add( new UiControl
				{
					Rect = new UiRect( left, top, right, top + RowHeight ),
					Mesh = isState ? UiMesh.Get( StateMesh! ) : null,
					Font = RowFont,

					// The name from the left and the rest from the right, where a screen's styler sets no
					// ColumnAligns. The original's cells stand at ( 0, 1 ), from the left, until a styler's
					// FUN_0065c428 moves them (FUN_0048fc40), and most stylers set their figures ( 2, 1 ).
					TextAcross = column < ColumnAligns.Length
						? ColumnAligns[column]
						: column == 0 ? TextAlign.Start : TextAlign.End,
					TextColour = UiColour.White
				} );
			}

			_slots.Add( cells );
		}

		Refresh();
	}

	/// <summary>
	/// One column heading - the stream's <c>op 0xc</c> children, which carry a rect and nothing else.
	/// </summary>
	/// <remarks>
	/// <b>The heading TEXT is not in the layout stream at all</b>, which is why walking the stream alone
	/// leaves five unnamed boxes. Each builder fetches the child by id <c>0x10 + index</c> and hands it a
	/// UITEXT row - <c>FUN_00485b00( row, ..., sortMessage )</c> - so the headings live in code and the
	/// rects live in data. A heading is a button, and its click is the list's: <see cref="HeadingClicked"/>.
	/// <para>
	/// <b>The hire and buy lists' headings are lettered and framed by <c>FUN_00486660</c></b>: the frame
	/// <c>!cbut</c>, the text centred both ways in the font and colour handed over, and a help row. A caller that
	/// names a <paramref name="mesh"/> gets that; one that does not keeps a bare label.
	/// </para>
	/// <para>
	/// They are children of the LIST, and they survive <see cref="Build"/> because it clears only the row
	/// cells it made itself - the same reason the tab groups survive it.
	/// </para>
	/// <para>
	/// <b>Its click is <c>Select3</c> (<c>0xbd</c>) on a <see cref="Sorted"/> list</b>, by the list's style and not the heading's
	/// (<c>WindowStack.Release</c>; <c>hud.md</c>, "Which window the click sound is for").
	/// </para>
	/// </remarks>
	internal UiButton AddHeading( int column, UiRect rect, string text, string? mesh = null, int? font = null,
		UiColour? colour = null, int help = -1 )
		=> Add( new UiButton
		{
			Id = 0x10 + column,
			Rect = rect,
			Mesh = mesh == null ? null : UiMesh.Get( mesh ),
			Font = font ?? RowFont,
			TextColour = colour ?? UiColour.White,
			TextAcross = mesh != null ? TextAlign.Centre
				: column < ColumnAligns.Length ? ColumnAligns[column]
				: column == 0 ? TextAlign.Start : TextAlign.End,
			TextWraps = true,
			Text = text,
			HelpText = help,
			Clicked = () => HeadingClicked( column ),

			// PINNED TO THE LIST, rather than to whichever third of the virtual screen each heading's
			// own middle happens to land in.
			//
			// Three of the item screen's five headings have a top of 317 against the list's 318, so the
			// list does not CONTAIN them, and left to itself each would resolve an anchor of its own -
			// the last one's middle falls past the two-thirds line, so it would take the window's right
			// edge where its neighbours take the centre. On the 4:3 screen the original lays out for
			// there is no slack and every anchor gives the same answer; on a 16:9 window "Remaining
			// Life" would slide 160 pixels clear of the column it belongs to, with the rows underneath
			// still correct because they ARE inside the row area.
			PinAcross = Anchor,
			PinDown = VerticalAnchor
		} );

	/// <summary>
	/// The headings' font slot as the all-staff, all-items and visitors openers hand it to <c>FUN_00486660</c>: each
	/// screen's own word (<c>[0x00751188]</c>, <c>[0x00750d58]</c>, <c>[0x007508b8]</c>), 8 in each and written by nothing.
	/// </summary>
	internal const int HeadingFont = 8;

	/// <summary>The colour every list opener hands <c>FUN_00486660</c>: 255, 239, 0.</summary>
	internal static readonly UiColour HeadingColour = new( 255, 239, 0 );

	/// <summary>
	/// A heading as the all-staff, all-items and visitors openers make it (<c>FUN_00496620</c>, <c>FUN_00494ec0</c> and its
	/// three twins, <c>FUN_00493530</c>): <c>FUN_00486660</c>'s frame <c>!cbut</c>, <see cref="HeadingFont"/>,
	/// <see cref="HeadingColour"/> and the help row handed over.
	/// </summary>
	internal UiButton AddFramedHeading( int column, UiRect rect, string text, int help )
		=> AddHeading( column, rect, text, "!cbut", HeadingFont, HeadingColour, help );

	/// <summary>The headings, in column order: <c>mesh font help "text"</c> each, for the screens' log lines.</summary>
	internal string HeadingsCensus()
		=> string.Join( ", ", Children.OfType<UiButton>()
			.Where( heading => heading.Id >= 0x10 && heading.Id < 0x10 + 8 )
			.OrderBy( heading => heading.Id )
			.Select( heading => $"{heading.Mesh?.Name ?? "bare"} {heading.Font} {heading.HelpText} \"{heading.Text}\"" ) );

	/// <summary>The rows' height, font and how many fit, for the screens' log lines.</summary>
	internal string RowsCensus() => $"rows of {RowHeight} units in font {RowFont}, {VisibleRows} visible";

	/// <summary>Empties the list - the original's <c>FUN_006649d5</c>.</summary>
	internal void Clear()
	{
		_rows.Clear();
		_scrollTop = 0;
		_selected = -1;
		_highlightSlot = -1;
		Refresh();
	}

	/// <summary>
	/// Adds a row - <c>FUN_0066403b</c> with no parent (-1), the head chain, - and answers where it went. On a sorted list
	/// that is the original's sorted insert (<c>list+0x48 &amp; 0x10</c>): before the first row it
	/// <see cref="SortsBefore"/>, so after every row equal to it, or at the end. Nothing already in the list
	/// moves, re-sorts or is scrolled to, and the selection keeps its index. A list with no sort takes it at the end.
	/// The first row into an empty list is picked (<see cref="FirstRow"/>).
	/// </summary>
	internal int Add( Row row )
	{
		// Only the head chain is compared: a row under another stands in its parent's chain (AddUnder).
		var at = _sortColumn < 0 ? _rows.Count : _rows.FindIndex( standing => standing.Depth == 0 && SortsBefore( row, standing ) );

		if ( at < 0 )
			at = _rows.Count;

		_rows.Insert( at, row );
		FirstRow();
		SettleTop();
		Refresh();

		return at;
	}

	/// <summary>
	/// Adds a row as a child of another - <c>FUN_0066403b</c> with the insert-under of the parent's row index
	/// (<c>FUN_00664d2a</c> of its thing), and answers where it went, or -1 where the parent is not in the list (the
	/// original's adder then adds nothing, <c>0x00493e4a</c>). The child chain is sorted as the head chain is: before the
	/// first child it <see cref="SortsBefore"/>, else after the last (so after every child equal to it); the first child
	/// stands straight under the parent. The children of the row's own are left where they stand, as nothing here moves.
	/// </summary>
	internal int AddUnder( Row row, int parentId )
	{
		var parent = _rows.FindIndex( standing => standing.Id == parentId );

		if ( parent < 0 )
			return -1;

		var depth = _rows[parent].Depth + 1;
		var end = parent + 1;
		var at = -1;

		for ( ; end < _rows.Count && _rows[end].Depth >= depth; ++end )
		{
			if ( at < 0 && _rows[end].Depth == depth && _sortColumn >= 0 && SortsBefore( row, _rows[end] ) )
				at = end;
		}

		_rows.Insert( at < 0 ? end : at, row with { Depth = depth } );
		FirstRow();
		SettleTop();
		Refresh();

		return at < 0 ? end : at;
	}

	/// <summary>
	/// Whether <paramref name="row"/> stands strictly before <paramref name="other"/> under the list's sort -
	/// <c>FUN_00663edc</c>. A number column compares the two numbers; a text column compares the two texts character
	/// by character on their plain values (<c>FUN_0067c290</c>), with no case folding, so every capital stands before
	/// every small letter. Descending swaps the two. Equal rows are before neither.
	/// </summary>
	private bool SortsBefore( in Row row, in Row other )
	{
		var (first, second) = _descending ? (other, row) : (row, other);

		return _sortColumn < TextColumns.Length && TextColumns[_sortColumn]
			? string.CompareOrdinal( TextOf( first, _sortColumn ), TextOf( second, _sortColumn ) ) < 0
			: NumberOf( first, _sortColumn ) < NumberOf( second, _sortColumn );
	}

	private static string TextOf( in Row row, int column )
		=> column == 0 ? row.Name : row.Values is { } values && column - 1 < values.Count ? values[column - 1] : string.Empty;

	private int NumberOf( in Row row, int column )
		=> row.Numbers is { } numbers ? column < numbers.Count ? numbers[column] : 0
			: StateMesh != null && Columns.Length > 2 && column == Columns.Length - 1 ? row.State
			: row.Value;

	/// <summary>
	/// Gives the list its sort from a screen's kept word - <c>FUN_006659af( word, 0 )</c>, which each opener calls
	/// while the list is still empty: the column, then the direction, and nothing sorted. Each of the two that
	/// changed tells <see cref="SortChanged"/>.
	/// </summary>
	internal void SetSort( int word )
	{
		SetSortColumn( Math.Abs( word ) - 1 );
		SetDescending( word < 0 );
	}

	/// <summary><c>FUN_0066590f</c>.</summary>
	private void SetSortColumn( int column )
	{
		if ( column == _sortColumn )
			return;

		_sortColumn = column;
		SortChanged?.Invoke( SortWord );
	}

	/// <summary><c>FUN_0066594d</c>.</summary>
	private void SetDescending( bool descending )
	{
		if ( descending == _descending )
			return;

		_descending = descending;
		SortChanged?.Invoke( SortWord );
	}

	/// <summary>
	/// A heading was clicked - <c>FUN_00665a44</c>, reached from the list's proc as its child's <c>0x100</c>. The
	/// column already sorted on flips the direction; another column sets ascending, then the column. Either way the
	/// whole list is sorted again, and with a row selected <see cref="SelectionChanged"/> is told the selected index,
	/// which now names a different row.
	/// </summary>
	/// <remarks>
	/// Every list of the original's carries the sort flag <c>0x10</c>, and every screen here hands its list a sort. A
	/// list built with headings and no sort, which only a test builds, does not sort: counted.
	/// </remarks>
	internal void HeadingClicked( int column )
	{
		if ( column < 0 || column >= Math.Max( Columns.Length, 1 ) )
			return;

		if ( _sortColumn < 0 )
		{
			Unimplemented.Report( "LIST_HEADING_SORT_NOT_BUILT" );
			return;
		}

		if ( column == _sortColumn )
		{
			SetDescending( !_descending );
		}
		else
		{
			SetDescending( false );
			SetSortColumn( column );
		}

		Sort();
		Refresh();

		if ( _selected >= 0 && _selected < _rows.Count )
			SelectionChanged?.Invoke( _rows[_selected].Id );
	}

	/// <summary>
	/// The sort and the rows as they stand, for a log line: each row's id, name and number - the sorted column's
	/// where that is a number, the row's own otherwise.
	/// </summary>
	internal string Census()
	{
		var numbered = _sortColumn >= 0 && !(_sortColumn < TextColumns.Length && TextColumns[_sortColumn]);

		return $"sort {SortWord}, {_rows.Count} rows: " + string.Join( ", ",
			_rows.Select( row => $"{row.Id}{(row.Depth > 0 ? $"^{row.Depth}" : "")} {row.Name} {(numbered ? NumberOf( row, _sortColumn ) : row.Value)}" ) );
	}

	/// <summary>What each slot with a row letters, its first two cells and their colour as (R,G,B), for the screens' log lines.</summary>
	internal string CellsCensus()
		=> string.Join( ", ", _slots.Where( cells => cells[0].Visible ).Select( cells =>
			$"\"{cells[0].Text}\" \"{(cells.Length > 1 ? cells[1].Text : null)}\" ({cells[0].TextColour.R},{cells[0].TextColour.G},{cells[0].TextColour.B})" ) );

	/// <summary>
	/// Sorts the head rows, each carrying its children in the order they stand - <c>FUN_006628f4</c>: the first least row goes to the head, then each row from the third on
	/// is put before the first row it <see cref="SortsBefore"/>. So <b>equal rows keep the order they stood in</b>,
	/// in either direction.
	/// </summary>
	private void Sort()
	{
		// The sort walks the head chain alone (FUN_006628f4 follows each row's next link from the head): a row's children
		// travel with it, in the order they stand in.
		var groups = new List<List<Row>>();

		foreach ( var row in _rows )
		{
			if ( row.Depth == 0 || groups.Count == 0 )
				groups.Add( [] );

			groups[^1].Add( row );
		}

		_rows.Clear();

		var sorted = new List<List<Row>>();

		foreach ( var group in groups )
		{
			var at = sorted.FindIndex( placed => SortsBefore( group[0], placed[0] ) );

			sorted.Insert( at < 0 ? sorted.Count : at, group );
		}

		foreach ( var group in sorted )
			_rows.AddRange( group );
	}

	/// <summary>
	/// Removes the row with this id - <c>FUN_006647cb</c>. Answers the index it stood at, or -1.
	/// </summary>
	/// <remarks>
	/// <b>The selection is not shifted</b>, and nothing is told when a row above it moves, as the original sends nothing
	/// then. Only when the removed row was the selected one is another chosen: its INDEX (one less if it was the last
	/// row) is handed to <c>FUN_0066525c</c> as if it were a visible slot, and that adds the top row to it
	/// (<c>docs/exe/hud.md</c>, "Removing the selected row"). So in a scrolled list it lands the top row's count of rows
	/// further down than a slot would, and an index at or past the visible rows (<c>+0x172</c>) leaves the selection as
	/// it was, the list's <c>+0x48 &amp; 0x100</c> being clear: the same index, now the removed row's successor, nothing
	/// posted. A reselection that moves the index raises <see cref="SelectionChanged"/>, the original's <c>0x401</c>,
	/// handed -1 for none.
	/// </remarks>
	internal int Remove( int id )
	{
		var at = _rows.FindIndex( row => row.Id == id );

		if ( at < 0 )
			return -1;

		_rows.RemoveAt( at );

		if ( _selected == at )
		{
			var was = _selected;
			var slot = _rows.Count < 1 ? -1 : _selected >= _rows.Count ? _selected - 1 : _selected;

			if ( slot < 0 )
			{
				_selected = -1;
				_highlightSlot = -1;
			}
			else if ( slot < VisibleRows && slot < _rows.Count )
			{
				_selected = slot + _scrollTop;
				_highlightSlot = slot;
			}

			// Otherwise FUN_0066525c returns at once: the index and the sprite stand, and the index may be the count.

			Log.Info( $"List 0x{Id:x}: the selected row at {was} was removed (top row {_scrollTop}, {_rows.Count} rows left), "
				+ $"selected index now {_selected} on slot {_highlightSlot}" );

			// FUN_0066525c sends 0x401 when the index moves, to nothing included.
			if ( _selected != was )
				SelectionChanged?.Invoke( Selected );
		}

		SettleTop();
		Refresh();

		return at;
	}

	/// <summary>
	/// Rewrites the row at <paramref name="index"/> in place - <c>FUN_006644ea</c>. It keeps its place even when the
	/// value it was sorted on has changed.
	/// </summary>
	internal void Replace( int index, Row row )
	{
		_rows[index] = row;
		Refresh();
	}

	/// <summary>
	/// The original's add selects the first row of an empty list (<c>FUN_0066403b</c>: <c>FUN_0066525c( 0 )</c> once
	/// the count reaches 1): the visible window's first row, whose INDEX is posted with <c>0x401</c>
	/// (<c>FUN_00658ccc</c> queues it). Built where <see cref="PicksFirstRow"/> is set; the other lists count it
	/// (<c>docs/exe/hud.md</c>, "The first row is picked as a list fills").
	/// </summary>
	private void FirstRow()
	{
		if ( _rows.Count != 1 )
			return;

		if ( !PicksFirstRow )
		{
			Unimplemented.Report( "LIST_FIRST_ROW_SELECTED" );
			return;
		}

		// Slot 0 is under the visible count and the row count, so FUN_0066525c takes the top row's index, places the
		// highlight there, and posts only when that moves the selection.
		_highlightSlot = 0;

		if ( _selected == _scrollTop )
			return;

		_selected = _scrollTop;
		_posted = _selected;
	}

	/// <summary>
	/// Builds the add's pick of the first row (<see cref="FirstRow"/>) for this list. Its <c>0x401</c> is posted, so
	/// <see cref="SelectionChanged"/> hears it only when the screen calls <see cref="TakePosted"/>, after the fill.
	/// </summary>
	internal bool PicksFirstRow { get; init; }

	// The index the add's pick posted, -1 for none waiting.
	private int _posted = -1;

	/// <summary>
	/// Hands a posted pick to <see cref="SelectionChanged"/>: the row standing at the posted index now, which after a
	/// sorted fill is the row sorted first, not the one added first, as the screen's handler reads the row by the
	/// message's index (<c>0x0049b894</c>). Where no row stands there (a list emptied before the take) the original's
	/// reader <c>FUN_00664c71</c> writes nothing and the handler goes on with a stale word; here it is handed -1.
	/// </summary>
	internal void TakePosted()
	{
		if ( _posted < 0 )
			return;

		var index = _posted;
		_posted = -1;

		SelectionChanged?.Invoke( index < _rows.Count ? _rows[index].Id : -1 );
	}

	/// <summary>
	/// What <c>FUN_00664495</c> does after an add or a removal: the scrollbar's range becomes count less visible and
	/// the slider clamps the top row into it. <b>With no more rows than fit, the slider is hidden and the top row
	/// is left where it was</b> (<c>0x006644d2</c>), even past the last row; the wheel clamps it (<see cref="Scroll"/>).
	/// </summary>
	private void SettleTop()
	{
		if ( Scrolls )
			_scrollTop = Math.Clamp( _scrollTop, 0, _rows.Count - VisibleRows );
	}

	/// <summary>
	/// Pushes the visible window's rows into the slot widgets - the original's <c>FUN_00663324</c>,
	/// which is the only thing that moves data into a widget.
	/// </summary>
	internal void Refresh()
	{
		if ( !_movedByBar )
			SyncScrollbar();

		for ( var slot = 0; slot < _slots.Count; ++slot )
		{
			var index = _scrollTop + slot;
			var cells = _slots[slot];
			var has = index < _rows.Count;

			foreach ( var cell in cells )
				cell.Visible = has;

			if ( !has )
				continue;

			var row = _rows[index];

			var colour = RowColour?.Invoke( row ) ?? row.Colour ?? UiColour.White;

			foreach ( var cell in cells )
				cell.TextColour = colour;

			cells[0].Text = row.Name;

			// A child's name stands IndentStep units in for each level (FUN_0065c672( depth * 0x14, 0 )), the text's own rectangle moved.
			cells[0].TextRect = row.Depth > 0
				? new UiRect( cells[0].Rect.Left + (row.Depth * IndentStep), cells[0].Rect.Top, cells[0].Rect.Right, cells[0].Rect.Bottom )
				: null;

			// Every column after the name takes its own text where the row supplies one, which is what
			// lets a five-column staff list and a six-column visitor list exist at all. Where it does
			// not, the single Value fills the first of them - the shape the buy and hire screens push.
			var tickBox = StateMesh != null && cells.Length > 2;
			var last = tickBox ? cells.Length - 1 : cells.Length;

			for ( var column = 1; column < last; ++column )
			{
				if ( cells[column] is ParkObjectWindow.UiStatBar bar )
				{
					var units = row.Numbers is { } numbers && column < numbers.Count ? numbers[column] : NoBar;

					bar.Known = units != NoBar;
					bar.Units = bar.Known ? units : 0;
					continue;
				}

				if ( MeshOf( column ) != null )
				{
					cells[column].Frame = row.Numbers is { } parts && column < parts.Count ? parts[column] : 0;
					continue;
				}

				cells[column].Text = row.Values is { } values
					? column - 1 < values.Count ? values[column - 1] : null
					: column == 1 ? row.Value.ToString() : null;
			}

			// The last column is the tick-box: a sprite framed by the state, never text - see StateMesh.
			// A list with no StateMesh has no such column and fills that cell with text like any other.
			if ( tickBox )
			{
				cells[^1].Text = null;
				cells[^1].Frame = row.State;
				cells[^1].Visible = row.State != 0;
			}
		}
	}

	/// <summary>The scrollbar the stream gives a list (<c>op 9</c>): its track with the skin <c>!slider</c>, an arrow at either end and the thumb.</summary>
	private UiSlider? _bar;

	/// <summary>Whether the bar moved the window this very refresh: it has put its thumb where the pointer holds it, and is not told where it stands.</summary>
	private bool _movedByBar;

	/// <summary>
	/// Builds the list's scrollbar (<c>op 9</c>: a type 3 control, flags <c>0x10</c>, id 1, with the arrows <c>op 6</c> and <c>op 7</c>, ids 1 and 2,
	/// and the thumb <c>op 8</c>, id 3; <c>docs/exe/hud.md</c>, "Ops 9 and 10 are runtime-conditional"). The bar runs up and down along
	/// <paramref name="bar"/>, its value the window's top row, 0 at the top of the track and <c>count - visible</c> at its foot
	/// (<c>FUN_00664495</c>); an arrow moves it one row and a click on the track one window (<c>Slider_Callback</c>, <c>Slider_PageTowards</c>).
	/// </summary>
	/// <remarks>
	/// <b>The bar's value here is the top row negated.</b> <see cref="UiSlider"/> runs its thumb from the foot of the track (the lowest value)
	/// to the head (the highest), as the original's sliders with a positive step do; the original's list bar has the range
	/// <c>( count - visible, 0 )</c>, which turns the step negative and puts nought at the head. A range of <c>( -(count - visible), 0 )</c> is the same
	/// line on this class.
	/// <para>
	/// <paramref name="bar"/> is the control's own rectangle (<c>op 9</c>), which takes the pointer. The thumb runs along, and the skin is drawn in,
	/// the slider's text rectangle (<c>op 3</c> inside the <c>op 9</c>: <c>Slider_SetPosition</c> reads <c>+0x32</c> and <c>+0x36</c>), given as
	/// <paramref name="track"/> where it differs from <paramref name="bar"/> (the visitors' list: <c>docs/exe/hud.md</c>, "The other lists' bars").
	/// </para>
	/// </remarks>
	internal void AddScrollbar( UiRect bar, UiRect up, UiRect down, UiRect thumb, UiRect? track = null )
	{
		var run = track ?? bar;

		// A child keeps to its parent's place on a wide window only where the parent's rectangle holds it (UiControl.Add), and the stream's
		// arrows stand outside the bar's own rectangle and its thumb a little over it: so the track's picture is its own control, and the
		// bar is the rectangle holding all four parts, taking the pointer on the track alone.
		var whole = new UiRect( Math.Min( Math.Min( Math.Min( bar.Left, run.Left ), up.Left ), Math.Min( down.Left, thumb.Left ) ),
			Math.Min( Math.Min( Math.Min( bar.Top, run.Top ), up.Top ), Math.Min( down.Top, thumb.Top ) ),
			Math.Max( Math.Max( Math.Max( bar.Right, run.Right ), up.Right ), Math.Max( down.Right, thumb.Right ) ),
			Math.Max( Math.Max( Math.Max( bar.Bottom, run.Bottom ), up.Bottom ), Math.Max( down.Bottom, thumb.Bottom ) ) );

		_barSkin = Add( new UiControl { Rect = run, Mesh = UiMesh.Get( "!slider" ) } );

		_bar = Add( new UiSlider
		{
			Id = 1,
			Rect = whole,
			HitRect = bar,
			Track = run,
			Vertical = true,
			Moved = BarMoved,
			WheelTurned = ScrollByWheel
		} );

		_bar.Page = VisibleRows;
		_bar.AddThumb( new UiSliderThumb { Id = 3, Rect = thumb, Mesh = UiMesh.Get( "b_scroller" ) } );
		_bar.Add( new UiRepeatButton { Id = 1, Rect = up, Mesh = UiMesh.Get( "b_up" ), Clicked = () => Scroll( -1 ), Repeated = () => Scroll( -1 ) } );
		_bar.Add( new UiRepeatButton { Id = 2, Rect = down, Mesh = UiMesh.Get( "b_down" ), Clicked = () => Scroll( 1 ), Repeated = () => Scroll( 1 ) } );

		SyncScrollbar();
	}

	/// <summary>The track's picture, the skin <c>!slider</c>: shown and hidden with the bar.</summary>
	private UiControl? _barSkin;

	/// <summary>The scrollbar, or null for a list that has none.</summary>
	internal UiSlider? Scrollbar => _bar;

	/// <summary>Where the window and its bar stand, for the console's <c>control</c>.</summary>
	internal string ScrollCensus()
		=> $"top row {_scrollTop} of {_rows.Count} rows, {VisibleRows} visible"
			+ (_bar == null ? ", no scrollbar" : $", scrollbar {(_bar.Visible ? "shown" : "hidden")} value {_bar.Value} in ({_bar.Minimum}, {_bar.Maximum})"
				+ $" page {_bar.Page}, thumb {_bar.Thumb?.Rect.Top} to {_bar.Thumb?.Rect.Bottom}")
			+ $", selected index {_selected} (row {Selected}), sprite on slot {_highlightSlot}";

	/// <summary>
	/// <c>FUN_00664495</c> with the bar, the count, the visible rows and the window's top: shown and ranged to <c>count - visible</c> where the list is
	/// longer than its window, hidden with the range ( 0, 0 ) and the value left where it was where it is not.
	/// </summary>
	private void SyncScrollbar()
	{
		if ( _bar == null )
			return;

		_bar.Visible = Scrolls;
		_barSkin!.Visible = Scrolls;

		if ( !Scrolls )
		{
			_bar.SetRange( 0, 0 );
			return;
		}

		_bar.SetRange( -(_rows.Count - VisibleRows), 0 );
		_bar.SetValue( -_scrollTop );
	}

	/// <summary>The bar's message <c>0x800</c> (<c>FUN_00664ea3</c>): the window's top becomes the bar's value, and the rows are pushed into their cells again if it moved.</summary>
	private void BarMoved()
	{
		var top = -_bar!.Value;

		if ( top == _scrollTop )
			return;

		_scrollTop = top;
		_movedByBar = true;

		try
		{
			Refresh();
		}
		finally
		{
			_movedByBar = false;
		}
	}

	/// <summary>Moves the window onto the list, by whole rows.</summary>
	/// <remarks>
	/// A list with no more rows than fit does not move, but is still clamped back to its top: the one way a top
	/// row <see cref="SettleTop"/> left past the end comes back (<c>0x1000d</c>).
	/// </remarks>
	internal void Scroll( int rows )
	{
		if ( Scrolls )
		{
			_scrollTop += rows;

			// A list longer than its window with no bar of the stream's built is scrolled by the wheel alone.
			if ( _bar == null )
				Unimplemented.Report( "LIST_SCROLLBAR_NOT_BUILT" );
		}

		_scrollTop = Math.Clamp( _scrollTop, 0, Math.Max( 0, _rows.Count - VisibleRows ) );
		Refresh();
	}

	/// <summary>
	/// The row of <paramref name="id"/> picked and brought into view (<c>FUN_00664d2a</c>, then <c>FUN_00665739</c>): a row
	/// outside the window becomes its top, held so the window ends at the last row. Nothing for an id with no row.
	/// </summary>
	internal void Pick( int id )
	{
		var index = _rows.FindIndex( row => row.Id == id );

		if ( index < 0 )
			return;

		if ( index < _scrollTop || index >= _scrollTop + VisibleRows )
		{
			_scrollTop = index;

			if ( _rows.Count < _scrollTop + VisibleRows )
				_scrollTop = _rows.Count - VisibleRows;
		}

		Select( index );
		Refresh();
	}

	/// <summary>How far down the list the visible window starts: the index of the row in the first slot.</summary>
	internal int ScrollTop => _scrollTop;

	/// <summary>
	/// The wheel turned over the list. A notch is one row, and a notch away moves down - the same
	/// direction <see cref="UiSlider.Scroll"/> takes.
	/// </summary>
	internal void ScrollByWheel( float notches ) => Scroll( -(int)notches );

	/// <summary>
	/// A left click on the list, as the class answers <c>0x11006</c> for button 0 (<c>FUN_006655a2</c>): the row under
	/// the click's point is selected if there is one, and then, under the list's flag <c>0x80</c>, which every list
	/// record of the original carries, the selected row is activated, whichever it is (<c>0x400</c>). A press alone does nothing
	/// (<c>docs/exe/hud.md</c>, "Who acts on the click, and who on the release").
	/// </summary>
	/// <remarks>
	/// The column the point is in (<c>0x404</c>, <c>FUN_00665494</c>) is told to nobody: no screen answers it.
	/// <para>
	/// The original's selected row is the one the pointer was last over (<see cref="SelectsUnderPointer"/>), so a
	/// click that misses every row activates that one. A list that does not follow the pointer here would name the
	/// row last clicked instead, so its miss activates nothing: counted.
	/// </para>
	/// </remarks>
	internal override void LeftClickedAt( float x, float y )
	{
		if ( RowAt( x, y ) is var index and >= 0 )
		{
			Select( index );
		}
		else if ( !SelectsUnderPointer )
		{
			Unimplemented.Report( "LIST_CLICK_MISS_SELECTION_NOT_UNDER_POINTER" );
			return;
		}

		ActivateSelected();
	}

	/// <summary>
	/// The list's proc answers the pointer's move itself and returns before the base proc (<c>0x00665d22</c>), so a
	/// press on a list is a click however far the pointer goes, for either button.
	/// </summary>
	internal override bool StraySpoilsAClick => false;

	/// <summary>The double click's second press activates the selected row again (<c>FUN_006656fa</c>).</summary>
	internal override void LeftDoubleClicked() => ActivateSelected();

	private void ActivateSelected()
	{
		if ( _selected < 0 || _selected >= _rows.Count )
			return;

		// The class posts 0x400 to its parent whoever that is; a screen that answers nothing here is counted.
		if ( Activated == null )
			Unimplemented.Report( "LIST_ROW_ACTIVATED_UNANSWERED" );
		else
			Activated( _rows[_selected].Id );
	}

	/// <summary>
	/// The pointer moved over the list: under <see cref="SelectsUnderPointer"/> the row under it is selected, unless
	/// a button pressed on the list is still down (<c>0x006656ba</c>).
	/// </summary>
	internal override void PointerMoved( float x, float y, bool held )
	{
		if ( !SelectsUnderPointer || held || RowAt( x, y ) is not (var index and >= 0) )
			return;

		// The same row under the pointer again places the highlight on this slot, posting nothing (0x006652da).
		if ( index != _selected )
			Log.Info( $"List 0x{Id:x}: row {_rows[index].Id} at {index} picked under the pointer" );

		Select( index );
	}

	/// <summary>
	/// A right click on the list, as the class answers a click of any button but the left (<c>FUN_0066563d</c>, from
	/// <c>0x00665dbd</c>): the row under the click's point is selected if there is one, and then the selected row,
	/// whichever it is, is handed to <see cref="RowRightClicked"/> - the original's <c>0x402</c>. So a click that
	/// misses every row still names the row already selected, and names nothing only when none is.
	/// </summary>
	/// <remarks>
	/// The original's list opens with a row selected and highlighted (<see cref="FirstRow"/>), so there a miss before
	/// any row was chosen names that row. A list without <see cref="PicksFirstRow"/> names nothing: counted.
	/// </remarks>
	internal override void RightClickedAt( float x, float y )
	{
		if ( RowAt( x, y ) is var index and >= 0 )
			Select( index );

		if ( Selected is var id and >= 0 )
			RowRightClicked?.Invoke( id );
		else if ( _rows.Count > 0 && RowRightClicked != null )
			Unimplemented.Report( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" );
	}

	/// <summary>The index of the row under a point in window pixels, or -1 where there is none.</summary>
	private int RowAt( float x, float y )
	{
		var area = VirtualScreen.ToPixels( RowArea, Anchor, VerticalAnchor );

		if ( !area.Contains( x, y ) )
			return -1;

		// A row is as tall on the screen as it is drawn (OnDraw), and the strip left under the last whole row is no
		// row's: FUN_0066552e divides by the row's height and FUN_0066525c takes no slot past the visible count.
		var slot = (int)((y - area.Y) / Math.Max( area.Height * RowHeight / Math.Max( RowArea.Height, 1 ), 1f ));
		var index = _scrollTop + slot;

		return slot < VisibleRows && index < _rows.Count ? index : -1;
	}

	private void Select( int index )
	{
		// FUN_0066525c places the highlight on the slot even when the index does not move, and posts only when it does.
		_highlightSlot = index - _scrollTop;

		if ( index == _selected )
			return;

		_selected = index;
		SelectionChanged?.Invoke( _rows[index].Id );
	}

	/// <summary>
	/// Draws the rows, and the selected one's highlight behind them.
	/// </summary>
	/// <remarks>
	/// The original puts a <c>hilight</c> sprite over the selected row's rect rather than colouring
	/// the row widget, and the same texture is already used by <see cref="UiEdit"/> for its selection.
	/// </remarks>
	protected override void OnDraw()
	{
		base.OnDraw();

		// The sprite shows while a row is selected, on the slot it was placed at, whichever row is under it now.
		if ( HighlightSlot < 0 )
			return;

		var top = RowArea.Top + (HighlightSlot * RowHeight);
		var box = VirtualScreen.ToPixels(
			new UiRect( RowArea.Left, top, RowArea.Right, top + RowHeight ), Anchor, VerticalAnchor );

		_highlight ??= new Texture( "ui/textures/hilight.wct" );
		Material.UI.Set( "Color", _highlight );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( box.X, Screen.Height - box.Y - box.Height, box.Width, box.Height ), Material.UI );
	}

	private static Texture? _highlight;
}
