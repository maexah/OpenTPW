using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The original's <c>Sound_StopFading</c> (<c>0x0051c300</c>): a x0.6 step on the voice's own level, with its group's gains
/// folded in, every 100 ms of a worker thread (250 ms when nothing was stepping), down to a stop at 0; and a hard stop at
/// sound detail <c>SoundInfo.FADETOGGLE</c> (30) or below (<c>docs/exe/audio.md</c>, "The fade"; Q509). The sequences are the
/// ones that page predicts. No game and no audio device: a voice is made on a silent clip and stepped by hand, as the
/// mixer would step it.
/// </summary>
[TestClass]
public class SoundStopFadingTests
{
	/// <summary>A voice on a second of silence, not on <see cref="Audio.Voices"/>.</summary>
	private static Voice MakeVoice( float volume, AudioBus bus = AudioBus.Effects )
	{
		var clip = (AudioClip)typeof( AudioClip ).GetConstructor( BindingFlags.Instance | BindingFlags.NonPublic, [typeof( string ), typeof( short[] ), typeof( int )] )!
			.Invoke( ["silence", new short[Audio.SampleRate], 1] );

		return new Voice( clip, volume, loop: false, fadeInSeconds: 0f, bus, position: null );
	}

	[TestCleanup]
	public void PutTheStockDetailBack() => Audio.SetSoundDetail( 50, 30 );

	/// <summary>
	/// The step on a level out of 100: at a group product of 1.0 it runs 100, 60, 36, 21, 12, 7, 4, 2, 1, 0 (nine steps); at the
	/// effects' 0.75 the device gets 75, then 45, 20, 9, 4, 1, 0 (six steps).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> rounding <c>60 x n / 100</c> instead of truncating gives 22 for 36's step; rounding <c>own x product</c> half away from
	/// nought gives 3 for 6 at 0.75 (4.5 rounds to 4 by <c>fistp</c>); the product left out gives 60, 36, 21 at 0.75; a step of x0.4 fails the first.
	/// </remarks>
	[TestMethod]
	public void TheStepIsSixtyPercentOfTheLevelTimesItsGroupTruncated()
	{
		CollectionAssert.AreEqual( new[] { 60, 36, 21, 12, 7, 4, 2, 1, 0 }, Run( 100, 1.0 ) );
		CollectionAssert.AreEqual( new[] { 45, 20, 9, 4, 1, 0 }, Run( 100, 0.75 ) );

		Assert.AreEqual( 21, Audio.FadeStep( 36, 1.0 ), "60 x 36 / 100 = 21.6 is truncated" );
		Assert.AreEqual( 2, Audio.FadeStep( 6, 0.75 ), "6 x 0.75 = 4.5 rounds half to even, to 4" );
		Assert.AreEqual( 0, Audio.FadeStep( 100, 0.0 ), "a group switched off takes the level to nought at once" );

		static List<int> Run( int own, double product )
		{
			var levels = new List<int>();

			while ( own > 0 )
				levels.Add( own = Audio.FadeStep( own, product ) );

			return levels;
		}
	}

	/// <summary>
	/// The worker scans first and sleeps after: the device's first buffer scans at its first frame and steps nothing, so a fade
	/// started then is first stepped 250 ms on (frame 5512), and then every 100 ms (2205 frames).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a sleep of 100 ms whatever was stepped puts the second wake at 2205; a step every buffer puts it at 1024; sleeping
	/// before the scan rather than after puts the first wake at 5512 rather than 0.
	/// </remarks>
	[TestMethod]
	public void TheWorkerStepsEveryHundredMillisecondsAfterAWaitOfUpToAQuarterSecond()
	{
		var wait = 0.0;
		var wakes = new List<long>();

		for ( long start = 0; wakes.Count < 4; start += 1024 )
		{
			// Something is fading from the first scan on, but that scan was before the fade began.
			var at = Audio.NextFadeWake( ref wait, 1024, stepping: wakes.Count > 0 );

			if ( at >= 0 )
				wakes.Add( start + at );
		}

		CollectionAssert.AreEqual( new long[] { 0, 5512, 7717, 9922 }, wakes );
	}

