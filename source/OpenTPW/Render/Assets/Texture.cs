using StbImageSharp;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NeoVeldrid;

namespace OpenTPW;

public partial class Texture : Asset
{
	public SamplerType SamplerType { get; private set; } = SamplerType.AnisotropicRepeat;

	public uint Width { get; private set; }
	public uint Height { get; private set; }

	/// <summary>
	/// The flags this texture was ASKED for, recorded before the cache is consulted so that it says
	/// the same thing whether the file was read or an already-loaded texture was taken over.
	/// <see cref="SamplerType"/> is what the texture ended up being drawn with; the two are meant to
	/// agree, and this is here so that a harness can see whether they do.
	/// </summary>
	public TextureFlags Requested { get; private set; }

	/// <summary>
	/// Whether this took over an already-loaded texture's GPU handles instead of reading the file -
	/// see <see cref="TryAdoptCached"/>.
	/// </summary>
	public bool Adopted { get; private set; }

	/// <summary>
	/// How this texture's alpha is drawn: a .wct's is its file's (<see cref="TextureFile.Alpha"/>, the original's
	/// <c>FUN_00574bd0</c>); any other's is <see cref="Classify"/>'s count of its pixels. A model's material takes its
	/// alpha test from this alone (<c>LobbyModel</c>).
	/// </summary>
	public TextureAlpha Alpha { get; private set; }

	/// <summary>
	/// The alpha a cut-out texel has to reach to be drawn at all, 240 of 255. The original sets
	/// ALPHAFUNC to GREATEREQUAL once and never changes it, and picks this reference out of the
	/// pair at DAT_007012d8 for art whose alpha is effectively binary - so a frond is either there
	/// or it isn't, and the soft ring the .wct codec leaves around every hard edge is dropped
	/// rather than blended.
	/// </summary>
	private const byte CutOutAlphaReference = 240;

	/// <summary>
	/// A one-pixel white stand-in, for a material slot naming no texture of its own. One instance, handed
	/// to everyone who asks.
	///
	/// <para>
	/// It is built once and kept because the cache cannot share it: the constructor it calls passes an empty
	/// path, and <see cref="TryGetCachedTexture"/> refuses an empty path before it looks anything up, so a
	/// blank built on each read would be a GPU texture, a device submit and a loading-bar step apiece.
	/// </para>
	///
	/// <para>
	/// <b>Being shared, it must not be written to or released.</b> <see cref="UpdatePixels"/> rewrites a
	/// texture's pixels where they lie and <see cref="Delete"/> frees its GPU handles; either one done to
	/// this would reach every material holding it at once. Both refuse it and say so, rather than leaving a
	/// reader to remember - nothing in the game does either to it today, and the point of the guard is that
	/// nothing can quietly start to.
	/// </para>
	/// </summary>
	public static Texture Missing => _missing ??= new Texture( [255, 255, 255, 255], 1, 1 );

	/// <summary>The one blank, built on first use - see <see cref="Missing"/>.</summary>
	private static Texture? _missing;

	/// <summary>The game's own stand-in for a texture it can't load - see <see cref="NotFound"/>.</summary>
	private const string NotFoundPath = "generic/defaulttexture/NotFound.tga";

	/// <summary>Decoded once; <see cref="NotFound"/> hands out copies.</summary>
	private static TextureData? _notFound;

	internal NeoVeldrid.Texture NativeTexture;

	/// <summary>The bytes this texture counted into <see cref="GpuMemory"/>, taken back at <see cref="Delete"/>.</summary>
	private long _gpuBytes;
	internal TextureView NativeTextureView;

	/// <summary>
	/// From path on disk, or from a game resource
	/// </summary>
	public Texture( string path, TextureFlags flags = TextureFlags.None )
	{
		Requested = flags;

		// Before the file is touched, not after it has been decoded - see TryAdoptCached, which
		// carries the measurement. A model names the same texture once per mesh that uses it, so
		// this is the difference between decoding a .wct once and decoding it eighteen times.
		if ( TryAdoptCached( path, flags ) )
			return;

		if ( path.HasExtension( ".wct" ) )
			UpdateFromWct( path, flags );
		else
			UpdateFromStb( path, flags );
	}

	/// <summary>
	/// From data as bytes
	/// </summary>
	public Texture( byte[] data, int width, int height, TextureFlags flags = TextureFlags.None )
	{
		CreateTexture( "", data, (uint)width, (uint)height, flags );
	}

