using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// What the animation instructions answer once a script can see its thing's model - the difference
/// between the 300ms floor a model-less script gets and the real length of a shipped clip.
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// <see cref="RideScriptAnimationTests"/> is deliberately kept free of that, because the model-less
/// answers are the engine's own and should be provable without owning the game.
/// </para>
///
/// <para>
/// <b>Three answers that look alike and are not.</b> No model at all answers the 300 floor, because the
/// engine does the arithmetic on nought. A model that carries no such role answers 700, because the
/// engine substitutes a flat second before subtracting. And a model that does carry it answers the clip's
/// own declared length less 300. A machine that collapsed any two of those would look right on the
/// scripts that never got a model and be wrong on every one that did.
/// </para>
/// </summary>
[TestClass]
public class RideScriptModelTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A literal - tag 0x00, sign-extended from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>
	/// A whole .RSE file, the same builder the other families keep - written out again rather than shared
	/// so that neither file has to move for the other.
	/// </summary>
	private static RideScriptFile Build( int variableCount, int timeSlice, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 8 );          // stack size
		writer.Write( timeSlice );
		writer.Write( 0 );          // limbo records
		writer.Write( 0 );          // bounce records
		writer.Write( 0 );          // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );          // string blob length

		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>A script that triggers one role and entry and leaves the length in variable 0.</summary>
	private static RideScript Triggering( int role, int entry )
		=> new( Build( 1, 50,
			Word( Opcode.TRIGANIM ), Lit( role ), Lit( entry ), Var( 0 ),
			Word( Opcode.END ) ) );

	/// <summary>
	/// A ride's script is told the real length of the clip it started. The space ferry's first clip
	/// declares 600 frames, which at the engine's own float is a millisecond under twenty seconds - it
	/// multiplies by 33.33333206176758 and <b>truncates</b>, so 19999 rather than the 20000 an exact
	/// 1000/30 would give. The instruction answers that less the 300 it always takes off.
	///
	/// <para>
	/// <b>This is the clip <see cref="AnimationFile.TryLoad"/> refuses</b> - it carries no morph or
	/// rotation track at all - so a machine loading roles the fussy way would answer 700 here and never
	/// know it had dropped the longest animation in the game.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AScriptIsToldTheRealLengthOfTheClipItStarted()
	{
		var script = Triggering( 5, 0 );

		script.Animations = RideAnimations.Load( "levels/space/features/ferry", "ferry", data );

		Assert.AreEqual( 19999, script.Animations.DurationMilliseconds( 5, 0 ), "the clip's own declared length" );

		script.Turn( 0f );

		Assert.AreEqual( 19699, script.Variables[0], "the length the engine answers, less its 300" );
		Assert.IsTrue( script.WaitingForAnimation, "and the deadline WAIT4ANIM waits on was armed" );
	}

	/// <summary>
	/// A role the model does not carry answers a flat second - the engine substitutes 1000 rather than a
	/// clip length, and the 300 comes off that. The space big wheel names role 7 and ships no file for it,
	/// which is one of the eight such references in the shipped scripts.
	/// </summary>
	[TestMethod]
	public void ARoleTheModelDoesNotCarryAnswersAFlatSecond()
	{
		var script = Triggering( 7, 0 );

		script.Animations = RideAnimations.Load( "levels/space/rides/spawheel", "spawheel", data );

		Assert.AreEqual( 0, script.Animations.EntryCount( 7 ), "it really does ship nothing for that role" );

		script.Turn( 0f );

		Assert.AreEqual( 700, script.Variables[0], "a flat second, less the engine's 300" );
	}

	/// <summary>
	/// <b>A model carrying no clips is not the same as no model at all</b>, and the two answers differ by
	/// 400ms. With no model the engine never asks and does its arithmetic on nought, which the floor
	/// catches at 300. With a model that has nothing, it asks, gets the flat second, and answers 700.
	/// </summary>
	[TestMethod]
	public void AModelWithNoClipsIsNotTheSameAsNoModelAtAll()
	{
		var withNothing = Triggering( 5, 0 );

		withNothing.Animations = RideAnimations.None( "nothing" );
		withNothing.Turn( 0f );

		var withNoModel = Triggering( 5, 0 );

		Assert.IsNull( withNoModel.Animations, "this one is meant to have none" );

		withNoModel.Turn( 0f );

		Assert.AreEqual( 700, withNothing.Variables[0], "a model that carries nothing" );
		Assert.AreEqual( 300, withNoModel.Variables[0], "no model at all - the engine's floor on nought" );
	}

	/// <summary>
	/// <c>WAITANIM</c> holds for the clip's real length once there is a model to ask, where with none it
	/// costs a single turn. Its floor is unsigned, so the negative a model-less script produces sails
	/// past it - but a real length does not, and the script sits there until the clip is done.
	/// </summary>
	[TestMethod]
	public void WaitingOutAnAnimationWithAModelWaitsItsRealLength()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.WAITANIM ), Lit( 5 ), Lit( 0 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) )
		{
			Animations = RideAnimations.Load( "levels/space/features/ferry", "ferry", data )
		};

		script.Turn( 0f );

		Assert.IsTrue( script.Waiting, "it should be sitting on the clip" );
		Assert.AreEqual( 0, script.Variables[0], "so nothing after it has run" );

		script.Turn( 19698f );

		Assert.AreEqual( 0, script.Variables[0], "one millisecond short of the clip's length" );

		script.Turn( 19699f );

		Assert.AreEqual( 7, script.Variables[0], "and through on the millisecond it asked for" );
		Assert.IsFalse( script.Waiting );
	}

	/// <summary>
	/// <c>TRIGWAITANIM</c> onto an idle channel triggers and walks straight through, in the one turn.
	///
	/// <para>
	/// The mark it sets is the <b>role</b> plus one, and a trigger onto an idle channel makes that role
	/// current at once - so the re-entry the first visit rewinds into matches immediately. This is the
	/// case every vehicle takes on its first animation, and why implementing a blocking instruction did
	/// not slow the ferry and the seaplane down: they block only behind a clip of a <i>different</i> role.
	/// </para>
	///
	/// <para>
	/// It still arms what <c>WAIT4ANIM</c> waits on, because it triggers exactly as <c>TRIGANIM</c> does -
	/// which is what the two instructions after it in <c>Ferry.RSE</c> rely on.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TriggerAndWaitOnAnIdleChannelGoesStraightThrough()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGWAITANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) )
		{
			Animations = RideAnimations.Load( "levels/space/features/ferry", "ferry", data )
		};

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0], "through in the same turn it rewound in" );
		Assert.AreEqual( 0, script.NotImplemented, "and nothing counted, because there was a model to ask" );
		Assert.IsTrue( script.WaitingForAnimation, "the WAIT4ANIM deadline is armed, as TRIGANIM arms it" );
	}

	/// <summary>
	/// <b>The mark is cleared when it matches, so a second <c>TRIGWAITANIM</c> triggers like the first.</b>
	/// The engine clears <c>+0xbc</c> at <c>0x5535f4</c> on the equal branch, and a machine that left it
	/// standing would sail through every later one <i>without triggering anything</i> - which on a vehicle
	/// looks exactly like arriving once and never moving again. Both of this park's other vehicle scripts
	/// run three of these in a loop, so a sticky mark would strand them on their second leg.
	///
	/// <para>
	/// <b>What this asserts is the trigger, not the fall-through, and the difference is the whole test.</b>
	/// A mark left standing still falls through - the re-entry compare matches the role it never cleared -
	/// so the instructions after it run either way and asserting those proves nothing. It was written that
	/// way first and a mutation that removed the clear did not fail it. What does fail is asking whether
	/// the second one actually started anything: the first clip is still running, so a real trigger
	/// <b>queues</b> behind it, and a skipped one leaves the channel with nothing queued at all.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheMarkIsClearedSoASecondTriggerAndWaitStillTriggers()
	{
		var script = new RideScript( Build( 2, 50,
			Word( Opcode.TRIGWAITANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.TRIGWAITANIM ), Lit( 5 ), Lit( 1 ), Lit( 0 ),
			Word( Opcode.COPY ), Var( 1 ), Lit( 9 ),
			Word( Opcode.END ) ) )
		{
			Animations = RideAnimations.Load( "levels/space/features/ferry", "ferry", data )
		};

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0], "the first one completed" );
		Assert.AreEqual( 9, script.Variables[1], "and so did the second" );
		Assert.AreEqual( 0, script.NotImplemented );

		var channel = script.Animations!.Channel( 0 );

		Assert.IsNotNull( channel, "the ferry carries a player to ask" );
		Assert.IsTrue( channel.HasQueued,
			"the second TRIGWAITANIM triggered - a mark left standing would have stepped over it" );
		Assert.AreEqual( 1, channel.DeferredSubAnim, "and what it queued is the entry it named" );
	}

	/// <summary>
	/// <b>A trigger landing on a channel that is already running queues behind it, and the answer is both
	/// lengths added together.</b> This is the number that stops being the clip's own duration the moment a
	/// park is running: the engine returns the time still to run plus the length of what was just queued,
	/// each truncated separately (<c>0x0047337b</c> and <c>0x004733cc</c>).
	///
	/// <para>
	/// Ten seconds into a twenty-second clip there are 300 frames left, which is 9999ms, and the clip being
	/// queued is the same 19999 - so 29998 rather than the 19999 an idle channel answers.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TriggeringInsideAClipQueuesBehindItAndAnswersBothLengths()
	{
		var roles = RideAnimations.Load( "levels/space/features/ferry", "ferry", data );

		Assert.AreEqual( 19999, roles.Trigger( 5, 0, 0, 1f, 0 ), "an idle channel starts at once" );

		Assert.AreEqual( 9999 + 19999, roles.Trigger( 5, 0, 0, 1f, 10000 ),
			"and a busy one answers what is left plus what was queued" );

		Assert.IsTrue( roles.Channel( 0 )!.HasQueued, "which is to say it really did queue it" );
	}

	/// <summary>
	/// <c>FLUSHANIM</c> empties the queue and <b>stops nothing</b> - the clip that is running plays on. It
	/// is the one instruction whose behaviour changes from a genuine no-op to real work the moment a model
	/// exists, because with none the handler leaves before it reaches the channel.
	/// </summary>
	[TestMethod]
	public void FlushingEmptiesTheQueueAndLeavesTheClipRunning()
	{
		var roles = RideAnimations.Load( "levels/space/features/ferry", "ferry", data );

		roles.Trigger( 5, 0, 0, 1f, 0 );
		roles.Trigger( 5, 1, 0, 1f, 10000 );

		var channel = roles.Channel( 0 )!;

		Assert.IsTrue( channel.HasQueued );

		roles.Flush();

		Assert.IsFalse( channel.HasQueued, "the queue is empty" );
		Assert.AreEqual( 5, channel.AnimID, "and the clip that was playing is still playing" );
		Assert.AreEqual( 0, channel.SubAnim );
	}

	/// <summary>
	/// <b><c>WAITANIM</c> starts the clip it waits for</b>, which is the shipped behaviour this branch
	/// restores: the handler calls the same trigger its siblings do (<c>0x00552ab0</c>) and only then
	/// arms its deadline. Three of the eleven things standing in Lost Kingdom - the Staff Room, both
	/// Security Cameras and the Litter Bin - execute no other animation instruction at all, so a
	/// <c>WAITANIM</c> that merely waited would leave them inert for ever.
	/// </summary>
	[TestMethod]
	public void WaitingOutAnAnimationIsWhatStartsIt()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.WAITANIM ), Lit( 5 ), Lit( 0 ),
			Word( Opcode.END ) ) )
		{
			Animations = RideAnimations.Load( "levels/space/features/ferry", "ferry", data )
		};

		var channel = script.Animations!.Channel( 0 )!;

		Assert.IsTrue( channel.IsIdle, "nothing is playing before the script runs" );

		script.Turn( 0f );

		Assert.AreEqual( 5, channel.AnimID, "the wait started the clip rather than only waiting for it" );
		Assert.AreEqual( 0, channel.SubAnim );
		Assert.IsTrue( script.Waiting );

		// And sitting on it must not trigger it again - the engine triggers only on the visit where its
		// deadline field is still empty, so the clip carries on from where it had reached.
		script.Turn( 5000f );

		// A script tick no longer moves a channel - the frame sweep does, which is what ParkObjects.Sweep
		// runs once the turns a frame owes have been taken. Without this the channel would still be sitting
		// on the frame its trigger left it at, because the re-entry path above never reaches a trigger.
		script.Animations!.Advance( 5000 );

		Assert.AreEqual( 150f, channel.AnimFrame, 0.01f,
			"half a clip in, rather than back at the beginning it would be if the wait retriggered" );

		// <b>This is the assertion that can actually catch a missing guard, and the frame above is not.</b>
		// A re-entry that triggered again would not restart anything - the channel is busy, so the engine's
		// own test would send the clip to the QUEUE - and the frame would read 150 either way. What a
		// re-trigger really does is leave a second copy of the clip waiting behind the first, which then
		// plays a second time when it ends. Nothing the original does can queue a clip behind itself by
		// sitting still, so an empty queue here is the property worth pinning.
		Assert.IsFalse( channel.HasQueued,
			"a wait that sits still must not quietly queue the clip it is already waiting for" );
	}

	/// <summary>
	/// <b>A script tick does not move a channel - the frame sweep does.</b> The engine advances and poses its
	/// animation players once per frame, from outside the fixed-step loop its scripts run in, so a clip that
	/// runs out part way through a long frame keeps its queued successor waiting until every tick that frame
	/// owes has been taken.
	///
	/// <para>
	/// <b>Why this needs pinning rather than being obvious.</b> This class advanced its own channels once per
	/// tick until now, which promoted the queue early: with three ticks due, a clip ending on the first had
	/// its successor running before the second tick's instructions could look at it, and no state the engine
	/// can reach looks like that. The mistake is invisible at a frame boundary, because the last tick and the
	/// sweep land on the same millisecond - <c>ParkRides</c> hands the sweep <c>Ticks * 31</c>, which is
	/// exactly the instant its last tick ran at. Part way through a long frame is the only place it shows.
	/// </para>
	///
	/// <para>
	/// The script waits rather than ending, because <see cref="RideScript.Turn"/> leaves at its first line
	/// once a script has stopped: the turns have to actually reach the point the old advance sat at, or this
	/// would pass whether or not that advance came back.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AScriptTickLeavesTheQueueAloneAndTheSweepPromotesIt()
	{
		var roles = RideAnimations.Load( "levels/space/features/ferry", "ferry", data );

		// A clip running, and a second one queued behind it.
		roles.Trigger( 5, 0, 0, 1f, 0 );
		roles.Trigger( 5, 1, 0, 1f, 0 );

		var channel = roles.Channel( 0 )!;

		Assert.IsTrue( channel.HasQueued, "the second trigger did not queue, so there is nothing to promote" );

		// 30000 rather than anything larger, because a literal is sign-extended from its low sixteen bits:
		// 60000 arrives as -5536, the deadline lands in the past, and the script falls through to END part
		// way through the turns below. The Running assertion caught exactly that.
		var script = new RideScript( Build( 0, 50,
			Word( Opcode.WAIT ), Lit( 30000 ), Word( Opcode.END ) ) )
		{
			Animations = roles
		};

		// Three ticks' worth of turns, every one of them past the first clip's 600-frame end.
		script.Turn( 21000f );
		script.Turn( 21031f );
		script.Turn( 21062f );

		Assert.IsTrue( script.Running, "the script stopped, so the turns never reached where the advance sat" );
		Assert.IsTrue( script.Waiting, "and it should still be sitting on its WAIT" );

		// This is the assertion that catches the advance coming back: it would read 631.86 rather than nought.
		Assert.AreEqual( 0f, channel.AnimFrame, 0.01f, "a script tick moved the channel" );
		Assert.AreEqual( 0, channel.SubAnim, "a script tick promoted the queued clip" );
		Assert.IsTrue( channel.HasQueued, "a script tick emptied the queue" );

		// And then the sweep, which is the only thing that promotes.
		roles.Advance( 21062 );

		Assert.AreEqual( 1, channel.SubAnim, "the sweep did not promote the queued clip" );
		Assert.IsFalse( channel.HasQueued, "the queue should be empty once it has been promoted" );
	}
}
