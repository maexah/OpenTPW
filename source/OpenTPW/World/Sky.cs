using StbImageSharp;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// The lobby's sky and a park's, built the way the original builds them. The notes below are the lobby's;
/// the constructor says how a park's differs.
///
/// It is not a coloured dome. The original's lobby (FUN_005d8b50) hands the hard-coded path
/// <c>Data\Levels\fantasy</c> to its sky loader (FUN_005852b0), so every island in the lobby sits
/// under Wonder Land's sky and nothing ever swaps it. That sky is two pieces:
///
/// - A short band around the horizon textured with <c>sky_cyl.tga</c>, 720 units out and only 72
///   tall. It carries the whole horizon gradient, and its vertices are drawn plain white - no
///   ramp, no tint - so this part of the sky is permanently blue whatever a park asks for.
/// - Four cloud layers on a wide, shallow dome above it, textured with <c>sky.tga</c>. All four
///   share one 16x16 grid of positions and differ only in how far the texture tiles across it,
///   how fast it scrolls and how opaque it is.
///
/// The cloud layers are coloured by a 256-entry ramp, one copy per layer, normally a 16x16
/// downsample of <c>sky_rgb.tga</c> - a gradient from pale cyan to deep blue. Each lobby frame
/// FUN_005d96c0 floods <b>the first copy only</b> with a single colour, and that colour is the
/// park's SKYCOLOUR. So SKYCOLOUR reaches exactly one of the four cloud layers; the horizon band
/// and the other three stay as their textures paint them.
///
/// Two things gate even that in the original, both left out here, because nothing yet reads
/// the detail file's SKYQUALITY (Q32) and there is no software renderer: the tint only applies when SKYQUALITY is above 1 - the Low preset sets 1,
/// Medium 2, High 4 - and when the hardware path is running. On Low the ramp is flooded with the
/// fog colour instead, and the lobby sky is flatly, literally blue.
/// </summary>
public class Sky : Entity
{
	/// <summary>
	/// Where the lobby's sky art comes from. The original's lobby hands its texture loader the
	/// hard-coded <c>Data\Levels\fantasy</c> (FUN_005d8b50), so every island sits under Wonder
	/// Land's sky. A park passes its own theme's folder instead: the same three files ship under
	/// every level, and the loader (FUN_005852b0) builds its paths the same way for both.
	/// </summary>
	public const string LobbyDirectory = "levels/fantasy/sky";

	/// <summary>Where this sky's art comes from - see <see cref="LobbyDirectory"/>.</summary>
	private readonly string _directory;

	/// <summary>How wide the cloud grid is, from the sky object's own init (FUN_00584ef0, 0x960).</summary>
	private const float GridExtent = 2400f;

	/// <summary>
	/// How high the sky's centre sits in the lobby, from its call to FUN_00585690( 0, 180, 0 ).
	/// </summary>
	public const float LobbyHeight = 180f;

	/// <summary>
	/// The height the sky gives itself: FUN_00584ef0 writes 300 to +0x1f68 with the centre at
	/// (0, 0), and FUN_005856c0 puts all three back there. That is what a park runs at, because
	/// <b>nothing re-centres the sky for a park</b> - the setter's only caller is the lobby, and the
	/// only other caller of the rebuild is engine init (FUN_00572780, its " (tload/rain/sky ok)" step).
	/// </summary>
	public const float ParkHeight = 300f;

	/// <summary>How high this sky's centre sits.</summary>
	private readonly float _height;

	/// <summary>
	/// How far the dome droops per unit of horizontal distance from its centre - the constant at
	/// 0x00701f54. It is what turns a flat grid into a sky: the middle is overhead and the edges
	/// come down to meet the horizon band, which they reach exactly at its radius.
	/// </summary>
	private const float Droop = 0.2f;

	/// <summary>The grid is 16 vertices square, so 15 steps across <see cref="GridExtent"/>.</summary>
	private const int GridSize = 16;
	private const float GridStep = GridExtent / (GridSize - 1);

