namespace OpenTPW;

/// <summary>
/// A sound category with its banks loaded - what the game actually plays through.
///
/// The original addresses sound by category and effect id rather than by file, so this pairs a
/// <see cref="SoundCategoryFile"/> with the banks it names and turns "effect 1 of the global
/// lobby sfx" into a clip picked at the stated odds. See <see cref="LobbyAudio"/> for the ids
/// the lobby uses and where they came from.
/// </summary>
public sealed class SoundCategory
{
	/// <summary>An effect: what it can play, how it is picked, and how often it may repeat.</summary>
	private sealed class Effect
	{
		public int Id;
		public TimeSpan RepeatDelay;
		public int ParameterId;
		public int Flags;
		/// <summary>
		/// The weighted lists this effect picks between. Most effects have exactly one; hallow's
		/// repeating ambient effects have three to ten, and space's five to fourteen - see
		/// <see cref="SoundCategoryFile.ReadVariations"/>.
		/// </summary>
		public List<List<SoundCategoryFile.Sample>> Variations = new();

		/// <summary>
		/// Each variation's header - its gap and its zones - in the order of <see cref="Variations"/>,
		/// or empty when the file's headers would not walk. See <see cref="SoundCategoryFile.ReadVariations"/>.
		/// </summary>
		public IReadOnlyList<SoundCategoryFile.Variation> Headers = [];

		public int SampleCount => Variations.Sum( variation => variation.Count );

		/// <summary>
		/// The soonest this may play again, on <see cref="Time.Now"/>'s clock.
		///
		/// Counted from when the last pick would <i>finish</i>, not from when it started. The
		/// lobby's music effect's is four seconds against tracks that run seventeen and a half,
		/// so read as a gap between starts it would stack copies of a park's theme on top of
		/// each other. The delay is this project's own reading of the number, which the
		/// original holds as a voice priority (<c>docs/QUEUE.md</c> Q43).
		/// </summary>
		public float AvailableAt = float.NegativeInfinity;

		/// <summary>
		/// The sample the last draw from a list of two or more took, asked for the avoid-repeat step or not, as the
		/// original's voice keeps it in its byte <c>+0x46</c> (<c>0x006bc747</c>); -1 for none. The original keeps it per
		/// voice and here it is kept per effect, which is the same wherever one voice of the effect sounds at a time (the
		/// lobby's music, see <see cref="LobbyAudio"/>); a voice's first draw does not ask, and so does not read it.
		/// </summary>
		public int LastSample = -1;
	}

	public string Name { get; }

	/// <summary>
	/// False when the category's .map files were missing or did not parse, or name no bank or no effect, and
	/// every call below then no-ops. A bank that would not load keeps its slot, empty, and does not make it false.
	/// </summary>
	public bool IsValid { get; }

	private readonly List<SoundBank> _banks = new();
	private readonly List<Effect> _effects = new();
	private Random _random = RandomSource();

	/// <summary>
	/// Makes a category's source of rolls. The original's is one generator the whole sound library shares (<c>[0xfb1f20]</c>,
	/// stepped by <c>0x006bc6b9</c>..<c>0x006bc6de</c>); each category here has its own <see cref="Random"/>, with the same
	/// range and no sequence anyone could tell apart. Tests that draw replace this to force a roll.
	/// </summary>
	internal static Func<Random> RandomSource { get; set; } = static () => new Random();

