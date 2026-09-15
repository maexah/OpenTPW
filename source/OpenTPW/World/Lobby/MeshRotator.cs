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

	/// <param name="localTransforms">
	/// Each mesh's own transform, before its parents' - which is the frame a rotation key is
	/// authored in. See <see cref="BuildRestInverses"/>.
	/// </param>
	public MeshRotator( AnimationFile[] animations, ModelEntity[] entities, Matrix4x4[] baseTransforms,
		Matrix4x4[] localTransforms, Vector3[] offsets, int[] parentIndices )
	{
		_animations = animations.Take( ClipsUsed ).ToArray();
		_entities = entities;
		_baseTransforms = baseTransforms;
		_restInverses = BuildRestInverses( localTransforms );
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

		Pose( animation, animation.FirstFrame + (_elapsed * AnimationFile.FramesPerSecond) );
	}

	/// <summary>
	/// Turns this model's meshes to where <paramref name="animation"/> puts them at
	/// <paramref name="frame"/>, without touching this rotator's own playback - for a caller that
	/// sequences clips itself rather than looping through them. <see cref="MeshAnimator.Pose"/> is the
	/// same split, made for the same reason, and <b>the two have to be driven together</b>: one animation
	/// player poses every track of one clip, where a private clock each lets a model's morph half and its
	/// rotation half play different clips at the same time.
	///
	/// <para>
	/// <b>The frame is the clip's own, counted from nought.</b> An animation player counts frames from the
	/// start of whatever it is playing - see <c>AnimTimeControl.AnimFrame</c> - where <see cref="Update"/>
	/// counts from <see cref="AnimationFile.FirstFrame"/>. Those are the same number for every clip the
	/// game ships, because all of them declare nought, but they are not the same rule: the engine plays
	/// nought to the declared span, and the span a clip declares disagrees with the one its keys cover on
	/// 159 of them.
	/// </para>
	/// </summary>
	public void Pose( AnimationFile animation, float frame )
	{
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
	/// The inverse of each mesh's authored orientation <i>within its parent</i>.
	///
	/// A rotation keyframe is the orientation the mesh should be in, not a turn to add to the one
	/// it was already authored with. Every one of these animations opens on exactly the rotation
	/// its own mesh carries: the hallow gate's two halves are authored at 90 and 180 degrees and
	/// their first keyframes are 90 and 180 degrees, and the space gate's hatch is authored at
	/// 175 degrees and opens on 175. So the authored rotation has to come back out before the
	/// animated one goes in, or the mesh is turned twice - which laid hallow's doors flat into
	/// the ground and left space's hatch facing backwards.
	///
	/// <para>
	/// <b>The orientation a key replaces is the mesh's LOCAL one, not where it ends up in the
	/// model.</b> Every gate in the game parents its doors straight to a root that carries no
	/// rotation, where the two are the same matrix - so this class was written against the one
	/// family of models that cannot tell them apart, and taking the world orientation out was
	/// wrong everywhere else. It is wrong on 1,078 of the game's 2,592 rotation tracks.
	/// </para>
	///
	/// <para>
	/// The Jungle Spray is the case that shows it. Its three animal heads hang off a Bench which
	/// is itself turned a quarter turn, so each head's local orientation is square while its world
	/// orientation is that quarter turn; every clip keys them square, because square is what they
	/// are relative to the bench. Read as world orientations those keys flattened two of the three
	/// heads - the Lion and the Elephant, named by the two clips this class loops - and left the
	/// Eagle alone only because the clip naming it is never reached.
	/// </para>
	///
	/// <para>
	/// What settles it is the construction clip, which by definition ends on the built object: the
	/// Jungle Spray's ends with its fence keyed at 90 degrees, its guns at 180 and one puddle at
	/// 245, and those are the meshes' local orientations exactly, while disagreeing with every one
	/// of their world orientations.
	/// </para>
	/// </summary>
	private static Matrix4x4[] BuildRestInverses( Matrix4x4[] localTransforms )
	{
		var inverses = new Matrix4x4[localTransforms.Length];

		for ( int i = 0; i < localTransforms.Length; ++i )
		{
			// A sheared node has no rotation to take out - see LobbyModel.ToWorldSpace - so it
			// keeps the identity here and behaves exactly as it did before.
			inverses[i] = Matrix4x4.Decompose( localTransforms[i], out _, out var rotation, out _ )
				&& Matrix4x4.Invert( Matrix4x4.CreateFromQuaternion( rotation ), out var inverse )
					? inverse
					: Matrix4x4.Identity;
		}

		return inverses;
	}

	/// <summary>
	/// Puts back every mesh <paramref name="outgoing"/> turned, and everything those meshes carry, to the
	/// orientation the model was built with.
	///
	/// <para>
	/// This is the rotation arm of what the engine does whenever a model changes role
	/// (<c>FUN_00472310</c>): it walks the clip that is <i>leaving</i> and restores, from the model's
	/// master copy, every component that clip drove - the <c>0x289</c> arm of its restore mask. Without it
	/// the first clip looks right and every one after it keeps whatever the clip before left behind on any
	/// mesh it does not itself name.
	/// </para>
	///
	/// <para>
	/// <b>Visibility is deliberately not put back, because the engine does not put it back either.</b> That
	/// mask tests <c>0x289</c>, <c>0x1000</c> and <c>0x10000</c> and never <c>0x20000</c>, which is the
	/// visibility channel - so what a clip switched off stays off across a clip change and is shown again
	/// only by a later clip saying so. The engine un-hides exactly one thing on the way in, the outgoing
	/// clip's own hide list, which we do not read. That is what lets a built item keep the state its
	/// construction clip left it in - see <c>ParkObjects.PoseAsBuilt</c>, which is our stand-in for that
	/// list rather than a rival to it.
	/// </para>
	/// </summary>
	public void Rest( AnimationFile outgoing )
	{
		foreach ( var track in outgoing.RotationTracks )
		{
			var target = track.TargetIndex;

			if ( target < 0 || target >= _entities.Length )
				continue;

			_entities[target].LinearTransform = _baseTransforms[target];

			foreach ( var child in _descendants[target] )
				_entities[child].LinearTransform = _baseTransforms[child];
		}
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
