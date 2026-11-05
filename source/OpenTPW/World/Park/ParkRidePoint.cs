namespace OpenTPW;

/// <summary>
/// The sub-cell point a guest is put down at on a ride's exit, or taken to stand at to board: <c>FUN_004dedf0</c>'s
/// bytes (<c>docs/exe/ride-operation.md</c>, "Leaving a ride").
/// </summary>
public static class ParkRidePoint
{
	/// <summary>
	/// The two bytes <c>FUN_004dedf0</c> makes of the descriptor's pair of fractions, for a thing turned <paramref name="angle"/>
	/// degrees (its <c>+0x10</c>), in the order it stores them: the byte of the position's x, then of its z. A byte is the
	/// fraction times 255.0 (<c>0x004df123</c>, <c>fmul</c> of the double at <c>0x00700578</c>) truncated by <c>__ftol</c>
	/// (<c>0x0067a830</c>), so 0.5 is <c>0x7f</c>. The angle turns the pair first (<c>0x004df0ae</c>..<c>0x004df11d</c>): 0 as
	/// it stands, 90 gives <c>(y, 1 - x)</c>, 180 <c>(1 - x, 1 - y)</c> and 270 <c>(1 - y, x)</c>; any other angle leaves
	/// <c>(x, x)</c>, because the compare chain falls to a load of x and leaves x in the slot that should hold y.
	/// </summary>
	/// <remarks>
	/// The original also logs "Dodgy X exit point in SAM file" for a fraction not above 0 and below 1 (<c>0x004dee3d</c>..)
	/// and goes on; no queued item of the four themes trips it (the census test), and the log is not reproduced. The second
	/// slot's 1 - f at 90 and 180 is stored as a 32-bit float before the multiply (<c>0x004df0dc</c>, <c>0x004df119</c>), so
	/// it is cut to a float here (0.2 gives 204 and 0.4 gives 153). The 270 path and 180's first slot keep 1 - f in the x87
	/// register, which is 53-bit (control word <c>0x27f</c> at <c>0x004df0ae</c>, <c>docs/exe/ride-operation.md</c>, "Leaving a
	/// ride"), so they are done in doubles (0.2 gives 203 and 0.4 gives 152).
	/// </remarks>
	public static (byte X, byte Y) Bytes( float x, float y, int angle )
	{
		var (first, second) = angle switch
		{
			0 => ((double)x, (double)y),
			90 => ((double)y, (double)(1f - x)),
			180 => (1.0 - x, (double)(1f - y)),
			270 => (1.0 - y, (double)x),
			_ => ((double)x, (double)x)
		};

		return (ToByte( first ), ToByte( second ));
	}

	/// <summary>The fraction times 255.0 cut towards nought, then the low byte of the integer - <c>__ftol</c>, then <c>mov [edi], al</c>.</summary>
	private static byte ToByte( double fraction ) => unchecked((byte)(int)(fraction * 255.0));

	/// <summary>
	/// The position <c>FUN_004fa930</c> puts a person at for a cell and this byte: the cell and the byte make a 16-bit
	/// word, high byte the cell, and the word is shifted left 8 (<c>0x004fa946</c>), so the byte is in 256ths of a cell
	/// on the navigator's own scale.
	/// </summary>
	public static int Fixed( int cell, byte sub ) => cell * PeepNavigator.One + sub * (PeepNavigator.One / 256);
}
