using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Plays rotation animations onto a model's meshes.
///
/// Where a vertex animation reshapes one mesh (see <see cref="MeshAnimator"/>), a rotation
/// animation turns whole meshes about their own origins - the jungle gate's two doors swing
/// about the point each door was authored around. Each track names the mesh it drives, so one
/// animation can move several at once, and a keyframe is an orientation outright rather than a
/// turn to add to the mesh's own - see <see cref="BuildRestInverses"/>.
///
/// A turned mesh takes its children with it: the space gate's two sign panels hang off its
/// hatch, and only the hatch is animated, so without this they would stay put while it swung
/// away from underneath them.
/// </summary>
public class MeshRotator
{
	/// <summary>
	/// Two quaternions this close are the same pose. Loose enough for the 0.7 degrees hallow's
	/// gate stops short of shut by.
	/// </summary>
	private const float SamePose = 0.9999f;

	/// <summary>
	/// A gate is authored as a pair: M1 swings it open and M2 shuts it again. Hallow ships a
	/// third clip, which is the tail of the same timeline rather than a third movement, and it
	/// has no place in an open/shut loop.
	/// </summary>
	private const int ClipsUsed = 2;

	private readonly AnimationFile[] _animations;
	private readonly ModelEntity[] _entities;
	private readonly Matrix4x4[] _baseTransforms;
	private readonly Matrix4x4[] _restInverses;
	private readonly Matrix4x4[] _baseInverses;
	private readonly System.Numerics.Vector3[] _baseOffsets;
	private readonly int[][] _descendants;
	private readonly int[] _motionEnds;

	private int _current;
	private float _elapsed;

	public MeshRotator( AnimationFile[] animations, ModelEntity[] entities, Matrix4x4[] baseTransforms,
		Vector3[] offsets, int[] parentIndices )
	{
		_animations = animations.Take( ClipsUsed ).ToArray();
		_entities = entities;
		_baseTransforms = baseTransforms;
		_restInverses = BuildRestInverses( baseTransforms );
		_baseOffsets = offsets.Select( offset => offset.GetSystemVector3() ).ToArray();
		_descendants = BuildDescendants( parentIndices );

		_baseInverses = new Matrix4x4[baseTransforms.Length];
		for ( int i = 0; i < baseTransforms.Length; ++i )
			if ( !Matrix4x4.Invert( baseTransforms[i], out _baseInverses[i] ) )
				_baseInverses[i] = Matrix4x4.Identity;

		_motionEnds = _animations.Select( animation => MotionEnd( animation, _animations[0] ) ).ToArray();
	}

	/// <summary>
	/// True when at least one of this animation's rotation tracks lands on a mesh of the model.
	/// Models with extra hierarchy nodes index past their meshes - see <see cref="AnimationFile"/>.
	/// </summary>
	public static bool Drives( AnimationFile animation, int meshCount )
		=> animation.RotationTracks.Any( track => track.TargetIndex < meshCount );

	public void Update( float deltaTime )
	{
		var duration = Duration( _current );

		_elapsed += deltaTime;
		while ( _elapsed >= duration )
		{
			_elapsed -= duration;
			_current = (_current + 1) % _animations.Length;
			duration = Duration( _current );
		}

		var animation = _animations[_current];
		var frame = animation.FirstFrame + (_elapsed * AnimationFile.FramesPerSecond);

		foreach ( var track in animation.RotationTracks )
		{
			var target = track.TargetIndex;

			if ( target < 0 || target >= _entities.Length )
				continue;

			// Same Y/Z swap the meshes go through; that swap flips handedness, so the rotation
			// is conjugated too.
			var q = track.Sample( frame );
			var rotation = Matrix4x4.CreateFromQuaternion( new Quaternion( q.X, q.Z, q.Y, -q.W ) );

			// The keyframe is the orientation the mesh should hold rather than a turn to add to
			// it, so the authored one comes out first - see BuildRestInverses. Applied inside the
			// mesh's own transform, so a door turns about its hinge rather than being swung
			// around by whatever orientation its parent gave it.
			var turned = rotation * _restInverses[target] * _baseTransforms[target];
			_entities[target].LinearTransform = turned;

			if ( _descendants[target].Length == 0 )
				continue;

			// What the turn did to the world around this mesh, which is what its children have to
			// be carried through: they keep their own shape and swing about their parent's origin.
			var change = _baseInverses[target] * turned;
			var pivot = _entities[target].Position;

			foreach ( var child in _descendants[target] )
			{
				_entities[child].LinearTransform = _baseTransforms[child] * change;
				_entities[child].Position =
					(Vector3)System.Numerics.Vector3.Transform( _baseOffsets[child] - _baseOffsets[target], change ) + pivot;
			}
		}
	}

