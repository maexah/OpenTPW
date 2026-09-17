using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// How loud a park's crowd makes its music - the original's <c>FUN_004c81e0</c>, which is
/// <c>clamp( guests / 2, 0, 100 )</c>.
///
/// <para>
/// No game files and no device: this is arithmetic, and the point of pinning it is that the shipped park's
/// answer is <b>six</b>. A park that sounded right would mean this was not being asked.
/// </para>
/// </summary>
[TestClass]
public class ParkCrowdVolumeTests
{
	/// <summary>
	/// The curve, at the points where getting it wrong would show: empty, the shipped park, and both ends
	/// of the clamp.
	///
	/// <para>
	/// <b>Thirteen guests give six, not seven</b> - the original divides integers and truncates, so the odd
	/// guest is lost rather than rounded up. A float divide would give 6.5 and, rounded, 7, which is the
	/// kind of difference that never looks wrong and never matches either.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheCrowdLevelIsHalfTheGuestsHeldBetweenNoughtAndAHundred()
	{
		Assert.AreEqual( 0, ParkAudio.CrowdLevel( 0 ), "an empty park is silent, which is the original's design" );
		Assert.AreEqual( 0, ParkAudio.CrowdLevel( 1 ), "one guest still truncates to nothing" );
		Assert.AreEqual( 6, ParkAudio.CrowdLevel( 13 ), "the thirteen guests Lost Kingdom ships with" );
		Assert.AreEqual( 9, ParkAudio.CrowdLevel( 18 ), "and eighteen would be the answer if STAFF counted" );
		Assert.AreEqual( 100, ParkAudio.CrowdLevel( 200 ), "two hundred guests reach the top" );
		Assert.AreEqual( 100, ParkAudio.CrowdLevel( 5000 ), "and nothing goes past it" );
	}

	/// <summary>
	/// The count can only ever be guests, and this is the anti-vacuity guard for the row above: thirteen and
	/// eighteen have to give <i>different</i> answers, or a reader wired to the park's eighteen people
	/// instead of its thirteen guests would pass every assertion here.
	/// </summary>
	[TestMethod]
	public void CountingPeopleRatherThanGuestsWouldBeAudiblyDifferent()
	{
		Assert.AreNotEqual( ParkAudio.CrowdLevel( 13 ), ParkAudio.CrowdLevel( 18 ),
			"guests and people must not give the same level, or nothing here discriminates" );

		// Half as loud again, which is what reading FUN_004c7fa0's model test as "any person" would cost.
		Assert.AreEqual( 3, ParkAudio.CrowdLevel( 18 ) - ParkAudio.CrowdLevel( 13 ) );
	}

	/// <summary>A negative count cannot arise, and is held at nought rather than wrapping if it ever did.</summary>
	[TestMethod]
	public void ANegativeCrowdIsHeldAtNought()
		=> Assert.AreEqual( 0, ParkAudio.CrowdLevel( -4 ) );
}
