using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Selling a thing standing in a park: what goes with it. These read the shipped Lost Kingdom and are skipped
/// where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>Each calls the code its mutation changes</b> rather than re-deriving what it should have done: the
/// selling tests call <see cref="ParkBuilding.Sell(ParkState, ParkWorld, ParkItemCatalogue, ParkObjects?,
/// ParkRides?, int, ParkPeople?)"/>, the footprint tests <see cref="ParkBuilding.Unstamp"/>, which Sell clears the cells
/// with, and the particle test the item catalogue. So putting a fault back turns one of them red.
/// </para>
/// </summary>
[TestClass]
public class ParkSellTests
{
	private BaseFileSystem data = null!;

	/// <summary>The entities a test made, put away with it - the shape <c>ParkRidesTests</c> uses.</summary>
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

	/// <summary>The shipped park's Belly Bounce, anchored (51,23), covering (51,23)..(53,26).</summary>
	private const int BellyBounceThing = 13;

	/// <summary>The shipped park's gates - UI type 4, standing on no cell of their own.</summary>
	private const int GatesThing = 11;

	/// <summary>The Hot Pot, whose script spawns a sound script on its first turn.</summary>
	private const int HotPotItem = 1140;

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

	/// <summary>A footprint cell as <see cref="ParkBuilding.Stamp"/> leaves it, owned by the thing at (10,10).</summary>
	private static void StampAt( ParkState state, ParkWorld world, int x, int y, int type, short counter = 0,
		ushort flags = 0 )
		=> state.SetRecord( x, y, ParkState.CellFor( world, x, y ) with
		{
			Type = type,
			Flags = flags,
			OverlapCounter = counter,
			ParentId = (ushort)MapStep.CellId( 10, 10 )
		} );

