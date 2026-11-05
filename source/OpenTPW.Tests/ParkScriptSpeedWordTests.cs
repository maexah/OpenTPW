using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The speed word a script keeps (<see cref="RideScript.Speed"/>, <c>+0xc0</c>, Q155) and what reads it: the divisor <c>WAIT</c>
/// divides by, the scheduler's pushes to a bumper ride and to a <c>SPAWNCHILD</c> child, and the scream parameter
/// (<c>docs/exe/park.md</c>, "What a loaded script holds").
///
/// <para>
/// Every value here is one a wrong rule gets wrong: a word of 50 makes the divisor exactly 1 and the average with a level 25 above the
/// level, so 50 alone hides a missing read.
/// </para>
/// </summary>
[TestClass]
public class ParkScriptSpeedWordTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static RideScriptFile Build( int timeSlice, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 0 );          // variable count
		writer.Write( 8 );          // stack size
		writer.Write( timeSlice );
		writer.Write( 0 );          // limbo records
		writer.Write( 0 );          // bounce records
		writer.Write( 0 );          // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );          // string blob length
		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	private static RideScript Idle()
		=> new( Build( 1, [.. Enumerable.Repeat( Word( Opcode.NOP ), 200 )] ) );

	/// <summary>A script nothing has written holds the script creator's 50 (<c>0x00558c3c</c>), and the divisor at 50 is exactly one.</summary>
	[TestMethod]
	public void ANewScriptHoldsFiftyAndItsDivisorIsExactlyOne()
	{
		Assert.AreEqual( 50, Idle().Speed );
		Assert.AreEqual( 1f, RideScript.DivisorOf( 50 ) );
		Assert.AreEqual( 1.1f, RideScript.DivisorOf( 60 ) );
		Assert.AreEqual( 0.5f, RideScript.DivisorOf( 0 ) );
	}

	/// <summary>The field is a signed 16-bit word (<c>movsx</c> at <c>0x00551cc3</c>), so a value written past it wraps as the store does.</summary>
	[TestMethod]
	public void TheWordIsSixteenBitsSigned()
	{
		var script = Idle();

		script.Speed = 65536 + 60;
		Assert.AreEqual( 60, script.Speed );

		script.Speed = 40000;
		Assert.AreEqual( -25536, script.Speed );
	}

	/// <summary>
	/// <c>WAIT</c> adds <c>duration / (0.5 + 0.01 * speed)</c>, the quotient cut to a whole number (<c>0x005537fd</c>, <c>0x00553801</c>),
	/// to the clock: a word of 60 turns 3000 into 2727, 80 into 2307, 20 into 4285 and 0 into 6000.
	/// </summary>
	[TestMethod]
	[DataRow( 50, 3000 )]
	[DataRow( 60, 2727 )]
	[DataRow( 80, 2307 )]
	[DataRow( 20, 4285 )]
	[DataRow( 0, 6000 )]
	public void AWaitIsDividedByTheWordsDivisorAndCutToAWholeNumber( int speed, int waits )
	{
		var script = new RideScript( Build( 50, Word( Opcode.WAIT ), 3000, Word( Opcode.END ) ) ) { Speed = speed };

		script.Turn( 1000f );

		Assert.AreEqual( 1000f + waits, script.WaitDeadline, $"a word of {speed}" );

		script.Turn( 1000f + waits - 1 );
		Assert.IsTrue( script.Waiting, "one short of it" );

		script.Turn( 1000f + waits );
		Assert.IsFalse( script.Waiting, "and on it the wait is over" );
	}

	/// <summary>The word is read when the WAIT is armed and not again: the deadline a word changed under it keeps (<c>+0xa0</c> is a clock reading).</summary>
	[TestMethod]
	public void AWaitArmedKeepsItsDeadlineWhenTheWordChanges()
	{
		var script = new RideScript( Build( 50, Word( Opcode.WAIT ), 3000, Word( Opcode.END ) ) ) { Speed = 60 };

		script.Turn( 0f );
		script.Speed = 20;
		script.Turn( 1f );

		Assert.AreEqual( 2727f, script.WaitDeadline );
	}

	/// <summary>
	/// A due visit of a script (the first, then every eighth) pushes its word into its thing's bumper ride (<c>0x00551844</c>, <c>FUN_00545180</c>,
	/// clamped to 0-100 there) and into its <c>SPAWNCHILD</c> child (<c>0x00551888</c>), and into no other script; a visit not due jumps over
	/// both (<c>0x005516f6</c> to <c>0x0055188f</c>), so a word changed between two due visits waits for the next.
	/// </summary>
	[TestMethod]
	public void OnlyADueVisitPushesTheWordIntoTheBumperRideAndTheChild()
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( -1 );
		var ride = table.Cars.RideOf( handle )!;
		var scheduler = new RideScriptScheduler();
		var parent = Idle();
		var child = Idle();
		var other = Idle();

		scheduler.Add( 1, parent );
		scheduler.Add( 2, child );
		scheduler.Add( 3, other );
		parent.ChildId = 2;
		parent.Bumpers = table.Cars;
		parent.TrackRide = handle;
		parent.Speed = 80;

		Assert.AreEqual( 50, ride.Performance, "the template's, until a script pushes" );

		scheduler.Advance( 0f );
		Assert.AreEqual( (80, 80, 50), (ride.Performance, child.Speed, other.Speed), "tick 1: due" );

		parent.Speed = 35;

		for ( int tick = 2; tick <= 8; ++tick )
		{
			scheduler.Advance( 31f * tick );
			Assert.AreEqual( (80, 80), (ride.Performance, child.Speed), $"tick {tick}: not due, nothing pushed (0x005516f6)" );
		}

		scheduler.Advance( 31f * 9 );
		Assert.AreEqual( (35, 35, 50), (ride.Performance, child.Speed, other.Speed), "tick 9: due" );

		parent.Speed = 150;

		for ( int tick = 10; tick <= 17; ++tick )
			scheduler.Advance( 31f * tick );

		Assert.AreEqual( 100, ride.Performance, "held to 100 by the ride's own clamp" );
	}

	/// <summary>The scream parameter is the average of the level and the word, held to 0-100 (<c>0x00551241</c>), and the grid's step is their sum over 50, held at 3 (<c>FUN_00551320</c>).</summary>
	[TestMethod]
	public void TheScreamReadingsAverageTheLevelWithTheScriptsWord()
	{
		Assert.AreEqual( 35, ParkAudio.ScreamVolume( 20, 50 ), "the loader's word" );
		Assert.AreEqual( 40, ParkAudio.ScreamVolume( 20, 60 ), "the shipped Belly Bounce's" );
				Assert.AreEqual( 100, ParkAudio.ScreamVolume( 90, 120 ) );
		Assert.AreEqual( 0, ParkAudio.ScreamVolume( -90, 40 ) );

		Assert.AreEqual( 1, ParkAudio.ScreamGridIndex( 35, 50 ) );
		Assert.AreEqual( 2, ParkAudio.ScreamGridIndex( 35, 70 ) );
		Assert.AreEqual( 3, ParkAudio.ScreamGridIndex( 100, 80 ), "held at the top" );
		Assert.AreEqual( ParkAudio.SingleScreamEffectFor( 1, 35, 50 ) + 1, ParkAudio.SingleScreamEffectFor( 1, 35, 70 ), "the next step of the grid" );
	}

	/// <summary>
	/// A held scream keeps the word it was asked under, and its later children pick their variation by the average of the level with that word: the
	/// Belly Bounce's level 20 under a word of 180 is parameter 100, the top zone, where under 50 it is 35.
	/// </summary>
	[TestMethod]
	public void AChainPicksItsVariationByTheWordItWasStartedUnder()
	{
		FileSystem = GameData.Required();

		var picked = new Dictionary<int, int>();

		foreach ( var speed in new[] { 50, 180 } )
		{
			var played = new List<int>();
			var screams = new ParkScreams( effect => Kids( effect ), ( chain, variation ) =>
			{
				played.Add( variation );
				return null;
			}, () => new Vector3( 425f, 252f, 5f ), new Random( 1 ) );
			var chain = screams.Start( 13, 0x47, 1, 20, new Vector3( 425f, 252f, 5f ), now: 0f, speed: speed );

			for ( var i = 0; i < 3; ++i )
				screams.Pump( chain.NextAt + 0.001f );

			Assert.AreEqual( speed, chain.Speed );
			picked[speed] = played[1];
		}

		Assert.AreEqual( 1, picked[50], "parameter 35" );
		Assert.AreEqual( 3, picked[180], "parameter 100" );
	}

	private static IReadOnlyList<SoundCategoryFile.Variation> Kids( int effect )
	{
		var kids = new SoundCategoryFile( "global/sound", "kids" );

		Assert.IsTrue( kids.IsValid, "the global kids category should read" );

		return kids.ReadVariations()[kids.IndexOf( effect )];
	}
}
