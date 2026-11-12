using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// The script module's header block read short. <c>FUN_005597a0</c> reads the block over the scheduler's globals as far
/// as it goes, so a dword is taken only where the block holds all four of its bytes. These modules are built by hand and
/// hold no scripts, so they need no game.
/// </summary>
[TestClass]
public class ParkScriptStateHeaderTests
{
	/// <summary>The tick the built block holds at dword 1, where it holds that dword whole.</summary>
	private const int SavedTick = 6055;

	/// <summary>The next handle the built block holds at dword 2, where it holds that dword whole.</summary>
	private const int SavedNextHandle = 16;

	/// <summary>
	/// <b>Seven bytes hold no tick.</b> The tick's dword runs to byte eight; a rule that reads any block longer than its
	/// start would take three bytes of the block and one of the discarded dwords after it.
	/// </summary>
	[TestMethod]
	public void ABlockThatStopsInsideTheTickReadsNeither()
	{
		var states = new ParkScriptStates( Module( headerBytes: 7 ) );

		Assert.IsNull( states.Problem, "the module reads" );
		Assert.AreEqual( 0, states.Tick, "the tick's dword is not whole" );
		Assert.AreEqual( 0, states.NextHandle, "nor the next handle's" );
	}

	/// <summary><b>Eleven bytes hold the tick but not the next handle</b>, whose dword runs to byte twelve.</summary>
	[TestMethod]
	public void ABlockThatStopsInsideTheNextHandleReadsOnlyTheTick()
	{
		var states = new ParkScriptStates( Module( headerBytes: 11 ) );

		Assert.IsNull( states.Problem, "the module reads" );
		Assert.AreEqual( SavedTick, states.Tick, "the tick's dword is whole" );
		Assert.AreEqual( 0, states.NextHandle, "the next handle's is not" );
	}

	/// <summary>Twelve bytes hold both, which is what keeps the two above from passing on a rule that reads nothing.</summary>
	[TestMethod]
	public void TwelveBytesHoldBoth()
	{
		var states = new ParkScriptStates( Module( headerBytes: 12 ) );

		Assert.IsNull( states.Problem, "the module reads" );
		Assert.AreEqual( SavedTick, states.Tick );
		Assert.AreEqual( SavedNextHandle, states.NextHandle );
	}

	/// <summary>
	/// A module with no scripts: the trailer and magic, a header block of <paramref name="headerBytes"/>, the five
	/// discarded dwords, a count of nought and the smallest struct size the reader takes. Every byte of the block and the
	/// dwords after it is 0x11 except a whole tick or next-handle dword, so a dword read past the block's end is not nought.
	/// </summary>
	private static byte[] Module( int headerBytes )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		writer.Write( "RYLFRSSE"u8 );
		writer.Write( headerBytes );

		var block = new byte[headerBytes + (5 * 4)];
		Array.Fill( block, (byte)0x11 );

		if ( headerBytes >= 8 )
			BitConverter.TryWriteBytes( block.AsSpan( 4 ), SavedTick );

		if ( headerBytes >= 12 )
			BitConverter.TryWriteBytes( block.AsSpan( 8 ), SavedNextHandle );

		writer.Write( block );
		writer.Write( 0 );
		writer.Write( 51 * 4 );
		writer.Flush();

		return stream.ToArray();
	}
}
