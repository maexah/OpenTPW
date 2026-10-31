using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where saves go when the game's own save folder cannot be written (Q984). A folder is made unwritable by putting a file
/// where it must go, or, off Windows, by taking the write permission from it.
/// </summary>
[TestClass]
[DoNotParallelize]
public class SaveHomeTests
{
	private string root = null!;
	private string game = null!;
	private string players = null!;

	[TestInitialize]
	public void MakeFolder()
	{
		root = Directory.CreateTempSubdirectory( "opentpw-home-" ).FullName;
		game = Path.Join( root, "game", "save" );
		players = Path.Join( root, "appdata", "OpenTPW", "save" );
		Directory.CreateDirectory( Path.GetDirectoryName( game )! );
	}

	[TestCleanup]
	public void Restore()
	{
		if ( !OperatingSystem.IsWindows() )
		{
			foreach ( var folder in Directory.EnumerateDirectories( root, "*", SearchOption.AllDirectories ) )
				File.SetUnixFileMode( folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute );
		}

		Directory.Delete( root, true );
	}

	/// <summary>Takes the write permission from the folder; false where the system keeps the right (a superuser, Windows).</summary>
	private bool TakeWriteFrom( string folder )
	{
		if ( OperatingSystem.IsWindows() )
			return false;

		File.SetUnixFileMode( folder, UnixFileMode.UserRead | UnixFileMode.UserExecute );
		return SaveHome.Unwritable( folder ) != null;
	}

	[TestMethod]
	public void AFolderThatCanBeWrittenIsKeptAndTheProbeLeavesNothing()
	{
		Directory.CreateDirectory( game );
		Directory.CreateDirectory( Path.Join( game, "users", "1Test" ) );

		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( game, pick.Folder );
		Assert.IsFalse( pick.Fallback );
		Assert.IsNull( pick.Notice, "nothing is lost to the player" );
		Assert.IsFalse( Directory.Exists( players ), "the player's folder is not made when it is not used" );
		Assert.IsFalse( File.Exists( Path.Join( game, ".write-test" ) ) );
		Assert.AreEqual( 1, Directory.GetFileSystemEntries( game ).Length, "only users\\ is there" );
	}

	[TestMethod]
	public void AFolderThatIsNotThereIsMadeAndKept()
	{
		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( game, pick.Folder );
		Assert.IsTrue( Directory.Exists( game ) );
		Assert.IsFalse( pick.Fallback );
	}

	[TestMethod]
	public void AFolderThatCannotBeMadeFallsBackToThePlayersWithNoNotice()
	{
		File.WriteAllText( game, "" );

		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( players, pick.Folder );
		Assert.IsTrue( pick.Fallback );
		Assert.IsNull( pick.Notice, "a fresh install holds no players, so the log says and the screen does not" );
		Assert.IsTrue( Directory.Exists( players ) );
		Assert.IsNull( SaveHome.Unwritable( players ) );
	}

	[TestMethod]
	public void AFolderWithNoRightToWriteInIsNotKept()
	{
		Directory.CreateDirectory( game );

		if ( !TakeWriteFrom( game ) )
			Assert.Inconclusive( "this system keeps the right to write into a read-only folder" );

		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( players, pick.Folder );
		Assert.IsTrue( pick.Fallback );
	}

	[TestMethod]
	public void AFolderOfPlayersThatCannotBeWrittenIsLeftAloneAndTheNoticeNamesTheNewOne()
	{
		Directory.CreateDirectory( Path.Join( game, "Users", "1Test", "jungle" ) );
		File.WriteAllText( Path.Join( game, "Users", "1Test", "gms.dat" ), "x" );

		if ( !TakeWriteFrom( game ) )
			Assert.Inconclusive( "this system keeps the right to write into a read-only folder" );

		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( players, pick.Folder );
		Assert.IsTrue( pick.Fallback );
		StringAssert.StartsWith( pick.Notice, "SAVES GO IN\n\n" + players );
		StringAssert.Contains( pick.Notice, "not copied" );
		Assert.AreEqual( 0, Directory.GetFileSystemEntries( players ).Length, "nothing is copied (Q902)" );
		Assert.IsTrue( File.Exists( Path.Join( game, "Users", "1Test", "gms.dat" ) ), "and the players left behind are untouched" );
	}

	[TestMethod]
	public void WhereNeitherCanBeMadeTheStartSaysBothReasons()
	{
		File.WriteAllText( game, "" );
		Directory.CreateDirectory( Path.Join( root, "appdata" ) );
		File.WriteAllText( Path.Join( root, "appdata", "OpenTPW" ), "" );

		var e = Assert.ThrowsException<IOException>( () => SaveHome.Choose( game, players ) );

		StringAssert.Contains( e.Message, game );
		StringAssert.Contains( e.Message, players );
	}

	[TestMethod]
	public void APlayersFolderThatExistsButCannotBeWrittenIsStillMappedForTheBoxesToTell()
	{
		File.WriteAllText( game, "" );
		Directory.CreateDirectory( players );

		if ( !TakeWriteFrom( players ) )
			Assert.Inconclusive( "this system keeps the right to write into a read-only folder" );

		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( players, pick.Folder );
	}

	[TestMethod]
	public void PlayersAreTheFoldersUnderUsersInAnyCase()
	{
		Directory.CreateDirectory( game );
		Assert.IsFalse( SaveHome.HoldsPlayers( game ) );

		Directory.CreateDirectory( Path.Join( game, "users" ) );
		Assert.IsFalse( SaveHome.HoldsPlayers( game ), "an empty users folder holds none" );

		File.WriteAllText( Path.Join( game, "users", "stray.txt" ), "" );
		Assert.IsFalse( SaveHome.HoldsPlayers( game ), "a file is no player" );

		Directory.CreateDirectory( Path.Join( game, "users", "1Test" ) );
		Assert.IsTrue( SaveHome.HoldsPlayers( game ) );
		Assert.IsFalse( SaveHome.HoldsPlayers( Path.Join( root, "nowhere" ) ) );
	}

	[TestMethod]
	public void TheFallbackIsSaveInThePlayersDataFolder()
		=> Assert.AreEqual( Path.Join( UserFolders.Data(), "save" ), SaveHome.PlayersFolder() );
}
