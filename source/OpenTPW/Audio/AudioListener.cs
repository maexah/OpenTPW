namespace OpenTPW;

/// <summary>
/// Where the game is being heard from, and which way round the ears are.
///
/// The original had a real one: it resolves QMixer's SetListenerPosition, SetListenerOrientation and
/// SetListenerVelocity out of QMixer.dll, so its sound was placed against a moving head rather than
/// mixed flat. This is the same idea with far less behind it - a position and the direction of the
/// listener's right ear, which is all a stereo pan needs.
///
/// Engine, not content: what is heard from here, and where from, belongs to whoever plays it.
/// </summary>
internal readonly record struct AudioListener( Vector3 Position, Vector3 Right )
{
	/// <summary>
	/// A listener standing at <paramref name="position"/> looking along <paramref name="forward"/>,
	/// with its ears level.
	///
	/// <para>
	/// The right ear is worked out here rather than taken from the camera's rotation, because
	/// <c>Rotation.Right</c> cannot be used for this. <see cref="Rotation.LookAt"/> builds the
	/// shortest turn from the world's forward onto a direction and says nothing about roll, so the
	/// basis it hands back is rolled by whatever that turn happened to leave: around the lobby's own
	/// orbit its right ear tilts as far as 0.79 out of the horizontal, and at one angle it points the
	/// opposite way to the camera's actual right. None of that shows in the picture, because the view
	/// matrix is built by <c>CreateLookTo</c> against the world's up and squares its own basis up - so
	/// it would have been wrong in the sound alone, and wrong in a way that still sounds like working
	/// positional audio.
	/// </para>
	/// <para>
	/// Forward crossed with up is this world's own convention, not a choice: (1,0,0) x (0,0,1) is
	/// (0,-1,0), which is <see cref="Vector3.Right"/>.
	/// </para>
	/// </summary>
	public static AudioListener Facing( Vector3 position, Vector3 forward )
	{
		var right = Vector3.Cross( forward, Vector3.Up );

		// Looking straight up or straight down, there is no level right ear to find - the two vectors
		// are parallel and the cross product collapses. The world's own right stands in, rather than
		// the zero vector, which would put every sound dead centre without saying why.
		return new AudioListener( position, right.Length <= float.Epsilon ? Vector3.Right : right.Normal );
	}

	/// <summary>
	/// Where <paramref name="source"/> sits across the stereo field: -1 hard left, 0 straight ahead
	/// or behind, +1 hard right.
	///
	/// Two speakers cannot say front from back - a source directly ahead and one directly behind both
	/// dot to zero - and nothing here pretends otherwise. The original bought that distinction with
	/// QSound's own processing, which is not something a pan can reproduce.
	/// </summary>
	public float PanTo( Vector3 source )
	{
		var toSource = source - Position;
		var distance = toSource.Length;

		// Standing on top of the source there is no direction to pan it by, and normalising would be a
		// divide by zero.
		if ( distance <= float.Epsilon )
			return 0f;

		return Vector3.Dot( toSource / distance, Right ).Clamp( -1f, 1f );
	}
}
