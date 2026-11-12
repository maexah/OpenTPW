using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.IO;
using System.Linq;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsVector4 = System.Numerics.Vector4;

namespace OpenTPW.Tests;

/// <summary>
/// The draws the original culls besides its models (<c>docs/exe/render-states.md</c>, "Which draws the test covers"; Q613):
/// the ground and the paths on it, the sea, the build markers and the interface's meshes are culled, keeping the side the
/// original keeps, and the weather is drawn both-sided as the original's <c>Batch_AddPolygon</c> draws it. Each face is
/// projected through the park's own lens and asked of <see cref="Material.Keeps"/>, the winding the culled pipeline keeps.
/// </summary>
/// <remarks><b>Mutations:</b> <c>DisableCulling</c> put back on a sheet's or the interface's flags, or taken off the
/// weather's; a cell's, a marker square's or the sea's triangles wound the other way; a wall laid one way only; the
/// interface's matrix with its y scale turned over; the device's clip space left with Vulkan's y, running down.</remarks>
[TestClass]
public class SheetAndInterfaceCullingTests
{
	/// <summary>
	/// <b>The sheets and the interface cull, the weather does not</b>: the ground's flags (which the paths take too), the sea's,
	/// the markers' and a UI part's ask for no <c>DisableCulling</c>; the weather's do, with the rest of its state kept.
	/// </summary>
	[TestMethod]
	public void TheSheetsAndTheInterfaceCullAndTheWeatherDrawsBothSides()
	{
		foreach ( var (name, flags) in new[]
		{
			("ground", ParkGround.DrawFlags), ("sea", ParkSea.DrawFlags), ("markers", ParkBuildMarkers.DrawFlags), ("interface", UiMesh.DrawFlags)
		} )
		{
			Assert.IsFalse( flags.HasFlag( MaterialFlags.DisableCulling ), $"the {name} is culled" );
			Assert.AreEqual( NeoVeldrid.FaceCullMode.Back, Material.CullModeFor( flags ), name );
		}

		Assert.AreEqual( MaterialFlags.DisableDepth, UiMesh.DrawFlags, "a UI part is drawn over everything" );
		Assert.AreEqual( MaterialFlags.DisableDepthWrite | MaterialFlags.Additive | MaterialFlags.DisableCulling, WeatherSprites.DrawFlags );
		Assert.AreEqual( NeoVeldrid.FaceCullMode.None, Material.CullModeFor( WeatherSprites.DrawFlags ) );

		var source = Path.Join( RepoFiles.Root(), "source", "OpenTPW", "World", "Park", "ParkPaths.cs" );

		if ( !File.Exists( source ) )
			Assert.Inconclusive( "no source tree beside the tests" );

		StringAssert.Contains( File.ReadAllText( source ), "\"content/shaders/test.shader\", ParkGround.DrawFlags )", "the paths take the ground's flags" );
	}

	/// <summary>
	/// <b>The device's clip space runs y up the screen</b>, the axes <see cref="Material.Keeps"/> winds in: with Vulkan's own y,
	/// running down, every face the GPU is handed would turn the other way round and the culled sheets and interface would be
	/// dropped from every eye.
	/// </summary>
	[TestMethod]
	public void TheDeviceAsksForTheClipSpaceTheKeptWindingIsWrittenIn()
	{
		foreach ( var vsync in new[] { true, false } )
		{
			var options = Renderer.DeviceOptions( vsync );

			Assert.IsTrue( options.PreferStandardClipSpaceYDirection, "the clip space's y runs up the screen" );
			Assert.IsTrue( options.PreferDepthRangeZeroToOne, "depth runs 0 to 1, as System.Numerics builds it" );
			Assert.AreEqual( vsync, options.SyncToVerticalBlank );
		}
	}

	/// <summary>
	/// <b>A ground or path cell keeps its upper side, from every eye above it, on either diagonal</b>: seen from eight turns
	/// round it and three heights, through the park's lens, both triangles of a cell cut either way are kept; seen from below,
	/// neither.
	/// </summary>
	[TestMethod]
	public void ACellKeepsItsUpperSideFromEveryEyeAbove()
	{
		var corners = Cell( 30, 25, (x, y) => 0.3f * x - 0.2f * y );

		foreach ( var flags in new ushort[] { 0x02, 0x04 } )
		{
			var elements = new uint[6];
			ParkGround.PutCellElements( elements, 0, 0, flags );

			AssertKeptFromAboveOnly( corners, elements, $"cell 0x{flags:x}" );
		}
	}

