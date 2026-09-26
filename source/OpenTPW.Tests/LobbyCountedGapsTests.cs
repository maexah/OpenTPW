using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Two of the lobby's unbuilt paths are counted where the original takes them: the advisor's idle repeat, which the
/// lobby camera's update arms on every pass while a player is picked (<c>0x005e184c</c>, <c>docs/exe/ui.md</c>, "The
/// lobby's idle repeat"), and each isle's random clip, counted once as its first choice (<c>0x005e11f7</c>,
/// <c>docs/exe/lobby.md</c>, "Not sound").
///
/// <para>
/// <b>What these cannot see.</b> No test builds a lobby: the camera runs with no islands, and the isle is a stand-in made
/// without its constructor, holding a model with no clips. That the lobby builds four isles and runs its camera every
/// frame rests on the game run.
/// </para>
/// </summary>
[TestClass]
public class LobbyCountedGapsTests
{
	[TestInitialize]
	public void NobodyPlaying()
	{
		// The first report of each gap is logged, and run alone this class is the first to want a logger.
		Log ??= new();

		SetCurrentPlayer( null );
	}

	[TestCleanup]
	public void PutTheRosterBack() => SetCurrentPlayer( null );

	/// <summary><b>The arm is counted every frame a player is picked, and on no other.</b></summary>
	/// <remarks>
	/// <b>Mutations:</b> the report taken out counts none; the player test taken out counts the frames with nobody
	/// playing.
	/// </remarks>
	[TestMethod]
	public void TheIdleRepeatIsCountedEveryFrameAPlayerIsPicked()
	{
		var camera = new LobbyCameraMode();
		var before = Times( "LOBBY_ADVISOR_IDLE_REPEAT" );

		for ( var frame = 0; frame < 3; ++frame )
			camera.Update();

		Assert.AreEqual( before, Times( "LOBBY_ADVISOR_IDLE_REPEAT" ), "nobody playing: the original arms nothing" );

		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );

		for ( var frame = 0; frame < 3; ++frame )
			camera.Update();

		Assert.AreEqual( before + 3, Times( "LOBBY_ADVISOR_IDLE_REPEAT" ), "one arm a frame while a player is picked" );

		SetCurrentPlayer( null );
		camera.Update();

		Assert.AreEqual( before + 3, Times( "LOBBY_ADVISOR_IDLE_REPEAT" ), "nobody picked again: no arm" );
	}

	/// <summary>
	/// <b>Each isle's random clip is counted once, as its first choice</b>, however many frames it runs, and each isle
	/// counts its own.
	/// </summary>
	/// <remarks><b>Mutations:</b> the report taken out counts none; the latch taken out counts every frame.</remarks>
	[TestMethod]
	public void EachIslesRandomClipIsCountedOnce()
	{
		var first = AnIsle();
		var second = AnIsle();
		var before = Times( "LOBBY_ISLE_RANDOM_CLIP" );

		for ( var frame = 0; frame < 3; ++frame )
			first.Update();

		Assert.AreEqual( before + 1, Times( "LOBBY_ISLE_RANDOM_CLIP" ), "three frames of one isle, one choice" );

		second.Update();
		second.Update();

		Assert.AreEqual( before + 2, Times( "LOBBY_ISLE_RANDOM_CLIP" ), "the second isle counts its own, once" );
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>An isle made without its constructor, whose model has no clips to play.</summary>
	private static LobbyIsland AnIsle()
	{
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		SetField( model, "<Animators>k__BackingField", Array.Empty<MeshAnimator>() );

		var isle = (LobbyIsland)RuntimeHelpers.GetUninitializedObject( typeof( LobbyIsland ) );
		SetField( isle, "_model", model );

		return isle;
	}

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	private static void SetField( object target, string field, object? value )
	{
		for ( var type = target.GetType(); type != null; type = type.BaseType )
		{
			if ( type.GetField( field, BindingFlags.Instance | BindingFlags.NonPublic ) is { } info )
			{
				info.SetValue( target, value );
				return;
			}
		}

		throw new MissingFieldException( target.GetType().Name, field );
	}
}
