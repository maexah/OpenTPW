using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics = System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// The every-eighth step's reverb handoff of the ride the camera is on (<c>FUN_0055a470</c> at <c>0x0054f828</c>;
/// <c>docs/exe/audio.md</c>, "The reverb") is not built, reverb being cut, so the park's loop counts
/// <c>CAMERA_RIDE_REVERB</c> on each eighth tick while a ride's view is on (rule 4, Q864) and
/// <c>CAMERA_NO_RIDE_REVERB</c> on each other eighth tick, where the original hands the level's <c>Sound.Reverb</c> (Q1236).
/// </summary>
[TestClass]
[DoNotParallelize]
public class CameraRideReverbCountTests
{
	private const string Name = "CAMERA_RIDE_REVERB";
	private const string NoRide = "CAMERA_NO_RIDE_REVERB";

	private static int Times( string name = Name ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == name ).Times;

	/// <summary>Runs one frame of <paramref name="ticks"/> ticks and answers how many handoffs it counted under <paramref name="name"/>.</summary>
	private static int Counted( int ticks, ParkPeople? people = null, string name = Name )
	{
		var before = Times( name );

		// A tenth of a millisecond over, so the float sum is never a hair short of the last tick.
		SimulationClockScope.Frame( GameClock.TickSeconds * ticks + 1e-4f );
		Assert.AreEqual( ticks, GameClock.TicksDue, $"the frame owes {ticks} ticks" );
		ParkLoop.Run( null, null, people );

		return Times( name ) - before;
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
	/// <b>A ride under the camera is counted once each eighth tick under its own name</b>: 24 ticks in its view count
	/// 3, 40 ticks count 5, a tick a frame counts only on tick 96, and 24 ticks of the plain camera count none of it. With no people the loop still hands it, as the
	/// original's step runs it whatever the sweep did.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> counted every tick (24); every second (12); not asked whether a ride is viewed (3 with none); asked only
	/// beside the people's sweep (0 here); the beat taken from the frame's own index, or from another of the eight ticks (a tick a
	/// frame counts elsewhere than 96).
	/// </remarks>
	[TestMethod]
	public void EachEighthTickInARidesViewIsCountedAndNoneWithout()
	{
		using var clock = new SimulationClockScope();

		Assert.IsFalse( ParkRideViewCameraMode.Active );
		Assert.AreEqual( 0, Counted( 24 ), "no ride under the camera: not the ride's handoff" );

		try
		{
			ViewARide();
			Assert.AreEqual( 3, Counted( 24 ), "24 ticks hold three eighth ticks" );
			Assert.AreEqual( 5, Counted( 40 ), "40 ticks hold five" );

			// Ticks 89 to 97 one a frame: only 96 is an eighth tick, so the beat is the tick's and not the frame's.
			var single = Enumerable.Range( 89, 9 ).Select( _ => Counted( 1 ) ).ToArray();
			CollectionAssert.AreEqual( new[] { 0, 0, 0, 0, 0, 0, 0, 1, 0 }, single, "one frame a tick, from 89" );
		}
		finally
		{
			LeaveTheRide();
		}

		Assert.AreEqual( 0, Counted( 24 ), "the view left: nothing handed again" );
	}

	/// <summary>
	/// <b>With no ride under the camera the handoff is counted under its own name, each eighth tick</b>: the original's
	/// <c>FUN_0055a470</c> hands <c>Sound.Reverb</c> when <c>FUN_0042a4e0</c> answers 0 (<c>0x0055a477</c>, <c>0x0055a4c5</c>), so
	/// 24 ticks of the plain camera count 3 and 40 count 5, a tick a frame only on tick 72; in a ride's view it counts none, and
	/// left, it counts again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the no-ride handoff not counted (0); counted under the ride's name (0 here, 3 there); counted in a
	/// ride's view too (3 there); counted every tick (24); the beat from the frame's index (a tick a frame counts elsewhere than 72).
	/// </remarks>
	[TestMethod]
	public void EachEighthTickWithNoRideIsCountedUnderItsOwnName()
	{
		using var clock = new SimulationClockScope();

		Assert.IsFalse( ParkRideViewCameraMode.Active );
		Assert.AreEqual( 3, Counted( 24, name: NoRide ), "24 ticks with no ride hold three handoffs" );
		Assert.AreEqual( 5, Counted( 40, name: NoRide ), "40 ticks hold five" );

		// Ticks 65 to 73 one a frame: only 72 is an eighth tick.
		var single = Enumerable.Range( 65, 9 ).Select( _ => Counted( 1, name: NoRide ) ).ToArray();
		CollectionAssert.AreEqual( new[] { 0, 0, 0, 0, 0, 0, 0, 1, 0 }, single, "one frame a tick, from 65" );

		try
		{
			ViewARide();
			Assert.AreEqual( 0, Counted( 24, name: NoRide ), "a ride under the camera: not the no-ride handoff" );
		}
		finally
		{
			LeaveTheRide();
		}

		Assert.AreEqual( 3, Counted( 24, name: NoRide ), "the view left: the no-ride handoff again" );
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
