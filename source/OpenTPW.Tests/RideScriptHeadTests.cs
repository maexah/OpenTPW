using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// <c>ADDHEAD</c> and <c>DELHEAD</c>, and the head table they keep (<c>docs/exe/park.md</c>, "The head table"):
/// <c>ADDHEAD</c> (<c>0x00554c3e</c>) draws a free slot, <c>DELHEAD</c> (<c>0x00554d26</c>) frees every slot holding
/// the visitor, and neither writes the result register. The six Lost Kingdom rides that carry them are measured from
/// their own models where the game is installed.
/// </summary>
[TestClass]
public class RideScriptHeadTests
{
	internal static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static int Var( int index ) => unchecked( (int)(0x40000000u | (uint)index) );

	/// <summary>A whole .RSE file of <paramref name="body"/>, three variables and no slots of any other kind.</summary>
	internal static RideScriptFile Build( params int[] body )
	{
		const int variableCount = 3;

		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( variableCount );
		writer.Write( 0 );              // stack
		writer.Write( 50 );             // time slice
		writer.Write( 0 );              // limbo records
		writer.Write( 0 );              // bounce records
		writer.Write( 0 );              // walk records
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );              // string blob length

		for ( int i = 0; i < variableCount; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>The slot <c>ADDHEAD</c>'s draw names: the generator's magnitude halved, modulo the slot count.</summary>
	private static int SlotDrawn( ref uint state, int slots ) => (int)(ParkGenerator.Draw( ref state ) >> 1) % slots;

	/// <summary>
	/// <b>Each visitor goes on the slot the generator names</b>, a taken slot drawn again, one draw a try: the second
	/// visitor's slot is the first draw after the first's that names a free one.
	/// </summary>
	[TestMethod]
	public void EachVisitorGoesOnTheFreeSlotTheGeneratorDraws()
	{
		const uint seed = 12345;
		const int slots = 5;

		var script = new RideScript( Build(
			Word( Opcode.ADDHEAD ), 7,
			Word( Opcode.ADDHEAD ), 8,
			Word( Opcode.ADDHEAD ), 9,
			Word( Opcode.END ) ) );

		script.SizeHeads( slots );
		script.SeedRandom( seed );
		script.Turn( 0f );

		var state = seed;
		var expected = new int[slots];

		foreach ( var visitor in new[] { 7, 8, 9 } )
		{
			int slot;

			do
				slot = SlotDrawn( ref state, slots );
			while ( expected[slot] != 0 );

			expected[slot] = visitor;
		}

		var held = new int[slots];

		foreach ( var (node, handle, _) in script.Heads() )
			held[node - 1] = handle;

		CollectionAssert.AreEqual( expected, held, "each visitor on the slot the draws name" );
	}

	/// <summary><b>With no free slot, or no table, <c>ADDHEAD</c> does nothing</b> (<c>0x00554c89</c>, <c>0x00554ca9</c>).</summary>
	[TestMethod]
	public void WithNoFreeSlotOrNoTableAddHeadDoesNothing()
	{
		var full = new RideScript( Build(
			Word( Opcode.ADDHEAD ), 1,
			Word( Opcode.ADDHEAD ), 2,
			Word( Opcode.ADDHEAD ), 3,
			Word( Opcode.END ) ) );

		full.SizeHeads( 2 );
		full.Turn( 0f );

		CollectionAssert.AreEquivalent( new[] { 1, 2 }, full.Heads().Select( head => head.Handle ).ToArray(),
			"the third found no free slot" );

		var none = new RideScript( Build( Word( Opcode.ADDHEAD ), 1, Word( Opcode.END ) ) );

		none.Turn( 0f );

		Assert.AreEqual( 0, none.HeadSlots );
		Assert.AreEqual( 0, none.Heads().Count(), "a script with no table holds nobody" );
	}

	/// <summary>
	/// <b><c>ADDHEAD</c> makes no duplicate test</b> (<c>0x00554c8f</c>..<c>0x00554cd3</c>: a free slot asked for, then
	/// drawn until a slot holds nought, then stored): one visitor added twice holds two slots, and <c>DELHEAD</c>'s
	/// walk with no break is what frees both.
	/// </summary>
	[TestMethod]
	public void AVisitorAddedTwiceHoldsTwoSlots()
	{
		var script = new RideScript( Build(
			Word( Opcode.ADDHEAD ), 7,
			Word( Opcode.ADDHEAD ), 7,
			Word( Opcode.END ) ) );

		script.SizeHeads( 3 );
		script.Turn( 0f );

		var held = script.Heads().Select( head => head.Handle ).ToArray();

		CollectionAssert.AreEqual( new[] { 7, 7 }, held, "the second ADDHEAD took a slot of its own" );
		Assert.AreEqual( 2, script.Heads().Select( head => head.Node ).Distinct().Count(), "on two different nodes" );
	}

	/// <summary>
	/// <b><c>DELHEAD</c> frees every slot holding the visitor</b>, with no break after the first, and leaves the rest;
	/// <b>neither instruction writes the result register</b>.
	/// </summary>
	[TestMethod]
	public void DelHeadFreesEverySlotHoldingTheVisitorAndNeitherWritesTheRegister()
	{
		var script = new RideScript( Build(
			Word( Opcode.COPY ), Var( 0 ), 5,
			Word( Opcode.DELHEAD ), Var( 0 ),
			Word( Opcode.ADDHEAD ), 9,
			Word( Opcode.END ) ) );

		script.RestoreHeads( [5, 0, 6, 5] );
		script.Turn( 0f );

		var held = script.Heads().ToDictionary( head => head.Node, head => head.Handle );

		Assert.IsFalse( held.ContainsValue( 5 ), "both of 5's slots were freed" );
		Assert.AreEqual( 6, held[3], "and 6's was left" );
		Assert.AreEqual( 2, held.Count, "6, and 9 on one of the three free slots" );
		Assert.AreEqual( 5, script.Result, "the register still holds the COPY's 5" );
	}

	/// <summary>A saved table comes back at the save's length, whatever the model would give.</summary>
	[TestMethod]
	public void ASavedTableComesBackAtItsOwnLength()
	{
		var script = new RideScript( Build( Word( Opcode.END ) ) );

		script.SizeHeads( 5 );
		script.RestoreHeads( [0, 0, 447] );

		Assert.AreEqual( 3, script.HeadSlots );
		CollectionAssert.AreEqual( new[] { (3, 447, false) }, script.Heads().ToArray(),
			"held on node 3, and not hung without a model" );
	}

	/// <summary>
	/// <b>The six rides' head counts</b>, the run of head-space ids from 1 the loader counts (<c>FUN_005587f0</c>): the
	/// lengths every one of these rides' tables has in Alexah's played jungle saves (Spider 40, Mumbo 5, Sun God 32,
	/// Crazy Ape 16, Rocky Racers 8), and Eruption's 16.
	/// </summary>
	[TestMethod]
	public void TheSixRidesHaveTheHeadSlotsTheirSavedTablesHave()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		foreach ( var (stem, count) in new[] { ("incagod", 32), ("monkey", 16), ("mumbo", 5), ("porkpie", 8), ("spider", 40), ("volcano", 16) } )
		{
			var item = catalogue.All.Single( item => item.Stem == stem );
			using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
			var nodes = new RideNodes( new ModelFile( stream ), stem, false, [] );

			Assert.AreEqual( count, nodes.HeadCount(), stem );
			Assert.AreNotEqual( NodeEnd.Missing, nodes.FindHead( count, out _ ), $"{stem}'s last head is found" );
			Assert.AreEqual( NodeEnd.Missing, nodes.FindHead( count + 1, out _ ), $"{stem} has no head past it" );
		}
	}

