using System.Globalization;

namespace OpenTPW;

/// <summary>
/// The lobby's camera, which has <b>two modes</b>, as the original's does.
///
/// <para>
/// <b>With somebody playing</b> it orbits whichever park island is on show and moves between them,
/// which is what the rest of this class is about. <b>With nobody playing</b> it flies itself around
/// all four islands instead - see <see cref="Attract"/>. The original branches between the two in one
/// function on one condition (<c>FUN_005e0470</c>), and so does <see cref="Update"/>.
/// </para>
///
/// This follows what the original does, which was read out of its own camera update
/// (FUN_005e1210 in the decompile). It never moves the camera to a place; it works out where the
/// camera ought to be this frame - the island, plus the orbit offset - and then eases the camera
/// and its aim towards those separately:
///
///     position += (wantedPosition - position) * delta * 0.1
///     lookAt   += (wantedLookAt   - lookAt)   * delta * 0.2
///
/// That is a first-order lag, so the speed is set by how far there is left to go: quickest the
/// moment the island changes, easing down as it arrives. The aim closes at twice the rate of the
/// camera body, so the shot settles on the new island while the camera is still travelling.
///
/// Nothing in there is a transition - there is no arrival, no duration and no second code path.
/// Selecting another island only changes what the two are chasing, which is also why the orbit
/// carries on turning straight through a move.
///
/// Engine and content: the lobby's own camera, with its settings read from lobby.txt at the boundary;
/// a park's camera is a mode of its own (ParkOrbitCameraMode).
/// </summary>
public class LobbyCameraMode : CameraMode
{
	/// <summary>
	/// How fast the camera and its aim close on where they are headed, per second.
	///
	/// The original's 0.1 and 0.2 (0x00702c7c and 0x00702ca4, in FUN_005e1210) are not per frame:
	/// they multiply the lobby's delta, which is milliseconds times a hundredth, so a second of it
	/// comes to ten - see <see cref="LobbyScript.TicksPerSecond"/>. The rates are therefore
	/// 10 * 0.1 = 1.0 a second for the camera body and 10 * 0.2 = 2.0 for its aim.
	///
	/// Because the original already scales by its delta, those are its rates at any frame rate
	/// rather than at one assumed one, and its per-frame multiply is the first-order approximation
	/// of the curve <see cref="Time.SmoothingFactor"/> gives exactly. The 2:1 ratio is what settles
	/// the shot on the new island while the camera is still travelling.
	/// </summary>
	private const float PositionRate = 1f;
	private const float LookAtRate = 2f;

	/// <summary>
	/// How fast the orbit turns, in radians per second.
	///
	/// The script asks for SPINSPEED(0.02), which is per lobby tick, and the lobby ticks ten times a
	/// second - see <see cref="LobbyScript.TicksPerSecond"/> - so the orbit turns 0.2 radians a
	/// second and comes round in a little over half a minute. The original's own advance is the same
	/// arithmetic: its lobby tick (FUN_005e0470, in the resting state) reads SPINSPEED out of the
	/// settings block every frame, does <c>angle += delta * SPINSPEED</c> and wraps it against 2pi.
	/// </summary>
	private const float SpinSpeed = 0.2f;

	/// <summary>
	/// The box the camera wanders inside while nobody is playing, from the lobby object's constructor
	/// (<c>FUN_005dfcd0</c>): centre (500, 75, 500) with extents (400, 50, 400). Those are the
	/// <b>full</b> sizes rather than half-extents - the original rolls a point as
	/// <c>centre + rand * extent - extent / 2</c>.
	///
	/// <para>
	/// <b>The middle component is the vertical.</b> The original is Y-up and this world is Z-up, so in this
	/// world's axes the original's centre is (500, 500, 75) and its extents (400, 400, 50): its box spans 300 to 700
	/// across the lobby and 50 to 100 above it. That is the lobby's own geometry - the four islands
	/// stand at 400 and 600 in both directions, centred on (500, 500) - which is what says the axes
	/// have been read the right way round rather than transposed.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>A deliberate deviation, at Alexah's word: the box's height band is lowered.</b> The original's box
	/// is <c>centre (500, 75, 500)</c> with extents <c>(400, 50, 400)</c>, which in this world's axes is centre
	/// <c>(500, 500, 75)</c> and extents <c>(400, 400, 50)</c> - X and Z spanning 300 to 700, height 50 to 100 -
	/// and a flight inside it keeps a median of <b>101.5 units</b> from the nearest island, at most 143.5. The aim
	/// mechanism, the speeds, the arrival radii and both eases are all the original's and stay that way; only
	/// the box moves.
	///
	/// <para>
	/// <b>The lever is the HEIGHT, not the width.</b> The islands stand at the CORNERS of the square, (400,400),
	/// (600,400), (600,600) and (400,600), so the original's 300-700 box already centres each island in its own
	/// quadrant, and pulling the box in toward (500,500) moves the camera toward the one point farthest from all
	/// four - 141 units from each: a box narrowed to 360-640 flies a median of 104.3, farther than 101.5. Its
	/// horizontal median is 82.6, so about 64 units of that distance is vertical. So the horizontal box is the
	/// original's and only the band is lowered, from 50-100 to <b>30-60</b>. The islands' own camera targets sit
	/// 12.5 (jungle) to 38 (hallow) above them, so that leaves roughly 20 units of height over the thing being
	/// looked at, and brings the distance to about <see cref="NominalDistance"/> - the
	/// <c>sqrt(SPINRADIUS^2 + VERTICALOFFSET^2)</c> the lobby is composed at.
	/// </para>
	/// </remarks>
	private static readonly Vector3 WanderCentre = new( 500f, 500f, 45f );

