using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class ParkReplayTests
{
	private sealed class Draws( int seed ) : Random( seed )
	{
		public int Taken { get; private set; }
		public override int Next() { ++Taken; return base.Next(); }
		public override int Next( int maxValue ) { ++Taken; return base.Next( maxValue ); }
	}

	private static string[] Replay( int seed )
	{
		var data = GameData.Required();
		using var saved = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( saved ).ReadFile() );
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		var previousFiles = FileSystem;
		ParkPeople? people = null;
		try
		{
			FileSystem = data;
			var state = new ParkState( world );
			var arrival = new Draws( seed );
			var behaviour = new Draws( seed + 1 );
			var staff = new Draws( seed + 2 );
			var ride = new Draws( seed + 3 );
			using var bytes = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
			var script = new RideScript( new RideScriptFile( bytes ) );
			// One costume dismissal exercises the ride stream through the production entity update.
			var shop = state.Objects.Single( item => item.ThingId == 16 ) with { CatalogueId = 1202, State = 0 };
			Assert.IsTrue( state.ReplaceObject( shop ) );
			people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), state: state,
				catalogue: new ParkItemCatalogue( "jungle", data ), scriptFor: id => id == 16 ? script : null,
				random: arrival, banks: new ParkSpriteBanks( 6, 3, 4 ),
				behaviourRandom: behaviour, rideRandom: ride, staffRandom: staff );
			var guest = people.Peeps.OrderBy( peep => peep.ThingId ).First();
			guest.SpriteKind = ParkSpriteBanks.ChildKind;
			guest.QueuePos = 1;
			guest.MajorDest = 16;
			guest.SetState( PeepState.Riding, state.GameTick, new Random( 0 ) );
			Assert.IsTrue( script.Set( ParkRideOperation.DismissVariable, guest.ThingId ) );
			Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );
			var trace = new List<string>();
			for ( var frame = 0; frame < 400; ++frame )
			{
				SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
				people.Update();
				trace.Add( $"{GameClock.TicksDue}:{state.GameTick}:" + string.Join( ";", people.Peeps.Select( peep =>
					$"{peep.ThingId},{peep.State},{peep.Navigator.Position},{peep.MajorDest},{peep.Cash},{peep.Happiness:R},{peep.SpriteKind},{peep.SpriteBank}" ) )
					+ "|" + string.Join( ";", people.Staff.Select( person =>
						$"{person.ThingId},{person.Activity},{person.Navigator.Position},{person.Happiness:R},{person.TimeStartedIdling}" ) ) );
			}
			Assert.IsTrue( arrival.Taken > 0, "arrivals exercised" );
			Assert.IsTrue( behaviour.Taken > 0, "guest decisions exercised" );
			Assert.IsTrue( staff.Taken > 0, "staff decisions exercised" );
			Assert.IsTrue( ride.Taken > 0, "ride settlement exercised" );
			return trace.ToArray();
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			FileSystem = previousFiles;
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}

	[TestMethod]
	public void SameClockAndRandomInputsReplayEveryObservedTurn()
	{
		var first = Replay( 17 );
		CollectionAssert.AreEqual( first, Replay( 17 ) );
		Assert.IsFalse( first.SequenceEqual( Replay( 29 ) ), "different inputs must affect the observed simulation" );
	}
}
