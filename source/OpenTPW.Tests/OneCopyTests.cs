using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// One copy at a time on one game folder (Q995), as the original's 0x0045f640 lets one copy run: a second take on a folder
/// is refused, a take on another folder is not. Each test names folders of its own, so a copy of the game running on this
/// machine, or another run of the suite, is never one of them. Two processes are shown in the running game (STATUS).
/// </summary>
[TestClass]
public class OneCopyTests
{
	private static string Folder( string what ) => Path.Join( Path.GetTempPath(), $"opentpw-onecopy-{Guid.NewGuid():N}", what );

	[TestMethod]
	public void ASecondCopyOnTheSameFolderIsRefused()
	{
		var folder = Folder( "game" );

		using var first = OneCopy.Open( folder, out var firstUnknown );
		using var second = OneCopy.Open( folder, out var secondUnknown );

		Assert.IsNotNull( first );
		Assert.IsNull( firstUnknown );
		Assert.IsNull( second, "the second copy on one folder must be refused" );
		Assert.IsNull( secondUnknown, "a refusal is a known answer, not a lock that cannot be told" );
	}

	[TestMethod]
	public void ACopyOnAnotherFolderRunsBesideIt()
	{
		var root = Folder( string.Empty );

		using var first = OneCopy.Open( Path.Join( root, "lane1" ), out _ );
		using var second = OneCopy.Open( Path.Join( root, "lane2" ), out var unknown );

		Assert.IsNotNull( first );
		Assert.IsNotNull( second, "a lock on one folder must not stop a copy on another (never machine-wide)" );
		Assert.IsNull( unknown );
	}

	[TestMethod]
	public void AnotherSpellingOfTheSameFolderIsTheSameLock()
	{
		var folder = Folder( "game" );

		using var first = OneCopy.Open( folder, out _ );
		using var trailing = OneCopy.Open( folder + Path.DirectorySeparatorChar, out _ );
		using var dotted = OneCopy.Open( Path.Join( folder, ".", "..", "game" ), out _ );

		Assert.IsNotNull( first );
		Assert.IsNull( trailing, "a closing separator is the same folder" );
		Assert.IsNull( dotted, "a path through . and .. is the same folder" );
	}

	/// <summary>A real folder under a fresh temporary root, made on disk, for the link tests; the root is answered for clean-up.</summary>
	private static (string Root, string Game) RealFolder()
	{
		var root = Folder( string.Empty );
		var game = Path.Join( root, "real", "game" );
		Directory.CreateDirectory( game );
		return (root, game);
	}

	/// <summary>A symbolic link at <paramref name="at"/> to <paramref name="to"/>; false where this system will not make one.</summary>
	private static bool Link( string at, string to )
	{
		try
		{
			Directory.CreateSymbolicLink( at, to );
			return true;
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException )
		{
			return false;
		}
	}

