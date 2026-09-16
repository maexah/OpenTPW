using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The shipped park's guests, seeded from the save and then ticked - the reader and the simulation
/// joined up, which neither of their own test files can check alone.
///
/// <para>
/// These read real game files and are skipped where there is no installation: see <see cref="GameData"/>.
/// The peeps are built through the static factory rather than by constructing the entity, so nothing
/// here needs a graphics device or leaves anything behind in the world.
/// </para>
/// </summary>
[TestClass]
public class ParkPeopleTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// Only guests are simulated. The five staff share the person base and then carry a block of their
	/// own that nothing reads, and five state machines of their own that nothing runs, so taking them in
	/// would mean ticking guest needs over fields that are not needs.
	/// </summary>
	[TestMethod]
	public void OnlyTheGuestsAreSimulated()
	{
		var world = World();

		Assert.AreEqual( 18, world.People.Count, "people in the park" );

		var peeps = ParkPeople.PeepsIn( world );

		Assert.AreEqual( 13, peeps.Count, "guests simulating" );

		CollectionAssert.AreEqual(
			new[] { 42, 41, 40, 39, 38, 37, 36, 35, 34, 33, 32, 31, 29 },
			peeps.Select( peep => peep.ThingId ).ToArray(),
			"the guests, in the order the save lists them" );
	}

	/// <summary>The save's numbers arrive in the running copy unchanged, before any tick touches them.</summary>
	[TestMethod]
	public void EachGuestStartsFromWhatTheSaveSaid()
	{
		var peeps = ParkPeople.PeepsIn( World() ).ToDictionary( peep => peep.ThingId );

		Assert.AreEqual( 2, peeps[40].PersonType );
		Assert.AreEqual( 654, peeps[40].Cash );
		Assert.AreEqual( 57, peeps[40].ExitLevel );
		Assert.AreEqual( 12f, peeps[40].Thirst );
		Assert.AreEqual( 61f, peeps[40].Hunger );
		Assert.AreEqual( 24f, peeps[40].Toilet );

		Assert.IsTrue( peeps.Values.All( peep => peep.Happiness == 50f ),
			"every guest starts on the happiness a guest is made with" );
	}

	/// <summary>
	/// Sixteen ticks of the real park, which is half a second of play.
	///
	/// <para>
	/// Two things are pinned and they guard each other. Every guest takes exactly four of those sixteen
	/// turns, so every countdown home moves by four - if the slot gate were dropped they would each move
	/// by sixteen. And only the three guests whose thing id is a multiple of four grow hungrier at all,
	/// because the drift is gated on the same counter as the tick: if <i>that</i> gate were dropped, all
	/// thirteen would have moved.
	/// </para>
	/// </summary>
	[TestMethod]
	public void SixteenTicksMoveEveryGuestFourTurnsAndOnlyThreeGrowHungry()
	{
		var before = ParkPeople.PeepsIn( World() ).ToDictionary( peep => peep.ThingId,
			peep => (peep.ExitLevel, peep.Thirst, peep.Hunger, peep.Toilet) );

		var peeps = ParkPeople.PeepsIn( World() );

		for ( var tick = 1; tick <= 16; ++tick )
		{
			foreach ( var peep in peeps )
				peep.Tick( tick );
		}

		foreach ( var peep in peeps )
		{
			Assert.AreEqual( before[peep.ThingId].ExitLevel - 4, peep.ExitLevel,
				$"guest {peep.ThingId} should have taken four of the sixteen turns" );
		}

		var hungrier = peeps.Where( peep => peep.Hunger > before[peep.ThingId].Hunger )
			.Select( peep => peep.ThingId )
			.OrderByDescending( id => id )
			.ToArray();

		CollectionAssert.AreEqual( new[] { 40, 36, 32 }, hungrier,
			"only the guests whose id is a multiple of four ever drift" );

		// And by exactly the amounts the original drifts by: toilet one, hunger and thirst two.
		foreach ( var id in hungrier )
		{
			var peep = peeps.Single( p => p.ThingId == id );

			Assert.AreEqual( before[id].Toilet + 1f, peep.Toilet, $"guest {id} toilet" );
			Assert.AreEqual( before[id].Hunger + 2f, peep.Hunger, $"guest {id} hunger" );
			Assert.AreEqual( before[id].Thirst + 2f, peep.Thirst, $"guest {id} thirst" );
		}

		// Nobody in this park is anywhere near a maxed need, so nobody has lost any happiness yet.
		Assert.IsTrue( peeps.All( peep => peep.Happiness == 50f ),
			"a park this new should not have made anybody unhappy" );

		// And nobody is desperate enough to hurry.
		Assert.IsTrue( peeps.All( peep => peep.PurposeSpeed == Peep.UnhurriedSpeed ),
			"no guest in the shipped park needs the toilet badly enough to hurry" );
	}
}