	/// <param name="root">
	/// What the category's bank paths are relative to - the level folder, or "global". This is not
	/// where the .map files live; see <paramref name="maps"/>.
	/// </param>
	/// <param name="maps">The folder holding cat_&lt;name&gt;BANK.map and cat_&lt;name&gt;SFX.map.</param>
	/// <param name="keep">
	/// Whether the banks that decode whole are kept for the next category built from them (<see cref="SoundBank.Load"/>);
	/// a park's sound asks, so that a park built again does not decode its banks again.
	/// </param>
	public SoundCategory( string root, string maps, string name, bool keep = false )
	{
		Name = $"{maps}/{name}";

		var file = new SoundCategoryFile( maps, name );

		if ( !file.IsValid )
			return;

		for ( int i = 0; i < file.Banks.Count; ++i )
			// A bank that would not load still takes its slot, because a category addresses banks
			// by position and dropping one would shift every sample after it onto the wrong file.
			_banks.Add( SoundBank.Load( root, file.Banks[i], keep, file.BankFlags[i] ) ?? SoundBank.Empty( file.Banks[i] ) );

		// The walk steps to each variation's sample records by its header's count, as the original's loader
		// does (0x006c0510), so a record whose sample is longer or shorter than the map says is read all the
		// same. The scan by content stands in only for a file the walk cannot finish.
		var headers = file.ReadVariations();
		var lists = headers.Count == file.Effects.Count
			? headers.Select( effect => effect.Select( variation => variation.Records!.ToList() ).ToList() ).ToList()
			: file.ReadSamples( _banks.Select( bank => bank.Durations ).ToList() );

		if ( headers.Count != file.Effects.Count )
			Log.Warning( $"Sound category {Name}: its variation headers did not walk to the end of the file" );

		// Effect n takes list n, in the order of the effect table. An effect with no list plays nothing
		// rather than falling back to some other effect's samples, which would be worse than silence.
		for ( int i = 0; i < file.Effects.Count; ++i )
		{
			_effects.Add( new Effect
			{
				Id = file.Effects[i].Id,
				RepeatDelay = file.Effects[i].RepeatDelay,
				ParameterId = file.Effects[i].ParameterId,
				Flags = file.Effects[i].Flags,
				Variations = i < lists.Count ? lists[i] : new List<List<SoundCategoryFile.Sample>>(),
				Headers = i < headers.Count ? headers[i] : []
			} );
		}

		IsValid = _effects.Count > 0;

		Log.Info( $"Sound category {Name}: {_banks.Count} bank(s), "
			+ $"{_effects.Count} effect(s) [{string.Join( ", ", _effects.Select( e => $"{e.Id}x{e.SampleCount}" ) )}]" );
	}

	/// <summary>The effect ids this category defines, for logging and for the debug console.</summary>
	public IEnumerable<int> EffectIds => _effects.Select( effect => effect.Id );

	/// <summary>
	/// The id of the effect at <paramref name="index"/> in the category's effect table, from nought, as a park file's
	/// voice names it (FileFormats <c>saves.md</c>, "The voice list"), or -1 for none.
	/// </summary>
	public int EffectAt( int index ) => index >= 0 && index < _effects.Count ? _effects[index].Id : -1;

	/// <summary>
	/// Sample <paramref name="sample"/> of variation <paramref name="variation"/> of effect <paramref name="id"/>, each a
	/// place from nought, as a park file's voice names the one it holds, and its record's length; null where there is
	/// no such sample or it would not decode.
	/// </summary>
	public AudioClip? SampleAt( int id, int variation, int sample, out TimeSpan length )
	{
		length = TimeSpan.Zero;
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		if ( effect == null || variation < 0 || variation >= effect.Variations.Count
			|| sample < 0 || sample >= effect.Variations[variation].Count )
			return null;

		var record = effect.Variations[variation][sample];
		length = record.Duration;

		return record.Bank < _banks.Count ? _banks[record.Bank][record.Index] : null;
	}

