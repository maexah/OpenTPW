using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Leaving first person puts the orbit camera back where it was as first person was entered, its point of interest and
/// its yaw, and throws the walk away: the original saves both on entry (<c>0x0042af48</c>..<c>0x0042af6a</c>) and puts
/// them back on leaving (<c>0x0042af8c</c>..<c>0x0042afb4</c>; <c>docs/exe/park-engine.md</c>, "Entering and leaving
/// first person").
///
/// <para>
/// The level is a stand-in park with no ground and no windows, as <see cref="ParkCamcorderKeyOnReleaseTests"/>' is.
/// The walk is written straight into <see cref="ParkCamcorderCameraMode.Stand"/> and
/// <see cref="ParkCamcorderCameraMode.Yaw"/>, where the pointer and the keys would put it; the headings are no multiple
/// of 45 degrees, which is what a turn on the ground leaves.
/// </para>
/// </summary>
[TestClass]
public class ParkCamcorderLeaveTests
{
	private Level? _level;
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
	}

	/// <summary>
	/// <b>A walk and a turn on the ground are thrown away on leaving</b>: the orbit looks at the point it looked at
	/// before, from the yaw it had, and is an orbit again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <see cref="ParkCamcorderCameraMode.Leave"/> handing back the walk's position or heading, as it
	/// did before Q626, fails one assert each, and putting back nothing leaves the point written meanwhile;
	/// <see cref="ParkCamcorderCameraMode.Enter"/> not saving them leaves nought.
	/// </remarks>
	[TestMethod]
	public void LeavingPutsBackThePointAndYawOfTheEntry()
	{
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 437.5f, 262.25f, 0f );
		ParkOrbitCameraMode.Yaw = 3f * MathF.PI / 4f;

		ParkCamcorderCameraMode.Enter();
		Assert.IsTrue( ParkCamcorderCameraMode.Active );
		Assert.AreEqual( 437.5f, ParkCamcorderCameraMode.Stand.X, "the control: the viewer stands at the orbit's point" );

		Walk( new Vector3( 512.75f, 301.5f, 0f ), 1.2345f );

		// The orbit's own point written while first person is up, as the console's `camera` does: the original's walk
		// moves that very point (0x007908f0), so what leaving puts back is the copy saved on entry.
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 10f, 20f, 0f );
		ParkOrbitCameraMode.Yaw = 0.3f;

		ParkCamcorderCameraMode.Leave();

		Assert.IsFalse( ParkCamcorderCameraMode.Active );
		Assert.AreEqual( new Vector3( 437.5f, 262.25f, 0f ), ParkOrbitCameraMode.PointOfInterest, "the entry's point" );
		Assert.AreEqual( 3f * MathF.PI / 4f, ParkOrbitCameraMode.Yaw, "the entry's yaw, not the walk's 1.2345" );
	}

	/// <summary>
	/// <b>Each entry saves afresh</b>: the orbit moved between two visits to first person comes back to where it was
	/// at the second entry, not the first.
	/// </summary>
	/// <remarks><b>Mutations:</b> saving only while nothing is saved yet puts the second leave back at the first entry.</remarks>
	[TestMethod]
	public void EachEntrySavesWhereTheOrbitIsThen()
	{
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 100f, 200f, 0f );
		ParkOrbitCameraMode.Yaw = 0f;

		ParkCamcorderCameraMode.Enter();
		Walk( new Vector3( 150f, 220f, 0f ), -0.5f );
		ParkCamcorderCameraMode.Leave();

		ParkOrbitCameraMode.PointOfInterest = new Vector3( 640f, 330f, 0f );
		ParkOrbitCameraMode.Yaw = -MathF.PI / 2f;

		ParkCamcorderCameraMode.Enter();
		Walk( new Vector3( 700f, 360f, 0f ), 2.5f );
		ParkCamcorderCameraMode.Leave();

		Assert.AreEqual( new Vector3( 640f, 330f, 0f ), ParkOrbitCameraMode.PointOfInterest, "the second entry's point" );
		Assert.AreEqual( -MathF.PI / 2f, ParkOrbitCameraMode.Yaw, "the second entry's yaw" );
	}

	/// <summary>
	/// <b>A park left lets the saved view go</b> with the rest of the ground-level state (<see cref="Level.ForgetPark"/>).
	/// </summary>
	/// <remarks>Hygiene, not the restore: nothing reads the saved view after a forget, because every caller of
	/// <c>Leave</c> is guarded by <c>Active</c> and <c>Enter</c> saves it afresh, so the two mutations below change no
	/// behaviour. <b>Mutations:</b> <see cref="ParkCamcorderCameraMode.Forget"/> keeping either saved value.</remarks>
	[TestMethod]
	public void ForgettingTheParkForgetsTheSavedView()
	{
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 437.5f, 262.25f, 0f );
		ParkOrbitCameraMode.Yaw = 1f;

		ParkCamcorderCameraMode.Enter();
		ParkCamcorderCameraMode.Forget();

		Assert.AreEqual( Vector3.Zero, ParkCamcorderCameraMode.SavedPointOfInterest );
		Assert.AreEqual( 0f, ParkCamcorderCameraMode.SavedYaw );
	}

	/// <summary>Where a walk on the ground would leave the viewer, and the way a turn would leave them facing.</summary>
	private static void Walk( Vector3 to, float yaw )
	{
		ParkCamcorderCameraMode.StandAt( to );
		ParkCamcorderCameraMode.Yaw = yaw;
	}
}
