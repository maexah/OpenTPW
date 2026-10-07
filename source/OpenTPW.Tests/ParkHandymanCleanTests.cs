using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The handyman's toilet job: the search <c>FUN_004d7880</c>, the walk to the toilet (state <c>0xa</c>,
/// <c>FUN_004d7790</c>) and the clean (state <c>0xb</c>, <c>FUN_004dfd80</c>) -
/// <c>docs/exe/ride-operation.md</c>, "A toilet's dirt".
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkHandymanCleanTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Handyman = 25;

	private const int OtherMember = 26;

	/// <summary>The Small Toilet at (55,17), and the one beside it at (55,16).</summary>
	private const int Toilet = 21;

	private const int NextToilet = 22;

	/// <summary>The path cell the handyman stands on: five off toilet 21's cell squared, ten off toilet 22's.</summary>
	private static readonly (int X, int Y) Stand = (56, 19);

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private RideScript ToiletScript()
	{
		using var stream = data.OpenRead( "levels/jungle/features/Toilet.rse" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	/// <summary>The saved handyman, idle at stamp nought on <see cref="Stand"/>, rested and content.</summary>
	private static (Staff Member, PeepWalk Walk) HandymanOn( ParkWorld world, ParkState state, int grade = 3,
		int thing = Handyman )
	{
		var saved = world.People.Single( person => person.ThingId == Handyman );
		var member = new Staff( thing, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Idle, TimeStartedIdling = 0, PayGrade = grade
		}, saved.Navigator );

		member.Happiness = 50f;
		member.Tiredness = 80f;
		member.Navigator.Position = new FixedVector(
			PeepNavigator.WaypointCentre( Stand.X ), PeepNavigator.WaypointCentre( Stand.Y ) );
		member.Navigator.Target = member.Navigator.Position;

		return (member, new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked ));
	}

	private static ParkWorld.CatalogueObject Thing( ParkState state, int id )
	{
		Assert.IsTrue( state.TryObject( id, out var thing ), $"thing {id} is in the park" );

		return thing;
	}

	private static void Change( ParkState state, int id,
		Func<ParkWorld.CatalogueObject, ParkWorld.CatalogueObject> how )
		=> state.ReplaceObject( how( Thing( state, id ) ) );

	/// <summary>Puts a thing's own cell somewhere, its entry left where it is: the search measures the one and routes to the other.</summary>
	private static ParkWorld.CatalogueObject At( ParkWorld.CatalogueObject thing, int x, int y )
		=> thing with { RawX = x << 8, RawY = y << 8 };

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	/// <summary>
	/// A dirty toilet in range is found at the decide: the handyman takes state <c>0xa</c> with the hurry speed and
	/// the walk's animation, aims at its entry cell, and the toilet is assigned to him on the clock.
	/// </summary>
	[TestMethod]
	public void ADirtyToiletInRangeIsFoundAssignedAndWalkedTo()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );

		Change( state, Toilet, toilet => toilet with { StateOfRepair = 20f } );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( member, walk, playing: null, tick: 1001 );

		var toilet = Thing( state, Toilet );

		Assert.AreEqual( StaffActivity.GoingToLoo, member.Activity );
		Assert.AreEqual( Toilet, member.ToiletToClean );
		Assert.AreEqual( Handyman, toilet.AssignedStaff, "mAssignedStaffMember" );
		Assert.AreEqual( 1001, toilet.TimeMarkedForMaintenance, "stamped with mGameTick" );
		Assert.AreEqual( 25, member.PurposeSpeed, "the hurry speed" );
		Assert.AreEqual( 9, member.NextAnimation );
		Assert.AreEqual( new FixedVector( PeepNavigator.WaypointCentre( toilet.EntryCellX ),
			PeepNavigator.WaypointCentre( toilet.EntryCellY ) ), member.Navigator.Target, "aimed at mEntryPos" );
		Assert.IsTrue( walk.HasRoute );
	}

	/// <summary>
	/// Dirty is the State of repair's byte below 25, and the range is the grade's <c>DetectionRange</c> in cells,
	/// squared and strict, from the toilet's own cell (2, 3, 3, 4, 5: <c>0x007853f0</c>).
	/// </summary>
	[DataTestMethod]
	[DataRow( 24.9f, 3, 55, 17, true, DisplayName = "dirty at 24.9, five off, range 4" )]
	[DataRow( 25f, 3, 55, 17, false, DisplayName = "25 is not dirty" )]
	[DataRow( 20f, 0, 55, 17, false, DisplayName = "grade 0: five is not under 2 squared" )]
	[DataRow( 20f, 1, 55, 17, true, DisplayName = "grade 1: five is under 3 squared" )]
	[DataRow( 20f, 3, 56, 15, false, DisplayName = "sixteen is not under 4 squared" )]
	[DataRow( 20f, 3, 57, 15, false, DisplayName = "seventeen is not" )]
	[DataRow( 20f, 3, 56, 16, true, DisplayName = "nine is" )]
	[DataRow( 20f, 4, 56, 15, true, DisplayName = "grade 4: sixteen is under 5 squared" )]
	public void TheSearchTakesADirtyToiletStrictlyInsideTheGradesRange( float repair, int grade, int x, int y,
		bool found )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state, grade );

		Change( state, Toilet, toilet => At( toilet, x, y ) with { StateOfRepair = repair } );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( found ? Toilet : 0, member.ToiletToClean );
		Assert.AreEqual( found ? StaffActivity.GoingToLoo : StaffActivity.Walking, member.Activity );
		Assert.AreEqual( found ? Handyman : 0, Thing( state, Toilet ).AssignedStaff );
	}

	/// <summary>
	/// The dirty test does not ask the best so far, so of two dirty toilets in range the later in the object chain
	/// wins, nearer or not (<c>0x004d79a9</c>..<c>0x004d79cb</c>).
	/// </summary>
	[TestMethod]
	public void OfTwoDirtyToiletsInRangeTheLaterInTheChainWins()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );

		var order = state.ObjectsInChainOrder().Select( thing => thing.ThingId )
			.Where( id => id is Toilet or NextToilet ).ToArray();

		// The earlier in the chain one cell off, the later three.
		Change( state, order[0], toilet => At( toilet, Stand.X, Stand.Y - 1 ) with { StateOfRepair = 20f } );
		Change( state, order[1], toilet => At( toilet, Stand.X, Stand.Y - 3 ) with { StateOfRepair = 20f } );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( order[1], member.ToiletToClean );
		Assert.AreEqual( 0, Thing( state, order[0] ).AssignedStaff );
	}

	/// <summary>
	/// A toilet that has asked for service is work at any distance, clean or not, and of two the nearer: the
	/// request's test asks the best so far (<c>0x004d79a3</c>).
	/// </summary>
	[TestMethod]
	public void AToiletThatAskedForServiceIsTakenAtAnyDistanceAndTheNearerOfTwo()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );

		var order = state.ObjectsInChainOrder().Select( thing => thing.ThingId )
			.Where( id => id is Toilet or NextToilet ).ToArray();

		// The earlier in the chain the nearer, both far past the range: the later does not replace it.
		Change( state, order[0], toilet => At( toilet, Stand.X + 20, Stand.Y ) with { RequestedService = 1 } );
		Change( state, order[1], toilet => At( toilet, Stand.X + 30, Stand.Y ) with { RequestedService = 1 } );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( order[0], member.ToiletToClean );
		Assert.AreEqual( StaffActivity.GoingToLoo, member.Activity );
	}

	/// <summary>
	/// A toilet assigned to somebody else is passed over; the assignment is forgotten once it is more than a
	/// hundred sweeps old and its member no longer aims at the toilet (<c>FUN_004e0220</c>, <c>FUN_00506580</c>).
	/// </summary>
	[DataTestMethod]
	[DataRow( 901, false, false, DisplayName = "a hundred sweeps old: kept" )]
	[DataRow( 900, false, true, DisplayName = "a hundred and one, the member aiming elsewhere: forgotten" )]
	[DataRow( 900, true, false, DisplayName = "a hundred and one, the member still aiming at it: kept" )]
	public void AnotherMembersToiletIsPassedOverUntilTheAssignmentIsForgotten( int marked, bool stillAims,
		bool taken )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );
		var (other, _) = HandymanOn( world, state, thing: OtherMember );

		other.ToiletToClean = stillAims ? Toilet : 0;

		Change( state, Toilet, toilet => toilet with
		{
			StateOfRepair = 20f, AssignedStaff = OtherMember, TimeMarkedForMaintenance = marked
		} );

		var behaviour = new StaffBehaviour( Balance(), new Random( 1 ), state )
		{
			StaffById = id => id == OtherMember ? other : null
		};

		behaviour.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( taken ? Toilet : 0, member.ToiletToClean );
		Assert.AreEqual( taken ? Handyman : OtherMember, Thing( state, Toilet ).AssignedStaff );
		Assert.AreEqual( taken ? 1001 : marked, Thing( state, Toilet ).TimeMarkedForMaintenance );
	}

	/// <summary>A dirty toilet in range with no route to its entry cell is not taken (<c>FUN_004fa530</c> answering 0).</summary>
	[TestMethod]
	public void ADirtyToiletWithNoRouteToItIsNotTaken()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, _) = HandymanOn( world, state );
		var shut = new PeepWalk( member.Navigator, ( _, _, _ ) => true );

		Change( state, Toilet, toilet => toilet with { StateOfRepair = 20f } );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( member, shut, playing: null, tick: 1001 );

		Assert.AreEqual( 0, member.ToiletToClean );
		Assert.AreEqual( 0, Thing( state, Toilet ).AssignedStaff );
		Assert.AreNotEqual( StaffActivity.GoingToLoo, member.Activity );
	}

	/// <summary>
	/// A walk that ends off the toilet's entry cell starts no clean, assigned or not (<c>FUN_004d7790</c>'s first
	/// test, <c>mEntryPos</c> against his own cell).
	/// </summary>
	[TestMethod]
	public void AWalkThatEndsOffTheEntryCellStartsNoClean()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );

		Change( state, Toilet, toilet => toilet with
		{
			StateOfRepair = 20f, AssignedStaff = Handyman, TimeMarkedForMaintenance = 1000
		} );

		// Aimed where he stands: the walk is over at once, two cells from the toilet.
		member.ToiletToClean = Toilet;
		member.SetActivity( StaffActivity.GoingToLoo, 1000 );

		var behaviour = new StaffBehaviour( Balance(), new Random( 1 ), state );

		for ( var tick = 1001; tick < 1010 && member.Activity == StaffActivity.GoingToLoo; ++tick )
			behaviour.Step( member, walk, playing: null, tick );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.AreEqual( 20f, Thing( state, Toilet ).StateOfRepair );
	}

	/// <summary>A decide that finds no toilet writes nought over the job it held (<c>0x004d7302</c>).</summary>
	[TestMethod]
	public void ADecideThatFindsNoToiletForgetsTheOldJob()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );

		member.ToiletToClean = Toilet;

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 0, member.ToiletToClean );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
	}

	/// <summary>Walks the handyman from his decide to the sweep he starts cleaning on, which is returned.</summary>
	private static int WalkToTheLoo( StaffBehaviour behaviour, Staff member, PeepWalk walk, int from )
	{
		for ( var tick = from; tick < from + 400; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			if ( member.Activity != StaffActivity.GoingToLoo && tick > from )
				return tick;
		}

		Assert.Fail( "the handyman never reached the toilet" );

		return 0;
	}

	/// <summary>
	/// The whole job: the walk costs no rest, he cleans on the toilet's entry cell for WorkDuration + 1 sweeps
	/// (11 at grade 3), each a turn of work, and on the last the toilet is clean, open and nobody's, its script
	/// told <c>VAR_WORN</c> 0 and <c>VAR_RIDECLOSED</c> 0, and he stands idle at stamp nought.
	/// </summary>
	[TestMethod]
	public void HeWalksToItCleansForHisGradesDurationAndLeavesItCleanAndOpen()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );
		var script = ToiletScript();

		Change( state, Toilet, toilet => toilet with
		{
			StateOfRepair = 20f, RequestedService = 1, CanLoad = 0, State = 1
		} );
		script.Set( ParkRideOperation.WornVariable, 1 );
		script.Set( ParkRideOperation.ClosedVariable, 1 );

		var behaviour = new StaffBehaviour( Balance(), new Random( 1 ), state )
		{
			ScriptFor = id => id == Toilet ? script : null
		};

		var started = WalkToTheLoo( behaviour, member, walk, from: 1001 );
		var entry = Thing( state, Toilet );

		Assert.AreEqual( StaffActivity.Cleaning, member.Activity );
		Assert.AreEqual( (entry.EntryCellX, entry.EntryCellY), walk.Position.Cell, "on the toilet's entry cell" );
		Assert.AreEqual( started, member.TimeStartedCleaning );
		Assert.AreEqual( 0x11, member.NextAnimation );
		Assert.AreEqual( 80f, member.Tiredness, "the walk to a toilet costs no rest" );
		Assert.AreEqual( 50f, member.Happiness, "nor mood" );

		for ( var tick = started + 1; tick <= started + 10; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			Assert.AreEqual( StaffActivity.Cleaning, member.Activity, $"still cleaning on sweep {tick - started}" );
			Assert.AreEqual( 20f, Thing( state, Toilet ).StateOfRepair );
		}

		member.NextAnimation = 0;
		behaviour.Step( member, walk, playing: null, started + 11 );

		var clean = Thing( state, Toilet );

		Assert.AreEqual( StaffActivity.Idle, member.Activity, "done on the eleventh sweep after the stamp" );
		Assert.AreEqual( 0, member.TimeStartedIdling, "idle from work is stamped nought" );
		Assert.AreEqual( SpriteScript.Standing, member.NextAnimation );
		Assert.AreEqual( 100f, clean.StateOfRepair );
		Assert.AreEqual( 0, clean.AssignedStaff );
		Assert.AreEqual( 0, clean.RequestedService );
		Assert.AreEqual( 1, clean.CanLoad, "opened" );
		Assert.AreEqual( 0, clean.State, "the open's SetState( 0 )" );
		Assert.AreEqual( 0, script[ParkRideOperation.WornVariable] );
		Assert.AreEqual( 0, script[ParkRideOperation.ClosedVariable] );

		// Eleven turns of work at (6 - 3): 0.025 of rest and 0.01 of mood each.
		Assert.AreEqual( 80f - (11 * 3 * 0.025f), member.Tiredness, 1e-3f );
		Assert.AreEqual( 50f - (11 * 3 * 0.01f), member.Happiness, 1e-3f );
	}

	/// <summary>
	/// Arriving, he cleans only while still the toilet's assigned member (<c>FUN_004d7790</c>); otherwise he is
	/// idle and the toilet stays dirty.
	/// </summary>
	[TestMethod]
	public void ArrivingAtAToiletThatIsNoLongerHisHeDoesNotClean()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = HandymanOn( world, state );
		var behaviour = new StaffBehaviour( Balance(), new Random( 1 ), state );

		Change( state, Toilet, toilet => toilet with { StateOfRepair = 20f } );
		behaviour.Step( member, walk, playing: null, tick: 1001 );
		Assert.AreEqual( StaffActivity.GoingToLoo, member.Activity );

		Change( state, Toilet, toilet => toilet with { AssignedStaff = OtherMember } );
		WalkToTheLoo( behaviour, member, walk, from: 1002 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity );
		Assert.AreEqual( 20f, Thing( state, Toilet ).StateOfRepair );
	}

	/// <summary>A walk to a toilet with no way there ends idle (<c>FUN_004fa2a0</c> answering 2).</summary>
	[TestMethod]
	public void AWalkToAToiletThatCannotBeReachedEndsIdle()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, _) = HandymanOn( world, state );
		var saved = world.People.Single( person => person.ThingId == Handyman );
		var walking = new Staff( Handyman, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.GoingToLoo, ToiletToClean = Toilet
		}, saved.Navigator );
		var entry = Thing( state, Toilet );

		walking.Navigator.Position = member.Navigator.Position;
		walking.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( entry.EntryCellX ),
			PeepNavigator.WaypointCentre( entry.EntryCellY ) );
		var shut = new PeepWalk( walking.Navigator, ( _, _, _ ) => true );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).Step( walking, shut, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Idle, walking.Activity );
	}

	/// <summary>
	/// A handyman whose toilet is sold drops the job and goes idle, in any state; one aiming at another thing is
	/// left alone (<c>docs/exe/park-engine.md</c>, "Selling and the people on it").
	/// </summary>
	[DataTestMethod]
	[DataRow( Toilet, StaffActivity.Idle, 0 )]
	[DataRow( NextToilet, StaffActivity.Cleaning, Toilet )]
	public void AHandymanWhoseToiletIsSoldDropsTheJob( int sold, StaffActivity then, int job )
	{
		var world = World();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Handyman );
		var member = new Staff( Handyman, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Cleaning, ToiletToClean = Toilet
		}, saved.Navigator );

		new StaffBehaviour( Balance(), new Random( 1 ), state ).ThingRemoved( member, walk: null, sold, tick: 1001 );

		Assert.AreEqual( then, member.Activity );
		Assert.AreEqual( job, member.ToiletToClean );
	}

	/// <summary>
	/// The clean itself (<c>FUN_004dfd80</c>) counts the region effects it does not stamp only for a toilet found
	/// dirty, and clears the request and the assignment of any thing.
	/// </summary>
	[DataTestMethod]
	[DataRow( 20f, 1 )]
	[DataRow( 60f, 0 )]
	public void TheCleanCountsTheRegionEffectsOnlyForADirtyToilet( float repair, int counted )
	{
		var state = new ParkState( World() );

		Change( state, Toilet, toilet => toilet with
		{
			StateOfRepair = repair, AssignedStaff = Handyman, RequestedService = 1
		} );

		var before = Counted( "TOILET_DIRTY_REGION_EFFECTS" );

		ParkRideOperation.Clean( state, script: null, Toilet );

		Assert.AreEqual( counted, Counted( "TOILET_DIRTY_REGION_EFFECTS" ) - before );
		Assert.AreEqual( 100f, Thing( state, Toilet ).StateOfRepair );
		Assert.AreEqual( 0, Thing( state, Toilet ).AssignedStaff );
		Assert.AreEqual( 0, Thing( state, Toilet ).RequestedService );
	}

	/// <summary>
	/// The save's <c>mToiletToClean</c> (handyman record 509), <c>mTimeStartedCleaning</c> (505) and an object's
	/// <c>mTimeMarkedForMaintenance</c> (1082) are read (FileFormats <c>saves.md</c>). The shipped park holds
	/// nought in all three, which a reader at the wrong offset could read too, so a copy of its payload is given
	/// values first.
	/// </summary>
	[TestMethod]
	public void TheSavesToiletJobAndMaintenanceStampAreRead()
	{
		var world = World();
		var person = world.People.Single( person => person.ThingId == Handyman );
		var saved = person.Staff!.Value;
		var toilet = world.Objects.Single( thing => thing.ThingId == Toilet );

		Assert.AreEqual( 0, saved.ToiletToClean );
		Assert.AreEqual( 0, saved.TimeStartedCleaning );
		Assert.IsTrue( world.Objects.All( thing => thing.TimeMarkedForMaintenance == 0 ) );

		var bytes = new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile();

		// The handyman's record, found by where he stands at 140 and 144 and his rest at 499; the toilet's
		// by its footprint corner at 204, exit at 218 and State of repair at 1074.
		var member = Enumerable.Range( 0, bytes.Length - 513 ).Single( at =>
			BitConverter.ToInt32( bytes, at + 140 ) == person.Navigator.X && BitConverter.ToInt32( bytes, at + 144 ) == person.Navigator.Y
			&& BitConverter.ToSingle( bytes, at + 499 ) == saved.Tiredness && BitConverter.ToInt32( bytes, at + 483 ) == saved.State );
		var thing = Enumerable.Range( 0, bytes.Length - 1099 ).Single( at =>
			BitConverter.ToUInt16( bytes, at + 204 ) == toilet.TopLeft && BitConverter.ToUInt16( bytes, at + 218 ) == toilet.ExitPos
			&& BitConverter.ToSingle( bytes, at + 1074 ) == toilet.StateOfRepair && BitConverter.ToInt32( bytes, at + 214 ) == toilet.CanLoad
			&& BitConverter.ToInt32( bytes, at + 192 ) == toilet.RideScript );

		BitConverter.GetBytes( (ushort)Toilet ).CopyTo( bytes, member + 509 );
		BitConverter.GetBytes( 0x01020304 ).CopyTo( bytes, member + 505 );
		BitConverter.GetBytes( 0x0a0b0c0d ).CopyTo( bytes, thing + 1082 );

		var patched = new ParkWorld( bytes );
		var staff = ParkPeople.StaffIn( patched ).Single( member25 => member25.ThingId == Handyman );

		Assert.AreEqual( Toilet, staff.ToiletToClean, "mToiletToClean at 509, handed to the member" );
		Assert.AreEqual( 0x01020304, staff.TimeStartedCleaning, "mTimeStartedCleaning at 505" );
		Assert.AreEqual( 0x0a0b0c0d, patched.Objects.Single( placed => placed.ThingId == Toilet ).TimeMarkedForMaintenance,
			"mTimeMarkedForMaintenance at 1082" );
		Assert.IsTrue( patched.People.Where( other => other.ThingId != Handyman && other.Staff is { } )
			.All( other => other.Staff!.Value.ToiletToClean == 0 ), "only a handyman's record has the field" );
	}

	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>Runs the park one more thing sweep.</summary>
	private static void RunASweep( ParkPeople people )
	{
		var until = GameClock.Ticks / ParkPeople.ThingTickEvery + 1;

		for ( var frame = 0; frame < 600 && GameClock.Ticks / ParkPeople.ThingTickEvery < until; ++frame )
		{
			Frame( 1f / 60f );
			people.Update();
		}

		Assert.IsTrue( GameClock.Ticks / ParkPeople.ThingTickEvery >= until, "the sweep ran" );
	}

	/// <summary>
	/// <b>The park's own sweep runs the job</b>: the handyman put beside a dirty toilet finds it, cleans it and
	/// leaves its script told, with the park's staff answering for an assignment more than a hundred sweeps old.
	/// </summary>
	[TestMethod]
	public void TheParksSweepRunsTheJobAndTellsTheToiletsScript()
	{
		var world = World();
		var state = new ParkState( world );
		var script = ToiletScript();

		var people = new ParkPeople( world, Balance(), gateStatus: null, state,
			new ParkItemCatalogue( "jungle", data ), scriptFor: id => id == Toilet ? script : null );

		try
		{
			var member = people.Staff.Single( staff => staff.ThingId == Handyman );
			var entry = Thing( state, Toilet );

			// On the entry cell and on his way, the assignment stale by the clock: only his own aim keeps it.
			member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( entry.EntryCellX ),
				PeepNavigator.WaypointCentre( entry.EntryCellY ) );
			member.Navigator.Target = member.Navigator.Position;
			member.ToiletToClean = Toilet;
			member.SetActivity( StaffActivity.GoingToLoo, state.GameTick );

			var stale = state.GameTick - 200;

			Change( state, Toilet, toilet => toilet with
			{
				StateOfRepair = 20f, AssignedStaff = Handyman, TimeMarkedForMaintenance = stale
			} );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( 0f );

			for ( var sweep = 0; sweep < 60 && member.Activity != StaffActivity.Cleaning; ++sweep )
				RunASweep( people );

			Assert.AreEqual( StaffActivity.Cleaning, member.Activity, "the park's staff answered for the old assignment" );
			Assert.AreEqual( stale, Thing( state, Toilet ).TimeMarkedForMaintenance,
				"and it is the old assignment he cleans under, not one found afresh" );
			Assert.AreEqual( 1, script[ParkRideOperation.WornVariable], "the dirty toilet's turn told its script" );

			for ( var sweep = 0; sweep < 12 && member.Activity == StaffActivity.Cleaning; ++sweep )
				RunASweep( people );

			Assert.AreNotEqual( StaffActivity.Cleaning, member.Activity );
			Assert.AreEqual( 100f, Thing( state, Toilet ).StateOfRepair );
			Assert.AreEqual( 0, script[ParkRideOperation.WornVariable], "the clean told the toilet's script" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