	/// <summary>
	/// Plays effect <paramref name="id"/>, picking one of its samples at the odds the category
	/// states, and returns the voice - or null if the effect is unknown, empty, or was played too
	/// recently.
	/// </summary>
	/// <param name="respectDelay">
	/// Whether the effect's own repeat delay applies, which is what stops the lobby's one-shot roll -
	/// see <see cref="LobbyAudio"/> - from stacking the same frog on top of itself. <b>The original
	/// has no such throttle</b>: the number read as the delay is a voice priority there, and nothing on
	/// its play path reads a clock (<c>docs/exe/audio.md</c>). The throttle is this project's own,
	/// <c>docs/QUEUE.md</c> Q43; a held scream does not go through it - see <see cref="PickFrom"/>.
	/// </param>
	/// <param name="loop">
	/// Whether the clip repeats seamlessly. The lobby does not use this: its beds are replayed
	/// through the delay above instead, which is what lets a park whose bed is a one-second bird
	/// call behave the same way as one whose bed is ninety seconds of jungle.
	/// </param>
	/// <param name="bus">Which group the voice joins, and so whether the advisor ducks it.</param>
	/// <param name="position">
	/// Where in the world it is sounding, or null to play it flat - see <see cref="Audio.Play"/>. The
	/// category neither knows nor cares which; it picks a sample and hands the place straight on.
	/// </param>
	/// <param name="range">A placed voice's own range, or null for none - see <see cref="Audio.Play"/>.</param>
	/// <param name="ownRange">
	/// Whether a placed voice given no <paramref name="range"/> takes its variation's, as the original's does
	/// (<see cref="RangeOf"/>). The lobby's placed voices are this project's own and take none.
	/// </param>
	/// <param name="avoidRepeat">
	/// Asks the sample draw for its step against the sample before (<c>FUN_006bc680</c> asked with 1): with more
	/// than two samples in the variation, a pick equal to the effect's last moves on to the next. A voice's later
	/// samples ask (<c>0x006bde33</c>) and its first does not (<c>0x006bbfe3</c>), so the lobby's music restarts ask
	/// and its arrival does not; either way the pick is kept.
	/// </param>
	/// <param name="gain">
	/// Given, the volume is what it makes of the picked variation's header, and <paramref name="volume"/> is not
	/// read: the original's voice takes its volume from its variation (<c>FUN_006bc090</c>).
	/// </param>
	/// <param name="rate">
	/// Given, the voice plays at the rate it makes of the picked variation's header, asked after
	/// <paramref name="gain"/> as the original asks for its pitch after its volume (<c>FUN_006bc410</c>).
	/// </param>
	public Voice? Play( int id, float volume = 1f, bool loop = false, float fadeInSeconds = 0f,
		bool respectDelay = true, AudioBus bus = AudioBus.Effects, Vector3? position = null, float? range = null,
		bool ownRange = false, Func<SoundCategoryFile.Variation, float>? gain = null,
		Func<SoundCategoryFile.Variation, float>? rate = null, bool avoidRepeat = false )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		if ( effect == null || effect.SampleCount == 0 )
			return null;

		if ( respectDelay && Time.Now < effect.AvailableAt )
			return null;

		var clip = Pick( effect, out var variation, out _, avoidRepeat );

		if ( clip == null )
			return null;

