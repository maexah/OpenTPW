using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// Real selection/save paths over disposable files; no installed save folder is written. A Config.tcf or gms.dat the game
/// did not read whole is copied beside itself (<c>.opentpw-backup</c>) and then written over, as the original writes it
/// (Q216).
/// </summary>
[TestClass]
public class ProfilePreservationTests
{
	private string root = null!;
	private BaseFileSystem? oldData;
	private BaseFileSystem? oldSaves;
	private GameOptions oldOptions = null!;
	private object? oldThemes;
	private static readonly FieldInfo Themes = typeof( SaveFolder ).GetField( "_themes", BindingFlags.NonPublic | BindingFlags.Static )!;
	private string Profile => Path.Combine( root, "save", "users", "1Test", "gms.dat" );
	private string Backup => Profile + ".opentpw-backup";

	[TestInitialize]
	public void IsolateFiles()
	{
		oldData = FileSystemOrNull;
		oldSaves = SaveFileSystemOrNull;
		oldOptions = GameOptions.Current;
		oldThemes = Themes.GetValue( null );
		root = Directory.CreateTempSubdirectory( "opentpw-profiles-" ).FullName;
		Directory.CreateDirectory( Path.Combine( root, "data", "levels", "jungle" ) );
		Directory.CreateDirectory( Path.GetDirectoryName( Profile )! );
		File.WriteAllText( Path.Combine( root, "data", "levels", "jungle", "global.sam" ), "" );
		FileSystem = new BaseFileSystem( Path.Combine( root, "data" ) );
		SaveFileSystem = new BaseFileSystem( Path.Combine( root, "save" ) );
		GameOptions.Current = new GameOptions();
		Themes.SetValue( null, null );
	}

	/// <summary>Lets go of everything the save folder wanted to tell (a backup is told too, Q998), so no test hands it to the next.</summary>
	private static void DrainTrouble()
	{
		SaveTrouble.Asked();

		while ( SaveTrouble.Pending )
			SaveTrouble.Take();

		SaveTrouble.NewVisit();
	}

	[TestCleanup]
	public void RestoreFiles()
	{
		DrainTrouble();
		FileSystemOrNull = oldData;
		SaveFileSystemOrNull = oldSaves;
		GameOptions.Current = oldOptions;
		oldOptions.ApplySound();
		Themes.SetValue( null, oldThemes );
		if ( root != null )
			Directory.Delete( root, true );
	}

	private PlayerFile Saved() => PlayerFile.Read( new MemoryStream( File.ReadAllBytes( Profile ) ) );

	private static byte[] Written( PlayerFile file )
	{
		using var stream = new MemoryStream();
		file.Write( stream );
		return stream.ToArray();
	}

	private static byte[] UnreadPlayer( string kind )
	{
		var bytes = Written( new PlayerFile { KeysGiven = 7 } );
		switch ( kind )
		{
			case "empty": return [];
			case "truncated": return bytes[..25];
			case "older": BitConverter.GetBytes( 11 ).CopyTo( bytes, 0 ); break;
			case "newer": BitConverter.GetBytes( 13 ).CopyTo( bytes, 0 ); break;
			case "tail": return bytes.Concat( new byte[] { 0xA5, 0x5A } ).ToArray();
			case "bad count": BitConverter.GetBytes( -1 ).CopyTo( bytes, 21 ); break;
		}
		return bytes;
	}

	[DataTestMethod]
	[DataRow( "empty" )]
	[DataRow( "truncated" )]
	[DataRow( "older" )]
	[DataRow( "newer" )]
	[DataRow( "tail" )]
	[DataRow( "bad count" )]
	public void SelectingAndLeavingAnUnreadPlayerBacksItUpThenWritesIt( string kind )
	{
		var bytes = UnreadPlayer( kind );
		File.WriteAllBytes( Profile, bytes );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		Assert.IsNotNull( players.Current );
		Assert.IsFalse( players.Current!.File.ReadWhole );
		players.Current.AddKey();
		CollectionAssert.AreEqual( bytes, File.ReadAllBytes( Backup ), "the odd file is kept whole, under a name the game does not read" );
		players.SaveAndDeselect();

		using var saved = File.OpenRead( Profile );
		var written = PlayerFile.Read( saved );
		Assert.IsTrue( written.ReadWhole, "what is written is a whole version 12 file" );
		Assert.IsFalse( File.Exists( Profile + ".tmp" ) );
		Assert.IsFalse( File.Exists( Backup + "2" ), "one odd file is copied once, however many saves follow" );
	}

