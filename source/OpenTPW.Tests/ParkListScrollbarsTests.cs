using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The scrollbar on the visitors, all-staff, all-items, buy and hire lists (Q919; <c>docs/exe/hud.md</c>, "The list's scrollbar"), each
/// at the rectangles its own stream gives (<c>op 9</c> with the arrows <c>op 7</c> and <c>op 6</c> and the thumb <c>op 8</c>; <c>lib/uistream.py</c>).
/// The bar's behaviour is <see cref="UiListScrollbarTests"/>'s; these check that each screen builds it where its stream puts it, shows it for a
/// list longer than its window, and answers a real click on its arrows. Reads the shipped park; skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkListScrollbarsTests
{
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private Level? before;
	private Point2 size;
	private ParkWorld world = null!;
	private ParkState state = null!;
	private ParkPeople people = null!;
	private Level level = null!;
	private WindowStack stack = null!;

	[TestInitialize]
	public void OpenThePark()
	{
		before = Level.Current;
		size = Screen.Size;
		Screen.Size = new( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();

		using var file = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		world = new ParkWorld( new SaveReader( file ).ReadFile() );
		state = new( world );

		var balance = new ParkBalance( "jungle", easyMode: true );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		typeof( Level ).GetProperty( nameof( Level.StaffPool ) )!.SetValue( level, Level.StaffPoolFor( world, balance, world.GameTick ) );
		Level.Current = level;

		people = new( world, balance, null, state, catalogue );

		Input.Mouse = new();
		Time.PinWall( 0 );
		stack = new WindowStack();
	}

	[TestCleanup]
	public void PutBack()
	{
		Input.Mouse = new();
		Time.PinWall( null );
		new WindowStack().Update();
		people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = before!;
		Screen.Size = size;
	}

	/// <summary>One frame: the left button and where the pointer is, in the 2048 x 1536 layout, which is the window here.</summary>
	private void Frame( bool down, float x, float y, long now )
	{
		Time.PinWall( now );
		Input.Mouse = new() { Left = down, LeftWentDown = down && !Input.Mouse.Left, Position = new Vector2( x, y ), Delta = new Vector2( 1, 0 ) };
		stack.Update();
	}

	private void Click( UiRect arrow, long now )
	{
		var (x, y) = ((arrow.Left + arrow.Right) / 2, (arrow.Top + arrow.Bottom) / 2);

		Frame( false, x, y, now );
		Frame( true, x, y, now + 10 );
		Frame( false, x, y, now + 60 );
	}

	/// <summary>Each screen: how it is opened, its list, and the four rectangles its stream gives the bar (the address is the <c>op 9</c>; the track is the <c>op 3</c> inside it where that differs).</summary>
	private record Case( string Name, Func<WindowStack, UiWindow> Open, Func<UiWindow, UiList> ListOf,
		UiRect Bar, UiRect Up, UiRect Down, UiRect Thumb, UiRect? Track = null );

	private static T Field<T>( object screen, string name ) => (T)screen.GetType().GetField( name, Private )!.GetValue( screen )!;

	/// <remarks>The items screen opens on the tab the last one left (a static), so its list is the one shown, whichever of its three shapes that is.</remarks>
	private static readonly Case[] Cases =
	[
		new( "visitors 0x00750740", s => new ParkVisitorsScreen( s ), w => Field<UiList>( w, "_list" ),
			new( 1748, 265, 1807, 835 ), new( 1748, 202, 1808, 262 ), new( 1747, 843, 1807, 903 ), new( 1739, 370, 1807, 437 ),
			new( 1748, 263, 1807, 835 ) ),
		new( "all staff 0x00750fb4", s => new ParkStaffScreen( s ), w => Field<UiList>( w, "_list" ),
			new( 1747, 376, 1806, 719 ), new( 1747, 315, 1807, 375 ), new( 1747, 722, 1807, 782 ), new( 1739, 484, 1806, 551 ) ),
		new( "all items 0x00750b32", s => new ParkItemsScreen( s ), w => Field<Dictionary<int, UiList>>( w, "_lists" ).Values.Single( list => list.Visible ),
			new( 1766, 384, 1825, 811 ), new( 1765, 318, 1825, 379 ), new( 1765, 815, 1825, 876 ), new( 1758, 549, 1825, 616 ) ),
		new( "buy 0x00755032", s => new ParkBuyScreen( s ), w => Field<UiList>( w, "_list" ),
			new( 1743, 383, 1802, 803 ), new( 1742, 318, 1803, 378 ), new( 1742, 809, 1803, 869 ), new( 1735, 610, 1802, 677 ) ),
		new( "hire 0x0075232a", s => new ParkHireScreen( s ), w => Field<UiList>( w, "_list" ),
			new( 1743, 398, 1802, 843 ), new( 1742, 331, 1803, 391 ), new( 1742, 850, 1803, 910 ), new( 1735, 651, 1802, 718 ) )
	];

	private static void FillWith( UiList list, int rows )
	{
		list.Clear();

		for ( var row = 0; row < rows; ++row )
			list.Add( new UiList.Row( 9000 + row, $"row {row}", row ) );
	}

	/// <summary>
	/// <b>Every one of the five lists has a bar at its stream's rectangles</b>: the track, the up arrow, the down arrow and the thumb, shown
	/// for a list longer than its window and hidden at the window's own count.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a screen's bar left out; a rectangle taken from another screen's stream (each differs in at least one number);
	/// the bar shown at the window's own count; the arrows swapped.
	/// </remarks>
	[TestMethod]
	public void EachListHasItsStreamsBarShownOnlyWhenTheListIsLongerThanItsWindow()
	{
		foreach ( var each in Cases )
		{
			var screen = each.Open( stack );
			stack.Open( screen );

			var list = each.ListOf( screen );
			var bar = list.Scrollbar;

			Assert.IsNotNull( bar, each.Name );
			Assert.AreEqual( each.Track ?? each.Bar, bar.Track, each.Name + ": the track (op 3's rectangle where it differs from op 9's)" );
			Assert.AreEqual( each.Up, bar.Children.OfType<UiRepeatButton>().Single( b => b.Id == 1 ).Rect, each.Name + ": the up arrow" );
			Assert.AreEqual( each.Down, bar.Children.OfType<UiRepeatButton>().Single( b => b.Id == 2 ).Rect, each.Name + ": the down arrow" );
			Assert.AreEqual( each.Thumb.Width, bar.Thumb!.Rect.Width, each.Name + ": the thumb's size" );
			Assert.AreEqual( each.Thumb.Left, bar.Thumb.Rect.Left, each.Name + ": the thumb's column" );
			Assert.AreEqual( (each.Track ?? each.Bar).Top, bar.Thumb.Rect.Top, each.Name + ": the thumb's head stands at the track's top" );

			FillWith( list, list.VisibleRows );
			Assert.IsFalse( bar.Visible, each.Name + ": a full window needs no bar" );

			FillWith( list, list.VisibleRows + 1 );
			Assert.IsTrue( bar.Visible, each.Name + ": one row past the window" );
			Assert.AreEqual( list.VisibleRows, bar.Page, each.Name + ": a click on the track moves a window" );

			stack.Close( screen );
		}
	}

	/// <summary>
	/// <b>A real click on each list's down arrow moves its window one row and its up arrow back</b>, and the thumb stands one step down the
	/// track: <c>row x travel / (count - visible)</c> (<c>Slider_SetPosition</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> an arrow on the wrong side of the bar (a click at the rectangle misses); the bar not told the window's top; the arrows built
	/// as children of a bar smaller than their rectangles (hit at the window's edge on a wide window, Q918).
	/// </remarks>
	[TestMethod]
	public void ARealClickOnEachListsArrowsMovesItsWindowOneRow()
	{
		// The items screen opens on the tab the last screen left: the sideshows' (six columns) here, not the rides' the others share.
		var lastTab = typeof( ParkItemsScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!;
		var tab = lastTab.GetValue( null );

		lastTab.SetValue( null, 2 );

		try
		{
			ClickEachListsArrows();
		}
		finally
		{
			lastTab.SetValue( null, tab );
		}
	}

	private void ClickEachListsArrows()
	{
		foreach ( var each in Cases )
		{
			var screen = each.Open( stack );
			stack.Open( screen );

			var list = each.ListOf( screen );
			var bar = list.Scrollbar!;

			FillWith( list, list.VisibleRows + 6 );
			Assert.AreEqual( 0, list.ScrollTop, each.Name );

			Click( each.Down, 1000 );
			Assert.AreEqual( 1, list.ScrollTop, each.Name + ": the down arrow" );

			var travel = bar.Track.Bottom - bar.Track.Top - bar.Thumb!.Rect.Height;
			Assert.AreEqual( bar.Track.Top + (1 * travel / 6), bar.Thumb.Rect.Top, each.Name + ": the thumb one step down" );

			Click( each.Up, 1200 );
			Assert.AreEqual( 0, list.ScrollTop, each.Name + ": the up arrow" );

			stack.Close( screen );
		}
	}

	/// <summary>
	/// <b>The wheel-only path is no longer counted on a list with a bar</b>: <c>LIST_SCROLLBAR_NOT_BUILT</c> is reported for a list that scrolls
	/// with no bar and for none of the five.
	/// </summary>
	/// <remarks><b>Mutations:</b> the report removed (the bare list's count stays 0); the report made unconditional (the five lists count).</remarks>
	[TestMethod]
	public void OnlyAListWithNoBarCountsTheWheelOnlyPath()
	{
		var counted = () => Unimplemented.Summary.FirstOrDefault( entry => entry.What == "LIST_SCROLLBAR_NOT_BUILT" ).Times;
		var start = counted();

		foreach ( var each in Cases )
		{
			var screen = each.Open( stack );
			stack.Open( screen );

			var list = each.ListOf( screen );
			FillWith( list, list.VisibleRows + 3 );
			list.Scroll( 1 );
			stack.Close( screen );
		}

		Assert.AreEqual( start, counted(), "a list with a bar scrolls without the count" );

		var bare = new UiList { Rect = new UiRect( 0, 0, 400, 400 ), RowArea = new UiRect( 0, 0, 400, 400 ), RowHeight = 100 };
		bare.Build();

		for ( var row = 0; row < 6; ++row )
			bare.Add( new UiList.Row( row, $"row {row}", row ) );

		bare.Scroll( 1 );
		Assert.AreEqual( start + 1, counted(), "a bare list longer than its window is scrolled by the wheel alone, counted" );
	}
}