	/// <summary>The full size of <see cref="WanderCentre"/>'s box - see there.</summary>
	private static readonly Vector3 WanderExtent = new( 400f, 400f, 30f );

	/// <summary>
	/// How fast the camera flies, in units a second. The constructor keeps <b>1.0</b>, which is per
	/// lobby tick, and the lobby ticks ten times a second - see
	/// <see cref="LobbyScript.TicksPerSecond"/>.
	/// </summary>
	private const float WanderSpeed = 10f;

	/// <summary>
	/// How close the camera comes to its target before a new one is rolled. The constructor keeps
	/// <b>100.0</b> and the original tests it against a <b>squared</b> distance, so the radius is ten.
	/// </summary>
	private const float ArrivalRadius = 10f;

	/// <summary>
	/// How fast the aim point chases the island it has picked, in units a second, and how close it gets
	/// before it slows. The constructor keeps a cap of <b>2.0</b> a tick - twenty a second - and a
	/// threshold of <b>50.0</b>, squared, so about 7.07.
	/// </summary>
	private const float LookSpeedCap = 20f;

	/// <summary>How near the aim point has to be before it starts slowing - see <see cref="LookSpeedCap"/>.</summary>
	private const float LookArrivalRadius = 7.0710678f;

	/// <summary>
	/// How much of <see cref="LookSpeedCap"/> the aim point gains or loses each second as it ramps up
	/// to full speed and back down to nought.
	///
	/// <para>
	/// <b>This is the one number here that was chosen rather than read.</b> The original adds and
	/// subtracts 0.05 of the cap with no delta at all (<c>0x00702c78</c> and <c>0x00702c84</c>), so it
	/// is per frame and runs at whatever rate the machine happened to draw - the same shape as the
	/// lightning roll. Converting it is what this project already does with per-frame rolls, knowingly
	/// and one system at a time (see <see cref="LobbyWeather"/> and <c>LobbyAudio.RollOneShot</c>), so
	/// it is converted here too, against sixty frames a second: 0.05 x 60 = 3.0 of the cap a second,
	/// which reaches full speed in about a third of a second.
	/// </para>
	/// </summary>
	private const float LookRampPerSecond = 3f;

	/// <summary>
	/// How fast a heading turns toward the bearing it wants, per second. The original eases both the
	/// flying heading and the aim's at <c>0.1 x delta</c>, which is the same 1.0 a second
	/// <see cref="PositionRate"/> already carries - the two come from the same constant.
	/// </summary>
	private const float DirectionRate = 1f;

	/// <summary>
	/// How wide a view the lobby is framed at, as the vertical angle at 4:3 that <see cref="Camera"/>
	/// takes. <b>A deliberate deviation from the file, not a derivation of it - do not "correct" this
	/// by reading ISLANDFOV.</b>
	///
	/// What the file asks for is now known. The original has two lobbies: Lobby_Start, the globe one
	/// that loads data\lobby\globe, writes the camera's field itself - MOV dword ptr [EAX + 0x4],
	/// 0x42700000, which is 60.0f, at 0x005dd034 - while IslandLobby_Start, the one that calls
	/// IslandPanel_Create and parses each park's script, copies the file's value instead: FLD [ECX+4] /
	/// FSTP [EAX+4] at 0x005e13fb, ECX being the lobby.txt settings block and +4 where FUN_005e2cc0 put
	/// ISLANDFOV. The unit is the projection's - FUN_00578be0 works out <c>half = fov * (pi/180) * 0.5</c>
	/// and fills cos(half) into m00 and m11 - so ISLANDFOV(100) is a full hundred-degree field, and
	/// taken as the horizontal angle it comes to 83.58 vertical at 4:3.
	///
	/// So this camera, which is the island lobby, would take 83.58 from the file. Alexah looked at both
	/// and chose 60, the globe lobby's number.
	/// The wider view is faithful to the file; the narrower one frames the island better, and that is the
	/// call being made. Measurement cannot break the tie: projecting the island mesh against the two
	/// reference screenshots excludes the vertical reading outright, but brackets the horizontal angle
	/// only at roughly 80 to 95 degrees and cannot separate 100 from the high eighties.
	/// </summary>
	private const float FieldOfViewDegrees = 60f;

	/// <summary>
	/// Static so it survives <see cref="Camera.SetCameraMode{T}"/> creating a fresh instance.
	/// </summary>
	public static bool Paused { get; set; }

