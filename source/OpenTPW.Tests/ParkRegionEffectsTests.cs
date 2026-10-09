using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The region effects a thing holds on the cells round it, in the running park and in a park file written from it
/// (<c>docs/exe/ride-operation.md</c>, "The region effects"; <c>docs/exe/saves.md</c>, "OpenTPW's writer, the region
/// effects").
/// </summary>
[TestClass]
public class ParkRegionEffectsTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int CameraItem = 1413;

	/// <summary>The Small Toilet at (55,17), and a Security Camera of the file's.</summary>
	private const int Toilet = 21;

	private const int Words = ParkWorld.EffectWords;

	private BaseFileSystem data = null!;
	private ParkWorld shipped = null!;
	private string? root;
	private BaseFileSystem oldSaves = null!;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = GameData.Required();
		FileSystem = data;
		shipped = Read( data.ReadAllBytes( ShippedPark ) );

		oldSaves = SaveFileSystem;
		root = Directory.CreateTempSubdirectory( "opentpw-region-effects-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void RestoreSaves()
	{
		if ( root == null )
			return;

		SetCurrentPlayer( null );
		SaveFileSystem = oldSaves;
		Directory.Delete( root, true );
		ParkOrbitCameraMode.Forget();
		Unimplemented.Forget();
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	private static short[] Grid() => new short[ParkWorld.MapSize * ParkWorld.MapSize * Words];

	private static short[] At( IReadOnlyList<short> grid, int x, int y )
		=> [.. Enumerable.Range( 0, Words ).Select( word => grid[(((y * ParkWorld.MapSize) + x) * Words) + word] )];

	private static int Holding( IReadOnlyList<short> grid )
		=> Enumerable.Range( 0, grid.Count / Words ).Count( cell => Enumerable.Range( 0, Words ).Any( word => grid[(cell * Words) + word] != 0 ) );

	private ParkFileWriter.Running AsShipped( IReadOnlyList<short>? effects ) => new( shipped.GameTick, shipped.ParkClosed != 0,
		shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value, Effects: effects );

	/// <summary>The park's balance holds the eight the executable's defaults are, key for key.</summary>
	[TestMethod]
	public void TheBalancesEightAreStandardSams()
	{
		var read = ParkRegionEffects.From( new ParkBalance( "jungle", easyMode: true ) );

		for ( var effect = 0; effect < 8; ++effect )
			Assert.AreEqual( ParkRegionEffects.Standard[effect], read[effect], $"RegionFX[{effect}]" );

		Assert.AreEqual( new ParkRegionEffects.Effect( 2, 0, 0, 0, 10, 6 ), read[ParkRegionEffects.Fireworks] );
		Assert.AreSame( ParkRegionEffects.Standard, ParkRegionEffects.From( null ) );
	}

	/// <summary>
	/// Every cell of the shipped park holds what its things stamp: each object's by its flags, the entertainer's 0
	/// and the guard's 3 where they stand. 250 cells hold an effect, and each of those has an effects record.
	/// </summary>
	[TestMethod]
	public void TheShippedParksCellsHoldWhatItsThingsStamp()
	{
		var grid = Grid();

		foreach ( var thing in shipped.Objects )
		{
			foreach ( var effect in ParkRegionEffects.Of( thing ) )
				ParkRegionEffects.Standard.Stamp( grid, effect, thing.CellX, thing.CellY );
		}

		foreach ( var person in shipped.People.Where( person => person.Model is 6 or 7 ) )
			ParkRegionEffects.Standard.Stamp( grid, person.Model == 6 ? ParkRegionEffects.Entertainer : ParkRegionEffects.Guard, person.CellX, person.CellY );

		CollectionAssert.AreEqual( grid, shipped.CellEffects.ToArray() );
		Assert.AreEqual( 250, Holding( grid ) );
		Assert.AreEqual( 250, shipped.Cells.Count( cell => (cell.Status & 0x4) != 0 ) );
		CollectionAssert.AreEqual( new[] { 3, 2 }, shipped.Objects.SelectMany( ParkRegionEffects.Of ).GroupBy( effect => effect )
			.OrderBy( group => group.Key ).Select( group => group.Count() ).ToArray(), "three clean toilets, two cameras: effects 1 and 4" );
		CollectionAssert.AreEqual( shipped.CellEffects.ToArray(), new ParkState( shipped ).Effects.ToArray(), "the running park starts on the file's" );
	}

	private ParkWorld.CatalogueObject Bought( ParkState state, ParkItemCatalogue catalogue, ParkRides? rides, int itemId, int x, int y )
	{
		Assert.IsTrue( catalogue.TryGet( itemId, out var item ) );

		var id = state.NextThingId();
		var placed = ParkBuilding.Constructed( item, id, x, y, 0, MapStep.CellId( x + item.EntryDeltaX, y + item.EntryDeltaY ),
			MapStep.CellId( x + item.ExitDeltaX, y + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ) );

		state.AddObject( placed );
		ParkBuilding.StampEffects( state, placed );
		ParkBuilding.BindOperation( state, rides, placed, item );
		ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, x, y, 0 ), x, y );
		state.EnterCell( x, y, id );

		return placed;
	}

	/// <summary>
	/// A camera bought stamps security's effect round its cell and a toilet the clean toilet's; selling each takes
	/// its own off, and the park's cells are the file's again.
	/// </summary>
	[TestMethod]
	public void APurchaseStampsAndASaleTakesOff()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		var file = shipped.CellEffects.ToArray();

		var camera = Bought( state, catalogue, rides, CameraItem, 30, 60 );

		CollectionAssert.AreEqual( new short[] { 0, 0, 0, 20, 0 }, state.EffectsAt( 30, 60 ).ToArray() );
		CollectionAssert.AreEqual( new short[] { 0, 0, 0, 2, 0 }, state.EffectsAt( 35, 64 ).ToArray() );
		Assert.AreEqual( 250 + 121, Holding( state.Effects ) );

		var toilet = Bought( state, catalogue, rides, shipped.Objects.Single( thing => thing.ThingId == Toilet ).CatalogueId, 31, 60 );

		CollectionAssert.AreEqual( new short[] { 0, 1, -1, 10, 0 }, state.EffectsAt( 31, 60 ).ToArray() );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What is "BOUGHT_OBJECT_REGION_EFFECT" or "FIREWORKS_SPENT_REGION_EFFECT" ) );

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, camera.ThingId, people ), "sold" );

		CollectionAssert.AreEqual( new short[] { 0, 1, -1, 0, 0 }, state.EffectsAt( 31, 60 ).ToArray() );

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, toilet.ThingId, people ), "sold" );

		CollectionAssert.AreEqual( file, state.Effects.ToArray() );
	}

	/// <summary>Selling a toilet of the file's takes the clean one's effect off, or the dirty one's where it has been dirtied.</summary>
	[TestMethod]
	[DataRow( false )]
	[DataRow( true )]
	public void ASoldToiletTakesOffTheEffectItHolds( bool dirtied )
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		var expected = shipped.CellEffects.ToArray();

		Assert.IsTrue( state.TryObject( Toilet, out var toilet ) );

		if ( dirtied )
		{
			// Sixteen uses at a need of 100 take five each off the hundred: the sixteenth is the one that dirties.
			var operation = new ParkRideOperation( state, new Dictionary<int, Peep>() );

			for ( var use = 1; use <= 16; ++use )
				Assert.AreEqual( use == 16, operation.WearByUse( Toilet, 100 ), $"use {use}" );

			CollectionAssert.AreEqual( new short[] { 0, 1, -1, 0, 0 }, At( state.Effects, toilet.CellX, toilet.CellY ).Zip(
				At( expected, toilet.CellX, toilet.CellY ), ( now, was ) => (short)(now - was) ).ToArray(), "the dirty one's 2 for the clean one's 1" );
			Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "TOILET_DIRTY_REGION_EFFECTS" ) );
		}

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, Toilet, people ), "sold" );

		ParkRegionEffects.Standard.Stamp( expected, ParkRegionEffects.CleanToilet, toilet.CellX, toilet.CellY, off: true );

		CollectionAssert.AreEqual( expected, state.Effects.ToArray() );
	}

	/// <summary>Fireworks stamp theirs and count the turn that would take it off again.</summary>
	[TestMethod]
	public void FireworksStampTheirsAndCountTheSpentTurn()
	{
		var state = new ParkState( shipped );

		ParkBuilding.StampEffects( state, new ParkWorld.CatalogueObject( ThingId: 99, CatalogueId: 9, RawX: 20 << 8, RawY: 90 << 8,
			Angle: 0, Flags: ParkWorld.CatalogueObject.IsFireworksFlag ) );

		CollectionAssert.AreEqual( new short[] { 2, 0, 0, 0, 10 }, state.EffectsAt( 20, 90 ).ToArray() );
		CollectionAssert.AreEqual( new short[] { 0, 0, 0, 0, 1 }, state.EffectsAt( 26, 87 ).ToArray() );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "FIREWORKS_SPENT_REGION_EFFECT" ).Times );
	}

	/// <summary>The file's own effects written back leave the body as it was read, byte for byte.</summary>
	[TestMethod]
	public void TheFilesOwnEffectsWriteTheBodyRead()
		=> CollectionAssert.AreEqual( ParkFileWriter.Body( shipped, AsShipped( null ) ), ParkFileWriter.Body( shipped, AsShipped( shipped.CellEffects ) ) );

	/// <summary>
	/// A cell that gains an effect gains its ten bytes and the status bit, one that loses all of its loses both, and
	/// every other byte of the body is where it was, moved along.
	/// </summary>
	[TestMethod]
	public void ACellsEffectsRecordComesAndGoesWithItsWords()
	{
		var effects = shipped.CellEffects.ToArray();
		var camera = shipped.Objects.First( thing => (thing.Flags & ParkWorld.CatalogueObject.ProvidesSecurityFlag) != 0 );

		// One cell of bare ground gains a word; the cell under a camera, which holds security alone, loses it.
		effects[(((90 * ParkWorld.MapSize) + 20) * Words) + 2] = -7;

		var under = ((camera.CellY * ParkWorld.MapSize) + camera.CellX) * Words;
		var alone = Enumerable.Range( 0, Words ).All( word => word == 3 || effects[under + word] == 0 );

		Assert.IsTrue( alone && effects[under + 3] != 0, "the cell under the camera holds security alone" );
		effects[under + 3] = 0;

		var plain = ParkFileWriter.Body( shipped, AsShipped( null ) );
		var body = ParkFileWriter.Body( shipped, AsShipped( effects ) );
		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer );
		Assert.AreEqual( plain.Length, body.Length, "ten bytes on and ten off" );
		CollectionAssert.AreEqual( effects, written.CellEffects.ToArray() );
		Assert.AreEqual( 7, written.CellAt( 20, 90 ).Status );
		Assert.AreEqual( 3, written.CellAt( camera.CellX, camera.CellY ).Status );
		Assert.AreEqual( 250, written.Cells.Count( cell => (cell.Status & 0x4) != 0 ) );
		Assert.AreEqual( shipped.Objects.Count, written.Objects.Count );
		Assert.AreEqual( shipped.People.Count, written.People.Count );

		for ( var i = 0; i < shipped.Cells.Count; ++i )
			Assert.AreEqual( shipped.Cells[i] with { Status = 0, NearbyEffects = 0 }, written.Cells[i] with { Status = 0, NearbyEffects = 0 }, $"cell {i}" );

		// Grown by one record: the body is ten bytes longer and still reads whole.
		effects[under + 3] = 5;

		var longer = ParkFileWriter.Body( shipped, AsShipped( effects ) );

		Assert.AreEqual( plain.Length + 10, longer.Length );
		Assert.IsNull( new ParkWorld( longer ).Problem );
		CollectionAssert.AreEqual( effects, new ParkWorld( longer ).CellEffects.ToArray() );
	}

	/// <summary>Words that are not a map's are refused.</summary>
	[TestMethod]
	public void EffectsThatAreNotAMapsAreRefused()
		=> Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, AsShipped( new short[10] ) ) );

	/// <summary>
	/// The level writes the running park's effects: a camera bought and a toilet sold leave the file's cells holding
	/// the one's and not the other's, and OpenTPW's own load reads them back.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheEffectsOfAThingBoughtAndAThingSold()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var catalogue = new ParkItemCatalogue( "jungle", data );
		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		var expected = shipped.CellEffects.ToArray();

		Assert.IsTrue( state.TryObject( Toilet, out var toilet ) );

		var camera = Bought( state, catalogue, rides, CameraItem, 46, 27 );

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, Toilet, people ), "sold" );

		// And the toilet beside it dirtied: its record is written dirty, and the cells hold the dirty one's.
		var operation = new ParkRideOperation( state, new Dictionary<int, Peep>() );

		for ( var use = 0; use < 16; ++use )
			operation.WearByUse( Toilet + 1, 100 );

		Assert.IsTrue( state.TryObject( Toilet + 1, out var next ) && ParkState.IsDirty( next ) );

		ParkRegionEffects.Standard.Stamp( expected, ParkRegionEffects.Security, 46, 27 );
		ParkRegionEffects.Standard.Stamp( expected, ParkRegionEffects.CleanToilet, toilet.CellX, toilet.CellY, off: true );
		ParkRegionEffects.Standard.Stamp( expected, ParkRegionEffects.CleanToilet, next.CellX, next.CellY, off: true );
		ParkRegionEffects.Standard.Stamp( expected, ParkRegionEffects.DirtyToilet, next.CellX, next.CellY );

		Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Effects", people, null, rides, catalogue ) );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Effects.TPWS" ) ) );

		Assert.IsNull( written.Problem );
		CollectionAssert.AreEqual( expected, written.CellEffects.ToArray() );
		CollectionAssert.AreEqual( new short[] { 0, 0, 0, 20, 0 }, At( written.CellEffects, 46, 27 ) );
		Assert.AreEqual( 7, written.CellAt( 46, 27 ).Status );
		Assert.IsTrue( written.Objects.Any( thing => thing.ThingId == camera.ThingId ) );
		Assert.IsFalse( written.Objects.Any( thing => thing.ThingId == Toilet ) );
		Assert.IsTrue( ParkState.IsDirty( written.Objects.Single( thing => thing.ThingId == Toilet + 1 ) ), "the dirtied toilet's record" );

		// Each written object's effect is on the cells once: taking them all off leaves what the file's staff hold.
		var left = written.CellEffects.ToArray();
		var staff = shipped.CellEffects.ToArray();

		foreach ( var thing in written.Objects )
		{
			foreach ( var effect in ParkRegionEffects.Of( thing ) )
				ParkRegionEffects.Standard.Stamp( left, effect, thing.CellX, thing.CellY, off: true );
		}

		foreach ( var thing in shipped.Objects )
		{
			foreach ( var effect in ParkRegionEffects.Of( thing ) )
				ParkRegionEffects.Standard.Stamp( staff, effect, thing.CellX, thing.CellY, off: true );
		}

		CollectionAssert.AreEqual( staff, left );
		CollectionAssert.AreEqual( expected, new ParkState( written ).Effects.ToArray() );
	}

	/// <summary>
	/// The written cells hold each object's effect as its written record has it: none for a thing bought and left
	/// out, the file's for a thing sold and left in, and the file's for a toilet dirtied where the objects go out
	/// as the file's.
	/// </summary>
	[TestMethod]
	public void TheWrittenEffectsAreTheWrittenObjects()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( "jungle", shipped, catalogue, data );
		var file = shipped.CellEffects.ToArray();
		var kept = Level.WrittenThings( shipped );

		var camera = Bought( state, catalogue, rides, CameraItem, 46, 27 );

		CollectionAssert.AreEqual( file, Level.WrittenEffects( shipped, state, kept.Contains ), "the camera bought and not written" );
		CollectionAssert.AreNotEqual( file, Level.WrittenEffects( shipped, state, id => id == camera.ThingId || kept.Contains( id ) ) );

		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, camera.ThingId, people ), "sold" );
		StringAssert.Contains( ParkBuilding.Sell( state, shipped, catalogue, null, rides, Toilet, people ), "sold" );

		CollectionAssert.AreNotEqual( file, state.Effects.ToArray() );
		CollectionAssert.AreEqual( file, Level.WrittenEffects( shipped, state, kept.Contains ), "the toilet sold and still written" );
		CollectionAssert.AreEqual( state.Effects.ToArray(), Level.WrittenEffects( shipped, state, id => id != Toilet && kept.Contains( id ) ), "and taken out" );

		// A toilet dirtied: written dirty with its record, and clean where the record goes out as the file's.
		var operation = new ParkRideOperation( state, new Dictionary<int, Peep>() );

		for ( var use = 0; use < 16; ++use )
			operation.WearByUse( Toilet + 1, 100 );

		Assert.IsTrue( state.TryObject( Toilet + 1, out var next ) && ParkState.IsDirty( next ) );
		CollectionAssert.AreEqual( file, Level.WrittenEffects( shipped, state, kept.Contains, asTheFile: true ) );
		CollectionAssert.AreNotEqual( file, Level.WrittenEffects( shipped, state, kept.Contains ) );
	}
}

