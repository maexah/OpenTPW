using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The player's own folders (Q782): a new account has no data folder yet, and the runtime answers an empty string
/// for a folder that is not there unless it is told not to verify, which would put the cache in the working
/// directory. Linux's are measured here by moving <c>HOME</c>; Windows's and macOS's are the documented answers of
/// <c>Environment.GetFolderPath</c>, and no test of this file has run on either.
/// </summary>
[TestClass]
public class UserFoldersTests
{
	/// <summary>Runs <paramref name="body"/> with HOME an empty folder and the XDG variable as asked, then puts both back.</summary>
	private static void WithHome( string? xdgDataHome, Action<string> body )
	{
		var home = Directory.CreateTempSubdirectory( "tpw-userfolders" ).FullName;
		var oldHome = Environment.GetEnvironmentVariable( "HOME" );
		var oldXdg = Environment.GetEnvironmentVariable( "XDG_DATA_HOME" );

		try
		{
			Environment.SetEnvironmentVariable( "HOME", home );
			Environment.SetEnvironmentVariable( "XDG_DATA_HOME", xdgDataHome );
			body( home );
		}
		finally
		{
			Environment.SetEnvironmentVariable( "HOME", oldHome );
			Environment.SetEnvironmentVariable( "XDG_DATA_HOME", oldXdg );
			Directory.Delete( home, true );
		}
	}

	[TestMethod]
	public void AnAccountWithNoDataFolderYetGetsOneUnderItsHome()
	{
		if ( !OperatingSystem.IsLinux() )
			Assert.Inconclusive( "skipped: HOME moves the data folder on Linux only" );

		WithHome( null, home =>
		{
			Assert.IsFalse( Directory.Exists( Path.Join( home, ".local" ) ), "the account starts with no .local" );
			Assert.AreEqual( Path.Join( home, ".local", "share", "OpenTPW" ), UserFolders.Data() );
			Assert.AreEqual( Path.Join( home, ".local", "share", "OpenTPW", "cache" ), UserFolders.Cache() );
		} );
	}

	[TestMethod]
	public void AnXdgDataHomeThatIsNotThereYetIsTheDataFolderAndNeitherPathIsRelative()
	{
		if ( !OperatingSystem.IsLinux() )
			Assert.Inconclusive( "skipped: XDG_DATA_HOME moves the data folder on Linux only" );

		var xdg = Path.Join( Path.GetTempPath(), "tpw-userfolders-xdg-" + Guid.NewGuid().ToString( "N" ) );

		WithHome( xdg, _ =>
		{
			Assert.IsFalse( Directory.Exists( xdg ) );
			Assert.AreEqual( Path.Join( xdg, "OpenTPW" ), UserFolders.Data() );
			Assert.AreEqual( Path.Join( xdg, "OpenTPW", "cache" ), UserFolders.Cache() );
		} );
	}

	[TestMethod]
	public void TheCacheIsAlwaysAnAbsolutePathEndingInOpenTpwCache()
	{
		var cache = UserFolders.Cache();

		Assert.IsTrue( Path.IsPathRooted( cache ), cache );
		Assert.AreEqual( Path.Join( "OpenTPW", "cache" ), cache[^Path.Join( "OpenTPW", "cache" ).Length..] );
		Assert.IsTrue( Path.IsPathRooted( UserFolders.Data() ), UserFolders.Data() );
	}

	/// <remarks>.NET answers the system's Application Support folder on a Mac and reads no XDG variable (Environment.GetFolderPathCore.Unix.cs, TARGET_OSX). Not run on a Mac.</remarks>
	[TestMethod]
	public void OnAMacTheDataFolderIsApplicationSupportAndTheCacheIsInIt()
	{
		if ( !OperatingSystem.IsMacOS() )
			Assert.Inconclusive( "skipped: this is not a Mac" );

		var support = Path.Join( "Library", "Application Support", "OpenTPW" );

		Assert.IsTrue( UserFolders.Data().EndsWith( support ), UserFolders.Data() );
		Assert.AreEqual( Path.Join( UserFolders.Data(), "cache" ), UserFolders.Cache() );
	}

	[TestMethod]
	public void OnWindowsTheDataFolderIsAppDataAndTheCacheIsLocalAppData()
	{
		if ( !OperatingSystem.IsWindows() )
			Assert.Inconclusive( "skipped: this is not Windows" );

		Assert.AreEqual( Path.Join( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "OpenTPW" ), UserFolders.Data() );
		Assert.AreEqual( Path.Join( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "OpenTPW", "cache" ), UserFolders.Cache() );
	}
}
