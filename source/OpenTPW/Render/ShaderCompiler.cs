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

	private static byte[] GetBytes( string code )
	{
		return Device.ResourceFactory.BackendType switch
		{
			GraphicsBackend.Direct3D11 or GraphicsBackend.OpenGL or GraphicsBackend.OpenGLES or GraphicsBackend.Vulkan => Encoding.ASCII.GetBytes( code ),
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
