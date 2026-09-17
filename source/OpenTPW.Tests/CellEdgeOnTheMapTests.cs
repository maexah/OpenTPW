using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The edge test bound to a park that is really loaded - <see cref="CellEdge.For"/> against Lost Kingdom.
///
/// <para>
/// <see cref="CellEdgeTests"/> is deliberately synthetic: every cell in it is made up, so each test can
/// name exactly which two types meet at the edge it is about. That is the right shape for the ladder of
/// type comparisons, and it is kept. What it cannot do is say whether the thing answers sensibly about
/// real geography, and one of its three questions - the track record - could not be answered at all until
/// the binding existed. These read real game files and are skipped where there is no installation.
/// </para>
/// <para>
/// <b>The numbers below were derived before they were run, not copied off a measurement.</b> The class
/// remarks on <see cref="CellEdge"/> record 429 cells of track type 12, each naming one of 143 cells of
/// type 25, three apiece; that is 572 carrying a type the branch acts on. Exactly one cell carries the
/// low nibble that reopens it, and if that cell is a type 25 parent it frees itself and its three
/// children - which is precisely the documented 568 closed. Either that whole account holds together on
/// the real file or it does not.
/// </para>
/// </summary>
[TestClass]
public class CellEdgeOnTheMapTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>
	/// Through a stream of this test's own bytes rather than the global file system, which belongs to a
	/// running game - the same rule the other park tests follow.
	/// </summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The two track types the branch acts on, counted over the whole map, and the tree they form. A
	/// deferring cell whose parent is not what it claims would still produce a plausible answer, so the
	/// shape is pinned and not only the totals.
	/// </summary>
	[TestMethod]
	public void TheTrackRecordsFormTheTwoLevelTreeTheBranchExpects()
	{
		var world = World();

		Assert.AreEqual( 128 * 128, world.Cells.Count, "the map is the whole grid" );

		var defers = world.Cells.Where( cell => CellEdge.TrackDefersToParent( cell.TrackType ) ).ToList();
		var counts = world.Cells.Where( cell => CellEdge.TrackCounts( cell.TrackType ) ).ToList();

		Assert.AreEqual( 429, defers.Count, "cells that hand the question to a parent" );
		Assert.AreEqual( 143, counts.Count, "cells that answer it themselves" );
		Assert.AreEqual( 572, defers.Count + counts.Count, "cells the branch reaches at all" );

		// Only one of the five types each branch knows about actually occurs here.
		Assert.IsTrue( defers.All( cell => cell.TrackType == 12 ), "every deferring cell is type 12" );
		Assert.IsTrue( counts.All( cell => cell.TrackType == 25 ), "every answering cell is type 25" );

		// A type 25 names no parent: it is the top of the tree.
		Assert.IsTrue( counts.All( cell => cell.TrackParentId == 0 ), "no answering cell names a parent" );

		// And every deferring cell names one that really is an answering cell - three apiece.
		var parents = defers.GroupBy( cell => cell.TrackParentId ).ToList();

		Assert.AreEqual( 143, parents.Count, "distinct parents named" );
		Assert.IsTrue( parents.All( group => group.Count() == 3 ), "each parent is named three times" );

		foreach ( var group in parents )
		{
			var (x, y) = MapStep.CellAt( group.Key );

			Assert.AreEqual( 25, world.CellAt( x, y ).TrackType,
				$"cell {group.Key} is named as a parent and should be an answering cell" );
		}
	}

	/// <summary>
	/// What the branch actually decides, over every cell of the real map: 568 closed of the 572 it
	/// reaches, and the four it lets through are the one flagged cell and the three that defer to it.
	/// </summary>
	[TestMethod]
	public void TheTrackRecordClosesAllButTheOneFlaggedFamily()
	{
		var world = World();

		var edge = CellEdge.For( world, 0 );

		Assert.IsNotNull( edge, "a park that is loaded should give an edge test" );

		var byId = ( int id ) =>
		{
			var (x, y) = MapStep.CellAt( id );

			return world.CellAt( x, y );
		};

		var reached = world.Cells
			.Where( cell => CellEdge.TrackDefersToParent( cell.TrackType )
				|| CellEdge.TrackCounts( cell.TrackType ) )
			.ToList();

		var closed = reached.Count( cell => CellEdge.TrackCloses( cell, byId ) );

		Assert.AreEqual( 572, reached.Count, "cells the branch reaches" );
		Assert.AreEqual( 568, closed, "of those, the ones it closes" );

		// The four it does not close are one flagged parent and its three children.
		var flagged = world.Cells
			.Where( cell => CellEdge.TrackCounts( cell.TrackType )
				&& (cell.TrackFlags & CellEdge.TrackOpenFlags) != 0 )
			.ToList();

		Assert.AreEqual( 1, flagged.Count, "exactly one cell carries the reopening nibble" );
		Assert.AreEqual( 4, reached.Count - closed, "the flagged cell and the three that defer to it" );
	}

	/// <summary>
	/// The boundary still closes every cell on it once bound to a real park, which is the guard everything
	/// that walks leans on. Pinned here as well as in the synthetic tests because the binding replaces the
	/// map lookup, and a binding that read the grid wrongly could answer this one right by accident only if
	/// the boundary were still tested first - which is exactly the ordering worth holding.
	/// </summary>
	[TestMethod]
	public void TheEdgeOfTheRealMapIsStillClosed()
	{
		var edge = CellEdge.For( World(), 0 );

		Assert.IsTrue( edge.Blocked( 0, 10, StepDirection.West ), "west from the first column" );
		Assert.IsTrue( edge.Blocked( 10, 0, StepDirection.North ), "north from the first row" );
		Assert.IsTrue( edge.Blocked( 127, 10, StepDirection.East ), "east from the last column" );
		Assert.IsTrue( edge.Blocked( 10, 127, StepDirection.South ), "south from the last row" );
	}

	/// <summary>
	/// The park's path network is walkable along itself. This is the crudest possible check and it is worth
	/// having: a binding that read the grid transposed would still answer plausibly everywhere else, and
	/// Lost Kingdom's paths are not symmetric about the diagonal.
	/// </summary>
	[TestMethod]
	public void EveryStepFromOnePathCellToTheNextIsOpen()
	{
		var world = World();
		var edge = CellEdge.For( world, 0 );

		var paths = 0;
		var open = 0;

		for ( var y = 0; y < 128; ++y )
		{
			for ( var x = 0; x < 128; ++x )
			{
				if ( world.CellAt( x, y ).Type != CellEdge.Path )
					continue;

				++paths;

				foreach ( var direction in new[] { StepDirection.North, StepDirection.East,
					StepDirection.South, StepDirection.West } )
				{
					var (toX, toY) = MapStep.Beyond( x, y, direction );

					if ( MapStep.LeavesTheMap( x, y, direction )
						|| world.CellAt( toX, toY ).Type != CellEdge.Path )
						continue;

					++open;

					Assert.IsFalse( edge.Blocked( x, y, direction ),
						$"path ({x},{y}) to path ({toX},{toY}) going {direction}" );
				}
			}
		}

		// The anti-vacuity guard: the loop above proves nothing if it never found a pair.
		Assert.AreEqual( 78, paths, "path cells in Lost Kingdom" );
		Assert.IsTrue( open > 100, $"only {open} path-to-path steps were tested" );
	}

	/// <summary>
	/// <b>That binding a park actually changes an answer.</b> Everything above could pass with
	/// <see cref="CellEdge.For"/> quietly not supplying the track question at all: the closure test calls
	/// <see cref="CellEdge.TrackCloses"/> directly, which proves the rule works and says nothing about
	/// whether the factory wires it in. This is the test that pins the wiring, and it exists because
	/// designing a control run against the factory showed the others would not have caught it.
	///
	/// <para>
	/// The two edges differ in exactly one respect - one was given the park's track records and the other
	/// was not - so every edge of every cell where they disagree is a step the unbound form gets wrong.
	/// </para>
	/// </summary>
	[TestMethod]
	public void BindingTheParkChangesWhatTheEdgeTestAnswers()
	{
		var world = World();

		var bound = CellEdge.For( world, 0 );
		var unbound = new CellEdge( world.CellAt, 0 );

		var disagreements = 0;

		for ( var y = 0; y < 128; ++y )
		{
			for ( var x = 0; x < 128; ++x )
			{
				foreach ( var direction in new[] { StepDirection.North, StepDirection.East,
					StepDirection.South, StepDirection.West } )
				{
					var boundSays = bound.Blocked( x, y, direction );
					var unboundSays = unbound.Blocked( x, y, direction );

					if ( boundSays == unboundSays )
						continue;

					++disagreements;

					// The track record can only ever shut a way, never open one. A binding that got the
					// sense backwards would still disagree in as many places.
					Assert.IsTrue( boundSays,
						$"({x},{y}) going {direction} was OPENED by binding a park, which it cannot do" );
				}
			}
		}

		Assert.AreEqual( 2265, disagreements, "edges where the track record changes the answer" );

		// Not a magic number. The branch closes 568 cells, and closing a cell shuts the four ways into it,
		// which would be 2,272. Seven of those steps were already refused for some other reason, so
		// binding the park changed nothing about them.
		//
		// That shortfall is observed and NOT explained: which seven they are has not been established, and
		// nothing here claims otherwise. The bound below is what can honestly be asserted about it.
		Assert.IsTrue( disagreements <= 568 * 4,
			"no more ways can be shut than there are ways into the cells that close" );
	}
}
