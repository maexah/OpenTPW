namespace OpenTPW.UI;

/// <summary>
/// The screen Load Park and Save Park share, the stream at <c>0x007523f0</c>: the frame, the title, the list of the
/// player's saved parks for this theme and the cancel button (<c>docs/exe/saves.md</c>, "One screen, two uses").
/// The stream's OK button and name box are Save's alone (<see cref="ParkSaveScreen"/>); Load hides them.
///
/// <para>
/// It is loaded modal, and each opener pauses the game as a message box does and closes the park screen that is
/// open.
/// </para>
/// <para>
/// <b>The list is the folder</b>, read afresh as the screen opens and again at each click
/// (<see cref="SaveFolder.SavedParks"/>): the name, then the file's last write in local time, UITEXT 448's date and
/// the hour and minute. It has no headings and no sort; a row's id is its place in the folder's list. A list longer than
/// its window shows the stream's scrollbar (<see cref="UiList.AddScrollbar"/>).
/// </para>
/// </summary>
internal abstract class ParkFileScreen : UiWindow
{
	/// <summary>The font of the rows and of Save's name box, <c>FUN_00485a70( 7 )</c>.</summary>
	protected const int RowFont = 7;

	protected string Theme { get; }

	protected UiControl Frame { get; }

	protected UiList List { get; }

	protected ParkFileScreen( WindowStack stack, string theme, UIStrings title ) : base( stack )
	{
		Theme = theme;

		Modal = true;

		// FUN_004092a0( 0, 0 ) as it opens and FUN_00409300 as it goes (0x0049efba, 0x0049f29f; the handlers' 0x14).
		Pauses = true;

		// FUN_00485b40, before the tree is loaded (0x0049efc9, 0x0049f2ae).
		ClosesParkScreen = true;

		Root = Backdrop();

		Frame = Root.Add( new UiControl
		{
			Id = 0x23bcdf,
			Rect = new UiRect( 248, 30, 1800, 1007 ),
			Mesh = UiMesh.Get( "w_med" )
		} );

		// The title is lettered by UI_SetTitle (0x00485d20), as the options screen's is: font 5 in (234, 239, 102),
		// centred in the stream's rect (746 to 1218) widened by half its width either side.
		Frame.Add( new UiControl
		{
			Id = 0x23bce1,
			Rect = new UiRect( 510, 83, 1454, 162 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			Text = Localization.Get( title )
		} );

		List = Frame.Add( new UiList
		{
			Id = 0x23bce0,
			Rect = new UiRect( 341, 203, 1614, 772 ),
			Mesh = UiMesh.Get( "f_load" ),
			RowArea = new UiRect( 371, 229, 1511, 733 ),
			Columns = [(371, 1015), (1041, 1511)],

			// Both columns are text (FUN_006636b2( 0, 0 ), ( 1, 0 )), and the row factory sets the second from the
			// right (FUN_0065c428( 2, 1 ), in FUN_0049f0b0).
			TextColumns = [true, true],
			ColumnAligns = [TextAlign.Start, TextAlign.End],
			RowFont = RowFont,
			RowHeight = UiList.RowHeightOf( RowFont ),

			// The list's flags carry 0x80, and not 0x10 (0x81), so it is not Sorted.
			SelectsUnderPointer = true,
			Activated = RowClicked
		} );

		Frame.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1671, 818, 1754, 901 ),
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		List.Build();

		// The stream's scrollbar, a child of the list (op 9, with the arrows op 7 and op 6 and the thumb op 8): the track
		// 1528, 285, 1587, 691 under the skin !slider, the thumb's first place the stream's (the bar puts it where the top row belongs).
		List.AddScrollbar( new UiRect( 1528, 285, 1587, 691 ), up: new UiRect( 1528, 224, 1588, 284 ),
			down: new UiRect( 1528, 696, 1588, 756 ), thumb: new UiRect( 1520, 433, 1587, 500 ) );
	}

	/// <summary>A click on a row, the list's <c>0x400</c>, with the row's id.</summary>
	protected abstract void RowClicked( int row );

	/// <summary>The saved parks of whoever is playing, for this theme.</summary>
	/// <remarks>
	/// With nobody playing, which only the debug console's <c>park</c> reaches, there is no folder and the list is
	/// empty; the original has a player on every entry.
	/// </remarks>
	protected IReadOnlyList<SaveFolder.SavedPark> Folder()
		=> Players.Roster.Current is { } player ? SaveFolder.SavedParks( player.Slot, player.Name, Theme ) : [];

	/// <summary>
	/// One row an entry, its id the entry's place in the folder's list (<c>FUN_0049ec80</c> for Load,
	/// <c>FUN_0049edf0</c> for Save, alike).
	/// </summary>
	protected void Fill( string screen )
	{
		var parks = Folder();

		for ( var index = 0; index < parks.Count; ++index )
			List.Add( new UiList.Row( index, parks[index].Name, 0, Values: [DateOf( parks[index].Written )] ) );

		Log.Info( $"{screen}: {parks.Count} saved parks for {Theme}, rows of {List.RowHeight} units"
			+ string.Concat( parks.Select( park => $"; '{park.Name}' {DateOf( park.Written )}" ) ) );
	}

	/// <summary>
	/// A save's date as its row shows it: UITEXT 448 with the day as parameter 16, the month as 17 and the year as
	/// 18, none padded, then a space and the hour and the minute, two digits each (<c>0x00752564</c>).
	/// </summary>
	internal static string DateOf( DateTime written )
		=> Localization.Format( 448, (16, $"{written.Day}"), (17, $"{written.Month}"), (18, $"{written.Year}") )
			+ $" {written.Hour:00}:{written.Minute:00}";
}
