using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park as it is being played - <see cref="ParkState"/>.
///
/// <para>
/// <b>The assertion that carries this file is the identity one.</b> A running park has exactly one of
/// these and everything shares it; a simulation that quietly made its own would still pass every test
/// about arithmetic, and the money on screen would simply never move. So the test that matters asks
/// whether the object the caller handed in is the same object the simulation writes through.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkStateTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>It starts as the save left it, rather than at nought.</summary>
	[TestMethod]
	public void ItIsSeededFromTheSaveRatherThanFromNothing()
	{
		var world = Park();
		var state = new ParkState( world );

		Assert.AreEqual( 87987, state.Balance, "the balance the save was left with" );
		Assert.AreEqual( 0, state.Takings, "nothing has been taken at the gate yet" );
		Assert.AreEqual( 0, state.VisitorsToDate, "nobody has been admitted" );
		Assert.IsFalse( state.ParkIsClosed, "the shipped park is saved open" );
	}

	/// <summary>
	/// Taking a fee moves the balance <b>and</b> the gate's running total, and nothing else moves the running
	/// total - the original adds a fee to <c>mBalance</c> and to its analyser's month gate takings (<c>FUN_004d0600</c>).
	/// </summary>
	[TestMethod]
	public void TakingAFeeMovesTheBalanceAndTheTakingsTogether()
	{
		var state = new ParkState( Park() );
		var before = state.Balance;

		state.Take( 25 );
		state.Take( 25 );

		Assert.AreEqual( before + 50, state.Balance, "the balance has moved by both fees" );
		Assert.AreEqual( 50, state.Takings, "and so has the running total" );
	}

	/// <summary>Admitting hands back which visitor they are, counting from one.</summary>
	[TestMethod]
	public void AdmittingCountsFromOne()
	{
		var state = new ParkState( Park() );

		Assert.AreEqual( 1, state.Admit(), "the first visitor" );
		Assert.AreEqual( 2, state.Admit(), "the second" );
		Assert.AreEqual( 2, state.VisitorsToDate, "and the park has had two" );
	}

	/// <summary>
	/// The cells come from the save too - the eleven placed objects are already standing on theirs, and
	/// nothing has been dropped anywhere.
	/// </summary>
	[TestMethod]
	public void TheCellsAreSeededFromTheSaveAsWell()
	{
		var world = Park();
		var state = new ParkState( world );

		Assert.AreEqual( 0, state.LitteredCells, "a park nobody has played holds no litter" );

		foreach ( var o in world.Objects )
		{
			if ( o.IsPlaced )
				Assert.AreEqual( o.ThingId, state.CellAt( o.CellX, o.CellY ).Occupant,
					$"object {o.ThingId} should already occupy ({o.CellX},{o.CellY})" );
		}
	}

	/// <summary>
	/// <b>What is standing on a cell is a LIST, headed by the most recent arrival.</b>
	///
	/// <para>
	/// The original keeps the head in the cell's own <c>+0x24</c> and the links on the things themselves -
	/// <c>FUN_004d91f0</c> puts one on, <c>FUN_004d9280</c> takes one off. It is LIFO: whoever arrives
	/// last heads the list. That is not a detail, it is what the gate reads - <c>Wait</c>'s paid arm asks
	/// whether the cell names <i>this</i> guest.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WhatStandsOnACellIsAListHeadedByTheLatestArrival()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		state.StandOn( 7, 10, 10 );
		state.StandOn( 8, 10, 10 );
		state.StandOn( 9, 10, 10 );

		Assert.AreEqual( 9, state.CellAt( 10, 10 ).Occupant, "the last to arrive heads the list" );
		Assert.AreEqual( 8, state.NextOnCell( 9 ), "with the one before behind them" );
		Assert.AreEqual( 7, state.NextOnCell( 8 ) );
		Assert.AreEqual( 0, state.NextOnCell( 7 ), "and nobody behind the first" );
	}

	/// <summary>
	/// <b>The load-bearing one, as it is for the queue: taking the MIDDLE one out joins up both sides.</b>
	/// Dropping a link here only shows from the side nobody checked.
	/// </summary>
	[TestMethod]
	public void SteppingOffACellJoinsUpWhoeverStoodEitherSide()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		state.StandOn( 7, 10, 10 );
		state.StandOn( 8, 10, 10 );
		state.StandOn( 9, 10, 10 );

		// The middle one walks to another cell.
		state.StandOn( 8, 12, 12 );

		Assert.AreEqual( 9, state.CellAt( 10, 10 ).Occupant, "the head is untouched" );
		Assert.AreEqual( 7, state.NextOnCell( 9 ), "and the two either side of them are joined up" );
		Assert.AreEqual( 8, state.CellAt( 12, 12 ).Occupant, "and they now head the cell they walked to" );

		// And the head leaving promotes whoever is behind it.
		state.StandOn( 9, 12, 12 );

		Assert.AreEqual( 7, state.CellAt( 10, 10 ).Occupant, "the one left behind becomes the head" );
		Assert.AreEqual( 9, state.CellAt( 12, 12 ).Occupant, "and the mover heads its new cell" );
	}

	/// <summary>
	/// <b>Standing still is a no-op, and that is the original's first test rather than an optimisation.</b>
	/// <c>FUN_0050b6a0</c> compares the cell before it unlinks anything - a walking guest crosses one cell
	/// over many steps, and relinking on each would put them back at the head of their own cell every tick.
	/// </summary>
	[TestMethod]
	public void StandingStillDoesNotRelinkAnybody()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		state.StandOn( 7, 10, 10 );
		state.StandOn( 8, 10, 10 );

		// 7 is asked again for the cell it is already on.
		state.StandOn( 7, 10, 10 );

		Assert.AreEqual( 8, state.CellAt( 10, 10 ).Occupant,
			"the one standing still must not jump back to the head of its own cell" );
		Assert.AreEqual( 7, state.NextOnCell( 8 ), "and the order behind it is unchanged" );
	}

	/// <summary>
	/// <b>A cell can be changed, and the change sticks.</b> <see cref="ParkState.CellAt"/> hands back a
	/// reference; had it handed back a copy, every write would compile, run, and go nowhere - which is
	/// exactly the sort of silent no-op this project keeps catching.
	/// </summary>
	[TestMethod]
	public void ChangingACellSticksRatherThanWritingToACopy()
	{
		var state = new ParkState( Park() );

		ref var cell = ref state.CellAt( 10, 11 );
		cell.Litter = 5;
		cell.LitterCollector = 26;

		Assert.AreEqual( 5, state.CellAt( 10, 11 ).Litter, "the litter should still be there" );
		Assert.AreEqual( 26, state.CellAt( 10, 11 ).LitterCollector, "and so should thing 26's claim on it" );
		Assert.AreEqual( 1, state.LitteredCells, "and the park should now count one littered cell" );

		Assert.AreEqual( 0, state.CellAt( 11, 10 ).Litter, "the cell with the coordinates the other way round is untouched" );
	}

	/// <summary>Off the map throws, rather than handing back a default that could be written to harmlessly.</summary>
	[TestMethod]
	public void AskingForACellOffTheMapThrows()
	{
		var state = new ParkState( Park() );

		Assert.ThrowsException<ArgumentOutOfRangeException>( () => state.CellAt( -1, 0 ) );
		Assert.ThrowsException<ArgumentOutOfRangeException>( () => state.CellAt( 0, ParkWorld.MapSize ) );
		Assert.IsFalse( ParkState.OnMap( -1, 0 ) );
		Assert.IsTrue( ParkState.OnMap( 0, 0 ) );
	}

	/// <summary>
	/// <b>The one that would catch the layer failing silently.</b> The state handed to a simulation is the
	/// state that simulation writes through - so money taken at the gate lands on the balance the
	/// interface reads. Without this, everything else here would still pass while the park on screen
	/// never changed.
	/// </summary>
	[TestMethod]
	public void TheStateHandedInIsTheStateTheSimulationMoves()
	{
		var world = Park();
		var state = new ParkState( world );
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );

		Assert.AreSame( state, people.State, "the simulation must move the caller's state, not one of its own" );

		state.Take( 7 );
		Assert.AreEqual( 7, people.Takings, "and what the park has taken is visible through both" );

		// And a simulation given nothing still gets one of its own, rather than falling over.
		var alone = new ParkPeople( world );
		Assert.IsNotNull( alone.State, "a park built without one should make its own" );
		Assert.AreNotSame( state, alone.State, "and it should not be somebody else's" );
	}

	/// <summary>
	/// A cell that becomes bare ground, path or queue stamps its 16 x 16 block with a fresh value of the shared
	/// counter (<c>FUN_004d8c60</c>); a write that keeps the type, or makes a footprint, does not.
	/// </summary>
	[TestMethod]
	public void AChangeOfTypeToGroundPathOrQueueStampsTheCellsBlock()
	{
		var state = new ParkState( Park() );
		var path = state.Record( 48, 22 );
		Assert.AreEqual( CellEdge.Path, path.Type, "(48,22) is path in the shipped park" );
		Assert.AreEqual( 0u, state.BlockStamp( 48, 22 ), "a park starts with every stamp nought" );

		state.SetRecord( 48, 22, path with { Neighbours = 0 } );
		Assert.AreEqual( 0u, state.BlockStamp( 48, 22 ), "the same type written again stamps nothing" );

		state.SetRecord( 48, 22, path with { Type = CellEdge.Nothing } );
		var first = state.BlockStamp( 48, 22 );
		Assert.AreNotEqual( 0u, first, "path to bare ground: a value of the counter" );

		state.SetRecord( 48, 22, path with { Type = ParkRideChoice.QueueCellType } );
		var second = state.BlockStamp( 48, 22 );
		Assert.IsTrue( second > first, "bare ground to queue: a newer one" );

		state.SetRecord( 48, 22, path with { Type = CellEdge.Footprint } );
		Assert.AreEqual( second, state.BlockStamp( 48, 22 ), "a footprint is not one of the three writers' types" );

		state.SetRecord( 48, 22, path );
		var third = state.BlockStamp( 48, 22 );
		Assert.IsTrue( third > second, "a footprint made path again" );

		state.SetRecord( 48, 22, path );
		Assert.AreEqual( third, state.BlockStamp( 48, 22 ), "and path written over path stamps nothing" );

		// One block is 16 x 16 cells: (48,16)..(63,31) share (48,22)'s, and (47,22) and (48,32) do not.
		Assert.AreEqual( third, state.BlockStamp( 63, 31 ) );
		Assert.AreEqual( third, state.BlockStamp( 48, 16 ) );
		Assert.AreEqual( 0u, state.BlockStamp( 47, 22 ) );
		Assert.AreEqual( 0u, state.BlockStamp( 48, 32 ) );
		Assert.AreEqual( third, state.BlockStampOf( MapStep.CellId( 48, 22 ) ), "and by packed id, as FUN_004d8ca0 reads it" );
		Assert.AreEqual( third, state.BlockStampOf( MapStep.CellId( 48, 23 ) ), "an odd row reads the same block: three bits of column, no more" );
		Assert.AreEqual( 0u, state.BlockStampOf( MapStep.CellId( 47, 22 ) ) );
		Assert.AreEqual( 0u, state.BlockStampOf( MapStep.CellId( 47, 23 ) ) );
	}

	/// <summary>
	/// <b>Taking the LAST one off a cell's list leaves the one in front with nobody behind.</b>
	/// <c>FUN_004d9280</c> copies the leaver's own next (thing <c>+10</c>) into the previous thing's next, and
	/// for the tail that is nought - <see cref="ParkState.LeaveCell"/>'s doc comment: it joins up whatever
	/// stood either side. Two arrive, 7 then 8, so 8 heads the list with 7 behind; 7 walks away, and 8's
	/// next must read 0, not 7.
	/// </summary>
	[TestMethod]
	public void TheLastOnACellSteppingOffLeavesNobodyBehindTheOneInFront()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		state.StandOn( 7, 10, 10 );
		state.StandOn( 8, 10, 10 );

		Assert.AreEqual( 7, state.NextOnCell( 8 ), "the first to arrive stands behind the second" );

		// The one at the back walks to another cell.
		state.StandOn( 7, 12, 12 );

		Assert.AreEqual( 8, state.CellAt( 10, 10 ).Occupant, "the head is untouched" );
		Assert.AreEqual( 0, state.NextOnCell( 8 ), "and nobody stands behind it any more" );
		Assert.AreEqual( 7, state.CellAt( 12, 12 ).Occupant, "the one who left heads the cell it walked to" );
	}

	/// <summary>
	/// <b>Taking the middle one out links the one behind BACK to the one in front, not only forward.</b>
	/// <c>FUN_004d9280</c> writes the leaver's previous (thing <c>+8</c>) into the next thing's previous, and
	/// only a thing whose previous is nought is treated as the head - <see cref="ParkState.LeaveCell"/>.
	/// Three arrive, 7, 8, 9: the list is 9, 8, 7. 8 leaves, so 7's previous is 9; when 7 then leaves it is
	/// a tail behind 9, and 9's next must read 0. Had 7's back link been dropped it would pass for a head
	/// the cell does not name, and 9 would go on naming it.
	/// </summary>
	[TestMethod]
	public void TheOneBehindAMiddleLeaverIsLinkedBackToTheOneInFront()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		state.StandOn( 7, 10, 10 );
		state.StandOn( 8, 10, 10 );
		state.StandOn( 9, 10, 10 );

		// The middle one walks away, and then the one that stood behind it.
		state.StandOn( 8, 12, 12 );
		Assert.AreEqual( 7, state.NextOnCell( 9 ), "the two either side are joined up" );

		state.StandOn( 7, 14, 14 );

		Assert.AreEqual( 9, state.CellAt( 10, 10 ).Occupant, "the head never moved" );
		Assert.AreEqual( 0, state.NextOnCell( 9 ), "and it is alone on its cell, with nobody behind it" );
		Assert.AreEqual( 7, state.CellAt( 14, 14 ).Occupant, "the last to leave heads the cell it walked to" );
	}

	/// <summary>
	/// <b>A thing taken off its cell keeps no link of its own.</b> <c>FUN_004d9280</c> ends by zeroing the
	/// leaver's previous and next (thing <c>+8</c> and <c>+10</c>) on every path. 8 arrives, then 9, so 9
	/// heads the cell with 8 behind; 9 is taken off the map (<see cref="ParkState.Forget"/>), which
	/// promotes 8 to the head and must leave 9's next at 0, not 8.
	/// </summary>
	[TestMethod]
	public void AThingTakenOffTheMapNamesNobodyBehindIt()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		state.StandOn( 8, 10, 10 );
		state.StandOn( 9, 10, 10 );

		Assert.AreEqual( 8, state.NextOnCell( 9 ), "the earlier arrival stands behind the head" );

		state.Forget( 9 );

		Assert.AreEqual( 8, state.CellAt( 10, 10 ).Occupant, "the one behind becomes the head" );
		Assert.AreEqual( 0, state.NextOnCell( 9 ), "and the one that went no longer names it" );
		Assert.AreEqual( 0, state.NextOnCell( 8 ), "nor does anybody stand behind the new head" );
	}
}
