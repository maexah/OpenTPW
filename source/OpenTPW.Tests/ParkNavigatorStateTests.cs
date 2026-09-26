using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where Lost Kingdom's eighteen people were going when the park was saved. These read real game files
/// and are skipped where there is no installation: see <see cref="GameData"/>.
///
/// <para>
/// The navigator's block is reached the same way a guest's is - by summing the sizes of every field
/// before it - so nothing in it can be found by searching, and a map one byte out still returns a full
/// set of plausible-looking numbers. What makes this block checkable rather than merely readable is that
/// it carries the person's own position at a finer resolution than <c>mX</c> and <c>mY</c> do, and
/// <c>mX</c>/<c>mY</c> are read, and tested, apart from it.
/// </para>
/// </summary>
[TestClass]
public class ParkNavigatorStateTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <inheritdoc cref="ParkGuestStateTests.World"/>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// <b>The check the whole block rests on.</b> The navigator keeps its own position in 65536ths of a
	/// cell, and the engine reaches <c>mX</c> and <c>mY</c> from it by shifting right by eight - so the
	/// two have to agree, for every person, exactly.
	///
	/// <para>
	/// This is worth more than any range check because the two halves know nothing about each other.
	/// <c>mX</c> sits at <c>+8</c> and is read and tested on its own; the navigator's position sits at
	/// <c>+140</c> and was placed there by summing field sizes out of the executable. Neither was used to find the other. A block a single byte out of place cannot satisfy
	/// this for one person, let alone eighteen.
	/// </para>
	/// <para>
	/// The last assertion is the anti-vacuity guard: the park's people stand in seventeen different places,
	/// so this is not a column of equal numbers agreeing with itself.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryPersonsNavigatorSitsExactlyWhereTheirThingDoes()
	{
		var people = World().People;

		Assert.AreEqual( 18, people.Count, "people in the park" );

		foreach ( var person in people )
		{
			Assert.AreEqual( person.RawX, person.Navigator.RawX,
				$"thing {person.ThingId} is at mX {person.RawX} but navigates from "
				+ $"{person.Navigator.RawX} - the block is being read in the wrong place" );
			Assert.AreEqual( person.RawY, person.Navigator.RawY,
				$"thing {person.ThingId} is at mY {person.RawY} but navigates from {person.Navigator.RawY}" );
		}

		Assert.IsTrue( people.Select( person => person.RawX ).Distinct().Count() >= 15,
			"the park's people should not all share one column, or this test proves nothing" );
	}

	/// <summary>
	/// Every person's steering settings, pinned as measured. The two that never vary are pinned as
	/// constants the original's constructor writes; the two that do are pinned per person, because they
	/// are the evidence that these are live values rather than defaults.
	/// </summary>
	[TestMethod]
	public void EveryPersonCarriesTheSteeringSettingsTheSaveGaveThem()
	{
		var expected = new (int ThingId, int MaxForce, int MaxSpeed)[]
		{
			(42, 15728,  7864), (41, 26214, 13107), (40, 26491, 13245), (39, 15728,  7864),
			(38, 36700, 18350), (37, 31457, 15728), (36, 26491, 13245), (35, 31457, 15728),
			(34, 31457, 15728), (33, 36700, 18350), (32, 26491, 13245), (31, 15728,  7864),
			(30, 36700, 18350), (29, 15728,  7864), (28, 31581, 15790), (27, 36700, 18350),
			(26, 31457, 15728), (25, 31457, 15728)
		};

		var people = World().People;

		CollectionAssert.AreEqual( expected.Select( row => row.ThingId ).ToArray(),
			people.Select( person => person.ThingId ).ToArray(),
			"the people, by thing id, in the order the file lists them" );

		for ( var i = 0; i < expected.Length; ++i )
		{
			var row = expected[i];
			var nav = people[i].Navigator;

			Assert.AreEqual( row.MaxForce, nav.MaxForce, $"thing {row.ThingId} max force" );
			Assert.AreEqual( row.MaxSpeed, nav.MaxSpeed, $"thing {row.ThingId} max speed" );

			Assert.AreEqual( ParkWorld.NavigatorState.DefaultMass, nav.Mass,
				$"thing {row.ThingId} should still carry the mass the constructor gives it" );
			Assert.AreEqual( ParkWorld.NavigatorState.DefaultRadius, nav.Radius,
				$"thing {row.ThingId} should still carry the constructor's fifth of a cell of personal space" );
		}

		Assert.IsTrue( people.Select( person => person.Navigator.MaxSpeed ).Distinct().Count() >= 5,
			"the people should not all move at one speed, or the rows above prove nothing" );
	}

	/// <summary>
	/// <b>Max force is exactly twice max speed, on every person.</b> Two fields read at two offsets
	/// derived separately, holding a fixed relation eighteen times over - which the constructor agrees
	/// with, writing <c>0.4</c> and <c>0.2</c>.
	///
	/// <para>
	/// The halving truncates, so the relation is stated the way the numbers actually sit rather than as a
	/// clean multiply: <c>26491</c> against <c>13245</c> is twice plus one.
	/// </para>
	/// </summary>
	[TestMethod]
	public void MaxForceIsTwiceMaxSpeedForEveryone()
	{
		foreach ( var person in World().People )
		{
			var nav = person.Navigator;

			Assert.AreEqual( nav.MaxSpeed, nav.MaxForce / 2,
				$"thing {person.ThingId} has max force {nav.MaxForce} against max speed {nav.MaxSpeed}, "
				+ "which is not the doubling every other person shows" );
		}
	}

	/// <summary>
	/// Nobody is travelling faster than they are allowed to, which is the steering loop's clamp showing up
	/// in the saved data.
	///
	/// <para>
	/// The tolerance is deliberate and is the point of the test rather than a weakening of it. The clamp is
	/// applied in fixed point and the saved speeds sit a handful of units either side of the cap - guest 42
	/// is about four over, guest 40 about seventy. A tolerance tight enough to reject those would fail
	/// against correct data and prove nothing; two per cent still rejects anything read from the wrong
	/// offset, where the two fields would have no relation at all.
	/// </para>
	/// <para>
	/// The second assertion stops this passing for the wrong reason: if every velocity were zero, "no
	/// faster than allowed" would be true and empty.
	/// </para>
	/// </summary>
	[TestMethod]
	public void NobodyMovesFasterThanTheirMaxSpeed()
	{
		var people = World().People;

		foreach ( var person in people )
		{
			var nav = person.Navigator;
			var speed = MathF.Sqrt( (float)nav.VelocityX * nav.VelocityX
				+ (float)nav.VelocityY * nav.VelocityY );

			Assert.IsTrue( speed <= nav.MaxSpeed * 1.02f,
				$"thing {person.ThingId} is moving at {speed:0} against a cap of {nav.MaxSpeed}" );
		}

		Assert.IsTrue( people.Count( person => person.Navigator.Speed > 0.05f ) >= 15,
			"most of the park should be in motion, or the cap above is being checked against nothing" );
	}

	/// <summary>
	/// The park was saved moments after it opened, and the path state says so the same way the guests'
	/// needs do: everyone is walking, nobody has failed.
	///
	/// <para>
	/// <c>PathCount</c> is the cursor and <c>PathTotalCount</c> the length of the whole route, so the first
	/// can never exceed the second. Those are relations rather than values, and relations are what caught
	/// a field being read at the wrong offset while still returning a plausible zero.
	/// </para>
	/// <para>
	/// <b><c>PathBufferCount</c> equalling <c>PathTotalCount</c> is a fact about this park, not a law.</b>
	/// The navigator streams a long route through its five slots and refetches when the cursor reaches
	/// <c>PathBufferCount</c>, so the two part company as soon as a route needs more than five waypoints.
	/// Every route in the shipped park is three or shorter, which is why they agree here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryoneIsWalkingSomewhereAndNobodyHasGivenUp()
	{
		var people = World().People;

		foreach ( var person in people )
		{
			var nav = person.Navigator;
			var where = $"thing {person.ThingId}";

			Assert.IsFalse( nav.Stuck, $"{where} should not have given up on reaching anywhere" );
			Assert.AreEqual( 0, nav.NavMode, $"{where} navigation mode" );

			Assert.IsTrue( nav.PathCount <= nav.PathTotalCount,
				$"{where} is at waypoint {nav.PathCount} of {nav.PathTotalCount}" );
			Assert.IsTrue( nav.PathTotalCount is > 0 and <= ParkWorld.NavigatorState.SubpathSlots,
				$"{where} has {nav.PathTotalCount} waypoints, which will not fit the navigator's five" );
			Assert.AreEqual( nav.PathTotalCount, nav.PathBufferCount,
				$"{where} holds {nav.PathBufferCount} of its {nav.PathTotalCount} waypoints - which should "
				+ "match in this park, where no route is longer than the navigator's five slots" );

			Assert.AreNotEqual( 0, nav.TargetX, $"{where} should be heading somewhere" );
		}

		Assert.IsTrue( people.Count( person => person.Navigator.PathTotalCount > 1 ) >= 2,
			"some of the park should be on a route with a corner in it, or the waypoint checks above "
			+ "only ever see routes of one" );
	}

	/// <summary>
	/// How long each person's route measured when it was planned, pinned as saved.
	///
	/// <para>
	/// These are the three distances the navigator measures progress against, and they are octagonal
	/// rather than straight-line: the metric is <c>ax + ay - min(ax, ay) / 2</c>. The totals below are
	/// specific enough to be evidence on their own - eighteen different six-figure numbers that a reader
	/// pointed a few bytes out could not reproduce.
	/// </para>
	/// <para>
	/// <b>The other two assertions are not evidence, and are marked rather than dropped.</b> Every person
	/// in this park has a tail distance of nothing, because no route here is longer than the navigator's
	/// five slots, and a buffered distance of nothing, because the two people on a route with corners have
	/// both already reached their last leg. A column of zeros agrees with a misread block just as readily
	/// as with a correct one, so these say only that the shipped park is in the state it looks to be in.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryRouteIsAsLongAsTheSaveSaysItWas()
	{
		var expected = new (int ThingId, int TotalDistance)[]
		{
			(42, 545989), (41, 270172), (40, 317242), (39, 553109), (38, 306209), (37, 267587),
			(36, 269625), (35, 623119), (34, 307220), (33, 511906), (32, 290140), (31, 510158),
			(30, 170889), (29, 512409), (28, 128418), (27, 204458), (26, 194246), (25, 220769)
		};

		var people = World().People;

		Assert.AreEqual( expected.Length, people.Count, "people in the park" );

		for ( var i = 0; i < expected.Length; ++i )
		{
			var row = expected[i];
			var nav = people[i].Navigator;

			Assert.AreEqual( row.ThingId, people[i].ThingId, "the people, in the order the file lists them" );
			Assert.AreEqual( row.TotalDistance, nav.TotalDistance, $"thing {row.ThingId} route length" );

			Assert.AreEqual( 0, nav.TailDistance,
				$"thing {row.ThingId} should have no unbuffered tail, its route fitting the five slots" );
			Assert.AreEqual( 0, nav.BufferedDistance,
				$"thing {row.ThingId} is on its last leg, so nothing should be left buffered ahead of it" );
		}

		Assert.AreEqual( expected.Length, expected.Select( row => row.TotalDistance ).Distinct().Count(),
			"all eighteen route lengths should differ, or the column above is agreeing with itself" );
	}

	/// <summary>
	/// Staff navigate exactly as guests do, and this is the control for the reader being on the person
	/// base rather than on the guest block.
	///
	/// <para>
	/// A guest's record is 533 bytes and a staff record 509 to 513, so a navigator read from the wrong
	/// half would still land inside every record and still return numbers. What it would not do is place
	/// the five staff correctly, which the first test already requires of them.
	/// </para>
	/// </summary>
	[TestMethod]
	public void StaffCarryANavigatorJustAsGuestsDo()
	{
		var people = World().People;
		var staff = people.Where( person => person.Guest == null ).ToArray();

		Assert.AreEqual( 5, staff.Length, "staff in the park" );

		foreach ( var person in staff )
		{
			Assert.AreEqual( person.RawX, person.Navigator.RawX, $"staff {person.ThingId} navigates from mX" );
			Assert.AreEqual( ParkWorld.NavigatorState.DefaultRadius, person.Navigator.Radius,
				$"staff {person.ThingId} personal space" );
			Assert.IsTrue( person.Navigator.MaxSpeed > 0, $"staff {person.ThingId} should be able to move" );
		}
	}
}
