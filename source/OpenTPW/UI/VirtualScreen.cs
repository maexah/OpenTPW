namespace OpenTPW.UI;

/// <summary>
/// The screen the original lays its interface out on.
///
/// <para>
/// Every control in the front end's layout data has a rectangle on a 2048x1536 screen whatever the
/// real resolution is, and so does every mesh in ui.wad: the purple player button, b_login.md2, is
/// authored at exactly the place and size its control asks for. The original puts that screen onto
/// the real one by multiplying x by the screen's width over 2048 and y by its height over 1536,
/// separately (0x0048f4a6) - a stretch, not a fit. It could afford one: its own _Resolution.sam
/// lists 512x384 up to 2048x1536, all 4:3 but for 1280x1024, which is 5:4 and the one mode where
/// its own interface is drawn a little squat.
/// </para>
/// <para>
/// A window here can be any shape at all, and stretching the virtual screen across a wide one would
/// turn round buttons into ovals. So it keeps its shape, scaled by whichever of the window's two
/// sides runs out first (<see cref="Scale"/>), and each control is pinned to the part of the window
/// it was laid out against: one in the left third of the virtual screen to the window's left edge,
/// one in the right third to its right edge, anything else to the middle, and the same again down
/// the screen against its top, bottom and middle. A control that sits inside its parent moves with
/// its parent instead, so a panel's buttons never drift off the panel, and one the thirds would
/// part from what it belongs with names its own edge - see <see cref="UiControl.PinAcross"/>.
/// </para>
/// <para>
/// On a 4:3 window all of that lands exactly where the original has it: the scale is the height's,
/// every offset is zero, and the layout is the original's to the pixel.
/// </para>
/// </summary>
internal static class VirtualScreen
{
	public const int Width = 2048;
	public const int Height = 1536;

	/// <summary>The whole virtual screen, which is the rectangle a window's root control usually has.</summary>
	public static readonly UiRect Whole = new( 0, 0, Width, Height );

	/// <summary>
	/// Window pixels to a virtual unit: whichever of the two sides runs out first, so the whole
	/// 2048x1536 screen always fits inside the window with its shape kept. A window wider than 4:3
	/// is held by its height, which is what every window was held by before any other shape was
	/// allowed; one narrower than 4:3 - 1280x1024, or a window dragged tall - by its width. Scaling
	/// by the height alone drew a narrow window's interface wider than the window, and the player
	/// slots ran off both edges of it.
	/// </summary>
	public static float Scale => MathF.Min( Screen.Width / Width, Screen.Height / Height );

	/// <summary>Where a rectangle is on the window, in pixels from its top-left corner.</summary>
	public static PixelRect ToPixels( UiRect rect, Anchor anchor, VerticalAnchor down )
	{
		var scale = Scale;

		return new PixelRect(
			Offset( anchor ) + (rect.Left * scale),
			OffsetDown( down ) + (rect.Top * scale),
			rect.Width * scale,
			rect.Height * scale );
	}

	/// <summary>How far across the virtual screen a point <paramref name="x"/> pixels across the window is, for something pinned by <paramref name="anchor"/>.</summary>
	public static float ToVirtualX( float x, Anchor anchor ) => (x - Offset( anchor )) / Scale;

	/// <summary>How far down the virtual screen a point <paramref name="y"/> pixels down the window is, for something pinned by <paramref name="down"/>.</summary>
	public static float ToVirtualY( float y, VerticalAnchor down ) => (y - OffsetDown( down )) / Scale;

	/// <summary>How far across the window, in pixels, the left edge of the virtual screen is for something pinned by <paramref name="anchor"/>.</summary>
	public static float Offset( Anchor anchor ) => anchor switch
	{
		Anchor.Left => 0f,
		Anchor.Right => Screen.Width - (Width * Scale),
		_ => (Screen.Width - (Width * Scale)) * 0.5f
	};

	/// <summary>
	/// How far down the window, in pixels, the top edge of the virtual screen is for something
	/// pinned by <paramref name="down"/>. Zero every time on a window at least as wide as 4:3,
	/// where the height is what the scale is taken from and the virtual screen is exactly as tall
	/// as the window.
	/// </summary>
	public static float OffsetDown( VerticalAnchor down ) => down switch
	{
		VerticalAnchor.Top => 0f,
		VerticalAnchor.Bottom => Screen.Height - (Height * Scale),
		_ => (Screen.Height - (Height * Scale)) * 0.5f
	};

	/// <summary>Which edge a rectangle was laid out against - see the class remarks.</summary>
	public static Anchor AnchorFor( UiRect rect ) => AnchorAt( (rect.Left + rect.Right) * 0.5f );

	/// <summary>Which edge, down the screen, a rectangle was laid out against - see the class remarks.</summary>
	public static VerticalAnchor VerticalAnchorFor( UiRect rect ) => VerticalAnchorAt( (rect.Top + rect.Bottom) * 0.5f );

	/// <summary>Which edge something centred at <paramref name="x"/> across the virtual screen is pinned to.</summary>
	public static Anchor AnchorAt( float x )
	{
		return x < Width / 3f ? Anchor.Left
			: x > Width * 2f / 3f ? Anchor.Right
			: Anchor.Centre;
	}

	/// <summary>Which edge something centred at <paramref name="y"/> down the virtual screen is pinned to.</summary>
	public static VerticalAnchor VerticalAnchorAt( float y )
	{
		return y < Height / 3f ? VerticalAnchor.Top
			: y > Height * 2f / 3f ? VerticalAnchor.Bottom
			: VerticalAnchor.Middle;
	}
}

internal enum Anchor
{
	Left,
	Centre,
	Right
}

internal enum VerticalAnchor
{
	Top,
	Middle,
	Bottom
}

/// <summary>
/// A rectangle on the <see cref="VirtualScreen"/>, stored the way the layout data stores one: left,
/// top, right and bottom, with y growing downwards.
/// </summary>
internal readonly record struct UiRect( int Left, int Top, int Right, int Bottom )
{
	public int Width => Right - Left;
	public int Height => Bottom - Top;

	public bool IsWholeScreen => Left <= 0 && Top <= 0 && Right >= VirtualScreen.Width - 1 && Bottom >= VirtualScreen.Height - 1;

	public bool Contains( UiRect other )
		=> other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;
}

/// <summary>A rectangle on the window, in pixels from its top-left corner.</summary>
internal readonly record struct PixelRect( float X, float Y, float Width, float Height )
{
	public bool Contains( float x, float y ) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>A colour the interface asks for, as the original gives it: three bytes.</summary>
internal readonly record struct UiColour( byte R, byte G, byte B )
{
	public static readonly UiColour White = new( 255, 255, 255 );
	public static readonly UiColour Black = new( 0, 0, 0 );
}
