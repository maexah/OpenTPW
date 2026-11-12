using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The harbour's moving sea, <see cref="ParkSea"/> (<c>docs/exe/park-engine.md</c>, "The park's sea"). The expected
/// numbers are worked from the decoded formula with every sine read from the engine's 4,096-entry table, the ten
/// degrees' too: entry 114 is 10.02°, so cos 10° is 0.98475 and sin 10° 0.17398, and exact maths answers s a few
/// hundredths away at these distances (25.704 for the first, 85.886 for the point). Each point asked has a column unlike its row and a phase that is not nought, so
/// a rule with the axes swapped, a sign turned or the phase's half dropped answers otherwise.
/// </summary>
[TestClass]
public class ParkSeaTests
{
	private const float Table = 0.005f;

	[TestMethod]
	public void TheWaveRunsTenDegreesOffTheGridAndHalfThePhaseBack()
	{
		// (2π/3)(20 × 0.98475 − 40 × 0.17398) − 0.5 × 2.
		Assert.AreEqual( 25.6734f, ParkSea.Wave( 20f, 40f, 2f ), 0.001f );
	}

	[TestMethod]
	public void APointHeavesByOnePointSevenTimesTheCosine()
	{
		Assert.AreEqual( 0.91851f, ParkSea.Height( 1f ), Table );
	}

	[TestMethod]
	public void APointsNormalLeansEquallyAlongBothGroundAxesByTheSine()
	{
		var normal = ParkSea.Normal( 0.5f );

		Assert.AreEqual( 0.39682f, normal.X, Table );
		Assert.AreEqual( 0.82769f, normal.Y, Table );
		Assert.AreEqual( 0.39682f, normal.Z, Table );
	}

	[TestMethod]
	public void APointStandsTwentyAColumnAcrossAndTwentyARowAlongFromTheCorner()
	{
		var corner = new Vector3( 380f, -180f, -5f );

		// Column 3, row 5, phase 3: s = (2π/3)(60 × 0.98475 − 100 × 0.17398) − 1.5 = 85.808, 1.7 cos s = −0.940.
		var point = ParkSea.Point( corner, 3, 5, 3f );

		Assert.AreEqual( 440f, point.X, 0.001f );
		Assert.AreEqual( -80f, point.Y, 0.001f );
		Assert.AreEqual( -5.9401f, point.Z, Table );
	}

	[TestMethod]
	public void TheCornerIsTheThemesWaterStartPosWithItsUpTurnedToThisEnginesZ()
	{
		Assert.AreEqual( new Vector3( 380f, -180f, -5f ), ParkSea.CornerOf( new Vector3( 380f, -5f, -180f ) ) );
	}

	[TestMethod]
	public void TwoBlocksLay134QuadsRowByRowEachCountingItsWordsFromNought()
	{
		var quads = ParkSea.Quads().ToArray();

		Assert.AreEqual( 134, quads.Length );

		// The first block, columns 0 to 9 by rows 0 to 10, along a row first.
		Assert.AreEqual( (0, 0, 0), quads[0] );
		Assert.AreEqual( (9, 0, 9), quads[9] );
		Assert.AreEqual( (0, 1, 10), quads[10] );
		Assert.AreEqual( (9, 10, 109), quads[109] );

		// The second, columns 10 to 13 by rows 0 to 5, takes the first 24 words again.
		Assert.AreEqual( (10, 0, 0), quads[110] );
		Assert.AreEqual( (10, 1, 4), quads[114] );
		Assert.AreEqual( (13, 5, 23), quads[133] );
	}

	[TestMethod]
	public void EachQuadFoldsAlongTheLineFromItsSecondCornerToItsThird()
	{
		CollectionAssert.AreEqual( new uint[] { 2, 1, 0, 1, 2, 3, 6, 5, 4, 5, 6, 7 }, ParkSea.Elements( 2 ) );
	}

	[TestMethod]
	public void EveryWordIsOneOfTheThreeTextures()
	{
		var words = ParkSea.DrawWords( new Random( 490 ) );

		Assert.AreEqual( 110, words.Length );
		CollectionAssert.AreEquivalent( new[] { 0, 1, 2 }, words.Distinct().ToArray() );
	}