	[TestMethod]
	public void ADefaultPlayerOverAnOddDestinationBacksItUpFirst()
	{
		var bytes = UnreadPlayer( "older" );
		File.WriteAllBytes( Profile, bytes );
		Assert.IsTrue( SaveFolder.SavePlayer( 0, "Test", new PlayerFile() ) );
		CollectionAssert.AreEqual( bytes, File.ReadAllBytes( Backup ) );
		Assert.IsTrue( Saved().ReadWhole );
	}

	[TestMethod]
	public void AFileDamagedAfterSelectionIsBackedUpBeforeItIsReplaced()
	{
		File.WriteAllBytes( Profile, Written( new PlayerFile() ) );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		var damaged = UnreadPlayer( "truncated" );
		File.WriteAllBytes( Profile, damaged );
		players.SaveAndDeselect();
		CollectionAssert.AreEqual( damaged, File.ReadAllBytes( Backup ) );
		Assert.IsTrue( Saved().ReadWhole );
	}

	[TestMethod]
	public void AWholeFileIsWrittenOverWithNoBackup()
	{
		File.WriteAllBytes( Profile, Written( new PlayerFile { KeysGiven = 7 } ) );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		players.Current!.AddKey();
		players.SaveAndDeselect();
		Assert.IsFalse( File.Exists( Backup ), "a file this game read whole needs no copy" );
		Assert.AreEqual( 8, Saved().KeysGiven, "and it was written over" );
	}

	[TestMethod]
	public void APlayerReadWithNoFileBacksUpOneThatAppearsLater()
	{
		// No gms.dat when the player was read: one that turns up afterwards was never read, so it is kept before it is replaced.
		var players = new Players();
		players.Load();
		players.Select( 0 );
		var original = Written( new PlayerFile { KeysGiven = 19 } );
		File.WriteAllBytes( Profile, original );
		players.SaveAndDeselect();
		CollectionAssert.AreEqual( original, File.ReadAllBytes( Backup ) );
		Assert.AreEqual( 0, Saved().KeysGiven, "the player's own profile replaced it, not the file that appeared" );
	}

	/// <summary>
	/// A folder with no gms.dat is still a player (<see cref="SaveFolder.ScanPlayers"/>), and the original's writer
	/// (0x005afc60) writes whatever is there: their first key makes the file, and it reads back.
	/// </summary>
	[TestMethod]
	public void APlayerFolderWithNoFileIsSavedLikeAnyOther()
	{
		Assert.IsFalse( File.Exists( Profile ) );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		Assert.IsTrue( players.Current!.File.ReadWhole );
		players.Current.AddKey();
		Assert.IsTrue( File.Exists( Profile ), "the first save makes the file" );
		players.Current.AddKey();
		players.SaveAndDeselect();
		using ( var saved = File.OpenRead( Profile ) )
			Assert.AreEqual( 2, PlayerFile.Read( saved ).KeysGiven );

		var again = new Players();
		again.Load();
		again.Select( 0 );
		Assert.AreEqual( 2, again.Current!.File.KeysGiven );
		Assert.IsFalse( again.Current.FileMissing );
	}

	/// <summary>
	/// A gms.dat that is there but will not open is not a new player. While it cannot be copied, nothing is written over
	/// it; once it can be, the stand-in replaces it and the file it held is kept.
	/// </summary>
	[TestMethod]
	public void AFileThatWouldNotOpenIsCopiedAsideOnceItCan()
	{
		if ( OperatingSystem.IsWindows() )
		{
			Assert.Inconclusive( "the open failure is staged with Unix file modes" );
			return;
		}

		var original = Written( new PlayerFile { KeysGiven = 19 } );
		File.WriteAllBytes( Profile, original );
		File.SetUnixFileMode( Profile, UnixFileMode.None );

		var players = new Players();
		players.Load();
		players.Select( 0 );
		Assert.IsFalse( players.Current!.File.ReadWhole );
		Assert.IsFalse( players.Current.FileMissing );

		players.Current.AddKey();
		File.SetUnixFileMode( Profile, UnixFileMode.UserRead | UnixFileMode.UserWrite );
		CollectionAssert.AreEqual( original, File.ReadAllBytes( Profile ), "it could not be copied, so it was not written over" );
		Assert.IsFalse( File.Exists( Backup ) );

		players.Current.AddKey();
		CollectionAssert.AreEqual( original, File.ReadAllBytes( Backup ), "once it opens, what it held is kept" );
		Assert.IsTrue( PlayerFile.Read( new MemoryStream( File.ReadAllBytes( Profile ) ) ).ReadWhole );
	}

