using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Timing that must come out the same at any frame rate. Each test steps a clock the way
/// <see cref="Time.Update"/> does - a delta added every frame - at 30, 60 and 144 frames a second.
/// </summary>
[TestClass]
public class AnimationTimingTests
{
	private static readonly float[] FrameRates = { 30f, 60f, 144f };

	/// <summary>Three clips of made-up lengths: a second, 1.666 seconds and 0.666 of one.</summary>
	private static readonly int[] Clips = { 14, 4, 15 };

	private static int LengthOf( int clip ) => clip switch { 14 => 1000, 4 => 1666, 15 => 666, _ => 0 };

	private static ClipSequence Sequence() => new( Clips, LengthOf );

	[TestMethod]
	public void ClipsPlayBackToBackAndTheLastIsHeld()
	{
		var sequence = Sequence();

		Assert.AreEqual( 3332, sequence.Milliseconds );
		AssertAt( sequence, 0f, 14, 0f );
		AssertAt( sequence, 0.5f, 14, 0.5f );
		AssertAt( sequence, 1.25f, 4, 0.25f );
		AssertAt( sequence, 2.766f, 15, 0.1f );
		AssertAt( sequence, 10f, 15, 0.666f );
		AssertAt( sequence, -1f, 14, 0f );

		Assert.IsFalse( sequence.IsFinished( 3.3f ) );
		Assert.IsTrue( sequence.IsFinished( 3.34f ) );
	}

	[TestMethod]
	public void NoTimeIsLostAtAClipChangeAtAnyFrameRate()
	{
		var sequence = Sequence();

		foreach ( var rate in FrameRates )
		{
			var now = 0f;
			var changes = 0;
			var previous = Clips[0];

			while ( now < 4f )
			{
				now += 1f / rate;

				Assert.IsTrue( sequence.TryLocate( now, out var clip, out var intoClip ) );

				// Where the clip started plus how far into it we are is always just the time - the
				// thing that goes wrong when each clip is restarted from zero on the frame that
				// notices the last one ended.
				var expected = MathF.Min( now, sequence.Milliseconds / 1000f );
				Assert.AreEqual( expected, StartOf( clip ) + intoClip, 0.0005f, $"{rate}fps at {now}s" );

				if ( clip != previous )
				{
					changes++;
					previous = clip;
				}
			}

			Assert.AreEqual( 2, changes, $"{rate}fps" );
		}
	}

	[TestMethod]
	public void ALongFrameLandsWhereTheTimeSays()
	{
		var sequence = Sequence();

		// Straight from half a second in to three: all of clip 4 goes by within the one frame.
		AssertAt( sequence, 0.5f, 14, 0.5f );
		AssertAt( sequence, 3f, 15, 0.334f );
	}

	[TestMethod]
	public void AnEmptyRunHasNothingToPlay()
	{
		Assert.IsFalse( ClipSequence.Empty.TryLocate( 1f, out _, out _ ) );
		Assert.IsTrue( ClipSequence.Empty.IsFinished( 0f ) );
	}

	private static float StartOf( int clip )
	{
		var start = 0;

		foreach ( var candidate in Clips )
		{
			if ( candidate == clip )
				break;

			start += LengthOf( candidate );
		}

		return start / 1000f;
	}

	private static void AssertAt( ClipSequence sequence, float seconds, int clip, float intoClip )
	{
		Assert.IsTrue( sequence.TryLocate( seconds, out var actualClip, out var actualInto ) );
		Assert.AreEqual( clip, actualClip, $"clip at {seconds}s" );
		Assert.AreEqual( intoClip, actualInto, 0.0005f, $"time into clip at {seconds}s" );
	}
}
