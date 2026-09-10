namespace OpenTPW;

/// <summary>
/// A .lip file: when the advisor's mouth should be moving during one line of speech.
///
/// There is one per speech sample, named after it - data\global\Speech\lips.WAD holds sp_001.LIP
/// to sp_637.LIP for the global bank, and each park's Speech\lips folder holds the lip file for
/// its one introduction. The engine builds the name as <c>\Speech\lips\sp_%03d.lip</c> from the
/// sample number.
///
/// The format is nothing but a list of little-endian uint32 timestamps in MICROSECONDS from the
/// start of the sample, ascending, terminated by 0xFFFFFFFF. Each timestamp flips the mouth
/// between talking and quiet. The unit is confirmed by length - jungle's introduction is 25.65s
/// and its last timestamp is 25,327,573 - and the engine divides by 1000 to compare against its
/// millisecond clock.
///
/// Which way the first flip goes is less obvious than it looks. The engine marks the advisor as
/// talking the moment his sample starts, and every file begins with a timestamp of 0, which flips
/// that straight back to quiet - so the intervals that begin at the odd-numbered timestamps are
/// the talking ones. That was checked against the audio rather than inferred from the code alone:
/// across jungle's introduction the odd intervals average 0.12 RMS and the even ones 0.013, and
/// the odd intervals agree with where the speech actually is 84% of the time against 16% for the
/// opposite reading.
/// </summary>
public sealed class LipFile
{
	private const uint Terminator = 0xFFFFFFFF;

	/// <summary>The flips, in order, from the start of the sample.</summary>
	public IReadOnlyList<TimeSpan> Toggles { get; }

	public LipFile( byte[] data )
	{
		var toggles = new List<TimeSpan>( data.Length / 4 );

		for ( int offset = 0; offset + 4 <= data.Length; offset += 4 )
		{
			var microseconds = BitConverter.ToUInt32( data, offset );

			if ( microseconds == Terminator )
				break;

			toggles.Add( TimeSpan.FromTicks( microseconds * (TimeSpan.TicksPerMillisecond / 1000) ) );
		}

		Toggles = toggles;
	}

	/// <summary>
	/// Whether the mouth should be moving <paramref name="elapsed"/> into the sample. Talking
	/// before the first flip, and after an even number of them - see the class remarks.
	/// </summary>
	public bool IsTalking( TimeSpan elapsed )
	{
		var flips = 0;

		while ( flips < Toggles.Count && Toggles[flips] <= elapsed )
			++flips;

		// Past the last flip the engine stops reading the file and treats the mouth as quiet.
		if ( flips == Toggles.Count && Toggles.Count > 0 )
			return false;

		return flips % 2 == 0;
	}

	/// <summary>Loads the lip file at <paramref name="path"/>, or returns false if there isn't one.</summary>
	public static bool TryLoad( string path, out LipFile? lips )
	{
		lips = null;

		try
		{
			lips = new LipFile( FileSystem.ReadAllBytes( path ) );
			return true;
		}
		catch ( Exception )
		{
			return false;
		}
	}
}
