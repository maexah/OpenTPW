using System;
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
	public void AFileOfAGameTypeIsRefused( string path )
	{
		var (exit, output) = Guard( path );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "REFUSED " + path );
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
