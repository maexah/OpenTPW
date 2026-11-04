using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The cells' thing lists as a running park keeps them: seeded from the file's chains, and a member of staff relinked
/// as they walk (<c>FUN_0050b6a0</c>, from the walk tick at <c>0x004fa2fc</c>), so a click on the cell a member has
/// walked off finds whoever is left.
/// </summary>
[TestClass]
public class ParkStaffCellListTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>The stock park's guard, saved on (39,28) at the head of its list.</summary>
	private const int Guard = 28;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// Every person of the file is on the list of the cell they stand on from the load, so moving them one cell off
	/// leaves the thing the file chained behind them that cell's head (nought where nothing was), and never them.
	/// </summary>
	[TestMethod]
	public void EveryPersonOfTheFileLeavesTheCellTheyWalkOff()
	{
		var world = Park();
		var people = world.People;

		Assert.AreEqual( 18, people.Count, "the stock park's thirteen guests and five staff" );

		foreach ( var person in people )
		{
			var state = new ParkState( world );
			var (x, y) = (person.CellX, person.CellY);
			var chain = world.ChainAt( x, y );
			var behind = chain.SkipWhile( id => id != person.ThingId ).Skip( 1 ).FirstOrDefault();

			Assert.AreEqual( (x, y), state.CellOf( person.ThingId ), $"thing {person.ThingId} is on its own cell's list" );

			state.StandOn( person.ThingId, x + 1, y );

			Assert.AreNotEqual( person.ThingId, state.CellAt( x, y ).Occupant, $"thing {person.ThingId} walked off ({x},{y})" );
			Assert.AreEqual( chain[0] == person.ThingId ? behind : chain[0], state.CellAt( x, y ).Occupant,
				$"({x},{y}) is headed by what the file has there without thing {person.ThingId}" );
			Assert.AreEqual( person.ThingId, state.CellAt( x + 1, y ).Occupant );
		}
	}

	/// <summary>
	/// A member who walks one cell is relinked into it in the turn that takes them across, and not before: the guard
	/// set walking from his saved (39,28) to the middle of (40,28) heads (39,28) until a turn leaves him standing in
	/// (40,28), and on that very turn (40,28) names him and (39,28) does not. He is walking on that turn (the walk
	/// tick relinks him as he crosses, <c>FUN_004fa2a0</c> calling <c>FUN_0050b6a0</c>). The turn is
	/// <see cref="StaffBehaviour.Step"/> itself, stamped with where he stood first as the sweep does
	/// (<see cref="PeepNavigator.StampPrevious"/>), and no member is put on a cell by hand.
	/// </summary>
	[TestMethod]
	public void AMemberWalkingOneCellIsRelinkedOnTheTurnThatCrossesIt()
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Guard );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with { State = (int)StaffActivity.Walking }, saved.Navigator );
		var blocked = new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked;

		Assert.AreEqual( (39, 28), member.Navigator.Position.Cell );
		Assert.IsTrue( member.Navigator.NavigateTo( new FixedVector( PeepNavigator.WaypointCentre( 40 ), PeepNavigator.WaypointCentre( 28 ) ),
			blocked, addCurrent: false ), "(40,28) is one cell on from where the file has him" );

		var walk = new PeepWalk( member.Navigator, blocked );
		var behaviour = new StaffBehaviour( new ParkBalance( "jungle", easyMode: true ), null, state );
		var crossed = 0;

		for ( int turn = 1; turn <= 60 && crossed == 0; ++turn )
		{
			member.Navigator.StampPrevious();
			behaviour.Step( member, walk, playing: null, tick: 1000 + turn );

			var cell = walk.Position.Cell;

			Assert.AreEqual( cell, state.CellOf( Guard ), $"turn {turn}: the list has him where he stands" );

			if ( cell != (39, 28) )
				crossed = turn;
		}

		Assert.AreNotEqual( 0, crossed, "he crossed into the next cell within sixty turns" );
		Assert.AreEqual( (40, 28), walk.Position.Cell );
		Assert.AreEqual( StaffActivity.Walking, member.Activity, "the crossing turn was a walking turn, not an arrival's decide" );
		Assert.AreNotEqual( Guard, state.CellAt( 39, 28 ).Occupant, "(39,28) no longer names him" );
		Assert.AreEqual( Guard, state.CellAt( 40, 28 ).Occupant, "(40,28) does" );
	}

	/// <summary>
	/// A thing taken out of the middle of a file's chain leaves it joined up either side: (0,0) holds the file's
	/// unplaced things, 15, 12, 11 and on down to 1, and moving any one but the head leaves the rest in the file's order.
	/// </summary>
	[TestMethod]
	public void AThingBehindTheHeadLeavesItsListJoinedUp()
	{
		var world = Park();
		var chain = world.ChainAt( 0, 0 );

		CollectionAssert.AreEqual( new[] { 15, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, chain.ToArray() );

		foreach ( var moved in chain.Skip( 1 ) )
		{
			var state = new ParkState( world );

			state.StandOn( moved, 5, 5 );

			var left = new List<int>();

			for ( int id = state.CellAt( 0, 0 ).Occupant; id != 0 && left.Count <= chain.Count; id = state.NextOnCell( id ) )
				left.Add( id );

			CollectionAssert.AreEqual( chain.Where( id => id != moved ).ToArray(), left.ToArray(), $"thing {moved} moved off" );
		}
	}

	/// <summary>A member who has not moved stays where the list has him: the same turn changes nothing.</summary>
	[TestMethod]
	public void AMemberStandingStillStaysOnTheirList()
	{
		var world = Park();
		var state = new ParkState( world );
		var saved = world.People.Single( person => person.ThingId == Guard );
		var member = new Staff( saved.ThingId, saved.Model, saved.Staff!.Value with { State = (int)StaffActivity.Held }, saved.Navigator );
		var walk = new PeepWalk( member.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked );
		var behind = state.NextOnCell( Guard );

		new StaffBehaviour( new ParkBalance( "jungle", easyMode: true ), null, state ).Step( member, walk, playing: null, tick: 1000 );

		Assert.AreEqual( Guard, state.CellAt( 39, 28 ).Occupant );
		Assert.AreEqual( behind, state.NextOnCell( Guard ) );
		Assert.AreNotEqual( Guard, behind, "and is never linked behind himself" );
	}
}