	/// <summary>
	/// <b>A sold thing's script is taken down</b>, not left running for a thing that is gone - the object
	/// destructor's call of the script teardown (<c>0x004dd2c9</c>). The Belly Bounce's script spawns
	/// nothing, so exactly one script leaves the scheduler.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> dropping the unbind from Sell, or keeping the thing's entry in the map, fails the
	/// first assertion; forgetting to take it off the animated count fails the last.
	/// </remarks>
	[TestMethod]
	public void SellingAThingTheSaveBuiltTakesItsScriptDown()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );
		var rides = Bind( world, catalogue );

		var id = rides.ScriptFor( BellyBounceThing );
		var script = rides.Scheduler.Find( id );

		Assert.IsNotNull( script, "the Belly Bounce should be running a script before it is sold" );

		var running = rides.Scheduler.Count;
		var bound = rides.Bound;
		var animated = rides.Animated;
		var withClips = script!.Animations?.Loaded > 0 ? 1 : 0;
		var particles = TimesReported( "DESTROY_PARTICLE_EFFECT" );

		var reply = ParkBuilding.Sell( state, world, catalogue, null, rides, BellyBounceThing );

		StringAssert.StartsWith( reply, "sell: 'Belly Bounce'" );

		Assert.AreEqual( 0, rides.ScriptFor( BellyBounceThing ), "a sold thing names no script" );
		Assert.IsNull( rides.Scheduler.Find( id ), "and its script is no longer in the scheduler" );
		Assert.AreEqual( running - 1, rides.Scheduler.Count, "one script fewer running" );
		Assert.AreEqual( bound - 1, rides.Bound, "one thing fewer bound" );
		Assert.AreEqual( particles + 1, TimesReported( "DESTROY_PARTICLE_EFFECT" ),
			"its death particle, which nothing draws, is counted" );
		Assert.AreEqual( 1, withClips, "the Belly Bounce's model ships clips" );
		Assert.AreEqual( animated - withClips, rides.Animated, "and one fewer can see its own animations" );
	}

	/// <summary>
	/// <b>What a sold thing's script spawned goes down with it</b>, because the teardown takes its sound
	/// script and its child (<c>FUN_00559060</c>). The Hot Pot spawns its sound script on its first turn,
	/// so selling one bought here leaves the scheduler as it was before the buy.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the script out with <see cref="RideScriptScheduler.Remove"/> rather than
	/// <see cref="RideScriptScheduler.Destroy"/> leaves the sound script running and fails the count;
	/// counting the bought thing under anything but its own id leaves the animated count one high.
	/// </remarks>
	[TestMethod]
	public void SellingABoughtThingTakesWhatItsScriptSpawnedWithIt()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );
		var rides = Bind( world, catalogue );

		Assert.IsTrue( catalogue.TryGet( HotPotItem, out var item ), "the jungle offers a Hot Pot" );

		var placed = new ParkWorld.CatalogueObject( ThingId: BoughtThing, CatalogueId: HotPotItem,
			RawX: 20 << 8, RawY: 12 << 8, Angle: 0 );

		state.AddObject( placed );

		var before = rides.Scheduler.Count;
		var animated = rides.Animated;

		Assert.IsTrue( rides.BindNew( placed, item ), "a Hot Pot has a script to run" );

		var script = rides.Scheduler.Find( rides.ScriptFor( BoughtThing ) )!;

		Assert.IsTrue( script.Animations?.Loaded > 0, "the Hot Pot's model ships clips" );
		Assert.AreEqual( animated + 1, rides.Animated, "a bought thing that can see its animations is counted" );

		for ( var tick = 1; tick <= 16 && script.SoundChildId == 0; ++tick )
			rides.Scheduler.Advance( tick * 31f );

		var sound = script.SoundChildId;

		Assert.AreNotEqual( 0, sound, "its first turn spawns a sound script" );
		Assert.AreEqual( before + 2, rides.Scheduler.Count, "the Hot Pot's script and the one it spawned" );

		StringAssert.StartsWith( ParkBuilding.Sell( state, world, catalogue, null, rides, BoughtThing ), "sell: '" );

		Assert.IsNull( rides.Scheduler.Find( sound ), "the spawned sound script went with it" );
		Assert.AreEqual( before, rides.Scheduler.Count, "the scheduler is back where it was before the buy" );
		Assert.AreEqual( animated, rides.Animated, "and selling it takes it back off the animated count" );
	}

	/// <summary>
	/// <b>The gates cannot be sold</b>, so their script, which is what lets a guest in, stays running. The
	/// same test <see cref="ParkBuilding.Buy"/> makes. They are bound here by hand because a park built
	/// without models binds only what the save placed, and the gates stand on no cell of their own.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> dropping the test sells them; taking the script down before it is made leaves them
	/// standing with their script gone.
	/// </remarks>
	[TestMethod]
	public void TheGatesCannotBeSold()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );
		var rides = Bind( world, catalogue );

		var gates = world.Objects.Single( candidate => candidate.ThingId == GatesThing );

		Assert.IsTrue( catalogue.TryGet( gates.CatalogueId, out var item ), "the gates' item" );
		Assert.IsTrue( rides.BindNew( gates, item ), "the gates run a script" );

		var script = rides.ScriptFor( GatesThing );
		var running = rides.Scheduler.Count;

		var reply = ParkBuilding.Sell( state, world, catalogue, null, rides, GatesThing );

		StringAssert.Contains( reply, "which nothing can demolish" );
		Assert.IsTrue( state.TryObject( GatesThing, out _ ), "the gates are still standing" );
		Assert.AreEqual( script, rides.ScriptFor( GatesThing ), "they still name their script" );
		Assert.IsNotNull( rides.Scheduler.Find( script ), "which is still running" );
		Assert.AreEqual( running, rides.Scheduler.Count, "nothing left the scheduler" );
	}

	/// <summary>
	/// <b>A thing the save built leaves bare ground when it is sold</b>, not the save's record of it: every
	/// cell of the Belly Bounce's footprint becomes the cleared cell <c>FUN_005367a0</c> leaves - type
	/// nought, no owner, no links, no flags, tile 55 - and the queue node before its entrance goes with its
	/// overlap counter at nought. See <c>docs/exe/park-engine.md</c>, "The demolisher's order, and the cells
	/// it leaves".
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> clearing with <see cref="ParkState.ClearRecord"/> fails the first cell; the tile
	/// left at nought fails the tile; the drain keeping the node's counter fails the last assertion.
	/// </remarks>
	[TestMethod]
	public void SellingAThingTheSaveBuiltLeavesBareGround()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );

		Assert.AreEqual( 1, (int)ParkState.CellFor( world, 52, 22 ).OverlapCounter, "the save lays the node at one" );

		ParkBuilding.Sell( state, world, catalogue, null, null, BellyBounceThing );

		var cells = 0;

		for ( var y = 23; y <= 26; ++y )
		{
			for ( var x = 51; x <= 53; ++x )
			{
				var cell = ParkState.CellFor( world, x, y );
				var at = $"({x},{y})";

				Assert.AreEqual( CellEdge.Nothing, cell.Type, $"{at} is bare ground" );
				Assert.AreEqual( 0, (int)cell.ParentId, $"{at} names no owner" );
				Assert.AreEqual( 0, (int)cell.Neighbours, $"{at} links nowhere" );
				Assert.AreEqual( 0, (int)cell.Direction, $"{at} has no direction" );
				Assert.AreEqual( 0, (int)cell.Flags, $"{at} has no flags" );
				Assert.AreEqual( 55, cell.TileIndex, $"{at} is tiled as bare ground" );
				Assert.AreEqual( 0, (int)cell.OverlapCounter, $"{at} counts nothing laid over it" );
				Assert.IsFalse( ParkObjects.CoversGround( cell ), $"{at} lets the ground be drawn" );

				++cells;
			}
		}

		Assert.AreEqual( 12, cells, "the Belly Bounce covers twelve cells" );

		var node = ParkState.CellFor( world, 52, 22 );

		Assert.AreEqual( CellEdge.Nothing, node.Type, "the queue node is drained" );
		Assert.AreEqual( 0, (int)node.OverlapCounter, "and its counter is nought" );
	}

	/// <summary>
	/// <b>Land outside the park stays outside it.</b> The original clears the whole flag word and never
	/// stands a thing on such land; here the placement verdict is unbuilt, so a thing can, and clearing the
	/// flag would make that land part of the park.
	/// </summary>
	/// <remarks><b>Mutation:</b> clearing the whole word, as the original does, fails the assertion.</remarks>
	[TestMethod]
	public void ClearingAFootprintKeepsLandOutsideThePark()
	{
		var world = World();
		var state = new ParkState( world );

		StampAt( state, world, 10, 10, 4, flags: (ushort)(ParkPathBuilding.OutsideThePark | ParkPathBuilding.NoModify) );

		ParkBuilding.Unstamp( state, (10, 10, 10, 10), 10, 10, BoughtThing );

		Assert.AreEqual( ParkPathBuilding.OutsideThePark, (int)ParkState.CellFor( world, 10, 10 ).Flags,
			"only the outside-the-park flag survives" );
	}

	/// <summary>
	/// <b>A cleared end forgets how often it was built over, and a cleared body cell does not</b>: the clear's
	/// arms zero the counter of the type 9 and 10 cells and leave type 4's (<c>FUN_005367a0</c>). The shipped
	/// park's ends are all saved at nought, so only a counter set here tells the two apart.
	/// </summary>
	/// <remarks><b>Mutation:</b> keeping the end's counter fails the first assertion.</remarks>
	[TestMethod]
	public void ClearingAFootprintForgetsItsEndsCounters()
	{
		var world = World();
		var state = new ParkState( world );

		StampAt( state, world, 10, 10, CellEdge.RideEnd, counter: 2 );
		StampAt( state, world, 11, 10, CellEdge.RideFarEnd, counter: 2 );
		StampAt( state, world, 12, 10, 4, counter: 2 );

		ParkBuilding.Unstamp( state, (10, 10, 12, 10), 10, 10, BoughtThing );

		Assert.AreEqual( 0, (int)ParkState.CellFor( world, 10, 10 ).OverlapCounter, "the entrance's counter goes" );
		Assert.AreEqual( 0, (int)ParkState.CellFor( world, 11, 10 ).OverlapCounter, "the exit's counter goes" );
		Assert.AreEqual( 2, (int)ParkState.CellFor( world, 12, 10 ).OverlapCounter, "a body cell's is kept" );
	}

	/// <summary>
	/// <b>A cell of the rectangle the thing does not own is left alone</b> - the shape's empty cells, which
	/// the demolisher skips. Here a path runs through the middle of a three-cell rectangle.
	/// </summary>
	/// <remarks><b>Mutation:</b> clearing every cell of the rectangle takes the path up.</remarks>
	[TestMethod]
	public void ClearingAFootprintLeavesCellsItDoesNotOwn()
	{
		var world = World();
		var state = new ParkState( world );

		StampAt( state, world, 10, 10, 4 );
		state.SetRecord( 11, 10, ParkState.CellFor( world, 11, 10 ) with { Type = CellEdge.Path, ParentId = 0 } );
		StampAt( state, world, 12, 10, 4 );

		ParkBuilding.Unstamp( state, (10, 10, 12, 10), 10, 10, BoughtThing );

		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( world, 11, 10 ).Type, "the path it did not own stays" );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( world, 12, 10 ).Type, "the cell it owned is cleared" );
	}

	/// <summary>
	/// <b>Terrain the original never builds on comes back as it was.</b> (45,17), beside the gate, is saved
	/// as type 7 inside the park; the unbuilt placement verdict lets a thing stand there, and selling it must
	/// not leave walkable bare ground where the save has rock.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> clearing it like any other cell leaves type nought; giving back the type alone
	/// leaves the entrance's direction behind.
	/// </remarks>
	[TestMethod]
	public void ClearingAFootprintGivesBackTerrainTheOriginalNeverBuildsOn()
	{
		var world = World();
		var state = new ParkState( world );
		var saved = world.CellAt( 45, 17 );

		Assert.AreEqual( 7, saved.Type, "the save has (45,17) as type 7" );

		// Stamped, and marked as an entrance facing south, as a one-cell Litter Bin is.
		ParkBuilding.Stamp( state, (45, 17, 45, 17), 45, 17 );
		state.SetRecord( 45, 17, state.Record( 45, 17 ) with { Type = CellEdge.RideEnd, Direction = 0x10 } );
		ParkBuilding.Unstamp( state, (45, 17, 45, 17), 45, 17, BoughtThing );

		Assert.AreEqual( saved, ParkState.CellFor( world, 45, 17 ), "it is the save's own cell again, type 7" );
	}

	/// <summary>
	/// <b>The particle a demolished thing gives off is read per item</b>, falling back to its category:
	/// rides 75, sideshows 76, shops 77, and features 78 - which the Round Fountain overrides to 77 and the
	/// gates to 0. Measured across all 274 item archives: 69 override the key, every one of them a feature.
	/// </summary>
	/// <remarks><b>Mutation:</b> not reading the key answers nought for all five.</remarks>
	[TestMethod]
	public void TheDestroyParticleIsReadPerItem()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );

		int Particle( int thingId )
		{
			var placed = world.Objects.Single( candidate => candidate.ThingId == thingId );

			Assert.IsTrue( catalogue.TryGet( placed.CatalogueId, out var item ), $"thing {thingId}'s item" );

			return item.DestroyParticleEffect;
		}

		Assert.AreEqual( 75, Particle( BellyBounceThing ), "a ride - Rides.sam" );
		Assert.AreEqual( 76, Particle( 14 ), "the Jungle Spray, a sideshow - SideShow.sam" );
		Assert.AreEqual( 77, Particle( 16 ), "the Drinks Shop, a shop - Shops.sam" );
		Assert.AreEqual( 77, Particle( 24 ), "the Round Fountain overrides Features.sam's 78" );
		Assert.AreEqual( 0, Particle( GatesThing ), "the gates override it to nothing" );
	}
}
