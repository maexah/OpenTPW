namespace OpenTPW;

public class Time
{
	public static float Delta { get; internal set; }
	public static float Now { get; internal set; }

	/// <summary>
	/// The rate the original's per-tick numbers count in - flyer speeds and turns, the camera's
	/// lag, the cloud scroll, the per-frame chances of lightning and of an ambient sound - so they
	/// can be restated per second and behave the same at any frame rate.
	///
	/// <b>Inferred from the data, not found in the engine.</b> Read at 25 a second, three
	/// independent constants come out sensible - SPINSPEED(0.02) a twelve-second orbit, the
	/// camera's 0.1 a quarter-second lag, the butterflies' 1.5 a believable 37.5 units a second -
	/// where read per second they would be a five-minute orbit and a ten-second lag. It is a
	/// separate question from <see cref="AnimationFile.FramesPerSecond"/>, the rate .md2 animations
	/// play at - 30, from the engine's own code - and is kept apart from it so that settling one
	/// cannot quietly change the other.
	/// </summary>
	public const float TicksPerSecond = 25f;

	/// <summary>
	/// The blend factor for easing something towards a target at <paramref name="rate"/> per
	/// second, to be handed to a Lerp.
	///
	/// Use this rather than multiplying the rate by <see cref="Delta"/> directly. A straight
	/// multiply is only an approximation of this curve: it drifts as the frame rate changes, so
	/// the same movement settles at different speeds at 30, 60 and 144fps, and once a frame runs
	/// longer than 1/rate seconds it overshoots the target outright.
	/// </summary>
	public static float SmoothingFactor( float rate ) => 1f - MathF.Exp( -rate * Delta );

	/// <summary>
	/// The longest frame anything is told about.
	///
	/// Easing through <see cref="SmoothingFactor"/> is safe at any delta - it saturates rather
	/// than overshooting - but the things that integrate straight, a flyer's step and a
	/// raindrop's fall, are not: after a stall for loading or an alt-tab they would jump by
	/// however long the stall was. A tenth of a second is well past the longest ordinary frame
	/// and short enough that the jump is invisible; beyond it the world runs slow for a frame,
	/// which is the better of the two failures.
	/// </summary>
	private const float LongestFrame = 0.1f;

	/// <summary>
	/// Holds the world clock still: <see cref="Delta"/> reads zero and <see cref="Now"/> stops
	/// advancing, so every animation, the ocean's scroll, the drifting sky and the camera's ease
	/// all stop where they are and the next frame draws exactly the same pixels as this one.
	///
	/// Only <see cref="DebugConsole"/>'s `pause` sets this, and that is itself off unless the
	/// environment asks for it, so the shipping game never touches it. It exists because
	/// comparing two builds by screenshot is otherwise impossible here - the lobby has nothing
	/// that stands still, so pinning the camera alone still leaves every frame different.
	/// </summary>
	public static bool Paused { get; set; }

	/// <summary>
	/// How many more frames to run at <see cref="StepDelta"/> while <see cref="Paused"/>, set by
	/// <see cref="DebugConsole"/>'s `step`.
	///
	/// Stopping the clock is not on its own enough to make two builds comparable: the animations
	/// accumulate their own elapsed time from the deltas they are given, so where they stop
	/// depends on how long the world happened to run before the pause. Stepping a counted number
	/// of fixed-size frames from a pause instead advances every one of them by exactly the same
	/// amount every run, whatever the real frame rate is doing.
	/// </summary>
	public static int StepFrames { get; set; }

	private const float StepDelta = 1f / 60f;

	public static void Update( float deltaTime )
	{
		if ( !Paused )
		{
			Delta = deltaTime.Clamp( 0f, LongestFrame );
		}
		else if ( StepFrames > 0 )
		{
			Delta = StepDelta;
			StepFrames--;
		}
		else
		{
			Delta = 0f;
		}

		Now += Delta;
	}
}
