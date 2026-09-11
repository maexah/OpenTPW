namespace OpenTPW.UI;

/// <summary>
/// One window of the interface: a tree of controls opened and closed together, as each of the
/// original's layout streams is (0x0065fd0e).
///
/// A modal window goes through 0x0047ed80 instead, which puts a control over the whole screen under
/// it wearing LOLIGHT.MD2 - a quad of half-transparent black - so everything behind is dimmed and
/// nothing behind can be clicked until the window closes.
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

	/// <summary>The box typing goes to while this is the front window.</summary>
	internal UiEdit? Focus { get; set; }

	/// <summary>Called every frame the window is open.</summary>
	protected internal virtual void Update() { }

	/// <summary>Enter was pressed with this in front.</summary>
	protected internal virtual void Accept() { }

	/// <summary>Escape was pressed with this in front.</summary>
	protected internal virtual void Cancel() { }

	/// <summary>The dimmed backdrop a modal window sits on.</summary>
	protected static UiControl Backdrop() => new()
	{
		Rect = VirtualScreen.Whole,
		Mesh = UiMesh.Get( "LOLIGHT" ),
		FillsWindow = true
	};
}
