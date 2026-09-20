using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Channel 0x200, which says how far along its route a thing has travelled at each frame. These read
/// real game files and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// The numbers here were measured out of the files before the reader existed, so they fail a reader
/// that looks at the wrong offset rather than merely agreeing with whatever this one does.
/// </para>
/// </summary>
[TestClass]
public class AnimationPathTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private AnimationFile Read( string path ) => new( new MemoryStream( data.ReadAllBytes( path ) ) );

	/// <summary>
	/// The plainest case in the game: space's slide runs its whole route exactly once, 0 to 100 over
	/// 121 frames. It is what says the value is a percentage rather than a distance or a frame count.
	/// </summary>
	[TestMethod]
	public void TheSlideRunsItsWholeRouteExactlyOnce()
	{
		var scalar = Read( "levels/space/rides/slide/slidem.MD2" ).PathTracks.Single();

		Assert.AreEqual( 121, scalar.Values.Length, "one value per frame" );
		Assert.AreEqual( 0f, scalar.Values[0], 0.01f, "starts at the beginning of the route" );
		Assert.AreEqual( 100f, scalar.Values[^1], 0.01f, "and ends one whole lap later" );
		Assert.AreEqual( 0f, scalar.Start, 0.01f, "the record's first word is nought on all 71 tracks" );
	}

	/// <summary>
	/// The ferry travels its route backwards, so this clip's scalar ends below where it started. Only
	/// 46 of the game's 71 tracks never fall, so a sampler that assumes progress only rises is wrong
	/// for the other 25 - of which 4 never rise at all and 21 go both ways, passing 100 and wrapping.
	/// </summary>
	[TestMethod]
	public void TheFerryRunsItsRouteBackwards()
	{
		var scalar = Read( "levels/jungle/features/ferry/FerryM3.md2" ).PathTracks.Single();

		Assert.AreEqual( 501, scalar.Values.Length, "501 frames" );
		Assert.IsTrue( scalar.Values[0] > scalar.Values[^1],
			$"the ferry's progress should fall, but it runs {scalar.Values[0]} to {scalar.Values[^1]}" );

		Assert.AreEqual( 33.903f, scalar.Values[0], 0.01f, "from a third of the way round" );
		Assert.AreEqual( -0.017f, scalar.Values[^1], 0.01f, "back past the start" );
	}

	/// <summary>
	/// The bus's journey is split across three clips, and they are consecutive legs of ONE lap: each
	/// begins where the last ended, and together they span 100. That is the structure the whole decode
	/// rests on, and it fails if the three are read as unrelated animations.
	/// </summary>
	[TestMethod]
	public void TheBusThreeClipsChainIntoOneLap()
	{
		var second = Read( "levels/jungle/features/bus/Busm2.MD2" ).PathTracks.Single().Values;
		var third = Read( "levels/jungle/features/bus/Busm3.MD2" ).PathTracks.Single().Values;
		var first = Read( "levels/jungle/features/bus/Busm1.MD2" ).PathTracks.Single().Values;

		Assert.AreEqual( 55.997f, second.Max(), 0.01f, "m2 ends where m3 begins" );
		Assert.AreEqual( 55.996f, third.Min(), 0.01f, "m3 begins where m2 ended" );
		Assert.AreEqual( 99.835f, third.Max(), 0.01f, "m3 ends where m1 begins" );
		Assert.AreEqual( 99.835f, first.Min(), 0.01f, "m1 begins where m3 ended" );

		Assert.AreEqual( 100.025f, first.Max() - second.Min(), 0.01f,
			"the three together are one lap, starting part-way round" );
	}

	/// <summary>
	/// The other side of it. Most clips in the game carry no route scalar at all, and a reader that
	/// follows the +0x24 slot without testing the flag invents one out of whatever is there.
	/// </summary>
	[TestMethod]
	public void AClipWithNoRouteScalarHasNone()
	{
		Assert.AreEqual( 0, Read( "levels/jungle/features/gates/gatesm1.MD2" ).PathTracks.Count,
			"the gates do not travel" );
	}

	/// <summary>
	/// Sampling between frames, and holding at either end rather than running off the array - the same
	/// shape as the position channel's sampler.
	/// </summary>
	[TestMethod]
	public void ProgressIsHeldAtTheEndsAndInterpolatedBetween()
	{
		var scalar = Read( "levels/space/rides/slide/slidem.MD2" ).PathTracks.Single();

		Assert.AreEqual( scalar.Values[0], scalar.Sample( -5f ), 0.001f, "held before the first frame" );
		Assert.AreEqual( scalar.Values[^1], scalar.Sample( 9999f ), 0.001f, "and after the last" );
		Assert.AreEqual( scalar.Values[0], scalar.Sample( 0f ), 0.001f, "exact on a frame" );

		var midway = scalar.Sample( 10.5f );
		var low = scalar.Values[10];
		var high = scalar.Values[11];

		Assert.IsTrue( midway >= low && midway <= high,
			$"{midway} should lie between frame 10's {low} and frame 11's {high}" );
	}
}
