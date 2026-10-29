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
/// numbers (Q812, <c>original/basevertices.py</c> on Alexah's New Save): every listed base vertex on its corner, 917 used of 925 listed, and
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
	/// model stays where it was (the original: 917 used of 925 listed on, the Sun God 18 of 18, the Tom Tom Twister 20 of 20, the Costume Shop
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

	/// <summary>
	/// <b>The interpolating count lists the vertices the original listed</b> (Q812, <c>original/basevertices.py</c> on Alexah's New Save:
	/// the kart station's 16 on <c>gk_TRACK01</c>, the Lava Jump's 20 on <c>gk_brdge</c>), and only the nine meshes the lookup records mark
	/// <c>0x8</c> list any.
	/// </summary>
	/// <remarks><b>Mutations:</b> the tolerance 0.01 for 0.1; both axes required to be on a line; the height limit 0.01 for 100; the floor
	/// mesh not left out; the outline's sides ignored.</remarks>
	[TestMethod]
	public void TheInterpolatingCountListsTheVerticesTheOriginalListed()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var listed = new System.Collections.Generic.List<string>();

		for ( var id = 1000; id < 1600; ++id )
		{
			if ( !catalogue.TryGet( id, out var item ) )
				continue;

			ModelFile file;

			try { file = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" ); }
			catch ( Exception ) { continue; }

			foreach ( var (mesh, vertices) in ProceduralBase.InterpolatedVertices( file, item.Shape, ProceduralBase.ParkGrid ) )
				listed.Add( $"{item.Stem.ToLowerInvariant()}:{file.Meshes[mesh].Name.TrimEnd( '\0' )}:{vertices.Length}" );
		}

		CollectionAssert.AreEquivalent( new[] { "gokarts:gk_TRACK01:16", "lavajump:gk_brdge:20" }, listed.Where( l => l.StartsWith( "gokarts" ) || l.StartsWith( "lavajump" ) ).ToArray(), string.Join( ", ", listed ) );
		Assert.IsTrue( listed.All( l => l.Split( ':' )[1] is "gk_TRACK01" or "gk_brdge" or "wr_log1" or "wr_log2" or "wr_water3" or "wr_water4" or "wr_mwater1" or "wr_mwater2" or "wr_mwater04" ),
			string.Join( ", ", listed ) );
	}

	/// <summary>The catalogue's item whose model has the stem, Lost Kingdom's ids running 1000 to 1599.</summary>
	private static ParkItemCatalogue.Item ItemNamed( ParkItemCatalogue catalogue, string stem )
	{
		for ( var id = 1000; id < 1600; ++id )
		{
			if ( catalogue.TryGet( id, out var item ) && item.Stem.Equals( stem, StringComparison.OrdinalIgnoreCase ) )
				return item;
		}

		Assert.Fail( $"no item {stem}" );
		return default;
	}

	/// <summary>A cell, in the range the models are made on, whose four corners all stand at heights at least 0.5 apart from the next, so a wrong neighbour or weight gives a different height.</summary>
	private (int X, int Y) CellOfFourHeights()
	{
		for ( var y = 8; y < 120; ++y )
		{
			for ( var x = 8; x < 120; ++x )
			{
				var corners = new[] { field.HeightAt( x, y ), field.HeightAt( x + 1, y ), field.HeightAt( x, y + 1 ), field.HeightAt( x + 1, y + 1 ), field.HeightAt( x - 1, y ), field.HeightAt( x, y - 1 ) };

				if ( corners.OrderBy( h => h ).Zip( corners.OrderBy( h => h ).Skip( 1 ) ).All( pair => pair.Second - pair.First >= 0.4f ) )
					return (x, y);
			}
		}

		Assert.Fail( "the jungle ground has no such cell" );
		return default;
	}

	/// <summary>
	/// <b>The interpolating pass stands a vertex on the line it is within 0.1 of, at the ground blended along that line</b> less the
	/// model's height, in the original's four arms (<c>0x0046a0b4</c>..<c>0x0046a0ea</c>, <c>0x0046a2a7</c>..<c>0x0046a32d</c>): off the
	/// x line the blend runs along x from the corner on the vertex's side, on it along y; either way from the low corner when the
	/// fraction is under a half, from the high one when not. The expected heights are written here as the straight line between the
	/// two corners the cell's edge joins.
	/// </summary>
	/// <remarks><b>Mutations:</b> the corner taken by truncation in the high arms; the neighbour on the wrong side; the fraction and its
	/// complement swapped; the model's height not taken off; the snap's 0.1 widened to 0.5 or dropped; the x and y arms swapped; the arm chosen by the y line (<c>onY</c>) rather than the x line's absence.</remarks>
	[TestMethod]
	public void TheInterpolatingPassBlendsTheGroundAlongTheLineInFourArms()
	{
		var (cx, cy) = CellOfFourHeights();
		const float baseHeight = 7.25f, rest = 3f;

		float H( int x, int y ) => field.HeightAt( x, y );
		float Along( float a, float b, float fraction ) => a + ((b - a) * fraction);

		Vec At( float gx, float gy ) => new( field.OriginX + (gx * field.CellSizeX), field.OriginY + (gy * field.CellSizeY), rest );

		void Expect( Vec world, float gx, float gy, float ground, string arm )
		{
			var stands = ProceduralConform.Interpolated( field, world, baseHeight );

			Assert.IsNotNull( stands, arm );
			Assert.AreEqual( field.OriginX + (gx * field.CellSizeX), stands!.Value.X, 0.001f, $"{arm}: x" );
			Assert.AreEqual( field.OriginY + (gy * field.CellSizeY), stands.Value.Y, 0.001f, $"{arm}: y" );
			Assert.AreEqual( rest + (ground - baseHeight), stands.Value.Z, 0.001f, $"{arm}: height" );
		}

		// Off the x line, on the y line: along x, from the low corner and from the high one.
		Expect( At( cx + 0.3f, cy ), cx + 0.3f, cy, Along( H( cx, cy ), H( cx + 1, cy ), 0.3f ), "x low" );
		Expect( At( cx + 0.7f, cy ), cx + 0.7f, cy, Along( H( cx, cy ), H( cx + 1, cy ), 0.7f ), "x high" );

		// On the x line, off the y line: along y.
		Expect( At( cx, cy + 0.3f ), cx, cy + 0.3f, Along( H( cx, cy ), H( cx, cy + 1 ), 0.3f ), "y low" );
		Expect( At( cx, cy + 0.7f ), cx, cy + 0.7f, Along( H( cx, cy ), H( cx, cy + 1 ), 0.7f ), "y high" );

		// Within 0.1 of a line is on it: the vertex snaps, the blend runs along the other axis, and the fraction used is the one past the cell's low line.
		Expect( At( cx + 0.06f, cy + 0.4f ), cx, cy + 0.4f, Along( H( cx, cy ), H( cx, cy + 1 ), 0.4f ), "x snapped low" );
		Expect( At( cx + 0.94f, cy + 0.4f ), cx + 1, cy + 0.4f, Along( H( cx + 1, cy ), H( cx + 1, cy + 1 ), 0.4f ), "x snapped high" );
		Expect( At( cx + 0.4f, cy + 0.94f ), cx + 0.4f, cy + 1, Along( H( cx, cy + 1 ), H( cx + 1, cy + 1 ), 0.4f ), "y snapped high" );

		// On a line both ways it takes the y arms, whatever the x (0x0046a09f tests x alone); on neither it takes an x arm and snaps nothing.
		Expect( At( cx + 0.05f, cy + 0.96f ), cx, cy + 1, Along( H( cx, cy ), H( cx, cy + 1 ), 0.96f ), "both on a line: the y arm" );
		Expect( At( cx + 0.3f, cy + 0.4f ), cx + 0.3f, cy + 0.4f, Along( H( cx, cy ), H( cx + 1, cy ), 0.3f ), "neither on a line: the x arm" );

		// Past 0.1 it is off the line and does not snap.
		var apart = ProceduralConform.Interpolated( field, At( cx + 0.13f, cy ), baseHeight );
		Assert.AreEqual( field.OriginX + ((cx + 0.13f) * field.CellSizeX), apart!.Value.X, 0.001f, "0.13 of a cell is off the line" );

		// The heightfield's last corner is a corner; past it ends the pass.
		Assert.IsNotNull( ProceduralConform.Interpolated( field, At( field.CellsX, cy + 0.3f ), baseHeight ), "on the last line" );
		Assert.IsNull( ProceduralConform.Interpolated( field, At( field.CellsX + 0.7f, cy ), baseHeight ), "past the last corner" );
		Assert.IsNull( ProceduralConform.Interpolated( field, At( cx, field.CellsY + 0.7f ), baseHeight ), "past the last row" );
	}

	/// <summary>
	/// <b>Made on a slope, the kart station's and the Lava Jump's listed vertices each stand on the ground along their line</b> and
	/// nothing else moves (the original: 16 and 20 vertices, each raised by the ground along its line less the root's height, to
	/// 0.0001). The ground is read here by the cell's own triangle at the snapped place, not by the pass's blend.
	/// </summary>
	/// <remarks><b>Mutations:</b> the model's height not taken off; every vertex of the mesh moved, not the listed ones; the floor mesh
	/// not left out of the list; the vertex carried back without the turn.</remarks>
	[TestMethod]
	public void TheKartStationsAndTheLavaJumpsListedVerticesStandOnTheGroundAlongTheirLine()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var checkedStems = 0;

		for ( var id = 1000; id < 1600; ++id )
		{
			if ( !catalogue.TryGet( id, out var item ) || item.Stem.ToLowerInvariant() is not ("gokarts" or "lavajump") )
				continue;

			foreach ( var angle in new[] { 0, 90, 180, 270 } )
			{
				var (x, y) = SlopedSite( item, angle );
				var origin = ParkObjects.OriginFor( x, y, angle, field );
				origin = origin with { Z = ParkObjects.BaseHeight( item.Shape, origin, angle, field ) ?? origin.Z };

				var at = origin.GetSystemVector3();
				var turn = ParkObjects.Turn( angle );
				var before = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				var made = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				var listed = ProceduralBase.InterpolatedVertices( made, item.Shape, ProceduralBase.ParkGrid );
				var count = listed.Sum( pair => pair.Vertices.Length );
				var moved = ProceduralConform.Interpolate( made, item.Shape, at, turn, field );

				Assert.IsTrue( count > 0, $"{item.Stem}: lists vertices" );
				Assert.AreEqual( count, moved, $"{item.Stem} turned {angle}: vertices moved" );

				var named = listed.ToDictionary( pair => pair.Mesh, pair => pair.Vertices.ToHashSet() );
				var risen = 0;

				for ( var m = 0; m < before.Meshes.Count; ++m )
				{
					for ( var i = 0; i < before.Meshes[m].Vertices.Length; ++i )
					{
						var restPlace = Drawn( before.Meshes[m], before.Meshes[m].Vertices[i].Position.GetSystemVector3(), at, turn );
						var now = Drawn( made.Meshes[m], made.Meshes[m].Vertices[i].Position.GetSystemVector3(), at, turn );

						if ( !named.TryGetValue( m, out var which ) || !which.Contains( before.Meshes[m].VertexOrder[i] ) )
						{
							Assert.AreEqual( restPlace, now, $"{item.Stem} mesh {m} vertex {i} is not listed and stays" );
							continue;
						}

						// A listed vertex sits on the grid in the model, so the snap does not move it; the ground at its place less the model's height is its rise.
						Assert.AreEqual( restPlace.X, now.X, 0.002f, $"{item.Stem} turned {angle} mesh {m} vertex {i}: x" );
						Assert.AreEqual( restPlace.Y, now.Y, 0.002f, $"{item.Stem} turned {angle} mesh {m} vertex {i}: y" );

						var rise = ProceduralConform.DrapeHeight( field, now.X, now.Y ) - origin.Z;
						Assert.AreEqual( restPlace.Z + rise, now.Z, 0.002f, $"{item.Stem} turned {angle} mesh {m} vertex {i}: height" );
						risen += MathF.Abs( rise ) > 0.3f ? 1 : 0;
					}
				}

				Assert.IsTrue( risen > 0, $"{item.Stem} turned {angle} at ({x},{y}): a level site proves nothing" );
			}

			++checkedStems;
		}

		Assert.AreEqual( 2, checkedStems, "the kart station and the Lava Jump" );
	}

	/// <summary>A model with no mesh the lookup records mark <c>0x8</c> is left alone by the interpolating pass.</summary>
	[TestMethod]
	public void AModelWithNoInterpolatedMeshIsLeftAlone()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		Assert.IsTrue( catalogue.TryGet( 1100, out var item ) );

		var file = new ModelFile( $"{item!.Directory}/{item.Stem}.MD2" );
		var (x, y) = SlopedSite( item, 0 );

		Assert.AreEqual( 0, ProceduralConform.Interpolate( file, item.Shape, ParkObjects.OriginFor( x, y, 0, field ).GetSystemVector3(), ParkObjects.Turn( 0 ), field ) );
	}

	/// <summary>
	/// <b>The interpolating count takes a vertex within 0.1 of a cell line, and the conform's within 0.01</b> (<c>0x00469a96</c>,
	/// <c>0x00469aa8</c>): the station's and the Lava Jump's lists stand when the grid is moved 0.4 of a unit under the model (0.04 of a
	/// cell) and are lost when it is moved the other way by 1.5 (0.15 of a cell; a position below the grid's origin is taken as on a line, so the grid is not moved above the model).
	/// </summary>
	/// <remarks><b>Mutations:</b> the interpolating tolerance 0.01 or 0.5.</remarks>
	[TestMethod]
	public void TheInterpolatingCountTakesAVertexWithinATenthOfACellOfALine()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		foreach ( var (stem, expected) in new[] { ("gokarts", 16), ("lavajump", 20) })
		{
			var item = ItemNamed( catalogue, stem );
			var file = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			int Listed( float shift ) => ProceduralBase.InterpolatedVertices( file, item.Shape, new ProceduralBase.Grid( shift, shift, 10f, 10f ) ).Sum( pair => pair.Vertices.Length );

			Assert.AreEqual( expected, Listed( 0f ), $"{stem}: on the grid" );
			Assert.AreEqual( expected, Listed( -0.4f ), $"{stem}: 0.04 of a cell below" );
			Assert.AreEqual( expected, Listed( 0.4f ), $"{stem}: 0.04 of a cell above" );
			Assert.AreEqual( 0, Listed( -1.5f ), $"{stem}: 0.15 of a cell is off the line" );
		}
	}

	/// <summary>
	/// <b>The ground's edge ends the pass for the mesh:</b> a vertex whose corner is past the heightfield's last stops the vertices
	/// after it in the list (<c>0x00469dd7</c>..<c>0x0046a2a0</c>), the ones before it standing already.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pass carrying on past the vertex; the last corner counted as past.</remarks>
	[TestMethod]
	public void AVertexPastTheGroundsLastCornerEndsTheMeshsPass()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var item = ItemNamed( catalogue, "gokarts" );
		var file = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
		var listed = ProceduralBase.InterpolatedVertices( file, item.Shape, ProceduralBase.ParkGrid );
		var all = listed.Sum( pair => pair.Vertices.Length );

		var after = 0;
		var standing = 0;

		// The station's listed vertices stand in two columns 40 units apart (x 0 and 40 in the model): put the model at each turn with the far
		// edge of the heightfield between them or beside them, so some listed vertices are past it and others, before and after them in the list, not.
		foreach ( var angle in new[] { 0, 90, 180, 270 } )
		{
			foreach ( var (shiftX, shiftY) in new[] { -20f, 20f, -150f }.SelectMany( x => new[] { -150f, -20f, -13f, -7f, 0f }.Select( y => (x, y) ) ) )
			{
				var at = new Vec( field.OriginX + (field.CellsX * field.CellSizeX) + shiftX, field.OriginY + (field.CellsY * field.CellSizeY) + shiftY, 0f );
				var turn = ParkObjects.Turn( angle );
				var made = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				var rest = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				var moved = ProceduralConform.Interpolate( made, item.Shape, at, turn, field );

				Assert.IsTrue( moved <= all, $"turned {angle}: no more than the {all} listed vertices move" );

				foreach ( var (mesh, vertices) in listed )
				{
					var stopped = false;

					foreach ( var vertex in vertices )
					{
						var world = Drawn( rest.Meshes[mesh], rest.Meshes[mesh].SourceVertices[vertex].GetSystemVector3(), at, turn );
						var drawnAt = Enumerable.Range( 0, rest.Meshes[mesh].VertexOrder.Length ).First( i => rest.Meshes[mesh].VertexOrder[i] == vertex );
						var now = Drawn( made.Meshes[mesh], made.Meshes[mesh].Vertices[drawnAt].Position.GetSystemVector3(), at, turn );
						var past = (world.X - field.OriginX) / field.CellSizeX >= field.CellsX + 0.5f || (world.Y - field.OriginY) / field.CellSizeY >= field.CellsY + 0.5f;

						// The nearest corner is past the last when the vertex stands past half a cell beyond it.
						if ( past )
							stopped = true;
						else if ( stopped )
						{
							// A vertex after the first one past the edge, itself inside, is left where it was.
							Assert.AreEqual( world, now, $"turned {angle} mesh {mesh} vertex {vertex} comes after the one past the edge and is not moved" );
							++after;
						}
						else
							standing += now != world ? 1 : 0;
					}
				}
			}
		}

		Assert.IsTrue( standing > 0, "some vertex before the edge was raised" );
		Assert.IsTrue( after > 0, "some vertex inside the edge comes after one past it, which a pass that carried on would raise" );
	}

	/// <summary>
	/// <b>A placed thing's making runs all three passes:</b> the rim conform, the drape and the interpolating pass, so the station
	/// moves its 14 listed base vertices and its 16 interpolated ones (<c>rim</c>: <c>moved 30</c> on the original's 14 and 16).
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="ParkObjects.StandOnGround"/> not calling the interpolating pass.</remarks>
	[TestMethod]
	public void AThingsMakingRunsTheInterpolatingPassToo()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var item = ItemNamed( catalogue, "gokarts" );
		var file = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

		var (x, y) = SlopedSite( item, 0 );
		var origin = ParkObjects.OriginFor( x, y, 0, field );
		origin = origin with { Z = ParkObjects.BaseHeight( item.Shape, origin, 0, field ) ?? origin.Z };

		var listed = ProceduralBase.BaseVertices( file, item.Shape, ProceduralBase.ParkGrid ).Sum( pair => pair.Vertices.Length )
			+ ProceduralBase.InterpolatedVertices( file, item.Shape, ProceduralBase.ParkGrid ).Sum( pair => pair.Vertices.Length );

		Assert.AreEqual( 30, listed, "the original's 14 base vertices and 16 interpolated" );
		Assert.AreEqual( listed, ParkObjects.StandOnGround( file, item, origin, ParkObjects.Turn( 0 ), field ) );
	}

	/// <summary>
	/// <b>A model with a lookup record marked <c>0x10000</c> is not counted for the conform</b> (<c>0x0044a94b</c>, <c>0x00469ad0</c>): the
	/// Dino Karts Tunnel lists 7 base vertices, all on <c>gk_tracktop01</c> (the original's <c>bv75</c>), and none on <c>gk_mam1</c>, which the drape takes.
	/// </summary>
	/// <remarks><b>Mutation:</b> the <c>IdFlags &amp; 0x10000</c> skip in <see cref="ProceduralBase.BaseVertices"/> removed (14 listed, 8 on <c>gk_mam1</c>).</remarks>
	[TestMethod]
	public void ADrapedMeshListsNoBaseVertex()
	{
		var item = ItemNamed( new ParkItemCatalogue( "jungle", FileSystem ), "mammtunn" );
		var file = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
		var listed = ProceduralBase.BaseVertices( file, item.Shape, new ProceduralBase.Grid( field.OriginX, field.OriginY, field.CellSizeX, field.CellSizeY ) );

		CollectionAssert.AreEqual( new[] { "gk_tracktop01" }, listed.Select( pair => file.Meshes[pair.Mesh].Name.TrimEnd( '\0' ) ).ToArray() );
		Assert.AreEqual( 7, listed.Sum( pair => pair.Vertices.Length ) );
	}

	/// <summary>
	/// <b>A placed thing is stood on the ground by the conform of its listed rim and the drape of its lookup-marked meshes only</b>
	/// (<c>StandOnGround</c>): on the Dino Karts Tunnel at a sloped site <c>gk_mam1</c> and the listed vertices move and every other vertex stays.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>StandOnGround</c> passing <see cref="ProceduralConform.EveryMesh"/> in place of <see cref="ProceduralConform.LookupDraped"/>.</remarks>
	[TestMethod]
	public void StandingAThingDrapesOnlyTheMeshesItsLookupMarks()
	{
		var item = ItemNamed( new ParkItemCatalogue( "jungle", FileSystem ), "mammtunn" );
		var (x, y) = SlopedSite( item, 0 );
		var origin = ParkObjects.OriginFor( x, y, 0, field );
		var rest = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
		var made = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
		var listed = ProceduralBase.BaseVertices( made, item.Shape, new ProceduralBase.Grid( field.OriginX, field.OriginY, field.CellSizeX, field.CellSizeY ) )
			.ToDictionary( pair => pair.Mesh, pair => pair.Vertices.ToHashSet() );

		Assert.IsTrue( ParkObjects.StandOnGround( made, item, origin, ParkObjects.Turn( 0 ), field ) > 0 );

		var drapedMesh = Enumerable.Range( 0, made.Meshes.Count ).Single( m => made.Meshes[m].Name.TrimEnd( '\0' ) == "gk_mam1" );
		var drapedMoved = 0;

		for ( var m = 0; m < made.Meshes.Count; ++m )
		{
			for ( var i = 0; i < made.Meshes[m].Vertices.Length; ++i )
			{
				var same = rest.Meshes[m].Vertices[i].Position.GetSystemVector3() == made.Meshes[m].Vertices[i].Position.GetSystemVector3();

				if ( m == drapedMesh )
					drapedMoved += same ? 0 : 1;
				else if ( !( listed.TryGetValue( m, out var rim ) && rim.Contains( made.Meshes[m].VertexOrder[i] ) ) )
					Assert.IsTrue( same, $"mesh {m} vertex {i} is neither draped nor listed and stays" );
			}
		}

		Assert.IsTrue( drapedMoved > 0, "gk_mam1 is draped" );
	}

	private static string Source( params string[] path ) => File.ReadAllText( Path.Join( RepoFiles.Root(), "source", Path.Combine( path ) ) );

	/// <summary>
	/// <b>Every place a park makes a model that stands on the ground hands <see cref="LobbyModel"/> its <c>stand</c> hook, and the model
	/// runs it before it builds its meshes.</b> The constructor needs a graphics device, so this reads the source: the four makers
	/// (placed things, queue pieces, boundary hoarding, track pieces) each pass <c>stand:</c>, and the constructor calls it before
	/// the entities are built from the file's vertices.
	/// </summary>
	/// <remarks><b>Mutations:</b> a <c>stand:</c> argument removed from any of the four; <c>stand?.Invoke</c> moved below the entities' construction.</remarks>
	[TestMethod]
	public void EveryMakerOfAGroundedModelPassesTheStandHookAndTheModelRunsItFirst()
	{
		foreach ( var file in new[] { "ParkObjects.cs", "ParkQueues.cs", "ParkBoundaryHoarding.cs", "ParkTrackPieces.cs", "ParkCoasterPylons.cs" } )
		{
			var text = Source( "OpenTPW", "World", "Park", file );
			var from = text.IndexOf( "new LobbyModel(", StringComparison.Ordinal );
			var to = text.IndexOf( ");", from, StringComparison.Ordinal );

			Assert.IsTrue( from >= 0 && to > from, $"{file} makes a LobbyModel" );
			StringAssert.Contains( text.Substring( from, to - from ), "stand: file =>", $"{file}: the model is made with no stand hook" );
		}

		var model = Source( "OpenTPW", "World", "Lobby", "LobbyModel.cs" );
		var invoke = model.IndexOf( "stand?.Invoke( modelFile )", StringComparison.Ordinal );
		var built = model.IndexOf( "Entities = new ModelEntity[", StringComparison.Ordinal );
		var vertices = model.IndexOf( "meshVertices[", StringComparison.Ordinal );

		Assert.IsTrue( invoke > 0, "the constructor runs the stand hook" );
		Assert.IsTrue( invoke < built && invoke < vertices, "the stand hook runs before the meshes are built from the vertices" );
	}
}
