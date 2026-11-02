using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which faces of a model are drawn: the original culls every model face itself, keeping those whose screen corners
/// wind clockwise (<c>Face_KeptByWinding</c>, <c>0x00581320</c>; <c>docs/exe/render-states.md</c>, "The software
/// back-face test"), and tests depth less or equal (<c>0x00573614</c>). So of a sheet authored two-sided, the same three
/// points twice wound opposite ways, only the face turned to the eye is drawn, and lit its own way (Q586).
/// </summary>
/// <remarks><b>Mutations:</b> the model's flags given <see cref="MaterialFlags.DisableCulling"/> back; the pipeline culling
/// nothing, or the front faces; the depth test less.</remarks>
[TestClass]
public class ModelFaceCullingTests
{
	/// <summary>A model's materials cull, whatever the caller asked: <c>DisableCulling</c> is taken off, the rest kept.</summary>
	[TestMethod]
	public void AModelsMaterialsCull()
	{
		Assert.AreEqual( MaterialFlags.None, LobbyModel.DrawFlags( MaterialFlags.None ) );
		Assert.AreEqual( MaterialFlags.DisableDepthWrite, LobbyModel.DrawFlags( MaterialFlags.DisableCulling | MaterialFlags.DisableDepthWrite ) );
	}

	/// <summary>A material that culls drops the faces wound against clockwise; one that asks for both faces drops none.</summary>
	[TestMethod]
	public void ThePipelineKeepsClockwiseFaces()
	{
		Assert.AreEqual( FaceCullMode.Back, Material.CullModeFor( MaterialFlags.None ) );
		Assert.AreEqual( FaceCullMode.Back, Material.CullModeFor( MaterialFlags.DisableDepthWrite ) );
		Assert.AreEqual( FaceCullMode.None, Material.CullModeFor( MaterialFlags.DisableCulling ) );
		Assert.AreEqual( FrontFace.Clockwise, Material.KeptWinding );
	}

	/// <summary>Less or equal, so a face at the depth of one drawn before it replaces it.</summary>
	[TestMethod]
	public void TheLaterOfTwoFacesAtOneDepthIsDrawn()
	{
		Assert.AreEqual( ComparisonKind.LessEqual, Material.DepthComparisonFor( MaterialFlags.None ) );
		Assert.AreEqual( ComparisonKind.LessEqual, Material.DepthComparisonFor( MaterialFlags.DisableCulling ) );
		Assert.AreEqual( ComparisonKind.LessEqual, Material.DepthComparisonFor( MaterialFlags.DisableDepthWrite ) );
	}

	/// <summary>
	/// <b>The descriptions the pipeline is built from</b> (<see cref="Material.DepthStateFor"/>,
	/// <see cref="Material.RasterizerStateFor"/>, what <c>SetupResources</c> hands the device): a model's material culls the
	/// back faces, clockwise kept, tests depth less or equal and writes it; a two-sided one culls none; one that does not
	/// write depth does not write it.
	/// </summary>
	[TestMethod]
	public void TheDescriptionsThePipelineIsBuiltFromCullAndCompareAsTheOriginals()
	{
		var model = Material.RasterizerStateFor( LobbyModel.DrawFlags( MaterialFlags.DisableCulling ) );

		Assert.AreEqual( FaceCullMode.Back, model.CullMode );
		Assert.AreEqual( FrontFace.Clockwise, model.FrontFace );
		Assert.AreEqual( PolygonFillMode.Solid, model.FillMode );
		Assert.AreEqual( FaceCullMode.None, Material.RasterizerStateFor( MaterialFlags.DisableCulling ).CullMode );
		Assert.AreEqual( FrontFace.Clockwise, Material.RasterizerStateFor( MaterialFlags.DisableCulling ).FrontFace );

		var depth = Material.DepthStateFor( MaterialFlags.None );

		Assert.AreEqual( ComparisonKind.LessEqual, depth.DepthComparison );
		Assert.IsTrue( depth.DepthTestEnabled );
		Assert.IsTrue( depth.DepthWriteEnabled );
		Assert.IsFalse( Material.DepthStateFor( MaterialFlags.DisableDepthWrite ).DepthWriteEnabled );
		Assert.IsFalse( Material.DepthStateFor( MaterialFlags.DisableDepthTest ).DepthTestEnabled );
		Assert.AreEqual( ComparisonKind.LessEqual, Material.DepthStateFor( MaterialFlags.DisableCulling ).DepthComparison );
	}

