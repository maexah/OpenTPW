namespace OpenTPW;

/// <summary>
/// A .sdt bank, with its samples decoded and ready to play.
///
/// A small bank is decoded whole when it loads. The lobby's all are - each park's three lobby
/// themes come to 4.5 to 5.5MB of 16-bit words, the biggest sfx bank jungle's ambient bed at 4MB - and for those,
/// decoding a sample at the moment it is wanted would mean decoding on the frame a strike lands.
///
/// A big one is not, because one bank is very big indeed: data\global\Speech holds all 641 of
/// the advisor's lines, and decoding those up front costs 188MB of 16-bit words and three seconds of
/// startup for the sake of the one line he is about to say. Past
/// <see cref="DecodeEverythingBelow"/> a bank keeps the compressed bytes instead and decodes an
/// entry the first time it is asked for, which for a twenty-second line is a few milliseconds -
/// and the advisor's own 0.8-second lead-in before he speaks covers it. See
/// <see cref="Advisor"/>.
/// </summary>
public sealed class SoundBank
{
	/// <summary>
	/// How much decoded audio a bank may hold before it switches to decoding on demand.
	///
	/// 16MB of 16-bit words is about six minutes of mono at 22,050Hz, which clears every lobby sound bank -
	/// the largest is hallow's lobby music at 5.5MB - and catches the global speech bank, each park's
	/// music bank and the global kids bank, which are the only banks above it.
	/// </summary>
	private const int DecodeEverythingBelow = 16 * 1024 * 1024;

	/// <summary>How long each sample is, for <see cref="SoundCategoryFile.ReadSamples"/>.</summary>
	public IReadOnlyList<TimeSpan> Durations { get; }

	public string Path { get; }

	public int Count => _clips.Length;

	private readonly AudioClip?[] _clips;

	/// <summary>
	/// The banks a park asked to keep and that decode whole, by the file they came from and their flags, so a Load Park or a Restart Park,
	/// which build the park's sound again, take them as they stand instead of decoding them a second time (Q924; Q247).
	/// Held until the lobby starts (<see cref="ForgetKept"/>). Only the game's thread reads or writes it, as it does the
	/// banks' own caches.
	/// </summary>
	private static readonly Dictionary<string, SoundBank> Kept = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// Lets go of the kept banks. The lobby calls it as it starts: a park left for the lobby does not hold its sound
	/// through it, and the lobby's own banks are not among them.
	/// </summary>
	public static void ForgetKept()
	{
		if ( Kept.Count > 0 )
			Log.Info( $"Sound banks: {Kept.Count} kept banks let go" );

		Kept.Clear();
	}

	/// <summary>
	/// The still-compressed samples, or null once the whole bank has been decoded. An entry goes
	/// null as it is decoded, so nothing holds the .mp2 bytes for longer than it needs to.
	/// </summary>
	private readonly (string Name, byte[] Data, int PlayedBytes)[]? _encoded;

	/// <summary>Its byte <c>+9</c> in the category that named it (<see cref="SoundCategoryFile.BankFlags"/>), stamped on every clip.</summary>
	public int Flags { get; }

	/// <summary>Which entries have been through <see cref="AudioClip.Decode"/>, decoded or not.</summary>
	private readonly bool[] _tried;

	private SoundBank( string path, AudioClip?[] clips, TimeSpan[] durations,
		(string, byte[], int)[]? encoded, int flags )
	{
		Path = path;
		Flags = flags;
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
	///
	/// <paramref name="flags"/> is the bank's byte <c>+9</c> in the category naming it, which its clips carry. A kept bank is
	/// kept by its file and its flags; no file ships named with two flags (Q1166).
	/// </summary>
	public static SoundBank? Load( string root, string bankPath, bool keep = false, int flags = 0 )
	{
		var wanted = $"{root}/{bankPath}HD.sdt";
		var file = Resolve( wanted );

		var key = file == null ? null : $"{FileSystem.GetAbsolutePath( file )}|{flags}";

		if ( key != null && Kept.TryGetValue( key, out var kept ) )
			return kept;

		var timer = System.Diagnostics.Stopwatch.StartNew();

		try
		{
			using var stream = file == null ? null : FileSystem.OpenReadOrNull( file );

			if ( file == null || stream == null )
			{
				Log.Warning( $"Sound bank '{wanted}' is not there" );
				return null;
			}

			var archive = new SdtArchive( stream );

			var clips = new AudioClip?[archive.soundFiles.Count];
			var durations = new TimeSpan[archive.soundFiles.Count];

			// The .sdt header carries the length the game plays of each entry, which is what each clip is cut to, so how much this bank would cost
			// is known before a single frame is decoded.
			var decoded = archive.soundFiles.Sum( entry => (long)entry.DecodedSize );
			var lazy = decoded > DecodeEverythingBelow;

			var encoded = lazy ? new (string, byte[], int)[archive.soundFiles.Count] : null;

			for ( int i = 0; i < archive.soundFiles.Count; ++i )
			{
				var entry = archive.soundFiles[i];

				durations[i] = entry.Duration;

				if ( lazy )
					encoded![i] = (entry.Name, entry.SoundData, entry.DecodedSize);
				else
					clips[i] = AudioClip.Decode( entry.Name, entry.SoundData, entry.DecodedSize, flags );
			}

			if ( lazy )
				Log.Info( $"Sound bank '{file}': {clips.Length} samples, "
					+ $"{decoded / (1024 * 1024)}MB decoded - decoding on demand" );
			else
				Log.Info( $"Sound bank '{file}': {clips.Length} samples, {decoded / (1024 * 1024)}MB decoded in {timer.ElapsedMilliseconds} ms" );

			var bank = new SoundBank( file, clips, durations, encoded, flags );

			// A bank decoded whole is the same every time it is read, so a park built again (Load Park, Restart Park)
			// takes the one already decoded. A bank that decodes on demand is not kept: what it has decoded by then
			// would sit in memory for as long as the bank does.
			if ( keep && !lazy )
				Kept[key!] = bank;

			return bank;
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
		=> new( path, Array.Empty<AudioClip?>(), Array.Empty<TimeSpan>(), null, 0 );

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

			var (name, data, playedBytes) = _encoded[index];
			_clips[index] = AudioClip.Decode( name, data, playedBytes, Flags );

			// Whether it decoded or not, the compressed bytes have had their one chance.
			_encoded[index] = default;

			return _clips[index];
		}
	}
}
