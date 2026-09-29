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
}
