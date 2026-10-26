using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// How the advisor is lit, as the original lights him: per vertex, the ambient plus a white light of one for each advisor
/// record loaded, times max(0, -(d.n)), d his record light's direction taken through the inverse of the node's chain, n
/// the model file's own normal, each channel clamped and rounded to a byte (docs/exe/ui.md, "How he is lit"). Measured in
/// the running original: ambient (0.333, 0.333, 0.408) and strength 2 in a Lost Kingdom park, 0.267 and 1 in the lobby.
/// The expected bytes were worked out apart from the code (q657's vfit.py arithmetic).
/// </summary>
[TestClass]
public class AdvisorLightTests
{
	/// <summary>Lost Kingdom's <c>AmbientLightLevel</c>, 85 85 104.</summary>
	private static readonly OpenTPW.Vector3 Park = new( 85f / 255f, 85f / 255f, 104f / 255f );

	private static void Lit( OpenTPW.Vector3 normal, Matrix4x4 model, float strength, float red, float blue, string what )
	{
		var lit = AdvisorModel.VertexLight( normal, model, Park, strength );
		Assert.AreEqual( (red, red, blue), (lit.X, lit.Y, lit.Z), what );
	}

	[TestMethod]
	public void A_face_a_quarter_turned_to_the_light_takes_twice_its_facing_over_the_ambient()
	{
		// The file's normal (-0.968, 0, -0.25) is (-0.968, -0.25, 0) in this model's world: facing 0.253.
		var normal = new OpenTPW.Vector3( -0.968245837f, 0f, -0.25f );

		Lit( normal, Matrix4x4.Identity, 2f, 214f, 233f, "two record lights, a park" );
		Lit( normal, Matrix4x4.Identity, 1f, 149f, 168f, "one record light, the lobby" );
	}

	[TestMethod]
	public void The_light_comes_through_his_root_scale_nearly_straight_into_the_screen()
	{
		// A face turned square across the screen: lit only by the light's across part. Divided by the root's lengths it
		// is 0.003 of the whole and the face 87; the record's own (26.667, 800, 26.667) would make it 102.
		Lit( new OpenTPW.Vector3( -1f, 0f, 0f ), Matrix4x4.Identity, 2f, 87f, 106f, "the across face" );
	}

	[TestMethod]
	public void A_face_turned_from_the_light_is_the_ambient_and_one_facing_it_is_clamped()
	{
		Lit( new OpenTPW.Vector3( 0f, 0f, 1f ), Matrix4x4.Identity, 2f, 85f, 104f, "the back of him" );
		Lit( new OpenTPW.Vector3( 0f, 0f, -1f ), Matrix4x4.Identity, 2f, 255f, 255f, "the front of him" );
	}

	[TestMethod]
	public void The_light_is_taken_into_the_node_by_the_inverse_of_its_matrix()
	{
		// Turned a quarter about the up axis, the across face is turned to the light; unturned it would be 87.
		Lit( new OpenTPW.Vector3( -1f, 0f, 0f ), Matrix4x4.CreateRotationZ( MathF.PI / 2f ), 2f, 255f, 255f, "turned" );

		// Squashed across to a hundredth, the inverse stretches the light's across part a hundredfold: 230. A transpose
		// would shrink it (85), and the node ignored would leave 87.
		Lit( new OpenTPW.Vector3( -1f, 0f, 0f ), Matrix4x4.CreateScale( 0.01f, 1f, 1f ), 2f, 230f, 249f, "squashed" );
	}

	[TestMethod]
	public void A_park_lights_him_with_its_ambient_and_the_lobby_with_its_own()
	{
		var park = new ParkLight( Park, new OpenTPW.Vector3( 0f, 0f, -1f ), Lit: false );

		var (inPark, two) = AdvisorModel.LightFor( park, 2, lit: true );
		Assert.AreEqual( (Park, AdvisorModel.Travels, false, 2f), (inPark.Ambient, inPark.Travels, inPark.Lit, two),
			"the park's ambient and quality, his own direction, two records" );

		var (inLobby, one) = AdvisorModel.LightFor( null, 1, lit: true );
		Assert.AreEqual( (new OpenTPW.Vector3( 68f / 255f ), true, 1f), (inLobby.Ambient, inLobby.Lit, one), "0x444444 and one record" );
	}

	[TestMethod]
	public void Every_advisor_is_lit_by_its_files_own_normal_for_each_vertex()
	{
		var data = GameData.Required();

		foreach ( var path in new[] { "global/advisor/Advisor.md2", "levels/jungle/advisor/Advisor_JUN.md2", "levels/fantasy/advisor/Advisor_FAN.md2",
			"levels/hallow/advisor/Advisor_HAL.md2", "levels/space/advisor/Advisor_SPA.md2" } )
		{
			var model = new ModelFile( new MemoryStream( data.ReadAllBytes( path ) ) );

			foreach ( var mesh in model.Meshes.Where( mesh => mesh.Vertices.Length > 0 ) )
			{
				Assert.IsTrue( mesh.FaceNormals.Length >= mesh.Vertices.Length, $"{path} {mesh.Name}: its table covers its {mesh.Vertices.Length} vertices" );

				for ( int i = 0; i < mesh.Vertices.Length; ++i )
					Assert.AreEqual( mesh.FaceNormals[i], AdvisorModel.FileNormal( mesh, i ), $"{path} {mesh.Name} vertex {i}" );
			}
		}

		// The global fly's right hand, where the loader's own normals stand furthest from the file's (a median 33 degrees).
		var fly = new ModelFile( new MemoryStream( data.ReadAllBytes( "global/advisor/Advisor.md2" ) ) );
		var hand = fly.Meshes.First( mesh => mesh.Name.TrimEnd( '\0' ).Trim() == "Right hand" );
		var apart = Enumerable.Range( 0, hand.Vertices.Length ).Count( i => OpenTPW.Vector3.Dot( hand.Normals[i], AdvisorModel.FileNormal( hand, i ) ) < 0.9f );
		Assert.IsTrue( apart > hand.Vertices.Length / 2, $"the file's normals, not the loader's: {apart} of {hand.Vertices.Length} more than 26 degrees apart" );
	}
}
