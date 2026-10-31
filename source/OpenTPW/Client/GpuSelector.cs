using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace OpenTPW;

/// <summary>
/// Reads the Vulkan devices the loader lists and, before the game's own device is made, asks the loader to put the one
/// <see cref="GpuChoice"/> picked first: NeoVeldrid takes the first and cannot be told another (see
/// <see cref="GpuChoice"/>). <b>OpenTPW's own, a deviation from the original.</b> Called once from
/// <c>Renderer.CreateGraphicsDevice</c>; it never throws, and a start that cannot choose goes on with the loader's first device.
/// What it learned stays for the Options screen's Video Card row (<see cref="Count"/>, <see cref="Exact"/>, <see cref="DrawsOnPrimary"/>).
///
/// <para>
/// The loader reads its environment each time an instance is made, so a setting is tried by setting it and listing
/// the devices again through a throwaway instance. Only a setting that put the choice first is kept; the others are put back.
/// The game's <c>GPU:</c> line (<see cref="LaunchReport"/>) then names the device NeoVeldrid really took.
/// </para>
/// </summary>
internal static class GpuSelector
{
	/// <summary>How many Vulkan devices the loader listed: 0 where nothing was listed (macOS, no device), so the Options row offers no Secondary.</summary>
	public static int Count { get; private set; }

	/// <summary>Whether opentpw.cfg's <c>gpu</c> line named a card and was met: it then wins over the Options row.</summary>
	public static bool Exact { get; private set; }

	/// <summary>Whether the card drawn on is the one <see cref="GpuChoice.Pick"/> prefers (Primary) rather than another (Secondary).</summary>
	public static bool DrawsOnPrimary { get; private set; } = true;

	private static bool _firstIsPrimary = true;

	/// <summary>The loader variables <see cref="Choose"/> left set; <see cref="Withdraw"/> takes them back.</summary>
	private static readonly List<string> Left = new();

	/// <summary>Counts a variable as left set by the choice, so <see cref="Withdraw"/> takes it back.</summary>
	internal static void Track( string name ) => Left.Add( name );

	/// <summary>Whether <see cref="Choose"/> set a loader variable that is still set.</summary>
	public static bool IsSet => Left.Count > 0;

	/// <summary>The loader's own steering, which a person set and this leaves alone.</summary>
	private static readonly string[] PersonalSettings = { "MESA_VK_DEVICE_SELECT", "VK_LOADER_DRIVERS_SELECT", "VK_LOADER_DRIVERS_DISABLE", "VK_DRIVER_FILES", "VK_ICD_FILENAMES" };

	/// <summary>
	/// Lists the devices, picks one by <paramref name="setting"/> (opentpw.cfg's <c>gpu</c> line) and <paramref name="secondary"/>
	/// (Config.tcf's video card, the Options row; <see cref="GpuChoice.Pick"/>) and sets the environment so that it is first.
	/// Logs the list, the pick and the result. A pick takes effect at the start, so a change in the Options row is drawn at the next one.
	/// </summary>
	public static void Choose( string? setting, bool secondary = false )
	{
		Count = 0;
		Exact = false;
		DrawsOnPrimary = _firstIsPrimary = true;
		Left.Clear();

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

			Count = devices.Count;
			Log.Info( $"Graphics card: {devices.Count} Vulkan device{(devices.Count == 1 ? "" : "s")}: {string.Join( "; ", devices.Select( GpuChoice.Describe ) )}" );

			var (index, why, met) = GpuChoice.Pick( devices, setting, secondary );
			var chosen = devices[index];
			var primary = GpuChoice.Ranked( devices )[0].Index;

			Exact = met && GpuChoice.Names( setting );
			DrawsOnPrimary = index == primary;

			if ( !met )
				Log.Warning( $"Graphics card: {why}" );

			if ( index == 0 )
			{
				Log.Info( $"Graphics card: {GpuChoice.Describe( chosen )} is chosen ({why}) and the loader lists it first, so nothing is set" );
				return;
			}

			// The card drawn on is the loader's first unless a lever below moves the chosen one up.
			DrawsOnPrimary = _firstIsPrimary = Is( devices, devices[0], primary );

			if ( Same( chosen, devices[0] ) )
			{
				Log.Info( $"Graphics card: {GpuChoice.Describe( chosen )} is chosen ({why}), but the loader's first device is the same make, model and name, which no setting can tell apart; nothing is set" );
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

				if ( List() is { Count: > 0 } now && Same( now[0], chosen ) )
				{
					Track( name );
					DrawsOnPrimary = index == primary;
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

	/// <summary>Whether two listed devices are the same make, model and name: nothing the loader is given tells two such cards apart.</summary>
	private static bool Same( GpuInfo a, GpuInfo b ) => a.VendorId == b.VendorId && a.DeviceId == b.DeviceId && a.Name == b.Name;

	/// <summary>Whether <paramref name="drawn"/> is the primary card (the one at <paramref name="primary"/>), or indistinguishable from it.</summary>
	private static bool Is( IReadOnlyList<GpuInfo> devices, GpuInfo drawn, int primary ) => drawn.Index == primary || Same( drawn, devices[primary] );

	/// <summary>
	/// Takes back the loader variables <see cref="Choose"/> set, after the device the choice put first could not be made
	/// (it cannot present to the window, or open): the start then goes on with the loader's own first device. A variable a
	/// person set was never touched. The Options row then says what is drawn on (<see cref="DrawsOnPrimary"/>).
	/// </summary>
	public static void Withdraw( Exception why )
	{
		Log.Warning( $"Graphics card: the chosen card could not be used ({why.GetType().Name}: {why.Message}); {string.Join( ", ", Left )} taken back, and the device is made as the loader allows" );

		foreach ( var name in Left )
			Set( name, null );

		Left.Clear();
		DrawsOnPrimary = _firstIsPrimary;
		Exact = false;
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
	internal static void Set( string name, string? value )
	{
		if ( OperatingSystem.IsWindows() )
		{
			Environment.SetEnvironmentVariable( name, value );
			return;
		}

		_ = value is null ? unsetenv( name ) : setenv( name, value, 1 );
	}

	/// <summary>A variable of the process's own environment as the loader sees it, which <c>Environment.GetEnvironmentVariable</c> does not show for one <see cref="Set"/> on Linux and macOS.</summary>
	internal static string? Get( string name )
		=> OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable( name ) : Marshal.PtrToStringUTF8( getenv( name ) );

	[DllImport( "libc", EntryPoint = "getenv" )]
	private static extern nint getenv( string name );

	[DllImport( "libc", EntryPoint = "setenv" )]
	private static extern int setenv( string name, string value, int overwrite );

	[DllImport( "libc", EntryPoint = "unsetenv" )]
	private static extern int unsetenv( string name );
}
