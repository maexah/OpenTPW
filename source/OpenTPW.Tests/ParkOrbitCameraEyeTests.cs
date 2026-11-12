using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// Where the park's orbit camera stands - <see cref="ParkOrbitCameraMode.EyeAt"/> - against the eye read in the running
/// original (its camera matrix <c>0x0087aff0</c>, Q435; <c>docs/exe/park-engine.md</c>, "The park camera"). No game files
/// needed.
/// </summary>
[TestClass]
public class ParkOrbitCameraEyeTests
{
	/// <summary>Zoom drives pitch: 45 degrees pulled in, 65 pushed out, 58.33 at the 110 the original starts at.</summary>
	[TestMethod]
	public void ZoomDrivesThePitch()
	{
		Assert.AreEqual( 45f, ParkOrbitCameraMode.PitchAt( 70f ), 1e-4f, "pulled in" );
		Assert.AreEqual( 65f, ParkOrbitCameraMode.PitchAt( 130f ), 1e-4f, "pushed out" );
		Assert.AreEqual( 58.3333f, ParkOrbitCameraMode.PitchAt( 110f ), 1e-3f, "the original's start" );
	}

	/// <summary>
	/// The original at point (484.442, 259.319), zoom 110, rotation nought, had its eye at (484.44, 93.62, 201.57) in its
	/// own axes, Y up: here (484.44, 201.57, 93.62), Z up.
	/// </summary>
	[TestMethod]
	public void TheEyeIsTheOriginalsAtItsOwnNumbers()
	{
		var eye = ParkOrbitCameraMode.EyeAt( new Vector3( 484.442f, 259.319f, 0f ), 0f, 110f, 0f );

		Assert.AreEqual( 484.442f, eye.X, 0.01f, "straight behind the point" );
		Assert.AreEqual( 201.572f, eye.Y, 0.01f, "57.75 back from it, on the near side" );
		Assert.AreEqual( 93.623f, eye.Z, 0.01f, "93.62 up" );
	}

	/// <summary>
	/// The original turned to 90 and 180 at point (495, 285), zoom 110, stood its eye at (437.253, 93.623, 285.000) and
	/// (495.000, 93.623, 342.747): on the point less (h sin r, h cos r). This yaw swings the other way, so the same eye is
	/// the rotation negated (Q440, <c>lib/bothcam.py</c>).
	/// </summary>
	[TestMethod]
	public void TheOriginalsRotationIsThisYawNegated()
	{
		var point = new Vector3( 495f, 285f, 0f );
		var ninety = ParkOrbitCameraMode.EyeAt( point, -MathF.PI / 2f, 110f, 0f );
		var half = ParkOrbitCameraMode.EyeAt( point, -MathF.PI, 110f, 0f );

		Assert.AreEqual( 437.253f, ninety.X, 0.01f, "at 90, 57.75 less in x than the point" );
		Assert.AreEqual( 285f, ninety.Y, 0.01f, "at 90, level with it" );
		Assert.AreEqual( 495f, half.X, 0.01f, "at 180, level with it" );
		Assert.AreEqual( 342.747f, half.Y, 0.01f, "at 180, 57.75 beyond it" );
	}

	/// <summary>The eased ground under the point raises the eye by as much (the original's <c>0x00790aa4</c>).</summary>
	[TestMethod]
	public void TheGroundUnderThePointRaisesTheEye()
	{
		var flat = ParkOrbitCameraMode.EyeAt( new Vector3( 484.442f, 259.319f, 0f ), 0f, 110f, 0f );
		var raised = ParkOrbitCameraMode.EyeAt( new Vector3( 484.442f, 259.319f, 0f ), 0f, 110f, 12f );

		Assert.AreEqual( flat.Z + 12f, raised.Z, 0.01f, "twelve higher" );
		Assert.AreEqual( flat.Y, raised.Y, 0.01f, "no nearer or further" );
	}

