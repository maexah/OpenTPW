using System.Text;

namespace OpenTPW;

public static class Localization
{
	private static StringFile UIStrings;
	private static StringFile HelpTexts;

	static Localization()
	{
		// Deviation: the original reads the american folder (registry Language 0x409, docs/exe/park-engine.md); this reads English. Filed as Q697.
		UIStrings = new StringFile( "Language/English/UITEXT.str" );
		HelpTexts = new StringFile( "Language/English/UIHELPTEXT.str" );
	}

	/// <summary>A line of the interface's text.</summary>
	public static string Get( UIStrings id ) => Bare( UIStrings, "UITEXT", (int)id );

	/// <summary>
	/// A row of UIHELPTEXT.str - what the help bar says over a control, as the control's layout data
	/// gives it - or an empty string for a row that does not exist.
	/// </summary>
	public static string Help( int id ) => id >= 0 && id < HelpTexts.Entries.Length ? Bare( HelpTexts, "UIHELPTEXT", id ) : string.Empty;

	/// <summary>A row of UIHELPTEXT.str with its parameters filled, as <see cref="Format(int, ValueTuple{int, string}[])"/> fills UITEXT's.</summary>
	public static string FormatHelp( int row, params (int Parameter, string Value)[] values ) => Format( HelpTexts, row, values );

	/// <summary>
	/// A row of UITEXT.str by number, for the rows <see cref="UIStrings"/> does not name.
	/// </summary>
	/// <remarks>
	/// <b>It exists so that an unnamed row need not be given an invented name.</b> A screen that picks between rows
	/// the enum does not name (the ride window's 365, 366, 388 and 389 status words, the staff window's labels) reads them by number.
	/// Naming a row from a guess would put a word this project does not own into the interface, where a wrong one reads exactly like a right one.
	/// </remarks>
	public static string Text( int row ) => row >= 0 && row < UIStrings.Entries.Length ? Bare( UIStrings, "UITEXT", row ) : string.Empty;

	/// <summary>
	/// A row's first text part, and a count, <c>UITEXT_30_READ_BARE</c>, where the row has words after a parameter
	/// that the first part leaves out: such a row is shown whole only through <see cref="Format(int, ValueTuple{int, string}[])"/>.
	/// </summary>
	private static string Bare( StringFile table, string name, int row )
	{
		if ( table.LosesWordsBare( row ) )
			Unimplemented.Report( $"{name}_{row}_READ_BARE" );

		return table[row];
	}

	/// <summary>
	/// A row of UITEXT.str with its parameters filled, as the original writes a save.s date (<c>docs/exe/saves.md</c>, "The list is the folder"): each
	/// parameter part is replaced by the value handed over for its number, and one handed no value by nothing.
	/// </summary>
	public static string Format( int row, params (int Parameter, string Value)[] values )
		=> Format( UIStrings, row, values );

	/// <summary>A row of any string table with its parameters filled, as <see cref="Format(int, ValueTuple{int, string}[])"/> fills UITEXT's.</summary>
	public static string Format( StringFile table, int row, params (int Parameter, string Value)[] values )
	{
		if ( row < 0 || row >= table.Parts.Length )
			return string.Empty;

		var text = new StringBuilder();

		foreach ( var part in table.Parts[row] )
		{
			if ( part.Text != null )
				text.Append( part.Text );
			else
				text.Append( values.FirstOrDefault( value => value.Parameter == part.Parameter ).Value );
		}

		return text.ToString();
	}

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