	/// <summary>
	/// <b>The sea's quilt keeps its upper side</b>: a quad laid as <c>Fill</c> lays it ([c][r], [c+1][r], [c][r+1], [c+1][r+1],
	/// <see cref="ParkSea.Point"/> at a phase that tilts it) and wound as <c>FUN_0053e530</c> winds it (<see cref="ParkSea.Elements"/>).
	/// </summary>
	[TestMethod]
	public void TheSeaKeepsItsUpperSide()
	{
		var corner = new Vector3( 380f, -180f, -5f );
		const float phase = 2.1f;
		var corners = new[]
		{
			ParkSea.Point( corner, 3, 4, phase ), ParkSea.Point( corner, 4, 4, phase ), ParkSea.Point( corner, 3, 5, phase ), ParkSea.Point( corner, 4, 5, phase )
		}.Select( point => new NumericsVector3( point.X, point.Y, point.Z ) ).ToArray();

		AssertKeptFromAboveOnly( corners, ParkSea.Elements( 1 ), "sea quad" );
	}

	/// <summary>
	/// <b>A marker square keeps its upper side and a wall shows both</b>: a square laid as <c>Fill</c> lays it ((x, y), (x + 1, y),
	/// (x + 1, y + 1), (x, y + 1)) is kept from above only; a wall's corners (<see cref="ParkBuildMarkers.WallCorners"/>) are
	/// laid both ways round, so from either side exactly one of each pair is kept.
	/// </summary>
	[TestMethod]
	public void AMarkerSquareKeepsItsUpperSideAndAWallShowsBothSides()
	{
		var elements = ParkBuildMarkers.Elements( 1, 1 );

		Assert.AreEqual( 18, elements.Length );
		AssertKeptFromAboveOnly( Cell( 40, 30, (x, y) => 21.5f ), elements[..6], "marker square" );

		var wall = ParkBuildMarkers.WallCorners( new ParkBuildMarkers.Wall( 53, 23, ParkBuildMarkers.FaceNorth, 1, 1.5f ), false )
			.Select( corner => new NumericsVector3( corner.X * 10f, corner.Y * 10f, corner.Over ) )
			.ToArray();
		var shifted = elements[6..].Select( index => index - 4u ).ToArray();
		var middle = new NumericsVector3( 535f, 230f, 6.5f );

		foreach ( var side in new[] { -1f, 1f } )
		{
			foreach ( var turn in new[] { -40f, 0f, 40f } )
			{
				var across = turn * MathF.PI / 180f;
				var eye = middle + (new NumericsVector3( 60f * MathF.Sin( across ), side * 60f * MathF.Cos( across ), 45f ));
				var kept = Kept( wall, shifted, eye, middle );

				Assert.AreEqual( 2, kept.Count( k => k ), $"one side's two triangles from y {side}, turned {turn}" );
				Assert.AreEqual( kept[0], kept[1], "the first side's triangles agree" );
				Assert.AreNotEqual( kept[0], kept[2], "the two sides differ" );
			}
		}
	}

