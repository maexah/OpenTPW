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
/// <b>Easy_Standard.sam is deliberately not loaded.</b> The original reads it as a third pass, and
/// in the jungle it overrides exactly four keys and introduces none - all four
/// <c>LoanInfo[n].Lendername</c>, which name who lends the money rather than changing any number.
/// Reading it would be asserting the game is in easy mode, and nothing here knows that yet.
/// </para>
/// </summary>
public sealed class ParkBalance
{
	private readonly Dictionary<string, string> _values = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>How many keys the stack came to, for the log.</summary>
	public int Count => _values.Count;

	public ParkBalance( string themeName )
	{
		Layer( "levels/Standard.sam" );
		Layer( $"levels/{themeName.ToLowerInvariant()}/Standard.sam" );
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
