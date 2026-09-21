using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Which sampler a texture's flags ask for - <see cref="Texture.SamplerFor"/>.
///
/// <para>
/// <b>This is the half of the texture cache's behaviour a test can reach, and it is not the proof.</b>
/// What the cache does with a hit - carrying the sampler, the size and the path across to the texture
/// that adopts it - cannot be tested here at all: every road to a cached texture runs through
/// <c>CreateTexture</c>, which builds a GPU texture, and a test run has no graphics device. That
/// wiring rests on the capture instead, where the lobby's sea was photographed on first load and
/// again on the way back from a park. This is written down rather than papered over, the way the
/// audio wiring's own gap is.
/// </para>
///
/// <para>
/// The rule was pulled out of <c>CreateTexture</c> into a static precisely so that this much could be
/// pinned without a device - the parameter was shrunk until the test became possible.
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
	/// <b>The assertion that carries the defect.</b> A texture served out of the cache used to keep
	/// the default sampler rather than the one its flags asked for, and the reason that showed on
	/// screen at all is that these two are different things: the default mirrors, so every other tile
	/// of the sea was flipped and its wave ripples came out as a diamond lattice. If these two are
	/// ever made equal, the fault becomes invisible rather than fixed - so this fails first.
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
}
