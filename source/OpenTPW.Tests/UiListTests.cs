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

		for ( var at = 0; at < keys.Length; ++at )
			list.Insert( Row( 100 + at, keys[at] ), Key );

		return list;
	}

	/// <summary>The virtual screen at one to one, so a press lands in the slot its virtual y names.</summary>
	[TestInitialize]
	public void OneToOne() => Screen.Size = new Point2( 2048, 1536 );

	[TestCleanup]
	public void PutTheScreenBack() => Screen.Size = new Point2( 1280, 720 );

	private static void Press( UiList list, int slot ) => list.PointerPressed( 50, (slot * 44) + 22 );

	private static UiList.Row Row( int id, int key ) => new( id, $"{key}", key );

	private static int Key( UiList.Row row ) => row.Value;

	private static int Times( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static int[] Keys( UiList list ) => list.Rows.Select( row => row.Value ).ToArray();

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

		Assert.AreEqual( 3, list.Insert( Row( 1, 2 ), Key ), "after both 2s (FUN_00663edc is strictly less)" );
		Assert.AreEqual( 0, list.Insert( Row( 2, 0 ), Key ), "a nought, a guest not yet through the gate, at the top" );
		Assert.AreEqual( 6, list.Insert( Row( 3, 9 ), Key ), "the greatest at the end" );
	}

	[TestMethod]
	public void AnInsertDoesNotScroll()
	{
		var list = ListOf( Enumerable.Range( 1, 20 ).ToArray() );

		list.Scroll( 5 );
		list.Insert( Row( 1, 0 ), Key );

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

		list.Insert( Row( 1, 5 ), Key );
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
}
