using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park's full-screen view, F3 (<c>FUN_004a29d0</c>): the key let go puts a full-screen control over the park's
/// layer, which is hidden; under it only the camera hears the player, F3 or a plain Escape takes it off and opens no
/// menu, and Ctrl+P is the postcard's. See <c>ParkFrontEnd.ToggleFullScreen</c>, <c>WindowStack.Covered</c> and
/// <c>docs/exe/park-engine.md</c>, "The full-screen view: F3".
///
/// <para>
/// <b>The frame is the game's own</b>: SDL's key events through <see cref="Input.UpdateFrom"/> and the real stack's
/// whole update under the real park front end. No park is loaded, so the hand holds a build tool. That the layer is
/// not drawn is the photograph's to show, not these.
/// </para>
/// </summary>
[TestClass]
public class ParkFullScreenViewTests
{
	/// <summary>The gadget's bare metal above its date, on the interface's 2048x1536, which the screen is here.</summary>
	private static readonly Vector2 GadgetBody = new( 240, 1030 );

	/// <summary>A point of the park no control is over.</summary>
	private static readonly Vector2 Grass = new( 1100, 800 );

	private Point2 _screen;
	private Level? _level;
	private Vector3 _orbitLooksAt;
	private float _orbitYaw;

	[TestInitialize]
	public void StartWithNothingHeld()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		_level = Level.Current;
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		_orbitYaw = ParkOrbitCameraMode.Yaw;

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
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		Level.Current = _level!;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>F3 acts on its release</b>: held, its repeats included, the view is off; let go, it is on; let go again, off.
	/// The game table's row 4 is run on the key-up (<c>FUN_0040c990</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the toggle on the press turns it on while the key is held; a toggle that only turns on leaves
	/// it on after the second.
	/// </remarks>
	[TestMethod]
	public void F3LetGoTurnsTheViewOnAndLetGoAgainOff()
	{
		var stack = APark();

		for ( var frame = 0; frame < 3; ++frame )
			Frame( stack, Down( Key.F3 ) );

		Assert.IsFalse( stack.Covered, "held, with its repeats: nothing yet" );

		Frame( stack, Up( Key.F3 ) );
		Assert.IsTrue( stack.Covered, "let go: the view is on" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and no window opened or closed" );

		Frame( stack, Down( Key.F3 ) );
		Assert.IsTrue( stack.Covered, "held again: still on" );

		Frame( stack, Up( Key.F3 ) );
		Assert.IsFalse( stack.Covered, "let go again: off" );
	}

	/// <summary>
	/// <b>Escape under the view takes it off and does nothing more</b>: the tool stays in the hand and no menu opens,
	/// where an Escape with the view off would have put the tool away. The view's control answers the key itself
	/// (<c>0x004a2990</c>) and the park's tables never see it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> handing the key on to the menu's road after turning the view off puts the tool away; an Escape
	/// the view does not answer leaves it on.
	/// </remarks>
	[TestMethod]
	public void EscapeUnderTheViewPutsTheInterfaceBackAndOpensNoMenu()
	{
		var stack = APark();
		ParkBuildMode.Arm( ParkBuildMode.Path );

		Press( stack, Key.F3 );
		Assert.IsTrue( stack.Covered );

		Press( stack, Key.Escape );
		Assert.IsFalse( stack.Covered, "Escape let go: the view is off" );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "the tool is still in the hand" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and there is no menu" );

		Press( stack, Key.Escape );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "the next Escape is the park's own again" );
	}

	/// <summary>
	/// <b>An Escape and an F3 let go in one frame are two keys</b>, each seeing what the one before left: F3 then Escape
	/// turns the view on and off again and opens no menu; Escape then F3, with the hand empty, opens the menu, whose
	/// F3 is nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> reading whether the view is on once for the frame opens the menu under the view.</remarks>
	[TestMethod]
	public void TwoKeysInOneFrameEachSeeWhatTheOtherLeft()
	{
		var stack = APark();

		Frame( stack, Down( Key.F3 ), Down( Key.Escape ) );
		Frame( stack, Up( Key.F3 ), Up( Key.Escape ) );
		Assert.IsFalse( stack.Covered, "on, then off" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and no menu" );

		Frame( stack, Down( Key.F3 ), Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ), Up( Key.F3 ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "the menu, from the Escape" );
		Assert.IsFalse( stack.Covered, "and the F3 after it is the menu's, which does nothing with it" );
	}

	/// <summary>
	/// <b>With a modifier held neither key is the view's</b>: Shift, Ctrl or Alt with F3 does not turn it on, and with
	/// it on neither F3 nor Escape under one turns it off. Row 4 names no modifier, and the control's Escape test is
	/// for none (<c>0x004a2938</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> taking the modifier test off either arm toggles the view under the modifier.</remarks>
	[DataTestMethod]
	[DataRow( Key.ShiftLeft )]
	[DataRow( Key.ControlLeft )]
	[DataRow( Key.AltRight )]
	public void AModifierHeldKeepsBothKeysFromTheView( Key modifier )
	{
		var stack = APark();

		Frame( stack, Down( modifier ), Down( Key.F3 ) );
		Frame( stack, Up( Key.F3 ) );
		Assert.IsFalse( stack.Covered, $"{modifier}+F3 does not turn it on" );

		Frame( stack, Up( modifier ) );
		Press( stack, Key.F3 );
		Assert.IsTrue( stack.Covered );

		Frame( stack, Down( modifier ), Down( Key.F3 ) );
		Frame( stack, Up( Key.F3 ) );
		Assert.IsTrue( stack.Covered, $"{modifier}+F3 does not turn it off" );

		Frame( stack, Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ) );
		Assert.IsTrue( stack.Covered, $"nor {modifier}+Escape" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "which opens no menu either" );
	}

