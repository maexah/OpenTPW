namespace OpenTPW.UI;

/// <summary>
/// The map's three words: what its overlay shows (<c>docs/exe/hud.md</c>, "The map's overlay"). They live in the map's
/// static (<c>0x00f86db8</c> <c>+0x18</c>, <c>+0x1c</c>, <c>+0x20</c>), which the original's constructor (<c>0x005f0690</c>)
/// sets once a process to 1, 1 and 1, so they outlive the screen like its zoom: a fresh game opens on the rides layer, the
/// handyman and happiness, and each opening after that on what the last one was left at.
/// </summary>
internal static class ParkMapOverlay
{
	/// <summary>The layer bits of <see cref="Layers"/>, each one switch's (<c>Map_Proc</c> <c>0x005f1130</c>, message <c>0x100</c>).</summary>
	internal const int Rides = 1, Shops = 2, Sideshows = 4, Miscellaneous = 8, Tracks = 0x10, Visitors = 0x20;

	/// <summary>The staff picker's values: <see cref="Staff"/> 0 is none.</summary>
	internal const int Handyman = 1, Mechanic = 2, Guard = 3, Entertainer = 4;

	/// <summary>The metric picker's values: <see cref="Metric"/> 0 is none.</summary>
	internal const int Happiness = 1, Excitement = 2, Satisfaction = 3;

	/// <summary><c>+0x18</c>: the layer bits, each switch an XOR of its own.</summary>
	internal static int Layers { get; set; } = 1;

	/// <summary><c>+0x1c</c>: the staff picker, the chosen member's value or 0 with none down.</summary>
	internal static int Staff { get; set; } = 1;

	/// <summary><c>+0x20</c>: the metric picker, the chosen metric's value or 0 with none down.</summary>
	internal static int Metric { get; set; } = 1;

	/// <summary>The three words as the console's <c>map</c> and the log print them.</summary>
	internal static string Census => $"layers 0x{Layers:x2} staff {Staff} metric {Metric}";
}