	/// <summary>
	/// <b>Of the interface's faces, the culled pipeline drops exactly those the original's face test drops</b>: every mesh of
	/// every <c>ui/*.md2</c>, placed by its node chain as <c>UiMesh</c> places it and drawn through
	/// <see cref="UiMesh.ToScreen"/>, keeps a face exactly when it turns clockwise in the file's (x, y), y up. Of 278 models,
	/// 1,185 meshes and 6,310 faces, 422 are dropped, in 13 meshes of three models: <c>s_X1</c>'s ten (392), <c>s_key</c>'s two
	/// (14 each) and <c>warnlite</c>'s root (2), as <c>tools/harness/lib/uiwinding.py</c> counts them.
	/// </summary>
	[TestMethod]
	public void TheInterfaceDropsTheFacesTheOriginalsTestDrops()
	{
		FileSystem = GameData.Required();

		var models = FileSystem.GetFiles( "/ui" )
			.Where( file => file.EndsWith( ".md2", StringComparison.OrdinalIgnoreCase ) )
			.Select( file => Path.GetFileName( file ) )
			.ToArray();

		var (meshes, faces, dropped, edgeOn) = (0, 0, 0, 0);
		var byModel = new System.Collections.Generic.Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		var turnedMeshes = 0;

		foreach ( var name in models )
		{
			var file = new ModelFile( $"ui/{name}" );

			foreach ( var mesh in file.Meshes )
			{
				++meshes;

				var placed = mesh.Vertices.Select( vertex => NumericsVector3.Transform( new NumericsVector3( vertex.Position.X, vertex.Position.Y, vertex.Position.Z ), mesh.WorldTransform ) ).ToArray();

				if ( placed.Length == 0 )
					continue;

				var bounds = (placed.Min( p => p.X ), placed.Max( p => p.X ), placed.Min( p => p.Y ), placed.Max( p => p.Y ));
				var toScreen = UiMesh.ToScreen( new PixelRect( 120f, 80f, 300f, 200f ), bounds, 1024f, 768f );
				var meshDropped = 0;

				for ( var face = 0; face + 2 < mesh.Indices.Length; face += 3 )
				{
					++faces;

					var a = placed[mesh.Indices[face]];
					var b = placed[mesh.Indices[face + 1]];
					var c = placed[mesh.Indices[face + 2]];
					var turn = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

					if ( turn == 0f )
					{
						++edgeOn;
						continue;
					}

					var keeps = Material.Keeps( Clip( a, toScreen ), Clip( b, toScreen ), Clip( c, toScreen ) );

					Assert.AreEqual( turn < 0f, keeps, $"{name} {mesh.Name} face {face / 3}: kept exactly when clockwise in the file" );

					if ( !keeps )
						++meshDropped;
				}

				if ( meshDropped > 0 )
				{
					++turnedMeshes;
					byModel[name] = (byModel.TryGetValue( name, out var sum ) ? sum : 0) + meshDropped;
				}

				dropped += meshDropped;
			}
		}

		Assert.AreEqual( 278, models.Length, "models" );
		Assert.AreEqual( 1185, meshes, "meshes" );
		Assert.AreEqual( 6310, faces, "faces" );
		Assert.AreEqual( 0, edgeOn, "edge-on" );
		Assert.AreEqual( 422, dropped, "dropped" );
		Assert.AreEqual( 13, turnedMeshes, "meshes with a face dropped" );
		CollectionAssert.AreEquivalent( new[] { "s_X1.md2:392", "s_key.MD2:28", "warnlite.MD2:2" },
			byModel.Select( model => $"{model.Key}:{model.Value}" ).ToArray() );
	}

	/// <summary>A cell's four corners as the ground lays them: (x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1), ten units a cell.</summary>
	private static NumericsVector3[] Cell( int x, int y, Func<int, int, float> height )
		=> [.. new[] { (x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1) }.Select( c => new NumericsVector3( c.Item1 * 10f, c.Item2 * 10f, height( c.Item1, c.Item2 ) ) )];

	/// <summary>Every triangle kept from eight turns round the faces at three heights above them, and none from below.</summary>
	private static void AssertKeptFromAboveOnly( NumericsVector3[] corners, uint[] elements, string what )
	{
		var middle = corners.Aggregate( NumericsVector3.Zero, ( sum, c ) => sum + c ) / corners.Length;

		for ( var yaw = 0; yaw < 360; yaw += 45 )
		{
			foreach ( var pitch in new[] { 25f, 50f, 80f } )
			{
				var (y, p) = (yaw * MathF.PI / 180f, pitch * MathF.PI / 180f);
				var offset = new NumericsVector3( MathF.Cos( p ) * MathF.Cos( y ), MathF.Cos( p ) * MathF.Sin( y ), MathF.Sin( p ) ) * 120f;

				Assert.IsTrue( Kept( corners, elements, middle + offset, middle ).All( k => k ), $"{what} from yaw {yaw}, pitch {pitch}" );

				var below = offset with { Z = -offset.Z };
				Assert.IsTrue( Kept( corners, elements, middle + below, middle ).All( k => !k ), $"{what} from under it, yaw {yaw}, pitch {pitch}" );
			}
		}
	}

	/// <summary>Whether each triangle is kept by the culled pipeline, seen from <paramref name="eye"/> looking at <paramref name="at"/> through the park's lens.</summary>
	private static bool[] Kept( NumericsVector3[] corners, uint[] elements, NumericsVector3 eye, NumericsVector3 at )
	{
		var view = System.Numerics.Matrix4x4.CreateLookTo( eye, at - eye, NumericsVector3.UnitZ );
		var lens = Camera.Lens( Camera.DrawnAngle( 90f ), 4f / 3f ).Projection;
		var toClip = view * lens;

		return [.. Enumerable.Range( 0, elements.Length / 3 ).Select( t =>
			Material.Keeps( Clip( corners[elements[t * 3]], toClip ), Clip( corners[elements[(t * 3) + 1]], toClip ), Clip( corners[elements[(t * 3) + 2]], toClip ) ) )];
	}

	private static NumericsVector4 Clip( NumericsVector3 point, System.Numerics.Matrix4x4 toClip )
		=> NumericsVector4.Transform( new NumericsVector4( point, 1f ), toClip );
}
