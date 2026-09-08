using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	private readonly LobbyModel _model;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];

		// An island's meshes sit 2.5 units below the origin it is placed at.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_isle.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ) );
	}

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );
	}
}