	/// <summary>
	/// Holds the orbiting camera even with nobody playing, for DebugConsole.
	///
	/// <para>
	/// The attract flight is seeded from <see cref="RandomPointInBox"/>, and <see cref="ForgetIsland"/>
	/// clears <see cref="_wandering"/> as a lobby ends - so every lobby build rolls a fresh place to
	/// stand. <see cref="DebugSelect"/>, <see cref="DebugOrbit"/> and <see cref="DebugSettle"/> are all
	/// ignored while it is flying, because <see cref="Update"/> returns before it reads any of them.
	/// Two lobbies in one run therefore cannot be photographed from the same viewpoint, which is what
	/// this is for: the orbit branch takes its position from the island and the orbit angle alone.
	/// </para>
	///
	/// <para>
	/// Off unless a harness asks. The shipping game never sets it, and <see cref="Paused"/>'s reason for
	/// being static is this one's as well.
	/// </para>
	/// </summary>
	internal static bool DebugHoldOrbit { get; set; }

	/// <summary>
	/// Whether the lobby stays on the island it is showing, however it is asked to move. An Instant
	/// Action game is played that way: the original's next and previous island handlers (0x005e1ee0
	/// and 0x005e1f40) do nothing at all unless the game type is something other than 2, so nothing
	/// it offers - the panel's arrows, the cursor keys, or the bracket keys here - goes anywhere.
	///
	/// Set by the front end, which is what knows who is playing; static for the same reason
	/// <see cref="Paused"/> is. <see cref="SelectFirst"/> is not held by it - that is the lobby
	/// putting itself back, not something the player asked for.
	/// </summary>
	internal static bool HeldToOneIsland { get; set; }

	/// <summary>
	/// How far the camera stands from what it is looking at: the orbit's own geometry, and so the
	/// distance the lobby was composed at.
	///
	/// It is <c>sqrt(SPINRADIUS^2 + VERTICALOFFSET^2)</c> - about 72.8 with the numbers lobby.txt
	/// ships - and it lives here because this is where those two are read. The lobby's sound uses it
	/// as the distance at which a placed sound is heard at the level it was measured into, so it wants
	/// to come from the file rather than be written down again somewhere else. Seeded with the same
	/// values <see cref="Settings"/> falls back to, so it is right before the first frame as well.
	/// </summary>
	internal static float NominalDistance { get; private set; } = MathF.Sqrt( (70f * 70f) + (20f * 20f) );

	/// <summary>The island being orbited, as an index into the lobby's running order.</summary>
	private static int IslandIndex { get; set; }

	/// <summary>
	/// The island the lobby is currently showing, or null before the lobby has built any.
	///
	/// The original's lobby keeps the same thing - a pointer to the nearest island - and reads
	/// that park's weather and sky off it every frame. <see cref="LobbyWeather"/> does the same.
	/// </summary>
	public static LobbyIsland? CurrentIsland { get; private set; }

	// Accumulated separately from Time.Now so unpausing resumes where it stopped instead of
	// snapping back onto the wall-clock orbit. Static for the same reason Paused is: the orbit
	// should not restart just because a camera mode was swapped out and back.
	private static float _orbitTime;

	// Where the camera and its aim have actually got to, as opposed to where they are headed.
	private static Vector3 _position;
	private static Vector3 _lookAt;
	private static bool _placed;

	private List<LobbyIsland>? _islands;
	private CameraSettings? _settings;

	/// <summary>
	/// The lobby-wide camera settings, which live in lobby.wad's own lobby.txt rather than in any
	/// one park's script:
	///
	///     ISLANDFOV(100)  SPINSPEED(0.02)  SPINRADIUS(70)
	///     VERTICALOFFSET(20)  GLOBERADIUSOUT(475)  GLOBERADIUSIN(200)
	///
	/// The two globe radii are the camera's distance from the lobby globe zoomed out and in. The
	/// original selects an island by spinning its globe until that island's heading faces the
	/// camera and then pulling in from one radius to the other; this orbits the islands where
	/// they stand instead, so nothing reads them yet.
	/// </summary>
	internal readonly record struct CameraSettings( float SpinRadius, float VerticalOffset );

	public override void Update()
	{
		// The original's update also arms the advisor's 90-second repeat of response 394 or 395, drawn afresh, on
		// every pass while a player is picked (0x005e184c, docs/exe/ui.md, "The lobby's idle repeat"). Not built
		// (docs/QUEUE.md Q77). Counted every frame a player is picked.
		if ( Players.Roster.Current != null )
			Unimplemented.Report( "LOBBY_ADVISOR_IDLE_REPEAT" );

		var islands = Islands();

		if ( Input.Pressed( InputButton.FreezeCamera ) )
			Paused = !Paused;

		IslandKeys( Input.Pressed( InputButton.NextIsland ), Input.Pressed( InputButton.PreviousIsland ) );

		if ( islands.Count == 0 )
			return;

		var settings = Settings();

		NominalDistance = MathF.Sqrt(
			(settings.SpinRadius * settings.SpinRadius) + (settings.VerticalOffset * settings.VerticalOffset) );

		// With nobody playing the lobby flies itself instead of orbiting one island - the same branch
		// the original takes, on the same condition. Its test is that no player is selected
		// (FUN_0048bcd0's +0x60 reading -1); ours is the roster having no current player.
		// Leaving for a park takes precedence over the attract flight, and that guard is OURS rather than
		// the original's - it cannot reach this case at all, because IslandLobby_EnterPark is only
		// reachable with a player selected and the attract branch is only taken when none is. Here the
		// debug console can ask for a park entry with nobody playing, and without this the camera would
		// carry on wandering while the leave sequence never stepped: the park would never load and the
		// game would look hung rather than wrong.
		if ( Players.Roster.Current is null && !DebugHoldOrbit && _leaving == Leaving.No )
		{
			Attract( islands );

			FieldOfView = FieldOfViewDegrees;
			return;
		}

		if ( !Paused && _leaving == Leaving.No )
			_orbitTime += Time.Delta;

		CurrentIsland = islands[Math.Clamp( IslandIndex, 0, islands.Count - 1 )];

		// Leaving for a park runs the original's own two states in front of the ordinary orbit, and
		// while they run they decide the heading and the distance instead of it - see LeaveForPark.
		var angle = _leaving == Leaving.No ? _orbitTime * SpinSpeed : StepLeaving( settings );

		var radius = _leaving == Leaving.FlyingIn ? _leaveRadius : settings.SpinRadius;
		var vertical = _leaving == Leaving.FlyingIn ? _leaveVertical : settings.VerticalOffset;

		var wantedLookAt = CurrentIsland.CameraTarget;

		var wantedPosition = wantedLookAt + new Vector3(
			MathF.Sin( angle ) * radius,
			MathF.Cos( angle ) * radius,
			vertical );

		if ( _placed )
		{
			_position = _position.LerpTo( wantedPosition, Time.SmoothingFactor( PositionRate ) );
			_lookAt = _lookAt.LerpTo( wantedLookAt, Time.SmoothingFactor( LookAtRate ) );
		}
		else
		{
			// The first frame has nothing to ease from.
			_position = wantedPosition;
			_lookAt = wantedLookAt;
			_placed = true;
		}

		Position = _position;
		Rotation = Rotation.LookAt( _lookAt - _position );

		FieldOfView = FieldOfViewDegrees;
	}

