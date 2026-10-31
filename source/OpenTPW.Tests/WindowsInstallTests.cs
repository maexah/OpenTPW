using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Where a Windows install is looked for (Q981). The registry is read only on Windows, so these take the parts on
/// plain strings; the read itself is confirmed on a Windows PC.
/// </summary>
[TestClass]
public class WindowsInstallTests
{
	private const string Box = @"C:\Program Files (x86)\SimTheme Park";

	[TestMethod]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f""C:\Program Files (x86)\SimTheme Park\Uninst.isu""", Box )]
	[DataRow( @"""C:\WINDOWS\IsUninst.exe"" -f""D:\Games\Theme Park World\Uninst.isu""", @"D:\Games\Theme Park World" )]
	[DataRow( @"C:\WINDOWS\ISUN0409.EXE -f""C:\Program Files (x86)\Bullfrog\Theme Park World\Uninst.isu"" -c""C:\x\uninst.dll""", @"C:\Program Files (x86)\Bullfrog\Theme Park World" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -F""C:\Program Files (x86)\SimTheme Park\Uninst.isu""", Box )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -fC:\Games\TPW\Uninst.isu", @"C:\Games\TPW" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -fC:\Games\TPW\Uninst.isu -cC:\Games\TPW\uninst.dll -BFLANG=1033", @"C:\Games\TPW" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f""C:\Program Files (x86)\SimTheme Park\Uninst.isu"" -c""C:\Program Files (x86)\SimTheme Park\uninst.dll"" -BFLANG=1033", Box )]
	[DataRow( @"""C:\Old -f tools\IsUninst.exe"" -f""C:\Games\TPW\Uninst.isu""", @"C:\Games\TPW" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f""D:\Uninst.isu""", @"D:\" )]
	public void TheFolderIsTheFolderOfTheFileTheSwitchNames( string text, string expected )
		=> Assert.AreEqual( expected, WindowsInstall.FolderOfUninstallString( text ) );

