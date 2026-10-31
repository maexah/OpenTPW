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

	[TestCleanup]
	public void RestoreFiles()
	{
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
		Assert.IsTrue( written.ReadWhole, "what is written is a whole version 1 file" );
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
	}
}
