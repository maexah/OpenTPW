using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// How a category divides its run of sample records up between the effects that pick from them.
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// This is worth pinning because the obvious way to work it out - measuring the gap before each list
/// and calling anything past thirty-two bytes a new effect - cannot work, and the numbers say why:
/// jungle's ambient runs sixty-four bytes between two lists of the <i>same</i> effect, while the
/// smallest gap between two <i>different</i> effects anywhere is forty-two. The ranges overlap, so no
/// threshold separates them.
/// </para>
/// <para>
/// The lobby is the one part of the game a gap rule happens to get right: all four local sfx
/// categories and all four music ones declare a single variation each, so they group the same either
/// way. Not every park category does - under a gap rule jungle's nine ambient effects come out as
/// twenty-six, and effects 178 through 192 each play one of effect 177's beasts.
/// </para>
/// </summary>
[TestClass]
public class SoundCategoryTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		// SoundCategoryFile reads through the GLOBAL file system rather than an instance, because the
		// game has exactly one - so a test wanting the real .map files has to put one there, or it
		// reads nothing and passes for the wrong reason. Set to the same folder GameData mounts.
		FileSystem = GameData.Required();
	}

	private const string ParkSound = "levels/jungle/Sound";

	/// <summary>
	/// The count lives in the second int of each twenty-byte effect record. Jungle's ambient is the
	/// clearest case: nine effects declaring twenty-nine lists
	/// between them, which is exactly the number of lists that follow.
	/// </summary>
	[TestMethod]
	public void AnEffectRecordSaysHowManyListsItPicksBetween()
	{
		var category = new SoundCategoryFile( ParkSound, "ambient" );

		Assert.IsTrue( category.IsValid, "jungle's ambient category should read" );
		CollectionAssert.AreEqual(
			new[] { 177, 178, 179, 180, 181, 182, 191, 192, 190 },
			category.Effects.Select( effect => effect.Id ).ToArray(),
			"the effect ids, in the order the file lists them" );

		CollectionAssert.AreEqual(
			new[] { 9, 5, 7, 4, 1, 0, 1, 1, 1 },
			category.Effects.Select( effect => effect.Variations ).ToArray(),
			"how many lists each effect picks between" );

		Assert.AreEqual( 29, category.Effects.Sum( effect => effect.Variations ),
			"the lists those nine effects account for between them" );
	}

	/// <summary>
	/// A park's music is a single effect that picks between six arrangements. Under a gap rule those six
	/// come back as six separate effects, and the five after the first are dropped on the
	/// floor, because a category hands list <i>i</i> to effect <i>i</i> and there is only one effect.
	/// </summary>
	[TestMethod]
	public void TheParkMusicEffectPicksBetweenSixArrangements()
	{
		var music = new SoundCategoryFile( "levels/jungle/Music", "music" );

		Assert.IsTrue( music.IsValid, "jungle's music category should read" );
		Assert.AreEqual( 1, music.Effects.Count, "cat_music declares one effect" );
		Assert.AreEqual( 2, music.Effects[0].Id, "and its id is the 2 the original plays" );
		Assert.AreEqual( 6, music.Effects[0].Variations, "the arrangements it picks between" );
	}

	/// <summary>
	/// A declared zero is real, and it is the sharpest test there is: jungle's effect 182 picks between
	/// no lists at all, so it must play nothing. A gap rule hands it one of effect 177's beasts, and
	/// 178, 179, 180, 181, 190, 191 and 192 as well - under it every park ambient effect plays a beast.
	///
	/// <para>
	/// Asked through <see cref="SoundCategory.Length"/>, which is zero for an effect with no samples,
	/// rather than by widening the class's surface for a test.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnEffectThatPicksBetweenNoListsPlaysNothing()
	{
		var ambient = new SoundCategory( "levels/jungle", ParkSound, "ambient" );

		Assert.IsTrue( ambient.IsValid, "jungle's ambient category should load" );
		Assert.AreEqual( TimeSpan.Zero, ambient.Length( 182 ),
			"effect 182 declares no variations, so it has nothing to play" );
		Assert.AreNotEqual( TimeSpan.Zero, ambient.Length( 177 ),
			"effect 177 declares nine, so it certainly has something" );
	}

	/// <summary>
	/// The global rides' 212 is one sample of 1,442 ms, the length the original's one-shot held for the Jungle
	/// Spray (Q565's run). 203 is 395 ms, and 202 picks between three samples.
	/// </summary>
	[TestMethod]
	public void TheGlobalRidesPlayTheSamplesTheirRecordsName()
	{
		// 202's six records end at 65,532 (10922 ... 65532), so a roll of 65,533 or more gives no sample. The rolls are
		// the test's own, every record's odds and the one above the last, so no draw is left to chance.
		FixedRoll.During( 0, roll =>
		{
			var rides = new SoundCategory( "global", "global/sound", "rides" );

			Assert.IsTrue( rides.IsValid, "the global rides should load" );

			foreach ( var (effect, milliseconds) in new[] { (212, 1442), (211, 999), (203, 395) } )
			{
				Assert.IsNotNull( rides.PickSample( effect, out var variation, out var length ), $"effect {effect} should decode" );
				Assert.AreEqual( (0, milliseconds), (variation, (int)length.TotalMilliseconds), $"effect {effect}'s sample" );
			}

			var lengths = new System.Collections.Generic.HashSet<int>();

			foreach ( var odds in new[] { 0, 10922, 21844, 32766, 43688, 54610, 65532 } )
			{
				roll.Roll = odds;
				Assert.IsNotNull( rides.PickSample( 202, out _, out var length ), $"roll {odds} lands in a record" );
				lengths.Add( (int)length.TotalMilliseconds );
			}

			CollectionAssert.AreEquivalent( new[] { 1794, 1452, 944 }, lengths.ToArray(), "202's three samples, never 203's" );

			foreach ( var above in new[] { 65533, 65535 } )
			{
				roll.Roll = above;
				Assert.IsNull( rides.PickSample( 202, out _, out var length ), $"roll {above} is above the last odds: no sample" );
				Assert.AreEqual( TimeSpan.Zero, length );
			}
		} );
	}

	/// <summary>
	/// The lobby's music (effect 1 of <c>locallobbymusic</c>: one list of three samples, odds 21845, 43690, 65535) as a
	/// voice draws it (<c>FUN_006bc680</c>): the pick is kept on every draw of a list of two or more (<c>0x006bc747</c>),
	/// asked for the step or not, and the step is taken only where asked. The rolls are the test's own.
	/// </summary>
	[TestMethod]
	public void ADrawKeepsItsPickWhetherOrNotItAskedAndOnlyAnAskStepsOffIt()
	{
		FixedRoll.During( 0, roll =>
		{
			var music = new SoundCategory( "levels/jungle", ParkSound, "locallobbymusic" );

			Assert.IsTrue( music.IsValid, "jungle's lobby music should load" );

			int Draw( int at, bool ask )
			{
				roll.Roll = at;
				Assert.IsNotNull( music.PickSample( 1, out _, out var length, ask ), $"roll {at} lands in a sample" );

				return music.LastSampleOf( 1 );
			}

			Assert.AreEqual( -1, music.LastSampleOf( 1 ), "nothing drawn yet" );
			Assert.AreEqual( 0, Draw( 0, ask: false ), "a first draw asks for no step and still keeps its pick" );
			Assert.AreEqual( 0, Draw( 0, ask: false ), "and an unasked draw does not step off the pick kept" );
			Assert.AreEqual( 1, Draw( 0, ask: true ), "an asked draw that repeats the kept pick moves on to the next" );
			Assert.AreEqual( 0, Draw( 0, ask: true ), "and one that does not repeat it (0 after 1) stays where the roll put it" );
			Assert.AreEqual( 1, Draw( 0, ask: true ), "a repeat is read against the pick kept: 0 again moves on to 1" );
			Assert.AreEqual( 2, Draw( 30000, ask: true ), "and 1 again moves on to 2" );
			Assert.AreEqual( 0, Draw( 65535, ask: true ), "the last sample's repeat wraps to the first" );
			Assert.AreEqual( 2, Draw( 65535, ask: false ), "an unasked draw keeps its pick, which the next ask reads" );
			Assert.AreEqual( 0, Draw( 65535, ask: true ), "so asking now steps off the last" );
		} );
	}

	/// <summary>
	/// A park voice's samples (<c>PickFrom</c>) keep the pick in the voice's own byte (<c>+0x46</c>, <c>0x006bc747</c>), not
	/// the effect's: two voices of one effect step off their own last pick, the first draw asks for no step and still
	/// stores, and a list of one stores nothing. The rolls are the test's own.
	/// </summary>
	[TestMethod]
	public void AParkVoiceStepsOffItsOwnLastPickAndNoOtherVoices()
	{
		FixedRoll.During( 0, roll =>
		{
			var music = new SoundCategory( "levels/jungle", ParkSound, "locallobbymusic" );
			int voiceA = -1, voiceB = -1;

			Assert.IsNotNull( music.PickFrom( 1, 0, ref voiceA, avoidRepeat: false ) );
			Assert.AreEqual( 0, voiceA, "a first draw asks for no step and stores its pick" );
			Assert.IsNotNull( music.PickFrom( 1, 0, ref voiceA, avoidRepeat: true ) );
			Assert.AreEqual( 1, voiceA, "a later sample that rolls the pick before moves on to the next" );
			Assert.IsNotNull( music.PickFrom( 1, 0, ref voiceB, avoidRepeat: true ) );
			Assert.AreEqual( 0, voiceB, "another voice of the effect has no pick before, so the same roll stands" );
			Assert.AreEqual( -1, music.LastSampleOf( 1 ), "and the effect's own pick, the lobby's, was not touched" );

			roll.Roll = 65535;
			Assert.IsNotNull( music.PickFrom( 1, 0, ref voiceA, avoidRepeat: true ) );
			Assert.AreEqual( 2, voiceA, "a pick that is not the one before stands" );
			Assert.IsNotNull( music.PickFrom( 1, 0, ref voiceA, avoidRepeat: true ) );
			Assert.AreEqual( 0, voiceA, "and a repeat of the last sample wraps to the first" );

			var rides = new SoundCategory( "global", "global/sound", "rides" );
			var one = -1;

			Assert.IsNotNull( rides.PickFrom( 212, 0, ref one, avoidRepeat: true ) );
			Assert.AreEqual( -1, one, "a list of one returns before the store (0x006bc6ae)" );
		} );
	}

	/// <summary>
	/// The land's following voices (the jungle's sea and gulls, effects 3 and 2 of the global ambient) never reach the avoid-repeat
	/// step: no list they draw from holds more than two samples (effect 2 is one list of two, effect 3 three lists of one), so the
	/// ask they make for every sample after the first (<c>0x006bde33</c>) steps off nothing. Dead by CONTENT (<c>AGENTS.md</c> rule 3):
	/// this test is what says so, and fails if a bank change gives them a longer list.
	/// </summary>
	[TestMethod]
	public void TheLandsFollowingVoicesHoldNoListOfMoreThanTwoSamples()
	{
		var ambient = new SoundCategory( "global", "global/sound", "ambient" );

		Assert.IsTrue( ambient.IsValid, "the global ambient should load" );
		CollectionAssert.AreEqual( new[] { 2 }, ambient.VariationsOf( 2 ).Select( header => header.Samples ).ToArray() );
		CollectionAssert.AreEqual( new[] { 1, 1, 1 }, ambient.VariationsOf( 3 ).Select( header => header.Samples ).ToArray() );
	}

	/// <summary>A list of one is returned with no draw and no store (<c>0x006bc6ae</c>): the pick kept from before stays.</summary>
	[TestMethod]
	public void AListOfOneKeepsNoPick()
	{
		FixedRoll.During( 0, dice =>
		{
			var rides = new SoundCategory( "global", "global/sound", "rides" );

			Assert.IsNotNull( rides.PickSample( 212, out _, out _, avoidRepeat: true ) );
			Assert.AreEqual( -1, rides.LastSampleOf( 212 ), "212 is one sample of one list" );
			Assert.IsNotNull( rides.PickSample( 202, out _, out _ ) );
			Assert.AreEqual( 0, rides.LastSampleOf( 202 ), "and 202's six samples keep theirs" );
		} );
	}

	/// <summary>
	/// And the lobby's categories declare one variation per effect, which is why a gap rule gives no
	/// wrong answer there - and this says so rather than leaving it to
	/// be rediscovered the next time the lobby's sound changes for no apparent reason.
	/// </summary>
	[TestMethod]
	public void TheLobbysOwnCategoriesDeclareOneListEach()
	{
		var lobby = new SoundCategoryFile( ParkSound, "locallobbysfx" );

		Assert.IsTrue( lobby.IsValid, "jungle's lobby sfx category should read" );
		CollectionAssert.AreEqual(
			new[] { 1, 1, 1, 1 },
			lobby.Effects.Select( effect => effect.Variations ).ToArray(),
			"one list each, which is why a gap rule gives no wrong answer here" );
	}
}
