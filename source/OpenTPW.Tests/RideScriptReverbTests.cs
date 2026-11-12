using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <c>SETREVERB</c> (<c>0x0055649c</c>; <c>docs/exe/audio.md</c>, "The reverb") stores its resolved operand at the script's
/// <c>+0xd8</c> and nothing else; reverb being cut, the store is counted under a name of its own,
/// <c>SCRIPT_REVERB_LEVEL</c>, no longer as a gap at the dispatch's default (Q1236, from Q580).
/// </summary>
[TestClass]
[DoNotParallelize]
public class RideScriptReverbTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static int Var( int index ) => unchecked( (int)(0x40000000u | (uint)index) );

	private static int Times( string name ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == name ).Times;

	private static RideScript Script( int variableCount, params int[] body )
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

		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		var file = new RideScriptFile( new MemoryStream( memory.ToArray() ) );
		Assert.IsTrue( file.IsValid, "the hand-built script did not read - the builder is wrong, not the machine" );

		return new RideScript( file );
	}

	/// <summary>
	/// <b>Each literal is stored as it is, nought and a negative too, and counted once a store</b>: the Sun God's 8 then 0
	/// (<c>incagod</c> at 162 and 188) leave 0, a -1 leaves -1, three stores count three, and nothing is a gap.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the case left out (null, the dispatch's default counted, a gap); a store of non-zero only (8 kept);
	/// the operand's word unsigned (65,535); no count (0); counted as a gap (<see cref="RideScript.NotImplemented"/> 3); the level
	/// starting at null or nought (not the loader's -1).
	/// </remarks>
	[TestMethod]
	public void EachLiteralIsStoredAndCounted()
	{
		var script = Script( 0,
			Word( Opcode.SETREVERB ), 8,
			Word( Opcode.SETREVERB ), 0,
			Word( Opcode.END ) );

		Assert.AreEqual( -1, script.Reverb, "the loader's -1 before the first (0x00558c59)" );

		var before = Times( "SCRIPT_REVERB_LEVEL" );
		script.Turn( 0f );

		Assert.AreEqual( 0, script.Reverb, "8 then 0: the later store stands, nought included" );
		Assert.AreEqual( 2, Times( "SCRIPT_REVERB_LEVEL" ) - before, "one count a store" );
		Assert.AreEqual( 0, script.NotImplemented, "the store is built, not a gap" );

		var negative = Script( 0, Word( Opcode.SETREVERB ), 0xFFFF, Word( Opcode.END ) );
		negative.Turn( 0f );

		Assert.AreEqual( -1, negative.Reverb, "a literal's low sixteen bits, signed, as every literal is read" );
		Assert.AreEqual( 3, Times( "SCRIPT_REVERB_LEVEL" ) - before );
	}

	/// <summary>
	/// <b>A variable operand stores the variable's value</b>: the handler resolves it through the shared resolver
	/// (<c>0x005564a7</c> -> <c>FUN_005573a0</c>), as every operand here is (<c>RideScript.Value</c>), and counts the store.
	/// </summary>
	/// <remarks><b>Mutations:</b> a variable stores nothing (-1, the old counted gap); its index stored (0); the raw word stored;
	/// not counted (0).</remarks>
	[TestMethod]
	public void AVariableOperandStoresTheVariablesValue()
	{
		var script = Script( 1,
			Word( Opcode.COPY ), Var( 0 ), 5,
			Word( Opcode.SETREVERB ), Var( 0 ),
			Word( Opcode.END ) );

		var stored = Times( "SCRIPT_REVERB_LEVEL" );
		script.Turn( 0f );

		Assert.AreEqual( 5, script.Reverb, "the variable's value" );
		Assert.AreEqual( 1, Times( "SCRIPT_REVERB_LEVEL" ) - stored, "counted as a store" );
		Assert.AreEqual( 0, script.NotImplemented, "not a gap" );
	}
}
