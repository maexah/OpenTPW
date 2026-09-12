namespace OpenTPW;

/// <summary>
/// Finds the engine's own files - the shaders it compiles, and the font the loading screen writes with.
///
/// <para>
/// These are OpenTPW's, not the original game's: they live in content\ in the repository, and the build copies
/// them next to the binary keeping that same layout (see OpenTPW.csproj). They are found relative to the
/// binary rather than to the working directory, so the game runs the same however it was started - from the
/// folder the game's files are in, from a published folder, or with "dotnet run" from source\OpenTPW.
/// </para>
/// <para>
/// <see cref="GameDir"/> does the same job for the original game's files, which come from wherever the player
/// installed them instead.
/// </para>
/// </summary>
internal static class ContentDir
{
	/// <summary>
	/// Joins the folder this build is in to <paramref name="path"/>.
	/// </summary>
	/// <param name="path">A path as it is spelled in the repository - "content/shaders/3d.shader".</param>
	public static string GetPath( string path )
	{
		var normalizedPath = path.Replace( '\\', Path.DirectorySeparatorChar )
			.Replace( '/', Path.DirectorySeparatorChar );

		// A path that has already been resolved is handed straight back, so resolving one twice costs nothing
		// and changes nothing - a shader is resolved in Shader.GetOrCreate and again in the constructor.
		if ( Path.IsPathRooted( normalizedPath ) )
			return normalizedPath;

		return Path.Join( AppContext.BaseDirectory, normalizedPath );
	}
}
