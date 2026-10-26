using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What building a thing gives off: the item's <c>Info.CreateParticleEffect</c>, which the original's script loader
/// starts as a bought or moved thing's script starts and never as a park loads (<c>docs/exe/park.md</c>, "What building a
/// thing gives off"). Counted here, not drawn. These read the shipped Lost Kingdom and are skipped where there is no
/// installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkBuildParticleTests
{
	private BaseFileSystem data = null!;

	private readonly List<Entity> made = [];

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
	}

	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private const int GatesThing = 11;
	private const int BellyBounceThing = 13;
	private const int JungleSprayThing = 14;
	private const int DrinksShopThing = 16;
	private const int RoundFountainThing = 24;

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

	private static int TimesReported( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

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
	/// <b>A bought thing reaches its create effect, a loaded one does not.</b> The loader spawns it only for a thing
	/// built in play (its third argument, <c>DAT_00785a2c</c>, nought), only when the item names one
	/// (<c>0x00558e7f</c>). A load does reach the loader, for the gates and the lights (<c>FUN_005156a0</c>, through
	/// <c>0x004db090</c>), and starts nothing because their items declare 0; the rest of a park's things come back through
	/// the save-state reader, which does not reach it. So the shipped park bound as it loads counts nothing, a Round
	/// Fountain bought counts one, and the gates, declaring 0, none.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the count out leaves the fountain's at nought. Not run: dropping the effect's test (it
	/// would count the gates) and counting where the load binds (it would count the shipped park's things); the
	/// assertions on the gates and the load answer each.
	/// </remarks>
	[TestMethod]
	public void OnlyABoughtThingReachesItsCreateParticle()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );

		var before = TimesReported( "CREATE_PARTICLE_EFFECT" );
		var rides = Bind( world, catalogue );

		Assert.IsTrue( rides.Scheduler.Count > 0, "the shipped park's things run their scripts" );
		Assert.AreEqual( before, TimesReported( "CREATE_PARTICLE_EFFECT" ), "a load starts no create effect" );

		var gates = world.Objects.Single( candidate => candidate.ThingId == GatesThing );

		Assert.IsTrue( catalogue.TryGet( gates.CatalogueId, out var gatesItem ), "the gates' item" );
		Assert.IsTrue( rides.BindNew( gates with { ThingId = BoughtThing + 1 }, gatesItem ), "the gates run a script" );
		Assert.AreEqual( before, TimesReported( "CREATE_PARTICLE_EFFECT" ), "effect nought starts nothing" );

		var fountain = world.Objects.Single( candidate => candidate.ThingId == RoundFountainThing );

		Assert.IsTrue( catalogue.TryGet( fountain.CatalogueId, out var fountainItem ), "the Round Fountain's item" );
		Assert.IsTrue( rides.BindNew( fountain with { ThingId = BoughtThing }, fountainItem ), "it runs a script" );
		Assert.AreEqual( before + 1, TimesReported( "CREATE_PARTICLE_EFFECT" ), "a bought fountain reaches its 81" );
	}

	/// <summary>
	/// <b>Nothing is counted where the script did not spawn</b> (<c>ParkRides.BindNew</c> returns before the count): a Round
	/// Fountain whose script is not there reports no create effect.
	/// </summary>
	/// <remarks><b>Mutation:</b> the count moved above <c>Scheduler.Spawn</c> in <c>BindNew</c> (counts the scriptless one).</remarks>
	[TestMethod]
	public void NothingIsCountedWhereTheScriptDidNotSpawn()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = Bind( world, catalogue );
		var fountain = world.Objects.Single( candidate => candidate.ThingId == RoundFountainThing );

		Assert.IsTrue( catalogue.TryGet( fountain.CatalogueId, out var item ), "the Round Fountain's item" );
		Assert.AreNotEqual( 0, item.CreateParticleEffect );

		var before = TimesReported( "CREATE_PARTICLE_EFFECT" );

		Assert.IsFalse( rides.BindNew( fountain with { ThingId = BoughtThing + 2 }, item with { Stem = "noSuchScript" } ), "no script" );
		Assert.AreEqual( before, TimesReported( "CREATE_PARTICLE_EFFECT" ), "a script that did not spawn counts nothing" );
	}
}
