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
		StringAssert.StartsWith( text, "PARK NOT SAVED\nThe save folder cannot be written." );
		Assert.IsFalse( SaveTrouble.Pending );
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
		SaveFolder.SaveDisplay();

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
}