	[TestMethod]
	[DataRow( null )]
	[DataRow( "" )]
	[DataRow( "   " )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe" )]
	[DataRow( @"C:\Tools-for-all\Uninstall.exe /silent" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f""Uninst.isu""" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f""not a path""" )]
	[DataRow( @"C:\WINDOWS\IsUninst.exe -f""SimTheme Park\Uninst.isu""" )]
	public void NoSwitchOrNoPathGivesNothing( string? text )
		=> Assert.IsNull( WindowsInstall.FolderOfUninstallString( text ) );

	[TestMethod]
	public void TheLettersFOfAFolderNameAreNotTheSwitch()
	{
		// "-f" inside a word is not the switch; the real one after it is.
		Assert.AreEqual( Box, WindowsInstall.FolderOfUninstallString( @"C:\Tools\a-folder\un.exe -f""" + Box + @"\Uninst.isu""" ) );
		Assert.IsNull( WindowsInstall.FolderOfUninstallString( @"C:\Tools\a-folder\un.exe" ) );
	}

	[TestMethod]
	[DataRow( "Theme Park World", null, true )]
	[DataRow( "SimTheme Park", null, true )]
	[DataRow( "Sim Theme Park", null, true )]
	[DataRow( "{ABC-123}", "SimTheme Park", true )]
	[DataRow( "Theme Park World", "SimTheme Park", true )]
	[DataRow( "{ABC-123}", "THEME PARK WORLD", true )]
	[DataRow( "Theme Park Inc", "Theme Park Inc", false )]
	[DataRow( "Theme Hospital", "Theme Hospital", false )]
	[DataRow( "{ABC-123}", null, false )]
	[DataRow( "", "", false )]
	public void AnUninstallEntryIsTheGamesByEitherNameOnEitherValue( string key, string? display, bool expected )
		=> Assert.AreEqual( expected, WindowsInstall.IsTheGame( key, display ) );

	[TestMethod]
	public void TheBoxDefaultIsSimThemeParkInTheThirtyTwoBitProgramFiles()
	{
		Assert.AreEqual( Box, WindowsInstall.DefaultFolder( @"C:\Program Files (x86)" ) );
		Assert.AreEqual( @"D:\Programs (x86)\SimTheme Park", WindowsInstall.DefaultFolder( @"D:\Programs (x86)\" ) );
		Assert.AreEqual( Box, WindowsInstall.DefaultFolder( "" ), "a machine that cannot say has the usual one" );
	}

	[TestMethod]
	public void TheCandidatesPutTheRegistryBeforeBothDefaultsAndAfterEveryPlaceThePlayerNamed()
	{
		var where = GameDir.Candidates( ["--game", "/g"], [@"E:\Odd\Place"], Box ).Select( c => c.Where ).ToList();

		Assert.AreEqual( "--game", where[0] );

		var registry = where.IndexOf( "the Windows uninstall entry" );
		var original = where.IndexOf( "where the original installs itself" );
		var box = where.IndexOf( "where Sim Theme Park installs itself" );

		Assert.IsTrue( where.IndexOf( "the working directory" ) < registry, "the player's own places come first" );
		Assert.IsTrue( registry < original && original < box, "registry, then the original's default, then the box's" );
		Assert.AreEqual( where.Count - 1, box );
	}

	[TestMethod]
	public void EveryRegisteredFolderIsACandidateAndNoneWhenThereAreNone()
	{
		var some = GameDir.Candidates( [], [@"E:\A", @"E:\B"], Box ).Where( c => c.Where == "the Windows uninstall entry" ).Select( c => c.Path ).ToList();
		var none = GameDir.Candidates( [], new List<string>(), Box ).Where( c => c.Where == "the Windows uninstall entry" );

		CollectionAssert.AreEqual( new[] { @"E:\A", @"E:\B" }, some );
		Assert.AreEqual( 0, none.Count() );
	}

	[TestMethod]
	public void OffWindowsNoRegistryIsRead()
	{
		if ( System.OperatingSystem.IsWindows() )
			Assert.Inconclusive( "this is the Windows PC's check: RegisteredFolders reads the registry here" );

		// Without the IsWindows guard the look would throw here (no registry), be caught and warned: so none is the proof.
		var warnings = new List<string>();
		Logger.LogDelegate keep = ( severity, text ) => { if ( severity == Logger.Level.Warning ) warnings.Add( text ); };
		var old = Logger.OnLog;
		Logger.OnLog = keep;

		try
		{
			Assert.AreEqual( 0, WindowsInstall.RegisteredFolders().Count );
		}
		finally
		{
			Logger.OnLog = old;
		}

		Assert.AreEqual( 0, warnings.Count, string.Join( "; ", warnings ) );
	}

	/// <summary>
	/// The box's list through <c>Find</c> itself (Q981 F5, Q985): two registered folders are both said, and a folder that the
	/// uninstall entry and the box's default write in their own way (a trailing separator here) is said once, under the first.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Find</c> noting a missing path without comparing it (<c>tried.Add</c>), a comparison that
	/// keeps the separator, or a loop that names only the first registered folder, fails a step.</remarks>
	[TestMethod]
	public void TwoRegisteredFoldersAreBothSaidAndAFolderIsSaidOnceUnderItsFirstSource()
	{
		var old = System.Environment.GetEnvironmentVariable( "OPENTPW_GAME_PATH" );
		System.Environment.SetEnvironmentVariable( "OPENTPW_GAME_PATH", null );

		try
		{
			const string registered = "/q985-none/Sim Theme Park/";
			const string other = "/q985-none/Other";

			Assert.IsFalse( GameDir.Find( [], [registered, other], "/q985-none/Sim Theme Park" ) );

			var said = GameDir.Reasons.Where( line => line.Contains( "/q985-none/" ) ).ToList();
			Assert.AreEqual( 2, said.Count, string.Join( "\n", said ) );
			Assert.AreEqual( $"Game files: looked in {registered} (the Windows uninstall entry)", said[0] );
			Assert.AreEqual( $"Game files: looked in {other} (the Windows uninstall entry)", said[1] );
		}
		finally
		{
			System.Environment.SetEnvironmentVariable( "OPENTPW_GAME_PATH", old );
		}
	}

	/// <summary>A folder that exists, named by the uninstall entry and again, written another way, by the box's default, is tried and said once.</summary>
	/// <remarks><b>Mutation:</b> an existing folder tried again under a second source (the comparison dropped) says it twice.</remarks>
	[TestMethod]
	public void AFolderThatExistsIsTriedOnceWhateverSourceNamesItAgain()
	{
		var old = System.Environment.GetEnvironmentVariable( "OPENTPW_GAME_PATH" );
		System.Environment.SetEnvironmentVariable( "OPENTPW_GAME_PATH", null );
		var folder = System.IO.Path.Join( System.IO.Path.GetTempPath(), "q985-exists-" + System.Guid.NewGuid().ToString( "N" ) );
		System.IO.Directory.CreateDirectory( folder );

		try
		{
			Assert.IsFalse( GameDir.Find( [], [folder], folder + System.IO.Path.DirectorySeparatorChar ) );

			var said = GameDir.Reasons.Where( line => line.Contains( folder ) ).ToList();
			Assert.AreEqual( 1, said.Count, string.Join( "\n", said ) );
			StringAssert.EndsWith( said[0], "(the Windows uninstall entry)" );
		}
		finally
		{
			System.Environment.SetEnvironmentVariable( "OPENTPW_GAME_PATH", old );
			System.IO.Directory.Delete( folder );
		}
	}

	[TestMethod]
	public void TheSameFolderWrittenTwoWaysIsOnePlace()
	{
		var tried = new List<(string Where, string Path)> { ("the working directory", "/work/") };

		GameDir.NoteMissing( tried, "the Windows uninstall entry", "/a/b" );
		GameDir.NoteMissing( tried, "where Sim Theme Park installs itself", "/a/b/" );
		GameDir.NoteMissing( tried, "where the original installs itself", "/work" );
		GameDir.NoteMissing( tried, "where the original installs itself", "/a/c" );

		Assert.AreEqual( 3, tried.Count, string.Join( "\n", tried ) );
		Assert.AreEqual( "the Windows uninstall entry", tried[1].Where, "under the first source" );
		Assert.IsTrue( GameDir.SamePlace( "/a/b", "/a/b/" ) );
		Assert.IsFalse( GameDir.SamePlace( "/a/b", "/a/c" ) );
		Assert.AreEqual( !System.OperatingSystem.IsWindows() && !System.OperatingSystem.IsMacOS(), !GameDir.SamePlace( "/a/B", "/a/b" ), "case counts where the system's folders are case-sensitive" );
	}
}
