namespace OpenTPW;

/// <summary>
/// Plays vertex animations onto a single mesh.
///
/// An animation file has one channel per vertex of the mesh it drives (plus two trailing
/// channels that aren't vertices), and each keyframe value is that vertex's position for that
/// frame, quantised into the mesh's bounding box - see <see cref="AnimationFile"/>. Channels
/// the animation doesn't move still carry a single rest keyframe, so sampling every channel
/// reproduces the whole mesh.
///
/// A model can have several animations - the jungle island's Dino has two, a small jaw
/// movement and a much larger head sweep. Nothing in the files says how they are sequenced,
/// so they are simply played one after another on a loop.
/// </summary>
public class MeshAnimator
{
	/// <summary>
	/// Keyframe indices are authoring frames. The lobby animations span 0..100 and 0..200,
	/// which at 25fps are a 4 and an 8 second loop.
	/// </summary>
	public const float FramesPerSecond = 25f;

	private readonly AnimationFile[] _animations;
	private readonly ModelFile.Mesh _mesh;
	private readonly Model _model;

	// Rebuilt each frame and re-uploaded; seeded from the mesh's rest pose so anything the
	// animation doesn't touch keeps its original normal, UV, texture index and flags.
	private readonly Vertex[] _vertices;
	private readonly Vector3[] _positions;

	// ModelFile only keeps the reordered vertex array, so recover the source positions by
	// inverting that mapping: Vertices[i] is source vertex VertexOrder[i].
	private readonly Vector3[] _restPositions;

	private int _current;
	private float _elapsed;

	public MeshAnimator( AnimationFile[] animations, ModelFile.Mesh mesh, Model model, Vertex[] restVertices )
	{
		_animations = animations;
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
	}

	/// <summary>
	/// True when this animation's channel count matches the mesh it is supposed to drive.
	/// </summary>
	public static bool Drives( AnimationFile animation, ModelFile.Mesh mesh )
		=> animation.ChannelCount == mesh.VertexCount + 2;

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

		var frame = animation.FirstFrame + (_elapsed * FramesPerSecond);

		for ( int v = 0; v < _positions.Length; ++v )
			_positions[v] = SamplePosition( animation, v, frame );

		var order = _mesh.VertexOrder;
		for ( int i = 0; i < _vertices.Length && i < order.Length; ++i )
		{
			var p = _positions[order[i]];

			// Same swizzle the mesh itself is built with in LobbyIsland.
			_vertices[i].Position = new Vector3( p.X, p.Z, p.Y );
		}

		_model.UpdateVertices( _vertices );
	}

	private static float Duration( AnimationFile animation )
		=> Math.Max( animation.LastFrame - animation.FirstFrame, 1 ) / FramesPerSecond;

	private Vector3 SamplePosition( AnimationFile animation, int channelId, float frame )
	{
		if ( !animation.TryGetChannel( channelId, out var track, out var slot ) || track == null )
			return _restPositions[channelId];

		var frames = track.FrameIndices;

		if ( track.IsConstant || frame <= frames[0] )
			return Decode( track.Raw( 0, slot ) );

		if ( frame >= frames[^1] )
			return Decode( track.Raw( frames.Length - 1, slot ) );

		var hi = 1;
		while ( hi < frames.Length && frames[hi] < frame )
			hi++;

		var lo = hi - 1;
		var span = frames[hi] - frames[lo];
		var t = span <= 0 ? 0f : (frame - frames[lo]) / span;

		// Positions are dequantised before interpolating, so a keyframe pair straddling the
		// quantisation grid still blends smoothly.
		var a = Decode( track.Raw( lo, slot ) );
		var b = Decode( track.Raw( hi, slot ) );
		return a + ((b - a) * t);
	}

	private Vector3 Decode( uint raw )
		=> AnimationFile.DecodePosition( raw, _mesh.BoundsMin, _mesh.BoundsMax );
}
