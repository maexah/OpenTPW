using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The world-sprite animation VM against the executable's own scripts.
///
/// <para>
/// <b>These need no game files and no graphics device</b>, because the scripts are compiled into
/// <c>testme.exe</c> rather than stored in the game's data - so everything here is arithmetic over a table
/// copied out of it, the same arrangement <see cref="PeepHeadingTests"/> has.
/// </para>
/// <para>
/// <b>What is worth pinning here is the shape, not the pictures.</b> A test that asserts the walk shows frame
/// 3 on its fourth turn would pass just as happily against a table somebody typed in wrong. The tests that
/// earn their place are the ones that would fail if the <i>rules</i> were wrong: that only a frame ends a
/// turn, that a jump moves the counter without changing which script is running, that every script's jump
/// lands on a real instruction, and that no two scripts overlap.
/// </para>
/// </summary>
[TestClass]
public class SpriteScriptTests
{
	/// <summary>A sprite on no script at all, ready to be put on one.</summary>
	private static SpriteScript Fresh() => new( SpriteScript.None, SpriteScript.None, 0, 0 );

	/// <summary>Turns of the sprite system, 62ms apart, from a sprite that has just been scheduled.</summary>
	private static List<int> FramesOver( SpriteScript sprite, int turns, int every = 62 )
	{
		var seen = new List<int>();

		for ( var turn = 1; turn <= turns; ++turn )
		{
			if ( sprite.Step( turn * every ) )
				seen.Add( sprite.Frame );
		}

		return seen;
	}

