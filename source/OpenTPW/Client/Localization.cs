using System.Text;

namespace OpenTPW;

public static class Localization
{
	private static StringFile UIStrings;
	private static StringFile HelpTexts;

	static Localization()
	{
		UIStrings = new StringFile( "Language/English/UITEXT.str" );
		HelpTexts = new StringFile( "Language/English/UIHELPTEXT.str" );
	}

	/// <summary>A line of the interface's text.</summary>
	public static string Get( UIStrings id ) => UIStrings[(int)id];

	/// <summary>
	/// A row of UIHELPTEXT.str - what the help bar says over a control, as the control's layout data
	/// gives it - or an empty string for a row that does not exist.
	/// </summary>
	public static string Help( int id ) => id >= 0 && id < HelpTexts.Entries.Length ? HelpTexts[id] : string.Empty;

	/// <summary>
	/// A row of UITEXT.str by number, for the rows <see cref="UIStrings"/> does not name.
	/// </summary>
	/// <remarks>
	/// <b>It exists so that an unnamed row need not be given an invented name.</b> The ride window's
	/// duration readout picks between a singular row and a plural one - 428 and 429 - and only the
	/// first is in the enum. Naming the second from a guess would put a word this project does not own
	/// into the interface, where a wrong one reads exactly like a right one.
	/// </remarks>
	public static string Text( int row ) => row >= 0 && row < UIStrings.Entries.Length ? UIStrings[row] : string.Empty;

	private class LocalizationParser : BaseParser
	{
		public LocalizationParser( string input ) : base( input ) { }

		public string Parse()
		{
			var sb = new StringBuilder();

			while ( !EndOfFile() )
			{
				sb.Append( ConsumeWhile( c => c != '#' ) );

				if ( !EndOfFile() )
				{
					ConsumeChar(); // #

					var key = ConsumeWhile( x => !char.IsWhiteSpace( x ) );
					key = key.Trim();

					// <b>An unknown token puts its own name on screen rather than taking the frame down
					// with it.</b> Enum.Parse would throw ArgumentException on anything it does not
					// recognise, and a string drawn as text would then throw straight out of rendering on
					// a single mistyped token. Nothing calls Parse: dead by CODE.
					if ( Enum.TryParse<UIStrings>( key, out var enumVal ) )
						sb.Append( UIStrings[(int)enumVal] );
					else
						sb.Append( '#' ).Append( key );
				}
			}

			return sb.ToString();
		}
	}

	public static string Parse( string str )
	{
		var parser = new LocalizationParser( str );
		return parser.Parse();
	}
}
