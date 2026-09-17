using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The fixed-point arithmetic the peep simulation is written in.
///
/// <para>
/// The lengths below are all exact rather than approximate, which is deliberate: each is a 3-4-5 triangle
/// scaled into one of the three precision ranges, so a wrong shift shows up as a wrong number and not as
/// a rounding difference that a tolerance would swallow.
/// </para>
/// </summary>
[TestClass]
public class FixedVectorTests
{
	/// <summary>A whole cell is the unit everything here is expressed in.</summary>
	[TestMethod]
	public void OneIsAWholeCell()
	{
		Assert.AreEqual( 65536, FixedVector.One );
	}

	[TestMethod]
	public void AddingSubtractingAndNegating()
	{
		var a = new FixedVector( 0x30000, 0x10000 );
		var b = new FixedVector( 0x10000, 0x40000 );

		Assert.AreEqual( new FixedVector( 0x40000, 0x50000 ), a + b );
		Assert.AreEqual( new FixedVector( 0x20000, -0x30000 ), a - b, "this minus the other, in that order" );
		Assert.AreEqual( new FixedVector( -0x30000, -0x10000 ), -a );
	}

	/// <summary>Multiplying by a fraction, with the whole product formed before it is shifted back down.</summary>
	[TestMethod]
	public void ScalingMultipliesByAFraction()
	{
		var v = new FixedVector( 0x10000, 0x20000 );

		Assert.AreEqual( new FixedVector( 0x8000, 0x10000 ), v.ScaledBy( 0x8000 ), "half" );
		Assert.AreEqual( v, v.ScaledBy( FixedVector.One ), "one is an identity" );
		Assert.AreEqual( FixedVector.Zero, v.ScaledBy( 0 ) );
	}

	/// <summary>
	/// <b>Dividing by one is an identity</b>, and it is worth pinning on its own: the steering step divides
	/// by exactly that every tick, so the step that reads like "divide by the mass" divides by nothing.
	/// </summary>
	[TestMethod]
	public void DividingByOneIsAnIdentity()
	{
		var v = new FixedVector( 0x12345, -0x6789 );

		Assert.AreEqual( v, v.DividedBy( FixedVector.One ) );
		Assert.AreEqual( new FixedVector( 0x10000, 0x20000 ),
			new FixedVector( 0x8000, 0x10000 ).DividedBy( 0x8000 ), "and halving undoes a doubling" );
	}

	/// <summary>The shortest range, where the squares are taken as they stand.</summary>
	[TestMethod]
	public void LengthInTheNearestRange()
	{
		// 2048, 1536 - a 3-4-5 triangle, so the answer is exactly 2560.
		Assert.AreEqual( 0xa00, new FixedVector( 0x800, 0x600 ).Length );
		Assert.AreEqual( 0xa00, new FixedVector( -0x800, -0x600 ).Length, "sign makes no difference" );
	}

	/// <summary>The middle range, measured shifted down by eight and shifted back.</summary>
	[TestMethod]
	public void LengthInTheMiddleRange()
	{
		Assert.AreEqual( 0xa0000, new FixedVector( 0x80000, 0x60000 ).Length );
	}

	/// <summary>The widest range, shifted down by eighteen - the one that loses precision.</summary>
	[TestMethod]
	public void LengthInTheWidestRange()
	{
		Assert.AreEqual( 0x2800000, new FixedVector( 0x2000000, 0x1800000 ).Length );
	}

	/// <summary>Nothing at all has no length, and a whole cell along one axis is a whole cell.</summary>
	[TestMethod]
	public void TheEasyLengths()
	{
		Assert.AreEqual( 0, FixedVector.Zero.Length );
		Assert.AreEqual( FixedVector.One, new FixedVector( FixedVector.One, 0 ).Length );
		Assert.AreEqual( FixedVector.One, new FixedVector( 0, -FixedVector.One ).Length );
	}

	/// <summary>
	/// The eight-sided measure: both parts added, less half the smaller. It is not the true length and is
	/// not meant to be - here it says 2816 where the real answer is 2560.
	/// </summary>
	[TestMethod]
	public void TheOctagonalMeasureIsNotTheTrueLength()
	{
		var v = new FixedVector( 0x800, 0x600 );

		Assert.AreEqual( 0xb00, v.OctagonalLength );
		Assert.AreEqual( 0xa00, v.Length, "which the true length is not" );

		// Along an axis the two agree exactly, which is what makes it usable at all.
		Assert.AreEqual( 0x800, new FixedVector( 0x800, 0 ).OctagonalLength );
		Assert.AreEqual( 0x800, new FixedVector( 0x800, 0 ).Length );
	}

	/// <summary>Something longer than the limit comes back at exactly the limit.</summary>
	[TestMethod]
	public void ClampingShortensToTheLimit()
	{
		var clamped = new FixedVector( 0x800, 0x600 ).ClampedTo( 0x500 );

		Assert.AreEqual( new FixedVector( 0x400, 0x300 ), clamped );
		Assert.AreEqual( 0x500, clamped.Length, "which is the limit exactly" );
	}

	/// <summary>
	/// Something shorter is handed straight back - and something exactly at the limit still goes through
	/// the arithmetic, because the original asks "shorter than" and not "no longer than". Either way the
	/// value is the same, which is why this is pinned by value rather than by identity.
	/// </summary>
	[TestMethod]
	public void ClampingLeavesAnythingNotLongerAlone()
	{
		var v = new FixedVector( 0x800, 0x600 );

		Assert.AreEqual( v, v.ClampedTo( 0xb00 ), "shorter than the limit" );
		Assert.AreEqual( v, v.ClampedTo( 0xa00 ), "exactly at it" );
	}

	/// <summary>Which cell a position falls in, and the corner of a cell, being exact inverses.</summary>
	[TestMethod]
	public void CellsAndPositionsConvertBothWays()
	{
		Assert.AreEqual( (31, 2), new FixedVector( 0x1f0000, 0x20000 ).Cell );
		Assert.AreEqual( (31, 2), new FixedVector( 0x1fffff, 0x2ffff ).Cell, "the fraction is dropped" );

		Assert.AreEqual( new FixedVector( 0x1f0000, 0x20000 ), FixedVector.AtCell( 31, 2 ) );
		Assert.AreEqual( (47, 17), FixedVector.AtCell( 47, 17 ).Cell, "and back again" );
	}

	/// <summary>
	/// <b>The root takes its argument unsigned.</b> The original zeroes the high half of a stack qword
	/// before loading it, so a value with the top bit set is a large positive number and not a negative
	/// one - read as signed this would be the square root of minus one.
	/// </summary>
	[TestMethod]
	public void TheRootReadsItsArgumentUnsigned()
	{
		Assert.AreEqual( 65535, FixedVector.Root( 0xffffffffu ) );
		Assert.AreEqual( 256, FixedVector.Root( 65536u ) );
		Assert.AreEqual( 0, FixedVector.Root( 0u ) );
		Assert.AreEqual( 3, FixedVector.Root( 15u ), "the whole part, truncated" );
	}
}
