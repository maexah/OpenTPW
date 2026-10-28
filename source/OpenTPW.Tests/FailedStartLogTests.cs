using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A launch that finds no game still keeps console.log (Q798): the player has nowhere else to look, and the issue
/// forms ask for the file. These start the built program, as a player does, with a game folder that is not one.
/// </summary>
[TestClass]
public class FailedStartLogTests
{
	private static string Scratch() => Directory.CreateTempSubdirectory( "tpw-failedstart" ).FullName;

	/// <summary>The program built beside the tests, run on the same dotnet; the end of the test where neither can be found.</summary>
	private static (int Exit, string Output) Start( string workingFolder, Dictionary<string, string?> environment, params string[] arguments )
	{
		var dll = Path.Join( AppContext.BaseDirectory, "OpenTPW.dll" );
		var host = Environment.ProcessPath;

		if ( !File.Exists( dll ) || host == null || !Path.GetFileNameWithoutExtension( host ).Equals( "dotnet", StringComparison.OrdinalIgnoreCase ) )
			Assert.Inconclusive( "skipped: the built program or the dotnet that runs it cannot be found from here" );

		// A game folder named in the environment must not rescue the run: --game below is the one that is looked at.
		environment["OPENTPW_GAME_PATH"] = null;
		environment["SDL_AUDIODRIVER"] = "dummy";

		// A path that goes wrong lands in scratch, never in the player's own data folder (AGENTS.md rule 12): the data
		// folder follows XDG_DATA_HOME on Linux, HOME on macOS and APPDATA on Windows.
		environment.TryAdd( "HOME", Path.Join( workingFolder, "home" ) );
		environment.TryAdd( "USERPROFILE", environment["HOME"] );
		environment.TryAdd( "XDG_DATA_HOME", Path.Join( workingFolder, "xdg" ) );
		environment.TryAdd( "APPDATA", Path.Join( workingFolder, "appdata" ) );
		environment.TryAdd( "LOCALAPPDATA", Path.Join( workingFolder, "appdata" ) );

		return RepoFiles.Run( host!, [dll, .. arguments], environment, workingFolder )!.Value;
	}

	[TestMethod]
	public void AFolderThatIsNotTheGameLeavesAConsoleLogWithTheReason()
	{
		var folder = Scratch();
		var logs = Path.Join( folder, "logs" );

		try
		{
			var (exit, output) = Start( folder, new() { ["OPENTPW_LOG_DIR"] = logs }, "--game", folder );

			Assert.AreEqual( 1, exit, output );

			var log = File.ReadAllText( Path.Join( logs, ConsoleLog.FileName ) );

			StringAssert.Contains( log, "(no game folder was found)" );
			StringAssert.Contains( log, "does not hold Theme Park World" );
			StringAssert.Contains( log, "Game files:" );
			StringAssert.Contains( output, $"this run is kept in {Path.Join( logs, ConsoleLog.FileName )}", "the terminal says where it is" );
			Assert.IsFalse( File.Exists( Path.Join( folder, ConsoleLog.FileName ) ), "nothing is written in the folder that was named" );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void AFolderThatDoesNotExistLeavesOneToo()
	{
		var folder = Scratch();
		var logs = Path.Join( folder, "logs" );

		try
		{
			var (exit, output) = Start( folder, new() { ["OPENTPW_LOG_DIR"] = logs }, "--game", Path.Join( folder, "not-there" ) );

			Assert.AreEqual( 1, exit, output );
			StringAssert.Contains( File.ReadAllText( Path.Join( logs, ConsoleLog.FileName ) ), "there is no folder at" );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void ItIsRotatedLikeAnyOtherLaunchesAndNeverNamesTheHomeFolder()
	{
		var folder = Scratch();
		var logs = Path.Join( folder, "logs" );

		try
		{
			Directory.CreateDirectory( logs );
			File.WriteAllText( Path.Join( logs, ConsoleLog.FileName ), "an earlier launch" );

			// The game folder named is under the child's home folder, so the list of what was tried holds a home path
			// that the log must print as "~".
			var home = Path.Join( folder, "home" );
			var named = Path.Join( home, "Games", "not-the-game" );

			Directory.CreateDirectory( named );
			Start( folder, new() { ["OPENTPW_LOG_DIR"] = logs, ["HOME"] = home, ["USERPROFILE"] = home }, "--game", named );

			Assert.AreEqual( "an earlier launch", File.ReadAllText( Path.Join( logs, ConsoleLog.PreviousName ) ) );

			var log = File.ReadAllText( Path.Join( logs, ConsoleLog.FileName ) );

			StringAssert.Contains( log, "does not hold" );
			StringAssert.Contains( log, "~", "the home folder is written as ~" );
			Assert.IsFalse( log.Contains( home ), "the home folder is never named: " + log );
		}
		finally { Directory.Delete( folder, true ); }
	}

	/// <remarks>Without OPENTPW_LOG_DIR the file goes in the player's own data folder; on Linux that is under XDG_DATA_HOME.</remarks>
	[TestMethod]
	public void WithoutAGameFolderTheFileGoesInThePlayersDataFolder()
	{
		if ( !OperatingSystem.IsLinux() )
			Assert.Inconclusive( "skipped: the data folder is moved by XDG_DATA_HOME only on Linux" );

		var folder = Scratch();

		try
		{
			var data = Path.Join( folder, "data" );
			var (exit, output) = Start( folder, new() { ["OPENTPW_LOG_DIR"] = null, ["XDG_DATA_HOME"] = data }, "--game", folder );

			Assert.AreEqual( 1, exit, output );
			StringAssert.Contains( File.ReadAllText( Path.Join( data, "OpenTPW", ConsoleLog.FileName ) ), "does not hold Theme Park World" );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void TheFoldersToTryAreTheVariablesAloneOtherwiseThePlayersOwn()
	{
		CollectionAssert.AreEqual( new[] { "/data/OpenTPW" }, ConsoleLog.NoGameCandidates( null, "/data/OpenTPW" ) );
		CollectionAssert.AreEqual( new[] { "/data/OpenTPW" }, ConsoleLog.NoGameCandidates( "  ", "/data/OpenTPW" ) );
		CollectionAssert.AreEqual( new[] { "/logs" }, ConsoleLog.NoGameCandidates( "/logs", "/data/OpenTPW" ) );
	}
}
