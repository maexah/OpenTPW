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
		Assert.AreEqual( 26, state.CellAt( 10, 11 ).LitterCollector, "and so should the handyman who claimed it" );
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
	/// A cell that becomes bare ground, path or queue is counted once, where the original stamps the cell's
	/// 16 x 16 block (<c>FUN_004d8c60</c>); a write that keeps the type, or makes a footprint, is not.
	/// </summary>
	[TestMethod]
	public void AChangeOfTypeToGroundPathOrQueueCountsABlockStamp()
	{
		var state = new ParkState( Park() );
		var path = state.Record( 48, 22 );
		Assert.AreEqual( CellEdge.Path, path.Type, "(48,22) is path in the shipped park" );

		var before = Stamps();
		state.SetRecord( 48, 22, path with { Neighbours = 0 } );
		Assert.AreEqual( before, Stamps(), "the same type written again stamps nothing" );

		state.SetRecord( 48, 22, path with { Type = CellEdge.Nothing } );
		Assert.AreEqual( before + 1, Stamps(), "path to bare ground" );

		state.SetRecord( 48, 22, path with { Type = ParkRideChoice.QueueCellType } );
		Assert.AreEqual( before + 2, Stamps(), "bare ground to queue" );

		state.SetRecord( 48, 22, path with { Type = CellEdge.Footprint } );
		Assert.AreEqual( before + 2, Stamps(), "a footprint is not one of the three writers' types" );

		state.ClearRecord( 48, 22 );
		Assert.AreEqual( before + 3, Stamps(), "giving the cell back to the save's path" );

		state.ClearRecord( 48, 22 );
		Assert.AreEqual( before + 3, Stamps(), "and a cell already the save's stamps nothing" );
	}

	private static int Stamps() =>
		Unimplemented.Summary.FirstOrDefault( gap => gap.What == "MAP_TYPE_WRITE_BLOCK_STAMP" ).Times;
}
