using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The advisor himself: his model, posed from his animations, drawn on the screen rather than in
/// the world.
///
/// <para>
/// <b>What he is.</b> data\global\advisor.wad's Advisor.md2 is a bug - a head on a root node
/// (25), a head pivot (26) carrying his eyes, five mouths, antennae and every hat, and two arm
/// pivots (27, 28) carrying his hands. Advisorm1 to Advisorm15 animate him. Three of the nodes his
/// animations turn have no geometry of their own, which is why this composes the node tree itself
/// rather than using <see cref="MeshRotator"/>, which only turns meshes.
/// </para>
/// <para>
/// <b>Where he is drawn.</b> The original creates him (0x00429ba0) with a transform of its own,
/// translate (0.6, -0.6, 0.2), and scales his root node's axes by 0.01125 across, 0.001 in depth
/// and 0.015 up. At 640x480 those give 3.6 pixels per unit in both directions - square pixels in
/// normalised screen space, which only works out for a model drawn straight onto the screen. So
/// he sits in the lower right at a fixed size relative to the screen, flattened in depth so
/// nothing in the scene can cut into him. <see cref="ScreenProjection"/> is that transform, with
/// the horizontal scale following the window's aspect instead of assuming 4:3.
/// </para>
/// <para>
/// <b>What he wears.</b> Every mesh ships visible - no node in the file carries the engine's
/// hidden bit, 0x10 - so what he wears is decided at runtime by costume ids (see
/// <see cref="ModelFile.Node.Id"/>). The parks' Standard.sam files say which way the defaults run:
/// jungle's advisor has to add its pith helmet (id 4) and take away an antenna (id 19), so the
/// add-on pieces start hidden and his own antennae and hands (19 to 22) start on. That is what
/// <see cref="Dress"/> does. His spare mouths and closed eyelids start hidden too; the mouths are
/// switched one at a time as he talks.
/// </para>
/// </summary>
public sealed class AdvisorModel
{
	/// <summary>
	/// The engine plays .md2 animations at thirty frames a second: it advances an animation by
	/// elapsed milliseconds times 0.03 (0x00472f60), and works out a clip's length as frames * 1000
	/// / 30 (0x00474070).
	/// </summary>
	public const float FramesPerSecond = 30f;

	/// <summary>The lowest costume id that is part of him rather than something he puts on.</summary>
	private const int FirstOwnPartId = 19;

	/// <summary>
	/// His mouths, in the engine's numbering: shape 1 is his mouth at rest, 2 to 5 the ones he
	/// talks with. It finds them by name, ignoring case (0x0044b2e0).
	/// </summary>
	private static readonly string[] MouthNames =
		{ "mouth - normal", "mouth - aah", "mouth - eee", "mouth - ooh", "mouth - sss" };

	private readonly LobbyModel _model;
	private readonly ModelFile _file;
	private readonly AnimationFile?[] _clips;

	private readonly Matrix4x4[] _restLocal;
	private readonly Matrix4x4[] _local;
	private readonly Matrix4x4[] _world;
	private readonly int[] _parents;
	private readonly int[] _parentsFirst;

	private readonly int[] _mouths;

	public AdvisorModel( string modelPath, string textureDirectory )
	{
		_model = new LobbyModel( modelPath, textureDirectory, Vector3.Zero );
		_file = new ModelFile( modelPath );

		foreach ( var entity in _model.Entities )
			entity.DrawnByOwner = true;

		var stem = modelPath[..modelPath.LastIndexOf( '.' )];
		var clips = new List<AnimationFile?> { null };

		for ( int number = 1; AnimationFile.TryLoad( $"{stem}M{number}.md2", out var clip ); ++number )
			clips.Add( clip );

		_clips = [.. clips];

		var nodeCount = _file.Nodes.Count;
		_restLocal = _file.Nodes.Select( node => node.LocalTransform ).ToArray();
		_local = (Matrix4x4[])_restLocal.Clone();
		_world = new Matrix4x4[nodeCount];
		_parents = _file.Nodes.Select( node => node.ParentIndex ).ToArray();
		_parentsFirst = ParentsFirst( _parents );

		_mouths = MouthNames.Select( MeshNamed ).ToArray();

		Dress();
		ShowMouth( 1 );
		Pose( 0, 0f );

		Log.Info( $"{modelPath}: advisor with {_file.Meshes.Count} meshes, {nodeCount} nodes, {_clips.Length - 1} clips" );
	}

	/// <summary>How many animations he has, numbered from 1 as the engine numbers them.</summary>
	public int ClipCount => _clips.Length - 1;

	/// <summary>How long clip <paramref name="clip"/> plays for, in the engine's whole milliseconds.</summary>
	public int ClipMilliseconds( int clip )
		=> clip >= 1 && clip < _clips.Length && _clips[clip] is { } animation
			? Math.Max( animation.LastFrame - animation.FirstFrame, 0 ) * 1000 / 30
			: 0;

	/// <summary>
	/// Poses him <paramref name="seconds"/> into clip <paramref name="clip"/>, holding the last
	/// frame past its end. Clip 0 is his rest pose.
	/// </summary>
	public void Pose( int clip, float seconds )
	{
		var animation = clip >= 1 && clip < _clips.Length ? _clips[clip] : null;

		Array.Copy( _restLocal, _local, _local.Length );

		if ( animation != null )
		{
			var frame = MathF.Min( animation.FirstFrame + (seconds * FramesPerSecond), animation.LastFrame );

			foreach ( var track in animation.RotationTracks )
			{
				var node = track.TargetIndex;

				if ( node < 0 || node >= _local.Length || track.Rotations.Length == 0 )
					continue;

				_local[node] = Turned( _restLocal[node], track.Sample( frame ) );
			}

			foreach ( var animator in _model.Animators )
				animator.Pose( animation, frame );
		}

		Place();
	}

