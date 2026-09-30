using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The researched flag the buy list lists by (<c>docs/exe/hud.md</c>, "What the buy list actually filters on"): the
/// save's record for an item it holds, <c>Upgrades[0].CostOfResearch</c> nought for one it lacks. The first tests build
/// a theme of their own on disk; the rest read Lost Kingdom and skip without the game.
/// </summary>
[TestClass]
public class ParkResearchTests
{
	private string _root = null!;

	[TestInitialize]
	public void Setup()
	{
		Log ??= new();
		_root = Path.Combine( Path.GetTempPath(), $"opentpw-research-{System.Guid.NewGuid():N}" );

		Write( "shops/Shops.sam", "Info.WhichUIType 1\n" );
		Write( "shops/free/free.sam", "Info.Id 1201\nInfo.Name \"Free\"\nUpgrades[0].CostOfResearch 0\n" );
		Write( "shops/dear/dear.sam", "Info.Id 1202\nInfo.Name \"Dear\"\nUpgrades[0].CostOfResearch 500\n" );
		Write( "shops/unsaved/unsaved.sam", "Info.Id 1203\nInfo.Name \"Unsaved\"\n" );
		Write( "shops/costly/costly.sam", "Info.Id 1204\nInfo.Name \"Costly\"\nUpgrades[0].CostOfResearch 300\n" );
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

	private ParkResearch Research( out ParkItemCatalogue catalogue )
	{
		catalogue = new ParkItemCatalogue( "test", new BaseFileSystem( _root ) );

		// The save says no to a free item and yes to a dear one; it holds nothing for the other two.
		return new ParkResearch(
		[
			new ParkWorld.ObjectControl( 1201, Researched: false, TierResearched: 0 ),
			new ParkWorld.ObjectControl( 1202, Researched: true, TierResearched: 2 ),
		], catalogue );
	}

	[TestMethod]
	public void TheSavesFlagWinsOverTheFile()
	{
		var research = Research( out _ );

		Assert.IsFalse( research.IsResearched( 1201 ), "the save holds it unresearched, though its file costs nothing" );
		Assert.IsTrue( research.IsResearched( 1202 ), "the save holds it researched, though its file costs 500" );
		Assert.AreEqual( 2, research.TierResearched( 1202 ) );
	}

	[TestMethod]
	public void AnItemTheSaveLacksIsSeededFromItsCost()
	{
		var research = Research( out _ );

		Assert.IsTrue( research.IsResearched( 1203 ), "no cost set reads nought: researched" );
		Assert.IsFalse( research.IsResearched( 1204 ), "a cost of 300: not researched" );
		Assert.AreEqual( 2, research.FromSave );
		Assert.AreEqual( 2, research.Seeded );
	}

	[TestMethod]
	public void TheBuyListListsOnlyResearchedItems()
	{
		var research = Research( out var catalogue );

		CollectionAssert.AreEqual( new[] { "Dear", "Unsaved" },
			ParkBuyScreen.Listed( catalogue, research, 1 ).Select( item => item.Name ).ToArray() );
	}

	[TestMethod]
	public void AGoldenTicketRideIsAMysteryUntilThePlayerUnlocksIt()
	{
		var ride = new ParkItemCatalogue.Item( 1105, "Ride", "", "ride", 1, 1, null, GoldenTicketCost: 5 );

		Assert.IsTrue( ParkBuyScreen.IsMystery( ride, null ), "nobody playing: nothing unlocked" );
		Assert.IsTrue( ParkBuyScreen.IsMystery( ride, new ushort[] { 1106 } ) );
		Assert.IsFalse( ParkBuyScreen.IsMystery( ride, new ushort[] { 1105 } ), "unlocked with tickets" );
		Assert.IsFalse( ParkBuyScreen.IsMystery( ride with { GoldenTicketCost = 0 }, null ), "no ticket cost" );
	}

	private static ParkWorld ShippedPark( BaseFileSystem data )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	[TestMethod]
	public void TheShippedParkHoldsFiftyRecordsTwentySixResearched()
	{
		var records = ShippedPark( GameData.Required() ).ObjectControlRecords;

		Assert.AreEqual( 50, records.Count );
		Assert.AreEqual( 26, records.Count( record => record.Researched ) );
		Assert.AreEqual( 17, records.Count( record => record.TierResearched == 2 ) );
		Assert.IsTrue( records.All( record => record.TierResearched == 0 || record.Researched ), "a tier only where researched" );
	}

	/// <summary>
	/// The control: in the shipped park the save's flag and the file's cost agree item for item (Q201's measure), so
	/// this passes whichever rule is read; the tab tests below pass only if the filter is applied at all.
	/// </summary>
	[TestMethod]
	public void InTheShippedParkTheSaveAndTheFilesAgree()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );
		var research = new ParkResearch( ShippedPark( data ).ObjectControlRecords, catalogue );

		Assert.AreEqual( 50, research.FromSave );
		Assert.AreEqual( 0, research.Seeded, "the save holds every item Instant Action catalogues" );

		foreach ( var item in catalogue.All )
			Assert.AreEqual( item.ResearchCost == 0, research.IsResearched( item.Id ), item.Name );
	}

	[TestMethod]
	public void LostKingdomsTabsListWhatTheOriginalShowed()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );
		var research = new ParkResearch( ShippedPark( data ).ObjectControlRecords, catalogue );

		string[] Tab( int tab ) => ParkBuyScreen.Listed( catalogue, research, tab ).Select( item => item.Name ).ToArray();

		// q178b/orig/s06-s09.png, the original under Proton.
		CollectionAssert.AreEqual( new[] { "Aztec Mayhem", "Belly Bounce", "Crazy Ape", "Rocky Racers" }, Tab( 0 ) );
		CollectionAssert.AreEqual( new[] { "Balloon Shop", "Burger Shop", "Drinks Shop" }, Tab( 1 ) );
		CollectionAssert.AreEqual( new[] { "Jungle Spray", "Strength Bird" }, Tab( 2 ) );
	}
}
