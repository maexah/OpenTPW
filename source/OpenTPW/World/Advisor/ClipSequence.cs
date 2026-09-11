namespace OpenTPW;

/// <summary>
/// Clips played back to back on one timeline - each starting the moment the one before it ends,
/// the last held on its final frame.
///
/// Where the run has got to is worked out from how long ago it began, never added up a frame at
/// a time. Moving on to the next clip on the frame that notices the last one has finished - which
/// is what the original does, in Advisor_Update (0x00599880) - loses whatever part of that frame
/// came after the clip's end, at every change: half a frame each on average, so about 17ms at 30fps
/// and 3.5ms at 144fps, gathering over a line so the body falls behind the voice by an amount that
/// depends on the frame rate. Asked by time instead, the same moment gives the same clip and the
/// same point in it at any frame rate, a long frame simply lands further along, and nothing drifts.
/// </summary>
public sealed class ClipSequence
{
	public static readonly ClipSequence Empty = new( Array.Empty<int>(), _ => 0 );

	private readonly int[] _clips;

	/// <summary>How long each clip plays for, in whole milliseconds.</summary>
	private readonly int[] _lengths;

	/// <summary>When each clip starts, in whole milliseconds from the start of the run.</summary>
	private readonly int[] _starts;

	/// <param name="clips">The clips, in the order they play.</param>
	/// <param name="millisecondsOf">How long a clip plays for, in whole milliseconds.</param>
	public ClipSequence( IReadOnlyList<int> clips, Func<int, int> millisecondsOf )
	{
		_clips = [.. clips];
		_lengths = _clips.Select( clip => Math.Max( millisecondsOf( clip ), 0 ) ).ToArray();
		_starts = new int[_clips.Length];

		for ( int i = 1; i < _clips.Length; ++i )
			_starts[i] = _starts[i - 1] + _lengths[i - 1];

		Milliseconds = _clips.Length == 0 ? 0 : _starts[^1] + _lengths[^1];
	}

	public int Count => _clips.Length;

	/// <summary>How long the whole run takes, in whole milliseconds.</summary>
	public int Milliseconds { get; }

	/// <summary>Whether every clip has played out <paramref name="seconds"/> into the run.</summary>
	public bool IsFinished( float seconds ) => seconds * 1000f >= Milliseconds;

	/// <summary>
	/// Which clip is playing <paramref name="seconds"/> into the run, and how far into it it is.
	/// Before the start that is the first clip's first moment; past the end, the last clip's last.
	/// False only for a run with no clips in it.
	/// </summary>
	public bool TryLocate( float seconds, out int clip, out float secondsIntoClip )
	{
		clip = 0;
		secondsIntoClip = 0f;

		if ( _clips.Length == 0 )
			return false;

		var milliseconds = MathF.Max( seconds * 1000f, 0f );

		// The last clip that has started. A clip of no length is passed straight over, because the
		// one after it starts at the same moment.
		var index = 0;
		while ( index + 1 < _clips.Length && _starts[index + 1] <= milliseconds )
			index++;

		clip = _clips[index];
		secondsIntoClip = MathF.Min( milliseconds - _starts[index], _lengths[index] ) / 1000f;
		return true;
	}
}
