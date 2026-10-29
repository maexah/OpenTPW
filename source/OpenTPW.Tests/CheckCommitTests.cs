using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The contributor's commit check (<c>tools/check_commit.sh</c>): a commit passes only when its build exits 0 with no
/// error, its tests exit 0 with none failed and the content guard refuses no file in its tree. Each test runs the script
/// in a scratch repository with a stand-in <c>dotnet</c> on the PATH, so what the build and the tests print and answer is
/// chosen here, and reads the script's own exit code, not its summary alone.
/// </summary>
[TestClass]
public class CheckCommitTests
{
	private const string BuiltClean = "    0 Warning(s)\n    0 Error(s)\n";
	private const string TestsPassed = "Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 1 s\n";

	/// <summary>A scratch repository holding copies of the script, the guard and the disc's list, one commit made.</summary>
	private sealed class Scratch : IDisposable
	{
		public readonly string Folder = Path.Join( Path.GetTempPath(), "opentpw-check-" + Guid.NewGuid().ToString( "N" ) );
		private string Bin => Path.Join( Folder, "bin" );
		public string Temp => Path.Join( Folder, "tmp" );
		public string Repo => Path.Join( Folder, "repo" );

		public Scratch()
		{
			var root = RepoFiles.Root();

			if ( OperatingSystem.IsWindows() || RepoFiles.Run( "bash", "-c", "true" ) is not { Exit: 0 } )
				Assert.Inconclusive( "skipped: no bash here (the script is run on Linux and macOS, and on Windows from Git Bash)" );

			Directory.CreateDirectory( Path.Join( Repo, "tools" ) );
			Directory.CreateDirectory( Path.Join( Repo, "source", "OpenTPW", "Client" ) );
			Directory.CreateDirectory( Bin );
			Directory.CreateDirectory( Temp );
			File.Copy( Path.Join( root, "tools", "check_commit.sh" ), Path.Join( Repo, "tools", "check_commit.sh" ) );
			File.Copy( Path.Join( root, "tools", "content_guard.py" ), Path.Join( Repo, "tools", "content_guard.py" ) );
			File.Copy( Path.Join( root, "source", "OpenTPW", "Client", "DiscData.txt" ), Path.Join( Repo, "source", "OpenTPW", "Client", "DiscData.txt" ) );
			File.WriteAllText( Path.Join( Repo, "README.md" ), "x\n" );

			// The stand-in answers build and test with what each test puts in FAKE_*; it ignores the solution.
			var dotnet = Path.Join( Bin, "dotnet" );
			File.WriteAllText( dotnet, "#!/bin/sh\ncase \"$1\" in\n"
				+ "build) printf '%b' \"$FAKE_BUILD_OUT\"; exit \"${FAKE_BUILD_EXIT:-0}\" ;;\n"
				+ "test) printf '%b' \"$FAKE_TEST_OUT\"; exit \"${FAKE_TEST_EXIT:-0}\" ;;\nesac\n" );
			if ( !OperatingSystem.IsWindows() )
				File.SetUnixFileMode( dotnet, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute );

			if ( Git( "init", "-q" ).Exit != 0 )
				Assert.Inconclusive( "skipped: no git here" );

			Git( "config", "user.email", "a@b.c" );
			Git( "config", "user.name", "scratch" );
			Git( "config", "commit.gpgsign", "false" );
			Commit( "base", "README.md", "tools/check_commit.sh", "tools/content_guard.py", "source/OpenTPW/Client/DiscData.txt" );
		}

		public (int Exit, string Output) Git( params string[] arguments )
			=> RepoFiles.Run( "git", arguments, null, Repo ) ?? (127, "no git");

		public void Commit( string message, params string[] files )
		{
			foreach ( var file in files )
			{
				if ( !File.Exists( Path.Join( Repo, file ) ) )
					File.WriteAllText( Path.Join( Repo, file ), "x\n" );
			}

			Assert.AreEqual( 0, Git( new[] { "add", "--" }.Concat( files ).ToArray() ).Exit );
			Assert.AreEqual( 0, Git( "commit", "-q", "-m", message ).Exit );
		}

