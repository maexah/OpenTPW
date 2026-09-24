namespace OpenTPW;

/// <summary>
/// The screams the rides are holding, from <c>STARTSCREAM</c> to <c>STOPSCREAM</c>, each on its own clock.
///
/// <para>
/// In the original a held scream is a voice that plays nothing itself and keeps a CHAIN of one-shot
/// children (class <c>0x0070a2e0</c>, tick <c>0x006bdae0</c>). The first child plays at once, from the
/// effect's first variation. Each time the clock passes the newest child's time, another child is made
/// with a fresh variation, a fresh sample and a fresh wait (<c>0x006bdb50</c>, <c>0x006c3a80</c>). The
/// wait belongs to that handle alone and nothing is kept per effect, so two rides holding the same
/// scream each go on their own clock, and a stop takes down its own chain and touches no other
/// (<c>0x006bd9b0</c>). <c>docs/exe/audio.md</c>, "How the engine plays an effect: priority, not a repeat delay" (its
/// paragraph "A held voice is a chain").
/// </para>
///
/// <para>
/// Engine only. What a child sounds like - which category, how loud, where - is handed in by
/// <see cref="ParkAudio"/>, so the clock runs the same with no sound device.
/// </para>
/// </summary>
internal sealed class ParkScreams
{
	/// <summary>One ride's held scream.</summary>
	internal sealed class Chain
	{
		public required int Effect { get; init; }
		public required int Band { get; init; }
		public required Vector3 At { get; init; }

		/// <summary>What the script asked for; <c>SCREAMLEVEL</c> moves it under a scream already held.</summary>
		public int Level;

		/// <summary>The variation the newest child played, zero-based; -1 before the first.</summary>
		public int Variation = -1;

		/// <summary>The time the clock must pass before the next child is made - the newest child's <c>+0x1c</c>.</summary>
		public float NextAt;

		/// <summary>How long the newest child's wait was, in milliseconds.</summary>
		public int Gap;

		/// <summary>How many children the chain has made.</summary>
		public int Plays;

		/// <summary>
		/// The newest child's voice, which is the one a stop cuts. An older child is let go without
		/// being stopped, as the original prunes one (<c>0x006bdc3e</c>), so its sample plays out.
		/// </summary>
		public Voice? Voice;

		public string Sample = "";
	}

	private readonly Dictionary<int, Chain> _chains = [];
	private readonly Func<int, IReadOnlyList<SoundCategoryFile.Variation>> _variationsOf;
	private readonly Func<Chain, int, Voice?> _play;
	private readonly Random _random;

	/// <param name="variationsOf">An effect's variation headers - its waits and its zones.</param>
	/// <param name="play">Sounds one child: a sample of the given variation, for the given chain.</param>
	/// <param name="random">
	/// The draws. The original takes every one from a single generator at <c>0x00fb1f20</c>; the values
	/// differ from it, and the ranges they are drawn over do not.
	/// </param>
	internal ParkScreams( Func<int, IReadOnlyList<SoundCategoryFile.Variation>> variationsOf,
		Func<Chain, int, Voice?> play, Random? random = null )
	{
		_variationsOf = variationsOf;
		_play = play;
		_random = random ?? new Random();
	}

	/// <summary>How many rides are holding a scream.</summary>
	internal int Count => _chains.Count;

	/// <summary>The scream a script is holding, or null.</summary>
	internal Chain? Find( int scriptId ) => _chains.GetValueOrDefault( scriptId );

	/// <summary>Starts a chain for a script, and makes its first child now.</summary>
	internal Chain Start( int scriptId, int effect, int band, int level, Vector3 at, float now )
	{
		var chain = new Chain { Effect = effect, Band = band, Level = level, At = at };

		_chains[scriptId] = chain;
		Step( scriptId, chain, now );

		return chain;
	}

	/// <summary>
	/// Makes the next child of every chain whose clock has passed its newest child's time - the
	/// original's <c>now &gt; +0x1c</c> (<c>0x006bdb88</c>).
	/// </summary>
	/// <remarks>
	/// Called every frame. The original's service runs whenever the group volumes are applied
	/// (<c>0x0051bfab</c>), at a rate not measured, and starts a new child on the pass after the one that
	/// makes it; both put a child here at most a frame from where the original's lands.
	/// </remarks>
	internal void Pump( float now )
	{
		foreach ( var (scriptId, chain) in _chains )
		{
			if ( chain.Plays == 0 || now > chain.NextAt )
				Step( scriptId, chain, now );
		}
	}

