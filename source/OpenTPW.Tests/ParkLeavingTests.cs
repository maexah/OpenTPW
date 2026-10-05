using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// When a guest leaves - the leaving arm of the deciding turn (<c>FUN_004fec90</c>, <c>0x004fee5b</c>;
/// <c>docs/exe/ride-operation.md</c>, "Q109").
/// </summary>
[TestClass]
public class ParkLeavingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private sealed class Constant( int draw ) : Random
	{
		public int Taken { get; private set; }

		public override int Next()
		{
			++Taken;

			return draw;
		}
	}

	/// <summary>A draw whose remainder by three is the split's idle arm, with the low bit clear.</summary>
	private const int IdleEven = 2;

	/// <summary><see cref="IdleEven"/> with the low bit set.</summary>
	private const int IdleOdd = 5;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkAdmission Admission( ParkWorld world, int? fee = null )
		=> new( new ParkBalance( "jungle", easyMode: true ), fee ?? world.Economy!.Value.AdmissionFee );

	/// <summary>
	/// Guest 38, saved in the gateway, put in a state with a walk of their own: the park's edges, or walls on
	/// every side so that no route is ever found.
	/// </summary>
	private (PeepBehaviour Behaviour, Peep Guest, PeepWalk Walk, ParkAdmission Admission) Guest(
		PeepState state, int draw = IdleEven, bool shut = false, bool walled = false, int? fee = null,
		Random? random = null )
	{
		var world = Park();
		var admission = Admission( world, fee );
		var behaviour = new PeepBehaviour( shut, world.NumberOfVisitorsToDate, random ?? new Constant( draw ),
			admission, () => ParkRides.GateIsOpen );

		Func<int, int, StepDirection, bool> blocked = walled
			? ( _, _, _ ) => true
			: CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var guest = ParkPeople.PeepsIn( world ).First( peep => peep.ThingId == 38 );

		guest.SetState( state, 0, new Random( 1 ) );
		guest.Happiness = 50;
		guest.ExitLevel = 100;

		return (behaviour, guest, new PeepWalk( guest.Navigator, blocked ), admission);
	}

	private static (int, int) Centre( (int X, int Y) cell )
		=> (PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ));

	[TestMethod]
	public void TheCrossingsParkSideIsTheBalanceFilesOwn()
	{
		var admission = Admission( Park() );

		Assert.AreEqual( (47, 9), admission.CrossingParkSideA );
		Assert.AreEqual( (48, 9), admission.CrossingParkSideB );
	}

	/// <summary>
	/// The exit level exactly nought: 25 off, and off to the crossing cell the draw's low bit puts first.
	/// </summary>
	[TestMethod]
	[DataRow( IdleEven, 47 )]
	[DataRow( IdleOdd, 48 )]
	public void ADecidingGuestWhoseDayHasRunOutSetsOffForTheCrossing( int draw, int column )
	{
		var (behaviour, guest, walk, admission) = Guest( PeepState.Deciding, draw );

		guest.ExitLevel = 0;
		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.HeadingForExit, guest.State );
		Assert.AreEqual( 50f - admission.BigHappinessChange, guest.Happiness, 0.01f );
		Assert.AreEqual( Centre( (column, 9) ), (guest.Navigator.Target.X, guest.Navigator.Target.Y) );
	}

	/// <summary>
	/// The test is for nought and not for nought or less: a guest whose day ran out while they were doing
	/// something else stays, however far below it has gone.
	/// </summary>
	[TestMethod]
	[DataRow( -1 )]
	[DataRow( -300 )]
	[DataRow( 1 )]
	public void AnExitLevelThatIsNotNoughtKeepsThemInThePark( int exitLevel )
	{
		var (behaviour, guest, walk, _) = Guest( PeepState.Deciding );

		guest.ExitLevel = exitLevel;

		for ( var tick = 1; tick <= 40; ++tick )
			behaviour.Step( guest, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Deciding, guest.State );
		Assert.AreEqual( 50f, guest.Happiness, 0.01f );
	}

	/// <summary>Only the deciding turn reads the exit level: a wanderer at nought, or below it, walks on.</summary>
	[TestMethod]
	[DataRow( PeepState.Wandering, 0 )]
	[DataRow( PeepState.Wandering, -20 )]
	[DataRow( PeepState.GoingToRide, 0 )]
	[DataRow( PeepState.LeavingRide, -20 )]
	public void NoOtherStateSendsAGuestHomeOnTheExitLevel( PeepState state, int exitLevel )
	{
		var (behaviour, guest, walk, _) = Guest( state );

		guest.ExitLevel = exitLevel;
		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreNotEqual( PeepState.HeadingForExit, guest.State );
		Assert.AreEqual( 50f, guest.Happiness, 0.01f );
	}

	/// <summary>The happiness byte, so anything under one is nought and one is not.</summary>
	[TestMethod]
	[DataRow( 0f, true )]
	[DataRow( 0.9f, true )]
	[DataRow( 1f, false )]
	public void AMiserableGuestLeavesWithDayToSpare( float happiness, bool leaves )
	{
		var (behaviour, guest, walk, _) = Guest( PeepState.Deciding );

		guest.Happiness = happiness;
		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( leaves ? PeepState.HeadingForExit : PeepState.Deciding, guest.State );
	}

	/// <summary>
	/// With no route to either cell they stay deciding, and the 25 comes off again on every turn the test
	/// holds; the original's second pass in walking mode 1 is counted each time. The turn takes one draw, which
	/// serves the order of the cells and the split after them.
	/// </summary>
	[TestMethod]
	public void WithNoWayOutTheyGoOnDecidingAndLoseHeartEveryTurn()
	{
		var draws = new Constant( IdleOdd );
		var (behaviour, guest, walk, _) = Guest( PeepState.Deciding, walled: true, random: draws );

		guest.ExitLevel = 0;
		guest.Happiness = 80;
		Unimplemented.Forget();

		for ( var tick = 1; tick <= 3; ++tick )
			behaviour.Step( guest, walk, playing: null, tick );

		Assert.AreEqual( PeepState.Deciding, guest.State );
		Assert.AreEqual( 5f, guest.Happiness, 0.01f );
		Assert.AreEqual( 3, Unimplemented.Summary.Single( row => row.What == "LEAVE_ROUTE_MODE_1_RETRY" ).Times );
		Assert.AreEqual( 3, draws.Taken, "one draw a turn" );
	}

	/// <summary>
	/// A leaver with no way out goes on into the split on the same turn and the same draw: here its wander arm,
	/// which finds nowhere either and stamps the idle time.
	/// </summary>
	[TestMethod]
	public void AGuestWithNoWayOutGoesOnIntoTheSplit()
	{
		var (behaviour, guest, walk, _) = Guest( PeepState.Deciding, draw: 1, walled: true );

		guest.ExitLevel = 0;
		behaviour.Step( guest, walk, playing: null, tick: 7 );

		Assert.AreEqual( PeepState.Deciding, guest.State );
		Assert.AreEqual( 25f, guest.Happiness, 0.01f );
		Assert.AreEqual( 7, guest.TimeStartedIdling, "the wander arm's stamp" );
	}

	/// <summary>A leaver whose walk sticks is put back to deciding (<c>FUN_00500a50</c>).</summary>
	[TestMethod]
	public void AGuestStuckOnTheWayOutDecidesAgain()
	{
		var (behaviour, guest, walk, admission) = Guest( PeepState.HeadingForExit, walled: true );

		PeepBehaviour.SendTo( behaviour.State, guest, walk, admission.CrossingParkSideA );
		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.Deciding, guest.State );
	}

	/// <summary>
	/// The gate's two leaving arms zero the exit level after the state (<c>FUN_004ff7f0</c>,
	/// <c>FUN_004ff9d0</c>), so such a guest put back to deciding leaves again at once.
	/// </summary>
	[TestMethod]
	[DataRow( PeepState.WaitingForOpening )]
	[DataRow( PeepState.JudgingTheFee )]
	public void AGuestTheGateTurnsAwayHasNoDayLeft( PeepState state )
	{
		var (behaviour, guest, walk, _) = Guest( state, shut: state == PeepState.WaitingForOpening, fee: 100000 );

		guest.ParkOpeningWait = 0;
		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.HeadingForExit, guest.State );
		Assert.AreEqual( 0, guest.ExitLevel );

		// Only the far-too-expensive arm thinks thought 6 (0x004ffa44); the waited-out arm thinks nothing.
		Assert.AreEqual( state == PeepState.JudgingTheFee ? 6 : 0, guest.Thoughts.Last );
	}

	/// <summary>
	/// The expensive arm's own leaver: the sulk is rolled, the medium change takes their last happiness, and
	/// the exit level is zeroed with the state. The fee is searched for, since the opinion's bands move with
	/// the park's worth.
	/// </summary>
	[TestMethod]
	public void AGuestSulkedOutOfTheirLastHappinessHasNoDayLeft()
	{
		for ( var fee = 20; fee <= 600; fee += 5 )
		{
			var (behaviour, guest, walk, _) = Guest( PeepState.JudgingTheFee, fee: fee );

			guest.ParkOpeningWait = 0;
			guest.Happiness = 10;
			behaviour.Step( guest, walk, playing: null, tick: 1 );

			// Of the arms that do not take the fee, only the expensive one rolls a sulk.
			if ( guest.State == PeepState.WaitingForOpening )
			{
				Assert.AreEqual( 0, guest.Thoughts.Last, "a guest who pays thinks nothing of the fee" );
				continue;
			}

			if ( guest.ParkOpeningWait == 0 )
				continue;

			Assert.AreEqual( PeepState.HeadingForExit, guest.State );
			Assert.AreEqual( 0, guest.ExitLevel );
			Assert.AreEqual( 6, guest.Thoughts.Last, "the sulk comes with thought 6 (0x004ffa7d)" );

			return;
		}

		Assert.Fail( "no fee from 20 to 600 was on the expensive side" );
	}
}
