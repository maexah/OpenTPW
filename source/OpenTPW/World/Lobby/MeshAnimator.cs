using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Everything still unknown about the .md2 animation format lives here, so hypotheses can be
/// swapped without touching the parser or the entity plumbing.
///
/// Two things are unknown and independent: how a channel's 32-bit value decodes to a number,
/// and which entity a channel drives. Resolve the target first (Layout.ForceSpin) - a decode
/// iterated against the wrong mesh can't be falsified.
/// </summary>
public static class AnimTuning
{
	public enum ChannelLayout
	{
		/// <summary>Sample and log, change nothing on screen. Safe default.</summary>
		None,

		/// <summary>Ignore the file; spin ProbeMeshIndex so we can see which mesh it is.</summary>
		ForceSpin,

		/// <summary>Drive yaw from a single channel - the "does this channel do anything" probe.</summary>
		SingleChannelYaw,

		/// <summary>ChannelBase + 0..2 as euler angles.</summary>
		EulerXYZ,

		/// <summary>ChannelBase + 0..2 as a world-space offset.</summary>
		TranslationXYZ,

		/// <summary>ChannelBase + 0..3 as a quaternion.</summary>
		QuatXYZW,
	}

	public static ChannelLayout Layout = ChannelLayout.None;

	/// <summary>Value decode: raw is treated as fixed point, then biased and gained.</summary>
	public static float Scale = 1f / (1 << 29);
	public static float Bias = 0f;
	public static float Gain = 1f;

	/// <summary>Which mesh of the base model the animated channel block drives.</summary>
	public static int TargetMeshIndex = 10;

	/// <summary>First channel of the block being tested.</summary>
	public static int ChannelBase = 163;

	/// <summary>Mesh spun by ForceSpin, and the channel probed by SingleChannelYaw.</summary>
	public static int ProbeMeshIndex = 10;
	public static int ProbeChannel = 163;

	/// <summary>The keyframe indices are authoring frames; this maps them onto seconds.</summary>
	public static float FramesPerSecond = 25f;

	public static float Decode( uint raw ) => ((raw * Scale) + Bias) * Gain;
}

/// <summary>
/// Samples an <see cref="AnimationFile"/> and applies it on top of each mesh's rest transform.
/// </summary>
public class MeshAnimator
{
	private readonly AnimationFile _animation;

	public MeshAnimator( AnimationFile animation )
	{
		_animation = animation;
	}

	/// <summary>Wraps elapsed time onto the animation's frame range.</summary>
	private float FrameAt( float time )
	{
		var span = _animation.LastFrame - _animation.FirstFrame;
		if ( span <= 0 )
			return _animation.FirstFrame;

		var frame = (time * AnimTuning.FramesPerSecond) % span;
		return _animation.FirstFrame + frame;
	}

	/// <summary>Decoded value of a channel at a frame, or null when the channel isn't present.</summary>
	public float? Sample( int channelId, float frame )
	{
		if ( !_animation.TryGetChannel( channelId, out var track, out var slot ) || track == null )
			return null;

		var frames = track.FrameIndices;

		if ( track.IsConstant )
			return AnimTuning.Decode( track.Raw( 0, slot ) );

		if ( frame <= frames[0] )
			return AnimTuning.Decode( track.Raw( 0, slot ) );

		if ( frame >= frames[^1] )
			return AnimTuning.Decode( track.Raw( frames.Length - 1, slot ) );

		var hi = 1;
		while ( hi < frames.Length && frames[hi] < frame )
			hi++;

		var lo = hi - 1;
		var span = frames[hi] - frames[lo];
		var t = span <= 0 ? 0f : (frame - frames[lo]) / span;

		// Decode before interpolating - the decode may not be linear.
		var a = AnimTuning.Decode( track.Raw( lo, slot ) );
		var b = AnimTuning.Decode( track.Raw( hi, slot ) );
		return a + ((b - a) * t);
	}

	public void Apply( float time, ModelEntity?[] meshEntities, Vector3[] basePositions, Quaternion[] baseRotations )
	{
		var frame = FrameAt( time );

		switch ( AnimTuning.Layout )
		{
			case AnimTuning.ChannelLayout.None:
				return;

			case AnimTuning.ChannelLayout.ForceSpin:
			{
				var entity = At( meshEntities, AnimTuning.ProbeMeshIndex );
				if ( entity == null ) return;
				entity.Rotation = Yaw( time ) * baseRotations[AnimTuning.ProbeMeshIndex];
				return;
			}

			case AnimTuning.ChannelLayout.SingleChannelYaw:
			{
				var entity = At( meshEntities, AnimTuning.ProbeMeshIndex );
				var value = Sample( AnimTuning.ProbeChannel, frame );
				if ( entity == null || value == null ) return;
				entity.Rotation = Yaw( value.Value ) * baseRotations[AnimTuning.ProbeMeshIndex];
				return;
			}

			case AnimTuning.ChannelLayout.EulerXYZ:
			{
				var entity = At( meshEntities, AnimTuning.TargetMeshIndex );
				var x = Sample( AnimTuning.ChannelBase + 0, frame );
				var y = Sample( AnimTuning.ChannelBase + 1, frame );
				var z = Sample( AnimTuning.ChannelBase + 2, frame );
				if ( entity == null || x == null || y == null || z == null ) return;
				var rot = Quaternion.CreateFromYawPitchRoll( y.Value, x.Value, z.Value );
				entity.Rotation = baseRotations[AnimTuning.TargetMeshIndex] * rot;
				return;
			}

			case AnimTuning.ChannelLayout.TranslationXYZ:
			{
				var entity = At( meshEntities, AnimTuning.TargetMeshIndex );
				var x = Sample( AnimTuning.ChannelBase + 0, frame );
				var y = Sample( AnimTuning.ChannelBase + 1, frame );
				var z = Sample( AnimTuning.ChannelBase + 2, frame );
				if ( entity == null || x == null || y == null || z == null ) return;
				entity.Position = basePositions[AnimTuning.TargetMeshIndex] + new Vector3( x.Value, z.Value, y.Value );
				return;
			}

			case AnimTuning.ChannelLayout.QuatXYZW:
			{
				var entity = At( meshEntities, AnimTuning.TargetMeshIndex );
				var x = Sample( AnimTuning.ChannelBase + 0, frame );
				var y = Sample( AnimTuning.ChannelBase + 1, frame );
				var z = Sample( AnimTuning.ChannelBase + 2, frame );
				var w = Sample( AnimTuning.ChannelBase + 3, frame );
				if ( entity == null || x == null || y == null || z == null || w == null ) return;
				var q = Quaternion.Normalize( new Quaternion( x.Value, z.Value, y.Value, -w.Value ) );
				entity.Rotation = baseRotations[AnimTuning.TargetMeshIndex] * q;
				return;
			}
		}
	}

	private static ModelEntity? At( ModelEntity?[] entities, int index )
		=> index >= 0 && index < entities.Length ? entities[index] : null;

	/// <summary>
	/// World up is Z here - the lobby camera orbits in XY at a fixed Z - so "turning on the
	/// spot" is a rotation about Z, applied in world space (pre-multiplied).
	/// </summary>
	private static Quaternion Yaw( float radians )
		=> Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, radians );
}
