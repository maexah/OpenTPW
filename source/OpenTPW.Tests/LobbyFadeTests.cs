using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// The park-entry fly-in's fade to black (<see cref="LobbyFade"/>; <c>docs/exe/lobby.md</c>, "Escape cancels the fly-in").
///
/// <para>
/// <b>The numbers are written out from the decode, not read from the code under test</b>: the target
/// <c>clamp( (1 - (r - 8) / (SPINRADIUS - 8)) x 65536, 0, 65535 )</c>, the alpha <c>min( v >> 8, 255 )</c>, and the ease of an
/// eighth of the gap per pass at the original's 30 passes a second. What is drawn is not seen here: that rests on the run.
/// </para>
/// </summary>
[TestClass]
public class LobbyFadeTests
{
	private const float Pass = 1f / 30f;

	private float _delta;

	[TestInitialize]
	public void Clear()
	{
		Log ??= new();
		_delta = Time.Delta;

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.Paused = false;
	}

	[TestCleanup]
	public void PutBack()
	{
		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.DebugOrbit = 0f;
		Time.Delta = _delta;
	}

	/// <summary>
	/// <b>The target counts from radius 8, not from nought</b>: half way from SPINRADIUS 70 to 8 is radius 39 and half black. A rule
	/// over the whole radius (<c>1 - r / 70</c>) answers 0.443 there.
	/// </summary>
	[TestMethod]
	public void TheTargetRunsFromTheSpinRadiusToEight()
	{
		Assert.AreEqual( 0f, LobbyFade.Target( 70f, 70f ), 1e-6f, "none at the orbit's radius" );
		Assert.AreEqual( 0.5f, LobbyFade.Target( 39f, 70f ), 1e-6f, "half at radius 39" );
		Assert.AreEqual( 0.25f, LobbyFade.Target( 54.5f, 70f ), 1e-6f, "a quarter at 54.5" );
		Assert.AreEqual( 0f, LobbyFade.Target( 90f, 70f ), 1e-6f, "never below nought" );
		Assert.AreEqual( 65535f / 65536f, LobbyFade.Target( 8f, 70f ), 1e-7f, "0xffff at 8, the clamp's top" );
		Assert.AreEqual( 65535f / 65536f, LobbyFade.Target( 2f, 70f ), 1e-7f, "and no further" );
	}

	/// <summary><b>The alpha is the value's high byte, held at 255</b>: 0x10000 is 255, not 256 wrapped to 0; 0x80 a byte is nought.</summary>
	[TestMethod]
	public void TheAlphaIsTheHighByteHeldAt255()
	{
		Assert.AreEqual( (byte)255, LobbyFade.AlphaOf( 1f ) );
		Assert.AreEqual( (byte)255, LobbyFade.AlphaOf( 65535f / 65536f ) );
		Assert.AreEqual( (byte)128, LobbyFade.AlphaOf( 0.5f ) );
		Assert.AreEqual( (byte)0, LobbyFade.AlphaOf( 255f / 65536f ), "0xff is below the first step" );
		Assert.AreEqual( (byte)1, LobbyFade.AlphaOf( 256f / 65536f ) );
		Assert.AreEqual( (byte)0, LobbyFade.AlphaOf( 0f ) );
	}

	/// <summary>
	/// <b>An eighth of the gap per pass at 30 a second, and the same curve per second at 60</b>: one 1/60 s frame closes
	/// <c>1 - sqrt( 7/8 )</c> of it, 0.0646. An eighth per frame, the original's rule taken raw, closes 0.125 at any rate.
	/// </summary>
	[TestMethod]
	public void TheEaseIsAnEighthAPassAtThirtyAndTheSameCurveAtSixty()
	{
		Time.Delta = Pass;
		LobbyFade.Step( 1f );
		Assert.AreEqual( 0.125f, LobbyFade.Value, 1e-4f, "one pass at 30 a second" );

		LobbyFade.Forget();
		Time.Delta = 1f / 60f;
		LobbyFade.Step( 1f );
		Assert.AreEqual( 1f - MathF.Sqrt( 7f / 8f ), LobbyFade.Value, 1e-4f, "one frame at 60 a second" );

		LobbyFade.Step( 1f );
		Assert.AreEqual( 0.125f, LobbyFade.Value, 1e-4f, "two frames at 60 are one pass at 30" );
	}

	/// <summary>
	/// <b>A cancel's lift from full black reaches alpha nought in 42 passes</b>, about 1.4 s: <c>65536 x (7/8)^n</c> first falls below
	/// 256 at n = 42.
	/// </summary>
	[TestMethod]
	public void TheLiftFromFullBlackTakesFortyTwoPasses()
	{
		Time.Delta = 100f;
		LobbyFade.Step( 1f );
		Assert.AreEqual( (byte)255, LobbyFade.Alpha, "full black to start" );

		Time.Delta = Pass;
		var passes = 0;

		while ( LobbyFade.Alpha > 0 && passes < 1000 )
		{
			LobbyFade.Step( 0f );
			passes++;
		}

		Assert.AreEqual( 42, passes );
	}

	/// <summary>
	/// <b>State 2 writes the target from the radius it has just decayed to, and the whole of it at arrival</b>: the first decay
	/// pass at 30 a second takes radius 70 to <c>70 x (1 - 0.7/30)</c> = 68.367, whose target is 0.0263; state 1 writes none.
	/// </summary>
	[TestMethod]
	public void StateTwoWritesTheTargetAndArrivalWritesTheWhole()
	{
		Time.Delta = Pass;
		var settings = new LobbyCameraMode.CameraSettings( 70f, 20f, 100f );

		// Already facing the gate, so the first pass is state 1's arrival and writes no target.
		LobbyCameraMode.DebugOrbit = MathF.PI;
		var asked = false;
		LobbyCameraMode.LeaveForPark( () => asked = true );

		LobbyCameraMode.StepLeaving( settings );
		Assert.AreEqual( 0f, LobbyCameraMode.FadeTarget, 1e-6f, "state 1 writes none" );

		LobbyCameraMode.StepLeaving( settings );
		var radius = 70f * (1f - (0.7f / 30f));
		Assert.AreEqual( 1f - ((radius - 8f) / 62f), LobbyCameraMode.FadeTarget, 1e-4f, "from the decayed radius" );
		Assert.AreEqual( 0.0263f, LobbyCameraMode.FadeTarget, 1e-3f );

		for ( var i = 0; i < 200 && !asked; ++i )
			LobbyCameraMode.StepLeaving( settings );

		Assert.IsTrue( asked, "the park was asked for" );
		Assert.AreEqual( 1f, LobbyCameraMode.FadeTarget, 1e-6f, "the whole of it at arrival" );
	}

	/// <summary><b>A new lobby starts with no fade</b>, as each lobby builds its render camera afresh.</summary>
	[TestMethod]
	public void ANewLobbyStartsClear()
	{
		Time.Delta = 100f;
		LobbyFade.Step( 1f );
		Assert.AreEqual( (byte)255, LobbyFade.Alpha );

		LobbyCameraMode.BeginLobby();
		Assert.AreEqual( (byte)0, LobbyFade.Alpha );
		Assert.AreEqual( 0f, LobbyFade.Value );
	}
}
