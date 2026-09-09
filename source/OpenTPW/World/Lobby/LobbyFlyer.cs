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
	/// How much of the original's flight volume to actually use.
	///
	/// The script asks for a box 200 wide and 100 tall around an island only about 50 across, and
	/// at face value that is what it gets: the flyers scatter across the whole sky and out over
	/// the open sea, and stop reading as belonging to the island at all. The original could
	/// afford it because its lobby camera watches from much further out - it orbits a globe of
	/// radius 475 - where this one sits 70 units away, so the same box fills far more of the
	/// screen here.
	///
	/// So the whole flight system is scaled down together: the volume, the arrival radius (by the
	/// square of this, since it is compared squared) and the speed. Scaling all three keeps every
	/// ratio the original set - how far a flyer travels between destinations, how long that takes,
	/// how much of the leg it spends turning - and only changes how big the whole thing is. The
	/// models themselves are not scaled; they are drawn at the size they were authored, which is
	/// what the original does.
	/// </summary>
	private const float VolumeScale = 0.45f;

	/// <summary>
	/// How far above the island the flight volume starts.
	///
	/// The original centres the box on the island, so half of it is underneath - which on its
	/// globe is open sky, and here is the sea and the island itself. Sitting the box just above
	/// the terrain instead keeps flyers out of both.
	/// </summary>
	private const float TerrainClearance = 8f;

	private readonly LobbyModel _model;
	private readonly Random _rng;
	private readonly Vector3 _centre;
	private readonly Vector3 _halfVolume;
	private readonly float _speed;

	private Vector3 _position;
	private Vector3 _target;
	private Vector3 _direction;

	/// <summary>
	/// <paramref name="seed"/> drives every random choice this flyer makes for the rest of its
	/// life - which is only ever where it goes next. See the swarm seed logged by
	/// <see cref="Spawn"/>.
	/// </summary>
	public LobbyFlyer( string modelName, Vector3 centre, Vector3 halfVolume, float speed, int seed )
	{
		_rng = new Random( seed );
		_centre = centre;
		_halfVolume = halfVolume;
		_speed = speed;

		// Starting somewhere random in the box and heading somewhere else random in it is exactly
		// how the original's flyer constructor (FUN_005d9830) starts one.
		_position = RandomPoint();
		_target = RandomPoint();

		var toTarget = _target - _position;
		_direction = toTarget.LengthSquared > 0.0001f ? toTarget.Normal : Vector3.Forward;

		_model = new LobbyModel( $"lobby/terrain/{modelName}.md2", "lobby/terrain/textures", _position );
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
	public static void Spawn( LobbyScript.FlyingMesh mesh, Vector3 islandOrigin )
	{
		var seed = Environment.TickCount;
		var rng = new Random( seed );

		var half = mesh.HalfVolume * VolumeScale;

		// The script's volume is in the original's Y-up axes: X wide, Y tall, Z deep. This world
		// swaps Y and Z, so the tall one is the last of the three here.
		var halfVolume = new Vector3( half.X, half.Z, half.Y );
		var centre = islandOrigin + (Vector3.Up * (halfVolume.Z + TerrainClearance));
		var speed = mesh.SpeedPerSecond * VolumeScale;

		Log.Info( $"Lobby: {mesh.Count}x '{mesh.Model}' at {speed:F1} units/s "
			+ $"in a {halfVolume * 2f} box around {centre} (seed {seed})" );

		for ( int i = 0; i < mesh.Count; ++i )
			_ = new LobbyFlyer( mesh.Model, centre, halfVolume, speed, rng.Next() );
	}

	protected override void OnUpdate()
	{
		var dt = Time.Delta;

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
		if ( (_target - _position).LengthSquared < ArrivalRadiusSquared * VolumeScale * VolumeScale )
			_target = RandomPoint();

		_model.Update( dt );
		_model.SetTransform( _position, LookAlong( _direction ) );
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
