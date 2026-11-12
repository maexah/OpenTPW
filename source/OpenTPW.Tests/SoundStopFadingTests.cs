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
	/// effects' 0.75 the device gets 75, then 45, 20, 9, 4, 1, 0 (six steps). The product is stored as a float before it is rounded
	/// (<c>0x006bb8b6</c>): 45 x 0.1f is 4.5 as a float, which rounds to 4, where the double just above 4.5 rounds to 5.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> rounding <c>60 x n / 100</c> instead of truncating gives 22 for 36's step; rounding <c>own x product</c> half away from
	/// nought gives 3 for 6 at 0.75 (4.5 rounds to 4 by <c>fistp</c>); the product left out gives 60, 36, 21 at 0.75; a step of x0.4 fails the first;
	/// the double rounded without the float store gives 3 for 45 at 0.1f.
	/// </remarks>
	[TestMethod]
	public void TheStepIsSixtyPercentOfTheLevelTimesItsGroupTruncated()
	{
		CollectionAssert.AreEqual( new[] { 60, 36, 21, 12, 7, 4, 2, 1, 0 }, Run( 100, 1.0 ) );
		CollectionAssert.AreEqual( new[] { 45, 20, 9, 4, 1, 0 }, Run( 100, 0.75 ) );

		Assert.AreEqual( 21, Audio.FadeStep( 36, 1.0 ), "60 x 36 / 100 = 21.6 is truncated" );
		Assert.AreEqual( 2, Audio.FadeStep( 6, 0.75 ), "6 x 0.75 = 4.5 rounds half to even, to 4" );
		Assert.AreEqual( 0, Audio.FadeStep( 100, 0.0 ), "a group switched off takes the level to nought at once" );
		Assert.AreEqual( 2, Audio.FadeStep( 45, 0.1f ), "45 x 0.1f stored as a float is 4.5, which rounds to 4: 60 x 4 / 100 = 2.4" );

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
	/// An effect voice faded from the game's own gain (<see cref="ParkAudio.ChannelGain"/>) is heard, in the original's levels of 127,
	/// at the steps its variation's volume gives: 100 starts at 75 and is heard at 45, 20, 9, 4, 1, its sixth step stopping it; the
	/// bumper engine's 68 starts at 51 and is heard at 30, 13, 6, 2, 1; the crowd's 14 starts at 10 and is heard at 6, 2, 1. It plays
	/// on until then.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the level seeded as the voice's gain out of 100 (41 for 100) rather than the variation's volume gives 43.6 for
	/// 100's first step and stops a step early; the stepped level kept as the level over the product (the scale of a hundred) gives
	/// 0.6 of the volume, 99 of 127; stopping at the first step, or never, fails the counts.
	/// </remarks>
	[TestMethod]
	public void AFadedVoiceIsHeardAtTheOriginalsStepsAndStopsAtNought()
	{
		Assert.AreEqual( 6, Heard( 100, [45f, 20f, 9f, 4f, 1f] ) );
		Assert.AreEqual( 6, Heard( 68, [30f, 13f, 6f, 2f, 1f] ) );
		Assert.AreEqual( 4, Heard( 14, [6f, 2f, 1f] ) );

		// The steps' count.
		static int Heard( int variationVolume, float[] expected )
		{
			var voice = MakeVoice( ParkAudio.ChannelGain( variationVolume ) );
			var start = Of127( voice.Volume );
			voice.StopFading( variationVolume );

			Assert.IsTrue( voice.Stepping && voice.Ending && voice.Playing, "flagged, and still sounding" );
			Assert.AreEqual( start, Of127( voice.Volume ), "nothing is taken off before the first step" );

			var steps = 0;
			var heard = new List<float>();

			while ( voice.Playing && steps < 50 )
			{
				steps++;

				if ( voice.StepFade( 0.75, 1.0 ) )
					heard.Add( Of127( voice.Volume ) );
			}

			CollectionAssert.AreEqual( expected, heard.ConvertAll( level => MathF.Round( level, 3 ) ), $"from {variationVolume}" );
			Assert.IsFalse( voice.Stepping );
			return steps;
		}
	}

	/// <summary>
	/// The stepped level is heard over what the mixer still multiplies a voice by: with the effects' slider at twice its default (a bus
	/// of 2.0) and the group at 1.0, 100's first step, 60 of 127, is kept at half the gain it would have at a bus of 1.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bus left out of <c>StepFade</c>'s division plays 60 of 127 twice over, at 120.</remarks>
	[TestMethod]
	public void AStepIsHeardOverTheBusesSlider()
	{
		var voice = MakeVoice( ParkAudio.ChannelGain( 100 ) );
		voice.StopFading( 100 );
		voice.StepFade( 1.0, 2.0 );

		Assert.AreEqual( 60f, Of127( voice.Volume ) * 2f, 1e-4f );
	}

	/// <summary>
	/// Advisor_StopQuietly sets his voice's volume to 0 (<c>0x005996e0</c>) before Sound_StopFading (<c>0x00599786</c>): the quietened
	/// voice is silent at once, and the fade's first step, on a level of nought, stops it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the <c>SetVolume( 0f )</c> taken out of <c>Advisor.Quieten</c> leaves the voice at 0.52; seeding the fade from 52 rather
	/// than 0 leaves it playing after the first step.
	/// </remarks>
	[TestMethod]
	public void TheQuietenedAdvisorIsSilentAtOnceAndStopsAtTheFirstStep()
	{
		var voice = MakeVoice( 0.52f, AudioBus.Speech );
		Advisor.Quieten( voice );

		Assert.AreEqual( 0f, voice.Volume, "silent at once" );
		Assert.IsTrue( voice.Playing && voice.Stepping, "handed to the fade" );
		Assert.IsFalse( voice.StepFade( 1.0, 1.0 ), "the first step stops it" );
		Assert.IsFalse( voice.Playing );
	}

	/// <summary>A level of 127, as the original's device has it: the music's 60 plays at <see cref="ParkAudio.MusicVolume"/>.</summary>
	private static float Of127( float volume ) => volume / ParkAudio.MusicVolume * ParkAudio.MusicGroupLevel;

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
		cut.StopFading( 100 );
		Assert.IsFalse( cut.Playing, "at 30 it is the hard stop" );

		Audio.SetSoundDetail( 31, 30 );
		var faded = MakeVoice( 1f );
		faded.StopFading( 100 );
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
