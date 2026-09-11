namespace OpenTPW.UI;

/// <summary>
/// Draws the particle system's on-screen effects over the interface: the sparkles round the golden
/// key, the keys the advisor hands over, the glints round a button.
///
/// <para>
/// <b>Where they go.</b> The original turns each particle into a sprite in two steps.
/// Particles_Render (0x0051ef30) takes an on-screen particle's position across as
/// <c>((x - 3125) * 16) / 49</c> and down as <c>(z * 16 - 37500) / 37</c>, and the sprite pass
/// (0x0057c620) scales both by 1/1024 into -1 to 1 of the screen. So particle space runs 6250 across
/// and 4687 down - the 100000 by 75000 effects are started at, over sixteen - on the same 2048x1536
/// screen the interface is laid out on (<see cref="VirtualScreen"/>): an effect started at 50000
/// across is in the middle. Up and down in the world, y, is not used.
/// </para>
/// <para>
/// <b>How big.</b> A particle's size over 64 is its height in 2048ths of the screen's -1 to 1, and
/// its width is that times the sprite's own width over height - in the screen's -1 to 1 across,
/// which is wider, so on a 4:3 screen the original stretches every sprite by a third across. It
/// does: the twinkles in screenshots of the park picker are wider than they are tall. A turned
/// particle's corners are turned in those same units, in 256ths of a turn (0x00591430).
/// </para>
/// <para>
/// <b>What they wear.</b> The effect's sprite set names a bank in <c>esprites.wad</c>'s
/// Generic\Particles folder - banks numbered in the order the folder's files come - and a set in it
/// (<see cref="SpriteBankFile"/>), whose pictures are in the bank's <c>.TPC</c>
/// (<see cref="SpritePackFile"/>). A particle shows the frame its age has reached. All of the
/// pictures go into one texture here, with space between so that the smaller mip levels of one do
/// not bleed into the next.
/// </para>
/// <para>
/// <b>How.</b> Each sprite is the picture times the particle's colour, alpha included. An effect
/// whose draw flags have 0x4 set adds its sprites to the screen, scaled by their alpha when 0x2000
/// is set too (source alpha, one) and not when it is not (one, one); the state word the sprite pass
/// builds carries the blend as source and destination nibbles, 0x52 and 0x22. Any other effect is
/// blended over the screen by its alpha, as the sprite textures ask. The lobby's effects all add.
/// </para>
/// <para>
/// <b>In front of what.</b> The original draws them all over every window, so the key's sparkles and
/// the ring of keys shine through the game menu's dimming and the quit box. The original's interface
/// controls are models whose depth goes by how far up they are (<c>1 - level / 40</c>, a menu or box
/// going above the top one), and the sprites are queued after all of the models (0x00582170) at
/// depth 0, tested less-or-equal and not written, so they pass in front of everything; and nothing
/// hides them when a menu or box opens. This departs from that on purpose. An effect started for a window (<see cref="Emitter.Owner"/>) is drawn straight after that
/// window - by the window stack, through <see cref="Draw(UiWindow)"/> - so the windows opened after it
/// cover it, dimming included, and it is not drawn while its window is put away or closed. Anything
/// else is still drawn here, over the whole interface.
/// </para>
/// <para>
/// <b>Engine and content.</b> Drawing on-screen effects is engine, the same for a park's interface. Which
/// effects are started, where, and for which window is the content of whoever starts them.
/// </para>
/// </summary>
internal sealed class ScreenParticles : Panel
{
	private const string SpriteFolder = "esprites/Generic/Particles";
	private const int AtlasWidth = 1024;
	private const int Padding = 4;

	private readonly List<(SpriteBankFile Bank, Region[] Pictures)> _banks = new();
	private readonly Region _plain;
	private readonly Texture _atlas;
	private readonly Layers _unowned;

	/// <summary>
	/// A pair of layers for each window drawn with effects so far this frame. A layer's vertices go to
	/// the device as they are written but its draw only runs with the frame, so one layer cannot be
	/// drawn twice in a frame with different sprites.
	/// </summary>
	private readonly List<Layers> _windowLayers = new();
	private int _windowLayersUsed;
	private bool _frameEndScheduled;

	internal static ScreenParticles? Current { get; private set; }

	/// <summary>Particle space across to the virtual screen - see the class remarks.</summary>
	internal static float ScreenX( int x ) => 1024f + ((x - 3125) * 16 / 49);

