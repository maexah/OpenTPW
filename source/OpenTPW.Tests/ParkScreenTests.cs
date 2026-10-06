using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park's screens - the six management screens and the object windows - as the original keeps them: built onto the
/// park's own layer, not modal, one open at a time (<c>DAT_007c24c8</c>). The gadget answers beside one, a press on its
/// bare frame is the interface's, a left press beside it is kept from the park's idle click, and while it is open only
/// the shortcuts' keys are run. See <c>UiWindow.ParkScreen</c> and <c>docs/exe/park-engine.md</c>, "A park screen is open".
///
/// <para>
/// <b>The frame is the game's own</b>: SDL's events through <see cref="Input.UpdateFrom"/> and the real stack's whole
/// update under the real park front end. No park is loaded, so the hand holds a build tool where it holds anything.
/// </para>
/// </summary>
[TestClass]
public class ParkScreenTests
{
	/// <summary>The middle of the gadget's Buy, Info and camcorder buttons, on the interface's 2048x1536.</summary>
	private static readonly Vector2 Buy = new( 229, 1181 );
	private static readonly Vector2 Info = new( 346, 1192 );
	private static readonly Vector2 Camcorder = new( 261, 1414 );

	/// <summary>The buy screen's bare frame, at its left edge, where none of its controls is.</summary>
	private static readonly Vector2 Frame = new( 200, 500 );

	/// <summary>A point of the park below every screen, which no control is over.</summary>
	private static readonly Vector2 Grass = new( 1100, 1250 );

	private Point2 _screen;
	private Level? _level;
	private float _orbitYaw;
	private Vector3 _orbitLooksAt;

	[TestInitialize]
	public void StartWithNothingHeld()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		_level = Level.Current;
		_orbitYaw = ParkOrbitCameraMode.Yaw;
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;

		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();
		Time.Now = 100f;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkBuildMode.Forget();
		ParkCamcorderCameraMode.Forget();
		Camera.SetCameraMode<CameraMode>();
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		Level.Current = _level!;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();

