using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The two stacks and the result register: <c>JSR</c>, <c>RETURN</c>, <c>PUSH</c> and <c>POP</c> at the
/// call end, <c>HUSH</c> and <c>HOP</c> at the heap end, and the instructions that leave the register
/// alone (<c>docs/exe/park.md</c>, "The two stacks" and "Arithmetic, the destination rule and the result
/// register").
///
/// <para>
/// <b>No shipped path reaches the error arms</b>: every call is one deep and every <c>HUSH</c> ride's
/// capacity fits its stack. The rest are reached - the tagged frame by the Belly Bounce, <c>HUSH</c>'s and
/// <c>WALKON</c>'s register by every walk-on ride, the restore by every load - and each register write is
/// masked by a writer before the next branch. So the scripts here are built to show each arm, and each
/// asserts what the engine's handler does there.
/// </para>
/// </summary>
[TestClass]
public class RideScriptStackTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	private static int Lit( int value ) => value;

	private static int Label( int word ) => 0x20000000 | word;

	/// <summary>What <c>JSR</c> pushes: the word after it, with the label tag the engine ORs in.</summary>
	private const int Tag = 0x20000000;

	/// <summary>
	/// A whole .RSE file with the header fields these tests vary: the stack size above all, the time
	/// slice for the one test that ends a turn early, and the walk records for <c>WALKON</c>.
	/// </summary>
	private static RideScriptFile Build( int stack, int[] body, int timeSlice = 50, int walkRecords = 0 )
	{
		const int variableCount = 3;

		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( stack );
		writer.Write( timeSlice );
		writer.Write( 0 );              // limbo records
		writer.Write( 0 );              // bounce records
		writer.Write( walkRecords );
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

	private static RideScript Run( int stack, params int[] body )
	{
		var script = new RideScript( Build( stack, body ) );

		script.Turn( 0f );

		return script;
	}

	/// <summary>
	/// <b>A <c>JSR</c> with no room parks the script and then jumps anyway</b> (<c>0x00553a07</c>, then
	/// <c>0x00553a24</c>), so the script lives. Its subroutine's <c>RETURN</c> then pops the frame of the
	/// call still open and comes back one frame too far, skipping the rest of the enclosing subroutine.
	/// </summary>
	[TestMethod]
	public void ACallWithAFullStackJumpsAnywayAndItsReturnGoesOneFrameTooFar()
	{
		var script = Run( 1,
			Word( Opcode.JSR ), Label( 6 ),          // 0: the one slot takes 2
			Word( Opcode.COPY ), Var( 1 ), Lit( 1 ), // 2: where the too-far return lands
			Word( Opcode.END ),                      // 5
			Word( Opcode.JSR ), Label( 12 ),         // 6: no room
			Word( Opcode.COPY ), Var( 2 ), Lit( 1 ), // 8: skipped
			Word( Opcode.RETURN ),                   // 11
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 12
			Word( Opcode.RETURN ) );                 // 15: pops slot 0, the outer call's

		Assert.AreEqual( 7, script.Variables[0], "the full stack's JSR still jumped" );
		Assert.AreEqual( 1, script.Variables[1], "and its RETURN came back to the outer caller" );
		Assert.AreEqual( 0, script.Variables[2], "skipping what the outer subroutine had left to run" );
		Assert.IsFalse( script.Running, "the script ends on its own END" );
	}

	/// <summary>
	/// With no stack at all the <c>JSR</c> jumps anyway too, and the subroutine's <c>RETURN</c> finds no
	/// stack and parks for good (<c>0x00553a63</c>).
	/// </summary>
	[TestMethod]
	public void ACallWithNoStackJumpsAnywayAndItsReturnEndsTheScript()
	{
		var script = Run( 0,
			Word( Opcode.JSR ), Label( 6 ),          // 0: no stack
			Word( Opcode.COPY ), Var( 1 ), Lit( 1 ), // 2
			Word( Opcode.END ),                      // 5
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 6
			Word( Opcode.RETURN ),                   // 9: parks
			Word( Opcode.COPY ), Var( 2 ), Lit( 1 ), // 10
			Word( Opcode.END ) );                    // 13

		Assert.AreEqual( 7, script.Variables[0], "the JSR jumped with nowhere to push" );
		Assert.AreEqual( 0, script.Variables[1], "so nothing came back to the caller" );
		Assert.AreEqual( 0, script.Variables[2], "and the RETURN did not carry on either" );
		Assert.IsFalse( script.Running, "the RETURN ended the script" );
	}

	/// <summary>
	/// <b>Neither <c>JSR</c> nor <c>RETURN</c> writes the result register</b>, on any arm: the branch after the
	/// call reads what the <c>COPY</c> left.
	/// </summary>
	[TestMethod]
	public void ACallAndAReturnLeaveTheResultRegisterAlone()
	{
		foreach ( var stack in new[] { 4, 0 } )
		{
			var script = Run( stack,
				Word( Opcode.COPY ), Var( 0 ), Lit( 9 ), // 0
				Word( Opcode.JSR ), Label( 9 ),          // 3: with no stack, jumps anyway
				Word( Opcode.BRANCH_NZ ), Label( 8 ),    // 5
				Word( Opcode.END ),                      // 7: a nought in the register
				Word( Opcode.END ),                      // 8: what the COPY left
				Word( Opcode.RETURN ) );                 // 9: with no stack, parks

			Assert.AreEqual( 9, script.Result, $"stack {stack}: the register still holds what the COPY left" );

			if ( stack > 0 )
				Assert.AreEqual( 9, script.Position, "and the branch after the call read it, ending on word 8" );
		}
	}

	/// <summary><b>A <c>RETURN</c> with no frame ends the script</b> (<c>0x00553a63</c>) rather than carrying on.</summary>
	[TestMethod]
	public void AReturnWithNoFrameEndsTheScript()
	{
		var script = Run( 4,
			Word( Opcode.RETURN ),                   // 0: nothing pushed
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 1
			Word( Opcode.END ) );                    // 4

		Assert.IsFalse( script.Running, "a RETURN with nothing pushed parks the script" );
		Assert.AreEqual( 0, script.Variables[0], "so nothing after it runs" );
	}

	/// <summary>
	/// <b>A <c>JSR</c> to something that is not a label pushes its return address and carries on</b>: the
	/// push comes first and the operand's tag after it, which leaves through <c>NOP</c>
	/// (<c>0x00553a1c</c>). The <c>POP</c> shows what was pushed - the next word, tagged <c>0x20000000</c>.
	/// Without room the same <c>JSR</c> stays parked.
	/// </summary>
	[TestMethod]
	public void ACallToAnOperandThatIsNotALabelPushesAndCarriesOn()
	{
		var script = Run( 4,
			Word( Opcode.JSR ), Lit( 5 ),            // 0: pushes 2, tagged, and carries on
			Word( Opcode.POP ), Var( 0 ),            // 2
			Word( Opcode.END ) );                    // 4

		Assert.AreEqual( Tag | 2, script.Variables[0], "the POP after the JSR ran, and took its return address, tagged" );
		Assert.AreEqual( Tag | 2, script.Result, "POP writes the register too" );

		var parked = Run( 0,
			Word( Opcode.JSR ), Lit( 5 ),            // 0: no stack, and nothing to jump to
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 2
			Word( Opcode.END ) );                    // 5

		Assert.IsFalse( parked.Running, "without room the non-label JSR stays parked" );
		Assert.AreEqual( 0, parked.Variables[0], "so nothing after it runs" );
	}

	/// <summary>
	/// <b>A popped word without the label tag is dropped</b> (<c>0x00553a74</c>): the pop stands, and the
	/// script carries on after the <c>RETURN</c>. The second <c>RETURN</c> proves the pop stood.
	/// </summary>
	[TestMethod]
	public void AReturnDropsAPoppedWordWithoutTheLabelTagAndCarriesOn()
	{
		var script = Run( 4,
			Word( Opcode.PUSH ), Lit( 5 ),           // 0: an untagged word where a frame would be
			Word( Opcode.RETURN ),                   // 2: pops it, and carries on
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 3
			Word( Opcode.RETURN ),                   // 6: nothing left, so it parks
			Word( Opcode.COPY ), Var( 1 ), Lit( 1 ), // 7
			Word( Opcode.END ) );                    // 10

		Assert.AreEqual( 7, script.Variables[0], "the RETURN carried on past the untagged word" );
		Assert.AreEqual( 0, script.Variables[1], "and the second RETURN found the stack empty" );
		Assert.IsFalse( script.Running );
	}

	/// <summary>
	/// <b><c>PUSH</c> writes the result register whether or not there was room</b> (<c>0x00553c89</c>,
	/// <c>0x00553cac</c>), and on no room parks the script.
	/// </summary>
	[TestMethod]
	public void APushWritesTheResultRegisterAndWithNoRoomEndsTheScript()
	{
		var pushed = Run( 4,
			Word( Opcode.PUSH ), Lit( 5 ),
			Word( Opcode.END ) );

		Assert.AreEqual( 5, pushed.Result, "PUSH leaves its value in the register" );

		var refused = Run( 0,
			Word( Opcode.PUSH ), Lit( 5 ),           // 0: no stack
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 2
			Word( Opcode.END ) );                    // 5

		Assert.AreEqual( 5, refused.Result, "a refused PUSH still writes the register" );
		Assert.AreEqual( 0, refused.Variables[0], "and ends the script" );
		Assert.IsFalse( refused.Running );
	}

	/// <summary>
	/// <b>A <c>POP</c> from an empty stack reads nought, writes it, and ends the script</b>: the error parks
	/// and then takes the store tail like any <c>POP</c> (<c>0x00553d1d</c>, <c>0x00555939</c>).
	/// </summary>
	[TestMethod]
	public void APopFromAnEmptyStackWritesNoughtAndEndsTheScript()
	{
		var script = Run( 4,
			Word( Opcode.COPY ), Var( 0 ), Lit( 9 ), // 0
			Word( Opcode.POP ), Var( 0 ),            // 3: nothing pushed
			Word( Opcode.COPY ), Var( 1 ), Lit( 7 ), // 5
			Word( Opcode.END ) );                    // 8

		Assert.AreEqual( 0, script.Variables[0], "the failed POP still writes its nought" );
		Assert.AreEqual( 0, script.Result, "into the register as well" );
		Assert.AreEqual( 0, script.Variables[1], "and then the script is over" );
		Assert.IsFalse( script.Running );
	}

	/// <summary>
	/// <b><c>HUSH</c> writes the result register</b> (<c>0x00553d95</c>). Refused, it writes nothing and
	/// the script lives: a heap error only logs (<c>0x00553dfa</c>). The branch reads the register the
	/// <c>COPY</c> left, so a refused <c>HUSH 0</c> that wrote it would send the script to the <c>END</c>.
	/// </summary>
	[TestMethod]
	public void AHushWritesTheResultRegisterAndARefusedOneWritesNothing()
	{
		var pushed = Run( 4,
			Word( Opcode.HUSH ), Lit( 5 ),
			Word( Opcode.END ) );

		Assert.AreEqual( 5, pushed.Result, "HUSH leaves its value in the register" );

		var refused = Run( 0,
			Word( Opcode.COPY ), Var( 0 ), Lit( 9 ), // 0
			Word( Opcode.HUSH ), Lit( 0 ),           // 3: no stack
			Word( Opcode.BRANCH_NZ ), Label( 8 ),    // 5
			Word( Opcode.END ),                      // 7
			Word( Opcode.COPY ), Var( 1 ), Lit( 1 ), // 8
			Word( Opcode.END ) );                    // 11

		Assert.AreEqual( 1, refused.Variables[1], "the refused HUSH left the register and the script alone" );
	}

	/// <summary>
	/// <b><c>HUSH</c> is bounded by the stack alone</b>: it never reads the call index, so it writes over
	/// an open frame. The <c>RETURN</c> then pops the value where the return address was, finds no label
	/// tag on it, and carries on rather than returning.
	/// </summary>
	[TestMethod]
	public void AHushIsBoundedByTheStackAloneAndWritesOverAFrame()
	{
		var script = Run( 2,
			Word( Opcode.JSR ), Label( 3 ),          // 0: slot 1 takes the return address
			Word( Opcode.END ),                      // 2
			Word( Opcode.HUSH ), Lit( 5 ),           // 3: slot 0
			Word( Opcode.HUSH ), Lit( 6 ),           // 5: slot 1, over the frame
			Word( Opcode.RETURN ),                   // 7: pops the 6
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 8
			Word( Opcode.END ) );                    // 11

		Assert.AreEqual( 7, script.Variables[0], "the frame was overwritten, so the RETURN carried on" );
	}

	/// <summary>
	/// <b>A <c>HOP</c> from an empty heap writes nothing</b> - no variable, no register, no park
	/// (<c>0x00553dfa</c>) - where a <c>HOP</c> with something to take writes both. The branch reads the
	/// register the <c>COPY</c> left, so an empty <c>HOP</c> that wrote a nought would send it to the
	/// <c>END</c>.
	/// </summary>
	[TestMethod]
	public void AHopFromAnEmptyHeapWritesNothing()
	{
		foreach ( var stack in new[] { 4, 0 } )
		{
			var script = Run( stack,
				Word( Opcode.COPY ), Var( 0 ), Lit( 9 ), // 0
				Word( Opcode.HOP ), Var( 0 ),            // 3: nothing hushed
				Word( Opcode.BRANCH_NZ ), Label( 8 ),    // 5
				Word( Opcode.END ),                      // 7
				Word( Opcode.COPY ), Var( 1 ), Lit( 1 ), // 8
				Word( Opcode.END ) );                    // 11

			Assert.AreEqual( 9, script.Variables[0], $"stack {stack}: the empty HOP left its destination" );
			Assert.AreEqual( 1, script.Variables[1], $"stack {stack}: and the register, and the script carried on" );
		}

		var hopped = Run( 4,
			Word( Opcode.HUSH ), Lit( 5 ),
			Word( Opcode.COPY ), Var( 1 ), Lit( 9 ),
			Word( Opcode.HOP ), Var( 0 ),
			Word( Opcode.END ) );

		Assert.AreEqual( 5, hopped.Variables[0], "a HOP with something to take writes it" );
		Assert.AreEqual( 5, hopped.Result, "and writes the register" );
	}

	/// <summary>
	/// <b><c>ADD</c> tests its destination before it touches the register</b> (<c>0x00553e72</c>), so one
	/// naming a literal leaves the register as it was and carries on.
	/// </summary>
	[TestMethod]
	public void AnAddToALiteralLeavesTheResultRegisterAlone()
	{
		var script = Run( 0,
			Word( Opcode.COPY ), Var( 0 ), Lit( 9 ), // 0
			Word( Opcode.ADD ), Lit( 0 ), Lit( 5 ),  // 3
			Word( Opcode.COPY ), Var( 1 ), Var( 0 ), // 6: reads 9, so the register cannot tell
			Word( Opcode.END ) );                    // 9

		Assert.AreEqual( 9, script.Variables[1], "the script carried on past the ADD" );
		Assert.AreEqual( 1, script.IgnoredWrites, "the ADD was skipped" );

		var alone = Run( 0,
			Word( Opcode.COPY ), Var( 0 ), Lit( 9 ),
			Word( Opcode.ADD ), Lit( 0 ), Lit( 5 ),
			Word( Opcode.END ) );

		Assert.AreEqual( 9, alone.Result, "the register still holds what the COPY left" );
	}

	/// <summary>
	/// <b>A <c>COPY</c> to a literal ends the script</b>: it tests its destination before it fetches its
	/// source (<c>0x00551da3</c>), so it leaves the position on its second operand, and the next dispatch
	/// refuses that word and parks (<c>0x005567a3</c>). The register is not touched. The park is at the
	/// next dispatch, so a <c>COPY</c> that spends a turn's last unit leaves the script to the next turn.
	/// </summary>
	[TestMethod]
	public void ACopyToALiteralEndsTheScriptAtTheNextDispatch()
	{
		var script = Run( 0,
			Word( Opcode.COPY ), Var( 1 ), Lit( 9 ), // 0
			Word( Opcode.COPY ), Lit( 0 ), Lit( 5 ), // 3
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 6
			Word( Opcode.END ) );                    // 9

		Assert.IsFalse( script.Running, "the literal-destination COPY ended the script" );
		Assert.AreEqual( 0, script.Variables[0], "so nothing after it ran" );
		Assert.AreEqual( 9, script.Result, "and it never wrote the register" );

		var sliced = new RideScript( Build( 0,
			[
				Word( Opcode.COPY ), Var( 1 ), Lit( 9 ), // 0
				Word( Opcode.COPY ), Lit( 0 ), Lit( 5 ), // 3: spends the turn's last unit
				Word( Opcode.END )                       // 6
			], timeSlice: 2 ) );

		sliced.Turn( 0f );

		Assert.IsTrue( sliced.Running, "the park waits for the next dispatch" );
		Assert.AreEqual( 5, sliced.Position, "which is the COPY's second operand" );

		sliced.Turn( 1f );

		Assert.IsFalse( sliced.Running, "and the next turn's first dispatch refuses it" );
	}

	/// <summary><b><c>WALKON</c> never writes the result register</b>: nothing after its call does (<c>0x00555afe</c>).</summary>
	[TestMethod]
	public void AWalkOnLeavesTheResultRegisterAlone()
	{
		var script = new RideScript( Build( 0,
			[
				Word( Opcode.COPY ), Var( 0 ), Lit( 9 ),
				Word( Opcode.WALKON ), Lit( 1 ), 1, 1, 1, 1, 1, 1,
				Word( Opcode.END )
			], walkRecords: 1 ) );

		script.Turn( 0f );

		Assert.AreEqual( 9, script.Result, "the register still holds what the COPY left" );
		Assert.AreEqual( 0, script.NotImplemented );
	}

	/// <summary>
	/// <b>A restore puts back the array, both indices and the register</b>, as the save reader does. The
	/// slots above the call index are frames, stored tagged, so a <c>RETURN</c> takes one as the engine
	/// wrote it; the heap comes back with its values; and the array's length is the saved block's, not the
	/// script's declared stack.
	/// </summary>
	[TestMethod]
	public void ARestoredStackReturnsHopsAndBranchesAsItWasSaved()
	{
		var frame = new RideScript( Build( 3,
		[
			Word( Opcode.RETURN ),                   // 0: the frame in slot 2
			Word( Opcode.COPY ), Var( 0 ), Lit( 1 ), // 1
			Word( Opcode.COPY ), Var( 1 ), Lit( 1 ), // 4
			Word( Opcode.END )                       // 7
		] ) );

		frame.RestoreStacks( [0, 0, Tag | 4], callIndex: 1, heapIndex: 0, result: 0 );
		frame.Turn( 0f );

		Assert.AreEqual( 0, frame.Variables[0], "the RETURN took the saved frame" );
		Assert.AreEqual( 1, frame.Variables[1], "and came back where it said" );

		var heap = new RideScript( Build( 3,
		[
			Word( Opcode.BRANCH_Z ), Label( 7 ),     // 0: the saved register
			Word( Opcode.HOP ), Var( 0 ),            // 2
			Word( Opcode.HOP ), Var( 1 ),            // 4
			Word( Opcode.END ),                      // 6
			Word( Opcode.END )                       // 7
		] ) );

		heap.RestoreStacks( [7, 9, Tag | 24], callIndex: 2, heapIndex: 2, result: 3 );
		heap.Turn( 0f );

		Assert.AreEqual( 9, heap.Variables[0], "the saved heap's top" );
		Assert.AreEqual( 7, heap.Variables[1], "and the value under it" );

		var resized = new RideScript( Build( 0,
		[
			Word( Opcode.HUSH ), Lit( 5 ),
			Word( Opcode.HOP ), Var( 0 ),
			Word( Opcode.END )
		] ) );

		resized.RestoreStacks( [0, 0], callIndex: 1, heapIndex: 0, result: 0 );
		resized.Turn( 0f );

		Assert.AreEqual( 5, resized.Variables[0], "the saved block is the stack, though the file declares none" );
	}

	/// <summary>
	/// <b>A restored index past the stack meets the handlers' own bounds</b>, since neither the save reader
	/// nor the restore checks it. <c>JSR</c> and <c>RETURN</c> bound the call index both ways; <c>HOP</c>
	/// refuses a call index past the stack's size, the engine's own second test, and a heap index past the
	/// stack, which the engine would read past the array.
	/// </summary>
	[TestMethod]
	public void ARestoredIndexPastTheStackIsRefusedRatherThanReadPast()
	{
		int[] Hop() =>
		[
			Word( Opcode.COPY ), Var( 0 ), Lit( 9 ), // 0
			Word( Opcode.HOP ), Var( 0 ),            // 3
			Word( Opcode.END )                       // 5
		];

		var calls = new RideScript( Build( 0, Hop() ) );

		calls.RestoreStacks( [5, 6], callIndex: 3, heapIndex: 1, result: 0 );
		calls.Turn( 0f );

		Assert.AreEqual( 9, calls.Variables[0], "a call index past the stack's size refuses the HOP" );

		var heap = new RideScript( Build( 0, Hop() ) );

		heap.RestoreStacks( [5, 6], callIndex: 1, heapIndex: 5, result: 0 );
		heap.Turn( 0f );

		Assert.AreEqual( 9, heap.Variables[0], "and so does a heap index past it" );

		var jsr = new RideScript( Build( 0,
		[
			Word( Opcode.JSR ), Label( 3 ),          // 0: the index is past the top slot
			Word( Opcode.END ),                      // 2
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 3
			Word( Opcode.END )                       // 6
		] ) );

		jsr.RestoreStacks( [5, 6], callIndex: 2, heapIndex: 0, result: 0 );
		jsr.Turn( 0f );

		Assert.AreEqual( 7, jsr.Variables[0], "the JSR refused to push and jumped anyway" );
		CollectionAssert.AreEqual( new[] { 5, 6 }, new[] { jsr.Stack[0], jsr.Stack[1] }, "writing nothing" );

		var ret = new RideScript( Build( 0,
		[
			Word( Opcode.RETURN ),                   // 0: the index is below slot -1
			Word( Opcode.COPY ), Var( 0 ), Lit( 7 ), // 1
			Word( Opcode.END )                       // 4
		] ) );

		ret.RestoreStacks( [Tag | 1, Tag | 1], callIndex: -3, heapIndex: 0, result: 0 );
		ret.Turn( 0f );

		Assert.IsFalse( ret.Running, "the RETURN found no frame and parked" );
		Assert.AreEqual( 0, ret.Variables[0] );
	}
}