	/// <summary>
	/// The horizon band's radius - 0.6 of the grid's half width, from 0x00701f58 - and the four
	/// radii its rings sit at, 1, 0.98, 0.96 and 0.84 of it (0x00701f64 onwards). It tapers
	/// slightly toward the bottom rather than being a true cylinder.
	/// </summary>
	private const float BandRadius = GridExtent * 0.5f * 0.6f;
	private static readonly float[] RingRadii = [1f, 0.98f, 0.96f, 0.84f];

	/// <summary>
	/// Half the band's height, which is where the dome meets it. It follows the sky's own height, so
	/// a park's band reaches as much higher than the lobby's as its dome does.
	/// </summary>
	private readonly float _bandTop;

	/// <summary>
	/// How many columns each half of the horizon band is drawn in. The original walks 0 to pi in
	/// nine steps and pi to 2pi in nine more, with the texture running 0 to 1 across each half -
	/// and sky_cyl.tga is the same gradient stacked twice, so each half gets one copy of it.
	/// </summary>
	private const int BandColumns = 9;

	/// <summary>
	/// The V coordinate of each of the band's four rings, per half, read out of the draw
	/// (FUN_005863c0). The lobby picks a different set from a park: its span covers a whole copy
	/// of the texture and repeats the top row at the bottom, which mirrors the gradient below the
	/// horizon the way haze over water would.
	/// </summary>
	private static readonly float[] UpperV = [0.99f, 0.69f, 0.51f, 0.99f];
	private static readonly float[] LowerV = [0.49f, 0.19f, 0.01f, 0.49f];

	/// <summary>
	/// How many ticks of cloud scroll go by in a second.
	///
	/// The sky's step (FUN_00585f10) works out <c>frameTime * 25.0</c> - the constant at 0x00701f7c -
	/// and adds <c>rate * step</c> to each layer's scroll offset, dropping the step outright when it
	/// comes out past +/-25, which is to say when frameTime passes one. <b>That the field is seconds
	/// is read off that guard</b> rather than out of the code that writes it, which has not been
	/// found; either way the 25.0 is the sky's own conversion.
	///
	/// This is the sky's clock and not the lobby's, which counts ten a second - see
	/// <see cref="LobbyScript.TicksPerSecond"/>. The sky runs in a park too, and the lobby's delta
	/// lives on an object that does not exist outside the lobby.
	/// </summary>
	private const float TicksPerSecond = 25f;

	/// <summary>
	/// One of the four cloud layers, from the loop in FUN_00584ef0.
	///
	/// <c>Tiling</c> is (7 - layer) * pi / 112, so the first layer repeats the cloud texture about
	/// three times across the grid and the last about half as often. The scroll rates are per tick
	/// of the sky's own clock, twenty-five a second - see <see cref="TicksPerSecond"/> - restated
	/// here per second. <c>Opacity</c> starts at 0xCF and steps down by 0x38 a layer, so the first
	/// is nearly solid and the last is a suggestion.
	/// </summary>
	private readonly record struct CloudLayer( float Tiling, Vector2 Scroll, float Opacity );

	private static CloudLayer[] BuildLayers()
	{
		var layers = new CloudLayer[4];

		for ( int i = 0; i < layers.Length; ++i )
		{
			layers[i] = new CloudLayer(
				Tiling: (7 - i) * MathF.PI / 112f,
				Scroll: new Vector2( 0.0008f + (i * 0.0005f), 0.00009f - (i * 0.00003f) )
					* TicksPerSecond,
				Opacity: (0xCF - (i * 0x38)) / 255f );
		}

		return layers;
	}

	/// <summary>
	/// The colour flooded into the first cloud layer's ramp - a park's SKYCOLOUR, 0-1 per channel.
	/// <see cref="LobbyWeather"/> eases it between parks and feeds it here.
	///
	/// The original gets this wrong, provably: its ease reads the red target out of the blue slot
	/// (<c>mov edx,[ecx+0x58]</c> at 0x005d96fc, where the red target at +0x50 is compared and
	/// then never used), so red always converges on blue and no park receives what it wrote -
	/// jungle's 243,203,191 arrives as 191,203,191 and fantasy's 5,170,255 as 255,170,255. This
	/// passes the colour through as the scripts wrote it.
	/// </summary>
	public Vector3 Tint { get; set; } = LobbyScript.DefaultSkyColour;


