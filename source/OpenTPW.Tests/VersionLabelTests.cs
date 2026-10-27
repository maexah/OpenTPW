using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>The lobby's build label (Q752): the original's "v 2.0" box (<c>FUN_0048b020</c>), lettered with this build's name.</summary>
[TestClass]
public class VersionLabelTests
{
	[TestMethod]
	public void TheLabelNamesTheBuildAlone()
	{
		Assert.AreEqual( "weekly-2026-11-01", LaunchReport.BuildName( "weekly-2026-11-01+52ac9c2c35e5eed437f16fadd2a758853d9fbbb1" ) );
		Assert.AreEqual( "local", LaunchReport.BuildName( "local+9d4484a117393d1d50b3bd4689585ddc3656bdea" ) );
		Assert.AreEqual( "weekly-2026-11-01", LaunchReport.BuildName( "weekly-2026-11-01" ) );
		Assert.AreEqual( "unknown", LaunchReport.BuildName( null ) );
		Assert.AreEqual( "unknown", LaunchReport.BuildName( " " ) );
	}

	[TestMethod]
	public void TheBoxStandsBackFromTheRightEdgeAndTheBottomCutTowardNought()
	{
		// 46 x 2048 / 800 = 117.76 and 14 x 1536 / 600 = 35.84: cut, not rounded.
		Assert.AreEqual( new UiRect( 1984 - 117, 1472 - 35, 1984, 1472 ), VersionLabel.Box( 46, 14, 600 ) );

		// 46 x 2048 / 640 = 147.2 and 14 x 1536 / 480 = 44.8: each set against its own screen.
		Assert.AreEqual( new UiRect( 1984 - 147, 1472 - 44, 1984, 1472 ), VersionLabel.Box( 46, 14, 480 ) );
	}

	[TestMethod]
	public void TheLabelIsThePlainWhiteLabelInFontSix()
	{
		var control = new VersionLabel( "local" ).Control;

		Assert.AreEqual( 0x8cfd55, control.Id );
		Assert.AreEqual( "local", control.Text );
		Assert.AreEqual( 6, control.Font );
		Assert.AreEqual( UiColour.White, control.TextColour );
		Assert.IsFalse( control.TextShadow );
		Assert.AreEqual( TextAlign.Start, control.TextAcross );
		Assert.AreEqual( TextAlign.Centre, control.TextDown );
		Assert.IsFalse( control.TakesMouse, "the label takes no pointer: a press on it is a press on the lobby's view" );
	}

	/// <summary>
	/// The original's own "v 2.0" in GAME8.bf4, font 6 of the 800 x 600 set, measured into the box the original made for
	/// it at 800 x 600, read from its memory (<c>uitree.py PID 0x00f82884+4</c>).
	/// </summary>
	[TestMethod]
	public void TheOriginalsVersionMeasuresToTheOriginalsBox()
	{
		FileSystem = GameData.Required();
		var font = BitmapFont.Get( "GAME8.bf4" ) ?? throw new AssertFailedException( "GAME8.bf4 did not load" );

		// The original's control 0x8cfd55 at 800 x 600, read 2026-10-27 (Q752): ( 1926, 1444, 1984, 1472 ).
		Assert.AreEqual( new UiRect( 1926, 1444, 1984, 1472 ), VersionLabel.Box( font.Measure( "v 2.0" ), font.LineHeight, 600 ) );
	}
}