		/// <summary>Runs the script with the build's and the tests' output and exit codes chosen; the script's own exit code and output.</summary>
		public (int Exit, string Output) Check( string[] arguments, string build = BuiltClean, int buildExit = 0, string tests = TestsPassed, int testExit = 0 )
		{
			var environment = new Dictionary<string, string?>
			{
				["PATH"] = Bin + Path.PathSeparator + Environment.GetEnvironmentVariable( "PATH" ),
				["TMPDIR"] = Temp,
				["CHECK_DIR"] = null,
				["OPENTPW_GAME_PATH"] = null,
				["FAKE_BUILD_OUT"] = build,
				["FAKE_BUILD_EXIT"] = buildExit.ToString(),
				["FAKE_TEST_OUT"] = tests,
				["FAKE_TEST_EXIT"] = testExit.ToString(),
			};
			var result = RepoFiles.Run( "bash", new[] { "tools/check_commit.sh" }.Concat( arguments ).ToArray(), environment, Repo );

			Assert.IsNotNull( result );

			// Pass or fail, the throwaway worktree and its folder are gone.
			Assert.AreEqual( 1, Git( "worktree", "list", "--porcelain" ).Output.Split( '\n' ).Count( l => l.StartsWith( "worktree " ) ), result.Value.Output );
			Assert.AreEqual( 0, Directory.GetFileSystemEntries( Temp ).Length, result.Value.Output );
			return result.Value;
		}

		public void Dispose()
		{
			try { Directory.Delete( Folder, true ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { }
		}
	}

	/// <remarks>The control for the tests below: the stand-in's clean build and passing tests are a pass.</remarks>
	[TestMethod]
	public void ACleanBuildAndPassingTestsPass()
	{
		using var scratch = new Scratch();
		var (exit, output) = scratch.Check( [] );

		Assert.AreEqual( 0, exit, output );
		StringAssert.Contains( output, "result:  OK" );
		StringAssert.Contains( output, "build:   exit 0, 0 errors" );
	}

	/// <remarks><b>Mutation:</b> judging the build by its printed error count alone (dropping <c>build_exit</c> from <c>built</c>) passes this build.</remarks>
	[TestMethod]
	public void ABuildThatExitsNonZeroFailsEvenWhenItPrintsNoError()
	{
		using var scratch = new Scratch();
		var (exit, output) = scratch.Check( [], buildExit: 1 );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "build:   exit 1, 0 errors" );
		StringAssert.Contains( output, "tests:   not run (the build failed)" );
		StringAssert.Contains( output, "result:  FAILED" );
	}

	/// <remarks><b>Mutation:</b> dropping the failed-count or the exit-code check from the test step passes this.</remarks>
	[TestMethod]
	public void AFailedTestFails()
	{
		using var scratch = new Scratch();
		var (exit, output) = scratch.Check( [], tests: "Failed!  - Failed:     1, Passed:     2, Skipped:     0, Total:     3, Duration: 1 s\n", testExit: 1 );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "tests:   2 passed, 1 failed, 0 skipped, of 3" );
	}

	/// <remarks><b>Mutation:</b> guarding the commit's diff-tree (no -m) instead of its tree lists no file for a merge, and its .wad passes.</remarks>
	[TestMethod]
	public void AMergeThatBringsInAGameFileIsRefused()
	{
		using var scratch = new Scratch();
		var main = scratch.Git( "rev-parse", "--abbrev-ref", "HEAD" ).Output.Trim();

		Assert.AreEqual( 0, scratch.Git( "checkout", "-q", "-b", "side" ).Exit );
		scratch.Commit( "a wad", "a.wad" );
		Assert.AreEqual( 0, scratch.Git( "checkout", "-q", main ).Exit );
		scratch.Commit( "a note", "note.md" );
		Assert.AreEqual( 0, scratch.Git( "merge", "-q", "--no-ff", "-m", "merge", "side" ).Exit );

		var (exit, output) = scratch.Check( [] );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "REFUSED a.wad" );
		StringAssert.Contains( output, "guard:   REFUSED" );
	}

	/// <remarks><b>Mutation:</b> the guard's step not setting <c>fail</c> on a refusal passes this.</remarks>
	[TestMethod]
	public void AnOrdinaryCommitOfAGameFileIsRefusedAndAnOlderCleanOneStillPasses()
	{
		using var scratch = new Scratch();
		scratch.Commit( "a wad", "a.wad" );

		Assert.AreEqual( 1, scratch.Check( [] ).Exit );
		Assert.AreEqual( 0, scratch.Check( ["HEAD~1"] ).Exit );
	}

	/// <remarks><b>Mutation:</b> dropping the note at the start or in the summary fails this; making it a failure fails the exit code.</remarks>
	[TestMethod]
	public void UncommittedChangesAreNamedAsNotCheckedOnlyWhenHeadIsTheDefault()
	{
		using var scratch = new Scratch();
		File.WriteAllText( Path.Join( scratch.Repo, "README.md" ), "changed\n" );

		var (exit, output) = scratch.Check( [] );

		Assert.AreEqual( 0, exit, output );
		StringAssert.Contains( output, "check_commit: note: tracked files have uncommitted changes, and they are NOT checked" );
		StringAssert.Contains( output, "note:    tracked files have uncommitted changes" );
		Assert.IsFalse( scratch.Check( ["HEAD"] ).Output.Contains( "NOT checked" ) );
	}
}
