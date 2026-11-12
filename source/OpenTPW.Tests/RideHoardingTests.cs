using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using V3 = System.Numerics.Vector3;

namespace OpenTPW.Tests;

[TestClass]
public class RideHoardingTests
{
	private static ItemHoarding Outline( string rows ) => new ItemDescriptionFile( "Info.Hoarding\n---\n" + rows + "\n---\n" ).Hoarding!;

	[TestMethod]
	public void HoardingAlphabetReversesRowsSkipsSpacesAndPads()
	{
		var h = Outline( ". ^ _ [ ] J F 7\nL = H C U n 3 O\n\n^" );
		Assert.AreEqual( 8, h.Width ); Assert.AreEqual( 4, h.Depth );
		CollectionAssert.AreEqual( new byte[] { 0, 1, 16, 64, 4, 20, 65, 5 }, Enumerable.Range( 0, 8 ).Select( x => h.At( x, 3 ) ).ToArray() );
		CollectionAssert.AreEqual( new byte[] { 80, 17, 68, 81, 84, 69, 21, 85 }, Enumerable.Range( 0, 8 ).Select( x => h.At( x, 2 ) ).ToArray() );
		Assert.AreEqual( (byte)1, h.At( 0, 0 ) ); Assert.AreEqual( (byte)0, h.At( 7, 0 ) ); Assert.AreEqual( (byte)0, h.At( 0, 1 ) );
	}

	[TestMethod]
	public void HoardingRejectsBadCharactersAndOversizedPictures()
	{
		Assert.ThrowsException<InvalidDataException>( () => Outline( "*" ) );
		Assert.ThrowsException<InvalidDataException>( () => Outline( new string( '.', 21 ) ) );
		Assert.ThrowsException<InvalidDataException>( () => Outline( string.Join( "\n", Enumerable.Repeat( ".", 21 ) ) ) );
	}

	[TestMethod]
	public void SingletonKeepsOriginalAsymmetricCornerAndFourEdges()
	{
		var panels = RideHoardingGeometry.Build( Outline( "O" ), [] );
		Assert.AreEqual( 4, panels.Length );
		var south = panels.Single( p => p.Normal.Y == -1 );
		Assert.AreEqual( 0.3f, south.A.X, 0.00001f ); Assert.AreEqual( 10.3f, south.B.X, 0.00001f );
		var east = panels.Single( p => p.Normal.X == 1 );
		Assert.AreEqual( 9.7f, east.A.X, 0.00001f );
		Assert.AreEqual( 0.6f, south.B.X - east.A.X, 0.00001f );
	}

	[TestMethod]
	public void SparseOutlineKeepsGapAndFitsSecondSourceVertex()
	{
		var panels = RideHoardingGeometry.Build( Outline( "^." ), [new( 0, 0, 10 ), new( 2, 0, 12 ), new( 100, 0, 100 )] );
		Assert.AreEqual( 1, panels.Length, "one marked edge, not a bounding rectangle" );
		Assert.AreEqual( 11f, panels[0].A.Y, 0.00001f, "second closest sets (12-10)/2" );
		Assert.AreEqual( 10f, panels[0].A.X ); Assert.AreEqual( 0f, panels[0].B.X );
	}

	[TestMethod]
	public void CloseOpenRatesReversalStaggerAndDelayedCleanup()
	{
		var h = new RideHoardingState(); h.Close(); h.Advance( 2.5f );
		Assert.AreEqual( 0.5f, h.Progress, 0.00001f );
		Assert.AreEqual( 10f, h.Panel( 0, 12 ).Height );
		Assert.AreEqual( 8f, h.Panel( 1, 12 ).Height );
		Assert.AreEqual( 0f, h.Panel( 11, 12 ).Height );
		h.Open(); h.Advance( 1f ); Assert.AreEqual( 0.2f, h.Progress, 0.00001f );
		h.Close(); Assert.AreEqual( 0.2f, h.Progress, 0.00001f ); h.Advance( 4.1f );
		Assert.AreEqual( 1f, h.Progress ); Assert.AreNotEqual( 0u, h.Flags & 2 );
		h.Advance( 0.1f ); Assert.AreEqual( 0u, h.Flags & 2 ); Assert.IsTrue( h.Active );
		h.Open(); h.Advance( 4f ); Assert.AreEqual( 0f, h.Progress ); Assert.IsTrue( h.Active );
		h.Advance( 0.1f ); Assert.IsFalse( h.Active ); Assert.AreEqual( 8u, h.Flags );
	}

