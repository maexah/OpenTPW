using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The grey a bolt is drawn in and the sixteen slots it lives in (<c>docs/exe/weather.md</c>, "The bolt";
/// FUN_0057ee30, FUN_00580320). The colour words and lives below are ones the original's own bolt records held, read
/// at 2 ms from [0x008bcbac] while a bolt fell (<c>q1044/orig/run1/a-load.log</c>); the base table is the listing's
/// arithmetic worked by hand at the points the doc names.
/// </summary>
[TestClass]
public class BoltEnvelopeTests
{
	/// <summary>
	/// <b>The base grey is a saw-tooth of 192..255 every 500 ms of the life left:</b> up over the last 50 ms of a tooth
	/// and down over the other 450, not a fade over the whole life.
	/// </summary>
	/// <remarks><b>Mutations:</b> the base fading with the whole 2000 ms; a period of 1000; the rise and fall swapped; the 0.9f turn at 0.5f.</remarks>
	[TestMethod]
	public void TheBaseGreyIsACyclingSawTooth()
	{
		var table = new (int Ms, int Base)[]
		{
			(0, 192), (10, 193), (100, 206), (200, 220), (300, 234), (400, 248),
			(443, 255), (449, 255), (450, 255), (451, 254), (460, 243), (480, 217), (490, 204), (499, 193),
		};

		foreach ( var (ms, expected) in table )
		{
			Assert.AreEqual( expected, BoltEnvelope.Base( ms ), $"{ms} ms into a tooth" );

			// Every 500 ms is the same tooth: a park's 2000 ms is four of them.
			Assert.AreEqual( expected, BoltEnvelope.Base( ms + 500 ), $"{ms} + 500" );
			Assert.AreEqual( expected, BoltEnvelope.Base( ms + 1500 ), $"{ms} + 1500" );
		}
	}

	/// <summary>
	/// <b>A grey past 255 is packed unclamped and wraps to near black:</b> 256..319 gives R = G = (g - 256) | 1 and
	/// B = g - 256; 0..255 is neutral.
	/// </summary>
	/// <remarks><b>Mutations:</b> the sum clamped to 255; B taking the | 1; the wrap's red and green not taking it; a 6-bit wrap.</remarks>
	[TestMethod]
	public void AGreyPastTwoFiftyFiveWrapsToNearBlack()
	{
		Assert.AreEqual( 0xFFFFFFFFu, BoltEnvelope.Pack( 255 ) );
		Assert.AreEqual( 0xFFC0C0C0u, BoltEnvelope.Pack( 192 ) );
		Assert.AreEqual( 0xFF010100u, BoltEnvelope.Pack( 256 ) );
		Assert.AreEqual( 0xFF0F0F0Fu, BoltEnvelope.Pack( 271 ) );
		Assert.AreEqual( 0xFF0F0F0Eu, BoltEnvelope.Pack( 270 ) );
		Assert.AreEqual( 0xFF3F3F3Fu, BoltEnvelope.Pack( 319 ) );
	}

	/// <summary>
	/// <b>The words the original's bolt packed, with the life it held:</b> a life left of 1967 (467 into a tooth, base 234)
	/// drawn with a rand of 45 is a grey of 279, which packs to <c>0xff171717</c>, and the others the same way.
	/// </summary>
	/// <remarks><b>Mutations:</b> the base read from the whole life; the rand's mask 31 or 127; the life left taken before the frame's ms came off.</remarks>
	[TestMethod]
	public void TheOriginalsOwnWordsComeBack()
	{
		// (life left, rand & 63 that gives the word read at that life, the word the original held)
		var words = new (int Life, int Rand, uint Word)[]
		{
			( 1967, 45, 0xFF171717u ),
			( 1962, 9, 0xFFF9F9F9u ),
			( 1929, 13, 0xFF0B0B0Au ),
			( 1062, 11, 0xFFD3D3D3u ),
			( 1000, 59, 0xFFFBFBFBu ),
		};

		foreach ( var (life, rand, word) in words )
		{
			var (r, g, b) = BoltEnvelope.Colour( life, rand );

			Assert.AreEqual( word, 0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | b, $"life {life}, rand {rand}" );
		}

		// Only the six low bits of the CRT's 15-bit draw are used.
		Assert.AreEqual( BoltEnvelope.Colour( 1967, 45 ), BoltEnvelope.Colour( 1967, 45 + 64 * 7 ) );
	}

	/// <summary>
	/// <b>About half the frames draw the bolt near black:</b> over every phase of a tooth and every draw, 0.4927 of the
	/// pairs carry past 255 (the original's run: 583 of 1,158 colour words read).
	/// </summary>
	/// <remarks><b>Mutations:</b> a base clamped at 192..223; the sum clamped; a jitter of 0..31 or 0..127.</remarks>
	[TestMethod]
	public void AboutHalfTheFramesWrap()
	{
		int wrapped = 0, all = 0;

		for ( var ms = 0; ms < BoltEnvelope.Period; ++ms )
		{
			for ( var rand = 0; rand < 64; ++rand )
			{
				var (r, _, b) = BoltEnvelope.Colour( ms, rand );
				++all;

				if ( r < 64 )
					++wrapped;
			}
		}

		Assert.AreEqual( 0.4927, wrapped / (double)all, 0.0005 );
	}
}
