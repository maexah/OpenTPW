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
/// a park's camera will be a mode of its own.
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
	///
	/// These used to be 1.32 and 2.79, from a derivation that was wrong twice over: it read the
	/// delta as 25 ticks a second rather than 10, and it applied <c>-25 * ln(1 - f)</c> to a factor
	/// the original already multiplies by a delta. It then halved the result to stop the
	/// deceleration reading as slow - a correction the faithful rate does not need, being slower
	/// than the tuned value it replaces.
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
	///
	/// The value is unchanged by the correction to that tick - the old comment called it tuned
	/// rather than taken, and it was neither: it is exactly what the file asks for, arrived at by
	/// luck while the tick rate was being read at 25 a second.
	/// </summary>
	private const float SpinSpeed = 0.2f;

	/// <summary>
	/// The box the camera wanders inside while nobody is playing, from the lobby object's constructor
	/// (<c>FUN_005dfcd0</c>): centre (500, 75, 500) with extents (400, 50, 400). Those are the
	/// <b>full</b> sizes rather than half-extents - the original rolls a point as
	/// <c>centre + rand * extent - extent / 2</c>.
	///
	/// <para>
	/// <b>The middle component is the vertical.</b> The original is Y-up and this world is Z-up, so the
	/// centre lands at (500, 500, 75) here and the extents at (400, 400, 50): the box spans 300 to 700
	/// across the lobby and 50 to 100 above it. That is the lobby's own geometry - the four islands
	/// stand at 400 and 600 in both directions, centred on (500, 500) - which is what says the axes
	/// have been read the right way round rather than transposed.
	/// </para>
	/// </summary>
	private static readonly Vector3 WanderCentre = new( 500f, 500f, 75f );

	/// <summary>The full size of <see cref="WanderCentre"/>'s box - see there.</summary>
	private static readonly Vector3 WanderExtent = new( 400f, 400f, 50f );

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
	/// So this camera, which is the island lobby, could take 83.58 and did briefly. Alexah looked at both
	/// and preferred 60, which is the number that was already here by accident - it is the globe lobby's.
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
	/// Whether the lobby stays on the island it is showing, however it is asked to move. An Instant
	/// Action game is played that way: the original's previous and next island handlers (0x005e1ee0
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
	private readonly record struct CameraSettings( float SpinRadius, float VerticalOffset );

	public override void Update()
	{
		var islands = Islands();

		if ( Input.Pressed( InputButton.FreezeCamera ) )
			Paused = !Paused;

		if ( !HeldToOneIsland && Input.Pressed( InputButton.NextIsland ) )
			MoveTo( IslandIndex + 1, islands );

		if ( !HeldToOneIsland && Input.Pressed( InputButton.PreviousIsland ) )
			MoveTo( IslandIndex - 1, islands );

		if ( islands.Count == 0 )
			return;

		var settings = Settings();

		NominalDistance = MathF.Sqrt(
			(settings.SpinRadius * settings.SpinRadius) + (settings.VerticalOffset * settings.VerticalOffset) );

		// With nobody playing the lobby flies itself instead of orbiting one island - the same branch
		// the original takes, on the same condition. Its test is that no player is selected
		// (FUN_0048bcd0's +0x60 reading -1); ours is the roster having no current player.
		if ( Players.Roster.Current is null )
		{
			Attract( islands );

			FieldOfView = FieldOfViewDegrees;
			return;
		}

		if ( !Paused )
			_orbitTime += Time.Delta;

		var angle = _orbitTime * SpinSpeed;

		CurrentIsland = islands[Math.Clamp( IslandIndex, 0, islands.Count - 1 )];

		var wantedLookAt = CurrentIsland.CameraTarget;

		var wantedPosition = wantedLookAt + new Vector3(
			MathF.Sin( angle ) * settings.SpinRadius,
			MathF.Cos( angle ) * settings.SpinRadius,
			settings.VerticalOffset );

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
	/// Moves <paramref name="step"/> islands along, wrapping at either end - what the lobby panel's
	/// arrow buttons and the cursor keys do (see IslandPanel), and the bracket keys too.
	/// </summary>
	internal static void Step( int step )
	{
		if ( HeldToOneIsland )
			return;

		MoveTo( IslandIndex + step, AllIslands() );
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
	/// Lets go of the island on show as the lobby ends, so nothing reads an island out of a lobby that has gone.
	/// Which island it was, and where the camera was, are kept, as they are across camera modes - see
	/// <see cref="Paused"/> - so the lobby built next picks up where this one left off.
	/// </summary>
	internal static void ForgetIsland()
	{
		CurrentIsland = null;

		// The wander is seeded from where it happens to be standing, so a lobby built next has to roll
		// its own rather than carrying on from a flight through a lobby that has gone.
		_wandering = false;
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
