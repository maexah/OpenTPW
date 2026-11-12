using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The original keeps one matrix a node for all of a model's channels; ours poses a clip that moves nodes through
/// <see cref="NodePoser"/> and one that only turns meshes through <see cref="MeshRotator"/> (<see cref="LobbyModel.PoseMeshes"/>).
/// A turn on a later channel than a node-moving clip draws as one tree would while nothing above or under the turned mesh
/// moves, and no model of the four themes that runs more than one channel has a node-moving clip that moves anything above
/// or under a mesh another clip turns alone (Q574).
/// </summary>
[TestClass]
public class OneTreeSweepTests
{
	private static AnimationFile Clip( (int Target, float Degrees)[] turns, (int Target, Vector3 Point)[] places )
	{
		var clip = (AnimationFile)RuntimeHelpers.GetUninitializedObject( typeof( AnimationFile ) );

		foreach ( var property in new[] { "PositionTracks", "RotationTracks", "PathTracks", "ScaleTracks" } )
		{
			var field = typeof( AnimationFile ).GetField( $"<{property}>k__BackingField",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!;
			field.SetValue( clip, field.FieldType.GetConstructor( Type.EmptyTypes )!.Invoke( null ) );
		}

		foreach ( var (target, degrees) in turns )
		{
			var turn = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, degrees * MathF.PI / 180f );
			clip.RotationTracks.Add( new()
			{
				TargetIndex = target,
				FrameIndices = [0, 40],
				Rotations = [turn, turn],
				CurveIds = [AnimationFile.RotationTrack.NoCurve, AnimationFile.RotationTrack.NoCurve]
			} );
		}

		foreach ( var (target, point) in places )
			clip.PositionTracks.Add( new() { TargetIndex = target, FrameIndices = [0, 40], Points = [point, point] } );

		return clip;
	}

	private static ModelEntity[] Meshes( int count )
		=> [.. Enumerable.Range( 0, count ).Select( _ => (ModelEntity)RuntimeHelpers.GetUninitializedObject( typeof( ModelEntity ) ) )];

	/// <summary>
	/// Three meshes at rest under nothing. Channel 0 plays a clip that moves nodes (it places mesh 1 and mesh 2 and turns mesh 1 by 40 degrees);
	/// channel 1, posed after it, a clip that turns mesh 1 alone by 10 degrees. Drawn: mesh 1 turned 10 degrees where channel 0 put it, mesh 2
	/// where channel 0 put it; the same as one tree posing channel 0 and then channel 1. Every frame, as the channels are posed each frame.
	/// </summary>
	/// <remarks><b>Mutations:</b> the tree's placing removed from <see cref="LobbyModel.PoseThroughTree"/>, and <see cref="MeshRotator.Pose"/>
	/// putting the turned mesh back at its rest place: each leaves mesh 1 5.39 off the place one tree draws.</remarks>
	[TestMethod]
	public void ATurnOnALaterChannelIsDrawnAsOneTreeWhereNothingAboveOrUnderItMoves()
	{
		var tree = Clip( [(1, 40f)], [(1, new Vector3( 5f, 0f, 2f )), (2, new Vector3( -3f, 1f, 0f ))] );
		var turn = Clip( [(1, 10f)], [] );

		Assert.IsTrue( NodePoser.Drives( tree, 3 ), "the tree clip moves nodes" );
		Assert.IsFalse( NodePoser.Drives( turn, 3 ), "the other turns a mesh alone" );

		var identity = Enumerable.Repeat( Matrix4x4.Identity, 3 ).ToArray();
		var parents = new[] { -1, -1, -1 };
		var drawn = Meshes( 3 );
		var rotator = new MeshRotator( [tree, turn], drawn, identity, identity, new OpenTPW.Vector3[3], parents );
		var poser = new NodePoser( identity, parents, 0 );

		var one = new NodePoser( identity, parents, 0 );
		var reference = Meshes( 3 );

		for ( var frame = 0f; frame <= 20f; frame += 10f )
		{
			LobbyModel.PoseMeshes( drawn, rotator, () => poser, false, tree, frame, 1f, Quaternion.Identity, OpenTPW.Vector3.Zero );
			LobbyModel.PoseMeshes( drawn, rotator, () => poser, false, turn, frame, 1f, Quaternion.Identity, OpenTPW.Vector3.Zero );

			one.Pose( tree, frame );
			one.Pose( turn, frame );
			LobbyModel.PlacePosed( reference, one, 1f, Quaternion.Identity, OpenTPW.Vector3.Zero );

			for ( var mesh = 1; mesh < 3; ++mesh )
			{
				var at = $"frame {frame}, mesh {mesh}";
				Assert.IsNotNull( drawn[mesh].LinearTransform, $"{at}: posed" );

				var expected = reference[mesh].LinearTransform!.Value;
				var actual = drawn[mesh].LinearTransform!.Value;

				Assert.AreEqual( Angle( expected ), Angle( actual ), 0.01f, $"{at}: the turn one tree draws" );
				Assert.AreEqual( 0f, Gap( reference[mesh].Position, drawn[mesh].Position ), 1e-4f, $"{at}: the place one tree draws" );
			}

			Assert.AreEqual( 10f, Angle( drawn[1].LinearTransform!.Value ), 0.01f, $"frame {frame}: the later channel's turn" );
		}
	}

