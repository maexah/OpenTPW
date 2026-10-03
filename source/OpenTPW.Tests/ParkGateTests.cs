using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class ParkGateTests
{
	private BaseFileSystem data = null!;
	private readonly List<Entity> made = [];

	[TestInitialize]
	public void Mount() => FileSystem = data = GameData.Required();

	[TestCleanup]
	public void Cleanup()
	{
		foreach ( var entity in made ) entity.Delete();
		made.Clear();
		Entity.ApplyDeletions();
		Time.Paused = false;
	}

	private ParkWorld World()
	{
		using var stream = data.OpenRead( "levels/jungle/Easymode.TPWI" );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	// Give the fixed gate a placed coordinate so the headless fixture binds it without a GPU model.
	// All other seed fields and the real saved script module remain available to the production constructor.
	private sealed class Seed( IParkInitialState source, bool closed, bool saved, bool empty ) : IParkInitialState
	{
		public ParkWorld? Save => saved ? source.Save : null;
		public int ParkClosed => closed ? 1 : 0;
		public IReadOnlyList<ParkWorld.Person> People => empty ? [] : source.People;
		public IReadOnlyList<ParkWorld.CatalogueObject> Objects => source.Objects.Select( o => o.ThingId == source.ParkGates
			? o with { RawX = 47 << 8, RawY = 10 << 8 } : o ).ToArray();
		public IReadOnlyList<ParkThingIdentity> Things => source.Things;
		public ParkWorld.EconomyState? Economy => source.Economy;
		public ParkWorld.StaffHqState? StaffHq => source.StaffHq;
		public IReadOnlyList<ParkWorld.Sprite> Sprites => source.Sprites;
		public IReadOnlyList<ParkWorld.MapCell> Cells => source.Cells;
		public ParkWorld.ArrivalBlock Arrival => source.Arrival;
		public IReadOnlyList<ParkWorld.ObjectControl> ObjectControlRecords => source.ObjectControlRecords;
		public int ParkGates => source.ParkGates;
		public int TrafficLights => source.TrafficLights;
		public int FirstObject => source.FirstObject;
		public int RandomSeed => source.RandomSeed;
		public int Weather => source.Weather;
		public int GameTick => source.GameTick;
		public int BankAccount => source.BankAccount;
		public int NumberOfVisitorsToDate => source.NumberOfVisitorsToDate;
		public int WorldState => source.WorldState;
		public int ThingCount => source.ThingCount;
		public int ArrivalVehicleForSmallCrowd => source.ArrivalVehicleForSmallCrowd;
		public int ArrivalVehicleForMediumCrowd => source.ArrivalVehicleForMediumCrowd;
		public int ArrivalVehicleForLargeCrowd => source.ArrivalVehicleForLargeCrowd;
		public int CurrentArrivalVehicle => source.CurrentArrivalVehicle;
		public ParkWorld.MapCell CellAt( int x, int y ) => source.CellAt( x, y );
	}

	private (ParkPeople People, ParkState State, RideScript Gate) Park( bool empty = true )
	{
		var world = new Seed( World(), closed: false, saved: false, empty );
		var state = new ParkState( world );
		using var stream = data.OpenRead( "levels/jungle/features/gates/Gates.RSE" );
		var gate = new RideScript( new RideScriptFile( stream ) )
		{
			Animations = RideAnimations.Load( "levels/jungle/features/gates", "gates", data )
		};
		gate.Set( "VAR_COMMAND", 1 );
		gate.Set( "VAR_STATUS", 1 );
		var people = new ParkPeople( world, state: state, scriptFor: id => id == world.ParkGates ? gate : null );
		made.Add( people );
		return (people, state, gate);
	}

	[TestMethod]
	public void EmptyDoorClosesWithZeroAndReopensThroughNormalDispatch()
	{
		var (people, state, gate) = Park();
		state.SetParkClosed( true );
		Assert.AreEqual( 0, gate["VAR_COMMAND"] );
		Run( gate, 0 );
		Assert.AreEqual( 0, gate["VAR_STATUS"], "ordinary close finishes" );
		state.SetParkClosed( false );
		Assert.AreEqual( 1, gate["VAR_COMMAND"] );
		Run( gate, 20000 );
		Assert.AreEqual( 1, gate["VAR_STATUS"], "dispatch remains live after closing" );
	}

	private static void Run( RideScript gate, int start )
	{
		for ( var tick = 0; tick < 600; ++tick ) gate.Turn( start + tick * 31 );
	}

	[TestMethod]
	public void RepeatedDoorRequestsDoNotOverwriteTheCommandAndClosingRequiresOpenStatus()
	{
		var (_, state, gate) = Park();
		gate.Set( "VAR_COMMAND", 7 );
		state.SetParkClosed( false );
		Assert.AreEqual( 7, gate["VAR_COMMAND"] );
		gate.Set( "VAR_STATUS", 0 );
		state.SetParkClosed( true );
		Assert.AreEqual( 7, gate["VAR_COMMAND"] );
		gate.Set( "VAR_STATUS", 1 );
		state.SetParkClosed( true );
		Assert.AreEqual( 7, gate["VAR_COMMAND"], "no immediate retry for an unchanged door" );
	}

	[TestMethod]
	public void CensusReadsLivePositionAndCellTypeRatherThanOccupancyOrGuestActivity()
	{
		var (people, state, _) = Park( empty: false );
		foreach ( var guest in people.Peeps ) guest.Navigator.Position = FixedVector.AtCell( 0, 0 );
		state.SetRecord( 0, 0, state.Record( 0, 0 ) with { Type = 30 } );
		var subject = people.Peeps[0];
		state.SetRecord( 2, 2, state.Record( 2, 2 ) with { Type = 30 } );
		state.StandOn( subject.ThingId, 2, 2 );
		Assert.AreEqual( (2, 2), state.CellOf( subject.ThingId )!.Value );
		subject.Navigator.Position = FixedVector.AtCell( 1, 1 );
		foreach ( var type in new[] { 0, 1, 3, 9, 10, 4, 30, 2 } )
		{
			state.SetRecord( 1, 1, state.Record( 1, 1 ) with { Type = type } );
			foreach ( var activity in new[] { PeepState.Deciding, PeepState.InQueue, PeepState.Riding } )
			{
				subject.SetState( activity, 0, new Random( 1 ) );
				Assert.AreEqual( type is 0 or 1 or 3 or 9 or 10 ? 1 : 0, people.GateGuestCensus,
					$"type {type}, activity {activity}; save/occupancy were not moved" );
			}
		}
	}

	private static void OutsideGuests( ParkPeople people, ParkState state )
	{
		state.SetRecord( 0, 0, state.Record( 0, 0 ) with { Type = 30 } );
		foreach ( var guest in people.Peeps ) guest.Navigator.Position = FixedVector.AtCell( 0, 0 );
		state.SetRecord( 1, 1, state.Record( 1, 1 ) with { Type = 1 } );
		foreach ( var staff in people.Staff ) staff.Navigator.Position = FixedVector.AtCell( 1, 1 );
	}

	[TestMethod]
	public void PopulatedDoorWaitsForLastGuestAndThirtyWorldSweeps()
	{
		var (people, state, gate) = Park( empty: false );
		OutsideGuests( people, state );
		people.Peeps[0].Navigator.Position = FixedVector.AtCell( 1, 1 );
		Assert.AreEqual( 1, people.GateGuestCensus );
		state.SetParkClosed( true );
		Assert.AreEqual( 1, gate["VAR_COMMAND"], "one guest still inside" );
		while ( state.GameTick % 30 != 0 ) state.AdvanceGameTick();
		people.RetryGateClose();
		Assert.AreEqual( 1, gate["VAR_COMMAND"], "periodic check also waits for guests" );
		people.Peeps[0].Navigator.Position = FixedVector.AtCell( 0, 0 );
		for ( var i = 1; i <= 30; ++i )
		{
			state.AdvanceGameTick();
			people.RetryGateClose();
			Assert.AreEqual( i == 30 ? 0 : 1, gate["VAR_COMMAND"], $"sweep {i}" );
		}
	}

	[TestMethod]
	public void DeferredCloseWaitsForEveryStaffKindRegardlessOfActivityButDoorDoesNot()
	{
		var (people, state, gate) = Park( empty: false );
		OutsideGuests( people, state );
		people.Staff[0].Navigator.Position = FixedVector.AtCell( 0, 0 );
		state.SetParkClosed( true );
		Assert.AreEqual( 0, gate["VAR_COMMAND"], "immediate door ignores outside staff" );
		while ( state.GameTick % 30 != 0 ) state.AdvanceGameTick();
		foreach ( var staff in people.Staff )
		{
			OutsideGuests( people, state );
			staff.Navigator.Position = FixedVector.AtCell( 0, 0 );
			staff.SetActivity( StaffActivity.Walking, state.GameTick );
			gate.Set( "VAR_COMMAND", 1 );
			Assert.AreEqual( 1, people.GateStaffOutside, $"kind {staff.Model}" );
			people.RetryGateClose();
			Assert.AreEqual( 1, gate["VAR_COMMAND"], $"outside kind {staff.Model}, activity {staff.Activity}" );
			staff.Navigator.Position = FixedVector.AtCell( 1, 1 );
			people.RetryGateClose();
			Assert.AreEqual( 0, gate["VAR_COMMAND"] );
		}
	}

	[TestMethod]
	public void FiredOutsideStaffNoLongerBlockTheRetry()
	{
		var (people, state, gate) = Park( empty: false );
		OutsideGuests( people, state );
		var staff = people.Staff[0];
		staff.Navigator.Position = FixedVector.AtCell( 0, 0 );
		state.SetParkClosed( true );
		gate.Set( "VAR_COMMAND", 1 );
		while ( state.GameTick % 30 != 0 ) state.AdvanceGameTick();
		people.RetryGateClose();
		Assert.AreEqual( 1, gate["VAR_COMMAND"] );
		Assert.IsTrue( people.Fire( staff.ThingId ) );
		people.RetryGateClose();
		Assert.AreEqual( 0, gate["VAR_COMMAND"] );
	}

	[TestMethod]
	public void RealWorldUpdateWiresRetryToIncrementedWorldClock()
	{
		var (people, state, gate) = Park();
		gate.Set( "VAR_STATUS", 0 );
		state.SetParkClosed( true );
		gate.Set( "VAR_STATUS", 1 );
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0 );
		GameClock.Update( false, GameClock.ParkCatchUp );
		var before = state.GameTick;
		Assert.AreNotEqual( 0, before % 30, "fixture must distinguish world clock from zero-based frame clock" );
		while ( state.GameTick < before + 65 )
		{
			gate.Set( "VAR_COMMAND", 1 );
			var old = state.GameTick;
			Time.Update( .031f );
			GameClock.Update( false, GameClock.ParkCatchUp );
			people.Update();
			Assert.AreEqual( state.GameTick != old && state.GameTick % 30 == 0 ? 0 : 1,
				gate["VAR_COMMAND"], $"tick {state.GameTick}" );
		}
	}

	[TestMethod]
	public void RetryRequiresClosedParkAndOpenGateAndCensusIsReadOnly()
	{
		var (people, state, gate) = Park();
		while ( state.GameTick % 30 != 0 ) state.AdvanceGameTick();
		people.RetryGateClose();
		Assert.AreEqual( 1, gate["VAR_COMMAND"], "an open park never retries close" );
		gate.Set( "VAR_STATUS", 0 );
		state.SetParkClosed( true );
		people.RetryGateClose();
		Assert.AreEqual( 1, gate["VAR_COMMAND"], "a shut gate is not commanded again" );
		var before = gate.Variables.ToArray();
		_ = people.GateDescription();
		CollectionAssert.AreEqual( before, gate.Variables.ToArray() );
		gate.Set( "VAR_STATUS", 1 );
		people.RetryGateClose();
		Assert.AreEqual( 0, gate["VAR_COMMAND"], "positive control" );
	}

	[TestMethod]
	public void MissingSavedGateStateInitializesNormallyInsteadOfPreservingEndCommand()
	{
		var world = World();
		var handle = world.Objects.Single( o => o.ThingId == world.ParkGates ).RideScript;
		Assert.IsTrue( ((Dictionary<int, SavedScript>)world.ScriptStates.ByHandle).Remove( handle ) );
		var seed = new Seed( world, closed: true, saved: true, empty: true );
		var rides = new ParkRides( "jungle", seed, new ParkItemCatalogue( "jungle", data ), data );
		made.Add( rides );
		var gate = rides.Scheduler.Find( rides.ScriptFor( seed.ParkGates ) )!;
		Assert.IsNotNull( gate );
		Assert.AreEqual( 0, gate["VAR_COMMAND"] );
		Run( gate, 0 );
		gate.Set( "VAR_COMMAND", 1 );
		Run( gate, 20000 );
		Assert.AreEqual( 1, gate["VAR_STATUS"] );
	}

	[TestMethod]
	public void FreshClosedGateNeverEntersTheTerminalSequence()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), catalogue, data );
		var seed = new Seed( fresh, closed: true, saved: false, empty: true );
		var rides = new ParkRides( "jungle", seed, catalogue, data );
		made.Add( rides );
		var gate = rides.Scheduler.Find( rides.ScriptFor( seed.ParkGates ) )!;
		Assert.IsNotNull( gate );
		Assert.AreEqual( 0, gate["VAR_COMMAND"] );
		Run( gate, 0 );
		Assert.AreEqual( 0, gate["VAR_STATUS"] );
		gate.Set( "VAR_COMMAND", 1 );
		Run( gate, 20000 );
		Assert.AreEqual( 1, gate["VAR_STATUS"] );
	}

	[TestMethod]
	[DataRow( 0 )]
	[DataRow( 1 )]
	public void SavedClosedGatePreservesItsPendingCommandAndProgramCounter( int command )
	{
		var world = World();
		var record = world.Objects.Single( o => o.ThingId == world.ParkGates );
		var saved = world.ScriptStates.For( record.RideScript )!.Value;
		using var stream = data.OpenRead( "levels/jungle/features/gates/Gates.RSE" );
		var names = new RideScript( new RideScriptFile( stream ) );
		// A closed park can still be opening its gate while guests leave. Keep that pending command.
		saved.Variables[names.IndexOf( "VAR_COMMAND" )] = command;
		var seed = new Seed( world, closed: true, saved: true, empty: true );
		var rides = new ParkRides( "jungle", seed, new ParkItemCatalogue( "jungle", data ), data );
		made.Add( rides );
		var gate = rides.Scheduler.Find( rides.ScriptFor( seed.ParkGates ) )!;
		Assert.IsNotNull( gate );
		Assert.AreEqual( command, gate["VAR_COMMAND"] );
		Assert.AreEqual( saved.Position, gate.Position );
		CollectionAssert.AreEqual( saved.Variables, gate.Variables.ToArray() );
	}
}
