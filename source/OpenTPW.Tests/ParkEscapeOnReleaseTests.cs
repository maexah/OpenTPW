using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// A park takes Escape on its release, as the original's does (message 0x1000b; the press only latches its binding row,
/// <c>FUN_0040c900</c>): held, its repeats included, it lets go of nothing, opens nothing and closes nothing, and each
/// time it comes up it does one thing - leaves first person, closes the menu, empties the hand or opens the menu. With a
/// modifier held only the menu and the viewfinder answer it. See <c>ParkFrontEnd.ParkKeys</c> and
/// <c>docs/exe/park-engine.md</c>, "The hand's ways out".
///
/// <para>
/// <b>The frame is the game's own</b>: SDL's key events through <see cref="Input.UpdateFrom"/>, which fills the presses,
/// the releases, the keys held and the bindings, then every open window's <c>Update</c> and the stack's <c>Keyboard</c>.
/// The park front end is the real one, over a stack made without its constructor; no park is loaded, so the hand holds a
/// build tool, the lightest thing it can hold.
/// </para>
/// </summary>
[TestClass]
public class ParkEscapeOnReleaseTests
{
	private Point2 _screen;
	private Vector3 _orbitLooksAt;
	private float _orbitYaw;

	[TestInitialize]
	public void StartWithNothingHeld()
	{
		Log ??= new();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		// Leaving first person hands the orbit camera the viewer's place and heading, so both are put back after.
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		_orbitYaw = ParkOrbitCameraMode.Yaw;

		Input.ForgetHeldKeys();
		Input.TextCaptured = false;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkBuildMode.Forget();
		InFirstPerson( false );
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>A held Escape does nothing, and each release does one thing</b>: the first empties the hand and is spent doing
	/// it, the next opens the menu, and the one after closes it. Held over the menu, it stays open.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the park's Escape back on the press - <c>ParkKeys</c> reading <see cref="Input.KeysPressed"/> -
	/// puts the tool away while the key is held; handing Escape to the park on every frame it is held does the same.
	/// </remarks>
	[TestMethod]
	public void AHeldEscapeDoesNothingUntilItComesUp()
	{
		var stack = APark();
		ParkBuildMode.Arm( ParkBuildMode.Path );

		HoldEscape( stack );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "held, with its repeats: the tool is still in the hand" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and nothing opened" );

		Frame( stack, Up( Key.Escape ) );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "let go: the tool is put away" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and that is all it does" );

		HoldEscape( stack );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "held with the hand empty: no menu yet" );

		Frame( stack, Up( Key.Escape ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "let go: the menu" );

		HoldEscape( stack );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "held over the menu: it stays" );

		Frame( stack, Up( Key.Escape ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "let go again: the menu closes" );
	}

	/// <summary>
	/// <b>In first person Escape leaves on its release</b>, with the hand full, and does nothing else: the key is the
	/// viewfinder layer's (<c>0x00488a00</c>), which answers the key-up.
	/// </summary>
	/// <remarks><b>Mutations:</b> the park's Escape back on the press leaves first person while the key is held.</remarks>
	[TestMethod]
	public void InFirstPersonEscapeLeavesOnItsRelease()
	{
		var stack = APark();
		ParkBuildMode.Arm( ParkBuildMode.Path );
		InFirstPerson( true );

		HoldEscape( stack );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "held: still in first person" );

		Frame( stack, Up( Key.Escape ) );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "let go: out of it" );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "and the hand is as it was" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "with no menu" );
	}

	/// <summary>
	/// <b>With a modifier held, Escape reaches the menu and the viewfinder and nothing else</b>, whichever modifier it is.
	/// The menu's handler (<c>0x0048bb36</c>) and the viewfinder's compare the key alone; the chain's two Escape rows
	/// name no modifier, and a row's modifiers must match exactly.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking out the modifier test empties the hand under the modifier; a test that knows only one of
	/// the three, or only the left of a pair, does the same for the others; putting the test before the menu's arm, or
	/// before first person's, keeps the menu open or the viewer down.
	/// </remarks>
	[DataTestMethod]
	[DataRow( Key.ShiftLeft )]
	[DataRow( Key.ControlLeft )]
	[DataRow( Key.AltLeft )]
	[DataRow( Key.ShiftRight )]
	public void WithAModifierHeldOnlyTheMenuAndTheViewfinderAnswer( Key modifier )
	{
		var stack = APark();
		ParkBuildMode.Arm( ParkBuildMode.Path );

		Frame( stack, Down( modifier ), Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ) );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, $"{modifier}+Escape empties no hand" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "and opens no menu" );

		Frame( stack, Up( modifier ) );
		Frame( stack, Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ) );
		Frame( stack, Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "two plain ones empty the hand and open the menu" );

		Frame( stack, Down( modifier ), Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), $"{modifier}+Escape closes the menu" );

		InFirstPerson( true );
		Frame( stack, Down( Key.Escape ) );
		Frame( stack, Up( Key.Escape ) );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, $"and, {modifier} still held, leaves first person" );
	}

	/// <summary>
	/// <b>A message box over the menu keeps Escape</b>: let go, it closes neither the box nor the menu, empties no hand
	/// and opens no second menu. The original's box takes the focus and drops the key.
	/// </summary>
	/// <remarks><b>Mutations:</b> taking out the modal test puts the tool away under the box.</remarks>
	[TestMethod]
	public void AMessageBoxOverTheMenuKeepsEscape()
	{
		var stack = APark();

		Frame( stack, Down( Key.Escape ), Up( Key.Escape ) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ) );

		stack.Open( new UI.MessageBox( stack, "Restart this park?", () => { } ) );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		HoldEscape( stack );
		Frame( stack, Up( Key.Escape ) );

		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu, MessageBox", Names( stack ), "the box and the menu stay" );
		Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "and the hand is as it was" );
	}

	/// <summary>
	/// <b>Each release is a key of its own</b>, in the order they came up, each seeing the windows the one before left:
	/// two in one frame put the tool away and then open the menu, and two more close it and open it again - as the
	/// original's queue hands on two key-ups.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> reading only the first release of the frame leaves the menu shut; reading the front window once
	/// for the frame closes the menu twice, and it stays shut.
	/// </remarks>
	[TestMethod]
	public void TwoReleasesInOneFrameAreTwoEscapes()
	{
		var stack = APark();
		ParkBuildMode.Arm( ParkBuildMode.Path );

		Frame( stack, Down( Key.Escape ), Up( Key.Escape ), Down( Key.Escape ), Up( Key.Escape ) );

		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "the first put the tool away" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "and the second opened the menu" );

		var menu = stack.Windows[^1];
		Frame( stack, Down( Key.Escape ), Up( Key.Escape ), Down( Key.Escape ), Up( Key.Escape ) );

		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "the first closed it and the second opened one" );
		Assert.AreNotSame( menu, stack.Windows[^1], "a new one" );
	}

	/// <summary>A park's interface: the real front end over a stack made without its constructor, with its gadget and viewfinder up.</summary>
	private static UI.WindowStack APark()
	{
		FileSystem = GameData.Required();

		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );
		typeof( UI.WindowStack ).GetField( "_windows", BindingFlags.Instance | BindingFlags.NonPublic )!
			.SetValue( stack, new List<UI.UiWindow>() );

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>Holds Escape down for three frames, the last two its repeats, without letting it go.</summary>
	private static void HoldEscape( UI.WindowStack stack )
	{
		for ( var frame = 0; frame < 3; ++frame )
			Frame( stack, Down( Key.Escape ) );
	}

	/// <summary>One frame of the game with these key events: the input, every window's update, then the stack's keys.</summary>
	private static void Frame( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys ) );

		foreach ( var window in stack.Windows.ToArray() )
			window.Update();

		typeof( UI.WindowStack ).GetMethod( "Keyboard", BindingFlags.Instance | BindingFlags.NonPublic )!.Invoke( stack, [] );
	}

	/// <summary>Puts first person up or down, as entering and leaving it do, without a camera to move.</summary>
	private static void InFirstPerson( bool active )
		=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, active );

	/// <summary>SDL's key events for one frame, and nothing else.</summary>
	private sealed class Snapshot( KeyEvent[] keys ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => System.Numerics.Vector2.Zero;
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => false;
	}

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );
}
