using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The lens: every view hands the projection an angle, and the original's screen scale diag( 1.5, -2 ) narrows the picture
/// to <c>2 atan( tan(a/2) / 2 )</c> (<c>docs/exe/park-engine.md</c>, "The lens", Q260). Only the lobby's file and its isle need
/// the game.
/// </summary>
[TestClass]
public class CameraLensTests
{
	/// <summary>The park's handed 90 draws 53.13 high, the lobby's ISLANDFOV 100 draws 61.58 (Q260, measured live).</summary>
	[TestMethod]
	public void TheScreenScaleHalvesTheTangent()
	{
		Assert.AreEqual( 53.1301f, Camera.DrawnAngle( 90f ), 1e-3f, "the park" );
		Assert.AreEqual( 61.5795f, Camera.DrawnAngle( 100f ), 1e-3f, "the lobby" );
	}

	/// <summary>
	/// At 4:3 the park's projection is the original's draw matrix: x/w 1.5 and y/w 2 a unit of tangent off the eye's axis
	/// (<c>[0x0087b088]</c>, Q435), and the pointer's ray reaches 0.667 across and 0.5 down a unit ahead, its picking's own
	/// half-tangents (<c>FUN_0045bf90</c>).
	/// </summary>
	[TestMethod]
	public void TheParkDrawsTheOriginalsMatrixAt4By3()
	{
		var lens = Camera.Lens( ParkOrbitCameraMode.Lens, 4f / 3f );

		Assert.AreEqual( 1.5f, lens.Projection.M11, 1e-4f, "x/w per unit of tangent" );
		Assert.AreEqual( 2f, lens.Projection.M22, 1e-4f, "y/w per unit of tangent" );
		Assert.AreEqual( 0.6667f, lens.Across, 1e-4f, "across, a unit ahead" );
		Assert.AreEqual( 0.6667f, lens.ReferenceAcross, 1e-4f, "and at 4:3 itself" );
	}

	/// <summary>A wider window keeps the picture's height and sees further to the sides: at 16:9, 0.5 x 16/9 across.</summary>
	[TestMethod]
	public void AWiderWindowKeepsTheHeight()
	{
		var lens = Camera.Lens( ParkOrbitCameraMode.Lens, 16f / 9f );

		Assert.AreEqual( 2f, lens.Projection.M22, 1e-4f, "the same height" );
		Assert.AreEqual( 0.8889f, lens.Across, 1e-4f, "further across" );
		Assert.AreEqual( 0.6667f, lens.ReferenceAcross, 1e-4f, "against 4:3's" );
	}

	/// <summary>Both park cameras draw 53.13: nothing in a park hands the projection another angle.</summary>
	[TestMethod]
	public void BothParkCamerasDrawTheSameLens()
	{
		Assert.AreEqual( 53.1301f, new ParkOrbitCameraMode().FieldOfView, 1e-3f, "the orbit camera" );
		Assert.AreEqual( 53.1301f, new ParkCamcorderCameraMode().FieldOfView, 1e-3f, "the camcorder's first person" );
	}

	/// <summary>
	/// The lobby takes lobby.txt's ISLANDFOV, 100 in the shipped file, through the same law. 100 is also what the reader falls
	/// back to, so the file is opened here through the game's file system and its ISLANDFOV line found in the stream before
	/// the reader is trusted to have read it (<see cref="TheLobbyReadsItsFileFromTheFileSystem"/> shows the reader's own path).
	/// </summary>
	[TestMethod]
	public void TheLobbyDrawsItsFilesAngle()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		using var stream = FileSystem.OpenReadOrNull( "lobby/lobby.txt" );
		Assert.IsNotNull( stream, "lobby/lobby.txt is in the game's files" );

		var lines = new List<string>();
		using ( var reader = new StreamReader( stream ) )
		{
			while ( reader.ReadLine() is { } line )
				lines.Add( line );
		}

		var found = lines.Where( line => LobbyScript.TryReadSetting( line.TrimStart(), "ISLANDFOV", out _ ) ).ToList();
		Assert.AreEqual( 1, found.Count, "one ISLANDFOV line in the file" );
		LobbyScript.TryReadSetting( found[0].TrimStart(), "ISLANDFOV", out var inFile );
		Assert.AreEqual( 100f, inFile, 1e-4f, "the file's own line" );

		var settings = LobbyCameraMode.ReadSettings();

