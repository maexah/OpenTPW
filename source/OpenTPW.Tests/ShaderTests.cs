using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// Splitting a .shader into its vertex and fragment halves, on a real shader out of content\shaders. The
/// project file copies that folder next to the test binary, so this reads it from beside the assembly rather
/// than from wherever the run happened to be started - which used to be "E:\OpenTPW".
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
}
