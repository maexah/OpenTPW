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
	/// The bus's route, at file 0xac. The numbers here were measured out of the file before the reader
	/// existed, so they fail if the record is read at the wrong offset rather than merely agreeing with
	/// whatever the reader happens to do.
	///
	/// 45 is a multiple of three and not 3n+1, which is what says the loop is CLOSED - the last cubic
	/// segment ends back at point 0. A reader that assumed an open chain would want 46.
	/// </summary>
	[TestMethod]
	public void TheBusCarriesOneClosedBezierRouteOfFortyFivePoints()
	{
		var model = Read( "levels/jungle/features/bus/Bus.MD2" );

		Assert.AreEqual( 1, model.Paths.Count, "the bus has exactly one route" );

		var route = model.Paths[0];

		Assert.AreEqual( 45, route.Points.Length, "45 points" );
		Assert.AreEqual( 0, route.Points.Length % 3, "a closed Bezier loop is a multiple of three" );
		Assert.IsTrue( route.IsBezier, "the bus's route is Bezier" );
		Assert.IsFalse( route.IsStraight, "and not a straight polyline" );

		Assert.AreEqual( 207.449f, route.Points[0].X, 0.01f, "first point X" );
		Assert.AreEqual( 0f, route.Points[0].Y, 0.01f, "the bus drives on the ground" );
		Assert.AreEqual( -247.813f, route.Points[0].Z, 0.01f, "first point Z" );
	}

	/// <summary>
	/// The haunted house is the only model in the game with more than one route, and it is what the
	/// count rule exists for: nothing in the file says how many records there are, so they are counted
	/// from the nodes that name them. This fails against a reader that always reads one.
	///
	/// Its four carts share one circuit - the four routes have the same bounding box - and are offset
	/// in phase instead.
	/// </summary>
	[TestMethod]
	public void TheHauntedHouseCarriesFourRoutesCountedFromItsNodes()
	{
		var model = Read( "levels/hallow/rides/haunt/haunt.MD2" );

		Assert.AreEqual( 4, model.Paths.Count, "four routes, one per cart" );

		foreach ( var route in model.Paths )
			Assert.AreEqual( 48, route.Points.Length, "each is 48 points" );

		for ( int index = 1; index <= 3; ++index )
		{
			var name = $"Kart_path0{index + 1}";
			var node = model.Nodes.FindIndex( n => n.Name == name );

			Assert.AreNotEqual( -1, node, $"no node called {name}" );
			Assert.AreEqual( index, model.Nodes[node].PathId, $"{name} names route {index}" );
		}
	}

	/// <summary>
	/// The other side of the same rule. Most of the game's models have no route at all, and a reader
	/// that follows the pointer at 0xac without checking it is zero invents one out of whatever lies
	/// at the start of the file.
	/// </summary>
	[TestMethod]
	public void AModelWithNoRouteHasNone()
	{
		Assert.AreEqual( 0, Read( "levels/jungle/features/gates/gates.MD2" ).Paths.Count, "the gates" );
		Assert.AreEqual( 0, Read( "levels/jungle/features/end/End.MD2" ).Paths.Count, "the end sign" );
	}

	/// <summary>
	/// Space's slide is the only route in the game whose type asks for plain waypoints rather than
	/// Bezier controls, so it is the single case that tells the two type bits apart.
	/// </summary>
	[TestMethod]
	public void TheSlideIsTheOnlyStraightRoute()
	{
		var route = Read( "levels/space/rides/slide/slide.MD2" ).Paths.Single();

		Assert.IsTrue( route.IsStraight, "the slide's route is a straight polyline" );
		Assert.IsFalse( route.IsBezier, "and not Bezier" );
		Assert.AreEqual( 12, route.Points.Length, "12 waypoints" );
	}

	/// <summary>
	/// A node's path id does not say whether the node follows a route: nought means both "route 0" and
	/// "no route". The seaplane is the plain case - one route, and one node naming it at nought - and
	/// it is why the count is a floor of one rather than a test for whether any node names anything.
	/// </summary>
	[TestMethod]
	public void ANodeThatNamesNoRouteReadsTheSameAsOneNamingTheFirst()
	{
		var model = Read( "levels/jungle/features/seaplane/Seaplane.MD2" );

		Assert.AreEqual( 1, model.Paths.Count, "one route" );

		var flightpath = model.Nodes.FindIndex( node => node.Name == "Flightpath" );

		Assert.AreNotEqual( -1, flightpath, "the seaplane has a node called Flightpath" );
		Assert.AreEqual( 0, model.Nodes[flightpath].PathId, "which names route 0" );
		Assert.IsTrue( model.Nodes.All( node => node.PathId == 0 ), "and it is the only one that names any" );

		Assert.IsTrue( model.Paths[0].Points.Max( point => point.Y ) > 300f,
			"the seaplane's route climbs, where the bus's and the ferry's are flat" );
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
