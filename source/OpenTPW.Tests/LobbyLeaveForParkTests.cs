using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The lobby's park-entry move - the original's globe states 1 and 2, which run between clicking Enter
/// and the park being asked for. See <see cref="LobbyCameraMode.LeaveForPark"/>.
///
/// <para>
/// <b>The numbers here are written out rather than read from the code under test.</b> 0.5 rad/s, 0.7
/// and 6.0 a second and the radius of 8 are the decode's, converted from the executable's per-delta
/// 0.05, 0.07, 0.6 and 8.0; taking them from the constants instead would be an instrument reading the
/// same state as its subject, which <c>docs/VERIFYING.md</c> rule 2 says cannot falsify anything.
/// </para>
/// <para>
/// <b>What these can and cannot see.</b> The arithmetic is pinned directly, and the sequence itself is
/// stepped through <see cref="LobbyCameraMode.StepLeaving"/> with <see cref="Time.Delta"/> set, from the
/// Enter to the park being asked for. <see cref="LobbyCameraMode.Update"/> placing the camera from what the
/// sequence answers needs a lobby's islands, and the panel's Enter asking for the sequence needs its UI, so
/// those two rest on the capture.
/// </para>
/// </summary>
[TestClass]
public class LobbyLeaveForParkTests
{
	private const float Target = MathF.PI;

	private float _delta;

	[TestInitialize]
	public void StartInOrbit()
	{
		// Forgetting a flight is logged, and run alone this class is the first to want a logger.
		Log ??= new();

		_delta = Time.Delta;

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.Paused = false;
	}

	[TestCleanup]
	public void PutTheLobbyBack()
	{
		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.DebugOrbit = 0f;

		Time.Delta = _delta;
	}

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

		// Just short of half a turn either way, where the short way and the long way are nearly the same length:
		// the turn changes direction at half a turn exactly, the original's pi (_DAT_00702c18).
		Assert.AreEqual( Target + 3.0f, LobbyCameraMode.NextAngle( Target + 3.1f, Target, 0.1f, out _ ), 0.0001f,
			"3.1 past the target is still the short way back down" );
		Assert.AreEqual( Target - 3.0f, LobbyCameraMode.NextAngle( Target - 3.1f, Target, 0.1f, out _ ), 0.0001f,
			"and 3.1 short of it the short way up" );
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