	/// <summary>
	/// <b>Ctrl+P under the view is the postcard's</b>, counted once a key-up; with the view off the park front end counts
	/// nothing for it, and a plain P under the view is nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> counting any P, or counting with the view off, moves the count.</remarks>
	[TestMethod]
	public void CtrlPUnderTheViewIsCountedAsThePostcard()
	{
		var stack = APark();

		Frame( stack, Down( Key.ControlLeft ), Down( Key.P ) );
		Frame( stack, Up( Key.P ) );
		Frame( stack, Up( Key.ControlLeft ) );
		Assert.AreEqual( 0, Times( "FULL_SCREEN_VIEW_POSTCARD" ), "with the view off: not this count" );

		Press( stack, Key.F3 );
		Press( stack, Key.P );
		Assert.AreEqual( 0, Times( "FULL_SCREEN_VIEW_POSTCARD" ), "a plain P: nothing" );

		Frame( stack, Down( Key.ControlLeft ), Down( Key.P ) );
		Frame( stack, Up( Key.P ) );
		Assert.AreEqual( 1, Times( "FULL_SCREEN_VIEW_POSTCARD" ), "Ctrl+P let go: counted" );
		Assert.IsTrue( stack.Covered, "and the view stays on" );
	}

	/// <summary><b>In first person F3 is refused</b> (<c>gui_CameraFlags &amp; 0x16</c>, <c>0x004a29e6</c>).</summary>
	/// <remarks><b>Mutations:</b> taking the test out turns the view on over the viewfinder.</remarks>
	[TestMethod]
	public void InFirstPersonF3IsRefused()
	{
		var stack = APark();
		InFirstPerson( true );

		Press( stack, Key.F3 );

		Assert.IsFalse( stack.Covered );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "and first person is as it was" );
	}

	/// <summary>
	/// <b>Over a window that has the keys F3 does nothing</b>: the game menu, and a message box, and a park screen, whose
	/// handler runs the shortcuts' table alone (<c>FUN_00488ba0</c>) while the game table is off. Over a window that is
	/// neither the view goes on, the window still open under it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking out the modal test turns the view on over the menu; taking out the park screen's turns
	/// it on over the screen; refusing every window in front refuses the plain one.
	/// </remarks>
	[TestMethod]
	public void OverAWindowWithTheKeysF3DoesNothing()
	{
		var stack = APark();

		Press( stack, Key.Escape );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ) );

		Press( stack, Key.F3 );
		Assert.IsFalse( stack.Covered, "over the game menu: nothing" );

		Press( stack, Key.Escape );
		var screen = new AWindow( stack, modal: false, parkScreen: true );
		stack.Open( screen );

		Press( stack, Key.F3 );
		Assert.IsFalse( stack.Covered, "over a park screen, whose handler runs the shortcuts' table alone: nothing" );

		stack.Close( screen );
		stack.Open( new AWindow( stack, modal: false, parkScreen: false ) );

		Press( stack, Key.F3 );
		Assert.IsTrue( stack.Covered, "over a window that is neither: on" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow", Names( stack ), "the window still open under it" );
	}

	/// <summary>
	/// <b>Under the view every press is the cover's</b>: the pointer over the gadget is over no control, a left press
	/// and a right press there or on the grass are both taken from the park, and nothing is pressed. With the view off
	/// the same point is the gadget's and the grass is the park's.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a cover that still hit-tests hovers the gadget; one that does not take the left press, or the
	/// right, hands the grass's to the park.
	/// </remarks>
	[TestMethod]
	public void UnderTheViewEveryPressIsTheCovers()
	{
		var stack = APark();

		Pointer( stack, GadgetBody, left: false, right: false );
		Assert.IsNotNull( stack.Hovered, "view off: the gadget is under the pointer" );

		Pointer( stack, Grass, left: true, right: true );
		Assert.IsFalse( UI.WindowStack.PointerTaken, "view off: a left press on the grass is the park's" );
		Assert.IsFalse( UI.WindowStack.RightPointerTaken, "and a right one" );
		Pointer( stack, Grass, left: false, right: false );

		Press( stack, Key.F3 );

		Pointer( stack, GadgetBody, left: false, right: false );
		Assert.IsNull( stack.Hovered, "view on: nothing is under the pointer" );

		Pointer( stack, GadgetBody, left: true, right: true );
		Assert.IsNull( stack.Hovered, "nor pressed" );
		Assert.IsTrue( UI.WindowStack.PointerTaken, "and the press is not the park's" );
		Assert.IsTrue( UI.WindowStack.RightPointerTaken );
		Pointer( stack, GadgetBody, left: false, right: false );

		Pointer( stack, Grass, left: true, right: false );
		Assert.IsTrue( UI.WindowStack.PointerTaken, "view on: a left press on the grass is the cover's" );
		Pointer( stack, Grass, left: false, right: true );
		Assert.IsTrue( UI.WindowStack.RightPointerTaken, "and a right one" );
	}

	/// <summary>
	/// <b>A control held down as the view goes on is let go of unclicked</b>, and its release under the view clicks
	/// nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> a cover that leaves the press standing leaves the control pressed.</remarks>
	[TestMethod]
	public void AControlHeldAsTheViewGoesOnIsLetGo()
	{
		var stack = APark();

		Pointer( stack, GadgetBody, left: true, right: false );
		var held = stack.Hovered!;
		var clicked = 0;
		held.Clicked += () => ++clicked;
		Assert.IsTrue( held.Pressed, "pressed, and held" );

		Input.UpdateFrom( new Snapshot( [Down( Key.F3 )], GadgetBody, left: true ) );
		stack.Update();
		Input.UpdateFrom( new Snapshot( [Up( Key.F3 )], GadgetBody, left: true ) );
		stack.Update();

		Assert.IsTrue( stack.Covered );
		Assert.IsFalse( held.Pressed, "the view on: let go of" );

		Pointer( stack, GadgetBody, left: false, right: false );
		Assert.AreEqual( 0, clicked, "and the release clicks nothing" );
	}

	/// <summary>
	/// <b>The build keys and C are not heard under the view</b>: Delete let go leaves the tool in the hand, and C let go
	/// leaves the camera in orbit. With the view off each does its own.
	/// </summary>
	/// <remarks><b>Mutations:</b> taking the view's test out of <c>Level.BuildKeys</c> puts the tool away; out of the
	/// orbit camera's C, goes into first person.</remarks>
	[TestMethod]
	public void TheBuildKeysAndCAreNotHeardUnderTheView()
	{
		var stack = APark();
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		Level.Current = park;
		Camera.SetCameraMode<ParkOrbitCameraMode>();

		Press( stack, Key.F3 );
		Assert.IsTrue( park.FullScreenView );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		WorldFrame( stack, park, Down( Key.Delete ), Down( Key.C ) );
		WorldFrame( stack, park, Up( Key.Delete ), Up( Key.C ) );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "Delete under the view: the tool stays" );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "C under the view: still in orbit" );

		Press( stack, Key.F3 );
		Assert.IsFalse( park.FullScreenView );

		WorldFrame( stack, park, Down( Key.Delete ) );
		WorldFrame( stack, park, Up( Key.Delete ) );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "the view off: Delete puts the tool away" );

		WorldFrame( stack, park, Down( Key.C ) );
		WorldFrame( stack, park, Up( Key.C ) );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "and C goes down into first person" );
	}

	/// <summary>A park's interface: the real front end over a real stack, its gadget and viewfinder up.</summary>
	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>One frame of the game with these key events and the pointer on the grass: the input, then the stack's update.</summary>
	private static void Frame( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys, Grass ) );
		stack.Update();
	}

	/// <summary>A key down in one frame and up in the next.</summary>
	private static void Press( UI.WindowStack stack, Key key )
	{
		Frame( stack, Down( key ) );
		Frame( stack, Up( key ) );
	}

	/// <summary>One frame with the pointer at a point of the screen and the buttons down or up.</summary>
	private static void Pointer( UI.WindowStack stack, Vector2 at, bool left, bool right )
	{
		Input.UpdateFrom( new Snapshot( [], at, left, right ) );
		stack.Update();
	}

	/// <summary>One frame as <c>Level.Update</c> orders it: the input, the stack, the build keys, then the camera.</summary>
	private static void WorldFrame( UI.WindowStack stack, Level park, params KeyEvent[] keys )
	{
		Frame( stack, keys );
		typeof( Level ).GetMethod( "BuildKeys", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( park, [] );
		Camera.Update();
	}

	/// <summary>Puts first person up or down, as entering and leaving it do, without a camera to move.</summary>
	private static void InFirstPerson( bool active )
		=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, active );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	/// <summary>A bare window, modal or not, a park screen or not.</summary>
	private sealed class AWindow : UI.UiWindow
	{
		public AWindow( UI.WindowStack stack, bool modal, bool parkScreen ) : base( stack )
		{
			Root = new UI.UiControl { Rect = new UI.UiRect( 600, 300, 1000, 600 ) };
			Modal = modal;
			ParkScreen = parkScreen;
		}
	}

	/// <summary>SDL's events for one frame: the keys, where the pointer is and which buttons are down.</summary>
	private sealed class Snapshot( KeyEvent[] keys, Vector2 at, bool left = false, bool right = false ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => new( at.X, at.Y );
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => button == MouseButton.Left ? left : button == MouseButton.Right && right;
	}
}
