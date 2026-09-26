using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
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
	/// lines; the log line taken out logs none.
	/// </remarks>
	[TestMethod]
	public void AStringTableIsParsedOnce()
	{
		var table = Table( 2 );
		var (file, lines) = Parses( $"String table: 2 strings in {table.Length} bytes", () => new StringFile( new MemoryStream( table ) ) );

		Assert.AreEqual( 2, file.Entries.Length );
		Assert.AreEqual( "", file[1] );
		Assert.AreEqual( 1, lines, "one parse for one StringFile" );
	}

	/// <summary>
	/// <b>The interface's own table, parsed once.</b> <c>UITEXT.str</c> holds 474 strings in 17,076 bytes, and row
	/// 200 (<see cref="UIStrings.ParkName"/>) is "Park name".
	/// </summary>
	[TestMethod]
	public void TheInterfacesTextIsParsedOnce()
	{
		FileSystem = GameData.Required();

		var (file, lines) = Parses( "String table: 474 strings in 17076 bytes",
			() => new StringFile( "Language/English/UITEXT.str" ) );

		Assert.AreEqual( 474, file.Entries.Length );
		Assert.AreEqual( "Park name", file[(int)UIStrings.ParkName] );
		Assert.AreEqual( 1, lines, "one parse for one StringFile" );
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
