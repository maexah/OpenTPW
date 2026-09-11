using Matrix4x4 = System.Numerics.Matrix4x4;
using NumericsVector3 = System.Numerics.Vector3;

namespace OpenTPW.UI;

/// <summary>
/// One of the interface's meshes out of ui.wad, drawn flat onto a control's rectangle.
///
/// <para>
/// <b>How the original finds them.</b> It loads every mesh ui.wad lists and files each under the name
/// of its first mesh (0x00659a58, keyed by a hash that multiplies by 47), and the layout data asks
/// for them by that hash - so w_dialog_wave.md2 is asked for as "wdialogw" and islandlobby.md2 as
/// "islandlob". Nothing here needs the hash: the windows name the file.
/// </para>
/// <para>
/// <b>Parts.</b> A mesh is a list of parts, one per node, and a control shows one of them at a time:
/// its frame plus its state (0x00476e80). The six-part buttons are built that way on purpose - see
/// <see cref="UiButton"/> - and so are the gold and grey keys of s_key.md2 and the "x 1" to "x 5" of
/// s_X1.md2, gold and then grey.
/// </para>
/// <para>
/// <b>Placing one.</b> The part is stretched until its bounds fill the control's rectangle exactly
/// (0x00476f10, 0x00476c50). The meshes are authored on the same 2048x1536 screen as the layout, so
/// for nearly all of them the stretch is only a move.
/// </para>
/// <para>
/// <b>Frames.</b> The meshes named with a leading '!' are nine-slice frames, a 4x4 grid of vertices.
/// Before they are fitted, 0x00477310 moves every vertex right of the part's origin right by however
/// much wider the control is than the mesh, and every vertex below it down by however much taller,
/// so the corners keep their size and only the middle grows.
/// </para>
/// <para>
/// <b>How they look.</b> Unlit, with no depth test - a control covers whatever was drawn before it -
/// and every material takes its texture's alpha. The interface depends on that throughout: the
/// screen dimmer behind a dialog, lolight.wct, is flat black at half alpha on a material not flagged
/// see-through, and the world's shader only honours alpha on materials that are.
/// </para>
/// </summary>
internal sealed class UiMesh
{
	/// <summary>The see-through bit of a material's flags - see ModelFile.MaterialData.IsTranslucent.</summary>
	private const uint TranslucentFlag = 0x2;

	private static readonly Dictionary<string, UiMesh?> Loaded = new( StringComparer.OrdinalIgnoreCase );

	private static Texture? _blank;

	private readonly Part[] _parts;
	private readonly bool _isFrame;

	public string Name { get; }

	public int PartCount => _parts.Length;

	/// <summary>A mesh by name - "b_login" for ui/b_login.md2 - loaded once. Null, and a warning, if it will not load.</summary>
	public static UiMesh? Get( string name )
	{
		if ( Loaded.TryGetValue( name, out var mesh ) )
			return mesh;

		try
		{
			mesh = new UiMesh( name );
		}
		catch ( Exception e )
		{
			Log.Warning( $"UI: mesh '{name}' would not load - {e.Message}" );
		}

		Loaded[name] = mesh;
		return mesh;
	}

	private UiMesh( string name )
	{
		Name = name;
		_isFrame = name.StartsWith( '!' );

		var file = new ModelFile( $"ui/{name}.md2" );
		_parts = file.Meshes.Select( mesh => new Part( mesh, _isFrame ) ).ToArray();
	}

	/// <summary>
	/// Draws part <paramref name="part"/> over <paramref name="pixels"/>. A part the mesh does not have
	/// draws nothing, as the original's lookup comes back empty.
	/// </summary>
	/// <param name="size">The control's size on the virtual screen, which is what a frame stretches to.</param>
	public void Draw( int part, PixelRect pixels, UiRect size, float opacity = 1f )
	{
		if ( part < 0 || part >= _parts.Length )
			return;

		if ( _isFrame )
			_parts[part].Stretch( size.Width, size.Height );

		_parts[part].Draw( pixels, opacity );
	}

	private sealed class Part
	{
		private readonly NumericsVector3[] _authored;
		private readonly Matrix4x4 _transform;
		private readonly Vertex[] _vertices;
		private readonly Model _model;

		private readonly float _authoredWidth;
		private readonly float _authoredHeight;

		private float _left, _right, _bottom, _top;
		private int _stretchedWidth = -1;
		private int _stretchedHeight = -1;

