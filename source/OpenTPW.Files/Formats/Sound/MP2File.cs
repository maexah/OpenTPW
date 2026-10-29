namespace OpenTPW;

/// <summary>
/// One entry in a .sdt sound bank.
///
/// Despite the name, the audio is not always MPEG Layer II. Of the 3,739 entries the game ships,
/// 2,650 are Layer I and 1,089 are Layer II - so the type below says which family it is and how
/// many channels, and the stream's own frame header says the rest.
/// </summary>
public sealed class MP2File : ArchiveFile
{
	public enum SoundTypes
	{
		NONE = 0, //  on blanks
		WAV = 2, // on wav
		WAV_OLD = 3, // used before 1.7, like in the german cd version
		MP2_MONO = 36, // mono: 64kbit/s Layer I or 48kbit/s Layer II in the shipped banks
		MP2_STEREO = 37 // stereo: 112kbit/s Layer II or 64kbit/s Layer I in the shipped banks
	}

	public int Header { get; set; }

	/// <summary>The audio on its own, without the entry's header.</summary>
	public byte[] SoundData { get; set; }

	/// <summary>The whole entry, header included.</summary>
	public byte[] Data { get; set; }

	public int SampleRate { get; set; }
	public int BitsPerSample { get; set; }

	public SoundTypes SoundType { get; set; }

	/// <summary>How many bytes of 16-bit PCM the game plays of this entry, as the bank's header states it: a little less than the stream decodes to.</summary>
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
	/// Not from <see cref="DecodedSize"/>, which is the length the game plays, 0.59 to 2.58 frames short
	/// of what the stream decodes to - see <see cref="SoundCategoryFile.ReadSamples"/>, the scan a category
	/// falls back on, which needs lengths accurate enough to tell two samples in a bank apart.
	/// </summary>
	public TimeSpan Duration
	{
		get
		{
			// The bitrate tables in kbit/s: MPEG-1 and MPEG-2 (and 2.5) each have their own, per layer.
			ReadOnlySpan<int> mpeg1Layer1 = [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448];
			ReadOnlySpan<int> mpeg1Layer2 = [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384];
			ReadOnlySpan<int> mpeg2Layer1 = [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256];
			ReadOnlySpan<int> mpeg2Layer2 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];

			if ( SoundData.Length < 4 || SoundData[0] != 0xFF || (SoundData[1] & 0xE0) != 0xE0 )
				return TimeSpan.Zero;

			// The version bits: 3 is MPEG-1, 2 is MPEG-2 (0 is MPEG-2.5, which uses MPEG-2's tables).
			var mpeg1 = ((SoundData[1] >> 3) & 3) == 3;
			var layer = (SoundData[1] >> 1) & 3;
			var bitrateIndex = (SoundData[2] >> 4) & 0xF;

			// 3 is Layer I, 2 is Layer II. Layer III never occurs in the shipped data.
			var table = layer == 3
				? (mpeg1 ? mpeg1Layer1 : mpeg2Layer1)
				: (mpeg1 ? mpeg1Layer2 : mpeg2Layer2);

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
