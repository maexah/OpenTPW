using NLayer;

namespace OpenTPW;

/// <summary>
/// One decoded sound, held as 32-bit float PCM at <see cref="Audio.SampleRate"/> and ready to
/// mix.
///
/// Everything the game ships is MPEG audio - 3,739 samples across every .sdt, and not one of
/// them is the WAV that <see cref="MP2File.SoundTypes"/> leaves room for. They divide into
/// MPEG-2 Layer I mono at 64kbit/s (2,641 of them), Layer II mono at 48 (644) and Layer II
/// stereo at 112 (445), all at 22,050Hz, plus five Layer I files at 44,100. So the decoder has
/// to cover Layers I and II but never Layer III, which is the layer that needs a bit reservoir,
/// Huffman tables and an MDCT.
///
/// Clips are decoded whole rather than streamed. The largest in the lobby is jungle's 91-second
/// ambient bed, which comes to 8MB of float; the four parks' music is 1.5MB each. That is small
/// enough that streaming would only buy trouble - a decoder cannot be driven from the mixer
/// callback without a lock, and the whole point of the callback is that it never waits.
/// </summary>
public sealed class AudioClip
{
	/// <summary>Interleaved samples, <see cref="Channels"/> per frame, nominally -1 to 1.</summary>
	public float[] Samples { get; }

	public int Channels { get; }

	/// <summary>How many frames long this is - <see cref="Samples"/> divided by the channel count.</summary>
	public int Frames => Samples.Length / Channels;

	public float Duration => Frames / (float)Audio.SampleRate;

	/// <summary>What it was called in the bank it came out of, for logging.</summary>
	public string Name { get; }

	private AudioClip( string name, float[] samples, int channels )
	{
		Name = name;
		Samples = samples;
		Channels = channels;
	}

	/// <summary>
	/// Decodes one MPEG stream, or returns null if it will not decode.
	///
	/// Null rather than an exception because a bank is loaded as a unit and one bad sample in it
	/// should cost that sample, not the park's whole ambience. The decoder does throw on at least
	/// one file that ships - an entry in global/Speech/speechHD.SDT - so this is not theoretical.
	/// </summary>
	public static AudioClip? Decode( string name, byte[] data )
	{
		try
		{
			using var mpeg = new MpegFile( new MemoryStream( data, writable: false ) );

			// MpegFile.Length is a byte count of the decoded stream, and it is a prediction until
			// the last frame has actually been read - so it sizes the buffer and the buffer grows
			// if it turns out short, rather than being trusted outright.
			var pcm = new float[Math.Max( 4096, mpeg.Length / sizeof( float ) )];
			var count = 0;

			while ( true )
			{
				if ( count == pcm.Length )
					Array.Resize( ref pcm, pcm.Length * 2 );

				var read = mpeg.ReadSamples( pcm, count, pcm.Length - count );

				if ( read <= 0 )
					break;

				count += read;
			}

			if ( count == 0 )
			{
				Log.Warning( $"Sound '{name}' decoded to nothing" );
				return null;
			}

			var channels = mpeg.Channels;

			// Whole frames only. A stereo clip that ended mid-frame would put every later sample
			// on the wrong channel.
			count -= count % channels;

			if ( count != pcm.Length )
				Array.Resize( ref pcm, count );

			if ( mpeg.SampleRate != Audio.SampleRate )
				pcm = Resample( pcm, channels, mpeg.SampleRate, Audio.SampleRate );

			return new AudioClip( name, pcm, channels );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Sound '{name}' would not decode: {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Linear resampling, for the handful of clips that are not already at the device's rate.
	///
	/// Five of the game's samples are 44,100Hz and the rest are 22,050, so rather than run the
	/// mixer at the higher rate for the sake of five sounds - or make it interpolate on every
	/// voice on every frame - the odd ones out are brought down once, here, when they load.
	/// Linear is audibly poor for a big ratio; for a clean 2:1 it is averaging adjacent pairs.
	/// </summary>
	private static float[] Resample( float[] source, int channels, int fromRate, int toRate )
	{
		var sourceFrames = source.Length / channels;
		var targetFrames = (int)((long)sourceFrames * toRate / fromRate);

		if ( targetFrames <= 0 )
			return source;

		var target = new float[targetFrames * channels];
		var step = sourceFrames / (double)targetFrames;

		for ( int frame = 0; frame < targetFrames; ++frame )
		{
			var at = frame * step;
			var left = (int)at;
			var right = Math.Min( left + 1, sourceFrames - 1 );
			var blend = (float)(at - left);

			for ( int channel = 0; channel < channels; ++channel )
			{
				var a = source[(left * channels) + channel];
				var b = source[(right * channels) + channel];

				target[(frame * channels) + channel] = a + ((b - a) * blend);
			}
		}

		return target;
	}
}
