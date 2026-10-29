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
		Balance: 54321, Camera: new ParkCameraModule.View( Zoom: 95f, YRotation: 1.5f, PointX: 300f, PointZ: 250f ),
		RandomSeed: 0x9e3779b9u );

	private byte[] raw = null!;
	private ParkWorld shipped = null!;
	private string? root;
	private BaseFileSystem? oldSaves;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		FileSystem = GameData.Required();
		raw = FileSystem.ReadAllBytes( ShippedPark );
		shipped = Read( raw );

		oldSaves = SaveFileSystemOrNull;
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
		SaveFileSystemOrNull = oldSaves;
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

	/// <summary>
	/// The shipped park's advisor, thing 3: he has said message 0xb9 on tick 257, been cut off once saying 0xbd,
	/// and never said the mechanic's line; he keeps 351 records and no more.
	/// </summary>
	[TestMethod]
	public void TheShippedParksAdvisorRecordsAreRead()
	{
		Assert.IsTrue( shipped.AdvisorAt > 0 );
		Assert.AreEqual( new ParkWorld.AdvisorHistory( 257, 0, true, 0 ), shipped.AdvisorHistoryOf( 0xb9 ) );
		Assert.AreEqual( new ParkWorld.AdvisorHistory( 55, 0, false, 1 ), shipped.AdvisorHistoryOf( 0xbd ) );
		Assert.AreEqual( new ParkWorld.AdvisorHistory( 16, 0, true, 0 ), shipped.AdvisorHistoryOf( 0x143 ) );
		Assert.AreEqual( ParkWorld.AdvisorHistory.Fresh, shipped.AdvisorHistoryOf( 0x46 ) );
		Assert.AreEqual( ParkWorld.AdvisorHistory.Fresh, shipped.AdvisorHistoryOf( 350 ) );
		Assert.IsNull( shipped.AdvisorHistoryOf( 351 ) );
		Assert.IsNull( shipped.AdvisorHistoryOf( -1 ) );
		Assert.AreEqual( 345, Enumerable.Range( 0, ParkWorld.AdvisorMessages ).Count( id => shipped.AdvisorHistoryOf( id ) == ParkWorld.AdvisorHistory.Fresh ) );
	}

	/// <summary>
	/// The shipped park's advisor holds one waiting message, the park's opening at a score of 20 in slot 2;
	/// slots 0 and 1 keep a said message's bytes with the slot let go, and he has eight slots and no more.
	/// </summary>
	[TestMethod]
	public void TheShippedParksAdvisorSlotsAreRead()
	{
		Assert.AreEqual( new ParkWorld.AdvisorSlot( 0, 0, 128, 20, true, -1, false, false ), shipped.AdvisorSlotOf( 2 ) );
		Assert.AreEqual( new ParkWorld.AdvisorSlot( 0, 0, 188, 1000, false, -1, false, false ), shipped.AdvisorSlotOf( 0 ) );
		Assert.AreEqual( new ParkWorld.AdvisorSlot( 0, 0, 346, 1000, false, -1, false, false ), shipped.AdvisorSlotOf( 1 ) );
		Assert.AreEqual( 1, Enumerable.Range( 0, ParkWorld.AdvisorSlots ).Count( index => shipped.AdvisorSlotOf( index )!.Value.Valid ) );
		Assert.AreEqual( 0xcdcd, shipped.AdvisorSlotOf( 7 )!.Value.Thing );
		Assert.IsNull( shipped.AdvisorSlotOf( 8 ) );
		Assert.IsNull( shipped.AdvisorSlotOf( -1 ) );
	}

	/// <summary>
	/// The shipped park's advisor began his last line at 114,820,087 on the second stopwatch, which the file's
	/// clock module holds at 114,876,286, waits 10,832 ms from it and was saying message 0x11a: a wait long over.
	/// The second stopwatch moves with the clock.
	/// </summary>
	[TestMethod]
	public void TheShippedParksAdvisorActionIsReadAgainstItsSecondStopwatch()
	{
		Assert.AreEqual( new ParkWorld.AdvisorAction( 114820087, 10832, 0x11a ), shipped.AdvisorActionOf() );
		Assert.AreEqual( 114876286u, shipped.Clock.Second );
		Assert.AreEqual( 114876286u, shipped.Clock.SecondAt( shipped.Clock.Reading!.Value ) );
		Assert.AreEqual( 114876286u + 5000, shipped.Clock.SecondAt( shipped.Clock.Reading!.Value + 5000 ) );
		Assert.AreEqual( 114876286u - 7, shipped.Clock.SecondAt( shipped.Clock.Reading!.Value - 7 ) );
	}

	/// <summary>
	/// The lab is written as it runs and read back: Dino Karts researched on the 95th hand-out, at tier 2, its
	/// project at its cost, the rides on their next project in group 2, and an appended project counted in
	/// <c>mNumProjects</c>; the work load and the dearest project are written as the lab holds them, the file's here,
	/// and every byte past the lab's used fields stays the file's. A work load the research screen set is written.
	/// With no lab given the file's own stands.
	/// </summary>
	[TestMethod]
	public void TheLabIsWrittenAsItRuns()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem, instantAction: true );
		var lab = new ParkResearch( shipped.ObjectControlRecords, catalogue, shipped.ResearchProjects, shipped.ResearchKinds,
			shipped.ResearchWorkLoad, ParkResearch.GroupOpensAt( new ParkBalance( "jungle", easyMode: true ) ),
			shipped.ResearchMostExpensiveProject );

		while ( lab.HandOuts < 95 )
			lab.SpendPoints( 12, 760 + lab.HandOuts * 20 );

		var written = lab.Written with { Projects = [.. lab.Projects, new( 1150, ParkResearch.Upgrade, 0, 1, 250f, 12.5f )] };
		var read = Read( ParkFileWriter.Write( shipped, Played with { Lab = written } ) );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( 25, read.ResearchProjects.Count );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1150, 0, 1, 0, 600f, 600f ), read.ResearchProjects[1] );
		Assert.AreEqual( new ParkWorld.ResearchProject( 1150, 4, 0, 1, 250f, 12.5f ), read.ResearchProjects[24] );
		Assert.AreEqual( 150f, read.ResearchProjects[8].Done, "Stone Head: 7.5 and 95 of 1.5" );
		Assert.AreEqual( new ParkWorld.ResearchKind( 100, 49, 2, 2 ), read.ResearchKinds[0] );
		Assert.AreEqual( shipped.ResearchKinds[1], read.ResearchKinds[1] );
		Assert.AreEqual( 85, read.ResearchWorkLoad );
		Assert.IsTrue( read.ObjectControlRecords.Single( control => control.ItemId == 1150 ) is { Researched: true, TierResearched: 2 } );
		Assert.IsFalse( shipped.ObjectControlRecords.Single( control => control.ItemId == 1150 ).Researched );
		Assert.IsTrue( new ParkResearch( read.ObjectControlRecords, catalogue ).IsResearched( 1150 ), "the buy list lists it after a load" );

		var mine = Inflate( ParkFileWriter.Write( shipped, Played with { Lab = written } ) );
		var theirs = Inflate( ParkFileWriter.Write( shipped, Played ) );
		var lab0 = read.ResearchLabAt;
		Assert.AreEqual( theirs.Length, mine.Length );
		CollectionAssert.AreEqual( theirs[(lab0 + 4181)..(lab0 + 4190)], mine[(lab0 + 4181)..(lab0 + 4190)], "the work load, the dearest, mLabActive" );
		CollectionAssert.AreEqual( theirs[(lab0 + 16 + 25 * 16)..(lab0 + 4112)], mine[(lab0 + 16 + 25 * 16)..(lab0 + 4112)], "the unused projects" );
		Assert.AreEqual( shipped.ResearchProjects.Count, Read( ParkFileWriter.Write( shipped, Played ) ).ResearchProjects.Count );

		lab.SetWorkLoad( 83 );
		var set = Read( ParkFileWriter.Write( shipped, Played with { Lab = lab.Written } ) );
		Assert.AreEqual( (83, shipped.ResearchMostExpensiveProject), (set.ResearchWorkLoad, set.ResearchMostExpensiveProject) );
		Assert.AreEqual( 48, set.ResearchKinds[0].Output, "the rides' output worked again: their share, 58 of a hundred, times 83, over a hundred" );
		CollectionAssert.AreEqual( shipped.ResearchProjects.ToArray(), Read( ParkFileWriter.Write( shipped, Played ) ).ResearchProjects.ToArray() );
	}

	/// <summary>
	/// The advisor's last action is written over the file's and read back: the start and the wait at 196 and 200
	/// of his thing and the message playing in its last dword, 5842, and no other byte of the file moves. It is
	/// asked for with the second stopwatch's reading the file holds, the file's own where the clock is not
	/// written; with none asked for the file's stands.
	/// </summary>
	[TestMethod]
	public void TheAdvisorsActionIsWrittenOverTheFilesOwn()
	{
		var asked = new List<uint>();
		ParkWorld.AdvisorAction Action( uint second )
		{
			asked.Add( second );
			return new ParkWorld.AdvisorAction( second - 0x1234, 0x05060708, 0x01020346 );
		}

		var mine = Inflate( ParkFileWriter.Write( shipped, Played with { AdvisorAction = Action } ) );
		var theirs = Inflate( ParkFileWriter.Write( shipped, Played ) );
		var written = Read( ParkFileWriter.Write( shipped, Played with { AdvisorAction = Action } ) );

		Assert.IsNull( written.Problem );
		CollectionAssert.AreEqual( new[] { 114876286u, 114876286u }, asked );
		Assert.AreEqual( new ParkWorld.AdvisorAction( 114876286u - 0x1234, 0x05060708, 0x01020346 ), written.AdvisorActionOf() );
		Assert.AreEqual( shipped.AdvisorActionOf(), Read( ParkFileWriter.Write( shipped, Played ) ).AdvisorActionOf() );

		Assert.AreEqual( theirs.Length, mine.Length );
		var at = written.AdvisorAt;
		var started = BitConverter.GetBytes( 114876286u - 0x1234 );
		CollectionAssert.AreEqual( new byte[] { started[0], started[1], started[2], started[3], 8, 7, 6, 5 }, mine[(at + 196)..(at + 204)],
			"mLastActionStarted, mLastActionDuration" );
		CollectionAssert.AreEqual( new byte[] { 0x46, 3, 2, 1 }, mine[(at + 5842)..(at + 5846)], "mMessagePlaying" );
		var differing = Enumerable.Range( 0, mine.Length ).Where( i => mine[i] != theirs[i] ).ToList();
		Assert.IsTrue( differing.Count >= 6 );
		Assert.IsTrue( differing.All( i => (i >= at + 196 && i < at + 204) || (i >= at + 5842 && i < at + 5846) ), "the three dwords and no more" );
		Assert.AreEqual( shipped.AdvisorSlotOf( 2 ), written.AdvisorSlotOf( 2 ) );
		Assert.AreEqual( shipped.AdvisorHistoryOf( 0 ), written.AdvisorHistoryOf( 0 ) );
		Assert.AreEqual( shipped.AdvisorHistoryOf( 350 ), written.AdvisorHistoryOf( 350 ) );
	}

	/// <summary>
	/// The advisor's slots are written over the file's and read back: a taken one whole, twenty-one bytes at
	/// 24 + 21 a slot of his thing; of an empty one its valid byte alone; a null one not at all; and no other
	/// byte of the file moves.
	/// </summary>
	[TestMethod]
	public void TheAdvisorsSlotsAreWrittenOverTheFilesOwn()
	{
		var busy = new ParkWorld.AdvisorSlot( 13, 1100, 0x45, 107, true, -1, false, false );
		var odd = new ParkWorld.AdvisorSlot( 0x1234, -2, 0x46, -3, true, 2, true, false );
		var given = new ParkWorld.AdvisorSlot( 1, 2, 3, 4, true, 5, false, true );
		var slots = new ParkWorld.AdvisorSlot?[] { busy, null, default( ParkWorld.AdvisorSlot ), odd, given };
		var mine = Inflate( ParkFileWriter.Write( shipped, Played with { AdvisorSlots = slots } ) );
		var theirs = Inflate( ParkFileWriter.Write( shipped, Played ) );
		var written = Read( ParkFileWriter.Write( shipped, Played with { AdvisorSlots = slots } ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( busy, written.AdvisorSlotOf( 0 ) );
		Assert.AreEqual( shipped.AdvisorSlotOf( 1 ), written.AdvisorSlotOf( 1 ) );
		Assert.AreEqual( shipped.AdvisorSlotOf( 2 )!.Value with { Valid = false }, written.AdvisorSlotOf( 2 ) );
		Assert.AreEqual( odd, written.AdvisorSlotOf( 3 ) );
		Assert.AreEqual( given, written.AdvisorSlotOf( 4 ) );
		Assert.AreEqual( shipped.AdvisorSlotOf( 5 ), written.AdvisorSlotOf( 5 ) );
		var untouched = Read( ParkFileWriter.Write( shipped, Played with { AdvisorSlots = new ParkWorld.AdvisorSlot?[] { null, null, null } } ) );
		Assert.AreEqual( shipped.AdvisorSlotOf( 2 ), untouched.AdvisorSlotOf( 2 ), "a null slot is the file's, taken as it was" );

		Assert.AreEqual( theirs.Length, mine.Length );
		var at = written.AdvisorAt + 24;
		CollectionAssert.AreEqual( new byte[] { 13, 0, 0x4c, 4, 0, 0, 0x45, 0, 0, 0, 107, 0, 0, 0, 1, 0xff, 0xff, 0xff, 0xff, 0, 0 },
			mine[at..(at + 21)], "mThingId, mSecondary, mMessage, mScore, mValid, mResponseNo, mOverrideOnlyOnce, mScoreOverridden" );
		CollectionAssert.AreEqual( new byte[] { 0x34, 0x12, 0xfe, 0xff, 0xff, 0xff, 0x46, 0, 0, 0, 0xfd, 0xff, 0xff, 0xff, 1, 2, 0, 0, 0, 1, 0 },
			mine[(at + 63)..(at + 84)] );
		var differing = Enumerable.Range( 0, mine.Length ).Where( i => mine[i] != theirs[i] ).ToList();
		Assert.IsTrue( differing.All( i => (i >= at && i < at + 21) || i == at + 42 + 14 || (i >= at + 63 && i < at + 105) ),
			"only slots 0, 3 and 4 and slot 2's valid byte" );
		Assert.AreEqual( 1, mine[at + 84 + 20] );
		Assert.AreEqual( 0, mine[at + 84 + 19] );
		Assert.IsTrue( differing.Contains( at + 42 + 14 ) );
		Assert.AreEqual( shipped.AdvisorHistoryOf( 0xb9 ), written.AdvisorHistoryOf( 0xb9 ) );
	}

	/// <summary>
	/// The advisor's records named are written over the file's and read back; the three bytes after a said byte,
	/// every record not named and the rest of his thing stay the file's; a message he keeps no record of is
	/// written nowhere; with none handed over the file's own stand.
	/// </summary>
	[TestMethod]
	public void TheAdvisorsRecordsAreWrittenOverTheFilesOwn()
	{
		var histories = new Dictionary<int, ParkWorld.AdvisorHistory>
		{
			[0x46] = new( 880, 1, true, 0 ),
			[0x11c] = new( 911, 0, false, 2 ),
			[0xb9] = ParkWorld.AdvisorHistory.Fresh,
			[351] = new( 7, 7, true, 7 ),
			[-1] = new( 7, 7, true, 7 )
		};
		var written = Read( ParkFileWriter.Write( shipped, Played with { AdvisorHistories = histories } ) );
		var plain = Read( ParkFileWriter.Write( shipped, Played ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( new ParkWorld.AdvisorHistory( 880, 1, true, 0 ), written.AdvisorHistoryOf( 0x46 ) );
		Assert.AreEqual( new ParkWorld.AdvisorHistory( 911, 0, false, 2 ), written.AdvisorHistoryOf( 0x11c ) );
		Assert.AreEqual( ParkWorld.AdvisorHistory.Fresh, written.AdvisorHistoryOf( 0xb9 ) );
		Assert.AreEqual( new ParkWorld.AdvisorHistory( 55, 0, false, 1 ), written.AdvisorHistoryOf( 0xbd ) );
		Assert.AreEqual( shipped.AdvisorHistoryOf( 0xb9 ), plain.AdvisorHistoryOf( 0xb9 ) );
		Assert.AreEqual( ParkWorld.AdvisorHistory.Fresh, plain.AdvisorHistoryOf( 0x46 ) );

		Assert.AreEqual( plain.AdvisorAt, written.AdvisorAt );
		var mine = Inflate( ParkFileWriter.Write( shipped, Played with { AdvisorHistories = histories } ) );
		var theirs = Inflate( ParkFileWriter.Write( shipped, Played ) );
		Assert.AreEqual( theirs.Length, mine.Length );
		int At( int message ) => written.AdvisorAt + 0xcc + (16 * message);
		var named = new[] { 0x46, 0x11c, 0xb9 };
		var differing = Enumerable.Range( 0, mine.Length ).Where( i => mine[i] != theirs[i] ).ToList();
		Assert.IsTrue( differing.Count > 0 );
		Assert.IsTrue( differing.All( i => named.Any( id => i >= At( id ) && i < At( id ) + 16 && (i - At( id )) is not (9 or 10 or 11) ) ),
			"only the named records' four fields change" );
		CollectionAssert.AreEqual( new byte[] { 0x70, 0x03, 0, 0, 1, 0, 0, 0, 1, 0xcd, 0xcd, 0xcd, 0, 0, 0, 0 }, mine[At( 0x46 )..(At( 0x46 ) + 16)] );
		CollectionAssert.AreEqual( new byte[] { 0x8f, 0x03, 0, 0, 0, 0, 0, 0, 0, 0xcd, 0xcd, 0xcd, 2, 0, 0, 0 }, mine[At( 0x11c )..(At( 0x11c ) + 16)] );
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
	/// Every module is carried: the body differs from the file's only inside the nine fields written, and not at
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

		// mGameTick, mParkClosed, mNumberOfVisitorsToDate, mRandomSeed; mBalance; the zoom and rotation, the point's x, its z.
		(int At, int Size)[] written =
			[(header + 14, 4), (header + 22, 4), (header + 26, 4), (header + 34, 4), (economy, 4), (camera, 8), (camera + 12, 4),
				(camera + 20, 4)];

		var changed = Enumerable.Range( 0, body.Length ).Where( at => body[at] != before[at] ).ToArray();
		var outside = changed.Where( at => !written.Any( field => at >= field.At && at < field.At + field.Size ) ).ToArray();

		Assert.AreEqual( 0, outside.Length, $"bytes changed outside the fields written, the first at 0x{outside.FirstOrDefault():x}" );

		foreach ( var field in written )
			Assert.IsTrue( changed.Any( at => at >= field.At && at < field.At + field.Size ), $"nothing changed in the field at 0x{field.At:x}" );

	}

	/// <summary>
	/// <b>The park's generator is written as <c>mRandomSeed</c></b>, the header's dword after <c>mTrafficLights</c>
	/// (<c>world + 0x1da708</c>), and read back as the seed; with none given the file's stands.
	/// </summary>
	/// <remarks><b>Mutations:</b> the seed not written; written at <c>mGameTick</c>'s place; written with none given.</remarks>
	[TestMethod]
	public void TheParksGeneratorIsWrittenAsTheSeed()
	{
		var written = Read( ParkFileWriter.Write( shipped, Played ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( unchecked((int)0x9e3779b9u), written.RandomSeed );
		Assert.AreEqual( 1234, written.GameTick, "beside the clock, not over it" );
		Assert.AreNotEqual( shipped.RandomSeed, written.RandomSeed );
		Assert.AreEqual( shipped.RandomSeed, Read( ParkFileWriter.Write( shipped, Played with { RandomSeed = null } ) ).RandomSeed,
			"none given: the file's" );
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
	/// The level's half: the running park's clock, door, count and cash, the analyser's stamp of the newest hire, its
	/// lifetime visitors and dearest ticket, and the orbit camera's view, written into the player's folder for the theme
	/// under the name given.
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
		state.StampStaffHired();
		state.Take( 27 );

		ParkOrbitCameraMode.Zoom = 80f;
		ParkOrbitCameraMode.Yaw = 0.75f;
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 310f, 220f, 0f );

		var path = Level.WritePark( shipped, state, "jungle", "My Park" );

		Assert.AreEqual( Path.Join( "users", "1Test", "jungle", "My Park.TPWS" ), path );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "My Park.TPWS" ) ) );

		Assert.AreEqual( 900, written.GameTick );
		Assert.AreEqual( 1, written.ParkClosed );
		Assert.AreEqual( 2, written.NumberOfVisitorsToDate );
		Assert.AreEqual( 88098 + 27, written.Economy!.Value.Balance );
		Assert.AreEqual( (9, 27), (written.LifetimeVisitors, written.MostPaidForTicket), "the shipped 8 and 25, one fee of 27 on" );
		Assert.AreNotEqual( shipped.LastStaffHired, written.LastStaffHired );
		Assert.AreEqual( ParkWorld.StaffState.FileTimeOf( new DateTime( 2000, 1, 1 ).AddSeconds( 900 * 3750 ) ), written.LastStaffHired, "mLastStaffHired, stamped on tick 900" );
		Assert.AreEqual( new ParkCameraModule.View( 80f, -0.75f, 310f, 220f ), written.Camera.Saved, "the rotation turns the other way" );

		Assert.AreEqual( "My Park", SaveFolder.SavedParks( 0, "Test", "jungle" ).Single().Name, "the Load Park screen's list" );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_WITH_NO_FILE" ) );
	}

	/// <summary>
	/// The level hands the writer the analyser's rings of the recently researched as the running park holds them: a push
	/// into the shops' ring is written, the other three stay the shipped file's, never pushed.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheRingsOfTheRecentlyResearched()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var state = new ParkState( shipped );
		state.RecentlyResearched.Push( 1, 1212 );

		Level.WritePark( shipped, state, "jungle", "Rings" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Rings.TPWS" ) ) ).RecentlyResearched!;

		Assert.AreEqual( (0, 3, false), (written[1].Current, written[1].Entries, written[1].Wrapped) );
		CollectionAssert.AreEqual( new ushort[] { 1212, 0xCDCD, 0xCDCD }, written[1].Data );
		Assert.IsTrue( new[] { 0, 2, 3 }.All( tab => written[tab].Current == -1 && written[tab].Data.All( slot => slot == 0xCDCD ) ) );
	}

	/// <summary>
	/// The level hands the writer the analyser's three counts of misbehaving kids as the running park holds them: a
	/// park read from a body holding 5, 2 and 9 holds 5, 2 and nought (the third is never read), written over the shipped
	/// file's nought, nought and nought.
	/// </summary>
	[TestMethod]
	public void TheLevelWritesTheMisbehavingKids()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var body = new SaveReader( new MemoryStream( raw ) ).ReadFile();
		shipped.PutMisbehavingKids( body, new( 5, 2, 9 ) );
		var state = new ParkState( new ParkWorld( body ) );

		Level.WritePark( shipped, state, "jungle", "Kids" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Kids.TPWS" ) ) );

		Assert.AreEqual( new ParkWorld.MisbehavingKidCounts( 0, 0, 0 ), shipped.MisbehavingKids );
		Assert.AreEqual( new ParkWorld.MisbehavingKidCounts( 5, 2, 0 ), written.MisbehavingKids );
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

			// Two seconds are 64 ticks and 16 ms owed toward the next: the file's moment is the frame's, not the last tick's.
			Assert.AreEqual( shipped.Clock.Reading + (64u * 31u) + 16u, written.Clock.Reading );
			Assert.AreEqual( 31, written.Objects.Single( placed => placed.ThingId == 13 ).FirstInQueue );
			Assert.IsNull( written.ThingStates( id => catalogue.ChannelsFor( id ) ).Problem, "the Jungle Spray's three channels are walked as three" );
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
			// A request to open the park, the door already open, stamps the analyser's mParkLastOpened.
			state.SetParkClosed( false );

			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Bought", people, null, rides, catalogue ) );

			var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Bought.TPWS" ) ) );
			var made = written.Objects.Single( thing => thing.ThingId == ape );
			var record = written.RecordOf( ape )!;

			Assert.IsNull( written.Problem );
			Assert.AreEqual( ParkWorld.StaffState.FileTimeOf( new DateTime( 2000, 1, 1 ).AddSeconds( 900 * 3750 ) ), written.LastObjectBuilt, "mLastObjectBuilt, stamped as the things were bought on tick 900" );
			Assert.AreEqual( shipped.LastStaffHired, written.LastStaffHired );
			Assert.AreEqual( ParkWorld.StaffState.FileTimeOf( new DateTime( 2000, 1, 1 ).AddSeconds( 900 * 3750 ) ), written.ParkLastOpened, "mParkLastOpened, stamped as the park was opened on tick 900" );
			Assert.AreEqual( (1101, 91, rides.ScriptFor( ape )), (made.CatalogueId, made.MeshInstance, made.RideScript) );
			Assert.AreEqual( "Crazy", Encoding.Unicode.GetString( [.. Enumerable.Range( 0, 5 ).SelectMany( i => record.AsSpan( 60 + (i * 4), 2 ).ToArray() )] ) );
			Assert.AreEqual( "Ape", Encoding.Unicode.GetString( [.. Enumerable.Range( 0, 3 ).SelectMany( i => record.AsSpan( 62 + (i * 4), 2 ).ToArray() )] ) );
			Assert.AreEqual( bounce, written.FirstObject, "the newest heads the object list" );
			Assert.IsFalse( written.Objects.Any( thing => thing.ThingId is 16 || thing.ThingId == karts || thing.ThingId == coaster ), "the shop sold, and the two track rides left out" );
			Assert.AreEqual( 16, written.Objects.Count );
			Assert.AreEqual( (1417, 92, rides.ScriptFor( speaker )), (written.Objects.Single( thing => thing.ThingId == speaker ).CatalogueId, written.Objects.Single( thing => thing.ThingId == speaker ).MeshInstance, written.Objects.Single( thing => thing.ThingId == speaker ).RideScript), "a thing with emitter files is written as any other" );
			Assert.AreEqual( ape, written.People.Single( person => person.ThingId == walker.Key ).Guest!.Value.MajorDest, "a handle to a thing bought" );

			// The script's folder as the engine keeps one, its folder spelt as the executable's strings, and the model's footprint
			// across then down.
			var body = Inflate( File.ReadAllBytes( Path.Combine( Jungle, "Bought.TPWS" ) ) );
			var folder = "data\\levels\\jungle\\Rides\\monkey\\\0";
			var folderAt = body.AsSpan().IndexOf( Encoding.ASCII.GetBytes( folder ) );

			Assert.IsTrue( folderAt > 4 && BitConverter.ToInt32( body, folderAt - 4 ) == folder.Length, "its length before it" );
			Assert.IsTrue( body.AsSpan().IndexOf( (byte[])[1, .. BitConverter.GetBytes( 1100 ), .. BitConverter.GetBytes( 30 ), .. BitConverter.GetBytes( 60 ), .. BitConverter.GetBytes( 3 ), .. BitConverter.GetBytes( 4 )] ) > 0, "the Belly Bounce's model, three by four" );

			var script = written.ScriptStates.For( rides.ScriptFor( ape ) )!.Value;

			Assert.AreEqual( (ape, 91), ((int)script.Thing, script.ModelHandle) );
			Assert.IsNull( written.ScriptStates.For( 6 ), "the shop's script" );

			var models = written.ThingStates( id => catalogue.ChannelsFor( id ) );

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

			ParkThingStates Models( ParkWorld world ) => world.ThingStates( item => catalogue.ChannelsFor( item ) );

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

			// The file in a go, loaded: the boat keeps its model's handle, and its clip goes on from the record's stamps.
			var goPeople = new ParkPeople( go );
			var goRides = new ParkRides( "jungle", go, catalogue, FileSystem );

			try
			{
				var afloat = goPeople.State.TrackRides.Cars.CarsOf( Handle ).Single();
				var boatItem = HotPot + 1 + afloat.Mesh;

				RideAnimations Clips() => RideAnimations.Load( item.Directory, "b_car", FileSystem );

				Assert.AreEqual( going.Slot + 1, afloat.SavedModel );
				Unimplemented.Forget();

				var players = Clips();

				Assert.AreEqual( 5, ParkBumperBoats.Resume( afloat, players, boatItem, goRides ) );
				Assert.AreEqual( (5, 0, false, AnimTimeControl.LoopFlag), (players.Channel( 0 )!.AnimID, players.Channel( 0 )!.SubAnim, players.Channel( 0 )!.IsIdle, players.Channel( 0 )!.Flags & AnimTimeControl.LoopFlag) );
				CollectionAssert.AreEqual( going.NodeWords, players.Nodes!.Words( players.NodeFrames() ), "the file's node words, its clip bound" );
				Assert.AreEqual( 0, Counted( "SAVED_TRACK_CAR_MODEL_NOT_ITS_OWN" ) );

				// A record whose clip began 102 ms before the save: the clip stands 102 ms in at the load.
				var earlier = going with { Channels = [going.Channels[0] with { StartTime = go.Clock.Reading!.Value - 102, Time = go.Clock.Reading!.Value, NoPauseTime = go.Clock.Reading!.Value }] };
				var resumed = Clips();

				Assert.AreEqual( 5, goRides.ResumeCar( resumed, earlier, boatItem )!.Value.Role );
				Assert.AreEqual( (goRides.LoadedAt - 102, goRides.LoadedAt), (resumed.Channel( 0 )!.StartAnimTime, resumed.Channel( 0 )!.AnimTime) );

				// A record of another item, an empty slot and no record are not the boat's: nothing starts, and the first two are counted.
				var other = Clips();

				Assert.IsNull( goRides.ResumeCar( other, earlier, HotPot + 1 ) );
				Assert.IsNull( ParkBumperBoats.Resume( afloat, other, HotPot + 1, goRides ), "its record is the boat's item's, not the wake's" );

				afloat.SavedModel = Models( go ).Header.Cursor + 5;
				Assert.IsNull( ParkBumperBoats.Resume( afloat, other, boatItem, goRides ), "a slot the file leaves empty" );
				Assert.AreEqual( 2, Counted( "SAVED_TRACK_CAR_MODEL_NOT_ITS_OWN" ) );

				afloat.SavedModel = 0;
				Assert.IsNull( ParkBumperBoats.Resume( afloat, other, boatItem, goRides ), "a car launched here names none" );
				Assert.IsNull( ParkBumperBoats.Resume( goPeople.State.TrackRides.Cars.CarsOf( Handle ).Single(), other, boatItem, null ) );
				Assert.AreEqual( 2, Counted( "SAVED_TRACK_CAR_MODEL_NOT_ITS_OWN" ) );
				Assert.IsTrue( other.Channel( 0 )!.IsIdle, "and nothing was started" );

				// A record at rest is found, and starts nothing.
				var still = Clips();

				Assert.AreEqual( 12, goRides.ResumeCar( still, record, boatItem )!.Value.Role );
				Assert.IsTrue( still.Channel( 0 )!.IsIdle );
			}
			finally
			{
				goRides.Delete();
				Entity.ApplyDeletions();
				TestRun.DeleteEvery<ParkPeople>();
			}

			var boatPeople = new ParkPeople( boat );
			var boatRides = new ParkRides( "jungle", boat, catalogue, FileSystem );

			try
			{
				Assert.AreEqual( 1, boatPeople.State.TrackRides.Cars.CarsOf( Handle ).Count() );

				// Its boat's record is at rest: found, nothing started, no role answered and nothing counted.
				var resting = RideAnimations.Load( item.Directory, "b_car", FileSystem );
				var moored = boatPeople.State.TrackRides.Cars.CarsOf( Handle ).Single();

				Unimplemented.Forget();
				Assert.AreEqual( own + 1, moored.SavedModel );
				Assert.IsNull( ParkBumperBoats.Resume( moored, resting, HotPot + 1 + moored.Mesh, boatRides ) );
				Assert.IsTrue( resting.Channel( 0 )!.IsIdle );
				Assert.AreEqual( 0, Counted( "SAVED_TRACK_CAR_MODEL_NOT_ITS_OWN" ) );
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
	public void AParkMadeFreshIsWrittenAsTheFileAtTickNought()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );

		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ),
			new ParkItemCatalogue( "jungle", instantAction: false ) );

		var path = Level.WritePark( fresh, new ParkState( fresh ), "jungle", "Fresh" );

		Assert.AreEqual( "Fresh.TPWS", Path.GetFileName( path ) );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_WITH_NO_FILE" ) );

		var written = Read( SaveFileSystem.ReadAllBytes( path! ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( 0, written.GameTick );
		Assert.AreEqual( 1, Directory.GetFiles( Jungle ).Length );
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
		TrackType = 25, TrackFlags = 3, TrackParentId = 4321, TrackNeighbours = 0x11, TrackOverlapCounter = 2
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
		Assert.AreEqual( (0, (ushort)0, (ushort)0, (byte)0, (short)0),
			(was.TrackType, was.TrackFlags, was.TrackParentId, was.TrackNeighbours, was.TrackOverlapCounter) );

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

		// mDirection and mFlags; mNeighbours to mTileData; mType; then the track record's flags, neighbours, overlap counter,
		// parent, type.
		(int At, int Size)[] fields =
			[(map, 3), (map + 7, 17), (map + 24, 4), (track + 1, 2), (track + 7, 1), (track + 8, 2), (track + 10, 2), (track + 24, 4)];

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

			using ( var stream = FileSystem.OpenReadOrNull( "levels/jungle/terrain/base.MD2" )! )
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

	/// <summary>
	/// <b>A written park holds the economy thing's seven fields and its eight loans</b>, each from its own place
	/// (FileFormats <c>saves.md</c>, "The economy thing"), and every byte the writer changes for them lies inside the
	/// record's 284 bytes past its map base. Handed none, the writer writes the balance alone and leaves the rest the
	/// file's: the shipped fee of 25 and profit of -12013.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the fee written in the balance's place; the last balance and the red stamp swapped; the profit
	/// not written; the loans a slot out of place; the last loan slot not written; bought and repaid swapped; the
	/// writer never calling it.
	/// </remarks>
	[TestMethod]
	public void AWrittenParkHoldsTheBanksSevenFieldsAndEightLoans()
	{
		var loans = Enumerable.Range( 0, ParkWorld.EconomyState.LoanSlots )
			.Select( i => new ParkWorld.LoanState( 100 * i + 1, 100 * i + 2, 100 * i + 3, 100 * i + 4, 100 * i + 5,
				100 * i + 6, 100 * i + 7, 100 * i + 8 ) ).ToArray();
		var economy = new ParkWorld.EconomyState( AdmissionFee: 31, Balance: -2677, BatchBalance: 77, WithdrawalsEnabled: 0,
			LastBalance: -555, TurnEnteredRed: 16900, ProfitThisYear: 4321, Loans: loans );

		var alone = ParkFileWriter.Body( shipped, Played );
		var body = ParkFileWriter.Body( shipped, Played with { Economy = economy } );
		var written = new ParkWorld( body ).Economy!.Value;

		Assert.AreEqual( (31, -2677, 77, 0, -555, 16900, 4321),
			(written.AdmissionFee, written.Balance, written.BatchBalance, written.WithdrawalsEnabled, written.LastBalance,
				written.TurnEnteredRed, written.ProfitThisYear) );
		CollectionAssert.AreEqual( loans, written.Loans.ToArray() );

		var changed = Enumerable.Range( 0, body.Length ).Where( i => body[i] != alone[i] ).ToArray();

		Assert.IsTrue( changed.Length > 0 );
		Assert.IsTrue( changed[^1] - changed[0] < 284, $"changed bytes {changed[0]}..{changed[^1]}" );

		var left = new ParkWorld( alone ).Economy!.Value;

		Assert.AreEqual( (25, 54321, -12013, 87787), (left.AdmissionFee, left.Balance, left.ProfitThisYear, left.LastBalance) );
		CollectionAssert.AreEqual( shipped.Economy!.Value.Loans.ToArray(), left.Loans.ToArray() );
	}

	/// <summary>
	/// <b>The running bank hands the writer every field it loaded</b>, each in its own place: a park read from a file
	/// whose seven fields and eight loans all differ gives them back unchanged.
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="ParkState.Economy"/>'s last balance the balance, its withdrawals always on, its
	/// batch nought, its loans not the running ones.</remarks>
	[TestMethod]
	public void TheRunningBankHandsBackEveryFieldItLoaded()
	{
		var loans = Enumerable.Range( 0, ParkWorld.EconomyState.LoanSlots )
			.Select( i => new ParkWorld.LoanState( 100 * i + 1, 100 * i + 2, 100 * i + 3, 100 * i + 4, 100 * i + 5,
				100 * i + 6, 100 * i + 7, 100 * i + 8 ) ).ToArray();
		var economy = new ParkWorld.EconomyState( AdmissionFee: 31, Balance: -2677, BatchBalance: 77, WithdrawalsEnabled: 0,
			LastBalance: -555, TurnEnteredRed: 16900, ProfitThisYear: 4321, Loans: loans );
		var fixture = Inflate( raw );
		shipped.PutEconomy( fixture, economy );

		var running = new ParkState( new ParkWorld( fixture ) ).Economy;

		Assert.AreEqual( economy with { Loans = [] }, running with { Loans = [] } );
		CollectionAssert.AreEqual( loans, running.Loans.ToArray() );
		Assert.AreEqual( "fee 31 balance -2677 batch 77 withdrawals 0 last -555 red 16900 profit 4321 loans [0: 7/4 of 5, "
			+ "1: 107/104 of 105, 2: 207/204 of 205, 3: 307/304 of 305, 4: 407/404 of 405, 5: 507/504 of 505, 6: 607/604 of 605, "
			+ "7: 707/704 of 705]", new ParkState( new ParkWorld( fixture ) ).DescribeEconomy() );
	}

	/// <summary>
	/// <b>The level writes the bank as it runs</b>: a fee moved on the entry-price screen, and a month's change that
	/// pays a bought loan's instalment (loan 4, 750 a month over 24) into the red, are in the file it writes, and a
	/// park loaded from it runs on from them.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Level.WritePark</c> handing the writer no economy; a loan's months repaid not counted;
	/// the getter's last balance the balance.</remarks>
	[TestMethod]
	public void TheLevelWritesTheBanksFeeAndARepaidLoan()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		var loans = shipped.Economy!.Value.Loans.ToArray();
		loans[4] = loans[4] with { Bought = 1, MonthsRepaid = 5 };
		var fixture = Inflate( raw );
		shipped.PutEconomy( fixture, shipped.Economy!.Value with { Balance = 100, Loans = loans } );

		using var stream = new MemoryStream( raw );
		var reader = new SaveReader( stream );
		reader.ReadFile();
		var world = new ParkWorld( fixture, reader.Preamble );
		var state = new ParkState( world );
		state.SetGameTick( 16840 );
		state.SetAdmissionFee( 28 );
		state.TurnTheMonth( null );

		Assert.AreEqual( (-650, -650, 16840), (state.Balance, state.LastBalance, state.TurnEnteredRed) );

		// A deposit moves the balance and the profit and leaves the last balance where the withdrawal put it.
		state.Deposit( 40 );

		Level.WritePark( world, state, "jungle", "Loan Park" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Loan Park.TPWS" ) ) ).Economy!.Value;

		// The instalment is withdrawn from the year's profit and its principal's share, the whole 750, given back.
		Assert.AreEqual( (28, -610, -650, 16840, -12013 + 40),
			(written.AdmissionFee, written.Balance, written.LastBalance, written.TurnEnteredRed, written.ProfitThisYear) );
		Assert.AreEqual( (1, 6, 750), (written.Loans[4].Bought, written.Loans[4].MonthsRepaid, written.Loans[4].MonthlyRepayment) );

		var reloaded = new ParkState( Read( File.ReadAllBytes( Path.Combine( Jungle, "Loan Park.TPWS" ) ) ) );

		Assert.AreEqual( state.DescribeEconomy(), reloaded.DescribeEconomy() );
	}
	/// <summary>
	/// <b>A written park holds the park clock's month and day</b> (FileFormats <c>saves.md</c>, "The arrival and clock
	/// fields"), and those eight bytes are all the writer changes for them: the clock's start, session and rate stay the
	/// file's. Handed none, the writer leaves the shipped park's month 1 and day 2, the date of its tick 755.
	/// </summary>
	/// <remarks><b>Mutations:</b> the month and the day swapped; the month written one-based; the writer never calling it;
	/// the reader a field out of place.</remarks>
	[TestMethod]
	public void AWrittenParkHoldsTheCalendarsMonthAndDay()
	{
		Assert.AreEqual( new ParkWorld.CalendarBlock( 125911584000000000, shipped.Calendar!.SessionStart, 1, 2, 15000 ),
			shipped.Calendar );

		var alone = ParkFileWriter.Body( shipped, Played );
		var body = ParkFileWriter.Body( shipped, Played with { Calendar = (0, 6) } );
		var written = new ParkWorld( body ).Calendar!;

		Assert.AreEqual( shipped.Calendar with { MonthAtLastUpdate = 0, DayAtLastUpdate = 6 }, written );

		var changed = Enumerable.Range( 0, body.Length ).Where( i => body[i] != alone[i] ).ToArray();

		Assert.IsTrue( changed.Length > 0 );
		Assert.IsTrue( changed[0] >= shipped.CalendarAt + 0x10 && changed[^1] < shipped.CalendarAt + 0x18,
			$"changed bytes {changed[0]}..{changed[^1]}, the clock at {shipped.CalendarAt}" );
		Assert.AreEqual( (1, 2), (new ParkWorld( alone ).Calendar!.MonthAtLastUpdate, new ParkWorld( alone ).Calendar!.DayAtLastUpdate) );
	}

	/// <summary>
	/// <b>The running park's month and day are the date of its tick</b>, as the original's calendar stores them every
	/// sweep: tick 16970 is 6 January 2002, month nought; 17556 the last of January and 17557 the first of February;
	/// 16842 the last of 2001 (month 11) and 16843 the first of 2002.
	/// </summary>
	/// <remarks><b>Mutations:</b> the month one-based; the day taken from the calendar that counts from nought.</remarks>
	[TestMethod]
	public void TheRunningParksMonthAndDayAreTheDateOfItsTick()
	{
		var state = new ParkState( shipped );

		Assert.AreEqual( (1, 2), state.CalendarAtLastUpdate );

		state.SetGameTick( 16970 );
		Assert.AreEqual( (0, 6), state.CalendarAtLastUpdate );

		state.SetGameTick( 17556 );
		Assert.AreEqual( (0, 31), state.CalendarAtLastUpdate );

		state.SetGameTick( 17557 );
		Assert.AreEqual( (1, 1), state.CalendarAtLastUpdate );

		state.SetGameTick( 16842 );
		Assert.AreEqual( (11, 31), state.CalendarAtLastUpdate );

		state.SetGameTick( 16843 );
		Assert.AreEqual( (0, 1), state.CalendarAtLastUpdate );
	}

	/// <summary>
	/// <b>The level writes the calendar as it stands</b>: a park on tick 16970 is written with month nought and day 6,
	/// where the file it was loaded from held the shipped 1 and 2.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Level.WritePark</c> handing the writer no calendar.</remarks>
	[TestMethod]
	public void TheLevelWritesTheCalendarsMonthAndDay()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		using var stream = new MemoryStream( raw );
		var reader = new SaveReader( stream );
		reader.ReadFile();
		var world = new ParkWorld( Inflate( raw ), reader.Preamble );
		var state = new ParkState( world );
		state.SetGameTick( 16970 );

		Level.WritePark( world, state, "jungle", "Calendar Park" );

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Calendar Park.TPWS" ) ) ).Calendar!;

		Assert.AreEqual( (0, 6, 16970), (written.MonthAtLastUpdate, written.DayAtLastUpdate,
			Read( File.ReadAllBytes( Path.Combine( Jungle, "Calendar Park.TPWS" ) ) ).GameTick) );
	}

	/// <summary>The park calendar on a tick as a <c>FILETIME</c>: 2000-01-01 plus the tick's 3,750 seconds at the shipped rate.</summary>
	private static long CalendarOn( int tick ) => (12591158400L + (tick * 3750L)) * 10_000_000L;

	/// <summary>
	/// <b>The shipped park's weather thing reads as the original wrote it</b> (FileFormats <c>saves.md</c>, "The weather"):
	/// quality 78 applied on tick 746, the forecast made on 653 and due four days later, the thunder count -755 on
	/// tick 755, no bolt ever put down, the wind 139 at 0.22. And it is written back whole: every field the running park
	/// changes, in its place, the voice's handle nought, the location left where none is given, <c>mPType</c> and
	/// <c>mOverridden</c> the file's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the drops' two places swapped, reader and writer alike; the location written where none is
	/// given; the handle left; the writer never calling it.</remarks>
	[TestMethod]
	public void AWrittenParkHoldsTheWeathersFields()
	{
		var file = shipped.WeatherRecord!.Value;

		Assert.AreEqual( new ParkWorld.WeatherState( 78, CalendarOn( 746 ), CalendarOn( 653 ),
			CalendarOn( 653 ) + (4 * TimeSpan.TicksPerDay), 78, false, 0, 0, false, 0, 0, -755, null, 139f, file.WindForce ), file );
		Assert.AreEqual( 0.22f, file.WindForce, 1e-6f );

		var stormy = new ParkWorld.WeatherState( 12, CalendarOn( 17000 ), CalendarOn( 17010 ), CalendarOn( 17102 ), 55, true,
			340, 450, true, 123, 3, -40, new System.Numerics.Vector3( 93.25f, 300f, 705.5f ), 163.5f, 0.88f );
		var alone = ParkFileWriter.Body( shipped, Played );
		var body = ParkFileWriter.Body( shipped, Played with { Weather = stormy } );

		Assert.AreEqual( stormy, new ParkWorld( body ).WeatherRecord );

		// Where FileFormats saves.md puts them, so a field read and written at another's place is caught too.
		Assert.AreEqual( (12, 340, 450, -40, 163.5f), (BitConverter.ToInt32( body, shipped.WeatherAt + 16 ),
			BitConverter.ToInt32( body, shipped.WeatherAt + 49 ), BitConverter.ToInt32( body, shipped.WeatherAt + 53 ),
			BitConverter.ToInt32( body, shipped.WeatherAt + 66 ), BitConverter.ToSingle( body, shipped.WeatherAt + 90 )) );
		Assert.AreEqual( 300f, BitConverter.ToSingle( body, shipped.WeatherAt + 74 ) );

		var changed = Enumerable.Range( 0, body.Length ).Where( i => body[i] != alone[i] ).ToArray();
		var at = shipped.WeatherAt;

		Assert.IsTrue( changed.Length > 0 );
		Assert.IsTrue( changed[0] >= at + 16 && changed[^1] < at + 98, $"changed bytes {changed[0]}..{changed[^1]}, the weather at {at}" );
		Assert.IsFalse( changed.Any( i => i >= at + 86 && i < at + 90 ), "mPType is the file's" );

		var dry = stormy with { LastLightningLocation = null };
		var heard = (byte[])alone.Clone();
		heard[at + 82] = 7;
		shipped.PutWeather( heard, dry );

		Assert.AreEqual( dry, new ParkWorld( heard ).WeatherRecord, "no location given leaves the file's fill" );
		Assert.AreEqual( 0, BitConverter.ToInt32( heard, at + 82 ), "the voice's handle is written nought" );
	}

	/// <summary>
	/// <b>A forecast falls due four whole days of the park calendar after it is made</b>, as the original's message 2
	/// stamps it: the shipped park's own forecast, made on tick 653, is due on 745.16.
	/// </summary>
	/// <remarks><b>Mutations:</b> the warning counted in hours.</remarks>
	[TestMethod]
	public void AForecastFallsDueItsDaysOfWarningLater()
	{
		var file = shipped.WeatherRecord!.Value;

		Assert.AreEqual( file.ForecastTime, ParkWeather.ForecastDue( file.LastForecasted, 4 ) );
	}

	/// <summary>
	/// <b>The running weather is written as it stands</b>: a quality of 10 applied on tick 16970 is stamped with that
	/// tick's calendar, asks for 450 drops and arms the lightning; with no forecast made the file's last forecast is kept
	/// and none is pending; with no bolt the file's location is kept. A bolt aimed at (100, 200) arms the thunder (10 or
	/// 11 weather ticks off) and is written at its top in the original's axes, y up, 300; a second bolt while the thunder
	/// is armed leaves it there.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stamp from the calendar that counts from nought; the location not turned into the
	/// original's axes; the location taken at every bolt; a forecast shown pending with none made.</remarks>
	[TestMethod]
	public void TheRunningWeatherIsWrittenAsItStands()
	{
		var file = shipped.WeatherRecord!.Value;
		var state = new ParkState( shipped );
		state.SetGameTick( 16970 );
		var weather = new ParkWeather( 139f, drawn: false );

		try
		{
			weather.DebugWind( 139f );
			weather.DebugQuality( 10 );

			var written = weather.Written( file );

			Assert.AreEqual( new ParkWorld.WeatherState( 10, CalendarOn( 16970 ), file.LastForecasted, file.ForecastTime,
				file.ForecastQuality, false, 0, 450, true, 0, 0, 0, null, 139f, written.WindForce ), written );
			Assert.AreEqual( 0.9f, written.WindForce, 1e-6f );

			weather.DebugStrike( new Vector3( 100f, 200f, 0f ) );
			var struck = weather.Written( file );

			Assert.IsTrue( struck.ThunderCountdown is 10 or 11, $"thunder {struck.ThunderCountdown}" );
			Assert.AreEqual( 300f, struck.LastLightningLocation!.Value.Y );
			Assert.AreEqual( 100f, struck.LastLightningLocation.Value.X, 50f );
			Assert.AreEqual( 200f, struck.LastLightningLocation.Value.Z, 50f );

			weather.DebugStrike( new Vector3( 900f, 900f, 0f ) );

			Assert.AreEqual( struck.LastLightningLocation, weather.Written( file ).LastLightningLocation );
		}
		finally
		{
			weather.Delete();
		}
	}

	/// <summary>
	/// <b>A forecast made is written valid, with its own stamp, due time and quality</b>: the park calendar three days
	/// past the last change (the shipped 7 days between changes less 4 of warning) makes one, which the save carries as
	/// pending, stamped with the tick's calendar and falling due four days on. <b>The thunder sounds at the top of the
	/// bolt that armed it</b>, not the latest bolt's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the forecast not recorded for the save; its valid flag fixed false; the thunder taken
	/// at the latest bolt.</remarks>
	[TestMethod]
	public void AForecastMadeIsWrittenValidAndTheThunderSoundsWhereItWasArmed()
	{
		var file = shipped.WeatherRecord!.Value;
		var state = new ParkState( shipped );
		state.SetGameTick( 16970 );
		GameCalendar.Rebase();
		var weather = new ParkWeather( 139f, drawn: false );

		try
		{
			weather.DebugQuality( 50 );

			// A sweep a frame until the third day has turned (47 sweeps, the first turning 1 January).
			for ( var tick = 1; tick < 5000 && GameCalendar.Days < 3; ++tick )
			{
				GameCalendar.Update( tick );
				weather.Update();
			}

			var written = weather.Written( file );

			Assert.IsTrue( weather.Forecast >= 0, "a forecast is made three days on" );
			Assert.IsTrue( written.ForecastValid );
			Assert.AreEqual( CalendarOn( 16970 ), written.LastForecasted );
			Assert.AreEqual( ParkWeather.ForecastDue( written.LastForecasted, 4 ), written.ForecastTime );
			Assert.AreEqual( weather.Forecast, written.ForecastQuality );

			weather.DebugQuality( 10 );
			weather.DebugStrike( new Vector3( 100f, 200f, 0f ) );
			var armedAt = weather.LastStrike;
			weather.DebugStrike( new Vector3( 900f, 900f, 0f ) );

			Assert.AreNotEqual( armedAt, weather.LastStrike );
			Assert.AreEqual( armedAt, weather.ThunderPlace );
		}
		finally
		{
			weather.Delete();
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>Entering the shipped Lost Kingdom puts the calendar on its own clock</b>: tick 755, 2 February 2000 at 18:27:30,
	/// the file's month 1 and day 2 the same date, so its first sweep turns nothing; a park with no file starts on
	/// 1 January and turns a day on its first sweep.
	/// </summary>
	/// <remarks><b>Mutations:</b> the park's tick not passed; the file's clock block not passed.</remarks>
	[TestMethod]
	public void EnteringTheShippedParkStartsTheCalendarOnItsTick()
	{
		try
		{
			Level.StartTheCalendar( "jungle", shipped );
			Assert.AreEqual( (755, new DateTime( 2000, 2, 2, 18, 27, 30 )), (GameCalendar.Counter, GameCalendar.Now) );

			GameCalendar.Update( 756 );
			Assert.IsFalse( GameCalendar.DayRolled || GameCalendar.MonthRolled || GameCalendar.YearRolled );

			Level.StartTheCalendar( "jungle", null );
			GameCalendar.Update( 1 );
			Assert.AreEqual( (GameCalendar.Epoch.Date, true), (GameCalendar.Now.Date, GameCalendar.DayRolled) );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The thunder's count falls every weather tick, armed or not</b>, as the original's does: a dry park five weather
	/// ticks in holds -5, the stock park's -755 on its tick 755 the same rule from nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the count held at nought between storms.</remarks>
	[TestMethod]
	public void TheThundersCountFallsEveryWeatherTick()
	{
		_ = new ParkState( shipped );
		GameCalendar.Rebase();
		var weather = new ParkWeather( 139f, drawn: false );

		try
		{
			weather.DebugQuality( 78 );
			GameCalendar.Update( 5 );
			weather.Update();

			Assert.AreEqual( -5, weather.Written( null ).ThunderCountdown );
		}
		finally
		{
			weather.Delete();
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>A loaded park's weather ticks only for the sweeps run since it was made</b>: entered on tick 755 and swept to
	/// 758, its thunder count stands at -3, not the -758 of a weather that counted from nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the weather's tick started from nought.</remarks>
	[TestMethod]
	public void ALoadedParksWeatherTicksFromItsOwnClock()
	{
		_ = new ParkState( shipped );
		GameCalendar.Rebase( shipped.GameTick, shipped.Calendar );
		var weather = new ParkWeather( 139f, drawn: false );

		try
		{
			weather.DebugQuality( 78 );
			GameCalendar.Update( 758 );
			weather.Update();

			Assert.AreEqual( -3, weather.Written( null ).ThunderCountdown );
		}
		finally
		{
			weather.Delete();
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The level writes the weather as it stands</b>: a park whose weather was set to 10 on tick 16970 is written with
	/// that quality, that stamp and its 450 drops asked for, where the file it was loaded from held the shipped 78.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Level.WritePark</c> handing the writer no weather.</remarks>
	[TestMethod]
	public void TheLevelWritesTheWeather()
	{
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		using var stream = new MemoryStream( raw );
		var reader = new SaveReader( stream );
		reader.ReadFile();
		var world = new ParkWorld( Inflate( raw ), reader.Preamble );
		var state = new ParkState( world );
		state.SetGameTick( 16970 );
		var weather = new ParkWeather( 139f, drawn: false );

		try
		{
			weather.DebugQuality( 10 );
			Level.WritePark( world, state, "jungle", "Weather Park" );
		}
		finally
		{
			weather.Delete();
		}

		var written = Read( File.ReadAllBytes( Path.Combine( Jungle, "Weather Park.TPWS" ) ) ).WeatherRecord!.Value;

		Assert.AreEqual( (10, CalendarOn( 16970 ), 450), (written.Quality, written.LastChanged, written.TargetDrops) );
	}
}
