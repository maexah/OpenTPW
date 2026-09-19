using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// Vector2, Vector4, Color and Rotation compare by value, through Equals and through ==. Rotation's == allows a
/// tolerance, where its Equals is exact.
/// </summary>
[TestClass]
public class EqualityTests
{
	[TestMethod]
	public void Vector2ComparesByValue()
	{
		Assert.IsTrue( new Vector2( 1, 2 ).Equals( new Vector2( 1, 2 ) ) );
		Assert.IsFalse( new Vector2( 1, 2 ).Equals( new Vector2( 2, 1 ) ) );
		Assert.IsTrue( new Vector2( 1, 2 ) == new Vector2( 1, 2 ) );
		Assert.IsTrue( new Vector2( 1, 2 ) != new Vector2( 1, 3 ) );
	}

	[TestMethod]
	public void Vector4ComparesByValue()
	{
		Assert.IsTrue( new Vector4( 1, 2, 3, 4 ).Equals( new Vector4( 1, 2, 3, 4 ) ) );
		Assert.IsFalse( new Vector4( 1, 2, 3, 4 ).Equals( new Vector4( 1, 2, 3, 5 ) ) );
		Assert.IsTrue( new Vector4( 1, 2, 3, 4 ) == new Vector4( 1, 2, 3, 4 ) );
		Assert.IsTrue( new Vector4( 1, 2, 3, 4 ) != new Vector4( 4, 3, 2, 1 ) );
	}

	[TestMethod]
	public void ColorComparesByValue()
	{
		Assert.IsTrue( new Color( 0.1f, 0.2f, 0.3f, 1f ).Equals( new Color( 0.1f, 0.2f, 0.3f, 1f ) ) );
		Assert.IsFalse( new Color( 0.1f, 0.2f, 0.3f, 1f ).Equals( new Color( 0.1f, 0.2f, 0.3f, 0.5f ) ) );
		Assert.IsTrue( new Color( 0.1f, 0.2f, 0.3f, 1f ) == new Color( 0.1f, 0.2f, 0.3f, 1f ) );
		Assert.IsTrue( new Color( 0.1f, 0.2f, 0.3f, 1f ) != new Color( 1f, 0.2f, 0.3f, 1f ) );
	}

	/// <summary>
	/// Two rotations that are the same are ==, and a rotation is at no angle from itself. The quarter turn is
	/// the case that needs a tolerance: its dot with an identical one rounds to just under 1. The tolerance is
	/// pinned from both sides - a tenth of a degree apart is ==, two tenths is not - and q against -q, one
	/// rotation with a negative dot, is != and yet 0 degrees apart.
	/// </summary>
	[TestMethod]
	public void RotationComparesByValue()
	{
		var quarterTurn = new Rotation( 0f, 0.70710677f, 0f, 0.70710677f );
		var theSameQuarterTurn = new Rotation( 0f, 0.70710677f, 0f, 0.70710677f );

		Assert.IsTrue( Rotation.Identity == Rotation.Identity );
		Assert.IsTrue( new Rotation( 0f, 0f, 0f, 1f ) == Rotation.Identity );
		Assert.IsTrue( quarterTurn == theSameQuarterTurn );
		Assert.IsTrue( quarterTurn.Equals( theSameQuarterTurn ) );
		Assert.IsTrue( quarterTurn != Rotation.Identity );
		Assert.AreEqual( 0f, Rotation.Angle( Rotation.Identity, Rotation.Identity ) );
		Assert.AreEqual( 0f, Rotation.Angle( quarterTurn, theSameQuarterTurn ) );
		Assert.AreEqual( 90f, Rotation.Angle( quarterTurn, Rotation.Identity ), 0.01f );

		Assert.IsTrue( quarterTurn == AboutY( 90.1f ), "a tenth of a degree apart is the same rotation" );
		Assert.IsTrue( quarterTurn != AboutY( 90.2f ), "two tenths is not" );

		Assert.IsTrue( quarterTurn != -quarterTurn, "== takes the dot as it is signed" );
		Assert.AreEqual( 0f, Rotation.Angle( quarterTurn, -quarterTurn ), "Angle takes its size" );
	}

	/// <summary>A turn of <paramref name="degrees"/> about the vertical, built from its half angle.</summary>
	private static Rotation AboutY( float degrees )
		=> new( 0f, MathF.Sin( degrees * MathF.PI / 360f ), 0f, MathF.Cos( degrees * MathF.PI / 360f ) );
}
