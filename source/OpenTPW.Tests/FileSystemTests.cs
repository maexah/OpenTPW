global using static OpenTPW.Common.GlobalNamespace;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Reading the game's own files, out of the folders and the archives they are packed into. These need a real
/// installation and are skipped where there is none - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class FileSystemTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	[TestMethod]
	public void TestRead()
	{
		Assert.IsTrue( data.ReadAllText( "Challenges.sam" ).Length > 0 );
	}

	[TestMethod]
	public void TestReadArchive()
	{
		Assert.IsTrue( data.ReadAllText( "levels/jungle/terrain/qickload.txt" ).Length > 0 );
	}

	[TestMethod]
	public void EnumerateFiles()
	{
		var files = data.GetFiles( "/levels" );
		var directories = data.GetDirectories( "/levels" );

		// Standard.sam and Online_Standard.sam sit loose in there, with a folder for each theme.
		Assert.IsTrue( files.Length > 0, "no files in /levels" );
		Assert.IsTrue( directories.Length > 0, "no directories in /levels" );
	}

	/// <summary>
	/// data\fonts.wad, the TrueType fonts the parks are lettered with. Its own table spells them
	/// BIGLA___.TTF, in capitals; asking for the extension either way round says "a font is in there" rather
	/// than "this copy of the game was packed in capitals".
	/// </summary>
	[TestMethod]
	public void EnumerateFilesWADArchive()
	{
		var files = data.GetFiles( "/fonts" );

		Assert.IsTrue( files.Length > 0, "nothing in /fonts" );
		Assert.IsTrue( files.Any( file => file.EndsWith( ".ttf", StringComparison.OrdinalIgnoreCase ) ),
			$"no TrueType font among: {string.Join( ", ", files )}" );
	}

	[TestMethod]
	public void LoadFromArchiveDirectory()
	{
		var file = data.ReadAllBytes( "/levels/jungle/terrain/textures/jgr_bas1.wct" );

		Assert.IsTrue( file.Length > 0 );
	}

	/// <summary>
	/// data\global\sound\AmbientHD.sdt. A name in a .sdt is a fixed sixteen bytes and a longer one is cut to
	/// fit, mid-extension, so only some of them still end in it - which is why this asks whether any does.
	/// </summary>
	[TestMethod]
	public void EnumerateFilesSDTArchive()
	{
		var files = data.GetFiles( "/global/sound/AmbientHD" );

		Assert.IsTrue( files.Length > 0, "nothing in /global/sound/AmbientHD" );
		Assert.IsTrue( files.Any( file => file.EndsWith( ".mp2", StringComparison.OrdinalIgnoreCase ) ),
			$"no MPEG stream among: {string.Join( ", ", files )}" );
	}
}
