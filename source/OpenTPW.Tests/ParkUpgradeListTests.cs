using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The ride window's Buy Upgrades list (Q512; <c>docs/exe/ride-operation.md</c>, "The ride window's Buy Upgrades list"):
/// its rows from the ride's item, level, state and research, and what a choice does with the balance. The first tests
/// build a theme of their own on disk, where each add-on's control price differs from its own build price; the last
/// reads Lost Kingdom's Dino Karts and skips without the game.
/// </summary>
[TestClass]
public class ParkUpgradeListTests
{
	private const int Kart = 1150;
	private const int Spin = 1160;
	private const int Tunnel = 1501;
	private const int Jump = 1502;

	private string _root = null!;
	private ParkItemCatalogue _catalogue = null!;

	[TestInitialize]
	public void Setup()
	{
		Log ??= new();
		_root = Path.Combine( Path.GetTempPath(), $"opentpw-upgrades-{System.Guid.NewGuid():N}" );

		Write( "rides/Rides.sam", "Info.WhichUIType 0\n" );
		Write( "rides/kart/kart.sam", "Info.Id 1150\nInfo.Name \"Kart\"\nUpgrades[0].CostOfUpgrade 1000\n"
			+ "Upgrades[1].CostOfUpgrade 1500\nUpgrades[2].CostOfUpgrade 2300\n" );
		Write( "rides/spin/spin.sam", "Info.Id 1160\nInfo.Name \"Spin\"\nUpgrades[0].CostOfUpgrade 800\n" );
		Write( "upgrades/Upgrades.sam", "Info.WhichUIType 0\n" );
		Write( "upgrades/tunnel/tunnel.sam", "Info.Id 1501\nInfo.Name \"Tunnel\"\nAddOn.UpgradesId 1150\nUpgrades[0].CostOfUpgrade 600\n" );
		Write( "upgrades/jump/jump.sam", "Info.Id 1502\nInfo.Name \"Jump\"\nAddOn.UpgradesId 1150\nUpgrades[0].CostOfUpgrade 700\n" );
		Write( "upgrades/unready/unready.sam", "Info.Id 1503\nInfo.Name \"Unready\"\nAddOn.UpgradesId 1150\n" );
		Write( "upgrades/other/other.sam", "Info.Id 1504\nInfo.Name \"Other\"\nAddOn.UpgradesId 1170\n" );

		_catalogue = new ParkItemCatalogue( "test", new BaseFileSystem( _root ) );
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

	/// <summary>The park's control records: the kart researched to <paramref name="kartTier"/>, the tunnel at 900 and the jump at 750 (not their build prices), one add-on unresearched.</summary>
	private ParkResearch Research( int kartTier ) => new(
	[
		new ParkWorld.ObjectControl( Kart, Researched: true, TierResearched: kartTier, Price: 1000 ),
		new ParkWorld.ObjectControl( Spin, Researched: true, TierResearched: 2, Price: 800 ),
		new ParkWorld.ObjectControl( Tunnel, Researched: true, TierResearched: 0, Price: 900 ),
		new ParkWorld.ObjectControl( Jump, Researched: true, TierResearched: 0, Price: 750 ),
		new ParkWorld.ObjectControl( 1503, Researched: false, TierResearched: 0, Price: 100 ),
		new ParkWorld.ObjectControl( 1504, Researched: true, TierResearched: 0, Price: 100 ),
	], _catalogue );

	private static ParkWorld.CatalogueObject Ride( int item, int level = 0, int state = 0 )
		=> new( 60, item, 0, 0, 0, State: state, UpgradeLevel: level );

	private static string Shape( System.Collections.Generic.IEnumerable<ParkUpgradeList.Row> rows )
		=> string.Join( "; ", rows.Select( row => $"{row.Kind} {row.Value} {row.Text} {row.Shown} {row.Price}" ) );

	[TestMethod]
	public void TheAddOnsAtTheirRecordsPricesThenTheNextLevelAtItsCostOfUpgrade()
	{
		var rows = ParkUpgradeList.For( Ride( Kart ), _catalogue, Research( kartTier: 1 ), instantAction: false );

		// The ride's own researched add-ons in the catalogue's order, at the control record's +0x04 (not the 600 and 700 the
		// files give); the unresearched one and another ride's left out; then UITEXT 29 with level 0 + 2 at CostOfUpgrade[1].
		Assert.AreEqual( "AddOn 1502 0 0 750; AddOn 1501 0 0 900; Level -1 29 2 1500", Shape( rows ) );
	}

	[TestMethod]
	public void InstantActionOffersNothingButItsMessage()
	{
		var rows = ParkUpgradeList.For( Ride( Kart ), _catalogue, Research( kartTier: 2 ), instantAction: true );

		Assert.AreEqual( "Message -7890 27 0 0", Shape( rows ) );
	}

	[TestMethod]
	public void AWaitingUpgradeIsOfferedOnlyItsCancelAfterTheAddOns()
	{
		var rows = ParkUpgradeList.For( Ride( Kart, level: 1, state: ParkUpgradeList.UpgradeWaiting ), _catalogue,
			Research( kartTier: 2 ), instantAction: false );

		// UITEXT 30 with level 1 + 2, price nought; no level row although level 2 is researched.
		Assert.AreEqual( "AddOn 1502 0 0 750; AddOn 1501 0 0 900; Cancel -12999 30 3 0", Shape( rows ) );
	}

	[TestMethod]
	public void TheNextLevelWaitsForItsResearch()
	{
		var research = Research( kartTier: 1 );

		// Level 1 wants tier 2 researched; tier 1 is not enough.
		Assert.AreEqual( "AddOn 1502 0 0 750; AddOn 1501 0 0 900",
			Shape( ParkUpgradeList.For( Ride( Kart, level: 1 ), _catalogue, research, instantAction: false ) ) );

		// With it, UITEXT 29 with level 1 + 2 at CostOfUpgrade[2].
		Assert.AreEqual( "AddOn 1502 0 0 750; AddOn 1501 0 0 900; Level -2 29 3 2300",
			Shape( ParkUpgradeList.For( Ride( Kart, level: 1 ), _catalogue, Research( kartTier: 2 ), instantAction: false ) ) );

		// Tier 0: nothing at level 0.
		Assert.AreEqual( "AddOn 1502 0 0 750; AddOn 1501 0 0 900",
			Shape( ParkUpgradeList.For( Ride( Kart ), _catalogue, Research( kartTier: 0 ), instantAction: false ) ) );
	}

	[TestMethod]
	public void AnEmptyListSaysFullyUpgradedOnlyAtLevelTwo()
	{
		var research = Research( kartTier: 2 );

		// No add-on fits Spin. Level 2: UITEXT 28. Level 0 with everything researched has its level row.
		Assert.AreEqual( "Message -7890 28 0 0", Shape( ParkUpgradeList.For( Ride( Spin, level: 2 ), _catalogue, research, false ) ) );
		Assert.AreEqual( "Level -1 29 2 0", Shape( ParkUpgradeList.For( Ride( Spin ), _catalogue, research, false ) ) );

		// A save's byte of 3 is past level 2 but not level 2: no level row, and UITEXT 26, not 28.
		Assert.AreEqual( "Message -7890 26 0 0", Shape( ParkUpgradeList.For( Ride( Spin, level: 3 ), _catalogue, research, false ) ) );
	}

	[TestMethod]
	public void ALevelIsOrderedOnlyBelowTheBalanceAndAnAddOnAtItsBalance()
	{
		var research = Research( kartTier: 2 );
		var kart = Ride( Kart );

		// CostOfUpgrade[1] is 1500: strictly below the balance.
		Assert.AreEqual( ParkUpgradeList.Outcome.OrderRefused, ParkUpgradeList.Choose( -1, kart, _catalogue, research, 1500 ) );
		Assert.AreEqual( ParkUpgradeList.Outcome.Order, ParkUpgradeList.Choose( -1, kart, _catalogue, research, 1501 ) );

		// The jump's record price is 750 (its file's is 700): at most the balance.
		Assert.AreEqual( ParkUpgradeList.Outcome.Carry, ParkUpgradeList.Choose( Jump, kart, _catalogue, research, 750 ) );
		Assert.AreEqual( ParkUpgradeList.Outcome.TooDear, ParkUpgradeList.Choose( Jump, kart, _catalogue, research, 749 ) );

		Assert.AreEqual( ParkUpgradeList.Outcome.Cancel, ParkUpgradeList.Choose( ParkUpgradeList.CancelValue, kart, _catalogue, research, 0 ) );
		Assert.AreEqual( ParkUpgradeList.Outcome.Nothing, ParkUpgradeList.Choose( ParkUpgradeList.MessageValue, kart, _catalogue, research, 99999 ) );
	}

	/// <summary>
	/// Lost Kingdom's Dino Karts, with its two add-ons and its first upgrade researched as in the save Q1041 measured, where
	/// the original's list read the Jump at $750, the Tunnel at $900 and the upgrade to level 2 at $1500
	/// (<c>docs/exe/park-engine.md</c>, "An add-on has no window and no sale of its own"). The record prices here are the
	/// items' own, as a park with no record of them seeds them.
	/// </summary>
	[TestMethod]
	public void LostKingdomsDinoKartsListsItsJumpAndTunnelThenLevelTwo()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var addOns = catalogue.All.Where( item => item.AddOnUpgradesId == Kart ).ToArray();
		var research = new ParkResearch(
			[.. addOns.Select( item => new ParkWorld.ObjectControl( item.Id, true, 0, Price: item.BuildPrice ) ),
				new ParkWorld.ObjectControl( Kart, true, 1, Price: 0 )], catalogue );

		var rows = ParkUpgradeList.For( Ride( Kart ), catalogue, research, instantAction: false );

		Assert.AreEqual( "AddOn 0 750; AddOn 0 900; Level 2 1500",
			string.Join( "; ", rows.Select( row => $"{row.Kind} {row.Shown} {row.Price}" ) ), Shape( rows ) );
	}
}
