using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// Escape during the park-entry flight cancels it, as the original's island camera does (<c>0x005e1890</c>): the camera
/// goes back to orbiting from where the flight had turned it, a gate that the flight opened is shut again, the island
/// panel comes back, and no game menu opens. See <see cref="LobbyCameraMode.CancelLeave"/>, <see cref="LobbyGate"/> and
/// <c>docs/exe/lobby.md</c>, "Escape cancels the fly-in".
///
/// <para>
/// <b>The numbers are written out, not read from the code under test.</b> The homing turn is 0.5 radians a second and
/// every gate clip here is played over the span its file declares, 60 frames at 30 a second for the jungle's.
/// </para>
/// <para>
/// <b>What these cannot see.</b> No test builds a lobby, so the island, its gate and the front end are stand-ins made
/// without their constructors, holding the real clips out of the real archive where one is needed. The stack handing
/// Escape to the front end is stepped by <see cref="LobbyKeysOnReleaseTests"/>; the front end being in the lobby
/// at all rests on the game run.
/// </para>
/// </summary>
[TestClass]
public class LobbyEscapeTests
{
	private const float Frame = 1f / 60f;

	private float _delta;

	[TestInitialize]
	public void StartInOrbit()
	{
		// A cancel is logged, and run alone this class is the first to want a logger.
		Log ??= new();

		_delta = Time.Delta;
		Time.Delta = Frame;

		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.Paused = false;
		LobbyCameraMode.DebugOrbit = 2.6f;
	}

	[TestCleanup]
	public void PutTheLobbyBack()
	{
		LobbyCameraMode.ForgetIsland();
		LobbyCameraMode.DebugOrbit = 0f;
		SetCurrentPlayer( null );

		Time.Delta = _delta;
	}

	/// <summary>In orbit Escape is not the camera's: it answers false, so the menu opens, and changes nothing.</summary>
	/// <remarks><b>Mutations:</b> answering true whatever the state keeps the game menu shut for good.</remarks>
	[TestMethod]
	public void InOrbitEscapeIsNotTheCameras()
	{
		Assert.IsFalse( LobbyCameraMode.CancelLeave(), "nothing to cancel" );
		Assert.AreEqual( "leave=No angle=0.000 radius=0.00 vertical=0.00 waiting=False", LobbyCameraMode.LeaveDescription() );
		Assert.AreEqual( 2.6f, LobbyCameraMode.DebugOrbit, 0.0001f, "and the orbit is where it was" );
	}

	/// <summary>
	/// <b>Escape while the camera is still swinging round puts it back in orbit from where it had turned to</b> - the
	/// original's orbit and its homing are one field - with no park waiting, and the island keys and a new Enter taken.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> leaving the leave state, the park still waiting, or the orbit angle alone in
	/// <see cref="LobbyCameraMode.CancelLeave"/> each turns this red.
	/// </remarks>
	[TestMethod]
	public void EscapeWhileSwingingRoundGoesBackToOrbitingFromThere()
	{
		var asked = 0;
		LobbyCameraMode.LeaveForPark( () => ++asked );

		// 0.5 rad/s for a third of a second: 2.6 turns toward pi by 1/6 of a radian.
		Step( 20 );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing angle=2.767 ", "still homing" );

		Assert.IsTrue( LobbyCameraMode.CancelLeave(), "a leave to cancel, so the menu stays shut" );

		Assert.AreEqual( "leave=No angle=0.000 radius=0.00 vertical=0.00 waiting=False", LobbyCameraMode.LeaveDescription() );
		Assert.AreEqual( 2.6f + (20 * Frame * 0.5f), LobbyCameraMode.DebugOrbit, 0.001f,
			"the orbit carries on from the angle the leave had turned it to, not the one it left from" );
		Assert.IsTrue( LobbyCameraMode.Step( 1 ), "the island keys are taken again" );
		Assert.IsFalse( LobbyCameraMode.CancelLeave(), "and a second Escape is the menu's" );

		LobbyCameraMode.LeaveForPark( () => ++asked );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing angle=2.767 ",
			"a new Enter starts a new flight from there" );

