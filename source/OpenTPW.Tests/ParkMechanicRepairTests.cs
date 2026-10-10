using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The mechanic's repair job: the search <c>FUN_004daa90</c> and its cursor, the decide <c>FUN_004da5b0</c>, the
/// walk to the ride (state <c>0xc</c>, <c>FUN_004da740</c>), the repair (state <c>0xd</c>, <c>FUN_004da830</c>)
/// and the ride's side of it (<c>FUN_004df8f0</c>) - <c>docs/exe/ride-operation.md</c>, "The mechanic's repair".
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkMechanicRepairTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Mechanic = 26;

	private const int OtherMember = 25;

	/// <summary>The Jungle Spray, and the Small Toilet at (55,17).</summary>
	private const int Ride = 14;

	private const int Toilet = 21;

	/// <summary>The path cell the mechanic stands on.</summary>
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

	/// <summary>The saved mechanic, idle at stamp nought on <see cref="Stand"/>, rested and content.</summary>
	private static (Staff Member, PeepWalk Walk) MechanicOn( ParkWorld world, ParkState state, int grade = 3,
		int thing = Mechanic )
	{
		var saved = world.People.Single( person => person.ThingId == Mechanic );
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

	private static StaffBehaviour Behaviour( ParkState state, Func<int, RideScript?>? scripts = null, Staff? other = null )
		=> new( Balance(), new Random( 1 ), state )
		{
			ScriptFor = scripts, StaffById = id => other != null && id == other.ThingId ? other : null
		};

	/// <summary>Makes searches until the mechanics' cursor names a thing, and fails where it never does.</summary>
	private static void CursorTo( ParkState state, int thing )
	{
		for ( var search = 0; search < 100 && state.MechanicCursor != thing; ++search )
			state.ObjectsFromTheMechanicsCursor();

		Assert.AreEqual( thing, state.MechanicCursor, "the cursor reaches every object in turn" );
	}

	/// <summary>Puts the mechanic on a thing's entry cell, on his way to it and its assigned member.</summary>
	private static void OnHisWayAtTheEntry( ParkState state, Staff member, int ride, int marked = 1000 )
	{
		var entry = Thing( state, ride );

		member.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( entry.EntryCellX ),
			PeepNavigator.WaypointCentre( entry.EntryCellY ) );
		member.Navigator.Target = member.Navigator.Position;
		member.ObjectToRepair = ride;
		member.SetActivity( StaffActivity.GoingToRide, marked );

		Change( state, ride, thing => thing with { AssignedStaff = (ushort)member.ThingId, TimeMarkedForMaintenance = marked } );
	}

	/// <summary>Steps until the mechanic leaves an activity, and answers the sweep he left it on.</summary>
	private static int Until( StaffBehaviour behaviour, Staff member, PeepWalk walk, StaffActivity leaves, int from )
	{
		for ( var tick = from; tick < from + 400; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			if ( member.Activity != leaves )
				return tick;
		}

		Assert.Fail( $"still {leaves} after 400 sweeps" );

		return 0;
	}

	/// <summary>
	/// A ride broken down is found at the decide: the mechanic takes state <c>0xc</c> with the walk's animation and
	/// no hurry, aims at its entry cell, the ride is assigned to him on the clock, and the advisor's message for
	/// a ride nobody called him to is counted.
	/// </summary>
	[TestMethod]
	public void ABrokenRideIsFoundAssignedAndWalkedTo()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var message = Counted( "MECHANIC_ON_HIS_WAY_MESSAGE" );

		Change( state, Ride, ride => ride with { State = 1 } );

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		var ride = Thing( state, Ride );

		Assert.AreEqual( StaffActivity.GoingToRide, member.Activity );
		Assert.AreEqual( Ride, member.ObjectToRepair );
		Assert.AreEqual( Mechanic, ride.AssignedStaff, "mAssignedStaffMember" );
		Assert.AreEqual( 1001, ride.TimeMarkedForMaintenance, "stamped with mGameTick" );
		Assert.AreEqual( 0, member.PurposeSpeed, "no hurry: his own setter writes the animation and the state alone" );
		Assert.AreEqual( 9, member.NextAnimation );
		Assert.AreEqual( new FixedVector( PeepNavigator.WaypointCentre( ride.EntryCellX ),
			PeepNavigator.WaypointCentre( ride.EntryCellY ) ), member.Navigator.Target, "aimed at mEntryPos" );
		Assert.IsTrue( walk.HasRoute );
		Assert.AreEqual( 1, Counted( "MECHANIC_ON_HIS_WAY_MESSAGE" ) - message );
		Assert.AreEqual( 1, ride.State, "the search changes nothing of the ride but the assignment" );
	}

	/// <summary>
	/// What the search takes (<c>0x004dac06</c>..<c>0x004dac29</c>): a thing broken down, toilet or not, or one a
	/// mechanic was called to that is no toilet. Not one condemned, not one operating; one waiting for an upgrade
	/// is the original's too and is counted here. A called ride posts no message.
	/// </summary>
	[DataTestMethod]
	[DataRow( Ride, 1, 0, true, 1, 0, DisplayName = "a ride broken down" )]
	[DataRow( Toilet, 1, 0, true, 1, 0, DisplayName = "a toilet broken down" )]
	[DataRow( Ride, 0, 1, true, 0, 0, DisplayName = "a ride he was called to: no message" )]
	[DataRow( Ride, 1, 1, true, 0, 0, DisplayName = "broken and called: no message" )]
	[DataRow( Toilet, 0, 1, false, 0, 0, DisplayName = "a toilet's call is the handyman's" )]
	[DataRow( Ride, 0, 0, false, 0, 0, DisplayName = "operating" )]
	[DataRow( Ride, 4, 0, false, 0, 0, DisplayName = "condemned" )]
	[DataRow( Ride, 3, 0, false, 0, 0, DisplayName = "never offered" )]
	[DataRow( Ride, 2, 0, false, 0, 1, DisplayName = "waiting for an upgrade: counted" )]
	public void TheSearchTakesTheBrokenAndTheCalled( int thing, int thingState, int requested, bool found,
		int messages, int upgrades )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var before = (Counted( "MECHANIC_ON_HIS_WAY_MESSAGE" ), Counted( "MECHANIC_UPGRADE_JOB" ));

		Change( state, thing, it => it with { State = thingState, RequestedService = requested } );

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( found ? thing : 0, member.ObjectToRepair );
		Assert.AreEqual( found ? StaffActivity.GoingToRide : StaffActivity.Walking, member.Activity );
		Assert.AreEqual( found ? Mechanic : 0, Thing( state, thing ).AssignedStaff );
		Assert.AreEqual( messages, Counted( "MECHANIC_ON_HIS_WAY_MESSAGE" ) - before.Item1 );
		Assert.AreEqual( upgrades, Counted( "MECHANIC_UPGRADE_JOB" ) - before.Item2 );
	}

	/// <summary>
	/// The nearest wins, cell to cell from the mechanic's own, at any distance and strictly: of two as near as
	/// each other the first the walk meets is kept (<c>0x004dacd2</c>, <c>JNC</c>).
	/// </summary>
	[DataTestMethod]
	[DataRow( 30, 40, true, DisplayName = "the nearer of two, both far off" )]
	[DataRow( 40, 30, false, DisplayName = "the other way round" )]
	public void TheNearestBrokenThingWins( int rideOff, int toiletOff, bool rideWins )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );

		Change( state, Ride, ride => At( ride, Stand.X - rideOff, Stand.Y ) with { State = 1 } );
		Change( state, Toilet, toilet => At( toilet, Stand.X, Stand.Y + toiletOff ) with { State = 1 } );

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( rideWins ? Ride : Toilet, member.ObjectToRepair );
		Assert.AreEqual( 0, Thing( state, rideWins ? Toilet : Ride ).AssignedStaff );
	}

	/// <summary>Of two broken things as near as each other, the one the cursor's walk meets first wins.</summary>
	[TestMethod]
	public void OfTwoAsNearTheFirstTheWalkMeetsWins()
	{
		var world = World();
		var chain = new ParkState( world ).ObjectsInChainOrder().Select( thing => thing.ThingId ).ToList();
		var first = Math.Min( chain.IndexOf( Ride ), chain.IndexOf( Toilet ) );
		var second = Math.Max( chain.IndexOf( Ride ), chain.IndexOf( Toilet ) );

		Assert.IsTrue( first > 0 && second > first + 1, "the two stand apart in the chain, neither its head" );

		// A cursor on the thing before one of them begins the walk on that one.
		foreach ( var begins in new[] { first, second } )
		{
			var state = new ParkState( world );
			var (member, walk) = MechanicOn( world, state );

			Change( state, Ride, ride => At( ride, Stand.X - 7, Stand.Y ) with { State = 1 } );
			Change( state, Toilet, toilet => At( toilet, Stand.X, Stand.Y + 7 ) with { State = 1 } );

			CursorTo( state, chain[begins - 1] );

			Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

			Assert.AreEqual( chain[begins], member.ObjectToRepair );
			Assert.AreEqual( chain[begins], state.MechanicCursor, "the cursor is left on the thing the walk began on" );
		}
	}

	/// <summary>
	/// The cursor (<c>0x004daa9d</c>..<c>0x004dab53</c>, the mechanics' HQ's <c>mNextObject</c>): the file's is
	/// read; each search begins on the object after the one it names and goes right round; the search that steps
	/// off the chain's end looks at nothing and leaves nought; nought, or a thing no longer in the chain, begins
	/// at the head.
	/// </summary>
	[TestMethod]
	public void EachSearchBeginsOneObjectOnAndTheOneOffTheEndLooksAtNothing()
	{
		var world = World();
		var state = new ParkState( world );
		var chain = state.ObjectsInChainOrder().Select( thing => thing.ThingId ).ToList();

		Assert.AreEqual( 18, world.MechanicCursor, "the shipped park's mNextObject" );
		Assert.AreEqual( 18, state.MechanicCursor );
		Assert.IsTrue( chain.Count > 3 );

		var at = chain.IndexOf( 18 );

		for ( var search = 1; search <= chain.Count * 2 + 3; ++search )
		{
			var walked = state.ObjectsFromTheMechanicsCursor().Select( thing => thing.ThingId ).ToList();

			at = at == chain.Count ? 0 : at + 1;

			if ( at == chain.Count )
			{
				Assert.AreEqual( 0, walked.Count, $"search {search} stepped off the end" );
				Assert.AreEqual( 0, state.MechanicCursor );

				continue;
			}

			CollectionAssert.AreEqual( chain.Skip( at ).Concat( chain.Take( at ) ).ToList(), walked, $"search {search}" );
			Assert.AreEqual( chain[at], state.MechanicCursor );
		}

		// A cursor naming a thing sold begins at the head.
		CursorTo( state, chain[2] );

		state.RemoveObject( chain[2] );

		var left = state.ObjectsInChainOrder().Select( thing => thing.ThingId ).ToList();

		CollectionAssert.AreEqual( left, state.ObjectsFromTheMechanicsCursor().Select( thing => thing.ThingId ).ToList() );
		Assert.AreEqual( left[0], state.MechanicCursor );
	}

	/// <summary>A search that looks at nothing finds nothing, broken ride or not, and the next finds it.</summary>
	[TestMethod]
	public void TheSearchThatStepsOffTheEndFindsNoRide()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var last = state.ObjectsInChainOrder().Last().ThingId;

		Change( state, Ride, ride => ride with { State = 1 } );

		CursorTo( state, last );

		var behaviour = Behaviour( state );

		behaviour.Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 0, member.ObjectToRepair );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
		Assert.AreEqual( 0, state.MechanicCursor );

		var (next, nextWalk) = MechanicOn( world, state );

		behaviour.Step( next, nextWalk, playing: null, tick: 1002 );

		Assert.AreEqual( Ride, next.ObjectToRepair );
	}

	/// <summary>
	/// A ride assigned to somebody else is passed over; the assignment is forgotten once it is more than a
	/// hundred sweeps old and its member no longer aims at the ride, and a mechanic's aim is his
	/// <c>mObjectToRepair</c> (<c>FUN_004e0220</c>, <c>FUN_00506580</c>).
	/// </summary>
	[DataTestMethod]
	[DataRow( 901, false, false, DisplayName = "a hundred sweeps old: kept" )]
	[DataRow( 900, false, true, DisplayName = "a hundred and one, the member aiming elsewhere: forgotten" )]
	[DataRow( 900, true, false, DisplayName = "a hundred and one, the member still aiming at it: kept" )]
	public void AnotherMechanicsRideIsPassedOverUntilTheAssignmentIsForgotten( int marked, bool stillAims, bool taken )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var (other, _) = MechanicOn( world, state, thing: OtherMember );

		other.ObjectToRepair = stillAims ? Ride : 0;

		Change( state, Ride, ride => ride with { State = 1, AssignedStaff = OtherMember, TimeMarkedForMaintenance = marked } );

		Behaviour( state, other: other ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( taken ? Ride : 0, member.ObjectToRepair );
		Assert.AreEqual( taken ? Mechanic : OtherMember, Thing( state, Ride ).AssignedStaff );
	}

	/// <summary>
	/// With no route to the ride's entry the mechanic walks about or stands, the ride not assigned, and the ride
	/// found is still written to him (<c>0x004da609</c> comes before the route's test).
	/// </summary>
	[TestMethod]
	public void ABrokenRideWithNoRouteToItIsNotTakenButIsStillNamed()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, _) = MechanicOn( world, state );
		var shut = new PeepWalk( member.Navigator, ( _, _, _ ) => true );

		Change( state, Ride, ride => ride with { State = 1 } );

		Behaviour( state ).Step( member, shut, playing: null, tick: 1001 );

		Assert.AreEqual( Ride, member.ObjectToRepair );
		Assert.AreEqual( 0, Thing( state, Ride ).AssignedStaff );
		Assert.AreNotEqual( StaffActivity.GoingToRide, member.Activity );
	}

	/// <summary>A decide that finds no ride writes nought over the job it held.</summary>
	[TestMethod]
	public void ADecideThatFindsNoRideForgetsTheOldJob()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );

		member.ObjectToRepair = Ride;

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( 0, member.ObjectToRepair );
		Assert.AreEqual( StaffActivity.Walking, member.Activity );
	}

	/// <summary>
	/// Arriving, he repairs for his grade's <c>WorkDuration</c> (80, 60, 40, 30, 20: <c>0x0078542c</c>) scaled by
	/// the State of repair lost, its byte, in whole sweeps (<c>0x004da42a</c>), on animation <c>0x12</c>; the
	/// walk's last turn costs him nothing.
	/// </summary>
	[DataTestMethod]
	[DataRow( 92f, 3, 2, DisplayName = "92 at grade 3: 8 x 30 / 100" )]
	[DataRow( 92.9f, 0, 6, DisplayName = "cut to a byte, not rounded: 8 x 80 / 100" )]
	[DataRow( 0f, 0, 80, DisplayName = "nothing left at grade 0" )]
	[DataRow( 50f, 1, 30, DisplayName = "half at grade 1" )]
	[DataRow( 99f, 4, 0, DisplayName = "1 x 20 / 100 is nought" )]
	[DataRow( 100f, 2, 0, DisplayName = "nothing lost" )]
	public void ArrivingHeRepairsForHisGradesDurationScaledByTheRepairLost( float repair, int grade, int sweeps )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state, grade );

		Change( state, Ride, ride => ride with { State = 1, StateOfRepair = repair } );
		OnHisWayAtTheEntry( state, member, Ride );

		var rest = member.Tiredness;

		Assert.AreEqual( 1001, Until( Behaviour( state ), member, walk, StaffActivity.GoingToRide, 1001 ) );
		Assert.AreEqual( StaffActivity.Repairing, member.Activity );
		Assert.AreEqual( sweeps, member.DurationOfRepair );
		Assert.AreEqual( Staff.RepairingAnimation, member.NextAnimation );
		Assert.AreEqual( 0x12, Staff.RepairingAnimation );
		Assert.AreEqual( rest, member.Tiredness, "the walk to a ride costs no rest" );
		Assert.AreEqual( Ride, member.ObjectToRepair );
		Assert.AreEqual( repair, Thing( state, Ride ).StateOfRepair, "nothing is mended yet" );
	}

	/// <summary>A called ride is repaired on arrival too, broken or not (<c>FUN_004da740</c> asks 1, 2 and the call).</summary>
	[TestMethod]
	public void ArrivingAtARideHeWasCalledToHeRepairs()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );

		Change( state, Ride, ride => ride with { RequestedService = 1, StateOfRepair = 40f } );
		OnHisWayAtTheEntry( state, member, Ride );

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Repairing, member.Activity );
		Assert.AreEqual( 18, member.DurationOfRepair, "60 x 30 / 100" );
	}

	/// <summary>
	/// Arriving at a ride that wants nothing he decides again in the same turn; on the way to one that is no
	/// longer his he forgets it and decides (<c>FUN_004da740</c>).
	/// </summary>
	[DataTestMethod]
	[DataRow( true, DisplayName = "his, but mended by the time he arrives" )]
	[DataRow( false, DisplayName = "broken, but another's" )]
	public void ARideThatNeedsNothingOrIsNotHisSendsHimBackToHisDecide( bool his )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );

		Change( state, Ride, ride => ride with { State = his ? 0 : 1 } );
		OnHisWayAtTheEntry( state, member, Ride );

		if ( !his )
			Change( state, Ride, ride => ride with { AssignedStaff = OtherMember, TimeMarkedForMaintenance = 1001 } );

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.Walking, member.Activity, "the decide ran, found nothing and walks" );
		Assert.AreEqual( 0, member.ObjectToRepair, "written by that decide's search" );
		Assert.AreEqual( his ? 0 : 1, Thing( state, Ride ).State );
	}

	/// <summary>
	/// The job is forgotten by the walk's and the repair's own arms, before the decide: a mechanic who then rests
	/// and makes no search holds no ride.
	/// </summary>
	[DataTestMethod]
	[DataRow( StaffActivity.GoingToRide, DisplayName = "on the way to a ride that is another's" )]
	[DataRow( StaffActivity.Repairing, DisplayName = "as the repair ends" )]
	public void TheJobIsForgottenBeforeTheDecideAndATiredMechanicSearchesNoMore( StaffActivity from )
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var cursor = state.MechanicCursor;

		Change( state, Ride, ride => ride with { RequestedService = 1 } );
		OnHisWayAtTheEntry( state, member, Ride );

		if ( from == StaffActivity.GoingToRide )
			Change( state, Ride, ride => ride with { AssignedStaff = OtherMember, TimeMarkedForMaintenance = 1001 } );
		else
			member.StartRepairing( 0 );

		member.Tiredness = 0.5f;

		Behaviour( state ).Step( member, walk, playing: null, tick: 1001 );

		Assert.AreEqual( StaffActivity.GoingToRest, member.Activity, "the decide sent him to rest" );
		Assert.AreEqual( cursor, state.MechanicCursor, "and made no search" );
		Assert.AreEqual( 0, member.ObjectToRepair );
	}

	/// <summary>The variable the repair's end writes is <c>VAR_BREAKSTAT</c>, the fifth of the common twelve.</summary>
	[TestMethod]
	public void TheBreakIsCalledOffThroughBreakStat()
	{
		Assert.AreEqual( "VAR_BREAKSTAT", ParkRideOperation.BreakStatVariable );
		Assert.AreEqual( 4, (int)RideVariables.VAR_BREAKSTAT );
	}

	/// <summary>A walk to a ride that cannot be reached ends in the decide, the job forgotten.</summary>
	[TestMethod]
	public void AWalkToARideThatCannotBeReachedForgetsItAndDecides()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, _) = MechanicOn( world, state );
		var entry = Thing( state, Ride );

		member.ObjectToRepair = Ride;
		member.SetActivity( StaffActivity.GoingToRide, 1000 );
		member.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( entry.EntryCellX ),
			PeepNavigator.WaypointCentre( entry.EntryCellY ) );
		Change( state, Ride, ride => ride with { State = 4, AssignedStaff = Mechanic, TimeMarkedForMaintenance = 1000 } );

		var shut = new PeepWalk( member.Navigator, ( _, _, _ ) => true );
		var cursor = state.MechanicCursor;

		// Too tired to work, so the decide that follows makes no search: the walk's own arm forgets the job.
		member.Tiredness = 0.5f;

		Behaviour( state ).Step( member, shut, playing: null, tick: 1001 );

		Assert.AreNotEqual( StaffActivity.GoingToRide, member.Activity );
		Assert.AreEqual( cursor, state.MechanicCursor, "no search was made" );
		Assert.AreEqual( 0, member.ObjectToRepair );
	}

	/// <summary>
	/// The repair (<c>FUN_004da830</c>): a turn of work and one off the count each sweep; at nought a broken
	/// ride's script is told the break is over and he works on while it still reads broken; then the ride is
	/// repaired and opened and he decides in the same turn.
	/// </summary>
	[TestMethod]
	public void HeCountsDownWaitsForTheScriptThenTheRideIsRepairedAndOpen()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var script = ToiletScript();

		Change( state, Toilet, toilet => toilet with
		{
			State = 1, CanLoad = 0, StateOfRepair = 90f, RequestedService = 1
		} );
		script.Set( ParkRideOperation.BrokenVariable, 1 );
		script.Set( ParkRideOperation.BreakStatVariable, 1 );
		script.Set( ParkRideOperation.ClosedVariable, 1 );
		script.Set( ParkRideOperation.WornVariable, 1 );
		OnHisWayAtTheEntry( state, member, Toilet );

		var behaviour = Behaviour( state, id => id == Toilet ? script : null );

		behaviour.Step( member, walk, playing: null, tick: 1001 );
		Assert.AreEqual( 3, member.DurationOfRepair, "10 x 30 / 100" );

		var rest = member.Tiredness;

		for ( var left = 2; left >= 0; --left )
		{
			behaviour.Step( member, walk, playing: null, tick: 1004 - left );

			Assert.AreEqual( left, member.DurationOfRepair );
			Assert.AreEqual( 1, script[ParkRideOperation.BreakStatVariable], "not told until the count is out" );
		}

		Assert.IsTrue( member.Tiredness < rest, "each turn is a turn of work" );

		// The count out: the script is told, and still reads broken for two more sweeps.
		for ( var tick = 1005; tick < 1007; ++tick )
		{
			behaviour.Step( member, walk, playing: null, tick );

			Assert.AreEqual( StaffActivity.Repairing, member.Activity );
			Assert.AreEqual( 0, script[ParkRideOperation.BreakStatVariable] );
			Assert.AreEqual( 1, Thing( state, Toilet ).State, "not mended while the script reads broken" );
			Assert.AreEqual( 90f, Thing( state, Toilet ).StateOfRepair );
		}

		script.Set( ParkRideOperation.BrokenVariable, 0 );
		behaviour.Step( member, walk, playing: null, tick: 1007 );

		var mended = Thing( state, Toilet );

		Assert.AreEqual( 0, mended.State );
		Assert.AreEqual( 1, mended.CanLoad );
		Assert.AreEqual( 100f, mended.StateOfRepair );
		Assert.AreEqual( 0, mended.AssignedStaff );
		Assert.AreEqual( 0, mended.RequestedService );
		Assert.AreEqual( 0, script[ParkRideOperation.ClosedVariable] );
		Assert.AreEqual( 0, script[ParkRideOperation.WornVariable] );
		Assert.AreEqual( 0, member.ObjectToRepair );
		Assert.AreEqual( StaffActivity.Walking, member.Activity, "the decide ran in the same turn" );
	}

	/// <summary>A called ride that is not broken is repaired as the count runs out, with no word to its script and no wait.</summary>
	[TestMethod]
	public void ACalledRideIsRepairedAsTheCountRunsOut()
	{
		var world = World();
		var state = new ParkState( world );
		var (member, walk) = MechanicOn( world, state );
		var script = ToiletScript();

		Change( state, Ride, ride => ride with { RequestedService = 1, CanLoad = 0, StateOfRepair = 99f } );
		script.Set( ParkRideOperation.BrokenVariable, 1 );
		script.Set( ParkRideOperation.BreakStatVariable, 1 );
		OnHisWayAtTheEntry( state, member, Ride );

		var behaviour = Behaviour( state, id => id == Ride ? script : null );

		behaviour.Step( member, walk, playing: null, tick: 1001 );
		Assert.AreEqual( StaffActivity.Repairing, member.Activity );
		Assert.AreEqual( 0, member.DurationOfRepair );

		behaviour.Step( member, walk, playing: null, tick: 1002 );

		Assert.AreEqual( 100f, Thing( state, Ride ).StateOfRepair, "no wait for a script that reads broken: the ride's state is not 1" );
		Assert.AreEqual( 1, Thing( state, Ride ).CanLoad );
		Assert.AreEqual( 0, Thing( state, Ride ).RequestedService );
		Assert.AreEqual( 0, script[ParkRideOperation.BreakStatVariable], "the repair itself writes it nought" );
		Assert.AreNotEqual( StaffActivity.Repairing, member.Activity );
	}

	/// <summary>
	/// The ride's side (<c>FUN_004df8f0</c>) with no script, and its unbuilt arms: a ride waiting for an upgrade is
	/// counted and left; a coaster's circuit test is counted.
	/// </summary>
	[DataTestMethod]
	[DataRow( 1, 0, 0, 0, DisplayName = "broken down" )]
	[DataRow( 4, 0, 0, 0, DisplayName = "condemned: opened whatever its state" )]
	[DataRow( 2, 0, 1, 0, DisplayName = "an upgrade: counted, left" )]
	[DataRow( 1, ItemDescriptionFile.CoasterTrack, 0, 1, DisplayName = "a coaster: the circuit test counted" )]
	public void TheRepairMendsAndOpensAndCountsWhatItDoesNotBuild( int thingState, int track, int upgrades, int circuits )
	{
		var state = new ParkState( World() );
		var before = (Counted( "RIDE_UPGRADE_COMPLETION" ), Counted( "OPEN_GUARD_COASTER_TRACK_RECORD" ));

		Change( state, Ride, ride => ride with
		{
			State = thingState, CanLoad = 0, StateOfRepair = 12f, AssignedStaff = Mechanic, RequestedService = 1
		} );

		ParkRideOperation.Repair( state, script: null, Ride, track );

		var ride = Thing( state, Ride );
		var mended = upgrades == 0;

		Assert.AreEqual( mended ? 0 : 2, ride.State );
		Assert.AreEqual( mended ? 1 : 0, ride.CanLoad );
		Assert.AreEqual( mended ? 100f : 12f, ride.StateOfRepair );
		Assert.AreEqual( mended ? 0 : Mechanic, ride.AssignedStaff );
		Assert.AreEqual( mended ? 0 : 1, ride.RequestedService );
		Assert.AreEqual( upgrades, Counted( "RIDE_UPGRADE_COMPLETION" ) - before.Item1 );
		Assert.AreEqual( circuits, Counted( "OPEN_GUARD_COASTER_TRACK_RECORD" ) - before.Item2 );
	}

	/// <summary>
	/// A mechanic whose ride is sold drops the job and goes idle, in any state; one aiming at another thing is
	/// left alone (<c>docs/exe/park-engine.md</c>, "Selling and the people on it").
	/// </summary>
	[DataTestMethod]
	[DataRow( Ride, StaffActivity.Idle, 0 )]
	[DataRow( Toilet, StaffActivity.Repairing, Ride )]
	public void AMechanicWhoseRideIsSoldDropsTheJob( int sold, StaffActivity then, int job )
	{
		var world = World();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Mechanic );
		var member = new Staff( Mechanic, saved.Model, saved.Staff!.Value with
		{
			State = (int)StaffActivity.Repairing, ObjectToRepair = Ride
		}, saved.Navigator );

		Behaviour( state ).ThingRemoved( member, walk: null, sold, tick: 1001 );

		Assert.AreEqual( then, member.Activity );
		Assert.AreEqual( job, member.ObjectToRepair );
	}

	/// <summary>
	/// The save's <c>mDurationOfRepair</c> (mechanic record 503), <c>mObjectToRepair</c> (507) and the mechanics'
	/// HQ's <c>mNextObject</c> are read, and written back as the park runs (FileFormats <c>saves.md</c>). The
	/// shipped park holds nought in the first two, so the writer's file is what a reader at a wrong offset
	/// would misread.
	/// </summary>
	[TestMethod]
	public void TheRepairJobAndTheCursorAreWrittenAndReadBack()
	{
		var world = World();
		var saved = world.People.Single( person => person.ThingId == Mechanic ).Staff!.Value;

		Assert.AreEqual( 0, saved.ObjectToRepair );
		Assert.AreEqual( 0, saved.DurationOfRepair );

		var state = new ParkState( world );
		var people = new ParkPeople( world, Balance(), gateStatus: null, state, new ParkItemCatalogue( "jungle", data ) );

		try
		{
			var member = people.Staff.Single( staff => staff.ThingId == Mechanic );

			member.ObjectToRepair = Ride;
			member.StartRepairing( 0x01020304 );
			state.ObjectsFromTheMechanicsCursor();

			// A handyman's cleaning stamp lies over the same bytes of his record.
			people.Staff.Single( staff => staff.ThingId == OtherMember ).TimeStartedCleaning = 0x05060708;

			var cursor = state.MechanicCursor;

			Assert.AreNotEqual( world.MechanicCursor, cursor );

			var again = new ParkWorld( ParkFileWriter.Body( world, new ParkFileWriter.Running( world.GameTick, false, 0, 0,
				world.Camera.Saved!.Value, People: people.Written( Level.WrittenThings( world ).Contains ),
				MechanicCursor: cursor ) ) );

			Assert.IsNull( again.Problem );

			var written = again.People.Single( person => person.ThingId == Mechanic ).Staff!.Value;

			Assert.AreEqual( (int)StaffActivity.Repairing, written.State );
			Assert.AreEqual( Ride, written.ObjectToRepair, "mObjectToRepair at 507" );
			Assert.AreEqual( 0x01020304, written.DurationOfRepair, "mDurationOfRepair at 503" );
			Assert.AreEqual( cursor, again.MechanicCursor, "mNextObject" );
			Assert.IsTrue( again.People.Where( other => other.ThingId != Mechanic && other.Staff is { } )
				.All( other => other.Staff!.Value.ObjectToRepair == 0 && other.Staff!.Value.DurationOfRepair == 0 ),
				"only a mechanic's record has the two" );

			var loaded = ParkPeople.StaffIn( again ).Single( staff => staff.ThingId == Mechanic );

			Assert.AreEqual( Ride, loaded.ObjectToRepair );
			Assert.AreEqual( 0x01020304, loaded.DurationOfRepair );
			Assert.AreEqual( cursor, new ParkState( again ).MechanicCursor );

			// Without the cursor given, the file's is left.
			var left = new ParkWorld( ParkFileWriter.Body( world, new ParkFileWriter.Running( world.GameTick, false, 0, 0,
				world.Camera.Saved!.Value ) ) );

			Assert.AreEqual( world.MechanicCursor, left.MechanicCursor );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>A mechanic on a job whose ride is not written is written standing, the job dropped.</summary>
	[TestMethod]
	public void AMechanicWhoseRideIsNotWrittenIsWrittenStanding()
	{
		var world = World();
		var state = new ParkState( world );
		var people = new ParkPeople( world, Balance(), gateStatus: null, state, new ParkItemCatalogue( "jungle", data ) );

		try
		{
			var member = people.Staff.Single( staff => staff.ThingId == Mechanic );

			member.ObjectToRepair = Ride;
			member.StartRepairing( 7 );

			var written = people.Written( id => id != Ride ).Single( person => person.Person.ThingId == Mechanic ).Person.Staff!.Value;

			Assert.AreEqual( (int)StaffActivity.Idle, written.State );
			Assert.AreEqual( 0, written.ObjectToRepair );
			Assert.AreEqual( 0, written.DurationOfRepair );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
