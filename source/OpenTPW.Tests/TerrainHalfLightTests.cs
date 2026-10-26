using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Files;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The terrain model's meshes on land outside the park: <c>Terrain_ScaleNormalsByHalfLight</c> (<c>0x00450b30</c>) makes a
/// vertex's normal 0.225 long where its posed place's cell is half lit, 1.0 elsewhere on the field, and leaves it off
/// the field; a material whose record's last word has <c>0x1</c> is drawn white (docs/exe/park-engine.md, "The terrain
/// model's meshes on that land are half lit too", "The water is drawn unlit, not half lit").
/// </summary>
[TestClass]
public class TerrainHalfLightTests
{
	private const string Terrain = "levels/jungle/terrain/base.MD2";

	/// <summary>
	/// The cell is the place times 0.1 truncated toward nought, so a point at x in (-10, 0) is column 0 and -10 is off the
	/// field; the field's far edges are off it too.
	/// </summary>
	/// <remarks><b>Mutations:</b> floored rather than truncated fails -9.99; the bounds tested inclusive fails 960 and
	/// 850; a signed test dropped fails -10; the x and z swapped fails (15, 845).</remarks>
	[TestMethod]
	public void TheCellIsTruncatedTowardNoughtAndHeldToTheField()
	{
		Assert.AreEqual( (0, 0), TerrainHalfLight.CellOf( -9.99f, -0.5f, 96, 85 ) );
		Assert.IsNull( TerrainHalfLight.CellOf( -10f, 5f, 96, 85 ) );
		Assert.IsNull( TerrainHalfLight.CellOf( 5f, -10f, 96, 85 ) );
		Assert.AreEqual( (1, 84), TerrainHalfLight.CellOf( 15f, 845f, 96, 85 ) );
		Assert.AreEqual( (95, 0), TerrainHalfLight.CellOf( 959.99f, 0f, 96, 85 ) );
		Assert.IsNull( TerrainHalfLight.CellOf( 960f, 0f, 96, 85 ) );
		Assert.IsNull( TerrainHalfLight.CellOf( 0f, 850f, 96, 85 ) );
	}

	/// <summary>
	/// A vertex is posed by its mesh's place in the tree before its cell is taken: the mesh moved 20 units along x puts
	/// its vertex at 5 on the half-lit column 2, not the lit column 0 its own place gives. A lit cell gives 1.0, a cell off
	/// the field leaves the normal as it was, and a zero normal stays zero.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pose dropped fails the first; the lengths swapped fails the first two; off the
	/// field given 1.0 fails the third (its normal is 2 long); the zero guard dropped fails the last (NaN).</remarks>
	[TestMethod]
	public void EachVertexTakesTheLengthOfItsPosedCell()
	{
		// Columns 0 and 1 lit, 2 half lit, one row deep.
		var light = new TerrainHalfLight( [false, false, true], 3, 1 );
		var mesh = new ModelFile.Mesh
		{
			Name = "test",
			Vertices =
			[
				new ModelFile.Vertex { Position = new Vector3( 5f, 0f, 5f ) },
				new ModelFile.Vertex { Position = new Vector3( -5f, 0f, 5f ) },
				new ModelFile.Vertex { Position = new Vector3( 15f, 0f, 5f ) },
				new ModelFile.Vertex { Position = new Vector3( 0f, 0f, 5f ) },
			],
			Normals = [new Vector3( 0f, 1f, 0f ), new Vector3( 0f, 1f, 0f ), new Vector3( 0f, 2f, 0f ), Vector3.Zero],
			WorldTransform = System.Numerics.Matrix4x4.CreateTranslation( 20f, 0f, 0f )
		};

		// The original's floats, not the code's constants: 0.225 at 0x0074cf30 (read 0.22499999) and 1.0 at 0x006fe380.
		Assert.AreEqual( 0.225f, light.Normal( mesh, 0 ).Length, 1e-6f, "posed to x 25, column 2: half" );
		Assert.AreEqual( 1f, light.Normal( mesh, 1 ).Length, 1e-6f, "posed to x 15, column 1: lit" );
		Assert.AreEqual( 2f, light.Normal( mesh, 2 ).Length, 1e-6f, "posed to x 35, off the field: left alone" );
		Assert.AreEqual( Vector3.Zero, light.Normal( mesh, 3 ), "posed to x 20, column 2: a zero normal stays zero" );
		Assert.AreEqual( (2, 1, 1), light.CountsFor( mesh ), "the census: two half, one full, one off the field" );
	}

