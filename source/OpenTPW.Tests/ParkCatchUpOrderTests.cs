using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// Characterizes the open Q150 defect through both production OnUpdate paths. These assertions record
/// the discrepancy, not desired behaviour; replace them with partition equality when Q150 is fixed.
/// </summary>
[TestClass]
public class ParkCatchUpOrderTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static RideScript AcceptOnSecondTurn()
	{
		using var bytes = new MemoryStream();
		using var writer = new BinaryWriter( bytes, Encoding.ASCII, leaveOpen: true );
		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		foreach ( var value in new[] { 1, 8, 50, 0, 0, 0 } ) writer.Write( value );
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		int[] body = [Word( Opcode.ENDSLICE ), Word( Opcode.COPY ), 0x40000000, 0,
			Word( Opcode.ENDSLICE ), Word( Opcode.BRANCH ), 0x20000004];
		writer.Write( body.Length );
		foreach ( var word in body ) writer.Write( word );
		writer.Write( 0 );
		var name = Encoding.ASCII.GetBytes( ParkRideOperation.AdmitVariable + "\0" );
		writer.Write( name.Length );
		writer.Write( name );
		writer.Flush();
		return new RideScript( new RideScriptFile( new MemoryStream( bytes.ToArray() ) ) );
	}

	[DataTestMethod]
	[DataRow( 1, 2 )]
	[DataRow( 8, 2 )]
	[DataRow( 16, 1 )]
	public void CatchUpCanCompleteAdmissionBeforeItsScriptTick( int ticksPerFrame, int expectedWorldTick )
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
			people = new ParkPeople( world, state: state, scriptFor: id =>
			{
				if ( guest?.State == PeepState.Riding ) admittedAt ??= state.GameTick - initialTick;
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
				rides.Update();
				people.Update();
			}
			Assert.AreEqual( 16, rides.Scheduler.Tick );
			Assert.AreEqual( 2, state.GameTick - initialTick );
			Assert.AreEqual( PeepState.Riding, guest.State );
			Assert.AreEqual( expectedWorldTick, admittedAt,
				$"{ticksPerFrame} ticks/frame: script clears admission at tick 9, observed boarding on world tick {admittedAt}" );
			Console.WriteLine( $"{ticksPerFrame} ticks/frame: boarding world tick {admittedAt}; script acceptance tick 9" );
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
}
