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

	public static void Update( float deltaTime )
	{
		Delta = deltaTime;
		Now += deltaTime;
	}
}
