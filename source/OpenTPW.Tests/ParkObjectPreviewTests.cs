using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>
/// The object window's preview: which model it shows, and where the original's fit puts it in the panel
/// (<c>FUN_004689f0</c>, <c>FUN_00468e50</c>; docs/exe/park-engine.md, "The object window's preview"). The expected
/// fits are the original's own, read from its record at <c>0x007afd08</c> with each window open.
/// </summary>
[TestClass]
public class ParkObjectPreviewTests
{
	/// <summary>The ride window's panel, 414 by 414 of the layout's 2048 by 1536, in the original's screen units.</summary>
	private const float WindowWidth = 414f / 1024f, WindowHeight = 414f / 768f;

	private static readonly string[] Themes = ["jungle", "fantasy", "hallow", "space"];

	private static ItemHeightMapFile Heights( BaseFileSystem data, string directory, string stem )
		=> ItemHeightMapFile.Read( data.ReadAllBytes( $"{directory}/{stem}.hmp" ) )!;

	private static ParkPreviewFit FitOf( BaseFileSystem data, string directory, string stem, float width, float height )
	{
		var heights = Heights( data, directory, stem );

		return ParkPreviewFit.For( heights.BoxMin.GetSystemVector3(), heights.BoxMax.GetSystemVector3(), width, height );
	}

	private static void AssertFit( ParkPreviewFit fit, float halfX, float halfZ, float share, float diagonal, float reach )
	{
		Assert.AreEqual( halfX, fit.HalfX, 2e-7f, "half the box's width" );
		Assert.AreEqual( halfZ, fit.HalfZ, 2e-7f, "half the box's depth" );
		Assert.AreEqual( share, fit.Share, 2e-7f, "the reach's share" );
		Assert.AreEqual( diagonal, fit.Diagonal, 2e-7f, "the diagonal" );
		Assert.AreEqual( reach, fit.Reach, 2e-7f, "the reach" );
	}

	[TestMethod]
	public void TheBellyBouncesFitInItsWindowIsTheOriginals()
	{
		var data = FileSystem = GameData.Required();

		AssertFit( FitOf( data, "levels/jungle/rides/bouncy", "bouncy", WindowWidth, WindowHeight ),
			0.1510225087404251f, 0.20136332511901855f, 0.5574497580528259f, 0.5390625f, 0.31705427169799805f );
	}

	[TestMethod]
	public void TheAztecMayhemsFitInItsWindowIsTheOriginals()
	{
		var data = FileSystem = GameData.Required();

		AssertFit( FitOf( data, "levels/jungle/rides/tvsim", "tvsim", WindowWidth, WindowHeight ),
			0.1666562855243683f, 0.1666562855243683f, 0.5989949107170105f, 0.5390625f, 0.35205456614494324f );
	}

	/// <summary>The buy screen's panel is 360 by 363: the same box in it, as the original keeps it there.</summary>
	[TestMethod]
	public void TheAztecMayhemsFitInTheBuyScreenIsTheOriginals()
	{
		var data = FileSystem = GameData.Required();

		AssertFit( FitOf( data, "levels/jungle/rides/tvsim", "tvsim", 0.3515625f, 0.47265625f ),
			0.1461261808872223f, 0.1461261808872223f, 0.5989949107170105f, 0.47265625f, 0.3086855411529541f );
	}

	/// <summary>
	/// Every shipped fit so far is bound by the panel's height. A low wide box in a narrow panel is bound by the
	/// width: half the footprint's diagonal in two thirds of it.
	/// </summary>
	[TestMethod]
	public void ALowWideBoxInANarrowPanelIsFittedByTheWidth()
	{
		var fit = ParkPreviewFit.For( Vector3.Zero, new Vector3( 40f, 1f, 40f ), 0.2f, 0.5f );

		// 0.2 * (2/3) / sqrt( 20^2 + 20^2 ) against 0.5 / sqrt( 40^2 + 1 + 40^2 ) = 0.0088374.
		Assert.AreEqual( 0.00471405f, fit.Scale, 1e-7f );
		Assert.AreEqual( 0.0942809f, fit.HalfX, 1e-6f );
		Assert.AreEqual( 0.2667088f, fit.Diagonal, 1e-6f );
		Assert.AreEqual( 0.1334164f, fit.Reach, 1e-6f );
		Assert.AreEqual( 0.5001562f, fit.Share, 1e-6f );
	}

