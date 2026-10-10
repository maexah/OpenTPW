using System.Numerics;
using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// Draws the particle system's effects that stand in the world: the bubbles over a Drinks Shop, a dirty toilet's
/// stink, the Jungle Spray's jet. Every effect not flagged for the screen is one (<see cref="ScreenParticles"/>
/// draws the others).
///
/// <para>
/// <b>Where they go.</b> Particles_Render (0x0051ef30) hands a world particle on at sixteen times its own
/// position and twice its size, and the sprite pass (0x0057c620) takes the position over 1024, so a particle
/// stands at its position over 64 in the park's units: x, the height, z. The pass puts that point through the
/// scene's matrix and builds the sprite round where it lands, a half height of the doubled size over 2048 and a
/// half width of that times the picture's width over its height, or 0.75 for an effect with no picture
/// (<c>DAT_008bcbcc</c>, the screen's height over its width).
/// </para>
/// <para>
/// <b>How big that is in the park.</b> The corners are set out after the matrix, so the sprite is flat to the
/// screen and its size in the park follows from the matrix alone: the park's 90 degree lens (each of x and y
/// times cos 45, the fourth over sin 45 of the depth) and then the screen's own scale, 1.5 across and 2 down
/// (<c>0x0087b0d0</c>). A half height of one is so 1 / (2 cos 45) of a park unit, and a half width of one
/// 1 / (1.5 cos 45): every picture a third wider than it is authored, as on the screen. The quads here are
/// built that size in the world, turned to the camera, so they are right whatever lens draws them.
/// </para>
/// <para>
/// <b>How.</b> The picture times the particle's colour. An effect whose draw flags carry 0x4 is added to what
/// is behind it, scaled by its alpha; any other is blended over it. Tested against the scene's depth and never
/// written to it (the pass sets state bit 0x800 for every world sprite). Counted and drawn scaled by its alpha
/// all the same: an effect added without it (0x4 with no 0x2000).
/// </para>
/// <para>
/// <b>Engine and content.</b> Drawing is engine. Which effects are started and where is up to whoever starts
/// them: in a park, the ride scripts (<c>RideScript.StartParticle</c>).
/// </para>
/// </summary>
internal sealed class WorldParticles : Entity
{
	/// <summary>A half width of one in the sprite pass, in park units: 1 / (1.5 cos 45).</summary>
	internal const float UnitsAcross = 1f / (1.5f * 0.70710677f);

	/// <summary>A half height of one in the sprite pass, in park units: 1 / (2 cos 45).</summary>
	internal const float UnitsDown = 1f / (2f * 0.70710677f);

	/// <summary>The width over the height of an effect with no picture (<c>DAT_008bcbcc</c>).</summary>
	internal const float PlainAspect = 0.75f;

	internal static WorldParticles? Current { get; private set; }

	/// <summary>How many particles the last frame drew.</summary>
	internal int Drawn { get; private set; }

	private Texture? _atlas;
	private Pool? _blended;
	private Pool? _added;

	public WorldParticles()
	{
		Current = this;
	}

	/// <summary>
	/// One particle's sprite as the pass sets it out: where its middle stands in the park (x, the height, z) and
	/// its half width and half height in park units.
	/// </summary>
	internal readonly record struct Sprite( float X, float Height, float Z, float HalfWidth, float HalfHeight );

	/// <summary>
	/// The sprite of a particle of <paramref name="emitter"/>, whose picture is <paramref name="aspect"/> wide for
	/// its height. An effect with a scale draws each particle that many thousandths of the way out from its emitter.
	/// </summary>
	internal static Sprite SpriteOf( Emitter emitter, in Particle particle, float aspect )
	{
		var template = emitter.Template;
		int x = particle.X, y = particle.Y, z = particle.Z;

		if ( template.Scale != 1000 )
		{
			x = ((x - emitter.X) * template.Scale / 1000) + emitter.X;
			y = ((y - emitter.Y) * template.Scale / 1000) + emitter.Y;
			z = ((z - emitter.Z) * template.Scale / 1000) + emitter.Z;
		}

		// The size doubled as a sixteen-bit word, then over 1024 and halved.
		var half = (ushort)((short)particle.Size << 1) / 1024f * 0.5f;

		return new Sprite( x / 64f, y / 64f, z / 64f, half * aspect * UnitsAcross, half * UnitsDown );
	}

	/// <summary>Whether <paramref name="emitter"/> is one this draws: running, holding particles, shown, and of the world.</summary>
	internal static bool Draws( Emitter emitter ) => emitter.Active && emitter.Count > 0 && !emitter.Hidden && !emitter.Template.OnScreen;