	/// <summary>Particle space down to the virtual screen - see the class remarks.</summary>
	internal static float ScreenY( int z ) => 768f + (((z * 16) - 37500) / 37 * 0.75f);

	public ScreenParticles()
	{
		var pictures = new List<SpritePicture>();
		var banks = new List<(SpriteBankFile, int First, int Count)>();

		foreach ( var file in FileSystem.GetFiles( SpriteFolder ).Where( file => file.EndsWith( ".esp", StringComparison.OrdinalIgnoreCase ) ) )
		{
			try
			{
				var bank = new SpriteBankFile( file );
				var pack = new SpritePackFile( Path.ChangeExtension( file, ".TPC" ) );

				banks.Add( (bank, pictures.Count, pack.Pictures.Length) );
				pictures.AddRange( pack.Pictures );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Particles: sprite bank '{file}' would not load - {e.Message}" );
			}
		}

		// A small white square for effects with no frames, which the original draws untextured.
		pictures.Add( new SpritePicture( 4, 4, 2, 2, Enumerable.Repeat( (byte)255, 4 * 4 * 4 ).ToArray() ) );

		var atlas = BuildAtlas( pictures, out var regions );

		foreach ( var (bank, first, count) in banks )
			_banks.Add( (bank, regions[first..(first + count)]) );

		_plain = regions[^1];

		_atlas = atlas;
		_unowned = new Layers( atlas );

		Current = this;
	}

	protected override void OnRender() => DrawEffects( null );

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	/// <summary>Draws the effects started for <paramref name="window"/>, just after the window - see the class remarks.</summary>
	internal void Draw( UiWindow window ) => DrawEffects( window );

	private void DrawEffects( UiWindow? owner )
	{
		if ( ParticleSystem.Current is not { } system )
			return;

		Layers layers;

		if ( owner == null )
		{
			layers = _unowned;
		}
		else
		{
			if ( !Array.Exists( system.Emitters, emitter => emitter.Owner == owner && Drawn( emitter ) ) )
				return;

			if ( !_frameEndScheduled )
			{
				_frameEndScheduled = true;
				Render.ScheduleDelete( () => (_windowLayersUsed, _frameEndScheduled) = (0, false) );
			}

			if ( _windowLayersUsed == _windowLayers.Count )
				_windowLayers.Add( new Layers( _atlas ) );

			layers = _windowLayers[_windowLayersUsed++];
		}

		layers.Blended.Begin();
		layers.Added.Begin();

		// By slot, as the sprite pass goes through them.
		for ( int slot = 0; slot < ParticleSystem.EmitterCount; ++slot )
		{
			var emitter = system.Emitters[slot];
			var template = emitter.Template;

			if ( emitter.Owner != owner || !Drawn( emitter ) )
				continue;

			var adds = (template.DrawFlags & 0x4) != 0;
			var layer = adds ? layers.Added : layers.Blended;
			var ignoresAlpha = adds && (template.DrawFlags & 0x2000) == 0;
			var offset = VirtualScreen.Offset( emitter.Anchor );

			for ( int index = emitter.FirstParticle; index >= 0; index = system.Particles[index].Next )
			{
				ref var particle = ref system.Particles[index];

				var region = template.Frames == 0 ? _plain : Picture( template.SpriteSet, particle.Frame );
				if ( region is null )
					continue;

				int x = particle.X, z = particle.Z;

				if ( template.Scale != 1000 )
				{
					x = ((x - emitter.X) * template.Scale / 1000) + emitter.X;
					z = ((z - emitter.Z) * template.Scale / 1000) + emitter.Z;
				}

				var halfHeight = Math.Max( (short)particle.Size / 64, 0 ) / 2048f;

				layer.Add( (ScreenX( x ) - 1024f) / 1024f, (ScreenY( z ) - 768f) / 768f, halfHeight * region.Aspect, halfHeight,
					particle.Rotation, region, particle.Colour, ignoresAlpha, offset );
			}
		}

		layers.Blended.Draw();
		layers.Added.Draw();
	}

	private static bool Drawn( Emitter emitter ) => emitter.Active && emitter.Count > 0 && !emitter.Hidden && emitter.Template.OnScreen;

	/// <summary>The two blends' layers drawn together.</summary>
	private sealed class Layers( Texture atlas )
	{
		public readonly Layer Blended = new( atlas, MaterialFlags.None );
		public readonly Layer Added = new( atlas, MaterialFlags.Additive );
	}

