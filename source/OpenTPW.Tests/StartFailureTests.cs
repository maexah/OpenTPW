using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What the message box of a failed start says (Q978): the log's reason lines, cleaned for a reader, and where the log is.
/// The box itself is shown by SDL and is looked at in the running program (FailedStartLogTests starts it with the box off).
/// </summary>
[TestClass]
public class StartFailureTests
{
	[TestMethod]
	public void TheMessageHoldsTheHeadTheReasonsAndTheLogsPlace()
	{
		var text = StartFailure.Compose( "OpenTPW could not start.", ["Game files: there is no folder at /x, given by --game."], "/logs/console.log" );

		Assert.AreEqual( "OpenTPW could not start.\n\nThere is no folder at /x, given by --game.\n\nLog: /logs/console.log", text );
	}

	[TestMethod]
	public void EveryReasonLineIsKeptInOrderWithItsPrefixCutAndItsFirstLetterRaised()
	{
		var text = StartFailure.Compose( "head", ["Game files: Theme Park World was not found.", "Game files: a folder counts when it holds a data folder.", "Game files: looked in /a (the working directory)"], "/l" );
		var lines = text.Split( '\n' );

		CollectionAssert.AreEqual( new[] { "Theme Park World was not found.", "A folder counts when it holds a data folder.", "Looked in /a (the working directory)" }, lines[2..5] );
		Assert.IsFalse( text.Contains( "Game files:" ) );
	}

	[TestMethod]
	public void WithNoLogTheMessageSaysNone()
	{
		var text = StartFailure.Compose( "head", ["Game files: x."], string.Empty );

		StringAssert.EndsWith( text, "Log: none" );
	}

	[TestMethod]
	public void AnExceptionNamesItsTypeAndMessage()
	{
		var text = StartFailure.Compose( new UnauthorizedAccessException( "Access to the path 'C:\\save' is denied." ), "/l" );

		StringAssert.StartsWith( text, StartFailure.Head );
		StringAssert.Contains( text, "UnauthorizedAccessException: Access to the path 'C:\\save' is denied." );
		StringAssert.Contains( text, "/l" );
	}

	[TestMethod]
	public void FindKeepsTheLinesItLoggedAndASecondFindStartsOver()
	{
		var folder = System.IO.Directory.CreateTempSubdirectory( "tpw-reasons" ).FullName;

		try
		{
			Assert.IsFalse( GameDir.Find( ["--game", folder] ) );

			Assert.AreEqual( 2, GameDir.Reasons.Count );
			StringAssert.Contains( GameDir.Reasons[0], "does not hold Theme Park World" );
			StringAssert.Contains( GameDir.Reasons[1], "a folder counts when" );

			Assert.IsFalse( GameDir.Find( ["--game", System.IO.Path.Join( folder, "none" )] ) );
			Assert.AreEqual( 1, GameDir.Reasons.Count, "the last call's lines only" );
		}
		finally { System.IO.Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void TheVariableKeepsTheBoxOffAndSaysSo()
	{
		var before = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, "1" );
			Assert.IsFalse( StartFailure.Show( "text" ) );
		}
		finally { Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, before ); }
	}

	/// <summary>
	/// <b>The filter logs the exception with its stack before anything unwinds, keeps the first one, and lets a failed start
	/// through only until the main loop begins.</b>
	/// </summary>
	/// <remarks><b>Mutation:</b> a filter that logs nothing, one that keeps the last exception, or one that ignores
	/// <see cref="StartFailure.Started"/>, fails a step.</remarks>
	[TestMethod]
	public void TheFilterLogsTheFirstExceptionAndOnlyBeforeTheLoop()
	{
		var error = Console.Error;
		var wasStarted = StartFailure.Started;
		var written = new System.IO.StringWriter();

		try
		{
			Console.SetError( written );
			StartFailure.ResetForTest();
			StartFailure.Started = false;

			Assert.IsTrue( StartFailure.IsFailedStart( new InvalidOperationException( "the cause" ) ) );
			Assert.IsTrue( StartFailure.IsFailedStart( new InvalidOperationException( "thrown while unwinding" ) ) );

			StringAssert.Contains( written.ToString(), "Unhandled exception: System.InvalidOperationException: the cause" );
			Assert.AreEqual( "the cause", StartFailure.First!.Message, "the first exception is the one the box names" );

			StartFailure.Started = true;
			written.GetStringBuilder().Clear();
			Assert.IsFalse( StartFailure.IsFailedStart( new InvalidOperationException( "in the loop" ) ) );
			Assert.AreEqual( string.Empty, written.ToString(), "nothing logged for an exception once the loop runs" );
		}
		finally
		{
			Console.SetError( error );
			StartFailure.Started = wasStarted;
			StartFailure.ResetForTest();
		}
	}

	/// <summary><b>The box kept off still logs what it would have said</b>, so a test or a harness can read it.</summary>
	[TestMethod]
	public void ASuppressedBoxLogsItsMessageOnOneLine()
	{
		var before = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );
		var lines = new List<string>();
		Logger.LogDelegate capture = ( _, text ) => lines.Add( text );

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, "1" );
			Log = new();
			Logger.OnLog += capture;
			StartFailure.Show( "head\n\nreason\n\nLog: /l" );
		}
		finally
		{
			Logger.OnLog -= capture;
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, before );
		}

		Assert.IsTrue( lines.Any( line => line.Contains( "head |  | reason |  | Log: /l" ) ), string.Join( "\n", lines ) );
	}
}
