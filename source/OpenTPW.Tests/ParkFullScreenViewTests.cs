using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

	/// <summary>
	/// <b>F3, Escape and Ctrl+P are judged by the modifiers held as each came up</b> (<c>GetKeyState</c> at the key-up,
	/// <c>0x0046bb0b</c>): F3 up and then Shift up in one frame turns nothing on; under the view the same Escape turns
	/// nothing off, and P up then Ctrl up is still the postcard's.
	/// </summary>
	/// <remarks><b>Mutations:</b> any of the three asking the keys held as the frame ends fails its line.</remarks>
	[TestMethod]
	public void TheModifiersAreTheOnesHeldAsEachKeyCameUp()
	{
		var stack = APark();

		Frame( stack, Down( Key.ShiftLeft ), Down( Key.F3 ) );
		Frame( stack, Up( Key.F3 ), Up( Key.ShiftLeft ) );
		Assert.IsFalse( stack.Covered, "F3 up, then Shift up, in one frame: Shift+F3, nothing" );

		Press( stack, Key.F3 );
		Frame( stack, Down( Key.ShiftLeft ), Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ), Up( Key.ShiftLeft ) );
		Assert.IsTrue( stack.Covered, "under the view, Escape up then Shift up: the view stays on" );

		Frame( stack, Down( Key.ControlLeft ), Down( Key.P ) );
		Frame( stack, Up( Key.P ), Up( Key.ControlLeft ) );
		Assert.AreEqual( 1, Times( "FULL_SCREEN_VIEW_POSTCARD" ), "P up, then Ctrl up: Ctrl+P, counted" );

		Frame( stack, Down( Key.F3 ) );
		Frame( stack, Up( Key.F3 ), Down( Key.ShiftLeft ) );
		Assert.IsFalse( stack.Covered, "F3 up, then Shift down: a plain F3, the view off" );

		Frame( stack, Up( Key.ShiftLeft ) );
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
	/// <remarks><b>Mutations:</b> taking the view's test out of <c>Level.GameTableKeys</c> puts the tool away; out of the
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

	/// <summary>
	/// <b>A held pans the orbit camera left and S back, a park screen up stops the keys, and Up held zooms in at 50 a
	/// second</b> (<c>ParkOrbitCameraMode.Update</c>): A and S are no longer the shortcuts' rows, and
	/// <c>ParkOrbitCameraMode.Panning</c> says a pan was held, which ends a locator's follow.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pan left out of the frame; the park screen's gate taken out; the zoom's rate or
	/// direction; Panning not set; <c>Time.Delta</c> left out of the point's move.</remarks>
	[TestMethod]
	public void TheWASDKeysPanTheOrbitCameraAndUpZooms()
	{
		var stack = APark();
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		Level.Current = park;
		ParkOrbitCameraMode.Forget();
		Camera.SetCameraMode<ParkOrbitCameraMode>();
		var delta = Time.Delta;
		Time.Delta = 0.1f;

		try
		{
			var from = ParkOrbitCameraMode.PointOfInterest;
			WorldFrame( stack, park, Down( Key.A ) );
			Assert.IsTrue( ParkOrbitCameraMode.Panning, "A held is a pan" );
			Assert.IsTrue( ParkOrbitCameraMode.PointOfInterest.X < from.X - 1f && ParkOrbitCameraMode.PointOfInterest.Y == from.Y, "A held: left" );
			Assert.AreEqual( from.X - 9.368f, ParkOrbitCameraMode.PointOfInterest.X, 0.01f, "by the term times Time.Delta: the first frame's rise of 0.8932 of 104.88, for 0.1 s" );
			Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and opens no screen" );

			WorldFrame( stack, park, Up( Key.A ) );
			Assert.IsFalse( ParkOrbitCameraMode.Panning, "let go" );
			ParkOrbitCameraMode.Scroll = Vector2.Zero;

			from = ParkOrbitCameraMode.PointOfInterest;
			WorldFrame( stack, park, Down( Key.S ) );
			Assert.IsTrue( ParkOrbitCameraMode.PointOfInterest.Y < from.Y - 1f && ParkOrbitCameraMode.PointOfInterest.X == from.X, "S held: back" );
			WorldFrame( stack, park, Up( Key.S ) );
			ParkOrbitCameraMode.Scroll = Vector2.Zero;

			Press( stack, Key.E );
			Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkStaffScreen", Names( stack ), "E: the staff" );
			from = ParkOrbitCameraMode.PointOfInterest;
			WorldFrame( stack, park, Down( Key.W ), Down( Key.Up ) );
			Assert.AreEqual( (from, 110f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Panning), "under a park screen: no camera key" );
			WorldFrame( stack, park, Up( Key.W ), Up( Key.Up ) );
			Press( stack, Key.Escape );

			WorldFrame( stack, park, Down( Key.Up ) );
			Assert.AreEqual( 105f, ParkOrbitCameraMode.Zoom, 0.001f, "Up held 0.1 s" );
			WorldFrame( stack, park, Up( Key.Up ), Down( Key.Down ) );
			Assert.AreEqual( 110f, ParkOrbitCameraMode.Zoom, 0.001f, "Down held 0.1 s" );
			WorldFrame( stack, park, Up( Key.Down ) );
		}
		finally
		{
			Time.Delta = delta;
			ParkOrbitCameraMode.Forget();
			UI.ParkCategoryScreens.Seed();
		}
	}

	/// <summary>
	/// <b>No camera key pans or zooms under a message box, the Save or Load screen, the game menu or the options
	/// screen</b>: the first three switch the camera's table off (<c>FUN_00486b70</c>, <c>0x0047f241</c>,
	/// <c>0x0049f406</c>), the last two take every key themselves (<c>0x0048b6a0</c>; measured in the original,
	/// <c>q692/orig</c>), and a box being typed into has the keyboard (<c>Input.TextCaptured</c>). A pan held as the menu
	/// opens is let go there. With the box shut the same keys move the park.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>WindowStack.CameraKeysAway</c> left out of the keys' gate; the message box, the
	/// file screen, the game menu or the options screen left out of <c>CameraKeysAway</c>; the <c>TextCaptured</c> test
	/// taken out of the keys; the reset or the turn moved outside the <c>CameraKeysAway</c> gate.</remarks>
	[TestMethod]
	public void TheCameraKeysAreSilentUnderAMessageBoxTheFileScreensTheMenuTheOptionsAndATextBox()
	{
		var stack = APark();
		var park = AParkLevel( stack );
		var delta = Time.Delta;
		Time.Delta = 0.1f;

		try
		{
			var from = ParkOrbitCameraMode.PointOfInterest;
			var yaw = ParkOrbitCameraMode.Yaw;

			// The turn (Left) and the reset (Home, on its release) are camera keys too.
			void TurnAndResetKeysAreSilent( string under )
			{
				WorldFrame( stack, park, Down( Key.Left ), Down( Key.Home ) );
				WorldFrame( stack, park, Up( Key.Left ), Up( Key.Home ) );
				Assert.AreEqual( (yaw, false), (ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.WasReset), $"under {under}: Left does not turn, Home does not reset" );
			}

			var box = new UI.MessageBox( stack, "Test", () => { } );
			stack.Open( box );
			WorldFrame( stack, park, Down( Key.A ), Down( Key.Up ) );
			Assert.AreEqual( (from, 110f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Panning), "under a message box: no camera key" );
			WorldFrame( stack, park, Up( Key.A ), Up( Key.Up ) );
			stack.Close( box );

			var screen = new AFileScreen( stack );
			stack.Open( screen );
			WorldFrame( stack, park, Down( Key.A ), Down( Key.Up ) );
			Assert.AreEqual( (from, 110f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Panning), "under a file screen: none" );
			WorldFrame( stack, park, Up( Key.A ), Up( Key.Up ) );
			stack.Close( screen );

			var menu = new UI.GameMenu( stack, [], 10 );
			stack.Open( menu );
			WorldFrame( stack, park, Down( Key.A ), Down( Key.Up ) );
			Assert.AreEqual( (from, 110f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Panning), "under the game menu: none" );
			WorldFrame( stack, park, Up( Key.A ), Up( Key.Up ) );
			TurnAndResetKeysAreSilent( "the game menu" );

			UI.OptionsScreen.Open( stack, () => { } );
			WorldFrame( stack, park, Down( Key.A ), Down( Key.Up ) );
			Assert.AreEqual( (from, 110f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Panning), "under the options screen: none" );
			WorldFrame( stack, park, Up( Key.A ), Up( Key.Up ) );
			TurnAndResetKeysAreSilent( "the options screen" );
			stack.Close( stack.Windows.First( window => window is UI.OptionsScreen ) );
			stack.Close( menu );

			WorldFrame( stack, park, Down( Key.A ) );
			Assert.IsTrue( ParkOrbitCameraMode.Panning, "A held: panning" );
			stack.Open( new UI.GameMenu( stack, [], 10 ) );
			WorldFrame( stack, park, Down( Key.A ) );
			Assert.IsFalse( ParkOrbitCameraMode.Panning, "the menu opened over it: let go, the key still down" );
			stack.Close( stack.Windows.First( window => window is UI.GameMenu ) );
			WorldFrame( stack, park, Up( Key.A ) );
			ParkOrbitCameraMode.Forget();
			from = ParkOrbitCameraMode.PointOfInterest;

			Frame( stack, Down( Key.A ), Down( Key.Up ) );
			Input.TextCaptured = true;
			Camera.Update();
			Assert.AreEqual( (from, 110f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Panning), "a text box with the keyboard: none" );
			Frame( stack, Up( Key.A ), Up( Key.Up ) );

			WorldFrame( stack, park, Down( Key.A ), Down( Key.Up ) );
			Assert.IsTrue( ParkOrbitCameraMode.PointOfInterest.X < from.X - 1f && ParkOrbitCameraMode.Zoom < 110f, "the box shut: the same keys move the park" );
			WorldFrame( stack, park, Up( Key.A ), Up( Key.Up ) );
		}
		finally
		{
			Time.Delta = delta;
			ParkOrbitCameraMode.Forget();
			UI.ParkCategoryScreens.Seed();
		}
	}

	/// <summary>
	/// <b>Keypad 5 and Home put the camera back on their release</b>: zoom 110, rotation 0, the point (475, 175)
	/// (camera rows 19 and 24, <c>0x0042a500</c> from the key-up, applied at <c>0x0042b518</c>), the pan's term left
	/// alone so a coast carries on from there (measured in the original, <c>q690/orig</c>). Held, nothing yet; with Ctrl
	/// held, under a park screen or with a text box holding the keyboard, nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> on the press, not the release; the scroll noughted; a modifier let through; the park
	/// screen's gate or the text box's left out; each of the three values; <c>WasReset</c> not set or left set.</remarks>
	[TestMethod]
	public void TheCameraResetKeysPutTheCameraBackOnTheirRelease()
	{
		var stack = APark();
		var park = AParkLevel( stack );
		var delta = Time.Delta;
		Time.Delta = 0.1f;
		var away = new Vector3( 600f, 300f, 0f );

		void Away( Vector2 scroll )
		{
			(ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom) = (away, 2f, 90f);
			ParkOrbitCameraMode.Scroll = scroll;
		}

		(Vector3, float, float) Now() => (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom);

		try
		{
			Away( Vector2.Zero );
			WorldFrame( stack, park, Down( Key.Home ) );
			Assert.AreEqual( (away, 2f, 90f, false), (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.WasReset), "Home held: nothing yet" );
			WorldFrame( stack, park, Up( Key.Home ) );
			Assert.AreEqual( (new Vector3( 475f, 175f, 0f ), 0f, 110f), Now(), "Home let go: the original's three" );
			Assert.IsTrue( ParkOrbitCameraMode.WasReset, "and the frame says so" );
			WorldFrame( stack, park );
			Assert.IsFalse( ParkOrbitCameraMode.WasReset, "for that frame alone" );

			// A pan coasting at 50 a second along x: the term is not noughted, so the same frame's coast (50 / (1 + 10 x 0.1),
			// for 0.1 s) carries the point on 2.5 from the reset's.
			WorldFrame( stack, park, Down( Key.Keypad5 ) );
			Away( new Vector2( 50f, 0f ) );
			WorldFrame( stack, park, Up( Key.Keypad5 ) );
			Assert.AreEqual( (new Vector3( 477.5f, 175f, 0f ), 0f, 110f), Now(), "keypad 5, a coast running: on from (475, 175)" );
			Assert.AreEqual( 25f, ParkOrbitCameraMode.Scroll.X, 0.001f, "the term kept, coasted" );

			Away( Vector2.Zero );
			WorldFrame( stack, park, Down( Key.ControlLeft ), Down( Key.Home ) );
			WorldFrame( stack, park, Up( Key.Home ), Up( Key.ControlLeft ) );
			Assert.AreEqual( (away, 2f, 90f), Now(), "Ctrl+Home: not the row" );

			Press( stack, Key.E );
			Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkStaffScreen", Names( stack ), "E: the staff" );
			WorldFrame( stack, park, Down( Key.Home ) );
			WorldFrame( stack, park, Up( Key.Home ) );
			Assert.AreEqual( (away, 2f, 90f), Now(), "under a park screen: no camera key" );
			Press( stack, Key.Escape );

			Frame( stack, Down( Key.Home ) );
			Frame( stack, Up( Key.Home ) );
			Input.TextCaptured = true;
			Camera.Update();
			Input.TextCaptured = false;
			Assert.AreEqual( (away, 2f, 90f), Now(), "a text box with the keyboard: none" );
		}
		finally
		{
			Input.TextCaptured = false;
			Time.Delta = delta;
			ParkOrbitCameraMode.Forget();
			UI.ParkCategoryScreens.Seed();
		}
	}

	/// <summary>
	/// <b>In first person the reset keys are counted, not built</b> (<c>CAMERA_RESET_FIRST_PERSON</c>, <c>0x0042b518</c>): a bare
	/// Home or keypad 5 let go counts once each, and the viewer stays where it stood. Not held, not with a modifier, not under a
	/// park screen and not with a text box holding the keyboard.
	/// </summary>
	/// <remarks><b>Mutations:</b> counted on the press; a modifier let through; the park screen's gate or the text box's left
	/// out; the count left out.</remarks>
	[TestMethod]
	public void TheResetKeysAreCountedInFirstPerson()
	{
		var stack = APark();
		var park = AParkLevel( stack );
		Unimplemented.Forget();

		try
		{
			ParkOrbitCameraMode.PointOfInterest = new Vector3( 600f, 300f, 0f );
			ParkCamcorderCameraMode.Enter();
			var stood = ParkCamcorderCameraMode.Stand;

			WorldFrame( stack, park, Down( Key.Home ) );
			Assert.AreEqual( 0, Times( "CAMERA_RESET_FIRST_PERSON" ), "Home held: nothing yet" );
			WorldFrame( stack, park, Up( Key.Home ) );
			Assert.AreEqual( 1, Times( "CAMERA_RESET_FIRST_PERSON" ), "Home let go" );
			WorldFrame( stack, park, Down( Key.Keypad5 ) );
			WorldFrame( stack, park, Up( Key.Keypad5 ) );
			Assert.AreEqual( 2, Times( "CAMERA_RESET_FIRST_PERSON" ), "keypad 5 let go" );
			Assert.AreEqual( stood, ParkCamcorderCameraMode.Stand, "and nothing moves" );

			WorldFrame( stack, park, Down( Key.ControlLeft ), Down( Key.Home ) );
			WorldFrame( stack, park, Up( Key.Home ), Up( Key.ControlLeft ) );
			WorldFrame( stack, park, Down( Key.Keypad6 ) );
			WorldFrame( stack, park, Up( Key.Keypad6 ) );
			Assert.AreEqual( 2, Times( "CAMERA_RESET_FIRST_PERSON" ), "Ctrl+Home and another key: not the row" );

			WorldFrame( stack, park, Down( Key.Home ) );
			Frame( stack, Up( Key.Home ) );
			Input.TextCaptured = true;
			Camera.Update();
			Input.TextCaptured = false;
			Assert.AreEqual( 2, Times( "CAMERA_RESET_FIRST_PERSON" ), "a text box with the keyboard: none" );

			var screen = new AWindow( stack, modal: false, parkScreen: true );
			stack.Open( screen );
			WorldFrame( stack, park, Down( Key.Home ) );
			WorldFrame( stack, park, Up( Key.Home ) );
			Assert.AreEqual( 2, Times( "CAMERA_RESET_FIRST_PERSON" ), "under a park screen: no camera key" );
			stack.Close( screen );

			var file = new AFileScreen( stack );
			stack.Open( file );
			WorldFrame( stack, park, Down( Key.Home ) );
			WorldFrame( stack, park, Up( Key.Home ) );
			Assert.AreEqual( 2, Times( "CAMERA_RESET_FIRST_PERSON" ), "under a file screen: none" );
			stack.Close( file );

			WorldFrame( stack, park, Down( Key.Home ) );
			WorldFrame( stack, park, Up( Key.Home ) );
			Assert.AreEqual( 3, Times( "CAMERA_RESET_FIRST_PERSON" ), "and with the screens closed, counted again" );
		}
		finally
		{
			Input.TextCaptured = false;
			Unimplemented.Forget();
			ParkCamcorderCameraMode.Forget();
			ParkOrbitCameraMode.Forget();
			UI.ParkCategoryScreens.Seed();
		}
	}

	/// <summary>A park's stack with a bare park level over it, the camera put on the orbit and its state forgotten.</summary>
	private static Level AParkLevel( UI.WindowStack stack )
	{
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		typeof( Level ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!.SetValue( park, stack );
		Level.Current = park;
		ParkOrbitCameraMode.Forget();
		Camera.SetCameraMode<ParkOrbitCameraMode>();

		return park;
	}

	/// <summary>A file screen with no rows: the Save and Load screens' base, which holds the camera's table off.</summary>
	private sealed class AFileScreen : UI.ParkFileScreen
	{
		public AFileScreen( UI.WindowStack stack ) : base( stack, "jungle", UIStrings.LoadPark )
		{
		}

		protected override void RowClicked( int row )
		{
		}
	}

	/// <summary>
	/// <b>A cover with keys of its own, the End feature's, hears every key let go</b> (<c>EndFeature_OnMessage</c>,
	/// <c>0x0048a970</c>), F3 and Escape among them, so neither takes the cover off nor opens the menu; and the full-screen
	/// toggle itself refuses such a cover.
	/// </summary>
	/// <remarks><b>Mutations:</b> the park's keys not handed to the cover's (F3 heard by the view); the toggle not refusing it.</remarks>
	[TestMethod]
	public void ACoverWithKeysOfItsOwnHearsEveryKeyAndF3LeavesIt()
	{
		var stack = new UI.WindowStack();
		var front = new UI.ParkFrontEnd( stack, "jungle" );
		var heard = new List<Key>();

		try
		{
			stack.Cover( true );
			stack.CoverKeys = release => heard.Add( release.Key );

			Press( stack, Key.F3 );
			Press( stack, Key.Escape );

			CollectionAssert.AreEqual( new[] { Key.F3, Key.Escape }, heard, "the cover's handler hears both" );
			Assert.IsTrue( stack.Covered, "and stays on" );
			Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "no menu opened" );

			front.ToggleFullScreen();
			Assert.IsTrue( stack.Covered, "the toggle refuses a cover with keys of its own" );
		}
		finally
		{
			stack.Cover( false );
		}
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
		typeof( Level ).GetMethod( "GameTableKeys", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( park, [] );
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
