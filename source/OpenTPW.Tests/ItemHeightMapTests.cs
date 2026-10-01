using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The items' <c>.hmp</c> files and the lift the build squares take from them. These read real game files and are
/// skipped where there is no installation: see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ItemHeightMapTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private static readonly string[] Themes = ["jungle", "fantasy", "hallow", "space"];

	/// <summary>
	/// Every shipped <c>.hmp</c> - an item's own, and the track pieces, pylons, queue pieces and park hoarding in the
	/// same archives - reads with the original's signature, is <c>48 + 27n</c> bytes, and keeps over each cell the
	/// highest of that cell's 25 raster bytes; the four gates are the exception, a raster bare but for its last cell
	/// under a grid of one height. An item's own is the size its <c>.sam</c>'s footprint says, so the original would
	/// rebuild none of them.
	/// </summary>
	[TestMethod]
	public void EveryShippedFileReadsAndFitsItsItem()
	{
		var read = 0;
		var items = 0;
		var gates = 0;

		foreach ( var theme in Themes )
		{
			foreach ( var wad in Directory.GetFiles( Path.Combine( GameDir.Data, "levels", theme ), "*.wad", SearchOption.AllDirectories ) )
			{
				var directory = Path.GetRelativePath( GameDir.Data, Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
				var stem = Path.GetFileName( directory );

				foreach ( var member in data.GetFiles( directory ).Where( name => name.EndsWith( ".hmp", StringComparison.OrdinalIgnoreCase ) ) )
				{
					var name = $"{directory}/{Path.GetFileName( member )}";
					var bytes = data.ReadAllBytes( name );

					var file = ItemHeightMapFile.Read( bytes );
					Assert.IsNotNull( file, $"{name}: the original's loader would not accept its signature" );

					var cells = file.Columns * file.Rows;
					Assert.AreEqual( 48 + (27 * cells), bytes.Length, $"{name}: {file.Columns}x{file.Rows}" );

					var width = file.Columns * ItemHeightMapFile.SamplesPerCell;
					var maxima = Enumerable.Range( 0, cells ).Select( cell => (byte)Enumerable.Range( 0, 25 ).Max( sample =>
						file.Raster[(((cell / file.Columns * 5) + (sample / 5)) * width) + (cell % file.Columns * 5) + (sample % 5)] ) ).ToArray();

					if ( stem.Equals( "gates", StringComparison.OrdinalIgnoreCase ) )
					{
						Assert.AreEqual( 1, file.CellMaxima.Distinct().Count(), $"{name}: the gate's grid is one height" );
						Assert.IsTrue( maxima.Take( cells - 1 ).All( b => b == 0 ), $"{name}: the gate's raster is bare to its last cell" );
						++gates;
					}
					else
					{
						CollectionAssert.AreEqual( maxima, file.CellMaxima, $"{name}: the per-cell grid" );
					}

					Assert.IsTrue( file.Marks.All( b => b is 0 or 1 ), $"{name}: the mark plane" );

					if ( Path.GetFileNameWithoutExtension( member ).Equals( stem, StringComparison.OrdinalIgnoreCase )
						&& data.GetFiles( directory ).Any( other => Path.GetFileName( other ).Equals( $"{stem}.sam", StringComparison.OrdinalIgnoreCase ) ) )
					{
						using var sam = data.OpenRead( $"{directory}/{stem}.sam" );
						var item = new ItemDescriptionFile( sam );
						Assert.AreEqual( (item.FootprintWidth, item.FootprintDepth), (file.Columns, file.Rows),
							$"{name}: the .sam's footprint against the .hmp's" );
						++items;
					}

					++read;
				}
			}
		}

		Assert.AreEqual( 435, read, "every .hmp the four themes ship" );
		Assert.AreEqual( 274, items, "an item's own, beside its .sam" );
		Assert.AreEqual( 4, gates );
	}

	/// <summary>
	/// The lift over the shipped park's built cells: the Belly Bounce (51,23) unturned, the fountain (57,19) a quarter
	/// turn, a path cell and bare ground. Each is ceil10 of the item's height there, the fountain's read through its
	/// turn - (59,19) is the one corner its <c>.hmp</c> keeps low (14 / 2.55, so 10), which a turn ignored misses.
	/// </summary>
	[TestMethod]
	public void TheSquaresLiftOverTheShippedParksBuiltCells()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		(int X, int Y, float Lift)[] expected =
		[
			(51, 23, 20f), (52, 24, 20f), (53, 23, 20f), (53, 26, 10f),
			(58, 18, 40f), (57, 19, 20f), (57, 17, 20f), (59, 17, 20f), (59, 19, 10f),
			(48, 22, 0f), (20, 60, 0f)
		];

		foreach ( var (x, y, lift) in expected )
			Assert.AreEqual( lift, ParkBuildMarkers.LiftOver( state, catalogue, field, x, y ), $"({x},{y})" );
	}

	/// <summary>
	/// A Belly Bounce stood on the hill at (76,64), where the ground is 37: its corner cell is 29 / 2.55 = 11.4 high
	/// over a base of 37, so ceil10( 48 ) = 50. The ground alone would give 40 and the item alone 20.
	/// </summary>
	[TestMethod]
	public void TheLiftCountsTheGroundUnderTheThing()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var state = new ParkState( new ParkWorld( new SaveReader( stream ).ReadFile() ) );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		Assert.AreEqual( 37, (int)field.HeightAt( 76, 64 ), "the hill" );
		Assert.AreEqual( 40f, ParkBuildMarkers.LiftOver( state, catalogue, field, 76, 64 ), "the bare hill: its ground" );

		state.AddObject( new ParkWorld.CatalogueObject( state.NextThingId(), 1100, 76 << 8, 64 << 8, 0 ) );

		Assert.AreEqual( 50f, ParkBuildMarkers.LiftOver( state, catalogue, field, 76, 64 ) );
	}

	/// <summary>The original's rounding: <c>(t + 9) / 10 * 10</c>, integer division towards nought.</summary>
	[TestMethod]
	public void TheLiftRoundsUpToTen()
	{
		Assert.AreEqual( 0f, ParkBuildMarkers.CeilingTen( 0 ) );
		Assert.AreEqual( 10f, ParkBuildMarkers.CeilingTen( 1 ) );
		Assert.AreEqual( 10f, ParkBuildMarkers.CeilingTen( 10 ) );
		Assert.AreEqual( 20f, ParkBuildMarkers.CeilingTen( 11 ) );
		Assert.AreEqual( 0f, ParkBuildMarkers.CeilingTen( -5 ) );
	}
}
