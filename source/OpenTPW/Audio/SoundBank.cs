namespace OpenTPW;

/// <summary>
/// A .sdt bank with every sample in it decoded and ready to play.
///
/// Banks are loaded whole. The lobby's are small - the four parks' music is 1.5MB of float each
/// and the biggest sfx bank is jungle's 8MB ambient bed - and loading a sample at the moment it
/// is wanted would mean decoding on the frame a strike lands.
/// </summary>
public sealed class SoundBank
{
	/// <summary>The bank's samples, in the order the .sdt lists them - which is what a category indexes.</summary>
	public IReadOnlyList<AudioClip?> Clips { get; }

	/// <summary>How long each sample is, for <see cref="SoundCategoryFile.ReadSamples"/>.</summary>
	public IReadOnlyList<TimeSpan> Durations { get; }

	public string Path { get; }

	public int Count => Clips.Count;

	private SoundBank( string path, AudioClip?[] clips, TimeSpan[] durations )
	{
		Path = path;
		Clips = clips;
		Durations = durations;
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

			for ( int i = 0; i < archive.soundFiles.Count; ++i )
			{
				var entry = archive.soundFiles[i];

				durations[i] = entry.Duration;
				clips[i] = AudioClip.Decode( entry.Name, entry.SoundData );
			}

			return new SoundBank( file, clips, durations );
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
		=> new( path, Array.Empty<AudioClip?>(), Array.Empty<TimeSpan>() );

	public AudioClip? this[int index] => index >= 0 && index < Clips.Count ? Clips[index] : null;
}
