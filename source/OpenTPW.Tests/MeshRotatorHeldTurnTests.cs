using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A clip that turns meshes alone is posed by <see cref="MeshRotator"/>, and before a rotation track's first key the original skips the
/// track (<c>0x00471c56</c>) and the node keeps the turn it holds, as <see cref="NodePoser"/> keeps it (Q1010; <c>docs/exe/park.md</c>,
/// <c>FUN_00471860</c>). Mumbo's <c>c</c> is the construction clip, played once when the ride is bought: its <c>jm_leaf</c> is keyed from frame 12, and
/// the original's, polled live from the put-down, stood at the leaf's rest turn through frames 0 to 12 and at the key from frame 12 on.
/// </summary>
[TestClass]
public class MeshRotatorHeldTurnTests
{
	private static AnimationFile Clip( params (int Target, int[] Frames, float[] Degrees)[] tracks )
	{
		var clip = (AnimationFile)RuntimeHelpers.GetUninitializedObject( typeof( AnimationFile ) );

		foreach ( var property in new[] { "PositionTracks", "RotationTracks", "PathTracks", "ScaleTracks" } )
		{
			var field = typeof( AnimationFile ).GetField( $"<{property}>k__BackingField",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!;
			field.SetValue( clip, field.FieldType.GetConstructor( Type.EmptyTypes )!.Invoke( null ) );
		}

		foreach ( var (target, frames, degrees) in tracks )
		{
			clip.RotationTracks.Add( new()
			{
				TargetIndex = target,
				FrameIndices = [.. frames.Select( frame => (ushort)frame )],
				Rotations = [.. degrees.Select( degree => Quaternion.CreateFromAxisAngle( Vector3.UnitZ, degree * MathF.PI / 180f ) )],
				CurveIds = [.. frames.Select( _ => AnimationFile.RotationTrack.NoCurve )]
			} );
		}

		return clip;
	}

	private static (MeshRotator Rotator, ModelEntity[] Meshes) ARotator( AnimationFile[] clips, int count )
	{
		var meshes = Enumerable.Range( 0, count )
			.Select( _ => (ModelEntity)RuntimeHelpers.GetUninitializedObject( typeof( ModelEntity ) ) ).ToArray();
		var identity = Enumerable.Repeat( Matrix4x4.Identity, count ).ToArray();

		return (new MeshRotator( clips, meshes, identity, identity, new OpenTPW.Vector3[count], Enumerable.Repeat( -1, count ).ToArray() ), meshes);
	}

	/// <summary>How far a transform turns, in degrees: the rotator conjugates a key into the meshes' axes, so the axis is not z.</summary>
	private static float Z( Matrix4x4? turn )
		=> MathF.Round( 2f * MathF.Acos( MathF.Min( 1f, MathF.Abs( Quaternion.CreateFromRotationMatrix( turn!.Value ).W ) ) ) * 180f / MathF.PI, 1 );

	/// <summary>
	/// Mesh 1 is keyed from frame 12 at 10 then 30 degrees. Before frame 12 it is not touched (a mesh just built stands as it was built, and a
	/// frame of 11.9 is still before the key); frame 12 is the key; a pass that comes round to frame 5 leaves the last key where it put it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the first key held before its frame (the mesh at 10 degrees at frame 5); the skip tested on the rounded frame
	/// (11.9 would pose the key); the skip up to and including the first frame (frame 12 untouched).</remarks>
	[TestMethod]
	public void BeforeATracksFirstKeyTheMeshKeepsTheTurnItHolds()
	{
		var clip = Clip( (1, [12, 40], [10f, 30f]) );
		var (rotator, meshes) = ARotator( [clip], 2 );

		rotator.Pose( clip, 5f );
		Assert.IsNull( meshes[1].LinearTransform, "a model just built: not posed before the first key" );

		rotator.Pose( clip, 11.9f );
		Assert.IsNull( meshes[1].LinearTransform, "frame 11.9 is before the key at 12" );

		rotator.Pose( clip, 12f );
		Assert.AreEqual( 10f, Z( meshes[1].LinearTransform ), "the first key itself" );

		rotator.Pose( clip, 60f );
		Assert.AreEqual( 30f, Z( meshes[1].LinearTransform ), "the last key" );

		rotator.Pose( clip, 5f );
		Assert.AreEqual( 30f, Z( meshes[1].LinearTransform ), "the pass comes round: the turn the pass before left, not the first key" );
	}

	/// <summary>
	/// <b>A model has one matrix a node, and two posers</b>: what a rotator's pose left is handed over once (<see cref="MeshRotator.TakeHeld"/>) and
	/// <see cref="NodePoser.Keep"/> takes it up in the tree, so a clip the tree poses after one the rotator posed holds the turn the clip before
	/// left. A change of clip puts back what the outgoing clip turned, all but what the incoming clip turns too
	/// (<c>FUN_00472310</c>, <see cref="MeshRotator.Rest"/>).
	/// </summary>
	/// <remarks><b>Mutations:</b> a hand-over that gives the turn again at the second take; a rest that restores a mesh the incoming clip turns; a
	/// rest that leaves a mesh nothing turns next.</remarks>
	[TestMethod]
	public void WhatTheRotatorLeavesIsHandedOverOnceAndAChangeOfClipRestsAllButWhatTheNextTurns()
	{
		var first = Clip( (1, [12, 40], [10f, 30f]), (2, [0, 40], [5f, 5f]) );
		var next = Clip( (1, [14, 90], [30f, 60f]) );
		var (rotator, meshes) = ARotator( [first, next], 3 );

		Assert.AreEqual( 0, rotator.TakeHeld().Count(), "nothing posed, nothing held" );

		rotator.Pose( first, 20f );

		var held = rotator.TakeHeld().ToArray();

		CollectionAssert.AreEqual( new[] { 1, 2 }, held.Select( turn => turn.Node ).ToArray(), "each mesh the pose turned" );
		Assert.AreEqual( 0, rotator.TakeHeld().Count(), "handed over once" );

		var poser = new NodePoser( [Matrix4x4.Identity, Matrix4x4.CreateRotationZ( 5f * MathF.PI / 180f ), Matrix4x4.Identity], [-1, -1, -1], 0 );

		foreach ( var (node, key) in held )
			poser.Keep( node, key );

		Assert.AreEqual( held[0].Key.W, Quaternion.CreateFromRotationMatrix( poser.Local( 1 ) ).W, 1e-3f, "the tree stands on the key the rotator posed" );

		rotator.Pose( first, 20f );
		var before = meshes[1].LinearTransform;
		rotator.Rest( first, next );
		Assert.AreEqual( before, meshes[1].LinearTransform, "mesh 1 is turned by the next clip too: left as it was" );
		Assert.AreEqual( Matrix4x4.Identity, meshes[2].LinearTransform, "mesh 2 is not turned by the next clip: put back" );

		var left = rotator.TakeHeld().Select( turn => turn.Node ).ToArray();

		CollectionAssert.AreEqual( new[] { 1 }, left, "mesh 1 is still held for the tree, and nothing of mesh 2, which was put back" );
	}

	/// <summary>
	/// Mumbo's <c>c</c> then <c>m</c>, from the game's own files: <c>c</c> turns <c>jm_leaf</c> alone (a mesh, so the rotator's), keyed from frame 12 at
	/// the turn <c>m</c> ends on; <c>m</c> turns <c>jm_dum</c> (the tree's). Through frame 12 of <c>c</c> the leaf is not posed (the original's, at
	/// rest); posed through frame 20 and handed to the tree, <c>m</c>'s frames before its leaf's first key (14) hold <c>c</c>'s turn, where a tree
	/// that never saw it stands the leaf at its rest turn, 3.74 degrees off.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hand-over left out (the leaf at its rest in <c>m</c>'s first frames); the first key held in <c>c</c>.</remarks>
	[TestMethod]
	public void MumbosLeafStandsAtRestThroughCsFirst12FramesAndMHoldsCsTurnUntilItsFirstKey()
	{
		var data = GameData.Required();
		var item = new ParkItemCatalogue( "jungle", data ).All.Single( item => item.Stem == "mumbo" );

		using var modelStream = new System.IO.MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
		var model = new ModelFile( modelStream );
		var leaf = model.Nodes.FindIndex( node => node.Name == "jm_leaf" );

		AnimationFile Load( string suffix )
		{
			using var stream = new System.IO.MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}{suffix}.MD2" ) );
			return new AnimationFile( stream );
		}

		var construction = Load( "c" );
		var running = Load( "m" );
		var dummy = model.Nodes.FindIndex( node => node.Name == "jm_dum" );

		Assert.IsTrue( construction.RotationTracks.All( track => track.TargetIndex < dummy ), "c turns meshes alone, so the rotator poses it" );
		Assert.AreEqual( 12, construction.RotationTracks.Single( track => track.TargetIndex == leaf ).FrameIndices[0], "c's leaf key" );
		Assert.IsTrue( running.RotationTracks.Any( track => track.TargetIndex == dummy ), "m turns the dummy, so the tree poses it" );

		var (rotator, meshes) = ARotator( [construction], leaf + 1 );

		rotator.Pose( construction, 0f );
		rotator.Pose( construction, 11.9f );
		Assert.IsNull( meshes[leaf].LinearTransform, "frames 0 to 12 of c: the leaf stands as it was built, at rest" );

		rotator.Pose( construction, 20f );
		Assert.IsNotNull( meshes[leaf].LinearTransform, "from frame 12: the key" );

		static float[] Rows( Matrix4x4 m ) => [m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33];

		float[] key = [0.99945f, 0.00003f, -0.03307f, -0.00118f, 0.99939f, -0.03483f, 0.03305f, 0.03485f, 0.99885f];
		float[] rest = [0.99905f, -0.04362f, 0f, 0.04362f, 0.99905f, 0f, 0f, 0f, 1f];

		void Is( float[] expected, Matrix4x4 actual, string what )
		{
			var rows = Rows( actual );

			for ( var i = 0; i < 9; ++i )
				Assert.AreEqual( expected[i], rows[i], 0.002f, $"{what}: element {i}" );
		}

		var handed = new NodePoser( model );

		foreach ( var (node, turn) in rotator.TakeHeld() )
			handed.Keep( node, turn );

		var forgotten = new NodePoser( model );

		Is( rest, forgotten.Local( leaf ), "a tree that never saw c: the leaf at its rest turn" );

		handed.Pose( running, 5f );
		Is( key, handed.Local( leaf ), "m's frame 5, before its leaf's first key at 14: the turn c left" );

		forgotten.Pose( running, 5f );
		Is( rest, forgotten.Local( leaf ), "without the hand-over: the rest, 3.74 degrees off the original's" );

		handed.Pose( running, 14f );
		Is( rest, handed.Local( leaf ), "m's own first key, at frame 14" );
	}

