using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// One tick of a walking person: adding up what is pulling on them, and moving them if they may move.
/// </summary>
[TestClass]
public class PeepSteeringTests
{
	private const int Cell = FixedVector.One;

	private static readonly Func<FixedVector, FixedVector, bool> Allowed = ( _, _ ) => true;
	private static readonly Func<FixedVector, FixedVector, bool> Refused = ( _, _ ) => false;

	private static PeepSteering Walker() => new() { MaxForce = Cell, MaxSpeed = Cell };

	private static List<Steer> Just( int weight, FixedVector force )
		=> [new Steer( weight, () => force )];

	/// <summary>The three weights, as the original registers them.</summary>
	[TestMethod]
	public void TheThreeWeightsAreTheOriginals()
	{
		Assert.AreEqual( 0xe666, PeepSteering.AvoidWallsWeight, "nine tenths, for the walls" );
		Assert.AreEqual( 0x8000, PeepSteering.FollowPathWeight, "a half, for the route" );
		Assert.AreEqual( 0x1999, PeepSteering.SeparationWeight, "a tenth, for crowding - not built" );

		Assert.IsTrue( PeepSteering.AvoidWallsWeight > PeepSteering.FollowPathWeight,
			"the walls outweigh the route, which is why nobody walks through scenery" );
	}