	public Texture( Stream stream, TextureFlags flags = TextureFlags.None )
	{
		var fileData = new byte[stream.Length];
		stream.ReadAtLeast( fileData, fileData.Length, throwOnEndOfStream: false );

		var image = ImageResult.FromMemory( fileData, ColorComponents.RedGreenBlueAlpha );

		var data = image.Data;
		var width = (uint)image.Width;
		var height = (uint)image.Height;
		var debugName = $"Stream {stream.GetHashCode()}";

		CreateTexture( debugName, data, width, height, flags );
	}

	/// <summary>
	/// Update from an image format supported by the STB library (jpg, png, gif, tga, etc.)
	/// </summary>
	[MemberNotNull( nameof( NativeTexture ), nameof( NativeTextureView ) )]
	private void UpdateFromStb( string path, TextureFlags flags )
	{
		var fileData = File.ReadAllBytes( path );
		var image = ImageResult.FromMemory( fileData, ColorComponents.RedGreenBlueAlpha );

		var data = image.Data;
		var width = (uint)image.Width;
		var height = (uint)image.Height;

		CreateTexture( path, data, width, height, flags );
	}

	/// <summary>
	/// Update from a Bullfrog WCT file
	/// </summary>
	[MemberNotNull( nameof( NativeTexture ), nameof( NativeTextureView ) )]
	private void UpdateFromWct( string path, TextureFlags flags )
	{
		var (image, alpha) = ReadWct( path );

		CreateTexture( path, image.Data, (uint)image.Width, (uint)image.Height, flags, alpha );
	}

	/// <summary>
	/// A .wct's pixels and alpha, read through <see cref="FileSystem"/> with no device: the alpha its file's
	/// (<see cref="TextureFile.Alpha"/>). A file that could not be read is opaque, and so is its stand-in
	/// (<see cref="NotFound"/>), the original's notfound.tga. Logs each cut-out texture.
	/// </summary>
	internal static (TextureData Image, TextureAlpha Alpha) ReadWct( string path )
	{
		var file = new TextureFile( path );
		var image = file.IsValid ? file.Data : NotFound( path );

		if ( file.Alpha == TextureAlpha.CutOut )
		{
			var pixels = image.Data;
			int clear = 0, under = 0;

			for ( int i = 3; i < pixels.Length; i += 4 )
			{
				clear += pixels[i] == 0 ? 1 : 0;
				under += pixels[i] < CutOutAlphaReference ? 1 : 0;
			}

			Log.Info( $"Texture: '{path}' reads its alpha chunk by its bit count, header byte 1 nought: {clear} of {pixels.Length / 4} texels clear, {under} under {CutOutAlphaReference}" );
		}

		return (image, file.Alpha);
	}

	/// <summary>
	/// What the original draws in place of a texture it cannot load.
	///
	/// Its texture cache installs Data\Generic\defaulttexture\notfound.tga as entry zero when it
	/// starts up, and the routine that resolves a model's materials assigns that entry to any
	/// frame whose texture it failed to find - the same branch that logs "Could not load texture
	/// '%s' from '%s' or '%s'". So a material naming a texture the game doesn't have is not an
	/// error the original refuses to draw; it has a shipped answer for it.
	///
	/// That answer is a plain brown noise tile rather than a loud debug colour, which is why the
	/// one material in the game that needs it goes unnoticed: hallow's lobby sign frames itself
	/// with a "signgrab" texture that appears nowhere in the data, and brown reads as weathered
	/// wood on a haunted sign where a loud debug colour would read as a bug.
	/// </summary>
	private static TextureData NotFound( string wanted )
	{
		Log.Warning( $"Could not load texture '{wanted}' - drawing the game's not-found texture instead" );

		if ( _notFound == null )
		{
			using var stream = FileSystem.OpenReadOrNull( NotFoundPath );

			// An install missing the not-found texture as well leaves TextureFile's own last
			// resort in place, which is the only thing left to draw.
			if ( stream == null )
				return new TextureFile( stream ).Data;

			var image = ImageResult.FromStream( stream, ColorComponents.RedGreenBlueAlpha );
			_notFound = new TextureData( image.Width, image.Height, image.Data );
		}

		// CreateTexture rewrites the pixels it is handed - chroma keying does - so every caller
		// gets its own copy rather than editing the one they all share.
		var texture = _notFound.Value;
		return texture with { Data = (byte[])texture.Data.Clone() };
	}

