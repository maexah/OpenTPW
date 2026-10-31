using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Where the tests that read real game files get them from.
///
/// <para>
/// A test run reaches only the folder <c>OPENTPW_GAME_PATH</c> names, asked of the game's own <see cref="GameDir"/> so that
/// folder is judged as the game judges it. Nothing else is looked in: not the working directory, not the build's folder, and
/// not the places a Windows install can be, because a PC with Sim Theme Park installed would then run the suite against a
/// player's own copy (AGENTS.md rule 12; Q986). No variable means no game, on every system.
/// </para>
/// <para>
/// Where there is no installation the tests needing one are skipped rather than failed, so a contributor who
/// has never owned the game still gets a green run.
/// </para>
/// </summary>
internal static class GameData
{
	public const string GamePathVariable = "OPENTPW_GAME_PATH";

	/// <summary>
	/// Whether the folder <paramref name="named"/> holds the game, asked through <paramref name="find"/> with that one folder
	/// given as <c>--game</c>. Nothing named is false without asking, so the game's other places are never reached.
	/// </summary>
	internal static bool Locate( string? named, Func<string[], bool> find )
		=> !string.IsNullOrWhiteSpace( named ) && find( ["--game", named] );

	/// <summary>Mounted once and kept: every test using it would otherwise read the archives again each time.</summary>
	private static BaseFileSystem? _data;

	/// <summary>
	/// The game's data folder, mounted, or the end of the calling test: <see cref="Assert.Inconclusive(string)"/>
	/// throws, and MSTest reports what it throws from as skipped rather than failed.
	/// </summary>
	public static BaseFileSystem Required()
	{
		if ( _data != null )
			return _data;

		// GameDir says where it looked through the log, which TestRun makes before any test runs.
		if ( !Locate( Environment.GetEnvironmentVariable( GamePathVariable ), GameDir.Find ) )
			Assert.Inconclusive( $"skipped: {GamePathVariable} does not name a Theme Park World installation" );

		var data = new BaseFileSystem( GameDir.Data );

		// Exactly what Game.Run registers, so the tests see the game's own file system and not a near miss.
		data.RegisterArchiveHandler( ".wad", path => new WadArchive( path ) );
		data.RegisterArchiveHandler( ".sdt", path => new SdtArchive( path ) );

		return _data = data;
	}
}
