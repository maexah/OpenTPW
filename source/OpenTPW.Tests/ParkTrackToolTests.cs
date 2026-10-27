using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The karts' and the water rides' track tool - <see cref="ParkTrackTool"/>: where a click arms it, where a click's run
/// ends, its verdict and the run it lays (<c>docs/exe/park.md</c>, "The track tool"). The loop is the original's own, laid
/// by the same clicks under Proton on <c>q581/karts.TPWS</c> (<c>q587/orig/A-load.log</c>): a Dino Karts bought on its
/// cell (30,24) at turn 0, runs to (34,29), (28,29) and back to (28,25).
/// </summary>
[TestClass]
public class ParkTrackToolTests
{
	private static readonly ushort Owner = (ushort)MapStep.CellId( 30, 24 );

	private static ParkState Bought( int balance = 53522 )
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );
		return state;
	}

	private static (byte Mask, byte Way, short Over, int Row, int Turn) Piece( ParkState state, int x, int y )
	{
		var cell = state.Record( x, y );
		return (cell.TrackNeighbours, cell.TrackDirection, cell.TrackOverlapCounter, cell.TrackTileIndex, cell.TrackTileAngle);
	}

	[TestMethod]
	public void AClickOnAPieceOrItsOtherCellFindsItsAnchor()
	{
		var state = Bought();

		Assert.AreEqual( (34, 25), ParkTrackTool.AnchorUnder( state, 34, 25 ) );
		Assert.AreEqual( (34, 25), ParkTrackTool.AnchorUnder( state, 35, 26 ), "a kart piece's 0xc cell names its anchor" );
		Assert.IsNull( ParkTrackTool.AnchorUnder( state, 30, 25 ), "a station arms nothing" );
		Assert.IsNull( ParkTrackTool.AnchorUnder( state, 31, 26 ), "nor a station's other cell" );
		Assert.IsNull( ParkTrackTool.AnchorUnder( state, 40, 40 ) );
	}

	/// <summary>The arming walk: a bought ride's open end is its mouth; after two runs it is the second run's end, past one bend.</summary>
	[TestMethod]
	public void TheArmingWalkEndsAtTheOpenEnd()
	{
		var state = Bought();
		var walk = ParkTrackTool.WalkToOpenEnd( state, 30, 24, 0 );

		Assert.AreEqual( ((34, 25), false), (walk.End, walk.Closed) );
		CollectionAssert.AreEqual( new[] { (34, 25), (34, 25) }, walk.Pending.ToArray() );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { TrackOverlapCounter = 5 } );
		state.SetRecord( 32, 29, state.Record( 32, 29 ) with { TrackOverlapCounter = 3 } );
		walk = ParkTrackTool.WalkToOpenEnd( state, 30, 24, 0 );

		Assert.AreEqual( ((28, 29), false), (walk.End, walk.Closed) );
		CollectionAssert.AreEqual( new[] { (34, 25), (34, 25), (34, 29), (28, 29) }, walk.Pending.ToArray(),
			"the mouth, each bend and the end" );
		Assert.AreEqual( (short)1, state.Record( 34, 29 ).TrackOverlapCounter, "a bend's counter is set to 1" );
		Assert.AreEqual( (short)0, state.Record( 32, 29 ).TrackOverlapCounter, "a straight's to 0" );

		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.IsTrue( ParkTrackTool.WalkToOpenEnd( state, 30, 24, 0 ).Closed, "a closed loop walks into the station" );
	}

	[TestMethod]
	public void AClicksTargetIsOnTheAnchorsRowOrColumnAndItsGrid()
	{
		Assert.AreEqual( (34, 29), ParkTrackTool.TargetOf( (34, 25), 35, 30 ), "the farther axis wins, the other is the anchor's" );
		Assert.AreEqual( (34, 27), ParkTrackTool.TargetOf( (34, 25), 34, 28 ), "an odd distance onward is taken back" );
		Assert.AreEqual( (34, 25), ParkTrackTool.TargetOf( (34, 29), 34, 26 ), "and an odd distance back is taken on" );
		Assert.AreEqual( (28, 29), ParkTrackTool.TargetOf( (34, 29), 29, 30 ) );
		Assert.AreEqual( (30, 25), ParkTrackTool.TargetOf( (34, 25), 31, 28 ), "a tie goes to the row" );
		Assert.AreEqual( (34, 25), ParkTrackTool.TargetOf( (34, 25), 34, 25 ) );
	}

	/// <summary>The original's three runs, cell for cell as <c>A-load.log</c> read them (181.06 s, 192.21 s, 221.95 s).</summary>
	[TestMethod]
	public void TheOriginalsLoopIsLaidCellForCell()
	{
		var state = Bought();
		var laid = ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( (false, 400, 2), (laid.MetTrack, laid.Charged, laid.NewPieces) );
		Assert.AreEqual( 53122, state.Balance );
		Assert.AreEqual( ((byte)0x50, (byte)0x10, (short)1, 3, 90), Piece( state, 34, 25 ) );
		Assert.AreEqual( ((byte)0x11, (byte)0x10, (short)0, 2, 180), Piece( state, 34, 27 ) );
		Assert.AreEqual( ((byte)0x01, (byte)0x10, (short)0, 1, 180), Piece( state, 34, 29 ) );
		Assert.AreEqual( (0xb, Owner, 3), (state.Record( 34, 27 ).TrackType, state.Record( 34, 27 ).TrackParentId, state.Record( 34, 27 ).TrackTileSet) );
		Assert.AreEqual( (0xc, (ushort)MapStep.CellId( 34, 27 )), (state.Record( 35, 28 ).TrackType, state.Record( 35, 28 ).TrackParentId) );

		laid = ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		Assert.AreEqual( (false, 600, 3), (laid.MetTrack, laid.Charged, laid.NewPieces) );
		Assert.AreEqual( ((byte)0x41, (byte)0x40, (short)1, 3, 180), Piece( state, 34, 29 ) );
		Assert.AreEqual( ((byte)0x44, (byte)0x40, (short)0, 2, 270), Piece( state, 32, 29 ) );
		Assert.AreEqual( ((byte)0x44, (byte)0x40, (short)0, 2, 270), Piece( state, 30, 29 ) );
		Assert.AreEqual( ((byte)0x04, (byte)0x40, (short)0, 1, 270), Piece( state, 28, 29 ) );

		laid = ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.AreEqual( (true, 200, 1), (laid.MetTrack, laid.Charged, laid.NewPieces), "the run met the other mouth" );
		Assert.AreEqual( 52322, state.Balance );
		Assert.AreEqual( ((byte)0x05, (byte)0x01, (short)1, 3, 270), Piece( state, 28, 29 ) );
		Assert.AreEqual( ((byte)0x11, (byte)0x01, (short)0, 2, 0), Piece( state, 28, 27 ) );

		// The original's circuit walk then turns the closing bend's way on to the way it leaves (0x04); ours stops at the
		// run's flow, 0x01, which takes the same row and turn.
		Assert.AreEqual( ((byte)0x14, (byte)0x01, (short)1, 3, 0), Piece( state, 28, 25 ) );
		Assert.AreEqual( ((byte)0x44, (byte)0x04), (state.Record( 30, 25 ).TrackNeighbours, state.Record( 30, 25 ).TrackDirection),
			"the station is not relaid" );
	}

	[TestMethod]
	public void TheVerdictRefusesWhatTheRunCannotCross()
	{
		var state = Bought( balance: 500 );

		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused );
		Assert.AreEqual( 400, ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Total, "the anchor's own piece is free" );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 31), 200 ).Refused, "600 is more than 500" );
		var station = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (30, 25), 200 );
		Assert.IsTrue( station.Refused && station.Why!.Contains( "station" ), "into its own station: " + station.Why );

		var other = (ushort)MapStep.CellId( 40, 40 );
		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { TrackType = 0xc, TrackParentId = (ushort)MapStep.CellId( 36, 29 ) } );
		state.SetRecord( 36, 29, state.Record( 36, 29 ) with { TrackType = 0xb, TrackParentId = other } );

		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused,
			"another ride's piece under a new piece's two by two" );

	}

	[TestMethod]
	public void ARunOverObjectsOrPathIsRefused()
	{
		var state = Bought();

		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { Type = CellEdge.Footprint } );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused );

		state = Bought();
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { Type = CellEdge.Path } );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused );

		state = Bought();
		state.SetRecord( 33, 30, state.Record( 33, 30 ) with { Flags = 0x40 } );
		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused, "(33,30) is no piece's cell" );
		state.SetRecord( 35, 30, state.Record( 35, 30 ) with { Flags = 0x40 } );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused, "outside the park's land" );
	}

	[TestMethod]
	public void AStampTheBalanceCannotPayStopsThere()
	{
		var state = Bought( balance: 300 );
		var laid = ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( (200, 1), (laid.Charged, laid.NewPieces) );
		Assert.AreEqual( (0xb, 0), (state.Record( 34, 27 ).TrackType, state.Record( 34, 29 ).TrackType) );
	}

	/// <summary>
	/// A run whose last cell is a piece's other cell (0xc) has not met track: the stamp refuses that cell and the retile pass
	/// answers as for a new end (<c>0x00820ae8</c> is set for track types 0xb, 0xd, 0x10, 0x12 and 0x19 alone).
	/// </summary>
	[TestMethod]
	public void ARunEndingOnAPiecesOtherCellHasNotMetTrack()
	{
		var state = Bought();

		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackType = 0xc, TrackParentId = (ushort)MapStep.CellId( 33, 26 ) } );

		Assert.IsFalse( ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 27), 200 ).MetTrack );
		Assert.AreEqual( 0xc, state.Record( 34, 27 ).TrackType, "the stamp leaves another type alone" );
		Assert.IsTrue( ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 25), 200 ).MetTrack, "the anchor itself is track" );
	}

	/// <summary>A refused stamp stops its pass: a piece of its own further on is not counted over again.</summary>
	[TestMethod]
	public void ARefusedStampLeavesTheRestOfThePass()
	{
		var state = Bought( balance: 100 );
		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { TrackType = 0xb, TrackParentId = Owner } );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( ((short)1, 0, (short)0), (state.Record( 34, 25 ).TrackOverlapCounter, state.Record( 34, 27 ).TrackType,
			state.Record( 34, 29 ).TrackOverlapCounter) );
	}

	/// <summary>A piece that becomes a bend loses its hill flags (the retile pass, <c>0x00538757</c>).</summary>
	[TestMethod]
	public void ABendLosesItsHillFlags()
	{
		var state = Bought();
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackFlags = 0x11 } );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( ((ushort)0x10, 3), (state.Record( 34, 25 ).TrackFlags, state.Record( 34, 25 ).TrackTileIndex),
			"the add-on bit stays, the hill bit goes" );
	}

	/// <summary>The water ride's runs lay 0x10 pieces from the water kit's table, their other cells 0xc.</summary>
	[TestMethod]
	public void AWaterRunLaysWaterPieces()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 47452 );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 2, 18, 18, 0 );
		var owner = (ushort)MapStep.CellId( 18, 18 );

		var laid = ParkTrackTool.LayRun( state, 0x10, owner, (22, 19), (22, 15), 250 );

		Assert.AreEqual( (500, 2), (laid.Charged, laid.NewPieces) );
		Assert.AreEqual( ((byte)0x41, (byte)0x01, (short)1, 19, 180), Piece( state, 22, 19 ), "W-cells.log's corner, wr_trckB2" );
		Assert.AreEqual( ((byte)0x11, (byte)0x01, (short)0, 2, 0), Piece( state, 22, 17 ) );
		Assert.AreEqual( (0x10, 0xc), (state.Record( 22, 15 ).TrackType, state.Record( 23, 16 ).TrackType) );
	}
}
