using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// First person's keys turn and walk as the original's (<c>docs/exe/park-engine.md</c>, "The walk's keys, turn and
/// speed"): Left and keypad 9, Right and keypad 7 turn at 1.2 radians a second; Up, Down and keypads 1 and 3 walk; the
/// pointer turns 2.4 radians a second a unit past 0.4; the walk's term settles at <c>sqrt( zoom ) x 2</c> a second,
/// halving its gap every 31 ms here (rule 10), and is the pan's term, so a pan carries into the walk.
///
/// <para>
/// The level is a stand-in park with no ground, no save and no windows, as <see cref="ParkCamcorderLeaveTests"/>' is,
/// so no cell edge stops a step and every step is taken whole.
/// </para>
/// </summary>
[TestClass]
public class ParkCamcorderKeysTests
{
	private Level? _level;
	private Vector3 _orbitLooksAt;
	private float _orbitYaw;
	private float _zoom;
	private Vector2 _scroll;
	private float _delta;
	private Point2 _screen;
	private bool _textCaptured;
	private readonly Dictionary<string, bool> _gates = [];

	/// <summary>The window stack's gates on the camera's keys, which an earlier test's windows can leave set.</summary>
	private static readonly string[] Gates = ["ParkScreenOpen", "MapOpen", "CameraKeysAway"];

