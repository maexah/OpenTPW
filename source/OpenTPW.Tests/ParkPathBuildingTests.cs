using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Laying and lifting path - <see cref="ParkPathBuilding"/>, the verb a player performs.
///
/// <para>
/// Every number asserted here is measured rather than chosen: the cell price is
/// <c>Costs.PathCell</c> = <b>20</b> from <c>data/levels/Standard.sam</c>, which jungle's
/// <c>Easy_Standard.sam</c> does not override; a cell that is already path is <b>free</b>, which is
/// the original's own shortcut before any price is fetched; and lifting a path <b>refunds nothing</b>,
/// which is an asymmetry in the original rather than an omission here - only a queue cell credits
/// anything back.
/// </para>
/// <para>
/// <b>On y = 10 the bare band is x = 0..27</b> (measured), and these use cells inside it, away from
/// the park's own walkways at x 39..57, y 15..29.
/// </para>
/// </summary>
[TestClass]
public class ParkPathBuildingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The gate the stamp consults first - the original's <c>FUN_00535600</c>. <b>4-over-4 is refused
	/// even though "the type it already is" is otherwise allowed</b>, which is the carve-out most
	/// likely to be tidied away by someone simplifying this.
	/// </summary>
	[TestMethod]
	public void TheTypeGateAllowsOnlyWhatTheOriginalAllows()
	{
		Assert.IsTrue( ParkPathBuilding.MayBecome( CellEdge.Nothing, CellEdge.Path ), "path on bare ground" );
		Assert.IsTrue( ParkPathBuilding.MayBecome( ParkRideChoice.QueueCellType, CellEdge.Path ), "path over queue" );
		Assert.IsTrue( ParkPathBuilding.MayBecome( CellEdge.Path, CellEdge.Footprint ), "a footprint over path" );
		Assert.IsTrue( ParkPathBuilding.MayBecome( CellEdge.Path, CellEdge.Path ), "the type it already is" );
		Assert.IsTrue( ParkPathBuilding.MayBecome( CellEdge.Footprint, CellEdge.Nothing ), "clearing is always allowed" );

		Assert.IsFalse( ParkPathBuilding.MayBecome( CellEdge.Footprint, CellEdge.Footprint ),
			"4 over 4 is refused - the one carve-out from 'the type it already is'" );
		Assert.IsFalse( ParkPathBuilding.MayBecome( CellEdge.RideEnd, CellEdge.Path ),
			"path may not be laid over a ride entrance" );
	}

	/// <summary>
	/// The price comes from the theme's own balance file, and it is 20 for Lost Kingdom.
	/// </summary>
	[TestMethod]
	public void APathCellCostsWhatTheBalanceFileSays()
	{
		var balance = new ParkBalance( "jungle" );

		Assert.AreEqual( 20, balance.Int( ParkPathBuilding.PathCellCostKey, -1 ),
			"Costs.PathCell in Standard.sam, which jungle's Easy_Standard.sam does not override" );
		Assert.AreEqual( 75, balance.Int( "Costs.QueueCell", -1 ), "and a queue cell, for contrast" );
	}

	/// <summary>
	/// How much of the shipped park NOMODIFY actually protects - <b>18 of its 78 path cells, not all
	/// of them</b>.
	///
	/// <para>
	/// <b>This number is measured</b>, and pinned here rather than left to a comment. The executable's
	/// loader reconstructs cells from the
	/// level's design map and sets the flag on the path cells it creates that way; OpenTPW reads the
	/// <b>save's stored</b> flags instead. Those cover the whole design-map avenue, and whether the loader
	/// rebuilds any more design-map path than that is not established (<c>docs/exe/park-engine.md</c>,
	/// "Deleting is not demolishing"). The refusal in <see cref="ParkPathBuilding.Lift"/> is real, and it covers 18 cells.
	/// </para>
	/// <para>
	/// <b>Which 18, measured cell by cell:</b> the ten-cell avenue at x 47..48, y 17..21, which is
	/// design-map path, and the eight paths the placer laid before things' ends
	/// (<c>docs/exe/park-engine.md</c>, "What the placer builds in front of a thing").
	/// </para>
	/// </summary>
	[TestMethod]
	public void NoModifyProtectsEighteenOfTheParksSeventyEightPathCells()
	{
		var park = World();
		var protectedCells = 0;
		var paths = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var cell = park.CellAt( x, y );

				if ( cell.Type != CellEdge.Path )
					continue;

				++paths;

				if ( (cell.Flags & ParkPathBuilding.NoModify) != 0 )
					++protectedCells;
			}
		}

		Assert.AreEqual( 78, paths, "the shipped park's path cells" );
		Assert.AreEqual( 18, protectedCells,
			"the save carries NOMODIFY on 18 of them - the loader's runtime flag is a different thing" );
	}

	/// <summary>
	/// A QUEUE cell is given a piece by <see cref="ParkPathBuilding.Retile"/>, as a path cell is.
	///
	/// <para>
	/// <b>The failure this pins was invisible rather than wrong-looking.</b> A laid queue cell kept
	/// whatever tile index the ground under it carried - <b>55</b> on bare ground - which is outside
	/// <see cref="ParkQueues"/>'s table of eight, so the cell drew <b>nothing at all</b> and was counted
	/// as naming a piece the game has no model for.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>The expected values are the shipped park's own art, not a reading of the table.</b> Its two
	/// straight queue cells at (50,22) and (51,22) carry <c>index 2 angle 270</c> against a table row of
	/// <c>index 2 angle 90</c> - the 180 comes from the direction base, which applies to a straight.
	/// <para>
	/// <b>Mutation:</b> taking the queue arm out of <see cref="ParkPathBuilding.Retile"/> leaves the
	/// index at 55 and fails here.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void ALaidQueueCellIsGivenAPieceFromTheGamesOwnTable()
	{
		var park = World();
		var state = new ParkState( park );

		Assert.AreEqual( 0, ParkState.CellFor( park, 15, 10 ).Type, "(15,10) should be bare ground to begin with" );
		Assert.AreEqual( 55, ParkState.CellFor( park, 15, 10 ).TileIndex,
			"bare ground names no piece, and 55 is what a queue cell was left carrying" );

		// A queue running east to west, which is the shape three of the shipped park's four carry.
		state.SetRecord( 15, 10, ParkState.CellFor( park, 15, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Neighbours = 0x44,
			Direction = 0x04
		} );

		ParkPathBuilding.Retile( state, park, 15, 10 );

		Assert.AreEqual( ParkQueues.QueueTileSet, ParkState.CellFor( park, 15, 10 ).TileSet, "a queue cell's set is 2" );
		Assert.AreEqual( 2, ParkState.CellFor( park, 15, 10 ).TileIndex, "mask 0x44 is a straight" );
		Assert.AreEqual( 270, ParkState.CellFor( park, 15, 10 ).TileAngle,
			"and direction 0x04 carries the table's 90 round to 270 - the shipped (50,22) and (51,22) exactly" );
	}

	/// <summary>
	/// The queue cell where a queue meets a path draws the <b>end</b> piece - three more on the index for
	/// each cardinal link that reaches a path, which is the shipped (49,22)'s own <c>index 5</c>.
	/// </summary>
	/// <remarks>
	/// <b>The link has to be MUTUAL, and the second half of this is what pins that.</b> The original
	/// counts a neighbour only where this cell's mask carries the bit AND the neighbour's own mask
	/// carries the opposite one - a park's mask is legitimately full of one-sided bits, so counting
	/// those would bump cells the original leaves alone.
	/// </remarks>
	[TestMethod]
	public void TheQueueCellWhereItMeetsAPathDrawsTheEndPiece()
	{
		var park = World();
		var state = new ParkState( park );

		Assert.AreEqual( 0, ParkState.CellFor( park, 24, 10 ).Type,
			"(24,10) should be bare ground, or the east side would bump the index too" );

		// A path to the WEST naming the queue cell back - 0x40 from the queue, 0x04 returning.
		state.SetRecord( 22, 10, ParkState.CellFor( park, 22, 10 ) with
		{
			Type = CellEdge.Path,
			Neighbours = 0x04
		} );

		state.SetRecord( 23, 10, ParkState.CellFor( park, 23, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Neighbours = 0x44,
			Direction = 0x04
		} );

		ParkPathBuilding.Retile( state, park, 23, 10 );

		Assert.AreEqual( 5, ParkState.CellFor( park, 23, 10 ).TileIndex,
			"two for the straight and three for the path it meets - the shipped (49,22) exactly" );

		// One-sided is not a link. A second overlay, so the first half's records cannot answer:
		// ParkState.Current is whichever was built last.
		var second = new ParkState( park );

		second.SetRecord( 22, 10, ParkState.CellFor( park, 22, 10 ) with { Type = CellEdge.Path, Neighbours = 0 } );
		second.SetRecord( 23, 10, ParkState.CellFor( park, 23, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Neighbours = 0x44,
			Direction = 0x04
		} );

		ParkPathBuilding.Retile( second, park, 23, 10 );

		Assert.AreEqual( 2, ParkState.CellFor( park, 23, 10 ).TileIndex,
			"a bit the neighbour does not carry back is not a link, so the straight stands" );
	}

	/// <summary>
	/// A queue cell always names a piece the game actually has, however many paths it meets.
	///
	/// <para>
	/// <b>This was found by looking at a screenshot, and no number in the run reported it.</b> Two
	/// mutual path links bump a corner's index to 4 + 3 + 3 = <b>10</b> against a table of
	/// <see cref="ParkQueues.PieceCount"/> pieces, and <see cref="ParkQueues"/> then declines to draw a
	/// piece it has no model for - while <see cref="ParkGround"/> has already left the cell alone
	/// because its tile set is 2. The result is a hole with the sky showing through it, photographed in
	/// a running park at (44,22).
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> removing the loop that drops links until the index fits puts the index at 10 and
	/// fails here - and in a park it puts the hole back.
	/// </remarks>
	[TestMethod]
	public void AQueueCellMeetingTwoPathsStillNamesAPieceTheGameHas()
	{
		var park = World();
		var state = new ParkState( park );

		// Paths to the west and to the north, each naming the queue cell back, so both links are mutual.
		state.SetRecord( 25, 10, ParkState.CellFor( park, 25, 10 ) with { Type = CellEdge.Path, Neighbours = 0x04 } );
		state.SetRecord( 26, 9, ParkState.CellFor( park, 26, 9 ) with { Type = CellEdge.Path, Neighbours = 0x10 } );

		// A corner facing north - mask 0x41 with direction 0x01 is one of the four pairs the original
		// bumps by one, so this starts at 4 before either link is counted.
		state.SetRecord( 26, 10, ParkState.CellFor( park, 26, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Neighbours = 0x41,
			Direction = 0x01
		} );

		ParkPathBuilding.Retile( state, park, 26, 10 );

		var index = ParkState.CellFor( park, 26, 10 ).TileIndex;

		Assert.IsTrue( index < ParkQueues.PieceCount,
			$"index {index} is outside the game's table of {ParkQueues.PieceCount} pieces, so the cell draws "
			+ "nothing and the sky shows through the ground" );
		Assert.AreEqual( 7, index,
			"one link is dropped and one kept - as many as the table can express, rather than none" );
	}

	/// <summary>
	/// A queue cell says which thing it serves, through the packed cell its owner stands on.
	///
	/// <para>
	/// <c>OwnerOf</c> is read by <see cref="ParkPathBuilding.LayQueue"/>, the queue tool's verdict,
	/// <see cref="ParkPathBuilding.LiftQueue"/>, the path run, the placer's path stub, and the click that
	/// re-arms the queue tool on a queue cell, so an answer of nought here reads exactly
	/// like "that cell is not a queue" and sends the click somewhere else entirely.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AQueueCellNamesTheThingItServes()
	{
		var park = World();
		var state = new ParkState( park );

		state.AddObject( new ParkWorld.CatalogueObject(
			ThingId: 77, CatalogueId: 1100, RawX: 41 << 8, RawY: 23 << 8, Angle: 0 ) );

		var cell = ParkState.CellFor( park, 20, 12 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			ParentId = (ushort)MapStep.CellId( 41, 23 )
		};

		state.SetRecord( 20, 12, cell );

		Assert.AreEqual( 2986, (int)cell.ParentId, "the packed cell of (41,23), counted from one" );
		Assert.AreEqual( 77, ParkPathBuilding.OwnerOf( state, cell ),
			"the queue cell names the thing anchored on the cell its ParentId packs" );

		// And a cell naming nobody answers nought rather than the first object in the list.
		Assert.AreEqual( 0, ParkPathBuilding.OwnerOf( state, cell with { ParentId = 0 } ),
			"a cell with no owner recorded names nobody" );
	}

	/// <summary>A ride standing at (19,11) with its entrance at (20,11), the shape of the shipped Belly Bounce one row up.</summary>
	private static ParkWorld.CatalogueObject Ride( ParkState state )
	{
		var ride = new ParkWorld.CatalogueObject( ThingId: 77, CatalogueId: 1100, RawX: 19 << 8, RawY: 11 << 8, Angle: 0,
			EntryPos: (ushort)MapStep.CellId( 20, 11 ), ExitPos: (ushort)MapStep.CellId( 20, 14 ) );

		state.AddObject( ride );

		return ride;
	}

	/// <summary>
	/// Stands <see cref="Ride"/> on its cells, as the placer does before it marks the ends
	/// (<see cref="ParkBuilding.Stamp"/>): each names the anchor as its owner, the entrance included, and the
	/// anchor is typed, which a queue cell's refund asks.
	/// </summary>
	private static void StandOnItsFootprint( ParkState state ) => ParkBuilding.Stamp( state, (19, 11, 20, 14), 19, 11 );

	private static void LayPath( ParkState state, ParkWorld park, int x, int y )
		=> state.SetRecord( x, y, ParkState.CellFor( park, x, y ) with
		{
			Type = CellEdge.Path,
			TileSet = ParkPaths.PathTileSet,
			Neighbours = 0,
			Direction = 0
		} );

	/// <summary>
	/// <b>The shipped Belly Bounce's queue, laid again on bare ground the way a player lays it</b>: the
	/// placer's cell before the entrance, then one run west from it to a path. Every mask, flow byte and
	/// owner is the shipped park's own - (52,22) <c>0x50</c> flowing <c>0x10</c>, three cells of
	/// <c>0x44</c> flowing <c>0x04</c>, and the path the run ends on joined back and owned by the ride -
	/// and the queue measures four cells ending where the run turned into the path.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> writing only the new cell's half of each link leaves the stub at <c>0x10</c> and
	/// each cell one bit short, and fails the first assertion on each.
	/// </remarks>
	[TestMethod]
	public void AQueueLaidFromThePlacersCellMatchesTheShippedOne()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );
		var owner = MapStep.CellId( 19, 11 );

		for ( var x = 16; x <= 20; ++x )
			Assert.AreEqual( 0, ParkState.CellFor( park, x, 10 ).Type, $"({x},10) starts as bare ground" );

		LayPath( state, park, 16, 10 );

		var node = ParkBuilding.MarkWaysInAndOut( state, park, 20, 11, 20, 14, 0x10, 0x10, 0, hasQueue: true, owner );

		Assert.AreEqual( (20, 10), node, "the placer's queue cell is the anchor" );

		ParkPathBuilding.StampQueueCell( state, park, 19, 10, 20, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 18, 10, 19, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 17, 10, 18, 10, ride, firstOfRun: false );
		ParkPathBuilding.JoinQueueToPath( state, park, 17, 10, 16, 10, ride );

		Assert.AreEqual( 0x50, ParkState.CellFor( park, 20, 10 ).Neighbours, "the placer's cell, as (52,22)" );
		Assert.AreEqual( 0x10, ParkState.CellFor( park, 20, 10 ).Direction );

		for ( var x = 17; x <= 19; ++x )
		{
			Assert.AreEqual( 0x44, ParkState.CellFor( park, x, 10 ).Neighbours, $"({x},10) joined both ways, as (49..51,22)" );
			Assert.AreEqual( 0x04, ParkState.CellFor( park, x, 10 ).Direction, $"({x},10) flows back toward the ride" );
			Assert.AreEqual( owner, ParkState.CellFor( park, x, 10 ).ParentId, $"({x},10) is the ride's" );
		}

		var path = ParkState.CellFor( park, 16, 10 );

		Assert.AreEqual( CellEdge.Path, path.Type, "the cell the run ended on stays a path" );
		Assert.AreEqual( 0x04, path.Neighbours & 0x04, "joined back to the queue" );
		Assert.AreEqual( owner, path.ParentId, "and owned by the ride, as (48,22) is the one owned path in the park" );

		Assert.AreEqual( (MapStep.CellId( 17, 10 ), 4), ParkRideChoice.QueueCellsFor( park, ride ),
			"four cells, ending where the queue meets the path - the shipped (4, 2866) in shape" );
	}

	/// <summary>
	/// <b>A queue never joins a path it runs beside</b> - the queue arm links back along the run and to the
	/// entrance, and to nothing by type - so a queue laid alongside a walkway stays a single file.
	/// </summary>
	[TestMethod]
	public void AQueueDoesNotJoinAPathItRunsBeside()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		LayPath( state, park, 6, 9 );
		state.SetRecord( 7, 10, ParkState.CellFor( park, 7, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType, TileSet = ParkQueues.QueueTileSet, Neighbours = 0x04, Direction = 0x04,
			ParentId = (ushort)MapStep.CellId( 19, 11 )
		} );

		ParkPathBuilding.StampQueueCell( state, park, 6, 10, 7, 10, ride, firstOfRun: false );

		Assert.AreEqual( 0x04, ParkState.CellFor( park, 6, 10 ).Neighbours, "joined to the cell before it and nothing else" );
		Assert.AreEqual( 0x44, ParkState.CellFor( park, 7, 10 ).Neighbours, "which is joined back" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 6, 9 ).Neighbours, "the path beside it gains nothing" );
	}

	/// <summary>
	/// The two gates on joining back: <b>a cell already joined two ways is left alone</b>, and so is a
	/// queue that belongs to another ride - which is what stops a run forking a queue or stealing one.
	/// </summary>
	[TestMethod]
	public void AQueueCellInTheMiddleOfAFileOrAnotherRidesIsNotJoined()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		state.SetRecord( 8, 10, ParkState.CellFor( park, 8, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType, Neighbours = 0x44, Direction = 0x04, ParentId = (ushort)MapStep.CellId( 19, 11 )
		} );

		ParkPathBuilding.StampQueueCell( state, park, 8, 9, 8, 10, ride, firstOfRun: false );

		Assert.AreEqual( 0, ParkState.CellFor( park, 8, 9 ).Neighbours, "a two-way cell is not joined a third way" );
		Assert.AreEqual( 0x44, ParkState.CellFor( park, 8, 10 ).Neighbours );

		state.SetRecord( 11, 10, ParkState.CellFor( park, 11, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType, Neighbours = 0x04, Direction = 0x04, ParentId = (ushort)MapStep.CellId( 3, 3 )
		} );

		ParkPathBuilding.StampQueueCell( state, park, 10, 10, 11, 10, ride, firstOfRun: false );

		Assert.AreEqual( 0, ParkState.CellFor( park, 10, 10 ).Neighbours, "another ride's queue is not joined" );
	}

	/// <summary>
	/// The first cell of a run bonds to the ride's entrance, and <b>only when the entrance's direction
	/// byte points straight at it</b> - an equality on the whole byte, as the original tests it.
	/// </summary>
	[TestMethod]
	public void TheFirstCellOfARunBondsToTheEntranceItFaces()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		state.SetRecord( 20, 11, ParkState.CellFor( park, 20, 11 ) with { Type = CellEdge.RideEnd, Direction = 0x04 } );

		ParkPathBuilding.StampQueueCell( state, park, 20, 10, 20, 11, ride, firstOfRun: true );

		Assert.AreEqual( 0, ParkState.CellFor( park, 20, 10 ).Neighbours & 0x10, "an entrance facing east does not bond north" );

		state.SetRecord( 20, 11, ParkState.CellFor( park, 20, 11 ) with { Direction = 0x01 } );

		ParkPathBuilding.StampQueueCell( state, park, 20, 10, 20, 11, ride, firstOfRun: true );

		Assert.AreNotEqual( 0, ParkState.CellFor( park, 20, 10 ).Neighbours & 0x10, "one facing it does" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 20, 11 ).Neighbours & 0x01, "both ways" );
	}

	/// <summary>
	/// <b>A queue run across a path clears the path out of its way first</b> - the stamp's force-clear -
	/// so the crossing cell carries only the queue's own links and the paths either side stop naming it.
	/// Kept, the path's links would give the crossing cell three bits and the next cell of the run could
	/// not join it.
	/// </summary>
	[TestMethod]
	public void AQueueRunAcrossAPathClearsThePathFirst()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		// A north-south path through (13,9)..(13,11), joined both ways, and a queue arriving from the east.
		foreach ( var y in new[] { 9, 10, 11 } )
			LayPath( state, park, 13, y );

		state.SetRecord( 13, 9, ParkState.CellFor( park, 13, 9 ) with { Neighbours = 0x10 } );
		state.SetRecord( 13, 10, ParkState.CellFor( park, 13, 10 ) with { Neighbours = 0x11, Direction = 0x10 } );
		state.SetRecord( 13, 11, ParkState.CellFor( park, 13, 11 ) with { Neighbours = 0x01 } );
		state.SetRecord( 14, 10, ParkState.CellFor( park, 14, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType, Neighbours = 0x04, Direction = 0x04,
			ParentId = (ushort)MapStep.CellId( 19, 11 )
		} );

		ParkPathBuilding.StampQueueCell( state, park, 13, 10, 14, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 12, 10, 13, 10, ride, firstOfRun: false );

		Assert.AreEqual( 0x44, ParkState.CellFor( park, 13, 10 ).Neighbours, "the crossing cell is joined along the queue only" );
		Assert.AreEqual( 0x04, ParkState.CellFor( park, 13, 10 ).Direction, "and flows back along it, not the path's way" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 13, 9 ).Neighbours & 0x10, "the path north no longer names it" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 13, 11 ).Neighbours & 0x01, "nor the path south" );
		Assert.AreEqual( 0x04, ParkState.CellFor( park, 12, 10 ).Neighbours & 0x04, "and the run carries on past it" );
	}

	/// <summary>
	/// <b>What the placer's test pass refuses about a thing's ends</b>: a queued entrance facing anything
	/// but bare ground or an ordinary path, and an end facing off the map. The entrance of a thing with no
	/// queue may face a NOMODIFY path.
	/// </summary>
	[TestMethod]
	public void APlacementIsRefusedWhereItsEndsCannotBeBuilt()
	{
		var park = World();
		var state = new ParkState( park );

		// A one-cell entrance picture: the entrance is the anchor, facing 0x10, so it faces the cell north.
		var queued = new ParkItemCatalogue.Item( 1, "queued", "", "", 1, 1, null, HasQueue: true, HasEntrance: true,
			EntryDirection: 0x10, ExitDirection: 0x10 );
		var shop = queued with { HasQueue = false };

		Assert.IsNull( ParkBuilding.EndRefusal( state, queued, 5, 10, 0 ), "bare ground north of it" );

		LayPath( state, park, 5, 9 );
		Assert.IsNull( ParkBuilding.EndRefusal( state, queued, 5, 10, 0 ), "an ordinary path" );

		state.SetRecord( 5, 9, ParkState.CellFor( park, 5, 9 ) with { Flags = ParkPathBuilding.NoModify } );
		Assert.IsNotNull( ParkBuilding.EndRefusal( state, queued, 5, 10, 0 ), "a NOMODIFY path refuses a queued entrance" );
		Assert.IsNull( ParkBuilding.EndRefusal( state, shop, 5, 10, 0 ), "but not a shop's" );

		state.SetRecord( 5, 9, ParkState.CellFor( park, 5, 9 ) with { Type = ParkRideChoice.QueueCellType, Flags = 0 } );
		Assert.IsNotNull( ParkBuilding.EndRefusal( state, queued, 5, 10, 0 ), "another queue refuses it" );

		Assert.IsNotNull( ParkBuilding.EndRefusal( state, queued, 5, 0, 0 ), "facing off the map refuses it" );
	}

	/// <summary>
	/// <b>Selling a queued ride drains its queue, the placer's node included, and hands back what the
	/// placer laid before its exit as an ordinary path</b> - the demolisher's own work outside the
	/// footprint. Four queue cells return three cells' worth: each refunds, and one is taken back for the
	/// node the placer laid for nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> leaving the node standing - clearing only from the second cell - keeps (20,10) a
	/// NOMODIFY queue cell, which is what left a moved ride unable to go back where it was.
	/// </remarks>
	[TestMethod]
	public void SellingARideDrainsItsQueueAndFreesItsExitPath()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );
		var owner = MapStep.CellId( 19, 11 );

		LayPath( state, park, 16, 10 );
		StandOnItsFootprint( state );
		ParkBuilding.MarkWaysInAndOut( state, park, 20, 11, 20, 14, 0x10, 0x10, 0, hasQueue: true, owner );
		ParkPathBuilding.StampQueueCell( state, park, 19, 10, 20, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 18, 10, 19, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 17, 10, 18, 10, ride, firstOfRun: false );
		ParkPathBuilding.JoinQueueToPath( state, park, 17, 10, 16, 10, ride );

		Assert.AreEqual( ParkPathBuilding.NoModify, ParkState.CellFor( park, 20, 15 ).Flags, "the exit's path starts NOMODIFY" );

		var balance = state.Balance;
		var returned = ParkPathBuilding.DrainQueue( state, park, ride );

		Assert.AreEqual( 3 * 75, returned, "four cells, one taken back" );
		Assert.AreEqual( balance + (3 * 75), state.Balance, "and the park has it" );

		for ( var x = 17; x <= 20; ++x )
			Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, x, 10 ).Type, $"({x},10) is bare ground again" );

		Assert.AreEqual( 0, ParkState.CellFor( park, 20, 10 ).Flags, "the node's NOMODIFY went with it" );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( park, 16, 10 ).Type, "the path it joined stays" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 16, 10 ).Neighbours & 0x04, "and is let go of" );

		ParkBuilding.ReleaseEnds( state, park, ride );

		Assert.AreEqual( 0, ParkState.CellFor( park, 20, 15 ).Flags, "the exit's path is ordinary path again" );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( park, 20, 15 ).Type );
		Assert.AreEqual( 0, ParkState.CellFor( park, 20, 15 ).Neighbours & 0x01, "and no longer names the exit" );
	}

	/// <summary>
	/// <b>A queue with a corner is drained a run at a time, and measured after each</b> -
	/// <c>FUN_0052fe50</c> once a run (<c>docs/exe/ride-operation.md</c>, "The sale's drain"). Laid from the
	/// placer's cell west to (17,10) and north to (17,8), where it joins the path at (17,7), its list is the
	/// node twice, the corner and the end. The first run clears (17,8) to (17,10) and leaves three cells whose
	/// back still has both its links - connected, which is what could open a closed ride again; the second
	/// clears the rest and leaves the one bare cell the entrance links to, not connected; the third clears
	/// nothing. Six cells refund, and one is taken back.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> stopping each run short of its far end leaves the corner standing, and the first measure
	/// finds four cells.
	/// </remarks>
	[TestMethod]
	public void ADrainedQueueIsMeasuredAfterEachRun()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state ) with { Flags = ParkWorld.CatalogueObject.QueuePathFlag };
		var owner = MapStep.CellId( 19, 11 );

		state.ReplaceObject( ride );

		LayPath( state, park, 17, 7 );
		StandOnItsFootprint( state );
		ParkBuilding.MarkWaysInAndOut( state, park, 20, 11, 20, 14, 0x10, 0x10, 0, hasQueue: true, owner );
		ParkPathBuilding.StampQueueCell( state, park, 19, 10, 20, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 18, 10, 19, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 17, 10, 18, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 17, 9, 17, 10, ride, firstOfRun: false );
		ParkPathBuilding.StampQueueCell( state, park, 17, 8, 17, 9, ride, firstOfRun: false );
		ParkPathBuilding.JoinQueueToPath( state, park, 17, 8, 17, 7, ride );

		Assert.AreEqual( 6, ParkRideChoice.QueueCellsFor( park, ride ).Cells, "six cells, one corner" );

		var measured = new System.Collections.Generic.List<(int Cells, bool Connected)>();

		state.QueueRemeasured = thing => measured.Add( (ParkRideChoice.QueueCellsFor( park, ride ).Cells,
			ParkRideOperation.BackOfQueueConnected( park, ride )) );

		var balance = state.Balance;
		var returned = ParkPathBuilding.DrainQueue( state, park, ride );

		CollectionAssert.AreEqual( new[] { (3, true), (1, false), (1, false) }, measured.ToArray(),
			"three cells still linked to the corner's place, then the bare node, twice" );
		Assert.AreEqual( 5 * 75, returned, "six cells, one taken back" );
		Assert.AreEqual( balance + (5 * 75), state.Balance );

		foreach ( var (x, y) in new[] { (20, 10), (19, 10), (18, 10), (17, 10), (17, 9), (17, 8) } )
			Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, x, y ).Type, $"({x},{y}) is bare ground again" );

		Assert.AreEqual( 0, ParkState.CellFor( park, 17, 7 ).Neighbours & 0x10, "the path is let go of" );
		Assert.AreEqual( 0x01, ParkState.CellFor( park, 20, 11 ).Neighbours, "the entrance keeps its link" );
	}

	/// <summary>
	/// <b>An entrance that faces a path is let go of by the path, and nothing is drained</b>: the walk reaches
	/// the path from the entrance and does not step back onto it (<c>0x0053036d</c>), so the path is pushed as
	/// the end as well as the faced cell, and a run whose far end is a path clears nothing (<c>0x0052ff9a</c>).
	/// The queue still measures the one cell the entrance links to, so the gate passes and one cell's worth is
	/// taken all the same.
	/// </summary>
	[TestMethod]
	public void AnEntranceFacingAPathDrainsNothing()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		StandOnItsFootprint( state );
		state.SetRecord( 20, 11, state.Record( 20, 11 ) with { Type = CellEdge.RideEnd, Neighbours = 0x01, Direction = 0x01 } );
		LayPath( state, park, 20, 10 );
		LayPath( state, park, 19, 10 );
		state.SetRecord( 20, 10, state.Record( 20, 10 ) with { Neighbours = 0x10 | 0x40 } );
		state.SetRecord( 19, 10, state.Record( 19, 10 ) with { Neighbours = 0x04 } );

		CollectionAssert.AreEqual( new[] { (20, 10), (20, 10) }, ParkPathBuilding.QueueEnds( state, park, ride ).ToArray(),
			"the path is the faced cell and the end" );
		Assert.AreEqual( 0x40, ParkState.CellFor( park, 20, 10 ).Neighbours, "the path lets the entrance go" );
		Assert.AreEqual( 0x01, ParkState.CellFor( park, 20, 11 ).Neighbours, "the entrance keeps its bit" );

		var balance = state.Balance;
		var otherKinds = Times( "QUEUE_DRAIN_CLEARS_ANOTHER_KIND" );

		Assert.AreEqual( -75, ParkPathBuilding.DrainQueue( state, park, ride ), "nothing refunds, one cell is taken" );
		Assert.AreEqual( balance - 75, state.Balance );
		Assert.AreEqual( CellEdge.Path, ParkState.CellFor( park, 20, 10 ).Type, "the path stays" );
		Assert.AreEqual( otherKinds, Times( "QUEUE_DRAIN_CLEARS_ANOTHER_KIND" ), "a run ending on a path is not cleared at all" );
	}

	/// <summary>
	/// <b>A ride bought and sold at once drains its lone node for nothing</b>: the node is the faced cell, and
	/// the walk stops on it, a queue cell with one link (<c>0x005302f7</c>), so it is the end too. The one run
	/// clears it and refunds it, and the debit takes it back.
	/// </summary>
	[TestMethod]
	public void ALoneNodeIsItsQueuesEnd()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		StandOnItsFootprint( state );
		ParkBuilding.MarkWaysInAndOut( state, park, 20, 11, 20, 14, 0x10, 0x10, 0, hasQueue: true, MapStep.CellId( 19, 11 ) );

		Assert.AreEqual( 0x10, ParkState.CellFor( park, 20, 10 ).Neighbours, "the node links the entrance alone" );
		CollectionAssert.AreEqual( new[] { (20, 10), (20, 10) }, ParkPathBuilding.QueueEnds( state, park, ride ).ToArray(),
			"the node is the faced cell and the end" );

		var balance = state.Balance;

		Assert.AreEqual( 0, ParkPathBuilding.DrainQueue( state, park, ride ), "one refunded, one taken" );
		Assert.AreEqual( balance, state.Balance );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 20, 10 ).Type, "the node goes" );
	}

	/// <summary>
	/// <b>A queue that measures nothing is not drained, and nothing is taken</b>: the gate reads the queue's
	/// length (<c>0x00527fb4</c>), and an entrance with no link has none, so the list is thrown away.
	/// </summary>
	[TestMethod]
	public void AQueueThatMeasuresNothingIsNotDrained()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		StandOnItsFootprint( state );
		state.SetRecord( 20, 11, state.Record( 20, 11 ) with { Type = CellEdge.RideEnd, Neighbours = 0, Direction = 0x01 } );

		Assert.AreEqual( 0, ParkRideChoice.QueueCellsFor( park, ride ).Cells, "no link, no queue" );

		var balance = state.Balance;
		var posts = Times( "QUEUE_DRAIN_ADVISOR_0xCB" );

		Assert.AreEqual( 0, ParkPathBuilding.DrainQueue( state, park, ride ), "nothing drained" );
		Assert.AreEqual( balance, state.Balance, "and nothing taken" );
		Assert.AreEqual( posts + 1, Times( "QUEUE_DRAIN_ADVISOR_0xCB" ), "mode 3 is armed before the gate" );
	}

	/// <summary>
	/// <b>An entrance that faces another thing's queue drains nothing of it</b>: a faced queue or entrance cell
	/// whose owner is not the entrance's is pushed alone (<c>0x005301ea</c>), a list of one is never popped,
	/// and one cell's worth is taken all the same.
	/// </summary>
	[TestMethod]
	public void AnEntranceFacingAnotherThingsQueueDrainsNothing()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		StandOnItsFootprint( state );
		state.SetRecord( 20, 11, state.Record( 20, 11 ) with { Type = CellEdge.RideEnd, Neighbours = 0x01, Direction = 0x01 } );
		state.SetRecord( 20, 10, state.Record( 20, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Neighbours = 0x10 | 0x40,
			Direction = 0x10,
			ParentId = (ushort)MapStep.CellId( 5, 5 )
		} );

		CollectionAssert.AreEqual( new[] { (20, 10) }, ParkPathBuilding.QueueEnds( state, park, ride ).ToArray(),
			"pushed alone, and no walk" );

		var balance = state.Balance;
		var posts = Times( "QUEUE_DRAIN_ADVISOR_0xCB" );

		Assert.AreEqual( -75, ParkPathBuilding.DrainQueue( state, park, ride ), "nothing refunds, one cell is taken" );
		Assert.AreEqual( balance - 75, state.Balance );
		Assert.AreEqual( ParkRideChoice.QueueCellType, ParkState.CellFor( park, 20, 10 ).Type, "the other queue stays" );
		Assert.AreEqual( posts + 2, Times( "QUEUE_DRAIN_ADVISOR_0xCB" ), "mode 3, and the one call's re-arm" );
	}

	/// <summary>
	/// <b>A queue cell refunds only while the cell its owner stands on is typed</b> (<c>0x00536a37</c>). The drain
	/// comes before the footprint passes, so in the game it always is; with the anchor bare, the cells still go
	/// and nothing comes back but the debit.
	/// </summary>
	[TestMethod]
	public void AQueueCellWhoseOwnerIsGoneRefundsNothing()
	{
		var park = World();
		var state = new ParkState( park );
		var ride = Ride( state );

		StandOnItsFootprint( state );
		ParkBuilding.MarkWaysInAndOut( state, park, 20, 11, 20, 14, 0x10, 0x10, 0, hasQueue: true, MapStep.CellId( 19, 11 ) );
		ParkPathBuilding.StampQueueCell( state, park, 19, 10, 20, 10, ride, firstOfRun: false );
		state.SetRecord( 19, 11, state.Record( 19, 11 ) with { Type = CellEdge.Nothing } );

		var balance = state.Balance;

		Assert.AreEqual( -75, ParkPathBuilding.DrainQueue( state, park, ride ), "two cleared, none refunded" );
		Assert.AreEqual( balance - 75, state.Balance );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 19, 10 ).Type, "the cells go all the same" );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 20, 10 ).Type );
	}

	private static int Times( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
