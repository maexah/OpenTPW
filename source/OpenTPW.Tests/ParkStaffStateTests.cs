using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The block the park's five staff carry, which nothing read until now.
///
/// <para>
/// <b>The reconciliation these turn on.</b> A staff record is <c>8 + 390 + 105</c> = 503 bytes plus what
/// its kind adds, and the record-size table - derived years apart from this, by running the original's own
/// reader under emulation - says 511, 513, 509, 511 and 509 for the five models. Those leave 8, 10, 6, 8
/// and 6, and each kind's serialiser declares exactly that many bytes of extras. So an offset that is
/// wrong anywhere in the shared block shows up as a value out of range here rather than as a plausible
/// number, which is what these assert.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkStaffStateTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkWorld.Person[] Staff() =>
		[.. World().People.Where( person => person.Model != 1 ).OrderBy( person => person.ThingId )];

	/// <summary>
	/// Exactly one of the two blocks is set on every person, decided by their model - the two occupy the
	/// same bytes, so a person carrying both or neither would mean the branch is wrong.
	/// </summary>
	[TestMethod]
	public void EveryPersonCarriesTheBlockTheirModelSaysAndNotTheOther()
	{
		foreach ( var person in World().People )
		{
			Assert.AreEqual( person.Model == 1, person.Guest != null,
				$"thing {person.ThingId} is model {person.Model}, so it should "
				+ (person.Model == 1 ? "carry" : "have no") + " guest block" );

			Assert.AreEqual( person.Model != 1, person.Staff != null,
				$"thing {person.ThingId} is model {person.Model}, so it should "
				+ (person.Model != 1 ? "carry" : "have no") + " staff block" );
		}
	}

	/// <summary>Lost Kingdom ships one of each kind of staff, which is what makes five.</summary>
	[TestMethod]
	public void TheParkShipsOneOfEachOfTheFiveKinds()
	{
		var models = Staff().Select( person => person.Model ).OrderBy( model => model ).ToArray();

		CollectionAssert.AreEqual( new[] { 4, 5, 6, 7, 8 }, models,
			"the five staff models, one each - mechanic, handyman, entertainer, guard, researcher" );
	}

	/// <summary>
	/// Every pay grade indexes the balance file's <c>PerGradeStaffConsts[0..4]</c>, so one outside that
	/// range would index off the end of the constants the behaviour reads.
	/// </summary>
	[TestMethod]
	public void EveryStaffMemberIsAtARealPayGrade()
	{
		foreach ( var person in Staff() )
		{
			var staff = person.Staff!.Value;

			Assert.IsTrue( staff.PayGrade is >= 0 and < ParkWorld.StaffState.PayGrades,
				$"staff {person.ThingId} has pay grade {staff.PayGrade}, "
				+ $"outside the {ParkWorld.StaffState.PayGrades} the balance file describes" );

			Assert.IsTrue( staff.PercentageThroughGrade is >= 0 and <= 100,
				$"staff {person.ThingId} is {staff.PercentageThroughGrade}% through their grade" );
		}
	}

	/// <summary>
	/// Mood and tiredness are percentages the original clamps to 0..100 everywhere it touches them, so a
	/// value outside that band means the offset is wrong rather than that the staff member is unusual.
	/// </summary>
	[TestMethod]
	public void TheirMoodAndTirednessAreRealPercentages()
	{
		foreach ( var person in Staff() )
		{
			var staff = person.Staff!.Value;

			Assert.IsTrue( staff.Happiness is >= 0f and <= 100f,
				$"staff {person.ThingId} has happiness {staff.Happiness}" );

			Assert.IsTrue( staff.Tiredness is >= 0f and <= 100f,
				$"staff {person.ThingId} has tiredness {staff.Tiredness}" );
		}
	}

	/// <summary>
	/// A patrol area is two packed cell ids - <c>y * 128 + 1 + x</c> - naming the bottom-left and top-right
	/// corners of a rectangle. Nought is "no area", which is the whole map; anything else has to unpack to
	/// a corner pair the right way round, or the random-destination roll inside it would have an empty
	/// range to pick from.
	/// </summary>
	[TestMethod]
	public void EveryPatrolRectangleIsEitherAbsentOrTheRightWayRound()
	{
		foreach ( var person in Staff() )
		{
			var staff = person.Staff!.Value;

			if ( staff.PatrolBottomLeft == 0 && staff.PatrolTopRight == 0 )
				continue;

			// The original unpacks by subtracting one and splitting on the low seven bits, which is what
			// makes this a cell id rather than an index.
			var (leftX, leftY) = ((staff.PatrolBottomLeft - 1) & 0x7f, (staff.PatrolBottomLeft - 1) >> 7);
			var (rightX, rightY) = ((staff.PatrolTopRight - 1) & 0x7f, (staff.PatrolTopRight - 1) >> 7);

			Assert.IsTrue( leftX <= rightX && leftY <= rightY,
				$"staff {person.ThingId} patrols from ({leftX},{leftY}) to ({rightX},{rightY}), "
				+ "which is not a rectangle the roll inside it could pick from" );

			Assert.IsTrue( rightY < ParkWorld.MapSize,
				$"staff {person.ThingId} patrols to row {rightY}, off a {ParkWorld.MapSize}-cell map" );
		}
	}

	/// <summary>
	/// <b>The three-numbering trap, asserted rather than trusted.</b> A thing model, a sprite folder and the
	/// balance file's <c>PerTypeStaffConsts</c> each number the five kinds differently, and crossing any two
	/// would read the wrong constants while still looking entirely plausible - the mechanic is model 4, wears
	/// sprite type 6, and is paid as type 1.
	/// </summary>
	[TestMethod]
	public void PayTypeMapsEachModelToItsOwnBalanceFileEntry()
	{
		// Handyman, mechanic, entertainer, guard, researcher - the order PerTypeStaffConsts comments give.
		Assert.AreEqual( 0, ParkWorld.StaffState.PayTypeOf( 5 ), "handyman is model 5, pay type 0" );
		Assert.AreEqual( 1, ParkWorld.StaffState.PayTypeOf( 4 ), "mechanic is model 4, pay type 1" );
		Assert.AreEqual( 2, ParkWorld.StaffState.PayTypeOf( 6 ), "entertainer is model 6, pay type 2" );
		Assert.AreEqual( 3, ParkWorld.StaffState.PayTypeOf( 7 ), "guard is model 7, pay type 3" );
		Assert.AreEqual( 4, ParkWorld.StaffState.PayTypeOf( 8 ), "researcher is model 8, pay type 4" );

		Assert.AreEqual( -1, ParkWorld.StaffState.PayTypeOf( 1 ), "a guest is not staff" );
		Assert.AreEqual( -1, ParkWorld.StaffState.PayTypeOf( 3 ), "a catalogue object is not staff" );
	}

	/// <summary>
	/// What the shipped park actually holds, pinned field by field.
	///
	/// <para>
	/// <b>Four of the five are saved in state 1 - walking - and the guard in state 0.</b> That is the whole
	/// of Alexah's "the staff still aren't navigating": the file has them mid-journey, and nothing has ever
	/// stepped them. It is also the check that the state offset is right, because 0 and 1 are the two
	/// states a park that has never opened could plausibly have left them in.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheFiveStaffAreSavedExactlyAsMeasured()
	{
		// thing, model, state, grade, happiness, tiredness, patrol BL, patrol TR, idling
		var expected = new (int Thing, int Model, int State, int Grade, float Happy, float Tired,
			int BottomLeft, int TopRight, int Idling)[]
		{
			(25, 5, 1, 3, 89f, 75f, 2728, 3641, 0),
			(26, 4, 1, 3, 89f, 75f, 2728, 3641, 0),
			(27, 6, 1, 3, 92f, 82f, 2352, 3249, 712),
			(28, 7, 0, 3, 91f, 79f, 2728, 3641, 752),
			(30, 8, 1, 2, 97f, 93f, 1, 16384, 0)
		};

		var staff = Staff();

		Assert.AreEqual( expected.Length, staff.Length, "staff in the park" );

		for ( var i = 0; i < expected.Length; ++i )
		{
			var (thing, model, state, grade, happy, tired, bottomLeft, topRight, idling) = expected[i];
			var saved = staff[i].Staff!.Value;

			Assert.AreEqual( thing, staff[i].ThingId, $"staff {i}'s thing id" );
			Assert.AreEqual( model, staff[i].Model, $"staff {thing}'s model" );
			Assert.AreEqual( state, saved.State, $"staff {thing}'s state" );
			Assert.AreEqual( grade, saved.PayGrade, $"staff {thing}'s pay grade" );
			Assert.AreEqual( happy, saved.Happiness, 0.01f, $"staff {thing}'s happiness" );
			Assert.AreEqual( tired, saved.Tiredness, 0.01f, $"staff {thing}'s tiredness" );
			Assert.AreEqual( bottomLeft, saved.PatrolBottomLeft, $"staff {thing}'s patrol corner" );
			Assert.AreEqual( topRight, saved.PatrolTopRight, $"staff {thing}'s other patrol corner" );
			Assert.AreEqual( idling, saved.TimeStartedIdling, $"staff {thing}'s idle stamp" );

			// A park that has never admitted anybody cannot have had any work done in it.
			Assert.AreEqual( 0, saved.JobsDone, $"staff {thing} has done a job in a park with no visitors" );
			Assert.AreEqual( 0, saved.RestArea, $"staff {thing} is using a rest area this park has not got" );
		}
	}

	/// <summary>
	/// <b>The patrol rectangles mean something, which is the strongest check on the offsets.</b> An offset
	/// a byte or two out still yields numbers; it does not yield a two-cell-wide strip running south from
	/// the gate, or a rectangle that is exactly the whole map.
	/// </summary>
	[TestMethod]
	public void ThePatrolRectanglesUnpackToPlacesThatMakeSense()
	{
		var byThing = Staff().ToDictionary( person => person.ThingId, person => person.Staff!.Value );

		static (int X, int Y) Cell( int packed ) => ((packed - 1) & 0x7f, (packed - 1) >> 7);

		// The entertainer works the entrance path: the gateway cells are (47,17) and (48,17), and his beat
		// is the two columns directly below them.
		Assert.AreEqual( (47, 18), Cell( byThing[27].PatrolBottomLeft ), "the entertainer's beat starts" );
		Assert.AreEqual( (48, 25), Cell( byThing[27].PatrolTopRight ), "the entertainer's beat ends" );

		// The researcher is not penned in at all - his rectangle is the entire map.
		Assert.AreEqual( (0, 0), Cell( byThing[30].PatrolBottomLeft ), "the researcher's area starts" );
		Assert.AreEqual( (ParkWorld.MapSize - 1, ParkWorld.MapSize - 1), Cell( byThing[30].PatrolTopRight ),
			"the researcher's area is the whole map" );

		// The other three share the park body, south and either side of the entrance.
		foreach ( var thing in new[] { 25, 26, 28 } )
		{
			Assert.AreEqual( (39, 21), Cell( byThing[thing].PatrolBottomLeft ), $"staff {thing}'s area starts" );
			Assert.AreEqual( (56, 28), Cell( byThing[thing].PatrolTopRight ), $"staff {thing}'s area ends" );
		}
	}

	/// <summary>
	/// The idle stamp is a reading of the park's own clock, so it cannot be ahead of it - and the two staff
	/// carrying one are within a minute of the tick the park was saved on. <b>A wrong offset could not land
	/// two values just under the park's own clock.</b>
	/// </summary>
	[TestMethod]
	public void TheIdleStampsAreReadingsOfTheParksOwnClock()
	{
		var world = World();

		Assert.IsTrue( world.GameTick > 0, "the park should have a clock to compare against" );

		foreach ( var person in World().People.Where( person => person.Staff != null ) )
		{
			var stamp = person.Staff!.Value.TimeStartedIdling;

			Assert.IsTrue( stamp <= world.GameTick,
				$"staff {person.ThingId} started idling at {stamp}, after the park's own clock "
				+ $"reached {world.GameTick}" );
		}
	}
}
