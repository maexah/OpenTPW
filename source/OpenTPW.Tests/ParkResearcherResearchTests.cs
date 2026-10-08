using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The researcher's research - <c>FUN_005029f0</c>'s choice and state <c>0xf</c>, and the pre-step's points for
/// the lab (<c>docs/exe/ride-operation.md</c>, "The research, in both games"). These read real game files and
/// are skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkResearcherResearchTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Researcher = 30;

	private const int Guard = 28;

	/// <summary>The saved researcher's grade, 2: 30 sweeps of work, and twelve points for the lab at a time.</summary>
	private const int Grade = 2;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	/// <summary>
	/// A saved member on the path at (48,22) with a walk that has ended. With <paramref name="nowhere"/> they
	/// have no patrol area, which a behaviour given no park finds nowhere to walk from: no cells to step
	/// through, and no area for the patrol roll.
	/// </summary>
	private (Staff Member, PeepWalk Walk, ParkState State) OnThePath(
		StaffActivity activity = StaffActivity.Walking, int id = Researcher, bool nowhere = false )
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == id );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with
		{
			State = (int)activity, TimeStartedIdling = 5, TimeStartedResearching = 0,
			PatrolBottomLeft = nowhere ? 0 : MapStep.CellId( 0, 0 ),
			PatrolTopRight = nowhere ? 0 : MapStep.CellId( 127, 127 )
		}, saved.Navigator );

		member.Happiness = 50f;
		member.Tiredness = 80f;
		member.NextAnimation = 0;
		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 48 ), PeepNavigator.WaypointCentre( 22 ) );
		member.Navigator.Target = member.Navigator.Position;

		return (member, new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked ), state);
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

	[TestMethod]
	public void TheSavedResearcherIsGradeTwoAndTheBalanceGivesItThirtySweepsAndTwelvePoints()
	{
		var (member, _, _) = OnThePath();
		var behaviour = new StaffBehaviour( Balance() );

		Assert.AreEqual( 8, member.Model );
		Assert.AreEqual( Grade, member.PayGrade );
		CollectionAssert.AreEqual( new[] { 10, 20, 30, 40, 50 },
			Enumerable.Range( 0, 5 ).Select( behaviour.ResearcherWorkDurationAt ).ToArray() );
		CollectionAssert.AreEqual( new[] { 6, 9, 12, 16, 20 },
			Enumerable.Range( 0, 5 ).Select( behaviour.ResearchAbilityAt ).ToArray(), "jungle's Easy_Standard.sam" );
		CollectionAssert.AreEqual( new[] { 2, 3, 4, 5, 6 },
			Enumerable.Range( 0, 5 ).Select( new StaffBehaviour().ResearchAbilityAt ).ToArray(), "Standard.sam's" );
	}

	/// <summary>
	/// The shipped park's researcher was saved with <c>mTimeStartedResearching</c> 697, which the original holds
	/// in <c>+0x214</c> on its first sweep.
	/// </summary>
	[TestMethod]
	public void TheSavedStampIsReadAndKept()
	{
		var world = Park();
		var saved = world.People.Single( person => person.ThingId == Researcher );

		Assert.AreEqual( 697, saved.Staff!.Value.TimeStartedResearching );
		Assert.AreEqual( 697, new Staff( saved.ThingId, saved.Model, saved.Staff.Value, saved.Navigator ).TimeStartedResearching );

		// +503 is another field for every other kind: the mechanic's count down, the handyman's litter cell, the
		// entertainer's own stamp, the guard's perp.
		foreach ( var other in world.People.Where( person => person.Staff != null && person.ThingId != Researcher ) )
			Assert.AreEqual( 0, other.Staff!.Value.TimeStartedResearching, $"thing {other.ThingId}, model {other.Model}" );
	}

	/// <summary>
	/// A draw whose low two bits are nought researches where the researcher stands, from a walk's end and from an
	/// idle alike: the stamp, animation 10 and the state, with no setter, so the idle stamp is left alone. The
	/// clock's own bits are not asked.
	/// </summary>
	[DataTestMethod]
	[DataRow( StaffActivity.Walking, 0, 1001 )]
	[DataRow( StaffActivity.Walking, 4, 1002 )]
	[DataRow( StaffActivity.Idle, 8, 1003 )]
	public void ADrawOfNoughtResearchesWhereTheResearcherStands( StaffActivity from, int draw, int tick )
	{
		var (member, walk, state) = OnThePath( from );
		var random = new CountedDraw( draw );

		new StaffBehaviour( Balance(), random, state ).Step( member, walk, playing: null, tick );

		Assert.AreEqual( StaffActivity.Researching, member.Activity );
		Assert.AreEqual( 0xf, (int)member.Activity );
		Assert.AreEqual( tick, member.TimeStartedResearching );
		Assert.AreEqual( 10, member.NextAnimation );
		Assert.AreEqual( 5, member.TimeStartedIdling, "no setter runs" );
		Assert.AreEqual( 1, random.Asked, "the choice's one draw" );
		Assert.AreEqual( (48, 22), member.Navigator.Position.Cell );
		Assert.AreEqual( 80f, member.Tiredness, "the start costs nothing" );
		Assert.IsFalse( global::OpenTPW.Staff.IsAWalkingState( member.Activity ) );
	}

	/// <summary>Any other draw walks when somewhere is found, on a multiple of four of the clock too.</summary>
	[DataTestMethod]
	[DataRow( 1 )]
	[DataRow( 2 )]
	[DataRow( 3 )]
	public void AnyOtherDrawWalks( int draw )
	{
		var (member, walk, state) = OnThePath();

		new StaffBehaviour( Balance(), new CountedDraw( draw ), state ).Step( member, walk, playing: null, tick: 1000 );

		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		Assert.AreEqual( 0, member.TimeStartedResearching );
		Assert.AreNotEqual( 10, member.NextAnimation );
	}

	/// <summary>A draw that would walk, with nowhere found, researches too: the researcher is never stood idle.</summary>
	[DataTestMethod]
	[DataRow( StaffActivity.Walking )]
	[DataRow( StaffActivity.Idle )]
	public void WithNowhereToWalkTheResearcherResearches( StaffActivity from )
	{
		var (member, walk, _) = OnThePath( from, nowhere: true );

		new StaffBehaviour( Balance(), new CountedDraw( 1 ) ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Researching, member.Activity );
		Assert.AreEqual( 1001, member.TimeStartedResearching );
		Assert.AreEqual( 10, member.NextAnimation );
	}

	/// <summary>The guard's choice is still the clock's: nought stands, idle.</summary>
	[TestMethod]
	public void TheGuardStillStandsOnTheClocksMultipleOfFour()
	{
		var (member, walk, state) = OnThePath( id: Guard );

		new StaffBehaviour( Balance(), new CountedDraw( 1 ), state ).Step( member, walk, playing: null, tick: 1000 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.AreEqual( 0, member.TimeStartedResearching );
	}

	/// <summary>
	/// A spell is the stamp's sweep and WorkDuration more: a turn of work on each of the 31 sweeps after the
	/// stamp, (6 - grade) x 0.025 of rest and x 0.01 of mood, and on the 31st the walk, with the walk's
	/// animation queued and the stamp left as it was.
	/// </summary>
	[TestMethod]
	public void ASpellIsThirtyOneTurnsOfWorkThenAWalk()
	{
		var (member, walk, state) = OnThePath();
		var random = new CountedDraw( 0 );
		var behaviour = new StaffBehaviour( Balance(), random, state );

		behaviour.Step( member, walk, playing: null, tick: 1001 );
		member.NextAnimation = 0;

		for ( var tick = 1002; tick <= 1031; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			Assert.AreEqual( StaffActivity.Researching, member.Activity, $"mGameTick {tick}" );
			Assert.AreEqual( 80f - ((tick - 1001) * 0.1f), member.Tiredness, 0.001f );
			Assert.AreEqual( 1001, member.TimeStartedResearching );
		}

		Assert.AreEqual( 0, member.NextAnimation, "the animation is queued once" );
		Assert.AreEqual( 1, random.Asked, "a turn of research draws nothing here" );

		behaviour.Step( member, walk, playing: null, tick: 1032 );

		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		Assert.AreEqual( 9, member.NextAnimation );
		Assert.AreEqual( 80f - 3.1f, member.Tiredness, 0.001f );
		Assert.AreEqual( 50f - 1.24f, member.Happiness, 0.001f );
		Assert.AreEqual( 1001, member.TimeStartedResearching );
		Assert.IsTrue( walk.HasRoute );
	}

	/// <summary>
	/// The spell's length is the grade's own, and its end is a walk whatever the draw would say and however
	/// tired the researcher is: neither the choice's draw nor the shared tired test comes before it.
	/// </summary>
	[DataTestMethod]
	[DataRow( 0, 10 )]
	[DataRow( 4, 50 )]
	public void TheEndWalksWithNoChoiceAndNoTiredTest( int grade, int work )
	{
		var (member, walk, state) = OnThePath();
		var behaviour = new StaffBehaviour( Balance(), new CountedDraw( 0 ), state );

		member.PayGrade = grade;
		behaviour.Step( member, walk, playing: null, tick: 1001 );

		for ( var tick = 1002; tick <= 1001 + work; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( StaffActivity.Researching, member.Activity );

		member.Tiredness = 0.5f;
		behaviour.Step( member, walk, playing: null, tick: 1002 + work );

		Assert.AreEqual( StaffActivity.Walking, member.Activity );
	}

	/// <summary>
	/// A spell that ends with nowhere to walk is stamped again and goes on, with no animation queued and no
	/// state set: 31 sweeps more.
	/// </summary>
	[TestMethod]
	public void ASpellEndingWithNowhereToWalkIsStampedAgain()
	{
		var (member, walk, _) = OnThePath( nowhere: true );
		var behaviour = new StaffBehaviour( Balance(), new CountedDraw( 0 ) );

		for ( var tick = 1001; tick <= 1031; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( 1001, member.TimeStartedResearching );

		member.NextAnimation = 0;
		behaviour.Step( member, walk, playing: null, tick: 1032 );

		Assert.AreEqual( StaffActivity.Researching, member.Activity );
		Assert.AreEqual( 1032, member.TimeStartedResearching );
		Assert.AreEqual( 0, member.NextAnimation );
		Assert.AreEqual( 5, member.TimeStartedIdling );
		Assert.AreEqual( 80f - 3.1f, member.Tiredness, 0.001f, "the ending turn works too" );

		for ( var tick = 1033; tick <= 1062; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( 1032, member.TimeStartedResearching );

		behaviour.Step( member, walk, playing: null, tick: 1063 );

		Assert.AreEqual( 1063, member.TimeStartedResearching );
	}

	/// <summary>
	/// The pre-step counts the grade's points for the lab on every sweep the clock divides by twenty, in any
	/// state but the two of rest, the two of the strike and carried, and for no other kind.
	/// </summary>
	[DataTestMethod]
	[DataRow( StaffActivity.Idle, 1000, Researcher, 12 )]
	[DataRow( StaffActivity.Walking, 1020, Researcher, 12 )]
	[DataRow( StaffActivity.Waiting, 1000, Researcher, 12 )]
	[DataRow( StaffActivity.Researching, 1000, Researcher, 12 )]
	[DataRow( StaffActivity.Researching, 1001, Researcher, 0 )]
	[DataRow( StaffActivity.Researching, 1010, Researcher, 0 )]
	[DataRow( StaffActivity.GoingToRest, 1000, Researcher, 0 )]
	[DataRow( StaffActivity.Resting, 1000, Researcher, 0 )]
	[DataRow( StaffActivity.GoingOnStrike, 1000, Researcher, 0 )]
	[DataRow( StaffActivity.OnStrike, 1000, Researcher, 0 )]
	[DataRow( StaffActivity.Held, 1000, Researcher, 0 )]
	[DataRow( StaffActivity.Idle, 1000, Guard, 0 )]
	public void ThePreStepCountsThePointsForTheLab( StaffActivity activity, int tick, int id, int points )
	{
		var (member, walk, state) = OnThePath( activity, id );
		var behaviour = new StaffBehaviour( Balance(), new CountedDraw( 0 ), state );
		var before = Counted( "RESEARCH_POINTS_TO_THE_LAB" );

		member.TimeStartedIdling = tick;
		behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( points, behaviour.ResearchPointsCounted );
		Assert.AreEqual( points == 0 ? 0 : 1, Counted( "RESEARCH_POINTS_TO_THE_LAB" ) - before );
	}

	/// <summary>The points are the grade's own and add up over a run.</summary>
	[TestMethod]
	public void ThePointsAreTheGradesAndAddUp()
	{
		var (member, walk, state) = OnThePath( StaffActivity.Held );
		var behaviour = new StaffBehaviour( Balance(), new CountedDraw( 0 ), state );

		member.SetActivity( StaffActivity.Waiting, 1000 );
		member.PayGrade = 4;

		for ( var tick = 1000; tick <= 1040; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( 60, behaviour.ResearchPointsCounted, "20 on each of 1000, 1020 and 1040" );
	}

	/// <summary>
	/// In the park a researching researcher's sprite is put on animation 10's script, word 402, set 4, and stays
	/// where it stands.
	/// </summary>
	[TestMethod]
	public void InTheParkAResearchingResearcherIsOnScriptFourHundredAndTwo()
	{
		var world = Park();
		using var clock = new SimulationClockScope();
		var previousState = ParkState.Current;
		var previousPeople = ParkPeople.Current;
		ParkPeople? people = null;

		try
		{
			var state = new ParkState( world );

			people = new ParkPeople( world, Balance(), state: state, random: new Random( 1 ),
				behaviourRandom: new Random( 2 ), rideRandom: new Random( 3 ), staffRandom: new Random( 4 ) );

			var member = people.Staff.Single( one => one.ThingId == Researcher );
			var sprite = people.SpriteFor( Researcher )!;
			var place = member.Navigator.Position;

			Assert.AreEqual( 402, SpriteScript.EntryFor( global::OpenTPW.Staff.ResearchingAnimation ) );
			Assert.IsFalse( sprite.IsOn( 10 ) );

			member.StartResearching( state.GameTick + 1 );

			SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
			people.Update();

			Assert.AreEqual( StaffActivity.Researching, member.Activity );
			Assert.IsTrue( sprite.IsOn( 10 ) );
			Assert.AreEqual( 402, sprite.Script );
			Assert.AreEqual( place, member.Navigator.Position );

			for ( var sweep = 0; sweep < 6; ++sweep )
			{
				SimulationClockScope.Frame( 8 * GameClock.TickSeconds );
				people.Update();
			}

			Assert.AreEqual( 4, sprite.Set );
			Assert.AreEqual( place, member.Navigator.Position );
		}
		finally
		{
			people?.Delete();
			Entity.ApplyDeletions();
			typeof( ParkPeople ).GetProperty( "Current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!.SetValue( null, previousPeople );
			typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!.SetValue( null, previousState );
		}
	}
}