	/// <summary>Frame <paramref name="frame"/> of sprite set <paramref name="set"/> - 0x005423a0.</summary>
	private Region? Picture( int set, int frame )
	{
		var bank = set >> 4;
		if ( bank < 0 || bank >= _banks.Count )
			return null;

		var (file, pictures) = _banks[bank];
		var index = file.Sets[set & 0xf].First + frame;

		return index >= 0 && index < pictures.Length ? pictures[index] : null;
	}

	/// <summary>Every picture into one texture, row after row, with <see cref="Padding"/> clear pixels round each.</summary>
	private static Texture BuildAtlas( List<SpritePicture> pictures, out Region[] regions )
	{
		var places = new (int X, int Y)[pictures.Count];
		int x = Padding, y = Padding, rowHeight = 0;

		for ( int i = 0; i < pictures.Count; ++i )
		{
			if ( x + pictures[i].Width + Padding > AtlasWidth )
			{
				x = Padding;
				y += rowHeight + Padding;
				rowHeight = 0;
			}

			places[i] = (x, y);
			x += pictures[i].Width + Padding;
			rowHeight = Math.Max( rowHeight, pictures[i].Height );
		}

		var height = y + rowHeight + Padding;
		var pixels = new byte[AtlasWidth * height * 4];
		regions = new Region[pictures.Count];

		for ( int i = 0; i < pictures.Count; ++i )
		{
			var picture = pictures[i];
			var (left, top) = places[i];

			for ( int row = 0; row < picture.Height; ++row )
				Array.Copy( picture.Rgba, row * picture.Width * 4, pixels, ((top + row) * AtlasWidth + left) * 4, picture.Width * 4 );

			regions[i] = new Region(
				left / (float)AtlasWidth, top / (float)height,
				(left + picture.Width) / (float)AtlasWidth, (top + picture.Height) / (float)height,
				picture.Width / (float)Math.Max( picture.Height, 1 ) );
		}

		return new Texture( pixels, AtlasWidth, height );
	}

	/// <summary>Where a picture is in the texture, and how wide it is for its height.</summary>
	private sealed record Region( float Left, float Top, float Right, float Bottom, float Aspect );

	/// <summary>
	/// A pool of quads for one blend, built into clip space every frame and drawn in one go. Quads not
	/// wanted this frame are shrunk to nothing, as <see cref="WeatherSprites"/> does, so the buffers
	/// never change size.
	/// </summary>
	private sealed class Layer
	{
		private readonly Vertex[] _vertices = new Vertex[ParticleSystem.ParticleCount * 4];
		private readonly Model _model;
		private int _used;
		private int _uploaded;

		public Layer( Texture atlas, MaterialFlags blend )
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

			var material = new Material( "content/shaders/particles.shader", MaterialFlags.DisableDepth | MaterialFlags.DisableCulling | blend );
			material.Set( "Color", atlas );

			_model = new Model( _vertices, indices, material );
			_model.EnableFrequentUpdates( _vertices );
		}

		public void Begin() => _used = 0;

		/// <param name="centreX">Across the screen, -1 to 1.</param>
		/// <param name="centreY">Down the screen, -1 to 1.</param>
		/// <param name="rotation">Of 65536.</param>
		/// <param name="offset">Where the virtual screen starts across the window, for the effect's pin.</param>
		public void Add( float centreX, float centreY, float halfWidth, float halfHeight, int rotation, Region region, uint colour, bool ignoresAlpha, float offset )
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

			var scale = VirtualScreen.Scale;
			var first = _used * 4;

			Corner( first, -halfWidth, -halfHeight, region.Left, region.Top );
			Corner( first + 1, halfWidth, -halfHeight, region.Right, region.Top );
			Corner( first + 2, halfWidth, halfHeight, region.Right, region.Bottom );
			Corner( first + 3, -halfWidth, halfHeight, region.Left, region.Bottom );

			_used++;

			void Corner( int vertex, float across, float down, float u, float v )
			{
				var x = centreX + (across * cos) + (down * sin);
				var y = centreY + (down * cos) - (across * sin);

				var pixelX = offset + ((1024f + (x * 1024f)) * scale);
				var pixelY = (768f + (y * 768f)) * scale;

				_vertices[vertex] = new Vertex
				{
					Position = new Vector3( (2f * pixelX / Screen.Width) - 1f, 1f - (2f * pixelY / Screen.Height), 0f ),
					TexCoords = new Vector2( u, v ),
					TexIndex = ignoresAlpha ? 1 : 0,
					MatFlags = colour
				};
			}
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

			if ( _used > 0 )
				_model.Draw();
		}
	}
}
