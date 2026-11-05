using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The buy list's features tab (<c>docs/exe/hud.md</c>, "The buy screen"): the researched features, and after them the two land
/// tools the original appends with the ids -1 and -2, which no tab but this one carries.
/// </summary>
[TestClass]
public class ParkBuyFeaturesTabTests
{
	private Level? _level;
	private Point2 _screen;

	[TestInitialize]
	public void Keep()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		Log ??= new();
		FileSystem = GameData.Required();
		typeof( ParkBuyScreen ).GetField( "_lastTab", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, 0 );
	}

	[TestCleanup]
	public void PutBack()
	{
		ParkHand.LetGo();
		Level.Current = _level!;
		Screen.Size = _screen;
	}

	private static UiList ListOf( ParkBuyScreen screen )
		=> (UiList)typeof( ParkBuyScreen ).GetField( "_list", BindingFlags.NonPublic | BindingFlags.Instance )!.GetValue( screen )!;

	/// <summary>
	/// The features tab lists every researched feature of the catalogue and the two land rows, Buy Land (-1) and Clear Land (-2),
	/// each lettered "$ 0"; the rides, shops and sideshows tabs carry neither land row.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the land rows added to every tab; either land row left off; the land rows' ids swapped; the tab's kind
	/// taken as the shops'; the unresearched features listed.
	/// </remarks>
	[TestMethod]
	public void TheFeaturesTabCarriesTheTwoLandRowsAfterItsResearchedFeatures()
	{
		var (catalogue, screen, _) = ParkFootprintPictureTests.BuyScreen();
		var research = Level.Current!.Research!;
		var list = ListOf( screen );

		var features = catalogue.All.Where( item => item.UiType == ItemDescriptionFile.Feature && research.IsResearched( item.Id ) )
			.Select( item => item.Id ).ToHashSet();

		Assert.IsTrue( catalogue.All.Any( item => item.UiType == ItemDescriptionFile.Feature && !research.IsResearched( item.Id ) ),
			"the park has a feature not yet researched, which the tab leaves off" );

		Assert.AreNotEqual( 0, features.Count, "the park is saved with features researched" );

		screen.PickTab( ItemDescriptionFile.Feature );

		var ids = list.Rows.Select( row => row.Id ).ToList();

		CollectionAssert.AreEquivalent( features.Concat( [-1, -2] ).ToList(), ids );

		var land = list.Rows.Where( row => row.Id < 0 ).ToDictionary( row => row.Id );

		Assert.AreEqual( Localization.Get( UIStrings.BuyLand ), land[-1].Name );
		Assert.AreEqual( Localization.Get( UIStrings.ClearLand ), land[-2].Name );
		Assert.IsTrue( land.Values.All( row => row.Value == 0 && row.Values![0] == ParkBuyScreen.PriceText( 0 ) ) );

		foreach ( var tab in new[] { ItemDescriptionFile.Ride, 1, ItemDescriptionFile.SideShow } )
		{
			screen.PickTab( tab );

			Assert.IsFalse( list.Rows.Any( row => row.Id < 0 ), $"tab {tab}: no land row" );
		}
	}
}
