using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The deciding turn's arms either side of its leave test - <c>FUN_004fec90</c>: the happy jump, built; the vomit and
/// watching fireworks, counted, each test asserting the count moves exactly where the original's own test holds; and
/// the litter, watching an entertainer and the pranks, built.
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkDecidingArmsTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	/// <summary>A seed whose first roll satisfies the test.</summary>
	private static int SeedWhere( Func<int, bool> test )
		=> Enumerable.Range( 0, 2000 ).First( candidate => test( new Random( candidate ).Next() ) );

	/// <summary>The Litter Bin's cell in Lost Kingdom's save.</summary>
	private const int BinX = 44, BinY = 29;

	/// <summary>
	/// One guest made on a cell of the shipped park, Deciding at happiness 50 with their choice held off, and a
	/// turn of the park's own behaviour on the seed given.
	/// </summary>
	private (Peep Guest, Action<int> Turn, ParkPeople People) DecidingAt( int x, int y, int seed )
	{
		var world = Park();
		var people = new ParkPeople( world );
		var guest = people.Guests[people.Admit( x, y )];
		var walk = people.AnyWalkFor( guest.ThingId )!;
		var behaviour = new PeepBehaviour( world, new Random( seed ),
			new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee ),
			state: people.State )
		{
			EntertainerToWatch = people.Behaviour.EntertainerToWatch,
			LetGoABalloonBeside = people.Behaviour.LetGoABalloonBeside
		};

		guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
		guest.Happiness = 50f;
		guest.PrankeryIndex = 0;
		guest.Vomit = guest.Litter = 0f;

		return (guest, tick => behaviour.Step( guest, walk, playing: null, tick ), people);
	}

	/// <summary>
	/// <b>A guest above 80 and more than 100 sweeps past their last spot animation jumps</b> (<c>0x004fecd2</c>,
	/// <c>0x004fece2</c>, <c>0x004fecea</c>): spot animation 5 for eleven sweeps, back to deciding, and no jump again
	/// until more than 100 sweeps past the jump; at neither edge does a guest jump.
	/// </summary>
	[TestMethod]
	public void AHappyGuestJumpsAboveEightyAndPastTheGap()
	{
		// An arm-2 roll, so the split does nothing and the guest stays deciding.
		var (guest, turn, _) = DecidingAt( 47, 22, SeedWhere( roll => roll % 3 == 2 ) );
		var stamp = guest.TimeOfLastSpotAnim;

		guest.Happiness = 80.9f;
		turn( stamp + 101 );
		Assert.AreEqual( PeepState.Deciding, guest.State, "the byte 80 is not above 80" );

		guest.Happiness = 90f;
		turn( stamp + 100 );
		Assert.AreEqual( PeepState.Deciding, guest.State, "100 sweeps on is not more than 100" );

		var jump = stamp + 101;
		turn( jump );
		Assert.AreEqual( (PeepState.PlayingSpotAnimation, PeepState.Deciding, PeepBehaviour.SpotHappy, jump),
			(guest.State, guest.SavedState, guest.NextAnimation, guest.TimeOfLastSpotAnim), "90, and 101 sweeps on" );

		for ( var tick = jump + 1; tick <= jump + 10; tick++ )
		{
			turn( tick );
			Assert.AreEqual( PeepState.PlayingSpotAnimation, guest.State, $"still jumping at {tick - jump} sweeps on" );
		}

		turn( jump + 11 );
		Assert.AreEqual( PeepState.Deciding, guest.State, "back to deciding on the eleventh sweep" );

		// The later draws are not the seed's first, so a guest the split sends off is put back to deciding.
		for ( var tick = jump + 12; tick <= jump + 100; tick++ )
		{
			if ( guest.State != PeepState.Deciding )
				guest.SetState( PeepState.Deciding, tick, new Random( 1 ) );

			turn( tick );
			Assert.AreNotEqual( PeepState.PlayingSpotAnimation, guest.State, $"no jump again at {tick - jump} sweeps on" );
		}

		guest.SetState( PeepState.Deciding, jump + 101, new Random( 1 ) );

		turn( jump + 101 );
		Assert.AreEqual( (PeepState.PlayingSpotAnimation, jump + 101), (guest.State, guest.TimeOfLastSpotAnim),
			"and again at 101 sweeps on" );
	}

	/// <summary>
	/// <b>The jump ends the turn</b> (the return after <c>0x004fecea</c>): a guest who jumps is not also counted
	/// for the vomit the same turn would have reached.
	/// </summary>
	[TestMethod]
	public void TheJumpEndsTheTurnBeforeTheVomit()
	{
		var (guest, turn, _) = DecidingAt( 47, 22, SeedWhere( roll => roll % 3 == 0 ) );
		guest.TimeStartedIdling = 1000;
		guest.Vomit = 100f;
		guest.Happiness = 90f;
		var from = Counted( "DECIDE_VOMIT" );

		turn( guest.TimeOfLastSpotAnim + 101 );
		Assert.AreEqual( (PeepState.PlayingSpotAnimation, from), (guest.State, Counted( "DECIDE_VOMIT" )),
			"jumped, and the vomit's test not reached" );
	}

	/// <summary>
	/// <b>The vomit is counted at illness exactly 100 on a draw that divides by three</b> (<c>0x004fed15</c>,
	/// <c>0x004fed26</c>).
	/// </summary>
	[TestMethod]
	public void TheVomitIsCountedAtExactlyAHundredOnAThirdOfTheDraws()
	{
		var from = Counted( "DECIDE_VOMIT" );

		var (ill, turn, _) = DecidingAt( 47, 22, SeedWhere( roll => roll % 3 == 0 ) );
		ill.TimeStartedIdling = 1000;
		ill.Vomit = 99.9f;
		turn( 2 );
		Assert.AreEqual( from, Counted( "DECIDE_VOMIT" ), "the byte 99" );

		(ill, turn, _) = DecidingAt( 47, 22, SeedWhere( roll => roll % 3 == 0 ) );
		ill.TimeStartedIdling = 1000;
		ill.Vomit = 100f;
		turn( 2 );
		Assert.AreEqual( from + 1, Counted( "DECIDE_VOMIT" ), "100, and the draw divides" );

		(ill, turn, _) = DecidingAt( 47, 22, SeedWhere( roll => roll % 3 == 2 ) );
		ill.Vomit = 100f;
		turn( 2 );
		Assert.AreEqual( from + 1, Counted( "DECIDE_VOMIT" ), "100, and the draw does not" );
	}

	/// <summary>
	/// A guest deciding on (x, y) as <see cref="DecidingAt"/> makes one, with their litter at <paramref name="litter"/>,
	/// and the bins they put it in recorded.
	/// </summary>
	private (Peep Guest, PeepWalk Walk, Action<int> Turn, ParkPeople People, System.Collections.Generic.List<int> Told) LitteredAt(
		int x, int y, int seed, float litter )
	{
		var world = Park();
		var people = new ParkPeople( world );
		var guest = people.Guests[people.Admit( x, y )];
		var walk = people.AnyWalkFor( guest.ThingId )!;
		var told = new System.Collections.Generic.List<int>();
		var behaviour = new PeepBehaviour( world, new Random( seed ),
			new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee ),
			state: people.State )
		{
			TriggerBin = bin => told.Add( bin.ThingId )
		};

		guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
		guest.Happiness = 50f;
		guest.PrankeryIndex = 0;
		guest.Vomit = 0f;
		guest.Litter = litter;

		return (guest, walk, tick => behaviour.Step( guest, walk, playing: null, tick ), people, told);
	}

	/// <summary>Turns until the guest is out of the bin errand, at most <paramref name="most"/>; answers how many.</summary>
	private static int UntilTheErrandEnds( Peep guest, Action<int> turn, int from, int most = 4000 )
	{
		for ( var tick = from; tick < from + most; ++tick )
		{
			turn( tick );

			if ( guest.State != PeepState.GoingToMinorDestination )
				return tick - from + 1;
		}

		return -1;
	}

	/// <summary>
	/// <b>Litter at 90 inside three cells of a routable bin sends the guest to it</b> (<c>0x004fedb3</c>, <c>FUN_00500dc0</c>
	/// with 3: squared distance under 9): state 9 and the bin in <see cref="Peep.MajorDest"/>, the litter still carried and
	/// the turn ended before the split; then the errand walks to the bin's entrance, tells the bin once, zeroes the litter and decides
	/// again, dropping nothing. At 89.9, the byte 89, the turn goes on to the split.
	/// </summary>
	[TestMethod]
	public void LitterInReachOfTheBinIsWalkedToItAndPutIn()
	{
		// An arm-1 roll: a turn that went on past the errand would set them wandering.
		var seed = SeedWhere( roll => roll % 3 == 1 );

		var (guest, walk, turn, people, told) = LitteredAt( BinX + 2, BinY + 2, seed, litter: 89.9f );
		turn( 2 );
		Assert.AreEqual( (PeepState.Wandering, 0, 89.9f), (guest.State, (int)guest.MajorDest, guest.Litter), "the byte 89: the split's wander" );

		// Two cells east and two south: squared distance 8.
		(guest, walk, turn, people, told) = LitteredAt( BinX + 2, BinY + 2, seed, litter: 90f );
		turn( 2 );
		Assert.AreEqual( (PeepState.GoingToMinorDestination, 17, 90f), (guest.State, (int)guest.MajorDest, guest.Litter),
			"squared distance 8: off to the bin, litter still carried" );

		var turns = UntilTheErrandEnds( guest, turn, 3 );
		var entry = MapStep.CellAt( people.State.ObjectsInChainOrder().Single( thing => thing.ThingId == 17 ).EntryPos );

		Assert.IsTrue( turns > 1, $"the errand should end, and not on its first turn: {turns}" );
		Assert.AreEqual( (PeepState.Deciding, 0f), (guest.State, guest.Litter), "the litter is in the bin and they decide again" );
		CollectionAssert.AreEqual( new[] { 17 }, told, "the bin told once" );
		Assert.AreEqual( entry, walk.Position.Cell, "standing on the bin's entrance cell" );
		Assert.AreEqual( 0, people.State.LitteredCells, "and nothing dropped" );
	}

	/// <summary>
	/// <b>With no bin in reach the litter goes on the guest's cell</b>, of kind 1 + r % 5 on a fresh draw after the turn's
	/// own (<c>0x004fee0e</c>..<c>0x004fee51</c>), their litter nought, and the turn goes on, the guest still deciding. A
	/// cell already littered keeps what it holds, and the guest's litter is nought all the same (<c>0x004d93fc</c>).
	/// </summary>
	[TestMethod]
	public void LitterOutOfReachOfABinIsDroppedOnTheCell()
	{
		// An arm-2 roll, and a second roll whose kind is neither the first roll's nor its remainder without the one.
		var seed = Enumerable.Range( 0, 2000 ).First( candidate =>
		{
			var random = new Random( candidate );
			var (first, second) = (random.Next(), random.Next());

			return first % 3 == 2 && second % 5 == 3 && first % 5 != 3 && first % 5 != 4;
		} );

		// Three cells east: squared distance 9, which is not under 9.
		var (guest, walk, turn, people, told) = LitteredAt( BinX + 3, BinY, seed, litter: 90f );
		var (x, y) = walk.Position.Cell;
		turn( 2 );

		Assert.AreEqual( (PeepState.Deciding, 0, 0f), (guest.State, (int)guest.MajorDest, guest.Litter), "dropped, still deciding" );
		Assert.AreEqual( 4, people.State.CellAt( x, y ).Litter, "kind 1 + 3, of the second draw" );
		Assert.AreEqual( 1, people.State.LitteredCells );
		Assert.AreEqual( 0, told.Count );

		// The same cell holding kind 2 already: it keeps it.
		(guest, walk, turn, people, _) = LitteredAt( BinX + 3, BinY, seed, litter: 95f );
		people.State.CellAt( x, y ).Litter = 2;
		turn( 2 );

		Assert.AreEqual( (2, 0f), (people.State.CellAt( x, y ).Litter, guest.Litter ), "a littered cell keeps its litter" );
	}

	/// <summary>
	/// <b>An errand that arrives with no bin named drops the litter where it stands</b> (<c>0x004fff8c</c>..<c>0x004fffca</c>):
	/// the bin is not told, the litter is on the arrival cell, and the guest decides again.
	/// </summary>
	[TestMethod]
	public void AnErrandArrivingWithNoBinNamedDropsTheLitter()
	{
		var seed = SeedWhere( roll => roll % 3 == 2 );
		var (guest, walk, turn, people, told) = LitteredAt( BinX + 2, BinY + 2, seed, litter: 100f );
		turn( 2 );
		Assert.AreEqual( PeepState.GoingToMinorDestination, guest.State );

		guest.MajorDest = 0;
		var turns = UntilTheErrandEnds( guest, turn, 3 );

		Assert.IsTrue( turns > 0 );
		Assert.AreEqual( (PeepState.Deciding, 0f), (guest.State, guest.Litter) );
		Assert.AreEqual( 0, told.Count, "no bin told" );
		Assert.AreNotEqual( 0, people.State.CellAt( walk.Position.Cell.X, walk.Position.Cell.Y ).Litter, "dropped on arrival" );
		Assert.AreEqual( 1, people.State.LitteredCells );
	}

	/// <summary>
	/// <b>A bin in reach whose entrance gets no route is passed over</b> (<c>FUN_00500dc0</c>: the best is taken only on a
	/// route, <c>0x00500ec0</c>): the guest, refused every route as still stranded, drops the litter on their own cell on the
	/// same turn and goes on deciding, no bin named and none told.
	/// </summary>
	[TestMethod]
	public void ABinInReachWithNoRouteIsPassedOverAndTheLitterDropped()
	{
		var seed = Enumerable.Range( 0, 2000 ).First( candidate =>
		{
			var random = new Random( candidate );
			var (first, second) = (random.Next(), random.Next());

			return first % 3 == 2 && second % 5 == 3;
		} );

		// Two cells east and two south: squared distance 8, in reach.
		var (guest, walk, turn, people, told) = LitteredAt( BinX + 2, BinY + 2, seed, litter: 90f );
		var (x, y) = walk.Position.Cell;
		// Stamped stranded at the next counter value, newer than every block's stamp: each route is refused.
		guest.StrandedTime = ParkState.Counter + 1;
		Unimplemented.Forget();
		turn( 2 );
		Assert.AreEqual( 1, Unimplemented.Summary.FirstOrDefault( gap => gap.What == "LITTER_SPRITE_AND_RESEED" ).Times, "the drop's sprite and reseed counted" );
		Unimplemented.Forget();

		Assert.AreEqual( (PeepState.Deciding, 0, 0f), (guest.State, (int)guest.MajorDest, guest.Litter), "no route: dropped, still deciding" );
		Assert.AreEqual( 4, people.State.CellAt( x, y ).Litter, "kind 1 + 3, on their own cell" );
		Assert.AreEqual( 0, told.Count );
	}

	/// <summary>
	/// <b>An errand whose walk gives up drops the litter where the guest stands</b> (<c>FUN_004fff20</c>: any answer but
	/// walking ends it, <c>0x004fff8c</c>..<c>0x004fffca</c>): the bin still named is not told, and they decide again.
	/// </summary>
	[TestMethod]
	public void AnErrandWhoseWalkGivesUpDropsTheLitterWhereTheyStand()
	{
		var seed = SeedWhere( roll => roll % 3 == 1 );
		var (guest, walk, turn, people, told) = LitteredAt( BinX + 2, BinY + 2, seed, litter: 100f );
		turn( 2 );
		Assert.AreEqual( (PeepState.GoingToMinorDestination, 17), (guest.State, (int)guest.MajorDest) );
		turn( 3 );
		Assert.AreEqual( PeepState.GoingToMinorDestination, guest.State, "still walking after one turn" );

		typeof( PeepNavigator ).GetProperty( nameof( PeepNavigator.CannotReach ) )!.SetValue( guest.Navigator, true );
		turn( 4 );
		var (x, y) = walk.Position.Cell;

		Assert.AreEqual( (PeepState.Deciding, 0f), (guest.State, guest.Litter), "given up: the litter dropped, deciding again" );
		Assert.AreEqual( 0, told.Count, "no bin told" );
		Assert.AreNotEqual( 0, people.State.CellAt( x, y ).Litter, "dropped where they stand" );
		Assert.AreEqual( 1, people.State.LitteredCells );
	}

	/// <summary>
	/// <b>The save's one litter holder is the bin at (44,29), and nothing in it is fireworks</b>, so the fireworks'
	/// count is dead by content in Lost Kingdom.
	/// </summary>
	[TestMethod]
	public void TheSavesOneLitterHolderIsTheBinAndNothingIsFireworks()
	{
		var objects = new ParkPeople( Park() ).State.ObjectsInChainOrder().ToArray();

		CollectionAssert.AreEqual( new[] { (17, BinX, BinY) },
			objects.Where( thing => thing.HoldsLitter ).Select( thing => (thing.ThingId, thing.CellX, thing.CellY) ).ToArray() );
		Assert.IsFalse( objects.Any( thing => thing.IsFireworks ) );
	}

	/// <summary>
	/// <b>A deciding guest beside a performing entertainer turns to face him, and the turn ends there</b>
	/// (<c>0x004fefcf</c>..<c>0x004ff0f4</c>): the heading is <c>FUN_006e7074</c> of the negated x step and the y step,
	/// the event is counted, and the split's wander is not reached. A signed sum of the steps of nought (his own cell,
	/// a diagonal of opposite signs), two cells off, and an entertainer not performing are each passed by.
	/// </summary>
	[TestMethod]
	public void ADecidingGuestWatchesAPerformingEntertainer()
	{
		// An arm-1 roll: a guest who does not watch goes on to the wander.
		var seed = SeedWhere( roll => roll % 3 == 1 );
		var from = Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" );

		var people = new ParkPeople( Park() );
		var entertainer = people.Staff.Single( member => member.Model == ParkPeople.EntertainerModel );
		var (x, y) = people.StaffWalkFor( entertainer.ThingId )!.Position.Cell;

		// A guest dx before him in x and dy before him in y.
		(Peep Guest, PeepWalk Walk, Action<int> Turn) At( int dx, int dy, bool performing )
		{
			var (moved, turnThere, there) = DecidingAt( x - dx, y - dy, seed );

			if ( performing )
				there.Staff.Single( member => member.Model == ParkPeople.EntertainerModel ).StartPerforming( 0, 1 );

			var walk = there.AnyWalkFor( moved.ThingId )!;
			walk.Heading = 0x123;

			return (moved, walk, turnThere);
		}

		// Him a step on in x and in y: facing 0x500, and the turn ends with the guest still deciding.
		var (watcher, walk, watch) = At( 1, 1, performing: true );
		watch( 2 );
		Assert.AreEqual( (0x500, PeepState.Deciding, from + 1),
			(walk.Heading, watcher.State, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), "a step on in x and y" );

		// Him a step on in y alone: facing 0x400.
		(_, walk, watch) = At( 0, 1, performing: true );
		watch( 2 );
		Assert.AreEqual( (0x400, from + 2), (walk.Heading, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), "a step on in y" );

		// Steps summing to nought, two cells off, or no performance: nobody watched, the heading untouched.
		foreach ( var (dx, dy, performing, what) in new[]
		{
			(1, -1, true, "a diagonal of opposite signs"), (0, 0, true, "his own cell"),
			(2, 0, true, "two cells off in x"), (0, 2, true, "two cells off in y"), (0, -2, true, "two cells off in y, the other way"),
			(-2, 2, true, "two cells off in both"), (1, 1, false, "not performing")
		} )
		{
			(_, walk, watch) = At( dx, dy, performing );
			watch( 2 );
			Assert.AreEqual( (0x123, from + 2), (walk.Heading, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), what );
		}
	}

	/// <summary>The cell the shipped park's one entertainer stands on.</summary>
	private (int X, int Y) EntertainerCell()
	{
		var people = new ParkPeople( Park() );

		return people.StaffWalkFor( people.Staff.Single( member => member.Model == ParkPeople.EntertainerModel ).ThingId )!.Position.Cell;
	}

	/// <summary>
	/// <b>The entertainer watched is the nearest of all, and only if he performs</b> (<c>FUN_00500f00</c>, then the state
	/// test at <c>0x004ff04a</c>): a second entertainer nearer the guest, not performing, leaves the guest watching nobody
	/// though the performing one is beside them; both performing, the guest faces the nearer.
	/// </summary>
	[TestMethod]
	public void TheEntertainerWatchedIsTheNearestOfAllAndMustBePerforming()
	{
		var seed = SeedWhere( roll => roll % 3 == 1 );
		var from = Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" );
		var (x, y) = EntertainerCell();

		// The guest a step off the shipped entertainer in x and y; the second one stands on the cell beside the guest in
		// x alone, so he is the nearer (1 cell squared against 2).
		(Action<int> Turn, PeepWalk Walk) Set( bool nearerPerforms )
		{
			var (guest, turn, people) = DecidingAt( x - 1, y - 1, seed );
			var shipped = people.Staff.Single( member => member.Model == ParkPeople.EntertainerModel );
			shipped.StartPerforming( 0, 1 );

			Assert.AreNotEqual( 0, people.Hire( new ParkStaffPool.Candidate( Id: 997, Kind: 2, Name: "Ent Test", Grade: 0, Costume: 0, Wage: 69 ), x - 1, y ) );
			var second = people.Staff.Single( member => member.Model == ParkPeople.EntertainerModel && member != shipped );
			Assert.AreEqual( (x - 1, y), people.StaffWalkFor( second.ThingId )!.Position.Cell, "the second entertainer's cell" );

			second.SetActivity( nearerPerforms ? StaffActivity.Performing : StaffActivity.Idle, 1 );

			var walk = people.AnyWalkFor( guest.ThingId )!;
			walk.Heading = 0x123;

			return (turn, walk);
		}

		var (turn, walk) = Set( nearerPerforms: false );
		turn( 2 );
		Assert.AreEqual( (0x123, from), (walk.Heading, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), "the nearer is idle: nobody is watched" );

		(turn, walk) = Set( nearerPerforms: true );
		turn( 2 );
		Assert.AreEqual( (0x400, from + 1), (walk.Heading, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), "both perform: the nearer is faced" );
	}

	/// <summary>
	/// <b>The watching arm is after the leave test</b> (<c>0x004fee5b</c>..<c>0x004fef13</c> before <c>0x004fefcf</c>): a
	/// miserable guest, or one whose day has run out, beside a performing entertainer goes home instead of stopping to
	/// watch him.
	/// </summary>
	[TestMethod]
	public void AGuestWhoWantsToLeaveDoesNotStopToWatch()
	{
		var seed = SeedWhere( roll => roll % 3 == 1 );
		var from = Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" );
		var (x, y) = EntertainerCell();

		foreach ( var (what, ready) in new (string, Action<Peep>)[]
		{
			("miserable", guest => guest.Happiness = 0f), ("a day run out", guest => guest.ExitLevel = 0)
		} )
		{
			var (guest, turn, people) = DecidingAt( x - 1, y - 1, seed );
			people.Staff.Single( member => member.Model == ParkPeople.EntertainerModel ).StartPerforming( 0, 1 );
			ready( guest );

			var walk = people.AnyWalkFor( guest.ThingId )!;
			walk.Heading = 0x123;
			turn( 2 );

			Assert.AreEqual( (PeepState.HeadingForExit, 0x123, from), (guest.State, walk.Heading, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), what );
		}
	}

	/// <summary>
	/// <b>A guest's needs turn is handed the region effects of the cell they stand on</b> (<c>0x005016ba</c>): in the
	/// shipped park the entertainer's own cell holds happiness 2 and the four beside it 1 (effect 0, divided by the
	/// steps plus one), a diagonal and the cells two off nothing.
	/// </summary>
	[TestMethod]
	public void TheNeedsTurnReadsTheCellTheGuestStandsOn()
	{
		var people = new ParkPeople( Park() );
		var entertainer = people.Staff.Single( member => member.Model == ParkPeople.EntertainerModel );
		var (x, y) = people.StaffWalkFor( entertainer.ThingId )!.Position.Cell;

		short HappinessAt( int dx, int dy ) => people.CellEffects( people.Guests[people.Admit( x + dx, y + dy )] ).Happiness;

		Assert.AreEqual( (2, 1, 1, 0, 0), (HappinessAt( 0, 0 ), HappinessAt( 1, 0 ), HappinessAt( 0, -1 ), HappinessAt( 1, 1 ),
			HappinessAt( 2, 0 )) );
	}

	/// <summary>
	/// <b>The park's sweep hands each guest's needs turn the cell they stand on</b> (<c>0x005016ba</c>): a guest held in a
	/// spot animation on a quiet cell with effect 0 stamped round it gains its 2 on each of their two needs turns in eight
	/// sweeps, and nothing else moves their happiness there.
	/// </summary>
	[TestMethod]
	public void ASweepHandsTheNeedsTurnTheGuestsCell()
	{
		using var clock = new SimulationClockScope();
		var people = new ParkPeople( Park(), new ParkBalance( "jungle", easyMode: true ) );

		try
		{
			people.State.StampEffect( 0, 55, 30 );

			var guest = people.Guests[people.Admit( 55, 30 )];
			guest.SetState( PeepState.PlayingSpotAnimation, 1_000_000, new Random( 1 ) );
			guest.Happiness = 50f;
			guest.Thirst = guest.Hunger = guest.Toilet = guest.Vomit = 10f;

			for ( var sweep = 0; sweep < 8; ++sweep )
			{
				SimulationClockScope.Frame( GameClock.TickSeconds * 8 );
				ParkFrame.Run( people );
			}

			Assert.AreEqual( (PeepState.PlayingSpotAnimation, 54f), (guest.State, guest.Happiness) );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>A seed whose first two rolls satisfy the test.</summary>
	private static int SeedWhere( Func<int, int, bool> test )
		=> Enumerable.Range( 0, 20000 ).First( candidate =>
		{
			var random = new Random( candidate );
			var first = random.Next();

			return test( first, random.Next() );
		} );

	/// <summary>The cell the pranksters stand on, and the cell beside it.</summary>
	private const int PrankX = 47, PrankY = 22;

	/// <summary>
	/// A prankster deciding on <see cref="PrankX"/>, <see cref="PrankY"/> with the prankery and happiness given, whose id
	/// % 3 is <paramref name="idModThree"/> where one is asked for, and a guest holding a balloon on
	/// <paramref name="balloonAt"/> where one is given; then one turn on <paramref name="seed"/>.
	/// </summary>
	private (Peep Guest, ParkPeople People, Peep? Other) Prank( int prankery, float happiness, int seed,
		(int X, int Y)? balloonAt = null, bool ownBalloon = false, int idModThree = -1 )
	{
		var (guest, turn, people) = DecidingAt( PrankX, PrankY, seed );

		while ( idModThree >= 0 && guest.ThingId % 3 != idModThree )
		{
			guest.SetState( PeepState.Wandering, tick: 1, new Random( 1 ) );
			guest = people.Guests[people.Admit( PrankX, PrankY )];
			guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
			guest.Vomit = guest.Litter = 0f;
		}

		var walk = people.AnyWalkFor( guest.ThingId )!;
		var behaviour = new PeepBehaviour( Park(), new Random( seed ),
			new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), 20 ), state: people.State )
		{
			LetGoABalloonBeside = people.Behaviour.LetGoABalloonBeside
		};

		guest.PrankeryIndex = prankery;
		guest.Happiness = happiness;
		// Not so low that the leave test takes the turn first: the byte is 1 or more.
		guest.ExitLevel = 50;

		if ( ownBalloon )
			guest.Balloon = Balloon.Make( guest.ThingId, 4, now: 0 );

		Peep? other = null;

		if ( balloonAt is var (x, y) )
		{
			other = people.Guests[people.Admit( x, y )];
			other.Balloon = Balloon.Make( other.ThingId, 4, now: 0 );
			other.BalloonLife = 37;
		}

		behaviour.Step( guest, walk, playing: null, tick: 2 );

		return (guest, people, other);
	}

	private static readonly string[] PrankCounts =
		["DECIDE_PRANK_EVENT", "PRANK_GUARD_SENT", "DECIDE_PRANK_STINK_BOMB_SOUND", "LITTER_STINK_BOMB_PARTICLE"];

	private static int[] Counts() => [.. PrankCounts.Select( Counted )];

	/// <summary>
	/// <b>No prank is played at happiness 15, with no prankery, on a failed bomb roll or on a draw not under the
	/// prankery</b> (<c>0x004ff108</c>..<c>0x004ff150</c>, <c>0x004ff24b</c>): nothing on the cell, happiness as it was,
	/// no message sent.
	/// </summary>
	[TestMethod]
	public void NoPrankIsPlayedWhereItsTestsFail()
	{
		var passes = SeedWhere( roll => roll % 3 == 2 && roll % 101 < 100 && roll % 100 < 25 );
		var fails = SeedWhere( roll => roll % 3 == 2 && roll % 101 < 100 && roll % 100 >= 25 );
		var notUnder = SeedWhere( roll => roll % 3 == 2 && roll % 101 == 100 && roll % 100 < 25 );
		var from = Counts();

		foreach ( var (prankery, happiness, seed, why) in new[]
		{
			(100, 15f, passes, "happiness 15"), (0, 14f, passes, "no prankery"), (100, 14f, fails, "the bomb's roll 25 or more"),
			(100, 14f, notUnder, "a draw % 101 of 100"), (101, 15.5f, passes, "litter at the byte 15")
		} )
		{
			var (guest, people, _) = Prank( prankery, happiness, seed );

			Assert.AreEqual( (0, happiness), (people.State.CellAt( PrankX, PrankY ).Litter, guest.Happiness), why );
		}

		CollectionAssert.AreEqual( from, Counts(), "no event, no message, no sound" );
	}

	/// <summary>
	/// <b>Prankery 100 drops a stink bomb</b>: litter of kind 8 on the cell, region effect 5 round it (happiness -3 on
	/// the cell itself), the sound and the particle counted, message <c>0xe</c> and the event counted, and the
	/// prankster's happiness up one (14.9 to 15.9: the byte 14 is under 15). The writer takes the effect off again, as
	/// the litter is not written (Q1288).
	/// </summary>
	[TestMethod]
	public void APranksterOfAHundredDropsAStinkBomb()
	{
		var seed = SeedWhere( roll => roll % 3 == 2 && roll % 101 < 100 && roll % 100 < 25 );
		var file = Park();
		// The cell's effects in a park where the bomb's roll fails, a turn that plays no prank.
		var before = Prank( 100, 14.9f, SeedWhere( roll => roll % 3 == 2 && roll % 100 >= 25 ) ).People.State
			.EffectsAt( PrankX, PrankY ).ToArray();
		var from = Counts();

		var (guest, people, _) = Prank( 100, 14.9f, seed );

		Assert.AreEqual( (ParkState.StinkBombLitter, 15.9f), (people.State.CellAt( PrankX, PrankY ).Litter, guest.Happiness) );
		Assert.AreEqual( before[0] - 3, people.State.EffectsAt( PrankX, PrankY )[0], "effect 5's happiness on its own cell" );
		Assert.AreEqual( before[1] + 3, people.State.EffectsAt( PrankX, PrankY )[1], "and its illness" );
		CollectionAssert.AreEqual( new[] { from[0] + 1, from[1] + 1, from[2] + 1, from[3] + 1 }, Counts(),
			"the event, the guard, the sound and the particle" );

		var written = Level.WrittenEffects( file, people.State, _ => true );
		var at = ((PrankY * ParkWorld.MapSize) + PrankX) * ParkWorld.EffectWords;
		CollectionAssert.AreEqual( before, written[at..(at + ParkWorld.EffectWords)], "the writer takes the effect off" );
	}

	/// <summary>
	/// <b>Prankery 101 drops litter of kind 1 + the turn's own draw % 5</b> (<c>0x004ff237</c>), not a fresh draw's and
	/// not the draw % 5 alone, with no stink bomb's effect, and happiness up one.
	/// </summary>
	[TestMethod]
	public void APranksterOfAHundredAndOneDropsLitterOfTheTurnsDraw()
	{
		var seed = SeedWhere( ( roll, next ) => roll % 3 == 2 && roll % 5 == 3 && next % 5 != 3 && next % 5 != 2 );
		var before = Prank( 0, 3f, seed ).People.State.EffectsAt( PrankX, PrankY ).ToArray();

		var (guest, people, _) = Prank( 101, 3f, seed );

		Assert.AreEqual( (4, 4f), (people.State.CellAt( PrankX, PrankY ).Litter, guest.Happiness) );
		CollectionAssert.AreEqual( before, people.State.EffectsAt( PrankX, PrankY ).ToArray(), "no region effect" );
	}

	/// <summary>
	/// <b>Prankery 102 lets go the balloon of another guest on the prankster's cell</b> (<c>0x004ff156</c>..
	/// <c>0x004ff1f7</c>, <c>FUN_004fe950</c>): that guest holds none, theirs bursts where it was, and their balloon's
	/// life is left. The prankster's own balloon and one on the next cell are not let go, and with none on the cell the
	/// prank is not done: no message, happiness as it was.
	/// </summary>
	[TestMethod]
	public void APranksterOfAHundredAndTwoLetsGoABalloonOnTheirCell()
	{
		var seed = SeedWhere( roll => roll % 3 == 2 );
		var from = Counts();

		foreach ( var (balloonAt, own, why) in new[] { ((PrankX, PrankY + 1), false, "the next cell"), (((int, int)?)null, true, "their own") } )
		{
			var (guest, people, other) = Prank( 102, 10f, seed, balloonAt, own );

			Assert.AreEqual( (10f, 0), (guest.Happiness, people.Bursting.Count), why );
			Assert.IsTrue( (other ?? guest).Balloon != null, $"{why}: still held" );
		}

		CollectionAssert.AreEqual( from, Counts(), "no prank done, no message" );

		var (prankster, park, victim) = Prank( 102, 10f, seed, balloonAt: (PrankX, PrankY) );

		Assert.AreEqual( (11f, null, 37, 1), (prankster.Happiness, victim!.Balloon, victim.BalloonLife, park.Bursting.Count) );
		Assert.AreEqual( from[0] + 1, Counted( PrankCounts[0] ), "the event" );
	}

	/// <summary>
	/// <b>The advice a prank posts is by the prankster's id % 3, not by the prank played</b> (the bus record's
	/// <c>+0xc</c>, <c>DIV 3</c> at <c>0x004ff2f8</c>; <c>ReceiveMessage</c> arm <c>0x0059b221</c>): 0 the stink bomb's
	/// row 125, message <c>0x7d</c>, 1 the litter's, 2 the balloon's. A litter prank (prankery 101) by a guest whose id
	/// % 3 is nought posts the stink bomb's, which is counted as its rows are not built.
	/// </summary>
	[TestMethod]
	public void ThePranksAdviceIsByTheGuestsIdModThree()
	{
		Assert.AreEqual( (0x7d, 0x7e, 0x7f, 0x7d), (ParkAdvisorMessages.PrankAdviceOf( 3 ), ParkAdvisorMessages.PrankAdviceOf( 4 ),
			ParkAdvisorMessages.PrankAdviceOf( 5 ), ParkAdvisorMessages.PrankAdviceOf( 99 )) );

		var seed = SeedWhere( roll => roll % 3 == 2 );
		var (bombs, litter) = (Counted( "PARK_ADVISOR_PRANK_STINK_BOMB" ), Counted( "PARK_ADVISOR_PRANK_LITTER" ));

		var (guest, _, _) = Prank( 101, 10f, seed, idModThree: 0 );

		Assert.AreEqual( (0, 11f), (guest.ThingId % 3, guest.Happiness) );
		Assert.AreEqual( (bombs + 1, litter), (Counted( "PARK_ADVISOR_PRANK_STINK_BOMB" ), Counted( "PARK_ADVISOR_PRANK_LITTER" )) );
	}

	/// <summary><b>The stink bomb's chance is the balance file's</b>, 25 in a hundred.</summary>
	[TestMethod]
	public void TheStinkBombsChanceIsTheBalanceFiles()
		=> Assert.AreEqual( 25, new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), 20 ).StinkbombLikelihood );
}
