using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The camcorder key acts on its release, both ways, as the original's does: C is the shortcuts table's row 16
/// (<c>0x0040c5c0</c>), run on a key-up (<c>FUN_0040c990</c>), and first person is left on a key-up whose action is
/// camcorder (<c>0x00488a00</c>). The key-down only latches the row (<c>FUN_0040c900</c>). See
/// <see cref="Input.KeyUp"/>.
///
/// <para>
/// <b>The frame is the game's own</b>: SDL's key events through <see cref="Input.UpdateFrom"/>, then
/// <see cref="Camera.Update"/>, which runs the real camera mode's <c>Update</c>. The level is a stand-in park with no
/// ground and no windows.
/// </para>
/// </summary>
[TestClass]
public class ParkCamcorderKeyOnReleaseTests
{
	private Level _level = null!;
	private Vector3 _orbitLooksAt;
	private float _orbitYaw;

	[TestInitialize]
	public void StartInOrbit()
	{
		Log ??= new();

		_level = Level.Current;
		_orbitLooksAt = ParkOrbitCameraMode.PointOfInterest;
		_orbitYaw = ParkOrbitCameraMode.Yaw;

		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		Level.Current = park;

		Input.ForgetHeldKeys();
		Input.TextCaptured = false;
		Camera.SetCameraMode<ParkOrbitCameraMode>();
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkCamcorderCameraMode.Forget();
		Camera.SetCameraMode<CameraMode>();
		ParkOrbitCameraMode.PointOfInterest = _orbitLooksAt;
		ParkOrbitCameraMode.Yaw = _orbitYaw;
		Level.Current = _level;
		Input.ForgetHeldKeys();
		Input.TextCaptured = false;
	}

	/// <summary>
	/// <b>A held C does nothing, and each release does one thing</b>: the first goes down into first person, the next
	/// comes back up.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> either camera mode reading <see cref="Input.Pressed"/> again moves the camera while the key is
	/// held; the other mode's site is the other half of the test.
	/// </remarks>
	[TestMethod]
	public void AHeldCDoesNothingUntilItComesUp()
	{
		HoldC();
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "held in orbit, with its repeats: still in orbit" );

		Frame( Up( Key.C ) );
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "let go: down in first person" );

		HoldC();
		Assert.IsTrue( ParkCamcorderCameraMode.Active, "held in first person: still down" );

		Frame( Up( Key.C ) );
		Assert.IsFalse( ParkCamcorderCameraMode.Active, "let go: back up" );
	}

	/// <summary>
	/// <b>A modifier under the held key is no release</b>, and C let go with Ctrl held is Close Park's row, not the
	/// camcorder's - both ways. The original finds the row by the key let go and the modifiers together.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <see cref="Input.KeyUp"/> as the binding's release edge (<see cref="Input.Released"/>) moves
	/// the camera as Ctrl goes down; taking out its modifier test moves it on Ctrl+C.
	/// </remarks>
	[TestMethod]
	public void CLetGoWithCtrlHeldIsNotTheCamcorder()
	{
		for ( var down = 0; down < 2; ++down )
		{
			var where = down == 0 ? "in orbit" : "in first person";

			Frame( Down( Key.C ) );
			Frame( Down( Key.ControlLeft ) );
			Assert.AreEqual( down == 1, ParkCamcorderCameraMode.Active, $"{where}: Ctrl under the held C moves nothing" );

			Frame( Up( Key.C ) );
			Assert.AreEqual( down == 1, ParkCamcorderCameraMode.Active, $"{where}: Ctrl+C let go moves nothing" );

			Frame( Up( Key.ControlLeft ) );
			Frame( Down( Key.C ) );
			Frame( Up( Key.C ) );
			Assert.AreEqual( down == 0, ParkCamcorderCameraMode.Active, $"{where}: a plain C let go moves it" );
		}
	}

	/// <summary>Holds C down for three frames, the last two its repeats, without letting it go.</summary>
	private static void HoldC()
	{
		for ( var frame = 0; frame < 3; ++frame )
			Frame( Down( Key.C ) );
	}

	/// <summary>One frame of the game with these key events: the input, then the camera.</summary>
	private static void Frame( params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys ) );
		Camera.Update();
	}

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
}
