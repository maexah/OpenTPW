using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// In first person a right click leaves it, with the Options switch "RMB cancel" on: the viewfinder's layer answers the
/// UI library's click message (<c>0x10006</c>) for the right button as Escape's release does (<c>0x00488aa1</c>). A click
/// is one press let go of within 500 ms, never having strayed more than 6 units, that was not a double click's second
/// press. See <see cref="UI.ParkViewfinder.RightClicked"/>, the stack's reading of the click, and
/// <c>docs/exe/hud.md</c>, "Four ways out of camcorder mode".
///
/// <para>
/// <b>The frame is the game's own</b>: the right button and the pointer as <see cref="Input.Mouse"/> holds them, then the
/// stack's whole update, over the real park front end, its gadget and its viewfinder. The screen is the interface's own
/// 2048x1536, so a point in the window is the same point on the layouts. No park is loaded, so leaving moves no camera.
/// </para>
/// </summary>
[TestClass]
public class ParkFirstPersonRightClickTests
{
	private Point2 _screen;
	private Vector3 _orbitLooksAt;
	private float _orbitYaw;
	private bool _rmbCancel;
	private float _now;

	/// <summary>On the view, in the middle of the viewfinder's frame.</summary>
	private static readonly Vector2 View = new( 1024, 700 );

	/// <summary>On the eject button, (1916,1404)-(2018,1506).</summary>
	private static readonly Vector2 Eject = new( 1960, 1450 );

	[TestInitialize]
	public void StartInFirstPerson()
	{
		Log ??= new();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		// Leaving first person hands the orbit camera the viewer's place and heading, so both are put back after.
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		_orbitYaw = ParkOrbitCameraMode.Yaw;
		_rmbCancel = GameOptions.Current.RmbCancel;
		_now = Time.Now;

		FileSystem = GameData.Required();
		GameOptions.Current.RmbCancel = true;
		Input.Mouse = new();

		// The park's keys run in the same update, and a release left from another test would be an Escape.
		Input.ForgetHeldKeys();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		InFirstPerson( false );
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		GameOptions.Current.RmbCancel = _rmbCancel;
		Time.Now = _now;
		Input.Mouse = new();
		Input.ForgetHeldKeys();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>A quick right click on the view leaves first person, on its release</b>: the press alone does nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the park front end not handing the stack its answer (the bug put back); the click taken on the
	/// press.
	/// </remarks>
	[TestMethod]
	public void AQuickRightClickLeavesOnItsRelease()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, View, 10.00f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "pressed: still in first person" );

