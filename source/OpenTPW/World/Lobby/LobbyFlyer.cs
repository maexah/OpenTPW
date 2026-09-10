using System.Numerics;

namespace OpenTPW;

/// <summary>
/// One of a park's FLYINGMESH swarm - the jungle's butterflies and hallow's bats.
///
/// Unlike most of the lobby, none of this is invented: the original's flying meshes are a class
/// of their own, and its per-flyer update (FUN_005d9b50) is short enough to state outright.
/// Every frame it does
///
///     wanted    = normalise( target - position )
///     direction = normalise( direction + (wanted - direction) * delta * 0.1 )
///     position += direction * speed * delta
///
///     if ( distance to target squared &lt; 500 )
///         target = centre + extent * (random01 - 0.5)   // per axis
///
/// and that is the whole behaviour. A flyer picks a random point in a box around its island,
/// turns towards it without ever turning sharply, flies until it is within about 22 units, then
/// picks another. There is no wandering, no boundary avoidance and no flocking - the box is
/// respected because every destination is inside it, not because anything pushes back at the
/// edges.
///
/// The flapping is real animation data: Bfly_YELLm1/Bfly_PINKm1 are 5-frame vertex animations of
/// the 15-vertex Butterfly mesh, and Batm1 a longer one of the 14-vertex h_bat mesh. The original
/// simply plays it - it does not pause or vary it, so neither does this.
/// </summary>
public sealed class LobbyFlyer : Entity
{
	/// <summary>
	/// How sharply a flyer can turn, per second.
	///
	/// The original lerps its direction toward the one it wants by <c>delta * 0.1</c>, in the same
	/// per-tick units as its speed - so 2.5 a second, applied here through
	/// <see cref="Time.SmoothingFactor"/> rather than as a raw multiply, which is the same
	/// behaviour at any frame rate rather than only at 25fps.
	/// </summary>
	private const float TurnRate = 0.1f * MeshAnimator.FramesPerSecond;

	/// <summary>
	/// How close counts as having arrived, squared. The original passes a hard-coded 500 into
	/// every swarm it builds and compares the squared distance against it, so this is about 22
	/// units - large next to an island only 50 across, which is why flyers visibly turn for their
	/// next destination well before reaching the last one.
	/// </summary>
	private const float ArrivalRadiusSquared = 500f;

	/// <summary>
	/// How far above the island the flight volume starts.
	///
	/// The original centres the box on the island, so half of it is underneath - which on its
	/// globe is open sky, and here is the sea and the island itself. Sitting the box just above
	/// the terrain instead keeps flyers out of both.
	/// </summary>
	private const float TerrainClearance = 8f;

	/// <summary>
	/// How quickly a swarm fades in and out, per second - about as long as the camera's own slide
	/// between islands.
	///
	/// The original never needs this: its islands sit around a globe of radius 475 and its camera
	/// zooms in on one, so another park's flyers are simply never in shot. Here they are laid out
	/// flat and 200 apart, close enough that the box the script asks for reaches halfway to the
	/// neighbours and that a neighbour is visible in the background anyway. So a swarm belongs to
	/// its island and is only drawn while that island is the one on show.
	/// </summary>
	private const float FadeRate = 3.5f;

	/// <summary>
	/// How close to the camera a flyer may get, in multiples of its own radius, before it is gone
	/// entirely - and how far away before it is fully solid again.
	///
	/// Nothing stops a flyer flying through the camera: its destinations are picked anywhere in a
	/// box 200 across, and the camera orbits 70 units out inside that box. Left alone one
	/// eventually fills the screen and clips through the near plane, which is a jarring flash
	/// rather than a close pass.
	///
	/// In multiples of the model's own radius rather than in units, so a bat and a butterfly both
	/// disappear at the same apparent size rather than the bigger one lingering. At the far end of
	/// the band a flyer covers about a third of the screen's height, which is as close as it needs
	/// to get to read as a close pass; by the near end it would have covered nearly all of it.
	/// </summary>
	private const float HiddenWithinRadii = 1.8f;
	private const float SolidBeyondRadii = 6f;

