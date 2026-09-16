namespace OpenTPW;

public class SAMParser : BaseParser
{
	public SAMParser( string input ) : base( input ) { }

	public List<SettingsPair> Parse()
	{
		var settings = new List<SettingsPair>();

		while ( !EndOfFile() )
		{
			ConsumeWhitespace();

			if ( EndOfFile() )
				break;

			if ( NextChar() == '#' )
			{
				ConsumeComment();
			}
			else
			{
				ParseEntry( settings );
			}
		}

		return settings;
	}

	protected void ConsumeComment()
	{
		ConsumeWhile( c => c != '\n' );
	}

	/// <summary>
	/// How many fields one key may name. The original stops at sixteen and carries the string
	/// <c>"Too many fields have been specified"</c> for the seventeenth - at which point it refuses the
	/// whole file. This takes the sixteen it would have accepted instead, because failing a balance file
	/// outright takes the park down with it, and no file the game ships comes near the limit.
	/// </summary>
	private const int MostFieldsAKeyMayName = 16;

	/// <summary>
	/// One line, which defines <b>one setting per field its key names</b>.
	///
	/// <para>
	/// A key is a group, an optional subscript, and then one or more field names separated by dots, and
	/// the line supplies a value for each in order - so
	/// <c>PeepTypes[0].PreferredExcitement.StartingCash.BoredomThreshold  80 300 40</c> is three
	/// settings, and <c>PeepTypes[0].StartingCash</c> is 300. Reading it as a single key and a single
	/// value keeps the 80, drops the rest and reports nothing, which is how the peep constants came to
	/// look as though they were missing from a file that states them plainly.
	/// </para>
	/// <para>
	/// That also explains why the unmarked prose these files trail their values with is harmless: the
	/// loop takes exactly as many values as the key named fields and never looks at the rest of the
	/// line, so <c>PeepInfo.ExitLevel  120  starting value for the ExitLevel counter</c> reads 120 and
	/// stops. Only the first value may be looked for beyond the end of the line, which is what this
	/// parser has always done and is left alone.
	/// </para>
	/// </summary>
	protected void ParseEntry( List<SettingsPair> settings )
	{
		var key = ConsumeWhile( c => !char.IsWhiteSpace( c ) ).Trim();
		var fields = FieldsNamedBy( key );

		ConsumeWhitespace();

		for ( var field = 0; field < fields.Length; ++field )
		{
			if ( field > 0 )
			{
				ConsumeWhile( c => c is ' ' or '\t' );

				// A line supplying fewer values than its key names fields defines the ones it did
				// supply, rather than reaching into the line below for the remainder.
				if ( EndOfFile() || NextChar() is '\r' or '\n' or '#' )
					break;
			}

			var value = ConsumeWhile( c => !char.IsWhiteSpace( c ) && c != '#' ).Trim();

			if ( string.IsNullOrEmpty( fields[field] ) || string.IsNullOrEmpty( value ) )
				break;

			settings.Add( new SettingsPair { Key = fields[field], Value = value } );
		}

		ConsumeWhile( c => c != '\r' && c != '\n' );
	}

	/// <summary>
	/// The settings one key defines: its group, then one for each dot-separated field name after it.
	///
	/// <para>
	/// A key naming a single field - which is every key in the game bar sixteen lines - comes back
	/// exactly as it was written, subscript and all, so <c>PeepInfo.ExitLevel</c> and
	/// <c>Seasons[0].AvgWeatherQuality</c> are unchanged. The subscript needs no handling of its own
	/// because it sits inside the group, before the first dot.
	/// </para>
	/// </summary>
	private static string[] FieldsNamedBy( string key )
	{
		var group = key.IndexOf( '.' );

		if ( group < 0 )
			return [key];

		var names = key[(group + 1)..].Split( '.' );
		var count = names.Length < MostFieldsAKeyMayName ? names.Length : MostFieldsAKeyMayName;
		var fields = new string[count];

		for ( var i = 0; i < count; ++i )
			fields[i] = string.Concat( key[..group], ".", names[i] );

		return fields;
	}
}
