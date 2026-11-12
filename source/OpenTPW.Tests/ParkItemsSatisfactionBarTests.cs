using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// <b>The all-items satisfaction bar is not masked to a byte</b> (Q457). The fill and the rewrite hand the bar
/// <c>FUN_004e1e30</c>'s value through the shift alone, with no <c>and 0xff</c> (<c>0x00494031</c>, <c>0x00494199</c>,
/// <c>0x00495888</c>, <c>0x00495959</c>), and the bar's setter <c>FUN_0066d885</c> holds it to 0..0x400
/// (<c>docs/exe/hud.md</c>, "The all-items rows: a number, then bars"): -1 is an empty bar, 256 a full one. The masked
/// rule makes -1 a full bar (255) and 256 an empty one (0).
/// </summary>
[TestClass]
public class ParkItemsSatisfactionBarTests
{
	/// <summary>
	/// <b>The rule and the setter</b>: -1 is -10 units and an empty bar, 256 is 2621 units and a full one, 50 is 512.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>&amp; 0xff</c> put back (2611 and 0); the setter's clamp dropped.</remarks>
	[TestMethod]
	public void TheSatisfactionIsShiftedUnmaskedAndTheBarHoldsItToOneFullBar()
	{
		Assert.AreEqual( 512, ParkItemsScreen.SatisfactionBar( 50 ) );
		Assert.AreEqual( -10, ParkItemsScreen.SatisfactionBar( -1 ), "below nought stays below nought" );
		Assert.AreEqual( 2621, ParkItemsScreen.SatisfactionBar( 256 ), "past 255 is not its low byte" );

		var bar = new ParkObjectWindow.UiStatBar { Units = ParkItemsScreen.SatisfactionBar( -1 ) };
		Assert.AreEqual( 0f, bar.At, "-1 is an empty bar" );

		bar.Units = ParkItemsScreen.SatisfactionBar( 256 );
		Assert.AreEqual( 1f, bar.At, "256 is a full bar" );
	}

	/// <summary>
	/// <b>The row's satisfaction column takes that rule</b>: the Belly Bounce in <c>Easymode.TPWI</c> with thirty finished days
	/// of satisfaction -51 (fifty plus -51 is -1) gets -10 in the shops' tab's last column, and with thirty of 206 (256) gets
	/// 2621. Skipped without the game (<see cref="GameData"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>NumbersOf</c> back on <c>Bar</c> (2611 and 0).</remarks>
	[TestMethod]
	public void TheItemsRowsSatisfactionColumnIsUnmasked()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var state = new ParkState( world );

		Assert.IsTrue( state.TryObject( 13, out var ride ), "the Belly Bounce, thing 13" );
		Assert.IsTrue( catalogue.TryGet( ride.CatalogueId, out var item ) );

		int SatisfactionAfterAMonthOf( int day )
		{
			var rings = state.RingsFor( 13 );

			for ( var each = 0; each < 30; ++each )
			{
				rings.Satisfaction.Today = day;
				rings.Roll();
			}

			return ParkItemsScreen.NumbersOf( 1, ride, item, state )[^1];
		}

		Assert.AreEqual( -10, SatisfactionAfterAMonthOf( -51 ), "fifty less 51 is -1" );
		Assert.AreEqual( 2621, SatisfactionAfterAMonthOf( 206 ), "fifty plus 206 is 256" );
	}
}
