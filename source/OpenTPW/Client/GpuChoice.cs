namespace OpenTPW;

/// <summary>What kind of card Vulkan says a device is (<c>VkPhysicalDeviceType</c>, in its own order).</summary>
internal enum GpuKind
{
	Other = 0,
	Integrated = 1,
	Discrete = 2,
	Virtual = 3,
	Cpu = 4
}

/// <summary>One Vulkan device as the loader lists it: where it stands in the list and who made it.</summary>
internal readonly record struct GpuInfo( int Index, string Name, uint VendorId, uint DeviceId, GpuKind Kind );

/// <summary>
/// Which graphics card the game draws with, decided from what the Vulkan loader lists. The original lists up to four
/// DirectDraw drivers and its Options row switches between the primary one and a secondary one (0x00563574,
/// 0x004a361c; <c>docs/exe/ui.md</c>, "Video card"); that row is Primary and Secondary here too (Q983), over the Vulkan
/// devices: <b>Primary is the card <see cref="Pick"/> prefers, Secondary the next one after it</b>. Choosing by the
/// <c>gpu</c> line of opentpw.cfg, or by name, is OpenTPW's own, a deviation from the original.
///
/// <para>
/// NeoVeldrid takes the first device the loader lists and offers no way to name another (<c>VkGraphicsDevice.CreatePhysicalDevice</c>:
/// "Just use the first one"; <c>VulkanDeviceOptions</c> holds only extensions). On a PC with a built-in GPU and a
/// graphics card the first one can be the built-in one (Windows 11, Q980: an AMD built-in GPU ahead of a GTX 1650). So
/// the choice is made here, and <see cref="GpuSelector"/> asks the loader to put the choice first.
/// </para>
/// <para>
/// This is the pure part: the pick, its words and the loader settings that can put a device first. The probe that reads
/// the list and sets the environment is <see cref="GpuSelector"/>.
/// </para>
/// </summary>
internal static class GpuChoice
{
	/// <summary>The setting that leaves the choice to the game: a discrete card if there is one.</summary>
	public const string Auto = "auto";

	/// <summary>The setting's prefix for a place in the list the log prints: <c>#1</c> is the second device.</summary>
	public const string IndexPrefix = "#";

	/// <summary>
	/// The device to draw with. <paramref name="setting"/> is opentpw.cfg's <c>gpu</c> line: empty or <c>auto</c> for the
	/// best kind (a discrete card, then a built-in one, then anything else, a software renderer last; the loader's own
	/// order breaks a tie), <c>#N</c> for the Nth device in the list, or any other text for the first device whose name
	/// holds it, ignoring case. A setting nothing matches falls back to the automatic pick, the reason says so and
	/// <c>Met</c> is false (the caller warns). <paramref name="secondary"/> is the Options row's Secondary: the automatic
	/// pick takes the card after the best one (the best one, where there is no other); a setting that is met wins over it.
	/// </summary>
	public static (int Index, string Why, bool Met) Pick( IReadOnlyList<GpuInfo> devices, string? setting, bool secondary = false )
	{
		if ( devices.Count == 0 )
			throw new ArgumentException( "no devices to choose from", nameof( devices ) );

		var ask = setting?.Trim() ?? string.Empty;
		var fallback = string.Empty;

		if ( ask.Length > 0 && !ask.Equals( Auto, StringComparison.OrdinalIgnoreCase ) )
		{
			if ( ask.StartsWith( IndexPrefix, StringComparison.Ordinal ) && int.TryParse( ask[1..], out var place ) )
			{
				if ( place >= 0 && place < devices.Count )
					return (place, $"opentpw.cfg's \"gpu {ask}\"", true);

				fallback = $"opentpw.cfg's \"gpu {ask}\" names no device (there are {devices.Count}), so ";
			}
			else
			{
				foreach ( var device in devices )
				{
					if ( device.Name.Contains( ask, StringComparison.OrdinalIgnoreCase ) )
						return (device.Index, $"opentpw.cfg's \"gpu {ask}\"", true);
				}

				fallback = $"opentpw.cfg's \"gpu {ask}\" matches no device name, so ";
			}
		}

		var ranked = Ranked( devices );

		if ( secondary && ranked.Count > 1 )
			return (ranked[1].Index, fallback + "the Options row says Secondary: the card after the preferred one", fallback.Length == 0);

		var best = ranked[0];
		var kind = best.Kind == GpuKind.Discrete ? "a discrete card is preferred" : $"no discrete card, so the first {KindName( best.Kind )} one";
		return (best.Index, fallback + kind, fallback.Length == 0);
	}

