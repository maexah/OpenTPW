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
/// panels' order from <see cref="RideHoardingGeometry.Build"/>, and the ground <see cref="ParkRideHoarding.Ground(HeightfieldFile, System.Numerics.Vector2, Vector3, System.Numerics.Quaternion)"/>
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

	/// <summary>The shipped Bounce's model passes the gate: 12 panels, and the park keeps the progress it hands back.</summary>
	[TestMethod]
	public void AModelWithManyNodesAndAMeshIsGivenItsHoarding()
	{
		var state = new ParkState( false, 0 );
		var model = BounceModel();
		Assert.IsTrue( model.Nodes.Count > 1, "the shipped model has more than the one node the gate refuses" );

		var objects = new ParkObjects( "jungle", null, null );
		var hoarding = objects.HoardingFor( state, Placed(), bounce.Hoarding, model );

		Assert.IsNotNull( hoarding );
		Assert.AreEqual( 12, hoarding.Value.Panels.Length );
		Assert.AreSame( hoarding.Value.Progress, state.HoardingFor( 13 ), "the park keeps the progress the ride's doors drive" );
	}

	/// <summary>The gate is the node count (more than one) and a mesh; either missing, or no outline, raises nothing and binds nothing.</summary>
	[TestMethod]
	public void AModelOfOneNodeOrNoMeshOrAnItemWithNoOutlineIsGivenNone()
	{
		var objects = new ParkObjects( "jungle", null, null );

		var oneNode = BounceModel();
		oneNode.Nodes.RemoveRange( 1, oneNode.Nodes.Count - 1 );
		var noNodes = BounceModel();
		noNodes.Nodes.Clear();
		var noMesh = BounceModel();
		noMesh.Meshes.Clear();

		foreach ( var (why, model, outline) in new[] { ("one node", oneNode, bounce.Hoarding), ("no node", noNodes, bounce.Hoarding),
			("no mesh", noMesh, bounce.Hoarding), ("no outline", BounceModel(), null) } )
		{
			var state = new ParkState( false, 0 );

			Assert.IsNull( objects.HoardingFor( state, Placed(), outline, model ), why );
			Assert.IsNull( state.HoardingFor( 13 ), $"{why}: nothing is bound for the ride" );
		}
	}

	/// <summary>The park file's saved progress for the ride's own slot and item is laid over the fresh hoarding (<c>docs/exe/ride-hoardings.md</c>).</summary>
	[TestMethod]
	public void TheFilesSavedProgressForTheRidesSlotAndItemIsRestored()
	{
		var state = new ParkState( false, 0 );
		var objects = new ParkObjects( "jungle", null, null );
		objects.TakeSavedThings( Saved( bounce.Id, 0x45, 0.6f ) );

		var progress = objects.HoardingFor( state, Placed( meshInstance: 1 ), bounce.Hoarding, BounceModel() )!.Value.Progress;

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

			var progress = objects.HoardingFor( state, Placed( meshInstance ), bounce.Hoarding, BounceModel() )!.Value.Progress;

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
	/// The ground a panel's end stands on is read 0.1 of a cell on (<c>FUN_00454050</c>): a point 0.95 of the way across a cell
	/// reads the next cell's height. The cell is the one where both neighbours differ most, so the bias cannot be told from none on flat ground.
	/// </summary>
	[TestMethod]
	public void TheGroundIsReadATenthOfACellOn()
	{
		using var input = data.OpenRead( "levels/jungle/terrain/base.md2" );
		var field = new HeightfieldFile( input );

		// The cell where the heights to the right and below differ most from its own.
		var (xs, ys) = (0, 0);
		var best = 0f;

		for ( var y = 0; y < field.CellsY - 1; ++y )
		for ( var x = 0; x < field.CellsX - 1; ++x )
		{
			var step = Math.Min( Math.Abs( field.HeightAt( x + 1, y ) - field.HeightAt( x, y ) ),
				Math.Abs( field.HeightAt( x, y + 1 ) - field.HeightAt( x, y ) ) );

			if ( step > best )
				(xs, ys, best) = (x, y, step);
		}

		Assert.IsTrue( best > 1f, $"the jungle's heightfield is not flat everywhere (the largest step is {best})" );

		var origin = new V3( (xs + 0.95f) * field.CellSizeX, (ys + 0.95f) * field.CellSizeY, 3f );
		var ground = ParkRideHoarding.Ground( field, new( 0, 0 ), (OpenTPW.Vector3)origin, System.Numerics.Quaternion.Identity );

		Assert.AreEqual( (field.HeightAt( xs + 1, ys + 1 ) - 3f) * 0.5f, ground, 0.0001f, "both axes read the next cell" );

		var acrossOnly = new V3( (xs + 0.95f) * field.CellSizeX, ys * field.CellSizeY + 1f, 3f );
		var across = ParkRideHoarding.Ground( field, new( 0, 0 ), (OpenTPW.Vector3)acrossOnly, System.Numerics.Quaternion.Identity );

		Assert.AreEqual( (field.HeightAt( xs + 1, ys ) - 3f) * 0.5f, across, 0.0001f, "x alone reads the next cell" );

		var downOnly = new V3( xs * field.CellSizeX + 1f, (ys + 0.95f) * field.CellSizeY, 3f );
		var down = ParkRideHoarding.Ground( field, new( 0, 0 ), (OpenTPW.Vector3)downOnly, System.Numerics.Quaternion.Identity );

		Assert.AreEqual( (field.HeightAt( xs, ys + 1 ) - 3f) * 0.5f, down, 0.0001f, "y alone reads the next cell" );
	}
}
