using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The animation instructions a ride script blocks on: <c>FLUSHANIM</c>, <c>TRIGANIM</c>,
/// <c>WAITANIM</c>, <c>LOOPANIM</c> and <c>WAIT4ANIM</c>. Between them they are 1,016 of the corpus's
/// 11,913 instructions, and <c>WAITANIM</c> alone appears in 250 of the 308 shipped scripts.
///
/// <para>
/// <b>These assert what the engine does for a script with no model</b>, which is every script here and
/// is a path the engine defines rather than one it leaves open: each handler tests the model handle at
/// <c>+0xc8</c> first and substitutes nought for the length it would otherwise have asked for.
/// </para>
///
/// <para>
/// Three of these guard against readings that would look right and be wrong. A <c>WAIT4ANIM</c> that
/// waits when nothing was triggered would park 75 shipped scripts for ever. A <c>WAITANIM</c> that
/// waits the 300 its sibling answers would be wrong by a whole turn's worth of arithmetic - the two
/// handlers differ in signedness, and only one of them floors. And a <c>LOOPANIM</c> that failed to
/// clear the triggered deadline would leave a script waiting on an animation that is now looping and
/// will therefore never finish.
/// </para>
/// </summary>
[TestClass]
public class RideScriptAnimationTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A literal - tag 0x00, and what the engine sign-extends from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>
	/// A whole .RSE file to the format in <see cref="RideScriptFile"/>. The same builder as
	/// <see cref="RideScriptClockTests"/> keeps, written out again rather than shared so that neither
	/// file has to move for the other.
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

		// The variable-name tail, which every real .RSE carries. Writing it is not only fidelity: a file
		// that declares variables and names none of them sends the reader down a path that logs, and the
		// logger exists only once some other test class has built one. A blob without this tail passes
		// in a full run and throws when the class is run on its own.
		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>
	/// <c>WAITANIM</c> costs a turn and no more. Its deadline is <c>clock - 300</c>, which is already
	/// past the moment it is set - the handler reads the length-less-300 out of a qword whose high half
	/// is nought, so the negative arrives as four thousand million, converts back to itself, and then
	/// passes an <b>unsigned</b> comparison against the 300 floor that a signed one would have caught.
	/// The turn is still spent, because the engine sets the deadline without looking at it.
	/// </summary>
	[TestMethod]
	public void AnAnimationWaitCostsOneTurnRatherThanTheThreeHundredItLooksLike()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.WAITANIM ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 1000f );

		Assert.IsTrue( script.Waiting, "the turn was given up even though the deadline was already past" );
		Assert.AreEqual( 0, script.Variables[0], "so nothing after it ran" );

		script.Turn( 1000f );

		Assert.AreEqual( 7, script.Variables[0], "and the very next turn walks through, same clock and all" );
		Assert.IsFalse( script.Waiting );
	}

	/// <summary>
	/// <c>TRIGANIM</c> answers how long the animation it started runs. The engine subtracts 300 from
	/// the model's answer and floors the result at 300 with a signed comparison; with no model to ask,
	/// the sum is <c>0 - 300</c> and the floor is what comes back.
	/// </summary>
	[TestMethod]
	public void TriggeringAnAnimationAnswersItsLengthWhichWithNoModelIsTheFloor()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGANIM ), Lit( 5 ), Lit( 0 ), Var( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 300, script.Variables[0], "the engine's own floor, not a number chosen here" );
	}

	/// <summary>
	/// 64 of the 74 shipped <c>TRIGANIM</c>s write a literal where a destination would go, so the
	/// length lands in the result register and the write is stepped over. Unlike <c>COAST 2 0</c> and
	/// <c>GETTIMER</c>, no shipped script then reads it there.
	/// </summary>
	[TestMethod]
	public void TriggeringWithNowhereToPutTheLengthStillLeavesItInTheResult()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 300, script.Result, "the result register carries it" );
		Assert.AreEqual( 1, script.IgnoredWrites, "and the write was stepped over rather than refused" );
		Assert.AreEqual( 0, script.Variables[0], "nothing was written anywhere" );
	}

	/// <summary>
	/// <c>WAIT4ANIM</c> with nothing triggered does not wait. The handler's first test is whether its
	/// deadline is nought, and it leaves if it is. A machine that waited anyway would park every one of
	/// the 75 scripts that use the instruction.
	/// </summary>
	[TestMethod]
	public void WaitingForAnAnimationNobodyStartedDoesNotWaitAtAll()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.WAIT4ANIM ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0], "straight past it, in the same turn" );
		Assert.IsFalse( script.WaitingForAnimation );
		Assert.AreEqual( 0, script.NotImplemented );
	}

	/// <summary>
	/// What <c>WAIT4ANIM</c> waits on is the deadline a <c>TRIGANIM</c> armed - the engine stores it in
	/// a second field (<c>+0xa4</c>) that <c>WAIT</c> never touches, and a plain trigger arms it exactly
	/// as <c>TRIGWAITANIM</c> does.
	/// </summary>
	[TestMethod]
	public void WhatWaitingForAnAnimationWaitsOnIsTheOneThatWasTriggered()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.WAIT4ANIM ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.IsTrue( script.WaitingForAnimation, "the trigger armed it and the wait took hold" );
		Assert.AreEqual( 0, script.Variables[0] );

		script.Turn( 299f );

		Assert.AreEqual( 0, script.Variables[0], "one millisecond short of the length it answered" );

		script.Turn( 300f );

		Assert.AreEqual( 7, script.Variables[0], "and through on the tick it asked for" );
		Assert.IsFalse( script.WaitingForAnimation );
	}

	/// <summary>
	/// Setting an animation looping clears the deadline a trigger left behind. A loop never finishes,
	/// so there would be nothing for <c>WAIT4ANIM</c> to wait on - and a machine that kept the deadline
	/// would hold the script until an animation that is still running appeared to end.
	/// </summary>
	[TestMethod]
	public void SettingAnAnimationLoopingCancelsTheWaitForTheTriggeredOne()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.LOOPANIM ), Lit( 2 ), Lit( 0 ),
			Word( Opcode.WAIT4ANIM ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0], "the whole of it ran in one turn" );
		Assert.IsFalse( script.WaitingForAnimation, "the loop took the deadline away" );
	}

	/// <summary>
	/// <b>A <c>WAITANIM</c>'s first visit forgets the deadline a trigger left</b>, model or not
	/// (<c>0x00552b14</c>), so a <c>WAIT4ANIM</c> after it does not sit out an older trigger's 300ms.
	/// </summary>
	[TestMethod]
	public void AnAnimationWaitForgetsTheDeadlineATriggerLeft()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.WAITANIM ), Lit( 6 ), Lit( 0 ),
			Word( Opcode.WAIT4ANIM ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.IsFalse( script.WaitingForAnimation, "the WAITANIM's first visit cleared the trigger's deadline" );

		script.Turn( 31f );

		Assert.AreEqual( 7, script.Variables[0], "so the WAIT4ANIM passed in the WAITANIM's next turn, not at 300" );
	}

	/// <summary>
	/// <b>A <c>WAITANIM</c>'s first visit sets the looping key to one-shot</b>, model or not
	/// (<c>0x00552b1a</c>), so a <c>LOOPANIM</c> of the key last looped is not skipped after one. With no model
	/// what shows it is the loop's clear of the <c>WAIT4ANIM</c> deadline: a <c>TRIGANIM_CH</c>, which leaves
	/// the key alone, arms one, and the second <c>LOOPANIM 2, 0</c> takes it away.
	/// </summary>
	[TestMethod]
	public void AnAnimationWaitLetsTheSameLoopBeAskedForAgain()
	{
		var script = new RideScript( Build( 2, 50,
			Word( Opcode.LOOPANIM ), Lit( 2 ), Lit( 0 ),
			Word( Opcode.WAITANIM ), Lit( 6 ), Lit( 0 ),
			Word( Opcode.TRIGANIM_CH ), Lit( 1 ), Lit( 0 ), Var( 0 ), Lit( 0 ),
			Word( Opcode.LOOPANIM ), Lit( 2 ), Lit( 0 ),
			Word( Opcode.WAIT4ANIM ),
			Word( Opcode.COPY ), Var( 1 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );
		script.Turn( 31f );

		Assert.AreEqual( 300, script.Variables[0], "the TRIGANIM_CH ran and armed its 300" );
		Assert.IsFalse( script.WaitingForAnimation, "the second LOOPANIM 2, 0 ran and took the deadline away" );
		Assert.AreEqual( 7, script.Variables[1], "so the WAIT4ANIM passed at once" );
	}

	/// <summary>
	/// <c>FLUSHANIM</c> fetches the model and leaves if there is none, so with no model it is a real
	/// no-op and not an instruction this refuses - which is the difference between a script carrying on
	/// and a script counting a gap.
	/// </summary>
	[TestMethod]
	public void FlushingAnimationsIsNotAnInstructionThisRefuses()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.FLUSHANIM ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0] );
		Assert.AreEqual( 0, script.NotImplemented, "nothing here reaches into a world that is missing" );
	}

	/// <summary>
	/// <c>TRIGWAITANIM</c> with no model is counted and stepped over - the one declared deviation in
	/// <c>RideScript.TriggerAndWaitForAnimation</c>. With a model it is built: see
	/// <see cref="RideScriptModelTests"/>.
	///
	/// <para>
	/// The handler triggers exactly as <c>TRIGANIM</c> does, marks
	/// <c>+0xbc</c> with the animation id plus one, rewinds four words onto itself and returns without
	/// ending the slice; on re-entry it asks the model for channel 0 and goes on only when that answer
	/// plus one matches the mark. <b>With no model the query is skipped and the comparison is made
	/// against the raw third operand</b>, which nothing can ever change - so the instruction would park
	/// the script for ever unless operand three happened to equal operand one. Across the 133 shipped
	/// uses it never does: 132 differ outright and the last is a variable.
	/// </para>
	///
	/// <para>
	/// So reproducing the model-less path faithfully would hang <b>56</b> scripts, which is why a script
	/// with no model counts the instruction and walks past it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TriggerAndWaitIsStillCountedRatherThanGuessed()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.TRIGWAITANIM ), Lit( 5 ), Lit( 0 ), Lit( 0 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.NotImplemented, "counted" );
		Assert.AreEqual( 7, script.Variables[0], "and stepped over rather than stopping the script" );
	}
}
