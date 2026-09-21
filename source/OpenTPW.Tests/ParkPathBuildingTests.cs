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
}
