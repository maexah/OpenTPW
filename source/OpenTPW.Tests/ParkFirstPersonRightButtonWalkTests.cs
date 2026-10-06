using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// In first person a held right button walks forward: the layer's handler hands every message to the camera's own
/// (<c>FUN_0042a760</c>), which keeps the button in bit 4 of <c>DAT_00790aac</c> from the press to the release, and the
/// walking camera adds the Up arrow's amount to its forward term while it is set (<c>0x0042b935</c>). See
/// <see cref="ParkCamcorderCameraMode.RightHeld"/> and <c>docs/exe/hud.md</c>, "Four ways out of camcorder mode".
///
/// <para>
/// <b>The frame is the game's own</b>, as <see cref="ParkFirstPersonRightClickTests"/>' is: the right button and the
/// pointer as <see cref="Input.Mouse"/> holds them, the stack's whole update over the real park front end, then the
/// camera's reading of the button and its walk, in the order <c>Level</c> runs them. No park is loaded, so no cell
/// edge stops a step. What is left unpinned is <c>Level</c>'s one call of the reading.
/// </para>
/// </summary>
[TestClass]
public class ParkFirstPersonRightButtonWalkTests
{
	private Point2 _screen;
	private Vector3 _orbitLooksAt;
	private float _orbitYaw;
	private bool _rmbCancel;
	private float _now;
	private float _delta;
	private bool _hudHidden;

	/// <summary>On the view, in the middle of the viewfinder's frame.</summary>
	private static readonly Vector2 View = new( 1024, 700 );

	/// <summary>On the eject button, (1916,1404)-(2018,1506).</summary>
	private static readonly Vector2 Eject = new( 1960, 1450 );

	/// <summary>Where the viewer starts, facing +Y.</summary>
	private static readonly Vector3 Start = new( 500f, 500f, 0f );

	/// <summary>A frame's length, and how far the forward key alone walks in it at 40 units a second.</summary>
	private const float FrameLength = 0.5f;

	private const float OneStep = 20f;

	[TestInitialize]
	public void StartInFirstPerson()
	{
		Log ??= new();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );

		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		_orbitYaw = ParkOrbitCameraMode.Yaw;
		_rmbCancel = GameOptions.Current.RmbCancel;
		_now = Time.Now;
		_delta = Time.Delta;
		_hudHidden = UI.RootPanel.Hidden;

		FileSystem = GameData.Required();
		GameOptions.Current.RmbCancel = true;
		UI.RootPanel.Hidden = false;
		Input.Mouse = new();
		Input.Forward = 0f;
		Input.Right = 0f;
		Input.ForgetHeldKeys();

