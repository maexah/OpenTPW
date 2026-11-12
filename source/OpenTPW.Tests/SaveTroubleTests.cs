using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// A save folder that cannot be written is told to the player (Q982). The folder is made unwritable by putting a file
/// where a folder must go, which fails the same way on every system and needs no permission to remove.
/// </summary>
[TestClass]
[DoNotParallelize]
public class SaveTroubleTests
{
	private string root = null!;
	private BaseFileSystem? oldSaves;
	private GameOptions oldOptions = null!;

	[TestInitialize]
	public void MakeFolder()
	{
		oldSaves = SaveFileSystemOrNull;
		oldOptions = GameOptions.Current;
		root = Directory.CreateTempSubdirectory( "opentpw-trouble-" ).FullName;
		Directory.CreateDirectory( Path.Combine( root, "save" ) );
		SaveFileSystem = new BaseFileSystem( Path.Combine( root, "save" ) );
		GameOptions.Current = new GameOptions();
		Drain();
	}

	[TestCleanup]
	public void Restore()
	{
		Drain();
		SaveFileSystemOrNull = oldSaves;
		GameOptions.Current = oldOptions;
		oldOptions.ApplySound();
		Directory.Delete( root, true );
	}

	private static void Drain()
	{
		SaveTrouble.Asked();

		while ( SaveTrouble.Pending )
			SaveTrouble.Take();

		SaveTrouble.NewVisit();
	}

	/// <summary>The theme's folder in the player's folder is a file, so no park can be written into it.</summary>
	private void BlockTheThemeFolder()
	{
		var player = Path.Combine( root, "save", "users", "1Test" );
		Directory.CreateDirectory( player );
		File.WriteAllText( Path.Combine( player, "jungle" ), "" );
	}

	/// <summary>The save folder itself is a file.</summary>
	private void BlockTheSaveFolder()
	{
		Directory.Delete( Path.Combine( root, "save" ), true );
		File.WriteAllText( Path.Combine( root, "save" ), "" );
	}

