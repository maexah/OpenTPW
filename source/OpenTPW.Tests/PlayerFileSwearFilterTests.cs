using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The swear filter's load after a profile read (Q731; <c>FUN_005afd70</c>, <c>0x005afeba</c>..<c>0x005aff37</c>,
/// <c>docs/exe/saves.md</c>, the <c>+0x25</c> row): with the flag set, the swear list under the language's folder is opened,
/// and where it does not open the flag is cleared.
/// </summary>
[TestClass]
[DoNotParallelize]
public class PlayerFileSwearFilterTests
{
	private string root = null!;
	private BaseFileSystem? oldSaves;
	private GameOptions oldOptions = null!;

	[TestInitialize]
	public void MakeFolder()
	{
		oldSaves = SaveFileSystemOrNull;
		oldOptions = GameOptions.Current;
		root = Directory.CreateTempSubdirectory( "opentpw-swears-" ).FullName;
		Directory.CreateDirectory( Path.Combine( root, "save" ) );
		SaveFileSystem = new BaseFileSystem( Path.Combine( root, "save" ) );
		GameOptions.Current = new GameOptions();
	}

	[TestCleanup]
	public void Restore()
	{
		while ( SaveTrouble.Take() != null )
		{
		}

		SaveFileSystemOrNull = oldSaves;
		GameOptions.Current = oldOptions;
		Directory.Delete( root, true );
	}

	private static string Profile( string root ) => Path.Combine( root, "save", "users", "1Test", "gms.dat" );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>0x409 names the American folder, every other language the English one (<c>0x005c0d00</c>).</summary>
	/// <remarks><b>Mutations:</b> the folders swapped; English for every language.</remarks>
	[TestMethod]
	public void TheSwearListIsUnderAmericanFor0x409AndEnglishOtherwise()
	{
		Assert.AreEqual( @"data\Language\American\swears.txt", PlayerFile.SwearListPath( 0x409 ) );
		Assert.AreEqual( @"data\Language\English\swears.txt", PlayerFile.SwearListPath( 0x809 ) );
		Assert.AreEqual( @"data\Language\English\swears.txt", PlayerFile.SwearListPath( 0x40c ) );
		Assert.AreEqual( 0x409, SaveFolder.SwearListLanguage, "the US installer's value, as the reference install writes" );
	}

	/// <summary>A set flag is cleared where the list does not open and kept where it opens; a clear flag stays clear and opens nothing.</summary>
	/// <remarks><b>Mutations:</b> the flag set to 1 before the test (a clear flag would then come out 1 where the list opens);
	/// the test inverted; the list asked with the flag clear.</remarks>
	[TestMethod]
	public void ASetFlagIsClearedWhereTheSwearListDoesNotOpenAndAClearOneIsLeftAlone()
	{
		var asked = 0;

		var missing = new PlayerFile { FilterSwearing = true };
		missing.LoadSwearFilter( () => { ++asked; return false; } );
		Assert.IsFalse( missing.FilterSwearing, "set, the list missing: cleared" );
		Assert.AreEqual( 1, asked );

		var present = new PlayerFile { FilterSwearing = true };
		present.LoadSwearFilter( () => { ++asked; return true; } );
		Assert.IsTrue( present.FilterSwearing, "set, the list there: kept" );
		Assert.AreEqual( 2, asked );

		var clear = new PlayerFile { FilterSwearing = false };
		clear.LoadSwearFilter( () => { ++asked; return true; } );
		Assert.IsFalse( clear.FilterSwearing, "read clear: left clear even where the list opens" );
		Assert.AreEqual( 2, asked, "the list is opened only with the flag set" );
	}

	/// <summary>
	/// A player's load on the game's own folder: a profile written with the flag set is read back and written again, and
	/// gms.dat's byte 0x13 is 1 only where <c>data\Language\American\swears.txt</c> is there. A disc copy holds only
	/// <c>English\swears.txt</c>, so it writes 0 there, as the original does on such a folder.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>LoadPlayer</c> not running the load (1 on a disc copy); the English folder asked (1 on a disc copy).</remarks>
	[TestMethod]
	public void APlayersLoadOnTheGamesFolderWritesTheSwearByteByTheAmericanList()
	{
		GameData.Required();
		var american = File.Exists( GameDir.GetPath( PlayerFile.SwearListPath( 0x409 ) ) );

		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", new PlayerFile() ) );
		Assert.AreEqual( 1, File.ReadAllBytes( Profile( root ) )[0x13], "a new player is written with the filter on" );

		var file = SaveFolder.LoadPlayer( 0, "Test" )!;
		Assert.AreEqual( american, file.FilterSwearing );

		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", file ) );
		Assert.AreEqual( american ? 1 : 0, File.ReadAllBytes( Profile( root ) )[0x13] );
	}

	/// <summary>A read that stopped early keeps what it read and counts the unread step rather than guessing it.</summary>
	/// <remarks><b>Mutation:</b> the load run after a short read: the flag comes out 0 where no list is under the folder.</remarks>
	[TestMethod]
	public void AShortReadKeepsItsFlagAndIsCounted()
	{
		Directory.CreateDirectory( Path.GetDirectoryName( Profile( root ) )! );

		using ( var memory = new MemoryStream() )
		{
			new PlayerFile().Write( memory );
			File.WriteAllBytes( Profile( root ), memory.ToArray()[..0x20] );
		}

		var before = Times( "PLAYER_SWEAR_FILTER_AFTER_SHORT_READ" );
		var file = SaveFolder.LoadPlayer( 0, "Test" )!;

		Assert.IsFalse( file.IsComplete );
		Assert.IsTrue( file.FilterSwearing, "byte 0x13 was read as 1 before the file ended" );
		Assert.AreEqual( before + 1, Times( "PLAYER_SWEAR_FILTER_AFTER_SHORT_READ" ) );
	}
}
