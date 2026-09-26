using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// Where a guest walks up to a thing the player has built - the entrance and exit marks in the item's own
/// <c>Info.Shape</c> picture, turned by the angle it is built at - and what the placer builds in front of
/// them.
///
/// <para>
/// <b>The picture tests need no game files</b>, because the description reader takes text directly.
/// </para>
/// <para>
/// <b>The characters are the executable's own alphabet</b>, the nineteen-row table at <c>0x007396c8</c>:
/// <c>2</c> is an entrance facing <c>0x10</c> and <c>S</c> an exit facing the same, and the reader turns the
/// rows upside down (<c>0x00402938</c>..<c>0x004029ab</c>). The shipped save's eleven placed objects all
/// fall out of that reading - see <see cref="EveryPlacedThingsEntryAndExitCellFallOutOfItsPicture"/>.
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
		Assert.AreEqual( 0, item.EntryDeltaY, "the 2 is on the LAST row drawn, which the reader turns into row 0" );
		Assert.AreEqual( 0x10, item.EntryDirection, "a 2 faces 0x10" );
		Assert.AreEqual( 1, item.ExitDeltaX, "exit column" );
		Assert.AreEqual( 3, item.ExitDeltaY, "and the S, drawn first, ends up last" );
		Assert.AreEqual( 0x10, item.ExitDirection, "an S faces 0x10 as well" );
	}

	/// <summary>
	/// The two shipped pictures that tell the upside-down reading apart from the right-way-up one: the
	/// Staff Room and the Jungle Spray each draw their <c>2</c> on the last row, and the save puts their
	/// entrances on the anchor's own row.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> leaving the rows the way they are drawn puts both entrances on the far row, and
	/// fails both pairs of assertions.
	/// </remarks>
	[TestMethod]
	public void ThePictureIsReadUpsideDown()
	{
		var staffRoom = Described( "**\n*2\n" );

		Assert.AreEqual( (1, 0), (staffRoom.EntryDeltaX, staffRoom.EntryDeltaY), "the Staff Room enters on row 0" );

		var spray = Described( "***\n***\n*2*\n" );

		Assert.AreEqual( (1, 0), (spray.EntryDeltaX, spray.EntryDeltaY), "and so does the Jungle Spray" );
	}

	/// <summary>
	/// The Drinks Shop's own picture: a <c>2</c> and no exit, so the exit falls on the entrance and
	/// carries its bit - which is what makes <c>mExitPos</c> equal <c>mEntryPos</c> on ten of the shipped
	/// park's eleven placed objects.
	/// </summary>
	[TestMethod]
	public void AnEntranceWithNoExitPutsTheExitOnTheEntrance()
	{
		var item = Described( "**\n2*\n" );

		Assert.IsTrue( item.HasEntrance, "a 2 is an entrance" );
		Assert.AreEqual( (0, 0), (item.EntryDeltaX, item.EntryDeltaY) );
		Assert.AreEqual( (item.EntryDeltaX, item.EntryDeltaY), (item.ExitDeltaX, item.ExitDeltaY),
			"the exit falls onto the entrance" );
		Assert.AreEqual( item.EntryDirection, item.ExitDirection, "and takes the entrance's bit with it" );
	}

	/// <summary>A picture with no entrance at all leaves both ends on the anchor, with FUN_00413410's two defaults.</summary>
	[TestMethod]
	public void AnItemWithNoEntranceMarkFallsBackToItsAnchorCell()
	{
		var item = Described( "***\n*S*\n" );

		Assert.IsFalse( item.HasEntrance, "an S alone is an exit, not an entrance" );
		Assert.AreEqual( (0, 0, 0, 0), (item.EntryDeltaX, item.EntryDeltaY, item.ExitDeltaX, item.ExitDeltaY),
			"the exit is ignored without an entrance, as the engine does" );
		Assert.AreEqual( 0x01, item.EntryDirection );
		Assert.AreEqual( 0x10, item.ExitDirection );
	}

	/// <summary>
	/// The reader's two quieter rules, which no shipped picture exercises: a space is not a cell, and a
	/// blank line is still a row.
	/// </summary>
	[TestMethod]
	public void ASpaceIsNotACellAndABlankLineIsARow()
	{
		var item = Described( "* N *\n\n*2*\n" );

		Assert.AreEqual( 3, item.FootprintWidth, "the spaces take no column" );
		Assert.AreEqual( 3, item.FootprintDepth, "and the blank line is a row" );
		Assert.AreEqual( (1, 0), (item.EntryDeltaX, item.EntryDeltaY) );
		Assert.AreEqual( (1, 2), (item.ExitDeltaX, item.ExitDeltaY) );
		Assert.AreEqual( 0x01, item.ExitDirection, "an N faces 0x01" );
	}

	/// <summary>
	/// The engine's own turn table, <c>MapDelta::Rotate</c>. It asserts on anything but a quarter turn, so
	/// the four arms are the whole of it.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> swapping the 90 and 270 arms fails here, and would put a bought ride's entrance on
	/// the wrong side of it in a park.
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
	/// The corpus, read from the shipped files rather than asserted from one of them: <b>every entrance in
	/// the game is a <c>2</c>, and every ride - every item with a queue - has one and an exit of its
	/// own.</b> Needs the game.
	/// </summary>
	[TestMethod]
	public void EveryQueuedItemHasAnEntranceAndAnExitOfItsOwn()
	{
		var data = GameData.Required();
		int items = 0, queued = 0, withEntrance = 0;

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			var catalogue = new ParkItemCatalogue( theme, data );

			foreach ( var item in catalogue.All )
			{
				++items;

				if ( item.HasEntrance )
				{
					++withEntrance;
					Assert.AreEqual( 0x10, item.EntryDirection, $"{theme} '{item.Name}' enters by something other than a 2" );
				}

				if ( !item.HasQueue )
					continue;

				++queued;

				Assert.IsTrue( item.HasEntrance, $"{theme} '{item.Name}' has a queue and no entrance" );
				Assert.AreNotEqual( (item.EntryDeltaX, item.EntryDeltaY), (item.ExitDeltaX, item.ExitDeltaY),
					$"{theme} '{item.Name}' has a queue and no exit of its own" );
			}
		}

		Assert.IsTrue( items > 200, $"only {items} items were catalogued across four themes" );
		Assert.IsTrue( queued > 0 && withEntrance >= queued, $"{queued} queued, {withEntrance} with an entrance" );
	}

	/// <summary>
	/// <b>Every placed thing in the shipped park</b>, its <c>mEntryPos</c> and <c>mExitPos</c> derived from
	/// its own picture, turned by its own angle, and compared with what the original wrote when it was
	/// placed. Needs the game.
	/// </summary>
	/// <remarks>
	/// <b>All eleven match.</b> Read the other way - <c>S</c> as the way in, rows as drawn - two do not: the
	/// Staff Room at 90 degrees and the Jungle Spray. The save's other three objects are the fixed bus,
	/// gates and lights, which stand at (0,0) and are not in the buy catalogue.
	/// </remarks>
	[TestMethod]
	public void EveryPlacedThingsEntryAndExitCellFallOutOfItsPicture()
	{
		var data = GameData.Required();
		var park = World( data );
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var compared = 0;

		foreach ( var placed in park.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var (entryX, entryY) = ParkBuilding.RotateDelta( item.EntryDeltaX, item.EntryDeltaY, placed.Angle );
			var (exitX, exitY) = ParkBuilding.RotateDelta( item.ExitDeltaX, item.ExitDeltaY, placed.Angle );

			Assert.AreEqual( placed.EntryPos, MapStep.CellId( placed.CellX + entryX, placed.CellY + entryY ),
				$"thing {placed.ThingId} '{item.Name}' at ({placed.CellX},{placed.CellY}) turned {placed.Angle}: entry" );
			Assert.AreEqual( placed.ExitPos, MapStep.CellId( placed.CellX + exitX, placed.CellY + exitY ),
				$"thing {placed.ThingId} '{item.Name}': exit" );

			++compared;
		}

		Assert.AreEqual( 11, compared, "placed things the jungle catalogue describes" );
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
	/// <b>The turned values are the rotate's.</b>
	/// <c>FUN_004d8c20</c> left-rotates the byte by <c>log2</c> of the angle's base bit and the placer
	/// pairs base <c>0x40</c> with 90 degrees, so a quarter is a left-rotate of six - a right-rotate of
	/// two - and <see cref="ParkBuilding.RotateDelta"/> turns a cell delta the same way at all four
	/// angles. <c>0x04</c> at 90 would be the other way round.
	/// </para>
	/// <para>
	/// <b>Mutation:</b> rotating one place a quarter instead of two puts the byte on a diagonal, which no
	/// cell of the shipped park carries on either field, and fails here. <b>Rotating the right number of
	/// places the wrong way fails only the 90 and 270 lines</b> - the opposite-ends loop below holds
	/// under either sense, because a rotation commutes with a nibble swap.
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
	/// <b>A queued thing lays one queue cell before its entrance</b> - the node the player lays the rest
	/// of the queue from - and one path cell before its exit. Both are the shipped park's own: the Belly
	/// Bounce's entrance (52,23) faces its queue cell (52,22), which is type 3, NOMODIFY, owned by the
	/// ride and flowing <c>0x10</c> back at it; its exit (52,26) faces (52,27), a NOMODIFY path with the
	/// bit back at the exit and direction nought.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> leaving out the stub leaves (14,9) bare ground and fails everything from the
	/// "queue cell" assertion on, and the placer's return with it, which is what arms the queue tool.
	/// </remarks>
	[TestMethod]
	public void AQueuedThingLaysAQueueCellBeforeItsEntranceAndAPathBeforeItsExit()
	{
		var park = World( GameData.Required() );
		var state = new ParkState( park );
		var owner = MapStep.CellId( 13, 10 );

		// Well clear of the park's own paths, which run x 39..57, y 15..29.
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 14, 9 ).Type, "(14,9) starts as bare ground" );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 14, 14 ).Type, "and so does (14,14)" );

		var node = ParkBuilding.MarkWaysInAndOut( state, park, 14, 10, 14, 13, 0x10, 0x10, 0, hasQueue: true, owner );

		Assert.AreEqual( (14, 9), node, "the placer answers the queue cell it laid" );

		var entrance = ParkState.CellFor( park, 14, 10 );

		Assert.AreEqual( CellEdge.RideEnd, entrance.Type, "the way in is typed 9" );
		Assert.AreEqual( 0x01, entrance.Direction, "and carries Opposite(H), as (52,23) does" );
		Assert.AreNotEqual( 0, entrance.Neighbours & 0x01, "and names the queue cell it faces" );

		var stub = ParkState.CellFor( park, 14, 9 );

		Assert.AreEqual( ParkRideChoice.QueueCellType, stub.Type, "the cell before the entrance is a queue cell" );
		Assert.AreEqual( ParkQueues.QueueTileSet, stub.TileSet, "drawn by the queue renderer" );
		Assert.AreEqual( 1, stub.TileIndex, "as quedead, the one-link piece" );
		Assert.AreEqual( 0x10, stub.Direction, "flowing back at the entrance" );
		Assert.AreEqual( 0x10, stub.Neighbours, "and naming it, and nothing else" );
		Assert.AreEqual( ParkPathBuilding.NoModify, stub.Flags, "NOMODIFY, assigned" );
		Assert.AreEqual( owner, stub.ParentId, "owned by the thing" );

		var exit = ParkState.CellFor( park, 14, 13 );

		Assert.AreEqual( CellEdge.RideFarEnd, exit.Type, "the way out is typed 10" );
		Assert.AreEqual( 0x10, exit.Direction );

		var exitStub = ParkState.CellFor( park, 14, 14 );

		Assert.AreEqual( CellEdge.Path, exitStub.Type, "the cell before the exit is a path" );
		Assert.AreEqual( ParkPathBuilding.NoModify, exitStub.Flags, "NOMODIFY" );
		Assert.AreEqual( 0, exitStub.Direction, "with no direction byte, which is what (52,27) measures" );
		Assert.AreNotEqual( 0, exitStub.Neighbours & 0x01, "joined to the way out" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 14, 13 ).Neighbours & 0x10, "which is joined back" );
	}

	/// <summary>
	/// A thing with no queue lays a PATH before its entrance, and its entrance keeps its own bit rather
	/// than the one pointing at the cell it faces - the shipped park's seven shop and toilet entrances,
	/// each with a NOMODIFY path one step the other way.
	/// </summary>
	[TestMethod]
	public void AThingWithNoQueueLaysAPathBeforeItsEntrance()
	{
		var park = World( GameData.Required() );
		var state = new ParkState( park );

		var node = ParkBuilding.MarkWaysInAndOut( state, park, 20, 10, 20, 10, 0x10, 0x10, 0, hasQueue: false,
			MapStep.CellId( 20, 10 ) );

		Assert.IsNull( node, "no queue cell, so nothing to arm the queue tool on" );
		Assert.AreEqual( 0x10, ParkState.CellFor( park, 20, 10 ).Direction, "the entrance keeps H" );

		var stub = ParkState.CellFor( park, 20, 9 );

		Assert.AreEqual( CellEdge.Path, stub.Type, "the cell before the entrance is a path" );
		Assert.AreEqual( ParkPathBuilding.NoModify, stub.Flags );
		Assert.AreNotEqual( 0, stub.Neighbours & 0x10, "joined to the entrance through the type-9 test" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 20, 10 ).Neighbours & 0x01, "which is joined back" );
		Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park, 20, 11 ).Type, "and nothing is laid behind it" );
	}

	/// <summary>
	/// A thing built at a quarter turn faces the way its entry cell moved.
	///
	/// <para>
	/// <b>Only a quarter or three quarters can tell the two rotate senses apart</b>: a bit and a delta turn
	/// the same way at 0 and at 180 whichever direction the bit is rotated.
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

		var node = ParkBuilding.MarkWaysInAndOut( state, park, 18 + entryX, 11 + entryY, 18, 14, 0x10, 0x10, 90,
			hasQueue: true, MapStep.CellId( 18, 11 ) );

		Assert.AreEqual( 0x40, ParkState.CellFor( park, 18, 10 ).Direction,
			"a quarter turn takes the way in from north to west" );
		Assert.AreEqual( (17, 10), node,
			"so its queue cell is to the WEST - turned the other way it went east, back across the thing" );
	}

	/// <summary>
	/// A way out placed against an existing path <b>joins and retiles that path</b>, so the join is
	/// visible and not merely recorded - and the path becomes the exit's own NOMODIFY cell.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> the path is retiled twice - by the sweep's relink and again by the exit's own path
	/// cell - so removing BOTH retiles leaves it drawing tile 0 and fails the last two assertions, while
	/// the mask assertion still passes. Removing either one alone survives, because the other redraws it.
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

		// The way out at (16,10) faces +y onto that path; the way in is put well clear of it.
		ParkBuilding.MarkWaysInAndOut( state, park, 16, 7, 16, 10, 0x10, 0x10, 0, hasQueue: true, MapStep.CellId( 15, 7 ) );

		Assert.AreEqual( CellEdge.RideFarEnd, ParkState.CellFor( park, 16, 10 ).Type, "the way out is typed 10" );
		Assert.AreNotEqual( 0, ParkState.CellFor( park, 16, 11 ).Neighbours & 0x01,
			"the path gains the link back toward the way out" );
		Assert.AreEqual( ParkPathBuilding.NoModify, ParkState.CellFor( park, 16, 11 ).Flags,
			"and becomes the exit's NOMODIFY cell, as the placer's exit half makes it" );

		// And the art follows the mask: mask 0x01 is the table's own single-ended piece, index 1 at 180.
		Assert.AreEqual( 1, ParkState.CellFor( park, 16, 11 ).TileIndex,
			"the joined path redraws as a single-ended piece instead of the lone tile it was" );
		Assert.AreEqual( 180, ParkState.CellFor( park, 16, 11 ).TileAngle,
			"and faces the way out it joined" );
	}

	/// <summary>
	/// <b>Every one of the jungle's seventeen rides declares an entrance</b>, a <c>2</c> in every case.
	/// Six of them - the three coasters, the go-karts, the water ride and the TV simulator - carry no
	/// <c>S</c> at all, so a reading that took <c>S</c> for the way in would find none on them.
	/// </summary>
	[TestMethod]
	public void EveryOneOfTheJunglesRidesDeclaresAnEntrance()
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
		Assert.AreEqual( 17, withEntrance, "every one of them marks an entrance" );
	}
}
