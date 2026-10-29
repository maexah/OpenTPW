using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class ConsoleLogTests
{
	private static string Scratch() => Directory.CreateTempSubdirectory( "tpw-consolelog" ).FullName;

	[TestMethod]
	public void TheFirstLaunchWritesConsoleLogWithTheHeaderThenWhatWasHeld()
	{
		var folder = Scratch();

		try
		{
			using var sink = new ConsoleLog.Sink();
			var tee = new ConsoleLog.Tee( new StringWriter(), sink );

			tee.WriteLine( "before the folder is known" );
			Assert.IsTrue( sink.Open( [folder], "HEADER" ) );
			tee.WriteLine( "after" );

			var lines = File.ReadAllLines( Path.Join( folder, ConsoleLog.FileName ) );

			CollectionAssert.AreEqual( new[] { "HEADER", "before the folder is known", "after" }, lines );
			Assert.IsFalse( File.Exists( Path.Join( folder, ConsoleLog.PreviousName ) ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void TheNextLaunchKeepsTheOldFileAsPreviousAndReplacesAnOlderOne()
	{
		var folder = Scratch();

		try
		{
			for ( var run = 1; run <= 3; run++ )
			{
				using var sink = new ConsoleLog.Sink();
				sink.Open( [folder], $"run {run}" );
				sink.Write( $"body {run}\n" );
			}

			Assert.AreEqual( "run 3\nbody 3\n", File.ReadAllText( Path.Join( folder, ConsoleLog.FileName ) ).ReplaceLineEndings( "\n" ) );
			Assert.AreEqual( "run 2\nbody 2\n", File.ReadAllText( Path.Join( folder, ConsoleLog.PreviousName ) ).ReplaceLineEndings( "\n" ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	/// <remarks><b>Mutation:</b> console.log moved over console-prev.log before the new file is opened fails the first half
	/// (the older previous log is lost, the current one is gone).</remarks>
	[TestMethod]
	public void AFileThatCannotBeOpenedLeavesBothOlderLogsAsTheyWere()
	{
		var folder = Scratch();

		try
		{
			File.WriteAllText( Path.Join( folder, ConsoleLog.FileName ), "old current" );
			File.WriteAllText( Path.Join( folder, ConsoleLog.PreviousName ), "old previous" );

			using var sink = new ConsoleLog.Sink( _ => throw new IOException( "Input/output error" ) );

			Assert.IsFalse( sink.Open( [folder], "H" ), "no folder could be written" );
			StringAssert.Contains( sink.Failures, "Input/output error" );

			Assert.AreEqual( "old current", File.ReadAllText( Path.Join( folder, ConsoleLog.FileName ) ) );
			Assert.AreEqual( "old previous", File.ReadAllText( Path.Join( folder, ConsoleLog.PreviousName ) ) );
			CollectionAssert.AreEquivalent( new[] { ConsoleLog.FileName, ConsoleLog.PreviousName },
				Directory.GetFiles( folder ).Select( Path.GetFileName ).ToArray(), "nothing else was left behind" );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void WhenTheOpenFailsInTheFirstFolderTheNextIsTriedAndTheFirstKeepsItsLogs()
	{
		var folder = Scratch();

		try
		{
			var first = Path.Join( folder, "first" );
			Directory.CreateDirectory( first );
			File.WriteAllText( Path.Join( first, ConsoleLog.FileName ), "old current" );
			File.WriteAllText( Path.Join( first, ConsoleLog.PreviousName ), "old previous" );

			using var sink = new ConsoleLog.Sink( path => path.StartsWith( first ) ? throw new IOException( "Input/output error" )
				: new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete ) );

			Assert.IsTrue( sink.Open( [first, Path.Join( folder, "second" )], "H" ) );
			Assert.AreEqual( Path.Join( folder, "second", ConsoleLog.FileName ), sink.Path );
			Assert.AreEqual( "old current", File.ReadAllText( Path.Join( first, ConsoleLog.FileName ) ) );
			Assert.AreEqual( "old previous", File.ReadAllText( Path.Join( first, ConsoleLog.PreviousName ) ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void ANormalOpenMovesTheCurrentLogToPreviousAndLeavesNoNewFile()
	{
		var folder = Scratch();

		try
		{
			File.WriteAllText( Path.Join( folder, ConsoleLog.FileName ), "old current" );
			File.WriteAllText( Path.Join( folder, ConsoleLog.PreviousName ), "old previous" );

			using var sink = new ConsoleLog.Sink();

			Assert.IsTrue( sink.Open( [folder], "HEADER" ) );
			sink.Write( "body\n" );

			Assert.AreEqual( Path.Join( folder, ConsoleLog.FileName ), sink.Path );
			Assert.AreEqual( string.Empty, sink.Failures );
			Assert.AreEqual( "HEADER\nbody\n", File.ReadAllText( Path.Join( folder, ConsoleLog.FileName ) ).ReplaceLineEndings( "\n" ) );
			Assert.AreEqual( "old current", File.ReadAllText( Path.Join( folder, ConsoleLog.PreviousName ) ) );
			Assert.IsFalse( File.Exists( Path.Join( folder, ConsoleLog.FileName + ConsoleLog.NewSuffix ) ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	/// <remarks><b>Mutation:</b> the failed move ignored (Path left as console.log) fails the path and the reason.</remarks>
	[TestMethod]
	public void AMoveThatFailsAfterTheOpenKeepsTheRunsFileUnderItsNewName()
	{
		if ( OperatingSystem.IsWindows() )
		{
			Assert.Inconclusive( "A folder where the previous log goes refuses the move differently there." );
			return;
		}

		var folder = Scratch();

		try
		{
			File.WriteAllText( Path.Join( folder, ConsoleLog.FileName ), "old current" );

			// A folder where console-prev.log should go: the move over it fails, after the new file is open.
			Directory.CreateDirectory( Path.Join( folder, ConsoleLog.PreviousName, "inside" ) );

			using var sink = new ConsoleLog.Sink();

			Assert.IsTrue( sink.Open( [folder], "H" ) );
			sink.Write( "body\n" );

			var kept = Path.Join( folder, ConsoleLog.FileName + ConsoleLog.NewSuffix );

			Assert.AreEqual( kept, sink.Path );
			StringAssert.Contains( sink.Failures, "could not be moved into place" );
			StringAssert.Contains( File.ReadAllText( kept ), "body" );
			Assert.AreEqual( "old current", File.ReadAllText( Path.Join( folder, ConsoleLog.FileName ) ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void AFolderThatCannotBeWrittenFallsThroughToTheNext()
	{
		var folder = Scratch();

		try
		{
			// A file where the first folder should be: the folder cannot be made.
			var blocked = Path.Join( folder, "blocked" );
			File.WriteAllText( blocked, "x" );

			using var sink = new ConsoleLog.Sink();

			Assert.IsTrue( sink.Open( [blocked, Path.Join( folder, "own" )], "H" ) );
			Assert.AreEqual( Path.Join( folder, "own", ConsoleLog.FileName ), sink.Path );
			StringAssert.Contains( File.ReadAllText( sink.Path ), "cannot be written" );
			StringAssert.Contains( sink.Failures, "blocked" );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void TheVariableNamesTheOnlyFolderAndOtherwiseTheGamesThenThePlayers()
	{
		CollectionAssert.AreEqual( new[] { "/run" }, ConsoleLog.Candidates( "/game", "/run", "/user" ) );
		CollectionAssert.AreEqual( new[] { "/game", "/user" }, ConsoleLog.Candidates( "/game", null, "/user" ) );
		CollectionAssert.AreEqual( new[] { "/game", "/user" }, ConsoleLog.Candidates( "/game", "  ", "/user" ) );
	}

	[TestMethod]
	public void EveryWayOfWritingReachesBothTheTerminalAndTheFile()
	{
		var folder = Scratch();

		try
		{
			var terminal = new StringWriter();
			using var sink = new ConsoleLog.Sink();
			var tee = new ConsoleLog.Tee( terminal, sink );

			sink.Open( [folder], "" );
			tee.Write( 'a' );
			tee.Write( "b" );
			tee.Write( new[] { 'c', 'd', 'e' }, 1, 2 );
			tee.Write( "f".AsSpan() );

			Assert.AreEqual( "abdef", terminal.ToString() );
			Assert.AreEqual( "abdef", File.ReadAllText( Path.Join( folder, ConsoleLog.FileName ) ).ReplaceLineEndings( "" ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void AnExistingFolderThatCannotBeWrittenFallsThroughToTheNext()
	{
		if ( OperatingSystem.IsWindows() )
		{
			Assert.Inconclusive( "Folder permissions are set with Unix modes here." );
			return;
		}

		var folder = Scratch();
		var readOnly = Path.Join( folder, "readonly" );

		try
		{
			Directory.CreateDirectory( readOnly );
			File.SetUnixFileMode( readOnly, UnixFileMode.UserRead | UnixFileMode.UserExecute );

			// A superuser writes anywhere, so the folder would not be refused.
			try { File.WriteAllText( Path.Join( readOnly, "probe" ), "x" ); Assert.Inconclusive( "Running as a user that ignores folder permissions." ); }
			catch ( UnauthorizedAccessException ) { }

			using var sink = new ConsoleLog.Sink();

			Assert.IsTrue( sink.Open( [readOnly, Path.Join( folder, "own" )], "H" ) );
			Assert.AreEqual( Path.Join( folder, "own", ConsoleLog.FileName ), sink.Path );
			StringAssert.Contains( sink.Failures, "UnauthorizedAccessException" );
		}
		finally
		{
			File.SetUnixFileMode( readOnly, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute );
			Directory.Delete( folder, true );
		}
	}

	[TestMethod]
	public void AFileThatStopsBeingWritableEndsTheFileAndNeverTheTerminal()
	{
		var terminal = new StringWriter();
		var disk = new FailingStream();
		using var sink = new ConsoleLog.Sink( _ => disk );
		var tee = new ConsoleLog.Tee( terminal, sink );

		sink.Open( ["anywhere"], "H" );
		tee.WriteLine( "kept" );
		disk.Full = true;

		// None of these may throw; the terminal gets every one, and the failure is told once.
		tee.WriteLine( "lost 1" );
		tee.Write( 'x' );
		tee.WriteLine( "lost 2" );

		var shown = terminal.ToString().ReplaceLineEndings( "\n" );

		StringAssert.Contains( shown, "kept\nlost 1" );
		StringAssert.Contains( shown, "xlost 2" );
		Assert.AreEqual( 1, System.Text.RegularExpressions.Regex.Count( shown, "the file stops here" ) );
		StringAssert.Contains( Encoding.UTF8.GetString( disk.Written ), "kept" );
		Assert.IsFalse( Encoding.UTF8.GetString( disk.Written ).Contains( "lost" ) );
	}

	[TestMethod]
	public void TheLogsUserFolderIsTheDataFolderTheRuntimeNamesForThisSystem()
		=> Assert.AreEqual( UserFolders.Data(), ConsoleLog.UserFolder() );

	/// <summary>A file that takes writes until <see cref="Full"/>, then fails as a full disk does.</summary>
	private sealed class FailingStream : MemoryStream
	{
		public bool Full;

		public byte[] Written => ToArray();

		public override void Write( byte[] buffer, int offset, int count )
		{
			if ( Full )
				throw new IOException( "No space left on device" );

			base.Write( buffer, offset, count );
		}

		public override void Write( ReadOnlySpan<byte> buffer )
		{
			if ( Full )
				throw new IOException( "No space left on device" );

			base.Write( buffer );
		}
	}
}
