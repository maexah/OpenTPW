using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The contributor's guard (<c>tools/content_guard.py</c>, Q800): a commit that stages a file of the game's is refused,
/// and a file of ours is not. It is run as the pre-commit hook runs it, over named paths.
/// </summary>
[TestClass]
public class ContentGuardTests
{
	/// <summary>The guard's exit code and what it printed for these repo-relative paths; the end of the test where there is no Python.</summary>
	private static (int Exit, string Output) Guard( params string[] paths )
	{
		var script = Path.Join( RepoFiles.Root(), "tools", "content_guard.py" );
		var arguments = new[] { script, "paths" }.Concat( paths ).ToArray();
		var result = RepoFiles.Run( "python3", arguments, null, null ) ?? RepoFiles.Run( "python", arguments, null, null );

		if ( result == null )
			Assert.Inconclusive( "skipped: no Python here" );

		return result!.Value;
	}

	/// <remarks><b>Mutation:</b> <c>refusal()</c> in content_guard.py answering None for a type on the disc's list passes this.</remarks>
	[TestMethod]
	[DataRow( "levels/jungle.wad" )]
	[DataRow( "anything/at/all/Park.MD2" )]
	[DataRow( "textures/new.tga" )]
	[DataRow( "docs/notes.txt" )]
	[DataRow( "disc.iso" )]
	[DataRow( "jungle.tpws" )]
	[DataRow( "testme.exe" )]
	[DataRow( "TP.ICD" )]
	[DataRow( "users/x/jungle/restart.INTS" )]
	[DataRow( "Save/Config.tcf" )]
	[DataRow( "game/save/opentpw.cfg" )]
	[DataRow( "Save/notes.md" )]
	[DataRow( "SAVE/x" )]
	[DataRow( "game/Save/users/x.md" )]
	[DataRow( "extracted/Bouncy.RSE" )]
	[DataRow( "extracted/BIGLA___.TTF" )]
	[DataRow( "menus/MENU1.DAL" )]
	[DataRow( "docs/manual.pdf" )]
	public void AFileOfAGameTypeIsRefused( string path )
	{
		var (exit, output) = Guard( path );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "REFUSED " + path );
	}

	/// <remarks><b>Mutation:</b> refusing every .ttf or every Save/ folder fails this.</remarks>
	[TestMethod]
	public void OurFontsIconsAndSaveFormatSourcesAreLetThrough()
	{
		var (exit, output) = Guard( "content/fonts/Roboto-Medium.ttf", "content/icon.ico", "source/OpenTPW/icon.ico", "source/OpenTPW.Files/Formats/Save/SaveFile.cs", "source/OpenTPW.Files/Formats/Save/X.cs", "docs/saved.md", "save.md" );

		Assert.AreEqual( 0, exit, output );
	}

	/// <summary>Runs the hook or the guard in a scratch repository that holds copies of the guard, the disc's list, the hook, and the files.</summary>
	private static (int Exit, string Output) InScratchRepo( bool hook, params (string Name, bool Stage)[] files )
		=> InScratchRepo( hook, null, files );

	private static (int Exit, string Output) InScratchRepo( bool hook, Dictionary<string, string?>? environment, params (string Name, bool Stage)[] files )
	{
		var root = RepoFiles.Root();
		var folder = Path.Join( Path.GetTempPath(), "opentpw-guard-" + Guid.NewGuid().ToString( "N" ) );

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "tools", "hooks" ) );
			Directory.CreateDirectory( Path.Join( folder, "source", "OpenTPW", "Client" ) );
			File.Copy( Path.Join( root, "tools", "content_guard.py" ), Path.Join( folder, "tools", "content_guard.py" ) );
			File.Copy( Path.Join( root, "tools", "hooks", "pre-commit" ), Path.Join( folder, "tools", "hooks", "pre-commit" ) );
			File.Copy( Path.Join( root, "source", "OpenTPW", "Client", "DiscData.txt" ), Path.Join( folder, "source", "OpenTPW", "Client", "DiscData.txt" ) );

			if ( RepoFiles.Run( "git", new[] { "init", "-q" }, null, folder ) is not { Exit: 0 } )
				Assert.Inconclusive( "skipped: no git here" );

			foreach ( var (name, stage) in files )
			{
				File.WriteAllText( Path.Join( folder, name ), "x" );

				if ( stage )
					Assert.AreEqual( 0, RepoFiles.Run( "git", new[] { "add", name }, null, folder )!.Value.Exit );
			}

			var result = hook
				? RepoFiles.Run( "sh", new[] { "tools/hooks/pre-commit" }, environment, folder )
				: RepoFiles.Run( "python3", new[] { "tools/content_guard.py", "staged" }, null, folder ) ?? RepoFiles.Run( "python", new[] { "tools/content_guard.py", "staged" }, null, folder );

			if ( result == null )
				Assert.Inconclusive( "skipped: no Python or sh here" );

			return result!.Value;
		}
		finally
		{
			try { Directory.Delete( folder, true ); } catch ( IOException ) { }
		}
	}

	/// <remarks><b>Mutation:</b> dropping --cached from staged() reads the working tree's unstaged changes and checks nothing here; splitting its output on newline instead of NUL checks one joined name, so the .wad passes.</remarks>
	[TestMethod]
	public void StagedChecksEveryStagedFileAndOnlyTheStagedOnes()
	{
		var (exit, output) = InScratchRepo( false, ("a.wad", true), ("ok.md", true), ("unstaged.wad", false) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "2 path(s) checked, 1 refused" );
		StringAssert.Contains( output, "REFUSED a.wad" );
		Assert.IsFalse( output.Contains( "REFUSED unstaged.wad" ), output );
		Assert.IsFalse( output.Contains( "REFUSED ok.md" ), output );
	}

	/// <remarks><b>Mutation:</b> the hook running the guard without <c>|| exit 1</c> lets a commit with a .wad through.</remarks>
	[TestMethod]
	public void TheHookRefusesAStagedGameFileAndPassesACleanCommit()
	{
		Assert.AreEqual( 1, InScratchRepo( true, ("a.wad", true), ("ok.md", true) ).Exit );
		Assert.AreEqual( 0, InScratchRepo( true, ("ok.md", true) ).Exit );
	}

	/// <remarks><b>Mutation:</b> the hook's no-Python branch without its <c>exit 1</c> (a warning only) lets the commit through.</remarks>
	[TestMethod]
	public void TheHookRefusesWhenThereIsNoPythonAndSaysHowToSkipIt()
	{
		var bin = Directory.CreateTempSubdirectory( "tpw-nopython" ).FullName;

		try
		{
			foreach ( var tool in new[] { "git", "sh" } )
			{
				var found = ( Environment.GetEnvironmentVariable( "PATH" ) ?? "" ).Split( Path.PathSeparator )
					.Select( d => Path.Join( d, tool ) ).FirstOrDefault( File.Exists );

				if ( found == null )
				{
					Assert.Inconclusive( $"skipped: no {tool} here" );
					return;
				}

				File.CreateSymbolicLink( Path.Join( bin, tool ), found );
			}

			var (exit, output) = InScratchRepo( true, new Dictionary<string, string?> { ["PATH"] = bin }, ("ok.md", true) );

			Assert.AreEqual( 1, exit, output );
			StringAssert.Contains( output, "git commit --no-verify" );
			StringAssert.Contains( output, "Install Python" );
		}
		finally { Directory.Delete( bin, true ); }
	}

	/// <remarks><b>Mutation:</b> dropping the save/ rule from content_guard.py passes this.</remarks>
	[TestMethod]
	public void ThePlayersSaveFolderAndTheReferencePicturesAreRefusedWhateverTheirType()
	{
		var (exit, output) = Guard( "save/users/Alexah/notes.md", "content/ReferenceScreenshots/jungle.png" );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "REFUSED save/users/Alexah/notes.md" );
		StringAssert.Contains( output, "REFUSED content/ReferenceScreenshots/jungle.png" );
	}

	/// <remarks><b>Mutation:</b> an empty EXEMPT in content_guard.py refuses the icon; an EXEMPT that matches by type instead of whole path lets the second path through.</remarks>
	[TestMethod]
	public void OurOwnFilesWithAGameTypeAreLetThroughByNameAndOnlyByName()
	{
		Assert.AreEqual( 0, Guard( "content/icon.tga", "tools/weekly/README.txt", "README.md", "source/OpenTPW/Client/GameDir.cs", "save.md" ).Exit );
		Assert.AreEqual( 1, Guard( "content/other-icon.tga" ).Exit );
	}
}