	/// <summary>
	/// <see cref="LobbyModel.PoseThroughTree"/> (what <see cref="LobbyModel.Pose"/> runs for a node-tree clip) hands the rotator's turns to the tree
	/// before the pose and places the meshes from it: <c>next</c>'s mesh 1 is keyed from frame 14, so a pose at frame 5 stands on the turn the
	/// rotator left, and the placed mesh carries it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hand-over loop after <c>poser.Pose</c> (the placed mesh stands at rest); the loop removed.</remarks>
	[TestMethod]
	public void ThePoseThroughTheTreeStartsFromTheTurnTheRotatorLeft()
	{
		var first = Clip( (1, [12, 40], [10f, 30f]) );
		var next = Clip( (1, [14, 90], [30f, 60f]) );
		var (rotator, meshes) = ARotator( [first, next], 2 );

		rotator.Pose( first, 20f );

		var heldDegrees = Z( meshes[1].LinearTransform );
		Assert.IsTrue( heldDegrees > 12f && heldDegrees < 28f, "between the first clip's keys" );

		var poser = new NodePoser( [Matrix4x4.Identity, Matrix4x4.Identity], [-1, -1], 0 );
		var placed = new[] { 0, 1 }.Select( _ => (ModelEntity)RuntimeHelpers.GetUninitializedObject( typeof( ModelEntity ) ) ).ToArray();

		LobbyModel.PoseThroughTree( placed, rotator, poser, next, 5f, 1f, Quaternion.Identity, OpenTPW.Vector3.Zero );

		Assert.AreEqual( heldDegrees, Z( placed[1].LinearTransform ), 0.2f, "before the key at 14: the turn the rotator left" );
		Assert.AreEqual( 0, rotator.TakeHeld().Count(), "handed over" );
	}

