namespace OpenTPW.UI;

/// <summary>
/// The lobby's build label, bottom right: the original's "v 2.0" (<c>FUN_0048b020</c>, which <c>FrontEnd_Init</c> calls on
/// every entry to the lobby; <c>docs/exe/boot.md</c>, "Which build this is").
///
/// <para>
/// Built as the original's: a plain label (<c>0x0048f6a0</c>: white, no dark edge) in font 6, lettered from the left and
/// in the middle of a box the text is measured into. The box is the text's width and the font's line in the pixels of
/// the screen the font set is drawn for, taken to the 2048 x 1536 layout and cut toward nought, its right edge at
/// <c>0x7c0</c> and its bottom at <c>0x5c0</c> (<c>0x0048b0a8</c>-<c>0x0048b0ee</c>). It is drawn under every window, as
/// the original's sits in the lobby's own root, and takes no pointer.
/// </para>
/// <para>
/// <b>A deviation:</b> the original letters its own version (<c>L"v %d.%d"</c>, 2 and 0). This one letters OpenTPW's
/// build, <c>weekly-YYYY-MM-DD</c> (a dry run's <c>weekly-test-N</c>), <c>local</c> for a build not given the tag
/// (<see cref="LaunchReport.BuildName"/>),
/// so a screenshot says which build it came from.
/// </para>
/// </summary>
internal sealed class VersionLabel : Panel
{
	/// <summary>The id the original gives the label's control (<c>0x0048b0f4</c>).</summary>
	internal const int ControlId = 0x8cfd55;

	internal const int LabelFont = 6;

	internal const int Right = 0x7c0;

	internal const int Bottom = 0x5c0;

	public UiControl Control { get; }

	public VersionLabel( string text )
	{
		Control = new UiControl
		{
			Id = ControlId,
			Font = LabelFont,
			Text = text,
			TextColour = UiColour.White,
			TextAcross = TextAlign.Start,
			TextDown = TextAlign.Centre
		};

		Log.Info( $"Front end: the build label reads '{text}'" );
	}

	protected override void OnUpdate()
	{
		if ( UiFonts.Get( LabelFont ) is { } font )
			Control.Rect = Box( font.Measure( Control.Text ), font.LineHeight, UiFonts.SetScreenHeight );
	}

	protected override void OnRender() => Control.Draw();

	/// <summary>
	/// The label's box on the layout for a text <paramref name="width"/> by <paramref name="height"/> of the font's pixels,
	/// the font drawn for a 4:3 screen <paramref name="screenHeight"/> tall: width x 2048 over the screen's width and
	/// height x 1536 over its height, each cut toward nought, back from the right edge and the bottom.
	/// </summary>
	internal static UiRect Box( int width, int height, int screenHeight )
	{
		var screenWidth = screenHeight * 4 / 3;
		var across = width * VirtualScreen.Width / screenWidth;
		var down = height * VirtualScreen.Height / screenHeight;

		return new UiRect( Right - across, Bottom - down, Right, Bottom );
	}
}
