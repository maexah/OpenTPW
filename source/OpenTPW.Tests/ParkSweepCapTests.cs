using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park's loop runs at most three thing sweeps in a rendered frame and drops the rest (<c>0x0054f680</c>;
/// <c>docs/exe/park-engine.md</c>, "What the 31 ms tick drives"). Driven through <see cref="ParkPeople"/> a frame at
/// a time, as <see cref="GameClock"/> hands it the ticks.
/// </summary>
[TestClass]
public class ParkSweepCapTests
{
	/// <summary>
	/// <b>A frame that owes eight sweeps runs three, and the five are never made up</b>: the park's clock goes up
	/// three, and the frame after, owing one, runs one. A frame owing exactly three runs all three.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no cap; a cap of two or of four; the dropped sweeps owed to the next frame; the count kept
	/// from frame to frame, so the frame after a long one runs none.
	/// </remarks>
	[TestMethod]
	public void ALongFrameRunsThreeSweepsAndDropsTheRest()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		using var clock = new SimulationClockScope();
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ) );

		try
		{
			var start = people.State.GameTick;

			// Two seconds is the clock's own cap: 64 ticks, eight sweeps owed.
			SimulationClockScope.Frame( 2f );
			Assert.AreEqual( 64, GameClock.TicksDue, "the frame owes 64 ticks" );
			people.Update();
			Assert.AreEqual( start + 3, people.State.GameTick, "three sweeps run" );
			Assert.AreEqual( (3, 5), (people.SweepsRun, people.SweepsDropped), "and five dropped" );

			SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
			people.Update();
			Assert.AreEqual( start + 4, people.State.GameTick, "the next frame owes one and runs one: nothing is made up" );

			SimulationClockScope.Frame( GameClock.TickSeconds * 24 );
			Assert.AreEqual( 24, GameClock.TicksDue );
			people.Update();
			Assert.AreEqual( start + 7, people.State.GameTick, "a frame owing three runs all three" );
			Assert.AreEqual( (7, 5), (people.SweepsRun, people.SweepsDropped) );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The staff pool's unbuilt refresh is counted once a sweep the park runs</b>, where the original runs
	/// <c>FUN_005084f0</c> (<c>0x004d7b30</c>), and not on a sweep that is dropped.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pool's turn not called from the sweep; called once a frame, or on a dropped sweep.</remarks>
	[TestMethod]
	public void TheStaffPoolsRefreshIsCountedOnceASweep()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var balance = new ParkBalance( "jungle", easyMode: true );
		var poolBefore = ParkStaffPool.Current;

		using var clock = new SimulationClockScope();
		var people = new ParkPeople( world, balance );
		_ = new ParkStaffPool( balance );

		try
		{
			Unimplemented.Forget();

			SimulationClockScope.Frame( 2f );
			people.Update();

			Assert.AreEqual( 3, Unimplemented.Summary.FirstOrDefault( gap => gap.What == "STAFF_POOL_REFRESH" ).Times,
				"a frame owing eight sweeps runs three, and the pool's turn with each" );
		}
		finally
		{
			Unimplemented.Forget();
			typeof( ParkStaffPool ).GetProperty( nameof( ParkStaffPool.Current ) )!.SetValue( null, poolBefore );
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