	/// <summary>
	/// Both distances start at height nought, not at the box's own floor: the Inca Totem's pit, 46.65 under the
	/// ground, changes neither.
	/// </summary>
	[TestMethod]
	public void TheDistancesAreTakenFromTheGroundNotFromTheBoxsFloor()
	{
		var shallow = ParkPreviewFit.For( new Vector3( 0f, 0f, 0f ), new Vector3( 30f, 50f, 40f ), WindowWidth, WindowHeight );
		var deep = ParkPreviewFit.For( new Vector3( 0f, -46f, 0f ), new Vector3( 30f, 50f, 40f ), WindowWidth, WindowHeight );

		Assert.AreEqual( shallow, deep );
	}

	/// <summary>
	/// The model turns about the middle of its footprint, which stands on the panel's middle line, 0.35 of the
	/// reach's share of the reach under its middle - but for the 0.7 the original writes for the 45 degrees' sine.
	/// </summary>
	[TestMethod]
	public void TheFootprintsMiddleStaysPutAsTheModelTurns()
	{
		var fit = ParkPreviewFit.For( Vector3.Zero, new Vector3( 30f, 19.15f, 40f ), WindowWidth, WindowHeight );
		var middle = new Vector3( 15f, 0f, 20f );

		for ( var angle = 0f; angle < 7f; angle += 0.37f )
		{
			var at = fit.Place( middle, angle );
			var (sin, cos) = MathF.SinCos( angle );

			Assert.AreEqual( 0f, at.X, 1e-6f, $"across at {angle}" );

			var left = (MathF.Sin( MathF.PI / 4f ) - 0.7f) * ((fit.HalfZ * cos) - (fit.HalfX * sin));

			Assert.AreEqual( (-0.35f * fit.Share * fit.Reach) + left, at.Y, 1e-6f, $"up at {angle}" );
		}
	}

	/// <summary>
	/// One point by hand at a quarter turn (sine 1, cosine 0): across is three quarters of the point's z less half
	/// the depth, and up is its height and its x the other way, each through the 45 degrees.
	/// </summary>
	[TestMethod]
	public void APointAtAQuarterTurnLandsWhereTheOriginalsArithmeticPutsIt()
	{
		var fit = new ParkPreviewFit( 0.01f, 0.15f, 0.2f, 0.5f, 0.5f, 0.3f );
		var at = fit.Place( new Vector3( 10f, 6f, 30f ), MathF.PI / 2f );

		var tip = MathF.Sin( MathF.PI / 4f );

		Assert.AreEqual( 0.75f * ((0.01f * 30f) - 0.2f), at.X, 1e-6f );
		Assert.AreEqual( (0.01f * tip * (6f - 10f)) - (0.35f * 0.5f * 0.3f) + (0.7f * 0.15f), at.Y, 1e-6f );
		Assert.AreEqual( 0.1f * 0.01f * tip * (-10f - 6f), at.Z, 1e-7f );
	}

