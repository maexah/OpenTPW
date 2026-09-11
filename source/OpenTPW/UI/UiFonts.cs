namespace OpenTPW.UI;

/// <summary>
/// The thirteen fonts the interface asks for by number.
///
/// <para>
/// A control names a font by slot, and 0x00485a70 hands back that slot out of whichever of four sets
/// suits the resolution. The language loader (0x00419710) loads each set by resource id, and the ids
/// go through Language\English\residx.dat - whose entries run 1021 upwards over GAME5AA.bf4 to
/// POSTCARD.bf4 in the order its names are listed. It picks the set from the resolution: the smallest
/// for the lowest, the largest for 1024x768 and up. That the four sets are for 512x384, 640x480,
/// 800x600 and anything bigger is inferred from the options screen offering exactly those steps.
/// </para>
/// <para>
/// Each set is drawn at its own pixel size, so here the set is chosen for the tallest of those
/// resolutions the window is at least as tall as, and scaled up from there by
/// <see cref="Scale"/>.
/// </para>
/// <para>
/// What the front end uses: 0 is the park's name on the lobby panel, 2 the key count, 4 a message
/// box's text, 5 the purple buttons and dialog titles, 6 the new player dialog, 7 the help bar.
/// </para>
/// </summary>
internal static class UiFonts
{
	private static readonly int[] SetHeights = [384, 480, 600, 768];

	private static readonly string[][] Sets =
	[
		["MENUSMALL.bf4", "CASHSMALL.bf4", "SESHSMALL.bf4", "DATETINY.bf4", "GAME6AA.bf4", "TITLESMALL.bf4",
			"GAME5AA.bf4", "GAME6AA.bf4", "GAME5AA.bf4", "GAME5AA.bf4", "CONSOLE6.bf4", "GAME6AA.bf4", "POSTCARD.bf4"],

		["MENUMED.bf4", "CASHMED.bf4", "SESHMED.bf4", "DATESMALL.bf4", "GAME10AA.bf4", "TITLEMED.bf4",
			"GAME7.bf4", "GAME8.bf4", "GAME8AA.bf4", "GAME7.bf4", "CONSOLE6.bf4", "GAME7.bf4", "POSTCARD.bf4"],

		["MENUBIG.bf4", "CASHBIG.bf4", "SESHBIG.bf4", "DATEBIG.bf4", "GAME8AA.bf4", "TITLEBIG.bf4",
			"GAME8.bf4", "GAME9.bf4", "GAME8AA.bf4", "GAME8.bf4", "CONSOLE6.bf4", "GAME8.bf4", "POSTCARD.bf4"],

		["MENUBIG.bf4", "CASHBIG.bf4", "SESHBIG.bf4", "DATEBIG.bf4", "GAME12AA.bf4", "TITLEBIG.bf4",
			"GAME9.bf4", "GAME10.bf4", "GAME12.bf4", "GAME9.bf4", "CONSOLE6.bf4", "GAME9.bf4", "POSTCARD.bf4"],
	];

	/// <summary>Which set the window's height calls for.</summary>
	public static int SetIndex
	{
		get
		{
			var index = 0;

			for ( int i = 0; i < SetHeights.Length; ++i )
			{
				if ( Screen.Size.Y >= SetHeights[i] )
					index = i;
			}

			return index;
		}
	}

	/// <summary>How much larger than its own pixels a font is drawn.</summary>
	public static float Scale => Screen.Height / SetHeights[SetIndex];

	/// <summary>The height of the screen the current set was drawn for, in pixels.</summary>
	public static int SetScreenHeight => SetHeights[SetIndex];

	/// <summary>The font in <paramref name="slot"/>, or null for a slot that does not exist.</summary>
	public static BitmapFont? Get( int slot )
		=> slot >= 0 && slot < Sets[0].Length ? BitmapFont.Get( Sets[SetIndex][slot] ) : null;

	/// <summary>Loads every font the current set holds, so the first dialog to need one does not wait.</summary>
	public static void Preload()
	{
		foreach ( var name in Sets[SetIndex].Distinct() )
			BitmapFont.Get( name );
	}
}
