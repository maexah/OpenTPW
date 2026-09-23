using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Which sampler a texture's flags ask for - <see cref="Texture.SamplerFor"/> - and what a texture served
/// out of the cache carries across from the one that loaded it - <c>Texture.TryAdoptCached</c>.
///
/// <para>
/// <b>The cache is reached without a graphics device</b>, which a test run has none of. Loading a texture
/// builds a GPU texture, so the one already loaded is a stand-in: a <see cref="Texture"/> made without
/// running a constructor, given the flags, sampler, size, path and alpha a load would have given it, and
/// put in <see cref="Asset.All"/> where the cache looks. The texture under test is then built by the real
/// path constructor, which asks the cache before it touches a file. Its GPU handles are not pinned: the
/// stand-in's are null, and so are those of a texture that failed to copy them.
/// </para>
/// </summary>
[TestClass]
public class TextureSamplerTests
{
	/// <summary>
	/// The lobby's sea asks for <see cref="TextureFlags.Wrap"/>, and that has to mean the wrapping
	/// sampler. It is the one request in the game that wants anything other than the default.
	/// </summary>
	[TestMethod]
	public void WrapAsksForTheWrappingSampler()
		=> Assert.AreEqual( SamplerType.AnisotropicWrap, Texture.SamplerFor( TextureFlags.Wrap ) );

	[TestMethod]
	public void RepeatAsksForTheRepeatingSampler()
		=> Assert.AreEqual( SamplerType.AnisotropicRepeat, Texture.SamplerFor( TextureFlags.Repeat ) );

	[TestMethod]
	public void PointFilterAsksForThePointSampler()
		=> Assert.AreEqual( SamplerType.Point, Texture.SamplerFor( TextureFlags.PointFilter ) );

	/// <summary>
	/// Asking for nothing gets the same sampler the field is declared with, which is what every
	/// texture that names no flags is drawn with today.
	/// </summary>
	[TestMethod]
	public void AskingForNothingGetsTheDefault()
		=> Assert.AreEqual( SamplerType.AnisotropicRepeat, Texture.SamplerFor( TextureFlags.None ) );

	/// <summary>
	/// A wrap request and a request for nothing are drawn differently. The default mirrors, and the lobby's
	/// sea drawn with it has every other tile flipped and its wave ripples in a diamond lattice - which is
	/// what makes a cached texture that kept the default a fault that shows, rather than one that cannot.
	/// </summary>
	[TestMethod]
	public void TheWrappingSamplerIsNotTheDefaultOne()
		=> Assert.AreNotEqual( Texture.SamplerFor( TextureFlags.None ), Texture.SamplerFor( TextureFlags.Wrap ),
			"a wrap request has to differ from asking for nothing, or nothing can go wrong visibly" );

	/// <summary>
	/// The precedence the three assignments this replaced had, where each overwrote the last: Repeat
	/// beats Wrap beats PointFilter. Nothing in the game asks for two at once; this pins the order so
	/// that rewriting the rule cannot quietly change it.
	/// </summary>
	[TestMethod]
	public void RepeatBeatsWrapWhichBeatsPointFilter()
	{
		Assert.AreEqual( SamplerType.AnisotropicRepeat,
			Texture.SamplerFor( TextureFlags.Repeat | TextureFlags.Wrap ) );

		Assert.AreEqual( SamplerType.AnisotropicWrap,
			Texture.SamplerFor( TextureFlags.Wrap | TextureFlags.PointFilter ) );
	}

	/// <summary>
	/// The chroma key is about PIXELS, not about sampling, so it must leave the sampler alone - which
	/// is also why a cached texture cannot be handed to a request whose flags differ from the ones it
	/// was loaded under: those pixels have already been rewritten, or already have not been.
	/// </summary>
	[TestMethod]
	public void TheChromaKeyDoesNotChooseASampler()
		=> Assert.AreEqual( Texture.SamplerFor( TextureFlags.None ),
			Texture.SamplerFor( TextureFlags.PinkChromaKey ) );

	/// <summary>
	/// <b>A texture served out of the cache is drawn the way the one that loaded it was</b>: with the
	/// sampler its flags asked for, at its size, under its path, and graded or cut out as its pixels are.
	/// The first row is the lobby's sea on the way back from a park - <c>Water.Spawn</c> asks for
	/// <see cref="TextureFlags.Wrap"/>, and the texture it gets is the one the first visit loaded.
	/// </summary>
	/// <remarks>
	/// The two rows carry opposite values, and between them every value differs from what a texture that
	/// copied nothing would read - the default sampler, 0 by 0, no path, a cut-out - so each copy is pinned on
	/// its own, and so is copying rather than assuming. A texture taken over is not registered, so
	/// <see cref="Asset.All"/> is the same length afterwards.
	/// <b>Mutations:</b> taking any one of the five copies out of <c>TryAdoptCached</c>, or replacing one with
	/// a constant, fails the assertion that names it.
	/// </remarks>
	[DataTestMethod]
	[DataRow( TextureFlags.Wrap, SamplerType.AnisotropicWrap, 64, 32, true )]
	[DataRow( TextureFlags.PointFilter, SamplerType.Point, 16, 8, false )]
	public void ACachedTextureIsDrawnTheWayTheLoadedOneWas( TextureFlags flags, SamplerType sampler, int width,
		int height, bool graded )
	{
		// Nowhere on disk, so only the cache can answer it.
		var path = $"opentpw-tests/lobby/{flags}.wct";

		var loaded = Loaded( path, flags, sampler, (uint)width, (uint)height, graded );

		Asset.All.Add( loaded );

		var registered = Asset.All.Count;

		try
		{
			var served = new Texture( path, flags );

			Assert.IsTrue( served.Adopted, "the same path under the same flags is served from the cache" );
			Assert.AreEqual( flags, served.Requested, "it says what it was asked for" );
			Assert.AreEqual( sampler, served.SamplerType, "the sampler its flags asked for" );
			Assert.AreEqual( (uint)width, served.Width, "its width" );
			Assert.AreEqual( (uint)height, served.Height, "its height" );
			Assert.AreEqual( path, served.Path, "the path it came from" );
			Assert.AreEqual( graded, served.HasGradedAlpha, "graded or cut out, as the pixels it shares are" );
			Assert.AreEqual( registered, Asset.All.Count, "and it is not registered a second time" );
		}
		finally
		{
			Asset.All.Remove( loaded );
		}
	}

	/// <summary>
	/// A texture as a load would have left it, made without a device: no constructor runs, so nothing
	/// reaches the GPU, and each property a load assigns is set the way the load would set it.
	/// </summary>
	private static Texture Loaded( string path, TextureFlags flags, SamplerType sampler, uint width, uint height,
		bool graded )
	{
		var texture = (Texture)RuntimeHelpers.GetUninitializedObject( typeof( Texture ) );

		Set( texture, nameof( Texture.Requested ), flags );
		Set( texture, nameof( Texture.SamplerType ), sampler );
		Set( texture, nameof( Texture.Width ), width );
		Set( texture, nameof( Texture.Height ), height );
		Set( texture, nameof( Texture.HasGradedAlpha ), graded );

		texture.Path = path;

		return texture;
	}

	/// <summary>Sets a property through its private setter.</summary>
	private static void Set( Texture texture, string property, object value )
		=> typeof( Texture ).GetProperty( property )!.SetValue( texture, value );
}
