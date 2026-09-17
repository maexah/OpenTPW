using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// Which way a person faces after a step - <see cref="PeepHeading"/>, the original's <c>FUN_006e7074</c>.
///
/// <para>
/// <b>The expected headings below were derived from the decoded branch structure, not invented and not
/// read off a running game.</b> Each cardinal falls out of which of the eight cases the two components
/// select and which quarter turn that case anchors on; they are asserted here so that a rearrangement of
/// those cases cannot quietly mirror or rotate the whole park.
/// </para>
/// </summary>
[TestClass]
public class PeepHeadingTests
{
	/// <summary>A step of one whole cell, which is far more than anyone walks in a tick but divides cleanly.</summary>
	private const int Step = FixedVector.One;

	/// <summary>
	/// What the walk passes: the across component <b>negated</b>, the down component as it stands. The
	/// original builds its arguments that way at <c>004fa348</c>-<c>004fa34a</c> and the two are not
	/// symmetric, so this helper exists to keep every test below honest about the convention.
	/// </summary>
	private static int After( int dx, int dy, int wasFacing = 0 )
		=> PeepHeading.Of( -dx, dy, wasFacing );

	/// <summary>
	/// The table is the executable's, copied out of <c>0x006e71b4</c>. Endpoints and four sampled entries.
	/// </summary>
	[TestMethod]
	public void TheTableIsTheOneInTheExecutable()
	{
		Assert.AreEqual( 0, PeepHeading.Arctan( 0 ), "atan(0) is no turn at all" );
		Assert.AreEqual( 256, PeepHeading.Arctan( 256 ), "atan(1) is an eighth of a turn, which is 256" );

		// Sampled from the executable's own words, so a wholesale mistranscription cannot pass.
		Assert.AreEqual( 79, PeepHeading.Arctan( 64 ) );
		Assert.AreEqual( 151, PeepHeading.Arctan( 128 ) );
		Assert.AreEqual( 209, PeepHeading.Arctan( 192 ) );
		Assert.AreEqual( 255, PeepHeading.Arctan( 255 ) );

		for ( var i = 1; i <= PeepHeading.Steps; ++i )
		{
			Assert.IsTrue( PeepHeading.Arctan( i ) >= PeepHeading.Arctan( i - 1 ),
				$"the curve never turns back on itself, and it does not at {i}" );
		}
	}

	/// <summary>
	/// <b>This is the test that catches a typo in 257 hand-copied numbers</b>, and that is the only reason
	/// it exists.
	///
	/// <para>
	/// The table is <c>trunc( atan( i / 256 ) * 2048 / 2pi )</c> on every one of its entries - checked
	/// against all 257 words in the executable, where rounding instead of truncating matches only 135. So
	/// the curve is a sound check on the transcription, even though it is deliberately <i>not</i> what the
	/// code uses: see <see cref="PeepHeading"/> for why a table cannot drift and a <c>Math.Atan</c> can.
	/// </para>
	/// <para>
	/// <b>The last entry is checked separately and that is the whole point of the split.</b> At
	/// <c>i = 256</c> the true value is exactly 256, so a runtime whose <c>Math.Atan</c> returns the nearest
	/// double below <c>pi/4</c> truncates to 255 - which would fail here while the table is perfectly
	/// correct. Comparing the curve over 0..255 and pinning 256 by hand keeps this test from failing for a
	/// reason that has nothing to do with the table.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheTableAgreesWithTheCurveItWasTruncatedFrom()
	{
		for ( var i = 0; i < PeepHeading.Steps; ++i )
		{
			var curve = (int)(Math.Atan( i / (double)PeepHeading.Steps )
				* PeepHeading.FullTurn / (2.0 * Math.PI));

			Assert.AreEqual( curve, PeepHeading.Arctan( i ), $"table entry {i}" );
		}

		Assert.AreEqual( 256, PeepHeading.Arctan( PeepHeading.Steps ),
			"and the last, which the curve cannot be trusted to reproduce on every runtime" );
	}

	/// <summary>
	/// The four cardinal directions are a quarter turn apart, and which one is which is the thing that would
	/// mirror the whole park if it were wrong.
	/// </summary>
	[TestMethod]
	public void TheFourCardinalsAreQuarterTurnsApart()
	{
		Assert.AreEqual( 0, After( 0, -Step ), "towards lower y" );
		Assert.AreEqual( 0x200, After( -Step, 0 ), "towards lower x" );
		Assert.AreEqual( 0x400, After( 0, Step ), "towards higher y" );
		Assert.AreEqual( 0x600, After( Step, 0 ), "towards higher x" );

		Assert.AreEqual( PeepHeading.QuarterTurn, 0x200, "which is what a quarter of 0x800 is" );
	}

	/// <summary>
	/// A guest saved facing <c>0x200</c> reads as octant 6, which is the one heading in the shipped park
	/// that is not zero - so this ties the arithmetic above to a number that came out of the game's own file
	/// rather than out of this test.
	/// </summary>
	[TestMethod]
	public void TheOneNonZeroHeadingInTheShippedParkFoldsToItsRecordedOctant()
	{
		Assert.AreEqual( 6, ParkWorld.Person.OctantOf( 0x200 ),
			"thing 33 is saved at angle 0x200 and the file separately records facing 6" );

		Assert.AreEqual( 0, ParkWorld.Person.OctantOf( 0x400 ), "and the quarter turn from it is octant 0" );
	}

	/// <summary>Halfway between two cardinals, which is where the table's last entry does the work.</summary>
	[TestMethod]
	public void ADiagonalSitsMidwayBetweenItsTwoCardinals()
	{
		// Equal parts higher-x and higher-y, so the ratio is exactly one and the table is read at its end.
		var diagonal = After( Step, Step );

		Assert.AreEqual( 0x500, diagonal );

		Assert.IsTrue( diagonal > After( 0, Step ) && diagonal < After( Step, 0 ),
			"between the two cardinals it is made of" );
	}

	/// <summary>
	/// <b>Standing still keeps the heading you had.</b> The original answers with whatever was in the
	/// register, which is not an answer; a person who has not moved has not turned, and the caller's own
	/// heading is what is given back.
	/// </summary>
	[TestMethod]
	public void StandingStillKeepsTheHeadingYouHad()
	{
		Assert.AreEqual( 0x123, PeepHeading.Of( 0, 0, 0x123 ) );
		Assert.AreEqual( 0, PeepHeading.Of( 0, 0, 0 ) );
	}

	/// <summary>
	/// Every answer is inside one turn. The <c>0x800 - table[0]</c> branch is the one that would otherwise
	/// hand back a whole turn where it means none at all.
	/// </summary>
	[TestMethod]
	public void EveryAnswerIsInsideOneTurn()
	{
		for ( var dx = -3; dx <= 3; ++dx )
		{
			for ( var dy = -3; dy <= 3; ++dy )
			{
				var heading = After( dx * Step, dy * Step );

				Assert.IsTrue( heading >= 0 && heading < PeepHeading.FullTurn,
					$"({dx},{dy}) gave {heading}" );
			}
		}
	}
}
