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
		public IReadOnlyList<ParkWorld.ResearchProject> ResearchProjects => source.ResearchProjects;
		public IReadOnlyList<ParkWorld.ResearchKind> ResearchKinds => source.ResearchKinds;
		public int ResearchWorkLoad => source.ResearchWorkLoad;
		public int ResearchMostExpensiveProject => source.ResearchMostExpensiveProject;
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

	private (ParkPeople People, ParkState State, RideScript Gate) Park( bool empty = true, Dictionary<int, RideScript>? others = null )
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
		var people = new ParkPeople( world, state: state, scriptFor: id => id == world.ParkGates ? gate : others?.GetValueOrDefault( id ) );
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
			ParkFrame.Run( people );
			Assert.AreEqual( state.GameTick != old && state.GameTick % 30 == 0 ? 0 : 1,
				gate["VAR_COMMAND"], $"tick {state.GameTick}" );
		}
	}

	/// <summary>
	/// <b>The retry is the sweep's after the advisor's turn</b> (FUN_00516380 walks the things, him among them, then tries
	/// the close at 0x0051643b; docs/exe/park-gate.md, "The later close check"). The closed park's message 0x81, posted by
	/// the door, is said on the next sweep's turn of his; that sweep lands on a multiple of 30, so the retry runs on it
	/// too. No word either turn reads is written by the other, so the console's order is what shows it: his line, then
	/// the gate's, both on tick 780, and the gate commanded shut.
	/// </summary>
	/// <remarks><b>Mutation:</b> the retry back before his turn (after the rides') logs the gate first and fails.</remarks>
	[TestMethod]
	public void TheRetryIsLoggedAfterTheAdvisorSpeaksOnTheSameSweep()
	{
		using var clock = new SimulationClockScope();
		var (people, state, gate) = Park();
		// The stock file scores the closed park's message 20, under its threshold's 25, so he would never say it:
		// the score is put at 26, as an Advisor.sam may have it.
		var settings = new SettingsFile( "Advisor/Advisor.sam" );
		var index = settings.Entries.FindIndex( pair => pair.Key == "ParkNowClosed.Score" );
		Assert.AreEqual( "20", settings.Entries[index].Value, "the stock score" );
		settings.Entries[index] = new SettingsPair( settings.Entries[index].Key, "26" );
		state.AdvisorMessages = new ParkAdvisorMessages( settings );

		// The door's close finds the gate shut and writes nothing; the retry is left to command it.
		gate.Set( "VAR_STATUS", 0 );
		state.SetParkClosed( true );
		Assert.AreEqual( 1, gate["VAR_COMMAND"], "the door wrote nothing" );
		gate.Set( "VAR_STATUS", 1 );
		state.SetGameTick( 779 );

		var lines = new List<string>();
		Logger.LogDelegate listen = ( _, text ) =>
		{
			if ( text.StartsWith( "Park advisor: attempt=" ) || text.StartsWith( "Gate retry:" ) )
				lines.Add( text );
		};
		Logger.OnLog += listen;
		try { people.Sweep( 0 ); }
		finally { Logger.OnLog -= listen; }

		Assert.AreEqual( 780, state.GameTick );
		Assert.AreEqual( 2, lines.Count, string.Join( "\n", lines ) );
		StringAssert.StartsWith( lines[0], "Park advisor: attempt=1 message=129 " );
		StringAssert.Contains( lines[0], " tick=780 " );
		StringAssert.StartsWith( lines[1], "Gate retry: " );
		StringAssert.Contains( lines[1], " tick 780 " );
		Assert.AreEqual( 0, gate["VAR_COMMAND"], "the retry commanded the gate shut" );
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

	/// <summary>
	/// <b>A gate whose saved script is missing is commanded from the park's flag in an open park too.</b> The rule is
	/// docs/exe/park-gate.md: "A missing or invalid saved script falls back to initializing the gate command from the
	/// park flag", and an open park's command is 1 (the open request's write, <c>0x00519f76</c>). The closed case
	/// above cannot tell the fallback from none, since a fresh script's command is already 0. Every other thing this
	/// fixture binds has its saved script, so the gate is the one script left at its beginning.
	/// </summary>
	[TestMethod]
	public void MissingSavedGateStateInAnOpenParkIsCommandedOpen()
	{
		var world = World();
		var handle = world.Objects.Single( o => o.ThingId == world.ParkGates ).RideScript;
		Assert.IsTrue( ((Dictionary<int, SavedScript>)world.ScriptStates.ByHandle).Remove( handle ) );
		var seed = new Seed( world, closed: false, saved: true, empty: true );
		var rides = new ParkRides( "jungle", seed, new ParkItemCatalogue( "jungle", data ), data );
		made.Add( rides );
		var gate = rides.Scheduler.Find( rides.ScriptFor( seed.ParkGates ) )!;
		Assert.IsNotNull( gate );
		Assert.AreEqual( 1, gate["VAR_COMMAND"], "an open park's gate with no saved script is commanded open" );
		Assert.AreEqual( 1, rides.NotResumed, "and the gate is the one script left at its beginning" );
	}

	/// <summary>
	/// Pins that the census counts a guest on the map's edge cells. <c>FUN_004fa990</c> makes <c>y*128+x+1</c> from
	/// the position's cell bytes and asks that cell's type, accepting {0, 1, 3, 9, 10} (docs/exe/park-gate.md, "Which
	/// people count as inside"): column 0, row 0 and cell (127, 127) are cells like any other, so one guest on a
	/// type 1 cell there is a census of 1, with every other guest on a type 30 cell.
	/// </summary>
	[TestMethod]
	public void CensusCountsAGuestOnTheMapsFirstColumnFirstRowAndLastCell()
	{
		var (people, state, _) = Park( empty: false );
		OutsideGuests( people, state );
		Assert.AreEqual( 0, people.GateGuestCensus, "every guest starts on the type 30 cell" );
		var subject = people.Peeps[0];
		foreach ( var (x, y) in new[] { (0, 5), (5, 0), (127, 127) } )
		{
			state.SetRecord( x, y, state.Record( x, y ) with { Type = 1 } );
			subject.Navigator.Position = FixedVector.AtCell( x, y );
			Assert.AreEqual( 1, people.GateGuestCensus, $"cell ({x}, {y})" );
		}
	}

	/// <summary>
	/// The census reads a position off the 128 x 128 map as the original's byte pack does (Q1333,
	/// <see cref="PeepBehaviour.CountingRecord"/>): x -1 on row 5 is the cell (127, 6) and x 128 is (0, 6), so each
	/// counts by that cell's type; y -1 and y 128 pack past the map's last record and count nobody.
	/// </summary>
	/// <remarks><b>Mutations:</b> every position off the map counting nobody (the old guard); the same row's cell.</remarks>
	[TestMethod]
	public void CensusReadsAPositionOffTheMapByTheOriginalsBytePack()
	{
		var (people, state, _) = Park( empty: false );
		OutsideGuests( people, state );
		var subject = people.Peeps[0];
		subject.Navigator.Position = FixedVector.AtCell( 1, 1 );
		Assert.AreEqual( 1, people.GateGuestCensus, "on the type 1 cell the guest is counted" );

		void Type( int x, int y, int type ) => state.SetRecord( x, y, state.Record( x, y ) with { Type = type } );
		int At( int x, int y )
		{
			subject.Navigator.Position = FixedVector.AtCell( x, y );
			return people.GateGuestCensus;
		}

		Type( 127, 5, 4 );
		Type( 0, 5, 4 );
		Type( 127, 6, 1 );
		Type( 0, 6, 4 );
		Assert.AreEqual( (1, 0), (At( -1, 5 ), At( 128, 5 )), "(127, 6) a path, (0, 6) a footprint" );

		Type( 127, 6, 4 );
		Type( 0, 6, 1 );
		Assert.AreEqual( (0, 1), (At( -1, 5 ), At( 128, 5 )), "(127, 6) a footprint, (0, 6) a path" );

		Assert.AreEqual( (0, 0), (At( 5, -1 ), At( 5, 128 )), "past the map's last record" );
	}

	/// <summary>
	/// <b>The end of the park</b> (<c>FUN_005168f0</c>): world state 4, <c>VAR_BREAKSTAT</c> 1 on every object a guest may be
	/// offered and on nothing else, and the gate's <c>VAR_COMMAND</c> 2 (<c>FUN_00519ef0( 0, 1 )</c>), the door itself not moved.
	/// </summary>
	/// <remarks><b>Mutations:</b> the gate given 0 (the door's close); the door shut; every object broken; none.</remarks>
	[TestMethod]
	public void TheEndBreaksTheRidesGuestsChooseAndCommandsTheGateTwo()
	{
		var world = World();
		var others = new Dictionary<int, RideScript>();

		foreach ( var thing in world.Objects.Where( o => o.ThingId != world.ParkGates ) )
		{
			using var rse = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
			others[thing.ThingId] = new RideScript( new RideScriptFile( rse ) );
		}

		var (people, state, gate) = Park( others: others );
		var visitable = state.ObjectsInChainOrder().Where( o => o.IsVisitable ).Select( o => o.ThingId ).ToHashSet();
		var other = others.Keys.Where( id => !visitable.Contains( id ) ).ToArray();

		Assert.IsTrue( visitable.Count > 0 && other.Length > 0, $"{visitable.Count} visitable, {other.Length} not" );
		Assert.IsTrue( others.Values.All( script => script[ParkRideOperation.BreakStatVariable] == 0 ) );

		state.EndThePark();

		Assert.AreEqual( (ParkState.EndedWorldState, 2, false), (state.WorldState, gate["VAR_COMMAND"], state.ParkIsClosed) );
		Assert.IsTrue( visitable.All( id => others[id][ParkRideOperation.BreakStatVariable] == 1 ), "every object a guest may be offered" );
		Assert.IsTrue( other.All( id => others[id][ParkRideOperation.BreakStatVariable] == 0 ), "and nothing else" );
	}

	/// <summary>
	/// <b>In world state 4 no thing takes a turn, and the clock goes on</b> (<c>0x0051639e</c>): the stock park's guests,
	/// who walk in when it runs, stand where they were over twenty sweeps while <c>mGameTick</c> counts twenty.
	/// </summary>
	/// <remarks><b>Mutations:</b> the guests' turns not skipped; the clock skipped with them.</remarks>
	[TestMethod]
	public void InAnEndedParkNobodyTakesATurnAndTheClockGoesOn()
	{
		foreach ( var ended in new[] { false, true } )
		{
			var (people, state, _) = Park( empty: false );

			if ( ended )
				state.EndThePark();

			var before = people.Peeps.Select( peep => peep.Navigator.Position ).ToArray();
			var tick = state.GameTick;

			for ( var i = 1; i <= 20; ++i )
				people.Sweep( i * ParkPeople.ThingTickEvery );

			var moved = people.Peeps.Select( peep => peep.Navigator.Position ).Zip( before ).Count( pair => pair.First != pair.Second );

			Assert.AreEqual( tick + 20, state.GameTick, $"ended {ended}: the clock" );
			Assert.AreEqual( ended, moved == 0, $"ended {ended}: {moved} of {before.Length} guests moved" );

			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The weather turns in no sweep that began in world state 4</b>, and does in the sweep whose tail ends the park,
	/// whose walk had turned it already (<c>0x0051639e</c>; <see cref="ParkLoop.Tail"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the weather turned whatever the state; the state read after the calendar.</remarks>
	[TestMethod]
	public void TheWeatherTurnsInNoSweepOfAnEndedPark()
	{
		var (people, state, _) = Park();
		var weather = new ParkWeather( null, drawn: false );

		try
		{
			ParkLoop.Tail( people, null, weather );
			ParkLoop.Tail( people, _ => state.EndThePark(), weather );
			Assert.AreEqual( 2, weather.Turns, "the sweep whose tail ends the park turns it" );

			ParkLoop.Tail( people, null, weather );
			Assert.AreEqual( 2, weather.Turns, "an ended park's does not" );
		}
		finally
		{
			weather.Delete();
		}
	}
}
