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

	/// <summary>Each notice is told once: it comes until it is in the told set, and one notice told does not hush the other.</summary>
	/// <remarks><b>Mutation:</b> both notices under one name.</remarks>
	[TestMethod]
	public void ANoticeIsToldOnceAndEachOnItsOwn()
	{
		var told = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var kept = new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName );
		var moved = new SaveHome.Pick( players, true, SaveHome.Notice(), SaveHome.MovedName );

		Assert.AreEqual( SaveHome.KeptNotice(), SaveHome.TellOnce( kept, told ) );
		told.Add( SaveHome.KeptName );
		Assert.IsNull( SaveHome.TellOnce( kept, told ), "not told again" );
		Assert.AreEqual( SaveHome.Notice(), SaveHome.TellOnce( moved, told ), "the other notice is its own" );
		told.Add( SaveHome.MovedName );
		Assert.IsNull( SaveHome.TellOnce( moved, told ) );
		Assert.IsNull( SaveHome.TellOnce( new SaveHome.Pick( game, false, null ), told ), "nothing to tell" );
	}

	/// <summary>A notice given to the box but never opened (the start failed, or the window closed in the load) is not counted told.</summary>
	/// <remarks><b>Mutation:</b> the name added to the set in <c>TellOnce</c> (or in <c>LoadDisplayAndTell</c>), where it was before the box opened.</remarks>
	[TestMethod]
	public void ANoticeGivenButNeverOpenedIsNotCountedTold()
	{
		var told = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		var kept = new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName );

		Assert.IsNotNull( SaveHome.TellOnce( kept, told ) );
		Assert.AreEqual( 0, told.Count, "a check counts nothing" );
		Assert.IsNotNull( SaveHome.TellOnce( kept, told ), "so it is still due" );
	}

	/// <summary>
	/// The start's whole path across starts: opentpw.cfg is read before the check, the notice is counted and written only once
	/// its box has opened, and the start after that has no box. A start that ends before the box leaves the notice due.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> the check before <c>LoadDisplay</c> in <c>LoadDisplayAndTell</c> (the box at every start); the write dropped from
	/// <c>Told</c> (the box at every start, unless a loading screen happened to write); the name added to the set at the start (the
	/// unopened start is counted, and a write by the loading screen between would reach the disk).
	/// </remarks>
	[TestMethod]
	public void ANoticeIsToldAtTheFirstStartThatOpensItsBoxAndNotAfter()
	{
		Directory.CreateDirectory( game );
		var kept = new SaveHome.Pick( game, false, SaveHome.KeptNotice(), SaveHome.KeptName );
		var was = GameOptions.Current;
		var wasSaves = SaveFileSystemOrNull;
		var file = Path.Join( game, "opentpw.cfg" );

		try
		{
			SaveFileSystem = new BaseFileSystem( game );
			DrainTrouble();

			// Start 1: the notice is given, and the start ends before the box opens. A write by something else in that gap
			// (the loading screen's, which writes the whole options set) must not carry a told line.
			GameOptions.Current = new();
			SaveHome.LoadDisplayAndTell( kept );
			Assert.IsTrue( SaveTrouble.Pending );
			SaveFolder.SaveDisplay( tell: false );
			Assert.IsFalse( File.ReadAllText( file ).Contains( "told" ), "given but not opened: no told line on disk" );
			DrainTrouble();

			// Start 2: still due; this time the box opens.
			GameOptions.Current = new();
			SaveHome.LoadDisplayAndTell( kept );
			Assert.IsTrue( SaveTrouble.Pending, "the notice came again" );
			Assert.AreEqual( SaveHome.KeptNotice(), SaveTrouble.Take( out var name ) );
			Assert.AreEqual( SaveHome.KeptName, name, "the name travels with the text" );
			SaveHome.Told( name! );
			StringAssert.Contains( File.ReadAllText( file ), "\ntold kept\n", "written as the box opens" );

			// Start 3: read back, so no box.
			GameOptions.Current = new();
			SaveHome.LoadDisplayAndTell( kept );
			Assert.IsFalse( SaveTrouble.Pending, "told: no box" );
		}
		finally
		{
			DrainTrouble();
			GameOptions.Current = was;
			SaveFileSystemOrNull = wasSaves;
		}
	}

	private static void DrainTrouble()
	{
		SaveTrouble.Asked();

		while ( SaveTrouble.Pending )
			SaveTrouble.Take();

		SaveTrouble.NewVisit();
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
			SaveHome.Told( SaveHome.KeptName );

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
	public void AFolderOfPlayersThatCannotBeWrittenIsLeftAloneAndTheNoticeNamesNoFolder()
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

		Directory.CreateDirectory( Path.Join( game, "users", "notes" ) );
		Assert.IsFalse( SaveHome.HoldsPlayers( game ), "a folder the scan skips is no player" );

		Directory.CreateDirectory( Path.Join( game, "users", "5Test" ) );
		Directory.CreateDirectory( Path.Join( game, "users", "1" ) );
		Assert.IsFalse( SaveHome.HoldsPlayers( game ), "slot 5 and a slot digit with no name are no player" );

		Directory.CreateDirectory( Path.Join( game, "users", "1Test" ) );
		Assert.IsTrue( SaveHome.HoldsPlayers( game ) );
		Assert.IsFalse( SaveHome.HoldsPlayers( Path.Join( root, "nowhere" ) ) );
	}

	/// <remarks><b>Mutation:</b> either bound of the slot digit moved, the length test dropped, or <see cref="SaveHome.HoldsPlayers"/> taking any folder again.</remarks>
	[TestMethod]
	[DataRow( "1A", true )]
	[DataRow( "4zz", true )]
	[DataRow( "3 Name", true )]
	[DataRow( "1", false )]
	[DataRow( "", false )]
	[DataRow( "0Test", false )]
	[DataRow( "5Test", false )]
	[DataRow( "notes", false )]
	public void APlayerFolderIsASlotDigitOneToFourThenAName( string folder, bool player )
		=> Assert.AreEqual( player, SaveFolder.IsPlayerFolderName( folder ) );

	[TestMethod]
	public void TheFallbackIsSaveInThePlayersDataFolder()
		=> Assert.AreEqual( Path.Join( UserFolders.Data(), "save" ), SaveHome.PlayersFolder() );
}
