using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The attract flight and the orbit share one eye (Q749 review): the flight's pass ends by copying its eye <c>+0x78</c> and
/// look point <c>+0xb4</c> into the island camera's <c>+0x28</c> and <c>+0x40</c> (<c>0x005e0dfd</c>..<c>0x005e0e26</c>), and
/// the orbit's pass copies them back (<c>0x005e07a0</c>..<c>0x005e07c7</c>); a lobby's first pass takes the delta cap of
/// 500 ms (<c>0x005d5c6f</c>) and blends linearly (<c>docs/exe/lobby.md</c>, "The orbit's seed").
///
/// <para>
/// <b>What these cannot see.</b> The islands are stand-ins made without their constructors and handed to the camera
/// directly, so that a lobby builds its islands and runs this camera every frame rests on the game run. The expected
/// numbers are written out from the law, not read from the code under test. Nor can they see that the renderer carries a
/// build's length in the next frame's delta (<c>Time.RawDelta</c> is set by hand): a <c>_lastFrame</c> reset after a load
/// would make the first pass an ordinary frame's, and only the log line "the first pass after a build steps" shows it.
/// </para>
/// </summary>
[TestClass]
[DoNotParallelize]
public class LobbyEyeHandoverTests
{
	private float _delta;
	private float _rawDelta;

	[TestInitialize]
	public void AnEmptyLobby()
	{
		Log ??= new();

		_delta = Time.Delta;
		_rawDelta = Time.RawDelta;

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.Paused = false;
		LobbyCountedGapsTests.SetCurrentPlayer( null );
	}

	[TestCleanup]
	public void PutTheLobbyBack()
	{
		LobbyCountedGapsTests.SetCurrentPlayer( null );
		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.DebugOrbit = 0f;

		Time.Delta = _delta;
		Time.RawDelta = _rawDelta;
	}

	/// <summary>
	/// <b>A player chosen out of the attract flight eases from the flight's eye and look point, not from the seed.</b>
	/// The flight is put at (300, 650, 60) looking at (350, 400, 30); one frame of 0.05 s with a player picked closes
	/// 1 - e^-0.05 = 0.04877 of the eye's gap (rate 1 a second) and 1 - e^-0.1 = 0.09516 of the look point's (rate 2).
	/// The island stands at (600, 400, 0) with its camera target 12.5 above, the orbit at 0.05 s x 0.2 = 0.01 rad.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the attract pass not copying its eye and look into the island camera's (the cut to the seed);
	/// the copy of the eye alone; the copy of the look point alone; the copy at the top of the pass, before the flight's step.
	/// </remarks>
	[TestMethod]
	public void APlayerChosenOutOfTheFlightEasesFromTheFlightsEye()
	{
		var camera = ACamera( out var island );

		LobbyCameraMode.BeginLobby();

		Time.RawDelta = Time.Delta = 0.05f;
		camera.Update();

		// Standing the flight where the test wants it; no step, so the pass only copies.
		SetStatic( "_wanderPosition", new Vector3( 300f, 650f, 60f ) );
		SetStatic( "_lookPosition", new Vector3( 350f, 400f, 30f ) );
		Time.RawDelta = Time.Delta = 0f;
		camera.Update();

		AssertNear( new Vector3( 300f, 650f, 60f ), GetStatic( "_position" ), 0.001f, "the island camera's eye is the flight's" );
		AssertNear( new Vector3( 350f, 400f, 30f ), GetStatic( "_lookAt" ), 0.001f, "and its look point" );

		// A pass that does step: the copy is of the flight after its step (0x005e0dfd comes after the flight's moves), not the pass before's.
		Time.RawDelta = Time.Delta = 0.05f;
		camera.Update();

		var stepped = GetStatic( "_wanderPosition" );
		Assert.IsTrue( (stepped - new Vector3( 300f, 650f, 60f )).Length > 0.1f, "the flight stepped in this pass" );
		AssertNear( stepped, GetStatic( "_position" ), 0.0001f, "the copy is of the flight after its step: the eye" );
		AssertNear( GetStatic( "_lookPosition" ), GetStatic( "_lookAt" ), 0.0001f, "and the look point" );

		SetStatic( "_wanderPosition", new Vector3( 300f, 650f, 60f ) );
		SetStatic( "_lookPosition", new Vector3( 350f, 400f, 30f ) );
		Time.RawDelta = Time.Delta = 0f;
		camera.Update();

		LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );
		Time.RawDelta = Time.Delta = 0.05f;
		camera.Update();

