using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// Teaches the Vulkan bindings how to find dlopen on a modern Linux.
///
/// <para>
/// vk.dll - the Vk package Veldrid draws its Vulkan bindings from - declares its loader as
/// [DllImport( "libdl" )] on Vulkan.Libdl's dlopen, dlsym, dlclose and dlerror. glibc merged libdl into libc
/// in 2.34 and distributions now ship only the versioned libdl.so.2, so none of the names the runtime looks
/// for - libdl.so, liblibdl.so, libdl, liblibdl, in the framework folder and beside the game - exists any
/// more. The first touch of Vulkan.VulkanNative then dies in its type initializer with "Unable to load shared
/// library 'libdl'", and the first touch is GraphicsDevice.CreateVulkan in <see cref="Renderer"/>, so the game
/// never reaches a frame. Until now the way round it was to make a libdl.so symlink beside the build by hand.
/// </para>
/// <para>
/// A resolver belongs to the assembly that declares the import rather than to the one installing it, so this
/// is registered against vk.dll itself; registered against ours it would never be consulted. Naming a type in
/// vk.dll does not run VulkanNative's type initializer, so asking for the assembly here cannot trip the very
/// failure this prevents - and VkResult is a plain enum with no initializer of its own.
/// </para>
/// <para>
/// Nothing here is wanted on Windows, where the same bindings use kernel32, nor on macOS, where dl lives in
/// libSystem and the bare name resolves.
/// </para>
/// </summary>
internal static class NativeLibraries
{
	/// <summary>
	/// Called once, before anything can touch the Vulkan bindings. Registering twice for one assembly throws,
	/// so there is deliberately only the one call site, in <see cref="Program"/>.
	/// </summary>
	public static void Register()
	{
		if ( !OperatingSystem.IsLinux() )
			return;

		NativeLibrary.SetDllImportResolver( typeof( Vulkan.VkResult ).Assembly, Resolve );
	}

	/// <summary>
	/// The main program's handle is handed back rather than a libc named outright: its symbol lookup reaches
	/// everything already loaded into the process, and the runtime itself calls dlopen, so the four functions
	/// are always there. Naming a library instead would mean choosing between a glibc new enough to carry dl
	/// inside libc.so.6 and one still shipping libdl.so.2 - and on an older glibc, libc.so.6 would load
	/// happily and then fail on the missing entry point instead.
	/// </summary>
	private static IntPtr Resolve( string libraryName, Assembly assembly, DllImportSearchPath? searchPath )
	{
		// Zero means "carry on as usual", which is what everything else vk.dll asks for wants.
		return libraryName == "libdl" ? NativeLibrary.GetMainProgramHandle() : IntPtr.Zero;
	}
}
