using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// Where a guest walks up to a thing the player has built - the entrance and exit marks in the item's own
/// <c>Info.Shape</c> picture, turned by the angle it is built at.
///
/// <para>
/// <b>Four of these need no game files</b>, because the description reader takes text directly, and the
/// branch that matters is the one no shipped park can reach: nothing in a saved park was ever built during
/// play, so every object in it already carries the entry cell the original wrote when it was placed.
/// </para>
/// <para>
/// <b>What names the marks is two records meeting, not one.</b> <c>FUN_00413410</c> stores the cell holding
/// the value <b>9</b> as the entrance and <b>10</b> as the exit; <c>bouncy.sam</c> puts its <c>S</c> at
/// column 1 row 0 and its <c>2</c> at column 1 row 3; and the shipped save gives that same Belly Bounce,
/// anchored at (51,23), <c>mEntryPos</c> 2997 = (52,23) and <c>mExitPos</c> 3381 = (52,26). Neither the
/// picture nor the save alone would name a letter.
/// </para>
/// </summary>
[TestClass]
public class ParkEntryCellTests
{
	private static ItemDescriptionFile Described( string shape )
		=> new( "Info.Id 1\nInfo.Shape\n---\n" + shape + "---\n" );

	/// <summary>The Belly Bounce's own picture, and the deltas the save independently agrees with.</summary>
	[TestMethod]
	public void TheShapePictureNamesTheEntranceAndTheExit()
	{
		var item = Described( "*S*\n***\n***\n*2*\n" );

		Assert.IsTrue( item.HasEntrance, "the picture marks an entrance" );
		Assert.AreEqual( 1, item.EntryDeltaX, "entrance column" );
		Assert.AreEqual( 0, item.EntryDeltaY, "entrance row" );
		Assert.AreEqual( 1, item.ExitDeltaX, "exit column" );
		Assert.AreEqual( 3, item.ExitDeltaY, "exit row" );
	}

	/// <summary>
	/// The Drinks Shop's own picture. <b>93 of the corpus's 137 items carrying an exit mark carry no
	/// entrance</b>, and the engine leaves both cells on the anchor for every one of them - so this is the
	/// common case rather than a malformed file.
	/// </summary>
	[TestMethod]
	public void AnItemWithNoEntranceMarkFallsBackToItsAnchorCell()
	{
		var item = Described( "**\n2*\n" );

		Assert.IsFalse( item.HasEntrance, "there is no S in this picture" );
		Assert.AreEqual( 0, item.EntryDeltaX );
		Assert.AreEqual( 0, item.EntryDeltaY );
		Assert.AreEqual( 0, item.ExitDeltaX, "the exit mark is ignored without an entrance, as the engine does" );
		Assert.AreEqual( 0, item.ExitDeltaY );
	}

	/// <summary>
	/// An entrance with no exit puts the exit on the entrance - which is what makes <c>mExitPos</c> equal
	/// <c>mEntryPos</c> on ten of the shipped park's eleven placed objects.
	/// </summary>
	[TestMethod]
	public void AnEntranceWithNoExitPutsTheExitOnTheEntrance()
	{
		var item = Described( "*S*\n***\n" );

		Assert.IsTrue( item.HasEntrance );
		Assert.AreEqual( 1, item.EntryDeltaX );
		Assert.AreEqual( 0, item.EntryDeltaY );
		Assert.AreEqual( item.EntryDeltaX, item.ExitDeltaX, "the exit falls onto the entrance" );
		Assert.AreEqual( item.EntryDeltaY, item.ExitDeltaY );
	}

	/// <summary>Blank rows inside the fence are layout, so a mark below one must not slide down a cell.</summary>
	[TestMethod]
	public void ABlankRowInsideTheFenceDoesNotMoveTheMarksDown()
	{
		var item = Described( "*S*\n\n*2*\n" );

		Assert.AreEqual( 0, item.EntryDeltaY, "the entrance is on the first kept row" );
		Assert.AreEqual( 1, item.ExitDeltaY, "and the exit on the second, not the third" );
	}

