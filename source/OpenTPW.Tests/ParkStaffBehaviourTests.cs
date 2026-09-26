using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What the park's staff do once something steps them.
///
/// <para>
/// <b>The assertion that matters is about position, not state.</b> A staff member who reached a state and
/// was handed a destination but never moved would satisfy any state check, so the payoff tests ask whether
/// they <b>end up somewhere else on the map</b>.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkStaffBehaviourTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkBalance Balance() => new( "jungle", easyMode: true );

	/// <summary>The guard and the researcher, whose decide arm the shared switch answers itself.</summary>
	private const int Guard = 28;

	private const int Researcher = 30;

	/// <summary>The three whose work-finding is a kind-specific function this does not build.</summary>
	private static readonly int[] WithoutWork = [25, 26, 27];

	/// <summary>
	/// Steps every member of staff <paramref name="turns"/> sweeps on the park's clock: <c>mGameTick</c> carries on
	/// from the save's, one up before each sweep, as <see cref="ParkState.GameTick"/> does.
	/// </summary>
	private (StaffBehaviour Behaviour, Dictionary<int, Staff> Staff, Dictionary<int, PeepWalk> Walks) Run(
		ParkWorld world, int turns, int seed = 1234 )
	{
		var behaviour = new StaffBehaviour( Balance(), new Random( seed ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var staff = ParkPeople.StaffIn( world ).ToDictionary( member => member.ThingId );
		var walks = staff.Values.ToDictionary( member => member.ThingId,
			member => new PeepWalk( member.Navigator, blocked ) );

		for ( var tick = world.GameTick + 1; tick <= world.GameTick + turns; ++tick )
		{
			foreach ( var member in staff.Values )
				behaviour.Step( member, walks[member.ThingId], playing: null, tick );
		}

		return (behaviour, staff, walks);
	}

	/// <summary>
	/// <b>The guard and the researcher stop standing still.</b> This is the whole point of the shared
	/// spine: both kinds finish a walk, roll, and set off again, so over a long run neither can be where
	/// the file left them.
	/// </summary>
	[TestMethod]
	public void TheGuardAndTheResearcherGoSomewhere()
	{
		var world = Park();

		var started = ParkPeople.StaffIn( world )
			.ToDictionary( member => member.ThingId, member => member.Navigator.Position );

		var (_, staff, _) = Run( world, turns: 400 );

		foreach ( var thing in new[] { Guard, Researcher } )
		{
			Assert.AreNotEqual( started[thing], staff[thing].Navigator.Position,
				$"staff {thing} is exactly where the save left them after four hundred turns" );
		}
	}

	/// <summary>
	/// <b>A guard never leaves their patch.</b> Every destination the behaviour hands them has to fall
	/// inside the patrol rectangle, which is what the candidate strike-out in the staff half of
	/// <c>FUN_004f9490</c> is for - the guest half has no such test, so an implementation that reused it
	/// would look right and let the guard wander off across the park.
	/// </summary>
	[TestMethod]
	public void AGuardOnlyEverAimsInsideTheirPatrolArea()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 4242 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var guard = ParkPeople.StaffIn( world ).Single( member => member.ThingId == Guard );
		var walk = new PeepWalk( guard.Navigator, blocked );

		var seen = new List<(int X, int Y)>();

		for ( var tick = world.GameTick + 1; tick <= world.GameTick + 400; ++tick )
		{
			behaviour.Step( guard, walk, playing: null, tick );

			// Collected only while walking, because that is the only state the behaviour has just chosen a
			// destination in - a standing guard still carries whatever the file left on them.
			if ( guard.Activity == StaffActivity.Walking )
			{
				seen.Add( (guard.Navigator.Target.X / PeepNavigator.One,
					guard.Navigator.Target.Y / PeepNavigator.One) );
			}
		}

		Assert.IsTrue( seen.Count > 0, "the guard never walked anywhere across four hundred turns" );

		foreach ( var (x, y) in seen.Distinct() )
		{
			Assert.IsTrue( guard.Patrols( x, y ),
				$"the guard was sent to ({x},{y}), outside the patrol area "
				+ $"{guard.PatrolFrom} to {guard.PatrolTo}" );
		}

		Assert.IsTrue( guard.Activity is StaffActivity.Walking or StaffActivity.Idle,
			"the shared spine can only leave a guard walking or standing" );
	}

	/// <summary>
	/// The three kinds whose work-finding is not built end up standing, rather than in some state the
	/// shared switch cannot produce. <b>This asserts the deferral rather than hiding it.</b>
	/// </summary>
	[TestMethod]
	public void TheKindsWithNoWorkToFindFinishTheirWalkAndStand()
	{
		var (_, staff, _) = Run( Park(), turns: 400 );

		foreach ( var thing in WithoutWork )
		{
			Assert.AreEqual( StaffActivity.Idle, staff[thing].Activity,
				$"staff {thing} has no work-finding arm built, so they should have come to a stand" );
		}
	}

	/// <summary>
	/// Walking wears a staff member down by <c>(6 - grade)</c> times the executable's own two rates, and a
	/// better grade wears down more slowly.
	/// </summary>
	[TestMethod]
	public void WalkingTiresThemAtTheRateTheirGradeEarns()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 11 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		// Thing 25 is saved walking at grade 3, so one turn of it is a turn of tiring.
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == 25 );
		var walk = new PeepWalk( member.Navigator, blocked );

		Assert.AreEqual( StaffActivity.Walking, member.Activity, "thing 25 should be saved mid-walk" );

		var tiredBefore = member.Tiredness;
		var happyBefore = member.Happiness;

		behaviour.Step( member, walk, playing: null, tick: world.GameTick + 1 );

		// Only meaningful if that turn was actually spent walking rather than arriving.
		if ( member.Activity != StaffActivity.Walking )
			Assert.Inconclusive( "thing 25 finished its walk on the first turn" );

		var scale = StaffBehaviour.TiringBase - member.PayGrade;

		Assert.AreEqual( tiredBefore - (scale * StaffBehaviour.TirednessPerWalkingTurn),
			member.Tiredness, 0.0001f, "one turn of walking costs (6 - grade) x 0.012 rest" );

		Assert.AreEqual( happyBefore - (scale * StaffBehaviour.HappinessPerWalkingTurn),
			member.Happiness, 0.0001f, "one turn of walking costs (6 - grade) x 0.005 mood" );
	}

	/// <summary>
	/// The per-grade constants are the balance file's own numbers, not literals - and they are read at the
	/// grade each staff member actually carries.
	/// </summary>
	[TestMethod]
	public void ThePerGradeConstantsComeFromTheBalanceFile()
	{
		var behaviour = new StaffBehaviour( Balance() );

		// PerGradeStaffConsts[3] in the shipped global file, which easy mode does not override.
		Assert.AreEqual( 10, behaviour.IdleDurationAt( 3 ), "grade 3 IdleDuration" );
		Assert.AreEqual( 0.5f, behaviour.RecuperationAt( 3 ), 0.001f, "grade 3 RecuperationRate" );
		Assert.AreEqual( 3f, behaviour.HappinessRecuperationAt( 3 ), 0.001f,
			"grade 3 HappinessRecuperationRate" );

		Assert.AreEqual( 20, behaviour.IdleDurationAt( 2 ), "grade 2 IdleDuration" );
		Assert.AreEqual( 0.4f, behaviour.RecuperationAt( 2 ), 0.001f, "grade 2 RecuperationRate" );

		Assert.AreEqual( 1, behaviour.RestLevel, "AllStaffConstants.RestLevel" );
		Assert.AreEqual( 2, behaviour.HappinessHitForNoRestArea, "AllStaffConstants.HappyHitCosNoRestArea" );
	}

	/// <summary>
	/// Resting climbs both stats by the grade's own rates and ends the moment rest is full, letting go of
	/// the rest area on the way out.
	/// </summary>
	[TestMethod]
	public void RestingRecoversThemAndThenSendsThemBackToWork()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 5 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );
		var walk = new PeepWalk( member.Navigator, blocked );

		member.Tiredness = 10f;
		member.Happiness = 10f;
		member.RestArea = 99;
		member.SetActivity( StaffActivity.Resting, tick: 1 );

		var rate = behaviour.RecuperationAt( member.PayGrade );

		behaviour.Step( member, walk, playing: null, tick: 2 );

		Assert.AreEqual( 10f + rate, member.Tiredness, 0.001f, "one turn of rest" );
		Assert.AreEqual( StaffActivity.Resting, member.Activity, "still resting after one turn" );

		// Long enough for the slowest grade to fill from ten.
		for ( var tick = 3; tick < 1000 && member.Activity == StaffActivity.Resting; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( StaffActivity.Idle, member.Activity, "a rested staff member goes back to work" );
		Assert.AreEqual( 100f, member.Tiredness, 0.001f, "and is fully rested" );
		Assert.AreEqual( 0, member.RestArea, "and has let go of the rest area" );
	}

	/// <summary>
	/// Entering the idle state stamps the clock only when they arrive there from a walk, which is the
	/// original's own asymmetry and is why three of the shipped park's five carry no stamp at all.
	/// </summary>
	[TestMethod]
	public void TheIdleStampIsTakenOnlyOnArrivingFromAWalk()
	{
		var world = Park();
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );

		member.SetActivity( StaffActivity.Walking, tick: 10 );
		member.SetActivity( StaffActivity.Idle, tick: 50 );

		Assert.AreEqual( 50, member.TimeStartedIdling, "arriving from a walk stamps the clock" );

		member.SetActivity( StaffActivity.Resting, tick: 60 );
		member.SetActivity( StaffActivity.Idle, tick: 70 );

		Assert.AreEqual( 0, member.TimeStartedIdling, "arriving from anything else clears it" );
	}

	/// <summary>
	/// <b>A saved idle stamp is read on the saved clock, not cleared.</b> Lost Kingdom's guard is saved idle since
	/// <c>mGameTick</c> 752 against the save's 755, at grade 3 (<c>IdleDuration</c> 10), so they stand with that stamp
	/// through sweep 762 and leave idle on 763, the eighth sweep, whose low two bits, 3, send them walking
	/// (<c>docs/exe/ride-operation.md</c>, "The idle wait").
	/// </summary>
	[TestMethod]
	public void TheGuardSavedIdleSince752WalksOnMGameTick763()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 3 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );
		var walk = new PeepWalk( member.Navigator, blocked );

		Assert.AreEqual( 755, world.GameTick, "the save's mGameTick" );
		Assert.AreEqual( StaffActivity.Idle, member.Activity, "the guard is saved idle" );
		Assert.AreEqual( 752, member.TimeStartedIdling, "since mGameTick 752" );
		Assert.AreEqual( 3, member.PayGrade, "at grade 3" );

		for ( var tick = 756; tick <= 762; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			Assert.AreEqual( StaffActivity.Idle, member.Activity, $"still idle on mGameTick {tick}" );
			Assert.AreEqual( 752, member.TimeStartedIdling, $"and still stamped 752 on mGameTick {tick}" );
		}

		behaviour.Step( member, walk, playing: null, tick: 763 );

		Assert.AreEqual( StaffActivity.Walking, member.Activity, "walking on mGameTick 763" );
	}

	/// <summary>
	/// <b>Nothing zeroes a stamp that reads ahead of the clock.</b> The original compares it raw, so a member of staff
	/// stamped after the current <c>mGameTick</c> stands until the clock passes stamp + <c>IdleDuration</c>
	/// (<c>docs/exe/ride-operation.md</c>, "The idle wait"). No shipped save holds one: this pins the rule.
	/// </summary>
	[TestMethod]
	public void AStampAheadOfTheClockIsLeftAlone()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 3 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );
		var walk = new PeepWalk( member.Navigator, blocked );

		behaviour.Step( member, walk, playing: null, tick: 101 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity, "a guard stamped 752 is still idle on mGameTick 101" );
		Assert.AreEqual( 752, member.TimeStartedIdling, "and the stamp is left as it was" );
	}

	/// <summary>
	/// <b>A guard's walk-or-stay is the clock's low two bits, not a draw</b> (<c>0x004d655d</c>): past the idle wait,
	/// one stays on a sweep that is a multiple of four and sets off on any other, whatever the draws.
	/// </summary>
	[TestMethod]
	public void AGuardStaysOnAMultipleOfFourWhateverTheDraws()
	{
		var world = Park();
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		for ( var seed = 0; seed < 16; ++seed )
		{
			foreach ( var tick in new[] { 1000, 1001, 1002, 1003 } )
			{
				var behaviour = new StaffBehaviour( Balance(), new Random( seed ) );
				var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );
				var walk = new PeepWalk( member.Navigator, blocked );

				// Idle from idle carries no stamp, so the wait is long over on any of these sweeps.
				member.SetActivity( StaffActivity.Idle, tick: 1 );

				behaviour.Step( member, walk, playing: null, tick );

				var expected = (tick & 3) == 0 ? StaffActivity.Idle : StaffActivity.Walking;

				Assert.AreEqual( expected, member.Activity, $"seed {seed}, mGameTick {tick}" );
			}
		}
	}

	/// <summary>
	/// <b>A researcher's walk-or-stay is still a draw</b> (<c>0x00502ba9</c>): on a sweep that is a multiple of four,
	/// where a guard always stays, some draws send the researcher walking and some keep them standing.
	/// </summary>
	[TestMethod]
	public void AResearchersChoiceIsADrawNotTheClock()
	{
		var world = Park();
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var outcomes = new HashSet<StaffActivity>();

		for ( var seed = 0; seed < 16; ++seed )
		{
			var behaviour = new StaffBehaviour( Balance(), new Random( seed ) );
			var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Researcher );
			var walk = new PeepWalk( member.Navigator, blocked );

			// Idle from walking, stamped long ago, so the wait is over on the sweep below.
			member.SetActivity( StaffActivity.Idle, tick: 1 );

			behaviour.Step( member, walk, playing: null, tick: 1000 );

			outcomes.Add( member.Activity );
		}

		CollectionAssert.AreEquivalent( new[] { StaffActivity.Idle, StaffActivity.Walking }, outcomes.ToArray(),
			"across sixteen draws on mGameTick 1000 the researcher both stays and walks" );
	}

	/// <summary>
	/// <b>State 6's wait is unsigned</b>, as <c>FUN_005056e0</c>'s (<c>0x00505745</c>): a member leaves it once
	/// mGameTick − stamp is more than three idle durations, so a stamp ahead of the clock wraps and lets them go at
	/// once. Only a saved <c>mState</c> enters state 6, and no shipped save holds one: this pins the compare.
	/// </summary>
	[TestMethod]
	public void TheWaitingStateComparesUnsigned()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 3 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );
		var walk = new PeepWalk( member.Navigator, blocked );

		// Grade 3: IdleDuration 10, so thirty sweeps.
		member.SetActivity( StaffActivity.Waiting, tick: 1000 );

		behaviour.Step( member, walk, playing: null, tick: 1030 );
		Assert.AreEqual( StaffActivity.Waiting, member.Activity, "thirty sweeps on, still waiting" );

		behaviour.Step( member, walk, playing: null, tick: 1031 );
		Assert.AreEqual( StaffActivity.Idle, member.Activity, "thirty-one on, idle" );

		member.SetActivity( StaffActivity.Waiting, tick: 900 );

		behaviour.Step( member, walk, playing: null, tick: 101 );
		Assert.AreEqual( StaffActivity.Idle, member.Activity, "a stamp ahead of the clock wraps and is over at once" );
	}

	/// <summary>
	/// One frame, through both clocks, in the order a frame runs them: <c>Time.Update</c> in
	/// <c>Renderer.Update</c>, then <c>GameClock.Update</c> in <see cref="Level.Update"/>.
	/// </summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>Entering a park: the clock re-bases, and the frame spanning the load is spent.</summary>
	private static void EnterPark()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );
	}

	/// <summary>
	/// <b>The park's sweep hands its staff the park's clock.</b> Driven through <see cref="ParkPeople"/> frame by
	/// frame, so what is measured is the wiring and not <see cref="StaffBehaviour"/> alone: a sweep that handed them
	/// the 31 ms counter, eight to a sweep from wherever the process left it, fails here.
	/// <para>
	/// Read after every sweep: the guard keeps the save's 752 through 762; every idle spell after a walk is stamped
	/// with the sweep's <c>mGameTick</c> and holds it 11 sweeps for the guard (grade 3) and 21 for the researcher
	/// (grade 2); and the guard never sets off from idle on a multiple of four.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheParksSweepHandsTheStaffItsOwnClock()
	{
		const int sweeps = 400;

		var world = Park();
		var people = new ParkPeople( world, Balance() );

		try
		{
			EnterPark();

			var seen = new List<(int Tick, Dictionary<int, (StaffActivity Activity, int Stamp)> Staff)>();
			var last = people.State.GameTick;

			while ( seen.Count < sweeps )
			{
				Frame( GameClock.TickSeconds );
				people.Update();

				if ( people.State.GameTick == last )
					continue;

				Assert.AreEqual( last + 1, people.State.GameTick, "one sweep a frame at the most" );

				last = people.State.GameTick;
				seen.Add( (last, people.Staff.ToDictionary( member => member.ThingId,
					member => (member.Activity, member.TimeStartedIdling) )) );
			}

			Assert.AreEqual( 756, seen[0].Tick, "the first sweep is one past the save's mGameTick" );

			for ( var i = 0; i < 7; ++i )
			{
				Assert.AreEqual( (StaffActivity.Idle, 752), seen[i].Staff[Guard],
					$"the guard on mGameTick {seen[i].Tick}" );
			}

			Assert.AreNotEqual( (StaffActivity.Idle, 752), seen[7].Staff[Guard], "the guard on mGameTick 763" );

			foreach ( var (thing, holds) in new[] { (Guard, 11), (Researcher, 21) } )
			{
				var spells = 0;

				for ( var i = 1; i + holds < seen.Count; ++i )
				{
					if ( seen[i - 1].Staff[thing].Activity != StaffActivity.Walking
						|| seen[i].Staff[thing].Activity != StaffActivity.Idle )
						continue;

					var tick = seen[i].Tick;

					++spells;

					for ( var j = i; j < i + holds; ++j )
					{
						Assert.AreEqual( (StaffActivity.Idle, tick), seen[j].Staff[thing],
							$"staff {thing}'s spell from mGameTick {tick}, read on {seen[j].Tick}" );
					}

					Assert.AreNotEqual( (StaffActivity.Idle, tick), seen[i + holds].Staff[thing],
						$"staff {thing}'s spell from mGameTick {tick} is over on {seen[i + holds].Tick}" );
				}

				Assert.IsTrue( spells >= 3, $"staff {thing} ended only {spells} walks in {sweeps} sweeps" );
			}

			var setOff = 0;

			for ( var i = 1; i < seen.Count; ++i )
			{
				if ( seen[i - 1].Staff[Guard].Activity != StaffActivity.Idle
					|| seen[i].Staff[Guard].Activity != StaffActivity.Walking )
					continue;

				++setOff;

				Assert.AreNotEqual( 0, seen[i].Tick & 3, $"the guard set off from idle on mGameTick {seen[i].Tick}" );
			}

			Assert.IsTrue( setOff >= 3, $"the guard set off from idle only {setOff} times in {sweeps} sweeps" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// A staff member with no patrol area is at home anywhere, and one whose rectangle is the whole map
	/// is too - the researcher's is exactly that.
	/// </summary>
	[TestMethod]
	public void APatrolAreaOfTheWholeMapPensNobodyIn()
	{
		var researcher = ParkPeople.StaffIn( Park() ).Single( staff => staff.ThingId == Researcher );

		Assert.IsTrue( researcher.HasPatrolArea, "the researcher does carry an area" );
		Assert.IsTrue( researcher.Patrols( 0, 0 ), "the corner of the map" );
		Assert.IsTrue( researcher.Patrols( ParkWorld.MapSize - 1, ParkWorld.MapSize - 1 ), "the far corner" );
		Assert.IsFalse( researcher.Patrols( -1, 0 ), "off the map is still off the map" );
	}
}
