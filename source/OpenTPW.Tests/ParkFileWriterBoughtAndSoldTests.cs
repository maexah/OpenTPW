using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, a thing bought and a thing sold (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a thing
/// bought and a thing sold"): a made object's three records and its cells, a gone one's taken out.
/// </summary>
[TestClass]
public class ParkFileWriterBoughtAndSoldTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int CrazyApe = 1101, DrinksShopItem = 1203, CameraItem = 1413;
	private const int DrinksShop = 16, DrinksScript = 6, DrinksSlot = 115;
	private const uint ShippedClock = 114374804;

	private BaseFileSystem data = null!;
	private byte[] payload = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;
	private readonly List<Entity> made = [];

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
		Unimplemented.Forget();
	}

	private int ChannelsFor( int id ) => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1;

	private ParkThingStates Models( ParkWorld world ) => world.ThingStates( ChannelsFor );

	private ParkRides Bind( ParkWorld world )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

	private static int Times( string gap ) => Unimplemented.Summary.Where( counted => counted.What == gap ).Sum( counted => counted.Times );

	private static WrittenScript AsWritten( SavedScript saved ) => new( saved.Handle, saved.Position, saved.CallIndex,
		saved.HeapIndex, saved.Result, saved.Stack, saved.Variables, saved.WaitDeadline, saved.AnimationDeadline,
		saved.LoopingKey, saved.AnimationMark, saved.TimerDeadline, saved.Limbo, saved.InLimbo, saved.Bounce,
		saved.Bouncing, saved.BounceBase, saved.BounceNode, saved.Walk, saved.Heads );

	/// <summary>The things as the file holds them, with <paramref name="bought"/> made and <paramref name="gone"/> taken out.</summary>
	private ParkFileWriter.RunningThings Things( IReadOnlyList<ParkFileWriter.MadeThing>? bought = null, HashSet<int>? gone = null,
		Dictionary<int, (int Standing, uint FirstBuilt)>? built = null )
	{
		gone ??= [];

		var goneHandles = gone.SelectMany( shipped.ScriptStates.HandlesOf ).ToHashSet();
		var goneSlots = shipped.Objects.Where( thing => gone.Contains( thing.ThingId ) ).Select( thing => thing.MeshInstance - 1 ).ToHashSet();

		return new( [.. shipped.Objects.Where( thing => !gone.Contains( thing.ThingId ) )],
			shipped.ScriptStates.Tick, shipped.ScriptStates.NextHandle + (bought?.Count ?? 0),
			[.. shipped.ScriptStates.Order.Where( handle => !goneHandles.Contains( handle ) ).Select( handle => AsWritten( shipped.ScriptStates.For( handle )!.Value ) )],
			Models( shipped ),
			[.. Models( shipped ).Things.Where( thing => !goneSlots.Contains( thing.Slot ) ).Select( thing => new WrittenModel( thing.Slot, thing.Channels, thing.HoardingFlags, thing.HoardingProgress ) )],
			ShippedClock, bought, gone, built );
	}

	private ParkFileWriter.Running Running( ParkFileWriter.RunningThings things, bool people = true ) => new( shipped.GameTick,
		shipped.ParkClosed != 0, shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value,
		People: people ? new ParkPeople( shipped ).Written( id => true ) : null, Things: things );

	private ParkWorld Written( ParkFileWriter.RunningThings things, out byte[] body )
	{
		body = ParkFileWriter.Body( shipped, Running( things ), out _, out var done );

		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer && written.ClosedOnSpriteTrailer, "the list and the sprite table close" );
		Assert.IsNull( written.ScriptStates.Problem, "the scripts read" );
		Assert.IsNull( Models( written ).Problem, "the models read" );
		Assert.IsNotNull( done );

		return written;
	}

	/// <summary>A Crazy Ape made by hand: thing <paramref name="id"/> on (41,22), closed, with a script of handle <paramref name="handle"/>.</summary>
	private ParkFileWriter.MadeThing Ape( int id = 43, int handle = 16, int x = 41, int y = 22 )
	{
		Assert.IsTrue( catalogue.TryGet( CrazyApe, out var item ) );

		var placed = ParkBuilding.Constructed( item, id, x, y, 0, MapStep.CellId( x + 1, y ), MapStep.CellId( x + 2, y + 3 ),
			new ParkWorld.BuiltWhen( 2000, 2, 10, 4, 2, 30, 7, 9 ) ) with
		{
			CanLoad = 0,
			BackOfQueue = (ushort)MapStep.CellId( x + 1, y - 1 ),
			QueueSizeInCells = 1,
			TotalTakings = 12,
			Rings = new ParkObjectRings().Written(),
		};

		var script = new WrittenScript( handle, 26, 19, 0, 1, [.. Enumerable.Repeat( -1, 20 )], [0, 0, 8, 3, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
			ShippedClock + 500, 0, 0xffff, 0, 0, [], 0, [], 0, 0, 1, [], new int[16] );

		var bytes = data.ReadAllBytes( ParkRides.ScriptPathFor( item ) );
		var file = new RideScriptFile( new MemoryStream( bytes ) );

		return new( new ParkWorld.MadeObject( placed, "Crazy", "Ape" ), item.Width, item.Depth,
			new MadeScript( script, id, file.Body, file.StringBlob, 0, file.TimeSlice, 60, "data\\levels\\jungle\\rides\\monkey\\" ),
			[new SavedChannel( 0, 0, 0x14, 1f, ShippedClock - 9000, ShippedClock - 1000, ShippedClock, ParkThingStates.NoRole, 0, 0, 1f )],
			0x9, 1f );
	}

	private static int Find( byte[] bytes, byte[] run ) => bytes.AsSpan().IndexOf( run );

	private static int Int( byte[] bytes, int at ) => BinaryPrimitives.ReadInt32LittleEndian( bytes.AsSpan( at, 4 ) );

	private static int Short( byte[] bytes, int at ) => BinaryPrimitives.ReadUInt16LittleEndian( bytes.AsSpan( at, 2 ) );

	/// <summary>With nothing made and nothing gone the body is the one the stage before wrote.</summary>
	[TestMethod]
	public void WithNothingBoughtOrSoldNoByteChanges()
	{
		var plain = ParkFileWriter.Body( shipped, Running( Things() ) );
		var empty = ParkFileWriter.Body( shipped, Running( Things( [], [] ) ) );

		CollectionAssert.AreEqual( plain, empty );
		CollectionAssert.AreEqual( plain, shipped.ScriptStates.Splice( plain, [], [] ), "the scripts spliced with nothing" );
		CollectionAssert.AreEqual( plain, Models( shipped ).Splice( plain, [], [] ), "the models spliced with nothing" );
	}

	/// <summary>
	/// A made object's record, byte by byte where the page puts each field: the middle of its anchor cell, its item
	/// and date, the model handle, its flags, both lines of its name, its script, <c>mState</c>, <c>mTopLeft</c>, its
	/// ends, its link, the door, six rings of thirty days, the running fields and the three floats.
	/// </summary>
	[TestMethod]
	public void AMadeObjectsRecordIsWrittenWhole()
	{
		var written = Written( Things( [Ape()] ), out _ );
		var record = written.RecordOf( 43 )!;

		Assert.AreEqual( 1099, record.Length );
		Assert.AreEqual( (42, 3), (Int( record, 0 ), Int( record, 4 )), "ahead of the file's newest thing, a catalogue object" );
		Assert.AreEqual( ((41 << 8) | 0x80, (22 << 8) | 0x80), (Short( record, 8 ), Short( record, 10 )), "the middle of its cell" );
		Assert.AreEqual( (0, CrazyApe), (Int( record, 16 ), Short( record, 20 )) );
		CollectionAssert.AreEqual( new[] { 2000, 2, 10, 4, 2, 30, 7, 9 }, Enumerable.Range( 0, 8 ).Select( i => Int( record, 22 + (i * 4) ) ).ToArray() );
		Assert.AreEqual( 91, Int( record, 54 ), "the lowest empty slot of the model table, plus one" );
		Assert.AreEqual( 0xc, Short( record, 58 ), "may be offered, and has a queue" );

		var lineA = new string( [.. Enumerable.Range( 0, 33 ).Select( i => (char)Short( record, 60 + (i * 4) ) )] ).TrimEnd( '\0' );
		var lineB = new string( [.. Enumerable.Range( 0, 33 ).Select( i => (char)Short( record, 62 + (i * 4) ) )] ).TrimEnd( '\0' );

		Assert.AreEqual( ("Crazy", "Ape"), (lineA, lineB) );
		Assert.AreEqual( (16, 0, 0), (Int( record, 192 ), Int( record, 196 ), Int( record, 200 )), "its script, no track ride, mState nought" );
		Assert.AreEqual( (2858, 2859, 3244), (Short( record, 204 ), Short( record, 206 ), Short( record, 218 )), "mTopLeft the anchor, the entry, the exit" );
		Assert.AreEqual( shipped.FirstObject, Short( record, 208 ), "mNext the file's first object" );
		Assert.AreEqual( (0, 2731, 0, 0, 1, 0), (Short( record, 210 ), Short( record, 212 ), Int( record, 214 ), Short( record, 220 ), Int( record, 222 ), Short( record, 226 )) );

		foreach ( var ring in new[] { 228, 361, 498, 635, 768, 901 } )
			Assert.AreEqual( 30, Int( record, ring + 4 ), $"the ring at {ring} holds thirty days" );

		Assert.AreEqual( (8, 3, 60), ((int)record[1034], (int)record[1035], Int( record, 1036 )) );
		Assert.AreEqual( (0, 50, 100, 0, 50, 1), (Int( record, 1042 ), Int( record, 1046 ), Int( record, 1050 ), Int( record, 1054 ), Int( record, 1058 ), Int( record, 1062 )) );
		Assert.AreEqual( (100f, 100f, 100f), (BitConverter.ToSingle( record, 1066 ), BitConverter.ToSingle( record, 1070 ), BitConverter.ToSingle( record, 1074 )) );
		Assert.AreEqual( (0, 12), (Int( record, 1086 ), Int( record, 1090 )) );

		var read = written.Objects.Single( thing => thing.ThingId == 43 );

		Assert.AreEqual( (CrazyApe, 91, 16, 0), (read.CatalogueId, read.MeshInstance, read.RideScript, read.State) );
	}

	/// <summary>A thing a guest is never offered is made on <c>mState</c> 3, and an item with one line of name leaves the second empty.</summary>
	[TestMethod]
	public void AThingNobodyIsOfferedIsMadeOnStateThree()
	{
		Assert.IsTrue( catalogue.TryGet( CameraItem, out var item ) );

		var placed = ParkBuilding.Constructed( item, 43, 46, 27, 270, MapStep.CellId( 46, 27 ), MapStep.CellId( 46, 27 ), default,
			unchecked((int)0xfffffc00) );

		var record = ParkWorld.MadeObjectRecord( new ParkWorld.MadeObject( placed with { TopLeft = 1234 }, "Security Camera", "" ), 7 );

		Assert.AreEqual( 3, Int( record, 200 ) );
		Assert.AreEqual( (270, unchecked((int)0xfffffc00)), (Int( record, 16 ), Int( record, 196 )), "its angle, and a track ride's handle" );
		Assert.AreEqual( 1234, Short( record, 204 ), "a top left the record names is kept" );
		Assert.AreEqual( ('S', 0), ((char)Short( record, 60 ), Short( record, 62 )) );
		Assert.AreEqual( 'a', (char)Short( record, 60 + (14 * 4) ) );
		Assert.AreEqual( 0, Short( record, 60 + (15 * 4) ), "and its terminator" );
	}

	/// <summary>A name of more than thirty-two characters is cut, so the thirty-third is its terminator.</summary>
	[TestMethod]
	public void ANameIsCutToItsLine()
	{
		Assert.IsTrue( catalogue.TryGet( CameraItem, out var item ) );

		var placed = ParkBuilding.Constructed( item, 43, 46, 27, 0, 1, 1, default );
		var record = ParkWorld.MadeObjectRecord( new ParkWorld.MadeObject( placed, new string( 'x', 40 ), new string( 'y', 40 ) ), 7 );

		Assert.AreEqual( ('x', 'y'), ((char)Short( record, 60 + (31 * 4) ), (char)Short( record, 62 + (31 * 4) )) );
		Assert.AreEqual( 0, Int( record, 60 + (32 * 4) ), "the thirty-third pair is the two terminators" );
		Assert.AreEqual( 0, Int( record, 192 ), "and nothing runs on into the script handle" );
	}

	/// <summary>The thing list, the object list, set 0xb and the anchor cell's chain take a made object; the object goes behind whoever stands on its cell.</summary>
	[TestMethod]
	public void AMadeObjectJoinsTheListsAndItsCell()
	{
		// Guest 33 stands on (48,27) in the shipped park: an object made there goes behind them.
		var guest = shipped.People.First( person => person.Model == ParkWorld.GuestModel );
		var (x, y) = (guest.RawX >> 8, guest.RawY >> 8);
		var written = Written( Things( [Ape( 43, 16, x, y ), Ape( 50, 17, 20, 20 )] ), out _ );

		CollectionAssert.AreEqual( new[] { 50, 43, 42 }, written.Things.Take( 3 ).Select( thing => thing.ThingId ).ToArray(), "the made, newest first, then the file's" );
		Assert.AreEqual( 50, written.FirstObject );
		Assert.AreEqual( (43, shipped.FirstObject), ((int)written.Objects.Single( thing => thing.ThingId == 50 ).NextObject, (int)written.Objects.Single( thing => thing.ThingId == 43 ).NextObject) );

		var chain = written.ChainAt( x, y );

		Assert.AreEqual( 43, chain[^1], "behind the people" );
		CollectionAssert.Contains( chain.ToList(), guest.ThingId );
		CollectionAssert.AreEqual( new[] { 50 }, written.ChainAt( 20, 20 ).ToArray() );

		var set = written.MessageSets()![0xb];

		CollectionAssert.AreEqual( shipped.MessageSets()![0xb].Concat( [43, 50] ).ToArray(), set.ToArray(), "the day's change, in rising id" );
		CollectionAssert.AreEqual( shipped.MessageSets()![0xa].ToArray(), written.MessageSets()![0xa].ToArray(), "and no other set" );
	}

	/// <summary>An object gone is out of the thing list, the object list, its cell's chain and every set; the things either side of it are linked.</summary>
	[TestMethod]
	public void AGoneObjectLeavesTheListsAndItsCell()
	{
		var written = Written( Things( gone: [DrinksShop] ), out _ );

		Assert.IsFalse( written.Things.Any( thing => thing.ThingId == DrinksShop ) );
		Assert.AreEqual( shipped.Things.Count - 1, written.Things.Count );
		Assert.AreEqual( 13, written.Objects.Count );

		var chain = new List<int>();

		for ( var id = written.FirstObject; id != 0 && chain.Count < 50; id = written.Objects.Single( thing => thing.ThingId == id ).NextObject )
			chain.Add( id );

		CollectionAssert.AreEqual( new[] { 15, 24, 23, 22, 21, 20, 19, 18, 17, 14, 13, 12, 11 }, chain.ToArray(), "the file's chain without it" );
		Assert.AreEqual( 0, written.ChainAt( 43, 30 ).Count, "nobody and nothing on its anchor cell" );
		Assert.IsFalse( written.MessageSets()!.Any( set => set.Contains( DrinksShop ) ) );
		CollectionAssert.AreEqual( shipped.MessageSets()![0xb].Where( id => id != DrinksShop ).ToArray(), written.MessageSets()![0xb].ToArray() );
	}

	/// <summary>The last object of the chain gone leaves the one before it the last; the first gone moves the head.</summary>
	[TestMethod]
	public void TheObjectListClosesOverEitherEnd()
	{
		var written = Written( Things( gone: [11, 15] ), out _ );

		Assert.AreEqual( 24, written.FirstObject );
		Assert.AreEqual( 0, written.Objects.Single( thing => thing.ThingId == 12 ).NextObject );
	}

	/// <summary>A made script's record: first in the module, the struct as the loader fills it with the running state laid over, and its blocks.</summary>
	[TestMethod]
	public void AMadeScriptsRecordIsWrittenWhole()
	{
		var ape = Ape();
		var written = Written( Things( [ape] ), out var body );
		var scripts = written.ScriptStates;

		Assert.AreEqual( 16, scripts.Order[0], "the newest first" );
		Assert.AreEqual( shipped.ScriptStates.Declared + 1, scripts.Declared );
		Assert.AreEqual( 17, scripts.NextHandle );

		var saved = scripts.For( 16 )!.Value;

		Assert.AreEqual( (26, 19, 0, 1), (saved.Position, saved.CallIndex, saved.HeapIndex, saved.Result) );
		Assert.AreEqual( (43, 91), ((int)saved.Thing, saved.ModelHandle), "its thing, and its thing's model" );
		Assert.AreEqual( ShippedClock + 500, saved.WaitDeadline );
		Assert.AreEqual( 0xffff, saved.LoopingKey );
		Assert.AreEqual( (20, 16, 16), (saved.Stack.Length, saved.Variables.Length, saved.Heads!.Length) );
		Assert.AreEqual( (8, 3), (saved.Variables[2], saved.Variables[3]) );
		Assert.AreEqual( ape.Script!.Body.Length / 4, saved.BodyWords );

		// The struct, where the page puts each field the loader sets.
		var at = body.AsSpan().IndexOf( "RYLFRSSE"u8 ) + 8;

		at += 4 + Int( body, at ) + 20 + 8;

		Assert.AreEqual( (0, 0, 16), (Int( body, at ), Int( body, at + 4 ), Int( body, at + 8 )), "no addresses, and the handle" );
		Assert.AreEqual( (16, 436, 20), (Int( body, at + 0x4c ), Int( body, at + 0x50 ), Int( body, at + 0x54 )), "head slots, body words, stack" );
		Assert.AreEqual( (0, 0, 0), (Int( body, at + 0x58 ), Int( body, at + 0x64 ), Int( body, at + 0x7c )), "no limbo, bounce or walk slots" );
		Assert.AreEqual( (1, 0), (Int( body, at + 0x70 ), Int( body, at + 0x74 )), "the node base, and the name's offset" );
		Assert.AreEqual( 0x03e803e8, Int( body, at + 0x88 ) );
		Assert.AreEqual( (16, ape.Script.Strings.Length, 50, -1), (Int( body, at + 0x8c ), Int( body, at + 0x90 ), Int( body, at + 0x94 ), Int( body, at + 0x98 )) );
		Assert.AreEqual( (60, -1), (Int( body, at + 0xc0 ), Int( body, at + 0xd8 )), "the speed word" );
		Assert.AreEqual( unchecked((int)0xffff03e8), Int( body, at + 0xe4 ) );

		// The blocks: the body, the stack, the variables, the strings, limbo, bounce, walk, heads, the directory, the guard.
		var block = at + scripts.StructSize;

		Assert.AreEqual( ape.Script.Body.Length, Int( body, block ) );
		CollectionAssert.AreEqual( ape.Script.Body, body.AsSpan( block + 4, ape.Script.Body.Length ).ToArray() );
		block += 4 + ape.Script.Body.Length;
		Assert.AreEqual( 80, Int( body, block ) );
		block += 4 + 80;
		Assert.AreEqual( 64, Int( body, block ) );
		block += 4 + 64;
		CollectionAssert.AreEqual( ape.Script.Strings, body.AsSpan( block + 4, Int( body, block ) ).ToArray() );
		block += 4 + ape.Script.Strings.Length;
		Assert.AreEqual( (0, 0, 0, 64), (Int( body, block ), Int( body, block + 4 ), Int( body, block + 8 ), Int( body, block + 12 )), "limbo, bounce, walk, heads" );
		block += 16 + 64;

		var directory = "data\\levels\\jungle\\rides\\monkey\\\0";

		Assert.AreEqual( directory.Length, Int( body, block ) );
		Assert.AreEqual( directory, Encoding.ASCII.GetString( body, block + 4, directory.Length ) );
		block += 4 + directory.Length;
		Assert.AreEqual( "OBJ ", Encoding.ASCII.GetString( body, block, 4 ) );
		Assert.AreEqual( (0, 28), (Int( body, block + 4 ), Int( body, block + 8 )) );
		Assert.AreEqual( 15, Int( body, block + 12 + 8 ), "then the file's newest record" );
	}

	/// <summary>A made script with riders: its limbo, bounce and walk slots go out as a kept script's do.</summary>
	[TestMethod]
	public void AMadeScriptsSlotsAreWritten()
	{
		var ape = Ape();

		var script = ape.Script!.Script with
		{
			Limbo = [new( 31, 5000 ), default], InLimbo = 1,
			Bounce = [default, new( 32, 2, 7000, 6000 )], Bouncing = 1, BounceBase = 8,
			Walk = [new( 1, 2, 3, 4, 100, 200, 33, 5, 2, 6 ), default],
			Heads = [0, 34],
		};

		var record = ParkScriptStates.MadeRecord( ape.Script with { Script = script, NameAt = -1 }, 91 );
		var written = Written( Things( [ape with { Script = ape.Script with { Script = script, NameAt = -1 } }] ), out _ );
		var saved = written.ScriptStates.For( 16 )!.Value;

		Assert.AreEqual( -1, Int( record, 0x74 ), "not named yet" );
		Assert.AreEqual( (2, 1), (Int( record, 0x58 ), Int( record, 0x60 )) );
		Assert.AreEqual( (2, 0x00080001), (Int( record, 0x64 ), Int( record, 0x6c )) );
		Assert.AreEqual( (2, 2), (Int( record, 0x7c ), Int( record, 0x4c )) );
		CollectionAssert.AreEqual( new SavedLimboSlot[] { new( 31, 5000 ), default }, saved.Limbo );
		CollectionAssert.AreEqual( new SavedBounceSlot[] { default, new( 32, 2, 7000, 6000 ) }, saved.Bounce );
		Assert.AreEqual( new SavedWalkSlot( 1, 2, 3, 4, 100, 200, 33, 5, 2, 6 ), saved.Walk![0] );
		Assert.AreEqual( default, saved.Walk[1] );
		CollectionAssert.AreEqual( new[] { 0, 34 }, saved.Heads );
		Assert.AreEqual( (1, 1, 8), (saved.InLimbo, saved.Bouncing, saved.BounceBase) );
	}

	/// <summary>A gone object's script records are taken out, its own and any it started, and the count follows.</summary>
	[TestMethod]
	public void AGoneObjectsScriptIsTakenOut()
	{
		var written = Written( Things( gone: [DrinksShop] ), out _ );

		Assert.IsNull( written.ScriptStates.For( DrinksScript ) );
		Assert.AreEqual( shipped.ScriptStates.Declared - 1, written.ScriptStates.Declared );
		CollectionAssert.AreEqual( shipped.ScriptStates.Order.Where( handle => handle != DrinksScript ).ToArray(), written.ScriptStates.Order.ToArray() );
		CollectionAssert.AreEqual( new[] { DrinksScript }, shipped.ScriptStates.HandlesOf( DrinksShop ).ToArray() );
		var (was, after) = (shipped.ScriptStates.For( 7 )!.Value, written.ScriptStates.For( 7 )!.Value);

		Assert.AreEqual( (was.Position, was.BodyWords, (int)was.Thing, was.ModelHandle), (after.Position, after.BodyWords, (int)after.Thing, after.ModelHandle), "the record after it reads as it did" );
		CollectionAssert.AreEqual( was.Variables, after.Variables );
	}

	/// <summary>A made model's record: its item, cell, footprint, the placer's flags, its script, the hoarding, its angle, no tables, its channels.</summary>
	[TestMethod]
	public void AMadeModelsRecordIsWrittenWhole()
	{
		var record = ParkThingStates.MadeRecord( CrazyApe, 41, 22, 4, 5, 16, 0x89, 0.5f, 90,
			[new SavedChannel( 2, 3, 0x14, 1.5f, 100, 200, 300, 4, 5, 6, 2.5f ), new SavedChannel( ParkThingStates.NoRole, 0, 0, 1f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 1f )] );

		Assert.AreEqual( 0x37 + 88, record.Length );
		Assert.AreEqual( 1, record[0] );
		Assert.AreEqual( (CrazyApe, 41, 22, 4, 5), (Int( record, 1 ), Int( record, 5 ), Int( record, 9 ), Int( record, 0xd ), Int( record, 0x11 )) );
		Assert.AreEqual( (0x32f, 16, 0x9), (Int( record, 0x15 ), Int( record, 0x19 ), Int( record, 0x1d )), "the hoarding's seven bits alone" );
		Assert.AreEqual( (0, 0.5f, 90), (Short( record, 0x21 ), BitConverter.ToSingle( record, 0x23 ), Int( record, 0x27 )) );
		Assert.AreEqual( (0, 0, 0, 0), (Short( record, 0x2b ), Short( record, 0x2d ), Int( record, 0x2f ), Int( record, 0x33 )), "no node words, no lookup records" );
		CollectionAssert.AreEqual( new[] { 0x14, 2, 3, 100, 200, 300, BitConverter.SingleToInt32Bits( 1.5f ), 4, 5, 6, BitConverter.SingleToInt32Bits( 2.5f ) },
			Enumerable.Range( 0, 11 ).Select( i => Int( record, 0x37 + (i * 4) ) ).ToArray() );
		Assert.AreEqual( ParkThingStates.NoRole, Int( record, 0x37 + 44 + 4 ) );
	}

	/// <summary>A made model takes the slot at the cursor, the lowest empty one, and the header's three counts are kept.</summary>
	[TestMethod]
	public void AMadeModelTakesTheLowestEmptySlot()
	{
		var before = Models( shipped );

		Assert.AreEqual( (161, 7, 90), before.Header );
		CollectionAssert.AreEqual( new[] { 90, 91 }, before.Plan( [], 2 ) );
		CollectionAssert.AreEqual( new[] { 5, 90 }, before.Plan( [5], 2 ), "a slot let go is the lowest" );
		CollectionAssert.AreEqual( new[] { 90, 91, 92, 93, 94, 95, 96, 168, 169 }, before.Plan( [], 9 ), "past the last where none is empty" );

		var written = Written( Things( [Ape()] ), out var body );
		var models = Models( written );
		var record = models.Things.Single( thing => thing.Slot == 90 );

		Assert.AreEqual( (162, 6, 91), models.Header );
		Assert.AreEqual( (CrazyApe, 16, 0x9u, 1f), (record.CatalogueId, record.ScriptHandle, record.HoardingFlags, record.HoardingProgress) );

		// The record's cell is the thing's, across then down.
		var at = written.RecordOf( 43 ) is not null ? Find( body, [1, .. BitConverter.GetBytes( CrazyApe ), .. BitConverter.GetBytes( 41 ), .. BitConverter.GetBytes( 22 ), .. BitConverter.GetBytes( 4 ), .. BitConverter.GetBytes( 4 )] ) : -1;

		Assert.IsTrue( at > 0, "item 1101 on (41,22), four by four" );
		Assert.AreEqual( new SavedChannel( 0, 0, 0x14, 1f, ShippedClock - 9000, ShippedClock - 1000, ShippedClock, ParkThingStates.NoRole, 0, 0, 1f ), record.Channels.Single() );
		Assert.AreEqual( before.Things.Count + 1, models.Things.Count );
		Assert.AreEqual( before.Things.Single( thing => thing.Slot == 109 ), models.Things.Single( thing => thing.Slot == 109 ) with { Channels = before.Things.Single( thing => thing.Slot == 109 ).Channels }, "the Belly Bounce's record is where it was" );
	}

	/// <summary>A table with no empty slot grows by the made, and the cursor is the count.</summary>
	[TestMethod]
	public void AFullTableGrows()
	{
		var before = Models( shipped );
		var body = (byte[])payload.Clone();
		var record = ParkThingStates.MadeRecord( CameraItem, 1, 2, 1, 1, 0, 0, 0f, 0, [new SavedChannel( ParkThingStates.NoRole, 0, 0, 1f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 1f )] );
		var slots = before.Plan( [], 8 );
		var grown = new ParkWorld( before.Splice( body, [], [.. slots.Select( slot => (slot, record) )] ) );
		var models = Models( grown );

		Assert.IsNull( models.Problem );
		Assert.AreEqual( (169, 0, 169), models.Header );
		Assert.AreEqual( 169, models.Slots );
		Assert.ThrowsException<InvalidOperationException>( () => before.Splice( body, [], [(109, record)] ), "a slot taken" );
		Assert.ThrowsException<InvalidOperationException>( () => before.Splice( body, [], [(170, record)] ), "a slot past the next" );
	}

	/// <summary>A gone object's model slot is emptied, and is the cursor where it is the lowest empty one.</summary>
	[TestMethod]
	public void AGoneObjectsModelSlotIsEmptied()
	{
		var written = Written( Things( gone: [DrinksShop, 11] ), out _ );
		var models = Models( written );

		Assert.AreEqual( (159, 9, 0), models.Header, "the gates' slot nought is the lowest" );
		Assert.IsFalse( models.Things.Any( thing => thing.Slot is DrinksSlot or 0 ) );
		Assert.AreEqual( 168, models.Slots );
	}

	/// <summary>A thing sold and a thing bought in one file: the bought one's model takes the sold one's slot where it is the lowest.</summary>
	[TestMethod]
	public void ABoughtThingTakesASoldThingsSlot()
	{
		var written = Written( Things( [Ape()], [11] ), out _ );

		Assert.AreEqual( 1, written.Objects.Single( thing => thing.ThingId == 43 ).MeshInstance );
		Assert.AreEqual( 1, written.ScriptStates.For( 16 )!.Value.ModelHandle );
		Assert.AreEqual( (161, 7, 90), Models( written ).Header );
	}

	/// <summary>A thing bought and a queue cell laid in one file: the thing's model takes the lower slot, as the original's bought ride and its queue's first cell do.</summary>
	[TestMethod]
	public void ABoughtThingsModelComesBeforeAQueueCells()
	{
		var cell = (21 * ParkWorld.MapSize) + 42;
		var written = Written( Things( [Ape()] ) with { QueueCells = new Dictionary<int, ParkFileWriter.QueuePiece?> { [cell] = new( 1, 0 ) } }, out var body );
		var models = Models( written );

		Assert.AreEqual( (91, 92), (written.Objects.Single( thing => thing.ThingId == 43 ).MeshInstance, written.CellAt( 42, 21 ).MeshInstance) );
		Assert.AreEqual( (CrazyApe, 17001), (models.ItemIn( 90 ), models.ItemIn( 91 )) );
		Assert.AreEqual( (163, 5, 92), models.Header, "the original's own counts after it bought an ape" );
		Assert.IsTrue( Find( body, ParkThingStates.QueuePieceRecord( 42, 21, 1, 0 ) ) > 0 );
	}

	/// <summary>Two made, the older takes the lower slot; their script records fall newest first.</summary>
	[TestMethod]
	public void TheOlderOfTwoMadeTakesTheLowerSlot()
	{
		var written = Written( Things( [Ape( 50, 17, 20, 20 ), Ape( 43, 16 )] ), out _ );

		Assert.AreEqual( (91, 92), (written.Objects.Single( thing => thing.ThingId == 43 ).MeshInstance, written.Objects.Single( thing => thing.ThingId == 50 ).MeshInstance) );
		CollectionAssert.AreEqual( new[] { 17, 16, 15 }, written.ScriptStates.Order.Take( 3 ).ToArray() );
		Assert.AreEqual( (20, 20), (Models( written ).Things.Single( thing => thing.Slot == 91 ).CatalogueId == CrazyApe ? 20 : 0, 20) );
	}

	/// <summary>A made thing with no script names none, in its record and in its model's.</summary>
	[TestMethod]
	public void AMadeThingWithNoScriptNamesNone()
	{
		var written = Written( Things( [Ape() with { Script = null }] ), out _ );

		Assert.AreEqual( 0, written.Objects.Single( thing => thing.ThingId == 43 ).RideScript );
		Assert.AreEqual( 0, Models( written ).Things.Single( thing => thing.Slot == 90 ).ScriptHandle );
		Assert.AreEqual( shipped.ScriptStates.Declared, written.ScriptStates.Declared );
	}

	/// <summary>The object controls: each item's standing count and first-build stamp, where the file holds a control for it.</summary>
	[TestMethod]
	public void TheControlsCountWhatStands()
	{
		var built = new Dictionary<int, (int Standing, uint FirstBuilt)> { [CrazyApe] = (1, 924), [DrinksShopItem] = (0, 15), [99999] = (4, 4) };
		var written = Written( Things( [Ape()], [DrinksShop], built ), out _ );

		var ape = written.ObjectControlRecords.Single( control => control.ItemId == CrazyApe );
		var shop = written.ObjectControlRecords.Single( control => control.ItemId == DrinksShopItem );

		Assert.AreEqual( (1, 924u), (ape.Standing, ape.FirstBuilt) );
		Assert.AreEqual( (0, 15u), (shop.Standing, shop.FirstBuilt) );
		Assert.AreEqual( shipped.ObjectControlRecords.Single( control => control.ItemId == CameraItem ), written.ObjectControlRecords.Single( control => control.ItemId == CameraItem ) );
		Assert.AreEqual( shipped.ObjectControlRecords.Count, written.ObjectControlRecords.Count );
		Assert.AreEqual( shipped.ObjectControlRecords.Single( control => control.ItemId == CrazyApe ).Researched, ape.Researched, "the rest of a control is the file's" );
	}

	/// <summary>What cannot be written is refused: a made id the file holds, a gone id that is no object, a thing with no people given.</summary>
	[TestMethod]
	public void WhatCannotBeWrittenIsRefused()
	{
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( [Ape( 42 )] ) ) ), "an id a guest of the park holds" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( [Ape( 13 )] ) ) ), "an id an object of the file's holds" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( [Ape( 43 ), Ape( 43, 17 )] ) ) ), "an id twice" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( gone: [33] ) ) ), "a guest is no object" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( gone: [999] ) ) ), "nor is nothing" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( [Ape( 20000 )] ) ) ), "an id past the table" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( [Ape()] ), people: false ) ), "with no people" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkFileWriter.Body( shipped, Running( Things( gone: [DrinksShop] ), people: false ) ) );
	}

	// ---- the running park's half

	/// <summary>A thing bought by hand, as <c>ParkBuilding.Build</c> leaves one: constructed, added, its script bound and its footprint stamped.</summary>
	private ParkWorld.CatalogueObject Buy( ParkState state, ParkRides? rides, int itemId, int x, int y )
	{
		Assert.IsTrue( catalogue.TryGet( itemId, out var item ) );

		var id = state.NextThingId();

		var placed = ParkBuilding.Constructed( item, id, x, y, 0, MapStep.CellId( x + item.EntryDeltaX, y + item.EntryDeltaY ),
			MapStep.CellId( x + item.ExitDeltaX, y + item.ExitDeltaY ), ParkWorld.BuiltWhen.At( state.CalendarNow ) );

		state.AddObject( placed );
		ParkBuilding.BindOperation( state, rides, placed, item );
		ParkBuilding.Stamp( state, ParkObjects.FootprintAt( item, x, y, 0 ), x, y );
		state.EnterCell( x, y, id );

		return placed;
	}

	/// <summary>The state hands over what was bought, the newest first, and what was sold; a thing of an item that will not write is counted and left.</summary>
	[TestMethod]
	public void TheStateHandsOverWhatWasBoughtAndSold()
	{
		var state = new ParkState( shipped );
		var ape = Buy( state, null, CrazyApe, 41, 22 );
		var camera = Buy( state, null, CameraItem, 46, 27 );

		Assert.IsTrue( state.RemoveObject( DrinksShop ) );
		Assert.IsTrue( state.RemoveObject( 18 ) );

		var kept = state.WrittenObjects( shipped, id => id != CameraItem, out var bought, out var gone );

		CollectionAssert.AreEqual( new[] { ape.ThingId }, bought.Select( thing => thing.ThingId ).ToArray() );
		CollectionAssert.AreEqual( new[] { DrinksShop }, gone.ToArray() );
		Assert.AreEqual( 12, kept.Count );
		Assert.AreEqual( (1, 1), (Times( "SAVE_PARK_OBJECT_BOUGHT" ), Times( "SAVE_PARK_OBJECT_SOLD" )), "the camera bought and the camera sold" );
		Assert.IsNotNull( bought[0].Rings, "with its rings" );
		Assert.AreEqual( (100f, 100f), (bought[0].StateOfRepair, bought[0].RemainingLife), "as the constructor leaves the two" );

		Unimplemented.Forget();

		state.WrittenObjects( shipped, id => true, out bought, out gone );

		CollectionAssert.AreEqual( new[] { camera.ThingId, ape.ThingId }, bought.Select( thing => thing.ThingId ).ToArray(), "the newest first" );
		CollectionAssert.AreEquivalent( new[] { DrinksShop, 18 }, gone.ToArray() );
		Assert.AreEqual( (0, 0), (Times( "SAVE_PARK_OBJECT_BOUGHT" ), Times( "SAVE_PARK_OBJECT_SOLD" )) );

		state.WrittenObjects( shipped, null, out bought, out gone );

		Assert.AreEqual( (0, 0, 2), (bought.Count, gone.Count, Times( "SAVE_PARK_OBJECT_SOLD" )), "with no test nothing is made or taken out" );
		Assert.AreEqual( (1, (uint)state.GameTick), state.BuiltItems[CrazyApe] );
		Assert.AreEqual( 0, state.BuiltItems[DrinksShopItem].Standing );
	}

	/// <summary>A thing bought, then sold again before the save, is in neither list.</summary>
	[TestMethod]
	public void AThingBoughtAndSoldAgainIsNowhere()
	{
		var state = new ParkState( shipped );
		var ape = Buy( state, null, CrazyApe, 41, 22 );

		Assert.IsTrue( state.RemoveObject( ape.ThingId ) );

		var kept = state.WrittenObjects( shipped, id => true, out var bought, out var gone );

		Assert.AreEqual( (14, 0, 0), (kept.Count, bought.Count, gone.Count) );
	}

	/// <summary>A bought thing's queue is read off the map, whatever its record holds.</summary>
	[TestMethod]
	public void ABoughtThingsQueueIsMeasured()
	{
		var state = new ParkState( shipped );
		var ape = Buy( state, null, CrazyApe, 41, 22 );

		ParkBuilding.MarkWaysInAndOut( state, shipped, 42, 22, 43, 25, 0x10, 0x10, 0, hasQueue: true, MapStep.CellId( 41, 22 ) );
		state.ReplaceObject( ape with { BackOfQueue = 0, QueueSizeInCells = 0 } );

		// The entrance's link to its queue cell, as the original's placer leaves one.
		state.SetRecord( 42, 22, state.Record( 42, 22 ) with { Neighbours = 0x01 } );
		state.WrittenObjects( shipped, id => true, out var bought, out _ );

		Assert.AreEqual( (MapStep.CellId( 42, 21 ), 1), ((int)bought[0].BackOfQueue, bought[0].QueueSizeInCells) );
	}

	/// <summary>The rides hand a bought thing's script and model over whole, and count neither its script as made since the load nor a sold thing's as ended.</summary>
	[TestMethod]
	public void TheRidesHandOverABoughtThingsScriptAndModel()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );

		// The scripts of the file's that this fixture never runs are counted as ended, whatever is bought or sold.
		rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor );

		var ended = Times( "SAVE_PARK_SCRIPT_ENDED" );

		Unimplemented.Forget();

		var ape = Buy( state, rides, CrazyApe, 41, 22 );
		var handle = rides.ScriptFor( ape.ThingId );

		Assert.AreEqual( shipped.ScriptStates.NextHandle, handle );
		Assert.IsTrue( catalogue.TryGet( DrinksShopItem, out var shop ) );
		rides.Unbind( DrinksShop, shop );
		Assert.IsTrue( state.RemoveObject( DrinksShop ) );

		var kept = state.WrittenObjects( shipped, id => true, out var bought, out var gone );
		Assert.IsTrue( catalogue.TryGet( CrazyApe, out var item ) );
		Assert.IsTrue( rides.Scheduler.Find( handle )!.TakeDeclaredName(), "its script names itself, as its first turn does" );

		var things = rides.Written( shipped, kept, ChannelsFor, state.HoardingFor,
			[new ParkRides.BoughtThing( new ParkWorld.MadeObject( bought[0], "Crazy", "Ape" ), item.Width, item.Depth, "there\\" )], gone, state.BuiltItems )!;

		var thing = things.Made!.Single();

		Assert.AreEqual( (0, ended), (Times( "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" ), Times( "SAVE_PARK_SCRIPT_ENDED" )), "neither the bought thing's script nor the sold thing's" );
		Assert.AreEqual( (handle, ape.ThingId, 60, "there\\"), (thing.Script!.Script.Handle, thing.Script.Thing, thing.Script.Speed, thing.Script.Directory) );
		Assert.AreEqual( (436 * 4, 50), (thing.Script.Body.Length, thing.Script.TimeSlice) );
		Assert.AreEqual( 16, thing.Script.Script.Heads!.Length );
		Assert.AreEqual( (4, 4, 1), (thing.Across, thing.Down, thing.Channels.Count) );
		Assert.IsFalse( things.Scripts.Any( script => script.Handle == handle || script.Handle == DrinksScript ), "written whole, not over a record" );
		Assert.AreEqual( handle + 1, things.NextHandle );
		CollectionAssert.AreEqual( new[] { DrinksShop }, things.Gone!.ToArray() );
		Assert.AreEqual( state.BuiltItems, things.Built );

		Assert.IsTrue( thing.Script.NameAt >= 0, "the script had named itself, and its name's offset goes with it" );
		Assert.AreEqual( 0u, thing.HoardingFlags, "with no hoarding bound" );

		// Its hoarding is the thing's as it stands, and a model of three channels is handed three.
		var hoarding = state.BindHoarding( ape.ThingId );

		hoarding.Close();
		hoarding.Advance( 2f );

		var spray = Buy( state, rides, 1303, 30, 30 );
		Assert.IsTrue( catalogue.TryGet( 1303, out var sprayItem ) );
		state.WrittenObjects( shipped, id => true, out bought, out gone );

		var again = rides.Written( shipped, kept, ChannelsFor, state.HoardingFor,
			[.. bought.Select( placed => new ParkRides.BoughtThing( new ParkWorld.MadeObject( placed, "A", "B" ), 1, 1, "x" ) )], gone, null )!;

		Assert.AreEqual( (0xbu, 0.4f), (again.Made!.Single( placed => placed.Object.Object.ThingId == ape.ThingId ).HoardingFlags, again.Made!.Single( placed => placed.Object.Object.ThingId == ape.ThingId ).HoardingProgress) );
		Assert.AreEqual( sprayItem.AnimationChannels, again.Made!.Single( placed => placed.Object.Object.ThingId == spray.ThingId ).Channels.Count );
		Assert.AreEqual( 3, sprayItem.AnimationChannels );

		var body = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value,
			People: new ParkPeople( shipped ).Written( id => true ), Things: things ) );

		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( (ape.ThingId, 91), ((int)written.ScriptStates.For( handle )!.Value.Thing, written.ScriptStates.For( handle )!.Value.ModelHandle) );
		Assert.IsNull( written.ScriptStates.For( DrinksScript ) );
	}

	/// <summary>A script with no thing bought behind it is still counted, and a file's script that has ended with its thing standing.</summary>
	[TestMethod]
	public void OtherScriptsAreStillCounted()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );

		rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor );

		var ended = Times( "SAVE_PARK_SCRIPT_ENDED" );

		Unimplemented.Forget();
		Assert.IsTrue( catalogue.TryGet( 1100, out var item ) );
		Assert.AreNotEqual( 0, rides.Scheduler.Spawn( ParkRides.ScriptPathFor( item ) ) );
		Assert.IsTrue( catalogue.TryGet( DrinksShopItem, out var shop ) );
		rides.Unbind( DrinksShop, shop );

		rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor, [], new HashSet<int>(), null );

		Assert.AreEqual( (1, ended + 1), (Times( "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" ), Times( "SAVE_PARK_SCRIPT_ENDED" )) );
	}

	/// <summary>The cells: under a made thing's footprint on the original's tile, off a gone thing's on bare ground's, and any other footprint's still counted and left.</summary>
	[TestMethod]
	public void TheFootprintsCellsAreWritten()
	{
		var state = new ParkState( shipped );
		var ape = Buy( state, null, CrazyApe, 41, 22 );
		var camera = Buy( state, null, CameraItem, 46, 27 );

		ParkBuilding.Sell( state, shipped, catalogue, null, null, DrinksShop );
		ParkBuilding.Sell( state, shipped, catalogue, null, null, 18 );
		state.WrittenObjects( shipped, id => id != CameraItem, out var bought, out var gone );
		Unimplemented.Forget();

		var cells = Level.WrittenCells( shipped, state, bought, gone );
		var anchor = cells[(22 * 128) + 41];

		Assert.AreEqual( (CellEdge.Footprint, MapStep.CellId( 41, 22 )), ((int)anchor.Type, (int)anchor.ParentId) );
		Assert.AreEqual( (0, Level.FootprintTile, 0), ((int)anchor.TileSet, (int)anchor.TileIndex, (int)anchor.TileAngle) );
		Assert.AreEqual( 16, cells.Values.Count( cell => cell.ParentId == MapStep.CellId( 41, 22 ) && cell.Type == CellEdge.Footprint ) );

		var bare = cells[(30 * 128) + 43];

		Assert.AreEqual( (0, 0), ((int)bare.Type, (int)bare.ParentId) );
		Assert.AreEqual( (0, 55, 0), ((int)bare.TileSet, (int)bare.TileIndex, (int)bare.TileAngle), "bare ground's tile, as the sale leaves it" );
		Assert.IsFalse( cells.ContainsKey( (27 * 128) + 46 ), "the unwritten camera's cell stays the file's" );
		Assert.IsFalse( cells.ContainsKey( (29 * 128) + 55 ), "and the cell of the camera sold and left in the file" );
		Assert.AreEqual( 2, Times( "SAVE_PARK_FOOTPRINT_CELL" ) );

		Unimplemented.Forget();
		Assert.AreEqual( 0, Level.WrittenCells( shipped, state ).Values.Count( cell => cell.Type == CellEdge.Footprint && cell.ParentId == MapStep.CellId( 41, 22 ) ), "with nothing made no footprint is written" );
		Assert.IsTrue( Times( "SAVE_PARK_FOOTPRINT_CELL" ) >= 20 );

		var things = Level.WrittenThings( shipped, bought, gone );

		Assert.IsTrue( things.Contains( ape.ThingId ) && !things.Contains( DrinksShop ) && !things.Contains( camera.ThingId ) && things.Contains( 18 ) );
	}

	/// <summary>The two lines of an item's name are the executable's rows of the game's own table.</summary>
	[TestMethod]
	public void AnItemsNameIsItsRowsOfTheTable()
	{
		var names = new StringFile( ParkObjectNames.TablePath );

		Assert.AreEqual( ("Crazy", "Ape"), ParkObjectNames.Lines( CrazyApe, names ) );
		Assert.AreEqual( ("Belly", "Bounce"), ParkObjectNames.Lines( 1100, names ) );
		Assert.AreEqual( ("Drinks Shop", ""), ParkObjectNames.Lines( DrinksShopItem, names ) );
		Assert.AreEqual( ("Security Camera", ""), ParkObjectNames.Lines( CameraItem, names ) );
		Assert.AreEqual( ("LOST", "Kingdom"), ParkObjectNames.Lines( 1601, names ) );
		Assert.AreEqual( (2, 3), ParkObjectNames.RowsOf( 1150 ) );
		Assert.AreEqual( (371, 1), ParkObjectNames.RowsOf( 3602 ), "the table's last row" );
		Assert.IsNull( ParkObjectNames.RowsOf( 17000 ) );
		Assert.IsNull( ParkObjectNames.Lines( 17000, names ) );
		Assert.IsNull( ParkObjectNames.Lines( CrazyApe, null ) );
		Assert.IsNull( ParkObjectNames.Lines( 3602, new StringFile( "Language/English/LOANNAMES.str" ) ), "a row the table handed does not hold" );
		Assert.IsNotNull( ParkObjectNames.Lines( 3602, names ) );

		// A table of 21 rows holds the Aztec Mayhem's first line's row, 20, and not its second's, 21.
		var short21 = new StringFile( "Language/English/FEMALE_NAMES.str" );

		Assert.AreEqual( (21, (20, 21)), (short21.Entries.Length, ParkObjectNames.RowsOf( 1104 )!.Value) );
		Assert.IsNull( ParkObjectNames.Lines( 1104, short21 ) );

		// Every object of the shipped park wears its item's two lines.
		foreach ( var thing in shipped.Objects )
		{
			var record = shipped.RecordOf( thing.ThingId )!;
			var lineA = new string( [.. Enumerable.Range( 0, 33 ).Select( i => (char)Short( record, 60 + (i * 4) ) ).TakeWhile( letter => letter != 0 )] );
			var lineB = new string( [.. Enumerable.Range( 0, 33 ).Select( i => (char)Short( record, 62 + (i * 4) ) ).TakeWhile( letter => letter != 0 )] );

			Assert.AreEqual( (lineA, lineB), ParkObjectNames.Lines( thing.CatalogueId, names ), $"thing {thing.ThingId}, item {thing.CatalogueId}" );
		}
	}

	/// <summary>The script file keeps its body and its string blob as the file holds them.</summary>
	[TestMethod]
	public void TheScriptFileKeepsItsRawBlocks()
	{
		Assert.IsTrue( catalogue.TryGet( 1100, out var item ) );

		var file = new RideScriptFile( new MemoryStream( data.ReadAllBytes( ParkRides.ScriptPathFor( item ) ) ) );
		var saved = shipped.ScriptStates.For( 3 )!.Value;

		Assert.AreEqual( saved.BodyWords * 4, file.Body.Length );

		// The shipped park's own record of the Belly Bounce's script carries the same body and blob.
		var at = payload.AsSpan().IndexOf( file.Body );

		Assert.IsTrue( at > 0, "the body is in the park file, byte for byte" );
		Assert.IsTrue( payload.AsSpan().IndexOf( file.StringBlob ) > at, "and the blob after it" );
	}
}
