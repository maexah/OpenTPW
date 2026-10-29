using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// First person's fog: linear in the original's clip z, 0.70718 (eye depth - 0.1), from the detail file's band, in the
/// theme's fog colour, over every world draw but the sky (docs/exe/park-engine.md, "The park view's colours").
/// </summary>
[TestClass]
public class FirstPersonFogTests
{
	private Level? _level;

	[TestInitialize]
	public void Remember()
	{
		Log ??= new();
		_level = Level.Current;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkCamcorderCameraMode.Forget();
		Camera.SetCameraMode<CameraMode>();
		Level.Current = _level;
	}

	/// <summary>The four bands <c>DetailFile_Apply</c> sets, and the startup default for any other value.</summary>
	/// <remarks><b>Mutation:</b> any band's start or end changed, or another value given a shipped band, fails here.</remarks>
	[TestMethod]
	public void EachViewDistanceHasTheOriginalsBand()
	{
		Assert.AreEqual( new FirstPersonFog( 50f, 60f ), FirstPersonFog.ForViewDistance( 0 ) );
		Assert.AreEqual( new FirstPersonFog( 100f, 110f ), FirstPersonFog.ForViewDistance( 1 ) );
		Assert.AreEqual( new FirstPersonFog( 135f, 150f ), FirstPersonFog.ForViewDistance( 2 ) );
		Assert.AreEqual( new FirstPersonFog( 230f, 250f ), FirstPersonFog.ForViewDistance( 3 ) );
		Assert.AreEqual( new FirstPersonFog( 100f, 130f ), FirstPersonFog.ForViewDistance( 4 ) );
		Assert.AreEqual( new FirstPersonFog( 100f, 130f ), FirstPersonFog.ForViewDistance( -1 ) );
	}

	/// <summary>low.sam, med.sam and high.sam carry 0, 1 and 2.</summary>
	/// <remarks><b>Mutation:</b> the key misspelt (every file medium's) or the value ignored fails here.</remarks>
	[TestMethod]
	public void EachDetailFileGivesItsOwnBand()
	{
		FileSystem = GameData.Required();
		Log = new();

		Assert.AreEqual( new FirstPersonFog( 50f, 60f ), FirstPersonFog.Read( "low.sam" ) );
		Assert.AreEqual( new FirstPersonFog( 100f, 110f ), FirstPersonFog.Read( "med.sam" ) );
		Assert.AreEqual( new FirstPersonFog( 135f, 150f ), FirstPersonFog.Read( "high.sam" ) );
	}

	/// <summary>
	/// The fraction along the path measured in the original with the band held at 20 to 40 (<c>q485/orig/b-fogfit.txt</c>):
	/// 0.40 at eye depth 40 and 0.75 at 50, where measured in the eye depth itself it would be whole at both.
	/// </summary>
	/// <remarks><b>Mutation:</b> the band taken in eye depth (no 0.70718), or the near left out of a band near the eye, fails here.</remarks>
	[TestMethod]
	public void TheFogIsMeasuredInTheOriginalsClipDepth()
	{
		var band = new FirstPersonFog( 20f, 40f );

		Assert.AreEqual( 0.40f, band.At( 40f ), 0.02f );
		Assert.AreEqual( 0.75f, band.At( 50f ), 0.02f );
		Assert.AreEqual( 0f, band.At( 28f ) );
		Assert.AreEqual( 1f, band.At( 57f ) );

		// The near: 0.70718 x (0.1 + 0.2) is 0.21 into a band from 0 to 1, where without it 0.28.
		Assert.AreEqual( 0.2122f, new FirstPersonFog( 0f, 1f ).At( 0.4f ), 0.002f );
	}

	/// <summary>Medium's 100 to 110 is eye depth 141.5 to 155.6, which is what the shaders get.</summary>
	/// <remarks><b>Mutation:</b> the band handed over unconverted, the ends swapped or the switch left off fails here.</remarks>
	[TestMethod]
	public void TheShadersGetTheBandInEyeDepth()
	{
		var uniform = FirstPersonFog.ForViewDistance( 1 ).Uniform;

		Assert.AreEqual( 141.51f, uniform.X, 0.01f );
		Assert.AreEqual( 155.65f, uniform.Y, 0.01f );
		Assert.AreEqual( 1f, uniform.Z );
	}

	/// <summary>Only a park in first person is fogged by it: not its orbit view, not the lobby, not a park with no band.</summary>
	/// <remarks><b>Mutation:</b> any one of the three conditions dropped fails here.</remarks>
	[TestMethod]
	public void OnlyFirstPersonInAParkIsFogged()
	{
		var fog = FirstPersonFog.ForViewDistance( 1 );

		Assert.AreEqual( fog.Uniform, Level.LinearFogFor( Level.Scene.Park, true, fog ) );
		Assert.AreEqual( default, Level.LinearFogFor( Level.Scene.Park, false, fog ) );
		Assert.AreEqual( default, Level.LinearFogFor( Level.Scene.Lobby, true, fog ) );
		Assert.AreEqual( default, Level.LinearFogFor( Level.Scene.Park, true, null ) );
	}