	/// <summary>A single behaviour at full weight moves the person by what it asked for.</summary>
	[TestMethod]
	public void OneBehaviourMovesThePerson()
	{
		var walker = Walker();

		walker.Step( Just( FixedVector.One, new FixedVector( Cell / 2, 0 ) ), Allowed, () => 0 );

		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Velocity );
		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Position, "and they got there" );
	}

	/// <summary>
	/// <b>A behaviour is clamped before it is weighted</b>, so asking for far more than the limit buys no
	/// extra say. Asking for four cells with a limit of one gives the same answer as asking for one.
	/// </summary>
	[TestMethod]
	public void ABehaviourIsClampedBeforeItIsWeighted()
	{
		var shouting = Walker();
		var asking = Walker();

		shouting.Step( Just( FixedVector.One, new FixedVector( 4 * Cell, 0 ) ), Allowed, () => 0 );
		asking.Step( Just( FixedVector.One, new FixedVector( Cell, 0 ) ), Allowed, () => 0 );

		Assert.AreEqual( asking.Velocity, shouting.Velocity );
	}

	/// <summary>Weight is applied after that clamp, so half weight is half the push.</summary>
	[TestMethod]
	public void WeightIsAppliedAfterTheClamp()
	{
		var walker = Walker();

		walker.Step( Just( PeepSteering.FollowPathWeight, new FixedVector( Cell, 0 ) ), Allowed, () => 0 );

		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Velocity );
	}

	/// <summary>
	/// <b>The order of the clamp and the weight, pinned where it can actually be seen.</b> It takes a force
	/// ABOVE the limit and a weight BELOW one to tell the two apart: clamping first gives a whole cell
	/// halved, and weighting first gives four cells halved and then clamped, which is a whole cell.
	///
	/// <para>
	/// The two tests either side of this one cannot distinguish them - one uses full weight and the other a
	/// force already under the limit, and both orders agree in those cases.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AHeavyForceAtHalfWeightShowsWhichComesFirst()
	{
		var walker = Walker();

		walker.Step( Just( PeepSteering.FollowPathWeight, new FixedVector( 4 * Cell, 0 ) ),
			Allowed, () => 0 );

		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Velocity,
			"clamped to one cell and then halved - weighting first would have given a whole cell" );
	}

	/// <summary>Two behaviours add together, and the total is held to the same limit as one of them.</summary>
	[TestMethod]
	public void TwoBehavioursAddUpAndTheTotalIsClampedToo()
	{
		var walker = Walker();

		var both = new List<Steer>
		{
			new( FixedVector.One, () => new FixedVector( Cell, 0 ) ),
			new( FixedVector.One, () => new FixedVector( Cell, 0 ) )
		};

		walker.Step( both, Allowed, () => 0 );

		Assert.AreEqual( new FixedVector( Cell, 0 ), walker.Velocity,
			"two whole cells asked for, one cell allowed" );
	}

	/// <summary>Speed is held down as well, separately from force.</summary>
	[TestMethod]
	public void SpeedIsHeldDownSeparatelyFromForce()
	{
		var walker = Walker();

		walker.MaxSpeed = Cell / 4;
		walker.Step( Just( FixedVector.One, new FixedVector( Cell, 0 ) ), Allowed, () => 0 );

		Assert.AreEqual( new FixedVector( Cell / 4, 0 ), walker.Velocity );
	}

	/// <summary>
	/// <b>A refused step leaves the person where they were, but not going nowhere.</b> The speed is worked
	/// out and kept either way; it is only the move that is refused.
	/// </summary>
	[TestMethod]
	public void ARefusedStepLeavesThemWhereTheyWereButStillMoving()
	{
		var walker = Walker();

		walker.Step( Just( FixedVector.One, new FixedVector( Cell / 2, 0 ) ), Refused, () => 0 );

		Assert.AreEqual( FixedVector.Zero, walker.Position, "they did not move" );
		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Velocity, "but they are still trying" );
	}

	/// <summary>
	/// <b>A taken step writes one bit into the record of being stuck; a refused one writes two.</b> That
	/// asymmetry is the original's.
	/// </summary>
	[TestMethod]
	public void ARefusedStepFillsTheStuckRecordTwiceAsFast()
	{
		var went = Walker();
		var stopped = Walker();

		// Progress improves in both, so only the step itself writes anything.
		went.LastProgress = 0;
		stopped.LastProgress = 0;

		went.Step( Just( FixedVector.One, new FixedVector( Cell / 2, 0 ) ), Allowed, () => 1 );
		stopped.Step( Just( FixedVector.One, new FixedVector( Cell / 2, 0 ) ), Refused, () => 1 );

		Assert.AreEqual( 0b0, went.StuckBits, "one shift, nothing written" );
		Assert.AreEqual( 0b10, stopped.StuckBits, "a one written, then shifted along" );
	}

	/// <summary>Getting no closer writes a one at the end of the tick, whether or not the step was taken.</summary>
	[TestMethod]
	public void GettingNoCloserWritesAOneAsWell()
	{
		var stalled = Walker();
		var closing = Walker();

		stalled.LastProgress = 50;
		closing.LastProgress = 50;

		stalled.Step( [], Allowed, () => 50 );
		closing.Step( [], Allowed, () => 51 );

		Assert.AreEqual( 0b1, stalled.StuckBits, "no closer than last tick" );
		Assert.AreEqual( 0b0, closing.StuckBits, "closer, so nothing written" );

		Assert.AreEqual( 50, stalled.LastProgress, "and the mark is remembered either way" );
		Assert.AreEqual( 51, closing.LastProgress );
	}

	/// <summary>The record shifts along, so old ticks march up and out.</summary>
	[TestMethod]
	public void TheRecordShiftsAlongEveryTick()
	{
		var walker = Walker();

		walker.LastProgress = 0;

		// Three ticks of getting nowhere, each writing a one at the end.
		for ( var tick = 0; tick < 3; ++tick )
			walker.Step( [], Allowed, () => 0 );

		Assert.AreEqual( 0b111, walker.StuckBits );
	}

	/// <summary>With nothing pulling, a person still has their speed held down and still steps.</summary>
	[TestMethod]
	public void WithNothingPullingTheyCarryOnAtTheSpeedTheyHad()
	{
		var walker = Walker();

		walker.Velocity = new FixedVector( Cell / 2, 0 );
		walker.Step( [], Allowed, () => 0 );

		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Velocity );
		Assert.AreEqual( new FixedVector( Cell / 2, 0 ), walker.Position );
	}
}
