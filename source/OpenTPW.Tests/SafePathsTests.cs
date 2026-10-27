using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class SafePathsTests
{
	private static SafePaths Linux() => new( [("/home/kim/Games/TPWorld", SafePaths.GameFolderName), ("/home/kim", SafePaths.HomeName)], "kim", ignoreCase: false );

	private static SafePaths Windows() => new( [(@"C:\Users\Kim\Games\TPWorld", SafePaths.GameFolderName), (@"C:\Users\Kim", SafePaths.HomeName)], "Kim", ignoreCase: true );

	[TestMethod]
	public void TheGameFolderIsNamedAsTheGamesEvenInsideTheHomeFolder()
	{
		Assert.AreEqual( "[1:00] Game files: Theme Park World in <game folder> (--game)",
			Linux().Scrub( "[1:00] Game files: Theme Park World in /home/kim/Games/TPWorld (--game)" ) );
		Assert.AreEqual( "Saves: <game folder>/save/users", Linux().Scrub( "Saves: /home/kim/Games/TPWorld/save/users" ) );
	}

	[TestMethod]
	public void TheHomeFolderBecomesATilde()
	{
		Assert.AreEqual( "kept in ~/.local/share/OpenTPW/console.log", Linux().Scrub( "kept in /home/kim/.local/share/OpenTPW/console.log" ) );
		Assert.AreEqual( "at ~.", Linux().Scrub( "at /home/kim." ) );
	}

	[TestMethod]
	public void AFolderIsMatchedOnlyWhole()
	{
		// Another account's folder, whose name starts with this one's, is left as it is.
		Assert.AreEqual( "/home/kimberly/x", Linux().Scrub( "/home/kimberly/x" ) );
		Assert.AreEqual( "/home/kim.old/x", Linux().Scrub( "/home/kim.old/x" ) );
		Assert.AreEqual( "/home/kim_2", Linux().Scrub( "/home/kim_2" ) );
	}

	[TestMethod]
	public void TheAccountsNameIsCutWhereItIsAFolderInAnotherPath()
	{
		Assert.AreEqual( "/media/<user>/USB/TPW", Linux().Scrub( "/media/kim/USB/TPW" ) );
		Assert.AreEqual( "/run/media/<user>", Linux().Scrub( "/run/media/kim" ) );

		// Not where it is part of a word, or a word on its own.
		Assert.AreEqual( "/media/kimchi and kim said", Linux().Scrub( "/media/kimchi and kim said" ) );
	}

	[TestMethod]
	public void LinuxKeepsCaseAndWindowsDoesNot()
	{
		Assert.AreEqual( "/HOME/KIM/x", Linux().Scrub( "/HOME/KIM/x" ) );
		Assert.AreEqual( @"<game folder>\save", Windows().Scrub( @"c:\users\kim\games\tpworld\save" ) );
	}

	[TestMethod]
	public void WindowsPathsAreMatchedWithEitherSeparator()
	{
		Assert.AreEqual( "~/AppData/Roaming/OpenTPW", Windows().Scrub( "C:/Users/Kim/AppData/Roaming/OpenTPW" ) );
		Assert.AreEqual( @"~\AppData\Roaming\OpenTPW", Windows().Scrub( @"C:\Users\Kim\AppData\Roaming\OpenTPW" ) );
		Assert.AreEqual( @"D:\<user>\TPW", Windows().Scrub( @"D:\kim\TPW" ) );
	}

	[TestMethod]
	public void EveryPlaceInALineIsCut()
	{
		Assert.AreEqual( "<game folder> and ~/x and <game folder>/data",
			Linux().Scrub( "/home/kim/Games/TPWorld and /home/kim/x and /home/kim/Games/TPWorld/data" ) );
	}

	[TestMethod]
	public void NoFolderAndNoNameLeaveTheTextAlone()
	{
		var none = new SafePaths( [("", SafePaths.HomeName), ("/", SafePaths.HomeName)], "", ignoreCase: false );

		Assert.AreEqual( "/home/kim/x", none.Scrub( "/home/kim/x" ) );
	}

	[TestMethod]
	public void ThisMachinesHomeAndAccountAreCut()
	{
		var home = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
		var game = Path.Join( home, "SomeGame" );
		var scrubbed = SafePaths.ForThisMachine( game ).Scrub( $"in {game} and {Path.Join( home, "other" )}" );

		Assert.AreEqual( $"in {SafePaths.GameFolderName} and ~{Path.DirectorySeparatorChar}other", scrubbed );
		StringAssert.DoesNotMatch( scrubbed, new System.Text.RegularExpressions.Regex( System.Text.RegularExpressions.Regex.Escape( Environment.UserName ) ) );
	}

	[TestMethod]
	public void TheConsoleLogFileIsWrittenScrubbedAndTheTerminalIsNot()
	{
		var folder = Directory.CreateTempSubdirectory( "tpw-safepaths" ).FullName;

		try
		{
			var terminal = new StringWriter();

			using ( var sink = new ConsoleLog.Sink() )
			{
				var tee = new ConsoleLog.Tee( terminal, sink );

				tee.WriteLine( "held: /home/kim/Games/TPWorld" );
				Assert.IsTrue( sink.Open( [folder], "HEADER /home/kim", Linux().Scrub ) );
				tee.WriteLine( "after: /home/kim/Games/TPWorld/save" );
			}

			CollectionAssert.AreEqual( new[] { "HEADER ~", "held: <game folder>", "after: <game folder>/save" },
				File.ReadAllLines( Path.Join( folder, ConsoleLog.FileName ) ) );
			StringAssert.Contains( terminal.ToString(), "after: /home/kim/Games/TPWorld/save" );
		}
		finally { Directory.Delete( folder, true ); }
	}
}
