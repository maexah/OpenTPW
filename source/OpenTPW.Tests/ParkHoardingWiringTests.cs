using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using V3 = System.Numerics.Vector3;

namespace OpenTPW.Tests;

/// <summary>
/// The hoarding a placed ride is given, tested where the park raises it (Q1029, from Q211): <see cref="ParkObjects.HoardingFor"/>'s
/// gate on the model and its restore of the park file's saved progress, <see cref="ParkRideHoarding.Fill"/>'s vertices, the
/// panels' order from <see cref="RideHoardingGeometry.Build"/>, and the ground <see cref="ParkRideHoarding.Ground(HeightfieldFile, System.Numerics.Vector2, System.Numerics.Vector2, Vector3, System.Numerics.Quaternion)"/>
/// samples (<c>docs/exe/ride-hoardings.md</c>). The helpers' own tests are <see cref="RideHoardingTests"/>.
/// </summary>
[TestClass]
public class ParkHoardingWiringTests
{
	private BaseFileSystem data = null!;
	private ParkItemCatalogue.Item bounce;

	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = data = GameData.Required();
		bounce = new ParkItemCatalogue( "jungle", data ).All.First( item => item.Stem.Equals( "bouncy", StringComparison.OrdinalIgnoreCase ) );
		Assert.IsNotNull( bounce.Hoarding, "the shipped Bounce has an outline" );
	}

	[TestCleanup]
	public void EndTheParksObjects() => TestRun.DeleteEvery<ParkObjects>();

	private ModelFile BounceModel() => new( new MemoryStream( data.ReadAllBytes( $"{bounce.Directory}/{bounce.Stem}.md2" ) ) );

	private ParkWorld.CatalogueObject Placed( int meshInstance = 1 )
		=> new( 13, bounce.Id, 10 << 8, 10 << 8, 0, CanLoad: 1, MeshInstance: meshInstance );

	/// <summary>A park file's thing-states module holding one present record: item <paramref name="item"/>, hoarding flags and progress, in slot 0.</summary>
	private static ParkThingStates Saved( int item, uint flags, float progress )
	{
		var record = new byte[0x37 + 44];
		record[0] = 1;
		BitConverter.GetBytes( item ).CopyTo( record, 1 );
		BitConverter.GetBytes( flags ).CopyTo( record, 0x1d );
		BitConverter.GetBytes( progress ).CopyTo( record, 0x23 );
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );
		writer.Write( Encoding.ASCII.GetBytes( "SYSG" ) );
		writer.Write( 12 + record.Length );
		writer.Write( 1 );
		writer.Write( 0 );
		writer.Write( 1 );
		writer.Write( record );
		writer.Flush();

		var states = new ParkThingStates( stream.ToArray(), _ => 1 );
		Assert.IsNull( states.Problem );

		return states;
	}

	/// <summary>The shipped Bounce's model passes the gate (base vertices, more than one): 12 panels, and the park keeps the progress it hands back.</summary>
	[TestMethod]
	public void AModelWithBaseVerticesIsGivenItsHoarding()
	{
		var state = new ParkState( false, 0 );
		var model = BounceModel();
		ProceduralBase.FloorMesh( model, bounce.Shape, ProceduralBase.ParkGrid, out var bases );
		Assert.IsTrue( bases > 1, "the shipped model has more than the one base vertex the gate refuses" );

		var objects = new ParkObjects( "jungle", null, null );
		var hoarding = objects.HoardingFor( state, Placed(), bounce.Hoarding, bounce.Shape, model );

		Assert.IsNotNull( hoarding );
		Assert.AreEqual( 12, hoarding.Value.Panels.Length );
		Assert.AreSame( hoarding.Value.Progress, state.HoardingFor( 13 ), "the park keeps the progress the ride's doors drive" );
	}

	/// <summary>
	/// The gate is the count of base vertices <c>FUN_00469a80</c> finds (more than one, <c>0x0046a838</c>), not the node count:
	/// with none, or one, nothing is raised or bound though the model keeps every node; no outline raises nothing either.
	/// </summary>
	[TestMethod]
	public void AModelWithOneBaseVertexOrNoneOrAnItemWithNoOutlineIsGivenNone()
	{
		var objects = new ParkObjects( "jungle", null, null );
		var grid = ProceduralBase.ParkGrid;
		var bases = ProceduralBase.BaseVertices( BounceModel(), bounce.Shape, grid ).Single( b => b.Mesh == 0 ).Vertices;
		Assert.IsTrue( bases.Length > 2, $"the shipped Bounce's floor has {bases.Length} base vertices" );

		var none = Lifted( BounceModel(), _ => true );
		var one = Lifted( BounceModel(), i => i != bases[0] );

		foreach ( var (why, model, outline, count) in new[] { ("no base vertex", none, bounce.Hoarding, 0), ("one base vertex", one, bounce.Hoarding, 1),
			("no outline", BounceModel(), null, bases.Length) } )
		{
			ProceduralBase.FloorMesh( model, bounce.Shape, grid, out var counted );
			Assert.AreEqual( count, counted, $"{why}: the base vertices counted" );
			Assert.IsTrue( model.Nodes.Count > 1, $"{why}: every node is kept, so the node count cannot refuse it" );
			var state = new ParkState( false, 0 );

			Assert.IsNull( objects.HoardingFor( state, Placed(), outline, bounce.Shape, model ), why );
			Assert.IsNull( state.HoardingFor( 13 ), $"{why}: nothing is bound for the ride" );
		}
	}

	/// <summary>
	/// The panels are laid from the mesh with the most base vertices (<c>0x0046a82c</c>), wherever it stands among the meshes:
	/// the Bounce's floor moved to another walked node is still the one the panels are laid from, where the first mesh would lay others.
	/// </summary>
	[TestMethod]
	public void ThePanelsAreLaidFromTheMeshWithTheMostBaseVertices()
	{
		var grid = ProceduralBase.ParkGrid;
		var model = BounceModel();

		// Another mesh the walk takes, its vertices swapped with the floor's, so the floor's vertices now stand at that node.
		var swapped = Enumerable.Range( 1, model.Meshes.Count - 1 ).Select( k =>
		{
			var m = BounceModel();
			(m.Meshes[0].SourceVertices, m.Meshes[k].SourceVertices) = (m.Meshes[k].SourceVertices, m.Meshes[0].SourceVertices);
			return (k, m);
		} ).First( c => ProceduralBase.FloorMesh( c.m, bounce.Shape, grid, out _ ) == c.k );
		RideHoardingGeometry.Panel[] Laid( ModelFile.Mesh mesh ) => mesh.SourceVertices.Length < 2 ? []
			: RideHoardingGeometry.Build( bounce.Hoarding!, mesh.SourceVertices.Select( v => V3.Transform( v.GetSystemVector3(), mesh.WorldTransform ) ).ToArray() );
		var expected = Laid( swapped.m.Meshes[swapped.k] );
		var first = Laid( swapped.m.Meshes[0] );

		var hoarding = objects().HoardingFor( new ParkState( false, 0 ), Placed(), bounce.Hoarding, bounce.Shape, swapped.m );

		Assert.IsNotNull( hoarding, $"the floor at mesh {swapped.k}" );
		Assert.AreEqual( 12, hoarding.Value.Panels.Length );
		CollectionAssert.AreEqual( expected.Select( p => p.A ).ToArray(), hoarding.Value.Panels.Select( p => p.A ).ToArray(), "the floor's own endpoints, through its node's matrix" );
		Assert.IsFalse( first.Length == expected.Length && first.Select( p => p.A ).SequenceEqual( expected.Select( p => p.A ) ),
			"the first mesh would lay other endpoints" );

		ParkObjects objects() => new( "jungle", null, null );
	}

	/// <summary>The model with every vertex of its first mesh that <paramref name="lift"/> picks raised off the ground (the model's Y).</summary>
	private static ModelFile Lifted( ModelFile model, Func<int, bool> lift )
	{
		var vertices = model.Meshes[0].SourceVertices;
		for ( var i = 0; i < vertices.Length; ++i )
			if ( lift( i ) ) vertices[i] = new OpenTPW.Vector3( vertices[i].X, vertices[i].Y + 5f, vertices[i].Z );
		return model;
	}

	/// <summary>The park file's saved progress for the ride's own slot and item is laid over the fresh hoarding (<c>docs/exe/ride-hoardings.md</c>).</summary>
	[TestMethod]
	public void TheFilesSavedProgressForTheRidesSlotAndItemIsRestored()
	{
		var state = new ParkState( false, 0 );
		var objects = new ParkObjects( "jungle", null, null );
		objects.TakeSavedThings( Saved( bounce.Id, 0x45, 0.6f ) );

		var progress = objects.HoardingFor( state, Placed( meshInstance: 1 ), bounce.Hoarding, bounce.Shape, BounceModel() )!.Value.Progress;

		Assert.IsTrue( progress.Active );
		Assert.AreEqual( 3, progress.Texture );
		Assert.AreEqual( 0.6f, progress.Progress, 0.00001f );
		Assert.AreEqual( -0.3f, progress.Rate );
	}

	/// <summary>A record saved under another item (a slot the file has reused) or for another slot is not this ride's: it stands at rest.</summary>
	[TestMethod]
	public void ARecordOfAnotherItemOrAnotherSlotIsNotRestored()
	{
		foreach ( var (why, item, meshInstance) in new[] { ("another item", bounce.Id + 1, 1), ("another slot", bounce.Id, 2) } )
		{
			var state = new ParkState( false, 0 );
			var objects = new ParkObjects( "jungle", null, null );
			objects.TakeSavedThings( Saved( item, 0x45, 0.6f ) );

			var progress = objects.HoardingFor( state, Placed( meshInstance ), bounce.Hoarding, bounce.Shape, BounceModel() )!.Value.Progress;

			Assert.IsFalse( progress.Active, why );
			Assert.AreEqual( 0f, progress.Progress, why );
		}
	}

	/// <summary>Panels are handed out in the original's order, the angle of each round the outline's middle (<c>FUN_00470630</c>), not the order the cells are walked.</summary>
	[TestMethod]
	public void ThePanelsComeOutInTheOrderOfTheirAngle()
	{
		var model = BounceModel();
		var mesh = model.Meshes[0];
		var points = mesh.SourceVertices.Select( v => V3.Transform( v.GetSystemVector3(), mesh.WorldTransform ) ).ToArray();
		var panels = RideHoardingGeometry.Build( bounce.Hoarding!, points );

		Assert.AreEqual( 12, panels.Length );
		CollectionAssert.AreEqual( panels.Select( p => p.Order ).Order().ToArray(), panels.Select( p => p.Order ).ToArray(), "ascending" );
		Assert.IsTrue( panels.Select( p => p.Order ).Distinct().Count() > 1, "and the angles differ, so an unsorted list is told from a sorted one" );

		// The shipped Bounce's order worked from the executable (FUN_00452e70 keys, FUN_00470630 ranks ascending and stable), not from our
		// output: each panel's middle about the outline's centre, atan2 plus 5pi/4, modulo 2pi. Panel (0,4)'s middle lies exactly on the 135 degree
		// diagonal, so it is last at 53 or 64 bits (the 0x27f Proton runs under, Q221 (7)); at 24 bits it would round to 0 and come first.
		CollectionAssert.AreEqual(
			new System.Numerics.Vector2[] { new( 0, 3 ), new( 0, 2 ), new( 0, 1 ), new( 0, 0 ), new( 2, 0 ), new( 3, 0 ), new( 3, 1 ), new( 3, 2 ), new( 3, 3 ), new( 3, 4 ), new( 1, 4 ), new( 0, 4 ) },
			panels.Select( p => p.GridA ).ToArray(), "the original's rank" );
	}

	/// <summary>
	/// A part-way hoarding's eight vertices a panel: the upper corners rise by the panel's height over the ground at their end,
	/// the lower stay on it, and the lower corners' V slides up as the panel rises (amount worked here from the rate, not from
	/// <see cref="RideHoardingState.Panel"/>).
	/// </summary>
	[TestMethod]
	public void FillRaisesTheUpperCornersAndSlidesTheLowerV()
	{
		var model = BounceModel();
		var mesh = model.Meshes[0];
		var points = mesh.SourceVertices.Select( v => V3.Transform( v.GetSystemVector3(), mesh.WorldTransform ) ).ToArray();
		var panels = RideHoardingGeometry.Build( bounce.Hoarding!, points );
		var heights = Enumerable.Range( 0, panels.Length * 2 ).Select( i => 1f + 0.25f * i ).ToArray();
		var state = new RideHoardingState();
		state.Restore( 0x45, 0.5f );
		Assert.AreEqual( 3, state.Texture, "a texture other than the first, so a texture left at 0 is seen" );
		var vertices = new Vertex[panels.Length * 8];

		ParkRideHoarding.Fill( vertices, panels, heights, state );

		var partial = 0;

		for ( var i = 0; i < panels.Length; ++i )
		{
			var amount = Math.Clamp( 4f * 0.5f - 3.2f * i / panels.Length, 0f, 1f ) * (i % 2 == 0 ? 1f : 0.8f);
			partial += amount is > 0f and < 0.8f ? 1 : 0;

			for ( var side = 0; side < 2; ++side )
			for ( var corner = 0; corner < 4; ++corner )
			{
				var v = vertices[i * 8 + side * 4 + corner];
				var end = corner & 1;
				var upper = corner < 2;
				var at = end == 0 ? panels[i].A : panels[i].B;
				var label = $"panel {i} side {side} corner {corner}";

				Assert.AreEqual( at.X, v.Position.X, label );
				Assert.AreEqual( at.Y, v.Position.Y, label );
				Assert.AreEqual( heights[i * 2 + end] + (upper ? 10f * amount : 0f), v.Position.Z, 0.0001f, label );
				Assert.AreEqual( end, v.TexCoords.X, label );
				Assert.AreEqual( upper ? 1f : 1f - amount, v.TexCoords.Y, 0.0001f, label );
				Assert.AreEqual( state.Texture, v.TexIndex, label );
				Assert.AreEqual( side == 0 ? panels[i].Normal.X : -panels[i].Normal.X, v.Normal.X, label );
				Assert.AreEqual( side == 0 ? panels[i].Normal.Y : -panels[i].Normal.Y, v.Normal.Y, label );
			}
		}

		Assert.IsTrue( partial > 0, "some panels are part-way, so the V is not 0 or 1 everywhere" );
	}

	/// <summary>
	/// The ground a panel's end stands on is read 0.1 of a cell on (<c>FUN_00454050</c>), inside the inclusive rectangle: on an
	/// outline 11 cells across, grid line 1 carries to 10/11 of a cell, which the tenth takes to the next vertex and a plain cut
	/// leaves on the first. The ground is a slope whose every vertex differs, so the vertex read is the one asked for.
	/// </summary>
	[TestMethod]
	public void TheGroundIsReadATenthOfACellOn()
	{
		using var input = data.OpenRead( "levels/jungle/terrain/base.md2" );
		var field = new HeightfieldFile( input );
		for ( var y = 0; y <= field.CellsY; ++y )
		for ( var x = 0; x <= field.CellsX; ++x )
			field.Heights[y * (field.CellsX + 1) + x] = x * 3 + y * 1000;

		var origin = new V3( 40 * field.CellSizeX, 50 * field.CellSizeY, 7f );
		var size = new System.Numerics.Vector2( 11, 11 );
		var turn = System.Numerics.Quaternion.Identity;

		Assert.AreEqual( (field.HeightAt( 41, 50 ) - 7f) * 0.5f, ParkRideHoarding.Ground( field, new( 1, 0 ), size, (OpenTPW.Vector3)origin, turn ), 0.0001f, "x: 40 + 10/11 + 0.1 is 41" );
		Assert.AreEqual( (field.HeightAt( 40, 51 ) - 7f) * 0.5f, ParkRideHoarding.Ground( field, new( 0, 1 ), size, (OpenTPW.Vector3)origin, turn ), 0.0001f, "y: 50 + 10/11 + 0.1 is 51" );
		Assert.AreEqual( (field.HeightAt( 50, 60 ) - 7f) * 0.5f, ParkRideHoarding.Ground( field, new( 11, 11 ), size, (OpenTPW.Vector3)origin, turn ), 0.0001f, "the last grid line reads the last cell, 40 + 11 - 1" );
	}
}
