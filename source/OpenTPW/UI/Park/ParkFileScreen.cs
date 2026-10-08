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
/// the hour and minute. It has no headings and no sort; a row's id is its place in the folder's list.
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
			RowHeight = RowHeightFor( UiFonts.Get( RowFont )?.LineHeight, UiFonts.SetScreenHeight ),

			// The list's flags carry 0x80.
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
	}

	/// <summary>A click on a row, the list's <c>0x400</c>, with the row's id.</summary>
	protected abstract void RowClicked( int row );

	/// <summary>
	/// A row's height on the virtual screen, as the row factory works it out from the font it letters the row in
	/// (<c>FUN_0049f0b0</c>): the font's height times <c>0x600</c> over the screen's height, plus 6. The font here is
	/// one of a set drawn for a screen height of its own, so that height stands for the screen's.
	/// </summary>
	internal static int RowHeightFor( int? fontHeight, int screenHeight )
		=> fontHeight is { } height && screenHeight > 0 ? (height * 0x600 / screenHeight) + 6 : 44;

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

		// The stream's scrollbar (control 1, b_up, b_scroller and b_down) is not built: the wheel scrolls the list.
		if ( List.Scrolls )
			Unimplemented.Report( "LOAD_PARK_LIST_SCROLLBAR" );

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
