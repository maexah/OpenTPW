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
/// <b>The assertion that matters is about position, not state.</b> Alexah's report was that the staff do
/// not navigate, and a staff member who reached a state and was handed a destination but never moved would
/// satisfy any state check. So the payoff tests ask whether they <b>end up somewhere else on the map</b>.
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

	private (StaffBehaviour Behaviour, Dictionary<int, Staff> Staff, Dictionary<int, PeepWalk> Walks) Run(
		ParkWorld world, int turns, int seed = 1234 )
	{
		var behaviour = new StaffBehaviour( Balance(), new Random( seed ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var staff = ParkPeople.StaffIn( world ).ToDictionary( member => member.ThingId );
		var walks = staff.Values.ToDictionary( member => member.ThingId,
			member => new PeepWalk( member.Navigator, blocked ) );

		for ( var tick = 1; tick <= turns; ++tick )
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

		for ( var tick = 1; tick <= 400; ++tick )
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

		behaviour.Step( member, walk, playing: null, tick: 1 );

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
	/// <b>A stamp left by the original's clock does not strand them.</b>
	///
	/// <para>
	/// Every saved idle stamp is a reading of the park's <c>mGameTick</c>, which Lost Kingdom left at 755,
	/// and a freshly loaded park counts from nought. So without the rule that a stamp ahead of the clock is
	/// stale, "have I idled long enough" is false for the first several hundred turns of every session and
	/// the staff stand perfectly still while every state in the machine is correct. That is the failure
	/// this pins, and it is worth a test of its own because a state check cannot see it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AStampFromTheOldParksClockDoesNotStrandThem()
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 3 ) );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == Guard );
		var walk = new PeepWalk( member.Navigator, blocked );

		Assert.IsTrue( member.TimeStartedIdling > 0,
			"the guard should be saved carrying a stamp from the original's clock" );

		behaviour.Step( member, walk, playing: null, tick: 1 );

		Assert.AreEqual( 0, member.TimeStartedIdling,
			"a stamp taken after the current tick is stale and should have been cleared" );
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
