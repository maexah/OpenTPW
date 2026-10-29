using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which materials the original's loader makes opaque (<c>docs/exe/textures.md</c>, "Which materials the loader makes
/// opaque"): every material of the floor mesh <c>FUN_00469a80</c> picks by the footprint's outline, or of the root mesh.
/// The counts are the original's own, read from its memory with every Lost Kingdom item on offer loaded
/// (<c>original/floormesh.py</c>): 54 of 54 item models agree on the count and the floor.
/// </summary>
[TestClass]
public class ProceduralBaseTests
{
	private BaseFileSystem? was;

	[TestInitialize]
	public void MountTheGame()
	{
		was = FileSystemOrNull;
		FileSystem = GameData.Required();
	}

	[TestCleanup]
	public void PutItBack() => FileSystemOrNull = was;

	private static readonly ProceduralBase.Grid Park = new( 0f, 0f, 10f, 10f );

	/// <summary>
	/// <b>The floor is the mesh with most base vertices, a vertex at the ground on a cell corner along the outline.</b>
	/// The original's counts: the Belly Bounce 14 (its 3 x 4 picture's outline), Sign 1 14 where its 4 x 2 outline has 12
	/// corners, the 2 x 1 Log 6, the 1 x 1 Rock 4; each floor its mesh 0. With no picture nothing counts, and the root is taken.
	/// </summary>
	/// <remarks><b>Mutations:</b> the edge bits taken as a whole cell's (any side); the tolerance's |f - 1| dropped; the
	/// clamp at the far side not flipping low to high; ties going to the later mesh; the 0x200 node counted.</remarks>
	[TestMethod]
	public void AnItemsFloorIsTheMeshWithMostBaseVerticesAlongItsOutline()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		foreach ( var (id, count) in new[] { (1100, 14), (1431, 14), (1424, 6), (1428, 4), (1203, 8), (1303, 12) } )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );
			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			Assert.AreEqual( 0, ProceduralBase.FloorMesh( model, item.Shape, Park, out var bases ), $"{item.Stem}'s floor" );
			Assert.AreEqual( count, bases, $"{item.Stem}'s base vertices, as the original counted them" );
			Assert.IsNull( ProceduralBase.FloorMesh( model, null, Park, out var none ), $"{item.Stem} with no picture" );
			Assert.AreEqual( 0, none );
		}

	}

	/// <summary>
	/// <b>Every Lost Kingdom item model the original loaded with a floor counts as it counted</b>: its 41 models, each
	/// floor mesh 0 (<c>q362/floormesh-b.txt</c>, the stock park's file).
	/// </summary>
	/// <remarks><b>Mutations:</b> the ground test dropped; a later mesh taking a tie; the edge in z from two rows on.</remarks>
	[TestMethod]
	public void EveryLostKingdomItemCountsItsBaseAsTheOriginalDid()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var original = new[] { (1100, 14), (1101, 16), (1102, 16), (1104, 16), (1108, 16), (1109, 16), (1110, 14), (1113, 20),
			(1140, 20), (1150, 14), (1160, 16), (1180, 14), (1202, 12), (1203, 8), (1206, 8), (1207, 8), (1209, 12), (1212, 8),
			(1303, 12), (1304, 12), (1305, 10), (1307, 12), (1400, 8), (1401, 4), (1402, 4), (1403, 12), (1406, 4), (1407, 4),
			(1411, 8), (1413, 4), (1414, 4), (1417, 4), (1418, 4), (1419, 10), (1422, 16), (1423, 8), (1424, 6), (1425, 4),
			(1428, 4), (1429, 4), (1431, 14) };

		foreach ( var (id, count) in original )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );
			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			Assert.AreEqual( 0, ProceduralBase.FloorMesh( model, item.Shape, Park, out var bases ), $"{item.Stem}'s floor" );
			Assert.AreEqual( count, bases, $"{item.Stem}'s base vertices" );
		}
	}

	/// <summary>
	/// <b>A node's links are the file's: its first child at +0x0C, its next sibling at +0x08</b>, so the walk visits a
	/// node before its children: each child names the node as its parent, siblings share it, and the root has none.
	/// </summary>
	/// <remarks><b>Mutations:</b> sibling and child read the other way.</remarks>
	[TestMethod]
	public void ANodesChildAndSiblingAreTheFilesLinks()
	{
		foreach ( var path in new[] { "levels/jungle/rides/bouncy/bouncy.MD2", "levels/jungle/queue/queend.MD2", "levels/jungle/sideshow/junspray/Junspray.MD2" } )
		{
			var model = new ModelFile( path );
			Assert.AreEqual( -1, model.Nodes[model.RootNode].ParentIndex, $"{path}: the root" );
			Assert.AreEqual( -1, model.Nodes[model.RootNode].NextSibling, $"{path}: the root has no sibling" );
			Assert.IsTrue( model.Nodes[model.RootNode].FirstChild >= 0, $"{path}: the root has children" );

			for ( var node = 0; node < model.Nodes.Count; ++node )
				for ( var child = model.Nodes[node].FirstChild; child >= 0; child = model.Nodes[child].NextSibling )
					Assert.AreEqual( node, model.Nodes[child].ParentIndex, $"{path}: node {child} under {node}" );
		}
	}

	/// <summary>
	/// <b>The opaque frames are the floor's materials', the model's other meshes naming the same texture included.</b> The
	/// Belly Bounce's floor names twelve frames, every <c>m_grass</c> and <c>ss_PATH</c>; its body's <c>Bd_42</c> is not one.
	/// A queue piece's are its root mesh's (<c>jpa_que1</c>, not <c>jpa_que5</c>), and a <c>P</c> model's its root's
	/// without a search: the Totem's preview floor <c>tp_ground</c>, whose materials carry <c>0x2</c> too.
	/// </summary>
	/// <remarks><b>Mutations:</b> the floor's frames taken from mesh 1; the queue piece's from its last mesh; the P model
	/// searched by the picture (none found, still the root: not killable here).</remarks>
	[TestMethod]
	public void TheFloorsTheQueuePiecesAndThePModelsRootsFramesAreOpaque()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		Assert.IsTrue( catalogue.TryGet( 1100, out var bouncy ) );

		var model = new ModelFile( $"{bouncy.Directory}/{bouncy.Stem}.MD2" );
		var frames = ProceduralBase.ForItem( model, bouncy.Shape );
		Assert.AreEqual( 12, frames.Count );
		Assert.IsTrue( model.Meshes[0].Materials.All( m => frames.Contains( m.FrameOffset ) ) );
		Assert.IsFalse( model.Meshes.SelectMany( m => m.Materials ).Any( m => m.Name.TrimEnd( '\0' ) == "Bd_42" && frames.Contains( m.FrameOffset ) ) );

		var piece = new ModelFile( "levels/jungle/queue/queend.MD2" );
		var opaque = ProceduralBase.ForQueuePiece( piece );
		Assert.IsTrue( Named( piece, "jpa_que1" ).All( m => opaque.Contains( m.FrameOffset ) ), "jpa_que1, the root's" );
		Assert.IsFalse( Named( piece, "jpa_que5" ).Any( m => opaque.Contains( m.FrameOffset ) ), "jpa_que5, the second mesh's" );

		Assert.IsTrue( catalogue.TryGet( 1110, out var totem ) );
		var preview = new ModelFile( $"{totem.Directory}/P{totem.Stem}.MD2" );
		var root = ProceduralBase.ForPModel( preview );
		Assert.AreEqual( 0, preview.RootNode );
		Assert.IsTrue( preview.Meshes[0].Materials.All( m => root.Contains( m.FrameOffset ) ) && root.Count > 0 );
		Assert.IsTrue( preview.Meshes[0].Materials.Any( m => m.IsTranslucent ), "tp_ground carries 0x2 and is opaque all the same" );
	}

	private static ModelFile.MaterialData[] Named( ModelFile model, string name )
		=> model.Meshes.SelectMany( m => m.Materials ).Where( m => m.Name.TrimEnd( '\0' ) == name ).ToArray();
}
