namespace OpenTPW.UI;

/// <summary>
/// The interface's sounds: effects of the category the original registers as "cat_ui" (0x0051eae0)
/// and keeps at 0x00803a2c, out of data\global\sound.
///
/// <para>
/// <b>What plays them.</b> Two things in the lobby, and nothing else there does. UI_Init hooks every
/// message the interface sends (0x00485780), and on a button's "I was clicked" message it plays
/// effect 31, BUTTON01 - or effect 189, Select3, when the control the message is for is flagged
/// 0x10, which in the lobby's layouts is the new player dialog's pair of game-mode toggles. And the
/// advisor's cue in his tour of the lobby (see LobbyAdvisor) plays effect 198, goldkey, as the new
/// player's key arrives. The sparkle round an affordable park's key, the burst at that cue and the
/// glints on a button under the pointer are not sounds at all but particle effects, out of
/// data\Particle\Tp2.plb, which nothing here draws yet.
/// </para>
/// <para>
/// <b>How loud.</b> Set from the samples' measured loudness, the way <see cref="LobbyAudio"/>'s layers
/// are, rather than by ear. BUTTON01 measures -21.7 dBFS RMS and peaks at +0.6, Select3 -17.8, and
/// goldkey -17.7 peaking at -0.3. A click is put at -30 dBFS, under the park's theme (-26) and over
/// its ambience (-34), and goldkey at -24, level with thunder - it only ever plays while the advisor
/// talks, so his duck takes it down to about -32.
/// </para>
/// <para>
/// <b>No repeat delay.</b> The category gives every effect a repeat delay - 5.7 seconds for BUTTON01,
/// 3.7 for the rest - but the reading of that field was established on the lobby's ambience, and
/// honouring it here would swallow every click but the first in a quick run of them. Whether the
/// original applies it to its own interface is not established, so these always play.
/// </para>
/// </summary>
internal static class UiSounds
{
	private const int ButtonClick = 31;
	private const int ToggleClick = 189;
	private const int GoldKey = 198;

	private const float ButtonClickVolume = 0.385f;
	private const float ToggleClickVolume = 0.245f;
	private const float GoldKeyVolume = 0.484f;

	private static SoundCategory? _category;

	/// <summary>Loads the category, so the first click does not wait on its banks.</summary>
	public static void Preload()
	{
		if ( Audio.Ready )
			_category ??= new SoundCategory( "global", "global/sound", "ui" );
	}

	/// <summary>A button was clicked - see the class remarks for which sound.</summary>
	public static void Click( bool toggle )
	{
		if ( toggle )
			Play( ToggleClick, ToggleClickVolume );
		else
			Play( ButtonClick, ButtonClickVolume );
	}

	/// <summary>The advisor has handed a new player their first golden key.</summary>
	public static void GoldKeyHandedOver() => Play( GoldKey, GoldKeyVolume );

	private static void Play( int effect, float volume )
	{
		Preload();
		_category?.Play( effect, volume, respectDelay: false );
	}
}