	/// <summary>
	/// The inverse of each mesh's authored orientation.
	///
	/// A rotation keyframe is the orientation the mesh should be in, not a turn to add to the one
	/// it was already authored with. Every one of these animations opens on exactly the rotation
	/// its own mesh carries: the hallow gate's two halves are authored at 90 and 180 degrees and
	/// their first keyframes are 90 and 180 degrees, and the space gate's hatch is authored at
	/// 175 degrees and opens on 175. So the authored rotation has to come back out before the
	/// animated one goes in, or the mesh is turned twice - which laid hallow's doors flat into
	/// the ground and left space's hatch facing backwards.
	///
	/// The jungle gate hid this: its doors are authored square, so composing and replacing agree,
	/// and it was the model this class was written against.
	/// </summary>
	private static Matrix4x4[] BuildRestInverses( Matrix4x4[] baseTransforms )
	{
		var inverses = new Matrix4x4[baseTransforms.Length];

		for ( int i = 0; i < baseTransforms.Length; ++i )
		{
			// A sheared node has no rotation to take out - see LobbyModel.ToWorldSpace - so it
			// keeps the identity here and behaves exactly as it did before.
			inverses[i] = Matrix4x4.Decompose( baseTransforms[i], out _, out var rotation, out _ )
				&& Matrix4x4.Invert( Matrix4x4.CreateFromQuaternion( rotation ), out var inverse )
					? inverse
					: Matrix4x4.Identity;
		}

		return inverses;
	}

	/// <summary>Every mesh under each mesh, so a turn can carry the whole subtree.</summary>
	private static int[][] BuildDescendants( int[] parentIndices )
	{
		var descendants = new List<int>[parentIndices.Length];

		for ( int i = 0; i < descendants.Length; ++i )
			descendants[i] = new List<int>();

		for ( int mesh = 0; mesh < parentIndices.Length; ++mesh )
		{
			// Walking up rather than down, so a subtree of any depth lands on every ancestor.
			// The guard is against a parent chain that loops back on itself.
			var parent = parentIndices[mesh];

			for ( int guard = parentIndices.Length; guard > 0 && parent >= 0 && parent < descendants.Length; --guard )
			{
				descendants[parent].Add( mesh );
				parent = parentIndices[parent];
			}
		}

		return descendants.Select( list => list.ToArray() ).ToArray();
	}

	/// <summary>
	/// The frame a clip's movement is actually over on, which is not always the frame it ends on.
	///
	/// Every gate in the game swings by frame 50 and holds the pose at 60; jungle's and space's
	/// clips stop there, but hallow's carry on for another nine seconds - its M1 shuts the gate
	/// again and then swings it open the opposite way, ending on a pose M2 never starts from,
	/// which is the lurch that made the gate look broken.
	///
	/// A repeated keyframe is the animator holding a finished pose, so that is where a movement
	/// ends. A clip that never holds ends where it is back at the pose it rests in instead, which
	/// is how the closing half is found - hallow's M2 shuts the gate by frame 50 and then reopens
	/// it.
	/// </summary>
	private static int MotionEnd( AnimationFile animation, AnimationFile first )
	{
		var end = animation.LastFrame;

		foreach ( var track in animation.RotationTracks )
		{
			var rest = first.RotationTracks
				.FirstOrDefault( other => other.TargetIndex == track.TargetIndex )?.Rotations;

			for ( int key = 1; key < track.Rotations.Length; ++key )
			{
				var held = MathF.Abs( Quaternion.Dot( track.Rotations[key], track.Rotations[key - 1] ) ) >= SamePose;
				var atRest = rest is { Length: > 0 }
					&& MathF.Abs( Quaternion.Dot( track.Rotations[key], rest[0] ) ) >= SamePose;

				if ( !held && !atRest )
					continue;

				end = Math.Min( end, track.FrameIndices[key] );
				break;
			}
		}

		return end;
	}

	private float Duration( int index )
		=> Math.Max( _motionEnds[index] - _animations[index].FirstFrame, 1 ) / AnimationFile.FramesPerSecond;
}
