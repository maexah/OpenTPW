namespace OpenTPW;

/// <summary>
/// Plays a model's animations onto one of its meshes - vertex morph, UV scroll, or both.
///
/// A morph track has one channel per vertex of the mesh it names (plus two trailing channels
/// that aren't vertices), and each keyframe value is that vertex's position for that frame,
/// quantised into the mesh's bounding box - see <see cref="AnimationFile"/>. Channels the
/// animation doesn't move still carry a rest keyframe, so sampling every channel reproduces
/// the whole mesh.
///
/// One animation can morph several meshes at once - ratraceM1 morphs four - so there is one of
/// these per target mesh, each picking out its own track by index. An animation that doesn't
/// move this particular mesh leaves it in its rest pose.
///
/// A mesh can carry both kinds at once - a fountain's water morphs while its texture scrolls -
/// so both are sampled here and uploaded together, rather than by two animators fighting over
/// the same vertex buffer.
///
/// A model can have several animations - the jungle island's Dino has two, a small jaw movement
/// and a much larger head sweep. Nothing in the files says how they are sequenced, so they are
/// simply played one after another on a loop.
/// </summary>
public class MeshAnimator
{
	/// <summary>
	/// Keyframe indices are authoring frames. The lobby animations span 0..100 and 0..200,
	/// which at 25fps are a 4 and an 8 second loop.
	/// </summary>
	public const float FramesPerSecond = 25f;

	private readonly AnimationFile[] _animations;
	private readonly int _targetIndex;
	private readonly ModelFile.Mesh _mesh;
	private readonly Model _model;

	// Rebuilt each frame and re-uploaded; seeded from the mesh's rest pose so anything the
	// animation doesn't touch keeps its original normal, UV, texture index and flags.
	private readonly Vertex[] _vertices;
	private readonly Vector3[] _positions;

	// ModelFile only keeps the reordered vertex array, so recover the source positions by
	// inverting that mapping: Vertices[i] is source vertex VertexOrder[i].
	private readonly Vector3[] _restPositions;

	// UV animation slides texture coordinates rather than vertices, so the rest UVs are kept
	// to restore from and to leave untouched whatever the animation doesn't name.
	private readonly System.Numerics.Vector2[] _restUvs;

	private int _current;
	private float _elapsed;
	private bool _atRest;

	public MeshAnimator( AnimationFile[] animations, int targetIndex, ModelFile.Mesh mesh,
		Model model, Vertex[] restVertices )
	{
		_animations = animations;
		_targetIndex = targetIndex;
		_mesh = mesh;
		_model = model;
		_vertices = (Vertex[])restVertices.Clone();
		_positions = new Vector3[mesh.VertexCount];

		_restPositions = new Vector3[mesh.VertexCount];
		for ( int i = 0; i < mesh.Vertices.Length && i < mesh.VertexOrder.Length; ++i )
		{
			var source = mesh.VertexOrder[i];
			if ( source < _restPositions.Length )
				_restPositions[source] = mesh.Vertices[i].Position;
		}

		_restUvs = new System.Numerics.Vector2[restVertices.Length];
		for ( int i = 0; i < restVertices.Length; ++i )
			_restUvs[i] = restVertices[i].TexCoords;
	}

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

		var morph = animation.MorphTrackFor( _targetIndex );
		var uv = animation.UvTrackFor( _targetIndex );

		// A morph track has one channel per vertex of its mesh plus two. Where that doesn't
		// hold the target index landed on a node that isn't this mesh (a handful of models
		// have more nodes than meshes), and morphing anyway would scramble the geometry.
		if ( morph != null && morph.ChannelCount != _mesh.VertexCount + 2 )
			morph = null;

		if ( morph == null && uv == null )
		{
			// This animation doesn't touch our mesh. Put it back once, then leave it alone.
			if ( !_atRest )
				RestorePose();

			return;
		}

		_atRest = false;

		var frame = animation.FirstFrame + (_elapsed * FramesPerSecond);

		if ( morph != null )
		{
			for ( int v = 0; v < _positions.Length; ++v )
				_positions[v] = SamplePosition( morph, v, frame );

			WritePositions( _positions );
		}

		if ( uv != null )
			WriteUvs( uv, frame );

		_model.UpdateVertices( _vertices );
	}

	/// <summary>
	/// Slides the UV components this track names from their start value to their end value,
	/// each on its own end frame. Components are two per coordinate, so component c is the
	/// U of vertex c/2 when c is even and the V when it's odd.
	/// </summary>
	private void WriteUvs( AnimationFile.UvTrack track, float frame )
	{
		for ( int e = 0; e < track.EntryCount; ++e )
		{
			var endFrame = track.EndFrame[e];
			var t = endFrame <= 0 ? 1f : Math.Clamp( frame / endFrame, 0f, 1f );

			var count = track.ComponentCount[e];
			var values = track.ValueOffset[e];

			for ( int k = 0; k < count; ++k )
			{
				var component = track.FirstComponent[e] + k;
				var slot = component / 2;

				if ( slot >= _vertices.Length )
					continue;

				var start = track.Values[values + k];
				var end = track.Values[values + count + k];
				var value = start + ((end - start) * t);

				var uv = _vertices[slot].TexCoords;

				if ( (component & 1) == 0 )
					uv.X = value;
				else
					uv.Y = value;

				_vertices[slot].TexCoords = uv;
			}
		}
	}

	private void RestorePose()
	{
		WritePositions( _restPositions );

		for ( int i = 0; i < _vertices.Length; ++i )
			_vertices[i].TexCoords = _restUvs[i];

		_model.UpdateVertices( _vertices );
		_atRest = true;
	}

	private void WritePositions( Vector3[] positions )
	{
		var order = _mesh.VertexOrder;
		for ( int i = 0; i < _vertices.Length && i < order.Length; ++i )
		{
			var p = positions[order[i]];

			// Same swizzle the mesh itself is built with in LobbyModel.
			_vertices[i].Position = new Vector3( p.X, p.Z, p.Y );
		}
	}

	private static float Duration( AnimationFile animation )
		=> Math.Max( animation.LastFrame - animation.FirstFrame, 1 ) / FramesPerSecond;

	private Vector3 SamplePosition( AnimationFile.MorphTrack track, int channelId, float frame )
	{
		if ( !track.TryGetChannel( channelId, out var record, out var slot ) || record == null )
			return _restPositions[channelId];

		var frames = record.FrameIndices;

		if ( record.IsConstant || frame <= frames[0] )
			return Decode( record.Raw( 0, slot ) );

		if ( frame >= frames[^1] )
			return Decode( record.Raw( frames.Length - 1, slot ) );

		var hi = 1;
		while ( hi < frames.Length && frames[hi] < frame )
			hi++;

		var lo = hi - 1;
		var span = frames[hi] - frames[lo];
		var t = span <= 0 ? 0f : (frame - frames[lo]) / span;

		// Positions are dequantised before interpolating, so a keyframe pair straddling the
		// quantisation grid still blends smoothly.
		var a = Decode( record.Raw( lo, slot ) );
		var b = Decode( record.Raw( hi, slot ) );
		return a + ((b - a) * t);
	}

	private Vector3 Decode( uint raw )
		=> AnimationFile.DecodePosition( raw, _mesh.BoundsMin, _mesh.BoundsMax );
}