	private static float Gap( OpenTPW.Vector3 a, OpenTPW.Vector3 b )
		=> Vector3.Distance( new Vector3( a.X, a.Y, a.Z ), new Vector3( b.X, b.Y, b.Z ) );

	/// <summary>How far a transform turns, in degrees.</summary>
	private static float Angle( Matrix4x4 turn )
		=> 2f * MathF.Acos( MathF.Min( 1f, MathF.Abs( Quaternion.CreateFromRotationMatrix( turn ).W ) ) ) * 180f / MathF.PI;

	/// <summary>
	/// Every item of the four themes' catalogues running more than one channel (the park gates are items; the sweep tool's walk of every
	/// <c>.sam</c> in every archive finds no other), each clip on the channels its scripts start it on (<c>TRIGANIM_CH</c> and
	/// <c>LOOPANIM_CH</c> name one, every other start is channel 0; no shipped start names either in a variable). Of two clips on two
	/// channels, one <see cref="LobbyModel.PoseMeshes"/> sends to the rotator (<see cref="MeshRotator.Drives"/> and not
	/// <see cref="NodePoser.Drives(AnimationFile, int)"/>) and one it sends to the tree, the first turns no mesh above or under a node the
	/// second moves, nor one it scales, so whichever is posed first, ours draws them as one tree. The one model where the two turn one
	/// mesh is the Inca Totem: its role-5 clips on channels 1 to 3 turn meshes 8, 9 and 10, which its clips of roles 0, 9 and 10 on
	/// channel 0 turn too, all under <c>tp_ground</c>, which nothing moves (<c>tools/harness/dotnet/channelclips</c> prints the pairs).
	/// Space's Lunar turns a mesh alone under a node its tree clips move, all on channel 0, so never in one frame.
	/// </summary>
	/// <remarks><b>Mutation:</b> <see cref="RideAnimations"/>'s numbered run read from 2 (16 pairs, not 36). It does not guard
	/// <see cref="NodePoser.Drives(AnimationFile, int)"/>'s turn past the meshes: the Totem's tree clips move nodes by position tracks too.</remarks>
	[TestMethod]
	public void NoTwoChannelsTurnAMeshAloneWhereATreeClipMovesAboveOrUnderIt()
	{
		var data = GameData.Required();
		FileSystem = data;

		var models = new Dictionary<string, (string Dir, string Stem, int Channels)>( StringComparer.OrdinalIgnoreCase );

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			foreach ( var item in new ParkItemCatalogue( theme, data, false ).All )
				models.TryAdd( $"{item.Directory}/{item.Stem}", (item.Directory, item.Stem, item.AnimationChannels) );
		}

		var starters = new HashSet<Opcode> { Opcode.TRIGANIM, Opcode.TRIGANIM_CH, Opcode.LOOPANIM, Opcode.LOOPANIM_CH, Opcode.TRIGWAITANIM, Opcode.WAITANIM };
		var multi = 0;
		var pairs = 0;
		var shared = new SortedDictionary<string, SortedSet<int>>();
		var crossed = new List<string>();
		var unread = new List<string>();