	/// <summary>Which part of the leaving sequence is running - see <see cref="LeaveForPark"/>.</summary>
	private enum Leaving { No, Homing, FlyingIn }

	private static Leaving _leaving;
	private static float _leaveAngle;
	private static float _leaveRadius;
	private static float _leaveVertical;
	private static Action? _whenArrived;

	/// <summary>
	/// How fast the orbit swings round onto the gate side, in radians a second.
	///
	/// <para>
	/// The original turns by <c>delta * 0.05</c> each frame (<c>_DAT_00702c78</c>), and its delta is
	/// milliseconds times a hundredth - ten a second, see <see cref="LobbyScript.TicksPerSecond"/> - so
	/// the rate is 0.5 a second. It is a <b>constant-rate</b> turn rather than an ease: the original adds
	/// or subtracts a fixed step and stops when it is within half a step of the target, which is why this
	/// is not written with <see cref="Time.SmoothingFactor"/>.
	/// </para>
	/// </summary>
	private const float HomingRate = 0.5f;

	/// <summary>
	/// How fast the camera closes on the island once it has swung round, as a fraction of what is left
	/// each second - <c>_DAT_00702c60</c>, 0.07 per delta, so 0.7 a second.
	/// </summary>
	private const float RadiusDecay = 0.7f;

	/// <summary>
	/// How fast the eye drops to the island's own level - <c>_DAT_00702c64</c>, 0.6 per delta, so 6.0 a
	/// second. Nearly ten times the radius rate, so the camera comes down to the island long before it
	/// arrives at it.
	/// </summary>
	private const float VerticalDecay = 6f;

	/// <summary>
	/// How close the camera gets before the park is asked for - <c>_DAT_00702c68</c>, <b>8.0</b>. From
	/// SPINRADIUS 70 decaying at <see cref="RadiusDecay"/> that is <c>ln(70/8) / 0.7</c>, about
	/// <b>3.1 seconds</b> of flying in.
	/// </summary>
	private const float ArrivedRadius = 8f;

	/// <summary>
	/// The heading the camera swings onto before it flies in.
	///
	/// <para>
	/// The original homes onto <c>island[+0x14] + pi</c>, the island's own heading turned about. These
	/// islands are all placed at the same heading, and this project has already measured that their gates
	/// face the island's <b>-Y</b> side - which is <c>orbit pi</c>. The two agree, so the gate is what the
	/// camera ends up looking at, which is the point of the manoeuvre.
	/// </para>
	/// </summary>
	private const float GateHeading = MathF.PI;

