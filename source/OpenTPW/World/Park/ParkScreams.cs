namespace OpenTPW;

/// <summary>
/// The screams the rides are holding, from <c>STARTSCREAM</c> to <c>STOPSCREAM</c>, each on its own clock, and
/// the ones a refused second start let go, until the park ends (<see cref="LetGo"/>).
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
/// A chain out of range of the listener makes no child, its first included, and one at once in range again: the
/// sound service ticks only the voices in range (<c>SoundService_Pass</c>, <c>0x006b62b0</c>; <see cref="InRange"/>).
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

		/// <summary>The script's speed word when it asked (<c>STARTSCREAM</c> or <c>SCREAMLEVEL</c>): the other half of the average that is the chain's parameter 6.</summary>
		public int Speed = ParkAudio.ScriptSpeed;

		/// <summary>The variation the newest child played, zero-based; -1 before the first.</summary>
		public int Variation = -1;

		/// <summary>
		/// The chain's own variation (<c>+0x8</c>), zero-based, which its range reads (<see cref="InRange"/>): its set-up's
		/// draw by weight among all (<c>0x006bbfc5</c> -> <c>FUN_006bc810</c>), noughted by its first child's branch
		/// (<c>0x006bdb2b</c>), then each child's, which the child's draw writes into it (<c>0x006c3e00</c>).
		/// </summary>
		public int RangeVariation;

		/// <summary>The time the clock must pass before the next child is made - the newest child's <c>+0x1c</c>.</summary>
		public float NextAt;

		/// <summary>How long the newest child's wait was, in milliseconds.</summary>
		public int Gap;

		/// <summary>How many children the chain has made.</summary>
		public int Plays;

		/// <summary>
		/// Whether the chain's list holds no child: as it is made, and when its newest child is pruned (its voice let go, <c>0x006bdc3e</c>) on the very
		/// millisecond it is due, which <c>HeldChain_Extend</c> passes over (<c>now &gt; +0x1c</c>, <c>0x006bdb88</c>) and
		/// <c>HeldChain_Prune</c> does not (<c>now &gt;= +0x1c</c>, <c>0x006bdc23</c>). The next pass in range makes its child
		/// as a first (<c>HeldChain_HeadOrFirstChild</c>, <c>0x006bdb10</c>): a scream's effect has a key (parameter 6) and
		/// its first child set the voice's <c>0x20</c>, so from the second on it draws from its own variation, as any child does.
		/// </summary>
		public bool ListEmpty = true;

		/// <summary>
		/// The newest child's voice while it is in the list, which is the one a stop cuts; null once the list is empty
		/// (<see cref="ListEmpty"/>). A pruned child is let go without being stopped, as the original prunes one
		/// (<c>0x006bdc3e</c>), so its sample plays out.
		/// </summary>
		public Voice? Voice;

		public string Sample = "";

		/// <summary>The newest child's pitch, in 96ths of an octave: each child's own (<c>0x006c3ae0</c>).</summary>
		public int Pitch;

		/// <summary>Whether a second start let this chain go - see <see cref="LetGo"/>.</summary>
		public bool HeldByNothing;

		/// <summary>
		/// Whether the listener was last found past its range (<see cref="InRange"/>): the original's voice flag
		/// <c>0x800</c> (<c>FUN_006bca70</c>).
		/// </summary>
		public bool Away;

		/// <summary>How many children it made with no voice while the world was held (<c>ParkAudio.Unheard</c>).</summary>
		public int Unheard;
	}

	private readonly Dictionary<int, Chain> _chains = [];

	/// <summary>The chains a second start let go: each still makes its children, and only the park's end stops it.</summary>
	private readonly List<(int ScriptId, Chain Chain)> _letGo = [];
	private readonly Func<int, IReadOnlyList<SoundCategoryFile.Variation>> _variationsOf;
	private readonly Func<Chain, int, Voice?> _play;
	private readonly Func<Vector3> _ears;
	private readonly Random _random;

	/// <param name="variationsOf">An effect's variation headers - its waits and its zones.</param>
	/// <param name="play">Sounds one child: a sample of the given variation, for the given chain.</param>
	/// <param name="ears">Where the listener stands, which a chain must be in range of to make a child (<see cref="InRange"/>).</param>
	/// <param name="random">
	/// The draws. The original takes every one from a single generator at <c>0x00fb1f20</c>; the values
	/// differ from it, and the ranges they are drawn over do not.
	/// </param>
	internal ParkScreams( Func<int, IReadOnlyList<SoundCategoryFile.Variation>> variationsOf,
		Func<Chain, int, Voice?> play, Func<Vector3> ears, Random? random = null )
	{
		_variationsOf = variationsOf;
		_play = play;
		_ears = ears;
		_random = random ?? new Random();
	}

	/// <summary>How many rides are holding a scream.</summary>
	internal int Count => _chains.Count;

	/// <summary>How many chains are screaming held by nothing - see <see cref="LetGo"/>.</summary>
	internal int LetGoCount => _letGo.Count;

	/// <summary>The scream a script is holding, or null.</summary>
	internal Chain? Find( int scriptId ) => _chains.GetValueOrDefault( scriptId );

	/// <summary>Starts a chain for a script, and makes its first child now, or, out of range, once it is in range.</summary>
	internal Chain Start( int scriptId, int effect, int band, int level, Vector3 at, float now, int speed = ParkAudio.ScriptSpeed )
	{
		var chain = new Chain
		{
			Effect = effect, Band = band, Level = level, At = at, Speed = speed,
			RangeVariation = Math.Max( ParkAudio.AnyVariation( _variationsOf( effect ), -1, _random ), 0 )
		};

		_chains[scriptId] = chain;

		var variations = _variationsOf( effect );

		Log.Info( $"Park audio: script {scriptId} scream effect {effect} set up at its own variation {chain.RangeVariation + 1}"
			+ (variations.Count > 0 ? $" of range {variations[chain.RangeVariation].Range:0}" : "") );

		if ( Reached( scriptId, chain ) )
		{
			Step( scriptId, chain, now );
			Prune( scriptId, chain, now );
		}

		return chain;
	}

	/// <summary>
	/// Makes the next child of every chain in range whose clock has passed its newest child's time - the
	/// original's <c>now &gt; +0x1c</c> (<c>0x006bdb88</c>), in whole milliseconds as its sound service reads its clock. On
	/// the very millisecond of that time the tick extends nothing and prunes the child (<c>now &gt;= +0x1c</c>,
	/// <c>0x006bdc23</c>), so the list is empty for that pass and the next pass makes the child as a first
	/// (<see cref="Chain.ListEmpty"/>), as a script's own chains do (<see cref="ParkAudio.ScriptVoice.ListEmpty"/>). A chain
	/// out of range makes none; in range again its time has long passed, so it makes one at once, and the next its wait after that.
	/// </summary>
	/// <remarks>
	/// Called every frame, a park's menu or not. The original's service runs whenever the group volumes are applied
	/// (<c>0x0051bfab</c>), each frame through a pause, at a rate not measured, and starts a new child on the pass after
	/// the one that makes it; both put a child here at most a frame from where the original's lands.
	/// </remarks>
	internal void Pump( float now )
	{
		foreach ( var (scriptId, chain) in _chains )
			Tick( scriptId, chain, now );

		// A chain nobody holds is still in the engine's voice list, and its tick knows nothing of the handle.
		foreach ( var (scriptId, chain) in _letGo )
			Tick( scriptId, chain, now );
	}

	/// <summary>One chain's tick (<c>0x006bdae0</c>): extend, then prune, with the one clock reading (<c>0x006bdaef</c>, <c>0x006bdaf7</c>).</summary>
	private void Tick( int scriptId, Chain chain, float now )
	{
		if ( !Reached( scriptId, chain ) )
			return;

		if ( chain.ListEmpty || ParkAudio.Milliseconds( now ) > ParkAudio.Milliseconds( chain.NextAt ) )
			Step( scriptId, chain, now );

		Prune( scriptId, chain, now );
	}

	/// <summary>
	/// The tick's prune (<c>HeldChain_Prune</c>, <c>now &gt;= +0x1c</c>, <c>0x006bdc23</c>): the newest child, once the clock
	/// reaches its time, is deleted without its channel being stopped (<c>0x006bdc3e</c>), so a stop on the pass that empties
	/// the list finds no child to cut (<c>0x006bd9b0</c> walks only the list). It comes on the very millisecond a child is due,
	/// in the pass that makes a child whose wait is nought (its time is the pass's own, <c>0x006c3ac5</c>), and after an extend
	/// that made nothing. Whether the device plays the let-go sample out is not traced (<c>docs/exe/audio.md</c>, "Pruning").
	/// </summary>
	private static void Prune( int scriptId, Chain chain, float now )
	{
		var (ms, due) = (ParkAudio.Milliseconds( now ), ParkAudio.Milliseconds( chain.NextAt ));

		if ( chain.ListEmpty || ms < due )
			return;

		chain.ListEmpty = true;
		chain.Voice = null;
		Log.Info( $"Park audio: script {scriptId} scream effect {chain.Effect}, its newest child pruned at {ms} ms, its time {due} ms, "
			+ $"after {chain.Plays} child(ren), its list empty and its voice let go" + (chain.HeldByNothing ? ", held by nothing" : "") );
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

	/// <summary>
	/// Forgets a script's chain without stopping it: the engine's refused second <c>STARTSCREAM</c>, which
	/// stores its nought over the handle (<c>0x00555ee6</c>). The chain screams on, and no stop keyed by the
	/// script reaches it again.
	/// </summary>
	/// <returns>The chain let go, or null if the script held none.</returns>
	internal Chain? LetGo( int scriptId )
	{
		if ( !_chains.Remove( scriptId, out var chain ) )
			return null;

		chain.HeldByNothing = true;
		_letGo.Add( (scriptId, chain) );

		return chain;
	}

	/// <summary>Takes every chain down, as the park ends, the ones held by nothing with them.</summary>
	internal void StopAll()
	{
		foreach ( var chain in _chains.Values.Concat( _letGo.Select( letGo => letGo.Chain ) ) )
			chain.Voice?.Stop();

		_chains.Clear();
		_letGo.Clear();
	}

	/// <summary>
	/// Whether a chain is in range of the listener (<see cref="InRange"/>), with <see cref="Chain.Away"/> set as the
	/// original's <c>0x800</c> is (<c>FUN_006bca70</c>) and each change logged. The child before plays out: the chain has
	/// no channel to give up, and its children are not in the service's pass.
	/// </summary>
	private bool Reached( int scriptId, Chain chain )
	{
		var near = InRange( chain, _variationsOf( chain.Effect ), _ears() );

		if ( near != chain.Away )
			return near;

		chain.Away = !near;
		Log.Info( $"Park audio: script {scriptId} scream effect {chain.Effect} "
			+ (near ? "in range again" : $"out of range at ({chain.At.X:0},{chain.At.Y:0})")
			+ $" after {chain.Plays} child(ren)" + (chain.HeldByNothing ? ", held by nothing" : "") );

		return near;
	}

	/// <summary>
	/// Whether a chain at <see cref="Chain.At"/> is in range of <paramref name="ears"/>, as the sound service's pass asks
	/// of each voice (<c>FUN_006bca10</c>, from <c>SoundService_Pass</c>, <c>0x006b62b0</c>): the squared distance over
	/// the ground's two axes alone (<c>FUN_006bb9f0</c>) not past the range's square. <c>STARTSCREAM</c> gives no range
	/// (<c>FUN_00551130</c>), so the range is the newest child's variation's, which the child's draw writes into the chain
	/// (<c>FUN_006bc650</c>, <c>0x006c3e00</c>). Only a voice in range is ticked (slot <c>+0x14</c>, <c>0x006bdae0</c>), so
	/// out of range a chain makes no child. Before its first child the range is the variation its set-up drew by weight
	/// among all (<see cref="Chain.RangeVariation"/>); every variation of every kids' scream is 95, so nothing differs there.
	/// </summary>
	internal static bool InRange( Chain chain, IReadOnlyList<SoundCategoryFile.Variation> variations, Vector3 ears )
	{
		if ( variations.Count == 0 )
			return true;

		var range = variations[Math.Clamp( chain.RangeVariation, 0, variations.Count - 1 )].Range;
		var x = chain.At.X - ears.X;
		var y = chain.At.Y - ears.Y;

		return x * x + y * y <= range * range;
	}

	/// <summary>
	/// One child: its variation, its wait, then its sound (<c>0x006c3a80</c>). A chain with no variation
	/// for its parameter makes nothing and is asked again next pass, which only a new level can change -
	/// the original parks it until one comes (<c>0x006c3f49</c>).
	/// </summary>
	private void Step( int scriptId, Chain chain, float now )
	{
		var variations = _variationsOf( chain.Effect );

		// The first child's branch noughts the chain's own variation (0x006bdb2b), which the child's draw then writes.
		if ( chain.Plays == 0 )
			chain.RangeVariation = 0;

		var next = NextVariation( variations, chain.Variation, ParkAudio.ScreamVolume( chain.Level, chain.Speed ), _random );

		if ( next < 0 )
			return;

		var gap = GapMilliseconds( variations[next], _random );

		var fromEmpty = chain.ListEmpty && chain.Plays > 0;

		chain.ListEmpty = false;
		chain.Variation = next;
		chain.RangeVariation = next;
		chain.Gap = gap;
		chain.NextAt = now + (gap / 1000f);
		++chain.Plays;

		// The newest child is the one a stop reaches; the previous one plays out untouched.
		chain.Voice = _play( chain, next );
		chain.Sample = chain.Voice?.Name ?? "";

		Log.Info( $"Park audio: script {scriptId} scream {chain.Plays}, effect {chain.Effect} "
			+ $"variation {next + 1} sample '{chain.Sample}' pitch {chain.Pitch}, the next in {gap} ms"
			+ (fromEmpty ? ", made from an empty list" : "")
			+ (chain.HeldByNothing ? ", held by nothing" : "") + (chain.Voice is null ? ", no voice" : "") );
	}

	/// <summary>
	/// Which variation a chain's next child plays (<c>0x006c3e00</c>). The first takes the effect's
	/// first variation whatever the parameter says. Each later one draws, by weight, among the
	/// variations the current one's zones allow for this parameter: the first zone whose running total of weights is at
	/// least the roll (<c>0x006c3f12</c>..<c>0x006c3f16</c>), the roll being a draw below the total.
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

			if ( roll <= 0 )
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
		if ( (variation.FirstMask & 4) != 0 )
			Unimplemented.Report( "SOUND_GAP_BY_PARAMETER" );

		var low = Math.Min( variation.GapMin, variation.GapMax );
		var high = Math.Max( variation.GapMin, variation.GapMax );

		return high == low ? low : low + random.Next( high - low );
	}
}
