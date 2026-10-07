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
	/// <b>The park's music, through <c>ParkAudio.OnUpdate</c> as the game runs it</b> (<c>docs/exe/park-engine.md</c>,
	/// "The music's level"). The level is set only on a step whose count has its low five bits nought, from the
	/// guests the park holds then, held to 89, and nought while the world's state is 4. A sample is played at the
	/// music's one loudness whatever the level, and when it has ended the next comes from the variation whose zone
	/// holds the level: the first at level 6, the sixth at 89, the first again at nought, never silence.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the level set every frame; the crowd's level set without the hold and the state; the level
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
						park.Update();
					}
				}

				park.Update();
				Assert.AreEqual( (0, 0, 0), (park.LevelNow, park.LevelSets, park.VariationNow), "the first sample is the first variation's" );

				var first = Music();

				Assert.AreEqual( ParkAudio.MusicVolume, first.Volume, 0.0001f, "at the music's loudness with the level nought" );
				StringAssert.StartsWith( first.Name.ToLowerInvariant(), "level1" );

				ToTheBeat();
				Assert.AreEqual( (6, 1), (park.LevelNow, park.LevelSets), "thirteen guests: level 6, set on the beat" );

				ToTheBeat();
				Assert.AreEqual( 2, park.LevelSets, "and set again 32 ticks on, not on the frames between" );
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

				// They go again: the level falls with them.
				foreach ( var id in came )
					Assert.IsTrue( people.Depart( id ) );

				ToTheBeat();
				Assert.AreEqual( 6, park.LevelNow, "thirteen guests again: level 6" );

				// World state 4: the level is nought, and the music is the first variation's, not silence.
				var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

				typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, new InWorldStateFour( world ) );
				Level.Current = level;

				ToTheBeat();
				Assert.AreEqual( 0, park.LevelNow, "world state 4: nought" );

				loud.Stop();
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
						park.Update();
					}
				}

				float Gain( int volume ) => volume / 100f * 0.30f;

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
				Assert.AreEqual( park.CrowdVoiceSample.Pitch == 0 ? 1.0 : Math.Pow( 2, (park.CrowdVoiceSample.Pitch + 1) / 96.0 ), first.Rate, 0.00001, "at its drawn pitch" );

				Frame( held: false, GameClock.TickSeconds );
				park.Update();
				Assert.AreSame( first, Crowd(), "while a sample plays no other is started" );

				// As each ends the next is drawn: at level 13 the first variation again or its twin, the seventh.
				var seen = new HashSet<int>();
				var pitches = new HashSet<int>();

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
					pitches.Add( park.CrowdVoiceSample.Pitch );
					Assert.IsTrue( park.CrowdVoiceVariation == 0 ? park.CrowdVoiceSample.Volume == 15 : park.CrowdVoiceSample.Volume is >= 14 and <= 21 );
					seen.Add( park.CrowdVoiceVariation );
				}

				Assert.AreEqual( 2, seen.Count, "both the first and its twin in two hundred draws" );
				Assert.AreEqual( 201, park.CrowdVoicePlays.Samples );
				CollectionAssert.AreEquivalent( new[] { 0, 1, 2, 3, 4, 5 }, pitches.ToList(), "every pitch from 0 to 5" );

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
				var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

				typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, new InWorldStateFour( world ) );
				Level.Current = level;

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
				Level.Current = levelBefore!;
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

	/// <summary>Lost Kingdom's save with its world state read as 4, and nothing else changed.</summary>
	private sealed class InWorldStateFour( ParkWorld source ) : IParkInitialState
	{
		public ParkWorld? Save => source;
		public IReadOnlyList<ParkThingIdentity> Things => source.Things;
		public IReadOnlyList<ParkWorld.CatalogueObject> Objects => source.Objects;
		public ParkWorld.EconomyState? Economy => source.Economy;
		public ParkWorld.StaffHqState? StaffHq => source.StaffHq;
		public IReadOnlyList<ParkWorld.Person> People => source.People;
		public IReadOnlyList<ParkWorld.Sprite> Sprites => source.Sprites;
		public IReadOnlyList<ParkWorld.MapCell> Cells => source.Cells;
		public ParkWorld.MapCell CellAt( int x, int y ) => source.CellAt( x, y );
		public int ParkGates => source.ParkGates;
		public int TrafficLights => source.TrafficLights;
		public int FirstObject => source.FirstObject;
		public int RandomSeed => source.RandomSeed;
		public int Weather => source.Weather;
		public int ParkClosed => source.ParkClosed;
		public int GameTick => source.GameTick;
		public int BankAccount => source.BankAccount;
		public int NumberOfVisitorsToDate => source.NumberOfVisitorsToDate;
		public int WorldState => ParkAudio.SilentWorldState;
		public int ThingCount => source.ThingCount;
		public int ArrivalVehicleForSmallCrowd => source.ArrivalVehicleForSmallCrowd;
		public int ArrivalVehicleForMediumCrowd => source.ArrivalVehicleForMediumCrowd;
		public int ArrivalVehicleForLargeCrowd => source.ArrivalVehicleForLargeCrowd;
		public int CurrentArrivalVehicle => source.CurrentArrivalVehicle;
		public ParkWorld.ArrivalBlock Arrival => source.Arrival;
		public IReadOnlyList<ParkWorld.ObjectControl> ObjectControlRecords => source.ObjectControlRecords;
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
				Assert.AreEqual( ParkAudio.ScreamVolume( level ) / 100f, child.Volume, 0.0001f, $"at level {level}'s volume" );
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
}
