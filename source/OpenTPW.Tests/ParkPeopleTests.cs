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
	/// Which vehicle brings a crowd, which the original decides by how many are coming rather than at
	/// random - <c>FUN_004cf3e0</c> takes the first under <c>0x24</c> and otherwise
	/// <c>(0x3c &lt; count) + 2</c>. The save agrees from the other side, naming its three slots
	/// <c>mArrivalVehicle_Size1..3</c>.
	///
	/// <para>
	/// The boundaries are the point of this rather than the middles: 35 and 36 sit either side of the
	/// first, and 60 and 61 either side of the second, so an off-by-one in either comparison shows up
	/// here rather than in a park that quietly never sends a ferry.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WhichVehicleComesIsDecidedByHowManyAreArriving()
	{
		Assert.AreEqual( 1, ParkPeople.VehicleFor( 1 ), "one person takes the bus" );
		Assert.AreEqual( 1, ParkPeople.VehicleFor( 35 ), "and so do thirty-five" );

		Assert.AreEqual( 2, ParkPeople.VehicleFor( 36 ), "thirty-six is the second vehicle" );
		Assert.AreEqual( 2, ParkPeople.VehicleFor( 60 ), "and sixty is still the second" );

		Assert.AreEqual( 3, ParkPeople.VehicleFor( 61 ), "sixty-one takes the third" );
		Assert.AreEqual( 3, ParkPeople.VehicleFor( 500 ), "and so does a full load" );
	}

	/// <summary>
	/// Somebody who was never in the save. <see cref="ParkPeople.Admit"/> is what an arrival is, and what
	/// this is really testing is the wiring rather than the guest: several separate structures have to
	/// learn about them, and each one fails quietly, and differently, when it does not. A guest missing
	/// from the walk table stands still; one missing from the visitor count is invisible to the park's
	/// own arithmetic; one given a saved thing's id overwrites somebody.
	///
	/// <para>
	/// The sprite pool is the one wiring left to the running game. It needs a graphics device, which is
	/// why <see cref="ParkPeople.Admit"/> reaches it through a null-conditional and why nothing here can
	/// see it - the same division this file's own remarks describe.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnArrivalJoinsEveryListThatHasToKnowAboutIt()
	{
		var world = World();
		var state = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		var before = people.Peeps.Count;
		var visitors = state.VisitorsToDate;

		// BusStopA, which Standard.sam puts at (42,5).
		var id = people.Admit( 42, 5 );

		Assert.AreNotEqual( 0, id, "the park should take a guest at the bus stop" );
		Assert.AreEqual( before + 1, people.Peeps.Count, "the simulation list" );
		Assert.IsNotNull( people.WalkFor( id ), "the walk, without which they never move" );
		Assert.AreEqual( visitors + 1, state.VisitorsToDate, "the park's own count of who has come" );

		// Not a thing the file already named. This is the assertion that would have caught the id being
		// taken from a documented list rather than computed - the list was a guest short.
		Assert.IsFalse( world.People.Any( person => person.ThingId == id ),
			$"id {id} already belongs to a person in the save" );
		Assert.IsFalse( world.Objects.Any( placed => placed.ThingId == id ),
			$"id {id} already belongs to an object in the save" );

		// And they begin where the admission sequence begins rather than in the hub a guest is
		// constructed in, which is a deviation Admit explains at the site.
		Assert.AreEqual( PeepState.AtGate, people.Peeps.Single( peep => peep.ThingId == id ).State,
			"an arrival joins the admission sequence at its head" );
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
	/// <b>Every guest arrives knowing how far along a route they had got.</b> This is the join between the
	/// navigator, which had been built and left dormant, and the guests the park actually simulates -
	/// nothing constructed one from real data until now.
	///
	/// <para>
	/// <b>This test was called "carrying the route the save gave them", and that was an overclaim.</b>
	/// Nothing below is a waypoint, because the save reader deliberately does not parse
	/// <c>subpath_buffer[]</c> - only <c>path_buffer_count - 1</c> of its entries are ever written, so the
	/// rest hold the uninitialised fill or a stale distance from an earlier route. The waypoints are the
	/// route; what travels here is the bookkeeping around it, which is real and worth pinning but is not
	/// the same thing.
	/// </para>
	/// <para>
	/// The numbers are not quoted from an outside measurement. Each field is checked against the save
	/// reader's own <c>Navigator</c> for the same person, so what is pinned is that the value travelled
	/// intact rather than that it happens to equal some figure written down elsewhere. The buffered count
	/// is then checked against the rule that derives it, which is a relation the code computes for itself.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryGuestArrivesKnowingHowFarAlongTheirRouteTheyWere()
	{
		var world = World();
		var peeps = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var saved = world.People.Where( person => person.Guest != null )
			.ToDictionary( person => person.ThingId, person => person.Navigator );

		Assert.AreEqual( saved.Count, peeps.Count, "every guest should have been built" );

		foreach ( var (id, navigator) in saved )
		{
			var carried = peeps[id].Navigator;

			Assert.AreEqual( navigator.Radius, carried.Radius, $"guest {id} radius" );

			// Where they are, which way they are going, and the limits they move under. Checked against
			// the reader's own record rather than against numbers measured elsewhere - ParkNavigatorState
			// tests already pin these against the file, and repeating that here would test the reader
			// twice and the running copy not at all.
			Assert.AreEqual( new FixedVector( navigator.X, navigator.Y ), carried.Position,
				$"guest {id} position" );
			Assert.AreEqual( new FixedVector( navigator.VelocityX, navigator.VelocityY ), carried.Velocity,
				$"guest {id} velocity" );
			Assert.AreEqual( new FixedVector( navigator.TargetX, navigator.TargetY ), carried.Target,
				$"guest {id} target" );
			Assert.AreEqual( navigator.MaxSpeed, carried.MaxSpeed, $"guest {id} max speed" );
			Assert.AreEqual( navigator.MaxForce, carried.MaxForce, $"guest {id} max force" );

			Assert.AreEqual( navigator.PathCount, carried.Cursor, $"guest {id} cursor" );
			Assert.AreEqual( navigator.PathTotalCount, carried.TotalWaypoints, $"guest {id} waypoints" );
			Assert.AreEqual( navigator.PathBufferCount, carried.BufferedWaypoints, $"guest {id} buffered" );
			Assert.AreEqual( navigator.TotalDistance, carried.TotalDistance, $"guest {id} route length" );
			Assert.AreEqual( navigator.StuckBits, carried.StuckBits, $"guest {id} stuck record" );

			// A relation the code works out rather than a number anyone measured.
			Assert.AreEqual( PeepNavigator.BufferedFor( carried.TotalWaypoints ), carried.BufferedWaypoints,
				$"guest {id} should buffer as many waypoints as the rule says" );
		}

		// Each guest must have their OWN navigator - handing every peep the same one would pass every
		// assertion above for any park whose guests happen to share a route.
		var distinct = peeps.Values.Select( peep => peep.Navigator ).Distinct().Count();

		Assert.AreEqual( peeps.Count, distinct, "no two guests should share a navigator object" );
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

	/// <summary>
	/// <b>When a load is due</b>: <c>FUN_0041a990</c> against the period at <c>0x004cf3f6</c>. The clock and the mark
	/// are each shifted down two, unsigned, and the difference has to be MORE than the period. From Lost Kingdom's
	/// mark of 661 that is <c>mGameTick</c> 1264 and not a tick sooner, a mark ahead of the clock is due at once, and a load
	/// let go the sweep after a drop brings the next 602 to 605 sweeps after that drop (<c>docs/exe/park.md</c>,
	/// "Arrivals").
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>&gt;=</c> for <c>&gt;</c> makes 1260 due; a signed difference makes the mark ahead of the
	/// clock wait.
	/// </remarks>
	[TestMethod]
	public void ALoadIsDueOnceItsWaitIsPastThePeriod()
	{
		Assert.IsFalse( ParkPeople.LoadIsDue( 1263, 661, 150 ), "1263 >> 2 is 315, which is 150 past 165: not more" );
		Assert.IsFalse( ParkPeople.LoadIsDue( 1260, 661, 150 ), "nor on the first tick of that count" );
		Assert.IsTrue( ParkPeople.LoadIsDue( 1264, 661, 150 ), "316 is 151 past: due" );
		Assert.AreEqual( 1264, ParkPeople.FirstDueTick( 661, 150 ), "the first tick it is due, worked out" );

		Assert.IsTrue( ParkPeople.LoadIsDue( 100, 661, 150 ), "a mark ahead of the clock wraps, and is due at once" );

		for ( var drop = 1000; drop < 1004; ++drop )
		{
			var next = ParkPeople.FirstDueTick( drop + 1, 150 );

			Assert.IsTrue( next - drop is >= 602 and <= 605,
				$"a last drop on {drop}, let go on {drop + 1}, brings the next on {next}" );
		}
	}

	/// <summary>
	/// When a vehicle is sent on - the rule out of <c>FUN_004cf3e0</c>'s arms, which decides whether the
	/// park keeps getting visitors at all.
	///
	/// <para>
	/// <b>Every vehicle script parks three times a circuit</b>, each time spinning on <c>VAR_TRIGGER</c>
	/// until something writes it. Releasing only one of the three is not a partial fix but a park that
	/// empties: arrivals are gated on the vehicle answering 2, so a bus stopped at 4 means nobody ever
	/// arrives again. That was measured in a live park before this rule existed - pc 90, status 4,
	/// unmoved over a hundred seconds - so these are the states that were actually observed, not a
	/// guessed set.
	/// </para>
	///
	/// <para>
	/// <b>The refusals are the assertions that matter.</b> Unloading is not released while a load is held: the
	/// original drops one guest a sweep for exactly as long as the vehicle answers 2, so a rule that let it go
	/// with somebody aboard would send the bus away with its passengers still on it, and one that let it go on the
	/// sweep of the last drop would move it on before the sweep after, which is the one that lets the load go and
	/// starts the next wait (<c>0x004cf56b</c>).
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> dropping <c>!loadHeld</c> from the rule releases the bus on the last drop's sweep, and the
	/// second assertion fails.
	/// </remarks>
	[TestMethod]
	public void AVehicleIsSentOnFromEveryStateItParksIn()
	{
		Assert.IsFalse( ParkPeople.ReleasesVehicle( 2, 3, loadHeld: true ),
			"unloading with three still aboard: it must NOT be sent away" );
		Assert.IsFalse( ParkPeople.ReleasesVehicle( 2, 0, loadHeld: true ),
			"nor on the sweep that dropped the last one, while the load is still held" );

		Assert.IsTrue( ParkPeople.ReleasesVehicle( 2, 0, loadHeld: false ),
			"but a load let go is what sends it away" );

		Assert.IsTrue( ParkPeople.ReleasesVehicle( 4, 0, loadHeld: false ), "leaving - the one the bus was stuck at" );
		Assert.IsTrue( ParkPeople.ReleasesVehicle( 4, 2, loadHeld: true ),
			"and it is stuck there whether or not a load is outstanding, so the count must not gate it" );

		Assert.IsTrue( ParkPeople.ReleasesVehicle( 0, 0, loadHeld: false ), "idle between runs" );
		Assert.IsTrue( ParkPeople.ReleasesVehicle( 6, 0, loadHeld: false ), "and finished, which also forgets it" );

		// The states it passes through under its own power. Nudging one of these would release a spin
		// the script has not reached, which the next COPY VAR_TRIGGER, 0 would then swallow silently.
		Assert.IsFalse( ParkPeople.ReleasesVehicle( 1, 0, loadHeld: false ), "arriving" );
		Assert.IsFalse( ParkPeople.ReleasesVehicle( 3, 0, loadHeld: false ), "pulling away" );
		Assert.IsFalse( ParkPeople.ReleasesVehicle( 5, 0, loadHeld: false ), "running its last clip" );
	}
}