	[TestMethod]
	public void AParkThatCannotBeWrittenIsToldAsNotSaved()
	{
		BlockTheThemeFolder();

		Assert.IsNull( SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1, 2, 3] ) );
		Assert.IsTrue( SaveTrouble.Pending );

		var text = SaveTrouble.Take();

		Assert.IsNotNull( text );
		Assert.AreEqual( "PARK NOT SAVED\n\nThe save folder cannot be written.", text, "no path in the box (Q923): the log names it" );
		Assert.IsFalse( SaveTrouble.Pending );
	}

	/// <summary>
	/// <b>A park the writer refuses says so in words of its own</b> (Q1200): not the folder's sentence, told once a visit as the game's own
	/// failures are and every time where asked, and the folder's failure after it has its own sentence back.
	/// </summary>
	[TestMethod]
	public void AParkTheWriterRefusesIsToldAsNotSavedInItsOwnWords()
	{
		SaveTrouble.ParkRefused();
		Assert.IsTrue( SaveTrouble.Pending );
		Assert.AreEqual( $"PARK NOT SAVED\n\n{SaveTrouble.ParkRefusedWhy}", SaveTrouble.Take() );
		Assert.AreNotEqual( "The save folder cannot be written.", SaveTrouble.ParkRefusedWhy );

		SaveTrouble.ParkRefused();
		Assert.IsNull( SaveTrouble.Take(), "the game's own second one (the autosave) is not told again this visit" );

		SaveTrouble.ParkRefused();
		SaveTrouble.Asked();
		Assert.IsNotNull( SaveTrouble.Take(), "Save Park and Alt+S are told whenever" );

		BlockTheThemeFolder();
		SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1] );
		SaveTrouble.Asked();
		Assert.AreEqual( "PARK NOT SAVED\n\nThe save folder cannot be written.", SaveTrouble.Take(), "the folder's own words come back" );

		SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1] );
		SaveTrouble.ParkRefused();
		SaveTrouble.Asked();
		Assert.AreEqual( $"PARK NOT SAVED\n\n{SaveTrouble.ParkRefusedWhy}", SaveTrouble.Take(), "the later park's words are told" );
	}

	/// <summary>The notice's name is handed out with its text, once, and a failure carries none.</summary>
	[TestMethod]
	public void ANoticeKeepsItsNameUntilTaken()
	{
		SaveTrouble.Tell( "SOMETHING\n\nA sentence.", "kept" );
		Assert.AreEqual( "SOMETHING\n\nA sentence.", SaveTrouble.Take( out var name ) );
		Assert.AreEqual( "kept", name );

		BlockTheThemeFolder();
		SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1] );
		StringAssert.StartsWith( SaveTrouble.Take( out name ), "PARK NOT SAVED" );
		Assert.IsNull( name, "a failure has no name to count" );
		Assert.IsNull( SaveTrouble.Take( out name ) );
		Assert.IsNull( name );
	}

	[TestMethod]
	public void AParkThatIsWrittenTellsNothing()
	{
		Assert.IsNotNull( SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1, 2, 3] ) );

		Assert.IsFalse( SaveTrouble.Pending );
		Assert.IsNull( SaveTrouble.Take() );
	}

	[TestMethod]
	public void TheGamesOwnWriteIsToldOnceAVisitAndWhatTheyAskedForEveryTime()
	{
		BlockTheThemeFolder();

		SaveFolder.WritePark( 0, "Test", "jungle", "autosave", [1] );
		Assert.IsNotNull( SaveTrouble.Take(), "the first failure of a visit is told" );

		SaveFolder.WritePark( 0, "Test", "jungle", "autosave", [1] );
		Assert.IsNull( SaveTrouble.Take(), "the game's own second one is not" );

		SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1] );
		SaveTrouble.Asked();
		Assert.IsNotNull( SaveTrouble.Take(), "what the player asked for is told whenever it fails" );

		SaveFolder.WritePark( 0, "Test", "jungle", "autosave", [1] );
		Assert.IsNull( SaveTrouble.Take(), "being asked once does not make the game's own next one asked" );

		SaveFolder.WritePark( 0, "Test", "jungle", "autosave", [1] );
		SaveTrouble.NewVisit();
		Assert.IsNotNull( SaveTrouble.Take(), "a new scene is told again" );
	}

	[TestMethod]
	public void ANoticeIsToldOnceAndAheadOfAFailure()
	{
		BlockTheThemeFolder();
		SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1] );
		SaveTrouble.Tell( "SOMETHING\n\nA sentence." );

		Assert.IsTrue( SaveTrouble.Pending );
		Assert.AreEqual( "SOMETHING\n\nA sentence.", SaveTrouble.Take(), "the notice first" );
		Assert.IsTrue( SaveTrouble.Pending, "the failure is still waiting" );
		StringAssert.StartsWith( SaveTrouble.Take(), "PARK NOT SAVED" );
		Assert.IsFalse( SaveTrouble.Pending );
		Assert.IsNull( SaveTrouble.Take(), "told once" );
	}

	[TestMethod]
	public void ANoticeAloneIsPendingUntilTaken()
	{
		SaveTrouble.Tell( "SOMETHING\n\nA sentence." );

		Assert.IsTrue( SaveTrouble.Pending, "nothing failed, and the notice still waits to be told" );
		Assert.AreEqual( "SOMETHING\n\nA sentence.", SaveTrouble.Take() );
		Assert.IsFalse( SaveTrouble.Pending );
	}

	[TestMethod]
	public void AFileLeftAloneOnPurposeIsNotTroubleWithTheFolder()
	{
		SaveTrouble.Failed( "PLAYER", new InvalidDataException( "the player was not fully understood" ) );

		Assert.IsFalse( SaveTrouble.Pending );
	}

	[TestMethod]
	public void TheParkOutranksThePlayerWrittenBeforeIt()
	{
		SaveTrouble.Failed( "PLAYER", new IOException() );
		SaveTrouble.Failed( "PARK", new UnauthorizedAccessException() );
		SaveTrouble.Failed( "OPTIONS", new IOException() );

		StringAssert.StartsWith( SaveTrouble.Take(), "PARK NOT SAVED" );
	}

	[TestMethod]
	public void TheOptionsThatCannotBeWrittenAreToldAsOptions()
	{
		BlockTheSaveFolder();
		SaveFolder.SaveDisplay( tell: true );

		StringAssert.StartsWith( SaveTrouble.Take(), "OPTIONS NOT SAVED" );
	}

	[TestMethod]
	public void TheMachinesOptionsThatCannotBeWrittenAreToldAsOptions()
	{
		BlockTheSaveFolder();
		SaveFolder.SaveConfig();

		StringAssert.StartsWith( SaveTrouble.Take(), "OPTIONS NOT SAVED" );
	}

	[TestMethod]
	public void APlayerThatCannotBeWrittenIsToldAsAPlayer()
	{
		BlockTheSaveFolder();
		Assert.IsFalse( SaveFolder.SavePlayer( 0, "Test", new PlayerFile() ) );

		StringAssert.StartsWith( SaveTrouble.Take(), "PLAYER NOT SAVED" );
	}

	[TestMethod]
	public void TheLoadingScreensOwnWriteIsNotTold()
	{
		BlockTheSaveFolder();
		SaveFolder.SaveDisplay( tell: false );

		Assert.IsFalse( SaveTrouble.Pending );
	}

	[TestMethod]
	public void WithNoSaveFolderMountedTheQuitQuestionIsThePlainOne()
	{
		SaveFileSystemOrNull = null;

		Assert.AreEqual( "QUIT GAME", SaveTrouble.QuitQuestion( "QUIT GAME" ) );
	}

	[TestMethod]
	public void AFolderThatCannotBeWrittenIsToldBeforeQuitting()
	{
		const string Question = "QUIT GAME\nAre you sure?";

		Assert.IsTrue( SaveFolder.CanWrite() );
		Assert.AreEqual( Question, SaveTrouble.QuitQuestion( Question ) );
		Assert.AreEqual( 0, Directory.GetFileSystemEntries( Path.Combine( root, "save" ) ).Length, "the probe leaves nothing behind" );

		BlockTheSaveFolder();

		Assert.IsFalse( SaveFolder.CanWrite() );
		Assert.AreEqual( Question + "\nThe save folder cannot be written, so nothing is saved.", SaveTrouble.QuitQuestion( Question ) );
	}

	private static string Profile( string root ) => Path.Combine( root, "save", "users", "1Test", "gms.dat" );

	/// <summary>A gms.dat this game did not read whole is copied aside and told in a box with no path (Q998).</summary>
	/// <remarks><b>Mutation:</b> <c>BackUp</c> without <c>SaveTrouble.BackedUp</c>: nothing is pending.</remarks>
	[TestMethod]
	public void APlayerFileCopiedAsideIsToldInPlainWords()
	{
		Directory.CreateDirectory( Path.GetDirectoryName( Profile( root ) )! );
		File.WriteAllBytes( Profile( root ), [1, 2, 3] );

		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", new PlayerFile() ) );
		Assert.IsTrue( File.Exists( Profile( root ) + ".opentpw-backup" ) );
		Assert.IsTrue( SaveTrouble.Pending );
		Assert.IsFalse( SaveTrouble.LostAWrite, "nothing was lost: the write went through" );

		var text = SaveTrouble.Take( out var name );

		Assert.AreEqual( "PROGRESS FILE BACKED UP\n\nThe game didn't read all of your old progress, so it kept a copy before writing over it.", text );
		Assert.IsNull( name, "not a start notice: nothing to count in opentpw.cfg" );
		Assert.IsFalse( text!.Contains( root ) || text.Contains( "opentpw-backup" ) || text.Contains( '/' ) || text.Contains( '\\' ), "no folder or file path in the box" );
		Assert.IsFalse( SaveTrouble.Pending );
		Assert.IsNull( SaveTrouble.Take() );
	}

	/// <remarks><b>Mutation:</b> <c>SavePlayer</c> telling every write: the whole file is told too.</remarks>
	[TestMethod]
	public void AFileThatReadWholeIsWrittenOverInSilence()
	{
		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", new PlayerFile() ), "a new player" );
		var file = SaveFolder.LoadPlayer( 0, "Test" )!;
		Drain();

		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", file ) );
		Assert.IsFalse( File.Exists( Profile( root ) + ".opentpw-backup" ) );
		Assert.IsFalse( SaveTrouble.Pending );
	}

	[TestMethod]
	public void AConfigCopiedAsideIsToldAsOptionsAndOnceForBothFiles()
	{
		File.WriteAllBytes( Path.Combine( root, "save", "Config.tcf" ), [1, 2, 3] );
		var display = Path.Combine( root, "save", "opentpw.cfg" );
		File.WriteAllText( display, "display windowed\n" );

		// A read that fails (the file held open) leaves the defaults in memory, so its lines are copied aside at the next write.
		using ( new FileStream( display, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
			SaveFolder.LoadDisplay();

		SaveFolder.SaveConfig();
		SaveFolder.SaveDisplay( tell: false );

		Assert.IsTrue( File.Exists( Path.Combine( root, "save", "Config.tcf.opentpw-backup" ) ) );
		Assert.IsTrue( File.Exists( Path.Combine( root, "save", "opentpw.cfg.opentpw-backup" ) ) );
		Assert.AreEqual( "OPTIONS FILE BACKED UP\n\nThe game didn't read all of your old options, so it kept a copy before writing over it.", SaveTrouble.Take() );
		Assert.IsNull( SaveTrouble.Take(), "two files, one kind: one box" );
	}

	/// <summary>The start's notice comes first, then each backup, then a failure; each is one box.</summary>
	[TestMethod]
	public void ABackupIsToldAfterTheNoticeAndAheadOfAFailure()
	{
		BlockTheThemeFolder();
		SaveFolder.WritePark( 0, "Test", "jungle", "jungle", [1] );
		SaveTrouble.BackedUp( "PLAYER" );
		SaveTrouble.BackedUp( "OPTIONS" );
		SaveTrouble.Tell( "SOMETHING\n\nA sentence." );

		Assert.AreEqual( "SOMETHING\n\nA sentence.", SaveTrouble.Take() );
		StringAssert.StartsWith( SaveTrouble.Take(), "PROGRESS FILE BACKED UP" );
		StringAssert.StartsWith( SaveTrouble.Take(), "OPTIONS FILE BACKED UP" );
		StringAssert.StartsWith( SaveTrouble.Take(), "PARK NOT SAVED" );
		Assert.IsFalse( SaveTrouble.Pending );
	}

	/// <summary>A write that fails after a good copy is tried again without a second copy and so without a second box.</summary>
	[TestMethod]
	public void ARetriedWriteOfACopiedFileIsNotToldTwice()
	{
		Directory.CreateDirectory( Path.GetDirectoryName( Profile( root ) )! );
		File.WriteAllBytes( Profile( root ), [1, 2, 3] );
		File.WriteAllBytes( Profile( root ) + ".opentpw-backup", [1, 2, 3] );

		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", new PlayerFile(), expectMissing: true ) );
		Assert.IsFalse( File.Exists( Profile( root ) + ".opentpw-backup2" ) );
		Assert.IsFalse( SaveTrouble.Pending, "the copy was made and told by the earlier try" );
	}
}
