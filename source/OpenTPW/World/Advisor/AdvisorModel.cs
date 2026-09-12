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
/// <see cref="ModelFile.Node.Id"/>). Screenshots of the original settle the defaults: with no
/// costume on he has his antennae and gloves and nothing else, so the add-on pieces start hidden
/// and his own antennae and hands (19 to 22) start on - which the parks' costume lists fit, since
/// jungle's has to switch its pith helmet (id 4) on explicitly. That is what
/// <see cref="Dress"/> does. His spare mouths and closed eyelids start hidden too; the mouths are
/// switched one at a time as he talks.
/// </para>
/// </summary>
public sealed class AdvisorModel
{
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
			? (int)(Math.Max( animation.LastFrame - animation.FirstFrame, 0 ) * 1000 / AnimationFile.FramesPerSecond)
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
			var frame = MathF.Min( animation.FirstFrame + (seconds * AnimationFile.FramesPerSecond), animation.LastFrame );

			foreach ( var track in animation.RotationTracks )
			{
				var node = track.TargetIndex;

				if ( node < 0 || node >= _local.Length || track.Rotations.Length == 0 )
					continue;

				_local[node] = Turned( _restLocal[node], track.Sample( frame ) );
			}

			// A position key replaces where the node sits relative to its parent, as a rotation
			// key replaces its orientation. Clips 14 and 15 are the ones with these: they raise his
			// head and body from below the screen and drop them back down out of sight.
			foreach ( var track in animation.PositionTracks )
			{
				var node = track.TargetIndex;

				if ( node < 0 || node >= _local.Length )
					continue;

				var position = track.Sample( frame );
				_local[node].M41 = position.X;
				_local[node].M42 = position.Y;
				_local[node].M43 = position.Z;
			}

			// Visibility keys switch a node on or off from a frame on, and leave it that way - which
			// is how he blinks, swapping his eyes for his eyelids for four frames at a time.
			foreach ( var track in animation.VisibilityTracks )
			{
				if ( track.VisibleAt( frame ) is bool visible )
					SetMeshVisible( track.TargetIndex, visible );
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

		OpenEyes();
	}

	/// <summary>
	/// Puts his eyes back where they belong: both open, both eyelids away.
	///
	/// A clip's visibility keys are state that outlives the clip, and only the eyelids carry a key
	/// at frame 0 to re-establish themselves. The eye tracks' first key is the start of a blink -
	/// frame 10 in clip 8, frame 80 in clip 3 - and <see cref="AnimationFile.VisibilityTrack.VisibleAt"/>
	/// leaves a mesh alone before a track's first key. So a line cut off inside a blink's four
	/// frames leaves his eyes hidden, the next line inherits that, and nothing shows them again
	/// until its first blink: up to 2.7 seconds of him looking out with no eyes at all.
	///
	/// Every clip's eye track ends on a key that shows them, which is why this only ever bites a
	/// line that was interrupted rather than one that played out.
	/// </summary>
	public void OpenEyes()
	{
		SetMeshVisible( MeshNamed( "right eye" ), true );
		SetMeshVisible( MeshNamed( "left eye" ), true );

		SetMeshVisible( MeshNamed( "shuteye r" ), false );
		SetMeshVisible( MeshNamed( "shuteye l" ), false );
	}

	/// <summary>Draws him over whatever is already on the screen - see <see cref="Level.Render"/>.</summary>
	public void Draw()
	{
		var projection = ScreenProjection();

		// Every solid part before any see-through one. His head and body are see-through - flat discs
		// with the ball painted on, under material flag 0x2 - so drawn a mesh at a time his head went
		// down before his hands and a hand swung behind it showed straight through. Drawn after them,
		// it covers whatever is behind it.
		//
		// The see-through parts now write depth like everything else, and his face survives it because
		// nothing of his is coplanar: his mouths stand 2.01 model units clear of the head disc and the
		// bowler's tie 0.96 clear of the body, which through his orthographic screen projection is
		// thousands of depth values apart, not a rounding error. The one pair that does share a plane,
		// an eyelid and the eye beneath it, is never drawn at once - a blink swaps one for the other.
		foreach ( var entity in _model.Entities )
			entity.DrawOverlay( Matrix4x4.Identity, projection, LightPosition, LightColor, Ambient, worldNormals: true );

		foreach ( var entity in _model.Entities )
			entity.DrawOverlay( Matrix4x4.Identity, projection, LightPosition, LightColor, Ambient, worldNormals: true, translucent: true );
	}

	/// <summary>
	/// How he is lit. The original gives him a light of his own, at (26.67, -26.67, 800) with a
	/// colour of one (0x00429ba0), but what space those numbers are in is not established. So these
	/// are set against screenshots of the original instead, by measurement. There his gloves sit at
	/// a median brightness of 220 to 240 out of 255, brighter than their texture - White.wct
	/// averages 210, with its creases painted in down to 98 - so he is lit past his textures' own
	/// colours, and the ambient term alone has to carry the gloves' shaded side. At an ambient of
	/// 0.6 they came out at 115, and at 1.0 at 155. His eyes saturate either way, and his head's
	/// shine is in its texture. The light sits in front of him, up and to his right as the viewer
	/// sees it.
	/// </summary>
	private static readonly Vector3 LightPosition = new( -150f, -400f, 250f );

	private static readonly Vector3 LightColor = new( 0.3f, 0.3f, 0.3f );

	private const float Ambient = 1.4f;

	/// <summary>
	/// Model to screen: the original's root scale and on-screen translation, as one matrix. Our
	/// world axes are the file's with Y and Z swapped - see <see cref="LobbyModel.ToWorldSpace"/> -
	/// so world X runs across, world Z up, and world Y into the screen, with his face towards -Y.
	/// </summary>
	/// <para>
	/// He stands on the interface rather than on the window. The original puts him at 0.6, -0.6 of
	/// the screen - eight tenths of the way across it and two tenths up from the bottom - and on a
	/// 4:3 window this puts him in exactly that place at exactly that size. On a window of any other
	/// shape the interface is a 2048x1536 screen pinned inside it (see <see cref="UI.VirtualScreen"/>),
	/// and eight tenths across the window is no longer eight tenths across the interface: on a 21:9
	/// window it would walk him a third of the way into the player slots he is talking about, and
	/// further in the wider the window. So where he stands and how big he is are both taken from the
	/// virtual screen, which on a 4:3 window is the window.
	/// </para>
	public static Matrix4x4 ScreenProjection()
	{
		// Where the original has him, on the interface's own screen.
		const float across = 0.8f * UI.VirtualScreen.Width;
		const float down = 0.8f * UI.VirtualScreen.Height;

		// A model unit was 0.015 of half the window's height, which on that screen is 11.52 units.
		const float unit = 0.015f * UI.VirtualScreen.Height / 2f;

		var scale = UI.VirtualScreen.Scale;
		var up = unit * scale * 2f / Screen.Height;
		var sideways = unit * scale * 2f / Screen.Width;

		var x = ((UI.VirtualScreen.Offset( UI.Anchor.Right ) + (across * scale)) * 2f / Screen.Width) - 1f;
		var y = 1f - ((UI.VirtualScreen.OffsetDown( UI.VerticalAnchor.Bottom ) + (down * scale)) * 2f / Screen.Height);

		return new Matrix4x4(
			sideways, 0f, 0f, 0f,
			0f, 0f, 0.001f, 0f,
			0f, up, 0f, 0f,
			x, y, 0.2f, 1f );
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