	private readonly SkyPiece[] _pieces;
	private readonly CloudLayer[] _layers;
	private readonly byte[] _tintPixel = [255, 255, 255, 255];
	/// <summary>
	/// The one-pixel ramp the first cloud layer is drawn through, flooded with <see cref="Tint"/>
	/// every frame - or <b>null where nothing floods it</b>, which is a park. That null is the whole
	/// difference between the two skies: it decides whether the first layer is tinted or keeps the
	/// gradient like the other three, and whether this sky writes <see cref="Level.FogColour"/>.
	/// See the constructor.
	/// </summary>
	private readonly Texture? _tintRamp;

	/// <summary>
	/// The rest of what this sky built for itself, kept only so that it can let go of them again.
	/// Every one is made from pixels rather than loaded by path, so none is in the cache that scenes
	/// share and nothing else can be holding them - see <see cref="OnDelete"/>.
	/// </summary>
	private readonly Texture _bandTexture;
	private readonly Texture _cloudTexture;
	private readonly Texture _bandRamp;
	private readonly Texture _gradient;

	// What the sky averages out to, kept so its colour at the horizon can be worked out each
	// frame without reading anything back off the GPU - see HorizonColour.
	private readonly Vector3 _bandColour;
	private readonly Vector3 _gradientColour;
	private readonly float _cloudCoverage;

	/// <summary>
	/// A sky over <paramref name="directory"/>, centred on <paramref name="centre"/> with its middle
	/// <paramref name="height"/> above the ground. The defaults are the lobby's, so the lobby builds
	/// one by asking for nothing.
	///
	/// <para>
	/// <paramref name="tinted"/> is the one switch that separates the lobby's sky from a park's, and
	/// it is one rather than two because both halves follow from the same fact. The lobby floods the
	/// first cloud layer's ramp with the selected park's SKYCOLOUR every frame (FUN_005d96c0); a park
	/// has no island script, and nothing in the original ever floods it there, so <b>all four layers
	/// keep the gradient sampled out of sky_rgb.tga</b>. And because the lobby's horizon then moves
	/// with whichever park the camera is on, the lobby's sky is what decides
	/// <see cref="Level.FogColour"/> - while a park's fog is its own ThemeEngine.FogColour, set once
	/// as the level is built, so a park's sky has to leave it alone.
	/// </para>
	/// </summary>
	public Sky( string? directory = null, Vector3? centre = null, float height = LobbyHeight,
		bool tinted = true )
	{
		_directory = directory ?? LobbyDirectory;
		_height = height;
		_bandTop = height - (BandRadius * Droop);

		var middle = centre ?? LobbyCentre();
		var layers = BuildLayers();

		_layers = layers;

		// Loaded once and shared: four cloud layers drawing the same file have no reason to hold four
		// copies of it. This sky owns both and lets go of them in OnDelete, which is why they are kept
		// in fields rather than passed straight through.
		_bandTexture = LoadTexture( $"{_directory}/sky_cyl.tga", out var bandAverage );
		_cloudTexture = LoadTexture( $"{_directory}/sky.tga", out var cloudAverage );

		var bandTexture = _bandTexture;
		var cloudTexture = _cloudTexture;

		_bandColour = new Vector3( bandAverage.X, bandAverage.Y, bandAverage.Z );
		_cloudCoverage = cloudAverage.W;

		// No ramp to flood where nothing floods it - see the note on tinted above.
		_tintRamp = tinted ? new Texture( _tintPixel, 1, 1 ) : null;

		_gradient = GradientRamp( _directory, out _gradientColour );
		_bandRamp = White();

		var gradient = _gradient;
		var pieces = new List<SkyPiece>
		{
			// The band goes down first: it is the furthest thing there is, and the clouds are
			// drawn over it.
			SkyPiece.Band( middle, _bandTop, bandTexture, _bandRamp )
		};

		for ( int i = 0; i < layers.Length; ++i )
		{
			// Only the first ramp copy is flooded, so only the first layer is tinted; the rest
			// keep the gradient sampled out of sky_rgb.tga. Where nothing floods it at all - a
			// park - that layer keeps the gradient too, so the sky is four layers of it.
			var ramp = (i == 0 ? _tintRamp : null) ?? gradient;

			pieces.Add( SkyPiece.Clouds( middle, _height, _bandTop, layers[i], cloudTexture, ramp ) );
		}

		// The lobby's own flag (0x2000000, set by FUN_005dcfe0 and tested at the end of the draw)
		// draws all of that a second time upside down, as a bowl below. That is not reproduced,
		// and the reason is the sea.
		//
		// The original's lobby has none: its islands hang around a globe, so below the horizon is
		// open sky and the bowl is what fills it. Ours sits on an ocean that runs out to ten
		// thousand and covers everything below the horizon by itself. The dome already reaches
		// three to six degrees below eye level - further down than the sea's own far edge - so
		// there is no gap for the bowl to fill, and every triangle of it would be behind water.
		// Drawn here, it would only show through as a second bank of cloud with a ridge of
		// grid-shaped peaks along the horizon.

		_pieces = [.. pieces];
	}