	/// <summary>
	/// Swings the camera round onto the gate and flies it into the island, then runs
	/// <paramref name="whenArrived"/> - which is how a park entry waits for the camera.
	///
	/// <para>
	/// <b>This is the original's.</b> <c>IslandLobby_EnterPark</c>
	/// (<c>0x005e1cc0</c>, vtable <c>+0x40</c>) checks the keys and calls <c>+0x44</c>,
	/// <c>IslandLobby_LeaveForPark</c> (<c>0x005e1e30</c>) - which sets the lobby's <c>+0x14</c> to
	/// <b>1</b>, plays the key puff and hides the panel. That field is <c>param_1[5]</c> in the camera
	/// update, <b>the state machine's own state</b>, and 1 is "home the angle". So hiding the panel is
	/// not the end of the beat, it is the start of it:
	/// </para>
	/// <list type="number">
	/// <item>state 1 turns the orbit onto the island's heading at <see cref="HomingRate"/>, then plays the gate's
	/// opening clip and sets 2;</item>
	/// <item>state 2 locks that heading and decays the radius and the vertical offset
	/// (<see cref="RadiusDecay"/>, <see cref="VerticalDecay"/>) - the camera flies in;</item>
	/// <item>below <see cref="ArrivedRadius"/> it calls vtable <c>+0x48</c>, which the island lobby
	/// overrides with <c>FUN_005e1e50</c>;</item>
	/// <item>that sets the scene's own choice to <b>2</b>, and <c>FUN_005d5cf0</c> - the state-3
	/// teardown - <i>returns</i> that field, which is the documented "choice 2 means play a park".</item>
	/// </list>
	/// <para>
	/// So the park loads when the camera arrives, not when the panel goes. Escape before then cancels
	/// the whole of it - see <see cref="CancelLeave"/>.
	/// </para>
	/// <para>
	/// <b>Which park</b> is the caller's, fixed here in <paramref name="whenArrived"/>, where the original reads
	/// its current island at arrival (<c>0x005e1e50</c>). Nothing moves the island in flight: the island keys are
	/// refused (<see cref="Step"/>), Escape cancels the flight rather than opening the game menu whose Select New
	/// Player would reach <see cref="SelectFirst"/>, and a lobby that ends mid-flight forgets the closure
	/// (<see cref="ForgetIsland"/>).
	/// </para>
	/// </summary>
	internal static void LeaveForPark( Action whenArrived )
	{
		// The original refuses a second press by testing that the state is still nought.
		if ( _leaving != Leaving.No )
			return;

		_leaveAngle = Wrap( _orbitTime * SpinSpeed );
		_whenArrived = whenArrived;
		_leaving = Leaving.Homing;
	}

	/// <summary>One frame of the leaving sequence, answering the heading to place the camera at.</summary>
	/// <remarks>Internal so that a test can step the sequence without a lobby; only <see cref="Update"/> calls it.</remarks>
	internal static float StepLeaving( CameraSettings settings )
	{
		if ( Paused )
			return _leaving == Leaving.Homing ? _leaveAngle : GateHeading;

		if ( _leaving == Leaving.Homing )
		{
			_leaveAngle = NextAngle( _leaveAngle, GateHeading, HomingRate * Time.Delta, out var arrived );

			if ( arrived )
			{
				_leaveRadius = settings.SpinRadius;
				_leaveVertical = settings.VerticalOffset;
				_leaving = Leaving.FlyingIn;

				// The gate's M1, once, as the camera faces it (0x005e06e4).
				CurrentIsland?.Gate.Open();
			}

			return _leaveAngle;
		}

		// State 2 also darkens the screen as the radius closes on 8 (the render camera's +0x60, 0x005e052f), which
		// nothing here draws yet.
		Unimplemented.Report( "LOBBY_FLY_IN_FADE" );

		_leaveRadius = Decayed( _leaveRadius, RadiusDecay, Time.Delta );
		_leaveVertical = Decayed( _leaveVertical, VerticalDecay, Time.Delta );

		if ( _leaveRadius < ArrivedRadius )
		{
			// Told exactly once, and the state cleared first so that a park load asking anything of this
			// camera on its way out does not find it still leaving.
			var landed = _whenArrived;

			_whenArrived = null;
			_leaving = Leaving.No;

			landed?.Invoke();
		}

		return GateHeading;
	}

	/// <summary>
	/// Whether the camera is on its way to a park - the original's state <c>+0x14</c> not being nought, which Enter
	/// this park tests first (<c>0x005e1ce0</c>).
	/// </summary>
	internal static bool IsLeaving => _leaving != Leaving.No;

	/// <summary>
	/// Escape while the camera is leaving for a park: the island camera's <c>+0x18</c> (<c>0x005e1890</c>), which the
	/// lobby's key handler asks before it will open the game menu.
	///
	/// <para>
	/// While leaving it puts the camera back in orbit and answers true, which keeps the menu shut; the front end then
	/// shows the island panel again. From state 2, flying in, it first plays the gate's shutting clip, since state 1's
	/// arrival has opened it; from state 1 no clip has played. Nothing else the leave did is undone, because nothing
	/// else needs it: the orbit carries on from the angle the leave turned it to - the original's orbit and its
	/// homing are one field, <c>+0x18</c> - and the radius and height go back to the settings on the next frame, so
	/// the camera eases back out as it eases anywhere. In orbit it answers false and the menu opens.
	/// </para>
	/// </summary>
	/// <returns>Whether there was a leave to cancel.</returns>
	internal static bool CancelLeave()
	{
		if ( _leaving == Leaving.No )
			return false;

		if ( _leaving == Leaving.FlyingIn )
			CurrentIsland?.Gate.Shut();

		Log.Info( $"Lobby camera: Escape cancelled the leave for a park while {_leaving}, at angle {_leaveAngle:F3} - "
			+ $"back to orbiting island {IslandIndex}" );

		_orbitTime = _leaveAngle / SpinSpeed;

		_leaving = Leaving.No;
		_whenArrived = null;
		_leaveAngle = 0f;
		_leaveRadius = 0f;
		_leaveVertical = 0f;

		return true;
	}

