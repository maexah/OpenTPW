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

	/// <summary>
	/// At a true end of file each reader keeps its buffer's length, takes the bytes that are there and leaves the
	/// unread tail nought, as before; a read that throws at the end (ReadExactly) fails this.
	/// </summary>
	[TestMethod]
	[DataRow( "SaveReader" )]
	[DataRow( "BFMUReader" )]
	[DataRow( "BFSTReader" )]
	[DataRow( "SoundFile" )]
	[DataRow( "SdtArchive" )]
	public void AStreamReadFromPartWayLeavesTheTailNought( string reader )
	{
		// The last four bytes are the magic (for an archive, a file count of one, whose offset reads as nought):
		// that is all the reader gets from a stream positioned at 60.
		var data = Sample( "" );
		var magic = reader == "BFMUReader" ? "BFMU" : reader == "BFSTReader" ? "BFST" : reader == "SdtArchive" ? "\x01\0\0\0" : "ABCD";
		for ( var i = 0; i < 4; i++ )
			data[60 + i] = (byte)magic[i];
		var stream = new MemoryStream( data ) { Position = 60 };

		byte[] buffer;
		switch ( reader )
		{
			case "SaveReader": buffer = new SaveReader( stream ).buffer; break;
			case "BFMUReader": buffer = new BFMUReader( stream ).buffer; break;
			case "BFSTReader": buffer = new BFSTReader( stream, "" ).buffer; break;
			case "SoundFile": buffer = new SoundFile( stream ).buffer; break;
			default:
				using ( var archive = new SdtArchive( stream ) )
					buffer = archive.buffer;
				break;
		}

		Assert.AreEqual( 64, buffer.Length, reader );
		CollectionAssert.AreEqual( data[60..], buffer[..4], reader );
		Assert.IsTrue( buffer[4..].All( b => b == 0 ), reader );
	}
}
