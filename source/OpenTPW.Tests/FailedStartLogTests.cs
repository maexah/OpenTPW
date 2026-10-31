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
		var host = RepoFiles.Dotnet();

		if ( !File.Exists( dll ) || host == null )
			Assert.Inconclusive( "skipped: the built program or the dotnet that runs it cannot be found from here" );

		// A game folder named in the environment must not rescue the run: --game below is the one that is looked at.
		environment["OPENTPW_GAME_PATH"] = null;
		environment["SDL_AUDIODRIVER"] = "dummy";

		// The message box a failed start shows (Q978) must not open on the desktop of whoever runs the suite.
		environment[StartFailure.SuppressVariable] = "1";

		// A path that goes wrong lands in scratch, never in the player's own data folder (AGENTS.md rule 12): the data
		// folder follows XDG_DATA_HOME (else HOME) on Linux only. On Windows it is the shell's known folder and on macOS the system's
		// Application Support folder, and no variable set here moves either.
		environment.TryAdd( "HOME", Path.Join( workingFolder, "home" ) );
		environment.TryAdd( "USERPROFILE", environment["HOME"] );
		environment.TryAdd( "XDG_DATA_HOME", Path.Join( workingFolder, "xdg" ) );
		environment.TryAdd( "APPDATA", Path.Join( workingFolder, "appdata" ) );
		environment.TryAdd( "LOCALAPPDATA", Path.Join( workingFolder, "appdata" ) );

		return RepoFiles.Run( host!, [dll, .. arguments], environment, workingFolder )!.Value;
	}

	/// <summary>The line the log keeps of the box that was kept off: the message it would have shown, on one line.</summary>
	private static string BoxLine( string log )
		=> log.Split( '\n' ).Single( line => line.Contains( $"Start failure: no message box, {StartFailure.SuppressVariable} is set" ) );

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
			StringAssert.Contains( log, $"Start failure: no message box, {StartFailure.SuppressVariable} is set", "the start went on to the box, which the variable kept off" );

			// What the box would have said: the reasons, and the log's place (a box shown before the log is opened says "Log: none").
			var box = BoxLine( log );
			StringAssert.Contains( box, StartFailure.Head );
			StringAssert.Contains( box, "does not hold Theme Park World" );
			StringAssert.Contains( box, $"Log: {Path.Join( logs, ConsoleLog.FileName )}" );
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

	/// <summary>
	/// An exception before the main loop is logged, answered by the box (kept off here) and ends the run with exit 1 (Q978).
	/// Linux only: with no display variable SDL has no window to make, which is the exception; elsewhere the same start would
	/// open a window on whoever runs the suite.
	/// </summary>
	[TestMethod]
	public void AnExceptionBeforeTheMainLoopIsLoggedAndEndsTheRunWithExit1()
	{
		if ( !OperatingSystem.IsLinux() )
			Assert.Inconclusive( "skipped: only Linux can be given no display to fail on" );

		var folder = Scratch();
		var logs = Path.Join( folder, "logs" );

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "game", "data", "levels" ) );

			var (exit, output) = Start( folder, new() { ["OPENTPW_LOG_DIR"] = logs, ["DISPLAY"] = null, ["WAYLAND_DISPLAY"] = null }, "--game", Path.Join( folder, "game" ) );

			Assert.AreEqual( 1, exit, output );

			var log = File.ReadAllText( Path.Join( logs, ConsoleLog.FileName ) );

			StringAssert.Contains( log, "Unhandled exception:", "the stack is in the log" );

			var box = BoxLine( log );
			StringAssert.Contains( box, "Exception: ", "the box names the exception" );
			StringAssert.Contains( box, $"Log: {Path.Join( logs, ConsoleLog.FileName )}" );
		}
		finally { Directory.Delete( folder, true ); }
	}
}