	/// <summary>
	/// How close any flyer has come to the camera, in multiples of its own radius, and how close
	/// while still solid enough to notice. For DebugConsole: the first says close passes are
	/// actually happening, the second says the fade caught them.
	/// </summary>
	internal static float DebugClosestApproach = float.MaxValue;
	internal static float DebugClosestSolid = float.MaxValue;

	private readonly LobbyModel _model;
	private readonly Random _rng;
	private readonly LobbyIsland _island;
	private readonly Vector3 _centre;
	private readonly Vector3 _halfVolume;
	private readonly float _speed;

	private Vector3 _position;
	private Vector3 _target;
	private Vector3 _direction;

	// Starts hidden so the lobby's opening swarm fades in with everything else rather than
	// being there from the first frame.
	private float _opacity;

	/// <summary>
	/// <paramref name="seed"/> drives every random choice this flyer makes for the rest of its
	/// life - which is only ever where it goes next. See the swarm seed logged by
	/// <see cref="Spawn"/>.
	/// </summary>
	public LobbyFlyer( string modelName, LobbyIsland island, Vector3 centre, Vector3 halfVolume,
		float speed, int seed )
	{
		_rng = new Random( seed );
		_island = island;
		_centre = centre;
		_halfVolume = halfVolume;
		_speed = speed;

		// Starting somewhere random in the box and heading somewhere else random in it is exactly
		// how the original's flyer constructor (FUN_005d9830) starts one.
		_position = RandomPoint();
		_target = RandomPoint();

		var toTarget = _target - _position;
		_direction = toTarget.LengthSquared > 0.0001f ? toTarget.Normal : Vector3.Forward;

		// Never writing depth, because a part-faded flyer that did would punch a hole through
		// whatever is drawn after it - and entity draw order is creation order, so the jungle's
		// butterflies are drawn before the Fantasy island they can reach. The cost is only
		// flyer-over-flyer ordering, and these are flat two-winged meshes with nothing to speak
		// of to self-occlude.
		_model = new LobbyModel( $"lobby/terrain/{modelName}.md2", "lobby/terrain/textures", _position,
			materialFlags: MaterialFlags.DisableDepthWrite );

		_model.SetOpacity( 0f );
	}

	/// <summary>
	/// Spawns one park's FLYINGMESH swarm around its island.
	///
	/// The original builds the swarm up over several frames rather than at once - its tick makes
	/// one flyer and then keeps going with a seven-in-eight chance, so fifty bats take about six
	/// frames. That is a fifth of a second during loading, so this makes them all at once.
	///
	/// It also scales the count by a detail percentage, clamped so there is always at least one
	/// and never more than the script asked for. At full detail that is the script's number,
	/// which is what this uses.
	/// </summary>
	public static void Spawn( LobbyScript.FlyingMesh mesh, LobbyIsland island )
	{
		var seed = Environment.TickCount;
		var rng = new Random( seed );

		var half = mesh.HalfVolume;

		// The script's volume is in the original's Y-up axes: X wide, Y tall, Z deep. This world
		// swaps Y and Z, so the tall one is the last of the three here.
		var halfVolume = new Vector3( half.X, half.Z, half.Y );
		var centre = island.Position + (Vector3.Up * (halfVolume.Z + TerrainClearance));

		Log.Info( $"Lobby: {mesh.Count}x '{mesh.Model}' at {mesh.SpeedPerSecond:F1} units/s "
			+ $"in a {halfVolume * 2f} box around {centre} (seed {seed})" );

		for ( int i = 0; i < mesh.Count; ++i )
			_ = new LobbyFlyer( mesh.Model, island, centre, halfVolume, mesh.SpeedPerSecond, rng.Next() );
	}