	/// <summary>
	/// The turn's sense, as photographed in the original: as the angle grows, the model's +x side swings towards
	/// the viewer, which is down the panel, and a higher point is always further up it.
	/// </summary>
	[TestMethod]
	public void TheModelsXSideComesTowardsTheViewerAsTheAngleGrows()
	{
		var fit = ParkPreviewFit.For( Vector3.Zero, new Vector3( 40f, 30f, 40f ), WindowWidth, WindowHeight );
		var corner = new Vector3( 40f, 0f, 20f );

		Assert.IsTrue( fit.Place( corner, 0.5f ).Y < fit.Place( corner, 0f ).Y );
		Assert.IsTrue( fit.Place( corner, 0.5f ).Z < fit.Place( corner, 0f ).Z, "nearer is the smaller depth" );
		Assert.IsTrue( fit.Place( new Vector3( 20f, 30f, 20f ), 1.1f ).Y > fit.Place( new Vector3( 20f, 0f, 20f ), 1.1f ).Y );
		Assert.IsTrue( fit.Place( new Vector3( 40f, 0f, 20f ), 0f ).X > fit.Place( new Vector3( 0f, 0f, 20f ), 0f ).X );
	}

	/// <summary>
	/// The matrix is <see cref="ParkPreviewFit.Place"/> for OpenTPW's axes, which keep the item's z along y and its
	/// height along z, moved to the panel's middle and stretched by the screen's units.
	/// </summary>
	[TestMethod]
	public void TheMatrixPlacesAWorldPointWhereTheFitPlacesTheItemsOwn()
	{
		var fit = ParkPreviewFit.For( Vector3.Zero, new Vector3( 30f, 19.15f, 40f ), WindowWidth, WindowHeight );
		var matrix = fit.Matrix( 2.3f, -0.4f, 0.3f, 0.9f, 1.1f );

		var item = new Vector3( 7f, 11f, 23f );
		var clip = Vector4.Transform( new Vector4( item.X, item.Z, item.Y, 1f ), matrix );
		var at = fit.Place( item, 2.3f );

		Assert.AreEqual( -0.4f + (0.9f * at.X), clip.X, 1e-6f );
		Assert.AreEqual( 0.3f + (1.1f * at.Y), clip.Y, 1e-6f );
		Assert.AreEqual( 0.5f + at.Z, clip.Z, 1e-6f );
		Assert.AreEqual( 1f, clip.W, 1e-6f );
	}

	/// <summary>The original takes 0.0004 of a radian off its angle for each millisecond: a turn in 15.7 seconds.</summary>
	[TestMethod]
	public void TheTurnIsFourTenThousandthsOfARadianAMillisecond()
		=> Assert.AreEqual( 0.0004f, ParkPreviewFit.RadiansPerMillisecond );

	private static ParkItemCatalogue.Item Item( string directory, string stem )
		=> new( 1, stem, directory, stem, 1, 1, null );

	[TestMethod]
	public void AnItemWithAPModelPreviewsItAndOneWithoutPreviewsItsOwn()
	{
		var item = Item( "levels/jungle/rides/totem", "totem" );

		Assert.AreEqual( "levels/jungle/rides/totem/Ptotem.MD2",
			ParkObjectPreview.ModelPathFor( item, path => path == "levels/jungle/rides/totem/Ptotem.MD2", out var isP ) );
		Assert.IsTrue( isP );

		Assert.AreEqual( "levels/jungle/rides/totem/totem.MD2", ParkObjectPreview.ModelPathFor( item, _ => false, out isP ) );
		Assert.IsFalse( isP );
	}

