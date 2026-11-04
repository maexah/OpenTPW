using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The animation triggers read the script's speed divisor (play rate, deadline) and this game does not yet (docs/QUEUE.md Q155): a
/// trigger at a divisor other than 1 counts as <c>ANIM_TRIGGER_SPEED_RATE</c> (with a model, the rate it hands the channel) and
/// <c>ANIM_TRIGGER_SPEED_DEADLINE</c> (the <c>WAIT4ANIM</c> deadline of <c>TRIGANIM</c>, <c>TRIGWAITANIM</c> and the <c>_CH</c> one); a
/// <c>WAITANIM</c> whose length-less-300 is negative (a clip under 300 ms or no model) counts <c>WAITANIM_SPEED_WRAP</c>, the one turn it waits here where the
/// engine's unsigned sum waits days. At the word 50 none count.
/// </summary>
[TestClass]
[DoNotParallelize]
public class AnimTriggerSpeedCountTests
{
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static RideScriptFile Build( int variableCount, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 8 );
		writer.Write( 50 );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );

		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static RideScript Trigger( Opcode opcode, int speed, params int[] operands )
	{
		Unimplemented.Forget();

		var script = new RideScript( Build( 1, [Word( opcode ), .. operands, Word( Opcode.END )] ) ) { Speed = speed };

		script.Turn( 1000f );

		return script;
	}

	/// <summary>A model-less trigger arms a deadline, so it counts the deadline and not the rate (the rate goes only to a channel).</summary>
	[TestMethod]
	[DataRow( 50, 0 )]
	[DataRow( 60, 1 )]
	[DataRow( 0, 1 )]
	[DataRow( 80, 1 )]
	public void ATriggerAtADivisorOtherThanOneCountsItsDeadlineAlone( int speed, int counted )
	{
		Trigger( Opcode.TRIGANIM, speed, 5, 0, 0 );

		Assert.AreEqual( counted, Times( "ANIM_TRIGGER_SPEED_DEADLINE" ), $"a word of {speed}" );
		Assert.AreEqual( 0, Times( "ANIM_TRIGGER_SPEED_RATE" ), "no model, no channel to hand a rate to" );

		Unimplemented.Forget();
	}

	/// <summary>The _CH trigger arms the same deadline.</summary>
	[TestMethod]
	public void ATriggerOnAChannelCountsItsDeadlineToo()
	{
		Trigger( Opcode.TRIGANIM_CH, 60, 5, 0, 0, 0 );
		Assert.AreEqual( 1, Times( "ANIM_TRIGGER_SPEED_DEADLINE" ) );

		Trigger( Opcode.TRIGANIM_CH, 50, 5, 0, 0, 0 );
		Assert.AreEqual( 0, Times( "ANIM_TRIGGER_SPEED_DEADLINE" ), "and not at the word 50" );

		Unimplemented.Forget();
	}

	/// <summary>A <c>LOOPANIM</c> arms no deadline and has no model to hand a rate to here: nothing is counted.</summary>
	[TestMethod]
	public void ALoopWithNoModelCountsNothing()
	{
		Trigger( Opcode.LOOPANIM, 60, 5, 0 );

		Assert.AreEqual( 0, Times( "ANIM_TRIGGER_SPEED_DEADLINE" ) );
		Assert.AreEqual( 0, Times( "ANIM_TRIGGER_SPEED_RATE" ) );

		Unimplemented.Forget();
	}

	/// <summary>With no model <c>WAITANIM</c>'s sum is -300, which the engine divides as a qword: counted at any word but 50.</summary>
	[TestMethod]
	[DataRow( 50, 0 )]
	[DataRow( 60, 1 )]
	[DataRow( 20, 1 )]
	public void AnAnimationWaitWithANegativeSumCountsTheWrapOffTheDivisor( int speed, int counted )
	{
		Trigger( Opcode.WAITANIM, speed, 0, 0 );

		Assert.AreEqual( counted, Times( "WAITANIM_SPEED_WRAP" ), $"a word of {speed}" );

		Unimplemented.Forget();
	}

	/// <summary>
	/// With a model, the rate is counted at every trigger of a script at a divisor other than 1, a loop included, once for each start: the
	/// Gates' <c>TRIGANIM</c>, <c>LOOPANIM</c> and <c>WAITANIM</c> are three starts and 60 counts three, 50 counts none.
	/// </summary>
	[TestMethod]
	[DataRow( 50, 0 )]
	[DataRow( 60, 3 )]
	public void WithAModelEveryStartCountsItsRate( int speed, int counted )
	{
		FileSystem = GameData.Required();

		Unimplemented.Forget();

		var script = new RideScript( Build( 1,
			Word( Opcode.TRIGANIM ), 0, 0, 0,
			Word( Opcode.LOOPANIM ), 0, 0,
			Word( Opcode.WAITANIM ), 0, 0,
			Word( Opcode.END ) ) )
		{
			Speed = speed,
			Animations = RideAnimations.Load( "levels/jungle/features/gates", "gates", FileSystem )
		};

		for ( var tick = 0; tick < 5; ++tick )
			script.Turn( 1000f + tick * 31 );

		Assert.AreEqual( counted, Times( "ANIM_TRIGGER_SPEED_RATE" ), $"a word of {speed}" );

		Unimplemented.Forget();
	}

	/// <summary>
	/// <c>WAITANIM</c> divides the sum and then floors the quotient (<c>0x00552acd</c>, <c>0x00552ad6</c>), by the clip's length: at a word of 60 the divisor is 1.1,
	/// so 400 is 100 / 1.1 = 90, floored to 300 (floor first would wait 272); 630 is 330 / 1.1f = 299.99999, cut to 299 and floored to 300 (floor-first,
	/// 299 either way); 1000 is 636 and 5000 is 4272 (the Belly Bounce's two clips). At a word of 40 the divisor is 0.9: 400 is 111, floored to 300
	/// (floor first, 333); at a word of 20 (divisor 0.7) a 550 ms clip is 250 / 0.7 = 357, above the floor, where floor-first waits 428. A negative sum is its own answer: -300 at any word (the deviation at all but 50).
	/// </summary>
	/// <remarks><b>Mutation:</b> the floor before the divide (the order Q875 had) gives 272, 299 and 333 for the first, second and fifth rows.</remarks>
	[TestMethod]
	[DataRow( 60, 400, 300 )]
	[DataRow( 60, 630, 300 )]
	[DataRow( 60, 631, 300 )]
	[DataRow( 60, 1000, 636 )]
	[DataRow( 60, 5000, 4272 )]
	[DataRow( 40, 400, 300 )]
	[DataRow( 50, 400, 300 )]
	[DataRow( 50, 1000, 700 )]
	[DataRow( 20, 550, 357 )]
	[DataRow( 50, 0, -300 )]
	[DataRow( 60, 0, -300 )]
	[DataRow( 40, 0, -300 )]
	[DataRow( 60, 100, -200 )]
	public void AnAnimationWaitDividesTheSumAndThenFloorsIt( int speed, int length, int waits )
		=> Assert.AreEqual( waits, RideScript.AnimationWait( length - 300, speed ), $"word {speed}, clip {length} ms" );

	/// <summary>The same through the instruction: a model-less sum is -300, which is the wrap counted alone, and costs the one turn; at the word 50 nothing counts.</summary>
	[TestMethod]
	public void AModelessAnimationWaitCountsTheWrapAndWaitsOneTurn()
	{
		Trigger( Opcode.WAITANIM, 60, 0, 0 );

		Assert.AreEqual( 1, Times( "WAITANIM_SPEED_WRAP" ) );

		var script = new RideScript( Build( 1, Word( Opcode.WAITANIM ), 0, 0, Word( Opcode.END ) ) ) { Speed = 60 };

		script.Turn( 1000f );
		Assert.AreEqual( -300, script.LastWait );
		Assert.IsTrue( script.Waiting, "the instruction gives up the turn" );
		script.Turn( 1031f );
		Assert.IsFalse( script.Waiting, "one turn, not the days the engine's unsigned sum waits" );

		Unimplemented.Forget();
	}

	/// <summary>
	/// A real short clip: the Lights' role 5 is 333 ms, so <c>WAITANIM</c>'s sum is 33, which divides to 30 and is floored to 300 at any word
	/// (floor first, 272 at 60); the 1x1 east scenery's role 0 is 666 ms, a sum of 366 that waits 332 at 60 and 366 at 50. Nothing counts: no negative sum.
	/// </summary>
	[TestMethod]
	[DataRow( "features", "lights", 5, 60, 300 )]
	[DataRow( "features", "lights", 5, 50, 300 )]
	[DataRow( "features", "lights", 5, 20, 300 )]
	[DataRow( "features", "1x1east", 0, 60, 332 )]
	[DataRow( "features", "1x1east", 0, 50, 366 )]
	public void AShortClipsAnimationWaitIsFlooredAfterTheDivide( string kind, string stem, int role, int speed, int waits )
	{
		FileSystem = GameData.Required();

		Unimplemented.Forget();

		var script = new RideScript( Build( 1, Word( Opcode.WAITANIM ), role, 0, Word( Opcode.END ) ) )
		{
			Speed = speed,
			Animations = RideAnimations.Load( $"levels/jungle/{kind}/{stem}", stem, FileSystem )
		};

		script.Turn( 1000f );

		Assert.AreEqual( waits, script.LastWait, $"{stem} role {role} at a word of {speed}" );
		Assert.AreEqual( 0, Times( "WAITANIM_SPEED_WRAP" ), "a clip of 300 ms or more has no wrap" );

		Unimplemented.Forget();
	}

	/// <summary>
	/// The scheduler's visit and <c>COAST</c>'s initialise hand a script's speed word to a ride (<c>0x005517e1</c>, <c>0x00554aab</c>) and, after a turn of a
	/// script with a model, to its channels (<c>0x00551789</c>); none is built, so each counts at a divisor other than 1 and not at 50.
	/// </summary>
	[TestMethod]
	[DataRow( 50, 0, 0 )]
	[DataRow( 60, 2, 1 )]
	public void TheSchedulersAndCoastsReadsOfTheSpeedWordCountAtADivisorOtherThanOne( int speed, int coast, int channel )
	{
		FileSystem = GameData.Required();

		Unimplemented.Forget();

		var script = new RideScript( Build( 1, Word( Opcode.COAST ), 8, 0, Word( Opcode.WAIT ), 1000, Word( Opcode.END ) ) )
		{
			Speed = speed,
			Ride = new RideState(),
			Animations = RideAnimations.Load( "levels/jungle/features/gates", "gates", FileSystem )
		};

		var scheduler = new RideScriptScheduler();

		scheduler.Add( 1, script );
		scheduler.Advance( 1000f );

		Assert.AreEqual( coast, Times( "COAST_RIDE_SPEED" ), "the initialise and the visit" );
		Assert.AreEqual( channel, Times( "SCHEDULER_CHANNEL_SPEED" ) );

		Unimplemented.Forget();
	}
}
