namespace OpenTPW;

/// <summary>
/// One entry in a .sdt sound bank.
///
/// Despite the name, the audio is not always MPEG Layer II. Of the 3,739 entries the game ships,
/// 2,646 are Layer I and 1,093 are Layer II - so the type below says which family it is and how
/// many channels, and the stream's own frame header says the rest.
/// </summary>
public sealed class MP2File : ArchiveFile
{
	public enum SoundTypes
	{
		NONE = 0, //  on blanks
		WAV = 2, // on wav
		WAV_OLD = 3, // used before 1.7, like in the german cd version
		MP2_MONO = 36, // on mp2 (64kbit/s mono)
		MP2_STEREO = 37 // on mp2 (112kbit/s stereo)
	}

	public string Name { get; set; }
	public int Header { get; set; }

	/// <summary>The audio on its own, without the entry's header.</summary>
	public byte[] SoundData { get; set; }

	/// <summary>The whole entry, header included.</summary>
	public byte[] Data { get; set; }

	public int SampleRate { get; set; }
	public int BitsPerSample { get; set; }

	public SoundTypes SoundType { get; set; }

	/// <summary>How many bytes of PCM this decodes to, as the bank's header states it.</summary>
	public int DecodedSize { get; set; }

	public MP2File( int header, string name, byte[] soundData, int sampleRate, int bitsPerSample,
		int soundType, int decodedSize, byte[] data )
	{
		Header = header;
		Name = name;
		SoundData = soundData;
		SampleRate = sampleRate;
		BitsPerSample = bitsPerSample;
		SoundType = (SoundTypes)soundType;
		DecodedSize = decodedSize;
		Data = data;
	}

	/// <summary>
	/// How long this entry is, worked out from the bitrate in its own first frame header.
	///
	/// Not from <see cref="DecodedSize"/>, which is a rounded count that disagrees with the
	/// decoder by up to a frame - see <see cref="SoundCategoryFile.ReadSamples"/>, which needs
	/// lengths accurate enough to tell two samples in a bank apart.
	/// </summary>
	public TimeSpan Duration
	{
		get
		{
			// MPEG-1 and MPEG-2 bitrate tables, Layer I and Layer II, in kbit/s.
			ReadOnlySpan<int> layer1 = [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256];
			ReadOnlySpan<int> layer2 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];

			if ( SoundData.Length < 4 || SoundData[0] != 0xFF || (SoundData[1] & 0xE0) != 0xE0 )
				return TimeSpan.Zero;

			var layer = (SoundData[1] >> 1) & 3;
			var bitrateIndex = (SoundData[2] >> 4) & 0xF;

			// 3 is Layer I, 2 is Layer II. Layer III never occurs in the shipped data.
			var table = layer == 3 ? layer1 : layer2;

			if ( bitrateIndex is 0 or 0xF )
				return TimeSpan.Zero;

			var bitrate = table[bitrateIndex] * 1000;

			return bitrate == 0
				? TimeSpan.Zero
				: TimeSpan.FromSeconds( SoundData.Length * 8.0 / bitrate );
		}
	}

	public override byte[] GetData()
	{
		return Data;
	}
}