	/// <summary>
	/// <b>The pipeline description the device is handed carries those states</b> (<see cref="Material.StatesFor"/>, built by
	/// <c>CreateSharedPipeline</c>; Q736 (c)): a model's flags cull the back faces, test and write depth less or equal; a
	/// two-sided one culls none; additive and multiply blend as they are asked. Only the device's building of a pipeline from
	/// it is left to the game run.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>RasterizerState</c> or <c>DepthStencilState</c> left out of <c>StatesFor</c>.</remarks>
	[TestMethod]
	public void TheDescriptionHandedToTheDeviceCarriesTheMaterialsStates()
	{
		var model = Material.StatesFor( LobbyModel.DrawFlags( MaterialFlags.DisableCulling ) );

		Assert.AreEqual( FaceCullMode.Back, model.RasterizerState.CullMode );
		Assert.AreEqual( FrontFace.Clockwise, model.RasterizerState.FrontFace );
		Assert.AreEqual( ComparisonKind.LessEqual, model.DepthStencilState.DepthComparison );
		Assert.IsTrue( model.DepthStencilState.DepthWriteEnabled );
		Assert.AreEqual( PrimitiveTopology.TriangleList, model.PrimitiveTopology );
		Assert.AreEqual( BlendStateDescription.SingleAlphaBlend, model.BlendState );

		Assert.AreEqual( FaceCullMode.None, Material.StatesFor( MaterialFlags.DisableCulling ).RasterizerState.CullMode );
		Assert.IsFalse( Material.StatesFor( MaterialFlags.DisableDepthWrite ).DepthStencilState.DepthWriteEnabled );
		Assert.AreEqual( BlendStateDescription.SingleAdditiveBlend, Material.StatesFor( MaterialFlags.Additive ).BlendState );
		Assert.AreEqual( BlendFactor.SourceColor, Material.StatesFor( MaterialFlags.Multiply ).BlendState.AttachmentStates[0].DestinationColorFactor );
	}

	/// <summary>
	/// <b>Every place that builds a <see cref="Material"/> is accounted for</b> (Q736 (d); <c>render-states.md</c>, "Which draws
	/// the test covers"): the one that draws a model's meshes (<c>LobbyModel</c>, which every park and lobby model, the advisor
	/// and the object window's preview go through) takes its flags through <see cref="LobbyModel.DrawFlags"/>; every other
	/// is a listed draw (the table's two-sided ones, and the culled ones that ask for no flag). A new
	/// <c>new Material</c> anywhere else fails here until it is put on one list, which is the question of how it is culled.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>LobbyModel</c>'s <c>DrawFlags( materialFlags )</c> taken out; a new <c>new Material</c> in another file.</remarks>
	[TestMethod]
	public void EveryMaterialIsBuiltBySomethingTheTableAccountsFor()
	{
		var source = Path.Join( RepoFiles.Root(), "source", "OpenTPW" );

		if ( !Directory.Exists( source ) )
			Assert.Inconclusive( "no source tree beside the tests" );

		var sites = Directory.EnumerateFiles( source, "*.cs", SearchOption.AllDirectories )
			.Where( file => !file.Contains( $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}" ) && !file.Contains( $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}" ) )
			.Select( file => (Name: Path.GetFileName( file ), Count: File.ReadAllLines( file )
				.Count( line => !line.TrimStart().StartsWith( "///" ) && System.Text.RegularExpressions.Regex.IsMatch( line, @"\bnew Material\b" ) )) )
			.Where( site => site.Count > 0 )
			.OrderBy( site => site.Name, System.StringComparer.Ordinal )
			.Select( site => $"{site.Name}:{site.Count}" );

