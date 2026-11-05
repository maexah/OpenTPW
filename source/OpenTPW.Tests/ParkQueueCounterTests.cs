using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A queue cell's counter - written by the lay's re-stamp, read by the clear - and Backspace on the queue tool, which is
/// the clear's one reader in play (<c>docs/exe/park-engine.md</c>, "A queue cell's counter"). The ride is
/// <see cref="Ride"/>'s thing 77 at (19,11), its node at (20,10); the runs go north to (20,7) and west to (17,7), on bare
/// ground inside the band the path tests use.
/// </summary>
[TestClass]
public class ParkQueueCounterTests
{
	private BaseFileSystem data = null!;
	private Level? previous;

	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = data = GameData.Required();
		previous = Level.Current;
		ParkBuildMode.Forget();
	}

	[TestCleanup]
	public void PutTheLevelBack()
	{
		Level.Current = previous!;
		ParkBuildMode.Forget();
	}

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>A level that holds a park and its running state, as <c>Level.Current</c>.</summary>
	private void Enter( ParkWorld park, ParkState state )
	{
		var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, park );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		Level.Current = level;
	}

	/// <summary>The ride and its node: the placer's queue cell before the entrance, with the tool anchored on it.</summary>
	private (ParkWorld Park, ParkState State) Placed()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = new ParkWorld.CatalogueObject( ThingId: 77, CatalogueId: 1100, RawX: 19 << 8, RawY: 11 << 8, Angle: 0,
			EntryPos: (ushort)MapStep.CellId( 20, 11 ), ExitPos: (ushort)MapStep.CellId( 20, 14 ) );

		state.AddObject( ride );
		ParkBuilding.Stamp( state, (19, 11, 20, 14), 19, 11 );
		ParkBuilding.MarkWaysInAndOut( state, park, 20, 11, 20, 14, 0x10, 0x10, 0, hasQueue: true, MapStep.CellId( 19, 11 ) );
		Enter( park, state );

		// The placer empties the pending list and pushes the anchor.
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, 77, 20, 10 );
		ParkBuildMode.ClearPending();
		ParkBuildMode.Push( 20, 10 );

		return (park, state);
	}

	private static ParkWorld.MapCell At( ParkWorld park, int x, int y ) => ParkState.CellFor( park, x, y );

	/// <summary>
	/// <b>Every click's run starts on its anchor, already queue, and the stamp adds 1 to it</b> (<c>0x005346f5</c>), so
	/// the node holds 1 after the first click and the corner the first run ended on holds 1 after the second; the cells
	/// a run lays fresh hold nought. Backspace then takes up the last run (<c>FUN_0052fe50</c>): the three fresh cells go
	/// and the corner stays, at 0 now it has lost its link toward the run, the end of the queue as it was. The second
	/// press takes up the first run, and the node, which the placer marked NOMODIFY, is returned untouched (<c>0x005367d4</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the lay's already-queue arm writing no counter leaves the node at 0 and the corner at 0, which the
	/// first two assertions read, and the corner then goes with its run; the pending list not pushed at each click
	/// leaves Backspace nothing to take up.
	/// </remarks>
	[TestMethod]
	public void ARunsAnchorGainsOneAndBackspaceKeepsTheCornerItLeft()
	{
		var (park, state) = Placed();
		var balance = state.Balance;

		Assert.AreEqual( 0, At( park, 20, 10 ).OverlapCounter, "the placer's node holds nought before a click" );

		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3 from (20,10) to (20,7)" );

		Assert.AreEqual( 1, At( park, 20, 10 ).OverlapCounter, "the first click's anchor, the node, was re-stamped" );

		foreach ( var y in new[] { 9, 8, 7 } )
			Assert.AreEqual( 0, At( park, 20, y ).OverlapCounter, $"(20,{y}) was laid fresh" );

		StringAssert.Contains( ParkPathBuilding.RunQueue( 17, 7 ), "laid 3 from (20,7) to (17,7)" );

		Assert.AreEqual( 1, At( park, 20, 7 ).OverlapCounter, "the second click's anchor, the corner, was re-stamped" );
		Assert.AreEqual( 1, At( park, 20, 10 ).OverlapCounter, "and the node is not stamped again" );

		foreach ( var x in new[] { 19, 18, 17 } )
			Assert.AreEqual( 0, At( park, x, 7 ).OverlapCounter, $"({x},7) was laid fresh" );

		CollectionAssert.AreEqual( new[] { (20, 10), (20, 7), (17, 7) }, ParkBuildMode.Pending.ToArray(), "each click pushed its target" );
		Assert.AreEqual( balance - 6 * 75, state.Balance, "six cells were laid, the re-stamps charged nothing" );

		var posts = Times( "BACKSPACE_QUEUE_ADVISOR_0xCB" );
		var said = ParkPathBuilding.UndoQueueRun();

		StringAssert.Contains( said, "took up the queue run from (20,7) to (17,7), 3 cells went, refunded 225" );

		foreach ( var x in new[] { 19, 18, 17 } )
			Assert.AreEqual( CellEdge.Nothing, At( park, x, 7 ).Type, $"({x},7) went" );

		var corner = At( park, 20, 7 );

		Assert.AreEqual( ParkRideChoice.QueueCellType, corner.Type, "the corner stays, the end of the queue as it was" );
		Assert.AreEqual( 0, corner.OverlapCounter, "its 1 was taken once it was no corner" );
		Assert.AreEqual( 0x10, corner.Neighbours & 0x55, "and it links toward the node (south) only" );
		Assert.AreEqual( (20, 7), ParkBuildMode.Anchor, "the tool anchors where the run began" );
		CollectionAssert.AreEqual( new[] { (20, 10), (20, 7) }, ParkBuildMode.Pending.ToArray() );
		Assert.AreEqual( balance - 3 * 75, state.Balance, "three cells give their price back" );
		Assert.AreEqual( posts + 1, Times( "BACKSPACE_QUEUE_ADVISOR_0xCB" ), "mode 3 is re-armed, advisor 0xcb counted" );

		StringAssert.Contains( ParkPathBuilding.UndoQueueRun(), "took up the queue run from (20,10) to (20,7), 3 cells went" );

		foreach ( var y in new[] { 9, 8, 7 } )
			Assert.AreEqual( CellEdge.Nothing, At( park, 20, y ).Type, $"(20,{y}) went" );

		var node = At( park, 20, 10 );

		Assert.AreEqual( ParkRideChoice.QueueCellType, node.Type, "the node stays" );
		Assert.AreNotEqual( 0, node.Flags & ParkPathBuilding.NoModify, "it is the placer's NOMODIFY cell" );
		Assert.AreEqual( 1, node.OverlapCounter, "returned untouched, so its 1 is kept" );
		Assert.AreEqual( balance, state.Balance, "every laid cell is given back" );

		Assert.IsTrue( ParkPathBuilding.UndoQueueRun().Contains( "nothing laid" ), "the first entry is never popped" );
		Assert.AreEqual( (20, 10), ParkBuildMode.Anchor );
	}

	/// <summary>
	/// <b>The queue button keeps the walk's list pending</b> (<c>0x0081b740</c>): the entrance's faced cell, each corner and the
	/// cell the walk stops on, the last the anchor. Backspace then takes the edited queue up corner by corner, as it takes up
	/// a run just laid.
	/// </summary>
	/// <remarks><b>Mutation:</b> leaving the list as it was (the node alone, from the placer) answers "nothing laid" at the first press.</remarks>
	[TestMethod]
	public void TheQueueButtonKeepsTheWalksListForBackspace()
	{
		var (park, state) = Placed();

		ParkPathBuilding.RunQueue( 20, 7 );
		ParkPathBuilding.RunQueue( 17, 7 );
		ParkBuildMode.Disarm();
		ParkBuildMode.ClearPending();

		StringAssert.Contains( ParkPathBuilding.EditQueue( 77 ), "from (17,7)" );

		CollectionAssert.AreEqual( new[] { (20, 10), (20, 7), (17, 7) }, ParkBuildMode.Pending.ToArray(), "the walk's list" );
		Assert.AreEqual( 1, At( park, 20, 7 ).OverlapCounter, "the walk sets a corner to 1" );
		Assert.AreEqual( 0, At( park, 20, 10 ).OverlapCounter, "and the rest to nought" );

		StringAssert.Contains( ParkPathBuilding.UndoQueueRun(), "took up the queue run from (20,7) to (17,7), 3 cells went" );
		Assert.AreEqual( ParkRideChoice.QueueCellType, At( park, 20, 7 ).Type, "the corner stays" );
		Assert.AreEqual( (20, 7), ParkBuildMode.Anchor );
	}

	/// <summary>
	/// <b>Nothing is cleared when the run's start is a path</b> (<c>0x0052ff9a</c>): the pop still anchors the tool there.
	/// </summary>
	[TestMethod]
	public void AClearIsSkippedWhereTheRunsStartIsAPath()
	{
		var (park, state) = Placed();

		ParkPathBuilding.RunQueue( 20, 8 );
		state.SetRecord( 20, 10, At( park, 20, 10 ) with { Type = CellEdge.Path } );

		StringAssert.Contains( ParkPathBuilding.UndoQueueRun(), "0 cells went" );
		Assert.AreEqual( ParkRideChoice.QueueCellType, At( park, 20, 8 ).Type, "the run is left standing" );
		Assert.AreEqual( (20, 10), ParkBuildMode.Anchor );
	}

	/// <summary>
	/// <b>The clear's queue arm</b> (<c>0x005369ec</c>..<c>0x00536ab2</c>), the table of Q896's C4: a press takes one off, an
	/// unstepped one or a corner's sets -1, and the cell goes only below nought; a NOMODIFY cell is returned untouched.
	/// </summary>
	/// <remarks><b>Mutations:</b> removing at nought, not below it, fails the straight at 1; skipping the corner test fails the corner at 1; ignoring NOMODIFY fails the last row.</remarks>
	[TestMethod]
	public void ASteppedClearKeepsACellWhoseCounterIsStillNoughtOrMore()
	{
		var (park, state) = Placed();
		var owner = (ushort)MapStep.CellId( 19, 11 );

		bool Press( int counter, int neighbours, bool stepped, int flags = 0 )
		{
			state.SetRecord( 24, 3, At( park, 24, 3 ) with
			{
				Type = ParkRideChoice.QueueCellType,
				Neighbours = (byte)neighbours,
				OverlapCounter = (short)counter,
				ParentId = owner,
				Flags = (byte)flags
			} );

			return ParkPathBuilding.ClearQueueCell( state, park, 24, 3, stepped, out _ );
		}

		Assert.IsFalse( Press( 1, 0x11, stepped: true ), "a straight at 1 is kept" );
		Assert.AreEqual( 0, At( park, 24, 3 ).OverlapCounter, "at 0" );
		Assert.IsTrue( Press( 0, 0x11, stepped: true ), "a straight at 0 goes" );
		Assert.AreEqual( CellEdge.Nothing, At( park, 24, 3 ).Type );
		Assert.IsTrue( Press( 1, 0x41, stepped: true ), "a corner at 1 goes: its counter is -1 whatever it holds" );
		Assert.IsTrue( Press( 3, 0x11, stepped: false ), "an unstepped press on a straight at 3 goes" );
		Assert.IsFalse( Press( 1, 0x10, stepped: true ), "a one-link end at 1 is kept" );
		Assert.AreEqual( 0, At( park, 24, 3 ).OverlapCounter, "at 0" );
		Assert.IsFalse( Press( 0, 0x11, stepped: true, flags: ParkPathBuilding.NoModify ), "a NOMODIFY cell is returned untouched" );
		Assert.AreEqual( ParkRideChoice.QueueCellType, At( park, 24, 3 ).Type );
		Assert.AreEqual( 0, At( park, 24, 3 ).OverlapCounter, "its counter too" );
	}

	private static int Times( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
