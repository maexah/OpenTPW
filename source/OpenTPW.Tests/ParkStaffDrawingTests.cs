using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which pool a person's walk comes from when the park draws them.
///
/// <para>
/// <b>Guests and staff are drawn from one list, but their walks are kept in two pools.</b>
/// <see cref="ParkPeople.WalkFor"/> knows only the guests, and <see cref="ParkGuestSprites.Standing"/>
/// falls back to the position the save left a person at when it has no walk - so a drawing that asked
/// the guests' pool would show every member of staff standing still all run while they were simulated,
/// routed and moving.
/// </para>
/// <para>
/// <b>Why this is a test about the LOOKUP rather than about movement.</b> Neither <c>Standing</c>, which
/// does exactly what it is given, nor <see cref="StaffBehaviour"/>, which walks them, chooses the pool:
/// the caller does - so what has to be pinned is that the two pools disagree, and that the lookup the
/// drawing uses spans both.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkStaffDrawingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// The shipped park really does carry both kinds, which is what lets the rest of this class tell a
	/// right lookup from a wrong one. Measured, not chosen: five staff and thirteen guests.
	/// </summary>
	[TestMethod]
	public void TheParkCarriesBothStaffAndGuests()
	{
		var world = World();

		CollectionAssert.AreEquivalent( new[] { 25, 26, 27, 28, 30 },
			ParkPeople.StaffIn( world ).Select( member => member.ThingId ).ToArray(),
			"the five staff the save names" );

		Assert.AreEqual( 13, ParkPeople.PeepsIn( world ).Count, "and the guests beside them" );

		// Anti-vacuity for the identification itself: staff are the people carrying a staff block, and
		// every one of the ids above has one while no guest does.
		Assert.AreEqual( 5, world.People.Count( person => person.Staff != null ), "five staff records" );
	}

	/// <summary>
	/// <b>The two pools, as they stand.</b> A member of staff has a walk, and the guests' pool does
	/// not know about it.
	/// </summary>
	[TestMethod]
	public void AStaffMembersWalkIsFoundOnlyByTheLookupThatSpansBothPools()
	{
		var world = World();
		var people = new ParkPeople( world );

		foreach ( var member in ParkPeople.StaffIn( world ) )
		{
			// A drawing that asked this would take the null for "they are not going anywhere".
			Assert.IsNull( people.WalkFor( member.ThingId ),
				$"thing {member.ThingId} is staff, so the GUESTS' pool must not hold their walk" );

			Assert.IsNotNull( people.StaffWalkFor( member.ThingId ),
				$"thing {member.ThingId} is staff and the staff pool does hold it" );

			Assert.IsNotNull( people.AnyWalkFor( member.ThingId ),
				$"thing {member.ThingId}: the lookup the drawing uses has to find a staff walk, or they " +
				"are drawn at the position the save left them at for the whole run" );
		}
	}

	/// <summary>
	/// <b>The call site.</b> A member of staff who has moved is drawn
	/// where they have moved to, and not at the position the save left them at.
	/// </summary>
	/// <remarks>
	/// <b>The three tests around it pin what the two pools hold, not which pool the drawing reaches
	/// into.</b> This one moves a staff member and asks the drawing where they are, so it fails against a
	/// drawing that reaches into the guests-only pool.
	/// <para>
	/// The staff member is taken from <see cref="ParkPeople.Staff"/> rather than from
	/// <c>ParkPeople.StaffIn</c>: that helper builds fresh <see cref="Staff"/> objects with navigators of
	/// their own, so moving one of those would move somebody this <see cref="ParkPeople"/> has never heard
	/// of and the test would pass against anything.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void AStaffMemberWhoHasMovedIsDrawnWhereTheyMovedTo()
	{
		var world = World();
		var people = new ParkPeople( world );

		var member = people.Staff[0];
		var person = world.People.Single( saved => saved.ThingId == member.ThingId );
		var sprite = world.Sprites.Single( picture => picture.Slot == person.SpriteSlot );

		// Somewhere that is emphatically not where the save put them.
		member.Navigator.Position = new FixedVector(
			PeepNavigator.WaypointCentre( 50 ), PeepNavigator.WaypointCentre( 24 ) );

		const float Cell = 10f;
		var (x, y, _) = ParkGuestSprites.StandingFrom( people, Cell, Cell, person, sprite );

		var walk = people.StaffWalkFor( member.ThingId )!;
		var expectedX = (walk.Position.X / (float)FixedVector.One) * Cell;
		var expectedY = (walk.Position.Y / (float)FixedVector.One) * Cell;

		Assert.AreEqual( expectedX, x, 0.01f, "the drawing follows the staff member's walk" );
		Assert.AreEqual( expectedY, y, 0.01f, "in both axes" );

		// <b>And the half that makes it a test rather than a tautology.</b> With the guests-only lookup
		// the walk is never found and Standing falls back to exactly these two numbers.
		Assert.AreNotEqual( sprite.X, x, "not the position the save left them at" );
		Assert.AreNotEqual( sprite.Y, y, "in either axis" );
	}

	/// <summary>
	/// And the other half, without which the rule above would be satisfied by a lookup that simply
	/// answered everything: a guest's walk is still the guest's walk.
	/// </summary>
	[TestMethod]
	public void AGuestsWalkIsTheSameWhicheverLookupAsksForIt()
	{
		var world = World();
		var people = new ParkPeople( world );

		foreach ( var peep in ParkPeople.PeepsIn( world ) )
		{
			var guests = people.WalkFor( peep.ThingId );

			Assert.IsNotNull( guests, $"thing {peep.ThingId} is a guest and has a walk" );
			Assert.IsNull( people.StaffWalkFor( peep.ThingId ), "and is not in the staff pool" );

			Assert.AreSame( guests, people.AnyWalkFor( peep.ThingId ),
				$"thing {peep.ThingId}: spanning both pools must hand back the SAME walk, not a second one" );
		}
	}
}