	/// <summary>
	/// The stock Lost Kingdom park's terrain meshes take the lengths the running original's do (Q619, its live read):
	/// every vertex of the river meshes and of <c>newcliff01</c> to <c>10</c>, <c>newcliff11</c> 34 of its 130,
	/// every one of <c>volcanoe</c>'s 1,672 (the original makes 13 of them NaN, its zero normals), none of the approach's
	/// <c>road_center</c>; 135 meshes in all.
	/// </summary>
	/// <remarks><b>Mutations:</b> the cell's x and z swapped, or the mask read the other way round, fail the counts.</remarks>
	[TestMethod]
	public void TheStockJungleParksMeshesAreHalfLitAsTheOriginals()
	{
		var data = GameData.Required();
		FileSystem = data;
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var half = ParkGround.LandOutsideThePark( 96, 85, 95, 84, ( x, y ) => world.CellAt( x, y ).Flags );

		var model = new ModelFile( Terrain );
		var light = new TerrainHalfLight( half, 96, 85 );

		foreach ( var mesh in model.Meshes )
			for ( var i = 0; i < mesh.Vertices.Length; ++i )
				light.Normal( mesh, i );

		(int Half, int Full, int Off) Counts( string name ) => light.CountsFor( model.Meshes.Single( m => m.Name.TrimEnd( '\0' ).Trim() == name ) );

		var everyVertex = new[] { "river13", "river14", "river15", "river16", "river17", "river22", "river23", "river25", "river26" }
			.Concat( Enumerable.Range( 1, 10 ).Select( n => $"newcliff{n:00}" ) );

		foreach ( var name in everyVertex )
		{
			var counts = Counts( name );
			Assert.IsTrue( counts.Half > 0 && counts.Full == 0 && counts.Off == 0, $"{name}: every vertex half lit, {counts}" );
		}

		Assert.AreEqual( (178, 0, 0), Counts( "river13" ) );

		Assert.AreEqual( (34, 96, 0), Counts( "newcliff11" ) );
		Assert.AreEqual( 1672, Counts( "volcanoe" ).Half );
		Assert.AreEqual( (0, 248, 0), Counts( "road_center" ) );
		Assert.AreEqual( 135, model.Meshes.Count( m => light.CountsFor( m ).Half > 0 ) );
	}

	/// <summary>
	/// In <c>base.MD2</c> exactly the ten water materials carry the unlit bit (<c>lib/matword.py</c>), and a vertex of
	/// one is drawn with <see cref="LobbyModel.UnlitFlag"/>; the river bed under the water is not.
	/// </summary>
	/// <remarks><b>Mutations:</b> the word read as any non-zero value is the same here (every word is 0 or 1);
	/// <c>MaterialFlagsFor</c> dropping the flag fails the water; setting it always fails the bed.</remarks>
	[TestMethod]
	public void TheWatersMaterialsAreDrawnUnlit()
	{
		FileSystem = GameData.Required();
		var model = new ModelFile( Terrain );

		Assert.AreEqual( 10, model.Meshes.Sum( mesh => mesh.Materials.Count( m => m.IsUnlit ) ) );

		var textures = Enumerable.Range( 0, Material.TextureSlots ).Select( _ => Opaque() ).ToList();
		uint Flags( string meshName, bool unlit )
		{
			var mesh = model.Meshes.First( m => m.Name.TrimEnd( '\0' ).Trim() == meshName && m.Materials.Any( x => x.IsUnlit == unlit ) );
			var material = System.Array.FindIndex( mesh.Materials, m => m.IsUnlit == unlit );
			return LobbyModel.MaterialFlagsFor( mesh, textures, new bool[Material.TextureSlots], material );
		}

		Assert.AreNotEqual( 0u, Flags( "surface13", true ) & LobbyModel.UnlitFlag, "the water" );
		Assert.AreEqual( 0u, Flags( "river13", false ) & LobbyModel.UnlitFlag, "the bed" );
	}

	private static Texture Opaque()
	{
		var texture = (Texture)RuntimeHelpers.GetUninitializedObject( typeof( Texture ) );
		typeof( Texture ).GetProperty( nameof( Texture.Alpha ) )!.SetValue( texture, TextureAlpha.Opaque );
		return texture;
	}
}
