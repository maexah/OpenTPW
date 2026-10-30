using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// Refpack decoding into an array of the declared size (Q836). The streams are made by a generator written from the
/// format's description alone (the command bit patterns of the FileFormats docs), which keeps its own copy of the output,
/// so the decoder is checked against a second reading of the format rather than against itself.
/// </summary>
[TestClass]
public class RefpackTests
{
	private sealed class Generated
	{
		public readonly List<byte> Bytes = new() { 0x10, 0xFB, 0, 0, 0 };
		public readonly List<byte> Expected = new();
		public int Overlaps, FarCopies, LongCopies;
		public readonly HashSet<string> Used = new();

		/// <summary>Appends <paramref name="literals"/> to the output as the command's own trailing bytes.</summary>
		private void Literals( Random random, int literals )
		{
			for ( var i = 0; i < literals; ++i )
			{
				var value = (byte)random.Next( 256 );
				Bytes.Add( value );
				Expected.Add( value );
			}
		}

		private void Copy( int length, int distance )
		{
			if ( distance < length ) ++Overlaps;
			if ( distance > 65536 ) ++FarCopies;
			if ( length > 255 ) ++LongCopies;
			for ( var i = 0; i < length; ++i )
				Expected.Add( Expected[Expected.Count - distance] );
		}

		public void Run( Random random )
		{
			var literals = random.Next( 4 );
			// A copy needs its source to exist after the literals.
			var available = Expected.Count + literals;
			switch ( random.Next( 5 ) )
			{
				case 0 when available >= 1:
				{
					var length = 3 + random.Next( 8 );
					var distance = 1 + random.Next( Math.Min( 1024, available ) );
					var d = distance - 1;
					Bytes.Add( (byte)(((d >> 8) << 5) | ((length - 3) << 2) | literals) );
					Bytes.Add( (byte)(d & 0xFF) );
					Literals( random, literals );
					Copy( length, distance );
					Used.Add( "two" );
					break;
				}
				case 1 when available >= 1:
				{
					var length = 4 + random.Next( 64 );
					var distance = 1 + random.Next( Math.Min( 16384, available ) );
					var d = distance - 1;
					Bytes.Add( (byte)(0x80 | (length - 4)) );
					Bytes.Add( (byte)((literals << 6) | (d >> 8)) );
					Bytes.Add( (byte)(d & 0xFF) );
					Literals( random, literals );
					Copy( length, distance );
					Used.Add( "three" );
					break;
				}
				case 2 when available >= 1:
				{
					var length = 5 + random.Next( 1024 );
					var distance = 1 + random.Next( Math.Min( 131072, available ) );
					var d = distance - 1;
					Bytes.Add( (byte)(0xC0 | ((d >> 16) << 4) | (((length - 5) >> 8) << 2) | literals) );
					Bytes.Add( (byte)((d >> 8) & 0xFF) );
					Bytes.Add( (byte)(d & 0xFF) );
					Bytes.Add( (byte)((length - 5) & 0xFF) );
					Literals( random, literals );
					Copy( length, distance );
					Used.Add( "four" );
					break;
				}
				default:
				{
					var blocks = 1 + random.Next( 28 );
					Bytes.Add( (byte)(0xE0 | (blocks - 1)) );
					Literals( random, blocks * 4 );
					Used.Add( "one" );
					break;
				}
			}
		}

		public byte[] Finish( Random random )
		{
			var literals = random.Next( 4 );
			Bytes.Add( (byte)(0xFC | literals) );
			Literals( random, literals );
			Used.Add( "stop" );
			var size = Expected.Count;
			Bytes[2] = (byte)(size >> 16);
			Bytes[3] = (byte)(size >> 8);
			Bytes[4] = (byte)size;
			return Bytes.ToArray();
		}
	}

	private static byte[] Pack( int size, string commands )
	{
		var bytes = new List<byte> { 0x10, 0xFB, (byte)(size >> 16), (byte)(size >> 8), (byte)size };
		bytes.AddRange( Convert.FromHexString( commands ) );
		return bytes.ToArray();
	}

	private static byte[] Decode( byte[] stream, int size ) => new Refpack( stream ).Decompress( (uint)size );

	[TestMethod]
	public void RandomStreamsOfEveryCommandDecodeToTheGeneratorsOutput()
	{
		var random = new Random( 836 );
		var overlaps = 0;
		var far = 0;
		var longs = 0;
		var used = new HashSet<string>();
		for ( var round = 0; round < 40; ++round )
		{
			var stream = new Generated();
			// Enough output that a copy can reach back past 65,536 bytes.
			while ( stream.Expected.Count < 200_000 )
				stream.Run( random );
			var packed = stream.Finish( random );

			var decoded = Decode( packed, stream.Expected.Count );

			Assert.AreEqual( stream.Expected.Count, decoded.Length, $"round {round}: the array is the declared size" );
			CollectionAssert.AreEqual( stream.Expected.ToArray(), decoded, $"round {round}" );
			overlaps += stream.Overlaps;
			far += stream.FarCopies;
			longs += stream.LongCopies;
			used.UnionWith( stream.Used );
		}

		Assert.AreEqual( 5, used.Count, "all five commands were generated" );
		Assert.IsTrue( overlaps > 100, "copies that overlap their own output were generated" );
		Assert.IsTrue( far > 100, "copies from more than 65,536 bytes back were generated" );
		Assert.IsTrue( longs > 100, "copies of more than 255 bytes were generated" );
	}

	[TestMethod]
	public void TheLongestOneByteRunIsALiteralRunAndTheNextOpcodeStops()
	{
		// 0xFB is the last one-byte command (112 literals, 0x70); 0xFC is the first stop.
		var literals = new byte[112];
		for ( var i = 0; i < literals.Length; ++i ) literals[i] = (byte)(i + 1);
		var stream = Pack( 112, "FB" + Convert.ToHexString( literals ) + "FC" );

		CollectionAssert.AreEqual( literals, Decode( stream, 112 ) );
	}

	[DataTestMethod]
	[DataRow( 3, "0100" + "41" + "FC", "a copy that ends past the declared size" )]
	[DataRow( 3, "E061626364FC", "literals that end past it" )]
	[DataRow( 4, "0000FC", "a copy from before the output" )]
	[DataRow( 7, "E0616263640004FC", "a copy that reaches one byte before the start" )]
	[DataRow( 4, "E061626364FC00", "a byte after the stop" )]
	public void DamagedStreamsAreRejectedWithoutWritingPastTheArray( int size, string commands, string why )
	{
		Assert.ThrowsException<InvalidDataException>( () => Decode( Pack( size, commands ), size ), why );
	}

	[TestMethod]
	public void ACopyMayReachBackExactlyToTheFirstByte()
	{
		// Four literals, then a two-byte command (distance 4, length 3) copies a, b, c.
		CollectionAssert.AreEqual( "abcdabc"u8.ToArray(), Decode( Pack( 7, "E061626364" + "0003" + "FC" ), 7 ) );
	}

	[TestMethod]
	public void ASizeThatDisagreesWithTheWadIsRejected()
	{
		Assert.ThrowsException<InvalidDataException>( () => Decode( Pack( 4, "E061626364FC" ), 5 ) );
	}
}
