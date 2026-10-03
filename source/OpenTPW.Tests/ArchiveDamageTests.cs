using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class ArchiveDamageTests
{
	private static byte[] Wad( byte[] payload, bool compressed = false, int expanded = 0 )
	{
		using var bytes = new MemoryStream();
		using var writer = new BinaryWriter( bytes );
		writer.Write( Encoding.ASCII.GetBytes( "DWFB" ) );
		writer.Write( 2 );
		writer.Write( new byte[64] );
		writer.Write( 1 );
		writer.Write( 128 + payload.Length );
		writer.Write( 2 );
		writer.Write( 0 );
		foreach ( var word in new[] { 123, 128 + payload.Length, 2, 128, payload.Length, compressed ? 4 : 0, expanded, 0, 0, 0 } )
			writer.Write( word );
		writer.Write( payload );
		writer.Write( new byte[] { (byte)'x', 0 } );
		return bytes.ToArray();
	}

	private static byte[] Pack( int size, string commands ) =>
		new byte[] { 0x10, 0xfb, (byte)(size >> 16), (byte)(size >> 8), (byte)size }
			.Concat( Convert.FromHexString( commands ) ).ToArray();

	private static void Reject( Action action )
	{
		try { action(); }
		catch ( InvalidDataException ) { return; }
		catch ( EndOfStreamException ) { return; }
		Assert.Fail( "Damaged data must fail explicitly, not produce a partial result." );
	}

	private static byte[] Read( byte[] bytes )
	{
		using var archive = new WadArchive( new MemoryStream( bytes ) );
		return archive.GetFile( "x" ).GetData();
	}

	[TestMethod]
	public void EveryTruncatedArchivePrefixIsRejected()
	{
		var complete = Wad( [1, 2, 3] );
		CollectionAssert.AreEqual( new byte[] { 1, 2, 3 }, Read( complete ) );
		for ( var length = 0; length < complete.Length; ++length )
			Reject( () => Read( complete[..length] ) );
	}

	[DataTestMethod]
	[DataRow( 72, -1 )]
	[DataRow( 72, int.MaxValue )]
	[DataRow( 76, -1 )]
	[DataRow( 80, -1 )]
	[DataRow( 92, -1 )]
	[DataRow( 92, 0 )]
	[DataRow( 96, 0 )]
	[DataRow( 96, -1 )]
	[DataRow( 100, -1 )]
	[DataRow( 104, int.MaxValue )]
	[DataRow( 108, 5 )]
	public void InvalidDirectoryFieldsAreRejected( int offset, int value )
	{
		var bytes = Wad( [1, 2, 3] );
		BitConverter.GetBytes( value ).CopyTo( bytes, offset );
		Reject( () => Read( bytes ) );
	}

	[TestMethod]
	public void FilenameMustHaveItsTerminatingNul()
	{
		var bytes = Wad( [] );
		bytes[^1] = 1;
		Reject( () => Read( bytes ) );
	}

	[DataTestMethod]
	[DataRow( 0, "FC", "" )]
	[DataRow( 3, "FF616263", "abc" )]
	[DataRow( 4, "E061626364FC", "abcd" )]
	[DataRow( 4, "010041FC", "AAAA" )]
	[DataRow( 6, "06014142FC", "ABABAB" )]
	[DataRow( 5, "80400042FC", "BBBBB" )]
	[DataRow( 6, "C100000043FC", "CCCCCC" )]
	public void EveryCommandFamilyAndOverlappingCopiesDecode( int size, string commands, string expected ) =>
		CollectionAssert.AreEqual( Encoding.ASCII.GetBytes( expected ), Read( Wad( Pack( size, commands ), true, size ) ) );

	[DataTestMethod]
	[DataRow( 4, "00" )]
	[DataRow( 4, "8000" )]
	[DataRow( 4, "C00000" )]
	[DataRow( 4, "E0616263" )]
	[DataRow( 3, "FF6162" )]
	[DataRow( 3, "0000FC" )]
	[DataRow( 3, "E061626364FC" )]
	[DataRow( 5, "E061626364FC" )]
	[DataRow( 4, "E061626364" )]
	[DataRow( 0, "FC00" )]
	public void BrokenCompressionIsRejected( int size, string commands ) =>
		Reject( () => Read( Wad( Pack( size, commands ), true, size ) ) );

	[TestMethod]
	public void CompressionHeadersMustAgreeAndBeComplete()
	{
		Reject( () => Read( Wad( Pack( 0, "FC" ), true, 1 ) ) );
		Reject( () => Read( Wad( [0x10, 0xfb], true ) ) );
		Reject( () => Read( Wad( [0, 0, 0, 0, 0, 0xfc], true ) ) );
	}

	[TestMethod]
	public void PublicDataReadsValidateTheirSpan()
	{
		using var archive = new WadArchive( new MemoryStream( Wad( [] ) ) );
		Reject( () => archive.GetData( -1, 1 ) );
		Reject( () => archive.GetData( 0, -1 ) );
		Reject( () => archive.GetData( int.MaxValue, int.MaxValue ) );
	}

	[TestMethod]
	public void FilenameCannotContainAnEmbeddedNul()
	{
		var bytes = Wad( [] ).Concat( new byte[] { 0 } ).ToArray();
		BitConverter.GetBytes( 3 ).CopyTo( bytes, 80 );
		BitConverter.GetBytes( 3 ).CopyTo( bytes, 96 );
		Reject( () => Read( bytes ) );
	}

	private sealed class ShortReads( byte[] bytes ) : MemoryStream( bytes )
	{
		public override int Read( byte[] buffer, int offset, int count ) => base.Read( buffer, offset, Math.Min( count, 1 ) );
		public override int Read( Span<byte> buffer ) => base.Read( buffer[..Math.Min( buffer.Length, 1 )] );
	}

	[TestMethod]
	public void LegalShortReadsAreRetriedAndTheOwnedStreamIsClosed()
	{
		var stream = new ShortReads( Wad( [7, 8, 9] ) );
		using var archive = new WadArchive( stream );
		CollectionAssert.AreEqual( new byte[] { 7, 8, 9 }, archive.GetFile( "x" ).GetData() );
		Assert.IsFalse( stream.CanRead );
	}

	[TestMethod]
	public void FixedWidthReadsDoNotInventMissingBytes()
	{
		using var bytes = new ExpandedMemoryStream( [1, 2, 3] );
		Reject( () => bytes.ReadInt32() );
		Assert.AreEqual( 0L, bytes.Position, "reject before allocating or consuming a partial field" );
	}

	[TestMethod]
	public void EveryInstalledWadEntryStillDecodes()
	{
		GameData.Required();
		var archives = Directory.EnumerateFiles( GameDir.Data, "*", SearchOption.AllDirectories )
			.Where( path => path.EndsWith( ".wad", StringComparison.OrdinalIgnoreCase ) ).ToArray();
		Assert.IsTrue( archives.Length > 0 );
		var count = 0;
		var hashes = new List<string>();
		foreach ( var path in archives )
		{
			using var archive = new WadArchive( path );
			var pending = new Stack<string>();
			pending.Push( "" );
			while ( pending.TryPop( out var folder ) )
			{
				foreach ( var name in archive.GetFiles( folder ) )
				{
					var file = (WadArchiveFile)archive.GetFile( folder + name );
					var decoded = file.GetData();
					hashes.Add( Path.GetRelativePath( GameDir.Data, path ).Replace( '\\', '/' ).ToLowerInvariant()
						+ "\0" + (folder + name).ToLowerInvariant() + "\0"
						+ Convert.ToHexString( SHA256.HashData( decoded ) ).ToLowerInvariant() + "\n" );
					file.Free();
					++count;
				}
				foreach ( var name in archive.GetDirectories( folder ) ) pending.Push( folder + name + "/" );
			}
		}
		hashes.Sort( StringComparer.Ordinal );
		Console.WriteLine( "Decoded corpus SHA256: " + Convert.ToHexString( SHA256.HashData( Encoding.UTF8.GetBytes( string.Concat( hashes ) ) ) ) );
		Console.WriteLine( $"Decoded {count} entries in {archives.Length} installed WAD archives." );
	}
}