	/// <summary>
	/// <b>The walk is the executable's own eight pictures of set 1, and it loops.</b> This is the script the
	/// whole feature exists for.
	/// </summary>
	[TestMethod]
	public void TheWalkIsEightPicturesOfSetOneAndItLoops()
	{
		var sprite = Fresh();

		Assert.IsTrue( sprite.Start( SpriteScript.Walking ), "animation 1 names a script" );
		Assert.AreEqual( 42, sprite.Script, "the walk lives at word 42 of the original's array" );

		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		var frames = FramesOver( sprite, 10 );

		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1 }, frames,
			"eight frames and then round again" );

		Assert.AreEqual( 1, sprite.Set, "and set 1 throughout" );
	}

	/// <summary>
	/// <b>Only showing a frame ends a turn.</b> Choosing a set does not, so the turn a script starts on gets
	/// through its preamble and its first picture together - which is why a guest never appears for one turn
	/// wearing the set they had before.
	/// </summary>
	[TestMethod]
	public void ChoosingASetCostsNoTurnButShowingAFrameDoes()
	{
		var sprite = Fresh();

		sprite.Start( SpriteScript.Walking );
		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		Assert.IsTrue( sprite.Step( 62 ) );

		// Three instructions ran on that one turn: the locals write, the set, and frame 0.
		Assert.AreEqual( 1, sprite.Set, "the set was chosen on the same turn as the first picture" );
		Assert.AreEqual( 0, sprite.Frame );

		Assert.IsTrue( sprite.Step( 124 ) );
		Assert.AreEqual( 1, sprite.Frame, "and the next turn moves on by exactly one picture" );
	}

	/// <summary>
	/// <b>The standing script is one picture of set 0, and it never changes.</b>
	/// </summary>
	[TestMethod]
	public void StandingIsOnePictureOfSetZero()
	{
		var sprite = Fresh();

		sprite.Start( SpriteScript.Standing );
		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		var frames = FramesOver( sprite, 5 );

		CollectionAssert.AreEqual( new[] { 0, 0, 0, 0, 0 }, frames );
		Assert.AreEqual( 0, sprite.Set );
	}

	/// <summary>
	/// <b>A sprite left on the default interval takes a turn off, and a walking one does not.</b> This is the
	/// entire animation-rate mechanism: 62 against turns 62ms apart fails a strict "has it come due", so a
	/// standing sprite runs at half the rate without anything having decided that.
	/// </summary>
	[TestMethod]
	public void TheDefaultIntervalShowsAPictureEveryOtherTurn()
	{
		var idle = Fresh();
		idle.Start( SpriteScript.Walking );
		idle.ScheduleFrom( 0 );

		Assert.AreEqual( SpriteScript.DefaultInterval, idle.Interval, "62, as the original's constructor writes" );

		var lazy = 0;
		var busy = 0;

		for ( var turn = 1; turn <= 10; ++turn )
		{
			if ( idle.Step( turn * 62 ) )
				++lazy;
		}

		var walking = Fresh();
		walking.Start( SpriteScript.Walking );
		walking.Interval = 1;
		walking.ScheduleFrom( 0 );

		for ( var turn = 1; turn <= 10; ++turn )
		{
			if ( walking.Step( turn * 62 ) )
				++busy;
		}

		Assert.AreEqual( 10, busy, "a walking sprite comes due on every turn" );
		Assert.AreEqual( 5, lazy, "and one on the default interval on every other turn" );
	}

	/// <summary>
	/// <b>A jump moves the counter and leaves the script alone.</b> Animation 22 runs off its own frames into
	/// animation 23's body and keeps answering that it is on 1310 - which is what the original's "is this
	/// person already walking?" test reads, so getting it wrong would make a walking guest be put back on the
	/// walk every single tick.
	/// </summary>
	[TestMethod]
	public void JumpingDoesNotChangeWhichScriptIsRunning()
	{
		var sprite = Fresh();

		sprite.Start( 22 );
		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		Assert.AreEqual( 1310, sprite.Script );

		// Its eight frames, and then the ninth turn is taken from the script it jumped into.
		for ( var turn = 1; turn <= 9; ++turn )
			sprite.Step( turn * 62 );

		Assert.AreEqual( 1310, sprite.Script, "still reports the script it was started on" );
		Assert.AreEqual( 8, sprite.Set, "but is drawing animation 23's set" );
	}

	/// <summary>
	/// <b>A one-shot animation plays once and settles into standing.</b> Six of the person scripts end by
	/// jumping to word 90 rather than to themselves, which is how the original stops them repeating.
	/// </summary>
	[TestMethod]
	public void AOneShotAnimationFallsIntoTheStandingScript()
	{
		var sprite = Fresh();

		sprite.Start( 5 );
		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		var frames = FramesOver( sprite, 8 );

		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3, 0, 0, 0, 0 }, frames,
			"its four pictures, then standing for ever" );

		Assert.AreEqual( 0, sprite.Set, "which is set 0" );
	}

	/// <summary>
	/// <b>Every script's jump lands on a real instruction.</b> A structural check over all twenty-one: if the
	/// word count of any opcode were wrong, or a jump target mistyped, a script would jump into the middle of
	/// another one and this would say so.
	/// </summary>
	[TestMethod]
	public void EveryScriptRunsForEverWithoutFallingOffTheEnd()
	{
		for ( var animation = 0; animation < SpriteScript.TableSize; ++animation )
		{
			if ( SpriteScript.EntryFor( animation ) == SpriteScript.None )
				continue;

			var sprite = Fresh();

			sprite.Start( animation );
			sprite.Interval = 1;
			sprite.ScheduleFrom( 0 );

			// Comfortably past the longest script, which is thirty-two pictures.
			for ( var turn = 1; turn <= 80; ++turn )
				sprite.Step( turn * 62 );

			Assert.AreNotEqual( SpriteScript.None, sprite.Pc,
				$"animation {animation} ran off the end of the program" );
		}
	}

	/// <summary>
	/// <b>The four table entries that are not scripts are not treated as ones.</b> Entries 13 to 16 hold the
	/// literals 0 to 3, which the original tells apart by range and looks up a different way entirely. Reading
	/// them as addresses would point four animations at word 0, 1, 2 and 3 of the script array.
	/// </summary>
	[TestMethod]
	public void TheTableEntriesThatAreStateNumbersNameNoScript()
	{
		foreach ( var animation in new[] { 0, 13, 14, 15, 16 } )
		{
			Assert.AreEqual( SpriteScript.None, SpriteScript.EntryFor( animation ),
				$"animation {animation} is a state number, not a script address" );

			Assert.IsFalse( Fresh().Start( animation ), "so nothing can be started on it" );
		}

		Assert.AreEqual( 42, SpriteScript.EntryFor( SpriteScript.Walking ) );
		Assert.AreEqual( 66, SpriteScript.EntryFor( SpriteScript.Hurrying ) );
		Assert.AreEqual( 90, SpriteScript.EntryFor( SpriteScript.Standing ) );
		Assert.AreEqual( 2, SpriteScript.EntryFor( 9 ), "the half-speed walk" );
	}

	/// <summary>
	/// <b>Animation 9 is the walk at half speed, and it is the same eight pictures.</b> Worth pinning because
	/// it is the one script whose whole purpose is its repeats - a decode that dropped duplicate frames would
	/// turn it back into an ordinary walk and nothing else would notice.
	/// </summary>
	[TestMethod]
	public void TheHalfSpeedWalkHoldsEveryPictureTwice()
	{
		var sprite = Fresh();

		sprite.Start( 9 );
		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		CollectionAssert.AreEqual( new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 0, 0 },
			FramesOver( sprite, 18 ) );
	}

	/// <summary>
	/// <b>A sprite resumes from where the save left it rather than restarting.</b> Sixteen of the shipped
	/// park's eighteen people are saved part-way through the walk, and starting them all at frame 0 would put
	/// the whole park in step with itself.
	/// </summary>
	[TestMethod]
	public void ASpriteCarriesOnFromTheWordTheSaveRecorded()
	{
		// Word 57 is the sixth of the walk's eight frames.
		var sprite = new SpriteScript( script: 42, pc: 57, spriteNumber: 1, frame: 4 );

		Assert.AreEqual( 4, sprite.Frame, "the saved picture, until a turn moves it" );

		sprite.Interval = 1;
		sprite.ScheduleFrom( 0 );

		CollectionAssert.AreEqual( new[] { 5, 6, 7, 0 }, FramesOver( sprite, 4 ),
			"and carries on through the cycle from there" );
	}

	/// <summary>
	/// <b>The interval is a doubling, and the numbers are the original's.</b> A full walking step of about
	/// 15,728 units truncates to nothing when hurrying and to one when not - so the person who is <i>not</i>
	/// in a hurry is the one whose interval gets written, which reads backwards and is what the code does.
	/// </summary>
	[TestMethod]
	public void AWalkingStepTruncatesToNothingOrToOne()
	{
		// A whole step along one axis, at the speed the shipped park's guests carry.
		Assert.AreEqual( 0, SpriteScript.IntervalFor( 15728, 0, hurrying: true ),
			"a hurrying person's step truncates away, which leaves their interval alone" );

		Assert.AreEqual( 1, SpriteScript.IntervalFor( 15728, 0, hurrying: false ),
			"and doubled it reaches one" );

		Assert.AreEqual( 0, SpriteScript.IntervalFor( 0, 0, hurrying: false ),
			"standing still asks for no change at all" );
	}

	/// <summary>
	/// <b>The cap at 250 cannot be reached by a walk at all, and that is a finding rather than an omission.</b>
	///
	/// <para>
	/// The original squares both components as 32-bit integers. To want an interval past 250 a person would
	/// have to move far enough that the square came to about ten million million - some five thousand times
	/// what an <c>int</c> holds - so the arithmetic wraps long before the clamp could ever fire. That is a
	/// third independent route to the same conclusion as <see cref="AWalkingStepTruncatesToNothingOrToOne"/>:
	/// this chain is a doubling, not a rate. The clamp is reproduced because the original has it, not because
	/// anything reaches it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void NoStepAWalkCanTakeEverReachesTheCap()
	{
		// The largest step whose square still fits in an int - already far past the single cell a step is
		// allowed to cross.
		var biggest = SpriteScript.IntervalFor( 46340, 0, hurrying: false );

		Assert.IsTrue( biggest is > 0 and < 10,
			$"even an impossible step asks for only {biggest}, nowhere near {SpriteScript.LongestInterval}" );

		Assert.IsTrue( SpriteScript.IntervalFor( -15728, 0, hurrying: false ) > 0,
			"a step the other way is still a distance" );
	}

	/// <summary>
	/// <b>A sprite says whether it actually took a turn.</b> The original keeps this flag to know what needs
	/// redrawing; here only this test reads it, to tell "the script is running" apart from "the script
	/// has not come due yet", which are indistinguishable from a frame number alone.
	/// </summary>
	[TestMethod]
	public void ASpriteReportsWhetherItTookATurn()
	{
		var sprite = Fresh();

		sprite.Start( SpriteScript.Walking );
		sprite.Interval = SpriteScript.DefaultInterval;
		sprite.ScheduleFrom( 0 );

		Assert.IsFalse( sprite.Step( 62 ), "not due - the deadline is met, not passed" );
		Assert.IsTrue( sprite.Step( 124 ), "and due on the turn after" );
		Assert.IsTrue( sprite.Shown );
	}
}
