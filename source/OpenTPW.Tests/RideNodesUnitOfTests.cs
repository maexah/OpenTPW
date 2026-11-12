using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// A node's direction over its length, <see cref="RideNodes.UnitOf"/>, worked as <c>FUN_00556b90</c> works it
/// (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a particle with a direction"). Needs no game.
/// </summary>
/// <remarks><b>Mutations:</b> the root kept as a double; the third square summed first; the sum taken in floats.</remarks>
[TestClass]
public class RideNodesUnitOfTests
{
	private static string Bits( System.Numerics.Vector3 v ) =>
		$"{BitConverter.SingleToInt32Bits( v.X ):X8} {BitConverter.SingleToInt32Bits( v.Y ):X8} {BitConverter.SingleToInt32Bits( v.Z ):X8}";

	/// <summary>
	/// <b>Each element is divided by the root kept as a float</b>: divided by the double root, y is one bit lower
	/// (<c>3F61AB7E</c>).
	/// </summary>
	[TestMethod]
	public void EachElementIsDividedByTheRootKeptAsAFloat()
	{
		Assert.AreEqual( "BE8DF860 3F61AB7F 3EC3A7A9", Bits( RideNodes.UnitOf( -0.23355906f, 0.7425114f, 0.32187724f ) ) );
	}

	/// <summary>
	/// <b>The first two squares are summed, then the third</b>: x and y here are a Pythagorean pair over 2^24, so
	/// <c>x*x + y*y</c> is a square whose root lies on a tie between two floats, and the tiny z decides it only
	/// when its square is added first (<c>3CBD7780 3F7FEE77 328FF637</c>, as a sum taken in floats gives too).
	/// </summary>
	[TestMethod]
	public void TheFirstTwoSquaresAreSummedThenTheThird()
	{
		Assert.AreEqual( "3CBD7781 3F7FEE79 328FF638", Bits( RideNodes.UnitOf( 0.02313441f, 0.99999785f, 1.6763806E-08f ) ) );
	}

	/// <summary><b>A node with no matrix points along z</b>, and the unit z is its own unit.</summary>
	[TestMethod]
	public void TheUnitZIsItsOwnUnit()
	{
		Assert.AreEqual( System.Numerics.Vector3.UnitZ, RideNodes.UnitOf( 0f, 0f, 1f ) );
	}
}
