using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The deciding turn's arms either side of its leave test - <c>FUN_004fec90</c>: the happy jump, the vomit, litter,
/// watching fireworks and the pranks, counted, each test asserting the count moves exactly where the original's own
/// test holds; and watching an entertainer, built.
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
			BalloonBeside = people.Behaviour.BalloonBeside
		};

		guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
		guest.Happiness = 50f;
		guest.PrankeryIndex = 0;
		guest.Vomit = guest.Litter = 0f;

		return (guest, tick => behaviour.Step( guest, walk, playing: null, tick ), people);
	}

	/// <summary>
	/// <b>The happy jump is counted above 80 and more than 100 sweeps past the last spot animation</b>
	/// (<c>0x004fecd2</c>, <c>0x004fece2</c>), and at neither edge.
	/// </summary>
	[TestMethod]
	public void TheHappyJumpIsCountedAboveEightyAndPastTheGap()
	{
		// An arm-2 roll, so the split does nothing and the guest stays deciding.
		var (guest, turn, _) = DecidingAt( 47, 22, SeedWhere( roll => roll % 3 == 2 ) );
		var from = Counted( "DECIDE_HAPPY_SPOT_ANIMATION" );
		var stamp = guest.TimeOfLastSpotAnim;

		guest.Happiness = 80.9f;
		turn( stamp + 101 );
		Assert.AreEqual( from, Counted( "DECIDE_HAPPY_SPOT_ANIMATION" ), "the byte 80 is not above 80" );

		guest.Happiness = 81f;
		turn( stamp + 100 );
		Assert.AreEqual( from, Counted( "DECIDE_HAPPY_SPOT_ANIMATION" ), "100 sweeps on is not more than 100" );

		turn( stamp + 101 );
		Assert.AreEqual( from + 1, Counted( "DECIDE_HAPPY_SPOT_ANIMATION" ), "81, and 101 sweeps on" );
		Assert.AreNotEqual( PeepState.PlayingSpotAnimation, guest.State, "counted, not built: nobody jumps" );
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
	/// <b>Litter at 90 is counted as the bin's errand inside three cells of a bin, and as dropped outside</b>
	/// (<c>0x004fedb3</c>, <c>FUN_00500dc0</c> with 3: squared distance under 9).
	/// </summary>
	[TestMethod]
	public void LitterIsCountedForTheBinInReachAndForTheGroundOutOfIt()
	{
		var seed = SeedWhere( roll => roll % 3 == 2 );
		var (errand, dropped) = (Counted( "DECIDE_LITTER_BIN_ERRAND" ), Counted( "DECIDE_LITTER_DROPPED" ));

		// Two cells east and two south: squared distance 8.
		var (guest, turn, _) = DecidingAt( BinX + 2, BinY + 2, seed );
		guest.Litter = 89.9f;
		turn( 2 );
		Assert.AreEqual( (errand, dropped), (Counted( "DECIDE_LITTER_BIN_ERRAND" ), Counted( "DECIDE_LITTER_DROPPED" )), "the byte 89" );

		guest.Litter = 90f;
		turn( 3 );
		Assert.AreEqual( (errand + 1, dropped), (Counted( "DECIDE_LITTER_BIN_ERRAND" ), Counted( "DECIDE_LITTER_DROPPED" )), "squared distance 8" );

		// Three cells east: squared distance 9, which is not under 9.
		(guest, turn, _) = DecidingAt( BinX + 3, BinY, seed );
		guest.Litter = 90f;
		turn( 2 );
		Assert.AreEqual( (errand + 1, dropped + 1), (Counted( "DECIDE_LITTER_BIN_ERRAND" ), Counted( "DECIDE_LITTER_DROPPED" )), "squared distance 9" );

		// Beside the bin, well inside the fireworks' reach: a bin is not fireworks, and the save has none.
		var watched = Counted( "DECIDE_WATCH_FIREWORKS" );
		(_, turn, _) = DecidingAt( BinX + 1, BinY + 1, seed );
		turn( 2 );
		Assert.AreEqual( watched, Counted( "DECIDE_WATCH_FIREWORKS" ) );
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
			(2, 0, true, "two cells off"), (1, 1, false, "not performing")
		} )
		{
			(_, walk, watch) = At( dx, dy, performing );
			watch( 2 );
			Assert.AreEqual( (0x123, from + 2), (walk.Heading, Counted( "DECIDE_WATCH_ENTERTAINER_EVENT" )), what );
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
				people.Update();
			}

			Assert.AreEqual( (PeepState.PlayingSpotAnimation, 54f), (guest.State, guest.Happiness) );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A prank is counted below happiness 15 on a draw under the prankery, by the prankery's own arm</b>
	/// (<c>0x004ff108</c>..<c>0x004ff150</c>): 100 a stink bomb on <c>StinkbombLikelihood</c> of a hundred draws, 101
	/// litter, 102 a balloon held by somebody else on the cell.
	/// </summary>
	[TestMethod]
	public void APrankIsCountedByThePrankerysOwnArm()
	{
		string[] names = ["DECIDE_PRANK_STINK_BOMB", "DECIDE_PRANK_LITTER", "DECIDE_PRANK_BALLOON"];
		(int, int, int) Counts() => (Counted( names[0] ), Counted( names[1] ), Counted( names[2] ));
		var (bombs, litter, balloons) = Counts();

		// A draw the stink bomb's two tests pass, on the split's idle arm.
		var passes = SeedWhere( roll => roll % 3 == 2 && roll % 101 < 100 && roll % 100 < 25 );
		var fails = SeedWhere( roll => roll % 3 == 2 && roll % 101 < 100 && roll % 100 >= 25 );

		void Prank( int prankery, float happiness, int seed, (int X, int Y)? balloonAt = null, bool ownBalloon = false )
		{
			var (guest, turn, people) = DecidingAt( 47, 22, seed );

			guest.PrankeryIndex = prankery;
			guest.Happiness = happiness;
			// Not so low that the leave test takes the turn first: the byte is 1 or more.
			guest.ExitLevel = 50;

			if ( ownBalloon )
				guest.Balloon = Balloon.Make( guest.ThingId, 4, now: 0 );

			if ( balloonAt is var (x, y) )
			{
				var other = people.Guests[people.Admit( x, y )];
				other.Balloon = Balloon.Make( other.ThingId, 4, now: 0 );
			}

			turn( 2 );
		}

		Prank( 100, 15f, passes );
		Prank( 0, 14f, passes );
		Prank( 100, 14f, fails );
		Prank( 100, 14f, SeedWhere( roll => roll % 3 == 2 && roll % 101 == 100 && roll % 100 < 25 ) );
		Assert.AreEqual( (bombs, litter, balloons), Counts(),
			"happiness 15, no prankery, a failed bomb roll, a draw not under the prankery" );

		Prank( 100, 14.9f, passes );
		Assert.AreEqual( (bombs + 1, litter, balloons), Counts(), "prankery 100" );

		Prank( 101, 14f, passes );
		Assert.AreEqual( (bombs + 1, litter + 1, balloons), Counts(), "prankery 101" );

		Prank( 102, 14f, passes );
		Prank( 102, 14f, passes, ownBalloon: true );
		Prank( 102, 14f, passes, balloonAt: (47, 23) );
		Assert.AreEqual( (bombs + 1, litter + 1, balloons), Counts(),
			"prankery 102: nobody's balloon, their own, and one on the next cell" );

		Prank( 102, 14f, passes, balloonAt: (47, 22) );
		Assert.AreEqual( (bombs + 1, litter + 1, balloons + 1), Counts(), "prankery 102, a balloon on the cell" );
	}

	/// <summary><b>The stink bomb's chance is the balance file's</b>, 25 in a hundred.</summary>
	[TestMethod]
	public void TheStinkBombsChanceIsTheBalanceFiles()
		=> Assert.AreEqual( 25, new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), 20 ).StinkbombLikelihood );
}
