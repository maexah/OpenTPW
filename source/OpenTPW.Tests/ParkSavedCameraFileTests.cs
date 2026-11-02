using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// A park opens where the camera module of its file says (<c>docs/exe/saves.md</c>, "Loading the modules", Camera), for a park entered as for one
/// loaded from the Load Park list: <see cref="Level.TakeTheFilesCamera"/> over the shipped park and over a file written with another view.
/// </summary>
[TestClass]
public class ParkSavedCameraFileTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld _shipped = null!;
	private bool _ninety;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( ShippedPark ) );
		var reader = new SaveReader( stream );

		_shipped = new ParkWorld( reader.ReadFile(), reader.Preamble );
		_ninety = GameOptions.Current.NinetyDegreeRotation;
		ParkOrbitCameraMode.Forget();
	}

	[TestCleanup]
	public void PutItBack()
	{
		GameOptions.Current.NinetyDegreeRotation = _ninety;
		ParkOrbitCameraMode.Forget();
	}

	private ParkWorld WrittenWith( ParkCameraModule.View view )
		=> new( ParkFileWriter.Body( _shipped, new ParkFileWriter.Running( _shipped.GameTick, _shipped.ParkClosed != 0,
			_shipped.NumberOfVisitorsToDate, _shipped.Economy!.Value.Balance, view ) ) );

	/// <summary>
	/// <b>The shipped park's own camera is taken on entering</b>: zoom 110, rotation 0 and the point (475, 175.00726), not the default's (475, 175).
	/// </summary>
	/// <remarks><b>Mutations:</b> the call left out of <c>Level</c> cannot be seen here, only in the running game; the point taken from the default; the two ground numbers swapped.</remarks>
	[TestMethod]
	public void TheShippedParksCameraIsTakenOnEntering()
	{
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 1f, 2f, 0f );
		ParkOrbitCameraMode.Zoom = 70f;
		ParkOrbitCameraMode.Yaw = 1f;

		Level.TakeTheFilesCamera( _shipped );

		Assert.AreEqual( 110f, ParkOrbitCameraMode.Zoom );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.Yaw );
		Assert.AreEqual( new Vector3( 475f, 175.00726f, 0f ), ParkOrbitCameraMode.PointOfInterest );
	}

	/// <summary>
	/// <b>A view written into a file is the view the park opens on</b>, its rotation rounded to the nearest quarter turn with the Rotation option on 90
	/// degrees (the original's default and this game's) and as written with it off.
	/// </summary>
	/// <remarks><b>Mutations:</b> the option ignored either way; the rotation not negated.</remarks>
	[TestMethod]
	public void AViewWrittenIntoAFileIsTheViewTheParkOpensOn()
	{
		var world = WrittenWith( new( 85f, 0.8726646f, 300f, 200f ) );

		GameOptions.Current.NinetyDegreeRotation = true;
		Level.TakeTheFilesCamera( world );

		Assert.AreEqual( 85f, ParkOrbitCameraMode.Zoom );
		Assert.AreEqual( new Vector3( 300f, 200f, 0f ), ParkOrbitCameraMode.PointOfInterest );
		Assert.AreEqual( -1.5707964f, ParkOrbitCameraMode.Yaw, 1e-6f, "written as +50 degrees, rounded to +90, taken as the yaw -90" );

		GameOptions.Current.NinetyDegreeRotation = false;
		Level.TakeTheFilesCamera( world );
		Assert.AreEqual( -0.8726646f, ParkOrbitCameraMode.Yaw, 1e-6f, "Smooth: as written, negated" );

		GameOptions.Current.NinetyDegreeRotation = true;
		Level.TakeTheFilesCamera( WrittenWith( new( 85f, 0.5235988f, 300f, 200f ) ) );
		Assert.AreEqual( 0f, ParkOrbitCameraMode.Yaw, "30 degrees rounds to nought" );
	}

	/// <summary><b>No park, or a park with no file behind it, leaves the camera where it is.</b></summary>
	[TestMethod]
	public void NoFileLeavesTheCameraAlone()
	{
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 12f, 34f, 0f );
		ParkOrbitCameraMode.Zoom = 77f;

		Level.TakeTheFilesCamera( null );

		Assert.AreEqual( new Vector3( 12f, 34f, 0f ), ParkOrbitCameraMode.PointOfInterest );
		Assert.AreEqual( 77f, ParkOrbitCameraMode.Zoom );
	}
}
