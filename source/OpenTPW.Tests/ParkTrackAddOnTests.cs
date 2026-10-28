using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A track add-on's put-down - <see cref="ParkTrackAddOn"/>: the preview's snap to the ride's grid (<c>0x005236b0</c>), the
/// verdict on its track cells (<c>0x0053594e</c>) and the commit's stamp (<c>0x0052950d</c>). The ride and its track are
/// Alexah's played Full Simulation Dino Karts (<c>New Save.TPWS</c>): the ride's cell (25,21) at 180, its Jump anchored at
/// (19,9) turned 270 over (18..19, 9..10) and its Tunnel at (17,21) turned 90 over (16..19, 19..21), the anchors under them
/// (18,9) way on 4 and (16,19), (18,19) way on 0x40 (<c>q778</c>'s <c>cells.py</c>).
/// </summary>
[TestClass]
public class ParkTrackAddOnTests
{
	private const int RideX = 25, RideY = 21;

	private static readonly ushort Owner = (ushort)MapStep.CellId( RideX, RideY );

	private static readonly ParkWorld.CatalogueObject Ride = new( 289, 1150, RideX << 8, RideY << 8, 180 );

	/// <summary>A kart straight's anchor at (x, y) of the ride, linked both ways along its way on, and its three other cells.</summary>
	private static void Straight( ParkState state, int x, int y, byte way, ushort owner = 0 )
	{
		var links = (byte)(way is 0x01 or 0x10 ? 0x11 : 0x44);

		state.SetRecord( x, y, state.Record( x, y ) with
		{
			TrackType = ParkTrackPieces.KartAnchor, TrackDirection = way, TrackNeighbours = links,
			TrackParentId = owner == 0 ? Owner : owner, TrackFlags = 0x3
		} );

		foreach ( var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1) } )
			state.SetRecord( x + dx, y + dy, state.Record( x + dx, y + dy ) with
			{
				TrackType = ParkTrackPieces.KartCell, TrackParentId = (ushort)MapStep.CellId( x, y )
			} );
	}

	private static ParkState Track()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 50000 );

		for ( var x = 14; x <= 20; x += 2 )
			Straight( state, x, 9, 0x04 );

		for ( var x = 14; x <= 20; x += 2 )
			Straight( state, x, 19, 0x40 );

		state.AddObject( Ride );

		return state;
	}

	/// <summary>An add-on made up for the arithmetic: two by two, all track, its four adjusts each distinct.</summary>
	private static ParkItemCatalogue.Item MadeUp( int offsetY = 0 )
		=> new( 1599, "made up", "", "", 2, 2, null, Shape: new ItemShape( 2, 2, [[0xb, 0xb], [0xb, 0xb]] ),
			BumperAdjusts: [(1, 2), (3, 4), (5, 6), (7, 8)], MapOffsetY: offsetY, AddOnUpgradesId: 1150, AddOnUpgradeType: 1,
			OverwritePriority: 5 );

	/// <summary>
	/// The snap: the grid takes the floor of half the distance (-7 to -8, where truncation would give -6); the way on 4 turns it
	/// 270 and adds the EAST adjust, and 0x40 turns it 90 and adds the WEST one (the snap's own table, where the bumper
	/// placer's answers the other way, <see cref="ParkItemCatalogue.Item.BumperAdjustAt"/>); the ride's turn of 180 moves it one
	/// west and one south.
	/// </summary>
	[TestMethod]
	public void TheSnapTakesTheGridTheWayOnTheAdjustAndTheRidesTurn()
	{
		var state = Track();
		var item = MadeUp();

		// (18,10) is 7 west and 11 north of the ride: the grid's (17,9), the other cell of (16,9), way on 4. East (3,4), then
		// one west and one south: (19,14). Truncation would put it on (19,11) and so (21,14).
		var angle = 0;
		Assert.AreEqual( (19, 14), ParkTrackAddOn.Snap( state, item, Ride, 18, 10, ref angle ) );
		Assert.AreEqual( 270, angle );

		// (18,20): the grid's (17,19), the other cell of (16,19), way on 0x40, so 90 and the West adjust (7,8).
		angle = 0;
		Assert.AreEqual( (23, 28), ParkTrackAddOn.Snap( state, item, Ride, 18, 20, ref angle ) );
		Assert.AreEqual( 90, angle );

		// Off the track, (23,31): the angle kept, and the adjust the kept angle's, South (5,6).
		angle = 180;
		Assert.AreEqual( (27, 38), ParkTrackAddOn.Snap( state, item, Ride, 24, 32, ref angle ) );
		Assert.AreEqual( 180, angle );

		// From (19,9) (East (3,4) to (22,13)): a ride turned 90 moves it one west only, 270 one south only, 0 not at all.
		angle = 0;
		Assert.AreEqual( (21, 13), ParkTrackAddOn.Snap( state, item, Ride with { Angle = 90 }, 19, 9, ref angle ) );
		angle = 0;
		Assert.AreEqual( (22, 14), ParkTrackAddOn.Snap( state, item, Ride with { Angle = 270 }, 20, 10, ref angle ) );
		angle = 0;
		Assert.AreEqual( (22, 13), ParkTrackAddOn.Snap( state, item, Ride with { Angle = 0 }, 20, 9, ref angle ) );
	}

	/// <summary>The four ways on and the angles they give; any other way on keeps the angle.</summary>
	[TestMethod]
	public void EachWayOnGivesItsAngle()
	{
		var state = Track();
		var item = MadeUp();

		foreach ( var (way, expected) in new[] { ((byte)0x01, 0), ((byte)0x04, 270), ((byte)0x10, 180), ((byte)0x40, 90), ((byte)0x02, 45) } )
		{
			state.SetRecord( 18, 9, state.Record( 18, 9 ) with { TrackDirection = way } );

			var angle = 45;
			ParkTrackAddOn.Snap( state, item, Ride, 19, 9, ref angle );

			Assert.AreEqual( expected, angle, $"way on 0x{way:x}" );
		}
	}

	/// <summary>
	/// Alexah's played Jump and Tunnel are where the decoded snap puts them: a pointer on (19..20, 9..10) gives the Jump's
	/// anchor (19,9) turned 270, one on (17..18, 19..20) the Tunnel's (17,21) turned 90, and their cells are the file's.
	/// </summary>
	[TestMethod]
	public void ThePlayedFilesAddOnsStandWhereTheSnapPutsThem()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: false );
		var state = Track();

		Assert.IsTrue( catalogue.TryGet( 1500, out var jump ) );
		Assert.IsTrue( catalogue.TryGet( 1501, out var tunnel ) );

		foreach ( var (x, y) in new[] { (19, 9), (20, 9), (19, 10), (20, 10) } )
		{
			var angle = 0;
			var (preview, anchor) = ParkTrackAddOn.Under( state, jump, Ride, x, y, ref angle );

			Assert.AreEqual( ((18, 10), (19, 9), 270), (preview, anchor, angle ), $"the Jump from ({x},{y})" );
		}

		CollectionAssert.AreEquivalent( new[] { (18, 9, 0xb), (19, 9, 0xb), (18, 10, 0xb), (19, 10, 0xb) },
			ParkTrackAddOn.Cells( jump, 19, 9, 270 ).ToArray() );

		foreach ( var (x, y) in new[] { (17, 19), (18, 19), (17, 20), (18, 20) } )
		{
			var angle = 0;
			var (preview, anchor) = ParkTrackAddOn.Under( state, tunnel, Ride, x, y, ref angle );

			Assert.AreEqual( ((18, 20), (17, 21), 90), (preview, anchor, angle), $"the Tunnel from ({x},{y})" );
		}

		var cells = ParkTrackAddOn.Cells( tunnel, 17, 21, 90 ).ToArray();

		CollectionAssert.AreEquivalent( new[] { (17, 21), (18, 21) }, cells.Where( c => c.Kind == 4 ).Select( c => (c.X, c.Y) ).ToArray(),
			"its two body cells; (16,21) and (19,21) are its '.' and are left alone" );
		CollectionAssert.AreEquivalent( Enumerable.Range( 16, 4 ).SelectMany( x => new[] { (x, 19), (x, 20) } ).ToArray(),
			cells.Where( c => ParkTrackAddOn.IsTrackKind( c.Kind ) ).Select( c => (c.X, c.Y) ).ToArray() );

		Assert.IsNull( ParkBuilding.PlacementRefusal( state, tunnel, 17, 21, 90, Ride.ThingId ), "it goes down there" );
		Assert.IsNull( ParkBuilding.PlacementRefusal( state, jump, 19, 9, 270, Ride.ThingId ), "and so does the Jump" );
		Assert.IsNotNull( ParkBuilding.PlacementRefusal( state, jump, 19, 9, 270 ), "but for no ride named" );
	}

	/// <summary>The verdict on a track cell, each refusal on its own (and the one cell that passes).</summary>
	[TestMethod]
	public void ATrackCellPassesOnlyOnTheRidesOwnStraight()
	{
		var state = Track();

		Assert.IsNull( ParkTrackAddOn.TrackCellRefusal( state, Ride, 5, 18, 9 ), "the ride's straight" );
		Assert.IsNull( ParkTrackAddOn.TrackCellRefusal( state, Ride, 5, 19, 10 ), "its other cell, by its anchor" );
		Assert.AreEqual( "has no ride named for the add-on", ParkTrackAddOn.TrackCellRefusal( state, null, 5, 18, 9 ) );
		Assert.AreEqual( "is not track", ParkTrackAddOn.TrackCellRefusal( state, Ride, 5, 30, 30 ) );

		var other = Track();
		Straight( other, 18, 9, 0x04, owner: (ushort)MapStep.CellId( 40, 40 ) );
		Assert.AreEqual( "is another ride's track", ParkTrackAddOn.TrackCellRefusal( other, Ride, 5, 19, 10 ) );

		var bend = Track();
		bend.SetRecord( 18, 9, bend.Record( 18, 9 ) with { TrackNeighbours = 0x14 } );
		Assert.AreEqual( "is a bend or a crossing of the track", ParkTrackAddOn.TrackCellRefusal( bend, Ride, 5, 19, 9 ) );

		var crossing = Track();
		crossing.SetRecord( 18, 9, crossing.Record( 18, 9 ) with { TrackNeighbours = 0x55 } );
		Assert.AreEqual( "is a bend or a crossing of the track", ParkTrackAddOn.TrackCellRefusal( crossing, Ride, 5, 18, 9 ) );

		var flagged = Track();
		flagged.SetRecord( 19, 9, flagged.Record( 19, 9 ) with { Flags = ParkTrackPieces.AddOnFlag } );
		Assert.AreEqual( "has an add-on already", ParkTrackAddOn.TrackCellRefusal( flagged, Ride, 5, 19, 9 ) );

		var outside = Track();
		outside.SetRecord( 19, 9, outside.Record( 19, 9 ) with { Flags = ParkPathBuilding.OutsideThePark } );
		Assert.AreEqual( "is outside the park", ParkTrackAddOn.TrackCellRefusal( outside, Ride, 5, 19, 9 ) );

		var queued = Track();
		queued.SetRecord( 19, 9, queued.Record( 19, 9 ) with { Type = ParkRideChoice.QueueCellType } );
		Assert.AreEqual( "is a queue", ParkTrackAddOn.TrackCellRefusal( queued, Ride, 5, 19, 9 ) );

		var boundary = Track();
		boundary.SetRecord( 18, 9, boundary.Record( 18, 9 ) with { TrackType = ParkTrackPieces.Boundary } );
		Assert.AreEqual( "is the boundary's", ParkTrackAddOn.TrackCellRefusal( boundary, Ride, 5, 19, 9 ) );

		// A station is track to the verdict.
		var station = Track();
		station.SetRecord( 18, 9, station.Record( 18, 9 ) with { TrackType = ParkTrackPieces.KartStation } );
		Assert.IsNull( ParkTrackAddOn.TrackCellRefusal( station, Ride, 5, 19, 9 ) );
	}

	/// <summary>
	/// A thing's cell under a track cell passes the verdict only below the add-on's priority, and the test pass refuses a
	/// thing's cell (map type 4) whatever its priority; a body cell over track passes.
	/// </summary>
	[TestMethod]
	public void AThingsCellRefusesATrackCellAndNotABodyCellOverTrack()
	{
		var state = Track();
		var priority = ParkTrackTool.OverwritePriorityOf;

		try
		{
			ParkTrackTool.OverwritePriorityOf = ( _, _ ) => 1;
			state.SetRecord( 19, 9, state.Record( 19, 9 ) with { Type = CellEdge.Footprint, ParentId = 77 } );

			Assert.IsNull( ParkTrackAddOn.TrackCellRefusal( state, Ride, 5, 19, 9 ), "the verdict lets a lower thing go" );
			StringAssert.Contains( ParkBuilding.PlacementRefusal( state, MadeUp(), 19, 9, 0, Ride.ThingId ), "(19,9) is a thing's cell" );

			ParkTrackTool.OverwritePriorityOf = ( _, _ ) => 5;
			Assert.AreEqual( "is another thing's, which the add-on may not build over", ParkTrackAddOn.TrackCellRefusal( state, Ride, 5, 19, 9 ) );
		}
		finally
		{
			ParkTrackTool.OverwritePriorityOf = priority;
		}

		// A body kind over track: a made-up add-on all body, over the ride's own track, refuses nothing; but the verdict's
		// first answers stand before the track's, so over another add-on's cell, or over land outside the park, it is refused.
		var body = MadeUp() with { Shape = new ItemShape( 2, 2, [[4, 4], [4, 4]] ) };
		var over = Track();
		Assert.IsNull( ParkBuilding.PlacementRefusal( over, body, 18, 9, 0, Ride.ThingId ) );

		over.SetRecord( 19, 10, over.Record( 19, 10 ) with { Type = CellEdge.Footprint, ParentId = 77 } );
		ParkTrackTool.OverwritePriorityOf = ( _, _ ) => 5;

		try
		{
			StringAssert.Contains( ParkBuilding.PlacementRefusal( over, body, 18, 9, 0, Ride.ThingId ), "(19,10) is another thing's" );
		}
		finally
		{
			ParkTrackTool.OverwritePriorityOf = priority;
		}

		var outside = Track();
		outside.SetRecord( 18, 10, outside.Record( 18, 10 ) with { Flags = ParkPathBuilding.OutsideThePark } );
		StringAssert.Contains( ParkBuilding.PlacementRefusal( outside, body, 18, 9, 0, Ride.ThingId ), "(18,10) is outside the park" );
	}

	/// <summary>
	/// The commit's stamp: every cell but '.' a thing's cell owned by the anchor, each track cell's map and track flags written
	/// 0x10, whatever they held (FUN_0053a510 writes the word; the file's under the Jump and the Tunnel), the track flags' low four bits gone, the track's type, links and way
	/// on kept.
	/// </summary>
	[TestMethod]
	public void TheStampFlagsTheTrackCellsAndKeepsTheTrack()
	{
		var state = Track();
		var item = MadeUp( offsetY: 1 ) with { Shape = new ItemShape( 2, 3, [[0, 4], [0xb, 0xb], [0xb, 0xb]] ) };

		state.SetRecord( 19, 9, state.Record( 19, 9 ) with { Flags = 0x20, TrackFlags = 0x13 } );
		state.SetRecord( 18, 10, state.Record( 18, 10 ) with { Flags = 0x20, TrackFlags = 0x103 } );

		// Anchored at (18,10) turned 0, its picture starts a row back, at (18,9) (its map offset of one row).
		Assert.AreEqual( 4, ParkTrackAddOn.Stamp( state, item, 18, 10, 0 ) );

		var owner = (ushort)MapStep.CellId( 18, 10 );

		foreach ( var (x, y) in new[] { (18, 10), (19, 10), (18, 11), (19, 11) } )
		{
			var cell = state.Record( x, y );

			Assert.AreEqual( (CellEdge.Footprint, owner, (ushort)0x10, (ushort)0x10), (cell.Type, cell.ParentId, cell.Flags, cell.TrackFlags), $"({x},{y})" );
		}

		Assert.AreEqual( (CellEdge.Footprint, owner, (ushort)0x20), (state.Record( 19, 9 ).Type, state.Record( 19, 9 ).ParentId, state.Record( 19, 9 ).Flags ),
			"the body cell is a thing's, its flags as they were" );
		Assert.AreEqual( (ushort)0x10, state.Record( 19, 9 ).TrackFlags, "its track flags lose their low four bits" );
		Assert.AreEqual( (0, (ushort)0), (state.Record( 18, 9 ).Type, state.Record( 18, 9 ).ParentId ), "the '.' cell is left alone" );
		Assert.AreEqual( (ParkTrackPieces.KartCell, (ushort)MapStep.CellId( 18, 9 )), (state.Record( 18, 10 ).TrackType, state.Record( 18, 10 ).TrackParentId ),
			"the track under it stays" );

		Straight( state, 18, 12, 0x04 );
		ParkTrackAddOn.Stamp( state, item, 18, 12, 0 );

		Assert.AreEqual( (ParkTrackPieces.KartAnchor, (byte)0x44, (byte)0x04, (ushort)0x10), (state.Record( 18, 12 ).TrackType,
			state.Record( 18, 12 ).TrackNeighbours, state.Record( 18, 12 ).TrackDirection, state.Record( 18, 12 ).TrackFlags ),
			"an anchor under a track cell keeps its type, links and way on" );
	}

	/// <summary>A made object's record carries the ride an add-on was put down on in <c>mUpgradeParent</c>, at 226.</summary>
	[TestMethod]
	public void AMadeRecordWritesItsUpgradeParent()
	{
		var record = ParkWorld.MadeObjectRecord( new ParkWorld.MadeObject( new ParkWorld.CatalogueObject( 412, 1500, 19 << 8, 9 << 8, 270,
			UpgradeParent: 289 ), "Dino Karts Jump", "" ), 7 );

		Assert.AreEqual( 289, System.BitConverter.ToUInt16( record, 226 ) );
	}
}