	[TestMethod]
	public void ThePhaseGainsTwelveASecondAndNothingWhilePaused()
	{
		// 0.4 a frame at 30 frames a second; the build squares' 0.1 would give 2.5.
		Assert.AreEqual( 7f, ParkSea.Advance( 1f, 0.5f, paused: false, ParkSea.SeaPeriod ), 0.0001f );
		Assert.AreEqual( 1f, ParkSea.Advance( 1f, 0.5f, paused: true, ParkSea.SeaPeriod ), 0.0001f );
	}

	/// <summary>Every corner of the quilt, at its column and row.</summary>
	private static (int Column, int Row)[] Grid()
		=> ParkSea.Quads().SelectMany( q => new[] { (q.Column, q.Row), (q.Column + 1, q.Row), (q.Column, q.Row + 1), (q.Column + 1, q.Row + 1) } )
			.Distinct().ToArray();

	/// <summary>
	/// The sea's phase wraps at 4π (Q504): a frame from 0.2 short of it lands at 0.2, and every corner stands where the
	/// unwrapped phase would stand it. Wrapping it at the flags' 20 instead would not keep the wave: 0.2 and 20.2 differ.
	/// </summary>
	[TestMethod]
	public void TheSeasPhaseWrapsAtFourPiAndTheWaveIsTheSameEitherSide()
	{
		var before = ParkSea.SeaPeriod - 0.2f;
		var after = ParkSea.Advance( before, 1f / 30f, paused: false, ParkSea.SeaPeriod );
		Assert.AreEqual( 0.2f, after, 0.0001f, "wrapped" );

		foreach ( var (column, row) in Grid() )
		{
			var wrapped = ParkSea.Point( Vector3.Zero, column, row, after );
			var unwrapped = ParkSea.Point( Vector3.Zero, column, row, before + 0.4f );
			Assert.AreEqual( unwrapped.Z, wrapped.Z, Table, $"column {column} row {row}" );
		}

		Assert.IsTrue( Grid().Any( p => MathF.Abs( ParkSea.Point( Vector3.Zero, p.Column, p.Row, 0.2f ).Z
			- ParkSea.Point( Vector3.Zero, p.Column, p.Row, 20.2f ).Z ) > 0.5f ), "20 is not the sea's period" );
	}

	/// <summary>
	/// The flags' phase wraps at 20 (Q504): a frame from 19.8 lands at 0.2, and every corner of every flag stands where
	/// 20.2 would stand it. Wrapping it at the sea's 4π instead would not keep the wave: 0.2 and 0.2 + 4π differ.
	/// </summary>
	[TestMethod]
	public void TheFlagsPhaseWrapsAtTwentyAndTheWaveIsTheSameEitherSide()
	{
		var after = ParkFlags.PhaseAfter( null, 19.8f, 1f / 30f, paused: false );
		Assert.AreEqual( 0.2f, after, 0.0001f, "wrapped" );
		Assert.AreEqual( 0.2f, ParkSea.Advance( 19.8f, 1f / 30f, paused: false, ParkSea.FlagsPeriod ), 0.0001f, "the sea steps the flags' alike" );

		var wrapped = ParkFlags.Corners( after, 139f ).ToArray();
		var unwrapped = ParkFlags.Corners( 20.2f, 139f ).ToArray();
		for ( var at = 0; at < wrapped.Length; ++at )
			Assert.IsTrue( (wrapped[at].Position - unwrapped[at].Position).Length < 0.01f, $"corner {at}" );

		var sea = ParkFlags.Corners( 0.2f + ParkSea.SeaPeriod, 139f ).ToArray();
		Assert.IsTrue( wrapped.Zip( sea ).Any( pair => (pair.First.Position - pair.Second.Position).Length > 0.5f ), "4π is not the flags' period" );
	}

	/// <summary>
	/// A day of frames at 144 Hz still steps the phase 12/144 a frame: unwrapped, the float's spacing rounds the step to
	/// 0.0625 by then (Q504).
	/// </summary>
	[TestMethod]
	public void ADayAtOneHundredAndFortyFourHertzStillStepsTheWaveAtItsRate()
	{
		const float delta = 1f / 144f;
		var (sea, flags) = (0f, 0f);

		for ( var frame = 0; frame < 144 * 60 * 60 * 24; ++frame )
		{
			sea = ParkSea.Advance( sea, delta, paused: false, ParkSea.SeaPeriod );
			flags = ParkSea.Advance( flags, delta, paused: false, ParkSea.FlagsPeriod );
		}

		var step = 12f / 144f;
		Assert.AreEqual( step, ParkSea.Wrap( ParkSea.Advance( sea, delta, false, ParkSea.SeaPeriod ) - sea, ParkSea.SeaPeriod ), 1e-4f, "the sea" );
		Assert.AreEqual( step, ParkSea.Wrap( ParkSea.Advance( flags, delta, false, ParkSea.FlagsPeriod ) - flags, ParkSea.FlagsPeriod ), 1e-4f, "the flags" );
	}

