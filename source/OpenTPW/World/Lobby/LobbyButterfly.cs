using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A butterfly wandering the airspace around one of the lobby islands, flapping as it goes.
///
/// The flapping is real animation data - Bfly_YELLm1/Bfly_PINKm1 are 5-frame vertex
/// animations of the 15-vertex Butterfly mesh. Everything about where it flies is ours:
/// nothing in the model or animation files describes a flight path, so this is a simple
/// wander-and-turn-around steering behaviour rather than anything from the original game.
///
/// Flight is bounded by a sphere - <see cref="MaxRadius"/> from a point centred between
/// <see cref="MinHeight"/> and <see cref="MaxHeight"/> above the island's origin - plus a
/// hard floor at <see cref="MinHeight"/> so nothing dips into the island itself (the jungle
/// island's terrain tops out around 6-8 units above its own origin, its trees around 30, so
/// a floor of 12 clears the terrain with margin while still sitting below the treetops).
/// Both boundaries are approached with a gentle steering bias rather than a hard bounce or a
/// teleport, and every direction change - wander, boundary avoidance, floor avoidance alike -
/// is capped by <see cref="TurnRate"/>, so nothing snaps or reverses instantly.
/// </summary>
public sealed class LobbyButterfly : Entity
{
	/// <summary>Never flies lower than this many units above the island's own origin.</summary>
	public const float MinHeight = 12f;

	/// <summary>Won't wander higher than this many units above the island's origin.</summary>
	public const float MaxHeight = 46f;

	/// <summary>How far it will wander from a point centred between Min/MaxHeight before turning back.</summary>
	public const float MaxRadius = 42f;

	private const float Speed = 10f;

	/// <summary>Hard cap, in radians/second, on how fast its heading can change at all.</summary>
	private const float TurnRate = 1.05f;

	/// <summary>How fast the random wander alone tries to turn - well under TurnRate, so it reads as drifting rather than darting.</summary>
	private const float WanderRate = 0.4f;

	private readonly LobbyModel _model;
	private readonly Vector3 _origin;
	private readonly Vector3 _flightCentre;
	private readonly Random _rng;
	private readonly float _speed;

	private Vector3 _position;
	private Vector3 _direction;

