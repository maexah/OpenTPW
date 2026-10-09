using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// An entertainer's and a guard's region effect: stamped where they are hired, moved on the sweep that finds them on
/// another cell, taken off as they are fired, and written with the cell it stands round
/// (<c>docs/exe/ride-operation.md</c>, "The region effects"; <c>docs/exe/saves.md</c>, "OpenTPW's writer, the
/// staff's region effects").
/// </summary>
[TestClass]
public class ParkStaffRegionEffectsTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>The shipped park's entertainer, on (47,25), its guard, on (39,28), and its mechanic.</summary>
	private const int Entertainer = 27;
	private const int Guard = 28;
	private const int Mechanic = 26;

	private const int Happiness = 0;
	private const int Security = 3;

	private ParkWorld shipped = null!;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void Forget()
	{
		TestRun.DeleteEvery<ParkPeople>();
		Unimplemented.Forget();
	}

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	private static int Sum( ParkState state, int word )
		=> Enumerable.Range( 0, state.Effects.Count / ParkWorld.EffectWords ).Sum( cell => state.Effects[(cell * ParkWorld.EffectWords) + word] );

	private static void PutOn( Staff member, int x, int y )
	{
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );
		member.Navigator.Target = member.Navigator.Position;
	}

	private static ParkStaffPool.Candidate Candidate( int kind )
		=> new( Id: 999, Kind: kind, Name: "Ada Test", Grade: 1, Costume: 0, Wage: 60 );

	/// <summary>Every cell's five words rebuilt from the file's own objects and its entertainers and guards.</summary>
	private static short[] Rebuilt( ParkWorld park )
	{
		var grid = new short[ParkWorld.MapSize * ParkWorld.MapSize * ParkWorld.EffectWords];
		var effects = ParkRegionEffects.Standard;

		foreach ( var thing in park.Objects )
		{
			foreach ( var effect in ParkRegionEffects.Of( thing ) )
				effects.Stamp( grid, effect, thing.CellX, thing.CellY );
		}

		foreach ( var person in park.People )
		{
			if ( ParkRegionEffects.OfStaff( person.Model ) is { } effect && MapStep.IsCellId( person.LastRecordedMapId ) )
			{
				var (x, y) = MapStep.CellAt( person.LastRecordedMapId );

				effects.Stamp( grid, effect, x, y );
			}
		}

		return grid;
	}

	[TestMethod]
	public void OnlyAnEntertainerAndAGuardCarryAnEffect()
	{
		Assert.AreEqual( ParkRegionEffects.Entertainer, ParkRegionEffects.OfStaff( 6 ) );
		Assert.AreEqual( ParkRegionEffects.Guard, ParkRegionEffects.OfStaff( 7 ) );

		foreach ( var model in new[] { 1, 4, 5, 8 } )
			Assert.IsNull( ParkRegionEffects.OfStaff( model ), $"model {model}" );
	}

	[TestMethod]
	public void TheFilesRecordedCellIsRead()
	{
		var recorded = shipped.People.ToDictionary( person => person.ThingId, person => person.LastRecordedMapId );

		Assert.AreEqual( MapStep.CellId( 47, 25 ), recorded[Entertainer] );
		Assert.AreEqual( MapStep.CellId( 39, 28 ), recorded[Guard] );
		Assert.AreEqual( MapStep.CellId( 48, 28 ), recorded[Mechanic], "the cell the mechanic was made on, not the one they stand on" );

		var people = new ParkPeople( shipped );

		Assert.AreEqual( recorded[Entertainer], people.Staff.Single( member => member.ThingId == Entertainer ).RecordedCell );
		Assert.AreEqual( recorded[Mechanic], people.Staff.Single( member => member.ThingId == Mechanic ).RecordedCell );
	}

	[TestMethod]
	public void TheEffectMovesToTheCellTheyAreFoundOn()
	{
		var people = new ParkPeople( shipped );
		var state = people.State;
		var entertainer = people.Staff.Single( member => member.ThingId == Entertainer );
		var guard = people.Staff.Single( member => member.ThingId == Guard );
		var sums = (Sum( state, Happiness ), Sum( state, Security ));
		var guardsCell = state.EffectsAt( 39, 28 )[Security];
		var newCell = state.EffectsAt( 60, 60 )[Security];
		var entertainersNewCell = state.EffectsAt( 50, 30 )[Security];

		Assert.AreEqual( (6, 930), sums );
		Assert.AreEqual( 2, state.EffectsAt( 47, 25 )[Happiness] );

		// Found on the recorded cell: nothing moves.
		people.MoveEffect( entertainer );
		Assert.AreEqual( 2, state.EffectsAt( 47, 25 )[Happiness] );

		PutOn( entertainer, 50, 30 );
		PutOn( guard, 60, 60 );
		people.MoveEffect( entertainer );
		people.MoveEffect( guard );

		Assert.AreEqual( (0, 0, 2, 1, 0), (state.EffectsAt( 47, 25 )[Happiness], state.EffectsAt( 47, 24 )[Happiness],
			state.EffectsAt( 50, 30 )[Happiness], state.EffectsAt( 50, 31 )[Happiness], state.EffectsAt( 51, 31 )[Happiness]) );
		Assert.AreEqual( (guardsCell - 10, newCell + 10), (state.EffectsAt( 39, 28 )[Security], state.EffectsAt( 60, 60 )[Security]) );
		Assert.AreEqual( 0, state.EffectsAt( 60, 60 )[Happiness], "a guard's is effect 3, not the entertainer's" );
		Assert.AreEqual( entertainersNewCell, state.EffectsAt( 50, 30 )[Security], "an entertainer's is effect 0, not the guard's" );
		Assert.AreEqual( (MapStep.CellId( 50, 30 ), MapStep.CellId( 60, 60 )), (entertainer.RecordedCell, guard.RecordedCell) );
		Assert.AreEqual( sums, (Sum( state, Happiness ), Sum( state, Security )), "moved, not added" );

		// Asked again on the same cell: stamped once.
		people.MoveEffect( entertainer );
		Assert.AreEqual( sums, (Sum( state, Happiness ), Sum( state, Security )) );
	}

	[TestMethod]
	public void ARecordedIdThatIsNoCellHasNothingTakenOff()
	{
		var people = new ParkPeople( shipped );
		var state = people.State;
		var entertainer = people.Staff.Single( member => member.ThingId == Entertainer );

		entertainer.RecordedCell = 0;
		people.MoveEffect( entertainer );

		Assert.AreEqual( 4, state.EffectsAt( 47, 25 )[Happiness], "the file's, and theirs stamped again over it" );
		Assert.AreEqual( 12, Sum( state, Happiness ) );
		Assert.AreEqual( MapStep.CellId( 47, 25 ), entertainer.RecordedCell );
	}

	[TestMethod]
	public void AnotherKindsEffectIsNobodys()
	{
		var people = new ParkPeople( shipped );
		var state = people.State;
		var mechanic = people.Staff.Single( member => member.ThingId == Mechanic );
		var before = state.EffectsCopy();
		var recorded = mechanic.RecordedCell;

		PutOn( mechanic, 60, 60 );
		people.MoveEffect( mechanic );

		CollectionAssert.AreEqual( before, state.EffectsCopy() );
		Assert.AreEqual( recorded, mechanic.RecordedCell, "only an entertainer's and a guard's pre-step records the cell" );
	}

	[TestMethod]
	public void TheSweepMovesTheEffectBeforeTheirTurn()
	{
		using var clock = new SimulationClockScope();
		var people = new ParkPeople( shipped, random: new Random( 1 ), behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ),
			staffRandom: new Random( 4 ) );
		var state = people.State;
		var guard = people.Staff.Single( member => member.ThingId == Guard );

		PutOn( guard, 80, 80 );
		guard.Navigator.StampPrevious();

		var seen = false;

		for ( var sweep = 0; sweep < 40 && !seen; ++sweep )
		{
			SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
			people.Update();
			seen = guard.RecordedCell != MapStep.CellId( 39, 28 );
		}

		Assert.IsTrue( seen, "a sweep ran" );

		var (x, y) = MapStep.CellAt( guard.RecordedCell );

		Assert.IsTrue( Math.Abs( x - 80 ) <= 1 && Math.Abs( y - 80 ) <= 1, $"recorded where the sweep found them, ({x},{y})" );
		Assert.AreEqual( 10, state.EffectsAt( x, y )[Security] );
		Assert.AreEqual( 930, Sum( state, Security ) );
	}

	[TestMethod]
	public void AHireStampsTheirsWhereTheyArePutAndAFiringTakesItOff()
	{
		var people = new ParkPeople( shipped, staffRandom: new Random( 1 ) );
		var state = people.State;
		var entertainer = people.Hire( Candidate( 2 ), 30, 40 );
		var guard = people.Hire( Candidate( 3 ), 30, 50 );
		var mechanic = people.Hire( Candidate( 1 ), 30, 60 );

		Assert.AreEqual( 6, people.Staff.Single( member => member.ThingId == entertainer ).Model );
		Assert.AreEqual( 7, people.Staff.Single( member => member.ThingId == guard ).Model );
		Assert.AreEqual( (12, 1044), (Sum( state, Happiness ), Sum( state, Security )) );
		Assert.AreEqual( (2, 10, 1, 0), (state.EffectsAt( 30, 40 )[Happiness], state.EffectsAt( 30, 50 )[Security],
			state.EffectsAt( 33, 53 )[Security], state.EffectsAt( 34, 50 )[Security]) );
		Assert.IsTrue( state.EffectsAt( 30, 60 ).ToArray().All( word => word == 0 ), "a mechanic stamps none" );

		Assert.AreEqual( MapStep.CellId( 30, 40 ), people.Staff.Single( member => member.ThingId == entertainer ).RecordedCell );
		Assert.AreEqual( MapStep.CellId( 30, 50 ), people.Staff.Single( member => member.ThingId == guard ).RecordedCell );
		Assert.AreEqual( MapStep.CellId( 30, 60 ), people.Staff.Single( member => member.ThingId == mechanic ).RecordedCell,
			"every person records the cell they are made on" );

		// The file's entertainer, walked off the cell recorded: the firing takes the effect off the recorded one.
		var files = people.Staff.Single( member => member.ThingId == Entertainer );

		PutOn( files, 60, 60 );
		Assert.IsTrue( people.Fire( Entertainer ) );
		Assert.AreEqual( (0, 0, 6), (state.EffectsAt( 47, 25 )[Happiness], state.EffectsAt( 60, 60 )[Happiness], Sum( state, Happiness )) );

		Assert.IsTrue( people.Fire( Guard ) );
		Assert.AreEqual( 930, Sum( state, Security ) );

		Assert.IsTrue( people.Fire( entertainer ) && people.Fire( guard ) && people.Fire( mechanic ) );
		Assert.AreEqual( (0, 816), (Sum( state, Happiness ), Sum( state, Security )) );
		Assert.IsTrue( state.EffectsAt( 30, 40 ).ToArray().All( word => word == 0 ) );
	}

	[TestMethod]
	public void TheWrittenParksCellsAreWhatItsOwnThingsStamp()
	{
		CollectionAssert.AreEqual( Rebuilt( shipped ), shipped.CellEffects.ToArray(), "the shipped park's, the rule's own check" );

		var people = new ParkPeople( shipped, staffRandom: new Random( 1 ) );
		var state = people.State;
		var entertainer = people.Hire( Candidate( 2 ), 30, 40 );
		var guard = people.Hire( Candidate( 3 ), 30, 50 );
		var researcher = people.Hire( Candidate( 4 ), 30, 60 );
		var moved = people.Staff.Single( member => member.ThingId == entertainer );

		PutOn( moved, 33, 41 );
		people.MoveEffect( moved );
		Assert.IsTrue( people.Fire( Entertainer ) && people.Fire( Guard ) );

		var written = Level.WrittenThings( shipped );

		var running = new ParkFileWriter.Running( shipped.GameTick, shipped.ParkClosed != 0, shipped.NumberOfVisitorsToDate,
			shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value, People: people.Written( written.Contains ),
			Effects: Level.WrittenEffects( shipped, state, written.Contains, asTheFile: true ) );

		var park = new ParkWorld( ParkFileWriter.Body( shipped, running ) );

		Assert.IsNull( park.Problem );

		var recorded = park.People.ToDictionary( person => person.ThingId, person => person.LastRecordedMapId );

		Assert.AreEqual( (MapStep.CellId( 33, 41 ), MapStep.CellId( 30, 50 ), MapStep.CellId( 30, 60 )),
			(recorded[entertainer], recorded[guard], recorded[researcher]) );
		Assert.IsFalse( recorded.ContainsKey( Entertainer ) || recorded.ContainsKey( Guard ) );
		Assert.AreEqual( MapStep.CellId( 48, 28 ), recorded[Mechanic], "a kept member's is the running park's, which is the file's" );

		foreach ( var guest in shipped.People.Where( person => person.Guest != null ) )
			Assert.AreEqual( guest.LastRecordedMapId, recorded[guest.ThingId], $"guest {guest.ThingId}'s is left the file's" );

		CollectionAssert.AreEqual( Rebuilt( park ), park.CellEffects.ToArray() );
		Assert.AreEqual( 2, park.CellEffects[(((41 * ParkWorld.MapSize) + 33) * ParkWorld.EffectWords) + Happiness] );
	}
}
