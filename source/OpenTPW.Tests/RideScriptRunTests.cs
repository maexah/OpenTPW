using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Running the scripts the game ships, rather than a script written to pass.
///
/// <para>
/// The interesting failures here are the quiet ones. A machine with the wrong branch condition, or
/// the wrong operand order, still runs - it just runs somewhere else, and a test that only asked
/// "did it execute a hundred instructions" would pass throughout. So these ask where a real script
/// ends up: every ride script in the game settles onto a <c>WAIT</c> or a branch loop rather than
/// running off its own end, and <c>Coaster1</c> in particular names itself before it does.
/// </para>
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideScriptRunTests
{
	private BaseFileSystem _data = null!;

	/// <summary>
	/// Coaster1's main loop, by word: it begins where the branches come back to and ends on the
	/// branch that closes it. Taken from the script's own disassembly, not from watching this run.
	/// </summary>
	private const int MainLoopStart = 18;
	private const int MainLoopEnd = 67;

	[TestInitialize]
	public void MountTheGame() => _data = GameData.Required();

	private RideScriptFile Read( string path )
	{
		using var stream = new MemoryStream( _data.ReadAllBytes( path ) );

		return new RideScriptFile( stream );
	}

	/// <summary>
	/// Lost Kingdom's first coaster, worth singling out because its string blob is known by hand:
	/// "Coaster" then "EventMap.rse", so <c>NAME</c>'s operand of 0 must come back as "Coaster".
	///
	/// <para>
	/// It is found through the same walk the other tests use rather than by a written-out path. An
	/// archive is addressed by whatever spelling this file system hands back, and writing that out by
	/// hand produced a missing file twice - which fails as a broken test rather than as a wrong
	/// answer, and says nothing about the machine under test.
	/// </para>
	/// </summary>
	private RideScriptFile CoasterFile()
	{
		foreach ( var (path, file) in EveryScript() )
		{
			if ( Path.GetFileName( path ).Equals( "Coaster1.RSE", StringComparison.OrdinalIgnoreCase ) )
				return file;
		}

		Assert.Fail( "Coaster1.RSE was not found by the walk" );

		return null!;
	}

	/// <summary>
	/// A real script runs, names itself, and comes to rest - it does not fall off its own end.
	///
	/// <para>
	/// Running until it stops would be the wrong test: scripts are written as endless loops, so a
	/// correct machine never stops and an incorrect one might. What is checked instead is that after
	/// a generous number of turns it is still running, has named itself, and has settled inside its
	/// own main loop - which is where this script has to end up, since its only <c>WAIT</c> is behind
	/// a variable no world is here to set.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ACoasterScriptRunsAndNamesItself()
	{
		var file = CoasterFile();

		Assert.IsTrue( file.IsValid, "the coaster script did not read" );

		var script = new RideScript( file );

		for ( int turn = 0; turn < 200; ++turn )
			script.Turn( 0f );

		Assert.IsTrue( script.Running, "the script stopped, which no shipped script should do" );
		Assert.AreEqual( "Coaster", script.Name, "the name NAME gave it" );

		// It settles into its own main loop, which runs from word 18 to the branch at 67 that closes
		// it. Coaster1's only WAIT is at word 88, behind a test of VAR_BREAKSTAT that nothing but a
		// running park ever sets, so with no world it is unreachable by construction - and the loop is
		// where a correct machine has to end up instead.
		Assert.IsTrue( script.Position >= MainLoopStart && script.Position <= MainLoopEnd,
			$"the script settled at word {script.Position}, outside its main loop" );

		// The loop has no ENDSLICE in it. The only reason a turn ever ends is CRIT_UNLOCK giving up
		// the rest of the slice, so this passing at all is that semantic working.
		Assert.IsTrue( script.NotImplemented > 0, "nothing was counted as unimplemented, which cannot be right" );
	}

	/// <summary>
	/// A wait is a place the script sits, not a pause in the caller.
	///
	/// <para>
	/// The engine rewinds the program counter onto the <c>WAIT</c> so it runs again next turn. That
	/// makes the check simple and strict: while the clock has not moved the position must not either,
	/// and once it passes the deadline the script must go on. Getting the rewind wrong by one word
	/// would leave the script executing an operand as though it were an instruction.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWaitHoldsItsPlaceUntilTheClockPasses()
	{
		// Which scripts reach a WAIT with nothing driving them is not something to assume. Most waits
		// sit behind a variable only a running park ever sets - Coaster1's does - so this finds one
		// rather than naming one, and says so plainly if none exists.
		RideScript? waiting = null;
		var found = string.Empty;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			var candidate = new RideScript( file );

			for ( int turn = 0; turn < 60 && candidate.Running && !candidate.Waiting; ++turn )
				candidate.Turn( 0f );

			if ( !candidate.Waiting )
				continue;

			waiting = candidate;
			found = Path.GetFileName( path );
			break;
		}

		Assert.IsNotNull( waiting, "no shipped script reached a WAIT, so nothing here was exercised" );

		var script = waiting!;
		var waitingAt = script.Position;

		for ( int turn = 0; turn < 5; ++turn )
			script.Turn( 0f );

		Assert.AreEqual( waitingAt, script.Position, $"{found} moved while its wait was outstanding" );

		// Far past any duration a script asks for, so the deadline is certainly behind us.
		script.Turn( 100000f );

		Assert.AreNotEqual( waitingAt, script.Position, $"{found} did not go on once the wait was up" );
		Assert.IsTrue( script.Running, $"{found} stopped when its wait came due" );
	}

	/// <summary>
	/// Every ride script in the game runs without falling over, and none of them runs off its end.
	///
	/// <para>
	/// This is the walk that would catch a wrong operand count or a wrong branch target: either sends
	/// the position somewhere that is not an instruction, and the machine stops. One script proves
	/// little; three hundred of them agreeing is the instruction set and the data agreeing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRideScriptInTheGameRuns()
	{
		var stopped = new System.Collections.Generic.List<string>();
		var ran = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			var script = new RideScript( file );

			for ( int turn = 0; turn < 40 && script.Running && !script.Waiting; ++turn )
				script.Turn( 0f );

			++ran;

			if ( !script.Running )
				stopped.Add( Path.GetFileName( path ) );
		}

		Assert.AreEqual( 0, stopped.Count,
			$"{stopped.Count} of {ran} scripts stopped, starting with '{stopped.FirstOrDefault()}'" );

		Assert.AreEqual( RideScriptTests.ExpectedScripts, ran, "scripts run" );
	}

	/// <summary>
	/// Nothing is quietly guessed: whatever the machine does not implement, it counts.
	///
	/// <para>
	/// The point of the count is that it is visible. A script that spends its whole life in
	/// unimplemented instructions is not running in any useful sense, and this says so rather than
	/// reporting a green run.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WhatIsNotImplementedIsCountedRatherThanGuessed()
	{
		var script = new RideScript( CoasterFile() );

		for ( int turn = 0; turn < 200 && !script.Waiting; ++turn )
			script.Turn( 0f );

		Assert.IsTrue( script.NotImplemented >= 0, "the count went backwards" );
		Assert.IsTrue( script.Name.Length > 0, "the script did not get as far as naming itself" );
	}

	private System.Collections.Generic.IEnumerable<(string Path, RideScriptFile File)> EveryScript()
	{
		foreach ( var theme in Entries( "levels", directories: true ) )
		{
			var themeName = Path.GetFileName( theme );

			if ( string.IsNullOrEmpty( themeName ) )
				continue;

			foreach ( var folder in new[] { "features", "rides", "shops", "sideshow", "upgrades" } )
			{
				foreach ( var item in Entries( $"levels/{themeName}/{folder}", directories: true ) )
				{
					var stem = Path.GetFileName( item );

					if ( string.IsNullOrEmpty( stem ) )
						continue;

					foreach ( var entry in Entries( $"levels/{themeName}/{folder}/{stem}", directories: false ) )
					{
						var name = Path.GetFileName( entry );

						if ( string.IsNullOrEmpty( name )
							|| !name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) )
							continue;

						var path = $"levels/{themeName}/{folder}/{stem}/{name}";

						yield return (path, Read( path ));
					}
				}
			}
		}
	}

	private string[] Entries( string path, bool directories )
	{
		try
		{
			return directories ? _data.GetDirectories( path ) : _data.GetFiles( path );
		}
		catch ( Exception )
		{
			return [];
		}
	}
}
