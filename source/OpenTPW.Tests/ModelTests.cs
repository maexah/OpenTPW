using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Reading a model's node tree, and the names that say what each node is for. These read real game
/// files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ModelTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	/// <summary>
	/// Reads through the file system this test mounted rather than the global one, which belongs to
	/// a running game and which no test should need to have been set.
	/// </summary>
	private ModelFile Read( string path ) => new( new MemoryStream( data.ReadAllBytes( path ) ) );

	/// <summary>
	/// A transform-only node carries a name like any other. They are the ones it was never read for:
	/// the name pointer sits at +0x54 of the record, which the mesh path has always read and the node
	/// path went past.
	///
	/// The Space island is the case that matters - its antenna carries the nodes marking where an
	/// effect belongs - and the two named here are both transform-only (flag 0x200) and both hang off
	/// the Antennae01 mesh.
	/// </summary>
	[TestMethod]
	public void ATransformOnlyNodeIsNamed()
	{
		var model = Read( "lobby/terrain/Spa_isle.md2" );

		var emitter = model.Nodes.FindIndex( node => node.Name == "ant_emitter" );

		Assert.AreNotEqual( -1, emitter,
			$"no node called ant_emitter among: {string.Join( ", ", model.Nodes.Select( node => $"'{node.Name}'" ) )}" );

		Assert.IsTrue( emitter >= model.Meshes.Count, "ant_emitter should be a node rather than a mesh" );
		Assert.AreEqual( 0x200u, model.Nodes[emitter].Flags & 0x200u, "ant_emitter is a transform-only node" );

		var parent = model.Nodes[emitter].ParentIndex;

		Assert.IsTrue( parent >= 0 && parent < model.Meshes.Count, "ant_emitter hangs off a mesh" );
		Assert.AreEqual( "Antennae01", model.Nodes[parent].Name, "ant_emitter hangs off the antenna" );
	}

	/// <summary>
	/// A mesh is a node too, and reads back the same name either way. <see cref="ModelFile.Mesh.Name"/>
	/// keeps the terminator it was read with, so the comparison trims it.
	/// </summary>
	[TestMethod]
	public void AMeshReadsBackTheSameNameAsItsNode()
	{
		var model = Read( "lobby/terrain/Spa_isle.md2" );

		Assert.IsTrue( model.Nodes.Count >= model.Meshes.Count, "every mesh should have a node" );

		for ( int i = 0; i < model.Meshes.Count; ++i )
			Assert.AreEqual( model.Meshes[i].Name.TrimEnd( '\0' ), model.Nodes[i].Name, $"mesh {i}" );
	}

	/// <summary>
	/// What the names are for. A park's gate marks the spot a sound belongs at with a node called
	/// "sound node", and that node is the one carrying the sound bit - 0x200 - in its id record's flag
	/// word, where a particle emitter carries 0x100. The two halves have to agree, or a name would be
	/// the only thing saying what a node is, and the engine looks a node up by the flag rather than by
	/// the name.
	/// </summary>
	[TestMethod]
	public void TheGateSoundNodeCarriesTheSoundFlag()
	{
		var model = Read( "levels/space/features/gates/gates.MD2" );

		var sound = model.Nodes.FirstOrDefault( node => node.Name == "sound node" );

		Assert.IsNotNull( sound,
			$"no node called 'sound node' among: {string.Join( ", ", model.Nodes.Select( node => $"'{node.Name}'" ) )}" );

		Assert.AreEqual( 0x200u, sound.IdFlags & 0x200u, $"its id flags were 0x{sound.IdFlags:X}" );

		var emitter = model.Nodes.FirstOrDefault( node => node.Name == "ant_emitter" );

		Assert.IsNotNull( emitter, "the same gate's particle emitter" );
		Assert.AreEqual( 0x100u, emitter.IdFlags & 0x100u, $"its id flags were 0x{emitter.IdFlags:X}" );
	}
}
