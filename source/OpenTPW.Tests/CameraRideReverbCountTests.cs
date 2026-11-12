using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics = System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// The every-eighth step's reverb handoff of the ride the camera is on (<c>FUN_0055a470</c> at <c>0x0054f828</c>;
/// <c>docs/exe/audio.md</c>, "The reverb") is not built, reverb being cut, so the park's loop counts
/// <c>CAMERA_RIDE_REVERB</c> on each eighth tick while a ride's view is on (rule 4, Q864).
/// </summary>
[TestClass]
[DoNotParallelize]
public class CameraRideReverbCountTests
{
	private const string Name = "CAMERA_RIDE_REVERB";

	private static int Times() => Unimplemented.Summary.FirstOrDefault( entry => entry.What == Name ).Times;

	/// <summary>Runs one frame of <paramref name="ticks"/> ticks and answers how many handoffs it counted.</summary>
	private static int Counted( int ticks, ParkPeople? people = null )
	{
		var before = Times();

		// A tenth of a millisecond over, so the float sum is never a hair short of the last tick.
		SimulationClockScope.Frame( GameClock.TickSeconds * ticks + 1e-4f );
		Assert.AreEqual( ticks, GameClock.TicksDue, $"the frame owes {ticks} ticks" );
		ParkLoop.Run( null, null, people );

		return Times() - before;
	}

	private static void ViewARide()
	{
		ParkRideViewCameraMode.Viewed = () => Numerics.Matrix4x4.Identity;
		Camera.SetCameraMode<ParkRideViewCameraMode>();
	}

	private static void LeaveTheRide()
	{
		ParkRideViewCameraMode.Leave();
		Camera.SetCameraMode<CameraMode>();
	}

	/// <summary>
	/// <b>A ride under the camera is counted once each eighth tick, and no ride is not counted</b>: 24 ticks in its view count
	/// 3, 40 ticks count 5, and 24 ticks of the plain camera count none. With no people the loop still hands it, as the
	/// original's step runs it whatever the sweep did.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> counted every tick (24); every second (12); not asked whether a ride is viewed (3 with none); asked only
	/// beside the people's sweep (0 here).
	/// </remarks>
	[TestMethod]
	public void EachEighthTickInARidesViewIsCountedAndNoneWithout()
	{
		using var clock = new SimulationClockScope();

		Assert.IsFalse( ParkRideViewCameraMode.Active );
		Assert.AreEqual( 0, Counted( 24 ), "no ride under the camera: nothing handed" );

		try
		{
			ViewARide();
			Assert.AreEqual( 3, Counted( 24 ), "24 ticks hold three eighth ticks" );
			Assert.AreEqual( 5, Counted( 40 ), "40 ticks hold five" );
		}
		finally
		{
			LeaveTheRide();
		}

		Assert.AreEqual( 0, Counted( 24 ), "the view left: nothing handed again" );
	}

	/// <summary>
	/// <b>A frame owing eight sweeps runs three and still counts eight handoffs</b>: the original's step past the third sweep
	/// jumps to <c>0x0054f828</c>, which still calls <c>FUN_0055a470</c> (<c>docs/exe/park-engine.md</c>, "A sweep that cannot
	/// run is dropped, not owed").
	/// </summary>
	/// <remarks><b>Mutations:</b> the handoff asked only when the sweep runs (3); asked only when a sweep is dropped (5).</remarks>
	[TestMethod]
	public void ADroppedSweepStillHandsTheReverb()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		using var clock = new SimulationClockScope();
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ) );

		try
		{
			ViewARide();

			var dropped = people.SweepsDropped;

			Assert.AreEqual( 8, Counted( 64, people ), "64 ticks, eight handoffs" );
			Assert.AreEqual( 5, people.SweepsDropped - dropped, "though five of the eight sweeps were dropped" );
		}
		finally
		{
			LeaveTheRide();
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