		Frame( stack, false, View, 10.08f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "let go 80 ms on: out of it" );
	}

	/// <summary>
	/// <b>Anywhere on the view but the eject button</b>: the frame is disabled in the original and takes no pointer, so a
	/// press on it is the layer's, as one in the margins is; the eject button takes a right press and drops its click.
	/// </summary>
	/// <remarks><b>Mutations:</b> a click whose press landed on a control handed to the view leaves from the eject button.</remarks>
	[TestMethod]
	public void EverywhereButTheEjectButtonLeaves()
	{
		var stack = APark();
		var now = 10f;

		foreach ( var (at, where) in new (Vector2, string)[]
		{
			(View, "the middle of the frame"), (new( 180, 180 ), "the frame's top-left corner"), (new( 80, 80 ), "the top-left margin"),
			(new( 1990, 60 ), "the top-right margin"), (new( 100, 1480 ), "the bottom-left margin, where the gadget sits in orbit"),
			(new( 1900, 1450 ), "just left of the eject button"), (new( 1960, 1390 ), "just above it"),
		} )
		{
			InFirstPerson( true );
			QuickRightClick( stack, at, now += 1f );
			Assert.IsFalse( ParkCamcorderCameraMode.Active, $"a quick right click on {where} leaves" );
		}

		InFirstPerson( true );
		QuickRightClick( stack, Eject, now += 1f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "one on the eject button does not" );
	}

	/// <summary><b>With RMB cancel off no right click leaves</b>: the layer's handler reads the switch first (<c>0x00488aa8</c>).</summary>
	/// <remarks><b>Mutations:</b> the switch not read.</remarks>
	[TestMethod]
	public void WithRmbCancelOffARightClickStays()
	{
		var stack = APark();
		GameOptions.Current.RmbCancel = false;
		InFirstPerson( true );

		QuickRightClick( stack, View, 10f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "still in first person" );
	}

	/// <summary>
	/// <b>Held 500 ms it is no click, and 490 ms it is</b>: the base proc makes a click only when the release comes less
	/// than 500 ms after the press (<c>0x0065f969</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the limit taken as at most 500 ms; no limit.</remarks>
	[TestMethod]
	public void HeldHalfASecondIsNoClick()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, View, 10.0f );
		Frame( stack, true, View, 10.25f );
		Frame( stack, false, View, 10.5f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "held 500 ms: still in first person" );

		Frame( stack, true, View, 11.2f );
		Frame( stack, false, View, 11.69f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "held 490 ms: out of it" );
	}

	/// <summary>
	/// <b>A stray of 7 across or down is a drag, and 6 either way is not</b>: a move more than 6 units from where the press
	/// went down, on either axis, spoils the click for good (<c>0x0065fab7</c>, <c>0x0065fadb</c>), and coming back does
	/// not mend it.
	/// </summary>
	/// <remarks><b>Mutations:</b> a stray of 6 taken as a drag; no stray judged; only the stray across judged.</remarks>
	[TestMethod]
	public void AStrayOfSevenIsADragAndSixIsNot()
	{
		var stack = APark();
		InFirstPerson( true );

		DraggedRightClick( stack, new Vector2( 7, 0 ), 10f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "7 across and back: a drag" );

		DraggedRightClick( stack, new Vector2( 0, -7 ), 11f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "7 up and back: a drag" );

		DraggedRightClick( stack, new Vector2( 6, -6 ), 12f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "6 across and 6 up: a click" );
	}

	/// <summary>
	/// <b>A stray is judged only over the view</b>: the layer takes no capture, so a move over the eject button goes to the
	/// button and not to the layer (<c>0x0065fa33</c>). A press beside the button, taken over it and back, still clicks.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stray judged wherever the pointer is.</remarks>
	[TestMethod]
	public void AStrayOverTheEjectButtonIsNotJudged()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, new Vector2( 1900, 1450 ), 10.00f );
		Frame( stack, true, Eject, 10.05f );
		Frame( stack, true, new Vector2( 1902, 1450 ), 10.10f );
		Frame( stack, false, new Vector2( 1902, 1450 ), 10.15f );

		Assert.IsFalse( ParkCamcorderCameraMode.Active, "out of first person" );
	}

	/// <summary>
	/// <b>A press within 500 ms of the last release is a double click's second, and makes no click</b>: the button's one
	/// stamp is the release's time once a press that could still click is let go of, held long or not (<c>0x0065f9af</c>),
	/// and a press that finds it fresh is <c>0x10007</c> (<c>0x0065f8c7</c>). That second press's release clears the stamp
	/// (<c>0x0065f9bd</c>), so a third press clicks again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no double click; the stamp left at the press's time by its release; the stamp kept after a
	/// double click's release.
	/// </remarks>
	[TestMethod]
	public void APressHardOnTheLastReleaseMakesNoClick()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, View, 10.0f );
		Frame( stack, false, View, 10.7f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "held 700 ms: no click" );

		QuickRightClick( stack, View, 11.0f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "a quick one 300 ms after that release: a double click's second, no click" );

		QuickRightClick( stack, View, 11.3f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "and the one 220 ms after that: a click" );
	}

	/// <summary>
	/// <b>A right double click leaves on its first click</b>; its second press is a double click's second and opens nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bug put back.</remarks>
	[TestMethod]
	public void ARightDoubleClickLeavesOnItsFirstClick()
	{
		var stack = APark();
		InFirstPerson( true );

		QuickRightClick( stack, View, 10.0f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "out after the first click" );

		QuickRightClick( stack, View, 10.2f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "and still out after the second" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) ), "with nothing opened" );
	}

	/// <summary>
	/// <b>What answers a click is settled at the press</b>: the release goes to what took the press (<c>0x00658b5b</c>),
	/// so a right press made in orbit, on the park's own layer, and let go of once C has put first person up, leaves
	/// nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the answer asked of the scene at the release.</remarks>
	[TestMethod]
	public void APressMadeInOrbitDoesNotLeaveOnItsRelease()
	{
		var stack = APark();

		Frame( stack, true, View, 10.00f );
		InFirstPerson( true );
		Frame( stack, true, View, 10.05f );
		Frame( stack, false, View, 10.10f );

		Assert.IsTrue( ParkCamcorderCameraMode.Active, "still in first person" );
	}

	/// <summary>
	/// <b>Only a press the window system sent can click</b>: a button already down when the stack first sees it, as after
	/// F2 has hidden the interface, is not one.
	/// </summary>
	/// <remarks><b>Mutations:</b> a button seen down taken as a press.</remarks>
	[TestMethod]
	public void AButtonAlreadyDownIsNoPress()
	{
		var stack = APark();
		InFirstPerson( true );

		Time.Now = 10f;
		Input.Mouse = new() { Right = true, Position = View };
		stack.Update();
		Frame( stack, false, View, 10.05f );

		Assert.IsTrue( ParkCamcorderCameraMode.Active, "still in first person" );
	}

	/// <summary>
	/// <b>The stamp is the button's, not a control's</b>: a quick right click on the eject button, which drops it, still
	/// makes a press on the view 220 ms later a double click's second; one a second after that clicks.
	/// </summary>
	/// <remarks><b>Mutations:</b> a click on a control not stamping the time.</remarks>
	[TestMethod]
	public void AClickOnTheEjectButtonStampsTheButton()
	{
		var stack = APark();
		InFirstPerson( true );

		QuickRightClick( stack, Eject, 10.0f );
		QuickRightClick( stack, View, 10.3f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "220 ms after the eject button's click: no click" );

		QuickRightClick( stack, View, 11.3f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "a second later: out of first person" );
	}

	/// <summary>
	/// <b>The press decides whose the click is, and the release may be anywhere</b>: one pressed on the eject button and let
	/// go of on the view stays the button's, and one pressed on the view and let go of on the button leaves.
	/// </summary>
	/// <remarks><b>Mutations:</b> the click given to what is under the pointer at the release.</remarks>
	[TestMethod]
	public void TheReleaseMayBeAnywhere()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, Eject, 10.00f );
		Frame( stack, false, View, 10.08f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "pressed on the eject button: still in first person" );

		Frame( stack, true, new Vector2( 1900, 1450 ), 11.00f );
		Frame( stack, false, Eject, 11.08f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "pressed on the view, let go of on the button: out of it" );
	}

	/// <summary>
	/// <b>A stray is measured in the interface's units, not the window's pixels</b>: at 1024x768, half the interface's
	/// size, 4 pixels are 8 units and a drag, and 3 are 6 and a click.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stray measured in window pixels.</remarks>
	[TestMethod]
	public void AStrayIsMeasuredInInterfaceUnits()
	{
		Screen.Size = new Point2( 1024, 768 );
		var stack = APark();
		var middle = new Vector2( 512, 350 );
		InFirstPerson( true );

		Frame( stack, true, middle, 10.00f );
		Frame( stack, true, middle + new Vector2( 4, 0 ), 10.05f );
		Frame( stack, false, middle, 10.10f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "4 pixels: a drag" );

		Frame( stack, true, middle, 11.00f );
		Frame( stack, true, middle + new Vector2( 3, 0 ), 11.05f );
		Frame( stack, false, middle, 11.10f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "3 pixels: a click" );
	}

	/// <summary>
	/// <b>A press 500 ms after the last clean release is a press of its own</b>: only one less than 500 ms after it is a
	/// double click's second (<c>0x0065f8d3</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the double click's window taken as at most 500 ms.</remarks>
	[TestMethod]
	public void APressHalfASecondAfterTheLastReleaseClicks()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, View, 10.00f );
		Frame( stack, false, View, 10.25f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "the first click leaves" );

		InFirstPerson( true );
		Frame( stack, true, View, 10.75f );
		Frame( stack, false, View, 10.80f );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "and one pressed 500 ms after its release leaves too" );
	}

	/// <summary>A park's interface: the real front end over a real stack, its gadget and viewfinder up.</summary>
	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>One frame of the game: the right button and the pointer at a time, then the stack's update.</summary>
	private static void Frame( UI.WindowStack stack, bool right, Vector2 at, float now )
	{
		Time.Now = now;
		Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = at };
		stack.Update();
	}

	/// <summary>A right press and its release 80 ms on, at one point.</summary>
	private static void QuickRightClick( UI.WindowStack stack, Vector2 at, float now )
	{
		Frame( stack, true, at, now );
		Frame( stack, false, at, now + 0.08f );
	}

	/// <summary>A right press on the view taken a way off and back inside 150 ms, and let go of where it went down.</summary>
	private static void DraggedRightClick( UI.WindowStack stack, Vector2 by, float now )
	{
		Frame( stack, true, View, now );
		Frame( stack, true, View + by, now + 0.05f );
		Frame( stack, true, View, now + 0.10f );
		Frame( stack, false, View, now + 0.15f );
	}

	/// <summary>Puts first person up or down, as entering and leaving it do, without a camera to move.</summary>
	private static void InFirstPerson( bool active )
		=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, active );
}
