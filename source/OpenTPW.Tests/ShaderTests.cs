using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// Splitting a .shader into its vertex and fragment halves, on a real shader out of content\shaders. The
/// project file copies that folder next to the test binary, so this reads it from beside the assembly rather
/// than from wherever the run happened to be started.
/// </summary>
[TestClass]
public class ShaderTests
{
	[TestMethod]
	public void PreprocessTest()
	{
		Log = new();

		var path = Path.Combine( AppContext.BaseDirectory, "content", "shaders", "test.shader" );

		Assert.IsTrue( File.Exists( path ), $"the content folder was not copied beside the tests: {path}" );

		var result = ShaderPreprocessor.PreprocessShader( path );
		Assert.IsTrue( result.VertexShader.Length > 0 && result.FragmentShader.Length > 0 );
	}

	/// <summary>
	/// The park's lighting fields sit where std140 puts them in content/shaders/test.shader's block: a
	/// vec3 starts on 16 bytes and a float after it fills its fourth slot. If the struct and the block
	/// drift apart, the park is lit from whatever bytes land there, and nothing else notices.
	/// </summary>
	[TestMethod]
	public void TheParkLightFieldsSitWhereTheShaderReadsThem()
	{
		static int At( string field ) => (int)System.Runtime.InteropServices.Marshal.OffsetOf<ObjectUniformBuffer>( field );

		Assert.AreEqual( 264, At( nameof( ObjectUniformBuffer.g_flWorldNormals ) ) );
		Assert.AreEqual( 272, At( nameof( ObjectUniformBuffer.g_vAmbientColour ) ) );
		Assert.AreEqual( 284, At( nameof( ObjectUniformBuffer.g_flParkLight ) ) );
		Assert.AreEqual( 288, At( nameof( ObjectUniformBuffer.g_vLightTravels ) ) );
		Assert.AreEqual( 300, At( nameof( ObjectUniformBuffer.g_flFullLight ) ) );
		Assert.AreEqual( 304, At( nameof( ObjectUniformBuffer.g_vLinearFog ) ) );
		Assert.AreEqual( 320, At( nameof( ObjectUniformBuffer.g_vVertexColour ) ) );
		Assert.AreEqual( 336, System.Runtime.InteropServices.Marshal.SizeOf<ObjectUniformBuffer>() );
	}

	/// <summary>
	/// Both of test.shader's stages declare the pulse's colour after first person's fog, where the struct puts it, and the
	/// vertex stage puts it in place of the park light when its <c>w</c> is set (<see cref="ModelEntity.VertexColour"/>).
	/// </summary>
	[TestMethod]
	public void TheModelShaderReadsThePulsesColourInPlaceOfTheLight()
	{
		var text = File.ReadAllText( Path.Combine( AppContext.BaseDirectory, "content", "shaders", "test.shader" ) );
		const string block = "vec4 g_vLinearFog;\n        vec4 g_vVertexColour;\n    } g_oUbo;";

		Assert.AreEqual( 2, text.Split( block ).Length - 1, "in both stages" );
		StringAssert.Contains( text, "if (g_oUbo.g_vVertexColour.w > 0.5)\n            {\n                vParkLight = g_oUbo.g_vVertexColour.rgb;" );
	}

	/// <summary>
	/// test.shader binds <see cref="Material.TextureSlots"/> textures, one per material a mesh can draw with, and picks
	/// each by its index; an index past them draws flat magenta.
	/// </summary>
	[TestMethod]
	public void TheModelShaderBindsEveryTextureSlot()
	{
		var text = File.ReadAllText( Path.Combine( AppContext.BaseDirectory, "content", "shaders", "test.shader" ) );

		for ( var slot = 0; slot < Material.TextureSlots; ++slot )
		{
			StringAssert.Contains( text, $"binding = {slot} ) uniform texture2D Color{slot};" );
			StringAssert.Contains( text, $"if ( texIndex == {slot} ) vTextureSample = SampleColor(Color{slot}, finalTexCoords);" );
		}

		StringAssert.Contains( text, $"binding = {Material.TextureSlots} ) uniform sampler s_Color;" );
		Assert.IsFalse( text.Contains( $"Color{Material.TextureSlots};" ), "no slot past them" );
	}

	/// <summary>
	/// A vertex carrying <see cref="LobbyModel.ClampFlag"/> samples with the clamped twin <c>Material.Set</c> binds after
	/// the slots' sampler; every other vertex with the sampler its caller bound.
	/// </summary>
	/// <remarks><b>Mutations:</b> the flag's value apart from LobbyModel's; the samplers swapped.</remarks>
	[TestMethod]
	public void TheModelShaderSamplesAClampedVertexWithTheClampedSampler()
	{
		var text = File.ReadAllText( Path.Combine( AppContext.BaseDirectory, "content", "shaders", "test.shader" ) );

		StringAssert.Contains( text, $"binding = {Material.TextureSlots + 1} ) uniform sampler s_ColorClamp;" );
		StringAssert.Contains( text, $"const uint FLAG_CLAMP = 0x{LobbyModel.ClampFlag:x};" );
		StringAssert.Contains( text, "if ((outMatFlags & FLAG_CLAMP) != 0)\n            return texture(sampler2D(colour, s_ColorClamp), uv);\n\n        return texture(sampler2D(colour, s_Color), uv);" );
	}
}
