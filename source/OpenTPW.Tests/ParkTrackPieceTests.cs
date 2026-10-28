using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The karts' and the water rides' track pieces - <see cref="ParkTrackPieces"/>: the piece each anchor takes, what a
/// bought ride lays, and what its sale clears (<c>docs/exe/park.md</c>, "A track piece's model, price and open end").
/// </summary>
[TestClass]
public class ParkTrackPieceTests
{
	/// <summary>
	/// Every kart anchor of Alexah's played Full Simulation Dino Karts (Lost Kingdom, written by the original): its links,
	/// way on and flags, and the row and turn its track record holds. Three carry the add-on flag 0x10.
	/// </summary>
	private static readonly (byte Mask, byte Direction, ushort Flags, int Row, int Turn)[] PlayedKartAnchors =
	[
		(0x14, 0x04, 0, 3, 0), (0x44, 0x04, 0, 2, 90), (0x44, 0x04, 0x10, 2, 90), (0x44, 0x04, 0, 2, 90), (0x44, 0x04, 0, 2, 90),
		(0x50, 0x10, 0, 3, 90), (0x11, 0x01, 0, 2, 0), (0x11, 0x10, 0, 2, 180), (0x14, 0x04, 0, 3, 0), (0x55, 0x05, 0, 4, 0),
		(0x44, 0x04, 0, 2, 90), (0x50, 0x10, 0, 3, 90), (0x14, 0x10, 0, 19, 0), (0x41, 0x40, 0, 3, 180), (0x11, 0x01, 0, 2, 0),
		(0x11, 0x01, 0, 2, 0), (0x11, 0x10, 0, 2, 180), (0x05, 0x04, 0, 19, 270), (0x44, 0x04, 0, 2, 90), (0x50, 0x10, 0, 3, 90),
		(0x11, 0x01, 0, 2, 0), (0x05, 0x01, 0, 3, 270), (0x44, 0x40, 0, 2, 270), (0x41, 0x40, 0, 3, 180), (0x11, 0x10, 0, 2, 180),
		(0x05, 0x01, 0, 3, 270), (0x44, 0x40, 0, 2, 270), (0x44, 0x40, 0x10, 2, 270), (0x44, 0x40, 0x10, 2, 270), (0x44, 0x40, 0, 2, 270),
		(0x41, 0x40, 0, 3, 180)
	];