		var wantedEye = island.CameraTarget + new Vector3( 70f * MathF.Sin( 0.01f ), 70f * MathF.Cos( 0.01f ), 20f );
		var eyeBlend = 0.048771f;
		var lookBlend = 0.095163f;

		AssertNear( new Vector3( 300f, 650f, 60f ) + ((wantedEye - new Vector3( 300f, 650f, 60f )) * eyeBlend),
			GetStatic( "_position" ), 0.01f, "one frame's ease from the flight's eye" );
		AssertNear( new Vector3( 350f, 400f, 30f ) + ((island.CameraTarget - new Vector3( 350f, 400f, 30f )) * lookBlend),
			GetStatic( "_lookAt" ), 0.01f, "and from the flight's look point" );
	}

	/// <summary>
	/// <b>The first orbit pass after a build leaves the seed by the original's law.</b> A build that took 2 s: the step is
	/// capped at 0.5 s, so the orbit turns 0.5 x 0.2 = 0.1 rad, the eye closes min(1, 1 x 0.5) = half the gap and the look
	/// point min(1, 2 x 0.5) = all of it, linearly (the exponential of the same rates would close 0.393 and 0.632). A
	/// second pass is an ordinary frame's, not capped.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the step not capped (0.1 s clamp, or the raw 2 s); the blend exponential rather than linear; the look
	/// blend at the eye's rate; the cap at 5 rather than 0.5; the first pass not marked by the build.
	/// </remarks>
	[TestMethod]
	public void TheFirstOrbitPassAfterABuildLeavesTheSeedByTheLaw()
	{
		var camera = ACamera( out var island );
		LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );

		LobbyCameraMode.BeginLobby();

		Time.RawDelta = 2f;
		Time.Delta = 0.1f;
		camera.Update();

		Assert.AreEqual( 0.1f, LobbyCameraMode.DebugOrbit, 0.0001f, "0.5 s at 0.2 rad a second" );

		// The seed (500, 500, 100) towards the orbit's place at 0.1 rad: half the way for the eye.
		var wantedEye = island.CameraTarget + new Vector3( 70f * MathF.Sin( 0.1f ), 70f * MathF.Cos( 0.1f ), 20f );
		AssertNear( new Vector3( 500f, 500f, 100f ) + ((wantedEye - new Vector3( 500f, 500f, 100f )) * 0.5f),
			GetStatic( "_position" ), 0.01f, "half the eye's gap" );
		AssertNear( island.CameraTarget, GetStatic( "_lookAt" ), 0.01f, "the whole of the look point's" );

		// The next pass is an ordinary frame: 0.1 s clamps to 0.1, the orbit moves 0.02 rad.
		camera.Update();

		Assert.AreEqual( 0.12f, LobbyCameraMode.DebugOrbit, 0.0001f, "a second pass takes the frame's own delta" );
	}

	/// <summary>
	/// <b>A build that took 0.2 s steps 0.2 s.</b> The cap is a ceiling: the orbit turns 0.04 rad and the eye closes 0.2 of
	/// its gap, the look point 0.4.
	/// </summary>
	/// <remarks><b>Mutations:</b> the step always the cap; the step always 0.1 s.</remarks>
	[TestMethod]
	public void AShortFirstPassTakesItsOwnLength()
	{
		var camera = ACamera( out var island );
		LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );

		LobbyCameraMode.BeginLobby();

		Time.RawDelta = 0.2f;
		Time.Delta = 0.1f;
		camera.Update();

		Assert.AreEqual( 0.04f, LobbyCameraMode.DebugOrbit, 0.0001f );

		var wantedEye = island.CameraTarget + new Vector3( 70f * MathF.Sin( 0.04f ), 70f * MathF.Cos( 0.04f ), 20f );
		AssertNear( new Vector3( 500f, 500f, 100f ) + ((wantedEye - new Vector3( 500f, 500f, 100f )) * 0.2f),
			GetStatic( "_position" ), 0.01f, "a fifth of the eye's gap" );
		AssertNear( new Vector3( 500f, 500f, 0f ) + ((island.CameraTarget - new Vector3( 500f, 500f, 0f )) * 0.4f),
			GetStatic( "_lookAt" ), 0.01f, "two fifths of the look point's" );
	}

	/// <summary>
	/// <b>The flight a player starts by Select New Player takes off from where the orbit stood.</b> A lobby entered with a
	/// player rolls no flight until the orbit's first pass; the orbit then copies its eye and look point into the flight's
	/// (<c>0x005e07a0</c>..<c>0x005e07c7</c>), and the attract pass that follows must not roll them again.
	/// </summary>
	/// <remarks><b>Mutations:</b> the reverse copy taken out (the flight stands at a rolled place); the roll left to
	/// overwrite the copy; the copy of the look point alone.</remarks>
	[TestMethod]
	public void ASelectNewPlayerFlightStartsWhereTheOrbitStood()
	{
		var camera = ACamera( out _ );
		LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );

		LobbyCameraMode.BeginLobby();
		Time.RawDelta = Time.Delta = 0.05f;
		camera.Update();
		camera.Update();

		var eye = GetStatic( "_position" );
		var look = GetStatic( "_lookAt" );

		// Nobody playing, no time passing: the flight's pass copies the flight's eye over the island camera's.
		LobbyCountedGapsTests.SetCurrentPlayer( null );
		Time.RawDelta = Time.Delta = 0f;
		camera.Update();

		AssertNear( eye, GetStatic( "_position" ), 0.001f, "the flight stands at the orbit's eye" );
		AssertNear( look, GetStatic( "_lookAt" ), 0.001f, "looking where the orbit looked" );
	}

	/// <summary>
	/// <b>The console's held flight stands still and a pick out of it eases from exactly that state (Q920).</b> The flight is held at
	/// the eye and look point desk3's C1 pick started from (479.466, 86.613, 363.595) and (575.835, 15.470, 392.691) in the original's
	/// y-up axes, ours z-up. Twenty frames pass and neither point has moved (the flight would step 0.5 units a frame); a player
	/// is then picked and one frame of 0.05 s closes 0.04877 of the eye's gap and 0.09516 of the look point's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the hold not applied; the hold applied but the flight still stepping; the look point left out of the hold.</remarks>
	[TestMethod]
	public void AHeldFlightStandsStillAndAPickEasesFromIt()
	{
		var camera = ACamera( out var island );
		island.Position = new Vector3( 400f, 400f, 0f );
		var eye = new Vector3( 479.466f, 363.595f, 86.613f );
		var look = new Vector3( 575.835f, 392.691f, 15.470f );

		LobbyCameraMode.BeginLobby();
		LobbyCameraMode.DebugFlightHold = (eye, look);
		try
		{
			Time.RawDelta = Time.Delta = 0.05f;

			for ( var i = 0; i < 20; i++ )
				camera.Update();

			AssertNear( eye, GetStatic( "_position" ), 0.001f, "the held flight's eye has not moved" );
			AssertNear( look, GetStatic( "_lookAt" ), 0.001f, "nor its look point" );

			LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );
			camera.Update();

			var wantedEye = island.CameraTarget + new Vector3( 70f * MathF.Sin( 0.01f ), 70f * MathF.Cos( 0.01f ), 20f );
			AssertNear( eye + ((wantedEye - eye) * 0.048771f), GetStatic( "_position" ), 0.01f, "one frame's ease from the held eye" );
			AssertNear( look + ((island.CameraTarget - look) * 0.095163f), GetStatic( "_lookAt" ), 0.01f, "and from the held look point" );
		}
		finally
		{
			LobbyCameraMode.DebugFlightHold = null;
		}
	}

	/// <summary>
	/// <b>A pick from that held state swings the view over near-empty water, as the original's does (Q920).</b> The same state, Lost
	/// Kingdom at (400, 400) with the shipped orbit (70, 20): at 30 frames a second the view's pitch below the horizon rises from 35 to a
	/// peak and falls back, because the look point (twice the eye's rate) reaches the island before the eye has come down. Measured
	/// on the desk, the original from this state: peak 74.2 degrees, over 45 degrees for 0.87 s; ours: 72.9 and 0.94 s
	/// (<c>docs/exe/lobby.md</c>, "The pick's swing"). The bounds are the two games' numbers with a frame's room.
	/// </summary>
	/// <remarks><b>Mutations:</b> the look point eased at the eye's rate; the eye eased at the look point's rate; the look point
	/// jumped to the island whole.</remarks>
	[TestMethod]
	public void APickFromTheHeldFlightSwingsOverWaterAsTheOriginalsDoes()
	{
		var camera = ACamera( out var island );
		island.Position = new Vector3( 400f, 400f, 0f );

		LobbyCameraMode.BeginLobby();
		LobbyCameraMode.DebugFlightHold = (new Vector3( 479.466f, 363.595f, 86.613f ), new Vector3( 575.835f, 392.691f, 15.470f ));
		try
		{
			Time.RawDelta = Time.Delta = 1f / 30f;
			camera.Update();
			LobbyCountedGapsTests.SetCurrentPlayer( new Player( 0, "Test", new PlayerFile() ) );

			float peak = 0f, firstOver = -1f, lastOver = -1f;

			for ( var frame = 1; frame <= 90; frame++ )
			{
				camera.Update();

				var eye = GetStatic( "_position" );
				var look = GetStatic( "_lookAt" );
				var pitch = MathF.Atan2( eye.Z - look.Z, MathF.Sqrt( ((eye.X - look.X) * (eye.X - look.X)) + ((eye.Y - look.Y) * (eye.Y - look.Y)) ) ) * 180f / MathF.PI;
				var t = frame / 30f;

				peak = MathF.Max( peak, pitch );

				if ( pitch > 45f )
				{
					if ( firstOver < 0f )
						firstOver = t;

					lastOver = t;
				}
			}

			Assert.IsTrue( peak is > 66f and < 80f, $"the swing peaks between the games' 72.9 and 74.2 degrees, not {peak}" );
			Assert.IsTrue( firstOver is > 0.05f and < 0.3f, $"the pitch passes 45 degrees at about +0.12 to +0.14 s, not {firstOver}" );
			Assert.IsTrue( lastOver - firstOver is > 0.7f and < 1.1f, $"and stays over it for 0.87 to 0.94 s, not {lastOver - firstOver}" );
		}
		finally
		{
			LobbyCameraMode.DebugFlightHold = null;
		}
	}

	/// <summary>
	/// An island on a stand: at (600, 400, 0), its camera target 12.5 above. The camera reads it as the lobby's only island
	/// and reads the settings the shipped lobby.txt gives (spin radius 70, vertical offset 20, field of view 100).
	/// </summary>
	private static LobbyCameraMode ACamera( out LobbyIsland island )
	{
		var script = (LobbyScript)RuntimeHelpers.GetUninitializedObject( typeof( LobbyScript ) );
		LobbyCountedGapsTests.SetField( script, "<CameraHeight>k__BackingField", 12.5f );
		LobbyCountedGapsTests.SetField( script, "<Index>k__BackingField", 0 );

		island = (LobbyIsland)RuntimeHelpers.GetUninitializedObject( typeof( LobbyIsland ) );
		LobbyCountedGapsTests.SetField( island, "<Script>k__BackingField", script );
		island.Position = new Vector3( 600f, 400f, 0f );

		var camera = new LobbyCameraMode();
		LobbyCountedGapsTests.SetField( camera, "_islands", new List<LobbyIsland> { island } );
		LobbyCountedGapsTests.SetField( camera, "_settings", new LobbyCameraMode.CameraSettings( 70f, 20f, 100f ) );

		return camera;
	}

	private static Vector3 GetStatic( string name )
		=> (Vector3)typeof( LobbyCameraMode ).GetField( name, BindingFlags.Static | BindingFlags.NonPublic )!.GetValue( null )!;

	private static void SetStatic( string name, object value )
		=> typeof( LobbyCameraMode ).GetField( name, BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, value );

	private static void AssertNear( Vector3 expected, Vector3 actual, float within, string what )
		=> Assert.IsTrue( (expected - actual).Length <= within, $"{what}: expected {expected}, got {actual}" );
}
