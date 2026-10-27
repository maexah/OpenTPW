using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
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

	/// <summary>The tool's statics (the mode, its anchor and the list of run ends) are the program's one set; each test starts and leaves them clear.</summary>
	[TestInitialize]
	[TestCleanup]
	public void ForgetTheTool() => ParkBuildMode.Forget();

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

	private static int Valid( ParkState state, int thingId ) => state.TryObject( thingId, out var ride ) ? ride.IsTrackRideValid : -1;

	/// <summary>
	/// A run the balance cannot pay is refused: nothing is laid and the tool is put away (<c>0x00524a63</c>). Made to fail by
	/// leaving the tool armed, or by laying the run.
	/// </summary>
	[TestMethod]
	public void ARefusedClickLaysNothingAndPutsTheToolAway()
	{
		var (state, ride) = Standing( 1, 30, 24, 300 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );

		var said = ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 34, 29, true );

		Assert.IsTrue( said.Contains( "refused" ), said );
		Assert.AreEqual( (ParkBuildMode.None, false), (ParkBuildMode.Current, ParkBuildMode.Anchored), "the tool is put away" );
		Assert.AreEqual( (300, 0), (state.Balance, state.Record( 34, 27 ).TrackType), "and nothing is laid or charged" );
	}

	/// <summary>A click that lays a run and does not meet track moves the anchor to its end and keeps the tool armed.</summary>
	[TestMethod]
	public void AClickThatMovesTheAnchorKeepsTheToolArmed()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );
		ParkBuildMode.Push( 34, 25 );

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 34, 29, true );

		Assert.AreEqual( 53122, state.Balance );
		Assert.AreEqual( (ParkBuildMode.KartTrack, (34, 29)), (ParkBuildMode.Current, ParkBuildMode.Anchor) );
		CollectionAssert.AreEqual( new[] { (34, 25), (34, 29) }, ParkBuildMode.Pending.ToArray(), "the run's end is pushed" );
		Assert.AreEqual( 0, Valid( state, ride.ThingId ), "no circuit walked yet" );
	}

	/// <summary>
	/// A click that does not move the anchor lays nothing and closes the tool, unless it is on the mouth while the anchor
	/// is the mouth, which lays (<c>0x00526d86</c>..<c>0x00526da4</c>). Made to fail by dropping the mouth's case, or by
	/// laying on every such click.
	/// </summary>
	[TestMethod]
	public void AClickThatDoesNotMoveTheAnchorLaysOnlyOnTheMouth()
	{
		Assert.IsTrue( ParkTrackTool.LaysRun( (34, 25), (34, 29), (34, 25) ), "a run that moves the anchor" );
		Assert.IsTrue( ParkTrackTool.LaysRun( (34, 25), (34, 25), (34, 25) ), "on the mouth while the anchor is the mouth" );
		Assert.IsFalse( ParkTrackTool.LaysRun( (34, 29), (34, 29), (34, 25) ), "anywhere else on the anchor" );

		var (state, ride) = Standing( 1, 30, 24 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );
		ParkBuildMode.Push( 34, 25 );

		Assert.AreEqual( (short)0, state.Record( 34, 25 ).TrackOverlapCounter, "a bought mouth has been laid over by nothing" );

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 34, 25, true );

		Assert.AreEqual( ((short)1, 2), (state.Record( 34, 25 ).TrackOverlapCounter, ParkBuildMode.Pending.Count), "the mouth's own piece counted again, and pushed" );
		Assert.AreEqual( (ParkBuildMode.None, 0), (ParkBuildMode.Current, Valid( state, ride.ThingId )), "closed on an open loop: +0x2c 0" );

		ParkBuildMode.Forget();
		(state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 29 );
		ParkBuildMode.Push( 34, 29 );
		var before = state.Record( 34, 29 ).TrackOverlapCounter;

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 34, 29, true );

		Assert.AreEqual( (before, 1), (state.Record( 34, 29 ).TrackOverlapCounter, ParkBuildMode.Pending.Count), "off the mouth it lays nothing" );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
	}

	[TestMethod]
	public void AClickMovesTheAnchorUnlessItMetTrack()
	{
		Assert.AreEqual( ParkTrackTool.RunOutcome.Moved, ParkTrackTool.OutcomeOf( (34, 25), (34, 29), false ) );
		Assert.AreEqual( ParkTrackTool.RunOutcome.Closed, ParkTrackTool.OutcomeOf( (34, 25), (34, 29), true ), "a run that met track closes" );
		Assert.AreEqual( ParkTrackTool.RunOutcome.Closed, ParkTrackTool.OutcomeOf( (34, 25), (34, 25), false ), "so does a click that moves nothing" );
	}

	/// <summary>
	/// The run that closes the loop puts the tool away and stores the walk's 1 in the ride's <c>+0x2c</c>; the ride is not
	/// opened, no queue being joined. Made to fail by keeping the tool armed on a run that met track, by moving the anchor
	/// there, or by not storing the walk's answer.
	/// </summary>
	[TestMethod]
	public void TheRunThatClosesTheLoopPutsTheToolAwayAndStoresTheWalk()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 28, 29 );

		var said = ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 28, 25, true );

		Assert.IsTrue( said.Contains( "it met track" ) && said.Contains( "+0x2c 1" ), said );
		Assert.AreEqual( (ParkBuildMode.None, false, 1), (ParkBuildMode.Current, ParkBuildMode.Anchored, Valid( state, ride.ThingId )) );
		Assert.AreEqual( 0, state.Objects.Single( o => o.ThingId == ride.ThingId ).CanLoad, "no queue joined, so not opened" );
		Assert.AreEqual( 10, state.TrackRides.SectionsOf( ride.TrackRide ).Count, "the walk laid the circuit" );
	}

	/// <summary>
	/// The tail of the closing click (<c>0x00526f0d</c>..<c>0x00526f2d</c>): <c>+0x2c</c> stored, and the ride opened where
	/// the walk answered 1 and its queue is joined. A shipped ride whose back of queue is joined, shut by hand. Made to fail
	/// by not storing, by opening on a 0, or by not opening on a 1.
	/// </summary>
	[TestMethod]
	public void TheClosingClicksTailStoresTheWalkAndOpensOnlyOnOne()
	{
		FileSystem = GameData.Required();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var joined = state.Objects.First( o => ParkRideOperation.BackOfQueueConnected( world, o ) && o.RequestedService == 0
			&& o.State is not (1 or 2 or 4) );

		void Shut( int valid ) => state.ReplaceObject( state.Objects.Single( o => o.ThingId == joined.ThingId ) with { CanLoad = 0, IsTrackRideValid = valid } );

		Shut( 5 );
		ParkTrackTool.Settle( state, world, null, joined.ThingId, 1, 0 );

		Assert.AreEqual( (0, 0), (Valid( state, joined.ThingId ), state.Objects.Single( o => o.ThingId == joined.ThingId ).CanLoad ), "a 0 is stored and opens nothing" );

		Shut( 5 );
		ParkTrackTool.Settle( state, world, null, joined.ThingId, 1, 1 );

		Assert.AreEqual( (1, 1), (Valid( state, joined.ThingId ), state.Objects.Single( o => o.ThingId == joined.ThingId ).CanLoad ), "a 1 is stored and opens the ride whose queue is joined" );

		Shut( 5 );
		ParkTrackTool.Settle( state, null, null, joined.ThingId, 1, 1 );

		Assert.AreEqual( (1, 0), (Valid( state, joined.ThingId ), state.Objects.Single( o => o.ThingId == joined.ThingId ).CanLoad ), "the guard refuses with no queue joined" );
	}

	/// <summary>Arming walks the track to its open end, which becomes the anchor, and a closed loop is not armed but counted.</summary>
	[TestMethod]
	public void ArmingAnchorsAnOpenLoopAtItsEndAndRefusesAClosedOne()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		Assert.IsTrue( ParkTrackTool.ArmOn( state, _ => 1, 35, 26 ).Contains( "open end" ) );
		Assert.AreEqual( (ParkBuildMode.KartTrack, ride.ThingId, (28, 29)), (ParkBuildMode.Current, ParkBuildMode.Serves, ParkBuildMode.Anchor) );
		CollectionAssert.AreEqual( new[] { (34, 25), (34, 25), (34, 29), (28, 29) }, ParkBuildMode.Pending.ToArray() );

		ParkBuildMode.Forget();
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		var said = ParkTrackTool.ArmOn( state, _ => 1, 35, 26 );

		Assert.IsTrue( said.Contains( "closed" ), said );
		Assert.AreEqual( (ParkBuildMode.None, false, 0), (ParkBuildMode.Current, ParkBuildMode.Anchored, ParkBuildMode.Pending.Count), "a closed loop arms nothing" );
		Assert.IsFalse( ParkTrackTool.ArmOn( state, _ => 1, 30, 25 ).Contains( "open end" ), "a station arms nothing" );
	}

	[TestMethod]
	public void ArmingAWaterRideTakesTheWaterMode()
	{
		var (state, ride) = Standing( 2, 18, 18, 47452 );

		ParkTrackTool.ArmOn( state, _ => 2, 22, 19 );

		Assert.AreEqual( (ParkBuildMode.WaterTrack, ride.ThingId), (ParkBuildMode.Current, ParkBuildMode.Serves) );
	}

	/// <summary>
	/// A three-link cell answers 0 where the cell beyond is of no type, or is track with no links while heading north
	/// (<c>0x0052b22f</c>..<c>0x0052b280</c>); else the walk goes straight on through it. Told by the steps taken, which a
	/// cell that is not track would add one to anyway.
	/// </summary>
	[TestMethod]
	public void AThreeLinkCellAnswersNoughtWhereTheCellBeyondIsNothing()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackNeighbours = 0x15 } );
		Assert.AreEqual( (1, 10), ParkTrackTool.WalkCircuit( state, ride, check: true ), "a junction is gone straight on through" );

		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { TrackType = 0 } );
		Assert.AreEqual( (0, 4), ParkTrackTool.WalkCircuit( state, ride, check: true ), "the cell beyond of no type: nought at the junction" );

		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { TrackType = 0xc } );
		Assert.AreEqual( (0, 5), ParkTrackTool.WalkCircuit( state, ride, check: true ), "of some type: the junction lets it by, the next cell fails" );
	}

	/// <summary>Heading north, track with no links beyond a three-link cell answers 0; heading south it does not.</summary>
	[TestMethod]
	public void ATrackCellWithNoLinksBeyondAThreeLinkCellFailsOnlyHeadingNorth()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		// South: (34,27) has three links and the cell beyond it is track with none; the walk fails at that cell next step.
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackNeighbours = 0x15 } );
		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { TrackNeighbours = 0 } );
		Assert.AreEqual( (0, 5), ParkTrackTool.WalkCircuit( state, ride, check: true ), "heading south the junction lets it by" );

		// North: the same on the west leg.
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackNeighbours = 0x11 } );
		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { TrackNeighbours = 0x41 } );
		state.SetRecord( 28, 27, state.Record( 28, 27 ) with { TrackNeighbours = 0x51 } );
		state.SetRecord( 28, 25, state.Record( 28, 25 ) with { TrackNeighbours = 0 } );
		Assert.AreEqual( (0, 9), ParkTrackTool.WalkCircuit( state, ride, check: true ), "heading north it answers nought at the junction" );
	}

	/// <summary>A bend entered past its links leaves by the link it has beyond the one it was entered along (<c>0x0052b1cc</c>).</summary>
	[TestMethod]
	public void ABendEnteredWithNoLinkBackLeavesTheWayItHas()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		// Entered going east, (34,25) has links east and south and none back west: it leaves south.
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackNeighbours = 0x14 } );

		Assert.AreEqual( (1, 10), ParkTrackTool.WalkCircuit( state, ride, check: true ) );
		Assert.AreEqual( (byte)0x10, state.Record( 34, 25 ).TrackDirection, "the bend's way on is the way it leaves" );
	}

	/// <summary>
	/// Backspace after two runs: the last is popped, its pieces refunded, the anchor put at its start and the mouth's bend
	/// left alone (the list is not back at its first entry). Made to fail by putting the anchor at the run's end.
	/// </summary>
	[TestMethod]
	public void BacktrackTakesBackTheLastRunAndAnchorsAtItsStart()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 28, 29 );

		foreach ( var at in new[] { (34, 25), (34, 25), (34, 29), (28, 29) } )
			ParkBuildMode.Push( at.Item1, at.Item2 );

		var said = ParkTrackTool.BacktrackOn( state, ride, 200 );

		Assert.IsTrue( said.Contains( "3 piece(s) removed, 600 refunded" ), said );
		Assert.AreEqual( ((34, 29), 3), (ParkBuildMode.Anchor, ParkBuildMode.Pending.Count) );
		Assert.AreEqual( 53122, state.Balance );
		Assert.AreEqual( ((byte)0x50, (byte)0x10), (state.Record( 34, 25 ).TrackNeighbours, state.Record( 34, 25 ).TrackDirection), "the mouth's bend stays" );
		Assert.AreEqual( ((byte)0x01, (byte)0x10, (short)0, 1, 180), Piece( state, 34, 29 ) );
	}

	/// <summary>
	/// Back at the list's first entry the mouth's bend is broken: the pop leaves one entry. Made to fail by deciding on
	/// no entries left, or on the list before the pop.
	/// </summary>
	[TestMethod]
	public void BacktrackBreaksTheMouthsBendWhereTheListIsBackAtItsFirstEntry()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );
		ParkBuildMode.Push( 34, 25 );
		ParkBuildMode.Push( 34, 25 );

		ParkTrackTool.BacktrackOn( state, ride, 200 );

		Assert.AreEqual( 1, ParkBuildMode.Pending.Count );
		Assert.AreEqual( ((byte)0x40, (byte)0x04), (state.Record( 34, 25 ).TrackNeighbours, state.Record( 34, 25 ).TrackDirection), "the bend is broken" );
		Assert.AreEqual( (byte)0x10, state.Record( 34, 27 ).TrackNeighbours, "at both ends" );

		Assert.IsTrue( ParkTrackTool.BacktrackOn( state, ride, 200 ).Contains( "nothing laid" ), "the first entry is never popped" );
	}

	/// <summary>A water run taken back: three pieces of 250 refunded, the start an end again in the water kit's family.</summary>
	[TestMethod]
	public void BacktrackTakesBackAWaterRun()
	{
		var (state, ride) = Standing( 2, 18, 18, 47452 );
		var owner = (ushort)MapStep.CellId( 18, 18 );
		ParkTrackTool.LayRun( state, 0x10, owner, (22, 19), (22, 23), 250 );
		ParkTrackTool.LayRun( state, 0x10, owner, (22, 23), (16, 23), 250 );
		ParkBuildMode.ArmAt( ParkBuildMode.WaterTrack, ride.ThingId, 16, 23 );

		foreach ( var at in new[] { (22, 19), (22, 19), (22, 23), (16, 23) } )
			ParkBuildMode.Push( at.Item1, at.Item2 );

		var before = state.Balance;
		var said = ParkTrackTool.BacktrackOn( state, ride, 250 );

		Assert.IsTrue( said.Contains( "3 piece(s) removed, 750 refunded" ), said );
		Assert.AreEqual( before + 750, state.Balance );
		Assert.AreEqual( ((byte)0x01, (byte)0x10, (short)0, 1, 180), Piece( state, 22, 23 ), "an end again, its way out of it" );
		Assert.AreEqual( (4, 0), (state.Record( 22, 23 ).TrackTileSet, state.Record( 16, 23 ).TrackType), "a water piece, and the removed ones gone" );
	}
	/// <summary>
	/// Backspace after the original's second run takes it back as <c>q587/orig/A-load.log</c> read it at 199.64 s: three
	/// pieces removed and 600 refunded, the corner (34,29) an end piece again with the way out of it, the removed anchors
	/// and their cells bare at row 0x37.
	/// </summary>
	[TestMethod]
	public void BackspaceTakesBackTheOriginalsSecondRun()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		Assert.AreEqual( 52522, state.Balance );

		var taken = ParkTrackTool.TakeBack( state, ride, (28, 29), (34, 29), 200, backToFirst: false );

		Assert.AreEqual( (3, 600), (taken.Removed, taken.Refunded) );
		Assert.AreEqual( 53122, state.Balance );
		Assert.AreEqual( ((byte)0x01, (byte)0x10, (short)0, 1, 180), Piece( state, 34, 29 ), "an end again, its way out of it" );
		Assert.AreEqual( ((byte)0x11, (byte)0x10, (short)0, 2, 180), Piece( state, 34, 27 ) );
		Assert.AreEqual( ((byte)0x50, (byte)0x10, (short)1, 3, 90), Piece( state, 34, 25 ), "the mouth is not touched" );

		foreach ( var (x, y) in new[] { (32, 29), (30, 29), (28, 29), (33, 30), (29, 29), (28, 30) } )
		{
			var cell = state.Record( x, y );
			Assert.AreEqual( (0, (byte)0, (byte)0, (ushort)0, 0, ParkTrackTool.ClearedRow, 0),
				(cell.TrackType, cell.TrackNeighbours, cell.TrackDirection, cell.TrackParentId, cell.TrackTileSet, cell.TrackTileIndex,
					cell.TrackTileAngle), $"({x},{y})" );
		}

		Assert.AreEqual( 0xc, state.Record( 35, 30 ).TrackType, "the kept end's own cells stay" );
	}

	/// <summary>Taking back the first run leaves the mouth as the ride was bought, and refunds the run's two pieces.</summary>
	[TestMethod]
	public void TakingBackTheFirstRunLeavesTheMouthAsBought()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		var bought = Piece( state, 34, 25 );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		var taken = ParkTrackTool.TakeBack( state, ride, (34, 29), (34, 25), 200, backToFirst: false );

		Assert.AreEqual( (2, 400), (taken.Removed, taken.Refunded) );
		Assert.AreEqual( 53522, state.Balance );
		Assert.AreEqual( bought, Piece( state, 34, 25 ) );
		Assert.AreEqual( ((byte)0x40, (byte)0x04), (state.Record( 34, 25 ).TrackNeighbours, state.Record( 34, 25 ).TrackDirection) );
	}

	/// <summary>
	/// A crossing taken back keeps its piece with the run's axis let go; a piece another run was laid over keeps it too;
	/// and a step of nought removes outright a piece that is not the mouth.
	/// </summary>
	[TestMethod]
	public void APieceLaidOverIsKeptAndANoughtStepRemoves()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 31), 200 );
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with
		{
			TrackNeighbours = 0x55, TrackDirection = 0x14, TrackOverlapCounter = 1
		} );

		var taken = ParkTrackTool.TakeBack( state, ride, (34, 31), (34, 25), 200, backToFirst: false );

		Assert.AreEqual( (2, 400), (taken.Removed, taken.Refunded), "(34,29) and (34,31) go" );
		Assert.AreEqual( (0xb, (byte)0x44, (byte)0x04, (short)0),
			(state.Record( 34, 27 ).TrackType, state.Record( 34, 27 ).TrackNeighbours, state.Record( 34, 27 ).TrackDirection,
				state.Record( 34, 27 ).TrackOverlapCounter), "the crossing keeps its east-west links and way" );
		Assert.AreEqual( (byte)0x40, state.Record( 34, 25 ).TrackNeighbours, "the mouth lets go of the crossing, which no longer links back" );

		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackOverlapCounter = 1 } );
		taken = ParkTrackTool.TakeBack( state, ride, (34, 27), (34, 27), 200, backToFirst: false );
		Assert.AreEqual( (1, 200), (taken.Removed, taken.Refunded), "a step of nought removes it whatever its counter" );
		Assert.AreEqual( 0, state.Record( 34, 27 ).TrackType );
	}

	/// <summary>
	/// A corner whose neighbour along the run is kept (laid over) still has its link back at its own clear: it lets it go
	/// and, left an end, leads out of it.
	/// </summary>
	[TestMethod]
	public void ABendLeftAnEndLeadsOutOfIt()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		state.SetRecord( 32, 29, state.Record( 32, 29 ) with { TrackOverlapCounter = 1 } );

		var taken = ParkTrackTool.TakeBack( state, ride, (28, 29), (34, 29), 200, backToFirst: false );

		Assert.AreEqual( (2, 400), (taken.Removed, taken.Refunded) );
		Assert.AreEqual( ((byte)0x01, (byte)0x10, (short)0, 1, 180), Piece( state, 34, 29 ) );
		Assert.AreEqual( ((byte)0x04, (short)0), (state.Record( 32, 29 ).TrackNeighbours, state.Record( 32, 29 ).TrackOverlapCounter) );
	}

	/// <summary>The mouth left one link takes the way away from its station, whatever way it had.</summary>
	[TestMethod]
	public void AClearedMouthLeadsAwayFromItsStation()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackDirection = 0x40 } );

		ParkTrackTool.TakeBack( state, ride, (34, 25), (34, 25), 200, backToFirst: false );

		Assert.AreEqual( ((byte)0x40, (byte)0x04), (state.Record( 34, 25 ).TrackNeighbours, state.Record( 34, 25 ).TrackDirection) );
	}

	/// <summary>Back at the list's first entry a mouth left a bend is broken at the link not to the station.</summary>
	[TestMethod]
	public void BackAtTheFirstEntryTheMouthsBendIsBroken()
	{
		var (state, ride) = Standing( 1, 30, 24 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		ParkTrackTool.TakeBack( state, ride, (34, 25), (34, 25), 200, backToFirst: false );
		Assert.AreEqual( (byte)0x50, state.Record( 34, 25 ).TrackNeighbours, "not at the first entry: the bend stays" );

		ParkTrackTool.TakeBack( state, ride, (34, 25), (34, 25), 200, backToFirst: true );
		Assert.AreEqual( ((byte)0x40, (byte)0x04), (state.Record( 34, 25 ).TrackNeighbours, state.Record( 34, 25 ).TrackDirection) );
		Assert.AreEqual( (byte)0x10, state.Record( 34, 27 ).TrackNeighbours, "its link back let go" );
		Assert.AreEqual( (short)-1, state.Record( 34, 25 ).TrackOverlapCounter, "the mouth's counter goes below nought and stays" );
	}

	/// <summary>
	/// The river takes a plain piece; a run may not end on queue, a ride's used-from cell or a cell flagged 0x20, but
	/// may end on its own track there; path or queue mid-run is the counted hill; types 7, 0x15 and 30 refuse.
	/// </summary>
	[TestMethod]
	public void TheRiverIsLaidOverAndARunMayNotEndOnQueue()
	{
		var state = Bought();
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { Type = 2 } );
		state.SetRecord( 35, 29, state.Record( 35, 29 ) with { Type = 2 } );
		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused, "over the river" );

		state = Bought();
		state.SetRecord( 35, 30, state.Record( 35, 30 ) with { Type = ParkRideChoice.QueueCellType } );
		var queue = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		Assert.IsTrue( queue.Refused && queue.Why!.Contains( "may not end" ), queue.Why );

		state = Bought();
		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { Flags = 0x20 } );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Why!.Contains( "may not end" ) );
		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 31), 200 ).Refused, "0x20 mid-run" );

		state = Bought();
		state.SetRecord( 35, 26, state.Record( 35, 26 ) with { Flags = 0x20 } );
		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (36, 25), (34, 25), 200 ).Refused, "its own track at the end" );

		state = Bought();
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { Type = ParkRideChoice.QueueCellType } );
		var hill = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		Assert.IsTrue( hill.Refused && hill.Why!.Contains( "hill" ), hill.Why );

		foreach ( var type in new[] { 7, 0x15, CellEdge.Approach } )
		{
			state = Bought();
			state.SetRecord( 34, 27, state.Record( 34, 27 ) with { Type = type } );
			Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused, $"map type {type}" );
		}
	}

	/// <summary>
	/// A run the balance cannot pay is refused as over the balance, and so is one refused for something else too: the total
	/// runs on past a refused cell, each empty cell's price added (<c>0x00539f5c</c>, <c>0x00539f87</c>..<c>0x00539fb6</c>).
	/// </summary>
	[TestMethod]
	public void ARunOverTheBalanceIsMarkedSoWhetherOrNotItIsRefusedForSomethingElse()
	{
		var state = Bought( balance: 500 );

		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 31), 200 ).OverBalance );
		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).OverBalance, "400 is under 500" );

		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { Type = CellEdge.Footprint } );
		var judged = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.IsTrue( judged.Refused && judged.OverBalance, "refused by the object, and over the balance too" );
		Assert.AreEqual( 600, judged.Total, "the cell over the object is added to the total" );
		Assert.IsTrue( judged.Why!.Contains( "map type 4" ), "the first reason is the one said: " + judged.Why );
		Assert.IsFalse( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).OverBalance, "refused, under the balance" );
	}

	/// <summary>The armed pointer: <c>c_track</c> over a run that may be laid, <c>c_cash</c> over the balance (which wins), <c>c_noplace</c> otherwise.</summary>
	[TestMethod]
	public void TheArmedPointerShowsTrackNoPlaceOrCash()
	{
		var (state, ride) = Standing( 1, 30, 24, 500 );

		Assert.AreEqual( Input.CursorTypes.Track, ParkTrackTool.CursorFor( state, ride, 200, 34, 29 ), "not anchored" );

		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );

		Assert.AreEqual( Input.CursorTypes.Track, ParkTrackTool.CursorFor( state, ride, 200, 34, 29 ) );
		Assert.AreEqual( Input.CursorTypes.Cash, ParkTrackTool.CursorFor( state, ride, 200, 34, 31 ), "600 is more than 500" );

		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { Type = CellEdge.Footprint } );

		Assert.AreEqual( Input.CursorTypes.NoGo, ParkTrackTool.CursorFor( state, ride, 200, 34, 29 ), "refused, and 400 is within the balance" );
		Assert.AreEqual( Input.CursorTypes.Cash, ParkTrackTool.CursorFor( state, ride, 200, 34, 31 ), "refused and over: the cash wins" );
	}
}
