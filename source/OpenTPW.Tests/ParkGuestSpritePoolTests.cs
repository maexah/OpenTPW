using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The people's quads from one frame to the next: what a frame wrote and the next did not is folded to nothing,
/// and a vertex array made again starts with nothing written.
///
/// <para>
/// These need no park and no device: a pool made for no park loads nothing and builds no model, and
/// <see cref="ParkGuestSprites.Resize"/> and <see cref="ParkGuestSprites.Fold"/> are the two halves of the draw's
/// bookkeeping that touch neither. That the draw and the rebuild call them is the game run's to show.
/// </para>
/// </summary>
[TestClass]
public class ParkGuestSpritePoolTests
{
	[TestCleanup]
	public void LetThePoolsGo() => TestRun.DeleteEvery<ParkGuestSprites>();

	private static ParkGuestSprites Pool( int quads )
	{
		var pool = new ParkGuestSprites( "jungle", null );
		pool.Resize( quads );

		return pool;
	}

	/// <summary>
	/// Sixty-two people under the facing overlay write 124 quads; forty-two go home inside one frame and the array is
	/// made again at four quads a person and sixteen, 96. The next frame folds nothing past it.
	/// </summary>
	[TestMethod]
	public void ASmallerArrayStartsWithNothingUploaded()
	{
		var pool = Pool( (62 * 4) + 16 );

		Assert.AreEqual( 124, pool.Fold( 124 ) );

		pool.Resize( (20 * 4) + 16 );

		Assert.AreEqual( 40, pool.Fold( 40 ), "the upload reaches what this frame wrote, and nothing of the array before" );
	}

	/// <summary>The same array, a shorter frame: the upload still reaches the last frame's quads, once, to fold them.</summary>
	[TestMethod]
	public void AShorterFrameReachesTheLastFramesQuadsOnce()
	{
		var pool = Pool( 96 );

		Assert.AreEqual( 60, pool.Fold( 60 ) );
		Assert.AreEqual( 60, pool.Fold( 36 ), "the 24 quads left over are folded and uploaded" );
		Assert.AreEqual( 36, pool.Fold( 36 ), "and not again" );
		Assert.AreEqual( 36, pool.Fold( 0 ), "an empty frame reaches the 36 to fold them" );
		Assert.AreEqual( 0, pool.Fold( 0 ) );
	}

	/// <summary>A frame that counts more quads than the array holds has uploaded the array, and no more is folded later.</summary>
	[TestMethod]
	public void ACountPastTheArraysEndIsTheArrays()
	{
		var pool = Pool( 96 );

		Assert.AreEqual( 96, pool.Fold( 200 ) );
		Assert.AreEqual( 96, pool.Fold( 0 ) );
	}
}
