using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
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
		Time.PinWall( 0 );
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
	/// arm and its end are 30 wide behind the body. <b>The camcorder button slides it out with its panel</b> (the arm's
	/// <c>0x004a14f0</c>): ten steps of 52, 20 ms apart, as the original's handle was read to move, the panel put at
	/// the arm's left less its width and fifty and coming out with it, each part shown once its right edge is past the
	/// body's, at (482,1206)-(801,1359) when out; an eleventh firing ends it. The retract button slides it in the same.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the arm left out at build; the handle not moved with the end; its outline left behind when it
	/// moves; the arm or its end not stopping the pointer; the travel the stream's 645; a step every frame; the ten steps
	/// ended on the tenth firing; the panel not moved with the arm; its parts shown by the arm's left in the steps; the
	/// panel not hidden at the end in.
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

		var panel = Find( gadget, 0x62 );
		Assert.AreEqual( (true, UI.ParkGadget.ArmMove.Out), (gadget.ArmOut, gadget.Moving) );
		Assert.AreEqual( (new UI.UiRect( -38, 1206, 281, 1359 ), false), (panel.Rect, panel.Visible), "the arm's left less its width and fifty" );
		Assert.AreEqual( new UI.UiRect( 331, 1058, 484, 1503 ), Find( gadget, 0x23 ).Rect, "no step before 20 ms" );
		Assert.IsTrue( Find( gadget, 0x24 ).Visible, "the retract button as it starts" );

		gadget.Slide( 19f );
		Assert.AreEqual( 331, Find( gadget, 0x23 ).Rect.Left );
		gadget.Slide( 1f );
		Assert.AreEqual( (383, new UI.UiRect( 331, 1069, 413, 1495 )), (Find( gadget, 0x23 ).Rect.Left, Find( gadget, 0x21 ).Rect), "a step of 52" );

		gadget.Slide( 40f );
		Assert.AreEqual( (487, false, false), (Find( gadget, 0x23 ).Rect.Left, Find( gadget, 0x64 ).Visible, panel.Visible),
			"three steps: the postcard button's and the panel's right edges, 437, past the arm's left and not yet the body's 439" );
		gadget.Slide( 20f );
		Assert.AreEqual( (539, 170), (Find( gadget, 0x23 ).Rect.Left, panel.Rect.Left), "four steps: the panel as far" );
		Assert.AreEqual( (true, false, true), (panel.Visible, Find( gadget, 0x63 ).Visible, Find( gadget, 0x64 ).Visible),
			"the panel's and the postcard button's right edges, 489, are past the body's 439; the camera button's, 331, is not" );

		gadget.Slide( 120f );
		Assert.AreEqual( (851, UI.ParkGadget.ArmMove.Out), (Find( gadget, 0x23 ).Rect.Left, gadget.Moving), "ten steps, and the slide not over" );
		gadget.Slide( 20f );
		Assert.AreEqual( UI.ParkGadget.ArmMove.Rest, gadget.Moving, "the eleventh firing ends it" );

		Assert.AreEqual( (new UI.UiRect( 482, 1206, 801, 1359 ), true, true, true), (panel.Rect, panel.Visible, Find( gadget, 0x63 ).Visible, Find( gadget, 0x64 ).Visible) );
		Assert.AreEqual( 0x21, Under( gadget, 850, 1150 ), "the arm's bare panel" );
		Assert.AreEqual( 0x22, Under( gadget, 910, 1200 ), "its end" );
		Assert.AreEqual( 0x23, Under( gadget, 975, 1300 ), "the handle inside its outline" );
		Assert.IsNull( Under( gadget, 995, 1070 ), "and not in the corner of its rectangle the outline leaves bare" );
		Assert.AreEqual( 0x21, Under( gadget, 460, 1310 ), "where the handle was is the arm now" );
		Assert.IsTrue( stack.TakesRightPress( 850, 1150 ) );
		Assert.AreEqual( new UI.UiRect( 331, 1069, 881, 1495 ), Find( gadget, 0x21 ).Rect );
		Assert.AreEqual( new UI.UiRect( 851, 1058, 1004, 1503 ), Find( gadget, 0x23 ).Rect );

		Assert.IsTrue( stack.ClickAt( 389, 1426 ), "the retract button, 0x24" );

		var retract = (UI.UiButton)Find( gadget, 0x24 );
		Assert.AreEqual( (true, UI.ParkGadget.ArmMove.In, true, false), (gadget.ArmOut, gadget.Moving, retract.Visible, retract.Enabled), "coming in, the retract button switched off and drawn" );
		gadget.Slide( 100f );
		Assert.AreEqual( (591, 222), (Find( gadget, 0x23 ).Rect.Left, panel.Rect.Left), "five steps of -52" );
		Assert.IsTrue( retract.Visible, "still drawn half way in" );
		gadget.Slide( 100f );
		Assert.AreEqual( (331, true), (Find( gadget, 0x23 ).Rect.Left, gadget.ArmOut), "in, and the panel still on it" );
		gadget.Slide( 20f );

		Assert.AreEqual( (false, false, UI.ParkGadget.ArmMove.Rest), (gadget.ArmOut, panel.Visible, gadget.Moving) );
		Assert.IsFalse( retract.Visible, "hidden at rest in" );
		Assert.AreEqual( 0x23, Under( gadget, 460, 1310 ), "and the handle is back" );
		Assert.IsNull( Under( gadget, 850, 1150 ) );
	}

	/// <summary>
	/// <b>The frame clock steps the slide</b>: the gadget's own turn runs the arm's timer over the frame's
	/// <see cref="Time.Delta"/>, so nothing moves with the clock held and a quarter second ends the slide.
	/// </summary>
	/// <remarks><b>Mutations:</b> the slide not stepped in the gadget's turn; the wall clock for the frame's.</remarks>
	[TestMethod]
	public void TheFrameClockStepsTheSlide()
	{
		var (stack, gadget) = AGadget();
		var delta = Time.Delta;

		try
		{
			Time.Delta = 0f;
			Assert.IsTrue( stack.ClickAt( 261, 1414 ), "the camcorder button" );
			stack.Update();
			Assert.AreEqual( (331, UI.ParkGadget.ArmMove.Out), (Find( gadget, 0x23 ).Rect.Left, gadget.Moving), "the clock held: no step" );

			Time.Delta = 0.05f;
			stack.Update();
			Assert.AreEqual( 435, Find( gadget, 0x23 ).Rect.Left, "50 ms: two steps of 52" );

			Time.Delta = 0.25f;
			stack.Update();
			Assert.AreEqual( (851, UI.ParkGadget.ArmMove.Rest), (Find( gadget, 0x23 ).Rect.Left, gadget.Moving) );
		}
		finally
		{
			Time.Delta = delta;
		}
	}

	/// <summary>
	/// <b>A slide turned back comes in by whole tenths of where the arm stands, and ends at its rest</b> (the arm's <c>0xb</c>: a tenth of the
	/// arm's left less the handle's, a whole division, from wherever it stands): three steps of 52 out, the camcorder
	/// button up, a step of -15; a panel asked for then sends the arm in again (<c>FUN_004a2590</c>'s <c>0xb</c>), ten
	/// steps of -14 from 472, to 332, and the slide's end puts the arm back at its rest, 331, as the original's was read
	/// to do; the panel waits, and goes out once the arm is in; a third asked for meanwhile is refused.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the step in figured from the travel's end, not the handle; the division rounded; the timer not
	/// started again, so the turn keeps the old phase; the arm left where the tenth step leaves it; a panel waiting sent
	/// at once; a panel waiting forgotten at the end;
	/// a third panel taking the waiting one's place; the fold while coming in sending the arm in again.
	/// </remarks>
	[TestMethod]
	public void ASlideTurnedBackComesInFromWhereItStands()
	{
		var (stack, gadget) = AGadget();
		var handle = Find( gadget, 0x23 );
		var staff = gadget.Locator.StaffPanel;
		var visitors = gadget.Locator.VisitorPanel;

		Assert.IsTrue( stack.ClickAt( 261, 1414 ), "the camcorder button down" );
		gadget.Slide( 70f );
		Assert.AreEqual( 487, handle.Rect.Left, "three steps of 52" );

		Assert.IsTrue( stack.ClickAt( 261, 1414 ), "and up" );
		Assert.AreEqual( UI.ParkGadget.ArmMove.In, gadget.Moving );
		gadget.Slide( 19f );
		Assert.AreEqual( 487, handle.Rect.Left, "the timer started again: no step for 20 ms" );
		gadget.Slide( 1f );
		Assert.AreEqual( 472, handle.Rect.Left, "a step of -156 / 10, -15" );

		Assert.IsTrue( gadget.Carry( staff ), "a panel asked for while the arm comes in" );
		StringAssert.Contains( gadget.BarCensus(), "move=In carried=62@" );
		StringAssert.Contains( gadget.BarCensus(), "waiting=95c" );
		Assert.IsFalse( gadget.Carry( visitors ), "a third is refused while one waits" );

		gadget.Slide( 200f );
		Assert.AreEqual( (332, UI.ParkGadget.ArmMove.In), (handle.Rect.Left, gadget.Moving),
			"the ask sent the arm in again from where it stood: ten steps of -141 / 10, -14, one short of the rest" );
		gadget.Slide( 20f );
		Assert.AreEqual( (UI.ParkGadget.ArmMove.Out, 331), (gadget.Moving, handle.Rect.Left), "in, put back at its rest, and the panel waiting sent out" );
		Assert.IsFalse( Find( gadget, 0x62 ).Visible, "the camcorder panel off the arm" );

		gadget.Slide( 220f );
		Assert.AreEqual( (UI.ParkGadget.ArmMove.Rest, 331 + 670), (gadget.Moving, handle.Rect.Left), "out by its own travel" );
		Assert.AreEqual( new UI.UiRect( 484, 1127, 951, 1438 ), staff.Rect, "and the panel where the arm puts it, whatever the handle" );

		gadget.Carry( visitors );
		StringAssert.Contains( gadget.BarCensus(), "waiting=963" );
		gadget.Fold( liftsCamcorder: false );
		StringAssert.Contains( gadget.BarCensus(), "move=In carried=95c@", "the fold while coming in forgets the panel waiting" );
		StringAssert.Contains( gadget.BarCensus(), "waiting=none" );
		Settle( gadget );
		Assert.AreEqual( (false, 331, new UI.UiRect( 331, 1069, 361, 1495 )), (gadget.ArmOut, handle.Rect.Left, Find( gadget, 0x21 ).Rect), "in, nothing carried" );
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
		Settle( gadget );

		CollectionAssert.AreEqual(
			new[] { 0x62, 0x21, 0x22, 0x63, 0x64, 0x24, 0x1d, 0x23, 0x1e, 0x20, 0x25, 0x1f, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b },
			Find( gadget, 0x1d ).DrawOrder().Select( control => control.Id ).ToArray(), "the arm out, its panel at 3 and the buttons at 5" );
	}

	/// <summary>
	/// <b>Every panel the arm carries is drawn behind the body and the retract button</b>: the arm's 0xa sets the panel
	/// three under the depth its link gave it (0x004a15bf to 0x004a15cd), the arm's 4 and two less three, so 3, its
	/// parts 5 before the retract button's 7 and the body's 8, and theirs 7, tied with the retract button - the camcorder panel, the message bar
	/// and both locators' panels, each shown whole as it stands half way out. A locator's buttons, under a group, stand
	/// at 7 with the retract button.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> any one panel left at its parent's depth and two; the carried depth the arm's less three, not
	/// its link's.
	/// </remarks>
	[TestMethod]
	public void EveryCarriedPanelIsDrawnBehindTheBody()
	{
		foreach ( var id in new[] { 0x62, 0x10d2d, 0x95c, 0x963 } )
		{
			var (_, gadget) = AGadget();
			var panel = Find( gadget, id );
			Show( panel );
			Find( gadget, 0x24 ).Visible = true;

			var order = Find( gadget, 0x1d ).DrawOrder().Select( control => control.Id ).ToList();
			var parts = Everything( panel ).Select( control => control.Id ).ToList();

			Assert.AreEqual( 0, order.IndexOf( id ), $"0x{id:x} at 3, before the arm at 4" );
			Assert.IsTrue( order.IndexOf( 0x21 ) < order.IndexOf( parts[1] ), $"0x{id:x}'s parts at 5, over the arm" );
			Assert.IsTrue( panel.Children.All( part => order.IndexOf( part.Id ) < order.IndexOf( 0x24 ) ), $"0x{id:x}'s parts behind the retract button" );
			Assert.IsTrue( parts.All( part => order.IndexOf( part ) < order.IndexOf( 0x1d ) ), $"0x{id:x} wholly behind the body" );
			Assert.IsTrue( order.IndexOf( 0x24 ) < order.IndexOf( 0x1d ), "the retract button behind the body" );
		}

		static void Show( UI.UiControl control )
		{
			control.Visible = true;

			foreach ( var child in control.Children )
				Show( child );
		}

		static IEnumerable<UI.UiControl> Everything( UI.UiControl control )
			=> control.Children.SelectMany( Everything ).Prepend( control );
	}

	/// <summary>
	/// <b>The aerial stands as the builder leaves it with no tag</b>: the mast eight high on the body, the red top
	/// 58 below the stream's place; both take a press, and the top's right click with no tag changes nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the aerial at the stream's rectangles; the mast not stopping the pointer.</remarks>
	[TestMethod]
	public void TheAerialTakesAPress()
	{
		var (stack, gadget) = AGadget();
		gadget.Tags = new ParkTags();

		Assert.AreEqual( new UI.UiRect( 94, 976, 144, 984 ), Find( gadget, 0x2d ).Rect );
		Assert.AreEqual( new UI.UiRect( 77, 848, 161, 976 ), Find( gadget, 0x2e ).Rect );
		Assert.AreEqual( 0x2e, Under( gadget, 119, 900 ), "the red top" );
		Assert.AreEqual( 0x2d, Under( gadget, 119, 980 ), "the mast" );
		Assert.IsNull( Under( gadget, 119, 820 ), "nothing where the top would be with a tag under it" );
		Assert.AreEqual( 476, Find( gadget, 0x2e ).HelpText );

		Frame( stack, true, new Vector2( 119, 900 ), 10.00f );
		Frame( stack, false, new Vector2( 119, 900 ), 10.08f );
		Assert.AreEqual( new UI.UiRect( 77, 848, 161, 976 ), Find( gadget, 0x2e ).Rect );
	}

	/// <summary>
	/// <b>A tag stands on the mast and lifts the red top by its height and ten</b> (<c>LAB_004a11e0</c>, 0x100001):
	/// the oldest lowest, each 70 by 65 across the mast's middle, eight under where the top stood, showing its row's
	/// part of msgtag.md2, shown half a second after it is put on (the mast's message 6, <c>0x004a1286</c>); a hidden one
	/// keeps its place; one taken off puts the top and the later tags down.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the top not lifted; lifted by the tag's height alone; the newest tag lowest; the tag at the
	/// mast's top edge; the picture not set; shown at once; shown a frame late; a hidden tag still shown; a hidden tag's
	/// place given up at once; the old controls left on the mast.
	/// </remarks>
	[TestMethod]
	public void ATagStandsOnTheMastAndLiftsTheTop()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();

		Frame( stack, false, new Vector2( 1000, 100 ), 1.0f );
		var first = tags.Said( 68, 13, 1000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 1.1f );

		Assert.AreEqual( new UI.UiRect( 94, 901, 144, 984 ), Find( gadget, 0x2d ).Rect, "the mast up 75" );
		Assert.AreEqual( new UI.UiRect( 77, 773, 161, 901 ), Find( gadget, 0x2e ).Rect, "and the top on it" );
		Assert.AreEqual( new UI.UiRect( 84, 909, 154, 974 ), Find( gadget, 900 ).Rect );
		Assert.AreEqual( (1, 475, false), (Find( gadget, 900 ).Frame, Find( gadget, 900 ).HelpText, Find( gadget, 900 ).Visible),
			"the mast lifted at once, the tag not shown until the mast's message 6, half a second on" );
		Assert.AreNotEqual( 900, Under( gadget, 119, 940 ), "an unshown tag takes no pointer" );
		Frame( stack, false, new Vector2( 1000, 100 ), 1.499f );
		Assert.IsFalse( Find( gadget, 900 ).Visible );
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Assert.IsTrue( Find( gadget, 900 ).Visible, "shown 500 ms on" );
		Assert.AreEqual( 900, Under( gadget, 119, 940 ), "the tag takes the pointer" );
		Assert.AreEqual( 0x2e, Under( gadget, 119, 820 ), "the top where it now stands" );

		tags.Said( 150, 0, 1600 );
		Frame( stack, false, new Vector2( 1000, 100 ), 1.7f );
		Assert.AreEqual( new UI.UiRect( 94, 826, 144, 984 ), Find( gadget, 0x2d ).Rect );
		Assert.AreEqual( new UI.UiRect( 84, 909, 154, 974 ), Find( gadget, 900 ).Rect, "the older stays lowest" );
		Assert.AreEqual( new UI.UiRect( 84, 834, 154, 899 ), Find( gadget, 901 ).Rect );

		tags.Hide( first, 1800 );
		Frame( stack, false, new Vector2( 1000, 100 ), 1.9f );
		Assert.IsFalse( Find( gadget, 900 ).Visible, "hidden" );
		Assert.AreEqual( new UI.UiRect( 94, 826, 144, 984 ), Find( gadget, 0x2d ).Rect, "and its place kept for half a second" );
		Assert.AreNotEqual( 900, Under( gadget, 119, 940 ), "a hidden tag takes no pointer" );

		Frame( stack, false, new Vector2( 1000, 100 ), 2.3f );
		Assert.AreEqual( new UI.UiRect( 94, 901, 144, 984 ), Find( gadget, 0x2d ).Rect, "off: the top comes down" );
		Assert.AreEqual( new UI.UiRect( 84, 909, 154, 974 ), Find( gadget, 900 ).Rect, "and the later tag with it" );
		Assert.AreEqual( 1, tags.Standing.Count );
		Assert.AreEqual( 2, Find( gadget, 0x2d ).Children.Count, "the top and the one tag: no control of a tag taken off is left on the mast" );
	}

	/// <summary>
	/// <b>A tag put on squashes the red top, and a tag taken off squashes the top and every tag above it</b>
	/// (<c>LAB_004a11e0</c> <c>0x004a135b</c>, <c>FUN_0048dae0</c> <c>0x0048dcda</c>, <c>0x0048dd06</c>): the
	/// streams at 0x00752e48 and 0x007502f0, the left and right out and the top down by 2, 4 ... 12, 8, 4, 6, 8, 6, 4,
	/// 2, 0, a row on the first frame more than 50 ms after the last on the top and 20 ms on a tag; a tag put on while the top plays does not start it
	/// again, a tag taken off does; a tag below the one gone keeps still.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no squash; the top's stream at a tag's 20 ms; a tag's at the top's 50; the bottom moved too; the
	/// put-on stream started over; the take-off's not; the tags below the one gone squashed; a row on the ideal clock, not by frames; a tag above restarted though it plays (0x0048dcce); any of rows 8 to 12 of the table changed.
	/// </remarks>
	[TestMethod]
	public void ATagPutOnOrTakenOffSquashesTheTopAndTheTagsAbove()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();
		var at = 0.89f;
		// A frame each 10 ms up to the time given, so a 50 ms row comes on the frame 60 ms after the last and a 20 ms one 30 ms after.
		void Until( float end )
		{
			for ( ; at < end - 0.0005f; ) Frame( stack, false, new Vector2( 1000, 100 ), at = System.MathF.Round( (at + 0.01f) * 100f ) / 100f );
		}
		UI.UiRect Top() => Find( gadget, 0x2e ).Rect;

		Until( 0.99f );
		tags.Said( 68, 13, 1000 );
		Until( 1.05f );
		Assert.AreEqual( new UI.UiRect( 77, 773, 161, 901 ), Top(), "no row yet: a row comes only more than 50 ms on" );
		Until( 1.06f );
		Assert.AreEqual( new UI.UiRect( 75, 775, 163, 901 ), Top(), "a row of two, the bottom still" );

		tags.Said( 68, 14, 1100 );
		Until( 1.18f );
		Assert.AreEqual( new UI.UiRect( 71, 704, 167, 826 ), Top(), "three rows from the first tag: the second did not start it again" );
		Until( 1.36f );
		Assert.AreEqual( new UI.UiRect( 65, 710, 173, 826 ), Top(), "six rows, twelve" );
		Until( 1.42f );
		Assert.AreEqual( new UI.UiRect( 69, 706, 169, 826 ), Top(), "the seventh row back four" );
		Until( 1.48f );
		Assert.AreEqual( new UI.UiRect( 73, 702, 165, 826 ), Top(), "row 8: four" );
		Until( 1.54f );
		Assert.AreEqual( new UI.UiRect( 71, 704, 167, 826 ), Top(), "row 9: six" );
		Until( 1.60f );
		Assert.AreEqual( new UI.UiRect( 69, 706, 169, 826 ), Top(), "row 10: eight" );
		Until( 1.66f );
		Assert.AreEqual( new UI.UiRect( 71, 704, 167, 826 ), Top(), "row 11: six" );
		Until( 1.72f );
		Assert.AreEqual( new UI.UiRect( 73, 702, 165, 826 ), Top(), "row 12: four" );
		Until( 1.78f );
		Assert.AreEqual( new UI.UiRect( 75, 700, 163, 826 ), Top(), "thirteen rows" );
		Until( 1.95f );
		Assert.AreEqual( new UI.UiRect( 77, 698, 161, 826 ), Top(), "fourteen and the end: at rest" );
		StringAssert.Contains( gadget.AerialDrawn(), "drawn: top=77,698,161,826 " );

		tags.Said( 68, 15, 2000 );
		Until( 2.99f );
		tags.Hide( tags.Standing[0], 3000 );
		Until( 3.5f );
		Assert.AreEqual( 2, tags.Standing.Count );
		Until( 3.56f );
		Assert.AreEqual( new UI.UiRect( 80, 913, 158, 974 ), Find( gadget, 900 ).Rect, "the tag above moved down and two rows in at 20 ms" );
		Assert.AreEqual( new UI.UiRect( 80, 838, 158, 899 ), Find( gadget, 901 ).Rect, "and the one above it" );
		Assert.AreEqual( new UI.UiRect( 75, 700, 163, 826 ), Top(), "the top one row in at 50 ms" );
		Until( 3.68f );
		Assert.AreEqual( new UI.UiRect( 72, 921, 166, 974 ), Find( gadget, 900 ).Rect, "six rows" );
		Assert.AreEqual( new UI.UiRect( 71, 704, 167, 826 ), Top() );
		Until( 3.74f );
		Assert.AreEqual( new UI.UiRect( 80, 913, 158, 974 ), Find( gadget, 900 ).Rect, "a tag's row 8: four" );
		Until( 3.77f );
		Assert.AreEqual( new UI.UiRect( 78, 915, 160, 974 ), Find( gadget, 900 ).Rect, "row 9: six" );
		Until( 3.80f );
		Assert.AreEqual( new UI.UiRect( 76, 917, 162, 974 ), Find( gadget, 900 ).Rect, "row 10: eight" );
		Until( 3.83f );
		Assert.AreEqual( new UI.UiRect( 78, 915, 160, 974 ), Find( gadget, 900 ).Rect, "row 11: six" );
		Until( 3.85f );
		tags.Hide( tags.Standing[1], 3850 );
		Until( 3.86f );
		Assert.AreEqual( new UI.UiRect( 80, 913, 158, 974 ), Find( gadget, 900 ).Rect, "row 12: four" );
		Until( 3.89f );
		Assert.AreEqual( new UI.UiRect( 82, 911, 156, 974 ), Find( gadget, 900 ).Rect, "thirteen rows: two left" );
		Until( 3.92f );
		Assert.AreEqual( new UI.UiRect( 84, 909, 154, 974 ), Find( gadget, 900 ).Rect, "fourteen: at rest" );
		StringAssert.Contains( gadget.AerialCensus(), "top=77,698,161,826 tag900=84,909,154,974/", "the census reads the places at rest" );

		// The second goes while the top's stream still plays (its thirteenth row at 4280): started over.
		Until( 4.35f );
		Assert.AreEqual( 1, tags.Standing.Count );
		Until( 4.41f );
		Assert.AreEqual( new UI.UiRect( 75, 775, 163, 901 ), Top(), "one row of the stream started again" );
		Assert.AreEqual( new UI.UiRect( 84, 909, 154, 974 ), Find( gadget, 900 ).Rect, "the tag below the one gone keeps still" );

		// Three more tags, then the oldest two hidden 100 ms apart, so the second goes while the tags above still play the
		// first's 20 ms stream: they play none new (0x0048dcce), the stream running on from the first.
		tags.Said( 68, 14, 4450 );
		tags.Said( 68, 15, 4450 );
		tags.Said( 150, 0, 4450 );
		Until( 5.6f );
		Assert.AreEqual( 4, tags.Standing.Count );
		var older = tags.Standing[0];
		var second = tags.Standing[1];
		tags.Hide( older, 5600 );
		Until( 5.7f );
		tags.Hide( second, 5700 );
		Until( 6.11f );
		Assert.AreEqual( 3, tags.Standing.Count, "the oldest is gone, the second still stands" );
		Until( 6.21f );
		Assert.AreEqual( 2, tags.Standing.Count, "and the second is gone" );
		Until( 6.28f );
		Assert.AreEqual( new UI.UiRect( 72, 921, 166, 974 ), Find( gadget, 900 ).Rect, "the tag above plays on, row 6: twelve, not started again" );
		Assert.AreEqual( new UI.UiRect( 72, 846, 166, 899 ), Find( gadget, 901 ).Rect, "and the one above that" );
	}

	/// <summary>
	/// <b>A tag's right click hides it, its left click opens the message bar and takes nothing off, and the top's
	/// right click takes every tag off, the oldest first, a tenth of a second apart</b> (<c>FUN_0048dae0</c>,
	/// <c>FUN_004a1190</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a tag's right click doing nothing; taking it off at once; the tag being put on show hidden by it; the left click opening nothing; the
	/// top's right click doing nothing; taking every tag at once; the newest first.
	/// </remarks>
	[TestMethod]
	public void ATagAndTheTopAnswerTheirClicks()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();

		tags.Said( 68, 13, 1000 );
		tags.Said( 68, 14, 1000 );
		tags.Said( 68, 15, 1000 );
		Frame( stack, false, new Vector2( 1000, 100 ), 2.0f );
		Assert.AreEqual( 902, Under( gadget, 119, 790 ) );

		Input.Mouse = new() { Left = true, LeftWentDown = true, Position = new Vector2( 119, 790 ) };
		stack.Update();
		Input.Mouse = new() { Position = new Vector2( 119, 790 ) };
		stack.Update();
		Assert.AreEqual( (tags.Standing[2], tags.Standing[2]), (tags.OnShow, tags.PuttingOnShow), "on show as the arm starts out, and being put on show" );
		Frame( stack, true, new Vector2( 119, 790 ), 2.10f );
		Frame( stack, false, new Vector2( 119, 790 ), 2.15f );
		Assert.IsFalse( tags.Standing[2].Hidden, "its right click refused while the arm slides out with it" );
		Settle( gadget );
		Assert.AreEqual( (true, tags.Standing[2], null), (gadget.BarOut, tags.OnShow, tags.PuttingOnShow), "the left click puts its message on the bar" );
		Assert.AreEqual( 3, tags.Standing.Count( tag => !tag.Hidden ), "and takes nothing off" );

		Frame( stack, true, new Vector2( 119, 790 ), 3.00f );
		Frame( stack, false, new Vector2( 119, 790 ), 3.08f );
		Assert.AreEqual( (3, true), (tags.Standing.Count, tags.Standing[2].Hidden), "the right click hides it where it stands" );
		Frame( stack, false, new Vector2( 1000, 100 ), 3.57f );
		Assert.AreEqual( 3, tags.Standing.Count );
		Frame( stack, false, new Vector2( 1000, 100 ), 3.58f );
		Assert.AreEqual( 2, tags.Standing.Count, "and half a second on it is off" );

		// The top stands on two tags: 976 - 150 = 826, so its middle is at 762.
		Assert.AreEqual( 0x2e, Under( gadget, 119, 762 ) );
		Frame( stack, true, new Vector2( 119, 762 ), 5.00f );
		Frame( stack, false, new Vector2( 119, 762 ), 5.08f );
		Assert.AreEqual( 2, tags.Standing.Count, "not at the click" );
		Frame( stack, false, new Vector2( 1000, 100 ), 5.18f );
		Assert.AreEqual( (1, 14), (tags.Standing.Count, tags.Standing[0].Subject), "the oldest a tenth of a second on" );
		Frame( stack, false, new Vector2( 1000, 100 ), 5.28f );
		Assert.AreEqual( 0, tags.Standing.Count, "and the next a tenth after" );
		Assert.AreEqual( new UI.UiRect( 77, 848, 161, 976 ), Find( gadget, 0x2e ).Rect, "the top back on the body" );
	}

	/// <summary>
	/// <b>A tag's left click puts its message on the bar and the bar on the arm</b> (<c>FUN_0048d9c0</c>, the arm's
	/// <c>0x004a14f0</c>): the arm 820 out, the bar at (486,1112)-(1101,1453) with the tag's text, its first button
	/// dressed by the row's type and its second hidden for nought, the erase button shown; the tag swells and
	/// shrinks while on show and stands past its life; the same tag clicked again folds the arm away.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the bar not shown; the arm's travel the camcorder's 645; the handle not moved; the text not
	/// set; the first button undressed; the second shown for nought; the erase button hidden; the tag's timers left
	/// running; no swell; the swell in the census; a second click opening again; the advisor
	/// not posted his line, or it left in the slots as the arm folds; the camcorder's panel shown with the bar.
	/// </remarks>
	[TestMethod]
	public void ATagsLeftClickPutsItsMessageOnTheArm()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		gadget.Park = new ParkState( null ) { AdvisorMessages = lines };

		Frame( stack, false, new Vector2( 1000, 100 ), 1.0f );
		var tag = tags.Said( 72, 13, 1000 )!;
		tag.Text = "A ride has broken down.";
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Assert.IsFalse( Find( gadget, 0x10d2d ).Visible, "no bar until a tag is opened" );
		Assert.IsNull( Under( gadget, 565, 1172 ) );

		Time.PinWall( 3000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );

		Assert.AreEqual( (true, true, tag), (gadget.BarOut, gadget.ArmOut, tags.OnShow) );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=284/subject=0 ", "the advisor is posted message 0x11c" );
		Assert.AreEqual( new UI.UiRect( 331, 1069, 1181, 1495 ), Find( gadget, 0x21 ).Rect, "the arm 820 out" );
		Assert.AreEqual( new UI.UiRect( 1151, 1058, 1304, 1503 ), Find( gadget, 0x23 ).Rect, "and the handle with it" );
		Assert.AreEqual( (new UI.UiRect( 486, 1112, 1101, 1453 ), true), (Find( gadget, 0x10d2d ).Rect, Find( gadget, 0x10d2d ).Visible) );
		Assert.AreEqual( ("A ride has broken down.", 6, new UI.UiRect( 637, 1129, 1084, 1433 )),
			(Find( gadget, 0x10d32 ).Text, Find( gadget, 0x10d32 ).Font, Find( gadget, 0x10d32 ).TextRect) );
		Assert.AreEqual( (true, 516), (Find( gadget, 0x10d30 ).Visible, Find( gadget, 0x10d30 ).HelpText), "mb_locate, \"Left-click to go there\"" );
		Assert.AreEqual( (true, null), (Find( gadget, 0x10d2f ).Visible, UI.ParkGadget.BarButtonLook( 0 )), "a second button of type nought, which has no look, is shown: the arm shows every part past the body" );
		Assert.IsTrue( Find( gadget, 0x10d2e ).Visible, "the erase button" );
		Assert.IsFalse( Find( gadget, 0x62 ).Visible, "the camcorder's panel is not on the arm" );
		Assert.IsTrue( Find( gadget, 0x24 ).Visible, "the retract button" );
		Assert.AreEqual( 0x10d30, Under( gadget, 565, 1172 ) );
		Assert.AreEqual( 0x10d2e, Under( gadget, 565, 1387 ) );
		var unbuilt = Times( "TAG_BAR_BUTTON" );
		Assert.AreEqual( 0x10d2f, Under( gadget, 565, 1280 ), "it takes the pointer" );
		Assert.IsTrue( stack.ClickAt( 565, 1280 ) );
		Assert.AreEqual( (unbuilt, true), (Times( "TAG_BAR_BUTTON" ), gadget.BarOut), "and its click does nothing, as FUN_0048e0f0 has no case for nought" );
		StringAssert.Contains( gadget.BarCensus(), "bar: out=1 rect=486,1112,1101,1453 arm=331,1069,1181,1495 handle=1151,1058,1304,1503" );
		StringAssert.Contains( gadget.BarCensus(), "second=none/help=-1" );
		StringAssert.Contains( gadget.BarCensus(), "text=\"A ride has broken down.\"" );

		// The stream at 0x007502b8: 0, 2, 4, 6, 4, 2, 0 a side, a step every 100 ms, from the opening.
		foreach ( var (at, swell) in new[] { (3.05f, 0), (3.15f, 2), (3.25f, 4), (3.35f, 6), (3.45f, 4), (3.55f, 2), (3.65f, 0), (3.75f, 0), (3.85f, 2), (4.05f, 6) } )
		{
			Frame( stack, false, new Vector2( 1000, 100 ), at );
			Assert.AreEqual( new UI.UiRect( 84 - swell, 909 - swell, 154 + swell, 974 + swell ), Find( gadget, 900 ).Rect, $"the tag on show at {at}" );
		}

		StringAssert.Contains( gadget.AerialCensus(), "tag900=84,909,154,974/", "the census reads the tag's own rectangle" );

		// A second tag, not on show, keeps its own rectangle beside the swelling one.
		tags.Said( 150, 0, 4000 );
		Frame( stack, false, new Vector2( 1000, 100 ), 4.05f );
		Assert.AreEqual( new UI.UiRect( 78, 903, 160, 980 ), Find( gadget, 900 ).Rect );
		Assert.AreEqual( new UI.UiRect( 84, 834, 154, 899 ), Find( gadget, 901 ).Rect, "only the tag on show swells" );
		Frame( stack, false, new Vector2( 1000, 100 ), 34.1f );
		Frame( stack, false, new Vector2( 1000, 100 ), 34.7f );

		Frame( stack, false, new Vector2( 1000, 100 ), 45.0f );
		Assert.AreEqual( (1, false, true), (tags.Standing.Count, tag.Hidden, gadget.BarOut), "an opened tag stands past its thirty seconds" );

		Time.PinWall( 46000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( (false, false, null, 1), (gadget.BarOut, gadget.ArmOut, tags.OnShow, tags.Standing.Count), "the tag on show clicked again folds the arm away" );
		StringAssert.Contains( lines.Census(), "pending=0 attempts=0 ", "and it is withdrawn as the bar leaves" );
		Assert.AreEqual( new UI.UiRect( 331, 1069, 361, 1495 ), Find( gadget, 0x21 ).Rect );
		Assert.AreEqual( new UI.UiRect( 331, 1058, 484, 1503 ), Find( gadget, 0x23 ).Rect );
		Assert.IsFalse( Find( gadget, 0x10d2d ).Visible );
		StringAssert.Contains( gadget.BarCensus(), "bar: out=0 " );
		Frame( stack, false, new Vector2( 1000, 100 ), 46.35f );
		Assert.AreEqual( new UI.UiRect( 84, 909, 154, 974 ), Find( gadget, 900 ).Rect, "and the tag is still" );

		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( (true, tag), (gadget.BarOut, tags.OnShow), "and a third click opens it again" );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=284/subject=0 " );
	}

	/// <summary>
	/// <b>The bar leaving the arm cuts the advisor off its line</b> (<c>FUN_0048e2f0</c>'s 0x1001d8,
	/// <c>FUN_00486b40</c>): folded away while he says message 0x11c, the message is cut off, on the game's clock,
	/// and may be posted again; a second tag opened over the first withdraws before it posts
	/// (<c>FUN_0048d9c0</c>: <c>FUN_004a2590</c>, then <c>FUN_00486b00</c>); the erase button withdraws too; after
	/// three cuts an opening posts nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no withdrawal at the fold, at the erase or at a second tag's opening; the post before the
	/// withdrawal; the wall clock for the game's.
	/// </remarks>
	[TestMethod]
	public void TheBarLeavingTheArmCutsTheAdvisorOffItsLine()
	{
		using var clock = new SimulationClockScope();
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		var park = gadget.Park = new ParkState( null ) { AdvisorMessages = lines };
		var said = 0;
		int Play( int response, int sample ) { ++said; return 9000; }

		for ( var i = 0; i < 7; ++i )
			park.AdvanceGameTick();

		Frame( stack, false, new Vector2( 1000, 100 ), 1.0f );
		tags.Said( 72, 13, 1000 );
		tags.Said( 150, 0, 1000 );
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );

		// The wall clock far past the line's end: the cut is judged on the game's clock, which stands at nought.
		Time.PinWall( 60000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		lines.Tick( park.GameTick, 0, Play );
		StringAssert.Contains( lines.Census(), $"message=284/score=1000/line=0/lastTick={park.GameTick}/said=1/slaps=0" );
		Assert.AreEqual( 7, park.GameTick );

		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.IsFalse( gadget.BarOut );
		StringAssert.Contains( lines.Census(), "/said=0/slaps=1", "folded away while he says it" );

		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=1 threshold=25 slot=284/subject=0 ", "and posted again at the next opening" );
		lines.Tick( park.GameTick, 0, Play );

		// The second tag opened over the first: withdrawn, a cut, then posted.
		Assert.IsTrue( stack.ClickAt( 119, 866 ) );
		stack.Update();
		Settle( gadget );
		Assert.AreEqual( (true, 150), (gadget.BarOut, tags.OnShow!.Row.Id) );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=2 threshold=25 slot=284/subject=0 " );
		StringAssert.Contains( lines.Census(), "/said=0/slaps=2" );
		lines.Tick( park.GameTick, 0, Play );
		Assert.AreEqual( 3, said );

		Assert.IsTrue( stack.ClickAt( 565, 1387 ), "the erase button" );
		Settle( gadget );
		Assert.IsFalse( gadget.BarOut );
		StringAssert.Contains( lines.Census(), "/said=0/slaps=3" );

		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.IsTrue( gadget.BarOut );
		StringAssert.Contains( lines.Census(), "pending=0 attempts=3 ", "cut off three times, it is posted no more" );
	}

	/// <summary>
	/// <b>The camcorder button tells of its panel and takes it back</b>: down posts message <c>0x131</c> on the park's
	/// tick after asking the arm for the panel, refused or not (<c>FUN_00498bb0</c>); up withdraws it after the fold
	/// (<c>FUN_00498bd0</c>), which cuts him off on the game's clock; either of the panel's buttons withdraws it before
	/// folding the arm (<c>FUN_00498ad0</c>, <c>0x100</c>). A tag taking the arm from the panel leaves it be.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the post missing, or only where the arm took the panel; no withdrawal at the button's up, at
	/// the camera button or at the postcard button; a withdrawal where a tag takes the arm; the wall clock for the
	/// game's; tick nought for the park's; the row's response or sample, its category, or two lines.
	/// </remarks>
	[TestMethod]
	public void TheCamcorderButtonTellsOfItsPanelAndTakesItBack()
	{
		using var clock = new SimulationClockScope();
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		var park = gadget.Park = new ParkState( null ) { AdvisorMessages = lines };
		var camcorder = (UI.UiButton)Find( gadget, 0x27 );
		var said = new System.Collections.Generic.List<(int Response, int Sample)>();
		int Play( int response, int sample ) { said.Add( (response, sample) ); return 9000; }
		var orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		var orbitYaw = ParkOrbitCameraMode.Yaw;

		// Past day 10, so the control panel's row scores 110 on the park's tick and nought on tick nought.
		for ( var i = 0; i < 300; ++i )
			park.AdvanceGameTick();
		var tick = park.GameTick;

		// The wall clock far past the line's end: a cut is judged on the game's clock, which stands at nought.
		Time.PinWall( 60000 );

		try
		{
			Assert.IsTrue( stack.ClickAt( 261, 1414 ), "the camcorder button down" );
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=305/subject=0 " );
			StringAssert.Contains( lines.Census(), "message=185/score=110/", "posted on the park's own tick" );
			lines.Tick( park.GameTick, 0, Play );
			CollectionAssert.AreEqual( new[] { (565, 583) }, said, "response 565, sample 583" );
			StringAssert.Contains( lines.Census(), $"message=305/score=1000/line=0/lastTick={tick}/said=1/slaps=0", "said on the park's tick" );

			Assert.IsTrue( stack.ClickAt( 261, 1414 ), "and up while he says it" );
			Assert.AreEqual( (false, UI.ParkGadget.ArmMove.In), (camcorder.IsDown, gadget.Moving) );
			StringAssert.Contains( lines.Census(), $"message=305/score=1000/line=0/lastTick={tick}/said=0/slaps=1", "cut off" );
			Settle( gadget );

			// Down again, out, and a tag takes the arm: the line stays posted, and its next say is the same one line.
			Assert.IsTrue( stack.ClickAt( 261, 1414 ) );
			Settle( gadget );
			Frame( stack, false, new Vector2( 1000, 100 ), 1.0f );
			tags.Said( 72, 13, 1000 );
			Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
			Time.PinWall( 60000 );
			Assert.IsTrue( stack.ClickAt( 119, 941 ), "the tag" );
			Settle( gadget );
			Assert.AreEqual( (true, true), (gadget.BarOut, camcorder.IsDown) );
			StringAssert.Contains( lines.Census(), "slot=305/subject=0 ", "a tag taking the arm leaves the camcorder's line be" );
			lines.Withdraw( ParkAdvisorMessages.TagMessage, 0, () => { } );
			lines.Tick( park.GameTick, 0, Play );
			Assert.AreEqual( (565, 583), said[1], "its one line again" );

			// The camcorder button up folds the bar away and withdraws the camcorder's line while he says it.
			Assert.IsTrue( stack.ClickAt( 261, 1414 ) );
			Settle( gadget );
			StringAssert.Contains( lines.Census(), $"message=305/score=1000/line=0/lastTick={tick}/said=0/slaps=2" );

			// The postcard button withdraws it before folding the arm: nothing posted is left.
			Assert.IsTrue( stack.ClickAt( 261, 1414 ) );
			Settle( gadget );
			Assert.IsTrue( stack.ClickAt( 720, 1280 ), "the postcard button, 0x64" );
			Assert.AreEqual( (false, UI.ParkGadget.ArmMove.In), (camcorder.IsDown, gadget.Moving), "the arm folds and lifts the button" );
			StringAssert.Contains( lines.Census(), "pending=0 " );
			Settle( gadget );

			// The camera button the same, and into first person.
			Assert.IsTrue( stack.ClickAt( 261, 1414 ) );
			Settle( gadget );
			StringAssert.Contains( lines.Census(), "pending=1 " );
			Assert.IsTrue( stack.ClickAt( 560, 1280 ), "the camera button, 0x63" );
			Assert.AreEqual( (true, false), (ParkCamcorderCameraMode.Active, camcorder.IsDown) );
			StringAssert.Contains( lines.Census(), "pending=0 " );
			ParkCamcorderCameraMode.Leave();
			Settle( gadget );

			// A panel waits for the arm: the camcorder's is refused, and posted all the same.
			Assert.IsTrue( stack.ClickAt( 261, 1414 ) );
			gadget.Slide( 70f );
			Assert.IsTrue( stack.ClickAt( 261, 1414 ) );
			Assert.IsTrue( gadget.Carry( gadget.Locator.StaffPanel ) );
			Assert.IsTrue( stack.ClickAt( 261, 1414 ), "down with the staff's panel waiting" );
			StringAssert.Contains( gadget.BarCensus(), "waiting=95c", "the camcorder's panel refused" );
			StringAssert.Contains( lines.Census(), "pending=1 attempts=2 threshold=25 slot=305/subject=0 ", "posted all the same" );
		}
		finally
		{
			Time.PinWall( null );
			ParkCamcorderCameraMode.Leave();
			ParkOrbitCameraMode.PointOfInterest = orbitLooksAt;
			ParkOrbitCameraMode.Yaw = orbitYaw;
		}
	}

	/// <summary>
	/// <b>The bar's ways off the arm</b> (<c>FUN_0048e2f0</c>, <c>FUN_0048dae0</c>): the erase button hides the tag
	/// on show at once, takes it off half a second on and folds the arm away; the tag's right click the same; the tag
	/// taken off by a later line; the retract button, which also lifts the camcorder button; and the camcorder
	/// button, which puts its own panel on the arm or folds it away, and stays down while a tag takes the arm.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> erase leaving the tag; taking it off at once; leaving the arm out; the right click leaving
	/// the arm out; a tag gone and its bar left; the camcorder's panel and the bar both shown; the camcorder button
	/// lifted by a tag's click, or left down by the retract button; a button of an unbuilt type uncounted, or
	/// folding the arm; a declining tag's erase button shown.
	/// </remarks>
	[TestMethod]
	public void TheBarLeavesTheArmWithItsTag()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags( [.. ParkTags.Rows, new ParkTags.Row( 20, ParkTags.NoTag, 1, 0, 0x13, ParkTags.DeclineButton, 30000, 0 ),
			new ParkTags.Row( 21, ParkTags.NoTag, 1, 0, 4, 0, 30000, 0 )] );
		var camcorder = (UI.UiButton)Find( gadget, 0x27 );

		// Erase.
		var tag = tags.Said( 72, 13, 1000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Time.PinWall( 2000 );
		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.IsTrue( gadget.BarOut );
		Assert.IsTrue( stack.ClickAt( 565, 1387 ), "mb_erase" );
		Settle( gadget );
		Assert.AreEqual( (true, 1, false, null), (tag.Hidden, tags.Standing.Count, gadget.BarOut, tags.OnShow), "hidden at once, the arm in" );
		Frame( stack, false, new Vector2( 1000, 100 ), 2.49f );
		Assert.AreEqual( 1, tags.Standing.Count );
		Frame( stack, false, new Vector2( 1000, 100 ), 2.51f );
		Assert.AreEqual( 0, tags.Standing.Count, "and off half a second on" );

		// The tag's own right click.
		tag = tags.Said( 72, 13, 3000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 3.5f );
		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.IsTrue( gadget.BarOut );
		Frame( stack, true, new Vector2( 119, 941 ), 4.00f );
		Frame( stack, false, new Vector2( 119, 941 ), 4.08f );
		Settle( gadget );
		Assert.AreEqual( (true, false, null), (tag.Hidden, gadget.BarOut, tags.OnShow), "the right click hides it and folds the arm away" );
		Frame( stack, false, new Vector2( 1000, 100 ), 4.7f );
		Assert.AreEqual( 0, tags.Standing.Count );

		// Taken off by a later line.
		var worn = tags.Said( 68, 13, 5000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 5.5f );
		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.AreEqual( (true, worn), (gadget.BarOut, tags.OnShow) );
		tags.Said( 72, 13, 6000 );
		Frame( stack, false, new Vector2( 1000, 100 ), 6.5f );
		Settle( gadget );
		Assert.AreEqual( (false, false), (gadget.BarOut, gadget.ArmOut), "the tag on show gone, the bar goes with it" );

		// The camcorder button and the retract button.
		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.IsTrue( gadget.BarOut );
		Assert.IsTrue( stack.ClickAt( 261, 1414 ), "the camcorder button" );
		Settle( gadget );
		Assert.AreEqual( (false, true, true, null), (gadget.BarOut, gadget.ArmOut, camcorder.IsDown, tags.OnShow), "its panel takes the arm" );
		Assert.AreEqual( (true, false), (Find( gadget, 0x62 ).Visible, Find( gadget, 0x10d2d ).Visible) );
		Assert.AreEqual( new UI.UiRect( 851, 1058, 1004, 1503 ), Find( gadget, 0x23 ).Rect, "520 out, as the original's" );

		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.AreEqual( (true, true), (gadget.BarOut, camcorder.IsDown), "a tag takes the arm and the camcorder button stays down" );
		Assert.AreEqual( (false, true), (Find( gadget, 0x62 ).Visible, Find( gadget, 0x10d2d ).Visible) );
		Assert.AreEqual( new UI.UiRect( 1151, 1058, 1304, 1503 ), Find( gadget, 0x23 ).Rect );
		Assert.IsTrue( stack.ClickAt( 261, 1414 ), "the camcorder button, up" );
		Settle( gadget );
		Assert.AreEqual( (false, false, false, null), (gadget.BarOut, gadget.ArmOut, camcorder.IsDown, tags.OnShow) );

		// The bar's own ways in leave the camcorder button as it is: erase, with the button down.
		stack.ClickAt( 261, 1414 );
		Settle( gadget );
		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.AreEqual( (true, true), (gadget.BarOut, camcorder.IsDown) );
		Assert.IsTrue( stack.ClickAt( 565, 1387 ), "mb_erase" );
		Settle( gadget );
		Assert.AreEqual( (false, false, true), (gadget.BarOut, gadget.ArmOut, camcorder.IsDown), "erase folds the arm and leaves the camcorder button down" );
		Assert.IsTrue( stack.ClickAt( 261, 1414 ), "up again, with nothing out" );
		Settle( gadget );
		Assert.AreEqual( (false, false), (gadget.ArmOut, camcorder.IsDown) );
		Frame( stack, false, new Vector2( 1000, 100 ), 7.0f );
		Assert.AreEqual( 0, tags.Standing.Count );
		tags.Said( 72, 13, 7100 );
		Frame( stack, false, new Vector2( 1000, 100 ), 7.6f );

		stack.ClickAt( 261, 1414 );
		Settle( gadget );
		stack.ClickAt( 119, 941 );
		Settle( gadget );
		Assert.AreEqual( (true, true), (gadget.BarOut, camcorder.IsDown) );
		Assert.IsTrue( stack.ClickAt( 389, 1426 ), "the retract button" );
		Settle( gadget );
		Assert.AreEqual( (false, false, false, null), (gadget.BarOut, gadget.ArmOut, camcorder.IsDown, tags.OnShow), "which lifts the camcorder button too" );
		Assert.AreEqual( 1, tags.Standing.Count, "and leaves the tag" );

		// An offer of research, and to decline, which is not built.
		var buttons = Times( "TAG_BAR_BUTTON" );
		var offer = tags.Said( 20, 0, 7700 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 8.2f );
		Assert.IsTrue( stack.ClickAt( 119, 866 ), "the second tag up" );
		Settle( gadget );
		Assert.AreEqual( offer, tags.OnShow );
		Assert.AreEqual( (true, 0x213, true, -1, true),
			(Find( gadget, 0x10d30 ).Visible, Find( gadget, 0x10d30 ).HelpText, Find( gadget, 0x10d2f ).Visible, Find( gadget, 0x10d2f ).HelpText, Find( gadget, 0x10d2e ).Visible),
			"mb_resrch, mb_decline, and the erase button shown again by the arm's slide" );
		Assert.AreEqual( (null, "mb_erase", "mb_erase"), (UI.ParkGadget.EraseLook( ParkTags.DeclineButton ), UI.ParkGadget.EraseLook( 0 ), UI.ParkGadget.EraseLook( 0x1a )),
			"no mesh beside a decline, mb_erase beside anything else" );
		Assert.IsTrue( stack.ClickAt( 565, 1280 ) );
		Settle( gadget );
		Assert.AreEqual( (buttons + 1, true), (Times( "TAG_BAR_BUTTON" ), gadget.BarOut), "the decline counted, and the bar left out" );
		Assert.AreEqual( 0x10d2e, Under( gadget, 565, 1387 ), "the erase button with no mesh takes the pointer, as the original's shown again" );

		// mb_locate of the other type, which goes by the item (FUN_004868d0, not read): counted, and the camera left.
		var looked = ParkOrbitCameraMode.PointOfInterest;
		var byItem = tags.Said( 21, 13, 8300 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 8.8f );
		Assert.IsTrue( stack.ClickAt( 119, 791 ), "the third tag up" );
		Settle( gadget );
		Assert.AreEqual( (byItem, 515), (tags.OnShow, Find( gadget, 0x10d30 ).HelpText) );
		Assert.IsTrue( stack.ClickAt( 565, 1172 ) );
		Settle( gadget );
		Assert.AreEqual( (buttons + 2, looked), (Times( "TAG_BAR_BUTTON" ), ParkOrbitCameraMode.PointOfInterest) );

		// mb_resrch runs the research opener, which in Instant Action is its box, and answers 2: the tag off, the arm in.
		Assert.IsTrue( Level.InstantAction, "no player: Instant Action" );
		Assert.IsTrue( stack.ClickAt( 119, 866 ), "the offer again" );
		Settle( gadget );
		Assert.AreEqual( offer, tags.OnShow );
		Assert.IsTrue( stack.ClickAt( 565, 1172 ) );
		Settle( gadget );
		Assert.AreEqual( (buttons + 2, true, false, null), (Times( "TAG_BAR_BUTTON" ), offer.Hidden, gadget.ArmOut, tags.OnShow),
			"nothing counted, the tag hidden at once, the arm in" );
		Assert.AreEqual( ParkResearchOpenerTests.InstantActionText, ParkResearchOpenerTests.TextOf( (UI.MessageBox)stack.Windows[^1] ) );
	}

	/// <summary>
	/// <b>The bar's buy button</b> (<c>FUN_0048e0f0</c>, types 9 to 0xc, which answer 2): the no-rides tag shows
	/// part 2 of the tag's picture and dresses the bar's first button as <c>mb_buy</c>; its click opens the buy
	/// screen on the rides tab whatever tab it was last left on, hides the tag at once and folds the arm away, and
	/// is not counted as unbuilt. With the buy screen open on another tab the click picks the type's tab on that
	/// screen and opens no second one.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button counted as unbuilt; the tag left; the arm left out; the screen not opened, opened on the tab last left, or opened twice; the open screen's tab left, or a new screen put in its place; the tab the type itself; the item's row counted for no item, or never.</remarks>
	[TestMethod]
	public void TheBuyButtonOpensTheBuyScreenOnItsTabAndTakesItsTagOff()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags( [.. ParkTags.Rows, new ParkTags.Row( 21, ParkTags.NoTag, 1, 0, 0xb, 0, 30000, 0 )] );
		var buttons = Times( "TAG_BAR_BUTTON" );
		var rows = Times( "TAG_BAR_BUY_ITEM_ROW" );

		// The screen last left on its shops tab.
		var earlier = new UI.ParkBuyScreen( stack, 1 );
		stack.Open( earlier );
		Assert.AreEqual( 1, earlier.Tab );
		stack.Close( earlier );

		var tag = tags.Said( 64, 0, 1000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Assert.AreEqual( (2, true), (Find( gadget, 900 ).Frame, Find( gadget, 900 ).Visible) );
		Time.PinWall( 2000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( (true, tag), (gadget.BarOut, tags.OnShow) );
		Assert.AreEqual( (true, 0x209, true, true),
			(Find( gadget, 0x10d30 ).Visible, Find( gadget, 0x10d30 ).HelpText, Find( gadget, 0x10d2f ).Visible, Find( gadget, 0x10d2e ).Visible),
			"mb_buy, the second button shown with no mesh, as the arm shows every part past the body, and the erase button" );

		Assert.IsTrue( stack.ClickAt( 565, 1172 ), "mb_buy" );
		Settle( gadget );
		Assert.AreEqual( (true, false, false, null, buttons),
			(tag.Hidden, gadget.BarOut, gadget.ArmOut, tags.OnShow, Times( "TAG_BAR_BUTTON" )), "hidden at once, the arm in, nothing counted" );
		var screens = stack.Windows.OfType<UI.ParkBuyScreen>().ToArray();
		Assert.AreEqual( (1, 0, rows), (screens.Length, screens[0].Tab, Times( "TAG_BAR_BUY_ITEM_ROW" )), "the buy screen, on its rides tab; a tag of no item asks for no row" );
		var opened = screens[0];

		// The screen open: a tag of type 0xb picks the sideshows tab on it.
		Frame( stack, false, new Vector2( 1000, 100 ), 2.6f );
		Assert.AreEqual( 0, tags.Standing.Count );
		var second = tags.Said( 21, 0, 3000, item: 1303 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 3.5f );
		Time.PinWall( 3600 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( second, tags.OnShow );
		Assert.IsTrue( stack.ClickAt( 565, 1172 ) );
		Settle( gadget );
		screens = stack.Windows.OfType<UI.ParkBuyScreen>().ToArray();
		Assert.AreEqual( (1, 2, true, buttons), (screens.Length, screens[0].Tab, second.Hidden, Times( "TAG_BAR_BUTTON" )) );
		Assert.AreSame( opened, screens[0], "the screen that was open, not a new one in its place" );
		Assert.AreEqual( rows + 1, Times( "TAG_BAR_BUY_ITEM_ROW" ), "the item's row is not selected: counted" );
	}

	/// <summary>
	/// <b>The bar's hire button</b> (<c>FUN_0048e0f0</c>, types 0xd to 0x11, which answer 2): the worn-rides tag
	/// shows part 2 of the tag's picture and dresses the bar's first button as <c>mb_buy</c> with the help row of
	/// its type; its click opens the hire screen on the mechanics' tab whatever tab it was last left on, hides the
	/// tag at once and folds the arm away, and is not counted as unbuilt. With the hire screen open on that tab or
	/// another the click builds the screen again on the type's tab (<c>FUN_0049bdd0</c> destroys the open park
	/// screen first), and each of the five types names its own tab.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button counted as unbuilt; the tag left; the arm left out; the screen not opened, or opened on the tab last left; the tab the type itself, or one off; the open screen kept; a type past 0x11 a hire button, hidden or not; row 7's button another's.</remarks>
	[TestMethod]
	public void TheHireButtonOpensTheHireScreenOnItsTabAndTakesItsTagOff()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags( [.. ParkTags.Rows, .. Enumerable.Range( 0xd, 6 ).Select( type => new ParkTags.Row( 200 + type, ParkTags.NoTag, 1, 0, type, 0, 30000, 0 ) )] );
		var buttons = Times( "TAG_BAR_BUTTON" );

		// The screen last left on its guards' tab.
		var earlier = new UI.ParkHireScreen( stack, openOn: 3 );
		stack.Open( earlier );
		Assert.AreEqual( 3, earlier.Tab );
		stack.Close( earlier );

		var tag = tags.Said( 7, 0, 1000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Assert.AreEqual( (2, true), (Find( gadget, 900 ).Frame, Find( gadget, 900 ).Visible) );
		Time.PinWall( 2000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( (true, tag), (gadget.BarOut, tags.OnShow) );
		Assert.AreEqual( ("mb_buy", 0x20e), UI.ParkGadget.BarButtonLook( 0xe ) );
		CollectionAssert.AreEqual( new int?[] { null, 0, 1, 2, 3, 4, null, null },
			Enumerable.Range( 0xc, 8 ).Select( UI.ParkGadget.HireTabOf ).ToArray(), "types 0xc to 0x13: the five hire buttons and no other" );
		Assert.AreEqual( (true, 0x20e, true, true),
			(Find( gadget, 0x10d30 ).Visible, Find( gadget, 0x10d30 ).HelpText, Find( gadget, 0x10d2f ).Visible, Find( gadget, 0x10d2e ).Visible),
			"mb_buy with type 0xe's help row, the second button shown with no mesh, as the arm shows every part past the body, and the erase button" );

		Assert.IsTrue( stack.ClickAt( 565, 1172 ), "mb_buy" );
		Settle( gadget );
		Assert.AreEqual( (true, false, false, null, buttons),
			(tag.Hidden, gadget.BarOut, gadget.ArmOut, tags.OnShow, Times( "TAG_BAR_BUTTON" )), "hidden at once, the arm in, nothing counted" );
		var screens = stack.Windows.OfType<UI.ParkHireScreen>().ToArray();
		Assert.AreEqual( (1, 1), (screens.Length, screens[0].Tab), "the hire screen, on its mechanics' tab, not the guards'" );
		var opened = screens[0];

		// Each type's own tab, on a screen built again; the type after the last is not a hire button.
		var now = 2.6f;
		for ( var type = 0xd; type <= 0x12; ++type )
		{
			Frame( stack, false, new Vector2( 1000, 100 ), now );
			Assert.AreEqual( 0, tags.Standing.Count, $"type 0x{type:x}: the mast bare" );
			var next = tags.Said( 200 + type, 0, (long)(now * 1000) + 100 )!;
			Frame( stack, false, new Vector2( 1000, 100 ), now + 0.6f );
			Time.PinWall( (long)(now * 1000) + 700 );
			Assert.IsTrue( stack.ClickAt( 119, 941 ) );
			Settle( gadget );
			Assert.AreEqual( next, tags.OnShow );

			if ( type == 0x12 )
			{
				Assert.IsNull( UI.ParkGadget.BarButtonLook( 0x12 ), "type 0x12 is not dressed" );
				break;
			}

			Assert.IsTrue( stack.ClickAt( 565, 1172 ) );
			Settle( gadget );
			screens = stack.Windows.OfType<UI.ParkHireScreen>().ToArray();
			Assert.AreEqual( (1, type - 0xd, true, buttons), (screens.Length, screens[0].Tab, next.Hidden, Times( "TAG_BAR_BUTTON" )), $"type 0x{type:x}" );
			Assert.AreNotSame( opened, screens[0], "built again, as the opener destroys the open screen first" );
			opened = screens[0];
			now += 1.5f;
		}

		// The tab last left on is the session's: put back for the tests after this one.
		stack.Open( new UI.ParkHireScreen( stack, openOn: 0 ) );
	}

	/// <summary>
	/// <b>The bar's entry-price button</b> (<c>FUN_0048e0f0</c>, type 0x18, which answers 2): the shut park's tag
	/// shows part 2 of the tag's picture and dresses the bar's first button as <c>b_entry</c>; its click hides the
	/// tag at once, takes it off the mast half a second on and folds the arm away, and is not counted as unbuilt.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button counted as unbuilt; the tag left; the tag off at once; the arm left out; the button not dressed; the screen not opened.</remarks>
	[TestMethod]
	public void TheEntryPriceButtonTakesItsTagOffAndFoldsTheArm()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();
		var buttons = Times( "TAG_BAR_BUTTON" );

		var tag = tags.Said( 3, 0, 1000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Assert.AreEqual( (2, true), (Find( gadget, 900 ).Frame, Find( gadget, 900 ).Visible) );
		Time.PinWall( 2000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( (true, tag), (gadget.BarOut, tags.OnShow) );
		Assert.AreEqual( (true, 0xec, true, true),
			(Find( gadget, 0x10d30 ).Visible, Find( gadget, 0x10d30 ).HelpText, Find( gadget, 0x10d2f ).Visible, Find( gadget, 0x10d2e ).Visible),
			"b_entry, the second button shown with no mesh, as the arm shows every part past the body, and the erase button" );

		Assert.IsTrue( stack.ClickAt( 565, 1172 ), "b_entry" );
		Settle( gadget );
		Assert.AreEqual( (true, 1, false, false, null, buttons),
			(tag.Hidden, tags.Standing.Count, gadget.BarOut, gadget.ArmOut, tags.OnShow, Times( "TAG_BAR_BUTTON" )), "hidden at once, the arm in, nothing counted" );
		Assert.AreEqual( 1, stack.Windows.Count( window => window is UI.ParkEntryPriceScreen ), "the entry-price screen opened" );
		Frame( stack, false, new Vector2( 1000, 100 ), 2.49f );
		Assert.AreEqual( 1, tags.Standing.Count );
		Frame( stack, false, new Vector2( 1000, 100 ), 2.51f );
		Assert.AreEqual( 0, tags.Standing.Count, "and off half a second on" );

		// With the screen open already the button builds no second one, and takes its tag off all the same.
		var screen = stack.Windows.Single( window => window is UI.ParkEntryPriceScreen );
		var again = tags.Said( 3, 0, 3000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 3.5f );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( again, tags.OnShow );
		Assert.IsTrue( stack.ClickAt( 565, 1172 ) );
		Settle( gadget );
		Assert.AreSame( screen, stack.Windows.Single( window => window is UI.ParkEntryPriceScreen ), "the screen that was open" );
		Assert.AreEqual( (true, false), (again.Hidden, gadget.BarOut) );
	}

	/// <summary>
	/// <b>The bar's staff-costs button</b> (<c>FUN_0048e0f0</c>, type 0x15, which answers 2): the mechanics' training
	/// tag dresses the bar's first button as <c>b_staffcost</c>; its click opens the Staff Training Budgets screen,
	/// leaves the Money category on it, hides the tag at once and folds the arm away, and is not counted as unbuilt.
	/// With the screen open already it builds no second one and takes its tag off all the same.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button counted as unbuilt; the tag left; the arm left out; the entry-price screen opened; a second screen built; the category not left on 9.</remarks>
	[TestMethod]
	public void TheStaffCostsButtonOpensTheStaffTrainingBudgetsAndTakesItsTagOff()
	{
		var (stack, gadget) = AGadget();
		var tags = gadget.Tags = new ParkTags();
		var buttons = Times( "TAG_BAR_BUTTON" );

		UI.ParkCategoryScreens.Seed();

		var tag = tags.Said( 30, 0, 1000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
		Time.PinWall( 2000 );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( (true, tag), (gadget.BarOut, tags.OnShow) );
		Assert.AreEqual( (true, 0x215, true, true),
			(Find( gadget, 0x10d30 ).Visible, Find( gadget, 0x10d30 ).HelpText, Find( gadget, 0x10d2f ).Visible, Find( gadget, 0x10d2e ).Visible),
			"b_staffcost, the second button shown with no mesh, as the arm shows every part past the body, and the erase button" );

		Assert.IsTrue( stack.ClickAt( 565, 1172 ), "b_staffcost" );
		Settle( gadget );
		Assert.AreEqual( (true, false, false, null, buttons),
			(tag.Hidden, gadget.BarOut, gadget.ArmOut, tags.OnShow, Times( "TAG_BAR_BUTTON" )), "hidden at once, the arm in, nothing counted" );
		Assert.AreEqual( "ParkGadget, ParkStaffCostsScreen", string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) ) );
		Assert.AreEqual( 9, UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Finance ) );

		var screen = stack.Windows.Single( window => window is UI.ParkStaffCostsScreen );
		var again = tags.Said( 31, 0, 3000 )!;
		Frame( stack, false, new Vector2( 1000, 100 ), 3.5f );
		Assert.IsTrue( stack.ClickAt( 119, 941 ) );
		Settle( gadget );
		Assert.AreEqual( again, tags.OnShow );
		Assert.IsTrue( stack.ClickAt( 565, 1172 ) );
		Settle( gadget );
		Assert.AreSame( screen, stack.Windows.Single( window => window is UI.ParkStaffCostsScreen ), "the screen that was open" );
		Assert.AreEqual( (true, false), (again.Hidden, gadget.BarOut) );

		UI.ParkCategoryScreens.Seed();
	}

	/// <summary>
	/// <b>A tag's two effects</b> (<c>FUN_0048dae0</c>): its first show, half a second after it is put on, starts
	/// MessageTag1 (0x5a) over its middle, once, and none while the park is paused or the gadget put away; timer 0x80081,
	/// five seconds before its end, starts EndOfMessage (0x5b) there and keeps it, hidden while the tag is, moved with the
	/// tag as one below goes (message 0x13), and killed as it is opened with its timers stopped or taken off; a tag that
	/// declines an offer keeps it when opened.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no 0x5a; shown at once; started while paused; started while put away; across over 2048 rounded
	/// up; down over 1536 of the top edge; no 0x5b; started at the put-on; not kept; not hidden with the tag; not moved;
	/// moved to the old place; not killed as the tag goes; not killed as it is opened; killed though the tag declines;
	/// drawn over the whole interface, not with the gadget; not pinned down by the tag; a hidden tag shown.
	/// </remarks>
	[TestMethod]
	public void ATagsEffectsFollowItsShowingAndItsEnd()
	{
		var (stack, gadget) = AGadget();
		// An offer that declines, as the research offer's row (FUN_0048d9c0's 0x1b).
		var tags = gadget.Tags = new ParkTags( ParkTags.Rows.Append( new ParkTags.Row( 20, ParkTags.NoTag, 1, 0, 0x1a, ParkTags.DeclineButton, 30000, 0 ) ) );
		var system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		try
		{
			Emitter[] Live( int effect ) => system.Emitters.Where( emitter => emitter.Active && emitter.Effect == effect && emitter.Generation != 0 ).ToArray();

			Frame( stack, false, new Vector2( 1000, 100 ), 1.0f );
			var low = tags.Said( 68, 13, 1000 )!;
			Frame( stack, false, new Vector2( 1000, 100 ), 1.499f );
			Assert.AreEqual( 0, Live( ParkTags.FirstShowEffect ).Length, "none before the tag is shown" );
			Frame( stack, false, new Vector2( 1000, 100 ), 1.5f );
			var first = Live( ParkTags.FirstShowEffect );
			Assert.AreEqual( 1, first.Length, "MessageTag1 as it is shown" );
			// The tag (84,909)-(154,974): its middle 119 across, 941 down; 119 * 100000 / 2048 = 5810, 941 * 75000 / 1536 = 45947.
			Assert.AreEqual( (5810 >> 4, 45947 >> 4, UI.Anchor.Left, UI.VerticalAnchor.Bottom, gadget),
				(first[0].X, first[0].Z, first[0].Anchor, first[0].Down, first[0].Owner) );
			Assert.AreEqual( (5810, 45947), UI.ParkGadget.EffectMiddle( new UI.UiRect( 84, 909, 154, 974 ) ), "each toward nought" );
			Assert.AreEqual( (5810, 42285), UI.ParkGadget.EffectMiddle( new UI.UiRect( 84, 834, 154, 899 ) ) );
			Assert.IsTrue( system.Library.Effects[ParkTags.FirstShowEffect].OnScreen && system.Library.Effects[ParkTags.EndingEffect].OnScreen, "both drawn on the screen" );
			Assert.AreEqual( ParkTags.EndingEffect, (int)ParLib.P_EFFECT_EndOfMessage );
			Assert.AreEqual( ParkTags.FirstShowEffect, (int)ParLib.P_EFFECT_MessageTag1 );

			// Paused at its show: none, and none later.
			var high = tags.Said( 72, 14, 2000 )!;
			GameClock.Update( true, GameClock.ParkCatchUp );
			Frame( stack, false, new Vector2( 1000, 100 ), 2.5f );
			GameClock.Rebase();
			Frame( stack, false, new Vector2( 1000, 100 ), 2.6f );
			Assert.AreEqual( (1, true), (Live( ParkTags.FirstShowEffect ).Length, high.ShownAt( 2600 )), "paused as it was shown: no MessageTag1" );

			// Put away at its show: none either.
			gadget.PutAway = true;
			var away = tags.Said( 150, 0, 3000 )!;
			Frame( stack, false, new Vector2( 1000, 100 ), 3.5f );
			gadget.PutAway = false;
			Frame( stack, false, new Vector2( 1000, 100 ), 3.6f );
			Assert.AreEqual( (1, true), (Live( ParkTags.FirstShowEffect ).Length, away.FirstShown), "put away as it was shown: none" );

			// Timer 0x80081: EndOfMessage over the low tag's middle, kept.
			Frame( stack, false, new Vector2( 1000, 100 ), 25.999f );
			Assert.AreEqual( 0, Live( ParkTags.EndingEffect ).Length );
			Frame( stack, false, new Vector2( 1000, 100 ), 26.0f );
			var ending = Live( ParkTags.EndingEffect );
			Assert.AreEqual( 1, ending.Length, "EndOfMessage five seconds before its end" );
			Assert.AreEqual( (5810 >> 4, 45947 >> 4, false, gadget), (ending[0].X, ending[0].Z, ending[0].Hidden, ending[0].Owner) );
			Assert.IsTrue( system.IsLive( low.Ending ) );

			// The high tag's, at its own middle, (84,834)-(154,899): 866 down, 866 * 75000 / 1536 = 42285.
			Frame( stack, false, new Vector2( 1000, 100 ), 27.0f );
			Assert.IsTrue( system.TryEmitter( high.Ending, out var highEnding ) );
			Assert.AreEqual( (5810 >> 4, 42285 >> 4), (highEnding.X, highEnding.Z) );

			// Hidden with the tag, killed as it is taken off; the high tag moves down and its effect with it.
			tags.Hide( low, 27500 );
			Frame( stack, false, new Vector2( 1000, 100 ), 27.5f );
			Assert.IsTrue( ending[0].Hidden, "hidden with the tag" );
			Assert.AreNotEqual( -2, ending[0].Lifetime );
			Frame( stack, false, new Vector2( 1000, 100 ), 28.0f );
			Assert.AreEqual( -2, ending[0].Lifetime, "killed as the tag is taken off" );
			Assert.AreEqual( (5810 >> 4, 45947 >> 4), (highEnding.X, highEnding.Z), "moved down with its tag" );

			// Opened, its timers stopped: killed.
			Assert.IsTrue( stack.ClickAt( 119, 941 ) );
			Settle( gadget );
			Frame( stack, false, new Vector2( 1000, 100 ), 28.1f );
			Assert.AreEqual( (high, -2), (tags.OnShow, highEnding.Lifetime), "opened: killed with its timers" );

			// A tag that declines an offer keeps its timers and its effect as it is opened.
			var offer = tags.Said( 20, 0, 29000 )!;
			Frame( stack, false, new Vector2( 1000, 100 ), 29.0f + (offer.Row.Life - ParkTags.WarningBeforeTheEnd) / 1000f );
			Assert.IsTrue( system.TryEmitter( offer.Ending, out var offerEnding ) );
			Assert.AreEqual( ParkTags.DeclineButton, offer.Row.Button2 );
			Assert.IsTrue( tags.Open( offer, Time.WallMilliseconds ) );
			Frame( stack, false, new Vector2( 1000, 100 ), 29.1f + (offer.Row.Life - ParkTags.WarningBeforeTheEnd) / 1000f );
			Assert.AreNotEqual( -2, offerEnding.Lifetime, "a declining tag's effect kept" );
		}
		finally
		{
			system.Shutdown();
			GameClock.Rebase();
		}
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
		Time.PinWall( (long)System.MathF.Round( now * 1000f ) );
		Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = at };
		stack.Update();
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>The arm's slides run to their ends: one in and one out again, each 220 ms, with time to spare.</summary>
	internal static void Settle( UI.ParkGadget gadget )
	{
		for ( var i = 0; i < 3; ++i )
			gadget.Slide( 240f );
	}
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
