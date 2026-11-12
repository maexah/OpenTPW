using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A string table is parsed once for each <see cref="StringFile"/> made from it. The reader logs one line each time it
/// parses a table, and these count that line.
/// </summary>
[TestClass]
public class StringFileTests
{
	/// <summary>
	/// <b>Two empty strings, parsed once.</b> Neither string has a character, so the multibyte table is never
	/// opened and no game is needed.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>ReadFile()</c> called again at the end of the reader's <c>ReadFromStream</c> logs two
	/// lines; the log line taken out logs none; the character table opened before the first character fails it in a
	/// run without the game, which has no file system to open it through.
	/// </remarks>
	[TestMethod]
	public void AStringTableIsParsedOnce()
	{
		var table = Table( 2 );
		var (file, lines) = Parses( $"String table: 2 strings in {table.Length} bytes, the longest 0 characters", () => new StringFile( new MemoryStream( table ) ) );

		Assert.AreEqual( 2, file.Entries.Length );
		Assert.AreEqual( "", file[1] );
		Assert.AreEqual( 1, lines, "one parse for one StringFile" );
	}

	/// <summary>
	/// <b>The interface's own table, parsed once.</b> <c>UITEXT.str</c> holds 474 strings in 17,076 bytes, the longest
	/// row 417's 615 characters, and row 200 (<see cref="UIStrings.ParkName"/>) is "Park name".
	/// </summary>
	[TestMethod]
	public void TheInterfacesTextIsParsedOnce()
	{
		FileSystem = GameData.Required();

		var (file, lines) = Parses( "String table: 474 strings in 17076 bytes, the longest 615 characters",
			() => new StringFile( "Language/English/UITEXT.str" ) );

		Assert.AreEqual( 474, file.Entries.Length );
		Assert.AreEqual( "Park name", file[(int)UIStrings.ParkName] );
		Assert.AreEqual( 1, lines, "one parse for one StringFile" );
	}

	/// <summary>
	/// <b>A string over 255 characters is read whole.</b> A string's length is three bytes (FileFormats
	/// <c>strings.md</c>), and four shipped strings need more than the first, all in <c>UITEXT.str</c>: row 400
	/// (<see cref="UIStrings.Change"/>), 262 characters in English and 256 in american, and row 417
	/// (<see cref="UIStrings.SoftwareCopyright"/>), 615 and 614, each starting with the same words in both, apart from
	/// the american "(c)" for the English "©".
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the length taken from its first byte alone reads row 400 as "CHANGE" and as empty, and row
	/// 417 cut to 103 and 102 characters. No shipped string reaches 65,536 characters, so the third byte is
	/// <see cref="AStringsLengthTakesAllThreeBytes"/>'s.
	/// </remarks>
	[TestMethod]
	[DataRow( "English", 262, 615, "©" )]
	[DataRow( "american", 256, 614, "(c)" )]
	public void AStringOver255CharactersIsReadWhole( string language, int change, int copyright, string sign )
	{
		FileSystem = GameData.Required();

		var file = new StringFile( $"Language/{language}/UITEXT.str" );

		Assert.AreEqual( change, file[(int)UIStrings.Change].Length );
		Assert.AreEqual( copyright, file[(int)UIStrings.SoftwareCopyright].Length );

		Assert.IsTrue( file[(int)UIStrings.Change].StartsWith( "CHANGE SCREEN RESOLUTION\n\n" ) );
		Assert.IsTrue( file[(int)UIStrings.SoftwareCopyright].StartsWith( $"Software {sign} 1999 Bullfrog Productions Ltd." ) );

		if ( language != "English" )
			return;

		Assert.IsTrue( file[(int)UIStrings.Change].EndsWith( " your original setting will be restored." ) );
		Assert.IsTrue( file[(int)UIStrings.SoftwareCopyright].EndsWith( "gistered trademarks of QSound Labs, Inc." ) );
	}

	/// <summary>
	/// <b>Each language folder's tables are decoded through that folder's own character table.</b> The american
	/// <c>MBToUni.dat</c> has no ’, so its indices past it are one lower than the English ones (FileFormats
	/// <c>strings.md</c>): through the English table the american <c>THEMENAMES.str</c> reads "Mptu Ljohepn". Through
	/// its own it reads the four theme names, named with either separator, and the English table, read after it, still
	/// reads its own. Row 444 of the american <c>UIHELPTEXT.str</c> is the queue's help row the original showed,
	/// "Left-click to edit this line", where the English one says "queue".
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> every table through the English character table reads "Mptu Ljohepn"; one table kept for every
	/// folder, the first opened, reads one of the two folders shifted; a backslash left as it is finds no table.
	/// </remarks>
	[TestMethod]
	public void EachFolderIsDecodedThroughItsOwnCharacterTable()
	{
		FileSystem = GameData.Required();

		CollectionAssert.AreEqual( new[] { "Lost Kingdom", "Halloween World", "Wonder Land", "Space Zone" },
			new StringFile( "Language/american/THEMENAMES.str" ).Entries );
		Assert.AreEqual( "Lost Kingdom", new StringFile( "Language/English/THEMENAMES.str" )[0] );
		Assert.AreEqual( "Lost Kingdom", new StringFile( "Language\\american\\THEMENAMES.str" )[0], "a path the file system takes with backslashes" );
		Assert.AreEqual( "Left-click to edit this line", new StringFile( "Language/american/UIHELPTEXT.str" )[444] );
		Assert.AreEqual( "Left-click to edit this queue", new StringFile( "Language/English/UIHELPTEXT.str" )[444] );
	}