	/// <summary>
	/// A profile that read whole, then a selection whose reload will not open the file: what is held is not what the file
	/// holds, so a file that opens later is copied aside before the held profile is written over it.
	/// </summary>
	[TestMethod]
	public void AFailedReloadOfAGoodProfileBacksUpWhateverOpensLater()
	{
		if ( OperatingSystem.IsWindows() )
		{
			Assert.Inconclusive( "the open failure is staged with Unix file modes" );
			return;
		}

		File.WriteAllBytes( Profile, Written( new PlayerFile { KeysGiven = 7 } ) );
		var players = new Players();
		players.Load();
		Assert.IsTrue( players.Current is null );

		var elsewhere = Written( new PlayerFile { KeysGiven = 19 } );
		File.WriteAllBytes( Profile, elsewhere );
		File.SetUnixFileMode( Profile, UnixFileMode.None );
		players.Select( 0 );
		Assert.IsFalse( players.Current!.File.ReadWhole, "the held profile no longer matches the file" );
		Assert.AreEqual( 7, players.Current.File.KeysGiven );

		File.SetUnixFileMode( Profile, UnixFileMode.UserRead | UnixFileMode.UserWrite );
		players.Current.AddKey();
		CollectionAssert.AreEqual( elsewhere, File.ReadAllBytes( Backup ), "the file that opened later is kept" );
		Assert.AreEqual( 8, Saved().KeysGiven );
	}

	[TestMethod]
	public void AFailedSelectionReloadKeepsRestoredProgressAndBacksUpALaterFile()
	{
		File.WriteAllBytes( Profile, Written( new PlayerFile { KeysGiven = 7 } ) );
		var players = new Players();
		players.Load();
		File.Delete( Profile );
		players.Select( 0 );
		Assert.AreEqual( 7, players.Current!.File.KeysGiven, "retain the cached readable progress" );
		Assert.IsTrue( players.Current.FileMissing, "the file was gone at this selection: one that appears later was never read" );
		// Repeated selection failures must not clear the provenance.
		players.Select( 0 );
		var restored = Written( new PlayerFile { KeysGiven = 19 } );
		File.WriteAllBytes( Profile, restored );
		players.Current!.AddKey();
		CollectionAssert.AreEqual( restored, File.ReadAllBytes( Backup ), "the file that appeared is kept before it is replaced" );
		Assert.IsFalse( players.Current.FileMissing );
		players.SaveAndDeselect();
		Assert.IsFalse( File.Exists( Backup + "2" ), "a second save copies nothing more" );
		Assert.IsFalse( File.Exists( Profile + ".tmp" ) );

		players.Select( 0 );
		Assert.IsTrue( players.Current!.File.ReadWhole, "a successful reload restores normal saving" );
		Assert.AreEqual( 8, players.Current.File.KeysGiven );
	}

	[TestMethod]
	public void AFailedConfigLoadBacksUpAFileThatBecomesReadableLater()
	{
		var path = Path.Combine( root, "save", "Config.tcf" );
		File.WriteAllBytes( path, new byte[] { 1 } );
		SaveFolder.LoadConfig();
		using var source = new MemoryStream();
		new ConfigFile { AudioQuality = 17 }.Write( source );
		var original = source.ToArray();
		File.WriteAllBytes( path, original );
		SaveFolder.SaveConfig();
		CollectionAssert.AreEqual( original, File.ReadAllBytes( path + ".opentpw-backup" ), "defaults were about to be written over a file never read" );
		using var saved = File.OpenRead( path );
		Assert.AreEqual( GameOptions.Current.AudioQuality, ConfigFile.Read( saved )!.AudioQuality );

		// The next save copies nothing: the file is the one this game wrote.
		GameOptions.Current.AudioQuality = 32;
		SaveFolder.SaveConfig();
		Assert.IsFalse( File.Exists( path + ".opentpw-backup2" ) );
	}

