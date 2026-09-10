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
		/// <summary>
		/// The weighted lists this effect picks between. Most effects have exactly one; hallow's
		/// bats and terrors are seven, and space's looping beds three - see
		/// <see cref="SoundCategoryFile.ReadSamples"/>.
		/// </summary>
		public List<List<SoundCategoryFile.Sample>> Variations = new();

		public int SampleCount => Variations.Sum( variation => variation.Count );

		/// <summary>
		/// The soonest this may play again, on <see cref="Time.Now"/>'s clock.
		///
		/// Counted from when the last pick would <i>finish</i>, not from when it started. The
		/// delays only make sense that way: the music effect's is four seconds against tracks
		/// that run seventeen and a half, so read as a gap between starts it would stack three
		/// copies of a park's theme on top of each other.
		/// </summary>
		public float AvailableAt = float.NegativeInfinity;
	}

	public string Name { get; }

	/// <summary>False when the category's files or banks were missing. Every call below no-ops.</summary>
	public bool IsValid { get; }

	private readonly List<SoundBank> _banks = new();
	private readonly List<Effect> _effects = new();
	private readonly Random _random = new();

	/// <param name="root">
	/// What the category's bank paths are relative to - the level folder, or "global". This is not
	/// where the .map files live; see <paramref name="maps"/>.
	/// </param>
	/// <param name="maps">The folder holding cat_&lt;name&gt;BANK.map and cat_&lt;name&gt;SFX.map.</param>
	public SoundCategory( string root, string maps, string name )
	{
		Name = $"{maps}/{name}";

		var file = new SoundCategoryFile( maps, name );

		if ( !file.IsValid )
			return;

		foreach ( var bankPath in file.Banks )
			// A bank that would not load still takes its slot, because a category addresses banks
			// by position and dropping one would shift every sample after it onto the wrong file.
			_banks.Add( SoundBank.Load( root, bankPath ) ?? SoundBank.Empty( bankPath ) );

		var lists = file.ReadSamples( _banks.Select( bank => bank.Durations ).ToList() );

		// The lists come back in the order the file gives them, which is the order of the effect
		// table - so effect n takes list n. An effect with no list plays nothing rather than
		// falling back to some other effect's samples, which would be worse than silence.
		for ( int i = 0; i < file.Effects.Count; ++i )
		{
			_effects.Add( new Effect
			{
				Id = file.Effects[i].Id,
				RepeatDelay = file.Effects[i].RepeatDelay,
				Variations = i < lists.Count ? lists[i] : new List<List<SoundCategoryFile.Sample>>()
			} );
		}

		IsValid = _effects.Count > 0;

		Log.Info( $"Sound category {Name}: {_banks.Count} bank(s), "
			+ $"{_effects.Count} effect(s) [{string.Join( ", ", _effects.Select( e => $"{e.Id}x{e.SampleCount}" ) )}]" );
	}

	/// <summary>The effect ids this category defines, for logging and for the debug console.</summary>
	public IEnumerable<int> EffectIds => _effects.Select( effect => effect.Id );

	/// <summary>
	/// Plays effect <paramref name="id"/>, picking one of its samples at the odds the category
	/// states, and returns the voice - or null if the effect is unknown, empty, or was played too
	/// recently.
	/// </summary>
	/// <param name="respectDelay">
	/// Whether the effect's own repeat delay applies. The original throttles every effect this
	/// way, which is what stops the lobby's one-shot roll - see <see cref="LobbyAudio"/> - from
	/// stacking the same frog on top of itself.
	/// </param>
	/// <param name="loop">
	/// Whether the clip repeats seamlessly. The lobby does not use this: its beds are replayed
	/// through the delay above instead, which is what lets a park whose bed is a one-second bird
	/// call behave the same way as one whose bed is ninety seconds of jungle.
	/// </param>
	/// <param name="bus">Which group the voice joins, and so whether the advisor ducks it.</param>
	public Voice? Play( int id, float volume = 1f, bool loop = false, float fadeInSeconds = 0f,
		bool respectDelay = true, AudioBus bus = AudioBus.Effects )
	{
		var effect = _effects.FirstOrDefault( candidate => candidate.Id == id );

		if ( effect == null || effect.SampleCount == 0 )
			return null;

		if ( respectDelay && Time.Now < effect.AvailableAt )
			return null;

		var clip = Pick( effect );

		if ( clip == null )
			return null;

		// A looping voice never finishes, so it holds the effect until something stops it.
		effect.AvailableAt = loop
			? float.PositiveInfinity
			: Time.Now + clip.Duration + (float)effect.RepeatDelay.TotalSeconds;

		return Audio.Play( clip, volume, loop, fadeInSeconds, bus );
	}

	/// <summary>
	/// How long effect <paramref name="id"/>'s sample runs - its longest, if it has several - or
	/// zero for an unknown effect. From the bank's headers, so nothing is decoded to answer it.
	///
	/// The advisor needs this before he speaks: the original sizes the animations he talks
	/// through to the length of the line - see <see cref="LobbyAdvisor"/>.
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

	/// <summary>How long effect <paramref name="id"/> waits after finishing before it may replay.</summary>
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
	/// The weights are a running total out of 65,535, so a roll in that range lands in exactly one
	/// entry's slice. Nothing in the file weights the variations against each other, so they are
	/// even - which for hallow means its rain, its terrors, its three pairs of bats and its spirit
	/// each get a turn rather than the bats crowding out everything else.
	///
	/// A sample that would not decode gives silence rather than a re-roll: re-rolling would
	/// quietly change the odds, and nothing in the lobby's banks fails to decode anyway.
	/// </summary>
	private AudioClip? Pick( Effect effect )
	{
		var variation = effect.Variations[_random.Next( effect.Variations.Count )];

		if ( variation.Count == 0 )
			return null;

		var roll = _random.Next( 0, Math.Max( 1, variation[^1].Weight ) + 1 );

		foreach ( var sample in variation )
		{
			if ( roll > sample.Weight )
				continue;

			return sample.Bank < _banks.Count ? _banks[sample.Bank][sample.Index] : null;
		}

		return null;
	}

}
