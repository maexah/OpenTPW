using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// <b>A park file that will not read keeps the running park</b> (Q994): the original's load checks the file before its
/// teardown (<c>FUN_00414d40</c>, <c>0x0041501d</c>), so a bad file loaded from a park (a Load Park row, Alt+L, Restart Park)
/// leaves the park running, and one entered with leaves the new world standing (<c>0x005ad054</c>; <c>docs/exe/saves.md</c>,
/// "The load"). Over a disposable save folder, the files cut from the shipped park.
/// </summary>
[TestClass]
public class ParkFileReadBeforeTheSceneEndsTests
{
	private string? root;
	private BaseFileSystem? oldSaves;
	private Level? _level;

	private string Jungle => Path.Combine( root!, "users", "1Test", "jungle" );

	[TestInitialize]
	public void IsolateSaves()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		oldSaves = SaveFileSystemOrNull;
		root = Directory.CreateTempSubdirectory( "opentpw-read-ahead-" ).FullName;
		Directory.CreateDirectory( Jungle );
		SaveFileSystem = new BaseFileSystem( root );
		_level = Level.Current;

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );
		ForgetWhatWasAsked();
	}

	[TestCleanup]
	public void RestoreSaves()
	{
		// Without the game the set-up stops before anything is changed.
		if ( root == null )
			return;

		ForgetWhatWasAsked();
		CarriedYear.SetValue( null, null );
		SetCurrentPlayer( null );
		SaveFileSystemOrNull = oldSaves;
		Directory.Delete( root, true );
		Level.Current = _level!;
	}

	/// <summary>
	/// The files that will not read: one cut inside the preamble, one cut in its compressed body (both short of what the
	/// block's size says), one whose body is spoilt at its full length (the inflate fails: the original's undefined case,
	/// filled), and four bytes of nothing.
	/// </summary>
	private IEnumerable<(string Name, byte[] Bytes)> BadFiles()
	{
		var shipped = FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" );

		var spoilt = (byte[])shipped.Clone();
		Array.Fill( spoilt, (byte)0xff, shipped.Length / 2, 64 );

		yield return ("cut in the preamble", shipped[..1000]);
		yield return ("cut in the body", shipped[..(shipped.Length / 2)]);
		yield return ("spoilt in the body", spoilt);
		yield return ("four bytes", [1, 2, 3, 4]);
	}

	/// <summary>
	/// <b>Loaded from a park, a file that will not read takes nothing down</b>: neither the unload nor the build is run, the
	/// running park stays the current scene, the year is not carried and the ask is spent. The shipped park's copy, which
	/// reads, is unloaded for and built from the file read here, its 755.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the read moved below the unload (the scene ended before the check, as before Q994) unloads for every
	/// bad file; the failure's <c>return</c> dropped builds a park from nothing; the check moved below the carry carries the
	/// year; the file read here not handed on gives the build no world.
	/// </remarks>
	[TestMethod]
	public void AFileThatWillNotReadKeepsTheRunningPark()
	{
		var running = AParkRunning();

		foreach ( var (name, bytes) in BadFiles() )
		{
			var path = Path.Combine( Jungle, "Bad.TPWS" );
			File.WriteAllBytes( path, bytes );

			var done = Change( Path.Join( "users", "1Test", "jungle", "Bad.TPWS" ), out var built );

			Assert.AreEqual( "", done, $"{name}: nothing is taken down" );
			Assert.IsNull( built, name );
			Assert.AreSame( running, Level.Current, $"{name}: the running park is still the scene" );
			Assert.IsNull( CarriedYear.GetValue( null ), $"{name}: the year is not carried" );
			Assert.IsNull( Asked( "_parkAsked" ).GetValue( null ), $"{name}: the ask is spent" );
		}

		File.WriteAllBytes( Path.Combine( Jungle, "Good.TPWS" ), FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var good = Change( Path.Join( "users", "1Test", "jungle", "Good.TPWS" ), out var read );

		Assert.AreEqual( "unload, park", good );
		Assert.AreEqual( 755, read?.Read?.GameTick, "the park is built from the file read before the unload" );
	}

	/// <summary>
	/// <b>Entered with a newest file that will not read, the world is made fresh</b>, as the original's new world stands:
	/// before Q994 it gave no park at all, nothing placed and every save refused. An older file that reads is not taken
	/// in its place: the original loads the newest or nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the fresh world dropped from the failure gives null; the next newest file read instead gives 755.</remarks>
	[TestMethod]
	public void EnteringWithANewestFileThatWillNotReadMakesTheWorldFresh()
	{
		var older = Path.Combine( Jungle, "easymode.TPWI" );
		File.WriteAllBytes( older, FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		File.SetLastWriteTimeUtc( older, DateTime.UtcNow.AddDays( -3 ) );

		foreach ( var (name, bytes) in BadFiles() )
		{
			File.WriteAllBytes( Path.Combine( Jungle, "autosave.TPWS" ), bytes );

			Assert.IsInstanceOfType( Enter( Level.ParkStart.Entered ), typeof( FreshPark ), name );
		}
	}

	/// <summary>
	/// <b>The park is built from the file read ahead, not read again</b>, and a world asked for fresh is fresh whatever
	/// the folder holds (Restart Park on a park made fresh with no <c>restart.INTS</c>: S-M3 of the week-end audit
	/// 2026-10-31, where the newest file, an Alt+S since, was entered).
	/// </summary>
	/// <remarks><b>Mutations:</b> the world read ahead ignored enters the newest file, a different world; the fresh ask taken
	/// for a plain entry enters the newest file, 755.</remarks>
	[TestMethod]
	public void TheParkIsBuiltFromTheFileReadAheadOrFreshAsAsked()
	{
		File.WriteAllBytes( Path.Combine( Jungle, "jungle.TPWS" ), FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		var ahead = Level.ReadParkAhead( "jungle", Path.Join( "users", "1Test", "jungle", "jungle.TPWS" ) );
		Assert.IsNotNull( ahead );

		Assert.AreSame( ahead, Enter( new Level.ParkStart( "jungle.TPWS", ahead, false ) ) );
		Assert.IsInstanceOfType( Enter( Level.ParkStart.MadeFresh ), typeof( FreshPark ) );
		Assert.AreEqual( 755, Enter( Level.ParkStart.Entered )?.GameTick, "a plain entry takes the newest" );

		Game.RequestFreshPark( "jungle" );
		var asked = (Level.ParkStart?)null;
		Game.ChangeSceneIfAsked( () => { }, ( _, start ) => asked = start, () => { } );
		Assert.AreSame( Level.ParkStart.MadeFresh, asked );
	}

	/// <summary>
	/// <b>Alt+L's answer is logged once the file has been read</b> (Q1179): the key only asks, and the scene change says
	/// "QuickSave Load Failed" for a <c>jungle.TPWS</c> that will not read and "QuickSave Loaded" for one that reads, as
	/// the original picks the string from its load's answer (<c>0x00747ed4</c>, <c>0x007486bc</c>). A Load Park row's
	/// ask, even one made over Alt+L's in the same frame, logs neither.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the key's old "QuickSave Loaded" at the ask fails the key's line; the failure's line dropped, or
	/// the loaded line logged before the read, fails the cut file; the quick mark not cleared by a plain ask logs the
	/// row's load as Alt+L's.
	/// </remarks>
	[TestMethod]
	public void AltLsAnswerIsLoggedOnceTheFileIsRead()
	{
		var running = AParkRunning();
		typeof( Level ).GetProperty( nameof( Level.ThemeName ) )!.SetValue( running, "jungle" );

		var shipped = FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" );
		var quick = Path.Combine( Jungle, "jungle.TPWS" );

		File.WriteAllBytes( quick, shipped[..(shipped.Length / 2)] );
		var (key, cut) = QuickLines( running.QuickLoadKey );

		Assert.IsFalse( key.Contains( "QuickSave" ), $"the key only asks: {key}" );
		CollectionAssert.AreEqual( new[] { "Quickload: QuickSave Load Failed - users/1Test/jungle/jungle.TPWS will not read" },
			cut.Select( line => line.Replace( '\\', '/' ) ).ToArray() );

		File.WriteAllBytes( quick, shipped );
		var (_, good) = QuickLines( running.QuickLoadKey );

		CollectionAssert.AreEqual( new[] { "Quickload: QuickSave Loaded - users/1Test/jungle/jungle.TPWS is loaded" },
			good.Select( line => line.Replace( '\\', '/' ) ).ToArray() );

		var (_, row) = QuickLines( () =>
		{
			var said = running.QuickLoadKey();
			Game.RequestParkLoad( "jungle", Path.Join( "users", "1Test", "jungle", "jungle.TPWS" ) );
			return said;
		} );

		Assert.AreEqual( 0, row.Length, "a Load Park row's ask over Alt+L's is not Alt+L's" );
	}

	/// <summary>The key's own line, and the Quickload lines the scene change after it logged.</summary>
	private static (string Key, string[] Changed) QuickLines( Func<string> press )
	{
		var said = new List<string>();
		Logger.LogDelegate listen = ( _, line ) => said.Add( line );
		var key = press();

		Logger.OnLog += listen;

		try
		{
			Game.ChangeSceneIfAsked( () => { }, ( _, _ ) => { }, () => { } );
		}
		finally { Logger.OnLog -= listen; }

		return (key, said.Where( line => line.Contains( "Quickload:" ) ).ToArray());
	}

	/// <summary>
	/// <b>A file read ahead takes its particle density when the park is built from it, and not before</b>: reading it takes
	/// nothing into the running game, and the build runs the park at the file's 500 where the options' are 1000
	/// (<c>docs/exe/park.md</c>, "A park's particles").
	/// </summary>
	/// <remarks><b>Mutations:</b> the read-ahead branch's <c>TakeParticleDensity</c> deleted leaves 1000 (every park loaded from
	/// a row, Alt+L or Restart Park at the options' density); the density taken in <c>ReadParkAhead</c> gives 500 before the build.</remarks>
	[TestMethod]
	public void AFileReadAheadTakesItsParticleDensityWhenTheParkIsBuilt()
	{
		File.WriteAllBytes( Path.Combine( Jungle, "jungle.TPWS" ), FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var system = new ParticleSystem( "Particle/Tp2.plb", 1000 );

		try
		{
			var ahead = Level.ReadParkAhead( "jungle", Path.Join( "users", "1Test", "jungle", "jungle.TPWS" ) );
			Assert.IsNotNull( ahead );
			Assert.AreEqual( 1000, system.Density, "reading ahead takes nothing into the running game" );

			Enter( new Level.ParkStart( "jungle.TPWS", ahead, false ) );
			Assert.AreEqual( 500, system.Density, "the park is built at its file's density" );
		}
		finally
		{
			system.Shutdown();
		}
	}

	/// <summary>The park asked for, changed to: what was done, and what the build was handed.</summary>
	private static string Change( string file, out Level.ParkStart? built )
	{
		var done = new List<string>();
		Level.ParkStart? handed = null;

		Game.RequestParkLoad( "jungle", file );
		Game.ChangeSceneIfAsked( () => done.Add( "unload" ), ( _, start ) => { done.Add( "park" ); handed = start; }, () => done.Add( "lobby" ) );

		built = handed;
		return string.Join( ", ", done );
	}

	private static IParkInitialState? Enter( Level.ParkStart start )
		=> Level.EnterPark( "jungle", new ParkBalance( "jungle", easyMode: Level.InstantAction ),
			new ParkItemCatalogue( "jungle", instantAction: Level.InstantAction ), start );

	/// <summary>A running park's scene, as far as the scene change asks of it.</summary>
	private static Level AParkRunning()
	{
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( level, Level.Scene.Park );
		Level.Current = level;
		return level;
	}

	private static FieldInfo CarriedYear => typeof( GameCalendar ).GetField( "_carriedYear", BindingFlags.Static | BindingFlags.NonPublic )!;

	private static void ForgetWhatWasAsked()
	{
		Asked( "_parkAsked" ).SetValue( null, null );
		Asked( "_parkFileAsked" ).SetValue( null, null );
		Asked( "_parkFreshAsked" ).SetValue( null, false );
		Asked( "_parkQuickAsked" ).SetValue( null, false );
	}

	private static FieldInfo Asked( string field ) => typeof( Game ).GetField( field, BindingFlags.Static | BindingFlags.NonPublic )!;

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );
}
