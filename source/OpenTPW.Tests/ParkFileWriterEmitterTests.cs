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

	/// <summary>
	/// A start with a direction is <c>Particles_SpawnFull</c>'s: the three words at <c>+0x38</c> are the direction
	/// times the effect's speed, over 1024 toward nought, and the generator is not drawn. The Jungle Spray's jet at
	/// its node 2 is the original's own emitter, turned 0 and turned 270.
	/// </summary>
	[TestMethod]
	public void ADirectedStartIsAimedAndDrawsNothing()
	{
		const int jet = 37, ranged = 3;

		Assert.AreEqual( 40, Dword( shipped.Particles.Template( jet ), 0xb0 ), "the jet's speed" );
		Assert.AreEqual( (0, 40, 0), (Dword( shipped.Particles.Template( jet ), 0x38 ), Dword( shipped.Particles.Template( jet ), 0x3c ), Dword( shipped.Particles.Template( jet ), 0x40 )) );

		var edit = shipped.Particles.Begin();
		var north = edit.Start( At( jet, 525, 5, 311 ) with { Direction = (-11, 656, 785) } ) & 0xffff;
		var west = edit.Start( At( jet, 418, 5, 265 ) with { Direction = (-785, 656, -11) } ) & 0xffff;

		(int, int, int) Aim( int slot ) => (Dword( edit.Bytes( slot ), 0x38 ), Dword( edit.Bytes( slot ), 0x3c ), Dword( edit.Bytes( slot ), 0x40 ));

		Assert.AreEqual( (0, 25, 30), Aim( north ), "-11 x 40 over 1024 is cut to nought, not down to -1" );
		Assert.AreEqual( (-30, 25, 0), Aim( west ) );
		Assert.AreEqual( new ParkParticles.Emitter( north, true, 218, jet, 525 * 64, 5 * 64, 311 * 64, Dword( shipped.Particles.Template( jet ), 0x20 ) ), edit.At( north ) );

		// Outside the aim it is the plain start's emitter.
		var plain = shipped.Particles.Begin();
		var same = plain.Start( At( jet, 525, 5, 311 ) ) & 0xffff;

		Assert.AreEqual( north, same );
		CollectionAssert.AreEqual( new[] { 0x3c, 0x40 }, Differing( plain.Bytes( same ), edit.Bytes( north ) ).Where( at => at != 0xd2 ).ToArray() );

		// An effect with a range: a plain start draws three times, a directed one not at all.
		var seed = Dword( payload.AsSpan( shipped.Particles.LiveAt ), 0x10 );
		var aimed = shipped.Particles.Begin();
		var slot = aimed.Start( At( ranged, 1, 1, 1 ) with { Direction = (0, 0, 1024) } ) & 0xffff;
		var body = (byte[])payload.Clone();

		shipped.Particles.Put( body, aimed );
		Assert.AreEqual( seed, Dword( body.AsSpan( shipped.Particles.LiveAt ), 0x10 ) );

		for ( var axis = 0; axis < 3; ++axis )
			Assert.AreEqual( Dword( shipped.Particles.Template( ranged ), 0x2c + (axis * 4) ), Dword( aimed.Bytes( slot ), 0x2c + (axis * 4) ) );

		Assert.AreEqual( (0, 0, Dword( shipped.Particles.Template( ranged ), 0xb0 )), (Dword( aimed.Bytes( slot ), 0x38 ), Dword( aimed.Bytes( slot ), 0x3c ), Dword( aimed.Bytes( slot ), 0x40 )) );
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

		// The two that ask for none, each counted.
		script.Effects.Kill( 1 );
		script.Effects.Kill( 10 );
		script.Effects.Add( 1, 1, 0x8001, 2, script.Nodes.EffectIndex( 1, 1 ) );
		script.Effects.Add( 1, -1, 58, 3 );
		script.Effects.Add( 1, 77, 58, 4 );

		written = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!.Scripts.Single( entry => entry.Handle == ToiletScript );

		CollectionAssert.AreEqual( new ParkParticles.Spawn?[] { null, null, null }, written.Emitters );

		int Times( string what ) => Unimplemented.Summary.Where( entry => entry.What == what ).Sum( entry => entry.Times );

		Assert.AreEqual( (0, 1, 2), (Times( "SAVE_PARK_EMITTER_DIRECTED" ), Times( "SAVE_PARK_EMITTER_ITEM_EFFECT" ), Times( "SAVE_PARK_EMITTER_NO_PLACE" )) );
	}

	/// <summary>
	/// <b>A particle with a direction is written aimed</b>: the Jungle Spray's jet, a type 2 on its node 2, is
	/// handed over at its node's place with the way the node points, and the file written holds the emitter the
	/// original's own file of the same park holds while the jet runs: at (525, 5, 311), aimed (0, 25, 30).
	/// </summary>
	[TestMethod]
	public void TheJungleSpraysJetIsWrittenAimed()
	{
		const int spray = 14, jet = 37;

		var rides = new ParkRides( Theme, shipped, catalogue, data );

		made.Add( rides );

		var state = new ParkState( shipped );
		var script = rides.Scheduler.Find( rides.ScriptFor( spray ) )!;

		script.Effects!.Add( 2, 2, jet, 1, script.Nodes!.EffectIndex( 2, 2 ) );

		var things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var written = things.Scripts.Single( entry => entry.Handle == script.Id );

		Assert.AreEqual( At( jet, 525, 5, 311 ) with { Direction = (-11, 656, 785) }, written.Emitters!.Single( emitter => emitter is not null ) );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What.StartsWith( "SAVE_PARK_EMITTER" ) ) );

		var file = new ParkWorld( ParkFileWriter.Body( shipped, Carried with { Things = things }, out _, out _ ) );

		Assert.IsNull( file.Problem );

		var record = file.ScriptStates.For( script.Id )!.Value.Effects!.Single( entry => entry.Type == 2 );

		Assert.IsTrue( file.Particles.Names( record.Handle ) );

		var slot = record.Handle & 0xffff;

		Assert.AreEqual( new ParkParticles.Emitter( slot, true, 218, jet, 525 * 64, 5 * 64, 311 * 64, 1 ), file.Particles.At( slot ) );
		Assert.AreEqual( (0, 25, 30), file.Particles.Aim( slot ) );
		Assert.AreEqual( (0, 18, 0), file.Particles.Aim( Bubbles & 0xffff ), "an emitter no script aims keeps its effect's" );

		// Its other two lanes, as the original's own starts of them stand in its memory: node 1 at (515, 5, 311)
		// aimed (0, 19, 34) and node 3 at (534, 5, 311) aimed (0, 25, 30).
		script.Effects.Add( 2, 1, jet, 2, script.Nodes.EffectIndex( 2, 1 ) );
		script.Effects.Add( 2, 3, jet, 3, script.Nodes.EffectIndex( 2, 3 ) );

		var lanes = new ParkWorld( ParkFileWriter.Body( shipped, Carried with
		{
			Things = rides.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!
		}, out _, out _ ) );

		var aimed = lanes.Particles.Used.Where( emitter => emitter.Template == jet )
			.Select( emitter => (emitter.X / 64, emitter.Height / 64, emitter.Z / 64, lanes.Particles.Aim( emitter.Slot )) ).OrderBy( lane => lane.Item1 ).ToArray();

		CollectionAssert.AreEqual( new[] { (515, 5, 311, (0, 19, 34)), (525, 5, 311, (0, 25, 30)), (534, 5, 311, (0, 25, 30)) }, aimed );
	}

	private const int PotHandle = unchecked((int)0xffffff00);

	private static readonly SavedTrackRide Pot = new( PotHandle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 );

	private static readonly SavedChannel AtRest = new( ParkThingStates.NoRole, 0, 0, 1f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 0f );

	/// <summary>Where the original's own smoking boat's node stood under its menu's pause, and its emitter's place in its file.</summary>
	private static readonly ParkParticles.Spawn TheirSmoke = new( 2, (int)(442.6575f * 1024f), (int)(30.4622f * 1024f), (int)(263.3747f * 1024f) );

	private static readonly (int X, int Height, int Z) TheirSmokeAt = (28330, 1949, 16855);

	/// <summary>A boat whose <c>+0x2c</c> holds <paramref name="smoke"/>, asking for <paramref name="spawn"/>.</summary>
	private static ParkFileWriter.WrittenCar Boat( int smoke, ParkParticles.Spawn? spawn )
	{
		var bytes = new byte[SavedTrackCar.Size];

		BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0x2c ), smoke );

		var car = new SavedTrackCar( PotHandle, bytes, 0x20a00, 0x13200, PotHandle, 0x20187, 0x12987, 0 );

		car.Riders.Add( new SavedTrackRider( 31, 1 ) );

		return new ParkFileWriter.WrittenCar( car, new ParkFileWriter.CarModel( 1142, AtRest, null ), null, [], spawn );
	}

	/// <summary><paramref name="world"/>'s things with a Hot Pot of these boats written whole.</summary>
	private ParkFileWriter.RunningThings WithPot( ParkWorld world, params ParkFileWriter.WrittenCar[] boats )
		=> Things( world, script => script ) with { Tracks = [new ParkFileWriter.WrittenTrack( Pot, boats, [], [] )] };

	/// <summary>
	/// <b><c>Particles_Move</c> writes an emitter's place and nothing else</b>, each of the three shifted down four
	/// bits, and moves nothing for a handle that names none.
	/// </summary>
	[TestMethod]
	public void AMoveIsTheEmittersPlaceAlone()
	{
		var edit = shipped.Particles.Begin();

		Assert.IsFalse( edit.Move( 0, 16, 32, 48 ) );
		Assert.IsFalse( edit.Move( Bubbles + 0x10000, 16, 32, 48 ) );
		Assert.AreEqual( 0, Differing( shipped.Particles.Bytes( 20 ), edit.Bytes( 20 ) ).Count );

		Assert.IsTrue( edit.Move( Bubbles, TheirSmoke.X, TheirSmoke.Height, TheirSmoke.Z ) );
		Assert.AreEqual( TheirSmokeAt, (edit.At( 20 ).X, edit.At( 20 ).Height, edit.At( 20 ).Z) );
		Assert.IsTrue( Differing( shipped.Particles.Bytes( 20 ), edit.Bytes( 20 ) ).All( at => at is >= 0x14 and < 0x20 ) );
		CollectionAssert.AreEqual( shipped.Particles.Used.Select( emitter => emitter.Slot ).ToArray(), edit.Used.Select( emitter => emitter.Slot ).ToArray() );
	}

	/// <summary>
	/// <b>A boat that began to smoke here is written with its smoke's emitter</b> (<c>FUN_00544c80</c>): effect 2
	/// where its node stands, at 64ths of a unit as the original's own file has it, its handle in the boat's
	/// <c>+0x2c</c>, dealt after the scripts' emitters; a boat with no smoke keeps -1.
	/// </summary>
	[TestMethod]
	public void TheWriterStartsASmokingBoatsEmitterAndWritesItsHandle()
	{
		var things = WithPot( shipped, Boat( -1, TheirSmoke ), Boat( -1, null ) );

		things = things with
		{
			Scripts = [.. things.Scripts.Select( script => script.Handle == ToiletScript
				? script with { Effects = [new SavedEffect( 1, 0, 1, 1, 1 )], Emitters = [At( 9, 555, 10, 174 )] } : script )]
		};

		var written = new ParkWorld( ParkFileWriter.Body( shipped, Carried with { Things = things }, out _, out var report ) );

		Assert.IsNull( written.Problem );
		Assert.IsNull( written.Particles.Problem );
		CollectionAssert.AreEqual( new[] { 107, 69, 8, 13, 20, 0 }, written.Particles.Used.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( 9, written.Particles.At( 69 ).Template, "the script's first" );
		Assert.AreEqual( new ParkParticles.Emitter( 107, true, 219, 2, TheirSmokeAt.X, TheirSmokeAt.Height, TheirSmokeAt.Z, 0 ), written.Particles.At( 107 ) );

		CollectionAssert.AreEqual( new[] { 0xdb006b, -1 }, written.TrackRides.Cars.Select( car => car.Word( 0x2c ) ).ToArray() );

		foreach ( var car in written.TrackRides.Cars )
		{
			Assert.AreEqual( (0x20a00, 0x13200, PotHandle, 0x20187, 0x12987), (car.CentreX, car.CentreZ, car.BuoyRide, car.BuoyX, car.BuoyZ) );
			Assert.AreEqual( new SavedTrackRider( 31, 1 ), car.Riders.Single(), "the boat is written whole, its handle apart" );
		}

		Assert.IsTrue( written.Particles.Names( 0xdb006b ) );

		var emitters = report!.Value.Emitters!;

		CollectionAssert.AreEqual( new[] { (PotHandle, 107, false) }, emitters.Smoke!.Select( entry => (entry.Ride, entry.Emitter.Slot, entry.Kept) ).ToArray() );
		Assert.AreEqual( (1, 0, 0), (emitters.Started.Count, emitters.Killed.Count, emitters.NotStarted) );

		// Its bytes are a plain start's of effect 2 there: no draw the start itself does not make.
		var edit = shipped.Particles.Begin();

		edit.Start( At( 9, 555, 10, 174 ) );
		edit.Start( TheirSmoke );
		CollectionAssert.AreEqual( edit.Bytes( 107 ).ToArray(), written.Particles.Bytes( 107 ).ToArray() );

		// An effect that links an effector is not started: counted, and the boat keeps -1.
		var refused = new ParkWorld( ParkFileWriter.Body( shipped, Carried with { Things = WithPot( shipped, Boat( -1, TheirSmoke with { Template = 84 } ) ) }, out _, out report ) );

		Assert.AreEqual( -1, refused.TrackRides.Cars.Single().Word( 0x2c ) );
		Assert.AreEqual( (0, 1), (report!.Value.Emitters!.Smoke!.Count, report.Value.Emitters.NotStarted) );
		CollectionAssert.AreEqual( new[] { 8, 13, 20, 0 }, refused.Particles.Used.Select( emitter => emitter.Slot ).ToArray() );

		// A file whose module did not read has nowhere to start one: counted the same, the boat keeping -1.
		var (unread, at) = WithLive( _ => { } );

		BinaryPrimitives.WriteInt32LittleEndian( unread.AsSpan( at + 8 ), ParkParticles.LiveSize + 1 );

		var blind = new ParkWorld( unread );

		Assert.IsNotNull( blind.Particles.Problem );
		ParkFileWriter.Body( blind, Carried with { Things = WithPot( blind, Boat( -1, TheirSmoke ), Boat( -1, null ) ) }, out _, out report );
		Assert.AreEqual( 1, report!.Value.Emitters!.NotStarted );
	}

	/// <summary>
	/// <b>A file's smoking boat keeps its emitter</b>, put where its node stands now as the boat's step puts it
	/// every tick (<c>0x00548613</c>); <b>a boat fixed, or sold with its ride, has it killed</b>
	/// (<c>FUN_00544e50</c>, <c>0x0054b077</c>); and a handle that names no smoke is not kept.
	/// </summary>
	[TestMethod]
	public void TheWriterKeepsMovesAndKillsAFilesBoatsSmoke()
	{
		var smoking = new ParkWorld( ParkFileWriter.Body( shipped, Carried with { Things = WithPot( shipped, Boat( -1, TheirSmoke ) ) }, out _, out _ ) );
		const int Handle = 0xda0045;

		Assert.AreEqual( Handle, smoking.TrackRides.Cars.Single().Word( 0x2c ) );

		// Kept: the same emitter, moved, nothing started or killed.
		var moved = TheirSmoke with { X = 440 << 10, Height = 31 << 10, Z = 250 << 10 };
		var kept = new ParkWorld( ParkFileWriter.Body( smoking, Carried with { Things = WithPot( smoking, Boat( Handle, moved ) ) }, out _, out var report ) );

		Assert.IsNull( kept.Problem );
		Assert.AreEqual( Handle, kept.TrackRides.Cars.Single().Word( 0x2c ) );
		Assert.AreEqual( new ParkParticles.Emitter( 69, true, 218, 2, 440 * 64, 31 * 64, 250 * 64, 0 ), kept.Particles.At( 69 ) );
		CollectionAssert.AreEqual( smoking.Particles.Used.Select( emitter => emitter.Slot ).ToArray(), kept.Particles.Used.Select( emitter => emitter.Slot ).ToArray() );
		CollectionAssert.AreEqual( new[] { (PotHandle, 69, true) }, report!.Value.Emitters!.Smoke!.Select( entry => (entry.Ride, entry.Emitter.Slot, entry.Kept) ).ToArray() );
		Assert.AreEqual( (0, 0, 0), (report.Value.Emitters.Started.Count, report.Value.Emitters.Killed.Count, report.Value.Emitters.NotStarted) );

		// Fixed: the boat written with no smoke, and the file's emitter killed.
		var mended = new ParkWorld( ParkFileWriter.Body( smoking, Carried with { Things = WithPot( smoking, Boat( -1, null ) ) }, out _, out report ) );

		Assert.AreEqual( -1, mended.TrackRides.Cars.Single().Word( 0x2c ) );
		Assert.AreEqual( ParkParticles.KilledLife, mended.Particles.At( 69 ).Life );
		CollectionAssert.AreEqual( new[] { 69 }, report!.Value.Emitters!.Killed.Select( emitter => emitter.Slot ).ToArray() );
		Assert.AreEqual( 0, report.Value.Emitters.Smoke!.Count );

		// Sold: the ride taken out, and its boat's smoke with it.
		var sold = new ParkWorld( ParkFileWriter.Body( smoking, Carried with { Things = Things( smoking, script => script ) with { GoneTracks = [PotHandle] } }, out _, out report ) );

		Assert.AreEqual( 0, sold.TrackRides.Cars.Count );
		Assert.AreEqual( ParkParticles.KilledLife, sold.Particles.At( 69 ).Life );
		CollectionAssert.AreEqual( new[] { 69 }, report!.Value.Emitters!.Killed.Select( emitter => emitter.Slot ).ToArray() );

		// A ride not written again leaves its boats' smoke alone.
		var left = new ParkWorld( ParkFileWriter.Body( smoking, Carried with { Things = Things( smoking, script => script ) }, out _, out report ) );

		Assert.AreEqual( 0, left.Particles.At( 69 ).Life );
		Assert.IsNull( report!.Value.Emitters );

		// A handle that names an emitter of another effect, or none, is no smoke to keep: one is started.
		foreach ( var stale in new[] { Bubbles, Handle + 0x10000 } )
		{
			var anew = new ParkWorld( ParkFileWriter.Body( smoking, Carried with { Things = WithPot( smoking, Boat( stale, TheirSmoke ) ) }, out _, out report ) );
			var dealt = anew.TrackRides.Cars.Single().Word( 0x2c );

			Assert.AreNotEqual( stale, dealt );
			Assert.AreNotEqual( Handle, dealt );
			Assert.IsTrue( anew.Particles.Names( dealt ) );
			Assert.AreEqual( 2, anew.Particles.At( dealt & 0xffff ).Template );
			Assert.AreEqual( (TheirSmokeAt.X, false), (anew.Particles.At( dealt & 0xffff ).X, report!.Value.Emitters!.Smoke!.Single().Kept) );
			Assert.AreEqual( ParkParticles.KilledLife, anew.Particles.At( 69 ).Life, "the file's boat's own, which no boat names now" );
			Assert.AreEqual( 0, anew.Particles.At( 20 ).Life, "the bubbles are their script's" );
		}
	}

	/// <summary>
	/// <b>With no boat drawn there is nowhere to start a boat's smoke</b>: the car writer asks for none and counts
	/// it, and a boat that does not smoke is not counted.
	/// </summary>
	[TestMethod]
	public void ASmokingBoatWithNoBoatDrawnAsksForNoEmitterAndIsCounted()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );
		var cars = table.Cars;

		cars.Place( handle, 0x20a00, 0x13200 );
		cars.OpenForLoading( handle );
		var boat = cars.Launch( handle )!;

		boat.Flags |= ParkBumperCars.CarFlags.Active;

		Assert.IsTrue( catalogue.TryGet( 1140, out var pot ) );

		var record = new SavedTrackRide( handle, 0x20400, 0x12c00, 0, 1140, 60, 1, 1, 750, 2 );
		var sound = ParkCarWriter.Track( cars, record, pot, _ => 0u, null, data )!;

		Assert.IsNull( sound.Cars.Single().Smoke );
		Assert.IsFalse( Unimplemented.Summary.Any( gap => gap.What == "SAVE_PARK_CAR_SMOKE" ) );

		cars.Break( handle );
		Assert.IsTrue( boat.Smoking );

		var broken = ParkCarWriter.Track( cars, record, pot, _ => 0u, null, data )!;

		Assert.IsNull( broken.Cars.Single().Smoke );
		Assert.AreEqual( -1, broken.Cars.Single().Car.Word( 0x2c ) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "SAVE_PARK_CAR_SMOKE" ).Times );
	}
}
