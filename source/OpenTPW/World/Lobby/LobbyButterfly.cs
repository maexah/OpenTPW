using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A butterfly circling one of the lobby islands, flapping as it goes.
///
/// The flapping is real animation data - Bfly_YELLm1/Bfly_PINKm1 are 5-frame vertex
/// animations of the 15-vertex Butterfly mesh. The flight path is not: nothing in the model
/// or animation files describes where these sit or how they move, so the circling here is
/// ours rather than the original game's.
/// </summary>
public sealed class LobbyButterfly : Entity
{
	private readonly LobbyModel _model;
	private readonly Vector3 _centre;
	private readonly float _radius;
	private readonly float _height;
	private readonly float _speed;
	private readonly float _phase;

	public LobbyButterfly( string modelName, Vector3 centre, float radius, float height, float speed, float phase )
	{
		_centre = centre;
		_radius = radius;
		_height = height;
		_speed = speed;
		_phase = phase;

		_model = new LobbyModel( $"lobby/terrain/{modelName}.md2", "lobby/terrain/textures", centre );
	}

	protected override void OnUpdate()
	{
		_model.Animator?.Update( Time.Delta );

		var t = (Time.Now * _speed) + _phase;

		// Circle the island, bobbing gently, and face the way we're going.
		// Wide enough to clear the island, which spans about 26 units from its centre.
		var offset = new Vector3(
			MathF.Cos( t ) * _radius,
			MathF.Sin( t ) * _radius,
			_height + (MathF.Sin( t * 3f ) * 1.5f) );

		var heading = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, t + MathF.PI * 0.5f );

		_model.SetTransform( _centre + offset, heading );
	}
}
