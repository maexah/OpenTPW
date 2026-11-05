namespace OpenTPW;

/// <summary>
/// The grey a lightning bolt is drawn in, from its life left: FUN_0057ee30 (0x0057eeba..0x0057ef61). The original
/// keeps no starting lifetime, so there is no fade over a bolt's life; each frame, for each live bolt, the life left
/// in ms is taken modulo 500 and a base grey of 192..255 comes from where that falls in the tooth, plus the CRT's
/// <c>rand() &amp; 63</c>. <c>docs/exe/weather.md</c>, "The bolt" holds the walk.
/// </summary>
internal static class BoltEnvelope
{
	/// <summary>A tooth's length in ms (0x0057eed5): a lobby bolt is one tooth, a park's four.</summary>
	public const int Period = 500;

	/// <summary>The base grey, 192..255: a tooth rises 193 to 255 over its first 50 ms and falls to 192 over the next 450.</summary>
	public static int Base( int lifeMs )
	{
		// Float arithmetic as the listing's: 0x00701cd8 (0.002f), 0x00701cdc (0.9f), 0x00701ce0 (1.11f), 0x00701ce4 (64),
		// 0x00701c68 (1), 0x00701ce8 (10), 0x00701cec (-64). The conversion truncates (_ftol, 0x0067a830).
		var p = (lifeMs % Period) * 0.002f;

		return p < 0.9f
			? 255 - (int)((0.9f - p) * 1.11f * 64f)
			: 192 - (int)((1f - p) * 10f * -64f);
	}

	/// <summary>
	/// The word the bolt is packed in (0x0057ef4f..0x0057ef61), as the original packs it: not clamped, so a grey of 256
	/// to 319 loses its high bit into the alpha and the colour wraps to near black, R = G = (g - 256) | 1, B = g - 256.
	/// </summary>
	public static uint Pack( int grey )
	{
		var g = (uint)grey;
		var word = g | 0xFFFFFF00u;
		word = (word << 8) | g;
		word = (word << 8) | g;
		return word;
	}

	/// <summary>The bolt's colour in bytes for a life left and a draw of the game's <c>rand()</c>.</summary>
	public static (byte R, byte G, byte B) Colour( int lifeMs, int rand )
	{
		var word = Pack( Base( lifeMs ) + (rand & 63) );
		return ((byte)(word >> 16), (byte)(word >> 8), (byte)word);
	}
}
