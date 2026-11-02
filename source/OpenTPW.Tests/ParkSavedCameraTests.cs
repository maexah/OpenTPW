using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// A park opens where its file's camera module says the camera stood (<c>docs/exe/saves.md</c>, "Loading the modules", Camera;
/// <c>FUN_0042cec0</c>): the zoom and the point as written, the yaw the rotation negated, the rotation first rounded to the nearest quarter turn
/// with the Rotation option on 90 degrees. Nothing here needs the game.
/// </summary>
[TestClass]
public class ParkSavedCameraTests
{
	private const float Quarter = 1.5707964f;

	[TestInitialize]
	public void Start()
	{
		Log ??= new();
		ParkOrbitCameraMode.Forget();
	}

	[TestCleanup]
	public void PutTheCameraBack() => ParkOrbitCameraMode.Forget();

	/// <summary>
	/// <b>The rotation is the nearest quarter turn, a tie going towards nought</b> (<c>0x0042d05e</c>..<c>0x0042d0da</c>: the rotation less its
	/// <c>fmod</c> by a quarter, moved on a quarter when the remainder passes an eighth), and a quarter turn or a whole number of them stays as written.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the rotation left as written; the remainder taken with floor and not <c>fmod</c> (a negative rotation lands a quarter out); the
	/// tie rounded away from nought (the measured file, -0.7854 read 0.0 after the load, would read -1.5708).
	/// </remarks>
	[TestMethod]
	[DataRow( 0f, 0f )]
	[DataRow( 0.5f, 0f )]
	[DataRow( 0.8f, Quarter )]
	[DataRow( -0.5f, 0f )]
	[DataRow( -0.8f, -Quarter )]
	[DataRow( 0.7853982f, 0f )]
	[DataRow( -0.7853982f, 0f )]
	[DataRow( Quarter, Quarter )]
	[DataRow( -Quarter, -Quarter )]
	[DataRow( 2.4f, 3.1415927f )]
	[DataRow( -2.4f, -3.1415927f )]
	[DataRow( 3.1415927f, 3.1415927f )]
	public void TheRotationIsRoundedToTheNearestQuarterTurn( float written, float kept )
		=> Assert.AreEqual( kept, ParkOrbitCameraMode.RoundedToAQuarter( written ), 1e-6f, $"{written}" );

	/// <summary>
	/// <b>The camera takes the file's view</b>: the zoom and the point as written, the yaw the rotation negated (the writer's
	/// <c>0 - Yaw</c> undone), and with the 90-degree option off the rotation as written.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the yaw not negated; the point's two numbers swapped; the zoom left at 110; the rounding applied with the option off.
	/// </remarks>
	[TestMethod]
	public void TheCameraTakesTheFilesView()
	{
		ParkOrbitCameraMode.TakeSaved( new( 80f, -1.5707964f, 312.5f, 88.25f ), quarterTurns: true );

		Assert.AreEqual( 80f, ParkOrbitCameraMode.Zoom );
		Assert.AreEqual( 1.5707964f, ParkOrbitCameraMode.Yaw, "the file's rotation is the yaw negated" );
		Assert.AreEqual( new Vector3( 312.5f, 88.25f, 0f ), ParkOrbitCameraMode.PointOfInterest );

		ParkOrbitCameraMode.TakeSaved( new( 100f, 0.5f, 200f, 100f ), quarterTurns: false );
		Assert.AreEqual( -0.5f, ParkOrbitCameraMode.Yaw, "Smooth: as written" );

		ParkOrbitCameraMode.TakeSaved( new( 100f, 0.5f, 200f, 100f ), quarterTurns: true );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.Yaw, "90 degrees: 0.5 rounds to nought" );
		Assert.AreEqual( 1f, MathF.Sign( 1f / ParkOrbitCameraMode.Yaw ), "and to +0, as the writer's yaw of nought is" );
	}

	/// <summary><b>A view with a number that is not finite leaves the camera where it is.</b></summary>
	/// <remarks><b>Mutations:</b> the check dropped, which sets a NaN yaw.</remarks>
	[TestMethod]
	public void AViewThatIsNotFiniteIsRefused()
	{
		ParkOrbitCameraMode.TakeSaved( new( 90f, 0f, 300f, 200f ), quarterTurns: true );
		ParkOrbitCameraMode.TakeSaved( new( float.NaN, 0f, 1f, 1f ), quarterTurns: true );
		ParkOrbitCameraMode.TakeSaved( new( 90f, float.PositiveInfinity, 1f, 1f ), quarterTurns: true );

		Assert.AreEqual( 90f, ParkOrbitCameraMode.Zoom );
		Assert.AreEqual( new Vector3( 300f, 200f, 0f ), ParkOrbitCameraMode.PointOfInterest );
	}
}