		ParkCamcorderCameraMode.Forget();
		ParkCamcorderCameraMode.Stand = Start;
		Time.Delta = FrameLength;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkCamcorderCameraMode.Forget();
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		GameOptions.Current.RmbCancel = _rmbCancel;
		UI.RootPanel.Hidden = _hudHidden;
		Time.Now = _now;
		Time.Delta = _delta;
		Input.Mouse = new();
		Input.Forward = 0f;
		Input.Right = 0f;
		Input.ForgetHeldKeys();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>Held on the view it walks forward, a frame as far as the forward key walks</b>, and nowhere before the press.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bug put back (the keys alone walk); the button's amount not the key's.</remarks>
	[TestMethod]
	public void AHeldRightButtonWalksAsFarAsTheForwardKey()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, false, View, 10.0f );
		Assert.AreEqual( Start.Y, ParkCamcorderCameraMode.Stand.Y, "nothing held: standing" );

		Frame( stack, true, View, 10.5f );
		Frame( stack, true, View, 11.0f );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "held a second with RMB cancel on: still in first person" );
		Assert.AreEqual( Start.Y + (2 * OneStep), ParkCamcorderCameraMode.Stand.Y, 0.001f, "two frames held: two steps forward" );
		Assert.AreEqual( Start.X, ParkCamcorderCameraMode.Stand.X, 0.001f, "and none aside" );

		var byButton = ParkCamcorderCameraMode.Stand.Y - Start.Y;

		ParkCamcorderCameraMode.Forget();
		ParkCamcorderCameraMode.Stand = Start;
		InFirstPerson( true );
		Input.Forward = 1f;
		Frame( stack, false, View, 12.0f );
		Frame( stack, false, View, 12.5f );

		Assert.AreEqual( byButton, ParkCamcorderCameraMode.Stand.Y - Start.Y, 0.001f, "the forward key alone goes as far" );
	}

	/// <summary><b>Whatever RMB cancel is</b>: the camera reads the button's bit and never the switch (<c>0x0042b925</c>).</summary>
	/// <remarks><b>Mutations:</b> the walk kept to RMB cancel on, or to off.</remarks>
	[TestMethod]
	public void ItWalksWhateverRmbCancelIs()
	{
		var stack = APark();

		foreach ( var rmbCancel in new[] { true, false } )
		{
			GameOptions.Current.RmbCancel = rmbCancel;
			ParkCamcorderCameraMode.Forget();
			ParkCamcorderCameraMode.Stand = Start;
			InFirstPerson( true );

			Frame( stack, true, View, rmbCancel ? 10.0f : 20.0f );

			Assert.AreEqual( Start.Y + OneStep, ParkCamcorderCameraMode.Stand.Y, 0.001f, $"RMB cancel {(rmbCancel ? "on" : "off")}" );

			Frame( stack, false, View, rmbCancel ? 11.0f : 21.0f );
		}
	}

	/// <summary>
	/// <b>The button's amount is added to the keys'</b>: with the forward key it walks twice as far, with the back key it
	/// stands, and a sideways key is left as it is.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button taken only when no key walks; the button overriding the keys.</remarks>
	[TestMethod]
	public void ItAddsToWhatTheKeysGive()
	{
		var stack = APark();
		InFirstPerson( true );

		Input.Forward = 1f;
		Frame( stack, true, View, 10.0f );
		Assert.AreEqual( Start.Y + (2 * OneStep), ParkCamcorderCameraMode.Stand.Y, 0.001f, "with the forward key: twice as far" );

		Input.Forward = -1f;
		Frame( stack, true, View, 10.5f );
		Assert.AreEqual( Start.Y + (2 * OneStep), ParkCamcorderCameraMode.Stand.Y, 0.001f, "with the back key: standing" );

		Input.Forward = 0f;
		Input.Right = 1f;
		Frame( stack, true, View, 11.0f );
		Assert.AreEqual( Start.Y + (3 * OneStep), ParkCamcorderCameraMode.Stand.Y, 0.001f, "with a sideways key: forward as before" );
		Assert.AreEqual( Start.X + OneStep, ParkCamcorderCameraMode.Stand.X, 0.001f, "and aside as the key asks" );
	}

	/// <summary>
	/// <b>A press on the eject button does not walk</b>: the button takes the press, which never reaches the layer.
	/// </summary>
	/// <remarks><b>Mutations:</b> any held right button walking.</remarks>
	[TestMethod]
	public void APressOnTheEjectButtonDoesNotWalk()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, Eject, 10.0f );
		Frame( stack, true, Eject, 10.5f );
		Frame( stack, true, View, 11.0f );

		Assert.IsFalse( ParkCamcorderCameraMode.RightHeld );
		Assert.AreEqual( Start.Y, ParkCamcorderCameraMode.Stand.Y, "standing, over the button and taken off it" );
	}

	/// <summary>
	/// <b>It walks until the button is let go of, wherever the pointer goes</b>: the release goes to what took the press
	/// (<c>0x00658b5b</c>), so a pointer taken over the eject button while held still walks, and the release stops it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button read where the pointer is each frame; the release not read.</remarks>
	[TestMethod]
	public void ItWalksUntilTheButtonIsLetGoOf()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, View, 10.0f );
		Frame( stack, true, Eject, 10.5f );
		Assert.AreEqual( Start.Y + (2 * OneStep), ParkCamcorderCameraMode.Stand.Y, 0.001f, "over the eject button, still held: walking" );

		Frame( stack, false, Eject, 11.0f );
		Frame( stack, false, View, 11.5f );
		Assert.IsFalse( ParkCamcorderCameraMode.RightHeld );
		Assert.AreEqual( Start.Y + (2 * OneStep), ParkCamcorderCameraMode.Stand.Y, 0.001f, "let go: standing" );
	}

	/// <summary><b>Only a press the window system sent sets it</b>: a button already down when first seen is no press.</summary>
	/// <remarks><b>Mutations:</b> a button seen down taken as a press.</remarks>
	[TestMethod]
	public void AButtonAlreadyDownDoesNotWalk()
	{
		var stack = APark();
		InFirstPerson( true );

		Time.Now = 10f;
		Input.Mouse = new() { Right = true, Position = View };
		stack.Update();
		ParkCamcorderCameraMode.ReadRightButton();
		ParkCamcorderCameraMode.Walk();

		Assert.AreEqual( Start.Y, ParkCamcorderCameraMode.Stand.Y );
	}

	/// <summary>
	/// <b>A press made in orbit, on the park, and held into first person walks</b>: the park's own layer hands its
	/// messages to the same function (<c>0x00488290</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> only a press made in first person held.</remarks>
	[TestMethod]
	public void APressMadeInOrbitAndHeldIntoFirstPersonWalks()
	{
		var stack = APark();

		Frame( stack, true, View, 10.0f, walk: false );
		InFirstPerson( true );
		Frame( stack, true, View, 10.5f );

		Assert.AreEqual( Start.Y + OneStep, ParkCamcorderCameraMode.Stand.Y, 0.001f );
	}

	/// <summary>
	/// <b>With the HUD hidden the interface takes no press</b>: the stack is not run then, and what it said of the last
	/// press it saw, here one on the eject button with the HUD hidden while it was still held, is not read.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stack's last answer read with the HUD hidden.</remarks>
	[TestMethod]
	public void WithTheHudHiddenAPressIsTheViews()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, Eject, 10.0f );
		Assert.IsTrue( UI.WindowStack.RightPointerTaken, "the eject button took the last press the stack saw" );
		Assert.AreEqual( Start.Y, ParkCamcorderCameraMode.Stand.Y, "the eject button's press: standing" );

		UI.RootPanel.Hidden = true;
		HiddenFrame( false, 10.5f );
		HiddenFrame( true, 11.0f );

		Assert.AreEqual( Start.Y + OneStep, ParkCamcorderCameraMode.Stand.Y, 0.001f, "the same place with the HUD hidden: walking" );
	}

	/// <summary><b>Leaving a park lets go of it</b>, as it does of the rest of the walk.</summary>
	/// <remarks><b>Mutations:</b> the held button kept by <see cref="ParkCamcorderCameraMode.Forget"/>.</remarks>
	[TestMethod]
	public void LeavingTheParkLetsGoOfIt()
	{
		var stack = APark();
		InFirstPerson( true );

		Frame( stack, true, View, 10.0f );
		Assert.IsTrue( ParkCamcorderCameraMode.RightHeld );

		ParkCamcorderCameraMode.Forget();
		Assert.IsFalse( ParkCamcorderCameraMode.RightHeld );
	}

	/// <summary>A park's interface: the real front end over a real stack, its gadget and viewfinder up.</summary>
	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>
	/// One frame of the game: the right button and the pointer at a time, the stack's update, the camera's reading of the
	/// button, and its walk.
	/// </summary>
	private static void Frame( UI.WindowStack stack, bool right, Vector2 at, float now, bool walk = true )
	{
		Time.Now = now;
		Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = at };
		stack.Update();
		ParkCamcorderCameraMode.ReadRightButton();

		if ( walk )
			ParkCamcorderCameraMode.Walk();
	}

	/// <summary>One frame with the HUD hidden: the stack is not run, the camera reads the button and walks.</summary>
	private static void HiddenFrame( bool right, float now )
	{
		Time.Now = now;
		Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = Eject };
		ParkCamcorderCameraMode.ReadRightButton();
		ParkCamcorderCameraMode.Walk();
	}

	/// <summary>Puts first person up or down, as entering and leaving it do, without a camera to move.</summary>
	private static void InFirstPerson( bool active )
		=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, active );
}