	[TestMethod]
	public void OnlyTextureQualityNoughtAndOneTakeTheSmallCopies()
	{
		Assert.IsTrue( ParkSea.SmallTextures( 0 ) );
		Assert.IsTrue( ParkSea.SmallTextures( 1 ) );
		Assert.IsFalse( ParkSea.SmallTextures( 2 ) );
		Assert.IsFalse( ParkSea.SmallTextures( 3 ) );
	}

	[TestMethod]
	public void EveryParkLaysItsSeaAtTheCornerItsThemeFileGives()
	{
		FileSystem = GameData.Required();

		// The global Standard.sam says (375, −5, 0); each theme's own file says otherwise.
		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
			Assert.AreEqual( new Vector3( 380f, -5f, -180f ), new ParkBalance( theme ).Vector( "WaterStartPos", new Vector3( -1f ) ), theme );
	}

	[TestMethod]
	public void LowDetailTakesTheSmallTexturesAndMediumTheFullOnes()
	{
		FileSystem = GameData.Required();

		Assert.IsTrue( ParkSea.SmallTextures( int.Parse( new SettingsFile( "/low.sam" )["GraphicalOptions.TEXTUREQUALITY"]! ) ) );
		Assert.IsFalse( ParkSea.SmallTextures( int.Parse( new SettingsFile( "/med.sam" )["GraphicalOptions.TEXTUREQUALITY"]! ) ) );

		foreach ( var folder in new[] { "textures", "stexture" } )
			foreach ( var name in ParkSea.TextureNames )
				Assert.IsTrue( FileSystem.FileExists( $"generic/dynamic/{folder}/{name}.wct" ), $"{folder}/{name}" );
	}

	private static readonly Vector3 Corner = new( 380f, -180f, -5f );

	/// <summary>A sea without its model: every word 1 but the first block's 42nd, which is 2 and the first, which is 0.</summary>
	private static ParkSea Sea()
	{
		var words = Enumerable.Repeat( 1, ParkSea.Words ).ToArray();
		words[0] = 0;
		words[42] = 2;
		return new ParkSea( Corner, "textures", words );
	}

	[TestMethod]
	public void EachQuadIsLaidFourCornersWithTheirUvTextureAndFlags()
	{
		var sea = Sea();

		try
		{
			GameClock.Update( paused: true, GameClock.ParkCatchUp );
			sea.SetPhase( 3f );
			sea.Update();

			// Quad 42 is column 2, row 4 of the first block (word 4 × 10 + 2): [c][r], [c+1][r], [c][r+1], [c+1][r+1].
			// Phase 3, held by the paused clock, so the points are worked from the table at that phase.
			var quad = sea.Vertices.Skip( 42 * 4 ).Take( 4 ).ToArray();
			var expected = new[]
			{
				(new Vector3( 420f, -100f, -5.0183f ), new Vector2( 0f, 0f )),
				(new Vector3( 440f, -100f, -4.3087f ), new Vector2( 1f, 0f )),
				(new Vector3( 420f, -80f, -3.5750f ), new Vector2( 0f, 1f )),
				(new Vector3( 440f, -80f, -5.9401f ), new Vector2( 1f, 1f ))
			};

			for ( var corner = 0; corner < 4; ++corner )
			{
				var (position, uv) = expected[corner];
				Assert.AreEqual( position.X, quad[corner].Position.X, 0.001f, $"corner {corner} x" );
				Assert.AreEqual( position.Y, quad[corner].Position.Y, 0.001f, $"corner {corner} y" );
				Assert.AreEqual( position.Z, quad[corner].Position.Z, Table, $"corner {corner} z" );
				Assert.AreEqual( uv, quad[corner].TexCoords, $"corner {corner} uv" );
				Assert.AreEqual( 2, quad[corner].TexIndex, $"corner {corner} wears the quad's word" );
				Assert.AreEqual( 0x2u | LobbyModel.ClampFlag, quad[corner].MatFlags, $"corner {corner} flags" );
			}

			// The corner [c+1][r+1] at phase 9: s = 85.808 less 3, the sine 0.9027 along both ground axes.
			sea.SetPhase( 9f );
			sea.Update();

			var normal = sea.Vertices[(42 * 4) + 3].Normal;
			Assert.AreEqual( 0.5569f, normal.X, Table );
			Assert.AreEqual( 0.6169f, normal.Y, Table );
			Assert.AreEqual( 0.5569f, normal.Z, Table );
			Assert.AreEqual( -4.2684f, sea.Vertices[(42 * 4) + 3].Position.Z, Table, "laid at the phase now" );

			// The second block counts its words from nought again: its first quad, column 10, wears word 0.
			Assert.AreEqual( 580f, sea.Vertices[110 * 4].Position.X, 0.001f );
			Assert.AreEqual( 0, sea.Vertices[110 * 4].TexIndex );
		}
		finally
		{
			GameClock.Rebase();
			sea.Delete();
			Entity.ApplyDeletions();
		}
	}

