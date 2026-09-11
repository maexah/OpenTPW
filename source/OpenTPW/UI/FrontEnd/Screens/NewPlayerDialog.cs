namespace OpenTPW.UI;

/// <summary>
/// The dialog an empty player slot opens: a name, Instant Action or Full Simulation, and a tick to
/// start.
///
/// <para>
/// 0x004a6e40 opens it, modal, from the layout stream at 0x00753f50, and the advisor explains it
/// (response 570). Inside the wave-edged dialog, w_dialog_wave.md2, "Please enter your name" (UITEXT
/// 239) is the title in font 5 on the purple skin; the name box holds "Type your name" (UITEXT 243),
/// selected so the first key replaces it, in white in font 6; and the two modes are toggles in a
/// framed group, labelled in black in font 6. The joystick beside the name is shown only while
/// Instant Action is chosen (0x004a6d00, on the group's message 0x101).
/// </para>
/// <para>
/// No mode starts chosen. The dialog asks the group for a button with id 0x7a17 - the id of the player
/// screen's joystick, not of either toggle - so the group keeps its -1, and the joystick keeps the
/// visibility the layout gives it until one is picked. A player made without picking one is Full
/// Simulation, as 0x004a6b80 only asks whether Instant Action is the choice.
/// </para>
/// <para>
/// The tick (0x004a6b80) takes the trailing spaces off the name and does nothing if that leaves it
/// empty; otherwise it makes the player and closes the dialog and the slots. The name box's
/// contents are the name whatever they are, so ticking without typing makes a player called "Type
/// your name", as it does in the original. Enter is the tick and Escape the cross (0x802, 0x804).
/// </para>
/// </summary>
internal sealed class NewPlayerDialog : UiWindow
{
	private const int TitleFont = 5;
	private const int DialogFont = 6;
	private const int InstantAction = 0x70d;

	/// <summary>What the name box refuses, from 0x00752588: the characters a file name cannot hold.</summary>
	private const string NotInNames = "\\/*?:|<>\"";

	private readonly int _slot;
	private readonly UiEdit _name;
	private readonly UiRadioGroup _mode;

	public NewPlayerDialog( FrontEnd frontEnd, int slot ) : base( frontEnd )
	{
		_slot = slot;
		Modal = true;

		Root = Backdrop();

		var dialog = Root.Add( new UiControl
		{
			Id = 0x708,
			Rect = new UiRect( 500, 233, 1516, 830 ),
			Mesh = UiMesh.Get( "w_dialog_wave" )
		} );

		dialog.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1387, 613, 1470, 696 ),
			HelpText = 2,
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = Cancel
		} );

		dialog.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1376, 524, 1460, 608 ),
			HelpText = 360,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = Accept
		} );

		dialog.Add( new UiControl
		{
			Id = 0x709,
			Rect = new UiRect( 655, 285, 1315, 365 ),
			Text = Localization.Get( UIStrings.EnterName ),
			Font = TitleFont,
			TextShadow = true
		} );

		_name = dialog.Add( new UiEdit
		{
			Id = 0x70a,
			Rect = new UiRect( 593, 616, 1216, 698 ),
			HelpText = 356,
			Mesh = UiMesh.Get( "!frame" ),
			TextRect = new UiRect( 623, 632, 1186, 677 ),
			Font = DialogFont,
			TextAcross = TextAlign.Start,
			MaxLength = 16,
			Refused = NotInNames
		} );

		_name.SetText( Localization.Get( UIStrings.TypeYourName ), selectAll: true );
		Focus = _name;

		var joystick = dialog.Add( new UiControl
		{
			Id = 0x70b,
			Rect = new UiRect( 1241, 606, 1343, 708 ),
			Mesh = UiMesh.Get( "e_joy" )
		} );

		_mode = dialog.Add( new UiRadioGroup
		{
			Id = 0x70c,
			Rect = new UiRect( 735, 377, 1235, 597 ),
			Mesh = UiMesh.Get( "!f_plain" )
		} );

		_mode.AddOption( new UiButton
		{
			Id = InstantAction,
			Rect = new UiRect( 764, 405, 1206, 476 ),
			HelpText = 358,
			Mesh = UiMesh.Get( "b_ltoggle" ),
			Text = Localization.Get( UIStrings.InstantAction ),
			Font = DialogFont,
			TextColour = UiColour.Black
		} );

		_mode.AddOption( new UiButton
		{
			Id = 0x70e,
			Rect = new UiRect( 764, 498, 1206, 569 ),
			HelpText = 359,
			Mesh = UiMesh.Get( "b_ltoggle" ),
			Text = Localization.Get( UIStrings.FullSimulation ),
			Font = DialogFont,
			TextColour = UiColour.Black
		} );

		_mode.SelectionChanged = () => joystick.Visible = _mode.Selected == InstantAction;
	}

	protected internal override void Accept()
	{
		var name = _name.Value.TrimEnd( ' ' );

		if ( name.Length == 0 )
			return;

		var player = FrontEnd.Players.Create( _slot, name, instantAction: _mode.Selected == InstantAction );
		Log.Info( $"Front end: '{player.Name}' is playing in slot {_slot + 1}, {(player.InstantAction ? "Instant Action" : "Full Simulation")}" );

		FrontEnd.PlayerCreated( this );
	}

	protected internal override void Cancel() => FrontEnd.Close( this );
}
