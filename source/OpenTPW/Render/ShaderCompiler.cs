using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace OpenTPW;

internal struct ShaderInfo
{
	public Veldrid.Shader VertexShader { get; set; }
	public Veldrid.Shader FragmentShader { get; set; }
	public SpirvReflection Reflection { get; set; }

	public readonly Veldrid.Shader[] ShaderProgram => [VertexShader, FragmentShader];
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
			GraphicsBackend.Metal => CrossCompileTarget.MSL,
			GraphicsBackend.OpenGLES => CrossCompileTarget.ESSL,
			_ => throw new NotImplementedException( $"Unknown cross-compile target" )
		};
	}

	private static byte[] GetBytes( string code )
	{
		return Device.ResourceFactory.BackendType switch
		{
			GraphicsBackend.Direct3D11 or GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES or GraphicsBackend.Vulkan => Encoding.ASCII.GetBytes( code ),
			GraphicsBackend.Metal => Encoding.UTF8.GetBytes( code ),
			_ => throw new SpirvCompilationException( "Unknown target" ),
		};
	}

	public static ShaderInfo CompileShader( string path )
	{
		var target = GetCrossCompileTarget();

		var preprocessedShader = ShaderPreprocessor.PreprocessShader( path );
		var vertexSource = preprocessedShader.VertexShader;
		var fragmentSource = preprocessedShader.FragmentShader;

		var vertexSourceBytes = GetBytes( vertexSource );
		var fragmentSourceBytes = GetBytes( fragmentSource );

		// Cross-compiled even on Vulkan, where the translation itself is thrown away: the reflection naming
		// every resource binding - what Material builds its layouts from, and looks its bound resources up by
		// name with - comes out of this call and nowhere else. It is given the GLSL source rather than
		// compiled SPIR-V on purpose. SPIR-V built with the default options carries no debug names, and the
		// reflection read back out of it has empty names for every texture and sampler, which Material throws
		// on at the first draw.
		var compilationResult = SpirvCompilation.CompileVertexFragment( vertexSourceBytes, fragmentSourceBytes, target );

		// The shader objects themselves are left to Veldrid, which is handed the same Vulkan GLSL the
		// reflection above was read out of. It knows what each backend will accept - which form, which text
		// encoding, and that a Metal entry point has to be called "main0" because "main" is a reserved word
		// in MSL - and none of that can be tested here, so none of it is worth restating here.
		//
		// What this replaces was Vulkan-only in three separate ways: it fed the cross-compiled result back
		// into a GLSL compiler, which for Metal means handing MSL to a GLSL parser; it then gave a Metal
		// device SPIR-V, which wants metallib or MSL text; and it named the entry point "main".
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
