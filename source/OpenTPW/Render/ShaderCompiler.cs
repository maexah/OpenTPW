using System.Text;
using NeoVeldrid;
using NeoVeldrid.SPIRV;

namespace OpenTPW;

internal struct ShaderInfo
{
	public NeoVeldrid.Shader VertexShader { get; set; }
	public NeoVeldrid.Shader FragmentShader { get; set; }
	public SpirvReflection Reflection { get; set; }

	public readonly NeoVeldrid.Shader[] ShaderProgram => [VertexShader, FragmentShader];
}

internal static class ShaderCompiler
{
	private static CrossCompileTarget GetCrossCompileTarget()
	{
		return Device.ResourceFactory.BackendType switch
		{
			GraphicsBackend.Direct3D11 => CrossCompileTarget.HLSL,
			GraphicsBackend.OpenGL => CrossCompileTarget.GLSL,
			GraphicsBackend.Vulkan => CrossCompileTarget.GLSL,
			GraphicsBackend.OpenGLES => CrossCompileTarget.ESSL,
			_ => throw new NotImplementedException( $"Unknown cross-compile target" )
		};
	}

	public static ShaderInfo CompileShader( string path )
	{
		var target = GetCrossCompileTarget();

		var preprocessedShader = ShaderPreprocessor.PreprocessShader( path );
		var vertexSource = preprocessedShader.VertexShader;
		var fragmentSource = preprocessedShader.FragmentShader;

		// ASCII, which is what every backend NeoVeldrid offers takes. This chose between ASCII and UTF-8
		// while Metal was a backend, because an MSL source has to be UTF-8; with Metal gone there is one
		// answer, so it is written here rather than behind a switch that can no longer branch.
		var vertexSourceBytes = Encoding.ASCII.GetBytes( vertexSource );
		var fragmentSourceBytes = Encoding.ASCII.GetBytes( fragmentSource );

		// Cross-compiled even on Vulkan, where the translation itself is thrown away: the reflection naming
		// every resource binding - what Material builds its layouts from, and looks its bound resources up by
		// name with - comes out of this call and nowhere else. It is given the GLSL source rather than
		// compiled SPIR-V on purpose. SPIR-V built with the default options carries no debug names, and the
		// reflection read back out of it has empty names for every texture and sampler, which Material throws
		// on at the first draw.
		//
		// What it calls a uniform block matters to every caller of Material.Set. A shader declares both names
		// - "uniform ObjectUniformBuffer { ... } g_oUbo;" is a block type and a block instance - and this
		// reflection reports the INSTANCE name, g_oUbo. Veldrid's reported the type name, which is why the
		// call sites that bind it changed spelling with the move. A name that does not match is not caught
		// until the first draw of that material, where Material throws with the reflection's own name.
		var compilationResult = SpirvCompilation.CompileVertexFragment( vertexSourceBytes, fragmentSourceBytes, target );

		// The shader objects themselves are left to NeoVeldrid, which is handed the same Vulkan GLSL the
		// reflection above was read out of. It knows what each backend will accept - which form and which
		// text encoding - and none of that can be tested here, so none of it is worth restating here.
		//
		// What this replaces fed the cross-compiled result back into a GLSL compiler and then handed the
		// device SPIR-V, which is a thing only Vulkan wanted. Handing the source across once and letting
		// the backend decide is what makes one path work on all of them.
		var shaders = Device.ResourceFactory.CreateFromSpirv(
			new ShaderDescription( ShaderStages.Vertex, vertexSourceBytes, "main" ),
			new ShaderDescription( ShaderStages.Fragment, fragmentSourceBytes, "main" ) );

		return new ShaderInfo()
		{
			VertexShader = shaders[0],
			FragmentShader = shaders[1],
			Reflection = compilationResult.Reflection
		};
	}
}
