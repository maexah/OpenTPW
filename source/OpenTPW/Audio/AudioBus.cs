namespace OpenTPW;

/// <summary>
/// Which of the game's three groups of sound a voice belongs to.
///
/// The original keeps a volume per group rather than per sound - data\sound.sam ships the
/// defaults as SFX 75, MUSIC 60, SPEECH 75 and MOVIE 100 out of a hundred - and it is those
/// group volumes that the advisor lowers while he talks. <see cref="Audio.Duck"/> is the same
/// idea, so a voice has to say which group it is in before it can be left out of the ducking.
///
/// There is no Movie here because nothing plays the FMV yet. Add it when something does; the
/// mixer treats anything that is not <see cref="Speech"/> as duckable.
/// </summary>
public enum AudioBus
{
	/// <summary>Ambience, one-shots, thunder - everything that is not music or a voice.</summary>
	Effects,

	/// <summary>The park themes.</summary>
	Music,

	/// <summary>The advisor. The one bus <see cref="Audio.Duck"/> leaves alone.</summary>
	Speech
}