	[TestMethod]
	public void ALinkToTheFolderIsTheSameLock()
	{
		var (root, game) = RealFolder();

		try
		{
			var link = Path.Join( root, "link" );

			if ( !Link( link, game ) )
				Assert.Inconclusive( "this system will not make a symbolic link" );

			using var first = OneCopy.Open( game, out _ );
			using var second = OneCopy.Open( link, out var unknown );

			Assert.IsNotNull( first );
			Assert.IsNull( second, "a copy started through a link to the folder must be refused" );
			Assert.IsNull( unknown );
			Assert.AreEqual( OneCopy.Name( game ), OneCopy.Name( link + Path.DirectorySeparatorChar ) );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	[TestMethod]
	public void ALinkPartWayAlongThePathIsFollowed()
	{
		var (root, game) = RealFolder();

		try
		{
			// The link is the parent; the last component, "game", is a plain folder, so resolving only the last misses it.
			var parent = Path.Join( root, "parent" );

			if ( !Link( parent, Path.Join( root, "real" ) ) )
				Assert.Inconclusive( "this system will not make a symbolic link" );

			Assert.AreEqual( OneCopy.Name( game ), OneCopy.Name( Path.Join( parent, "game" ) ) );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	[TestMethod]
	public void ARelativeLinkThroughAnotherLinkIsFollowedToTheEnd()
	{
		var (root, game) = RealFolder();

		try
		{
			// outer -> "inner" (relative, beside it), inner -> real: two hops, the first one relative.
			var inner = Path.Join( root, "inner" );
			var outer = Path.Join( root, "outer" );

			if ( !Link( inner, Path.Join( root, "real" ) ) || !Link( outer, "inner" ) )
				Assert.Inconclusive( "this system will not make a symbolic link" );

			// Both sides through WithoutLinks: the temporary folder may itself lie under a link (macOS's /var).
			var followed = OneCopy.WithoutLinks( Path.Join( outer, "game" ) );

			Assert.AreEqual( OneCopy.WithoutLinks( game ), followed );
			StringAssert.EndsWith( followed, Path.Join( "real", "game" ) );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	/// <summary>
	/// A copy of a build before Q1174, which named a folder as spelled, still holds the spelled name of a folder with a
	/// link along its path: a copy started there now is refused, and one started first takes that name too.
	/// </summary>
	[TestMethod]
	public void AnOlderCopyHoldingTheSpelledNameRefusesThisOne()
	{
		var (root, game) = RealFolder();

		try
		{
			var link = Path.Join( root, "link" );

			if ( !Link( link, game ) )
				Assert.Inconclusive( "this system will not make a symbolic link" );

			Assert.AreNotEqual( OneCopy.Name( link ), OneCopy.SpelledName( link ), "the link makes the two names differ" );

			using ( var older = new System.Threading.Mutex( false, OneCopy.SpelledName( link ), out var createdNew ) )
			{
				Assert.IsTrue( createdNew );

				using var refused = OneCopy.Open( link, out var unknown );

				Assert.IsNull( refused, "an older copy on the folder as spelled must refuse this one" );
				Assert.IsNull( unknown );
			}

			using var first = OneCopy.Open( link, out _ );

			Assert.IsNotNull( first );

			using var older2 = new System.Threading.Mutex( false, OneCopy.SpelledName( link ), out var madeNew );

			Assert.IsFalse( madeNew, "this copy holds the spelled name too" );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	[TestMethod]
	public void AFolderBesideTheLinkedOneIsStillAnotherLock()
	{
		var (root, game) = RealFolder();

		try
		{
			var other = Path.Join( root, "real", "other" );
			Directory.CreateDirectory( other );
			var link = Path.Join( root, "link" );

			if ( !Link( link, game ) )
				Assert.Inconclusive( "this system will not make a symbolic link" );

			Assert.AreNotEqual( OneCopy.Name( other ), OneCopy.Name( link ) );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	/// <summary>
	/// A loop of links does not exist to <see cref="DirectoryInfo"/> (its stat fails), so the walk ends at it as at any
	/// missing component and the path is named as spelled; the cap on links followed is a guard this does not reach.
	/// </summary>
	[TestMethod]
	public void ALoopOfLinksDoesNotExistSoTheWalkEndsThere()
	{
		var root = Folder( string.Empty );
		Directory.CreateDirectory( root );

		try
		{
			var a = Path.Join( root, "a" );
			var b = Path.Join( root, "b" );

			if ( !Link( a, b ) || !Link( b, a ) )
				Assert.Inconclusive( "this system will not make a symbolic link" );

			StringAssert.StartsWith( OneCopy.Name( Path.Join( a, "game" ) ), OneCopy.Prefix );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}

	[TestMethod]
	public void OnceTheFirstCopyEndsTheFolderIsFreeAgain()
	{
		var folder = Folder( "game" );

		OneCopy.Open( folder, out _ )!.Dispose();

		using var again = OneCopy.Open( folder, out _ );

		Assert.IsNotNull( again );
	}

	[TestMethod]
	public void CaseCountsInTheNameOnlyWhereTheSystemsNamesDo()
	{
		Assert.AreEqual( OneCopy.Name( "/Games/TPW", ignoreCase: true ), OneCopy.Name( "/games/tpw", ignoreCase: true ) );
		Assert.AreNotEqual( OneCopy.Name( "/Games/TPW", ignoreCase: false ), OneCopy.Name( "/games/tpw", ignoreCase: false ) );
	}

	[TestMethod]
	public void TheNameIsTheGlobalPrefixAndAHashWithNoSeparator()
	{
		var name = OneCopy.Name( Folder( "a/very/deep/game/folder" ) );

		StringAssert.StartsWith( name, @"Global\OpenTPW.OneCopy." );
		Assert.AreEqual( OneCopy.Prefix.Length + 32, name.Length );
		Assert.IsFalse( name[OneCopy.Prefix.Length..].Contains( '/' ) || name[OneCopy.Prefix.Length..].Contains( '\\' ) );
	}

	[TestMethod]
	public void TakeHoldsTheFolderForTheRestOfTheRun()
	{
		var folder = Folder( "game" );

		try
		{
			Assert.IsTrue( OneCopy.Take( folder ) );

			// Held by the run, not by a handle the caller could drop: the next take is refused.
			GC.Collect();
			GC.WaitForPendingFinalizers();

			Assert.IsFalse( OneCopy.Take( folder ) );
		}
		finally
		{
			OneCopy.ReleaseForTest();
		}

		using var after = OneCopy.Open( folder, out _ );
		Assert.IsNotNull( after );
	}

	[TestMethod]
	public void TheRefusalNamesTheFolderAndKeepsNoLog()
	{
		var text = StartFailure.Compose( StartFailure.Head, OneCopy.Refusal( "/games/tpw" ), string.Empty );

		StringAssert.Contains( text, "Another copy of OpenTPW is already running on the game in /games/tpw." );
		StringAssert.EndsWith( text, "Log: none" );
	}
}