		// A model's: LobbyModel. Two-sided in the table: UiMesh, ScreenParticles, WorldParticles, ParkGuestSprites (two: the
		// people and their shadows), ParkPaths, ParkSea, ParkGround, ParkBuildMarkers, Sky. Asking for no flag, so culled as
		// a model is: Material.UI (the Default), Water, ParkRideHoarding, ParkFlags. Weather's are culled, its quads turned
		// to the camera.
		CollectionAssert.AreEqual(
			new[]
			{
				"LobbyModel.cs:1", "Material.UI.cs:1", "ParkBuildMarkers.cs:1", "ParkFlags.cs:1", "ParkGround.cs:1",
				"ParkGuestSprites.cs:2", "ParkPaths.cs:1", "ParkRideHoarding.cs:1", "ParkSea.cs:1", "ScreenParticles.cs:1",
				"Sky.cs:1", "UiMesh.cs:1", "Water.cs:1", "WeatherSprites.cs:1", "WorldParticles.cs:1"
			},
			sites.ToArray() );

		var lobby = File.ReadAllText( Path.Join( source, "World", "Lobby", "LobbyModel.cs" ) );

		StringAssert.Contains( lobby, "var flags = DrawFlags( materialFlags );" );
		StringAssert.Contains( lobby, "new Material<ObjectUniformBuffer>( \"content/shaders/test.shader\", flags )" );
	}

	/// <summary>A material that neither tests nor writes depth compares nothing.</summary>
	[TestMethod]
	public void NoDepthComparesNothing()
		=> Assert.AreEqual( ComparisonKind.Always, Material.DepthComparisonFor( MaterialFlags.DisableDepth ) );

	/// <summary>
	/// <b>The Sun God's plates are eight two-sided pairs, each underside first</b>: <c>incagod.MD2</c>'s <c>Body</c>,
	/// material <c>Inc_Saucer</c>, faces 124 to 139. Each pair stands on one set of three points wound opposite ways;
	/// the first face's normal (the table entry it names) points down the mesh's z, the second's up it, and the node's
	/// turn (120 degrees about -(1, 1, 1)) takes the mesh's z to the park's up. Drawn both, the first at a depth wins
	/// under a less test and the plates are dark; culled, the lit top is the one seen from above.
	/// </summary>
	[TestMethod]
	public void TheSunGodsPlatesAreTwoSidedUndersideFirst()
	{
		var data = GameData.Required();
		var model = new ModelFile( new MemoryStream( data.ReadAllBytes( "levels/jungle/rides/incagod/incagod.MD2" ) ) );
		var body = model.Meshes.Single( mesh => mesh.Name.TrimEnd( '\0', ' ' ) == "Body" );
		var saucer = body.Materials.Single( material => material.Name.StartsWith( "Inc_Saucer", System.StringComparison.OrdinalIgnoreCase ) );

		Assert.AreEqual( 124, (int)saucer.a, "the plates' faces start at 124" );
		Assert.AreEqual( 16, (int)saucer.b, "the two plates are sixteen faces" );

		for ( int face = saucer.a; face < saucer.a + saucer.b; face += 2 )
		{
			var under = Corners( body, face );
			var over = Corners( body, face + 1 );

			CollectionAssert.AreEquivalent( under, over, $"faces {face} and {face + 1} stand on the same three points" );
			Assert.IsTrue( Facing( under ).Dot( Facing( over ) ) < 0f, $"faces {face} and {face + 1} are wound opposite ways" );

			var first = body.FaceNormals[body.FaceNormalIndices[face]];
			var second = body.FaceNormals[body.FaceNormalIndices[face + 1]];

			Assert.IsTrue( first.Z < -0.8f, $"face {face}'s normal {first} points down the mesh's z" );
			Assert.IsTrue( second.Z > 0.8f, $"face {face + 1}'s normal {second} points up it" );
		}
	}

	private static Vector3[] Corners( ModelFile.Mesh mesh, int face )
		=> [.. Enumerable.Range( 0, 3 ).Select( corner => mesh.Vertices[mesh.Indices[(face * 3) + corner]].Position )];

	private static Vector3 Facing( Vector3[] corners )
		=> Vector3.Cross( corners[1] - corners[0], corners[2] - corners[0] );
}
