using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The animation instructions a ride script blocks on: <c>FLUSHANIM</c>, <c>TRIGANIM</c>,
/// <c>WAITANIM</c>, <c>LOOPANIM</c> and <c>WAIT4ANIM</c>. Between them they are 1,016 of the corpus's
/// 11,913 instructions, and <c>WAITANIM</c> alone is the first thing to block 183 of the 308 shipped
/// scripts.
///
/// <para>
/// <b>These assert what the engine does for a script with no model</b>, which is every script here and
/// is a path the engine defines rather than one it leaves open: each handler tests the model handle at
/// <c>+0xc8</c> first and substitutes nought for the length it would otherwise have asked for.
/// </para>
///
/// <para>
/// Three of these guard against readings that would look right and be wrong. A <c>WAIT4ANIM</c> that
/// waits when nothing was triggered would park 74 shipped scripts for ever. A <c>WAITANIM</c> that
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
	/// length lands in the result register and the write is stepped over - the same idiom as
	/// <c>COAST 2 0</c> and <c>GETTIMER</c>.
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
	/// the 74 scripts that use the instruction.
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
	/// <c>TRIGWAITANIM</c> is deliberately still counted rather than guessed. It is the one instruction
	/// in the family that rewinds itself and walks a channel cursor (<c>+0xbc</c>) across turns, which
	/// has not been read.
	///
	/// <para>
	/// <b>The reason it was left out has since expired, and saying so is the point of this note.</b> When
	/// the rest of the family landed it completed no further script - 109 of 308 either way - which made
	/// 133 instructions of reach a bad trade. The effect opcodes changed that: with those in, it completes
	/// <b>11</b> and leads every remaining single-opcode candidate. This test still pins the current
	/// behaviour, but it is no longer evidence that leaving it out is right.
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