	[TestMethod]
	public void WarningSurvivesOrdinaryCloseUntilOpeningOrWarningChange()
	{
		var h = new RideHoardingState(); h.Close( 2 ); h.Close(); Assert.AreEqual( 1, h.Texture );
		h.Close( 4 ); h.Close(); Assert.AreEqual( 2, h.Texture );
		h.Close( 8 ); h.Close(); Assert.AreEqual( 3, h.Texture );
		h.Open(); Assert.AreEqual( 3, h.Texture ); h.Close(); Assert.AreEqual( 0, h.Texture );
		h.Open(); h.Advance( 0.01f ); Assert.IsFalse( h.Active );
	}

	[TestMethod]
	public void EndpointIsNotFullHeightForEveryLargeOutlinePanel()
	{
		var h = new RideHoardingState(); h.Restore( 9, 1 );
		Assert.AreEqual( 8.8f, h.Panel( 39, 40 ).Height / 0.8f, 0.0001f );
		Assert.AreEqual( 0.296f, h.Panel( 39, 40 ).LowerV, 0.0001f );
	}

	[TestMethod]
	public void PackedSaveFieldsRestoreProgressSelectionAndMovement()
	{
		var record = new byte[0x37 + 44]; record[0] = 1;
		BitConverter.GetBytes( 1100 ).CopyTo( record, 1 );
		BitConverter.GetBytes( 0x145u ).CopyTo( record, 0x1d );
		BitConverter.GetBytes( 0.6f ).CopyTo( record, 0x23 );
		using var stream = new MemoryStream(); using var w = new BinaryWriter( stream );
		w.Write( Encoding.ASCII.GetBytes( "SYSG" ) ); w.Write( 12 + record.Length ); w.Write( 1 ); w.Write( 0 ); w.Write( 1 ); w.Write( record ); w.Flush();
		var saved = new ParkThingStates( stream.ToArray(), _ => 1 );
		Assert.IsNull( saved.Problem ); Assert.IsTrue( saved.ClosedOnEnd );
		var value = saved.Things.Single(); Assert.AreEqual( 0x45u, value.HoardingFlags );
		var h = new RideHoardingState(); h.Restore( value.HoardingFlags, value.HoardingProgress );
		Assert.IsTrue( h.Active ); Assert.AreEqual( 3, h.Texture ); Assert.AreEqual( -0.3f, h.Rate );
		h.Advance( 1 ); Assert.AreEqual( 0.3f, h.Progress, 0.00001f );
	}

	[TestMethod]
	public void DoorOperationDrivesBoundHoardingAndRemovalForgetsIt()
	{
		var state = new ParkState( false, 0 );
		state.AddObject( new ParkWorld.CatalogueObject( 13, 1100, 10 << 8, 10 << 8, 0, CanLoad: 1 ) );
		var h = state.BindHoarding( 13 );
		var op = new ParkRideOperation( state, new Dictionary<int, Peep>() );
		op.Close( null, 13 ); Assert.IsTrue( h.Active ); h.Advance( 1 ); Assert.AreEqual( 0.2f, h.Progress );
		op.Open( null, 13 ); Assert.AreEqual( -0.3f, h.Rate );
		state.RemoveObject( 13 ); Assert.IsNull( state.HoardingFor( 13 ) );
	}

