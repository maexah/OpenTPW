using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, the things the file holds: each object's record, each script's and each model's written
/// over from the running park, under the clock moved on (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the objects").
/// </summary>
[TestClass]
public class ParkFileWriterThingsTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>The Belly Bounce, its script and the slot of its model's record in the shipped park.</summary>
	private const int BellyBounce = 13;
	private const int BouncyScript = 3;
	private const int BouncyModelSlot = 109;

	/// <summary>The Jungle Spray, whose script declares three walk slots, and the Drinks Shop, whose declares limbo.</summary>
	private const int SprayScript = 4;

	/// <summary>The shipped park's clock, and its second stopwatch.</summary>
	private const uint ShippedClock = 114374804;
	private const uint ShippedSecondClock = 114876286;

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

	private ParkFileWriter.Running Carried => new( shipped.GameTick, shipped.ParkClosed != 0,
		shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value );

	private ParkRides Bind( ParkWorld world )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

	private static WrittenScript AsWritten( SavedScript saved ) => new( saved.Handle, saved.Position, saved.CallIndex,
		saved.HeapIndex, saved.Result, saved.Stack, saved.Variables, saved.WaitDeadline, saved.AnimationDeadline,
		saved.LoopingKey, saved.AnimationMark, saved.TimerDeadline, saved.Limbo, saved.InLimbo, saved.Bounce,
		saved.Bouncing, saved.BounceBase, saved.BounceNode, saved.Walk, saved.Heads );

	/// <summary>The things as the file itself holds them, each record read and handed straight back.</summary>
	private ParkFileWriter.RunningThings AsTheFile() => new( shipped.Objects,
		shipped.ScriptStates.Tick, shipped.ScriptStates.NextHandle,
		[.. shipped.ScriptStates.Order.Select( handle => AsWritten( shipped.ScriptStates.For( handle )!.Value ) )],
		Models( shipped ),
		[.. Models( shipped ).Things.Select( thing => new WrittenModel( thing.Slot, thing.Channels, thing.HoardingFlags, thing.HoardingProgress ) )],
		ShippedClock );

	/// <summary>
	/// Where a script's record lies, found by the page's layout and not by the reader under test (FileFormats
	/// <c>saves.md</c>, "The ride script module"): the struct, then the first byte of the stack's, the variables',
	/// limbo's, the bounce slots', the walk slots' and the head table's data.
	/// </summary>
	private (int Struct, int Stack, int Variables, int Limbo, int Bounce, int Walk, int Heads) PlaceOf( byte[] body, int handle )
	{
		var at = body.AsSpan().IndexOf( "RYLFRSSE"u8 ) + 8;
		var header = Int( body, at );

		at += 4 + header + 20;

		var count = Int( body, at );
		var size = Int( body, at + 4 );

		at += 8;

		for ( var script = 0; script < count; ++script )
		{
			var record = at;
			var places = new int[5];

			at += size;
			at += 4 + Int( body, at );                       // the body

			for ( var block = 0; block < 5; ++block )           // stack, variables, strings, limbo, bounce
			{
				places[block] = at + 4;
				at += 4 + Int( body, at );
			}

			var walk = at + 4;

			at += 4 + (Int( body, at ) * 32);

			var heads = at + 4;

			at += 4 + Int( body, at );                       // the head table
			at += 4 + Int( body, at );                       // the directory

			Assert.AreEqual( "OBJ ", Encoding.ASCII.GetString( body, at, 4 ) );

			at += 12 + (Int( body, at + 4 ) * Int( body, at + 8 ));

			if ( Int( body, record + 8 ) == handle )
				return (record, places[0], places[1], places[3], places[4], walk, heads);
		}

		Assert.Fail( $"no record of script {handle}" );
		return default;
	}

	/// <summary>
	/// A copy of the shipped payload with <paramref name="slots"/> empty limbo slots given to a script's record: the
	/// block's length, its eight bytes a slot and the struct's count at <c>+0x58</c>. No shipped script declares any.
	/// </summary>
	private byte[] WithLimbo( int handle, int slots )
	{
		var place = PlaceOf( payload, handle );
		var body = new byte[payload.Length + (slots * 8)];

		Array.Copy( payload, body, place.Limbo );
		Array.Copy( payload, place.Limbo, body, place.Limbo + (slots * 8), payload.Length - place.Limbo );
		Put( body, place.Limbo - 4, slots * 8 );
		Put( body, place.Struct + 0x58, slots );

		return body;
	}

	/// <summary>The same, for <paramref name="slots"/> empty head slots: the block's length, four bytes a slot, and the struct's count at <c>+0x4c</c>.</summary>
	private byte[] WithHeads( int handle, int slots )
	{
		var place = PlaceOf( payload, handle );
		var body = new byte[payload.Length + (slots * 4)];

		Array.Copy( payload, body, place.Heads );
		Array.Copy( payload, place.Heads, body, place.Heads + (slots * 4), payload.Length - place.Heads );
		Put( body, place.Heads - 4, slots * 4 );
		Put( body, place.Struct + 0x4c, slots );

		return body;
	}

	private static int Int( byte[] body, int at ) => BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at, 4 ) );

	private static void Put( byte[] body, int at, int value ) => BinaryPrimitives.WriteInt32LittleEndian( body.AsSpan( at, 4 ), value );

	private static void Put16( byte[] body, int at, int value ) => BinaryPrimitives.WriteInt16LittleEndian( body.AsSpan( at, 2 ), (short)value );

	/// <summary>Every byte that differs between two bodies of one length.</summary>
	private static List<int> Differing( byte[] a, byte[] b )
	{
		Assert.AreEqual( a.Length, b.Length, "no record changes its length" );

		return [.. Enumerable.Range( 0, a.Length ).Where( at => a[at] != b[at] )];
	}

	// ---- the files' side ----

	/// <summary>
	/// Every record read and handed straight back changes no byte: each field is written where it is read, and
	/// nothing else is touched.
	/// </summary>
	[TestMethod]
	public void TheFilesOwnThingsWrittenBackChangeNoByte()
	{
		var body = ParkFileWriter.Body( shipped, Carried with { Things = AsTheFile() }, out _, out var things );

		CollectionAssert.AreEqual( payload, ParkFileWriter.Body( shipped, Carried ), "the control: the file carried is the file" );
		CollectionAssert.AreEqual( payload, body );
		Assert.AreEqual( new ParkFileWriter.ThingsWritten( 14, 14, 0, 161 ), things );
	}

	/// <summary>The reader takes limbo, the bounce slots and the walk slots, with the struct's counts beside them, from where the page puts them.</summary>
	[TestMethod]
	public void TheReaderTakesTheRidersTables()
	{
		var body = (byte[])payload.Clone();
		var bouncy = PlaceOf( body, BouncyScript );
		var spray = PlaceOf( body, SprayScript );

		Put16( body, bouncy.Struct + 0x6c, 2 );
		Put16( body, bouncy.Struct + 0x6e, 9 );
		Put( body, bouncy.Struct + 0x70, 3 );
		Put( body, bouncy.Struct + 0x60, 5 );

		// Bounce slot 4: a guest, a node, due and start.
		Put( body, bouncy.Bounce + (4 * 16), 31 );
		Put( body, bouncy.Bounce + (4 * 16) + 4, 7 );
		Put( body, bouncy.Bounce + (4 * 16) + 8, 1000 );
		Put( body, bouncy.Bounce + (4 * 16) + 12, 900 );

		// Walk slot 1, field by field.
		var walk = spray.Walk + 32;

		Put16( body, walk, 11 );
		Put16( body, walk + 2, 12 );
		Put16( body, walk + 4, 13 );
		Put16( body, walk + 6, 14 );
		Put( body, walk + 8, 2000 );
		Put( body, walk + 12, 2100 );
		Put( body, walk + 16, 33 );
		Put16( body, walk + 0x14, 5 );
		Put16( body, walk + 0x16, 4 );
		Put16( body, walk + 0x18, 3 );
		Put16( body, walk + 0x1a, 1 );

		var read = new ParkScriptStates( body );
		var saved = read.For( BouncyScript )!.Value;

		Assert.IsNull( read.Problem );
		Assert.AreEqual( (2, 9, 3, 5), (saved.Bouncing, saved.BounceBase, saved.BounceNode, saved.InLimbo) );
		Assert.AreEqual( 10, saved.Bounce!.Length, "Bouncy's ten slots" );
		Assert.AreEqual( new SavedBounceSlot( 31, 7, 1000, 900 ), saved.Bounce[4] );
		Assert.AreEqual( (BellyBounce, 110), (saved.Thing, saved.ModelHandle), "its thing and its model's handle" );
		Assert.AreEqual( new SavedWalkSlot( 11, 12, 13, 14, 2000, 2100, 33, 4, 3, 1 ), read.For( SprayScript )!.Value.Walk![1] );
		Assert.AreEqual( 3, read.For( SprayScript )!.Value.Walk!.Length );

		Assert.AreEqual( 0, saved.Limbo!.Length, "no shipped script declares limbo" );

		// Limbo, on a record given two slots.
		var held = WithLimbo( SprayScript, 2 );
		var limbo = PlaceOf( held, SprayScript ).Limbo;

		Put( held, limbo + 8, 35 );
		Put( held, limbo + 12, 4000 );

		var withLimbo = new ParkScriptStates( held );

		Assert.IsNull( withLimbo.Problem );
		CollectionAssert.AreEqual( new[] { default, new SavedLimboSlot( 35, 4000 ) }, withLimbo.For( SprayScript )!.Value.Limbo );
		Assert.AreEqual( 3, withLimbo.For( SprayScript )!.Value.Walk!.Length, "and the walk slots after it still read" );
	}

	/// <summary>A script's every field lands where the page puts it, and the record's other bytes stay.</summary>
	[TestMethod]
	public void AScriptIsWrittenFieldByField()
	{
		var saved = shipped.ScriptStates.For( BouncyScript )!.Value;
		var bounce = (SavedBounceSlot[])saved.Bounce!.Clone();

		bounce[2] = new SavedBounceSlot( 40, 3, 9000, 8000 );

		var script = AsWritten( saved ) with
		{
			Position = 20, CallIndex = 1, HeapIndex = 1, Result = 77,
			Stack = [40, 0x20000030, 0x20000018],
			Variables = [.. Enumerable.Range( 100, saved.Variables.Length )],
			WaitDeadline = 501, AnimationDeadline = 502, LoopingKey = 0x10005, AnimationMark = 3, TimerDeadline = 503,
			Bounce = bounce, Bouncing = 1, BounceBase = 6, BounceNode = 2,
		};

		var body = (byte[])payload.Clone();
		var done = shipped.ScriptStates.Put( body, 7000, 21, [script] );
		var place = PlaceOf( body, BouncyScript );

		Assert.AreEqual( new ParkScriptStates.Written( 1, 0 ), done );
		Assert.AreEqual( (20, 1, 1, 77), (Int( body, place.Struct + 0x3c ), Int( body, place.Struct + 0x40 ), Int( body, place.Struct + 0x44 ), Int( body, place.Struct + 0x48 )) );
		Assert.AreEqual( (501, 502, 0x10005, 3, 503), (Int( body, place.Struct + 0xa0 ), Int( body, place.Struct + 0xa4 ),
			Int( body, place.Struct + 0xa8 ), Int( body, place.Struct + 0xbc ), Int( body, place.Struct + 0xc4 )) );
		Assert.AreEqual( 0x00060001, Int( body, place.Struct + 0x6c ), "bouncing below, BOUNCESETBASE's above" );
		Assert.AreEqual( 2, Int( body, place.Struct + 0x70 ) );
		Assert.AreEqual( (40, 0x20000030, 0x20000018), (Int( body, place.Stack ), Int( body, place.Stack + 4 ), Int( body, place.Stack + 8 )) );
		Assert.AreEqual( (100, 101, 113), (Int( body, place.Variables ), Int( body, place.Variables + 4 ), Int( body, place.Variables + (13 * 4) )) );
		Assert.AreEqual( (40, 3, 9000, 8000), (Int( body, place.Bounce + 32 ), Int( body, place.Bounce + 36 ), Int( body, place.Bounce + 40 ), Int( body, place.Bounce + 44 )) );

		var read = new ParkScriptStates( body );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( (7000, 21), (read.Tick, read.NextHandle), "the scheduler's tick and the next handle, in the header" );

		// The record's other bytes: the handle, the body length, the thing, the model handle and the speed word.
		Assert.AreEqual( (BouncyScript, saved.BodyWords, BellyBounce, 110, 60), (Int( body, place.Struct + 8 ), Int( body, place.Struct + 0x50 ),
			Int( body, place.Struct + 0xac ) & 0xffff, Int( body, place.Struct + 0xc8 ), Int( body, place.Struct + 0xc0 )) );

		// And every other script's record is the file's.
		foreach ( var handle in read.Order.Where( handle => handle != BouncyScript ) )
		{
			var (was, now) = (shipped.ScriptStates.For( handle )!.Value, read.For( handle )!.Value);

			Assert.AreEqual( (was.Position, was.WaitDeadline, was.Result), (now.Position, now.WaitDeadline, now.Result), $"script {handle}" );
			CollectionAssert.AreEqual( was.Variables, now.Variables, $"script {handle}" );
		}
	}

	/// <summary>A walk slot and a limbo slot in use are written whole; the facing and the last dword of a walk slot stay the file's.</summary>
	[TestMethod]
	public void AWalkSlotAndALimboSlotAreWritten()
	{
		var saved = shipped.ScriptStates.For( SprayScript )!.Value;
		var walk = (SavedWalkSlot[])saved.Walk!.Clone();

		walk[2] = new SavedWalkSlot( 21, 22, 23, 24, 3000, 3100, 36, 5, 1, 1 );

		var body = (byte[])payload.Clone();
		var at = PlaceOf( body, SprayScript ).Walk + 64;

		Put16( body, at + 0x14, 6 );
		Put( body, at + 0x1c, 0x1234 );

		shipped.ScriptStates.Put( body, 0, 0, [AsWritten( saved ) with { Walk = walk }] );

		Assert.AreEqual( (21, 22, 23, 24), (BitConverter.ToInt16( body, at ), BitConverter.ToInt16( body, at + 2 ), BitConverter.ToInt16( body, at + 4 ), BitConverter.ToInt16( body, at + 6 )) );
		Assert.AreEqual( (3000, 3100, 36), (Int( body, at + 8 ), Int( body, at + 12 ), Int( body, at + 16 )) );
		Assert.AreEqual( (6, 5, 1, 1), (BitConverter.ToInt16( body, at + 0x14 ), BitConverter.ToInt16( body, at + 0x16 ), BitConverter.ToInt16( body, at + 0x18 ), BitConverter.ToInt16( body, at + 0x1a )),
			"the facing is the file's; the action, the state and the flags the slot's" );
		Assert.AreEqual( 0x1234, Int( body, at + 0x1c ), "the last dword is the file's" );

		// Limbo, over a file whose record holds two slots, the first let go with its reading left behind.
		var held = WithLimbo( SprayScript, 2 );
		var place = PlaceOf( held, SprayScript );

		Put( held, place.Limbo + 4, 777 );

		var file = new ParkScriptStates( held );
		var written = (byte[])held.Clone();

		file.Put( written, 0, 0, [AsWritten( file.For( SprayScript )!.Value ) with { Limbo = [default, new SavedLimboSlot( 37, 5000 )], InLimbo = 1 }] );

		Assert.AreEqual( (0, 777, 37, 5000), (Int( written, place.Limbo ), Int( written, place.Limbo + 4 ), Int( written, place.Limbo + 8 ), Int( written, place.Limbo + 12 )),
			"a free slot keeps its stale reading" );
		Assert.AreEqual( 1, Int( written, place.Struct + 0x60 ), "the count taken" );
	}

	/// <summary>A slot let go keeps what it held but its guest and its state, as the engine's does.</summary>
	[TestMethod]
	public void AFreeSlotKeepsItsStaleReadings()
	{
		var body = (byte[])payload.Clone();
		var bouncy = PlaceOf( body, BouncyScript );
		var spray = PlaceOf( body, SprayScript );

		// A file whose slots were used and let go.
		Put( body, bouncy.Bounce, 31 );
		Put( body, bouncy.Bounce + 4, 1 );
		Put( body, bouncy.Bounce + 8, 700 );
		Put( body, bouncy.Bounce + 12, 600 );
		Put16( body, spray.Walk, 9 );
		Put( body, spray.Walk + 8, 800 );
		Put( body, spray.Walk + 16, 32 );
		Put16( body, spray.Walk + 0x18, 2 );

		var file = new ParkWorld( body );
		var written = (byte[])body.Clone();

		file.ScriptStates.Put( written, 0, 0, [
			AsWritten( file.ScriptStates.For( BouncyScript )!.Value ) with { Bounce = new SavedBounceSlot[10] },
			AsWritten( file.ScriptStates.For( SprayScript )!.Value ) with { Walk = new SavedWalkSlot[3] }] );

		Assert.AreEqual( (0, 1, 700, 600), (Int( written, bouncy.Bounce ), Int( written, bouncy.Bounce + 4 ), Int( written, bouncy.Bounce + 8 ), Int( written, bouncy.Bounce + 12 )) );
		Assert.AreEqual( (9, 800, 0, 0), (BitConverter.ToInt16( written, spray.Walk ), Int( written, spray.Walk + 8 ), Int( written, spray.Walk + 16 ), (int)BitConverter.ToInt16( written, spray.Walk + 0x18 )) );
	}

	/// <summary>A table of another length than the file's record holds is left, and said; a script the file does not hold is not written.</summary>
	[TestMethod]
	public void ATableOfAnotherLengthIsLeft()
	{
		var saved = shipped.ScriptStates.For( BouncyScript )!.Value;
		var body = (byte[])payload.Clone();

		var done = shipped.ScriptStates.Put( body, shipped.ScriptStates.Tick, shipped.ScriptStates.NextHandle, [
			AsWritten( saved ) with { Stack = [1, 2], Variables = [1], Bounce = new SavedBounceSlot[3], Walk = null, Limbo = new SavedLimboSlot[9], Heads = [1, 2, 3] },
			AsWritten( saved ) with { Handle = 99, Position = 5 }] );

		Assert.AreEqual( new ParkScriptStates.Written( 1, 6 ), done );
		CollectionAssert.AreEqual( payload, body, "and the record's struct was the file's already" );
	}

	/// <summary>A model's channels land in the record's eleven dwords, and the hoarding in the low seven bits and the float.</summary>
	[TestMethod]
	public void AModelIsWrittenFieldByField()
	{
		var states = Models( shipped );
		var record = states.ForScript( BouncyScript )!.Value;

		Assert.AreEqual( (BouncyModelSlot, 1100), (record.Slot, record.CatalogueId), "the record that names script 3 is the Belly Bounce's" );

		var channel = new SavedChannel( Role: 5, Entry: 1, Flags: 0x11, Speed: 1.25f, StartTime: 601, Time: 602, NoPauseTime: 603,
			QueuedRole: 2, QueuedEntry: 3, QueuedFlags: 1, QueuedSpeed: 0.5f );

		var body = (byte[])payload.Clone();

		Assert.AreEqual( 1, states.Put( body, [new WrittenModel( BouncyModelSlot, [channel], 0x13, 0.4f ),
			new WrittenModel( BouncyModelSlot + 5, [channel, channel] ), new WrittenModel( 9999, [channel] )] ),
			"a record of another count of channels and a slot with none are left" );

		var read = new ParkWorld( body ).ThingStates( ChannelsFor );

		Assert.IsNull( read.Problem );
		Assert.AreEqual( channel, read.ForScript( BouncyScript )!.Value.Channels[0] );
		Assert.AreEqual( (0x13u, 0.4f), (read.ForScript( BouncyScript )!.Value.HoardingFlags, read.ForScript( BouncyScript )!.Value.HoardingProgress) );

		// One record's bytes and no more: its flags and progress, and its one channel, less than a kilobyte apart.
		var differing = Differing( payload, body );

		Assert.IsTrue( differing.Count > 20 && differing.Max() - differing.Min() < 1024, "one record's bytes" );

		foreach ( var other in states.Things.Where( thing => thing.Slot != BouncyModelSlot ) )
			Assert.AreEqual( other.Channels[0], read.Things.Single( thing => thing.Slot == other.Slot ).Channels[0] );
	}

	/// <summary>The hoarding's seven bits go under the packed flags' other bits, which stay.</summary>
	[TestMethod]
	public void TheHoardingKeepsThePackedFlagsOtherBits()
	{
		var states = Models( shipped );
		var record = states.ForScript( BouncyScript )!.Value;
		var at = FlagsOf( payload, record );
		var body = (byte[])payload.Clone();

		Put( body, at, unchecked((int)0xabcd_ef08) );

		var file = new ParkWorld( body );
		var written = (byte[])body.Clone();

		Models( file ).Put( written, [new WrittenModel( BouncyModelSlot, record.Channels, 0x45, 1f )] );

		Assert.AreEqual( unchecked((int)0xabcd_ef45), Int( written, at ) );
		Assert.AreEqual( 1f, BitConverter.ToSingle( written, at + 6 ), "the progress, six bytes on" );

		Models( file ).Put( written, [new WrittenModel( BouncyModelSlot, record.Channels )] );

		Assert.AreEqual( unchecked((int)0xabcd_ef45), Int( written, at ), "and no hoarding given leaves the record's" );
	}

	/// <summary>Where a model record's packed flags lie: the dword that changes when its hoarding does.</summary>
	private int FlagsOf( byte[] body, SavedThing record )
	{
		var probe = (byte[])body.Clone();

		new ParkWorld( body ).ThingStates( ChannelsFor ).Put( probe, [new WrittenModel( record.Slot, record.Channels, record.HoardingFlags ^ 0x7f, record.HoardingProgress )] );

		return Differing( body, probe ).Single();
	}

	/// <summary>The clock's reading is written, and the second stopwatch moved by as much.</summary>
	[TestMethod]
	public void TheClockMovesBothItsReadings()
	{
		var body = (byte[])payload.Clone();

		shipped.Clock.Put( body, ShippedClock + 5000 );

		var at = body.AsSpan().IndexOf( "SSEM"u8 ) + 4;

		Assert.AreEqual( "KOLC", Encoding.ASCII.GetString( body, at + 8, 4 ) );
		Assert.AreEqual( (ShippedClock + 5000, ShippedSecondClock + 5000), ((uint)Int( body, at ), (uint)Int( body, at + 4 )) );
		Assert.AreEqual( ShippedClock + 5000, new ParkClock( body ).Reading );
		Assert.IsTrue( Differing( payload, body ).All( offset => offset >= at && offset < at + 8 ), "the two dwords and no more" );
	}

	/// <summary>An object's running fields land where the reader reads them, its rings among them; the rest of its record stays.</summary>
	[TestMethod]
	public void AnObjectIsWrittenFieldByField()
	{
		var was = shipped.Objects.Single( thing => thing.ThingId == BellyBounce );

		ParkWorld.DayRing Ring( int seed ) => new( seed, 30, seed % 2 == 0, seed * 10, [.. Enumerable.Range( seed * 100, 30 )] );

		var now = was with
		{
			AssignedStaff = 30, BackOfQueue = 3000, CanLoad = 0, FirstInQueue = 31, PersonBeingLoaded = 32,
			OperatingCapacity = 7, OperatingDuration = 40, OperatingSpeed = 75, CostOfGoods = 11, QualityOfGoods = 12,
			ChanceOfWinning = 13, PricePerUse = 14, AmountOfSpecialIngredient = 15, QueueSizeInCells = 16,
			RemainingLife = 17.5f, StateOfRepair = 18.5f, RequestedService = 1, TimeMarkedForMaintenance = 19,
			TotalCosts = 20, TotalTakings = 21,
			Rings = new ParkWorld.ObjectRings( Ring( 1 ), Ring( 2 ), 22, Ring( 3 ), 23, Ring( 4 ), Ring( 5 ), Ring( 6 ) ),
		};

		var somebody = shipped.People[0].ThingId;
		var body = ParkFileWriter.Body( shipped, Carried with { Things = AsTheFile() with { Objects = [now, now with { ThingId = 999 }, now with { ThingId = somebody }] } }, out _, out var things );
		var read = new ParkWorld( body );
		var got = read.Objects.Single( thing => thing.ThingId == BellyBounce );

		Assert.AreEqual( 1, things!.Value.Objects, "an object the file holds no record for is not written, nor one under a person's id" );
		CollectionAssert.AreEqual( shipped.RecordOf( somebody ), read.RecordOf( somebody ), "and that person's record is untouched" );
		Assert.IsNull( read.Problem );
		Assert.AreEqual( now with { Rings = null }, got with { Rings = null } );

		foreach ( var (wanted, held) in new[] { (now.Rings!.Costs, got.Rings!.Costs), (now.Rings.Takings, got.Rings.Takings), (now.Rings.Customers, got.Rings.Customers),
			(now.Rings.WalkAways, got.Rings.WalkAways), (now.Rings.Served, got.Rings.Served), (now.Rings.Satisfaction, got.Rings.Satisfaction) } )
		{
			Assert.AreEqual( (wanted.CurrentEntry, wanted.WrappedAround, wanted.Today), (held.CurrentEntry, held.WrappedAround, held.Today) );
			CollectionAssert.AreEqual( wanted.Days.ToList(), held.Days.ToList() );
		}

		Assert.AreEqual( (22, 23), (got.Rings.NumCustomers, got.Rings.NumWalkAways) );

		foreach ( var other in shipped.Objects.Where( thing => thing.ThingId != BellyBounce ) )
			Assert.AreEqual( other with { Rings = null }, read.Objects.Single( thing => thing.ThingId == other.ThingId ) with { Rings = null } );
	}

	/// <summary>The things are refused where the file's scripts were not read whole.</summary>
	[TestMethod]
	public void ThingsAreRefusedOverAFileNotReadWhole()
	{
		var body = (byte[])payload.Clone();
		var at = body.AsSpan().IndexOf( "OBJ "u8 );

		body[at] = (byte)'X';

		var broken = new ParkWorld( body );

		Assert.IsNotNull( broken.ScriptStates.Problem );
		Assert.ThrowsException<InvalidOperationException>( () => broken.ScriptStates.Put( (byte[])body.Clone(), 0, 0, [] ) );
	}

	// ---- the running park's side ----

	private const int Rider = 42;

	private RideScriptFile ScriptOf( int thing )
	{
		Assert.IsTrue( catalogue.TryGet( shipped.Objects.Single( placed => placed.ThingId == thing ).CatalogueId, out var item ) );

		using var stream = data.OpenRead( ParkRides.ScriptPathFor( item ) );

		return new RideScriptFile( stream! );
	}

	private RideScript? Script( ParkItemCatalogue.Item item )
	{
		try
		{
			using var stream = data.OpenRead( ParkRides.ScriptPathFor( item ) );

			return stream == null ? null : new RideScript( new RideScriptFile( stream ) );
		}
		catch ( Exception )
		{
			return null;
		}
	}

	/// <summary>Bouncy with one rider aboard for <paramref name="seconds"/>, and the clock it was left on.</summary>
	private float Aboard( RideScript script, int seconds )
	{
		script.Set( "VAR_CAPACITY", 4 );
		script.Set( "VAR_DURATION", seconds );
		script.Set( "VAR_LETMEON", Rider );

		var clock = 0f;

		for ( var turn = 0; turn < 400 && script["VAR_LETMEON"] != 0; ++turn )
			script.Turn( clock += 600f );

		Assert.IsTrue( script.TryBounceNode( Rider, out _ ), "Bouncy never took the rider aboard" );

		return clock;
	}

	/// <summary>
	/// A script hands over who it carries with every reading turned onto the file's clock: the rider in the first
	/// bounce slot on node 1, due their ride's length after they got on, and the count beside them.
	/// </summary>
	[TestMethod]
	public void AScriptIsHandedOverWithItsRider()
	{
		var script = new RideScript( ScriptOf( BellyBounce ) ) { Id = BouncyScript };
		var clock = Aboard( script, seconds: 30 );
		var written = script.Written( moment => (uint)(moment + 1_000_000f) );

		Assert.AreEqual( (BouncyScript, script.Position, script.Result), (written.Handle, written.Position, written.Result) );
		Assert.AreEqual( 10, written.Bounce!.Length );
		Assert.AreEqual( (Rider, 1), (written.Bounce[0].Handle, written.Bounce[0].Node), "the first slot, on the loader's base node" );
		Assert.AreEqual( 30_000u, written.Bounce[0].Due - written.Bounce[0].Start, "VAR_DURATION seconds" );
		Assert.IsTrue( written.Bounce[0].Start is > 1_000_000 and <= 1_000_000 + 240_000, $"got on at a moment of the run, turned: {written.Bounce[0].Start}" );
		Assert.IsTrue( written.Bounce.Skip( 1 ).All( slot => slot == default ), "a free slot holds nought" );
		Assert.AreEqual( (1, 1), (written.Bouncing, written.BounceNode), "one aboard, and the base node numbers are counted from" );
		Assert.AreEqual( 8, written.BounceBase, "BOUNCESETBASE's 8" );
		Assert.AreEqual( script.WaitDeadline is { } wait ? (uint)(wait + 1_000_000f) : 0u, written.WaitDeadline );
		CollectionAssert.AreEqual( script.Stack.ToArray(), written.Stack );
		CollectionAssert.AreEqual( script.Variables.ToArray(), written.Variables, "Bouncy keeps no reading in a variable" );
		Assert.IsTrue( clock > 0f );
	}

	/// <summary>A script takes back who a save left on it, each reading moved, and lets them off when their time comes.</summary>
	[TestMethod]
	public void AScriptTakesItsRidersBack()
	{
		var first = new RideScript( ScriptOf( BellyBounce ) ) { Id = BouncyScript };
		var clock = Aboard( first, seconds: 30 );
		var written = first.Written( moment => (uint)(moment + 1_000_000f) );

		var saved = new SavedScript( BouncyScript, written.Position, 0, written.Variables, written.CallIndex, written.HeapIndex,
			written.Result, written.Stack, written.WaitDeadline, written.AnimationDeadline, written.LoopingKey, written.AnimationMark,
			written.TimerDeadline, written.Heads, written.Limbo, written.InLimbo, written.Bounce, written.Bouncing, written.BounceBase,
			written.BounceNode, written.Walk );

		var second = new RideScript( ScriptOf( BellyBounce ) ) { Id = BouncyScript };

		for ( var slot = 0; slot < saved.Variables.Length; ++slot )
			second.SeedVariable( slot, saved.Variables[slot] );

		Assert.IsTrue( second.ResumeAt( saved.Position ) );
		second.RestoreStacks( saved.Stack, saved.CallIndex, saved.HeapIndex, saved.Result );
		second.RestoreClockState( saved.WaitDeadline != 0 ? saved.WaitDeadline - 1_000_000f : null, null, saved.LoopingKey, saved.AnimationMark, 0f );

		Assert.AreEqual( 1, second.RestoreRiders( saved, reading => reading - 1_000_000f ), "one guest in its tables" );
		Assert.IsTrue( second.TryBounceNode( Rider, out var node ) );
		Assert.AreEqual( 1, node );

		var again = second.Written( moment => (uint)(moment + 1_000_000f) );

		Assert.AreEqual( written.Bounce![0], again.Bounce![0], "the slot as it was written" );
		Assert.AreEqual( (written.Bouncing, written.BounceBase, written.BounceNode), (again.Bouncing, again.BounceBase, again.BounceNode) );

		// The node base is the save's, whatever the loader set.
		var third = new RideScript( ScriptOf( BellyBounce ) );

		third.RestoreRiders( saved with { BounceNode = 3 }, reading => reading );

		Assert.AreEqual( 3, third.Written( moment => (uint)moment ).BounceNode );

		// The two run on alike: the rider comes off on the same turn of each.
		int? OffAt( RideScript script )
		{
			for ( var turn = 1; turn <= 120; ++turn )
			{
				script.Turn( clock + (turn * 600f) );

				if ( !script.TryBounceNode( Rider, out _ ) )
					return turn;
			}

			return null;
		}

		var off = OffAt( first );

		Assert.IsTrue( off is >= 45 and <= 55, $"thirty seconds of 600 ms turns, less the boarding: turn {off}" );
		Assert.AreEqual( off, OffAt( second ) );
	}

	/// <summary>A table of another length than the script declares is not the script's, and is left.</summary>
	[TestMethod]
	public void AScriptLeavesATableOfAnotherLength()
	{
		var script = new RideScript( ScriptOf( BellyBounce ) );

		var saved = new SavedScript( BouncyScript, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0,
			Bounce: [new SavedBounceSlot( Rider, 1, 500, 100 )], Bouncing: 1, BounceBase: 3, BounceNode: 5,
			Limbo: [new SavedLimboSlot( Rider, 1 )], InLimbo: 1, Walk: [new SavedWalkSlot( 1, 2, 3, 4, 5, 6, Rider, 1, 2, 1 )] );

		Assert.AreEqual( 0, script.RestoreRiders( saved, reading => reading ) );
		Assert.IsFalse( script.TryBounceNode( Rider, out _ ) );
		Assert.AreEqual( 0, script.InLimbo );
		Assert.IsFalse( script.Walking().Any() );
	}

	/// <summary>A walk slot and a limbo slot come back as a save left them, and go out again the same.</summary>
	[TestMethod]
	public void WalkAndLimboSlotsComeBackAndGoOutAgain()
	{
		var spray = new RideScript( ScriptOf( 14 ) );
		var none = spray.Written( moment => (uint)moment );
		var walk = (SavedWalkSlot[])none.Walk!.Clone();

		walk[1] = new SavedWalkSlot( 3, 4, 5, 6, 7000, 7400, Rider, 5, 2, 1 );

		var shop = catalogue.All.Select( item => Script( item ) ).First( script => script is { LimboSpace: > 0 } )!;
		var limbo = new SavedLimboSlot[shop.LimboSpace];

		limbo[0] = new SavedLimboSlot( Rider + 1, 9000 );

		SavedScript Saved( SavedWalkSlot[]? walkSlots, SavedLimboSlot[]? limboSlots, int inLimbo )
			=> new( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk: walkSlots, Limbo: limboSlots, InLimbo: inLimbo );

		Assert.AreEqual( 1, spray.RestoreRiders( Saved( walk, null, 0 ), reading => reading + 100f ) );
		Assert.AreEqual( 1, shop.RestoreRiders( Saved( null, limbo, 1 ), reading => reading + 100f ) );

		var walking = spray.Walking().Single();

		Assert.AreEqual( (1, Rider, RideScript.WalkState.Carried), (walking.Slot, walking.Handle, walking.State) );
		Assert.AreEqual( 1, shop.InLimbo );

		Assert.AreEqual( walk[1], spray.Written( moment => (uint)(moment - 100f) ).Walk![1] );
		Assert.AreEqual( limbo[0], shop.Written( moment => (uint)(moment - 100f) ).Limbo![0] );
		Assert.AreEqual( 1, shop.Written( moment => (uint)moment ).InLimbo );
	}

	/// <summary>
	/// A park loaded and written at once is the file again, byte for byte: every object, script, model and stamp
	/// goes back where it was read from.
	/// </summary>
	[TestMethod]
	public void AParkWrittenAsItWasLoadedIsTheFileAgain()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );
		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var body = ParkFileWriter.Body( shipped, Carried with { Things = things }, out _, out var done );

		// With no models stood, the eleven placed things are bound; the gates, the lights and the bus are not.
		Assert.AreEqual( new ParkFileWriter.ThingsWritten( 14, 11, 0, 11 ), done );
		Assert.AreEqual( 3, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_SCRIPT_ENDED" ).Times, "the three with no script running, left the file's and counted" );
		Assert.AreEqual( ShippedClock, things.Clock, "no time has passed" );
		Assert.AreEqual( (shipped.ScriptStates.Tick, shipped.ScriptStates.NextHandle), (things.SchedulerTick, things.NextHandle) );
		Assert.AreEqual( 7, things.Models.Sum( model => model.Channels.Count( channel => (channel.Flags & 0x14) == 0x4 ) ),
			"the bound things' held channels go out with the file's own word: the Jungle Spray's three, the bin's and the toilets'" );
		Assert.AreEqual( 0, Differing( payload, body ).Count );
	}

	/// <summary>
	/// Time passed moves the clock and every deadline with it: the cameras' waits and the Belly Bounce's keep their
	/// distance from the file's clock, which reads the load's reading and the time since.
	/// </summary>
	[TestMethod]
	public void EveryDeadlineKeepsItsDistanceFromTheClock()
	{
		Time.Paused = false;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		var rides = Bind( shipped );
		var state = new ParkState( shipped );

		// Two seconds of the game's clock, in which no script is given a turn.
		for ( var frame = 0; frame < 4; ++frame )
		{
			Time.Update( 0.5f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );
		}

		var passed = (uint)((int)(GameClock.Ticks * 31f) - rides.LoadedAt);

		Assert.AreEqual( 64u * 31u, passed, "four half-second frames are 64 ticks" );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var body = ParkFileWriter.Body( shipped, Carried with { Things = things } );
		var read = new ParkWorld( body );

		Assert.AreEqual( ShippedClock + passed, read.Clock.Reading );

		foreach ( var handle in shipped.ScriptStates.Order )
		{
			var (was, now) = (shipped.ScriptStates.For( handle )!.Value, read.ScriptStates.For( handle )!.Value);

			Assert.AreEqual( (was.WaitDeadline, was.AnimationDeadline, was.TimerDeadline), (now.WaitDeadline, now.AnimationDeadline, now.TimerDeadline),
				$"script {handle}'s deadlines are the readings they were: the clock has moved on past them, or towards them" );
		}

		// The Belly Bounce's loop: its start where it was, its clip time and third stamp the save's moment.
		var bouncy = read.ThingStates( ChannelsFor ).ForScript( BouncyScript )!.Value.Channels[0];

		Assert.AreEqual( (ShippedClock - 1376, ShippedClock + passed, ShippedClock + passed), (bouncy.StartTime, bouncy.Time, bouncy.NoPauseTime) );

		// A held channel, a toilet's: its start and its clip time where they were, its third stamp the save's moment.
		var toilet = Models( shipped ).ForScript( 11 )!.Value.Channels[0];
		var written = read.ThingStates( ChannelsFor ).ForScript( 11 )!.Value.Channels[0];

		Assert.AreEqual( 0x4, toilet.Flags & 0x6, "held in the file" );
		Assert.AreEqual( (toilet.StartTime, toilet.Time, ShippedClock + passed), (written.StartTime, written.Time, written.NoPauseTime) );

		// And a park loaded from it waits what was left: the clock moved, so each wait is that much shorter.
		var again = Bind( read );
		var camera = again.Scheduler.Find( 8 )!;

		Assert.AreEqual( 2329f - passed, camera.WaitDeadline!.Value - again.LoadedAt, "the camera's WAIT, saved 2,329 ms ahead" );
	}

	/// <summary>
	/// The file's objects as they run: the queue's head, the guest being loaded, the totals and the rings from the
	/// park, the door from its record; the queue's size measured afresh only once the queue was edited; and an
	/// object sold left out and counted.
	/// </summary>
	[TestMethod]
	public void TheObjectsAreHandedOverAsTheyRun()
	{
		var state = new ParkState( shipped );
		var bouncy = shipped.Objects.Single( placed => placed.ThingId == BellyBounce );

		state.JoinQueue( BellyBounce, 31 );
		state.JoinQueue( BellyBounce, 32 );
		state.NominateForLoading( BellyBounce, 31 );
		state.ReplaceObject( bouncy with { CanLoad = 0, AssignedStaff = 28 } );
		state.RingsFor( BellyBounce ).CountCustomer();
		state.RingsFor( BellyBounce ).CountCustomer();
		state.RingsFor( BellyBounce ).Roll();
		state.RingsFor( BellyBounce ).CountCustomer();
		state.RingsFor( BellyBounce ).Served.Today = 5;
		state.RingsFor( BellyBounce ).WalkAways.Today = 7;
		state.RingsFor( BellyBounce ).Satisfaction.Today = 9;
		Assert.IsTrue( state.RemoveObject( 16 ), "the Drinks Shop sold" );

		var objects = state.WrittenObjects( shipped );
		var got = objects.Single( placed => placed.ThingId == BellyBounce );

		Assert.AreEqual( 13, objects.Count );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_OBJECT_SOLD" ).Times );
		CollectionAssert.AreEqual( shipped.Objects.Where( placed => placed.ThingId != 16 ).Select( placed => placed.ThingId ).ToList(),
			objects.Select( placed => placed.ThingId ).ToList(), "in the file's order" );
		Assert.AreEqual( (31, 31, 0, 28), (got.FirstInQueue, got.PersonBeingLoaded, got.CanLoad, got.AssignedStaff) );
		Assert.AreEqual( (bouncy.BackOfQueue, bouncy.QueueSizeInCells), (got.BackOfQueue, got.QueueSizeInCells), "the file's pair while nobody has edited the queue" );
		Assert.AreEqual( (3, 1, 2), (got.Rings!.NumCustomers, got.Rings.Customers.Today, got.Rings.Customers.Days[got.Rings.Customers.CurrentEntry]) );
		Assert.AreEqual( 30, got.Rings.Customers.NumEntries );
		Assert.AreEqual( (7, 5, 9), (got.Rings.WalkAways.Today, got.Rings.Served.Today, got.Rings.Satisfaction.Today), "each ring its own" );

		// A pair the record holds goes out as it is, whatever the map says, until the queue is edited: then it is
		// measured off the map again.
		state.ReplaceObject( bouncy with { BackOfQueue = 1234, QueueSizeInCells = 99 } );

		var cached = state.WrittenObjects( shipped ).Single( placed => placed.ThingId == BellyBounce );

		Assert.AreEqual( (1234, 99), ((int)cached.BackOfQueue, cached.QueueSizeInCells) );

		state.InvalidateQueue( BellyBounce );

		var again = state.WrittenObjects( shipped ).Single( placed => placed.ThingId == BellyBounce );

		Assert.AreEqual( ((int)bouncy.BackOfQueue, bouncy.QueueSizeInCells), ((int)again.BackOfQueue, again.QueueSizeInCells), "the shipped queue, four cells ending on 2866, walked" );
		Assert.AreEqual( (2866, 4), ((int)again.BackOfQueue, again.QueueSizeInCells) );
	}

	/// <summary>The totals a park books are the ones written.</summary>
	[TestMethod]
	public void AnObjectsTotalsAreThePark()
	{
		var state = new ParkState( shipped );

		state.TakeAt( 14, 25 );
		state.BookCostOfGoods( 14, 7 );

		var spray = state.WrittenObjects( shipped ).Single( placed => placed.ThingId == 14 );

		Assert.AreEqual( (25, 7), (spray.TotalTakings, spray.TotalCosts) );
		Assert.AreEqual( (25, 7), (spray.Rings!.Takings.Today, spray.Rings.Costs.Today), "and today's, in their rings" );
	}

	/// <summary>A load takes the guest an object was loading from its record, with its queue's head.</summary>
	[TestMethod]
	public void ALoadTakesTheGuestBeingLoaded()
	{
		var bouncy = shipped.Objects.Single( placed => placed.ThingId == BellyBounce );
		var body = ParkFileWriter.Body( shipped, Carried with { Things = AsTheFile() with { Objects = [bouncy with { FirstInQueue = 31, PersonBeingLoaded = 31 }] } } );
		var state = new ParkState( new ParkWorld( body ) );

		Assert.AreEqual( (31, 31), (state.FirstInQueue( BellyBounce ), state.PersonBeingLoaded( BellyBounce )) );
		Assert.AreEqual( 0, state.PersonBeingLoaded( 14 ) );
	}

	/// <summary>
	/// A model's record is paired with the thing whose script it names, whatever order the records lie in: with the
	/// first two Small Toilets' records naming each other's scripts, each toilet takes the record that names its own.
	/// </summary>
	[TestMethod]
	public void AModelsRecordGoesToTheThingWhoseScriptItNames()
	{
		var states = Models( shipped );
		var (first, second) = (states.ForScript( 11 )!.Value, states.ForScript( 12 )!.Value);
		var body = (byte[])payload.Clone();

		// The script handle lies four bytes before the packed flags.
		Put( body, FlagsOf( payload, first ) - 4, 12 );
		Put( body, FlagsOf( payload, second ) - 4, 11 );

		var swapped = new ParkWorld( body );
		var read = Models( swapped );

		Assert.AreEqual( (second.Slot, first.Slot), (read.ForScript( 11 )!.Value.Slot, read.ForScript( 12 )!.Value.Slot) );
		Assert.IsNull( read.ForScript( 0 ), "nought names no thing, though most records hold it" );
		Assert.IsNull( read.ForScript( 99 ) );

		var rides = Bind( swapped );
		var toilets = swapped.Objects.Where( placed => placed.RideScript is 11 or 12 ).ToDictionary( placed => placed.RideScript, placed => placed.ThingId );

		// Each toilet's held clip stands where its own record's start stamp puts it.
		foreach ( var (script, record) in new[] { (11, second), (12, first) } )
		{
			var channel = rides.Scheduler.Find( rides.ScriptFor( toilets[script] ) )!.Animations!.Channel( 0 )!;

			Assert.AreEqual( rides.LoadedAt + unchecked((int)(record.Channels[0].StartTime - ShippedClock)), channel.StartAnimTime, $"script {script}" );
		}

		Assert.AreNotEqual( first.Channels[0].StartTime, second.Channels[0].StartTime, "the two records differ, or this proves nothing" );
	}

	/// <summary>A record of another item than the thing's is not its model's.</summary>
	[TestMethod]
	public void ARecordOfAnotherItemIsNotTheThingsModel()
	{
		var states = Models( shipped );
		var body = (byte[])payload.Clone();

		// The Fountain's record made to name the Belly Bounce's script, and the Belly Bounce's own to name nothing.
		Put( body, FlagsOf( payload, states.ForScript( BouncyScript )!.Value ) - 4, 0 );
		Put( body, FlagsOf( payload, states.ForScript( 14 )!.Value ) - 4, BouncyScript );

		var rides = Bind( new ParkWorld( body ) );
		var bouncy = rides.Scheduler.Find( rides.ScriptFor( BellyBounce ) )!;

		Assert.IsTrue( bouncy.Animations!.Channel( 0 )!.IsIdle, "no channel was put back from another item's record" );

		// Nor is its model written into that record.
		var world = new ParkWorld( body );
		var things = rides.Written( world, new ParkState( world ).WrittenObjects( world ), ChannelsFor, _ => null )!;

		Assert.IsFalse( things.Models.Any( model => model.Slot == states.ForScript( 14 )!.Value.Slot ) );
		Assert.AreEqual( 9, things.Models.Count, "the nine other bound things': no record names the Fountain's script now, nor is the Belly Bounce's its item's" );
	}

	/// <summary>A channel held on its last frame is written with its clip time a whole clip past its start, as the engine keeps it.</summary>
	[TestMethod]
	public void AHeldChannelsClipTimeIsAClipPastItsStart()
	{
		var channel = new AnimTimeControl();

		channel.Start( 2, 0, 0, 1f, now: 1000, frames: 30f );
		channel.Start( AnimTimeControl.HoldAtEnd, 0, 0, 1f, now: 5000, frames: 0f );

		Assert.AreEqual( 5000, channel.StartAnimTime );
		Assert.IsTrue( channel.HeldTime is 5999 or 6000, $"thirty frames at 30 a second past the start: {channel.HeldTime}" );
		Assert.IsTrue( channel.AnimTime < channel.StartAnimTime, "where this build's own clip time lies before it" );
	}

	/// <summary>The head table is written slot for slot, over a record given three.</summary>
	[TestMethod]
	public void TheHeadTableIsWritten()
	{
		var held = WithHeads( BouncyScript, 3 );
		var file = new ParkScriptStates( held );
		var written = (byte[])held.Clone();

		Assert.IsNull( file.Problem );
		Assert.AreEqual( 3, file.For( BouncyScript )!.Value.Heads!.Length );

		file.Put( written, 0, 0, [AsWritten( file.For( BouncyScript )!.Value ) with { Heads = [0, 41, 43] }] );

		CollectionAssert.AreEqual( new[] { 0, 41, 43 }, new ParkScriptStates( written ).For( BouncyScript )!.Value.Heads );
	}

	/// <summary>The five fields a clock or an animation keeps go out, each deadline turned onto the file's clock and an empty one nought.</summary>
	[TestMethod]
	public void AScriptsDeadlinesAreHandedOver()
	{
		var script = new RideScript( ScriptOf( BellyBounce ) );

		script.RestoreClockState( 1500f, 2500f, 0x10005, 3, 3500f );

		var written = script.Written( moment => (uint)(moment + 1_000_000f) );

		Assert.AreEqual( (1_001_500u, 1_002_500u, 0x10005, 3, 1_003_500u),
			(written.WaitDeadline, written.AnimationDeadline, written.LoopingKey, written.AnimationMark, written.TimerDeadline) );

		script.RestoreClockState( null, null, 0xffff, 0, 0f );
		written = script.Written( moment => (uint)(moment + 1_000_000f) );

		Assert.AreEqual( (0u, 0u, 0xffff, 0, 0u), (written.WaitDeadline, written.AnimationDeadline, written.LoopingKey, written.AnimationMark, written.TimerDeadline) );
	}

	/// <summary>A reading a script keeps in a variable is turned onto the file's clock with the rest, and no other variable is.</summary>
	[TestMethod]
	public void AKeptReadingIsTurnedOntoTheFilesClock()
	{
		var script = catalogue.All.Select( Script ).First( candidate => candidate is not null && RideScript.KeptReadings( FileOf( candidate ) ).Count > 0 )!;
		var kept = RideScript.KeptReadings( FileOf( script ) );

		for ( var slot = 0; slot < script.Variables.Count; ++slot )
			script.SeedVariable( slot, 5000 + slot );

		var written = script.Written( moment => (uint)(moment + 1_000_000f) );

		for ( var slot = 0; slot < script.Variables.Count; ++slot )
			Assert.AreEqual( kept.Contains( slot ) ? 1_005_000 + slot : 5000 + slot, written.Variables[slot], $"variable {slot}" );

		script.SeedVariable( kept[0], 0 );

		Assert.AreEqual( 0, script.Written( moment => (uint)(moment + 1_000_000f) ).Variables[kept[0]], "nought is a variable never written, and stays" );
	}

	private static RideScriptFile FileOf( RideScript script )
		=> (RideScriptFile)typeof( RideScript ).GetField( "_file", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!.GetValue( script )!;

	/// <summary>
	/// A park loaded from a file that holds a rider has them on the ride again: in the bounce slot and on the node the
	/// file gives, due off as long after the load as the file puts their due after its clock.
	/// </summary>
	[TestMethod]
	public void ALoadedParkTakesItsRidersBack()
	{
		var saved = shipped.ScriptStates.For( BouncyScript )!.Value;
		var bounce = (SavedBounceSlot[])saved.Bounce!.Clone();

		bounce[2] = new SavedBounceSlot( 31, 3, ShippedClock + 5000, ShippedClock - 25000 );

		var body = (byte[])payload.Clone();

		shipped.ScriptStates.Put( body, shipped.ScriptStates.Tick, shipped.ScriptStates.NextHandle, [AsWritten( saved ) with { Bounce = bounce, Bouncing = 1 }] );

		var rides = Bind( new ParkWorld( body ) );
		var bouncy = rides.Scheduler.Find( rides.ScriptFor( BellyBounce ) )!;

		Assert.AreEqual( 1, rides.RidersRestored );
		Assert.IsTrue( bouncy.TryBounceNode( 31, out var node ) );
		Assert.AreEqual( 3, node );

		var again = bouncy.Written( moment => ShippedClock + (uint)(int)(moment - rides.LoadedAt) );

		Assert.AreEqual( bounce[2], again.Bounce![2], "moved onto this clock and back, the slot is the file's" );
		Assert.AreEqual( 1, again.Bouncing );
		Assert.AreEqual( 0, Bind( shipped ).RidersRestored, "and the shipped park holds nobody" );
	}

	/// <summary>A script the file holds no record for is not written, and counted; one that has ended is left the file's, and counted.</summary>
	[TestMethod]
	public void AScriptMadeSinceTheLoadIsCountedAndLeftOut()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );

		Assert.IsTrue( catalogue.TryGet( 1100, out var item ) );

		var made = rides.Scheduler.Spawn( ParkRides.ScriptPathFor( item ) );

		Assert.AreEqual( shipped.ScriptStates.NextHandle, made, "under the next handle" );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;

		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_SCRIPT_MADE_SINCE_THE_LOAD" ).Times );
		Assert.IsFalse( things.Scripts.Any( script => script.Handle == made ) );
		Assert.AreEqual( made + 1, things.NextHandle, "and the next handle is past it" );
		Assert.AreEqual( 11, things.Scripts.Count );
	}

	/// <summary>A thing's hoarding goes out with its model: the seven bits and the progress as they stand.</summary>
	[TestMethod]
	public void AHoardingIsHandedOverWithItsModel()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );
		var hoarding = state.BindHoarding( BellyBounce );

		hoarding.Close();
		hoarding.Advance( 2f );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var model = things.Models.Single( written => written.Slot == BouncyModelSlot );

		Assert.AreEqual( (0xbu, 0.4f), (model.HoardingFlags, model.HoardingProgress) );
		Assert.IsTrue( things.Models.Where( written => written.Slot != BouncyModelSlot ).All( written => written.HoardingFlags is null ), "a thing with no hoarding leaves its record's" );

		var body = ParkFileWriter.Body( shipped, Carried with { Things = things } );
		var read = new ParkWorld( body ).ThingStates( ChannelsFor ).ForScript( BouncyScript )!.Value;

		Assert.AreEqual( (0xbu, 0.4f), (read.HoardingFlags, read.HoardingProgress) );
	}

	/// <summary>
	/// A clip started since the load goes out as the engine keeps a running one: its role, clip and speed, its start
	/// where it began, its clip time and third stamp the save's moment, the file's leftover queue behind it; and the
	/// keep-shown bit a channel here does not keep is counted where the file's is another clip's.
	/// </summary>
	[TestMethod]
	public void AClipStartedSinceTheLoadIsWrittenRunning()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );
		var bin = shipped.Objects.Single( placed => placed.RideScript == 7 );
		var players = rides.Scheduler.Find( rides.ScriptFor( bin.ThingId ) )!.Animations!;
		var file = Models( shipped ).ForScript( 7 )!.Value.Channels[0];

		Assert.AreEqual( 0xc, file.Flags, "the Litter Bin's channel, held with the keep-shown bit" );

		var role = Enumerable.Range( 0, RideAnimations.RoleCount ).First( candidate => candidate != file.Role && players.EntryCount( candidate ) > 0 );

		players.Trigger( role, 0, AnimTimeControl.LoopFlag | AnimTimeControl.StartAtOnceFlag, 1.5f, rides.LoadedAt + 400 );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var written = things.Models.Single( model => model.Slot == Models( shipped ).ForScript( 7 )!.Value.Slot ).Channels[0];

		Assert.AreEqual( (role, 0, 1.5f), (written.Role, written.Entry, written.Speed) );
		Assert.AreEqual( 0x19, written.Flags, "looping, not held; the 0x10 a hold leaves behind, as the original's files hold it on a channel running since; the file's 0x8 left" );
		Assert.AreEqual( (ShippedClock + 400, ShippedClock, ShippedClock), (written.StartTime, written.Time, written.NoPauseTime) );
		Assert.AreEqual( (ParkThingStates.NoRole, file.QueuedEntry, file.QueuedFlags, file.QueuedSpeed), (written.QueuedRole, written.QueuedEntry, written.QueuedFlags, written.QueuedSpeed) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_CHANNEL_KEEP_SHOWN_BIT" ).Times );

		// And one queued behind it goes out as queued.
		players.Channel( 0 )!.Queue( file.Role, 0, AnimTimeControl.LoopFlag, 0.75f );

		var queued = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!
			.Models.Single( model => model.Slot == Models( shipped ).ForScript( 7 )!.Value.Slot ).Channels[0];

		Assert.AreEqual( (file.Role, 0, AnimTimeControl.LoopFlag, 0.75f), (queued.QueuedRole, queued.QueuedEntry, queued.QueuedFlags, queued.QueuedSpeed) );
	}

	/// <summary>A head hung on a model's node is in the model's lookup records, which are not written: counted.</summary>
	[TestMethod]
	public void AHeadOnAModelNodeIsCounted()
	{
		var rides = Bind( shipped );
		var state = new ParkState( shipped );

		rides.Scheduler.Find( rides.ScriptFor( BellyBounce ) )!.RestoreHeads( [0, 31] );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;

		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_HEAD_ON_A_MODEL_NODE" ).Times );
		CollectionAssert.AreEqual( new[] { 0, 31 }, things.Scripts.Single( script => script.Handle == BouncyScript ).Heads );
	}

	/// <summary>With the file's clock unread no moment here has a reading there, and nothing is handed over.</summary>
	[TestMethod]
	public void WithNoClockNothingIsHandedOver()
	{
		var body = (byte[])payload.Clone();
		var at = body.AsSpan().IndexOf( "KOLC"u8 );

		body[at] = (byte)'X';

		var broken = new ParkWorld( body );

		Assert.IsNotNull( broken.Clock.Problem );
		Assert.IsNull( Bind( broken ).Written( broken, new ParkState( broken ).WrittenObjects( broken ), ChannelsFor, _ => null ) );
		Assert.ThrowsException<InvalidOperationException>( () => broken.Clock.Put( (byte[])body.Clone(), 1 ) );
	}
}
