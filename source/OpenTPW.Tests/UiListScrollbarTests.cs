using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A list's scrollbar, the stream's <c>op 9</c> (<c>docs/exe/hud.md</c>, "Ops 9 and 10 are runtime-conditional"; <c>docs/exe/saves.md</c>,
/// "OpenTPW's Load Park"): shown for a list longer than its window with the range <c>count - visible</c> that <c>FUN_00664495</c> gives
/// it, an arrow a row, a click on the track a window, the thumb the top row (<c>Slider_Callback</c>, <c>Slider_PageTowards</c>,
/// <c>Slider_ReadThumb</c>), and the arrows repeat while held (<c>Button_RepeatTick</c>). Driven through a real <see cref="WindowStack"/>
/// a frame at a time with the real clock pinned.
/// </summary>
[TestClass]
public class UiListScrollbarTests
{
	/// <summary>Ten rows of 44 fit the row area (100,100)-(300,540); the track runs 140 to 500, its thumb 50 tall, so 310 of travel.</summary>
	private static readonly UiRect Track = new( 320, 140, 360, 500 );
	private static readonly UiRect Up = new( 320, 100, 360, 140 );
	private static readonly UiRect Down = new( 320, 500, 360, 540 );
	private static readonly UiRect Thumb = new( 316, 140, 360, 190 );

	private const int Travel = 310;

	private Point2 _screen;
	private WindowStack _stack = null!;
	private AWindow _window = null!;

	private UiList List => _window.List;

	[TestInitialize]
	public void OneToOne()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new();
		Time.PinWall( 0 );

		_stack = new WindowStack();
		_window = new AWindow( _stack );
		_stack.Open( _window );
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Input.Mouse = new();
		Time.PinWall( null );
		new WindowStack().Update();
		Screen.Size = _screen;
	}

	private void Fill( int rows )
	{
		List.Clear();

		for ( var row = 0; row < rows; ++row )
			List.Add( new UiList.Row( 100 + row, $"row {row}", row ) );
	}

	/// <summary>One frame: the left button, where the pointer is, and the real clock.</summary>
	private void Frame( bool down, float x, float y, long now )
	{
		Time.PinWall( now );
		Input.Mouse = new() { Left = down, LeftWentDown = down && !Input.Mouse.Left, Position = new Vector2( x, y ), Delta = new Vector2( 1, 0 ) };
		_stack.Update();
	}

	private static int ThumbTopFor( int top, int rows ) => 140 + (top * Travel / (rows - 10));

	/// <summary>
	/// <b>The bar is shown for a list longer than its window and hidden at or under it</b>, its range <c>( -(count - visible), 0 )</c> here
	/// (<c>( count - visible, 0 )</c> in <c>FUN_00664495</c>), and a list that shrinks back loses it again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the bar shown at ten rows (the window's own size); the track's picture left standing when the bar is hidden; the range taken as <c>count</c> alone, which leaves the last
	/// window out of reach; the bar left shown after the rows go.
	/// </remarks>
	[TestMethod]
	public void TheBarIsShownOnlyForAListLongerThanItsWindow()
	{
		var bar = List.Scrollbar!;
		var skin = List.Children.Single( child => child.Rect == Track );

		Fill( 10 );
		Assert.IsFalse( bar.Visible, "ten rows fill the window" );
		Assert.IsFalse( skin.Visible, "and the track's picture goes with it" );
		Assert.AreEqual( (0, 0), (bar.Minimum, bar.Maximum) );

		Fill( 11 );
		Assert.IsTrue( bar.Visible );
		Assert.IsTrue( skin.Visible );
		Assert.AreEqual( (-1, 0), (bar.Minimum, bar.Maximum), "one row more than fits" );

		Fill( 25 );
		Assert.AreEqual( (-15, 0), (bar.Minimum, bar.Maximum) );
		Assert.AreEqual( 10, bar.Page, "a click on the track moves the window's rows (0x00663a0f)" );

		for ( var row = 0; row < 15; ++row )
			List.Remove( 100 + row );

		Assert.IsFalse( bar.Visible, "back to ten" );
	}

	/// <summary>
	/// <b>An arrow let go within half a second moves the window one row</b>, plays <c>Select3</c>'s click, and the thumb follows to
	/// <c>top x travel / (count - visible)</c> down the track; the arrows stop at the ends.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the up arrow moving down; a step of the visible rows; the thumb left where it was; the foot passed; the click <c>BUTTON01</c>'s.
	/// </remarks>
	[TestMethod]
	public void AnArrowMovesTheWindowOneRowAndTheThumbFollows()
	{
		Fill( 25 );
		Assert.AreEqual( 140, List.Scrollbar!.Thumb!.Rect.Top, "row 0 at the head of the track" );

		UiSounds.LastClick = null;

		Frame( false, 340, 520, 1000 );
		Frame( true, 340, 520, 1010 );
		Frame( false, 340, 520, 1100 );
		Assert.AreEqual( 1, List.ScrollTop, "the down arrow let go at 90 ms" );
		Assert.AreEqual( 189, UiSounds.LastClick, "Select3, 0xbd: the bar the arrow reports to carries the flag 0x10" );
		Assert.AreEqual( ThumbTopFor( 1, 25 ), List.Scrollbar.Thumb.Rect.Top );

		Frame( true, 340, 120, 2000 );
		Frame( false, 340, 120, 2100 );
		Assert.AreEqual( 0, List.ScrollTop, "the up arrow" );

		Frame( true, 340, 120, 3000 );
		Frame( false, 340, 120, 3100 );
		Assert.AreEqual( 0, List.ScrollTop, "and not past the top" );

		for ( var press = 0; press < 20; ++press )
		{
			Frame( true, 340, 520, 4000 + (press * 200) );
			Frame( false, 340, 520, 4100 + (press * 200) );
		}

		Assert.AreEqual( 15, List.ScrollTop, "the foot is count less visible" );
		Assert.AreEqual( 500 - 50, List.Scrollbar.Thumb.Rect.Top, "the thumb at the foot of the track" );
	}

	/// <summary>
	/// <b>A held arrow repeats</b>: first once 500 ms of the real clock have gone, then each time more than 125 have, and the
	/// release after a repeat is no click, so it does not move the window a further row. A pointer that has left the arrow repeats nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the first repeat at 499; the second at exactly 125; the release after a repeat moving a row; the repeat
	/// with the pointer off the arrow.
	/// </remarks>
	[TestMethod]
	public void AHeldArrowRepeatsAfterHalfASecondThenEveryEighthAndItsReleaseIsNoClick()
	{
		Fill( 25 );
		UiSounds.LastClick = null;

		Frame( true, 340, 520, 1000 );
		Frame( true, 340, 520, 1499 );
		Assert.AreEqual( 0, List.ScrollTop, "499 ms: not yet" );

		Frame( true, 340, 520, 1500 );
		Assert.AreEqual( 1, List.ScrollTop, "500 ms: the first repeat" );

		Frame( true, 340, 520, 1625 );
		Assert.AreEqual( 1, List.ScrollTop, "125 ms on: not more than 125" );

		Frame( true, 340, 520, 1626 );
		Assert.AreEqual( 2, List.ScrollTop, "126 ms on: the next" );

		Frame( true, 340, 520, 1800 );
		Assert.AreEqual( 3, List.ScrollTop );

		Frame( true, 100, 800, 1950 );
		Frame( true, 100, 800, 2200 );
		Assert.AreEqual( 3, List.ScrollTop, "the pointer off the arrow: nothing is posted" );

		Frame( true, 340, 520, 2300 );
		Assert.AreEqual( 3, List.ScrollTop, "back on it, 100 ms after the last tick, which took the stamp without posting" );

		Frame( true, 340, 520, 2400 );
		Assert.AreEqual( 4, List.ScrollTop, "and 200 on from it" );

		Frame( false, 340, 520, 2450 );
		Assert.AreEqual( 4, List.ScrollTop, "the release after a repeat posts no click" );

		// The first delay running out with the pointer off the arrow: the state moves on, nothing is posted, and the next repeat is
		// due 125 ms after that tick all the same (Button_RepeatTick posts only if the pointer is on it).
		Frame( true, 340, 520, 5000 );
		Frame( true, 100, 800, 5200 );
		Frame( true, 100, 800, 5600 );
		Assert.AreEqual( 4, List.ScrollTop, "500 ms held with the pointer off the arrow: no first repeat" );

		Frame( true, 340, 520, 5800 );
		Assert.AreEqual( 5, List.ScrollTop, "and back on it, 200 ms after that tick: the repeat" );

		Frame( false, 340, 520, 5850 );
		Assert.IsNull( UiSounds.LastClick, "and plays none: the repeats are not 0x100" );
	}

	/// <summary>
	/// <b>A click on the track pages by the visible rows towards the click</b>: below the thumb's foot down, above it up, held at the ends.
	/// </summary>
	/// <remarks><b>Mutations:</b> a page of one row; the directions swapped; the page not held at the foot.</remarks>
	[TestMethod]
	public void AClickOnTheTrackPagesTowardsTheClick()
	{
		Fill( 25 );

		Frame( true, 340, 400, 1000 );
		Frame( false, 340, 400, 1100 );
		Assert.AreEqual( 10, List.ScrollTop, "below the thumb: a window down" );

		Frame( true, 340, 450, 2000 );
		Frame( false, 340, 450, 2100 );
		Assert.AreEqual( 15, List.ScrollTop, "and held at the foot" );

		Frame( true, 340, 160, 3000 );
		Frame( false, 340, 160, 3100 );
		Assert.AreEqual( 5, List.ScrollTop, "above the thumb: a window up" );
	}

	/// <summary>
	/// <b>The thumb dragged sets the top row from where it stands</b> (<c>Slider_ReadThumb</c>: the track's foot less the thumb's foot,
	/// over the travel, times <c>count - visible</c>, in whole numbers), follows the pointer without snapping, and is put on its row's place when let go.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the top row rounded where the original truncates; the drag read from the thumb's head and not its foot; the thumb snapped while held.
	/// </remarks>
	[TestMethod]
	public void TheThumbDraggedSetsTheTopRowAndSettlesWhenLetGo()
	{
		Fill( 25 );

		Frame( true, 340, 165, 1000 );
		Frame( true, 340, 165 + 85, 1100 );

		// Thumb 225..275: its foot 275, 225 above the track's foot 500; 225 x 15 / 310 = 10.89 whole 10, so the top row is 15 - 10.
		Assert.AreEqual( 15 - (225 * 15 / Travel), List.ScrollTop );
		Assert.AreEqual( 5, List.ScrollTop );
		Assert.AreEqual( 225, List.Scrollbar!.Thumb!.Rect.Top, "the thumb is where the pointer holds it, not snapped" );

		Frame( false, 340, 165 + 85, 1200 );
		Assert.AreEqual( ThumbTopFor( List.ScrollTop, 25 ), List.Scrollbar.Thumb.Rect.Top, "let go: on its row's place" );
	}

	/// <summary>
	/// <b>The wheel over the bar moves the window as over the list</b> (<c>0x1000d</c>: the value less the notch), a notch up a row up.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bar's own wheel direction, which goes the other way on the negated range.</remarks>
	[TestMethod]
	public void TheWheelOverTheBarMovesTheListAsOverTheList()
	{
		Fill( 25 );

		List.Scrollbar!.Scroll( -1f );
		List.Scrollbar.Scroll( -1f );
		Assert.AreEqual( 2, List.ScrollTop, "two notches away: two rows down" );

		List.Scrollbar.Scroll( 1f );
		Assert.AreEqual( 1, List.ScrollTop );
		Assert.AreEqual( ThumbTopFor( 1, 25 ), List.Scrollbar.Thumb!.Rect.Top, "and the thumb moves with it" );
	}

	/// <summary><b>A window scrolled by the wheel puts the thumb where the top row belongs.</b></summary>
	/// <remarks><b>Mutations:</b> the bar not told when the list scrolls itself.</remarks>
	[TestMethod]
	public void TheListScrolledByItselfMovesTheThumb()
	{
		Fill( 25 );
		List.ScrollByWheel( -5f );

		Assert.AreEqual( 5, List.ScrollTop );
		Assert.AreEqual( -5, List.Scrollbar!.Value );
		Assert.AreEqual( ThumbTopFor( 5, 25 ), List.Scrollbar.Thumb!.Rect.Top );
	}

	/// <summary>
	/// <b>The track, the arrows and the thumb stand together on a window wider than the screen's 4:3</b>: the arrows lie outside the bar's own rectangle
	/// and the thumb a little over it, and a control only keeps to its parent's third of the window where the parent's rectangle holds it, so
	/// built under the bar alone they drifted to the window's edge (seen on the first desktop run, 1280 x 720).
	/// </summary>
	/// <remarks><b>Mutations:</b> the arrows and thumb made children of a bar whose rectangle is the track alone.</remarks>
	[TestMethod]
	public void ThePartsStandTogetherOnAWideWindow()
	{
		Screen.Size = new Point2( 1280, 720 );
		Fill( 25 );

		var bar = List.Scrollbar!;
		var skin = List.Children.Single( child => child.Rect == Track );
		var arrows = bar.Children.OfType<UiRepeatButton>().ToArray();

		Assert.AreEqual( 2, arrows.Length );
		Assert.IsTrue( arrows.All( arrow => Math.Abs( arrow.Pixels.X - skin.Pixels.X ) < 1.01f ), $"the arrows' left {string.Join( ", ", arrows.Select( arrow => arrow.Pixels.X ) )} and the track's {skin.Pixels.X}" );
		Assert.IsTrue( Math.Abs( (skin.Pixels.X - bar.Thumb!.Pixels.X) - (4 * VirtualScreen.Scale) ) < 1.01f, "the thumb 4 units left of the track" );
		Assert.IsTrue( Math.Abs( skin.Pixels.Y - arrows[0].Pixels.Y - (40 * VirtualScreen.Scale) ) < 1.01f, "the up arrow 40 units above the track's head" );
	}

	/// <summary>A window with a list and the stream's three-part scrollbar.</summary>
	private sealed class AWindow : UiWindow
	{
		public UiList List { get; }

		public AWindow( WindowStack stack ) : base( stack )
		{
			Root = new UiControl { Rect = new UiRect( 0, 0, 1200, 900 ), HoldsChildren = true };

			List = Root.Add( new UiList
			{
				Rect = new UiRect( 100, 100, 400, 560 ),
				RowArea = new UiRect( 100, 100, 300, 540 ),
				Columns = [(100, 300)],
				SelectsUnderPointer = true
			} );

			List.Build();
			List.AddScrollbar( Track, Up, Down, Thumb );
		}
	}
}
