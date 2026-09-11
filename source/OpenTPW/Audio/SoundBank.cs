namespace OpenTPW;

/// <summary>
/// A .sdt bank, with its samples decoded and ready to play.
///
/// A small bank is decoded whole when it loads. The lobby's all are - the four parks' music is
/// 1.5MB of float each and the biggest sfx bank is jungle's 8MB ambient bed - and for those,
/// decoding a sample at the moment it is wanted would mean decoding on the frame a strike lands.
///
/// A big one is not, because one bank is very big indeed: data\global\Speech holds all 641 of
/// the advisor's lines, and decoding those up front costs 375MB of float and three seconds of
/// startup for the sake of the one line he is about to say. Past
/// <see cref="DecodeEverythingBelow"/> a bank keeps the compressed bytes instead and decodes an
/// entry the first time it is asked for, which for a twenty-second line is a few milliseconds -
/// and the advisor's own one-second pause before he speaks covers it. See
/// <see cref="Advisor"/>.
/// </summary>
public sealed class SoundBank
{
	/// <summary>
	/// How much decoded audio a bank may hold before it switches to decoding on demand.
	///
	/// 32MB of float is about six minutes of mono at 22,050Hz, which clears every bank the lobby
	/// touches - the largest is jungle's ambience at 8MB - and catches the speech banks, which
	/// are the only things anywhere near it.
	/// </summary>
	private const int DecodeEverythingBelow = 32 * 1024 * 1024;

	/// <summary>How long each sample is, for <see cref="SoundCategoryFile.ReadSamples"/>.</summary>
	public IReadOnlyList<TimeSpan> Durations { get; }

	public string Path { get; }

	public int Count => _clips.Length;

	private readonly AudioClip?[] _clips;

	/// <summary>
	/// The still-compressed samples, or null once the whole bank has been decoded. An entry goes
	/// null as it is decoded, so nothing holds the .mp2 bytes for longer than it needs to.
	/// </summary>
	private readonly (string Name, byte[] Data)[]? _encoded;

	/// <summary>Which entries have been through <see cref="AudioClip.Decode"/>, decoded or not.</summary>
	private readonly bool[] _tried;

	private SoundBank( string path, AudioClip?[] clips, TimeSpan[] durations,
		(string, byte[])[]? encoded )
	{
		Path = path;
		Durations = durations;
		_clips = clips;
		_encoded = encoded;
		_tried = new bool[clips.Length];
	}

	/// <summary>
	/// Loads a bank named by a category, or returns null if it is not there.
	///
	/// A category names its banks relative to the folder its <i>level</i> is in rather than to the
	/// folder its own .map files are in - "Sound/LobbySfx" from levels/jungle, not from
	/// levels/jungle/Sound - so <paramref name="root"/> is the level and not where the category
	/// was read from.
	///
	/// The file is that path plus "HD.sdt". Every bank the game ships carries the suffix and there
	/// is no matching set without it, so it is appended here rather than being something a caller
	/// has to know.
	/// </summary>
	public static SoundBank? Load( string root, string bankPath )
	{
		var wanted = $"{root}/{bankPath}HD.sdt";
		var file = Resolve( wanted );

		try
		{
			using var stream = file == null ? null : FileSystem.OpenRead( file );

			if ( stream == null )
			{
				Log.Warning( $"Sound bank '{wanted}' is not there" );
				return null;
			}

			var archive = new SdtArchive( stream );

			var clips = new AudioClip?[archive.soundFiles.Count];
			var durations = new TimeSpan[archive.soundFiles.Count];

			// The .sdt header carries each entry's decoded size, so how much this bank would cost
			// is known before a single frame is decoded.
			var decoded = archive.soundFiles.Sum( entry => (long)entry.DecodedSize * 2 );
			var lazy = decoded > DecodeEverythingBelow;

			var encoded = lazy ? new (string, byte[])[archive.soundFiles.Count] : null;

			for ( int i = 0; i < archive.soundFiles.Count; ++i )
			{
				var entry = archive.soundFiles[i];

				durations[i] = entry.Duration;

				if ( lazy )
					encoded![i] = (entry.Name, entry.SoundData);
				else
					clips[i] = AudioClip.Decode( entry.Name, entry.SoundData );
			}

			if ( lazy )
				Log.Info( $"Sound bank '{file}': {clips.Length} samples, "
					+ $"{decoded / (1024 * 1024)}MB decoded - decoding on demand" );

			return new SoundBank( file, clips, durations, encoded );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Sound bank '{wanted}' would not load: {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// The path as it is actually spelled on disk, or null if nothing there matches.
	///
	/// The .map files were written on a case-insensitive filesystem and it shows: the global
	/// category asks for "Sound\Sfx" when the folder is called "sound", and hallow's rides ask
	/// for "Sound\ride" when the file is "rideHD.sdt". On Windows both work and nobody noticed.
	/// Rather than lower-casing - which would break the ones that <i>are</i> spelled right - each
	/// part of the path is matched against what the parent directory really holds.
	/// </summary>
	private static string? Resolve( string path )
	{
		if ( FileSystem.FileExists( path ) )
			return path;

		var parts = path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
		var resolved = string.Empty;

		for ( int i = 0; i < parts.Length; ++i )
		{
			var candidate = resolved.Length == 0 ? parts[i] : $"{resolved}/{parts[i]}";
			var last = i == parts.Length - 1;

			if ( last ? FileSystem.FileExists( candidate ) : FileSystem.DirectoryExists( candidate ) )
			{
				resolved = candidate;
				continue;
			}

			var absolute = FileSystem.GetAbsolutePath( resolved.Length == 0 ? "." : resolved );

			if ( !Directory.Exists( absolute ) )
				return null;

			var match = (last ? Directory.GetFiles( absolute ) : Directory.GetDirectories( absolute ))
				.FirstOrDefault( entry => string.Equals( System.IO.Path.GetFileName( entry ), parts[i],
					StringComparison.OrdinalIgnoreCase ) );

			if ( match == null )
				return null;

			resolved = resolved.Length == 0 ? System.IO.Path.GetFileName( match ) : $"{resolved}/{System.IO.Path.GetFileName( match )}";
		}

		return resolved;
	}

	/// <summary>A bank with nothing in it, for when one named by a category is missing.</summary>
	public static SoundBank Empty( string path )
		=> new( path, Array.Empty<AudioClip?>(), Array.Empty<TimeSpan>(), null );

	/// <summary>
	/// The sample at <paramref name="index"/>, decoding it first if this bank is holding it
	/// compressed, or null if there is nothing there or it would not decode.
	///
	/// Called from the game thread only - the mixer is handed clips, never the bank - so the
	/// caching needs no lock of its own.
	/// </summary>
	public AudioClip? this[int index]
	{
		get
		{
			if ( index < 0 || index >= _clips.Length )
				return null;

			if ( _encoded == null || _tried[index] )
				return _clips[index];

			_tried[index] = true;

			var (name, data) = _encoded[index];
			_clips[index] = AudioClip.Decode( name, data );

			// Whether it decoded or not, the compressed bytes have had their one chance.
			_encoded[index] = default;

			return _clips[index];
		}
	}
}
