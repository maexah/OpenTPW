namespace OpenTPW;

public class Time
{
	public static float Delta { get; internal set; }
	public static float Now { get; internal set; }

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

	public static void Update( float deltaTime )
	{
		Delta = deltaTime.Clamp( 0f, LongestFrame );
		Now += Delta;
	}
}
