using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The animation triggers read the script's speed divisor (play rate, deadline) and this game does not yet (docs/QUEUE.md Q155): a
/// trigger at a divisor other than 1 counts as <c>ANIM_TRIGGER_SPEED_RATE</c> (with a model, the rate it hands the channel) and
/// <c>ANIM_TRIGGER_SPEED_DEADLINE</c> (the <c>WAIT4ANIM</c> deadline of <c>TRIGANIM</c>, <c>TRIGWAITANIM</c> and the <c>_CH</c> one); a
/// <c>WAITANIM</c> whose length-less-300 is negative counts <c>WAITANIM_SPEED_WRAP</c>, and a positive one that the engine's divide-then-floor
/// arms for another number than <see cref="RideScript"/>'s floor-then-divide counts <c>WAITANIM_SPEED_ORDER</c>. At the word 50 none count.
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
	/// The engine floors the quotient (<c>0x00552ad6</c>); here the sum is floored and then divided. A sum of 100 at a word of 60 is 100 / 1.1 = 90,
	/// floored to 300 there and 300 / 1.1 = 272 here; 320 is 290 there, floored to 300, and 290 here; 330 is 299 over the float divisor 1.1f, floored to 300 there and 299 here; 340 is 309 and 700 is 636 in both.
	/// </summary>
	[TestMethod]
	[DataRow( 60, 100, true )]
	[DataRow( 60, 0, true )]
	[DataRow( 60, 320, true )]
	[DataRow( 60, 330, true )]
	[DataRow( 60, 340, false )]
	[DataRow( 60, 700, false )]
	[DataRow( 50, 100, false )]
	[DataRow( 20, 100, true )]
	[DataRow( 20, 200, true )]
	[DataRow( 20, 700, false )]
	[DataRow( 20, 1000, false )]
	public void TheEnginesDivideThenFloorDiffersFromFloorThenDivideBelowTheFloorsQuotient( int speed, int sum, bool differs )
		=> Assert.AreEqual( differs, RideScript.WaitAnimationOrderDiffers( sum, speed ), $"word {speed}, sum {sum}" );

	/// <summary>The same through the instruction: a model-less sum is -300 (the wrap, counted alone), and at the word 50 nothing counts.</summary>
	[TestMethod]
	public void AModelessAnimationWaitCountsTheWrapAndNotTheOrder()
	{
		Trigger( Opcode.WAITANIM, 60, 0, 0 );

		Assert.AreEqual( 1, Times( "WAITANIM_SPEED_WRAP" ) );
		Assert.AreEqual( 0, Times( "WAITANIM_SPEED_ORDER" ) );

		Unimplemented.Forget();
	}

	/// <summary>
	/// A real short clip: the Lights' role 5 is 333 ms, so <c>WAITANIM</c>'s sum is 33, which the engine divides to 30 at a word of 60 and floors to 300
	/// where this floors first and waits 272 (counted); the 1x1 east scenery's role 0 is 666 ms, a sum of 366 that both arms for 332 (not counted).
	/// </summary>
	[TestMethod]
	[DataRow( "features", "lights", 5, 60, 1 )]
	[DataRow( "features", "lights", 5, 50, 0 )]
	[DataRow( "features", "lights", 5, 20, 1 )]
	[DataRow( "features", "1x1east", 0, 60, 0 )]
	public void AShortClipsAnimationWaitCountsTheOrderAndALongOneDoesNot( string kind, string stem, int role, int speed, int counted )
	{
		FileSystem = GameData.Required();

		Unimplemented.Forget();

		var script = new RideScript( Build( 1, Word( Opcode.WAITANIM ), role, 0, Word( Opcode.END ) ) )
		{
			Speed = speed,
			Animations = RideAnimations.Load( $"levels/jungle/{kind}/{stem}", stem, FileSystem )
		};

		script.Turn( 1000f );

		Assert.AreEqual( counted, Times( "WAITANIM_SPEED_ORDER" ), $"{stem} role {role} at a word of {speed}" );
		Assert.AreEqual( 0, Times( "WAITANIM_SPEED_WRAP" ), "a clip of 300 ms or more has no wrap" );

		Unimplemented.Forget();
	}
}
