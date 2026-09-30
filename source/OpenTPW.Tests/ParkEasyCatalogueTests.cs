using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Instant Action's catalogue: each item's <c>Easy_&lt;stem&gt;.sam</c> laid over its own file, an item whose wad has
/// none left out, and a value the original's loader refuses (<c>park-engine.md</c>, "How a key finds its global").
/// The first ones build a theme of their own on disk; the last three read Lost Kingdom and skip without the game.
/// </summary>
[TestClass]
public class ParkEasyCatalogueTests
{
	private string _root = null!;

	[TestInitialize]
	public void Setup()
	{
		Log ??= new();
		_root = Path.Combine( Path.GetTempPath(), $"opentpw-easy-{System.Guid.NewGuid():N}" );

		Write( "shops/Shops.sam", "Info.WhichUIType 1\nInfo.IsChoosable 1\n" );
		Write( "shops/fries/fries.sam", "Info.Id 1201\nInfo.Name \"Fries\"\nInfo.AttractionValue 10\nInfo.HasQueue 1\n" );

		// Lower case where the stem is not: the original's wad lookup lowercases both sides.
		Write( "shops/fries/easy_Fries.sam", "Info.AttractionValue 30\n" );
		Write( "shops/gift/gift.sam", "Info.Id 1202\nInfo.Name \"Gift\"\n" );
		Write( "shops/bad/bad.sam", "Info.Id 1203\nInfo.Name \"Bad\"\nInfo.IsChoosable 2\n" );
		Write( "shops/bad/Easy_bad.sam", "# comments only\n" );
	}

	[TestCleanup]
	public void Cleanup()
	{
		if ( Directory.Exists( _root ) )
			Directory.Delete( _root, recursive: true );
	}

	private void Write( string path, string text )
	{
		var full = Path.Combine( _root, "levels", "test", path );
		Directory.CreateDirectory( Path.GetDirectoryName( full )! );
		File.WriteAllText( full, text );
	}

	private ParkItemCatalogue Catalogue( bool instantAction ) => new( "test", new BaseFileSystem( _root ), instantAction );

	[TestMethod]
	public void InstantActionLeavesOutAnItemWithNoEasyFile()
	{
		Assert.IsFalse( Catalogue( instantAction: true ).TryGet( 1202, out _ ), "the gift shop's wad has no Easy_ file" );
		Assert.IsTrue( Catalogue( instantAction: false ).TryGet( 1202, out _ ), "the standard catalogue keeps it" );
	}

	[TestMethod]
	public void TheEasyFileIsLaidOverTheItemsOwn()
	{
		Assert.IsTrue( Catalogue( instantAction: true ).TryGet( 1201, out var easy ) );
		Assert.AreEqual( 30, easy.AttractionValue, "the Easy_ file's value, matched without regard to case" );
		Assert.IsTrue( easy.HasQueue, "a key the Easy_ file leaves alone keeps the item's own" );
		Assert.IsTrue( easy.IsChoosable, "and the category's" );

		Assert.IsTrue( Catalogue( instantAction: false ).TryGet( 1201, out var standard ) );
		Assert.AreEqual( 10, standard.AttractionValue, "the standard catalogue reads no Easy_ file" );
	}

