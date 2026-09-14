namespace OpenTPW;

/// <summary>
/// A sound category - one pair of .map files naming the banks it draws on and the effects it can
/// play.
///
/// The game groups its sounds into categories rather than addressing samples directly. Each one
/// is two files sitting beside the banks, named after the category:
///
///     cat_locallobbysfxBANK.map   which .sdt banks the category draws on
///     cat_locallobbysfxSFX.map    the effects in it, and which samples each one picks from
///
/// Code plays an <i>effect id</i> within a category and the category decides the rest, so the
/// lobby's thunder is "global lobby sfx, effect 1" and there is nothing in the executable that
/// names a Thunder2.mp2. See <see cref="OpenTPW.LobbyAudio"/> for which ids the lobby uses.
///
/// Both files open with the same shape: a 16-byte GUID identifying which of the two they are
/// ({E9612C01-31D0-11D2-B409-00A0C993F203} for a BANK, ...C00-...-00B0... for an SFX), then eight
/// bytes of zero, then a count.
///
/// <b>The BANK file is fully understood.</b> After the count come that many 11-byte records that
/// hold nothing but leftover pointers - the same handful of values recur across every file, which
/// is what a struct written straight out of memory looks like - and then the payload: one
/// length-prefixed, NUL-terminated path per bank. All thirty that ship parse to exactly the end
/// of the file with nothing over.
///
/// <b>The SFX file is understood in part.</b> Its header and effect table are read below and are
/// solid: the counts land where they should in all thirty-one files and every delay comes out a
/// round number of milliseconds. What is <i>not</i> decoded is the per-effect header that sits
/// before each effect's sample list - it varies in size, so the lists cannot simply be stepped
/// over. <see cref="ReadSamples"/> explains how they are found instead.
/// </summary>
public sealed class SoundCategoryFile
{
	/// <summary>One effect: the id code plays it, and how long before it may play again.</summary>
	/// <param name="Id">
	/// What the game passes to its play-sound call. These are not indices - a category's ids need
	/// not start at 1 or run in order, and the ride categories' run into the hundreds.
	/// </param>
	/// <param name="RepeatDelay">
	/// The shortest gap between two plays of this effect. Every value in the shipped data is a
	/// round number of milliseconds, from 700 up to 10,000.
	/// </param>
	/// <param name="Variations">
	/// How many weighted lists this effect picks between - the second int of its record, which this
	/// reader used to skip. It is what divides the run of sample records up between the effects:
	/// jungle's ambient declares 9, 5, 7, 4, 1, 0, 1, 1, 1, summing to exactly the twenty-nine lists
	/// that follow it. A zero is real and means an effect with nothing to play; hallow's, jungle's
	/// and space's ambient each carry one. Checked against all thirty-one categories the game ships.
	/// </param>
	public readonly record struct Effect( int Id, TimeSpan RepeatDelay, int Variations );

	/// <summary>One sample an effect can pick, with the odds of it being the one picked.</summary>
	/// <param name="Bank">Which of <see cref="Banks"/> it lives in, already zero-based.</param>
	/// <param name="Index">Its place in that bank, already zero-based.</param>
	/// <param name="Weight">
	/// The running total of the odds across the effect's list, out of 65,535 - so a list of three
	/// equally likely samples reads 21845, 43690, 65535. The last entry is what ends the list.
	/// </param>
	/// <param name="Duration">How long the sample is, as the map states it.</param>
	public readonly record struct Sample( int Bank, int Index, int Weight, TimeSpan Duration );

	/// <summary>The banks this category draws on, as paths relative to the category's own folder.</summary>
	public IReadOnlyList<string> Banks { get; private set; } = Array.Empty<string>();

	/// <summary>The effects in the category, in the order the file lists them.</summary>
	public IReadOnlyList<Effect> Effects { get; private set; } = Array.Empty<Effect>();

	/// <summary>False when either file was missing or did not match the layout - never throws.</summary>
	public bool IsValid { get; private set; }

	private const int CountOffset = 0x18;
	private const int BankRecordStride = 11;
	private const int EffectTableOffset = 0x34;
	private const int EffectStride = 20;
	private const int EffectDelayField = 3;
	private const int SampleStride = 16;

	/// <summary>
	/// What the cumulative odds have to reach for a list to be over. See
	/// <see cref="ReadSamples"/>: the real figures are 65,529 to 65,535 at the end of a list and
	/// never above 58,248 anywhere else.
	/// </summary>
	private const int Saturated = 65500;