	/// <summary>
	/// Rewrites this texture's pixels in place, keeping the same GPU texture and view.
	///
	/// For anything that changes colour every frame this is the only sane route: constructing a
	/// Texture allocates a fresh GPU texture and adds it to <see cref="Asset.All"/>, and both are let
	/// go of only when whoever owns it calls <see cref="Delete"/> - so building one per frame would
	/// leak steadily however careful its owner is. The lobby sky's tint ramp is a 1x1 texture rewritten
	/// every frame with the colour of the park on show - see <see cref="Sky.Tint"/>.
	/// </summary>
	public void UpdatePixels( byte[] data )
	{
		// See Missing: one instance stands in for every blank material slot in the game, so rewriting its
		// pixel would repaint all of them.
		if ( ReferenceEquals( this, _missing ) )
		{
			Log.Warning( "Texture: the shared blank was asked to rewrite its pixel, which would repaint every material holding it - ignored" );
			return;
		}

		if ( NativeTexture == null || data.Length < Width * Height * 4 )
			return;

		// Cut only for a texture that does not hold OwnPixels; the three callers (LoadingScreen, UiText, Sky) all do, so
		// no caller reaches the cut branch today (dead by CODE).
		if ( Cut )
			Upload( data );
		else
			UpdateLevel( data, Width, Height, 0 );
	}

	/// <summary>
	/// Releases the GPU texture and takes this out of <see cref="Asset.All"/>, once the frame in
	/// progress is done with it.
	///
	/// Only for a texture nothing else can be holding. One constructed from a path that is already
	/// loaded shares that texture's GPU texture rather than owning one (see TryGetCachedTexture),
	/// so deleting either would pull it out from under the other. A texture built from pixels owns
	/// its own, with one exception: <see cref="Missing"/> is a single instance that every blank
	/// material slot in the game holds, and it is refused here rather than left to a caller to
	/// remember. Nothing deletes it today; this is so that nothing can begin to by accident.
	/// </summary>
	public void Delete()
	{
		if ( ReferenceEquals( this, _missing ) )
		{
			Log.Warning( "Texture: the shared blank was asked to delete itself, which would take it from every material holding it - ignored" );
			return;
		}

		All.Remove( this );

		if ( _gpuBytes != 0 )
		{
			GpuMemory.Remove( GpuMemory.Kind.Texture, _gpuBytes );
			_gpuBytes = 0;
		}

		var texture = NativeTexture;
		var view = NativeTextureView;

		Render.ScheduleDelete( () =>
		{
			view.Dispose();
			texture.Dispose();
		} );
	}

	private int CalculateMipLevels( int width, int height, int depth )
	{
		int maxDimension = Math.Max( width, Math.Max( height, depth ) );
		int mipLevels = (int)Math.Floor( Math.Log( maxDimension, 2 ) ) + 1;

		return mipLevels;
	}

	private void PreprocessTextureData( ref byte[] data, ref uint width, ref uint height, TextureFlags flags )
	{
		if ( flags == TextureFlags.None )
			return;

		if ( flags.HasFlag( TextureFlags.PinkChromaKey ) )
		{
			for ( int i = 0; i < data.Length; i += 4 )
			{
				var r = data[i];
				var g = data[i + 1];
				var b = data[i + 2];

				if ( r == 255 && g == 0 && b == 255 )
				{
					// Set alpha to 0
					data[i + 3] = 0;
				}
			}
		}
	}

	/// <summary>
	/// The alpha of a texture that is not a .wct, from its pixels, as the original's classifier <c>FUN_00575160</c> takes
	/// it for the other formats: graded when the texels strictly between nought and 240 are a twentieth of the area or
	/// more, cut out otherwise. One whose every texel is opaque is <see cref="TextureAlpha.Opaque"/> here where the
	/// original cuts a four-byte source out: a test at 240 drops none of its texels either way.
	/// </summary>
	internal static TextureAlpha Classify( byte[] data )
	{
		var texels = data.Length / 4;
		var partial = 0;
		var opaque = true;

		for ( int i = 3; i < data.Length; i += 4 )
		{
			if ( data[i] > 0 && data[i] < CutOutAlphaReference )
				++partial;

			opaque &= data[i] == 255;
		}

		return opaque ? TextureAlpha.Opaque : partial * 20 >= texels ? TextureAlpha.Graded : TextureAlpha.CutOut;
	}

