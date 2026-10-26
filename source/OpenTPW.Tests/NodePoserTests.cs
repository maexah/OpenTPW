using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A clip's position tracks, posed over the model's node tree - <see cref="NodePoser"/> (docs/exe/park.md, "Clip content";
/// FileFormats models.md, "Position (bit 0x1)"). The trees here are made so that a wrong rule lands somewhere else: a
/// turned root, so a key read in the world's axes rather than its parent's misses; a child, so a node that does not
/// carry what hangs off it leaves it behind; a first key past nought, so a sampler that clamps moves the node early.
/// </summary>
[TestClass]
public class NodePoserTests
{
	/// <summary>A root a quarter turn about y at (480, 0, 170), a plane under it at (0, 59, 273), a propeller under that at (6, 7, 3).</summary>
	private static NodePoser Tree( uint headerFlags = 0 )
	{
		var root = Matrix4x4.CreateRotationY( MathF.PI / 2 ) * Matrix4x4.CreateTranslation( 480, 0, 170 );

		return new NodePoser(
			[Matrix4x4.CreateTranslation( 0, 59, 273 ), Matrix4x4.CreateTranslation( 6, 7, 3 ), root],
			[2, 0, -1], headerFlags );
	}

	private static AnimationFile Clip( params AnimationFile.PositionTrack[] tracks )
	{
		var clip = (AnimationFile)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( AnimationFile ) );

		foreach ( var property in new[] { "PositionTracks", "RotationTracks", "PathTracks" } )
		{
			var field = typeof( AnimationFile ).GetField( $"<{property}>k__BackingField",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!;
			field.SetValue( clip, field.FieldType.GetConstructor( Type.EmptyTypes )!.Invoke( null ) );
		}

		clip.PositionTracks.AddRange( tracks );
		return clip;
	}

	/// <summary>The plane, node 0, flown from (0, 59, 273) at frame 10 to (0, 59, -77) at frame 20 in a straight line.</summary>
	private static AnimationFile.PositionTrack Flight( int node = 0 ) => new()
	{
		TargetIndex = node,
		FrameIndices = [10, 20],
		Points = [new( 0, 59, 273 ), new( 0, 59, -77 )],
		IsBezier = false
	};

	private static void Near( Vector3 expected, Vector3 actual, string what, float within = 0.01f )
		=> Assert.IsTrue( Vector3.Distance( expected, actual ) < within, $"{what}: expected {expected}, got {actual}" );

	/// <summary>
	/// Halfway, the plane's key (0, 59, 98) is within its parent: the root's quarter turn takes z to x, so it stands at
	/// (578, 59, 170). Read in the world's axes it would stand at (480, 59, 268).
	/// </summary>
	[TestMethod]
	public void AKeyIsWithinTheParent()
	{
		var poser = Tree();

		Near( new( 480 + 273, 59, 170 ), poser.World( 0 ).Translation, "at rest" );

		poser.Pose( Clip( Flight() ), 15 );

		Near( new( 480 + 98, 59, 170 ), poser.World( 0 ).Translation, "halfway" );
		Near( new( 0, 59, 98 ), poser.Local( 0 ).Translation, "its own translation" );
	}

	/// <summary>The propeller rides 6, 7 and 3 off the plane wherever the plane flies: at the end of the flight (-77 + 3) turned to x.</summary>
	[TestMethod]
	public void ANodeCarriesWhatHangsOffIt()
	{
		var poser = Tree();

		poser.Pose( Clip( Flight() ), 20 );

		Near( new( 480 - 77, 59, 170 ), poser.World( 0 ).Translation, "the plane" );
		Near( new( 480 - 77 + 3, 66, 170 - 6 ), poser.World( 1 ).Translation, "the propeller" );
	}

	/// <summary>
	/// Before the first key (frame 10) the node is left where it is: at rest on a fresh pose, and where the last pose put it
	/// after one - frame 9.9 floors to 9, still before. A sampler that clamps to the first point would put it at 273 on
	/// both, which is the rest here only by chance, so the second pose is the one that tells.
	/// </summary>
	[TestMethod]
	public void BeforeTheFirstKeyTheNodeStaysWhereItIs()
	{
		var poser = Tree();
		var clip = Clip( Flight() );

		poser.Pose( clip, 5 );
		Near( new( 480 + 273, 59, 170 ), poser.World( 0 ).Translation, "fresh" );

		poser.Pose( clip, 20 );
		poser.Pose( clip, 9.9f );
		Near( new( 480 - 77, 59, 170 ), poser.World( 0 ).Translation, "after the clip came round again" );

		poser.Pose( clip, 10 );
		Near( new( 480 + 273, 59, 170 ), poser.World( 0 ).Translation, "on the first key" );
	}

	/// <summary>
	/// A change of clip puts the node back, unless the incoming clip moves it too, when it stays where the outgoing left
	/// it until the incoming's first key (FUN_00472310).
	/// </summary>
	[TestMethod]
	public void AChangeOfClipPutsBackWhatTheIncomingDoesNotMove()
	{
		var poser = Tree();
		var outgoing = Clip( Flight() );

		poser.Pose( outgoing, 20 );
		poser.Rest( outgoing, Clip( Flight() ) );
		Near( new( 480 - 77, 59, 170 ), poser.World( 0 ).Translation, "incoming moves it too" );

		poser.Rest( outgoing, Clip( Flight( 1 ) ) );
		Near( new( 480 + 273, 59, 170 ), poser.World( 0 ).Translation, "incoming moves another node" );

		poser.Pose( outgoing, 20 );
		poser.Rest( outgoing, null );
		Near( new( 480 + 273, 59, 170 ), poser.World( 0 ).Translation, "no incoming" );
	}

	/// <summary>
	/// A rotation key replaces the node's own turn and carries what hangs off it. The plane rests turned a half turn,
	/// keyed a quarter: the propeller's (6, 7, 3) turns to (3, 7, -6) about the plane, which still stands at its rest place
	/// (frame 5, before the flight's first key), and the root's quarter turn takes the whole to (747, 66, 167); the turn
	/// leaves the plane's place alone. Keyed on
	/// top of the rest turn it would land at (753, 66, 173); left unturned, at (756, 66, 164).
	/// </summary>
	[TestMethod]
	public void ARotationKeyTurnsTheNodeAndWhatHangsOffIt()
	{
		var root = Matrix4x4.CreateRotationY( MathF.PI / 2 ) * Matrix4x4.CreateTranslation( 480, 0, 170 );
		var poser = new NodePoser(
			[Matrix4x4.CreateRotationY( MathF.PI ) * Matrix4x4.CreateTranslation( 0, 59, 273 ), Matrix4x4.CreateTranslation( 6, 7, 3 ), root],
			[2, 0, -1], 0 );

		var quarter = new AnimationFile.RotationTrack
		{
			TargetIndex = 0,
			FrameIndices = [0],
			Rotations = [Quaternion.CreateFromAxisAngle( Vector3.UnitY, MathF.PI / 2 )]
		};

		var clip = Clip( Flight() );
		clip.RotationTracks.Add( quarter );

		poser.Pose( clip, 5 );

		Near( new( 480 + 273, 59, 170 ), poser.World( 0 ).Translation, "the plane" );
		Near( new( 747, 66, 167 ), poser.World( 1 ).Translation, "the propeller" );

		// The turn keeps whatever place the node holds: flown to the end and come round before the first key again.
		poser.Pose( clip, 20 );
		poser.Pose( clip, 5 );
		Near( new( 480 - 77, 59, 170 ), poser.World( 0 ).Translation, "the plane where the flight left it" );

		var turnsOnly = Clip();
		turnsOnly.RotationTracks.Add( quarter );

		Assert.IsTrue( NodePoser.Drives( clip ) );
		Assert.IsFalse( NodePoser.Drives( turnsOnly ), "a clip that only turns is MeshRotator's" );
	}

	/// <summary>A model whose header carries 0x4 adds the key to the node's own translation: 273 + 98 halfway, from the rest every time.</summary>
	[TestMethod]
	public void AnAdditiveModelAddsTheKeyToTheRest()
	{
		var poser = Tree( 0x5 );
		var clip = Clip( Flight() );

		poser.Pose( clip, 15 );
		poser.Pose( clip, 15 );

		Near( new( 0, 118, 273 + 98 ), poser.Local( 0 ).Translation, "added once, not twice" );
	}

	/// <summary>
	/// The End's first clip flies its first plane, <c>_f_endplane02</c> (mesh 0), from where it rests, (480.3, 59, 443.4),
	/// down the park to the clip's last key, 353 nearer the gate along z; its propeller <c>prop08</c> keeps its distance
	/// from it, as it does at rest. Read from the game's own <c>end.wad</c>.
	/// </summary>
	[TestMethod]
	public void TheEndsFirstPlaneFliesAndTakesItsPropellers()
	{
		var data = GameData.Required();

		using var modelStream = data.OpenRead( "levels/jungle/features/end/End.MD2" )!;
		using var clipStream = data.OpenRead( "levels/jungle/features/end/Endm1.MD2" )!;

		var model = new ModelFile( modelStream );
		var clip = new AnimationFile( clipStream );
		var poser = new NodePoser( model );
		var track = clip.PositionTracks.Single( track => track.TargetIndex == 0 );
		var last = track.FrameIndices[^1];

		Assert.IsTrue( NodePoser.Drives( clip ) );

		var rest = poser.World( 0 ).Translation;
		var apart = Vector3.Distance( poser.World( 1 ).Translation, rest );

		Near( new( 480.3f, 59f, 443.4f ), rest, "the plane at rest", 0.1f );
		Near( model.Nodes[0].WorldTransform.Translation, rest, "as the file composes it" );

		poser.Pose( clip, last );

		var flown = poser.World( 0 ).Translation;

		Near( rest + new Vector3( 0, 0, -353 ), flown, $"the plane on frame {last}", 0.1f );
		Assert.AreEqual( apart, Vector3.Distance( poser.World( 1 ).Translation, flown ), 0.01f, "the propeller keeps its distance" );
	}

	/// <summary>
	/// The bus's first clip carries its body, <c>_fbustemp</c> (node 0), along the model's route by a route key alone
	/// (channel <c>0x200</c>): on frame 0, 99.835 of the way round its open route's 14 segments, the route's point 42,
	/// (-183.50, 0, -284.52) within the root <c>Line02</c> at (480, 0, 170), which is where it rests, (296.5, 0, -114.5);
	/// on frame 100 it has turned the corner past the route's point 0, and its sound node 3 rides with it, turned with
	/// it. Read from the game's own <c>bus.wad</c>.
	/// </summary>
	[TestMethod]
	public void TheBusRidesItsRouteAndCarriesItsSoundNode()
	{
		var data = GameData.Required();

		using var modelStream = data.OpenRead( "levels/jungle/features/bus/Bus.MD2" )!;
		using var clipStream = data.OpenRead( "levels/jungle/features/bus/Busm1.MD2" )!;

		var model = new ModelFile( modelStream );
		var clip = new AnimationFile( clipStream );
		var poser = new NodePoser( model );

		Assert.IsTrue( NodePoser.Drives( clip ), "a route key is posed over the tree" );
		Near( new( 296.5f, 0f, -114.5f ), poser.World( 0 ).Translation, "the body at rest", 0.1f );
		Near( new( 296.3f, 5.67f, -114.2f ), poser.World( 3 ).Translation, "the sound node at rest", 0.1f );

		poser.Pose( clip, 0f );

		Near( new( 296.50f, 0f, -114.52f ), poser.World( 0 ).Translation, "the body on frame 0", 0.05f );
		Near( new( 296.26f, 5.67f, -114.16f ), poser.World( 3 ).Translation, "the sound node with it", 0.05f );

		poser.Pose( clip, 100f );

		Near( new( 605.25f, 5.67f, 39.35f ), poser.World( 3 ).Translation, "on frame 100", 0.05f );
	}

	/// <summary>
	/// <b>A change of clip on a channel hands the incoming clip over</b> (<c>RideAnimations.PoseChannel</c>, which
	/// <c>ParkObjects.Sweep</c>, <c>RideNodes.Follow</c> and the object window's preview all pose through): the plane flown
	/// to (0, 59, -77) stays there when a clip that also moves it, from its frame 5, takes the channel at frame 0. Rested
	/// with no incoming it would be back at (0, 59, 273).
	/// </summary>
	/// <remarks><b>Mutation:</b> the change rested with no incoming.</remarks>
	[TestMethod]
	public void AChannelsChangeOfClipHandsTheIncomingOver()
	{
		var poser = Tree();
		var flight = Clip( Flight() );
		RideAnimations.Posed posed = default;

		RideAnimations.PoseChannel( poser, ref posed, flight, 20 );
		Near( new( 0, 59, -77 ), poser.Local( 0 ).Translation, "flown" );

		var later = Clip( new AnimationFile.PositionTrack
		{
			TargetIndex = 0,
			FrameIndices = [5, 15],
			Points = [new( 0, 59, 0 ), new( 0, 59, 100 )],
			IsBezier = false
		} );

		RideAnimations.PoseChannel( poser, ref posed, later, 0 );
		Assert.AreSame( later, posed.Clip );
		Near( new( 0, 59, -77 ), poser.Local( 0 ).Translation, "kept until the incoming's first key" );

		RideAnimations.PoseChannel( poser, ref posed, Clip( Flight( 1 ) ), 0 );
		Near( new( 0, 59, 273 ), poser.Local( 0 ).Translation, "put back by a clip that moves another node" );
	}

	/// <summary>The tree's three nodes as meshes, each placed where the tree rests (the swizzle: the file's y is up, ours z).</summary>
	private static (ModelEntity[] Entities, OpenTPW.Vector3[] Offsets) Meshes( NodePoser poser )
	{
		var entities = new ModelEntity[poser.Count];
		var offsets = new OpenTPW.Vector3[poser.Count];

		for ( var i = 0; i < entities.Length; ++i )
		{
			var at = poser.World( i ).Translation;

			entities[i] = new ModelEntity();
			offsets[i] = new Vector3( at.X, at.Z, at.Y );
		}

		LobbyModel.Place( entities, offsets, Quaternion.Identity, Vector3.Zero );

		return (entities, offsets);
	}

	private static void Delete( ModelEntity[] entities )
	{
		foreach ( var entity in entities )
			entity.Delete();

		Entity.ApplyDeletions();
	}

	/// <summary>
	/// <b>A posed mesh is placed as the model was placed</b> (<c>LobbyModel.PlacePosed</c>): the resting plane, at
	/// (753, 59, 170) in the file, at scale 2 is (1506, 340, 118) in ours, a quarter turn about up takes that to
	/// (-340, 1506, 118), and the placement's (100, 200, 0) to (-240, 1706, 118); its turn carries the scale. Unturned it
	/// would stand at (1606, 540, 118); unscaled, at (-70, 953, 59).
	/// </summary>
	/// <remarks><b>Mutations:</b> the placement's turn left out; the scale left out.</remarks>
	[TestMethod]
	public void APosedMeshIsPlacedAsTheModelWas()
	{
		var poser = Tree();
		var (entities, _) = Meshes( poser );

		LobbyModel.PlacePosed( entities, poser, 2f, Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI / 2 ), new( 100, 200, 0 ) );

		Near( new( -240, 1706, 118 ), entities[0].Position.GetSystemVector3(), "the plane" );
		Assert.AreEqual( 2f, Vector3.Transform( Vector3.UnitX, entities[0].LinearTransform!.Value ).Length(), 0.001f, "scaled twice" );

		Delete( entities );
	}

	/// <summary>
	/// <b>A change of clip puts back only the meshes the outgoing clip moved</b> (<c>LobbyModel.RestMeshes</c>;
	/// <c>FUN_00472310</c> restores the outgoing clip's nodes alone): with the plane flown by one clip and the propeller
	/// moved by another, resting the propeller's clip puts the propeller back on the flown plane, (6, 7, 3) off it turned
	/// by the root, and leaves the plane where it was flown and the root where another channel's turn left it. A model with
	/// no tree puts back the propeller's rest offset and leaves the plane's place alone.
	/// </summary>
	/// <remarks><b>Mutation:</b> every mesh put back (the blanket reset and <c>Place</c>).</remarks>
	[TestMethod]
	public void AChangeOfClipPutsBackOnlyTheMeshesTheOutgoingMoved()
	{
		var poser = Tree();
		var (entities, offsets) = Meshes( poser );
		var parents = new[] { 2, 0, -1 };
		var plane = Clip( Flight() );
		var propeller = Clip( Flight( 1 ) );

		poser.Pose( plane, 20 );
		poser.Pose( propeller, 20 );
		LobbyModel.PlacePosed( entities, poser, 1f, Quaternion.Identity, Vector3.Zero );

		var flown = entities[0].Position.GetSystemVector3();

		Near( new( 480 - 77, 170, 59 ), flown, "the plane flown" );

		// The root as a clip that only turns, on another channel, would leave it (MeshRotator, outside the tree).
		entities[2].Position = new OpenTPW.Vector3( 7, 8, 9 );

		LobbyModel.RestMeshes( entities, offsets, parents, poser, 1f, Quaternion.Identity, Vector3.Zero, propeller, null );

		Near( flown, entities[0].Position.GetSystemVector3(), "the plane stays where it was flown" );
		Near( new( 7, 8, 9 ), entities[2].Position.GetSystemVector3(), "the root stays where another channel left it" );
		Near( flown + new Vector3( 3, -6, 7 ), entities[1].Position.GetSystemVector3(), "the propeller back on the plane" );

		// No tree: the propeller's rest offset back, the plane's place left as it is.
		entities[0].Position = new OpenTPW.Vector3( 1, 2, 3 );
		entities[1].Position = new OpenTPW.Vector3( 4, 5, 6 );

		LobbyModel.RestMeshes( entities, offsets, parents, null, 1f, Quaternion.Identity, Vector3.Zero, propeller, null );

		Near( new( 1, 2, 3 ), entities[0].Position.GetSystemVector3(), "the plane left" );
		Near( offsets[1].GetSystemVector3(), entities[1].Position.GetSystemVector3(), "the propeller at rest" );

		Delete( entities );
	}
}
