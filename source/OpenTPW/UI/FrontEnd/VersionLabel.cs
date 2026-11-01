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
/// build, <c>weekly-YYYY-MM-DD</c> (a dry run's <c>weekly-test-N</c>), and for a build not given the tag <c>local - </c> and the
/// first eight characters of its commit, plain <c>local</c> where none is known (<see cref="LaunchReport.BuildLabel"/>),
/// so a screenshot says which build it came from. The Escape menu letters it again in the same place (<see cref="GameMenu"/>),
/// and <see cref="UpdateNotice"/> stands on the line above it when a newer weekly build is out.
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
		if ( LabelBox( Control.Text! ) is { } box )
			Control.Rect = box;
	}

	protected override void OnRender() => Control.Draw();

	protected override void OnDelete() => Control.ReleaseText();

	/// <summary>The box of the label <paramref name="text"/> in the build line's font, or null while the font is not loaded.</summary>
	internal static UiRect? LabelBox( string text )
		=> UiFonts.Get( LabelFont ) is { } font ? Box( font.Measure( text ), font.LineHeight, UiFonts.SetScreenHeight ) : null;

	/// <summary>
	/// The box of the update notice's <paramref name="notice"/> text: its own line directly above the label's, the two right-aligned to
	/// the same edge (<see cref="Right"/>).
	/// </summary>
	internal static UiRect? NoticeBox( string label, string notice )
	{
		if ( UiFonts.Get( LabelFont ) is not { } font )
			return null;

		var own = Box( font.Measure( notice ), font.LineHeight, UiFonts.SetScreenHeight );
		var beside = Box( font.Measure( label ), font.LineHeight, UiFonts.SetScreenHeight );
		var height = own.Bottom - own.Top;

		return new UiRect( own.Left, beside.Top - height, own.Right, beside.Top );
	}

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
