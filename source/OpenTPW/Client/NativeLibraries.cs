using Silk.NET.Core.Loader;

namespace OpenTPW;

/// <summary>
/// Teaches Silk.NET's loader where this build keeps the native libraries it ships with.
///
/// <para>
/// A build puts the natives its packages carry in runtimes\&lt;rid&gt;\native, and the runtime finds them
/// there by itself for anything declared with [DllImport] - which is how ImGui.NET's cimgui is found, and
/// why that one always worked. Silk.NET does not use DllImport: it opens its libraries itself, and the only
/// places it looks are the bare name and the folder this build sits in. Neither is where the natives are.
/// </para>
/// <para>
/// A bare name reaches whatever the machine already has, so the two failed in different ways and only one
/// of them was visible. libSDL2-2.0.so quietly resolved to the system's copy - the game ran, on an SDL it
/// did not ship - while libspirv-cross.so, which no system carries, failed outright in the first shader
/// compiled (see <see cref="ShaderCompiler"/>) and took the game down with it before a frame was drawn.
/// </para>
/// <para>
/// The directories come from the host rather than being built here, and that is the whole point. A package
/// does not have to ship its natives under the exact identifier the machine reports: SDL and MoltenVK ship
/// under plain <c>osx</c> while shaderc and SPIRV-Cross ship under <c>osx-arm64</c> and <c>osx-x64</c>, and
/// only the runtime identifier graph says that a Mac calling itself osx-arm64 should read both. The host has
/// already walked that graph and left the answer in NATIVE_DLL_SEARCH_DIRECTORIES, which is the same list
/// [DllImport] resolves against. Building one path out of
/// <c>RuntimeInformation.RuntimeIdentifier</c> instead would find every native on Linux and Windows and then
/// miss SDL and MoltenVK on macOS, where missing MoltenVK means no Vulkan at all.
/// </para>
/// <para>
/// Registered in <see cref="Program"/> before anything can ask for a library, and deliberately silent - it
/// runs before there is a log to write to. Where the host offers no directories - a self-contained build, say,
/// which lays its natives out flat beside the binary - this adds nothing and Silk's own search of that folder
/// is already the right answer.
/// </para>
/// </summary>
internal static class NativeLibraries
{
	/// <summary>
	/// Where the host says this application's native libraries are, in the order it means them to be tried.
	/// Read once: it cannot change while the process runs.
	/// </summary>
	private static readonly string[] SearchDirectories =
		((string?)AppContext.GetData( "NATIVE_DLL_SEARCH_DIRECTORIES" ) ?? string.Empty)
			.Split( Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries );

	/// <summary>
	/// Called once, before anything can open a native library. Nothing is reported if the resolver turns out
	/// not to be one that keeps a list: the game still starts, and a library that then cannot be found says
	/// so itself, which is the same message this prevents rather than a quieter one.
	/// </summary>
	public static void Register()
	{
		if ( PathResolver.Default is DefaultPathResolver resolver )
			resolver.Resolvers.Insert( 0, Shipped );
	}

	/// <summary>
	/// Where this build keeps <paramref name="name"/>. Handed back whether or not it is there: Silk tries
	/// each path in turn and moves on, so a directory that does not hold this one costs a failed open.
	/// </summary>
	private static IEnumerable<string> Shipped( string name )
	{
		foreach ( var directory in SearchDirectories )
			yield return Path.Combine( directory, name );
	}
}
