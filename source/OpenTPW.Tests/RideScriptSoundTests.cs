using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The two instructions that change what a ride sounds like - <c>DIPMUSIC</c> and <c>SETOBJPARAM</c>.
///
/// <para>
/// Both were chosen over the opcode that led the ladder. <c>REPAIREFFECT</c> completes twelve scripts on
/// paper and cannot be built: past its first call both of its paths index a runtime table by the model
/// handle and dereference the result three levels deep with no guard, so with no world it would take an
/// access violation rather than idle. These two need nothing that is missing.
/// </para>
///
/// <para>
/// What each would be easy to get wrong: <c>DIPMUSIC</c> is a <b>mute</b>, not a partial duck - the mixer
/// tests its global against nought and drives the music group's volume to zero outright - and <b>any</b>
/// non-nought value does it, so the operand is not the 0-or-1 switch it looks like. Nothing ever un-mutes
/// but a script's death. And <c>SETOBJPARAM</c>'s first operand is a <b>tag</b>, not a slot: it is the
/// same field <c>KILLOBJ</c> matches on, and a particle carrying that tag is walked past because the
/// type decides, not the tag.
/// </para>
/// </summary>
[TestClass]
public class RideScriptSoundTests
{
	/// <summary>The instruction word for an opcode: top byte 0x80, the opcode in the low bits.</summary>
	private static int Word( Opcode opcode ) => unchecked( (int)( 0x80000000u | (uint)opcode ) );

	/// <summary>A literal - tag 0x00, and what the engine sign-extends from its low sixteen bits.</summary>
	private static int Lit( int value ) => value;

	/// <summary>A branch target - tag 0x20, carrying a word index into the body.</summary>
	private static int Loc( int word ) => unchecked( (int)( 0x20000000u | (uint)word ) );

	private static RideScriptFile Build( int variableCount, params int[] body )
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

		writer.Write( 0 );              // string blob length

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

	private static RideScript Script( int variableCount, params int[] body )
	{
		var file = Build( variableCount, body );

		Assert.IsTrue( file.IsValid, "the hand-built script did not read - the builder is wrong, not the machine" );

		return new RideScript( file );
	}

	/// <summary>
	/// Muting is one setting for the whole game, held in the registry rather than in the script - and the
	/// script keeps only a marker saying it was the one that did it.
	/// </summary>
	[TestMethod]
	public void MutingTheMusicIsOneSettingForTheWholeGame()
	{
		var script = Script( 0, Word( Opcode.DIPMUSIC ), Lit( 1 ), Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );

		Assert.AreEqual( 0, scheduler.MusicDip, "the music starts up" );

		script.Turn( 0f );

		Assert.AreEqual( 1, scheduler.MusicDip, "the music was not muted" );
		Assert.IsTrue( script.DippedMusic, "the script did not record that it was holding it" );
	}

	/// <summary>
	/// <b>Any value other than nought mutes.</b> The mixer tests the setting against zero rather than
	/// against one, so a machine treating the operand as a boolean would be right about every shipped
	/// script and wrong about the instruction.
	/// </summary>
	[TestMethod]
	public void AnyValueOtherThanNoughtMutes()
	{
		var script = Script( 0, Word( Opcode.DIPMUSIC ), Lit( 7 ), Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );
		script.Turn( 0f );

		Assert.AreEqual( 7, scheduler.MusicDip, "the value reaches the setting as it stands" );
		Assert.IsTrue( script.DippedMusic );
	}

	/// <summary>
	/// <b>Only death ever lets the music back up.</b> No opcode clears the setting, and no shipped script
	/// passes nought, so the teardown reading the script's marker is the whole of the release path.
	/// </summary>
	[TestMethod]
	public void OnlyDeathEverLetsTheMusicBackUp()
	{
		var script = Script( 0, Word( Opcode.DIPMUSIC ), Lit( 1 ), Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );
		script.Turn( 0f );

		Assert.AreEqual( 1, scheduler.MusicDip, "it was never muted, so this proves nothing" );
		Assert.IsFalse( script.Running, "the script should have run its END" );

		scheduler.Advance( 0f );

		Assert.AreEqual( 0, scheduler.Count, "the stopped script was not dropped" );
		Assert.AreEqual( 0, scheduler.MusicDip, "the music is still being held down by a script that has gone" );
	}

	/// <summary>
	/// A script that never muted does not un-mute on its way out. The teardown consults the marker, so
	/// one script dying cannot release what another is holding.
	/// </summary>
	[TestMethod]
	public void AScriptThatNeverMutedDoesNotReleaseWhatAnotherIsHolding()
	{
		// The holder loops rather than ending, so it is still running when the other one dies.
		var holder = Script( 0,
			Word( Opcode.DIPMUSIC ), Lit( 1 ),
			Word( Opcode.ENDSLICE ),
			Word( Opcode.BRANCH ), Loc( 2 ) );

		var other = Script( 0, Word( Opcode.END ) );
		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, holder );
		scheduler.Add( 2, other );

