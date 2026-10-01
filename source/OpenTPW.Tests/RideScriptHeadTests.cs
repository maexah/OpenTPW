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
	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	private static int Var( int index ) => unchecked( (int)(0x40000000u | (uint)index) );

	/// <summary>A whole .RSE file of <paramref name="body"/>, three variables and no slots of any other kind.</summary>
	private static RideScriptFile Build( params int[] body )
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
	private static Vector3[] RestSources( ModelFile.Mesh mesh )
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
	/// <b>A head follows its tentacle</b>: move the face's third corner, as a morph frame might, and the face point moves by
	/// <c>v</c> times as much, the third corner's weight (the face normal, which the morph routine does not write, does not
	/// turn).
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
}