	[TestMethod]
	public void EveryAnchorOfThePlayedKartsTakesTheRowAndTurnItsRecordHolds()
	{
		foreach ( var (mask, direction, flags, row, turn) in PlayedKartAnchors )
			Assert.AreEqual( (row, turn), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, mask, direction, flags ),
				$"mask 0x{mask:x2} direction 0x{direction:x2}" );
	}

	/// <summary>The original's water loop and put-down under Proton (Q687, <c>q587/orig/W-cells.log</c>).</summary>
	[TestMethod]
	public void TheWaterLoopsPiecesAreTheOriginals()
	{
		Assert.AreEqual( (19, 180), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x41, 0x01, 0 ) );
		Assert.AreEqual( (19, 90), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x50, 0x40, 0 ) );
		Assert.AreEqual( (19, 0), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x14, 0x10, 0 ) );
		Assert.AreEqual( (19, 270), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x05, 0x04, 0 ) );
		Assert.AreEqual( (2, 180), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x11, 0x10, 0 ) );
		Assert.AreEqual( (2, 270), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x44, 0x40, 0 ) );
		Assert.AreEqual( (1, 90), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x40, 0x04, 0 ) );
	}

	/// <summary>The decoded arms no file to hand reaches: the hills and a water crossing turned by its way on.</summary>
	[TestMethod]
	public void HillsAndWaterCrossingsTakeTheirDecodedRows()
	{
		Assert.AreEqual( (11, 180), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, 0x11, 0x10, 1 ) );
		Assert.AreEqual( (0x12, 90), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, 0x44, 0x04, 4 ) );
		Assert.AreEqual( (3, 0), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, 0x14, 0x04, 1 ), "a bend takes no hill" );
		Assert.AreEqual( (20, 270), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x55, 0x50, 0 ) );
		Assert.AreEqual( (4, 90), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x55, 0x14, 0 ) );
		Assert.AreEqual( (4, 0), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, 0x55, 0x50, 0 ), "the karts' crossing is not turned" );
		Assert.AreEqual( (20, 0), ParkTrackPieces.PieceFor( ParkTrackPieces.WaterAnchor, 0x55, 0x41, 0 ) );
		Assert.AreEqual( (14, 0), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, 0x11, 0x01, 2 ) );
		Assert.AreEqual( (16, 0), ParkTrackPieces.PieceFor( ParkTrackPieces.KartAnchor, 0x10, 0x01, 8 ) );
	}

	[TestMethod]
	public void EachRowDrawsItsKitsModel()
	{
		Assert.AreEqual( "gk_trckQ", ParkTrackPieces.ModelOf( ParkTrackPieces.KartAnchor, 1 ) );
		Assert.AreEqual( "gk_trckX", ParkTrackPieces.ModelOf( ParkTrackPieces.KartAnchor, 7 ) );
		Assert.AreEqual( "gk_trckB2", ParkTrackPieces.ModelOf( ParkTrackPieces.KartAnchor, 19 ) );
		Assert.IsNull( ParkTrackPieces.ModelOf( ParkTrackPieces.KartAnchor, 20 ), "the karts' kit stops at row 19" );
		Assert.AreEqual( "wr_trckX2", ParkTrackPieces.ModelOf( ParkTrackPieces.WaterAnchor, 22 ) );
		Assert.IsNull( ParkTrackPieces.ModelOf( ParkTrackPieces.WaterAnchor, 0 ) );
		Assert.AreEqual( 0x11u, ParkTrackPieces.MaskOf( 2 ) );
		Assert.AreEqual( 0x54u, ParkTrackPieces.MaskOf( 6 ) );
	}

	/// <summary>A played anchor at turn 90 is made one cell on in x at 270, as its model record (17102 at (17,9), 270).</summary>
	[TestMethod]
	public void APieceIsMadeOnTheCellOneOnByItsTurn()
	{
		var piece = ParkTrackPieces.PieceAt( 16, 9, new ParkWorld.MapCell( 0, 0, 0, 0, 0, 0, 0, 3, TrackType: 0xb,
			TrackNeighbours: 0x44, TrackDirection: 0x04, TrackTileSet: 3, TrackTileIndex: 2, TrackTileAngle: 90 ) );

		Assert.AreEqual( ("gk_trckS", 17, 9, 270), (piece!.Value.Model, piece.Value.CellX, piece.Value.CellY, piece.Value.Angle) );

		var turned = ParkTrackPieces.PieceAt( 24, 11, new ParkWorld.MapCell( 0, 0, 0, 0, 0, 0, 0, 3, TrackType: 0xb,
			TrackTileIndex: 2, TrackTileAngle: 180 ) );

		Assert.AreEqual( (25, 12, 180), (turned!.Value.CellX, turned.Value.CellY, turned.Value.Angle) );
		Assert.IsNull( ParkTrackPieces.PieceAt( 18, 9, new ParkWorld.MapCell( 0, 0, 0, 0, 0, 0, 0, 3, TrackType: 0xb,
			TrackFlags: 0x10, TrackTileIndex: 2, TrackTileAngle: 90 ) ), "an add-on's cell makes no kit piece" );
		Assert.IsNull( ParkTrackPieces.PieceAt( 22, 19, new ParkWorld.MapCell( 0, 0, 0, 0, 0, 0, 0, 3, TrackType: 0xd ) ),
			"a station is never a piece" );
	}

	/// <summary>
	/// A Dino Karts bought at turn 0 on its cell (30,24), as the original's on preview cell (32,25) (Q687's round O): the
	/// other mouth (28,25), the station (30,25) and (32,25), the mouth (34,25), every way on east.
	/// </summary>
	[TestMethod]
	public void ABoughtKartRideLaysItsStationAndMouthsAsTheOriginals()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 60000 );

		Assert.AreEqual( 4, ParkTrackPieces.LayStation( state, 1, 30, 24, 0 ) );

		var owner = (ushort)3103;

		Assert.AreEqual( (0xb, (byte)0x04, (byte)0x04, owner, 3, 1, 270), Fields( state, 28, 25 ) );
		Assert.AreEqual( (0xd, (byte)0x44, (byte)0x04, owner, 0, 0, 0), Fields( state, 30, 25 ) );
		Assert.AreEqual( (0xd, (byte)0x44, (byte)0x04, owner, 0, 0, 0), Fields( state, 32, 25 ) );
		Assert.AreEqual( (0xb, (byte)0x40, (byte)0x04, owner, 3, 1, 90), Fields( state, 34, 25 ) );

		foreach ( var (x, y) in new[] { (35, 25), (34, 26), (35, 26) } )
			Assert.AreEqual( (0xc, (ushort)MapStep.CellId( 34, 25 )), (state.Record( x, y ).TrackType, state.Record( x, y ).TrackParentId ) );

		Assert.AreEqual( 0, state.Record( 36, 25 ).TrackType );
		Assert.AreEqual( 0, state.Record( 34, 27 ).TrackType, "the 2 x 2 runs right and down" );
		Assert.AreEqual( 60000, state.Balance, "the put-down lays its track free" );
	}

	/// <summary>
	/// At turn 180 from (25,21), as Alexah's played Dino Karts lies: the station (24,19) and (22,19), the mouth (20,19), the
	/// other mouth (26,19), every way on west. And a Splish Splash from (18,18) at turn 0 as the original's (W1).
	/// </summary>
	[TestMethod]
	public void TheStationTurnsWithTheRide()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		ParkTrackPieces.LayStation( state, 1, 25, 21, 180 );

		Assert.AreEqual( (0xd, (byte)0x44, (byte)0x40), Head( state, 24, 19 ) );
		Assert.AreEqual( (0xd, (byte)0x44, (byte)0x40), Head( state, 22, 19 ) );
		Assert.AreEqual( (0xb, (byte)0x04, (byte)0x40), Head( state, 20, 19 ) );
		Assert.AreEqual( (0xb, (byte)0x40, (byte)0x40), Head( state, 26, 19 ) );

		ParkTrackPieces.LayStation( state, 1, 60, 60, 90 );

		Assert.AreEqual( (0xd, 0xd, 0xb, 0xb), (state.Record( 61, 59 ).TrackType, state.Record( 61, 57 ).TrackType,
			state.Record( 61, 55 ).TrackType, state.Record( 61, 61 ).TrackType ) );
		Assert.AreEqual( (byte)0x10, state.Record( 61, 55 ).TrackNeighbours, "the mouth at 90 links south, back to the station" );

		// Facing north the way on is 0x01; the mouth's link is the one back, south, and the other mouth's the way on.
		var north = (ushort)MapStep.CellId( 60, 60 );

		Assert.AreEqual( (0xb, (byte)0x01, (byte)0x01, north, 3, 1, 180), Fields( state, 61, 61 ), "the other mouth at 90" );
		Assert.AreEqual( (0xd, (byte)0x11, (byte)0x01, north, 0, 0, 0), Fields( state, 61, 59 ) );
		Assert.AreEqual( (0xd, (byte)0x11, (byte)0x01, north, 0, 0, 0), Fields( state, 61, 57 ) );
		Assert.AreEqual( (0xb, (byte)0x10, (byte)0x01, north, 3, 1, 0), Fields( state, 61, 55 ), "the mouth at 90" );

		ParkTrackPieces.LayStation( state, 1, 90, 60, 270 );

		Assert.AreEqual( (0xd, 0xd, 0xb, 0xb), (state.Record( 88, 60 ).TrackType, state.Record( 88, 62 ).TrackType,
			state.Record( 88, 64 ).TrackType, state.Record( 88, 58 ).TrackType ) );

		// Facing south the way on is 0x10: the mouth at (88,64) links north, back to the station, piece row 1 at 180; the
		// other mouth at (88,58) links south, row 1 at 0.
		var south = (ushort)MapStep.CellId( 90, 60 );

		Assert.AreEqual( (0xb, (byte)0x01, (byte)0x10, south, 3, 1, 180), Fields( state, 88, 64 ), "the mouth at 270" );
		Assert.AreEqual( (0xb, (byte)0x10, (byte)0x10, south, 3, 1, 0), Fields( state, 88, 58 ), "the other mouth at 270" );
		Assert.AreEqual( (0xd, (byte)0x11, (byte)0x10, south, 0, 0, 0), Fields( state, 88, 60 ) );
		Assert.AreEqual( (0xd, (byte)0x11, (byte)0x10, south, 0, 0, 0), Fields( state, 88, 62 ) );

		state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		ParkTrackPieces.LayStation( state, 2, 18, 18, 0 );

		Assert.AreEqual( (0x10, (byte)0x40, (byte)0x04, (ushort)MapStep.CellId( 18, 18 ), 4, 1, 90), Fields( state, 22, 19 ) );
		Assert.AreEqual( (0x10, (byte)0x04, (byte)0x04, (ushort)MapStep.CellId( 18, 18 ), 4, 1, 270), Fields( state, 16, 19 ) );
		Assert.AreEqual( 0x12, state.Record( 18, 19 ).TrackType );
		Assert.AreEqual( 0xc, state.Record( 23, 20 ).TrackType, "op 0x83 stamps a water piece's other cells 0xc too (0x00538895)" );
	}

	[TestMethod]
	public void SellingABareStationClearsItAndPaysNothingNet()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 );

		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );

		Assert.AreEqual( 0, ParkTrackPieces.SellBareStation( state, 1, 30, 24, 200 ) );

		for ( var x = 26; x <= 37; ++x )
		{
			for ( var y = 23; y <= 28; ++y )
				Assert.AreEqual( (0, (ushort)0), (state.Record( x, y ).TrackType, state.Record( x, y ).TrackParentId ), $"({x},{y})" );
		}

		Assert.AreEqual( 1000, state.Balance );
	}

	/// <summary>A ride whose mouth a run has been laid from owns more than it was bought with, and is not cleared here.</summary>
	[TestMethod]
	public void ARideWithTrackLaidIsNotClearedAsABareOne()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 1000 );

		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackNeighbours = 0x50, TrackDirection = 0x10 } );

		Assert.IsNull( ParkTrackPieces.SellBareStation( state, 1, 30, 24, 200 ) );
		Assert.AreEqual( 0xb, state.Record( 34, 25 ).TrackType );

		var other = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		ParkTrackPieces.LayStation( other, 1, 30, 24, 0 );
		other.SetRecord( 34, 27, other.Record( 34, 27 ) with { TrackType = 0xb, TrackParentId = 3103, TrackNeighbours = 0x01 } );

		Assert.IsNull( ParkTrackPieces.SellBareStation( other, 1, 30, 24, 200 ), "a fifth anchor" );

		var short_ = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		ParkTrackPieces.LayStation( short_, 1, 30, 24, 0 );
		short_.SetRecord( 30, 25, short_.Record( 30, 25 ) with { TrackType = 0, TrackParentId = 0 } );

		Assert.IsNull( ParkTrackPieces.SellBareStation( short_, 1, 30, 24, 200 ), "a station section short" );
		Assert.AreEqual( 0xb, short_.Record( 34, 25 ).TrackType );
	}

	/// <summary>A straight's two end caps and an end's one, by the hide words of the karts' kit.</summary>
	[TestMethod]
	public void AStraightHidesBothEndCapsAndAnEndOne()
	{
		var data = GameData.Required();

		int Caps( string model, uint mask )
			=> new ModelFile( new MemoryStream( data.ReadAllBytes( $"levels/jungle/rides/gokarts/{model}.md2" ) ) ).Nodes
				.Count( node => (node.IdHide & mask) != 0 );

		Assert.AreEqual( 2, Caps( "gk_trckS", ParkTrackPieces.MaskOf( 2 ) ) );
		Assert.AreEqual( 1, Caps( "gk_trckQ", ParkTrackPieces.MaskOf( 1 ) ) );
		Assert.AreEqual( 0, Caps( "gk_trckB", ParkTrackPieces.MaskOf( 3 ) ) );
	}

	/// <summary>
	/// A straight's two end caps and an end's one, from the mask a standing piece asks its model to hide (<c>CapMaskOf</c>:
	/// the row its anchor's record holds): the model of a straight laid by a run, <c>gk_trckS</c>, has two nodes the mask
	/// hides, and its end piece <c>gk_trckQ</c> one.
	/// </summary>
	[TestMethod]
	public void APlacedStraightHidesItsTwoCapNodes()
	{
		var data = GameData.Required();
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 60000 );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );
		ParkTrackTool.LayRun( state, 0xb, (ushort)MapStep.CellId( 30, 24 ), (34, 25), (34, 29), 200 );

		int Hidden( int x, int y )
		{
			var piece = ParkTrackPieces.PieceAt( x, y, state.Record( x, y ) )!.Value;

			return new ModelFile( new MemoryStream( data.ReadAllBytes( $"levels/jungle/rides/gokarts/{piece.Model}.md2" ) ) ).Nodes
				.Count( node => (node.IdHide & ParkTrackPieces.CapMaskOf( piece )) != 0 );
		}

		Assert.AreEqual( "gk_trckS", ParkTrackPieces.PieceAt( 34, 27, state.Record( 34, 27 ) )!.Value.Model );
		Assert.AreEqual( 2, Hidden( 34, 27 ), "a straight" );
		Assert.AreEqual( 1, Hidden( 34, 29 ), "an end" );
		Assert.AreEqual( 0, Hidden( 34, 25 ), "a bend" );
	}

	/// <summary>
	/// The track record's four fields as the stock park holds them, read through <see cref="ParkWorld"/>: its 143 boundary
	/// anchors (track type 25) of family 5, their ways on, flags, turns and rows as the independent reader <c>parkfile.py -B</c>
	/// counts them in <c>Easymode.TPWI</c> (Q620).
	/// </summary>
	[TestMethod]
	public void TheStockParksBoundaryAnchorsReadTheirWayOnTileWordsAndFlags()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var anchors = new ParkWorld( new SaveReader( stream ).ReadFile() ).Cells.Where( cell => cell.TrackType == 25 ).ToList();

		Assert.AreEqual( 143, anchors.Count );
		Assert.IsTrue( anchors.All( cell => cell.TrackTileSet == 5 ), "family 5" );

		// (way on, flags, turn): a boundary's links are its own field (mHoardingNeighbours), which the track fields here do not read.
		var byWord = anchors.GroupBy( cell => (cell.TrackDirection, cell.TrackFlags, cell.TrackTileAngle) )
			.ToDictionary( group => group.Key, group => group.Count() );
		var expected = new Dictionary<(byte, ushort, int), int>
		{
			[(1, 0, 0)] = 21, [(1, 0, 180)] = 2, [(1, 0, 270)] = 4, [(4, 0, 0)] = 3, [(4, 0, 90)] = 38, [(4, 0, 270)] = 2,
			[(16, 0, 0)] = 1, [(16, 0, 90)] = 3, [(16, 0, 180)] = 23, [(64, 0, 90)] = 3, [(64, 0, 180)] = 2, [(64, 0, 270)] = 40,
			[(64, 1, 270)] = 1
		};

		CollectionAssert.AreEquivalent( expected.ToList(), byWord.ToList() );

		var rows = anchors.GroupBy( cell => cell.TrackTileIndex ).ToDictionary( group => group.Key, group => group.Count() );

		CollectionAssert.AreEquivalent( new Dictionary<int, int> { [2] = 90, [22] = 19, [25] = 13, [3] = 12, [19] = 8, [11] = 1 }.ToList(), rows.ToList() );
	}

	/// <summary>
	/// A laid station and a run written over the park file and read back: every track field of each cell the same, the
	/// way on, the three tile words and the overlap counter included.
	/// </summary>
	[TestMethod]
	public void ALaidStationAndRunsTrackFieldsWriteAndReadBack()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var shipped = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( shipped );

		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );
		ParkTrackTool.LayRun( state, 0xb, (ushort)MapStep.CellId( 30, 24 ), (34, 25), (34, 29), 200 );

		var cells = new Dictionary<int, ParkWorld.MapCell>();

		for ( var y = 23; y <= 30; ++y )
		{
			for ( var x = 26; x <= 37; ++x )
				cells[(y * ParkWorld.MapSize) + x] = state.Record( x, y );
		}

		var body = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0, shipped.Camera.Saved!.Value, Cells: cells ) );
		var written = new ParkWorld( body );

		Assert.AreEqual( 0xb, written.CellAt( 34, 25 ).TrackType );

		foreach ( var (index, cell) in cells )
		{
			var back = written.Cells[index];

			Assert.AreEqual( (cell.TrackType, cell.TrackNeighbours, cell.TrackDirection, cell.TrackFlags, cell.TrackParentId, cell.TrackTileSet, cell.TrackTileIndex, cell.TrackTileAngle, cell.TrackOverlapCounter),
				(back.TrackType, back.TrackNeighbours, back.TrackDirection, back.TrackFlags, back.TrackParentId, back.TrackTileSet, back.TrackTileIndex, back.TrackTileAngle, back.TrackOverlapCounter),
				$"cell ({index % ParkWorld.MapSize},{index / ParkWorld.MapSize})" );
		}

		Assert.AreEqual( ((byte)0x50, (byte)0x10, (short)1, 3, 90),
			(written.CellAt( 34, 25 ).TrackNeighbours, written.CellAt( 34, 25 ).TrackDirection, written.CellAt( 34, 25 ).TrackOverlapCounter,
				written.CellAt( 34, 25 ).TrackTileIndex, written.CellAt( 34, 25 ).TrackTileAngle ) );
	}

	private static (int Type, byte Mask, byte Direction) Head( ParkState state, int x, int y )
		=> (state.Record( x, y ).TrackType, state.Record( x, y ).TrackNeighbours, state.Record( x, y ).TrackDirection);

	private static (int Type, byte Mask, byte Direction, ushort Owner, int Family, int Row, int Turn) Fields( ParkState state, int x, int y )
	{
		var cell = state.Record( x, y );

		return (cell.TrackType, cell.TrackNeighbours, cell.TrackDirection, cell.TrackParentId, cell.TrackTileSet, cell.TrackTileIndex,
			cell.TrackTileAngle);
	}

	/// <summary>A bought ride's anchor whose two by two holds track already is not laid, so the other ride's track stands.</summary>
	[TestMethod]
	public void APutDownLeavesTrackAlreadyLaid()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 0 );
		var other = (ushort)MapStep.CellId( 50, 50 );

		state.SetRecord( 35, 26, state.Record( 35, 26 ) with { TrackType = 0xc, TrackParentId = (ushort)MapStep.CellId( 35, 25 ) } );
		state.SetRecord( 35, 25, state.Record( 35, 25 ) with { TrackType = 0x19, TrackParentId = other } );

		Assert.AreEqual( 3, ParkTrackPieces.LayStation( state, 1, 30, 24, 0 ), "the mouth (34,25) is not laid" );
		Assert.AreEqual( (0, 0x19, 0xc), (state.Record( 34, 25 ).TrackType, state.Record( 35, 25 ).TrackType, state.Record( 35, 26 ).TrackType) );
		Assert.AreEqual( 0xd, state.Record( 32, 25 ).TrackType );
	}

	/// <summary>
	/// Each of Lost Kingdom's three add-ons has its two end caps where <c>FUN_0044b3c0( instance, 1, side )</c> finds them,
	/// by the lookup records' hide words (an id walk would find none: both caps are id 0), and an add-on's anchor takes no
	/// kit piece, its own model standing there instead.
	/// </summary>
	[TestMethod]
	public void TheAddOnsCapsAreFoundByTheirHideWords()
	{
		var data = GameData.Required();

		ModelFile Model( string stem ) => new( new MemoryStream( data.ReadAllBytes( $"levels/jungle/upgrades/{stem}/{stem}.md2" ) ) );

		Assert.AreEqual( (4, 5), (Model( "lavajump" ).FindNodeByHide( 0x01, 1 ), Model( "lavajump" ).FindNodeByHide( 0x10, 1 )), "gk_end02, gk_end01" );
		Assert.AreEqual( (3, 4), (Model( "mammtunn" ).FindNodeByHide( 0x01, 1 ), Model( "mammtunn" ).FindNodeByHide( 0x10, 1 )), "gk_end09, gk_end08" );
		Assert.AreEqual( (8, 6), (Model( "watertun" ).FindNodeByHide( 0x01, 1 ), Model( "watertun" ).FindNodeByHide( 0x10, 1 )), "wr_end03, wr_end02" );
		Assert.AreEqual( -1, Model( "mammtunn" ).FindNodeByHide( 0x04, 1 ), "no cap of 4" );

		Assert.IsNull( ParkTrackPieces.PieceAt( 18, 9, new ParkWorld.MapCell( 4, 0x10, 0, 0, 0, 8, 0, 3, TrackType: 0xb,
			TrackFlags: ParkTrackPieces.AddOnFlag, TrackNeighbours: 0x44, TrackDirection: 0x04, TrackTileSet: 3, TrackTileIndex: 2,
			TrackTileAngle: 90 ) ), "the played file's jump anchor (18,9): row 2, but an add-on" );
	}

	/// <summary>
	/// A loaded add-on's caps are its file's node words' hidden bits: the played file's tunnel (item 1501) holds 0x401 on both
	/// caps, its jump (1500) nought, as the original shows them (<c>q778/orig/O1</c>: 0xc0000111 and 0x40000101).
	/// </summary>
	[TestMethod]
	public void ALoadedAddOnsCapsAreItsFilesNodeWords()
	{
		var data = GameData.Required();

		ModelFile Model( string stem ) => new( new MemoryStream( data.ReadAllBytes( $"levels/jungle/upgrades/{stem}/{stem}.md2" ) ) );

		CollectionAssert.AreEqual( new[] { (0x01u, 3, true), (0x10u, 4, true) },
			ParkObjects.SavedCaps( Model( "mammtunn" ), [0, 0xa2, 0, 0x401, 0x401, 0x601] ).ToArray(), "the tunnel's" );
		CollectionAssert.AreEqual( new[] { (0x01u, 4, false), (0x10u, 5, false) },
			ParkObjects.SavedCaps( Model( "lavajump" ), [0, 0, 0, 0, 0, 0, 0x601, 0x601, 0x601, 0x601] ).ToArray(), "the jump's" );
		CollectionAssert.AreEqual( new[] { (0x01u, 3, false), (0x10u, 4, true) },
			ParkObjects.SavedCaps( Model( "mammtunn" ), [0x1, 0x1, 0x1, 0x400, 0x401] ).ToArray(), "each cap its own word's bit" );
		Assert.AreEqual( 1, ParkObjects.SavedCaps( Model( "mammtunn" ), [0, 0, 0, 0] ).Count(), "a cap with no word is left" );
	}

	/// <summary>
	/// What the walk's bits and a load's words do to the caps' visibility, as the code that lays them does it
	/// (<c>ParkObjects.CapsFor</c>, <c>LoadedCaps</c>, <c>ApplyCaps</c>): on the tunnel, bit 0x01 hides the cap of word 1 (node 3)
	/// and bit 0x10 the cap of word 0x10 (node 4); a hidden cap is set not visible.
	/// </summary>
	[TestMethod]
	public void TheWalksBitsAndTheLoadsWordsChooseWhichCapIsHidden()
	{
		var data = GameData.Required();
		var tunnel = new ModelFile( new MemoryStream( data.ReadAllBytes( "levels/jungle/upgrades/mammtunn/mammtunn.md2" ) ) );

		CollectionAssert.AreEqual( new[] { (0x01u, 3, true), (0x10u, 4, false) }, ParkObjects.CapsFor( tunnel, 0x01 ).ToArray(), "bit 1" );
		CollectionAssert.AreEqual( new[] { (0x01u, 3, false), (0x10u, 4, true) }, ParkObjects.CapsFor( tunnel, 0x10 ).ToArray(), "bit 0x10" );
		CollectionAssert.AreEqual( new[] { (0x01u, 3, true), (0x10u, 4, true) }, ParkObjects.CapsFor( tunnel, 0x11 ).ToArray(), "both" );

		var shown = new Dictionary<int, bool>();
		ParkObjects.ApplyCaps( ( node, visible ) => shown[node] = visible, ParkObjects.CapsFor( tunnel, 0x01 ) );
		Assert.AreEqual( false, shown[3], "the hidden cap is not visible" );
		Assert.AreEqual( true, shown[4], "the other is" );

		uint[] words = [0, 0, 0, 0x401, 0x400];
		var saved = new SavedThing( 1501, 0, [], NodeWords: words );
		CollectionAssert.AreEqual( new[] { (0x01u, 3, true), (0x10u, 4, false) }, ParkObjects.LoadedCaps( tunnel, saved, 1501 ).ToArray(), "its own item" );
		Assert.AreEqual( 0, ParkObjects.LoadedCaps( tunnel, saved, 1500 ).Count(), "another item's record lays nothing" );
	}
}
