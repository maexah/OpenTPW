using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A held scream is a chain of samples on its ride's own clock (<see cref="ParkScreams"/>), and a
/// category's variation headers say how long each link waits (<see cref="SoundCategoryFile.ReadVariations"/>).
/// These read the shipped maps and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// The chain is driven here with no sound device: <see cref="ParkScreams"/> is handed a player that only
/// writes down what it was asked for, so what is pinned is WHEN and WHICH, and the sound itself rests on
/// the game run.
/// </para>
/// </summary>
[TestClass]
public class ParkScreamChainTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		// SoundCategoryFile and SoundBank read through the GLOBAL file system - see SoundCategoryTests.
		FileSystem = GameData.Required();
	}

	private static readonly (string Directory, string Name)[] EveryCategory =
	[
		("global/sound", "ambient"), ("global/sound", "globallobbysfx"), ("global/sound", "kids"),
		("global/sound", "rides"), ("global/sound", "staff"), ("global/sound", "ui"), ("global/Speech", "speech"),
		.. new[] { "fantasy", "hallow", "jungle", "space" }.SelectMany( theme => new[]
		{
			($"levels/{theme}/Music", "music"), ($"levels/{theme}/Sound", "ambient"),
			($"levels/{theme}/Sound", "locallobbymusic"), ($"levels/{theme}/Sound", "locallobbysfx"),
			($"levels/{theme}/Sound", "rides"), ($"levels/{theme}/Speech", "speech")
		} )
	];

	private static IReadOnlyList<SoundCategoryFile.Variation> Kids( int effect )
	{
		var kids = new SoundCategoryFile( "global/sound", "kids" );

		Assert.IsTrue( kids.IsValid, "the global kids category should read" );

		return kids.ReadVariations()[kids.IndexOf( effect )];
	}

	/// <summary>
	/// The walk is strict - it answers nothing unless it ends exactly at the end of the file - so every
	/// shipped category answering at all is the layout agreeing with all of them, to the byte.
	/// </summary>
	[TestMethod]
	public void EveryShippedCategoryWalksToItsLastByte()
	{
		Assert.AreEqual( 31, EveryCategory.Length, "the game ships thirty-one categories" );

		int effects = 0, variations = 0;

		foreach ( var (directory, name) in EveryCategory )
		{
			var file = new SoundCategoryFile( directory, name );

			Assert.IsTrue( file.IsValid, $"{directory}/{name} should read" );

			var walked = file.ReadVariations();

			Assert.AreEqual( file.Effects.Count, walked.Count, $"{directory}/{name} should walk to its last byte" );

			for ( int i = 0; i < walked.Count; ++i )
				Assert.AreEqual( file.Effects[i].Variations, walked[i].Count, $"{directory}/{name} effect {file.Effects[i].Id}" );

			effects += walked.Count;
			variations += walked.Sum( effect => effect.Count );
		}

		Assert.AreEqual( 1267, effects, "effects across every category" );
		Assert.AreEqual( 1595, variations, "variations across every category" );
	}

	/// <summary>
	/// The walk steps over the sample records by the count each header gives, and the older reader
	/// finds them by what they contain. They have to agree list for list, or a chain's variation would
	/// name one list while the category played another.
	/// </summary>
	[TestMethod]
	public void TheWalkAndTheSampleScanFindTheSameLists()
	{
		foreach ( var (root, directory, name) in new[]
		{
			("global", "global/sound", "kids"), ("global", "global/sound", "ambient"),
			("levels/jungle", "levels/jungle/Sound", "ambient"), ("levels/jungle", "levels/jungle/Music", "music")
		} )
		{
			var file = new SoundCategoryFile( directory, name );
			var durations = file.Banks
				.Select( bank => (IReadOnlyList<TimeSpan>)(SoundBank.Load( root, bank )?.Durations ?? []) )
				.ToList();

			var scanned = file.ReadSamples( durations );
			var walked = file.ReadVariations();

			Assert.AreEqual( file.Effects.Count, walked.Count, $"{directory}/{name} should walk" );

			for ( int i = 0; i < walked.Count; ++i )
				CollectionAssert.AreEqual( scanned[i].Select( list => list.Count ).ToArray(),
					walked[i].Select( variation => variation.Samples ).ToArray(),
					$"{directory}/{name} effect {file.Effects[i].Id}: the lists' lengths" );
		}
	}

	/// <summary>
	/// The four held screams, as shipped: four variations each, one wait apiece, and zones that send a
	/// chain to variation 1, 2, 3 or 4 by a parameter of 0-25, 26-50, 51-75 or 76-100.
	/// </summary>
	[TestMethod]
	public void TheHeldScreamsWaitWhatTheirVariationsSay()
	{
		foreach ( var (effect, low, high) in new[] { (71, 1000, 3000), (72, 500, 2000), (73, 100, 1000), (74, 0, 500) } )
		{
			var variations = Kids( effect );

			Assert.AreEqual( 4, variations.Count, $"effect {effect}'s variations" );

			foreach ( var variation in variations )
			{
				Assert.AreEqual( (low, high), (variation.GapMin, variation.GapMax ), $"effect {effect}'s wait" );
				Assert.AreEqual( 0, variation.GapByParameter, $"effect {effect} draws its wait at random" );
				CollectionAssert.AreEqual(
					new[] { new SoundCategoryFile.Zone( 0, 0, 25 ), new SoundCategoryFile.Zone( 1, 26, 50 ),
						new SoundCategoryFile.Zone( 2, 51, 75 ), new SoundCategoryFile.Zone( 3, 76, 100 ) },
					variation.Zones.ToArray(), $"effect {effect}'s zones" );
			}

			CollectionAssert.AreEqual( new long[] { 16383, 16383, 16383, 16383 },
				variations.Select( variation => variation.Weight ).ToArray(),
				$"effect {effect}'s running totals, turned into shares" );
		}
	}

	/// <summary>A chain driver with a player that writes down what it was asked for, and nothing else.</summary>
	private static (ParkScreams Screams, List<(ParkScreams.Chain Chain, int Variation)> Played) Driver( int seed = 1 )
	{
		var played = new List<(ParkScreams.Chain, int)>();
		var variations = new Dictionary<int, IReadOnlyList<SoundCategoryFile.Variation>>();

		var screams = new ParkScreams(
			effect => variations.TryGetValue( effect, out var known ) ? known : variations[effect] = Kids( effect ),
			( chain, variation ) =>
			{
				played.Add( (chain, variation) );
				return null;
			},
			new Random( seed ) );

		return (screams, played);
	}

	private static readonly Vector3 Here = new( 425f, 252f, 5f );

	/// <summary>
	/// The heart of it. Two rides holding the SAME scream each make their first child at once, side by
	/// side: nothing in the original is kept per effect for one to wait on the other.
	/// </summary>
	[TestMethod]
	public void TwoRidesHoldingTheSameScreamBothSoundAtOnce()
	{
		var (screams, played) = Driver();

		var first = screams.Start( 13, 0x47, 1, 20, Here, now: 100f );
		var second = screams.Start( 43, 0x47, 1, 20, Here, now: 100f );

		Assert.AreEqual( 2, played.Count, "both rides' first children, in the same instant" );
		Assert.AreSame( first, played[0].Chain );
		Assert.AreSame( second, played[1].Chain );
		Assert.AreEqual( 1, first.Plays );
		Assert.AreEqual( 1, second.Plays );
	}

	/// <summary>
	/// The item this was built for: one ride stopping must not set the other screaming. The other
	/// ride's time is its own, and its next child comes then and not a moment before.
	/// </summary>
	[TestMethod]
	public void StoppingOneRideLeavesTheOtherOnItsOwnClock()
	{
		var (screams, played) = Driver();

		screams.Start( 13, 0x47, 1, 20, Here, now: 100f );
		var other = screams.Start( 43, 0x47, 1, 20, Here, now: 100f );

		var due = other.NextAt;
		var stoppedAt = due - 0.2f;

		Assert.IsTrue( stoppedAt > 100.5f, $"the other ride is between children when the first stops (due {due})" );

		screams.Pump( stoppedAt - 0.01f );
		var before = other.Plays;

		Assert.IsNotNull( screams.Stop( 13 ), "the first ride was holding a scream" );
		Assert.IsNull( screams.Find( 13 ), "and holds none now" );

		screams.Pump( stoppedAt + 0.016f );

		Assert.AreEqual( before, other.Plays, "a frame after the stop, the other ride has made nothing new" );
		Assert.AreEqual( due, other.NextAt, "and its time is untouched" );

		var count = played.Count;
		screams.Pump( due + 0.001f );

		Assert.AreEqual( before + 1, other.Plays, "once its own time passes, it screams again" );
		Assert.AreEqual( count + 1, played.Count );
		Assert.AreSame( other, played[^1].Chain );
	}

	/// <summary>
	/// The first child takes the effect's first variation whatever the level; every one after goes
	/// where the zones send the parameter, <c>(level + 50) / 2</c>. Lost Kingdom's Belly Bounce asks
	/// for 20, which is 35, which is variation 2.
	/// </summary>
	[TestMethod]
	public void AChainStartsOnItsFirstVariationAndThenFollowsItsLevel()
	{
		foreach ( var (level, later) in new[] { (20, 1), (-100, 0), (90, 2), (200, 3) } )
		{
			var (screams, played) = Driver();
			var chain = screams.Start( 13, 0x47, 1, level, Here, now: 0f );

			for ( int i = 0; i < 5; ++i )
				screams.Pump( chain.NextAt + 0.001f );

			Assert.AreEqual( 0, played[0].Variation, $"level {level}: the first child's variation" );
			CollectionAssert.AreEqual( Enumerable.Repeat( later, 5 ).ToArray(),
				played.Skip( 1 ).Select( play => play.Variation ).ToArray(), $"level {level}: every later child's" );
		}
	}

	/// <summary>
	/// Each child's wait is drawn from its variation's own range and counted from its own start - the
	/// sample's length does not come into it, and neither does the 2700 the effect record carries.
	/// </summary>
	[TestMethod]
	public void EachChildWaitsWithinItsVariationsRange()
	{
		foreach ( var (effect, low, high) in new[] { (0x47, 1000, 3000), (0x49, 100, 1000), (0x4a, 0, 500) } )
		{
			var (screams, _) = Driver( seed: effect );
			var chain = screams.Start( 13, effect, 1, 20, Here, now: 10f );
			var gaps = new List<int>();

			for ( int i = 0; i < 200; ++i )
			{
				var started = chain.NextAt + 0.001f;

				gaps.Add( chain.Gap );
				screams.Pump( started );

				Assert.AreEqual( started + (chain.Gap / 1000f), chain.NextAt, 0.0005f, "the next time counts from this child's start" );
			}

			Assert.IsTrue( gaps.All( gap => gap >= low && gap < high ), $"effect {effect}: every wait within {low}..{high}" );
			Assert.IsTrue( gaps.Max() - gaps.Min() > (high - low) / 2, $"effect {effect}: and drawn across it" );
		}
	}

	/// <summary>
	/// What a chain plays is picked from its own variation, and the throttle <see cref="SoundCategory.Play"/>
	/// keeps per effect is neither asked nor set by it - so a play of the same effect just made cannot
	/// stop a chain's child.
	/// </summary>
	[TestMethod]
	public void AChainsPickIsNotHeldUpByTheEffectsThrottle()
	{
		var kids = new SoundCategory( "global", "global/sound", "kids" );

		Assert.IsTrue( kids.IsValid, "the global kids category should load" );
		Assert.IsNotNull( kids.PickFrom( 0x47, 0 ), "variation 1 of effect 71 has samples" );

		// Sets the effect's throttle as a play does, device or not.
		kids.Play( 0x47 );

		for ( int variation = 0; variation < 4; ++variation )
			Assert.IsNotNull( kids.PickFrom( 0x47, variation ), $"variation {variation + 1}, straight after a play" );

		Assert.IsNull( kids.PickFrom( 0x47, 4 ), "there is no fifth" );
	}
}
