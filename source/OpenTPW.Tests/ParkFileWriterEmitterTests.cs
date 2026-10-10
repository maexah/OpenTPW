using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park file's writer, the particles module: the live emitters follow the scripts' records
/// (<c>docs/exe/saves.md</c>, "OpenTPW's writer, an emitter started").
/// </summary>
[TestClass]
public class ParkFileWriterEmitterTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>The shipped park's Drinks Shop, its script, and its bubbles: emitter slot 20 under count 122.</summary>
	private const int DrinksScript = 6;
	private const int Bubbles = 0x7a0014;

	/// <summary>Small Toilet 21 and its script.</summary>
	private const int Toilet = 21;
	private const int ToiletScript = 11;

	/// <summary>The words of an emitter that run on after its start: its particles, its emitting countdown and the box they have reached.</summary>
	private static readonly HashSet<int> Running = [0x06, 0x08, 0x66, .. Enumerable.Range( 0, 12 ).Select( word => 0x128 + (word * 2) )];

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

	private ParkFileWriter.Running Carried => new( shipped.GameTick, shipped.ParkClosed != 0,
		shipped.NumberOfVisitorsToDate, shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value );

	private static WrittenScript AsWritten( SavedScript saved ) => new( saved.Handle, saved.Position, saved.CallIndex,
		saved.HeapIndex, saved.Result, saved.Stack, saved.Variables, saved.WaitDeadline, saved.AnimationDeadline,
		saved.LoopingKey, saved.AnimationMark, saved.TimerDeadline, saved.Limbo, saved.InLimbo, saved.Bounce,
		saved.Bouncing, saved.BounceBase, saved.BounceNode, saved.Walk, saved.Heads );

	/// <summary>The things as the file holds them, each script through <paramref name="change"/>.</summary>
	private ParkFileWriter.RunningThings Things( Func<WrittenScript, WrittenScript> change ) => Things( shipped, change );

	/// <summary><see cref="Things(Func{WrittenScript, WrittenScript})"/> of another park's file.</summary>
	private ParkFileWriter.RunningThings Things( ParkWorld world, Func<WrittenScript, WrittenScript> change )
	{
		var states = world.ThingStates( ChannelsFor );

		return new( world.Objects, world.ScriptStates.Tick, world.ScriptStates.NextHandle,
			[.. world.ScriptStates.Order.Select( handle => change( AsWritten( world.ScriptStates.For( handle )!.Value ) ) )],
			states, [.. states.Things.Select( thing => new WrittenModel( thing.Slot, thing.Channels, thing.HoardingFlags, thing.HoardingProgress ) )],
			world.Clock.Reading!.Value );
	}

	/// <summary>The shipped park's body with <paramref name="change"/> made to its live image, and where the module's magic is.</summary>
	private (byte[] Body, int At) WithLive( Action<Span<byte>> change )
	{
		var body = (byte[])payload.Clone();

		change( body.AsSpan( shipped.Particles.LiveAt, ParkParticles.LiveSize ) );

		return (body, shipped.Particles.LiveAt - 12);
	}

	/// <summary>Where emitter <paramref name="slot"/> lies in the live image.</summary>
	private static int EmitterAt( int slot ) => 0x2c + (slot * ParkParticles.EmitterSize);

	private static ParkParticles.Spawn At( int effect, int x, int height, int z ) => new( effect, x << 10, height << 10, z << 10 );

	private static int Word( ReadOnlySpan<byte> bytes, int at ) => BinaryPrimitives.ReadInt16LittleEndian( bytes[at..] );

	private static int Dword( ReadOnlySpan<byte> bytes, int at ) => BinaryPrimitives.ReadInt32LittleEndian( bytes[at..] );

	/// <summary>The 16-bit words at which two emitters differ.</summary>
	private static List<int> Differing( ReadOnlySpan<byte> a, ReadOnlySpan<byte> b )
	{
		var where = new List<int>();

		for ( var at = 0; at < ParkParticles.EmitterSize; at += 2 )
		{
			if ( a[at] != b[at] || a[at + 1] != b[at + 1] )
				where.Add( at );
		}

		return where;
	}

	/// <summary>The shipped park's module reads: four emitters on the used chain, the Drinks Shop's bubbles named by its record's handle.</summary>
	[TestMethod]
	public void TheShippedParksEmittersAreRead()
	{
		var module = shipped.Particles;

		Assert.IsNull( module.Problem );
		CollectionAssert.AreEqual( new[] { 8, 13, 20, 0 }, module.Used.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( new ParkParticles.Emitter( 20, true, 122, 58, 440 * 64, 6 * 64, 312 * 64, 0 ), module.At( 20 ) );
		Assert.AreEqual( Bubbles, module.At( 20 ).Handle );
		Assert.AreEqual( Bubbles, shipped.ScriptStates.For( DrinksScript )!.Value.Effects!.Single().Handle );
		Assert.IsFalse( module.At( 69 ).InUse );
		Assert.IsFalse( module.At( 120 ).InUse, "past the slots" );

		Assert.IsTrue( module.Names( Bubbles ) );
		Assert.IsFalse( module.Names( 0 ), "nought names none" );
		Assert.IsFalse( module.Names( Bubbles + 0x10000 ), "another count" );
		Assert.IsFalse( module.Names( 0x7a0078 ), "past the slots" );

		Assert.IsNotNull( new ParkParticles( payload, 0 ).Problem, "no module where the magic is not" );
		Assert.AreEqual( -1, new ParkParticles( payload, 0 ).LiveAt );

		// Nought names none, even where slot nought's count is nought, as a freed emitter's is.
		var (freed, at) = WithLive( live => BinaryPrimitives.WriteInt16LittleEndian( live[(EmitterAt( 0 ) + 0x0a)..], 0 ) );

		Assert.IsNull( new ParkParticles( freed, at ).Problem );
		Assert.IsFalse( new ParkParticles( freed, at ).Names( 0 ) );
		Assert.IsFalse( new ParkParticles( freed, at ).Begin().Kill( 0 ) );

		// A file saved with particles off holds neither image.
		var off = (byte[])payload.Clone();

		BinaryPrimitives.WriteInt32LittleEndian( off.AsSpan( at + 4 ), 0 );
		Assert.IsNotNull( new ParkParticles( off, at ).Problem );
	}

	/// <summary>
	/// <b>An emitter started is the original's own</b>: the shipped park's bubbles are the bytes a start of effect 58
	/// at their place makes, but for the words that run on, the count and the chain's links.
	/// </summary>
	[TestMethod]
	public void AnEmitterStartedIsTheOriginalsOwn()
	{
		var edit = shipped.Particles.Begin();
		var handle = edit.Start( At( 58, 440, 6, 312 ) );

		Assert.AreEqual( 0xda0045, handle, "the free chain's head, slot 69, under the next count, 218" );

		var differing = Differing( shipped.Particles.Bytes( 20 ), edit.Bytes( 69 ) ).Where( at => !Running.Contains( at ) ).ToArray();

		CollectionAssert.AreEqual( new[] { 0x0a, 0xd0, 0xd2 }, differing );

		var bytes = edit.Bytes( 69 );

		Assert.AreEqual( (1, -1, 0, 218, 58), (Word( bytes, 0x00 ), Word( bytes, 0x06 ), Word( bytes, 0x08 ), Word( bytes, 0x0a ), Word( bytes, 0x0c )) );
		Assert.AreEqual( (440 * 64, 6 * 64, 312 * 64), (Dword( bytes, 0x14 ), Dword( bytes, 0x18 ), Dword( bytes, 0x1c )) );
		Assert.AreEqual( 1, bytes[0x6f], "the effect has a force" );
		Assert.AreEqual( (8, -1), (Word( bytes, 0xd0 ), Word( bytes, 0xd2 )), "at the head of the used chain, the old head behind it" );

		CollectionAssert.AreEqual( new[] { int.MaxValue, int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, int.MinValue },
			Enumerable.Range( 0, 6 ).Select( word => Dword( edit.Bytes( 69 ), 0x128 + (word * 4) ) ).ToArray(), "the box, empty" );

		CollectionAssert.AreEqual( new[] { 69, 8, 13, 20, 0 }, edit.Used.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( 69, Word( edit.Bytes( 8 ), 0xd2 ), "the old head is linked back" );
		CollectionAssert.AreEqual( new[] { 8, 13, 20, 0 }, shipped.Particles.Used.Select( emitter => emitter.Slot ).ToArray(), "the file's own is not touched" );

		// A place is shifted down four bits as it stands, a fraction of a unit kept.
		var between = edit.Start( new ParkParticles.Spawn( 58, (450 << 10) + 512, (6 << 10) + 1023, -(1 << 10) ) );

		Assert.AreEqual( 0xdb006b, between, "the next of the free chain, slot 107, under the next count" );
		Assert.AreEqual( new ParkParticles.Emitter( 107, true, 219, 58, (450 * 64) + 32, (6 * 64) + 63, -64, 0 ), edit.At( 107 ) );
		Assert.AreEqual( 107, Word( edit.Bytes( 69 ), 0xd2 ) );
		Assert.AreEqual( 0xdc0001, edit.Start( At( 58, 1, 2, 3 ) ), "then slot 1" );
		CollectionAssert.AreEqual( new[] { 1, 107, 69, 8, 13, 20, 0 }, edit.Used.Select( emitter => emitter.Slot ).ToArray() );
	}

	/// <summary>
	/// An effect with no rates of its own is scaled by the file's particle density, 500 over 1024, and one with a
	/// life starts on the whole of it; an effect with its own rates is its template's.
	/// </summary>
	[TestMethod]
	public void AStartScalesByTheDensityAndStartsTheLife()
	{
		var edit = shipped.Particles.Begin();
		var stink = edit.Start( At( 9, 555, 10, 174 ) ) & 0xffff;
		var flies = edit.Start( At( 69, 555, 10, 174 ) ) & 0xffff;
		var bubbles = edit.Start( At( 58, 450, 6, 342 ) ) & 0xffff;

		Assert.AreEqual( (40, 19), (Word( shipped.Particles.Template( 9 ), 0x64 ), Word( edit.Bytes( stink ), 0x64 )) );
		Assert.AreEqual( (20, 9), (Word( shipped.Particles.Template( 69 ), 0x64 ), Word( edit.Bytes( flies ), 0x64 )) );
		Assert.AreEqual( Word( shipped.Particles.Template( 58 ), 0x64 ), Word( edit.Bytes( bubbles ), 0x64 ) );

		// Each rate byte too, and never down to nought.
		for ( var rate = 0; rate < 4; ++rate )
		{
			var was = (sbyte)shipped.Particles.Template( 9 )[0x68 + rate];
			var want = was == 0 ? 0 : Math.Max( (500 * was) >> 10, 1 );

			Assert.AreEqual( want, (sbyte)edit.Bytes( stink )[0x68 + rate], $"rate {rate} of {was}" );
		}

		Assert.AreEqual( (100000, 100000), (edit.At( stink ).Life, Dword( edit.Bytes( stink ), 0xd4 )) );
		Assert.AreEqual( (0, 0), (edit.At( flies ).Life, Dword( edit.Bytes( flies ), 0xd4 )) );

		// A burst is scaled as the most is: effect 4's twenty.
		var burst = edit.Start( At( 4, 1, 1, 1 ) ) & 0xffff;

		Assert.AreEqual( (20, 9), (Dword( shipped.Particles.Template( 4 ), 0x60 ), Dword( edit.Bytes( burst ), 0x60 )) );
	}

	/// <summary>An effect with a range draws the system's generator three times, one an axis, and adds each draw under the range to its velocity.</summary>
	[TestMethod]
	public void AStartDrawsTheSystemsGeneratorUnderTheEffectsRange()
	{
		const int ranged = 3;

		var template = shipped.Particles.Template( ranged );
		var range = Word( template, 0x12 );

		Assert.AreEqual( 10, range );

		var seed = Dword( payload.AsSpan( shipped.Particles.LiveAt ), 0x10 );
		var want = new int[3];

		for ( var axis = 0; axis < 3; ++axis )
		{
			seed = unchecked((seed * 0x343fd) + 0x269ec3);
			want[axis] = Dword( template, 0x2c + (axis * 4) ) + ((seed >> 16) % range);
		}

		var edit = shipped.Particles.Begin();
		var slot = edit.Start( At( ranged, 1, 1, 1 ) ) & 0xffff;

		CollectionAssert.AreEqual( want, Enumerable.Range( 0, 3 ).Select( axis => Dword( edit.Bytes( slot ), 0x2c + (axis * 4) ) ).ToArray() );
		Assert.IsTrue( want.Distinct().Count() > 1 || want[0] != Dword( template, 0x2c ), "the draws moved something" );

		// The generator is where the three draws left it: a second start draws on.
		var body = (byte[])payload.Clone();

		shipped.Particles.Put( body, edit );
		Assert.AreEqual( seed, Dword( body.AsSpan( shipped.Particles.LiveAt ), 0x10 ) );

		// A range of -1, the library's commonest, draws three times and adds nothing.
		var plain = edit.Start( At( 58, 1, 1, 1 ) ) & 0xffff;

		for ( var axis = 0; axis < 3; ++axis )
		{
			seed = unchecked((seed * 0x343fd) + 0x269ec3);
			Assert.AreEqual( Dword( shipped.Particles.Template( 58 ), 0x2c + (axis * 4) ), Dword( edit.Bytes( plain ), 0x2c + (axis * 4) ) );
		}

		shipped.Particles.Put( body, edit );
		Assert.AreEqual( seed, Dword( body.AsSpan( shipped.Particles.LiveAt ), 0x10 ) );

		// A range of nought draws nothing: effect 35's.
		Assert.AreEqual( 0, Word( shipped.Particles.Template( 35 ), 0x12 ) );

		var still = edit.Start( At( 35, 1, 1, 1 ) ) & 0xffff;

		shipped.Particles.Put( body, edit );
		Assert.AreEqual( seed, Dword( body.AsSpan( shipped.Particles.LiveAt ), 0x10 ) );
		Assert.AreEqual( Dword( shipped.Particles.Template( 35 ), 0x2c ), Dword( edit.Bytes( still ), 0x2c ) );
	}

	/// <summary>While the system keeps to the screen's effects, an effect of the world's is not started and answers nought; one of the screen's is.</summary>
	[TestMethod]
	public void AStartOfAWorldEffectAnswersNoughtWhileTheSystemKeepsToTheScreens()
	{
		var (body, at) = WithLive( live => BinaryPrimitives.WriteInt32LittleEndian( live[0x08..], 1 ) );
		var edit = new ParkParticles( body, at ).Begin();

		Assert.AreEqual( (1, 0), ((int)shipped.Particles.Template( 58 )[0xc1], (int)shipped.Particles.Template( 3 )[0xc1]) );
		Assert.AreEqual( 0, edit.Start( At( 58, 1, 1, 1 ) ) );
		CollectionAssert.AreEqual( new[] { 8, 13, 20, 0 }, edit.Used.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( 0xda0045, edit.Start( At( 3, 1, 1, 1 ) ) );

		// And with the switch off, as every file has it, the world's effect starts.
		Assert.AreEqual( 0xda0045, shipped.Particles.Begin().Start( At( 58, 1, 1, 1 ) ) );
	}

	/// <summary>
	/// An effect that links another starts it too and keeps its handle; one that follows is put off by the linked
	/// effect's own velocity and marked. An effect that links an effector is not started here.
	/// </summary>
	[TestMethod]
	public void AStartStartsTheEffectItLinks()
	{
		var edit = shipped.Particles.Begin();

		// Effect 4 links 5, which follows it.
		Assert.AreEqual( (5, 1, 0), (Dword( shipped.Particles.Template( 4 ), 0xb4 ), Word( shipped.Particles.Template( 4 ), 0xb8 ), (int)shipped.Particles.Template( 4 )[0xba]) );

		var parent = edit.Start( At( 4, 100, 20, 300 ) );

		Assert.AreEqual( 0xda0045, parent );
		Assert.AreEqual( 0xdb006b, Dword( edit.Bytes( 69 ), 0xb4 ), "the linked emitter's handle" );

		var linked = shipped.Particles.Template( 5 );

		Assert.AreEqual( new ParkParticles.Emitter( 107, true, 219, 5, (100 * 64) + Dword( linked, 0x2c ), (20 * 64) + Dword( linked, 0x30 ),
			(300 * 64) + Dword( linked, 0x34 ), Dword( linked, 0x20 ) ), edit.At( 107 ) );
		Assert.AreEqual( 1, edit.Bytes( 107 )[0xab], "it follows" );
		Assert.AreEqual( 0, edit.Bytes( 69 )[0xab] );
		CollectionAssert.AreEqual( new[] { 107, 69 }, edit.Used.Take( 2 ).Select( emitter => emitter.Slot ).ToArray() );

		// Effect 51 links 54, which does not follow: the same place, unmarked.
		Assert.AreEqual( (54, 0), (Dword( shipped.Particles.Template( 51 ), 0xb4 ), Word( shipped.Particles.Template( 51 ), 0xb8 )) );

		var second = edit.Start( At( 51, 7, 8, 9 ) ) & 0xffff;
		var its = Dword( edit.Bytes( second ), 0xb4 ) & 0xffff;

		Assert.AreEqual( (54, 7 * 64, 8 * 64, 9 * 64), (edit.At( its ).Template, edit.At( its ).X, edit.At( its ).Height, edit.At( its ).Z) );
		Assert.AreEqual( 0, edit.Bytes( its )[0xab] );

		// Effect 75 links 83, which follows it and has a velocity of its own: put off by it.
		Assert.AreEqual( (83, 1), (Dword( shipped.Particles.Template( 75 ), 0xb4 ), Word( shipped.Particles.Template( 75 ), 0xb8 )) );
		Assert.AreEqual( 625, Dword( shipped.Particles.Template( 83 ), 0x30 ) );

		var third = edit.Start( At( 75, 7, 8, 9 ) ) & 0xffff;
		var carried = Dword( edit.Bytes( third ), 0xb4 ) & 0xffff;

		Assert.AreEqual( (83, 7 * 64, (8 * 64) + 625, 9 * 64), (edit.At( carried ).Template, edit.At( carried ).X, edit.At( carried ).Height, edit.At( carried ).Z) );
		Assert.AreEqual( 1, edit.Bytes( carried )[0xab] );

		// An effector's link: 84, and none down 4's or 58's chain.
		Assert.AreEqual( 1, shipped.Particles.Template( 84 )[0xba] );
		Assert.IsFalse( edit.Starts( 84 ) );
		Assert.IsTrue( edit.Starts( 4 ) );
		Assert.IsTrue( edit.Starts( 58 ) );
	}

	/// <summary>No such effect and no slot free answer -1, and leave the image as it was.</summary>
	[TestMethod]
	public void AStartWithNoEffectOrNoSlotAnswersNone()
	{
		var edit = shipped.Particles.Begin();
		var body = (byte[])payload.Clone();

		Assert.AreEqual( ParkParticles.NoSlot, edit.Start( At( ParkParticles.TemplateSlots, 1, 1, 1 ) ) );
		Assert.AreEqual( ParkParticles.NoSlot, edit.Start( At( -1, 1, 1, 1 ) ) );
		shipped.Particles.Put( body, edit );
		CollectionAssert.AreEqual( payload, body );

		// 120 slots, four in use.
		for ( var started = 0; started < ParkParticles.EmitterSlots - 4; ++started )
			Assert.AreNotEqual( ParkParticles.NoSlot, edit.Start( At( 58, 1, 1, 1 ) ), $"start {started}" );

		Assert.AreEqual( ParkParticles.EmitterSlots, edit.Used.Count );
		Assert.AreEqual( ParkParticles.NoSlot, edit.Start( At( 58, 1, 1, 1 ) ) );
		Assert.AreEqual( ParkParticles.EmitterSlots, edit.Used.Count );
	}

	/// <summary>A kill sets the life the engine's does, on the emitter the handle names and no other.</summary>
	[TestMethod]
	public void AKillSetsTheLifeOfTheEmitterNamed()
	{
		var edit = shipped.Particles.Begin();

		Assert.IsFalse( edit.Kill( 0 ) );
		Assert.IsFalse( edit.Kill( Bubbles + 0x10000 ) );
		Assert.AreEqual( 0, edit.At( 20 ).Life );
		Assert.IsTrue( edit.Kill( Bubbles ) );
		Assert.AreEqual( -2, edit.At( 20 ).Life );
		CollectionAssert.AreEqual( new[] { 0x20, 0x22 }, Differing( shipped.Particles.Bytes( 20 ), edit.Bytes( 20 ) ).ToArray() );
		CollectionAssert.AreEqual( new[] { 8, 13, 20, 0 }, edit.Used.Select( emitter => emitter.Slot ).ToArray(), "still on the chain: the engine's tick frees it" );
	}

	/// <summary>
	/// <b>The writer gives each record that asks for one its emitter and its handle</b>, the list's oldest first,
	/// and the file reads back with both; with nothing asked for the module is the file's.
	/// </summary>
	[TestMethod]
	public void TheWriterStartsAnEmitterForEachRecordThatAsks()
	{
		var asking = new[] { new SavedEffect( 1, 0, 1, 1, 1 ), new SavedEffect( 5, 0, -1, -1, 10 ), new SavedEffect( 1, 0, 1, 1, 1 ) };
		var spawns = new ParkParticles.Spawn?[] { At( 69, 555, 10, 174 ), null, At( 9, 555, 10, 174 ) };

		var body = ParkFileWriter.Body( shipped, Carried with
		{
			Things = Things( script => script.Handle == ToiletScript ? script with { Effects = asking, Emitters = spawns } : script )
		}, out _, out var things );

		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.IsNull( written.Particles.Problem );
		CollectionAssert.AreEqual( new[] { 107, 69, 8, 13, 20, 0 }, written.Particles.Used.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( new ParkParticles.Emitter( 69, true, 218, 9, 555 * 64, 10 * 64, 174 * 64, 100000 ), written.Particles.At( 69 ), "the older record's, dealt first" );
		Assert.AreEqual( new ParkParticles.Emitter( 107, true, 219, 69, 555 * 64, 10 * 64, 174 * 64, 0 ), written.Particles.At( 107 ) );

		CollectionAssert.AreEqual( new[] { new SavedEffect( 1, 0xdb006b, 1, 1, 1 ), new SavedEffect( 5, 0, -1, -1, 10 ), new SavedEffect( 1, 0xda0045, 1, 1, 1 ) },
			written.ScriptStates.For( ToiletScript )!.Value.Effects, "each handle names its own emitter; the sound's is left" );
		Assert.IsTrue( written.Particles.Names( 0xdb006b ) && written.Particles.Names( 0xda0045 ) );
		Assert.AreEqual( Bubbles, written.ScriptStates.For( DrinksScript )!.Value.Effects!.Single().Handle );

		var report = things!.Value.Emitters!;

		CollectionAssert.AreEqual( new[] { (ToiletScript, 69), (ToiletScript, 107) }, report.Started.Select( entry => (entry.Script, entry.Emitter.Slot) ).ToArray() );
		Assert.AreEqual( (0, 0), (report.Killed.Count, report.NotStarted) );

		// The control: nothing asked for, and the module is the file's byte for byte.
		var plain = ParkFileWriter.Body( shipped, Carried with { Things = Things( script => script ) }, out _, out things );
		var at = shipped.Particles.LiveAt;

		CollectionAssert.AreEqual( payload[at..(at + ParkParticles.LiveSize)], plain[at..(at + ParkParticles.LiveSize)] );
		Assert.IsNull( things!.Value.Emitters, "and nothing to say of it" );

		// An effect that links an effector is not started: counted, its handle left nought.
		var refused = new ParkWorld( ParkFileWriter.Body( shipped, Carried with
		{
			Things = Things( script => script.Handle == ToiletScript
				? script with { Effects = [new SavedEffect( 1, 0, 1, 1, 1 )], Emitters = [At( 84, 1, 1, 1 )] } : script )
		}, out _, out things ) );

		Assert.AreEqual( (0, 1), (things!.Value.Emitters!.Started.Count, things.Value.Emitters.NotStarted) );
		Assert.AreEqual( 0, refused.ScriptStates.For( ToiletScript )!.Value.Effects!.Single().Handle );
		CollectionAssert.AreEqual( new[] { 8, 13, 20, 0 }, refused.Particles.Used.Select( emitter => emitter.Slot ).ToArray() );
	}

	/// <summary>
	/// <b>A particle record the file's script held and the running one does not has its emitter killed</b>; a list
	/// not handed over, a record still held and a sound's record kill nothing.
	/// </summary>
	[TestMethod]
	public void TheWriterKillsTheEmitterOfARecordGone()
	{
		var written = new ParkWorld( ParkFileWriter.Body( shipped, Carried with
		{
			Things = Things( script => script.Handle == DrinksScript ? script with { Effects = [] } : script )
		}, out _, out var things ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( ParkParticles.KilledLife, written.Particles.At( 20 ).Life );
		CollectionAssert.AreEqual( new[] { 20 }, things!.Value.Emitters!.Killed.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( 0, written.ScriptStates.For( DrinksScript )!.Value.Effects!.Length );

		// Still held: the file's record handed back.
		var held = new ParkWorld( ParkFileWriter.Body( shipped, Carried with
		{
			Things = Things( script => script with { Effects = shipped.ScriptStates.For( script.Handle )!.Value.Effects } )
		}, out _, out things ) );

		Assert.AreEqual( 0, held.Particles.At( 20 ).Life );
		Assert.IsNull( things!.Value.Emitters );

		// Every sound's record gone, the bubbles' kept: a sound's handle kills no emitter, whatever slot its low word names.
		var silent = new ParkWorld( ParkFileWriter.Body( shipped, Carried with
		{
			Things = Things( script => script with { Effects = [.. (shipped.ScriptStates.For( script.Handle )!.Value.Effects ?? []).Where( record => record.Type <= 2 )] } )
		}, out _, out things ) );

		Assert.IsNull( things!.Value.Emitters );
		CollectionAssert.AreEqual( shipped.Particles.Used.Select( emitter => emitter.Life ).ToArray(), silent.Particles.Used.Select( emitter => emitter.Life ).ToArray() );

		// Even where a sound's handle reads as an emitter's: the litter bin's sound is 0x1330061, and slot 0x61 is given count 0x133.
		Assert.IsTrue( shipped.ScriptStates.Order.Select( handle => shipped.ScriptStates.For( handle )!.Value )
			.Any( script => script.Effects?.Any( record => record.Type == 5 && record.Handle == 0x1330061 ) == true ) );

		var (body, _) = WithLive( live => BinaryPrimitives.WriteInt16LittleEndian( live[(EmitterAt( 0x61 ) + 0x0a)..], 0x133 ) );
		var alike = new ParkWorld( body );

		Assert.IsTrue( alike.Particles.Names( 0x1330061 ) );

		var kept = new ParkWorld( ParkFileWriter.Body( alike, Carried with
		{
			Things = Things( alike, script => script with { Effects = [.. (alike.ScriptStates.For( script.Handle )!.Value.Effects ?? []).Where( record => record.Type <= 2 )] } )
		}, out _, out things ) );

		Assert.IsNull( things!.Value.Emitters );
		Assert.AreEqual( alike.Particles.At( 0x61 ).Life, kept.Particles.At( 0x61 ).Life );
	}

	/// <summary>
	/// <b>The running park's started particles reach the writer</b>: a toilet told it is worn starts two on its
	/// node, and each is handed over with its effect at the node's place in whole units; a sound, a record the load
	/// put back, a particle with a direction, an item's own effect and a record with no node ask for none, the
	/// last three counted.
	/// </summary>
	[TestMethod]
	public void TheRunningParksParticlesAskForTheirEmitters()
	{
		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );

		var state = new ParkState( shipped );
		var script = rides.Scheduler.Find( rides.ScriptFor( Toilet ) )!;

		Assert.AreEqual( ToiletScript, script.Id );

		script.Effects!.Add( 1, 1, 9, 1, script.Nodes!.EffectIndex( 1, 1 ) );
		script.Effects.Add( 5, -1, 43, 10 );
		script.Effects.Add( 1, 1, 69, 1, script.Nodes.EffectIndex( 1, 1 ) );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var written = things.Scripts.Single( entry => entry.Handle == ToiletScript );

		CollectionAssert.AreEqual( new ParkParticles.Spawn?[] { At( 69, 555, 10, 174 ), null, At( 9, 555, 10, 174 ) }, written.Emitters );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What.StartsWith( "SAVE_PARK_EMITTER" ) ) );

		// The Drinks Shop's own record was put back by the load: its emitter is the file's.
		CollectionAssert.AreEqual( new ParkParticles.Spawn?[] { null }, things.Scripts.Single( entry => entry.Handle == DrinksScript ).Emitters );

		// And the file written from the park holds both emitters under the record's handles.
		var file = new ParkWorld( ParkFileWriter.Body( shipped, Carried with { Things = things }, out _, out _ ) );

		Assert.IsNull( file.Problem );
		CollectionAssert.AreEqual( new[] { 0xdb006b, 0, 0xda0045 }, file.ScriptStates.For( ToiletScript )!.Value.Effects!.Select( record => record.Handle ).ToArray() );
		Assert.AreEqual( new ParkParticles.Emitter( 107, true, 219, 69, 555 * 64, 10 * 64, 174 * 64, 0 ), file.Particles.At( 107 ) );

		// The three that ask for none, each counted once.
		script.Effects.Kill( 1 );
		script.Effects.Kill( 10 );
		script.Effects.Add( 2, 1, 37, 1, script.Nodes.EffectIndex( 2, 1 ) );
		script.Effects.Add( 1, 1, 0x8001, 2, script.Nodes.EffectIndex( 1, 1 ) );
		script.Effects.Add( 1, -1, 58, 3 );
		script.Effects.Add( 1, 77, 58, 4 );

		written = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!.Scripts.Single( entry => entry.Handle == ToiletScript );

		CollectionAssert.AreEqual( new ParkParticles.Spawn?[] { null, null, null, null }, written.Emitters );

		int Times( string what ) => Unimplemented.Summary.Where( entry => entry.What == what ).Sum( entry => entry.Times );

		Assert.AreEqual( (1, 1, 2), (Times( "SAVE_PARK_EMITTER_DIRECTED" ), Times( "SAVE_PARK_EMITTER_ITEM_EFFECT" ), Times( "SAVE_PARK_EMITTER_NO_PLACE" )) );
	}
}