	/// <summary>
	/// The engine's own turn table, <c>MapDelta::Rotate</c>. It asserts on anything but a quarter turn, so
	/// the four arms are the whole of it.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> swapping the 90 and 270 arms fails here, and would put a bought ride's entrance on
	/// the wrong side of it in a park - which no other test in this suite could catch, because nothing else
	/// builds anything.
	/// </remarks>
	[TestMethod]
	public void ACellDeltaTurnsTheWayTheEngineTurnsIt()
	{
		Assert.AreEqual( (1, 0), ParkBuilding.RotateDelta( 1, 0, 0 ), "no turn" );
		Assert.AreEqual( (0, -1), ParkBuilding.RotateDelta( 1, 0, 90 ), "a quarter turn" );
		Assert.AreEqual( (-1, 0), ParkBuilding.RotateDelta( 1, 0, 180 ), "a half turn" );
		Assert.AreEqual( (0, 1), ParkBuilding.RotateDelta( 1, 0, 270 ), "three quarters" );

		Assert.AreEqual( (0, 1), ParkBuilding.RotateDelta( 1, 0, -90 ),
			"a negative angle is the same turn as its positive twin" );
		Assert.AreEqual( (0, -1), ParkBuilding.RotateDelta( 1, 0, 450 ),
			"and so is one past a full circle" );
	}

	/// <summary>
	/// The packing the entry cell is stored in, against the shipped Belly Bounce's own recorded value.
	/// Anchored at (51,23) with its entrance at column 1 row 0 and no turn, the original wrote
	/// <c>mEntryPos</c> <b>2997</b> and <c>mExitPos</c> <b>3381</b>.
	/// </summary>
	[TestMethod]
	public void TheShippedBellyBouncesOwnEntryCellFallsOutOfTheDerivation()
	{
		var (entryX, entryY) = ParkBuilding.RotateDelta( 1, 0, 0 );
		var (exitX, exitY) = ParkBuilding.RotateDelta( 1, 3, 0 );

		Assert.AreEqual( 2997, MapStep.CellId( 51 + entryX, 23 + entryY ),
			"the save's own mEntryPos for thing 13" );
		Assert.AreEqual( 3381, MapStep.CellId( 51 + exitX, 23 + exitY ),
			"and its mExitPos, which is a different cell on this one object alone" );
	}

	/// <summary>
	/// The corpus behind the two fallback rules above, read from the shipped files rather than asserted
	/// from one of them. Needs the game.
	/// </summary>
	[TestMethod]
	public void EveryItemCarryingAnEntranceAlsoCarriesAnExit()
	{
		var data = GameData.Required();
		int items = 0, withEntrance = 0;

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			var catalogue = new ParkItemCatalogue( theme, data );

			foreach ( var item in catalogue.All )
			{
				++items;

				if ( !item.HasEntrance )
					continue;

				++withEntrance;

				// The engine only ever leaves the exit ON the entrance; it never leaves it unset while an
				// entrance exists, which is the rule ParkBuilding leans on to place a guest leaving.
				Assert.IsTrue( item.ExitDeltaX != 0 || item.ExitDeltaY != 0
					|| (item.EntryDeltaX == 0 && item.EntryDeltaY == 0),
					$"{theme} item {item.Id} '{item.Name}' has an entrance and an unset exit" );
			}
		}

