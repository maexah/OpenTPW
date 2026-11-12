using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// <see cref="LobbyScript.Read"/> over the four island scripts the game ships in <c>lobby.wad</c> (<c>jungle.txt</c>,
/// <c>fantasy.txt</c>, <c>hallow.txt</c>, <c>space.txt</c>), every field the reader keeps, read through the game's own file
/// system (Q80, the lobby audit's plan item 13). The four files differ in every field the reader takes from ISLAND, so a
/// field read from the wrong column, or a line skipped, gives a number one of the islands does not hold. The vocabulary and
/// the field order are the parser's, <c>FUN_005e3210</c> (<c>docs/exe/lobby.md</c>, "The island's heading").
/// </summary>
[TestClass]
public class LobbyScriptIslandsTests
{
	private static LobbyScript ReadShipped( string theme )
	{
		Log ??= new();
		FileSystem = GameData.Required();

		using ( var stream = FileSystem.OpenReadOrNull( $"lobby/{theme}.txt" ) )
			Assert.IsNotNull( stream, $"lobby/{theme}.txt is in the game's files" );

		return LobbyScript.Read( theme );
	}

	private static void AssertSky( LobbyScript script, int r, int g, int b )
	{
		Assert.IsTrue( script.HasSkyColour, "its SKYCOLOUR line is read" );
		Assert.AreEqual( r, (int)MathF.Round( script.SkyColour.X * 255f ), "sky red" );
		Assert.AreEqual( g, (int)MathF.Round( script.SkyColour.Y * 255f ), "sky green" );
		Assert.AreEqual( b, (int)MathF.Round( script.SkyColour.Z * 255f ), "sky blue" );
	}

	private static void AssertFlyer( LobbyScript.FlyingMesh flyer, string model, int count, float speed )
	{
		Assert.AreEqual( @"data\lobby\terrain", flyer.Directory, "the flyer's folder, quotes stripped" );
		Assert.AreEqual( model, flyer.Model, "the flyer's model" );
		Assert.AreEqual( count, flyer.Count, $"{model}'s count" );
		Assert.AreEqual( 200f, flyer.Volume.X, 1e-4f, $"{model}'s box across" );
		Assert.AreEqual( 100f, flyer.Volume.Y, 1e-4f, $"{model}'s box high" );
		Assert.AreEqual( 200f, flyer.Volume.Z, 1e-4f, $"{model}'s box deep" );
		Assert.AreEqual( speed, flyer.Speed, 1e-4f, $"{model}'s speed" );
	}

	/// <summary>Lost Kingdom: slot 0, heading 90, camera 12.5, two butterfly swarms, no weather.</summary>
	[TestMethod]
	public void LostKingdomsScriptReadsWhole()
	{
		var script = ReadShipped( "jungle" );

		Assert.AreEqual( 0, script.Index, "slot" );
		Assert.AreEqual( "Lost Kingdom", script.ParkName, "name, the ISLAND line's fifth field" );
		Assert.AreEqual( 90f, script.Heading, 1e-4f, "heading, the sixth field" );
		Assert.AreEqual( 12.5f, script.CameraHeight, 1e-4f, "camera height, the seventh field" );
		AssertSky( script, 243, 203, 191 );
		Assert.AreEqual( 0, script.Rainy, "no RAINY line" );
		Assert.AreEqual( 0, script.Lightning, "no LIGHTNING line" );
		Assert.AreEqual( 0f, script.StrikesPerSecond, "no storm" );

		Assert.AreEqual( 2, script.FlyingMeshes.Count, "two FLYINGMESH lines" );
		AssertFlyer( script.FlyingMeshes[0], "bfly_PINK", 10, 1.5f );
		AssertFlyer( script.FlyingMeshes[1], "bfly_YELL", 10, 1.5f );
		Assert.AreEqual( 15f, script.FlyingMeshes[0].SpeedPerSecond, 1e-4f, "1.5 a lobby tick is 15 a second" );
	}

	/// <summary>Wonder Land's file: slot 1, heading 270, camera 17.5, no flyers, no weather.</summary>
	[TestMethod]
	public void FantasysScriptReadsWhole()
	{
		var script = ReadShipped( "fantasy" );

		Assert.AreEqual( 1, script.Index, "slot" );
		Assert.AreEqual( "Fantasy", script.ParkName, "name" );
		Assert.AreEqual( 270f, script.Heading, 1e-4f, "heading" );
		Assert.AreEqual( 17.5f, script.CameraHeight, 1e-4f, "camera height" );
		AssertSky( script, 5, 170, 255 );
		Assert.AreEqual( 0, script.Rainy, "no RAINY line" );
		Assert.AreEqual( 0, script.Lightning, "no LIGHTNING line" );
		Assert.AreEqual( 0, script.FlyingMeshes.Count, "no FLYINGMESH line" );
	}

	/// <summary>Halloween: slot 2, heading 180, camera 38, fifty bats, rain and a LIGHTNING mask of 63.</summary>
	[TestMethod]
	public void HalloweensScriptReadsWhole()
	{
		var script = ReadShipped( "hallow" );

		Assert.AreEqual( 2, script.Index, "slot" );
		Assert.AreEqual( "Halloween", script.ParkName, "name" );
		Assert.AreEqual( 180f, script.Heading, 1e-4f, "heading" );
		Assert.AreEqual( 38f, script.CameraHeight, 1e-4f, "camera height" );
		AssertSky( script, 4, 44, 12 );
		Assert.AreEqual( 1, script.Rainy, "RAINY(1)" );
		Assert.AreEqual( 63, script.Lightning, "LIGHTNING(63)" );
		Assert.AreEqual( 25f / 64f, script.StrikesPerSecond, 1e-5f, "six mask bits: one frame in 64 at the assumed 25 a second" );

		Assert.AreEqual( 1, script.FlyingMeshes.Count, "one FLYINGMESH line" );
		AssertFlyer( script.FlyingMeshes[0], "bat", 50, 2.5f );
		Assert.AreEqual( 25f, script.FlyingMeshes[0].SpeedPerSecond, 1e-4f, "2.5 a lobby tick is 25 a second" );
	}

	/// <summary>Space Zone's file: slot 3, heading 0, camera 35, no flyers, no weather.</summary>
	[TestMethod]
	public void SpacesScriptReadsWhole()
	{
		var script = ReadShipped( "space" );

		Assert.AreEqual( 3, script.Index, "slot" );
		Assert.AreEqual( "Space", script.ParkName, "name" );
		Assert.AreEqual( 0f, script.Heading, 1e-4f, "heading" );
		Assert.AreEqual( 35f, script.CameraHeight, 1e-4f, "camera height" );
		AssertSky( script, 125, 8, 8 );
		Assert.AreEqual( 0, script.Rainy, "no RAINY line" );
		Assert.AreEqual( 0, script.Lightning, "no LIGHTNING line" );
		Assert.AreEqual( 0, script.FlyingMeshes.Count, "no FLYINGMESH line" );
	}

	/// <summary>The four scripts name the four slots once each, and the theme name is matched whatever its case.</summary>
	[TestMethod]
	public void TheFourScriptsHoldTheFourSlots()
	{
		var slots = new[] { "jungle", "fantasy", "hallow", "space" }.Select( theme => ReadShipped( theme ).Index ).ToArray();
		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3 }, slots, "the files' own slots, in the themes' order" );

		Assert.AreEqual( "Lost Kingdom", LobbyScript.Read( "Jungle" ).ParkName, "a theme name in capitals finds the same file" );
	}
}
