using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park gadget's body, handle, arm and aerial stop the pointer, as every shown control of the original's does
/// (<c>docs/exe/hud.md</c>, "The management gadget"; <c>docs/exe/park-engine.md</c>, "Whose a right press is"). These
/// read the gadget's meshes from the shipped game and are skipped where there is no installation - see
/// <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkGadgetPressTests
{
	private Point2 _screen;
	private float _now;

	[TestInitialize]
	public void MountTheGame()
	{
		// The interface's own 2048x1536, so a point in the window is the same point on the gadget's layout.
		_screen = Screen.Size;
		_now = Time.Now;
		Screen.Size = new Point2( 2048, 1536 );

		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Input.ForgetHeldKeys();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		Time.Now = _now;
		Input.Mouse = new();
		Input.ForgetHeldKeys();
		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>The body takes a press inside its 23-point outline and not in the rest of its rectangle</b>, and a right press
	/// there is the interface's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the body with no outline; the body answering over its whole rectangle.</remarks>
	[TestMethod]
	public void TheBodyTakesAPressInsideItsOutline()
	{
		var (stack, gadget) = AGadget();

		Assert.AreEqual( 0x1d, Under( gadget, 240, 1030 ), "bare metal above the date" );
		Assert.AreEqual( 0x1d, Under( gadget, 300, 1490 ), "bare metal under the camcorder button" );
		Assert.IsNull( Under( gadget, 50, 1000 ), "the rectangle's top-left corner, which the outline leaves bare" );
		Assert.IsNull( Under( gadget, 420, 1500 ), "its bottom-right corner, past the outline and the handle" );

		Assert.IsTrue( stack.TakesRightPress( 240, 1030 ) );
		Assert.IsFalse( stack.TakesRightPress( 50, 1000 ) );
		Assert.IsTrue( stack.ClickAt( 240, 1030 ), "and a left press there goes no further" );
	}

	/// <summary>
	/// <b>With the arm in, the handle sits 645 to the left of the stream's place, past the body's right edge</b>, and the
	/// arm and its end are 30 wide behind the body; out, all three are at the stream's rectangles.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the arm left out at build; the handle not moved with the end; its outline left behind when it
	/// moves; the arm or its end not stopping the pointer; the travel 675, the arm's whole width.
	/// </remarks>
	[TestMethod]
	public void TheHandleFollowsTheArmInAndOut()
	{
		var (stack, gadget) = AGadget();

		Assert.IsFalse( gadget.ArmOut, "built in" );
		Assert.AreEqual( 0x23, Under( gadget, 460, 1310 ), "the handle, past the body's 439" );
		Assert.AreEqual( 0x23, Under( gadget, 483, 1310 ), "its outline's rightmost point, 1129 less 645, is 484" );
		Assert.IsNull( Under( gadget, 486, 1310 ), "and nothing past it" );
		Assert.IsNull( Under( gadget, 1100, 1300 ), "nothing where the stream puts the handle" );
		Assert.IsNull( Under( gadget, 850, 1150 ), "nor where it puts the arm" );
		Assert.AreEqual( new UI.UiRect( 331, 1069, 361, 1495 ), Find( gadget, 0x21 ).Rect );
		Assert.AreEqual( new UI.UiRect( 361, 1069, 426, 1382 ), Find( gadget, 0x22 ).Rect );
		Assert.AreEqual( new UI.UiRect( 331, 1058, 484, 1503 ), Find( gadget, 0x23 ).Rect );

		Assert.IsTrue( stack.ClickAt( 261, 1414 ), "the camcorder button, 0x27" );

		Assert.IsTrue( gadget.ArmOut );
		Assert.AreEqual( 0x21, Under( gadget, 850, 1150 ), "the arm's bare panel" );
		Assert.AreEqual( 0x22, Under( gadget, 1030, 1200 ), "its end" );
		Assert.AreEqual( 0x23, Under( gadget, 1100, 1300 ), "the handle inside its outline" );
		Assert.IsNull( Under( gadget, 1120, 1070 ), "and not in the corner of its rectangle the outline leaves bare" );
		Assert.AreEqual( 0x21, Under( gadget, 460, 1310 ), "where the handle was is the arm now" );
		Assert.IsTrue( stack.TakesRightPress( 850, 1150 ) );
		Assert.AreEqual( new UI.UiRect( 331, 1069, 1006, 1495 ), Find( gadget, 0x21 ).Rect );
		Assert.AreEqual( new UI.UiRect( 976, 1058, 1129, 1503 ), Find( gadget, 0x23 ).Rect );

		Assert.IsTrue( stack.ClickAt( 389, 1426 ), "the retract button, 0x24" );

		Assert.IsFalse( gadget.ArmOut );
		Assert.AreEqual( 0x23, Under( gadget, 460, 1310 ), "and the handle is back" );
		Assert.IsNull( Under( gadget, 850, 1150 ) );
	}

	/// <summary>
	/// <b>Where two of the body's children meet, the later in the stream answers</b> (<c>FUN_0065db25</c>): the six
	/// buttons' panel before the arm, the arm before the date, any of them before the body.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the arm added after the buttons' panel; the arm added before the date; the panel not stopping
	/// the pointer.
	/// </remarks>
	[TestMethod]
	public void TheLaterChildAnswersWhereTwoMeet()
	{
		var (_, gadget) = AGadget();

		Assert.AreEqual( 0x21, Under( gadget, 345, 1100 ), "the date's (153,1044)-(404,1121) under the arm's 331 to 361" );
		Assert.AreEqual( 0x20, Under( gadget, 300, 1100 ), "the date beside it" );
		Assert.AreEqual( 0x25, Under( gadget, 340, 1127 ), "the buttons' panel over the arm, above the info button" );
		Assert.AreEqual( 0x2b, Under( gadget, 390, 1370 ), "research's corner, which the handle's outline reaches" );
		Assert.AreEqual( 0x23, Under( gadget, 395, 1370 ), "and the handle just past it" );
	}

	/// <summary>
	/// <b>The arm and its end are drawn behind the body and the handle in front of it</b>, by the depths
	/// <c>FUN_004a1d70</c> gives them: 4, 4, the retract button 7, the body 8, the handle 9, the rest 10 and up.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the body not drawing by depth; the handle left at its parent's depth and two; the sort not
	/// stable; a hidden control drawn.
	/// </remarks>
	[TestMethod]
	public void TheArmIsDrawnBehindTheBodyAndTheHandleInFront()
	{
		var (stack, gadget) = AGadget();

		CollectionAssert.AreEqual(
			new[] { 0x21, 0x22, 0x1d, 0x23, 0x1e, 0x20, 0x25, 0x1f, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b },
			Find( gadget, 0x1d ).DrawOrder().Select( control => control.Id ).ToArray(), "the arm in" );

		stack.ClickAt( 261, 1414 );

		CollectionAssert.AreEqual(
			new[] { 0x21, 0x22, 0x62, 0x24, 0x1d, 0x63, 0x64, 0x23, 0x1e, 0x20, 0x25, 0x1f, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b },
			Find( gadget, 0x1d ).DrawOrder().Select( control => control.Id ).ToArray(), "the arm out" );
	}

	/// <summary>
	/// <b>The aerial stands as the builder leaves it with no message</b>: the mast eight high on the body, the red top
	/// 58 below the stream's place; both take a press, and the top's right click is counted.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the aerial at the stream's rectangles; the mast not stopping the pointer; the count on the
	/// press rather than the click; the count on every control's click; no count.
	/// </remarks>
	[TestMethod]
	public void TheAerialTakesAPressAndCountsItsRightClick()
	{
		var stack = new UI.WindowStack();
		var gadget = new UI.ParkGadget( stack );
		stack.Open( gadget );

		Assert.AreEqual( new UI.UiRect( 94, 976, 144, 984 ), Find( gadget, 0x2d ).Rect );
		Assert.AreEqual( new UI.UiRect( 77, 848, 161, 976 ), Find( gadget, 0x2e ).Rect );
		Assert.AreEqual( 0x2e, Under( gadget, 119, 900 ), "the red top" );
		Assert.AreEqual( 0x2d, Under( gadget, 119, 980 ), "the mast" );
		Assert.IsNull( Under( gadget, 119, 820 ), "nothing where the top would be with a message under it" );
		Assert.AreEqual( 476, Find( gadget, 0x2e ).HelpText );

		var before = Times( "AERIAL_DELETE_ALL_MESSAGES" );

		Frame( stack, true, new Vector2( 119, 900 ), 10.00f );
		Assert.AreEqual( before, Times( "AERIAL_DELETE_ALL_MESSAGES" ), "the press alone is no click" );

		Frame( stack, false, new Vector2( 119, 900 ), 10.08f );
		Assert.AreEqual( before + 1, Times( "AERIAL_DELETE_ALL_MESSAGES" ), "the release makes it" );

		Frame( stack, true, new Vector2( 119, 980 ), 12.00f );
		Frame( stack, false, new Vector2( 119, 980 ), 12.08f );
		Frame( stack, true, new Vector2( 240, 1030 ), 14.00f );
		Frame( stack, false, new Vector2( 240, 1030 ), 14.08f );
		Assert.AreEqual( before + 1, Times( "AERIAL_DELETE_ALL_MESSAGES" ), "a right click on the mast or the body is not the top's" );
	}

	private static (UI.WindowStack Stack, UI.ParkGadget Gadget) AGadget()
	{
		var stack = new UI.WindowStack();
		var gadget = new UI.ParkGadget( stack );

		stack.Open( gadget );

		return (stack, gadget);
	}

	/// <summary>The id of the control the pointer stops at, or null.</summary>
	private static int? Under( UI.ParkGadget gadget, float x, float y ) => gadget.Root.HitTest( x, y )?.Id;

	private static UI.UiControl Find( UI.ParkGadget gadget, int id )
	{
		return In( gadget.Root ) ?? throw new AssertFailedException( $"no control 0x{id:x}" );

		UI.UiControl? In( UI.UiControl control )
			=> control.Id == id ? control : control.Children.Select( In ).FirstOrDefault( found => found != null );
	}

	/// <summary>One frame of the game: the right button and the pointer at a time, then the stack's update.</summary>
	private static void Frame( UI.WindowStack stack, bool right, Vector2 at, float now )
	{
		Time.Now = now;
		Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = at };
		stack.Update();
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}

/// <summary>
/// What <see cref="UI.UiControl"/> was given for the gadget: a move that takes everything with it, and a draw in order
/// of depth. These need no game.
/// </summary>
[TestClass]
public class UiControlMoveAndDepthTests
{
	/// <summary>
	/// <b>A moved control takes its outline, its text rectangle and its children with it</b> (<c>FUN_0065c7fe</c>).
	///
	/// </summary>
	/// <remarks><b>Mutations:</b> the children left; the outline left; the text rectangle left; the outline shared with the array it was given.</remarks>
	[TestMethod]
	public void AMovedControlTakesEverythingWithIt()
	{
		var given = new[] { new UI.UiPoint( 10, 10 ), new UI.UiPoint( 30, 10 ), new UI.UiPoint( 20, 40 ) };

		var control = new UI.UiControl
		{
			Rect = new UI.UiRect( 10, 10, 30, 40 ),
			TextRect = new UI.UiRect( 12, 12, 28, 20 ),
			Outline = given
		};

		var child = control.Add( new UI.UiControl { Rect = new UI.UiRect( 15, 15, 25, 25 ) } );

		control.Move( -5, 100 );

		Assert.AreEqual( new UI.UiRect( 5, 110, 25, 140 ), control.Rect );
		Assert.AreEqual( new UI.UiRect( 7, 112, 23, 120 ), control.TextRect );
		Assert.AreEqual( new UI.UiPoint( 15, 140 ), control.Outline![2] );
		Assert.AreEqual( new UI.UiRect( 10, 115, 20, 125 ), child.Rect );
		Assert.AreEqual( new UI.UiPoint( 20, 40 ), given[2], "the array it was given is not written to" );
	}

	/// <summary>
	/// <b>A control that draws by depth draws the lowest first, itself among them</b>, so a child can lie behind its
	/// parent; a hidden one and all under it are left out; and without the switch a parent comes before its children.
	///
	/// </summary>
	/// <remarks><b>Mutations:</b> the draw not reading the switch; the switch read as always on; a hidden control drawn.</remarks>
	[TestMethod]
	public void ADepthDrawnControlDrawsTheLowestFirst()
	{
		foreach ( var (byDepth, expected) in new[] { (true, "2 1 3 4"), (false, "1 2 3 4") } )
		{
			var drawn = new System.Collections.Generic.List<int>();
			var parent = new Recording( drawn ) { Id = 1, Depth = 8, DrawsByDepth = byDepth };
			var behind = parent.Add( new Recording( drawn ) { Id = 2, Depth = 4 } );

			behind.Add( new Recording( drawn ) { Id = 3, Depth = 9 } );
			parent.Add( new Recording( drawn ) { Id = 4 } );
			parent.Add( new Recording( drawn ) { Id = 5, Depth = 0, Visible = false } ).Add( new Recording( drawn ) { Id = 6 } );

			parent.Draw();

			Assert.AreEqual( expected, string.Join( " ", drawn ), byDepth ? "by depth" : "by the tree" );
		}
	}

	/// <summary>A control that notes its id when it is drawn, and draws nothing.</summary>
	private sealed class Recording( System.Collections.Generic.List<int> drawn ) : UI.UiControl
	{
		protected override void OnDraw() => drawn.Add( Id );
	}
}