	/// <summary>
	/// <b>The sequence runs at the decode's rates, and asks for the park once, on arrival.</b> Entered one
	/// radian short of the gate four laps into the orbit - about two minutes in the lobby - with SPINRADIUS 70
	/// and VERTICALOFFSET 20, and stepped at 60 frames a second:
	/// <list type="bullet">
	/// <item>the turn covers the radian at 0.5 a second in 120 frames, and the frame after finds it inside
	/// half a step;</item>
	/// <item>a sixth of a second into the fly-in the radius is <c>70 (1 - 0.7/60)^10</c> = 62.25 and the eye
	/// is <c>20 (1 - 6/60)^10</c> = 6.97 above the island, and one second in they are 34.62 and 0.04;</item>
	/// <item>the radius first falls below 8 on the frame <c>ln(8/70) / ln(1 - 0.7/60)</c> rounds up to,
	/// 185, and that frame asks for the park, facing the gate at pi;</item>
	/// <item>Enter pressed again half a second into the turn and again into the fly-in changes nothing - the
	/// original refuses a second press while the state is not nought.</item>
	/// </list>
	/// At 30 a second the same arithmetic gives 60 frames and one, 62.21 and 6.55, 34.47 and 0.02, and 92.
	/// </summary>
	/// <remarks>
	/// Each count is exact because each lands well inside its frame: the turn ends with the gap near nought
	/// against a half-step of 1/240 or more, and the arriving frame leaves the radius at 7.98 against the
	/// frame before's 8.08 or 8.17 - so a threshold moved by less than that gap is not seen. The readings are
	/// the console's, to two places. Two frame rates, because one cannot tell a rate multiplied by
	/// <see cref="Time.Delta"/> from one divided by that rate's own frames; four laps in, because the orbit's
	/// angle is never wrapped until the sequence takes it.
	/// <b>Mutations:</b> the turn rate 0.5 to 0.49 or 0.51, the radius rate 0.7 to 0.69 or 0.71, the eye's
	/// rate 6 to 5.9 or 6.1, the threshold 8 to 7.95, the heading, a rate taken per frame of 60 rather
	/// than by <see cref="Time.Delta"/>, a wrap that takes off one turn rather than all of them, a second
	/// press taken, and not asking for the park, each fail an assertion here.
	/// </remarks>
	[DataTestMethod]
	[DataRow( 60, 121, 185, 62.25f, 6.97f, 34.62f, 0.04f )]
	[DataRow( 30, 61, 92, 62.21f, 6.55f, 34.47f, 0.02f )]
	public void TheSequenceTurnsFliesInAndAsksForTheParkOnce( int perSecond, int turningFrames, int flyingFrames,
		float radiusAtASixth, float eyeAtASixth, float radiusAtASecond, float eyeAtASecond )
	{
		Time.Delta = 1f / perSecond;
		LobbyCameraMode.DebugOrbit = MathF.PI - 1f + (4f * MathF.Tau);

		var asked = 0;
		var askedAgain = 0;
		var settings = new LobbyCameraMode.CameraSettings( SpinRadius: 70f, VerticalOffset: 20f );

		LobbyCameraMode.LeaveForPark( () => ++asked );

		var turning = 0;

		while ( Leave() == "Homing" && turning < 10_000 )
		{
			LobbyCameraMode.StepLeaving( settings );
			++turning;

			if ( turning == perSecond / 2 )
				LobbyCameraMode.LeaveForPark( () => ++askedAgain );
		}

		Assert.AreEqual( turningFrames, turning, "frames to turn the radian, and one to find it has arrived" );
		Assert.AreEqual( "FlyingIn", Leave() );
		Assert.AreEqual( 70f, Reading( "radius" ), 0.001f, "the fly-in starts from the orbit's own radius" );
		Assert.AreEqual( 20f, Reading( "vertical" ), 0.001f, "and its own height" );

		var flying = 0;
		var heading = 0f;

		while ( asked == 0 && flying < 10_000 )
		{
			heading = LobbyCameraMode.StepLeaving( settings );
			++flying;

			if ( flying == perSecond / 6 )
			{
				Assert.AreEqual( radiusAtASixth, Reading( "radius" ), 0.006f, "the radius a sixth of a second in" );
				Assert.AreEqual( eyeAtASixth, Reading( "vertical" ), 0.006f, "the eye a sixth of a second in" );
			}

			if ( flying == perSecond / 2 )
				LobbyCameraMode.LeaveForPark( () => ++askedAgain );

			if ( flying == perSecond )
			{
				Assert.AreEqual( radiusAtASecond, Reading( "radius" ), 0.006f, "the radius one second in" );
				Assert.AreEqual( eyeAtASecond, Reading( "vertical" ), 0.006f, "the eye one second in" );
				Assert.AreEqual( 0, asked, "not there yet" );
			}
		}

		Assert.AreEqual( flyingFrames, flying, "the frame the radius first falls below 8" );
		Assert.AreEqual( 1, asked, "the park is asked for once" );
		Assert.AreEqual( 0, askedAgain, "and never for the second press" );
		Assert.AreEqual( MathF.PI, heading, 0.0001f, "facing the gate" );
		Assert.AreEqual( "No", Leave(), "and the sequence is over" );
		StringAssert.EndsWith( LobbyCameraMode.LeaveDescription(), "waiting=False", "with nothing left to ask for" );
	}

	/// <summary>Which part of the sequence is running, from the console's own reading of it.</summary>
	private static string Leave() => Field( "leave" );

	/// <summary>One number from the console's reading of the sequence, which prints it to two places.</summary>
	private static float Reading( string name ) => float.Parse( Field( name ), CultureInfo.CurrentCulture );

	private static string Field( string name )
		=> LobbyCameraMode.LeaveDescription().Split( ' ' ).Single( part => part.StartsWith( name + "=" ) )[(name.Length + 1)..];
}