		return Play( effect, clip, variation, volume, loop, fadeInSeconds, bus, position, range, ownRange, gain, rate );
	}

	/// <summary>
	/// Picks one of effect <paramref name="id"/>'s samples as <see cref="Play(int, float, bool, float, bool, AudioBus, Vector3?, float?, bool, Func{SoundCategoryFile.Variation, float}?, Func{SoundCategoryFile.Variation, float}?)"/>
	/// does, without playing it: what a one-shot's set-up draws and holds for its life (<c>0x006bbf60</c>).
	/// </summary>
	/// <param name="length">The sample record's length (FileFormats <c>sound-categories.md</c>, its <c>+0x08</c>).</param>
	/// <returns>The sample, or null for an unknown or empty effect, or one that would not decode.</returns>
	/// <param name="avoidRepeat">As for <see cref="Play(int, float, bool, float, bool, AudioBus, Vector3?, float?, bool, Func{SoundCategoryFile.Variation, float}?, Func{SoundCategoryFile.Variation, float}?, bool)"/>.</param>
	public AudioClip? PickSample( int id, out int variation, out TimeSpan length, bool avoidRepeat = false )
	{
		variation = -1;
		length = TimeSpan.Zero;
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		return effect == null || effect.SampleCount == 0 ? null : Pick( effect, out variation, out length, avoidRepeat );
	}

	/// <summary>The sample effect <paramref name="id"/>'s last draw from a list of two or more took, or -1 for none (<see cref="Effect.LastSample"/>).</summary>
	internal int LastSampleOf( int id ) => _effects.FirstOrDefault( candidate => candidate.Id == id )?.LastSample ?? -1;

	/// <summary>
	/// Plays <paramref name="clip"/>, a sample of variation <paramref name="variation"/> of effect <paramref name="id"/>
	/// that <see cref="PickSample"/> drew, as <see cref="Play(int, float, bool, float, bool, AudioBus, Vector3?, float?, bool, Func{SoundCategoryFile.Variation, float}?, Func{SoundCategoryFile.Variation, float}?)"/>
	/// plays what it picks, with no repeat delay asked.
	/// </summary>
	public Voice? PlaySample( int id, AudioClip clip, int variation, bool loop = false, AudioBus bus = AudioBus.Effects,
		Vector3? position = null, bool ownRange = false, Func<SoundCategoryFile.Variation, float>? gain = null,
		Func<SoundCategoryFile.Variation, float>? rate = null )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		return effect == null ? null : Play( effect, clip, variation, 1f, loop, 0f, bus, position, null, ownRange, gain, rate );
	}

	private Voice? Play( Effect effect, AudioClip clip, int variation, float volume, bool loop, float fadeInSeconds,
		AudioBus bus, Vector3? position, float? range, bool ownRange, Func<SoundCategoryFile.Variation, float>? gain,
		Func<SoundCategoryFile.Variation, float>? rate )
	{
		if ( ownRange && position is not null )
			range ??= RangeOf( effect, variation );

		if ( gain != null && variation < effect.Headers.Count )
			volume = gain( effect.Headers[variation] );

		// A looping voice never finishes, so it holds the effect until something stops it.
		effect.AvailableAt = loop
			? float.PositiveInfinity
			: Time.Now + clip.Duration + (float)effect.RepeatDelay.TotalSeconds;

		var voice = Audio.Play( clip, volume, loop, fadeInSeconds, bus, position, range );

		if ( voice != null && rate != null && variation < effect.Headers.Count )
			voice.SetRate( rate( effect.Headers[variation] ) );

		return voice;
	}

	/// <summary>
	/// How long effect <paramref name="id"/>'s sample runs - its longest, if it has several - or
	/// zero for an unknown effect. From the bank's headers, so nothing is decoded to answer it.
	///
	/// The advisor needs this before he speaks: the original sizes the animations he talks
	/// through to the length of the line - see <see cref="Advisor"/>.
	/// </summary>
	public TimeSpan Length( int id )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );
		var longest = TimeSpan.Zero;

		if ( effect == null )
			return longest;

		foreach ( var variation in effect.Variations )
		{
			foreach ( var sample in variation )
			{
				if ( sample.Bank < _banks.Count && sample.Index < _banks[sample.Bank].Durations.Count )
					longest = TimeSpan.FromTicks( Math.Max( longest.Ticks, _banks[sample.Bank].Durations[sample.Index].Ticks ) );
			}
		}

		return longest;
	}

	/// <summary>
	/// Picks one sample of variation <paramref name="variation"/> of effect <paramref name="id"/>, at that
	/// variation's odds - what a child of a held voice plays, the way the original's chain picks one
	/// (<c>0x006c3a80</c>). <b>Nothing shared gates it and it leaves nothing behind</b>: the effect's
	/// <see cref="Play"/> throttle is neither asked nor set, since in the original no play of an effect
	/// waits on another.
	/// </summary>
	/// <returns>The sample, or null for an unknown effect or variation, or one that would not decode.</returns>
	public AudioClip? PickFrom( int id, int variation )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		if ( effect == null || variation < 0 || variation >= effect.Variations.Count )
			return null;

		return PickIn( effect.Variations[variation] );
	}

	/// <summary>
	/// <see cref="PickFrom(int, int)"/> for a voice that keeps its own last pick (the original's byte <c>+0x46</c>, which a
	/// change of variation leaves alone): <paramref name="last"/> is read where <paramref name="avoidRepeat"/> asks the
	/// draw for its step (a voice's later samples do, <c>0x006bde33</c>, and its first does not, <c>0x006bbfe3</c>), and
	/// is set to the sample drawn on every draw from a list of two or more, asked or not (<c>0x006bc747</c>).
	/// Start a voice with -1.
	/// </summary>
	public AudioClip? PickFrom( int id, int variation, ref int last, bool avoidRepeat )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		if ( effect == null || variation < 0 || variation >= effect.Variations.Count )
			return null;

		var list = effect.Variations[variation];
		var index = Draw( list, _random.Next( RollRange ), avoidRepeat ? last : -1 );

		if ( index < 0 )
			return null;

		if ( list.Count > 1 )
			last = index;

		return Clip( list[index], out _ );
	}

	/// <summary>
	/// Effect <paramref name="id"/>'s variation headers - how long a held voice waits between samples,
	/// and which variation it may take next - or empty for an unknown effect.
	/// </summary>
	public IReadOnlyList<SoundCategoryFile.Variation> VariationsOf( int id )
		=> _effects.FirstOrDefault( effect => effect.Id == id )?.Headers ?? [];

	/// <summary>Effect <paramref name="id"/>'s parameter id, its record's <c>+0x12</c>, or 0 for none or an unknown effect.</summary>
	public int ParameterOf( int id ) => _effects.FirstOrDefault( effect => effect.Id == id )?.ParameterId ?? 0;

	/// <summary>The bit of an effect's flags word that makes its voice a looped one (<c>FUN_006bbe90</c>).</summary>
	public const int LoopedFlag = 0x8;

	/// <summary>Effect <paramref name="id"/>'s flags word, nought for an effect the category has not got.</summary>
	public int FlagsOf( int id ) => _effects.FirstOrDefault( effect => effect.Id == id )?.Flags ?? 0;

	/// <summary>The bit of an effect's flags word that gives its voice no place and no range (<c>FUN_006bbe90</c>).</summary>
	public const int NoPlaceFlag = 0x200;

	/// <summary>
	/// How far off a voice of variation <paramref name="variation"/> of effect <paramref name="id"/> is heard, where
	/// the voice is given no range of its own: the variation's own (<c>FUN_006bc650</c>), which the engine hands
	/// the mixer as the far end of the voice's distance mapping (<c>0x006bc410</c>). Null for an effect flagged
	/// <see cref="NoPlaceFlag"/>, whose voice is as loud anywhere, and for an effect or variation the category has
	/// not got.
	/// </summary>
	public float? RangeOf( int id, int variation )
		=> _effects.FirstOrDefault( effect => effect.Id == id ) is { } effect ? RangeOf( effect, variation ) : null;

	private static float? RangeOf( Effect effect, int variation )
		=> (effect.Flags & NoPlaceFlag) != 0 || variation < 0 || variation >= effect.Headers.Count
			? null
			: effect.Headers[variation].Range;

	/// <summary>
	/// How long effect <paramref name="id"/> waits after finishing before it may replay. Dead by CODE: nothing
	/// calls it; the play path reads the effect's own field.
	/// </summary>
	public TimeSpan RepeatDelay( int id )
		=> _effects.FirstOrDefault( effect => effect.Id == id )?.RepeatDelay ?? TimeSpan.Zero;

	/// <summary>
	/// Lets effect <paramref name="id"/> play again now - for when a looping voice it was holding
	/// has been stopped.
	/// </summary>
	public void Release( int id )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		if ( effect != null )
			effect.AvailableAt = float.NegativeInfinity;
	}

	/// <summary>
	/// Picks one of an effect's samples: a variation evenly, then a sample within it by weight.
	///
	/// The weights are a running total out of 65,535, and a roll above a list's last total lands in none of its
	/// slices and gives no sample (<see cref="Draw"/>). The variation is drawn evenly: this does not read the weight a variation's
	/// header carries (<see cref="SoundCategoryFile.Variation.Weight"/>) - which for hallow means its
	/// rain, its terrors, its three pairs of bats and its spirit each get a turn rather than the
	/// bats crowding out everything else.
	///
	/// A sample that would not decode gives silence rather than a re-roll: re-rolling would
	/// quietly change the odds, and nothing in the lobby's banks fails to decode anyway.
	/// </summary>
	private AudioClip? Pick( Effect effect, out int variation, out TimeSpan length, bool avoidRepeat = false )
	{
		variation = _random.Next( effect.Variations.Count );

		var list = effect.Variations[variation];
		var index = Draw( list, _random.Next( RollRange ), avoidRepeat ? effect.LastSample : -1 );

		length = TimeSpan.Zero;

		if ( index < 0 )
			return null;

		// Stored on every draw from a list of two or more, asked for the step or not (0x006bc747); a list of one
		// returns its sample before the store (0x006bc6ae).
		if ( list.Count > 1 )
			effect.LastSample = index;

		return Clip( effect.Variations[variation][index], out length );
	}

	/// <summary>
	/// The sample draw's roll runs 0 to 65,535 (<c>FUN_006bc680</c>); <c>Random.Next</c>'s bound is exclusive. The
	/// original's roll is the shared generator's (see <see cref="RandomSource"/>).
	/// </summary>
	internal const int RollRange = 65536;

	/// <summary>
	/// The sample draw (<c>FUN_006bc680</c>, <c>docs/exe/audio.md</c>, "The sample draw"): a list of one gives its
	/// sample with no draw; otherwise the first sample whose odds are at least <paramref name="roll"/> (0 to 65,535)
	/// wins, and none does when the roll is above the last odds. With <paramref name="last"/> given (the step the
	/// original asks for with 1) and more than two samples, a pick equal to it moves on to the next, modulo the count.
	/// </summary>
	/// <returns>The index of the sample drawn, or -1 for none.</returns>
	internal static int Draw( List<SoundCategoryFile.Sample> variation, int roll, int last = -1 )
	{
		if ( variation.Count == 0 )
			return -1;

		if ( variation.Count == 1 )
			return 0;

		var pick = -1;

		for ( int i = 0; i < variation.Count; ++i )
		{
			if ( variation[i].Weight >= roll )
			{
				pick = i;
				break;
			}
		}

		if ( pick >= 0 && variation.Count > 2 && pick == last )
			pick = (pick + 1) % variation.Count;

		return pick;
	}

	private AudioClip? Clip( SoundCategoryFile.Sample sample, out TimeSpan length )
	{
		length = sample.Duration;

		return sample.Bank < _banks.Count ? _banks[sample.Bank][sample.Index] : null;
	}

	/// <summary>One sample of one weighted list, by its running total out of 65,535.</summary>
	private AudioClip? PickIn( List<SoundCategoryFile.Sample> variation ) => PickIn( variation, out _ );

	/// <summary>
	/// One sample of one weighted list, by its running total out of 65,535, and its record's length, as the
	/// original's draw takes it (<see cref="Draw"/>): a roll of 0 to 65,535, and no sample when it is above the
	/// last odds. A held voice's child asks without the avoid-repeat step.
	/// </summary>
	private AudioClip? PickIn( List<SoundCategoryFile.Sample> variation, out TimeSpan length )
	{
		length = TimeSpan.Zero;
		var index = Draw( variation, _random.Next( RollRange ) );

		return index < 0 ? null : Clip( variation[index], out length );
	}
}
