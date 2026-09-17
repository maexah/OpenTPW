using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The number behind the management gadget's happiness gauge - <see cref="ParkPeople.AverageHappiness"/>,
/// which reproduces the original's <c>FUN_004c7bb0</c>.
///
/// <para>
/// <b>The middle test is the load-bearing one, and that is deliberate.</b> Every guest in the shipped park
/// is saved at exactly fifty, so a test that only read the shipped park would pass against almost any
/// arithmetic - a float mean, a rounding mean, or a hard-coded fifty would all satisfy it. That is the
/// shape of assertion this project has been caught by more than once: one that would pass with the feature
/// deleted. So the guests are moved to a total whose true mean is 50.53, where <b>truncating and rounding
/// disagree</b>, and the answer must step down. The original sums into an integer and finishes with
/// <c>FILD</c>/<c>FIDIV</c> - an integer division, which rounds towards nought.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkHappinessGaugeTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// What the gauge reads in the park the game ships.
	///
	/// <para>
	/// <b>The guest count is asserted first, and it is the anti-vacuity guard.</b> A walk that produced no
	/// guests at all would make the average nought, and nothing in the reading itself would say so.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheShippedParkReadsFiftyBecauseEveryGuestWasSavedAtFifty()
	{
		var people = new ParkPeople( Park() );

		Assert.AreEqual( 13, people.Peeps.Count, "the shipped park's guests" );
		Assert.IsTrue( people.Peeps.All( peep => peep.Happiness == 50f ),
			"and every one of them is saved at fifty" );

		Assert.AreEqual( 50, people.AverageHappiness(), "so the gauge reads fifty" );
	}

	/// <summary>
	/// <b>The one that can actually fail.</b> Twelve guests at fifty and one at fifty-seven is 657 over 13,
	/// which is 50.53 - so truncation gives fifty and rounding gives fifty-one, and only the first is what
	/// the original does. The second half then moves every guest together, so that a reading stuck at
	/// whatever the file said would fail too.
	/// </summary>
	[TestMethod]
	public void TheMeanIsWholeAndRoundsTowardsNought()
	{
		var people = new ParkPeople( Park() );
		var guests = people.Peeps;

		foreach ( var peep in guests )
			peep.Happiness = 50f;

		guests[0].Happiness = 57f;

		Assert.AreEqual( 50, people.AverageHappiness(),
			"657 over thirteen is 50.53, which steps down to fifty rather than rounding to fifty-one" );

		foreach ( var peep in guests )
			peep.Happiness = 74f;

		Assert.AreEqual( 74, people.AverageHappiness(),
			"and the gauge follows the guests rather than staying where the file left it" );
	}

	/// <summary>
	/// A park with nobody in it reads nought - the same value the original hands back when its walk counts
	/// no one, and when the park is shut: <c>DAT_0070031c</c>, which is <c>0.0f</c> in the image.
	/// </summary>
	/// <remarks>
	/// <b>This one would pass with the feature deleted</b>, which is why it is third and not first: it
	/// pins the empty case rather than the arithmetic, and the test above is what proves anything is
	/// computed at all.
	/// </remarks>
	[TestMethod]
	public void NobodyInTheParkReadsNought()
	{
		var people = new ParkPeople( null );

		Assert.AreEqual( 0, people.Peeps.Count, "a null park has no guests to average" );
		Assert.AreEqual( 0, people.AverageHappiness(), "so the gauge rests at the bottom" );
	}
}
