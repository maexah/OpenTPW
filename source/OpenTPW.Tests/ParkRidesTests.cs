using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// Giving the things standing in a park the scripts they run. These read real game files and are skipped
/// where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// Everything under test here existed separately and had never been joined: the reader, the interpreter,
/// the scheduler and the catalogue were all built and shipping, and nothing in the tree ever handed a
/// placed thing its script. So these are the first tests in which a script belongs to something standing
/// in a park rather than to a harness.
/// </para>
/// </summary>
[TestClass]
public class ParkRidesTests
{
	private BaseFileSystem data = null!;

	/// <summary>
	/// Whatever entities a test made, so that they leave <see cref="Entity.All"/> with the test that made
	/// them rather than piling up for whatever runs next - see <c>EntityLifetimeTests</c>, which is where
	/// this shape comes from.
	/// </summary>
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

	/// <summary>
	/// Through a stream of this test's own bytes rather than the global file system, which belongs to a
	/// running game and which no test should need to have been set - the rule every other park test
	/// follows, and the reason <see cref="ParkItemCatalogue"/> and <see cref="ParkRides"/> both take one.
	/// </summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue Catalogue() => new( Theme, data );

	private ParkRides Bind( ParkWorld world, ParkItemCatalogue catalogue )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

	/// <summary>Whether the file system can offer this at all, treating "not there" and "will not open" alike.</summary>
	private bool Has( string path )
	{
		try
		{
			using var stream = data.OpenRead( path );

			return stream != null;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// <b>A bound script starts where the park file left it, not at its own first instruction.</b> This is
	/// what stops a loaded park playing everything in it being built again: the Belly Bounce's script
	/// opens with <c>WAITANIM 0 0</c>, which starts its construction clip, and the save has it parked
	/// twenty instructions past that - so binding has to put it at word 46 rather than at nought.
	///
	/// <para>
	/// The name is asserted beside the counter because resuming steps over the <c>NAME</c> the script
	/// opens with, and a nameless script cannot be found by <c>FINDSCRIPTRAND</c> - so a resume that
	/// gained the right counter and lost the name would have traded one fault for another.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ABoundScriptResumesWhereTheSaveLeftIt()
	{
		var world = World();
		var rides = Bind( world, Catalogue() );

		var id = rides.ScriptFor( BellyBounceThing );

		Assert.AreNotEqual( 0, id, "the Belly Bounce should have been given a script" );

		var script = rides.Scheduler.Find( id );

		Assert.IsNotNull( script, "and that script should be in the scheduler" );
		Assert.AreEqual( 46, script!.Position, "where its script was saved, and so where it must start" );
		Assert.AreNotEqual( 0, script.Position, "starting at nought is what replayed the construction clip" );

		Assert.IsTrue( script.IsNamed, "a resumed script still has to know its own name" );
		Assert.AreNotEqual( string.Empty, script.Name, "and to have taken it" );

		// <b>NotResumed is the load-bearing one</b>: it says every counter that was offered landed on an
		// instruction boundary, across all the scripts this park binds rather than just this one.
		//
		// What is deliberately NOT asserted is `Bound == Resumed`. That equality holds only because this
		// fixture passes no ParkObjects: with one, the second binding pass spawns things the save never
		// named - the ferry and the seaplane - which have no saved state and correctly do not resume, so
		// the running game has Bound > Resumed. Asserting it here would have pinned a property of the
		// fixture that production breaks.
		Assert.AreEqual( 0, rides.NotResumed, "no bound script was left at its beginning" );
		Assert.AreNotEqual( 0, rides.Resumed, "and something was actually resumed" );
		Assert.IsTrue( rides.Resumed <= rides.Bound,
			$"{rides.Resumed} resumed of {rides.Bound} bound" );

		// <b>And the other half, which nothing asserted until a review pointed out that gutting it left
		// the suite green.</b> Resuming a script past its prologue means it never runs the LOOPANIM in
		// that prologue again, so a thing whose channels are not also put back stands frozen for the
		// whole session. These are named things rather than a count, because a count here would be a
		// property of this fixture - it binds no ParkObjects, so three of the fourteen never arrive.
		Assert.AreNotEqual( 0, rides.ChannelsRestored, "some channel was put back" );

		int RoleOn( int thing, int channel = 0 )
		{
			var script = rides.Scheduler.Find( rides.ScriptFor( thing ) );

			Assert.IsNotNull( script, $"thing {thing} should be running a script" );
			Assert.IsNotNull( script!.Animations, $"and thing {thing} should have animation players" );

			return script.Animations!.Channel( channel )?.AnimID ?? -1;
		}

		// The Fountain is the case the regression was found on: its script resumes into a two-instruction
		// loop that can never reach the LOOPANIM that starts this clip, so only the restore puts it on.
		Assert.AreEqual( 5, RoleOn( FountainThing ), "the Fountain's saved role" );

		// All three of the sideshow's lanes, which is what needs the per-item channel count to be right.
		Assert.AreEqual( 2, RoleOn( JungleSprayThing, 0 ), "the Jungle Spray's first lane" );
		Assert.AreEqual( 2, RoleOn( JungleSprayThing, 1 ), "its second" );
		Assert.AreEqual( 2, RoleOn( JungleSprayThing, 2 ), "its third" );

		// And the one that must NOT be given a clip: its record saves the sentinel, so a blanket restore
		// would show up here and nowhere else.
		Assert.AreEqual( RideAnimations.NoRole, RoleOn( StaffRoomThing ),
			"the Staff Room is saved running nothing, and must stay that way" );
	}

	/// <summary>The Belly Bounce - thing 13, the shipped park's only ride.</summary>
	private const int BellyBounceThing = 13;

	/// <summary>
	/// The Fountain Feature - thing 24, and the case that showed restoring the script alone is a loss.
	/// Its script resumes into a two-instruction loop that can never reach the <c>LOOPANIM</c> starting
	/// the only clip it has, so nothing but the channel restore ever puts that clip on.
	/// </summary>
	private const int FountainThing = 24;

	/// <summary>The Jungle Spray - thing 14, the one thing here that runs three animation channels.</summary>
	private const int JungleSprayThing = 14;

	/// <summary>The Staff Room - thing 20, whose record saves the sentinel rather than a role.</summary>
	private const int StaffRoomThing = 20;

	/// <summary>
	/// Every placed thing whose archive holds a script is running one, and every one whose archive does
	/// not is counted as having none.
	///
	/// <para>
	/// <b>The number is arrived at twice, by two different routes.</b> This test opens the files itself;
	/// <see cref="ParkRides"/> loads them through the scheduler. A binding that quietly skipped a whole
	/// folder - or one that counted an item it never actually loaded - would still look tidy in its own
	/// log, and disagreeing counts are what catches it. Asserting the total is non-zero first stops the
	/// whole thing passing vacuously if the park or the catalogue ever came back empty.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryPlacedThingWhoseArchiveHoldsAScriptIsRunningIt()
	{
		var world = World();
		var catalogue = Catalogue();

		var withScript = 0;
		var without = 0;

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			if ( Has( ParkRides.ScriptPathFor( item ) ) )
				++withScript;
			else
				++without;
		}

		Assert.IsTrue( withScript > 0,
			"nothing placed in Lost Kingdom ships a script, so this test would prove nothing" );

		var rides = Bind( world, catalogue );

		Assert.AreEqual( withScript, rides.Bound, "things given a script" );
		Assert.AreEqual( without, rides.Scriptless, "things with none to give" );
	}

	/// <summary>
	/// Each bound script has an id of its own, the registry really holds it, and it knows the directory it
	/// came from.
	///
	/// <para>
	/// The directory is not decoration: a script names whatever it spawns relative to its own folder, and
	/// the names are far from unique across the game - so a script that does not know where it came from
	/// can only spawn the wrong file or nothing at all. Nothing Lost Kingdom places happens to spawn
	/// anything, which is exactly why this pins the prefix here rather than relying on the park to
	/// exercise it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EachBoundScriptHasItsOwnIdAndKnowsWhereItCameFrom()
	{
		var world = World();
		var catalogue = Catalogue();
		var rides = Bind( world, catalogue );

		var seen = new HashSet<int>();

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var id = rides.ScriptFor( placed.ThingId );

			if ( id == 0 )
				continue;

			Assert.IsTrue( seen.Add( id ), $"script id {id} was handed out twice" );

			var script = rides.Scheduler.Find( id );

			Assert.IsNotNull( script, $"thing {placed.ThingId} names script {id}, which the registry has not got" );
			Assert.AreEqual( item.Directory, script!.Directory,
				"a script has to know the folder it came from, or nothing it spawns can be found" );
		}

		Assert.AreEqual( rides.Bound, seen.Count, "every bound script should be reachable from its own thing" );
	}

