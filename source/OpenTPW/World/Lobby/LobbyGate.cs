using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The gate on a lobby island - the way into that theme's park.
///
/// How much of the gate this model is varies by park. The jungle's is only its two doors and
/// hallow's only its two rails, because the structure they hang in is part of the island model.
/// Fantasy and space have no gateway on their island at all: their gate model is the whole
/// thing - fantasy's worm and its leaf sign, space's hatch and its two screens - which is why
/// those two parks had no front gate until this was built for every island rather than just the
/// jungle.
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

	/// <param name="signTextures">
	/// The park's sign panels, which land here rather than on the island for fantasy and space.
	/// Ignored by a gate model that does not name sign1 and sign2.
	/// </param>
	public LobbyGate( Vector3 _position, string themeName,
		IReadOnlyDictionary<string, Texture>? signTextures = null )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];

		// Same 2.5 unit drop the island's own meshes get - the two share a model space.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_gate.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ),
			textureOverrides: signTextures );
	}

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );
	}
}