	/// <summary>
	/// The sampler a request's flags ask for.
	///
	/// <para>
	/// Kept apart from <see cref="CreateTexture"/> so the rule can be tested without a graphics device,
	/// which a test run has none of. Repeat beats Wrap beats PointFilter, and asking for nothing
	/// leaves the same <see cref="SamplerType.AnisotropicRepeat"/> the field is declared with.
	/// </para>
	/// </summary>
	internal static SamplerType SamplerFor( TextureFlags flags )
	{
		if ( flags.HasFlag( TextureFlags.Repeat ) )
			return SamplerType.AnisotropicRepeat;

		if ( flags.HasFlag( TextureFlags.Wrap ) )
			return SamplerType.AnisotropicWrap;

		if ( flags.HasFlag( TextureFlags.PointFilter ) )
			return SamplerType.Point;

		return SamplerType.AnisotropicRepeat;
	}

	[MemberNotNull( nameof( NativeTexture ), nameof( NativeTextureView ) )]
	private void CreateTexture( string debugName, byte[] data, uint width, uint height, TextureFlags flags,
		TextureAlpha? alpha = null )
	{
		Requested = flags;

		// Still asked here as well as in the path constructor: this is also reached from the byte[]
		// and Stream constructors, which have no path to check beforehand.
		if ( TryAdoptCached( debugName, flags ) )
			return;

		PreprocessTextureData( ref data, ref width, ref height, flags );

		Alpha = alpha ?? Classify( data );

		SamplerType = SamplerFor( flags );

		uint mipLevels = (uint)CalculateMipLevels( (int)width, (int)height, 1 );

		var textureDescription = TextureDescription.Texture2D(
			width,
			height,
			mipLevels,
			1,
			PixelFormat.R8_G8_B8_A8_UNorm,
			TextureUsage.Sampled | TextureUsage.GenerateMipmaps
		);

		var texture = Device.ResourceFactory.CreateTexture( textureDescription );

		_gpuBytes = GpuMemory.TextureBytes( width, height, mipLevels );
		GpuMemory.Add( GpuMemory.Kind.Texture, _gpuBytes );

		Path = debugName;
		NativeTexture = texture;
		Width = width;
		Height = height;

		Upload( data );

		NativeTextureView = Device.ResourceFactory.CreateTextureView( texture );

		Register();
	}

	/// <summary>
	/// Every texture kept at eight bits a channel, the original's format 3 (A8R8G8B8): the default, by
	/// <see cref="KeepsEightBitsUnder"/>. Where not, the game's art is cut to four (<see cref="CutLevels"/>), as the
	/// shipped game draws it, banding and all. Set once at boot (<c>Game</c>), before the first texture.
	/// </summary>
	public static bool KeepsEightBits { get; set; } = true;

	/// <summary>
	/// Whether the game keeps eight bits a channel. <b>A deviation from the original</b>, on by default: OpenTPW keeps
	/// eight whatever the detail file says, where the original keeps eight only for <c>GraphicalOptions.TEXTURE32</c> 1,
	/// which no shipped detail file sets (all three hold 0), and otherwise runs format 2, A4R4G4B4 (<c>0x0044e1f4</c>;
	/// <c>textures.md</c>, "The texture cache's surface format"). <paramref name="originalDepth"/>
	/// (<see cref="GameOptions.Faithful"/>, opentpw.cfg's <c>graphics faithful</c>) takes the original's rule.
	/// </summary>
	internal static bool KeepsEightBitsUnder( bool originalDepth, bool texture32 ) => !originalDepth || texture32;

	/// <summary>
	/// What <c>Game</c> sets <see cref="KeepsEightBits"/> to at boot, and sets it: the options' depth, and the detail
	/// file's <c>TEXTURE32</c> read (through <paramref name="texture32"/>) only where the original's depth is asked for.
	/// </summary>
	internal static bool SetDepthFor( GameOptions options, Func<bool> texture32 )
		=> KeepsEightBits = KeepsEightBitsUnder( options.Faithful, options.Faithful && texture32() );

	/// <summary>Whether this texture's texels are cut to four bits a channel - see <see cref="CutLevels"/>.</summary>
	public bool Cut => CutsFor( Requested );

	/// <summary>Whether a texture asked for with <paramref name="flags"/> is cut: unless the game keeps eight bits or the pixels are OpenTPW's own.</summary>
	internal static bool CutsFor( TextureFlags flags ) => !KeepsEightBits && !flags.HasFlag( TextureFlags.OwnPixels );