	[TestMethod]
	public void NewAndKnownOptionsStillSave()
	{
		SaveFolder.LoadConfig();
		SaveFolder.SaveConfig();
		GameOptions.Current.AudioQuality = 17;
		SaveFolder.SaveConfig();
		using var saved = File.OpenRead( Path.Combine( root, "save", "Config.tcf" ) );
		var file = ConfigFile.Read( saved )!;
		Assert.IsTrue( file.ReadWhole );
		Assert.AreEqual( 17, file.AudioQuality );
	}

	[TestMethod]
	public void ACompletePlayerStillPersistsProgress()
	{
		File.WriteAllBytes( Profile, Written( new PlayerFile { KeysGiven = 7 } ) );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		players.Current!.AddKey();
		players.SaveAndDeselect();
		using var stream = File.OpenRead( Profile );
		var saved = PlayerFile.Read( stream );
		Assert.IsTrue( saved.ReadWhole );
		Assert.AreEqual( 8, saved.KeysGiven );
	}

	[TestMethod]
	public void ANewPlayerCanStillBeCreated()
	{
		var players = new Players();
		players.Create( 0, "Test", instantAction: false );
		players.SaveAndDeselect();
		using var stream = File.OpenRead( Profile );
		Assert.IsTrue( PlayerFile.Read( stream ).ReadWhole );
	}

	[TestMethod]
	public void AnUnreadPlayerIsWrittenAsAWholeFileThroughTheFormatApi()
	{
		using var input = new MemoryStream( UnreadPlayer( "tail" ) );
		var file = PlayerFile.Read( input );
		Assert.IsFalse( file.ReadWhole );
		using var output = new MemoryStream();
		file.Write( output );
		Assert.IsTrue( PlayerFile.Read( new MemoryStream( output.ToArray() ) ).ReadWhole );
	}

	[DataTestMethod]
	[DataRow( "truncated" )]
	[DataRow( "newer" )]
	[DataRow( "zero" )]
	[DataRow( "tail" )]
	public void SavingOptionsBacksUpAnUnreadConfigThenWritesIt( string kind )
	{
		using var source = new MemoryStream();
		new ConfigFile().Write( source );
		var bytes = source.ToArray();
		if ( kind == "truncated" ) bytes = bytes[..12];
		if ( kind == "newer" ) BitConverter.GetBytes( 2 ).CopyTo( bytes, 0 );
		if ( kind == "zero" ) BitConverter.GetBytes( 0 ).CopyTo( bytes, 0 );
		if ( kind == "tail" ) bytes = bytes.Concat( new byte[] { 0xA5 } ).ToArray();
		var path = Path.Combine( root, "save", "Config.tcf" );
		File.WriteAllBytes( path, bytes );
		SaveFolder.LoadConfig();
		SaveFolder.SaveConfig();
		CollectionAssert.AreEqual( bytes, File.ReadAllBytes( path + ".opentpw-backup" ) );
		using var saved = File.OpenRead( path );
		Assert.IsTrue( ConfigFile.Read( saved )!.ReadWhole );
	}

	[TestMethod]
	public void ABackupIsNeverReplacedByALaterOne()
	{
		var path = Path.Combine( root, "save", "Config.tcf" );
		File.WriteAllBytes( path, new byte[] { 9, 9 } );
		SaveFolder.LoadConfig();
		SaveFolder.SaveConfig();
		File.WriteAllBytes( path, new byte[] { 7 } );
		SaveFolder.SaveConfig();
		CollectionAssert.AreEqual( new byte[] { 9, 9 }, File.ReadAllBytes( path + ".opentpw-backup" ) );
		CollectionAssert.AreEqual( new byte[] { 7 }, File.ReadAllBytes( path + ".opentpw-backup2" ) );
		File.WriteAllBytes( path, new byte[] { 5, 5, 5 } );
		SaveFolder.SaveConfig();
		CollectionAssert.AreEqual( new byte[] { 9, 9 }, File.ReadAllBytes( path + ".opentpw-backup" ), "a third odd file replaces nothing" );
		CollectionAssert.AreEqual( new byte[] { 7 }, File.ReadAllBytes( path + ".opentpw-backup2" ) );
		CollectionAssert.AreEqual( new byte[] { 5, 5, 5 }, File.ReadAllBytes( path + ".opentpw-backup3" ) );
	}

