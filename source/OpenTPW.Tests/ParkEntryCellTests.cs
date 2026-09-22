using Microsoft.VisualStudio.TestTools.UnitTesting;

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
	/// is eight wide - <c>0x01 N, 0x02 NE, 0x04 E, …</c> - so a quarter turn moves a bit two places.
	/// </summary>
	/// <remarks>
	/// <b>The unturned values are measured, not chosen</b>: the shipped Belly Bounce at angle 0 carries
	/// <c>direction 0x01</c> on its type-9 entrance at (52,23) and <c>0x10</c> on its type-10 exit at
	/// (52,26), read out of the running game.
	/// <para>
	/// <b>Mutation:</b> rotating one place a quarter instead of two puts the byte on a diagonal, which no
	/// cell of the shipped park carries on either field, and fails here.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void AWayInCarriesItsHeadingRoundWithTheThing()
	{
		Assert.AreEqual( 0x01, ParkBuilding.RotateBit( 0x01, 0 ), "no turn leaves it alone" );
		Assert.AreEqual( 0x04, ParkBuilding.RotateBit( 0x01, 90 ), "a quarter moves it two places" );
		Assert.AreEqual( 0x10, ParkBuilding.RotateBit( 0x01, 180 ), "a half is the opposite bit" );
		Assert.AreEqual( 0x40, ParkBuilding.RotateBit( 0x01, 270 ) );

		Assert.AreEqual( 0x01, ParkBuilding.RotateBit( 0x01, 360 ), "a full circle comes home" );

		// The way out starts on the opposite bit to the way in and stays opposite through every turn,
		// which is what keeps a ride's two ends at the two ends of its middle column.
		foreach ( var angle in new[] { 0, 90, 180, 270 } )
		{
			Assert.AreEqual( CellEdge.Opposite( ParkBuilding.RotateBit( 0x01, angle ) ),
				ParkBuilding.RotateBit( 0x10, angle ), $"the two ends stay opposite at {angle}" );
		}
	}
}