	/// <summary>
	/// Lets go of the textures this sky built.
	/// </summary>
	/// <remarks>
	/// Its pieces are entities, so their models and materials go when they do. A material leaves the
	/// textures bound into it alone on purpose, because those are normally cached by path and shared
	/// across scenes - but a sky's are not. Every one is built from pixels for this sky alone, so
	/// nothing else can be holding them and nothing else lets go of them.
	/// </remarks>
	protected override void OnDelete()
	{
		// The blank is one instance that every empty material slot in the game holds, and
		// Texture.Delete refuses it loudly. LoadTexture hands it back when the art will not read, so
		// it is skipped here rather than warned about once a scene.
		static void Release( Texture? texture )
		{
			if ( texture != null && !ReferenceEquals( texture, Texture.Missing ) )
				texture.Delete();
		}

		Release( _bandTexture );
		Release( _cloudTexture );
		Release( _bandRamp );
		Release( _gradient );
		Release( _tintRamp );
	}

	protected override void OnUpdate()
	{
		// A park's sky does neither of these. No SKYCOLOUR reaches it, so there is no ramp to flood;
		// and its fog is the theme's own, set once as the level is built. See the constructor.
		if ( _tintRamp == null )
			return;

		_tintPixel[0] = Component( Tint.X );
		_tintPixel[1] = Component( Tint.Y );
		_tintPixel[2] = Component( Tint.Z );

		_tintRamp.UpdatePixels( _tintPixel );

		Level.FogColour = HorizonColour();
	}

	/// <summary>
	/// What the sky comes to where it meets the horizon: the four cloud layers composited in the
	/// order they are drawn, each covering the average fraction of sky.tga that is opaque, and
	/// then divided by how much of the result they actually cover.
	///
	/// That division is what makes it a fixed point - the colour the sky settles on if you keep
	/// laying these layers over it - and it is why the horizon band is not in here. The band spans
	/// 36 either side of the water, and the lobby camera sits between 32 and 58 above it, so at the
	/// horizon the camera is level with the band's top or above it and a horizontal ray misses the
	/// band entirely.
	///
	/// This is what distance hazes toward and what the frame is cleared to behind the gaps in the
	/// clouds, so it has to follow the park: a distant island on a storming Halloween hazes into
	/// the storm rather than into a bright blue day.
	/// </summary>
	private Vector3 HorizonColour()
	{
		var colour = Vector3.Zero;
		var covered = 0f;

		for ( int i = 0; i < _layers.Length; ++i )
		{
			// Only the first layer's ramp carries the park's colour, and only where one is flooded
			// into it at all; the rest keep the gradient.
			var layer = i == 0 && _tintRamp != null ? Tint : _gradientColour;
			var alpha = (_cloudCoverage * _layers[i].Opacity).Clamp( 0f, 1f );

			colour = colour.LerpTo( layer, alpha );
			covered = covered.LerpTo( 1f, alpha );
		}

		return covered > 0.001f ? colour / covered : _bandColour;
	}

	private static byte Component( float value ) => (byte)(value.Clamp( 0f, 1f ) * 255f);

