using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The instructions a ride script starts its particles and sounds with: <c>ADDOBJ</c>, <c>EVENT</c> and
/// <c>KILLOBJ</c>. Between them they are 1,406 of the corpus's 11,913 instructions, and <c>ADDOBJ</c>
/// alone is the first thing to block 79 of the 308 shipped scripts.
///
/// <para>
/// <b>Four of these guard against readings that would look right and be wrong.</b> A <c>KILLOBJ</c> that
/// stopped at the first match would leave every later duplicate of a tag playing, and the instruction's
/// shape gives no hint of it. A fourth operand read as a duration rather than a tag would put plausible
/// numbers in the right field and kill nothing. An <c>EVENT</c> that kept a record would make its effects
/// killable, which the engine's discarded return value says they are not. And an effect id checked
/// against the range the corpus happens to use would reject ids the interpreter is written to remap.
/// </para>
/// </summary>
[TestClass]
public class RideScriptEffectTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A literal - tag 0x00, and what the engine sign-extends from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>
	/// A whole .RSE file to the format in <see cref="RideScriptFile"/>. The same builder as
	/// <see cref="RideScriptAnimationTests"/> keeps, written out again rather than shared so that neither
	/// file has to move for the other - and carrying the variable-name tail for the reason stated there.
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

		// The variable-name tail, which every real .RSE carries. A file that declares variables and names
		// none of them sends the reader down a path that logs, and the logger exists only once some other
		// test class has built one - so without this the class passes in a full run and throws alone.
		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	private static RideScript WithEffects( params int[] body )
		=> new RideScript( Build( 1, 50, body ) ) { Effects = new RideEffects() };

	/// <summary>
	/// <c>ADDOBJ</c> keeps a record of what it started, holding the type, the node, the effect and the
	/// tag the engine puts in its 28-byte node.
	/// </summary>
	[TestMethod]
	public void StartingAnObjectKeepsARecordOfIt()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		var effects = script.Effects!;

		Assert.AreEqual( 1, effects.Count, "nothing was recorded" );
		Assert.AreEqual( 1, effects.Records[0].Type );
		Assert.AreEqual( -1, effects.Records[0].Node, "the commonest node in the corpus is the origin" );
		Assert.AreEqual( 16, effects.Records[0].Effect );
		Assert.AreEqual( 10, effects.Records[0].Tag );
		Assert.IsTrue( effects.Records[0].IsParticle, "type 1 is a particle" );
		Assert.AreEqual( 0, script.NotImplemented, "nothing here reaches into a world that is missing" );
	}

	/// <summary>
	/// <b>A kill takes every record carrying the tag, not just the first.</b> The handler advances to the
	/// next node before it unlinks the one it matched and then jumps back to the same test, so the walk
	/// continues. A machine that stopped at the first would leave the other two playing.
	/// </summary>
	[TestMethod]
	public void KillingByTagTakesEveryMatchRatherThanTheFirst()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.ADDOBJ ), Lit( 3 ), Lit( -1 ), Lit( 43 ), Lit( 10 ),
			Word( Opcode.ADDOBJ ), Lit( 2 ), Lit( -1 ), Lit( 22 ), Lit( 10 ),
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 70 ), Lit( 500 ),
			Word( Opcode.KILLOBJ ), Lit( 10 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		var effects = script.Effects!;

		Assert.AreEqual( 1, effects.Count, "a kill that stopped at the first match left two behind" );
		Assert.AreEqual( 500, effects.Records[0].Tag, "and it took the wrong one" );
		Assert.AreEqual( 3, effects.Stopped );
	}

	/// <summary>
	/// The tag is <c>ADDOBJ</c>'s <b>fourth</b> operand, not its third. Reading the third would look
	/// entirely plausible - it is the effect id, a small number - and every kill in the game would then
	/// match nothing.
	/// </summary>
	[TestMethod]
	public void TheTagIsTheFourthOperandRatherThanTheThird()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.KILLOBJ ), Lit( 16 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.Effects!.Count, "the effect id was treated as the tag" );

		script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.KILLOBJ ), Lit( 10 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.Effects!.Count, "the fourth operand did not reach the record" );
	}

	/// <summary>
	/// <c>EVENT</c> keeps nothing, so nothing can stop what it starts. The handler calls the same worker
	/// <c>ADDOBJ</c> does and throws the handle away.
	/// </summary>
	[TestMethod]
	public void AnEventLeavesNoRecordSoNothingCanStopIt()
	{
		var script = WithEffects(
			Word( Opcode.EVENT ), Lit( 3 ), Lit( -1 ), Lit( 43 ),
			Word( Opcode.KILLOBJ ), Lit( 43 ),
			Word( Opcode.KILLOBJ ), Lit( 3 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		var effects = script.Effects!;

		Assert.AreEqual( 1, effects.Started, "the event never started anything" );
		Assert.AreEqual( 0, effects.Count, "an event is not recorded" );
		Assert.AreEqual( 0, effects.Stopped, "and so there was nothing for either kill to take" );
	}

	/// <summary>
	/// A type outside the engine's ten starts nothing and leaves nothing. Its guard is unsigned after a
	/// <c>DEC</c>, so nought and negatives take the same path as eleven.
	/// </summary>
	[TestMethod]
	public void AnObjectTypeTheEngineDoesNotHaveStartsNothing()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 11 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.ADDOBJ ), Lit( 0 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.EVENT ), Lit( -1 ), Lit( -1 ), Lit( 43 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		var effects = script.Effects!;

		Assert.AreEqual( 0, effects.Count, "a type the engine rejects left a record behind" );
		Assert.AreEqual( 0, effects.Started );
		Assert.AreEqual( 3, effects.Unknown, "and all three should have been counted as unknown" );
	}

	/// <summary>
	/// <b>An effect id is not checked against anything.</b> No shipped script names a particle above 95,
	/// but that is a fact about the shipped scripts: the engine tests the id for bit 15 and remaps it when
	/// it is set, so an implementation that bounded ids by what the corpus uses would reject values the
	/// interpreter is written to handle.
	/// </summary>
	[TestMethod]
	public void AnEffectIdIsCarriedRatherThanChecked()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 20000 ), Lit( 1 ),
			Word( Opcode.ADDOBJ ), Lit( 3 ), Lit( -1 ), Lit( 220 ), Lit( 2 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		var effects = script.Effects!;

		Assert.AreEqual( 2, effects.Count, "an id outside the corpus's range was refused" );
		Assert.AreEqual( 220, effects.Records[0].Effect, "the sample index is carried as it stands" );
		Assert.IsFalse( effects.Records[0].IsParticle, "type 3 is a sound" );
	}

	/// <summary>
	/// A kill naming a tag nothing carries leaves the list alone. The corpus does this for real: two tags
	/// are killed that no <c>ADDOBJ</c> anywhere creates.
	/// </summary>
	[TestMethod]
	public void AKillThatMatchesNothingLeavesTheRestAlone()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.KILLOBJ ), Lit( 4 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.Effects!.Count, "an unmatched kill took something anyway" );
		Assert.AreEqual( 0, script.Effects!.Stopped );
	}

	/// <summary>
	/// The operands are resolved like every other value operand: a variable is read, a literal is
	/// sign-extended from sixteen bits. 23 shipped <c>EVENT</c>s name a variable for their node.
	/// </summary>
	[TestMethod]
	public void TheOperandsAreResolvedLikeAnyOtherValue()
	{
		var script = WithEffects(
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.ADDOBJ ), Lit( 1 ), Var( 0 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 7, script.Effects!.Records[0].Node, "the variable was not read" );
	}

	/// <summary>
	/// With nowhere to start them, these are counted rather than guessed - the same answer
	/// <c>COAST</c> gives a script with no ride.
	/// </summary>
	[TestMethod]
	public void WithNowhereToStartThemTheseAreCountedRatherThanGuessed()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.EVENT ), Lit( 3 ), Lit( -1 ), Lit( 43 ),
			Word( Opcode.KILLOBJ ), Lit( 10 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.IsNull( script.Effects );
		Assert.AreEqual( 3, script.NotImplemented, "all three should have been counted" );
		Assert.AreEqual( 7, script.Variables[0], "and stepped over rather than stopping the script" );
	}

	/// <summary>
	/// <c>FADEOBJ</c> and <c>SETOBJPARAM</c> are deliberately still counted. Between them they are 133
	/// instructions and they complete no further script - 165 of 308 either way - and <c>FADEOBJ</c>
	/// differs from <c>KILLOBJ</c> only in stopping a sound gently, which nothing here can yet hear.
	/// This test is here so that stays a decision rather than a drift.
	/// </summary>
	[TestMethod]
	public void FadingAndSettingParametersAreStillCountedRatherThanGuessed()
	{
		var script = WithEffects(
			Word( Opcode.ADDOBJ ), Lit( 1 ), Lit( -1 ), Lit( 16 ), Lit( 10 ),
			Word( Opcode.FADEOBJ ), Lit( 10 ),
			Word( Opcode.SETOBJPARAM ), Lit( 10 ), Lit( 1 ), Lit( 2 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 2, script.NotImplemented, "counted" );
		Assert.AreEqual( 1, script.Effects!.Count, "and the record a fade would have taken is still there" );
	}
}
