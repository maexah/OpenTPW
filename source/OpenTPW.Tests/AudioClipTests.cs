using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTPW.Tests;

/// <summary>
/// A clip is held as 16-bit samples, clamped as the original's decoder clamps a voice (<c>0x006c9270</c>; docs/exe/audio.md,
/// "How a voice is decoded"), not as the 32-bit floats NLayer decodes to: half the memory (Q827).
/// </summary>
[TestClass]
public class AudioClipTests
{
	/// <summary>
	/// The mixer's input for a float: rounded to the nearest 16-bit word, and held to the range a word has. A clip that peaks at 1.4
	/// goes to full scale, not round to the other side; a sample under half a word is nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> a cast with no clamp (1.4 wraps); the clamp's low end taken as -32767; truncating where it should round.</remarks>
	[TestMethod]
	public void AFloatBecomesTheNearest16BitWordAndIsHeldToItsRange()
	{
		short[] got = AudioClip.Quantise( [0f, 0.5f, -0.5f, 1f, -1f, 1.4f, -1.4f, 0.7f / 32768f, -0.7f / 32768f, 0.4f / 32768f, 3.6f / 32768f, 32766.6f / 32768f] );

		CollectionAssert.AreEqual( new short[] { 0, 16384, -16384, 32767, -32768, 32767, -32768, 1, -1, 0, 4, 32767 }, got );
	}

	/// <summary>The scale and the clamp meet at full scale: the top word is one step under 1, the bottom is exactly -1.</summary>
	/// <remarks><b>Mutations:</b> the scale taken as 1/32767.</remarks>
	[TestMethod]
	public void TheScaleBringsTheWordsBackToMinusOneAndJustUnderOne()
	{
		Assert.AreEqual( -1f, short.MinValue * AudioClip.Scale );
		Assert.AreEqual( 32767f / 32768f, short.MaxValue * AudioClip.Scale );
	}

	/// <summary>
	/// Every shipped clip, decoded here by NLayer's own float decode and by <see cref="AudioClip.Decode"/>: each stored
	/// word is that float taken to the nearest 16-bit word and held to the range. The clips that peak past full scale are the ones a missing
	/// clamp gets wrong, and they are counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> truncation for rounding; no clamp (a wrapped word); the resampled clips quantised before they are resampled.</remarks>
	[TestMethod]
	public void EveryShippedClipIsItsFloatDecodeTakenTo16BitsAndHeldToTheRange()
	{
		GameData.Required();

		var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
		var entries = Directory.EnumerateFiles( GameDir.Data, "*.sdt", options )
			.SelectMany( path =>
			{
				using var archive = new SdtArchive( path );
				return archive.soundFiles.Select( entry => (Path: path, Entry: entry) ).ToList();
			} )
			.ToList();

		var compared = 0;
		var past = 0;
		var skipped = 0;
		var resampled = 0;
		var firstWrong = new System.Collections.Concurrent.ConcurrentBag<string>();

		Parallel.ForEach( entries, item =>
		{
			var (path, entry) = item;
			float[] floats;
			int rate, channels;

			try
			{
				using var mpeg = new MpegFile( new MemoryStream( entry.SoundData, writable: false ) );
				rate = mpeg.SampleRate;
				channels = mpeg.Channels;
				var all = new List<float>();
				var buffer = new float[8192];

				while ( true )
				{
					var read = mpeg.ReadSamples( buffer, 0, buffer.Length );

					if ( read <= 0 )
						break;

					all.AddRange( buffer.AsSpan( 0, read ) );
				}

				floats = [.. all.Take( all.Count - (all.Count % channels) )];
			}
			catch ( Exception )
			{
				// The entries NLayer will not decode are the ones Decode returns null for.
				Assert.IsNull( AudioClip.Decode( entry.Name, entry.SoundData ), $"{path}:{entry.Name} would not decode here but did there" );
				Interlocked.Increment( ref skipped );
				return;
			}

			var clip = AudioClip.Decode( entry.Name, entry.SoundData );

			if ( floats.Length == 0 )
			{
				Assert.IsNull( clip );
				Interlocked.Increment( ref skipped );
				return;
			}

			Assert.IsNotNull( clip, $"{path}:{entry.Name}" );

			if ( floats.Any( f => MathF.Abs( f ) > 1f ) )
				Interlocked.Increment( ref past );

			// The five that are not at the device's rate go through the resampler first, then to words.
			if ( rate != Audio.SampleRate )
			{
				floats = AudioClip.Resample( floats, channels, rate, Audio.SampleRate );
				Interlocked.Increment( ref resampled );
			}

			Assert.AreEqual( floats.Length, clip!.Samples.Length, $"{path}:{entry.Name}" );
			Assert.AreEqual( channels, clip.Channels );

			if ( !AudioClip.Quantise( floats ).AsSpan().SequenceEqual( clip.Samples ) )
				firstWrong.Add( $"{path}:{entry.Name}" );

			Interlocked.Increment( ref compared );
		} );

		Assert.AreEqual( 0, firstWrong.Count, "stored words differ from the float decode's: " + string.Join( ", ", firstWrong.Take( 5 ) ) );
		Assert.IsTrue( compared > 3000, $"only {compared} clips compared" );
		Assert.AreEqual( 336, past, "clips whose float decode peaks past full scale (docs/exe/audio.md says 336)" );
		Assert.AreEqual( 5, resampled, "the clips shipped at 44,100 Hz" );
		Assert.IsTrue( skipped < 100, $"{skipped} clips set aside" );
	}
}