	/// <summary>
	/// <see cref="LobbyModel.RestMeshes"/> with a rotator: the rotator is told the next clip, and a mesh both clips turn is neither restored by the rotator
	/// nor placed again from the tree (the tree never saw its turn), as the exe's per-node mask leaves it (<c>FUN_00472310</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="LobbyModel.MeshesRested"/> without the incoming clip's mask (mesh 1 is placed from the tree); the rotator not
	/// told the next clip (mesh 1 is put back).</remarks>
	[TestMethod]
	public void AChangeOfClipLeavesAMeshBothClipsTurnWhereTheRotatorHeldIt()
	{
		var first = Clip( (1, [12, 40], [10f, 30f]), (2, [0, 40], [5f, 5f]) );
		var next = Clip( (1, [14, 90], [30f, 60f]) );
		var (rotator, meshes) = ARotator( [first, next], 3 );

		rotator.Pose( first, 20f );

		var held = meshes[1].LinearTransform;
		var poser = new NodePoser( [Matrix4x4.Identity, Matrix4x4.CreateRotationZ( 40f * MathF.PI / 180f ), Matrix4x4.Identity], [-1, -1, -1], 0 );

		LobbyModel.RestMeshes( meshes, new OpenTPW.Vector3[3], [-1, -1, -1], poser, 1f, Quaternion.Identity, OpenTPW.Vector3.Zero, first, next, rotator );

		Assert.AreEqual( held, meshes[1].LinearTransform, "mesh 1: turned by the next clip too, left as the rotator held it" );
		CollectionAssert.AreEqual( new[] { false, false, true }, LobbyModel.MeshesRested( first, [-1, -1, -1], 3, next ), "only mesh 2 is placed again" );
	}
}
