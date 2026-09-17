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
					// with it.</b> This was a bare Enum.Parse, which throws ArgumentException on anything
					// it does not recognise - and the one caller this class has is a text draw, so a
					// single mistyped token in a string file would have thrown straight out of rendering.
					// Nothing calls Parse today, so that was latent rather than live; it is guarded now
					// because the whole point of this class is to be wired up eventually, and a crash
					// waiting for its first caller is worse than one that has already happened.
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