	/// <summary>
	/// The devices best first (<see cref="Rank"/>, the loader's order within a kind): the first is Primary, the second
	/// Secondary, as the original's list has them (0x004a361c).
	/// </summary>
	public static IReadOnlyList<GpuInfo> Ranked( IReadOnlyList<GpuInfo> devices )
		=> devices.OrderBy( device => Rank( device.Kind ) ).ToList();

	/// <summary>
	/// The Video Card row's click (0x004a361c): Secondary goes back to Primary always, Primary goes to Secondary only where
	/// the loader lists more than one card (<paramref name="count"/>).
	/// </summary>
	public static bool Clicked( bool secondary, int count ) => !secondary && count > 1;

	/// <summary>
	/// Whether the row reads Secondary (0x004a3650), truthfully: the saved choice where it can be had, otherwise the card
	/// drawn on (<paramref name="drawsOnPrimary"/>), which is the case with one card listed and with a card named by the
	/// <c>gpu</c> line (<paramref name="exact"/>).
	/// </summary>
	public static bool Shows( bool secondary, int count, bool exact, bool drawsOnPrimary ) => count > 1 && !exact ? secondary : !drawsOnPrimary;

	/// <summary>Whether <paramref name="setting"/> names a card (a name or <c>#N</c>) rather than leaving it to the game.</summary>
	public static bool Names( string? setting )
		=> !string.IsNullOrWhiteSpace( setting ) && !setting.Trim().Equals( Auto, StringComparison.OrdinalIgnoreCase );

	/// <summary>Lower is better: a discrete card, a built-in one, then the rest, a software renderer last.</summary>
	private static int Rank( GpuKind kind ) => kind switch
	{
		GpuKind.Discrete => 0,
		GpuKind.Integrated => 1,
		GpuKind.Virtual => 2,
		GpuKind.Other => 3,
		_ => 4
	};

	public static string KindName( GpuKind kind ) => kind switch
	{
		GpuKind.Discrete => "discrete",
		GpuKind.Integrated => "integrated",
		GpuKind.Virtual => "virtual",
		GpuKind.Cpu => "software",
		_ => "other"
	};

	/// <summary>The maker a PCI vendor id names; the id in hex where it is not one the game knows.</summary>
	public static string VendorName( uint vendorId ) => vendorId switch
	{
		0x1002 => "AMD",
		0x10DE => "NVIDIA",
		0x8086 => "Intel",
		0x106B => "Apple",
		0x13B5 => "Arm",
		0x5143 => "Qualcomm",
		0x10005 => "Mesa software renderer",
		_ => $"vendor 0x{vendorId:x4}"
	};

	/// <summary>One line of the log's list: <c>[1] NVIDIA GeForce GTX 1650 (NVIDIA, discrete)</c>.</summary>
	public static string Describe( GpuInfo device )
		=> $"[{device.Index}] {device.Name} ({VendorName( device.VendorId )}, {KindName( device.Kind )})";

	/// <summary>
	/// The loader settings that can put <paramref name="device"/> first, best first. Each is tried and checked by listing
	/// the devices again (<see cref="GpuSelector"/>), so a setting the machine's loader ignores costs a log line, not a wrong card.
	/// <list type="bullet">
	/// <item>Mesa's device-select layer: <c>MESA_VK_DEVICE_SELECT=vendor:device</c> puts that device first among Mesa's
	/// own and any other driver's (Linux, and the Mesa software renderer).</item>
	/// <item>The Vulkan loader's driver filter: <c>VK_LOADER_DRIVERS_SELECT</c> keeps only the drivers whose manifest
	/// file names match, so the card's driver is the one left. The names are the vendors' usual ones and not read from
	/// any machine here but this Linux one's; where they miss, the check says so.</item>
	/// </list>
	/// <b>Two cards of one maker cannot be told apart by either lever on Windows</b>: the loader's filter works per manifest
	/// file, so an AMD built-in GPU and a Radeon, or an Intel built-in GPU and an Arc, share one and both are kept in the
	/// loader's own order. Choosing the second of them ends in the warning and the loader's first device (Q904).
	/// </summary>
	public static IReadOnlyList<(string Name, string Value)> Levers( GpuInfo device )
	{
		var levers = new List<(string, string)> { ("MESA_VK_DEVICE_SELECT", $"{device.VendorId:x4}:{device.DeviceId:x4}") };

		var drivers = device.VendorId switch
		{
			0x10DE => "*nvidia_icd*,*nv-vk*,*nv_vk*",
			0x1002 => "*radeon_icd*,*amd_icd*,*amd-vulkan*,*amdvlk*",
			0x8086 => "*intel_icd*,*igvk*",
			0x10005 => "*lvp_icd*",
			_ => null
		};

		if ( drivers is not null )
			levers.Add( ("VK_LOADER_DRIVERS_SELECT", drivers) );

		return levers;
	}
}
