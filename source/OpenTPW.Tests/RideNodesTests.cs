using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The model nodes a ride script finds by id, and where the engine stores them - <see cref="RideNodes"/> and
/// <see cref="ModelFile.FindNode"/>.
///
/// <para>
/// <b>The positions are the original's own</b>, read from its memory with Alexah's Lost Kingdom save loaded in the
/// reference install under Proton (docs/exe/ride-operation.md, "How long a leg lasts, and where its ends are"): the
/// translation row the pose walk stored for each walk node, at each thing's saved cell and turn. The save stands
/// some of them on raised or sunk ground, and nothing here loads the ground, so only x and z are compared where it
/// does.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideNodesTests
{
	private BaseFileSystem _data = null!;

	[TestInitialize]
	public void MountTheGame() => _data = GameData.Required();

	private ParkItemCatalogue.Item Item( int id )
	{
		Assert.IsTrue( new ParkItemCatalogue( "jungle", _data ).TryGet( id, out var item ), $"catalogue item {id} should exist" );

		return item;
	}

	/// <summary>A thing's nodes, stood at a cell and a turn as <see cref="ParkRides"/> stands them.</summary>
	private RideNodes Stand( int catalogueId, int cellX, int cellY, int angle )
	{
		var item = Item( catalogueId );
		var clips = RideAnimations.Load( item.Directory, item.Stem, _data, item.AnimationChannels ).AllClips;
		var nodes = RideNodes.Load( item.Directory, item.Stem, _data, item.DoHeadProcessing, clips );

		Assert.IsNotNull( nodes, $"{item.Stem}'s model should read" );

		nodes.Place( ParkObjects.OriginFor( cellX, cellY, angle ), angle );

		return nodes;
	}

	private static void AssertStoredAt( RideNodes nodes, int id, uint space, float x, float z, NodeEnd expected )
	{
		Assert.AreEqual( expected, nodes.Find( id, space, out var at ), $"{nodes.Stem} id {id} in 0x{space:x}" );
		Assert.AreEqual( x, at.X, 5e-6f, $"{nodes.Stem} id {id}'s x against the original's stored matrix" );
		Assert.AreEqual( z, at.Z, 5e-6f, $"{nodes.Stem} id {id}'s z against the original's stored matrix" );
	}

	/// <summary>
	/// <b>Every turn the save uses puts a node where the original stores it</b>, to the float: nought, a half and
	/// three quarters, through a root that is itself turned (the Jungle Spray's and the Hyenas' <c>floor</c>) and one
	/// that is not. A turn applied the wrong way round, about the wrong point, or through the root's file translation
	/// rather than in place of it moves each of these by at least a tenth of a unit.
	/// </summary>
	[TestMethod]
	public void EveryTurnPutsTheWalkNodesWhereTheOriginalStoresThem()
	{
		var spray = Stand( 1303, 42, 25, 270 );

		AssertStoredAt( spray, 4, RideNodes.WalkSpace, 429.155945f, 265.127106f, NodeEnd.Posed );
		AssertStoredAt( spray, 1, RideNodes.WalkSpace, 421.995117f, 255.931137f, NodeEnd.Posed );
		AssertStoredAt( spray, 3, RideNodes.WalkSpace, 421.995117f, 274.323090f, NodeEnd.Posed );

		spray.Find( 4, RideNodes.WalkSpace, out var entrance );
		Assert.AreEqual( 0.983895f, entrance.Y, 5e-6f, "the Jungle Spray stands on flat ground at nought" );

		var hyenas = Stand( 1304, 30, 27, 180 );

		AssertStoredAt( hyenas, 4, RideNodes.WalkSpace, 285.990021f, 274.513702f, NodeEnd.Posed );
		AssertStoredAt( hyenas, 1, RideNodes.WalkSpace, 295.009857f, 278.962433f, NodeEnd.Posed );

		var god = Stand( 1106, 15, 26, 270 );

		AssertStoredAt( god, 2, RideNodes.WalkSpace, 139.364960f, 285f, NodeEnd.Posed );
		AssertStoredAt( god, 3, RideNodes.WalkSpace, 128.514893f, 275f, NodeEnd.Posed );

		var steak = Stand( 1208, 13, 32, 0 );

		AssertStoredAt( steak, 1, RideNodes.WalkSpace, 154.998825f, 321.039307f, NodeEnd.Posed );
		AssertStoredAt( steak, 2, RideNodes.WalkSpace, 152.097656f, 326.495117f, NodeEnd.Posed );
	}

	/// <summary>
	/// <b>The Aztec Mayhem's heads ride its seats, which its clips move</b>, so they are found at rest and said to be -
	/// and at rest is where the original had them stored after loading Alexah's save, which these positions are, and
	/// where one bought in its stock park had them at the start of every walk it made.
	/// </summary>
	[TestMethod]
	public void TheAztecMayhemsHeadsAreFoundAtRest()
	{
		var mayhem = Stand( 1104, 46, 33, 0 );

		AssertStoredAt( mayhem, 1, RideNodes.HeadSpace, 470.704193f, 347.201233f, NodeEnd.RestPose );
		AssertStoredAt( mayhem, 5, RideNodes.HeadSpace, 489.428375f, 347.363068f, NodeEnd.RestPose );
		AssertStoredAt( mayhem, 1, RideNodes.WalkSpace, 475f, 333f, NodeEnd.Posed );
		AssertStoredAt( mayhem, 2, RideNodes.WalkSpace, 485.148010f, 332.973358f, NodeEnd.Posed );
	}

	/// <summary>
	/// <b>A record the pose walk never stores is found but unposed</b> - the Inca God's heads, childless, in the head
	/// space with no bit the loader turns into a stored pose, on an item that does not set
	/// <c>Info.DoHeadProcessing</c>. The original read (0, 0, 0) for 31 of its 32, and held a position for the one it had
	/// attached a rider to. A negative id and an id nothing carries are told apart from it.
	/// </summary>
	[TestMethod]
	public void AnUnstoredRecordAMissAndANegativeIdAreToldApart()
	{
		var god = Stand( 1106, 15, 26, 270 );

		Assert.AreEqual( NodeEnd.Unposed, god.Find( 6, RideNodes.HeadSpace, out _ ), "head06 is never stored" );
		Assert.AreEqual( NodeEnd.Missing, god.Find( 99, RideNodes.WalkSpace, out _ ), "no record carries id 99" );
		Assert.AreEqual( NodeEnd.NegativeId, god.Find( -1, RideNodes.WalkSpace, out _ ) );
	}

	/// <summary>
	/// <b>An id is unique only together with its space</b>: the Aztec Mayhem's id 1 is <c>WALK1</c> as a walk node
	/// and <c>Head1</c> as a head. A mask sharing no bit with the spaces the lookup knows is swapped for
	/// <see cref="ModelFile.AnySpace"/> (<c>0x0044b22e</c>), which finds the first record with the id and any bit of it.
	/// </summary>
	[TestMethod]
	public void AnIdIsFoundInItsOwnSpace()
	{
		var item = Item( 1104 );
		var model = new ModelFile( new MemoryStream( _data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) ) );

		var walk = model.FindNode( 1, RideNodes.WalkSpace );
		var head = model.FindNode( 1, RideNodes.HeadSpace );

		Assert.AreEqual( "WALK1", model.Nodes[walk].Name.Trim() );
		Assert.AreEqual( "Head1", model.Nodes[head].Name.Trim() );

		var first = Enumerable.Range( 0, model.Nodes.Count ).First( node => model.Nodes[node].Id == 1
			&& (model.Nodes[node].IdFlags & ModelFile.AnySpace) != 0 );

		Assert.AreEqual( first, model.FindNode( 1, 0x4 ), "a mask of 4 alone is swapped for every known space" );
		Assert.AreEqual( -1, model.FindNode( 99, RideNodes.WalkSpace ) );
	}

	/// <summary>
	/// <b>The quarter turns take the engine's own sines</b> (<c>FUN_004708d0</c>): entry 2048 is not nought but
	/// <c>sin( π )</c> in the build's float π - the 90° turn's cosine and the 180° turn's sine.
	/// </summary>
	[TestMethod]
	public void TheQuarterTurnsAreTheEnginesTable()
	{
		Assert.AreEqual( 0f, RideNodes.Table( 0 ) );
		Assert.AreEqual( 1f, RideNodes.Table( 1024 ) );
		Assert.AreEqual( -8.742278e-8f, RideNodes.Table( 2048 ) );
		Assert.AreEqual( -1f, RideNodes.Table( 3072 ) );
	}

	/// <summary>
	/// <b>A head on a face of a morphing mesh is told apart</b>: hallow's Fire Pit's <c>Head01</c> carries <c>0x40</c>, and
	/// its parent, the seats, is morphed by the Fire Pit's own clips, so the engine takes it from a face of that mesh once
	/// a morph has played it (<see cref="NodeEnd.OnAFace"/>). No Lost Kingdom walk item carries one.
	/// </summary>
	[TestMethod]
	public void AHeadOnAMorphingFaceIsToldApart()
	{
		const string directory = "levels/hallow/rides/firepit";

		var clips = RideAnimations.Load( directory, "firepit", _data ).AllClips;
		var nodes = RideNodes.Load( directory, "firepit", _data, true, clips );

		Assert.IsNotNull( nodes, "the Fire Pit's model should read" );

		var model = new ModelFile( new MemoryStream( _data.ReadAllBytes( $"{directory}/firepit.MD2" ) ) );
		var head = model.Nodes.First( node => node.Name.Trim() == "Head01" );

		Assert.AreNotEqual( 0u, head.IdFlags & 0x40, "Head01 carries the face bit" );
		Assert.AreEqual( NodeEnd.OnAFace, nodes!.Find( head.Id, RideNodes.HeadSpace, out _ ) );
	}

	/// <summary>
	/// <b>An effect's node is kept as its lookup record</b> (<c>FUN_00557970</c>, record <c>+0x14</c>), found in the
	/// particles' space for types 1 and 2 and the sounds' for the rest, counted from the model's first lookup node, and
	/// -1 with no node. The pairs are the Full Simulation park file's own records: the Gift Shop's three smokes, the
	/// Staff Room's, the bus's engine, the Jungle Spray's water and the Inca God's two fires.
	/// </summary>
	[TestMethod]
	[DataRow( "shops/giftshop", "giftshop", 1, 3, 0 )]
	[DataRow( "shops/giftshop", "giftshop", 1, 2, 1 )]
	[DataRow( "shops/giftshop", "giftshop", 1, 1, 2 )]
	[DataRow( "features/staff", "staff", 1, 1, 0 )]
	[DataRow( "features/bus", "bus", 8, 1, 0 )]
	[DataRow( "sideshow/junspray", "junspray", 2, 2, 1 )]
	[DataRow( "rides/incagod", "incagod", 1, 1, 34 )]
	[DataRow( "rides/incagod", "incagod", 1, 2, 35 )]
	[DataRow( "rides/incagod", "incagod", 3, -1, -1 )]
	[DataRow( "features/staff", "staff", 7, -1, -1 )]
	public void AnEffectsNodeIsItsLookupRecordInItsTypesSpace( string folder, string stem, int type, int node, int index )
	{
		var nodes = RideNodes.Load( $"levels/jungle/{folder}", stem, _data, false, [] );

		Assert.IsNotNull( nodes, $"{stem}'s model should read" );
		Assert.AreEqual( index, nodes!.EffectIndex( type, node ) );
	}

	/// <summary>A particle's node and a sound's are looked for apart: the bus's node 1 is a sound's and no particle's.</summary>
	[TestMethod]
	public void AParticleAndASoundAreLookedForInTheirOwnSpaces()
	{
		var nodes = RideNodes.Load( "levels/jungle/features/bus", "bus", _data, false, [] )!;
		var shop = RideNodes.Load( "levels/jungle/shops/giftshop", "giftshop", _data, false, [] )!;

		Assert.AreEqual( 0, nodes.EffectIndex( 3, 1 ) );
		Assert.AreEqual( 0, nodes.EffectIndex( 10, 1 ) );
		Assert.AreNotEqual( 0, nodes.EffectIndex( 2, 1 ) );
		Assert.AreEqual( 2, shop.EffectIndex( 2, 1 ) );
		Assert.AreNotEqual( 2, shop.EffectIndex( 3, 1 ) );
	}

	/// <summary>
	/// <b>A node points along the third row of its stored matrix, a unit long</b>: the Jungle Spray's jet node as the
	/// original's files hold its emitter aimed, turned nought and three quarters; a record carrying <c>0x10</c>
	/// points the other way; and a matrix never stored has no direction.
	/// </summary>
	[TestMethod]
	public void ANodePointsAlongItsStoredMatrixsThirdRow()
	{
		Assert.AreEqual( NodeEnd.RestPose, Stand( 1303, 52, 31, 0 ).FindDirection( 2, RideNodes.ParticleSpace, out var north ) );
		Assert.AreEqual( NodeEnd.RestPose, Stand( 1303, 42, 25, 270 ).FindDirection( 2, RideNodes.ParticleSpace, out var west ) );

		Assert.AreEqual( new System.Numerics.Vector3( -0.011314017f, 0.64135724f, 0.767159f ), north );
		Assert.AreEqual( new System.Numerics.Vector3( -0.767159f, 0.64135724f, -0.011314017f ), west );
		Assert.AreEqual( 1f, north.Length(), 1e-6f );

		// The Well Drop's walk node 2 carries the flag and its node 1 does not.
		var well = RideNodes.Load( "levels/fantasy/rides/welldrop", "welldrop", _data, false, [] )!;

		well.Place( ParkObjects.OriginFor( 51, 30, 0 ), 0 );

		Assert.AreEqual( NodeEnd.Posed, well.FindDirection( 2, RideNodes.WalkSpace, out var about ) );
		Assert.AreEqual( NodeEnd.Posed, well.FindDirection( 1, RideNodes.WalkSpace, out var straight ) );
		Assert.AreEqual( new System.Numerics.Vector3( -8.742278e-08f, -0f, 1f ), about, "its row is turned about" );
		Assert.AreEqual( System.Numerics.Vector3.UnitZ, straight );

		var god = Stand( 1106, 15, 26, 270 );

		Assert.AreEqual( NodeEnd.Unposed, god.FindDirection( 6, RideNodes.HeadSpace, out var none ), "head06 is never stored" );
		Assert.AreEqual( System.Numerics.Vector3.Zero, none );
		Assert.AreEqual( NodeEnd.Missing, god.FindDirection( 99, RideNodes.WalkSpace, out _ ) );
		Assert.AreEqual( NodeEnd.NegativeId, god.FindDirection( -1, RideNodes.WalkSpace, out _ ) );
	}
	/// <summary>
	/// <b>A followed tree is posed as the drawn model is</b>: on <c>Endm1</c> the End's node 5, under plane 02, is carried down
	/// the park and found posed; a change of channel 0 to <c>Endm3</c>, which flies other planes, puts it back where it rests,
	/// as the engine puts back what the outgoing clip moved; and a find not asked for <c>carried</c> reads the rest throughout.
	/// Endm3's node 11 climbs away, so the way it points turns.
	/// </summary>
	/// <remarks><b>Mutations:</b> the change of clip putting nothing back; a carried find reading the rest.</remarks>
	[TestMethod]
	public void AFollowedTreeIsPosedAsTheDrawnModelIs()
	{
		const string End = "levels/jungle/features/end";

		var players = RideAnimations.Load( End, "end", _data, 1 );
		var nodes = RideNodes.Load( End, "end", _data, false, players.AllClips )!;

		nodes.PlaceFixed();
		nodes.Find( 5, RideNodes.ParticleSpace, out var rest );
		nodes.FindDirection( 11, RideNodes.ParticleSpace, out var climbingAtRest );

		players.Trigger( 5, 0, 0, 1f, 0 );
		players.Advance( 4000 );
		nodes.Follow( players );

		Assert.AreEqual( NodeEnd.Posed, nodes.Find( 5, RideNodes.ParticleSpace, out var flown, carried: true ) );
		Assert.IsTrue( rest.Z - flown.Z > 100f, $"plane 02 comes down the park: {rest.Z} to {flown.Z}" );
		Assert.AreEqual( NodeEnd.RestPose, nodes.Find( 5, RideNodes.ParticleSpace, out var uncarried ) );
		Assert.AreEqual( rest, uncarried, "a find not carried reads the rest" );

		players.Trigger( 5, 2, AnimTimeControl.StartAtOnceFlag, 1f, 4000 );
		players.Advance( 9000 );
		nodes.Follow( players );

		nodes.Find( 5, RideNodes.ParticleSpace, out var back, carried: true );
		Assert.AreEqual( rest, back, "Endm3 does not fly plane 02, and the change of clip put it back" );

		Assert.AreEqual( NodeEnd.Posed, nodes.FindDirection( 11, RideNodes.ParticleSpace, out var climbing, carried: true ) );
		Assert.IsTrue( System.Numerics.Vector3.Distance( climbingAtRest, climbing ) > 0.05f, $"node 11 turns as it climbs: {climbingAtRest} to {climbing}" );
	}

	/// <summary>
	/// The End's model as <c>LobbyModel</c> draws a clip that moves nodes, through the same statics: the node tree posed,
	/// each mesh placed from it (<c>PlacePosed</c>), and a change of clip putting back the outgoing's meshes
	/// (<c>RestMeshes</c>); standing at the origin unturned at scale 1, as the End's does.
	/// </summary>
	private sealed class Drawn : IChannelPosed
	{
		public readonly NodePoser Poser;
		public readonly ModelEntity[] Entities;
		private readonly int[] _parents;

		public Drawn( ModelFile model )
		{
			Poser = new NodePoser( model );
			Entities = [.. model.Meshes.Select( _ => new ModelEntity() )];
			_parents = [.. model.Nodes.Select( node => node.ParentIndex )];

			LobbyModel.PlacePosed( Entities, Poser, 1f, System.Numerics.Quaternion.Identity, Vector3.Zero );
		}

		public void Rest( AnimationFile outgoing, AnimationFile? incoming )
			=> LobbyModel.RestMeshes( Entities, new Vector3[Entities.Length], _parents, Poser, 1f,
				System.Numerics.Quaternion.Identity, Vector3.Zero, outgoing, incoming );

		public void Pose( AnimationFile clip, float frame )
		{
			Assert.IsTrue( NodePoser.Drives( clip ), "the End's clips are drawn through the tree" );

			Poser.Pose( clip, frame );
			LobbyModel.PlacePosed( Entities, Poser, 1f, System.Numerics.Quaternion.Identity, Vector3.Zero );
		}
	}

	/// <summary>The angle, in degrees, of the rotation in a matrix's upper three rows.</summary>
	private static float TurnAngle( System.Numerics.Matrix4x4 m )
		=> System.MathF.Acos( System.Math.Clamp( (m.M11 + m.M22 + m.M33 - 1f) / 2f, -1f, 1f ) ) * 180f / System.MathF.PI;

	/// <summary>
	/// <b>A node's drawn turn is its rest turn inside its mesh, carried by the turn that mesh is drawn with</b>
	/// (<c>LobbyModel.DrawnTurnInMesh</c>, the matrix a head's picture is chosen from, Q947): the End's nodes 5, 6 and 11
	/// and every Mumbo node under a mesh (its heads among them), each with the mesh it hangs in left as it rests and then turned 40 degrees about a slanted
	/// axis. Each axis of the turn is the node's axis in its mesh (file axes, y up), taken to the mesh's own (z up),
	/// through the mesh's draw matrix and back, worked here a row at a time; the place in it is the mesh's draw place; and
	/// the turn has moved by 40 degrees from the one at rest (a conjugation keeps the angle).
	/// </summary>
	/// <remarks><b>Mutations:</b> the mesh's drawn turn left out or put on the wrong side; the node's axes not taken into
	/// its mesh or not swapped back; the first ancestor taken where it is not a mesh; the mesh's place left out.</remarks>
	[TestMethod]
	public void ANodesDrawnTurnIsItsRestTurnCarriedByItsMeshsDrawnTurn()
	{
		const string End = "levels/jungle/features/end";

		var mumbo = Item( 1109 );
		var players = RideAnimations.Load( End, "end", _data, 1 );
		var plane = RideNodes.Load( End, "end", _data, false, players.AllClips )!;
		var plant = RideNodes.Load( mumbo.Directory, mumbo.Stem, _data, mumbo.DoHeadProcessing,
			RideAnimations.Load( mumbo.Directory, mumbo.Stem, _data, mumbo.AnimationChannels ).AllClips )!;
		var checkedNodes = 0;

		foreach ( var (model, indices) in new[]
		{
			(plane.Model, new[] { 5, 6, 11 }.Select( id => plane.Model.FindNode( id, RideNodes.ParticleSpace ) ).ToArray()),
			(plant.Model, Enumerable.Range( 0, plant.Model.Nodes.Count ).Where( i => HangsInAMesh( plant.Model, i ) ).ToArray()),
		} )
		{
			var drawn = new Drawn( model );

			foreach ( var index in indices )
			{
				var mesh = model.Nodes[index].ParentIndex;

				while ( mesh >= drawn.Entities.Length )
					mesh = model.Nodes[mesh].ParentIndex;

				var entity = drawn.Entities[mesh];
				var before = entity.Rotation;

				Assert.IsTrue( LobbyModel.DrawnTurnInMesh( model, drawn.Entities, index, out var rest ), $"node {index} hangs in a mesh" );
				Assert.AreEqual( 1f, rest.GetDeterminant(), 1e-3f, $"node {index}: a rigid turn" );

				entity.Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.Normalize( new( 0.3f, 0.5f, 0.8f ) ), 40f * System.MathF.PI / 180f );

				Assert.IsTrue( LobbyModel.DrawnTurnInMesh( model, drawn.Entities, index, out var turned ) );
				Assert.AreEqual( 40f, TurnAngle( turned * Invert( rest ) ), 0.5f, $"node {index}: moved by the angle its mesh was turned by" );

				// The same turn worked a row at a time: each of the node's axes in its mesh, to the mesh's own axes, drawn, and back.
				var within = model.Nodes[index].WorldTransform * Invert( model.Meshes[mesh].WorldTransform );
				var draw = entity.ModelMatrix;
				var rows = new[] { new System.Numerics.Vector3( within.M11, within.M12, within.M13 ), new( within.M21, within.M22, within.M23 ), new( within.M31, within.M32, within.M33 ) };

				for ( var axis = 0; axis < 3; ++axis )
				{
					var drawnAxis = System.Numerics.Vector3.TransformNormal( new( rows[axis].X, rows[axis].Z, rows[axis].Y ), draw );
					var got = axis == 0 ? new System.Numerics.Vector3( turned.M11, turned.M12, turned.M13 )
						: axis == 1 ? new( turned.M21, turned.M22, turned.M23 ) : new( turned.M31, turned.M32, turned.M33 );

					Assert.IsTrue( (got - new System.Numerics.Vector3( drawnAxis.X, drawnAxis.Z, drawnAxis.Y )).Length() < 1e-3f, $"node {index} axis {axis}: {got} against {drawnAxis}" );
				}

				Assert.IsTrue( (turned.Translation - new System.Numerics.Vector3( draw.M41, draw.M43, draw.M42 )).Length() < 1e-3f, $"node {index}: the mesh's draw place" );

				entity.Rotation = before;
				++checkedNodes;
			}
		}

		Assert.IsTrue( checkedNodes >= 8, $"three plane nodes and Mumbo's nodes were checked, not {checkedNodes}" );
		Assert.IsTrue( Enumerable.Range( 0, plant.Model.Nodes.Count ).Any( i => HangsInAMesh( plant.Model, i ) && plant.Model.Nodes[i].ParentIndex >= plant.Model.Meshes.Count ),
			"a Mumbo node hangs on a node that is not a mesh, inside one that is" );
	}

	/// <summary>
	/// <b>A head on a morphing face whose record carries <c>0x40000</c> takes the face's frame</b> (<c>LobbyModel.DrawnTurn</c>,
	/// <c>FUN_0044a640</c>, Q1003): Mumbo's Head02 to Head05, their tentacle drawn turned 40 degrees about a slanted axis, are
	/// <c>TurnToFace</c> of the face in the mesh, carried by the mesh's drawn turn in the file's axes; Head1, whose record lacks the
	/// flag, and every head whose parent no morph poses keep the turn inside the mesh. The face turn is about a degree from that at rest,
	/// so a head left on it is caught.
	/// </summary>
	/// <remarks><b>Mutations:</b> the flag test left out; the face turn not given; the frame built in the meshes' z-up axes; the mesh's
	/// drawn turn left out of the corners; the normal moved by the mesh's place.</remarks>
	[TestMethod]
	public void AHeadOnAMorphingFaceTakesTheFacesFrameThroughItsMeshsDrawnTurn()
	{
		var mumbo = Item( 1109 );
		var plant = RideNodes.Load( mumbo.Directory, mumbo.Stem, _data, mumbo.DoHeadProcessing,
			RideAnimations.Load( mumbo.Directory, mumbo.Stem, _data, mumbo.AnimationChannels ).AllClips )!;
		var model = plant.Model;
		var drawn = new Drawn( model );
		var swap = new System.Numerics.Matrix4x4( 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1 );
		var turnedHeads = 0;

		static void Same( System.Numerics.Matrix4x4 want, System.Numerics.Matrix4x4 got, string what )
		{
			float[] a = [want.M11, want.M12, want.M13, want.M21, want.M22, want.M23, want.M31, want.M32, want.M33, want.M41, want.M42, want.M43];
			float[] b = [got.M11, got.M12, got.M13, got.M21, got.M22, got.M23, got.M31, got.M32, got.M33, got.M41, got.M42, got.M43];

			for ( var i = 0; i < a.Length; ++i )
				Assert.AreEqual( a[i], b[i], 1e-3f, $"{what}: element {i}" );
		}

		for ( var id = 1; id <= 5; ++id )
		{
			var index = model.FindNode( id, RideNodes.HeadSpace );
			var node = model.Nodes[index];
			var mesh = node.ParentIndex;
			var rest = RideScriptHeadTests.RestSources( model.Meshes[mesh] );
			var entity = drawn.Entities[mesh];
			var before = entity.Rotation;

			entity.Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.Normalize( new( 0.3f, 0.5f, 0.8f ) ), 40f * System.MathF.PI / 180f );

			Assert.IsTrue( LobbyModel.DrawnTurnInMesh( model, drawn.Entities, index, out var inMesh ) );
			Assert.IsTrue( LobbyModel.DrawnTurn( model, drawn.Entities, index, null, out var unmorphed ) );
			Same( inMesh, unmorphed, $"{node.Name} on a parent no morph poses" );

			Assert.IsTrue( LobbyModel.DrawnTurn( model, drawn.Entities, index, source => rest[source], out var turned ) );

			if ( (node.IdFlags & 0x40000) == 0 )
			{
				Assert.AreEqual( 1, id, "only Head1 lacks the flag" );
				Same( inMesh, turned, $"{node.Name}, no 0x40000" );
			}
			else
			{
				var on = ModelFile.OnFace( model.Meshes[mesh], node.Face!.Value, source => rest[source], recomputeNormal: true )!.Value;
				var local = LobbyModel.TurnToFace( on.Corner.GetSystemVector3(), on.Point.GetSystemVector3(), on.Normal.GetSystemVector3(),
					node.Face!.Value.Turn.GetSystemVector3() );
				var carried = (local * ((swap * entity.ModelMatrix * swap) with { M41 = 0, M42 = 0, M43 = 0 }))
					with { M41 = inMesh.M41, M42 = inMesh.M42, M43 = inMesh.M43 };

				Same( carried, turned, $"{node.Name}: the face's frame carried by the tentacle" );
				Assert.IsTrue( TurnAngle( turned * Invert( inMesh ) ) > 0.5f, $"{node.Name}: the face turn is not the turn inside the mesh" );
				++turnedHeads;
			}

			entity.Rotation = before;
		}

		Assert.AreEqual( 4, turnedHeads, "Head02 to Head05 carry 0x40000" );
	}

	/// <summary>
	/// <b>A node's parent counts as morphing only where an animator that targets it is morphing</b> (<c>LobbyModel.MorphedParent</c>,
	/// what <c>TryGetDrawnNodeTurn</c> hands <c>DrawnTurn</c>): the morphing one's vertices, not another mesh's animator, not one at rest, and
	/// nothing where none targets the mesh.
	/// </summary>
	/// <remarks><b>Mutations:</b> the answer always null (the face turn never applied in the game); the <c>Morphing</c> test dropped.</remarks>
	[TestMethod]
	public void OnlyAMorphingAnimatorOfTheParentMeshPosesItsFace()
	{
		static MeshAnimator Animator( int target, bool morphing, float x )
		{
			var animator = (MeshAnimator)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( MeshAnimator ) );
			const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

			void Set( string field, object value ) => typeof( MeshAnimator ).GetField( field, any )!.SetValue( animator, value );

			Set( "_targetIndex", target );
			Set( "_morphed", morphing );
			Set( "_positions", new[] { new OpenTPW.Vector3( x, 0, 0 ) } );
			Set( "_restPositions", new[] { new OpenTPW.Vector3( -x, 0, 0 ) } );

			return animator;
		}

		MeshAnimator[] animators = [Animator( 2, true, 7 ), Animator( 3, false, 9 )];

		Assert.AreEqual( 7f, LobbyModel.MorphedParent( animators, 2 )!( 0 ).X, "the morphing animator of mesh 2 gives its morphed vertices" );
		Assert.IsNull( LobbyModel.MorphedParent( animators, 3 ), "an animator at rest does not pose the face" );
		Assert.IsNull( LobbyModel.MorphedParent( animators, 4 ), "no animator targets mesh 4" );
	}

	/// <summary>
	/// <b>A head's picture is chosen by the turn its node is drawn with, not the stored matrix</b> (<c>ParkGuestSprites.HeadPicture</c>,
	/// <c>HeadOnRide</c>, Q1003): the same camera over a stored matrix that sees it from above (picture 0) and a drawn turn of 90 degrees about x
	/// that sees it level from behind (picture 28); with no drawn turn the stored matrix picks.
	/// </summary>
	/// <remarks><b>Mutations:</b> the drawn turn ignored (the stored matrix again, as before Q1003).</remarks>
	[TestMethod]
	public void AHeadsPictureIsChosenByTheDrawnTurnNotTheStoredMatrix()
	{
		var stored = System.Numerics.Matrix4x4.Identity;
		var drawn = System.Numerics.Matrix4x4.CreateRotationX( System.MathF.PI / 2 );
		var at = new OpenTPW.Vector3( 0, 0, 0 );
		var camera = new OpenTPW.Vector3( 0, 0, 10 );

		Assert.AreEqual( 0, ParkGuestSprites.HeadPicture( stored, null, at, camera ), "no drawn turn: the stored matrix, camera straight above" );
		Assert.AreEqual( 28, ParkGuestSprites.HeadPicture( stored, drawn, at, camera ), "the drawn turn picks, not the stored matrix" );
	}

	/// <summary>Whether node <paramref name="index"/> has a mesh among its ancestors.</summary>
	private static bool HangsInAMesh( ModelFile model, int index )
	{
		var node = model.Nodes[index].ParentIndex;

		for ( var guard = model.Nodes.Count; node >= model.Meshes.Count && guard > 0; --guard )
			node = model.Nodes[node].ParentIndex;

		return node >= 0 && node < model.Meshes.Count;
	}

	private static System.Numerics.Matrix4x4 Invert( System.Numerics.Matrix4x4 m )
	{
		Assert.IsTrue( System.Numerics.Matrix4x4.Invert( m, out var inverse ) );

		return inverse;
	}

	/// <summary>
	/// <b>A followed node stands where the drawn model draws it</b>: the End's trail nodes 5 and 6 and plane 06's climbing
	/// node 11, from one set of players, found by <see cref="RideNodes.Follow"/> (its own composition, the engine's
	/// float order) and drawn as <c>LobbyModel</c> draws them (each mesh from the posed tree, the node inside its nearest
	/// mesh, <c>LobbyModel.DrawnInMesh</c>), at rest, 1.5 s and 4 s into Endm1, and 5 s into Endm3 after the change of
	/// clip. Agreeing within a hundredth of a unit each time, the y and z of one read as the z and y of the other.
	/// </summary>
	/// <remarks><b>Mutations:</b> Follow posing a stale clip after the change (no Rest); a drawn mesh placed unswizzled.</remarks>
	[TestMethod]
	public void AFollowedNodeStandsWhereTheDrawnModelDrawsIt()
	{
		const string End = "levels/jungle/features/end";

		var players = RideAnimations.Load( End, "end", _data, 1 );
		var nodes = RideNodes.Load( End, "end", _data, false, players.AllClips )!;
		var drawn = new Drawn( nodes.Model );
		var posed = new RideAnimations.Posed[1];

		nodes.PlaceFixed();

		void Agree( string when )
		{
			nodes.Follow( players );
			players.PoseOnto( drawn, posed );

			foreach ( var id in new[] { 5, 6, 11 } )
			{
				nodes.Find( id, RideNodes.ParticleSpace, out var followed, carried: true );

				var index = nodes.Model.FindNode( id, RideNodes.ParticleSpace );

				Assert.IsTrue( LobbyModel.DrawnInMesh( nodes.Model, drawn.Entities, index, out var at ), $"node {id} hangs in a mesh" );
				Assert.IsTrue( System.Numerics.Vector3.Distance( new( followed.X, followed.Z, followed.Y ), at.GetSystemVector3() ) < 0.01f,
					$"{when}: node {id} followed at {followed}, drawn at {at}" );
			}
		}

		nodes.Find( 5, RideNodes.ParticleSpace, out var rest );

		players.Trigger( 5, 0, 0, 1f, 0 );
		players.Advance( 0 );
		Agree( "at rest" );

		players.Advance( 1500 );
		Agree( "1.5 s into Endm1" );

		players.Advance( 4000 );
		Agree( "4 s into Endm1" );

		nodes.Find( 5, RideNodes.ParticleSpace, out var flown, carried: true );
		Assert.IsTrue( rest.Z - flown.Z > 100f, $"plane 02 has come down the park: {rest.Z} to {flown.Z}" );

		players.Trigger( 5, 2, AnimTimeControl.StartAtOnceFlag, 1f, 4000 );
		players.Advance( 9000 );
		Agree( "5 s into Endm3" );
	}

	/// <summary>A model, or null for a member that is a clip: <see cref="ModelFile"/> refuses one (the clips share the extension).</summary>
	private static ModelFile? ReadModelOrNull( Stream stream )
	{
		try
		{
			return new ModelFile( stream );
		}
		catch ( InvalidDataException )
		{
			return null;
		}
	}

	/// <summary>
	/// <b>No clip of the game turns a ride head's node, or one between it and its mesh</b> (Q1009), which is what
	/// <see cref="ParkGuestSprites.HeadOnRide"/> relies on in choosing a head's picture by its rest turn inside the mesh it hangs on. Over every
	/// model of the four themes' ride and feature archives that has a head node (80, each theme's ferry and the gokarts' four included) and
	/// every clip beside it (<c>&lt;stem&gt;*.MD2</c>), each head node's parent is a mesh, so no node stands between a head and its mesh, and the
	/// only track any clip has on a head node is a visibility track: 84 of them (80 in clips <c>c</c>, 4 in the ferries' <c>m1</c>), none a turn, a move, a
	/// scale, a route or a morph. The heads are the run of ids from 1, as <see cref="RideNodes.HeadCount"/> counts them (the engine stops at the first gap).
	/// </summary>
	[TestMethod]
	public void NoClipTurnsAHeadNodeOrOneBetweenItAndItsMesh()
	{
		string[] themes = ["jungle", "fantasy", "hallow", "space"];
		var (models, heads, visibility) = (0, 0, 0);
		var others = new System.Collections.Generic.List<string>();

		foreach ( var theme in themes )
		{
			foreach ( var wad in Directory.GetFiles( Path.Combine( GameDir.Data, "levels", theme ), "*.wad", SearchOption.AllDirectories ) )
			{
				var directory = Path.GetRelativePath( GameDir.Data, Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
				var members = _data.GetFiles( directory ).Select( Path.GetFileName ).OfType<string>().Where( name => name.EndsWith( ".md2", System.StringComparison.OrdinalIgnoreCase ) ).ToArray();

				foreach ( var member in members )
				{
					using var stream = _data.OpenReadOrNull( $"{directory}/{member}" );
					var model = stream == null ? null : ReadModelOrNull( stream );

					if ( model == null || model.Meshes.Count == 0 || model.FindNode( 1, RideNodes.HeadSpace ) < 0 )
						continue;

					++models;

					var stem = Path.GetFileNameWithoutExtension( member );
					var headNodes = new System.Collections.Generic.List<int>();

					for ( var id = 1; model.FindNode( id, RideNodes.HeadSpace ) is var node and >= 0; ++id )
					{
						headNodes.Add( node );
						Assert.IsTrue( model.Nodes[node].ParentIndex >= 0 && model.Nodes[node].ParentIndex < model.Meshes.Count,
							$"{directory}/{member}: head {id} hangs on a mesh, with no node between" );
					}

					heads += headNodes.Count;

					foreach ( var clipName in members.Where( name => name != member && name.StartsWith( stem, System.StringComparison.OrdinalIgnoreCase ) ) )
					{
						using var clipStream = _data.OpenReadOrNull( $"{directory}/{clipName}" );

						if ( clipStream == null )
							continue;

						var clip = new AnimationFile( clipStream );

						foreach ( var (node, flags) in clip.Tracks.Where( track => headNodes.Contains( track.Node ) ) )
						{
							if ( flags == 0x20000 )
								++visibility;
							else
								others.Add( $"{directory}/{clipName} node {node} flags 0x{flags:x}" );
						}
					}
				}
			}
		}

		CollectionAssert.AreEqual( System.Array.Empty<string>(), others, "no clip has any track but a visibility track (flags 0x20000) on a head node" );
		Assert.AreEqual( 80, models, "the models with heads" );
		Assert.AreEqual( 84, visibility, "the visibility tracks on a head node: the one track a head node has" );
		Assert.AreEqual( 792, heads, "the head nodes read" );
	}
}
