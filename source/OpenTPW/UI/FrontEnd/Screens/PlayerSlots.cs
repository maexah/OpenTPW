namespace OpenTPW.UI;

/// <summary>
/// The screen the lobby opens on while nobody is playing: four purple player buttons down the right,
/// Quit Game hanging off the right edge below them, and the Theme Park World logo bottom left.
///
/// <para>
/// FrontEnd_ShowPlayerSlots (0x004a6580) loads it from the layout stream at 0x00753c68. Each slot is a
/// control wearing b_login.md2 with its label inside - "Create New Player" (UITEXT 421), or the name
/// of whoever is in it - in font 5 on the purple skin, a delete button (b_dellog) and the Instant
/// Action joystick (e_joy), those two only shown for a slot in use (FrontEnd_FillPlayerSlots,
/// 0x004a62b0). Quit Game is the same mesh with "Quit Game" (UITEXT 8).
/// </para>
/// <para>
/// The labels are orange, (236, 162, 4) from 0x007540c0. The pointer turns a slot's label yellow,
/// (255, 255, 0) from 0x007540cc, and Quit Game's red (0x004a6000, 0x004a61d0).
/// </para>
/// <para>
/// There is nothing saved yet, so no slot is ever in use when this opens, and the delete button has
/// nothing to do. The original asks before deleting, in a message box (0x0047f020).
/// </para>
/// </summary>
internal sealed class PlayerSlots : UiWindow
{
	private const int ButtonFont = 5;

	private static readonly UiColour Label = new( 236, 162, 4 );
	private static readonly UiColour Highlighted = new( 255, 255, 0 );
	private static readonly UiColour QuitHighlighted = new( 255, 0, 0 );

	/// <summary>Each slot's control id, top to bottom.</summary>
	private static readonly int[] SlotIds = [0x7a14, 0x7a19, 0x7a1a, 0x7a1b];

	/// <summary>How far apart the slots are, on the virtual screen.</summary>
	private const int SlotSpacing = 199;

	public PlayerSlots( FrontEnd frontEnd ) : base( frontEnd )
	{
		Root = new UiControl { Id = 0x7a13, Rect = VirtualScreen.Whole };

		for ( int slot = 0; slot < Players.SlotCount; ++slot )
		{
			var top = 46 + (slot * SlotSpacing);
			var player = frontEnd.Players[slot];

			var button = Root.Add( new UiControl
			{
				Id = SlotIds[slot],
				Rect = new UiRect( 884, top, 2048, top + 147 ),
				Mesh = UiMesh.Get( "b_login" )
			} );

			var label = button.Add( new UiControl
			{
				Id = 0x7a15,
				Rect = new UiRect( 941, top + 29, 1716, top + 118 ),
				Text = player?.Name ?? Localization.Get( UIStrings.CreateNewPlayer ),
				Font = ButtonFont,
				TextColour = Label,
				TextShadow = true
			} );

			label.Add( new UiButton
			{
				Id = 0x7a16,
				Rect = new UiRect( 1919, top + 22, 2021, top + 125 ),
				Mesh = UiMesh.Get( "b_dellog" ),
				Visible = player != null
			} );

			button.Add( new UiControl
			{
				Id = 0x7a17,
				Rect = new UiRect( 1762, top + 22, 1865, top + 125 ),
				Mesh = UiMesh.Get( "e_joy" ),
				Visible = player is { InstantAction: true }
			} );

			var chosen = slot;
			button.Entered = () => label.TextColour = Highlighted;
			button.Exited = () => label.TextColour = Label;
			button.Clicked = () => frontEnd.SlotClicked( chosen );
		}

		Root.Add( new UiControl
		{
			Id = 0x7a18,
			Rect = new UiRect( 32, 1321, 442, 1497 ),
			Mesh = UiMesh.Get( "tpwlogo" )
		} );

		var quit = Root.Add( new UiControl
		{
			Id = 0x7a1c,
			Rect = new UiRect( 1264, 895, 2428, 1042 ),
			Mesh = UiMesh.Get( "b_login" ),
			TextRect = new UiRect( 1339, 924, 2039, 1013 ),
			Text = Localization.Get( UIStrings.QuitGame ),
			Font = ButtonFont,
			TextColour = Label,
			TextShadow = true
		} );

		quit.Entered = () => quit.TextColour = QuitHighlighted;
		quit.Exited = () => quit.TextColour = Label;
		quit.Clicked = frontEnd.AskToQuit;
	}
}
