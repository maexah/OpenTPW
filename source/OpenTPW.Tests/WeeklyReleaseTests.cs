using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The weekly build's guard (<c>tools/weekly/release.py guard</c>, Q798): a zip must hold what the game and its
/// licences need, as well as nothing of the game's. It is run as the workflow runs it, over zips made here.
/// </summary>
[TestClass]
public class WeeklyReleaseTests
{
	private const string Top = "OpenTPW-weekly-test-1";

	/// <summary>A Linux build's entries, by the names the packager gives them; the shaders are the repository's own.</summary>
	private static List<string> Complete( string root, bool windows = false )
	{
		var entries = new List<string>
		{
			windows ? "OpenTPW.exe" : "OpenTPW",
			"OpenTPW.dll", "OpenTPW.Common.dll", "OpenTPW.Files.dll", "OpenTPW.pdb", "OpenTPW.deps.json", "OpenTPW.runtimeconfig.json", "OpenTPW.dll.config",
			windows ? "coreclr.dll" : "libcoreclr.so", windows ? "hostfxr.dll" : "libhostfxr.so", windows ? "hostpolicy.dll" : "libhostpolicy.so",
			windows ? "SDL2.dll" : "libSDL2-2.0.so", windows ? "shaderc_shared.dll" : "libshaderc_shared.so", windows ? "spirv-cross.dll" : "libspirv-cross.so",
			"System.Private.CoreLib.dll", "NeoVeldrid.dll",
			"README.txt", "LICENSE.md", "THIRD-PARTY-NOTICES.md",
			"content/fonts/Roboto-Medium.ttf", "content/fonts/LICENSE.txt",
		};

		var shaders = Path.Join( root, "content", "shaders" );

		entries.AddRange( Directory.GetFiles( shaders, "*", SearchOption.AllDirectories ).Select( f => "content/shaders/" + Path.GetRelativePath( shaders, f ).Replace( '\\', '/' ) ) );

		return entries;
	}

	private static string Zip( IEnumerable<string> entries )
	{
		var path = Path.Join( Directory.CreateTempSubdirectory( "tpw-weekly" ).FullName, $"{Top}-linux-x64.zip" );

		using ( var zip = ZipFile.Open( path, ZipArchiveMode.Create ) )
		{
			foreach ( var entry in entries )
				zip.CreateEntry( $"{Top}/{entry}" ).Open().Dispose();
		}

		return path;
	}

	/// <summary>The guard's exit code and what it printed, over a zip of these entries; the end of the test where there is no Python.</summary>
	private static (int Exit, string Output) Guard( IEnumerable<string> entries )
	{
		var zip = Zip( entries );

		try
		{
			var script = Path.Join( RepoFiles.Root(), "tools", "weekly", "release.py" );
			var result = RepoFiles.Run( "python3", script, "guard", zip ) ?? RepoFiles.Run( "python", script, "guard", zip );

			if ( result == null )
				Assert.Inconclusive( "skipped: no Python here" );

			return result!.Value;
		}
		finally { Directory.Delete( Path.GetDirectoryName( zip )!, true ); }
	}

	[TestMethod]
	public void AZipWithEverythingPassesOnBothSystems()
	{
		var root = RepoFiles.Root();

		Assert.AreEqual( 0, Guard( Complete( root ) ).Exit );
		Assert.AreEqual( 0, Guard( Complete( root, windows: true ) ).Exit );
	}

	/// <remarks><b>Mutation:</b> an empty REQUIRED in release.py passes this zip.</remarks>
	[TestMethod]
	public void AZipOfOnlyTheNotesIsRefused()
	{
		var (exit, output) = Guard( ["README.txt", "LICENSE.md", "THIRD-PARTY-NOTICES.md"] );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the program" );
		StringAssert.Contains( output, "missing: the shader 3d.shader" );
		StringAssert.Contains( output, "missing: the loading screen's font" );
	}

