using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// Q72: three values the lobby and the advisor held as constants, read from the game's files instead. The four shown
/// park names are rows of <c>THEMENAMES.str</c> (<see cref="ParkFixedItems.ParkDisplayName"/>, <c>docs/exe/lobby.md</c>,
/// "Park names and the locale tables"); an island's two models are its ISLAND() line's folder, third and fourth names
/// (<see cref="LobbyScript"/>); the advisor's duck is <c>sound.sam</c>'s <c>SoundInfo.DUCKINGLEVEL</c> over 100
/// (<see cref="GameOptions.DuckLevel"/>, <c>docs/exe/audio.md</c>, "The fade, the SoundInfo settings and the reverb").
/// For the shipped files the data equals the old constants, so each read is also tested where a wrong rule gives
/// another value: the four names are distinct, so a theme matched to the wrong row fails; the isle and gate names
/// differ, so a swapped column fails; and a duck file of 55 beside a <c>DROPPABLELEVEL</c> of 20 is read as 55.
/// </summary>
[TestClass]
public class ValuesFromDataTests
{
	/// <summary>Each theme takes its own row of THEMENAMES.str, whatever the case of its name; a theme outside the four takes none.</summary>
	[TestMethod]
	[DataRow( "Jungle", "Lost Kingdom" )]
	[DataRow( "Fantasy", "Wonder Land" )]
	[DataRow( "hallow", "Halloween World" )]
	[DataRow( "SPACE", "Space Zone" )]
	[DataRow( "lobby", "" )]
	public void AParksShownNameIsItsRowOfThemeNames( string theme, string shown )
	{
		Log ??= new();
		FileSystem = GameData.Required();

		Assert.AreEqual( shown, ParkFixedItems.ParkDisplayName( theme ), $"{theme}'s shown name" );
	}

	/// <summary>
	/// Each shipped script's ISLAND() line names its folder and its two models, and the paths the island and its gate
	/// load are files of the game's.
	/// </summary>
	[TestMethod]
	[DataRow( "jungle", "jun_isle", "jun_gate" )]
	[DataRow( "fantasy", "fan_isle", "fan_gate" )]
	[DataRow( "hallow", "hal_isle", "hal_gate" )]
	[DataRow( "space", "spa_isle", "spa_gate" )]
	public void AnIslandsModelsAreItsIslandLinesNames( string theme, string isle, string gate )
	{
		Log ??= new();
		FileSystem = GameData.Required();

		var script = LobbyScript.Read( theme );

		Assert.IsTrue( script.HasModels, $"{theme}'s ISLAND() line names its models" );
		Assert.AreEqual( "lobby/terrain", script.Directory, "the line's folder, data\\ dropped" );
		Assert.AreEqual( isle, script.IsleModel, "the isle, the third name" );
		Assert.AreEqual( gate, script.GateModel, "the gate, the fourth name" );
		Assert.AreEqual( $"lobby/terrain/{isle}.md2", script.IslePath );
		Assert.AreEqual( $"lobby/terrain/{gate}.md2", script.GatePath );
		Assert.AreEqual( "lobby/terrain/textures", script.TextureDirectory );

		using ( var model = FileSystem.OpenReadOrNull( script.IslePath ) )
			Assert.IsNotNull( model, $"{script.IslePath} is in the game's files" );

		using ( var model = FileSystem.OpenReadOrNull( script.GatePath ) )
			Assert.IsNotNull( model, $"{script.GatePath} is in the game's files" );
	}

	/// <summary>A script's folder is a path under the data folder: backslashes turned, a leading data folder and the outer slashes dropped.</summary>
	[TestMethod]
	[DataRow( @"data\lobby\terrain", "lobby/terrain" )]
	[DataRow( @"\DATA\levels\jungle\", "levels/jungle" )]
	[DataRow( @"lobby\terrain", "lobby/terrain" )]
	[DataRow( @"database\x", "database/x" )]
	public void AScriptsFolderIsReadUnderTheDataFolder( string folder, string path )
		=> Assert.AreEqual( path, LobbyScript.DataFolder( folder ) );

	private static SettingsFile Sam( string text ) => new( new MemoryStream( Encoding.ASCII.GetBytes( text ) ) );

	/// <summary>The duck is the file's DUCKINGLEVEL, not its neighbour's and not the shipped 38, and the shipped 38 stands only where the file has none.</summary>
	[TestMethod]
	public void TheDuckIsTheFilesDuckingLevel()
	{
		Log ??= new();

		var file = Sam( "SoundInfo.DROPPABLELEVEL\t20\r\nSoundInfo.DUCKINGLEVEL\t\t55\r\nSoundInfo.DEFAULTQUALITY\t50\r\n" );

		Assert.AreEqual( 55, GameOptions.DuckingLevel( file ), "DUCKINGLEVEL 55" );
		Assert.AreEqual( 38, GameOptions.DuckingLevel( Sam( "SoundInfo.FADETOGGLE\t30\r\n" ) ), "no DUCKINGLEVEL: the shipped 38" );
		Assert.AreEqual( 38, GameOptions.DuckingLevel( null ), "no file: the shipped 38" );
	}

	/// <summary>The shipped sound.sam ducks to 38 in a hundred, which is what the advisor is handed.</summary>
	[TestMethod]
	public void TheShippedDuckIs38()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		// The row itself, so a file without it cannot pass on the fallback, which is 38 too.
		var file = new SettingsFile( "/sound.sam" );
		Assert.AreEqual( "38", file.Entries.Single( pair => pair.Key == "SoundInfo.DUCKINGLEVEL" ).Value, "the shipped row" );
		Assert.AreEqual( 38, GameOptions.DuckingLevel( file ) );
		Assert.AreEqual( 0.38f, GameOptions.DuckLevel, 1e-6f, "the advisor's duck" );
	}
}
