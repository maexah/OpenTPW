using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// Who gets a turn, and when.
///
/// <para>
/// These build their own scripts rather than using the shipped ones, which is the opposite of what
/// <see cref="RideScriptRunTests"/> does and is deliberate. The shipped corpus is the right instrument
/// for "does the machine agree with the data"; it is the wrong one for a case the data holds only rarely,
/// and one of the cases below is exactly that - a critical section left open across the end of a turn,
/// which seven shipped sections do (docs/exe/park.md, "Corpus shape") and which the engine makes harmless
/// by clearing its flag as each script's turn begins.
/// </para>
///
/// <para>
/// A script here is a run of instructions with a time slice of 1, so it executes exactly one instruction
/// per turn and its position counts the turns it has been given. That makes the schedule directly
/// observable without the scheduler having to report on itself.
/// </para>
/// </summary>
[TestClass]
public class RideScriptSchedulerTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>
	/// A whole .RSE file, built by hand to the format in <see cref="RideScriptFile"/>: magic, version,
	/// the six header counts, the sixteen pad bytes, the body length in words, the body, and an empty
	/// string blob. No variables, so no name table follows.
	/// </summary>
	private static RideScriptFile Build( int timeSlice, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 0 );          // variable count
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
		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>A script that does nothing for as long as it is asked to, one instruction per turn.</summary>
	private static RideScript Idle( int instructions = 200 )
	{
		var body = Enumerable.Repeat( Word( Opcode.NOP ), instructions ).ToArray();

		var file = Build( 1, body );

		Assert.IsTrue( file.IsValid, "the hand-built script did not read - the builder is wrong, not the machine" );

		return new RideScript( file );
	}

	/// <summary>
	/// The rule itself, checked against the formula rather than inferred from a run: every script is due
	/// exactly one turn in any eight consecutive ticks, and the eight ids 0-7 fall on eight different
	/// ticks - which is the whole point of mixing the id in rather than running everybody together.
	/// </summary>
	[TestMethod]
	public void EveryScriptIsDueOneTurnInEightAndTheIdsSpreadThemOut()
	{
		for ( int id = 0; id < 16; ++id )
		{
			var due = Enumerable.Range( 1, 8 ).Count( tick => RideScriptScheduler.RunsOn( id, tick, false ) );

			Assert.AreEqual( 1, due, $"id {id} was due {due} turns in eight ticks" );
		}

		var ticks = Enumerable.Range( 0, 8 )
			.Select( id => Enumerable.Range( 1, 8 ).First( tick => RideScriptScheduler.RunsOn( id, tick, false ) ) )
			.ToArray();

		Assert.AreEqual( 8, ticks.Distinct().Count(), "ids 0-7 did not land on eight different ticks" );
	}

	/// <summary>TURBO takes a script out of the spread and gives it every tick.</summary>
	[TestMethod]
	public void TurboIsDueEveryTick()
	{
		for ( int tick = 1; tick <= 20; ++tick )
			Assert.IsTrue( RideScriptScheduler.RunsOn( 3, tick, everyTick: true ), $"tick {tick}" );
	}

	/// <summary>
	/// The counter moves before anybody runs, so the first tick is 1. That is not a detail: it decides
	/// who is due on it, and starting from 0 would shift every script by one tick forever.
	/// </summary>
	[TestMethod]
	public void TheTickCounterMovesBeforeAnybodyRuns()
	{
		var scheduler = new RideScriptScheduler();

		Assert.AreEqual( 0, scheduler.Tick, "nothing has happened yet" );

		scheduler.Advance( 0f );

		Assert.AreEqual( 1, scheduler.Tick, "the first tick should be 1" );
	}

	/// <summary>
	/// A plain script and a TURBO script, run side by side through the same scheduler for ten ticks.
	/// Because each takes one instruction per turn, the positions are the turn counts.
	///
	/// <para>
	/// The plain script has id 0, so it is due only on tick 8 of the first ten - one turn. The TURBO
	/// script has id 1, so it is due on tick 1, where it runs its <c>TURBO</c> and is due every tick
	/// after: one instruction plus its operand on the first turn, then nine more turns.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ATurboScriptRunsEveryTickAndAPlainOneOnceInEight()
	{
		var plain = Idle();

		var turbo = new RideScript( Build( 1,
			[Word( Opcode.TURBO ), 1, .. Enumerable.Repeat( Word( Opcode.NOP ), 50 )] ) );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 0, plain );
		scheduler.Add( 1, turbo );

		for ( int tick = 0; tick < 10; ++tick )
			scheduler.Advance( 0f );

		Assert.IsTrue( turbo.EveryTick, "TURBO did not take" );

		// Tick 8 only: (0 ^ 8) & 7 == 0, and no other tick in 1..10 satisfies it.
		Assert.AreEqual( 1, plain.Position, "the plain script did not get exactly one turn in ten ticks" );

		// Tick 1 runs TURBO and its operand (position 2), then ticks 2-10 run one NOP each.
		Assert.AreEqual( 11, turbo.Position, "the turbo script did not get a turn on every tick" );

		Assert.AreEqual( 2, scheduler.Count, "neither script should have stopped" );
	}

	/// <summary>
	/// A script that stops is dropped at the end of the same tick, as the engine unregisters one whose
	/// program counter has gone negative.
	/// </summary>
	[TestMethod]
	public void AScriptThatStopsIsDropped()
	{
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, new RideScript( Build( 1, Word( Opcode.END ) ) ) );

		Assert.AreEqual( 1, scheduler.Count, "it was not added" );

		scheduler.Advance( 0f );

		Assert.AreEqual( 0, scheduler.Count, "the stopped script was still being given turns" );
		Assert.AreEqual( 1, scheduler.Finished, "the drop was not counted" );
	}

	/// <summary>
	/// <b>A critical section does not outlive the turn that took it.</b> The engine clears its critical
	/// flag as each script's turn begins, so a <c>CRIT_LOCK</c> that is never unlocked stops mattering the
	/// moment the turn ends.
	///
	/// <para>
	/// The script below, with a time slice of one, locks, ends its slice while still locked, and then has two
	/// <c>NOP</c>s and an <c>END</c>. <c>CRIT_LOCK</c> costs nothing, so the first turn runs it and the
	/// <c>ENDSLICE</c>; with the reset each later turn runs one instruction, and the script is at word 4 after
	/// three turns, still running. Without it the budget is never decremented again, so the second turn runs
	/// both <c>NOP</c>s and the <c>END</c> in one go and the script is stopped at word 5. Seven shipped
	/// sections do yield while locked (docs/exe/park.md, "Corpus shape").
	/// </para>
	/// </summary>
	[TestMethod]
	public void ACriticalSectionDoesNotOutliveItsTurn()
	{
		var script = new RideScript( Build( 1,
			Word( Opcode.CRIT_LOCK ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.NOP ),
			Word( Opcode.NOP ),
			Word( Opcode.END ) ) );

		for ( int turn = 0; turn < 3; ++turn )
			script.Turn( 0f );

		Assert.IsTrue( script.Running, "the script ran past its budget and hit END inside three turns" );
		Assert.AreEqual( 4, script.Position, "the critical section outlived the turn that took it" );
	}

	/// <summary>Removing by id takes a script out whether or not it has stopped.</summary>
	[TestMethod]
	public void AScriptCanBeTakenOutByItsId()
	{
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 4, Idle() );

		Assert.IsFalse( scheduler.Remove( 5 ), "an id that was never added was removed" );
		Assert.IsTrue( scheduler.Remove( 4 ), "the script was not removed" );
		Assert.AreEqual( 0, scheduler.Count, "it is still there" );
	}
}
