using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, a vehicle made at its summons (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a vehicle
/// made at its summons"): the thing the summons makes is written whole, an object on the corner cell, and the
/// header names it in its size's slot and as the current vehicle.
/// </summary>
[TestClass]
public class ParkFileWriterMadeVehicleTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";
	private const int FerryItem = 1604, Bus = 15;

	private BaseFileSystem data = null!;
	private BaseFileSystem? oldSaves;
	private ParkFixedItems? oldItems;
	private string? root;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;
	private readonly List<Entity> made = [];
	private int scriptsBeforeTheSummons;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = FileSystem = GameData.Required();
		shipped = Read( data.ReadAllBytes( ShippedPark ), out _ );
		catalogue = new ParkItemCatalogue( Theme, data );

		oldSaves = SaveFileSystemOrNull;
		oldItems = ParkFixedItems.Current;
		root = Directory.CreateTempSubdirectory( "opentpw-made-vehicle-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
		TestRun.DeleteEvery<ParkPeople>();

		// Without the game the set-up stops before anything is changed.
		if ( root != null )
		{
			typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, null );
			typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, oldItems );
			SaveFileSystemOrNull = oldSaves;
			Directory.Delete( root, true );
		}

		ParkOrbitCameraMode.Forget();
		Unimplemented.Forget();
	}

	private static ParkWorld Read( byte[] file, out byte[] body )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );

		body = reader.ReadFile();

		return new ParkWorld( body, reader.Preamble );
	}

	private int ChannelsFor( int id ) => catalogue.ChannelsFor( id );

	private static int Times( string gap ) => Unimplemented.Summary.Where( counted => counted.What == gap ).Sum( counted => counted.Times );

	/// <summary>The park's fixed items as the load leaves them: the bus the file's thing, the two others not made.</summary>
	private static void StandVehicles()
	{
		var items = (ParkFixedItems)RuntimeHelpers.GetUninitializedObject( typeof( ParkFixedItems ) );

		typeof( ParkFixedItems ).GetField( "_thingOf", BindingFlags.NonPublic | BindingFlags.Instance )!
			.SetValue( items, new Dictionary<string, int> { ["bus"] = Bus } );

		typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, items );
	}

	/// <summary>One sweep of the park, through both clocks as a frame runs them.</summary>
	private static void Sweep( ParkPeople people )
	{
		var from = people.State.GameTick;

		for ( var frame = 0; frame < 10 && people.State.GameTick == from; ++frame )
		{
			Time.Update( 0.1f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );
			people.Update();
		}
	}

	/// <summary>
	/// The shipped park, its ferry not made, a load of sixty-one forced and swept until the ferry is summoned, and so
	/// made.
	/// </summary>
	private (ParkPeople People, ParkRides Rides, int Ferry) FerrySummoned( Action<ParkPeople>? first = null )
	{
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var state = new ParkState( shipped );
		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );
		StandVehicles();

		Assert.IsTrue( catalogue.TryGet( FerryItem, out _ ), "the jungle's catalogue has the ferry" );

		var people = new ParkPeople( shipped, new ParkBalance( Theme, easyMode: true ), () => ParkRides.GateIsOpen, state, catalogue,
			thing => rides.Scheduler.Find( rides.ScriptFor( thing ) ), random: new Random( 5 ), behaviourRandom: new Random( 5 ),
			rideRandom: new Random( 5 ), staffRandom: new Random( 5 ) );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		first?.Invoke( people );
		Assert.AreEqual( 3, people.ForceArrival( 61 ), "sixty-one come by the third vehicle" );
		scriptsBeforeTheSummons = rides.Scheduler.Scripts.Count();
		Sweep( people );
		Assert.AreEqual( 3, people.CurrentVehicle, "summoned on the sweep that finds none" );

		return (people, rides, people.VehicleThings[2]);
	}

	/// <summary>
	/// <b>The made ferry runs its own script, bound under its thing from its top</b> (<c>FUN_004db090</c> through
	/// <c>FUN_0051a2f0</c>): <c>Ferry.RSE</c> at word 0, given <c>VAR_STATUS</c> 1 and no trigger (<c>0x0051a5ed</c>), and
	/// no other script is made; the seaplane, not summoned, is not made.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no script bound; bound under another thing; triggered as a vehicle that was a thing; not given
	/// status 1; the seaplane made with it.
	/// </remarks>
	[TestMethod]
	public void TheMadeVehicleRunsItsOwnScriptFromItsTop()
	{
		var (_, rides, ferry) = FerrySummoned();
		var script = rides.Scheduler.Find( rides.ScriptFor( ferry ) );

		Assert.IsNotNull( script, "a script runs under the made thing" );
		Assert.AreEqual( (ferry, 2, 0), (script.ThingId, script.Variables.Count, script.Position), "naming it, Ferry.RSE's two variables, at its top" );
		Assert.AreEqual( (0, 1), (script["VAR_TRIGGER"], script["VAR_STATUS"]), "given status 1, not triggered" );
		Assert.AreEqual( scriptsBeforeTheSummons + 1, rides.Scheduler.Scripts.Count(), "and no other script is made" );
		Assert.AreEqual( ferry, ParkFixedItems.Current!.ThingFor( "ferry" ) );
		Assert.AreEqual( 0, ParkFixedItems.Current.ThingFor( "seaplane" ), "the seaplane, not summoned, is not made" );
	}

	/// <summary>
	/// <b>A vehicle summoned on a departed guest's id is written whole, and the guest is gone</b>: the summons takes the
	/// free list's head, which is the departed guest's id; the file holds the ferry as an object on that id at cell (0,0),
	/// no person on it, and names it the current arrival vehicle.
	/// </summary>
	[TestMethod]
	public void AVehicleMadeOnADepartedGuestsIdIsWrittenWhole()
	{
		var guest = 0;
		var (people, rides, ferry) = FerrySummoned( running =>
		{
			guest = running.Peeps.First( peep => !PeepBehaviour.HeldByAThing( peep ) ).ThingId;
			Assert.IsTrue( running.Depart( guest ) );
			running.State.FreeDeletedThings();
		} );

		Assert.AreEqual( guest, ferry, "the summons takes the free list's head" );
		Assert.IsNotNull( Level.WritePark( shipped, people.State, Theme, "FerryOnAGuest", people, null, rides, catalogue ) );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "FerryOnAGuest.TPWS" ) ), out _ );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( (FerryItem, 0, 0), written.Objects.Where( thing => thing.ThingId == ferry ).Select( thing => (thing.CatalogueId, thing.CellX, thing.CellY) ).Single() );
		Assert.IsFalse( written.People.Any( person => person.ThingId == ferry ), "the guest is gone from the file" );
		Assert.AreEqual( ferry, written.CurrentArrivalVehicle );
	}

	/// <summary>
	/// <b>A park written after the summons holds the ferry as the original's own files hold one</b>
	/// (<c>q257x/orig/fixed-by-the-original.TPWS</c>, thing 32): the header's third slot and its current vehicle
	/// name the thing; its record is an object of item 1604 at the middle of cell (0,0), facing nought, state 3,
	/// its corner and both ends cell 1, its door open, named "Ferry", with its script and its model; it heads the
	/// object list and the corner cell's, the bus behind it. Nothing is counted. Read back, the park has the
	/// ferry current on that thing and makes nothing at its next summons.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the slot left nought; the second slot written; the current vehicle left nought; the object
	/// left out of the file; its record on another cell; its ends nought; the cell's list left the bus's; the script
	/// or the model not written; the bus's slot lost; a load of the file making the vehicle again.
	/// </remarks>
	[TestMethod]
	public void AParkWrittenAfterTheSummonsHoldsTheVehicleAsAThing()
	{
		var (people, rides, ferry) = FerrySummoned();
		var state = people.State;
		var stamp = state.LastObjectBuilt;

		Unimplemented.Forget();
		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, "Ferried", people, null, rides, catalogue ) );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Ferried.TPWS" ) ), out var body );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( (Bus, 0, ferry, ferry),
			(written.ArrivalVehicleForSmallCrowd, written.ArrivalVehicleForMediumCrowd, written.ArrivalVehicleForLargeCrowd, written.CurrentArrivalVehicle),
			"the bus's slot the file's, the second none, the third and the current vehicle the made thing" );

		foreach ( var gap in new[] { "SAVE_PARK_ARRIVAL_VEHICLE", "SAVE_PARK_OBJECT_BOUGHT", "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD", "SAVE_PARK_MADE_MODEL_TABLES", "VEHICLE_MADE_NO_ITEM" } )
			Assert.AreEqual( 0, Times( gap ), gap );

		// The record, as the original's own.
		var record = written.Objects.Single( thing => thing.ThingId == ferry );
		var handle = rides.ScriptFor( ferry );

		Assert.AreEqual( (FerryItem, 0, 0, 0, 0), (record.CatalogueId, record.CellX, record.CellY, record.Angle, (int)record.Flags) );
		Assert.AreEqual( (3, 1, 1, 1, 1), (record.State, record.CanLoad, (int)record.TopLeft, (int)record.EntryPos, (int)record.ExitPos) );
		Assert.AreEqual( (50, 100, 50, 100f, 100f), (record.QualityOfGoods, record.ChanceOfWinning, record.AmountOfSpecialIngredient, record.StateOfRepair, record.RemainingLife) );
		Assert.AreEqual( handle, record.RideScript, "its script's handle" );
		Assert.IsTrue( record.MeshInstance > 0, "and a model's" );

		// Its place and its name's first line, where the record lies: the middle of the corner cell, and "Ferry".
		var at = body.AsSpan().IndexOf( ParkWorld.MadeObjectRecord( new ParkWorld.MadeObject( record, "Ferry", "" ), record.MeshInstance ).AsSpan( 16, 44 ) ) - 16;

		Assert.IsTrue( at > 0, "the record's angle, item, date, model and flags lie together" );
		Assert.AreEqual( (0x0080, 0x0080), ((int)BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( at + 8 ) ), (int)BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( at + 10 ) )) );
		Assert.AreEqual( "Ferry", new string( [.. Enumerable.Range( 0, 5 ).Select( i => (char)BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( at + 60 + (i * 4) ) ) )] ) );
		Assert.AreEqual( 0, BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( at + 60 + (5 * 4) ) ), "ended" );

		// The lists: the newest object and the newest thing on the corner cell, the bus next on that cell.
		Assert.AreEqual( ferry, written.FirstObject );
		Assert.AreEqual( ferry, written.CellAt( 0, 0 ).Occupant );
		Assert.AreEqual( Bus, BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( at + 12 ) ), "the thing behind it on its cell is the bus" );

		// Its script and its model, whose record is a fixed item's: on (48,17), one cell square, flags 0x361.
		Assert.IsNotNull( written.ScriptStates.For( handle ), "the script's record" );

		var model = written.ThingStates( ChannelsFor ).ForScript( handle );

		Assert.AreEqual( (FerryItem, record.MeshInstance - 1), (model?.CatalogueId, model?.Slot), "its model's, by that script, in the slot its record names" );
		Assert.IsTrue( model?.NodeWords?.Length > 0, "with its node words" );
		Assert.IsTrue( body.AsSpan().IndexOf( Dwords( FerryItem, 48, 17, 1, 1, 0x361, handle ) ) > 0, "item, cell, footprint, flags and script lie together" );

		// Read back: the ferry is current on that thing, and a summons makes nothing more.
		TestRun.DeleteEvery<ParkPeople>();

		var again = new ParkPeople( written, new ParkBalance( Theme, easyMode: true ), () => ParkRides.GateIsOpen, new ParkState( written ), catalogue );

		Assert.AreEqual( 3, again.CurrentVehicle );
		Assert.AreEqual( (Bus, 0, ferry), (again.VehicleThings[0], again.VehicleThings[1], again.VehicleThings[2]) );
		Assert.AreEqual( stamp, again.State.LastObjectBuilt, "the file's stamp is the summons's" );

		again.ForceArrival( 61 );
		Sweep( again );
		Sweep( again );

		Assert.AreEqual( ferry, again.VehicleThings[2], "the file's thing still" );
		Assert.AreEqual( stamp, again.State.LastObjectBuilt, "and nothing is stamped" );
		Assert.AreEqual( written.Objects.Count, again.State.Objects.Count, "nor made" );
	}

	/// <summary>
	/// <b>A vehicle's model record is a fixed item's</b> (<c>FUN_004dd500</c>): flags <c>0x361</c>, on cell (48,17)
	/// and one cell square whatever its object's cell and its item's footprint, as the bus's, the seaplane's and
	/// the ferry's records are in the original's own files; a thing bought keeps its own cell, footprint and
	/// <c>0x32f</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> a vehicle's record with a bought thing's flags, on its object's cell, of its item's footprint; a bought thing's a fixed item's.</remarks>
	[TestMethod]
	public void AVehiclesModelRecordIsAFixedItems()
	{
		var vehicle = ParkThingStates.MadeRecord( FerryItem, 0, 0, 3, 4, 18, 0, 0f, 0, [], vehicle: true );
		var bought = ParkThingStates.MadeRecord( FerryItem, 5, 6, 3, 4, 18, 0, 0f, 0, [] );

		CollectionAssert.AreEqual( Dwords( FerryItem, 48, 17, 1, 1, 0x361, 18 ), vehicle[1..29] );
		CollectionAssert.AreEqual( Dwords( FerryItem, 5, 6, 3, 4, 0x32f, 18 ), bought[1..29] );
		Assert.AreEqual( bought.Length, vehicle.Length );
		CollectionAssert.AreEqual( bought[29..], vehicle[29..], "the rest is the same" );
	}

	private static byte[] Dwords( params int[] values ) => [.. values.SelectMany( BitConverter.GetBytes )];

	/// <summary>
	/// <b>A park with no item for the vehicle makes nothing, and the summons falls back to the bus the file names</b>
	/// (<c>FUN_0051a2f0</c>'s bitmask walk): the ferry asked for and the seaplane after it each said and counted, no
	/// thing made, nothing stamped, and the bus, a thing already, current and its two slots nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> a thing made of no item; the stamp moved all the same; nothing counted; no fallback, so
	/// the ferry current with no thing; the bus tried before the seaplane.</remarks>
	[TestMethod]
	public void WithNoItemForTheVehicleTheSummonsFallsBackToTheBus()
	{
		var state = new ParkState( shipped );
		var people = new ParkPeople( shipped, new ParkBalance( Theme, easyMode: true ), () => ParkRides.GateIsOpen, state );
		var stamp = state.LastObjectBuilt;
		var missing = new List<string>();
		Logger.LogDelegate note = ( _, text ) =>
		{
			if ( text.Contains( "Could not find the '" ) )
				missing.Add( text[(text.IndexOf( '\'' ) + 1)..text.LastIndexOf( '\'' )] );
		};

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		Logger.OnLog += note;

		try
		{
			people.ForceArrival( 61 );
			Sweep( people );
		}
		finally
		{
			Logger.OnLog -= note;
		}

		Assert.AreEqual( 1, people.CurrentVehicle, "the bus" );
		CollectionAssert.AreEqual( new[] { "ferry", "seaplane" }, missing, "after the ferry and the seaplane, in that order" );
		Assert.AreEqual( (Bus, 0, 0), (people.VehicleThings[0], people.VehicleThings[1], people.VehicleThings[2]) );
		Assert.AreEqual( stamp, state.LastObjectBuilt );
		Assert.AreEqual( shipped.Objects.Count, state.Objects.Count );
		Assert.AreEqual( 2, Times( "VEHICLE_MADE_NO_ITEM" ) );
	}

	/// <summary>
	/// <b>With the thing table full the summons makes no vehicle and falls back to the bus the file names</b>
	/// (<see cref="ParkState.NextThingId"/>): no thing made, nothing stamped, the two slots nought. The original falls
	/// over here (<c>FUN_004d91f0</c>).
	/// </summary>
	/// <remarks><b>Mutation:</b> the ferry made as thing nought.</remarks>
	[TestMethod]
	public void WithTheThingTableFullTheSummonsMakesNoVehicle()
	{
		var state = new ParkState( shipped );
		var people = new ParkPeople( shipped, new ParkBalance( Theme, easyMode: true ), () => ParkRides.GateIsOpen, state, catalogue );
		var stamp = state.LastObjectBuilt;

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		// Bounded, so a table with no end fails rather than spins.
		for ( var taken = 0; taken <= ParkState.LastThingId && state.NextThingId() != 0; ++taken )
		{
		}

		people.ForceArrival( 61 );
		Sweep( people );

		Assert.AreEqual( 1, people.CurrentVehicle, "the bus" );
		Assert.AreEqual( (Bus, 0, 0), (people.VehicleThings[0], people.VehicleThings[1], people.VehicleThings[2]) );
		Assert.AreEqual( stamp, state.LastObjectBuilt );
		Assert.AreEqual( shipped.Objects.Count, state.Objects.Count );
	}

	/// <summary>
	/// <b>A theme without the ferry's item has the seaplane made for a load of sixty-one</b> (<c>FUN_0051a2f0</c>'s walk
	/// from the ferry to the seaplane): the ferry said and counted once, the seaplane made a thing in the second slot and
	/// current, its own script bound under it at status 1, and the third slot nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the made vehicle not made current; the bus tried before the seaplane; no fallback.</remarks>
	[TestMethod]
	public void WithNoFerryTheSeaplaneIsMadeForTheLoad()
	{
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var items = (Dictionary<int, ParkItemCatalogue.Item>)typeof( ParkItemCatalogue )
			.GetField( "_items", BindingFlags.NonPublic | BindingFlags.Instance )!.GetValue( catalogue )!;

		Assert.IsTrue( items.Remove( FerryItem ), "the ferry taken out of this theme" );

		var state = new ParkState( shipped );
		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );
		StandVehicles();

		var people = new ParkPeople( shipped, new ParkBalance( Theme, easyMode: true ), () => ParkRides.GateIsOpen, state, catalogue,
			thing => rides.Scheduler.Find( rides.ScriptFor( thing ) ), random: new Random( 5 ), behaviourRandom: new Random( 5 ),
			rideRandom: new Random( 5 ), staffRandom: new Random( 5 ) );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		Assert.AreEqual( 3, people.ForceArrival( 61 ) );
		Sweep( people );

		var seaplane = people.VehicleThings[1];

		Assert.AreEqual( 2, people.CurrentVehicle, "the seaplane, current" );
		Assert.AreEqual( (Bus, 0), (people.VehicleThings[0], people.VehicleThings[2]), "made in the second slot alone" );
		Assert.IsTrue( state.TryObject( seaplane, out var made2 ) && made2.CatalogueId == 1602, $"thing {seaplane} is the seaplane's item" );
		Assert.AreEqual( 1, rides.Scheduler.Find( rides.ScriptFor( seaplane ) )?["VAR_STATUS"], "its own script, at status 1" );
		Assert.AreEqual( 1, Times( "VEHICLE_MADE_NO_ITEM" ), "the ferry counted once" );
	}

	/// <summary>
	/// <b>A made vehicle's script is bound to the animations its model will stand with</b>: <see cref="ParkFixedItems.Make"/>
	/// loads them in the sweep and keeps them for <see cref="ParkFixedItems.Sync"/>, which stands the model outside the
	/// frame's walk, and the script plays on that same set, so what it triggers is what the drawn model is posed from.
	/// </summary>
	/// <remarks><b>Mutations:</b> the script given a set of its own; the thing not recorded.</remarks>
	[TestMethod]
	public void AMadeVehiclesScriptPlaysTheAnimationsItsModelWillStandWith()
	{
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var state = new ParkState( shipped );
		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );
		StandVehicles();

		// A park to stand into, which Make only keeps the vehicle for: Sync, which makes the model's entities, is not run.
		var items = ParkFixedItems.Current!;
		var flags = BindingFlags.NonPublic | BindingFlags.Instance;

		typeof( ParkFixedItems ).GetField( "_objects", flags )!.SetValue( items, RuntimeHelpers.GetUninitializedObject( typeof( ParkObjects ) ) );
		typeof( ParkFixedItems ).GetField( "_features", flags )!.SetValue( items, $"levels/{Theme}/features" );
		typeof( ParkFixedItems ).GetField( "_owed", flags )!.SetValue( items, new List<(string, int, int, RideAnimations)>() );

		var people = new ParkPeople( shipped, new ParkBalance( Theme, easyMode: true ), () => ParkRides.GateIsOpen, state, catalogue,
			thing => rides.Scheduler.Find( rides.ScriptFor( thing ) ), random: new Random( 5 ), behaviourRandom: new Random( 5 ),
			rideRandom: new Random( 5 ), staffRandom: new Random( 5 ) );

		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		people.ForceArrival( 61 );
		Sweep( people );

		var ferry = people.VehicleThings[2];
		var owed = (List<(string Item, int Thing, int CatalogueId, RideAnimations Animations)>)typeof( ParkFixedItems ).GetField( "_owed", flags )!.GetValue( items )!;

		Assert.AreEqual( 1, owed.Count, "one model owed" );
		Assert.AreEqual( ("ferry", ferry, FerryItem), (owed[0].Item, owed[0].Thing, owed[0].CatalogueId) );
		Assert.AreSame( owed[0].Animations, rides.Scheduler.Find( rides.ScriptFor( ferry ) )!.Animations, "the script plays the set the model will stand with" );
		Assert.AreEqual( ferry, items.ThingFor( "ferry" ) );
	}

	/// <summary>
	/// <b>The order a summons tries the vehicles in</b>: the one asked for, then the ferry, the seaplane and the bus of
	/// the others (<c>FUN_0051a2f0</c>, bits 4, 2 and 1).
	/// </summary>
	/// <remarks><b>Mutations:</b> the others in the sizes' order; the asked one tried twice; one left out.</remarks>
	[TestMethod]
	public void TheSummonsTriesTheFerryTheSeaplaneAndTheBusAfterTheOneAskedFor()
	{
		CollectionAssert.AreEqual( new[] { 1, 3, 2 }, ParkPeople.FallbackOrder( 1 ).ToArray(), "the bus, then the ferry and the seaplane" );
		CollectionAssert.AreEqual( new[] { 2, 3, 1 }, ParkPeople.FallbackOrder( 2 ).ToArray(), "the seaplane, then the ferry and the bus" );
		CollectionAssert.AreEqual( new[] { 3, 2, 1 }, ParkPeople.FallbackOrder( 3 ).ToArray(), "the ferry, then the seaplane and the bus" );
	}
}