	/// <summary>Shows mouth <paramref name="shape"/> (1 to 5) and hides the others.</summary>
	public void ShowMouth( int shape )
	{
		for ( int i = 0; i < _mouths.Length; ++i )
			SetMeshVisible( _mouths[i], i == shape - 1 );
	}

	/// <summary>
	/// The costume the front end gives him: none. Add-on pieces hidden, his own parts shown, eyes
	/// open. See the class remarks.
	/// </summary>
	public void Dress()
	{
		for ( int node = 0; node < _file.Meshes.Count && node < _file.Nodes.Count; ++node )
		{
			var id = _file.Nodes[node].Id;

			if ( id > 0 )
				SetMeshVisible( node, id >= FirstOwnPartId );
		}

		SetMeshVisible( MeshNamed( "shuteye r" ), false );
		SetMeshVisible( MeshNamed( "shuteye l" ), false );
	}

	/// <summary>Draws him over whatever is already on the screen - see <see cref="Level.Render"/>.</summary>
	public void Draw( float aspect )
	{
		var projection = ScreenProjection( aspect );

		foreach ( var entity in _model.Entities )
			entity.DrawOverlay( Matrix4x4.Identity, projection, LightPosition, Vector3.One );
	}

	/// <summary>
	/// Where his light is, in the space he is drawn in. The original gives him a light of his own
	/// at (26.67, -26.67, 800) with a colour of one, but what space those numbers are in is not
	/// established, so this is placed in front of him and a little to the right and above - which
	/// is where a light for a face on the screen would have to be for his face to be lit at all.
	/// </summary>
	private static readonly Vector3 LightPosition = new( 26.67f, -800f, 26.67f );

	/// <summary>
	/// Model to screen: the original's root scale and on-screen translation, as one matrix. Our
	/// world axes are the file's with Y and Z swapped - see <see cref="LobbyModel.ToWorldSpace"/> -
	/// so world X runs across, world Z up, and world Y into the screen, with his face towards -Y.
	/// </summary>
	public static Matrix4x4 ScreenProjection( float aspect )
	{
		const float up = 0.015f;
		var across = up / MathF.Max( aspect, 0.01f );

		return new Matrix4x4(
			across, 0f, 0f, 0f,
			0f, 0f, 0.001f, 0f,
			0f, up, 0f, 0f,
			0.6f, -0.6f, 0.2f, 1f );
	}

	/// <summary>
	/// A node with its orientation replaced by <paramref name="rotation"/>, keeping its own scale
	/// and position. A rotation keyframe is the orientation a node should hold outright, not a turn
	/// to add to the one it was authored with - see <see cref="MeshRotator"/> - and his arm pivots
	/// show it: they are authored a quarter turn about X, and every clip keys them at a quarter
	/// turn about X when they are at rest.
	/// </summary>
	private static Matrix4x4 Turned( Matrix4x4 rest, Quaternion rotation )
	{
		var translation = Matrix4x4.CreateTranslation( rest.Translation );
		var turn = Matrix4x4.CreateFromQuaternion( Quaternion.Normalize( rotation ) );

		return Matrix4x4.Decompose( rest, out var scale, out _, out _ ) && scale.X > 0f && scale.Y > 0f && scale.Z > 0f
			? Matrix4x4.CreateScale( scale ) * turn * translation
			: turn * translation;
	}

	/// <summary>Composes the node tree and hands each mesh its place, the way LobbyModel builds them.</summary>
	private void Place()
	{
		foreach ( var node in _parentsFirst )
		{
			var parent = _parents[node];
			_world[node] = parent >= 0 && parent < _world.Length ? _local[node] * _world[parent] : _local[node];
		}

		for ( int i = 0; i < _model.Entities.Length && i < _world.Length; ++i )
		{
			var world = _world[i];
			_model.Entities[i].LinearTransform = LobbyModel.ToWorldSpace( world );
			_model.Entities[i].Position = new Vector3( world.M41, world.M43, world.M42 );
		}
	}

	private int MeshNamed( string name )
	{
		for ( int i = 0; i < _file.Meshes.Count; ++i )
		{
			if ( string.Equals( _file.Meshes[i].Name.TrimEnd( '\0' ).Trim(), name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	private void SetMeshVisible( int mesh, bool visible )
	{
		if ( mesh >= 0 && mesh < _model.Entities.Length )
			_model.Entities[mesh].Opacity = visible ? 1f : 0f;
	}

	/// <summary>Every node after its parent, so one pass composes the whole tree.</summary>
	private static int[] ParentsFirst( int[] parents )
	{
		var order = new List<int>( parents.Length );
		var placed = new bool[parents.Length];

		void Visit( int node, int guard )
		{
			if ( placed[node] || guard <= 0 )
				return;

			var parent = parents[node];
			if ( parent >= 0 && parent < parents.Length && parent != node )
				Visit( parent, guard - 1 );

			if ( !placed[node] )
			{
				placed[node] = true;
				order.Add( node );
			}
		}

		for ( int node = 0; node < parents.Length; ++node )
			Visit( node, parents.Length );

		return [.. order];
	}
}
