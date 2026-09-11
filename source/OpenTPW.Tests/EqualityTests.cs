using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>Vector2, Vector4 and Color compare by value, through Equals and through ==.</summary>
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
}
