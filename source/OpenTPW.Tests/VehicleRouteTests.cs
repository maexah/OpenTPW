using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A route key (channel <c>0x200</c>) and its facing (<c>0x400</c>), as <c>Anim_PoseTrack</c> runs them
/// (docs/exe/park.md, "Clip content"; FileFormats models.md, "Paths" and "Path progress"): an open route's segments one
/// fewer than its points make, the node turned to face along the tangent from (0, 1, 0), the route its parent path node
/// names, and two keys more than 50 apart blended the short way round. The game's figures were worked out apart from
/// this code, by the decoded rules (<c>q583/expect.py</c>); the bus's stops are where the original's bus voice stood (Q562).
/// </summary>
[TestClass]
public class VehicleRouteTests
{
	private static void Near( Vector3 expected, Vector3 actual, string what, float within = 0.05f )
		=> Assert.IsTrue( Vector3.Distance( expected, actual ) < within, $"{what}: expected {expected}, got {actual}" );

	private static Vector3 Row( Matrix4x4 m, int row ) => row switch
	{
		0 => new( m.M11, m.M12, m.M13 ),
		1 => new( m.M21, m.M22, m.M23 ),
		_ => new( m.M31, m.M32, m.M33 )
	};

	private static ModelFile.ModelPath Route( uint type, params Vector3[] points )
		=> new() { Type = type, Points = [.. points.Select( point => new OpenTPW.Vector3( point.X, point.Y, point.Z ) )] };

	private static (ModelFile Model, AnimationFile Clip) Load( BaseFileSystem data, string folder, string model, string clip )
	{
		using var modelStream = data.OpenReadOrNull( $"{folder}/{model}" )!;
		using var clipStream = data.OpenReadOrNull( $"{folder}/{clip}" )!;

		return (new ModelFile( modelStream ), new AnimationFile( clipStream ));
	}

	/// <summary>
	/// The seaplane at its stop, <c>Seaplanem1</c>'s last frame (353, 50.776 of the way): its body <c>_fplane</c> at
	/// (534.88, -6.41, 5.18), level, nose along the route to -x, where the original's lies (Q583). Its route is open, 33
	/// points and 10 segments; counted as 11 it stood at (461.4, -6.41, 4.78), and unturned it kept its banked rest turn,
	/// up (0.22, 0.97, -0.11), with its propeller at (450.7, 6.0, 8.8).
	/// </summary>
	/// <remarks><b>Mutations:</b> the open route's segment left uncounted; the facing not applied.</remarks>
	[TestMethod]
	public void TheSeaplaneStopsLevelWhereTheOriginalsDoes()
	{
		var data = GameData.Required();
		var (model, clip) = Load( data, "levels/jungle/features/seaplane", "Seaplane.MD2", "Seaplanem1.MD2" );
		var poser = new NodePoser( model );

		Assert.IsTrue( NodePoser.Drives( clip ), "a route key is posed over the tree" );

		poser.Pose( clip, 353f );

		var body = poser.World( 0 );

		Near( new( 534.881f, -6.409f, 5.178f ), body.Translation, "the body at its stop" );
		Near( new( -0.9999f, 0f, -0.0103f ), Row( body, 2 ), "its nose along the route", 0.001f );
		Near( new( 0f, 1f, 0f ), Row( body, 1 ), "level", 0.001f );
		Near( new( -0.0103f, 0f, 0.9999f ), Row( body, 0 ), "its side", 0.001f );
		Near( new( 530.601f, 4.980f, -3.353f ), poser.World( 1 ).Translation, "its propeller turned with it" );
		Near( new( 530.565f, -2.076f, 5.479f ), poser.World( 5 ).Translation, "Dummy01 with it" );
	}

	/// <summary>
	/// The bus's stops, <c>Busm1</c>'s last frame (220, 142.437) and <c>Busm2</c>'s (260, 55.997): its sound node at
	/// (529.19, 5.68, 64.85) and (439.96, 5.69, 65.22), where the original's bus voice stood, (529, 64) and (439, 65)
	/// (Q562); counted as 15 segments they were (510, 65) and (422, 65). On <c>Busm1</c>'s frame 100 (122.968) the body
	/// faces (-0.540, 0, 0.841) along the road.
	/// </summary>
	[TestMethod]
	public void TheBusStopsWhereTheOriginalsVoiceStood()
	{
		var data = GameData.Required();
		var (model, first) = Load( data, "levels/jungle/features/bus", "Bus.MD2", "Busm1.MD2" );
		var (_, second) = Load( data, "levels/jungle/features/bus", "Bus.MD2", "Busm2.MD2" );
		var poser = new NodePoser( model );

		poser.Pose( first, 100f );

		Near( new( 604.879f, 0f, 39.144f ), poser.World( 0 ).Translation, "the body on frame 100" );
		Near( new( -0.5403f, 0f, 0.8414f ), Row( poser.World( 0 ), 2 ), "facing along the road", 0.001f );
		Near( new( 605.252f, 5.670f, 39.354f ), poser.World( 3 ).Translation, "the sound node with it" );

		poser.Pose( first, 220f );

		Near( new( 529.194f, 5.677f, 64.849f ), poser.World( 3 ).Translation, "the first stop" );

		poser.Rest( first, second );
		poser.Pose( second, 260f );

		Near( new( 439.955f, 5.692f, 65.216f ), poser.World( 3 ).Translation, "the second stop" );
	}

