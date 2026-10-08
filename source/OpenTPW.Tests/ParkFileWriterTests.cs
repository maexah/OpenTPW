using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer: the shipped park written back with a running park's clock, door, visitor count, cash,
/// camera and changed cells, and read again by the readers. These read real game files and are skipped where there is
/// no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkFileWriterTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int Preamble = 0x60D;

	private static readonly ParkFileWriter.Running Played = new( GameTick: 1234, ParkClosed: true, VisitorsToDate: 77,
		Balance: 54321, Camera: new ParkCameraModule.View( Zoom: 95f, YRotation: 1.5f, PointX: 300f, PointZ: 250f ) );

	private byte[] raw = null!;
	private ParkWorld shipped = null!;
	private string? root;
	private BaseFileSystem oldSaves = null!;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		raw = FileSystem.ReadAllBytes( ShippedPark );
		shipped = Read( raw );

		oldSaves = SaveFileSystem;
		root = Directory.CreateTempSubdirectory( "opentpw-park-writer-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void RestoreSaves()
	{
		// Without the game the set-up stops before anything is changed.
		if ( root == null )
			return;

		SetCurrentPlayer( null );
		SaveFileSystem = oldSaves;
		Directory.Delete( root, true );
		ParkOrbitCameraMode.Forget();
		Unimplemented.Forget();
	}

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	private static byte[] Inflate( byte[] file )
	{
		using var stream = new MemoryStream( file );
		return new SaveReader( stream ).ReadFile();
	}

	/// <summary>What the file's own fields read, so writing them back should change nothing.</summary>
	private ParkFileWriter.Running AsShipped() => new( shipped.GameTick, shipped.ParkClosed != 0,
		shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value );

	[TestMethod]
	public void TheShippedParksCameraIsReadFromItsModule()
	{
		Assert.IsNull( shipped.Camera.Problem );
		var saved = shipped.Camera.Saved!.Value;

		Assert.AreEqual( (110f, 0f, 475f), (saved.Zoom, saved.YRotation, saved.PointX) );
		Assert.AreEqual( 175.007f, saved.PointZ, 0.001f );
	}

	[TestMethod]
	public void AWrittenParkReadsBackWithTheRunningParksNumbers()
	{
		var written = Read( ParkFileWriter.Write( shipped, Played ) );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer, "the world block still ends on its tag" );
		Assert.AreEqual( 1234, written.GameTick );
		Assert.AreEqual( 1, written.ParkClosed );
		Assert.AreEqual( 77, written.NumberOfVisitorsToDate );
		Assert.AreEqual( 54321, written.Economy!.Value.Balance );
		Assert.AreEqual( Played.Camera, written.Camera.Saved );

		// The shipped park's own are other numbers, so none of the five was read back from a field left alone.
		Assert.AreEqual( 755, shipped.GameTick );
		Assert.AreEqual( 0, shipped.ParkClosed );
		Assert.AreEqual( 0, shipped.NumberOfVisitorsToDate );
		Assert.AreEqual( 87987, shipped.Economy!.Value.Balance );
	}

	[TestMethod]
	public void AnOpenParkIsWrittenOpen()
	{
		var closed = Read( ParkFileWriter.Write( shipped, Played ) );
		var open = Read( ParkFileWriter.Write( closed, Played with { ParkClosed = false } ) );

		Assert.AreEqual( 0, open.ParkClosed );
	}

	/// <summary>
	/// Every module is carried: the body differs from the file's only inside the eight fields written, and not at
	/// all when the running park's numbers are the file's own. The shipped file is not written to.
	/// </summary>
	[TestMethod]
	public void OnlyTheFieldsWrittenDifferFromTheBodyRead()
	{
		var before = Inflate( raw );
		var same = ParkFileWriter.Body( shipped, AsShipped() );

		CollectionAssert.AreEqual( before, same, "the file's own numbers written over it" );

		var body = ParkFileWriter.Body( shipped, Played );
		Assert.AreEqual( before.Length, body.Length );

		var header = 8 + BitConverter.ToInt32( before, 4 );
		var camera = Find( before, "EMAK" ) - 40;
		var economy = Enumerable.Range( 0, before.Length - 4 ).Single( at => BitConverter.ToInt32( before, at ) == 87987 );

		// mGameTick, mParkClosed, mNumberOfVisitorsToDate; mBalance; the zoom and rotation, the point's x, its z.
		(int At, int Size)[] written =
			[(header + 14, 4), (header + 22, 4), (header + 26, 4), (economy, 4), (camera, 8), (camera + 12, 4), (camera + 20, 4)];

		var changed = Enumerable.Range( 0, body.Length ).Where( at => body[at] != before[at] ).ToArray();
		var outside = changed.Where( at => !written.Any( field => at >= field.At && at < field.At + field.Size ) ).ToArray();

		Assert.AreEqual( 0, outside.Length, $"bytes changed outside the fields written, the first at 0x{outside.FirstOrDefault():x}" );

		foreach ( var field in written )
			Assert.IsTrue( changed.Any( at => at >= field.At && at < field.At + field.Size ), $"nothing changed in the field at 0x{field.At:x}" );

	}

	/// <summary><see cref="ParkWorld"/> is the file: writing a park from it leaves the body it holds as it was read.</summary>
	[TestMethod]
	public void TheBodyReadIsNotWrittenTo()
	{
		var held = Inflate( raw );
		var world = new ParkWorld( held, raw[..Preamble] );

		ParkFileWriter.Write( world, Played );

		CollectionAssert.AreEqual( Inflate( raw ), held );
	}

	/// <summary>
	/// The container: the original's version for a written park, the loaded file's preamble after it, and a block
	/// whose two lengths are the body's and its own to the end of the file.
	/// </summary>
	[TestMethod]
	public void TheContainerIsVersion500OverTheLoadedFilesPreamble()
	{
		var file = ParkFileWriter.Write( shipped, Played );

		Assert.AreEqual( 400, BitConverter.ToInt32( raw, 0 ), "the shipped park's version" );
		Assert.AreEqual( 500, BitConverter.ToInt32( file, 0 ), "a written park's" );
		CollectionAssert.AreEqual( raw[4..Preamble], file[4..Preamble], "the preamble past the version" );

		Assert.AreEqual( "BILZ", Encoding.ASCII.GetString( file, Preamble, 4 ) );
		Assert.AreEqual( Inflate( raw ).Length, BitConverter.ToInt32( file, Preamble + 4 ), "the length the body inflates to" );
		Assert.AreEqual( file.Length - Preamble, BitConverter.ToInt32( file, Preamble + 8 ), "the block's length, header and all" );

		CollectionAssert.AreEqual( new[] { 15, 9, 0, 0 },
			Enumerable.Range( 0, 4 ).Select( i => BitConverter.ToInt32( file, Preamble + 12 + (4 * i) ) ).ToArray() );

		Assert.AreEqual( 0x78, file[Preamble + 28], "a zlib stream begins after the 28-byte header" );
	}

	[TestMethod]
	public void ABodyWithNoFileBehindItIsRefused()
	{
		var bare = new ParkWorld( Inflate( raw ) );

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Write( bare, Played ) );
	}

	[TestMethod]
	public void AParkFileNotReadWholeIsRefused()
	{
		var body = Inflate( raw );
		var cut = new ParkWorld( body[..(body.Length / 2)], raw[..Preamble] );

		Assert.IsNotNull( cut.Problem );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Write( cut, Played ) );
	}

	[TestMethod]
	public void ABodyWithNoCameraModuleIsRefused()
	{
		var body = Inflate( raw );
		Encoding.ASCII.GetBytes( "XXXX" ).CopyTo( body, Find( body, "EMAK" ) );
		var world = new ParkWorld( body, raw[..Preamble] );

		Assert.IsNull( world.Problem, "the world block reads without the camera" );
		Assert.IsNotNull( world.Camera.Problem );
		Assert.IsNull( world.Camera.Saved );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Write( world, Played ) );
	}

	/// <summary>
	/// The level's half: the running park's clock, door, count and cash and the orbit camera's view, written into the
	/// player's folder for the theme under the name given.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheRunningParkIntoThePlayersFolder()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var state = new ParkState( shipped );
		state.SetGameTick( 900 );
		state.SetParkClosed( true );
		state.Deposit( 111 );
		state.Admit();
		state.Admit();

		ParkOrbitCameraMode.Zoom = 80f;
		ParkOrbitCameraMode.Yaw = 0.75f;
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 310f, 220f, 0f );

		var path = Level.WritePark( shipped, state, "jungle", "My Park" );

		Assert.AreEqual( Path.Join( "users", "1Test", "jungle", "My Park.TPWS" ), path );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "My Park.TPWS" ) ) );

		Assert.AreEqual( 900, written.GameTick );
		Assert.AreEqual( 1, written.ParkClosed );
		Assert.AreEqual( 2, written.NumberOfVisitorsToDate );
		Assert.AreEqual( 88098, written.Economy!.Value.Balance );
		Assert.AreEqual( new ParkCameraModule.View( 80f, -0.75f, 310f, 220f ), written.Camera.Saved, "the rotation turns the other way" );

		Assert.AreEqual( "My Park", SaveFolder.SavedParks( 0, "Test", "jungle" ).Single().Name, "the Load Park screen's list" );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_WITH_NO_FILE" ) );
	}

	/// <summary>
	/// The level hands the writer the park's people: a guest made here is in the file it writes, and one written
	/// without a like sprite in the file to copy its set byte from is counted.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheParksPeople()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var people = new ParkPeople( shipped );

		try
		{
			var made = people.Admit( 47, 21 );

			Assert.IsNotNull( Level.WritePark( shipped, people.State, "jungle", "Peopled", people ) );

			var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Peopled.TPWS" ) ) );

			Assert.AreEqual( shipped.ThingCount + 1, written.ThingCount );
			Assert.AreEqual( made, written.Things[0].ThingId, "the newest thing takes the list's head" );
			Assert.AreEqual( 19, written.Sprites.Count );

			ParkFileWriter.Body( shipped, new ParkFileWriter.Running( 0, false, 0, 0, shipped.Camera.Saved!.Value,
				People: people.Written( Level.WrittenThings( shipped ).Contains ) ), out var report );

			Assert.AreEqual( 1, report!.Value.UnmatchedSpriteSets, "the file holds no child standing on the made guest's bank" );
			Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_SPRITE_SET_BYTE" ).Times );
			Assert.IsTrue( written.ClosedOnTrailer && written.ClosedOnSpriteTrailer );

			Assert.IsNotNull( Level.WritePark( shipped, people.State, "jungle", "Carried" ) );
			Assert.AreEqual( shipped.ThingCount, Read( File.ReadAllBytes( Path.Combine( Jungle, "Carried.TPWS" ) ) ).ThingCount,
				"with no people handed over the file's own go out" );
		}
		finally
		{
			TestRun.DeleteEvery<ParkPeople>();
		}
	}

	/// <summary>The park the level itself reads for a player can be written back: it keeps its file's preamble.</summary>
	[TestMethod]
	public void TheParkTheLevelReadsForAPlayerCanBeWrittenBack()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		File.WriteAllBytes( Path.Combine( Jungle, "easymode.TPWI" ), raw );

		var park = Level.CreatePark( "jungle", new ParkBalance( "jungle", easyMode: true ),
			new ParkItemCatalogue( "jungle", instantAction: true ) );

		Assert.IsNotNull( Level.WritePark( park, new ParkState( park ), "jungle", "Again" ) );
		Assert.AreEqual( 500, BitConverter.ToInt32( File.ReadAllBytes( Path.Combine( Jungle, "Again.TPWS" ) ), 0 ) );
	}

	[TestMethod]
	public void ASaveUnderANameInAnotherCaseReplacesThatFile()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		File.WriteAllBytes( Path.Combine( Jungle, "my park.tpws" ), [1, 2, 3, 4] );

		var path = Level.WritePark( shipped, new ParkState( shipped ), "jungle", "My Park" );

		Assert.AreEqual( "my park.tpws", Path.GetFileName( path ) );
		Assert.AreEqual( 1, Directory.GetFiles( Jungle ).Length, "one file, replaced" );
		Assert.AreEqual( 755, Read( File.ReadAllBytes( Path.Combine( Jungle, "my park.tpws" ) ) ).GameTick );
	}

	[TestMethod]
	public void AParkMadeFreshIsCountedAndNotWritten()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );

		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ),
			new ParkItemCatalogue( "jungle", instantAction: false ) );

		Assert.IsNull( Level.WritePark( fresh, new ParkState( fresh ), "jungle", "Fresh" ) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_WITH_NO_FILE" ).Times );
		Assert.AreEqual( 0, Directory.GetFiles( Jungle ).Length );
	}

	[TestMethod]
	public void WithNobodyPlayingNothingIsWritten()
	{
		SetCurrentPlayer( null );

		Assert.IsNull( Level.WritePark( shipped, new ParkState( shipped ), "jungle", "Nobody's" ) );
		Assert.AreEqual( 0, Directory.GetFiles( root!, "*", SearchOption.AllDirectories ).Length );
	}

	/// <summary>Where a cell's status byte sits in a body whose every cell has a map and a track record, or those and effects.</summary>
	private static int CellAt( byte[] body, int index )
	{
		var at = 8 + BitConverter.ToInt32( body, 4 ) + 64 + (150 * 32) + 6 + (32 * 20) + 76;

		for ( var cell = 0; cell < index; ++cell )
			at += 1 + ((body[at] & 1) != 0 ? 52 : 0) + ((body[at] & 2) != 0 ? 31 : 0) + ((body[at] & 4) != 0 ? 10 : 0);

		return at;
	}

	private static int Index( int x, int y ) => (y * ParkWorld.MapSize) + x;

	/// <summary>The Belly Bounce's queue cell at (49,22), with every field the writer writes moved off the file's.</summary>
	private ParkWorld.MapCell Moved() => shipped.CellAt( 49, 22 ) with
	{
		Direction = 0x10, Flags = 0x20, Neighbours = 0x55, OverlapCounter = -1, ParentId = 1234,
		TileSet = 1, TileIndex = 7, TileAngle = 180, Type = 1,
		TrackType = 25, TrackFlags = 3, TrackParentId = 4321, TrackNeighbours = 0x11
	};

	/// <summary>
	/// A cell handed to the writer reads back field for field, the rest of its record as the file's (the reader's
	/// record holds its litter block and <c>mWho</c> too), and no other cell moves.
	/// </summary>
	[TestMethod]
	public void ACellWrittenReadsBackFieldForFieldAndNoOtherCellMoves()
	{
		var was = shipped.CellAt( 49, 22 );
		var moved = Moved();

		// Each of the thirteen is another value than the file's, so none reads back from a field left alone.
		Assert.AreEqual( (3, (ushort)0, (byte)68, (byte)4, 2, 5, 270, (short)0, (ushort)2996),
			(was.Type, was.Flags, was.Neighbours, was.Direction, was.TileSet, was.TileIndex, was.TileAngle, was.OverlapCounter, was.ParentId) );
		Assert.AreEqual( (0, (ushort)0, (ushort)0, (byte)0), (was.TrackType, was.TrackFlags, was.TrackParentId, was.TrackNeighbours) );

		// The Jungle Spray's own cell, which names it: a cell's mWho is not the writer's to move.
		var stoodOn = shipped.CellAt( 55, 15 ) with { TileIndex = 9 };
		Assert.AreEqual( 23, stoodOn.Occupant );

		var cells = new Dictionary<int, ParkWorld.MapCell> { [Index( 49, 22 )] = moved, [Index( 55, 15 )] = stoodOn with { Occupant = 0 } };
		var written = Read( ParkFileWriter.Write( shipped, AsShipped() with { Cells = cells } ) );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer );
		Assert.AreEqual( moved, written.CellAt( 49, 22 ) );
		Assert.AreEqual( stoodOn, written.CellAt( 55, 15 ), "the tile written, mWho the file's 23" );

		for ( var index = 0; index < shipped.Cells.Count; ++index )
			if ( index != Index( 49, 22 ) && index != Index( 55, 15 ) )
				Assert.AreEqual( shipped.Cells[index], written.Cells[index], $"cell {index}" );
	}

	/// <summary>
	/// The bytes: only the cell's own thirteen fields differ from the body read. Its mesh instance (a queue cell's is
	/// a handle), its hoarding neighbours, its litter block, <c>mWho</c> and its track record's segment are the file's.
	/// </summary>
	[TestMethod]
	public void OnlyTheCellsWrittenFieldsDifferFromTheBodyRead()
	{
		var before = Inflate( raw );
		var cells = new Dictionary<int, ParkWorld.MapCell> { [Index( 49, 22 )] = Moved() };
		var body = ParkFileWriter.Body( shipped, AsShipped() with { Cells = cells } );

		Assert.AreEqual( before.Length, body.Length );

		var map = CellAt( before, Index( 49, 22 ) ) + 1;
		var track = map + 52;

		Assert.AreEqual( 3, before[map - 1], "a map and a track record" );
		Assert.AreNotEqual( 0, BitConverter.ToInt32( before, map + 3 ), "the queue cell's mesh instance" );

		// mDirection and mFlags; mNeighbours to mTileData; mType; then the track record's flags, neighbours, parent, type.
		(int At, int Size)[] fields =
			[(map, 3), (map + 7, 17), (map + 24, 4), (track + 1, 2), (track + 7, 1), (track + 10, 2), (track + 24, 4)];

		var changed = Enumerable.Range( 0, body.Length ).Where( at => body[at] != before[at] ).ToArray();
		var outside = changed.Where( at => !fields.Any( field => at >= field.At && at < field.At + field.Size ) ).ToArray();

		Assert.AreEqual( 0, outside.Length, $"bytes changed outside the fields written, the first at 0x{outside.FirstOrDefault():x}" );

		foreach ( var field in fields )
			Assert.IsTrue( changed.Any( at => at >= field.At && at < field.At + field.Size ), $"nothing changed in the field at +{field.At - map}" );
	}

	/// <summary>The last cell is found as the first is: the walk to a cell steps over every record before it.</summary>
	[TestMethod]
	public void TheFirstAndTheLastCellAreWrittenWhereTheyLie()
	{
		var last = (ParkWorld.MapSize * ParkWorld.MapSize) - 1;
		var cells = new Dictionary<int, ParkWorld.MapCell>
		{
			[0] = shipped.Cells[0] with { TileIndex = 41 },
			[last] = shipped.Cells[last] with { TileIndex = 42 }
		};

		var written = Read( ParkFileWriter.Write( shipped, AsShipped() with { Cells = cells } ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( 41, written.Cells[0].TileIndex );
		Assert.AreEqual( 42, written.Cells[last].TileIndex );
		Assert.AreEqual( shipped.Cells[1], written.Cells[1] );
		Assert.AreEqual( shipped.Cells[last - 1], written.Cells[last - 1] );
	}

	/// <summary>The first cell's record with one of its parts cut out, and its status byte saying so.</summary>
	private ParkWorld Without( int part )
	{
		var body = Inflate( raw ).ToList();
		var at = CellAt( Inflate( raw ), 0 );

		body[at] = (byte)(3 & ~part);
		body.RemoveRange( part == 1 ? at + 1 : at + 53, part == 1 ? 52 : 31 );

		var world = new ParkWorld( body.ToArray(), raw[..Preamble] );

		Assert.IsNull( world.Problem );
		Assert.IsTrue( world.ClosedOnTrailer );
		return world;
	}

	/// <summary>
	/// The original's writer gives every cell a map and a track record, so a file without one is none of its own: a
	/// cell is not written where its record is not.
	/// </summary>
	[TestMethod]
	public void ACellWithNoRecordToWriteOverIsRefused()
	{
		var mapless = Without( 1 );
		var cells = new Dictionary<int, ParkWorld.MapCell> { [0] = shipped.Cells[0] with { TileIndex = 41 } };

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( mapless, AsShipped() with { Cells = cells } ) );

		var trackless = Without( 2 );
		var tracked = new Dictionary<int, ParkWorld.MapCell> { [0] = shipped.Cells[0] with { TrackType = 25 } };

		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( trackless, AsShipped() with { Cells = tracked } ) );

		// With no track field to write the map record is written, and the cell after it is found.
		var body = ParkFileWriter.Body( trackless, AsShipped() with { Cells = new Dictionary<int, ParkWorld.MapCell>
		{
			[0] = shipped.Cells[0] with { TileIndex = 41 },
			[1] = shipped.Cells[1] with { TileIndex = 42 }
		} } );
		var written = new ParkWorld( body, raw[..Preamble] );

		Assert.AreEqual( (41, 42), (written.Cells[0].TileIndex, written.Cells[1].TileIndex) );
		Assert.AreEqual( shipped.Cells[2], written.Cells[2] );
	}

	/// <summary>
	/// The level's half: of the cells the running park has changed, a path laid, a path cleared, a link on an entrance
	/// and a queue cell go into the file; a cell put back as the file has it is not counted among them; a cell that
	/// joins or leaves a footprint is left as the file's and counted, and a queue cell is counted for its model.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheGroundThePlayerChangedButAFootprint()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var state = new ParkState( shipped );
		var grass = Enumerable.Range( 0, shipped.Cells.Count ).Where( index => shipped.Cells[index] is { Type: 0, Flags: 0 } ).Take( 4 )
			.Select( index => (X: index % ParkWorld.MapSize, Y: index / ParkWorld.MapSize) ).ToArray();
		var entrance = Enumerable.Range( 0, shipped.Cells.Count ).Last( index => shipped.Cells[index].Type == CellEdge.RideEnd );
		var (entranceX, entranceY) = (entrance % ParkWorld.MapSize, entrance / ParkWorld.MapSize);

		Assert.AreEqual( (CellEdge.Path, CellEdge.RideEnd, ParkRideChoice.QueueCellType),
			(shipped.CellAt( 39, 21 ).Type, shipped.CellAt( 55, 15 ).Type, shipped.CellAt( 51, 22 ).Type) );
		Assert.AreNotEqual( Index( 55, 15 ), entrance );

		var laid = shipped.CellAt( grass[0].X, grass[0].Y ) with { Type = CellEdge.Path, TileSet = 1, TileIndex = 2, Neighbours = 0x44, Direction = 0x04 };
		var cleared = ParkPathBuilding.Cleared( shipped.CellAt( 39, 21 ) ) with { OverlapCounter = 0 };
		var linked = shipped.Cells[entrance] with { Neighbours = (byte)(shipped.Cells[entrance].Neighbours ^ 0x04) };
		var queued = shipped.CellAt( grass[1].X, grass[1].Y ) with { Type = ParkRideChoice.QueueCellType, TileSet = 2, TileIndex = 2, ParentId = 2996 };

		state.SetRecord( grass[0].X, grass[0].Y, laid );
		state.SetRecord( 39, 21, cleared );
		state.SetRecord( entranceX, entranceY, linked );
		state.SetRecord( grass[1].X, grass[1].Y, queued );
		state.SetRecord( 51, 22, shipped.CellAt( 51, 22 ) with { TileIndex = 3 } );                       // a queue cell tiled again
		state.SetRecord( 40, 21, shipped.CellAt( 40, 21 ) );                                              // changed and put back
		state.SetRecord( 55, 15, ParkPathBuilding.Cleared( shipped.CellAt( 55, 15 ) ) );                  // a thing sold
		state.SetRecord( grass[2].X, grass[2].Y, shipped.CellAt( grass[2].X, grass[2].Y ) with { Type = CellEdge.Footprint, ParentId = 77 } ); // one bought
		state.SetRecord( grass[3].X, grass[3].Y, shipped.CellAt( grass[3].X, grass[3].Y ) with { Type = CellEdge.RideEnd } );

		var cells = Level.WrittenCells( shipped, state );

		CollectionAssert.AreEquivalent(
			new[] { Index( grass[0].X, grass[0].Y ), Index( 39, 21 ), entrance, Index( grass[1].X, grass[1].Y ), Index( 51, 22 ) },
			cells.Keys.ToArray() );

		Unimplemented.Forget();
		Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Ground" ) );

		Assert.AreEqual( 3, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_FOOTPRINT_CELL" ).Times );
		Assert.AreEqual( 2, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_QUEUE_CELL_MODEL" ).Times );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Ground.TPWS" ) ) );

		Assert.AreEqual( laid, written.CellAt( grass[0].X, grass[0].Y ) );
		Assert.AreEqual( cleared, written.CellAt( 39, 21 ) );
		Assert.AreEqual( linked, written.Cells[entrance] );
		Assert.AreEqual( queued, written.CellAt( grass[1].X, grass[1].Y ) );
		Assert.AreEqual( 3, written.CellAt( 51, 22 ).TileIndex );
		Assert.AreEqual( shipped.CellAt( 55, 15 ), written.CellAt( 55, 15 ), "the sold thing's cell is the file's" );
		Assert.AreEqual( shipped.CellAt( grass[2].X, grass[2].Y ), written.CellAt( grass[2].X, grass[2].Y ), "the bought thing's cell is the file's" );
		Assert.AreEqual( 5, Enumerable.Range( 0, shipped.Cells.Count ).Count( index => shipped.Cells[index] != written.Cells[index] ) );
	}

	/// <summary>A park nobody has built in writes no cell and counts nothing.</summary>
	[TestMethod]
	public void AParkWithItsGroundUntouchedWritesNoCell()
	{
		var state = new ParkState( shipped );

		Assert.AreEqual( 0, Level.WrittenCells( shipped, state ).Count );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What.StartsWith( "SAVE_PARK_" ) ) );
	}

	private static int Find( byte[] data, string tag )
	{
		var bytes = Encoding.ASCII.GetBytes( tag );
		return Enumerable.Range( 0, data.Length - bytes.Length ).Last( at => data.AsSpan( at, bytes.Length ).SequenceEqual( bytes ) );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
