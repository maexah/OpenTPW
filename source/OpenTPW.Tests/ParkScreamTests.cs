using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// How loud a ride screams and which sample it screams with - the arithmetic of
/// <c>STARTSCREAM</c> (<c>FUN_00551130</c>), which is the half that needs no audio device.
///
/// <para>
/// <b>Alexah found this by playing: the children never scream on the Belly Bounce.</b> The codepath
/// audit named why - <c>STARTSCREAM</c> and <c>STOPSCREAM</c> were executed eight times each by a
/// placed ride and had no case in the VM at all.
/// </para>
/// <para>
/// <b>Playing it cannot be unit tested and the choosing can.</b> A voice wants a mixer and a category
/// wants the game's data, so <see cref="ParkAudio.Scream"/> itself rests on the in-game check; the band
/// and the volume are pure and are pinned here.
/// </para>
/// </summary>
[TestClass]
public class ParkScreamTests
{
	/// <summary>
	/// The four looping samples and their bands, read off the engine's own comparisons: nought screams
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
}
