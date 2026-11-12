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
	/// Frames of silence an MPEG Layer I decoder takes: a header and every subband's allocation nought, so each frame is 384
	/// samples a channel of nothing. Built here, so the test needs no game. <paramref name="mpeg1"/> picks MPEG-1 at 44,100 Hz
	/// (64 kbit/s, 68 bytes a frame), otherwise MPEG-2 at 22,050 Hz (64 kbit/s, 136 bytes a frame).
	/// </summary>
	internal static byte[] SilentLayerI( int frames, bool mpeg1, bool stereo )
	{
		var length = mpeg1 ? 68 : 136;
		var data = new byte[frames * length];

		for ( int f = 0; f < frames; ++f )
		{
			data[f * length] = 0xFF;
			data[(f * length) + 1] = (byte)(mpeg1 ? 0xFF : 0xF7);
			data[(f * length) + 2] = (byte)(mpeg1 ? 0x20 : 0x40);
			data[(f * length) + 3] = (byte)(stereo ? 0x00 : 0xC0);
		}

		return data;
	}

	/// <summary>
	/// A clip keeps the bank's played length, <c>DecodedSize / (2 x channels)</c> frames of its stream, and no more (Q659): five
	/// frames are 1,920 a channel, and the bank says to play fewer. A stereo clip counts both channels' bytes; a 44,100 Hz clip
	/// is cut at its own rate and then halved; a played length past the stream adds nothing; nought keeps the whole decode.
	/// </summary>
	/// <remarks><b>Mutations:</b> no cut (1920); the channels left out of the divisor (stereo 1000, not 500); the cut made
	/// after the resample (44,100 Hz: 700, not 350); the cut made as a sample count, not whole frames (stereo, odd bytes).</remarks>
	[TestMethod]
	public void AClipIsCutToTheBanksPlayedLengthInItsStreamsOwnFrames()
	{
		var mono = AudioClip.Decode( "mono", SilentLayerI( 5, mpeg1: false, stereo: false ), 2000 );
		Assert.IsNotNull( mono );
		Assert.AreEqual( 1, mono!.Channels );
		Assert.AreEqual( 1000, mono.Frames, "2,000 bytes of mono 16-bit PCM" );

		var stereo = AudioClip.Decode( "stereo", SilentLayerI( 5, mpeg1: false, stereo: true ), 2002 );
		Assert.IsNotNull( stereo );
		Assert.AreEqual( 2, stereo!.Channels );
		Assert.AreEqual( 500, stereo.Frames, "2,002 bytes of stereo 16-bit PCM: 500 whole frames" );
		Assert.AreEqual( 1000, stereo.Samples.Length );

		var fast = AudioClip.Decode( "fast", SilentLayerI( 5, mpeg1: true, stereo: false ), 1400 );
		Assert.IsNotNull( fast );
		Assert.AreEqual( 350, fast!.Frames, "700 frames at 44,100 Hz, then brought to 22,050" );

		Assert.AreEqual( 1920, AudioClip.Decode( "long", SilentLayerI( 5, mpeg1: false, stereo: false ), 100000 )!.Frames, "nothing added past the stream" );
		Assert.AreEqual( 1920, AudioClip.Decode( "none", SilentLayerI( 5, mpeg1: false, stereo: false ), 0 )!.Frames, "no played length, the whole decode" );
	}

	/// <summary>
	/// Every shipped clip is as long as its bank's played length, at the device's rate, and every one is cut by it: the stream
	/// is always the longer (Mp2DurationTests). The five MPEG-1 entries, at 44,100 Hz, are named and checked by frame count.
	/// The stream's channel count, which the cut divides by, is the bank's for every entry.
	/// </summary>
	/// <remarks><b>Mutations:</b> no cut (every clip long); the cut after the resample (the five MPEG-1 entries twice their length).</remarks>
	[TestMethod]
	public void EveryShippedClipIsItsBanksPlayedLength()
	{
		GameData.Required();

		var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
		var entries = Directory.EnumerateFiles( GameDir.Data, "*.sdt", options )
			.SelectMany( path =>
			{
				using var archive = new SdtArchive( path );
				return archive.soundFiles.Select( entry => (Path: Path.GetRelativePath( GameDir.Data, path ).Replace( '\\', '/' ), Entry: entry) ).ToList();
			} )
			.ToList();

		var cut = 0;
		var wrong = new System.Collections.Concurrent.ConcurrentBag<string>();
		var fast = new System.Collections.Concurrent.ConcurrentBag<string>();

		Parallel.ForEach( entries, item =>
		{
			var (path, entry) = item;
			var clip = AudioClip.Decode( entry.Name, entry.SoundData, entry.DecodedSize );

			if ( clip == null )
				return;

			var bankChannels = entry.SoundType == MP2File.SoundTypes.MP2_STEREO ? 2 : 1;
			var played = entry.DecodedSize / (2 * bankChannels);
			var expected = (int)((long)played * Audio.SampleRate / entry.SampleRate);

			if ( clip.Channels != bankChannels || clip.Frames != expected )
				wrong.Add( $"{path}:{entry.Name} {clip.Channels}ch {clip.Frames} frames, expected {bankChannels}ch {expected}" );

			if ( entry.SampleRate != Audio.SampleRate )
				fast.Add( $"{path}:{entry.Name} {played}>{clip.Frames}" );

			Interlocked.Increment( ref cut );
		} );

		Assert.AreEqual( 0, wrong.Count, string.Join( "; ", wrong.Take( 5 ) ) );
		Assert.AreEqual( 3659, cut, "every clip NLayer decodes" );
		CollectionAssert.AreEqual( new[] { "global/sound/RideHD.sdt:mortar_2.mp2", "global/sound/RideHD.sdt:mortar_3.mp2",
			"global/sound/RideHD.sdt:tp_riderepaired", "global/sound/RideHD.sdt:tp_sideshowwin_", "global/sound/SfxHD.sdt:keyexplode.mp2" },
			fast.Select( f => f[..f.LastIndexOf( ' ' )] ).Order().ToList(), "the five 44,100 Hz entries: " + string.Join( ", ", fast.Order() ) );
	}

	/// <summary>
	/// Every shipped clip, decoded here by NLayer's own float decode and by <see cref="AudioClip.Decode"/>: each stored
	/// word is that float taken to the nearest 16-bit word and held to the range. The clips that peak past full scale are the ones a missing
	/// clamp gets wrong, and they are counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> truncation for rounding; no clamp (a wrapped word); each fails here against the expression written in the test. Quantising before resampling is not caught: it is equivalent on the shipped clips, whose five 44,100 Hz members resample 2:1 exactly (blend 0).</remarks>
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
		var pastPlayed = 0;
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
				Assert.IsNull( AudioClip.Decode( entry.Name, entry.SoundData, entry.DecodedSize ), $"{path}:{entry.Name} would not decode here but did there" );
				Interlocked.Increment( ref skipped );
				return;
			}

			var clip = AudioClip.Decode( entry.Name, entry.SoundData, entry.DecodedSize );

			if ( floats.Length == 0 )
			{
				Assert.IsNull( clip );
				Interlocked.Increment( ref skipped );
				return;
			}

			Assert.IsNotNull( clip, $"{path}:{entry.Name}" );

			if ( floats.Any( f => MathF.Abs( f ) > 1f ) )
				Interlocked.Increment( ref past );

			// The clip is cut to the bank's played length before anything else (Q659), so the comparison is too.
			floats = [.. floats.Take( Math.Min( floats.Length, entry.DecodedSize / (2 * channels) * channels ) )];

			if ( floats.Any( f => MathF.Abs( f ) > 1f ) )
				Interlocked.Increment( ref pastPlayed );

			// The five that are not at the device's rate go through the resampler first, then to words.
			if ( rate != Audio.SampleRate )
			{
				floats = AudioClip.Resample( floats, channels, rate, Audio.SampleRate );
				Interlocked.Increment( ref resampled );
			}

			Assert.AreEqual( floats.Length, clip!.Samples.Length, $"{path}:{entry.Name}" );
			Assert.AreEqual( channels, clip.Channels );

			// The expected word is written out here, not taken from Quantise: nearest-even rounding of f * 32768, then held to -32768..32767.
			for ( int i = 0; i < floats.Length; ++i )
			{
				var word = Math.Round( (double)floats[i] * 32768.0, MidpointRounding.ToEven );
				var expected = (short)(word > 32767.0 ? 32767.0 : word < -32768.0 ? -32768.0 : word);

				if ( clip.Samples[i] != expected )
				{
					firstWrong.Add( $"{path}:{entry.Name}[{i}]" );
					break;
				}
			}

			Interlocked.Increment( ref compared );
		} );

		Assert.AreEqual( 0, firstWrong.Count, "stored words differ from the float decode's: " + string.Join( ", ", firstWrong.Take( 5 ) ) );
		Assert.AreEqual( 3659, compared, "the clips NLayer decodes, compared" );
		Assert.AreEqual( 336, past, "clips whose float decode peaks past full scale (docs/exe/audio.md says 336)" );
		Assert.AreEqual( 335, pastPlayed, "of them, the clips that peak past it within the played length: one only in the tail that is cut" );
		Assert.AreEqual( 5, resampled, "the clips shipped at 44,100 Hz" );
		Assert.AreEqual( 80, skipped, "the clips NLayer refuses, set aside (Decode refuses them too)" );
	}

	/// <summary>
	/// A bank hands out its clips cut to the played length on both of its roads: the global ride bank decodes whole as it
	/// loads (kept by a park), the global kids bank, over 16 MB decoded, decodes each entry when it is first asked for (never
	/// kept). Each clip is its entry's <c>DecodedSize / (2 x channels)</c> frames at the stream's rate, brought to the device's.
	/// </summary>
	/// <remarks><b>Mutations:</b> either road passing nought (the whole decode) or the compressed size for the played length.</remarks>
	[TestMethod]
	public void ABankCutsEachClipToItsPlayedLengthOnBothRoads()
	{
		FileSystem = GameData.Required();
		SoundBank.ForgetKept();

		try
		{
			foreach ( var (bankPath, whole) in new[] { ("Sound/Ride", true), ("Sound/Kids", false) } )
			{
				var bank = SoundBank.Load( "global", bankPath, keep: true );

				Assert.IsNotNull( bank, $"{bankPath} ships" );

				if ( whole )
					Assert.AreSame( bank, SoundBank.Load( "global", bankPath, keep: true ), $"{bankPath} decodes whole" );
				else
					Assert.AreNotSame( bank, SoundBank.Load( "global", bankPath, keep: true ), $"{bankPath} decodes on demand" );

				using var stream = FileSystem.OpenReadOrNull( bank!.Path )!;
				using var archive = new SdtArchive( stream );
				var checkedClips = 0;

				for ( int i = 0; i < bank.Count; ++i )
				{
					var entry = archive.soundFiles[i];
					var clip = bank[i];

					if ( clip == null )
						continue;

					var channels = entry.SoundType == MP2File.SoundTypes.MP2_STEREO ? 2 : 1;
					var expected = (int)((long)(entry.DecodedSize / (2 * channels)) * Audio.SampleRate / entry.SampleRate);

					Assert.AreEqual( expected, clip.Frames, $"{bankPath}:{entry.Name}" );
					++checkedClips;
				}

				Assert.IsTrue( checkedClips > 0, $"{bankPath} handed out clips" );
			}
		}
		finally
		{
			SoundBank.ForgetKept();
		}
	}
}