		Assert.AreEqual( inFile, settings.IslandFov, 1e-4f, "what the caller read is that line" );
		Assert.AreEqual( 61.5795f, LobbyCameraMode.LensOf( settings ), 1e-3f, "drawn" );
	}

	/// <summary>
	/// <see cref="LobbyCameraMode.ReadSettings()"/> opens <c>lobby/lobby.txt</c> through the file system and parses that
	/// stream: a folder holding one with values the reader's fallbacks (70, 20, 100) are not, and no game needed. A wrong
	/// path throws or gets the fallbacks; an unread stream gets them too.
	/// </summary>
	[TestMethod]
	public void TheLobbyReadsItsFileFromTheFileSystem()
	{
		var root = Directory.CreateTempSubdirectory( "opentpw-lobbycam-" ).FullName;
		var old = FileSystemOrNull;

		try
		{
			Directory.CreateDirectory( Path.Combine( root, "lobby" ) );
			File.WriteAllText( Path.Combine( root, "lobby", "lobby.txt" ), "SPINRADIUS(60)\n  ISLANDFOV(80)\nVERTICALOFFSET(25)\n" );
			FileSystemOrNull = new BaseFileSystem( root );

			var settings = LobbyCameraMode.ReadSettings();

			Assert.AreEqual( 80f, settings.IslandFov, 1e-4f, "ISLANDFOV from the file" );
			Assert.AreEqual( 60f, settings.SpinRadius, 1e-4f, "SPINRADIUS from the file" );
			Assert.AreEqual( 25f, settings.VerticalOffset, 1e-4f, "VERTICALOFFSET from the file" );
		}
		finally
		{
			FileSystemOrNull = old;
			Directory.Delete( root, true );
		}
	}

	/// <summary>
	/// The game keeps lobby.txt inside <c>lobby.wad</c>; an archive without it hands the reader no stream, and the reader draws
	/// the values the shipped file has (70, 20, 100), as it does for a file it cannot find. A stand-in archive holds nothing.
	/// </summary>
	[TestMethod]
	public void AnArchiveWithoutLobbyTxtLeavesTheShippedValues()
	{
		var root = Directory.CreateTempSubdirectory( "opentpw-lobbycam-" ).FullName;
		var old = FileSystemOrNull;

		try
		{
			File.WriteAllBytes( Path.Combine( root, "lobby.wad" ), [] );
			var files = new BaseFileSystem( root );
			files.RegisterArchiveHandler( ".wad", _ => new EmptyArchive() );
			FileSystemOrNull = files;

			var settings = LobbyCameraMode.ReadSettings();

			Assert.AreEqual( new LobbyCameraMode.CameraSettings( 70f, 20f, 100f ), settings, "the shipped values" );
		}
		finally
		{
			FileSystemOrNull = old;
			Directory.Delete( root, true );
		}
	}

	/// <summary>An archive that holds no file: every ask for one answers nothing.</summary>
	private sealed class EmptyArchive : IArchive
	{
		public string[] GetFiles( string internalPath ) => [];
		public string[] GetDirectories( string internalPath ) => [];
		public ArchiveFile? GetFile( string internalPath ) => null;
		public byte[] GetData( int offset, int length ) => [];
		public Stream? OpenFile( string path ) => null;
		public long GetFileSize( string path ) => 0;
		public DateTime GetModifiedTime() => default;
		public void Dispose() { }
	}

	/// <summary>
	/// The lobby's camera draws its file's angle both ways it runs: flying itself with nobody playing, and orbiting the
	/// island a picked player is on. A stand-in isle (<see cref="LobbyCountedGapsTests.AnIsle"/>) with the jungle's script,
	/// and an ISLANDFOV of 80, so no fixed number passes.
	/// </summary>
	[TestMethod]
	public void TheLobbyCameraDrawsItFlyingAndOrbiting()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		var isle = LobbyCountedGapsTests.AnIsle();
		LobbyCountedGapsTests.SetField( isle, "<Script>k__BackingField", LobbyScript.Read( "jungle" ) );

		var camera = new LobbyCameraMode();
		LobbyCountedGapsTests.SetField( camera, "_islands", new List<LobbyIsland> { isle } );
		LobbyCountedGapsTests.SetField( camera, "_settings", (LobbyCameraMode.CameraSettings?)new LobbyCameraMode.CameraSettings( 70f, 20f, 80f ) );

		try
		{
			LobbyCountedGapsTests.SetCurrentPlayer( null );
			camera.Update();
			Assert.AreEqual( 45.5210f, camera.FieldOfView, 1e-3f, "flying, nobody playing" );

			camera.FieldOfView = 0f;
			LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );
			camera.Update();
			Assert.AreEqual( 45.5210f, camera.FieldOfView, 1e-3f, "orbiting a picked player's island" );
		}
		finally
		{
			LobbyCountedGapsTests.SetCurrentPlayer( null );
			LobbyCameraMode.ForgetIsland();
		}
	}

	/// <summary>Another lobby.txt's ISLANDFOV is drawn by the same law, and one without it draws the shipped 100.</summary>
	[TestMethod]
	public void AnotherFilesAngleIsDrawnTheSameWay()
	{
		var other = LobbyCameraMode.ReadSettings( new StringReader( "SPINRADIUS(70)\n  ISLANDFOV(80)\nVERTICALOFFSET(20)\n" ) );
		var none = LobbyCameraMode.ReadSettings( new StringReader( "SPINRADIUS(70)\n" ) );

		Assert.AreEqual( 80f, other.IslandFov, 1e-4f, "read" );
		Assert.AreEqual( 45.5210f, LobbyCameraMode.LensOf( other ), 1e-3f, "2 atan( tan 40 / 2 )" );
		Assert.AreEqual( 61.5795f, LobbyCameraMode.LensOf( none ), 1e-3f, "the shipped 100" );
	}
}