	private static void Gate( string name, bool value )
		=> typeof( UI.WindowStack ).GetProperty( name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!
			.SetValue( null, value );

	private static bool GateOf( string name )
		=> (bool)typeof( UI.WindowStack ).GetProperty( name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!
			.GetValue( null )!;

	private static readonly Vector3 Start = new( 500f, 500f, 0f );

	/// <summary>What a held walk key settles at, at zoom 110: <c>sqrt( 110 ) x 2</c>.</summary>
	private const float AtZoom110 = 20.9762f;

	[TestInitialize]
	public void StandInAPark()
	{
		Log ??= new();

		_level = Level.Current;
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		_orbitYaw = ParkOrbitCameraMode.Yaw;
		_zoom = ParkOrbitCameraMode.Zoom;
		_scroll = ParkOrbitCameraMode.Scroll;
		_delta = Time.Delta;
		_screen = Screen.Size;
		_textCaptured = Input.TextCaptured;

		foreach ( var gate in Gates )
		{
			_gates[gate] = GateOf( gate );
			Gate( gate, false );
		}

		Input.TextCaptured = false;

		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		Level.Current = park;

		ParkOrbitCameraMode.Zoom = 110f;
		ParkOrbitCameraMode.Scroll = Vector2.Zero;
		ParkCamcorderCameraMode.Forget();
		ParkCamcorderCameraMode.Stand = Start;
		Input.Forward = 0f;
		Input.Right = 0f;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkCamcorderCameraMode.Forget();
		Camera.SetCameraMode<CameraMode>();
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		ParkOrbitCameraMode.Zoom = _zoom;
		ParkOrbitCameraMode.Scroll = _scroll;
		Time.Delta = _delta;
		Screen.Size = _screen;
		Input.TextCaptured = _textCaptured;

		foreach ( var (gate, value) in _gates )
			Gate( gate, value );

		Input.Forward = 0f;
		Input.Right = 0f;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Level.Current = _level;
	}

	/// <summary>
	/// <b>The keys are the original's rows</b>: Left and keypad 9 turn left, Right and keypad 7 right, Up and keypad 1
	/// walk forward, Down and keypad 3 back, none of them with a modifier held; W, A, S and D are not among them.
	/// </summary>
	/// <remarks><b>Mutations:</b> the turn's sign swapped; keypad 9 and 7 read the other way round; a modifier ignored.</remarks>
	[TestMethod]
	public void TheKeysAreTheOriginalsRows()
	{
		var cases = new (Key[] Down, float Forward, float Turn)[]
		{
			([Key.Left], 0f, 1f),
			([Key.Keypad9], 0f, 1f),
			([Key.Right], 0f, -1f),
			([Key.Keypad7], 0f, -1f),
			([Key.Up], 1f, 0f),
			([Key.Keypad1], 1f, 0f),
			([Key.Down], -1f, 0f),
			([Key.Keypad3], -1f, 0f),
			([Key.Up, Key.Left], 1f, 1f),
			([Key.Left, Key.Right], 0f, 0f),
			([Key.ControlLeft, Key.Left], 0f, 0f),
			([Key.ShiftLeft, Key.Up], 0f, 0f),
			([Key.W, Key.A, Key.S, Key.D], 0f, 0f),
		};

		foreach ( var (down, forward, turn) in cases )
			Assert.AreEqual( (forward, turn), ParkCamcorderCameraMode.KeyInput( down ), string.Join( "+", down ) );
	}

	/// <summary>
	/// <b>A held turn key turns 1.2 radians a second</b>, Left raising the yaw (ours is the original's rotation negated),
	/// whatever the frame rate: a second of sixtieths and a second of halves turn alike.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pointer's 2.4 or the orbit's 2.0 for the rate; a per-frame step.</remarks>
	[TestMethod]
	public void AHeldTurnKeyTurnsOnePointTwoRadiansASecond()
	{
		ParkCamcorderCameraMode.DebugKeys( 0f, 1f, 60 );
		Assert.AreEqual( 1.2f, ParkCamcorderCameraMode.Yaw, 0.0001f, "Left a second at sixty frames" );

		ParkCamcorderCameraMode.DebugKeys( 0f, -1f, 30 );
		Assert.AreEqual( 0.6f, ParkCamcorderCameraMode.Yaw, 0.0001f, "Right half a second back" );

		ParkCamcorderCameraMode.Yaw = 0f;
		Time.Delta = 0.5f;
		Input.Keyboard = new Input.KeyboardInfo( [Key.Left] );
		ParkCamcorderCameraMode.Walk();
		ParkCamcorderCameraMode.Walk();
		Assert.AreEqual( 1.2f, ParkCamcorderCameraMode.Yaw, 0.0001f, "the real key, two frames of half a second" );
		Assert.AreEqual( Start, ParkCamcorderCameraMode.Stand, "a turn walks nowhere" );
	}

	/// <summary>
	/// <b>A and D turn as Left and Right do and walk nobody aside</b> (ours, as W and S walk too): A raises the yaw 1.2
	/// radians a second, D lowers it, the stand stays put, and A held with Left turns at the one rate.
	/// </summary>
	/// <remarks><b>Mutations:</b> A and D left doing nothing; their sign swapped; the strafe put back (<see cref="Input.Right"/>
	/// passed as the across term); A and Left added to twice the rate.</remarks>
	[TestMethod]
	public void AAndDTurnAndWalkNobodyAside()
	{
		Time.Delta = 0.5f;

		Input.Right = -1f;
		ParkCamcorderCameraMode.Walk();
		Assert.AreEqual( 0.6f, ParkCamcorderCameraMode.Yaw, 0.0001f, "A half a second: left" );

		Input.Right = 1f;
		ParkCamcorderCameraMode.Walk();
		ParkCamcorderCameraMode.Walk();
		Assert.AreEqual( -0.6f, ParkCamcorderCameraMode.Yaw, 0.0001f, "D a second: right" );
		Assert.AreEqual( Start, ParkCamcorderCameraMode.Stand, "a turn walks nowhere, and nothing aside" );
		Assert.AreEqual( Vector2.Zero, ParkOrbitCameraMode.Scroll, "no term" );

		Input.Right = -1f;
		Input.Keyboard = new Input.KeyboardInfo( [Key.Left] );
		ParkCamcorderCameraMode.Walk();
		Assert.AreEqual( 0f, ParkCamcorderCameraMode.Yaw, 0.0001f, "A and Left together: the one rate" );
	}

	/// <summary>
	/// <b>Up and keypad 1 walk through <see cref="ParkCamcorderCameraMode.Walk"/></b>, the real keys and not the console's
	/// stand-in: half a second held walks the settled speed's half, 10.488 at zoom 110.
	/// </summary>
	/// <remarks><b>Mutations:</b> the arrow's amount left out of the walk's forward term.</remarks>
	[TestMethod]
	public void UpAndKeypadOneWalkThroughTheRealKeys()
	{
		Time.Delta = 0.5f;

		foreach ( var key in new[] { Key.Up, Key.Keypad1 } )
		{
			ParkCamcorderCameraMode.Stand = Start;
			ParkOrbitCameraMode.Scroll = Vector2.Zero;
			Input.Keyboard = new Input.KeyboardInfo( [key] );
			ParkCamcorderCameraMode.Walk();

			Assert.AreEqual( Start.Y + (AtZoom110 * 0.5f), ParkCamcorderCameraMode.Stand.Y, 0.002f, $"{key} half a second" );
		}
	}

	/// <summary>
	/// <b>With the camera's table off the keys do nothing</b>: a park screen up, Left and Up neither turn nor walk, as the
	/// orbit's keys (<see cref="ParkOrbitCameraMode.CameraTableOn"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the table's gate dropped from <see cref="ParkCamcorderCameraMode.Walk"/>.</remarks>
	[TestMethod]
	public void WithTheTableOffTheKeysDoNothing()
	{
		Gate( "ParkScreenOpen", true );
		Time.Delta = 0.5f;
		Input.Keyboard = new Input.KeyboardInfo( [Key.Left, Key.Up] );

		ParkCamcorderCameraMode.Walk();

		Assert.AreEqual( 0f, ParkCamcorderCameraMode.Yaw, "no turn" );
		Assert.AreEqual( Start, ParkCamcorderCameraMode.Stand, "no walk" );
	}

	/// <summary>
	/// <b>A walk near a map axis keeps its small axis</b>: the term is noughted only when both axes are under 0.1 a second,
	/// as the original's (<c>0x0042bd69</c>..<c>0x0042bdc3</c>), so at a yaw of 0.002 the walk drifts 0.021 aside in half a
	/// second rather than running along the axis.
	/// </summary>
	/// <remarks><b>Mutations:</b> each axis noughted on its own (the pan's <see cref="ParkOrbitCameraMode.Floored"/>).</remarks>
	[TestMethod]
	public void AWalkNearAMapAxisKeepsItsSmallAxis()
	{
		ParkCamcorderCameraMode.Yaw = 0.002f;
		ParkCamcorderCameraMode.Move( 1f, 0f, 0f, 0.5f, 1f );

		Assert.AreEqual( -MathF.Sin( 0.002f ) * AtZoom110, ParkOrbitCameraMode.Scroll.X, 0.0001f, "the small axis kept" );
		Assert.AreEqual( Start.X - (MathF.Sin( 0.002f ) * AtZoom110 * 0.5f), ParkCamcorderCameraMode.Stand.X, 0.0005f, "and walked" );
	}

	/// <summary>
	/// <b>A held walk key settles at <c>sqrt( zoom ) x 2</c> a second</b>, 20.976 at zoom 110 and 16.733 at 70, rising
	/// from rest: a second held from standing walks 20.20 units, not the whole 20.98.
	/// </summary>
	/// <remarks><b>Mutations:</b> the old 40 a second; the pan's <c>sqrt( zoom ) x 10</c>.</remarks>
	[TestMethod]
	public void AHeldWalkKeySettlesAtTheZoomsSpeed()
	{
		ParkCamcorderCameraMode.DebugKeys( 1f, 0f, 60 );

		Assert.AreEqual( AtZoom110, ParkOrbitCameraMode.Scroll.Y, 0.001f, "the term at zoom 110" );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.Scroll.X, "facing +Y" );
		Assert.AreEqual( Start.Y + 20.202f, ParkCamcorderCameraMode.Stand.Y, 0.002f, "a second from standing" );

		ParkOrbitCameraMode.Zoom = 70f;
		ParkCamcorderCameraMode.DebugKeys( -1f, 0f, 120 );
		Assert.AreEqual( -16.7332f, ParkOrbitCameraMode.Scroll.Y, 0.001f, "back, at zoom 70" );
	}

	/// <summary>
	/// <b>The term halves its gap every 31 ms, whatever the frame</b>: one frame of 31 ms and two of 15.5 both rise from
	/// rest to half the settled speed.
	/// </summary>
	/// <remarks><b>Mutations:</b> the original's half a frame with no time (two frames reach three quarters).</remarks>
	[TestMethod]
	public void TheRiseIsTheSameAtAnyFrameRate()
	{
		Input.Forward = 1f;

		Time.Delta = 0.031f;
		ParkCamcorderCameraMode.Walk();
		var once = ParkOrbitCameraMode.Scroll.Y;

		ParkOrbitCameraMode.Scroll = Vector2.Zero;
		Time.Delta = 0.0155f;
		ParkCamcorderCameraMode.Walk();
		ParkCamcorderCameraMode.Walk();
		var twice = ParkOrbitCameraMode.Scroll.Y;

		Assert.AreEqual( AtZoom110 / 2f, once, 0.01f, "one frame of 31 ms" );
		Assert.AreEqual( AtZoom110 / 2f, twice, 0.01f, "two frames of 15.5 ms" );
	}

	/// <summary>
	/// <b>The walk's term is the pan's</b>: entering keeps a pan's term and the walk carries on with it, halving to a stand
	/// with no key held, and leaving keeps it for the orbit.
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="ParkCamcorderCameraMode.Enter"/> or <c>Leave</c> noughting the term; a term
	/// of its own for the walk; no key stopping the walk at once.</remarks>
	[TestMethod]
	public void APanCarriesIntoTheWalkAndOut()
	{
		Camera.SetCameraMode<ParkOrbitCameraMode>();
		ParkOrbitCameraMode.PointOfInterest = Start;
		ParkOrbitCameraMode.Yaw = 0f;
		ParkOrbitCameraMode.Scroll = new Vector2( 0f, 50f );

		ParkCamcorderCameraMode.Enter();
		Assert.AreEqual( new Vector2( 0f, 50f ), ParkOrbitCameraMode.Scroll, "entering keeps the pan's term" );

		Time.Delta = 0.031f;
		ParkCamcorderCameraMode.Walk();
		Assert.AreEqual( 25f, ParkOrbitCameraMode.Scroll.Y, 0.01f, "no key: half in 31 ms" );
		Assert.AreEqual( Start.Y + (25f * 0.031f), ParkCamcorderCameraMode.Stand.Y, 0.001f, "and walked by it" );

		ParkCamcorderCameraMode.Leave();
		Assert.AreEqual( 25f, ParkOrbitCameraMode.Scroll.Y, 0.01f, "leaving keeps it for the orbit" );

		ParkOrbitCameraMode.Scroll = new Vector2( 0f, 50f );
		ParkCamcorderCameraMode.Enter();
		Time.Delta = 0.5f;
		ParkCamcorderCameraMode.Walk();
		Assert.AreEqual( Vector2.Zero, ParkOrbitCameraMode.Scroll, "under 0.1 a second it is nought" );
	}

	/// <summary>
	/// <b>The turn comes before the walk</b>, as the original's (<c>0x0042b417</c> before <c>0x0042bad9</c>..<c>0x0042bb27</c>): one frame of
	/// both held walks along the heading the frame turned to.
	/// </summary>
	/// <remarks><b>Mutations:</b> the term turned by the yaw before the key's turn (the step straight along +Y).</remarks>
	[TestMethod]
	public void TheTurnComesBeforeTheWalk()
	{
		ParkCamcorderCameraMode.Move( 1f, 0f, 1f, 0.5f, 1f );

		Assert.AreEqual( 0.6f, ParkCamcorderCameraMode.Yaw, 0.0001f );
		Assert.AreEqual( Start.X - (MathF.Sin( 0.6f ) * AtZoom110 * 0.5f), ParkCamcorderCameraMode.Stand.X, 0.001f, "along the new heading" );
		Assert.AreEqual( Start.Y + (MathF.Cos( 0.6f ) * AtZoom110 * 0.5f), ParkCamcorderCameraMode.Stand.Y, 0.001f );
	}

	/// <summary>
	/// <b>The pointer turns 2.4 radians a second a unit past 0.4</b>: at 0.6 across, half a second turns 0.24 towards it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the old 2.0 (0.20 turned); the 3.5 of the other branch (0.35).</remarks>
	[TestMethod]
	public void ThePointerTurnsTwoPointFourRadiansASecondAUnitPastTheBand()
	{
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new() { Position = new Vector2( 2048f * 0.8f, 768f ) };
		Time.Delta = 0.5f;

		ParkCamcorderCameraMode.Steer();

		Assert.AreEqual( -0.24f, ParkCamcorderCameraMode.Yaw, 0.0001f );
	}
}
