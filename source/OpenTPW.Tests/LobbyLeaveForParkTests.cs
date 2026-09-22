using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// The arithmetic of the lobby's park-entry move - the original's globe states 1 and 2, which run
/// between clicking Enter and the park being asked for. See <see cref="LobbyCameraMode.LeaveForPark"/>.
///
/// <para>
/// <b>The numbers here are written out rather than read from the code under test.</b> 0.5 rad/s, 0.7
/// and 6.0 a second and the radius of 8 are the decode's, converted from the executable's per-delta
/// 0.05, 0.07, 0.6 and 8.0; taking them from the constants instead would be an instrument reading the
/// same state as its subject, which <c>docs/VERIFYING.md</c> rule 2 says cannot falsify anything.
/// </para>
/// <para>
/// <b>What these cannot see.</b> Only the arithmetic is pure. The state machine itself reads
/// <c>Time.Delta</c>, needs a lobby with islands, and finishes by loading a park, so none of that is
/// reachable here and the wiring rests on the capture - said at the site, per rule 48.
/// </para>
/// </summary>
[TestClass]
public class LobbyLeaveForParkTests
{
	private const float Target = MathF.PI;

	/// <summary>A turn that has arrived stays put and says so, rather than stepping past and oscillating.</summary>
	[TestMethod]
	public void AnAngleAlreadyOnTheTargetHasArrived()
	{
		var next = LobbyCameraMode.NextAngle( Target, Target, 0.01f, out var arrived );

		Assert.IsTrue( arrived );
		Assert.AreEqual( Target, next, 0.0001f );
	}

	/// <summary>
	/// Within half a step counts as arrived - the original's own test. A step of 0.1 therefore swallows
	/// a gap of 0.04 but not one of 0.06.
	/// </summary>
	[TestMethod]
	public void ArrivalIsHalfAStepAndNotAWholeOne()
	{
		LobbyCameraMode.NextAngle( Target + 0.04f, Target, 0.1f, out var near );
		LobbyCameraMode.NextAngle( Target + 0.06f, Target, 0.1f, out var far );

		Assert.IsTrue( near, "0.04 away with a 0.1 step is inside half a step" );
		Assert.IsFalse( far, "0.06 away with a 0.1 step is not" );
	}

	/// <summary>
	/// <b>The shorter way round, in both directions.</b> This is the half that is easy to get wrong and
	/// impossible to see in a screenshot: a camera taking the long way round still arrives.
	/// </summary>
	[TestMethod]
	public void TheTurnTakesTheShorterWayRound()
	{
		// A little PAST the target: the gap the short way is small and negative-going.
		var back = LobbyCameraMode.NextAngle( Target + 0.5f, Target, 0.1f, out _ );

		Assert.IsTrue( back < Target + 0.5f, $"should turn down toward the target, got {back}" );
		Assert.AreEqual( Target + 0.4f, back, 0.0001f );

		// A little BEFORE it: the short way is upward.
		var forward = LobbyCameraMode.NextAngle( Target - 0.5f, Target, 0.1f, out _ );

		Assert.IsTrue( forward > Target - 0.5f, $"should turn up toward the target, got {forward}" );
		Assert.AreEqual( Target - 0.4f, forward, 0.0001f );
	}

	/// <summary>
	/// And it takes the short way <b>across the wrap</b>, which is where a naive subtraction sends the
	/// camera the long way round the whole circle.
	/// </summary>
	[TestMethod]
	public void TheTurnCrossesZeroRatherThanGoingAllTheWayRound()
	{
		// Target just above zero, camera just below 2pi: four hundredths apart across the seam.
		var next = LobbyCameraMode.NextAngle( MathF.Tau - 0.02f, 0.02f, 0.01f, out var arrived );

		Assert.IsFalse( arrived, "0.04 apart with a 0.01 step has not arrived" );

		// It must move TOWARDS the seam, i.e. up past 2pi and wrap to just under 0.02 - not down
		// through pi. Either way it stays within a tenth of the seam.
		var distance = MathF.Min( next, MathF.Tau - next );

		Assert.IsTrue( distance < 0.1f, $"should have stepped across the seam, landed at {next}" );
	}

	/// <summary>
	/// Every step moves, so the turn cannot stall - the failure that would hang a park entry for ever
	/// rather than merely looking wrong.
	/// </summary>
	[TestMethod]
	public void EveryStepThatHasNotArrivedActuallyMoves()
	{
		var angle = 0.2f;

		for ( var i = 0; i < 5; ++i )
		{
			var next = LobbyCameraMode.NextAngle( angle, Target, 0.05f, out var arrived );

			Assert.IsFalse( arrived, "0.2 is nowhere near pi" );
			Assert.AreNotEqual( angle, next, 0.0001f, $"step {i} did not move" );

			angle = next;
		}

		// Five steps of 0.05 toward pi from 0.2 is 0.45 of the way.
		Assert.AreEqual( 0.45f, angle, 0.0001f );
	}

	/// <summary>
	/// <b>The fly-in takes the time the decode says it does.</b> From SPINRADIUS 70, decaying 0.7 of
	/// what is left each second, reaching the threshold of 8 is <c>ln(70/8) / 0.7</c> = 3.10 s. That is
	/// the number the running game is checked against, so it is worth pinning independently of it.
	/// </summary>
	[TestMethod]
	public void TheFlyInReachesTheThresholdInAboutThreeSeconds()
	{
		var radius = 70f;
		var seconds = 0f;
		const float step = 1f / 60f;

		while ( radius >= 8f && seconds < 30f )
		{
			radius = LobbyCameraMode.Decayed( radius, 0.7f, step );
			seconds += step;
		}

		var predicted = MathF.Log( 70f / 8f ) / 0.7f;

		Assert.IsTrue( seconds < 30f, "the decay never reached the threshold at all" );
		Assert.AreEqual( predicted, seconds, 0.05f, $"closed in {seconds:F2}s against {predicted:F2}s" );
	}

	/// <summary>
	/// The eye drops to the island's level far sooner than the camera arrives - the vertical rate is
	/// almost ten times the radius rate, which is what makes it a swoop rather than a straight run in.
	/// </summary>
	[TestMethod]
	public void TheEyeComesDownLongBeforeTheCameraArrives()
	{
		float radius = 70f, vertical = 20f;
		const float step = 1f / 60f;

		// One second in.
		for ( var i = 0; i < 60; ++i )
		{
			radius = LobbyCameraMode.Decayed( radius, 0.7f, step );
			vertical = LobbyCameraMode.Decayed( vertical, 6f, step );
		}

		Assert.IsTrue( vertical < 0.1f, $"the eye should be down within a second, was {vertical:F3}" );
		Assert.IsTrue( radius > 30f, $"but the camera should still be well out, was {radius:F1}" );
	}

	/// <summary>A decay never overshoots past nought, however large the step it is given.</summary>
	[TestMethod]
	public void ADecayApproachesButDoesNotCross()
	{
		Assert.AreEqual( 0f, LobbyCameraMode.Decayed( 10f, 6f, 1f / 6f ), 0.0001f );
		Assert.IsTrue( LobbyCameraMode.Decayed( 10f, 0.7f, 1f / 60f ) > 0f );
	}
}