	/// <summary>
	/// One step of a constant-rate turn toward <paramref name="target"/>, the shorter way round.
	/// </summary>
	/// <remarks>
	/// Pure, so the shortest-way arithmetic can be pinned without a clock, a lobby or a device.
	/// <see cref="StepLeaving"/>, which calls it at the decode's rates, is stepped by a test with a set
	/// <see cref="Time.Delta"/>; <see cref="Update"/> placing the camera from what it answers needs a lobby's
	/// islands, and rests on the capture.
	///
	/// <para>
	/// <paramref name="arrived"/> is true once the gap is under half a step, which is the original's own
	/// test (<c>_DAT_00702c5c</c> = 0.5 applied to the step) rather than an equality a fixed step would
	/// step straight over and then oscillate about for ever.
	/// </para>
	/// </remarks>
	internal static float NextAngle( float angle, float target, float step, out bool arrived )
	{
		var difference = Wrap( angle - target );

		arrived = difference < step * 0.5f || difference > MathF.Tau - (step * 0.5f);

		if ( arrived )
			return Wrap( target );

		// Past half a turn it is shorter to keep going the way the difference points.
		return Wrap( target + (difference >= MathF.PI ? difference + step : difference - step) );
	}

	/// <summary>
	/// One step of the original's decay: it subtracts a fraction of what is LEFT each time, so the
	/// approach is exponential rather than linear and never quite reaches nought.
	/// </summary>
	internal static float Decayed( float value, float rate, float delta ) => value - (delta * value * rate);

	/// <summary>An angle wrapped into 0..2pi, as the original wraps its own with two while loops.</summary>
	private static float Wrap( float angle )
	{
		angle %= MathF.Tau;

		return angle < 0f ? angle + MathF.Tau : angle;
	}

	/// <summary>
	/// How far through leaving for a park the camera is, for the debug console. A pure getter. `waiting` says whether
	/// a park is still to be asked for when the camera arrives.
	/// </summary>
	internal static string LeaveDescription()
		=> $"leave={_leaving} angle={_leaveAngle:F3} radius={_leaveRadius:F2} vertical={_leaveVertical:F2} " +
			$"waiting={_whenArrived != null}";

	/// <summary>The orbit angle in radians. Written by DebugConsole to reproduce a shot exactly.</summary>
	internal static float DebugOrbit
	{
		get => _orbitTime * SpinSpeed;
		set => _orbitTime = value / SpinSpeed;
	}

	/// <summary>
	/// The lobby flying itself while nobody is playing, which is what the original does whenever no
	/// player is selected - <c>FUN_005e0470</c>'s first branch. Decode in <c>docs/exe/lobby.md</c>.
	///
	/// <para>
	/// <b>It steers; it is not placed.</b> The camera keeps a heading, eases that heading toward the
	/// bearing of a target, and flies along the heading it actually has - which is what rounds the
	/// corners off and makes the path read as a dolly rather than as a series of straight runs. When it
	/// comes within <see cref="ArrivalRadius"/> of the target it rolls another one inside the box, so
	/// the flight never ends and never repeats.
	/// </para>
	///
	/// <para>
	/// <b>The aim is a second point doing the same thing</b>, chasing whichever island is nearest,
	/// with a speed that ramps up while it has ground to cover and decays to nothing as it arrives. The
	/// camera looks at that point rather than at the island, so the shot swings rather than snapping.
	/// </para>
	///
	/// <para>
	/// The island it picks is put in <see cref="CurrentIsland"/>, because the original keeps the
	/// nearest island in the very field the picked one uses - which is what makes the lobby's sound and
	/// its weather follow the camera round for free rather than needing to be told.
	/// </para>
	/// </summary>
	private void Attract( List<LobbyIsland> islands )
	{
		if ( !_wandering )
		{
			// Seeded exactly as the constructor seeds it: a random point to stand at, another to head
			// for, and the aim already at full speed.
			_wanderPosition = RandomPointInBox();
			_wanderTarget = RandomPointInBox();
			_wanderDirection = Heading( _wanderPosition, _wanderTarget );

			_lookPosition = RandomPointInBox();
			_lookDirection = Heading( _lookPosition, RandomPointInBox() );
			_lookSpeed = LookSpeedCap;

			_wandering = true;
		}

		if ( !Paused )
		{
			_wanderDirection = Steer( _wanderDirection, _wanderPosition, _wanderTarget );
			_wanderPosition += _wanderDirection * WanderSpeed * Time.Delta;

			if ( (_wanderTarget - _wanderPosition).LengthSquared < ArrivalRadius * ArrivalRadius )
				_wanderTarget = RandomPointInBox();
		}

		var nearest = Nearest( islands, _wanderPosition );

		CurrentIsland = nearest;
		IslandIndex = islands.IndexOf( nearest );

		if ( !Paused )
		{
			var wanted = nearest.CameraTarget;

			_lookDirection = Steer( _lookDirection, _lookPosition, wanted );
			_lookPosition += _lookDirection * _lookSpeed * Time.Delta;

			// Ramped rather than set, so the aim gathers speed while it has somewhere to be and settles
			// as it arrives - see LookRampPerSecond for the one liberty taken with it.
			var step = LookSpeedCap * LookRampPerSecond * Time.Delta;

			_lookSpeed = (wanted - _lookPosition).LengthSquared >= LookArrivalRadius * LookArrivalRadius
				? MathF.Min( _lookSpeed + step, LookSpeedCap )
				: MathF.Max( _lookSpeed - step, 0f );
		}

		Position = _wanderPosition;
		Rotation = Rotation.LookAt( _lookPosition - _wanderPosition );
	}

