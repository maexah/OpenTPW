using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// A fixed pool of camera-facing quads drawn in one call, which is what both weather effects are.
///
/// The engine has no particle system, no instancing and no per-vertex colour, so a swarm of
/// sprites has to be one mesh whose vertices are rebuilt each frame - the same approach
/// <see cref="MeshAnimator"/> already takes for the models it morphs. The pool is allocated once
/// at its maximum size and quads that aren't wanted this frame are collapsed to nothing rather
/// than removed, so the vertex and index buffers never have to be resized.
/// </summary>
public abstract class WeatherSprites : ModelEntity
{
	[StructLayout( LayoutKind.Sequential )]
	protected struct ObjectUniformBuffer
	{
		public Matrix4x4 g_mModel; // 64
		public Matrix4x4 g_mView; // 64
		public Matrix4x4 g_mProj; // 64

		public Vector4 g_vTint; // 16
	}

	/// <summary>Multiplied into every sprite - see content/shaders/weather.shader.</summary>
	protected Vector4 Tint { get; set; } = Vector4.One;

	private readonly int _capacity;
	private readonly string _texturePath;
	private Vertex[] _vertices = Array.Empty<Vertex>();

	// How many quads the buffer currently holds live geometry for. Anything past this is
	// already collapsed on the GPU, so neither collapsing it again nor re-uploading it does
	// anything - which matters when the pool is sized for the heaviest storm and most frames
	// are using a fraction of it.
	private int _uploaded;

	protected WeatherSprites( int capacity, string texturePath )
	{
		_capacity = capacity;
		_texturePath = texturePath;

		Build();
	}

	/// <summary>
	/// Points a quad at the camera. <paramref name="up"/> is the quad's own up in world space -
	/// rain keeps world up so drops always fall straight down the screen - and the across
	/// direction is whatever is perpendicular to both that and the view.
	/// </summary>
	protected void WriteQuad( int index, Vector3 centre, Vector3 up, float halfWidth, float halfHeight )
	{
		if ( index < 0 || index >= _capacity )
			return;

		var toCamera = Camera.Position - centre;
		var across = up.Cross( toCamera );

		// Edge-on: the quad has no width from here, so there is nothing to draw. Collapsing it
		// is both correct and cheaper than picking an arbitrary axis to spin it around.
		if ( across.LengthSquared < 0.000001f )
		{
			Collapse( index );
			return;
		}

		across = across.Normal * halfWidth;
		var rise = up.Normal * halfHeight;

		var v = index * 4;
		_vertices[v + 0] = new Vertex( centre - across + rise, new Vector2( 0f, 0f ) );
		_vertices[v + 1] = new Vertex( centre + across + rise, new Vector2( 1f, 0f ) );
		_vertices[v + 2] = new Vertex( centre + across - rise, new Vector2( 1f, 1f ) );
		_vertices[v + 3] = new Vertex( centre - across - rise, new Vector2( 0f, 1f ) );
	}

	/// <summary>Shrinks a quad to a point, which rasterises to nothing.</summary>
	protected void Collapse( int index )
	{
		var v = index * 4;
		for ( int i = 0; i < 4; ++i )
			_vertices[v + i] = new Vertex( Vector3.Zero, Vector2.Zero );
	}

	/// <summary>
	/// Collapses whatever was live last frame but isn't now, then uploads just as far as either
	/// of those reaches.
	/// </summary>
	protected void Upload( int used )
	{
		used = Math.Clamp( used, 0, _capacity );

		for ( int i = used; i < _uploaded; ++i )
			Collapse( i );

		Model?.UpdateVertices( _vertices, Math.Max( used, _uploaded ) * 4 );
		_uploaded = used;
	}

	private void Build()
	{
		_vertices = new Vertex[_capacity * 4];

		var indices = new uint[_capacity * 6];
		for ( int i = 0; i < _capacity; ++i )
		{
			var v = (uint)(i * 4);
			var t = i * 6;

			indices[t + 0] = v + 0;
			indices[t + 1] = v + 1;
			indices[t + 2] = v + 2;
			indices[t + 3] = v + 0;
			indices[t + 4] = v + 2;
			indices[t + 5] = v + 3;
		}

		for ( int i = 0; i < _capacity; ++i )
			Collapse( i );

		// Depth-tested so weather sits behind the islands properly, but not depth-writing:
		// these are transparent and drawn in whatever order the pool happens to be in, so
		// letting them occlude each other would show as sprite-shaped holes.
		var material = new Material<ObjectUniformBuffer>( "content/shaders/weather.shader",
			MaterialFlags.DisableDepthWrite | MaterialFlags.Additive );

		material.Set( "Color", LoadTexture( _texturePath ) );

		Model = new Model( _vertices, indices, material );
		Model.EnableFrequentUpdates( _vertices );
	}

	/// <summary>
	/// The weather art is a loose .tga in the game's own directories rather than a .wct in an
	/// archive, and Texture's path constructor reads a real file off disk for those - which the
	/// game data is not, since it may live inside a WAD. Going through FileSystem gets it either
	/// way.
	/// </summary>
	private static Texture LoadTexture( string path )
	{
		using var stream = FileSystem.OpenRead( path );

		if ( stream == null )
		{
			Log.Warning( $"Weather texture '{path}' is missing - the effect will draw as a white square" );
			return Texture.Missing;
		}

		return new Texture( stream );
	}

	protected override void OnRender()
	{
		if ( Model == null )
			return;

		// Vertices are built in world space, so the model transform is identity rather than
		// this entity's - it has no position of its own to speak of.
		Model.Material.Set( "ObjectUniformBuffer", new ObjectUniformBuffer
		{
			g_mModel = Matrix4x4.Identity,
			g_mView = Camera.ViewMatrix,
			g_mProj = Camera.ProjMatrix,
			g_vTint = Tint
		} );

		Model.Draw();
	}
}
