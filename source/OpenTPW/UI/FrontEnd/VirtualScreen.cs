namespace OpenTPW.UI;

/// <summary>
/// The screen the original lays its interface out on.
///
/// <para>
/// Every control in the front end's layout data has a rectangle on a 2048x1536 screen whatever the
/// real resolution is, and so does every mesh in ui.wad: the purple player button, b_login.md2, is
/// authored at exactly the place and size its control asks for. The original only ever ran at 4:3 -
/// _Resolution.sam lists 512x384 up to 2048x1536 - so it simply scaled that screen to fit.
/// </para>
/// <para>
/// A window here can be wider than 4:3, and stretching the virtual screen across it would turn round
/// buttons into ovals. So it keeps its shape, scaled to the window's height, and each control is
/// pinned to the part of the window it was laid out against: one in the left third of the virtual
/// screen to the window's left edge, one in the right third to its right edge, anything else to the
/// middle. A control that sits inside its parent moves with its parent instead, so a panel's buttons
/// never drift off the panel. On a 4:3 window every pin lands in the same place, and the layout is
/// exactly the original's.
/// </para>
/// </summary>
internal static class VirtualScreen
{
	public const int Width = 2048;
	public const int Height = 1536;

	/// <summary>The whole virtual screen, which is the rectangle a window's root control usually has.</summary>
	public static readonly UiRect Whole = new( 0, 0, Width, Height );

	/// <summary>Window pixels to a virtual unit.</summary>
	public static float Scale => Screen.Height / Height;

	/// <summary>Where a rectangle is on the window, in pixels from its top-left corner.</summary>
	public static PixelRect ToPixels( UiRect rect, Anchor anchor )
	{
		var scale = Scale;
		var offset = Offset( anchor );

		return new PixelRect( offset + (rect.Left * scale), rect.Top * scale, rect.Width * scale, rect.Height * scale );
	}

	/// <summary>How far across the window, in pixels, the left edge of the virtual screen is for something pinned by <paramref name="anchor"/>.</summary>
	public static float Offset( Anchor anchor ) => anchor switch
	{
		Anchor.Left => 0f,
		Anchor.Right => Screen.Width - (Width * Scale),
		_ => (Screen.Width - (Width * Scale)) * 0.5f
	};

	/// <summary>Which edge a rectangle was laid out against - see the class remarks.</summary>
	public static Anchor AnchorFor( UiRect rect ) => AnchorAt( (rect.Left + rect.Right) * 0.5f );

	/// <summary>Which edge something centred at <paramref name="x"/> across the virtual screen is pinned to.</summary>
	public static Anchor AnchorAt( float x )
	{
		return x < Width / 3f ? Anchor.Left
			: x > Width * 2f / 3f ? Anchor.Right
			: Anchor.Centre;
	}
}

internal enum Anchor
{
	Left,
	Centre,
	Right
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
