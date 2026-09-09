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
/// </summary>
public class LobbyCameraMode : CameraMode
{
	/// <summary>
	/// How fast the camera and its aim close on where they are headed.
	///
	/// The original's constants are per frame, and only make sense that way: read as per-second
	/// they would give a ten-second camera lag and a five-minute orbit. At the 25fps the rest of
	/// the game's data assumes - see <see cref="MeshAnimator.FramesPerSecond"/> - its 0.1 and 0.2
	/// per frame come out as 2.63/s and 5.58/s, by -25 * ln(1 - perFrame).
	///
	/// Only the derivation touches 25fps - the rates themselves are per second, and are applied
	/// through <see cref="Time.SmoothingFactor"/>, so the camera behaves the same at any frame
	/// rate. These are deliberately half the original's. It spent those rates spinning a globe
	/// to the island it wanted, so they had a long move to decelerate over; sliding straight
	/// between islands the way this does, they arrive in about half a second and the slowing down
	/// barely reads. The 2:1 ratio between the two is the part worth keeping faithful - it is
	/// what settles the shot on the new island while the camera is still travelling.
	/// </summary>
	private const float PositionRate = 1.32f;   // half of the original's 0.1 per frame
	private const float LookAtRate = 2.79f;     // half of the original's 0.2 per frame

	/// <summary>
	/// How fast the orbit turns, in radians per second. The script asks for SPINSPEED(0.02),
	/// which is per frame - half a radian a second at 25fps, round every 12.6 seconds - and that
	/// is brisker than this wants, so the rate is tuned rather than taken: a little over half a
	/// minute to come round.
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

		if ( Input.Pressed( InputButton.NextIsland ) )
			MoveTo( IslandIndex + 1, islands );

		if ( Input.Pressed( InputButton.PreviousIsland ) )
			MoveTo( IslandIndex - 1, islands );

		if ( islands.Count == 0 )
			return;

		var settings = Settings();

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

	/// <summary>
	/// Points the camera at another island, wrapping at either end. There is nothing to reset:
	/// the camera and its aim simply have somewhere new to chase, so pressing again mid-move
	/// carries whatever speed they already have into the new heading.
	/// </summary>
	private void MoveTo( int index, List<LobbyIsland> islands )
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
