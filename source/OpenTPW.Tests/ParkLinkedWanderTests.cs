using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// SetRandomDest's linked arm - <c>0x004f95c6</c>..<c>0x004f9916</c>, decoded in
/// <c>docs/exe/ride-operation.md</c>, "SetRandomDest", the linked arm, and built as <see cref="LinkedWander"/>.
/// The first tests walk a map made here and need no game; the last three stand a guest in the shipped park.
/// </summary>
[TestClass]
public class ParkLinkedWanderTests
{
	private const int South = 0x10, East = 0x04, North = 0x01, West = 0x40;

	private sealed class Constant( int draw ) : Random
	{
		public int Taken { get; private set; }

		public override int Next()
		{
			++Taken;

			return draw;
		}
	}

	/// <summary>A map of the cells given, every other cell bare and unlinked.</summary>
	private static Func<int, int, ParkWorld.MapCell> Map(
		params (int X, int Y, int Type, int Neighbours, int Direction)[] cells )
	{
		var map = cells.ToDictionary( cell => (cell.X, cell.Y),
			cell => new ParkWorld.MapCell( cell.Type, 0, (byte)cell.Neighbours, (byte)cell.Direction, 0, 0, 0, 0 ) );

		return ( x, y ) => map.TryGetValue( (x, y), out var cell ) ? cell : default;
	}

	/// <summary>A straight run of path along y = 10 from x = from to x = to, linked end to end.</summary>
	private static Func<int, int, ParkWorld.MapCell> Corridor( int from, int to )
		=> Map( Enumerable.Range( from, to - from + 1 ).Select( x => (x, 10, CellEdge.Path,
			(x > from ? West : 0) | (x < to ? East : 0), 0) ).ToArray() );

	/// <summary>The slots run 0x10 (0, +1), 0x04 (+1, 0), 0x01 (0, -1), 0x40 (-1, 0), and the draw's low two bits pick the first.</summary>
	[DataTestMethod]
	[DataRow( 0, 20, 21 )]
	[DataRow( 1, 21, 20 )]
	[DataRow( 2, 20, 19 )]
	[DataRow( 3, 19, 20 )]
	[DataRow( 7, 19, 20 )]
	public void TheDrawPicksTheSlotTheWalkStartsAt( int draw, int x, int y )
	{
		var map = Map( (20, 20, CellEdge.Path, 0x55, 0) );

		CollectionAssert.AreEqual( new[] { (x, y) }, LinkedWander.Walk( map, 20, 20, 1, new Constant( draw ) ) );
	}

	/// <summary>The mask read is the cell's being LEFT: a link one way is walked, and the cell it leads to, with no links of its own, is a dead end.</summary>
	[TestMethod]
	public void TheMaskOfTheCellBeingLeftIsTheOneRead()
	{
		var map = Map( (20, 20, CellEdge.Path, East, 0) );

		CollectionAssert.AreEqual( new[] { (21, 20) }, LinkedWander.Walk( map, 20, 20, 1, new Constant( 0 ) ) );
		Assert.IsNull( LinkedWander.Walk( map, 20, 20, 2, new Constant( 0 ) ), "the second pass stands on a cell with no links" );
		Assert.IsNull( LinkedWander.Walk( map, 30, 30, 1, new Constant( 0 ) ), "a cell with no links has no slot" );
	}

	/// <summary>A count below two takes the first slot left with no draw, a step back allowed; a last pass back onto the person's own cell is given one more.</summary>
	[TestMethod]
	public void ASingleLinkIsTakenInSlotOrderAndMayStepBack()
	{
		var map = Corridor( 20, 21 );
		var draws = new Constant( 1 );

		CollectionAssert.AreEqual( new[] { (21, 10) }, LinkedWander.Walk( map, 20, 10, 1, draws ) );
		CollectionAssert.AreEqual( new[] { (21, 10), (20, 10), (21, 10) }, LinkedWander.Walk( map, 20, 10, 2, draws ),
			"back onto the own cell on the last pass, so one more" );
		CollectionAssert.AreEqual( new[] { (21, 10), (20, 10), (21, 10) }, LinkedWander.Walk( map, 20, 10, 3, draws ) );
		Assert.AreEqual( 0, draws.Taken, "a count below two draws nothing" );
	}

