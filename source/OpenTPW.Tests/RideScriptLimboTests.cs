using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// Limbo: where a shop or a toilet keeps a guest while they are inside it - <c>LIMBO</c>,
/// <c>UNLIMBO</c>, <c>FORCEUNLIMBO</c>, <c>INLIMBO</c> and <c>LIMBOSPACE</c>.
///
/// <para>
/// <b>These need no world, and that is the point of the family.</b> The slots are part of the script's
/// own frame - the loader sizes them from a header field and the teardown frees them beside the
/// variables and the stack - so unlike the families that reach guests, scenery or sound, every one of
/// these five can be exercised for real rather than counted. Exactly 24 of the 308 shipped scripts
/// declare slots, all of them ten, and they are exactly the 24 that use a limbo instruction.
/// </para>
///
/// <para>
/// Three of these guard readings that would look right and be wrong. <c>LIMBO</c>'s second operand is
/// <b>seconds</b>, not milliseconds, so a machine that added it to the clock unscaled would let
/// everyone out a thousand times too early. <c>LIMBO</c> writes to <b>neither</b> of its operands,
/// where every other instruction in the family answers into one. And <c>FORCEUNLIMBO</c> refuses a
/// destination that is not a variable <b>before</b> it does anything at all, which is the one place in
/// the family where the order of the engine's tests is visible.
/// </para>
/// </summary>
[TestClass]
public class RideScriptLimboTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A literal - tag 0x00, and what the engine sign-extends from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>
	/// A whole .RSE file, as <see cref="RideScriptClockTests"/> and <see cref="RideScriptAnimationTests"/>
	/// build one - except that this writes the <b>limbo records</b> field where those write nought,
	/// because that field is what sizes the slot array and is the whole subject here.
	/// </summary>
	private static RideScriptFile Build( int variableCount, int limboRecords, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 8 );              // stack size
		writer.Write( 50 );             // time slice
		writer.Write( limboRecords );   // limbo records - the header field that sizes the slots
		writer.Write( 0 );              // bounce records
		writer.Write( 0 );              // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );              // string blob length

		// The variable-name tail every real .RSE carries. A file that declares variables and names none
		// of them sends the reader down a path that logs, and the logger exists only once some other
		// class has built one - so leaving this out passes in a full run and throws when this class is
		// run on its own.
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
	/// The header field is read at all. Nothing else in this class means anything if the count does not
	/// come out of the file.
	/// </summary>
	[TestMethod]
	public void TheNumberOfSlotsComesOutOfTheFileHeader()
	{
		Assert.AreEqual( 10, Build( 1, 10, Word( Opcode.END ) ).LimboCapacity );
		Assert.AreEqual( 0, Build( 1, 0, Word( Opcode.END ) ).LimboCapacity );
	}

	/// <summary>
	/// Holding someone takes a slot and answers that there was room. The 1 is the engine's own, written
	/// to the result register on the way out of a successful <c>LIMBO</c>.
	/// </summary>
	[TestMethod]
	public void HoldingSomeoneTakesASlotAndSaysThereWasRoom()
	{
		var script = new RideScript( Build( 1, 10,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.Result, "the engine answers 1 when it found a slot" );
		Assert.AreEqual( 1, script.InLimbo );
		Assert.AreEqual( 9, script.LimboSpace );
	}

	/// <summary>
	/// A script whose header declares no slots can hold nobody, which is 284 of the 308 shipped scripts.
	/// The engine tests the count before it looks at anything else and leaves with nought.
	/// </summary>
	[TestMethod]
	public void AScriptWithNoSlotsInItsHeaderCanHoldNobody()
	{
		var script = new RideScript( Build( 1, 0,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.Result, "there was nowhere to put anyone" );
		Assert.AreEqual( 0, script.InLimbo );
		Assert.AreEqual( 0, script.LimboSpace );
	}

	/// <summary>
	/// <b>The duration is in seconds.</b> The engine multiplies the operand by a thousand before adding
	/// it to the clock - three chained <c>LEA</c>s coming to 125, then a scale of 8 - so the twenty
	/// shipped <c>LIMBO $0 5</c> instructions hold someone for five seconds. A machine that added the
	/// operand unscaled would release everyone a thousand times too early, and every script would still
	/// run, which is exactly the kind of wrong that does not announce itself.
	/// </summary>
	[TestMethod]
	public void TheDurationIsInSecondsRatherThanMilliseconds()
	{
		var script = new RideScript( Build( 2, 10,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.UNLIMBO ), Var( 0 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.UNLIMBO ), Var( 1 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );
		script.Turn( 4999f );

		Assert.AreEqual( 0, script.Variables[0], "nobody is due one millisecond before five seconds" );
		Assert.AreEqual( 1, script.InLimbo, "and they are still being held" );

		script.Turn( 5001f );

		Assert.AreEqual( 7, script.Variables[1], "and out they come once five seconds have passed" );
		Assert.AreEqual( 0, script.InLimbo );
	}

	/// <summary>
	/// <c>LIMBO</c> writes to neither operand. Both are read - the first for whoever is being held, the
	/// second for how long - and the handler returns without testing a destination tag at all, so the
	/// answer exists only in the result register.
	/// </summary>
	[TestMethod]
	public void HoldingSomeoneWritesNothingBackIntoItsOperands()
	{
		var script = new RideScript( Build( 2, 10,
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.COPY ), Var( 1 ), Lit( 5 ),
			Word( Opcode.LIMBO ), Var( 0 ), Var( 1 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.Result, "it found room" );
		Assert.AreEqual( 7, script.Variables[0], "the one being held is an input, not a destination" );
		Assert.AreEqual( 5, script.Variables[1], "and so is the duration" );
	}

	/// <summary>
	/// Asking for someone when nobody is due answers nought, and nought is how every shipped script
	/// tests it - the <c>BRANCH_Z</c> that follows is the whole point of the answer.
	/// </summary>
	[TestMethod]
	public void WhenNobodyIsDueNothingComesBack()
	{
		var script = new RideScript( Build( 1, 10,
			Word( Opcode.UNLIMBO ), Var( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.Variables[0] );
		Assert.AreEqual( 0, script.Result, "nought reaches the register too, which is what the branch reads" );
	}

	/// <summary>
	/// Forcing someone out does not wait for their time. It is the same walk as <c>UNLIMBO</c> with the
	/// clock test taken out, and the shipped scripts use it to empty themselves when they shut.
	/// </summary>
	[TestMethod]
	public void ForcingSomeoneOutDoesNotWaitForTheirTimeToBeUp()
	{
		var script = new RideScript( Build( 1, 10,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.FORCEUNLIMBO ), Var( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0], "out on the same clock reading that put them in" );
		Assert.AreEqual( 0, script.InLimbo );
	}

	/// <summary>
	/// <c>FORCEUNLIMBO</c> with a literal where a variable belongs does nothing whatsoever - it does not
	/// even reach the result register, because the tag test comes first and the handler leaves through
	/// NOP's own exit. All 23 shipped uses name a variable, so this path never fires in the corpus; it
	/// is here because it is the one place the order of the engine's tests can be seen.
	/// </summary>
	[TestMethod]
	public void ForcingWithoutSomewhereToPutThemDoesNothingAtAll()
	{
		var script = new RideScript( Build( 1, 10,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.FORCEUNLIMBO ), Lit( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.InLimbo, "nobody was let out" );
		Assert.AreEqual( 1, script.IgnoredWrites, "and the instruction was stepped over, not refused" );
		Assert.AreEqual( 1, script.Result, "the register still holds what the LIMBO before it put there" );
	}

	/// <summary>
	/// How many are held and how much room is left are two different questions, and the engine answers
	/// them from two different fields - the count it keeps, and that count against the size.
	/// </summary>
	[TestMethod]
	public void HowManyAreHeldAndHowMuchRoomIsLeftAreBothAnswered()
	{
		var script = new RideScript( Build( 3, 10,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.LIMBO ), Lit( 8 ), Lit( 5 ),
			Word( Opcode.INLIMBO ), Var( 0 ),
			Word( Opcode.LIMBOSPACE ), Var( 1 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 2, script.Variables[0], "two are being held" );
		Assert.AreEqual( 8, script.Variables[1], "and eight of the ten slots are free" );
	}

	/// <summary>
	/// All 24 shipped <c>LIMBOSPACE</c> instructions write a literal 0 where a destination would go, so
	/// the answer lands in the result register and the write is stepped over - the same idiom as
	/// <c>COAST 2 0</c> and <c>GETTIMER</c>. A machine that refused the instruction instead would break
	/// the branch that every one of those 24 depends on.
	/// </summary>
	[TestMethod]
	public void AskingForRoomWithNowhereToPutTheAnswerStillLeavesItInTheResult()
	{
		var script = new RideScript( Build( 1, 10,
			Word( Opcode.LIMBOSPACE ), Lit( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 10, script.Result, "the result register carries it" );
		Assert.AreEqual( 1, script.IgnoredWrites, "and the write was stepped over rather than refused" );
	}

	/// <summary>
	/// When every slot is taken the next one is turned away. The engine has no separate "full" test: it
	/// simply fails to find a free slot and falls out of the loop with nought.
	/// </summary>
	[TestMethod]
	public void WhenEverySlotIsTakenTheNextIsTurnedAway()
	{
		var script = new RideScript( Build( 1, 2,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 5 ),
			Word( Opcode.LIMBO ), Lit( 8 ), Lit( 5 ),
			Word( Opcode.LIMBO ), Lit( 9 ), Lit( 5 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.Result, "the third found nowhere to go" );
		Assert.AreEqual( 2, script.InLimbo, "and nobody was displaced to make room" );
		Assert.AreEqual( 0, script.LimboSpace );
	}

	/// <summary>
	/// A slot that has been emptied is filled again before a later one, because the engine scans from
	/// the start every time rather than keeping a cursor.
	///
	/// <para>
	/// The check turns on which of two people due at the same moment comes back first. Both are held
	/// for a second; the first is forced out, freeing slot nought; a third goes in and must take that
	/// slot rather than a later one - so once both are due, the walk from the start reaches the third
	/// before the second. Were the third appended instead, the second would come back first.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnEmptiedSlotIsFilledBeforeALaterOne()
	{
		var script = new RideScript( Build( 2, 2,
			Word( Opcode.LIMBO ), Lit( 7 ), Lit( 1 ),
			Word( Opcode.LIMBO ), Lit( 8 ), Lit( 1 ),
			Word( Opcode.FORCEUNLIMBO ), Var( 0 ),
			Word( Opcode.LIMBO ), Lit( 9 ), Lit( 1 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.UNLIMBO ), Var( 1 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Variables[0], "the first one out is the one in the first slot" );

		script.Turn( 2000f );

		Assert.AreEqual( 9, script.Variables[1], "the freed slot was filled again before the later one" );
	}

	/// <summary>
	/// <b>A deliberate reproduction of an engine defect, named rather than quietly fixed.</b> Holding
	/// nobody - a handle of nought, which is what an unset variable holds - still counts as holding
	/// someone: the engine writes the nought into the slot and increments its tally, but a slot with a
	/// nought in it is exactly what it calls free. So the tally and the slots disagree from then on.
	///
	/// <para>
	/// No shipped script can reach this. All 24 guard the instruction with a test of the variable that
	/// would hold the guest, so a <c>LIMBO</c> is never reached with nought in it. It is reproduced
	/// because the alternative is to invent a guard the engine does not have, and pinned here so that
	/// the behaviour is a decision on the record rather than an accident.
	/// </para>
	/// </summary>
	[TestMethod]
	public void HoldingNobodyStillCountsAsHoldingSomeoneWhichIsTheEnginesOwnDefect()
	{
		var script = new RideScript( Build( 1, 10,
			Word( Opcode.LIMBO ), Lit( 0 ), Lit( 5 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.Result, "the engine says it found room, and it did" );
		Assert.AreEqual( 1, script.InLimbo, "and counts it" );

		var after = new RideScript( Build( 1, 10,
			Word( Opcode.LIMBO ), Lit( 0 ), Lit( 5 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.UNLIMBO ), Var( 0 ),
			Word( Opcode.END ) ) );

		after.Turn( 0f );
		after.Turn( 100000f );

		Assert.AreEqual( 0, after.Variables[0], "but nobody can ever be found again, because the slot reads as free" );
	}
}