	/// <summary>
	/// Where the sky is centred. The original puts it at the world origin because its islands sit
	/// on a globe there; ours are laid out flat around the middle of the four
	/// ISLANDCAMERAPOSITION entries, so the sky goes there instead.
	/// </summary>
	private static Vector3 LobbyCentre() => new( 500f, 500f, 0f );

	private static Texture White() => new( [255, 255, 255, 255], 1, 1 );

	/// <summary>
	/// The cloud layers' colour ramp: sky_rgb.tga reduced to 16x16 by sampling the middle of each
	/// cell, which is how FUN_00585ce0 builds the original's table.
	/// </summary>
	private static Texture GradientRamp( string directory, out Vector3 average )
	{
		var path = $"{directory}/sky_rgb.tga";
		var image = Decode( path );

		average = Vector3.One;

		if ( image == null || image.Width < GridSize || image.Height < GridSize )
		{
			Log.Warning( $"Sky: '{path}' is unreadable - the cloud layers will draw untinted" );
			return White();
		}

		var stepX = image.Width / GridSize;
		var stepY = image.Height / GridSize;
		var ramp = new byte[GridSize * GridSize * 4];

		for ( int row = 0; row < GridSize; ++row )
		{
			for ( int col = 0; col < GridSize; ++col )
			{
				var x = (stepX / 2) + (col * stepX);
				var y = (stepY / 2) + (row * stepY);

				var from = ((y * image.Width) + x) * 4;
				var to = ((row * GridSize) + col) * 4;

				image.Data.AsSpan( from, 4 ).CopyTo( ramp.AsSpan( to, 4 ) );
			}
		}

		var mean = Average( ramp );
		average = new Vector3( mean.X, mean.Y, mean.Z );

		return new Texture( ramp, GridSize, GridSize );
	}

	/// <summary>The mean of every pixel, 0-1 per channel, alpha in W.</summary>
	private static Vector4 Average( byte[] pixels )
	{
		if ( pixels.Length < 4 )
			return Vector4.One;

		double r = 0, g = 0, b = 0, a = 0;

		for ( int i = 0; i + 3 < pixels.Length; i += 4 )
		{
			r += pixels[i];
			g += pixels[i + 1];
			b += pixels[i + 2];
			a += pixels[i + 3];
		}

		var count = (pixels.Length / 4) * 255d;

		return new Vector4( (float)(r / count), (float)(g / count), (float)(b / count), (float)(a / count) );
	}

	/// <summary>
	/// The sky art is a loose .tga in the game's own directories rather than a .wct in an archive,
	/// so it goes through FileSystem the way the weather art does.
	/// </summary>
	private static ImageResult? Decode( string path )
	{
		using var stream = FileSystem.OpenRead( path );

		if ( stream == null )
			return null;

		var bytes = new byte[stream.Length];
		stream.ReadExactly( bytes );

		return ImageResult.FromMemory( bytes, ColorComponents.RedGreenBlueAlpha );
	}

	private static Texture LoadTexture( string path, out Vector4 average )
	{
		var image = Decode( path );

		average = Vector4.One;

		if ( image == null )
		{
			Log.Warning( $"Sky texture '{path}' is missing - that part of the sky will draw white" );
			return Texture.Missing;
		}

		average = Average( image.Data );

		// Wrapped rather than clamped, because the cloud layers tile the texture across the grid
		// several times over and scroll it endlessly.
		return new Texture( image.Data, image.Width, image.Height, TextureFlags.Wrap );
	}

	/// <summary>
	/// One drawn piece of the sky. Each needs its own uniforms - its own tiling, scroll and ramp -
	/// and a material carries one uniform buffer, so each is an entity of its own rather than one
	/// mesh drawn several times over.
	/// </summary>
	private sealed class SkyPiece : ModelEntity
	{
		[StructLayout( LayoutKind.Sequential )]
		private struct SkyUniformBuffer
		{
			public Matrix4x4 g_mModel;
			public Matrix4x4 g_mView;
			public Matrix4x4 g_mProj;

			public Vector4 g_vTint;
			public Vector4 g_vUv;
			public Vector4 g_vRamp;

			/// <summary>The haze colour in xyz - see content/shaders/sky.</summary>
			public Vector4 g_vFog;

			/// <summary>Eye height in x, how far above it the haze clears in y.</summary>
			public Vector4 g_vHaze;
		}

