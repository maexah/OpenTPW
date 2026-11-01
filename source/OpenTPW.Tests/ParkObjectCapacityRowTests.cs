using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The object window's capacity readout picks its UITEXT row by the item's <c>Bumper.WhichTrackType</c> as
/// <c>FUN_004aec30</c> does (0x004aecf0, 0x004aed5b): 3 is row 463 "per train", 2 is 461 "per car", else 427.
/// </summary>
[TestClass]
public class ParkObjectCapacityRowTests
{
	[TestMethod]
	[DataRow( 3, 463 )]
	[DataRow( 2, 461 )]
	[DataRow( 1, 427 )]
	[DataRow( 0, 427 )]
	[DataRow( 4, 427 )]
	public void TheRowFollowsTheTrackType( int trackType, int row )
		=> Assert.AreEqual( row, UI.ParkObjectWindow.CapacityRow( trackType ) );
}