	/// <summary>
	/// An item shipping no script at all is ordinary data rather than a fault, and the game really does
	/// ship one: <c>mystery</c>, in all four themes, catalogued like any other item and holding no
	/// <c>.RSE</c> whatever.
	///
	/// <para>
	/// The original's answer to this case is to log "Ride script not located for %s - using placeholder"
	/// and load <c>Data\TestScript\test.rse</c> instead (0x004dcf90) - and <b>that file does not ship
	/// either</b>, so its fallback fails to open as well and its loader simply answers nought. Which is
	/// why nothing here treats a missing script as an error to report.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnItemCanShipNoScriptAtAll()
	{
		var mystery = new ParkItemCatalogue.Item( 100, "Mystery", $"levels/{Theme}/rides/mystery", "mystery",
			1, 1, null );

		Assert.IsTrue( Has( $"{mystery.Directory}/{mystery.Stem}.sam" ),
			"the item itself should be there, or this is testing a typo" );

		Assert.IsFalse( Has( ParkRides.ScriptPathFor( mystery ) ), "and it should ship no script" );
	}

	/// <summary>
	/// The spelling belongs to the archives and not to us. The jungle alone holds <c>Bouncy.RSE</c> and
	/// <c>Toilet.rse</c> - the stem and the extension each vary - so the lookup has to fold case across
	/// the whole path, archives included, or most of a park would silently run nothing.
	///
	/// <para>
	/// This is the trap that would break a park on Linux and on nothing else, since Windows folds case
	/// itself: see <c>FileSystemCaseTests</c>, which pins the behaviour this depends on.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AScriptIsFoundHoweverItsArchiveSpellsIt()
	{
		Assert.IsTrue( Has( "levels/jungle/rides/bouncy/bouncy.rse" ), "asked for all in lower case" );
		Assert.IsTrue( Has( "levels/jungle/rides/bouncy/BOUNCY.RSE" ), "and all in upper" );
		Assert.IsTrue( Has( "levels/jungle/features/toilet/toilet.RSE" ),
			"an item whose script is the one spelled .rse" );
	}

