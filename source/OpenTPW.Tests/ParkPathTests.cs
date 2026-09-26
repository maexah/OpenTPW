using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The paths a park was laid out with, and the table that names the art they draw. These read real game
/// files and are skipped where there is no installation: see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkPathTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private TextureTableFile Table()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/Jungle.tct" ) );
		return new TextureTableFile( stream );
	}

	/// <summary>
	/// The theme's texture table, which is the only place a path tile is named.
	///
	/// <para>
	/// Three of these are the file's own oddities rather than parsing faults, and each is pinned so that
	/// a reader "correcting" one would be caught: <c>jpa_ctr1</c> appears at both 13 and 14 (the original
	/// author's comment at the top of the file says why), <c>QueueTex</c>'s names are not in numeric
	/// order, and the <c>Water</c> section is commented out and so must not exist at all.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheThemesTableNamesEveryPathTile()
	{
		var table = Table();

		CollectionAssert.AreEqual( new[] { "PathTex", "QueueTex" }, table.SectionNames.ToArray(),
			"the sections of Jungle.tct - Water is commented out and must not be read as one" );

		var paths = table.Rows( "PathTex" );

		Assert.AreEqual( 22, paths.Count, "PathTex rows" );
		Assert.AreEqual( "jpa_squ1.tga", table.NameFor( "PathTex", 0 ) );
		Assert.AreEqual( "jpa_str1.tga", table.NameFor( "PathTex", 2 ) );
		Assert.AreEqual( "jpa_xrd1.tga", table.NameFor( "PathTex", 5 ) );
		Assert.AreEqual( "jpa_str2.tga", table.NameFor( "PathTex", 19 ) );

		Assert.AreEqual( table.NameFor( "PathTex", 13 ), table.NameFor( "PathTex", 14 ),
			"13 and 14 are the same art on purpose - the file says so in a comment" );

		Assert.AreEqual( 4, table.Rows( "QueueTex" ).Count, "QueueTex rows" );
		Assert.AreEqual( "jpa_que4.tga", table.NameFor( "QueueTex", 0 ),
			"QueueTex is not in numeric order, so nothing may be inferred from a name" );

		Assert.AreEqual( string.Empty, table.NameFor( "PathTex", 99 ), "a row the table does not have" );
	}

	/// <summary>
	/// Every tile the shipped park asks for is one the theme actually ships art for. This is what makes
	/// the tile index usable at all: it is a row of the table above, and that row names a file on disk.
	/// </summary>
	[TestMethod]
	public void EveryPathTileNamesArtTheThemeShips()
	{
		var table = Table();

		var used = World().Cells
			.Where( ParkPaths.IsPath )
			.Select( cell => cell.TileIndex )
			.Distinct()
			.OrderBy( index => index )
			.ToArray();

		Assert.AreEqual( 11, used.Length, "distinct path tiles the park uses" );

		foreach ( var index in used )
		{
			var name = table.NameFor( "PathTex", index );

			Assert.AreNotEqual( string.Empty, name, $"PathTex should name tile {index}" );

			// Named .tga in the table, shipped as .wct beside it - the one translation this needs.
			var stem = name[..name.LastIndexOf( '.' )];

			// Read it rather than asking FileExists, which is File.Exists on an absolute path and so is
			// blind to everything inside a .wad - it answers false for every one of these.
			var art = data.ReadAllBytes( $"levels/jungle/terrain/pathtex/{stem}.wct" );

			Assert.IsTrue( art.Length > 0,
				$"tile {index} ('{name}') should have art at pathtex/{stem}.wct" );
		}
	}

	/// <summary>
	/// Why the ground has to be told to skip these cells, rather than the paths simply being laid over
	/// whatever is there.
	///
	/// <para>
	/// A path cell is <b>ordinary drawn ground</b> in the model - it carries a real ground texture index,
	/// not the 0 that means "the scenery covers this". So the two surfaces would otherwise be built at
	/// identical heights over identical cells and fight for the same depth. The contrast is the park's
	/// fixed approach, which <i>is</i> index 0 throughout, because the entrance road really is scenery.
	/// </para>
	/// </summary>
	[TestMethod]
	public void APathCellIsOrdinaryGroundAndTheFixedApproachIsNot()
	{
		HeightfieldFile field;

		using ( var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) ) )
			field = new HeightfieldFile( stream );

		var world = World();
		var laid = 0;
		var approach = 0;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				var cell = world.CellAt( x, y );
				var ground = field.TextureAt( x, y );

				if ( ParkPaths.IsPath( cell ) )
				{
					Assert.AreNotEqual( 0, ground,
						$"the path at ({x},{y}) should be drawn ground, which is why it has to be skipped" );
					++laid;
				}

				if ( cell.Type != 30 )
					continue;

				Assert.AreEqual( 0, ground,
					$"the fixed approach at ({x},{y}) should be left to the scenery" );
				++approach;
			}
		}

		Assert.AreEqual( 78, laid, "path cells inside the heightfield" );
		Assert.AreEqual( 66, approach, "cells of the fixed approach" );
	}

	/// <summary>
	/// The tile tables read out of the executable reproduce the shipped park's own tiles.
	///
	/// <para>
	/// <b>Neither source can have been fitted to the other</b>, which is what makes this worth
	/// asserting: the tables are compiled into <c>testme.exe</c> (<c>FUN_00535dd0</c>, 49 path rows at
	/// <c>DAT_00763138</c> and 11 queue rows at <c>DAT_007630b0</c>) and the save was written by a
	/// different program years earlier. A park being EDITED needs this, because a cell nobody has drawn
	/// yet has no stored tile to read.
	/// </para>
	/// <para>
	/// <b>The index is asserted against the base OR its variant, deliberately.</b> The original carries
	/// <c>rand() &amp; 1</c> between calls and rewrites index 2 to 19 and 10 to 20, so a path tile is
	/// not a function of the mask at all - 51 of these 78 cells carry the base and the other 27 carry a
	/// variant. Demanding one fixed index would be asserting something the original does not do.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheExecutablesTileTablesReproduceTheShippedPark()
	{
		var world = World();

		var paths = 0;
		var queues = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var cell = world.CellAt( x, y );

				if ( cell.Type == 1 )
				{
					var (set, index, angle) = ParkPathTiles.TileFor( cell.Type, cell.Neighbours, cell.Direction );

					Assert.AreEqual( ParkPaths.PathTileSet, set, $"tile set at ({x},{y})" );
					Assert.AreEqual( cell.TileAngle, angle, $"tile angle at ({x},{y}), mask 0x{cell.Neighbours:x2}" );

					Assert.IsTrue( cell.TileIndex == index || cell.TileIndex == ParkPathTiles.Vary( index ),
						$"tile index at ({x},{y}), mask 0x{cell.Neighbours:x2}: stored {cell.TileIndex}, " +
						$"table {index} or its variant {ParkPathTiles.Vary( index )}" );

					++paths;
				}

				if ( cell.Type != ParkRideChoice.QueueCellType )
					continue;

				// A queue's index takes three more for each cardinal link that reaches a path, which is
				// what makes the cell where the queue meets the path draw the end piece.
				var links = 0;

				foreach ( var (bit, acrossBy, downBy) in new[] { (0x01, 0, -1), (0x04, 1, 0), (0x10, 0, 1), (0x40, -1, 0) } )
				{
					if ( (cell.Neighbours & bit) != 0 && world.CellAt( x + acrossBy, y + downBy ).Type == 1 )
						++links;
				}

				var queue = ParkPathTiles.TileFor( cell.Type, cell.Neighbours, cell.Direction, links );

				Assert.AreEqual( ParkQueues.QueueTileSet, queue.Set, $"queue tile set at ({x},{y})" );
				Assert.AreEqual( cell.TileIndex, queue.Index, $"queue tile index at ({x},{y})" );
				Assert.AreEqual( cell.TileAngle, queue.Angle, $"queue tile angle at ({x},{y})" );

				++queues;
			}
		}

		Assert.AreEqual( 78, paths, "path cells the tables were checked against" );
		Assert.AreEqual( 4, queues, "queue cells the tables were checked against" );
	}

	/// <summary>
	/// A cell a player has changed is answered from the running park, and a cell nobody has touched is
	/// answered from the file - <see cref="ParkState.CellFor"/>, which is the one statement of that rule
	/// and is read by the ground, the paths, the queues, the edge test and the queue walk alike.
	/// </summary>
	[TestMethod]
	public void TheRunningParkAnswersForCellsAPlayerHasChanged()
	{
		var world = World();
		var state = new ParkState( world );

		// Untouched, so the file answers: (47,21) is one of the 78 the shipped park lays path on.
		Assert.AreEqual( 1, ParkState.CellFor( world, 47, 21 ).Type, "an untouched path cell" );
		Assert.IsTrue( ParkPaths.IsPath( ParkState.CellFor( world, 47, 21 ) ) );

		// Changed, so the overlay answers - and this is the whole of what makes building possible,
		// because ParkWorld describes a file and may never be written to.
		Assert.IsFalse( ParkPaths.IsPath( ParkState.CellFor( world, 10, 10 ) ), "bare ground to begin with" );

		state.SetRecord( 10, 10, world.CellAt( 10, 10 ) with { Type = 1, TileSet = ParkPaths.PathTileSet } );

		Assert.IsTrue( ParkPaths.IsPath( ParkState.CellFor( world, 10, 10 ) ),
			"a cell the player has laid path on should read as path" );

		// And putting it back reaches the file again rather than writing the old value over the top.
		state.ClearRecord( 10, 10 );

		Assert.IsFalse( ParkPaths.IsPath( ParkState.CellFor( world, 10, 10 ) ), "cleared back to the file" );
	}

	/// <summary>
	/// An overlay belonging to a DIFFERENT park - or to no park at all - must not answer for this one.
	///
	/// <para>
	/// <b>This pins a guard whose removal would otherwise leave the whole suite green, which is why it is
	/// written as a mutation test rather than as a happy path.</b> <see cref="ParkState.Current"/> is a
	/// static that holds whichever overlay a test built last, and twenty test classes build an edge test
	/// over the shipped park through <see cref="CellEdge.For"/>. Let a park-less overlay answer and
	/// <see cref="ParkState.Record"/> returns <c>default</c> for every cell - and a default cell is
	/// <b>type 0</b>, bare ground - so every route, every queue walk and every edge test in the park
	/// would quietly change its answer instead of failing.
	/// </para>
	/// <para>
	/// <b>Measured by putting the bug back: take the <c>ReferenceEquals</c> out of
	/// <see cref="ParkState.CellFor"/> and SEVEN tests fail</b> - this one, and six more across the
	/// queue walk and the offer filter, because a park-less overlay makes every cell read as type 0.
	/// The ride's queue walk drops from four cells to nought, and the objects a guest may be offered
	/// collapse from six to two.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnOverlayForAnotherParkDoesNotAnswerForThisOne()
	{
		var world = World();

		// The two-fact overlay a behaviour test builds. Its park is null, and constructing it makes it
		// ParkState.Current - which is exactly the accident this guards against.
		_ = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		Assert.AreEqual( 1, ParkState.CellFor( world, 47, 21 ).Type,
			"a park-less overlay must not answer - the file says this cell is path" );

		// Belly Bounce's footprint, which would also read as bare ground under a mismatched overlay.
		Assert.AreEqual( 4, ParkState.CellFor( world, 51, 23 ).Type,
			"a park-less overlay must not answer - the file says this cell is a footprint" );

		// A second park's overlay is the same mistake wearing a real save, so it is refused the same way.
		var other = new ParkState( World() );

		Assert.AreEqual( 1, ParkState.CellFor( world, 47, 21 ).Type,
			"an overlay seeded from a different ParkWorld must not answer for this one" );

		Assert.IsNotNull( other.Park, "the other overlay really does have a park of its own" );
	}
}
