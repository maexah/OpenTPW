using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The guests' reached arms that Q157 counted: the gate's guard on an entering guest (<c>FUN_004ffb20</c>, built by
/// Q861), a leaver's change of mind (<c>FUN_00500a50</c>) and the route improver's diagonal pass
/// (<c>FUN_005108a0</c>). Each name is asserted present where the original tests and absent where it does not.
/// </summary>
[TestClass]
public class ParkReachedArmsCountTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	private (PeepBehaviour Behaviour, Peep Guest, PeepWalk Walk) Guest( PeepState state, bool shut = false, int gate = ParkRides.GateIsOpen )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );
		var parkState = new ParkState( world );

		parkState.SetParkClosed( shut );

		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => gate, parkState );
		var guest = ParkPeople.PeepsIn( world ).First( peep => peep.ThingId == 38 );

		guest.SetState( state, 0, new Random( 1 ) );
		guest.Happiness = 50;
		guest.ExitLevel = 100;

		return (behaviour, guest, new PeepWalk( guest.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ));
	}

	private static (int X, int Y) CellOfType( ParkState state, int type )
		=> Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize )
			.Select( at => (X: at % ParkWorld.MapSize, Y: at / ParkWorld.MapSize) )
			.First( cell => state.Record( cell.X, cell.Y ).Type == type );

	/// <summary>
	/// The gate's guard on an entering guest (<c>FUN_004ffb20</c>, <c>0x004ffb2b</c>..<c>0x004ffb42</c>, Q861): a shut park
	/// or a gate not reporting 1 skips the walk, sends the guest to the centre of a ticket booth (<c>FUN_004fa530</c>)
	/// and puts them in state 2, their opening wait untouched and nobody admitted. An open park and gate walk on.
	/// </summary>
	[TestMethod]
	[DataRow( false, ParkRides.GateIsOpen, false, "open park, open gate" )]
	[DataRow( true, ParkRides.GateIsOpen, true, "the entry-price door shut the park" )]
	[DataRow( false, 0, true, "the gate reports something other than 1" )]
	[DataRow( false, 2, true, "the gate reports 2" )]
	public void AnEnteringGuestIsTurnedBackToATicketBoothWhenTheGateWillNotAdmit( bool shut, int gate, bool turnedBack, string why )
	{
		var (behaviour, guest, walk) = Guest( PeepState.Entering, shut, gate );
		var admission = behaviour.Admission!;
		var at = walk.Position;
		var visitors = behaviour.VisitorsToDate;

		guest.ParkOpeningWait = 77;

		behaviour.Step( guest, walk, playing: null, tick: 1 );

		if ( !turnedBack )
		{
			Assert.AreEqual( PeepState.Entering, guest.State, why );
			Assert.AreNotEqual( at, walk.Position, $"{why}: the walk goes on" );
			return;
		}

		var booths = new[] { admission.TicketBoothA, admission.TicketBoothB }
			.Select( cell => new FixedVector( PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) ) );

		Assert.AreEqual( PeepState.HeadingForGate, guest.State, why );
		CollectionAssert.Contains( booths.ToList(), guest.Navigator.Target, $"{why}: the centre of a ticket booth" );
		Assert.AreEqual( at, walk.Position, $"{why}: the walk is skipped" );
		Assert.AreEqual( 77, guest.ParkOpeningWait, $"{why}: the opening wait is left as it is" );
		Assert.AreEqual( visitors, behaviour.VisitorsToDate, $"{why}: nobody is admitted" );
	}

	/// <summary>
	/// The booth is drawn every time (<c>0x004ffb85</c>..<c>0x004ffba1</c>): a guest standing on booth A is sent to
	/// booth B by some draws, where keeping the booth they stand on (the waiting turn's rule) would never.
	/// </summary>
	[TestMethod]
	public void TheTurnedBackGuestsBoothIsDrawnEvenOnABooth()
	{
		var chosen = new HashSet<(int X, int Y)>();

		for ( var seed = 0; seed < 16; seed++ )
		{
			using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
			var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
			var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );
			var state = new ParkState( world );

			state.SetParkClosed( true );

			var behaviour = new PeepBehaviour( world, new Random( seed ), admission, () => ParkRides.GateIsOpen, state );

			var guest = ParkPeople.PeepsIn( world ).First( peep => peep.ThingId == 38 );

			guest.SetState( PeepState.Entering, 0, new Random( 1 ) );
			guest.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( admission.TicketBoothA.X ),
				PeepNavigator.WaypointCentre( admission.TicketBoothA.Y ) );

			var walk = new PeepWalk( guest.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

			behaviour.Step( guest, walk, playing: null, tick: 1 );

			Assert.AreEqual( PeepState.HeadingForGate, guest.State );
			chosen.Add( guest.Navigator.Target.Cell );
		}

		Assert.IsTrue( chosen.Contains( (47, 13) ) && chosen.Contains( (48, 13) ),
			$"both booths drawn from booth A; drew {string.Join( ", ", chosen )}" );
	}

	/// <summary>The guard is built, so it is no longer counted, and the other states do not ask it.</summary>
	[TestMethod]
	[DataRow( PeepState.Entering )]
	[DataRow( PeepState.HeadingForExit )]
	public void TheGateGuardIsNoLongerCounted( PeepState state )
	{
		var (behaviour, guest, walk) = Guest( state, shut: true );
		var before = Counted( "ENTERING_GATE_GUARD" );

		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( before, Counted( "ENTERING_GATE_GUARD" ) );
	}

	/// <summary>
	/// The change of mind is counted when all four of its tests pass (<c>0x00500a53</c>..<c>0x00500a84</c>) and when
	/// any one fails it is not: the exit level above nought (a negative one is not, <c>JLE</c>), the happiness
	/// byte not nought (a half is truncated to nought by <c>__ftol</c>), an open park, and a counting cell.
	/// </summary>
	[TestMethod]
	[DataRow( 100, 50f, false, 1, true, "all four pass" )]
	[DataRow( 1, 50f, false, 1, true, "an exit level of one" )]
	[DataRow( 0, 50f, false, 1, false, "an exit level of nought" )]
	[DataRow( -1, 50f, false, 1, false, "a negative exit level" )]
	[DataRow( 100, 0f, false, 1, false, "no happiness" )]
	[DataRow( 100, 0.5f, false, 1, false, "half a point truncates to nought" )]
	[DataRow( 100, 1f, false, 1, true, "a point of happiness" )]
	[DataRow( 100, 50f, true, 1, false, "a shut park" )]
	[DataRow( 100, 50f, false, 2, false, "a cell that is a ride's footprint" )]
	public void AGuestHeadingForTheExitIsCountedWhereTheChangeOfMindIsTested(
		int exitLevel, float happiness, bool shut, int cellType, bool counted, string why )
	{
		var (behaviour, guest, walk) = Guest( PeepState.HeadingForExit, shut );
		var cell = CellOfType( behaviour.State, cellType );

		behaviour.State.StandOn( guest.ThingId, cell.X, cell.Y );
		guest.ExitLevel = exitLevel;
		guest.Happiness = happiness;

		var before = Counted( "HEADING_FOR_EXIT_CHANGE_OF_MIND" );

		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( counted ? before + 1 : before, Counted( "HEADING_FOR_EXIT_CHANGE_OF_MIND" ), why );
	}

	/// <summary>
	/// The change of mind is reached by a running game (Q862): the door shuts the park, a deciding guest with a positive exit level
	/// sets off (<c>PeepBehaviour.Leave</c> writes no exit level, as <c>0x004fee87</c>..<c>0x004fef13</c> store nothing to
	/// <c>+0x1bc</c>), the door opens again while they walk, and the next turn is counted. While it stays shut nothing is.
	/// </summary>
	[TestMethod]
	public void AGuestWhoSetOffWhenTheParkShutIsCountedWhenItReopens()
	{
		var (behaviour, guest, walk) = Guest( PeepState.Deciding, shut: true );
		var cell = CellOfType( behaviour.State, 1 );

		behaviour.State.StandOn( guest.ThingId, cell.X, cell.Y );
		guest.ExitLevel = 100;
		guest.Happiness = 50;

		var before = Counted( "HEADING_FOR_EXIT_CHANGE_OF_MIND" );

		behaviour.Step( guest, walk, playing: null, tick: 1 );

		Assert.AreEqual( PeepState.HeadingForExit, guest.State, "the shut park sent them out" );
		Assert.AreEqual( 100, guest.ExitLevel, "setting off writes no exit level" );

		behaviour.Step( guest, walk, playing: null, tick: 2 );
		Assert.AreEqual( before, Counted( "HEADING_FOR_EXIT_CHANGE_OF_MIND" ), "the park is still shut" );

		// Reopened while they walk: a path cell under them again, as one of the cells on the way is.
		behaviour.State.SetParkClosed( false );
		behaviour.State.StandOn( guest.ThingId, cell.X, cell.Y );
		behaviour.Step( guest, walk, playing: null, tick: 3 );

		Assert.AreEqual( before + 1, Counted( "HEADING_FOR_EXIT_CHANGE_OF_MIND" ), "reopened while they walk out" );
	}

	private static CellRoute Route( (int X, int Y) start, params (int X, int Y)[] waypoints )
	{
		var route = new CellRoute { Start = start, Goal = waypoints[^1] };

		route.Waypoints.AddRange( waypoints );

		return route;
	}

	/// <summary>
	/// A scan that splices nothing ends in the diagonal pass, which writes only to a step of one cell on both
	/// axes: the count is made for such a route, not for a straight one or a longer slant.
	/// </summary>
	[TestMethod]
	[DataRow( 10, 10, 11, 11, true, "a one-cell diagonal from the start" )]
	[DataRow( 10, 10, 11, 12, 12, 12, false, "open ground drops the waypoint, leaving a knight's step" )]
	[DataRow( 10, 10, 10, 15, false, "a straight run" )]
	[DataRow( 10, 10, 10, 11, false, "a one-cell step along one axis" )]
	[DataRow( 10, 10, 13, 13, false, "a three-cell slant" )]
	[DataRow( 10, 10, 11, 12, false, "a knight's step" )]
	public void ARouteWithAOneCellDiagonalIsCountedWhereTheDiagonalPassWouldRun( params object[] arguments )
	{
		var numbers = arguments.Take( arguments.Length - 2 ).Cast<int>().ToArray();
		var counted = (bool)arguments[^2];
		var why = (string)arguments[^1];
		var cells = Enumerable.Range( 1, numbers.Length / 2 - 1 ).Select( i => (numbers[i * 2], numbers[i * 2 + 1]) ).ToArray();
		var route = Route( (numbers[0], numbers[1]), cells );
		var before = Counted( "CELL_REROUTE_DIAGONAL_PASS" );

		CellReroute.Run( route, ( _, _, _ ) => false );

		Assert.AreEqual( counted ? before + 1 : before, Counted( "CELL_REROUTE_DIAGONAL_PASS" ), why );
	}

	/// <summary>
	/// A step after the first is looked at too: a wall down the east side of column 10 holds the waypoint (10,11) in place, so the
	/// route stays (10,10), (10,11), (11,12) and its second step is the one-cell diagonal. A test that read only the first step
	/// would count none.
	/// </summary>
	[TestMethod]
	public void ADiagonalAsTheSecondStepIsCountedWhereAWallHoldsTheWaypoint()
	{
		var route = Route( (10, 10), (10, 11), (11, 12) );
		var before = Counted( "CELL_REROUTE_DIAGONAL_PASS" );

		CellReroute.Run( route, ( x, _, side ) => x == 10 && side == StepDirection.East );

		CollectionAssert.AreEqual( new[] { (10, 11), (11, 12) }, route.Waypoints, "the wall kept the waypoint" );
		Assert.AreEqual( before + 1, Counted( "CELL_REROUTE_DIAGONAL_PASS" ) );
	}

	/// <summary>
	/// Running out of rounds returns without the diagonal pass (<c>0x00510a1e</c> is reached only by a scan that splices
	/// nothing). Three shut sides make the route from (5,0) through (11,8) to (12,8) splice nine times, one a round, and come out
	/// (6,1), (6,3), (9,4), (12,8): a one-cell diagonal in its first step, which a tenth scan would have counted, and none is.
	/// </summary>
	[TestMethod]
	public void NineSplicesEndTheRouteWithNoDiagonalPass()
	{
		var shut = new HashSet<(int, int, StepDirection)> { (6, 2, StepDirection.East), (7, 1, StepDirection.South), (8, 4, StepDirection.South) };
		var route = Route( (5, 0), (11, 8), (12, 8) );
		var before = Counted( "CELL_REROUTE_DIAGONAL_PASS" );

		CellReroute.Run( route, ( x, y, side ) => shut.Contains( (x, y, side) ) );

		CollectionAssert.AreEqual( new[] { (6, 1), (6, 3), (9, 4), (12, 8) }, route.Waypoints );
		Assert.AreEqual( before, Counted( "CELL_REROUTE_DIAGONAL_PASS" ), "no scan was left to run the pass" );
	}
}
