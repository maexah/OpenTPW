using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The particles a park's scripts start run in the park's particle system and are set out for drawing
/// (<c>docs/exe/park.md</c>, "A park's particles, started and drawn").
/// </summary>
[TestClass]
public class ParkParticleTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int DrinksShop = 16, DrinksScript = 6, Bubbles = 58;
	private const int Toilet = 21, Flies = 9, Stink = 69;
	private const int Spray = 14, Jet = 37;

	private BaseFileSystem data = null!;
	private byte[] payload = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;
	private ParticleSystem system = null!;
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
		system = new ParticleSystem( "Particle/Tp2.plb", 1000 );
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

		// Null where the game is absent and the set-up skipped before making one.
		system?.Shutdown();
		Unimplemented.Forget();
	}

	private ParkRides Rides( ParkWorld world )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

	/// <summary>The emitters of the world that are running, by slot.</summary>
	private Emitter[] Running => [.. system.Emitters.Where( emitter => emitter.Active && !emitter.Template.OnScreen )];

	private static (int, int, int) PlaceOf( Emitter emitter ) => (emitter.X, emitter.Y, emitter.Z);

	private static (int, int, int) Units( int x, int height, int z ) => (x * 64, height * 64, z * 64);

	private static bool Counted( string prefix ) => Unimplemented.Summary.Any( entry => entry.What.StartsWith( prefix ) );

	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A whole .RSE file of one variable, as <see cref="RideScriptEffectTests"/> builds one.</summary>
	private static RideScriptFile Build( params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 1 );
		writer.Write( 8 );
		writer.Write( 50 );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );

		var name = Encoding.ASCII.GetBytes( "VAR_0\0" );

		writer.Write( name.Length );
		writer.Write( name );
		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>A script of <paramref name="body"/> standing on the toilet's nodes.</summary>
	private RideScript OnTheToilet( ParkRides rides, params int[] body )
		=> new( Build( body ) ) { Effects = new RideEffects(), Nodes = rides.Scheduler.Find( rides.ScriptFor( Toilet ) )!.Nodes, Id = 900, ThingId = Toilet };

	/// <summary>
	/// <b>A loaded park's own particles carry on</b>: the shipped Drinks Shop's record names the file's emitter of
	/// its bubbles, and the load starts that effect where the file holds it. The records whose handles name no
	/// emitter in use start nothing.
	/// </summary>
	[TestMethod]
	public void TheShippedShopsBubblesCarryOnAtTheLoad()
	{
		var rides = Rides( shipped );
		var running = Running;

		Assert.AreEqual( 1, running.Length );
		Assert.AreEqual( Bubbles, running[0].Effect );
		Assert.AreEqual( Units( 440, 6, 312 ), PlaceOf( running[0] ) );
		Assert.IsNull( running[0].Aim );
		Assert.AreEqual( 0, running[0].Lifetime, "its template's own life" );

		var record = rides.Scheduler.Find( rides.ScriptFor( DrinksShop ) )!.Effects!.Records.Single();

		Assert.AreEqual( DrinksScript, rides.ScriptFor( DrinksShop ) );
		Assert.IsTrue( system.TryEmitter( record.Emitter, out var named ) );
		Assert.AreSame( running[0], named );

		// No other record a load put back holds an emitter.
		var others = shipped.Objects.Select( placed => rides.Scheduler.Find( rides.ScriptFor( placed.ThingId ) ) )
			.Where( script => script is not null && script.Id != DrinksScript )
			.SelectMany( script => script!.Effects?.Records ?? [] ).Where( entry => entry.IsParticle ).ToArray();

		Assert.IsTrue( others.All( entry => entry.Emitter == 0 ) );
		Assert.IsFalse( Counted( "LOADED_EMITTER" ) );
		Assert.IsFalse( Counted( "PARK_PARTICLE" ) );
	}

	/// <summary>With no particle system a load starts nothing and counts nothing.</summary>
	[TestMethod]
	public void WithNoParticleSystemNothingIsStarted()
	{
		system.Shutdown();

		var rides = Rides( shipped );

		Assert.AreEqual( 0, Running.Length );
		Assert.AreEqual( 0, rides.Scheduler.Find( rides.ScriptFor( DrinksShop ) )!.Effects!.Records.Single().Emitter );

		var script = OnTheToilet( rides, Word( Opcode.ADDOBJ ), 1, 1, Flies, 1, Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.Effects!.Records.Single().Emitter );
		Assert.IsFalse( Counted( "PARK_PARTICLE" ) );
	}

	/// <summary>
	/// <b><c>ADDOBJ</c> starts its particle on its node and <c>KILLOBJ</c> stops it</b>: the emitter stands at the
	/// node's place in whole units, its handle is kept on the record, and the kill sets its life to -2 and takes
	/// the record. A record of another tag keeps its emitter.
	/// </summary>
	[TestMethod]
	public void AnObjectAddedStartsItsEmitterAndAKillStopsIt()
	{
		var rides = Rides( shipped );
		var before = Running.Length;

		var script = OnTheToilet( rides,
			Word( Opcode.ADDOBJ ), 1, 1, Flies, 1,
			Word( Opcode.ADDOBJ ), 1, 1, Stink, 2,
			Word( Opcode.ADDOBJ ), 5, -1, 43, 1,
			Word( Opcode.WAIT ), 1000,
			Word( Opcode.KILLOBJ ), 1,
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( before + 2, Running.Length );

		var records = script.Effects!.Records;

		Assert.AreEqual( 3, records.Count );
		Assert.AreEqual( 0, records[0].Emitter, "a sound starts no emitter" );
		Assert.IsTrue( system.TryEmitter( records[1].Emitter, out var stink ) );
		Assert.IsTrue( system.TryEmitter( records[2].Emitter, out var flies ) );
		Assert.AreNotSame( stink, flies );
		Assert.AreEqual( Stink, stink.Effect );
		Assert.AreEqual( Flies, flies.Effect );
		Assert.AreEqual( Units( 555, 10, 174 ), PlaceOf( stink ) );
		Assert.AreEqual( Units( 555, 10, 174 ), PlaceOf( flies ) );
		Assert.IsNull( flies.Aim );
		Assert.IsFalse( Counted( "PARK_PARTICLE" ) );

		var fliesLife = flies.Lifetime;

		Assert.AreNotEqual( -2, fliesLife );

		script.Turn( 2000f );

		Assert.AreEqual( -2, flies.Lifetime, "killed" );
		Assert.AreNotEqual( -2, stink.Lifetime, "another tag's" );
		Assert.AreSame( stink, system.TryEmitter( script.Effects.Records.Single().Emitter, out var kept ) ? kept : null );
	}

	/// <summary><b>An <c>EVENT</c> starts its particle and keeps no record</b>, so nothing of the script's can stop it.</summary>
	[TestMethod]
	public void AnEventStartsItsEmitterAndKeepsNoRecord()
	{
		var rides = Rides( shipped );
		var before = Running;

		var script = OnTheToilet( rides,
			Word( Opcode.EVENT ), 1, 1, Stink,
			Word( Opcode.EVENT ), 5, -1, 43,
			Word( Opcode.END ) );

		script.Turn( 0f );

		var started = Running.Except( before ).Single();

		Assert.AreEqual( Stink, started.Effect );
		Assert.AreEqual( Units( 555, 10, 174 ), PlaceOf( started ) );
		Assert.AreEqual( 0, script.Effects!.Count );
		Assert.IsFalse( Counted( "PARK_PARTICLE" ), "a sound's event asks the particles for nothing" );

		script.StopParticles();

		Assert.AreNotEqual( -2, started.Lifetime );
	}

	/// <summary>
	/// <b>A script's end stops every particle its records hold</b>: a thing sold has its emitters killed, and what
	/// another script started runs on.
	/// </summary>
	[TestMethod]
	public void AScriptTornDownStopsItsParticles()
	{
		var rides = Rides( shipped );
		var bubbles = Running.Single();

		Assert.IsTrue( catalogue.TryGet( shipped.Objects.Single( placed => placed.ThingId == Toilet ).CatalogueId, out var item ) );

		var script = rides.Scheduler.Find( rides.ScriptFor( Toilet ) )!;

		Assert.IsTrue( script.Set( "VAR_WORN", 1 ) );

		// The toilet's own script starts its two inside a few turns of being told it is worn.
		for ( var tick = 1; tick <= 200 && script.Effects!.Records.Count( entry => entry.IsParticle && entry.Emitter != 0 ) < 2; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		var started = Running.Where( emitter => emitter != bubbles ).ToArray();

		CollectionAssert.AreEquivalent( new[] { Flies, Stink }, started.Select( emitter => emitter.Effect ).ToArray() );
		Assert.IsTrue( started.All( emitter => PlaceOf( emitter ) == Units( 555, 10, 174 ) ) );
		Assert.IsTrue( started.All( emitter => emitter.Lifetime != -2 ) );

		Assert.IsTrue( rides.Unbind( Toilet, item ) );

		Assert.IsTrue( started.All( emitter => emitter.Lifetime == -2 ), "killed with the script" );
		Assert.AreNotEqual( -2, bubbles.Lifetime, "the shop's run on" );
	}

	/// <summary>
	/// <b>A particle with a direction is started aimed</b>: the Jungle Spray's jet, a type 2 on its node 2, stands
	/// at the node and throws its particles the way the node points times the effect's speed, over 1024 toward
	/// nought, and its emitter's own velocity takes no draw.
	/// </summary>
	[TestMethod]
	public void TheJungleSpraysJetIsStartedAimed()
	{
		var rides = Rides( shipped );
		var spray = rides.Scheduler.Find( rides.ScriptFor( Spray ) )!;
		var script = new RideScript( Build( Word( Opcode.ADDOBJ ), 2, 2, Jet, 1, Word( Opcode.END ) ) )
			{ Effects = new RideEffects(), Nodes = spray.Nodes, Id = 901, ThingId = Spray };

		script.Turn( 0f );

		Assert.IsTrue( system.TryEmitter( script.Effects!.Records.Single().Emitter, out var jet ) );
		Assert.AreEqual( Jet, jet.Effect );
		Assert.AreEqual( Units( 525, 5, 311 ), PlaceOf( jet ) );
		Assert.AreEqual( new ParticleVector( 0, 25, 30 ), jet.Aim );

		var template = system.Library.Effects[Jet];

		Assert.AreEqual( (template.EmitterVelocity.X, template.EmitterVelocity.Y, template.EmitterVelocity.Z), (jet.VX, jet.VY, jet.VZ) );
		Assert.AreEqual( 40, template.VelocityScale );

		// The Spray's node rides a clip: counted, and the jet stays where the node rests.
		Assert.IsTrue( Counted( "PARK_PARTICLE_NODE_ON_A_CLIP" ) );
	}

	/// <summary>
	/// <b>An aimed emitter throws its particles at its aim</b>, not at its effect's own velocity: each of the jet's
	/// starts within the effect's random range of the aim, plus the emitter's own where the effect hands it on.
	/// </summary>
	[TestMethod]
	public void AnAimedEmitterThrowsItsParticlesAtItsAim()
	{
		var template = system.Library.Effects[Jet];
		var aim = new ParticleVector( 300, -200, 100 );

		Assert.AreEqual( 0, template.RadialSpeed, "the jet's particles take the effect's velocity" );
		Assert.IsTrue( System.Math.Abs( template.Velocity.X - aim.X ) > 2 * System.Math.Abs( template.VelocityRandom ), "an aim the effect's own cannot be taken for" );

		Assert.IsTrue( system.TryEmitter( system.Spawn( Jet, 0, 0, 0, aim: aim ), out var jet ) );

		for ( var tick = 0; tick < 12 && jet.Count == 0; ++tick )
			system.Tick();

		Assert.IsTrue( jet.Count > 0, "it has thrown one" );

		var carried = template.InheritVelocity ? (jet.VX, jet.VY, jet.VZ) : (0, 0, 0);
		var reach = System.Math.Abs( template.VelocityRandom );

		for ( var index = jet.FirstParticle; index >= 0; index = system.Particles[index].Next )
		{
			var particle = system.Particles[index];

			// A tick has stepped it since, so its velocity is held to the aim loosely: nearer it than the effect's own.
			Assert.IsTrue( System.Math.Abs( particle.VX - aim.X - carried.Item1 ) < System.Math.Abs( particle.VX - template.Velocity.X - carried.Item1 ), $"x {particle.VX}" );
			Assert.IsTrue( System.Math.Abs( particle.VX - aim.X - carried.Item1 ) <= reach + 64, $"x {particle.VX}" );
		}
	}

	/// <summary>The shipped park with <paramref name="change"/> made to the emitter of the Drinks Shop's bubbles, slot 20 of its file.</summary>
	private ParkWorld WithTheBubblesEmitter( System.Action<System.Span<byte>> change )
	{
		var body = (byte[])payload.Clone();

		change( body.AsSpan( shipped.Particles.LiveAt + 0x2c + (20 * ParkParticles.EmitterSize), ParkParticles.EmitterSize ) );

		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		var reader = new SaveReader( stream );

		reader.ReadFile();

		return new ParkWorld( body, reader.Preamble );
	}

	/// <summary>
	/// <b>A loaded record starts only the emitter its handle names</b>: under another count the slot is not the
	/// record's, a killed emitter (a life under nought) is left to end, and one part way through a life is begun
	/// again and counted.
	/// </summary>
	[TestMethod]
	public void ALoadedRecordStartsOnlyALiveEmitterItNames()
	{
		// Another count in the slot: the record's handle names nothing.
		Rides( WithTheBubblesEmitter( emitter => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian( emitter[0x0a..], 123 ) ) );

		Assert.AreEqual( 0, Running.Length, "the handle names no emitter" );
		Assert.IsFalse( Counted( "LOADED_EMITTER" ) );

		// Killed in the file: the first tick after a load frees it.
		Rides( WithTheBubblesEmitter( emitter => System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian( emitter[0x20..], -2 ) ) );

		Assert.AreEqual( 0, Running.Length, "a killed emitter is not started again" );
		Assert.IsFalse( Counted( "LOADED_EMITTER" ) );

		// Part way through a life: begun again, and counted.
		Rides( WithTheBubblesEmitter( emitter => System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian( emitter[0x20..], 5 ) ) );

		Assert.AreEqual( Bubbles, Running.Single().Effect );
		Assert.AreEqual( 0, Running.Single().Lifetime, "its template's life, not the file's" );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( entry => entry.What == "LOADED_EMITTER_LIFE_PART_RUN" ).Times );
	}

	/// <summary>
	/// <b>What cannot be placed is counted and starts nothing</b>: an effect of the item's own, a record with no
	/// node, and a node the model does not hold. Each keeps its record with no emitter.
	/// </summary>
	[TestMethod]
	public void AParticleWithNoPlaceIsCountedAndNotStarted()
	{
		var rides = Rides( shipped );
		var before = Running.Length;

		var script = OnTheToilet( rides,
			Word( Opcode.ADDOBJ ), 1, 1, 0x8001, 1,
			Word( Opcode.ADDOBJ ), 1, -1, Flies, 2,
			Word( Opcode.ADDOBJ ), 1, 77, Flies, 3,
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( before, Running.Length );
		Assert.AreEqual( 3, script.Effects!.Count );
		Assert.IsTrue( script.Effects.Records.All( entry => entry.Emitter == 0 ) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( entry => entry.What == "PARK_PARTICLE_ITEM_EFFECT" ).Times );
		Assert.AreEqual( 2, Unimplemented.Summary.Single( entry => entry.What == "PARK_PARTICLE_NO_PLACE" ).Times );
	}

	/// <summary>A start the system refuses, an effect its library does not hold, is counted and keeps its record.</summary>
	[TestMethod]
	public void AStartTheSystemRefusesIsCounted()
	{
		var rides = Rides( shipped );
		var script = OnTheToilet( rides, Word( Opcode.ADDOBJ ), 1, 1, 5000, 1, Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.Effects!.Records.Single().Emitter );
		Assert.IsTrue( Counted( "PARK_PARTICLE_NOT_STARTED" ) );
	}

	/// <summary>
	/// <b>A smoking car's smoke</b> is effect 2 started where the boat's emitter node is drawn, the height the
	/// system's second axis, each times 1024 and cut; the next frame moves that emitter and starts no other.
	/// </summary>
	[TestMethod]
	public void ASmokingCarsSmokeStartsAtItsNodeAndIsMovedWithIt()
	{
		var before = Running.Length;
		var held = ParkBumperBoats.Smoke( system, 0, smoking: true, new Vector3( 442.6575f, 263.3747f, 30.4622f ) );

		Assert.AreNotEqual( 0, held );
		Assert.AreEqual( before + 1, Running.Length );
		Assert.IsTrue( system.TryEmitter( held, out var emitter ) );
		Assert.AreEqual( 2, emitter.Effect, "the library's Smoke" );
		Assert.AreEqual( (28330, 1949, 16855), (emitter.X, emitter.Y, emitter.Z), "the original's own file's first car: times 1024, cut, shifted down four" );
		Assert.IsNull( emitter.Aim );
		Assert.IsFalse( emitter.Template.OnScreen );

		var moved = ParkBumperBoats.Smoke( system, held, smoking: true, new Vector3( 431.5f, 268.25f, 30.75f ) );

		Assert.AreEqual( held, moved );
		Assert.AreEqual( before + 1, Running.Length );
		Assert.AreEqual( (27616, 1968, 17168), (emitter.X, emitter.Y, emitter.Z) );
		Assert.AreEqual( system.Library.Effects[ParkBumperCars.SmokeEffect].Lifetime, emitter.Lifetime, "still running" );

		// It emits from where it stands now.
		for ( var tick = 0; tick < 8; ++tick )
			system.Tick();

		Assert.IsTrue( emitter.Count > 0 );
		Assert.IsFalse( Counted( "PARK_PARTICLE" ) );
	}

	/// <summary>A car that smokes no more has its emitter stopped and its handle let go; one that never smoked starts none.</summary>
	[TestMethod]
	public void ACarFixedHasItsSmokeStopped()
	{
		var before = Running.Length;

		Assert.AreEqual( 0, ParkBumperBoats.Smoke( system, 0, smoking: false, new Vector3( 1, 2, 3 ) ) );
		Assert.AreEqual( before, Running.Length );

		var held = ParkBumperBoats.Smoke( system, 0, smoking: true, new Vector3( 1, 2, 3 ) );

		Assert.IsTrue( system.TryEmitter( held, out var emitter ) );
		Assert.AreEqual( 0, ParkBumperBoats.Smoke( system, held, smoking: false, new Vector3( 1, 2, 3 ) ) );
		Assert.AreEqual( -2, emitter.Lifetime, "stopped as a kill stops it" );

		// Broken again, it smokes from a fresh emitter.
		var again = ParkBumperBoats.Smoke( system, 0, smoking: true, new Vector3( 1, 2, 3 ) );

		Assert.AreNotEqual( 0, again );
		Assert.AreNotEqual( held, again );
	}

	/// <summary>
	/// With no node drawn nothing is started and a running smoke is left as it is; with no particle system the handle
	/// is kept; a handle whose emitter has gone is started again; and a start the system refuses is counted.
	/// </summary>
	[TestMethod]
	public void ASmokeWithNoNodeOrNoSystemIsLeftAndARefusedStartCounted()
	{
		var before = Running.Length;

		Assert.AreEqual( 0, ParkBumperBoats.Smoke( system, 0, smoking: true, null ) );
		Assert.AreEqual( before, Running.Length );

		var held = ParkBumperBoats.Smoke( system, 0, smoking: true, new Vector3( 10, 20, 30 ) );

		Assert.IsTrue( system.TryEmitter( held, out var emitter ) );

		var at = (emitter.X, emitter.Y, emitter.Z);

		Assert.AreEqual( held, ParkBumperBoats.Smoke( system, held, smoking: true, null ) );
		Assert.AreEqual( at, (emitter.X, emitter.Y, emitter.Z) );
		Assert.AreEqual( held, ParkBumperBoats.Smoke( null, held, smoking: false, new Vector3( 10, 20, 30 ) ) );
		Assert.AreNotEqual( -2, emitter.Lifetime );

		// A handle of another count names no emitter: the smoke is started afresh.
		var stale = held + 0x20000;

		Assert.AreNotEqual( 0, stale );
		Assert.IsFalse( system.TryEmitter( stale, out _ ) );

		var fresh = ParkBumperBoats.Smoke( system, stale, smoking: true, new Vector3( 10, 20, 30 ) );

		Assert.AreNotEqual( 0, fresh );
		Assert.AreNotEqual( stale, fresh );
		Assert.AreEqual( before + 2, Running.Length );
		Assert.IsFalse( Counted( "PARK_PARTICLE_NOT_STARTED" ) );

		// Every emitter taken: the start is refused.
		for ( var taken = 0; taken < ParticleSystem.EmitterCount; ++taken )
			system.Spawn( ParkBumperCars.SmokeEffect, 0, 0, 0 );

		Assert.AreEqual( 0, ParkBumperBoats.Smoke( system, 0, smoking: true, new Vector3( 10, 20, 30 ) ) );
		Assert.IsTrue( Counted( "PARK_PARTICLE_NOT_STARTED" ) );
	}

	/// <summary>
	/// <b>A sprite's place and size in the park</b>: the particle's position over 64, a half height of its size
	/// doubled over 2048 and then over 2 cos 45, a half width of that times the picture's shape and over 1.5 cos 45.
	/// The doubled size is a sixteen-bit word. An effect with a scale draws a particle that far out from its emitter.
	/// </summary>
	[TestMethod]
	public void ASpriteStandsAtItsParticleOverSixtyFourAndIsSizedByThePass()
	{
		var plain = system.Library.Effects.First( effect => effect.Scale == 1000 );
		var emitter = new Emitter { Template = plain, X = 100 * 64, Y = 10 * 64, Z = 200 * 64 };
		var particle = new Particle { X = (104 * 64) + 32, Y = 20 * 64, Z = 190 * 64, Size = 1024 };

		var sprite = WorldParticles.SpriteOf( emitter, particle, 1f );

		Assert.AreEqual( 104.5f, sprite.X );
		Assert.AreEqual( 20f, sprite.Height );
		Assert.AreEqual( 190f, sprite.Z );
		Assert.AreEqual( 0.70710677f, sprite.HalfHeight, 0.00001f );
		Assert.AreEqual( 0.94280905f, sprite.HalfWidth, 0.00001f );

		var wide = WorldParticles.SpriteOf( emitter, particle, 0.75f );

		Assert.AreEqual( 0.70710677f, wide.HalfWidth, 0.00001f, "an effect with no picture is square on a 4:3 screen" );
		Assert.AreEqual( 0.75f, WorldParticles.PlainAspect );

		// 40000 doubled runs past sixteen bits: 80000 & 0xffff = 14464.
		var big = WorldParticles.SpriteOf( emitter, particle with { Size = 40000 }, 1f );

		Assert.AreEqual( 14464 / 1024f * 0.5f * WorldParticles.UnitsDown, big.HalfHeight, 0.00001f );

		// An effect of a scale of its own, 500 at +0xa2: halfway out from its emitter.
		var record = new byte[ParticleEffectTemplate.RecordSize];

		System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian( record.AsSpan( 0xa2 ), 500 );

		var scaled = new ParticleEffectTemplate( record );

		Assert.AreEqual( 500, scaled.Scale );

		var drawn = WorldParticles.SpriteOf( new Emitter { Template = scaled, X = emitter.X, Y = emitter.Y, Z = emitter.Z }, particle, 1f );

		Assert.AreEqual( 102.25f, drawn.X );
		Assert.AreEqual( 15f, drawn.Height );
		Assert.AreEqual( 195f, drawn.Z );
	}

	/// <summary>The world's drawer takes an emitter that is running, holds particles, is shown and is not the screen's.</summary>
	[TestMethod]
	public void TheWorldDrawsARunningShownEmitterOfTheWorld()
	{
		var world = system.Library.Effects[Bubbles];
		var screen = system.Library.Effects.First( effect => effect.OnScreen );

		Assert.IsFalse( world.OnScreen );
		Assert.IsTrue( WorldParticles.Draws( new Emitter { Template = world, Active = true, Count = 1 } ) );
		Assert.IsFalse( WorldParticles.Draws( new Emitter { Template = world, Active = false, Count = 1 } ) );
		Assert.IsFalse( WorldParticles.Draws( new Emitter { Template = world, Active = true, Count = 0 } ) );
		Assert.IsFalse( WorldParticles.Draws( new Emitter { Template = world, Active = true, Count = 1, Hidden = true } ) );
		Assert.IsFalse( WorldParticles.Draws( new Emitter { Template = screen, Active = true, Count = 1 } ) );
	}

	/// <summary>
	/// <b>A loaded jet carries on aimed as its file holds it</b>: a park written while the Spray's jet runs is
	/// loaded, and the emitter started for the record throws its particles at the file's own three words.
	/// </summary>
	[TestMethod]
	public void ALoadedJetCarriesOnAimedAsTheFileHoldsIt()
	{
		var first = Rides( shipped );
		var state = new ParkState( shipped );
		var spray = first.Scheduler.Find( first.ScriptFor( Spray ) )!;

		spray.Effects!.Add( 2, 2, Jet, 1, spray.Nodes!.EffectIndex( 2, 2 ) );

		int ChannelsFor( int id ) => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1;

		var things = first.Written( shipped, state.WrittenObjects( shipped ), ChannelsFor, state.HoardingFor )!;
		var carried = new ParkFileWriter.Running( shipped.GameTick, shipped.ParkClosed != 0, shipped.NumberOfVisitorsToDate,
			shipped.Economy!.Value.Balance, shipped.Camera.Saved!.Value ) { Things = things };
		var file = new ParkWorld( ParkFileWriter.Body( shipped, carried, out _, out _ ) );

		Assert.IsNull( file.Problem );

		system.Shutdown();
		system = new ParticleSystem( "Particle/Tp2.plb", 1000 );
		Unimplemented.Forget();

		var second = Rides( file );
		var jet = Running.Single( emitter => emitter.Effect == Jet );

		Assert.AreEqual( Units( 525, 5, 311 ), PlaceOf( jet ) );
		Assert.AreEqual( new ParticleVector( 0, 25, 30 ), jet.Aim );
		Assert.IsTrue( system.TryEmitter( second.Scheduler.Find( second.ScriptFor( Spray ) )!.Effects!.Records.Single( entry => entry.Type == 2 ).Emitter, out var named ) );
		Assert.AreSame( jet, named );
		Assert.AreEqual( 2, Running.Length, "and the shop's bubbles" );

		// Counted exactly where the file's life is not the template's own.
		var saved = file.Particles.At( file.ScriptStates.For( spray.Id )!.Value.Effects!.Single( entry => entry.Type == 2 ).Handle & 0xffff );

		Assert.AreEqual( saved.Life != system.Library.Effects[Jet].Lifetime, Counted( "LOADED_EMITTER_LIFE_PART_RUN" ) );
	}
}