		public Part( ModelFile.Mesh mesh, bool isFrame )
		{
			var textures = new Texture[16];

			for ( int i = 0; i < textures.Length; ++i )
			{
				textures[i] = i < mesh.Materials.Length && !string.IsNullOrEmpty( mesh.Materials[i].Name )
					? new Texture( $"ui/textures/{mesh.Materials[i].Name}.wct" )
					: _blank ??= Texture.Missing;
			}

			_transform = mesh.WorldTransform;
			_authored = new NumericsVector3[mesh.Vertices.Length];
			_vertices = new Vertex[mesh.Vertices.Length];

			for ( int i = 0; i < _vertices.Length; ++i )
			{
				var position = mesh.Vertices[i].Position;
				var material = (int)mesh.Vertices[i].TextureIndex;

				_authored[i] = new NumericsVector3( position.X, position.Y, position.Z );
				_vertices[i] = new Vertex
				{
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = material,
					MatFlags = (material < mesh.Materials.Length ? mesh.Materials[material].Flags : 0) | TranslucentFlag
				};
			}

			Place( 0f, 0f );
			_authoredWidth = _right - _left;
			_authoredHeight = _top - _bottom;

			// Wrapping bleeds a texture's far edge into its near one wherever a mesh maps it edge to
			// edge, which the buttons all do - the purple button grew a pale line down its left side.
			// So a mesh whose texture coordinates stay inside the texture is clamped, and only one
			// that reaches past it, to repeat it, wraps.
			var withinTexture = mesh.TexCoords.All( uv => uv.X >= -0.001f && uv.X <= 1.001f && uv.Y >= -0.001f && uv.Y <= 1.001f );

			var shading = new Material<ObjectUniformBuffer>( "content/shaders/test.shader",
				MaterialFlags.DisableDepth | MaterialFlags.DisableCulling );
			shading.Set( "Color", textures, withinTexture ? SamplerType.Anisotropic : SamplerType.AnisotropicWrap );

			_model = new Model( _vertices, mesh.Indices, shading );

			if ( isFrame )
				_model.EnableFrequentUpdates( _vertices );
		}

		/// <summary>Grows a frame's middle to a size, as 0x00477310 does - see the class remarks.</summary>
		public void Stretch( int width, int height )
		{
			if ( width == _stretchedWidth && height == _stretchedHeight )
				return;

			_stretchedWidth = width;
			_stretchedHeight = height;

			Place( width - _authoredWidth, height - _authoredHeight );
			_model.UpdateVertices( _vertices );
		}

		/// <summary>Puts the vertices where the node puts them, a frame's grown by the amounts given, and measures them.</summary>
		private void Place( float extraWidth, float extraHeight )
		{
			_left = _bottom = float.MaxValue;
			_right = _top = float.MinValue;

			for ( int i = 0; i < _vertices.Length; ++i )
			{
				var local = _authored[i];

				if ( local.X > 0f )
					local.X += extraWidth;

				if ( local.Y < 0f )
					local.Y -= extraHeight;

				var placed = NumericsVector3.Transform( local, _transform );
				_vertices[i].Position = new Vector3( placed.X, placed.Y, placed.Z );

				_left = MathF.Min( _left, placed.X );
				_right = MathF.Max( _right, placed.X );
				_bottom = MathF.Min( _bottom, placed.Y );
				_top = MathF.Max( _top, placed.Y );
			}
		}

		public void Draw( PixelRect pixels, float opacity )
		{
			if ( _vertices.Length == 0 )
				return;

			// The meshes are y-up with the top of the screen at zero; the window is y-down in pixels,
			// and clip space is y-up again. One matrix takes a vertex straight from the file to clip
			// space: its bounds onto the rectangle, the rectangle onto the window.
			var scaleX = pixels.Width / MathF.Max( _right - _left, 0.001f );
			var scaleY = pixels.Height / MathF.Max( _top - _bottom, 0.001f );

			var acrossScale = 2f * scaleX / Screen.Width;
			var acrossOffset = (2f * (pixels.X - (_left * scaleX)) / Screen.Width) - 1f;
			var upScale = 2f * scaleY / Screen.Height;
			var upOffset = 1f - (2f * (pixels.Y + (_top * scaleY)) / Screen.Height);

			var toScreen = new Matrix4x4(
				acrossScale, 0f, 0f, 0f,
				0f, upScale, 0f, 0f,
				0f, 0f, 0f, 0f,
				acrossOffset, upOffset, 0.5f, 1f );

			// Through the frame, as one part is drawn for every control that wears it - see Material.SetInFrame.
			_model.Material.SetInFrame( "ObjectUniformBuffer", new ObjectUniformBuffer
			{
				g_mModel = toScreen,
				g_mView = Matrix4x4.Identity,
				g_mProj = Matrix4x4.Identity,
				g_flTime = Time.Now,
				g_vFogColour = Level.FogColour,
				g_flOpacity = opacity,

				// Lit by nothing but a flat 1, so every texel is its texture's own colour.
				g_flAmbient = 1f
			} );

			_model.Draw();
		}
	}
}
