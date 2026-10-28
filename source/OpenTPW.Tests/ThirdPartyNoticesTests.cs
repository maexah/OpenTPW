using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <c>THIRD-PARTY-NOTICES.md</c> (Q798): the weekly zip carries it, so a package the build ships or a file in
/// <c>content/</c> that it does not name is a licence left out. Each row's first cell names what it covers.
/// </summary>
[TestClass]
public class ThirdPartyNoticesTests
{
	/// <summary>The projects whose packages reach the zip: the game, and the two it references.</summary>
	private static readonly string[] ShippedProjects = ["OpenTPW", "OpenTPW.Common", "OpenTPW.Files"];

	/// <summary>The `code` marks of every table row's first cell: package ids, and paths under content/ (with * and **).</summary>
	private static string[] Covered()
	{
		var notices = Path.Join( RepoFiles.Root(), "THIRD-PARTY-NOTICES.md" );

		Assert.IsTrue( File.Exists( notices ), "THIRD-PARTY-NOTICES.md is missing from the repository's root" );

		return File.ReadAllLines( notices )
			.Where( line => line.StartsWith( "| `" ) )
			.SelectMany( line => Regex.Matches( line.Split( '|' )[1], "`([^`]+)`" ).Select( m => m.Groups[1].Value ) )
			.ToArray();
	}

	/// <summary>Whether a covered path (with * for any file-name characters and ** for any path) names a file.</summary>
	public static bool PathCovers( string pattern, string file )
	{
		var expression = "^" + Regex.Escape( pattern ).Replace( @"\*\*", ".*" ).Replace( @"\*", "[^/]*" ) + "$";

		return Regex.IsMatch( file, expression );
	}

	private static IEnumerable<string> DirectPackages( string root )
		=> ShippedProjects.SelectMany( project => Regex.Matches( File.ReadAllText( Path.Join( root, "source", project, project + ".csproj" ) ), "<PackageReference Include=\"([^\"]+)\"" ).Select( m => m.Groups[1].Value ) );

	[TestMethod]
	public void EveryPackageTheShippedProjectsReferenceHasARow()
	{
		var covered = Covered();
		var missing = DirectPackages( RepoFiles.Root() ).Distinct().Where( id => !covered.Contains( id ) ).ToArray();

		Assert.AreEqual( 0, missing.Length, "no row in THIRD-PARTY-NOTICES.md for: " + string.Join( ", ", missing ) );
	}

	/// <remarks>The assets file lists every package the restore brought in, the ones a reference pulls in as well: that is the
	/// list that ships, and it is only there once the game has been restored (any build does).</remarks>
	[TestMethod]
	public void EveryPackageTheRestoreBroughtInHasARow()
	{
		var assets = Path.Join( RepoFiles.Root(), "source", "OpenTPW", "obj", "project.assets.json" );

		if ( !File.Exists( assets ) )
			Assert.Inconclusive( "skipped: the game has not been restored here" );

		using var json = JsonDocument.Parse( File.ReadAllText( assets ) );
		var covered = Covered();
		var missing = json.RootElement.GetProperty( "libraries" ).EnumerateObject()
			.Where( library => library.Value.GetProperty( "type" ).GetString() == "package" )
			.Select( library => library.Name.Split( '/' )[0] )
			.Where( id => !covered.Contains( id ) )
			.ToArray();

		Assert.AreEqual( 0, missing.Length, "no row in THIRD-PARTY-NOTICES.md for: " + string.Join( ", ", missing ) );
	}

	/// <remarks>The files git tracks, not the folder's: a disc image or screenshots left beside them are not the repository's.
	/// A copy of the tree without its <c>.git</c> (the mutation runs') has the folder's own files, which are the tracked ones.</remarks>
	[TestMethod]
	public void EveryTrackedFileUnderContentHasARow()
	{
		var root = RepoFiles.Root();
		var listing = RepoFiles.Run( "git", "-C", root, "ls-files", "--", "content" );
		var files = listing is { Exit: 0 } && listing.Value.Output.Length > 0
			? listing.Value.Output.Split( '\n', StringSplitOptions.RemoveEmptyEntries ).Select( f => f.Trim() ).ToArray()
			: Directory.GetFiles( Path.Join( root, "content" ), "*", SearchOption.AllDirectories ).Select( f => Path.GetRelativePath( root, f ).Replace( '\\', '/' ) ).ToArray();
		var covered = Covered().Where( c => c.StartsWith( "content/" ) ).ToArray();
		var missing = files.Where( file => !covered.Any( c => PathCovers( c, file ) ) ).ToArray();

		Assert.IsTrue( files.Length > 20, $"git listed only {files.Length} files under content/" );
		Assert.AreEqual( 0, missing.Length, "no row in THIRD-PARTY-NOTICES.md for: " + string.Join( ", ", missing ) );
	}

	[TestMethod]
	public void ARowCannotCoverMoreThanItsPathNames()
	{
		Assert.IsTrue( PathCovers( "content/fonts/Roboto-*.ttf", "content/fonts/Roboto-Thin.ttf" ) );
		Assert.IsFalse( PathCovers( "content/fonts/Roboto-*.ttf", "content/fonts/Other.ttf" ) );
		Assert.IsFalse( PathCovers( "content/fonts/*.ttf", "content/fonts/sub/Roboto-Thin.ttf" ), "* stops at a slash" );
		Assert.IsTrue( PathCovers( "content/shaders/**", "content/shaders/includes/3d.vert" ) );
		Assert.IsFalse( PathCovers( "content/shaders/**", "content/other/3d.vert" ) );
		Assert.IsFalse( PathCovers( "content/icon*", "contentXicon.png" ), "the dot and slash are not wildcards" );
	}

	[TestMethod]
	public void TheFontsLicenceSitsBesideTheFonts()
	{
		var folder = Path.Join( RepoFiles.Root(), "content", "fonts" );
		var text = File.ReadAllText( Path.Join( folder, "LICENSE.txt" ) );

		StringAssert.Contains( text, "Roboto" );
		StringAssert.Contains( text, "Copyright 2011 Google Inc." );
		StringAssert.Contains( text, "Apache License" );
		StringAssert.Contains( text, "Version 2.0, January 2004" );
	}
}