		holder.Turn( 0f );
		other.Turn( 0f );

		Assert.AreEqual( 1, scheduler.MusicDip );

		scheduler.Advance( 0f );

		Assert.IsTrue( holder.Running, "the holder stopped, so this would pass for the wrong reason" );
		Assert.AreEqual( 1, scheduler.MusicDip, "somebody else's death let the music back up" );
	}

	/// <summary>
	/// With no registry there is nowhere to put a setting that belongs to the whole game, so the
	/// instruction is counted rather than guessed - the same answer <c>COAST</c> gives without a ride.
	/// </summary>
	[TestMethod]
	public void MutingWithNoRegistryIsCountedRatherThanGuessed()
	{
		var script = Script( 0, Word( Opcode.DIPMUSIC ), Lit( 1 ), Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.NotImplemented, "the instruction was not counted" );
		Assert.IsFalse( script.DippedMusic, "it recorded a mute it never performed" );
	}

	/// <summary>
	/// Setting a parameter reaches every sound carrying the tag, and leaves the others alone. The handle
	/// going to nought is the engine's own answer: the call it makes returns nought when the sound system
	/// is down, and the handler stores whatever comes back.
	/// </summary>
	[TestMethod]
	public void SettingAParameterReachesEverySoundCarryingTheTag()
	{
		var effects = new RideEffects();

		effects.Add( type: 3, node: -1, effect: 20, tag: 1 );
		effects.Add( type: 5, node: -1, effect: 21, tag: 1 );
		effects.Add( type: 4, node: -1, effect: 22, tag: 2 );

		var script = new RideScript( Build( 0,
			Word( Opcode.SETOBJPARAM ), Lit( 1 ), Lit( 20 ), Lit( 75 ),
			Word( Opcode.END ) ) )
		{ Effects = effects };

		script.Turn( 0f );

		Assert.AreEqual( 2, effects.Parameters, "the tag matched the wrong number of records" );

		var untouched = effects.Records.Single( record => record.Tag == 2 );

		Assert.AreNotEqual( 0, untouched.Handle, "a record with a different tag was reached" );

		foreach ( var record in effects.Records.Where( record => record.Tag == 1 ) )
			Assert.AreEqual( 0, record.Handle, "the handle was not replaced by what the call answered" );
	}

	/// <summary>
	/// <b>A particle carrying the same tag is walked past.</b> The engine's dispatch has two cases and the
	/// particle case does nothing at all, so it is the record's type that decides rather than its tag -
	/// which matters because a tag is a group label that a particle and a sound can share.
	/// </summary>
	[TestMethod]
	public void AParticleCarryingTheSameTagIsWalkedPast()
	{
		var effects = new RideEffects();

		effects.Add( type: 1, node: -1, effect: 30, tag: 4 );
		effects.Add( type: 6, node: -1, effect: 31, tag: 4 );

		var script = new RideScript( Build( 0,
			Word( Opcode.SETOBJPARAM ), Lit( 4 ), Lit( 20 ), Lit( 0 ),
			Word( Opcode.END ) ) )
		{ Effects = effects };

		script.Turn( 0f );

		Assert.AreEqual( 1, effects.Parameters, "the particle was counted or the sound was missed" );

		var particle = effects.Records.Single( record => record.IsParticle );

		Assert.AreNotEqual( 0, particle.Handle, "the particle's handle was replaced, which the engine never does" );
	}

	/// <summary>
	/// An unmatched tag reaches nothing and says so, rather than falling back on the first record - the
	/// same discipline <c>KILLOBJ</c> keeps.
	/// </summary>
	[TestMethod]
	public void AnUnmatchedTagReachesNothing()
	{
		var effects = new RideEffects();

		effects.Add( type: 3, node: -1, effect: 20, tag: 1 );

		var script = new RideScript( Build( 0,
			Word( Opcode.SETOBJPARAM ), Lit( 9 ), Lit( 20 ), Lit( 0 ),
			Word( Opcode.END ) ) )
		{ Effects = effects };

		script.Turn( 0f );

		Assert.AreEqual( 0, effects.Parameters, "something was reached that carries a different tag" );
		Assert.AreNotEqual( 0, effects.Records.Single().Handle );
	}

	/// <summary>
	/// With nowhere to keep effects there is nothing to set a parameter on, so the instruction is counted
	/// rather than guessed.
	/// </summary>
	[TestMethod]
	public void SettingAParameterWithNowhereToKeepEffectsIsCounted()
	{
		var script = Script( 0,
			Word( Opcode.SETOBJPARAM ), Lit( 1 ), Lit( 20 ), Lit( 0 ),
			Word( Opcode.END ) );

		script.Turn( 0f );

		Assert.AreEqual( 1, script.NotImplemented, "the instruction was not counted" );
	}
}
