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
	/// The level hands the writer the park's things: with its scripts bound and two seconds gone, the file's clock
	/// has moved on by them and the Belly Bounce's queue head is the park's; without them the file's go out, and with
	/// a file whose clock will not read they go out counted.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheThings()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		Time.Paused = false;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var state = new ParkState( shipped );
		var rides = new ParkRides( "jungle", shipped, catalogue, FileSystem );

		try
		{
			for ( var frame = 0; frame < 4; ++frame )
			{
				Time.Update( 0.5f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );
			}

			state.JoinQueue( 13, 31 );

			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Things", rides: rides, catalogue: catalogue ) );

			var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Things.TPWS" ) ) );

			Assert.AreEqual( shipped.Clock.Reading + (64u * 31u), written.Clock.Reading );
			Assert.AreEqual( 31, written.Objects.Single( placed => placed.ThingId == 13 ).FirstInQueue );
			Assert.IsNull( written.ThingStates( id => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 ).Problem, "the Jungle Spray's three channels are walked as three" );
			Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_THINGS_AS_THE_FILE" ) );

			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Carried" ) );

			var carried = Read( File.ReadAllBytes( Path.Combine( Jungle, "Carried.TPWS" ) ) );

			Assert.AreEqual( shipped.Clock.Reading, carried.Clock.Reading, "with no scripts handed over the file's own go out" );
			Assert.AreEqual( 0, carried.Objects.Single( placed => placed.ThingId == 13 ).FirstInQueue );
			Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_THINGS_AS_THE_FILE" ) );

			// A file whose clock will not read: its things go out as the file's, and that is counted.
			using var stream = new MemoryStream( raw );
			var reader = new SaveReader( stream );
			var body = reader.ReadFile();

			body[body.AsSpan().IndexOf( "KOLC"u8 )] = (byte)'X';

			var broken = new ParkWorld( body, reader.Preamble );
			var unread = new ParkRides( "jungle", broken, catalogue, FileSystem );

			try
			{
				Assert.IsNotNull( Level.WritePark( broken, new ParkState( broken ), "jungle", "Unread", rides: unread, catalogue: catalogue ) );
				Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_THINGS_AS_THE_FILE" ).Times );
			}
			finally
			{
				unread.Delete();
			}
		}
		finally
		{
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// The level hands the writer a thing bought and a thing sold: the bought one's three records under its item's
	/// two lines of name, its cells and its control; the sold one's taken out; and a thing whose item will not write
	/// (a track ride, bumper or tracked) counted and left out. A thing whose folder holds emitter files is written.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesAThingBoughtAndAThingSold()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( "jungle", shipped, catalogue, FileSystem );

		int Buy( int itemId, int x, int y )
		{
			Assert.IsTrue( catalogue.TryGet( itemId, out var item ) );

			var id = state.NextThingId();

			var placed = ParkBuilding.Constructed( item, id, x, y, 0, MapStep.CellId( x + item.EntryDeltaX, y + item.EntryDeltaY ),
				MapStep.CellId( x + item.ExitDeltaX, y + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ),
				ParkBuilding.TakeTrackRide( state, item ) );

			state.AddObject( placed );
			ParkBuilding.BindOperation( state, rides, placed, item );
			ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, x, y, 0 ), x, y );
			state.EnterCell( x, y, id );

			return id;
		}

		try
		{
			state.SetGameTick( 900 );

			var ape = Buy( 1101, 41, 22 );
			// The karts: a track ride with no record of the bumper family's, so it is counted and left out.
			var racers = catalogue.All.First( item => item.BumperType != 0 && !ParkBumperCars.IsBumperFamily( item.BumperType ) );
			var karts = Buy( racers.Id, 20, 20 );
			var tracked = catalogue.All.First( item => item.BumperType == 0 && item.TrackType != 0 );
			var coaster = Buy( tracked.Id, 90, 90 );
			// The Loudspeaker, the one thing of the theme whose folder holds emitter files.
			var speaker = Buy( 1417, 70, 70 );
			var bounce = Buy( 1100, 30, 60 );

			// A guest walking to the bought ride names it, and the file keeps the name.
			var walker = people.Guests.First();

			walker.Value.MajorDest = ape;


			StringAssert.StartsWith( ParkBuilding.Sell( state, shipped, catalogue, null, rides, 16, people ), "sell: 'Drinks Shop' thing 16 sold" );
			Unimplemented.Forget();

			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Bought", people, null, rides, catalogue ) );

			var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Bought.TPWS" ) ) );
			var made = written.Objects.Single( thing => thing.ThingId == ape );
			var record = written.RecordOf( ape )!;

			Assert.IsNull( written.Problem );
			Assert.AreEqual( (1101, 91, rides.ScriptFor( ape )), (made.CatalogueId, made.MeshInstance, made.RideScript) );
			Assert.AreEqual( "Crazy", Encoding.Unicode.GetString( [.. Enumerable.Range( 0, 5 ).SelectMany( i => record.AsSpan( 60 + (i * 4), 2 ).ToArray() )] ) );
			Assert.AreEqual( "Ape", Encoding.Unicode.GetString( [.. Enumerable.Range( 0, 3 ).SelectMany( i => record.AsSpan( 62 + (i * 4), 2 ).ToArray() )] ) );
			Assert.AreEqual( bounce, written.FirstObject, "the newest heads the object list" );
			Assert.IsFalse( written.Objects.Any( thing => thing.ThingId is 16 || thing.ThingId == karts || thing.ThingId == coaster ), "the shop sold, and the two track rides left out" );
			Assert.AreEqual( 16, written.Objects.Count );
			Assert.AreEqual( (1417, 92, rides.ScriptFor( speaker )), (written.Objects.Single( thing => thing.ThingId == speaker ).CatalogueId, written.Objects.Single( thing => thing.ThingId == speaker ).MeshInstance, written.Objects.Single( thing => thing.ThingId == speaker ).RideScript), "a thing with emitter files is written as any other" );
			Assert.AreEqual( ape, written.People.Single( person => person.ThingId == walker.Key ).Guest!.Value.MajorDest, "a handle to a thing bought" );

			// The script's folder as the engine keeps one, and the model's footprint across then down.
			var body = Inflate( File.ReadAllBytes( Path.Combine( Jungle, "Bought.TPWS" ) ) );
			var folder = "data\\levels\\jungle\\rides\\monkey\\\0";
			var folderAt = body.AsSpan().IndexOf( Encoding.ASCII.GetBytes( folder ) );

			Assert.IsTrue( folderAt > 4 && BitConverter.ToInt32( body, folderAt - 4 ) == folder.Length, "its length before it" );
			Assert.IsTrue( body.AsSpan().IndexOf( (byte[])[1, .. BitConverter.GetBytes( 1100 ), .. BitConverter.GetBytes( 30 ), .. BitConverter.GetBytes( 60 ), .. BitConverter.GetBytes( 3 ), .. BitConverter.GetBytes( 4 )] ) > 0, "the Belly Bounce's model, three by four" );

			var script = written.ScriptStates.For( rides.ScriptFor( ape ) )!.Value;

			Assert.AreEqual( (ape, 91), ((int)script.Thing, script.ModelHandle) );
			Assert.IsNull( written.ScriptStates.For( 6 ), "the shop's script" );

			var models = written.ThingStates( id => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 );

			Assert.IsNull( models.Problem );
			Assert.AreEqual( (1101, rides.ScriptFor( ape )), (models.Things.Single( thing => thing.Slot == 90 ).CatalogueId, models.Things.Single( thing => thing.Slot == 90 ).ScriptHandle) );
			Assert.IsFalse( models.Things.Any( thing => thing.Slot == 115 ), "the shop's model" );
			Assert.AreEqual( (163, 5, 93), models.Header );

			var anchor = written.Cells[(22 * 128) + 41];
			var sold = written.Cells[(30 * 128) + 43];

			Assert.AreEqual( (4, MapStep.CellId( 41, 22 ), 8, ape), ((int)anchor.Type, (int)anchor.ParentId, (int)anchor.TileIndex, (int)anchor.Occupant) );
			Assert.AreEqual( (0, 0, 55, 0), ((int)sold.Type, (int)sold.ParentId, (int)sold.TileIndex, (int)sold.Occupant) );
			Assert.AreEqual( (1, 900u), (written.ObjectControlRecords.Single( control => control.ItemId == 1101 ).Standing, written.ObjectControlRecords.Single( control => control.ItemId == 1101 ).FirstBuilt) );
			Assert.AreEqual( (0, 15u), (written.ObjectControlRecords.Single( control => control.ItemId == 1203 ).Standing, written.ObjectControlRecords.Single( control => control.ItemId == 1203 ).FirstBuilt) );
			CollectionAssert.Contains( written.MessageSets()![0xb].ToList(), ape );
			CollectionAssert.DoesNotContain( written.MessageSets()![0xb].ToList(), 16 );

			Assert.AreEqual( (speaker, 92), ((int)written.ScriptStates.For( rides.ScriptFor( speaker ) )!.Value.Thing, written.ScriptStates.For( rides.ScriptFor( speaker ) )!.Value.ModelHandle) );
			Assert.AreEqual( 1417, models.Things.Single( thing => thing.Slot == 91 ).CatalogueId );
			Assert.AreEqual( 2, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_OBJECT_BOUGHT" ).Times, "the two track rides alone" );
			Assert.AreNotEqual( bounce, karts );
			Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_OBJECT_SOLD" ) );
			Assert.IsTrue( written.Cells[(20 * 128) + 20].Type != 4, "and its cells are the file's" );

			// Without the people nothing bought or sold is written, and each is counted.
			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "NoPeople", rides: rides, catalogue: catalogue ) );

			var plain = Read( File.ReadAllBytes( Path.Combine( Jungle, "NoPeople.TPWS" ) ) );

			Assert.IsTrue( plain.Objects.Any( thing => thing.ThingId == 16 ) && plain.Objects.All( thing => thing.ThingId != ape ) );
			Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_OBJECT_SOLD" ).Times );
		}
		finally
		{
			rides.Delete();
			Entity.ApplyDeletions();
			TestRun.DeleteEvery<ParkPeople>();
		}
	}

	private static int Counted( string gap ) => Unimplemented.Summary.Where( counted => counted.What == gap ).Sum( counted => counted.Times );

	/// <summary>
	/// A track ride's record in the track-rides module (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a track ride's
	/// record"). A Hot Pot bought and still shut is written, its record the original's own file's word for word; a
	/// load of that file holds the ride; kept, its record follows the running ride; with a boat out it is written
	/// with the boat, bought or kept ("OpenTPW's writer, a track ride's cars"); and sold it is taken out, its boats'
	/// models and their riders' heads with it.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesATrackRidesRecord()
	{
		const int HotPot = 1140;
		const int Handle = unchecked((int)0xffffff00);

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var people = new ParkPeople( shipped );
		var state = people.State;
		var rides = new ParkRides( "jungle", shipped, catalogue, FileSystem );
		ParkRides? again = null;

		ParkWorld Written( string name ) => Read( File.ReadAllBytes( Path.Combine( Jungle, name + ".TPWS" ) ) );

		try
		{
			Assert.IsTrue( catalogue.TryGet( HotPot, out var item ) );

			var id = state.NextThingId();
			var placed = ParkBuilding.Constructed( item, id, 41, 23, 0, MapStep.CellId( 41 + item.EntryDeltaX, 23 + item.EntryDeltaY ),
				MapStep.CellId( 41 + item.ExitDeltaX, 23 + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ),
				ParkBuilding.TakeTrackRide( state, item ) );

			state.AddObject( placed );
			ParkBuilding.BindOperation( state, rides, placed, item );
			ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, 41, 23, 0 ), 41, 23 );
			state.EnterCell( 41, 23, id );

			var cars = state.TrackRides.Cars;
			var (x, z) = ParkBumperCars.ArenaCentre( item, 41, 23, 0 );

			cars.Place( Handle, x, z );
			cars.SetPerformance( Handle, 60 );
			Unimplemented.Forget();

			// Bought and shut: written whole.
			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Shut", people, null, rides, catalogue ) );

			var shut = Written( "Shut" );

			Assert.IsNull( shut.Problem );
			Assert.AreEqual( new SavedTrackRide( Handle, 0x20400, 0x12c00, 0, HotPot, 60, 1, 1, 4350, 0 ), shut.TrackRides.Rides.Single() );
			Assert.AreEqual( (HotPot, Handle, 1), (shut.Objects.Single( thing => thing.ThingId == id ).CatalogueId, shut.Objects.Single( thing => thing.ThingId == id ).TrackRide, shut.Objects.Single( thing => thing.ThingId == id ).IsTrackRideValid) );
			Assert.AreEqual( 4, (int)shut.Cells[(23 * 128) + 41].Type, "and its footprint with it" );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_OBJECT_BOUGHT" ) );

			// With a boat out, a guest in it: the ride, the boat with its rider, the boat's model and its wake's,
			// and the rider's head on the seat's lookup record.
			var rider = people.Guests.First( guest => guest.Value.SpriteBank != 0 ).Key;
			var (leaver, boarder) = (people.Guests.Keys.First( guest => guest != rider ), people.Guests.Keys.Last( guest => guest != rider ));

			cars.OpenForLoading( Handle );

			// Open with somebody waiting to board and no boat yet: the ride is written whole all the same.
			Assert.IsTrue( cars.Board( Handle, boarder ) );
			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Boarder", people, null, rides, catalogue ) );
			Assert.AreEqual( (0, boarder, true), (Written( "Boarder" ).TrackRides.Cars.Count, Written( "Boarder" ).TrackRides.Listed.Single().Peep, Written( "Boarder" ).TrackRides.Listed.Single().Boarding) );
			cars.RideOf( Handle )!.Boarding.Clear();

			var launched = cars.Launch( Handle )!;

			Assert.IsTrue( cars.Board( Handle, rider ) );
			Assert.IsTrue( cars.Fill( Handle ) );

			// And one come off, one waiting for the next boat.
			cars.RideOf( Handle )!.Leaving.Add( leaver );
			Assert.IsTrue( cars.Board( Handle, boarder ) );

			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Boat", people, null, rides, catalogue ) );

			var boat = Written( "Boat" );

			Assert.IsNull( boat.Problem );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_OBJECT_BOUGHT" ) );
			Assert.AreEqual( 1, Counted( "SAVE_PARK_CAR_NO_HEIGHT" ), "nothing draws a boat here" );
			Assert.AreEqual( new SavedTrackRide( Handle, 0x20400, 0x12c00, 0, HotPot, 60, 1, 1, 4350, 1 ), boat.TrackRides.Rides.Single() );
			Assert.IsTrue( boat.Objects.Any( thing => thing.ThingId == id ) );

			var floated = boat.TrackRides.Cars.Single();

			Assert.AreEqual( ((int)launched.Flags, launched.X, launched.Z, launched.Steering, 4350, 3, 2, -1),
				(floated.Word( 0x00 ), floated.Word( 0x34 ), floated.Word( 0x38 ), floated.Word( 0x50 ), floated.Word( 0x88 ), floated.Word( 0x24 ), floated.Word( 0x28 ), floated.Word( 0xa4 )) );
			Assert.AreEqual( new SavedTrackRider( rider, 1 ), floated.Riders.Single() );
			Assert.AreEqual( (x, z), (floated.CentreX, floated.CentreZ) );
			CollectionAssert.AreEqual( new[] { (leaver, false), (boarder, true) }, boat.TrackRides.Listed.Select( peep => (peep.Peep, peep.Boarding) ).ToArray() );

			ParkThingStates Models( ParkWorld world ) => world.ThingStates( item => catalogue.TryGet( item, out var known ) ? known.AnimationChannels : 1 );

			var models = Models( boat );
			var (own, wake) = (floated.Word( 0x08 ) - 1, floated.Word( 0x0c ) - 1);

			Assert.IsNull( models.Problem );
			Assert.AreEqual( (HotPot + 2, HotPot + 1), (models.ItemIn( own ), models.ItemIn( wake )), "the boat's mesh and the wake's, each its own item" );

			var record = models.Things.Single( thing => thing.Slot == own );
			var trail = models.Things.Single( thing => thing.Slot == wake );

			CollectionAssert.AreEqual( new uint[] { 0x40, 0, 0x601, 0x601, 0x601, 0x601 }, record.NodeWords );
			CollectionAssert.AreEqual( new uint[] { 0x40 }, trail.NodeWords );
			Assert.AreEqual( new SavedChannel( 12, 0, 0, 1f, 0, 0, 0, 12, 0, 0, 0f ), record.Channels.Single() );
			Assert.AreEqual( new SavedChannel( 12, 0, 0, 0f, 0, 0, 0, 12, 0, 0, 0f ), trail.Channels.Single() );

			var lookups = models.LookupsOf( own )!.Value;
			var head = lookups.Records[0].Handle;

			CollectionAssert.AreEqual( new[] { (0x23, head), (0x21, -1), (0x29, -1), (0x29, -1) }, lookups.Records );
			Assert.AreEqual( (7, 1), (lookups.Shared, lookups.Attached) );
			Assert.AreEqual( (0, 0, 0), (models.LookupsOf( wake )!.Value.Records.Length, models.LookupsOf( wake )!.Value.Shared, models.LookupsOf( wake )!.Value.Attached) );
			Assert.AreEqual( ParkWorld.ChildHeadKind, boat.SpriteKindIn( head ) );
			Assert.AreEqual( people.Guests[rider].SpriteBank, boat.Sprites.Single( sprite => sprite.Slot == head ).Bank, "the rider's own head" );
			Assert.AreEqual( shipped.ThingStates( _ => 1 ).Header.Present + 3, models.Header.Present, "the pot's model, the boat's and the wake's" );

			// Its body's model record: the boat's flags, on no cell, of no footprint.
			var body = Inflate( File.ReadAllBytes( Path.Combine( Jungle, "Boat.TPWS" ) ) );
			var their = Convert.FromHexString( "01760400000000000000000000000000000000000001010000000000000000000000000000000000000000060004000700000001000000" );
			var at = body.AsSpan().IndexOf( their );

			Assert.IsTrue( at > 0, "the record's first 55 bytes are the original's own boat's" );

			// In a go the boat's clip is written running, and the words it marks.
			cars.Start( Handle );
			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Go", people, null, rides, catalogue ) );

			var go = Written( "Go" );
			var going = Models( go ).Things.Single( thing => thing.Slot == go.TrackRides.Cars.Single().Word( 0x08 ) - 1 );

			CollectionAssert.AreEqual( new uint[] { 0x40, 0x820, 0x601, 0x601, 0x601, 0x601 }, going.NodeWords );
			Assert.AreEqual( (5, 0, 1, 1f, 12), (going.Channels[0].Role, going.Channels[0].Entry, going.Channels[0].Flags, going.Channels[0].Speed, going.Channels[0].QueuedRole) );
			Assert.AreEqual( going.Channels[0].Time, going.Channels[0].NoPauseTime );
			Assert.AreEqual( (2, 5), (go.TrackRides.Rides.Single().State, go.TrackRides.Cars.Single().Word( 0x10 )) );

			// The boat's file loaded and written again: the boat takes the slots its file's gave up, and its rider's
			// head a new sprite, the file's gone.
			rides.Delete();
			Entity.ApplyDeletions();
			TestRun.DeleteEvery<ParkPeople>();

			var boatPeople = new ParkPeople( boat );
			var boatRides = new ParkRides( "jungle", boat, catalogue, FileSystem );

			try
			{
				Assert.AreEqual( 1, boatPeople.State.TrackRides.Cars.CarsOf( Handle ).Count() );
				Unimplemented.Forget();
				Assert.IsNotNull( Level.WritePark( boat, boatPeople.State, "jungle", "Again", boatPeople, null, boatRides, catalogue ) );

				var rewritten = Written( "Again" );
				var second = rewritten.TrackRides.Cars.Single();

				Assert.IsNull( rewritten.Problem );
				Assert.AreEqual( 0, Counted( "SAVE_PARK_TRACK_RIDE_AS_THE_FILE" ) );
				Assert.AreEqual( (own + 1, wake + 1), (second.Word( 0x08 ), second.Word( 0x0c )) );
				Assert.AreEqual( models.Header, Models( rewritten ).Header );
				Assert.AreEqual( new SavedTrackRider( rider, 1 ), second.Riders.Single() );

				var newHead = Models( rewritten ).LookupsOf( own )!.Value.Records[0];

				Assert.AreEqual( 0x23, newHead.Flags );
				Assert.AreNotEqual( head, newHead.Handle );
				Assert.AreEqual( (null, ParkWorld.ChildHeadKind), (rewritten.SpriteKindIn( head ), rewritten.SpriteKindIn( newHead.Handle )) );

				// Sold with its boat out: the ride, the boat, both models and the head go.
				StringAssert.StartsWith( ParkBuilding.Sell( boatPeople.State, boat, catalogue, null, boatRides, id, boatPeople ), "sell: 'The Hot Pot'" );
				Unimplemented.Forget();
				Assert.IsNotNull( Level.WritePark( boat, boatPeople.State, "jungle", "BoatSold", boatPeople, null, boatRides, catalogue ) );

				var boatSold = Written( "BoatSold" );

				Assert.IsNull( boatSold.Problem );
				Assert.AreEqual( 0, Counted( "SAVE_PARK_OBJECT_SOLD" ) );
				Assert.AreEqual( (0, 0), (boatSold.TrackRides.Rides.Count, boatSold.TrackRides.Cars.Count) );
				Assert.AreEqual( (null, null), (Models( boatSold ).ItemIn( own ), Models( boatSold ).ItemIn( wake )) );
				Assert.IsNull( boatSold.SpriteKindIn( head ) );
				Assert.AreEqual( shipped.ThingStates( _ => 1 ).Header.Present, Models( boatSold ).Header.Present );
			}
			finally
			{
				boatRides.Delete();
				Entity.ApplyDeletions();
				TestRun.DeleteEvery<ParkPeople>();
			}

			// The shut file loaded: the ride is the table's, and kept it is written over as it runs.
			rides.Delete();
			Entity.ApplyDeletions();
			TestRun.DeleteEvery<ParkPeople>();

			var loadedPeople = new ParkPeople( shut );
			var loaded = loadedPeople.State;

			again = new ParkRides( "jungle", shut, catalogue, FileSystem );

			var theirs = loaded.TrackRides.Cars;

			Assert.AreEqual( (4350, ParkBumperCars.RideState.Closed, 60), (theirs.RideOf( Handle )!.Duration, theirs.RideOf( Handle )!.State, theirs.RideOf( Handle )!.Performance) );

			theirs.Place( Handle, x, z );
			theirs.OpenForLoading( Handle );
			theirs.SetDuration( Handle, 750 );
			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( shut, loaded, "jungle", "Kept", loadedPeople, null, again, catalogue ) );
			Assert.AreEqual( new SavedTrackRide( Handle, 0x20400, 0x12c00, 0, HotPot, 60, 1, 1, 750, 1 ), Written( "Kept" ).TrackRides.Rides.Single() );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_TRACK_RIDE_AS_THE_FILE" ) );

			// Kept with a boat out: the ride is written again whole, the boat with it.
			Assert.IsNotNull( theirs.Launch( Handle ) );
			theirs.SetDuration( Handle, 900 );
			Assert.IsNotNull( Level.WritePark( shut, loaded, "jungle", "KeptBoat", loadedPeople, null, again, catalogue ) );
			Assert.AreEqual( (900, 1), (Written( "KeptBoat" ).TrackRides.Rides.Single().Duration, Written( "KeptBoat" ).TrackRides.CarsOf( Handle )) );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_TRACK_RIDE_AS_THE_FILE" ) );

			// Sold: its record and its close are taken out with its three records.
			StringAssert.StartsWith( ParkBuilding.Sell( loaded, shut, catalogue, null, again, id, loadedPeople ), "sell: 'The Hot Pot'" );
			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( shut, loaded, "jungle", "Sold", loadedPeople, null, again, catalogue ) );

			var sold = Written( "Sold" );

			Assert.IsNull( sold.Problem );
			Assert.IsTrue( sold.TrackRides.ClosedOnTag );
			Assert.AreEqual( 0, sold.TrackRides.Rides.Count );
			Assert.IsFalse( sold.Objects.Any( thing => thing.ThingId == id ) );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_OBJECT_SOLD" ) );
			Assert.AreEqual( shipped.Objects.Count, sold.Objects.Count );
		}
		finally
		{
			rides.Delete();
			again?.Delete();
			Entity.ApplyDeletions();
			TestRun.DeleteEvery<ParkPeople>();
		}
	}

	/// <summary>
	/// A ride the file holds with a car under its handle is written again as it runs: kept, its record follows the
	/// running ride and the file's car, which no car here answers to, is gone; sold, it is taken out; and a ride
	/// bought into the slot it had is written there.
	/// </summary>
	[TestMethod]
	public void ATrackRideWithACarInTheFileIsWrittenAgain()
	{
		const int HotPot = 1140;
		const int Handle = unchecked((int)0xffffff00);

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var people = new ParkPeople( shipped );
		var rides = new ParkRides( "jungle", shipped, catalogue, FileSystem );
		ParkRides? again = null;

		int Buy( ParkState state, ParkRides bound )
		{
			Assert.IsTrue( catalogue.TryGet( HotPot, out var item ) );

			var id = state.NextThingId();
			var placed = ParkBuilding.Constructed( item, id, 41, 23, 0, MapStep.CellId( 41 + item.EntryDeltaX, 23 + item.EntryDeltaY ),
				MapStep.CellId( 41 + item.ExitDeltaX, 23 + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ),
				ParkBuilding.TakeTrackRide( state, item ) );

			Assert.AreEqual( Handle, placed.TrackRide );
			state.AddObject( placed );
			ParkBuilding.BindOperation( state, bound, placed, item );
			ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, 41, 23, 0 ), 41, 23 );
			state.EnterCell( 41, 23, id );

			return id;
		}

		ParkWorld Written( string name ) => Read( File.ReadAllBytes( Path.Combine( Jungle, name + ".TPWS" ) ) );

		try
		{
			var id = Buy( people.State, rides );

			Assert.IsNotNull( Level.WritePark( shipped, people.State, "jungle", "Shut", people, null, rides, catalogue ) );

			// The file again with one car under the ride's handle, between its record and its close.
			var body = Inflate( File.ReadAllBytes( Path.Combine( Jungle, "Shut.TPWS" ) ) );
			var close = body.AsSpan().IndexOf( "KART"u8 ) - 16;
			var root = body.AsSpan().IndexOf( "SYSR"u8 ) + 4;
			byte[] car = [.. new[] { 5, 208, 208, Handle }.SelectMany( BitConverter.GetBytes ), .. new byte[0xac + 20]];
			byte[] withCar = [.. body[..close], .. car, .. body[close..]];

			BitConverter.TryWriteBytes( withCar.AsSpan( root + 8, 4 ), BitConverter.ToInt32( body, root + 8 ) + car.Length );

			using var shutStream = new MemoryStream( File.ReadAllBytes( Path.Combine( Jungle, "Shut.TPWS" ) ) );
			var shutReader = new SaveReader( shutStream );

			shutReader.ReadFile();

			var file = new ParkWorld( withCar, shutReader.Preamble );

			Assert.IsNull( file.Problem );
			Assert.AreEqual( 1, file.TrackRides.CarsOf( Handle ) );

			rides.Delete();
			Entity.ApplyDeletions();
			TestRun.DeleteEvery<ParkPeople>();

			var loadedPeople = new ParkPeople( file );
			var loaded = loadedPeople.State;

			again = new ParkRides( "jungle", file, catalogue, FileSystem );
			loaded.TrackRides.Cars.SetDuration( Handle, 900 );

			// The file's car answers to no car here, and the running ride counts none.
			loaded.TrackRides.Cars.RideOf( Handle )!.Cars = 0;
			Unimplemented.Forget();

			Assert.IsNotNull( Level.WritePark( file, loaded, "jungle", "Kept", loadedPeople, null, again, catalogue ) );
			Assert.AreEqual( (900, 0), (Written( "Kept" ).TrackRides.Rides.Single().Duration, Written( "Kept" ).TrackRides.CarsOf( Handle )) );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_TRACK_RIDE_AS_THE_FILE" ) );

			StringAssert.StartsWith( ParkBuilding.Sell( loaded, file, catalogue, null, again, id, loadedPeople ), "sell: 'The Hot Pot'" );
			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( file, loaded, "jungle", "Sold", loadedPeople, null, again, catalogue ) );

			var sold = Written( "Sold" );

			Assert.AreEqual( (0, 0), (sold.TrackRides.Rides.Count, sold.TrackRides.CarsOf( Handle )) );
			Assert.IsFalse( sold.Objects.Any( thing => thing.ThingId == id ), "its three records go with it" );
			Assert.AreEqual( 0, Counted( "SAVE_PARK_OBJECT_SOLD" ) );

			var next = Buy( loaded, again );

			Unimplemented.Forget();
			Assert.IsNotNull( Level.WritePark( file, loaded, "jungle", "Next", loadedPeople, null, again, catalogue ) );

			var after = Written( "Next" );

			Assert.IsNull( after.Problem );
			Assert.AreEqual( (1, 0), (after.TrackRides.Rides.Count, after.TrackRides.CarsOf( Handle )) );
			Assert.IsTrue( after.Objects.Any( thing => thing.ThingId == next ) );
			Assert.AreEqual( (0, 0), (Counted( "SAVE_PARK_OBJECT_BOUGHT" ), Counted( "SAVE_PARK_OBJECT_SOLD" )) );
		}
		finally
		{
			rides.Delete();
			again?.Delete();
			Entity.ApplyDeletions();
			TestRun.DeleteEvery<ParkPeople>();
		}
	}

	/// <summary>The level hands the writer the park's pool of candidates and its arrival timer.</summary>
	[TestMethod]
	public void TheLevelWritesThePoolAndTheArrivalTimer()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var people = new ParkPeople( shipped );
		var pool = new ParkStaffPool( new ParkBalance( "jungle", easyMode: true ), gameTick: shipped.GameTick, saved: shipped );

		try
		{
			for ( var tick = shipped.GameTick + 1; tick <= 1083; ++tick )
				pool.Sweep( tick, _ => 1 );

			people.HoldTheLoad( 3 );

			Assert.IsNotNull( Level.WritePark( shipped, people.State, "jungle", "Pooled", people, pool ) );

			var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Pooled.TPWS" ) ) );

			Assert.AreEqual( 1083, written.StaffPoolTimeSig );
			Assert.AreEqual( pool.Candidates.Count, written.StaffPool.Count( record => record.Valid ) );
			Assert.AreEqual( shipped.Arrival with { PeopleOnBus = 3, Offloading = true }, written.Arrival );

			Assert.IsNotNull( Level.WritePark( shipped, people.State, "jungle", "Carried" ) );

			var carried = Read( File.ReadAllBytes( Path.Combine( Jungle, "Carried.TPWS" ) ) );

			Assert.AreEqual( 722, carried.StaffPoolTimeSig, "with no pool handed over the file's own goes out" );
			Assert.AreEqual( shipped.Arrival, carried.Arrival );
		}
		finally
		{
			TestRun.DeleteEvery<ParkPeople>();
			ParkStaffPool.ForgetCurrent();
		}
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

	/// <summary>
	/// The scene's height under a point (<c>docs/exe/park.md</c>, "The scene's height under a point"): on a cell of a
	/// bought Hot Pot's footprint, the landscape's height plus its model's 29.8; on grass, a path, or a cell of the
	/// Belly Bounce, whose model says neither way and is counted once, the height the caller last had.
	/// </summary>
	[TestMethod]
	public void ThePointsCellSaysWhoseHeightItTakes()
	{
		const int HotPot = 1140;

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var people = new ParkPeople( shipped );
		var state = people.State;

		try
		{
			Assert.IsTrue( catalogue.TryGet( HotPot, out var item ) );

			var id = state.NextThingId();

			state.AddObject( ParkBuilding.Constructed( item, id, 41, 23, 0, MapStep.CellId( 41 + item.EntryDeltaX, 23 + item.EntryDeltaY ),
				MapStep.CellId( 41 + item.ExitDeltaX, 23 + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ),
				ParkBuilding.TakeTrackRide( state, item ) ) );
			ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, 41, 23, 0 ), 41, 23 );
			state.EnterCell( 41, 23, id );

			HeightfieldFile field;

			using ( var stream = FileSystem.OpenRead( "levels/jungle/terrain/base.MD2" )! )
				field = new HeightfieldFile( stream );

			var scene = new ParkSceneHeight( catalogue );
			var (centreX, centreZ) = ParkBumperCars.ArenaCentre( item, 41, 23, 0 );
			var (x, y) = (centreX * 10f / ParkBumperCars.CellUnits, centreZ * 10f / ParkBumperCars.CellUnits);

			Unimplemented.Forget();

			Assert.AreEqual( field.ScapeHeight( x, y ) + 29.8f, scene.Under( x, y, state, field, -7f ), 0.0001f, "in the pot" );
			Assert.AreEqual( field.ScapeHeight( x + 9f, y - 6f ) + 29.8f, scene.Under( x + 9f, y - 6f, state, field, -7f ), 0.0001f, "and a cell over" );
			Assert.AreEqual( 29.8f, scene.Under( x, y, state, field, -7f ), 0.01f, "the ground there is flat, at nought" );

			Assert.AreEqual( -7f, scene.Under( 705f, 505f, state, field, -7f ), "on grass" );
			Assert.AreEqual( -7f, scene.Under( 435f, 215f, state, field, -7f ), "on the path" );
			Assert.AreEqual( -7f, scene.Under( x, y, state, null, -7f ), "with no landscape" );
			Assert.AreEqual( 0, Counted( "SCENE_HEIGHT_OWN_MESH" ) + Counted( "SCENE_HEIGHT_SURFACE_MESHES" ) );

			// A cell on a slope given to the pot takes the slope's height; a queue cell of the pot's is nobody's
			// footprint; and a point past the landscape's edge asks the edge's cell, where the landscape answers nought.
			var pot = (ushort)MapStep.CellId( 41, 23 );
			var (hillX, hillY) = Enumerable.Range( 0, field.CellsX * field.CellsY ).Select( i => (i % field.CellsX, i / field.CellsX) )
				.First( at => MathF.Abs( field.ScapeHeight( (at.Item1 + 0.25f) * 10f, (at.Item2 + 0.5f) * 10f ) ) > 5f
					&& state.Record( at.Item1, at.Item2 ).Type == 0 );

			state.SetRecord( hillX, hillY, state.Record( hillX, hillY ) with { Type = CellEdge.Footprint, ParentId = pot } );
			Assert.AreEqual( field.ScapeHeight( (hillX + 0.25f) * 10f, (hillY + 0.5f) * 10f ) + 29.8f,
				scene.Under( (hillX + 0.25f) * 10f, (hillY + 0.5f) * 10f, state, field, -7f ), 0.0001f, "on a slope" );

			state.SetRecord( hillX, hillY, state.Record( hillX, hillY ) with { Type = ParkRideChoice.QueueCellType } );
			Assert.AreEqual( -7f, scene.Under( (hillX + 0.25f) * 10f, (hillY + 0.5f) * 10f, state, field, -7f ), "a queue cell" );

			var edge = field.CellsX - 1;

			state.SetRecord( edge, 40, state.Record( edge, 40 ) with { Type = CellEdge.RideEnd, ParentId = pot } );
			Assert.AreEqual( 29.8f, scene.Under( (field.CellsX * 10f) + 5f, 405f, state, field, -7f ), 0.0001f, "past the edge" );

			var bounce = state.Objects.Single( thing => thing.ThingId == 13 );

			Assert.AreEqual( CellEdge.Footprint, state.Record( bounce.CellX, bounce.CellY ).Type );
			Assert.AreEqual( -7f, scene.Under( (bounce.CellX + 0.5f) * 10f, (bounce.CellY + 0.5f) * 10f, state, field, -7f ), "on the Belly Bounce" );
			Assert.AreEqual( -7f, scene.Under( (bounce.CellX + 0.5f) * 10f, (bounce.CellY + 0.5f) * 10f, state, field, -7f ) );
			Assert.AreEqual( 1, Counted( "SCENE_HEIGHT_OWN_MESH" ), "counted once an item" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
