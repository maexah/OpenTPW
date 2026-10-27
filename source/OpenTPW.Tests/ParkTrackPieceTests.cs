using Microsoft.VisualStudio.TestTools.UnitTesting;
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

		ParkTrackPieces.LayStation( state, 1, 90, 60, 270 );

		Assert.AreEqual( (0xd, 0xd, 0xb, 0xb), (state.Record( 88, 60 ).TrackType, state.Record( 88, 62 ).TrackType,
			state.Record( 88, 64 ).TrackType, state.Record( 88, 58 ).TrackType ) );

		ParkTrackPieces.LayStation( state, 2, 18, 18, 0 );

		Assert.AreEqual( (0x10, (byte)0x40, (byte)0x04, (ushort)MapStep.CellId( 18, 18 ), 4, 1, 90), Fields( state, 22, 19 ) );
		Assert.AreEqual( (0x10, (byte)0x04, (byte)0x04, (ushort)MapStep.CellId( 18, 18 ), 4, 1, 270), Fields( state, 16, 19 ) );
		Assert.AreEqual( 0x12, state.Record( 18, 19 ).TrackType );
		Assert.AreEqual( 0x11, state.Record( 23, 20 ).TrackType );
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

	private static (int Type, byte Mask, byte Direction) Head( ParkState state, int x, int y )
		=> (state.Record( x, y ).TrackType, state.Record( x, y ).TrackNeighbours, state.Record( x, y ).TrackDirection);

	private static (int Type, byte Mask, byte Direction, ushort Owner, int Family, int Row, int Turn) Fields( ParkState state, int x, int y )
	{
		var cell = state.Record( x, y );

		return (cell.TrackType, cell.TrackNeighbours, cell.TrackDirection, cell.TrackParentId, cell.TrackTileSet, cell.TrackTileIndex,
			cell.TrackTileAngle);
	}
}
