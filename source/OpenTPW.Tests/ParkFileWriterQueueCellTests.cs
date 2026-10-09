using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, a queue cell's model (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a queue cell's
/// model"): a cell laid names a model made for it, a cell cleared gives its own up, a cell tiled again does both.
/// </summary>
[TestClass]
public class ParkFileWriterQueueCellTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";
	private const uint ShippedClock = 114374804;

	private BaseFileSystem data = null!;
	private BaseFileSystem oldSaves = null!;
	private string? root;
	private byte[] payload = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;
	private readonly List<Entity> made = [];

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = FileSystem = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		var reader = new SaveReader( stream );

		payload = reader.ReadFile();
		shipped = new ParkWorld( payload, reader.Preamble );
		catalogue = new ParkItemCatalogue( Theme, data );
		GameClock.Rebase();

		oldSaves = SaveFileSystem;
		root = Directory.CreateTempSubdirectory( "opentpw-queue-cells-" ).FullName;
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
			SaveFileSystem = oldSaves;
			Directory.Delete( root, true );
		}

		ParkOrbitCameraMode.Forget();
		Unimplemented.Forget();
	}

	private static int Index( int x, int y ) => (y * ParkWorld.MapSize) + x;

	private int ChannelsFor( int id ) => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1;

	private ParkThingStates Models( ParkWorld world ) => world.ThingStates( ChannelsFor );

	private static WrittenScript AsWritten( SavedScript saved ) => new( saved.Handle, saved.Position, saved.CallIndex,
		saved.HeapIndex, saved.Result, saved.Stack, saved.Variables, saved.WaitDeadline, saved.AnimationDeadline,
		saved.LoopingKey, saved.AnimationMark, saved.TimerDeadline, saved.Limbo, saved.InLimbo, saved.Bounce,
		saved.Bouncing, saved.BounceBase, saved.BounceNode, saved.Walk, saved.Heads );

	/// <summary>The things of <paramref name="world"/> as its file holds them, with these queue cells changed.</summary>
	private ParkFileWriter.RunningThings Things( ParkWorld world, Dictionary<int, ParkFileWriter.QueuePiece?> cells ) => new(
		[.. world.Objects], world.ScriptStates.Tick, world.ScriptStates.NextHandle,
		[.. world.ScriptStates.Order.Select( handle => AsWritten( world.ScriptStates.For( handle )!.Value ) )],
		Models( world ),
		[.. Models( world ).Things.Select( thing => new WrittenModel( thing.Slot, thing.Channels, thing.HoardingFlags, thing.HoardingProgress ) )],
		ShippedClock, QueueCells: cells );

	private ParkWorld Written( ParkWorld world, Dictionary<int, ParkFileWriter.QueuePiece?> cells, out byte[] body,
		out ParkFileWriter.ThingsWritten done, bool people = true )
	{
		var running = new ParkFileWriter.Running( world.GameTick, world.ParkClosed != 0, world.NumberOfVisitorsToDate,
			world.Economy!.Value.Balance, world.Camera.Saved!.Value,
			People: people ? new ParkPeople( world ).Written( id => true ) : null, Things: Things( world, cells ) );

		body = ParkFileWriter.Body( world, running, out _, out var things );

		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer && written.ClosedOnSpriteTrailer, "the list and the sprite table close" );
		Assert.IsNull( written.ScriptStates.Problem, "the scripts read" );
		Assert.IsNull( Models( written ).Problem, "the models read" );

		done = things!.Value;

		return written;
	}

	/// <summary>Every queue piece the shipped park holds is the record the writer makes for its cell, byte for byte.</summary>
	[TestMethod]
	public void AMadePiecesRecordIsTheFilesOwn()
	{
		var queue = Enumerable.Range( 0, shipped.Cells.Count ).Where( index => shipped.Cells[index].Type == ParkRideChoice.QueueCellType ).ToList();

		CollectionAssert.AreEqual( new[] { Index( 49, 22 ), Index( 50, 22 ), Index( 51, 22 ), Index( 52, 22 ) }, queue );
		CollectionAssert.AreEqual( new[] { (5, 114), (2, 113), (2, 112), (3, 111) },
			queue.Select( index => (shipped.Cells[index].TileIndex, shipped.Cells[index].MeshInstance) ).ToArray() );
		Assert.AreEqual( 4, shipped.Cells.Count( cell => cell.MeshInstance != 0 ), "no other cell names a model" );

		var models = Models( shipped );

		foreach ( var index in queue )
		{
			var cell = shipped.Cells[index];
			var record = ParkThingStates.QueuePieceRecord( index % 128, index / 128, cell.TileIndex, cell.TileAngle );
			var at = payload.AsSpan().IndexOf( record );

			Assert.IsTrue( at > 0, $"cell {index}'s record, whole" );
			Assert.AreEqual( 17000 + cell.TileIndex, models.ItemIn( cell.MeshInstance - 1 ) );
			Assert.AreEqual( 0x37 + (cell.TileIndex >= 5 ? 16 : 8) + 44, record.Length, "two node words, or four" );
		}

		Assert.IsNull( models.ItemIn( 90 ), "an empty slot" );
		Assert.IsNull( models.ItemIn( 168 ), "past the table" );
		Assert.IsTrue( ParkThingStates.IsQueuePiece( 17000 ) && ParkThingStates.IsQueuePiece( 17007 ) );
		Assert.IsFalse( ParkThingStates.IsQueuePiece( 16999 ) || ParkThingStates.IsQueuePiece( 17008 ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => ParkThingStates.QueuePieceRecord( 1, 1, 8, 0 ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => ParkThingStates.QueuePieceRecord( 1, 1, -1, 0 ) );
	}

	/// <summary>A piece's record: its item by the tile's index, its own cell, one square, the retile's flags, no script, 360 less the tile's angle, an idle channel.</summary>
	[TestMethod]
	public void APiecesRecordTurnsTheOtherWay()
	{
		int Int( byte[] bytes, int at ) => BitConverter.ToInt32( bytes, at );

		var record = ParkThingStates.QueuePieceRecord( 60, 61, 6, 90 );

		Assert.AreEqual( (17006, 60, 61, 1, 1), (Int( record, 1 ), Int( record, 5 ), Int( record, 9 ), Int( record, 0xd ), Int( record, 0x11 )) );
		Assert.AreEqual( (0x33a, 0, 0, 270), (Int( record, 0x15 ), Int( record, 0x19 ), Int( record, 0x1d ), Int( record, 0x27 )) );
		Assert.AreEqual( (4, 0), (BitConverter.ToInt16( record, 0x2b ), BitConverter.ToInt16( record, 0x2d )) );
		CollectionAssert.AreEqual( new[] { 0, 12, 0, 0, 0, 0, 0, 12, 0, 0, 0 }, Enumerable.Range( 0, 11 ).Select( i => Int( record, 0x37 + 16 + (i * 4) ) ).ToArray() );
		Assert.AreEqual( 0, Int( ParkThingStates.QueuePieceRecord( 1, 1, 2, 0 ), 0x27 ), "a whole turn is none" );
		Assert.AreEqual( 90, Int( ParkThingStates.QueuePieceRecord( 1, 1, 2, 270 ), 0x27 ) );
		Assert.AreEqual( (0x37 + 8 + 44, 2), (ParkThingStates.QueuePieceRecord( 1, 1, 1, 0 ).Length, BitConverter.ToInt16( ParkThingStates.QueuePieceRecord( 1, 1, 0, 0 ), 0x2b )), "the dead end's two" );
	}

	/// <summary>A cell laid takes the slot at the cursor and names it; nothing else of the table moves.</summary>
	[TestMethod]
	public void ACellLaidNamesAModelMadeForIt()
	{
		var written = Written( shipped, new() { [Index( 60, 61 )] = new( 2, 90 ) }, out var body, out var done );
		var models = Models( written );

		Assert.AreEqual( 91, written.CellAt( 60, 61 ).MeshInstance );
		Assert.AreEqual( (162, 6, 91), models.Header );
		Assert.AreEqual( 17002, models.ItemIn( 90 ) );
		Assert.IsTrue( body.AsSpan().IndexOf( ParkThingStates.QueuePieceRecord( 60, 61, 2, 90 ) ) > 0, "the record, whole" );
		Assert.AreEqual( 5, written.Cells.Count( cell => cell.MeshInstance != 0 ) );
		CollectionAssert.AreEqual( new[] { (Index( 60, 61 ), 91, 0) }, done.QueueCells!.ToArray() );
		Assert.AreEqual( 91, done.ModelSlots );
		Assert.AreEqual( shipped.Objects.Count, written.Objects.Count );
		Assert.AreEqual( shipped.ScriptStates.Order.Count, written.ScriptStates.Order.Count );
	}

	/// <summary>A cell cleared gives up its model: its slot is emptied and it names none.</summary>
	[TestMethod]
	public void ACellClearedGivesUpItsModel()
	{
		var written = Written( shipped, new() { [Index( 50, 22 )] = null }, out _, out var done );
		var models = Models( written );

		Assert.AreEqual( 0, written.CellAt( 50, 22 ).MeshInstance );
		Assert.AreEqual( (160, 8, 90), models.Header );
		Assert.IsNull( models.ItemIn( 112 ) );
		Assert.AreEqual( (17003, 17002, 17005), (models.ItemIn( 110 ), models.ItemIn( 111 ), models.ItemIn( 113 )), "the other three are where they were" );
		Assert.AreEqual( (114, 112, 111), (written.CellAt( 49, 22 ).MeshInstance, written.CellAt( 51, 22 ).MeshInstance, written.CellAt( 52, 22 ).MeshInstance) );
		CollectionAssert.AreEqual( new[] { (Index( 50, 22 ), 0, 113) }, done.QueueCells!.ToArray() );
	}

	/// <summary>A cell tiled again gives up its own and takes the lowest empty slot, as the original's retile does.</summary>
	[TestMethod]
	public void ACellTiledAgainDoesBoth()
	{
		var written = Written( shipped, new() { [Index( 51, 22 )] = new( 5, 180 ) }, out var body, out var done );
		var models = Models( written );

		Assert.AreEqual( 91, written.CellAt( 51, 22 ).MeshInstance );
		Assert.AreEqual( (17005, null), (models.ItemIn( 90 ), models.ItemIn( 111 )) );
		Assert.AreEqual( (161, 7, 91), models.Header );
		Assert.IsTrue( body.AsSpan().IndexOf( ParkThingStates.QueuePieceRecord( 51, 22, 5, 180 ) ) > 0 );
		Assert.IsTrue( body.AsSpan().IndexOf( ParkThingStates.QueuePieceRecord( 51, 22, 2, 270 ) ) < 0, "the old record is gone" );
		CollectionAssert.AreEqual( new[] { (Index( 51, 22 ), 91, 112) }, done.QueueCells!.ToArray() );
	}

	/// <summary>Several cells take the lowest empty slots in the map's order, and a slot given up is one of the empty.</summary>
	[TestMethod]
	public void CellsTakeTheirSlotsInTheMapsOrder()
	{
		var written = Written( shipped, new()
		{
			[Index( 70, 70 )] = new( 3, 0 ),
			[Index( 60, 61 )] = new( 2, 90 ),
			[Index( 52, 22 )] = null,
		}, out _, out _ );

		var models = Models( written );

		Assert.AreEqual( (0, 91, 92), (written.CellAt( 52, 22 ).MeshInstance, written.CellAt( 60, 61 ).MeshInstance, written.CellAt( 70, 70 ).MeshInstance) );
		Assert.AreEqual( (17002, 17003, null), (models.ItemIn( 90 ), models.ItemIn( 91 ), models.ItemIn( 110 )) );
		Assert.AreEqual( (162, 6, 92), models.Header );
	}

	/// <summary>The pieces are written with no people given, and nothing else of the file is asked for.</summary>
	[TestMethod]
	public void APieceIsWrittenWithoutThePeople()
	{
		var written = Written( shipped, new() { [Index( 60, 61 )] = new( 2, 90 ), [Index( 49, 22 )] = null }, out _, out _, people: false );

		Assert.AreEqual( (91, 0), (written.CellAt( 60, 61 ).MeshInstance, written.CellAt( 49, 22 ).MeshInstance) );
		Assert.AreEqual( shipped.People.Count, written.People.Count );
	}

	/// <summary>A cell whose handle names a record that is no queue piece's does not free it: the slot is not the cell's.</summary>
	[TestMethod]
	public void AHandleThatNamesNoPieceIsLeftAlone()
	{
		var doctored = (byte[])payload.Clone();

		shipped.PutCellModels( doctored, new Dictionary<int, int> { [Index( 50, 22 )] = 110, [Index( 51, 22 )] = 500 } );

		var world = new ParkWorld( doctored );

		Assert.AreEqual( 1100, Models( world ).ItemIn( 109 ), "the Belly Bounce's own model" );

		var written = Written( world, new() { [Index( 50, 22 )] = null, [Index( 51, 22 )] = null }, out _, out var done );

		Assert.AreEqual( 1100, Models( written ).ItemIn( 109 ) );
		Assert.AreEqual( (161, 7, 90), Models( written ).Header );
		Assert.AreEqual( (0, 0), (written.CellAt( 50, 22 ).MeshInstance, written.CellAt( 51, 22 ).MeshInstance) );
		CollectionAssert.AreEqual( new[] { (Index( 50, 22 ), 0, 0), (Index( 51, 22 ), 0, 0) }, done.QueueCells!.ToArray() );
	}

	/// <summary>A cell's handle is written where its record lies, and no other byte of the body moves.</summary>
	[TestMethod]
	public void ACellsHandleIsWrittenWhereItLies()
	{
		var body = (byte[])payload.Clone();

		shipped.PutCellModels( body, new Dictionary<int, int>() );
		CollectionAssert.AreEqual( payload, body );

		shipped.PutCellModels( body, new Dictionary<int, int> { [Index( 0, 0 )] = 7, [Index( 127, 127 )] = 0x01020304, [Index( 49, 22 )] = 0 } );

		var read = new ParkWorld( body );

		Assert.AreEqual( (7, 0x01020304, 0, 113), (read.CellAt( 0, 0 ).MeshInstance, read.CellAt( 127, 127 ).MeshInstance, read.CellAt( 49, 22 ).MeshInstance, read.CellAt( 50, 22 ).MeshInstance) );
		Assert.AreEqual( 12, Enumerable.Range( 0, body.Length ).Count( at => body[at] != payload[at] ) <= 12 ? 12 : -1 );
		Assert.AreEqual( shipped.CellAt( 49, 22 ) with { MeshInstance = 0 }, read.CellAt( 49, 22 ) );
	}

	/// <summary>
	/// The level hands the pieces over: a cell laid, one cleared and one tiled again go to the writer, a cell on a
	/// tile outside the eight pieces is counted and left, and a queue cell that only changed its links is neither.
	/// </summary>
	[TestMethod]
	public void TheLevelHandsOverEachChangedQueueCell()
	{
		var state = new ParkState( shipped );
		var grass = shipped.CellAt( 60, 61 );

		Assert.AreEqual( 0, grass.Type );

		state.SetRecord( 60, 61, grass with { Type = ParkRideChoice.QueueCellType, TileSet = 2, TileIndex = 4, TileAngle = 180, ParentId = 2996 } );
		state.SetRecord( 49, 22, ParkPathBuilding.Cleared( shipped.CellAt( 49, 22 ) ) );
		state.SetRecord( 50, 22, shipped.CellAt( 50, 22 ) with { TileIndex = 5 } );
		state.SetRecord( 51, 22, shipped.CellAt( 51, 22 ) with { TileAngle = 90 } );
		state.SetRecord( 52, 22, shipped.CellAt( 52, 22 ) with { Neighbours = 0x11 } );
		state.SetRecord( 61, 61, grass with { Type = ParkRideChoice.QueueCellType, TileSet = 2, TileIndex = 8 } );

		var pieces = new Dictionary<int, ParkFileWriter.QueuePiece?>();
		var cells = Level.WrittenCells( shipped, state, [], new HashSet<int>(), pieces );

		Assert.AreEqual( 6, cells.Count );
		CollectionAssert.AreEquivalent( new[] { Index( 60, 61 ), Index( 49, 22 ), Index( 50, 22 ), Index( 51, 22 ) }, pieces.Keys.ToArray() );
		Assert.AreEqual( new ParkFileWriter.QueuePiece( 4, 180 ), pieces[Index( 60, 61 )] );
		Assert.IsNull( pieces[Index( 49, 22 )] );
		Assert.AreEqual( new ParkFileWriter.QueuePiece( 5, 270 ), pieces[Index( 50, 22 )] );
		Assert.AreEqual( new ParkFileWriter.QueuePiece( 2, 90 ), pieces[Index( 51, 22 )] );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_QUEUE_CELL_MODEL" ).Times, "the tile with no piece" );

		// With nowhere to put them, each is counted.
		Unimplemented.Forget();
		Level.WrittenCells( shipped, state, [], new HashSet<int>() );
		Assert.AreEqual( 5, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_QUEUE_CELL_MODEL" ).Times );
	}

	/// <summary>A queue cell a thing bought now stands on gives up its piece, and one a thing sold has left for a queue takes one.</summary>
	[TestMethod]
	public void ACellUnderAThingBoughtGivesUpItsPiece()
	{
		Assert.IsTrue( catalogue.TryGet( 1413, out var camera ) );

		var state = new ParkState( shipped );
		var placed = ParkBuilding.Constructed( camera, 43, 50, 22, 0, 0, 0, new ParkWorld.BuiltWhen( 2000, 2, 10, 4, 2, 30, 7, 9 ) );
		var sold = shipped.Objects.Single( thing => thing.ThingId == 17 );
		var (soldX, soldY) = (sold.RawX >> 8, sold.RawY >> 8);

		state.SetRecord( 50, 22, shipped.CellAt( 50, 22 ) with { Type = CellEdge.Footprint, ParentId = (ushort)MapStep.CellId( 50, 22 ) } );
		state.SetRecord( soldX, soldY, shipped.CellAt( soldX, soldY ) with { Type = ParkRideChoice.QueueCellType, TileSet = 2, TileIndex = 2, TileAngle = 90, ParentId = 2996 } );

		var pieces = new Dictionary<int, ParkFileWriter.QueuePiece?>();
		var cells = Level.WrittenCells( shipped, state, [placed], new HashSet<int> { 17 }, pieces );

		Assert.AreEqual( 2, cells.Count );
		Assert.IsTrue( pieces.ContainsKey( Index( 50, 22 ) ) && pieces[Index( 50, 22 )] == null );
		Assert.AreEqual( new ParkFileWriter.QueuePiece( 2, 90 ), pieces[Index( soldX, soldY )] );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What.StartsWith( "SAVE_PARK_" ) ) );
	}

	/// <summary>
	/// A park's own save: a queue cut and laid with the park's own tools is written with each cell's model, and
	/// nothing is counted for one.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesAQueueLaidAndAQueueCleared()
	{
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );

		var grass = shipped.CellAt( 60, 61 );

		state.SetRecord( 60, 61, grass with { Type = ParkRideChoice.QueueCellType, TileSet = 2, TileIndex = 6, TileAngle = 90, ParentId = 2996 } );
		state.SetRecord( 49, 22, ParkPathBuilding.Cleared( shipped.CellAt( 49, 22 ) ) );
		Unimplemented.Forget();

		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, "Queued", people, null, rides, catalogue ) );

		using var stream = new MemoryStream( File.ReadAllBytes( Path.Combine( Jungle, "Queued.TPWS" ) ) );
		var reader = new SaveReader( stream );
		var body = reader.ReadFile();
		var written = new ParkWorld( body, reader.Preamble );
		var models = Models( written );

		Assert.IsNull( written.Problem );
		Assert.IsNull( models.Problem );
		Assert.AreEqual( (0, 91), (written.CellAt( 49, 22 ).MeshInstance, written.CellAt( 60, 61 ).MeshInstance) );
		Assert.AreEqual( (17006, null), (models.ItemIn( 90 ), models.ItemIn( 113 )) );
		Assert.IsTrue( body.AsSpan().IndexOf( ParkThingStates.QueuePieceRecord( 60, 61, 6, 90 ) ) > 0 );
		Assert.AreEqual( (161, 7, 91), models.Header );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_QUEUE_CELL_MODEL" ) );

		// Without the park's scripts the things go out as the file's, and so does each cell's model, counted.
		Unimplemented.Forget();
		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, "Plain", people ) );

		using var plainStream = new MemoryStream( File.ReadAllBytes( Path.Combine( Jungle, "Plain.TPWS" ) ) );
		var plain = new ParkWorld( new SaveReader( plainStream ).ReadFile() );

		Assert.AreEqual( (114, 0), (plain.CellAt( 49, 22 ).MeshInstance, plain.CellAt( 60, 61 ).MeshInstance) );
		Assert.AreEqual( 2, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_QUEUE_CELL_MODEL" ).Times );
	}
}
