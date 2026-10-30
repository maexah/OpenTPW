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

	/// <summary>An archive of two plain entries, "x" and "y".</summary>
	private static byte[] TwoEntryWad( byte[] x, byte[] y )
	{
		using var bytes = new MemoryStream();
		using var writer = new BinaryWriter( bytes );
		var data = 88 + 80;
		var names = data + x.Length + y.Length;
		writer.Write( Encoding.ASCII.GetBytes( "DWFB" ) );
		writer.Write( 2 );
		writer.Write( new byte[64] );
		writer.Write( 2 );
		writer.Write( names );
		writer.Write( 4 );
		writer.Write( 0 );
		foreach ( var word in new[] { 123, names, 2, data, x.Length, 0, 0, 0, 0, 0 } )
			writer.Write( word );
		foreach ( var word in new[] { 123, names + 2, 2, data + x.Length, y.Length, 0, 0, 0, 0, 0 } )
			writer.Write( word );
		writer.Write( x );
		writer.Write( y );
		writer.Write( new byte[] { (byte)'x', 0, (byte)'y', 0 } );
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
		return Entry( archive, "x" ).GetData();
	}

	/// <summary>The entry at <paramref name="path"/>, which the test needs to be there.</summary>
	private static ArchiveFile Entry( WadArchive archive, string path )
		=> archive.GetFile( path ) ?? throw new AssertFailedException( $"{path} is not in the archive" );

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
		CollectionAssert.AreEqual( new byte[] { 7, 8, 9 }, Entry( archive, "x" ).GetData() );
		Assert.IsFalse( stream.CanRead );
	}

	[TestMethod]
	public void FixedWidthReadsDoNotInventMissingBytes()
	{
		using var bytes = new ExpandedMemoryStream( [1, 2, 3] );
		Reject( () => bytes.ReadInt32() );
		Assert.AreEqual( 0L, bytes.Position, "reject before allocating or consuming a partial field" );
	}

	/// <summary>A WAD written to a temporary file, opened by its path; the file is gone when the test ends.</summary>
	private sealed class TempWad : IDisposable
	{
		public string Path { get; } = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"opentpw-wad-{Guid.NewGuid():N}.wad" );

		public TempWad( byte[] bytes ) => File.WriteAllBytes( Path, bytes );

		public void Dispose() => File.Delete( Path );
	}

	[TestMethod]
	public void AnArchiveOnDiskHoldsNoneOfItsBytesAndReadsAnEntryFromTheFileWhenAsked()
	{
		var bytes = Wad( [7, 8, 9] );
		using var wad = new TempWad( bytes );
		using var onDisk = new WadArchive( wad.Path );
		using var fromStream = new WadArchive( new MemoryStream( bytes ) );

		Assert.AreEqual( 0L, onDisk.HeldBytes, "the file is not held" );
		Assert.AreEqual( (long)bytes.Length, fromStream.HeldBytes, "a stream is consumed, so its bytes are" );
		CollectionAssert.AreEqual( new byte[] { 7, 8, 9 }, Entry( onDisk, "x" ).GetData() );
		CollectionAssert.AreEqual( fromStream.GetData( 128, 3 ), onDisk.GetData( 128, 3 ) );
		Reject( () => onDisk.GetData( bytes.Length - 1, 2 ) );
	}

	[TestMethod]
	public void AnArchiveOnDiskIsRejectedWhereAStreamIsAndItsFileIsLetGo()
	{
		var complete = Wad( [1, 2, 3] );
		for ( var length = 0; length < complete.Length; ++length )
		{
			using var wad = new TempWad( complete[..length] );
			Assert.ThrowsException<InvalidDataException>( () => new WadArchive( wad.Path ).Dispose(), $"a {length}-byte file is a damaged archive, found when it is opened" );
			Assert.IsTrue( CanBeTakenAlone( wad.Path ), $"a rejected {length}-byte archive left its file open" );
		}

		using var good = new TempWad( complete );
		new WadArchive( good.Path ).Dispose();
		Assert.IsTrue( CanBeTakenAlone( good.Path ), "a disposed archive left its file open" );
	}

	/// <summary>Whether nothing has the file open: the runtime's share modes, which Linux holds as locks too, refuse a writer that shares with no one.</summary>
	private static bool CanBeTakenAlone( string path )
	{
		try
		{
			using var alone = new FileStream( path, FileMode.Open, FileAccess.Write, FileShare.None );
			return true;
		}
		catch ( IOException )
		{
			return false;
		}
	}

	[TestMethod]
	[Timeout( 10000 )]
	public void AnArchiveOnDiskFailsAnEntryReadAfterItsFileShrank()
	{
		var bytes = Wad( [1, 2, 3, 4, 5, 6] );
		using var wad = new TempWad( bytes );
		using var archive = new WadArchive( wad.Path );

		// On Windows the archive's FileShare.Read keeps every writer out, so the file cannot shrink under it.
		if ( OperatingSystem.IsWindows() )
		{
			Assert.ThrowsException<IOException>( () => new FileStream( wad.Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite ).Dispose(), "FileShare.Read keeps writers out" );
			return;
		}

		using ( var file = new FileStream( wad.Path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite ) )
			file.SetLength( 130 );
		Reject( () => Entry( archive, "x" ).GetData() );
	}

	[TestMethod]
	public void AnEntrysSizeIsReadFromTheTableNotByDecodingIt()
	{
		using var plain = new WadArchive( new MemoryStream( Wad( [1, 2, 3] ) ) );
		Assert.AreEqual( 3L, plain.GetFileSize( "x" ) );
		using var compressed = new WadArchive( new MemoryStream( Wad( Pack( 4, "E061626364FC" ), true, 4 ) ) );
		Assert.AreEqual( 4L, compressed.GetFileSize( "x" ) );
		Assert.AreEqual( 0, compressed.Cached().Entries, "the size was read without decoding" );
		Assert.AreEqual( 0L, compressed.GetFileSize( "missing" ) );
	}

	[TestMethod]
	public void AnEntryAskedForOnceIsNotKeptAndOneAskedForAgainIsKeptFromThatAsk()
	{
		using var archive = new WadArchive( new MemoryStream( Wad( Pack( 4, "E061626364FC" ), true, 4 ) ) );
		var file = (WadArchiveFile)Entry( archive, "x" );

		var first = file.GetData();
		CollectionAssert.AreEqual( new byte[] { 0x61, 0x62, 0x63, 0x64 }, first );
		Assert.AreEqual( 0, archive.Cached().Entries, "the first ask keeps nothing" );
		Assert.AreEqual( 0, archive.MostAsked( 3 ).Count(), "an entry asked for once is not among the most asked" );
		Assert.AreEqual( (1, 1L, 1L), (archive.Asked().Entries, archive.Asked().Asks, archive.Asked().Reads) );

		var second = file.GetData();
		CollectionAssert.AreEqual( first, second, "the second ask returns the same bytes" );
		Assert.AreEqual( 1, archive.Cached().Entries, "the second ask keeps them" );
		Assert.AreEqual( 4L, archive.Cached().Compressed );
		Assert.AreEqual( 2L, archive.Asked().Reads, "the second ask read the file again" );

		var third = file.GetData();
		Assert.AreSame( second, third, "the third ask is answered from the kept bytes" );
		Assert.AreEqual( (3L, 2L), (archive.Asked().Asks, archive.Asked().Reads) );

		file.Free();
		Assert.AreEqual( 0, archive.Cached().Entries, "Free lets them go" );
	}

	/// <summary>
	/// Q826: a level's end starts every archive's asks again, so an entry asked for once in one level and once in the next is
	/// asked for once each time and never kept. <b>Mutations:</b> <see cref="Level.ForgetWadAsks"/> that frees the bytes and
	/// leaves the counts, that resets the counts and leaves the bytes, that skips an archive, or an entry that forgets
	/// only its reads, each fail here.
	/// </summary>
	[TestMethod]
	public void AnEntryAskedForOnceInEachOfTwoLevelsIsNeverKept()
	{
		using var first = new WadArchive( new MemoryStream( Wad( [1, 2, 3, 4] ) ) );
		using var second = new WadArchive( new MemoryStream( Wad( Pack( 4, "E061626364FC" ), true, 4 ) ) );
		var archives = new IArchive[] { first, second };
		var files = archives.Cast<WadArchive>().Select( a => (WadArchiveFile)Entry( a, "x" ) ).ToArray();

		foreach ( var file in files )
			file.GetData();

		Level.ForgetWadAsks( archives );

		foreach ( var wad in archives.Cast<WadArchive>() )
			Assert.AreEqual( (0, 0L, 0L, 0.0), wad.Asked(), "the counts start again" );

		foreach ( var file in files )
			Assert.AreEqual( (0, 0L), (file.Reads, file.ReadTicks), "the reads and their time start again" );

		foreach ( var file in files )
			file.GetData();

		foreach ( var wad in archives.Cast<WadArchive>() )
		{
			Assert.AreEqual( 0, wad.Cached().Entries, "one ask in the next level keeps nothing" );
			Assert.AreEqual( (1, 1L, 1L), (wad.Asked().Entries, wad.Asked().Asks, wad.Asked().Reads) );
		}
	}

	/// <summary>Q826: what the level before kept is let go of at its end, not left until the next level asks again.</summary>
	[TestMethod]
	public void AnEntryKeptInOneLevelIsLetGoOfAtItsEnd()
	{
		using var archive = new WadArchive( new MemoryStream( Wad( Pack( 4, "E061626364FC" ), true, 4 ) ) );
		var file = (WadArchiveFile)Entry( archive, "x" );
		file.GetData();
		var kept = file.GetData();
		Assert.AreEqual( 1, archive.Cached().Entries );

		Level.ForgetWadAsks( [archive] );

		Assert.AreEqual( 0, archive.Cached().Entries, "the kept bytes are gone" );
		Assert.AreEqual( 0, file.Asks );
		CollectionAssert.AreEqual( kept, file.GetData(), "and the entry still reads" );
		Assert.AreEqual( 0, archive.Cached().Entries, "the first ask of the next level keeps nothing" );
	}

	/// <summary>
	/// Q826: <c>Level.Unload</c> ends the level by calling <see cref="Level.ForgetWadAsks"/> on the open archives, after
	/// <c>ForgetRunningPark()</c>. No test can run <c>Unload</c>, so its source is read, as
	/// <c>ParkCoasterPylonsTests.TheLevelMakesThePylonsAndTheConsoleListsThem</c> reads <c>Level.cs</c>.
	/// <b>Mutation:</b> the call removed from <c>Unload</c>.
	/// </summary>
	[TestMethod]
	public void LevelUnloadEndsWithForgetWadAsks()
	{
		var source = File.ReadAllText( Path.Join( RepoFiles.Root(), "source", "OpenTPW", "World", "Level.cs" ) );
		var start = source.IndexOf( "\tpublic void Unload()", StringComparison.Ordinal );
		Assert.IsTrue( start >= 0, "Unload is found" );
		var end = source.IndexOf( "\n\t}", start, StringComparison.Ordinal );
		var body = source.Substring( start, end - start );

		StringAssert.Contains( body, "ForgetRunningPark();\n\t\tForgetWadAsks( FileSystem.OpenArchives );" );
	}

	/// <summary>Q826: every entry of an archive starts again, not the first one alone.</summary>
	[TestMethod]
	public void EveryEntryOfAnArchiveStartsAgainAtALevelsEnd()
	{
		using var archive = new WadArchive( new MemoryStream( TwoEntryWad( [1, 2], [3, 4, 5] ) ) );
		var files = new[] { "x", "y" }.Select( name => (WadArchiveFile)Entry( archive, name ) ).ToArray();
		CollectionAssert.AreEqual( new byte[] { 3, 4, 5 }, files[1].GetData() );

		foreach ( var file in files )
			file.GetData();

		Assert.AreEqual( 1, archive.Cached().Entries, "y was asked for twice and kept, x once" );

		Level.ForgetWadAsks( [archive] );

		Assert.AreEqual( (0, 0L, 0L, 0.0), archive.Asked() );
		Assert.AreEqual( 0, archive.Cached().Entries );
		Assert.AreEqual( 0, files[1].Asks, "the second entry's count too" );
	}

	[TestMethod]
	public void EntriesAskedForOnceAreCountedApartFromTheKept()
	{
		using var archive = new WadArchive( new MemoryStream( Wad( [1, 2, 3, 4, 5, 6] ) ) );
		var file = (WadArchiveFile)Entry( archive, "x" );
		file.GetData();
		file.GetData();

		Assert.AreEqual( 0, archive.Cached( asks => asks == 1 ).Entries, "an entry asked for twice is not one asked for once" );
		Assert.AreEqual( 1, archive.Cached( asks => asks == 2 ).Entries );
		Assert.AreEqual( 6L, archive.Cached( asks => asks == 2 ).Plain );
		Assert.AreEqual( "x", archive.MostAsked( 3 ).Single().Name );
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
					var file = (WadArchiveFile)Entry( archive, folder + name );
					var decoded = file.GetData();
					Assert.AreEqual( (long)decoded.Length, archive.GetFileSize( folder + name ), $"{path}: {folder}{name}" );
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