	/// <summary>With two or more counted the walk never undoes its last step, so a corridor is walked one way for all its passes.</summary>
	[TestMethod]
	public void TwoOrMoreNeverTurnStraightBack()
	{
		var map = Corridor( 10, 30 );

		for ( var seed = 0; seed < 50; ++seed )
		{
			for ( var passes = 1; passes <= LinkedWander.MostPasses; ++passes )
			{
				var stepped = LinkedWander.Walk( map, 20, 10, passes, new Random( seed ) )!;

				Assert.AreEqual( passes, stepped.Count );
				Assert.AreEqual( passes, Math.Abs( stepped[^1].X - 20 ), $"seed {seed}: {passes} passes end {passes} cells away" );
			}
		}
	}

	/// <summary>From a path cell a queue or entrance neighbour is struck out; from a bare cell it is not.</summary>
	[DataTestMethod]
	[DataRow( 3 )]
	[DataRow( CellEdge.RideEnd )]
	public void FromPathAQueueOrEntranceNeighbourIsStruckOut( int type )
	{
		var path = Map( (20, 20, CellEdge.Path, East | West, 0), (21, 20, type, West, 0), (19, 20, CellEdge.Path, East, 0) );
		var bare = Map( (20, 20, CellEdge.Nothing, East | West, 0), (21, 20, type, West, 0), (19, 20, CellEdge.Path, East, 0) );

		// A draw of 1 starts at the east slot, which the count of two would take.
		CollectionAssert.AreEqual( new[] { (19, 20) }, LinkedWander.Walk( path, 20, 20, 1, new Constant( 1 ) ) );
		CollectionAssert.AreEqual( new[] { (21, 20) }, LinkedWander.Walk( bare, 20, 20, 1, new Constant( 1 ) ) );
	}

	/// <summary>An exit neighbour is struck out whatever the cell left.</summary>
	[DataTestMethod]
	[DataRow( CellEdge.Path )]
	[DataRow( CellEdge.Nothing )]
	[DataRow( 3 )]
	public void AnExitNeighbourIsStruckOut( int from )
	{
		var map = Map( (20, 20, from, East | West, 0), (21, 20, CellEdge.RideFarEnd, West, 0), (19, 20, CellEdge.Path, East, 0) );

		CollectionAssert.AreEqual( new[] { (19, 20) }, LinkedWander.Walk( map, 20, 20, 1, new Constant( 1 ) ) );
	}

	/// <summary>
	/// On a queue cell the slot <c>mDirection</c> names is struck out and the count lowered, even when that slot
	/// was already empty: two links then count as one, and the first in slot order is taken with no draw. An
	/// entrance keeps the slot.
	/// </summary>
	[TestMethod]
	public void AQueueCellLosesTheSlotItsDirectionNames()
	{
		var named = Map( (20, 20, 3, East | West, East) );

		CollectionAssert.AreEqual( new[] { (19, 20) }, LinkedWander.Walk( named, 20, 20, 1, new Constant( 1 ) ) );

		// Links east and north, the direction naming the empty west: counted one, so the fixed order's east,
		// where a draw of 2 on a count of two would start at north.
		var empty = Map( (20, 20, 3, East | North, West) );
		var draws = new Constant( 2 );

		CollectionAssert.AreEqual( new[] { (21, 20) }, LinkedWander.Walk( empty, 20, 20, 1, draws ) );
		Assert.AreEqual( 0, draws.Taken );

		var entrance = Map( (20, 20, CellEdge.RideEnd, East | West, East) );

		CollectionAssert.AreEqual( new[] { (21, 20) }, LinkedWander.Walk( entrance, 20, 20, 1, new Constant( 1 ) ) );
	}

