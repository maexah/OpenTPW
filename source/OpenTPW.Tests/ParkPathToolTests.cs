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
	/// <b>The refusal latch a pass leaves</b> (<c>DAT_00816d50</c>, which a followed member's patrol edges read): set by a
	/// thing's cell, not by land outside the park; a type <c>0x15</c> cell sets <c>DAT_00816d58</c>, which the red square's
	/// own second verdict (<c>0x00533927</c>) copies into it (<c>0x00535885</c>), so it latches last in the run too, and
	/// before a square outside the park. In the original (<c>q532/orig/a-run.out</c>) the run from (49,24) onto the Belly
	/// Bounce's (51,24) left <c>d50</c> 1 and (43,16) outside the park left it 0.
	/// </summary>
	/// <remarks><b>Mutations:</b> any red square latching; the second verdict's copy dropped.</remarks>
	[TestMethod]
	public void APathPassLeavesTheRefusalLatchOnlyWhereItsVerdictsSetIt()
	{
		var park = World();
		var state = new ParkState( park );
		ParkBuildMode.Arm( ParkBuildMode.Path );

		bool Latched( int toX )
		{
			ParkPathBuilding.PathStrip( state, park, toX, 10, Price, out var latched );
			return latched;
		}

		ParkBuildMode.AnchorAt( 10, 10 );
		Assert.IsFalse( Latched( 13 ), "bare ground" );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Flags = ParkPathBuilding.OutsideThePark } );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):0 (13,10):0", Describe( ParkPathBuilding.PathStrip( state, park, 13, 10, Price ) ) );
		Assert.IsFalse( Latched( 13 ), "red outside the park, but not latched" );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = CellEdge.Footprint, Flags = 0 } );
		Assert.IsTrue( Latched( 11 ), "a thing's cell, last" );
		Assert.IsTrue( Latched( 13 ), "a thing's cell, then bare ground" );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = 0, Flags = 0 } );
		state.SetRecord( 12, 10, ParkState.CellFor( park, 12, 10 ) with { Type = 0x15 } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1", Describe( ParkPathBuilding.PathStrip( state, park, 12, 10, Price ) ) );
		Assert.IsTrue( Latched( 12 ), "a type 0x15 cell last: its own second verdict copies d58" );

		state.SetRecord( 13, 10, ParkState.CellFor( park, 13, 10 ) with { Flags = ParkPathBuilding.OutsideThePark } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1", Describe( ParkPathBuilding.PathStrip( state, park, 13, 10, Price ) ) );
		Assert.IsTrue( Latched( 13 ), "copied at (12,10), before the square outside the park" );
	}

	/// <summary>
	/// <b>The queue tool's pass leaves the latch as its verdict sets it</b> (<c>FUN_00535670</c>'s mode-3 arm): a thing's
	/// cell, NOMODIFY or not, a track, the money, the middle of its own queue and another ride's set <c>d50</c>; a NOMODIFY
	/// path, a type <c>0x15</c> cell and a track corner, the run's last path included, set <c>d58</c>, which the red
	/// square's second verdict copies; land outside the park, a thing's cell there included, and the join set neither,
	/// so the squares after them take their own verdicts. NOMODIFY bare ground is blue.
	/// </summary>
	/// <remarks><b>Mutations:</b> any red square latching; the second verdict's copy dropped; the join latching; the
	/// middle of the queue not latching; NOMODIFY read on bare ground; the NOMODIFY thing not latching; a thing tested
	/// before the outside flag; the join answered before the corner test.</remarks>
	[TestMethod]
	public void AQueuePassLeavesTheRefusalLatchOnlyWhereItsVerdictsSetIt()
	{
		var park = World();
		var state = new ParkState( park );
		var original = Enumerable.Range( 10, 4 ).ToDictionary( x => x, x => ParkState.CellFor( park, x, 10 ) );
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, 999, 10, 10 );

		string Strip( int toX, out bool latched )
			=> Describe( ParkPathBuilding.QueueStrip( state, park, toX, 10, Price, out latched ) );

		void Set( int x, ParkWorld.MapCell cell ) => state.SetRecord( x, 10, cell );

		void Reset()
		{
			foreach ( var (x, cell) in original )
				Set( x, cell );
		}

		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):0 (13,10):0", Strip( 13, out var latched ), "bare ground" );
		Assert.IsFalse( latched );

		Set( 11, original[11] with { Flags = ParkPathBuilding.OutsideThePark } );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):0 (13,10):0", Strip( 13, out latched ), "outside: the next squares their own" );
		Assert.IsFalse( latched, "outside the park latches nothing" );

		Set( 11, original[11] with { Type = CellEdge.Footprint, Flags = ParkPathBuilding.OutsideThePark } );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):0 (13,10):0", Strip( 13, out latched ), "a thing outside the park" );
		Assert.IsFalse( latched, "the outside flag answers before the thing" );

		Set( 11, original[11] with { Type = CellEdge.Footprint, Flags = ParkPathBuilding.NoModify } );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1 (13,10):1", Strip( 13, out latched ), "a NOMODIFY thing" );
		Assert.IsTrue( latched, "a NOMODIFY thing latches as any thing does in mode 3" );

		Set( 11, original[11] with { Type = CellEdge.Footprint, Flags = 0 } );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1 (13,10):1", Strip( 13, out latched ), "a thing" );
		Assert.IsTrue( latched, "a thing's cell" );

		Set( 11, original[11] with { Type = 0, Flags = ParkPathBuilding.NoModify } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):0", Strip( 12, out latched ), "NOMODIFY bare ground is blue" );
		Assert.IsFalse( latched );

		Set( 11, original[11] with { Type = CellEdge.Path, Flags = ParkPathBuilding.NoModify } );
		Assert.AreEqual( "(10,10):0 (11,10):8", Strip( 11, out latched ), "the join, NOMODIFY or not" );
		Assert.IsFalse( latched, "the join sets only d58" );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1", Strip( 12, out latched ), "a NOMODIFY path in the run" );
		Assert.IsTrue( latched, "d58, copied" );

		Set( 12, original[12] with { Flags = ParkPathBuilding.OutsideThePark } );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1", Strip( 12, out latched ) );
		Assert.IsTrue( latched, "copied by (11,10)'s own second verdict, before the square outside the park" );

		Reset();
		Set( 12, original[12] with { Type = 0x15 } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1", Strip( 12, out latched ) );
		Assert.IsTrue( latched, "a type 0x15 cell last" );

		Reset();
		Set( 12, original[12] with { Type = CellEdge.Path, TrackNeighbours = 0x05 } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1", Strip( 13, out latched ), "a track corner" );
		Assert.IsTrue( latched );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1", Strip( 12, out latched ), "a track corner under the run's last path" );
		Assert.IsTrue( latched, "the corner is asked before the join" );

		Reset();
		Set( 12, original[12] with { TrackType = 0x19 } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1", Strip( 13, out latched ), "a track" );
		Assert.IsTrue( latched );

		Reset();
		Set( 12, original[12] with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x11 } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1", Strip( 13, out latched ) );
		Assert.IsTrue( latched, "the middle of a queue" );

		Set( 12, original[12] with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01 } );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1", Strip( 12, out latched ) );
		Assert.IsTrue( latched, "another ride's queue end" );

		Reset();
		var ride = state.Objects[0];
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, ride.ThingId, 10, 10 );
		Set( 12, original[12] with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01, ParentId = (ushort)((ride.CellY * 128) + ride.CellX + 1) } );
		state.Spend( state.Balance - (2 * Price) );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):11", Strip( 12, out latched ), "its own queue's end is not paid for" );
		Assert.IsFalse( latched );

		Reset();
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1", Strip( 13, out latched ), "the third cell unpaid for" );
		Assert.IsTrue( latched, "the money" );
	}

	/// <summary>
	/// <b>The money is tested only once something is owed</b> (<c>0x0053590a</c>, in both of a red square's verdicts): with
	/// the balance below nought the queue tool's anchor on its own queue and the join to a path pass, a red square that
	/// owes nothing keeps <c>c_noplace</c> rather than <c>c_cash</c>, and the path tool may still end on a path.
	/// </summary>
	/// <remarks><b>Mutations:</b> the money tested with nothing owed (either first verdict, or the second); a red square
	/// flagged unaffordable by the balance alone.</remarks>
	[TestMethod]
	public void AParkInTheRedIsRefusedForMoneyOnlyWhereSomethingIsOwed()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = state.Objects[0];
		var rideCell = (ushort)((ride.CellY * 128) + ride.CellX + 1);

		state.Spend( state.Balance + 100 );
		Assert.IsTrue( state.Balance < 0 );

		ParkBuildMode.ArmAt( ParkBuildMode.Queue, ride.ThingId, 10, 10 );
		state.SetRecord( 10, 10, ParkState.CellFor( park, 10, 10 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01, ParentId = rideCell } );
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = CellEdge.Path, Flags = 0 } );

		var strip = ParkPathBuilding.QueueStrip( state, park, 11, 10, Price, out var latched );
		Assert.AreEqual( "(10,10):0 (11,10):8", Describe( strip ), "its own queue's end, then the join: nothing owed" );
		Assert.IsFalse( latched );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01, ParentId = 0 } );

		strip = ParkPathBuilding.QueueStrip( state, park, 11, 10, Price, out latched );
		Assert.AreEqual( "(10,10):0 (11,10):1", Describe( strip ), "another ride's queue end" );
		Assert.IsTrue( latched );
		Assert.IsFalse( strip.Any( square => square.Unaffordable ), "red for the queue, not for the money: a queue is not charged in mode 3" );

		ParkBuildMode.Forget();
		ParkBuildMode.Arm( ParkBuildMode.Path );
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = CellEdge.Path, Neighbours = 0, ParentId = 0 } );

		Assert.AreEqual( "(11,10):11", Describe( ParkPathBuilding.PathStrip( state, park, 11, 10, Price ) ), "one square on a path is free" );
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

	/// <summary>
	/// <b>A red square's second verdict charges it again</b> (<c>0x005358f4</c>) and asks the money once more: a square
	/// after a latch is paid for by it alone, one the first call charged (a corner on bare ground) twice, and a run that
	/// passes the balance is refused for money (<c>DAT_00816d5c</c>, <c>c_cash</c>) where the squares alone would only be
	/// red. Land outside the park and a thing's cell are not charged, the queue tool pays for bare ground alone, and the
	/// money is not asked with nothing owed.
	/// </summary>
	/// <remarks><b>Mutations:</b> the second charge dropped; a thing's cell charged; land outside the park charged; the
	/// path tool's rule in the queue tool; the money asked with nothing owed; the second test's refusal not reported.</remarks>
	[TestMethod]
	public void ARedSquaresSecondVerdictChargesItAgain()
	{
		var park = World();
		var state = new ParkState( park );
		var original = Enumerable.Range( 10, 5 ).ToDictionary( x => x, x => ParkState.CellFor( park, x, 10 ) );

		void Set( int x, ParkWorld.MapCell cell ) => state.SetRecord( x, 10, cell );

		void Reset()
		{
			foreach ( var (x, cell) in original )
				Set( x, cell );
		}

		void HoldTheBalanceAt( int balance ) => state.Spend( state.Balance - balance );

		string Strip( List<ParkPathBuilding.QueueSquare> strip )
			=> string.Join( " ", strip.Select( square => $"({square.X},{square.Y}):{square.Marker}{(square.Unaffordable ? "$" : "")}" ) )
				+ $" owes {strip[^1].Owed}";

		string PathTo( int toX ) => Strip( ParkPathBuilding.PathStrip( state, park, toX, 10, Price ) );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkBuildMode.AnchorAt( 10, 10 );

		// Bare, bare, a thing, bare, bare: the thing's cell is never paid for, and the squares after it only by their
		// second verdicts.
		Set( 12, original[12] with { Type = CellEdge.Footprint } );
		HoldTheBalanceAt( 50 );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 owes 40", PathTo( 12 ), "the thing's cell is not charged" );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1$ owes 60", PathTo( 13 ), "60 against 50: c_cash" );

		HoldTheBalanceAt( 70 );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1 owes 60", PathTo( 13 ), "60 against 70: c_noplace" );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 (13,10):1 (14,10):1$ owes 80", PathTo( 14 ), "each charged once" );

		// A corner on bare ground: charged by its first verdict and again by its second.
		Reset();
		Set( 11, original[11] with { TrackNeighbours = 0x05 } );
		HoldTheBalanceAt( 50 );
		Assert.AreEqual( "(10,10):0 (11,10):1$ owes 60", PathTo( 11 ), "40 by the first verdicts, 60 by the second" );

		// Land outside the park answers before the charge in the second verdict too.
		Reset();
		Set( 11, original[11] with { Flags = ParkPathBuilding.OutsideThePark } );
		HoldTheBalanceAt( 30 );
		Assert.AreEqual( "(10,10):0 (11,10):1 owes 20", PathTo( 11 ), "outside the park: not charged" );

		// Track 0x19 latches before the charge in the second verdict too (0x005358b5).
		Reset();
		Set( 12, original[12] with { TrackType = 0x19 } );
		HoldTheBalanceAt( 50 );
		Assert.AreEqual( "(10,10):0 (11,10):0 (12,10):1 owes 40", PathTo( 12 ), "track 0x19: not charged" );

		// Nothing owed, the balance below nought: a red path after a latch is not refused for money.
		Reset();
		ParkBuildMode.AnchorAt( 11, 10 );
		Set( 11, original[11] with { Type = CellEdge.Footprint } );
		Set( 12, original[12] with { Type = CellEdge.Path } );
		HoldTheBalanceAt( -100 );
		Assert.AreEqual( "(11,10):1 (12,10):1 owes 0", PathTo( 12 ), "the money is not asked with nothing owed" );

		// The queue tool pays for bare ground alone: another ride's queue end latches unpaid, the bare ground after it
		// is paid for by its second verdict.
		Reset();
		ParkBuildMode.Forget();
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, state.Objects[0].ThingId, 10, 10 );
		Set( 11, original[11] with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01 } );
		HoldTheBalanceAt( 30 );
		var queue = ParkPathBuilding.QueueStrip( state, park, 11, 10, Price, out _ );
		Assert.AreEqual( "(10,10):0 (11,10):1 owes 20", Strip( queue ), "a queue is not charged in mode 3" );
		queue = ParkPathBuilding.QueueStrip( state, park, 12, 10, Price, out var latched );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1$ owes 40", Strip( queue ), "40 against 30" );
		Assert.IsTrue( latched );

		// Nor does the queue tool's second verdict pay for a path (0x005358e9): bare, a thing, a path, bare.
		Reset();
		Set( 11, original[11] with { Type = CellEdge.Footprint } );
		Set( 12, original[12] with { Type = CellEdge.Path } );
		HoldTheBalanceAt( 50 );
		queue = ParkPathBuilding.QueueStrip( state, park, 13, 10, Price, out _ );
		Assert.AreEqual( "(10,10):0 (11,10):1 (12,10):1 (13,10):1 owes 40", Strip( queue ), "the path is not charged" );
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

	/// <summary>
	/// <b>No square the path or queue tool leaves unrefused is lifted</b>, on any of the shipped park's 16,384 cells,
	/// first or last, so none is walled, and the latches a wall zeroes (<c>0x005333bd</c>) are never zeroed under a
	/// strip here. Lifted cells are met: the Belly Bounce's, the fountain's, and type 7 land on raised ground, all red.
	/// </summary>
	[TestMethod]
	public void NoUnrefusedPathOrQueueSquareIsLifted()
	{
		FileSystem = data;
		var park = World();
		var state = new ParkState( park );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		var serves = ParkPathBuilding.OwnerOf( state, ParkState.CellFor( park, 50, 22 ) );
		var (unrefused, liftedRed) = (0, 0);

		void Judge( IEnumerable<ParkPathBuilding.QueueSquare> strip )
		{
			foreach ( var square in strip )
			{
				var lift = ParkBuildMarkers.LiftOver( state, catalogue, field, square.X, square.Y );

				if ( square.Marker == ParkPathBuilding.MarkerRed )
				{
					liftedRed += lift > 0 ? 1 : 0;
					continue;
				}

				++unrefused;
				Assert.AreEqual( 0f, lift, $"({square.X},{square.Y}) marker {square.Marker}" );
			}
		}

		for ( var y = 0; y < 128; ++y )
		{
			for ( var x = 0; x < 128; ++x )
			{
				ParkBuildMode.Arm( ParkBuildMode.Path );
				Judge( ParkPathBuilding.PathStrip( state, park, x, y, Price ) );

				if ( x == 127 )
					continue;

				ParkBuildMode.AnchorAt( x, y );
				Judge( ParkPathBuilding.PathStrip( state, park, x + 1, y, Price ).Take( 1 ) );

				ParkBuildMode.ArmAt( ParkBuildMode.Queue, serves, x, y );
				Judge( ParkPathBuilding.QueueStrip( state, park, x, y, Price, out _ ) );
				Judge( ParkPathBuilding.QueueStrip( state, park, x + 1, y, Price, out _ ).Take( 1 ) );
			}
		}

		Assert.AreEqual( 8590, unrefused, "measured" );
		Assert.AreEqual( 1496, liftedRed, "measured" );

		// A path on the hill at (76,64), 37 high, is still unlifted: the path tool's m_end and the queue's join there.
		state.SetRecord( 75, 64, ParkState.CellFor( park, 75, 64 ) with { Type = 0, Flags = 0 } );
		state.SetRecord( 76, 64, ParkState.CellFor( park, 76, 64 ) with { Type = CellEdge.Path, Flags = 0 } );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		Judge( ParkPathBuilding.PathStrip( state, park, 76, 64, Price ) );
		ParkBuildMode.ArmAt( ParkBuildMode.Queue, serves, 75, 64 );
		Judge( ParkPathBuilding.QueueStrip( state, park, 76, 64, Price, out _ ) );

		Assert.AreEqual( 8590 + 3, unrefused, "an m_end, then bare ground and the join" );
	}

	/// <summary>
	/// <b>The unanchored queue tool's preview is one square, the pointer's cell, judged as the run's last</b>
	/// (<c>0x0052460a</c>, <c>FUN_00536100</c> with <c>DAT_00820ab8</c> set for its one cell): bare ground blue, a path
	/// <c>m_link</c>, the ride's own queue end <c>m_end</c>, another ride's queue red.
	/// </summary>
	/// <remarks><b>Mutations:</b> the square not judged last (a path and the queue end go blue); the strip empty unanchored.</remarks>
	[TestMethod]
	public void AnUnanchoredQueueStripIsOneSquareJudgedAsTheLast()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = state.Objects[0];
		var rideCell = (ushort)((ride.CellY * 128) + ride.CellX + 1);
		ParkBuildMode.Arm( ParkBuildMode.Queue, ride.ThingId );

		string Strip() => Describe( ParkPathBuilding.QueueStrip( state, park, 11, 10, Price, out _ ) );

		Assert.AreEqual( "(11,10):0", Strip(), "bare ground" );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = CellEdge.Path, Flags = 0 } );
		Assert.AreEqual( "(11,10):8", Strip(), "a path: the join" );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01, ParentId = rideCell } );
		Assert.AreEqual( "(11,10):11", Strip(), "its own queue's end" );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01, ParentId = 0 } );
		Assert.AreEqual( "(11,10):1", Strip(), "another ride's queue" );
	}

	/// <summary>
	/// <b>The unanchored queue click anchors on a fresh list and zeroes the counter</b> (<c>0x005272ab</c>..<c>0x00527316</c>),
	/// so Backspace has nothing of an earlier tool's runs to pop; <b>its own verdict refuses a path and a second cell's
	/// price</b> (<c>0x0052729d</c>), and then nothing changes. Its own queue end is not charged and is anchored on.
	/// </summary>
	/// <remarks><b>Mutations:</b> the list not emptied; the counter not zeroed; the path anchored on; the second charge
	/// dropped (39 anchors); the money test at or below (40 refuses); the queue end charged (a balance of 0 refuses).</remarks>
	[TestMethod]
	public void AnUnanchoredQueueClickAnchorsOnAFreshListAndRefusesWhatItsOwnVerdictRefuses()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = state.Objects[0];
		var rideCell = (ushort)((ride.CellY * 128) + ride.CellX + 1);

		void Stale()
		{
			ParkBuildMode.Forget();
			ParkBuildMode.Push( 40, 20 );
			ParkBuildMode.Push( 44, 20 );
			ParkBuildMode.Clicks = 2;
			ParkBuildMode.Arm( ParkBuildMode.Queue, ride.ThingId );
		}

		string Click() => ParkPathBuilding.FirstQueueClick( state, park, ParkPathBuilding.QueueStrip( state, park, 11, 10, Price, out _ )[0], Price );

		void BalanceAt( int balance ) => state.Spend( state.Balance - balance );

		void Untouched( string because )
		{
			Assert.IsFalse( ParkBuildMode.Anchored, because );
			CollectionAssert.AreEqual( new[] { (40, 20), (44, 20) }, ParkBuildMode.Pending.ToArray(), because );
			Assert.AreEqual( 2, ParkBuildMode.Clicks, because );
			Assert.AreEqual( ParkBuildMode.Queue, ParkBuildMode.Current, "the tool stays out: " + because );
		}

		Stale();
		StringAssert.Contains( Click(), "anchored at (11,10)" );
		Assert.AreEqual( (11, 10), ParkBuildMode.Anchor );
		CollectionAssert.AreEqual( new[] { (11, 10) }, ParkBuildMode.Pending.ToArray() );
		Assert.AreEqual( 0, ParkBuildMode.Clicks );
		Assert.IsFalse( ParkBuildMode.TryPopRun( out _, out _ ), "Backspace has nothing to pop" );

		Stale();
		BalanceAt( (2 * Price) - 1 );
		StringAssert.Contains( Click(), "would bring the run to 40 against a balance of 39" );
		Untouched( "one cell is paid for, not two" );

		Stale();
		BalanceAt( 2 * Price );
		StringAssert.Contains( Click(), "anchored at (11,10)" );

		Stale();
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = CellEdge.Path, Flags = 0 } );
		StringAssert.Contains( Click(), "is a path" );
		Untouched( "a path" );

		Stale();
		BalanceAt( 0 );
		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x01, ParentId = rideCell } );
		StringAssert.Contains( Click(), "anchored at (11,10)" );
		CollectionAssert.AreEqual( new[] { (11, 10) }, ParkBuildMode.Pending.ToArray() );
	}

	/// <summary>
	/// <b>The path tool's unanchored click asks the verdict again, as the run's middle square</b>
	/// (<c>FUN_00535670( cell, 1, 0 )</c>, <c>0x0052727b</c>): bare ground and a loose queue end are charged a second
	/// time, so one square of bare ground needs twice the price; a path is free and anchors on any balance; a path on a
	/// track corner, <c>m_end</c> in the preview, is refused by the click. A refusal changes nothing and keeps the tool.
	/// </summary>
	/// <remarks><b>Mutations:</b> the second charge dropped (39 anchors); the money test at or below (40 refuses); a path
	/// charged (a balance of 0 refuses it); the verdict asked as the last square (the corner anchors); the list not
	/// emptied; the counter not zeroed; the loose queue end charged once (39 anchors).</remarks>
	[TestMethod]
	public void AnUnanchoredPathClickAnchorsOnlyWhereItsOwnVerdictAgrees()
	{
		var park = World();
		var state = new ParkState( park );
		var bare = ParkState.CellFor( park, 11, 10 );

		void Stale()
		{
			ParkBuildMode.Forget();
			ParkBuildMode.Push( 40, 20 );
			ParkBuildMode.Push( 44, 20 );
			ParkBuildMode.Clicks = 2;
			ParkBuildMode.Arm( ParkBuildMode.Path );
		}

		ParkPathBuilding.QueueSquare Preview() => ParkPathBuilding.PathStrip( state, park, 11, 10, Price ) is [var one] ? one : default;

		string Click() => ParkPathBuilding.FirstPathClick( state, park, Preview(), Price );

		void BalanceAt( int balance ) => state.Spend( state.Balance - balance );

		void Untouched( string because )
		{
			Assert.IsFalse( ParkBuildMode.Anchored, because );
			CollectionAssert.AreEqual( new[] { (40, 20), (44, 20) }, ParkBuildMode.Pending.ToArray(), because );
			Assert.AreEqual( 2, ParkBuildMode.Clicks, because );
			Assert.AreEqual( ParkBuildMode.Path, ParkBuildMode.Current, "the tool stays out: " + because );
		}

		Assert.AreEqual( 0, bare.Type, "(11,10) is bare ground in the stock park" );

		Stale();
		BalanceAt( (2 * Price) - 1 );
		Assert.AreEqual( (ParkPathBuilding.MarkerBlue, 20), (Preview().Marker, Preview().Owed), "the preview charges it once and lets it through" );
		StringAssert.Contains( Click(), "would bring the run to 40 against a balance of 39" );
		Untouched( "bare ground is charged twice" );

		Stale();
		BalanceAt( 2 * Price );
		StringAssert.Contains( Click(), "anchored at (11,10)" );
		Assert.AreEqual( (11, 10), ParkBuildMode.Anchor );
		CollectionAssert.AreEqual( new[] { (11, 10) }, ParkBuildMode.Pending.ToArray() );
		Assert.AreEqual( 0, ParkBuildMode.Clicks );

		Stale();
		BalanceAt( 0 );
		state.SetRecord( 11, 10, bare with { Type = CellEdge.Path } );
		StringAssert.Contains( Click(), "anchored at (11,10)", "a path is free" );

		Stale();
		BalanceAt( 1000 );
		state.SetRecord( 11, 10, bare with { Type = CellEdge.Path, TrackNeighbours = 0x05 } );
		Assert.AreEqual( ParkPathBuilding.MarkerEnd, Preview().Marker, "the preview's last square is m_end before the corner is asked" );
		StringAssert.Contains( Click(), "is a track corner or junction" );
		Untouched( "the corner is asked of a middle square" );

		Stale();
		BalanceAt( (2 * Price) - 1 );
		state.SetRecord( 11, 10, bare with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x10 } );
		state.SetRecord( 11, 11, ParkState.CellFor( park, 11, 11 ) with { Type = ParkRideChoice.QueueCellType, Neighbours = 0x11 } );
		Assert.AreEqual( ParkPathBuilding.MarkerLink, Preview().Marker );
		StringAssert.Contains( Click(), "would bring the run to 40 against a balance of 39" );
		Untouched( "a loose queue end is charged twice" );

		BalanceAt( 2 * Price );
		StringAssert.Contains( Click(), "anchored at (11,10)" );
	}

	private static string Describe( IEnumerable<ParkPathBuilding.QueueSquare> strip )
		=> string.Join( " ", strip.Select( square => $"({square.X},{square.Y}):{square.Marker}" ) );
}