	/// <remarks><b>Mutations:</b> the copy wrapped in a catch that goes on (<c>try { BackUp( path ); } catch { }</c> in <c>SaveConfig</c>): the file is replaced with no copy, and nothing is told; <c>SaveTrouble.BackedUp( what )</c> moved above the <c>WriteAllBytes( backup, bytes )</c> in <c>SaveFolder.BackUp</c>: a box says a copy was kept when none was.</remarks>
	[TestMethod]
	public void AConfigThatCannotBeCopiedIsNotWrittenOverAndIsTold()
	{
		var path = Path.Combine( root, "save", "Config.tcf" );
		File.WriteAllBytes( path, new byte[] { 1 } );
		Directory.CreateDirectory( path + ".opentpw-backup" );
		SaveFolder.LoadConfig();

		try
		{
			SaveFolder.SaveConfig();

			CollectionAssert.AreEqual( new byte[] { 1 }, File.ReadAllBytes( path ), "it could not be copied, so it was not written over" );
			Assert.IsTrue( SaveTrouble.LostAWrite, "and the player is told" );

			SaveTrouble.Asked();
			Assert.AreEqual( "OPTIONS NOT SAVED\n\nThe save folder cannot be written.", SaveTrouble.Take(), "the failure alone: no BACKED UP box claims a copy that was not made" );
			Assert.IsFalse( SaveTrouble.Pending );
		}
		finally
		{
			DrainTrouble();
		}
	}

	/// <remarks><b>Mutation:</b> <c>BackUp</c> without the same-bytes test: the retry makes <c>.opentpw-backup2</c>.</remarks>
	[TestMethod]
	public void AWriteThatFailsAfterAGoodCopyDoesNotCopyAgainAtTheRetry()
	{
		var odd = UnreadPlayer( "tail" );
		File.WriteAllBytes( Profile, odd );
		Directory.CreateDirectory( Profile + ".tmp" );
		var file = PlayerFile.Read( new MemoryStream( odd ) );
		Assert.IsFalse( file.ReadWhole );

		try
		{
			Assert.IsFalse( SaveFolder.SavePlayer( 0, "Test", file ) );
			Assert.IsFalse( SaveFolder.SavePlayer( 0, "Test", file ) );

			CollectionAssert.AreEqual( odd, File.ReadAllBytes( Backup ) );
			Assert.IsFalse( File.Exists( Backup + "2" ), "the same bytes are not copied twice" );
			CollectionAssert.AreEqual( odd, File.ReadAllBytes( Profile ), "the file was not written over" );
			Assert.IsTrue( SaveTrouble.LostAWrite );
		}
		finally
		{
			DrainTrouble();
		}
	}

	/// <summary>
	/// <b>A player's name is hidden before the scan logs a path</b> (the week-end audit of 2026-10-31, R-F1): a gms.dat that ended early
	/// is logged with its folder, which holds the name, and the log file is already open. Each line is scrubbed as the log's own would be,
	/// at the moment it is printed.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>ConsoleLog.HidePlayer</c> taken out of <c>ScanPlayers</c> (it is then called only after the scan, in <c>Players.Load</c>): the name is in a line.</remarks>
	[TestMethod]
	public void ThePlayersNameIsHiddenBeforeTheScanLogsItsFolder()
	{
		var safeField = typeof( ConsoleLog ).GetField( "_safe", BindingFlags.NonPublic | BindingFlags.Static )!;
		var oldSafe = safeField.GetValue( null );
		var oldLog = Logger.OnLog;
		var printed = new System.Collections.Generic.List<string>();

		try
		{
			safeField.SetValue( null, SafePaths.ForThisMachine( root ) );
			Logger.OnLog = ( _, text ) => printed.Add( ( (SafePaths)safeField.GetValue( null )! ).Scrub( text ) );

			var folder = Path.Combine( root, "save", "users", "2Zebulon Pike" );
			Directory.CreateDirectory( folder );
			File.WriteAllBytes( Path.Combine( folder, "gms.dat" ), UnreadPlayer( "truncated" ) );

			var found = SaveFolder.ScanPlayers();

			Assert.IsTrue( found.Any( player => player.Name == "Zebulon Pike" ) );
			Assert.IsTrue( printed.Any( line => line.Contains( "ended early" ) ), "the scan logged the unreadable file" );
			Assert.IsFalse( printed.Any( line => line.Contains( "Zebulon" ) ), string.Join( "\n", printed ) );
		}
		finally
		{
			Logger.OnLog = oldLog;
			safeField.SetValue( null, oldSafe );
		}
	}

