using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// An exception once the main loop runs (Q79): logged before anything unwinds, said in a box that names it, what was not
/// saved and where the log is, and the run ended with exit code 1 however the audio's shutdown goes. The box itself is SDL's,
/// and the whole path is seen in the running program with the debug console's <c>throw</c>.
/// </summary>
[TestClass]
[DoNotParallelize]
public class RunFailureTests
{
	/// <summary>
	/// <b>The filter takes an exception only once the loop has begun</b>, logs it with its stack there and then, and keeps the
	/// first; before the loop it leaves the exception to the failed start's catch.
	/// </summary>
	/// <remarks><b>Mutation:</b> a filter that ignores <see cref="StartFailure.Started"/>, logs nothing, or keeps the last exception, fails a step.</remarks>
	[TestMethod]
	public void TheFilterTakesAnExceptionOnlyOnceTheLoopRunsAndKeepsTheFirst()
	{
		var error = Console.Error;
		var wasStarted = StartFailure.Started;
		var written = new System.IO.StringWriter();

		try
		{
			Console.SetError( written );
			RunFailure.ResetForTest();

			StartFailure.Started = false;
			Assert.IsFalse( RunFailure.IsRunFailure( new InvalidOperationException( "before the loop" ) ), "a failed start is not a run failure" );
			Assert.AreEqual( string.Empty, written.ToString(), "nothing logged for an exception before the loop" );
			Assert.IsNull( RunFailure.First );

			StartFailure.Started = true;
			Assert.IsTrue( RunFailure.IsRunFailure( Thrown( "the cause" ) ) );
			Assert.IsTrue( RunFailure.IsRunFailure( new InvalidOperationException( "thrown while unwinding" ) ) );

			StringAssert.Contains( written.ToString(), "Run failure: the game stops on an unhandled exception: System.InvalidOperationException: the cause" );
			StringAssert.Contains( written.ToString(), "at OpenTPW.Tests.RunFailureTests.Thrown(", "the stack is logged" );
			Assert.AreEqual( "the cause", RunFailure.First!.Message, "the first exception is the one the box names" );
		}
		finally
		{
			Console.SetError( error );
			StartFailure.Started = wasStarted;
			RunFailure.ResetForTest();
		}
	}

	/// <summary>An exception that has been thrown, so it has a stack.</summary>
	private static Exception Thrown( string message )
	{
		try
		{
			throw new InvalidOperationException( message );
		}
		catch ( InvalidOperationException e )
		{
			return e;
		}
	}

	/// <summary><b>The box says the game stopped, the cause's first line, that nothing was saved, and where the log is</b>; a type initialiser's wrapper is looked through.</summary>
	/// <remarks><b>Mutation:</b> the failed start's head, the save line left out, the wrapper's own message, or no log line, fails a step.</remarks>
	[TestMethod]
	public void TheBoxNamesTheCauseWhatWasNotSavedAndTheLog()
	{
		var lines = RunFailure.Compose( new TypeInitializationException( "OpenTPW.Material", new InvalidOperationException( "a frame went wrong\nsecond line" ) ), "/logs/console.log" ).Split( '\n' );

		CollectionAssert.AreEqual( new[] { RunFailure.Head, string.Empty, "A frame went wrong", RunFailure.NothingSaved, string.Empty, "Log: /logs/console.log" }, lines );
		Assert.AreNotEqual( StartFailure.Head, lines[0] );

		StringAssert.Contains( RunFailure.Compose( new InvalidOperationException( "" ), "/l" ), "InvalidOperationException", "an empty message gives the type" );
	}

	/// <summary><b>The box kept off still logs its message, under the run failure's label</b>, so a harness can read it.</summary>
	/// <remarks><b>Mutation:</b> the label not passed through (the line says "Start failure"), fails.</remarks>
	[TestMethod]
	public void ASuppressedBoxLogsUnderTheRunFailuresLabel()
	{
		var before = Environment.GetEnvironmentVariable( StartFailure.SuppressVariable );
		var lines = new List<string>();

		try
		{
			Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, "1" );
			Assert.IsFalse( StartFailure.Show( "head\nreason", _ => null, lines.Add, lines.Add, RunFailure.Label ) );
		}
		finally { Environment.SetEnvironmentVariable( StartFailure.SuppressVariable, before ); }

		Assert.AreEqual( $"Run failure: no message box, {StartFailure.SuppressVariable} is set: head | reason", lines.Single() );
	}

	/// <summary><b>The run ends with exit code 1 when the audio closes, and when closing it throws too</b> (logged, not let out of Main).</summary>
	/// <remarks><b>Mutation:</b> no exit code set, or the shutdown's exception let through, fails a step.</remarks>
	[TestMethod]
	public void TheRunEndsWithExitCodeOneEvenWhenTheShutdownThrows()
	{
		var error = Console.Error;
		var exitCode = Environment.ExitCode;
		var written = new System.IO.StringWriter();
		var closed = 0;

		try
		{
			Console.SetError( written );

			Environment.ExitCode = 0;
			RunFailure.End( () => ++closed );
			Assert.AreEqual( 1, Environment.ExitCode );
			Assert.AreEqual( 1, closed, "the audio is closed once" );

			Environment.ExitCode = 0;
			RunFailure.End( () => throw new InvalidOperationException( "the device is gone" ) );
			Assert.AreEqual( 1, Environment.ExitCode );
			StringAssert.Contains( written.ToString(), "Run failure: the audio device could not be closed (InvalidOperationException: the device is gone)" );
		}
		finally
		{
			Console.SetError( error );
			Environment.ExitCode = exitCode;
		}
	}
}
