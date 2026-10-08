using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, first stage: the shipped park written back with a running park's clock, door, visitor
/// count, cash and camera, and read again by the readers. These read real game files and are skipped where there is
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

	private static int Find( byte[] data, string tag )
	{
		var bytes = Encoding.ASCII.GetBytes( tag );
		return Enumerable.Range( 0, data.Length - bytes.Length ).Last( at => data.AsSpan( at, bytes.Length ).SequenceEqual( bytes ) );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