/// <summary>The region effects' rule and an object's own, which need no game data.</summary>
[TestClass]
public class ParkRegionEffectRuleTests
{
	private const int Words = ParkWorld.EffectWords;

	private static short[] Grid() => new short[ParkWorld.MapSize * ParkWorld.MapSize * Words];

	private static short[] At( IReadOnlyList<short> grid, int x, int y )
		=> [.. Enumerable.Range( 0, Words ).Select( word => grid[(((y * ParkWorld.MapSize) + x) * Words) + word] )];

	private static int Holding( IReadOnlyList<short> grid )
		=> Enumerable.Range( 0, grid.Count / Words ).Count( cell => Enumerable.Range( 0, Words ).Any( word => grid[(cell * Words) + word] != 0 ) );

	/// <summary>A word is divided by the cell's distance along the grid plus one and cut towards nought, over the radius's square.</summary>
	[TestMethod]
	[DataRow( 4, 0, 0, 3, 20 )]
	[DataRow( 4, 1, 0, 3, 10 )]
	[DataRow( 4, 1, 1, 3, 6 )]
	[DataRow( 4, 5, 5, 3, 1 )]
	[DataRow( 4, -5, 2, 3, 2 )]
	[DataRow( 4, 6, 0, 3, 0 )]
	[DataRow( 4, 0, -6, 3, 0 )]
	[DataRow( 5, 1, 0, 0, -1 )]
	[DataRow( 5, 1, 0, 1, 1 )]
	[DataRow( 5, 1, 0, 2, 0 )]
	[DataRow( 5, 3, 3, 0, 0 )]
	[DataRow( 7, 0, 0, 4, 10 )]
	[DataRow( 7, 6, 6, 4, 0 )]
	[DataRow( 7, 6, 3, 4, 1 )]
	[DataRow( 2, 1, 1, 1, 0 )]
	[DataRow( 2, 2, 0, 1, 0 )]
	public void AStampDividesEachWordByTheDistance( int effect, int dx, int dy, int word, int value )
	{
		var grid = Grid();

		ParkRegionEffects.Standard.Stamp( grid, effect, 50, 60 );

		Assert.AreEqual( value, At( grid, 50 + dx, 60 + dy )[word] );
	}