	/// <summary>The caller's filter strikes a slot out first, and with every slot gone the walk is a dead end.</summary>
	[TestMethod]
	public void TheCallersFilterStrikesSlotsOut()
	{
		var map = Corridor( 10, 30 );

		CollectionAssert.AreEqual( new[] { (19, 10) },
			LinkedWander.Walk( map, 20, 10, 1, new Constant( 1 ), ( x, _ ) => x <= 20 ) );
		Assert.IsNull( LinkedWander.Walk( map, 20, 10, 1, new Constant( 1 ), ( _, _ ) => false ) );
	}

	private const string Theme = "jungle";

	private static (ParkWorld World, ParkState State, ParkPeople People) Open()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );

		return (world, state, new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( Theme, data ) ));
	}

	private static Peep GuestAt( int cellX, int cellY, PeepState state = PeepState.Deciding )
		=> new( 7, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Wandering, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: PeepNavigator.WaypointCentre( cellX ), Y: PeepNavigator.WaypointCentre( cellY ),
				VelocityX: 0, VelocityY: 0,
				TargetX: PeepNavigator.WaypointCentre( cellX ), TargetY: PeepNavigator.WaypointCentre( cellY + 1 ),
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 26214, MaxSpeed: 13107, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

	private static PeepBehaviour Behaviour( ParkWorld world, ParkState state, Random random )
		=> new( world, random, new ParkAdmission( new ParkBalance( Theme, easyMode: true ),
			world.Economy!.Value.AdmissionFee ), () => ParkRides.GateIsOpen, state );

	/// <summary>
	/// In the shipped park (48,22) is path linked to the Belly Bounce's back of queue at (49,22). No wander from
	/// it is aimed into a queue, entrance or exit cell or back into (48,22), every one is aimed into path, and
	/// across the seeds the aims lie from one to five cells off.
	/// </summary>
	[TestMethod]
	public void AGuestWanderingFromBesideAQueueKeepsToThePath()
	{
		var (world, state, people) = Open();

		try
		{
			var own = state.Record( 48, 22 );

			Assert.AreEqual( CellEdge.Path, own.Type );
			Assert.IsTrue( (own.Neighbours & East) != 0 && state.Record( 49, 22 ).Type == 3,
				"the cell must link to a queue cell, or the filter is not exercised" );

			var far = new HashSet<int>();

			for ( var seed = 0; seed < 400; ++seed )
			{
				var peep = GuestAt( 48, 22 );
				var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

				if ( !Behaviour( world, state, new Random( seed ) ).SetRandomDest( peep, walk ) )
					continue;

				var (x, y) = peep.Navigator.Target.Cell;

				Assert.AreEqual( CellEdge.Path, state.Record( x, y ).Type, $"seed {seed} aims into ({x},{y})" );
				Assert.AreNotEqual( (48, 22), (x, y), $"seed {seed} aims into the guest's own cell" );
				Assert.IsTrue( peep.SetDestSuccessfully, "a route sets mSetDestSuccessfully" );

				far.Add( Math.Abs( x - 48 ) + Math.Abs( y - 22 ) );
			}

			Assert.AreEqual( 1, far.Min() );
			Assert.AreEqual( LinkedWander.MostPasses, far.Max(), "a wander of five passes ends five cells off" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// Entering the wandering state routes again to the stored destination once SetRandomDest has ever routed
	/// the guest (<c>FUN_00501db0</c> case 7), and not before: seen as a spot animation ends on a guest who was
	/// wandering, whose walk has no route yet.
	/// </summary>
	[DataTestMethod]
	[DataRow( true )]
	[DataRow( false )]
	public void EnteringWanderingRoutesAgainOnceADestinationWasEverSet( bool everSet )
	{
		var (world, state, people) = Open();

		try
		{
			var peep = GuestAt( 48, 22, PeepState.PlayingSpotAnimation );
			var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

			peep.SetDestSuccessfully = everSet;

			Assert.IsFalse( walk.HasRoute );

			Behaviour( world, state, new Random( 1 ) ).Step( peep, walk, playing: null,
				tick: PeepBehaviour.SpotAnimationSweeps + 1 );

			Assert.AreEqual( PeepState.Wandering, peep.State );
			Assert.AreEqual( everSet, walk.HasRoute );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
