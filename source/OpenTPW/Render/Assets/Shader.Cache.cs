namespace OpenTPW;

partial class Shader
{
	private static bool TryGetCachedShader( string path, out Shader? shader )
	{
		shader = null;

		if ( string.IsNullOrEmpty( path ) )
			return false;

		var existingShader = Asset.All.OfType<Shader>().FirstOrDefault( s => s.Path == path );
		if ( existingShader != null )
		{
			shader = existingShader;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Returns the already-compiled Shader for this path if one exists (so multiple
	/// Materials using the same shader source share one compile and one file watcher),
	/// otherwise compiles and caches a new one.
	/// </summary>
	internal static Shader GetOrCreate( string path )
	{
		// Resolved before the lookup rather than after it. TryGetCachedShader compares whole path strings, so
		// a shader asked for by the spelling the code uses would never match the resolved path the shader
		// itself keeps, and every material would get a compile and a file watcher of its own.
		path = ContentDir.GetPath( path );

		if ( TryGetCachedShader( path, out var existingShader ) )
			return existingShader!;

		return new Shader( path );
	}
}
