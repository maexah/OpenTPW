using System;
using System.Collections.Generic;
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
}