	/// <summary>
	/// The original at point (595, 355), zoom 70, rotation 180, over ground 2.6265 (Q1106, its camera matrix
	/// <c>0x0087aff0</c>): the eye (595, 52.124, 404.497) and the forward (0, -0.7071, -0.7071) in its own axes, the zoom's
	/// 45 degrees, so its draw matrix put the point lifted by the ground, (595, 2.6265, 355), on the frame's middle. Looking
	/// at the point itself from that eye would be 46.47 degrees down, a forward height of -0.7249.
	/// </summary>
	[TestMethod]
	public void OverRaisedGroundTheAimRisesWithTheEye()
	{
		var (eye, aim) = ParkOrbitCameraMode.View( new Vector3( 595f, 355f, 0f ), -MathF.PI, 70f, 2.6265f );
		var look = aim.Normal;

		Assert.AreEqual( 404.497f, eye.Y, 0.01f, "49.50 beyond the point" );
		Assert.AreEqual( 52.124f, eye.Z, 0.01f, "49.50 up, plus the ground" );
		Assert.AreEqual( 0f, look.X, 1e-4f, "straight along" );
		Assert.AreEqual( -0.7071f, look.Y, 1e-4f, "towards the point" );
		Assert.AreEqual( -0.7071f, look.Z, 1e-4f, "the zoom's 45 degrees down, not 46.47" );

		var middle = eye + (look * 70f);

		Assert.AreEqual( 595f, middle.X, 0.001f, "the frame's middle, across" );
		Assert.AreEqual( 355f, middle.Y, 0.001f, "the frame's middle, along" );
		Assert.AreEqual( 2.6265f, middle.Z, 0.001f, "the point lifted by the ground" );
	}

	/// <summary>
	/// The camera itself, placed over the same ground at the same numbers, faces that way: what the frame is drawn from
	/// (<see cref="Camera"/> builds its view from the mode's forward).
	/// </summary>
	[TestMethod]
	public void ThePlacedCameraFacesTheZoomsPitchOverRaisedGround()
	{
		var (point, yaw, zoom) = (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom);

		try
		{
			ParkOrbitCameraMode.PointOfInterest = new Vector3( 595f, 355f, 0f );
			ParkOrbitCameraMode.Yaw = -MathF.PI;
			ParkOrbitCameraMode.Zoom = 70f;

			var camera = new ParkOrbitCameraMode();
			camera.Place( 2.6265f );
			var forward = camera.Rotation.Forward;

			Assert.AreEqual( 52.124f, camera.Position.Z, 0.01f, "the eye on the ground" );
			Assert.AreEqual( -0.7071f, forward.Y, 1e-3f, "towards the point" );
			Assert.AreEqual( -0.7071f, forward.Z, 1e-3f, "45 degrees down, not the 46.47 that looks at height nought" );
		}
		finally
		{
			(ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom) = (point, yaw, zoom);
		}
	}

	/// <summary>
	/// On ground at height nought the look runs through the point itself: the original at point (484.442, 259.319), zoom
	/// 110, rotation nought, read its forward (0, -0.851, 0.525) through the point (Q435), here (0, 0.525, -0.851); and at 90.
	/// </summary>
	[TestMethod]
	public void OnGroundAtNoughtTheAimRunsThroughThePoint()
	{
		var point = new Vector3( 484.442f, 259.319f, 0f );
		var (eye, aim) = ParkOrbitCameraMode.View( point, 0f, 110f, 0f );
		var look = aim.Normal;

		Assert.AreEqual( 0.525f, look.Y, 0.001f, "towards the point" );
		Assert.AreEqual( -0.851f, look.Z, 0.001f, "the zoom's 58.33 degrees down" );
		Assert.AreEqual( 0f, Vector3.DistanceBetween( point, eye + (look * 110f) ), 0.001f, "the point is the frame's middle" );

		// Turned to the original's 90, the eye stands 57.75 less in x than the point (Q440), so the look runs along +x.
		var (turnedEye, turnedAim) = ParkOrbitCameraMode.View( point, -MathF.PI / 2f, 110f, 0f );
		var turned = turnedAim.Normal;

		Assert.AreEqual( 0.525f, turned.X, 0.001f, "at 90, towards the point along x" );
		Assert.AreEqual( 0f, Vector3.DistanceBetween( point, turnedEye + (turned * 110f) ), 0.001f, "at 90 the point is the frame's middle too" );
	}
}