	[TestMethod]
	public void AZipWithoutTheFontIsRefused()
	{
		var (exit, output) = Guard( Complete( RepoFiles.Root() ).Where( e => e != "content/fonts/Roboto-Medium.ttf" ) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the loading screen's font" );
		Assert.AreEqual( 1, output.Split( "missing:" ).Length - 1, "only the font is missing: " + output );
	}

	[TestMethod]
	public void AZipWithoutTheFontsLicenceIsRefused()
	{
		var (exit, output) = Guard( Complete( RepoFiles.Root() ).Where( e => e != "content/fonts/LICENSE.txt" ) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the font's licence" );
	}

	[TestMethod]
	public void AZipWithoutTheNoticesIsRefused()
	{
		var (exit, output) = Guard( Complete( RepoFiles.Root() ).Where( e => e != "THIRD-PARTY-NOTICES.md" ) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the third-party notices" );
		Assert.AreEqual( 1, output.Split( "missing:" ).Length - 1, "only the notices are missing: " + output );
	}

	/// <remarks>REQUIRED names the shaders one by one, so a shader added to the repository and left off it is found here.</remarks>
	[TestMethod]
	public void EveryShaderInTheRepositoryIsRequired()
	{
		var root = RepoFiles.Root();
		var shaders = Complete( root ).Where( e => e.StartsWith( "content/shaders/" ) ).ToArray();

		Assert.IsTrue( shaders.Length >= 12, $"only {shaders.Length} shaders found" );

		foreach ( var shader in shaders )
		{
			var (exit, output) = Guard( Complete( root ).Where( e => e != shader ) );

			Assert.AreEqual( 1, exit, $"{shader} is not required: {output}" );
			StringAssert.Contains( output, "missing: the shader " + shader["content/shaders/".Length..] );
		}
	}

	[TestMethod]
	public void ANativeLibraryOrTheRuntimesCoreMissingIsRefused()
	{
		var root = RepoFiles.Root();

		foreach ( var name in new[] { "libSDL2-2.0.so", "libshaderc_shared.so", "libspirv-cross.so", "libcoreclr.so", "libhostfxr.so", "OpenTPW.deps.json", "OpenTPW.runtimeconfig.json" } )
			Assert.AreEqual( 1, Guard( Complete( root ).Where( e => e != name ) ).Exit, name );
	}

	/// <remarks><b>Mutation:</b> a packager that leaves the notices, the font's licence, the README or LICENSE.md out of the zip: the guard inside <c>package</c> refuses it.</remarks>
	[TestMethod]
	public void ThePackagerAddsTheNotesAndTheNoticesFromTheRepository()
	{
		var root = RepoFiles.Root();
		var publish = Directory.CreateTempSubdirectory( "tpw-publish" ).FullName;
		var output = Directory.CreateTempSubdirectory( "tpw-zips" ).FullName;

		try
		{
			// A publish folder holds the build and the shaders and the one font; the packager brings the rest.
			var fromRepository = new[] { "README.txt", "LICENSE.md", "THIRD-PARTY-NOTICES.md", "content/fonts/LICENSE.txt" };

			foreach ( var entry in Complete( root ).Where( e => !fromRepository.Contains( e ) ) )
			{
				var path = Path.Join( publish, entry );

				Directory.CreateDirectory( Path.GetDirectoryName( path )! );
				File.WriteAllText( path, entry );
			}

			var script = Path.Join( root, "tools", "weekly", "release.py" );
			var result = RepoFiles.Run( "python3", script, "package", publish, "weekly-test-1", "linux-x64", output ) ?? RepoFiles.Run( "python", script, "package", publish, "weekly-test-1", "linux-x64", output );

			if ( result == null )
				Assert.Inconclusive( "skipped: no Python here" );

			Assert.AreEqual( 0, result!.Value.Exit, result.Value.Output );

			using var zip = ZipFile.OpenRead( Path.Join( output, $"{Top}-linux-x64.zip" ) );
			var held = zip.Entries.Select( e => e.FullName ).ToArray();

			foreach ( var entry in fromRepository )
				CollectionAssert.Contains( held, $"{Top}/{entry}" );

			Assert.AreEqual( File.ReadAllText( Path.Join( root, "THIRD-PARTY-NOTICES.md" ) ), new StreamReader( zip.GetEntry( $"{Top}/THIRD-PARTY-NOTICES.md" )!.Open() ).ReadToEnd(), "the notices are the repository's own" );
		}
		finally
		{
			Directory.Delete( publish, true );
			Directory.Delete( output, true );
		}
	}

	[TestMethod]
	public void TheGuardsOtherRulesStillHold()
	{
		var root = RepoFiles.Root();

		Assert.AreEqual( 1, Guard( Complete( root ).Append( "data/levels/jungle/Easymode.TPWI" ) ).Exit, "an unlisted file is refused" );
		Assert.AreEqual( 1, Guard( Complete( root ).Append( "extra.wad" ) ).Exit, "one of the disc's file types is refused" );
		Assert.AreEqual( 1, Guard( Complete( root ).Append( "notes.txt" ) ).Exit, "a .txt other than the two ours is refused" );
	}
}
