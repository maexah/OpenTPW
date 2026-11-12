using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A player's folder deleted by <see cref="SaveFolder.DeletePlayer"/> is dropped from the save folder's remembered names
/// (<see cref="BaseFileSystem.Changed"/>), so a player made again in the slot under another case keeps the case asked for.
/// </summary>
[TestClass]
[DoNotParallelize]
public class SaveFolderDeleteTests
{
	private string root = null!;
	private BaseFileSystem? oldSaves;

	[TestInitialize]
	public void MakeFolder()
	{
		Log ??= new();
		oldSaves = SaveFileSystemOrNull;
		root = Directory.CreateTempSubdirectory( "opentpw-delete-" ).FullName;
		Directory.CreateDirectory( Path.Combine( root, "users", "1Kim", "jungle" ) );
		SaveFileSystem = new BaseFileSystem( root );
	}

	[TestCleanup]
	public void Restore()
	{
		SaveFileSystemOrNull = oldSaves;
		Directory.Delete( root, true );
	}

	/// <summary>"Kim" deleted from slot 1, then "KIM" made there: the folder is <c>1KIM</c>, not the gone <c>1Kim</c>.</summary>
	[TestMethod]
	public void ANameMadeAgainInAnotherCaseKeepsItsCase()
	{
		// The folder was looked up before it went, as the front end's scan and the player's own reads do.
		Assert.IsTrue( SaveFileSystem.GetAbsolutePath( "users/1KIM" ).EndsWith( "1Kim" ), "the listing knows the folder first" );

		SaveFolder.DeletePlayer( 0, "Kim" );

		Assert.IsFalse( Directory.Exists( Path.Combine( root, "users", "1Kim" ) ), "the folder is gone" );

		var made = SaveFileSystem.GetAbsolutePath( "users/1KIM" );
		Directory.CreateDirectory( made );

		Assert.AreEqual( "1KIM", Path.GetFileName( made ) );
		Assert.AreEqual( "1KIM", Directory.GetDirectories( Path.Combine( root, "users" ) ).Select( Path.GetFileName ).Single() );
	}
}
