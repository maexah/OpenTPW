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
		if ( TryGetCachedShader( path, out var existingShader ) )
			return existingShader!;

		return new Shader( path );
	}
}
