namespace OpenTPW;

public static class Screen
{
	public static Point2 Size { get; set; } = new( 1, 1 );

	public static float Width => Size.X;
	public static float Height => Size.Y;

	public static float Aspect => (float)Size.X / (float)Size.Y;

	/// <summary>
	/// Takes the window's size, never letting it fall below a pixel each way. A minimised window
	/// reports no size at all on some desktops, and a zero here would go straight out through
	/// <see cref="Aspect"/> into the projection, the interface's scale and every screen-space
	/// transform behind them, as a NaN that nothing downstream tests for.
	/// </summary>
	public static void UpdateFrom( Point2 size )
	{
		Size = new Point2( Math.Max( size.X, 1 ), Math.Max( size.Y, 1 ) );
	}
}