	/// <summary>
	/// The levels of a texture asked for with <paramref name="flags"/>: every one made here (<see cref="CutLevels"/>) where
	/// the texture is cut (<see cref="CutsFor"/>), null where it is kept at eight bits and the device makes the mip maps.
	/// </summary>
	/// <remarks>
	/// Cutting costs time, under <c>graphics faithful</c> only. The park loads about 0.76 s slower with every texture cut than with eight bits (the
	/// totals over four runs, <c>q700/*/run.log</c>: 3.4 s against 4.2 s), the guest sprites 273 -> 729 ms, the ground
	/// 560 -> 691, the objects 717 -> 785, the fixed items 172 -> 206; the guest atlas is packed again, cut again, on
	/// every switch into and out of first person (<c>ParkGuestSprites.UseFirstPersonPictures</c>), about 0.45 s by the
	/// load figure, not measured.
	/// </remarks>
	internal static IEnumerable<(byte[] Pixels, int Width, int Height)>? LevelsFor( byte[] data, int width, int height, TextureFlags flags )
		=> CutsFor( flags ) ? CutLevels( data, width, height ) : null;

	/// <summary>
	/// Puts <paramref name="data"/> in every level: cut, every level made here (<see cref="LevelsFor"/>); kept at eight,
	/// the first level and the device's mip maps.
	/// </summary>
	private void Upload( byte[] data )
	{
		if ( LevelsFor( data, (int)Width, (int)Height, Requested ) is { } levels )
		{
			var level = 0u;

			foreach ( var (pixels, w, h) in levels )
				UpdateLevel( pixels, (uint)w, (uint)h, level++ );

			return;
		}

		UpdateLevel( data, Width, Height, 0 );

		Render.ImmediateSubmit( cmd =>
		{
			cmd.GenerateMipmaps( NativeTexture );
		} );
	}

	private void UpdateLevel( byte[] pixels, uint width, uint height, uint level )
	{
		var ptr = Marshal.AllocHGlobal( pixels.Length );
		Marshal.Copy( pixels, 0, ptr, pixels.Length );
		Device.UpdateTexture( NativeTexture, ptr, (uint)pixels.Length, 0, 0, 0, width, height, 1, level, 0 );
		Marshal.FreeHGlobal( ptr );
	}

	/// <summary>
	/// A channel as the shipped game's A4R4G4B4 surface holds it and the device hands it back: cut to its top four bits
	/// (<c>TexFormat_BuildChannelTables</c> <c>0x005839f0</c>, <c>v &gt;&gt; 4</c>, never rounded), and the nibble read
	/// as <c>n / 15</c>, the unsigned-normalised rule of Direct3D, OpenGL and Vulkan alike, so <c>n x 17</c> of 255.
	/// </summary>
	internal static byte CutChannel( byte value ) => (byte)((value >> 4) * 17);

	/// <summary>
	/// Every level of a cut texture, the first first, each channel of each through <see cref="CutChannel"/>, alpha too:
	/// the original stores every level of every texture in the one format (<c>textures.md</c>, "The texture cache's
	/// surface format"). How it fills a level below the first is not read; here each is the rounded mean of the eight-bit
	/// level above's two by two (its last row or column alone on an odd side), cut, down to one texel, where the
	/// original makes four levels (<c>0x0058da96</c>).
	/// </summary>
	internal static IEnumerable<(byte[] Pixels, int Width, int Height)> CutLevels( byte[] data, int width, int height )
	{
		var levels = (int)Math.Floor( Math.Log2( Math.Max( width, height ) ) ) + 1;
		var level = data;

		for ( var i = 0; i < levels; ++i )
		{
			var cut = new byte[width * height * 4];

			for ( var j = 0; j < cut.Length; ++j )
				cut[j] = CutChannel( level[j] );

			yield return (cut, width, height);

			if ( i + 1 == levels )
				break;

			var w = Math.Max( 1, width / 2 );
			var h = Math.Max( 1, height / 2 );
			var next = new byte[w * h * 4];

			for ( var y = 0; y < h; ++y )
				for ( var x = 0; x < w; ++x )
					for ( var c = 0; c < 4; ++c )
					{
						int sum = 0, count = 0;

						for ( var dy = 0; dy < 2 && (2 * y) + dy < height; ++dy )
							for ( var dx = 0; dx < 2 && (2 * x) + dx < width; ++dx )
							{
								sum += level[((((2 * y) + dy) * width) + (2 * x) + dx) * 4 + c];
								++count;
							}

						next[((y * w) + x) * 4 + c] = (byte)((sum + (count / 2)) / count);
					}

			level = next;
			width = w;
			height = h;
		}
	}
}
