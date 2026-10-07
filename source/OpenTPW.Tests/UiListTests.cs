using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The list widget kept current in place, as the original's all-visitors list is (<c>docs/exe/hud.md</c>, "How
/// allpeeps keeps itself current"): rows rewritten where they stand, added in sort order and removed, with the top
/// row moved only as the original's slider moves it. Nothing here needs the game or a graphics device.
/// </summary>
[TestClass]
public class UiListTests
{
	/// <summary>Ten rows fit: a row area 440 tall at the default 44.</summary>
	private static UiList ListOf( params int[] keys )
	{
		var list = new UiList { RowArea = new UiRect( 0, 0, 100, 440 ), Columns = [(0, 100)] };

		list.Build();
		list.SetSort( 1 );

		for ( var at = 0; at < keys.Length; ++at )
			list.Add( Row( 100 + at, keys[at] ) );

		return list;
	}

	/// <summary>The virtual screen at one to one, so a press lands in the slot its virtual y names.</summary>
	[TestInitialize]
	public void OneToOne() => Screen.Size = new Point2( 2048, 1536 );

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = new Point2( 1280, 720 );

	private static void Press( UiList list, int slot ) => list.LeftClickedAt( 50, (slot * 44) + 22 );

	private static UiList.Row Row( int id, int key ) => new( id, $"{key}", key );