	/// <summary>A point somewhere inside <see cref="WanderCentre"/>'s box, the way the original rolls one.</summary>
	private static Vector3 RandomPointInBox()
		=> WanderCentre + new Vector3(
			(Random.Shared.NextSingle() - 0.5f) * WanderExtent.X,
			(Random.Shared.NextSingle() - 0.5f) * WanderExtent.Y,
			(Random.Shared.NextSingle() - 0.5f) * WanderExtent.Z );

	/// <summary>
	/// The unit bearing from one point to another, or straight along X where there is none - which is
	/// the substitution the original makes rather than dividing by nought.
	/// </summary>
	private static Vector3 Heading( Vector3 from, Vector3 to )
	{
		var heading = (to - from).Normal;

		return heading.LengthSquared > 0f ? heading : new Vector3( 1f, 0f, 0f );
	}

	/// <summary>Turns a heading toward the bearing it wants and keeps it a unit vector.</summary>
	private static Vector3 Steer( Vector3 heading, Vector3 from, Vector3 to )
		=> heading.LerpTo( Heading( from, to ), Time.SmoothingFactor( DirectionRate ) ).Normal;

	/// <summary>
	/// Whichever island is nearest a point, by the square of the distance to what the camera would aim
	/// at - the original walks its whole island list the same way, seeded at 9999999.
	/// </summary>
	private static LobbyIsland Nearest( List<LobbyIsland> islands, Vector3 to )
	{
		var nearest = islands[0];
		var best = float.MaxValue;

		foreach ( var island in islands )
		{
			var distance = (island.CameraTarget - to).LengthSquared;

			if ( distance >= best )
				continue;

			best = distance;
			nearest = island;
		}

		return nearest;
	}

	/// <summary>Whether the wander has been seeded - see <see cref="Attract"/>.</summary>
	private static bool _wandering;

	private static Vector3 _wanderPosition;
	private static Vector3 _wanderTarget;
	private static Vector3 _wanderDirection;

	private static Vector3 _lookPosition;
	private static Vector3 _lookDirection;
	private static float _lookSpeed;

	/// <summary>Selects an island by index, for DebugConsole. Wraps like the bracket keys do.</summary>
	internal static void DebugSelect( int index )
	{
		var islands = Entity.All.OfType<LobbyIsland>().OrderBy( island => island.Index ).ToList();

		if ( islands.Count == 0 )
			return;

		IslandIndex = ((index % islands.Count) + islands.Count) % islands.Count;
		CurrentIsland = islands[IslandIndex];
	}

	/// <summary>
	/// The bracket keys, which are OpenTPW's own - the original moves between islands only with the panel's arrows
	/// and the cursor keys - and ask through <see cref="Step"/> as both of those do, so all three are refused alike.
	/// </summary>
	/// <returns>Whether either key moved the island.</returns>
	internal static bool IslandKeys( bool next, bool previous )
	{
		var moved = next && Step( 1 );

		return (previous && Step( -1 )) || moved;
	}

	/// <summary>
	/// Moves <paramref name="step"/> islands along, wrapping at either end - what the lobby panel's
	/// arrow buttons and the cursor keys do (see IslandPanel and FrontEnd.LobbyKeys), and the bracket keys too.
	///
	/// <para>
	/// Like the original's next and previous handlers it refuses while the camera is leaving for a park - their
	/// first test (<c>0x005e1ee3</c>), before the game type is looked at - and while an Instant Action game holds
	/// the lobby to one island. See <c>docs/exe/lobby.md</c>, "The island keys wait for the fly-in".
	/// </para>
	/// </summary>
	/// <returns>False when it refused; true when it went to the islands, however many there are.</returns>
	internal static bool Step( int step )
	{
		if ( _leaving != Leaving.No )
		{
			Log.Info( $"Lobby camera: staying on island {IslandIndex} - the camera is leaving for a park" );
			return false;
		}

		if ( HeldToOneIsland )
			return false;

		MoveTo( IslandIndex + step, AllIslands() );
		return true;
	}

	/// <summary>
	/// Puts the lobby back on the first island in the running order, which is Lost Kingdom. The
	/// original does this every time the player slots close, whoever was picked: 0x005e1fa0, called
	/// with 1 from FrontEnd_ClosePlayerSlots, takes the head of the island list and forgets the park
	/// that was remembered, rather than walking the list for a name as it does with 0.
	/// </summary>
	internal static void SelectFirst() => MoveTo( 0, AllIslands() );

	/// <summary>
	/// The islands in the order their scripts give, for the static callers. <see cref="Islands()"/> is the
	/// same list kept by an instance, which is what the camera itself reads every frame.
	/// </summary>
	private static List<LobbyIsland> AllIslands()
		=> Entity.All.OfType<LobbyIsland>().OrderBy( island => island.Index ).ToList();

	/// <summary>
	/// Drops the camera onto wherever it is currently headed, for DebugConsole - the same path
	/// the very first frame takes, so there is no separate teleport to keep working.
	/// </summary>
	internal static void DebugSettle() => _placed = false;