	protected override void OnUpdate()
	{
		var dt = Time.Delta;

		var onShow = LobbyCameraMode.CurrentIsland == _island ? 1f : 0f;

		_opacity = _opacity.LerpTo( onShow, Time.SmoothingFactor( FadeRate ) );

		// Out of sight: stop flying and stop flapping too. Three of the four swarms are idle at
		// any one time, which is the whole of their cost gone rather than just their draw.
		if ( _opacity < 0.004f && onShow <= 0f )
		{
			_model.SetOpacity( 0f );
			return;
		}

		var toTarget = _target - _position;

		if ( toTarget.LengthSquared > 0.0001f )
		{
			// A first-order lag on the heading, which is what stops a flyer ever turning sharply:
			// it is always chasing the direction it wants rather than adopting it.
			var wanted = toTarget.Normal;
			_direction = (_direction + ((wanted - _direction) * Time.SmoothingFactor( TurnRate ))).Normal;
		}

		_position += _direction * _speed * dt;

		// Note this is tested after moving, against the position just reached - the same order the
		// original does it in, so a flyer commits to one more step before looking for a new
		// destination.
		if ( (_target - _position).LengthSquared < ArrivalRadiusSquared )
			_target = RandomPoint();

		_model.Update( dt );
		_model.SetTransform( _position, LookAlong( _direction ) );

		// It still flies while it is fading for being close - freezing one in front of the camera
		// would be far worse than drawing it.
		var opacity = _opacity * NearCameraFade();
		_model.SetOpacity( opacity );

		var radii = (_position - Camera.Position).Length / MathF.Max( _model.Radius, 0.001f );

		DebugClosestApproach = MathF.Min( DebugClosestApproach, radii );

		if ( opacity > 0.5f )
			DebugClosestSolid = MathF.Min( DebugClosestSolid, radii );
	}

	/// <summary>Fades this flyer out as it closes on the camera - see <see cref="HiddenWithinRadii"/>.</summary>
	private float NearCameraFade()
	{
		var radius = MathF.Max( _model.Radius, 0.001f );

		return (_position - Camera.Position).Length
			.FadeBetween( radius * HiddenWithinRadii, radius * SolidBeyondRadii );
	}

	/// <summary>A uniformly random point in the flight volume - the original's only steering input.</summary>
	private Vector3 RandomPoint()
	{
		return _centre + new Vector3(
			(_rng.NextSingle() - 0.5f) * _halfVolume.X * 2f,
			(_rng.NextSingle() - 0.5f) * _halfVolume.Y * 2f,
			(_rng.NextSingle() - 0.5f) * _halfVolume.Z * 2f );
	}

	/// <summary>
	/// Faces along a direction without ever rolling.
	///
	/// The original builds its orientation from the flight direction as forward, and a sideways
	/// axis of <c>normalise(dz, 0, -dx)</c> - note the zero in the up slot, read out of the
	/// executable rather than assumed. That is the horizontal perpendicular, so the flyer pitches
	/// with its climb and dive but never banks into a turn.
	///
	/// Crossing forward with world up gives the same horizontal axis here. Local Forward is
	/// (1,0,0), Up is (0,0,1) and Right is (0,-1,0) - see the constants on <see cref="Vector3"/> -
	/// which is why the matrix rows are forward, then -right, then up.
	/// </summary>
	private static Quaternion LookAlong( Vector3 forward )
	{
		forward = forward.Normal;

		var right = forward.Cross( Vector3.Up );

		// Straight up or straight down: the horizontal perpendicular is undefined, so keep the
		// last usable one rather than letting the orientation spin. Any fixed axis will do, since
		// every heading through the pole is equally arbitrary.
		if ( right.LengthSquared < 0.000001f )
			right = Vector3.Right;

		right = right.Normal;

		var up = right.Cross( forward ).Normal;
		var left = -right;

		var matrix = new Matrix4x4(
			forward.X, forward.Y, forward.Z, 0f,
			left.X, left.Y, left.Z, 0f,
			up.X, up.Y, up.Z, 0f,
			0f, 0f, 0f, 1f );

		return Quaternion.CreateFromRotationMatrix( matrix );
	}
}
