using System.Globalization;

namespace OpenTPW;

/// <summary>
/// A park's balance file, which is not one file but a stack of them.
///
/// <para>
/// The original loads <c>data/levels/Standard.sam</c> first and then the theme's own
/// <c>data/levels/&lt;theme&gt;/Standard.sam</c> <b>over the top of it</b>, key by key, rather than
/// instead of it. That distinction is the whole reason this class exists: jungle's file names 94
/// keys and the global one names 362, so a loader that simply read the theme's file would lose the
/// 286 keys it never mentions - every peep constant, all the staff and ride economics - and would
/// do it silently, because nothing would be missing, only absent.
/// </para>
///
/// <para>
/// Of the 76 keys the two files share, only nine actually differ (four loan lenders, three staff
/// grades and two of the water start position's three axes). So the theme file is mostly a
/// restatement of the defaults with eighteen genuinely new keys on the end - the challenges, the
/// golden ticket targets and the advisor's costume.
/// </para>
///
/// <para>
/// <b>There is a THIRD layer, and it is asked for rather than assumed - <c>Easy_Standard.sam</c>.</b>
/// The original builds its name with <c>sprintf( dest, "%s\%s%s", theme, "Easy_", "Standard.sam" )</c>
/// and reads it only when its mode word <c>DAT_00fb3b7c</c> is <b>2</b>, which is Instant Action; a
/// theme that ships no such file gets the benign line "There is no Easy_standard.sam file for this
/// theme - This is not critical". So it is a real pass and it is genuinely conditional, which is why
/// <see cref="ParkBalance(string, bool)"/> takes the answer instead of guessing it.
/// </para>
/// <para>
/// <b>This paragraph used to say the file "overrides exactly four keys and introduces none - all four
/// LoanInfo[n].Lendername", and that was a description of the WRONG FILE.</b> Those four Lendername
/// keys are what <c>jungle/Standard.sam</c> overrides. Measured, <c>jungle/Easy_Standard.sam</c> names
/// 68 keys - all of which the global file already has, so it introduces none - and changes <b>33</b> of
/// them, among which are two that decide whether anybody gets into the park:
/// <c>PeepInfo.AveragePriceMultiplier</c> 1.25 to <b>1.5</b> and
/// <c>PeepInfo.ExpensivePriceMultiplier</c> 2.0 to <b>2.5</b>. The rest are
/// <c>BankAccountInfo.InitialCash</c> 50000 to 100000, all eight <c>LoanInfo[n].APRInPercent</c> to
/// nought, and the staff wages, researcher abilities and track costs.
/// </para>
/// <para>
/// <b>And the shipped park proves it is an easy-mode park, so this is not a preference.</b> Its economy
/// thing carries an APR of <b>nought on all eight loans</b> - which matches
/// <c>Easy_Standard.sam</c> exactly and matches the global file, whose APRs run 20, 20, 20, 20, 23, 22,
/// 18 and 21, <b>nowhere</b>. Its balance of 87,987 also sits above the global file's 50,000 starting
/// cash and below easy mode's 100,000. Two independent fields of the save agree, and the file is
/// literally called <c>Easymode.TPWI</c>.
/// </para>
/// </summary>
public sealed class ParkBalance
{
	private readonly Dictionary<string, string> _values = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>How many keys the stack came to, for the log.</summary>
	public int Count => _values.Count;

	/// <param name="easyMode">
	/// Whether to read the theme's <c>Easy_Standard.sam</c> over the top, as the original does when its
	/// mode word says Instant Action. <b>Defaulted to false so that asking for a park's balance without
	/// an opinion gets the standard game</b>, which is what every existing caller and test means; the one
	/// place that knows better says so.
	/// </param>
	public ParkBalance( string themeName, bool easyMode = false )
	{
		var theme = themeName.ToLowerInvariant();

		Layer( "levels/Standard.sam" );
		Layer( $"levels/{theme}/Standard.sam" );

		// Third and last, and only when asked - see the class remarks for the two fields of the shipped
		// save that show this park is one of these.
		if ( easyMode )
			Layer( $"levels/{theme}/Easy_Standard.sam" );
	}

	/// <summary>
	/// Reads one file over whatever is already here. A missing or unreadable layer is reported and
	/// stepped over rather than thrown: the global file existing and the theme's not is a broken
	/// installation, but it is one the park can still be looked at with.
	/// </summary>
	private void Layer( string path )
	{
		try
		{
			var file = new SettingsFile( path );

			foreach ( var pair in file.Entries )
				_values[pair.Key] = pair.Value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Balance: {path} would not load, so its keys keep whatever came before - {e.Message}" );
		}
	}

	public string? this[string key] => _values.TryGetValue( key, out var value ) ? value : null;

	public int Int( string key, int fallback = 0 )
		=> int.TryParse( this[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value )
			? value
			: fallback;

	public float Float( string key, float fallback = 0f )
		=> float.TryParse( this[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value )
			? value
			: fallback;

	/// <summary>
	/// A colour, which these files write as a plain decimal number packing <c>0xAARRGGBB</c>. The
	/// jungle's fog is 4774136 = 0x0048D8F8, so red 72, green 216, blue 248 - a pale sky blue, and
	/// within a few points of the constant the lobby fogs with, which is the check that the byte
	/// order is the right way round. Alpha is ignored: it is 0 on the fog values and 255 on the
	/// light ones, so it plainly is not opacity.
	/// </summary>
	public Vector3 Colour( string key, Vector3 fallback )
	{
		if ( !uint.TryParse( this[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var packed ) )
			return fallback;

		return new Vector3(
			((packed >> 16) & 0xFF) / 255f,
			((packed >> 8) & 0xFF) / 255f,
			(packed & 0xFF) / 255f );
	}

	/// <summary>
	/// A three-component vector written as three keys with <c>.X</c>, <c>.Y</c> and <c>.Z</c> on the
	/// end, which is how the files carry <c>LightNormal</c> and <c>WaterStartPos</c>. Returned in the
	/// file's own axes - the original has Y up - so a caller that wants engine space has to swap Y
	/// and Z itself, and should say so where it does.
	/// </summary>
	public Vector3 Vector( string key, Vector3 fallback )
		=> new(
			Float( $"{key}.X", fallback.X ),
			Float( $"{key}.Y", fallback.Y ),
			Float( $"{key}.Z", fallback.Z ) );
}
