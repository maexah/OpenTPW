using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Linq;
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

	/// <summary>
	/// <b>A view whose rotation or point is not finite leaves the camera where it is</b>, the zoom it held and its point alike (the zoom is
	/// not one of these: <see cref="AZoomOutsideTheRangeIsHeldToIt"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the check dropped, which sets a NaN yaw; the check left to the rotation alone (a NaN point is taken).</remarks>
	[TestMethod]
	[DataRow( 90f, float.PositiveInfinity, 1f, 1f )]
	[DataRow( 90f, float.NaN, 1f, 1f )]
	[DataRow( 90f, 0f, float.NaN, 1f )]
	[DataRow( 90f, 0f, 1f, float.NegativeInfinity )]
	public void AViewThatIsNotFiniteIsRefused( float zoom, float rotation, float x, float z )
	{
		ParkOrbitCameraMode.TakeSaved( new( 90f, 0f, 300f, 200f ), quarterTurns: true );
		ParkOrbitCameraMode.TakeSaved( new( zoom, rotation, x, z ), quarterTurns: true );

		Assert.AreEqual( 90f, ParkOrbitCameraMode.Zoom );
		Assert.AreEqual( new Vector3( 300f, 200f, 0f ), ParkOrbitCameraMode.PointOfInterest );
	}

	/// <summary>
	/// <b>A zoom outside 70 to 130 is held to it</b>, the rest of the view taken as written (the original's reader takes it as written and its next
	/// camera update holds it, <c>docs/exe/saves.md</c>, "A zoom outside 70 to 130 in the file"): a negative zoom is 70, so the pan's
	/// <c>sqrt( zoom )</c> stays a number; a huge one 130; a NaN 70; an infinity 130 or 70 by its sign; one inside, the ends included, as written.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the warning dropped, or printed for a zoom inside the range; the zoom taken as written; the clamp's ends swapped or either left off (the -5 and the 1e9 rows); a NaN left to <c>Clamp</c>,
	/// which keeps it; a NaN refused (the camera would stay at 90); the clamp applied to the point as well.
	/// </remarks>
	[TestMethod]
	[DataRow( -5f, 70f )]
	[DataRow( 0f, 70f )]
	[DataRow( 69.9f, 70f )]
	[DataRow( 70f, 70f )]
	[DataRow( 100f, 100f )]
	[DataRow( 130f, 130f )]
	[DataRow( 130.1f, 130f )]
	[DataRow( 1e9f, 130f )]
	[DataRow( float.MaxValue, 130f )]
	[DataRow( float.NegativeInfinity, 70f )]
	[DataRow( float.PositiveInfinity, 130f )]
	[DataRow( float.NaN, 70f )]
	public void AZoomOutsideTheRangeIsHeldToIt( float written, float taken )
	{
		ParkOrbitCameraMode.TakeSaved( new( 90f, 0f, 300f, 200f ), quarterTurns: true );

		var warnings = new ConcurrentQueue<string>();
		Logger.LogDelegate hear = ( severity, text ) =>
		{
			if ( severity == Logger.Level.Warning && text.Contains( "the park file's zoom" ) )
				warnings.Enqueue( text );
		};

		Logger.OnLog += hear;

		try { ParkOrbitCameraMode.TakeSaved( new( written, 0.5f, 312.5f, 88.25f ), quarterTurns: true ); }
		finally { Logger.OnLog -= hear; }

		// The warning is for a zoom held to the range, and for none inside it, the ends included.
		var held = float.IsNaN( written ) || written != taken;
		Assert.AreEqual( held ? 1 : 0, warnings.Count, $"{written}: {string.Join( " | ", warnings )}" );
		Assert.IsTrue( !held || warnings.All( w => w.Contains( $"taken as {taken}" ) ), $"{written}: the warning names the zoom taken" );

		Assert.AreEqual( taken, ParkOrbitCameraMode.Zoom, $"{written}" );
		Assert.AreEqual( new Vector3( 312.5f, 88.25f, 0f ), ParkOrbitCameraMode.PointOfInterest, "the point is the file's" );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.Yaw, "and the yaw too" );

		var pan = ParkOrbitCameraMode.PanTarget( 1f, 0f, ParkOrbitCameraMode.Yaw, ParkOrbitCameraMode.Zoom );
		Assert.IsTrue( float.IsFinite( pan.X ) && float.IsFinite( pan.Y ), "a held key's pan from the taken zoom is a number" );
	}
}
