using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// A font slot is bounded by the set it is looked up in. The shipped sets are thirteen apiece, as the original's are
/// (0x00485a70 answers none from slot 13 on), so only a made table whose sets differ in length shows which set the
/// bound is read from.
/// </summary>
[TestClass]
public class UiFontsTests
{
	[TestMethod]
	public void ASlotIsBoundedByTheSetItIsLookedUpIn()
	{
		string[][] sets = [["A0", "A1", "A2"], ["B0"]];

		Assert.AreEqual( "A2", UiFonts.FileName( sets, 0, 2 ), "the first set's last" );
		Assert.AreEqual( "B0", UiFonts.FileName( sets, 1, 0 ), "the second set's only" );
		Assert.IsNull( UiFonts.FileName( sets, 1, 2 ), "past the second set's end" );
		Assert.IsNull( UiFonts.FileName( sets, 0, 3 ), "past the first set's end" );
		Assert.IsNull( UiFonts.FileName( sets, 1, -1 ), "a control with no font" );
	}
}
