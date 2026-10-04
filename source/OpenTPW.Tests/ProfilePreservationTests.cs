using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>Real selection/save paths over disposable files; no installed save folder is written.</summary>
[TestClass]
public class ProfilePreservationTests
{
	private string root = null!;
	private BaseFileSystem oldData = null!;
	private BaseFileSystem oldSaves = null!;
	private GameOptions oldOptions = null!;
	private object? oldThemes;
	private static readonly FieldInfo Themes = typeof( SaveFolder ).GetField( "_themes", BindingFlags.NonPublic | BindingFlags.Static )!;
	private string Profile => Path.Combine( root, "save", "users", "1Test", "gms.dat" );

	[TestInitialize]
	public void IsolateFiles()
	{
		oldData = FileSystem;
		oldSaves = SaveFileSystem;
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
		FileSystem = oldData;
		SaveFileSystem = oldSaves;
		GameOptions.Current = oldOptions;
		oldOptions.ApplySound();
		Themes.SetValue( null, oldThemes );
		if ( root != null )
			Directory.Delete( root, true );
	}

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
	public void SelectingAndLeavingAnUnreadPlayerPreservesItsBytes( string kind )
	{
		var bytes = UnreadPlayer( kind );
		File.WriteAllBytes( Profile, bytes );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		Assert.IsNotNull( players.Current );
		Assert.IsFalse( players.Current!.File.CanWrite );
		players.Current.AddKey();
		players.SaveAndDeselect();
		CollectionAssert.AreEqual( bytes, File.ReadAllBytes( Profile ) );
		Assert.IsFalse( File.Exists( Profile + ".tmp" ) );
	}

	[TestMethod]
	public void ADefaultPlayerCannotReplaceAnUnreadDestination()
	{
		var bytes = UnreadPlayer( "older" );
		File.WriteAllBytes( Profile, bytes );
		SaveFolder.SavePlayer( 0, "Test", new PlayerFile() );
		CollectionAssert.AreEqual( bytes, File.ReadAllBytes( Profile ) );
	}

	[TestMethod]
	public void AFileDamagedAfterSelectionIsStillPreserved()
	{
		File.WriteAllBytes( Profile, Written( new PlayerFile() ) );
		var players = new Players();
		players.Load();
		players.Select( 0 );
		var damaged = UnreadPlayer( "truncated" );
		File.WriteAllBytes( Profile, damaged );
		players.SaveAndDeselect();
		CollectionAssert.AreEqual( damaged, File.ReadAllBytes( Profile ) );
	}

	[TestMethod]
	public void AFailedPlayerLoadCannotOverwriteAFileThatBecomesReadableLater()
	{
		// A missing gms.dat takes the same null-returning fallback as an open failure.
		var players = new Players();
		players.Load();
		players.Select( 0 );
		var original = Written( new PlayerFile { KeysGiven = 19 } );
		File.WriteAllBytes( Profile, original );
		players.SaveAndDeselect();
		CollectionAssert.AreEqual( original, File.ReadAllBytes( Profile ) );
	}

	[TestMethod]
	public void AFailedSelectionReloadPreservesRestoredProgressUntilSuccessfulReload()
	{
		File.WriteAllBytes( Profile, Written( new PlayerFile { KeysGiven = 7 } ) );
		var players = new Players();
		players.Load();
		File.Delete( Profile );
		players.Select( 0 );
		Assert.AreEqual( 7, players.Current!.File.KeysGiven, "retain the cached readable progress" );
		Assert.IsFalse( players.Current.File.CanWrite, "the failed reload invalidates its write permission" );
		// Repeated selection failures must not clear the provenance.
		players.Select( 0 );
		var restored = Written( new PlayerFile { KeysGiven = 19 } );
		File.WriteAllBytes( Profile, restored );
		players.Current!.AddKey();
		CollectionAssert.AreEqual( restored, File.ReadAllBytes( Profile ), "immediate key saves are protected too" );
		players.SaveAndDeselect();
		CollectionAssert.AreEqual( restored, File.ReadAllBytes( Profile ), "deselection cannot save the stale roster" );
		Assert.IsFalse( File.Exists( Profile + ".tmp" ) );

		players.Select( 0 );
		Assert.IsTrue( players.Current!.File.CanWrite, "a successful reload restores normal saving" );
		Assert.AreEqual( 19, players.Current.File.KeysGiven );
		players.Current.AddKey();
		players.SaveAndDeselect();
		using var saved = File.OpenRead( Profile );
		Assert.AreEqual( 20, PlayerFile.Read( saved ).KeysGiven );
	}

	[TestMethod]
	public void AFailedConfigLoadCannotOverwriteAFileThatBecomesReadableLater()
	{
		var path = Path.Combine( root, "save", "Config.tcf" );
		File.WriteAllBytes( path, new byte[] { 1 } );
		SaveFolder.LoadConfig();
		using var source = new MemoryStream();
		new ConfigFile { AudioQuality = 17 }.Write( source );
		var original = source.ToArray();
		File.WriteAllBytes( path, original );
		SaveFolder.SaveConfig();
		CollectionAssert.AreEqual( original, File.ReadAllBytes( path ) );
		// An explicit successful reload permits a later intentional update.
		SaveFolder.LoadConfig();
		GameOptions.Current.AudioQuality = 32;
		SaveFolder.SaveConfig();
		using var saved = File.OpenRead( path );
		Assert.AreEqual( 32, ConfigFile.Read( saved )!.AudioQuality );
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
		Assert.IsTrue( file.CanWrite );
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
		Assert.IsTrue( saved.CanWrite );
		Assert.AreEqual( 8, saved.KeysGiven );
	}

	[TestMethod]
	public void ANewPlayerCanStillBeCreated()
	{
		var players = new Players();
		players.Create( 0, "Test", instantAction: false );
		players.SaveAndDeselect();
		using var stream = File.OpenRead( Profile );
		Assert.IsTrue( PlayerFile.Read( stream ).CanWrite );
	}

	[TestMethod]
	public void AnUnreadPlayerCannotBeWrittenThroughTheFormatApi()
	{
		using var input = new MemoryStream( UnreadPlayer( "tail" ) );
		var file = PlayerFile.Read( input );
		using var output = new MemoryStream();
		Assert.ThrowsException<InvalidDataException>( () => file.Write( output ) );
		Assert.AreEqual( 0L, output.Length );
	}

	[DataTestMethod]
	[DataRow( "truncated" )]
	[DataRow( "newer" )]
	[DataRow( "zero" )]
	[DataRow( "tail" )]
	public void SavingOptionsPreservesAnUnreadConfig( string kind )
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
		CollectionAssert.AreEqual( bytes, File.ReadAllBytes( path ) );
	}
}
