using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>The little-endian reads on the stream the archive readers share.</summary>
[TestClass]
public class ExpandedMemoryStreamTests
{
	/// <summary>
	/// A sixteen-bit read takes two bytes and leaves the next two where they are, so two shorts read in a
	/// row come back as the two fields they are.
	/// </summary>
	[TestMethod]
	public void ASixteenBitReadTakesTwoBytesAndLeavesTheRest()
	{
		var stream = new ExpandedMemoryStream( new byte[] { 0x34, 0x12, 0x78, 0x56 } );

		Assert.AreEqual( 0x1234, stream.ReadInt16() );
		Assert.AreEqual( 2, stream.Position, "two bytes consumed, not four" );
		Assert.AreEqual( 0x5678, stream.ReadInt16() );
	}
}
