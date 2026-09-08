using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The gate on a lobby island - the way into that theme's park.
///
/// Its doors are the clearest example of a rotation animation in the game: Jun_gateM1 swings
/// door01 a quarter turn and door02 back the other way, and Jun_gateM2 is exactly the inverse.
/// The gate is authored in the same model space as the island it belongs to (the island's own
/// static 'gateway' mesh sits right where this one lands), so it needs no placement of ours.
///
/// The doors loop open and shut continuously, which is a diagnostic rather than the behaviour
/// we want: the gate should idle shut and swing open only when the player enters the park, and
/// back again when they leave. Nothing raises a park entry yet, so the loop stands in for it -
/// it is also the easiest way to see at a glance that rotation animation is still working.
/// </summary>
public sealed class LobbyGate : Entity
{
	private readonly LobbyModel _model;

	public LobbyGate( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];

		// Same 2.5 unit drop the island's own meshes get - the two share a model space.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_gate.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ) );
	}

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );
	}
}
