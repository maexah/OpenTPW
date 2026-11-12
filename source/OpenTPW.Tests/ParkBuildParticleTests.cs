using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What building a thing gives off: the item's <c>Info.CreateParticleEffect</c>, which the original's script loader
/// starts as a bought or moved thing's script starts and never as a park loads (<c>docs/exe/park.md</c>, "What building a
/// thing gives off"), over its model's box (<see cref="ParkCreateEffect"/>). These read the shipped Lost Kingdom and are
/// skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkBuildParticleTests
{
	private BaseFileSystem data = null!;

	private ParticleSystem system = null!;

	private readonly List<Entity> made = [];

	/// <summary>The shipped park's particle density, at which the original was measured (Q663).</summary>
	private const int ShippedDensity = 500;

	[TestInitialize]
	public void MountTheGame()
	{
		Log ??= new();
		data = FileSystem = GameData.Required();
		system = new ParticleSystem( "Particle/Tp2.plb", ShippedDensity );

		using var ground = data.OpenRead( "levels/jungle/terrain/base.MD2" )!;
		ParkCreateEffect.Ground = new HeightfieldFile( ground );
	}

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();

		// Null where the game is absent and the set-up skipped before making them.
		system?.Shutdown();
		ParkCreateEffect.Ground = null;
	}

	/// <summary>The emitters of the world that are running.</summary>
	private Emitter[] Running => [.. system.Emitters.Where( emitter => emitter.Active && !emitter.Template.OnScreen )];

	/// <summary>The emitters of the world running now that were not in <paramref name="before"/> (the park's own, started as it binds).</summary>
	private Emitter[] StartedSince( Emitter[] before ) => [.. Running.Except( before )];

	/// <summary>The four create effects the jungle's items name, 79 to 82.</summary>
	private Emitter[] CreateEffects => [.. Running.Where( emitter => emitter.Effect is >= 79 and <= 82 )];

	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int GatesThing = 11;
	private const int BellyBounceThing = 13;
	private const int JungleSprayThing = 14;
	private const int DrinksShopThing = 16;
	private const int RoundFountainThing = 24;

	/// <summary>The Huge Hollow Rock, whose model stands a row off its anchor (Q744).</summary>
	private const int HollowRockItem = 1427;

	/// <summary>An id nothing in the shipped park uses, for a thing bought by a test.</summary>
	private const int BoughtThing = 77;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkRides Bind( ParkWorld world, ParkItemCatalogue catalogue )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

		/// <summary>
	/// <b>The create effect is the item's own where it declares one and its category's otherwise</b>, read from
	/// <c>Info.CreateParticleEffect</c> and not its destroy twin: the Round Fountain declares the shops' 81 over
	/// <c>Features.sam</c>'s 82 (the effect the original started at its placing click, Q663), the gates 0.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> reading the category alone gives the fountain and the gates 82; reading the item alone gives
	/// the rides, sideshows and shops nought; reading <c>Info.DestroyParticleEffect</c> gives 75, 76, 77 and 77.
	/// </remarks>
	[TestMethod]
	public void TheCreateParticleIsReadPerItem()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );

		int Particle( int thingId )
		{
			var placed = world.Objects.Single( candidate => candidate.ThingId == thingId );

			Assert.IsTrue( catalogue.TryGet( placed.CatalogueId, out var item ), $"thing {thingId}'s item" );

			return item.CreateParticleEffect;
		}

		Assert.AreEqual( 79, Particle( BellyBounceThing ), "a ride - Rides.sam" );
		Assert.AreEqual( 80, Particle( JungleSprayThing ), "the Jungle Spray, a sideshow - SideShow.sam" );
		Assert.AreEqual( 81, Particle( DrinksShopThing ), "the Drinks Shop, a shop - Shops.sam" );
		Assert.AreEqual( 81, Particle( RoundFountainThing ), "the Round Fountain overrides Features.sam's 82" );
		Assert.AreEqual( 0, Particle( GatesThing ), "the gates override it to nothing" );
	}

	/// <summary>
	/// <b>A bought thing starts its create effect, a loaded one does not.</b> The loader starts it only for a thing built in
	/// play (its third argument, <c>DAT_00785a2c</c>, nought), only when the item names one (<c>0x00558e7f</c>). A load does
	/// reach the loader, for the gates and the lights (<c>FUN_005156a0</c>, through <c>0x004db090</c>), and starts nothing
	/// because their items declare 0; the rest of a park's things come back through the save-state reader, which does not
	/// reach it. So the shipped park bound as it loads starts no emitter, the gates bought, declaring 0, none, and a Round
	/// Fountain bought one: effect 81, life 30, at most 146 particles at the shipped park's density of 500, as the original's
	/// (<c>original/emitters.py</c>, Q663).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the start taken out of <c>BindNew</c> leaves no emitter; <c>Info.DestroyParticleEffect</c> started in
	/// its place fails it. Not run: dropping the effect's test (it would start the gates' nought, a running effect
	/// of its own) and starting where the load binds (it would start the shipped park's things); the assertions on the
	/// gates and the load answer each.
	/// </remarks>
	[TestMethod]
	public void OnlyABoughtThingStartsItsCreateParticle()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = Bind( world, catalogue );

		Assert.IsTrue( rides.Scheduler.Count > 0, "the shipped park's things run their scripts" );
		Assert.AreEqual( 0, CreateEffects.Length, "a load starts no create effect" );

		var before = Running;
		var gates = world.Objects.Single( candidate => candidate.ThingId == GatesThing );

		Assert.IsTrue( catalogue.TryGet( gates.CatalogueId, out var gatesItem ), "the gates' item" );
		Assert.IsTrue( rides.BindNew( gates with { ThingId = BoughtThing + 1 }, gatesItem ), "the gates run a script" );
		Assert.AreEqual( 0, StartedSince( before ).Length, "effect nought starts nothing" );

		var fountain = world.Objects.Single( candidate => candidate.ThingId == RoundFountainThing );

		Assert.IsTrue( catalogue.TryGet( fountain.CatalogueId, out var fountainItem ), "the Round Fountain's item" );
		Assert.IsTrue( rides.BindNew( fountain with { ThingId = BoughtThing }, fountainItem ), "it runs a script" );

		var started = StartedSince( before ).Single();

		Assert.AreEqual( (81, 30, 146), (started.Effect, started.Lifetime, started.MaxParticles), "the fountain's 81, as the original's" );
	}

	/// <summary>
	/// <b>The effect starts at the middle of its model's cells, at its base height, spread over half its box across and along
	/// and a quarter of its height</b> (<c>0x00558eab</c>..<c>0x00558fac</c>), in the emitter's units (a sixteenth of the
	/// 1024ths it is started at). The shipped park's Round Fountain, turned, covers (57,17)..(59,19), its base at nought and
	/// its <c>.hmp</c> box 34.0 high: (37440, 0, 11840), area (960, 544, 960). The Huge Hollow Rock bought at (19,27) unturned
	/// stands its model a row before its anchor (Q744): its cells (19,26)..(23,30), its base at -4.5 and its box 23.39 high:
	/// (13760, -288, 18240), area (1600, 374, 1600).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the anchor's footprint for the model's cells gives the rock z 18880; the ground under the anchor for
	/// the base height gives the rock y -384; the box's least corner for its middle gives the fountain (36480, 0, 10880); the
	/// box's full height for its quarter gives 2176 and 1496.
	/// </remarks>
	[TestMethod]
	[DataRow( RoundFountainThing, 0, 0, 0, 37440, 0, 11840, 960, 544, 960, DisplayName = "the shipped park's Round Fountain, turned" )]
	[DataRow( 0, HollowRockItem, 19, 27, 13760, -288, 18240, 1600, 374, 1600, DisplayName = "the Huge Hollow Rock, a row off its anchor" )]
	public void TheCreateEffectStartsOverItsModelsBox( int thingId, int itemId, int cellX, int cellY,
		int x, int y, int z, int areaX, int areaY, int areaZ )
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = Bind( world, catalogue );
		ParkWorld.CatalogueObject placed;
		ParkItemCatalogue.Item item;

		if ( thingId != 0 )
		{
			placed = world.Objects.Single( candidate => candidate.ThingId == thingId ) with { ThingId = BoughtThing };
			Assert.IsTrue( catalogue.TryGet( placed.CatalogueId, out item ), "the thing's item" );
		}
		else
		{
			Assert.IsTrue( catalogue.TryGet( itemId, out item ), "the item" );
			placed = ParkBuilding.Constructed( item, BoughtThing, cellX, cellY, 0, 1, 1, default );
		}

		var before = Running;

		Assert.IsTrue( rides.BindNew( placed, item ), "it runs a script" );

		var started = StartedSince( before ).Single();

		Assert.AreEqual( item.CreateParticleEffect, started.Effect );
		Assert.AreEqual( (x, y, z), (started.X, started.Y, started.Z), "the box's middle at its base height" );
		Assert.AreEqual( new ParticleVector( areaX, areaY, areaZ ), started.Area, "half the box across and along, a quarter of its height" );
	}

	/// <summary>
	/// <b>The particles are thrown out over the area set, not the template's</b> (the emitter's own <c>+0x44</c>, which
	/// <c>Particles_SetArea</c> writes): effect 81's template spreads its particles up to 249 above the emitter, and the
	/// fountain's area up to 544. Of those its first ticks throw out, each read where it was thrown, at least one stands higher
	/// than the template's area reaches and none higher than the area set.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Emit</c> reading the template's area: the highest of five at 241.</remarks>
	[TestMethod]
	public void ItsParticlesAreThrownOutOverTheAreaSet()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = Bind( world, catalogue );
		var fountain = world.Objects.Single( candidate => candidate.ThingId == RoundFountainThing );

		Assert.IsTrue( catalogue.TryGet( fountain.CatalogueId, out var item ) );

		var before = Running;

		Assert.IsTrue( rides.BindNew( fountain with { ThingId = BoughtThing }, item ) );

		var started = StartedSince( before ).Single();
		var heights = new List<int>();
		var seen = new HashSet<int>();

		// A particle stands where it was thrown out until the tick after the one that threw it, so each is read the tick it is new.
		for ( var tick = 0; tick < 6; ++tick )
		{
			system.Tick();

			for ( var index = started.FirstParticle; index >= 0; index = system.Particles[index].Next )
			{
				if ( seen.Add( index ) )
					heights.Add( system.Particles[index].Y - started.Y );
			}
		}

		Assert.IsTrue( heights.Count > 0, "its first ticks threw particles out" );
		Assert.AreEqual( 249, started.Template.Area.Y, "the template's own" );
		Assert.IsTrue( heights.Max() > 249, $"the highest at {heights.Max()} of {heights.Count}" );
		Assert.IsTrue( heights.Max() <= 544 && heights.Min() >= 0, $"between {heights.Min()} and {heights.Max()}" );
	}

	/// <summary>
	/// <b>Nothing is started where the script did not spawn</b> (<c>ParkRides.BindNew</c> returns before the start): a Round
	/// Fountain whose script is not there starts no create effect.
	/// </summary>
	/// <remarks><b>Mutation:</b> the start moved above <c>Scheduler.Spawn</c> in <c>BindNew</c> (starts the scriptless one's).</remarks>
	[TestMethod]
	public void NothingIsStartedWhereTheScriptDidNotSpawn()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = Bind( world, catalogue );
		var fountain = world.Objects.Single( candidate => candidate.ThingId == RoundFountainThing );

		Assert.IsTrue( catalogue.TryGet( fountain.CatalogueId, out var item ), "the Round Fountain's item" );
		Assert.AreNotEqual( 0, item.CreateParticleEffect );

		var before = Running;

		Assert.IsFalse( rides.BindNew( fountain with { ThingId = BoughtThing + 2 }, item with { Stem = "noSuchScript" } ), "no script" );
		Assert.AreEqual( 0, StartedSince( before ).Length, "a script that did not spawn starts nothing" );
	}
}
