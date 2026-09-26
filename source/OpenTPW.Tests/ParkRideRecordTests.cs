using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The catalogue object's ride and queue fields - what a park would need before anything could queue for
/// a ride, pay for it, or be turned away from a full one.
///
/// <para>
/// <b>What is really under test here is the ring arithmetic.</b> Five of these fields sit past six ring
/// buffers whose size the record does not state. An empty ring writes 13 bytes, which totals 379 against
/// the 1,099 the record occupies - a gap of exactly 720, or six rings of thirty four-byte entries. At
/// thirty each the record closes on 1,099 exactly. That is an argument, not a measurement, so these tests
/// check what it predicts against things a wrong offset could not produce.
/// </para>
/// <para>
/// <b>The weakest assertion here is that the takings are nought</b>, and it is kept as support rather than
/// evidence: a park nobody has played would read nought through a deleted feature too. The prices carry
/// the weight instead.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideRecordTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The three things a guest can spend time at, named and classified by the game's own data rather
	/// than inferred from their numbers: catalogue 1203 is <c>Drinks Shop</c> in <c>jungle/shops</c>,
	/// 1303 is <c>Jungle Spray</c> in <c>jungle/sideshow</c>, and 1100 is <c>Belly Bounce</c> in
	/// <c>jungle/rides</c>. <b>The middle one is a SIDESHOW, not a shop.</b>
	/// </summary>
	private const int DrinksShop = 16;

	private const int JungleSpray = 14;

	private const int BellyBounce = 13;

	/// <summary>
	/// <b>The load-bearing one.</b> Two objects carry a price and the other twelve carry none - and the two
	/// are different from each other, and both fall inside the 0-500 the original clamps this field to as
	/// it reads. A misplaced offset does not produce two distinct plausible prices on exactly the two
	/// objects that would have them.
	/// </summary>
	[TestMethod]
	public void OnlyTheShopAndTheSideshowChargeAnythingAndTheyChargeDifferently()
	{
		var objects = Park().Objects;

		Assert.AreEqual( 30, objects.Single( o => o.ThingId == DrinksShop ).PricePerUse, "catalogue item 1203" );
		Assert.AreEqual( 20, objects.Single( o => o.ThingId == JungleSpray ).PricePerUse, "catalogue item 1303" );

		Assert.AreEqual( 2, objects.Count( o => o.PricePerUse != 0 ), "and nothing else charges" );
		Assert.IsTrue( objects.All( o => o.PricePerUse is >= 0 and <= 500 ),
			"every price is inside the bound the original clamps this field to as it reads it" );
	}

	/// <summary>
	/// The one ride is identifiable by three fields agreeing about it: it is the only object with a
	/// non-zero operating duration, it holds the most people, and it has the longest queue.
	/// </summary>
	[TestMethod]
	public void TheParksOneRideIsTheOnlyThingWithAnOperatingDuration()
	{
		var objects = Park().Objects;
		var ride = objects.Single( o => o.ThingId == BellyBounce );

		Assert.AreEqual( 1100, ride.CatalogueId, "the ride's catalogue item" );
		Assert.AreEqual( 30, ride.OperatingDuration, "how long a go on it lasts" );
		Assert.AreEqual( 5, ride.OperatingCapacity, "how many can ride at once" );
		Assert.AreEqual( 4, ride.QueueSizeInCells, "and its queue is four cells long" );

		Assert.AreEqual( 1, objects.Count( o => o.OperatingDuration != 0 ),
			"nothing else in this park runs for a duration" );
		Assert.AreEqual( ride.OperatingCapacity, objects.Max( o => o.OperatingCapacity ),
			"and nothing holds more people" );
	}

	/// <summary>
	/// <b>Two adjacent fields that are different kinds of thing.</b> <c>mBackOfQueue</c> is a packed cell -
	/// the last cell of the queue - while <c>mFirstInQ</c> two bytes later is a person handle, which is
	/// how <c>GetPositionInQueue</c> uses it. The check is that the cell lands beside its own object; a
	/// field that was not a cell would miss by the width of the map.
	/// </summary>
	[TestMethod]
	public void TheBackOfAQueueIsACellBesideItsObjectAndNobodyIsStandingInIt()
	{
		var objects = Park().Objects;

		foreach ( var o in objects.Where( o => o.BackOfQueue != 0 ) )
		{
			var queueX = (o.BackOfQueue - 1) % ParkWorld.MapSize;
			var queueY = (o.BackOfQueue - 1) / ParkWorld.MapSize;
			var distance = System.Math.Abs( queueX - o.CellX ) + System.Math.Abs( queueY - o.CellY );

			Assert.IsTrue( distance <= 3,
				$"object {o.ThingId} at ({o.CellX},{o.CellY}) has its queue end at ({queueX},{queueY})" );
		}

		Assert.AreEqual( 2, objects.Count( o => o.BackOfQueue != 0 ), "two objects have a queue laid out" );
		Assert.AreEqual( 0, objects.Count( o => o.FirstInQueue != 0 ),
			"and nobody is standing in either, which is a person handle rather than a cell" );
	}

	/// <summary>
	/// The third bit of the flags byte: somewhere a guest may be <em>offered</em>. Six objects carry it -
	/// the three toilets, the Drinks Shop, the Jungle Spray sideshow and the ride - and the rest area
	/// pointedly does not.
	/// </summary>
	/// <remarks>
	/// <b>The count is pinned because it is easy to get wrong by eye</b> off a flags dump.
	/// </remarks>
	[TestMethod]
	public void SixObjectsAreSomewhereAGuestMayBeOfferedAndTheStaffRoomIsNotOneOfThem()
	{
		var objects = Park().Objects;
		var visitable = objects.Where( o => o.IsVisitable ).Select( o => o.ThingId ).OrderBy( id => id ).ToArray();

		CollectionAssert.AreEqual( new[] { 13, 14, 16, 21, 22, 23 }, visitable,
			"the three Small Toilets, the Drinks Shop, the Jungle Spray sideshow and the Belly Bounce ride" );

		Assert.IsTrue( objects.Where( o => o.IsToilet ).All( o => o.IsVisitable ),
			"a toilet is somewhere a guest chooses to go" );
		Assert.IsFalse( objects.Single( o => o.IsRestArea ).IsVisitable,
			"the rest area is for staff, and a guest has no business in it" );
	}

	/// <summary>
	/// Measured rather than claimed as a rule: in this park every offerable object reads state 0 and every
	/// other one reads 3. Worth pinning because a change in either direction is worth noticing, and worth
	/// saying plainly that one park cannot establish what the states <em>mean</em>.
	/// </summary>
	[TestMethod]
	public void EveryOfferableObjectIsInStateNoughtAndEveryOtherInStateThree()
	{
		var objects = Park().Objects;

		Assert.IsTrue( objects.Where( o => o.IsVisitable ).All( o => o.State == 0 ), "offerable objects" );
		Assert.IsTrue( objects.Where( o => !o.IsVisitable ).All( o => o.State == 3 ), "everything else" );

		// Supporting, not load-bearing: a park nobody has played has taken nothing anywhere.
		Assert.AreEqual( 0, objects.Count( o => o.TotalTakings != 0 ), "nothing has taken any money" );
		Assert.AreEqual( 0, objects.Count( o => o.AssignedStaff != 0 ), "and nobody is assigned to anything" );
	}
}
