using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The animation triggers read the script's speed divisor twice (docs/QUEUE.md Q155): as the play rate they hand the channel, which is built (Q1039:
/// <c>TheRateATriggerHandsTheChannelIsTheDivisor</c> and the tests after it), and as the divisor of the deadline (Q1060: the <c>WAIT4ANIM</c> deadline of <c>TRIGANIM</c>,
/// <c>TRIGWAITANIM</c> and the <c>_CH</c> one is the floored length divided by it, <see cref="RideScript.TriggerDeadline"/>). A
/// <c>WAITANIM</c> whose length-less-300 is negative (a clip under 300 ms or no model) counts <c>WAITANIM_SPEED_WRAP</c>, the one turn it waits here where the
/// engine's unsigned sum waits days; at the word 50 it counts nothing.
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

	/// <summary>
	/// A trigger's deadline is the floored length divided by the divisor and cut toward zero (<c>0x005529ac</c>, <c>0x00552d94</c>, <c>0x005531ee</c>): at the word 60
	/// (divisor 1.1) a length of 1000 arms 909 and one of 300 arms 272; at 40 (0.9) 1000 arms 1111; at 80 (1.3) 769; at 20 (0.7) 500 arms 714; at 0 (0.5) 1000 arms 2000;
	/// at 50 the length itself. A divisor read the wrong way round (0.5 - 0.01 x) or a deadline left whole fails every row but 50.
	/// </summary>
	[TestMethod]
	[DataRow( 60, 1000, 909 )]
	[DataRow( 60, 300, 272 )]
	[DataRow( 60, 700, 636 )]
	[DataRow( 40, 1000, 1111 )]
	[DataRow( 80, 1000, 769 )]
	[DataRow( 20, 500, 714 )]
	[DataRow( 0, 1000, 2000 )]
	[DataRow( 50, 1000, 1000 )]
	[DataRow( 50, 300, 300 )]
	public void ATriggerDeadlineIsTheLengthDividedByTheDivisorAndCut( int speed, int length, int armed )
		=> Assert.AreEqual( armed, RideScript.TriggerDeadline( length, speed ), $"word {speed}, length {length}" );

	/// <summary>
	/// The three triggers through the instruction, with no model (the length floors to 300, <c>0x00552fe5</c>'s sum): the deadline <c>WAIT4ANIM</c> reads is the clock
	/// plus the divided 300, 272 at the word 60 and 333 at 40, 300 at 50; the length stored for the script, here the result register, stays the 300.
	/// </summary>
	[TestMethod]
	[DataRow( Opcode.TRIGANIM, 60, 272 )]
	[DataRow( Opcode.TRIGANIM, 40, 333 )]
	[DataRow( Opcode.TRIGANIM, 50, 300 )]
	[DataRow( Opcode.TRIGANIM_CH, 60, 272 )]
	[DataRow( Opcode.TRIGANIM_CH, 40, 333 )]
	[DataRow( Opcode.TRIGANIM_CH, 50, 300 )]
	public void ATriggerArmsItsDeadlineDividedAndStoresTheLengthWhole( Opcode opcode, int speed, int armed )
	{
		var script = Trigger( opcode, speed, opcode == Opcode.TRIGANIM_CH ? [5, 0, 0, 0] : [5, 0, 0] );

		Assert.AreEqual( 300, script.Result, "the length stored is undivided" );
		Assert.AreEqual( armed, script.LastAnimationDeadline, $"{opcode} at a word of {speed}" );
		Assert.AreEqual( 1000f + armed, script.AnimationDeadline, $"{opcode}: the deadline WAIT4ANIM reads" );
		Assert.AreEqual( 0, Times( "ANIM_TRIGGER_SPEED_DEADLINE" ), "nothing is counted any more" );

		Unimplemented.Forget();
	}

	/// <summary>A <c>LOOPANIM</c> arms no deadline.</summary>
	[TestMethod]
	public void ALoopArmsNoDeadline()
	{
		var script = Trigger( Opcode.LOOPANIM, 60, 5, 0 );

		Assert.AreEqual( 0, script.LastAnimationDeadline );
		Assert.IsNull( script.AnimationDeadline );

		Unimplemented.Forget();
	}

	/// <summary>
	/// On the 1x1 east scenery's 666 ms role 0 (length 366) a <c>TRIGANIM</c> at the word 60 stores 366 and arms 332 (<c>366 / 1.1</c>), at 50 arms 366,
	/// and the same through <c>TRIGWAITANIM</c> (which triggers by the same call) and <c>TRIGANIM_CH</c>.
	/// </summary>
	[TestMethod]
	[DataRow( Opcode.TRIGANIM, 60, 332 )]
	[DataRow( Opcode.TRIGANIM, 50, 366 )]
	[DataRow( Opcode.TRIGWAITANIM, 60, 332 )]
	[DataRow( Opcode.TRIGANIM_CH, 60, 332 )]
	[DataRow( Opcode.TRIGANIM_CH, 20, 522 )]
	public void ATriggerOnARealClipDividesItsLength( Opcode opcode, int speed, int armed )
	{
		FileSystem = GameData.Required();

		Unimplemented.Forget();

		var body = opcode == Opcode.TRIGANIM_CH ? new[] { Word( opcode ), 0, 0, 0, 0 } : new[] { Word( opcode ), 0, 0, 0 };
		var script = new RideScript( Build( 1, [.. body, Word( Opcode.END )] ) )
		{
			Speed = speed,
			Animations = RideAnimations.Load( "levels/jungle/features/1x1east", "1x1east", FileSystem )
		};

		script.Turn( 1000f );

		Assert.AreEqual( 366, script.Result, "the length stored is undivided" );
		Assert.AreEqual( armed, script.LastAnimationDeadline, $"{opcode} at a word of {speed}" );
		Assert.AreEqual( 1000f + armed, script.AnimationDeadline );

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

	private static RideScript WithLights( int speed, int channels, int[] body )
	{
		FileSystem = GameData.Required();

		Unimplemented.Forget();

		return new RideScript( Build( 1, [.. body, Word( Opcode.END )] ) )
		{
			Speed = speed,
			Animations = RideAnimations.Load( "levels/jungle/features/lights", "lights", FileSystem, channels )
		};
	}

	/// <summary>
	/// On the Lights' 333 ms clip (role 5), every start hands the channel the divisor <c>0.5 + 0.01 x</c> the word as its play rate (<c>0x00552952</c>, <c>0x00552aa4</c>, <c>0x00552bea</c>, <c>0x00552d3c</c>, <c>0x00553190</c>,
	/// <c>0x00553471</c>): 1 at the word 50, 1.1 at the Belly Bounce's 60, 0.9 at 40, 1.3 at 80 and 0.5 at 0 - the six handlers built, the
	/// plain and the <c>_CH</c> ones (<c>TRIGWAITANIM</c> triggers through the same call). A word of 50 hands exactly 1, which a play rate stuck at 1 and one inverted (0.5 - 0.01 x) agree on, so 60 is the row that tells them apart.
	/// </summary>
	/// <remarks><b>Mutation:</b> the 1 back in <c>StartAnimation</c> fails every row but 50; the divisor inverted fails 60, 40, 80 and 0; the speed word read as the constant 50 fails every row but 50.</remarks>
	[TestMethod]
	[DataRow( Opcode.TRIGANIM, 50, 1f )]
	[DataRow( Opcode.TRIGANIM, 60, 1.1f )]
	[DataRow( Opcode.TRIGANIM, 40, 0.9f )]
	[DataRow( Opcode.TRIGANIM, 80, 1.3f )]
	[DataRow( Opcode.TRIGANIM, 0, 0.5f )]
	[DataRow( Opcode.TRIGWAITANIM, 60, 1.1f )]
	[DataRow( Opcode.TRIGWAITANIM, 50, 1f )]
	[DataRow( Opcode.LOOPANIM, 60, 1.1f )]
	[DataRow( Opcode.LOOPANIM, 40, 0.9f )]
	[DataRow( Opcode.WAITANIM, 60, 1.1f )]
	[DataRow( Opcode.WAITANIM, 80, 1.3f )]
	[DataRow( Opcode.WAITANIM, 50, 1f )]
	public void TheRateATriggerHandsTheChannelIsTheDivisor( Opcode opcode, int speed, float rate )
	{
		var script = WithLights( speed, 1, opcode is Opcode.TRIGANIM or Opcode.TRIGWAITANIM ? [Word( opcode ), 5, 0, 0] : [Word( opcode ), 5, 0] );

		script.Turn( 1000f );

		Assert.AreEqual( rate, script.Animations!.Channel( 0 )!.Speed, 1e-6f, $"{opcode} at a word of {speed}" );

		Unimplemented.Forget();
	}

	/// <summary>The <c>_CH</c> triggers hand the same rate to the channel they name, each on its own, and leave the first channel as it was.</summary>
	[TestMethod]
	[DataRow( Opcode.TRIGANIM_CH, 60, 1.1f )]
	[DataRow( Opcode.TRIGANIM_CH, 40, 0.9f )]
	[DataRow( Opcode.LOOPANIM_CH, 60, 1.1f )]
	[DataRow( Opcode.LOOPANIM_CH, 40, 0.9f )]
	public void ATriggerOnAChannelHandsThatChannelTheRate( Opcode opcode, int speed, float rate )
	{
		var script = WithLights( speed, 2, opcode == Opcode.TRIGANIM_CH ? [Word( opcode ), 5, 0, 0, 1] : [Word( opcode ), 5, 0, 1] );

		script.Turn( 1000f );

		Assert.AreEqual( rate, script.Animations!.Channel( 1 )!.Speed, 1e-6f, $"{opcode}: the channel named" );
		Assert.IsTrue( script.Animations.Channel( 0 )!.IsIdle, "channel nought was not started" );

		Unimplemented.Forget();
	}

	/// <summary>A loop that runs out starts again at the rate it was started with, not at 1 (the restart passes the channel's own speed).</summary>
	[TestMethod]
	public void ALoopRestartsAtTheRateItWasStartedWith()
	{
		var script = WithLights( 60, 1, [Word( Opcode.LOOPANIM ), 5, 0] );

		script.Turn( 1000f );
		script.Animations!.Advance( 1500 );

		var channel = script.Animations.Channel( 0 )!;

		Assert.IsTrue( (channel.Flags & AnimTimeControl.LoopFlag) != 0, "still looping" );
		Assert.AreEqual( 1.1f, channel.Speed, 1e-6f );
		Assert.IsTrue( channel.AnimFrame < channel.TotalAnimFrames, "restarted, so mid-clip and not past its end" );

		Unimplemented.Forget();
	}

	/// <summary>
	/// A trigger that finds the channel busy queues the clip at the rate it was triggered with (<c>FUN_004732a0</c>'s queue arm, <c>+0x30</c>), which the promotion then starts it at:
	/// the first script at the word 50 plays at 1, a second at 60 queues 1.1 behind it.
	/// </summary>
	[TestMethod]
	public void AQueuedTriggerKeepsTheRateItWasMadeAt()
	{
		var first = WithLights( 50, 1, [Word( Opcode.TRIGANIM ), 5, 0, 0] );
		var second = new RideScript( Build( 1, Word( Opcode.TRIGANIM ), 5, 0, 0, Word( Opcode.END ) ) ) { Speed = 60, Animations = first.Animations };

		first.Turn( 1000f );
		second.Turn( 1000f );

		var channel = first.Animations!.Channel( 0 )!;

		Assert.AreEqual( 1f, channel.Speed, 1e-6f, "the clip playing" );
		Assert.IsTrue( channel.HasQueued, "the second waits its turn" );
		Assert.AreEqual( 1.1f, channel.DeferredSpeed, 1e-6f, "at the rate it was triggered with" );

		// The first clip (333 ms at 1) has run out by 1400 ms: the promotion starts the queued one at 1.1 (0x004738a3).
		first.Animations.Advance( 1400 );

		Assert.IsFalse( channel.HasQueued, "promoted" );
		Assert.AreEqual( 1.1f, channel.Speed, 1e-6f, "and playing at the rate it was queued with" );

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