	/// <summary>
	/// <b>Only a folder named for a slot is a player</b> (Q933): <c>users/notes</c>, a slot 5 and a bare slot digit are skipped, and
	/// <see cref="SaveHome.HoldsPlayers"/> says the same of that folder, so the notices do not count them.
	/// </summary>
	/// <remarks><b>Mutation:</b> the scan's own length test in place of <see cref="SaveFolder.IsPlayerFolderName"/>: <c>notes</c> and <c>5Test</c> are players.</remarks>
	[TestMethod]
	public void OnlyAFolderNamedForASlotIsAPlayer()
	{
		var users = Path.Combine( root, "save", "users" );

		Directory.Delete( Path.Combine( users, "1Test" ), true );

		foreach ( var name in new[] { "notes", "5Test", "1", "0Test" } )
			Directory.CreateDirectory( Path.Combine( users, name ) );

		Assert.AreEqual( 0, SaveFolder.ScanPlayers().Count );
		Assert.IsFalse( SaveHome.HoldsPlayers( Path.Combine( root, "save" ) ) );

		Directory.CreateDirectory( Path.Combine( users, "4Ann" ) );

		var found = SaveFolder.ScanPlayers();

		Assert.AreEqual( 1, found.Count );
		Assert.AreEqual( (3, "Ann"), (found[0].Slot, found[0].Name) );
		Assert.IsTrue( SaveHome.HoldsPlayers( Path.Combine( root, "save" ) ) );
	}

