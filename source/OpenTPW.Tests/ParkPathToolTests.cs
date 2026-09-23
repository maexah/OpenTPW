using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The path tool as the player holds it - its preview (<see cref="ParkPathBuilding.PathStrip"/>), a run
/// (<see cref="ParkPathBuilding.LayPathRun"/>), Backspace's undo and the clear
/// (<see cref="ParkPathBuilding.ClearPathCell"/>) - on the shipped Lost Kingdom park, and the two save
/// fields they read. See <c>docs/exe/park-engine.md</c>, "The path tool".
/// </summary>
/// <remarks>
/// <b>(10..13, 10..12) is bare ground inside the park</b> (measured), away from the park's own walkways at
/// x 39..57, y 15..29.
/// </remarks>
[TestClass]
public class ParkPathToolTests
{
	private const int Price = 20;

	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame()
	{
		data = GameData.Required();
		ParkBuildMode.Forget();
	}

	[TestCleanup]
	public void ForgetTheTool() => ParkBuildMode.Forget();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The save's <c>mOverlapCounter</c> is the stamp's re-stamp count: <b>14 path cells carry it, all at
	/// corners and junctions</b>, and the queue's node at (52,22) carries 1.
	/// </summary>
	[TestMethod]
	public void TheOverlapCounterSitsOnTheParksCornersAndJunctions()
	{
		var park = World();
		var counted = new Dictionary<(int, int), int>();

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var cell = park.CellAt( x, y );

				if ( cell.Type == CellEdge.Path && cell.OverlapCounter != 0 )
					counted[(x, y)] = cell.OverlapCounter;
			}
		}

		var once = new[] { (39, 21), (39, 28), (43, 29), (44, 28), (47, 28), (48, 20), (56, 15), (56, 16), (56, 17), (56, 21), (56, 28) };
		var twice = new[] { (47, 21), (48, 21), (48, 28) };

		CollectionAssert.AreEquivalent( once.Concat( twice ).ToArray(), counted.Keys.ToArray() );
		Assert.IsTrue( once.All( at => counted[at] == 1 ) && twice.All( at => counted[at] == 2 ) );
		Assert.AreEqual( 1, park.CellAt( 52, 22 ).OverlapCounter, "the queue's node" );
	}

	/// <summary>
	/// <b>Land outside the park carries flag <c>0x40</c></b> - 13,878 of the 16,384 cells, and none that
	/// hold a path, a queue or a thing.
	/// </summary>
	[TestMethod]
	public void OutsideTheParkIsFlaggedAndNothingBuiltIsOnIt()
	{
		var park = World();
		var outside = 0;
		var built = 0;
		var tracksLinked = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var cell = park.CellAt( x, y );

				if ( (cell.Flags & ParkPathBuilding.OutsideThePark) == 0 )
					continue;

				++outside;

				if ( cell.Type is CellEdge.Path or ParkRideChoice.QueueCellType or CellEdge.Footprint
					or CellEdge.RideEnd or CellEdge.RideFarEnd )
					++built;
			}
		}

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( park.CellAt( x, y ).TrackNeighbours != 0 )
					++tracksLinked;
			}
		}

		Assert.AreEqual( 13_878, outside );
		Assert.AreEqual( 0, built );
		Assert.AreEqual( 0, tracksLinked, "every track record's links are empty, so the corner rule never fires here" );
	}

	/// <summary>
	/// With nothing anchored the preview is one square under the pointer, and it is both the first cell
	/// and the last: over path that is <c>m_end</c>.
	/// </summary>
	[TestMethod]
	public void UnanchoredThePreviewIsOneSquare()
	{
		var park = World();
		var state = new ParkState( park );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		var bare = ParkPathBuilding.PathStrip( state, park, 42, 24, Price );
		var path = ParkPathBuilding.PathStrip( state, park, 39, 24, Price );
		var outside = ParkPathBuilding.PathStrip( state, park, 43, 16, Price );

		Assert.AreEqual( "(42,24):0", Describe( bare ) );
		Assert.AreEqual( "(39,24):11", Describe( path ) );
		Assert.AreEqual( "(43,16):1", Describe( outside ) );
	}

	/// <summary>
	/// <b>The outside-the-park test comes first</b>, before a thing's cells are looked at, and it does not
	/// latch: the squares after it are judged on their own. A thing's cell does latch.
	/// </summary>
	[TestMethod]
	public void OutsideTheParkComesFirstAndDoesNotLatch()
	{
		var park = World();
		var state = new ParkState( park );

		// (11,10) outside the park AND a ride's footprint: the verdict names the land.
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with
		{
			Type = CellEdge.Footprint,
			Flags = ParkPathBuilding.OutsideThePark
		} );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.AnchorAt( 10, 10 );

		var strip = ParkPathBuilding.PathStrip( state, park, 13, 10, Price );

		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):0 (13,10):0", Describe( strip ) );
		Assert.AreEqual( "is outside the park", strip[1].Why );

		// Only a footprint: red, and every square after it too.
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Flags = 0 } );

		strip = ParkPathBuilding.PathStrip( state, park, 13, 10, Price );

		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1 (13,10):1", Describe( strip ) );
		Assert.AreEqual( "comes after a refused square", strip[2].Why );
	}

	/// <summary>
	/// <b>A path as the last square is <c>m_end</c> before the track-corner test is asked</b>; in the
	/// middle of a run the same cell is refused by it.
	/// </summary>
	[TestMethod]
	public void EndingOnAPathIsAskedBeforeTheTrackCorner()
	{
		var park = World();
		var state = new ParkState( park );

		state.SetRecord( 12, 10, ParkState.CellFor( park, 12, 10 ) with { Type = CellEdge.Path, TrackNeighbours = 0x05 } );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.AnchorAt( 10, 10 );

		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):11", Describe( ParkPathBuilding.PathStrip( state, park, 12, 10, Price ) ) );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1",
			Describe( ParkPathBuilding.PathStrip( state, park, 13, 10, Price ) ) );
	}

	/// <summary>A track corner is two cardinal links at right angles, a junction three or more.</summary>
	[TestMethod]
	public void ATrackCornerIsTwoLinksAtRightAnglesAndAJunctionThree()
	{
		static bool Asks( byte links ) => ParkPathBuilding.TrackCornerOrJunction( new ParkWorld.MapCell() with { TrackNeighbours = links } );

		Assert.IsTrue( Asks( 0x05 ), "north and east" );
		Assert.IsTrue( Asks( 0x15 ), "a junction" );
		Assert.IsFalse( Asks( 0x11 ), "north and south is a straight" );
		Assert.IsFalse( Asks( 0x44 ), "and so is east and west" );
		Assert.IsFalse( Asks( 0x01 ) );
		Assert.IsFalse( Asks( 0x0a ), "the diagonals are not counted" );
	}

	/// <summary>
	/// <b>A queue may be paved only at a loose end</b>, one link and that to another queue cell, which
	/// shows <c>m_link</c>; any other queue cell refuses and latches. Whose queue it is is never asked.
	/// </summary>
	[TestMethod]
	public void OnlyALooseQueueEndMayBePaved()
	{
		var park = World();
		var state = new ParkState( park );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x10 } );
		state.SetRecord( 11, 11, ParkState.CellFor( park, 11, 11 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x11 } );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.AnchorAt( 10, 10 );

		Assert.AreEqual( "(10,10):0 (11,10):8 (12,10):0", Describe( ParkPathBuilding.PathStrip( state, park, 12, 10, Price ) ) );

		ParkBuildMode.AnchorAt( 10, 11 );

		Assert.AreEqual( "(10,11):0 (11,11):1 (12,11):1", Describe( ParkPathBuilding.PathStrip( state, park, 12, 11, Price ) ) );
	}

	/// <summary>
	/// The run is paid for cell by cell: <b>the first square the balance cannot cover is red with the cash
	/// cursor's flag</b>, and every one after it is red. A cell already path is free.
	/// </summary>
	[TestMethod]
	public void TheRunIsRefusedWhereTheMoneyRunsOut()
	{
		var park = World();
		var state = new ParkState( park );

		state.Spend( state.Balance - 30 );
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = CellEdge.Path } );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.AnchorAt( 10, 10 );

		var strip = ParkPathBuilding.PathStrip( state, park, 13, 10, Price );

		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1", Describe( strip ),
			"20 for (10,10), nothing for the path, and 40 is more than 30" );
		Assert.IsTrue( strip[2].Unaffordable );
	}

	/// <summary>
	/// A run is laid one pass at a time over the whole line: every cell stamped and paid for, joined, given
	/// the flow of the step, and retiled. <b>A path it crosses or ends on costs nothing and counts once
	/// more</b>, and ending on one is what puts the tool away.
	/// </summary>
	[TestMethod]
	public void ARunIsLaidPassByPassAndCountsWhatItCrosses()
	{
		var park = World();
		var state = new ParkState( park );
		var before = state.Balance;

		var (laid, ended) = ParkPathBuilding.LayPathRun( state, park, 10, 10, 13, 10, Price );

		Assert.AreEqual( (4, false), (laid, ended) );
		Assert.AreEqual( before - 80, state.Balance );
		Assert.IsTrue( Enumerable.Range( 10, 4 ).All( x => ParkState.CellFor( park, x, 10 ).Type == CellEdge.Path ) );
		Assert.AreEqual( 0x04, ParkState.CellFor( park, 10, 10 ).Neighbours & 0x55, "the first cell joins east" );
		Assert.AreEqual( 0x44, ParkState.CellFor( park, 11, 10 ).Neighbours & 0x55 );
		Assert.IsTrue( Enumerable.Range( 10, 4 ).All( x => ParkState.CellFor( park, x, 10 ).Direction == 0x40 ),
			"a step east flows 0x40, the opposite" );

		(laid, ended) = ParkPathBuilding.LayPathRun( state, park, 13, 10, 13, 12, Price );

		Assert.AreEqual( (2, false), (laid, ended), "the corner is already path" );
		Assert.AreEqual( before - 120, state.Balance );
		Assert.AreEqual( 1, ParkState.CellFor( park, 13, 10 ).OverlapCounter, "the corner was stamped again" );
		Assert.AreEqual( 0x40, ParkState.CellFor( park, 13, 10 ).Direction, "a flow already set is kept" );
		Assert.AreEqual( 0x50, ParkState.CellFor( park, 13, 10 ).Neighbours & 0x55 );
		Assert.AreEqual( 0x01, ParkState.CellFor( park, 13, 12 ).Direction, "a step south flows 0x01" );

		(laid, ended) = ParkPathBuilding.LayPathRun( state, park, 11, 12, 13, 12, Price );

		Assert.AreEqual( (2, true), (laid, ended), "ending on a path" );
	}

	/// <summary>A run of one cell - a click on the anchor straight after anchoring - flows 0x01.</summary>
	[TestMethod]
	public void AOneCellRunFlowsSouth()
	{
		var park = World();
		var state = new ParkState( park );

		Assert.AreEqual( (1, false), ParkPathBuilding.LayPathRun( state, park, 10, 10, 10, 10, Price ) );
		Assert.AreEqual( 0x01, ParkState.CellFor( park, 10, 10 ).Direction );
	}

	/// <summary>
	/// <b>Backspace takes up what a run laid and leaves what it crossed</b>: every cell of the line gets one
	/// press of the clear, and a cell stamped twice survives one.
	/// </summary>
	[TestMethod]
	public void BackspaceTakesUpTheRunAndKeepsTheCornerItCrossed()
	{
		var park = World();
		var state = new ParkState( park );

		ParkPathBuilding.LayPathRun( state, park, 10, 10, 13, 10, Price );
		ParkPathBuilding.LayPathRun( state, park, 13, 10, 13, 12, Price );
		var balance = state.Balance;

		Assert.AreEqual( 2, ParkPathBuilding.UndoRun( state, park, (13, 12), (13, 10) ) );

		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 13, 11 ).Type );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 13, 12 ).Type );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( park, 13, 10 ).Type, "the corner stays" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 13, 10 ).OverlapCounter );
		Assert.AreEqual( 0x40, ParkState.CellFor( park, 13, 10 ).Neighbours & 0x55, "and lets go of the south" );
		Assert.AreEqual( balance, state.Balance, "nothing is refunded" );

		Assert.AreEqual( 4, ParkPathBuilding.UndoRun( state, park, (13, 10), (10, 10) ) );
		Assert.IsTrue( Enumerable.Range( 10, 4 ).All( x => ParkState.CellFor( park, x, 10 ).Type == CellEdge.Nothing ) );
	}

	/// <summary>
	/// One press of the clear: <b>a cell goes only once its counter falls below nought</b>, an unstepped
	/// press sets it straight to −1, and NOMODIFY keeps a joined-up cell while letting a lone one go.
	/// </summary>
	[TestMethod]
	public void AClearGoesThroughTheCounterAndNoModify()
	{
		var park = World();
		var state = new ParkState( park );

		state.SetRecord( 10, 10, ParkState.CellFor( park, 10, 10 ) with { Type = CellEdge.Path, OverlapCounter = 2 } );

		Assert.IsFalse( ParkPathBuilding.ClearPathCell( state, park, 10, 10, stepped: true ) );
		Assert.IsFalse( ParkPathBuilding.ClearPathCell( state, park, 10, 10, stepped: true ) );
		Assert.IsTrue( ParkPathBuilding.ClearPathCell( state, park, 10, 10, stepped: true ), "the third press" );

		state.SetRecord( 10, 10, ParkState.CellFor( park, 10, 10 ) with { Type = CellEdge.Path, OverlapCounter = 2 } );

		Assert.IsTrue( ParkPathBuilding.ClearPathCell( state, park, 10, 10, stepped: false ), "unstepped goes at once" );

		// The park's own avenue at (47,19): NOMODIFY and joined, so it never goes.
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 47, 19 ).Flags & ParkPathBuilding.NoModify );
		Assert.IsFalse( ParkPathBuilding.ClearPathCell( state, park, 47, 19, stepped: false ) );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( park, 47, 19 ).Type );

		state.SetRecord( 10, 10, ParkState.CellFor( park, 10, 10 ) with { Type = CellEdge.Path, Flags = ParkPathBuilding.NoModify } );

		Assert.IsTrue( ParkPathBuilding.ClearPathCell( state, park, 10, 10, stepped: true ), "a lone NOMODIFY cell goes" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 10, 10 ).Flags );
	}

	private static string Describe( IEnumerable<ParkPathBuilding.QueueSquare> strip )
		=> string.Join( " ", strip.Select( square => $"({square.X},{square.Y}):{square.Marker}" ) );
}
