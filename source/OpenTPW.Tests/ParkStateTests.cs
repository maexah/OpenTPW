using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// The park as it is being played - <see cref="ParkState"/>, which is the layer four separate features
/// were each waiting on.
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
	/// Taking a fee moves the balance <b>and</b> the running total, which is the one place the two move
	/// together - the original adds a fee to <c>mBalance</c> and <c>mProfitThisYear</c> alike.
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
}
