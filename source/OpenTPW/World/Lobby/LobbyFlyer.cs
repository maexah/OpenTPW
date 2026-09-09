using System.Numerics;

namespace OpenTPW;

/// <summary>
/// One of a park's FLYINGMESH swarm, wandering the airspace around its island and flapping as it
/// goes - the jungle's butterflies and hallow's bats.
///
/// The flapping is real animation data: Bfly_YELLm1/Bfly_PINKm1 are 5-frame vertex animations of
/// the 15-vertex Butterfly mesh, and Batm1 is a longer one of the 14-vertex h_bat mesh. What
/// flies, how many, and how big all come from the park's own script - see
/// <see cref="LobbyScript.FlyingMesh"/>. Everything about where it flies is ours: nothing in the
/// model or animation files describes a flight path, so this is a simple wander-and-turn-around
/// steering behaviour rather than anything from the original game.
///
/// Flight is bounded by a sphere - <see cref="MaxRadius"/> from a point centred between
/// <see cref="MinHeight"/> and <see cref="MaxHeight"/> above the island's origin - plus a
/// hard floor at <see cref="MinHeight"/> so nothing dips into the island itself (the jungle
/// island's terrain tops out around 6-8 units above its own origin, its trees around 30, so
/// a floor of 12 clears the terrain with margin while still sitting below the treetops).
/// Both boundaries are approached with a gentle steering bias rather than a hard bounce or a
/// teleport, and every direction change - wander, boundary avoidance, floor avoidance alike -
/// is capped by <see cref="TurnRate"/>, so nothing snaps or reverses instantly.
///
/// Movement is fully 3D (it does climb and descend, and does wander in every direction) but
/// facing only ever reflects the horizontal component of that - see <see cref="FaceDirection"/> -
/// so it always flies level, banking into nothing. Descending is a distinct visible cue instead:
/// the wingbeat briefly holds on its rest pose - wings up - right as a descent begins, then
/// resumes on its own.
/// </summary>
public sealed class LobbyFlyer : Entity
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

	/// <summary>
	/// How far below level counts as "starting to descend" - a deadband below 0 so gentle
	/// wander noise around level flight doesn't retrigger this constantly. Crossing back above
	/// <see cref="ResumeDescentTrigger"/>, a little higher, re-arms it, so a single continuous
	/// descent only pauses the wingbeat once at its start rather than repeatedly.
	/// </summary>
	private const float DescentTrigger = -0.15f;
	private const float ResumeDescentTrigger = -0.05f;

	/// <summary>How long the wingbeat holds on its rest pose when a descent begins.</summary>
	private const float DescentPauseDuration = 0.25f;

	/// <summary>
	/// Below this horizontal speed (as a fraction of full speed), the direction is close enough
	/// to straight up or down that its horizontal projection - and so the yaw derived from it -
	/// is no longer trustworthy. A small, fixed rotation applied near that pole swings the
	/// projected angle wildly for a fixed rotation angle (checked directly: at 6 degrees off
	/// vertical, one frame of ordinary wander jitter alone can swing the projected yaw nearly
	/// 5 degrees, growing without bound closer to the pole) even though the actual 3D direction
	/// is turning smoothly the whole time. <see cref="_facingYaw"/> is what actually gets faced;
	/// it keeps its last trustworthy heading through a steep climb or dive instead of chasing
	/// that noise.
	/// </summary>
	private const float MinYawConfidence = 0.3f;

	private readonly LobbyModel _model;
	private readonly Vector3 _origin;
	private readonly Vector3 _flightCentre;
	private readonly Random _rng;
	private readonly float _speed;

	private Vector3 _position;
	private Vector3 _direction;

	/// <summary>
	/// The last yaw (radians) confident enough to face - see <see cref="MinYawConfidence"/>.
	/// A scalar angle rather than a direction vector deliberately: smoothing this as a vector
	/// with the same cross-product-based RotateTowards used for movement has its own pole -
	/// two horizontal vectors that are exactly opposite cross to zero, and the fallback axis
	/// that then picks is itself horizontal rather than vertical, so rotating about it tips the
	/// facing out of the horizontal plane right when reversing heading. A plain angle, wrapped
	/// and clamped with ordinary scalar arithmetic, has no such case - it can't ever leave the
	/// horizontal plane because it was never anything but horizontal to begin with.
	/// </summary>
	private float _facingYaw;

	private bool _canTriggerDescentPause = true;
	private float _flapPauseRemaining;

	/// <summary>
	/// <paramref name="seed"/> drives every random choice this flyer makes for the rest of its
	/// life (wander direction, speed variance) - see the swarm seed logged by <see cref="Spawn"/>.
	/// </summary>
	public LobbyFlyer( string modelName, float scale, Vector3 origin, Vector3 startPosition,
		Vector3 startDirection, int seed )
	{
		_origin = origin;
		_flightCentre = origin + (Vector3.Up * ((MinHeight + MaxHeight) * 0.5f));
		_rng = new Random( seed );

		// A little per-flyer speed variety so a whole swarm doesn't move in lockstep.
		_speed = Speed * (0.8f + (_rng.NextSingle() * 0.4f));

		_position = startPosition;
		_direction = startDirection.Normal;

		// Same confidence rule as OnUpdate: only trust the start direction's own horizontal
		// component if there's enough of it, otherwise face an arbitrary but valid heading -
		// it'll pick up a real one within a frame or two once it starts actually moving.
		var startHorizontal = new Vector3( _direction.X, _direction.Y, 0f );
		_facingYaw = startHorizontal.Length >= MinYawConfidence
			? MathF.Atan2( startHorizontal.Y, startHorizontal.X )
			: 0f;

		_model = new LobbyModel( $"lobby/terrain/{modelName}.md2", "lobby/terrain/textures", _position, scale );
	}

	/// <summary>
	/// Spawns one park's FLYINGMESH swarm around its island: as many as the script asks for, at
	/// the scale it asks for, scattered across the flight volume rather than clustered at one
	/// radius and height so the swarm looks established from the first frame instead of visibly
	/// starting from a single ring.
	///
	/// Everything random here comes from one seed, logged so a particular arrangement can be
	/// reproduced if it is ever worth debugging.
	/// </summary>
	public static void Spawn( LobbyScript.FlyingMesh mesh, Vector3 islandOrigin )
	{
		var seed = Environment.TickCount;
		var rng = new Random( seed );

		Log.Info( $"Lobby: {mesh.Count}x '{mesh.Model}' at scale {mesh.EngineScale} around {islandOrigin} (seed {seed})" );

		for ( int i = 0; i < mesh.Count; ++i )
		{
			var angle = rng.NextSingle() * MathF.PI * 2f;
			var radius = MathF.Sqrt( rng.NextSingle() ) * MaxRadius * 0.8f;
			var height = MinHeight + (rng.NextSingle() * (MaxHeight - MinHeight));

			var startPosition = islandOrigin + new Vector3(
				MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, height );

			// Mostly horizontal to start, with a shallow pitch so nobody spawns already diving
			// or climbing steeply.
			var headingYaw = rng.NextSingle() * MathF.PI * 2f;
			var headingPitch = ((rng.NextSingle() * 2f) - 1f) * (MathF.PI / 12f);
			var startDirection = new Vector3(
				MathF.Cos( headingYaw ) * MathF.Cos( headingPitch ),
				MathF.Sin( headingYaw ) * MathF.Cos( headingPitch ),
				MathF.Sin( headingPitch ) );

			_ = new LobbyFlyer( mesh.Model, mesh.EngineScale, islandOrigin, startPosition, startDirection, rng.Next() );
		}
	}

	protected override void OnUpdate()
	{
		var dt = Time.Delta;

		var desired = Wander( _direction, dt );
		desired = AvoidRadius( desired );
		desired = AvoidFloorAndCeiling( desired );

		// Whatever the wander/boundary logic above wants, the actual heading only ever moves
		// toward it at this capped rate - the one place "never drastic" is actually enforced.
		_direction = RotateTowards( _direction, desired, TurnRate * dt );
		_position += _direction * _speed * dt;

		// Only chase the new heading's horizontal projection when it's confident enough to
		// trust - see MinYawConfidence. Below that, keep facing the last confident heading
		// rather than let a steep climb or dive spin the facing on projection noise.
		var horizontal = new Vector3( _direction.X, _direction.Y, 0f );
		if ( horizontal.Length >= MinYawConfidence )
		{
			var targetYaw = MathF.Atan2( horizontal.Y, horizontal.X );
			var maxStep = TurnRate * dt;

			_facingYaw += WrapAngle( targetYaw - _facingYaw ).Clamp( -maxStep, maxStep );
		}

		// Edge-triggered: a single continuous descent pauses the wingbeat once, right as it
		// begins, rather than for as long as the descent lasts.
		if ( _canTriggerDescentPause && _direction.Z < DescentTrigger )
		{
			_flapPauseRemaining = DescentPauseDuration;
			_canTriggerDescentPause = false;
			_model.Pause();
		}
		else if ( _direction.Z > ResumeDescentTrigger )
		{
			_canTriggerDescentPause = true;
		}

		if ( _flapPauseRemaining > 0f )
			_flapPauseRemaining -= dt;
		else
			_model.Update( dt );

		_model.SetTransform( _position, FaceDirection( _facingYaw ) );
	}

	/// <summary>Wraps an angle in radians to (-pi, pi], so a shortest-path turn can be found by simple clamping.</summary>
	private static float WrapAngle( float radians )
	{
		const float tau = MathF.PI * 2f;

		radians %= tau;

		if ( radians < -MathF.PI )
			radians += tau;
		else if ( radians > MathF.PI )
			radians -= tau;

		return radians;
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
	/// A yaw-only facing rotation: it turns to face <paramref name="yaw"/>, but never pitches
	/// or rolls, so it always flies with level wings no matter how much it's currently climbing
	/// or descending (that's real - see <see cref="_position"/> - it just isn't reflected in
	/// orientation). No banking into turns, by construction rather than by damping: for a
	/// forward vector with no vertical component, this always resolves to exactly world up,
	/// with zero roll, for every heading - checked directly before ever touching this file,
	/// not just assumed.
	///
	/// This replaced an earlier version that derived up from the actual 3D flight direction
	/// (tilting to stay perpendicular to it, the usual way to face a moving object), which
	/// read as banking whenever a climb or dive nudged that direction's vertical component -
	/// exactly what was asked to go away here.
	///
	/// Local Forward is (1,0,0), Up is (0,0,1) and Right is (0,-1,0) (so local Y is Left) -
	/// see the constants on <see cref="Vector3"/> - which is why the matrix rows below are
	/// forward, then -right, then up, matching how a rotation quaternion's matrix places each
	/// local axis's world-space image.
	/// </summary>
	private static Quaternion FaceDirection( float yaw )
	{
		var forward = new Vector3( MathF.Cos( yaw ), MathF.Sin( yaw ), 0f );

		var right = forward.Cross( Vector3.Up ).Normal;
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