	/// <summary>Which int of an effect's record says how many lists it picks between.</summary>
	private const int EffectVariationField = 1;

	/// <summary>
	/// How far the length a sample record states may be from the length of the sample it points at,
	/// before a share of the length takes over - see <see cref="TryReadSample"/>.
	/// </summary>
	private const double LengthSlackMilliseconds = 100.0;

	private readonly byte[] _sfx = Array.Empty<byte>();

	/// <summary>
	/// Reads the category called <paramref name="name"/> out of <paramref name="directory"/> -
	/// so ("levels/jungle/Sound", "locallobbysfx") for the jungle island's ambience.
	/// </summary>
	public SoundCategoryFile( string directory, string name )
	{
		var bank = ReadAll( $"{directory}/cat_{name}BANK.map" );
		_sfx = ReadAll( $"{directory}/cat_{name}SFX.map" );

		if ( bank.Length == 0 || _sfx.Length == 0 )
			return;

		try
		{
			Banks = ReadBanks( bank );
			Effects = ReadEffects( _sfx );
			IsValid = Banks.Count > 0 && Effects.Count > 0;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Sound category '{name}' in {directory} did not parse: {e.Message}" );
		}
	}

	/// <summary>Where <paramref name="id"/> sits in <see cref="Effects"/>, or -1.</summary>
	public int IndexOf( int id )
	{
		for ( int i = 0; i < Effects.Count; ++i )
			if ( Effects[i].Id == id )
				return i;

		return -1;
	}

	private static string[] ReadBanks( byte[] data )
	{
		var count = BitConverter.ToInt32( data, CountOffset );

		if ( count <= 0 || count > 64 )
			return Array.Empty<string>();

		var offset = CountOffset + 4 + (count * BankRecordStride);
		var banks = new string[count];

		for ( int i = 0; i < count; ++i )
		{
			var length = BitConverter.ToInt32( data, offset );
			offset += 4;

			// Length includes the terminator.
			banks[i] = System.Text.Encoding.ASCII.GetString( data, offset, Math.Max( 0, length - 1 ) )
				.Replace( '\\', '/' );

			offset += length;
		}

		return banks;
	}

	private static Effect[] ReadEffects( byte[] data )
	{
		var count = BitConverter.ToInt32( data, CountOffset + 4 );

		if ( count <= 0 || EffectTableOffset + (count * EffectStride) > data.Length )
			return Array.Empty<Effect>();

		var effects = new Effect[count];

		for ( int i = 0; i < count; ++i )
		{
			var record = EffectTableOffset + (i * EffectStride);

			effects[i] = new Effect(
				BitConverter.ToInt32( data, record ),
				TimeSpan.FromMilliseconds( BitConverter.ToInt32( data, record + (EffectDelayField * 4) ) ),
				BitConverter.ToInt32( data, record + (EffectVariationField * 4) ) );
		}

		return effects;
	}

