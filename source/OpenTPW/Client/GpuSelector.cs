using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace OpenTPW;

/// <summary>
/// Reads the Vulkan devices the loader lists and, before the game's own device is made, asks the loader to put the one
/// <see cref="GpuChoice"/> picked first: NeoVeldrid takes the first and cannot be told another (see
/// <see cref="GpuChoice"/>). <b>OpenTPW's own, a deviation from the original.</b> Called once from
/// <c>Renderer.CreateGraphicsDevice</c>; it never throws, and a start that cannot choose goes on with the loader's first device.
///
/// <para>
/// The loader reads its environment each time an instance is made, so a setting is tried by setting it and listing
/// the devices again through a throwaway instance. Only a setting that put the choice first is kept; the others are put back.
/// The game's <c>GPU:</c> line (<see cref="LaunchReport"/>) then names the device NeoVeldrid really took.
/// </para>
/// </summary>
internal static class GpuSelector
{
	/// <summary>The loader's own steering, which a person set and this leaves alone.</summary>
	private static readonly string[] PersonalSettings = { "MESA_VK_DEVICE_SELECT", "VK_LOADER_DRIVERS_SELECT", "VK_LOADER_DRIVERS_DISABLE", "VK_DRIVER_FILES", "VK_ICD_FILENAMES" };

	/// <summary>
	/// Lists the devices, picks one by <paramref name="setting"/> (opentpw.cfg's <c>gpu</c> line, <see cref="GpuChoice.Pick"/>)
	/// and sets the environment so that it is first. Logs the list, the pick and the result.
	/// </summary>
	public static void Choose( string? setting )
	{
		try
		{
			// MoltenVK lists the Mac's one GPU or its choice already; there is nothing to put first, and no Mac to test on.
			if ( OperatingSystem.IsMacOS() )
			{
				Log.Info( "Graphics card: not chosen on macOS; MoltenVK's own device is used" );
				return;
			}

			var devices = List();

			if ( devices.Count == 0 )
			{
				Log.Warning( "Graphics card: the Vulkan loader lists no device to choose from; the device is made as the loader allows" );
				return;
			}

			Log.Info( $"Graphics card: {devices.Count} Vulkan device{(devices.Count == 1 ? "" : "s")}: {string.Join( "; ", devices.Select( GpuChoice.Describe ) )}" );

			var (index, why, met) = GpuChoice.Pick( devices, setting );
			var chosen = devices[index];

			if ( !met )
				Log.Warning( $"Graphics card: {why}" );

			if ( index == 0 )
			{
				Log.Info( $"Graphics card: {GpuChoice.Describe( chosen )} is chosen ({why}) and the loader lists it first, so nothing is set" );
				return;
			}

			if ( PersonalSettings.FirstOrDefault( name => !string.IsNullOrEmpty( Environment.GetEnvironmentVariable( name ) ) ) is { } theirs )
			{
				Log.Warning( $"Graphics card: {GpuChoice.Describe( chosen )} is chosen ({why}), but {theirs} is already set in the environment, which is left alone; {GpuChoice.Describe( devices[0] )} will be used unless that setting says otherwise" );
				return;
			}

			foreach ( var (name, value) in GpuChoice.Levers( chosen ) )
			{
				Set( name, value );

				if ( List() is { Count: > 0 } now && now[0].VendorId == chosen.VendorId && now[0].DeviceId == chosen.DeviceId && now[0].Name == chosen.Name )
				{
					Log.Info( $"Graphics card: {GpuChoice.Describe( chosen )} is chosen ({why}), and {name}={value} puts it first" );
					return;
				}

				Log.Info( $"Graphics card: {name}={value} did not put {GpuChoice.Describe( chosen )} first on this machine; taken back" );
				Set( name, null );
			}

			Log.Warning( $"Graphics card: {GpuChoice.Describe( chosen )} is chosen ({why}), but the loader could not be asked to put it first; NeoVeldrid takes the first, {GpuChoice.Describe( devices[0] )}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Graphics card: not chosen, the device is made as the loader allows - {e.GetType().Name}: {e.Message}" );
		}
	}

	/// <summary>Every device the loader lists now, in its order, read through an instance made and thrown away.</summary>
	private static unsafe List<GpuInfo> List()
	{
		var found = new List<GpuInfo>();
		using var vk = Vk.GetApi();

		var app = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version10 };
		var info = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &app };

		if ( vk.CreateInstance( &info, null, out var instance ) != Result.Success )
			return found;

		try
		{
			uint count = 0;
			vk.EnumeratePhysicalDevices( instance, &count, null );

			var devices = new PhysicalDevice[count];

			fixed ( PhysicalDevice* ptr = devices )
				vk.EnumeratePhysicalDevices( instance, &count, ptr );

			for ( var i = 0; i < count; ++i )
			{
				vk.GetPhysicalDeviceProperties( devices[i], out var properties );
				var name = Marshal.PtrToStringUTF8( (nint)properties.DeviceName ) ?? string.Empty;

				found.Add( new GpuInfo( i, name, properties.VendorID, properties.DeviceID, (GpuKind)(int)properties.DeviceType ) );
			}
		}
		finally
		{
			vk.DestroyInstance( instance, null );
		}

		return found;
	}

	/// <summary>
	/// Sets (or, with a null value, removes) a variable in the process's own environment, where the loader looks.
	/// .NET's <c>SetEnvironmentVariable</c> reaches it on Windows only; on the others it keeps a copy of its own.
	/// </summary>
	private static void Set( string name, string? value )
	{
		if ( OperatingSystem.IsWindows() )
		{
			Environment.SetEnvironmentVariable( name, value );
			return;
		}

		_ = value is null ? unsetenv( name ) : setenv( name, value, 1 );
	}

	[DllImport( "libc", EntryPoint = "setenv" )]
	private static extern int setenv( string name, string value, int overwrite );

	[DllImport( "libc", EntryPoint = "unsetenv" )]
	private static extern int unsetenv( string name );
}