		foreach ( var (key, (dir, stem, channels)) in models )
		{
			if ( channels <= 1 )
				continue;

			using var stream = data.OpenReadOrNull( $"{dir}/{stem}.md2" );

			if ( stream == null )
				continue;

			++multi;

			var model = new ModelFile( stream );
			var meshes = model.Meshes.Count;
			var parents = model.Nodes.Select( node => node.ParentIndex ).ToArray();
			var animations = RideAnimations.Load( dir, stem, data, channels );

			// Every start in the scripts beside the model: (channel, role, entry).
			var starts = new HashSet<(int Channel, int Role, int Entry)>();

			foreach ( var name in data.GetFiles( dir ).Where( name => name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) ) )
			{
				using var script = data.OpenReadOrNull( $"{dir}/{System.IO.Path.GetFileName( name )}" );

				if ( script == null )
					continue;

				foreach ( var instruction in new RideScriptFile( script ).Instructions.Where( instruction => starters.Contains( instruction.Opcode ) ) )
				{
					var operands = instruction.Operands;
					var channel = instruction.Opcode switch { Opcode.TRIGANIM_CH => operands[3], Opcode.LOOPANIM_CH => operands[2], _ => default };

					if ( operands[0].Kind == RideOperandKind.Variable || operands[1].Kind == RideOperandKind.Variable || channel.Kind == RideOperandKind.Variable )
						unread.Add( $"{key} {instruction.Opcode} at {instruction.Address}" );
					else
						starts.Add( (channel.Value, operands[0].Value, operands[1].Value) );
				}
			}

			bool Under( int node, int ancestor )
			{
				for ( int at = node, guard = parents.Length; at >= 0 && at < parents.Length && guard >= 0; at = parents[at], --guard )
				{
					if ( at == ancestor )
						return true;
				}

				return false;
			}

			foreach ( var t in starts )
			{
				if ( animations.Clip( t.Role, t.Entry ) is not { } turn || NodePoser.Drives( turn, meshes ) || !MeshRotator.Drives( turn, meshes ) )
					continue;

				foreach ( var n in starts.Where( n => n.Channel != t.Channel ) )
				{
					if ( animations.Clip( n.Role, n.Entry ) is not { } tree || !NodePoser.Drives( tree, meshes ) )
						continue;

					++pairs;

					foreach ( var mesh in turn.RotationTracks.Select( track => track.TargetIndex ).Where( mesh => mesh >= 0 && mesh < meshes ).Distinct() )
					{
						foreach ( var node in tree.Tracks.Select( track => track.Node ).Distinct() )
						{
							if ( node == mesh )
								( shared.TryGetValue( key, out var set ) ? set : shared[key] = [] ).Add( mesh );
							else if ( Under( mesh, node ) || Under( node, mesh ) )
								crossed.Add( $"{key}: mesh {mesh} turned alone on channel {t.Channel}, node {node} moved on channel {n.Channel}" );
						}

						// A scale the tree gives the mesh itself, which the rotator's turn would replace.
						if ( tree.ScaleTracks.Any( track => track.TargetIndex == mesh ) )
							crossed.Add( $"{key}: mesh {mesh} turned alone on channel {t.Channel}, scaled on channel {n.Channel}" );
					}
				}
			}
		}

		Assert.AreEqual( 9, multi, "the models of more than one channel with a model file: five sideshows, the Totem, the space gates, Lunar and Sci Puzzle" );
		Assert.AreEqual( "", string.Join( "; ", unread ), "a start whose role, entry or channel is a variable" );
		Assert.AreEqual( 36, pairs, "the Totem's nine turns on channels 1 to 3 against its four tree clips on channel 0" );
		Assert.AreEqual( "", string.Join( "; ", crossed.Distinct() ), "a mesh turned alone under or over a node a tree clip on another channel moves" );
		Assert.AreEqual( "levels/jungle/rides/totem/totem: 8,9,10",
			string.Join( "; ", shared.Select( pair => $"{pair.Key}: {string.Join( ",", pair.Value )}" ) ), "the models whose two kinds of clip turn one mesh" );
	}
}