	/// <summary>
	/// The haunted house's four carts each follow the route their parent path node names (<c>Kart_path01</c> to
	/// <c>04</c> hold 0 to 3; every cart's own <c>+0x52</c> holds 0): on <c>hauntM</c>'s frame 100 they stand at four
	/// places a quarter of the loop apart, where on the one route all four would stand together.
	/// </summary>
	[TestMethod]
	public void EachCartFollowsItsParentsRoute()
	{
		var data = GameData.Required();
		var (model, clip) = Load( data, "levels/hallow/rides/haunt", "haunt.MD2", "hauntM.MD2" );
		var poser = new NodePoser( model );

		poser.Pose( clip, 100f );

		Near( new( 21.350f, 13.355f, 41.649f ), poser.World( 12 ).Translation, "the first cart" );
		Near( new( 40.219f, 13.575f, 24.086f ), poser.World( 13 ).Translation, "the second cart" );
		Near( new( 32.072f, 15.440f, 12.342f ), poser.World( 14 ).Translation, "the third cart" );
		Near( new( 9.918f, 13.172f, 26.121f ), poser.World( 15 ).Translation, "the fourth cart" );
	}

	/// <summary>
	/// A route key follows the route its node's parent names only where that parent is a path node (flag <c>0x8</c>): the
	/// seaplane's body hangs off <c>Flightpath</c> (route 0) and its propeller off the body, which is not one; the haunted
	/// house's carts hang off <c>Kart_path01</c> to <c>04</c>, routes 0 to 3, where each cart's own <c>+0x52</c> holds 0.
	/// </summary>
	/// <remarks><b>Mutations:</b> the parent's flag untested; the node's own path id read.</remarks>
	[TestMethod]
	public void ARouteIsNamedByAParentPathNode()
	{
		var data = GameData.Required();
		var (seaplane, _) = Load( data, "levels/jungle/features/seaplane", "Seaplane.MD2", "Seaplanem1.MD2" );
		var (haunt, _) = Load( data, "levels/hallow/rides/haunt", "haunt.MD2", "hauntM.MD2" );

		Assert.AreEqual( 0, NodePoser.RouteOf( seaplane, seaplane.Nodes[0] ), "the body" );
		Assert.AreEqual( -1, NodePoser.RouteOf( seaplane, seaplane.Nodes[1] ), "the propeller" );
		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3 }, Enumerable.Range( 12, 4 ).Select( cart => NodePoser.RouteOf( haunt, haunt.Nodes[cart] ) ).ToArray(), "the carts" );
	}

	/// <summary>
	/// Six Bezier points, 75 of the way: a closed route has two segments and stands half into the second, which runs
	/// from point 3 back to point 0, at (2, -3, 0.125), as an open one counted the same would; an open one has one and stands
	/// three quarters into it, at (3.375, 2.25, 0.421875).
	/// </summary>
	[TestMethod]
	public void AClosedRouteRunsBackToItsFirstPointAndAnOpenOneDoesNot()
	{
		Vector3[] points = [new( 0, 0, 0 ), new( 0, 4, 0 ), new( 4, 4, 0 ), new( 4, 0, 1 ), new( 4, -4, 0 ), new( 0, -4, 0 )];

		Near( new( 2f, -3f, 0.125f ), LobbyModel.SampleRoute( Route( 3, points ), 75f ), "closed", 0.001f );
		Near( new( 3.375f, 2.25f, 0.421875f ), LobbyModel.SampleRoute( Route( 2, points ), 75f ), "open", 0.001f );

		// Past 100 and below nought the percentage wraps: -25 and 175 are 75.
		Near( new( 3.375f, 2.25f, 0.421875f ), LobbyModel.SampleRoute( Route( 2, points ), -25f ), "below nought", 0.001f );
		Near( new( 3.375f, 2.25f, 0.421875f ), LobbyModel.SampleRoute( Route( 2, points ), 175f ), "past 100", 0.001f );
	}

	/// <summary>
	/// Four straight points open make three segments: halfway is (10, 5, 0) and the step there (0, 10, 0); 40 of the way
	/// is a fifth into the second segment, (10, 2, 0).
	/// </summary>
	[TestMethod]
	public void AStraightRouteHasOneSegmentFewerThanItsPoints()
	{
		var route = Route( 8, new( 0, 0, 0 ), new( 10, 0, 0 ), new( 10, 10, 0 ), new( 0, 10, 0 ) );

		Near( new( 10, 5, 0 ), LobbyModel.SampleRoute( route, 50f ), "halfway", 0.001f );
		Near( new( 10, 2, 0 ), LobbyModel.SampleRoute( route, 40f ), "a fifth into the second", 0.001f );
		Near( new( 0, 10, 0 ), LobbyModel.RouteTangent( route, 50f ), "the step", 0.001f );
	}

	/// <summary>The Bezier's tangent is its first derivative: it points as two points either side of it do.</summary>
	[TestMethod]
	public void ABezierTangentIsTheWayTheRouteRuns()
	{
		var route = Route( 2, new( 0, 0, 0 ), new( 0, 4, 0 ), new( 4, 4, 0 ), new( 4, 0, 1 ), new( 4, -4, 0 ), new( 0, -4, 0 ) );

		foreach ( var percent in new[] { 10f, 40f, 63f, 90f } )
		{
			var step = LobbyModel.SampleRoute( route, percent + 0.01f ) - LobbyModel.SampleRoute( route, percent - 0.01f );
			var tangent = LobbyModel.RouteTangent( route, percent );

			// One segment: a percentage of 0.02 is t of 0.0002, so the step is the derivative times that.
			Near( tangent * 0.0002f, step, $"at {percent}", 0.0005f );
		}
	}

	/// <summary>
	/// The facing's rows (<c>Matrix_FaceAlongTangent</c>) for a tangent along z: from the up row (0, 1, 0), side x and up y; from a
	/// rotation key's up (0.707, 0.707, 0), side (0.707, -0.707, 0) and up (0.707, 0.707, 0). A tangent of nought leaves the turn.
	/// </summary>
	/// <remarks><b>Mutations:</b> the key's up row ignored; the side not normalised.</remarks>
	[TestMethod]
	public void TheFacingIsBuiltFromTheUpRow()
	{
		var tilted = Matrix4x4.CreateRotationZ( -MathF.PI / 4 );
		var along = new Vector3( 0, 0, 2 );

		var plain = NodePoser.FacingAlong( tilted, along, keyed: false );

		Near( Vector3.UnitX, Row( plain, 0 ), "side", 0.001f );
		Near( Vector3.UnitY, Row( plain, 1 ), "up", 0.001f );
		Near( Vector3.UnitZ, Row( plain, 2 ), "forward, normalised", 0.001f );

		var keyed = NodePoser.FacingAlong( tilted, along, keyed: true );
		var half = MathF.Sqrt( 0.5f );

		Near( new( half, half, 0 ), Row( tilted, 1 ), "the key's up row" );
		Near( new( half, -half, 0 ), Row( keyed, 0 ), "side from the key", 0.001f );
		Near( new( half, half, 0 ), Row( keyed, 1 ), "up from the key", 0.001f );

		Assert.AreEqual( tilted, NodePoser.FacingAlong( tilted, Vector3.Zero, keyed: false ), "no tangent, no turn" );

		// Climbing at 45 degrees the side is still a unit x, and the up leans back, (0, 0.707, -0.707).
		var climbing = NodePoser.FacingAlong( tilted, new( 0, 3, 3 ), keyed: false );

		Near( Vector3.UnitX, Row( climbing, 0 ), "side, normalised", 0.001f );
		Near( new( 0, half, -half ), Row( climbing, 1 ), "up, leaning back", 0.001f );
	}

	/// <summary>Two keys more than 50 apart are blended the short way round: 10 and 95 halfway is 2.5, not 52.5.</summary>
	/// <remarks><b>Mutation:</b> the plain blend kept for keys far apart.</remarks>
	[TestMethod]
	public void FarKeysBlendTheShortWayRound()
	{
		Assert.AreEqual( 2.5f, new AnimationFile.PathTrack { Values = [10f, 95f] }.Sample( 0.5f ), 0.001f, "rising" );
		Assert.AreEqual( 2.5f, new AnimationFile.PathTrack { Values = [95f, 10f] }.Sample( 0.5f ), 0.001f, "falling" );
		Assert.AreEqual( 30f, new AnimationFile.PathTrack { Values = [10f, 50f] }.Sample( 0.5f ), 0.001f, "near keys" );
		Assert.AreEqual( 97.125f, new AnimationFile.PathTrack { Values = [95f, 3.5f] }.Sample( 0.25f ), 0.001f, "a quarter" );
	}

	/// <summary>
	/// A route clip is drawn through the node tree: <c>LobbyModel.Pose</c> of the bus's <c>Busm1</c> (a route key, no
	/// position key) on frame 100 puts the body's mesh at the place <c>NodePoser</c> finds, (604.88, 0, 39.14) in the
	/// file's axes and so (604.88, 39.14, 0) swizzled, with its turn; left to the rotation poser it stays unposed.
	/// </summary>
	/// <remarks><b>Mutation:</b> the dispatch keyed on <c>PositionTracks.Count &gt; 0</c> rather than <c>NodePoser.Drives</c>.</remarks>
	[TestMethod]
	public void TheDrawnBusIsPosedOnItsRoute()
	{
		Log ??= new();

		var data = GameData.Required();
		var (file, clip) = Load( data, "levels/jungle/features/bus", "Bus.MD2", "Busm1.MD2" );

		Assert.AreEqual( 0, clip.PositionTracks.Count, "the bus's clip moves by its route alone" );
		Assert.IsTrue( clip.PathTracks.Count > 0 );

		var model = (LobbyModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		var meshes = file.Meshes.Select( _ => (ModelEntity)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( ModelEntity ) ) ).ToArray();

		void Set( string field, object value ) => typeof( LobbyModel ).GetField( field,
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.SetValue( model, value );

		Set( "_file", file );
		Set( "_modelPath", "Bus.MD2" );
		Set( "_scale", 1f );
		Set( "_placedRotation", Quaternion.Identity );
		Set( "<Entities>k__BackingField", meshes );
		Set( "<Clips>k__BackingField", new[] { clip } );
		Set( "<Animators>k__BackingField", Array.Empty<MeshAnimator>() );

		model.Pose( clip, 100f );

		var expected = new NodePoser( file );

		expected.Pose( clip, 100f );

		Assert.IsNotNull( meshes[0].LinearTransform, "the body is turned" );
		Near( new( 604.879f, 39.144f, 0f ), new( meshes[0].Position.X, meshes[0].Position.Y, meshes[0].Position.Z ), "the body on frame 100" );
		Near( expected.World( 3 ).Translation, model.Posed( 3 )!.Value.Translation, "the sound node is posed with it" );
	}

	/// <summary>
	/// A route key on a node that also has a rotation key faces along the route from the key's up row, not from (0, 1, 0)
	/// (<c>0x004720aa</c>, the track's own rotation descriptor). A cart under a path node, keyed a quarter turn of 45 degrees
	/// about z, on a route running along z: its rows are side (0.707, 0.707, 0), up (-0.707, 0.707, 0) and forward (0, 0, 1);
	/// from (0, 1, 0) they would be (1, 0, 0), (0, 1, 0) and (0, 0, 1). The shipped clips cannot show it: the haunted
	/// house's <c>hauntc</c>, the one keyed route clip, keys carts that keep their up row at (0, 1, 0).
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>NodePoser.Turned</c> answering false.</remarks>
	[TestMethod]
	public void ARouteKeyOnAKeyedNodeFacesFromTheKeysUp()
	{
		var half = MathF.Sqrt( 0.5f );
		var route = Route( 2, new( 0, 0, 0 ), new( 0, 0, 10 ), new( 0, 0, 20 ), new( 0, 0, 30 ), new( 0, 0, 40 ), new( 0, 0, 50 ), new( 0, 0, 60 ) );
		var poser = new NodePoser( [Matrix4x4.Identity, Matrix4x4.Identity], [-1, 0], 0, [route], [-1, 0] );

		var clip = (AnimationFile)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( AnimationFile ) );

		foreach ( var property in new[] { "PositionTracks", "RotationTracks", "PathTracks", "ScaleTracks" } )
		{
			var field = typeof( AnimationFile ).GetField( $"<{property}>k__BackingField",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!;
			field.SetValue( clip, field.FieldType.GetConstructor( Type.EmptyTypes )!.Invoke( null ) );
		}

		var tilt = Quaternion.CreateFromAxisAngle( Vector3.UnitZ, MathF.PI / 4 );

		clip.RotationTracks.Add( new() { TargetIndex = 1, FrameIndices = [0, 10], Rotations = [tilt, tilt], CurveIds = [AnimationFile.RotationTrack.NoCurve, AnimationFile.RotationTrack.NoCurve] } );
		clip.PathTracks.Add( new() { TargetIndex = 1, Values = [10f, 20f, 30f, 40f, 50f, 60f], OrientsAlongRoute = true } );

		poser.Pose( clip, 3f );

		var cart = poser.Local( 1 );

		Near( new( 0, 0, 12f ), cart.Translation, "40 of the way along its one segment, points 0 to 3", 0.01f );
		Near( Vector3.UnitZ, Row( cart, 2 ), "forward along z", 0.001f );
		Near( new( half, half, 0 ), Row( cart, 0 ), "side, from the key's up", 0.001f );
		Near( new( -half, half, 0 ), Row( cart, 1 ), "up, the key's", 0.001f );
	}
}
