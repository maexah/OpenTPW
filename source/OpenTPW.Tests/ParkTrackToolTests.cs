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

		// The run leaves the closing bend at its flow, 0x01; the circuit's walk that follows turns it to the way it leaves
		// (TheClosedKartLoopsWalkLaysTheOriginalsSections).
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

	/// <summary>A bought kart (water) ride standing on its cell at turn 0, the table holding it, as the walk finds it by the station's owner.</summary>
	private static (ParkState State, ParkWorld.CatalogueObject Ride) Standing( int trackType, int x, int y, int balance = 53522 )
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, trackType, x, y, 0 );

		var ride = new ParkWorld.CatalogueObject( state.NextThingId(), trackType == 1 ? 1150 : 1160, x << 8, y << 8, 0,
			TrackRide: state.TrackRides.Take( trackType == 1 ? -4 : -5 ) );
		state.AddObject( ride );

		return (state, ride);
	}

	private static (int Type, int X, int Y) Section( int type, int flags, int x, int y ) => (type | (flags << 16), x * 0xc00, y * 0xc00);

	/// <summary>
	/// The closed kart loop's walk lays the original's ten sections in circuit order (<c>q581/orig/A-track.log</c>, after
	/// the same clicks), answers 1, and turns the closing corner's way on to the way it leaves, 0x04 (<c>A-load.log</c>).
	/// </summary>
	[TestMethod]
	public void TheClosedKartLoopsWalkLaysTheOriginalsSections()
	{
		var (state, ride) = Standing( 1, 30, 24 );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.AreEqual( (1, 10), ParkTrackTool.WalkCircuit( state, ride, check: false ), "once round, back at the start" );
		CollectionAssert.AreEqual( new[]
		{
			Section( 9, 1, 30, 25 ), Section( 9, 1, 32, 25 ), Section( 7, 0, 34, 25 ), Section( 10, 0, 34, 27 ),
			Section( 6, 0, 34, 29 ), Section( 9, 0, 32, 29 ), Section( 9, 0, 30, 29 ), Section( 5, 0, 28, 29 ),
			Section( 10, 0, 28, 27 ), Section( 8, 0, 28, 25 )
		}, state.TrackRides.SectionsOf( ride.TrackRide ).ToArray() );
		Assert.AreEqual( ((byte)0x14, (byte)0x04, (short)1, 3, 0), Piece( state, 28, 25 ), "A-load.log's (28,25) after the close" );
		Assert.AreEqual( ((byte)0x50, (byte)0x10, (short)1, 3, 90), Piece( state, 34, 25 ), "a bend already leaving its way is left" );

		// A second walk lets the first's sections go before it lays them again.
		Assert.AreEqual( 1, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid );
		Assert.AreEqual( 10, state.TrackRides.SectionsOf( ride.TrackRide ).Count );
	}

	/// <summary>The water loop south of its station, Q581's (<c>q581/orig/D-track.log</c>), and Q687's north of it, whose closing corner the walk makes <c>wr_trckB2</c> (<c>q587/orig/W-cells.log</c>).</summary>
	[TestMethod]
	public void TheWaterLoopsWalkLaysItsSectionsAndTurnsTheClosingCorner()
	{
		var (state, ride) = Standing( 2, 18, 18, 47452 );
		var owner = (ushort)MapStep.CellId( 18, 18 );

		ParkTrackTool.LayRun( state, 0x10, owner, (22, 19), (22, 23), 250 );
		ParkTrackTool.LayRun( state, 0x10, owner, (22, 23), (16, 23), 250 );
		ParkTrackTool.LayRun( state, 0x10, owner, (16, 23), (16, 19), 250 );

		Assert.AreEqual( 1, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid );
		CollectionAssert.AreEqual( new[]
		{
			Section( 9, 1, 18, 19 ), Section( 9, 1, 20, 19 ), Section( 7, 0, 22, 19 ), Section( 10, 0, 22, 21 ),
			Section( 6, 0, 22, 23 ), Section( 9, 0, 20, 23 ), Section( 9, 0, 18, 23 ), Section( 5, 0, 16, 23 ),
			Section( 10, 0, 16, 21 ), Section( 8, 0, 16, 19 )
		}, state.TrackRides.SectionsOf( ride.TrackRide ).ToArray() );

		(state, ride) = Standing( 2, 18, 18, 47452 );
		ParkTrackTool.LayRun( state, 0x10, owner, (22, 19), (22, 15), 250 );
		ParkTrackTool.LayRun( state, 0x10, owner, (22, 15), (16, 15), 250 );
		ParkTrackTool.LayRun( state, 0x10, owner, (16, 15), (16, 19), 250 );

		Assert.AreEqual( ((byte)0x05, (byte)0x10, 3), (Piece( state, 16, 19 ).Mask, Piece( state, 16, 19 ).Way, Piece( state, 16, 19 ).Row ),
			"the run leaves the corner wr_trckB" );
		Assert.AreEqual( 1, ParkTrackTool.WalkCircuit( state, ride, check: true ).Valid );
		Assert.AreEqual( ((byte)0x05, (byte)0x04, (short)1, 19, 270), Piece( state, 16, 19 ), "W-cells.log's (16,19), wr_trckB2" );
		Assert.AreEqual( 0, state.TrackRides.SectionsOf( ride.TrackRide ).Count, "a checking walk lays no section" );
	}

	/// <summary>An open loop's walk stops at its open end, answering 0, with the sections it passed laid.</summary>
	[TestMethod]
	public void AnOpenLoopsWalkAnswersNought()
	{
		var (state, ride) = Standing( 1, 30, 24 );

		Assert.AreEqual( 0, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid, "the bought station alone: the mouth is an end" );
		Assert.AreEqual( 2, state.TrackRides.SectionsOf( ride.TrackRide ).Count );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		Assert.AreEqual( 0, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid );
		CollectionAssert.AreEqual( new[]
		{
			Section( 9, 1, 30, 25 ), Section( 9, 1, 32, 25 ), Section( 7, 0, 34, 25 ), Section( 10, 0, 34, 27 ),
			Section( 6, 0, 34, 29 ), Section( 9, 0, 32, 29 ), Section( 9, 0, 30, 29 )
		}, state.TrackRides.SectionsOf( ride.TrackRide ).ToArray(), "no section for the end at (28,29)" );

		// Track beyond the end, not linked to it: the walk stops at the end and lays nothing there.
		state.SetRecord( 26, 29, state.Record( 26, 29 ) with { TrackType = 0xb, TrackNeighbours = 0x44, TrackParentId = Owner } );
		Assert.AreEqual( (0, 8), ParkTrackTool.WalkCircuit( state, ride, check: false ) );
		Assert.AreEqual( 7, state.TrackRides.SectionsOf( ride.TrackRide ).Count, "an end stops it, track beyond or not" );

		state.SetRecord( 32, 29, state.Record( 32, 29 ) with { TrackType = 0 } );
		Assert.AreEqual( 0, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid );
		Assert.AreEqual( 5, state.TrackRides.SectionsOf( ride.TrackRide ).Count, "a cell that is not track stops it" );

		// A station that is no longer track: the walk answers 0 before it lets the sections go.
		state.SetRecord( 30, 25, state.Record( 30, 25 ) with { TrackType = 0 } );
		Assert.AreEqual( (0, 0), ParkTrackTool.WalkCircuit( state, ride, check: false ) );
		Assert.AreEqual( 5, state.TrackRides.SectionsOf( ride.TrackRide ).Count );
	}

	/// <summary>A crossing lays section 11 and is gone straight through; an add-on's straight lays 12 or 13.</summary>
	[TestMethod]
	public void ACrossingAndAnAddOnLayTheirOwnSections()
	{
		var (state, ride) = Standing( 1, 30, 24 );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackNeighbours = 0x55 } );
		state.SetRecord( 32, 29, state.Record( 32, 29 ) with { TrackFlags = ParkTrackPieces.AddOnFlag } );
		state.SetRecord( 28, 27, state.Record( 28, 27 ) with { TrackFlags = ParkTrackPieces.AddOnFlag } );

		Assert.AreEqual( 1, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid );

		var sections = state.TrackRides.SectionsOf( ride.TrackRide );

		Assert.AreEqual( Section( 11, 0, 34, 27 ), sections[3] );
		Assert.AreEqual( Section( 12, 0, 32, 29 ), sections[5], "east-west with an add-on" );
		Assert.AreEqual( Section( 13, 0, 28, 27 ), sections[8], "north-south with an add-on" );
	}

	/// <summary>A ride whose loop the tool closes takes the walk's 1 into <c>+0x2c</c>, and opens only where the open guard lets it.</summary>
	[TestMethod]
	public void ARideWithNoQueueJoinedIsValidAndNotOpened()
	{
		var (state, ride) = Standing( 1, 30, 24 );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.AreEqual( 1, ParkTrackTool.WalkCircuit( state, ride, check: false ).Valid );
		Assert.IsFalse( ParkRideOperation.MayOpen( null, ride, 1 ), "no park, no queue joined: the guard refuses" );
	}
}