		private readonly Vector4 _tint;
		private readonly Vector4 _ramp;
		private readonly Vector2 _scroll;

		/// <summary>
		/// How far above eye level the sky's haze clears.
		///
		/// The sky is not fogged by distance the way everything else is - see the note in
		/// content/shaders/sky. It hazes by height, fully at eye level and not at all this far above
		/// it, which is what seals the join with the sea: the dome comes down to eye level exactly
		/// where the sea reaches the horizon, so both sides of that line are the fog colour whatever
		/// colour that is and wherever the camera sits.
		///
		/// The height is a matter of how many degrees of haze look right rather than of anything read
		/// out of the original, and the horizon band's own half-height is the scale the sky already
		/// works in. It puts the haze in the last four degrees or so above the horizon - and because
		/// it is the band's top, it follows the sky's own height, so a park hazes over a taller band
		/// than the lobby does.
		/// </summary>
		private readonly float _hazeHeight;

		private Vector4 _uv;

		private SkyPiece( Vertex[] vertices, uint[] indices, Texture texture, Texture ramp,
			Vector4 tint, Vector4 uv, Vector4 rampMapping, Vector2 scroll, float hazeHeight )
		{
			_tint = tint;
			_uv = uv;
			_ramp = rampMapping;
			_scroll = scroll;
			_hazeHeight = hazeHeight;

			// Never writing depth, because the sky is behind everything and drawn before it -
			// letting it write would stand a 720-unit wall in front of an ocean that runs out to
			// ten thousand. No culling either: the camera is inside all of this.
			var material = new Material<SkyUniformBuffer>( "content/shaders/sky.shader",
				MaterialFlags.DisableDepthWrite | MaterialFlags.DisableCulling );

			material.Set( "Color", texture );
			material.Set( "Ramp", ramp );

			Model = new Model( vertices, indices, material );
		}

		/// <summary>The horizon band: two halves of <see cref="BandColumns"/> columns, four rings each.</summary>
		public static SkyPiece Band( Vector3 centre, float bandTop, Texture texture, Texture ramp )
		{
			var vertices = new List<Vertex>();
			var indices = new List<uint>();

			// The halves are built and indexed separately, as the original does. They meet at pi
			// on a duplicated column, so there is no quad to bridge and no seam to see.
			AddBandHalf( vertices, indices, centre, bandTop, 0f, UpperV );
			AddBandHalf( vertices, indices, centre, bandTop, MathF.PI, LowerV );

			return new SkyPiece( [.. vertices], [.. indices], texture, ramp,
				tint: Vector4.One,
				uv: new Vector4( 1f, 1f, 0f, 0f ),
				rampMapping: Vector4.Zero,
				scroll: Vector2.Zero,
				hazeHeight: bandTop )
			{ Name = "Sky horizon" };
		}

		private static void AddBandHalf( List<Vertex> vertices, List<uint> indices, Vector3 centre,
			float bandTop, float startAngle, float[] ringV )
		{
			// The two middle rings sit a quarter and a half of the way from the top to the bottom,
			// from the constants at 0x00701f5c and 0x00701f50.
			var heights = new[]
			{
				bandTop,
				bandTop - (bandTop * 0.5f),
				0f,
				-bandTop
			};

			var first = (uint)vertices.Count;

			for ( int column = 0; column < BandColumns; ++column )
			{
				var angle = startAngle + (MathF.PI * column / (BandColumns - 1));
				var cos = MathF.Cos( angle );
				var sin = MathF.Sin( angle );
				var u = column / (float)(BandColumns - 1);

				for ( int ring = 0; ring < RingRadii.Length; ++ring )
				{
					var radius = BandRadius * RingRadii[ring];

					vertices.Add( new Vertex(
						new Vector3( centre.X + (cos * radius), centre.Y + (sin * radius), heights[ring] ),
						new Vector2( u, ringV[ring] ) ) );
				}
			}

			for ( uint column = 0; column < BandColumns - 1; ++column )
			{
				for ( uint ring = 0; ring < RingRadii.Length - 1; ++ring )
				{
					var v = first + (column * (uint)RingRadii.Length) + ring;
					var next = v + (uint)RingRadii.Length;

					indices.AddRange( [v, next + 1, next, v, v + 1, next + 1] );
				}
			}
		}

