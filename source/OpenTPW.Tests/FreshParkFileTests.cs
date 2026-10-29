using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A park made fresh, saved: its file at tick nought (<c>docs/exe/saves.md</c>, "A park made fresh: its file at tick
/// nought"; FileFormats <c>saves.md</c>, "A park made fresh"). Every value asserted here is one the docs give, measured on
/// the original's own files; none is read from a game file. They read real game files for the theme's data and are skipped
/// where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>These save the park with no people, rides or scripts to hand</b>, so what is written is the file at tick nought as
/// <see cref="FreshParkFile"/> makes it, patched by the clock, the economy, the pool and the camera: the two fixed objects, made
/// with the running rides' scripts and models, are in the files a run writes (the run's census), not here.
/// </para>
/// </summary>
[TestClass]
public class FreshParkFileTests
{
	private BaseFileSystem data = null!;
	private FreshPark fresh = null!;
	private ParkItemCatalogue catalogue = null!;
	private string? root;
	private BaseFileSystem oldSaves = null!;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void MakeAFreshPark()
	{
		Log ??= new();
		data = GameData.Required();
		FileSystem = data;
		catalogue = new ParkItemCatalogue( "jungle", data );
		fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), catalogue, data, seed: 1 );

		oldSaves = SaveFileSystem;
		root = Directory.CreateTempSubdirectory( "opentpw-fresh-file-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = false } ) );
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void Restore()
	{
		if ( root == null )
			return;

		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, null );
		SaveFileSystem = oldSaves;
		Directory.Delete( root, true );
		ParkOrbitCameraMode.Forget();
		Unimplemented.Forget();
	}

	/// <summary>The inflated body of the park saved as a fresh one is, and the file.</summary>
	private (byte[] File, byte[] Body) Save()
	{
		var path = Level.WritePark( fresh, new ParkState( fresh ), "jungle", "Fresh", catalogue: catalogue );

		Assert.IsNotNull( path, "a park made fresh is written" );
		var file = SaveFileSystem.ReadAllBytes( "users/1Test/jungle/" + Path.GetFileName( path ) );

		return (file, new SaveReader( new MemoryStream( file ) ).ReadFile());
	}

	/// <summary>Each module's extent in an inflated body: the action recording and the world, then the others by the tag that follows them.</summary>
	private static Dictionary<string, (int At, int Length)> Modules( byte[] body )
	{
		string[] tags = ["WRLD", "SPSC", "PART", "MESS", "CLOK", "VANT", "GSYS", "RSYS", "TRAK", "FLYR", "RSSE", "KAME", "COAS", "ADVS", "SOUN", "CHTS", "ADSC"];
		var recording = 8 + BitConverter.ToInt32( body, 4 );
		var modules = new Dictionary<string, (int, int)> { ["ACTREC"] = (0, recording) };
		var at = recording;

		foreach ( var tag in tags )
		{
			var found = IndexOf( body, Encoding.ASCII.GetBytes( new string( tag.Reverse().ToArray() ) ), at );
			Assert.IsTrue( found >= 0, $"the {tag} tag follows its module" );
			modules[tag] = (at, found - at);
			at = found + 4;
		}

		modules["UI"] = (at, body.Length - at);

		return modules;
	}

	private static int IndexOf( byte[] body, byte[] tag, int from )
	{
		for ( var at = from; at + tag.Length <= body.Length; ++at )
		{
			if ( body.AsSpan( at, tag.Length ).SequenceEqual( tag ) )
				return at;
		}

		return -1;
	}

	/// <summary>Every module is the length the original's files hold (FileFormats <c>saves.md</c>, "A park made fresh"), but for the sound's.</summary>
	[TestMethod]
	public void EveryModuleIsTheLengthOfTheOriginals()
	{
		var (_, body) = Save();
		var modules = Modules( body );

		Assert.AreEqual( 143, modules["ACTREC"].Length );
		Assert.AreEqual( 1_469_113 - 2 * 1099, modules["WRLD"].Length, "the original's world, less the two objects made with the rides" );
		Assert.AreEqual( 412, modules["SPSC"].Length );
		Assert.AreEqual( 76_252, modules["PART"].Length );
		Assert.AreEqual( 196, modules["MESS"].Length );
		Assert.AreEqual( 8, modules["CLOK"].Length );
		Assert.AreEqual( 4, modules["VANT"].Length );
		Assert.AreEqual( 36, modules["GSYS"].Length );
		Assert.AreEqual( 44, modules["TRAK"].Length );
		Assert.AreEqual( 452, modules["FLYR"].Length );
		Assert.AreEqual( 56, modules["RSSE"].Length, "no script: the gates' and the lights' are made with the rides" );
		Assert.AreEqual( 16 + 2 + 107 * 103, modules["RSYS"].Length, "two empty slots and the boundary's 107 pieces of 103 bytes" );
		Assert.AreEqual( 40, modules["KAME"].Length );
		Assert.AreEqual( 16, modules["COAS"].Length );
		Assert.AreEqual( 372, modules["ADVS"].Length );
		Assert.AreEqual( 2, modules["CHTS"].Length );
		Assert.AreEqual( 4, modules["ADSC"].Length );
		Assert.AreEqual( 4, modules["UI"].Length );
	}

	/// <summary>The container: version 500 and the shipped park's legal text, the world twelve things and every module read.</summary>
	[TestMethod]
	public void TheFileIsVersion500WithTheShippedParksLegalTextAndReadsWhole()
	{
		var (file, body) = Save();
		var shipped = data.ReadAllBytes( "levels/jungle/Easymode.TPWI" );

		Assert.AreEqual( 500, BitConverter.ToInt32( file, 0 ) );
		CollectionAssert.AreEqual( shipped[4..0x60D], file[4..0x60D], "the text after the version is the shipped park's" );

		var world = new ParkWorld( body, file[..0x60D] );

		Assert.IsNull( world.Problem );
		Assert.IsTrue( world.ClosedOnTrailer );
		Assert.AreEqual( 10, world.ThingCount, "the ten managers; the objects are made with the rides" );
		Assert.AreEqual( 0, world.GameTick );
		Assert.AreEqual( 1, world.ParkClosed );
		Assert.AreEqual( 0, world.NumberOfVisitorsToDate );
		Assert.AreEqual( 11, world.ParkGates );
		Assert.AreEqual( 12, world.TrafficLights );
		Assert.AreEqual( 0, world.FirstObject, "the object list is empty until the objects are made" );
		Assert.IsNull( world.ScriptStates.Problem );
		Assert.IsNull( world.TrackRides.Problem );
		Assert.IsNull( world.Clock.Problem );
		Assert.IsNull( world.Camera.Problem );
		Assert.IsNull( world.Tags.Problem );
	}

	/// <summary>The ten managers, newest first, with cell 0's chain theirs alone until the objects head it.</summary>
	[TestMethod]
	public void TheManagersStandInOneChainOnCellZero()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );

		Assert.AreEqual( 0, world.Objects.Count );
		CollectionAssert.AreEqual( new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, world.Things.Select( thing => thing.ThingId ).ToArray() );
		Assert.AreEqual( 10, world.Cells[0].Occupant );
	}

	/// <summary>Every cell the file reads is the fresh park's own, field for field: the ground, the paths, the boundary's anchors.</summary>
	[TestMethod]
	public void EveryCellReadsBackAsTheFreshParksOwn()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );

		Assert.AreEqual( 128 * 128, world.Cells.Count );

		for ( var cell = 0; cell < world.Cells.Count; ++cell )
			Assert.AreEqual( cell == 0 ? fresh.Cells[0] with { Occupant = 10 } : fresh.Cells[cell], world.Cells[cell], $"cell {cell % 128},{cell / 128}" );
	}

	/// <summary>107 boundary anchors of track type 25, each taking the model slot after the gates' and lights', in the walk's order.</summary>
	[TestMethod]
	public void TheBoundarysAnchorsTakeTheModelSlotsInTheOrderTheyWereLaid()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );
		var anchors = world.Cells.Count( cell => cell.TrackType == 25 );

		Assert.AreEqual( 107, anchors );
		Assert.AreEqual( 107, fresh.Boundary.Count );

		var modules = Modules( body );
		var ride = modules["RSYS"].At;

		// The present records, the empty slots and the cursor: the first two slots are left for the gates' and lights' models.
		Assert.AreEqual( 107, BitConverter.ToInt32( body, ride + 4 ) );
		Assert.AreEqual( 2, BitConverter.ToInt32( body, ride + 8 ) );
		Assert.AreEqual( 0, BitConverter.ToInt32( body, ride + 12 ) );
		Assert.AreEqual( 0, body[ride + 16], "slot 0 is empty" );
		Assert.AreEqual( 1, body[ride + 18], "slot 2 holds the first piece" );

		// Each piece's record, in the walk's order: its item, cell, two cells square, the queue piece's flags and its angle.
		for ( var anchor = 0; anchor < fresh.Boundary.Count; ++anchor )
		{
			var piece = ParkBoundaryHoarding.PieceOf( fresh.Boundary[anchor] );
			var record = ride + 18 + anchor * 103;

			Assert.AreEqual( (ParkBoundaryHoarding.FirstItem + piece.Row, piece.CellX, piece.CellY, 2, 2, 0x33a, piece.Angle),
				(BitConverter.ToInt32( body, record + 1 ), BitConverter.ToInt32( body, record + 5 ), BitConverter.ToInt32( body, record + 9 ),
				BitConverter.ToInt32( body, record + 0x0d ), BitConverter.ToInt32( body, record + 0x11 ), BitConverter.ToInt32( body, record + 0x15 ),
				BitConverter.ToInt32( body, record + 0x27 )), $"piece {anchor}" );
		}
	}

	/// <summary>
	/// Each anchor's track record names its piece's slot (the third on, in the walk's order), the mask of its neighbours two cells away, the way
	/// the boundary leaves it, and, on a corner, one build over it: a clicked point ends a run and starts the next.
	/// </summary>
	[TestMethod]
	public void EachAnchorsTrackRecordHoldsItsSlotMaskDirectionAndOverlap()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );
		var cells = Modules( body )["WRLD"].At + 64 + 150 * 32 + 6 + 640 + 76;

		for ( var at = 0; at < fresh.Boundary.Count; ++at )
		{
			var anchor = fresh.Boundary[at];
			var index = anchor.Y * 128 + anchor.X;
			var track = cells + index * ParkWorld.FreshCellSize + 1 + 52;
			var corner = int.PopCount( anchor.Mask ) == 2 && anchor.Mask is not (0x11 or 0x44);

			Assert.AreEqual( 3 + at, BitConverter.ToInt32( body, track + 3 ), $"anchor {at}'s piece's model slot plus one" );
			Assert.AreEqual( anchor.Mask, body[track + 28], $"anchor {at}'s neighbours" );
			Assert.AreEqual( anchor.Direction, body[track], $"anchor {at}'s direction" );
			Assert.AreEqual( corner ? 1 : 0, BitConverter.ToInt16( body, track + 8 ), $"anchor {at}'s overlap counter" );
			Assert.AreEqual( ParkBoundaryHoarding.Family, world.Cells[index].TrackTileSet );
		}
	}

	/// <summary>
	/// The gates and the lights are fixed items: their models are recorded on the item's own cell (the gates' 45 and 16, the lights'
	/// and the vehicles' 48 and 17) with flags 0x361, where a placed object's are 0x32f on the cell it stands on.
	/// </summary>
	[TestMethod]
	public void TheGatesAndTheLightsAreRecordedAsFixedItems()
	{
		Assert.IsTrue( catalogue.TryGet( 1601, out var gates ) );
		Assert.IsTrue( catalogue.TryGet( 1603, out var lights ) );
		Assert.AreEqual( (45, 16), gates.EngineMapOffset );
		Assert.IsNull( lights.EngineMapOffset );

		var fixedRecord = ParkThingStates.MadeRecord( 1601, 0, 0, 6, 3, 1, 0, 0f, 0, [], null, false, (45, 16) );
		Assert.AreEqual( (45, 16, 6, 3, 0x361), (BitConverter.ToInt32( fixedRecord, 5 ), BitConverter.ToInt32( fixedRecord, 9 ),
			BitConverter.ToInt32( fixedRecord, 0x0d ), BitConverter.ToInt32( fixedRecord, 0x11 ), BitConverter.ToInt32( fixedRecord, 0x15 )) );

		var placed = ParkThingStates.MadeRecord( 1203, 10, 12, 2, 2, 1, 0, 0f, 0, [] );
		Assert.AreEqual( (10, 12, 2, 2, 0x32f), (BitConverter.ToInt32( placed, 5 ), BitConverter.ToInt32( placed, 9 ),
			BitConverter.ToInt32( placed, 0x0d ), BitConverter.ToInt32( placed, 0x11 ), BitConverter.ToInt32( placed, 0x15 )) );
	}

	/// <summary>The particles: the land's one emitter, WaterFall (effect 20), on handle 0x10000, in the library's slot 20.</summary>
	[TestMethod]
	public void TheParticlesHoldTheLandsWaterFallAlone()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );
		var used = world.Particles.Used;

		Assert.AreEqual( 1, used.Count );
		Assert.AreEqual( 20, used[0].Template );
		Assert.AreEqual( 0x10000, used[0].Handle );
		Assert.AreEqual( (34068, 312, 33668), (used[0].X, used[0].Height, used[0].Z) );
		Assert.AreEqual( 1000, world.Particles.Density, "the options' graphics density" );
	}

	/// <summary>The sprite table has 100 slots and none live; the message centre's sets are the twelve things'.</summary>
	[TestMethod]
	public void TheSpritesAreNoneAndTheMessageSetsAreTheTwelveThings()
	{
		var (_, body) = Save();
		var modules = Modules( body );
		var sprites = modules["SPSC"].At;

		Assert.AreEqual( "TPCS", Encoding.ASCII.GetString( body, sprites, 4 ) );
		Assert.AreEqual( 0x118, BitConverter.ToInt32( body, sprites + 4 ) );
		Assert.AreEqual( 100, BitConverter.ToInt32( body, sprites + 8 ) );
		Assert.IsTrue( body.AsSpan( sprites + 12, 400 ).ToArray().All( b => b == 0 ) );

		var at = modules["MESS"].At;
		Assert.AreEqual( 29, BitConverter.ToInt32( body, at ) );
		at += 4;

		var sets = new List<int[]>();

		for ( var set = 0; set < 29; ++set )
		{
			var count = BitConverter.ToInt32( body, at );
			sets.Add( Enumerable.Range( 0, count ).Select( n => (int)BitConverter.ToUInt16( body, at + 4 + n * 2 ) ).ToArray() );
			at += 4 + count * 2;
		}

		CollectionAssert.AreEqual( new[] { 1, 4, 5, 8 }, sets[0xc], "the month's change" );
		CollectionAssert.AreEqual( new[] { 10, 11, 12 }, sets[0xb], "the day's change: the two objects and the challenge manager" );
		CollectionAssert.AreEqual( new[] { 8 }, sets[0xd], "the year's change: the economy" );
		CollectionAssert.AreEqual( new[] { 3, 5, 6, 10 }, sets[0x11] );
	}

	/// <summary>The analyser: a 32 by 32 grid of 4-cell blocks, each with nine answers never worked out, and sixteen months' rings opening at -1.</summary>
	[TestMethod]
	public void TheAnalyserIsAsItsConstructorsLeaveIt()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );
		var at = Modules( body )["WRLD"].At + 64 + 150 * 32 + 6 + 640 + 76 + 128 * 128 * ParkWorld.FreshCellSize + 4;

		// The twelve things are written newest first; the analyser is the fifth from the end of the managers' run: find it by model.
		var analyser = FindThing( body, at, 13 );

		Assert.AreEqual( (4, 32, 32, 128, 128), (BitConverter.ToInt32( body, analyser + 16 ), BitConverter.ToInt32( body, analyser + 20 ),
			BitConverter.ToInt32( body, analyser + 24 ), BitConverter.ToInt32( body, analyser + 28 ), BitConverter.ToInt32( body, analyser + 32 )) );

		// Block 33 is (4, 4): its answers are -999, the first two as floats.
		var block = analyser + 36 + 33 * 61;
		Assert.AreEqual( (4, 4, 4, 4), (BitConverter.ToInt32( body, block ), BitConverter.ToInt32( body, block + 4 ),
			BitConverter.ToInt32( body, block + 8 ), BitConverter.ToInt32( body, block + 12 )) );
		Assert.AreEqual( -999f, BitConverter.ToSingle( body, block + 16 ) );
		Assert.AreEqual( -999, BitConverter.ToInt32( body, block + 16 + 2 * 5 ) );

		// The first of the sixteen rings: current entry -1, 144 entries, nothing in it.
		var ring = analyser + 62_598;
		Assert.AreEqual( (-1, 144), (BitConverter.ToInt32( body, ring ), BitConverter.ToInt32( body, ring + 4 )) );
		Assert.IsNotNull( world );
	}

	/// <summary>The challenge manager holds the theme's challenges in list order, from <c>Challenges.sam</c>: the first is "sell 30 drinks in 60 days" for $5000.</summary>
	[TestMethod]
	public void TheChallengeManagerHoldsTheThemesChallengesInListOrder()
	{
		var (_, body) = Save();
		var at = FindThing( body, Modules( body )["WRLD"].At + 64 + 150 * 32 + 6 + 640 + 76 + 128 * 128 * ParkWorld.FreshCellSize + 4, 19 );

		Assert.AreEqual( (3, 60, 30, 5000, 1), (BitConverter.ToInt32( body, at + 16 ), BitConverter.ToInt32( body, at + 20 ),
			BitConverter.ToInt32( body, at + 24 ), BitConverter.ToInt32( body, at + 36 ), (int)body[at + 58] ) );
		Assert.AreEqual( -1, BitConverter.ToInt32( body, at + 916 ), "no challenge is current" );
		Assert.AreEqual( 0, body[at + 928], "the system is off" );
	}

	/// <summary>The clock before its first update holds month 0 and day -1, and a start of 2000-01-01 at 15000 a second.</summary>
	[TestMethod]
	public void TheClockHoldsMonthNoughtAndDayMinusOneAtTickNought()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );

		Assert.AreEqual( ParkWorld.StaffState.FileTimeOf( new DateTime( 2000, 1, 1 ) ), world.Calendar!.FunnyTimeStart );
		Assert.AreEqual( (0, -1, 15000), (world.Calendar.MonthAtLastUpdate, world.Calendar.DayAtLastUpdate, world.Calendar.FunnySecsPerRealSec) );
		Assert.AreEqual( (50000, 50000, 1), (world.Economy!.Value.Balance, world.Economy.Value.LastBalance, world.Economy.Value.WithdrawalsEnabled),
			"the bank's last balance is the cash it starts with" );
	}

	/// <summary>The advisor's record: slot 0 taken by message 0 at a score of 100000, and all 351 messages never said.</summary>
	[TestMethod]
	public void TheAdvisorsRecordsAreAllFresh()
	{
		var (_, body) = Save();
		var at = FindThing( body, Modules( body )["WRLD"].At + 64 + 150 * 32 + 6 + 640 + 76 + 128 * 128 * ParkWorld.FreshCellSize + 4, 11 );

		Assert.AreEqual( 100000, BitConverter.ToInt32( body, at + 24 + 10 ) );
		Assert.AreEqual( 5, BitConverter.ToInt32( body, Modules( body )["ADVS"].At + 8 + 0x110 ), "Advisor_ResetState's +0x110" );

		for ( var message = 0; message < 351; ++message )
			Assert.AreEqual( -1, BitConverter.ToInt32( body, at + 204 + 16 * message + 4 ), $"message {message}'s line" );
	}

	/// <summary>The sound: the land's five voices of Lost Kingdom, two of them following the listener in a rectangle.</summary>
	/// <remarks><b>Mutations:</b> a voice's handle, place, range, category or rectangle changed; the voices in another order.</remarks>
	[TestMethod]
	public void TheSoundHoldsTheLandsFiveVoices()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );
		var sound = Modules( body )["SOUN"].At;

		// 12 bytes of state header, the 84 of the state, 12 of floats, then the list's length and voice count.
		Assert.AreEqual( 5, BitConverter.ToInt32( body, sound + 12 + 84 + 12 + 4 ) );
		Assert.AreEqual( 0, BitConverter.ToInt32( body, sound + 12 ), "no music voice is saved" );
		Assert.IsNotNull( world );

		// The category record and the pool's block count sit between the count and the first voice.
		var at = sound + 12 + 84 + 12 + 8;
		at += 8 + BitConverter.ToInt32( body, at ) + 4;

		var voices = new List<(int Handle, float X, float Z, float Range, int Category, bool Rectangle, int Corners)>();

		for ( var voice = 0; voice < 5; ++voice )
		{
			var handle = BitConverter.ToInt32( body, at );
			var rectangle = BitConverter.ToInt32( body, at + 52 ) != 0;
			var corners = rectangle ? BitConverter.ToInt32( body, at + 56 + 8 ) + BitConverter.ToInt32( body, at + 56 + 12 )
				+ BitConverter.ToInt32( body, at + 56 + 16 ) + BitConverter.ToInt32( body, at + 56 + 20 ) : 0;

			voices.Add( (handle, BitConverter.ToSingle( body, at + 12 ), BitConverter.ToSingle( body, at + 20 ), BitConverter.ToSingle( body, at + 28 ),
				BitConverter.ToInt32( body, at + 32 ), rectangle, corners) );

			// 13 dwords, the rectangle's flag, its eight controller bytes and four corners, the mark, a byte, the handle and a nought.
			at += 52 + 4 + (rectangle ? 24 : 0) + 4 + 1 + 4 + 4;
		}

		CollectionAssert.AreEqual( new[] { 0x360061, 0x370060, 0x38005e, 0x390063, 0x3a005f }, voices.Select( voice => voice.Handle ).ToArray() );
		CollectionAssert.AreEqual( new[] { 353f, 372f, 21f, 530f, 511f }, voices.Select( voice => voice.X ).ToArray() );
		CollectionAssert.AreEqual( new[] { 31f, 21f, 39f, 513f, 664f }, voices.Select( voice => voice.Z ).ToArray() );
		CollectionAssert.AreEqual( new[] { 0f, 0f, 19f, 97f, 97f }, voices.Select( voice => voice.Range ).ToArray() );
		CollectionAssert.AreEqual( new[] { 1, 1, 7, 1, 1 }, voices.Select( voice => voice.Category ).ToArray() );
		CollectionAssert.AreEqual( new[] { true, true, false, false, false }, voices.Select( voice => voice.Rectangle ).ToArray() );
		CollectionAssert.AreEqual( new[] { 353 + 31 + 450 - 66, 372 + 21 + 567 - 174, 0, 0, 0 }, voices.Select( voice => voice.Corners ).ToArray() );
		Assert.AreEqual( at, sound + Modules( body )["SOUN"].Length, "the five voices end the module" );
	}

	/// <summary>The object controls: each of the 70 items' record reads back as the fresh park's own, the count after the 150 slots 70.</summary>
	[TestMethod]
	public void TheObjectControlsReadBackAsTheFreshParksOwn()
	{
		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );

		Assert.AreEqual( 70, fresh.ObjectControlRecords.Count );
		Assert.AreEqual( 70, BitConverter.ToInt32( body, 143 + 64 + 150 * 32 ), "mNumObjectControls" );
		CollectionAssert.AreEqual( fresh.ObjectControlRecords.ToArray(), world.ObjectControlRecords.ToArray() );
	}

	/// <summary>
	/// The sound's category table: 16 slots, the first named by a nought, five of the global sound, the global speech, the level's sound
	/// and speech and its music after them, and five unused; the length of the list counts itself.
	/// </summary>
	[TestMethod]
	public void TheSoundsCategoryTableNamesTheLandsCategories()
	{
		var (_, body) = Save();
		var sound = Modules( body )["SOUN"];
		var at = sound.At + 12 + 84 + 12;

		Assert.AreEqual( sound.At + sound.Length - at, BitConverter.ToInt32( body, at ), "the list's length counts itself and ends the module" );

		at += 8;
		var catalogueLength = BitConverter.ToInt32( body, at );
		Assert.AreEqual( 16, BitConverter.ToInt32( body, at + 4 ) );
		at += 8 + 16 * 4;

		var names = new List<string>();
		var start = at;

		Assert.AreEqual( (1, 0), (body[start], body[start + 1]), "the first slot is named by a nought: a length of one and the nought" );

		for ( var slot = 0; slot < 16; ++slot )
		{
			var length = body[at++];
			names.Add( Encoding.ASCII.GetString( body, at, length ).TrimEnd( '\0' ) );
			at += length;
		}

		Assert.AreEqual( catalogueLength, at - start + 16 * 4, "the record's length counts the slots' noughts and the names" );
		Assert.AreEqual( 1, BitConverter.ToInt32( body, at ), "one block of voices" );
		Assert.AreEqual( "", names[0] );
		Assert.AreEqual( @"\data\global\sound\cat_ambient", names[1] );
		Assert.AreEqual( @"\data\global\speech\cat_speech", names[6] );
		Assert.AreEqual( @"\data\levels\jungle\sound\cat_ambient", names[7] );
		Assert.AreEqual( @"\data\levels\jungle\music\cat_music", names[10] );
		Assert.IsTrue( names.Skip( 11 ).All( name => name.Length == 0 ) );
	}

	/// <summary>The advisor's costume: Lost Kingdom's add-on 4 (the pith helmet) and its two sub-offs, 19 and 20 (the antennae).</summary>
	[TestMethod]
	public void TheAdvisorsCostumeWordsAreTheThemes()
	{
		var (_, body) = Save();
		var advisor = Modules( body )["ADVS"].At + 8;

		Assert.AreEqual( 4, BitConverter.ToInt32( body, advisor + 0x144 ) );
		Assert.AreEqual( 19, BitConverter.ToInt32( body, advisor + 0x154 ) );
		Assert.AreEqual( 20, BitConverter.ToInt32( body, advisor + 0x158 ) );
	}

	/// <summary>The running rides of a park made fresh hold its file's clock, so the things made after the load are written by it.</summary>
	[TestMethod]
	public void ARunningFreshParkHasItsClockToWriteThingsBy()
	{
		GameClock.Rebase();
		var rides = new ParkRides( "jungle", fresh, catalogue, data );

		try
		{
			var things = rides.Written( fresh.BaseFile(), [], id => 1, _ => null );

			Assert.IsNotNull( things, "the scripts, models and clock of the base read" );
		}
		finally
		{
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// The advisor's model words (<c>Advisor_ResetState</c>): each slot's head node and five mouths, found by name, and twenty clip lengths of
	/// one model, slot 1's, with clip 14's nought. Lost Kingdom's: the lobby fly's mouths are nodes 5 to 9 and the level's 7 to 11, the
	/// two have no node named <c>bug head</c>, and the level's clips run 1666, 2500, 3333 ... 666 then 28666, 633, 2966, 1300 and 2000.
	/// </summary>
	/// <remarks><b>Mutations:</b> the mouths in the other slot's place; the lengths from slot 0; clip 14 kept; an entry off by one.</remarks>
	[TestMethod]
	public void TheAdvisorsModelWordsAreTheModelsOwn()
	{
		var (_, body) = Save();
		var advisor = Modules( body )["ADVS"].At + 8;
		int Word( int offset ) => BitConverter.ToInt32( body, advisor + offset );

		Assert.AreEqual( (0, 0), (Word( 0x38 ), Word( 0x3c )), "no node is named bug head" );
		CollectionAssert.AreEqual( new[] { 5, 6, 7, 8, 9 }, Enumerable.Range( 0, 5 ).Select( mouth => Word( 0x40 + mouth * 4 ) ).ToArray() );
		CollectionAssert.AreEqual( new[] { 7, 8, 9, 10, 11 }, Enumerable.Range( 0, 5 ).Select( mouth => Word( 0x54 + mouth * 4 ) ).ToArray() );
		CollectionAssert.AreEqual(
			new[] { 1666, 2500, 3333, 4166, 5000, 7500, 9166, 10000, 10833, 12500, 3333, 5000, 11000, 0, 666, 28666, 633, 2966, 1300, 2000 },
			Enumerable.Range( 0, 20 ).Select( clip => Word( 0x68 + clip * 4 ) ).ToArray() );

		// The rule, from the models: the same words by name and by the declared span.
		var lobby = FreshParkFile.AdvisorModelWords( "global/advisor/Advisor.md2" );
		var level = FreshParkFile.AdvisorModelWords( "levels/jungle/advisor/Advisor_JUN.md2" );

		Assert.IsTrue( lobby.Found && level.Found );
		CollectionAssert.AreEqual( lobby.Mouths, Enumerable.Range( 0, 5 ).Select( mouth => Word( 0x40 + mouth * 4 ) ).ToArray() );
		CollectionAssert.AreEqual( level.Mouths, Enumerable.Range( 0, 5 ).Select( mouth => Word( 0x54 + mouth * 4 ) ).ToArray() );
		Assert.AreEqual( 0, FreshParkFile.AdvisorModelWords( "levels/jungle/advisor/NoSuchAdvisor.md2" ).Clips.Sum(), "a model that is not there gives noughts" );
	}

	/// <summary>
	/// The gates and the lights reach the file as fixed items through the whole writer (the level's bought things, the rides' record, the
	/// file writer): the gates' record is (45, 16) six by three and the lights' (48, 17) one by one, both with flags 0x361.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Level.WritePark</c> passes no fixed cell; <c>ParkRides.Written</c> drops <c>FixedCell</c>; the file writer ignores it.</remarks>
	[TestMethod]
	public void TheGatesAndTheLightsReachTheFileAsFixedItemsThroughTheWholeWriter()
	{
		GameClock.Rebase();
		var rides = new ParkRides( "jungle", fresh, catalogue, data );

		try
		{
			var state = new ParkState( fresh );
			var people = new ParkPeople( fresh, new ParkBalance( "jungle", easyMode: false ), null, state, catalogue );
			var path = Level.WritePark( fresh, state, "jungle", "FreshGates", people, null, rides, catalogue );

			Assert.IsNotNull( path );

			var body = new SaveReader( new MemoryStream( SaveFileSystem.ReadAllBytes( "users/1Test/jungle/" + Path.GetFileName( path ) ) ) ).ReadFile();
			var ride = Modules( body )["RSYS"].At;

			// Slot 0 is the gates' record and slot 1 the lights', each a whole record (a record's flag byte, item, then the cell, the
			// footprint and the flags): found by their words, which no placed object's record shares (its flags are 0x32f).
			var module = Modules( body )["RSYS"];
			bool Holds( params int[] words )
			{
				for ( var at = ride + 16; at + 4 * words.Length <= ride + module.Length; ++at )
				{
					if ( words.Select( ( word, n ) => BitConverter.ToInt32( body, at + 4 * n ) == word ).All( same => same ) )
						return true;
				}

				return false;
			}

			Assert.AreEqual( 1, body[ride + 16], "slot 0 holds the gates" );
			Assert.IsTrue( Holds( 45, 16, 6, 3, 0x361 ), "the gates, on (45, 16)" );
			Assert.IsTrue( Holds( 48, 17, 1, 1, 0x361 ), "the lights, on (48, 17)" );
			Assert.IsFalse( Holds( 6, 3, 0x32f ) || Holds( 1, 1, 0x32f ), "no placed object's record: the gates and lights stand on no cell of their own" );
		}
		finally
		{
			TestRun.DeleteEvery<ParkPeople>();
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// The two objects as the original's own file at tick nought holds them (<c>q327/orig/a</c>; <c>docs/exe/saves.md</c>, "OpenTPW's
	/// writer, a park made fresh"): their model records' tables as the model maker leaves them, the gates' words <c>0 0 0 601 601 401</c>
	/// with two lookups <c>(9, -1)</c> and <c>(0x21, -1)</c> and shared flags 1, the lights' eight noughts and <c>401</c> with none, and each
	/// one's channel never started, at speed nought; their scripts never yet run, the budget word <c>+0x98</c> nought and the looping key
	/// <c>0xFFFF</c>, the header counting the two, each in the folder the executable spells <c>Features</c>.
	/// </summary>
	/// <remarks><b>Mutations:</b> no fresh tables for an object with no saved record; a never-started channel at speed 1; <c>+0x98</c> -1
	/// before a turn; the looping key starting at nought; the made scripts left uncounted; the folder lower case.</remarks>
	[TestMethod]
	public void TheGatesAndTheLightsAreWrittenAsTheOriginalsAtTickNought()
	{
		GameClock.Rebase();

		// The two stand in the park as its objects do in a running game, which is what binds an unplaced fixed item's script.
		var objects = new ParkObjects( "jungle", null, null );

		foreach ( var placed in fresh.Objects )
		{
			Assert.IsTrue( catalogue.TryGet( placed.CatalogueId, out var item ) );
			objects.Stand( placed.ThingId, null!, RideAnimations.Load( item.Directory, item.Stem, data, item.AnimationChannels ) );
		}

		var rides = new ParkRides( "jungle", fresh, catalogue, data, objects );

		try
		{
			var state = new ParkState( fresh );
			var people = new ParkPeople( fresh, new ParkBalance( "jungle", easyMode: false ), null, state, catalogue );
			var path = Level.WritePark( fresh, state, "jungle", "FreshTables", people, null, rides, catalogue );

			Assert.IsNotNull( path );
			Assert.AreEqual( 0, Unimplemented.Summary.Where( counted => counted.What == "SAVE_PARK_MADE_MODEL_TABLES" ).Sum( counted => counted.Times ) );

			var file = SaveFileSystem.ReadAllBytes( "users/1Test/jungle/" + Path.GetFileName( path ) );
			var body = new SaveReader( new MemoryStream( file ) ).ReadFile();
			var world = new ParkWorld( body, file[..0x60D] );
			var models = world.ThingStates( _ => 1 );

			// Slot 0 is the gates' model (handle 1), slot 1 the lights' (handle 2).

			var gates = models.InSlot( 0 )!.Value;
			var lights = models.InSlot( 1 )!.Value;

			Assert.AreEqual( (1601, 1603), (gates.CatalogueId, lights.CatalogueId) );
			CollectionAssert.AreEqual( new uint[] { 0, 0, 0, 0x601, 0x601, 0x401 }, gates.NodeWords );
			CollectionAssert.AreEqual( new uint[] { 0, 0, 0, 0, 0, 0, 0, 0, 0x401 }, lights.NodeWords );

			var gateLookups = models.LookupsOf( 0 )!.Value;
			CollectionAssert.AreEqual( new[] { (9, -1), (0x21, -1) }, gateLookups.Records );
			Assert.AreEqual( 1, gateLookups.Shared );
			Assert.AreEqual( (0, 0), (models.LookupsOf( 1 )!.Value.Records.Length, models.LookupsOf( 1 )!.Value.Shared) );

			foreach ( var channel in gates.Channels.Concat( lights.Channels ) )
				Assert.AreEqual( (ParkThingStates.NoRole, 0f, 0f), (channel.Role, channel.Speed, channel.QueuedSpeed), "a channel never started" );

			var scripts = Modules( body )["RSSE"].At;
			Assert.AreEqual( (3, 2), (BitConverter.ToInt32( body, scripts + 16 ), BitConverter.ToInt32( body, scripts + 20 )), "the next handle, and the count of the two made" );

			foreach ( var handle in new[] { 1, 2 } )
				Assert.AreEqual( 0xFFFF, world.ScriptStates.For( handle )!.Value.LoopingKey, "the loader's looping key" );

			// The newest first: the lights' struct opens the records, after the module's header, five discarded dwords, the count and the size.
			var first = scripts + 8 + 20 + 20 + 8;
			Assert.AreEqual( (2, 0), (BitConverter.ToInt32( body, first + 8 ), BitConverter.ToInt32( body, first + 0x98 )), "no turn yet: the budget word nought" );

			foreach ( var stem in new[] { "gates", "lights" } )
			{
				Assert.IsTrue( body.AsSpan().IndexOf( Encoding.ASCII.GetBytes( $"data\\levels\\jungle\\Features\\{stem}\\\0" ) ) > 0, $"the {stem}' folder" );
			}
		}
		finally
		{
			TestRun.DeleteEvery<ParkPeople>();
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// The object controls' <c>+0x08</c>, the item's <c>FatigueEffect</c> (<c>FUN_004d3e00</c>): 5 on 31 of the 70, the rides, the shops,
	/// the sideshows and item 100, as every fresh file of the original's holds it; nought on the rest, the three add-ons 1500 to 1502 among them.
	/// </summary>
	/// <remarks><b>Mutations:</b> the word nought; RipOffOK in its place; the category's fallback dropped.</remarks>
	[TestMethod]
	public void TheObjectControlsHoldEachItemsFatigueEffect()
	{
		var (_, body) = Save();
		var controls = 143 + 64;
		var fives = new List<int>();

		for ( var record = 0; record < fresh.ObjectControlRecords.Count; ++record )
		{
			var word = BitConverter.ToInt32( body, controls + record * 32 + 8 );
			Assert.IsTrue( word is 0 or 5, $"record {record}: {word}" );

			if ( word == 5 )
				fives.Add( BitConverter.ToUInt16( body, controls + record * 32 ) );
		}

		Assert.AreEqual( 31, fives.Count );
		Assert.AreEqual( 17, fives.Count( item => item is >= 1100 and < 1200 ) );
		Assert.AreEqual( (8, 5), (fives.Count( item => catalogue.TryGet( item, out var shop ) && shop.Directory.Contains( "/shops/" ) ),
			fives.Count( item => catalogue.TryGet( item, out var side ) && side.Directory.Contains( "/sideshow/" ) )) );
		CollectionAssert.Contains( fives, 100 );
		CollectionAssert.DoesNotContain( fives, 1500 );
		CollectionAssert.DoesNotContain( fives, 1601 );
		Assert.AreEqual( 0, BitConverter.ToInt32( body, controls + 8 + 32 * fresh.ObjectControlRecords.ToList().FindIndex( control => control.ItemId == 1601 ) ) );
	}

	/// <summary>
	/// A park made fresh holds month 0 and day -1 while its tick is nought, and its date from the first sweep on; so does one read back from a
	/// file that holds day -1, which is how a restart of it runs.
	/// </summary>
	/// <remarks><b>Mutations:</b> the tick-nought test dropped; the file's day -1 arm dropped.</remarks>
	[TestMethod]
	public void AFreshParksCalendarHoldsDayMinusOneUntilItsFirstSweep()
	{
		var state = new ParkState( fresh );

		Assert.AreEqual( (0, -1), state.CalendarAtLastUpdate );

		state.SetGameTick( 1 );
		Assert.AreNotEqual( (0, -1), state.CalendarAtLastUpdate, "a later tick writes its own date" );
		Assert.AreEqual( (0, 1), state.CalendarAtLastUpdate, "the first of January" );

		var (file, body) = Save();
		var world = new ParkWorld( body, file[..0x60D] );
		var again = new ParkState( world );

		Assert.IsNotNull( world.Calendar );
		Assert.AreEqual( -1, world.Calendar.DayAtLastUpdate );
		Assert.AreEqual( (0, -1), again.CalendarAtLastUpdate, "read back from the file" );

		again.SetGameTick( 1 );
		Assert.AreEqual( (0, 1), again.CalendarAtLastUpdate );
	}

	/// <summary>The camera's rotation is written as +0 where the yaw is nought (<c>0f - Yaw</c>), not as a negative zero.</summary>
	/// <remarks><b>Mutations:</b> <c>-Yaw</c> in <c>Level.WritePark</c>.</remarks>
	[TestMethod]
	public void TheCameraYawOfNoughtIsWrittenAsPositiveZero()
	{
		ParkOrbitCameraMode.Forget();
		var (_, body) = Save();

		Assert.AreEqual( 0, BitConverter.ToInt32( body, Modules( body )["KAME"].At + 4 ), "the rotation's bits" );
	}

	private static int FindThing( byte[] body, int from, int model )
	{
		int[] sizes = [937, 16, 300, 99, 4190, 73544, 16, 5846, 18, 103];
		int[] models = [19, 17, 16, 15, 14, 13, 12, 11, 10, 9];
		var at = from;

		for ( var thing = 0; thing < models.Length; ++thing )
		{
			Assert.AreEqual( models[thing], BitConverter.ToInt32( body, at + 4 ), $"thing {thing}'s model" );

			if ( models[thing] == model )
				return at;

			at += sizes[thing];
		}

		throw new AssertFailedException( $"no thing of model {model}" );
	}
}
