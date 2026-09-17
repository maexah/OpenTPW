using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where a tired member of staff goes - the arm of <c>FUN_00506a40</c> that was blocked until the
/// catalogue object's flags byte was read.
///
/// <para>
/// <b>The assertion that proves the reading is which cell they aim at.</b> The shipped park's one rest
/// area is thing 20, catalogue item 1411, standing at (58,16) - and its <c>mEntryPos</c> is 1979, which
/// unpacks to the cell <b>(58,15)</b>. The original walks to the <i>entry</i>, not to the object, so a
/// build that used the object's own cell would still look like it worked, would still set the rest area,
/// and would be walking a staff member into the furniture.
/// <para>
/// <b>These two tests failed first, and that is why they are worth having.</b> The entry was decoded as a
/// plain <c>y * 128 + x</c>, which puts it at (59,15) - a cell with no connected edges at all, so no
/// route could ever be planned to it and every tired staff member stood about instead. <c>mEntryPos</c>
/// is packed with a one added, exactly as the patrol corners are. See
/// <see cref="ParkWorld.CatalogueObject.EntryCellX"/>.
/// </para>
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRestAreaTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	/// <summary>The two kinds whose decide arm the shared switch answers itself - see <see cref="StaffBehaviour"/>.</summary>
	private const int Guard = 28;

	private const int Researcher = 30;

	/// <summary>The shipped park's only rest area, and the cell it asks to be approached from.</summary>
	private const int RestAreaThing = 20;

	private const int EntryX = 58;

	private const int EntryY = 15;

	private (StaffBehaviour Behaviour, Staff Member, PeepWalk Walk) Worn( int thingId, ParkWorld? knownPark )
	{
		var world = Park();
		var behaviour = new StaffBehaviour( Balance(), new Random( 7 ), knownPark );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var member = ParkPeople.StaffIn( world ).Single( staff => staff.ThingId == thingId );
		var walk = new PeepWalk( member.Navigator, blocked );

		// Worn out, standing about, and long enough ago that the idle duration has run out - which is what
		// puts them through Decide on the next step.
		member.Tiredness = 0f;
		member.SetActivity( StaffActivity.Idle, tick: 1 );
		member.TimeStartedIdling = 0;

		return (behaviour, member, walk);
	}

	/// <summary>
	/// A worn-out guard finds the park's rest area, takes it, and heads for the cell it is approached
	/// from.
	/// </summary>
	[TestMethod]
	public void ATiredGuardWalksToTheRestAreasEntryCellRatherThanIntoTheRestArea()
	{
		var (behaviour, member, walk) = Worn( Guard, Park() );

		behaviour.Step( member, walk, playing: null, tick: 500 );

		Assert.AreEqual( StaffActivity.GoingToRest, member.Activity,
			"a tired staff member with a rest area in the park should set off for it" );
		Assert.AreEqual( RestAreaThing, member.RestArea, "and should have claimed thing 20" );

		var (targetX, targetY) = member.Navigator.Target.Cell;

		Assert.AreEqual( EntryX, targetX, "they walk to the rest area's mEntryPos, not to the object" );
		Assert.AreEqual( EntryY, targetY, "and the same down the map" );

		Assert.AreNotEqual( (58, 16), (targetX, targetY),
			"walking to the object's own cell would put them inside the furniture" );
	}

	/// <summary>The researcher does the same, so this is the shared arm rather than one member's luck.</summary>
	[TestMethod]
	public void SoDoesTheResearcher()
	{
		var (behaviour, member, walk) = Worn( Researcher, Park() );

		behaviour.Step( member, walk, playing: null, tick: 500 );

		Assert.AreEqual( StaffActivity.GoingToRest, member.Activity, "the researcher sets off too" );
		Assert.AreEqual( RestAreaThing, member.RestArea, "for the same rest area" );
	}

	/// <summary>
	/// <b>The control, and it is what stops the test above being vacuous.</b> The same worn-out guard,
	/// given a behaviour that knows of no park, cannot find a rest area - so they take the original's
	/// other path and stand about instead. If the arm fired regardless of what the park holds, this would
	/// fail.
	/// </summary>
	[TestMethod]
	public void WithNoParkToSearchTheyTakeTheCouldNotFindOnePath()
	{
		var (behaviour, member, walk) = Worn( Guard, knownPark: null );

		behaviour.Step( member, walk, playing: null, tick: 500 );

		Assert.AreEqual( StaffActivity.Idle, member.Activity,
			"with nowhere to rest they stand about, which is the original's own other arm" );
		Assert.AreEqual( 0, member.RestArea, "and claim nothing" );
	}

	/// <summary>
	/// A staff member who is <b>not</b> tired does not go looking - otherwise the arm above would fire for
	/// everybody and the rest level would mean nothing.
	/// </summary>
	[TestMethod]
	public void SomebodyWhoIsNotTiredDoesNotGoLookingForARestArea()
	{
		var (behaviour, member, walk) = Worn( Guard, Park() );

		member.Tiredness = 100f;

		behaviour.Step( member, walk, playing: null, tick: 500 );

		Assert.AreNotEqual( StaffActivity.GoingToRest, member.Activity,
			"a rested guard has no business in a rest area" );
		Assert.AreEqual( 0, member.RestArea, "and claims none" );
	}

	/// <summary>
	/// The park really does hold exactly one rest area, which is what makes "the nearest" unambiguous
	/// here - and is the reason this file can assert a particular thing id at all.
	/// </summary>
	[TestMethod]
	public void TheParkHoldsExactlyOneRestAreaAndItIsThingTwenty()
	{
		var world = Park();
		var restAreas = world.Objects.Where( o => o.IsRestArea ).ToArray();

		Assert.AreEqual( 1, restAreas.Length, "rest areas in the shipped park" );
		Assert.AreEqual( RestAreaThing, restAreas[0].ThingId, "and which thing it is" );
		Assert.AreEqual( 58, restAreas[0].CellX, "where it stands" );
		Assert.AreEqual( 16, restAreas[0].CellY, "where it stands, down the map" );
		Assert.AreEqual( EntryX, restAreas[0].EntryCellX, "and where it is approached from" );
		Assert.AreEqual( EntryY, restAreas[0].EntryCellY, "and the same down the map" );

		// The raw field, pinned as the file holds it - so that the unpacking above is visibly an
		// unpacking rather than two numbers that happen to agree.
		Assert.AreEqual( 1979, restAreas[0].EntryPos, "mEntryPos packed, as it sits in the record" );
	}
}