	/// <summary>
	/// A voice faded at full volume in a group at 1.0 is heard at 0.6, 0.36, 0.21 ... of itself, and its ninth step stops it; in the
	/// effects at 0.75 it is heard at 45, 20, 9, 4, 1 of the 75 it began at, and its sixth step stops it. It plays on until then.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the volume kept as the stepped level without dividing by the product gives 0.45 for the first heard step at 0.75;
	/// stopping at the first step, or never, fails the counts; <see cref="Voice.StopFading"/> starting the level at 100 whatever the volume
	/// fails the half-volume voice's 30.
	/// </remarks>
	[TestMethod]
	public void AFadedVoiceIsHeardAtTheOriginalsStepsAndStopsAtNought()
	{
		Assert.AreEqual( 9, Heard( 1.0f, 1.0, [0.6f, 0.36f, 0.21f, 0.12f, 0.07f, 0.04f, 0.02f, 0.01f] ) );
		Assert.AreEqual( 6, Heard( 1.0f, 0.75, [45f / 75, 20f / 75, 9f / 75, 4f / 75, 1f / 75] ) );
		Assert.AreEqual( 0.3f, Heard( 0.5f, 1.0, null ), 1e-6f, "a voice at half volume starts at 50 and steps to 30" );

		// The steps' count, or, with no levels given, what the first step leaves.
		static float Heard( float volume, double product, float[]? expected )
		{
			var voice = MakeVoice( volume );
			voice.StopFading();

			Assert.IsTrue( voice.Stepping && voice.Ending && voice.Playing, "flagged, and still sounding" );
			Assert.AreEqual( volume, voice.Volume, "nothing is taken off before the first step" );

			if ( expected is null )
			{
				voice.StepFade( product );
				return voice.Volume;
			}

			var steps = 0;
			var heard = new List<float>();

			while ( voice.Playing && steps < 50 )
			{
				steps++;

				if ( voice.StepFade( product ) )
					heard.Add( voice.Volume / volume );
			}

			Assert.AreEqual( expected.Length, heard.Count );

			for ( var i = 0; i < expected.Length; i++ )
				Assert.AreEqual( expected[i], heard[i], 1e-5f, $"step {i + 1}" );

			Assert.IsFalse( voice.Stepping );
			return steps;
		}
	}

	/// <summary>
	/// At sound detail 30 (the shipped <c>FADETOGGLE</c>) or below, <c>Sound_StopFading</c> is the hard stop; at 31 it fades; and a detail
	/// that falls to 30 stops every voice that is stepping (<c>0x006b6240</c>) while a voice not fading plays on.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>detail >= fadeToggle</c> fades at 30; <c>CutFade</c> left out leaves the stepping voice playing; <c>CutFade</c>
	/// stopping every voice stops the one that was not fading.
	/// </remarks>
	[TestMethod]
	public void AtDetailThirtyOrBelowTheFadeIsAStop()
	{
		Audio.SetSoundDetail( 30, 30 );
		var cut = MakeVoice( 1f );
		cut.StopFading();
		Assert.IsFalse( cut.Playing, "at 30 it is the hard stop" );

		Audio.SetSoundDetail( 31, 30 );
		var faded = MakeVoice( 1f );
		faded.StopFading();
		Assert.IsTrue( faded.Playing && faded.Stepping, "at 31 it fades" );

		var other = MakeVoice( 1f );

		lock ( Audio.Lock )
			Audio.Voices.AddRange( [faded, other] );

		try
		{
			Audio.SetSoundDetail( 25, 30 );
			Assert.IsFalse( faded.Playing, "turning fading off stops a voice that is stepping" );
			Assert.IsTrue( other.Playing, "and only those" );
		}
		finally
		{
			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => voice == faded || voice == other );
		}
	}

	/// <summary>The options hand their audio quality on as the detail, against sound.sam's FADETOGGLE (30 shipped, and 30 with no game).</summary>
	/// <remarks><b>Mutations:</b> <c>ApplySoundDetail</c> handing on a fixed 50 leaves fading on at a quality of 30.</remarks>
	[TestMethod]
	public void TheOptionsAudioQualityIsTheDetail()
	{
		var options = new GameOptions { AudioQuality = 30 };
		options.ApplySoundDetail();
		Assert.IsFalse( Audio.FadesOn );

		options.AudioQuality = 31;
		options.ApplySoundDetail();
		Assert.IsTrue( Audio.FadesOn );
	}
}