	/// <summary>
	/// <paramref name="seed"/> drives every random choice this butterfly makes for the rest of
	/// its life (wander direction, speed variance) - see the swarm seed logged when the lobby
	/// spawns its butterflies.
	/// </summary>
	public LobbyButterfly( string modelName, Vector3 origin, Vector3 startPosition, Vector3 startDirection, int seed )
	{
		_origin = origin;
		_flightCentre = origin + (Vector3.Up * ((MinHeight + MaxHeight) * 0.5f));
		_rng = new Random( seed );

		// A little per-butterfly speed variety so sixteen of them don't move in lockstep.
		_speed = Speed * (0.8f + (_rng.NextSingle() * 0.4f));

		_position = startPosition;
		_direction = startDirection.Normal;

		_model = new LobbyModel( $"lobby/terrain/{modelName}.md2", "lobby/terrain/textures", _position );
	}

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );

		var dt = Time.Delta;

		var desired = Wander( _direction, dt );
		desired = AvoidRadius( desired );
		desired = AvoidFloorAndCeiling( desired );

		// Whatever the wander/boundary logic above wants, the actual heading only ever moves
		// toward it at this capped rate - the one place "never drastic" is actually enforced.
		_direction = RotateTowards( _direction, desired, TurnRate * dt );
		_position += _direction * _speed * dt;

		_model.SetTransform( _position, FaceDirection( _direction ) );
	}

	/// <summary>
	/// Nudges a direction by a small random rotation about a uniformly random axis - a
	/// continuous random walk on the unit sphere of possible headings, rather than picking a
	/// fresh random direction outright (which is what would look "drastic").
	/// </summary>
	private Vector3 Wander( Vector3 direction, float dt )
	{
		var z = (_rng.NextSingle() * 2f) - 1f;
		var theta = _rng.NextSingle() * MathF.PI * 2f;
		var r = MathF.Sqrt( MathF.Max( 0f, 1f - (z * z) ) );
		var axis = new Vector3( r * MathF.Cos( theta ), r * MathF.Sin( theta ), z );

		var angle = _rng.NextSingle() * WanderRate * dt;
		var rotation = Quaternion.CreateFromAxisAngle( axis.GetSystemVector3(), angle );

		return System.Numerics.Vector3.Transform( direction.GetSystemVector3(), rotation );
	}

	/// <summary>Biases the desired direction back toward <see cref="_flightCentre"/> once past <see cref="MaxRadius"/>.</summary>
	private Vector3 AvoidRadius( Vector3 desired )
	{
		var offset = _position - _flightCentre;
		var distance = offset.Length;

		if ( distance <= MaxRadius )
			return desired;

		// Already past the edge - bias hard back toward the centre. The turn-rate clamp in
		// OnUpdate still limits how fast this actually turns the butterfly, so it can carry a
		// little further past the boundary rather than snapping back onto a new heading.
		var inward = (-offset).Normal;
		var overshoot = MathF.Min( (distance - MaxRadius) / MaxRadius, 1f );

		return desired.LerpTo( inward, 0.5f + (overshoot * 0.5f) ).Normal;
	}

	/// <summary>
	/// Biases the desired direction up near <see cref="MinHeight"/> and down near
	/// <see cref="MaxHeight"/>, starting a little before either limit so the turn-rate clamp
	/// has room to act before something is actually breached.
	/// </summary>
	private Vector3 AvoidFloorAndCeiling( Vector3 desired )
	{
		const float margin = 8f;

		var heightAboveOrigin = _position.Z - _origin.Z;

		if ( heightAboveOrigin < MinHeight + margin )
		{
			var urgency = ((MinHeight + margin - heightAboveOrigin) / margin).Clamp( 0f, 1f );
			var up = desired.WithZ( MathF.Abs( desired.Z ) + 0.75f ).Normal;

			return desired.LerpTo( up, urgency ).Normal;
		}

		if ( heightAboveOrigin > MaxHeight - margin )
		{
			var urgency = ((heightAboveOrigin - (MaxHeight - margin)) / margin).Clamp( 0f, 1f );
			var down = desired.WithZ( -MathF.Abs( desired.Z ) - 0.75f ).Normal;

			return desired.LerpTo( down, urgency ).Normal;
		}

		return desired;
	}

	/// <summary>
	/// A stable "keep this side up" facing rotation, built directly rather than through
	/// <see cref="Rotation.LookAt"/> - that helper is a minimal single-axis rotation from a
	/// fixed reference (local Forward), which leaves roll around the new forward axis
	/// completely unconstrained. A butterfly wandering through the full circle of yaw
	/// regularly passes near that reference's opposite direction, where the axis it rotates
	/// about goes unstable, and the model would flip, roll or end up flying sideways right as
	/// it changed direction - which is exactly what this replaced.
	///
	/// Instead this derives a full local frame - forward along the flight direction, up kept
	/// as close to world up as the tilt of that direction allows, right/left completing it -
	/// and builds the rotation whose local axes land exactly on that frame. Roll is never
	/// "whatever falls out of the maths", it is pinned to level: the wings stay upright.
	///
	/// Local Forward is (1,0,0), Up is (0,0,1) and Right is (0,-1,0) (so local Y is Left) -
	/// see the constants on <see cref="Vector3"/> - which is why the matrix rows below are
	/// forward, then -right, then up, matching how a rotation quaternion's matrix places each
	/// local axis's world-space image.
	/// </summary>
	private static Quaternion FaceDirection( Vector3 forward )
	{
		forward = forward.Normal;

		var worldUp = Vector3.Up;
		var right = forward.Cross( worldUp );

		// Forward is nearly straight up or down, where forward x up degenerates (forward is
		// then necessarily close to (0,0,+-1), so world Forward is always a safe substitute
		// reference here) - fall back so we still get a valid frame. Flight is pitch-limited
		// enough that this is a safety net rather than something that happens in practice.
		if ( right.LengthSquared < 0.0001f )
			right = forward.Cross( Vector3.Forward );

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

	/// <summary>Rotates <paramref name="from"/> toward <paramref name="to"/> by at most <paramref name="maxAngle"/> radians.</summary>
	private static Vector3 RotateTowards( Vector3 from, Vector3 to, float maxAngle )
	{
		from = from.Normal;
		to = to.Normal;

		var dot = from.Dot( to ).Clamp( -1f, 1f );
		var angle = MathF.Acos( dot );

		if ( angle <= maxAngle || angle < 0.0001f )
			return to;

		var axis = from.Cross( to );
		if ( axis.LengthSquared < 0.0000001f )
			axis = from.Cross( MathF.Abs( from.Z ) < 0.9f ? Vector3.Up : Vector3.Right );

		axis = axis.Normal;

		var rotation = Quaternion.CreateFromAxisAngle( axis.GetSystemVector3(), maxAngle );
		return System.Numerics.Vector3.Transform( from.GetSystemVector3(), rotation );
	}
}
