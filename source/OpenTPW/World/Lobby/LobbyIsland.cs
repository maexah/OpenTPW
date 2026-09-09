using System.Globalization;
using System.Numerics;

namespace OpenTPW;

public sealed class LobbyIsland : Entity
{
	/// <summary>
	/// Fallback for a park whose script cannot be read - the height the lobby camera used for
	/// every island before it started asking.
	/// </summary>
	private const float DefaultCameraHeight = 12.5f;

	/// <summary>
	/// Where the lobby camera looks while this island is the one on show: the island itself,
	/// raised by the height its script asks for. The four parks differ a lot here - 12.5 for the
	/// jungle against 38 for hallow - so this is not something the camera can assume.
	/// </summary>
	public Vector3 CameraTarget => Position + (Vector3.Up * _script.CameraHeight);

	/// <summary>This island's place in the lobby's running order, taken from its script.</summary>
	public int Index => _script.Index;

	/// <summary>The park's display name - "Lost Kingdom" for the jungle. Empty if unreadable.</summary>
	public string ParkName => _script.ParkName;

	private readonly LobbyModel _model;
	private readonly IslandScript _script;

	/// <summary>
	/// The single ISLAND() line in a park's lobby script:
	///
	///     ISLAND(0,"data\lobby\terrain","jun_isle","jun_gate","Lost Kingdom",90.0,12.5)
	///
	/// Its index is the lobby's running order, its fourth quoted field is the name painted on the
	/// sign, and its last number is how far above the island the camera looks. The terrain
	/// directory and the two model names are paths this class and LobbyGate still build by hand,
	/// and the number before the last one looks like a heading - none of those are read yet.
	/// </summary>
	private readonly record struct IslandScript( int Index, string ParkName, float CameraHeight );

	public LobbyIsland( Vector3 _position, string themeName )
	{
		Position = _position;

		var modelPrefix = themeName[0..3];
		_script = ReadScript( themeName );

		// An island's meshes sit 2.5 units below the origin it is placed at.
		_model = new LobbyModel(
			$"lobby/terrain/{modelPrefix}_isle.md2",
			"lobby/terrain/textures",
			Position - new Vector3( 0, 0, 2.5f ),
			textureOverrides: BuildSign( modelPrefix, _script.ParkName ) );
	}

	/// <summary>
	/// Reads the park's ISLAND() line - see <see cref="IslandScript"/> - from the lobby script the
	/// game ships, rather than keeping the same facts as literals here. The jungle's sign reads
	/// "Lost Kingdom", which nothing in the theme name or the model prefix would tell you.
	///
	/// A park whose script is missing or malformed falls back to defaults and simply keeps its
	/// placeholder sign.
	/// </summary>
	private static IslandScript ReadScript( string themeName )
	{
		var fallback = new IslandScript( 0, string.Empty, DefaultCameraHeight );

		using var stream = FileSystem.OpenRead( $"lobby/{themeName.ToLowerInvariant()}.txt" );

		if ( stream == null )
			return fallback;

		using var reader = new StreamReader( stream );

		while ( reader.ReadLine() is { } line )
		{
			var trimmed = line.TrimStart();

			// ISLANDFOV and ISLANDCAMERAPOSITION share the prefix but not the bracket.
			if ( !trimmed.StartsWith( "ISLAND(", StringComparison.OrdinalIgnoreCase ) )
				continue;

			// Quoted fields land on the odd indices, so a full line has nine pieces: the index
			// ahead of the first quote, four quoted fields, and the trailing numbers.
			var pieces = trimmed.Split( '"' );

			if ( pieces.Length < 9 )
				continue;

			_ = int.TryParse( pieces[0]["ISLAND(".Length..].Trim( ',', ' ' ), out var index );

			var numbers = pieces[8].Trim( ',', ')', ' ' ).Split( ',' );

			// Invariant culture because the script writes 12.5 whatever the machine's locale does.
			var height = numbers.Length > 0
				&& float.TryParse( numbers[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed )
					? parsed
					: DefaultCameraHeight;

			return new IslandScript( index, pieces[7], height );
		}

		return fallback;
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
