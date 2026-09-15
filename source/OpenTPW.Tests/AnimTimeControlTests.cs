using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The arithmetic one animation player does - <see cref="AnimTimeControl"/>, which is the engine's own
/// name for it.
///
/// <para>
/// <b>These need no game installed on purpose.</b> Every number here is the channel's own arithmetic on
/// spans handed to it, so the properties that are easiest to "simplify" away - the truncation, the strict
/// finish, the overshoot carried through milliseconds rather than frames - are pinned on any machine
/// rather than only on one with the game on it.
/// </para>
/// </summary>
[TestClass]
public class AnimTimeControlTests
{
	/// <summary>
	/// A clip is at its first frame the moment it starts and at its last when its whole length has gone by.
	/// Ten seconds of a three-hundred frame clip is all of it, because the engine counts thirty frames to
	/// the second going in and something a shade under a thirtieth of a second per frame coming back.
	/// </summary>
	[TestMethod]
	public void AClipRunsFromItsFirstFrameToItsLast()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 300f );

		Assert.AreEqual( 0f, channel.AnimFrame, 0.001f, "it should start at the beginning" );
		Assert.AreEqual( 300f, channel.TotalAnimFrames, "and know how long it is" );
		Assert.AreEqual( 5, channel.AnimID );
		Assert.IsFalse( channel.IsIdle );

		channel.MoveTo( 5000 );

		Assert.AreEqual( 150f, channel.AnimFrame, 0.01f, "halfway through, halfway in" );

		channel.MoveTo( 10000 );

		Assert.AreEqual( 300f, channel.AnimFrame, 0.01f, "and all of it after its whole length" );
	}

	/// <summary>
	/// <b>A clip sitting exactly on its last frame has not finished.</b> The engine's test is
	/// <c>total &lt; elapsed</c> and it is strict, which is what lets it pose that final frame once before
	/// anything replaces the clip - a <c>&lt;=</c> here would cut every animation in the game one frame
	/// short and nothing would look obviously wrong.
	/// </summary>
	[TestMethod]
	public void AClipExactlyOnItsLastFrameHasNotFinished()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 300f );
		channel.MoveTo( 10000 );

		Assert.IsFalse( channel.IsFinished, "exactly on the last frame is still playing" );

		channel.MoveTo( 10001 );

		Assert.IsTrue( channel.IsFinished, "and one millisecond later it is over" );
	}

	/// <summary>
	/// A clip started on a channel that had already run past its end begins that far into itself, rather
	/// than at nought - the engine carries the overshoot by moving the new clip's start stamp backwards
	/// (<c>FUN_00472bc0</c>), which is the whole reason the stamp exists instead of an accumulator.
	///
	/// <para>
	/// <b>The carry goes through milliseconds and is truncated on the way</b>, so thirty frames of overshoot
	/// come back as 999ms and not 1000, and the new clip opens at 29.97 frames rather than a round 30. That
	/// is the engine's own rounding and not an approximation here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnOvershootCarriesIntoTheNextClip()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 300f );
		channel.MoveTo( 11000 );

		Assert.IsTrue( channel.IsFinished, "a second past its ten" );
		Assert.AreEqual( 330f, channel.AnimFrame, 0.01f, "thirty frames past the end" );

		var carry = channel.AnimFrame - channel.TotalAnimFrames;

		channel.Start( 6, 0, 0, 1f, 11000, frames: 600f, carry: carry );

		Assert.AreEqual( 29.97f, channel.AnimFrame, 0.01f,
			"the overshoot arrives as milliseconds, truncated - not as a round thirty frames" );
	}

	/// <summary>
	/// The carry is clamped to the length of the clip it is carried into, so a channel left finished for a
	/// long time cannot skip a short clip entirely - it opens it at its end instead.
	///
	/// <para>
	/// <b>The tolerance here is tight on purpose, and it was not always.</b> Truncating the clamped carry
	/// gives 599.96997 frames and rounding it gives exactly 600 - a difference of 0.03 - so the ±0.05 this
	/// first carried would have passed under the very mutation the test exists to catch. A tolerance wider
	/// than the effect being measured is decoration, not a test.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheOvershootCannotCarryPastTheNewClipsOwnEnd()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 600f, carry: 100000f );

		Assert.IsFalse( channel.IsFinished, "clamped to this clip, so it is at the end and not past it" );
		Assert.AreEqual( 599.97f, channel.AnimFrame, 0.005f, "which is the end, less the truncation" );
	}

	/// <summary>
	/// <b>Emptying the queue deliberately leaves the flags behind.</b> The engine's promotion resets the
	/// queued role, entry and speed and pointedly does not touch <c>DeferredFlags</c> (<c>0x004738a3</c>),
	/// so a clip queued later inherits the flags of the one before it unless it sets its own. Clearing it
	/// would be a behaviour change wearing the clothes of a tidy-up, which is why it is pinned here.
	/// </summary>
	[TestMethod]
	public void EmptyingTheQueueLeavesItsFlagsBehindOnPurpose()
	{
		var channel = new AnimTimeControl();

		channel.Queue( 3, 2, AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag, 2f );

		Assert.IsTrue( channel.HasQueued );

		channel.ClearQueue();

		Assert.IsFalse( channel.HasQueued, "the role is back to the sentinel" );
		Assert.AreEqual( RideAnimations.NoRole, channel.DeferredAnimID );
		Assert.AreEqual( 0, channel.DeferredSubAnim );
		Assert.AreEqual( 0f, channel.DeferredSpeed );

		Assert.AreEqual( AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag, channel.DeferredFlags,
			"and the flags are still there, exactly as the engine leaves them" );
	}

	/// <summary>
	/// Holding the last frame is how the engine says "finished": it re-enters the channel with role 14,
	/// which parks the timebase so that the elapsed frame lands on the total exactly. The clip is therefore
	/// still playing by the strict test above, and gets posed at its true final frame.
	/// </summary>
	[TestMethod]
	public void HoldingTheEndParksTheClipOnItsLastFrame()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 300f );
		channel.Start( AnimTimeControl.HoldAtEnd, 0, 0, 1f, 1000, frames: 0f );

		Assert.AreEqual( 300f, channel.AnimFrame, 0.01f, "pinned to the end" );
		Assert.IsFalse( channel.IsFinished, "which is not the same as being past it" );

		channel.MoveTo( 60000 );

		Assert.AreEqual( 300f, channel.AnimFrame, 0.01f, "and a minute later it has not moved" );
	}

	/// <summary>
	/// A frozen channel stops advancing, but <see cref="AnimTimeControl.NoPauseAnimTime"/> keeps following
	/// the clock - which is the only reason that field exists rather than being a copy of the other. Freezing
	/// is role 13, and it pins the clip to its first frame.
	/// </summary>
	[TestMethod]
	public void AFrozenChannelStopsButItsUnpausedClockDoesNot()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 300f );
		channel.MoveTo( 5000 );

		Assert.AreEqual( 150f, channel.AnimFrame, 0.01f );

		channel.Start( AnimTimeControl.FreezeAtStart, 0, 0, 1f, 5000, frames: 0f );
		channel.MoveTo( 20000 );

		Assert.AreEqual( 0f, channel.AnimFrame, 0.001f, "frozen on its first frame" );
		Assert.AreEqual( 20000, channel.NoPauseAnimTime, "while the clock that ignores freezes ran on" );
		Assert.AreNotEqual( channel.NoPauseAnimTime, channel.AnimTime,
			"the two must not be the same field - that is what makes the unpaused one worth keeping" );
	}

	/// <summary>
	/// A trigger naming a role the model has nothing for <b>stops the channel</b> rather than leaving the
	/// old clip running. The engine restores the rest pose and writes the sentinel role, which is what makes
	/// the eight shipped references to absent roles a stop rather than a no-op.
	/// </summary>
	[TestMethod]
	public void StartingAClipThatIsNotThereStopsWhateverWasPlaying()
	{
		var channel = new AnimTimeControl();

		channel.Start( 5, 0, 0, 1f, 0, frames: 300f );

		Assert.IsFalse( channel.IsIdle );

		channel.Start( 7, 0, 0, 1f, 1000, frames: 0f );

		Assert.IsTrue( channel.IsIdle, "the channel is stopped, not left running the old clip" );
		Assert.AreEqual( RideAnimations.NoRole, channel.AnimID );
	}
}
