using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Finding the game's files on a filesystem that minds the difference between "Data" and "data".
///
/// <para>
/// The game's own files ask for paths spelled however whoever wrote them felt like spelling them - the sound
/// categories ask for "Sound\Sfx" where the folder is "sound", and one archive in the whole game is
/// data\global\Speech\lips.WAD, in capitals - and on Windows nobody ever noticed. None of this needs a copy of
/// the game: a couple of empty files in a temporary folder stand in for an installation, so it runs anywhere.
/// </para>
/// <para>
/// On Windows, and on a Mac's default filesystem, these pass without the fix because the filesystem folds case
/// itself. That is fine: what they pin is the behaviour, not how it is arrived at.
/// </para>
/// </summary>
[TestClass]
public class FileSystemCaseTests
{
	private string root = null!;
	private BaseFileSystem files = null!;

	[TestInitialize]
	public void BuildATinyInstallation()
	{
		root = Path.Combine( Path.GetTempPath(), $"opentpw-tests-{Guid.NewGuid():N}" );

		Directory.CreateDirectory( Path.Combine( root, "global", "Speech" ) );
		File.WriteAllText( Path.Combine( root, "Challenges.sam" ), "challenges" );

		// Empty on purpose: nothing here may open an archive, only find one.
		File.WriteAllBytes( Path.Combine( root, "global", "Speech", "lips.WAD" ), [] );

		files = new BaseFileSystem( root );
		files.RegisterArchiveHandler<WadArchive>( ".wad" );
	}

	[TestCleanup]
	public void TakeItAway() => Directory.Delete( root, true );

	/// <summary>data\Challenges.sam, asked for the three ways the game's own files ask for things.</summary>
	[TestMethod]
	public void AFileIsFoundWhateverCaseItIsAskedFor()
	{
		Assert.IsTrue( files.FileExists( "Challenges.sam" ), "as it is spelled" );
		Assert.IsTrue( files.FileExists( "challenges.sam" ), "all lower" );
		Assert.IsTrue( files.FileExists( "CHALLENGES.SAM" ), "all upper" );
		Assert.AreEqual( "challenges", files.ReadAllText( "challenges.SAM" ), "read back" );
	}

	/// <summary>
	/// The folders above it as well: the sound categories ask for "Sound\Sfx" where the folder on disk is
	/// "sound", so a part in the middle of a path has to resolve just as the last part does.
	/// </summary>
	[TestMethod]
	public void ADirectoryIsFoundWhateverCaseItIsAskedFor()
	{
		Assert.IsTrue( files.DirectoryExists( "global/Speech" ), "as it is spelled" );
		Assert.IsTrue( files.DirectoryExists( "Global/speech" ), "the other way round" );
	}

	/// <summary>
	/// An archive stands in for a directory of the same name, so data\global\Speech\lips.WAD - the advisor's
	/// lip sync, and the one archive in the game named in capitals - is only found if that lookup ignores case
	/// too. Registering ".WAD" as well as ".wad" is what used to stand in for this.
	/// </summary>
	[TestMethod]
	public void AnArchiveIsFoundWhateverCaseItsNameIsIn()
	{
		Assert.IsTrue( files.IsArchive( files.GetAbsolutePath( "global/Speech/lips/greet.raw" ) ),
			"lips.WAD was not recognised as an archive" );

		Assert.IsTrue( files.IsArchive( files.GetAbsolutePath( "Global/speech/LIPS/greet.raw" ) ),
			"nor with the folders above it in another case" );
	}

	/// <summary>
	/// And an archive is a directory as far as the rest of the game is concerned: listed among the
	/// directories, never among the files, whatever case its extension is in.
	/// </summary>
	[TestMethod]
	public void AnArchiveIsListedAsADirectoryAndNotAsAFile()
	{
		Assert.IsFalse( files.GetFiles( "/global/Speech" ).Any( file => Path.GetFileName( file ) == "lips.WAD" ),
			"lips.WAD was listed as a file" );

		Assert.IsTrue( files.GetDirectories( "/global/Speech" ).Any( directory => Path.GetFileName( directory ) == "lips" ),
			"lips.WAD was not listed as a directory" );
	}

	/// <summary>
	/// A name that is not there is left exactly as it was asked for, so a file being written keeps the name
	/// its caller chose rather than quietly taking some other spelling.
	/// </summary>
	[TestMethod]
	public void ANameThatIsNotThereIsKeptAsItWasAskedFor()
	{
		var wanted = Path.Combine( root, "global", "Speech", "NotHereYet.dat" );

		Assert.AreEqual( wanted, files.GetAbsolutePath( "global/Speech/NotHereYet.dat" ) );
	}

	/// <summary>
	/// Mapping somewhere that is not there must not answer by making it. It used to: the tests mapped
	/// "C:\Program Files (x86)\Bullfrog\Theme Park World\Data", the constructor made a directory of that whole
	/// name inside bin\, and six tests then read an empty one and failed.
	/// </summary>
	[TestMethod]
	public void MappingAFolderThatIsNotThereDoesNotMakeOne()
	{
		var missing = Path.Combine( Path.GetTempPath(), $"opentpw-tests-{Guid.NewGuid():N}" );

		Assert.ThrowsException<DirectoryNotFoundException>( () => new BaseFileSystem( missing ) );
		Assert.IsFalse( Directory.Exists( missing ), "mapping a folder that is not there made one" );
	}
}
