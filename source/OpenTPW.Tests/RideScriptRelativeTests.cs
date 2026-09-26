using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The instructions that reach other scripts - <c>SPAWNCHILD</c>, <c>SPAWNSOUND</c>,
/// <c>REMOVECHILD</c>, the four that read and write a parent's or a child's variables, the two that do
/// the same to any script by id, and <c>FINDSCRIPTRAND</c>.
///
/// <para>
/// <b>These need no world either.</b> Every one works
/// on another script's own frame, and the registry they all go through is
/// <see cref="RideScriptScheduler"/>. Nothing about them waits on guests or
/// models.
/// </para>
///
/// <para>
/// Several of them would look right and be wrong in ways nothing else would catch.
/// <c>GETREMOTEVAR</c>'s three operands are destination, script, variable - the published docs call all
/// three unknown, and any other order still runs. Failing to find a script does not leave the
/// destination alone: it writes a nought, because the handler zeroes the result register before it
/// looks. A script that has never run <c>NAME</c> can never be found, because the loader leaves its
/// name offset at -1 rather than at nought. And <c>REMOVECHILD</c> <b>kills</b> the child rather than
/// forgetting it, which is only visible if something asks the registry afterwards.
/// </para>
/// </summary>
[TestClass]
public class RideScriptRelativeTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A variable operand - tag 0x40.</summary>
	private static int Var( int index ) => unchecked( (int)( 0x40000000u | (uint)index ) );

	/// <summary>A string operand - tag 0x10, carrying a byte offset into the blob.</summary>
	private static int Str( int offset ) => unchecked( (int)( 0x10000000u | (uint)offset ) );

	/// <summary>A literal - tag 0x00, and what the engine sign-extends from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>A branch target - tag 0x20, carrying a word index into the body.</summary>
	private static int Loc( int word ) => unchecked( (int)( 0x20000000u | (uint)word ) );

	/// <summary>
	/// A whole .RSE file, as the other families build one, except that this writes a <b>real string
	/// blob</b>: <c>NAME</c>, <c>SPAWNCHILD</c> and <c>FINDSCRIPTRAND</c> all take string operands, and a
	/// file with an empty blob cannot exercise any of them.
	/// </summary>
	private static RideScriptFile Build( int variableCount, string[] strings, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 8 );              // stack size
		writer.Write( 50 );             // time slice
		writer.Write( 0 );              // limbo records
		writer.Write( 0 );              // bounce records
		writer.Write( 0 );              // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		var blob = new MemoryStream();

		foreach ( var text in strings )
		{
			var bytes = Encoding.ASCII.GetBytes( text );

			blob.Write( bytes, 0, bytes.Length );
			blob.WriteByte( 0 );
		}

		writer.Write( (int)blob.Length );
		writer.Write( blob.ToArray() );

		// The variable-name tail every real .RSE carries - see RideScriptLimboTests for why leaving it
		// out passes in a full run and throws when this class runs on its own.
		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>Where the n-th string lands in the blob, which is what a string operand carries.</summary>
	private static int Offset( string[] strings, int index )
	{
		var at = 0;

		for ( int i = 0; i < index; ++i )
			at += strings[i].Length + 1;

		return at;
	}

	private static RideScript Script( int variableCount, string[] strings, params int[] body )
	{
		var file = Build( variableCount, strings, body );

		Assert.IsTrue( file.IsValid, "the hand-built script did not read - the builder is wrong, not the machine" );

		return new RideScript( file );
	}

	/// <summary>
	/// Registering a script is also what tells it who it is and where to look, because in the original
	/// the id is a field of the script's own frame and there is exactly one registry.
	/// </summary>
	[TestMethod]
	public void RegisteringAScriptTellsItItsOwnIdAndWhereToLook()
	{
		var script = Script( 0, [], Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 12, script );

		Assert.AreEqual( 12, script.Id );
		Assert.AreSame( scheduler, script.Host );
		Assert.AreSame( script, scheduler.Find( 12 ) );
		Assert.IsNull( scheduler.Find( 13 ), "an id nothing was registered under found something" );
	}

	/// <summary>
	/// Spawning loads a script, registers it, and links it <b>both ways</b> - the parent keeps the
	/// child's id and the child is told the parent's. The engine does the second half by finding the new
	/// script in the registry and writing its own id into the child's <c>+0x10</c>.
	/// </summary>
	[TestMethod]
	public void SpawningAChildRegistersItAndLinksItBothWays()
	{
		var parent = Script( 0, ["child.rse"], Word( Opcode.SPAWNCHILD ), Str( 0 ), Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler();

		var asked = string.Empty;

		scheduler.Loader = name =>
		{
			asked = name;

			return Script( 1, [], Word( Opcode.END ) );
		};

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( "child.rse", asked, "the name the script wrote is what reached the loader" );
		Assert.AreNotEqual( 0, parent.ChildId, "the parent kept no child" );

		var child = scheduler.Find( parent.ChildId );

		Assert.IsNotNull( child, "the child was never registered" );
		Assert.AreEqual( parent.Id, child!.ParentId, "the child was not told who spawned it" );
		Assert.AreEqual( 2, scheduler.Count, "the child is not being given turns" );
	}

	/// <summary>
	/// A spawned script is named <b>relative to the folder its parent came from</b> - the engine's field
	/// <c>+0x38</c>, which its loader fills in by stripping the last component off the path it was itself
	/// handed, and which both spawning instructions concatenate their operand onto.
	///
	/// <para>
	/// <b>It decides which file is meant, not merely where to look.</b> All 48 spawn sites in the shipped
	/// corpus name a file sitting in the asking script's own directory, and the names are nowhere near
	/// unique: 28 of them ask for <c>EventMap.rse</c>, of which the game ships one copy per item. A loader
	/// handed the bare name therefore cannot tell which of them was wanted, and in a park full of items it
	/// would confidently answer the wrong one. The empty case is pinned by
	/// <see cref="SpawningAChildRegistersItAndLinksItBothWays"/>, where a script that came from nowhere
	/// asks for the bare name it wrote.
	/// </para>
	/// </summary>
	[TestMethod]
	public void SpawningAsksForTheChildWhereTheParentCameFrom()
	{
		var parent = Script( 0, ["EventMap.rse"], Word( Opcode.SPAWNCHILD ), Str( 0 ), Word( Opcode.END ) );

		parent.Directory = "levels/jungle/rides/coaster1";

		var scheduler = new RideScriptScheduler();
		var asked = string.Empty;

		scheduler.Loader = name =>
		{
			asked = name;

			return Script( 1, [], Word( Opcode.END ) );
		};

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( "levels/jungle/rides/coaster1/EventMap.rse", asked,
			"the child should have been asked for under the folder its parent came from" );

		Assert.AreNotEqual( 0, parent.ChildId, "nothing was spawned, so this proves nothing" );
	}

	/// <summary>
	/// <c>SPAWNCHILD</c> writes no result. Almost every other instruction in the family answers into the
	/// result register, so a machine that set one here by analogy would change the branch that follows.
	/// </summary>
	[TestMethod]
	public void SpawningWritesNoResult()
	{
		var parent = Script( 1, ["child.rse"],
			Word( Opcode.COPY ), Var( 0 ), Lit( 9 ),
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler { Loader = _ => Script( 1, [], Word( Opcode.END ) ) };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreNotEqual( 0, parent.ChildId, "nothing was spawned, so this proves nothing" );
		Assert.AreEqual( 9, parent.Result, "the COPY's answer should still be in the register" );
	}

	/// <summary>
	/// With nothing to load with, spawning is counted rather than guessed - the same answer
	/// <c>COAST</c> gives without a ride and <c>ADDOBJ</c> without somewhere to put an effect.
	/// </summary>
	[TestMethod]
	public void SpawningWithNothingToLoadWithIsCountedRatherThanGuessed()
	{
		var script = Script( 0, ["child.rse"], Word( Opcode.SPAWNCHILD ), Str( 0 ), Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.ChildId, "a child appeared from nowhere" );
		Assert.AreEqual( 1, script.NotImplemented, "the instruction was not counted" );
	}

	/// <summary>
	/// <c>SPAWNSOUND</c> is not a sound instruction and it links nothing: it loads a script into a second
	/// and separate slot, leaving the child slot alone and telling the new script nothing about who
	/// spawned it. All 28 shipped uses ask for the same file, <c>EventMap.rse</c>.
	/// </summary>
	[TestMethod]
	public void SpawningASoundScriptLinksNothingAtAll()
	{
		var script = Script( 0, ["EventMap.rse"], Word( Opcode.SPAWNSOUND ), Str( 0 ), Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler { Loader = _ => Script( 1, [], Word( Opcode.END ) ) };

		scheduler.Add( 1, script );
		script.Turn( 0f );

		Assert.AreNotEqual( 0, script.SoundChildId, "nothing was loaded" );
		Assert.AreEqual( 0, script.ChildId, "a SPAWNSOUND filled the child slot, which is a different field" );

		var spawned = scheduler.Find( script.SoundChildId );

		Assert.IsNotNull( spawned );
		Assert.AreEqual( 0, spawned!.ParentId, "the spawned script was linked to a parent it does not have" );
	}

	/// <summary>
	/// A parent writes into its child's variable and reads one back out. These are two halves of the same
	/// engine block, reached with the child id rather than the parent's.
	/// </summary>
	[TestMethod]
	public void AParentWritesIntoItsChildAndReadsBackOut()
	{
		var child = Script( 2, [], Word( Opcode.END ) );

		var parent = Script( 1, ["child.rse"],
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.SETVARINCHILD ), Lit( 1 ), Lit( 42 ),
			Word( Opcode.GETVARINCHILD ), Var( 0 ), Lit( 1 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler { Loader = _ => child };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( 42, child.Variables[1], "the write never reached the child" );
		Assert.AreEqual( 42, parent.Variables[0], "the read never came back" );
	}

	/// <summary>
	/// A child reaches back into its parent with <c>GETVARINPARENT</c>.
	/// </summary>
	[TestMethod]
	public void AChildReachesBackIntoItsParent()
	{
		var child = Script( 1, [], Word( Opcode.GETVARINPARENT ), Var( 0 ), Lit( 2 ), Word( Opcode.END ) );

		var parent = Script( 3, ["child.rse"],
			Word( Opcode.COPY ), Var( 2 ), Lit( 77 ),
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler { Loader = _ => child };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );
		child.Turn( 0f );

		Assert.AreEqual( 77, child.Variables[0], "the child could not see its parent's variable" );
	}

	/// <summary>
	/// Reaching for a relation that is not there does nothing whatever - the engine tests the id against
	/// nought and leaves. This is the state of every script until a <c>SPAWNCHILD</c> has run, which is
	/// why it must be quiet rather than counted.
	/// </summary>
	[TestMethod]
	public void ReachingForARelationThatIsNotThereDoesNothing()
	{
		var script = Script( 1, [],
			Word( Opcode.COPY ), Var( 0 ), Lit( 5 ),
			Word( Opcode.SETVARINCHILD ), Lit( 0 ), Lit( 9 ),
			Word( Opcode.GETVARINPARENT ), Var( 0 ), Lit( 0 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 5, script.Variables[0], "something was written where there is no relation to read" );
		Assert.AreEqual( 5, script.Result, "the result register was disturbed" );
	}

	/// <summary>
	/// Reading into something that is not a variable does nothing at all, and does not even reach the
	/// result register - the destination is tested before the relation is so much as looked at. That is
	/// <c>FORCEUNLIMBO</c>'s shape rather than the usual one, and getting it wrong would leave an answer
	/// in the register for the next branch to read.
	/// </summary>
	[TestMethod]
	public void ReadingIntoSomethingThatIsNotAVariableDoesNothingAtAll()
	{
		var child = Script( 2, [], Word( Opcode.END ) );

		var parent = Script( 1, ["child.rse"],
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.SETVARINCHILD ), Lit( 1 ), Lit( 42 ),
			Word( Opcode.COPY ), Var( 0 ), Lit( 3 ),
			Word( Opcode.GETVARINCHILD ), Lit( 0 ), Lit( 1 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler { Loader = _ => child };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( 3, parent.Result, "the refused read still reached the result register" );
		Assert.AreEqual( 1, parent.IgnoredWrites, "the instruction was not stepped over" );
	}

	/// <summary>
	/// An index past the target's own count is refused. The engine compares against the <b>target's</b>
	/// variable count, not the caller's, so a script with plenty of variables of its own cannot reach
	/// past the end of a smaller one.
	/// </summary>
	[TestMethod]
	public void AnIndexPastTheTargetsOwnCountIsRefused()
	{
		var child = Script( 1, [], Word( Opcode.END ) );

		var parent = Script( 8, ["child.rse"],
			Word( Opcode.COPY ), Var( 0 ), Lit( 4 ),
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.SETVARINCHILD ), Lit( 6 ), Lit( 99 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler { Loader = _ => child };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( 1, child.Variables.Count, "the child should have one variable" );
		Assert.AreEqual( 4, parent.Result, "the refused write still set the result register" );
	}

	/// <summary>
	/// <b><c>GETREMOTEVAR</c>'s operands are destination, script, variable.</b> The published docs call all
	/// three unknown. Any other reading still runs and still writes something, which is exactly why this
	/// is pinned: the value only lands in the right place if the order is right.
	/// </summary>
	[TestMethod]
	public void TheOperandsOfARemoteReadAreDestinationThenScriptThenVariable()
	{
		var other = Script( 4, [], Word( Opcode.COPY ), Var( 3 ), Lit( 77 ), Word( Opcode.END ) );
		var script = Script( 1, [], Word( Opcode.GETREMOTEVAR ), Var( 0 ), Lit( 2 ), Lit( 3 ), Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );
		scheduler.Add( 2, other );

		other.Turn( 0f );
		script.Turn( 0f );

		Assert.AreEqual( 77, script.Variables[0], "the remote read did not fetch the other script's variable" );
		Assert.AreEqual( 77, script.Result );
	}

	/// <summary>
	/// <b>Failing to find a script is not silence: it answers nought.</b> The handler zeroes the result
	/// register before it looks anything up, and every way of failing jumps to a tail that writes that
	/// nought into the destination anyway. A machine that left the destination alone would leave whatever
	/// was there for the branch that follows to read.
	/// </summary>
	[TestMethod]
	public void AskingForAScriptThatIsNotThereWritesANought()
	{
		var script = Script( 1, [],
			Word( Opcode.COPY ), Var( 0 ), Lit( 5 ),
			Word( Opcode.GETREMOTEVAR ), Var( 0 ), Lit( 999 ), Lit( 0 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );
		script.Turn( 0f );

		Assert.AreEqual( 0, script.Variables[0], "the failed read left the old value in place" );
		Assert.AreEqual( 0, script.Result, "and the register kept it too" );
	}

	/// <summary>
	/// Writing into any script by id, which is the other half of the remote pair. Unlike its parent and
	/// child cousins this one checks both ends of the index and leaves the result register alone when it
	/// refuses.
	/// </summary>
	[TestMethod]
	public void AVariableCanBeWrittenInAnyScriptByItsId()
	{
		var other = Script( 2, [], Word( Opcode.END ) );
		var script = Script( 1, [], Word( Opcode.SETREMOTEVAR ), Lit( 2 ), Lit( 1 ), Lit( 42 ), Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );
		scheduler.Add( 2, other );

		script.Turn( 0f );

		Assert.AreEqual( 42, other.Variables[1], "the write never reached the other script" );
	}

	/// <summary>
	/// <b><c>REMOVECHILD</c> kills the child rather than forgetting it.</b> The handler hands the id to the
	/// same teardown the tick loop uses on a script that has stopped, so the child stops being given turns
	/// at all - which is only visible by asking the registry afterwards.
	/// </summary>
	[TestMethod]
	public void RemovingAChildKillsItRatherThanForgettingIt()
	{
		var parent = Script( 0, ["child.rse"],
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.REMOVECHILD ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler { Loader = _ => Script( 1, [], Word( Opcode.END ) ) };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( 0, parent.ChildId, "the parent is still holding a child" );
		Assert.AreEqual( 1, scheduler.Count, "the child was forgotten rather than taken down" );
	}

	/// <summary>
	/// A script that stops takes its child down with it, because what the tick loop calls on a stopped
	/// script is the very same teardown <c>REMOVECHILD</c> calls.
	/// </summary>
	[TestMethod]
	public void AScriptThatStopsTakesItsChildWithIt()
	{
		var parent = Script( 0, ["child.rse"], Word( Opcode.SPAWNCHILD ), Str( 0 ), Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler { Loader = _ => Script( 1, [], Word( Opcode.NOP ) ) };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreEqual( 2, scheduler.Count, "there should be a parent and a child" );
		Assert.IsFalse( parent.Running, "the parent should have run its END" );

		scheduler.Advance( 0f );

		Assert.AreEqual( 0, scheduler.Count, "the child outlived the script that spawned it" );
		Assert.AreEqual( 1, scheduler.Finished, "only the script that stopped should be counted as finished" );
	}

	/// <summary>
	/// A child that dies tells its parent it has gone, so nothing is ever left holding an id that names
	/// nobody.
	/// </summary>
	[TestMethod]
	public void AChildThatDiesTellsItsParentItHasGone()
	{
		var child = Script( 0, [], Word( Opcode.END ) );

		// The parent has to still be running when the child dies, or its own teardown would clear the slot
		// and this would pass without the child having told it anything. So it loops rather than ending:
		// ENDSLICE gives up each turn and the branch comes back to it, which is the shape every shipped
		// script settles into anyway.
		var parent = Script( 0, ["child.rse"],
			Word( Opcode.SPAWNCHILD ), Str( 0 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.BRANCH ), Loc( 2 ) );

		var scheduler = new RideScriptScheduler { Loader = _ => child };

		scheduler.Add( 1, parent );
		parent.Turn( 0f );

		Assert.AreNotEqual( 0, parent.ChildId, "nothing was spawned" );

		child.Turn( 0f );
		scheduler.Advance( 0f );

		Assert.IsTrue( parent.Running, "the parent stopped, so its own teardown would have cleared the slot" );
		Assert.AreEqual( 1, scheduler.Count, "only the child should have gone" );
		Assert.AreEqual( 0, parent.ChildId, "the parent is still holding an id that names nobody" );
	}

	/// <summary>
	/// <b>A script that has never run <c>NAME</c> can never be found.</b> The loader leaves the name offset
	/// at -1 rather than at nought and the walk skips a negative, which is what separates "has not named
	/// itself" from "is called whatever sits at offset nought" - and 31 of the 308 shipped scripts are in
	/// exactly that position.
	/// </summary>
	[TestMethod]
	public void AScriptThatHasNeverNamedItselfCannotBeFound()
	{
		var names = new[] { "Ghost Train" };

		var unnamed = Script( 0, names, Word( Opcode.NOP ) );
		var named = Script( 0, names, Word( Opcode.NAME ), Str( 0 ), Word( Opcode.NOP ) );

		var seeker = Script( 1, names,
			Word( Opcode.FINDSCRIPTRAND ), Str( Offset( names, 0 ) ), Var( 0 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, seeker );
		scheduler.Add( 2, unnamed );
		scheduler.Add( 3, named );

		Assert.IsFalse( unnamed.IsNamed );

		named.Turn( 0f );

		Assert.IsTrue( named.IsNamed, "the NAME did not take" );

		seeker.Turn( 0f );

		Assert.AreEqual( 3, seeker.Variables[0], "the only script with that name should have been found" );
	}

	/// <summary>
	/// Looking for a name nobody has answers nought <b>in the register only, and leaves the destination
	/// exactly as it was</b>.
	///
	/// <para>
	/// That is the opposite of what <c>GETREMOTEVAR</c> does with a script it cannot find, and the two
	/// sitting side by side is why both are pinned. <c>GETREMOTEVAR</c> zeroes the register up front and
	/// every failing path falls into a tail that writes it out, so a failure overwrites the destination
	/// with nought. <c>FINDSCRIPTRAND</c> zeroes the same register up front but only ever writes from
	/// inside its second walk, on a match - so a failure writes nothing at all, and whatever the variable
	/// held survives. A machine that treated the two alike would be wrong about one of them either way.
	/// </para>
	/// </summary>
	[TestMethod]
	public void LookingForANameNobodyHasLeavesTheDestinationAlone()
	{
		var names = new[] { "Ghost Train" };

		var seeker = Script( 1, names,
			Word( Opcode.COPY ), Var( 0 ), Lit( 6 ),
			Word( Opcode.FINDSCRIPTRAND ), Str( 0 ), Var( 0 ),
			Word( Opcode.END ) );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, seeker );
		seeker.Turn( 0f );

		Assert.AreEqual( 6, seeker.Variables[0], "the failed search overwrote the variable" );
		Assert.AreEqual( 0, seeker.Result, "but the register carries the nought the branch reads" );
	}

	/// <summary>
	/// With more than one script of the same name the answer is one of them, drawn against the engine's
	/// own generator - and always one of them, never a script with a different name.
	/// </summary>
	[TestMethod]
	public void WithSeveralOfTheSameNameTheAnswerIsAlwaysOneOfThem()
	{
		var names = new[] { "Ghost Train" };

		var first = Script( 0, names, Word( Opcode.NAME ), Str( 0 ), Word( Opcode.NOP ) );
		var second = Script( 0, names, Word( Opcode.NAME ), Str( 0 ), Word( Opcode.NOP ) );
		var other = Script( 0, ["Log Flume"], Word( Opcode.NAME ), Str( 0 ), Word( Opcode.NOP ) );

		var body = Enumerable.Range( 0, 12 )
			.SelectMany( _ => new[] { Word( Opcode.FINDSCRIPTRAND ), Str( 0 ), Var( 0 ), Word( Opcode.ENDSLICE ) } )
			.Append( Word( Opcode.END ) )
			.ToArray();

		var seeker = Script( 1, names, body );

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, seeker );
		scheduler.Add( 2, first );
		scheduler.Add( 3, second );
		scheduler.Add( 4, other );

		first.Turn( 0f );
		second.Turn( 0f );
		other.Turn( 0f );

		var seen = new System.Collections.Generic.HashSet<int>();

		for ( int turn = 0; turn < 12; ++turn )
		{
			seeker.Turn( 0f );
			seen.Add( seeker.Variables[0] );
		}

		Assert.IsTrue( seen.All( id => id == 2 || id == 3 ),
			$"something other than the two named scripts was answered: {string.Join( ", ", seen )}" );

		Assert.IsFalse( seen.Contains( 4 ), "the script with a different name was found" );
	}
}