	/// <summary>
	/// The samples each effect can play, given how long each bank's samples actually are.
	///
	/// The lists cannot be stepped to, because the per-effect header in front of each one varies
	/// in size and has not been decoded. So they are found by what they contain instead: a
	/// sixteen-byte record is a sample if its bank and index are both in range, its two spare
	/// bytes are zero, its odds are inside 1..65,535, and the length it claims matches the length
	/// of the sample it points at. Four independent fields agreeing is not something arbitrary
	/// bytes do, and across the nine lobby categories the records it finds account for every
	/// sample in every bank, in an order that matches what the parks sound like - jungle's beasts,
	/// frogs and screeches, hallow's rain, terrors, bats and bells, the four thunders.
	///
	/// Two things then divide the run of records up.
	///
	/// A <b>list</b> ends when its odds saturate, because they are cumulative out of 65,535 - so
	/// a list of six reads 10922, 21844, 32766, 43688, 54610, 65532 and the last of those is the
	/// end of it. Rounding leaves that final figure anywhere from 65,529 to 65,535 while the
	/// largest that is <i>not</i> the end of a list is 58,248, so <see cref="Saturated"/> sits in
	/// the gap between them.
	///
	/// An <b>effect</b> is over once it has taken as many lists as its own record says it picks
	/// between - <see cref="Effect.Variations"/>, read out of the file rather than guessed at.
	///
	/// <b>This used to measure the gap before the next list instead, and no gap can work.</b> The
	/// two ranges overlap: jungle's ambient runs sixty-four bytes between two lists of the SAME
	/// effect, while the smallest gap between two DIFFERENT effects is forty-two. The lobby never
	/// noticed because all four of its local sfx categories and all four of its music ones declare
	/// a single variation each, so they group the same either way - but every park category did
	/// not, and jungle's nine ambient effects came out as twenty-six, which left effects 178 to 192
	/// each playing one of effect 177's beasts. The global lobby and UI categories were wrong too.
	///
	/// The result is one entry per effect, in the effect table's own order.
	/// </summary>
	/// <returns>
	/// One entry per effect, in the order of <see cref="Effects"/>, each holding one or more
	/// weighted lists to pick between.
	/// </returns>
	public List<List<List<Sample>>> ReadSamples( IReadOnlyList<IReadOnlyList<TimeSpan>> bankDurations )
	{
		var effects = new List<List<List<Sample>>>();

		if ( !IsValid )
			return effects;

		// One entry per effect from the start, so callers may index this by effect whatever the
		// file turns out to hold - and so an effect that declares no variations simply stays empty
		// rather than having to be padded in afterwards.
		for ( int i = 0; i < Effects.Count; ++i )
			effects.Add( new List<List<Sample>>() );

		var list = new List<Sample>();
		var offset = EffectTableOffset + (Effects.Count * EffectStride);
		var effect = 0;

		// Skip anything that takes no lists before reading a single record. The same test moves
		// past an effect once it is full, so a declared zero needs no special case.
		while ( effect < Effects.Count && effects[effect].Count >= Effects[effect].Variations )
			++effect;

		while ( offset + SampleStride <= _sfx.Length && effect < Effects.Count )
		{
			if ( !TryReadSample( offset, bankDurations, out var sample ) )
			{
				offset++;
				continue;
			}

			list.Add( sample );
			offset += SampleStride;

			if ( sample.Weight < Saturated )
				continue;

			effects[effect].Add( list );
			list = new List<Sample>();

			while ( effect < Effects.Count && effects[effect].Count >= Effects[effect].Variations )
				++effect;
		}

		return effects;
	}

	private bool TryReadSample( int offset, IReadOnlyList<IReadOnlyList<TimeSpan>> bankDurations, out Sample sample )
	{
		sample = default;

		var index = BitConverter.ToInt32( _sfx, offset );
		var weight = BitConverter.ToUInt16( _sfx, offset + 4 );
		var spare = BitConverter.ToUInt16( _sfx, offset + 6 );
		var milliseconds = BitConverter.ToInt32( _sfx, offset + 8 );
		var bank = BitConverter.ToInt32( _sfx, offset + 12 );

		if ( spare != 0 || weight == 0 || milliseconds <= 0 )
			return false;

		if ( bank < 1 || bank > bankDurations.Count )
			return false;

		var samples = bankDurations[bank - 1];

		if ( index < 1 || index > samples.Count )
			return false;

		// The map's length and the sample's own disagree, so this is a tolerance rather than an
		// equality. The sample is always the longer, and by an amount that does not grow with its
		// length - by 26 to 83ms across every record in all thirty-one categories the game ships -
		// which is what an encoder's delay and the padding of its last frame would add. So the
		// allowance is a fixed amount, with 6% taking over on long samples. It used to be
		// 60ms, which turned away effect 639's record (288ms against 366) - and a record turned away
		// is not just lost, because the next one found then stands in for it, so every effect after
		// it in the category played the sample of the one after.
		var actual = samples[index - 1].TotalMilliseconds;

		if ( actual <= 0 || Math.Abs( milliseconds - actual ) > Math.Max( LengthSlackMilliseconds, actual * 0.06 ) )
			return false;

		sample = new Sample( bank - 1, index - 1, weight, TimeSpan.FromMilliseconds( milliseconds ) );
		return true;
	}

	private static byte[] ReadAll( string path )
	{
		try
		{
			using var stream = FileSystem.OpenRead( path );

			if ( stream == null )
				return Array.Empty<byte>();

			using var memory = new MemoryStream();
			stream.CopyTo( memory );

			return memory.ToArray();
		}
		catch
		{
			// A category that isn't there is normal - not every folder has every category - and
			// the caller finds out through IsValid.
			return Array.Empty<byte>();
		}
	}
}
