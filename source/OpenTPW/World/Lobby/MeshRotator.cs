using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Plays rotation animations onto a model's meshes.
///
/// Where a vertex animation reshapes one mesh (see <see cref="MeshAnimator"/>), a rotation
/// animation turns whole meshes about their own origins - the jungle gate's two doors swing
/// about the point each door was authored around. Each track names the mesh it drives, so one
/// animation can move several at once.
///
/// As with vertex animations, nothing in the files says how a model's animations are sequenced,
/// so they are played one after another on a loop. For the gate that reads correctly by luck of
/// the authoring: M1 opens the doors and M2 shuts them again.
/// </summary>
public class MeshRotator
{
	private readonly AnimationFile[] _animations;
	private readonly ModelEntity[] _entities;
	private readonly Quaternion[] _baseRotations;

	private int _current;
	private float _elapsed;

	public MeshRotator( AnimationFile[] animations, ModelEntity[] entities, Quaternion[] baseRotations )
	{
		_animations = animations;
		_entities = entities;
		_baseRotations = baseRotations;
	}

	/// <summary>
	/// True when at least one of this animation's rotation tracks lands on a mesh of the model.
	/// Models with extra hierarchy nodes index past their meshes - see <see cref="AnimationFile"/>.
	/// </summary>
	public static bool Drives( AnimationFile animation, int meshCount )
		=> animation.RotationTracks.Any( track => track.TargetIndex < meshCount );

	public void Update( float deltaTime )
	{
		var animation = _animations[_current];
		var duration = Duration( animation );

		_elapsed += deltaTime;
		while ( _elapsed >= duration )
		{
			_elapsed -= duration;
			_current = (_current + 1) % _animations.Length;
			animation = _animations[_current];
			duration = Duration( animation );
		}

		var frame = animation.FirstFrame + (_elapsed * MeshAnimator.FramesPerSecond);

		foreach ( var track in animation.RotationTracks )
		{
			if ( track.TargetIndex < 0 || track.TargetIndex >= _entities.Length )
				continue;

			// The keyframes are a delta from the mesh's authored orientation - every animation
			// that starts from rest starts on the identity quaternion.
			var rotation = LobbyModel.ToWorldSpace( track.Sample( frame ) );

			_entities[track.TargetIndex].Rotation = _baseRotations[track.TargetIndex] * rotation;
		}
	}

	private static float Duration( AnimationFile animation )
		=> Math.Max( animation.LastFrame - animation.FirstFrame, 1 ) / MeshAnimator.FramesPerSecond;
}
