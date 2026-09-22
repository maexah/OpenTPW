using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

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
	/// <b>This number was measured after two different guesses were both wrong</b>, which is why it is
	/// pinned here rather than left to a comment. The executable's loader reconstructs cells from the
	/// level's design map and sets the flag on the path cells it creates that way; OpenTPW reads the
	/// <b>save's stored</b> flags instead, and the two do not agree. So "the player cannot lift the
	/// level's own walkways" is true of the original's runtime and <b>only partly true here</b>: the
	/// refusal in <see cref="ParkPathBuilding.Lift"/> is real, and it covers 18 cells.
	/// </para>
	/// <para>
	/// <b>A hypothesis, marked as one:</b> the 18 are plausibly the level author's own fixed paths,
	/// with the other 60 laid while the scenario was authored - which is what a shipped scenario save
	/// would look like. Settling it means checking those 18 against <c>base.map</c>'s design bits, and
	/// nothing here should assume it in the meantime.
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
	/// A QUEUE cell is given a piece, which <see cref="ParkPathBuilding.Retile"/> answered only for paths.
	///
	/// <para>
	/// <b>The failure this pins was invisible rather than wrong-looking.</b> A laid queue cell kept
	/// whatever tile index the ground under it carried - <b>55</b> on bare ground - which is outside
	/// <see cref="ParkQueues"/>'s table of eight, so the cell drew <b>nothing at all</b> and was counted
	/// as naming a piece the game has no model for. Measured in a running park before the arm existed:
	/// a queue laid at (42,22) read <c>tile set 2 index 55 angle 0</c>, and <c>drawn</c> reported the
	/// park's four shipped pieces with five queue cells standing in it.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>The expected values are the shipped park's own art, not a reading of the table.</b> Its three
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
}
