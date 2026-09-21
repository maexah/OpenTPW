namespace OpenTPW.UI;

/// <summary>
/// A scrolling multi-column list - the original's control <b>type 7</b>, and the one widget both the
/// buy and the hire screen are built on.
///
/// <para>
/// <b>Nothing in this interface had one.</b> The only scrolling thing in the tree is
/// <see cref="UiSlider"/>, which is a value between two ends, and the nearest thing to a list is the
/// game menu, which measures its items and stacks them without scrolling.
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
	/// <b><see cref="Values"/> is what makes the remarks above true rather than aspirational.</b> This
	/// record said a row carried "a value per column" while being fixed at one, so a screen wanting
	/// five columns - the staff list - could not be expressed at all. <see cref="Value"/> is kept as
	/// the first of them, because two screens already push exactly one and reading them is easier than
	/// rewriting them.
	/// </remarks>
	internal readonly record struct Row( int Id, string Name, int Value, int State = 0,
		IReadOnlyList<string>? Values = null );

	private readonly List<Row> _rows = [];

	/// <summary>Every row the list holds, in the order it was given them.</summary>
	internal IReadOnlyList<Row> Rows => _rows;

	/// <summary>
	/// Where the rows are drawn, which the original carries as a rect of its own - stream op <c>0xa</c>,
	/// distinct from the control's rect and from its text rect. On both shipped screens those happen to
	/// hold the same numbers, so conflating them looks right here and breaks elsewhere.
	/// </summary>
	internal UiRect RowArea { get; init; }

	/// <summary>
	/// The left and right edge of each column on the virtual screen, from the stream's op <c>0xb</c>.
	/// The name is column nought; the rest are numbers.
	/// </summary>
	internal (int Left, int Right)[] Columns { get; init; } = [];

	/// <summary>
	/// How tall a row is, in virtual units. <b>A choice, and marked as one.</b> The original derives it
	/// from the measured height of the first row widget its factory builds -
	/// <c>(fontHeight * 0x600) / screenHeight + 6</c> - which is a runtime number this cannot reproduce
	/// without building a row to measure first.
	/// </summary>
	internal int RowHeight { get; init; } = 44;

	/// <summary>Which font the rows are lettered in.</summary>
	internal int RowFont { get; init; } = 6;

	/// <summary>
	/// Which way each column's text is pushed, where the default is not what the screen wants. Empty
	/// leaves the default: the name reads from the left and every number from the right.
	/// </summary>
	/// <remarks>
	/// <b>The default is not a rule, and treating it as one gets one screen wrong.</b> The original
	/// sets this per column, through <c>FUN_006636b2( column, rightAligned )</c> - and the visitor
	/// list calls it with <b>1 for all six</b>, its name column included, where the staff and item
	/// lists use 0 for the name and 1 for the rest. So a hardcoded "column nought reads from the
	/// left" is right three times and wrong once.
	/// </remarks>
	internal TextAlign[] ColumnAligns { get; init; } = [];

	/// <summary>
	/// A mesh the LAST column draws instead of text, framed by the row's state - the original's
	/// <c>i_boxtick</c> on the buy list.
	///
	/// <para>
	/// <b>That column is not a text column, and treating it as one collides with the one beside it.</b>
	/// The buy list's third column is 51 virtual units wide, which is a tick-box and nothing else;
	/// writing "owned" into it overran the price column to its left and read as "500owned" on screen.
	/// The original skins it with a sprite and picks the frame from the state, which is why it can be
	/// that narrow.
	/// </para>
	/// </summary>
	internal string? StateMesh { get; init; }

	/// <summary>The row the pointer last chose, by id, or -1.</summary>
	internal int Selected { get; private set; } = -1;

	/// <summary>A row was chosen - the original's message <c>0x400</c>. Handed the row's id.</summary>
	internal Action<int>? Activated { get; set; }

	/// <summary>The selection moved - message <c>0x401</c>. Handed the row's id.</summary>
	internal Action<int>? SelectionChanged { get; set; }

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
		// everything would delete controls its owner had given it - which is exactly what happened:
		// the buy screen parented four tabs to the list, called Build, and the tabs vanished with no
		// error anywhere. The screen drew perfectly and had no tabs on it.
		foreach ( var cells in _slots )
		{
			foreach ( var cell in cells )
				Children.Remove( cell );
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

				cells[column] = Add( new UiControl
				{
					Rect = new UiRect( left, top, right, top + RowHeight ),
					Mesh = isState ? UiMesh.Get( StateMesh! ) : null,
					Font = RowFont,

					// The name reads from the left and the numbers from the right, which is how the
					// original's own columns are skinned - a label for column nought and a right
					// aligned value cell for the rest. A screen whose own layout data says otherwise
					// overrides it per column; see ColumnAligns.
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
	/// rects live in data. The sort message each carries is the other half of that call and is not
	/// reproduced here; these headings label, they do not sort.
	/// <para>
	/// They are children of the LIST, and they survive <see cref="Build"/> because it clears only the row
	/// cells it made itself - the same reason the tab groups survive it.
	/// </para>
	/// </remarks>
	internal UiControl AddHeading( int column, UiRect rect, string text )
		=> Add( new UiControl
		{
			Id = 0x10 + column,
			Rect = rect,
			Font = RowFont,
			TextColour = UiColour.White,
			TextAcross = column < ColumnAligns.Length
				? ColumnAligns[column]
				: column == 0 ? TextAlign.Start : TextAlign.End,
			TextWraps = true,
			Text = text,

			// PINNED TO THE LIST, rather than to whichever third of the virtual screen each heading's
			// own middle happens to land in.
			//
			// Three of the item screen's five headings have a top of 317 against the list's 318, so the
			// list does not CONTAIN them, so each resolved an anchor of its own - and the last one's
			// middle falls past the two-thirds line, so it took the window's right edge where its
			// neighbours took the centre. On the 4:3 screen the original lays out for there is no slack
			// and every anchor gives the same answer; on a 16:9 window it slid "Remaining Life" 160
			// pixels clear of the column it belongs to, with the rows underneath still correct because
			// they ARE inside the row area. Photographed before it was understood.
			PinAcross = Anchor,
			PinDown = VerticalAnchor
		} );

	/// <summary>Empties the list - the original's <c>FUN_006649d5</c>.</summary>
	internal void Clear()
	{
		_rows.Clear();
		_scrollTop = 0;
		Selected = -1;
		Refresh();
	}

	/// <summary>Adds a row at the end - <c>FUN_0066403b</c> with an insert-after of -1.</summary>
	internal void Add( Row row )
	{
		_rows.Add( row );
		Refresh();
	}

	/// <summary>
	/// Pushes the visible window's rows into the slot widgets - the original's <c>FUN_00663324</c>,
	/// which is the only thing that moves data into a widget.
	/// </summary>
	internal void Refresh()
	{
		var highest = Math.Max( 0, _rows.Count - VisibleRows );

		_scrollTop = Math.Clamp( _scrollTop, 0, highest );

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

			cells[0].Text = row.Name;

			// Every column after the name takes its own text where the row supplies one, which is what
			// lets a five-column staff list and a six-column visitor list exist at all. Where it does
			// not, the single Value fills the first of them - the shape the buy and hire screens push.
			var tickBox = StateMesh != null && cells.Length > 2;
			var last = tickBox ? cells.Length - 1 : cells.Length;

			for ( var column = 1; column < last; ++column )
			{
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

	/// <summary>Moves the window onto the list, by whole rows.</summary>
	internal void Scroll( int rows )
	{
		if ( !Scrolls )
			return;

		_scrollTop += rows;
		Refresh();
	}

	/// <summary>
	/// The wheel turned over the list. A notch is one row, and a notch away moves down - the same
	/// direction <see cref="UiSlider.Scroll"/> takes.
	/// </summary>
	internal void ScrollByWheel( float notches ) => Scroll( -(int)notches );

	/// <summary>
	/// A press picks the row under the pointer and activates it.
	/// </summary>
	/// <remarks>
	/// <b>The original sends both messages, and a press-and-release sends the first one twice.</b>
	/// With the list's flag <c>0x80</c> set - which both screens set - <c>0x400</c> is raised on the
	/// press and again on the release; the buy screen survives that only because its first handler
	/// closes the screen and the second finds the tree gone. Firing once, on the press, is the
	/// behaviour that arrangement produces and is what this does rather than reproducing a double
	/// message that only works by accident.
	/// </remarks>
	internal override void PointerPressed( float x, float y )
	{
		var area = VirtualScreen.ToPixels( RowArea, Anchor, VerticalAnchor );

		if ( !area.Contains( x, y ) )
			return;

		var slot = (int)((y - area.Y) / Math.Max( area.Height / VisibleRows, 1f ));
		var index = _scrollTop + Math.Clamp( slot, 0, VisibleRows - 1 );

		if ( index < 0 || index >= _rows.Count )
			return;

		var id = _rows[index].Id;

		if ( id != Selected )
		{
			Selected = id;
			SelectionChanged?.Invoke( id );
		}

		Activated?.Invoke( id );
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

		if ( Selected < 0 )
			return;

		var index = _rows.FindIndex( row => row.Id == Selected );

		if ( index < _scrollTop || index >= _scrollTop + VisibleRows )
			return;

		var slot = index - _scrollTop;
		var top = RowArea.Top + (slot * RowHeight);
		var box = VirtualScreen.ToPixels(
			new UiRect( RowArea.Left, top, RowArea.Right, top + RowHeight ), Anchor, VerticalAnchor );

		_highlight ??= new Texture( "ui/textures/hilight.wct" );
		Material.UI.Set( "Color", _highlight );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( box.X, Screen.Height - box.Y - box.Height, box.Width, box.Height ), Material.UI );
	}

	private static Texture? _highlight;
}
