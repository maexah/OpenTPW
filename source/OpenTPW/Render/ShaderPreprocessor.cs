namespace OpenTPW;

internal record struct PreprocessedShader( string VertexShader, string FragmentShader );

internal static partial class ShaderPreprocessor
{
	public static PreprocessedShader PreprocessShader( string filePath )
	{
		var fileContents = File.ReadAllText( filePath );

		// The parser is given the folder the shader came from, so that an include names its neighbour rather
		// than the directory the game happened to be started from - see ShaderSegmentParser.Parse.
		var segmentParser = new ShaderSegmentParser( fileContents, Path.GetDirectoryName( filePath ) ?? "" );
		segmentParser.Parse( out var vertexStage1, out var fragmentStage1 );

		var result = new PreprocessedShader( vertexStage1, fragmentStage1 );
		return result;
	}
}
