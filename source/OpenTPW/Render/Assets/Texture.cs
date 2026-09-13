using StbImageSharp;
using System.Runtime.InteropServices;
using Veldrid;

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
	/// A one-pixel white stand-in, for a material slot naming no texture of its own.
	///
	/// <para>
	/// <b>Every read of this builds another one.</b> It is a property rather than a kept instance, so each
	/// use creates a GPU texture, copies a pixel into it, submits it to the device and registers itself as
	/// one more step of the loading bar. It can never be served from the cache either: the constructor it
	/// calls passes an empty path, and <c>TryGetCachedTexture</c> refuses an empty path before it looks
	/// anything up - so no two of these are ever the same object.
	/// </para>
	///
	/// <para>
	/// That costs nothing for the callers wanting a single texture - <c>Sky</c> and <c>WeatherSprites</c>
	/// return it as a fallback, and <c>UiMesh</c> keeps its own with <c>_blank ??= Texture.Missing</c>,
	/// which is the shape worth copying. It is <c>LobbyModel</c> that pays: it reads this inside a loop over
	/// sixteen material slots for every mesh of every model, so a mesh naming two materials mints fourteen
	/// blank textures, each with its own device submit and its own loading step.
	/// </para>
	///
	/// <para>
	/// <b>How much that actually costs has not been measured.</b> There is a reason to suspect it is a large
	/// share of the lobby's load - a texture found in the cache returns before it registers a step at all,
	/// while every one of these registers - but nobody has counted them. <b>Count them before treating this
	/// as a performance problem worth fixing</b>, because the answer may turn out to be small. Sharing one
	/// blank would also make it an object many materials hold a handle to, so anything that later deletes a
	/// texture would need to know not to delete this one.
	/// </para>
	/// </summary>
	public static Texture Missing => new Texture( [255, 255, 255, 255], 1, 1 );

	/// <summary>The game's own stand-in for a texture it can't load - see <see cref="NotFound"/>.</summary>
	private const string NotFoundPath = "generic/defaulttexture/NotFound.tga";

	/// <summary>Decoded once; <see cref="NotFound"/> hands out copies.</summary>
	private static TextureData? _notFound;

	internal Veldrid.Texture NativeTexture;
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
	/// so deleting either would pull it out from under the other. A texture built from pixels is
	/// never shared.
	/// </summary>
	public void Delete()
	{
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
