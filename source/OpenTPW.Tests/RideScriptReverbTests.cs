using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <c>SETREVERB</c> (<c>0x0055649c</c>; <c>docs/exe/audio.md</c>, "The reverb") stores its operand at the script's
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
	/// the operand's word unsigned (65,535); no count (0); counted as a gap (<see cref="RideScript.NotImplemented"/> 3).
	/// </remarks>
	[TestMethod]
	public void EachLiteralIsStoredAndCounted()
	{
		var script = Script( 0,
			Word( Opcode.SETREVERB ), 8,
			Word( Opcode.SETREVERB ), 0,
			Word( Opcode.END ) );

		Assert.IsNull( script.Reverb, "nothing stored before the first" );

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
	/// <b>A variable operand stores nothing and is counted as a gap</b>, <c>SETREVERB_VARIABLE_OPERAND</c>: whether the original
	/// resolves it or stores the raw word, as <c>TURBO</c> does, is not read, and no shipped script names one.
	/// </summary>
	/// <remarks><b>Mutations:</b> the variable resolved (5 stored); its index stored (0); not counted (0, no gap).</remarks>
	[TestMethod]
	public void AVariableOperandIsCountedAndStoresNothing()
	{
		var script = Script( 1,
			Word( Opcode.COPY ), Var( 0 ), 5,
			Word( Opcode.SETREVERB ), Var( 0 ),
			Word( Opcode.END ) );

		var before = Times( "SETREVERB_VARIABLE_OPERAND" );
		var stored = Times( "SCRIPT_REVERB_LEVEL" );
		script.Turn( 0f );

		Assert.IsNull( script.Reverb, "nothing stored" );
		Assert.AreEqual( 1, Times( "SETREVERB_VARIABLE_OPERAND" ) - before, "counted once" );
		Assert.AreEqual( 0, Times( "SCRIPT_REVERB_LEVEL" ) - stored, "and not as a store" );
		Assert.AreEqual( 1, script.NotImplemented, "a gap of the script's" );
	}
}