	/// <summary>
	/// <b>An opentpw.cfg that will not read is copied aside before the defaults replace it</b> (ST-F6), as Config.tcf's is: its <c>gpu</c>
	/// line is not lost to the loading screen's own write. The open failure is a lock another handle holds.
	/// </summary>
	/// <remarks><b>Mutation:</b> the <c>_unreadDisplay</c> test taken out of <c>SaveDisplay</c>: no copy, the file is replaced.</remarks>
	[TestMethod]
	public void AnUnreadDisplayFileIsCopiedAsideBeforeItIsWrittenOver()
	{
		var path = Path.Combine( root, "save", "opentpw.cfg" );
		var original = "display fullscreen\ngpu GTX 1650\n";
		File.WriteAllText( path, original );

		using ( new FileStream( path, FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
		{
			SaveFolder.LoadDisplay();
			SaveFolder.SaveDisplay( tell: false );
			Assert.IsFalse( File.Exists( path + ".opentpw-backup" ) );
		}

		Assert.AreEqual( original, File.ReadAllText( path ), "it could not be copied, so it was not written over" );

		SaveFolder.SaveDisplay( tell: false );
		Assert.AreEqual( original, File.ReadAllText( path + ".opentpw-backup" ), "once it opens, what it held is kept" );
		StringAssert.Contains( File.ReadAllText( path ), "graphics improved" );

		GameOptions.Current.Faithful = true;
		SaveFolder.SaveDisplay( tell: false );
		Assert.IsFalse( File.Exists( path + ".opentpw-backup2" ), "the file is the one this game wrote now" );
	}

	/// <summary>
	/// <b>A file that reads is written over with no copy</b>, and the gpu line with a letter outside ASCII reads back whole (S-M5):
	/// the file is written as UTF-8 and was read as ASCII.
	/// </summary>
	/// <remarks><b>Mutation:</b> the read back to <c>ReadAllText</c> (ASCII): the line reads with replacement characters.</remarks>
	[TestMethod]
	public void ADisplayFileReadsBackAsItWasWrittenWithNoCopy()
	{
		var path = Path.Combine( root, "save", "opentpw.cfg" );
		GameOptions.Current.Gpu = "Radeon \u00c9clair \u2122";
		SaveFolder.SaveDisplay( tell: false );
		GameOptions.Current.Gpu = "";

		SaveFolder.LoadDisplay();
		Assert.AreEqual( "Radeon \u00c9clair \u2122", GameOptions.Current.Gpu );

		SaveFolder.SaveDisplay( tell: false );
		Assert.IsFalse( File.Exists( path + ".opentpw-backup" ) );
		Assert.AreEqual( "Radeon \u00c9clair \u2122", File.ReadAllText( path ).Split( '\n' ).Single( l => l.StartsWith( "gpu " ) )[4..].Trim() );
	}

	/// <remarks><b>Mutation:</b> <c>SameBytes</c> without its catch: a newest copy that will not open fails the save.</remarks>
	[TestMethod]
	public void ANewestCopyThatWillNotOpenDoesNotStopTheSave()
	{
		var path = Path.Combine( root, "save", "Config.tcf" );
		File.WriteAllBytes( path, new byte[] { 9, 9 } );
		File.WriteAllBytes( path + ".opentpw-backup", new byte[] { 9, 9 } );
		SaveFolder.LoadConfig();

		using ( new FileStream( path + ".opentpw-backup", FileMode.Open, FileAccess.ReadWrite, FileShare.None ) )
			SaveFolder.SaveConfig();

		CollectionAssert.AreEqual( new byte[] { 9, 9 }, File.ReadAllBytes( path + ".opentpw-backup2" ), "a fresh copy is made" );
		using var saved = File.OpenRead( path );
		Assert.IsTrue( ConfigFile.Read( saved )!.ReadWhole, "and the file is written over" );
	}

	/// <summary>
	/// <b>A rewrite of opentpw.cfg keeps what this game does not know, where it stands</b> (Q932): a directive a later build
	/// wrote, a comment of the player's, a blank line, in a file that also holds old comments of this game's and settings in another order.
	/// The settings are made again (the first line of each in its place, a repeat dropped), the old comments are replaced by the
	/// current ones, and no copy is made, as the file read whole.
	/// </summary>
	/// <remarks><b>Mutation:</b> the kept branch at the end of the walk in <c>SaveDisplay</c> taken out (the unknown lines and the comment are gone, as before the item); or the <c>OwnComments</c> test taken out (an old comment of this game's is kept and written again at every save).</remarks>
	[TestMethod]
	public void AnUnknownLineAndAPlayersCommentSurviveARewriteWhereTheyStand()
	{
		var path = Path.Combine( root, "save", "opentpw.cfg" );
		File.WriteAllText( path,
			"# my notes: keep the window small\r\n" +
			"# OpenTPW's own settings. The original's Config.tcf beside this one holds the\r\n" +
			"gpu GTX 1650\r\n" +
			"FutureThing on 3\r\n" +
			"\r\n" +
			"# ground asked: the ground's tiles are the size the detail file asks, 32 pixels at medium and high, as\r\n" +
			"Display Fullscreen\r\n" +
			"ground original\r\n" +
			"# a comment between two of the game's\r\n" +
			"updatecheck off\r\n" +
			"gpu RTX 9999\r\n" +
			"monitor 2 left\r\n" );

		SaveFolder.LoadDisplay();
		SaveFolder.SaveDisplay( tell: false );

		var lines = File.ReadAllText( path ).Split( '\n' );
		Assert.AreEqual( "", lines[Array.IndexOf( lines, "FutureThing on 3" ) + 1], "the blank line stays after the directive" );

		// In place: the player's first comment before the settings, the unknown directive between the gpu block and the display block, and so on.
		var at = lines.Distinct().ToDictionary( line => line, line => Array.IndexOf( lines, line ), StringComparer.Ordinal );
		Assert.IsTrue( at["# my notes: keep the window small"] < at["gpu RTX 9999"], "the player's comment is above the gpu block" );
		Assert.IsTrue( at["gpu RTX 9999"] < at["FutureThing on 3"] && at["FutureThing on 3"] < at["display fullscreen"], "the directive stays between the gpu and the display lines" );
		Assert.IsTrue( at["display fullscreen"] < at["# a comment between two of the game's"] && at["# a comment between two of the game's"] < at["updatecheck off"] );
		Assert.IsTrue( at["updatecheck off"] < at["monitor 2 left"], "and the last line stays last of the unknown ones" );
		Assert.AreEqual( 1, lines.Count( l => l.StartsWith( "gpu " ) ), "the repeated gpu line is made once" );
		Assert.AreEqual( 1, lines.Count( l => l.StartsWith( "display ", StringComparison.OrdinalIgnoreCase ) ), "a setting named in capitals is the same setting" );
		Assert.IsFalse( lines.Any( l => l.StartsWith( "ground" ) ), "the retired line is dropped, as its warning says" );
		Assert.IsFalse( lines.Any( l => l.StartsWith( "# ground asked" ) || l.Contains( '\r' ) ), "an earlier build's comment is replaced and the lines end in LF" );
		Assert.IsFalse( File.Exists( path + ".opentpw-backup" ), "the file read whole, so it needs no copy" );

		// The settings it read are the ones written (the last gpu line wins on a read).
		Assert.AreEqual( "RTX 9999", GameOptions.Current.Gpu );
		StringAssert.Contains( File.ReadAllText( path ), "\ngpu RTX 9999\n" );
		Assert.AreEqual( DisplayMode.FullScreen, GameOptions.Current.DisplayMode );
		Assert.IsFalse( GameOptions.Current.UpdateCheck );
	}

	/// <summary>
	/// <b>A second rewrite changes nothing</b> (Q932): the kept lines are not written twice and this game's own comments do not pile up,
	/// and a file this game wrote alone (nothing unknown in it) is written back byte for byte.
	/// </summary>
	/// <remarks><b>Mutation:</b> the <c>OwnComments</c> test taken out of <c>SaveDisplay</c>: the second write holds the comments twice.</remarks>
	[TestMethod]
	public void ARewriteOfARewriteIsTheSameBytes()
	{
		var path = Path.Combine( root, "save", "opentpw.cfg" );
		GameOptions.Current.NoticesTold.Add( "moved" );
		SaveFolder.SaveDisplay( tell: false );
		var fresh = File.ReadAllBytes( path );

		SaveFolder.LoadDisplay();
		SaveFolder.SaveDisplay( tell: false );
		CollectionAssert.AreEqual( fresh, File.ReadAllBytes( path ), "a file this game wrote is written back as it was" );

		File.WriteAllText( path, "# note\nfuture 1\n" + File.ReadAllText( path ) );
		SaveFolder.LoadDisplay();
		SaveFolder.SaveDisplay( tell: false );
		var once = File.ReadAllBytes( path );
		SaveFolder.LoadDisplay();
		SaveFolder.SaveDisplay( tell: false );
		CollectionAssert.AreEqual( once, File.ReadAllBytes( path ), "and so is one that also holds a line it does not know" );
		StringAssert.StartsWith( File.ReadAllText( path ), "# note\nfuture 1\n# OpenTPW's own settings." );
	}

	/// <summary>A hand-made opentpw.cfg with a byte order mark, or one a text editor saved as UTF-16, still reads its first line (the review of the week-end audit's fixes).</summary>
	/// <remarks><b>Mutation:</b> the read back to <c>Encoding.UTF8.GetString</c>: the mark joins the first line and the card is lost; the UTF-16 file reads as nothing.</remarks>
	[DataTestMethod]
	[DataRow( "utf8" )]
	[DataRow( "utf16" )]
	public void ADisplayFileWithAByteOrderMarkReadsItsFirstLine( string kind )
	{
		var path = Path.Combine( root, "save", "opentpw.cfg" );
		var text = "gpu GTX 1650\ndisplay borderless\n";
		File.WriteAllBytes( path, kind == "utf8"
			? new byte[] { 0xEF, 0xBB, 0xBF }.Concat( System.Text.Encoding.UTF8.GetBytes( text ) ).ToArray()
			: new byte[] { 0xFF, 0xFE }.Concat( System.Text.Encoding.Unicode.GetBytes( text ) ).ToArray() );

		SaveFolder.LoadDisplay();

		Assert.AreEqual( "GTX 1650", GameOptions.Current.Gpu );
		Assert.AreEqual( DisplayMode.BorderlessFullScreen, GameOptions.Current.DisplayMode );
	}
}
