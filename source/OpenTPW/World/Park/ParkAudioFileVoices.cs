namespace OpenTPW;

/// <summary>
/// A park file's voices put back, as the original's load does (<c>Sound_LoadModule</c>, <c>0x0051c400</c>, through the
/// reader <c>0x006b6da0</c>; <c>docs/exe/audio.md</c>, "A park file's voices"): each voice rebuilt in the class its record's
/// type names (<c>0x006b6f0c</c>..<c>0x006b6f1c</c>), not the one its effect's flags would make, its category found by the
/// slot's file name, and kept under its saved handle, so a script's record that names the handle reaches it
/// (<see cref="RideScript.TakeRestoredSounds"/>).
///
/// <para>
/// <b>Built: the held chain</b> (type 6) of any script category (<see cref="ScriptCategory"/>): the gates' 177, the
/// Loudspeakers' 177 to 180 and the Staff Room's 188 in Lost Kingdom's files. Each other class is counted and not put
/// back (<c>PARK_FILE_SOUND_VOICE_*</c>), as are the screams' held chains, which are <see cref="ParkScreams"/>' and not a
/// script's, and a voice of a category no script plays in (the crowd's, the music's).
/// </para>
///
/// <para>
/// <b>Not the original's:</b> its reader stops every live voice first (<c>0x006b6deb</c>), so the land's own sounds this
/// park started at its entry would go and the file's copies stand in for them; here the land's stand and the file's
/// copies of them are counted with the other classes.
/// </para>
/// </summary>
public sealed partial class ParkAudio
{
	/// <summary>The name a voice put back from a park file sounds under until a script's record takes it.</summary>
	internal const int FileOwner = -2;

	/// <summary>The voices put back from the park file, by their saved handles (<see cref="PutBackFileVoices"/>).</summary>
	private readonly Dictionary<int, ScriptVoice> _fileVoices = [];

	/// <summary>The handles of the voices put back from the park file that have since been let go, which a save leaves out.</summary>
	internal IReadOnlySet<int> FileVoicesLetGo
		=> _fileVoices.Where( entry => !_scriptVoices.Contains( entry.Value ) ).Select( entry => entry.Key ).ToHashSet();

	/// <summary>
	/// Whether the park file's voices were skipped for want of a sound device (<see cref="PutBackFileVoices"/>): none was put
	/// back, so none can be let go, and a save leaves out each held chain a script named at the load and none names now instead
	/// (<see cref="ParkFileWriter.Running.VoicesNotPutBack"/>).
	/// </summary>
	internal bool FileVoicesNotPutBack { get; private set; }

	/// <summary>
	/// Whether the park holds a script voice it started itself, none put back from the file: what the original's save
	/// writes from its live voices (<c>0x0051c350</c>) and this one's does not (<see cref="Level"/>'s save counts it).
	/// </summary>
	internal bool HoldsVoicesStartedHere => _scriptVoices.Any( sound => !_fileVoices.ContainsValue( sound ) );

	/// <summary>
	/// Puts back the held chains of <paramref name="voices"/> and answers them by their saved handles; the rest are
	/// counted. With no sound device nothing is loaded and nothing put back, as the original's load with its sound off
	/// skips the list (<c>0x0051c547</c>).
	/// </summary>
	internal IReadOnlyDictionary<int, ScriptVoice> PutBackFileVoices( ParkVoices voices )
	{
		var put = new Dictionary<int, ScriptVoice>();

		if ( voices.Problem is { } problem )
		{
			Log.Warning( $"Park audio: the park file's voice list would not read, so none is put back - {problem}" );
			return put;
		}

		if ( !Audio.Ready )
		{
			FileVoicesNotPutBack = true;
			return put;
		}

		foreach ( var saved in voices.Saved )
		{
			var name = voices.CategoryOf( saved.Record );
			var (sound, gap) = PutBack( saved, name );

			if ( gap != null )
			{
				Unimplemented.Report( gap );
				Log.Info( $"Park audio: the park file's voice 0x{saved.Handle:x8} (type {saved.Type}, '{name}', effect place "
					+ $"{saved.Record.Effect}) is not put back: {gap}" );

				continue;
			}

			put[saved.Handle] = sound!;
			_fileVoices[saved.Handle] = sound!;
			_scriptVoices.Add( sound! );

			Log.Info( $"Park audio: the park file's {sound!.Kind} {sound.Effect} of type {sound.Type} put back under 0x{saved.Handle:x8} at {Time.Now:F3} s "
				+ $"at ({sound.Place?.X:0},{sound.Place?.Y:0},{sound.Place?.Z:0}), variation {sound.Variation + 1} drawn again, "
				+ (sound.KeptChild is null ? "no child" : $"its child's sample '{sound.KeptChild.Name}' kept at variation {sound.KeptChildVariation + 1}") );
		}

		// The original's reader reads such a voice's handle and type and not its record, and every voice after it from
		// the wrong bytes (0x006b6f13, 0x006b6efd); here the list stops there.
		if ( voices.Stopped is { } stopped )
		{
			Unimplemented.Report( voices.StoppedAtVoice?.Type == ParkVoices.FlagHundredType
				? "PARK_FILE_SOUND_VOICE_CLASS_10"
				: "PARK_FILE_SOUND_VOICE_UNREAD" );
			Log.Warning( $"Park audio: the park file's voice list stops short of its {voices.Count}: {stopped}" );
		}

		Log.Info( $"Park audio: {put.Count} of the park file's {voices.Saved.Count} voice(s) put back" );

		return put;
	}

