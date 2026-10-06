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
	/// from frame to frame, so the frame after a long one runs none; the cap only on a two-second frame; the sprites
	/// capped with the sweeps.
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

			// The sprites are stepped before the gate and take no part in the cap (FUN_00475360 at 0x0054f5fb): each
			// has had its turn at the frame's last sprite tick, so none is due before it.
			var lastSpriteTick = (GameClock.Ticks & ~(ParkPeople.SpriteTickEvery - 1)) * ParkPeople.MillisecondsPerTick;
			var sprites = people.Peeps.Select( peep => people.SpriteFor( peep.ThingId ) ).Where( sprite => sprite is { Ended: false } ).ToList();

			Assert.IsTrue( sprites.Count >= 13, $"the save's guests have sprites ({sprites.Count})" );

			foreach ( var sprite in sprites )
				Assert.IsTrue( sprite!.Due >= lastSpriteTick, $"a sprite due at {sprite.Due} was not stepped up to {lastSpriteTick}" );

			SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
			people.Update();
			Assert.AreEqual( start + 4, people.State.GameTick, "the next frame owes one and runs one: nothing is made up" );

			SimulationClockScope.Frame( GameClock.TickSeconds * 24 );
			Assert.AreEqual( 24, GameClock.TicksDue );
			people.Update();
			Assert.AreEqual( start + 7, people.State.GameTick, "a frame owing three runs all three" );
			Assert.AreEqual( (7, 5), (people.SweepsRun, people.SweepsDropped) );

			// The cap is on any pass, not only the clock's longest (0x0054f680): 40 ticks owe five sweeps.
			SimulationClockScope.Frame( GameClock.TickSeconds * 40 );
			Assert.AreEqual( 40, GameClock.TicksDue );
			people.Update();
			Assert.AreEqual( start + 10, people.State.GameTick, "a frame owing five runs three" );
			Assert.AreEqual( (10, 7), (people.SweepsRun, people.SweepsDropped), "and drops two" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The staff pool takes its turn on the park's sweep and the park's clock</b>, where the original runs
	/// <c>FUN_005084f0</c> (<c>0x004d7b30</c>): a pool whose mark is 360 sweeps old is topped up on the first sweep
	/// the park runs, and marked with that sweep's <c>mGameTick</c>; and in a frame that owes eight sweeps it takes
	/// three turns, one with each sweep run and none with a sweep dropped.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the pool's turn not called from the sweep; handed the 31 ms count, or a tick late; called once
	/// a frame, or on a dropped sweep.
	/// </remarks>
	[TestMethod]
	public void TheStaffPoolTakesItsTurnOnTheParksSweep()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var balance = new ParkBalance( "jungle", easyMode: true );
		var poolBefore = ParkStaffPool.Current;

		using var clock = new SimulationClockScope();
		var people = new ParkPeople( world, balance );
		var start = people.State.GameTick;
		var pool = new ParkStaffPool( balance, gameTick: start - 360 );

		try
		{
			SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
			people.Update();

			Assert.AreEqual( start + 1, people.State.GameTick );
			Assert.AreEqual( start + 1, pool.Mark, "topped up on the park's first sweep, 361 past its mark" );
			Assert.AreEqual( 1, pool.Turns );

			SimulationClockScope.Frame( 2f );
			people.Update();
			Assert.AreEqual( 4, pool.Turns, "a frame owing eight sweeps runs three, and the pool's turn with each" );
		}
		finally
		{
			typeof( ParkStaffPool ).GetProperty( nameof( ParkStaffPool.Current ) )!.SetValue( null, poolBefore );
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