	[TestMethod]
	public void AnItemWithARefusedValueIsLeftOutAndCounted()
	{
		Unimplemented.Forget();

		var catalogue = Catalogue( instantAction: true );

		Assert.IsFalse( catalogue.TryGet( 1203, out _ ), "IsChoosable is bounded [0, 2), so 2 is refused" );
		Assert.AreEqual( 1, catalogue.Count, "the rest of the catalogue still loads" );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( one => one.What == "ITEM_VALUE_REFUSED" ).Times );
	}

	[TestMethod]
	[DataRow( "Info.IsChoosable 1", false )]
	[DataRow( "Info.IsChoosable 2", true )]
	[DataRow( "Info.NewAttractionDecayTime 0", true )]
	[DataRow( "Info.NewAttractionDecayTime 999", false )]
	[DataRow( "Info.NewAttractionDecayTime 1000", true )]
	[DataRow( "UsageInfo.ExciteFactor 49", true )]
	[DataRow( "Info.AttractionValue -1", true )]
	[DataRow( "Upgrades[2].WearRate -1", true )]
	[DataRow( "Upgrades[2].WearRate 0", false )]
	[DataRow( "UsageInfo.ThirstEffect -5", false )]
	[DataRow( "Info.IsChooseable 1", true )]
	[DataRow( "info.IsChoosable 1", true )]
	[DataRow( "Info.HasQueue 1.5", true )]
	[DataRow( "Info.HasQueue yes", true )]
	[DataRow( "Info.HasQueue +1", true )]
	[DataRow( "Upgrades[1].QueueWaitTimeConstant 1.5", false )]
	[DataRow( "Upgrades[1].QueueWaitTimeConstant 1.5.1", true )]
	[DataRow( "Info.Name \"Two Words\"", false )]
	public void TheSchemasBoundsDecideARefusal( string line, bool refused )
		=> Assert.AreEqual( refused, new ItemDescriptionFile( $"Info.Id 1\n{line}\n" ).Refused != null, line );

	[TestMethod]
	public void AShapesRowsAreNotKeys()
		=> Assert.IsNull( new ItemDescriptionFile( "Info.Id 1\nInfo.Shape\n---\n*2*\n---\nInfo.HasQueue 1\n" ).Refused );

	[TestMethod]
	public void ARefusedCategoryLeavesOutItsFolderCountedOnce()
	{
		Write( "shops/Shops.sam", "Info.WhichUIType 1\nInfo.IsChoosable 9\n" );
		Unimplemented.Forget();

		Assert.AreEqual( 0, Catalogue( instantAction: false ).Count );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( one => one.What == "ITEM_VALUE_REFUSED" ).Times );
	}

	[TestMethod]
	public void TheWearAndResearchKeysAreCounted()
	{
		Write( "shops/fries/easy_Fries.sam", "Upgrades[0].WearRate 3\nUpgrades[2].WearRate 1\nResearch.Group 2\n" );
		Unimplemented.Forget();

		Catalogue( instantAction: true );

		Assert.AreEqual( 2, Unimplemented.Summary.Single( one => one.What == "ITEM_WEAR_RATE" ).Times );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( one => one.What == "ITEM_RESEARCH_KEYS" ).Times );
	}

	[TestMethod]
	public void LostKingdomsInstantActionCataloguesFifty()
	{
		var data = GameData.Required();

		var easy = new ParkItemCatalogue( "jungle", data, instantAction: true );
		var standard = new ParkItemCatalogue( "jungle", data );

		Assert.AreEqual( 50, easy.Count, "park-engine.md: 50 of Lost Kingdom's item wads hold an Easy_ file" );
		Assert.AreEqual( 67, standard.Count );

		var shops = easy.All.Where( item => item.UiType == 1 ).Select( item => item.Name ).ToList();

		Assert.AreEqual( 6, shops.Count, string.Join( ", ", shops ) );
		CollectionAssert.DoesNotContain( shops, "Gift Shop" );
		CollectionAssert.DoesNotContain( shops, "Steak Restaurant" );
	}

	[TestMethod]
	public void EveryShopAndSideshowStaysNewForTheLowerBound()
	{
		var data = GameData.Required();

		foreach ( var item in new ParkItemCatalogue( "jungle", data, instantAction: true ).All
			.Where( item => item.UiType is 1 or 2 ) )
			Assert.AreEqual( 1, item.NewAttractionDecayTime, item.Name );
	}

	[TestMethod]
	public void NoFileLostKingdomShipsHasARefusedLine()
	{
		var data = GameData.Required();
		var read = 0;

		foreach ( var folder in new[] { "features", "shops", "rides", "sideshow", "upgrades" } )
		{
			var path = $"levels/jungle/{folder}";

			// What the loader reads: the category file, and each item's own and its layers. A coaster's Coaster.sam
			// (coaster1, coaster3, minecart) is its track's textures, not an item description.
			var files = data.GetFiles( path ).Where( file => Named( file, folder ) )
				.Concat( data.GetDirectories( path )
					.SelectMany( directory => data.GetFiles( directory ).Where( file => Named( file, Path.GetFileName( directory ) ) ) ) );

			foreach ( var file in files )
			{
				Assert.IsNull( new ItemDescriptionFile( data.ReadAllText( file ) ).Refused, file );
				++read;
			}
		}

		// Five category files and Online_Rides.sam; in the wads 70 own, 50 Easy_ and 2 Online_.
		Assert.AreEqual( 128, read, "every item description Lost Kingdom's item folders hold" );
	}

	private static bool Named( string file, string stem )
	{
		var name = Path.GetFileName( file );

		return new[] { "", "Easy_", "Online_" }.Any( prefix =>
			name.Equals( $"{prefix}{stem}.sam", System.StringComparison.OrdinalIgnoreCase ) );
	}
}
