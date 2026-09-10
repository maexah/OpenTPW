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
	public readonly record struct Effect( int Id, TimeSpan RepeatDelay );

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

	/// <summary>
	/// How far apart two lists have to be for the second to belong to a different effect - see
	/// <see cref="ReadSamples"/>. Within an effect the gap is 0, 16 or 24; between effects it is
	/// 42 or more.
	/// </summary>
	private const int NewEffectGap = 32;

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
				TimeSpan.FromMilliseconds( BitConverter.ToInt32( data, record + (EffectDelayField * 4) ) ) );
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
	/// An <b>effect</b> ends when the space before the next list is big enough to be a new
	/// effect's header rather than a continuation. Those two are well separated too: a list
	/// following another within the same effect starts 0, 16 or 24 bytes later, and a list
	/// starting a new effect starts 42, 58, 84, 168 or 210 bytes later.
	///
	/// The result is one entry per effect - hallow's rain, then its seven pairs of terrors and
	/// bats, then a blank, then its three bells - and it lines up with the effect table for all
	/// nine categories the lobby loads.
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

		var list = new List<Sample>();
		var offset = EffectTableOffset + (Effects.Count * EffectStride);
		var endOfLast = -1;

		while ( offset + SampleStride <= _sfx.Length )
		{
			if ( !TryReadSample( offset, bankDurations, out var sample ) )
			{
				offset++;
				continue;
			}

			// Starting a list: how far it is from the end of the one before says whether it
			// belongs to the effect that was being read or starts the next one.
			if ( list.Count == 0 && endOfLast >= 0 && offset - endOfLast >= NewEffectGap )
				effects.Add( new List<List<Sample>>() );

			if ( effects.Count == 0 )
				effects.Add( new List<List<Sample>>() );

			list.Add( sample );
			offset += SampleStride;

			if ( sample.Weight < Saturated )
				continue;

			effects[^1].Add( list );
			endOfLast = offset;
			list = new List<Sample>();
		}

		// An effect table can name more effects than the file has lists for - the global lobby
		// category names four and holds three - so the tail is padded rather than left short,
		// because callers index this by effect.
		while ( effects.Count < Effects.Count )
			effects.Add( new List<List<Sample>>() );

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

		// The map's length and the sample's own disagree by a frame or two - the map is rounded
		// down off the encoder's own count and the decoder pads the last frame - so this is a
		// tolerance rather than an equality. 6% is far tighter than the gap between any two
		// samples in a bank and far looser than the disagreement, which never exceeds 45ms.
		var actual = samples[index - 1].TotalMilliseconds;

		if ( actual <= 0 || Math.Abs( milliseconds - actual ) > Math.Max( 60.0, actual * 0.06 ) )
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