		Assert.AreEqual( 0, asked, "no park was asked for" );
	}

	/// <summary>
	/// <b>Escape while flying in cancels it, and the orbit carries on from the gate side</b>, with no park waiting;
	/// the next Enter then needs no turn at all, flies straight in, and asks for its park once.
	/// </summary>
	/// <remarks><b>Mutations:</b> leaving the orbit angle alone in <see cref="LobbyCameraMode.CancelLeave"/> turns this red.</remarks>
	[TestMethod]
	public void EscapeWhileFlyingInGoesBackToOrbitingFromTheGateSide()
	{
		var first = 0;
		LobbyCameraMode.LeaveForPark( () => ++first );

		// 0.542 rad at 0.5 a second is 65 frames of turning, and the 66th finds it there; four more fly in.
		Step( 70 );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=FlyingIn angle=3.142 ", "flying in" );

		Assert.IsTrue( LobbyCameraMode.CancelLeave() );
		Assert.AreEqual( "leave=No angle=0.000 radius=0.00 vertical=0.00 waiting=False", LobbyCameraMode.LeaveDescription() );
		Assert.AreEqual( MathF.PI, LobbyCameraMode.DebugOrbit, 0.001f, "the orbit carries on from the gate side" );

		var second = 0;
		LobbyCameraMode.LeaveForPark( () => ++second );
		Step( 1 );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=FlyingIn ", "already facing the gate" );

		// ln(70/8) / 0.7 is 3.1 seconds of flying in.
		Step( 200 );
		Assert.AreEqual( 1, second, "the new Enter reaches its park, once" );
		Assert.AreEqual( 0, first, "and the cancelled one never did" );
	}

	/// <summary>
	/// <b>The flight opens the gate as the camera faces it, and Escape from then on shuts it</b>: M1 once at the
	/// homing's arrival, M2 once on the cancel - queued behind M1, which is still opening, as the original's channel
	/// queues it. Escape before the arrival plays nothing, because nothing has opened.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> dropping the <c>Open</c> at the arrival, opening on every frame of the fly-in rather than once,
	/// dropping the <c>Shut</c> in the cancel, or the state test in front of it each turns this red.
	/// </remarks>
	[TestMethod]
	public void TheFlightOpensTheGateAndEscapeShutsIt()
	{
		var island = AnIsland( out var gate );

		LobbyCameraMode.LeaveForPark( () => { } );
		Step( 20 );
		Assert.IsTrue( LobbyCameraMode.CancelLeave() );
		Assert.AreEqual( "shut", gate.Describe(), "cancelled while swinging round: the gate never opened, and is not shut" );

		LobbyCameraMode.DebugOrbit = 2.6f;
		LobbyCameraMode.LeaveForPark( () => { } );
		Step( 65 );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "65 frames turn it, and it has not seen so" );
		Assert.AreEqual( "shut", gate.Describe(), "still swinging round" );

		Step( 1 );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=FlyingIn ", "the 66th finds it has arrived" );
		Assert.AreEqual( "M1,playing,0.00/2.00", gate.Describe(), "and the gate starts to open" );

		// The flight goes on while the gate plays, and asks nothing more of it: M1 is played once.
		for ( var i = 0; i < 30; ++i )
		{
			Step( 1 );
			PlayGate( gate, 1 );
		}

		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=FlyingIn ", "still flying in" );
		Assert.AreEqual( "M1,playing,0.50/2.00", gate.Describe(), "half a second into opening, with nothing queued" );

		Assert.IsTrue( LobbyCameraMode.CancelLeave() );
		Assert.AreEqual( "M1,playing,0.50/2.00,then=M2", gate.Describe(), "M2 waits for M1 to finish opening" );

		PlayGate( gate, 100 );
		StringAssert.StartsWith( gate.Describe(), "M2,playing,", "then shuts" );

		PlayGate( gate, 120 );
		Assert.AreEqual( "M2,held,2.00/2.00", gate.Describe(), "and stays shut" );

		GC.KeepAlive( island );
	}

	/// <summary>
	/// <b>A gate plays each clip once over the span it declares, and starts one asked for while it is idle at
	/// once</b>: hallow's M1 declares 60 frames though its keys run to 600, and fantasy's and space's declare 100.
	/// </summary>
	/// <remarks><b>Mutations:</b> timing the clip by its keys rather than its declared span turns this red.</remarks>
	[TestMethod]
	public void AGateClipLastsTheSpanItDeclares()
	{
		foreach ( var (prefix, seconds) in new[] { ("Jun", 2f), ("Hal", 2f), ("Fan", 100f / 30f), ("Spa", 100f / 30f) } )
		{
			var clips = GateClips( prefix );

			Assert.AreEqual( seconds, LobbyGate.PlaySeconds( clips[0] ), 0.001f, $"{prefix}_gateM1" );
			Assert.AreEqual( seconds, LobbyGate.PlaySeconds( clips[1] ), 0.001f, $"{prefix}_gateM2" );
		}

		var gate = AGate( GateClips( "Hal" ) );

		gate.Shut();
		Assert.AreEqual( "M2,playing,0.00/2.00", gate.Describe(), "idle, so M2 starts at once" );

		// Two seconds is 120 sixtieths; asked a few frames either side, so the boundary's rounding is not under test.
		PlayGate( gate, 117 );
		StringAssert.StartsWith( gate.Describe(), "M2,playing,", "not two seconds yet" );
		PlayGate( gate, 6 );
		Assert.AreEqual( "M2,held,2.00/2.00", gate.Describe(), "held on its last frame" );

		gate.Open();
		Assert.AreEqual( "M1,playing,0.00/2.00", gate.Describe(), "and a held gate starts the next clip at once" );
	}

	/// <summary>A gate whose model ships no second clip plays nothing when shut - our guard; no shipped gate lacks M2.</summary>
	[TestMethod]
	public void AGateWithNoShuttingClipPlaysNothing()
	{
		var gate = AGate( GateClips( "Jun" ).Take( 1 ).ToArray() );

		gate.Shut();
		Assert.AreEqual( "shut", gate.Describe() );
	}

	/// <summary>
	/// <b>The gate is posed at the clip's own frame</b>, 30 a second from its declared start: half a second into M1
	/// is frame 15, and once the clip has ended it rests on frame 60. Checked against the same rotator posed
	/// directly, over the jungle gate's real clips and two stand-in doors.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> dropping the pose, or posing at the frame rate rather than 30 a second, turns this red. A
	/// pose skipped on the clip's last frame is not seen: the jungle's doors have stopped by frame 50.
	/// </remarks>
	[TestMethod]
	public void TheGateIsPosedAtTheClipsOwnFrame()
	{
		var clips = GateClips( "Jun" );
		var (rotator, doors) = ARotator( clips );
		var (reference, expected) = ARotator( clips );
		var gate = AGate( clips, rotator );

		Assert.IsTrue( doors.All( door => door.LinearTransform == null ), "nothing is posed before a clip plays" );

		gate.Open();
		PlayGate( gate, 30 );
		reference.Pose( clips[0], 15f );
		AssertPosed( expected, doors, "half a second into M1 is its frame 15" );

		PlayGate( gate, 123 );
		reference.Pose( clips[0], 60f );
		AssertPosed( expected, doors, "an ended M1 rests on frame 60" );
	}

	/// <summary>
	/// <b>With nobody playing there is no island panel to give back</b>: the debug console's flight is cancelled by
	/// Escape like any other, and the lobby is left showing what it showed - here nothing - with no menu. In orbit the
	/// same key then opens the menu.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> giving the panel back whoever is playing, or the camera answering Escape in orbit, turns this
	/// red.
	/// </remarks>
	[TestMethod]
	public void EscapeDuringAFlightWithNobodyPlayingOpensNoPanel()
	{
		FileSystem = GameData.Required();

		var (frontEnd, stack, _) = ALobbyFrontEnd();

		LobbyCameraMode.LeaveForPark( () => { } );
		Step( 70 );
		Escape( frontEnd );

		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "Escape cancelled the flight" );
		Assert.AreEqual( "", Names( stack ), "no panel with nobody playing, and no menu" );

		Escape( frontEnd );
		Assert.AreEqual( "GameMenu", Names( stack ), "in orbit, Escape opens the game menu" );
	}

	/// <summary>
	/// <b>Escape through the lobby front end's own handler, during the flight</b>: the flight is cancelled, the island
	/// panel is open again, and no game menu is - where in orbit the same key opens the menu. Then the panel's Enter
	/// this park takes the player straight back into a flight.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> taking the camera out of <c>FrontEnd.MenuKey</c> opens the menu over the flight; not giving
	/// the panel back leaves no way in; and a panel guarding Enter with a flag of its own, set as it closes, refuses
	/// the second Enter.
	/// </remarks>
	[TestMethod]
	public void EscapeDuringTheFlightGivesThePanelBackAndOpensNoMenu()
	{
		FileSystem = GameData.Required();

		var (frontEnd, stack, panel) = ALobbyFrontEnd();
		AnIsland( out _ );
		SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		Enter( stack, panel );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "Enter this park started the flight" );
		Assert.AreEqual( "", Names( stack ), "and closed the panel" );

		Step( 70 );
		Escape( frontEnd );

		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=No ", "Escape cancelled the flight" );
		Assert.AreEqual( "IslandPanel", Names( stack ), "the panel is back, and no menu opened" );

		Enter( stack, panel );
		StringAssert.StartsWith( LobbyCameraMode.LeaveDescription(), "leave=Homing ", "the panel's Enter enters again" );

		Escape( frontEnd );
		Assert.AreEqual( "IslandPanel", Names( stack ), "a second flight is cancelled the same way" );

		// The positive control: in orbit, the same key opens the menu over the panel.
		Escape( frontEnd );
		Assert.AreEqual( "IslandPanel, GameMenu", Names( stack ), "in orbit, Escape opens the game menu" );
	}

	private static void Step( int frames )
	{
		for ( var i = 0; i < frames; ++i )
			LobbyCameraMode.StepLeaving( new LobbyCameraMode.CameraSettings( 70f, 20f ) );
	}

	private static void PlayGate( LobbyGate gate, int frames )
	{
		for ( var i = 0; i < frames; ++i )
			gate.Update();
	}

	private static AnimationFile[] GateClips( string prefix )
	{
		FileSystem = GameData.Required();

		return LobbyModel.LoadAnimations( $"lobby/terrain/{prefix}_gate.md2" );
	}

	/// <summary>
	/// A gate made without its constructor around a model made without its own, holding the real clips and, where one
	/// is given, a rotator to pose. Without one nothing is posed, which the clip timing does not need.
	/// </summary>
	private static LobbyGate AGate( AnimationFile[] clips, MeshRotator? rotator = null )
	{
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		SetBacking( model, "Clips", clips );
		SetBacking( model, "Animators", Array.Empty<MeshAnimator>() );
		SetBacking( model, "Rotator", rotator );

		var gate = (LobbyGate)RuntimeHelpers.GetUninitializedObject( typeof( LobbyGate ) );
		SetField( gate, "_model", model );
		SetField( gate, "_clip", -1 );
		SetField( gate, "_queued", -1 );

		return gate;
	}

	/// <summary>
	/// A real rotator over the clips, turning stand-in meshes that have no parents and stand at the origin untransformed
	/// - enough for every track's target, and nothing that needs a device.
	/// </summary>
	private static (MeshRotator Rotator, ModelEntity[] Meshes) ARotator( AnimationFile[] clips )
	{
		var count = clips.SelectMany( clip => clip.RotationTracks ).Max( track => track.TargetIndex ) + 1;
		var meshes = Enumerable.Range( 0, count )
			.Select( _ => (ModelEntity)RuntimeHelpers.GetUninitializedObject( typeof( ModelEntity ) ) ).ToArray();
		var identity = Enumerable.Repeat( System.Numerics.Matrix4x4.Identity, count ).ToArray();

		var rotator = new MeshRotator( clips, meshes, identity, identity, new Vector3[count], Enumerable.Repeat( -1, count ).ToArray() );

		return (rotator, meshes);
	}

	private static void AssertPosed( ModelEntity[] expected, ModelEntity[] actual, string what )
	{
		for ( var i = 0; i < expected.Length; ++i )
		{
			Assert.IsNotNull( actual[i].LinearTransform, $"{what}: mesh {i} was posed" );

			var e = expected[i].LinearTransform!.Value;
			var a = actual[i].LinearTransform!.Value;

			Assert.IsTrue( MathF.Abs( e.M11 - a.M11 ) < 1e-4f && MathF.Abs( e.M13 - a.M13 ) < 1e-4f
				&& MathF.Abs( e.M31 - a.M31 ) < 1e-4f && MathF.Abs( e.M22 - a.M22 ) < 1e-4f
				&& MathF.Abs( e.M12 - a.M12 ) < 1e-4f && MathF.Abs( e.M21 - a.M21 ) < 1e-4f,
				$"{what}: mesh {i} is posed as the rotator poses that frame" );
		}
	}

	/// <summary>The jungle's island made current, holding a stand-in gate with the jungle gate's real clips.</summary>
	private static LobbyIsland AnIsland( out LobbyGate gate )
	{
		gate = AGate( GateClips( "Jun" ) );

		var island = (LobbyIsland)RuntimeHelpers.GetUninitializedObject( typeof( LobbyIsland ) );
		SetBacking( island, "Gate", gate );
		SetBacking( island, "ParkName", "Lost Kingdom" );
		SetBacking( island, "ThemeName", "Jungle" );

		typeof( LobbyCameraMode ).GetProperty( nameof( LobbyCameraMode.CurrentIsland ) )!
			.SetValue( null, island );

		return island;
	}

	/// <summary>The lobby's front end over a stack of its own, with a real island panel - the three a lobby builds.</summary>
	private static (UI.FrontEnd FrontEnd, UI.WindowStack Stack, UI.IslandPanel Panel) ALobbyFrontEnd()
	{
		var stack = (UI.WindowStack)RuntimeHelpers.GetUninitializedObject( typeof( UI.WindowStack ) );
		SetField( stack, "_windows", new List<UI.UiWindow>() );

		var panel = new UI.IslandPanel( stack );

		var frontEnd = (UI.FrontEnd)RuntimeHelpers.GetUninitializedObject( typeof( UI.FrontEnd ) );
		SetField( frontEnd, "_stack", stack );
		SetField( frontEnd, "_islandPanel", panel );

		return (frontEnd, stack, panel);
	}

	/// <summary>Escape with no box to type into, through the lobby front end's own handler, as the stack hands it over.</summary>
	private static void Escape( UI.FrontEnd frontEnd )
	{
		var stack = (UI.WindowStack)typeof( UI.FrontEnd ).GetField( "_stack", BindingFlags.Instance | BindingFlags.NonPublic )!
			.GetValue( frontEnd )!;

		typeof( UI.FrontEnd ).GetMethod( "MenuKey", BindingFlags.Instance | BindingFlags.NonPublic )!
			.Invoke( frontEnd, [stack.Windows.Count > 0 ? stack.Windows[^1] : null] );
	}

	/// <summary>The panel's Enter this park, as its button runs it - opened first, as the lobby shows it.</summary>
	private static void Enter( UI.WindowStack stack, UI.IslandPanel panel )
	{
		stack.Open( panel );
		panel.ShowKeys();

		typeof( UI.IslandPanel ).GetMethod( "EnterPark", BindingFlags.Instance | BindingFlags.NonPublic )!
			.Invoke( panel, [] );
	}

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	private static void SetBacking( object target, string property, object? value )
		=> SetField( target, $"<{property}>k__BackingField", value );

	private static void SetField( object target, string field, object? value )
	{
		for ( var type = target.GetType(); type != null; type = type.BaseType )
		{
			if ( type.GetField( field, BindingFlags.Instance | BindingFlags.NonPublic ) is { } info )
			{
				info.SetValue( target, value );
				return;
			}
		}

		throw new MissingFieldException( target.GetType().Name, field );
	}
}
