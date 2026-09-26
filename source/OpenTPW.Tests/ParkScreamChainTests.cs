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

		try
		{
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
			ready.SetValue( null, false );
			GameClock.Rebase();
		}
	}

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
	/// <b>A park makes each child of a held scream on the first frame its time has passed, and none while the
	/// world is held</b>: <see cref="ParkAudio"/>'s update pumps the chains the tests above pump by hand. Two rides
	/// scream, the second starting later, and each keeps its own clock; each first wait counts from the call.
	/// </summary>
	/// <remarks>
	/// The second row is a theme with no music to load, whose update returns early and must pump first. The park's
	/// chains draw their waits from an unseeded generator, so each due time is read off its chain rather than
	/// predicted. The hold lasts 200 frames, longer than effect 71's longest wait of 3 s, so a due time always
	/// passes inside it. <b>Mutations:</b> taking the pump out of the update, pumping behind the music's guard,
	/// pumping while the world is held or on another clock, stepping only the first chain or stopping at the first
	/// not due, and starting a chain's clock anywhere but now, each fail an assertion here. Not pinned: whether the
	/// pump comes before or after the hold in the update, which no listener could tell apart, and the debug
	/// console's own pause, which the update ignores on purpose.
	/// </remarks>
	[TestMethod]
	[DataRow( "jungle" )]
	[DataRow( "nomusic" )]
	public void AParkMakesEachChildWhenItsTimeHasPassedAndNoneWhileHeld( string theme )
	{
		const int SecondFrom = 40, HoldFrom = 300, HoldTo = 500, Frames = 900;

		InAPark( theme, park =>
		{
			var chains = new List<(int Script, ParkScreams.Chain Chain)>();
			var passedWhileHeld = new HashSet<int>();

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
				var before = chains.Select( entry => (entry.Chain.NextAt, entry.Chain.Plays) ).ToList();

				Frame( held );
				park.Update();

				for ( var i = 0; i < chains.Count; ++i )
				{
					var (script, chain) = chains[i];
					var (due, plays) = before[i];

					if ( chain.Plays == plays )
					{
						Assert.IsTrue( held || Time.Now <= due,
							$"script {script}, frame {frame} at {Time.Now:0.000} s is past the child due at {due:0.000} s, and made none" );

						if ( held && Time.Now > due )
							passedWhileHeld.Add( script );

						continue;
					}

					Assert.IsFalse( held, $"script {script}, frame {frame} made a child while the world was held" );
					Assert.IsTrue( Time.Now > due, $"script {script}, frame {frame} at {Time.Now:0.000} s made a child due at {due:0.000} s" );
					Assert.AreEqual( plays + 1, chain.Plays, "one child at a time" );
				}
			}

			foreach ( var (script, chain) in chains )
			{
				Assert.IsTrue( passedWhileHeld.Contains( script ), $"script {script}: a child fell due inside the hold, or the hold proves nothing" );
				Assert.IsTrue( chain.Plays >= 5, $"script {script}: fifteen seconds with waits of one to three made {chain.Plays}" );
			}
		} );
	}

	/// <summary>
	/// <b>Each child of a held scream is a new one-shot voice, placed at the ride, at the chain's level, playing a
	/// sample of the variation the chain chose</b>, and a newer child leaves the one before it to play out. A level
	/// moved mid-scream carries to every later child. A stop cuts the newest child and makes no more, and so does
	/// the park's end.
	/// </summary>
	/// <remarks>
	/// Level 90 is volume 70 and sends every child after the first to variation 3; level 20 is volume 35 and
	/// variation 2. The variations' sample lists are read from the category and must not overlap, or naming the
	/// variation by its sample would prove nothing. <b>Mutations:</b> a looped child, a child placed anywhere but the
	/// ride, at another level or at the one-shot's fixed level, from another variation or the effect's own pick, a
	/// child that stops or fades the one before, a level that does not carry, a stop that leaves the chain or the
	/// newest child running, a park that ends without cutting it, a band that screams at nought, a second start over
	/// a held scream, and a census that does not count the children, each fail an assertion here. Not pinned: the
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
				Assert.AreEqual( ParkAudio.ScreamVolume( level ) / 100f, child.Volume, 0.0001f, $"at level {level}'s volume" );
				Assert.IsTrue( samples[chain.Variation].Contains( child.Name ),
					$"'{child.Name}' is not a sample of variation {chain.Variation + 1}" );

				if ( children.Count > 0 )
					Assert.IsTrue( children[^1].Playing && !children[^1].Ending, "the child before is left to play out" );

				children.Add( child );
			}

			IsTheNewestChild();

			Assert.AreEqual( 0, chain.Variation, "the first child takes the first variation" );
			Assert.IsFalse( park.Scream( 13, 1, 20, Here ), "a script holding a scream cannot start another" );
			Assert.AreSame( chain, park.HeldScream( 13 ), "and keeps the one it holds" );

			for ( var frame = 0; frame < 900; ++frame )
			{
				if ( frame == 450 )
				{
					level = 20;

					Assert.IsTrue( park.ScreamLevel( 13, level ) );
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
}
