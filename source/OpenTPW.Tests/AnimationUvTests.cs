using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// How a UV animation is written down: one entry per vertex, each with its own run of keyframes.
///
/// <para>
/// <b>These guard against a reading that looks right on most of the game and is wrong on the rest.</b>
/// Reading a UV entry as a run of components sliding from a start value to an end value addresses exactly
/// the same bytes as the engine's reading whenever an entry has two keys - a two-key entry packs so that its
/// first key index is twice its entry index, so "component 2e, two components" and "key 2e, two keys"
/// coincide. Measured over every clip under levels/: 25,332 entries have two keys and 4,391 do not, and
/// those fall in 289 of the 670 UV tracks. On those the component reading does not merely flatten the middle
/// keys - it reads the (u,v) pairs as a block of u's followed by a block of v's, so it puts v values into u.
/// </para>
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class AnimationUvTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Read directly rather than through <see cref="AnimationFile.TryLoad"/>, which goes through the
	/// global file system that belongs to a running game.
	/// </summary>
	private AnimationFile Clip( string path ) => new( new MemoryStream( data.ReadAllBytes( path ) ) );

	/// <summary>The jungle Round Fountain's water, which is the clearest multi-key track in the game.</summary>
	private const string Fountain = "levels/jungle/features/fountain/fountainm.md2";

	/// <summary>
	/// An entry carries as many keys as it declares, and its frames climb. The fountain's second track is
	/// 44 entries of five keys each - the shape a start-to-end reading cannot express at all.
	/// </summary>
	[TestMethod]
	public void AnEntryCarriesItsOwnRunOfKeys()
	{
		var clip = Clip( Fountain );

		var track = clip.UvTracks.FirstOrDefault( uv => uv.EntryCount == 44 );

		Assert.IsNotNull( track, $"the fountain should carry a 44-entry UV track, not {clip.UvTracks.Count} tracks of "
			+ $"{string.Join( ", ", clip.UvTracks.Select( uv => uv.EntryCount ) )}" );

		for ( int entry = 0; entry < track!.EntryCount; ++entry )
		{
			Assert.AreEqual( 5, track.KeyCount[entry], $"entry {entry}" );

			var first = track.FirstKey[entry];

			for ( int key = 1; key < track.KeyCount[entry]; ++key )
			{
				Assert.IsTrue( track.Frames[first + key] > track.Frames[first + key - 1],
					$"entry {entry} key {key}: frames should climb, and they go "
					+ $"[{string.Join( ",", Enumerable.Range( 0, 5 ).Select( k => track.Frames[first + k] ) )}]" );
			}
		}
	}

	/// <summary>
	/// <b>The middle keys are honoured, which is the whole of the difference.</b> Asked for the exact
	/// frame a key sits on, the track answers that key's own coordinate - a property a two-point ramp
	/// cannot have, since it only ever knows the first and the last.
	///
	/// <para>
	/// Entry 0 of the fountain's water climbs 1.973, 2.534, 3.139, 3.789, 3.873 over frames 0, 16, 32, 48
	/// and 50. Read as a ramp from the first to the last, frame 32 would answer 3.189 - close enough to
	/// look plausible on screen and still the wrong number.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AskingForAKeysOwnFrameAnswersThatKey()
	{
		var clip = Clip( Fountain );
		var track = clip.UvTracks.First( uv => uv.EntryCount == 44 );

		var first = track.FirstKey[0];

		for ( int key = 0; key < track.KeyCount[0]; ++key )
		{
			var frame = track.Frames[first + key];
			var expected = track.Coordinates[first + key];

			var sampled = track.Sample( 0, frame );

			Assert.AreEqual( expected.X, sampled.X, 0.0001f, $"u at frame {frame}" );
			Assert.AreEqual( expected.Y, sampled.Y, 0.0001f, $"v at frame {frame}" );
		}

		// And the ramp a two-point reading would have drawn really is a different number, so the test
		// above is discriminating rather than merely true.
		var straight = System.Numerics.Vector2.Lerp( track.Coordinates[first],
			track.Coordinates[first + track.KeyCount[0] - 1], 32f / 50f );

		Assert.AreNotEqual( straight.Y, track.Sample( 0, 32f ).Y, 0.01f,
			"the middle key should not agree with a straight ramp, or this proves nothing" );
	}

	/// <summary>
	/// <b>u and v stay where they belong.</b> A component reading takes an entry's keys as a block of u's
	/// followed by a block of v's, so entry 0's second "component" is 1.973 - a v value sitting in u.
	/// This one scrolls only v, and u holds still at a thousandth throughout.
	/// </summary>
	[TestMethod]
	public void CoordinatesAreNotInterleavedIntoOneAnother()
	{
		var clip = Clip( Fountain );
		var track = clip.UvTracks.First( uv => uv.EntryCount == 44 );

		var first = track.FirstKey[0];

		for ( int key = 0; key < track.KeyCount[0]; ++key )
		{
			Assert.AreEqual( 0.001f, track.Coordinates[first + key].X, 0.0005f, $"u of key {key}" );

			Assert.IsTrue( track.Coordinates[first + key].Y > 1.9f,
				$"v of key {key} should be the height it scrolls through, and it is {track.Coordinates[first + key].Y}" );
		}
	}

	/// <summary>
	/// A clip whose only channels are UV still has a length, and it is the last frame any key names -
	/// which for this one is the 50 its own animation block declares.
	/// </summary>
	[TestMethod]
	public void AUvOnlyClipTakesItsLengthFromItsLastKey()
	{
		var clip = Clip( Fountain );

		Assert.IsTrue( clip.IsValid, "it carries UV tracks, so it is readable" );
		Assert.AreEqual( 50, clip.LastFrame, "the last frame any of its keys names" );
		Assert.AreEqual( 50, clip.DeclaredLastFrame, "and the file declares the same span" );
	}

	/// <summary>
	/// <b>The ordinary case is untouched.</b> The park terrain's own clip - the jungle's river and falls -
	/// is eleven UV tracks in which every entry has exactly two keys, which is where the component reading and
	/// the engine's agree. It is here so that a change made for the 43% cannot quietly move the 57%.
	/// </summary>
	[TestMethod]
	public void ATwoKeyTrackReadsAsItAlwaysDid()
	{
		var clip = Clip( "levels/jungle/terrain/basem.MD2" );

		Assert.AreEqual( 11, clip.UvTracks.Count, "the river, its falls and the rest" );

		foreach ( var track in clip.UvTracks )
		{
			for ( int entry = 0; entry < track.EntryCount; ++entry )
				Assert.AreEqual( 2, track.KeyCount[entry], $"entry {entry}" );

			// Two keys means the sample at the end is the second of them, which is exactly what a
			// start-to-end ramp answers.
			var first = track.FirstKey[0];
			var end = track.Sample( 0, track.Frames[first + 1] );

			Assert.AreEqual( track.Coordinates[first + 1].X, end.X, 0.0001f );
			Assert.AreEqual( track.Coordinates[first + 1].Y, end.Y, 0.0001f );
		}
	}
}