		/// <summary>One cloud layer on the shared 16x16 dome.</summary>
		public static SkyPiece Clouds( Vector3 centre, float skyHeight, float bandTop, CloudLayer layer,
			Texture texture, Texture ramp )
		{
			var vertices = new Vertex[GridSize * GridSize];
			var heights = new float[GridSize * GridSize];

			for ( int row = 0; row < GridSize; ++row )
			{
				for ( int col = 0; col < GridSize; ++col )
				{
					var x = (col * GridStep) - (GridExtent * 0.5f);
					var y = (row * GridStep) - (GridExtent * 0.5f);
					var height = skyHeight - (MathF.Sqrt( (x * x) + (y * y) ) * Droop);

					var index = (row * GridSize) + col;
					heights[index] = height;

					// The grid index is the texture coordinate and the shader scales and scrolls
					// it, so all four layers build this same mesh and never touch a vertex buffer.
					vertices[index] = new Vertex(
						new Vector3( centre.X + x, centre.Y + y, height ),
						new Vector2( col, row ) );
				}
			}

			var indices = new List<uint>();

			for ( int row = 0; row < GridSize - 1; ++row )
			{
				for ( int col = 0; col < GridSize - 1; ++col )
				{
					var v = (row * GridSize) + col;

					// The dome droops well past the bottom of the horizon band at its corners, and
					// the original drops any triangle that reaches below it - which is what stops
					// the far corners hanging down through the world.
					AddIfAbove( indices, heights, bandTop, v + 1, v, v + GridSize + 1 );
					AddIfAbove( indices, heights, bandTop, v, v + GridSize, v + GridSize + 1 );
				}
			}

			var min = centre - new Vector3( GridExtent * 0.5f, GridExtent * 0.5f, 0f );

			return new SkyPiece( vertices, [.. indices], texture, ramp,
				tint: new Vector4( 1f, 1f, 1f, layer.Opacity ),
				uv: new Vector4( layer.Tiling, layer.Tiling, 0f, 0f ),
				// One over the span the sixteen ramp texels cover, plus the half texel that lands
				// vertex (col,row) on texel (col,row) instead of between two of them.
				rampMapping: new Vector4( min.X, min.Y, 1f / (GridStep * GridSize), 0.5f / GridSize ),
				scroll: layer.Scroll,
				hazeHeight: bandTop )
			{ Name = $"Sky clouds (x{layer.Tiling:F2}, {layer.Opacity:P0})" };
		}

		private static void AddIfAbove( List<uint> indices, float[] heights, float bandTop, int a, int b, int c )
		{
			if ( heights[a] > -bandTop && heights[b] > -bandTop && heights[c] > -bandTop )
				indices.AddRange( [(uint)a, (uint)b, (uint)c] );
		}

		protected override void OnUpdate()
		{
			// Scrolling in the uniform rather than in the vertices, so a layer costs one buffer
			// write a frame instead of two hundred and fifty six.
			_uv.Z += _scroll.X * Time.Delta;
			_uv.W += _scroll.Y * Time.Delta;

			// Kept inside a single turn of the texture. Left to run, these reach the point where a
			// float can no longer tell one texel from the next and the clouds visibly quantise.
			_uv.Z -= MathF.Floor( _uv.Z );
			_uv.W -= MathF.Floor( _uv.W );
		}

		protected override void OnRender()
		{
			if ( Model == null )
				return;

			Model.Material.Set( "g_oUbo", new SkyUniformBuffer
			{
				// Vertices are already in world space, so there is no transform of its own here.
				g_mModel = Matrix4x4.Identity,
				g_mView = Camera.ViewMatrix,
				g_mProj = Camera.ProjMatrix,

				g_vTint = _tint,
				g_vUv = _uv,
				g_vRamp = _ramp,
				g_vFog = new Vector4( Level.FogColour.X, Level.FogColour.Y, Level.FogColour.Z, 0f ),
				g_vHaze = new Vector4( Camera.Position.Z, _hazeHeight, 0f, 0f )
			} );

			Model.Draw();
		}
	}
}
