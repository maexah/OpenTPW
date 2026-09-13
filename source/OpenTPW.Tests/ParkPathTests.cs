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
}
