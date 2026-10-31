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

	/// <remarks><b>Mutation:</b> a <c>ReadBytes</c> that asks for one byte more than it is given fails the 47.</remarks>
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
			strings += new StringFile( Path.GetRelativePath( GameDir.Data, path ).Replace( '\\', '/' ) ).Entries.Length;

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