		Assert.IsTrue( items > 200, $"only {items} items were catalogued across four themes" );
		Assert.IsTrue( withEntrance > 0, "no item anywhere declared an entrance" );
	}

	/// <summary>
	/// The compass bit a way in or a way out wears, carried round with the thing it belongs to. The ring
	/// is eight wide - <c>0x01 N, 0x02 NE, 0x04 E, …</c> - so a quarter turn moves a bit two places
	/// <b>backwards</b> round it.
	/// </summary>
	/// <remarks>
	/// <b>The unturned values are measured, not chosen</b>: the shipped Belly Bounce at angle 0 carries
	/// <c>direction 0x01</c> on its type-9 entrance at (52,23) and <c>0x10</c> on its type-10 exit at
	/// (52,26), read out of the running game.
	/// <para>
	/// <b>The turned values were asserted the wrong way round here until the rotate was read.</b>
	/// <c>FUN_004d8c20</c> left-rotates the byte by <c>log2</c> of the angle's base bit and the placer
	/// pairs base <c>0x40</c> with 90 degrees, so a quarter is a left-rotate of six - a right-rotate of
	/// two - and <see cref="ParkBuilding.RotateDelta"/> turns a cell delta the same way at all four
	/// angles. This asserted <c>0x04</c> at 90, which is the other way round.
	/// </para>
	/// <para>
	/// <b>Mutation:</b> rotating one place a quarter instead of two puts the byte on a diagonal, which no
	/// cell of the shipped park carries on either field, and fails here. <b>Rotating the right number of
	/// places the wrong way fails only the 90 and 270 lines</b> - the opposite-ends loop below holds
	/// under either sense, because a rotation commutes with a nibble swap, which is exactly why this
	/// test sat on top of the defect instead of catching it.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void AWayInCarriesItsHeadingRoundWithTheThing()
	{
		Assert.AreEqual( 0x01, ParkBuilding.RotateBit( 0x01, 0 ), "no turn leaves it alone" );
		Assert.AreEqual( 0x40, ParkBuilding.RotateBit( 0x01, 90 ), "a quarter moves it two places back round the ring" );
		Assert.AreEqual( 0x10, ParkBuilding.RotateBit( 0x01, 180 ), "a half is the opposite bit" );
		Assert.AreEqual( 0x04, ParkBuilding.RotateBit( 0x01, 270 ) );

		Assert.AreEqual( 0x01, ParkBuilding.RotateBit( 0x01, 360 ), "a full circle comes home" );

		// The invariant the two helpers have to keep BETWEEN them, which is what the sense above is for:
		// north is the delta (0,-1) and the bit 0x01, and a quarter turn has to take both of them west.
		Assert.AreEqual( (-1, 0), ParkBuilding.RotateDelta( 0, -1, 90 ),
			"the delta pointing north turns to west" );
		Assert.AreEqual( 0x40, ParkBuilding.RotateBit( 0x01, 90 ),
			"so the bit pointing north has to turn to west as well, or a turned thing faces its own footprint" );

		// The way out starts on the opposite bit to the way in and stays opposite through every turn,
		// which is what keeps a ride's two ends at the two ends of its middle column.
		foreach ( var angle in new[] { 0, 90, 180, 270 } )
		{
			Assert.AreEqual( CellEdge.Opposite( ParkBuilding.RotateBit( 0x01, angle ) ),
				ParkBuilding.RotateBit( 0x10, angle ), $"the two ends stay opposite at {angle}" );
		}
	}

	/// <summary>The shipped park, for the two tests below that need real ground to write cells on.</summary>
	private static ParkWorld World( BaseFileSystem data )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The pair the placer writes when a thing goes up: <b>the way in takes the bit pointing at the cell
	/// it faces, and that cell takes the opposite bit back</b>, so the two adjoin and whatever is laid
	/// there afterwards can be stepped into.
	/// </summary>
	/// <remarks>
	/// <b>Both halves are measured off the shipped park.</b> The Belly Bounce's entrance at (52,23)
	/// carries <c>neighbours 0x01 direction 0x01</c> and its queue cell at (52,22) carries <c>0x50</c>,
	/// which holds the opposite bit <c>0x10</c>; its exit at (52,26) carries <c>0x10</c> and the cell it
	/// faces, (52,27), carries <c>0x39</c>, which holds <c>0x01</c>. The placer writes both itself after
	/// its footprint sweep - see <c>docs/exe/park-engine.md</c>, "What authors an entrance's
	/// <c>mNeighbours</c>" - because the neighbour rule cannot earn either end.
	/// <para>
	/// <b>The faced cell is given NO direction byte, and that half the shipped park refutes.</b> The
	/// decode reads the placer as writing both fields on both cells; (52,27) carries the bit and
	/// <c>direction 0x00</c>. Asserting the nought here is what stops the other half being put back from
	/// the decode alone.
	/// </para>
	/// <para>
	/// <b>Mutation:</b> dropping the faced-cell write inside <c>JoinToWhateverIsThere</c> fails the two
	/// "names it back" assertions and nothing else in the suite, because nothing else places anything.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void AWayInAndTheCellItFacesAreGivenEachOthersBits()
	{
		var park = World( GameData.Required() );
		var state = new ParkState( park );

		// Well clear of the park's own paths, which run x 39..57, y 15..29. The guard is on the BITS
		// rather than on the ground: what must not already be true is the link this writes.
		Assert.AreNotEqual( CellEdge.Path, ParkState.CellFor( park, 14, 9 ).Type,
			"(14,9) must not be path, or the re-link would contribute bits of its own" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 14, 9 ).Neighbours & 0x10,
			"(14,9) starts without the bit pointing back at the way in" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 14, 10 ).Neighbours & 0x01,
			"and (14,10) starts without its way-in bit" );

		ParkBuilding.MarkWaysInAndOut( state, park, 14, 10, 14, 13, 0 );

		Assert.AreEqual( CellEdge.RideEnd, ParkState.CellFor( park, 14, 10 ).Type, "the way in is typed 9" );
		Assert.AreEqual( 0x01, ParkState.CellFor( park, 14, 10 ).Direction,
			"and carries the heading the shipped park's own entrance carries" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 14, 10 ).Neighbours & 0x01,
			"the way in names the cell it faces" );

		Assert.AreNotEqual( 0, ParkState.CellFor( park, 14, 9 ).Neighbours & 0x10,
			"and the cell it faces names it back - without this a queue laid there is unenterable" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 14, 9 ).Direction,
			"the faced cell is given no direction byte, which is what (52,27) measures" );

		Assert.AreEqual( CellEdge.RideFarEnd, ParkState.CellFor( park, 14, 13 ).Type, "the way out is typed 10" );
		Assert.AreEqual( 0x10, ParkState.CellFor( park, 14, 13 ).Direction );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 14, 14 ).Neighbours & 0x01,
			"and the way out's own faced cell gains the opposite bit too" );
	}

	/// <summary>
	/// A thing built at a quarter turn faces the way its entry cell moved.
	///
	/// <para>
	/// <b>This is the case the rotate defect needed and nothing built.</b> A bit and a delta turn the
	/// same way at 0 and at 180 whichever direction the bit is rotated, so only a quarter or three
	/// quarters can tell the two senses apart - and no test in this suite had ever driven an angle
	/// through the placement path.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AThingBuiltAtAQuarterTurnFacesTheWayItsEntryCellMoved()
	{
		var park = World( GameData.Required() );
		var state = new ParkState( park );

		// The Belly Bounce's own entrance delta, turned a quarter: (1,0) becomes (0,-1).
		var (entryX, entryY) = ParkBuilding.RotateDelta( 1, 0, 90 );

		Assert.AreEqual( (0, -1), (entryX, entryY), "the entry delta turns to -y" );

		Assert.AreEqual( 0, ParkState.CellFor( park, 17, 10 ).Neighbours & 0x04,
			"(17,10) starts without the bit pointing back at the way in" );

		ParkBuilding.MarkWaysInAndOut( state, park, 18 + entryX, 11 + entryY, 18, 14, 90 );

		Assert.AreEqual( 0x40, ParkState.CellFor( park, 18, 10 ).Direction,
			"a quarter turn takes the way in from north to west" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 17, 10 ).Neighbours & 0x04,
			"so the cell it faces is the one to the WEST - turned the other way it faced east, back across the thing" );
	}

	/// <summary>
	/// A way out placed against an existing path <b>retiles that path</b>, so the join is visible and
	/// not merely recorded.
	///
	/// <para>
	/// <b>This is the case Alexah reported and the one nothing in this suite reached.</b> Both tests
	/// above stand their ends on bare ground, so the arm that runs when the faced cell IS a path - the
	/// link pass, and the retile after it - had no cover at all. The link was being made correctly the
	/// whole time; what was missing was the art, because <see cref="ParkPaths"/> redraws each cell from
	/// its STORED tile index and nothing asked the joined cell to work out a new one.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> removing the <c>RetileAround</c> call from <c>JoinToWhateverIsThere</c> leaves
	/// the path drawing tile 0 and fails the last two assertions, while the mask assertions still pass -
	/// which is exactly the shape of the fault as it was reported.
	/// </remarks>
	[TestMethod]
	public void AWayOutPlacedAgainstAPathRetilesThatPath()
	{
		var park = World( GameData.Required() );
		var state = new ParkState( park );

		// A path cell with no neighbours, drawing the tile a lone path cell draws. (16,11) is clear of
		// the park's own walkways, which run x 39..57, y 15..29.
		state.SetRecord( 16, 11, ParkState.CellFor( park, 16, 11 ) with
		{
			Type = CellEdge.Path,
			TileSet = ParkPaths.PathTileSet,
			Neighbours = 0,
			TileIndex = 0,
			TileAngle = 0
		} );

		Assert.AreEqual( 0, ParkState.CellFor( park, 16, 11 ).TileIndex, "a lone path cell draws tile 0" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 16, 11 ).Neighbours & 0x01,
			"and starts with no link northward, which is the one this places" );

		// The way out at (16,10) faces +y onto that path; the way in is put well clear of it.
		ParkBuilding.MarkWaysInAndOut( state, park, 16, 7, 16, 10, 0 );

		Assert.AreEqual( CellEdge.RideFarEnd, ParkState.CellFor( park, 16, 10 ).Type, "the way out is typed 10" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 16, 11 ).Neighbours & 0x01,
			"the path gains the link back toward the way out" );

		// And the art follows the mask, which is the half that was missing: mask 0x01 is the table's
		// own single-ended piece, index 1 at 180 degrees.
		Assert.AreEqual( 1, ParkState.CellFor( park, 16, 11 ).TileIndex,
			"the joined path redraws as a single-ended piece instead of the lone tile it was" );
		Assert.AreEqual( 180, ParkState.CellFor( park, 16, 11 ).TileAngle,
			"and faces the way out it joined" );
	}

	/// <summary>
	/// How many of the jungle's rides declare an entrance at all - <b>and six of them do not</b>, which
	/// is why they get no way in, no way out and nothing a queue can attach to.
	/// </summary>
	/// <remarks>
	/// <b>Measured from the shipped item descriptions, not chosen.</b> An item whose picture carries no
	/// <c>S</c> has all four of its deltas zeroed by <see cref="ItemDescriptionFile"/> - the <c>2</c> it
	/// may carry is discarded with them - so entry and exit would both fall on the anchor. The six are
	/// the three coasters, the go-karts, the water ride and the TV simulator.
	/// <para>
	/// <b>The original may well mark the way out of those six anyway</b>, since its placer walks the
	/// shape grid with independent <c>case 9</c> and <c>case 10</c> arms rather than using the
	/// derivation that takes the no-9 fallback. That is not decoded, so
	/// <see cref="ParkBuilding"/> marks neither end and counts it.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void SixOfTheJunglesRidesDeclareNoEntranceAtAll()
	{
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );

		var rides = 0;
		var withEntrance = 0;

		foreach ( var item in catalogue.All )
		{
			if ( item.UiType != 0 )
				continue;

			++rides;

			if ( item.HasEntrance )
				++withEntrance;
		}

		Assert.AreEqual( 17, rides, "UI type 0 items in the jungle catalogue" );
		Assert.AreEqual( 11, withEntrance, $"of {rides} jungle rides, this many mark an entrance" );
		Assert.AreEqual( 6, rides - withEntrance,
			"the rest declare none, so nothing can be queued for them until the shape grid is decoded" );
	}
}
