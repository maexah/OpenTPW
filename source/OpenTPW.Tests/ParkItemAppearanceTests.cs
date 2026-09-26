using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// What an item standing in a park looks like: what its building leaves behind, and which way the
/// parts of it face. These read real game files and are skipped where there is no installation -
/// see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkItemAppearanceTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Reads through the file system this test mounted rather than the global one, which belongs to
	/// a running game and which no test should need to have been set.
	/// </summary>
	private ModelFile Model( string path ) => new( new MemoryStream( data.ReadAllBytes( path ) ) );

	/// <summary>
	/// A clip, read directly rather than through <see cref="AnimationFile.TryLoad"/> - that one goes
	/// through the global file system, and it turns down a clip that carries only visibility.
	/// </summary>
	private AnimationFile Clip( string path ) => new( new MemoryStream( data.ReadAllBytes( path ) ) );

	/// <summary>The frame an item has finished going up on - see ParkObjects.PoseAsBuilt.</summary>
	private static float EndOfBuild( AnimationFile construction )
	{
		var end = (float)construction.LastFrame;

		foreach ( var track in construction.VisibilityTracks )
		{
			foreach ( var entry in track.Entries )
				end = MathF.Max( end, MathF.Abs( entry ) );
		}

		return end;
	}

	/// <summary>
	/// The Belly Bounce arrives as an egg and hatches out of it, and what is left when it has
	/// finished is a dinosaur with no egg and no shell around it. Read off the clip that builds it:
	/// the egg is switched off at frame 94, which is the same frame the dinosaur is switched on, and
	/// the shell it leaves goes at 131.
	///
	/// Played to its end, that clip is what takes the egg away: left at the model's own pose, the egg
	/// stands in the finished ride, with the dinosaur that came out of it drawn through the shell.
	/// </summary>
	[TestMethod]
	public void TheBellyBouncesBuildEndsWithItsEggGone()
	{
		var model = Model( "levels/jungle/rides/bouncy/bouncy.MD2" );
		var construction = Clip( "levels/jungle/rides/bouncy/bouncyc.md2" );

		var end = EndOfBuild( construction );

		bool? ShownAtTheEnd( string name )
		{
			var mesh = model.Nodes.FindIndex( node => node.Name.Trim() == name );

			Assert.AreNotEqual( -1, mesh,
				$"no mesh called '{name}' among: {string.Join( ", ", model.Nodes.Select( node => $"'{node.Name.Trim()}'" ) )}" );

			var track = construction.VisibilityTracks.FirstOrDefault( visibility => visibility.TargetIndex == mesh );

			Assert.IsNotNull( track, $"the clip that builds it says nothing about '{name}'" );

			return track!.VisibleAt( end );
		}

		Assert.AreEqual( false, ShownAtTheEnd( "egg" ), "the egg it hatches out of" );
		Assert.AreEqual( false, ShownAtTheEnd( "shell06" ), "the shell it drops" );

		Assert.AreEqual( true, ShownAtTheEnd( "jb_bd" ), "the dinosaur itself" );
		Assert.AreEqual( true, ShownAtTheEnd( "jb_fence" ), "its fence" );
		Assert.AreEqual( true, ShownAtTheEnd( "jb_sign1" ), "its name board" );
	}

	/// <summary>
	/// A rotation key is the orientation a mesh holds <i>inside its parent</i>, not the one it ends
	/// up with in the model. The Jungle Spray is what shows the difference: its three animal heads
	/// hang off a bench that is itself turned a quarter turn, so each head is square within the
	/// bench while standing at a quarter turn in the model - and every clip keys them square.
	///
	/// Read as orientations in the model, those keys flatten the heads, which turns the
	/// Lion and the Elephant to face the wrong way. See MeshRotator.BuildRestInverses.
	/// </summary>
	[TestMethod]
	public void ARotationKeyIsTheOrientationInsideTheParent()
	{
		var model = Model( "levels/jungle/sideshow/junspray/Junspray.MD2" );

		(string Head, string Clip)[] lanes =
		[
			("Lion", "JunsprayM1"),
			("Elephant", "JunsprayM2"),
			("Eagle", "JunsprayM3")
		];

		foreach ( var (head, clip) in lanes )
		{
			var mesh = model.Nodes.FindIndex( node => node.Name.Trim() == head );

			Assert.AreNotEqual( -1, mesh, $"no mesh called '{head}'" );

			var node = model.Nodes[mesh];

			Assert.IsTrue( Matrix4x4.Decompose( node.LocalTransform, out _, out var local, out _ ), $"{head}'s own transform" );
			Assert.IsTrue( Matrix4x4.Decompose( node.WorldTransform, out _, out var world, out _ ), $"{head} in the model" );

			Assert.AreEqual( 1f, MathF.Abs( Quaternion.Dot( local, Quaternion.Identity ) ), 0.001f,
				$"{head} should be square within the bench it sits on" );

			Assert.AreEqual( 90f, Turn( world ), 1f,
				$"{head} should stand a quarter turn round once the bench is applied" );

			var animation = Clip( $"levels/jungle/sideshow/junspray/{clip}.MD2" );
			var track = animation.RotationTracks.FirstOrDefault( rotation => rotation.TargetIndex == mesh );

			Assert.IsNotNull( track, $"{clip} is the clip that drives {head}" );

			foreach ( var key in track!.Rotations )
			{
				Assert.AreEqual( 1f, MathF.Abs( Quaternion.Dot( key, local ) ), 0.001f,
					$"{clip}'s keys for {head} should be the orientation it is authored with inside its parent" );
			}
		}
	}

	/// <summary>
	/// Why a gate cannot catch this: every gate in the game parents its doors straight to a root that
	/// carries no turn of its own, and there the two orientations are the same matrix.
	/// </summary>
	[TestMethod]
	public void AGateCannotTellTheTwoOrientationsApart()
	{
		foreach ( var path in new[] { "lobby/terrain/Jun_gate.md2", "levels/jungle/features/gates/gates.MD2" } )
		{
			var model = Model( path );

			for ( int mesh = 0; mesh < model.Meshes.Count; ++mesh )
			{
				var node = model.Nodes[mesh];

				Assert.IsTrue( Matrix4x4.Decompose( node.LocalTransform, out _, out var local, out _ ), path );
				Assert.IsTrue( Matrix4x4.Decompose( node.WorldTransform, out _, out var world, out _ ), path );

				Assert.AreEqual( 1f, MathF.Abs( Quaternion.Dot( local, world ) ), 0.001f,
					$"{path}: '{node.Name.Trim()}' should sit on a root that does not turn it" );
			}
		}
	}

	/// <summary>How far round a turn is, in degrees, whichever way it is expressed.</summary>
	private static float Turn( Quaternion rotation )
		=> 2f * MathF.Acos( MathF.Min( 1f, MathF.Abs( rotation.W ) ) ) * 180f / MathF.PI;
}
