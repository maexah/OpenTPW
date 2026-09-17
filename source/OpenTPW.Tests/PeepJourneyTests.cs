using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Following a route, and the force that keeps a person on it.
///
/// <para>
/// The distances here are whole cells and halves of cells, chosen so every expected force divides exactly
/// - a rounding difference would otherwise hide a wrong shift in the arithmetic underneath.
/// </para>
/// </summary>
[TestClass]
public class PeepJourneyTests
{
	private const int Cell = FixedVector.One;

	private static PeepJourney Walking( params (int X, int Y)[] waypoints )
	{
		var journey = new PeepJourney
		{
			Radius = Cell,
			MaxSpeed = Cell,
			Position = FixedVector.Zero,
			Velocity = FixedVector.Zero,
			Count = waypoints.Length
		};

		foreach ( var (x, y) in waypoints )
		{
			journey.Waypoints.Add( FixedVector.AtCell( x, y ) );
			journey.LegLengths.Add( Cell );
		}

		return journey;
	}

	/// <summary>The two thresholds, which are deliberately different from one another.</summary>
	[TestMethod]
	public void TheTwoThresholdsAreTheOriginalsAndAreNotEqual()
	{
		Assert.AreEqual( 0x19999, PeepJourney.ArriveWithin );
		Assert.AreEqual( 0x20000, PeepJourney.PassWithin );
		Assert.AreEqual( 0x20000, PeepJourney.SlowingOver );

		Assert.IsTrue( PeepJourney.ArriveWithin < PeepJourney.PassWithin,
			"a middle waypoint is passed sooner than the end is reached" );
	}

	/// <summary>With no route at all the only force is a brake.</summary>
	[TestMethod]
	public void WithNoRouteItBrakes()
	{
		var journey = new PeepJourney { Velocity = new FixedVector( 0x1000, 0x2000 ), Count = 0 };

		Assert.AreEqual( new FixedVector( -0x1000, -0x2000 ), journey.Steer() );
	}

	/// <summary>And once it has arrived, likewise - that is the tick after arriving, not the one on it.</summary>
	[TestMethod]
	public void HavingArrivedItBrakes()
	{
		var journey = Walking( (5, 0) );

		journey.Velocity = new FixedVector( 0x3000, 0 );
		journey.Arrived = true;

		Assert.AreEqual( new FixedVector( -0x3000, 0 ), journey.Steer() );
	}

	/// <summary>
	/// Aiming at a middle waypoint that is still a long way off: straight at it, less whatever speed is
	/// already being carried.
	/// </summary>
	[TestMethod]
	public void AMiddleWaypointIsAimedAtFlatOut()
	{
		var journey = Walking( (5, 0), (9, 0), (12, 0) );

		Assert.AreEqual( FixedVector.AtCell( 5, 0 ), journey.Steer(), "straight at it" );

		journey.Velocity = new FixedVector( 0x8000, 0 );

		Assert.AreEqual( new FixedVector( (5 * Cell) - 0x8000, 0 ), journey.Steer(),
			"less the speed already carried" );
	}

	/// <summary>
	/// Coming within two radii of a middle waypoint steps on to the next one, takes that leg off what is
	/// left to walk, and aims at the new waypoint on the same tick.
	/// </summary>
	[TestMethod]
	public void PassingAWaypointStepsOnAndAimsAtTheNext()
	{
		var journey = Walking( (1, 0), (4, 0), (9, 0) );

		journey.CarriedLength = 3 * Cell;
		journey.Position = new FixedVector( 0x18000, 0 );

		// The first waypoint is now half a cell away, which is inside two radii.
		var force = journey.Steer();

		Assert.AreEqual( 1, journey.Index, "it stepped on" );
		Assert.AreEqual( 2 * Cell, journey.CarriedLength, "and took that leg off what is left" );
		Assert.AreEqual( new FixedVector( (4 * Cell) - 0x18000, 0 ), force,
			"and aimed at the new waypoint on the same tick, not the one it just left" );
	}

	/// <summary>Running out of carried waypoints is what asks the navigator for more.</summary>
	[TestMethod]
	public void RunningOutOfCarriedWaypointsAsksForMore()
	{
		var journey = Walking( (1, 0), (4, 0) );

		journey.Count = 9;                      // a longer route than the two being carried
		journey.Position = new FixedVector( 0x18000, 0 );

		var asked = 0;

		// Standing in for navigating again, which is what the original does here: restock the carried
		// waypoints from where the person now is, and start at the beginning of them.
		journey.Refill = () =>
		{
			++asked;

			journey.Waypoints.Clear();
			journey.Waypoints.Add( FixedVector.AtCell( 7, 0 ) );
			journey.Waypoints.Add( FixedVector.AtCell( 9, 0 ) );
			journey.LegLengths.Clear();
			journey.LegLengths.Add( Cell );
			journey.LegLengths.Add( Cell );
			journey.Index = 0;
		};

		journey.Steer();
		Assert.AreEqual( 0, asked, "one waypoint left, so nothing to ask for yet" );
		Assert.AreEqual( 1, journey.Index );

		journey.Position = new FixedVector( 0x38000, 0 );

		var force = journey.Steer();

		Assert.AreEqual( 1, asked, "the carried waypoints ran out" );
		Assert.AreEqual( 0, journey.Index, "and navigating again started them afresh" );
		Assert.AreEqual( new FixedVector( (7 * Cell) - 0x38000, 0 ), force,
			"aiming at the first of the new waypoints on the same tick" );
	}

	/// <summary>
	/// On the last leg it slows down: no faster than half the distance still to go. At two cells out that
	/// is one cell, which is also as fast as this person walks.
	/// </summary>
	[TestMethod]
	public void OnTheLastLegItClosesAtHalfTheDistance()
	{
		var journey = Walking( (2, 0) );

		journey.Target = FixedVector.AtCell( 2, 0 );

		Assert.AreEqual( new FixedVector( Cell, 0 ), journey.Steer() );
		Assert.IsFalse( journey.Arrived, "two cells out is further than one and three fifths" );
	}

	/// <summary>
	/// Closer in it both arrives and slows: one cell out is inside the arriving mark, and half a cell is
	/// half the distance to go.
	/// </summary>
	[TestMethod]
	public void ComingInsideTheMarkArrivesAndStillSlows()
	{
		var journey = Walking( (1, 0) );

		journey.Target = FixedVector.AtCell( 1, 0 );

		var force = journey.Steer();

		Assert.IsTrue( journey.Arrived, "one cell is inside one and three fifths" );
		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), force,
			"and the tick it arrives on still gets a real force, not a brake" );
	}

	/// <summary>
	/// <b>Standing exactly on the end gives no force at all - not a brake.</b> The original writes two
	/// zeroes and returns before it ever reaches the subtraction, which is a different answer.
	/// </summary>
	[TestMethod]
	public void StandingOnTheEndGivesNoForceRatherThanABrake()
	{
		var journey = Walking( (0, 0) );

		journey.Target = FixedVector.Zero;
		journey.Velocity = new FixedVector( 0x4000, 0x4000 );

		Assert.AreEqual( FixedVector.Zero, journey.Steer(),
			"no force - a brake here would have been (-0x4000, -0x4000)" );
	}
}
