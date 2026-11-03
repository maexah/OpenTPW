using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;

namespace OpenTPW.Tests;

/// <summary>
/// Where saves go when the game's own save folder cannot be written (Q984). A folder is made unwritable by putting a file
/// where it must go, or by taking the write permission from it (the mode bits off Windows, an explicit deny in the ACL on it).
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

	/// <summary>The folders a Windows test denied the account the right to create in, to be given back before the scratch is deleted.</summary>
	private readonly List<string> _denied = [];

	[TestCleanup]
	public void Restore()
	{
		if ( OperatingSystem.IsWindows() )
		{
			foreach ( var folder in _denied )
				AllowCreatingIn( folder );
		}
		else
		{
			foreach ( var folder in Directory.EnumerateDirectories( root, "*", SearchOption.AllDirectories ) )
				File.SetUnixFileMode( folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute );
		}

		Directory.Delete( root, true );
	}

	/// <summary>
	/// An explicit deny of creating files and folders in <paramref name="folder"/>, for the account that runs the test: a read-only
	/// attribute on a folder stops nothing, but a deny entry in its ACL does, whatever the account's other rights are.
	/// </summary>
	[System.Runtime.Versioning.SupportedOSPlatform( "windows" )]
	private static void DenyCreating( string folder )
	{
		var rule = new FileSystemAccessRule( System.Security.Principal.WindowsIdentity.GetCurrent().User!,
			FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.WriteData | FileSystemRights.AppendData,
			InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny );
		var info = new DirectoryInfo( folder );
		var security = info.GetAccessControl();

		security.AddAccessRule( rule );
		info.SetAccessControl( security );
	}

	[System.Runtime.Versioning.SupportedOSPlatform( "windows" )]
	private static void AllowCreatingIn( string folder )
	{
		var info = new DirectoryInfo( folder );

		if ( !info.Exists )
			return;

		var security = info.GetAccessControl();

		foreach ( FileSystemAccessRule rule in security.GetAccessRules( true, false, typeof( System.Security.Principal.SecurityIdentifier ) ) )
		{
			if ( rule.AccessControlType == AccessControlType.Deny )
				security.RemoveAccessRule( rule );
		}

		info.SetAccessControl( security );
	}

	/// <summary>Takes the write permission from the folder; false where the system keeps the right (a superuser).</summary>
	private bool TakeWriteFrom( string folder )
	{
		if ( OperatingSystem.IsWindows() )
		{
			DenyCreating( folder );
			_denied.Add( folder );
			return SaveHome.Unwritable( folder ) != null;
		}

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
		Assert.AreEqual( 0, Directory.GetFiles( game, ".write-test*" ).Length );
		Assert.AreEqual( 1, Directory.GetFileSystemEntries( game ).Length, "only users\\ is there" );
	}

	/// <remarks><b>Mutation:</b> the notice taken off the kept branch, <see cref="SaveHome.HoldsPlayers"/> asked of the game's folder, a path put in the words, or the word of safety taken out.</remarks>
	[TestMethod]
	public void AKeptFolderWhileThePlayersFolderHoldsPlayersSaysTheyAreSafeAndCopiesNothing()
	{
		Directory.CreateDirectory( game );
		Directory.CreateDirectory( Path.Join( players, "users", "1Old", "jungle" ) );
		File.WriteAllText( Path.Join( players, "users", "1Old", "gms.dat" ), "x" );

		var pick = SaveHome.Choose( game, players );

		Assert.AreEqual( game, pick.Folder );
		Assert.IsFalse( pick.Fallback );
		Assert.AreEqual( SaveHome.KeptName, pick.NoticeName );
		StringAssert.StartsWith( pick.Notice, "CAN'T SEE YOUR PLAYERS?\n\nThey are safe. Nothing was deleted." );
		AssertNoPath( pick.Notice!, game, players );
		Assert.AreEqual( 0, Directory.GetFileSystemEntries( game ).Length, "nothing is copied in (Q908)" );
		Assert.IsTrue( File.Exists( Path.Join( players, "users", "1Old", "gms.dat" ) ), "and the players left are untouched" );
	}

	/// <summary>A box a player reads at a glance holds no folder: a path wraps mid-word and a player cannot use it, so the log has both (Q922).</summary>
	private static void AssertNoPath( string notice, params string[] folders )
	{
		foreach ( var folder in folders )
			Assert.IsFalse( notice.Contains( folder, StringComparison.OrdinalIgnoreCase ), $"the notice names {folder}" );

		Assert.IsFalse( notice.Contains( '/' ) || notice.Contains( '\\' ), "no separator, so no path" );
	}

	[TestMethod]
	public void AKeptFolderWhileThePlayersFolderHoldsNoPlayersSaysNothing()
	{
		Directory.CreateDirectory( game );
		Directory.CreateDirectory( Path.Join( players, "users" ) );
		File.WriteAllText( Path.Join( players, "opentpw.cfg" ), "" );

		Assert.IsNull( SaveHome.Choose( game, players ).Notice, "an options file and an empty users folder are no players" );
	}

	/// <remarks><b>Mutation:</b> <c>Path.GetFullPath</c> taken out of the comparison, the trailing separator kept, or the case rule made exact on Windows.</remarks>
	[TestMethod]
	public void AKeptFolderThatIsThePlayersFolderToldNothingOfItself()
	{
		Directory.CreateDirectory( Path.Join( game, "users", "1Test" ) );

		Assert.IsNull( SaveHome.Choose( game, game + Path.DirectorySeparatorChar ).Notice, "a trailing separator is the same folder" );

		Directory.CreateDirectory( Path.Join( Path.GetDirectoryName( game )!, "elsewhere" ) );
		var viaParent = Path.Join( Path.GetDirectoryName( game )!, "elsewhere", "..", "save" );
		Assert.IsNull( SaveHome.Choose( game, viaParent ).Notice, "a spelling through .. is the same folder" );

		if ( OperatingSystem.IsWindows() )
			Assert.IsNull( SaveHome.Choose( game, game.ToUpperInvariant() ).Notice, "Windows ignores the case of a folder's name" );
	}

	/// <remarks><b>Mutation:</b> the case rule made loose on Linux, where a folder named in another case is another folder.</remarks>
	[TestMethod]
	public void OnLinuxAFolderInAnotherCaseIsAnotherFolder()
	{
		if ( !OperatingSystem.IsLinux() )
			Assert.Inconclusive( "only Linux's folders tell two cases apart for certain" );

		var other = Path.Join( Path.GetDirectoryName( game )!, "SAVE" );
		Directory.CreateDirectory( game );
		Directory.CreateDirectory( Path.Join( other, "users", "1Old" ) );

		Assert.IsNotNull( SaveHome.Choose( game, other ).Notice, "SAVE is not save, and it holds a player" );
	}

	/// <summary>Each notice is told once: the first start gets it and counts it told, the next gets none, and one notice told does not hush the other.</summary>
	/// <remarks><b>Mutation:</b> the name not added to the set, or both notices under one name.</remarks>
	[TestMethod]
	public void ANoticeIsToldOnceAndEachOnItsOwn()
	{
		var told = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var kept = new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName );
		var moved = new SaveHome.Pick( players, true, SaveHome.Notice(), SaveHome.MovedName );

		Assert.AreEqual( SaveHome.KeptNotice(), SaveHome.TellOnce( kept, told ) );
		Assert.IsNull( SaveHome.TellOnce( kept, told ), "not told again" );
		Assert.AreEqual( SaveHome.Notice(), SaveHome.TellOnce( moved, told ), "the other notice is its own" );
		Assert.IsNull( SaveHome.TellOnce( moved, told ) );
		Assert.IsNull( SaveHome.TellOnce( new SaveHome.Pick( game, false, null ), told ), "nothing to tell" );
	}

	/// <remarks><b>Mutation:</b> the line not read, not written, or written with no name.</remarks>
	[TestMethod]
	public void ANoticeToldIsRememberedInOpenTpwCfgAndAForgottenOneIsToldAgain()
	{
		Directory.CreateDirectory( game );
		var was = GameOptions.Current;
		var wasSaves = SaveFileSystemOrNull;

		try
		{
			SaveFileSystem = new BaseFileSystem( game );
			GameOptions.Current = new();
			Assert.IsNotNull( SaveHome.TellOnce( new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName ), GameOptions.Current.NoticesTold ) );
			SaveFolder.SaveDisplay( tell: false );

			var file = Path.Join( game, "opentpw.cfg" );
			StringAssert.Contains( File.ReadAllText( file ), "\ntold kept\n" );
			Assert.IsFalse( File.ReadAllText( file ).Contains( "told moved" ) );

			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.IsNull( SaveHome.TellOnce( new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName ), GameOptions.Current.NoticesTold ), "read back: told, not shown again" );

			File.WriteAllText( file, File.ReadAllText( file ).Replace( "told kept\n", "" ) );
			GameOptions.Current = new();
			SaveFolder.LoadDisplay();
			Assert.IsNotNull( SaveHome.TellOnce( new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName ), GameOptions.Current.NoticesTold ), "the line deleted: told again" );
		}
		finally
		{
			GameOptions.Current = was;
			SaveFileSystemOrNull = wasSaves;
		}
	}

	/// <summary>A players folder that cannot be listed is taken to hold none, and the log says so (a folder that is not there says nothing: it is the common case).</summary>
	/// <remarks><b>Mutation:</b> the warning taken out of the catch, or the not-there folder warned about.</remarks>
	[TestMethod]
	public void AFolderThatCannotBeListedIsTakenToHoldNoneAndSaysSoInTheLog()
	{
		Directory.CreateDirectory( players );
		var warned = new List<string>();
		Logger.LogDelegate hear = ( severity, text ) =>
		{
			if ( severity == Logger.Level.Warning )
				warned.Add( text );
		};

		Logger.OnLog += hear;

		try
		{
			Assert.IsFalse( SaveHome.HoldsPlayers( Path.Join( root, "nowhere" ) ) );
			Assert.AreEqual( 0, warned.Count, "a folder that is not there is the common case" );

			if ( OperatingSystem.IsWindows() )
			{
				Assert.Inconclusive( "a closed folder is made on Unix only here" );
				return;
			}

			File.SetUnixFileMode( players, UnixFileMode.None );

			try { _ = Directory.GetDirectories( players ); Assert.Inconclusive( "this system keeps the right to read a closed folder" ); }
			catch ( UnauthorizedAccessException ) { }

			Assert.IsFalse( SaveHome.HoldsPlayers( players ) );
			Assert.AreEqual( 1, warned.Count );
			StringAssert.Contains( warned[0], players );
		}
		finally
		{
			Logger.OnLog -= hear;
		}
	}

	/// <remarks><b>Mutation:</b> the probe back on the fixed name <c>.write-test</c> with <see cref="FileMode.Create"/>: the file is emptied and removed.</remarks>
	[TestMethod]
	public void AFileOfThePlayersNamedLikeTheOldProbeIsLeftAlone()
	{
		Directory.CreateDirectory( game );
		var theirs = Path.Join( game, ".write-test" );
		File.WriteAllText( theirs, "mine" );

		Assert.IsNull( SaveHome.Unwritable( game ) );

		Assert.AreEqual( "mine", File.ReadAllText( theirs ) );
		Assert.AreEqual( 1, Directory.GetFileSystemEntries( game ).Length );
	}

	/// <remarks><b>Mutation:</b> the probe on the fixed name with <see cref="FileShare.None"/>: a probe held open by another copy makes this one say unwritable.</remarks>
	[TestMethod]
	public void AProbeHeldOpenByAnotherCopyDoesNotMakeTheFolderUnwritable()
	{
		Directory.CreateDirectory( game );

		using var other = new FileStream( Path.Join( game, ".write-test" ), FileMode.Create, FileAccess.Write, FileShare.None );

		Assert.IsNull( SaveHome.Unwritable( game ) );
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
		Assert.AreEqual( SaveHome.MovedName, pick.NoticeName );
		StringAssert.StartsWith( pick.Notice, "YOUR GAME SAVES IN A NEW FOLDER\n\nPlayers saved before are safe" );
		AssertNoPath( pick.Notice!, game, players );
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
