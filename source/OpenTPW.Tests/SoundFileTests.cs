using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>How a sound bank's bytes are read into memory.</summary>
[TestClass]
public class SoundFileTests
{
	/// <summary>
	/// A bank opened by path holds the file's bytes. The path constructor reaches the reader through
	/// <see cref="BaseFormat"/>, so this holds only while <see cref="SoundFile"/> overrides its
	/// <c>ReadFromStream</c> rather than hiding it. No game files: a folder of its own stands in.
	/// </summary>
	[TestMethod]
	public void ABankReadByPathHoldsItsBytes()
	{
		var folder = Directory.CreateTempSubdirectory( "opentpw-soundfile-" );
		var previous = FileSystem;

		try
		{
			var bytes = new byte[] { 40, 0, 0, 0, 1, 2, 3, 4 };
			File.WriteAllBytes( Path.Join( folder.FullName, "bank.bin" ), bytes );
			FileSystem = new BaseFileSystem( folder.FullName );

			var bank = new SoundFile( "bank.bin" );

			CollectionAssert.AreEqual( bytes, bank.buffer );
		}
		finally
		{
			FileSystem = previous;
			folder.Delete( recursive: true );
		}
	}
}
