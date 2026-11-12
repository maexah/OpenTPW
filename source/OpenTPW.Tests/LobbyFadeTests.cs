using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

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
[DoNotParallelize]
public class LobbyFadeTests
{
	private const float Pass = 1f / 30f;

	private float _delta;
	private float _rawDelta;

	[TestInitialize]
	public void Clear()
	{
		Log ??= new();
		_delta = Time.Delta;
		_rawDelta = Time.RawDelta;

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.Paused = false;
	}

	[TestCleanup]
	public void PutBack()
	{
		LobbyCountedGapsTests.SetCurrentPlayer( null );
		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.DebugOrbit = 0f;
		Time.Delta = _delta;
		Time.RawDelta = _rawDelta;
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
	/// pass at 30 a second takes radius 70 to <c>70 x (1 - 0.7/30)</c> = 68.367, whose target is 0.0263. (State 1's nought is
	/// written by the camera's update, not here: <see cref="AnEscapeInStateTwoLiftsTheFadeThroughTheUpdate"/>.)
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

	/// <summary>
	/// <b>The lobby's end clears the fade</b>, so a park is never drawn under the fly-in's last black: the park draws
	/// <see cref="LobbyFade"/> too and only the lobby steps it.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>LobbyFade.Forget</c> left out of <see cref="LobbyCameraMode.ForgetIsland"/> fails this.</remarks>
	[TestMethod]
	public void TheLobbysEndClearsTheFade()
	{
		Time.Delta = 100f;
		LobbyFade.Step( 1f );
		SetStatic( "_fadeTarget", 1f );
		Assert.AreEqual( (byte)255, LobbyFade.Alpha );

		LobbyCameraMode.ForgetIsland();

		Assert.AreEqual( (0f, 0f, (byte)0, 0f), (LobbyFade.Value, LobbyFade.Wanted, LobbyFade.Alpha, LobbyCameraMode.FadeTarget) );
	}

	/// <summary>
	/// <b>Through the camera's update: state 2 darkens, and an Escape lifts it at once</b>. A fly-in stood at radius 39 by hand
	/// (the stand-in island has no gate for state 1 to open) runs ten passes at 30 a second, the target rising from 0.5 as the
	/// radius decays and the fade easing after it; after the cancel the next pass writes the target nought and closes an
	/// eighth of the gap, the value times 7/8.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the update's nought for states 0 and 1 left out (the stale target pulls the fade up, never lifting);
	/// the update's <c>LobbyFade.Step</c> left out (the fade never moves); each fails this.
	/// </remarks>
	[TestMethod]
	public void AnEscapeInStateTwoLiftsTheFadeThroughTheUpdate()
	{
		var camera = LobbyEyeHandoverTests.ACamera( out _ );
		LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );
		LobbyCameraMode.BeginLobby();

		Time.RawDelta = Time.Delta = Pass;
		camera.Update();
		Assert.AreEqual( 0f, LobbyFade.Value, "in orbit, no fade" );

		var leaving = typeof( LobbyCameraMode ).GetField( "_leaving", BindingFlags.Static | BindingFlags.NonPublic )!;
		leaving.SetValue( null, Enum.Parse( leaving.FieldType, "FlyingIn" ) );
		SetStatic( "_leaveRadius", 39f );
		SetStatic( "_leaveVertical", 20f );
		SetStatic( "_whenArrived", (Action)(() => { }) );

		for ( var i = 0; i < 10; ++i )
			camera.Update();

		var radius = 39f * MathF.Pow( 1f - (0.7f / 30f), 10f );
		Assert.AreEqual( 1f - ((radius - 8f) / 62f), LobbyCameraMode.FadeTarget, 1e-3f, "state 2's target from the decayed radius" );
		var before = LobbyFade.Value;
		Assert.IsTrue( before > 0.3f, $"the fade eased up after it: {before}" );

		typeof( LobbyCameraMode ).GetProperty( nameof( LobbyCameraMode.CurrentIsland ) )!.SetValue( null, null );
		Assert.IsTrue( LobbyCameraMode.CancelLeave() );
		camera.Update();

		Assert.AreEqual( 0f, LobbyCameraMode.FadeTarget, "back in orbit, the target is nought" );
		Assert.AreEqual( before * 7f / 8f, LobbyFade.Value, 1e-4f, "and the fade closes an eighth of the gap" );
	}

	private static void SetStatic( string name, object value )
		=> typeof( LobbyCameraMode ).GetField( name, BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, value );
}