	/// <summary>A stamp at the map's corner is held to the map, and an unstamp takes off exactly what a stamp put on.</summary>
	[TestMethod]
	public void AStampIsHeldToTheMapAndAnUnstampUndoesIt()
	{
		var grid = Grid();

		ParkRegionEffects.Standard.Stamp( grid, ParkRegionEffects.Security, 0, 127 );

		Assert.AreEqual( 36, Holding( grid ), "six by six of the eleven" );
		Assert.AreEqual( 20, At( grid, 0, 127 )[3] );

		ParkRegionEffects.Standard.Stamp( grid, 5, 3, 124 );
		ParkRegionEffects.Standard.Stamp( grid, ParkRegionEffects.Security, 0, 127, off: true );
		ParkRegionEffects.Standard.Stamp( grid, 5, 3, 124, off: true );

		Assert.AreEqual( 0, Holding( grid ) );
	}

	/// <summary>What an object holds is its flags': a toilet's clean or dirty one by its State of repair, security's, fireworks'.</summary>
	[TestMethod]
	[DataRow( 0x1, 100f, new[] { 1 } )]
	[DataRow( 0x1, 25f, new[] { 1 } )]
	[DataRow( 0x1, 24.9f, new[] { 6 } )]
	[DataRow( 0x10, 0f, new[] { 4 } )]
	[DataRow( 0x80, 0f, new[] { 7 } )]
	[DataRow( 0x91, 3f, new[] { 6, 4, 7 } )]
	[DataRow( 0x16e, 0f, new int[0] )]
	public void AnObjectsEffectsAreItsFlags( int flags, float repair, int[] effects )
		=> CollectionAssert.AreEqual( effects, ParkRegionEffects.Of( new ParkWorld.CatalogueObject( ThingId: 9, CatalogueId: 9,
			RawX: 0, RawY: 0, Angle: 0, Flags: (ushort)flags, StateOfRepair: repair ) ).ToArray() );
}
