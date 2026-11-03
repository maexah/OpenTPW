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
	public void AnExceptionsBoxHoldsItsMessageAndNotItsType()
	{
		var text = StartFailure.Compose( new UnauthorizedAccessException( "Access to the path 'C:\\save' is denied." ), "/l" );

		StringAssert.StartsWith( text, StartFailure.Head );
		StringAssert.Contains( text, "Access to the path 'C:\\save' is denied." );
		Assert.IsFalse( text.Contains( "UnauthorizedAccessException" ), text );
		StringAssert.Contains( text, "/l" );
	}

	/// <summary>A message that runs to several lines (a stack in it, a path on its own line) gives its first line only; one with no words gives the type.</summary>
	/// <remarks><b>Mutation:</b> the whole message shown, or the type shown always.</remarks>
	[TestMethod]
	public void AnExceptionsBoxHoldsTheFirstLineOfItsMessageOrElseItsType()
	{
		var multi = StartFailure.Compose( new InvalidOperationException( "first line\nsecond line" ), "/l" );

		Assert.AreEqual( "OpenTPW could not start.\n\nFirst line\n\nLog: /l", multi );
		StringAssert.Contains( StartFailure.Compose( new InvalidOperationException( "" ), "/l" ), "InvalidOperationException" );
	}

	/// <summary>A shader missing at a static initialiser is named by its file, not by the wrapper (Q957).</summary>
	/// <remarks><b>Mutation:</b> the wrapper's own message shown.</remarks>
	[TestMethod]
	public void ATypeInitialiserFailureNamesItsCause()
	{
		var text = StartFailure.Compose( new TypeInitializationException( "OpenTPW.Material", new System.IO.FileNotFoundException( "Shader ui.shader is missing" ) ), "/l" );

		StringAssert.Contains( text, "Shader ui.shader is missing" );
		Assert.IsFalse( text.Contains( "OpenTPW.Material" ), text );
	}

	/// <summary>
	/// <b>No line of the box is wider than the box's width</b> (Q912: a line SDL draws unwrapped fell off both ends of a
	/// 1024-pixel display), the words are all there in order, and a blank line stays one.
	/// </summary>
	/// <remarks><b>Mutation:</b> no wrap, a wrap that drops the word that does not fit, or one that loses the blank lines, fails a step.</remarks>
	[TestMethod]
	public void EveryLineOfTheBoxFitsItsWidthAndNoWordIsLost()
	{
		var long1 = "Shader ui.shader is missing or empty (is the content/shaders folder beside the game?)";
		var text = "OpenTPW could not start.\n\n" + long1 + "\n\nLog: /home/alex/.cache/tpw-harnesses/desk4/s4/noui/logs/console.log";
		var wrapped = StartFailure.Wrap( text );
		var lines = wrapped.Split( '\n' );

		Assert.IsTrue( lines.All( line => line.Length <= StartFailure.BoxWidth ), wrapped );
		Assert.AreEqual( "OpenTPW could not start.", lines[0] );
		Assert.AreEqual( string.Empty, lines[1] );
		Assert.AreEqual( long1, string.Join( " ", lines[2..^3] ), "the long line's words, in order" );
		Assert.AreEqual( 2, lines.Count( line => line.Length == 0 ), "the two blank lines are kept" );
		Assert.AreEqual( text.Replace( "\n", " " ).Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Length, wrapped.Replace( "\n", " " ).Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Length );
	}

	/// <summary>A path longer than the width is broken after a separator, so none of it is cut by the screen; one with no separator is cut at the width.</summary>
	/// <remarks><b>Mutation:</b> a word longer than the width left whole.</remarks>
	[TestMethod]
	public void APathLongerThanTheWidthIsBrokenAtItsSeparators()
	{
		var path = "C:\\Users\\someone\\AppData\\Local\\OpenTPW\\logs\\a-folder-with-a-long-name\\another-folder-too\\console.log";
		var lines = StartFailure.Wrap( "Log: " + path ).Split( '\n' );

		Assert.IsTrue( lines.Length > 1 && lines.All( line => line.Length <= StartFailure.BoxWidth ), string.Join( "\n", lines ) );
		Assert.AreEqual( "Log:", lines[0], "the label is not run on to a broken path" );
		Assert.AreEqual( path, string.Concat( lines[1..] ), "no character lost" );
		Assert.IsTrue( lines[1..^1].All( line => line.EndsWith( '\\' ) ), "each break is after a separator" );

		var bare = StartFailure.Wrap( new string( 'x', StartFailure.BoxWidth * 2 + 5 ) ).Split( '\n' );

		CollectionAssert.AreEqual( new[] { StartFailure.BoxWidth, StartFailure.BoxWidth, 5 }, bare.Select( line => line.Length ).ToArray() );
	}

	/// <summary>The box wraps and the log's line does not: what SDL is handed is wrapped, and what is logged for a box kept off is the whole message.</summary>
	/// <remarks><b>Mutation:</b> the message wrapped in <see cref="StartFailure.Compose(string, IEnumerable{string}, string)"/>, before <see cref="StartFailure.Show(string)"/> logs it.</remarks>
	[TestMethod]
	public void TheLogKeepsALongLineWholeWhereTheBoxWrapsIt()
	{
		var before = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );
		var lines = new List<string>();
		Logger.LogDelegate capture = ( _, text ) => lines.Add( text );
		var reason = string.Join( " ", Enumerable.Repeat( "A long reason", 12 ) );

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, "1" );
			Log = new();
			Logger.OnLog += capture;
			StartFailure.Show( StartFailure.Compose( "head", [reason], "/l" ) );
		}
		finally
		{
			Logger.OnLog -= capture;
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, before );
		}

		Assert.IsTrue( lines.Any( line => line.Contains( reason ) ), string.Join( "\n", lines ) );
	}

	/// <summary><b>What the box is handed is wrapped</b>: the text that reaches SDL has no line over the width, and its words are the message's.</summary>
	/// <remarks><b>Mutation:</b> the message handed to the box unwrapped (the failure Q912 found on screen).</remarks>
	[TestMethod]
	public void TheBoxIsHandedTheMessageWrapped()
	{
		var before = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );
		string? handed = null;
		var message = StartFailure.Compose( "head", [string.Join( " ", Enumerable.Repeat( "A long reason", 12 ) )], "/a/log/path/console.log" );

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, null );
			Assert.IsTrue( StartFailure.Show( message, text => { handed = text; return null; }, _ => { }, _ => { } ) );
		}
		finally { Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, before ); }

		Assert.IsTrue( message.Split( '\n' ).Any( line => line.Length > StartFailure.BoxWidth ), "the message has a line the box must wrap" );
		Assert.IsTrue( handed!.Split( '\n' ).All( line => line.Length <= StartFailure.BoxWidth ), handed );
		Assert.AreEqual( message.Replace( '\n', ' ' ).Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Length, handed.Replace( '\n', ' ' ).Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Length );
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

	/// <summary>
	/// <b>A box that was shown is reported as shown</b> even where the log's hook throws on the line that says so (the
	/// loading screen's hook redraws, and after a failed draw it throws: desk2, <c>ui.shader</c> removed).
	/// </summary>
	/// <remarks><b>Mutation:</b> the <c>info</c> call put back inside the <c>try</c> that wraps the box: <c>Show</c> answers false and warns "SDL could not be used".</remarks>
	[TestMethod]
	public void ABoxThatWasShownIsNotReportedAsNotShownWhenTheLogThrows()
	{
		var old = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );
		var warned = new List<string>();

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, null );

			var shown = StartFailure.Show( "m", _ => null, _ => throw new InvalidOperationException( "the hook drew" ), warned.Add );

			Assert.IsTrue( shown );
			Assert.AreEqual( 0, warned.Count, string.Join( " | ", warned ) );
		}
		finally { Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, old ); }
	}

	[TestMethod]
	public void ABoxThatSdlCouldNotShowIsReportedAsNotShown()
	{
		var old = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, null );

			var warned = new List<string>();
			Assert.IsFalse( StartFailure.Show( "m", _ => "no display", _ => { }, warned.Add ) );
			StringAssert.Contains( warned.Single(), "SDL said 'no display'" );

			warned.Clear();
			Assert.IsFalse( StartFailure.Show( "m", _ => throw new InvalidOperationException( "no sdl" ), _ => { }, warned.Add ) );
			StringAssert.Contains( warned.Single(), "SDL could not be used (InvalidOperationException: no sdl)" );
		}
		finally { Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, old ); }
	}
}
