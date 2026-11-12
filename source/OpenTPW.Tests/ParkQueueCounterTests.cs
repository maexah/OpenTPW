using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
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
		var ride = Ride();

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

	/// <summary>Thing 77, the ride <see cref="Placed"/> stands.</summary>
	private static ParkWorld.CatalogueObject Ride()
		=> new( ThingId: 77, CatalogueId: 1100, RawX: 19 << 8, RawY: 11 << 8, Angle: 0,
			EntryPos: (ushort)MapStep.CellId( 20, 11 ), ExitPos: (ushort)MapStep.CellId( 20, 14 ) );

	/// <summary>
	/// <b>Every click's run starts on its anchor, already queue, and the stamp adds 1 to it</b> (<c>0x005346f5</c>), so
	/// the node holds 1 after the first click and the corner the first run ended on holds 1 after the second; the cells
	/// a run lays fresh hold nought. Backspace then takes up the last run (<c>FUN_0052fe50</c>): the three fresh cells go
	/// and the corner stays, at 0 now it has lost its link toward the run, the end of the queue as it was. The second
	/// press takes up the first run, and the node, which the placer marked NOMODIFY, is returned untouched (<c>0x005367d4</c>).
	/// This level has no <c>Catalogue</c>, so the refunds are the counted <c>QUEUE_REFUND_OWNER_NOT_FOUND</c> fallback, the whole price;
	/// the percentage (37 a cell for the stock ride) is <c>ParkScrapTests</c>'.
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

		StringAssert.Contains( said, "took up the queue run from (20,7) to (17,7), 3 cells went, refunded 225" );   // the owner-not-found fallback: no Catalogue, so the whole price; the percentage is ParkScrapTests'

		foreach ( var x in new[] { 19, 18, 17 } )
			Assert.AreEqual( CellEdge.Nothing, At( park, x, 7 ).Type, $"({x},7) went" );

		var corner = At( park, 20, 7 );

		Assert.AreEqual( ParkRideChoice.QueueCellType, corner.Type, "the corner stays, the end of the queue as it was" );
		Assert.AreEqual( 0, corner.OverlapCounter, "its 1 was taken once it was no corner" );
		Assert.AreEqual( 0x10, corner.Neighbours & 0x55, "and it links toward the node (south) only" );
		Assert.AreEqual( (20, 7), ParkBuildMode.Anchor, "the tool anchors where the run began" );
		CollectionAssert.AreEqual( new[] { (20, 10), (20, 7) }, ParkBuildMode.Pending.ToArray() );
		Assert.AreEqual( balance - 3 * 75, state.Balance, "three cells give their price back (the owner-not-found fallback; the percentage is ParkScrapTests')" );
		Assert.AreEqual( posts + 1, Times( "BACKSPACE_QUEUE_ADVISOR_0xCB" ), "mode 3 is re-armed, advisor 0xcb counted" );

		StringAssert.Contains( ParkPathBuilding.UndoQueueRun(), "took up the queue run from (20,10) to (20,7), 3 cells went" );

		foreach ( var y in new[] { 9, 8, 7 } )
			Assert.AreEqual( CellEdge.Nothing, At( park, 20, y ).Type, $"(20,{y}) went" );

		var node = At( park, 20, 10 );

		Assert.AreEqual( ParkRideChoice.QueueCellType, node.Type, "the node stays" );
		Assert.AreNotEqual( 0, node.Flags & ParkPathBuilding.NoModify, "it is the placer's NOMODIFY cell" );
		Assert.AreEqual( 1, node.OverlapCounter, "returned untouched, so its 1 is kept" );
		Assert.AreEqual( balance, state.Balance, "every laid cell is given back (the owner-not-found fallback; the percentage is ParkScrapTests')" );

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

	/// <summary>
	/// <b>A click on the anchor lays its cell only while the list holds the tool's first cell alone</b>
	/// (<c>0x005273c7</c>..<c>0x005273cf</c>): then the node is re-stamped, +1, and pushed (<c>0x005273df</c>). After a run
	/// the click lays nothing (<c>0x00527528</c>), so the run's fresh end keeps its nought. Both put the tool away.
	/// </summary>
	/// <remarks><b>Mutation:</b> laying on every anchor click leaves (20,7) at 1.</remarks>
	[TestMethod]
	public void AClickOnTheAnchorLaysOnlyBeforeTheFirstRun()
	{
		var (park, _) = Placed();

		ParkPathBuilding.RunQueue( 20, 7 );
		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "put away" );

		Assert.AreEqual( 0, At( park, 20, 7 ).OverlapCounter, "two entries pending, so the click laid nothing" );
		Assert.AreEqual( 1, At( park, 20, 10 ).OverlapCounter, "the node keeps the first run's 1 alone" );
		CollectionAssert.AreEqual( new[] { (20, 10), (20, 7) }, ParkBuildMode.Pending.ToArray(), "nothing pushed" );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
		Assert.AreEqual( 2, ParkBuildMode.Clicks, "the anchor arm still counts (q1038 run2 b2 2, b3 3)" );
	}

	/// <summary><b>A click the preview refuses is not counted</b>: the latch turns it away before the apply arm's increment
	/// (0x00524a63 -> 0x005266a3; the increment is 0x0052732f). The original: q1038/orig/run4, d2-south 7, d3-ownqueue 7.</summary>
	/// <remarks><b>Mutation:</b> the increment back before the refusal reads 2.</remarks>
	[TestMethod]
	public void ARefusedRunIsNotCounted()
	{
		Placed();
		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3" );
		Assert.AreEqual( 1, ParkBuildMode.Clicks );
		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 9 ), "nothing laid" );   // (20,8) is the middle of its own queue
		Assert.AreEqual( 1, ParkBuildMode.Clicks, "the refused click is not counted" );
	}

	/// <summary>The placer's node clicked at once: one entry pending, so the node is laid again, +1, and pushed.</summary>
	[TestMethod]
	public void AClickOnTheNodeAtOnceRestampsIt()
	{
		var (park, _) = Placed();

		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 10 ), "put away" );

		Assert.AreEqual( 1, At( park, 20, 10 ).OverlapCounter );
		CollectionAssert.AreEqual( new[] { (20, 10), (20, 10) }, ParkBuildMode.Pending.ToArray() );
	}

	/// <summary>
	/// <b>A run that ends on its own queue stamps that last cell too</b> (<c>0x00534584</c> -> <c>FUN_005346d0</c>): it is
	/// queue already, so it gains 1.
	/// </summary>
	/// <remarks><b>Mutation:</b> linking the end cell without the lay's already-queue arm leaves (20,8) at 0.</remarks>
	[TestMethod]
	public void ARunEndingOnItsOwnQueueAddsOneToItsEndCell()
	{
		var (park, _) = Placed();

		ParkPathBuilding.RunQueue( 20, 8 );
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, 77, 23, 8 );
		ParkBuildMode.ClearPending();
		ParkBuildMode.Push( 23, 8 );

		Assert.AreEqual( 0, At( park, 20, 8 ).OverlapCounter );
		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 8 ), "ended on its own queue at (20,8)" );
		Assert.AreEqual( 1, At( park, 20, 8 ).OverlapCounter );
	}

	/// <summary>
	/// <b>Backspace measures the queue again</b> (<c>0x0052ffec</c>), read at the call itself, and <b>a removed cell whose owner's
	/// cell is bare gives nothing back</b>.
	/// </summary>
	/// <remarks><b>Mutations:</b> deleting the RemeasureQueue call in UndoQueueRun leaves the list empty; treating every owner as typed refunds the price.</remarks>
	[TestMethod]
	public void BackspaceMeasuresAgainAndABareOwnerGivesNoRefund()
	{
		var (park, state) = Placed();
		var told = new System.Collections.Generic.List<int>();

		ParkPathBuilding.RunQueue( 20, 7 );
		state.QueueRemeasured = id => told.Add( id );
		ParkPathBuilding.UndoQueueRun();

		CollectionAssert.Contains( told, 77, "the queue was measured again" );

		var balance = state.Balance;

		var bare = Enumerable.Range( 0, 128 ).Select( x => (X: x, Y: 3) ).First( at => At( park, at.X, at.Y ).Type == CellEdge.Nothing && at.X > 26 );

		state.SetRecord( 24, 3, At( park, 24, 3 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Neighbours = 0x11,
			OverlapCounter = 0,
			ParentId = (ushort)MapStep.CellId( bare.X, bare.Y )
		} );

		Assert.IsTrue( ParkPathBuilding.ClearQueueCell( state, park, 24, 3, stepped: true, out var refund ) );
		Assert.AreEqual( 0, refund, "its owner's cell is bare" );
		Assert.AreEqual( balance, state.Balance );
	}

	/// <summary>The park's advisor, with the Tutorial option on, which the Backspace tip's row needs.</summary>
	private static ParkAdvisorMessages Advisor( ParkState state )
		=> state.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );

	/// <summary>
	/// <b>The third anchored click posts the Backspace tip</b>, message <c>0xd6</c> about no thing (<c>0x0052732f</c>..
	/// <c>0x0052736d</c>), and <b>putting the tool away takes it back</b>, one of the ten the setter withdraws on a change
	/// of mode (<c>0x0052f279</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the post at the second click or the fourth; the withdrawal missing.</remarks>
	[TestMethod]
	public void TheThirdAnchoredClickPostsTheBackspaceTipAndPuttingTheToolAwayTakesItBack()
	{
		using var clock = new SimulationClockScope();
		var (_, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Clicks = 1;

		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3" );
		Assert.AreEqual( 2, ParkBuildMode.Clicks );
		StringAssert.Contains( lines.Census(), "pending=0 ", "nothing at the second" );

		StringAssert.Contains( ParkPathBuilding.RunQueue( 17, 7 ), "laid 3" );
		Assert.AreEqual( 3, ParkBuildMode.Clicks );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=214/subject=0 " );

		ParkBuildMode.Disarm();
		StringAssert.Contains( lines.Census(), "pending=0 ", "taken back as the tool is put away" );
	}

	/// <summary><b>The count runs on past three with no second post</b>: the test is for three exactly (<c>0x00527335</c>).</summary>
	/// <remarks><b>Mutation:</b> three or more posts at the fourth.</remarks>
	[TestMethod]
	public void TheFourthClickPostsNothing()
	{
		var (_, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Clicks = 3;

		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3" );
		Assert.AreEqual( 4, ParkBuildMode.Clicks );
		StringAssert.Contains( lines.Census(), "pending=0 " );
	}

	/// <summary>
	/// <b>The path tool counts on the same counter and posts the same tip</b> at its third anchored click, and he says
	/// it as response 433, sample 464 (row <c>0xd6</c>'s one line).
	/// </summary>
	/// <remarks><b>Mutations:</b> the path tool's post missing; the row's response or sample.</remarks>
	[TestMethod]
	public void ThePathToolsThirdClickPostsTheSameTip()
	{
		var (_, state) = Placed();
		var lines = Advisor( state );
		var said = new List<(int Response, int Sample)>();
		ParkBuildMode.Arm( ParkBuildMode.Path );

		StringAssert.Contains( ParkPathBuilding.RunPath( 11, 10 ), "anchored at (11,10)" );
		StringAssert.Contains( ParkPathBuilding.RunPath( 13, 10 ), "laid" );
		StringAssert.Contains( ParkPathBuilding.RunPath( 13, 12 ), "laid" );
		StringAssert.Contains( lines.Census(), "pending=0 ", "two clicks" );
		StringAssert.Contains( ParkPathBuilding.RunPath( 11, 12 ), "laid" );
		Assert.AreEqual( 3, ParkBuildMode.Clicks );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );

		lines.Tick( state.GameTick, 0, ( response, sample ) => { said.Add( (response, sample) ); return 9000; } );
		CollectionAssert.AreEqual( new[] { (433, 464) }, said );
	}

	/// <summary>
	/// <b>A Backspace that takes up a run withdraws the tip</b>: the pop arms <c>0x34</c> and then the mode again
	/// (<c>FUN_0052fe50</c>: <c>FUN_0052f200( 0x34, 1 )</c> at <c>0x0052ff08</c>, <c>FUN_0052f580</c> at <c>0x0052ffce</c>),
	/// a change of mode, which withdraws <c>0xd6</c> (<c>0x0052f279</c>).
	/// </summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkPathBuilding.UndoQueueRun"/> withdrawing nothing (the tip stays posted).</remarks>
	[TestMethod]
	public void BackspaceOnTheQueueToolWithdrawsTheTip()
	{
		using var clock = new SimulationClockScope();
		var (_, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Clicks = 2;

		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3" );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );

		StringAssert.Contains( ParkPathBuilding.UndoQueueRun(), "took up the queue run" );
		StringAssert.Contains( lines.Census(), "pending=0 ", "withdrawn by the pop's change of mode" );
	}

	/// <summary><b>The path tool's Backspace withdraws it the same way</b> (the same pop, mode 1).</summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkPathBuilding.UndoPathRun"/> withdrawing nothing.</remarks>
	[TestMethod]
	public void BackspaceOnThePathToolWithdrawsTheTip()
	{
		using var clock = new SimulationClockScope();
		var (_, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		ParkPathBuilding.RunPath( 11, 10 );
		ParkPathBuilding.RunPath( 13, 10 );
		ParkPathBuilding.RunPath( 13, 12 );
		ParkPathBuilding.RunPath( 11, 12 );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );

		StringAssert.Contains( ParkPathBuilding.UndoPathRun(), "took up the run" );
		StringAssert.Contains( lines.Census(), "pending=0 " );
	}

	/// <summary>
	/// <b>A Backspace with only the tool's first cell left changes no mode</b>, so the tip stays: that pop writes the
	/// count back and re-arms the same mode (<c>0x0052fecf</c>, <c>0x005300a6</c>).
	/// </summary>
	/// <remarks><b>Mutation:</b> the withdrawal before <see cref="ParkBuildMode.TryPopRun"/> rather than after its success.</remarks>
	[TestMethod]
	public void ABackspaceThatTakesUpNothingLeavesTheTip()
	{
		using var clock = new SimulationClockScope();
		var (_, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Clicks = 2;
		ParkBuildMode.CountClick();

		StringAssert.Contains( ParkPathBuilding.UndoQueueRun(), "nothing laid" );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );
	}

	/// <summary>
	/// <b>A change of mode to one other than None withdraws the tip</b>: the path tool's third click posts it, and the
	/// placer's hand-over to the queue tool (<c>FUN_0052f580( 3 )</c> at <c>0x0052529e</c>) takes it back
	/// (<c>cmp [0x81ae2c], esi</c> at <c>0x0052f239</c>, then <c>0x0052f279</c>). <b>Re-arming the same mode keeps it.</b>
	/// </summary>
	/// <remarks>
	/// <b>Mutations</b> of <see cref="ParkBuildMode.Arm"/>'s guard: <c>mode != Current</c> narrowed to <c>mode == None</c>; the
	/// withdrawal moved under <c>if ( mode == None )</c> (both keep it at the hand-over); the guard dropped (withdraws on the
	/// same-mode re-arm).
	/// </remarks>
	[TestMethod]
	public void AChangeOfToolWithdrawsTheTipAndTheSameToolKeepsIt()
	{
		using var clock = new SimulationClockScope();
		var (_, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		ParkPathBuilding.RunPath( 11, 10 );
		ParkPathBuilding.RunPath( 13, 10 );
		ParkPathBuilding.RunPath( 13, 12 );
		ParkPathBuilding.RunPath( 11, 12 );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 ", "the same mode withdraws nothing" );

		ParkBuildMode.ArmAt( ParkBuildMode.Queue, 77, 20, 10 );
		StringAssert.Contains( lines.Census(), "pending=0 ", "path to queue is a change" );
	}

	/// <summary>A ride whose entrance stands on bare ground (15,8): no queue, so the drain pops nothing.</summary>
	private static ParkWorld.CatalogueObject Unqueued( ParkWorld park )
	{
		Assert.AreEqual( CellEdge.Nothing, At( park, 15, 8 ).Type );
		return Ride() with { ThingId = 78, EntryPos = (ushort)MapStep.CellId( 15, 8 ) };
	}

	/// <summary>
	/// <b>The sale's drain withdraws the tip</b> when its arm of mode 3 (<c>FUN_0052f200( 3, 0 )</c>, <c>0x00527fa5</c>)
	/// changes the mode, here from the path tool, though the ride has no queue and nothing is popped.
	/// </summary>
	/// <remarks><b>Mutations:</b> the drain withdrawing nothing; withdrawing only when it pops.</remarks>
	[TestMethod]
	public void TheSalesDrainFromAnotherToolWithdrawsTheTip()
	{
		using var clock = new SimulationClockScope();
		var (park, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.Clicks = 2;
		ParkBuildMode.CountClick();
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );

		Assert.AreEqual( 0, ParkPathBuilding.DrainQueue( state, park, Unqueued( park ), 100 ) );
		StringAssert.Contains( lines.Census(), "pending=0 " );
	}

	/// <summary>
	/// <b>With the queue tool armed, the drain withdraws the tip only when it pops</b> (<c>0x00527fc1</c>, each pop a swap
	/// to <c>0x34</c> and back, <c>0x0052ff08</c>): a ride with no queue pops nothing and keeps it; thing 77's queue, its
	/// node and the run laid from it, is popped and withdraws it.
	/// </summary>
	/// <remarks><b>Mutations:</b> withdrawing on every drain (the first half); the drain withdrawing nothing (the second).</remarks>
	[TestMethod]
	public void TheSalesDrainUnderTheQueueToolWithdrawsOnlyWhenItPops()
	{
		using var clock = new SimulationClockScope();
		var (park, state) = Placed();
		var lines = Advisor( state );
		ParkBuildMode.Clicks = 2;

		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3" );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 " );

		Assert.AreEqual( 0, ParkPathBuilding.DrainQueue( state, park, Unqueued( park ), 100 ) );
		StringAssert.Contains( lines.Census(), "slot=214/subject=0 ", "no queue: nothing popped, and mode 3 stands" );

		ParkPathBuilding.DrainQueue( state, park, Ride(), 100 );
		StringAssert.Contains( lines.Census(), "pending=0 ", "a laid run is popped" );
	}

	/// <summary>
	/// <b>The click counter outlives a park</b>, as the original's global does across a load (<c>q1038</c>: run 3's
	/// <c>c5-east</c> 6, run 4's <c>d0-before</c> 6), so the third click can fall in the next park; the pending list is
	/// emptied, as the original's park entry empties it (a deviation for an in-park load, said at
	/// <see cref="ParkBuildMode.LeavePark"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="Level.ForgetPark"/> calling <see cref="ParkBuildMode.Forget"/>; the counter zeroed in <see cref="ParkBuildMode.LeavePark"/>; the list kept.</remarks>
	[TestMethod]
	public void TheClickCounterOutlivesAParkAndTheListDoesNot()
	{
		Placed();
		ParkBuildMode.Clicks = 1;
		StringAssert.Contains( ParkPathBuilding.RunQueue( 20, 7 ), "laid 3" );

		Level.ForgetPark();

		Assert.AreEqual( 2, ParkBuildMode.Clicks );
		Assert.AreEqual( 0, ParkBuildMode.Pending.Count );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
	}

	private static int Times( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