	protected override void OnRenderTranslucent()
	{
		Drawn = 0;

		if ( ParticleSystem.Current is not { } system || ScreenParticles.Current is not { } pictures )
			return;

		// The pictures belong to the interface's drawer, which a scene makes afresh.
		if ( !ReferenceEquals( _atlas, pictures.Atlas ) )
		{
			_blended?.Delete();
			_added?.Delete();

			_atlas = pictures.Atlas;
			_blended = new Pool( _atlas, MaterialFlags.None );
			_added = new Pool( _atlas, MaterialFlags.Additive );
		}

		var forward = Camera.Rotation.Forward;
		var across = forward.Cross( Vector3.Up );

		// Looking straight down there is no horizontal to set a sprite out along.
		if ( across.LengthSquared < 0.000001f )
			return;

		across = across.Normal;

		var upward = across.Cross( forward ).Normal;

		_blended!.Begin();
		_added!.Begin();

		for ( int slot = 0; slot < ParticleSystem.EmitterCount; ++slot )
		{
			var emitter = system.Emitters[slot];

			if ( !Draws( emitter ) )
				continue;

			var template = emitter.Template;
			var adds = (template.DrawFlags & 0x4) != 0;

			if ( adds && (template.DrawFlags & 0x2000) == 0 )
				Unimplemented.Report( "WORLD_PARTICLE_ADDED_WITHOUT_ALPHA" );

			var pool = adds ? _added : _blended;

			for ( int index = emitter.FirstParticle; index >= 0; index = system.Particles[index].Next )
			{
				ref var particle = ref system.Particles[index];

				var region = template.Frames == 0 ? pictures.Plain : pictures.Picture( template.SpriteSet, particle.Frame );

				if ( region is null )
					continue;

				var sprite = SpriteOf( emitter, particle, template.Frames == 0 ? PlainAspect : region.Aspect );

				// The park's x, height and z are this world's x, z and y.
				pool.Add( new Vector3( sprite.X, sprite.Z, sprite.Height ), across * sprite.HalfWidth, upward * sprite.HalfHeight,
					particle.Rotation, region, particle.Colour );

				++Drawn;
			}
		}

		_blended.Draw();
		_added.Draw();
	}

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;

		_blended?.Delete();
		_added?.Delete();
	}

	[System.Runtime.InteropServices.StructLayout( System.Runtime.InteropServices.LayoutKind.Sequential )]
	private struct ObjectUniformBuffer
	{
		public Matrix4x4 g_mModel;
		public Matrix4x4 g_mView;
		public Matrix4x4 g_mProj;
	}

	/// <summary>
	/// A pool of quads for one blend, built in the world every frame and drawn in one go. Quads not wanted this
	/// frame are shrunk to nothing, so the buffers never change size.
	/// </summary>
	private sealed class Pool
	{
		private readonly Vertex[] _vertices = new Vertex[ParticleSystem.ParticleCount * 4];
		private readonly Model _model;
		private int _used;
		private int _uploaded;

		public Pool( Texture atlas, MaterialFlags blend )
		{
			var indices = new uint[ParticleSystem.ParticleCount * 6];

			for ( uint i = 0; i < ParticleSystem.ParticleCount; ++i )
			{
				var at = i * 6;
				indices[at] = i * 4;
				indices[at + 1] = (i * 4) + 1;
				indices[at + 2] = (i * 4) + 2;
				indices[at + 3] = i * 4;
				indices[at + 4] = (i * 4) + 2;
				indices[at + 5] = (i * 4) + 3;
			}

			// Tested against the scene's depth and not written to it; two-sided because a quad turned to the
			// camera can be wound either way. The guests' shader: a picture times the colour its vertices carry.
			var material = new Material( "content/shaders/guests.shader", MaterialFlags.DisableDepthWrite | MaterialFlags.DisableCulling | blend );
			material.Set( "Color", atlas );

			_model = new Model( _vertices, indices, material );
			_model.EnableFrequentUpdates( _vertices );
		}

		/// <summary>Lets go of the pool's model and its material. The atlas is the interface drawer's.</summary>
		public void Delete() => _model.Delete();

		public void Begin() => _used = 0;

		/// <param name="across">The camera's right, the sprite's half width long.</param>
		/// <param name="upward">The camera's up, the sprite's half height long.</param>
		/// <param name="rotation">Of 65536, turned as the screen's sprites are.</param>
		public void Add( Vector3 centre, Vector3 across, Vector3 upward, int rotation, ScreenParticles.Region region, uint colour )
		{
			if ( _used >= ParticleSystem.ParticleCount )
				return;

			float cos = 1f, sin = 0f;

			if ( rotation != 0 )
			{
				var turn = ((rotation >> 8) & 0xff) * MathF.Tau / 256f;
				cos = MathF.Cos( turn );
				sin = MathF.Sin( turn );
			}

			var first = _used * 4;

			Corner( first, -1f, -1f, region.Left, region.Top );
			Corner( first + 1, 1f, -1f, region.Right, region.Top );
			Corner( first + 2, 1f, 1f, region.Right, region.Bottom );
			Corner( first + 3, -1f, 1f, region.Left, region.Bottom );

			_used++;

			// Down the screen is against the camera's up.
			void Corner( int vertex, float right, float down, float u, float v )
				=> _vertices[vertex] = new Vertex( centre + (across * ((right * cos) + (down * sin))) - (upward * ((down * cos) - (right * sin))), new Vector2( u, v ) ) { MatFlags = colour };
		}

		public void Draw()
		{
			for ( int i = _used * 4; i < _uploaded * 4; ++i )
				_vertices[i] = new Vertex();

			var reach = Math.Max( _used, _uploaded );
			_uploaded = _used;

			if ( reach == 0 )
				return;

			_model.UpdateVertices( _vertices, reach * 4 );

			if ( _used == 0 )
				return;

			_model.Material.Set( "g_oUbo", new ObjectUniformBuffer
			{
				g_mModel = Matrix4x4.Identity,
				g_mView = Camera.ViewMatrix,
				g_mProj = Camera.ProjMatrix
			} );

			_model.Draw();
		}
	}
}
