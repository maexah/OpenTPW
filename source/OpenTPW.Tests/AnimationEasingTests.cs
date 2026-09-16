using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The easing curve a rotation key blends out of - the ushort at key +0x02 and the eight-byte table it
/// indexes at track descriptor +0x34.
///
/// <para>
/// <b>These guard a field that was read as a flag word for as long as it was read at all.</b> It was
/// recorded as "flags (0 or 0xFFFF)", and both halves of that were wrong in the same way: 0xFFFF is a
/// sentinel meaning "blend evenly", but 0 is not a cleared flag - it is curve number nought, the commonest
/// id in the game. The pointer at +0x34 was known to exist and noted as "looks like a byte ramp, but the
/// records are not a fixed length and a third are not monotonic - noted, not claimed". At the true stride
/// of eight bytes the records are a fixed length; what varies is which of them a key asks for.
/// </para>
///
/// <para>
/// The engine reads it at 0x00471c73, immediately before its rotation sampler: it takes the id off the key
/// its own search settled on, and where that is not 0xFFFF it bends the blend through the curve rather
/// than passing the even fraction straight to the slerp. <b>457 of the 1,166 clips under levels/ carry at
/// least one eased rotation key</b>, so this is not a corner of the format.
/// </para>
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class AnimationEasingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Read through the file system this test mounted rather than the global one, which belongs to a
	/// running game - the same reason <see cref="AdvisorBlinkTests"/> gives, and the same path it uses.
	/// </summary>
	private AnimationFile Clip( int clip )
		=> new( new MemoryStream( data.ReadAllBytes( $"global/advisor/Advisorm{clip}.MD2" ) ) );

	/// <summary>
	/// The track turning one node, chosen by the node rather than by its place in the list so that a test
	/// says which mesh it means. The key count is asserted here because every test below depends on having
	/// found the track it meant.
	/// </summary>
	private static AnimationFile.RotationTrack Track( AnimationFile clip, int target, int keys )
	{
		var track = clip.RotationTracks.FirstOrDefault( rotation => rotation.TargetIndex == target );

		Assert.IsNotNull( track, $"expected a rotation track on node {target}, out of "
			+ $"{clip.RotationTracks.Count}: {string.Join( ", ", clip.RotationTracks.Select( r => r.TargetIndex ) )}" );

		Assert.AreEqual( keys, track!.FrameIndices.Length, $"node {target}'s key count" );

		return track;
	}

	/// <summary>
	/// A key names the curve it blends <i>out of</i>, and the key a track ends on names none - there is no
	/// segment beginning there. That holds for every eased track in the game: all 1,759 of them end on
	/// <see cref="AnimationFile.RotationTrack.NoCurve"/>, with no exceptions in either direction.
	/// </summary>
	[TestMethod]
	public void AKeyNamesTheCurveItBlendsOutOfAndTheLastKeyNamesNone()
	{
		var track = Track( Clip( 1 ), target: 26, keys: 3 );

		CollectionAssert.AreEqual(
			new ushort[] { 0, 1, AnimationFile.RotationTrack.NoCurve },
			track.CurveIds,
			"the advisor's first clip turns node 26 through two eased segments and stops" );

		// Anti-vacuity: the ids above say nothing unless the table they index was actually read.
		Assert.AreEqual( 2, track.Curves.Length, "one curve per id the keys ask for" );

		CollectionAssert.AreEqual(
			new byte[] { 32, 66, 105, 141, 176, 208, 233, 249 },
			track.Curves[0],
			"and this is the ramp an older note here quoted without knowing what it was" );
	}

	/// <summary>
	/// <b>The table is indexed by the id a key names, not by where the key sits.</b> That distinction is
	/// invisible on most of the game - 9,358 of the 12,428 eased keys name the id that happens to equal
	/// their own index - and wrong on the rest, where <b>3,070</b> do not. Ids run as high as 100, well past
	/// any key count.
	///
	/// <para>
	/// This track is the cleanest witness: only its <i>third</i> key eases, and it names curve <i>nought</i>.
	/// A reader indexing by key position would reach for the third entry and pose the turn through an
	/// entirely different curve.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheTableIsIndexedByTheIdAndNotByTheKeysOwnPosition()
	{
		var track = Track( Clip( 14 ), target: 28, keys: 4 );

		const ushort none = AnimationFile.RotationTrack.NoCurve;

		CollectionAssert.AreEqual(
			new ushort[] { none, none, 0, none },
			track.CurveIds,
			"only the third key of this one eases, and it names curve nought" );

		Assert.AreEqual( 1, track.Curves.Length, "so exactly one curve is wanted, whatever the key's index" );

		CollectionAssert.AreEqual(
			new byte[] { 19, 38, 64, 96, 128, 160, 192, 224 },
			track.Curves[0],
			"which is the table's FIRST entry, not its third" );

		// And the blend off that key really is taken through it. Asserting the table alone would leave a
		// reader that looked the curve up by key position entirely green: key 2 would fall off the end of a
		// one-entry table, quietly blend evenly, and every line above would still pass.
		Assert.AreEqual( 0.4392154f, track.Ease( 2, 0.5f ), 1e-5f,
			"half way off key 2 is bent by curve nought" );

		Assert.AreNotEqual( 0.5f, track.Ease( 2, 0.5f ), 1e-3f, "which is not where an even blend would be" );
	}

	/// <summary>
	/// <b>The ramp is ten points and nine straight segments</b>, not eight: the table's eight bytes are its
	/// inner points, with nought implied before the first and one implied after the last.
	///
	/// <para>
	/// The implied one at the end is doing real work rather than tidying an edge - the last byte is 255 in
	/// only <b>112 of the game's 12,428 curve entries</b>, so almost every curve is still climbing when it
	/// reaches that ninth segment. Checked at each ninth of the way, where the eased value is the byte for
	/// that boundary; that is what makes it nine segments and not eight.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheRampIsNineStraightSegmentsFromNoughtToOne()
	{
		var track = Track( Clip( 1 ), target: 26, keys: 3 );
		var curve = track.Curves[0];

		Assert.AreEqual( 0f, track.Ease( 0, 0f ), 1e-6f, "a blend starts where it started" );
		Assert.AreEqual( 1f, track.Ease( 0, 1f ), 1e-5f, "and arrives where it was going" );

		for ( int boundary = 1; boundary <= 8; ++boundary )
		{
			Assert.AreEqual(
				curve[boundary - 1] / 255f,
				track.Ease( 0, boundary / 9f ),
				1e-5f,
				$"{boundary}/9 of the way along should be the curve's {boundary}th byte" );
		}
	}

	/// <summary>
	/// A key naming <see cref="AnimationFile.RotationTrack.NoCurve"/> is handed back the fraction it was
	/// given, untouched. 1,263 of the game's 3,022 rotation tracks carry no table at all, and every one of
	/// their 9,051 keys is in this state - so this is the ordinary case, not the exception.
	/// </summary>
	[TestMethod]
	public void AKeyThatNamesNoCurveIsLeftToBlendEvenly()
	{
		var track = Track( Clip( 14 ), target: 28, keys: 4 );

		foreach ( var key in new[] { 0, 1, 3 } )
		{
			foreach ( var fraction in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f } )
			{
				Assert.AreEqual( fraction, track.Ease( key, fraction ), 0f,
					$"key {key} names no curve, so {fraction} should come back exactly" );
			}
		}
	}

	/// <summary>
	/// <b>A curve need not climb, and this one falls away to nothing.</b> 2,797 of the game's 12,428 curve
	/// entries are not monotonic, and what that means on screen is a pose that moves off and then comes back
	/// the way it came before going on - an author's overshoot, written down.
	///
	/// <para>
	/// It is pinned here so that nobody later reads it as a bad decode and "fixes" it by sorting the bytes.
	/// An older note called a third of these non-monotonic, which was measured before the record length was
	/// known; at the true stride of eight it is about 22%, and it is still thousands of entries.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ACurveThatDipsTakesThePoseBackTheWayItCame()
	{
		var track = Track( Clip( 14 ), target: 26, keys: 5 );

		CollectionAssert.AreEqual(
			new byte[] { 17, 11, 0, 0, 0, 0, 40, 122 },
			track.Curves[0],
			"the advisor's clip 14 really does ship a curve that falls back to nothing" );

		var risen = track.Ease( 0, 0.11f );
		var fallen = track.Ease( 0, 0.30f );

		Assert.IsTrue( risen > 0f, $"it should set off first, not sit still: {risen} at 0.11" );
		Assert.IsTrue( fallen < risen, $"and then come back: {risen} at 0.11 but {fallen} at 0.30" );

		Assert.AreEqual( 0f, track.Ease( 0, 0.5f ), 1e-6f,
			"by half way it is exactly back where it started, and waits there" );
	}

	/// <summary>
	/// The point of all of it: <see cref="AnimationFile.RotationTrack.Sample"/> poses the turn through the
	/// curve rather than evenly across the gap. Half way between this track's first two keys by time is
	/// about 0.62 of the way by pose.
	///
	/// <para>
	/// Both halves are asserted, because either alone would pass while the wiring was wrong: that the
	/// sampled pose is the slerp taken at the <i>eased</i> fraction, and that this is not the pose an even
	/// blend gives. The second needs the two keys to be different poses, which is checked rather than
	/// assumed - node 28 of this same advisor holds three keys at one pose, where such a test would pass
	/// whatever the easing did.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnEasedBlendIsNotTheEvenOneItReplaces()
	{
		var track = Track( Clip( 1 ), target: 26, keys: 3 );

		Assert.IsTrue(
			System.MathF.Abs( System.Numerics.Quaternion.Dot( track.Rotations[0], track.Rotations[1] ) ) < 0.999f,
			"the two keys must be different poses or this test proves nothing" );

		var frame = (track.FrameIndices[0] + track.FrameIndices[1]) / 2f;

		Assert.AreEqual( 0.5f,
			(frame - track.FrameIndices[0]) / (float)(track.FrameIndices[1] - track.FrameIndices[0]),
			1e-6f, "the chosen frame should sit exactly half way between the keys" );

		var eased = track.Ease( 0, 0.5f );

		Assert.AreNotEqual( 0.5f, eased, 1e-3f, "and the curve should actually bend that half" );

		var sampled = track.Sample( frame );
		var throughTheCurve = System.Numerics.Quaternion.Slerp( track.Rotations[0], track.Rotations[1], eased );
		var evenly = System.Numerics.Quaternion.Slerp( track.Rotations[0], track.Rotations[1], 0.5f );

		Assert.AreEqual( throughTheCurve.X, sampled.X, 1e-6f, "sampled X" );
		Assert.AreEqual( throughTheCurve.Y, sampled.Y, 1e-6f, "sampled Y" );
		Assert.AreEqual( throughTheCurve.Z, sampled.Z, 1e-6f, "sampled Z" );
		Assert.AreEqual( throughTheCurve.W, sampled.W, 1e-6f, "sampled W" );

		Assert.IsTrue(
			System.MathF.Abs( System.Numerics.Quaternion.Dot( sampled, evenly ) ) < 0.99999f,
			"the eased pose should not be the one an even blend gives" );
	}
}
