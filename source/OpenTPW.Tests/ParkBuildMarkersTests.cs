using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The queue tool's squares - <see cref="ParkBuildMarkers"/>. No game files needed.
/// </summary>
[TestClass]
public class ParkBuildMarkersTests
{
	/// <summary>
	/// The wave the queue tool's squares ride on: <c>sin( phase + x + z )</c> in world units, one unit either
	/// way - the 4,096-entry table <c>FUN_004708d0</c> fills, read at <c>(phase + x + z) * 4096 / 2π</c>.
	/// </summary>
	[TestMethod]
	public void TheQueueSquaresWaveOneUnitAlongTheDiagonal()
	{
		Assert.AreEqual( 0f, ParkBuildMarkers.Wave( 0f, 0f, 0f ), 1e-5f, "flat at the origin with no phase" );
		Assert.AreEqual( 1f, ParkBuildMarkers.Wave( System.MathF.PI / 2f, 0f, 0f ), 1e-5f, "one unit at the crest" );
		Assert.AreEqual( ParkBuildMarkers.Wave( 0f, 10f, 20f ), ParkBuildMarkers.Wave( 0f, 20f, 10f ), 1e-5f,
			"the two ground axes count alike, so the crests run corner to corner" );
		Assert.AreEqual( System.MathF.Sin( 30f ), ParkBuildMarkers.Wave( 0f, 10f, 20f ), 1e-5f, "in world units, not cells" );
	}
}
