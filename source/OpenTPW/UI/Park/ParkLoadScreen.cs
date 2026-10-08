namespace OpenTPW.UI;

/// <summary>
/// Load Park, the park menu's first row: the player's saved parks for this theme, one row each, and a click on a row
/// loads it over the running park with no question (<c>FUN_0049efb0</c>; <c>docs/exe/saves.md</c>, "One screen, two
/// uses" to "What the handlers answer").
///
/// <para>
/// <b>The layout</b> is the stream at <c>0x007523f0</c>, which Save Park shares: the frame, the cancel button, the
/// list, the title, and an OK button and a name box that Load hides (<c>0x0049f02a</c>, <c>0x0049f043</c>) and this
/// does not build. It is loaded modal, and the opener pauses the game as a message box does and closes the park
/// screen that is open.
/// </para>
/// <para>
/// <b>The list is the folder</b>, read afresh as the screen opens and again at the click
/// (<see cref="SaveFolder.SavedParks"/>): the name, then the file's last write in local time, UITEXT 448's date and
/// the hour and minute. It has no headings and no sort; a row's id is its place in the folder's list.
/// </para>
/// <para>
/// <b>It takes no keys</b>: the handler <c>FUN_0049e880</c> answers the cancel button, a row's click and its own
/// closing, and hands the rest to the modal base, which drops them.
/// </para>
/// </summary>
internal sealed class ParkLoadScreen : UiWindow
{
	/// <summary>The rows' font, the row factory's <c>FUN_00485a70( 7 )</c>.</summary>
	private const int RowFont = 7;

	private readonly string _theme;
	private readonly UiList _list;

	public ParkLoadScreen( WindowStack stack, string theme ) : base( stack )
	{
		_theme = theme;

		Modal = true;

		// FUN_004092a0( 0, 0 ) as it opens and FUN_00409300 as it goes (0x0049efba; the handler's 0x14).
		Pauses = true;

		// FUN_00485b40, before the tree is loaded (0x0049efc9).
		ClosesParkScreen = true;

		Root = Backdrop();

		var frame = Root.Add( new UiControl
		{
			Id = 0x23bcdf,
			Rect = new UiRect( 248, 30, 1800, 1007 ),
			Mesh = UiMesh.Get( "w_med" )
		} );

		// The title is lettered by UI_SetTitle (0x00485d20), as the options screen's is: font 5 in (234, 239, 102),
		// centred in the stream's rect (746 to 1218) widened by half its width either side.
		frame.Add( new UiControl
		{
			Id = 0x23bce1,
			Rect = new UiRect( 510, 83, 1454, 162 ),
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			Text = Localization.Get( UIStrings.LoadPark )
		} );

		_list = frame.Add( new UiList
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
			Activated = Load
		} );

		frame.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1671, 818, 1754, 901 ),
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Fill();
	}

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
	private IReadOnlyList<SaveFolder.SavedPark> Folder()
		=> Players.Roster.Current is { } player ? SaveFolder.SavedParks( player.Slot, player.Name, _theme ) : [];

	/// <summary>One row an entry, its id the entry's place in the folder's list (<c>FUN_0049ec80</c>).</summary>
	private void Fill()
	{
		var parks = Folder();

		for ( var index = 0; index < parks.Count; ++index )
			_list.Add( new UiList.Row( index, parks[index].Name, 0, Values: [DateOf( parks[index].Written )] ) );

		// The stream's scrollbar (control 1, b_up, b_scroller and b_down) is not built: the wheel scrolls the list.
		if ( _list.Scrolls )
			Unimplemented.Report( "LOAD_PARK_LIST_SCROLLBAR" );

		Log.Info( $"Load Park: {parks.Count} saved parks for {_theme}, rows of {_list.RowHeight} units"
			+ string.Concat( parks.Select( park => $"; '{park.Name}' {DateOf( park.Written )}" ) ) );
	}

	/// <summary>
	/// A save's date as its row shows it: UITEXT 448 with the day as parameter 16, the month as 17 and the year as
	/// 18, none padded, then a space and the hour and the minute, two digits each (<c>0x00752564</c>).
	/// </summary>
	internal static string DateOf( DateTime written )
		=> Localization.Format( 448, (16, $"{written.Day}"), (17, $"{written.Month}"), (18, $"{written.Year}") )
			+ $" {written.Hour:00}:{written.Minute:00}";

	/// <summary>
	/// A click on a row (<c>0x400</c>): the folder is read again, the entry at the row's place is loaded, and the
	/// screen closes. A place the folder no longer has loads nothing.
	/// </summary>
	/// <remarks>
	/// <b>A deviation.</b> The original takes the running park down where it stands and reads the file over it, with
	/// no loading screen and no change of scene (<c>FUN_00414d40</c>; <c>docs/exe/saves.md</c>, "The load"). Here the
	/// park's scene is ended and built again from the file behind the loading screen, as Restart Park's is.
	/// </remarks>
	private void Load( int row )
	{
		var parks = Folder();

		Stack.Close( this );

		if ( row < 0 || row >= parks.Count )
		{
			Log.Info( $"Load Park: row {row} is no longer in the folder, so nothing is loaded" );
			return;
		}

		Log.Info( $"Load Park: loading '{parks[row].Name}' from {parks[row].Path}" );
		Game.RequestParkLoad( _theme, parks[row].Path );
	}
}
