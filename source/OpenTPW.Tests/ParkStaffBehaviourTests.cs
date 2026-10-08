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

	/// <summary>The mechanic, the handyman and the entertainer, who find no work in the park as it ships.</summary>
	private const int Handyman = 25;

	private const int Mechanic = 26;

	private const int Entertainer = 27;

	private static readonly int[] WithoutWork = [Handyman, Mechanic, Entertainer];

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

	/// <summary>The saved ids are the kinds the tests take them for.</summary>
	[TestMethod]
	public void TheThreeIdsAreTheMechanicTheHandymanAndTheEntertainer()
	{
		var staff = ParkPeople.StaffIn( Park() ).ToDictionary( member => member.ThingId, member => member.Model );

		Assert.AreEqual( 4, staff[Mechanic] );
		Assert.AreEqual( 5, staff[Handyman] );
		Assert.AreEqual( 6, staff[Entertainer] );
	}

	/// <summary>One member standing on the path at (48,22) in the live park, idle with the stamp given.</summary>
	private (Staff Member, PeepWalk Walk, ParkState State) OnThePath( int thing, int stamp = 0, bool shut = false,
		StaffActivity activity = StaffActivity.Idle )
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == thing );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)activity, TimeStartedIdling = stamp,
			PatrolBottomLeft = MapStep.CellId( 0, 0 ), PatrolTopRight = MapStep.CellId( 127, 127 )
		}, saved.Navigator );

		member.Happiness = 50f;
		member.Tiredness = 80f;
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.Navigator.Target = member.Navigator.Position;

		var walk = shut
			? new PeepWalk( member.Navigator, ( _, _, _ ) => true )
			: new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );

		return (member, walk, state);
	}

	/// <summary>
	/// With no work the mechanic and the handyman set off on a random walk on every decide, whatever the clock's low
	/// bits and whatever the draws (<c>0x004da6fa</c>, <c>0x004d712d</c>): on a multiple of four, where a guard
	/// stays, they walk.
	/// </summary>
	[DataTestMethod]
	[DataRow( Mechanic )]
	[DataRow( Handyman )]
	public void TheMechanicAndTheHandymanWalkOnEveryDecide( int thing )
	{
		for ( var tick = 1000; tick < 1004; ++tick )
		{
			foreach ( var draws in new Random[] { new Random( tick ), new ConstantDraw(), new CountedDraw( 0 ) } )
			{
				var (member, walk, state) = OnThePath( thing );

				new StaffBehaviour( Balance(), draws, state ).Step( member, walk, playing: null, tick );

				Assert.AreEqual( StaffActivity.Walking, member.Activity, $"mGameTick {tick}" );
				Assert.AreNotEqual( (48, 22), member.Navigator.Target.Cell, "and has somewhere else to go" );
			}
		}
	}

	/// <summary>
	/// The three kinds keep moving in the shipped park: over four hundred sweeps each ends somewhere the save did
	/// not leave them, and the mechanic and the handyman stand on none of the last hundred.
	/// </summary>
	[TestMethod]
	public void TheKindsWithNoWorkToFindWalkAbout()
	{
		var world = Park();
		var state = new ParkState( world );
		var behaviour = new StaffBehaviour( Balance(), new Random( 1234 ), state );
		var blocked = new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked;
		var staff = ParkPeople.StaffIn( world ).ToDictionary( member => member.ThingId );
		var walks = staff.Values.ToDictionary( member => member.ThingId, member => new PeepWalk( member.Navigator, blocked ) );
		var cells = WithoutWork.ToDictionary( thing => thing, _ => new HashSet<(int, int)>() );
		var stood = WithoutWork.ToDictionary( thing => thing, _ => 0 );

		for ( var tick = world.GameTick + 1; tick <= world.GameTick + 400; ++tick )
		{
			foreach ( var member in staff.Values )
				behaviour.Step( member, walks[member.ThingId], playing: null, tick );

			foreach ( var thing in WithoutWork )
			{
				cells[thing].Add( staff[thing].Navigator.Position.Cell );

				if ( tick > world.GameTick + 300 && staff[thing].Activity == StaffActivity.Idle )
					++stood[thing];
			}
		}

		foreach ( var thing in WithoutWork )
			Assert.IsTrue( cells[thing].Count >= 5, $"staff {thing} stood on {cells[thing].Count} cells in four hundred sweeps" );

		Assert.AreEqual( 0, stood[Mechanic], "the mechanic never stands" );
		Assert.AreEqual( 0, stood[Handyman], "the handyman never stands" );
		Assert.IsTrue( stood[Entertainer] > 0, "the entertainer stands on the clock's multiples of four" );
	}

	/// <summary>
	/// A mechanic or a handyman who finds nowhere to walk is set idle (<c>FUN_004da370( 0 )</c>,
	/// <c>FUN_004d7330( 0 )</c>): stamped nought from standing, the clock from a walk. An entertainer is left as
	/// they were, state and stamp, since <c>FUN_004d46d0</c> calls no setter there.
	/// </summary>
	[DataTestMethod]
	[DataRow( Mechanic, StaffActivity.Idle, StaffActivity.Idle, 0 )]
	[DataRow( Handyman, StaffActivity.Idle, StaffActivity.Idle, 0 )]
	[DataRow( Entertainer, StaffActivity.Idle, StaffActivity.Idle, 5 )]
	[DataRow( Mechanic, StaffActivity.Walking, StaffActivity.Idle, 1001 )]
	[DataRow( Handyman, StaffActivity.Walking, StaffActivity.Idle, 1001 )]
	[DataRow( Entertainer, StaffActivity.Walking, StaffActivity.Walking, 5 )]
	public void FindingNowhereToWalkSetsIdleButLeavesAnEntertainerAsTheyWere(
		int thing, StaffActivity from, StaffActivity to, int stamp )
	{
		var (member, walk, state) = OnThePath( thing, stamp: 5, shut: true, activity: from );

		new StaffBehaviour( Balance(), new ConstantDraw(), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( to, member.Activity );
		Assert.AreEqual( stamp, member.TimeStartedIdling );
	}

	/// <summary>
	/// The entertainer's choice (<c>FUN_004d46d0</c>): on a multiple of four they stay, stamped, after one draw for
	/// the performance and one thrown away; on any other sweep they walk, after the performance's draw, the
	/// walk's own - as many as a guard's walk from the same cell takes - and one thrown away. The look for a guest
	/// is counted on a draw that divides by three, and only then.
	/// </summary>
	[DataTestMethod]
	[DataRow( 1000, 3, StaffActivity.Idle, 1 )]
	[DataRow( 1000, 4, StaffActivity.Idle, 0 )]
	[DataRow( 1001, 3, StaffActivity.Walking, 1 )]
	[DataRow( 1002, 4, StaffActivity.Walking, 0 )]
	[DataRow( 1003, 5, StaffActivity.Walking, 0 )]
	public void AnEntertainerStaysOnAMultipleOfFourAndLooksForGuestsOnADrawInThree(
		int tick, int draw, StaffActivity to, int looks )
	{
		// A guard's decide draws nothing but its walk's own.
		var (guard, guardWalk, guardState) = OnThePath( Guard, activity: StaffActivity.Walking );
		var walkDraws = new CountedDraw( draw );

		new StaffBehaviour( Balance(), walkDraws, guardState ).Step( guard, guardWalk, playing: null, tick );

		var (member, walk, state) = OnThePath( Entertainer, activity: StaffActivity.Walking );
		var random = new CountedDraw( draw );
		var asked = 0;

		new StaffBehaviour( Balance(), random, state )
		{
			GuestsNear = ( _, _ ) =>
			{
				++asked;

				return 0;
			}
		}.Step( member, walk, playing: null, tick );

		Assert.AreEqual( to, member.Activity );
		Assert.AreEqual( guard.Activity, member.Activity, "the guard's choice, by the same clock" );
		Assert.AreEqual( looks, asked );
		Assert.AreEqual( walkDraws.Asked + 2, random.Asked );

		if ( to == StaffActivity.Idle )
		{
			Assert.AreEqual( 1, walkDraws.Asked, "the walking turn's draw for a sound and no other" );
			Assert.AreEqual( tick, member.TimeStartedIdling, "idle from a walk stamps the clock" );
		}
	}

	/// <summary>
	/// Each search for work is counted where the original makes it: the mechanic's ride on every decide
	/// (<c>FUN_004daa90</c>), the handyman's litter on every decide (<c>FUN_004c8ed0</c>), and nobody else's. His
	/// toilet search is built (<see cref="ParkHandymanCleanTests"/>).
	/// </summary>
	[DataTestMethod]
	[DataRow( Mechanic, 1, 0 )]
	[DataRow( Handyman, 0, 1 )]
	[DataRow( Entertainer, 0, 0 )]
	[DataRow( Guard, 0, 0 )]
	[DataRow( Researcher, 0, 0 )]
	public void EachSearchForWorkIsCountedAtItsOwnKindsDecide( int thing, int ride, int litter )
	{
		var (member, walk, state) = OnThePath( thing );
		var before = (Counted( "MECHANIC_RIDE_SEARCH" ), Counted( "HANDYMAN_LITTER_SEARCH" ));

		// 1001 divides by no grade's idle duration, so the handyman's pre-step is not counted beside it.
		new StaffBehaviour( Balance(), new ConstantDraw(), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( ride, Counted( "MECHANIC_RIDE_SEARCH" ) - before.Item1 );
		Assert.AreEqual( litter, Counted( "HANDYMAN_LITTER_SEARCH" ) - before.Item2 );
	}

	/// <summary>
	/// An idle handyman's pre-step looks for litter on the sweeps the clock divides by his grade's idle duration,
	/// while he still waits (<c>FUN_004d7060</c>, <c>0x004d7087</c>), and on no other sweep and in no other state.
	/// </summary>
	[DataTestMethod]
	[DataRow( Handyman, StaffActivity.Idle, 1000, 1 )]
	[DataRow( Handyman, StaffActivity.Idle, 1001, 0 )]
	[DataRow( Handyman, StaffActivity.Resting, 1000, 0 )]
	[DataRow( Mechanic, StaffActivity.Idle, 1000, 0 )]
	public void AnIdleHandymansPreStepLooksForLitterWhenTheClockDividesByHisIdleDuration(
		int thing, StaffActivity activity, int tick, int looks )
	{
		// Stamped at the clock, so the wait is not over and no decide runs.
		var (member, walk, state) = OnThePath( thing, stamp: tick, activity: activity );
		var behaviour = new StaffBehaviour( Balance(), new ConstantDraw(), state );
		var before = Counted( "HANDYMAN_LITTER_SEARCH" );

		Assert.AreEqual( 0, tick % 1000 == 0 ? 1000 % behaviour.IdleDurationAt( member.PayGrade ) : 0,
			"1000 divides by the member's idle duration" );

		behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( looks, Counted( "HANDYMAN_LITTER_SEARCH" ) - before );
	}

	/// <summary>
	/// Every kind's decide opens with the tired test (<c>FUN_00506a40</c>): a tired mechanic, handyman or
	/// entertainer sets off for the Staff Room and looks for no work.
	/// </summary>
	[DataTestMethod]
	[DataRow( Mechanic )]
	[DataRow( Handyman )]
	[DataRow( Entertainer )]
	public void ATiredMemberOfAnyKindGoesToRestAndLooksForNoWork( int thing )
	{
		var (member, walk, state) = OnThePath( thing );
		var before = Counted( "MECHANIC_RIDE_SEARCH" ) + Counted( "HANDYMAN_LITTER_SEARCH" );
		var asked = 0;

		member.Tiredness = 0.5f;

		new StaffBehaviour( Balance(), new CountedDraw( 0 ), state )
		{
			GuestsNear = ( _, _ ) => ++asked
		}.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.GoingToRest, member.Activity );
		Assert.AreNotEqual( 0, member.RestArea );
		Assert.AreEqual( before, Counted( "MECHANIC_RIDE_SEARCH" ) + Counted( "HANDYMAN_LITTER_SEARCH" ) );
		Assert.AreEqual( 0, asked, "and the entertainer does not look" );
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
	/// <b>A researcher's walk-or-research is a draw</b> (<c>0x00502ba9</c>): on a sweep that is a multiple of four,
	/// where a guard always stays, some draws send the researcher walking and some set them researching.
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

		CollectionAssert.AreEquivalent( new[] { StaffActivity.Researching, StaffActivity.Walking }, outcomes.ToArray(),
			"across sixteen draws on mGameTick 1000 the researcher both researches and walks" );
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
	/// with the sweep's <c>mGameTick</c> and holds it 11 sweeps for the guard (grade 3); every spell of research
	/// after a walk is stamped the same way and holds it 31 sweeps for the researcher (grade 2), who is never idle
	/// once the first walk has ended; and the guard never sets off from idle on a multiple of four.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheParksSweepHandsTheStaffItsOwnClock()
	{
		const int sweeps = 400;

		var world = Park();

		// Every generator seeded: how long a walk lasts is drawn, and two seeds of 300 leave the guard or the
		// researcher with fewer than the three ended walks asked for below.
		var people = new ParkPeople( world, Balance(), random: new Random( 1 ), behaviourRandom: new Random( 1 ),
			rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

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
					member => (member.Activity, member.Activity == StaffActivity.Researching
						? member.TimeStartedResearching
						: member.TimeStartedIdling) )) );
			}

			Assert.AreEqual( 756, seen[0].Tick, "the first sweep is one past the save's mGameTick" );

			for ( var i = 0; i < 7; ++i )
			{
				Assert.AreEqual( (StaffActivity.Idle, 752), seen[i].Staff[Guard],
					$"the guard on mGameTick {seen[i].Tick}" );
			}

			Assert.AreNotEqual( (StaffActivity.Idle, 752), seen[7].Staff[Guard], "the guard on mGameTick 763" );

			foreach ( var (thing, holds, spell) in new[]
				{ (Guard, 11, StaffActivity.Idle), (Researcher, 31, StaffActivity.Researching) } )
			{
				var spells = 0;

				for ( var i = 1; i + holds < seen.Count; ++i )
				{
					if ( seen[i - 1].Staff[thing].Activity != StaffActivity.Walking
						|| seen[i].Staff[thing].Activity != spell )
						continue;

					var tick = seen[i].Tick;

					++spells;

					for ( var j = i; j < i + holds; ++j )
					{
						Assert.AreEqual( (spell, tick), seen[j].Staff[thing],
							$"staff {thing}'s spell from mGameTick {tick}, read on {seen[j].Tick}" );
					}

					Assert.AreNotEqual( (spell, tick), seen[i + holds].Staff[thing],
						$"staff {thing}'s spell from mGameTick {tick} is over on {seen[i + holds].Tick}" );
				}

				Assert.IsTrue( spells >= 3, $"staff {thing} ended only {spells} walks in {sweeps} sweeps" );
			}

			var firstResearch = seen.FindIndex( sweep => sweep.Staff[Researcher].Activity == StaffActivity.Researching );

			Assert.IsFalse( seen.Skip( firstResearch ).Any( sweep => sweep.Staff[Researcher].Activity == StaffActivity.Idle ),
				"the researcher never stands idle of its own accord" );

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
	[DataTestMethod]
	[DataRow( 28, 48, 22 )]
	[DataRow( 30, 48, 22 )]
	[DataRow( 30, 47, 17 )]
	[DataRow( 30, 48, 17 )]
	public void WanderingStaffStayOnPathsBesideQueuesAndTheGate( int id, int x, int y )
	{
		var world = Park();
		var state = new ParkState( world );
		var moved = 0;

		for ( var seed = 0; seed < 32; ++seed )
		{
			var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == id );
			member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );
			member.SetActivity( StaffActivity.Idle, tick: 1 );
			var walk = new PeepWalk( member.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );
			var behaviour = new StaffBehaviour( Balance(), new Random( seed ), state );

			for ( var tick = 1001; tick <= 1400; ++tick )
			{
				behaviour.Step( member, walk, playing: null, tick );
				var cell = member.Navigator.Position.Cell;
				Assert.AreEqual( CellEdge.Path, state.Record( cell.X, cell.Y ).Type,
					$"staff {id}, seed {seed}, tick {tick}: walked onto {cell}; at={member.Navigator.Position} target={member.Navigator.Target} previous={member.Navigator.Previous} velocity={member.Navigator.Velocity} activity={member.Activity}" );
				if ( cell != (x, y) )
					++moved;
			}
		}

		Assert.IsTrue( moved > 0, "refusing every walk is not a fix" );
	}

	[DataTestMethod]
	[DataRow( 3 )]
	[DataRow( 9 )]
	[DataRow( 30 )]
	public void PatrolFallbackRejectsReachableNonPathDestinations( int type )
	{
		var world = Park();
		var state = new ParkState( world );
		state.SetRecord( 49, 22, state.Record( 49, 22 ) with { Type = type, Neighbours = 0x44 } );
		// An entrance source may route into another queue cell; the path-only patrol gate must reject it.
		state.SetRecord( 48, 22, state.Record( 48, 22 ) with { Type = CellEdge.RideEnd, Neighbours = 0x04 } );
		// The guard, whose choice with nowhere found is to stand; a researcher's is to research.
		var saved = world.People.Single( person => person.ThingId == Guard );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Idle, TimeStartedIdling = 0,
			PatrolBottomLeft = MapStep.CellId( 49, 22 ), PatrolTopRight = MapStep.CellId( 49, 22 )
		}, saved.Navigator );
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		var edge = new CellEdge( state.Record, ParkPeople.WalkingMode );
		var walk = new PeepWalk( member.Navigator, edge.Blocked );
		member.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 49 ), PeepNavigator.WaypointCentre( 22 ) );
		Assert.IsTrue( walk.PlanRoute(), "the non-path destination must be reachable to expose the defect" );
		var behaviour = new StaffBehaviour( Balance(), new ConstantDraw(), state );
		behaviour.Step( member, walk, playing: null, tick: 1001 );
		Assert.AreEqual( StaffActivity.Idle, member.Activity, "a reachable queue/entrance/approach is not a patrol destination" );
		Assert.AreEqual( 0x16, member.Thoughts.Last, "thirty failed tries think 0x16 (0x0050701f)" );
		member.Thoughts.Restore( 0, 0 );

		state.SetRecord( 49, 22, state.Record( 49, 22 ) with { Type = CellEdge.Path } );
		behaviour.Step( member, walk, playing: null, tick: 1002 );
		Assert.AreEqual( StaffActivity.Walking, member.Activity, "the same live cell converted to path must be accepted" );
		Assert.AreEqual( 0, member.Thoughts.Last, "a roll that finds a cell thinks nothing" );
	}

	[DataTestMethod]
	[DataRow( 3, 0x04, false )]
	[DataRow( 3, 0x40, true )]
	[DataRow( 9, 0x40, false )]
	public void AQueueWanderHeadsOutButAnEntranceKeepsItsLinks( int type, int facing, bool east )
	{
		var world = Park();
		var state = new ParkState( world );
		state.SetRecord( 50, 22, state.Record( 50, 22 ) with { Type = type, Direction = (byte)facing } );
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Researcher );
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 50 ), PeepNavigator.WaypointCentre( 22 ) );
		member.SetActivity( StaffActivity.Idle, tick: 1 );
		var walk = new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );
		// The draw's slot 3 is west (bit 0x40). A facing exclusion must still allow the opposite direction, and
		// an entrance keeps the slot its facing names.
		var behaviour = new StaffBehaviour( Balance(), new ConstantDraw(), state );
		behaviour.Step( member, walk, playing: null, tick: 1001 );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		Assert.AreEqual( (east ? 51 : 49, 22), LinkedWander.Walk( state.Record, 50, 22, 1, new ConstantDraw() )![0] );
	}

	/// <summary>
	/// A member of staff inside their patrol area walks the linked walk: up to five cells with the whole map
	/// to patrol, and never aimed outside a small area, whose outside slots are struck out on every pass.
	/// </summary>
	[TestMethod]
	public void AStaffWanderWalksUpToFiveCellsAndKeepsInsideThePatrolArea()
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Researcher );
		var farthest = 0;
		var fencedWalks = 0;

		for ( var seed = 0; seed < 200; ++seed )
		{
			foreach ( var fenced in new[] { false, true } )
			{
				var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
				{
					State = (int)StaffActivity.Idle, TimeStartedIdling = 0,
					PatrolBottomLeft = fenced ? MapStep.CellId( 47, 18 ) : MapStep.CellId( 0, 0 ),
					PatrolTopRight = fenced ? MapStep.CellId( 48, 25 ) : MapStep.CellId( 127, 127 )
				}, saved.Navigator );

				member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
				Assert.IsTrue( member.Patrols( 48, 22 ) );

				var walk = new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );

				new StaffBehaviour( Balance(), new Random( seed ), state ).Step( member, walk, playing: null, tick: 1001 );

				if ( member.Activity != StaffActivity.Walking )
					continue;

				var (x, y) = member.Navigator.Target.Cell;
				var cells = Math.Abs( x - 48 ) + Math.Abs( y - 22 );

				Assert.IsTrue( cells is >= 1 and <= LinkedWander.MostPasses, $"seed {seed} aims {cells} cells off" );

				if ( fenced )
				{
					++fencedWalks;
					Assert.IsTrue( member.Patrols( x, y ), $"seed {seed} aims outside the area, into ({x},{y})" );
				}
				else
					farthest = Math.Max( farthest, cells );
			}
		}

		Assert.IsTrue( fencedWalks > 50, $"only {fencedWalks} fenced walks" );
		Assert.AreEqual( LinkedWander.MostPasses, farthest, "a walk of five passes ends five cells off" );
	}

	[TestMethod]
	public void AStaffWanderUsesTheSourceLinkAndRejectsAnExit()
	{
		var world = Park();
		var state = new ParkState( world );
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Researcher );
		var behaviour = new StaffBehaviour( Balance(), new ConstantDraw(), state );
		foreach ( var type in new[] { CellEdge.Path, CellEdge.RideFarEnd } )
		{
			// East destination links back, but source north/south links must take precedence.
			state.SetRecord( 48, 22, state.Record( 48, 22 ) with { Neighbours = (byte)(type == CellEdge.Path ? 0x11 : 0x15) } );
			state.SetRecord( 49, 22, state.Record( 49, 22 ) with { Type = type } );
			member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
			member.SetActivity( StaffActivity.Idle, tick: 1 );
			// A permissive route delegate isolates destination selection from CellEdge's own exit refusal.
			var walk = new PeepWalk( member.Navigator, ( x, y, direction ) => MapStep.LeavesTheMap( x, y, direction ) );
			behaviour.Step( member, walk, playing: null, tick: 1001 );
			Assert.AreEqual( StaffActivity.Walking, member.Activity );
			Assert.AreNotEqual( (49, 22), member.Navigator.Target.Cell );
		}
	}

	[TestMethod]
	public void AStaffRestWalkCanEnterAQueueAfterAConstrainedWander()
	{
		var world = Park();
		var state = new ParkState( world );
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Researcher );
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.SetActivity( StaffActivity.Idle, tick: 1 );
		var walk = new PeepWalk( member.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );
		var behaviour = new StaffBehaviour( Balance(), new ConstantDraw(), state );
		behaviour.Step( member, walk, playing: null, tick: 1001 );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		behaviour.Step( member, walk, playing: null, tick: 1002 );

		member.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 49 ), PeepNavigator.WaypointCentre( 22 ) );
		Assert.IsTrue( walk.PlanRoute(), "the wander policy must not leak into an intentional destination" );
		member.SetActivity( StaffActivity.GoingToRest, tick: 1003 );
		for ( var tick = 1004; tick < 1104 && member.Activity == StaffActivity.GoingToRest; ++tick )
			behaviour.Step( member, walk, playing: null, tick );
		Assert.AreEqual( StaffActivity.Resting, member.Activity );
		Assert.AreEqual( (49, 22), member.Navigator.Position.Cell );
	}

	[TestMethod]
	public void AFailedWanderConstraintDoesNotLeakIntoTheNextRoute()
	{
		var world = Park();
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Researcher );
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 49 ), PeepNavigator.WaypointCentre( 22 ) );
		var walk = new PeepWalk( member.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );
		Assert.ThrowsException<InvalidOperationException>( () => walk.PlanRoute( ( x, y, direction ) => throw new InvalidOperationException() ) );
		Assert.IsTrue( walk.PlanRoute(), "an exceptional constrained search must restore ordinary routing" );
	}

	/// <summary>
	/// A guard put down on grass inside their area takes SetRandomDest's no-links arm (<c>0x004f95c0</c>): the first
	/// path cell on the seven rays, in the table's order, and not the patrol roll's random cell. From (42,24) the
	/// path three cells west and the path three cells north are both nearer on the map than the answer, which is
	/// the third cell of the north-east ray, tried first.
	/// </summary>
	[DataTestMethod]
	[DataRow( 42, 24, 45, 21 )]
	[DataRow( 54, 24, 56, 26 )]
	[DataRow( 42, 26, 44, 28 )]
	public void AGuardPutDownOnGrassInTheirAreaWalksToTheFirstPathOnTheRays( int x, int y, int toX, int toY )
	{
		for ( var seed = 0; seed < 16; ++seed )
		{
			var world = Park();
			var state = new ParkState( world );
			var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );

			Assert.IsTrue( member.Patrols( x, y ), "the cell is inside the guard's area" );
			Assert.AreEqual( 0, state.Record( x, y ).Neighbours, "and has no links" );

			member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );
			member.Navigator.Target = member.Navigator.Position;
			member.SetActivity( StaffActivity.Idle, tick: 1 );

			var walk = new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );
			var behaviour = new StaffBehaviour( Balance(), new Random( seed ), state );

			behaviour.Step( member, walk, playing: null, tick: 1001 );

			Assert.AreEqual( StaffActivity.Walking, member.Activity, $"seed {seed}" );
			Assert.AreEqual( PeepNavigator.WaypointCentre( toX ), member.Navigator.Target.X, $"seed {seed}: the cell's centre, x" );
			Assert.AreEqual( PeepNavigator.WaypointCentre( toY ), member.Navigator.Target.Y, $"seed {seed}: the cell's centre, y" );
			Assert.AreEqual( 0, member.Thoughts.Last, "the arm thinks nothing" );

			for ( var tick = 1002; tick < 1100 && member.Navigator.Position.Cell != (toX, toY); ++tick )
				behaviour.Step( member, walk, playing: null, tick );

			Assert.AreEqual( (toX, toY), member.Navigator.Position.Cell, $"seed {seed}: the walk arrives" );
		}
	}

	/// <summary>
	/// Outside the patrol area the roll comes first, and its failure falls through to the count (<c>0x004f95af</c>):
	/// on a cell with no links the member still takes the no-links arm.
	/// </summary>
	[TestMethod]
	public void StaffOutsideTheirAreaOnGrassTakeTheSameArmOnceThePatrolRollFails()
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Guard );

		// An area of one grass cell: the roll takes only path, so all thirty tries fail.
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Idle, TimeStartedIdling = 0,
			PatrolBottomLeft = MapStep.CellId( 36, 14 ), PatrolTopRight = MapStep.CellId( 36, 14 )
		}, saved.Navigator );

		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 42 ), PeepNavigator.WaypointCentre( 24 ) );

		var walk = new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );

		new StaffBehaviour( Balance(), new Random( 7 ), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 0x16, member.Thoughts.Last, "the patrol roll ran and failed first (0x0050701f)" );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		Assert.AreEqual( (45, 21), member.Navigator.Target.Cell );
	}

	/// <summary>
	/// With no path on the rays that routes, the arm draws five cells, x before y, and five failures answer nought:
	/// no patrol roll follows, and nothing is thought (<c>0x004f9d19</c>).
	/// </summary>
	[TestMethod]
	public void FiveFailedTriesFromGrassLeaveAGuardStandingWithNoPatrolRoll()
	{
		var world = Park();
		var state = new ParkState( world );
		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );

		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 42 ), PeepNavigator.WaypointCentre( 24 ) );
		member.Navigator.Target = member.Navigator.Position;
		member.SetActivity( StaffActivity.Idle, tick: 1 );

		// Every edge shut, so no aim routes.
		var walk = new PeepWalk( member.Navigator, ( _, _, _ ) => true );
		var draws = new CountedDraw( 3 );

		new StaffBehaviour( Balance(), draws, state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.AreEqual( 0, member.Thoughts.Last, "a patrol roll would have thought 0x16" );
		Assert.AreEqual( 2 + (2 * PeepBehaviour.NowhereTries), draws.Asked,
			"the turn's draw for a sound, the call's pass draw, then x and y of five tries" );
	}

	private sealed class ConstantDraw : Random
	{
		public override int Next() => 3;
	}

	/// <summary>Answers one value and counts how often it was asked.</summary>
	private sealed class CountedDraw( int value ) : Random
	{
		public int Asked { get; private set; }

		public override int Next()
		{
			++Asked;

			return value;
		}
	}

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	/// <summary>One idle guard about to decide; on a multiple of four the mood's draw is the only one taken.</summary>
	private (Staff Member, PeepWalk Walk) Deciding( ParkWorld world, float happiness, float tiredness )
	{
		var saved = world.People.Single( person => person.ThingId == Guard );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Idle, TimeStartedIdling = 0
		}, saved.Navigator );

		member.Happiness = happiness;
		member.Tiredness = tiredness;

		return (member, new PeepWalk( member.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ));
	}

	/// <summary>
	/// The thought a decide opens with (<c>FUN_00506a40</c>): unhappy at a happiness byte of 10 or less, very
	/// happy above 97 on a draw whose low four bits are nought, and that draw taken only above 97.
	/// </summary>
	[DataTestMethod]
	[DataRow( 10.9f, 0, 1, 0, 0 )]
	[DataRow( 11f, 0, 0, 0, 0 )]
	[DataRow( 97.9f, 16, 0, 0, 0 )]
	[DataRow( 98f, 16, 0, 1, 1 )]
	[DataRow( 98f, 17, 0, 0, 1 )]
	[DataRow( 98f, 8, 0, 0, 1 )]
	public void ADecidingMemberThinksOfTheirMood( float happiness, int draw, int unhappy, int veryHappy, int draws )
	{
		var world = Park();
		var (member, walk) = Deciding( world, happiness, tiredness: 80f );
		var random = new CountedDraw( draw );
		new StaffBehaviour( Balance(), random ).Step( member, walk, playing: null, tick: 1000 );

		Assert.AreEqual( unhappy == 1 ? 0x13 : veryHappy == 1 ? 0x12 : 0, member.Thoughts.Last,
			"0x13 unhappy, 0x12 very happy, and a rested member never 0x14" );
		Assert.AreEqual( 1 + draws, random.Asked, "after the idle turn's draw for a sound, the mood's is taken only above 97" );
	}

	/// <summary>A tired member thinks <c>0x14</c> and nothing of their mood (<c>0x00506b50</c>).</summary>
	[TestMethod]
	public void ATiredMemberThinksTiredAndNothingElse()
	{
		var world = Park();
		var (member, walk) = Deciding( world, happiness: 5f, tiredness: 0.5f );
		new StaffBehaviour( Balance(), new ConstantDraw() ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 0x14, member.Thoughts.Last, "tired, though their happiness of 5 would think 0x13" );
	}

}
