namespace OpenTPW.UI;

/// <summary>
/// One window of the interface: a tree of controls opened and closed together, as each of the
/// original's layout streams is (0x0065fd0e).
///
/// A modal window goes through 0x0047ed80 instead, which puts a control over the whole screen under
/// it wearing LOLIGHT.MD2 - a quad of half-transparent black - so everything behind is dimmed and
/// nothing behind can be clicked until the window closes.
///
/// Engine and content: a window, and what the window manager does with it, is the interface's engine and the
/// same in a park; what a window holds and what its controls do belongs to its screen.
/// </summary>
internal abstract class UiWindow
{
	protected UiWindow( WindowStack stack )
	{
		Stack = stack;
	}

	/// <summary>The stack it opens in, and closes itself from.</summary>
	protected WindowStack Stack { get; }

	public UiControl Root { get; protected set; } = null!;

	/// <summary>Whether everything behind it is dimmed and shut out - see the class remarks.</summary>
	public bool Modal { get; protected init; }

	/// <summary>
	/// Whether the game is paused while it is open. The original's message box (0x0047f020) and options
	/// screen (OptionsScreen_Open, 0x004a3a30) ask for a pause as they open, through 0x004092a0; the new
	/// player dialog does not.
	///
	/// <para>
	/// <b>The game menu is more awkward than this used to say.</b> GameMenu_Open (0x0048c830) asks only on
	/// its park path: it branches on its scene argument at 0x0048c83a, and the non-zero the lobby passes
	/// (0x005e4207) builds the lobby's menu and returns without reaching the pause at all. So the same
	/// widget asks in one scene and not in the other.
	/// </para>
	/// <para>
	/// What a pause does is the scene's either way: the helper acts only while 0x00786ba4 - Game+0x3c, the
	/// pause-permission gate - is exactly 1, which the state machine sets for a park and clears for the
	/// lobby, and the lobby holds its advisor instead. See <see cref="GameClock"/> for the whole of it and
	/// <see cref="Advisor.Paused"/> for the lobby's side.
	/// </para>
	/// </summary>
	public bool Pauses { get; protected init; }

	/// <summary>
	/// Whether it is put away for the time being: still open, but neither drawn nor pointed at. The
	/// options screen puts the front end's window away like this while it is up (message 6, 0x004a3a30).
	/// </summary>
	public bool Hidden { get; set; }

	/// <summary>The box typing goes to while this is the front window.</summary>
	internal UiEdit? Focus { get; set; }

	/// <summary>Called every frame the window is open.</summary>
	protected internal virtual void Update() { }

	/// <summary>Enter was pressed with this in front.</summary>
	protected internal virtual void Accept() { }

	/// <summary>Escape was pressed with this in front.</summary>
	protected internal virtual void Cancel() { }

	/// <summary>The window came into view - opened, or brought back after being put away (message 0x11).</summary>
	protected internal virtual void Shown() { }

	/// <summary>The window was closed.</summary>
	protected internal virtual void Closed() { }

	/// <summary>The dimmed backdrop a modal window sits on.</summary>
	protected static UiControl Backdrop() => new()
	{
		Rect = VirtualScreen.Whole,
		Mesh = UiMesh.Get( "LOLIGHT" ),
		FillsWindow = true
	};
}