	/// <summary>
	/// What the ATTRACT flight is doing, for the debug console.
	///
	/// <para>
	/// <b>A pure getter, and it exists because nothing here was observable.</b> <see cref="DebugSelect"/>,
	/// <see cref="DebugOrbit"/> and <see cref="DebugSettle"/> are all read by the orbit branch that
	/// <see cref="Update"/> returns before reaching while the camera is flying, and the console's
	/// `attract` is a <b>setter</b> - polling it would select a mode rather than report one, which is
	/// exactly the shape <c>docs/VERIFYING.md</c> rule 88 warns about. `state` carries `cam=` and so can
	/// say where the camera stands, but nothing at all about where it is <i>looking</i> - and the aim is
	/// the whole of the complaint this was added for.
	/// </para>
	/// <para>
	/// <b>`aimDir` is the quantity that matters.</b> The camera looks along
	/// <c>_lookPosition - _wanderPosition</c>, so the angle between that on consecutive frames is the
	/// rate the view is turning - which is what separates a shot that eases from one that swings.
	/// `aimDist` is beside it because the two points share one box and can pass arbitrarily close, and a
	/// direction taken between two nearly coincident points is ill-conditioned however smoothly each of
	/// them moves.
	/// </para>
	/// </summary>
	internal static string AttractState()
	{
		var islands = AllIslands();
		var nearest = islands.Count > 0 ? Nearest( islands, _wanderPosition ) : null;

		var aim = _lookPosition - _wanderPosition;
		var direction = aim.Normal;

		return $"aim wandering={_wandering} " +
			$"pos=({_wanderPosition.X:F3},{_wanderPosition.Y:F3},{_wanderPosition.Z:F3}) " +
			$"target=({_wanderTarget.X:F3},{_wanderTarget.Y:F3},{_wanderTarget.Z:F3}) " +
			$"look=({_lookPosition.X:F3},{_lookPosition.Y:F3},{_lookPosition.Z:F3}) " +
			$"lookSpeed={_lookSpeed:F4} " +
			$"aimDir=({direction.X:F5},{direction.Y:F5},{direction.Z:F5}) " +
			$"aimDist={MathF.Sqrt( aim.LengthSquared ):F3} " +
			$"island={(nearest != null ? islands.IndexOf( nearest ) : -1)} " +
			$"name='{nearest?.ParkName}'";
	}

	/// <summary>
	/// Lets go of the island on show as the lobby ends, so nothing reads an island out of a lobby that has gone,
	/// and of any leave for a park still under way. Which island it was, and where the camera was, are kept, as
	/// they are across camera modes - see <see cref="Paused"/> - so the lobby built next picks up where this one
	/// left off.
	/// </summary>
	internal static void ForgetIsland()
	{
		CurrentIsland = null;

		// The wander is seeded from where it happens to be standing, so a lobby built next has to roll
		// its own rather than carrying on from a flight through a lobby that has gone.
		_wandering = false;

		// A lobby that ends mid-flight takes the flight with it. The original keeps its leave state on the camera
		// object, which every lobby builds afresh with the state at nought (0x005dfd2d), so the next lobby starts
		// in orbit and the park the last one was flying into is never asked for.
		if ( _leaving != Leaving.No )
			Log.Info( "Lobby camera: the lobby ended while leaving for a park, so that leave is forgotten" );

		_leaving = Leaving.No;
		_whenArrived = null;
		_leaveAngle = 0f;
		_leaveRadius = 0f;
		_leaveVertical = 0f;
	}

	/// <summary>
	/// Points the camera at another island, wrapping at either end. There is nothing to reset:
	/// the camera and its aim simply have somewhere new to chase, so pressing again mid-move
	/// carries whatever speed they already have into the new heading.
	/// </summary>
	private static void MoveTo( int index, List<LobbyIsland> islands )
	{
		if ( islands.Count == 0 )
			return;

		IslandIndex = ((index % islands.Count) + islands.Count) % islands.Count;

		Log.Info( $"Lobby camera: moving to island {IslandIndex}, '{islands[IslandIndex].ParkName}'" );
	}

	/// <summary>
	/// The islands in the order their scripts give, which is the order they sit around the lobby.
	/// Built once - they are created before this camera mode is and never change - but retried
	/// while empty in case that order ever reverses.
	/// </summary>
	private List<LobbyIsland> Islands()
	{
		if ( _islands is not { Count: > 0 } )
			_islands = Entity.All.OfType<LobbyIsland>().OrderBy( island => island.Index ).ToList();

		return _islands;
	}

	/// <summary>Reads lobby.txt once - see <see cref="CameraSettings"/>.</summary>
	private CameraSettings Settings()
	{
		if ( _settings is { } cached )
			return cached;

		// The same values the file ships with, so a missing script changes nothing.
		var spinRadius = 70f;
		var verticalOffset = 20f;

		using ( var stream = FileSystem.OpenRead( "lobby/lobby.txt" ) )
		{
			if ( stream != null )
			{
				using var reader = new StreamReader( stream );

				while ( reader.ReadLine() is { } line )
				{
					var trimmed = line.TrimStart();

					if ( LobbyScript.TryReadSetting( trimmed, "SPINRADIUS", out var value ) )
						spinRadius = value;
					else if ( LobbyScript.TryReadSetting( trimmed, "VERTICALOFFSET", out value ) )
						verticalOffset = value;
				}
			}
		}

		_settings = new CameraSettings( spinRadius, verticalOffset );
		return _settings.Value;
	}

}
