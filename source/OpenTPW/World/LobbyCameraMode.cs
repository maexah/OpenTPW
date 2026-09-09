namespace OpenTPW;

/// <summary>
/// Orbits whichever park island is currently on show, and slides between them.
///
/// The orbit is the same for every island - one angle, advancing steadily - and what changes
/// when you move to another park is the point it is centred on. That is deliberate: the camera
/// is never repositioned, so there is no arrival to snap to a default heading. It just keeps
/// turning while the centre slides across, and carries on around the new island from wherever
/// the move left it.
///
/// Because the orbit offset is the same at both ends of a move, sliding the centre and sliding
/// the camera are the same motion - the island stays framed the whole way over, with the old one
/// leaving the shot as the new one enters.
/// </summary>
public class LobbyCameraMode : CameraMode
{
	/// <summary>How far above the point it is looking at the camera sits.</summary>
	private const float Height = 7.5f;

	private const float Distance = 70f;
	private const float Speed = 0.2f;

	/// <summary>How long moving between two islands takes.</summary>
	private const float MoveDuration = 2.5f;

	/// <summary>
	/// Static so it survives <see cref="Camera.SetCameraMode{T}"/> creating a fresh instance.
	/// </summary>
	public static bool Paused { get; set; }

	/// <summary>The island being orbited, as an index into the lobby's running order.</summary>
	private static int IslandIndex { get; set; }

	// Accumulated separately from Time.Now so unpausing resumes where it stopped instead of
	// snapping back onto the wall-clock orbit.
	private float _orbitTime;

	// The point the orbit is centred on right now. While moving between islands it slides from
	// one island's target to the next; the orbit angle is left alone throughout.
	private Vector3 _centre;
	private Vector3 _movingFrom;
	private float _moveElapsed;
	private bool _moving;
	private bool _centred;

	private List<LobbyIsland>? _islands;

	public override void Update()
	{
		var islands = Islands();

		if ( Input.Pressed( InputButton.FreezeCamera ) )
			Paused = !Paused;

		if ( Input.Pressed( InputButton.NextIsland ) )
			MoveTo( IslandIndex + 1, islands );

		if ( Input.Pressed( InputButton.PreviousIsland ) )
			MoveTo( IslandIndex - 1, islands );

		if ( !Paused )
			_orbitTime += Time.Delta;

		UpdateCentre( islands );

		var x = MathF.Sin( _orbitTime * Speed ) * Distance;
		var y = MathF.Cos( _orbitTime * Speed ) * Distance;

		Position = _centre + new Vector3( x, y, Height );
		Rotation = Rotation.LookAt( _centre - Position );

		FieldOfView = 60;
	}

	/// <summary>
	/// Starts moving to another island, wrapping at either end. Pressing again mid-move sets off
	/// from wherever the camera has got to rather than from the island it left.
	/// </summary>
	private void MoveTo( int index, List<LobbyIsland> islands )
	{
		if ( islands.Count == 0 )
			return;

		IslandIndex = ((index % islands.Count) + islands.Count) % islands.Count;

		_movingFrom = _centre;
		_moveElapsed = 0f;
		_moving = _centred;

		Log.Info( $"Lobby camera: moving to island {IslandIndex}, '{islands[IslandIndex].ParkName}'" );
	}

	private void UpdateCentre( List<LobbyIsland> islands )
	{
		if ( islands.Count == 0 )
			return;

		var destination = islands[Math.Clamp( IslandIndex, 0, islands.Count - 1 )].CameraTarget;

		// The first frame has nowhere to move from, so it starts already there.
		if ( !_centred )
		{
			_centre = destination;
			_centred = true;
			return;
		}

		if ( !_moving )
		{
			_centre = destination;
			return;
		}

		_moveElapsed += Time.Delta;

		var t = Math.Clamp( _moveElapsed / MoveDuration, 0f, 1f );

		// Smoothstep, so the camera eases away from one island and settles onto the next rather
		// than starting and stopping abruptly.
		_centre = _movingFrom.LerpTo( destination, t * t * (3f - (2f * t)) );

		if ( t >= 1f )
			_moving = false;
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
}