	/// <summary>
	/// Takes a script's chain down: its newest child is cut at once and the chain is forgotten. Every
	/// other chain is left exactly as it was, the same effect's included.
	/// </summary>
	/// <remarks>
	/// A cut, not a fade. The original's <c>Sound_StopFading</c> reaches a held scream's children as a
	/// hard stop whether or not fading is on, because the chain itself has no channel to fade
	/// (<c>0x006bcc71</c>, then <c>0x006bd9b0</c>).
	/// </remarks>
	/// <returns>The chain taken down, or null if the script held none.</returns>
	internal Chain? Stop( int scriptId )
	{
		if ( !_chains.Remove( scriptId, out var chain ) )
			return null;

		chain.Voice?.Stop();
		chain.Voice = null;

		return chain;
	}

	/// <summary>Takes every chain down, as the park ends.</summary>
	internal void StopAll()
	{
		foreach ( var chain in _chains.Values )
			chain.Voice?.Stop();

		_chains.Clear();
	}

	/// <summary>
	/// One child: its variation, its wait, then its sound (<c>0x006c3a80</c>). A chain with no variation
	/// for its parameter makes nothing and is asked again next pass, which only a new level can change -
	/// the original parks it until one comes (<c>0x006c3f49</c>).
	/// </summary>
	private void Step( int scriptId, Chain chain, float now )
	{
		var variations = _variationsOf( chain.Effect );
		var next = NextVariation( variations, chain.Variation, ParkAudio.ScreamVolume( chain.Level ), _random );

		if ( next < 0 )
			return;

		var gap = GapMilliseconds( variations[next], _random );

		chain.Variation = next;
		chain.Gap = gap;
		chain.NextAt = now + (gap / 1000f);
		++chain.Plays;

		// The newest child is the one a stop reaches; the previous one plays out untouched.
		chain.Voice = _play( chain, next );
		chain.Sample = chain.Voice?.Name ?? "";

		Log.Info( $"Park audio: script {scriptId} scream {chain.Plays}, effect {chain.Effect} "
			+ $"variation {next + 1} sample '{chain.Sample}', the next in {gap} ms" );
	}

	/// <summary>
	/// Which variation a chain's next child plays (<c>0x006c3e00</c>). The first takes the effect's
	/// first variation whatever the parameter says. Each later one draws, by weight, among the
	/// variations the current one's zones allow for this parameter.
	/// </summary>
	/// <param name="current">The variation the newest child played, or -1 before the first.</param>
	/// <param name="parameter">The chain's parameter - for a scream, parameter 6, <see cref="ParkAudio.ScreamVolume"/>.</param>
	/// <returns>The variation, zero-based, or -1 when no zone covers the parameter.</returns>
	internal static int NextVariation( IReadOnlyList<SoundCategoryFile.Variation> variations, int current,
		int parameter, Random random )
	{
		if ( variations.Count == 0 )
			return -1;

		if ( current < 0 && variations[0].Samples > 0 )
			return 0;

		var zones = variations[Math.Max( current, 0 )].Zones
			.Where( zone => zone.Low <= parameter && parameter <= zone.High
				&& zone.Variation >= 0 && zone.Variation < variations.Count )
			.ToArray();

		if ( zones.Length == 0 )
			return -1;

		var total = zones.Sum( zone => variations[zone.Variation].Weight );

		if ( total <= 0 )
			return zones[0].Variation;

		var roll = random.NextInt64( total );

		foreach ( var zone in zones )
		{
			roll -= variations[zone.Variation].Weight;

			if ( roll < 0 )
				return zone.Variation;
		}

		return zones[^1].Variation;
	}

	/// <summary>
	/// How long a child waits before the next one, in milliseconds, counted from its own start
	/// (<c>0x006bc2d0</c>): nought when the variation gives nought both ways, otherwise from the
	/// shorter bound up to, not including, the longer.
	/// </summary>
	internal static int GapMilliseconds( SoundCategoryFile.Variation variation, Random random )
	{
		if ( variation.GapMin == 0 && variation.GapMax == 0 )
			return 0;

		// Bit 4 takes the wait from the voice's parameter instead (0x006c3d80). No scream sets it.
		if ( (variation.GapByParameter & 4) != 0 )
			Unimplemented.Report( "SOUND_GAP_BY_PARAMETER" );

		var low = Math.Min( variation.GapMin, variation.GapMax );
		var high = Math.Max( variation.GapMin, variation.GapMax );

		return high == low ? low : low + random.Next( high - low );
	}
}
