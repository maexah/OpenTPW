using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>Entering a park reads the newest park file in the player's own folder, over a disposable save folder.</summary>
[TestClass]
public class PlayersOwnParkTests
{
	private static readonly DateTime Old = new( 2001, 1, 1, 0, 0, 0, DateTimeKind.Utc );

	private string root = null!;
	private BaseFileSystem oldSaves = null!;

	private string Jungle => Path.Combine( root, "users", "1Test", "jungle" );

	[TestInitialize]
	public void IsolateSaves()
	{
		oldSaves = SaveFileSystem;
		root = Directory.CreateTempSubdirectory( "opentpw-own-park-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
	}

	[TestCleanup]
	public void RestoreSaves()
	{
		SetCurrentPlayer( null );
		SaveFileSystem = oldSaves;
		Directory.Delete( root, true );
	}

	[TestMethod]
	public void TheFileWrittenLastIsTheOneEntered()
	{
		Put( "easymode.TPWI", Old.AddDays( 2 ) );
		Put( "A park.TPWS", Old.AddDays( 3 ) );
		Put( "Before.TPWS", Old.AddDays( 1 ) );

		Assert.AreEqual( "A park.TPWS", Path.GetFileName( SaveFolder.NewestPark( 0, "Test", "jungle" ) ) );

		File.SetLastWriteTimeUtc( Path.Combine( Jungle, "easymode.TPWI" ), Old.AddDays( 4 ) );
		Assert.AreEqual( "easymode.TPWI", Path.GetFileName( SaveFolder.NewestPark( 0, "Test", "jungle" ) ) );
	}

	[TestMethod]
	public void OnlyAParkFilesNameCounts()
	{
		Put( "restart.INTS", Old.AddDays( 9 ) );
		Put( "gms.dat", Old.AddDays( 9 ) );
		Assert.IsNull( SaveFolder.NewestPark( 0, "Test", "jungle" ) );

		Put( "lower.tpws", Old );
		Assert.AreEqual( "lower.tpws", Path.GetFileName( SaveFolder.NewestPark( 0, "Test", "jungle" ) ) );
	}

	[TestMethod]
	public void AFileWrittenAtTheSameInstantDoesNotReplaceTheOneHeld()
	{
		Put( "b.TPWS", Old );
		Put( "a.TPWS", Old );
		Put( "c.TPWS", Old );

		Assert.AreEqual( "a.TPWS", Path.GetFileName( SaveFolder.NewestPark( 0, "Test", "jungle" ) ) );
	}

	[TestMethod]
	public void AFolderWithNoParkOrNoFolderAtAllGivesNone()
	{
		Assert.IsNull( SaveFolder.NewestPark( 0, "Test", "jungle" ) );
		Assert.IsNull( SaveFolder.NewestPark( 0, "Test", "space" ) );
		Assert.IsNull( SaveFolder.NewestPark( 1, "Nobody", "jungle" ) );
	}

	/// <summary>
	/// The level's own choice. The newer file is one that will not read, so which file was opened shows in what
	/// comes back: the shipped park's 755 from the copy, nothing from the other.
	/// </summary>
	[TestMethod]
	public void TheLevelReadsThePlayersNewestFileAndNotTheShippedOne()
	{
		FileSystem = GameData.Required();
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		File.WriteAllBytes( Path.Combine( Jungle, "easymode.TPWI" ), FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		File.SetLastWriteTimeUtc( Path.Combine( Jungle, "easymode.TPWI" ), Old.AddDays( 1 ) );
		Put( "Not a park.TPWS", Old );

		var park = Create();
		Assert.IsInstanceOfType( park, typeof( ParkWorld ) );
		Assert.AreEqual( 755, park!.GameTick );

		File.SetLastWriteTimeUtc( Path.Combine( Jungle, "Not a park.TPWS" ), Old.AddDays( 2 ) );
		Assert.IsNull( Create() );
	}

	[TestMethod]
	public void APlayerWithNoParkFileEntersAParkMadeFreshWhateverTheirMode()
	{
		FileSystem = GameData.Required();

		foreach ( var instantAction in new[] { true, false } )
		{
			SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = instantAction } ) );
			Assert.IsInstanceOfType( Create(), typeof( FreshPark ) );
		}
	}

	[TestMethod]
	public void WithNobodyPlayingTheShippedParkIsRead()
	{
		FileSystem = GameData.Required();
		Put( "Not a park.TPWS", Old );
		SetCurrentPlayer( null );

		var park = Create();
		Assert.IsInstanceOfType( park, typeof( ParkWorld ) );
		Assert.AreEqual( 755, park!.GameTick );
	}

	private static IParkInitialState? Create()
		=> Level.CreatePark( "jungle", new ParkBalance( "jungle", easyMode: Level.InstantAction ),
			new ParkItemCatalogue( "jungle", instantAction: Level.InstantAction ) );

	private void Put( string name, DateTime written )
	{
		var path = Path.Combine( Jungle, name );
		File.WriteAllBytes( path, [1, 2, 3, 4] );
		File.SetLastWriteTimeUtc( path, written );
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
