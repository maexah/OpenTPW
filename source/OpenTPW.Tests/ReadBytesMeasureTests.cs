using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// <see cref="ExpandedMemoryStream.ReadBytes"/> throws where a field runs past the end of the stream; it once padded the
/// short read with nought. These read every shipped file the readers built on it open, to prove none of them ever
/// asked for bytes the file does not have (Q216). The counts are the game's: 47 sound banks, 42 string tables, and
/// the one park file in the data folder.
/// </summary>
[TestClass]
public class ReadBytesMeasureTests
{
	private static string[] Shipped( string pattern )
	{
		GameData.Required();
		var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
		return Directory.EnumerateFiles( GameDir.Data, pattern, options ).Order().ToArray();
	}

	[TestMethod]
	public void All47SoundBanksReadEveryMember()
	{
		var paths = Shipped( "*.sdt" );
		Assert.AreEqual( 47, paths.Length );

		var members = 0;

		foreach ( var path in paths )
		{
			using var archive = new SdtArchive( path );

			foreach ( var member in archive.soundFiles )
			{
				Assert.IsTrue( member.SoundData.Length > 0, $"{path}: {member.Name} holds no bytes" );
				Assert.AreEqual( BitConverter.ToInt32( member.Data, 4 ), member.SoundData.Length, $"{path}: {member.Name} is cut short of its header's size" );
				members++;
			}
		}

		Assert.IsTrue( members > 0 );
		Console.WriteLine( $"ReadBytes measure: {paths.Length} .sdt, {members} members, none short" );
	}

	[TestMethod]
	public void All42StringTablesRead()
	{
		FileSystem = GameData.Required();
		var paths = Shipped( "*.str" );
		Assert.AreEqual( 42, paths.Length );

		var strings = 0;

		foreach ( var path in paths )
		{
			var entries = new StringFile( Path.GetRelativePath( GameDir.Data, path ).Replace( '\\', '/' ) ).Entries.Length;
			strings += entries;

			// ReadByte answers -1 past the end, so the characters are checked against the file's size here.
			var bytes = File.ReadAllBytes( path );
			Assert.AreEqual( BitConverter.ToInt32( bytes, 8 ), entries, $"{path}: the header's count" );

			for ( var i = 0; i < entries; i++ )
			{
				var at = BitConverter.ToInt32( bytes, 12 + (4 * i) ) + 12;
				var length = bytes[at + 1] | (bytes[at + 2] << 8) | (bytes[at + 3] << 16);
				Assert.IsTrue( at + 4 + length <= bytes.Length, $"{path}: string {i} runs past the end" );
			}
		}

		Assert.IsTrue( strings > 0 );
		Console.WriteLine( $"ReadBytes measure: {paths.Length} .str, {strings} strings, none short" );
	}

	[TestMethod]
	public void TheShippedParkFileReads()
	{
		var paths = Shipped( "*.TPW*" );
		Assert.AreEqual( 1, paths.Length );

		foreach ( var path in paths )
		{
			var payload = new SaveReader( path ).ReadFile();
			Assert.IsTrue( payload.Length > 0 );
			Console.WriteLine( $"ReadBytes measure: {Path.GetFileName( path )}, {payload.Length} payload bytes, none short" );
		}
	}
}
