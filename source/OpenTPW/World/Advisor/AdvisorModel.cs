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

	/// <summary>The node <c>Ticket2</c>, Lost Kingdom's golden ticket (-1 where he has none), and every node that is it or hangs from it.</summary>
	private readonly int _ticket;
	private readonly bool[] _onTicket;

	/// <summary>The ticket's x axis at rest and at the longest any clip's scale track makes it.</summary>
	private readonly float _ticketRest;
	private readonly float _ticketFull;

	private readonly int[] _mouths;

	/// <param name="sharedTextureDirectory">
	/// Where a texture the model names and its own wad does not ship is looked for: a theme's advisor wears
	/// the global advisor's face and gloves.
	/// </param>
	public AdvisorModel( string modelPath, string textureDirectory, string? sharedTextureDirectory = null )
	{
		_model = new LobbyModel( modelPath, textureDirectory, Vector3.Zero, sharedTextureDirectory: sharedTextureDirectory,
			vertexNormals: FileNormal );
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

		(_ticket, _ticketRest, _ticketFull) = TicketOf( _file, _clips.OfType<AnimationFile>() );
		_onTicket = OnTicketNodes( _parents, _parentsFirst, _ticket );

		Dress();
		ShowMouth( 1 );
		Pose( 0, 0f );

		Log.Info( $"{modelPath}: advisor with {_file.Meshes.Count} meshes, {nodeCount} nodes, {_clips.Length - 1} clips" );
	}

	/// <summary>How many animations he has, numbered from 1 as the engine numbers them.</summary>
	/// Dead by CODE: nothing calls it.
	public int ClipCount => _clips.Length - 1;

	/// <summary>
	/// How long clip <paramref name="clip"/> plays for, in the engine's whole milliseconds: the span the clip declares
	/// times 1000 over 30 (Anim_ClipMilliseconds, 0x00474070), not the span its keys cover. Lost Kingdom's clip 20 declares 60 frames and
	/// keys 89, so it lasts 2000 ms (the original's own table, q707/orig/O).
	/// </summary>
	public int ClipMilliseconds( int clip )
		=> clip >= 1 && clip < _clips.Length && _clips[clip] is { } animation ? MillisecondsOf( animation ) : 0;

	/// <summary>A clip's length as <see cref="ClipMilliseconds"/> counts it: its declared span, in whole milliseconds.</summary>
	internal static int MillisecondsOf( AnimationFile animation )
		=> (int)(Math.Max( animation.DeclaredLastFrame - animation.DeclaredFirstFrame, 0 ) * 1000 / AnimationFile.FramesPerSecond);

	/// <summary>The clip last started on him, whose hide list the next start shows again; nought before the first.</summary>
	private int _startedClip;

	/// <summary>
	/// A clip's start: the nodes the clip before it hid are shown again, and so is every node the incoming clip's tracks
	/// name, then the incoming clip's hide list is hidden. So the small ticket, which clips 1 to 16 hide and clip 17 does
	/// not, shows on clip 17, and the head that clips 19 and 20 leave hidden comes back with the next clip that moves it
	/// (clip 14's and 17's position keys).
	/// <para>
	/// Not the original's rule: the model's clip change, FUN_00472f60, calls FUN_00472310 with the OUTGOING clip (0x004730a9),
	/// which shows the nodes that clip's tracks name, and every node when the animation object's +4 holds 0x8 (not read for
	/// him); then FUN_00472d70 hides the INCOMING clip's list (0x00473117). FileFormats models.md, "Starting a clip", and
	/// docs/exe/scenes.md, "A clip's start", hold it. No Lost Kingdom frame is known to differ, and by the outgoing rule
	/// alone his head would stay hidden 633 ms where the original's film shows it, so what shows it there is unsettled.
	/// </para>
	/// </summary>
	internal void StartClip( int clip )
	{
		if ( _startedClip >= 1 && _startedClip < _clips.Length && _clips[_startedClip] is { } outgoing )
		{
			// The lengths the outgoing clip's scale tracks left, beside advnodes.py's lines from the original.
			if ( outgoing.ScaleTracks.Count > 0 )
				Log.Info( $"Advisor: clip {_startedClip} leaves " + string.Join( ", ", outgoing.ScaleTracks.Select( track => track.TargetIndex )
					.Where( node => node >= 0 && node < _local.Length )
					.Select( node => $"{NodeName( node )} at {AxisLengths( _local[node] ):0.000} t {_local[node].Translation:0.000}" ) ) );

			if ( outgoing.ScaleTracks.Any( track => track.TargetIndex == _ticket ) )
			{
				var (across, down) = TicketMove( TicketGrowth );
				Log.Info( $"Advisor: the ticket {TicketGrowth:0.000} grown, drawn {across + 0f:0} px across and {down + 0f:0} px down from his pin, "
					+ $"window {Screen.Width:0}x{Screen.Height:0}" );
			}

			foreach ( var node in outgoing.HideList )
				SetMeshVisible( node, true );
		}

		if ( clip >= 1 && clip < _clips.Length && _clips[clip] is { } incoming )
		{
			foreach ( var node in incoming.RotationTracks.Select( track => track.TargetIndex )
				.Concat( incoming.PositionTracks.Select( track => track.TargetIndex ) )
				.Concat( incoming.VisibilityTracks.Select( track => track.TargetIndex ) )
				.Concat( incoming.MorphTracks.Select( track => track.TargetIndex ) ) )
				SetMeshVisible( node, true );

			foreach ( var node in incoming.HideList )
				SetMeshVisible( node, false );
		}

		_startedClip = clip;
	}

	/// <summary>
	/// Advisor_ClipStarted (0x00598dc0), after a clip's start: his five mouths hidden for the update to show one, the
	/// line's two props put on, and with the clip word's <c>0x10000</c> no add-on shown and his head, the node
	/// named <c>bug head</c> (node 0 where there is none, FUN_0044b2e0), hidden too.
	/// </summary>
	internal void DressForClip( int first, int second, IReadOnlyList<(int AddOn, int SubOff)>? costume, bool faceAway )
	{
		foreach ( var mouth in _mouths )
			SetMeshVisible( mouth, false );

		AdvisorProps.Wear( _file, [], first, second, costume, SetMeshVisible, addOnsHidden: faceAway );

		if ( faceAway )
			SetMeshVisible( Math.Max( MeshNamed( "bug head" ), 0 ), false );
	}

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

			PoseNodes( animation, frame, _restLocal, _local );

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
				SetMeshVisible( node, id >= AdvisorProps.FirstOwnPart );
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

	/// <summary>
	/// Draws him over whatever is already on the screen - see <see cref="Level.Render"/> - lit per vertex by
	/// <paramref name="light"/> and a white light of <paramref name="strength"/> (<see cref="LightFor"/>, <see cref="VertexLight"/>).
	/// A material the file marks unlit (Lost Kingdom's mouths and tickets) takes white whatever the light, as the original's
	/// DrawList_AddMaterialFaces 0x00580530 gives it. <b>A deviation:</b> his textures are eight bits a channel here (Q616),
	/// so a fully lit glove is its texel's 231 where the original's four-bit texture shows 239.
	/// </summary>
	public void Draw( ParkLight light, float strength )
	{
		var white = new Vector3( strength );

		var projection = ScreenProjection();
		var ticketProjection = TicketProjection( projection, TicketGrowth );

		// Every solid part before any see-through one. His head and body are see-through - flat discs
		// with the ball painted on, under material flag 0x2 - so drawn a mesh at a time his head would go
		// down before his hands and a hand swung behind it would show straight through. Drawn after them,
		// it covers whatever is behind it.
		//
		// The see-through parts write depth like everything else, and his face survives it because
		// nothing of his is coplanar: his mouths stand 2.01 model units clear of the head disc and the
		// bowler's tie 0.96 clear of the body, which through his orthographic screen projection is
		// thousands of depth values apart, not a rounding error. The one pair that does share a plane,
		// an eyelid and the eye beneath it, is never drawn at once - a blink swaps one for the other.
		for ( int i = 0; i < _model.Entities.Length; ++i )
			_model.Entities[i].DrawOverlay( Matrix4x4.Identity, OnTicket( i ) ? ticketProjection : projection, Vector3.Zero, white, worldNormals: true, parkLight: light );

		for ( int i = 0; i < _model.Entities.Length; ++i )
			_model.Entities[i].DrawOverlay( Matrix4x4.Identity, OnTicket( i ) ? ticketProjection : projection, Vector3.Zero, white, worldNormals: true, translucent: true, parkLight: light );
	}

	private bool OnTicket( int node ) => node < _onTicket.Length && _onTicket[node];

	/// <summary>The node named <c>Ticket2</c> (-1 where there is none), its x axis at rest, and the longest its clips' scale keys make it.</summary>
	internal static (int Node, float AtRest, float Full) TicketOf( ModelFile file, IEnumerable<AnimationFile> clips )
	{
		var node = file.Meshes.FindIndex( mesh => string.Equals( mesh.Name.TrimEnd( '\0' ).Trim(), "Ticket2", StringComparison.OrdinalIgnoreCase ) );
		if ( node < 0 || node >= file.Nodes.Count )
			return (-1, 0f, 0f);

		var full = clips.SelectMany( clip => clip.ScaleTracks ).Where( track => track.TargetIndex == node )
			.SelectMany( track => track.Scales ).Select( lengths => lengths.X ).DefaultIfEmpty( 0f ).Max();

		return (node, AxisLengths( file.Nodes[node].LocalTransform ).X, full);
	}

	/// <summary>Which nodes are <paramref name="ticket"/> or hang from it, given each node's parent and an order with parents first.</summary>
	internal static bool[] OnTicketNodes( int[] parents, IEnumerable<int> parentsFirst, int ticket )
	{
		var on = new bool[parents.Length];

		foreach ( var node in parentsFirst )
			on[node] = node == ticket || (parents[node] >= 0 && parents[node] < on.Length && on[parents[node]]);

		return on;
	}

	/// <summary>How far the ticket has grown from its rest toward the largest its scale tracks make it, 0 to 1, as last posed.</summary>
	internal float TicketGrowth { get; private set; }

	/// <summary>The ticket's growth: its x axis <paramref name="length"/> from <paramref name="rest"/> to <paramref name="full"/>, held to 0 to 1.</summary>
	internal static float Growth( float length, float rest, float full )
		=> full > rest ? Math.Clamp( (length - rest) / (full - rest), 0f, 1f ) : 0f;

	/// <summary>
	/// How far, in window pixels, the ticket is drawn from where his own pin puts it: from his pin, the right and the
	/// bottom, toward the middle's, by its <paramref name="growth"/>.
	/// <para>
	/// <b>A deviation:</b> the original stretches him with its 4:3 screen to any mode (his root scale is the constant
	/// 0.01125, 0.001, 0.015 at Advisor_LoadModel 0x00429ba0), so the full-grown ticket of clips 18 to 20 stands across the
	/// middle of every mode. Here he keeps his shape and his pin to the right, where the lobby's player slots are, and the
	/// ticket alone eases to the middle's pin as it grows out of his hand, back as it shrinks (docs/exe/ui.md, "The big
	/// ticket is a scale track"). Nought on a 4:3 window.
	/// </para>
	/// </summary>
	internal static (float Across, float Down) TicketMove( float growth )
		=> ((UI.VirtualScreen.Offset( UI.Anchor.Centre ) - UI.VirtualScreen.Offset( UI.Anchor.Right )) * growth,
			(UI.VirtualScreen.OffsetDown( UI.VerticalAnchor.Middle ) - UI.VirtualScreen.OffsetDown( UI.VerticalAnchor.Bottom )) * growth);

	/// <summary><paramref name="projection"/> moved by <see cref="TicketMove"/> on the window.</summary>
	internal static Matrix4x4 TicketProjection( Matrix4x4 projection, float growth )
	{
		var (across, down) = TicketMove( growth );

		if ( Screen.Width > 0 && Screen.Height > 0 )
		{
			projection.M41 += across * 2f / Screen.Width;
			projection.M42 -= down * 2f / Screen.Height;
		}

		return projection;
	}

	/// <summary>
	/// The direction his record's light travels in this model's world (X across, Y into the screen, Z up). The original's
	/// is (26.667, -26.667, 800) in the world his node chain draws into (Advisor_LoadModel 0x00429ba0), and its lighter
	/// takes it through the inverse of the whole chain (FUN_005743a0, FUN_00578b20), his root's axis lengths 0.01125,
	/// 0.001 and 0.015 among it. Here those lengths are in <see cref="ScreenProjection"/>, so the direction is divided by
	/// them before the shader takes it through the rest of the chain: across 26.667 / 0.01125, into the screen
	/// 800 / 0.001, up 26.667 / 0.015 (the screen's flip turns -26.667 up). Nearly straight into the screen: the faces
	/// turned to the viewer are lit (docs/exe/ui.md, "How he is lit"). The lengths are the original's 4:3 ones, which
	/// keep his shape; here he keeps it on any window (<see cref="TicketMove"/>), so they hold on any.
	/// </summary>
	internal static readonly Vector3 Travels = new Vector3( 26.667f / 0.01125f, 800f / 0.001f, 26.667f / 0.015f ).Normal;

	/// <summary>
	/// The lobby's ambient, 68 of 255 a channel: every lobby camera's start hands the renderer 0x444444 (FUN_005d9230 from
	/// 0x005e13a9, IslandLobby_Start 0x005e1924 and 0x005e2445); measured 0.267 in the running original's lobby.
	/// </summary>
	internal static readonly Vector3 LobbyAmbient = new( 68f / 255f );

	/// <summary>
	/// His light: the park's ambient (the theme's <c>AmbientLightLevel</c>) or the lobby's <see cref="LobbyAmbient"/>, the
	/// direction <see cref="Travels"/>, and a strength of one for each of the <paramref name="records"/> advisor records
	/// loaded: each files its own white light along the same direction in his render list (Advisor_LoadModel 0x00429ba0,
	/// 0x00429d15), so the lobby's one model is lit by one and a park's two, the global fly and the theme's, by two
	/// (measured 1.00 and 2.00). At <c>LIGHTING</c> 0 (<paramref name="lit"/> false) every vertex is full, as the park's.
	/// </summary>
	internal static (ParkLight Light, float Strength) LightFor( ParkLight? park, int records, bool lit )
		=> (new ParkLight( park?.Ambient ?? LobbyAmbient, Travels, park?.Lit ?? lit ), records);

	/// <summary>
	/// Each shown mesh's lit colours as <see cref="VertexLight"/> gives them in the pose drawn last: its name, vertices, and
	/// the least, most and mean red, green and blue, as <c>original/advlight.py</c> reads the original's.
	/// </summary>
	internal IEnumerable<string> LitCensus( ParkLight light, float strength )
	{
		for ( int i = 0; i < _file.Meshes.Count && i < _model.Entities.Length; ++i )
		{
			if ( _model.Entities[i].Opacity <= 0f )
				continue;

			var mesh = _file.Meshes[i];
			var model = _model.Entities[i].ModelMatrix;
			var lit = Enumerable.Range( 0, mesh.Vertices.Length )
				.Select( vertex => light.Lit ? VertexLight( FileNormal( mesh, vertex ), model, light.Ambient, strength ) : new Vector3( 255f ) ).ToArray();

			if ( lit.Length == 0 )
				continue;

			string Of( Func<Vector3, float> channel ) => $"{lit.Min( channel ):0}/{lit.Max( channel ):0}/{lit.Average( channel ):0.0}";
			yield return $"{mesh.Name.TrimEnd( '\0' ).Trim()}: {lit.Length} vertices, r {Of( c => c.X )} g {Of( c => c.Y )} b {Of( c => c.Z )} (least/most/mean)";
		}
	}

	/// <summary>The normal the original lights vertex <paramref name="vertex"/> of <paramref name="mesh"/> by: the model file's own, its +0x64 table's entry for the vertex-order entry, or the loader's where the table is short.</summary>
	internal static Vector3 FileNormal( ModelFile.Mesh mesh, int vertex )
		=> vertex < mesh.FaceNormals.Length ? mesh.FaceNormals[vertex] : mesh.Normals[vertex];

	/// <summary>
	/// The colour, 0 to 255 a channel, the original lights a vertex of his with <paramref name="normal"/> (the file's, its
	/// axes) on a node drawn by <paramref name="model"/>: <paramref name="ambient"/> plus <paramref name="strength"/> of
	/// white times max(0, -(d.n)), d <see cref="Travels"/> taken into the node's space and normalised, each channel
	/// clamped and rounded to a byte (Light_SumAtVertex 0x005741b0, Light_ModelVertexColours 0x00574660). The texel is
	/// multiplied by it, a lit face at most its texel: his draws' state words never carry 0x10000, stage 0's MODULATE2X.
	/// content/shaders/test.shader does the same per vertex; this is its census (<see cref="LitCensus"/>).
	/// </summary>
	internal static Vector3 VertexLight( Vector3 normal, Matrix4x4 model, Vector3 ambient, float strength )
	{
		if ( !Matrix4x4.Invert( model, out var inverse ) )
			return Vector3.Zero;

		var d = System.Numerics.Vector3.Normalize( System.Numerics.Vector3.TransformNormal( Travels.GetSystemVector3(), inverse ) );
		var facing = -System.Numerics.Vector3.Dot( d, new System.Numerics.Vector3( normal.X, normal.Z, normal.Y ) );
		var lift = strength * MathF.Max( facing, 0f );

		static float Byte( float channel ) => MathF.Round( Math.Clamp( channel, 0f, 1f ) * 255f );
		return new Vector3( Byte( ambient.X + lift ), Byte( ambient.Y + lift ), Byte( ambient.Z + lift ) );
	}

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
	/// <para>
	/// It is affine, as the original's screen-space draw is (ModelRenderable_Draw 0x0057a750; docs/exe/ui.md, "His projection
	/// is affine"): how far toward the viewer a node stands changes no size. The golden ticket that fills the screen in Lost
	/// Kingdom's clips 18 to 20 is his own, grown by its scale track in clip 18, held through 19 and shrunk to three in 20 (<see cref="Pose"/>), and drawn toward the middle's pin as it grows (<see cref="TicketMove"/>).
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

	/// <summary>
	/// His nodes' own transforms <paramref name="frame"/> frames into <paramref name="animation"/>, from <paramref name="rest"/>:
	/// each rotation key's turn, then each scale key's axis lengths, then each position key's place, as Anim_PoseTrack takes
	/// a track's channels (0x00471860).
	/// </summary>
	internal static void PoseNodes( AnimationFile animation, float frame, Matrix4x4[] rest, Matrix4x4[] local )
	{
		foreach ( var track in animation.RotationTracks )
		{
			var node = track.TargetIndex;

			if ( node < 0 || node >= local.Length || track.Rotations.Length == 0 )
				continue;

			local[node] = Turned( rest[node], track.Sample( frame ) );
		}

		// A scale key sets the lengths of the node's three axes, after its turn (Anim_PoseTrack 0x00471d8d,
		// Matrix_SetAxisLengths 0x0046f910): Lost Kingdom's golden ticket grows twelvefold through clip 18 and falls to
		// three times in 20, and clips 17 and 18 squash his body and head (docs/exe/ui.md, "The big ticket is a scale
		// track").
		Scale( animation, frame, local );

		// A position key replaces where the node sits relative to its parent, as a rotation
		// key replaces its orientation. Clips 14 and 15 raise his head and body from below the
		// screen and drop them back down out of sight; Lost Kingdom's 17 to 20 carry the ticket.
		foreach ( var track in animation.PositionTracks )
		{
			var node = track.TargetIndex;

			if ( node < 0 || node >= local.Length )
				continue;

			var position = track.Sample( frame );
			local[node].M41 = position.X;
			local[node].M42 = position.Y;
			local[node].M43 = position.Z;
		}
	}

	/// <summary>Each node a scale track of <paramref name="animation"/> keys at <paramref name="frame"/>, its axes set to the keyed lengths.</summary>
	internal static void Scale( AnimationFile animation, float frame, Matrix4x4[] local )
	{
		foreach ( var track in animation.ScaleTracks )
		{
			var node = track.TargetIndex;

			if ( node < 0 || node >= local.Length || !track.TrySample( frame, out var lengths ) )
				continue;

			local[node] = WithAxisLengths( local[node], lengths );
		}
	}

	/// <summary>
	/// Matrix_SetAxisLengths (0x0046f910): each of the node's three axis rows scaled to the given length, its direction
	/// and its translation kept.
	/// </summary>
	internal static Matrix4x4 WithAxisLengths( Matrix4x4 local, System.Numerics.Vector3 lengths )
	{
		var x = new System.Numerics.Vector3( local.M11, local.M12, local.M13 );
		var y = new System.Numerics.Vector3( local.M21, local.M22, local.M23 );
		var z = new System.Numerics.Vector3( local.M31, local.M32, local.M33 );

		x *= lengths.X / x.Length();
		y *= lengths.Y / y.Length();
		z *= lengths.Z / z.Length();

		return new Matrix4x4(
			x.X, x.Y, x.Z, local.M14,
			y.X, y.Y, y.Z, local.M24,
			z.X, z.Y, z.Z, local.M34,
			local.M41, local.M42, local.M43, local.M44 );
	}

	/// <summary>Composes the node tree and hands each mesh its place, the way LobbyModel builds them.</summary>
	private void Place()
	{
		foreach ( var node in _parentsFirst )
		{
			var parent = _parents[node];
			_world[node] = parent >= 0 && parent < _world.Length ? _local[node] * _world[parent] : _local[node];
		}

		TicketGrowth = _ticket >= 0 ? Growth( AxisLengths( _local[_ticket] ).X, _ticketRest, _ticketFull ) : 0f;

		for ( int i = 0; i < _model.Entities.Length && i < _world.Length; ++i )
		{
			var world = _world[i];
			_model.Entities[i].LinearTransform = LobbyModel.ToWorldSpace( world );
			_model.Entities[i].Position = new Vector3( world.M41, world.M43, world.M42 );
		}
	}

	private static System.Numerics.Vector3 AxisLengths( Matrix4x4 m ) => new(
		new System.Numerics.Vector3( m.M11, m.M12, m.M13 ).Length(),
		new System.Numerics.Vector3( m.M21, m.M22, m.M23 ).Length(),
		new System.Numerics.Vector3( m.M31, m.M32, m.M33 ).Length() );

	private string NodeName( int node )
		=> node < _file.Meshes.Count ? _file.Meshes[node].Name.TrimEnd( '\0' ).Trim() : $"node {node}";

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
	internal static int[] ParentsFirst( int[] parents )
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