	/// <summary>Going into first person turns it on for every world draw, and leaving turns it off.</summary>
	/// <remarks><b>Mutation:</b> <see cref="Level.LinearFog"/> not following the camcorder fails here.</remarks>
	[TestMethod]
	public void FirstPersonTurnsTheFogOnAndLeavingTurnsItOff()
	{
		var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
		park.FirstPersonFog = FirstPersonFog.ForViewDistance( 1 );
		Level.Current = park;

		Assert.AreEqual( 0f, Level.LinearFog.Z );

		ParkCamcorderCameraMode.Enter();
		Assert.AreEqual( 141.51f, Level.LinearFog.X, 0.01f );

		ParkCamcorderCameraMode.Leave();
		Assert.AreEqual( 0f, Level.LinearFog.Z );
	}

	/// <summary>
	/// The world's models, its people with their shadows, and its particles each hand their shader first person's band and
	/// the theme's fog colour.
	/// </summary>
	/// <remarks><b>Mutation:</b> the band or the colour left out of any of the three draws' uniforms fails here.</remarks>
	[TestMethod]
	public void EveryWorldDrawHandsItsShaderTheFog()
	{
		var colour = Level.FogColour;
		try
		{
			Level.FogColour = new Vector3( 0.2f, 0.4f, 0.6f );
			var park = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
			typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( park, Level.Scene.Park );
			park.FirstPersonFog = FirstPersonFog.ForViewDistance( 1 );
			Level.Current = park;
			ParkCamcorderCameraMode.Enter();

			var model = (ModelEntity)RuntimeHelpers.GetUninitializedObject( typeof( ModelEntity ) );
			model.Level = park;
			var scene = model.SceneUniforms();
			Assert.AreEqual( 141.51f, scene.g_vLinearFog.X, 0.01f, "models" );
			Assert.AreEqual( 0.6f, scene.g_vFogColour.Z, 1e-6f, "models" );

			var people = ParkGuestSprites.DrawUniforms();
			Assert.AreEqual( 155.65f, people.g_vLinearFog.Y, 0.01f, "people" );
			Assert.AreEqual( 0.4f, people.g_vFogColour.Y, 1e-6f, "people" );

			var particles = WorldParticles.DrawUniforms();
			Assert.AreEqual( 1f, particles.g_vLinearFog.Z, "particles" );
			Assert.AreEqual( 0.2f, particles.g_vFogColour.X, 1e-6f, "particles" );
		}
		finally
		{
			Level.FogColour = colour;
		}
	}

	/// <summary>
	/// The park's model shader, the people's and the particles' each mix the fog per vertex and read it from the
	/// block's own field; the sky's does not.
	/// </summary>
	/// <remarks><b>Mutation:</b> the mix taken out of any of the three fails here.</remarks>
	[TestMethod]
	public void EveryWorldShaderButTheSkysMixesIt()
	{
		foreach ( var name in new[] { "test", "people", "guests" } )
		{
			var text = File.ReadAllText( Path.Combine( ContentRoot(), "shaders", $"{name}.shader" ) );
			StringAssert.Contains( text, "vec4 g_vLinearFog;", name );
			StringAssert.Matches( text, new System.Text.RegularExpressions.Regex( @"mix\(\s*fragColor\.(xyz|rgb),\s*g_oUbo\.g_vFogColour(\.rgb)?,\s*vFog\s*\)" ), name );
		}

		StringAssert.DoesNotMatch( File.ReadAllText( Path.Combine( ContentRoot(), "shaders", "sky.shader" ) ),
			new System.Text.RegularExpressions.Regex( "g_vLinearFog" ) );
	}

	/// <summary>
	/// <b>The fog is worked out per vertex from the planar eye depth</b> (<see cref="FirstPersonFog.At"/> is this formula's
	/// twin, tested alone): <c>clamp( ( -view.z - start ) / ( end - start ), 0, 1 )</c> in each of the three world shaders.
	/// </summary>
	/// <remarks><b>Mutations:</b> the sign dropped (<c>view.z</c>, never fogs), the radial <c>length( view.xyz )</c>, or the
	/// denominator <c>g_vLinearFog.y</c> alone, each fails here.</remarks>
	[TestMethod]
	public void EveryWorldShaderFogsByPlanarEyeDepth()
	{
		var line = new System.Text.RegularExpressions.Regex(
			@"vFog\s*=\s*clamp\(\s*\(\s*-\s*(view|vs_out\.vViewPosition)\.z\s*-\s*g_oUbo\.g_vLinearFog\.x\s*\)\s*/\s*"
			+ @"\(\s*g_oUbo\.g_vLinearFog\.y\s*-\s*g_oUbo\.g_vLinearFog\.x\s*\)\s*,\s*0\.0\s*,\s*1\.0\s*\)\s*;" );

		foreach ( var name in new[] { "test", "people", "guests" } )
			StringAssert.Matches( File.ReadAllText( Path.Combine( ContentRoot(), "shaders", $"{name}.shader" ) ), line, name );
	}

	private static string ContentRoot() => Path.Combine( AppContext.BaseDirectory, "content" );
}