	private static int Times( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static int[] Keys( UiList list ) => list.Rows.Select( row => row.Value ).ToArray();

	/// <summary>
	/// <b>A right click names the selected row, having first selected the one under it</b> (<c>FUN_0066563d</c>): so a
	/// click that misses every row names the row chosen before, and with none chosen names nothing and is counted.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the row under the click not selected first; a miss naming nothing though a row is selected; a
	/// miss with nothing selected naming the first row uncounted.
	/// </remarks>
	[TestMethod]
	public void ARightClickNamesTheSelectedRowAfterSelectingTheOneUnderIt()
	{
		var list = ListOf( 1, 2, 3 );
		var named = new System.Collections.Generic.List<int>();
		var selections = 0;

		list.RowRightClicked = named.Add;
		list.SelectionChanged = _ => ++selections;

		var before = Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" );

		list.RightClickedAt( 50, (6 * 44) + 22 );
		Assert.AreEqual( 0, named.Count, "a miss with no row chosen names nothing" );
		Assert.AreEqual( before + 1, Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" ), "and is counted" );

		list.RightClickedAt( 50, (1 * 44) + 22 );
		CollectionAssert.AreEqual( new[] { 101 }, named, "the row under the click" );
		Assert.AreEqual( 101, list.Selected, "which it selects" );
		Assert.AreEqual( 1, selections );

		list.RightClickedAt( 50, (6 * 44) + 22 );
		CollectionAssert.AreEqual( new[] { 101, 101 }, named, "a miss names the row still selected" );

		Press( list, 2 );
		list.RightClickedAt( 200, 22 );
		CollectionAssert.AreEqual( new[] { 101, 101, 102 }, named, "beside the rows too" );
		Assert.AreEqual( before + 1, Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" ) );
	}

	/// <summary>
	/// <b>The strip left under the last whole row is no row's</b>, and a row is as tall under the pointer as it is
	/// drawn (<c>FUN_0066552e</c>, <c>FUN_0066525c</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the slot clamped onto the last row; the area divided evenly among the rows.</remarks>
	[TestMethod]
	public void TheStripUnderTheLastWholeRowIsNoRows()
	{
		// Ten whole rows and 30 over, eleven rows in the list.
		var list = new UiList { RowArea = new UiRect( 0, 0, 100, 470 ), Columns = [(0, 100)] };
		list.Build();

		for ( var at = 0; at < 11; ++at )
			list.Add( Row( 100 + at, at ) );

		Assert.AreEqual( 10, list.VisibleRows );

		list.LeftClickedAt( 50, 455 );
		Assert.AreEqual( -1, list.Selected, "the strip selects nothing" );

		list.LeftClickedAt( 50, 439 );
		Assert.AreEqual( 109, list.Selected, "the tenth row ends at 440, where it is drawn to" );

		list.LeftClickedAt( 50, 396 );
		Assert.AreEqual( 109, list.Selected, "and begins at 396" );

		list.LeftClickedAt( 50, 395 );
		Assert.AreEqual( 108, list.Selected );
	}

	[TestMethod]
	public void ARewriteKeepsTheScrollAndTheOrder()
	{
		var list = ListOf( Enumerable.Range( 1, 20 ).ToArray() );

		list.Scroll( 4 );
		Assert.AreEqual( 4, list.ScrollTop );

		// Every value changed, the first row's to the greatest: nothing moves (0x00493270).
		for ( var index = list.Rows.Count - 1; index >= 0; --index )
			list.Replace( index, Row( list.Rows[index].Id, 100 - index ) );

		Assert.AreEqual( 4, list.ScrollTop, "the view stays where it was scrolled" );
		CollectionAssert.AreEqual( Enumerable.Range( 0, 20 ).Select( at => 100 - at ).ToArray(), Keys( list ),
			"each row keeps its place, unsorted" );
	}

	[TestMethod]
	public void AnInsertGoesAfterEveryRowNotGreater()
	{
		var list = ListOf( 3, 1, 2, 2 );

		CollectionAssert.AreEqual( new[] { 1, 2, 2, 3 }, Keys( list ) );

		Assert.AreEqual( 3, list.Add( Row( 1, 2 ) ), "after both 2s (FUN_00663edc is strictly less)" );
		Assert.AreEqual( 0, list.Add( Row( 2, 0 ) ), "a nought, a guest not yet through the gate, at the top" );
		Assert.AreEqual( 6, list.Add( Row( 3, 9 ) ), "the greatest at the end" );
	}

	[TestMethod]
	public void AnInsertDoesNotScroll()
	{
		var list = ListOf( Enumerable.Range( 1, 20 ).ToArray() );

		list.Scroll( 5 );
		list.Add( Row( 1, 0 ) );

		Assert.AreEqual( 5, list.ScrollTop, "the top row is not written by an insert" );
	}

	[TestMethod]
	public void ARemovalNearTheBottomPullsTheViewUp()
	{
		var list = ListOf( Enumerable.Range( 1, 20 ).ToArray() );

		list.Scroll( 10 );
		Assert.AreEqual( 10, list.ScrollTop, "scrolled to the end" );

		Assert.AreEqual( 19, list.Remove( 119 ) );
		Assert.AreEqual( 9, list.ScrollTop, "the slider clamps the top row to count less visible" );

		list.Scroll( -5 );
		list.Remove( 100 );
		Assert.AreEqual( 4, list.ScrollTop, "a removal with room below moves nothing" );
	}

	[TestMethod]
	public void WithNoMoreRowsThanFitTheTopRowIsLeft()
	{
		var list = ListOf( Enumerable.Range( 1, 11 ).ToArray() );

		list.Scroll( 1 );
		list.Remove( 100 );

		Assert.AreEqual( 10, list.Rows.Count );
		Assert.AreEqual( 1, list.ScrollTop, "the slider is disabled and never repositioned (0x006644d2)" );

		list.ScrollByWheel( 0 );
		Assert.AreEqual( 0, list.ScrollTop, "the wheel clamps it" );
	}

	[TestMethod]
	public void RemovingAnAbsentRowChangesNothing()
	{
		var list = ListOf( 1, 2, 3 );

		Assert.AreEqual( -1, list.Remove( 999 ) );
		Assert.AreEqual( 3, list.Rows.Count );
	}

	[TestMethod]
	public void TheSelectionIsAnIndexThatAnInsertAboveDoesNotShift()
	{
		var list = ListOf( 10, 20, 30, 40 );

		Press( list, 2 );
		Assert.AreEqual( 102, list.Selected, "the row with 30" );

		list.Add( Row( 1, 5 ) );
		Assert.AreEqual( 101, list.Selected, "the highlight stays on index 2, now the row with 20" );

		list.Remove( 1 );
		Assert.AreEqual( 102, list.Selected, "and a removal above moves it back onto 30" );
	}

	[TestMethod]
	public void RemovingTheSelectedRowChoosesBySlot()
	{
		var list = ListOf( Enumerable.Range( 1, 20 ).ToArray() );

		var told = new System.Collections.Generic.List<int>();
		list.SelectionChanged = told.Add;

		Press( list, 3 );
		Assert.AreEqual( 103, list.Selected );

		list.Remove( 103 );
		Assert.AreEqual( 104, list.Selected, "at top 0 the same index: the next row" );
		CollectionAssert.AreEqual( new[] { 103 }, told, "the index did not move, so nothing is told" );

		list.Scroll( 5 );
		Press( list, 1 );
		Assert.AreEqual( 107, list.Selected, "index 6, in slot 1" );

		list.Remove( 107 );
		Assert.AreEqual( 113, list.Selected, "index 6 taken as a slot and the top row 5 added: index 11" );
		Assert.AreEqual( 113, told[^1], "and told (0x401)" );

		var last = list.Rows[^1].Id;

		list.Scroll( 10 );
		Press( list, list.Rows.Count - 1 - list.ScrollTop );
		Assert.AreEqual( last, list.Selected, "the last row" );

		var counted = Times( "LIST_RESELECT_PAST_THE_WINDOW" );

		list.Remove( last );
		Assert.AreEqual( -1, list.Selected, "one less than the last is past the window: dropped" );
		Assert.AreEqual( -1, told[^1], "and told of none" );
		Assert.AreEqual( counted + 1, Times( "LIST_RESELECT_PAST_THE_WINDOW" ), "and counted" );
	}

	/// <summary>A two-column list as the hire screen's: the name text, the wage a number.</summary>
	private static UiList Hire( int word, params (int Slot, string Name, int Wage)[] people )
	{
		var list = new UiList
		{
			RowArea = new UiRect( 0, 0, 100, 440 ),
			Columns = [(0, 60), (60, 100)],
			TextColumns = [true, false]
		};

		list.Build();
		list.SetSort( word );

		foreach ( var person in people )
			list.Add( new UiList.Row( person.Slot, person.Name, person.Wage ) );

		return list;
	}

	private static string[] Names( UiList list ) => list.Rows.Select( row => row.Name ).ToArray();

	/// <summary>
	/// Six entertainers in slot order. The three at 48 are the ones Q130d read in the original, slots 0, 8 and 13
	/// (<c>docs/exe/hud.md</c>, "A list's order"); the three at 60 and their slots are the test's own.
	/// </summary>
	private static readonly (int Slot, string Name, int Wage)[] Entertainers =
	[
		(0, "Shintaro Kanaoya", 48), (3, "Simon Harris", 60), (4, "Chris Killpack", 60),
		(8, "Simon Carter", 48), (11, "Marcus Iremonger", 60), (13, "Duncan Kershaw", 48)
	];

	/// <summary>
	/// <b>An add on a text column is the sorted insert on plain character values</b> (<c>FUN_00663edc</c>,
	/// <c>FUN_0067c290</c>): no case folding, so a capital stands before every small letter, and a row equal to one
	/// standing goes after it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the compare folding case; an equal row put before its equals; the add at the end.</remarks>
	[TestMethod]
	public void ATextColumnSortsOnPlainCharacterValues()
	{
		var list = Hire( 1, (0, "bob", 1), (1, "Zed", 2), (2, "Bob", 3), (3, "Zed", 4), (4, "alan", 5) );

		CollectionAssert.AreEqual( new[] { "Bob", "Zed", "Zed", "alan", "bob" }, Names( list ) );
		CollectionAssert.AreEqual( new[] { 2, 1, 3, 4, 0 }, list.Rows.Select( row => row.Id ).ToArray(),
			"the second Zed after the first" );
	}

	/// <summary>
	/// <b>A heading's click sorts the whole list and keeps equal rows as they stood</b> (<c>FUN_00665a44</c>,
	/// <c>FUN_006628f4</c>), where a fresh fill leaves them in the order added: the original's six entertainers,
	/// as Q130d read them.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the re-sort not stable; the click sorting nothing; the wage compared as text; a fresh fill
	/// keeping name order among equals; a descending sort turning equal rows round.
	/// </remarks>
	[TestMethod]
	public void AHeadingsClickKeepsEqualRowsAsTheyStood()
	{
		var list = Hire( 1, Entertainers );

		CollectionAssert.AreEqual( new[]
		{
			"Chris Killpack", "Duncan Kershaw", "Marcus Iremonger", "Shintaro Kanaoya", "Simon Carter", "Simon Harris"
		}, Names( list ), "name order, which is not slot order" );

		list.HeadingClicked( 1 );

		CollectionAssert.AreEqual( new[]
		{
			"Duncan Kershaw", "Shintaro Kanaoya", "Simon Carter", "Chris Killpack", "Marcus Iremonger", "Simon Harris"
		}, Names( list ), "by wage, the equal ones in the name order they stood in" );

		list.HeadingClicked( 1 );

		CollectionAssert.AreEqual( new[]
		{
			"Chris Killpack", "Marcus Iremonger", "Simon Harris", "Duncan Kershaw", "Shintaro Kanaoya", "Simon Carter"
		}, Names( list ), "by wage falling, the equal ones still as they stood, not turned round" );

		// Another tab and back: the list is cleared and filled again in slot order under the same sort.
		var refilled = Hire( 2, Entertainers );

		CollectionAssert.AreEqual( new[]
		{
			"Shintaro Kanaoya", "Simon Carter", "Duncan Kershaw", "Simon Harris", "Chris Killpack", "Marcus Iremonger"
		}, Names( refilled ), "a fresh fill: equal wages in the order added, slots 0, 8, 13" );
	}

	/// <summary>
	/// <b>The sort's word</b> (<c>0x406</c>, <c>FUN_006658b9</c>): the column + 1, negated when descending. A click on
	/// the sorted column flips the direction; a click on another sets ascending, then the column, each telling the
	/// screen when it changed. Emptying the list keeps the sort.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> another column keeping the direction; the same column not flipping; the word not negated;
	/// <c>Clear</c> dropping the sort; <c>SetSort</c> not taking the direction.
	/// </remarks>
	[TestMethod]
	public void TheSortsWordFollowsTheHeadingsClicked()
	{
		var list = Hire( 1, Entertainers );
		var told = new System.Collections.Generic.List<int>();

		list.SortChanged = told.Add;

		Assert.AreEqual( 1, list.SortWord );

		list.HeadingClicked( 0 );
		Assert.AreEqual( -1, list.SortWord, "the same column: descending" );
		Assert.AreEqual( "Simon Harris", list.Rows[0].Name );

		list.HeadingClicked( 1 );
		Assert.AreEqual( 2, list.SortWord, "another column: ascending" );
		CollectionAssert.AreEqual( new[] { -1, 1, 2 }, told, "the direction told first, then the column" );

		list.HeadingClicked( 1 );
		Assert.AreEqual( -2, list.SortWord );

		list.Clear();
		Assert.AreEqual( -2, list.SortWord, "an emptied list keeps its sort" );

		list.Add( new UiList.Row( 1, "a", 5 ) );
		list.Add( new UiList.Row( 2, "b", 9 ) );
		Assert.AreEqual( 2, list.Rows[0].Id, "and adds by it: the greater wage first" );

		Assert.AreEqual( -2, Hire( -2 ).SortWord, "a screen's kept word sets both" );
	}

	/// <summary>
	/// <b>After a heading's click the selection is the same index, and the screen is told the row now there</b>
	/// (<c>0x401</c>, <c>0x00665a8f</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the selection following its row; nothing told; nothing selected and something told.</remarks>
	[TestMethod]
	public void AHeadingsClickTellsTheRowNowUnderTheSelection()
	{
		var list = Hire( 1, Entertainers );
		var told = new System.Collections.Generic.List<int>();

		list.SelectionChanged = told.Add;
		list.HeadingClicked( 0 );
		Assert.AreEqual( 0, told.Count, "nothing selected: nothing told" );

		list.HeadingClicked( 0 );
		Press( list, 0 );
		Assert.AreEqual( 4, list.Selected, "Chris Killpack, slot 4" );

		list.HeadingClicked( 1 );
		Assert.AreEqual( 13, list.Selected, "index 0 is Duncan Kershaw now" );
		CollectionAssert.AreEqual( new[] { 4, 13 }, told );
	}

	/// <summary>
	/// <b>A list no screen has given a sort takes each row at the end, and a click on its heading is counted</b>:
	/// the all-staff and all-items lists, whose sorts are not built.
	/// </summary>
	/// <remarks><b>Mutations:</b> the click sorting on column nought; the click uncounted.</remarks>
	[TestMethod]
	public void AListWithNoSortCountsAHeadingsClick()
	{
		var list = Hire( 0, Entertainers );
		var counted = Times( "LIST_HEADING_SORT_NOT_BUILT" );

		Assert.AreEqual( 0, list.SortWord );
		list.HeadingClicked( 0 );

		Assert.AreEqual( counted + 1, Times( "LIST_HEADING_SORT_NOT_BUILT" ) );
		CollectionAssert.AreEqual( Entertainers.Select( person => person.Name ).ToArray(), Names( list ), "as added" );
	}

	/// <summary>
	/// <b>A heading is a button whose click is the list's</b>, as the original's child <c>0x10 + column</c> sends its
	/// <c>0x100</c> to the list's proc.
	/// </summary>
	/// <remarks><b>Mutations:</b> the heading given no click; every heading clicking column nought.</remarks>
	[TestMethod]
	public void AHeadingIsAButtonThatSortsItsColumn()
	{
		var list = Hire( 1, Entertainers );
		var wage = list.AddHeading( 1, new UiRect( 60, 0, 100, 10 ), "Monthly Wage" );

		Assert.AreEqual( 0x11, wage.Id );
		wage.Clicked!();
		Assert.AreEqual( 2, list.SortWord );
	}
}
