using System.Globalization;

namespace OpenTPW;

/// <summary>
/// Orbits whichever park island is on show, and moves between them.
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
	/// The script asks for ISLANDFOV(100), but read as a vertical angle in degrees that leaves
	/// the island a speck in a bowed horizon - so it means something else, or reaches the
	/// projection some other way. The one lobby setting still to be run down.
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
	internal static void ForgetIsland() => CurrentIsland = null;

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
