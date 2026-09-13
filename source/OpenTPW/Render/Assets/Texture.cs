using StbImageSharp;
using System.Runtime.InteropServices;
using NeoVeldrid;

namespace OpenTPW;

public partial class Texture : Asset
{
	public SamplerType SamplerType { get; private set; } = SamplerType.AnisotropicRepeat;

	public uint Width { get; private set; }
	public uint Height { get; private set; }

	/// <summary>
	/// Whether this texture's alpha is a real gradient rather than a cut-out mask.
	///
	/// The original decides this from the pixels, not from anything a model says: FUN_00575160
	/// counts the texels whose alpha lies strictly between zero and the cut-out reference, and
	/// marks the texture graded once they reach a twentieth of its area (the 0.05 multiplier is
	/// at 0x00701720). A graded texture is then drawn with the low alpha reference and a cut-out
	/// one with the high reference - see <see cref="CutOutAlphaReference"/> and the shader.
	/// </summary>
	public bool HasGradedAlpha { get; private set; }

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
	/// This used to build another every time it was read - a GPU texture, a pixel copied into it, a device
	/// submit, and one more step of the loading bar - and it could never be served from the cache either,
	/// because the constructor it calls passes an empty path and <see cref="TryGetCachedTexture"/> refuses
	/// an empty path before it looks anything up. So no two were ever the same object.
	/// </para>
	///
	/// <para>
	/// <b>Measured on the lobby before it was shared:</b> 2,386 of the load's 3,214 registered assets were
	/// these - 74% of the bar - and building them cost 533ms of the 554ms the whole load spent making GPU
	/// textures. Sharing one takes the lobby from 3,214 registered assets to 829, and the load from 6.73s to
	/// 6.02s, each the mean of three runs. <c>LobbyModel</c> is what paid: it reads this inside a loop over
	/// sixteen material slots for every mesh of every model, so a mesh naming two materials minted fourteen
	/// blank textures. <c>Sky</c> and <c>WeatherSprites</c> want a single fallback and never paid anything;
	/// <c>UiMesh</c> had already kept its own with <c>_blank ??= Texture.Missing</c>.
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
	internal TextureView NativeTextureView;

	/// <summary>
	/// From path on disk, or from a game resource
	/// </summary>
	public Texture( string path, TextureFlags flags = TextureFlags.None )
	{
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
		stream.Read( fileData, 0, fileData.Length );

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
	private void UpdateFromWct( string path, TextureFlags flags )
	{
		var file = new TextureFile( path );
		var textureFileData = file.IsValid ? file.Data : NotFound( path );

		CreateTexture( path, textureFileData.Data, (uint)textureFileData.Width, (uint)textureFileData.Height, flags );
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
	/// wood on a haunted sign where magenta read as a bug.
	/// </summary>
	private static TextureData NotFound( string wanted )
	{
		Log.Warning( $"Could not load texture '{wanted}' - drawing the game's not-found texture instead" );

		if ( _notFound == null )
		{
			using var stream = FileSystem.OpenRead( NotFoundPath );

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
	/// Texture allocates a fresh GPU texture and adds it to <see cref="Asset.All"/>, neither of
	/// which is ever released, so building one per frame would leak steadily. The sky is a 1x1
	/// texture that follows whichever park is on show - see <see cref="Sky.Colour"/>.
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

		var ptr = Marshal.AllocHGlobal( data.Length );
		Marshal.Copy( data, 0, ptr, data.Length );
		Device.UpdateTexture( NativeTexture, ptr, (uint)data.Length, 0, 0, 0, Width, Height, 1, 0, 0 );
		Marshal.FreeHGlobal( ptr );
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
	/// The original's own test for a gradient - see <see cref="HasGradedAlpha"/>. Texels that are
	/// fully clear don't count, because a cut-out mask is mostly those; it is the partly-clear
	/// ones that tell a gradient from a mask.
	/// </summary>
	private static bool IsGraded( byte[] data )
	{
		var texels = data.Length / 4;
		var partial = 0;

		for ( int i = 3; i < data.Length; i += 4 )
		{
			if ( data[i] > 0 && data[i] < CutOutAlphaReference )
				++partial;
		}

		return partial * 20 >= texels;
	}

	private void CreateTexture( string debugName, byte[] data, uint width, uint height, TextureFlags flags )
	{
		if ( TryGetCachedTexture( debugName, out var cachedTexture ) )
		{
			NativeTexture = cachedTexture!.NativeTexture;
			NativeTextureView = cachedTexture!.NativeTextureView;

			// Carried across with the GPU handles: it is a property of the pixels those handles
			// hold, so a texture served from the cache has to answer the same as the one that
			// loaded it. Left out, every shared texture would read as a cut-out.
			HasGradedAlpha = cachedTexture!.HasGradedAlpha;

			return;
		}

		PreprocessTextureData( ref data, ref width, ref height, flags );

		HasGradedAlpha = IsGraded( data );

		if ( flags.HasFlag( TextureFlags.PointFilter ) )
			SamplerType = SamplerType.Point;

		if ( flags.HasFlag( TextureFlags.Wrap ) )
			SamplerType = SamplerType.AnisotropicWrap;

		if ( flags.HasFlag( TextureFlags.Repeat ) )
			SamplerType = SamplerType.AnisotropicRepeat;

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

		var textureDataPtr = Marshal.AllocHGlobal( data.Length );
		Marshal.Copy( data, 0, textureDataPtr, data.Length );
		Device.UpdateTexture( texture, textureDataPtr, (uint)data.Length, 0, 0, 0, width, height, 1, 0, 0 );
		Marshal.FreeHGlobal( textureDataPtr );

		var textureView = Device.ResourceFactory.CreateTextureView( texture );

		Path = debugName;
		NativeTexture = texture;
		NativeTextureView = textureView;
		Width = width;
		Height = height;

		Render.ImmediateSubmit( cmd =>
		{
			cmd.GenerateMipmaps( NativeTexture );
		} );

		Register();
	}
}
