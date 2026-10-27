using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The readers that take a whole stream into a buffer: a stream that hands out one byte a call (a pipe, a
/// compressed file) must give the same buffer as a stream that hands it all out at once.
/// </summary>
[TestClass]
public class ShortReadTests
{
	/// <summary>A stream that answers every read with at most one byte.</summary>
	private sealed class OneByteStream : MemoryStream
	{
		public OneByteStream( byte[] data ) : base( data ) { }
		public override int Read( byte[] buffer, int offset, int count ) => base.Read( buffer, offset, Math.Min( count, 1 ) );
		public override int Read( Span<byte> buffer ) => base.Read( buffer[..Math.Min( buffer.Length, 1 )] );
	}

	private static byte[] Sample( string magic )
	{
		var data = new byte[64];
		for ( var i = 0; i < data.Length; i++ )
			data[i] = (byte)(i + 1);
		for ( var i = 0; i < magic.Length; i++ )
			data[i] = (byte)magic[i];
		return data;
	}

	[TestMethod]
	public void SaveReaderTakesEveryByteFromAOneByteStream()
	{
		var data = Sample( "ABCD" );
		var reader = new SaveReader( new OneByteStream( data ) );
		CollectionAssert.AreEqual( data, reader.buffer );
	}

	[TestMethod]
	public void BfmuReaderTakesEveryByteFromAOneByteStream()
	{
		var data = Sample( "BFMU" );
		var reader = new BFMUReader( new OneByteStream( data ) );
		CollectionAssert.AreEqual( data, reader.buffer );
	}

	[TestMethod]
	public void BfstReaderTakesEveryByteFromAOneByteStream()
	{
		var data = Sample( "BFST" );
		var reader = new BFSTReader( new OneByteStream( data ), "" );
		CollectionAssert.AreEqual( data, reader.buffer );
	}

	[TestMethod]
	public void SoundFileTakesEveryByteFromAOneByteStream()
	{
		var data = Sample( "ABCD" );
		var reader = new SoundFile( new OneByteStream( data ) );
		CollectionAssert.AreEqual( data, reader.buffer );
	}

	[TestMethod]
	public void SdtArchiveTakesEveryByteFromAOneByteStream()
	{
		var data = new byte[64]; // a file count of nought
		data[8] = 0x7F;
		using var archive = new SdtArchive( new OneByteStream( data ) );
		CollectionAssert.AreEqual( data, archive.buffer );
	}

	/// <summary>At a true end of file the buffer keeps its length and its unread tail stays nought, as before.</summary>
	[TestMethod]
	public void AStreamReadFromPartWayLeavesTheTailNought()
	{
		var data = Sample( "ABCD" );
		var stream = new MemoryStream( data ) { Position = 60 };
		var reader = new SaveReader( stream );
		Assert.AreEqual( 64, reader.buffer.Length );
		CollectionAssert.AreEqual( data[60..], reader.buffer[..4] );
		Assert.IsTrue( reader.buffer[4..].All( b => b == 0 ) );
	}
}
