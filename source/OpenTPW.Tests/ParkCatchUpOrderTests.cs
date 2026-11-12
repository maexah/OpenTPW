using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park's loop takes a frame's ticks one at a time through every system (<see cref="ParkLoop"/>; the original's from
/// <c>0x0054f4bf</c>): a frame of sixteen ticks runs the scripts to the eighth, the sweep, the scripts to the sixteenth, the
/// sweep, so what each sweep sees is the same whatever the frame rate.
/// </summary>
[TestClass]
public class ParkCatchUpOrderTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static RideScript AcceptOnSecondTurn()
		=> Script( ParkRideOperation.AdmitVariable, Word( Opcode.ENDSLICE ), Word( Opcode.COPY ), 0x40000000, 0,
			Word( Opcode.ENDSLICE ), Word( Opcode.BRANCH ), 0x20000004 );

	/// <summary>A whole .RSE file of one variable, <paramref name="variable"/>, and <paramref name="body"/>.</summary>
	private static RideScript Script( string variable, params int[] body )
	{
		using var bytes = new MemoryStream();
		using var writer = new BinaryWriter( bytes, Encoding.ASCII, leaveOpen: true );
		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		foreach ( var value in new[] { 1, 8, 50, 0, 0, 0 } ) writer.Write( value );
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );
		foreach ( var word in body ) writer.Write( word );
		writer.Write( 0 );
		var name = Encoding.ASCII.GetBytes( variable + "\0" );
		writer.Write( name.Length );
		writer.Write( name );
		writer.Flush();
		return new RideScript( new RideScriptFile( new MemoryStream( bytes.ToArray() ) ) );
	}

	/// <summary>
	/// <b>A sweep sees the scripts up to its own tick and none after, at any frame rate</b>: a script that lets its guest on
	/// at its turn on tick 9 is answered on the sweep of tick 16, the park's second, whether the sixteen ticks come one, eight
	/// or sixteen to a frame; and the ride's turn in each sweep finds the scripts advanced to that sweep's own tick, 8 then 16.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the rides' ticks in a loop of their own before the people's (the two loops this replaced, which
	/// boarded on the first sweep of a sixteen-tick frame and showed it the scripts at 16); the sweep before the scripts in its
	/// own step.
	/// </remarks>
	[DataTestMethod]
	[DataRow( 1 )]
	[DataRow( 8 )]
	[DataRow( 16 )]
	public void EachSweepSeesTheScriptsToItsOwnTick( int ticksPerFrame )
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		var previousRides = ParkRides.Current;
		ParkRides? rides = null;
		ParkPeople? people = null;
		try
		{
			var state = new ParkState( world );
			var initialTick = state.GameTick;
			var script = AcceptOnSecondTurn();
			rides = new ParkRides( "jungle", null, null );
			rides.Scheduler.Add( 1, script ); // Eligible at ticks 1 and 9.
			Peep? guest = null;
			int? admittedAt = null;
			var seen = new List<(int Sweep, int ScriptTick)>();
			people = new ParkPeople( world, state: state, scriptFor: id =>
			{
				if ( guest?.State == PeepState.Riding ) admittedAt ??= state.GameTick - initialTick;
				if ( id == 13 ) seen.Add( (state.GameTick - initialTick, rides!.Scheduler.Tick) );
				return id == 13 ? script : null;
			}, random: new Random( 1 ), behaviourRandom: new Random( 2 ),
				rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			guest = people.Peeps.Single( peep => peep.ThingId == 29 );
			var id = guest.ThingId;
			guest.MajorDest = 13;
			guest.SetState( PeepState.EnteringRide, 0, new Random( 1 ) );
			state.JoinQueue( 13, id );
			Assert.IsTrue( script.Set( ParkRideOperation.AdmitVariable, id ) );

			for ( var ticks = 0; ticks < 16; ticks += ticksPerFrame )
			{
				SimulationClockScope.Frame( ticksPerFrame * GameClock.TickSeconds );
				Assert.AreEqual( ticksPerFrame, GameClock.TicksDue );
				// Same order as Level's Entity.All walk, through actual production update methods.
				ParkFrame.Run( people, rides );
			}
			Assert.AreEqual( 16, rides.Scheduler.Tick );
			Assert.AreEqual( 2, state.GameTick - initialTick );
			Assert.AreEqual( PeepState.Riding, guest.State );
			Assert.AreEqual( 2, admittedAt,
				$"{ticksPerFrame} ticks/frame: script clears admission at tick 9, observed boarding on world tick {admittedAt}" );
			CollectionAssert.AreEqual( new[] { (1, 8), (2, 16) }, seen.Distinct().ToArray(),
				$"{ticksPerFrame} ticks/frame: the ride's turn in each sweep saw the scripts at {string.Join( ", ", seen.Distinct() )}" );
			Console.WriteLine( $"{ticksPerFrame} ticks/frame: boarding world tick {admittedAt}; sweeps saw {string.Join( ", ", seen.Distinct() )}" );
		}
		finally
		{
			people?.Delete();
			rides?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkRides ).GetProperty( nameof( ParkRides.Current ) )!.SetValue( null, previousRides );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	/// <summary>
	/// <b>Each tick of a frame hands the scripts its own instant</b> (<see cref="ParkRides.Tick"/>; a kept deviation, the
	/// engine hands every tick of a catch-up the frame's one instant, <c>docs/exe/park.md</c>, difference 6): a script whose turn
	/// comes on the fifth of a frame's eight ticks waits 100 ms from that tick's 155 ms, not from the frame's first.
	/// </summary>
	/// <remarks><b>Mutations:</b> every tick of the frame handed the first tick's instant.</remarks>
	[TestMethod]
	public void EachTickHandsTheScriptsItsOwnInstant()
	{
		GameData.Required();
		using var clock = new SimulationClockScope();
		var previousRides = ParkRides.Current;
		var rides = new ParkRides( "jungle", null, null );

		try
		{
			var script = Script( "VAR_0", Word( Opcode.WAIT ), 100, Word( Opcode.END ) );

			// Its turn on the scheduler's fifth tick (RideScriptScheduler.RunsOn: the id's low three bits).
			rides.Scheduler.Add( 5, script );
			SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
			Assert.AreEqual( (8, 8), (GameClock.TicksDue, GameClock.Ticks) );
			ParkLoop.Run( null, rides, null );

			Assert.AreEqual( 5 * 31f + 100f, script.WaitDeadline, "tick 5's instant, 155 ms, and the wait" );
		}
		finally
		{
			rides.Delete();
			Entity.ApplyDeletions();
			typeof( ParkRides ).GetProperty( nameof( ParkRides.Current ) )!.SetValue( null, previousRides );
		}
	}

	/// <summary>
	/// <b>A system deleted earlier in the frame takes none of its ticks</b>, as the entity walk that ran them skips a deleted
	/// entity (<see cref="Entity.Update"/>): a frame of eight ticks leaves a deleted ride system's scripts where they stood.
	/// </summary>
	/// <remarks><b>Mutations:</b> the loop's test of a deleted ride system taken out.</remarks>
	[TestMethod]
	public void ADeletedSystemTakesNoTick()
	{
		GameData.Required();
		using var clock = new SimulationClockScope();
		var previousRides = ParkRides.Current;
		var rides = new ParkRides( "jungle", null, null );

		try
		{
			rides.Delete();
			SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
			Assert.AreEqual( 8, GameClock.TicksDue );
			ParkLoop.Run( null, rides, null );
			Assert.AreEqual( 0, rides.Scheduler.Tick, "a deleted system's scripts took no tick" );
		}
		finally
		{
			Entity.ApplyDeletions();
			typeof( ParkRides ).GetProperty( nameof( ParkRides.Current ) )!.SetValue( null, previousRides );
		}
	}
}
