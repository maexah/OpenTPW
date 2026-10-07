using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The left button's click, as the original's base control proc makes one (<c>docs/exe/hud.md</c>, "Who acts on the
/// click, and who on the release"): a press let go under 500 ms of real time that has not strayed more than 6, for
/// whatever is not a button; a button keeps its own release and has no limit. Driven through a real
/// <see cref="WindowStack"/> a frame at a time, the pointer's state set as the window system would leave it.
/// </summary>
[TestClass]
public class LeftClickTests
{
	private static readonly Vector2 OnThePanel = new( 150, 150 );
	private static readonly Vector2 OnTheButton = new( 450, 150 );
	private static readonly Vector2 Nowhere = new( 1500, 1200 );

	private Point2 _screen;
	private WindowStack _stack = null!;
	private AWindow _window = null!;

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
		Time.PinWall( 0 );

		// The stack's own statics, which the next test's first frame would otherwise read stale.
		new WindowStack().Update();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>Held 500 ms it is no click, and 499 it is</b> (<c>0x0065f969</c>), and the press alone does nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the click given on the press; no limit; the limit taken as at most 500.</remarks>
	[TestMethod]
	public void AControlClicksOnlyUnderHalfASecond()
	{
		Frame( true, OnThePanel, 1000 );
		Assert.AreEqual( 0, _window.PanelClicks, "the press is no click" );

		Frame( false, OnThePanel, 1500 );
		Assert.AreEqual( 0, _window.PanelClicks, "held 500 ms: none" );

		Frame( true, OnThePanel, 3000 );
		Frame( false, OnThePanel, 3499 );
		Assert.AreEqual( 1, _window.PanelClicks, "held 499 ms: a click" );
	}

	/// <summary>
	/// <b>A stray of 7 is a drag, and 6 is not; the release may be anywhere</b>: the click is the control's the press
	/// landed on (<c>0x0065fab7</c>, <c>0x0065f977</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> no stray judged; a stray of 6 a drag; the click given to what is under the release.</remarks>
	[TestMethod]
	public void AStrayOfSevenIsADragAndTheReleaseMayBeAnywhere()
	{
		Frame( true, OnThePanel, 1000 );
		Frame( true, OnThePanel + new Vector2( 7, 0 ), 1050 );
		Frame( true, OnThePanel, 1100 );
		Frame( false, OnThePanel, 1150 );
		Assert.AreEqual( 0, _window.PanelClicks, "strayed 7 and came back: no click" );

		Frame( true, OnThePanel, 3000 );
		Frame( true, OnThePanel + new Vector2( 6, 6 ), 3050 );
		Frame( false, OnThePanel + new Vector2( 6, 6 ), 3100 );
		Assert.AreEqual( 1, _window.PanelClicks, "strayed 6 each way: a click" );

		Frame( true, OnThePanel, 5000 );
		Frame( false, Nowhere, 5100 );
		Assert.AreEqual( 2, _window.PanelClicks, "let go far from it, within the limit: the panel's click" );
	}

	/// <summary>
	/// <b>A press under 500 ms from the last click's release is a double click's second</b>: the control is told on the
	/// press, its release clicks nothing, and the press after that clicks again (<c>0x0065f8c7</c>, <c>0x0065f9bd</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the second press clicking; the second not told; the stamp kept after a spoiled release.</remarks>
	[TestMethod]
	public void ASecondPressHardOnTheFirstIsADoubleClicksSecond()
	{
		Frame( true, OnThePanel, 1000 );
		Frame( false, OnThePanel, 1100 );
		Assert.AreEqual( (1, 0), (_window.PanelClicks, _window.Panel.Seconds) );

		Frame( true, OnThePanel, 1599 );
		Assert.AreEqual( (1, 1), (_window.PanelClicks, _window.Panel.Seconds), "499 ms on: the second, told on the press" );

		Frame( false, OnThePanel, 1650 );
		Assert.AreEqual( 1, _window.PanelClicks, "and its release is no click" );

		Frame( true, OnThePanel, 1700 );
		Frame( false, OnThePanel, 1750 );
		Assert.AreEqual( (2, 1), (_window.PanelClicks, _window.Panel.Seconds), "the third press clicks: the stamp was cleared" );

		Frame( true, OnThePanel, 2250 );
		Frame( false, OnThePanel, 2300 );
		Assert.AreEqual( (3, 1), (_window.PanelClicks, _window.Panel.Seconds), "500 ms after a release is no second" );
	}

	/// <summary>
	/// <b>A hold of half a second or more makes no click, and its release is stamped all the same</b>
	/// (<c>0x0065f9af</c>): a press under 500 ms after it is a double click's second, told on the press, and its
	/// release clicks nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stamp kept only by a release that clicked.</remarks>
	[TestMethod]
	public void ALongHoldsReleaseIsStampedAllTheSame()
	{
		Frame( true, OnThePanel, 1000 );
		Frame( false, OnThePanel, 1600 );
		Assert.AreEqual( (0, 0), (_window.PanelClicks, _window.Panel.Seconds), "held 600 ms: no click" );

		Frame( true, OnThePanel, 1900 );
		Assert.AreEqual( 1, _window.Panel.Seconds, "300 ms after that release: the second, told on the press" );

		Frame( false, OnThePanel, 1950 );
		Assert.AreEqual( 0, _window.PanelClicks, "and its release is no click" );
	}

	/// <summary>
	/// <b>The stray is judged only while the pointer is over the control the press landed on</b>: a move goes to the
	/// control under the pointer and nothing captures it (<c>FUN_006588ef</c>), so a press dragged straight off its
	/// control and let go within the limit is still that control's click.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stray judged wherever the pointer is.</remarks>
	[TestMethod]
	public void AStrayOffTheControlIsNotJudged()
	{
		Frame( true, OnThePanel, 1000 );
		Frame( true, Nowhere, 1050 );
		Frame( false, Nowhere, 1100 );
		Assert.AreEqual( 1, _window.PanelClicks, "dragged off the panel and let go: its click" );
	}

	/// <summary>
	/// <b>A lobby player slot and Quit Game answer a click of either button and no held press</b>: neither is a button
	/// to the original (type 1 in the stream at <c>0x00753c68</c>), and their callbacks answer the base proc's click
	/// (<c>0x004a6104</c>, <c>0x004a61e9</c>). The front end is a stand-in made without its constructor, holding the
	/// stack; an empty slot opens the new player's dialog and Quit Game its question.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a slot answering a release of any length; Quit Game answering one; Quit Game deaf to the right
	/// button.
	/// </remarks>
	[TestMethod]
	public void TheLobbysSlotsAndQuitGameAnswerAClickOfEitherButtonAndNoHeldPress()
	{
		var frontEnd = (FrontEnd)RuntimeHelpers.GetUninitializedObject( typeof( FrontEnd ) );
		typeof( FrontEnd ).GetField( "_stack", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( frontEnd, _stack );

		Assert.IsNull( Players.Roster[0], "the first slot is empty, so its click asks for a name and selects nobody" );

		var slots = new PlayerSlots( _stack, frontEnd );

		_stack.Open( slots );
		Frame( false, Nowhere, 500 );

		foreach ( var (id, at, opens) in new[] { (0x7a14, new Vector2( 900, 120 ), typeof( NewPlayerDialog )), (0x7a1c, new Vector2( 1300, 960 ), typeof( MessageBox )) } )
		{
			var control = slots.Root.Children.Single( child => child.Id == id );
			Assert.AreSame( control, slots.Root.HitTest( at.X, at.Y ), $"{id:x} takes the pointer" );

			int Opened() => _stack.Windows.Count( window => window.GetType() == opens );

			void CloseIt()
			{
				foreach ( var window in _stack.Windows.Where( window => window.GetType() == opens ).ToList() )
					_stack.Close( window );
			}

			Frame( true, at, 1000 );
			Frame( false, at, 1600 );
			Assert.AreEqual( 0, Opened(), $"{id:x}: the left button held 600 ms asks nothing" );

			Frame( true, at, 3000 );
			Frame( false, at, 3100 );
			Assert.AreEqual( 1, Opened(), $"{id:x}: a left click asks" );
			CloseIt();

			Frame( true, at, 5000, right: true );
			Frame( false, at, 5600, right: true );
			Assert.AreEqual( 0, Opened(), $"{id:x}: the right button held 600 ms asks nothing" );

			Frame( true, at, 7000, right: true );
			Frame( false, at, 7100, right: true );
			Assert.AreEqual( 1, Opened(), $"{id:x}: and a right click asks" );
			CloseIt();

			Frame( false, Nowhere, 9000 );
		}
	}

	/// <summary>
	/// <b>A button has no limit and takes no part in the click</b>: held two seconds it clicks as it comes up
	/// (<c>FUN_00668f9c</c>), it is never told a left click, and its release clears the stamp, so a press on a control
	/// straight after is no double click's second.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the limit put on a button; a button handed the timed click as well; a button's press left
	/// stamping the record for the next press to trip on.
	/// </remarks>
	[TestMethod]
	public void AButtonHasNoLimit()
	{
		Frame( true, OnTheButton, 1000 );
		Frame( false, OnTheButton, 3000 );
		Assert.AreEqual( (1, 0), (_window.ButtonClicks, _window.ButtonLeftClicks), "held two seconds: its own click, and no timed one" );

		Frame( true, OnTheButton, 4000 );
		Frame( false, OnTheButton, 4050 );
		Assert.AreEqual( (2, 0), (_window.ButtonClicks, _window.ButtonLeftClicks), "quick: the same" );

		Frame( true, OnThePanel, 4100 );
		Frame( false, OnThePanel, 4150 );
		Assert.AreEqual( (1, 0), (_window.PanelClicks, _window.Panel.Seconds), "a press 50 ms after a button's release clicks" );
	}

	/// <summary>
	/// <b>A list's row acts on the click, not the press</b> (<c>FUN_006655a2</c> on <c>0x11006</c>): pressed, or held
	/// 600 ms, nothing is selected or chosen; clicked, the row is selected and chosen; and a click that misses every row
	/// chooses the selected one again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the row chosen on the press; the click's point taken at the release; a miss choosing nothing;
	/// the row not selected before it is chosen.
	/// </remarks>
	[TestMethod]
	public void AListsRowActsOnTheClick()
	{
		var list = _window.List;

		Frame( true, Row( 1 ), 1000 );
		Assert.AreEqual( (-1, 0), (list.Selected, _window.Chosen.Count), "the press selects and chooses nothing" );

		Frame( false, Row( 1 ), 1600 );
		Assert.AreEqual( (-1, 0), (list.Selected, _window.Chosen.Count), "held 600 ms: nothing" );

		Frame( true, Row( 1 ), 3000 );
		Frame( false, Row( 2 ), 3100 );
		Assert.AreEqual( 101, list.Selected, "clicked: the row the PRESS was on is selected" );
		CollectionAssert.AreEqual( new[] { 101 }, _window.Chosen, "and chosen" );

		Frame( true, Row( 8 ), 5000 );
		Frame( false, Row( 8 ), 5100 );
		CollectionAssert.AreEqual( new[] { 101, 101 }, _window.Chosen, "a click under the last row chooses the selected row again" );
	}

	/// <summary>
	/// <b>A list that follows the pointer does not while a button pressed on it is down</b> (<c>0x006656ba</c>, the
	/// control's own record of its pressed buttons, <c>+0x11c</c>): the row under a moving pointer is selected with no
	/// button down, and stays as it was while the left or the right button pressed on the list is held.
	/// </summary>
	/// <remarks><b>Mutations:</b> the held button not asked; only the left asked.</remarks>
	[TestMethod]
	public void AListDoesNotFollowThePointerWhileAButtonIsDownOnIt()
	{
		var list = _window.List;

		Frame( false, Row( 1 ), 1000, moved: true );
		Assert.AreEqual( 101, list.Selected, "no button down: the row under the pointer" );

		Frame( true, Row( 1 ), 2000, moved: true );
		Frame( true, Row( 2 ), 2050, moved: true );
		Assert.AreEqual( 101, list.Selected, "the left button down on the list: the pointer is not followed" );

		Frame( false, Row( 2 ), 2100 );
		Frame( false, Row( 0 ), 3000, moved: true );
		Assert.AreEqual( 100, list.Selected, "let go, it follows again" );

		Frame( true, Row( 0 ), 4000, right: true, moved: true );
		Frame( true, Row( 2 ), 4050, right: true, moved: true );
		Assert.AreEqual( 100, list.Selected, "nor with the right button down on it" );

		Frame( false, Row( 2 ), 4700, right: true );
		Frame( false, Row( 2 ), 5000, moved: true );
		Assert.AreEqual( 102, list.Selected );
	}

	/// <summary>
	/// <b>The double click's second press chooses the selected row again, on the press</b> (<c>FUN_006656fa</c> on
	/// <c>0x11007</c>). The selected row is the one the pointer was last moved over (<c>FUN_006656a0</c>), so a second
	/// press on another row finds that row selected by the move that brought the pointer there.
	/// </summary>
	/// <remarks><b>Mutations:</b> the list not answering the second press; the second press choosing the row first clicked.</remarks>
	[TestMethod]
	public void AListsDoubleClickChoosesAgainOnThePress()
	{
		Frame( true, Row( 0 ), 1000 );
		Frame( false, Row( 0 ), 1100 );
		CollectionAssert.AreEqual( new[] { 100 }, _window.Chosen );

		Frame( true, Row( 0 ), 1300 );
		CollectionAssert.AreEqual( new[] { 100, 100 }, _window.Chosen, "the second press, on the press" );

		Frame( false, Row( 0 ), 1350 );
		CollectionAssert.AreEqual( new[] { 100, 100 }, _window.Chosen, "and its release nothing" );

		// On to another row between the two presses: the move selects it, and the second press chooses it.
		Frame( true, Row( 0 ), 3000 );
		Frame( false, Row( 0 ), 3100 );
		Frame( false, Row( 2 ), 3200, moved: true );
		Frame( true, Row( 2 ), 3300 );
		CollectionAssert.AreEqual( new[] { 100, 100, 100, 102 }, _window.Chosen, "the second press: the row the pointer moved onto" );
	}

	/// <summary>
	/// <b>The game menu's rows answer the click of either button, and no held press</b>
	/// (<c>MenuList_ChoiceCallback</c>, <c>0x00492d8f</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> a row chosen by a release of any length; the right button not wired; the left not.</remarks>
	[TestMethod]
	public void AMenuRowAnswersAClickOfEitherButtonAndNoHeldPress()
	{
		var chosen = 0;
		var menu = new GameMenu( _stack, [new GameMenu.Item( UIStrings.ResumeGame, 7, _ => ++chosen )], 10 );

		_stack.Open( menu );
		Frame( false, Nowhere, 500 );

		var row = menu.Root.Children.Single( child => child.Id == 7 );
		var at = new Vector2( (row.Rect.Left + row.Rect.Right) / 2f, (row.Rect.Top + row.Rect.Bottom) / 2f );
		Assert.AreSame( row, menu.Root.HitTest( at.X, at.Y ), "the row takes the pointer" );

		Frame( true, at, 1000 );
		Frame( false, at, 1600 );
		Assert.AreEqual( 0, chosen, "the left button held 600 ms chooses nothing" );

		Frame( true, at, 3000 );
		Frame( false, at, 3100 );
		Assert.AreEqual( 1, chosen, "a left click chooses" );

		Frame( true, at, 5000, right: true );
		Frame( false, at, 5100, right: true );
		Assert.AreEqual( 2, chosen, "and so does a right click" );
	}

	/// <summary>
	/// <b>The console's click is a quick click</b>: on a list's row it selects and chooses, as a harness expects of it,
	/// and on a button it is the button's own.
	/// </summary>
	/// <remarks><b>Mutations:</b> the console's click not making the timed click; making one for a button too.</remarks>
	[TestMethod]
	public void TheConsolesClickIsAQuickClick()
	{
		var at = Row( 1 );

		_stack.ClickAt( at.X, at.Y );
		CollectionAssert.AreEqual( new[] { 101 }, _window.Chosen );

		_stack.ClickAt( OnTheButton.X, OnTheButton.Y );
		Assert.AreEqual( (1, 0), (_window.ButtonClicks, _window.ButtonLeftClicks) );
	}

	/// <summary>
	/// <b>A slider's track pages towards a click, not a press</b> (<c>Slider_Callback</c> on <c>0x11006</c>): held 600
	/// ms beside the thumb it stays, clicked it moves a page.
	/// </summary>
	/// <remarks><b>Mutations:</b> the track paging on the press.</remarks>
	[TestMethod]
	public void ASlidersTrackPagesOnTheClick()
	{
		var slider = _window.Slider;
		var beside = new Vector2( 380, 720 );
		var before = slider.Value;

		Frame( true, beside, 1000 );
		Assert.AreEqual( before, slider.Value, "the press pages nothing" );

		Frame( false, beside, 1600 );
		Assert.AreEqual( before, slider.Value, "held 600 ms: nothing" );

		Frame( true, beside, 3000 );
		Frame( false, beside, 3100 );
		Assert.IsTrue( slider.Value > before, "clicked right of the thumb: a page up" );

		// Half way along, so the point is clear of the thumb, which a page moves only a little.
		slider.SetValue( 50 );
		var up = slider.Value;
		Frame( true, new Vector2( 105, 720 ), 5000, right: true );
		Frame( false, new Vector2( 105, 720 ), 5100, right: true );
		Assert.IsTrue( slider.Value < up, "a right click left of the thumb: a page down" );
	}

	/// <summary>
	/// <b>A slider's thumb keeps its press and leaves the stamp alone</b> (<c>0x0066bb9b</c>): a click, a grab of the
	/// thumb, and a press under 500 ms from that click's release is still a double click's second.
	/// </summary>
	/// <remarks><b>Mutations:</b> the thumb treated as a button, its release clearing the stamp.</remarks>
	[TestMethod]
	public void ASlidersThumbLeavesTheStampAlone()
	{
		Frame( true, OnThePanel, 1000 );
		Frame( false, OnThePanel, 1100 );

		var thumb = _window.Slider.Thumb!.Rect;
		var onTheThumb = new Vector2( (thumb.Left + thumb.Right) / 2f, 720 );
		Frame( true, onTheThumb, 1200 );
		Frame( false, onTheThumb, 1300 );

		Frame( true, OnThePanel, 1500 );
		Frame( false, OnThePanel, 1550 );
		Assert.AreEqual( (1, 1), (_window.PanelClicks, _window.Panel.Seconds), "400 ms after the click, a thumb's grab between: the second" );
	}

	/// <summary>
	/// <b>A list never judges the stray</b>: its proc answers the move and returns before the base proc
	/// (<c>0x00665d22</c>), so a press dragged down the list and let go under 500 ms clicks the row it went down on, for
	/// either button.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stray judged on a list, for the left button; for the right.</remarks>
	[TestMethod]
	public void AListNeverJudgesTheStray()
	{
		var named = new List<int>();
		_window.List.RowRightClicked = named.Add;

		Frame( true, Row( 0 ), 1000 );
		Frame( true, Row( 2 ), 1100 );
		Frame( false, Row( 2 ), 1200 );
		CollectionAssert.AreEqual( new[] { 100 }, _window.Chosen, "dragged two rows down: the row the press was on" );

		Frame( true, Row( 1 ), 3000, right: true );
		Frame( true, Row( 2 ), 3100, right: true );
		Frame( false, Row( 2 ), 3200, right: true );
		CollectionAssert.AreEqual( new[] { 101 }, named, "and the right button's the same" );
	}

	/// <summary>
	/// <b>A list that does not follow the pointer activates nothing on a miss, and a list nobody answers is
	/// counted</b>: the original's selected row is the one last hovered, which such a list does not know.
	/// </summary>
	/// <remarks><b>Mutations:</b> a miss activating the row last clicked; an unanswered activation dropped uncounted.</remarks>
	[TestMethod]
	public void AMissOnAListThatDoesNotFollowThePointerAndAnUnansweredRowAreCounted()
	{
		Unimplemented.Forget();

		var chosen = new List<int>();
		var list = new UiList { RowArea = new UiRect( 0, 0, 100, 440 ), Columns = [(0, 100)], Activated = chosen.Add };
		list.Build();
		list.Add( new UiList.Row( 5, "five", 0 ) );

		list.LeftClickedAt( 50, 22 );
		list.LeftClickedAt( 50, 300 );
		CollectionAssert.AreEqual( new[] { 5 }, chosen, "the miss chose nothing" );
		Assert.AreEqual( 1, Times( "LIST_CLICK_MISS_SELECTION_NOT_UNDER_POINTER" ) );

		list.Activated = null;
		list.LeftClickedAt( 50, 22 );
		Assert.AreEqual( 1, Times( "LIST_ROW_ACTIVATED_UNANSWERED" ) );

		Unimplemented.Forget();
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>
	/// <b>A window that closes under a held press takes the click with it, and a button found already down is no
	/// press</b>: only a press the window system sent can click.
	/// </summary>
	/// <remarks><b>Mutations:</b> the press kept for a closed window's control; a button seen down taken as a press.</remarks>
	[TestMethod]
	public void AClosedWindowAndAButtonAlreadyDownMakeNoClick()
	{
		Time.PinWall( 1000 );
		Input.Mouse = new() { Left = true, Position = OnThePanel };
		_stack.Update();
		Frame( false, OnThePanel, 1100 );
		Assert.AreEqual( 0, _window.PanelClicks, "down when the stack first looked: no click" );

		Frame( true, OnThePanel, 3000 );
		_stack.Close( _window );
		Frame( false, OnThePanel, 3100 );
		Assert.AreEqual( 0, _window.PanelClicks, "the window closed under the press: no click" );
	}

	/// <summary>
	/// <b>A press waiting on its release makes no click once its window has closed or the cover has gone on</b>, for
	/// either button: the control it would be told is gone, or hidden under the full-screen view.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the right press kept through a close; the left or the right press kept through the cover.
	/// </remarks>
	[TestMethod]
	public void AWaitingPressOfEitherButtonDiesWithItsWindowOrUnderTheCover()
	{
		var rights = 0;

		_window.Panel.RightClicked = () => ++rights;

		Frame( true, OnThePanel, 1000, right: true );
		Frame( false, OnThePanel, 1100, right: true );
		Assert.AreEqual( 1, rights, "a right click reaches the panel" );

		try
		{
			Frame( true, OnThePanel, 3000 );
			_stack.Cover( true );
			Frame( false, OnThePanel, 3100 );
			_stack.Cover( false );
			Assert.AreEqual( 0, _window.PanelClicks, "the cover went on under the left press: no click" );

			Frame( true, OnThePanel, 5000, right: true );
			_stack.Cover( true );
			Frame( false, OnThePanel, 5100, right: true );
			_stack.Cover( false );
			Assert.AreEqual( 1, rights, "nor under the right" );
		}
		finally
		{
			_stack.Cover( false );
		}

		Frame( true, OnThePanel, 7000, right: true );
		_stack.Close( _window );
		Frame( false, OnThePanel, 7100, right: true );
		Assert.AreEqual( 1, rights, "the window closed under the right press: no click" );
	}

	/// <summary>The middle of one of the list's rows, in window pixels: rows are 44 tall from (600,100).</summary>
	private static Vector2 Row( int slot ) => new( 700, 100 + (slot * 44) + 22 );

	/// <summary>One frame of the game: a button and the pointer at a time, then the stack's update.</summary>
	private void Frame( bool down, Vector2 at, long now, bool right = false, bool moved = false )
	{
		Time.PinWall( now );

		var delta = moved ? new Vector2( 1, 0 ) : default;

		Input.Mouse = right
			? new() { Right = down, RightWentDown = down && !Input.Mouse.Right, Position = at, Delta = delta }
			: new() { Left = down, LeftWentDown = down && !Input.Mouse.Left, Position = at, Delta = delta };

		_stack.Update();
	}

	/// <summary>A panel that counts the double click's seconds it is told of.</summary>
	private sealed class APanel : UiControl
	{
		public int Seconds { get; private set; }

		internal override void LeftDoubleClicked() => ++Seconds;
	}

	/// <summary>A window with a plain panel, a button, a three-row list and a slider, each counting what reaches it.</summary>
	private sealed class AWindow : UiWindow
	{
		public APanel Panel { get; }
		public UiList List { get; }
		public UiSlider Slider { get; }
		public List<int> Chosen { get; } = [];
		public int PanelClicks { get; private set; }
		public int ButtonClicks { get; private set; }
		public int ButtonLeftClicks { get; private set; }

		public AWindow( WindowStack stack ) : base( stack )
		{
			Root = new UiControl { Rect = new UiRect( 0, 0, 1200, 900 ), HoldsChildren = true };

			Panel = Root.Add( new APanel { Rect = new UiRect( 100, 100, 300, 300 ) } );
			Panel.LeftClicked = () => ++PanelClicks;

			var button = Root.Add( new UiButton { Rect = new UiRect( 400, 100, 500, 200 ) } );
			button.Clicked = () => ++ButtonClicks;
			button.LeftClicked = () => ++ButtonLeftClicks;

			List = Root.Add( new UiList
			{
				Rect = new UiRect( 600, 100, 800, 540 ),
				RowArea = new UiRect( 600, 100, 800, 540 ),
				Columns = [(0, 200)],
				SelectsUnderPointer = true,
				Activated = Chosen.Add
			} );

			Slider = Root.Add( new UiSlider
			{
				Rect = new UiRect( 100, 700, 500, 740 ),
				Track = new UiRect( 100, 700, 500, 740 ),
				HitRect = new UiRect( 100, 700, 500, 740 )
			} );

			Slider.AddThumb( new UiSliderThumb { Rect = new UiRect( 100, 700, 140, 740 ) } );

			List.Build();

			for ( var row = 0; row < 3; ++row )
				List.Add( new UiList.Row( 100 + row, $"row {row}", row ) );
		}
	}
}
