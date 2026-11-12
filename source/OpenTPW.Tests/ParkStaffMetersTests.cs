using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The all-staff screen's two meters, as the original averages them: each member's happiness truncated and cut to its low
/// byte, the kind's meter an integer mean then <c>(m &lt;&lt; 10) / 100</c> (<c>FUN_004c9cc0</c>), the all-staff meter a
/// float mean times 1024 times <c>0.01f</c> (<c>FUN_004c7cc0</c>, <c>0x00496a7b</c>). See <c>docs/exe/hud.md</c>, "The
/// all-staff rows: a money cell, then bars". No game is needed.
/// </summary>
[TestClass]
public class ParkStaffMetersTests
{
	/// <summary>Seven members at 87.45 and one at 88.45: the raw mean 87.575, the bytes' mean 697 / 8 = 87.125.</summary>
	private static readonly float[] Eight = [.. Enumerable.Repeat( 87.45f, 7 ), 88.45f];

	[TestMethod]
	public void TheAllStaffMeterIsTheFloatMeanOfTheTruncatedHappiness()
	{
		// The raw mean scaled reads 87.575 x 1024 / 100 = 896; the original's 87.125 x 1024 x 0.01f = 892.16.
		Assert.AreEqual( 892, ParkStaffScreen.AllStaffMeter( Eight ) );
	}

	[TestMethod]
	public void TheKindMeterIsTheIntegerMeanOfTheTruncatedHappiness()
	{
		// 697 / 8 = 87 in integers, (87 << 10) / 100 = 890: not the raw mean's 896, nor the float mean's 892.
		Assert.AreEqual( 890, ParkStaffScreen.KindMeter( Eight ) );
	}

	[TestMethod]
	public void TheAllStaffMeterScalesByTheFloatNearestAHundredth()
	{
		// 0.01f is 0.00999999977..., so a mean of exactly 50 reads 511.99998 truncated; the kind's integers read 512.
		float[] fifty = [50.5f, 50.9f];

		Assert.AreEqual( 511, ParkStaffScreen.AllStaffMeter( fifty ) );
		Assert.AreEqual( 512, ParkStaffScreen.KindMeter( fifty ) );
	}

	[TestMethod]
	public void BothMetersCountOnlyTheHappinesssLowByte()
	{
		// 300 truncated is 300, its low byte 44; with 44 beside it the mean is 44: 450 on either meter, not the raw mean's 1,765.
		float[] past = [300.7f, 44.2f];

		Assert.AreEqual( (44 << 10) / 100, ParkStaffScreen.KindMeter( past ) );
		Assert.AreEqual( 450, ParkStaffScreen.AllStaffMeter( past ) );
	}

	[TestMethod]
	public void AMeterOverNobodyReadsNought()
	{
		Assert.AreEqual( 0, ParkStaffScreen.KindMeter( [] ) );
		Assert.AreEqual( 0, ParkStaffScreen.AllStaffMeter( [] ) );
	}
}