	[TestMethod]
	public void EveryShippedHoardingHasACompleteBaseAndPreservesItsEdgeCount()
	{
		var data = GameData.Required();
		var items = 0; var hoardings = 0; var floors = 0; var minimum = int.MaxValue; var fewestBases = int.MaxValue;
		foreach ( var wad in Directory.GetFiles( Path.Combine( GameDir.Data, "levels" ), "*.wad", SearchOption.AllDirectories ) )
		{
			var directory = Path.GetRelativePath( GameDir.Data, Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
			var stem = Path.GetFileName( directory );
			if ( !data.GetFiles( directory ).Any( f => Path.GetFileName( f ).Equals( stem + ".sam", StringComparison.OrdinalIgnoreCase ) ) ) continue;
			using var sam = data.OpenRead( directory + "/" + stem + ".sam" );
			var description = new ItemDescriptionFile( sam );
			if ( description.Id == 0 ) continue;
			++items;
			if ( description.Hoarding is not { } h ) continue;
			++hoardings;
			Assert.AreEqual( (description.FootprintWidth, description.FootprintDepth), (h.Width, h.Depth), directory );
			using var md2 = data.OpenRead( directory + "/" + stem + ".md2" );
			var model = new ModelFile( md2 );
			Assert.IsTrue( model.Meshes.Count > 0, directory );
			var grid = new ProceduralBase.Grid( 0f, 0f, 10f, 10f );
			Assert.AreEqual( 0, ProceduralBase.FloorMesh( model, description.Shape, grid, out var bases ), $"{directory}: the mesh with the most base vertices" );
			Assert.IsTrue( bases > 1, $"{directory}: {bases} base vertices, which the gate (more than one, 0x0046a838) refuses" );
			fewestBases = Math.Min( fewestBases, bases );
			CollectionAssert.AreEqual( new[] { 0 }, ProceduralBase.BaseVertices( model, description.Shape, grid ).Select( b => b.Mesh ).ToArray(),
				$"{directory}: the meshes with base vertices, of {model.Meshes.Count}" );
			++floors;
			var mesh = model.Meshes[0]; Assert.AreEqual( mesh.TransformMatrix, mesh.WorldTransform, directory ); minimum = Math.Min( minimum, mesh.SourceVertices.Length );
			Assert.IsTrue( mesh.SourceVertices.Length >= 2, directory );
			var source = mesh.SourceVertices.Select( v => V3.Transform( v.GetSystemVector3(), mesh.WorldTransform ) ).ToArray();
			Assert.IsTrue( source.All( p => float.IsFinite( p.X ) && float.IsFinite( p.Y ) && float.IsFinite( p.Z ) ), directory );
			var panels = RideHoardingGeometry.Build( h, source );
			var expected = Enumerable.Range( 0, h.Width * h.Depth ).Sum( i => System.Numerics.BitOperations.PopCount( h.At( i % h.Width, i / h.Width ) ) );
			Assert.AreEqual( expected, panels.Length, directory );
			Assert.IsTrue( panels.All( p => float.IsFinite( p.A.X ) && float.IsFinite( p.A.Y ) && float.IsFinite( p.B.X ) && float.IsFinite( p.B.Y ) ), directory );
		}
		Console.WriteLine( $"hoarding corpus: items={items} outlines={hoardings} floorsAtMesh0={floors} minimumSourceVertices={minimum} fewestBaseVertices={fewestBases}" );
		Assert.AreEqual( 274, items );
		Assert.AreEqual( 129, hoardings );
		Assert.AreEqual( 129, floors );
	}

	/// <summary>
	/// <b>A panel endpoint stands on the ground inside the model's inclusive rectangle</b>, <c>+0xc0</c> its first cell and
	/// <c>+0xc4</c> its last (<c>FUN_00467030</c>, <c>0x004670c1</c>): the grid line <c>gx</c> of an outline <c>w</c> cells
	/// across reads <c>x0 + trunc( gx * (w - 1) / w + 0.1 )</c> (<c>FUN_00454050</c>, <c>FUN_00454190</c>;
	/// <c>docs/exe/ride-hoardings.md</c>). The expected vertex is worked here from that formula, carried between the turned
	/// rectangle's first and last cells; the ground is a slope whose every vertex differs, and the old rule's <c>x0 + gx</c>
	/// is shown to read other vertices.
	/// </summary>
	[TestMethod]
	public void TerrainEndpointsSampleTheOriginalsInclusiveRectangle()
	{
		var data = GameData.Required();
		using var input = data.OpenRead( "levels/jungle/terrain/base.md2" );
		var field = new HeightfieldFile( input );
		for ( var y = 0; y <= field.CellsY; ++y )
		for ( var x = 0; x <= field.CellsX; ++x )
			field.Heights[y * (field.CellsX + 1) + x] = x * 3 + y * 1000;
		const int W = 3, D = 4;
		var differs = 0;
		foreach ( var angle in new[] { 0, 90, 180, 270 } )
		{
			var turn = ParkObjects.Turn( angle );
			var anchor = new V3( 765, 645, 20 );
			var origin = anchor + V3.Transform( new V3( -5, -5, 0 ), turn );
			var corners = new[] { new V3( 0, 0, 0 ), new V3( W * 10, 0, 0 ), new V3( 0, D * 10, 0 ), new V3( W * 10, D * 10, 0 ) }
				.Select( v => V3.Transform( v, turn ) + origin ).ToArray();
			// The rectangle in cells, its last cell inclusive: one less than the far edge.
			var x0 = MathF.Round( corners.Min( v => v.X ) / 10 ); var x1 = MathF.Round( corners.Max( v => v.X ) / 10 ) - 1;
			var y0 = MathF.Round( corners.Min( v => v.Y ) / 10 ); var y1 = MathF.Round( corners.Max( v => v.Y ) / 10 ) - 1;
			Assert.AreEqual( angle % 180 == 0 ? (W - 1, D - 1) : (D - 1, W - 1), ((int)(x1 - x0), (int)(y1 - y0)), $"angle{angle}: the rectangle" );
			for ( var gy = 0; gy <= D; ++gy ) for ( var gx = 0; gx <= W; ++gx )
			{
				var u = gx / (float)W; var v = gy / (float)D;
				var (wx, wy) = angle switch
				{
					0 => (u * x1 + (1 - u) * x0, v * y1 + (1 - v) * y0),
					90 => (v * x1 + (1 - v) * x0, u * y0 + (1 - u) * y1),
					180 => (u * x0 + (1 - u) * x1, v * y0 + (1 - v) * y1),
					_ => (v * x0 + (1 - v) * x1, u * y1 + (1 - u) * y0)
				};
				var (vx, vy) = ((int)(wx + 0.1f), (int)(wy + 0.1f));
				if ( angle == 0 )
					Assert.AreEqual( ((int)x0 + (int)(gx * (W - 1) / (float)W + 0.1f), (int)y0 + (int)(gy * (D - 1) / (float)D + 0.1f)), (vx, vy), $"grid{gx},{gy}: the decoded formula" );
				var expected = (field.HeightAt( vx, vy ) - origin.Z) * 0.5f;
				var old = V3.Transform( new V3( gx * 10, gy * 10, 0 ), turn ) + origin;
				differs += field.HeightAt( (int)(old.X / 10 + 0.1f), (int)(old.Y / 10 + 0.1f) ) != field.HeightAt( vx, vy ) ? 1 : 0;
				Assert.AreEqual( expected, ParkRideHoarding.Ground( field, new( gx, gy ), new( W, D ), (OpenTPW.Vector3)origin, turn ), 0.0001f,
					$"angle{angle} grid{gx},{gy}" );
			}
		}
		Assert.IsTrue( differs > 0, "OpenTPW's former x0 + gx reads other vertices on this ground" );
		Console.WriteLine( $"terrain rectangle: {differs} of {4 * (W + 1) * (D + 1)} endpoints read another vertex than x0 + gx" );
	}

	[TestMethod]
	public void ShippedBounceHasTwelvePanelsAndItsOwnSavedSlot()
	{
		var data = GameData.Required(); FileSystem = data;
		var item = new ItemDescriptionFile( data.OpenRead( "levels/jungle/rides/bouncy/bouncy.sam" ) );
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( "levels/jungle/rides/bouncy/bouncy.md2" ) ) );
		var mesh = model.Meshes[0];
		var points = mesh.SourceVertices.Select( v => V3.Transform( v.GetSystemVector3(), mesh.WorldTransform ) ).ToArray();
		var panels = RideHoardingGeometry.Build( item.Hoarding!, points );
		Assert.AreEqual( 12, panels.Length );
		using var input = data.OpenRead( "levels/jungle/Easymode.TPWI" );
		var world = new ParkWorld( new SaveReader( input ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var saved = world.ThingStates( id => catalogue.ChannelsFor( id ) );
		Assert.IsNull( saved.Problem );
		Assert.AreEqual( 161, saved.Things.Count );
		Assert.IsTrue( saved.Things.All( s => float.IsFinite( s.HoardingProgress ) && s.HoardingProgress >= 0 && s.HoardingProgress <= 1 ) );
		foreach ( var placed in world.Objects.Where( o => o.IsPlaced ) )
			Assert.AreEqual( placed.CatalogueId, saved.Things.Single( s => s.Slot + 1 == placed.MeshInstance ).CatalogueId );
	}
}
