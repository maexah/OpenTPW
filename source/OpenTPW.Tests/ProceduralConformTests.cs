using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Vec = System.Numerics.Vector3;

namespace OpenTPW.Tests;

/// <summary>
/// How the original stands a made model's vertices on the ground (<c>docs/exe/park-engine.md</c>, "Where a placed object's model
/// stands"): a thing's floor rim on the terrain's corners (the conform), a queue piece draped vertex by vertex. The original's own
/// numbers (Q812, <c>original/basevertices.py</c> on Alexah's New Save): every listed base vertex on its corner, 917 of 917, and
/// each draped vertex raised by the ground under it less the piece's height to 0.0001. These read game files and skip without them.
/// </summary>
[TestClass]
public class ProceduralConformTests
{
	private BaseFileSystem? was;
	private HeightfieldFile field = null!;

	[TestInitialize]
	public void MountTheGame()
	{
		was = FileSystemOrNull;
		FileSystem = GameData.Required();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) );
		field = new HeightfieldFile( stream );
	}

	[TestCleanup]
	public void PutItBack() => FileSystemOrNull = was;

	/// <summary>
	/// Where a mesh's vertex is drawn: the draw path itself, <see cref="LobbyModel"/>'s - the vertex swapped to the world's axes,
	/// through the mesh's linear part, the mesh's offset added, the whole turned and moved to the model's origin.
	/// </summary>
	private static Vec Drawn( ModelFile.Mesh mesh, Vec position, Vec origin, Quaternion turn )
	{
		var world = mesh.WorldTransform;
		var linear = LobbyModel.ToWorldSpace( world );
		var offset = new System.Numerics.Vector3( world.M41, world.M43, world.M42 );
		var swizzled = new System.Numerics.Vector3( position.X, position.Z, position.Y );

		return System.Numerics.Vector3.Transform( System.Numerics.Vector3.Transform( swizzled, linear ) + offset, turn ) + origin;
	}

	private static System.Numerics.Vector3 Corner( HeightfieldFile field, int x, int y )
		=> new( x * field.CellSizeX, y * field.CellSizeY, field.HeightAt( x, y ) );

	/// <summary>The plane through three points, solved here by its normal, and its height at (x, y): the draped vertex's rule written another way.</summary>
	private static float OnPlane( System.Numerics.Vector3 a, System.Numerics.Vector3 b, System.Numerics.Vector3 c, float x, float y )
	{
		var normal = System.Numerics.Vector3.Cross( b - a, c - a );

		return a.Z - (((normal.X * (x - a.X)) + (normal.Y * (y - a.Y))) / normal.Z);
	}

	/// <summary>A cell, in the range the models are made on, whose four corners are not coplanar (a wrong diagonal gives a different height) and whose flag <c>0x4</c> is as asked.</summary>
	private (int X, int Y) CellWhere( bool other )
	{
		for ( var y = 8; y < 120; ++y )
		{
			for ( var x = 8; x < 120; ++x )
			{
				var twist = (field.HeightAt( x, y ) + field.HeightAt( x + 1, y + 1 )) - (field.HeightAt( x + 1, y ) + field.HeightAt( x, y + 1 ));

				if ( MathF.Abs( twist ) >= 1f && ((field.FlagsAt( x, y ) & 0x4) != 0) == other )
					return (x, y);
			}
		}

		Assert.Fail( "the jungle ground has no such cell" );
		return default;
	}

	/// <summary>
	/// <b>A draped vertex stands on the triangle its cell's flag <c>0x4</c> picks</b> (<c>0x0046a34c</c>; Q398's note): on a cell
	/// flagged so, below the other diagonal is the plane of (0,1), (1,1), (1,0), and a cell not flagged, of (0,0), (1,0), (1,1).
	/// Four points in a twisted cell of each kind, one on each side of its diagonal, against the plane through the three corners.
	/// </summary>
	/// <remarks><b>Mutations:</b> the flag test inverted; the diagonal's comparison reversed; <c>x</c> and <c>y</c> swapped.</remarks>
	[TestMethod]
	public void ADrapedVertexStandsOnTheTriangleTheCellsFlagPicks()
	{
		foreach ( var other in new[] { false, true } )
		{
			var (x, y) = CellWhere( other );
			var h00 = Corner( field, x, y );
			var h10 = Corner( field, x + 1, y );
			var h01 = Corner( field, x, y + 1 );
			var h11 = Corner( field, x + 1, y + 1 );

			foreach ( var (u, v) in new[] { (0.75f, 0.2f), (0.2f, 0.75f), (0.9f, 0.7f), (0.1f, 0.3f) } )
			{
				var at = new System.Numerics.Vector2( (x + u) * field.CellSizeX, (y + v) * field.CellSizeY );
				float expected;

				if ( !other )
					expected = v <= u ? OnPlane( h00, h10, h11, at.X, at.Y ) : OnPlane( h00, h01, h11, at.X, at.Y );
				else
					expected = 1f - v <= u ? OnPlane( h01, h11, h10, at.X, at.Y ) : OnPlane( h00, h10, h01, at.X, at.Y );

				Assert.AreEqual( expected, ProceduralConform.DrapeHeight( field, at.X, at.Y ), 0.001f, $"cell ({x},{y}) other {other} at ({u},{v})" );
			}
		}
	}

	/// <summary>
	/// <b>A queue piece's every vertex rises by the ground under it less the height it was made at</b> (<c>0x00469a80</c>, Q812:
	/// 78 pieces to 0.0001 in the original's memory), at every angle a piece is stood at and on a cell of each diagonal; the vertex
	/// is read where the model is drawn, so the turn and the node chain are the draw path's own.
	/// </summary>
	/// <remarks><b>Mutations:</b> the rise taken from the near corner; the height less H dropped; the vertex carried back without the turn;
	/// the rise added in the model's Y rather than the world's Z.</remarks>
	[TestMethod]
	public void AQueuePiecesVerticesRiseByTheGroundUnderThemLessItsHeight()
	{
		foreach ( var other in new[] { false, true } )
		{
			var (x, y) = CellWhere( other );

			foreach ( var angle in new[] { 0, 90, 180, 270 } )
			{
				var rest = new ModelFile( "levels/jungle/queue/queend.MD2" );
				var made = new ModelFile( "levels/jungle/queue/queend.MD2" );
				var origin = ParkObjects.OriginFor( x, y, angle, field ).GetSystemVector3();
				var turn = ParkObjects.Turn( angle );

				var moved = ProceduralConform.Drape( made, ProceduralConform.EveryMesh, origin, turn, field );
				var risen = 0;

				Assert.IsTrue( moved > 0, $"cell ({x},{y}) angle {angle}: nothing moved" );

				for ( var m = 0; m < rest.Meshes.Count; ++m )
				{
					for ( var i = 0; i < rest.Meshes[m].Vertices.Length; ++i )
					{
						var before = Drawn( rest.Meshes[m], rest.Meshes[m].Vertices[i].Position.GetSystemVector3(), origin, turn );
						var after = Drawn( made.Meshes[m], made.Meshes[m].Vertices[i].Position.GetSystemVector3(), origin, turn );
						var rise = ProceduralConform.DrapeHeight( field, before.X, before.Y ) - origin.Z;

						Assert.AreEqual( before.X, after.X, 0.001f, $"angle {angle} mesh {m} vertex {i} x" );
						Assert.AreEqual( before.Y, after.Y, 0.001f, $"angle {angle} mesh {m} vertex {i} y" );
						Assert.AreEqual( before.Z + rise, after.Z, 0.002f, $"cell ({x},{y}) angle {angle} mesh {m} vertex {i}" );
						risen += MathF.Abs( rise ) > 0.05f ? 1 : 0;
					}
				}

				Assert.IsTrue( risen > 0, $"cell ({x},{y}) angle {angle}: a level cell proves nothing" );
			}
		}
	}

	/// <summary>
	/// <b>A thing's listed base vertices all stand on a terrain corner</b> once the model is made on a slope, and the rest of the
	/// model stays where it was (the original: 917 of 917 on, the Sun God 18 of 18, the Tom Tom Twister 20 of 20, the Costume Shop
	/// 12 of 12). The Belly Bounce, the Huge Hollow Rock and a sign, at every turn, on ground spreading at least 1.5; before the
	/// conform some of the listed vertices are off, which is what a level site could not show.
	/// </summary>
	/// <remarks><b>Mutations:</b> the nearest corner taken by truncation; the corner's height added to the vertex's rather than set;
	/// the vertex carried back without the turn; every vertex of the mesh moved, not the listed ones.</remarks>
	[TestMethod]
	public void EveryListedBaseVertexStandsOnATerrainCornerAndNothingElseMoves()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		foreach ( var id in new[] { 1100, 1113, 1150 } )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );

			foreach ( var (angle, nudge) in new[] { (0, 0f), (90, 0f), (180, 0f), (270, 0f), (0, 0.004f), (90, -0.004f), (180, 0.004f), (270, -0.004f) } )
			{
				var (x, y) = SlopedSite( item, angle );
				var origin = ParkObjects.OriginFor( x, y, angle, field );
				origin = origin with { Z = ParkObjects.BaseHeight( item.Shape, origin, angle, field ) ?? origin.Z };

				// A nudge puts the model a hair off the grid line, the side a truncating rule gets wrong and a snap puts right.
				var at = origin.GetSystemVector3() + new Vec( nudge, -nudge, 0f );
				var turn = ParkObjects.Turn( angle );

				var rest = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				var made = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				var listed = ProceduralBase.BaseVertices( made, item.Shape, ProceduralBase.ParkGrid );
				var count = listed.Sum( pair => pair.Vertices.Length );

				var off = ProceduralConform.Measure( made, item.Shape, at, turn, field );
				Assert.AreEqual( count, off.Listed, $"{item.Stem} at ({x},{y}) turned {angle}: the listed vertices" );
				Assert.IsTrue( off.OnCorner < off.Listed, $"{item.Stem} at ({x},{y}) turned {angle}: a slope with every vertex already on a corner proves nothing" );

				Assert.AreEqual( count, ProceduralConform.Conform( made, item.Shape, at, turn, field ), $"{item.Stem}: vertices moved" );

				// Each listed vertex stands where the draw puts it on the corner its x and y name.
				var named = listed.ToDictionary( pair => pair.Mesh, pair => pair.Vertices.ToHashSet() );

				foreach ( var (mesh, vertices) in listed )
				{
					foreach ( var vertex in vertices )
					{
						var drawn = Enumerable.Range( 0, made.Meshes[mesh].VertexOrder.Length ).Where( i => made.Meshes[mesh].VertexOrder[i] == vertex );
						Assert.IsTrue( drawn.Any(), $"{item.Stem} mesh {mesh} vertex {vertex} is drawn" );

						foreach ( var i in drawn )
						{
							var world = Drawn( made.Meshes[mesh], made.Meshes[mesh].Vertices[i].Position.GetSystemVector3(), at, turn );

							// The corner is the one nearest where the vertex stood before it moved.
							var was = Drawn( rest.Meshes[mesh], rest.Meshes[mesh].Vertices[i].Position.GetSystemVector3(), at, turn );
							var cornerX = (int)MathF.Round( was.X / field.CellSizeX, MidpointRounding.AwayFromZero );
							var cornerY = (int)MathF.Round( was.Y / field.CellSizeY, MidpointRounding.AwayFromZero );

							Assert.AreEqual( field.HeightAt( cornerX, cornerY ), world.Z, 0.001f, $"{item.Stem} turned {angle} nudged {nudge} mesh {mesh} vertex {vertex}" );
							Assert.AreEqual( cornerX * field.CellSizeX, world.X, 0.0005f, $"{item.Stem} turned {angle} mesh {mesh} vertex {vertex}: x snapped to the line" );
							Assert.AreEqual( cornerY * field.CellSizeY, world.Y, 0.0005f, $"{item.Stem} turned {angle} mesh {mesh} vertex {vertex}: y snapped to the line" );
						}
					}
				}

				// And every other drawn vertex is where it was.
				for ( var m = 0; m < rest.Meshes.Count; ++m )
				{
					for ( var i = 0; i < rest.Meshes[m].Vertices.Length; ++i )
					{
						if ( named.TryGetValue( m, out var moved ) && moved.Contains( rest.Meshes[m].VertexOrder[i] ) )
							continue;

						Assert.AreEqual( rest.Meshes[m].Vertices[i].Position.GetSystemVector3(), made.Meshes[m].Vertices[i].Position.GetSystemVector3(),
							$"{item.Stem} mesh {m} vertex {i} is no base vertex and stays" );
					}
				}

				var after = ProceduralConform.Measure( made, item.Shape, at, turn, field );
				Assert.AreEqual( count, after.OnCorner, $"{item.Stem} at ({x},{y}) turned {angle}" );
				Assert.AreEqual( 0f, after.LargestOff, 0.001f );
			}
		}
	}

	/// <summary>The first cell, from (10, 10), where an item stood so has a footprint whose corners spread by at least 1.5.</summary>
	private (int X, int Y) SlopedSite( ParkItemCatalogue.Item item, int angle )
	{
		for ( var y = 10; y < 100; ++y )
		{
			for ( var x = 10; x < 100; ++x )
			{
				var origin = ParkObjects.OriginFor( x, y, angle, field );

				if ( ParkObjects.FootprintHeights( item.Shape, origin, angle, field ) is var (low, high) && high - low >= 1.5f )
					return (x, y);
			}
		}

		Assert.Fail( "no sloped site" );
		return default;
	}

	/// <summary>
	/// <b>Of Lost Kingdom's models only <c>mammtunn</c>'s <c>gk_mam1</c> is draped as a placed object</b> (lookup flag <c>0x10000</c>,
	/// <c>q812/lookflags.py</c>), and a model with no such record drapes none.
	/// </summary>
	[TestMethod]
	public void OnlyTheMammothTunnelsOwnMeshIsDrapedAsAPlacedObject()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var draped = new System.Collections.Generic.List<string>();

		for ( var id = 1000; id < 1600; ++id )
		{
			if ( !catalogue.TryGet( id, out var item ) )
				continue;

			ModelFile file;

			try { file = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" ); }
			catch ( Exception ) { continue; }

			var which = ProceduralConform.LookupDraped( file );

			for ( var mesh = 0; mesh < file.Meshes.Count; ++mesh )
			{
				if ( which( mesh ) )
					draped.Add( $"{item.Stem}:{file.Meshes[mesh].Name.TrimEnd( '\0' )}" );
			}
		}

		CollectionAssert.AreEqual( new[] { "mammtunn:gk_mam1" }, draped, string.Join( ", ", draped ) );
	}
}
