using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// How loud a ride screams and which sample it screams with - the arithmetic of
/// <c>STARTSCREAM</c> (<c>FUN_00551130</c>), which is the half that needs no audio device.
///
/// <para>
/// <b>Lost Kingdom's Belly Bounce reaches it</b>: its script stops and restarts its scream with
/// <c>STOPSCREAM</c> then <c>STARTSCREAM</c> as its riders change, and each has its own case in the VM.
/// </para>
/// <para>
/// <b>The choosing needs no audio device.</b> The band and the volume are pure and are pinned here;
/// <see cref="ParkAudio.Scream"/> itself is driven with a device stood in by
/// <see cref="ParkScreamChainTests"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkScreamTests
{
	/// <summary>
	/// The four held scream effects and their bands, read off the engine's own comparisons: nought screams
	/// not at all, then 1, 2-3, 4-7, and 8 upwards.
	/// </summary>
	[TestMethod]
	public void TheBandChoosesWhichScreamIsPlayed()
	{
		Assert.AreEqual( 0, ParkAudio.ScreamEffectFor( 0 ), "band nought screams not at all" );
		Assert.AreEqual( 0, ParkAudio.ScreamEffectFor( -1 ), "nor does a negative one" );

		Assert.AreEqual( 0x47, ParkAudio.ScreamEffectFor( 1 ) );

		Assert.AreEqual( 0x48, ParkAudio.ScreamEffectFor( 2 ), "2 and 3 share a sample" );
		Assert.AreEqual( 0x48, ParkAudio.ScreamEffectFor( 3 ) );

		Assert.AreEqual( 0x49, ParkAudio.ScreamEffectFor( 4 ), "4 through 7 share the next" );
		Assert.AreEqual( 0x49, ParkAudio.ScreamEffectFor( 7 ) );

		// The boundaries are the point: 3/4 and 7/8 are where the engine's < 4 and < 8 fall.
		Assert.AreEqual( 0x4a, ParkAudio.ScreamEffectFor( 8 ), "and 8 upwards the last" );
		Assert.AreEqual( 0x4a, ParkAudio.ScreamEffectFor( 900 ) );
	}

	/// <summary>
	/// <b>The volume is averaged with the script's SPEED, which is the surprise in this decode.</b> The
	/// engine computes <c>(operand + speed) / 2</c> and holds it to nought through a hundred, where the
	/// speed is the word at <c>+0xc0</c> its loader sets to 50 and no opcode ever writes.
	/// </summary>
	[TestMethod]
	public void TheVolumeIsTheLevelAveragedWithTheScriptSpeed()
	{
		Assert.AreEqual( 50, ParkAudio.ScriptSpeed, "the speed the loader sets, and nothing changes" );

		// Bouncy's own instruction is STARTSCREAM VAR_TEMP, 20 - so this is the real park's number.
		Assert.AreEqual( 35, ParkAudio.ScreamVolume( 20 ), "(20 + 50) / 2, which is what the Belly Bounce asks for" );

		Assert.AreEqual( 50, ParkAudio.ScreamVolume( 50 ), "a level equal to the speed leaves it alone" );
		Assert.AreEqual( 100, ParkAudio.ScreamVolume( 150 ), "and it is held at a hundred" );
		Assert.AreEqual( 25, ParkAudio.ScreamVolume( 0 ), "a level of nought is still half the speed" );
	}

	/// <summary>
	/// The clamp's far end, which only a negative level can reach - and a script may pass one, since
	/// <c>Value</c> sign-extends a literal.
	/// </summary>
	[TestMethod]
	public void AVolumeIsNeverBelowNought()
	{
		Assert.AreEqual( 0, ParkAudio.ScreamVolume( -50 ), "exactly cancelling the speed" );
		Assert.AreEqual( 0, ParkAudio.ScreamVolume( -1000 ), "and anything past it is still nought" );
	}

	/// <summary>
	/// <c>SINGLESCREAM</c>'s negative branch (<c>FUN_00551560</c>), which picks on the band alone.
	/// <b>The gap at <c>0x6b</c> is the original's own</b>, and the shipped category agrees with it:
	/// <c>global/sound/kids</c> declares 105, 106, 108 and 109 with 107 absent.
	/// </summary>
	[TestMethod]
	public void ANegativeLevelPicksAOneShotOnTheBandAlone()
	{
		Assert.AreEqual( 0, ParkAudio.SingleScreamEffectFor( 0, -1 ), "band nought screams not at all" );

		Assert.AreEqual( 0x69, ParkAudio.SingleScreamEffectFor( 1, -1 ) );
		Assert.AreEqual( 0x6a, ParkAudio.SingleScreamEffectFor( 2, -1 ), "2 and 3 share one" );
		Assert.AreEqual( 0x6a, ParkAudio.SingleScreamEffectFor( 3, -1 ) );
		Assert.AreEqual( 0x6c, ParkAudio.SingleScreamEffectFor( 4, -1 ), "and 4 through 7 the next" );
		Assert.AreEqual( 0x6c, ParkAudio.SingleScreamEffectFor( 7, -1 ) );
		Assert.AreEqual( 0x6d, ParkAudio.SingleScreamEffectFor( 8, -1 ), "8 upwards the last" );
		Assert.AreEqual( 0x6d, ParkAudio.SingleScreamEffectFor( 900, -1 ) );

		// The whole point of the branch: 0x6b is skipped, so no band may ever produce it.
		for ( int band = -5; band < 200; ++band )
			Assert.AreNotEqual( 0x6b, ParkAudio.SingleScreamEffectFor( band, -1 ),
				$"band {band} must not reach 0x6b, which the engine's own switch skips" );
	}

	/// <summary>
	/// The 4x4 grid (<c>FUN_00551320</c>): the band crossed with <c>(level + speed) / 50</c>, giving
	/// <c>0x4b</c> through <c>0x5a</c>. <b>The two levels here are the only ones shipped content ever
	/// passes</b> - <c>Monkey.rse</c> sends 90 and <c>Totem.RSE</c> sends 100, and every other one of
	/// the 46 uses passes 65535, which sign-extends to -1 and takes the branch above.
	/// </summary>
	[TestMethod]
	public void ANonNegativeLevelCrossesTheBandWithAGrid()
	{
		Assert.AreEqual( 0, ParkAudio.SingleScreamEffectFor( 0, 90 ), "band nought is still silent" );

		// Monkey.rse's own instruction: level 90 is grid step 2.
		Assert.AreEqual( 2, ParkAudio.ScreamGridIndex( 90 ) );
		Assert.AreEqual( 0x4d, ParkAudio.SingleScreamEffectFor( 1, 90 ) );
		Assert.AreEqual( 0x51, ParkAudio.SingleScreamEffectFor( 3, 90 ) );
		Assert.AreEqual( 0x55, ParkAudio.SingleScreamEffectFor( 5, 90 ) );
		Assert.AreEqual( 0x59, ParkAudio.SingleScreamEffectFor( 9, 90 ) );

		// Totem.RSE's: level 100 is step 3, which reaches the top of the range.
		Assert.AreEqual( 3, ParkAudio.ScreamGridIndex( 100 ) );
		Assert.AreEqual( 0x5a, ParkAudio.SingleScreamEffectFor( 8, 100 ), "the last id the grid holds" );

		// Held at the top and nowhere else, which is the engine's own asymmetry.
		Assert.AreEqual( 3, ParkAudio.ScreamGridIndex( 1000 ) );
		Assert.AreEqual( 0x5a, ParkAudio.SingleScreamEffectFor( 8, 100000 ) );
	}

	/// <summary>
	/// <b>The grid's first column cannot be reached at all, and that is arithmetic rather than an
	/// oversight.</b> The step is <c>(level + speed) / 50</c> with <see cref="ParkAudio.ScriptSpeed"/> at 50, so a
	/// step of nought needs a level below nought - and a level below nought is exactly what sends the handler
	/// down the band-only branch instead. So <c>0x4b</c>, <c>0x4f</c>, <c>0x53</c> and <c>0x57</c> are
	/// **dead by arithmetic** here for every script. The original's arithmetic does not close them: a placed
	/// thing's script takes its item's operating speed when one is set (<c>docs/exe/park.md</c>, "The clock,
	/// the speed word, and WAIT"), and a speed under 50 reaches column nought.
	/// </summary>
	[TestMethod]
	public void TheGridsFirstColumnIsUnreachable()
	{
		Assert.AreEqual( 1, ParkAudio.ScreamGridIndex( 0 ), "a level of nought is already step one" );
		Assert.AreEqual( 1, ParkAudio.ScreamGridIndex( 49 ), "and so is the last level below the step" );
		Assert.AreEqual( 2, ParkAudio.ScreamGridIndex( 50 ), "50 is where the second step begins" );

		foreach ( var first in new[] { 0x4b, 0x4f, 0x53, 0x57 } )
			for ( int band = 1; band < 40; ++band )
				for ( int level = 0; level < 400; ++level )
					Assert.AreNotEqual( first, ParkAudio.SingleScreamEffectFor( band, level ),
						$"band {band} level {level} reached column nought, which no level can" );
	}
}
