using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTPW.Tests;

/// <summary>
/// The clock and the chance a script keeps: <c>GETTIME</c>, <c>SETTIMER</c>, <c>GETTIMER</c> and
/// <c>RAND</c>, each built by hand rather than found in a shipped file.
///
/// <para>
/// <b>These are hand-built on purpose.</b> The shipped scripts that use timers are the same eighteen
/// for both halves of the pair, and every one of them reaches its <c>SETTIMER</c> only behind world
/// state nothing here provides - so a corpus walk would run them and assert nothing. A blob written
/// here reaches the instruction on its first turn.
/// </para>
///
/// <para>
/// Two of these guard against readings that would look right and be wrong: a timer that goes negative
/// once it has run out rather than resting at nought, and a <c>RAND</c> bound resolved as a variable.
/// The engine does neither, and the second is invisible in the shipped data because all 56 uses name a
/// literal bound.
/// </para>
/// </summary>
[TestClass]
public class RideScriptClockTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A branch target - tag 0x20.</summary>
	private static int Loc( int word ) => unchecked( (int)( 0x20000000u | (uint)word ) );

	/// <summary>A literal - tag 0x00, and what the engine sign-extends from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>
	/// A whole .RSE file to the format in <see cref="RideScriptFile"/>: magic, version, the six header
	/// counts, sixteen pad bytes, the body length in words, the body, and an empty string blob. No name
	/// table follows, which the reader is content with - a script sizes its variables from the header
	/// count.
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
		// in a full run and throws when the class is run on its own, which is how these nine shipped.
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
	/// <c>GETTIME</c> hands back the clock as it stands. The published docs call this "the time that
	/// the ride has been alive for", which would mean subtracting something the engine never subtracts:
	/// its handler reads the game's clock object and stores the result with no arithmetic at all.
	/// </summary>
	[TestMethod]
	public void TheTimeIsTheClockItselfRatherThanHowLongTheRideHasExisted()
	{
		var script = new RideScript( Build( 1, 50, Word( Opcode.GETTIME ), Var( 0 ), Word( Opcode.END ) ) );

		script.Turn( 1234f );

		Assert.AreEqual( 1234, script.Variables[0], "the clock, stored as it was handed in" );
	}

	/// <summary>
	/// <c>SETTIMER</c> puts a deadline on the clock and <c>GETTIMER</c> says how much of it is left.
	/// The two run on separate turns, which is the only way to see the gap open.
	/// </summary>
	[TestMethod]
	public void ATimerIsADeadlineAndWhatIsLeftOfItShrinks()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.SETTIMER ), Lit( 100 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.GETTIMER ), Var( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );
		script.Turn( 40f );

		Assert.AreEqual( 60, script.Variables[0], "a hundred set at nought, read at forty" );
	}

	/// <summary>
	/// A timer that has run out reads as nought and not as a negative number. The engine subtracts and
	/// then replaces the answer with zero if it came out below it, and a machine missing that would
	/// count downwards for ever - which every branch testing the timer would then read as "still
	/// running".
	/// </summary>
	[TestMethod]
	public void ATimerThatHasRunOutRestsAtNoughtRatherThanGoingNegative()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.SETTIMER ), Lit( 100 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.GETTIMER ), Var( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );
		script.Turn( 250f );

		Assert.AreEqual( 0, script.Variables[0], "long past its deadline" );
	}

	/// <summary>
	/// Every one of the 21 shipped <c>GETTIMER</c>s names a literal where a destination would go, so
	/// the answer lands in the result register and the write is skipped - the same idiom as
	/// <c>COAST 2 0</c>. A machine that refused the instruction instead would break all eighteen
	/// scripts that use it.
	/// </summary>
	[TestMethod]
	public void AskingForTheTimerWithNowhereToPutItStillLeavesItInTheResult()
	{
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.SETTIMER ), Lit( 100 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.GETTIMER ), Lit( 0 ),
			Word( Opcode.END ) ) );

		script.Turn( 0f );
		script.Turn( 40f );

		Assert.AreEqual( 60, script.Result, "the result register carries it" );
		Assert.AreEqual( 1, script.IgnoredWrites, "and the write was stepped over rather than refused" );
		Assert.AreEqual( 0, script.Variables[0], "nothing was written anywhere" );
	}

	/// <summary>
	/// <c>RAND</c>'s bound is reachable: the engine takes its draw modulo the bound <i>plus one</i>, so
	/// the range is nought to the bound inclusive. A bound of nought therefore has exactly one answer,
	/// which is the cleanest statement of it.
	/// </summary>
	[TestMethod]
	public void AChanceOfNothingCanOnlyComeOutNought()
	{
		var draws = Draw( bound: Lit( 0 ), turns: 20 );

		Assert.IsTrue( draws.All( value => value == 0 ), "a bound of nought leaves one answer" );
	}

	/// <summary>
	/// Over many draws the bound itself comes up and nothing above it ever does. Both halves matter:
	/// an exclusive bound would pass the second and fail the first.
	/// </summary>
	[TestMethod]
	public void EveryDrawLandsInRangeAndTheBoundItselfIsReached()
	{
		var draws = Draw( bound: Lit( 9 ), turns: 300 );

		Assert.IsTrue( draws.All( value => value >= 0 && value <= 9 ), "nothing outside nought to nine" );
		Assert.IsTrue( draws.Contains( 9 ), "the bound itself comes up" );
		Assert.IsTrue( draws.Contains( 0 ), "and so does nought" );
	}

	/// <summary>
	/// <b>The bound is read as a literal even when it is tagged as a variable.</b> The handler does a
	/// bare <c>MOVSX</c> on the operand word with none of the tag test every resolved value gets, so a
	/// variable-tagged bound is taken for its own index. This builds exactly that: the bound is written
	/// as variable 5, and every variable starts at nought - so a machine that resolved it would have a
	/// bound of nought and draw nothing but zeroes.
	/// </summary>
	[TestMethod]
	public void ABoundWrittenAsAVariableIsStillTakenAtFaceValue()
	{
		var draws = Draw( bound: Var( 5 ), turns: 200, variableCount: 6 );

		Assert.IsTrue( draws.Any( value => value != 0 ),
			"a resolved bound would be variable 5, which is nought, and every draw would be nought" );
		Assert.IsTrue( draws.All( value => value >= 0 && value <= 5 ), "the operand's own value is the bound" );
	}

	/// <summary>
	/// The same script drawn twice from a fresh machine gives the same numbers. The generator is the
	/// engine's own arithmetic and the seed is ours, so a run is repeatable even though the numbers are
	/// not the ones the original would have produced.
	/// </summary>
	[TestMethod]
	public void TheSameScriptDrawsTheSameNumbersTwice()
	{
		CollectionAssert.AreEqual( Draw( Lit( 100 ), 40 ), Draw( Lit( 100 ), 40 ), "two fresh machines agree" );
	}

	/// <summary>
	/// A <c>WAIT</c> is still counted in the caller's own units with nothing done to it. The engine
	/// divides by <c>0.5 + 0.01 * speed</c>, and the speed word is 50 for every script that ever runs,
	/// so that divisor is exactly one - this pins the boundary rather than the arithmetic.
	/// </summary>
	[TestMethod]
	public void AWaitEndsOnTheTickItAskedForAndNotBefore()
	{
		var script = new RideScript( Build( 0, 50, Word( Opcode.WAIT ), Lit( 3000 ), Word( Opcode.END ) ) );

		script.Turn( 0f );

		Assert.IsTrue( script.Waiting, "it has taken its deadline" );

		script.Turn( 2999f );

		Assert.IsTrue( script.Waiting, "one short of it is still waiting" );

		script.Turn( 3000f );

		Assert.IsFalse( script.Waiting, "and on it the wait is over" );
	}

	/// <summary>
	/// A critical section that loops without unlocking or yielding ends its turn at the cap, where the
	/// original's turn would never end: <c>CRIT_LOCK</c> makes instructions free, so the budget never runs
	/// out. The turn runs on a thread of its own so that a turn which never ends fails this test rather
	/// than hanging the run. The script is not stopped; the second turn runs and is capped the same way, as
	/// the count it keeps shows, and the cap is reported once, not every turn.
	/// </summary>
	[TestMethod]
	public void ACriticalSectionThatNeverUnlocksEndsTheTurnInsteadOfHangingIt()
	{
		// Lock, count, branch back to the lock: each pass is three locked instructions after the first.
		var script = new RideScript( Build( 1, 50,
			Word( Opcode.CRIT_LOCK ),
			Word( Opcode.ADD ), Var( 0 ), Lit( 1 ),
			Word( Opcode.BRANCH ), Loc( 0 ) ) );

		var previousLog = Log;
		var warnings = 0;
		Logger.LogDelegate count = ( severity, text ) =>
		{
			if ( severity == Logger.Level.Warning && text.Contains( "a critical section ran" ) )
				++warnings;
		};

		Log ??= new();
		Logger.OnLog += count;

		try
		{
			Exception? failure = null;
			var afterTheFirst = 0;

			var turns = new Thread( () =>
			{
				try
				{
					script.Turn( 0f );
					afterTheFirst = script.Variables[0];
					script.Turn( 0f );
				}
				catch ( Exception e )
				{
					failure = e;
				}
			} ) { IsBackground = true };

			turns.Start();

			Assert.IsTrue( turns.Join( TimeSpan.FromSeconds( 10 ) ), "a turn inside a critical section never ended" );

			if ( failure != null )
				throw failure;

			Assert.IsTrue( script.Running, "paused until its next turn, not stopped" );
			Assert.IsTrue( script.ReachedCriticalCap, "and it says it ran into the cap" );
			Assert.AreEqual( RideScript.CriticalStepCap + 1, script.LongestCritical,
				"each turn ends on the first instruction past the cap" );

			// 10,001 locked steps run ADD, BRANCH, CRIT_LOCK in turn from the first ADD, so the ADD is steps
			// 1, 4, ... 10,000 of them: 3,334 a turn.
			Assert.AreEqual( 3334, afterTheFirst, "the first turn counted to the cap" );
			Assert.AreEqual( 2 * 3334, script.Variables[0], "and the second ran, and was capped the same way" );
			Assert.AreEqual( 1, warnings, "the cap is reported once, not every turn" );
		}
		finally
		{
			Logger.OnLog -= count;
			Log = previousLog;
		}
	}

	/// <summary>
	/// A section whose <c>CRIT_UNLOCK</c> is the first instruction past the cap ends its turn by unlocking, as
	/// it would without the cap, and is not reported: the cap is tested after the instruction has run.
	/// </summary>
	[TestMethod]
	public void ASectionThatUnlocksOnTheStepPastTheCapIsNotCapped()
	{
		var body = new int[RideScript.CriticalStepCap + 4];
		body[0] = Word( Opcode.CRIT_LOCK );

		for ( var word = 1; word <= RideScript.CriticalStepCap; ++word )
			body[word] = Word( Opcode.NOP );

		body[RideScript.CriticalStepCap + 1] = Word( Opcode.CRIT_UNLOCK );
		body[RideScript.CriticalStepCap + 2] = Word( Opcode.BRANCH );
		body[RideScript.CriticalStepCap + 3] = Loc( 0 );

		var script = new RideScript( Build( 0, 50, body ) );

		script.Turn( 0f );

		Assert.AreEqual( RideScript.CriticalStepCap + 1, script.LongestCritical, "every NOP and the unlock ran locked" );
		Assert.AreEqual( RideScript.CriticalStepCap + 2, script.Position, "and the unlock ended the turn" );
		Assert.IsFalse( script.ReachedCriticalCap, "a section that unlocked is not reported as capped" );
	}

	/// <summary>
	/// A real section runs whole and is counted the way the census counts it: every instruction after the
	/// <c>CRIT_LOCK</c> up to and including the <c>CRIT_UNLOCK</c>, which ends the turn.
	/// </summary>
	[TestMethod]
	public void AShortCriticalSectionRunsWholeAndIsCounted()
	{
		var script = new RideScript( Build( 0, 50,
			Word( Opcode.CRIT_LOCK ),
			Word( Opcode.NOP ),
			Word( Opcode.NOP ),
			Word( Opcode.NOP ),
			Word( Opcode.CRIT_UNLOCK ),
			Word( Opcode.BRANCH ), Loc( 0 ) ) );

		script.Turn( 0f );

		Assert.AreEqual( 4, script.LongestCritical, "three NOPs and the unlock ran locked" );
		Assert.AreEqual( 5, script.Position, "and the unlock ended the turn" );
		Assert.IsFalse( script.ReachedCriticalCap );
	}

	/// <summary>
	/// Runs a two-word loop - draw, then branch back - once per turn, and collects what each draw left
	/// behind. A time slice of two is what makes a turn exactly one draw.
	/// </summary>
	private static int[] Draw( int bound, int turns, int variableCount = 1 )
	{
		var script = new RideScript( Build( variableCount, 2,
			Word( Opcode.RAND ), Var( 0 ), bound,
			Word( Opcode.BRANCH ), Loc( 0 ) ) );

		var drawn = new int[turns];

		for ( var i = 0; i < turns; ++i )
		{
			script.Turn( 0f );
			drawn[i] = script.Variables[0];
		}

		return drawn;
	}
}
