namespace OpenTPW.UI;

/// <summary>
/// The strip along the bottom of the screen that says what the control under the pointer does -
/// "Left-click to view next island".
///
/// <para>
/// 0x00492180 builds it: from x 574 to 1474 on the virtual screen with its bottom at 1520, and as
/// tall as twice the help font's line at the game's resolution plus ten, worked out as
/// <c>height * 0xc00 / screen height + 10</c>. It wears f_helpbg.md2, a single quad of translucent
/// black, with the text centred on it in white in font 7. Each control carries the row of
/// UIHELPTEXT.str it shows in its layout data (op 0x11), and a control with none shows nothing.
/// </para>
/// <para>
/// It shows only while the options screen's Popup help is on (<see cref="GameOptions.PopupHelp"/>).
/// Ctrl+H turns that off and back on too, through the binding OpenTPW already had for the help bar
/// (<see cref="InputButton.ToggleHelpBar"/>).
/// </para>
/// <para>
/// <b>Engine and content.</b> The strip is engine, the same under every scene's windows. Which row a control
/// shows is the content of the layout that made the control.
/// </para>
/// </summary>
internal sealed class HelpBar
{
	private const int Left = 574;
	private const int Right = 1474;
	private const int Bottom = 1520;
	private const int HelpFont = 7;

	private readonly UiControl _bar = new()
	{
		Rect = new UiRect( Left, Bottom - 40, Right, Bottom ),
		Mesh = UiMesh.Get( "f_helpbg" ),
		Font = HelpFont,
		Visible = false
	};

	/// <summary>Whether it is switched on - the original's Popup Help option, which the button glints also wait on.</summary>
	public bool Enabled => GameOptions.Current.PopupHelp;

	/// <summary>Shows row <paramref name="helpText"/>, or nothing for -1.</summary>
	public void Update( int helpText )
	{
		if ( Input.Pressed( InputButton.ToggleHelpBar ) )
			GameOptions.Current.PopupHelp = !GameOptions.Current.PopupHelp;

		var text = Enabled && helpText >= 0 ? Localization.Help( helpText ) : "";
		_bar.Visible = text.Length > 0;

		if ( !_bar.Visible )
			return;

		_bar.Text = text;

		var lineHeight = UiFonts.Get( HelpFont )?.LineHeight ?? 13;
		var height = (lineHeight * 0xc00 / UiFonts.SetScreenHeight) + 10;
		_bar.Rect = new UiRect( Left, Bottom - height, Right, Bottom );
	}

	public void Draw() => _bar.Draw();

	/// <summary>Lets go of the text it has drawn, as its scene ends.</summary>
	public void ReleaseText() => _bar.ReleaseText();
}
