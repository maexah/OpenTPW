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

	/// <summary>
	/// How many shipped scripts declare limbo slots: 24, every one of them ten. They are exactly the
	/// scripts that use a limbo instruction - shops, toilets and arcades - with none on either side of that line,
	/// so the count is pinned rather than left to chance.
	/// </summary>
	private const int ExpectedLimboScripts = 24;

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
	/// hand can name a file that is not there - which fails as a broken test rather than as a wrong
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

		// The loop has no ENDSLICE in it. What ends each turn is CRIT_UNLOCK at word 62 giving up the
		// rest of the slice; without that the fifty-instruction budget would end it a few laps later.
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

			for ( int turn = 0; turn < 60 && candidate.Running && waiting == null; ++turn )
			{
				candidate.Turn( 0f );

				if ( !candidate.Waiting )
					continue;

				// Not every wait holds. WAITANIM shares WAIT's deadline field (+0xa0) but, with no model
				// to ask for a length, sets it 300ms in the PAST: it gives up the turn it is sitting on
				// and goes straight on the next one. That is the instruction growth.RSE reaches first,
				// and it is not what this is about - so a candidate has to prove it stays put across a
				// turn before it is taken. WAITANIM's own rewind is covered in RideScriptAnimationTests.
				var held = candidate.Position;

				candidate.Turn( 0f );

				if ( candidate.Waiting && candidate.Position == held )
				{
					waiting = candidate;
					found = Path.GetFileName( path );
				}
			}

			if ( waiting != null )
				break;
		}

		Assert.IsNotNull( waiting, "no shipped script reached a wait that holds, so nothing here was exercised" );

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
	/// unimplemented instructions is not running in any useful sense. What is asserted is only that the
	/// count is not below nought and that the script names itself; the count is not checked against anything.
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

	/// <summary>
	/// A real script drives a real ride: it claims it, shuts it, sets its capacity and opens it again,
	/// in that order, without anything here telling it to.
	///
	/// <para>
	/// The capacity landing on zero is the interesting part rather than a weak assertion. Word 22 sets
	/// it from <c>VAR_CAPACITY</c>, which only a running park ever writes, so zero is the honest answer
	/// - and it is what makes <c>GETQUEUE</c> report no room further down, which is why no rider is ever
	/// admitted here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ACoasterScriptDrivesTheRideItIsGiven()
	{
		var script = new RideScript( CoasterFile() ) { Ride = new RideState() };
		var ride = script.Ride!;

		Assert.IsFalse( ride.Closed, "a ride starts open, which the script's first close depends on" );

		for ( int turn = 0; turn < 200; ++turn )
			script.Turn( 0f );

		Assert.IsTrue( script.Initialised, "the script never ran COAST_INITIALISE" );

		// Word 10 shuts it; word 25 opens it again from VAR_RIDECLOSED, which nothing has set.
		Assert.IsFalse( ride.Closed, "the script left the ride shut" );
		Assert.AreEqual( 0, ride.Capacity, "the capacity came from somewhere other than VAR_CAPACITY" );
	}

	/// <summary>
	/// A rider who has finished comes back out through <c>GETPEEP</c> and lands in the script's own
	/// variable - the one place a value crosses from the ride into the machine.
	/// </summary>
	[TestMethod]
	public void ARiderWhoHasFinishedComesBackIntoTheScript()
	{
		var script = new RideScript( CoasterFile() ) { Ride = new RideState() };

		script.Ride!.FinishRider( 7 );

		for ( int turn = 0; turn < 200; ++turn )
			script.Turn( 0f );

		// Word 35 is COAST 3 VAR_LETMEOFF: op 3 with the variable as its destination.
		Assert.AreEqual( 7, script["VAR_LETMEOFF"], "the rider never reached the script" );
	}

	/// <summary>
	/// <c>COAST 2 0</c> reads the queue into a literal, which cannot be written to - and the script then
	/// branches on the answer anyway, because the engine leaves it in the result register first.
	/// Counting the skipped write is how that shows up here.
	/// </summary>
	[TestMethod]
	public void TheQueueIsReadIntoNowhereAndTheBranchStillWorks()
	{
		var script = new RideScript( CoasterFile() ) { Ride = new RideState() };

		for ( int turn = 0; turn < 200; ++turn )
			script.Turn( 0f );

		Assert.IsTrue( script.IgnoredWrites > 0, "the dummy destination was never exercised" );
		Assert.IsTrue( script.Running, "the script stopped somewhere in its loop" );
	}

	/// <summary>
	/// Every shipped script still runs with a ride attached. The twelve that use <c>COAST</c>
	/// execute it for real, and that must not send any of them somewhere else.
	/// </summary>
	[TestMethod]
	public void EveryRideScriptStillRunsWithARideAttached()
	{
		var stopped = new System.Collections.Generic.List<string>();
		var ran = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			var script = new RideScript( file ) { Ride = new RideState() };

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
	/// Every shipped script still runs with somewhere to put its effects. 164 of the 308 use
	/// <c>ADDOBJ</c> and 107 use <c>EVENT</c> - with <c>KILLOBJ</c>, 1,406 instructions that run for real
	/// when reached - and running them must not send any script somewhere else.
	///
	/// <para>
	/// The count at the end is what stops this being a smoke test: if the operands were read in the wrong
	/// order, or the type guard were wrong, the scripts would still all run and nothing would ever be
	/// started.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRideScriptStillRunsWithEffectsAttached()
	{
		var stopped = new System.Collections.Generic.List<string>();
		var started = 0;
		var ran = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			var script = new RideScript( file ) { Ride = new RideState(), Effects = new RideEffects() };

			for ( int turn = 0; turn < 40 && script.Running && !script.Waiting; ++turn )
				script.Turn( 0f );

			++ran;
			started += script.Effects!.Started;

			if ( !script.Running )
				stopped.Add( Path.GetFileName( path ) );
		}

		Assert.AreEqual( 0, stopped.Count,
			$"{stopped.Count} of {ran} scripts stopped, starting with '{stopped.FirstOrDefault()}'" );

		Assert.AreEqual( RideScriptTests.ExpectedScripts, ran, "scripts run" );
		Assert.IsTrue( started > 0, "not one script started an effect, so nothing here was exercised" );
	}

	/// <summary>
	/// Every shipped script still runs with limbo built, and every script that declares slots
	/// still has all of them at the end - because there is nobody here to be held.
	///
	/// <para>
	/// The second half is what would catch a mistake. 24 scripts declare ten slots each and 101 limbo
	/// instructions run for real, but every <c>LIMBO</c> in the corpus
	/// sits behind a test of the variable that would name a guest, and nothing here ever sets one. So a
	/// script that came back holding somebody would mean the machine had invented them - which is
	/// precisely the quiet kind of wrong that a "did it still run" check sails past.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRideScriptStillRunsWithLimboAndNobodyIsEverHeld()
	{
		var stopped = new System.Collections.Generic.List<string>();
		var holding = new System.Collections.Generic.List<string>();
		var withSlots = 0;
		var ran = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			var script = new RideScript( file ) { Ride = new RideState(), Effects = new RideEffects() };

			for ( int turn = 0; turn < 40 && script.Running && !script.Waiting; ++turn )
				script.Turn( 0f );

			++ran;

			if ( file.LimboCapacity > 0 )
				++withSlots;

			if ( !script.Running )
				stopped.Add( Path.GetFileName( path ) );

			if ( script.InLimbo != 0 )
				holding.Add( Path.GetFileName( path ) );
		}

		Assert.AreEqual( 0, stopped.Count,
			$"{stopped.Count} of {ran} scripts stopped, starting with '{stopped.FirstOrDefault()}'" );

		Assert.AreEqual( 0, holding.Count,
			$"{holding.Count} scripts came back holding somebody nothing put there, starting with '{holding.FirstOrDefault()}'" );

		Assert.AreEqual( ExpectedLimboScripts, withSlots, "scripts declaring limbo slots" );
		Assert.AreEqual( RideScriptTests.ExpectedScripts, ran, "scripts run" );
	}

	/// <summary>
	/// Every shipped script still runs when they can all reach one another - registered together, able to
	/// spawn children, and able to find each other by name.
	///
	/// <para>
	/// This runs the corpus as a <b>system</b> rather than as 308 separate scripts,
	/// and that is the point: <c>SPAWNCHILD</c> loads a real sibling out of the same archives,
	/// <c>GETVARINPARENT</c> reads a variable the parent actually set, and <c>FINDSCRIPTRAND</c> searches
	/// names that real scripts have taken. None of those can be exercised by a script on its own.
	/// </para>
	///
	/// <para>
	/// <b>The loader has to be case-insensitive and must not add an extension.</b> Scripts ask for
	/// <c>Effects.rse</c>, <c>clock.rse</c>, <c>worn.rse</c> and <c>anims.rse</c> where the archives hold
	/// <c>effects.RSE</c>, <c>Clock.RSE</c>, <c>Worn.RSE</c> and <c>Anims.RSE</c> - so a loader matching
	/// on the name as written would find none of them, every spawn would answer nought, and this test
	/// would pass while proving nothing.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRideScriptStillRunsWhenTheyCanAllReachEachOther()
	{
		var files = new System.Collections.Generic.List<(string Path, RideScriptFile File)>();

		foreach ( var entry in EveryScript() )
		{
			if ( entry.File.IsValid )
				files.Add( entry );
		}

		var byName = new System.Collections.Generic.Dictionary<string, RideScriptFile>(
			StringComparer.OrdinalIgnoreCase );

		foreach ( var (path, file) in files )
			byName[Path.GetFileName( path )] = file;

		var scheduler = new RideScriptScheduler
		{
			Loader = name => byName.TryGetValue( name, out var file ) ? new RideScript( file ) : null
		};

		var scripts = new System.Collections.Generic.List<RideScript>();
		var id = 0;

		foreach ( var (_, file) in files )
		{
			var script = new RideScript( file ) { Ride = new RideState(), Effects = new RideEffects() };

			scheduler.Add( ++id, script );
			scripts.Add( script );
		}

		var registered = scheduler.Count;
		var stopped = new System.Collections.Generic.List<string>();

		for ( int i = 0; i < scripts.Count; ++i )
		{
			var script = scripts[i];

			for ( int turn = 0; turn < 40 && script.Running && !script.Waiting; ++turn )
				script.Turn( 0f );

			if ( !script.Running )
				stopped.Add( Path.GetFileName( files[i].Path ) );
		}

		Assert.AreEqual( 0, stopped.Count,
			$"{stopped.Count} of {scripts.Count} scripts stopped, starting with '{stopped.FirstOrDefault()}'" );

		Assert.AreEqual( RideScriptTests.ExpectedScripts, scripts.Count, "scripts run" );

		// Spawning has to have happened, or the loader silently matched nothing and the run above was the
		// same one the other corpus tests already do.
		Assert.IsTrue( scheduler.Count > registered,
			$"not one script was spawned - {scheduler.Count} registered where {registered} started" );

		Assert.IsTrue( scripts.Any( script => script.ChildId != 0 ),
			"no script came back holding a child, so nothing exercised SPAWNCHILD" );
	}

	/// <summary>
	/// Every shipped script still runs once it can see its own thing's animations, with clip lengths that
	/// are real rather than the engine's floor.
	///
	/// <para>
	/// <b>A model changes behaviour at scale.</b> Without one every <c>WAITANIM</c> in the corpus sets a
	/// deadline already in the past and costs a single turn; with one most of them wait for a real clip,
	/// some of them for twenty seconds. So scripts settle onto their waits far earlier than they do without
	/// one, and what this checks is that none of them settles anywhere it should not - a wrong role or
	/// entry index would still run, it would simply wait the wrong length or answer the wrong number.
	/// </para>
	///
	/// <para>
	/// The clip count at the end is what stops this being a smoke test: if the roles were read from the
	/// wrong folder, or the numbered walk were broken, every table would come back empty and every answer
	/// would quietly fall back to the model-less one.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRideScriptStillRunsWithItsModelAttached()
	{
		var stopped = new System.Collections.Generic.List<string>();
		var withClips = 0;
		var clips = 0;
		var ran = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			// The item's own folder, which is where its model and every one of its role files sit.
			var directory = path[..path.LastIndexOf( '/' )];
			var stem = Path.GetFileName( directory );

			var animations = RideAnimations.Load( directory, stem, _data );

			var script = new RideScript( file )
			{
				Ride = new RideState(),
				Effects = new RideEffects(),
				Animations = animations
			};

			for ( int turn = 0; turn < 40 && script.Running && !script.Waiting; ++turn )
				script.Turn( 0f );

			++ran;
			clips += animations.Loaded;

			if ( animations.Loaded > 0 )
				++withClips;

			if ( !script.Running )
				stopped.Add( Path.GetFileName( path ) );
		}

		Assert.AreEqual( 0, stopped.Count,
			$"{stopped.Count} of {ran} scripts stopped, starting with '{stopped.FirstOrDefault()}'" );

		Assert.AreEqual( RideScriptTests.ExpectedScripts, ran, "scripts run" );

		// Measured against the archives themselves: every one of the 308 scripts sits beside at least one role
		// clip, 1,085 between them. Both are pinned rather than loosely bounded, because a table read from
		// the wrong folder - or a numbered walk that stopped a file early - would still leave most of them
		// non-empty and sail straight past a "more than a hundred" guard.
		Assert.AreEqual( RideScriptTests.ExpectedScripts, withClips,
			$"only {withClips} of {ran} scripts can see any clips at all, so the roles are being read from the wrong place" );

		Assert.AreEqual( 1085, clips, "role clips the corpus can see between them" );
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
