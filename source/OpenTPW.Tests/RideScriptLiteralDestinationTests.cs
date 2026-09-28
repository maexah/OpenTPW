using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A literal where a destination goes: the answer lands in the result register alone, and the branch
/// after it reads it there - the shapes the shipped scripts are written in (<c>docs/exe/park.md</c>,
/// "Arithmetic, the destination rule and the result register").
///
/// <para>
/// <b>Each register idiom runs the way its script does</b>: variable nought holding a mark, the register
/// primed with the opposite answer, the instruction with a literal nought for its destination, then the
/// branch that script takes on it. An instruction that skipped the register, or stored into variable
/// nought, sends the branch the wrong way or spoils the mark. <c>GETTIMER</c>'s and <c>TRIGANIM</c>'s
/// literal answers are pinned beside their families, and <c>GETANIM_CH</c>'s with a model in
/// <see cref="RideScriptChannelTests"/>; <c>TRIGWAITANIM</c> stores through the trigger <c>TRIGANIM</c>'s
/// test pins. <c>BUMP 11 0</c>, <c>MIN</c>, <c>SEC</c> and <c>WALKFLOATSTAT</c> are unbuilt.
/// </para>
/// </summary>
[TestClass]
public class RideScriptLiteralDestinationTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A branch target - tag 0x20.</summary>
	private static int Loc( int word ) => unchecked( (int)( 0x20000000u | (uint)word ) );

	/// <summary>A literal - tag 0x00.</summary>
	private static int Lit( int value ) => value;

	/// <summary>A whole .RSE file, with the limbo records the header field sizes the slots from.</summary>
	private static RideScriptFile Build( int variableCount, int limboRecords, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 0 );              // stack size
		writer.Write( 50 );             // time slice
		writer.Write( limboRecords );   // limbo records
		writer.Write( 0 );              // bounce records
		writer.Write( 0 );              // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );              // string blob length

		// The variable-name tail every real .RSE carries; without it the reader logs, and the logger
		// exists only once another class has built one.
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
	/// Variable 0 set to <see cref="Mark"/>, then <paramref name="prime"/>, then <paramref name="instruction"/>,
	/// then either nothing or <paramref name="branch"/> to a tail that writes 2 into variable 1, where falling
	/// through writes 1. Variable 0 is what the literal nought would name if it were read as a variable; 2 and
	/// 3 are scratch.
	/// </summary>
	private static RideScript Idiom( int limboRecords, int[] prime, int[] instruction, Opcode? branch )
	{
		prime = [Word( Opcode.COPY ), Var( 0 ), Lit( Mark ), .. prime];
		var head = prime.Length + instruction.Length;
		int[] tail = branch is { } taking
			? [
				Word( taking ), Loc( head + 6 ),
				Word( Opcode.COPY ), Var( 1 ), Lit( 1 ),
				Word( Opcode.END ),
				Word( Opcode.COPY ), Var( 1 ), Lit( 2 ),
				Word( Opcode.END ),
			]
			: [Word( Opcode.END )];
		var body = prime.Concat( instruction ).Concat( tail ).ToArray();

		var file = Build( 4, limboRecords, body );

		Assert.IsTrue( file.IsValid, "the hand-built script did not read - the builder is wrong, not the machine" );

		return new RideScript( file );
	}

	/// <summary>What variable 0 holds before the instruction, so that a write into it shows whatever the answer.</summary>
	private const int Mark = 99;

	/// <summary>
	/// Primes the register with <paramref name="value"/>: into scratch variable 2, then <c>TEST</c>, which writes
	/// the register itself rather than through the store every instruction here shares - so a store that
	/// lost the register cannot also lose the priming and leave a nought that happens to be the answer.
	/// </summary>
	private static int[] Prime( int value ) => [Word( Opcode.COPY ), Var( 2 ), Lit( value ), Word( Opcode.TEST ), Var( 2 )];

	/// <summary>
	/// Runs the shape twice from <paramref name="make"/>: alone, where the register must hold
	/// <paramref name="answer"/> and nothing else was written, and with <paramref name="branch"/> after it,
	/// which must go the way that answer sends it. The tail's own <c>COPY</c> writes the register, so the
	/// answer can only be read from the first run.
	/// </summary>
	private static void AssertTheBranchRead( Func<Opcode?, RideScript> make, Opcode branch, bool taken, int answer )
	{
		var alone = make( null );

		alone.Turn( 0f );

		Assert.AreEqual( answer, alone.Result, "the answer is in the result register" );
		Assert.AreEqual( Mark, alone.Variables[0], "and nothing was written where the literal points" );
		Assert.AreEqual( 1, alone.IgnoredWrites, "the write, and only it, was stepped over" );

		var branched = make( branch );

		branched.Turn( 0f );

		Assert.AreEqual( taken ? 2 : 1, branched.Variables[1], "the branch after it read that answer" );
	}

	/// <summary>
	/// <c>LIMBOSPACE 0</c> then <c>BRANCH_Z</c>, as every one of its 24 uses is written - the Steak Shop's
	/// at word 9: ten slots, nobody held, room for ten, so the branch to "full" is not taken.
	/// </summary>
	[TestMethod]
	public void RoomLeftGoesToTheBranchAlone()
	{
		AssertTheBranchRead( branch => Idiom( 10, Prime( 0 ), [Word( Opcode.LIMBOSPACE ), Lit( 0 )], branch ),
			Opcode.BRANCH_Z, taken: false, answer: 10 );
	}

	/// <summary><c>INLIMBO 0</c> then <c>BRANCH_Z</c>, each theme's arcade: nobody held, so the branch is taken.</summary>
	[TestMethod]
	public void HowManyAreHeldGoesToTheBranchAlone()
	{
		AssertTheBranchRead( branch => Idiom( 10, Prime( 5 ), [Word( Opcode.INLIMBO ), Lit( 0 )], branch ),
			Opcode.BRANCH_Z, taken: true, answer: 0 );
	}

	/// <summary>
	/// <c>RAND 0 n</c> then a branch, four times in the corpus. A bound of nought answers nought every time,
	/// which is what makes the draw testable; the shipped bounds run 1 to 10, 300 and 5000.
	/// </summary>
	[TestMethod]
	public void ADrawGoesToTheBranchAlone()
	{
		AssertTheBranchRead( branch => Idiom( 0, Prime( 5 ), [Word( Opcode.RAND ), Lit( 0 ), Lit( 0 )], branch ),
			Opcode.BRANCH_Z, taken: true, answer: 0 );
	}

	/// <summary>
	/// <c>MOD 0 VAR_CAPACITY 9</c> then <c>BRANCH_Z</c>, Lost Kingdom's tour ride at word 33: a capacity of 18
	/// divides by nine, so the branch is taken.
	/// </summary>
	[TestMethod]
	public void ARemainderGoesToTheBranchAlone()
	{
		AssertTheBranchRead( branch => Idiom( 0,
			[Word( Opcode.COPY ), Var( 3 ), Lit( 18 ), .. Prime( 5 )],
			[Word( Opcode.MOD ), Lit( 0 ), Var( 3 ), Lit( 9 )], branch ),
			Opcode.BRANCH_Z, taken: true, answer: 0 );
	}

	/// <summary><c>SUB 0 v 9</c> then <c>BRANCH_NV</c>, space's orbiter at word 128: five less nine is negative.</summary>
	[TestMethod]
	public void ADifferenceGoesToTheBranchAlone()
	{
		AssertTheBranchRead( branch => Idiom( 0,
			[Word( Opcode.COPY ), Var( 3 ), Lit( 5 ), .. Prime( 0 )],
			[Word( Opcode.SUB ), Lit( 0 ), Var( 3 ), Lit( 9 )], branch ),
			Opcode.BRANCH_NV, taken: true, answer: -4 );
	}

	/// <summary><c>GETREMOTEVAR 0 id n</c> then <c>BRANCH_Z</c>, space's zob at word 108: the other script holds 77.</summary>
	[TestMethod]
	public void AnotherScriptsVariableGoesToTheBranchAlone()
	{
		RideScript Make( Opcode? branch )
		{
			var other = new RideScript( Build( 4, 0, Word( Opcode.COPY ), Var( 3 ), Lit( 77 ), Word( Opcode.END ) ) );
			var script = Idiom( 0, Prime( 0 ), [Word( Opcode.GETREMOTEVAR ), Lit( 0 ), Lit( 2 ), Lit( 3 )], branch );

			var scheduler = new RideScriptScheduler();

			scheduler.Add( 1, script );
			scheduler.Add( 2, other );

			other.Turn( 0f );

			return script;
		}

		AssertTheBranchRead( Make, Opcode.BRANCH_Z, taken: false, answer: 77 );
	}

	/// <summary>
	/// <c>COAST 2 0</c> then <c>BRANCH_Z</c>, every one of its 12 uses, Coaster1's at word 44: a capacity of five
	/// with nobody aboard or waiting leaves room for five, so the branch is not taken.
	/// </summary>
	[TestMethod]
	public void RoomOnTheCoasterGoesToTheBranchAlone()
	{
		RideScript Make( Opcode? branch )
		{
			var script = Idiom( 0, [Word( Opcode.COAST ), Lit( 6 ), Lit( 5 ), .. Prime( 0 )],
				[Word( Opcode.COAST ), Lit( 2 ), Lit( 0 )], branch );

			script.Ride = new RideState();

			return script;
		}

		AssertTheBranchRead( Make, Opcode.BRANCH_Z, taken: false, answer: 5 );
	}

	/// <summary>
	/// Unlike the idioms above, a trigger's length has no branch to read it and needs no priming:
	/// <c>TRIGANIM_CH</c> with a literal where its length goes, 60 of its 63 shipped uses: the length stays in
	/// the register, nobody reads it, and with no model it is the engine's own 300.
	/// </summary>
	[TestMethod]
	public void AChannelTriggersLengthStaysInTheRegister()
	{
		var script = new RideScript( Build( 1, 0,
			Word( Opcode.TRIGANIM_CH ), Lit( 5 ), Lit( 0 ), Lit( 0 ), Lit( 1 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 300, script.Result, "the result register carries it" );
		Assert.AreEqual( 1, script.IgnoredWrites, "and the write was stepped over rather than refused" );
		Assert.AreEqual( 0, script.Variables[0], "nothing was written anywhere" );
	}
}