	/// <summary>One saved voice rebuilt, or the name its gap is counted under.</summary>
	private (ScriptVoice? Sound, string? Gap) PutBack( ParkVoices.Voice saved, string? name )
	{
		if ( saved.Type != ParkVoices.HeldChainType )
		{
			return (null, saved.Type switch
			{
				ParkVoices.MusicType => "PARK_FILE_SOUND_VOICE_MUSIC_CLASS",
				ParkVoices.FlagTenType => "PARK_FILE_SOUND_VOICE_CLASS_2",
				ParkVoices.OneShotType => "PARK_FILE_SOUND_VOICE_ONE_SHOT",
				ParkVoices.ChainType => "PARK_FILE_SOUND_VOICE_CHAIN",
				_ => "PARK_FILE_SOUND_VOICE_PLAIN"
			});
		}

		var record = saved.Record;

		if ( FileCategory( name, out var type ) is not { } category )
			return (null, "PARK_FILE_SOUND_VOICE_NO_CATEGORY");

		var effect = category.EffectAt( record.Effect );

		if ( category == _kids && ScreamEffects.Contains( effect ) )
			return (null, "PARK_FILE_SOUND_VOICE_SCREAM");

		var headers = category.VariationsOf( effect );

		if ( effect < 0 || record.Variation < 0 || record.Variation >= headers.Count )
			return (null, "PARK_FILE_SOUND_VOICE_NO_SAMPLE");

		// The writer's held chains of the files on hand all have a key, no range of their own, no rectangle and a held
		// child or none; one that has not is counted rather than guessed at.
		if ( category.ParameterOf( effect ) == 0 )
			return (null, "PARK_FILE_SOUND_VOICE_HELD_CHAIN_NO_KEY");

		if ( record.Range != 0f || record.Rectangle )
			return (null, "PARK_FILE_SOUND_VOICE_HELD_CHAIN_OWN_RANGE");

		if ( record.Child is { Type: not ParkVoices.HeldChildType } )
			return (null, "PARK_FILE_SOUND_VOICE_HELD_CHAIN_CHILD_TYPE");

		var flat = (category.FlagsOf( effect ) & SoundCategory.NoPlaceFlag) != 0;
		var sound = new ScriptVoice
		{
			Script = FileOwner,
			Type = type,
			Effect = effect,
			Category = category,
			Key = category.ParameterOf( effect ),
			Chain = true,
			Held = true,
			Handle = saved.Handle,
			Place = flat ? null : Whole( new Vector3( record.X, record.Z, record.Height ) )
		};

		// The controllers come back with their values (0x006b7150 makes none where the record made them).
		if ( record.Values is { } values )
		{
			for ( var slot = 0; slot < sound.Values.Length; ++slot )
				sound.Values[slot] = values[slot];
		}

		// The child is read and set up before its chain (0x006bd587, then slot +0x10 at 0x006bd5b1): its variation drawn
		// among the zones of the chain's saved one (0x006c3e00), its saved sample kept (0x006bbfda), its time its set-up's
		// clock, 250 ms and the sample's length (0x006bc01b).
		if ( record.Child is { Record: var child } )
		{
			var clip = category.SampleAt( effect, child.Variation, child.Sample, out var length );
			var drawn = ParkScreams.NextVariation( headers, record.Variation, sound.Parameter, _scriptRandom );

			if ( clip is null )
				return (null, "PARK_FILE_SOUND_VOICE_NO_SAMPLE");

			// No zone of the saved variation holds the parameter: the original's child set-up fails and parks its chain
			// (0x006c3f49). No file on hand holds one.
			if ( drawn < 0 )
				return (null, "PARK_FILE_SOUND_VOICE_HELD_CHILD_NO_ZONE");

			sound.KeptChild = clip;
			sound.KeptChildVariation = drawn;
			sound.ListEmpty = false;
			sound.NextAt = Time.Now + OneShotGraceSeconds + (float)length.TotalSeconds;
		}

		// Then the chain's own set-up draws its variation again by weight among all (0x006bbfc5 -> 0x006bc810), and never
		// sets 0x20 (0x006bbe93): so the first time its list is empty it starts again from the effect's first variation.
		sound.Variation = AnyVariation( headers, -1, _scriptRandom );

		return (sound, null);
	}

	/// <summary>
	/// The script category whose files a park file's slot names (<c>\data\global\sound\cat_rides</c>,
	/// <c>\data\levels\jungle\sound\cat_ambient</c>...), and the script type it is, or null for none: a slot is named by
	/// its file name, since the slots' number and order change by file and theme (FileFormats <c>saves.md</c>, "The voice list").
	/// </summary>
	private SoundCategory? FileCategory( string? name, out int type )
	{
		type = 0;

		if ( name is null )
			return null;

		var path = name.Replace( '\\', '/' ).Trim( '/' ).ToLowerInvariant();

		if ( path.StartsWith( "data/", StringComparison.Ordinal ) )
			path = path["data/".Length..];

		var cut = path.LastIndexOf( '/' );

		if ( cut < 0 || !path[(cut + 1)..].StartsWith( "cat_", StringComparison.Ordinal ) )
			return null;

		path = path[..(cut + 1)] + path[(cut + 1 + "cat_".Length)..];

		for ( var candidate = RideEffects.LastParticleType + 1; candidate < RideEffects.LastType; ++candidate )
		{
			if ( ScriptCategory( candidate ) is { IsValid: true } category
				&& string.Equals( category.Name, path, StringComparison.OrdinalIgnoreCase ) )
			{
				type = candidate;
				return category;
			}
		}

		return null;
	}
}
