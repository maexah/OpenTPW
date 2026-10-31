using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A test run never reaches a player's install (AGENTS.md rule 12; Q986): on a PC with Sim Theme Park registered, the
/// lookup that mounts the game for the tests once went through the registry and ran the suite against the player's copy.
/// </summary>
[TestClass]
public class GameDataTests
{
	/// <remarks><b>Mutation:</b> <c>Locate</c> asking <c>GameDir.Find</c> with no folder given (the working directory, the build's folder and the registry's places) calls <c>find</c> here.</remarks>
	[TestMethod]
	[DataRow( null )]
	[DataRow( "" )]
	[DataRow( "   " )]
	public void NoFolderNamedMeansNoGameAndNothingIsLookedIn( string? named )
	{
		var asked = new List<string[]>();

		Assert.IsFalse( GameData.Locate( named, args => { asked.Add( args ); return true; } ) );
		Assert.AreEqual( 0, asked.Count, "something was asked for a game although no folder was named: " + string.Join( " | ", asked.Select( a => string.Join( " ", a ) ) ) );
	}

	/// <remarks><b>Mutation:</b> a named folder given to <c>find</c> as nothing but the variable's presence (<c>[]</c>), or with another argument beside it, fails the argument check.</remarks>
	[TestMethod]
	public void ANamedFolderIsAskedAsTheOnlyFolderAndTheAnswerIsKept()
	{
		string[]? asked = null;

		Assert.IsTrue( GameData.Locate( "/some/where", args => { asked = args; return true; } ) );
		CollectionAssert.AreEqual( new[] { "--game", "/some/where" }, asked );

		Assert.IsFalse( GameData.Locate( "/some/where", _ => false ), "a folder the game does not accept is no game" );
	}

	/// <remarks>The folder must be named as <c>--game</c>: that is the one source <c>GameDir.Find</c> never falls through to the places a player's install can be.</remarks>
	[TestMethod]
	public void ANamedFolderThatIsNotTheGameDoesNotFallThroughToAnotherPlace()
	{
		var folder = System.IO.Directory.CreateTempSubdirectory( "tpw-gamedata" ).FullName;

		try
		{
			// The registered folder and the box's default are a folder that does hold a game; the named one is not, and nothing falls through to them.
			var game = System.IO.Path.Join( folder, "installed" );
			System.IO.Directory.CreateDirectory( System.IO.Path.Join( game, "data", "levels" ) );

			var old = System.Environment.GetEnvironmentVariable( GameData.GamePathVariable );
			System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, null );

			try
			{
				// A folder that is found sets GameDir.Root for every test after it, so only the refusal is asked here.
				Assert.IsFalse( GameData.Locate( System.IO.Path.Join( folder, "not-the-game" ), args => GameDir.Find( args, [game], game ) ) );
			}
			finally { System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, old ); }
		}
		finally { System.IO.Directory.Delete( folder, true ); }
	}
}
