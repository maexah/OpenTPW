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
	/// declares 600 frames, which at the engine's 1000/30 is twenty seconds; the instruction answers that
	/// less the 300 the engine always takes off.
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

		Assert.AreEqual( 20000, script.Animations.DurationMilliseconds( 5, 0 ), "the clip's own declared length" );

		script.Turn( 0f );

		Assert.AreEqual( 19700, script.Variables[0], "the length the engine answers, less its 300" );
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

		script.Turn( 19699f );

		Assert.AreEqual( 0, script.Variables[0], "one millisecond short of the clip's length" );

		script.Turn( 19700f );

		Assert.AreEqual( 7, script.Variables[0], "and through on the millisecond it asked for" );
		Assert.IsFalse( script.Waiting );
	}
}
