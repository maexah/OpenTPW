namespace OpenTPW;

/// <summary>
/// Things built from a key and shared by every user of the same key, made once and let go of when the last user does (Q954). A
/// <see cref="Material"/> takes its pipeline and resource layouts from one: a model builds its own material, so without sharing a
/// park of 1,031 models made 1,031 pipelines of 13 distinct states, and the software Vulkan driver (lavapipe) kept 5.9 GB for them.
/// It knows nothing of the device: <see cref="Release"/> says when the last user has gone and the caller disposes the value, when
/// the frame is done with it.
/// </summary>
internal sealed class SharedCache<TKey, TValue> where TKey : notnull
{
	/// <summary>One shared value and how many users hold it.</summary>
	internal sealed class Entry( TKey key, TValue value )
	{
		public TKey Key { get; } = key;
		public TValue Value { get; } = value;
		public int Users { get; internal set; }
	}

	private readonly Dictionary<TKey, Entry> _entries = new();

	/// <summary>How many distinct values are alive.</summary>
	public int Count => _entries.Count;

	/// <summary>The entry for <paramref name="key"/>, made by <paramref name="make"/> if no user holds one, with one more user.</summary>
	public Entry Acquire( TKey key, Func<TKey, TValue> make )
	{
		if ( !_entries.TryGetValue( key, out var entry ) )
		{
			entry = new Entry( key, make( key ) );
			_entries[key] = entry;
		}

		entry.Users++;
		return entry;
	}

	/// <summary>Takes one user off <paramref name="entry"/>; true when it was the last, and the entry is gone from the cache.</summary>
	public bool Release( Entry? entry )
	{
		if ( entry == null || --entry.Users > 0 )
			return false;

		_entries.Remove( entry.Key );
		return true;
	}
}