	/// <summary>
	/// <b>A table read from a stream has no folder, and is decoded through the English character table.</b> The
	/// american <c>THEMENAMES.str</c>'s bytes handed over as a stream read "Mptu Ljohepn".
	/// </summary>
	[TestMethod]
	public void AStreamIsDecodedThroughTheEnglishCharacterTable()
	{
		FileSystem = GameData.Required();

		var bytes = File.ReadAllBytes( Path.Combine( GameDir.Data, "Language", "american", "THEMENAMES.str" ) );

		Assert.AreEqual( "Mptu Ljohepn", new StringFile( new MemoryStream( bytes ) )[0] );
	}

	/// <summary>
	/// <b>A string's length takes all three bytes.</b> A made-up table of one string 65,537 characters long, its
	/// length <c>01 00 01</c>, and a second of one character: a length of one byte or two reads the first as one
	/// character.
	/// </summary>
	[TestMethod]
	public void AStringsLengthTakesAllThreeBytes()
	{
		FileSystem = GameData.Required();

		const int length = 0x10001;
		var padded = (length + 3) & ~3;

		using var bytes = new MemoryStream();
		using var writer = new BinaryWriter( bytes );

		writer.Write( Encoding.ASCII.GetBytes( "BFST" ) );
		writer.Write( 0 );
		writer.Write( 2 );
		writer.Write( 8 );
		writer.Write( 8 + 4 + padded + 4 );
		writer.Write( 0x01 | (length << 8) );
		writer.Write( Enumerable.Repeat( (byte)2, length ).Concat( new byte[padded - length + 4] ).ToArray() );
		writer.Write( 0x01 | (1 << 8) );
		writer.Write( new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 } );

		var file = new StringFile( new MemoryStream( bytes.ToArray() ) );

		Assert.AreEqual( length, file[0].Length );
		Assert.AreEqual( 1, file[1].Length );
	}

	/// <summary>
	/// <b>Every row of every shipped table reads as its first text part.</b> The 4,730 rows of the 42 <c>.str</c>
	/// files, read by <see cref="StringFile.Entries"/>, each equal the text of the first of their
	/// <see cref="StringFile.Parts"/>, which takes a part's whole three-byte value.
	/// </summary>
	[TestMethod]
	public void EveryShippedStringIsItsFirstTextPart()
	{
		FileSystem = GameData.Required();

		var files = Directory.GetFiles( Path.Combine( GameDir.Data, "Language" ), "*.str", SearchOption.AllDirectories );
		var rows = 0;

		foreach ( var path in files )
		{
			var file = new StringFile( Path.GetRelativePath( GameDir.Data, path ) );

			for ( var row = 0; row < file.Entries.Length; ++row, ++rows )
				Assert.AreEqual( file.Parts[row].FirstOrDefault().Text ?? "", file[row], $"{path} row {row}" );
		}

		Assert.AreEqual( 42, files.Length );
		Assert.AreEqual( 4730, rows );
	}

	/// <summary>
	/// <b>The rows with words after a parameter, which their first text part leaves out</b>, in both interface tables
	/// of both English folders. UITEXT's 29, 182 and 447 end on an empty text part after their parameter and lose
	/// nothing; 446, 448 and 460 have their words between two parameters.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> any row with a parameter counted (29 and 182 join); only the last part looked at (446, 448 and
	/// 460 drop out); an empty text part counted (29 joins); the parts before the parameter looked at too (every row with
	/// a text part joins).
	/// </remarks>
	[TestMethod]
	[DataRow( "English" )]
	[DataRow( "american" )]
	public void TheRowsThatLoseWordsBareAreTheOnesWithTextAfterAParameter( string language )
	{
		FileSystem = GameData.Required();

		int[] uiText = [30, 181, 183, 205, 213, 215, 399, 428, 429, 433, 434, 435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 446, 448, 451, 452, 453, 454, 455, 460, 461, 463];
		int[] helpText = [148, 149, 150, 547, 548, 549, 550, 551, 553, 555, 580, 581];

		CollectionAssert.AreEqual( uiText, Losing( new StringFile( $"Language/{language}/UITEXT.str" ) ) );
		CollectionAssert.AreEqual( helpText, Losing( new StringFile( $"Language/{language}/UIHELPTEXT.str" ) ) );

		static int[] Losing( StringFile file ) => Enumerable.Range( 0, file.Entries.Length ).Where( file.LosesWordsBare ).ToArray();
	}

	/// <summary>Makes a file with <paramref name="read"/> and counts the log lines that say exactly <paramref name="line"/>.</summary>
	private static (StringFile File, int Lines) Parses( string line, Func<StringFile> read )
	{
		var previousLog = Log;
		var lines = 0;
		Logger.LogDelegate count = ( severity, text ) =>
		{
			if ( text == line )
				++lines;
		};

		Log ??= new();
		Logger.OnLog += count;

		try
		{
			return (read(), lines);
		}
		finally
		{
			Logger.OnLog -= count;
			Log = previousLog;
		}
	}

	/// <summary>
	/// A BFST table of <paramref name="strings"/> empty strings: the magic, a word the reader skips, the count, one
	/// offset a string counted from the end of the count, then each string's <c>01</c> and a length of nought.
	/// </summary>
	private static byte[] Table( int strings )
	{
		using var bytes = new MemoryStream();
		using var writer = new BinaryWriter( bytes );

		writer.Write( Encoding.ASCII.GetBytes( "BFST" ) );
		writer.Write( 0 );
		writer.Write( strings );

		for ( var i = 0; i < strings; ++i )
			writer.Write( (4 * strings) + (4 * i) );

		for ( var i = 0; i < strings; ++i )
			writer.Write( 0x01 );

		return bytes.ToArray();
	}
}
