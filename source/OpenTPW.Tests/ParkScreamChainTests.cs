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
/// Most of these drive the chain with no sound device: <see cref="ParkScreams"/> is handed a player that only
/// writes down what it was asked for, so what is pinned is WHEN and WHICH. The last two drive it through a
/// park, with a device stood in, and pin the park's update pumping it and the voices each child becomes.
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
	/// Every sample record the walk reads names a bank of its category and a sample in it, and the
	/// categories play these. The scan by content cannot see a variation with no samples, so the effects after hallow's
	/// 98 and 103 and space's 112 take the wrong lists, and hallow's 188 and 190 and space's 191 are left empty.
	/// </summary>
	[TestMethod]
	public void EveryWalkedSampleRecordNamesASampleOfItsCategory()
	{
		var records = 0;
		var differ = new List<string>();
		var emptied = new List<string>();

		foreach ( var (directory, name) in EveryCategory )
		{
			var file = new SoundCategoryFile( directory, name );
			var root = directory.StartsWith( "global" ) ? "global" : string.Join( '/', directory.Split( '/' ).Take( 2 ) );
			var banks = file.Banks.Select( bank => SoundBank.Load( root, bank ) ).ToList();
			var walked = file.ReadVariations();
			var scanned = file.ReadSamples( banks.Select( bank => (IReadOnlyList<TimeSpan>)(bank?.Durations ?? []) ).ToList() );

			for ( int i = 0; i < walked.Count; ++i )
			{
				var lists = walked[i].Select( variation => variation.Records! ).ToList();

				foreach ( var sample in lists.SelectMany( list => list ) )
				{
					Assert.IsTrue( sample.Bank >= 0 && sample.Bank < banks.Count && banks[sample.Bank] != null,
						$"{directory}/{name} effect {file.Effects[i].Id}: bank {sample.Bank}" );
					Assert.IsTrue( sample.Index >= 0 && sample.Index < banks[sample.Bank]!.Durations.Count,
						$"{directory}/{name} effect {file.Effects[i].Id}: sample {sample.Index}" );
					records++;
				}

				var same = lists.Count == scanned[i].Count
					&& lists.Zip( scanned[i] ).All( pair => pair.First.SequenceEqual( pair.Second ) );

				if ( !same )
					differ.Add( $"{directory}/{name} {file.Effects[i].Id}" );

				if ( lists.Sum( list => list.Count ) > 0 && scanned[i].Sum( list => list.Count ) == 0 )
					emptied.Add( $"{directory}/{name} {file.Effects[i].Id}" );
			}
		}

		Assert.AreEqual( 3631, records, "sample records across every category" );
		Assert.AreEqual( 94, differ.Count, "effects the scan reads otherwise" );
		Assert.IsFalse( differ.Any( effect => effect.StartsWith( "global" ) ), "no global category reads otherwise" );
		Assert.IsFalse( differ.Any( effect => effect.Contains( "jungle" ) ), "no jungle category reads otherwise" );
		CollectionAssert.AreEquivalent( new[] { "levels/hallow/Sound/rides 188", "levels/hallow/Sound/rides 190",
			"levels/space/Sound/rides 191" }, emptied, "the effects the scan leaves empty" );
	}

	/// <summary>
	/// The walk steps over the sample records by the count each header gives, and the sample scan
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
				Assert.AreEqual( 0, variation.FirstMask, $"effect {effect} draws its wait at random" );
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

	/// <summary>
	/// A chain driver with a player that writes down what it was asked for, and nothing else, heard from
	/// <paramref name="ears"/> (at <see cref="Here"/>, beside every chain these start, when not given).
	/// </summary>
	private static (ParkScreams Screams, List<(ParkScreams.Chain Chain, int Variation)> Played) Driver( int seed = 1,
		Func<Vector3>? ears = null )
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
			ears ?? (() => Here),
			new Random( seed ) );

		return (screams, played);
	}

	private static readonly Vector3 Here = new( 425f, 252f, 5f );

	/// <summary>
	/// A chain out of range of the listener makes no child, its first included, as the original's sound service ticks
	/// only the voices in range (<c>SoundService_Pass</c>, <c>0x006b62b0</c>): over the ground's two axes alone, against
	/// the newest child's variation's range (95 on every kids' scream), a distance of exactly the range in. In range again
	/// it makes one child at once and no more in that pass, however many waits passed while it was away.
	/// </summary>
	[TestMethod]
	public void AScreamOutOfRangeMakesNoChild()
	{
		var ears = Here;
		var (screams, played) = Driver( ears: () => ears );

		var chain = screams.Start( 13, 0x47, 1, 20, Here, now: 100f );

		Assert.AreEqual( 1, played.Count, "in range, the first child at once" );
		Assert.IsFalse( chain.Away );

		// The other ground axis, 96 off: out, where a rule over x and the height would call it in.
		ears = Here + new Vector3( 0f, 96f, 0f );

		for ( var now = 101f; now < 140f; now += 0.5f )
			screams.Pump( now );

		Assert.AreEqual( 1, played.Count, "out of range, no child through 39 s of passes" );
		Assert.AreEqual( 1, chain.Plays );
		Assert.IsTrue( chain.Away, "the original's 0x800" );

		ears = Here + new Vector3( 0f, 95f, 0f );
		screams.Pump( 140f );

		Assert.AreEqual( 2, played.Count, "exactly the range off is in range: one child at once" );
		Assert.IsFalse( chain.Away );

		screams.Pump( 140f );
		Assert.AreEqual( 2, played.Count, "one child a pass, never one for each wait missed" );

		// The height counts for nothing.
		ears = Here + new Vector3( 0f, 0f, 500f );
		screams.Pump( chain.NextAt + 0.01f );
		Assert.AreEqual( 3, played.Count, "500 above the ride is still in range" );

		// Started out of range, the first child waits for the listener, and is still the effect's first variation.
		ears = Here + new Vector3( 200f, 0f, 0f );
		var late = screams.Start( 43, 0x47, 1, 20, Here, now: 150f );

		Assert.AreEqual( 3, played.Count, "a chain started away makes nothing" );
		Assert.AreEqual( 0, late.Plays );
		Assert.IsTrue( late.Away );

		screams.Stop( 13 );
		screams.Pump( 160f );
		Assert.AreEqual( 3, played.Count );

		ears = Here;
		screams.Pump( 161f );

		Assert.AreEqual( 4, played.Count, "in range, its first child" );
		Assert.AreSame( late, played[^1].Chain );
		Assert.AreEqual( 0, played[^1].Variation, "the first variation, whatever the wait" );

		// A chain a second start let go is in the same pass, and out of range makes nothing either.
		screams.LetGo( 43 );
		ears = Here + new Vector3( -200f, 0f, 0f );
		screams.Pump( 200f );
		Assert.AreEqual( 4, played.Count, "the let-go chain away makes no child" );

		ears = Here;
		screams.Pump( 201f );
		Assert.AreEqual( 5, played.Count, "and one in range again" );
	}

	/// <summary>
	/// A chain is in range by its own variation's range (<c>FUN_006bc650</c>), which each child's draw writes
	/// (<c>0x006c3e00</c>): with hand-made variations of 50 and 95, a listener 70 off is out with the first and in with
	/// the second.
	/// </summary>
	[TestMethod]
	public void AChainIsInRangeByItsOwnVariation()
	{
		var variations = new SoundCategoryFile.Variation[]
		{
			new( 1, 0, 0, 0, 1, [], Range: 50f ),
			new( 1, 0, 0, 0, 1, [], Range: 95f ),
		};
		var chain = new ParkScreams.Chain { Effect = 0x47, Band = 1, At = Here };
		var ears = Here + new Vector3( 70f, 0f, 0f );

		chain.RangeVariation = 1;
		Assert.IsTrue( ParkScreams.InRange( chain, variations, ears ), "the second's 95" );

		chain.RangeVariation = 0;
		Assert.IsFalse( ParkScreams.InRange( chain, variations, ears ), "the first's 50" );
	}

	/// <summary>
	/// <b>Before its first child a chain ranges by its set-up's draw</b> (<c>0x006bbfc5</c> -> <c>FUN_006bc810</c>, by
	/// weight among all), not by the effect's first variation, and its first child still plays the first and writes it
	/// into the chain (<c>0x006bdb2b</c>, <c>0x006c3e00</c>). Hand-made: the first of range 50 and weight 1, the second of
	/// range 95 and weight a million, a listener 70 off. The set-up draws the second, so the chain is in range at once and
	/// makes its first child, of the first variation; from then on it ranges by 50 and makes no more.
	/// </summary>
	/// <remarks>The nought at the first child is not observed: the held child writes its own variation into the chain straight after, so deleting the nought passes too.</remarks>
	[TestMethod]
	public void BeforeItsFirstChildAChainRangesByItsSetUpsDraw()
	{
		var variations = new SoundCategoryFile.Variation[]
		{
			new( 1, 100, 100, 0, 1, [], Range: 50f ),
			new( 1, 100, 100, 0, 1_000_000, [], Range: 95f ),
		};
		var played = new List<int>();
		var screams = new ParkScreams( _ => variations, ( _, variation ) =>
		{
			played.Add( variation );
			return null;
		}, () => Here + new Vector3( 70f, 0f, 0f ), new Random( 1 ) );

		var chain = screams.Start( 1, 0x47, 1, 100, Here, 0f );

		Assert.AreEqual( 1, played.Count, "in range by the set-up's 95: its first child at once" );
		Assert.AreEqual( (0, 0, false), (played[0], chain.RangeVariation, chain.Away), "the effect's first, written into the chain" );

		screams.Pump( 10f );
		Assert.AreEqual( (1, true), (played.Count, chain.Away), "then out of range by the child's 50" );
	}

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
		// Effect 71's lists end between 65,528 and 65,534, so the roll is the test's own: a roll above a list's last
		// odds gives no sample (Q723), which is not what this test asks about.
		FixedRoll.During( 0, dice =>
		{
			var kids = new SoundCategory( "global", "global/sound", "kids" );

			Assert.IsTrue( kids.IsValid, "the global kids category should load" );
			Assert.IsNotNull( kids.PickFrom( 0x47, 0 ), "variation 1 of effect 71 has samples" );

			// Sets the effect's throttle as a play does, device or not.
			kids.Play( 0x47 );

			for ( int variation = 0; variation < 4; ++variation )
				Assert.IsNotNull( kids.PickFrom( 0x47, variation ), $"variation {variation + 1}, straight after a play" );

			Assert.IsNull( kids.PickFrom( 0x47, 4 ), "there is no fifth" );
		} );
	}
	/// <summary>
	/// A child is due only once the clock has passed its time, not when it reaches it - the original's
	/// <c>now &gt; +0x1c</c> (<c>0x006bdb88</c>).
	/// </summary>
	[TestMethod]
	public void AChildIsDueOnlyOnceItsTimeHasPassed()
	{
		var (screams, played) = Driver();
		var chain = screams.Start( 13, 0x47, 1, 20, Here, now: 100f );

		screams.Pump( chain.NextAt );

		Assert.AreEqual( 1, chain.Plays, "at its time exactly, not yet" );

		screams.Pump( chain.NextAt + 0.001f );

		Assert.AreEqual( 2, chain.Plays, "just past it, the next child" );
		Assert.AreEqual( 2, played.Count );
	}

	/// <summary>
	/// On the very millisecond a child is due the original's tick extends nothing (<c>now &gt; +0x1c</c>,
	/// <c>0x006bdb88</c>) and prunes it (<c>now &gt;= +0x1c</c>, <c>0x006bdc23</c>), both in whole milliseconds, so the
	/// list is empty for that pass, however far into the millisecond the frame falls, and the next pass makes the child as
	/// a first: with the chain's <c>0x20</c> set by its first child, from its own variation, so where its level sends it
	/// (Belly Bounce's 20 is variation 2), not the effect's first.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the time compared as a fraction of a second (a child made inside its due millisecond); a child
	/// made on the due millisecond; the child after the empty pass taken from the effect's first variation.
	/// </remarks>
	[TestMethod]
	public void AChildDueOnTheMillisecondEmptiesTheListForAPass()
	{
		var kids = new SoundCategory( "global", "global/sound", "kids" );

		Assert.AreNotEqual( 0, kids.ParameterOf( 0x47 ), "the scream has a key, so its first child sets 0x20" );

		var (screams, played) = Driver();
		var chain = screams.Start( 13, 0x47, 1, 20, Here, now: 100f );
		var due = ParkAudio.Milliseconds( chain.NextAt );
		var inside = chain.NextAt + 0.0002f;

		Assert.IsTrue( inside > chain.NextAt && ParkAudio.Milliseconds( inside ) == due, "the frame falls past the time, in its millisecond" );

		screams.Pump( inside );

		Assert.AreEqual( 1, chain.Plays, "on the due millisecond no child is made" );
		Assert.IsTrue( chain.ListEmpty, "and the newest is pruned, the list empty" );

		var next = inside + 0.016f;

		screams.Pump( next );

		Assert.AreEqual( 2, chain.Plays, "the next pass makes the child" );
		Assert.IsFalse( chain.ListEmpty );
		CollectionAssert.AreEqual( new[] { 0, 1 }, played.Select( play => play.Variation ).ToArray(),
			"the first child on variation 1, the one after the empty pass where the level sends it" );
		Assert.AreEqual( next + (chain.Gap / 1000f), chain.NextAt, 0.0005f, "its wait counted from that pass" );
	}

	/// <summary>A driver whose children each get a voice of their own, never handed to the mixer, so a stop is seen on it.</summary>
	private static (ParkScreams Screams, List<Voice> Voices) VoicedDriver( Random random )
	{
		var bank = SoundBank.Load( "global", "Sound/Kids" );

		Assert.IsNotNull( bank, "global/Sound/KidsHD.sdt is the bank cat_kids names, and it ships" );

		var voices = new List<Voice>();
		var variations = new Dictionary<int, IReadOnlyList<SoundCategoryFile.Variation>>();
		var screams = new ParkScreams(
			effect => variations.TryGetValue( effect, out var known ) ? known : variations[effect] = Kids( effect ),
			( chain, variation ) =>
			{
				var voice = new Voice( bank![0]!, 0.5f, loop: false, 0f, AudioBus.Effects, Here );

				voices.Add( voice );
				return voice;
			},
			() => Here,
			random );

		return (screams, voices);
	}

	/// <summary>Draws nought every time: each wait its variation's shorter bound, each zone the first.</summary>
	private sealed class Noughts : Random
	{
		public override int Next( int maxValue ) => 0;

		public override long NextInt64( long maxValue ) => 0;
	}

	/// <summary>
	/// <b>A stop on the pass that empties a chain's list cuts no child</b>: the prune deletes the newest child without
	/// stopping its channel (<c>0x006bdc3e</c>) and the stop walks only the list (<c>0x006bd9b0</c>). Two rides hold effect
	/// 71; the frame falls in the first's due millisecond, so its child is pruned and let go, and both stop then: the first's
	/// child plays on, the second's, still in its list, is cut.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the prune keeping the voice (the stop cuts it); the stop cutting a chain's child whatever its list
	/// holds; no stop of a child still in the list.
	/// </remarks>
	[TestMethod]
	public void AStopOnTheEmptyPassCutsNoChild()
	{
		var (screams, voices) = VoicedDriver( new Random( 1 ) );
		var first = screams.Start( 13, 0x47, 1, 20, Here, now: 100f );
		var second = screams.Start( 43, 0x47, 1, 20, Here, now: 100.5f );
		var inside = first.NextAt + 0.0002f;

		Assert.AreEqual( ParkAudio.Milliseconds( first.NextAt ), ParkAudio.Milliseconds( inside ), "the frame falls in the first's due millisecond" );
		Assert.IsTrue( ParkAudio.Milliseconds( inside ) < ParkAudio.Milliseconds( second.NextAt ), "and before the second's" );

		screams.Pump( inside );

		Assert.AreEqual( (1, 1), (first.Plays, second.Plays), "no child made on that pass" );
		Assert.IsTrue( first.ListEmpty && !second.ListEmpty, "the first's list pruned empty, the second's not" );
		Assert.IsNull( first.Voice, "the pruned child let go" );

		screams.Stop( 13 );
		screams.Stop( 43 );

		Assert.AreEqual( 2, voices.Count );
		Assert.IsTrue( voices[0].Playing, "the pruned child's sample is not cut by the stop" );
		Assert.IsFalse( voices[1].Playing, "the child still in the second's list is" );
	}

	/// <summary>
	/// <b>A child whose wait is nought is pruned in the pass that makes it</b>: its time is the pass's own
	/// (<c>0x006c3ac5</c>), and the tick's prune (<c>now &gt;= +0x1c</c>, <c>0x006bdc23</c>) follows its extend with the one
	/// clock reading. Effect 74's waits are 0 to 500 ms; drawn nought, the chain's first child leaves its list empty as it is
	/// made, a stop then cuts nothing, and a chain not stopped makes its next child on the next pass, from the empty list.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no prune after the start's child; no prune after a tick's extend; the pruned child's voice kept.
	/// </remarks>
	[TestMethod]
	public void AChildWithNoWaitIsPrunedInThePassThatMakesIt()
	{
		var (screams, voices) = VoicedDriver( new Noughts() );
		var stopped = screams.Start( 13, 0x4a, 8, 20, Here, now: 100f );

		Assert.AreEqual( (1, 0), (stopped.Plays, stopped.Gap), "its first child, with a wait of nought" );
		Assert.IsTrue( stopped.ListEmpty, "pruned in the pass that made it" );
		Assert.IsNull( stopped.Voice );

		screams.Stop( 13 );
		Assert.IsTrue( voices[0].Playing, "a stop then cuts nothing" );

		var going = screams.Start( 43, 0x4a, 8, 20, Here, now: 100f );

		screams.Pump( 100.016f );

		Assert.AreEqual( (2, 0), (going.Plays, going.Gap), "the next pass makes its child from the empty list, again with no wait" );
		Assert.IsTrue( going.ListEmpty, "and prunes it in that pass" );
		Assert.IsNull( going.Voice );
		Assert.IsTrue( voices[1].Playing && voices[2].Playing, "both let go, neither stopped" );
	}

	/// <summary>
	/// <b>The park's music, through <c>ParkAudio.OnUpdate</c> as the game runs it</b> (<c>docs/exe/park-engine.md</c>,
	/// "The music's level"). The level is set only on a step whose count has its low five bits nought, from the
	/// guests the park counts then (the ones on a counting cell, Q148), held to 89, and nought while the world's state is 4. A sample is played at the
	/// music's one loudness whatever the level, and when it has ended the next comes from the variation whose zone
	/// holds the level: the first at level 6, the sixth at 89, the first again at nought, never silence.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the level set every frame; every guest counted wherever they stand; the crowd's level set without the hold and the state; the level
	/// never falling; the world's state not handed in; what is played scaled by the level; the variation never
	/// moving on.
	/// </remarks>
	[TestMethod]
	public void TheParksMusicFollowsTheGuestsOnTheBeatAtOneLoudness()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var levelBefore = Level.Current;
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), random: new Random( 1 ),
			behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			InAPark( "jungle", park =>
			{
				Voice Music()
				{
					lock ( Audio.Lock )
						return Audio.Voices.Last( voice => voice.Bus == AudioBus.Music );
				}

				// Frames of a tick or so, the park's audio updated on each, up to and including the next beat.
				void ToTheBeat()
				{
					var beat = (GameClock.Ticks | 0x1f) + 1;

					while ( GameClock.Ticks < beat )
					{
						Frame( held: false, GameClock.TickSeconds );
						ParkLoop.Run( null, null, null, audio: park );
						park.Update();
					}
				}

				park.Update();
				Assert.AreEqual( (0, 0, 0), (park.LevelNow, park.LevelSets, park.VariationNow), "the first sample is the first variation's" );

				var first = Music();

				Assert.AreEqual( ParkAudio.MusicVolume, first.Volume, 0.0001f, "at the music's loudness with the level nought" );
				StringAssert.StartsWith( first.Name.ToLowerInvariant(), "level1" );

				ToTheBeat();
				Assert.AreEqual( (0, 1), (park.LevelNow, park.LevelSets), "thirteen guests, all on the bus stop's type 30 approach: none counts (FUN_004fa990), set on the beat" );

				// The guests walk onto the path (the cells they stand on, and the stop's, which the guests admitted below arrive on).
				var state = people.State;

				foreach ( var (x, y) in people.Peeps.Select( peep => peep.Navigator.Position.Cell ).Append( (42, 5) ).Distinct().ToList() )
					state.SetRecord( x, y, state.Record( x, y ) with { Type = 1 } );

				ToTheBeat();
				Assert.AreEqual( (6, 2), (park.LevelNow, park.LevelSets), "thirteen on the path: level 6, set on the beat" );

				ToTheBeat();
				Assert.AreEqual( 3, park.LevelSets, "and set again 32 ticks on, not on the frames between" );
				Assert.AreSame( first, Music(), "while a sample plays no other is started" );

				// Two hundred guests: the crowd's level is 100 and the music's is held to 89.
				var came = new List<int>();

				while ( people.Peeps.Count < 200 )
				{
					came.Add( people.Admit( 42, 5 ) );
					Assert.AreNotEqual( 0, came[^1], "the stop takes another" );
				}

				ToTheBeat();
				Assert.AreEqual( 89, park.LevelNow, "two hundred guests: held to 89" );

				first.Stop();
				park.Update();

				var loud = Music();

				Assert.AreNotSame( first, loud, "the sample ended, the next is started" );
				Assert.AreEqual( 5, park.VariationNow, "from the sixth variation, whose zone holds 89" );
				StringAssert.StartsWith( loud.Name.ToLowerInvariant(), "level6" );
				Assert.AreEqual( ParkAudio.MusicVolume, loud.Volume, 0.0001f, "at the same loudness" );

				// Each sample after the first asks the draw for its step against the voice's last pick (0x006bde33): where the
				// variation it came from holds more than two samples, it is never the same index twice running.
				var headers = new SoundCategory( "levels/jungle", "levels/jungle/Music", "music", keep: true ).VariationsOf( 2 );
				var pickBefore = park.MusicLastSample;
				var stepped = 0;

				for ( var draw = 0; draw < 150; ++draw )
				{
					Music().Stop();
					park.Update();

					if ( headers[park.VariationNow].Samples > 2 )
					{
						Assert.AreNotEqual( pickBefore, park.MusicLastSample, $"draw {draw + 1}: the sample before is not drawn again" );
						++stepped;
					}

					pickBefore = park.MusicLastSample;
				}

				Assert.IsTrue( stepped > 100, $"the music's lists hold more than two samples ({stepped} of 150 draws)" );

				// They go again: the level falls with them.
				foreach ( var id in came )
					Assert.IsTrue( people.Depart( id ) );

				ToTheBeat();
				Assert.AreEqual( 6, park.LevelNow, "thirteen guests again: level 6" );

				// World state 4: the level is nought, and the music is the first variation's, not silence.
				people.State.SetWorldState( ParkState.EndedWorldState );

				ToTheBeat();
				Assert.AreEqual( 0, park.LevelNow, "world state 4: nought" );

				Music().Stop();
				park.Update();
				Assert.AreEqual( 0, park.VariationNow, "the first variation again" );
				Assert.AreEqual( ParkAudio.MusicVolume, Music().Volume, 0.0001f, "and as loud as ever" );
			} );
		}
		finally
		{
			Level.Current = levelBefore!;
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>What the park loop hands the crowd's voice, and what a sample's volume and pitch are under it</b>
	/// (<c>docs/exe/audio.md</c>, "The crowd's voice"): the guests round the pointer held to a hundred and nought in
	/// world state 4; on kids 91's first six variations the volume is <c>level × 8 / 100 + 14</c> in whole numbers,
	/// on their twins a draw from 14 to 21, and the pitch a draw from 0 to 5 on all twelve.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the hold left off; state 4 not read; the volume not floored; the twins' volume the level's;
	/// the draw reaching its top; the pitch the level's.
	/// </remarks>
	[TestMethod]
	public void TheCrowdsVoiceTakesItsVolumeFromTheLevelOnTheFirstSixVariationsAndADrawOnTheirTwins()
	{
		Assert.AreEqual( 0, ParkAudio.CrowdVoiceLevel( 0, 0 ) );
		Assert.AreEqual( 12, ParkAudio.CrowdVoiceLevel( 12, 0 ) );
		Assert.AreEqual( 100, ParkAudio.CrowdVoiceLevel( 100, 0 ) );
		Assert.AreEqual( 100, ParkAudio.CrowdVoiceLevel( 250, 0 ), "held to a hundred" );
		Assert.AreEqual( 0, ParkAudio.CrowdVoiceLevel( 40, ParkAudio.SilentWorldState ), "nought in world state 4" );

		var headers = new SoundCategoryFile( "global/sound", "kids" ).ReadVariations()
			[new SoundCategoryFile( "global/sound", "kids" ).IndexOf( ParkAudio.CrowdVoiceEffect )];
		var random = new Random( 5 );

		Assert.AreEqual( 12, headers.Count, "kids 91 has twelve variations" );

		for ( var variation = 0; variation < 6; ++variation )
		{
			foreach ( var (level, volume) in new[] { (1, 14), (12, 14), (13, 15), (24, 15), (25, 16), (50, 18), (99, 21), (100, 22) } )
			{
				Assert.AreEqual( volume, ParkAudio.Controlled( headers[variation], 1, ParkAudio.CrowdVoiceParameter, level, random ),
					$"variation {variation + 1} at level {level}" );
			}
		}

		for ( var variation = 0; variation < 12; ++variation )
		{
			var volumes = new HashSet<int>();
			var pitches = new HashSet<int>();

			for ( var draw = 0; draw < 400; ++draw )
			{
				volumes.Add( ParkAudio.Controlled( headers[variation], 1, ParkAudio.CrowdVoiceParameter, 100, random ) );
				pitches.Add( ParkAudio.Controlled( headers[variation], 2, ParkAudio.CrowdVoiceParameter, 100, random ) );
			}

			CollectionAssert.AreEquivalent( new[] { 0, 1, 2, 3, 4, 5 }, pitches.ToList(), $"variation {variation + 1}'s pitch is drawn from 0 to 5" );

			if ( variation >= 6 )
				CollectionAssert.AreEquivalent( new[] { 14, 15, 16, 17, 18, 19, 20, 21 }, volumes.ToList(), $"twin {variation + 1}'s volume is drawn from 14 to 21" );
		}
	}

	/// <summary>
	/// <b>The guests round a cell</b> (<c>FUN_004c8d30( 1, cell, 4, 0 )</c>): those on the cells from four before
	/// to four after it each way, and nobody for cell nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the reach one short; one long; cell nought read as the map's first cell; staff counted.</remarks>
	[TestMethod]
	public void TheGuestsRoundACellAreThoseWithinFourCellsEachWay()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var people = new ParkPeople( new ParkWorld( new SaveReader( stream ).ReadFile() ), new ParkBalance( "jungle", easyMode: true ),
			random: new Random( 1 ), behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			static int Cell( int x, int y ) => (y * 128) + x + 1;

			int Stood( int x, int y, int reach = 4 )
				=> people.Peeps.Count( peep => Math.Abs( peep.Navigator.Position.Cell.X - x ) <= reach
					&& Math.Abs( peep.Navigator.Position.Cell.Y - y ) <= reach );

			// What the save's own guests give each cell asked about, then three more made on the stop's cell.
			var cells = new[] { (42, 5), (46, 9), (38, 1), (47, 5), (42, 10), (37, 5), (42, 0) };
			var before = cells.Select( cell => people.GuestsNear( Cell( cell.Item1, cell.Item2 ), 4 ) ).ToArray();

			for ( var i = 0; i < cells.Length; ++i )
				Assert.AreEqual( Stood( cells[i].Item1, cells[i].Item2 ), before[i], $"the save's guests round {cells[i]}" );

			Assert.AreEqual( 0, Stood( 42, 5, 0 ), "nobody of the save stands on the stop's cell" );

			for ( var i = 0; i < 3; ++i )
				Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

			Assert.AreEqual( 3, Stood( 42, 5, 0 ), "the three stand on the stop's cell" );

			var more = cells.Select( ( cell, i ) => people.GuestsNear( Cell( cell.Item1, cell.Item2 ), 4 ) - before[i] ).ToArray();

			CollectionAssert.AreEqual( new[] { 3, 3, 3, 0, 0, 0, 0 }, more,
				"counted on the cell, four away both ways and four the other way; not five away along a row or a column" );
			Assert.AreEqual( 3, people.GuestsNear( Cell( 42, 5 ), 0 ), "the reach is the caller's" );
			Assert.AreEqual( 0, people.GuestsNear( 0, 128 ), "no cell, nobody" );
			Assert.AreEqual( people.Peeps.Count, people.GuestsNear( Cell( 0, 0 ), 128 ), "every guest and no member of staff" );
			Assert.AreNotEqual( 0, people.Staff.Count, "the save has staff to leave out" );

			foreach ( var (x, y) in new[] { (50, 24), (52, 22), (48, 17), (0, 0), (127, 127) } )
				Assert.AreEqual( Stood( x, y ), people.GuestsNear( Cell( x, y ), 4 ), $"round ({x},{y})" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A held voice's later samples keep its volume and pitch</b> (<c>docs/exe/audio.md</c>, "A held voice's
	/// next sample"): the first sample of a voice takes its variation's, a later one what the voice had, on a twin
	/// as on any other, whatever the level and the generator.
	/// </summary>
	/// <remarks><b>Mutations:</b> a later sample drawn afresh; the first sample keeping what was there; the two swapped.</remarks>
	[TestMethod]
	public void AHeldVoicesLaterSampleKeepsItsVolumeAndPitch()
	{
		FileSystem = GameData.Required();

		var file = new SoundCategoryFile( "global/sound", "kids" );
		var headers = file.ReadVariations()[file.IndexOf( ParkAudio.CrowdVoiceEffect )];

		for ( var seed = 0; seed < 50; ++seed )
		{
			var random = new Random( seed );

			foreach ( var variation in new[] { 0, 6, 11 } )
			{
				Assert.AreEqual( (14, 3), ParkAudio.HeldSample( headers[variation], first: false, (14, 3), ParkAudio.CrowdVoiceParameter, 100, random ),
					$"variation {variation + 1}, a later sample" );
				Assert.AreEqual( (22, 0), ParkAudio.HeldSample( headers[variation], first: false, (22, 0), ParkAudio.CrowdVoiceParameter, 1, random ) );
			}

			var first = ParkAudio.HeldSample( headers[0], first: true, (99, 99), ParkAudio.CrowdVoiceParameter, 13, random );

			Assert.AreEqual( 15, first.Volume, "a voice's first sample: the first variation's volume at the level" );
			Assert.IsTrue( first.Pitch is >= 0 and <= 5, "and a drawn pitch" );
		}
	}

	/// <summary>
	/// <b>The crowd's voice, through <c>ParkAudio.OnUpdate</c> as the game runs it</b> (<c>docs/exe/audio.md</c>,
	/// "The crowd's voice"). On the music's beat the guests within four cells of the pointer's cell are its level.
	/// Above nought the voice is started, flat, with a sample of kids 91's first variation at the level's volume;
	/// as a sample ends the next is drawn from the variation the level's zone gives; a new level is the playing
	/// sample's volume at once; at nought, or in world state 4, the voice is faded and let go, and started again it
	/// begins from the first variation.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the level set off the beat; every guest counted, not those near; the pointer's cell not
	/// read; the voice started with no level; the volume not following a new level; the next sample never drawn; a
	/// sample drawn while one plays; the zone not followed; the voice not let go at nought; state 4 not handed in; a
	/// restart keeping its variation; the gain left at one; a sample left at its own pitch.
	/// </remarks>
	[TestMethod]
	public void TheCrowdsVoiceFollowsTheGuestsRoundThePointerOnTheBeat()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var levelBefore = Level.Current;
		var pointer = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var cellBefore = ParkPicking.Cell;
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), random: new Random( 1 ),
			behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			var samples = SamplesOf( ParkAudio.CrowdVoiceEffect );

			InAPark( "jungle", park =>
			{
				Voice? Crowd()
				{
					lock ( Audio.Lock )
						return Audio.Voices.LastOrDefault( voice => voice.Bus == AudioBus.Effects && samples.Any( set => set.Contains( voice.Name ) ) );
				}

				void ToTheBeat()
				{
					var beat = (GameClock.Ticks | 0x1f) + 1;

					while ( GameClock.Ticks < beat )
					{
						Frame( held: false, GameClock.TickSeconds );
						ParkLoop.Run( null, null, null, audio: park );
						park.Update();
					}
				}

				// The channel's number: the volume times the effects' 75 of a hundred, a half to the even, beside the music's 60.
				float Gain( int volume ) => (float)Math.Round( volume * 0.75, MidpointRounding.ToEven ) / 60f * 0.33f;

				// No cell under the pointer: nobody is counted, whoever is in the park.
				pointer.SetValue( null, 0 );
				park.Update();
				ToTheBeat();
				Assert.AreEqual( (0, false, 0), (park.CrowdVoiceLevelNow, park.CrowdVoiceHeld, park.CrowdVoicePlays.Starts), "no cell, no voice" );
				Assert.IsNull( Crowd() );

				// Thirteen at the stop, the pointer four cells off both ways: thirteen of the park's twenty-six.
				for ( var i = 0; i < 13; ++i )
					Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

				Assert.AreEqual( 13, people.GuestsNear( (1 * 128) + 38 + 1, 4 ), "none of the save's guests stands within four of (38,1)" );
				pointer.SetValue( null, (1 * 128) + 38 + 1 );
				Frame( held: false, GameClock.TickSeconds );
				park.Update();
				Assert.AreEqual( (0, false), (park.CrowdVoiceLevelNow, park.CrowdVoiceHeld), "not before the beat" );

				ToTheBeat();
				Assert.AreEqual( (13, 13, (1 * 128) + 38 + 1), (park.CrowdVoiceLevelNow, park.CrowdVoiceGuests, park.CrowdVoiceCell) );
				Assert.AreEqual( (true, 1, 1, 0), (park.CrowdVoiceHeld, park.CrowdVoicePlays.Starts, park.CrowdVoicePlays.Samples, park.CrowdVoiceVariation),
					"started on the beat, its first sample the first variation's" );

				var first = Crowd()!;

				Assert.IsTrue( samples[0].Contains( first.Name ) );
				Assert.IsFalse( first.IsPlaced, "flat, as the original plays it at (0,0,0)" );
				Assert.AreEqual( Gain( 15 ), first.Volume, 0.00001f, "level 13 is volume 15" );
				Assert.AreEqual( 11f / 60f * 0.33f, park.CrowdVoiceVolume, 0.00001f, "11 of 127 beside the music's 60, as the census reads it" );
				Assert.AreEqual( park.CrowdVoiceSample.Pitch == 0 ? 1.0 : Math.Pow( 2, (park.CrowdVoiceSample.Pitch + 1) / 96.0 ), first.Rate, 0.00001, "at its drawn pitch" );

				Frame( held: false, GameClock.TickSeconds );
				park.Update();
				Assert.AreSame( first, Crowd(), "while a sample plays no other is started" );

				// As each ends the next is drawn: at level 13 the first variation again or its twin, the seventh,
				// and each plays at the voice's own volume and pitch, the twin's too.
				var seen = new HashSet<int>();
				var pitches = new HashSet<int>();
				var kept = park.CrowdVoiceSample;
				var pickBefore = park.CrowdVoiceLastSample;
				var stepped = 0;

				for ( var i = 0; i < 200; ++i )
				{
					var last = Crowd()!;

					last.Stop();
					park.Update();

					var next = Crowd()!;

					Assert.AreNotSame( last, next, "the sample ended, the next is started" );
					Assert.IsTrue( park.CrowdVoiceVariation is 0 or 6, $"variation {park.CrowdVoiceVariation + 1} at level 13" );
					Assert.IsTrue( samples[park.CrowdVoiceVariation].Contains( next.Name ) );
					Assert.AreEqual( Gain( park.CrowdVoiceSample.Volume ), next.Volume, 0.00001f );
					Assert.AreEqual( park.CrowdVoiceSample.Pitch == 0 ? 1.0 : Math.Pow( 2, (park.CrowdVoiceSample.Pitch + 1) / 96.0 ), next.Rate, 0.00001 );
					Assert.AreEqual( kept, park.CrowdVoiceSample, $"variation {park.CrowdVoiceVariation + 1} keeps the voice's volume and pitch" );
					Assert.AreEqual( Gain( 15 ), next.Volume, 0.00001f );
					seen.Add( park.CrowdVoiceVariation );

					// Each later sample asks the draw for its step against the voice's last pick (0x006bde33), whichever
					// variation it came from: where its list holds more than two samples, it is never the same index again.
					if ( samples[park.CrowdVoiceVariation].Count > 2 )
					{
						Assert.AreNotEqual( pickBefore, park.CrowdVoiceLastSample, $"draw {i + 1}: the sample before is not drawn again" );
						++stepped;
					}

					pickBefore = park.CrowdVoiceLastSample;
				}

				Assert.IsTrue( stepped > 150, $"the crowd's lists hold more than two samples ({stepped} of 200 draws)" );
				Assert.AreEqual( 2, seen.Count, "both the first and its twin in two hundred draws" );
				Assert.AreEqual( 201, park.CrowdVoicePlays.Samples );

				// A beat's set draws the pitch again on a sample of the first variation, and leaves a twin's alone.
				for ( var beat = 0; beat < 80; ++beat )
				{
					var before = park.CrowdVoiceSample;
					var sounding = Crowd()!;

					ToTheBeat();
					Assert.AreSame( sounding, Crowd(), "a beat starts no sample" );
					Assert.AreEqual( 15, park.CrowdVoiceSample.Volume );
					Assert.AreEqual( park.CrowdVoiceSample.Pitch == 0 ? 1.0 : Math.Pow( 2, (park.CrowdVoiceSample.Pitch + 1) / 96.0 ), sounding.Rate, 0.00001 );

					if ( park.CrowdVoiceVariation == 6 )
						Assert.AreEqual( before, park.CrowdVoiceSample, "the twin names no parameter: the set applies nothing" );
					else
						pitches.Add( park.CrowdVoiceSample.Pitch );

					sounding.Stop();
					park.Update();
				}

				CollectionAssert.AreEquivalent( new[] { 0, 1, 2, 3, 4, 5 }, pitches.ToList(), "every pitch from 0 to 5, drawn on the beats" );

				// A sample of the first variation playing, then the crowd grows past a hundred: its volume at once.
				while ( park.CrowdVoiceVariation != 0 )
				{
					Crowd()!.Stop();
					park.Update();
				}

				var playing = Crowd()!;

				while ( people.Peeps.Count < 13 + 13 + 110 )
					Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

				ToTheBeat();
				Assert.AreEqual( (100, 123), (park.CrowdVoiceLevelNow, park.CrowdVoiceGuests), "held to a hundred" );
				Assert.AreSame( playing, Crowd(), "no new sample for a new level" );
				Assert.AreEqual( Gain( 22 ), playing.Volume, 0.00001f, "the playing sample takes volume 22 at once" );

				playing.Stop();
				park.Update();
				Assert.AreEqual( 5, park.CrowdVoiceVariation, "the next from the sixth variation, whose zone holds 100" );
				Assert.IsTrue( samples[5].Contains( Crowd()!.Name ) );
				Assert.AreEqual( Gain( 22 ), Crowd()!.Volume, 0.00001f );

				// World state 4: nought, the voice faded and let go.
				people.State.SetWorldState( ParkState.EndedWorldState );

				var last6 = Crowd()!;
				var starts = park.CrowdVoicePlays;

				ToTheBeat();
				Assert.AreEqual( (0, 123, false), (park.CrowdVoiceLevelNow, park.CrowdVoiceGuests, park.CrowdVoiceHeld), "world state 4: nought" );
				Assert.IsTrue( last6.Ending, "its sample fades out" );

				last6.Stop();
				Frame( held: false, GameClock.TickSeconds );
				park.Update();
				Assert.AreEqual( starts, park.CrowdVoicePlays, "and nothing follows it" );

				// Out of state 4 it is started again, from the first variation whatever the level.
				people.State.SetWorldState( 0 );
				ToTheBeat();
				Assert.AreEqual( (100, true, 0), (park.CrowdVoiceLevelNow, park.CrowdVoiceHeld, park.CrowdVoiceVariation) );
				Assert.AreEqual( (starts.Samples + 1, starts.Starts + 1), park.CrowdVoicePlays );
				Assert.AreEqual( Gain( 22 ), Crowd()!.Volume, 0.00001f );

				// The pointer moved off them: nought on the next beat.
				pointer.SetValue( null, (100 * 128) + 100 + 1 );
				ToTheBeat();
				Assert.AreEqual( (0, 0, false), (park.CrowdVoiceLevelNow, park.CrowdVoiceGuests, park.CrowdVoiceHeld), "far from everybody, nobody" );
			} );
		}
		finally
		{
			pointer.SetValue( null, cellBefore );
			Level.Current = levelBefore!;
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// Runs <paramref name="run"/> against a park's audio as if there were a device, then takes back every voice the
	/// park played and leaves the clock as a scene change leaves it.
	/// </summary>
	/// <remarks>
	/// A device is stood in by setting <see cref="Audio.Ready"/>, as <see cref="VoicePlacementTests"/> does, and the
	/// park's audio is built after it, so it loads the kids' category.
	/// </remarks>
	private static void InAPark( string theme, Action<ParkAudio> run )
	{
		var ready = typeof( Audio ).GetProperty( nameof( Audio.Ready ) )!;

		Assert.IsFalse( Audio.Ready, "a test run has no audio device, so nothing here should find one" );

		List<Voice> before;

		lock ( Audio.Lock )
			before = [.. Audio.Voices];

		ready.SetValue( null, true );

		ParkAudio? park = null;
		var listener = typeof( Audio ).GetField( "_listener", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )!;
		var heardFrom = listener.GetValue( null );

		try
		{
			// Heard from beside the ride, so its chain is in range (ParkScreams.InRange).
			Audio.SetListener( Here, Vector3.Right );
			park = new ParkAudio( theme );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( held: false, seconds: 0f );

			run( park );
		}
		finally
		{
			park?.Delete();
			Entity.ApplyDeletions();

			lock ( Audio.Lock )
				Audio.Voices.RemoveAll( voice => !before.Contains( voice ) );

			Audio.HoldPlaced( false );
			listener.SetValue( null, heardFrom );
			ready.SetValue( null, false );
			GameClock.Rebase();
		}
	}

	/// <summary>A whole .RSE file of one <c>SCREAMLEVEL</c> and an <c>END</c>, to the layout <see cref="RideScriptFile"/> reads.</summary>
	private static RideScriptFile ScreamLevelScript( int level )
	{
		using var memory = new System.IO.MemoryStream();
		using var writer = new System.IO.BinaryWriter( memory );
		int[] body = [unchecked( (int)(0x80000000u | (uint)Opcode.SCREAMLEVEL) ), level, unchecked( (int)(0x80000000u | (uint)Opcode.END) )];

		writer.Write( System.Text.Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 0 );          // variable count
		writer.Write( 8 );          // stack size
		writer.Write( 50 );         // time slice
		writer.Write( 0 );          // limbo records
		writer.Write( 0 );          // bounce records
		writer.Write( 0 );          // walk records
		writer.Write( System.Text.Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );          // string blob length
		writer.Flush();

		return new RideScriptFile( new System.IO.MemoryStream( memory.ToArray() ) );
	}

	/// <summary>
	/// <b>A script's three scream readings take its own speed word</b> (Q155, <c>0x00555ed0</c>, <c>0x00555fb8</c>, <c>0x00555fea</c>):
	/// <c>STARTSCREAM</c> starts its chain under the word and <c>SCREAMLEVEL</c> moves level and word together; <c>SINGLESCREAM</c> at level
	/// 35 plays grid step 1 at the loader's 50 and step 2 at 70, a different effect with samples of its own.
	/// </summary>
	/// <remarks><b>Mutations:</b> each of the three handlers' calls given no word; the chain not keeping it; <c>ScreamLevel</c> keeping the old one.</remarks>
	[TestMethod]
	public void AScriptsThreeScreamReadingsTakeItsOwnSpeedWord() => InAPark( "jungle", park =>
	{
		var scheduler = new RideScriptScheduler();
		var script = new RideScript( ScreamLevelScript( 25 ) ) { Speed = 60 };

		scheduler.Add( 5, script );
		script.StartScreamAt( 5, 20, Here );

		var chain = park.HeldScream( 5 )!;

		Assert.AreEqual( (20, 60), (chain.Level, chain.Speed), "STARTSCREAM" );

		script.Speed = 90;

		for ( var tick = 0; tick < 9 && script.Running; ++tick )
			scheduler.Advance( tick * 31f );

		Assert.AreEqual( (25, 90), (chain.Level, chain.Speed), "SCREAMLEVEL moved the level and the word together" );

		HashSet<string> Names( int effect ) => SamplesOf( effect ).SelectMany( set => set ).ToHashSet();

		Voice Last()
		{
			lock ( Audio.Lock )
				return Audio.Voices[^1];
		}

		var played = new List<string>();

		foreach ( var speed in new[] { 50, 70 } )
		{
			script.Speed = speed;
			Time.Now += 30f;
			script.SingleScreamAt( 1, 35, Here );

			var effect = ParkAudio.SingleScreamEffectFor( 1, 35, speed );

			Assert.IsTrue( Names( effect ).Contains( Last().Name ), $"a word of {speed}: effect {effect}'s sample, not '{Last().Name}'" );
			played.Add( Last().Name );
		}

		Assert.AreNotEqual( ParkAudio.SingleScreamEffectFor( 1, 35, 50 ), ParkAudio.SingleScreamEffectFor( 1, 35, 70 ), "two different effects, or this proves nothing" );
		Assert.AreEqual( 2, played.Count );
	} );

	/// <summary>One frame through both clocks, held or not, as a level runs one.</summary>
	private static void Frame( bool held, float seconds = 1f / 60f )
	{
		Time.Update( seconds );
		GameClock.Update( paused: held, GameClock.ParkCatchUp );
	}

	/// <summary>The names of each variation's samples of kids' effect <paramref name="effect"/>, as its category lists them.</summary>
	private static List<HashSet<string>> SamplesOf( int effect )
	{
		var file = new SoundCategoryFile( "global/sound", "kids" );
		var banks = file.Banks.Select( bank => SoundBank.Load( "global", bank ) ).ToList();
		var lists = file.ReadSamples( banks.Select( bank => (IReadOnlyList<TimeSpan>)(bank?.Durations ?? []) ).ToList() );

		return lists[file.IndexOf( effect )]
			.Select( variation => variation.Select( sample => banks[sample.Bank]![sample.Index]!.Name ).ToHashSet() )
			.ToList();
	}

	/// <summary>
	/// <b>A park makes each child of a held scream on the first frame its time has passed, a park's menu or not; one due
	/// while the world is held is made with no voice</b>: <see cref="ParkAudio"/>'s update pumps the chains the tests
	/// above pump by hand. Two rides scream, the second starting later, and each keeps its own clock; each first wait
	/// counts from the call. Through a pause the original's service ticks its chains on wall time and its mixer plays
	/// their children at nought (<c>docs/exe/audio.md</c>, "The pause substitution"), so none is held here to start with
	/// the rest on resume, and after it the next child comes at its own time.
	/// </summary>
	/// <remarks>
	/// The second row is a theme with no music to load, whose update returns early and must pump first. The park's
	/// chains draw their waits from an unseeded generator, so each due time is read off its chain rather than
	/// predicted. The hold lasts 200 frames, longer than effect 71's longest wait of 3 s, so a due time always
	/// passes inside it. <b>Mutations:</b> taking the pump out of the update, pumping behind the music's guard, not
	/// pumping while the world is held, giving a child due under the hold a voice (born held, it would start with the
	/// rest on resume), pumping on another clock, stepping only the first chain or stopping at the first not due, and
	/// starting a chain's clock anywhere but now, each fail an assertion here. A frame on a child's due millisecond empties
	/// the list and the next frame makes the child (<see cref="AChildDueOnTheMillisecondEmptiesTheListForAPass"/>). Not pinned: whether the pump comes before
	/// or after the hold in the update, which no listener could tell apart, and the debug console's own pause, which the
	/// update ignores on purpose.
	/// </remarks>
	[TestMethod]
	[DataRow( "jungle" )]
	[DataRow( "nomusic" )]
	public void AParkMakesEachChildWhenItsTimeHasPassedUnheardWhileHeld( string theme )
	{
		const int SecondFrom = 40, HoldFrom = 300, HoldTo = 500, Frames = 900;

		InAPark( theme, park =>
		{
			var chains = new List<(int Script, ParkScreams.Chain Chain)>();
			var madeWhileHeld = new HashSet<int>();

			void Start( int script, Vector3 at )
			{
				Assert.IsTrue( park.Scream( script, 1, 20, at ), $"script {script}: band 1 screams, and the kids' category loaded" );

				var chain = park.HeldScream( script );

				Assert.IsNotNull( chain, $"script {script} holds the scream it started" );
				Assert.AreEqual( 1, chain!.Plays, "its first child at once" );
				Assert.AreEqual( Time.Now + (chain.Gap / 1000f), chain.NextAt, 0.0001f, "and its first wait counted from now" );

				chains.Add( (script, chain) );
			}

			Start( 13, Here );

			for ( var frame = 0; frame < Frames; ++frame )
			{
				if ( frame == SecondFrom )
					Start( 43, Here + new Vector3( 40f, 0f, 0f ) );

				var held = frame is >= HoldFrom and < HoldTo;
				var before = chains.Select( entry => (entry.Chain.NextAt, entry.Chain.Plays, entry.Chain.Unheard, entry.Chain.ListEmpty) ).ToList();
				List<Voice> voices;

				lock ( Audio.Lock )
					voices = [.. Audio.Voices];

				Frame( held );
				park.Update();

				if ( held )
				{
					lock ( Audio.Lock )
						Assert.AreEqual( 0, Audio.Voices.Count( voice => voice.IsPlaced && !voices.Contains( voice ) ),
							$"frame {frame} started a placed voice while the world was held" );
				}

				for ( var i = 0; i < chains.Count; ++i )
				{
					var (script, chain) = chains[i];
					var (due, plays, unheard, wasEmpty) = before[i];
					var (nowMs, dueMs) = (ParkAudio.Milliseconds( Time.Now ), ParkAudio.Milliseconds( due ));

					if ( chain.Plays == plays )
					{
						Assert.IsFalse( wasEmpty, $"script {script}, frame {frame}: its list was empty, and it made no child" );
						Assert.IsTrue( nowMs <= dueMs,
							$"script {script}, frame {frame} at {nowMs} ms is past the child due at {dueMs} ms, and made none" );
						Assert.AreEqual( nowMs == dueMs, chain.ListEmpty, $"script {script}, frame {frame}: the list empties on the due millisecond alone" );
						continue;
					}

					Assert.IsTrue( wasEmpty || nowMs > dueMs, $"script {script}, frame {frame} at {nowMs} ms made a child due at {dueMs} ms" );
					Assert.AreEqual( plays + 1, chain.Plays, "one child at a time" );

					if ( held )
					{
						Assert.AreEqual( unheard + 1, chain.Unheard, $"script {script}, frame {frame}: the child made under the hold is unheard" );
						Assert.IsNull( chain.Voice, "and has no voice" );
						madeWhileHeld.Add( script );
					}
					else
					{
						Assert.AreEqual( unheard, chain.Unheard );
						Assert.IsFalse( chain.Voice?.IsHeld ?? true, $"script {script}, frame {frame}: a child after the hold is heard" );
					}
				}
			}

			foreach ( var (script, chain) in chains )
			{
				Assert.IsTrue( madeWhileHeld.Contains( script ), $"script {script}: a child fell due inside the hold, or the hold proves nothing" );
				Assert.IsTrue( chain.Plays >= 5, $"script {script}: fifteen seconds with waits of one to three made {chain.Plays}" );
			}
		} );
	}

	/// <summary>Answers each roll it is given, in turn, whatever the bound.</summary>
	private sealed class Rolls( params long[] rolls ) : Random
	{
		private int _next;

		public override long NextInt64( long maxValue ) => rolls[_next++];
	}

	/// <summary>
	/// <b>A chain's draw takes the first zone whose running total of weights is at least the roll</b> (<c>0x006c3f12</c>
	/// <c>add</c>, <c>0x006c3f14</c> <c>cmp</c>, <c>jae</c>): between a zone to variation 2 of weight 3 and one to 3 of
	/// weight 5, the roll 3 is 2's and 4 is 3's; and a first zone of weight nought takes the roll nought. Taking a zone
	/// only once the total is past the roll gives 3 and 2.
	/// </summary>
	[TestMethod]
	public void AChainsDrawTakesTheFirstZoneWhoseTotalReachesTheRoll()
	{
		var variations = new SoundCategoryFile.Variation[]
		{
			new( 1, 0, 0, 0, 1, [new( 1, 0, 100 ), new( 2, 0, 100 )] ),
			new( 1, 0, 0, 0, 3, [new( 3, 0, 100 ), new( 1, 0, 100 )] ),
			new( 1, 0, 0, 0, 5, [] ),
			new( 1, 0, 0, 0, 0, [] ),
		};

		Assert.AreEqual( 1, ParkScreams.NextVariation( variations, 0, 0, new Rolls( 0 ) ) );
		Assert.AreEqual( 1, ParkScreams.NextVariation( variations, 0, 0, new Rolls( 3 ) ), "the total 3 reaches the roll 3" );
		Assert.AreEqual( 2, ParkScreams.NextVariation( variations, 0, 0, new Rolls( 4 ) ) );
		Assert.AreEqual( 2, ParkScreams.NextVariation( variations, 0, 0, new Rolls( 7 ) ) );
		Assert.AreEqual( 3, ParkScreams.NextVariation( variations, 1, 0, new Rolls( 0 ) ), "a first zone of weight nought takes the roll nought" );
		Assert.AreEqual( 1, ParkScreams.NextVariation( variations, 1, 0, new Rolls( 1 ) ) );
	}

	/// <summary>
	/// <b>Each child of a held scream is a new one-shot voice, placed at the ride, at its variation's own volume, playing a
	/// sample of the variation the chain chose</b>, and a newer child leaves the one before it to play out. A level
	/// moved mid-scream carries to every later child. A stop cuts the newest child and makes no more, and so does
	/// the park's end.
	/// </summary>
	/// <remarks>
	/// Level 90 is volume 70 and sends every child after the first to variation 3; level 20 is volume 35 and
	/// variation 2. The variations' sample lists are read from the category and must not overlap, or naming the
	/// variation by its sample would prove nothing. <b>Mutations:</b> a looped child, a child placed anywhere but the
	/// ride, at the script's level or at a flat volume, from another variation or the effect's own pick, a
	/// child that stops or fades the one before, a level that does not carry, a stop that leaves the chain or the
	/// newest child running, a park that ends without cutting it, a band that screams at nought, and a census that
	/// does not count the children, each fail an assertion here. Not pinned: the
	/// balance between the ears a child starts at, which only the mixer reads.
	/// </remarks>
	[TestMethod]
	public void EachChildIsAPlacedOneShotOfItsVariationAtTheChainsLevel()
	{
		var samples = SamplesOf( 0x47 );

		Assert.AreEqual( 4, samples.Count, "effect 71 has four variations" );

		for ( var a = 0; a < samples.Count; ++a )
			for ( var b = a + 1; b < samples.Count; ++b )
				Assert.IsFalse( samples[a].Overlaps( samples[b] ), $"variations {a + 1} and {b + 1} share a sample" );

		InAPark( "jungle", park =>
		{
			Assert.IsFalse( park.Scream( 14, 0, 20, Here ), "band nought screams not at all" );
			Assert.IsNull( park.HeldScream( 14 ), "and holds nothing" );

			Assert.IsTrue( park.Scream( 13, 1, 90, Here ) );

			var chain = park.HeldScream( 13 )!;
			var children = new List<Voice>();
			var level = 90;

			void IsTheNewestChild()
			{
				var child = chain.Voice;

				Assert.IsNotNull( child, "every child sounds" );
				Assert.IsFalse( children.Contains( child! ), "a child is a new voice" );
				Assert.IsFalse( child!.Loop, "a child is a one-shot, which is what makes the next one a new sample" );
				Assert.AreEqual( AudioBus.Effects, child.Bus );
				Assert.AreEqual( Here, child.Place, "placed at the ride" );
				Assert.AreEqual( 35f / 60f * 0.33f, child.Volume, 0.0001f, $"at its variation's 47, 35 of 127, at level {level}" );
				Assert.IsTrue( samples[chain.Variation].Contains( child.Name ),
					$"'{child.Name}' is not a sample of variation {chain.Variation + 1}" );

				if ( children.Count > 0 )
					Assert.IsTrue( children[^1].Playing && !children[^1].Ending, "the child before is left to play out" );

				children.Add( child );
			}

			IsTheNewestChild();

			Assert.AreEqual( 0, chain.Variation, "the first child takes the first variation" );

			for ( var frame = 0; frame < 900; ++frame )
			{
				// Not on a pass whose list is pruned empty, whose child's voice is let go (Q1178).
				if ( frame >= 450 && level == 90 && !chain.ListEmpty )
				{
					level = 20;

					var playing = chain.Voice!.Volume;

					Assert.IsTrue( park.ScreamLevel( 13, level ) );
					Assert.AreEqual( playing, chain.Voice!.Volume, "no scream's variation names the parameter on its volume" );
				}

				var plays = chain.Plays;

				Frame( held: false );
				park.Update();

				if ( chain.Plays != plays )
					IsTheNewestChild();
			}

			Assert.IsTrue( children.Count >= 6, $"fifteen seconds with waits of one to three made {children.Count}" );
			Assert.AreEqual( 20, chain.Level, "the moved level is the chain's" );
			Assert.AreEqual( 1, chain.Variation, "and a child after it takes level 20's variation" );
			Assert.AreEqual( children.Select( child => child.Name ).Distinct().Count(), park.ScreamSamplesHeard,
				"the census counts every sample the children played" );

			// A stop on a pass that pruned the list empty cuts nothing (AStopOnTheEmptyPassCutsNoChild): stop on a pass that did not.
			while ( chain.ListEmpty )
			{
				var plays = chain.Plays;

				Frame( held: false );
				park.Update();

				if ( chain.Plays != plays )
					IsTheNewestChild();
			}

			var newest = children[^1];
			var before = children[^2];

			Assert.IsTrue( park.StopScream( 13 ), "the script was holding a scream" );
			Assert.IsNull( park.HeldScream( 13 ), "and holds none now" );
			Assert.IsFalse( newest.Playing, "a stop cuts the newest child" );
			Assert.IsTrue( before.Playing && !before.Ending, "and leaves the one before it to play out" );

			int voices;

			lock ( Audio.Lock )
				voices = Audio.Voices.Count;

			for ( var frame = 0; frame < 300; ++frame )
			{
				Frame( held: false );
				park.Update();
			}

			lock ( Audio.Lock )
				Assert.AreEqual( voices, Audio.Voices.Count, "a stopped scream makes no more children" );

			Assert.IsTrue( park.Scream( 13, 1, 90, Here ), "and the script may start again" );

			var last = park.HeldScream( 13 )!.Voice!;

			park.Delete();
			Entity.ApplyDeletions();

			Assert.IsFalse( last.Playing, "the park's end cuts the newest child of every scream" );
		} );
	}

	/// <summary>
	/// <b>Each child of a held scream is given a pitch of its own</b> (<c>docs/exe/audio.md</c>, "A scream's child
	/// and its pitch"): a draw between its variation's two pitch bytes, 0 to 5 on every shipped scream, made again
	/// for every child and set on its voice as the rate the pitch stands for. The volume is as it was.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no rate set on the child; a pitch never drawn; the volume's bytes read for it; one drawn a
	/// chain and kept; the rate of another number than the pitch.
	/// </remarks>
	[TestMethod]
	public void EachChildOfAScreamDrawsItsOwnPitch()
	{
		var file = new SoundCategoryFile( "global/sound", "kids" );

		foreach ( var effect in new[] { 0x47, 0x48, 0x49, 0x4a } )
			foreach ( var header in file.ReadVariations()[file.IndexOf( effect )] )
				Assert.AreEqual( (0, 6), header.Pitch, $"effect {effect}'s pitch bytes" );

		InAPark( "jungle", park =>
		{
			// Band 8 is effect 74, whose children are at most half a second apart.
			Assert.IsTrue( park.Scream( 13, 8, 90, Here ) );

			var chain = park.HeldScream( 13 )!;
			var pitches = new List<int>();

			Assert.AreEqual( 0x4a, chain.Effect );

			void IsPitched()
			{
				Assert.IsTrue( chain.Pitch is >= 0 and <= 5, $"a pitch of {chain.Pitch}" );
				pitches.Add( chain.Pitch );

				// A child drawn a wait of nought is pruned in the pass that makes it, its voice let go (Q1178).
				if ( chain.Voice is null )
				{
					Assert.IsTrue( chain.Gap == 0 && chain.ListEmpty, $"a child with no voice had a wait of {chain.Gap} ms" );
					return;
				}

				Assert.AreEqual( ParkCarSounds.Rate( chain.Pitch ), (float)chain.Voice!.Rate, 0.000001f, $"the rate of pitch {chain.Pitch}" );
				Assert.AreEqual( 35f / 60f * 0.33f, chain.Voice.Volume, 0.0001f, "at the volume it had" );
			}

			IsPitched();

			for ( var frame = 0; frame < 3600; ++frame )
			{
				var plays = chain.Plays;

				Frame( held: false );
				park.Update();

				if ( chain.Plays != plays )
					IsPitched();
			}

			Assert.IsTrue( pitches.Count >= 100, $"a minute with waits under half a second made {pitches.Count}" );

			for ( var pitch = 0; pitch <= 5; ++pitch )
				Assert.IsTrue( pitches.Contains( pitch ), $"no child of {pitches.Count} drew pitch {pitch}" );

			Assert.AreEqual( ParkCarSounds.Rate( 5 ), MathF.Pow( 2f, 6f / 96f ), 0.000001f, "a pitch of 5 is six 96ths of an octave up" );
		} );
	}

	/// <summary>
	/// <b>A one-shot of the kids' draws its pitch as it starts</b> (<c>docs/exe/audio.md</c>, "A one-shot's
	/// pitch"): from its variation's low pitch byte up to, not including, the high one, set on its voice as the
	/// rate the pitch stands for. A guest put off draws -5 to 4, a single scream of effects 75 to 90 -8 to 7, and
	/// one whose two bytes are alike (effect 105, a yawn) plays at its sample's own rate. Nothing is counted.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no rate handed to the category; the category sets none; the pitch never drawn; the
	/// volume's bytes read for it; the high byte drawn too; the rate of the pitch turned over; drawn once and kept;
	/// drawn twice, one for the line and one for the rate; a yawn or effect 105 handed no rate.
	/// </remarks>
	[TestMethod]
	public void AOneShotOfTheKidsDrawsItsPitch()
	{
		var file = new SoundCategoryFile( "global/sound", "kids" );

		Assert.AreEqual( (-5, 5), file.ReadVariations()[file.IndexOf( 0x80 )].Single().Pitch, "a guest put off" );
		Assert.AreEqual( (0, 0), file.ReadVariations()[file.IndexOf( 0x69 )].Single().Pitch, "effect 105" );

		foreach ( var header in file.ReadVariations()[file.IndexOf( 0x7e )] )
			Assert.AreEqual( (0, 0), header.Pitch, "a yawn" );

		for ( var effect = 0x4b; effect <= 0x5a; ++effect )
			Assert.AreEqual( (-8, 8), file.ReadVariations()[file.IndexOf( effect )].Single().Pitch, $"effect {effect}" );

		InAPark( "jungle", park =>
		{
			Voice Last()
			{
				lock ( Audio.Lock )
					return Audio.Voices[^1];
			}

			HashSet<int> Drawn( Func<bool> play, int low, int high, float volume )
			{
				var seen = new HashSet<int>();

				for ( var turn = 0; turn < 400; ++turn )
				{
					// Past the repeat delay a single scream waits out here.
					Time.Now += 30f;
					Assert.IsTrue( play(), $"turn {turn}" );

					var voice = Last();

					Assert.IsTrue( park.OneShotPitch >= low && park.OneShotPitch < high, $"a pitch of {park.OneShotPitch}" );
					Assert.AreEqual( ParkCarSounds.Rate( park.OneShotPitch ), (float)voice.Rate, 0.000001f, $"the rate of pitch {park.OneShotPitch}" );
					Assert.AreEqual( volume, voice.Volume, 1e-6f, "at the volume it had" );

					seen.Add( park.OneShotPitch );
					voice.Stop();
				}

				return seen;
			}

			var putOff = Drawn( () => park.PutOff( Here ), -5, 5, 10f / 60f * 0.33f );

			for ( var pitch = -5; pitch <= 4; ++pitch )
				Assert.IsTrue( putOff.Contains( pitch ), $"no put-off of 400 drew pitch {pitch}" );

			var scream = Drawn( () => park.SingleScream( 1, 90, Here ), -8, 8, 35f / 60f * 0.33f );

			for ( var pitch = -8; pitch <= 7; ++pitch )
				Assert.IsTrue( scream.Contains( pitch ), $"no single scream of 400 drew pitch {pitch}" );

			// Each of the two with nothing to draw is asked after a pitch that is not nought, which it must not keep.
			bool After( Func<bool> play )
			{
				while ( park.OneShotPitch == 0 )
					Assert.IsTrue( park.PutOff( Here ) );

				return play();
			}

			Assert.AreEqual( 1, Drawn( () => After( () => park.SingleScream( 1, -1, Here ) ), 0, 1, 35f / 60f * 0.33f ).Count, "effect 105" );
			Assert.AreEqual( 1, Drawn( () => After( () => park.Yawn( Here ) ), 0, 1, 35f / 60f * 0.33f ).Count, "a yawn" );

			Assert.AreEqual( MathF.Pow( 2f, -6f / 96f ), ParkCarSounds.Rate( -5 ), 0.000001f, "a pitch of -5 is six 96ths of an octave down" );
			Assert.AreEqual( 1f, ParkCarSounds.Rate( 0 ) );
			Assert.AreEqual( 0, Unimplemented.Summary.Where( gap => gap.What == "KIDS_ONE_SHOT_PITCH" ).Sum( gap => gap.Times ) );
		} );
	}

	/// <summary>
	/// <b>Thunder plays at its variation's own volume and draws its pitch</b> (<c>docs/exe/audio.md</c>, "A
	/// channel's volume" and "A one-shot's pitch"): the global ambient's effect 32 holds 85 and 85, which is 64 of
	/// 127 beside the music's 60, and pitch bytes -4 and 4, so a draw of -4 to 3, over a range of 1500.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the old flat 0.40; the volume through no channel's rounding; no rate handed over; the
	/// high pitch byte drawn too; another effect's bytes read.
	/// </remarks>
	[TestMethod]
	public void ThunderPlaysAtItsVariationsVolumeAndDrawsItsPitch()
	{
		var file = new SoundCategoryFile( "global/sound", "ambient" );
		var header = file.ReadVariations()[file.IndexOf( 32 )].Single();

		Assert.AreEqual( (85, 85), header.Volume );
		Assert.AreEqual( (-4, 4), header.Pitch );
		Assert.AreEqual( 1500f, header.Range );

		InAPark( "jungle", park =>
		{
			var seen = new HashSet<int>();

			for ( var turn = 0; turn < 400; ++turn )
			{
				// Past the repeat delay thunder waits out here.
				Time.Now += 30f;
				Assert.IsTrue( park.Thunder( Here + new Vector3( 0f, 0f, 300f ) ), $"turn {turn}" );

				Voice voice;

				lock ( Audio.Lock )
					voice = Audio.Voices[^1];

				Assert.AreEqual( 64f / 60f * 0.33f, voice.Volume, 1e-6f, "85 is 64 of 127 beside the music's 60" );
				Assert.AreEqual( 1500f, voice.Range );
				Assert.IsTrue( park.OneShotPitch >= -4 && park.OneShotPitch < 4, $"a pitch of {park.OneShotPitch}" );
				Assert.AreEqual( ParkCarSounds.Rate( park.OneShotPitch ), (float)voice.Rate, 0.000001f, $"the rate of pitch {park.OneShotPitch}" );

				seen.Add( park.OneShotPitch );
				voice.Stop();
			}

			for ( var pitch = -4; pitch <= 3; ++pitch )
				Assert.IsTrue( seen.Contains( pitch ), $"no thunder of 400 drew pitch {pitch}" );
		} );
	}

	/// <summary>
	/// <b>A second start over a held scream is refused, and the first screams on held by nothing</b>: the refusal's
	/// nought goes over the script's handle (<c>0x00555ee6</c>). The script holds no scream after it, its stop finds
	/// none, the chain let go goes on making children through a later start and stop, and only the park's end cuts it.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a refusal that keeps the first scream held, one that stops it, a chain let go that the pump
	/// skips, a park's end that leaves it running, and a script answering from anything but the audio's own hold,
	/// each fail an assertion here.
	/// </remarks>
	[TestMethod]
	public void ASecondStartLetsTheFirstScreamGoOnHeldByNothing()
	{
		using var rse = FileSystem.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( rse ) );

		new RideScriptScheduler().Add( 13, script );

		InAPark( "jungle", park =>
		{
			void Run( int frames )
			{
				for ( var frame = 0; frame < frames; ++frame )
				{
					Frame( held: false );
					park.Update();
				}
			}

			Assert.IsTrue( park.Scream( 13, 1, 20, Here ) );

			var first = park.HeldScream( 13 )!;

			Assert.IsTrue( script.Screaming, "the script holds the scream it started" );
			Assert.AreEqual( 0, park.ScreamsLetGo );

			Assert.IsFalse( park.Scream( 13, 2, 20, Here ), "a script holding a scream cannot start another" );
			Assert.IsNull( park.HeldScream( 13 ), "and holds none once it has tried" );
			Assert.IsFalse( script.Screaming, "which the script answers too" );
			Assert.AreEqual( 1, park.ScreamsLetGo, "the first is let go" );
			Assert.IsTrue( first.Voice is { Playing: true, Ending: false }, "and its newest child plays on" );

			Assert.IsFalse( park.StopScream( 13 ), "so the script's stop finds nothing" );
			Assert.IsTrue( first.Voice is { Playing: true, Ending: false }, "and cuts nothing" );

			var plays = first.Plays;

			Run( 300 );

			Assert.IsTrue( first.Plays > plays, $"five seconds with waits of one to three made {first.Plays - plays}" );

			Assert.IsTrue( park.Scream( 13, 1, 20, Here ), "a start with nothing held holds a new scream" );
			Assert.AreNotSame( first, park.HeldScream( 13 ) );
			Assert.IsTrue( park.StopScream( 13 ), "which its stop reaches" );
			Assert.AreEqual( 1, park.ScreamsLetGo, "and the one let go is not reached" );

			plays = first.Plays;

			Run( 300 );

			Assert.IsTrue( first.Plays > plays, "it still makes children" );

			var last = first.Voice!;

			park.Delete();
			Entity.ApplyDeletions();

			Assert.IsFalse( last.Playing, "the park's end cuts it" );
			Assert.AreEqual( 0, park.ScreamsLetGo, "and forgets it" );
		} );
	}

	/// <summary>
	/// <b>A script that dies stops the scream it holds, and so does its child removed flat with it</b>: the engine's
	/// flat destructor stops what <c>+0xd0</c> holds (<c>FUN_00558500</c>), and the child's removal is that destructor
	/// alone. A scream the dying script's second start let go is not reached.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the stop left in the teardown of relations, where a child removed flat never reaches it, and
	/// a stop that also takes the chains let go, each fail an assertion here.
	/// </remarks>
	[TestMethod]
	public void ADyingScriptStopsItsScreamAndItsChildsButNotOneLetGo()
	{
		RideScript Bouncy()
		{
			using var rse = FileSystem.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );

			return new RideScript( new RideScriptFile( rse ) );
		}

		var scheduler = new RideScriptScheduler();
		var parent = Bouncy();
		var child = Bouncy();

		scheduler.Add( 13, parent );
		scheduler.Add( 14, child );
		parent.ChildId = 14;
		child.ParentId = 13;

		InAPark( "jungle", park =>
		{
			Assert.IsTrue( park.Scream( 13, 1, 20, Here ) );
			Assert.IsFalse( park.Scream( 13, 1, 20, Here ), "a second start lets the first go" );

			var letGo = park.HeldScream( 13 );

			Assert.IsNull( letGo );
			Assert.IsTrue( park.Scream( 13, 1, 20, Here ) );
			Assert.IsTrue( park.Scream( 14, 1, 20, Here ) );

			var held = park.HeldScream( 13 )!.Voice!;
			var childs = park.HeldScream( 14 )!.Voice!;

			Assert.IsTrue( scheduler.Destroy( 13 ) );

			Assert.IsFalse( parent.Screaming, "the dying script holds no scream" );
			Assert.IsFalse( held.Playing, "and the one it held is cut" );
			Assert.IsFalse( child.Screaming, "its child, removed flat, holds none either" );
			Assert.IsFalse( childs.Playing, "and the child's is cut" );
			Assert.AreEqual( 0, scheduler.Count, "both are gone" );
			Assert.AreEqual( 1, park.ScreamsLetGo, "the scream let go is not reached" );
		} );
	}

	/// <summary>
	/// <c>ParkAudio.OnDelete</c>, "the park is ending: what it has playing fades": the music's sample and the rain's
	/// two voices are told to end as the park is deleted, as the original stops every voice between scenes
	/// (<c>0x0051bcb0</c>, <see cref="Audio.StopAll"/>). None is ending before: the music's first sample is
	/// started by the park's first update, the rain's bed by a level above nought and the weather's own voice by
	/// a turn to rain, each on the pass after.
	/// </summary>
	[TestMethod]
	public void AParkThatEndsFadesItsMusicAndItsRain()
	{
		InAPark( "jungle", park =>
		{
			park.Update();

			Voice music;
			List<Voice> dry;

			lock ( Audio.Lock )
			{
				music = Audio.Voices.Last( voice => voice.Bus == AudioBus.Music );
				dry = [.. Audio.Voices];
			}

			park.StartOwnRain();
			park.SetRainLevel( 96 );

			lock ( Audio.Lock )
				Assert.AreEqual( 0, Audio.Voices.Count( voice => !dry.Contains( voice ) ), "a held voice's sample starts on the pass after" );

			park.Update();

			List<Voice> wet;

			lock ( Audio.Lock )
				wet = Audio.Voices.Where( voice => !dry.Contains( voice ) ).ToList();

			Assert.AreEqual( 2, wet.Count, "the rain is two voices: the bed and the weather's own" );

			foreach ( var rain in wet )
			{
				Assert.IsFalse( rain.Loop, "one sample after another, not a loop" );
				Assert.AreEqual( "RAIN.mp2", rain.Name );
				Assert.AreEqual( AudioBus.Effects, rain.Bus );
				Assert.IsNull( rain.Range, "flat, as the original plays it at (0,0,0)" );
				Assert.IsFalse( rain.Ending, "the rain falls" );
			}

			Assert.IsFalse( music.Ending, "the music plays while the park runs" );

			park.Delete();

			Assert.IsTrue( music.Ending, "a park that ends fades its music out" );
			Assert.IsTrue( wet.All( rain => rain.Ending ), "and both of its rain's voices" );
			Assert.IsFalse( park.RainBed.Held );
			Assert.IsFalse( park.RainOwn.Held );
		} );
	}

	/// <summary>
	/// The global ambient's effect 33, the rain (<c>docs/exe/weather.md</c>, "Two rain voices, and how loud each is"): four variations of
	/// volume 1 to 2, 3, 4 and 5, each naming parameter 8 on its volume, each with the same four zones, a quarter of
	/// the level a variation. So a level gives <c>level x (high - 1) / 100 + 1</c> on the variation current, and
	/// the channel's own number beside the music's 60 is 1, 2 or 3 of 127 below a level of a hundred.
	/// </summary>
	[TestMethod]
	public void TheRainsFourVariationsTakeTheirVolumeFromTheLevel()
	{
		GameData.Required();

		var file = new SoundCategoryFile( "global/sound", "ambient" );
		var headers = file.ReadVariations()[file.IndexOf( 33 )];
		var random = new Random( 1 );

		Assert.AreEqual( 8, ParkAudio.RainParameter );
		Assert.AreEqual( 4, headers.Count );

		for ( var variation = 0; variation < 4; ++variation )
		{
			Assert.AreEqual( (1, variation + 2), headers[variation].Volume, $"variation {variation + 1}'s volume bytes" );
			Assert.AreEqual( 8, headers[variation].FirstKey );
			Assert.IsTrue( ParkAudio.NamesOnVolume( headers[variation], ParkAudio.RainParameter ) );
			Assert.AreEqual( 1, headers[variation].Samples );

			for ( var level = 0; level <= 100; ++level )
			{
				Assert.AreEqual( (level * (variation + 1) / 100) + 1,
					ParkAudio.Controlled( headers[variation], 1, ParkAudio.RainParameter, level, random ), $"variation {variation + 1} at level {level}" );
				Assert.AreEqual( 1, ParkAudio.Controlled( headers[variation], 1, 7, level, random ), "another parameter leaves it at its low" );
				Assert.AreEqual( level == 0 ? 0 : (level - 1) / 25,
					ParkScreams.NextVariation( headers, variation, level, random ), $"the zone of level {level}" );
			}
		}

		// The channel's own number: 1, 2, 2, 3 and 4 of 127 beside the music's 60.
		Assert.AreEqual( 0.0055f, ParkAudio.ChannelGain( 1 ), 0.00001f );
		Assert.AreEqual( 0.0110f, ParkAudio.ChannelGain( 2 ), 0.00001f );
		Assert.AreEqual( 0.0110f, ParkAudio.ChannelGain( 3 ), 0.00001f );
		Assert.AreEqual( 0.0165f, ParkAudio.ChannelGain( 4 ), 0.00001f );
		Assert.AreEqual( 0.0220f, ParkAudio.ChannelGain( 5 ), 0.00001f );
	}

	/// <summary>
	/// <c>ParkAudio.SetRainLevel</c>, <c>FUN_0051e830</c>: the bed is held from the first level above nought, its
	/// first sample of the first variation whatever the level; each set gives the variation current its volume again
	/// at once; a sample that follows another is of the level's zone and plays at what the voice kept, until the
	/// next set; nought fades it and lets it go, and the next level starts it afresh.
	/// </summary>
	[TestMethod]
	public void TheRainsBedTakesItsVolumeAtASetFromTheVariationCurrent()
	{
		InAPark( "jungle", park =>
		{
			park.Update();

			List<Voice> dry;

			lock ( Audio.Lock )
				dry = [.. Audio.Voices];

			Voice Sample()
			{
				lock ( Audio.Lock )
					return Audio.Voices.Last( voice => !dry.Contains( voice ) && voice.Name == "RAIN.mp2" );
			}

			var bed = park.RainBed;

			park.SetRainLevel( 3 );

			Assert.IsTrue( bed.Held );
			Assert.AreEqual( -1, bed.Variation, "no sample yet" );

			park.Update();

			var first = Sample();

			Assert.AreEqual( (0, 1, 1), (bed.Variation, bed.Volume, bed.Samples), "the first variation, at level 3" );
			Assert.AreEqual( 0.0055f, first.Volume, 0.00001f, "1 of 127 beside the music's 60" );

			// The ramp's sets, to the most the drops reach: the first variation's 1 to 2 stays 1 below a hundred.
			foreach ( var level in new[] { 6, 10, 50, 96 } )
			{
				park.SetRainLevel( level );
				park.Update();

				Assert.AreEqual( (0, 1, 1, level), (bed.Variation, bed.Volume, bed.Samples, bed.Level) );
				Assert.AreSame( first, Sample(), "a set starts no sample" );
				Assert.AreEqual( 0.0055f, first.Volume, 0.00001f );
			}

			// The sample ends: the next is of the zone of 96, the fourth variation, at the volume the voice kept.
			first.Stop();
			park.Update();

			var second = Sample();

			Assert.AreNotSame( first, second );
			Assert.AreEqual( (3, 1, 2), (bed.Variation, bed.Volume, bed.Samples), "queued with its sample alone: the volume is the voice's" );
			Assert.AreEqual( 0.0055f, second.Volume, 0.00001f );

			// A set on the fourth: 93 x 4 / 100 + 1 is 4, 3 of 127; 50 gives 3, 2 of 127.
			park.SetRainLevel( 93 );
			Assert.AreEqual( 4, bed.Volume );
			Assert.AreEqual( 0.0165f, second.Volume, 0.00001f, "at once, on the playing sample" );

			park.SetRainLevel( 50 );
			Assert.AreEqual( 3, bed.Volume );
			Assert.AreEqual( 0.0110f, second.Volume, 0.00001f );

			// The next is of the zone of 50, the second variation, still at 3 until a set: 46 x 2 / 100 + 1 is 1.
			second.Stop();
			park.Update();

			var third = Sample();

			Assert.AreEqual( (1, 3, 3), (bed.Variation, bed.Volume, bed.Samples) );
			Assert.AreEqual( 0.0110f, third.Volume, 0.00001f );

			park.SetRainLevel( 46 );
			Assert.AreEqual( 1, bed.Volume );
			Assert.AreEqual( 0.0055f, third.Volume, 0.00001f );

			Assert.IsFalse( park.RainOwn.Held, "the level starts the bed alone" );

			// Nought lets it go; a level above 100 is 100, and the voice starts afresh from the first variation.
			park.SetRainLevel( 0 );

			Assert.IsFalse( bed.Held );
			Assert.IsTrue( third.Ending, "faded" );

			park.Update();
			Assert.AreSame( third, Sample(), "a voice let go starts no sample" );

			park.SetRainLevel( 250 );
			park.Update();

			Assert.AreEqual( (0, 2, 4, 100), (bed.Variation, bed.Volume, bed.Samples, bed.Level), "the first variation at a hundred is its high, 2" );
			Assert.AreEqual( 0.0110f, Sample().Volume, 0.00001f );
		} );
	}

	/// <summary>
	/// <c>ParkAudio.StartOwnRain</c>, <c>FUN_005131f0</c>: the weather thing's own rain voice is the same effect held
	/// beside the bed with no level ever set, so every sample is the first variation's at its low volume, 1, whatever
	/// the bed's level; it is let go by its own stop and not by the bed's.
	/// </summary>
	[TestMethod]
	public void TheWeathersOwnRainVoiceStaysOnTheFirstVariationAtOne()
	{
		InAPark( "jungle", park =>
		{
			park.Update();

			List<Voice> dry;

			lock ( Audio.Lock )
				dry = [.. Audio.Voices];

			Voice Sample()
			{
				lock ( Audio.Lock )
					return Audio.Voices.Last( voice => !dry.Contains( voice ) && voice.Name == "RAIN.mp2" );
			}

			var own = park.RainOwn;

			park.StartOwnRain();
			park.Update();

			Assert.IsTrue( own.Held );
			Assert.IsFalse( park.RainBed.Held, "the weather's own voice is not the bed" );

			for ( var sample = 1; sample <= 6; ++sample )
			{
				var voice = Sample();

				Assert.AreEqual( (0, 1, sample, 0), (own.Variation, own.Volume, own.Samples, own.Level) );
				Assert.AreEqual( 0.0055f, voice.Volume, 0.00001f );

				park.StartOwnRain();
				park.Update();
				Assert.AreSame( voice, Sample(), "a second start of a held voice starts nothing" );
				Assert.AreEqual( 0, own.Variation, "and leaves it on its variation" );

				voice.Stop();
				park.Update();
			}

			// The bed's level and its stop leave it alone: its next sample is still of the zone of nought.
			park.SetRainLevel( 96 );
			park.Update();
			Assert.AreEqual( (0, 1, 0), (own.Variation, own.Volume, own.Level) );

			own.Voice!.Stop();
			park.Update();
			Assert.AreEqual( (0, 1, 0, 8), (own.Variation, own.Volume, own.Level, own.Samples), "whatever the bed's level" );
			Assert.AreEqual( 0.0055f, own.Plays, 0.00001f );

			park.SetRainLevel( 0 );
			Assert.IsTrue( own.Held );
			Assert.AreEqual( 0.0055f, own.Plays, 0.00001f );

			var last = own.Voice!;

			park.StopOwnRain();

			Assert.IsFalse( own.Held );
			Assert.IsTrue( last.Ending );

			// Started again it begins afresh.
			park.StartOwnRain();
			Assert.AreEqual( -1, own.Variation, "no sample until the next pass" );

			park.Update();
			Assert.AreEqual( (0, 1, 9), (own.Variation, own.Volume, own.Samples) );
		} );
	}

	/// <summary>
	/// <c>ParkWeather.RainLevel</c>, the level the drops' ramp hands the rain's bed at each step (<c>0x00512ac5</c>):
	/// the drops as a whole hundredth of the most, cut not rounded, so 20 of 600 a step reads 3, 6, 10, and the 580
	/// the heaviest rain settles at is 96, never a hundred.
	/// </summary>
	[TestMethod]
	public void TheRainsLevelIsTheDropsAsAWholeHundredthOfTheMost()
	{
		CollectionAssert.AreEqual( new[] { 3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50, 53, 56, 60 },
			Enumerable.Range( 1, 18 ).Select( step => ParkWeather.RainLevel( step * 20, 600 ) ).ToArray() );

		Assert.AreEqual( 96, ParkWeather.RainLevel( 580, 600 ) );
		Assert.AreEqual( 13, ParkWeather.RainLevel( 80, 600 ) );
		Assert.AreEqual( 100, ParkWeather.RainLevel( 600, 600 ) );
		Assert.AreEqual( 0, ParkWeather.RainLevel( 0, 600 ) );
		Assert.AreEqual( 0, ParkWeather.RainLevel( 5, 600 ), "under a hundredth is nought, which lets the bed go" );
		Assert.AreEqual( 0, ParkWeather.RainLevel( 20, 0 ), "a balance with no drops has no rain" );
	}

	/// <summary>
	/// <b>The music's level and the crowd's voice count the guests the 32nd tick's sweep left</b>: the park loop's
	/// every-32nd block runs in that tick's own step, after its sweep (<c>0x0054f82d</c>..<c>0x0054f8c8</c>;
	/// <c>docs/exe/park-engine.md</c>, "The order within a step"). One frame of ticks 25 to 40, a hitch, sweeps on 32 and
	/// 40; the sweep of 32's tail admits five guests onto the path at the pointer's cell and the sweep of 40's twenty: the
	/// level and the crowd's count hold the five and not the twenty.
	/// </summary>
	/// <remarks><b>Mutations:</b> the block asked once after the frame's ticks (it counts the twenty); asked
	/// before the step's sweep (it counts the guests before the sweep of 32).</remarks>
	[TestMethod]
	public void AFramesThirtySecondTickCountsTheGuestsItsOwnSweepLeft()
	{
		var data = GameData.Required();
		FileSystem = data;

		// The clock from nought, so the frame is ticks 25 to 40 whatever ran before.
		using var clock = new SimulationClockScope();

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pointer = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var cellBefore = ParkPicking.Cell;
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), random: new Random( 1 ),
			behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			InAPark( "jungle", park =>
			{
				// The guests' cells and the stop's made path, so every guest counts (FUN_004fa990).
				var state = people.State;

				foreach ( var (x, y) in people.Peeps.Select( peep => peep.Navigator.Position.Cell ).Append( (42, 5) ).Distinct().ToList() )
					state.SetRecord( x, y, state.Record( x, y ) with { Type = 1 } );

				var stop = (5 * 128) + 42 + 1;
				pointer.SetValue( null, stop );

				while ( GameClock.Ticks < 24 )
					Frame( held: false, GameClock.TickSeconds );

				Assert.AreEqual( 24, GameClock.Ticks );
				Frame( held: false, 16 * GameClock.TickSeconds );
				Assert.AreEqual( (40, 16), (GameClock.Ticks, GameClock.TicksDue), "one frame of ticks 25 to 40" );

				var tails = 0;
				var (census, near) = (-1, -1);

				ParkLoop.Run( null, null, people, calendar: swept =>
				{
					// Five admitted in the sweep of 32's own tail, which the block follows; twenty in the sweep of 40's.
					var admitted = ++tails == 1 ? 5 : 20;

					for ( var i = 0; i < admitted; ++i )
						Assert.AreNotEqual( 0, swept.Admit( 42, 5 ), "the stop takes another" );

					if ( tails == 1 )
						(census, near) = (swept.GateGuestCensus, swept.GuestsNear( stop, ParkAudio.CrowdVoiceReach ));
				}, audio: park );

				var (censusAfter, nearAfter) = (people.GateGuestCensus, people.GuestsNear( stop, ParkAudio.CrowdVoiceReach ));
				Console.WriteLine( $"after the sweep of 32: {census} counted, {near} near the stop; after the frame: {censusAfter}, {nearAfter};"
					+ $" level {park.LevelNow}, sets {park.LevelSets}, crowd voice {park.CrowdVoiceGuests} guests at level {park.CrowdVoiceLevelNow}" );

				Assert.AreEqual( 2, tails, "sweeps on 32 and 40" );
				Assert.IsTrue( census >= 10 && near >= 5 && nearAfter >= near + 20, $"the guests counted: {census}, then {near} and {nearAfter} near the stop" );
				Assert.AreNotEqual( ParkAudio.MusicLevel( census, 0 ), ParkAudio.MusicLevel( censusAfter, 0 ), "the twenty move the level" );
				Assert.AreEqual( (ParkAudio.MusicLevel( census, 0 ), 1), (park.LevelNow, park.LevelSets), "the level set once, from the sweep of 32's count" );
				Assert.AreEqual( (near, ParkAudio.CrowdVoiceLevel( near, 0 ), stop), (park.CrowdVoiceGuests, park.CrowdVoiceLevelNow, park.CrowdVoiceCell),
					"the crowd's voice from the guests round the stop after the sweep of 32" );
			} );
		}
		finally
		{
			pointer.SetValue( null, cellBefore );
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <c>ParkAudio.OnDelete</c>, "the park is ending: what it has playing fades": the crowd's voice, started on the
	/// beat by thirteen guests within four cells of the pointer's cell (<c>docs/exe/audio.md</c>, "The crowd's
	/// voice"), has its playing sample told to end as the park is deleted, as the original stops every voice
	/// between scenes (<c>0x0051bcb0</c>, <see cref="Audio.StopAll"/>). The setting is
	/// <see cref="TheCrowdsVoiceFollowsTheGuestsRoundThePointerOnTheBeat"/>'s.
	/// </summary>
	[TestMethod]
	public void AParkThatEndsFadesTheCrowdsVoice()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pointer = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var cellBefore = ParkPicking.Cell;
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), random: new Random( 1 ),
			behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			var samples = SamplesOf( ParkAudio.CrowdVoiceEffect );

			InAPark( "jungle", park =>
			{
				for ( var i = 0; i < 13; ++i )
					Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

				pointer.SetValue( null, (1 * 128) + 38 + 1 );

				var beat = (GameClock.Ticks | 0x1f) + 1;

				while ( GameClock.Ticks < beat )
				{
					Frame( held: false, GameClock.TickSeconds );
					ParkLoop.Run( null, null, null, audio: park );
					park.Update();
				}

				Assert.AreEqual( (13, true, 1), (park.CrowdVoiceLevelNow, park.CrowdVoiceHeld, park.CrowdVoicePlays.Samples),
					"thirteen near the pointer: the voice held, its first sample playing" );

				Voice? crowd;

				lock ( Audio.Lock )
					crowd = Audio.Voices.LastOrDefault( voice => voice.Bus == AudioBus.Effects && samples.Any( set => set.Contains( voice.Name ) ) );

				Assert.IsNotNull( crowd );
				Assert.IsFalse( crowd!.Ending, "it sounds while the park runs" );

				park.Delete();

				Assert.IsTrue( crowd.Ending, "a park that ends fades the crowd's voice out" );
			} );
		}
		finally
		{
			pointer.SetValue( null, cellBefore );
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A new level reaches the playing sample of any of variations 1 to 6, not the first alone</b>
	/// (<c>docs/exe/audio.md</c>, "The crowd's voice": "As controller slot 1 of variations 1 to 6 it is the volume:
	/// level × (22 − 14) / 100 + 14 in whole numbers ... the set applies it to the playing sample at once"). Forty
	/// guests round the pointer are level 40, whose band, 32-47, is the third variation's: the voice's first sample is
	/// the first variation's, and the one after it the third's (the first's zones give 32-47 to variation 3 alone), at
	/// volume 40 × 8 / 100 + 14 = 17, which is 13 of 127 (17 × 0.75 = 12.75). A hundred guests on the next beat are
	/// level 100, and the same sample, still playing, takes 100 × 8 / 100 + 14 = 22, which is 16 of 127 (16.5 to the
	/// even), with no new sample started.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> the set applied only while the first variation plays (<c>CrowdVoiceVariation &gt; 0</c> for
	/// <c>&lt; 0</c> in <c>ParkAudio.SetCrowdVoice</c>).
	/// </remarks>
	[TestMethod]
	public void ANewLevelIsThePlayingSamplesVolumeAtOnceOnTheThirdVariationToo()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var levelBefore = Level.Current;
		var pointer = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var cellBefore = ParkPicking.Cell;
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), random: new Random( 1 ),
			behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			var samples = SamplesOf( ParkAudio.CrowdVoiceEffect );

			InAPark( "jungle", park =>
			{
				Voice? Crowd()
				{
					lock ( Audio.Lock )
						return Audio.Voices.LastOrDefault( voice => voice.Bus == AudioBus.Effects && samples.Any( set => set.Contains( voice.Name ) ) );
				}

				void ToTheBeat()
				{
					var beat = (GameClock.Ticks | 0x1f) + 1;

					while ( GameClock.Ticks < beat )
					{
						Frame( held: false, GameClock.TickSeconds );
						ParkLoop.Run( null, null, null, audio: park );
						park.Update();
					}
				}

				// Forty at the stop, the pointer four cells off both ways: level 40.
				for ( var i = 0; i < 40; ++i )
					Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

				Assert.AreEqual( 40, people.GuestsNear( (1 * 128) + 38 + 1, 4 ), "none of the save's guests stands within four of (38,1)" );
				pointer.SetValue( null, (1 * 128) + 38 + 1 );
				ToTheBeat();
				Assert.AreEqual( (40, true, 0), (park.CrowdVoiceLevelNow, park.CrowdVoiceHeld, park.CrowdVoiceVariation),
					"started on the beat, its first sample the first variation's" );

				// Off the beat, the first sample ends: the next is the third variation's, whose band holds 40.
				Frame( held: false, GameClock.TickSeconds );
				park.Update();
				Crowd()!.Stop();
				park.Update();
				Assert.AreEqual( 2, park.CrowdVoiceVariation, "32-47 is the third variation's band" );

				var playing = Crowd()!;

				Assert.IsTrue( samples[2].Contains( playing.Name ) );
				Assert.AreEqual( 17, park.CrowdVoiceSample.Volume, "40 x 8 / 100 + 14" );
				Assert.AreEqual( 13f / 60f * 0.33f, playing.Volume, 0.00001f, "volume 17 is 13 of 127 beside the music's 60" );

				// The crowd grows to a hundred while it plays: its volume on the beat, and no new sample.
				for ( var i = 0; i < 60; ++i )
					Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

				ToTheBeat();
				Assert.AreEqual( (100, 100), (park.CrowdVoiceLevelNow, park.CrowdVoiceGuests) );
				Assert.AreSame( playing, Crowd(), "no new sample for a new level" );
				Assert.AreEqual( 2, park.CrowdVoiceVariation, "and the third variation's still" );
				Assert.AreEqual( 22, park.CrowdVoiceSample.Volume, "100 x 8 / 100 + 14" );
				Assert.AreEqual( 16f / 60f * 0.33f, playing.Volume, 0.00001f, "the playing sample takes volume 22, 16 of 127, at once" );
			} );
		}
		finally
		{
			pointer.SetValue( null, cellBefore );
			Level.Current = levelBefore!;
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A packed cell in the map's last column is on its own row</b> (<c>FUN_004c8d30( 1, cell, reach, 0 )</c>,
	/// <see cref="ParkPeople.GuestsNear"/>'s doc comment; a packed cell is <c>y*128 + x + 1</c>). Three guests made
	/// on (42,5) are 85 columns from column 127. Asked from (127,90) with a reach of 85 they are 85 rows off, the
	/// square's last row, so all three are counted; asked from (127,91) they are 86 rows off, so none is.
	/// </summary>
	/// <remarks><b>Mutations:</b> the row taken from the packed cell without the one taken off, which puts column 127 a row down.</remarks>
	[TestMethod]
	public void TheGuestsRoundACellInTheLastColumnAreCountedFromItsOwnRow()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var people = new ParkPeople( new ParkWorld( new SaveReader( stream ).ReadFile() ), new ParkBalance( "jungle", easyMode: true ),
			random: new Random( 1 ), behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			static int Cell( int x, int y ) => (y * 128) + x + 1;

			// What the save's own guests give each cell, so that only the three made here are measured.
			var onTheEdge = people.GuestsNear( Cell( 127, 90 ), 85 );
			var pastIt = people.GuestsNear( Cell( 127, 91 ), 85 );

			for ( var i = 0; i < 3; ++i )
				Assert.AreNotEqual( 0, people.Admit( 42, 5 ) );

			Assert.AreEqual( 3, people.GuestsNear( Cell( 127, 90 ), 85 ) - onTheEdge,
				"85 columns and 85 rows from (127,90): on the square's corner, counted" );
			Assert.AreEqual( 0, people.GuestsNear( Cell( 127, 91 ), 85 ) - pastIt,
				"86 rows from (127,91): outside the square" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A guest is counted on the cell the park has them linked into</b>, not the one under their position:
	/// <c>FUN_004c8d30</c> walks the cells' own lists, and the count here reads each guest's linked cell
	/// (<c>docs/exe/audio.md</c>, "The crowd's voice", "Said"; <see cref="ParkState.CellOf"/>). One guest made on
	/// (42,5) and then linked into (42,15), their position left where it was, is one more on (42,15) and none
	/// more on (42,5).
	/// </summary>
	/// <remarks><b>Mutations:</b> the linked cell not read, every guest counted where their position is.</remarks>
	[TestMethod]
	public void AGuestIsCountedOnTheCellTheParkHasThemLinkedInto()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new System.IO.MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var people = new ParkPeople( new ParkWorld( new SaveReader( stream ).ReadFile() ), new ParkBalance( "jungle", easyMode: true ),
			random: new Random( 1 ), behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ), staffRandom: new Random( 1 ) );

		try
		{
			static int Cell( int x, int y ) => (y * 128) + x + 1;

			// What the save's own guests give each of the two cells, so that only the one made here is measured.
			var madeOn = people.GuestsNear( Cell( 42, 5 ), 0 );
			var linkedInto = people.GuestsNear( Cell( 42, 15 ), 0 );

			var id = people.Admit( 42, 5 );

			Assert.AreNotEqual( 0, id );
			Assert.AreEqual( 1, people.GuestsNear( Cell( 42, 5 ), 0 ) - madeOn, "made on (42,5) and linked into it" );

			people.State.StandOn( id, 42, 15 );

			Assert.AreEqual( (42, 5), people.Guests[id].Navigator.Position.Cell, "their position has not moved" );
			Assert.AreEqual( (42, 15), people.State.CellOf( id )!.Value, "the park has them on (42,15)" );
			Assert.AreEqual( 1, people.GuestsNear( Cell( 42, 15 ), 0 ) - linkedInto, "counted on the cell they are linked into" );
			Assert.AreEqual( 0, people.GuestsNear( Cell( 42, 5 ), 0 ) - madeOn, "and not on the one under their position" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