	/// <summary>
	/// Which shipped items have a <c>P</c> model: 39 of them, each with a clip of its own in role M, and none a
	/// fixed item. Lost Kingdom's eleven are named.
	/// </summary>
	[TestMethod]
	public void ThirtyNineShippedItemsHaveAPModelEachWithARunningClip()
	{
		var data = FileSystem = GameData.Required();
		var found = Themes.ToDictionary( theme => theme, _ => new System.Collections.Generic.List<string>() );

		foreach ( var theme in Themes )
		{
			foreach ( var wad in Directory.GetFiles( Path.Combine( GameDir.Data, "levels", theme ), "*.wad", SearchOption.AllDirectories ) )
			{
				var directory = Path.GetRelativePath( GameDir.Data, Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
				var stem = Path.GetFileName( directory );
				var members = data.GetFiles( directory ).Select( Path.GetFileName ).ToArray();

				if ( !members.Any( name => string.Equals( name, $"{stem}.sam", StringComparison.OrdinalIgnoreCase ) )
					|| !members.Any( name => string.Equals( name, $"P{stem}.MD2", StringComparison.OrdinalIgnoreCase ) ) )
					continue;

				found[theme].Add( stem.ToLowerInvariant() );

				Assert.IsTrue( RideAnimations.Load( directory, $"P{stem}", data ).EntryCount( 5 ) > 0, $"{directory}: no M clip for its P model" );
				Assert.IsNotNull( Heights( data, directory, stem ), $"{directory}: no .hmp to fit it by" );
			}
		}

		CollectionAssert.AreEquivalent(
			new[] { "coaster1", "coaster3", "gokarts", "lookout", "minecart", "mumbo", "spider", "totem", "tvsim", "wateride", "junspray" },
			found["jungle"] );

		Assert.AreEqual( 6, found["fantasy"].Count );
		Assert.AreEqual( 10, found["hallow"].Count );
		Assert.AreEqual( 12, found["space"].Count );
	}

	/// <summary>The preview plays the model's first M clip round and round at its own speed, on the first channel.</summary>
	[TestMethod]
	public void ThePreviewLoopsTheFirstRunningClip()
	{
		var data = FileSystem = GameData.Required();
		var animations = RideAnimations.Load( "levels/jungle/rides/tvsim", "Ptvsim", data );

		ParkObjectPreview.StartRunning( animations );

		var channel = animations.Channel( 0 )!;

		Assert.IsFalse( channel.IsIdle );
		Assert.AreEqual( 5, channel.AnimID );
		Assert.AreEqual( 0, channel.SubAnim );
		Assert.AreNotEqual( 0, channel.Flags & AnimTimeControl.LoopFlag, "looped" );

		// A whole length and a half on, it is half way through again, not held at its end.
		var length = animations.DurationMilliseconds( 5, 0 );
		animations.Advance( length + (length / 2) );

		Assert.IsFalse( animations.Channel( 0 )!.IsIdle );
		Assert.AreEqual( animations.FramesFor( 5, 0 ) / 2f, animations.Channel( 0 )!.AnimFrame, 1.5f );
	}

	/// <summary>
	/// The name board goes onto a <c>P</c> model only where both models name both halves: the Aztec Mayhem's does,
	/// the first coaster's <c>P</c> model names neither.
	/// </summary>
	[TestMethod]
	public void TheNameBoardIsCarriedOnlyWhereBothModelsNameBothHalves()
	{
		FileSystem = GameData.Required();

		Assert.IsTrue( ParkObjectPreview.CarriesSignOnto(
			new ModelFile( "levels/jungle/rides/tvsim/tvsim.MD2" ), new ModelFile( "levels/jungle/rides/tvsim/Ptvsim.MD2" ) ) );

		Assert.IsFalse( ParkObjectPreview.CarriesSignOnto(
			new ModelFile( "levels/jungle/rides/coaster1/coaster1.MD2" ), new ModelFile( "levels/jungle/rides/coaster1/Pcoaster1.md2" ) ) );
	}

	/// <summary>One half named is not enough, on either model: all four must be found.</summary>
	[TestMethod]
	public void ANameBoardWithOneHalfMissingIsNotCarried()
	{
		string[] both = ["wall", "Sign1", "sign2"];

		Assert.IsTrue( ParkObjectPreview.CarriesSignOnto( both, both ) );
		Assert.IsFalse( ParkObjectPreview.CarriesSignOnto( both, ["wall", "sign1"] ) );
		Assert.IsFalse( ParkObjectPreview.CarriesSignOnto( both, ["sign2"] ) );
		Assert.IsFalse( ParkObjectPreview.CarriesSignOnto( ["sign1"], both ) );
		Assert.IsFalse( ParkObjectPreview.CarriesSignOnto( ["sign2", "wall"], both ) );
	}
}
