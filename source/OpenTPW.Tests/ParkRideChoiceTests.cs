using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which objects a guest may be offered - <see cref="ParkRideChoice"/>, the gate the original puts in
/// front of scoring a candidate.
///
/// <para>
/// <b>The interesting result is that the answer is six.</b> Six objects carry the "a guest may choose
/// this" bit and four of them declare no queue cells in their record, but the original's queue test,
/// <c>length &lt; cells * 4</c>, counts the cells by walking the map (<c>FUN_004de130</c>) whenever
/// <c>mBackOfQueue</c> is nought. So the shipped park can offer a guest the sideshow, the ride, the Drinks
/// Shop and all three toilets.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideChoiceTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private const int JungleSpray = 14;

	private const int BellyBounce = 13;

	private const int DrinksShop = 16;

	private static readonly int[] SmallToilets = [21, 22, 23];

	/// <summary>
	/// What the shipped park can actually offer, walked from the header's own list head.
	///
	/// <para>
	/// The filter compares a queue's length against the object's <c>+0x40</c>, which the save holds as
	/// <c>mQueueSizeInCells</c> - nought for the shop and the three toilets - but
	/// <c>FUN_004de130</c> <i>overwrites</i> that field by walking the map whenever
	/// <c>mBackOfQueue</c> is nought, and the filter calls it before it reads the count. So all six objects
	/// carrying the "may be chosen" bit really can be chosen, which is what the original does.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EverySixObjectCarryingTheChoosableBitCanBeOffered()
	{
		var world = Park();
		var offerable = ParkRideChoice.Offerable( world ).Select( o => o.ThingId ).ToArray();

		CollectionAssert.AreEquivalent(
			new[] { BellyBounce, JungleSpray, DrinksShop, 21, 22, 23 }, offerable,
			"the ride, the sideshow, the drinks shop and all three toilets" );

		// Anti-vacuity, and it is the whole point: four of those six declare NO queue cells in the record,
		// so a filter reading the record would drop them. They pass on a count walked off the map instead.
		Assert.AreEqual( 6, world.Objects.Count( o => o.IsVisitable ), "six may be chosen at all" );
		Assert.AreEqual( 4, world.Objects.Count( o => o.IsVisitable && o.QueueSizeInCells == 0 ),
			"and four of those still declare no queue in their own record" );
	}

	/// <summary>
	/// <b>The discriminating case.</b> The walk needs
	/// a park; without one the save's cached pair is all there is, and the shop falls back to being refused.
	/// So this pins the difference the park makes rather than the answer it happens to give.
	/// </summary>
	[TestMethod]
	public void TheShopIsOfferedOnlyWhenItsQueueCanBeWalkedOffTheMap()
	{
		var world = Park();
		var shop = world.Objects.Single( o => o.ThingId == DrinksShop );

		Assert.AreEqual( 0, shop.QueueSizeInCells, "its record declares no queue cells" );
		Assert.AreEqual( 0, shop.BackOfQueue, "and no back of queue, which is what sends the filter walking" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( shop, 0 ),
			"with no park there is nothing to walk, so the record's nought stands and it is refused" );

		Assert.IsTrue( ParkRideChoice.CanBeOffered( shop, 0, 0, world ),
			"with the park it is offered" );

		// And the walk itself, to the cell rather than to the verdict - the path cell north of its entry.
		var (back, cells) = ParkRideChoice.QueueCellsFor( world, shop );

		Assert.AreEqual( 1, cells, "one cell, because the start exists and nothing queue-like follows it" );
		Assert.AreEqual( MapStep.CellId( 43, 29 ), back, "the path cell its entry cell connects to" );
	}

	/// <summary>
	/// <b>The walk reproduces both cached pairs the save carries, and neither number was put in.</b> That is
	/// what says it is the original's walk rather than an arrangement that happens to admit a shop: the two
	/// objects whose queues ARE in the file are recomputed to the file's own values.
	/// </summary>
	[TestMethod]
	public void TheQueueWalkReproducesTheTwoCachedPairsTheSaveAlreadyHolds()
	{
		var world = Park();

		var ride = world.Objects.Single( o => o.ThingId == BellyBounce );
		var sideshow = world.Objects.Single( o => o.ThingId == JungleSpray );

		// Walked from the start cell rather than read, by clearing the cache the record carries.
		var (rideBack, rideCells) = ParkRideChoice.QueueCellsFor( world, ride with { BackOfQueue = 0 } );

		Assert.AreEqual( ride.QueueSizeInCells, rideCells, "the ride's four cells, walked" );
		Assert.AreEqual( ride.BackOfQueue, rideBack, "ending where its record says, at (49,22)" );

		var (sprayBack, sprayCells) = ParkRideChoice.QueueCellsFor( world, sideshow with { BackOfQueue = 0 } );

		Assert.AreEqual( sideshow.QueueSizeInCells, sprayCells, "the sideshow's one cell, walked" );
		Assert.AreEqual( sideshow.BackOfQueue, sprayBack, "ending where its record says, at (52,29)" );

		// Anti-vacuity: the two answers differ, so this is not one constant satisfying both.
		Assert.AreNotEqual( rideCells, sprayCells, "four and one are not the same number" );
	}

	/// <summary>
	/// The queue term itself: a queue fills at four per cell, and the object stops being offered exactly
	/// when it is full rather than one either side of it.
	/// </summary>
	[TestMethod]
	public void AQueueHoldsFourPerCellAndTheObjectIsRefusedWhenItIsFull()
	{
		var ride = Park().Objects.Single( o => o.ThingId == BellyBounce );

		Assert.AreEqual( 4, ride.QueueSizeInCells, "the ride's queue is four cells" );

		var room = ride.QueueSizeInCells * ParkRideChoice.QueueRoomPerCell;

		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, room - 1 ), "one short of full is still offered" );
		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride, room ), "exactly full is refused" );
		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride, room + 1 ), "and so is over-full" );
		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, 0 ), "an empty queue is offered" );
	}

	/// <summary>
	/// Each of the other refusals, one at a time, against an object that is otherwise offerable - so that
	/// every arm is shown to matter rather than only the one that happens to fire first.
	/// </summary>
	[TestMethod]
	public void EachRefusalMattersOnItsOwn()
	{
		var ride = Park().Objects.Single( o => o.ThingId == BellyBounce );

		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride, 0 ), "the ride is offerable to begin with" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { Flags = 0 }, 0 ),
			"an object a guest may not choose is refused" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { State = ParkRideChoice.StateRefusedOne }, 0 ),
			"and one in the first out-of-service state" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { State = ParkRideChoice.StateRefusedFour }, 0 ),
			"and the second" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { CanLoad = 0 }, 0 ),
			"and one that cannot load anybody" );

		Assert.IsFalse( ParkRideChoice.CanBeOffered( ride with { EntryPos = 0 }, 0 ),
			"and one with nowhere to be approached from" );

		// State 3 is what most of this park's objects carry and it is NOT a refusal, which is what keeps
		// the state test from being "anything but nought".
		Assert.IsTrue( ParkRideChoice.CanBeOffered( ride with { State = 3 }, 0 ),
			"state three is not one of the two that take an object out of service" );
	}

	/// <summary>
	/// <b>A thirsty guest is sent to the Drinks Shop from all over the park, and it is routable from
	/// everywhere they stand.</b> This is the shop half of <c>docs/PLAYER-GAPS.md</c> item 8, pinned at the
	/// level the live game could not show.
	///
	/// <para>
	/// <b>It refutes both readings of a shop that is offerable and never chosen</b> - "its entry cell is
	/// unroutable" and "it is outscored from where guests stand". Measured, neither is true: the route
	/// exists from the shop's own approach cell,
	/// from mid-park and from beside the ride, and a parched guest picks the shop from four of these five
	/// cells. What the live park lacks is a thirsty guest who is still DECIDING - see
	/// <see cref="Peep.Tick"/>, where only a quarter of guests ever grow thirsty at all.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AThirstyGuestIsSentToTheDrinksShopFromAllOverThePark()
	{
		var world = Park();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var chooser = new ParkRideChooser( world, catalogue );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var shop = world.Objects.Single( o => o.ThingId == DrinksShop );

		// A guest who is as thirsty as the meters allow, which is the term a drinks shop scores on.
		var parched = new ParkRideScore.Wants( 0, 100f, 0f, 0f, 0f );

		string Routes( int fromX, int fromY )
		{
			var peep = ParkPeople.PeepsIn( world ).First();
			var walk = new PeepWalk( peep.Navigator, blocked );

			peep.Navigator.Position = new FixedVector(
				PeepNavigator.WaypointCentre( fromX ), PeepNavigator.WaypointCentre( fromY ) );
			peep.Navigator.Target = new FixedVector(
				PeepNavigator.WaypointCentre( shop.EntryCellX ), PeepNavigator.WaypointCentre( shop.EntryCellY ) );

			return walk.PlanRoute() ? "yes" : "NO";
		}

		string Picks( int fromX, int fromY )
			=> (chooser.ChooseFor( parched, fromX, fromY, gameTick: 0,
				queueLength: _ => 0 )?.ThingId ?? 0).ToString();

		Assert.AreEqual( (43, 30), (shop.EntryCellX, shop.EntryCellY), "the shop is approached from here" );

		// Routable, which is the reading this refuted first. The original's own chooser takes a candidate
		// only if FUN_004fa530 finds a route, so an unroutable shop would be refused there rather than here.
		Assert.AreEqual( "yes", Routes( 43, 29 ), "from its own approach cell" );
		Assert.AreEqual( "yes", Routes( 47, 25 ), "from the middle of the park" );
		Assert.AreEqual( "yes", Routes( 48, 22 ), "and from beside the ride" );

		// And chosen, which refuted the second. Four of five - the shop wins on the thirst term from
		// everywhere except the sideshow's own doorstep, where the sideshow's distance term carries it.
		Assert.AreEqual( DrinksShop.ToString(), Picks( 43, 29 ), "standing at the shop" );
		Assert.AreEqual( DrinksShop.ToString(), Picks( 44, 28 ), "on the loop beside it" );
		Assert.AreEqual( DrinksShop.ToString(), Picks( 47, 25 ), "in the middle of the park" );
		Assert.AreEqual( DrinksShop.ToString(), Picks( 48, 22 ), "even standing beside the free ride" );

		// The anti-vacuity half, and it is what stops this reading as "the shop always wins": from the
		// sideshow's approach cell the sideshow does.
		Assert.AreEqual( JungleSpray.ToString(), Picks( 52, 29 ), "but not from the sideshow's doorstep" );

		// And the control that says thirst is what is carrying it: with no thirst at all, the free ride
		// wins from mid-park instead. A build ignoring the need terms would answer the shop both times.
		var content = new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f );

		Assert.AreEqual( BellyBounce,
			chooser.ChooseFor( content, 47, 25, gameTick: 0, queueLength: _ => 0 )?.ThingId,
			"an unthirsty guest in the same spot is offered the ride instead" );
	}

	/// <summary>A park with nothing loaded offers nothing, rather than throwing.</summary>
	[TestMethod]
	public void NoParkOffersNothing()
		=> Assert.AreEqual( 0, ParkRideChoice.Offerable( null ).Count );
}
