using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What the mouse wheel does to the park's zoom - <see cref="ParkOrbitCameraMode.ZoomAfterWheel"/> - against the original's
/// numbers (Q937, <c>docs/exe/hud.md</c>, "The wheel over a modal screen"): a notch down 110, 120, 130 at the clamp; five up
/// from 130 to 80. No game files needed.
/// </summary>
[TestClass]
public class ParkOrbitCameraWheelTests
{
	/// <remarks><b>Mutations:</b> the step put back to 5 (a notch down gives 115, 120, 125); the sign turned (a notch down zooms in).</remarks>
	[TestMethod]
	public void ANotchTowardThePlayerZoomsOutByTenToTheClamp()
	{
		var zoom = 110f;
		zoom = ParkOrbitCameraMode.ZoomAfterWheel( zoom, -1f ); Assert.AreEqual( 120f, zoom, "one notch down, 110 to 120" );
		zoom = ParkOrbitCameraMode.ZoomAfterWheel( zoom, -1f ); Assert.AreEqual( 130f, zoom, "two, at the clamp" );
		zoom = ParkOrbitCameraMode.ZoomAfterWheel( zoom, -1f ); Assert.AreEqual( 130f, zoom, "three, held at 130" );
	}

	/// <remarks><b>Mutations:</b> the step put back to 5 (five notches up give 105); the clamp's floor dropped (the last up goes to 70 - 10).</remarks>
	[TestMethod]
	public void FiveNotchesAwayFromThePlayerZoomInFromTheClampToEighty()
	{
		var zoom = 130f;
		for ( var i = 0; i < 5; i++ )
			zoom = ParkOrbitCameraMode.ZoomAfterWheel( zoom, 1f );

		Assert.AreEqual( 80f, zoom, "five notches up from 130" );
		Assert.AreEqual( 70f, ParkOrbitCameraMode.ZoomAfterWheel( 75f, 1f ), "held at 70, not 65" );
	}

	/// <summary>A frame that carries two notches moves it by two steps, as two messages would.</summary>
	/// <remarks><b>Mutations:</b> the notches' count ignored (a frame of two moves it by one step).</remarks>
	[TestMethod]
	public void SeveralNotchesInAFrameAreSeveralSteps()
	{
		Assert.AreEqual( 90f, ParkOrbitCameraMode.ZoomAfterWheel( 110f, 2f ), "two up in one frame" );
		Assert.AreEqual( 125f, ParkOrbitCameraMode.ZoomAfterWheel( 110f, -1.5f ), "a touchpad's half step is ours: half of 10" );
	}
}