	/// <summary>
	/// The park's scripts run when they are given turns, and none of them stops.
	///
	/// <para>
	/// Sixty-four ticks, so that every script has had several turns under the engine's one-in-eight rule
	/// rather than only the ones whose ids happen to fall early. Nothing should finish: a shipped script
	/// is written as an endless loop, so one that stops is a machine fault rather than a script ending.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheParksScriptsRunWhenTheyAreGivenTurns()
	{
		var rides = Bind( World(), Catalogue() );

		// The milliseconds the interpreter counts in, at the 31ms beat a park ticks on.
		for ( int tick = 0; tick < 64; ++tick )
			rides.Scheduler.Advance( tick * 31f );

		Assert.IsTrue( rides.Scheduler.TurnsGiven > 0, "nobody was given a turn at all" );
		Assert.AreEqual( 0, rides.Scheduler.Finished, "a shipped script stopped, which none of them should" );
		Assert.IsTrue( rides.Scheduler.Count >= rides.Bound, "a script went missing from the registry" );
	}

	/// <summary>
	/// Every bound script knows which thing it belongs to, and can see that thing's own animations.
	///
	/// <para>
	/// Both are seeded once, at load, and never again: the engine's loader takes the thing as its second
	/// argument and keeps it at <c>+0xac</c>, then fills the model handle at <c>+0xc8</c> from that
	/// thing's own entry in the world's table. Nothing writes either afterwards, so a script that did not
	/// get them at that moment never will - which is why this checks the binding rather than some later
	/// state.
	/// </para>
	///
	/// <para>
	/// <b>The clips are counted twice by separate routes.</b> This test loads each item's roles itself
	/// where <see cref="ParkRides"/> loads them through the binding, because handing every script the same
	/// empty table would still look perfectly tidy in its own log.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryBoundScriptKnowsItsThingAndCanSeeItsAnimations()
	{
		var world = World();
		var catalogue = Catalogue();
		var rides = Bind( world, catalogue );

		var withClips = 0;

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var id = rides.ScriptFor( placed.ThingId );

			if ( id == 0 )
				continue;

			var script = rides.Scheduler.Find( id );

			Assert.IsNotNull( script, $"thing {placed.ThingId} names a script the registry has not got" );
			Assert.AreEqual( placed.ThingId, script!.ThingId, $"'{item.Name}' does not know which thing it drives" );
			Assert.IsNotNull( script.Animations, $"'{item.Name}' was given no model at all" );

			var ours = RideAnimations.Load( item.Directory, item.Stem, data );

			Assert.AreEqual( ours.Loaded, script.Animations!.Loaded, $"'{item.Name}': clips" );
			Assert.AreEqual( ours.Roles, script.Animations.Roles, $"'{item.Name}': roles holding anything" );

			if ( ours.Loaded > 0 )
				++withClips;
		}

		Assert.IsTrue( withClips > 0,
			"nothing placed in Lost Kingdom ships a clip, so this test would prove nothing" );

		Assert.AreEqual( withClips, rides.Animated, "things that can see their own animations" );
	}
}