	/// <summary>Each source vertex of <paramref name="mesh"/> at rest, as the morph's own rest pose recovers them.</summary>
	internal static Vector3[] RestSources( ModelFile.Mesh mesh )
	{
		var rest = new Vector3[mesh.VertexCount];

		for ( var i = 0; i < mesh.Vertices.Length && i < mesh.VertexOrder.Length; ++i )
		{
			if ( mesh.VertexOrder[i] < rest.Length )
				rest[mesh.VertexOrder[i]] = mesh.Vertices[i].Position;
		}

		return rest;
	}

	/// <summary>
	/// <b>Mumbo's and the Crazy Ape's heads sit on faces of their tentacles</b>, and at rest the face rule
	/// (<c>FUN_0044b040</c>: two lerps across the face, then the face normal times the offset, in the parent mesh's space)
	/// lands on each head node's own stored place - as it does for 247 of the game's 248 face-anchored records.
	/// </summary>
	[TestMethod]
	public void AtRestEachFaceAnchoredHeadIsOnItsNode()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		foreach ( var (stem, heads) in new[] { ("mumbo", 5), ("monkey", 16) } )
		{
			var item = catalogue.All.Single( item => item.Stem == stem );
			using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
			var model = new ModelFile( stream );

			for ( var id = 1; id <= heads; ++id )
			{
				var node = model.Nodes[model.FindNode( id, RideNodes.HeadSpace )];
				var mesh = model.Meshes[node.ParentIndex];
				var rest = RestSources( mesh );

				Assert.IsNotNull( node.Face, $"{stem} head {id} is anchored on a face" );

				var local = ModelFile.PointOnFace( mesh, node.Face!.Value, source => rest[source] )!.Value;
				var placed = System.Numerics.Vector3.Transform( local.GetSystemVector3(), mesh.WorldTransform );

				Assert.IsTrue( System.Numerics.Vector3.Distance( placed, node.WorldTransform.Translation ) < 0.05f,
					$"{stem} head {id} ({node.Name}) at {placed}, its node at {node.WorldTransform.Translation}" );
			}
		}
	}

	/// <summary>
	/// <b><c>FUN_0044a640</c>'s turn</b>: the edge from the face's first corner to the anchor's point, normalised (here (0.6, 0, 0.8));
	/// rows n x e, n and e, the normal left as long as it is (2 here); then the kept turn about z, y and x, each from the 4,096 table, before
	/// the frame. The numbers are worked out in Python by the decode as `lib/faceturn.py` applies it; the order x, y, z instead, or e from the first corner to
	/// the second, gives others. Where the corner is the point, e is the normal's z, x, y.
	/// </summary>
	[TestMethod]
	public void TheFaceTurnIsTheOriginals()
	{
		var turned = LobbyModel.TurnToFace( new( 1f, 2f, 3f ), new( 4f, 2f, 7f ), new( 0f, 2f, 0f ), new( 0.3f, -0.7f, 1.1f ) );
		float[] expected = [0.88123f, 1.52932f, 0.01774f, -1.33919f, 1.20656f, 0.48734f, -0.59162f, -0.45321f, 1.35727f];
		float[] got = [turned.M11, turned.M12, turned.M13, turned.M21, turned.M22, turned.M23, turned.M31, turned.M32, turned.M33];

		for ( var i = 0; i < 9; ++i )
			Assert.AreEqual( expected[i], got[i], 2e-5f, $"row {(i / 3) + 1} column {(i % 3) + 1}" );

		Assert.AreEqual( 0f, turned.M41, "no translation" );

		var level = LobbyModel.TurnToFace( new( 1f, 2f, 3f ), new( 1f, 2f, 3f ), new( 0.6f, 0f, 0.8f ), default );
		float[] fallback = [-0.48f, 0.64f, 0.36f, 0.6f, 0f, 0.8f, 0.8f, 0.6f, 0f];
		float[] read = [level.M11, level.M12, level.M13, level.M21, level.M22, level.M23, level.M31, level.M32, level.M33];

		for ( var i = 0; i < 9; ++i )
			Assert.AreEqual( fallback[i], read[i], 2e-5f, $"no edge: row {(i / 3) + 1} column {(i % 3) + 1}" );
	}

	/// <summary>
	/// <b>At rest the face turn stands near each head's own turn</b>, by the angles an independent reworking measured over every model
	/// (`lib/faceturn.py`: 42 of the game's 126 such records within 0.1 degrees, 93 within 1): Mumbo's four heads that carry
	/// <c>0x40000</c> and the Crazy Ape's sixteen, each to a hundredth of a degree, with the kept turn read where the anchor's word points.
	/// </summary>
	[TestMethod]
	public void AtRestTheFaceTurnIsTheMeasuredAngleFromEachHeadsTurn()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var measured = new[]
		{
			("mumbo", new[] { 1.018, 0.804, 3.017, 4.958 }, 5),
			("monkey", new[] { 0.630, 0.588, 0.131, 0.382, 0.312, 0.321, 0.500, 0.409, 0.690, 0.623, 0.110, 0.111, 0.191, 1.135, 0.681, 1.349 }, 16)
		};

		foreach ( var (stem, angles, heads) in measured )
		{
			var item = catalogue.All.Single( item => item.Stem == stem );
			using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
			var model = new ModelFile( stream );
			var checkedHeads = 0;

			for ( var id = 1; id <= heads; ++id )
			{
				var node = model.Nodes[model.FindNode( id, RideNodes.HeadSpace )];

				if ( (node.IdFlags & 0x40000) == 0 )
				{
					Assert.AreEqual( "mumbo Head1", $"{stem} {node.Name}", "only Mumbo's Head1 is anchored without the turn" );
					continue;
				}

				// Head1, Head02 .. Head16: the measured angle by the head's number.
				var number = int.Parse( node.Name.Trim()[4..] );
				var expected = stem == "mumbo" ? angles[number - 2] : angles[number - 1];
				++checkedHeads;

				var mesh = model.Meshes[node.ParentIndex];
				var rest = RestSources( mesh );
				var on = ModelFile.OnFace( mesh, node.Face!.Value, source => rest[source] )!.Value;
				var turned = LobbyModel.TurnToFace( on.Corner.GetSystemVector3(), on.Point.GetSystemVector3(), on.Normal.GetSystemVector3(),
					node.Face!.Value.Turn.GetSystemVector3() );

				System.Numerics.Matrix4x4.Invert( mesh.WorldTransform, out var unplaced );
				var own = node.WorldTransform * unplaced;

				Assert.AreEqual( expected, Degrees( turned, own ), 0.01, $"{stem} head {id} ({node.Name})" );
			}

			Assert.AreEqual( angles.Length, checkedHeads, $"{stem}: every head with the turn" );
		}

		static double Degrees( System.Numerics.Matrix4x4 a, System.Numerics.Matrix4x4 b )
		{
			System.Numerics.Vector3[] Rows( System.Numerics.Matrix4x4 m ) =>
				[System.Numerics.Vector3.Normalize( new( m.M11, m.M12, m.M13 ) ), System.Numerics.Vector3.Normalize( new( m.M21, m.M22, m.M23 ) ),
					System.Numerics.Vector3.Normalize( new( m.M31, m.M32, m.M33 ) )];

			var (p, q) = (Rows( a ), Rows( b ));
			var trace = System.Numerics.Vector3.Dot( p[0], q[0] ) + System.Numerics.Vector3.Dot( p[1], q[1] ) + System.Numerics.Vector3.Dot( p[2], q[2] );

			return System.Math.Acos( System.Math.Clamp( (trace - 1.0) / 2.0, -1.0, 1.0 ) ) * 180.0 / System.Math.PI;
		}
	}

	/// <summary>
	/// <b>A head follows its tentacle</b>: move the face's third corner, as a morph frame might, and the face point moves by
	/// <c>v</c> times as much, the third corner's weight. Taking the file's normal, it moves by that and nothing else.
	/// </summary>
	[TestMethod]
	public void MovingTheTentacleMovesTheHeadWithIt()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var item = catalogue.All.Single( item => item.Stem == "mumbo" );
		using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
		var model = new ModelFile( stream );
		var node = model.Nodes[model.FindNode( 2, RideNodes.HeadSpace )];
		var mesh = model.Meshes[node.ParentIndex];
		var rest = RestSources( mesh );
		var lift = new Vector3( 0.5f, 3f, -1f );
		var face = node.Face!.Value;
		var third = mesh.VertexOrder[mesh.Indices[(face.Face * 3) + 2]];

		var before = ModelFile.PointOnFace( mesh, face, source => rest[source] )!.Value;
		var after = ModelFile.PointOnFace( mesh, face, source => source == third ? rest[source] + lift : rest[source] )!.Value;

		Assert.AreEqual( 3f * face.V, after.Y - before.Y, 1e-4f, "up with the tentacle, by the third corner's weight" );
		Assert.AreEqual( 0.5f * face.V, after.X - before.X, 1e-4f );
		Assert.AreEqual( -1f * face.V, after.Z - before.Z, 1e-4f );
	}

	/// <summary>
	/// <b>A morphed face's normal is worked out again</b> (<c>FUN_00473a40</c>, called from the pose walk at <c>0x0044afe0</c>):
	/// at rest it equals the file's entry for all 21 heads of Mumbo (5) and the Crazy Ape (16), not every model's anchors (so the rest pose is
	/// unchanged), and with the third corner lifted the head stands off by the offset times the normal's change - the
	/// normalised (c - b) x (a - b) of the lifted corners, not the file's.
	/// </summary>
	[TestMethod]
	public void AMorphedFacesNormalIsRecomputedFromItsCorners()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		foreach ( var (stem, heads) in new[] { ("mumbo", 5), ("monkey", 16) } )
		{
			var item = catalogue.All.Single( item => item.Stem == stem );
			using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
			var model = new ModelFile( stream );

			for ( var id = 1; id <= heads; ++id )
			{
				var node = model.Nodes[model.FindNode( id, RideNodes.HeadSpace )];
				var mesh = model.Meshes[node.ParentIndex];
				var rest = RestSources( mesh );
				var face = node.Face!.Value;

				var file = ModelFile.PointOnFace( mesh, face, source => rest[source] )!.Value;
				var worked = ModelFile.PointOnFace( mesh, face, source => rest[source], recomputeNormal: true )!.Value;

				Assert.IsTrue( file.Distance( worked ) < 0.05f,
					$"{stem} head {id} at rest: file {file}, recomputed {worked}" );
			}
		}

		{
			var item = catalogue.All.Single( item => item.Stem == "mumbo" );
			using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.MD2" ) );
			var model = new ModelFile( stream );
			var node = model.Nodes[model.FindNode( 2, RideNodes.HeadSpace )];
			var mesh = model.Meshes[node.ParentIndex];
			var rest = RestSources( mesh );
			var face = node.Face!.Value;
			var lift = new Vector3( 0.5f, 3f, -1f );
			var corners = new[] { 0, 1, 2 }.Select( corner => mesh.VertexOrder[mesh.Indices[(face.Face * 3) + corner]] ).ToArray();

			Vector3 Posed( int source ) => source == corners[2] ? rest[source] + lift : rest[source];

			var normal = Vector3.Cross( Posed( corners[2] ) - Posed( corners[1] ), Posed( corners[0] ) - Posed( corners[1] ) ).Normal;
			var fileMoved = ModelFile.PointOnFace( mesh, face, Posed )!.Value;
			var recomputed = ModelFile.PointOnFace( mesh, face, Posed, recomputeNormal: true )!.Value;

			Assert.IsTrue( (recomputed - fileMoved).Distance( (normal - mesh.FaceNormals[mesh.FaceNormalIndices[face.Face]]) * face.Offset ) < 1e-4f,
				"the head moves by the offset times the normal's change" );
			Assert.IsTrue( recomputed.Distance( fileMoved ) > 0.1f, "a lift of this size turns the normal enough to be seen" );
		}
	}

	/// <summary>
	/// <b>The recompute is the original's rule, face by face</b> (<c>FUN_00473a40</c>), on a mesh made here: the corners go
	/// through the vertex order, the cross product is (c - b) x (a - b) normalised, and where two faces name one entry the
	/// last one written is the one a head reads.
	/// </summary>
	[TestMethod]
	public void TheRecomputeFollowsTheOriginalsRuleOnAMadeMesh()
	{
		// Drawn vertex i is source vertex VertexOrder[i]: the order reverses the sources, so a corner read without it is wrong.
		var source = new[] { new Vector3( 0f, 0f, 0f ), new Vector3( 4f, 1f, 0f ), new Vector3( 0f, 2f, 3f ), new Vector3( 5f, 5f, 5f ) };
		var mesh = new ModelFile.Mesh
		{
			VertexOrder = [3, 2, 1, 0],
			Indices = [0, 1, 2, 1, 3, 2],
			FaceNormalIndices = [0, 0],
			FaceNormals = [new Vector3( 0f, 1f, 0f )]
		};
		var face = new ModelFile.FaceAnchor( 0, 0f, 0f, 2f );

		// Face 1 owns entry 0: its corners are drawn 1, 3, 2, which are sources 2, 0, 1.
		System.Numerics.Vector3 At( int s ) => source[s].GetSystemVector3();
		var expected = System.Numerics.Vector3.Normalize( System.Numerics.Vector3.Cross( At( 1 ) - At( 0 ), At( 2 ) - At( 0 ) ) );
		var point = ModelFile.PointOnFace( mesh, face, s => source[s], recomputeNormal: true )!.Value;
		var start = source[3];

		Assert.AreEqual( start.X + (expected.X * 2f), point.X, 1e-5f, "x: the last face's normal, the order's corners" );
		Assert.AreEqual( start.Y + (expected.Y * 2f), point.Y, 1e-5f, "y" );
		Assert.AreEqual( start.Z + (expected.Z * 2f), point.Z, 1e-5f, "z" );

		// Without the recompute the file's entry is read, whatever the corners do.
		var file = ModelFile.PointOnFace( mesh, face, s => source[s] )!.Value;
		Assert.AreEqual( start.Y + 2f, file.Y, 1e-5f, "the file's normal" );

		// A face with no area has no normal: the original writes (0, -1, 0).
		var flat = new[] { new Vector3( 0f, 0f, 0f ), new Vector3( 1f, 0f, 0f ), new Vector3( 2f, 0f, 0f ), new Vector3( 0f, 0f, 0f ) };
		var level = ModelFile.PointOnFace( mesh, face, s => flat[s], recomputeNormal: true )!.Value;
		Assert.AreEqual( flat[3].Y - 2f, level.Y, 1e-5f, "(0, -1, 0) times the offset" );
		Assert.AreEqual( flat[3].X, level.X, 1e-5f );
		Assert.AreEqual( flat[3].Z, level.Z, 1e-5f );
	}
}
