using System.Runtime.InteropServices;
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
/// Putting runtimes\&lt;rid&gt;\native at the front of that list answers both: the libraries this build ships
/// are the ones it opens, and the machine's own are never reached. Registered in <see cref="Program"/>
/// before anything can ask for a library, and deliberately silent - it runs before there is a log to write to.
/// </para>
/// </summary>
internal static class NativeLibraries
{
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
	/// each path in turn and moves on, so a platform whose natives live elsewhere costs one failed open.
	/// </summary>
	private static IEnumerable<string> Shipped( string name )
	{
		yield return Path.Combine( AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", name );
	}
}
