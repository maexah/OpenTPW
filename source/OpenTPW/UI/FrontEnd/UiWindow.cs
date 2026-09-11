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
	protected UiWindow( FrontEnd frontEnd )
	{
		FrontEnd = frontEnd;
	}

	protected FrontEnd FrontEnd { get; }

	public UiControl Root { get; protected set; } = null!;

	/// <summary>Whether everything behind it is dimmed and shut out - see the class remarks.</summary>
	public bool Modal { get; protected init; }

	/// <summary>
	/// Whether the game is paused while it is open. The original's game menu (GameMenu_Open, 0x0048c830),
	/// message box (0x0047f020) and options screen (OptionsScreen_Open, 0x004a3a30) each ask for a pause as they
	/// open, through 0x004092a0, and the new player dialog does not. What a pause does is the scene's: that
	/// helper only acts while a park is running (0x00786ba4), and the lobby holds its advisor instead - see
	/// <see cref="LobbyAdvisor.Paused"/>.
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
