using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A row read bare (<see cref="Localization.Get"/>, <see cref="Localization.Text"/>, <see cref="Localization.Help"/>)
/// is its first text part; one with words after a parameter is counted where it is read, so a screen that shows it
/// without <see cref="Localization.Format(int, System.ValueTuple{int, string}[])"/> is named in the census.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LocalizationBareReadTests
{
	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <remarks>
	/// <b>Mutations:</b> the report taken out of the bare reader fails the counted rows; the report made for every row
	/// with a parameter fails 29's and 488's nought; the help table named UITEXT fails 148's name.
	/// </remarks>
	[TestMethod]
	public void ARowWithWordsAfterItsParameterIsCountedWhereItIsReadBare()
	{
		FileSystem = GameData.Required();
		Unimplemented.Forget();

		Localization.Text( 30 );
		Localization.Get( UIStrings.Duration );
		Localization.Help( 148 );
		Localization.Text( 29 );
		Localization.Text( 457 );
		Localization.Help( 488 );
		Localization.Format( 30, (0, "2") );
		Localization.FormatHelp( 148, (0, "Item") );

		Assert.AreEqual( 1, Times( "UITEXT_30_READ_BARE" ), "30's words after its parameter" );
		Assert.AreEqual( 1, Times( "UITEXT_428_READ_BARE" ), "Duration, row 428" );
		Assert.AreEqual( 1, Times( "UIHELPTEXT_148_READ_BARE" ), "the help table's own name" );
		Assert.AreEqual( 3, Unimplemented.Summary.Count, "29 and 488 end on an empty part, 457 has no parameter, and Format reads whole" );

		Unimplemented.Forget();
	}
}
