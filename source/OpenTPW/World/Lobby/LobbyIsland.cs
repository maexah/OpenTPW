using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	/// <summary>
	/// Where the lobby camera looks while this island is the one on show: the island itself,
	/// raised by the height its script asks for. The four parks differ a lot here - 12.5 for the
	/// jungle against 38 for hallow - so this is not something the camera can assume.
	/// </summary>
	public Vector3 CameraTarget => Position + (Vector3.Up * Script.CameraHeight);

	/// <summary>This island's place in the lobby's running order, taken from its script.</summary>
	public int Index => Script.Index;

	/// <summary>
	/// Everything this park's script says - its weather included. Read once here and then read
	/// back by whatever needs it; <see cref="LobbyWeather"/> takes the rain, lightning and sky
	/// off whichever island the camera is currently on, which is how the original does it too.
	/// </summary>
	public LobbyScript Script { get; }

	/// <summary>The park's name as the player sees it, painted on the gate's sign.</summary>
	public string ParkName { get; }

	/// <summary>
	/// What each park is actually called.
	///
	/// A park's ISLAND() line does carry a name, but for three of the four it is the backend one
	/// - "Fantasy", "Halloween", "Space" - and only the jungle's happens to be what the player is
	/// shown. The displayed names are not in the shipped data at all: decompressing all 312 WADs
	/// and searching those, the loose language files and the executable turns up "Lost Kingdom"
	/// and no trace of the other three. So this table is ours rather than the game's, and a park
	/// outside these four falls back to whatever its script says.
	///
	/// All four are two words, which is what puts one on each of the sign's two panels.
	/// </summary>
	private static readonly Dictionary<string, string> DisplayNames = new( StringComparer.OrdinalIgnoreCase )
	{
		["Jungle"] = "Lost Kingdom",
		["Fantasy"] = "Wonder Land",
		["Hallow"] = "Halloween World",
		["Space"] = "Space Zone"
	};

	private readonly LobbyModel _model;

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		Script = LobbyScript.Read( themeName );

		ParkName = DisplayNames.TryGetValue( themeName, out var displayName )
			? displayName
			: Script.ParkName;

		// Offered to both models below: the sign panels are meshes of the island for the jungle
		// and hallow, but of the gate for fantasy and space, and whichever model names sign1 and
		// sign2 is the one that takes them.
		var sign = BuildSign( modelPrefix, ParkName );

		// An island's meshes sit 2.5 units below the origin it is placed at.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_isle.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ),
			textureOverrides: sign );

		// An island brings its gate with it, the way a park's script names both models on one
		// line. Entity.All keeps hold of it.
		_ = new LobbyGate( Position, themeName, sign );

		// ...and whatever its script says flies around it: ten of each butterfly for the jungle,
		// fifty bats for hallow, nothing at all for the other two.
		foreach ( var mesh in Script.FlyingMeshes )
			LobbyFlyer.Spawn( mesh, this );
	}

	/// <summary>
	/// The sign's two panels, or null to leave the model's placeholder textures in place.
	///
	/// The .sgn sits beside the model that carries the sign meshes and is named after it, which
	/// is the island for the jungle and hallow but the gate for fantasy and space - so both are
	/// tried rather than assuming either.
	/// </summary>
	private static IReadOnlyDictionary<string, Texture>? BuildSign( string modelPrefix, string parkName )
	{
		if ( string.IsNullOrEmpty( parkName ) )
			return null;

		foreach ( var model in new[] { "isle", "gate" } )
		{
			if ( !SignTexture.TryBuild( $"lobby/terrain/{modelPrefix}_{model}.sgn", parkName, out var left, out var right ) )
				continue;

			Log.Info( $"{modelPrefix}: painted '{parkName}' onto the {modelPrefix}_{model} sign" );

			return new Dictionary<string, Texture>( StringComparer.OrdinalIgnoreCase )
			{
				["sign1"] = left!,
				["sign2"] = right!
			};
		}

		Log.Warning( $"{modelPrefix}: no usable sign file - the sign keeps its placeholder texture" );
		return null;
	}

	protected override void OnUpdate()
	{
		_model.Update( Time.Delta );
	}
}
