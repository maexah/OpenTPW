using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// How a category divides its run of sample records up between the effects that pick from them.
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// This is worth pinning because the reader used to work it out by measuring the gap before each list
/// and calling anything past thirty-two bytes a new effect. That cannot work, and the numbers say why:
/// jungle's ambient runs sixty-four bytes between two lists of the <i>same</i> effect, while the
/// smallest gap between two <i>different</i> effects anywhere is forty-two. The ranges overlap, so no
/// threshold separates them.
/// </para>
/// <para>
/// It went unnoticed because the lobby is the one part of the game it happens to get right: all four
/// local sfx categories and all four music ones declare a single variation each, so they group the
/// same either way. Every park category did not - jungle's nine ambient effects came out as
/// twenty-six, which left effects 178 through 192 each playing one of effect 177's beasts.
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
		// reads nothing and passes for the wrong reason. Set to the same folder GameData mounts, and
		// nothing else in the suite uses it.
		FileSystem = GameData.Required();
	}

	private const string ParkSound = "levels/jungle/Sound";

	/// <summary>
	/// The count lives in the second int of each twenty-byte effect record, which this reader used to
	/// skip entirely. Jungle's ambient is the clearest case: nine effects declaring twenty-nine lists
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
	/// A park's music is a single effect that picks between six arrangements. Under the old gap rule
	/// those six came back as six separate effects, and the five after the first were dropped on the
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
	/// no lists at all, so it must play nothing. Before this it was handed one of effect 177's beasts,
	/// and so were 178, 179, 180, 181, 190, 191 and 192 - every park ambient effect played a beast.
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
	/// And the lobby is left exactly as it was. Its categories declare one variation per effect, which
	/// is why the old rule never gave a wrong answer there - and this says so rather than leaving it to
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
			"one list each, which is why the gap rule happened to work here" );
	}
}