		// The stack's own static, which the next test's first frame would otherwise read stale.
		new UI.WindowStack().Update();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>The gadget answers beside an open screen, and the screen it opens closes the one that was open</b>: with the
	/// buy screen up, a click on the gadget's Info button leaves one park screen, and it is not the buy screen.
	/// <c>FUN_004a0940</c> tests the game menu alone, and every opener starts with <c>FUN_00485b40</c>.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the buy screen modal again, and the click never reaches the button; an opener that closes
	/// nothing leaves two screens.
	/// </remarks>
	[TestMethod]
	public void TheGadgetAnswersBesideAScreenAndItsScreenClosesTheOneOpen()
	{
		var stack = APark();

		Click( stack, Buy );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ) );
		Assert.IsFalse( stack.ModalUp, "the buy screen is not modal" );

		Click( stack, Info );

		Assert.AreEqual( 1, stack.Windows.Count( window => window.ParkScreen ), "one park screen at a time" );
		Assert.IsFalse( stack.Windows.Any( window => window is UI.ParkBuyScreen ), "and the buy screen is the one that closed" );
	}

	/// <summary>
	/// <b>The Buy button with the buy screen already up closes and opens nothing</b>: the opener only picks its tab
	/// again (<c>0x004acca0</c>), so the screen that was open is the screen that is open.
	/// </summary>
	/// <remarks><b>Mutations:</b> opening a fresh one every click replaces it.</remarks>
	[TestMethod]
	public void BuyWithTheBuyScreenUpLeavesItAsItIs()
	{
		var stack = APark();

		Click( stack, Buy );
		var first = stack.Windows[^1];

		Click( stack, Buy );

		Assert.AreSame( first, stack.Windows[^1] );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ) );
	}

	/// <summary>
	/// <b>A left press on a screen's bare frame is the interface's, and one beside it is not</b>: the root is one plain
	/// rectangle that takes a press anywhere on it, as it does a right press, and the park below it is still the park.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a left press that asks only for a control goes through the frame; a screen that takes every
	/// press, as a modal one does, takes the park's too.
	/// </remarks>
	[TestMethod]
	public void APressOnAScreensBareFrameIsTheInterfacesAndOneBesideItIsNot()
	{
		var stack = APark();
		var buy = new UI.ParkBuyScreen( stack );
		stack.Open( buy );

		Assert.IsNull( buy.Root.HitTest( Frame.X, Frame.Y ), "no control of the buy screen's is there" );

		Pointer( stack, Frame, left: true );
		Assert.IsTrue( UI.WindowStack.PointerTaken, "the bare frame" );
		Pointer( stack, Frame, left: false );

		Pointer( stack, Grass, left: true );
		Assert.IsFalse( UI.WindowStack.PointerTaken, "the park below it" );
		Pointer( stack, Grass, left: false );

		Assert.IsTrue( stack.ClickAt( Frame.X, Frame.Y ), "the console's click reads the frame the same way" );
		Assert.IsFalse( stack.ClickAt( Grass.X, Grass.Y ) );
		Assert.IsTrue( stack.TakesRightPress( Frame.X, Frame.Y ), "and so does the right button" );
		Assert.IsFalse( stack.TakesRightPress( Grass.X, Grass.Y ) );
	}

	/// <summary>
	/// <b>A screen's frame hides the control behind it</b>: a window opened under a park screen takes no press through
	/// the screen's rectangle, and takes its own beside it.
	/// </summary>
	/// <remarks><b>Mutations:</b> a hit test that walks on past the screen's frame presses the control behind it.</remarks>
	[TestMethod]
	public void AScreensFrameHidesTheControlBehindIt()
	{
		var stack = APark();
		var clicks = 0;
		stack.Open( new AWindow( stack, parkScreen: false, new UI.UiRect( 150, 450, 250, 1300 ), () => ++clicks ) );
		stack.Open( new UI.ParkBuyScreen( stack ) );

		stack.ClickAt( Frame.X, Frame.Y );
		Assert.AreEqual( 0, clicks, "through the buy screen's frame: nothing" );

		stack.ClickAt( 200, 1280 );
		Assert.AreEqual( 1, clicks, "below the screen: the control's own" );
	}

	/// <summary>
	/// <b>Beside an open screen the park's idle click is kept, and what the hand holds is not</b>: the park proc skips a
	/// press while a screen is open (<c>0x00488741</c>), so nothing is opened or picked up; the release still reaches the
	/// mode's button-up slot, which is where a tool commits.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> keeping nothing lets the idle click through; keeping every press stops the armed tool; a flag
	/// never written, or never cleared, reads the wrong way after the screen opens or closes.
	/// </remarks>
	[TestMethod]
	public void BesideAnOpenScreenTheIdleClickIsKeptAndAnArmedToolIsNot()
	{
		var stack = APark();

		Pointer( stack, Grass, left: false );
		Assert.IsFalse( UI.WindowStack.ParkScreenOpen, "no screen yet" );
		Assert.IsFalse( Level.KeptFromThePark( UI.WindowStack.ParkScreenOpen ) );

		var screen = new AWindow( stack, parkScreen: true );
		stack.Open( screen );
		Pointer( stack, Grass, left: false );

		Assert.IsTrue( UI.WindowStack.ParkScreenOpen, "the stack says a screen is open" );
		Assert.IsTrue( Level.KeptFromThePark( UI.WindowStack.ParkScreenOpen ), "idle: the press is kept" );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		Assert.IsFalse( Level.KeptFromThePark( UI.WindowStack.ParkScreenOpen ), "the path tool armed: it is the tool's" );
		ParkBuildMode.Forget();

		stack.Close( screen );
		Pointer( stack, Grass, left: false );
		Assert.IsFalse( UI.WindowStack.ParkScreenOpen, "closed: the park's again" );
	}

	/// <summary>
	/// <b>The park's own frame joins the two</b>: after the stack's update the level reads the left button. With no
	/// screen open a press on the park is a world click; with one open and nothing held it is none, on the screen's bare
	/// frame or beside it; with a tool armed beside the screen it is the tool's, and anchors its run.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the level's frame not asking whether a screen is open clicks the park beside it; not asking
	/// whether the interface took the press clicks through the frame.
	/// </remarks>
	[TestMethod]
	public void TheParksFrameKeepsAnIdleClickBesideAScreenAndGivesAnArmedToolIts()
	{
		var stack = APark();
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		var cell = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var cellWas = cell.GetValue( null );
		cell.SetValue( null, (20 * ParkWorld.MapSize) + 47 + 1 );

		var clicks = 0;
		Logger.LogDelegate count = ( _, text ) => clicks += text.Contains( "world click:" ) ? 1 : 0;
		Logger.OnLog += count;

		try
		{
			WorldPress( stack, park, Grass );
			Assert.AreEqual( 1, clicks, "no screen open: the press is the park's" );

			stack.Open( new AWindow( stack, parkScreen: true ) );
			Pointer( stack, Grass, left: false );

			WorldPress( stack, park, Grass );
			Assert.AreEqual( 1, clicks, "beside the open screen, nothing held: kept from the park" );

			WorldPress( stack, park, new Vector2( 800, 450 ) );
			Assert.AreEqual( 1, clicks, "on the screen's bare frame: the interface's" );

			ParkBuildMode.Arm( ParkBuildMode.Queue );
			WorldPress( stack, park, Grass );
			Assert.AreEqual( 2, clicks, "beside the screen with a tool armed: the tool's" );
			Assert.IsTrue( ParkBuildMode.Anchored, "and it anchored its run" );

			WorldPress( stack, park, new Vector2( 800, 450 ) );
			Assert.AreEqual( 2, clicks, "the frame still takes its own, tool or no tool" );
		}
		finally
		{
			Logger.OnLog -= count;
			cell.SetValue( null, cellWas );
		}
	}

	/// <summary>
	/// <b>The game menu closes the open screen as it opens</b> (<c>MenuList_Show</c>, <c>0x00493171</c>), and so does any
	/// window that says it does; a window that does not leaves it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the menu not saying so leaves the screen open under it; every window closing it closes it under the plain one.</remarks>
	[TestMethod]
	public void TheGameMenuClosesTheOpenScreen()
	{
		var stack = APark();
		stack.Open( new AWindow( stack, parkScreen: true ) );
		stack.Open( new AWindow( stack, parkScreen: false ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow, AWindow", Names( stack ), "a plain window leaves it" );

		stack.Open( new UI.GameMenu( stack, [], 10 ) );

		Assert.AreEqual( 0, stack.Windows.Count( window => window.ParkScreen ), "the menu closed the screen" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow, GameMenu", Names( stack ) );
	}

	/// <summary>
	/// <b>A screen opening folds the gadget's arm away</b> (<c>FUN_004a25f0( 1 )</c> from <c>FUN_00485b70</c>): the
	/// camcorder button put the arm out, and the buy screen opening brings it in and lifts the button.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the fold out leaves the arm out under the screen; folding on every frame a screen is up
	/// brings it in again the frame after the button put it out.
	/// </remarks>
	[TestMethod]
	public void AScreenOpeningFoldsTheGadgetsArm()
	{
		var stack = APark();
		var gadget = stack.Windows.OfType<UI.ParkGadget>().Single();

		Click( stack, Camcorder );
		Assert.IsTrue( gadget.ArmOut, "the camcorder button puts the arm out" );

		stack.Open( new UI.ParkBuyScreen( stack ) );
		Pointer( stack, Grass, left: false );

		Assert.IsFalse( gadget.ArmOut, "the screen opening folds it" );

		Click( stack, Camcorder );
		Pointer( stack, Grass, left: false );
		Assert.IsTrue( gadget.ArmOut, "and with the screen still up the button puts it out again, and it stays out" );
	}

	/// <summary>
	/// <b>While a screen is open the game's and the camera's keys are not heard</b>: opening one switches both tables
	/// off (<c>FUN_00485b70</c>, <c>0x00485ccd</c>, <c>0x00485ce6</c>), so Delete leaves the tool armed and the Left arrow
	/// leaves the camera where it looks; closed, both act.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the build keys not asking puts the tool away under the screen; the camera not asking turns it.
	/// </remarks>
	[TestMethod]
	public void TheGamesAndTheCamerasKeysAreNotHeardWhileAScreenIsOpen()
	{
		var stack = APark();
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		Level.Current = park;
		Camera.SetCameraMode<ParkOrbitCameraMode>();

		var screen = new AWindow( stack, parkScreen: true );
		stack.Open( screen );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		var yaw = ParkOrbitCameraMode.Yaw;

		WorldFrame( stack, park, Down( Key.Delete ), Down( Key.Left ) );
		WorldFrame( stack, park, Up( Key.Delete ), Up( Key.Left ) );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "Delete under a screen: the tool stays" );
		Assert.AreEqual( yaw, ParkOrbitCameraMode.Yaw, "the Left arrow under a screen: the camera stays" );

		stack.Close( screen );

		WorldFrame( stack, park, Down( Key.Left ) );
		WorldFrame( stack, park, Up( Key.Left ) );
		Assert.AreNotEqual( yaw, ParkOrbitCameraMode.Yaw, "closed: the Left arrow turns the camera" );

		WorldFrame( stack, park, Down( Key.Delete ) );
		WorldFrame( stack, park, Up( Key.Delete ) );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "and Delete puts the tool away" );
	}

	/// <summary>
	/// <b>A management screen keeps a plain Escape and does nothing with it</b>, as it did while it was modal: the tool
	/// stays armed and no menu opens. Closing the screen on that key is the original's and is not built
	/// (<c>docs/QUEUE.md</c> Q119).
	/// </summary>
	/// <remarks><b>Mutations:</b> asking only for a modal window lets the key through, which puts the tool away.</remarks>
	[TestMethod]
	public void AManagementScreenStillKeepsEscape()
	{
		var stack = APark();
		stack.Open( new UI.ParkBuyScreen( stack ) );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		Keys( stack, Down( Key.Escape ) );
		Keys( stack, Up( Key.Escape ) );

		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "the tool is still armed" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkBuyScreen", Names( stack ), "and nothing opened or closed" );
	}

	/// <summary>A park's interface: the real front end over a real stack, its gadget and viewfinder up.</summary>
	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>One frame with the pointer at a point of the screen and the left button down or up.</summary>
	private static void Pointer( UI.WindowStack stack, Vector2 at, bool left )
	{
		Input.UpdateFrom( new Snapshot( [], at, left ) );
		stack.Update();
	}

	/// <summary>A real click: the pointer there, the button down for a frame, then up.</summary>
	private static void Click( UI.WindowStack stack, Vector2 at )
	{
		Pointer( stack, at, left: false );
		Pointer( stack, at, left: true );
		Pointer( stack, at, left: false );
	}

	/// <summary>A left press and its release as two of the game's frames: the stack's update, then the level's reading of the park's buttons.</summary>
	private static void WorldPress( UI.WindowStack stack, Level park, Vector2 at )
	{
		var worldClick = typeof( Level ).GetMethod( "WorldClick", BindingFlags.Instance | BindingFlags.NonPublic )!;

		foreach ( var down in new[] { true, false } )
		{
			Pointer( stack, at, down );
			worldClick.Invoke( park, [] );
		}
	}

	/// <summary>One frame with these key events and the pointer on the grass.</summary>
	private static void Keys( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys, Grass ) );
		stack.Update();
	}

	/// <summary>One frame as <c>Level.Update</c> orders it: the input, the stack, the build keys, then the camera.</summary>
	private static void WorldFrame( UI.WindowStack stack, Level park, params KeyEvent[] keys )
	{
		Keys( stack, keys );
		typeof( Level ).GetMethod( "BuildKeys", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( park, [] );
		Camera.Update();
	}

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	/// <summary>A bare window, a park screen or not, with one control that fills it when it is given a click to answer.</summary>
	private sealed class AWindow : UI.UiWindow
	{
		public AWindow( UI.WindowStack stack, bool parkScreen, UI.UiRect? rect = null, System.Action? clicked = null ) : base( stack )
		{
			Root = new UI.UiControl { Rect = rect ?? new UI.UiRect( 600, 300, 1000, 600 ), Clicked = clicked };
			ParkScreen = parkScreen;
		}
	}

	/// <summary>SDL's events for one frame: the keys, where the pointer is and whether the left button is down.</summary>
	private sealed class Snapshot( KeyEvent[] keys, Vector2 at, bool left = false ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => new( at.X, at.Y );
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => button == MouseButton.Left && left;
	}
}