	[TestMethod]
	public void AFrameAdvancesThePhaseByTheFramesTimeAndAPausedClockHoldsIt()
	{
		var sea = Sea();
		var delta = Time.Delta;

		try
		{
			Time.Delta = 0.5f;

			GameClock.Update( paused: false, GameClock.ParkCatchUp );
			sea.SetPhase( 3f );
			sea.Update();
			Assert.AreEqual( 9f, sea.Phase, 0.0001f, "12 a second for half a second" );
			Assert.AreEqual( -4.2684f, sea.Vertices[(42 * 4) + 3].Position.Z, Table, "and laid at the new phase" );

			GameClock.Update( paused: true, GameClock.ParkCatchUp );
			sea.Update();
			Assert.AreEqual( 9f, sea.Phase, 0.0001f, "held while the clock is" );

			// Q504: each phase wraps at its own period, from one frame's step.
			Time.Delta = 1f / 30f;
			GameClock.Update( paused: false, GameClock.ParkCatchUp );
			sea.SetPhase( ParkSea.SeaPeriod - 0.2f );
			sea.Update();
			Assert.AreEqual( 0.2f, sea.Phase, 0.0001f, "the sea's wrapped at 4π" );
			Assert.AreEqual( ParkSea.SeaPeriod + 0.2f, sea.FlagsPhase, 0.0001f, "the flags' not yet" );

			sea.SetPhase( 19.8f );
			Assert.AreEqual( 19.8f - ParkSea.SeaPeriod, sea.Phase, 0.0001f, "a phase set is wrapped for the sea" );
			sea.Update();
			Assert.AreEqual( 19.8f - ParkSea.SeaPeriod + 0.4f, sea.Phase, 0.0001f );
			Assert.AreEqual( 0.2f, sea.FlagsPhase, 0.0001f, "the flags' wrapped at 20" );

			// The original's phase as read live (park-engine.md, "The gates' flags"): 431.996 is 4.739 of the sea's and 11.996 of the flags'.
			sea.SetPhase( 431.996f );
			Assert.AreEqual( 4.7394f, sea.Phase, 0.001f );
			Assert.AreEqual( 11.996f, sea.FlagsPhase, 0.001f );
		}
		finally
		{
			Time.Delta = delta;
			GameClock.Rebase();
			sea.Delete();
			Entity.ApplyDeletions();
		}
	}

	[TestMethod]
	public void TheTextureQualityIsTheDetailFilesKeyAtEachGraphicsQuality()
	{
		FileSystem = GameData.Required();
		var quality = GameOptions.Current.GraphicsQuality;

		try
		{
			// low.sam, med.sam and high.sam say 1, 2 and 3; the options start at 1, medium.
			foreach ( var (graphics, expected) in new[] { (0, 1), (1, 2), (2, 3) })
			{
				GameOptions.Current.GraphicsQuality = graphics;
				Assert.AreEqual( expected, Level.TextureQuality(), $"graphics quality {graphics}" );
			}
		}
		finally
		{
			GameOptions.Current.GraphicsQuality = quality;
		}
	}
}
