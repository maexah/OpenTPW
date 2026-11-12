using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// The orbit's lean while it pans - <see cref="ParkOrbitCameraMode.LeanAt"/> and <see cref="ParkOrbitCameraMode.Leaned"/>
/// - against the original's turn by its x scroll term about its world Z (<c>0x0042c822</c>, <c>0x0042cc1d</c>;
/// <c>docs/exe/park-engine.md</c>, "The park camera"). No game files needed.
/// </summary>
[TestClass]
public class ParkOrbitCameraLeanTests
{
	/// <summary>The term a pan held at zoom 110 settles at, a second: the original's 0.10488 a millisecond.</summary>
	private static readonly float Held110 = MathF.Sqrt( 110f ) * 10f;

	/// <summary>
	/// <c>0.005 sqrt( 100 |t| ) - 0.001</c> of the term in units a millisecond, signed as the term: 0.015193 held at 110,
	/// 0.013463 at 70, 0.000581 at 1 a second; nought under 0.4 a second, where the original's clamp takes it.
	/// </summary>
	[TestMethod]
	public void TheLeanIsTheOriginalsOfTheXTerm()
	{
		Assert.AreEqual( 0.015193f, ParkOrbitCameraMode.LeanAt( Held110 ), 2e-6f, "held at zoom 110" );
		Assert.AreEqual( -0.015193f, ParkOrbitCameraMode.LeanAt( -Held110 ), 2e-6f, "held the other way" );
		Assert.AreEqual( 0.013463f, ParkOrbitCameraMode.LeanAt( MathF.Sqrt( 70f ) * 10f ), 2e-6f, "held at zoom 70" );
		Assert.AreEqual( 0.000581f, ParkOrbitCameraMode.LeanAt( 1f ), 2e-6f, "a term of 1 a second" );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.LeanAt( 0.3f ), "under the clamp" );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.LeanAt( -0.3f ), "under the clamp the other way" );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.LeanAt( 0f ), "at rest" );
	}

	/// <summary>
	/// At the original's rotation nought, zoom 110, the lean turns the camera's rows about its world Z: its right row
	/// (1, 0, 0) to (cos L, sin L, 0), here (cos L, 0, sin L); its forward swings to x by sin( pitch ) sin L and its up
	/// back by cos( pitch ) sin L: a look to the side and a roll of cos( pitch ) x L, 0.457 degrees.
	/// </summary>
	[TestMethod]
	public void FacingAlongYTheLeanRollsTheView()
	{
		var (_, aim) = ParkOrbitCameraMode.View( new Vector3( 300f, 400f, 0f ), 0f, 110f, 0f );
		var lean = ParkOrbitCameraMode.LeanAt( Held110 );
		var (look, up) = ParkOrbitCameraMode.Leaned( aim, lean );
		var right = look.Cross( up );

		Assert.AreEqual( 0.012928f, look.X, 2e-5f, "the look swings to x" );
		Assert.AreEqual( 0.5250f, look.Y, 1e-4f, "along y as before" );
		Assert.AreEqual( -0.007976f, up.X, 2e-5f, "the up leans back from x" );
		Assert.AreEqual( 0.015192f, right.Z, 2e-5f, "the right row rises by sin L" );
		Assert.AreEqual( 0f, right.Y, 1e-5f, "and keeps no y" );
		Assert.AreEqual( 0f, look.Dot( up ), 1e-5f, "square" );
	}

	/// <summary>
	/// At the original's rotation 90 its world Z is the camera's right row, so the same lean nods the view instead: 0.87
	/// degrees less far down than the zoom's 58.33, and no roll.
	/// </summary>
	[TestMethod]
	public void FacingAlongXTheLeanNodsTheView()
	{
		var (_, aim) = ParkOrbitCameraMode.View( new Vector3( 300f, 400f, 0f ), -MathF.PI / 2f, 110f, 0f );
		var (look, up) = ParkOrbitCameraMode.Leaned( aim, ParkOrbitCameraMode.LeanAt( Held110 ) );
		var right = look.Cross( up );

		Assert.AreEqual( 57.4628f, MathF.Asin( -look.Z ) * 180f / MathF.PI, 1e-3f, "less far down by L" );
		Assert.AreEqual( 0f, look.Y, 1e-5f, "still along x" );
		Assert.AreEqual( 0f, right.Z, 1e-5f, "no roll" );
	}

	/// <summary>With no lean the up is the world's, square to the look: what the view was built against before.</summary>
	[TestMethod]
	public void AtRestTheUpIsTheWorldsSquareToTheLook()
	{
		var (_, aim) = ParkOrbitCameraMode.View( new Vector3( 300f, 400f, 0f ), 0.7f, 90f, 0f );
		var (look, up) = ParkOrbitCameraMode.Leaned( aim, 0f );

		Assert.AreEqual( 0f, Vector3.DistanceBetween( aim.Normal, look ), 1e-6f, "the look is the aim" );
		Assert.AreEqual( 0f, look.Cross( up ).Z, 1e-6f, "no roll" );
		Assert.IsTrue( up.Z > 0f, "up is up" );
	}

	/// <summary>A mode leaned as a held pan leans it, the way <see cref="ParkOrbitCameraMode.Place"/> stands it.</summary>
	private sealed class LeanedMode : CameraMode
	{
		public LeanedMode()
		{
			var (_, aim) = ParkOrbitCameraMode.View( new Vector3( 300f, 400f, 0f ), 0f, 110f, 0f );
			var (look, up) = ParkOrbitCameraMode.Leaned( aim, ParkOrbitCameraMode.LeanAt( Held110 ) );
			Position = new Vector3( 300f, 342.253f, 93.623f );
			Rotation = Rotation.LookAt( look );
			Up = up;
		}
	}

	/// <summary>
	/// The view is built against the mode's up, so the leaned up is the picture's up and the map's x, across the picture
	/// facing along y, runs down to the right by the roll, cos( pitch ) x L: what the frames measured (0.459 to 0.520
	/// degrees in this game, 0.442 and 0.453 in the original, Q1109).
	/// </summary>
	[TestMethod]
	public void TheViewIsBuiltAgainstTheModesUp()
	{
		Log ??= new();

		try
		{
			Camera.SetCameraMode<LeanedMode>();
			Camera.Update();

			var up = Camera.Up;
			var drawn = System.Numerics.Vector3.TransformNormal( new System.Numerics.Vector3( up.X, up.Y, up.Z ), Camera.ViewMatrix );
			var across = System.Numerics.Vector3.TransformNormal( new System.Numerics.Vector3( 1f, 0f, 0f ), Camera.ViewMatrix );

			Assert.AreEqual( 0f, drawn.X, 1e-5f, "the leaned up is straight up the picture" );
			Assert.AreEqual( 1f, drawn.Y, 1e-5f, "and all of it" );
			Assert.AreEqual( 0.4570f, MathF.Atan2( -across.Y, across.X ) * 180f / MathF.PI, 2e-3f, "the map's x turned clockwise by the roll" );
		}
		finally
		{
			Camera.SetCameraMode<CameraMode>();
		}
	}

	/// <summary>
	/// Under the lean the pointer's ray and a person's quad turn with the picture: the original's ray goes through the
	/// leaned matrix (<c>0x0045c15c</c>) and its sprite's span is added in clip space. The ray through the right edge's
	/// middle stays on the picture's middle row, and a quad's top edge stays square to the picture's up, where either
	/// built on the world's up is off by the roll's 0.008.
	/// </summary>
	[TestMethod]
	public void ThePointersRayAndAPersonsQuadTurnWithThePicture()
	{
		Log ??= new();
		var screen = Screen.Size;

		try
		{
			Screen.Size = new Point2( 1280, 720 );
			Camera.SetCameraMode<LeanedMode>();
			Camera.Update();

			var ray = ParkPicking.RayThrough( new Vector2( 1280f, 360f ) );
			var seen = System.Numerics.Vector3.TransformNormal( new System.Numerics.Vector3( ray.X, ray.Y, ray.Z ), Camera.ViewMatrix );

			Assert.AreEqual( Camera.HorizontalTangent, seen.X / -seen.Z, 1e-4f, "at the right edge" );
			Assert.AreEqual( 0f, seen.Y / -seen.Z, 1e-4f, "on the middle row" );

			var corners = new Vertex[4];
			var picture = new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, 38, 50, 22, 42 );
			Assert.IsTrue( ParkGuestSprites.Corners( corners, Camera.Rotation.Forward, new Vector3( 300f, 400f, 0f ), picture, false, 255 ) );

			var top = corners[1].Position - corners[0].Position;
			var up = Camera.Up;
			var length = MathF.Sqrt( (top.X * top.X) + (top.Y * top.Y) + (top.Z * top.Z) );

			Assert.AreEqual( 0f, ((top.X * up.X) + (top.Y * up.Y) + (top.Z * up.Z)) / length, 1e-5f, "the top edge square to the picture's up" );
		}
		finally
		{
			Camera.SetCameraMode<CameraMode>();
			Screen.Size = screen;
		}
	}

	/// <summary>The placed camera takes its lean from the pan's term along x alone: a pan along y leans nothing.</summary>
	[TestMethod]
	public void ThePlacedCameraLeansByTheXTermAlone()
	{
		var (point, yaw, zoom, scroll) = (ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom,
			ParkOrbitCameraMode.Scroll);

		try
		{
			ParkOrbitCameraMode.PointOfInterest = new Vector3( 300f, 400f, 0f );
			ParkOrbitCameraMode.Yaw = 0f;
			ParkOrbitCameraMode.Zoom = 110f;

			ParkOrbitCameraMode.Scroll = new Vector2( -Held110, 0f );
			var camera = new ParkOrbitCameraMode();
			camera.Place( 0f );

			Assert.AreEqual( -0.012928f, camera.Rotation.Forward.X, 2e-5f, "a pan to -x looks to -x" );
			Assert.AreEqual( 0.007976f, camera.Up.X, 2e-5f, "and rolls the other way" );

			ParkOrbitCameraMode.Scroll = new Vector2( 0f, Held110 );
			camera.Place( 0f );

			Assert.AreEqual( 0f, camera.Rotation.Forward.X, 1e-5f, "a pan along y looks straight on" );
			Assert.AreEqual( 0f, camera.Up.X, 1e-5f, "and rolls nothing" );
		}
		finally
		{
			(ParkOrbitCameraMode.PointOfInterest, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom, ParkOrbitCameraMode.Scroll) =
				(point, yaw, zoom, scroll);
		}
	}
}
